using MedRecPro.Filters;
using MedRecPro.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecProTest.Unit.Security
{
    /**************************************************************/
    /// <summary>
    /// Exercises the Azure database throttle filter surface: the two
    /// IFilterFactory attributes, the blocking <see cref="ThrottleCheckFilter"/>,
    /// and the delaying <see cref="DatabaseLimitFilter"/>.
    /// </summary>
    /// <remarks>
    /// All throttle state comes from a Moq <see cref="IThrottleStateService"/>,
    /// so no Azure metrics, monitors, or network calls are involved. Delay
    /// tests advance <see cref="FakeTimeProvider"/> through the production
    /// delay seam, so no wall-clock waiting is required.
    /// </remarks>
    /// <seealso cref="DatabaseIntensiveAttribute"/>
    /// <seealso cref="DatabaseLimitAttribute"/>
    /// <seealso cref="ThrottleCheckFilter"/>
    /// <seealso cref="DatabaseLimitFilter"/>
    /// <seealso cref="IThrottleStateService"/>
    [TestClass]
    [TestCategory("Unit")]
    public class AzureThrottleFilterTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies DatabaseIntensiveAttribute.CreateInstance resolves its
        /// dependencies from the service provider and returns a
        /// <see cref="ThrottleCheckFilter"/>.
        /// </summary>
        /// <seealso cref="DatabaseIntensiveAttribute.CreateInstance"/>
        [TestMethod]
        public void DatabaseIntensiveAttribute_CreateInstance_ReturnsThrottleCheckFilter()
        {
            #region implementation
            // Arrange
            var attribute = new DatabaseIntensiveAttribute(OperationCriticality.NonCritical);
            var provider = createServiceProvider(ThrottleLevel.None, 0);

            // Act
            var filter = attribute.CreateInstance(provider);

            // Assert
            Assert.IsInstanceOfType(filter, typeof(ThrottleCheckFilter));
            Assert.AreEqual(OperationCriticality.NonCritical, attribute.Criticality);
            Assert.IsFalse(attribute.IsReusable, "Filter factory should create a fresh instance per request.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the DatabaseIntensiveAttribute default criticality is
        /// Normal and that CreateInstance throws when the throttle state
        /// service is not registered.
        /// </summary>
        /// <seealso cref="DatabaseIntensiveAttribute.CreateInstance"/>
        [TestMethod]
        public void DatabaseIntensiveAttribute_CreateInstance_MissingRegistration_Throws()
        {
            #region implementation
            // Arrange - provider without IThrottleStateService.
            var attribute = new DatabaseIntensiveAttribute();
            var provider = new ServiceCollection().BuildServiceProvider();

            // Assert default criticality first.
            Assert.AreEqual(OperationCriticality.Normal, attribute.Criticality);

            // Act + Assert - GetRequiredService failure surfaces as InvalidOperationException.
            Assert.ThrowsException<InvalidOperationException>(() => attribute.CreateInstance(provider));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies DatabaseLimitAttribute.CreateInstance returns a
        /// <see cref="DatabaseLimitFilter"/> and honors the default Wait value.
        /// </summary>
        /// <seealso cref="DatabaseLimitAttribute.CreateInstance"/>
        [TestMethod]
        public void DatabaseLimitAttribute_CreateInstance_ReturnsDatabaseLimitFilter()
        {
            #region implementation
            // Arrange
            var attribute = new DatabaseLimitAttribute(OperationCriticality.Critical) { Wait = 25 };
            var provider = createServiceProvider(ThrottleLevel.None, 0);

            // Act
            var filter = attribute.CreateInstance(provider);

            // Assert
            Assert.IsInstanceOfType(filter, typeof(DatabaseLimitFilter));
            Assert.AreEqual(OperationCriticality.Critical, attribute.Criticality);
            Assert.AreEqual(25, attribute.Wait);
            Assert.AreEqual(100, DatabaseLimitAttribute.DefaultWaitMs, "Documented default base wait should stay stable.");
            Assert.IsFalse(attribute.IsReusable);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a Normal-criticality action passes through untouched while
        /// the throttle level is below the Aggressive blocking threshold.
        /// </summary>
        /// <seealso cref="ThrottleCheckFilter.OnActionExecuting"/>
        [TestMethod]
        public void ThrottleCheckFilter_OnActionExecuting_NormalCriticalityBelowThreshold_AllowsAction()
        {
            #region implementation
            // Arrange - Moderate is below Normal's Aggressive threshold.
            var filter = createThrottleCheckFilter(ThrottleLevel.Moderate, 82.0, OperationCriticality.Normal);
            var context = createExecutingContext(out var httpContext);

            // Act
            filter.OnActionExecuting(context);

            // Assert - no short-circuit result and no retry header.
            Assert.IsNull(context.Result, "Action should not be blocked below the criticality threshold.");
            Assert.IsFalse(httpContext.Response.Headers.ContainsKey("Retry-After"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a Normal-criticality action is blocked with a 503 result
        /// and Retry-After header once the throttle level reaches Aggressive.
        /// </summary>
        /// <remarks>
        /// The 503 body is an anonymous object internal to MedRecPro, so the
        /// fields are read through reflection.
        /// </remarks>
        /// <seealso cref="ThrottleCheckFilter.OnActionExecuting"/>
        [TestMethod]
        public void ThrottleCheckFilter_OnActionExecuting_AboveThreshold_Returns503WithRetryAfter()
        {
            #region implementation
            // Arrange
            var filter = createThrottleCheckFilter(ThrottleLevel.Aggressive, 92.456, OperationCriticality.Normal);
            var context = createExecutingContext(out var httpContext);

            // Act
            filter.OnActionExecuting(context);

            // Assert - 503 ObjectResult with the documented body shape.
            var result = context.Result as ObjectResult;
            Assert.IsNotNull(result, "Blocking should set an ObjectResult.");
            Assert.AreEqual(StatusCodes.Status503ServiceUnavailable, result!.StatusCode);
            Assert.AreEqual("1800", httpContext.Response.Headers["Retry-After"].ToString(),
                "Aggressive level should advertise a 1800 second retry window.");

            Assert.AreEqual("Aggressive",
                FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "throttleLevel"));
            Assert.AreEqual(92.46,
                FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "percentUsed"));
            Assert.AreEqual(1800,
                FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "retryAfterSeconds"));
            Assert.AreEqual("state description",
                FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "message"));
            Assert.IsNotNull(FilterContextTestHelper.GetAnonymousPropertyValue(result.Value!, "error"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the criticality-versus-level blocking boundaries: each
        /// criticality blocks at its documented threshold and passes one level
        /// below it.
        /// </summary>
        /// <seealso cref="ThrottleCheckFilter.OnActionExecuting"/>
        [DataTestMethod]
        [DataRow(OperationCriticality.NonCritical, ThrottleLevel.Warning, false)]
        [DataRow(OperationCriticality.NonCritical, ThrottleLevel.Moderate, true)]
        [DataRow(OperationCriticality.Normal, ThrottleLevel.Moderate, false)]
        [DataRow(OperationCriticality.Normal, ThrottleLevel.Aggressive, true)]
        [DataRow(OperationCriticality.Critical, ThrottleLevel.Critical, false)]
        [DataRow(OperationCriticality.Critical, ThrottleLevel.CostLimit, true)]
        public void ThrottleCheckFilter_OnActionExecuting_BoundaryMatrix_BlocksAtDocumentedThreshold(
            OperationCriticality criticality, ThrottleLevel level, bool expectBlocked)
        {
            #region implementation
            // Arrange
            var filter = createThrottleCheckFilter(level, 88.0, criticality);
            var context = createExecutingContext(out _);

            // Act
            filter.OnActionExecuting(context);

            // Assert
            if (expectBlocked)
            {
                Assert.IsNotNull(context.Result, $"{criticality} should block at {level}.");
            }
            else
            {
                Assert.IsNull(context.Result, $"{criticality} should pass at {level}.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies OnActionExecuted is a no-op: it neither throws nor mutates
        /// the executed context.
        /// </summary>
        /// <seealso cref="ThrottleCheckFilter.OnActionExecuted"/>
        [TestMethod]
        public void ThrottleCheckFilter_OnActionExecuted_DoesNotMutateContext()
        {
            #region implementation
            // Arrange
            var filter = createThrottleCheckFilter(ThrottleLevel.CostLimit, 111.0, OperationCriticality.Critical);
            var httpContext = FilterContextTestHelper.CreateHttpContext();
            var actionContext = FilterContextTestHelper.CreateActionContext(httpContext);
            var executedContext = FilterContextTestHelper.CreateActionExecutedContext(actionContext);

            // Act
            filter.OnActionExecuted(executedContext);

            // Assert - context untouched even at the most severe level.
            Assert.IsNull(executedContext.Result);
            Assert.IsNull(executedContext.Exception);
            Assert.IsFalse(httpContext.Response.Headers.ContainsKey("Retry-After"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the throttle check filter constructor rejects null
        /// dependencies.
        /// </summary>
        /// <seealso cref="ThrottleCheckFilter"/>
        [TestMethod]
        public void ThrottleCheckFilter_Constructor_NullDependencies_Throw()
        {
            #region implementation
            // Act + Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new ThrottleCheckFilter(null!, NullLogger<ThrottleCheckFilter>.Instance, OperationCriticality.Normal));
            Assert.ThrowsException<ArgumentNullException>(() =>
                new ThrottleCheckFilter(new Mock<IThrottleStateService>().Object, null!, OperationCriticality.Normal));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies DatabaseLimitFilter forwards straight to the action with no
        /// delay headers when the throttle level is None.
        /// </summary>
        /// <seealso cref="DatabaseLimitFilter.OnActionExecutionAsync"/>
        [TestMethod]
        public async Task DatabaseLimitFilter_OnActionExecutionAsync_NoThrottle_CallsNextWithoutDelayHeaders()
        {
            #region implementation
            // Arrange
            var filter = createDatabaseLimitFilter(ThrottleLevel.None, OperationCriticality.Normal, baseWaitMs: 100);
            var context = createExecutingContext(out var httpContext);
            var next = FilterContextTestHelper.CreateRecordingNext(context);

            // Act
            await filter.OnActionExecutionAsync(context, next.Delegate);

            // Assert - next invoked once, no throttle headers emitted.
            Assert.AreEqual(1, next.InvocationCount);
            Assert.IsFalse(httpContext.Response.Headers.ContainsKey("X-Throttle-Delay-Ms"));
            Assert.IsFalse(httpContext.Response.Headers.ContainsKey("X-Throttle-Level"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a small base wait at Warning level produces the delay
        /// headers and still invokes the action.
        /// </summary>
        /// <seealso cref="DatabaseLimitFilter.OnActionExecutionAsync"/>
        [TestMethod]
        public async Task DatabaseLimitFilter_OnActionExecutionAsync_ThrottleWithSmallWait_AddsDelayHeadersAndCallsNext()
        {
            #region implementation
            // Arrange - Warning multiplier is 1, so a 1 ms base wait delays 1 ms.
            var timeProvider = new FakeTimeProvider();
            var filter = createDatabaseLimitFilter(
                ThrottleLevel.Warning, OperationCriticality.Normal, baseWaitMs: 1, timeProvider);
            var context = createExecutingContext(out var httpContext);
            var next = FilterContextTestHelper.CreateRecordingNext(context);

            // Act
            var execution = filter.OnActionExecutionAsync(context, next.Delegate);
            Assert.IsFalse(execution.IsCompleted, "The throttled action should await the configured delay.");
            timeProvider.Advance(TimeSpan.FromMilliseconds(1));
            await execution;

            // Assert
            Assert.AreEqual(1, next.InvocationCount, "The filter delays but never blocks.");
            Assert.AreEqual("1", httpContext.Response.Headers["X-Throttle-Delay-Ms"].ToString());
            Assert.AreEqual("Warning", httpContext.Response.Headers["X-Throttle-Level"].ToString());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the delay multiplier math: base wait 2 ms at Aggressive
        /// (multiplier 4) advertises an 8 ms delay.
        /// </summary>
        /// <seealso cref="DatabaseLimitFilter.OnActionExecutionAsync"/>
        [TestMethod]
        public async Task DatabaseLimitFilter_OnActionExecutionAsync_AggressiveLevel_MultipliesBaseWait()
        {
            #region implementation
            // Arrange
            var timeProvider = new FakeTimeProvider();
            var filter = createDatabaseLimitFilter(
                ThrottleLevel.Aggressive, OperationCriticality.Normal, baseWaitMs: 2, timeProvider);
            var context = createExecutingContext(out var httpContext);
            var next = FilterContextTestHelper.CreateRecordingNext(context);

            // Act
            var execution = filter.OnActionExecutionAsync(context, next.Delegate);
            Assert.IsFalse(execution.IsCompleted, "The throttled action should await the configured delay.");
            timeProvider.Advance(TimeSpan.FromMilliseconds(8));
            await execution;

            // Assert
            Assert.AreEqual("8", httpContext.Response.Headers["X-Throttle-Delay-Ms"].ToString());
            Assert.AreEqual("Aggressive", httpContext.Response.Headers["X-Throttle-Level"].ToString());
            Assert.AreEqual(1, next.InvocationCount);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies Critical-criticality operations skip the delay until the
        /// level reaches Aggressive.
        /// </summary>
        /// <seealso cref="DatabaseLimitFilter.OnActionExecutionAsync"/>
        [TestMethod]
        public async Task DatabaseLimitFilter_OnActionExecutionAsync_CriticalOperationAtModerate_SkipsDelay()
        {
            #region implementation
            // Arrange - Critical operations only slow down at Aggressive or worse.
            var filter = createDatabaseLimitFilter(ThrottleLevel.Moderate, OperationCriticality.Critical, baseWaitMs: 50);
            var context = createExecutingContext(out var httpContext);
            var next = FilterContextTestHelper.CreateRecordingNext(context);

            // Act
            await filter.OnActionExecutionAsync(context, next.Delegate);

            // Assert
            Assert.AreEqual(1, next.InvocationCount);
            Assert.IsFalse(httpContext.Response.Headers.ContainsKey("X-Throttle-Delay-Ms"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a zero base wait short-circuits the delay branch entirely.
        /// </summary>
        /// <seealso cref="DatabaseLimitFilter.OnActionExecutionAsync"/>
        [TestMethod]
        public async Task DatabaseLimitFilter_OnActionExecutionAsync_ZeroBaseWait_NoDelayHeaders()
        {
            #region implementation
            // Arrange
            var filter = createDatabaseLimitFilter(ThrottleLevel.CostLimit, OperationCriticality.Normal, baseWaitMs: 0);
            var context = createExecutingContext(out var httpContext);
            var next = FilterContextTestHelper.CreateRecordingNext(context);

            // Act
            await filter.OnActionExecutionAsync(context, next.Delegate);

            // Assert
            Assert.AreEqual(1, next.InvocationCount);
            Assert.IsFalse(httpContext.Response.Headers.ContainsKey("X-Throttle-Delay-Ms"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the DatabaseLimitFilter constructor rejects a negative base
        /// wait.
        /// </summary>
        /// <seealso cref="DatabaseLimitFilter"/>
        [TestMethod]
        public void DatabaseLimitFilter_Constructor_NegativeWait_ThrowsArgumentOutOfRange()
        {
            #region implementation
            // Arrange
            var throttleState = new Mock<IThrottleStateService>().Object;

            // Act + Assert
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new DatabaseLimitFilter(throttleState, NullLogger<DatabaseLimitFilter>.Instance,
                    OperationCriticality.Normal, baseWaitMs: -1, timeProvider: TimeProvider.System));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a service provider carrying a mocked throttle state service
        /// and the loggers the filter factories resolve.
        /// </summary>
        /// <param name="level">Throttle level the mock reports.</param>
        /// <param name="percentUsed">Usage percentage the mock reports.</param>
        /// <returns>A built <see cref="IServiceProvider"/>.</returns>
        /// <seealso cref="DatabaseIntensiveAttribute.CreateInstance"/>
        /// <seealso cref="DatabaseLimitAttribute.CreateInstance"/>
        private static IServiceProvider createServiceProvider(ThrottleLevel level, double percentUsed)
        {
            #region implementation
            var services = new ServiceCollection();
            services.AddSingleton(createThrottleState(level, percentUsed));
            services.AddSingleton<ILogger<ThrottleCheckFilter>>(NullLogger<ThrottleCheckFilter>.Instance);
            services.AddSingleton<ILogger<DatabaseLimitFilter>>(NullLogger<DatabaseLimitFilter>.Instance);
            services.AddSingleton<TimeProvider>(TimeProvider.System);

            return services.BuildServiceProvider();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a mocked <see cref="IThrottleStateService"/> reporting the
        /// supplied level and usage.
        /// </summary>
        /// <param name="level">Throttle level to report.</param>
        /// <param name="percentUsed">Usage percentage to report.</param>
        /// <returns>The mock's object instance.</returns>
        private static IThrottleStateService createThrottleState(ThrottleLevel level, double percentUsed)
        {
            #region implementation
            var mock = new Mock<IThrottleStateService>();
            mock.SetupGet(s => s.CurrentLevel).Returns(level);
            mock.SetupGet(s => s.PercentUsed).Returns(percentUsed);
            mock.Setup(s => s.GetStateDescription()).Returns("state description");

            return mock.Object;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="ThrottleCheckFilter"/> bound to a mocked state
        /// service.
        /// </summary>
        /// <param name="level">Throttle level to report.</param>
        /// <param name="percentUsed">Usage percentage to report.</param>
        /// <param name="criticality">Operation criticality under test.</param>
        /// <returns>The configured filter.</returns>
        private static ThrottleCheckFilter createThrottleCheckFilter(
            ThrottleLevel level, double percentUsed, OperationCriticality criticality)
        {
            #region implementation
            return new ThrottleCheckFilter(
                createThrottleState(level, percentUsed),
                NullLogger<ThrottleCheckFilter>.Instance,
                criticality);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="DatabaseLimitFilter"/> bound to a mocked state
        /// service.
        /// </summary>
        /// <param name="level">Throttle level to report.</param>
        /// <param name="criticality">Operation criticality under test.</param>
        /// <param name="baseWaitMs">Base wait in milliseconds.</param>
        /// <returns>The configured filter.</returns>
        private static DatabaseLimitFilter createDatabaseLimitFilter(
            ThrottleLevel level,
            OperationCriticality criticality,
            int baseWaitMs,
            TimeProvider? timeProvider = null)
        {
            #region implementation
            return new DatabaseLimitFilter(
                createThrottleState(level, 88.0),
                NullLogger<DatabaseLimitFilter>.Instance,
                criticality,
                baseWaitMs,
                timeProvider ?? TimeProvider.System);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext"/>
        /// over a fresh HTTP context and exposes that HTTP context for header
        /// assertions.
        /// </summary>
        /// <param name="httpContext">The HTTP context backing the pipeline contexts.</param>
        /// <returns>The executing context.</returns>
        /// <seealso cref="FilterContextTestHelper"/>
        private static Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext createExecutingContext(
            out DefaultHttpContext httpContext)
        {
            #region implementation
            httpContext = FilterContextTestHelper.CreateHttpContext();
            var actionContext = FilterContextTestHelper.CreateActionContext(httpContext);

            return FilterContextTestHelper.CreateActionExecutingContext(actionContext);
            #endregion
        }

        #endregion
    }
}
