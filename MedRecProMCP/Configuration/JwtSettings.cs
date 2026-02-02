/**************************************************************/
/// <summary>
/// Configuration settings for JWT token generation and validation.
/// </summary>
/// <remarks>
/// These settings control how the MCP server issues and validates
/// its own JWT access tokens.
/// </remarks>
/**************************************************************/

namespace MedRecProMCP.Configuration;

/**************************************************************/
/// <summary>
/// JWT configuration settings.
/// </summary>
/// <seealso cref="McpServerSettings"/>
/**************************************************************/
public class JwtSettings
{
    /**************************************************************/
    /// <summary>
    /// The symmetric signing key for JWT tokens.
    /// </summary>
    /// <remarks>
    /// This key should be at least 256 bits (32 characters) for HS256.
    /// In production, this should be stored in Azure Key Vault.
    /// </remarks>
    /**************************************************************/
    public string SigningKey { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Access token expiration time in minutes.
    /// </summary>
    /// <remarks>
    /// Recommended: 60 minutes (1 hour) for security best practices.
    /// </remarks>
    /**************************************************************/
    public int AccessTokenExpirationMinutes { get; set; } = 60;

    /**************************************************************/
    /// <summary>
    /// Refresh token expiration time in hours.
    /// </summary>
    /// <remarks>
    /// Recommended: 24 hours for balance between security and usability.
    /// Refresh tokens are rotated on each use per OAuth 2.1 best practices.
    /// </remarks>
    /**************************************************************/
    public int RefreshTokenExpirationHours { get; set; } = 24;

    /**************************************************************/
    /// <summary>
    /// Whether to include upstream IdP tokens in the MCP token claims.
    /// </summary>
    /// <remarks>
    /// When true, the upstream Google/Microsoft token is encrypted and
    /// embedded in the MCP token for forwarding to the MedRecPro API.
    /// </remarks>
    /**************************************************************/
    public bool IncludeUpstreamToken { get; set; } = true;

    /**************************************************************/
    /// <summary>
    /// Encryption key for encrypting upstream tokens embedded in JWT claims.
    /// </summary>
    /// <remarks>
    /// Should be a separate key from the signing key for defense in depth.
    /// </remarks>
    /**************************************************************/
    public string UpstreamTokenEncryptionKey { get; set; } = string.Empty;
}
