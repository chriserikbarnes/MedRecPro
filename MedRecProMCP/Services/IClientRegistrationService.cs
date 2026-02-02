/**************************************************************/
/// <summary>
/// Interface for OAuth Dynamic Client Registration (RFC 7591).
/// </summary>
/// <remarks>
/// This service manages OAuth client registrations. It supports both:
/// - Dynamic Client Registration (DCR) per RFC 7591
/// - Client ID Metadata Documents per draft-ietf-oauth-client-id-metadata-document
/// </remarks>
/**************************************************************/

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Service interface for OAuth client registration operations.
/// </summary>
/**************************************************************/
public interface IClientRegistrationService
{
    /**************************************************************/
    /// <summary>
    /// Registers a new OAuth client dynamically.
    /// </summary>
    /// <param name="request">The client registration request.</param>
    /// <returns>The registration response with client credentials.</returns>
    /**************************************************************/
    Task<ClientRegistrationResponse> RegisterClientAsync(ClientRegistrationRequest request);

    /**************************************************************/
    /// <summary>
    /// Validates a client ID and optional secret.
    /// </summary>
    /// <param name="clientId">The client ID to validate.</param>
    /// <param name="clientSecret">The client secret (optional for public clients).</param>
    /// <returns>The client registration data if valid, null otherwise.</returns>
    /**************************************************************/
    Task<RegisteredClient?> ValidateClientAsync(string clientId, string? clientSecret = null);

    /**************************************************************/
    /// <summary>
    /// Validates a redirect URI for a client.
    /// </summary>
    /// <param name="clientId">The client ID.</param>
    /// <param name="redirectUri">The redirect URI to validate.</param>
    /// <returns>True if the redirect URI is valid for this client.</returns>
    /**************************************************************/
    Task<bool> ValidateRedirectUriAsync(string clientId, string redirectUri);

    /**************************************************************/
    /// <summary>
    /// Fetches and validates a Client ID Metadata Document.
    /// </summary>
    /// <param name="clientIdUrl">The HTTPS URL serving as client_id.</param>
    /// <returns>The client metadata if valid, null otherwise.</returns>
    /// <remarks>
    /// Per draft-ietf-oauth-client-id-metadata-document, client IDs can be
    /// HTTPS URLs pointing to JSON metadata documents.
    /// </remarks>
    /**************************************************************/
    Task<RegisteredClient?> FetchClientMetadataDocumentAsync(string clientIdUrl);

    /**************************************************************/
    /// <summary>
    /// Deletes a registered client.
    /// </summary>
    /// <param name="clientId">The client ID to delete.</param>
    /// <returns>True if deleted successfully.</returns>
    /**************************************************************/
    Task<bool> DeleteClientAsync(string clientId);

    /**************************************************************/
    /// <summary>
    /// Gets a registered client by ID.
    /// </summary>
    /// <param name="clientId">The client ID.</param>
    /// <returns>The client if found, null otherwise.</returns>
    /**************************************************************/
    Task<RegisteredClient?> GetClientAsync(string clientId);

    /**************************************************************/
    /// <summary>
    /// Checks if a client ID uses Client ID Metadata Document format.
    /// </summary>
    /// <param name="clientId">The client ID to check.</param>
    /// <returns>True if the client ID is an HTTPS URL.</returns>
    /**************************************************************/
    bool IsClientIdMetadataDocument(string clientId);
}

/**************************************************************/
/// <summary>
/// OAuth Dynamic Client Registration request (RFC 7591).
/// </summary>
/**************************************************************/
public class ClientRegistrationRequest
{
    /**************************************************************/
    /// <summary>
    /// Human-readable name of the client.
    /// </summary>
    /**************************************************************/
    public string ClientName { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Array of registered redirect URIs.
    /// </summary>
    /**************************************************************/
    public List<string> RedirectUris { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Array of OAuth 2.0 grant types the client may use.
    /// </summary>
    /**************************************************************/
    public List<string> GrantTypes { get; set; } = new() { "authorization_code" };

    /**************************************************************/
    /// <summary>
    /// Array of OAuth 2.0 response types the client may use.
    /// </summary>
    /**************************************************************/
    public List<string> ResponseTypes { get; set; } = new() { "code" };

    /**************************************************************/
    /// <summary>
    /// Client token endpoint authentication method.
    /// </summary>
    /**************************************************************/
    public string TokenEndpointAuthMethod { get; set; } = "client_secret_post";

    /**************************************************************/
    /// <summary>
    /// URL of the client's home page.
    /// </summary>
    /**************************************************************/
    public string? ClientUri { get; set; }

    /**************************************************************/
    /// <summary>
    /// URL of the client's logo.
    /// </summary>
    /**************************************************************/
    public string? LogoUri { get; set; }

    /**************************************************************/
    /// <summary>
    /// Space-separated list of scopes the client may request.
    /// </summary>
    /**************************************************************/
    public string? Scope { get; set; }

    /**************************************************************/
    /// <summary>
    /// Contact email addresses for the client.
    /// </summary>
    /**************************************************************/
    public List<string>? Contacts { get; set; }
}

/**************************************************************/
/// <summary>
/// OAuth Dynamic Client Registration response.
/// </summary>
/**************************************************************/
public class ClientRegistrationResponse
{
    /**************************************************************/
    /// <summary>
    /// The unique client identifier.
    /// </summary>
    /**************************************************************/
    public string ClientId { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// The client secret (for confidential clients).
    /// </summary>
    /**************************************************************/
    public string? ClientSecret { get; set; }

    /**************************************************************/
    /// <summary>
    /// Timestamp when the client_id was issued.
    /// </summary>
    /**************************************************************/
    public long ClientIdIssuedAt { get; set; }

    /**************************************************************/
    /// <summary>
    /// Timestamp when the client_secret expires (0 for never).
    /// </summary>
    /**************************************************************/
    public long ClientSecretExpiresAt { get; set; }

    /**************************************************************/
    /// <summary>
    /// The client name.
    /// </summary>
    /**************************************************************/
    public string ClientName { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Array of registered redirect URIs.
    /// </summary>
    /**************************************************************/
    public List<string> RedirectUris { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Array of grant types the client may use.
    /// </summary>
    /**************************************************************/
    public List<string> GrantTypes { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Array of response types the client may use.
    /// </summary>
    /**************************************************************/
    public List<string> ResponseTypes { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Token endpoint authentication method.
    /// </summary>
    /**************************************************************/
    public string TokenEndpointAuthMethod { get; set; } = "client_secret_post";
}

/**************************************************************/
/// <summary>
/// A registered OAuth client.
/// </summary>
/**************************************************************/
public class RegisteredClient
{
    /**************************************************************/
    /// <summary>
    /// The unique client identifier.
    /// </summary>
    /**************************************************************/
    public string ClientId { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// The client secret hash (for confidential clients).
    /// </summary>
    /**************************************************************/
    public string? ClientSecretHash { get; set; }

    /**************************************************************/
    /// <summary>
    /// Human-readable name of the client.
    /// </summary>
    /**************************************************************/
    public string ClientName { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Array of registered redirect URIs.
    /// </summary>
    /**************************************************************/
    public List<string> RedirectUris { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Array of grant types the client may use.
    /// </summary>
    /**************************************************************/
    public List<string> GrantTypes { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Array of response types the client may use.
    /// </summary>
    /**************************************************************/
    public List<string> ResponseTypes { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Token endpoint authentication method.
    /// </summary>
    /**************************************************************/
    public string TokenEndpointAuthMethod { get; set; } = "client_secret_post";

    /**************************************************************/
    /// <summary>
    /// Space-separated list of scopes the client may request.
    /// </summary>
    /**************************************************************/
    public string? Scope { get; set; }

    /**************************************************************/
    /// <summary>
    /// Whether this is a public client (no secret required).
    /// </summary>
    /**************************************************************/
    public bool IsPublicClient { get; set; }

    /**************************************************************/
    /// <summary>
    /// Whether this client was loaded from a Client ID Metadata Document.
    /// </summary>
    /**************************************************************/
    public bool IsClientIdMetadataDocument { get; set; }

    /**************************************************************/
    /// <summary>
    /// When the client was registered.
    /// </summary>
    /**************************************************************/
    public DateTime CreatedAt { get; set; }

    /**************************************************************/
    /// <summary>
    /// When the client registration expires (null for never).
    /// </summary>
    /**************************************************************/
    public DateTime? ExpiresAt { get; set; }
}
