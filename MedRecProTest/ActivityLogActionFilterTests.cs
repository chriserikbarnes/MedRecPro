using MedRecPro.Filters;
using MedRecPro.Models;
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
    /// The filter persists through <see cref="IServiceScopeFactory"/> inside an
    /// unawaited <c>Task.Run</c>. Tests synchronize deterministically by having
    /// the mocked <see cref="IActivityLogService"/> complete a
    /// <see cref="TaskCompletionSource{TResult}"/> that the test awaits with a
    /// timeout, so no sleeps or polling are involved.
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
            var log = await harness.WaitForLogAsync();

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
            var log = await harness.WaitForLogAsync();

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
            var log = await harness.WaitForLogAsync();

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
            var log = await harness.WaitForLogAsync();

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
            var log = await harness.WaitForLogAsync();

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
        /// The scope factory throws inside the fire-and-forget task; the mocked
        /// logger's Error-level call is the deterministic completion signal.
        /// </remarks>
        /// <seealso cref="ActivityLogActionFilter.OnActionExecutionAsync"/>
        [TestMethod]
        public async Task ActivityLogActionFilter_OnActionExecutionAsync_PersistenceFailure_SwallowsAndLogsError()
        {
            #region implementation
            // Arrange - CreateScope throws inside Task.Run.
            var errorLogged = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var scopeFactory = new Mock<IServiceScopeFactory>();
            scopeFactory.Setup(f => f.CreateScope())
                .Throws(new InvalidOperationException("scope resolution failed"));

            var logger = new Mock<ILogger<ActivityLogActionFilter>>();
            logger.Setup(l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, type) => true),
                    It.IsAny<Exception?>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()))
                .Callback(() => errorLogged.TrySetResult(true));

            var filter = new ActivityLogActionFilter(
                scopeFactory.Object,
                new Mock<IHttpContextAccessor>().Object,
                logger.Object);

            var actionContext = FilterContextTestHelper.CreateActionContext(
                FilterContextTestHelper.CreateAnonymousHttpContext());
            var executingContext = FilterContextTestHelper.CreateActionExecutingContext(actionContext);
            var next = FilterContextTestHelper.CreateRecordingNext(actionContext);

            // Act - must not throw despite the broken persistence path.
            await filter.OnActionExecutionAsync(executingContext, next.Delegate);

            // Assert - action ran and the failure surfaced only as LogError.
            Assert.AreEqual(1, next.InvocationCount);
            var completed = await Task.WhenAny(errorLogged.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.AreSame(errorLogged.Task, completed, "Expected the swallowed failure to be logged at Error level.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Bundles the mocked scope-factory chain, the capturing activity-log
        /// service, and the filter under test.
        /// </summary>
        /// <remarks>
        /// The mocked <see cref="IActivityLogService"/> completes
        /// <see cref="WaitForLogAsync"/> when the fire-and-forget task inside
        /// the filter persists the log.
        /// </remarks>
        /// <seealso cref="ActivityLogActionFilter"/>
        private sealed class ActivityLogHarness
        {
            #region implementation

            private readonly TaskCompletionSource<ActivityLog> _logSaved =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            /**************************************************************/
            /// <summary>
            /// Gets the filter under test, wired to the mocked scope chain.
            /// </summary>
            public ActivityLogActionFilter Filter { get; }

            /**************************************************************/
            /// <summary>
            /// Initializes the mocked service-scope chain and the filter.
            /// </summary>
            public ActivityLogHarness()
            {
                #region implementation
                var activityLogService = new Mock<IActivityLogService>();
                activityLogService
                    .Setup(s => s.LogActivityAsync(It.IsAny<ActivityLog>()))
                    .Returns(Task.CompletedTask)
                    .Callback<ActivityLog>(log => _logSaved.TrySetResult(log));

                var serviceProvider = new Mock<IServiceProvider>();
                serviceProvider
                    .Setup(sp => sp.GetService(typeof(IActivityLogService)))
                    .Returns(activityLogService.Object);

                var scope = new Mock<IServiceScope>();
                scope.SetupGet(s => s.ServiceProvider).Returns(serviceProvider.Object);

                var scopeFactory = new Mock<IServiceScopeFactory>();
                scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

                Filter = new ActivityLogActionFilter(
                    scopeFactory.Object,
                    new Mock<IHttpContextAccessor>().Object,
                    new Mock<ILogger<ActivityLogActionFilter>>().Object);
                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Awaits the activity log captured by the mocked service, failing
            /// the test if the background persistence never fires.
            /// </summary>
            /// <returns>The persisted <see cref="ActivityLog"/>.</returns>
            public async Task<ActivityLog> WaitForLogAsync()
            {
                #region implementation
                var completed = await Task.WhenAny(_logSaved.Task, Task.Delay(TimeSpan.FromSeconds(5)));
                Assert.AreSame(_logSaved.Task, completed, "Timed out waiting for the fire-and-forget activity log save.");

                return await _logSaved.Task;
                #endregion
            }

            #endregion
        }

        #endregion
    }
}
