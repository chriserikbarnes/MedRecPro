/**************************************************************/
/// <summary>
/// Interface for OAuth flow operations with upstream identity providers.
/// </summary>
/// <remarks>
/// This service handles the OAuth authorization flow with Google and Microsoft,
/// acting as an OAuth client to these providers while the MCP server acts
/// as an OAuth authorization server to MCP clients (like Claude).
/// </remarks>
/// <seealso cref="OAuthService"/>
/**************************************************************/

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Service interface for OAuth provider interactions.
/// </summary>
/**************************************************************/
public interface IOAuthService
{
    /**************************************************************/
    /// <summary>
    /// Generates an authorization URL for the specified provider.
    /// </summary>
    /// <param name="provider">The OAuth provider (google or microsoft).</param>
    /// <param name="state">CSRF protection state parameter.</param>
    /// <param name="codeChallenge">PKCE code challenge.</param>
    /// <param name="redirectUri">The callback URL after authorization.</param>
    /// <param name="scopes">Requested scopes (optional, uses defaults if null).</param>
    /// <returns>The authorization URL to redirect the user to.</returns>
    /**************************************************************/
    string GetAuthorizationUrl(
        string provider,
        string state,
        string codeChallenge,
        string redirectUri,
        IEnumerable<string>? scopes = null);

    /**************************************************************/
    /// <summary>
    /// Exchanges an authorization code for tokens from the upstream provider.
    /// </summary>
    /// <param name="provider">The OAuth provider (google or microsoft).</param>
    /// <param name="code">The authorization code received from the provider.</param>
    /// <param name="codeVerifier">The PKCE code verifier.</param>
    /// <param name="redirectUri">The redirect URI used in the authorization request.</param>
    /// <returns>Token exchange result with tokens and user info.</returns>
    /**************************************************************/
    Task<UpstreamTokenResult?> ExchangeCodeForTokensAsync(
        string provider,
        string code,
        string codeVerifier,
        string redirectUri);

    /**************************************************************/
    /// <summary>
    /// Refreshes an upstream provider token.
    /// </summary>
    /// <param name="provider">The OAuth provider (google or microsoft).</param>
    /// <param name="refreshToken">The refresh token from the provider.</param>
    /// <returns>New tokens from the provider.</returns>
    /**************************************************************/
    Task<UpstreamTokenResult?> RefreshUpstreamTokenAsync(
        string provider,
        string refreshToken);

    /**************************************************************/
    /// <summary>
    /// Gets user information from the upstream provider.
    /// </summary>
    /// <param name="provider">The OAuth provider (google or microsoft).</param>
    /// <param name="accessToken">The access token for the provider.</param>
    /// <returns>User information claims.</returns>
    /**************************************************************/
    Task<UpstreamUserInfo?> GetUserInfoAsync(string provider, string accessToken);

    /**************************************************************/
    /// <summary>
    /// Validates that a provider name is supported.
    /// </summary>
    /// <param name="provider">The provider name to validate.</param>
    /// <returns>True if the provider is supported.</returns>
    /**************************************************************/
    bool IsProviderSupported(string provider);

    /**************************************************************/
    /// <summary>
    /// Gets the list of supported provider names.
    /// </summary>
    /// <returns>List of supported provider names.</returns>
    /**************************************************************/
    IEnumerable<string> GetSupportedProviders();
}

/**************************************************************/
/// <summary>
/// Result of an upstream token exchange operation.
/// </summary>
/**************************************************************/
public class UpstreamTokenResult
{
    /**************************************************************/
    /// <summary>
    /// Access token from the upstream provider.
    /// </summary>
    /**************************************************************/
    public string AccessToken { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Refresh token from the upstream provider (if provided).
    /// </summary>
    /**************************************************************/
    public string? RefreshToken { get; set; }

    /**************************************************************/
    /// <summary>
    /// Token expiration time in seconds.
    /// </summary>
    /**************************************************************/
    public int ExpiresIn { get; set; }

    /**************************************************************/
    /// <summary>
    /// ID token (for OpenID Connect providers).
    /// </summary>
    /**************************************************************/
    public string? IdToken { get; set; }

    /**************************************************************/
    /// <summary>
    /// Token type (usually "Bearer").
    /// </summary>
    /**************************************************************/
    public string TokenType { get; set; } = "Bearer";

    /**************************************************************/
    /// <summary>
    /// Granted scopes (space-separated).
    /// </summary>
    /**************************************************************/
    public string? Scope { get; set; }

    /**************************************************************/
    /// <summary>
    /// User information retrieved during token exchange.
    /// </summary>
    /**************************************************************/
    public UpstreamUserInfo? UserInfo { get; set; }
}

/**************************************************************/
/// <summary>
/// User information from an upstream OAuth provider.
/// </summary>
/**************************************************************/
public class UpstreamUserInfo
{
    /**************************************************************/
    /// <summary>
    /// Provider-specific user identifier.
    /// </summary>
    /**************************************************************/
    public string Id { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// User's email address.
    /// </summary>
    /**************************************************************/
    public string? Email { get; set; }

    /**************************************************************/
    /// <summary>
    /// Whether the email has been verified.
    /// </summary>
    /**************************************************************/
    public bool EmailVerified { get; set; }

    /**************************************************************/
    /// <summary>
    /// User's display name.
    /// </summary>
    /**************************************************************/
    public string? Name { get; set; }

    /**************************************************************/
    /// <summary>
    /// User's given (first) name.
    /// </summary>
    /**************************************************************/
    public string? GivenName { get; set; }

    /**************************************************************/
    /// <summary>
    /// User's family (last) name.
    /// </summary>
    /**************************************************************/
    public string? FamilyName { get; set; }

    /**************************************************************/
    /// <summary>
    /// URL to user's profile picture.
    /// </summary>
    /**************************************************************/
    public string? Picture { get; set; }

    /**************************************************************/
    /// <summary>
    /// User's locale/language preference.
    /// </summary>
    /**************************************************************/
    public string? Locale { get; set; }

    /**************************************************************/
    /// <summary>
    /// The OAuth provider name (google, microsoft).
    /// </summary>
    /**************************************************************/
    public string Provider { get; set; } = string.Empty;
}
