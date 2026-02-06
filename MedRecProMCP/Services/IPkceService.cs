/**************************************************************/
/// <summary>
/// Interface for PKCE (Proof Key for Code Exchange) operations.
/// </summary>
/// <remarks>
/// PKCE is mandatory for OAuth 2.1 and provides protection against
/// authorization code interception attacks. This service manages
/// PKCE code verifier/challenge pairs and their validation.
/// </remarks>
/**************************************************************/

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Service interface for PKCE operations.
/// </summary>
/**************************************************************/
public interface IPkceService
{
    /**************************************************************/
    /// <summary>
    /// Generates a PKCE code verifier and challenge pair.
    /// </summary>
    /// <returns>A tuple containing (codeVerifier, codeChallenge).</returns>
    /// <remarks>
    /// The code verifier is a cryptographically random string.
    /// The code challenge is the SHA-256 hash of the verifier, base64url-encoded.
    /// </remarks>
    /**************************************************************/
    (string CodeVerifier, string CodeChallenge) GenerateCodeChallengePair();

    /**************************************************************/
    /// <summary>
    /// Stores PKCE data for later validation.
    /// </summary>
    /// <param name="state">The state parameter to associate with this verifier.</param>
    /// <param name="codeVerifier">The upstream provider's code verifier (for Google/Microsoft token exchange).</param>
    /// <param name="codeChallenge">The client's original code_challenge (from the MCP client, e.g. Claude).</param>
    /// <param name="clientId">The client ID that initiated this flow.</param>
    /// <param name="redirectUri">The redirect URI for this flow.</param>
    /// <param name="scopes">The requested scopes.</param>
    /// <returns>Task for async operation.</returns>
    /// <remarks>
    /// The codeVerifier is used to exchange the authorization code with the upstream
    /// provider (Google/Microsoft). The codeChallenge is the client's original S256
    /// challenge that will be validated when the client exchanges its authorization
    /// code with this server using its own code_verifier.
    /// </remarks>
    /**************************************************************/
    Task StorePkceDataAsync(
        string state,
        string codeVerifier,
        string codeChallenge,
        string clientId,
        string redirectUri,
        IEnumerable<string> scopes);

    /**************************************************************/
    /// <summary>
    /// Retrieves and validates stored PKCE data.
    /// </summary>
    /// <param name="state">The state parameter to look up.</param>
    /// <returns>The PKCE data if found and valid, null otherwise.</returns>
    /**************************************************************/
    Task<PkceData?> GetPkceDataAsync(string state);

    /**************************************************************/
    /// <summary>
    /// Validates a code verifier against a stored code challenge.
    /// </summary>
    /// <param name="codeVerifier">The code verifier provided by the client.</param>
    /// <param name="codeChallenge">The stored code challenge.</param>
    /// <returns>True if the verifier is valid for the challenge.</returns>
    /**************************************************************/
    bool ValidateCodeVerifier(string codeVerifier, string codeChallenge);

    /**************************************************************/
    /// <summary>
    /// Removes stored PKCE data after use.
    /// </summary>
    /// <param name="state">The state parameter to remove.</param>
    /**************************************************************/
    Task RemovePkceDataAsync(string state);

    /**************************************************************/
    /// <summary>
    /// Generates a cryptographically secure state parameter.
    /// </summary>
    /// <returns>A random state string.</returns>
    /**************************************************************/
    string GenerateState();
}

/**************************************************************/
/// <summary>
/// Data stored for a PKCE authorization flow.
/// </summary>
/**************************************************************/
public class PkceData
{
    /**************************************************************/
    /// <summary>
    /// The PKCE code verifier.
    /// </summary>
    /**************************************************************/
    public string CodeVerifier { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// The PKCE code challenge (derived from verifier).
    /// </summary>
    /**************************************************************/
    public string CodeChallenge { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// The OAuth client ID that initiated this flow.
    /// </summary>
    /**************************************************************/
    public string ClientId { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// The redirect URI for the callback.
    /// </summary>
    /**************************************************************/
    public string RedirectUri { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// The requested scopes.
    /// </summary>
    /**************************************************************/
    public List<string> Scopes { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// The upstream OAuth provider (google or microsoft).
    /// </summary>
    /**************************************************************/
    public string? Provider { get; set; }

    /**************************************************************/
    /// <summary>
    /// When this PKCE data was created.
    /// </summary>
    /**************************************************************/
    public DateTime CreatedAt { get; set; }

    /**************************************************************/
    /// <summary>
    /// When this PKCE data expires.
    /// </summary>
    /**************************************************************/
    public DateTime ExpiresAt { get; set; }
}
