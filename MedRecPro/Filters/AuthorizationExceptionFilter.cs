using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using MedRecPro.Exceptions;

namespace MedRecPro.Filters
{
    /**************************************************************/
    /// <summary>
    /// Global exception filter that handles <see cref="AuthorizationException"/> and its derived types,
    /// converting them to appropriate HTTP responses with structured error information.
    /// </summary>
    /// <remarks>
    /// This filter should be registered globally in the application to catch authorization exceptions
    /// thrown by <see cref="UserRoleAuthorizationFilter"/> and <see cref="ActorAuthorizationFilter"/>.
    /// 
    /// The filter produces consistent JSON error responses with the following structure:
    /// <code>
    /// {
    ///     "error": "Error message",
    ///     "errorCode": "AUTH_*",
    ///     "statusCode": 401|403|500
    /// }
    /// </code>
    /// 
    /// For <see cref="UserRoleAuthorizationException"/>, additional fields are included:
    /// <code>
    /// {
    ///     "requiredRoles": ["Admin", "User Admin"],
    ///     "actualRole": "User"
    /// }
    /// </code>
    /// 
    /// For <see cref="ActorAuthorizationException"/>, additional fields are included:
    /// <code>
    /// {
    ///     "requiredActors": ["SystemAdmin", "LabelAdmin"]
    /// }
    /// </code>
    /// </remarks>
    /// <example>
    /// Register the filter globally in Program.cs:
    /// <code>
    /// builder.Services.AddControllers(options =>
    /// {
    ///     options.Filters.Add&lt;AuthorizationExceptionFilter&gt;();
    /// });
    /// </code>
    /// 
    /// Or register as a service filter:
    /// <code>
    /// builder.Services.AddScoped&lt;AuthorizationExceptionFilter&gt;();
    /// builder.Services.AddControllers(options =>
    /// {
    ///     options.Filters.AddService&lt;AuthorizationExceptionFilter&gt;();
    /// });
    /// </code>
    /// </example>
    /// <seealso cref="AuthorizationException"/>
    /// <seealso cref="UserRoleAuthorizationException"/>
    /// <seealso cref="ActorAuthorizationException"/>
    public class AuthorizationExceptionFilter : IExceptionFilter
    {
        #region Private Fields

        private readonly ILogger<AuthorizationExceptionFilter> _logger;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationExceptionFilter"/> class.
        /// </summary>
        /// <param name="logger">The logger for diagnostic output.</param>
        public AuthorizationExceptionFilter(ILogger<AuthorizationExceptionFilter> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region IExceptionFilter Implementation

        /**************************************************************/
        /// <summary>
        /// Called when an exception occurs during request processing.
        /// Handles <see cref="AuthorizationException"/> and its derived types.
        /// </summary>
        /// <param name="context">The exception context.</param>
        /// <remarks>
        /// This method sets <see cref="ExceptionContext.ExceptionHandled"/> to true
        /// for handled exceptions to prevent further exception processing.
        /// </remarks>
        public void OnException(ExceptionContext context)
        {
            #region implementation
            // Handle UserRoleAuthorizationException
            if (context.Exception is UserRoleAuthorizationException userRoleEx)
            {
                handleUserRoleException(context, userRoleEx);
                return;
            }

            // Handle ActorAuthorizationException
            if (context.Exception is ActorAuthorizationException actorEx)
            {
                handleActorException(context, actorEx);
                return;
            }

            // Handle base AuthorizationException
            if (context.Exception is AuthorizationException authEx)
            {
                handleAuthorizationException(context, authEx);
                return;
            }

            // Not an authorization exception - let other handlers deal with it
            #endregion
        }

        #endregion

        #region Private Methods

        /**************************************************************/
        /// <summary>
        /// Handles <see cref="UserRoleAuthorizationException"/> by creating an appropriate response.
        /// </summary>
        /// <param name="context">The exception context.</param>
        /// <param name="exception">The user role authorization exception.</param>
        private void handleUserRoleException(ExceptionContext context, UserRoleAuthorizationException exception)
        {
            #region implementation
            _logger.LogWarning(
                exception,
                "User role authorization failed. Required: [{RequiredRoles}], Actual: {ActualRole}",
                string.Join(", ", exception.RequiredRoles),
                exception.ActualRole ?? "N/A");

            var errorResponse = new
            {
                error = exception.Message,
                errorCode = exception.ErrorCode,
                statusCode = exception.StatusCode,
                requiredRoles = exception.RequiredRoles,
                actualRole = exception.ActualRole
            };

            context.Result = new ObjectResult(errorResponse)
            {
                StatusCode = exception.StatusCode
            };

            context.ExceptionHandled = true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles <see cref="ActorAuthorizationException"/> by creating an appropriate response.
        /// </summary>
        /// <param name="context">The exception context.</param>
        /// <param name="exception">The actor authorization exception.</param>
        private void handleActorException(ExceptionContext context, ActorAuthorizationException exception)
        {
            #region implementation
            _logger.LogWarning(
                exception,
                "Actor authorization failed. Required actors: [{RequiredActors}]",
                string.Join(", ", exception.RequiredActors));

            var errorResponse = new
            {
                error = exception.Message,
                errorCode = exception.ErrorCode,
                statusCode = exception.StatusCode,
                requiredActors = exception.RequiredActors
            };

            context.Result = new ObjectResult(errorResponse)
            {
                StatusCode = exception.StatusCode
            };

            context.ExceptionHandled = true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles generic <see cref="AuthorizationException"/> by creating an appropriate response.
        /// </summary>
        /// <param name="context">The exception context.</param>
        /// <param name="exception">The authorization exception.</param>
        private void handleAuthorizationException(ExceptionContext context, AuthorizationException exception)
        {
            #region implementation
            _logger.LogWarning(
                exception,
                "Authorization failed: {Message} (ErrorCode: {ErrorCode})",
                exception.Message,
                exception.ErrorCode);

            var errorResponse = new
            {
                error = exception.Message,
                errorCode = exception.ErrorCode,
                statusCode = exception.StatusCode
            };

            context.Result = new ObjectResult(errorResponse)
            {
                StatusCode = exception.StatusCode
            };

            context.ExceptionHandled = true;
            #endregion
        }

        #endregion
    }
}
