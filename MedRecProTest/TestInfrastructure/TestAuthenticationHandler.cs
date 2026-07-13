using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace MedRecProTest.TestInfrastructure;

/**************************************************************/
/// <summary>
/// Authenticates integration-test requests from explicit test-only request headers.
/// </summary>
/// <remarks>
/// Requests stay anonymous unless they opt in with <c>X-Test-User</c>. This keeps 401 and 403
/// contract tests honest while allowing a test to declare a user and comma-separated role claims.
/// </remarks>
/// <seealso cref="MedRecProWebApplicationFactory"/>
public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Gets the authentication-scheme name used by the test host.
    /// </summary>
    /// <remarks>
    /// The scheme is isolated to the test service provider and never changes production authentication registration.
    /// </remarks>
    public const string SchemeName = "TestAuth";

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="TestAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">Authentication options for the test scheme.</param>
    /// <param name="logger">Factory for the handler's framework logger.</param>
    /// <param name="encoder">URL encoder supplied by the authentication framework.</param>
    /// <seealso cref="AuthenticationHandler{TOptions}"/>
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        #region implementation
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Builds an authenticated principal only when a test supplied an explicit user header.
    /// </summary>
    /// <returns>An authentication result representing an anonymous or test-authenticated request.</returns>
    /// <seealso cref="SchemeName"/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        #region implementation

        if (!Request.Headers.TryGetValue("X-Test-User", out var user) ||
            string.IsNullOrWhiteSpace(user.ToString()))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userName = user.ToString();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.NameIdentifier, userName)
        };

        if (Request.Headers.TryGetValue("X-Test-Roles", out var roles))
        {
            claims.AddRange(roles.ToString()
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(role => new Claim(ClaimTypes.Role, role)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));

        #endregion
    }

    #endregion
}
