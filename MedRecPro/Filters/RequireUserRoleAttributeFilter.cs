using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using MedRecPro.DataAccess;
using MedRecPro.Exceptions;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.Extensions.Configuration;
using static MedRecPro.Models.UserRole;

namespace MedRecPro.Filters
{
    /**************************************************************/
    /// <summary>
    /// Attribute that restricts access to controller actions based on the user's role.
    /// Apply this attribute to controllers or actions to require specific user roles.
    /// </summary>
    /// <remarks>
    /// This attribute creates an instance of <see cref="UserRoleAuthorizationFilter"/> which
    /// performs the actual authorization check. The filter validates that the authenticated
    /// user's <see cref="User.UserRole"/> matches one of the specified allowed roles.
    /// 
    /// The attribute supports multiple roles - the user only needs to match ONE of the
    /// specified roles to gain access (OR logic).
    /// 
    /// Role comparison is case-insensitive.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Require Admin OR User Admin role
    /// [RequireUserRole(UserRole.Admin, UserRole.UserAdmin)]
    /// [HttpGet("admin/settings")]
    /// public IActionResult GetAdminSettings() { ... }
    /// 
    /// // Require only Admin role
    /// [RequireUserRole("Admin")]
    /// [HttpDelete("users/{id}")]
    /// public IActionResult DeleteUser(string id) { ... }
    /// </code>
    /// </example>
    /// <seealso cref="UserRoleAuthorizationFilter"/>
    /// <seealso cref="UserRoleAuthorizationException"/>
    /// <seealso cref="User.UserRole"/>
    /// <seealso cref="MedRecPro.Models.UserRole"/>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RequireUserRoleAttribute : TypeFilterAttribute
    {
        /**************************************************************/
        /// <summary>
        /// Gets the roles that are allowed to access the decorated resource.
        /// </summary>
        public string[] AllowedRoles { get; }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="RequireUserRoleAttribute"/> class.
        /// </summary>
        /// <param name="allowedRoles">
        /// One or more role names that are permitted to access the resource.
        /// Use constants from <see cref="MedRecPro.Models.UserRole"/> for consistency.
        /// </param>
        /// <exception cref="ArgumentException">Thrown when no roles are specified.</exception>
        /// <example>
        /// <code>
        /// [RequireUserRole(UserRole.Admin, UserRole.UserAdmin)]
        /// public class AdminController : ControllerBase { ... }
        /// </code>
        /// </example>
        public RequireUserRoleAttribute(params string[] allowedRoles)
            : base(typeof(UserRoleAuthorizationFilter))
        {
            #region implementation
            if (allowedRoles == null || allowedRoles.Length == 0)
            {
                throw new ArgumentException("At least one role must be specified.", nameof(allowedRoles));
            }

            AllowedRoles = allowedRoles;
            // Pass the allowed roles to the filter constructor
            Arguments = new object[] { allowedRoles };
            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Authorization filter that validates user role membership before allowing access to a resource.
    /// </summary>
    /// <remarks>
    /// This filter is instantiated by <see cref="RequireUserRoleAttribute"/> and performs the following checks:
    /// <list type="number">
    ///     <item>Validates that the user is authenticated (has valid claims)</item>
    ///     <item>Retrieves the user from the database using the encrypted ID from claims</item>
    ///     <item>Compares the user's <see cref="User.UserRole"/> against the allowed roles</item>
    /// </list>
    /// 
    /// If any check fails, the appropriate <see cref="AuthorizationException"/> is thrown,
    /// which should be caught by the global exception handler.
    /// </remarks>
    /// <seealso cref="RequireUserRoleAttribute"/>
    /// <seealso cref="UserRoleAuthorizationException"/>
    public class UserRoleAuthorizationFilter : IAsyncAuthorizationFilter
    {
        #region Private Fields

        private readonly string[] _allowedRoles;
        private readonly UserDataAccess _userDataAccess;
        private readonly ILogger<UserRoleAuthorizationFilter> _logger;
        private readonly string _pkSecret;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="UserRoleAuthorizationFilter"/> class.
        /// </summary>
        /// <param name="allowedRoles">The roles that are permitted to access the resource.</param>
        /// <param name="userDataAccess">The data access service for retrieving user information.</param>
        /// <param name="configuration">The application configuration for encryption settings.</param>
        /// <param name="logger">The logger for diagnostic output.</param>
        /// <exception cref="ArgumentNullException">Thrown when required dependencies are null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when encryption key is not configured.</exception>
        public UserRoleAuthorizationFilter(
            string[] allowedRoles,
            UserDataAccess userDataAccess,
            IConfiguration configuration,
            ILogger<UserRoleAuthorizationFilter> logger)
        {
            #region implementation
            _allowedRoles = allowedRoles ?? throw new ArgumentNullException(nameof(allowedRoles));
            _userDataAccess = userDataAccess ?? throw new ArgumentNullException(nameof(userDataAccess));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _pkSecret = configuration?["Security:DB:PKSecret"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_pkSecret))
            {
                _logger.LogError("Encryption key (Security:DB:PKSecret) is missing or empty in configuration.");
                throw new InvalidOperationException("Encryption key (Security:DB:PKSecret) is missing or empty in configuration.");
            }
            #endregion
        }

        #endregion

        #region IAsyncAuthorizationFilter Implementation

        /**************************************************************/
        /// <summary>
        /// Called early in the filter pipeline to confirm request is authorized.
        /// </summary>
        /// <param name="context">The <see cref="AuthorizationFilterContext"/>.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="UserRoleAuthorizationException">
        /// Thrown when the user is not authenticated or does not have the required role.
        /// </exception>
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            #region implementation
            _logger.LogDebug("UserRoleAuthorizationFilter executing for roles: {Roles}", string.Join(", ", _allowedRoles));

            // Step 1: Get the encrypted user ID from claims
            string? encryptedUserId = getEncryptedIdFromClaim(context);

            if (string.IsNullOrEmpty(encryptedUserId))
            {
                _logger.LogWarning("Authorization failed: Unable to determine user ID from authentication context.");
                throw new UserRoleAuthorizationException(_allowedRoles);
            }

            // Step 2: Retrieve the user from the database
            User? user;
            try
            {
                user = await _userDataAccess.GetByIdAsync(encryptedUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user from database during role authorization.");
                throw new AuthorizationException(
                    "An error occurred while validating user authorization.",
                    ex,
                    statusCode: 500,
                    errorCode: "AUTH_ROLE_ERROR");
            }

            if (user == null)
            {
                _logger.LogWarning("Authorization failed: User not found in database.");
                throw new UserRoleAuthorizationException(_allowedRoles);
            }

            // Step 3: Check if user's role is in the allowed roles
            bool isAuthorized = _allowedRoles.Any(role =>
                !string.IsNullOrEmpty(user.UserRole) &&
                user.UserRole.Equals(role, StringComparison.OrdinalIgnoreCase));

            if (!isAuthorized)
            {
                _logger.LogWarning(
                    "Authorization failed: User role '{ActualRole}' not in allowed roles [{AllowedRoles}].",
                    user.UserRole,
                    string.Join(", ", _allowedRoles));

                throw new UserRoleAuthorizationException(_allowedRoles, user.UserRole);
            }

            _logger.LogDebug(
                "Authorization successful: User role '{UserRole}' matches allowed roles.",
                user.UserRole);
            #endregion
        }

        #endregion

        #region Private Methods

        /**************************************************************/
        /// <summary>
        /// Retrieves the encrypted user ID from the claims of the authenticated user.
        /// </summary>
        /// <param name="context">The authorization filter context containing the HTTP context.</param>
        /// <returns>The encrypted user ID, or null if unable to retrieve.</returns>
        /// <seealso cref="StringCipher"/>
        private string? getEncryptedIdFromClaim(AuthorizationFilterContext context)
        {
            #region implementation
            try
            {
                // Get the authenticated ID from claims
                var idClaim = context.HttpContext.User.Claims
                    .FirstOrDefault(c => c.Type.Contains("NameIdentifier", StringComparison.OrdinalIgnoreCase))
                    ?.Value;

                if (string.IsNullOrEmpty(idClaim) || !long.TryParse(idClaim, out long id) || id <= 0)
                {
                    _logger.LogWarning("Unable to parse user ID from claims. Claim value: {ClaimValue}", idClaim);
                    return null;
                }

                // Encrypt the ID for database lookup
                string encryptedAuthUserId = StringCipher.Encrypt(
                    id.ToString(),
                    _pkSecret,
                    StringCipher.EncryptionStrength.Fast);

                return encryptedAuthUserId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get encrypted ID from claims.");
                return null;
            }
            #endregion
        }

        #endregion
    }
}
