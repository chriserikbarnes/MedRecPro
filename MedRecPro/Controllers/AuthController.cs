using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MedRecPro.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Manages authentication operations for the MedRecPro application, including external provider authentication,
    /// user session management, and access control.
    /// </summary>
    /// <remarks>
    /// This controller handles OAuth-based external authentication flows, user login/logout operations,
    /// and provides endpoints for retrieving authenticated user information. It integrates with ASP.NET Core
    /// Identity to manage user accounts, authentication tokens, and session state through cookie-based authentication.
    /// </remarks>
    /// <seealso cref="User"/>
    /// <seealso cref="Microsoft.AspNetCore.Identity.SignInManager{TUser}"/>
    /// <seealso cref="Microsoft.AspNetCore.Identity.UserManager{TUser}"/>
    [ApiController]
    public class AuthController : ApiControllerBase
    {
        #region Fields
        /**************************************************************/
        /// <summary>
        /// ASP.NET Core Identity sign-in manager for handling authentication operations.
        /// </summary>
        private readonly SignInManager<User> _signInManager;

        /**************************************************************/
        /// <summary>
        /// ASP.NET Core Identity user manager for user account operations.
        /// </summary>
        private readonly UserManager<User> _userManager;

        /**************************************************************/
        /// <summary>
        /// Logger for logging information and errors.
        /// </summary>
        private readonly ILogger<AuthController> _logger;

        /**************************************************************/
        /// <summary>
        /// For reading config elements
        /// </summary>
        private readonly IConfiguration _configuration;

        private readonly string _pkSecret;
        #endregion

        #region Constructor
        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="signInManager">The ASP.NET Core Identity sign-in manager.</param>
        /// <param name="userManager">The ASP.NET Core Identity user manager.</param>
        /// <param name="logger">Logger for logging information and errors.</param>
        public AuthController(SignInManager<User> signInManager, UserManager<User> userManager, ILogger<AuthController> logger, IConfiguration configuration)
        {
            #region implementation
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _configuration = configuration;

            _pkSecret = _configuration["Security:DB:PKSecret"] ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing.");
            if (string.IsNullOrWhiteSpace(_pkSecret))
            {
                throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' cannot be empty.");
            }
            #endregion
        }
        #endregion

        #region Private Methods
        /**************************************************************/
        /// <summary>
        /// Handles authentication for users already linked to external providers.
        /// </summary>
        /// <param name="user">The existing user linked to the external provider.</param>
        /// <param name="info">External login information from the provider.</param>
        /// <param name="returnUrl">URL to redirect to after successful authentication.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An IActionResult indicating the authentication outcome.</returns>
        /// <remarks>
        /// This method ensures the user has a valid username for Identity operations,
        /// attempts to sign in the user, and handles various sign-in results.
        /// </remarks>
        private async Task<IActionResult> handleLinkedExternalUser(
      User user, ExternalLoginInfo info, string returnUrl, CancellationToken cancellationToken)
        {
            #region implementation
            #region ensure username exists
            // Ensure UserName is present for Identity operations
            if (string.IsNullOrEmpty(user.UserName))
            {
                var emailClaimValue = getEmailClaim(info);
                if (!string.IsNullOrEmpty(emailClaimValue))
                {
                    user.UserName = emailClaimValue;
                    // Also populate email if missing
                    if (string.IsNullOrEmpty(user.Email))
                    {
                        user.Email = emailClaimValue;
                    }
                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        // Compile update errors into readable message
                        string updateErrors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                        return RedirectToAction(nameof(LoginFailure), new { Message = $"Error updating user with missing username: {updateErrors}" });
                    }
                }
                else
                {
                    // Edge case: no email claim available to use as username
                    return RedirectToAction(nameof(LoginFailure), new { Message = "Could not determine username for existing linked account as email claim was missing from provider." });
                }
            }
            #endregion

            #region sign in user
            // Attempt external login sign-in now that username is populated
            var signInResult = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                await updateExternalAuthenticationTokensAsync(info); // Persist tokens if needed
                await updateLoginTimestampsAsync(user);
                return LocalRedirect(returnUrl);
            }
            else if (signInResult.IsLockedOut)
            {
                return RedirectToAction(nameof(Lockout));
            }
            else
            {
                // Generic failure message for other sign-in failures
                return RedirectToAction(nameof(LoginFailure), new { Message = "External login failed after attempting to update user information. Please try again or contact support." });
            }
            #endregion
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Updates the user's last login and last activity timestamps after a successful sign-in.
        /// </summary>
        /// <remarks>
        /// Call this only after the sign-in has succeeded (password or external).
        /// Uses <see cref="DateTimeOffset.UtcNow"/> to avoid timezone ambiguity and to ensure
        /// consistent storage across environments and deployments.
        /// </remarks>
        /// <example>
        /// await updateLoginTimestampsAsync(user);
        /// </example>
        /// <seealso cref="User.LastLoginAt"/>
        /// <seealso cref="User.LastActivityAt"/>
        /// <seealso cref="Microsoft.AspNetCore.Identity.UserManager{TUser}.UpdateAsync(TUser)"/>
        private async Task updateLoginTimestampsAsync(User user)
        {
            #region implementation
            // Guard: null user should never happen post sign-in, but be defensive.
            if (user is null)
            {
                _logger.LogWarning("updateLoginTimestampsAsync called with null user.");
                return;
            }

            // Use UTC to keep storage normalized in SQL and avoid daylight-saving confusion.
            var now = Convert.ToDateTime(DateTimeOffset.UtcNow.ToString());

            // Update both “last seen” fields on successful auth.
            user.LastLoginAt = now; // <seealso cref="User.LastLoginAt"/>
            user.LastActivityAt = now; // <seealso cref="User.LastActivityAt"/>

            // Persist via Identity to ensure concurrency stamps and store behaviors are honored.
            var result = await _userManager.UpdateAsync(user); // <seealso cref="Microsoft.AspNetCore.Identity.UserManager{TUser}.UpdateAsync(TUser)"/>

            if (!result.Succeeded)
            {
                // Inline log helps surface mapping or store issues in dev without crashing login.
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Failed to update login timestamps for user {UserId}: {Errors}", user.Id, errors);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles authentication for users not yet linked to external providers.
        /// </summary>
        /// <param name="info">External login information from the provider.</param>
        /// <param name="returnUrl">URL to redirect to after successful authentication.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>An IActionResult indicating the authentication outcome.</returns>
        /// <remarks>
        /// This method either creates a new user or links an existing user (found by email)
        /// to the external provider, then signs them in.
        /// </remarks>
        private async Task<IActionResult> handleUnlinkedExternalUser(
            ExternalLoginInfo info, string returnUrl, CancellationToken cancellationToken)
        {
            #region implementation
            IdentityResult identityResult;
            User user;

            // Extract email and name from external provider claims
            string email = getEmailClaim(info);
            string name = getNameClaim(info, email);

            #region validate email claim
            if (string.IsNullOrEmpty(email))
            {
                // Cannot proceed without email
                return RedirectToAction(nameof(LoginFailure), new { Message = "Required user information (email) not provided by external login." });
            }
            #endregion

            #region find or create user
            // Try to find an existing user by email
            var existingUserByEmail = await _userManager.FindByEmailAsync(email);

            if (existingUserByEmail == null)
            {
                // New user creation
                user = createUserForExternalLogin(email, name);
                identityResult = await _userManager.CreateAsync(user);
            }
            else
            {
                // Update existing user if needed
                user = existingUserByEmail;
                bool needsUpdate = false;

                // Ensure username is populated
                if (string.IsNullOrEmpty(user.UserName))
                {
                    user.UserName = email;
                    needsUpdate = true;
                }

                // Update display name if different
                if (user.DisplayName != name)
                {
                    user.DisplayName = name;
                    needsUpdate = true;
                }

                identityResult = needsUpdate ? await _userManager.UpdateAsync(user) : IdentityResult.Success;
            }
            #endregion

            #region link and sign in user
            if (identityResult.Succeeded)
            {
                // Link external login to user
                identityResult = await _userManager.AddLoginAsync(user, info);
                if (identityResult.Succeeded)
                {
                    // Sign in the user
                    await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                    await updateExternalAuthenticationTokensAsync(info);
                    return LocalRedirect(returnUrl);
                }
            }
            #endregion

            // Handle errors from any operation above
            string errors = identityResult.Errors != null
                ? string.Join(", ", identityResult.Errors.Select(e => e.Description))
                : "Unknown error during external login processing.";

            return RedirectToAction(nameof(LoginFailure), new { Message = $"Error processing external login: {errors}" });
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the email claim from external login information.
        /// </summary>
        /// <param name="info">External login information containing claims.</param>
        /// <returns>The email address from claims, or empty string if not found.</returns>
        /// <remarks>
        /// Checks multiple claim types as different providers use different claim names.
        /// </remarks>
        private string getEmailClaim(ExternalLoginInfo info)
        {
            #region implementation
            // Try to retrieve the email claim using various keys
            var email = info.Principal.FindFirstValue(ClaimTypes.Email)
                ?? info.Principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            return email ?? string.Empty;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the name claim from external login information.
        /// </summary>
        /// <param name="info">External login information containing claims.</param>
        /// <param name="email">Email address to use as fallback for name extraction.</param>
        /// <returns>The user's name from claims, email prefix, or empty string if not found.</returns>
        /// <remarks>
        /// Attempts to retrieve name from various claim types. Falls back to email prefix
        /// if no name claim is found.
        /// </remarks>
        private string getNameClaim(ExternalLoginInfo info, string email)
        {
            #region implementation
            // Try multiple claim types for name
            var name = info.Principal.FindFirstValue(ClaimTypes.Name)
                ?? info.Principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
                ?? info.Principal.FindFirstValue(ClaimTypes.GivenName)
                ?? info.Principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname");

            // Fallback: extract name from email prefix
            if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(email))
            {
                var idx = email.IndexOf('@');
                name = idx > 0 ? email.Substring(0, idx) : email;
            }
            return name ?? string.Empty;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new user object populated from external login information.
        /// </summary>
        /// <param name="email">Email address for the new user.</param>
        /// <param name="name">Display name for the new user.</param>
        /// <returns>A new User object with appropriate defaults for external login.</returns>
        /// <remarks>
        /// Sets sensible defaults including confirmed email (standard for external logins)
        /// and generates a new security stamp.
        /// </remarks>
        private User createUserForExternalLogin(string email, string name)
        {
            #region implementation
            // Normalize email to lowercase
            var lcEmail = email.ToLowerInvariant();
            return new User
            {
                UserName = lcEmail,
                Email = lcEmail,
                PrimaryEmail = lcEmail,
                EmailConfirmed = true,  // Standard practice for external logins
                DisplayName = name,
                CanonicalUsername = lcEmail,
                Timezone = "UTC",  // Sensible default
                Locale = "en-US",  // Sensible default
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString()  // Important for Identity
            };
            #endregion
        }

#if DEBUG
        /**************************************************************/
        /// <summary>
        /// Logs all claims from external login provider for debugging purposes.
        /// </summary>
        /// <param name="info">External login information containing claims to log.</param>
        /// <remarks>
        /// This method is only available in DEBUG builds and helps troubleshoot
        /// claim-related issues with external authentication providers.
        /// </remarks>
        private void logExternalClaims(ExternalLoginInfo info)
        {
            #region implementation
            // Log all external claims for debugging
            System.Diagnostics.Debug.WriteLine("External Login Claims:");
            foreach (var claim in info.Principal.Claims)
            {
                System.Diagnostics.Debug.WriteLine($"Claim Type: {claim.Type}, Value: {claim.Value}");
            }
            #endregion
        }
#endif
        #endregion

        #region Swagger Authentication Endpoints
        /**************************************************************/
        /// <summary>
        /// Placeholder endpoint for Swagger UI's OAuth2 authorization URL.
        /// </summary>
        /// <returns>An OK result with instructions for initiating login.</returns>
        /// <remarks>
        /// This endpoint doesn't perform any authentication but serves as a valid
        /// relative URL for Swagger's AuthorizationUrl configuration. The actual
        /// login initiation happens via the provider-specific endpoints.
        /// </remarks>
        [HttpGet("external-login")]
        public IActionResult ExternalLoginSwagger()
        {
            #region implementation
            // This is just a placeholder for Swagger's AuthorizationUrl
            return Ok("Initiate login via /api/auth/login/{provider}");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Placeholder endpoint for Swagger UI's OAuth2 token URL.
        /// </summary>
        /// <returns>A BadRequest result explaining this endpoint is not used.</returns>
        /// <remarks>
        /// This endpoint serves as a placeholder for Swagger's TokenUrl configuration.
        /// In a cookie-based authentication flow, a separate token endpoint is not needed.
        /// </remarks>
        [HttpPost("token-placeholder")]
        public IActionResult TokenPlaceholder()
        {
            #region implementation
            return BadRequest("Token endpoint not applicable for cookie-based external provider flow.");
            #endregion
        }
        #endregion

        #region External Authentication
        /**************************************************************/
        /// <summary>
        /// Initiates the external login flow for a specific authentication provider.
        /// </summary>
        /// <param name="provider">The name of the authentication provider (e.g., Google).</param>
        /// <returns>A Challenge result that redirects to the external provider.</returns>
        /// <remarks>
        /// This endpoint configures the authentication properties, including the callback URL,
        /// and issues a challenge to the specified external provider. The user will be redirected
        /// to the provider's login page.
        /// </remarks>
        /// <example>
        /// GET /api/auth/login/Google
        /// </example>
        [HttpGet("login/{provider}")]
        [ProducesResponseType(503)]
        public IActionResult LoginExternalProvider(string provider)
        {
            #region implementation

            var extAuthEnabled = _configuration.GetValue<bool>("FeatureFlags:ExternalAuthEnabled", true);

            if (!extAuthEnabled)
            {
                return StatusCode(503, new
                {
                    error = "Exnternal auth functionality is currently disabled"
                });
            }

            // Path to Swagger UI (environment-aware based on build configuration)
#if DEBUG
            var swaggerPath = "/swagger/index.html"; // Local development
#else
            var swaggerPath = "/api/swagger/index.html"; // Azure production
#endif

            // Configure the redirect URL for after successful external authentication
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { ReturnUrl = swaggerPath });

            // Configure the external authentication properties
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);

            // Challenge the specified provider, which will redirect to the provider's login page
            return Challenge(properties, provider);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles the callback from external authentication providers after login attempt.
        /// </summary>
        /// <param name="returnUrl">The URL to redirect to after successful login.</param>
        /// <param name="remoteError">Error information from the external provider, if any.</param>
        /// <param name="cancellationToken">For stoping operations</param>
        /// <returns>A redirect to either the return URL or an error page.</returns>
        /// <remarks>
        /// This endpoint is automatically called by the external provider after the user 
        /// completes the authentication process. It handles various scenarios:
        /// 1. Authentication failure (redirects to LoginFailure)
        /// 2. Successful login for existing user (redirects to returnUrl)
        /// 3. Successful login for new user (creates account, links provider, then redirects)
        /// 4. Account lockout (redirects to Lockout)
        /// </remarks>
        [HttpGet("external-logincallback")]
        public async Task<IActionResult> ExternalLoginCallback(
            string? returnUrl = null, string? remoteError = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            #region implementation

            returnUrl = returnUrl ?? Url.Content("~/swagger/");

            // Handle errors from remote authentication provider
            if (remoteError != null)
            {
                _logger.LogWarning("External login error: {RemoteError}", remoteError);

                return RedirectToAction(nameof(LoginFailure), new { Message = $"Error from external provider: {remoteError}" });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                _logger.LogWarning("External login information is null.");

                return RedirectToAction(nameof(LoginFailure), new { Message = "Error loading external login information." });
            }

#if DEBUG
            logExternalClaims(info);
#endif

            // Try to find a user linked to this external login
            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

            if (user != null)
            {
                // Existing user linked to this login
                return await handleLinkedExternalUser(user, info, returnUrl, cancellationToken);
            }

            // No user by login, check for email and create/link if needed
            return await handleUnlinkedExternalUser(info, returnUrl, cancellationToken);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores external authentication tokens for the user.
        /// </summary>
        /// <param name="info">The external login information containing authentication tokens.</param>
        /// <remarks>
        /// This method saves tokens provided by the external authentication provider
        /// (like access tokens, refresh tokens, etc.) to the user's authentication data.
        /// These tokens can be used later to access the provider's APIs on behalf of the user.
        /// </remarks>
        private async Task updateExternalAuthenticationTokensAsync(ExternalLoginInfo info)
        {
            #region implementation
            // Only proceed if there are tokens to store
            if (info.AuthenticationTokens != null && info.AuthenticationTokens.Any())
            {
                // Find the user by their external login info
                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

                if (user != null)
                {
                    // Store each authentication token for the user
                    foreach (var token in info.AuthenticationTokens)
                    {
                        await _userManager.SetAuthenticationTokenAsync(
                            user,
                            info.LoginProvider,
                            token.Name,
                            token.Value);
                    }
                }
            }
            #endregion
        }
        #endregion

        #region Error Handling
        /**************************************************************/
        /// <summary>
        /// Handles failed login attempts.
        /// </summary>
        /// <param name="message">Optional error message explaining the reason for the failure.</param>
        /// <returns>A BadRequest result with the error message.</returns>
        /// <remarks>
        /// This endpoint provides a standardized way to handle and report various
        /// login failure scenarios, such as missing claims, authentication errors, etc.
        /// </remarks>
        [HttpGet("loginfailure")]
        public IActionResult LoginFailure(string? message = null)
        {
            #region implementation
            return BadRequest($"External login failed. {message ?? string.Empty}");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles requests for locked-out user accounts.
        /// </summary>
        /// <returns>A Forbidden status code with a lockout message.</returns>
        /// <remarks>
        /// This endpoint is called when a user attempts to log in but their account is locked out,
        /// typically due to too many failed login attempts or administrative action.
        /// </remarks>
        [HttpGet("lockout")]
        public IActionResult Lockout()
        {
            #region implementation
            return StatusCode(StatusCodes.Status403Forbidden, "User account locked out.");
            #endregion
        }
        #endregion

        #region User Management
        /**************************************************************/
        /// <summary>
        /// Retrieves information about the currently authenticated user.
        /// </summary>
        /// <returns>
        /// An OK result with the user's ID, name, and claims if authenticated;
        /// otherwise, an Unauthorized result.
        /// </returns>
        /// <remarks>
        /// This endpoint requires the user to be authenticated. It returns basic
        /// information about the current user, including all claims associated with
        /// their identity.
        /// </remarks>
        [HttpGet("user")]
        [Authorize] // Requires the user to be logged in (via cookie)
        public IActionResult GetUser()
        {
            #region implementation
            if (User.Identity?.IsAuthenticated == true)
            {
                // Extract claims to return in the response
                // withholding the id value
                var claims = User.Claims
                    .Select(c => new { c.Type, c.Value })
                    .Where(d => !string.IsNullOrEmpty(d.Type)
                        && !d.Type.Contains("nameidentifier", StringComparison.CurrentCultureIgnoreCase));

                // Return user information
                return Ok(new
                {
                    encryptedUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)?.Encrypt(_pkSecret, StringCipher.EncryptionStrength.Fast),
                    Name = User.Identity.Name,
                    Claims = claims
                });
            }

            // This should not happen if [Authorize] is working correctly
            return Unauthorized();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Logs out the current user.
        /// </summary>
        /// <returns>An OK result confirming successful logout.</returns>
        /// <remarks>
        /// This endpoint signs the user out by clearing the authentication cookie.
        /// Only authenticated users can access this endpoint.
        /// </remarks>
        [HttpPost("logout")]
        [Authorize] // Only logged-in users can log out
        public async Task<IActionResult> Logout()
        {
            #region implementation
            // Sign out using the Identity SignInManager
            await _signInManager.SignOutAsync();

            // Return confirmation message
            return Ok(new { Message = "Logged out successfully." });
            #endregion
        }
        #endregion

        #region Authentication Redirects
        /**************************************************************/
        /// <summary>
        /// Handles redirect requests for login operations.
        /// </summary>
        /// <returns>An Unauthorized result with instructions for initiating login.</returns>
        /// <remarks>
        /// This endpoint is a fallback for handling redirect operations during the
        /// cookie authentication flow. It's usually not called directly by clients.
        /// </remarks>
        [HttpGet("login")]
        public IActionResult HandleLoginRedirect()
        {
            #region implementation
            return Unauthorized("Authentication required. Please initiate login via /api/auth/login/{provider}.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles requests redirected due to insufficient permissions.
        /// </summary>
        /// <returns>A Forbid result with an access denied message.</returns>
        /// <remarks>
        /// This endpoint is called when a user is authenticated but lacks the
        /// necessary authorization to access a protected resource.
        /// </remarks>
        [HttpGet("accessdenied")]
        public IActionResult HandleAccessDenied()
        {
            #region implementation
            return Forbid("Access Denied.");
            #endregion
        }
        #endregion
    }
}
