/**************************************************************/
/// <summary>
/// Interface for MCP token service operations.
/// </summary>
/// <remarks>
/// This service handles:
/// - Issuing MCP access tokens that wrap upstream IdP tokens
/// - Validating MCP tokens on incoming requests
/// - Token refresh with rotation per OAuth 2.1 best practices
/// - Extracting upstream tokens for forwarding to MedRecPro API
/// </remarks>
/// <seealso cref="McpTokenService"/>
/**************************************************************/

using System.Security.Claims;
using System.Text.Json.Serialization;

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Service interface for MCP token operations.
/// </summary>
/**************************************************************/
public interface IMcpTokenService
{
    /**************************************************************/
    /// <summary>
    /// Generates an MCP access token for an authenticated user.
    /// </summary>
    /// <param name="userClaims">Claims from the upstream IdP (Google/Microsoft).</param>
    /// <param name="upstreamAccessToken">The access token from the upstream IdP.</param>
    /// <param name="upstreamRefreshToken">The refresh token from the upstream IdP (optional).</param>
    /// <param name="scopes">The scopes granted to this token.</param>
    /// <param name="clientId">The OAuth client ID that requested this token.</param>
    /// <returns>Token response containing access and refresh tokens.</returns>
    /// <seealso cref="TokenResponse"/>
    /**************************************************************/
    Task<TokenResponse> GenerateAccessTokenAsync(
        IEnumerable<Claim> userClaims,
        string upstreamAccessToken,
        string? upstreamRefreshToken,
        IEnumerable<string> scopes,
        string clientId);

    /**************************************************************/
    /// <summary>
    /// Refreshes an MCP access token using a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to use.</param>
    /// <param name="clientId">The OAuth client ID requesting the refresh.</param>
    /// <returns>New token response with rotated refresh token.</returns>
    /// <remarks>
    /// Implements refresh token rotation per OAuth 2.1 best practices.
    /// The old refresh token is invalidated after use.
    /// </remarks>
    /**************************************************************/
    Task<TokenResponse?> RefreshAccessTokenAsync(string refreshToken, string clientId);

    /**************************************************************/
    /// <summary>
    /// Extracts the upstream IdP token from an MCP access token.
    /// </summary>
    /// <param name="mcpAccessToken">The MCP access token.</param>
    /// <returns>The upstream IdP access token for forwarding to MedRecPro API.</returns>
    /// <remarks>
    /// The upstream token is encrypted within the MCP token claims
    /// and must be decrypted before use.
    /// </remarks>
    /**************************************************************/
    string? ExtractUpstreamToken(string mcpAccessToken);

    /**************************************************************/
    /// <summary>
    /// Validates an MCP access token and returns the claims principal.
    /// </summary>
    /// <param name="accessToken">The MCP access token to validate.</param>
    /// <returns>The claims principal if valid, null otherwise.</returns>
    /**************************************************************/
    ClaimsPrincipal? ValidateToken(string accessToken);

    /**************************************************************/
    /// <summary>
    /// Revokes a refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token to revoke.</param>
    /// <returns>True if revoked successfully, false otherwise.</returns>
    /**************************************************************/
    Task<bool> RevokeRefreshTokenAsync(string refreshToken);

    /**************************************************************/
    /// <summary>
    /// Revokes all tokens for a specific user.
    /// </summary>
    /// <param name="userId">The user's identifier.</param>
    /// <returns>Number of tokens revoked.</returns>
    /**************************************************************/
    Task<int> RevokeAllUserTokensAsync(string userId);
}

/**************************************************************/
/// <summary>
/// Response containing OAuth tokens.
/// </summary>
/**************************************************************/
public class TokenResponse
{
    /**************************************************************/
    /// <summary>
    /// The access token for accessing protected resources.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// The token type (always "Bearer").
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    /**************************************************************/
    /// <summary>
    /// Expiration time in seconds from issuance.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /**************************************************************/
    /// <summary>
    /// The refresh token for obtaining new access tokens.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    /**************************************************************/
    /// <summary>
    /// Space-separated list of granted scopes.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
