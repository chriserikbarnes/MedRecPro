using System;

namespace MedRecPro.Exceptions
{
    /**************************************************************/
    /// <summary>
    /// Base exception class for authorization failures in the MedRecPro system.
    /// Provides a foundation for specific authorization exceptions with HTTP status codes.
    /// </summary>
    /// <remarks>
    /// This exception serves as the base class for all authorization-related exceptions.
    /// It carries both an HTTP status code and a detailed error message to facilitate
    /// proper error responses through the global exception handler.
    /// </remarks>
    /// <example>
    /// <code>
    /// try
    /// {
    ///     // Authorization logic
    /// }
    /// catch (AuthorizationException ex)
    /// {
    ///     return StatusCode(ex.StatusCode, new { error = ex.Message });
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="UserRoleAuthorizationException"/>
    /// <seealso cref="ActorAuthorizationException"/>
    public class AuthorizationException : Exception
    {
        #region Properties

        /**************************************************************/
        /// <summary>
        /// Gets the HTTP status code associated with this authorization failure.
        /// </summary>
        /// <remarks>
        /// Common values include:
        /// - 401 (Unauthorized): User is not authenticated
        /// - 403 (Forbidden): User is authenticated but lacks required permissions
        /// </remarks>
        public int StatusCode { get; }

        /**************************************************************/
        /// <summary>
        /// Gets the error code for programmatic identification of the exception type.
        /// </summary>
        /// <remarks>
        /// Error codes follow the pattern "AUTH_{TYPE}_{REASON}" for consistent categorization.
        /// </remarks>
        public string ErrorCode { get; }

        #endregion

        #region Constructors

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationException"/> class
        /// with the specified message, status code, and error code.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="statusCode">The HTTP status code to return. Defaults to 403 (Forbidden).</param>
        /// <param name="errorCode">The programmatic error code. Defaults to "AUTH_FAILURE".</param>
        public AuthorizationException(
            string message,
            int statusCode = 403,
            string errorCode = "AUTH_FAILURE")
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationException"/> class
        /// with the specified message, inner exception, status code, and error code.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        /// <param name="statusCode">The HTTP status code to return. Defaults to 403 (Forbidden).</param>
        /// <param name="errorCode">The programmatic error code. Defaults to "AUTH_FAILURE".</param>
        public AuthorizationException(
            string message,
            Exception innerException,
            int statusCode = 403,
            string errorCode = "AUTH_FAILURE")
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Exception thrown when a user does not have the required role to access a resource.
    /// </summary>
    /// <remarks>
    /// This exception is thrown by the <see cref="MedRecPro.Filters.UserRoleAuthorizationFilter"/>
    /// when the authenticated user's <see cref="MedRecPro.Models.User.UserRole"/> does not match
    /// any of the required roles specified in the <see cref="MedRecPro.Filters.RequireUserRoleAttribute"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// // In a controller action protected by [RequireUserRole("Admin", "User Admin")]
    /// // If user's role is "User", this exception will be thrown automatically
    /// </code>
    /// </example>
    /// <seealso cref="AuthorizationException"/>
    /// <seealso cref="MedRecPro.Filters.RequireUserRoleAttribute"/>
    /// <seealso cref="MedRecPro.Models.UserRole"/>
    public class UserRoleAuthorizationException : AuthorizationException
    {
        #region Properties

        /**************************************************************/
        /// <summary>
        /// Gets the roles that were required for access.
        /// </summary>
        public string[] RequiredRoles { get; }

        /**************************************************************/
        /// <summary>
        /// Gets the user's actual role that failed the check.
        /// </summary>
        public string? ActualRole { get; }

        #endregion

        #region Constructors

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="UserRoleAuthorizationException"/> class.
        /// </summary>
        /// <param name="requiredRoles">The roles that were required for access.</param>
        /// <param name="actualRole">The user's actual role that failed the check. May be null if user not found.</param>
        public UserRoleAuthorizationException(string[] requiredRoles, string? actualRole)
            : base(
                message: buildMessage(requiredRoles, actualRole),
                statusCode: 403,
                errorCode: "AUTH_ROLE_INSUFFICIENT")
        {
            RequiredRoles = requiredRoles ?? Array.Empty<string>();
            ActualRole = actualRole;
        }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="UserRoleAuthorizationException"/> class
        /// for unauthenticated access attempts.
        /// </summary>
        /// <param name="requiredRoles">The roles that were required for access.</param>
        public UserRoleAuthorizationException(string[] requiredRoles)
            : base(
                message: "Authentication required to access this resource.",
                statusCode: 401,
                errorCode: "AUTH_ROLE_UNAUTHENTICATED")
        {
            RequiredRoles = requiredRoles ?? Array.Empty<string>();
            ActualRole = null;
        }

        #endregion

        #region Private Methods

        /**************************************************************/
        /// <summary>
        /// Builds a descriptive error message for the exception.
        /// </summary>
        /// <param name="requiredRoles">The roles that were required.</param>
        /// <param name="actualRole">The user's actual role.</param>
        /// <returns>A formatted error message.</returns>
        private static string buildMessage(string[] requiredRoles, string? actualRole)
        {
            #region implementation
            var rolesDisplay = requiredRoles != null && requiredRoles.Length > 0
                ? string.Join(", ", requiredRoles)
                : "unspecified roles";

            if (string.IsNullOrEmpty(actualRole))
            {
                return $"Access denied. Required role(s): {rolesDisplay}.";
            }

            return $"Access denied. Your role '{actualRole}' does not have permission. Required role(s): {rolesDisplay}.";
            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Exception thrown when a user does not have the required actor type permission to access a resource.
    /// </summary>
    /// <remarks>
    /// This exception is thrown by the <see cref="MedRecPro.Filters.ActorAuthorizationFilter"/>
    /// when the authenticated user's permissions do not include any of the required actor types
    /// specified in the <see cref="MedRecPro.Filters.RequireActorAttribute"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// // In a controller action protected by [RequireActor("SystemAdmin", "LabelAdmin")]
    /// // If user has no permissions with these actor types, this exception will be thrown
    /// </code>
    /// </example>
    /// <seealso cref="AuthorizationException"/>
    /// <seealso cref="MedRecPro.Filters.RequireActorAttribute"/>
    /// <seealso cref="MedRecPro.Models.Constant.ActorType"/>
    public class ActorAuthorizationException : AuthorizationException
    {
        #region Properties

        /**************************************************************/
        /// <summary>
        /// Gets the actor types that were required for access.
        /// </summary>
        public string[] RequiredActors { get; }

        #endregion

        #region Constructors

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="ActorAuthorizationException"/> class.
        /// </summary>
        /// <param name="requiredActors">The actor types that were required for access.</param>
        public ActorAuthorizationException(string[] requiredActors)
            : base(
                message: buildMessage(requiredActors),
                statusCode: 403,
                errorCode: "AUTH_ACTOR_INSUFFICIENT")
        {
            RequiredActors = requiredActors ?? Array.Empty<string>();
        }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="ActorAuthorizationException"/> class
        /// for unauthenticated access attempts.
        /// </summary>
        /// <param name="requiredActors">The actor types that were required for access.</param>
        /// <param name="isUnauthenticated">Indicates this is an authentication failure.</param>
        public ActorAuthorizationException(string[] requiredActors, bool isUnauthenticated)
            : base(
                message: "Authentication required to access this resource.",
                statusCode: 401,
                errorCode: "AUTH_ACTOR_UNAUTHENTICATED")
        {
            RequiredActors = requiredActors ?? Array.Empty<string>();
        }

        #endregion

        #region Private Methods

        /**************************************************************/
        /// <summary>
        /// Builds a descriptive error message for the exception.
        /// </summary>
        /// <param name="requiredActors">The actor types that were required.</param>
        /// <returns>A formatted error message.</returns>
        private static string buildMessage(string[] requiredActors)
        {
            #region implementation
            var actorsDisplay = requiredActors != null && requiredActors.Length > 0
                ? string.Join(", ", requiredActors)
                : "unspecified actor types";

            return $"Access denied. Required actor type(s): {actorsDisplay}.";
            #endregion
        }

        #endregion
    }
}
