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
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            #region implementation
            // Default to Swagger UI if no return URL is provided
            returnUrl ??= Url.Content("~/swagger/");

            // Handle authentication error from the provider
            if (remoteError != null)
            {
                return RedirectToAction(nameof(LoginFailure));
            }

            // Get the login info from the external provider
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                // No login info available - likely the user cancelled the login
                return RedirectToAction(nameof(LoginFailure));
            }

            // Attempt to sign in with the external provider
            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider, 
                info.ProviderKey, 
                isPersistent: false, 
                bypassTwoFactor: true);

            if (result.Succeeded)
            {
                // User already has an account linked to this provider
                // Update any authentication tokens
                await UpdateExternalAuthenticationTokensAsync(info);
                
                // Redirect to the original return URL
                return LocalRedirect(returnUrl);
            }
            
            if (result.IsLockedOut)
            {
                // User account is locked out
                return RedirectToAction(nameof(Lockout));
            }
            else
            {
                // User does not have an account or it's not linked to this provider
                // Extract user information from the claims
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                var name = info.Principal.FindFirstValue(ClaimTypes.Name) ??
                           info.Principal.FindFirstValue(ClaimTypes.GivenName) ??
                           email?.Split('@')[0]; // Fallback to username from email

                // Ensure we have the necessary user info
                if (email == null || name == null)
                {
                    Console.WriteLine("External login failed: Email or Name claim not found.");
                    
                    // Log available claims for debugging
                    foreach (var claim in info.Principal.Claims)
                    {
                        Console.WriteLine($"Claim Type: {claim.Type}, Value: {claim.Value}");
                    }
                    
                    // Return with error message
                    return RedirectToAction(
                        nameof(LoginFailure), 
                        new { Message = "Required user information (email, name) not provided by external login." });
                }

                // Check if user already exists with this email
                var user = await _userManager.FindByEmailAsync(email);
                IdentityResult identityResult;

                if (user == null)
                {
                    // Create a new user account
                    user = new User
                    {
                        UserName = email,                      // Standard Identity property
                        Email = email,                         // Standard Identity property
                        PrimaryEmail = email,                  // Custom property
                        EmailConfirmed = true,                 // Auto-confirm for external logins
                        DisplayName = name,                    // Custom property
                        CanonicalUsername = email.ToLowerInvariant(),
                        Timezone = "UTC",                      // Default timezone
                        Locale = "en-US",                      // Default locale
                        CreatedAt = DateTime.UtcNow,
                        SecurityStamp = Guid.NewGuid().ToString()
                    };
                    
                    // Create the user in the database
                    identityResult = await _userManager.CreateAsync(user);
                }
                else
                {
                    // User exists, just need to link the external login
                    identityResult = IdentityResult.Success;
                }

                if (identityResult.Succeeded)
                {
                    // Add the external login to the user
                    identityResult = await _userManager.AddLoginAsync(user, info);
                    
                    if (identityResult.Succeeded)
                    {
                        // Sign in the user
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        
                        // Update authentication tokens
                        await UpdateExternalAuthenticationTokensAsync(info);
                        
                        // Redirect to the original return URL
                        return LocalRedirect(returnUrl);
                    }
                }

                // If we get here, either creating the user or linking the external login failed
                foreach (var error in identityResult.Errors)
                {
                    Console.WriteLine($"Error creating/linking user: {error.Description}");
                }
                
                return RedirectToAction(nameof(LoginFailure));
            }
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
                return Ok(new { 
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
