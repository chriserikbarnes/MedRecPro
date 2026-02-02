/**************************************************************/
/// <summary>
/// Configuration settings for upstream OAuth identity providers.
/// </summary>
/// <remarks>
/// These settings configure Google and Microsoft OAuth integration
/// for user authentication. The MCP server acts as an OAuth client
/// to these providers while acting as an OAuth server to MCP clients.
/// </remarks>
/**************************************************************/

namespace MedRecProMCP.Configuration;

/**************************************************************/
/// <summary>
/// Container for all OAuth provider configurations.
/// </summary>
/// <seealso cref="GoogleOAuthSettings"/>
/// <seealso cref="MicrosoftOAuthSettings"/>
/**************************************************************/
public class OAuthProviderSettings
{
    /**************************************************************/
    /// <summary>
    /// Google OAuth provider configuration.
    /// </summary>
    /**************************************************************/
    public GoogleOAuthSettings Google { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Microsoft (Entra ID) OAuth provider configuration.
    /// </summary>
    /**************************************************************/
    public MicrosoftOAuthSettings Microsoft { get; set; } = new();
}

/**************************************************************/
/// <summary>
/// Google OAuth provider configuration.
/// </summary>
/// <remarks>
/// Google OAuth uses the authorization code flow with PKCE.
/// Scopes typically include openid, profile, and email.
/// </remarks>
/**************************************************************/
public class GoogleOAuthSettings
{
    /**************************************************************/
    /// <summary>
    /// Google OAuth Client ID.
    /// </summary>
    /// <remarks>
    /// Obtained from Google Cloud Console.
    /// In production, stored in Azure Key Vault.
    /// </remarks>
    /**************************************************************/
    public string ClientId { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Google OAuth Client Secret.
    /// </summary>
    /// <remarks>
    /// Obtained from Google Cloud Console.
    /// In production, stored in Azure Key Vault.
    /// </remarks>
    /**************************************************************/
    public string ClientSecret { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Google OAuth Authorization Endpoint.
    /// </summary>
    /**************************************************************/
    public string AuthorizationEndpoint { get; set; } = "https://accounts.google.com/o/oauth2/v2/auth";

    /**************************************************************/
    /// <summary>
    /// Google OAuth Token Endpoint.
    /// </summary>
    /**************************************************************/
    public string TokenEndpoint { get; set; } = "https://oauth2.googleapis.com/token";

    /**************************************************************/
    /// <summary>
    /// Google OpenID Connect UserInfo Endpoint.
    /// </summary>
    /**************************************************************/
    public string UserInfoEndpoint { get; set; } = "https://openidconnect.googleapis.com/v1/userinfo";

    /**************************************************************/
    /// <summary>
    /// Default scopes to request from Google.
    /// </summary>
    /**************************************************************/
    public string[] Scopes { get; set; } = { "openid", "profile", "email" };
}

/**************************************************************/
/// <summary>
/// Microsoft (Entra ID) OAuth provider configuration.
/// </summary>
/// <remarks>
/// Microsoft OAuth uses the authorization code flow with PKCE.
/// Supports both personal accounts and organizational accounts.
/// </remarks>
/**************************************************************/
public class MicrosoftOAuthSettings
{
    /**************************************************************/
    /// <summary>
    /// Microsoft OAuth Client ID (Application ID).
    /// </summary>
    /// <remarks>
    /// Obtained from Azure Portal App Registration.
    /// In production, stored in Azure Key Vault.
    /// </remarks>
    /**************************************************************/
    public string ClientId { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Microsoft OAuth Client Secret.
    /// </summary>
    /// <remarks>
    /// Obtained from Azure Portal App Registration.
    /// In production, stored in Azure Key Vault.
    /// </remarks>
    /**************************************************************/
    public string ClientSecret { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Azure AD Tenant ID.
    /// </summary>
    /// <remarks>
    /// Use "common" for multi-tenant apps supporting both personal
    /// and organizational accounts.
    /// </remarks>
    /**************************************************************/
    public string TenantId { get; set; } = "common";

    /**************************************************************/
    /// <summary>
    /// Microsoft OAuth Authorization Endpoint.
    /// </summary>
    /// <remarks>
    /// Dynamically constructed using TenantId.
    /// </remarks>
    /**************************************************************/
    public string AuthorizationEndpoint =>
        $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/authorize";

    /**************************************************************/
    /// <summary>
    /// Microsoft OAuth Token Endpoint.
    /// </summary>
    /// <remarks>
    /// Dynamically constructed using TenantId.
    /// </remarks>
    /**************************************************************/
    public string TokenEndpoint =>
        $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token";

    /**************************************************************/
    /// <summary>
    /// Microsoft Graph UserInfo Endpoint.
    /// </summary>
    /**************************************************************/
    public string UserInfoEndpoint { get; set; } = "https://graph.microsoft.com/v1.0/me";

    /**************************************************************/
    /// <summary>
    /// Default scopes to request from Microsoft.
    /// </summary>
    /**************************************************************/
    public string[] Scopes { get; set; } = { "openid", "profile", "email", "User.Read" };
}
