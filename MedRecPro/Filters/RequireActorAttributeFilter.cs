using System;
using System.Collections.Generic;
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
using static MedRecPro.Models.Constant;

namespace MedRecPro.Filters
{
    /**************************************************************/
    /// <summary>
    /// Attribute that restricts access to controller actions based on the user's actor type permissions.
    /// Apply this attribute to controllers or actions to require specific actor types.
    /// </summary>
    /// <remarks>
    /// This attribute creates an instance of <see cref="ActorAuthorizationFilter"/> which
    /// performs the actual authorization check. The filter validates that the authenticated
    /// user has at least one permission with an <see cref="ActorType"/> matching the specified actors.
    /// 
    /// The attribute supports multiple actor types - the user only needs to have ONE permission
    /// with a matching actor type to gain access (OR logic).
    /// 
    /// Actor type comparison is case-insensitive and supports both string names and enum values.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Require SystemAdmin OR LabelAdmin actor type
    /// [RequireActor("SystemAdmin", "LabelAdmin")]
    /// [HttpGet("labels/manage")]
    /// public IActionResult ManageLabels() { ... }
    /// 
    /// // Require only SystemAdmin actor type
    /// [RequireActor("SystemAdmin")]
    /// [HttpDelete("system/reset")]
    /// public IActionResult ResetSystem() { ... }
    /// 
    /// // Using nameof for type safety
    /// [RequireActor(nameof(ActorType.Reviewer), nameof(ActorType.Approver))]
    /// [HttpPost("review/submit")]
    /// public IActionResult SubmitReview() { ... }
    /// </code>
    /// </example>
    /// <seealso cref="ActorAuthorizationFilter"/>
    /// <seealso cref="ActorAuthorizationException"/>
    /// <seealso cref="ActorType"/>
    /// <seealso cref="Permission"/>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RequireActorAttribute : TypeFilterAttribute
    {
        /**************************************************************/
        /// <summary>
        /// Gets the actor types that are allowed to access the decorated resource.
        /// </summary>
        public string[] AllowedActors { get; }

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="RequireActorAttribute"/> class.
        /// </summary>
        /// <param name="allowedActors">
        /// One or more actor type names that are permitted to access the resource.
        /// Use <see cref="ActorType"/> enum names for consistency.
        /// </param>
        /// <exception cref="ArgumentException">Thrown when no actors are specified.</exception>
        /// <example>
        /// <code>
        /// [RequireActor("SystemAdmin", "LabelAdmin")]
        /// public class LabelManagementController : ControllerBase { ... }
        /// </code>
        /// </example>
        public RequireActorAttribute(params string[] allowedActors)
            : base(typeof(ActorAuthorizationFilter))
        {
            #region implementation
            if (allowedActors == null || allowedActors.Length == 0)
            {
                throw new ArgumentException("At least one actor type must be specified.", nameof(allowedActors));
            }

            AllowedActors = allowedActors;
            // Pass the allowed actors to the filter constructor
            Arguments = new object[] { allowedActors };
            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Authorization filter that validates user actor type permissions before allowing access to a resource.
    /// </summary>
    /// <remarks>
    /// This filter is instantiated by <see cref="RequireActorAttribute"/> and performs the following checks:
    /// <list type="number">
    ///     <item>Validates that the user is authenticated (has valid claims)</item>
    ///     <item>Retrieves the user from the database using the encrypted ID from claims</item>
    ///     <item>Decrypts the user's permissions and checks for matching actor types</item>
    /// </list>
    /// 
    /// Special handling: Users with <see cref="ActorType.SystemAdmin"/> in their permissions
    /// are granted access regardless of the specified required actors.
    /// 
    /// If any check fails, the appropriate <see cref="AuthorizationException"/> is thrown,
    /// which should be caught by the global exception handler.
    /// </remarks>
    /// <seealso cref="RequireActorAttribute"/>
    /// <seealso cref="ActorAuthorizationException"/>
    /// <seealso cref="IPermissionService"/>
    public class ActorAuthorizationFilter : IAsyncAuthorizationFilter
    {
        #region Private Fields

        private readonly string[] _allowedActors;
        private readonly UserDataAccess _userDataAccess;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<ActorAuthorizationFilter> _logger;
        private readonly string _pkSecret;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="ActorAuthorizationFilter"/> class.
        /// </summary>
        /// <param name="allowedActors">The actor types that are permitted to access the resource.</param>
        /// <param name="userDataAccess">The data access service for retrieving user information.</param>
        /// <param name="permissionService">The permission service for decrypting and validating permissions.</param>
        /// <param name="configuration">The application configuration for encryption settings.</param>
        /// <param name="logger">The logger for diagnostic output.</param>
        /// <exception cref="ArgumentNullException">Thrown when required dependencies are null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when encryption key is not configured.</exception>
        public ActorAuthorizationFilter(
            string[] allowedActors,
            UserDataAccess userDataAccess,
            IPermissionService permissionService,
            IConfiguration configuration,
            ILogger<ActorAuthorizationFilter> logger)
        {
            #region implementation
            _allowedActors = allowedActors ?? throw new ArgumentNullException(nameof(allowedActors));
            _userDataAccess = userDataAccess ?? throw new ArgumentNullException(nameof(userDataAccess));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
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
        /// <exception cref="ActorAuthorizationException">
        /// Thrown when the user is not authenticated or does not have the required actor type permissions.
        /// </exception>
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            #region implementation
            _logger.LogDebug("ActorAuthorizationFilter executing for actors: {Actors}", string.Join(", ", _allowedActors));

            // Step 1: Get the encrypted user ID from claims
            string? encryptedUserId = getEncryptedIdFromClaim(context);

            if (string.IsNullOrEmpty(encryptedUserId))
            {
                _logger.LogWarning("Authorization failed: Unable to determine user ID from authentication context.");
                throw new ActorAuthorizationException(_allowedActors, isUnauthenticated: true);
            }

            // Step 2: Retrieve the user from the database
            User? user;
            try
            {
                user = await _userDataAccess.GetByIdAsync(encryptedUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user from database during actor authorization.");
                throw new AuthorizationException(
                    "An error occurred while validating user authorization.",
                    ex,
                    statusCode: 500,
                    errorCode: "AUTH_ACTOR_ERROR");
            }

            if (user == null)
            {
                _logger.LogWarning("Authorization failed: User not found in database.");
                throw new ActorAuthorizationException(_allowedActors, isUnauthenticated: true);
            }

            // Step 3: Decrypt and validate user permissions
            List<Permission> permissions = new List<Permission>();

            if (!string.IsNullOrEmpty(user.UserPermissions))
            {
                if (!_permissionService.TryDecrypt(user.UserPermissions, out permissions))
                {
                    _logger.LogWarning("Failed to decrypt user permissions for user ID: {UserId}", encryptedUserId);
                    // Continue with empty permissions - will fail authorization below
                }
            }

            // Step 4: Check if user has SystemAdmin actor (bypass check)
            if (hasActorType(permissions, ActorType.SystemAdmin))
            {
                _logger.LogDebug("Authorization granted: User has SystemAdmin actor type (bypasses all actor checks).");
                return;
            }

            // Step 5: Check if user has any of the required actor types
            bool isAuthorized = false;
            foreach (var actorString in _allowedActors)
            {
                if (Enum.TryParse<ActorType>(actorString, ignoreCase: true, out ActorType actorType))
                {
                    if (hasActorType(permissions, actorType))
                    {
                        isAuthorized = true;
                        _logger.LogDebug("Authorization granted: User has required actor type '{ActorType}'.", actorType);
                        break;
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid actor type specified: '{ActorString}'", actorString);
                }
            }

            if (!isAuthorized)
            {
                _logger.LogWarning(
                    "Authorization failed: User does not have any of the required actor types [{AllowedActors}].",
                    string.Join(", ", _allowedActors));

                throw new ActorAuthorizationException(_allowedActors);
            }
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

        /**************************************************************/
        /// <summary>
        /// Checks if the permissions list contains any permission with the specified actor type.
        /// </summary>
        /// <param name="permissions">The list of permissions to check.</param>
        /// <param name="actorType">The actor type to look for.</param>
        /// <returns>True if a permission with the actor type exists; otherwise, false.</returns>
        /// <seealso cref="Permission"/>
        /// <seealso cref="ActorType"/>
        private bool hasActorType(List<Permission> permissions, ActorType actorType)
        {
            #region implementation
            if (permissions == null || !permissions.Any())
            {
                return false;
            }

            return permissions.Any(p => p.Actor == actorType);
            #endregion
        }

        #endregion
    }
}
