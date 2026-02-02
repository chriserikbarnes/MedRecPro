/**************************************************************/
/// <summary>
/// Implementation of OAuth Dynamic Client Registration service.
/// </summary>
/// <remarks>
/// Supports both RFC 7591 Dynamic Client Registration and
/// Client ID Metadata Documents for flexible client management.
///
/// Pre-registered clients (like Claude) are configured in settings.
/// Dynamic registrations are stored in memory cache.
/// </remarks>
/// <seealso cref="IClientRegistrationService"/>
/**************************************************************/

using MedRecProMCP.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Service for OAuth client registration and validation.
/// </summary>
/**************************************************************/
public class ClientRegistrationService : IClientRegistrationService
{
    private readonly McpServerSettings _mcpSettings;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ClientRegistrationService> _logger;

    // Cache key prefix for registered clients
    private const string ClientCachePrefix = "oauth_client_";

    // Pre-registered Claude client configuration
    private static readonly RegisteredClient ClaudeClient = new()
    {
        ClientId = "claude",
        ClientName = "Claude",
        RedirectUris = new List<string>
        {
            "https://claude.ai/api/mcp/auth_callback",
            "https://claude.com/api/mcp/auth_callback"
        },
        GrantTypes = new List<string> { "authorization_code" },
        ResponseTypes = new List<string> { "code" },
        TokenEndpointAuthMethod = "none",
        IsPublicClient = true,
        CreatedAt = DateTime.UtcNow
    };

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of ClientRegistrationService.
    /// </summary>
    /**************************************************************/
    public ClientRegistrationService(
        IOptions<McpServerSettings> mcpSettings,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ILogger<ClientRegistrationService> logger)
    {
        _mcpSettings = mcpSettings.Value;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public Task<ClientRegistrationResponse> RegisterClientAsync(ClientRegistrationRequest request)
    {
        #region implementation
        if (!_mcpSettings.EnableDynamicClientRegistration)
        {
            throw new InvalidOperationException("Dynamic client registration is disabled.");
        }

        // Validate request
        if (string.IsNullOrEmpty(request.ClientName))
        {
            throw new ArgumentException("client_name is required");
        }

        if (request.RedirectUris == null || request.RedirectUris.Count == 0)
        {
            throw new ArgumentException("redirect_uris is required");
        }

        // Validate redirect URIs
        foreach (var uri in request.RedirectUris)
        {
            if (!isValidRedirectUri(uri))
            {
                throw new ArgumentException($"Invalid redirect_uri: {uri}");
            }
        }

        // Generate client credentials
        var clientId = Guid.NewGuid().ToString("N");
        var clientSecret = generateClientSecret();
        var clientSecretHash = hashSecret(clientSecret);
        var now = DateTime.UtcNow;

        // Determine if public or confidential client
        var isPublicClient = request.TokenEndpointAuthMethod == "none";

        // Create registered client
        var client = new RegisteredClient
        {
            ClientId = clientId,
            ClientSecretHash = isPublicClient ? null : clientSecretHash,
            ClientName = request.ClientName,
            RedirectUris = request.RedirectUris,
            GrantTypes = request.GrantTypes.Count > 0
                ? request.GrantTypes
                : new List<string> { "authorization_code" },
            ResponseTypes = request.ResponseTypes.Count > 0
                ? request.ResponseTypes
                : new List<string> { "code" },
            TokenEndpointAuthMethod = request.TokenEndpointAuthMethod,
            Scope = request.Scope,
            IsPublicClient = isPublicClient,
            CreatedAt = now,
            ExpiresAt = now.AddHours(_mcpSettings.ClientRegistrationExpirationHours)
        };

        // Store in cache
        var cacheKey = ClientCachePrefix + clientId;
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromHours(_mcpSettings.ClientRegistrationExpirationHours));

        _cache.Set(cacheKey, client, cacheOptions);

        _logger.LogInformation(
            "[DCR] Registered new client: {ClientName} ({ClientId})",
            request.ClientName, clientId);

        // Create response
        var response = new ClientRegistrationResponse
        {
            ClientId = clientId,
            ClientSecret = isPublicClient ? null : clientSecret,
            ClientIdIssuedAt = new DateTimeOffset(now).ToUnixTimeSeconds(),
            ClientSecretExpiresAt = 0, // Never expires (use client expiration instead)
            ClientName = client.ClientName,
            RedirectUris = client.RedirectUris,
            GrantTypes = client.GrantTypes,
            ResponseTypes = client.ResponseTypes,
            TokenEndpointAuthMethod = client.TokenEndpointAuthMethod
        };

        return Task.FromResult(response);
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<RegisteredClient?> ValidateClientAsync(string clientId, string? clientSecret = null)
    {
        #region implementation
        // Check for pre-registered Claude client
        if (clientId.Equals("claude", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("[DCR] Validated pre-registered Claude client");
            return ClaudeClient;
        }

        // Check for Client ID Metadata Document
        if (IsClientIdMetadataDocument(clientId))
        {
            return await FetchClientMetadataDocumentAsync(clientId);
        }

        // Check cache for dynamically registered client
        var cacheKey = ClientCachePrefix + clientId;
        if (_cache.TryGetValue<RegisteredClient>(cacheKey, out var client) && client != null)
        {
            // Check expiration
            if (client.ExpiresAt.HasValue && client.ExpiresAt < DateTime.UtcNow)
            {
                _logger.LogWarning("[DCR] Client {ClientId} has expired", clientId);
                _cache.Remove(cacheKey);
                return null;
            }

            // Validate secret for confidential clients
            if (!client.IsPublicClient && !string.IsNullOrEmpty(client.ClientSecretHash))
            {
                if (string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogWarning("[DCR] Client secret required for {ClientId}", clientId);
                    return null;
                }

                if (!validateSecret(clientSecret, client.ClientSecretHash))
                {
                    _logger.LogWarning("[DCR] Invalid client secret for {ClientId}", clientId);
                    return null;
                }
            }

            _logger.LogDebug("[DCR] Validated client {ClientId}", clientId);
            return client;
        }

        _logger.LogWarning("[DCR] Unknown client ID: {ClientId}", clientId);
        return null;
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<bool> ValidateRedirectUriAsync(string clientId, string redirectUri)
    {
        #region implementation
        var client = await ValidateClientAsync(clientId);
        if (client == null)
        {
            return false;
        }

        // Check if redirect URI is in the allowed list
        var isValid = client.RedirectUris.Any(uri =>
            uri.Equals(redirectUri, StringComparison.OrdinalIgnoreCase));

        if (!isValid)
        {
            _logger.LogWarning(
                "[DCR] Invalid redirect URI for client {ClientId}: {RedirectUri}",
                clientId, redirectUri);
        }

        return isValid;
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<RegisteredClient?> FetchClientMetadataDocumentAsync(string clientIdUrl)
    {
        #region implementation
        if (!_mcpSettings.ClientIdMetadataDocumentSupported)
        {
            _logger.LogWarning("[DCR] Client ID Metadata Documents are disabled");
            return null;
        }

        if (!IsClientIdMetadataDocument(clientIdUrl))
        {
            return null;
        }

        try
        {
            // Check cache first
            var cacheKey = ClientCachePrefix + "cimd_" + hashUrl(clientIdUrl);
            if (_cache.TryGetValue<RegisteredClient>(cacheKey, out var cachedClient))
            {
                _logger.LogDebug("[DCR] Retrieved CIMD from cache: {Url}", clientIdUrl);
                return cachedClient;
            }

            // Fetch the metadata document
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.GetAsync(clientIdUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[DCR] Failed to fetch CIMD from {Url}: {Status}",
                    clientIdUrl, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var metadata = JsonSerializer.Deserialize<ClientMetadataDocument>(content);

            if (metadata == null)
            {
                _logger.LogWarning("[DCR] Failed to parse CIMD from {Url}", clientIdUrl);
                return null;
            }

            // Validate that client_id matches the URL
            if (!metadata.ClientId.Equals(clientIdUrl, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "[DCR] CIMD client_id mismatch. URL: {Url}, Document: {DocId}",
                    clientIdUrl, metadata.ClientId);
                return null;
            }

            // Create registered client from metadata
            var registeredClient = new RegisteredClient
            {
                ClientId = metadata.ClientId,
                ClientName = metadata.ClientName,
                RedirectUris = metadata.RedirectUris,
                GrantTypes = metadata.GrantTypes ?? new List<string> { "authorization_code" },
                ResponseTypes = metadata.ResponseTypes ?? new List<string> { "code" },
                TokenEndpointAuthMethod = metadata.TokenEndpointAuthMethod ?? "none",
                IsPublicClient = (metadata.TokenEndpointAuthMethod ?? "none") == "none",
                IsClientIdMetadataDocument = true,
                CreatedAt = DateTime.UtcNow
            };

            // Cache with HTTP cache headers or default TTL
            var cacheDuration = TimeSpan.FromHours(1);
            _cache.Set(cacheKey, registeredClient, cacheDuration);

            _logger.LogInformation(
                "[DCR] Fetched and cached CIMD: {ClientName} ({Url})",
                metadata.ClientName, clientIdUrl);

            return registeredClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DCR] Error fetching CIMD from {Url}", clientIdUrl);
            return null;
        }
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public Task<bool> DeleteClientAsync(string clientId)
    {
        #region implementation
        // Can't delete pre-registered clients
        if (clientId.Equals("claude", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        var cacheKey = ClientCachePrefix + clientId;
        _cache.Remove(cacheKey);

        _logger.LogInformation("[DCR] Deleted client: {ClientId}", clientId);
        return Task.FromResult(true);
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<RegisteredClient?> GetClientAsync(string clientId)
    {
        return await ValidateClientAsync(clientId);
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public bool IsClientIdMetadataDocument(string clientId)
    {
        return Uri.TryCreate(clientId, UriKind.Absolute, out var uri) &&
               uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrEmpty(uri.AbsolutePath) &&
               uri.AbsolutePath != "/";
    }

    #region Private Methods

    /**************************************************************/
    /// <summary>
    /// Validates a redirect URI per OAuth 2.1 requirements.
    /// </summary>
    /**************************************************************/
    private static bool isValidRedirectUri(string redirectUri)
    {
        #region implementation
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
        {
            return false;
        }

        // HTTPS required except for localhost
        if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            var host = uri.Host.ToLowerInvariant();
            if (host != "localhost" && host != "127.0.0.1")
            {
                return false;
            }
        }
        else if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            // Allow custom schemes for native apps
            return uri.Scheme.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '+');
        }

        // No fragments allowed
        return string.IsNullOrEmpty(uri.Fragment);
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Generates a secure client secret.
    /// </summary>
    /**************************************************************/
    private static string generateClientSecret()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /**************************************************************/
    /// <summary>
    /// Hashes a client secret for storage.
    /// </summary>
    /**************************************************************/
    private static string hashSecret(string secret)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hashBytes);
    }

    /**************************************************************/
    /// <summary>
    /// Validates a client secret against its hash.
    /// </summary>
    /**************************************************************/
    private static bool validateSecret(string secret, string hash)
    {
        var computedHash = hashSecret(secret);
        return computedHash.Equals(hash, StringComparison.OrdinalIgnoreCase);
    }

    /**************************************************************/
    /// <summary>
    /// Hashes a URL for cache key generation.
    /// </summary>
    /**************************************************************/
    private static string hashUrl(string url)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(hashBytes)[..16];
    }

    #endregion
}

/**************************************************************/
/// <summary>
/// Client ID Metadata Document structure.
/// </summary>
/**************************************************************/
internal class ClientMetadataDocument
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("client_name")]
    public string ClientName { get; set; } = string.Empty;

    [JsonPropertyName("redirect_uris")]
    public List<string> RedirectUris { get; set; } = new();

    [JsonPropertyName("grant_types")]
    public List<string>? GrantTypes { get; set; }

    [JsonPropertyName("response_types")]
    public List<string>? ResponseTypes { get; set; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; set; }

    [JsonPropertyName("client_uri")]
    public string? ClientUri { get; set; }

    [JsonPropertyName("logo_uri")]
    public string? LogoUri { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("contacts")]
    public List<string>? Contacts { get; set; }
}
