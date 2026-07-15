using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Security
{
    /**************************************************************/
    /// <summary>
    /// Shared harness for constructing ASP.NET Core MVC filter pipeline contexts
    /// (HTTP contexts, action contexts, authorization contexts, exception contexts)
    /// used by the MedRecPro filter unit tests.
    /// </summary>
    /// <remarks>
    /// Provides deterministic builders that never touch the network or a live
    /// database. Authenticated principals are created with a
    /// <see cref="ClaimTypes.NameIdentifier"/> claim, matching what
    /// <c>ClaimHelper.GetUserIdFromClaims</c> expects. Route data values
    /// ("controller"/"action") are populated because
    /// <c>ActivityLogActionFilter</c> reads those keys directly from
    /// <see cref="RouteData.Values"/>.
    /// </remarks>
    /// <seealso cref="MedRecPro.Filters.ActivityLogActionFilter"/>
    /// <seealso cref="MedRecPro.Filters.ActorAuthorizationFilter"/>
    /// <seealso cref="MedRecPro.Filters.UserRoleAuthorizationFilter"/>
    /// <seealso cref="MedRecPro.Filters.AuthorizationExceptionFilter"/>
    /// <seealso cref="MedRecPro.Filters.ThrottleCheckFilter"/>
    /// <seealso cref="MedRecPro.Filters.DatabaseLimitFilter"/>
    public static class FilterContextTestHelper
    {
        #region Constants

        /// <summary>
        /// Default controller route value used by the context builders.
        /// </summary>
        public const string DefaultControllerName = "Labels";

        /// <summary>
        /// Default action route value used by the context builders.
        /// </summary>
        public const string DefaultActionName = "Get";

        /// <summary>
        /// Default request path applied to constructed HTTP contexts.
        /// </summary>
        public const string DefaultRequestPath = "/api/test";

        #endregion

        #region HttpContext Builders

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="DefaultHttpContext"/> with a GET request to
        /// <see cref="DefaultRequestPath"/>. When claims are supplied the user
        /// is an authenticated principal; otherwise the principal is anonymous.
        /// </summary>
        /// <param name="claims">Optional claims to attach to an authenticated identity.</param>
        /// <returns>A configured <see cref="DefaultHttpContext"/>.</returns>
        /// <example>
        /// <code>
        /// var ctx = FilterContextTestHelper.CreateHttpContext(new Claim(ClaimTypes.NameIdentifier, "42"));
        /// </code>
        /// </example>
        /// <seealso cref="CreateAnonymousHttpContext"/>
        public static DefaultHttpContext CreateHttpContext(params Claim[] claims)
        {
            #region implementation

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Method = "GET";
            httpContext.Request.Path = DefaultRequestPath;

            if (claims != null && claims.Length > 0)
            {
                // Authenticated principal - authenticationType makes IsAuthenticated true
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestAuth"));
            }

            return httpContext;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an anonymous <see cref="DefaultHttpContext"/> (no claims).
        /// </summary>
        /// <returns>An HTTP context whose user has no identifying claims.</returns>
        /// <remarks>
        /// <see cref="DefaultHttpContext.User"/> is never null, so downstream
        /// claim parsing simply yields no user ID.
        /// </remarks>
        /// <seealso cref="CreateHttpContext"/>
        public static DefaultHttpContext CreateAnonymousHttpContext()
        {
            #region implementation

            return CreateHttpContext();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an authenticated <see cref="DefaultHttpContext"/> carrying a
        /// <see cref="ClaimTypes.NameIdentifier"/> claim for the supplied user ID.
        /// </summary>
        /// <param name="userId">The numeric user ID to place in the claim.</param>
        /// <returns>An HTTP context with an authenticated principal.</returns>
        /// <seealso cref="CreateHttpContext"/>
        public static DefaultHttpContext CreateAuthenticatedHttpContext(long userId)
        {
            #region implementation

            return CreateHttpContext(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a header-rich HTTP context including User-Agent,
        /// X-Forwarded-For, and a remote IP address, optionally authenticated.
        /// </summary>
        /// <param name="userAgent">Value for the User-Agent header.</param>
        /// <param name="forwardedFor">Value for the X-Forwarded-For header, or null to omit.</param>
        /// <param name="remoteIp">Remote IP address for the connection, or null to omit.</param>
        /// <param name="claims">Optional claims for an authenticated identity.</param>
        /// <returns>A configured <see cref="DefaultHttpContext"/>.</returns>
        /// <seealso cref="CreateHttpContext"/>
        public static DefaultHttpContext CreateHeaderRichHttpContext(
            string userAgent,
            string? forwardedFor,
            string? remoteIp,
            params Claim[] claims)
        {
            #region implementation

            var httpContext = CreateHttpContext(claims);

            httpContext.Request.Headers["User-Agent"] = userAgent;

            if (!string.IsNullOrEmpty(forwardedFor))
            {
                httpContext.Request.Headers["X-Forwarded-For"] = forwardedFor;
            }

            if (!string.IsNullOrEmpty(remoteIp))
            {
                httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
            }

            return httpContext;

            #endregion
        }

        #endregion

        #region MVC Context Builders

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="ActionContext"/> with route data populated for
        /// the given controller and action names.
        /// </summary>
        /// <param name="httpContext">The HTTP context to wrap.</param>
        /// <param name="controllerName">Route value stored under "controller".</param>
        /// <param name="actionName">Route value stored under "action".</param>
        /// <returns>An <see cref="ActionContext"/> with an empty <see cref="ActionDescriptor"/>.</returns>
        /// <remarks>
        /// The route values drive <c>ActivityLogActionFilter</c>'s
        /// ControllerName/ActionName capture and its login/logout/register
        /// activity-type detection.
        /// </remarks>
        /// <seealso cref="CreateActionExecutingContext"/>
        public static ActionContext CreateActionContext(
            HttpContext httpContext,
            string? controllerName = DefaultControllerName,
            string? actionName = DefaultActionName)
        {
            #region implementation

            var routeData = new RouteData();

            if (controllerName != null)
            {
                routeData.Values["controller"] = controllerName;
            }

            if (actionName != null)
            {
                routeData.Values["action"] = actionName;
            }

            return new ActionContext(httpContext, routeData, new ActionDescriptor());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="AuthorizationFilterContext"/> over the supplied
        /// HTTP context.
        /// </summary>
        /// <param name="httpContext">The HTTP context carrying the principal under test.</param>
        /// <returns>An authorization filter context with no filters.</returns>
        /// <seealso cref="MedRecPro.Filters.ActorAuthorizationFilter"/>
        /// <seealso cref="MedRecPro.Filters.UserRoleAuthorizationFilter"/>
        public static AuthorizationFilterContext CreateAuthorizationFilterContext(HttpContext httpContext)
        {
            #region implementation

            var actionContext = CreateActionContext(httpContext);
            return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="ActionExecutingContext"/> for action filter tests.
        /// </summary>
        /// <param name="actionContext">The underlying action context.</param>
        /// <param name="actionArguments">Optional action arguments (parameter dictionary).</param>
        /// <returns>An executing context with an anonymous controller instance.</returns>
        /// <seealso cref="CreateActionExecutedContext"/>
        public static ActionExecutingContext CreateActionExecutingContext(
            ActionContext actionContext,
            IDictionary<string, object?>? actionArguments = null)
        {
            #region implementation

            return new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                actionArguments ?? new Dictionary<string, object?>(),
                controller: new object());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="ActionExecutedContext"/>, optionally carrying an
        /// unhandled exception as if the action threw.
        /// </summary>
        /// <param name="actionContext">The underlying action context.</param>
        /// <param name="exception">Optional exception the executed action produced.</param>
        /// <returns>An executed context (ExceptionHandled left false).</returns>
        /// <seealso cref="CreateRecordingNext"/>
        public static ActionExecutedContext CreateActionExecutedContext(
            ActionContext actionContext,
            Exception? exception = null)
        {
            #region implementation

            var executedContext = new ActionExecutedContext(
                actionContext,
                new List<IFilterMetadata>(),
                controller: new object());

            if (exception != null)
            {
                executedContext.Exception = exception;
            }

            return executedContext;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="ExceptionContext"/> carrying the supplied exception,
        /// for exception filter tests.
        /// </summary>
        /// <param name="httpContext">The HTTP context for the failing request.</param>
        /// <param name="exception">The exception under test.</param>
        /// <returns>An exception context with ExceptionHandled false.</returns>
        /// <seealso cref="MedRecPro.Filters.AuthorizationExceptionFilter"/>
        public static ExceptionContext CreateExceptionContext(HttpContext httpContext, Exception exception)
        {
            #region implementation

            var actionContext = CreateActionContext(httpContext);

            return new ExceptionContext(actionContext, new List<IFilterMetadata>())
            {
                Exception = exception
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a recording <see cref="ActionExecutionDelegate"/> wrapper whose
        /// invocation count can be asserted and whose result optionally carries an
        /// exception (simulating an action that threw).
        /// </summary>
        /// <param name="actionContext">The action context the executed context wraps.</param>
        /// <param name="exception">Optional exception carried by the executed context.</param>
        /// <returns>A <see cref="RecordingActionExecutionDelegate"/> instance.</returns>
        /// <example>
        /// <code>
        /// var next = FilterContextTestHelper.CreateRecordingNext(actionContext);
        /// await filter.OnActionExecutionAsync(executingContext, next.Delegate);
        /// Assert.IsTrue(next.WasInvoked);
        /// </code>
        /// </example>
        /// <seealso cref="RecordingActionExecutionDelegate"/>
        public static RecordingActionExecutionDelegate CreateRecordingNext(
            ActionContext actionContext,
            Exception? exception = null)
        {
            #region implementation

            var executedContext = CreateActionExecutedContext(actionContext, exception);
            return new RecordingActionExecutionDelegate(executedContext);

            #endregion
        }

        #endregion

        #region Assertion Helpers

        /**************************************************************/
        /// <summary>
        /// Reads a property value from an anonymous object via reflection.
        /// </summary>
        /// <param name="instance">The anonymous-typed instance (e.g., an ObjectResult.Value).</param>
        /// <param name="propertyName">The property name to read.</param>
        /// <returns>The property value, or fails the test if the property is missing.</returns>
        /// <remarks>
        /// Anonymous types are internal to the MedRecPro assembly, so cross-assembly
        /// <c>dynamic</c> access fails; reflection-based reads work regardless.
        /// </remarks>
        /// <seealso cref="MedRecPro.Filters.AuthorizationExceptionFilter"/>
        public static object? GetAnonymousPropertyValue(object instance, string propertyName)
        {
            #region implementation

            Assert.IsNotNull(instance, "Anonymous response object should not be null.");

            var property = instance.GetType().GetProperty(propertyName);
            Assert.IsNotNull(property, $"Expected anonymous response property '{propertyName}' was not found.");

            return property!.GetValue(instance);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="InvalidOperationException"/> that has been thrown
        /// and caught so its <see cref="Exception.StackTrace"/> is populated.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <returns>An exception with a non-null stack trace.</returns>
        /// <remarks>
        /// A freshly constructed exception has a null StackTrace; tests asserting
        /// stack trace capture need one that actually propagated.
        /// </remarks>
        public static Exception CreateThrownException(string message = "Simulated action failure")
        {
            #region implementation

            try
            {
                throw new InvalidOperationException(message);
            }
            catch (InvalidOperationException ex)
            {
                return ex;
            }

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Wraps an <see cref="ActionExecutionDelegate"/> that records how many times
    /// it was invoked and returns a pre-built <see cref="ActionExecutedContext"/>.
    /// </summary>
    /// <remarks>
    /// Mirrors the MVC pipeline contract: when the simulated action throws, the
    /// delegate still completes normally and the exception rides on
    /// <see cref="ActionExecutedContext.Exception"/>.
    /// </remarks>
    /// <seealso cref="FilterContextTestHelper.CreateRecordingNext"/>
    public sealed class RecordingActionExecutionDelegate
    {
        #region Fields

        private int _invocationCount;

        #endregion

        #region Properties

        /**************************************************************/
        /// <summary>
        /// Gets the number of times the delegate was invoked.
        /// </summary>
        public int InvocationCount => _invocationCount;

        /**************************************************************/
        /// <summary>
        /// Gets a value indicating whether the delegate was invoked at least once.
        /// </summary>
        public bool WasInvoked => _invocationCount > 0;

        /**************************************************************/
        /// <summary>
        /// Gets the executed context returned to the filter under test.
        /// </summary>
        public ActionExecutedContext ExecutedContext { get; }

        /**************************************************************/
        /// <summary>
        /// Gets the <see cref="ActionExecutionDelegate"/> to pass to the filter.
        /// </summary>
        public ActionExecutionDelegate Delegate { get; }

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the recorder around a pre-built executed context.
        /// </summary>
        /// <param name="executedContext">The context returned on each invocation.</param>
        public RecordingActionExecutionDelegate(ActionExecutedContext executedContext)
        {
            #region implementation

            ExecutedContext = executedContext ?? throw new ArgumentNullException(nameof(executedContext));
            Delegate = invokeAsync;

            #endregion
        }

        #endregion

        #region Private Methods

        /**************************************************************/
        /// <summary>
        /// Records the invocation and returns the pre-built executed context.
        /// </summary>
        /// <returns>A completed task carrying <see cref="ExecutedContext"/>.</returns>
        private Task<ActionExecutedContext> invokeAsync()
        {
            #region implementation

            Interlocked.Increment(ref _invocationCount);
            return Task.FromResult(ExecutedContext);

            #endregion
        }

        #endregion
    }
}
