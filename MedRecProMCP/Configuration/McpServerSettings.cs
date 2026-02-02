/**************************************************************/
/// <summary>
/// Configuration settings for the MCP Server.
/// </summary>
/// <remarks>
/// These settings define the server's identity, supported scopes,
/// and OAuth behavior configuration.
/// </remarks>
/**************************************************************/

namespace MedRecProMCP.Configuration;

/**************************************************************/
/// <summary>
/// MCP Server configuration settings.
/// </summary>
/// <seealso cref="JwtSettings"/>
/// <seealso cref="OAuthProviderSettings"/>
/**************************************************************/
public class McpServerSettings
{
    /**************************************************************/
    /// <summary>
    /// The base URL of the MCP server (e.g., https://mcp.medrecpro.com).
    /// </summary>
    /// <remarks>
    /// This URL is used as the issuer for JWT tokens and the resource
    /// identifier in OAuth flows.
    /// </remarks>
    /**************************************************************/
    public string ServerUrl { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// The name of the MCP server for display purposes.
    /// </summary>
    /**************************************************************/
    public string ServerName { get; set; } = "MedRecPro MCP Server";

    /**************************************************************/
    /// <summary>
    /// List of OAuth scopes supported by this MCP server.
    /// </summary>
    /// <remarks>
    /// These scopes are advertised in the Protected Resource Metadata
    /// and Authorization Server Metadata documents.
    /// </remarks>
    /**************************************************************/
    public string[]? ScopesSupported { get; set; }

    /**************************************************************/
    /// <summary>
    /// Whether Dynamic Client Registration is enabled.
    /// </summary>
    /// <remarks>
    /// When enabled, clients can register themselves via the /oauth/register
    /// endpoint per RFC 7591.
    /// </remarks>
    /**************************************************************/
    public bool EnableDynamicClientRegistration { get; set; } = true;

    /**************************************************************/
    /// <summary>
    /// Whether Client ID Metadata Documents are supported.
    /// </summary>
    /// <remarks>
    /// When enabled, clients can use HTTPS URLs as client identifiers
    /// per draft-ietf-oauth-client-id-metadata-document-00.
    /// </remarks>
    /**************************************************************/
    public bool ClientIdMetadataDocumentSupported { get; set; } = true;

    /**************************************************************/
    /// <summary>
    /// Maximum number of dynamically registered clients to store.
    /// </summary>
    /**************************************************************/
    public int MaxRegisteredClients { get; set; } = 1000;

    /**************************************************************/
    /// <summary>
    /// Default expiration time for dynamically registered clients in hours.
    /// </summary>
    /**************************************************************/
    public int ClientRegistrationExpirationHours { get; set; } = 24;
}
