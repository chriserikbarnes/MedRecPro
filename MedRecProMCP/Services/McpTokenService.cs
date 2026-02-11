/**************************************************************/
/// <summary>
/// Implementation of MCP token service for JWT generation and validation.
/// </summary>
/// <remarks>
/// This service handles the complete lifecycle of MCP tokens:
/// - Generating JWT access tokens with embedded upstream IdP tokens
/// - Managing refresh tokens with rotation
/// - Token validation and claims extraction
/// - Secure storage and encryption of sensitive token data
/// </remarks>
/// <seealso cref="IMcpTokenService"/>
/// <seealso cref="JwtSettings"/>
/**************************************************************/

using MedRecProMCP.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Service for MCP token generation, validation, and management.
/// </summary>
/**************************************************************/
public class McpTokenService : IMcpTokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly McpServerSettings _mcpSettings;
    private readonly IPersistedCacheService _cache;
    private readonly ILogger<McpTokenService> _logger;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly byte[] _encryptionKey;

    // Cache keys for refresh tokens
    private const string RefreshTokenCachePrefix = "mcp_refresh_token_";
    private const string UserRefreshTokensPrefix = "mcp_user_tokens_";

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of McpTokenService.
    /// </summary>
    /// <param name="jwtSettings">JWT configuration settings.</param>
    /// <param name="mcpSettings">MCP server settings.</param>
    /// <param name="cache">Memory cache for refresh token storage.</param>
    /// <param name="logger">Logger instance.</param>
    /**************************************************************/
    public McpTokenService(
        IOptions<JwtSettings> jwtSettings,
        IOptions<McpServerSettings> mcpSettings,
        IPersistedCacheService cache,
        ILogger<McpTokenService> logger)
    {
        _jwtSettings = jwtSettings.Value;
        _mcpSettings = mcpSettings.Value;
        _cache = cache;
        _logger = logger;

        #region implementation
        // Initialize signing key using McpServer:JwtSigningKey (expected by the API)
        // Falls back to Jwt:Key if McpServer key is not configured
        var signingKeyValue = !string.IsNullOrEmpty(_mcpSettings.JwtSigningKey)
            ? _mcpSettings.JwtSigningKey
            : _jwtSettings.Key;
        _signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(signingKeyValue));

        // Initialize encryption key (derive from configured key or use signing key)
        var encryptionKeySource = !string.IsNullOrEmpty(_jwtSettings.UpstreamTokenEncryptionKey)
            ? _jwtSettings.UpstreamTokenEncryptionKey
            : signingKeyValue;

        using var sha256 = SHA256.Create();
        _encryptionKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(encryptionKeySource));
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<TokenResponse> GenerateAccessTokenAsync(
        IEnumerable<Claim> userClaims,
        string upstreamAccessToken,
        string? upstreamRefreshToken,
        IEnumerable<string> scopes,
        string clientId)
    {
        #region implementation
        var claimsList = userClaims.ToList();
        var now = DateTime.UtcNow;
        var accessTokenExpiry = now.AddMinutes(_jwtSettings.ExpirationMinutes);
        var refreshTokenExpiry = now.AddHours(_jwtSettings.RefreshTokenExpirationHours);

        // Generate unique token ID
        var tokenId = Guid.NewGuid().ToString("N");

        // Extract user identifier for refresh token tracking
        var userId = claimsList.FirstOrDefault(c =>
            c.Type == ClaimTypes.NameIdentifier ||
            c.Type == "sub")?.Value ?? Guid.NewGuid().ToString();

        // Build claims for the access token
        var accessTokenClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, tokenId),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("client_id", clientId),
            new("scope", string.Join(" ", scopes))
        };

        // Add user claims, normalizing long .NET URI claim types to standard
        // short JWT names (e.g., ClaimTypes.NameIdentifier → "sub") so the
        // token payload is standards-compliant and predictable regardless of
        // JwtSecurityTokenHandler's OutboundClaimTypeMap configuration.
        foreach (var claim in claimsList)
        {
            // Skip claims we're setting explicitly
            if (claim.Type == JwtRegisteredClaimNames.Jti ||
                claim.Type == JwtRegisteredClaimNames.Iat ||
                claim.Type == JwtRegisteredClaimNames.Iss ||
                claim.Type == JwtRegisteredClaimNames.Aud)
            {
                continue;
            }

            var normalizedType = normalizeClaimType(claim.Type);
            accessTokenClaims.Add(new Claim(normalizedType, claim.Value, claim.ValueType));
        }

        // Encrypt and embed upstream token if configured
        if (_jwtSettings.IncludeUpstreamToken && !string.IsNullOrEmpty(upstreamAccessToken))
        {
            var encryptedUpstreamToken = encryptToken(upstreamAccessToken);
            accessTokenClaims.Add(new Claim("upstream_token", encryptedUpstreamToken));
        }

        // Create the access token
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(accessTokenClaims),
            Expires = accessTokenExpiry,
            Issuer = _mcpSettings.ServerUrl.TrimEnd('/'),
            Audience = _mcpSettings.ServerUrl.TrimEnd('/'),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        // Prevent double-mapping: claims are already normalized to standard
        // JWT names, so the handler's outbound map must not re-map them.
        tokenHandler.OutboundClaimTypeMap.Clear();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        // Generate refresh token
        var refreshToken = generateRefreshToken();

        // Store refresh token metadata in cache
        var refreshTokenData = new RefreshTokenData
        {
            TokenId = tokenId,
            UserId = userId,
            ClientId = clientId,
            Scopes = scopes.ToList(),
            UpstreamAccessToken = upstreamAccessToken,
            UpstreamRefreshToken = upstreamRefreshToken,
            CreatedAt = now,
            ExpiresAt = refreshTokenExpiry
        };

        await storeRefreshTokenAsync(refreshToken, refreshTokenData);

        _logger.LogInformation(
            "[Token] Generated access token for user {UserId}, client {ClientId}, expires at {Expiry}",
            userId, clientId, accessTokenExpiry);

        return new TokenResponse
        {
            AccessToken = accessToken,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60,
            RefreshToken = refreshToken,
            Scope = string.Join(" ", scopes)
        };
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<TokenResponse?> RefreshAccessTokenAsync(string refreshToken, string clientId)
    {
        #region implementation
        // Retrieve refresh token data
        var tokenData = await getRefreshTokenDataAsync(refreshToken);
        if (tokenData == null)
        {
            _logger.LogWarning("[Token] Refresh token not found or expired");
            return null;
        }

        // Validate client ID matches
        if (tokenData.ClientId != clientId)
        {
            _logger.LogWarning(
                "[Token] Refresh token client ID mismatch. Expected: {Expected}, Got: {Got}",
                tokenData.ClientId, clientId);
            return null;
        }

        // Check if token is expired
        if (tokenData.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("[Token] Refresh token has expired");
            await RevokeRefreshTokenAsync(refreshToken);
            return null;
        }

        // Invalidate the old refresh token (rotation)
        await RevokeRefreshTokenAsync(refreshToken);

        // Recreate user claims from stored data
        var userClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, tokenData.UserId)
        };

        // Generate new tokens
        return await GenerateAccessTokenAsync(
            userClaims,
            tokenData.UpstreamAccessToken,
            tokenData.UpstreamRefreshToken,
            tokenData.Scopes,
            clientId);
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public string? ExtractUpstreamToken(string mcpAccessToken)
    {
        #region implementation
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(mcpAccessToken);

            var encryptedUpstreamToken = jwtToken.Claims
                .FirstOrDefault(c => c.Type == "upstream_token")?.Value;

            if (string.IsNullOrEmpty(encryptedUpstreamToken))
            {
                return null;
            }

            return decryptToken(encryptedUpstreamToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Token] Failed to extract upstream token");
            return null;
        }
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public ClaimsPrincipal? ValidateToken(string accessToken)
    {
        #region implementation
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _mcpSettings.ServerUrl.TrimEnd('/'),
                ValidAudience = _mcpSettings.ServerUrl.TrimEnd('/'),
                IssuerSigningKey = _signingKey,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            var principal = tokenHandler.ValidateToken(accessToken, validationParameters, out _);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Token] Token validation failed");
            return null;
        }
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
    {
        #region implementation
        var cacheKey = RefreshTokenCachePrefix + hashToken(refreshToken);
        await _cache.RemoveAsync(cacheKey);

        _logger.LogInformation("[Token] Revoked refresh token");
        return true;
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<int> RevokeAllUserTokensAsync(string userId)
    {
        #region implementation
        var userTokensKey = UserRefreshTokensPrefix + userId;

        var (found, tokenHashes) = await _cache.TryGetAsync<List<string>>(userTokensKey);
        if (found && tokenHashes != null)
        {
            foreach (var hash in tokenHashes)
            {
                await _cache.RemoveAsync(RefreshTokenCachePrefix + hash);
            }

            await _cache.RemoveAsync(userTokensKey);

            _logger.LogInformation(
                "[Token] Revoked {Count} tokens for user {UserId}",
                tokenHashes.Count, userId);

            return tokenHashes.Count;
        }

        return 0;
        #endregion
    }

    #region Private Methods

    /**************************************************************/
    /// <summary>
    /// Normalizes .NET claim type URIs to standard short JWT claim names.
    /// </summary>
    /// <remarks>
    /// <c>ClaimTypes.NameIdentifier</c>, <c>ClaimTypes.Name</c>, etc. use long URIs
    /// (e.g., <c>http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier</c>).
    /// JWT standards use short names (<c>sub</c>, <c>name</c>, <c>email</c>). This method
    /// maps the long URIs to their JWT equivalents so the token payload is
    /// standards-compliant and predictable.
    /// </remarks>
    /// <param name="claimType">The claim type string to normalize.</param>
    /// <returns>The standard JWT claim name, or the original value if no mapping exists.</returns>
    /**************************************************************/
    private static string normalizeClaimType(string claimType)
    {
        return claimType switch
        {
            ClaimTypes.NameIdentifier => JwtRegisteredClaimNames.Sub,
            ClaimTypes.Name           => JwtRegisteredClaimNames.Name,
            ClaimTypes.Email          => JwtRegisteredClaimNames.Email,
            ClaimTypes.GivenName      => JwtRegisteredClaimNames.GivenName,
            ClaimTypes.Surname        => JwtRegisteredClaimNames.FamilyName,
            _                         => claimType
        };
    }

    /**************************************************************/
    /// <summary>
    /// Generates a cryptographically secure refresh token.
    /// </summary>
    /**************************************************************/
    private static string generateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /**************************************************************/
    /// <summary>
    /// Encrypts an upstream token for embedding in JWT claims.
    /// </summary>
    /**************************************************************/
    private string encryptToken(string token)
    {
        #region implementation
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(token);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Combine IV and cipher text
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Decrypts an upstream token from JWT claims.
    /// </summary>
    /**************************************************************/
    private string decryptToken(string encryptedToken)
    {
        #region implementation
        var allBytes = Convert.FromBase64String(encryptedToken);

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;

        // Extract IV (first 16 bytes)
        var iv = new byte[16];
        Buffer.BlockCopy(allBytes, 0, iv, 0, 16);
        aes.IV = iv;

        // Extract cipher text
        var cipherBytes = new byte[allBytes.Length - 16];
        Buffer.BlockCopy(allBytes, 16, cipherBytes, 0, cipherBytes.Length);

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Hashes a refresh token for cache key storage.
    /// </summary>
    /**************************************************************/
    private static string hashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes);
    }

    /**************************************************************/
    /// <summary>
    /// Stores refresh token data in the cache.
    /// </summary>
    /**************************************************************/
    private async Task storeRefreshTokenAsync(string refreshToken, RefreshTokenData data)
    {
        #region implementation
        var tokenHash = hashToken(refreshToken);
        var cacheKey = RefreshTokenCachePrefix + tokenHash;

        // Store refresh token with absolute expiration
        var expiration = data.ExpiresAt - DateTime.UtcNow;
        if (expiration <= TimeSpan.Zero)
            expiration = TimeSpan.FromMinutes(1);

        await _cache.SetAsync(cacheKey, data, expiration);

        // Track token for user (for bulk revocation)
        var userTokensKey = UserRefreshTokensPrefix + data.UserId;
        var (found, userTokens) = await _cache.TryGetAsync<List<string>>(userTokensKey);
        if (!found || userTokens == null)
        {
            userTokens = new List<string>();
        }

        userTokens.Add(tokenHash);
        await _cache.SetAsync(userTokensKey, userTokens, TimeSpan.FromDays(7));
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Retrieves refresh token data from the cache.
    /// </summary>
    /**************************************************************/
    private async Task<RefreshTokenData?> getRefreshTokenDataAsync(string refreshToken)
    {
        var tokenHash = hashToken(refreshToken);
        var cacheKey = RefreshTokenCachePrefix + tokenHash;

        var (found, data) = await _cache.TryGetAsync<RefreshTokenData>(cacheKey);
        return found ? data : null;
    }

    #endregion
}

/**************************************************************/
/// <summary>
/// Internal data structure for storing refresh token metadata.
/// </summary>
/**************************************************************/
internal class RefreshTokenData
{
    public string TokenId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new();
    public string UpstreamAccessToken { get; set; } = string.Empty;
    public string? UpstreamRefreshToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
