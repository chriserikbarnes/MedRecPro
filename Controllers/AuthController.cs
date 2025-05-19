using MedRecPro.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MedRecPro.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        #region Fields
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        #endregion

        #region Constructor
        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="signInManager">The ASP.NET Core Identity sign-in manager.</param>
        /// <param name="userManager">The ASP.NET Core Identity user manager.</param>
        public AuthController(SignInManager<User> signInManager, UserManager<User> userManager)
        {
            #region implementation
            _signInManager = signInManager;
            _userManager = userManager;
            #endregion
        }
        #endregion

        #region Private Methods
        private async Task<IActionResult> handleLinkedExternalUser(
      User user, ExternalLoginInfo info, string returnUrl, CancellationToken cancellationToken)
        {
            // Ensure UserName is present for Identity operations
            if (string.IsNullOrEmpty(user.UserName))
            {
                var emailClaimValue = getEmailClaim(info);
                if (!string.IsNullOrEmpty(emailClaimValue))
                {
                    user.UserName = emailClaimValue;
                    if (string.IsNullOrEmpty(user.Email))
                    {
                        user.Email = emailClaimValue;
                    }
                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        string updateErrors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                        return RedirectToAction(nameof(LoginFailure), new { Message = $"Error updating user with missing username: {updateErrors}" });
                    }
                }
                else
                {
                    return RedirectToAction(nameof(LoginFailure), new { Message = "Could not determine username for existing linked account as email claim was missing from provider." });
                }
            }

            // Attempt external login sign-in now that username is populated
            var signInResult = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                await UpdateExternalAuthenticationTokensAsync(info); // Custom: persists tokens if needed
                return LocalRedirect(returnUrl);
            }
            else if (signInResult.IsLockedOut)
            {
                return RedirectToAction(nameof(Lockout));
            }
            else
            {
                return RedirectToAction(nameof(LoginFailure), new { Message = "External login failed after attempting to update user information. Please try again or contact support." });
            }
        }

        private async Task<IActionResult> handleUnlinkedExternalUser(
            ExternalLoginInfo info, string returnUrl, CancellationToken cancellationToken)
        {
            IdentityResult identityResult;
            User user;

            string email = getEmailClaim(info);
            string name = getNameClaim(info, email);

            if (string.IsNullOrEmpty(email))
            {
                // Claims missing email: log and fail
                return RedirectToAction(nameof(LoginFailure), new { Message = "Required user information (email) not provided by external login." });
            }

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
                user = existingUserByEmail;
                bool needsUpdate = false;
                if (string.IsNullOrEmpty(user.UserName))
                {
                    user.UserName = email;
                    needsUpdate = true;
                }
                if (user.DisplayName != name)
                {
                    user.DisplayName = name;
                    needsUpdate = true;
                }
                identityResult = needsUpdate ? await _userManager.UpdateAsync(user) : IdentityResult.Success;
            }

            if (identityResult.Succeeded)
            {
                // Link external login to user and sign in
                identityResult = await _userManager.AddLoginAsync(user, info);
                if (identityResult.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                    await UpdateExternalAuthenticationTokensAsync(info);
                    return LocalRedirect(returnUrl);
                }
            }

            string errors = identityResult.Errors != null
                ? string.Join(", ", identityResult.Errors.Select(e => e.Description))
                : "Unknown error during external login processing.";

            return RedirectToAction(nameof(LoginFailure), new { Message = $"Error processing external login: {errors}" });
        }

        private string getEmailClaim(ExternalLoginInfo info)
        {
            // Try to retrieve the email claim using various keys
            var email = info.Principal.FindFirstValue(ClaimTypes.Email)
                ?? info.Principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            return email ?? string.Empty;
        }

        private string getNameClaim(ExternalLoginInfo info, string email)
        {
            // Retrieve name from claims, or fallback to email prefix
            var name = info.Principal.FindFirstValue(ClaimTypes.Name)
                ?? info.Principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
                ?? info.Principal.FindFirstValue(ClaimTypes.GivenName)
                ?? info.Principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname");
            if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(email))
            {
                var idx = email.IndexOf('@');
                name = idx > 0 ? email.Substring(0, idx) : email;
            }
            return name ?? string.Empty;
        }

        private User createUserForExternalLogin(string email, string name)
        {
            // Create a user object populated with info from external login
            var lcEmail = email.ToLowerInvariant();
            return new User
            {
                UserName = lcEmail,
                Email = lcEmail,
                PrimaryEmail = lcEmail,
                EmailConfirmed = true,
                DisplayName = name,
                CanonicalUsername = lcEmail,
                Timezone = "UTC",
                Locale = "en-US",
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString()
            };
        }

#if DEBUG
        private void logExternalClaims(ExternalLoginInfo info)
        {
            // Log all external claims for debugging
            System.Diagnostics.Debug.WriteLine("External Login Claims:");
            foreach (var claim in info.Principal.Claims)
            {
                System.Diagnostics.Debug.WriteLine($"Claim Type: {claim.Type}, Value: {claim.Value}");
            }
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
        public IActionResult LoginExternalProvider(string provider)
        {
            #region implementation
            // Path to Swagger UI (adjust as needed)
            var swaggerPath = "/swagger/index.html";

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
                return RedirectToAction(nameof(LoginFailure), new { Message = $"Error from external provider: {remoteError}" });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
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
        private async Task UpdateExternalAuthenticationTokensAsync(ExternalLoginInfo info)
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
                var claims = User.Claims.Select(c => new { c.Type, c.Value });

                // Return user information
                return Ok(new
                {
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
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
