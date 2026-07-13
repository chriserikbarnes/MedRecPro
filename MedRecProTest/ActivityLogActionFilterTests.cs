using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Security.Claims;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Exercises <see cref="ActivityLogActionFilter.OnActionExecutionAsync"/>:
    /// request metadata capture, activity-type mapping, sensitive parameter
    /// filtering, exception detail capture, and the swallow-on-failure
    /// contract of the fire-and-forget persistence path.
    /// </summary>
    /// <remarks>
    /// The filter depends on an injected <see cref="IActivityLogDispatcher"/>.
    /// These tests use a synchronous dispatcher, so persistence assertions are
    /// immediate and require neither polling nor wall-clock timeouts.
    /// </remarks>
    /// <seealso cref="ActivityLogActionFilter"/>
    /// <seealso cref="IActivityLogService"/>
    /// <seealso cref="FilterContextTestHelper"/>
    [TestClass]
    public class ActivityLogActionFilterTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies a successful action produces an activity log carrying the
        /// user ID, route names, HTTP metadata, filtered parameters, and a
        /// Success result.
        /// </summary>
        /// <seealso cref="ActivityLogActionFilter.OnActionExecutionAsync"/>
        [TestMethod]
        public async Task ActivityLogActionFilter_OnActionExecutionAsync_Success_SavesActivityLog()
        {
            #region implementation
            // Arrange - authenticated POST with proxy headers and mixed arguments.
            var harness = new ActivityLogHarness();
            var httpContext = FilterContextTestHelper.CreateHeaderRichHttpContext(
                userAgent: "TestAgent/1.0",
                forwardedFor: "203.0.113.5, 10.0.0.1",
                remoteIp: "192.0.2.10",
                new Claim(ClaimTypes.NameIdentifier, "42"));
            httpContext.Request.Method = "POST";

            var actionContext = FilterContextTestHelper.CreateActionContext(httpContext, "Labels", "Create");
            var executingContext = FilterContextTestHelper.CreateActionExecutingContext(
                actionContext,
                new Dictionary<string, object?>
                {
                    ["labelName"] = "Aspirin",
                    ["password"] = "should-not-appear"
                });
            var next = FilterContextTestHelper.CreateRecordingNext(actionContext);

            // Act
            await harness.Filter.OnActionExecutionAsync(executingContext, next.Delegate);
            var log = harness.LastLog;

            // Assert - action always executes; log captures the request shape.
            Assert.AreEqual(1, next.InvocationCount);
            Assert.AreEqual(42L, log.UserId);
            Assert.AreEqual("Create", log.ActivityType, "POST maps to the Create activity type.");
            Assert.AreEqual("Labels", log.ControllerName);
            Assert.AreEqual("Create", log.ActionName);
            Assert.AreEqual("POST", log.HttpMethod);
            Assert.AreEqual("POST Labels/Create", log.Description);
            Assert.AreEqual("203.0.113.5", log.IpAddress, "First X-Forwarded-For entry wins over the remote IP.");
            Assert.AreEqual("TestAgent/1.0", log.UserAgent);
            Assert.AreEqual(FilterContextTestHelper.DefaultRequestPath, log.RequestPath);
            Assert.AreEqual(200, log.ResponseStatusCode);
            Assert.AreEqual("Success", log.Result);
            Assert.IsNull(log.ErrorMessage);
            Assert.IsNotNull(log.ExecutionTimeMs);
            Assert.IsTrue(log.ExecutionTimeMs >= 0);

            // Sensitive parameters are filtered; benign ones serialize through.
            Assert.IsNotNull(log.RequestParameters);
            StringAssert.Contains(log.RequestParameters, "labelName");
            StringAssert.Contains(log.RequestParameters, "Aspirin");
            Assert.IsFalse(log.RequestParameters!.Contains("should-not-appear"),
                "Password values must never be serialized into the activity log.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies an action that threw produces an Error log carrying the
        /// message, short exception type name, and stack trace.
        /// </summary>
        /// <seealso cref="ActivityLogActionFilter.OnActionExecutionAsync"/>
        [TestMethod]
        public async Task ActivityLogActionFilter_OnActionExecutionAsync_Exception_SavesErrorDetails()
        {
            #region implementation
            // Arrange - the recording delegate returns a context carrying a
            // thrown (stack-trace-bearing) exception, mirroring MVC behavior.
            var harness = new ActivityLogHarness();
            var httpContext = FilterContextTestHelper.CreateAuthenticatedHttpContext(7);
            var actionContext = FilterContextTestHelper.CreateActionContext(httpContext, "Labels", "Import");
            var executingContext = FilterContextTestHelper.CreateActionExecutingContext(actionContext);
            var exception = FilterContextTestHelper.CreateThrownException("Simulated action failure");
            var next = FilterContextTestHelper.CreateRecordingNext(actionContext, exception);

            // Act
            await harness.Filter.OnActionExecutionAsync(executingContext, next.Delegate);
            var log = harness.LastLog;

            // Assert
            Assert.AreEqual("Error", log.Result);
            Assert.AreEqual("Simulated action failure", log.ErrorMessage);
            Assert.AreEqual(nameof(InvalidOperationException), log.ExceptionType);
            Assert.IsNotNull(log.StackTrace, "A propagated exception carries a stack trace worth persisting.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies anonymous GET requests log a null user ID and the Read
        /// activity type.
        /// </summary>
        /// <seealso cref="ActivityLogActionFilter.OnActionExecutionAsync"/>
        [TestMethod]
        public async Task ActivityLogActionFilter_OnActionExecutionAsync_AnonymousGet_LogsNullUserIdAndReadType()
        {
            #region implementation
            // Arrange
            var harness = new ActivityLogHarness();
            var httpContext = FilterContextTestHelper.CreateAnonymousHttpContext();
            var actionContext = FilterContextTestHelper.CreateActionContext(httpContext, "Labels", "Get");
            var executingContext = FilterContextTestHelper.CreateActionExecutingContext(actionContext);
            var next = FilterContextTestHelper.CreateRecordingNext(actionContext);

            // Act
            await harness.Filter.OnActionExecutionAsync(executingContext, next.Delegate);
            var log = harness.LastLog;

            // Assert
            Assert.IsNull(log.UserId, "Anonymous principals have no user-ID claim to record.");
            Assert.AreEqual("Read", log.ActivityType);
            Assert.IsNull(log.RequestParameters, "Empty action arguments serialize as null.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies action names containing "login" map to the Login activity
        /// type regardless of the HTTP method.
        /// </summary>
        /// <seealso cref="ActivityLogActionFilter.OnActionExecutionAsync"/>
        [TestMethod]
        public async Task ActivityLogActionFilter_OnActionExecutionAsync_LoginAction_MapsLoginActivityType()
        {
            #region implementation
            // Arrange - GET would normally map to Read; the action name wins.
            var harness = new ActivityLogHarness();
            var httpContext = FilterContextTestHelper.CreateAnonymousHttpContext();
            var actionContext = FilterContextTestHelper.CreateActionContext(httpContext, "Auth", "Login");
            var executingContext = FilterContextTestHelper.CreateActionExecutingContext(actionContext);
            var next = FilterContextTestHelper.CreateRecordingNext(actionContext);

            // Act
            await harness.Filter.OnActionExecutionAsync(executingContext, next.Delegate);
            var log = harness.LastLog;

            // Assert
            Assert.AreEqual("Login", log.ActivityType);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the all-sensitive-parameters case writes the documented
        /// placeholder instead of serializing anything.
        /// </summary>
        /// <seealso cref="ActivityLogActionFilter.OnActionExecutionAsync"/>
        [TestMethod]
        public async Task ActivityLogActionFilter_OnActionExecutionAsync_AllSensitiveParameters_WritesFilteredPlaceholder()
        {
            #region implementation
            // Arrange - every argument name matches the sensitive-key list.
            var harness = new ActivityLogHarness();
            var httpContext = FilterContextTestHelper.CreateAnonymousHttpContext();
            var actionContext = FilterContextTestHelper.CreateActionContext(httpContext);
            var executingContext = FilterContextTestHelper.CreateActionExecutingContext(
                actionContext,
                new Dictionary<string, object?>
                {
                    ["password"] = "p",
                    ["apiKey"] = "k"
                });
            var next = FilterContextTestHelper.CreateRecordingNext(actionContext);

            // Act
            await harness.Filter.OnActionExecutionAsync(executingContext, next.Delegate);
            var log = harness.LastLog;

            // Assert
            Assert.AreEqual("[All parameters filtered - sensitive data]", log.RequestParameters);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies persistence failures are swallowed: the request pipeline
        /// completes, the action runs, and the filter only logs the error.
        /// </summary>
        /// <remarks>
        /// The production dispatcher catches persistence failures before its
        /// completed task returns, so the error-log assertion is immediate.
        /// </remarks>
        /// <seealso cref="ActivityLogActionFilter.OnActionExecutionAsync"/>
        [TestMethod]
        public async Task ActivityLogActionFilter_OnActionExecutionAsync_PersistenceFailure_SwallowsAndLogsError()
        {
            #region implementation
            // Arrange - the production dispatcher catches a scope failure.
            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(f => f.CreateScope())
                .Throws(new InvalidOperationException("scope resolution failed"));

            var dispatcherLogger = new RecordingLogger<ActivityLogDispatcher>();

            var dispatcher = new ActivityLogDispatcher(scopeFactory.Object, dispatcherLogger);

            var filter = new ActivityLogActionFilter(
                dispatcher,
                new TestUserContextAccessor(),
                TimeProvider.System,
                new Mock<ILogger<ActivityLogActionFilter>>().Object);

            var actionContext = FilterContextTestHelper.CreateActionContext(
                FilterContextTestHelper.CreateAnonymousHttpContext());
            var executingContext = FilterContextTestHelper.CreateActionExecutingContext(actionContext);
            var next = FilterContextTestHelper.CreateRecordingNext(actionContext);

            // Act - queueing must not throw despite the broken persistence path.
            await filter.OnActionExecutionAsync(executingContext, next.Delegate);
            await dispatcher.persistAsync(new ActivityLog());

            // Assert - action ran and the failure surfaced only as LogError.
            Assert.AreEqual(1, next.InvocationCount);
            Assert.IsTrue(dispatcherLogger.LogLevels.Contains(LogLevel.Error));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Bundles an immediate activity-log dispatcher and the filter under test.
        /// </summary>
        /// <remarks>
        /// Its dispatcher captures the log synchronously, letting assertions
        /// follow the filter invocation directly.
        /// </remarks>
        /// <seealso cref="ActivityLogActionFilter"/>
        private sealed class ActivityLogHarness
        {
            #region implementation

            /**************************************************************/
            /// <summary>
            /// Gets the filter under test, wired to the mocked scope chain.
            /// </summary>
            public ActivityLogActionFilter Filter { get; }

            /**************************************************************/
            /// <summary>
            /// Gets the log synchronously dispatched by the filter.
            /// </summary>
            public ActivityLog LastLog { get; private set; } = null!;

            /**************************************************************/
            /// <summary>
            /// Initializes the mocked service-scope chain and the filter.
            /// </summary>
            public ActivityLogHarness()
            {
                #region implementation
                Filter = new ActivityLogActionFilter(
                    new ImmediateActivityLogDispatcher(log => LastLog = log),
                    new TestUserContextAccessor(),
                    TimeProvider.System,
                    new Mock<ILogger<ActivityLogActionFilter>>().Object);
                #endregion
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Captures activity logs in the calling thread for deterministic filter tests.
        /// </summary>
        /// <seealso cref="IActivityLogDispatcher"/>
        private sealed class ImmediateActivityLogDispatcher : IActivityLogDispatcher
        {
            #region implementation

            private readonly Action<ActivityLog> _capture;

            /**************************************************************/
            /// <summary>
            /// Initializes a synchronous dispatcher with its capture callback.
            /// </summary>
            /// <param name="capture">Callback receiving the dispatched log.</param>
            public ImmediateActivityLogDispatcher(Action<ActivityLog> capture)
            {
                _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            }

            /**************************************************************/
            /// <inheritdoc/>
            public void Dispatch(ActivityLog activityLog)
            {
                _capture(activityLog);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records log levels without requiring a dynamic proxy for an internal dispatcher type.
        /// </summary>
        /// <typeparam name="T">The category type associated with this logger.</typeparam>
        private sealed class RecordingLogger<T> : ILogger<T>
        {
            #region implementation

            /// <summary>
            /// Gets the levels recorded by this logger.
            /// </summary>
            public List<LogLevel> LogLevels { get; } = new List<LogLevel>();

            /**************************************************************/
            /// <inheritdoc/>
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            /**************************************************************/
            /// <inheritdoc/>
            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            /**************************************************************/
            /// <inheritdoc/>
            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                LogLevels.Add(logLevel);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Test implementation of the current-user accessor that uses the same claim helper as production.
        /// </summary>
        /// <seealso cref="IUserContextAccessor"/>
        private sealed class TestUserContextAccessor : IUserContextAccessor
        {
            #region implementation

            /**************************************************************/
            /// <inheritdoc/>
            public long? GetCurrentUserId(HttpContext? httpContext = null)
            {
                #region implementation

                return httpContext?.User != null
                    ? ClaimHelper.GetUserIdFromClaims(httpContext.User.Claims)
                    : null;

                #endregion
            }

            #endregion
        }

        #endregion
    }
}
