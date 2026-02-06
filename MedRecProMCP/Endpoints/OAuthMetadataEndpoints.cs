/**************************************************************/
/// <summary>
/// OAuth 2.0 Authorization Server Metadata endpoints (RFC 8414).
/// </summary>
/// <remarks>
/// This class implements the well-known metadata endpoints required for
/// OAuth 2.1 discovery:
/// - /.well-known/oauth-authorization-server - AS metadata
/// - /.well-known/openid-configuration - OIDC discovery (optional)
///
/// These endpoints allow MCP clients to discover authorization and token
/// endpoints without hardcoding URLs.
/// </remarks>
/// <seealso href="https://www.rfc-editor.org/rfc/rfc8414"/>
/**************************************************************/

using MedRecProMCP.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace MedRecProMCP.Endpoints;

/**************************************************************/
/// <summary>
/// Extension methods for mapping OAuth metadata endpoints.
/// </summary>
/**************************************************************/
public static class OAuthMetadataEndpoints
{
    /**************************************************************/
    /// <summary>
    /// Maps OAuth metadata discovery endpoints.
    /// </summary>
    /**************************************************************/
    public static WebApplication MapOAuthMetadataEndpoints(this WebApplication app)
    {
        #region implementation
        /**************************************************************/
        /// DEBUG: Routes include /mcp prefix since app runs standalone.
        ///        Full path: /mcp/.well-known/oauth-authorization-server
        /// RELEASE: IIS virtual app at /mcp strips the prefix.
        ///        Internal path: /.well-known/* → External: /mcp/.well-known/*
        /**************************************************************/
#if DEBUG
        var wellKnownPrefix = "/mcp/.well-known";
#else
        var wellKnownPrefix = "/.well-known";
#endif

        // OAuth 2.0 Authorization Server Metadata (RFC 8414)
        app.MapGet($"{wellKnownPrefix}/oauth-authorization-server", HandleAuthorizationServerMetadata)
            .AllowAnonymous()
            .WithName("OAuthASMetadata")
            .WithTags("OAuth Metadata")
            .WithSummary("Returns OAuth 2.0 Authorization Server metadata (RFC 8414)");

        // OpenID Connect Discovery (for compatibility)
        app.MapGet($"{wellKnownPrefix}/openid-configuration", HandleAuthorizationServerMetadata)
            .AllowAnonymous()
            .WithName("OpenIDConfiguration")
            .WithTags("OAuth Metadata")
            .WithSummary("Returns OpenID Connect discovery document");

        // Protected Resource Metadata (RFC 9728)
        // The MCP SDK's McpAuthenticationHandler normally serves this endpoint, but when
        // ResourceMetadataUri is set to an absolute external URL (required for IIS virtual
        // app hosting), the handler's path matching fails because IIS strips the /mcp prefix.
        // We map it explicitly so the 401 challenge's resource_metadata URL resolves correctly.
        app.MapGet($"{wellKnownPrefix}/oauth-protected-resource", HandleProtectedResourceMetadata)
            .AllowAnonymous()
            .WithName("ProtectedResourceMetadata")
            .WithTags("OAuth Metadata")
            .WithSummary("Returns Protected Resource Metadata (RFC 9728)");

        return app;
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Handles the Authorization Server Metadata endpoint.
    /// </summary>
    /// <remarks>
    /// Returns metadata including:
    /// - issuer: The authorization server's issuer identifier
    /// - authorization_endpoint: URL of the authorization endpoint
    /// - token_endpoint: URL of the token endpoint
    /// - registration_endpoint: URL for dynamic client registration
    /// - scopes_supported: List of supported OAuth scopes
    /// - response_types_supported: Supported response types
    /// - grant_types_supported: Supported grant types
    /// - code_challenge_methods_supported: PKCE methods (S256)
    /// </remarks>
    /**************************************************************/
    private static IResult HandleAuthorizationServerMetadata(
        IOptions<McpServerSettings> mcpSettings)
    {
        #region implementation
        var serverUrl = mcpSettings.Value.ServerUrl.TrimEnd('/');

        var metadata = new AuthorizationServerMetadata
        {
            Issuer = serverUrl,
            AuthorizationEndpoint = $"{serverUrl}/oauth/authorize",
            TokenEndpoint = $"{serverUrl}/oauth/token",
            RegistrationEndpoint = mcpSettings.Value.EnableDynamicClientRegistration
                ? $"{serverUrl}/oauth/register"
                : null,
            JwksUri = $"{serverUrl}/.well-known/jwks.json",
            ScopesSupported = mcpSettings.Value.ScopesSupported?.ToList() ?? new List<string>
            {
                "openid",
                "profile",
                "email",
                "mcp:tools",
                "mcp:read",
                "mcp:write"
            },
            ResponseTypesSupported = new List<string> { "code" },
            ResponseModesSupported = new List<string> { "query" },
            GrantTypesSupported = new List<string>
            {
                "authorization_code",
                "refresh_token"
            },
            TokenEndpointAuthMethodsSupported = new List<string>
            {
                "client_secret_post",
                "client_secret_basic",
                "none"
            },
            CodeChallengeMethodsSupported = new List<string> { "S256" },
            ServiceDocumentation = $"{serverUrl}/docs",
            UiLocalesSupported = new List<string> { "en" },

            // Client ID Metadata Document support
            ClientIdMetadataDocumentSupported = mcpSettings.Value.ClientIdMetadataDocumentSupported,

            // Additional OIDC fields for compatibility
            SubjectTypesSupported = new List<string> { "public" },
            IdTokenSigningAlgValuesSupported = new List<string> { "RS256", "HS256" }
        };

        return Results.Json(metadata, new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
        });
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Handles the Protected Resource Metadata endpoint (RFC 9728).
    /// </summary>
    /// <remarks>
    /// Returns metadata that tells MCP clients where to find the OAuth
    /// authorization server and what scopes/methods are supported.
    /// This endpoint is referenced by the WWW-Authenticate header in 401
    /// challenge responses (resource_metadata parameter).
    /// </remarks>
    /**************************************************************/
    private static IResult HandleProtectedResourceMetadata(
        IOptions<McpServerSettings> mcpSettings)
    {
        #region implementation
        var serverUrl = mcpSettings.Value.ServerUrl.TrimEnd('/');

        var metadata = new ProtectedResourceMetadataResponse
        {
            Resource = serverUrl,
            AuthorizationServers = new List<string> { serverUrl },
            ScopesSupported = mcpSettings.Value.ScopesSupported?.ToList() ?? new List<string>
            {
                "mcp:tools",
                "mcp:read",
                "mcp:write",
                "openid",
                "profile",
                "email"
            },
            BearerMethodsSupported = new List<string> { "header" },
            ResourceDocumentation = $"{serverUrl}/docs"
        };

        return Results.Json(metadata, new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
        });
        #endregion
    }
}

/**************************************************************/
/// <summary>
/// OAuth 2.0 Authorization Server Metadata (RFC 8414).
/// </summary>
/**************************************************************/
public class AuthorizationServerMetadata
{
    /**************************************************************/
    /// <summary>
    /// The authorization server's issuer identifier (URL).
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// URL of the authorization endpoint.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("authorization_endpoint")]
    public string AuthorizationEndpoint { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// URL of the token endpoint.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("token_endpoint")]
    public string TokenEndpoint { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// URL of the dynamic client registration endpoint.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("registration_endpoint")]
    public string? RegistrationEndpoint { get; set; }

    /**************************************************************/
    /// <summary>
    /// URL of the JSON Web Key Set document.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("jwks_uri")]
    public string? JwksUri { get; set; }

    /**************************************************************/
    /// <summary>
    /// List of supported OAuth scopes.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("scopes_supported")]
    public List<string>? ScopesSupported { get; set; }

    /**************************************************************/
    /// <summary>
    /// List of supported response types.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("response_types_supported")]
    public List<string> ResponseTypesSupported { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// List of supported response modes.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("response_modes_supported")]
    public List<string>? ResponseModesSupported { get; set; }

    /**************************************************************/
    /// <summary>
    /// List of supported grant types.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("grant_types_supported")]
    public List<string>? GrantTypesSupported { get; set; }

    /**************************************************************/
    /// <summary>
    /// List of supported token endpoint authentication methods.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public List<string>? TokenEndpointAuthMethodsSupported { get; set; }

    /**************************************************************/
    /// <summary>
    /// List of supported PKCE code challenge methods.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("code_challenge_methods_supported")]
    public List<string>? CodeChallengeMethodsSupported { get; set; }

    /**************************************************************/
    /// <summary>
    /// URL of the service documentation.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("service_documentation")]
    public string? ServiceDocumentation { get; set; }

    /**************************************************************/
    /// <summary>
    /// List of supported UI locales.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("ui_locales_supported")]
    public List<string>? UiLocalesSupported { get; set; }

    /**************************************************************/
    /// <summary>
    /// Whether Client ID Metadata Documents are supported.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("client_id_metadata_document_supported")]
    public bool? ClientIdMetadataDocumentSupported { get; set; }

    /**************************************************************/
    /// <summary>
    /// List of supported subject types (OIDC).
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("subject_types_supported")]
    public List<string>? SubjectTypesSupported { get; set; }

    /**************************************************************/
    /// <summary>
    /// List of supported ID token signing algorithms (OIDC).
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public List<string>? IdTokenSigningAlgValuesSupported { get; set; }
}

/**************************************************************/
/// <summary>
/// Protected Resource Metadata (RFC 9728).
/// </summary>
/// <remarks>
/// Describes a protected resource's OAuth configuration,
/// including which authorization servers can issue tokens for it
/// and what scopes/methods are supported.
/// </remarks>
/**************************************************************/
public class ProtectedResourceMetadataResponse
{
    /**************************************************************/
    /// <summary>
    /// The protected resource identifier (URL).
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("resource")]
    public string Resource { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// List of authorization server URLs that can issue tokens.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("authorization_servers")]
    public List<string> AuthorizationServers { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// List of supported OAuth scopes.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("scopes_supported")]
    public List<string>? ScopesSupported { get; set; }

    /**************************************************************/
    /// <summary>
    /// List of supported bearer token delivery methods.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("bearer_methods_supported")]
    public List<string>? BearerMethodsSupported { get; set; }

    /**************************************************************/
    /// <summary>
    /// URL of the resource documentation.
    /// </summary>
    /**************************************************************/
    [JsonPropertyName("resource_documentation")]
    public string? ResourceDocumentation { get; set; }
}
