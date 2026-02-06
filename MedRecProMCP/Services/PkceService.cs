/**************************************************************/
/// <summary>
/// Implementation of PKCE service for authorization code protection.
/// </summary>
/// <remarks>
/// PKCE (RFC 7636) prevents authorization code interception attacks by
/// requiring clients to prove they initiated the authorization request.
/// This implementation uses S256 (SHA-256) code challenge method.
/// </remarks>
/// <seealso cref="IPkceService"/>
/**************************************************************/

using System.Security.Cryptography;
using System.Text;

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Service for PKCE code verifier/challenge operations.
/// </summary>
/**************************************************************/
public class PkceService : IPkceService
{
    private readonly IPersistedCacheService _cache;
    private readonly ILogger<PkceService> _logger;

    // PKCE data cache prefix
    private const string PkceCachePrefix = "pkce_state_";

    // PKCE data expiration (10 minutes is typical for authorization flows)
    private static readonly TimeSpan PkceExpiration = TimeSpan.FromMinutes(10);

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of PkceService.
    /// </summary>
    /**************************************************************/
    public PkceService(IPersistedCacheService cache, ILogger<PkceService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public (string CodeVerifier, string CodeChallenge) GenerateCodeChallengePair()
    {
        #region implementation
        // Generate a cryptographically random code verifier
        // RFC 7636 specifies 43-128 characters using unreserved URI characters
        var verifierBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(verifierBytes);

        var codeVerifier = base64UrlEncode(verifierBytes);

        // Generate code challenge using S256 method
        var codeChallenge = computeCodeChallenge(codeVerifier);

        _logger.LogDebug("[PKCE] Generated code challenge pair");

        return (codeVerifier, codeChallenge);
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task StorePkceDataAsync(
        string state,
        string codeVerifier,
        string codeChallenge,
        string clientId,
        string redirectUri,
        IEnumerable<string> scopes)
    {
        #region implementation
        var now = DateTime.UtcNow;
        // Store the upstream provider's verifier and the client's original code_challenge separately
        var pkceData = new PkceData
        {
            CodeVerifier = codeVerifier,       // Upstream verifier (for Google/Microsoft exchange)
            CodeChallenge = codeChallenge,      // Client's original S256 challenge (from Claude)
            ClientId = clientId,
            RedirectUri = redirectUri,
            Scopes = scopes.ToList(),
            CreatedAt = now,
            ExpiresAt = now.Add(PkceExpiration)
        };

        var cacheKey = PkceCachePrefix + state;
        await _cache.SetAsync(cacheKey, pkceData, PkceExpiration);

        _logger.LogDebug(
            "[PKCE] Stored PKCE data for state {State}, client {ClientId}",
            state, clientId);
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<PkceData?> GetPkceDataAsync(string state)
    {
        #region implementation
        var cacheKey = PkceCachePrefix + state;

        var (found, pkceData) = await _cache.TryGetAsync<PkceData>(cacheKey);
        if (found && pkceData != null)
        {
            // Check if expired (redundant with cache expiration, but belt-and-suspenders)
            if (pkceData.ExpiresAt > DateTime.UtcNow)
            {
                _logger.LogDebug("[PKCE] Retrieved PKCE data for state {State}", state);
                return pkceData;
            }

            // Expired, remove it
            await _cache.RemoveAsync(cacheKey);
            _logger.LogWarning("[PKCE] PKCE data expired for state {State}", state);
        }

        _logger.LogWarning("[PKCE] No PKCE data found for state {State}", state);
        return null;
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public bool ValidateCodeVerifier(string codeVerifier, string codeChallenge)
    {
        #region implementation
        if (string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(codeChallenge))
        {
            _logger.LogWarning("[PKCE] Code verifier or challenge is empty");
            return false;
        }

        var computedChallenge = computeCodeChallenge(codeVerifier);
        var isValid = computedChallenge == codeChallenge;

        if (!isValid)
        {
            _logger.LogWarning("[PKCE] Code verifier validation failed");
        }
        else
        {
            _logger.LogDebug("[PKCE] Code verifier validated successfully");
        }

        return isValid;
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task RemovePkceDataAsync(string state)
    {
        #region implementation
        var cacheKey = PkceCachePrefix + state;
        await _cache.RemoveAsync(cacheKey);

        _logger.LogDebug("[PKCE] Removed PKCE data for state {State}", state);
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public string GenerateState()
    {
        #region implementation
        var stateBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(stateBytes);

        return base64UrlEncode(stateBytes);
        #endregion
    }

    #region Private Methods

    /**************************************************************/
    /// <summary>
    /// Computes the S256 code challenge from a code verifier.
    /// </summary>
    /**************************************************************/
    private static string computeCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return base64UrlEncode(hashBytes);
    }

    /**************************************************************/
    /// <summary>
    /// Base64URL encodes a byte array (RFC 4648).
    /// </summary>
    /**************************************************************/
    private static string base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    #endregion
}
