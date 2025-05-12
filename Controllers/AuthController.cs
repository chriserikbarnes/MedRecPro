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
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;

        public AuthController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        // This endpoint is targeted by Swagger UI's AuthorizationUrl
        // It doesn't actually *do* anything, Swagger just needs a valid relative URL
        // The actual login initiation happens via the provider-specific endpoints below.
        [HttpGet("external-login")]
        public IActionResult ExternalLoginSwagger()
        {
            // This is just a placeholder for Swagger's AuthorizationUrl.
            // It might return a simple message or documentation hint.
            return Ok("Initiate login via /api/auth/login/{provider}");
        }

        // Placeholder for Swagger's TokenUrl (not used in cookie flow)
        [HttpPost("token-placeholder")]
        public IActionResult TokenPlaceholder()
        {
            return BadRequest("Token endpoint not applicable for cookie-based external provider flow.");
        }

        // Endpoint to initiate the external login flow for a specific provider
        // e.g., GET /api/auth/login/Google
        [HttpGet("login/{provider}")]
        public IActionResult LoginExternalProvider(string provider)
        {
            var swaggerPath = "/swagger/index.html"; // Path to Swagger UI (adjust as needed)
            // Request a redirect to the external login provider
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { ReturnUrl = swaggerPath });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        // Callback endpoint automatically hit by the external provider after successful login
        // Path matches Redirect URI configured with providers (e.g., /signin-google handled by middleware)
        // This action is invoked *after* the external cookie is processed by the middleware.
        [HttpGet("external-logincallback")]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            returnUrl ??= Url.Content("~/swagger/"); // Default redirect to Swagger UI root

            if (remoteError != null)
            {
                // Handle error from external provider
                return RedirectToAction(nameof(LoginFailure)); // Or return an error view/message
            }

            // Get information about the user from the external provider
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                // Handle case where external login info is not available
                return RedirectToAction(nameof(LoginFailure));
            }

            // Sign in the user with this external login provider if they already have a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (result.Succeeded)
            {
                // User already linked and signed in
                // Update any stored tokens if needed (info.AuthenticationTokens)
                await UpdateExternalAuthenticationTokensAsync(info);
                return LocalRedirect(returnUrl); // Redirect back to Swagger UI or original location
            }
            if (result.IsLockedOut)
            {
                return RedirectToAction(nameof(Lockout));
            }
            else
            {
                // --- If the user does not have an account, create one ---
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                var name = info.Principal.FindFirstValue(ClaimTypes.Name) ?? // Use Name claim if available
                           info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? // Fallback to GivenName
                           email?.Split('@')[0]; // Fallback to part of email

                if (email == null || name == null)
                {
                    // Cannot create user without essential info
                    // Maybe redirect to a page asking for more details
                    Console.WriteLine("External login failed: Email or Name claim not found.");
                    foreach (var claim in info.Principal.Claims)
                    {
                        Console.WriteLine($"Claim Type: {claim.Type}, Value: {claim.Value}");
                    }
                    // For Apple, ensure 'email' and 'name' scopes were requested and 'sub' is mapped
                    if (info.LoginProvider == "Apple")
                    {
                        Console.WriteLine("Ensure Apple service is configured to return email/name and scopes requested.");
                    }
                    return RedirectToAction(nameof(LoginFailure), new { Message = "Required user information (email, name) not provided by external login." });
                }

                var user = await _userManager.FindByEmailAsync(email);
                IdentityResult identityResult;

                if (user == null)
                {
                    // Create a new user account
                    user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true }; // Auto-confirm external logins
                    identityResult = await _userManager.CreateAsync(user);
                }
                else
                {
                    // User exists, link the external login
                    identityResult = IdentityResult.Success; // User already exists, just need to add login
                }


                if (identityResult.Succeeded)
                {
                    // Add the external login to the user
                    identityResult = await _userManager.AddLoginAsync(user, info);
                    if (identityResult.Succeeded)
                    {
                        // Sign in the newly created/linked user
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        await UpdateExternalAuthenticationTokensAsync(info); // Store tokens
                        return LocalRedirect(returnUrl);
                    }
                }

                // If creation or adding login failed, handle errors
                // Add errors to ModelState or log them
                foreach (var error in identityResult.Errors)
                {
                    Console.WriteLine($"Error creating/linking user: {error.Description}");
                }
                return RedirectToAction(nameof(LoginFailure));
            }
        }

        // Helper to store external authentication tokens if SaveTokens = true
        private async Task UpdateExternalAuthenticationTokensAsync(ExternalLoginInfo info)
        {
            if (info.AuthenticationTokens != null && info.AuthenticationTokens.Any())
            {
                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (user != null)
                {
                    foreach (var token in info.AuthenticationTokens)
                    {
                        // Store AccessToken, RefreshToken, IdToken etc. as needed
                        await _userManager.SetAuthenticationTokenAsync(user, info.LoginProvider, token.Name, token.Value);
                    }
                }
            }
        }


        [HttpGet("loginfailure")]
        public IActionResult LoginFailure(string? message = null)
        {
            // Return an error response
            return BadRequest($"External login failed. {message ?? string.Empty}");
        }

        [HttpGet("lockout")]
        public IActionResult Lockout()
        {
            // Return a specific error response for lockout
            return StatusCode(StatusCodes.Status403Forbidden, "User account locked out.");
        }

        // Endpoint to get current user info (requires authentication)
        [HttpGet("user")]
        [Authorize] // Requires the user to be logged in (via cookie)
        public IActionResult GetUser()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var claims = User.Claims.Select(c => new { c.Type, c.Value });
                return Ok(new { UserId = User.FindFirstValue(ClaimTypes.NameIdentifier), Name = User.Identity.Name, Claims = claims });
            }
            return Unauthorized(); // Should not happen if [Authorize] is effective
        }

        // Endpoint to log out
        [HttpPost("logout")]
        [Authorize] // Only logged-in users can log out
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync(); // Clears the authentication cookie
            return Ok(new { Message = "Logged out successfully." });
        }

        // Placeholder endpoints for cookie authentication events (if needed, usually not called directly)
        [HttpGet("login")]
        public IActionResult HandleLoginRedirect()
        {
            return Unauthorized("Authentication required. Please initiate login via /api/auth/login/{provider}.");
        }

        [HttpGet("accessdenied")]
        public IActionResult HandleAccessDenied()
        {
            return Forbid("Access Denied.");
        }
    }
}
