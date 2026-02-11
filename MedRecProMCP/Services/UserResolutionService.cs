/**************************************************************/
/// <summary>
/// Service for resolving upstream IdP identities to MedRecPro database user IDs.
/// </summary>
/// <remarks>
/// During the OAuth callback, the MCP server receives the user's identity from
/// Google/Microsoft (email, sub/GUID). The MedRecPro API's authentication
/// expects a numeric database user ID in the NameIdentifier claim.
///
/// This service bridges that gap by calling the API's resolve-mcp endpoint
/// to look up the user by email and return their encrypted database user ID,
/// which is then decrypted locally to get the numeric ID.
///
/// Uses a dedicated "MedRecProApiDirect" HttpClient without TokenForwardingHandler
/// since the temporary MCP JWT is attached manually.
/// </remarks>
/// <seealso cref="IUserResolutionService"/>
/// <seealso cref="IMcpTokenService"/>
/// <seealso cref="MedRecProMCP.Helpers.StringCipher"/>
/**************************************************************/

using MedRecProMCP.Helpers;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Interface for resolving upstream IdP identities to MedRecPro numeric user IDs.
/// </summary>
/// <seealso cref="UserResolutionService"/>
/**************************************************************/
public interface IUserResolutionService
{
    /**************************************************************/
    /// <summary>
    /// Resolves a user's email address to their numeric MedRecPro database user ID.
    /// </summary>
    /// <param name="email">The user's email address from the upstream IdP.</param>
    /// <param name="upstreamAccessToken">The upstream IdP access token (for JWT generation).</param>
    /// <param name="tempClaims">The temporary claims from the upstream IdP callback.</param>
    /// <returns>The numeric user ID if resolution succeeds; null otherwise.</returns>
    /// <seealso cref="IMcpTokenService.GenerateAccessTokenAsync"/>
    /**************************************************************/
    Task<long?> ResolveUserIdAsync(string email, string upstreamAccessToken, IEnumerable<Claim> tempClaims);
}

/**************************************************************/
/// <summary>
/// Resolves upstream IdP identities to MedRecPro numeric user IDs
/// by calling the API's resolve-mcp endpoint.
/// </summary>
/**************************************************************/
public class UserResolutionService : IUserResolutionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMcpTokenService _tokenService;
    private readonly StringCipher _stringCipher;
    private readonly string _pkSecret;
    private readonly ILogger<UserResolutionService> _logger;

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of UserResolutionService.
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating named HttpClient instances.</param>
    /// <param name="tokenService">Service for generating temporary MCP JWTs.</param>
    /// <param name="stringCipher">Cipher for decrypting the encrypted user ID.</param>
    /// <param name="configuration">Application configuration (for PKSecret).</param>
    /// <param name="logger">Logger instance.</param>
    /// <seealso cref="IHttpClientFactory"/>
    /// <seealso cref="IMcpTokenService"/>
    /// <seealso cref="StringCipher"/>
    /**************************************************************/
    public UserResolutionService(
        IHttpClientFactory httpClientFactory,
        IMcpTokenService tokenService,
        StringCipher stringCipher,
        IConfiguration configuration,
        ILogger<UserResolutionService> logger)
    {
        #region implementation
        _httpClientFactory = httpClientFactory;
        _tokenService = tokenService;
        _stringCipher = stringCipher;
        _logger = logger;

        _pkSecret = configuration["Security:DB:PKSecret"]
            ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing.");
        if (string.IsNullOrWhiteSpace(_pkSecret))
        {
            throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' cannot be empty.");
        }
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<long?> ResolveUserIdAsync(
        string email,
        string upstreamAccessToken,
        IEnumerable<Claim> tempClaims)
    {
        #region implementation
        try
        {
            // Generate a temporary MCP JWT using the current claims
            var tokenResponse = await _tokenService.GenerateAccessTokenAsync(
                tempClaims,
                upstreamAccessToken,
                upstreamRefreshToken: null,
                scopes: new[] { "mcp:tools" },
                clientId: "mcp-internal-resolver");

            // Create an HttpClient without TokenForwardingHandler
            var client = _httpClientFactory.CreateClient("MedRecProApiDirect");

            // Extract display name and provider from upstream claims for auto-provisioning
            var claimsList = tempClaims.ToList();
            var displayName = claimsList.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var provider = claimsList.FirstOrDefault(c => c.Type == "provider")?.Value;

            // Fallback: extract name from email prefix (same as AuthController pattern)
            if (string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(email))
            {
                var idx = email.IndexOf('@');
                displayName = idx > 0 ? email.Substring(0, idx) : email;
            }

            // Build the request with the temporary MCP JWT in the request headers
            // (not DefaultRequestHeaders, which is shared and thread-unsafe)
            var requestBody = new McpResolveRequest
            {
                Email = email,
                DisplayName = displayName,
                Provider = provider
            };
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            // Use the "api/" prefix to match the BaseUrl resolution pattern used
            // by all other MCP→API calls (e.g., UserTools uses "api/users/me")
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "api/users/resolve-mcp")
            {
                Content = jsonContent
            };
            requestMessage.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);

            var response = await client.SendAsync(requestMessage);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[UserResolution] API returned {StatusCode} for email {Email}",
                    response.StatusCode, email);
                return null;
            }

            // Parse the response
            var responseContent = await response.Content.ReadAsStringAsync();
            var resolveResponse = JsonSerializer.Deserialize<McpResolveResponse>(responseContent);

            if (resolveResponse == null || string.IsNullOrEmpty(resolveResponse.EncryptedUserId))
            {
                _logger.LogWarning(
                    "[UserResolution] Empty response from resolve-mcp for email {Email}",
                    email);
                return null;
            }

            // Log whether the user was auto-provisioned
            if (resolveResponse.WasProvisioned)
            {
                _logger.LogInformation(
                    "[UserResolution] Auto-provisioned new user for email {Email}",
                    email);
            }

            // Decrypt the encrypted user ID to get the numeric ID
            var decryptedId = _stringCipher.Decrypt(resolveResponse.EncryptedUserId, _pkSecret);

            if (long.TryParse(decryptedId, out long userId) && userId > 0)
            {
                _logger.LogInformation(
                    "[UserResolution] Resolved email {Email} to user ID {UserId}",
                    email, userId);
                return userId;
            }

            _logger.LogWarning(
                "[UserResolution] Failed to parse decrypted user ID for email {Email}",
                email);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[UserResolution] Error resolving user ID for email {Email}",
                email);
            return null;
        }
        #endregion
    }

    #region Private DTOs

    /**************************************************************/
    /// <summary>
    /// Request DTO for the resolve-mcp API endpoint.
    /// </summary>
    /**************************************************************/
    private class McpResolveRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DisplayName { get; set; }

        [JsonPropertyName("provider")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Provider { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Response DTO from the resolve-mcp API endpoint.
    /// </summary>
    /**************************************************************/
    private class McpResolveResponse
    {
        [JsonPropertyName("encryptedUserId")]
        public string EncryptedUserId { get; set; } = string.Empty;

        [JsonPropertyName("wasProvisioned")]
        public bool WasProvisioned { get; set; }
    }

    #endregion
}
