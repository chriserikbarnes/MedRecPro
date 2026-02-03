/**************************************************************/
/// <summary>
/// Configuration settings for upstream OAuth identity providers.
/// </summary>
/// <remarks>
/// These settings configure Google and Microsoft OAuth integration
/// for user authentication. The MCP server acts as an OAuth client
/// to these providers while acting as an OAuth server to MCP clients.
/// Maps to secrets: Authentication:Google and Authentication:Microsoft
/// </remarks>
/**************************************************************/

namespace MedRecProMCP.Configuration;

/**************************************************************/
/// <summary>
/// Container for all authentication provider configurations.
/// </summary>
/// <remarks>
/// Binds to the "Authentication" section in configuration.
/// </remarks>
/// <seealso cref="GoogleAuthSettings"/>
/// <seealso cref="MicrosoftAuthSettings"/>
/**************************************************************/
public class AuthenticationSettings
{
    /**************************************************************/
    /// <summary>
    /// Google OAuth provider configuration.
    /// </summary>
    /// <remarks>
    /// Maps to secrets: Authentication:Google:*
    /// </remarks>
    /**************************************************************/
    public GoogleAuthSettings Google { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Microsoft (Entra ID) OAuth provider configuration.
    /// </summary>
    /// <remarks>
    /// Maps to secrets: Authentication:Microsoft:*
    /// </remarks>
    /**************************************************************/
    public MicrosoftAuthSettings Microsoft { get; set; } = new();
}

/**************************************************************/
/// <summary>
/// Legacy class for backward compatibility.
/// </summary>
/// <remarks>
/// Use <see cref="AuthenticationSettings"/> instead.
/// </remarks>
/**************************************************************/
[Obsolete("Use AuthenticationSettings instead. This class exists for backward compatibility.")]
public class OAuthProviderSettings : AuthenticationSettings
{
}

/**************************************************************/
/// <summary>
/// Google OAuth provider configuration.
/// </summary>
/// <remarks>
/// Google OAuth uses the authorization code flow with PKCE.
/// Scopes typically include openid, profile, and email.
/// Maps to secrets: Authentication:Google:*
/// </remarks>
/**************************************************************/
public class GoogleAuthSettings
{
    /**************************************************************/
    /// <summary>
    /// Google OAuth Client ID.
    /// </summary>
    /// <remarks>
    /// Obtained from Google Cloud Console.
    /// In production, stored in Azure Key Vault.
    /// Maps to secrets: Authentication:Google:ClientId
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
    /// Maps to secrets: Authentication:Google:ClientSecret
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
/// Legacy class for backward compatibility.
/// </summary>
/// <remarks>
/// Use <see cref="GoogleAuthSettings"/> instead.
/// </remarks>
/**************************************************************/
[Obsolete("Use GoogleAuthSettings instead. This class exists for backward compatibility.")]
public class GoogleOAuthSettings : GoogleAuthSettings
{
}

/**************************************************************/
/// <summary>
/// Microsoft (Entra ID) OAuth provider configuration.
/// </summary>
/// <remarks>
/// Microsoft OAuth uses the authorization code flow with PKCE.
/// Supports both personal accounts and organizational accounts.
/// Maps to secrets: Authentication:Microsoft:*
/// </remarks>
/**************************************************************/
public class MicrosoftAuthSettings
{
    /**************************************************************/
    /// <summary>
    /// Microsoft OAuth Client ID (Application ID).
    /// </summary>
    /// <remarks>
    /// Obtained from Azure Portal App Registration.
    /// In production, stored in Azure Key Vault.
    /// Maps to secrets: Authentication:Microsoft:ClientId
    /// </remarks>
    /**************************************************************/
    public string ClientId { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Microsoft OAuth Client Secret for development environment.
    /// </summary>
    /// <remarks>
    /// Obtained from Azure Portal App Registration.
    /// In production, stored in Azure Key Vault.
    /// Maps to secrets: Authentication:Microsoft:ClientSecret:Dev
    /// </remarks>
    /**************************************************************/
    public MicrosoftClientSecretSettings ClientSecret { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Azure AD Tenant ID.
    /// </summary>
    /// <remarks>
    /// Use "common" for multi-tenant apps supporting both personal
    /// and organizational accounts.
    /// Maps to secrets: Authentication:Microsoft:TenantId
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

/**************************************************************/
/// <summary>
/// Microsoft client secret settings with Dev/Prod separation.
/// </summary>
/// <remarks>
/// Maps to secrets: Authentication:Microsoft:ClientSecret:Dev/Prod
/// </remarks>
/**************************************************************/
public class MicrosoftClientSecretSettings
{
    /**************************************************************/
    /// <summary>
    /// Client secret for development environment.
    /// </summary>
    /// <remarks>
    /// Maps to secrets: Authentication:Microsoft:ClientSecret:Dev
    /// </remarks>
    /**************************************************************/
    public string Dev { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Client secret for production environment.
    /// </summary>
    /// <remarks>
    /// Maps to secrets: Authentication:Microsoft:ClientSecret:Prod
    /// </remarks>
    /**************************************************************/
    public string Prod { get; set; } = string.Empty;
}

/**************************************************************/
/// <summary>
/// Legacy class for backward compatibility.
/// </summary>
/// <remarks>
/// Use <see cref="MicrosoftAuthSettings"/> instead.
/// </remarks>
/**************************************************************/
[Obsolete("Use MicrosoftAuthSettings instead. This class exists for backward compatibility.")]
public class MicrosoftOAuthSettings : MicrosoftAuthSettings
{
}
