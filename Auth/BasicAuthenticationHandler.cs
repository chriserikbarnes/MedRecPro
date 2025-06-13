using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using MedRecPro.DataAccess;
using MedRecPro.Models; // Required for User model
using MedRecPro.Helpers; // Required for StringCipher

namespace MedRecPro.Security
{
    /// <summary>
    /// Handles Basic Authentication.
    /// </summary>
    public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly UserDataAccess _userDataAccess;
        private readonly IConfiguration _configuration; // To get PkSecret for UpdateLastLoginAsync
        private readonly string _pkSecret; // To get PkSecret for UpdateLastLoginAsync

        // private readonly IConfiguration _configuration; // To get PkSecret for UpdateLastLoginAsync

        public BasicAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            UserDataAccess userDataAccess,
            IConfiguration configuration // Inject IConfiguration
            ) : base(options, logger, encoder)
        {
            _userDataAccess = userDataAccess ?? throw new ArgumentNullException(nameof(userDataAccess));

            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            _pkSecret = _configuration["Security:DB:PKSecret"] ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing.");

            // Check if PKSecret is empty or null
            if (string.IsNullOrWhiteSpace(_pkSecret))
            {
                throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' cannot be empty.");
            }

        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Skip authentication if endpoint has [AllowAnonymous]
            var endpoint = Context.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null)
            {
                return AuthenticateResult.NoResult();
            }

            if (!Request.Headers.ContainsKey("Authorization"))
            {
                Logger.LogWarning("Authorization header not found.");
                return AuthenticateResult.Fail("Authorization header not found.");
            }

            User? user = null;
            try
            {
                var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);

                if (authHeader.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase) && authHeader.Parameter != null)
                {
                    var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
                    var credentials = Encoding.UTF8.GetString(credentialBytes).Split(new[] { ':' }, 2);
                    var email = credentials[0];
                    var password = credentials[1];

                    // Authenticate the user
                    user = await _userDataAccess.AuthenticateAsync(email, password);

                    if (user == null)
                    {
                        Logger.LogWarning("Invalid email or password for email: {Email}", email);

                        // It's important to return Fail to trigger Challenge.
                        // Returning NoResult() might bypass challenge and lead to an infinite loop if this is the default scheme.
                        return AuthenticateResult.Fail("Invalid email or password.");
                    }

                    // Update last login information
                    if (string.IsNullOrWhiteSpace(_pkSecret))
                    {
                        Logger.LogError("PKSecret not configured, cannot update last login for basic auth.");
                    }
                    else
                    {
                        string encryptedUserId = StringCipher.Encrypt(user.Id.ToString(), _pkSecret, StringCipher.EncryptionStrength.Fast);
                        await _userDataAccess.UpdateLastLoginAsync(encryptedUserId, 
                            loginTime:DateTime.UtcNow, 
                            ipAddress:Context.Connection.RemoteIpAddress?.ToString());
                    }
                   
                    var claims = new[] {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Name, user.PrimaryEmail), // Using email as Name claim
                        new Claim(ClaimTypes.Email, user.PrimaryEmail),
                        // Add other claims as needed, e.g., roles
                        new Claim(ClaimTypes.Role, user.UserRole ?? "User"), // Add user role
                        new Claim("EncryptedUserId", user.EncryptedUserId
                            ?? StringCipher.Encrypt(user.Id.ToString(), _pkSecret, StringCipher.EncryptionStrength.Fast)) // Custom claim for encrypted ID if needed elsewhere
                    };
                    var identity = new ClaimsIdentity(claims, Scheme.Name);
                    var principal = new ClaimsPrincipal(identity);
                    var ticket = new AuthenticationTicket(principal, Scheme.Name);

                    Logger.LogInformation("User {Email} authenticated successfully via Basic Auth.", email);
                    return AuthenticateResult.Success(ticket);
                }
                else
                {
                    Logger.LogWarning("Authorization header not in Basic format or parameter is null.");
                    return AuthenticateResult.Fail("Invalid Authorization header format.");
                }
            }
            catch (FormatException ex)
            {
                Logger.LogError(ex, "Invalid Base64 format in Authorization header.");
                return AuthenticateResult.Fail("Invalid Authorization header format (Base64).");
            }
            catch (IndexOutOfRangeException ex)
            {
                Logger.LogError(ex, "Invalid credential format (missing colon).");
                return AuthenticateResult.Fail("Invalid credential format.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "An error occurred during Basic authentication.");
                return AuthenticateResult.Fail($"Authentication failure: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles the challenge for Basic Authentication.
        /// Sets the WWW-Authenticate header to indicate Basic scheme and realm.
        /// </summary>
        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{"MedRecPro API"}\", charset=\"UTF-8\"";
            return base.HandleChallengeAsync(properties);
        }
    }
}
