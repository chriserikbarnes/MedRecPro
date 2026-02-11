/**************************************************************/
/// <summary>
/// OAuth 2.1 endpoint implementations for the MCP Server.
/// </summary>
/// <remarks>
/// This class implements all OAuth endpoints required for MCP authorization:
/// - /oauth/authorize - Initiates authorization flow
/// - /oauth/token - Token exchange endpoint
/// - /oauth/register - Dynamic Client Registration (RFC 7591)
/// - /oauth/callback/google - Google OAuth callback
/// - /oauth/callback/microsoft - Microsoft OAuth callback
///
/// The MCP server acts as an OAuth Authorization Server to MCP clients
/// while delegating actual user authentication to Google/Microsoft.
/// </remarks>
/// <seealso cref="IOAuthService"/>
/// <seealso cref="IMcpTokenService"/>
/// <seealso cref="IClientRegistrationService"/>
/**************************************************************/

using MedRecProMCP.Configuration;
using MedRecProMCP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedRecProMCP.Endpoints;

/**************************************************************/
/// <summary>
/// Extension methods for mapping OAuth endpoints.
/// </summary>
/**************************************************************/
public static class OAuthEndpoints
{
    /**************************************************************/
    /// <summary>
    /// Maps all OAuth endpoints to the application.
    /// </summary>
    /// <param name="app">The WebApplication instance.</param>
    /// <returns>The WebApplication for chaining.</returns>
    /**************************************************************/
    public static WebApplication MapOAuthEndpoints(this WebApplication app)
    {
        #region implementation
        /**************************************************************/
        /// DEBUG: Routes include /mcp prefix since app runs standalone.
        ///        Full path: /mcp/oauth/authorize, /mcp/oauth/token, etc.
        /// RELEASE: IIS virtual app at /mcp strips the prefix.
        ///        Internal path: /oauth/authorize → External: /mcp/oauth/authorize
        /**************************************************************/
#if DEBUG
        var group = app.MapGroup("/mcp/oauth")
#else
        var group = app.MapGroup("/oauth")
#endif
            .WithTags("OAuth")
            .AllowAnonymous();

        // Authorization endpoint - initiates the OAuth flow
        group.MapGet("/authorize", HandleAuthorize)
            .WithName("OAuthAuthorize")
            .WithSummary("Initiates OAuth 2.1 authorization flow");

        // Token endpoint - exchanges code for tokens
        group.MapPost("/token", HandleToken)
            .WithName("OAuthToken")
            .WithSummary("Exchanges authorization code for tokens");

        // Dynamic Client Registration endpoint
        group.MapPost("/register", HandleRegister)
            .WithName("OAuthRegister")
            .WithSummary("Registers a new OAuth client dynamically");

        // Provider callbacks
        group.MapGet("/callback/google", HandleGoogleCallback)
            .WithName("OAuthGoogleCallback")
            .WithSummary("Handles Google OAuth callback");

        group.MapGet("/callback/microsoft", HandleMicrosoftCallback)
            .WithName("OAuthMicrosoftCallback")
            .WithSummary("Handles Microsoft OAuth callback");

        return app;
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Handles the authorization endpoint (/oauth/authorize).
    /// </summary>
    /// <remarks>
    /// This endpoint validates the authorization request and redirects
    /// the user to the appropriate identity provider (Google/Microsoft).
    ///
    /// Required parameters:
    /// - response_type: Must be "code"
    /// - client_id: The registered client identifier
    /// - redirect_uri: Must match a registered redirect URI
    /// - code_challenge: PKCE code challenge (required)
    /// - code_challenge_method: Must be "S256"
    /// - state: CSRF protection (required)
    ///
    /// Optional parameters:
    /// - scope: Space-separated scopes (defaults to configured scopes)
    /// - provider: "google" or "microsoft" (defaults to "google")
    /// </remarks>
    /**************************************************************/
    private static async Task<IResult> HandleAuthorize(
        HttpContext context,
        [FromQuery] string response_type,
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string code_challenge,
        [FromQuery] string code_challenge_method,
        [FromQuery] string state,
        [FromQuery] string? scope,
        [FromQuery] string? provider,
        IClientRegistrationService clientService,
        IOAuthService oauthService,
        IPkceService pkceService,
        IOptions<McpServerSettings> mcpSettings,
        ILogger<Program> logger)
    {
        #region implementation
        // Validate response_type
        if (response_type != "code")
        {
            return Results.BadRequest(new OAuthError
            {
                Error = "unsupported_response_type",
                ErrorDescription = "Only 'code' response type is supported"
            });
        }

        // Validate code challenge method (PKCE required)
        if (code_challenge_method != "S256")
        {
            return Results.BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "code_challenge_method must be 'S256'"
            });
        }

        if (string.IsNullOrEmpty(code_challenge))
        {
            return Results.BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "code_challenge is required"
            });
        }

        if (string.IsNullOrEmpty(state))
        {
            return Results.BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "state is required"
            });
        }

        // Validate client
        var client = await clientService.ValidateClientAsync(client_id);
        if (client == null)
        {
            return Results.BadRequest(new OAuthError
            {
                Error = "invalid_client",
                ErrorDescription = "Unknown client_id"
            });
        }

        // Validate redirect URI
        if (!await clientService.ValidateRedirectUriAsync(client_id, redirect_uri))
        {
            return Results.BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "Invalid redirect_uri"
            });
        }

        // Determine provider (default to Google)
        var selectedProvider = provider?.ToLowerInvariant() ?? "google";
        if (!oauthService.IsProviderSupported(selectedProvider))
        {
            return Results.BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = $"Unsupported provider: {provider}"
            });
        }

        // Parse scopes
        var scopes = string.IsNullOrEmpty(scope)
            ? mcpSettings.Value.ScopesSupported ?? new[] { "openid", "profile", "email", "mcp:tools" }
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Generate our own state for the upstream provider
        var upstreamState = pkceService.GenerateState();

        // Generate PKCE for upstream provider
        var (upstreamVerifier, upstreamChallenge) = pkceService.GenerateCodeChallengePair();

        // Store PKCE data: upstream verifier (for Google/Microsoft) + client's code_challenge (from Claude)
        await pkceService.StorePkceDataAsync(
            state, upstreamVerifier, code_challenge, client_id, redirect_uri, scopes);

        // Also store mapping from upstream state to client state (persisted to survive restarts)
        var persistedCache = context.RequestServices.GetRequiredService<IPersistedCacheService>();
        await persistedCache.SetAsync($"oauth_upstream_state_{upstreamState}", state, TimeSpan.FromMinutes(10));

        // Build callback URL for the upstream provider
        var callbackUri = $"{mcpSettings.Value.ServerUrl.TrimEnd('/')}/oauth/callback/{selectedProvider}";

        // Redirect to upstream provider
        var authUrl = oauthService.GetAuthorizationUrl(
            selectedProvider,
            upstreamState,
            upstreamChallenge,
            callbackUri,
            scopes);

        logger.LogInformation(
            "[OAuth] Redirecting to {Provider} for client {ClientId}",
            selectedProvider, client_id);

        return Results.Redirect(authUrl);
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Handles the token endpoint (/oauth/token).
    /// </summary>
    /// <remarks>
    /// Supports grant types:
    /// - authorization_code: Exchanges code for tokens
    /// - refresh_token: Refreshes an existing token
    ///
    /// For authorization_code:
    /// - code: The authorization code
    /// - redirect_uri: Must match the authorization request
    /// - code_verifier: PKCE code verifier
    /// - client_id: The client identifier
    ///
    /// For refresh_token:
    /// - refresh_token: The refresh token
    /// - client_id: The client identifier
    /// </remarks>
    /**************************************************************/
    private static async Task<IResult> HandleToken(
        HttpContext context,
        IClientRegistrationService clientService,
        IMcpTokenService tokenService,
        ILogger<Program> logger)
    {
        #region implementation
        // Parse form data
        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[OAuth] Failed to read form data. Content-Type: {ContentType}",
                context.Request.ContentType);
            return Results.Json(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "Request body must be application/x-www-form-urlencoded"
            }, statusCode: 400);
        }

        var grantType = form["grant_type"].ToString();
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();

        logger.LogInformation(
            "[OAuth] Token request: grant_type={GrantType}, client_id={ClientId}, has_secret={HasSecret}",
            grantType, clientId, !string.IsNullOrEmpty(clientSecret));

        // Validate client
        var client = await clientService.ValidateClientAsync(clientId, clientSecret);
        if (client == null)
        {
            logger.LogWarning(
                "[OAuth] Client validation failed for client_id={ClientId}", clientId);
            return Results.Json(new OAuthError
            {
                Error = "invalid_client",
                ErrorDescription = "Client authentication failed"
            }, statusCode: 401);
        }

        switch (grantType)
        {
            case "authorization_code":
                return await handleAuthorizationCodeGrant(context, form, client, tokenService, logger);

            case "refresh_token":
                return await handleRefreshTokenGrant(form, client, tokenService, logger);

            default:
                logger.LogWarning("[OAuth] Unsupported grant_type: {GrantType}", grantType);
                return Results.Json(new OAuthError
                {
                    Error = "unsupported_grant_type",
                    ErrorDescription = $"Grant type '{grantType}' is not supported"
                }, statusCode: 400);
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Handles authorization code grant type.
    /// </summary>
    /**************************************************************/
    private static async Task<IResult> handleAuthorizationCodeGrant(
        HttpContext context,
        IFormCollection form,
        RegisteredClient client,
        IMcpTokenService tokenService,
        ILogger<Program> logger)
    {
        #region implementation
        var code = form["code"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        var codeVerifier = form["code_verifier"].ToString();

        logger.LogInformation(
            "[OAuth] Auth code grant: has_code={HasCode}, has_redirect_uri={HasRedirectUri}, has_code_verifier={HasCodeVerifier}",
            !string.IsNullOrEmpty(code), !string.IsNullOrEmpty(redirectUri), !string.IsNullOrEmpty(codeVerifier));

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(redirectUri) || string.IsNullOrEmpty(codeVerifier))
        {
            logger.LogWarning("[OAuth] Missing required parameters in token request");
            return Results.Json(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "code, redirect_uri, and code_verifier are required"
            }, statusCode: 400);
        }

        // Retrieve stored authorization data using the code (from persistent cache)
        var persistedCache = context.RequestServices.GetRequiredService<IPersistedCacheService>();
        var cacheKey = $"oauth_auth_code_{code}";

        var (found, authData) = await persistedCache.TryGetAsync<AuthorizationCodeData>(cacheKey);
        if (!found || authData == null)
        {
            logger.LogWarning("[OAuth] Auth code not found in cache: {CacheKey}", cacheKey);
            return Results.Json(new OAuthError
            {
                Error = "invalid_grant",
                ErrorDescription = "Invalid or expired authorization code"
            }, statusCode: 400);
        }

        // Remove the code (one-time use)
        await persistedCache.RemoveAsync(cacheKey);

        // Validate redirect_uri matches
        if (!authData.RedirectUri.Equals(redirectUri, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "[OAuth] redirect_uri mismatch. Expected: {Expected}, Got: {Got}",
                authData.RedirectUri, redirectUri);
            return Results.Json(new OAuthError
            {
                Error = "invalid_grant",
                ErrorDescription = "redirect_uri mismatch"
            }, statusCode: 400);
        }

        // Validate PKCE code verifier
        var pkceService = context.RequestServices.GetRequiredService<IPkceService>();
        if (!pkceService.ValidateCodeVerifier(codeVerifier, authData.CodeChallenge))
        {
            logger.LogWarning("[OAuth] PKCE code_verifier validation failed");
            return Results.Json(new OAuthError
            {
                Error = "invalid_grant",
                ErrorDescription = "PKCE verification failed"
            }, statusCode: 400);
        }

        // Generate MCP tokens (convert serializable claims back to Claim objects)
        var claims = authData.ToClaimsList();
        var tokenResponse = await tokenService.GenerateAccessTokenAsync(
            claims,
            authData.UpstreamAccessToken,
            authData.UpstreamRefreshToken,
            authData.Scopes,
            client.ClientId);

        logger.LogInformation(
            "[OAuth] Issued tokens for client {ClientId}, user {User}",
            client.ClientId, claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value);

        return Results.Json(tokenResponse);
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Handles refresh token grant type.
    /// </summary>
    /**************************************************************/
    private static async Task<IResult> handleRefreshTokenGrant(
        IFormCollection form,
        RegisteredClient client,
        IMcpTokenService tokenService,
        ILogger<Program> logger)
    {
        #region implementation
        var refreshToken = form["refresh_token"].ToString();

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Results.Json(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "refresh_token is required"
            }, statusCode: 400);
        }

        var tokenResponse = await tokenService.RefreshAccessTokenAsync(refreshToken, client.ClientId);
        if (tokenResponse == null)
        {
            return Results.Json(new OAuthError
            {
                Error = "invalid_grant",
                ErrorDescription = "Invalid or expired refresh token"
            }, statusCode: 400);
        }

        logger.LogInformation("[OAuth] Refreshed tokens for client {ClientId}", client.ClientId);

        return Results.Json(tokenResponse);
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Handles Dynamic Client Registration (/oauth/register).
    /// </summary>
    /**************************************************************/
    private static async Task<IResult> HandleRegister(
        HttpContext context,
        IClientRegistrationService clientService,
        IOptions<McpServerSettings> mcpSettings,
        ILogger<Program> logger)
    {
        #region implementation
        if (!mcpSettings.Value.EnableDynamicClientRegistration)
        {
            return Results.Json(new OAuthError
            {
                Error = "registration_not_supported",
                ErrorDescription = "Dynamic client registration is not enabled"
            }, statusCode: 400);
        }

        try
        {
            var request = await context.Request.ReadFromJsonAsync<ClientRegistrationRequest>();
            if (request == null)
            {
                return Results.Json(new OAuthError
                {
                    Error = "invalid_request",
                    ErrorDescription = "Invalid request body"
                }, statusCode: 400);
            }

            var response = await clientService.RegisterClientAsync(request);

            logger.LogInformation(
                "[OAuth] Registered new client: {ClientName} ({ClientId})",
                response.ClientName, response.ClientId);

            return Results.Json(response, statusCode: 201);
        }
        catch (ArgumentException ex)
        {
            return Results.Json(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = ex.Message
            }, statusCode: 400);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[OAuth] Client registration failed");
            return Results.Json(new OAuthError
            {
                Error = "server_error",
                ErrorDescription = "Registration failed"
            }, statusCode: 500);
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Handles Google OAuth callback.
    /// </summary>
    /**************************************************************/
    private static async Task<IResult> HandleGoogleCallback(
        HttpContext context,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery] string? error_description,
        IOAuthService oauthService,
        IPkceService pkceService,
        IOptions<McpServerSettings> mcpSettings,
        ILogger<Program> logger)
    {
        return await handleProviderCallback(
            context, "google", code, state, error, error_description,
            oauthService, pkceService, mcpSettings, logger);
    }

    /**************************************************************/
    /// <summary>
    /// Handles Microsoft OAuth callback.
    /// </summary>
    /**************************************************************/
    private static async Task<IResult> HandleMicrosoftCallback(
        HttpContext context,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery] string? error_description,
        IOAuthService oauthService,
        IPkceService pkceService,
        IOptions<McpServerSettings> mcpSettings,
        ILogger<Program> logger)
    {
        return await handleProviderCallback(
            context, "microsoft", code, state, error, error_description,
            oauthService, pkceService, mcpSettings, logger);
    }

    /**************************************************************/
    /// <summary>
    /// Common handler for OAuth provider callbacks.
    /// </summary>
    /**************************************************************/
    private static async Task<IResult> handleProviderCallback(
        HttpContext context,
        string provider,
        string? code,
        string? upstreamState,
        string? error,
        string? errorDescription,
        IOAuthService oauthService,
        IPkceService pkceService,
        IOptions<McpServerSettings> mcpSettings,
        ILogger<Program> logger)
    {
        #region implementation
        // Check for error from provider
        if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning(
                "[OAuth] {Provider} returned error: {Error} - {Description}",
                provider, error, errorDescription);

            return Results.BadRequest(new OAuthError
            {
                Error = "access_denied",
                ErrorDescription = errorDescription ?? "Authorization denied by user"
            });
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(upstreamState))
        {
            return Results.BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "Missing code or state parameter"
            });
        }

        // Look up client state from upstream state (from persistent cache)
        var persistedCache = context.RequestServices.GetRequiredService<IPersistedCacheService>();
        var stateKey = $"oauth_upstream_state_{upstreamState}";

        var (stateFound, clientState) = await persistedCache.TryGetAsync<string>(stateKey);
        if (!stateFound || string.IsNullOrEmpty(clientState))
        {
            return Results.BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "Invalid or expired state"
            });
        }

        await persistedCache.RemoveAsync(stateKey);

        // Get stored PKCE data
        var pkceData = await pkceService.GetPkceDataAsync(clientState);
        if (pkceData == null)
        {
            return Results.BadRequest(new OAuthError
            {
                Error = "invalid_request",
                ErrorDescription = "Session expired"
            });
        }

        // Clean up PKCE data
        await pkceService.RemovePkceDataAsync(clientState);

        // Build callback URI
        var callbackUri = $"{mcpSettings.Value.ServerUrl.TrimEnd('/')}/oauth/callback/{provider}";

        // Exchange code for tokens from upstream provider
        var tokenResult = await oauthService.ExchangeCodeForTokensAsync(
            provider,
            code,
            pkceData.CodeVerifier,
            callbackUri);

        if (tokenResult == null)
        {
            return Results.BadRequest(new OAuthError
            {
                Error = "invalid_grant",
                ErrorDescription = "Failed to exchange authorization code"
            });
        }

        // Build user claims from upstream user info
        var userClaims = new List<Claim>();
        if (tokenResult.UserInfo != null)
        {
            userClaims.Add(new Claim(ClaimTypes.NameIdentifier, tokenResult.UserInfo.Id));
            if (!string.IsNullOrEmpty(tokenResult.UserInfo.Email))
                userClaims.Add(new Claim(ClaimTypes.Email, tokenResult.UserInfo.Email));
            if (!string.IsNullOrEmpty(tokenResult.UserInfo.Name))
                userClaims.Add(new Claim(ClaimTypes.Name, tokenResult.UserInfo.Name));
            if (!string.IsNullOrEmpty(tokenResult.UserInfo.GivenName))
                userClaims.Add(new Claim(ClaimTypes.GivenName, tokenResult.UserInfo.GivenName));
            if (!string.IsNullOrEmpty(tokenResult.UserInfo.FamilyName))
                userClaims.Add(new Claim(ClaimTypes.Surname, tokenResult.UserInfo.FamilyName));
            if (!string.IsNullOrEmpty(tokenResult.UserInfo.Picture))
                userClaims.Add(new Claim("picture", tokenResult.UserInfo.Picture));
            userClaims.Add(new Claim("provider", provider));
        }

        // Resolve the upstream IdP identity to a numeric MedRecPro database user ID.
        // The API's getEncryptedIdFromClaim() expects a numeric NameIdentifier, but
        // the upstream IdP provides a Google sub or Microsoft GUID. This resolution
        // replaces the NameIdentifier with the actual database user ID.
        var userEmail = userClaims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        if (!string.IsNullOrEmpty(userEmail))
        {
            var userResolutionService = context.RequestServices.GetRequiredService<IUserResolutionService>();
            var resolvedUserId = await userResolutionService.ResolveUserIdAsync(
                userEmail,
                tokenResult.AccessToken,
                userClaims);

            if (resolvedUserId.HasValue)
            {
                // Replace the upstream IdP identifier with the numeric database user ID
                userClaims.RemoveAll(c => c.Type == ClaimTypes.NameIdentifier);
                userClaims.Add(new Claim(ClaimTypes.NameIdentifier, resolvedUserId.Value.ToString()));

                logger.LogInformation(
                    "[OAuth] Resolved {Email} to MedRecPro user ID {UserId}",
                    userEmail, resolvedUserId.Value);
            }
            else
            {
                // With auto-provisioning in place, a resolution failure means
                // something went wrong server-side (DB error, network issue).
                // OAuth still succeeds but MCP tools that call the API will fail.
                logger.LogError(
                    "[OAuth] Could not resolve {Email} to a MedRecPro user. Auto-provisioning may have failed. MCP tools requiring API access will fail.",
                    userEmail);
            }
        }

        // Generate our own authorization code
        var ourCode = Guid.NewGuid().ToString("N");

        // Store authorization data for token exchange
        var authCodeData = new AuthorizationCodeData
        {
            UserClaims = AuthorizationCodeData.FromClaims(userClaims),
            UpstreamAccessToken = tokenResult.AccessToken,
            UpstreamRefreshToken = tokenResult.RefreshToken,
            Scopes = pkceData.Scopes,
            CodeChallenge = pkceData.CodeChallenge,
            RedirectUri = pkceData.RedirectUri,
            ClientId = pkceData.ClientId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        await persistedCache.SetAsync($"oauth_auth_code_{ourCode}", authCodeData, TimeSpan.FromMinutes(5));

        // Build redirect URL to client with our authorization code
        var redirectUrl = $"{pkceData.RedirectUri}?code={Uri.EscapeDataString(ourCode)}&state={Uri.EscapeDataString(clientState)}";

        logger.LogInformation(
            "[OAuth] {Provider} authentication successful for {Email}, redirecting to client",
            provider, tokenResult.UserInfo?.Email);

        return Results.Redirect(redirectUrl);
        #endregion
    }
}

/**************************************************************/
/// <summary>
/// OAuth error response format.
/// </summary>
/**************************************************************/
public class OAuthError
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    [JsonPropertyName("error_uri")]
    public string? ErrorUri { get; set; }
}

/**************************************************************/
/// <summary>
/// Internal data structure for authorization code storage.
/// </summary>
/// <remarks>
/// Uses SerializableClaim instead of System.Security.Claims.Claim
/// to support JSON serialization in the persistent file cache.
/// </remarks>
/**************************************************************/
internal class AuthorizationCodeData
{
    public List<SerializableClaim> UserClaims { get; set; } = new();
    public string UpstreamAccessToken { get; set; } = string.Empty;
    public string? UpstreamRefreshToken { get; set; }
    public List<string> Scopes { get; set; } = new();
    public string CodeChallenge { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    /**************************************************************/
    /// <summary>
    /// Converts the serializable claims back to Claim objects.
    /// </summary>
    /**************************************************************/
    public List<Claim> ToClaimsList()
    {
        return UserClaims.Select(c => new Claim(c.Type, c.Value)).ToList();
    }

    /**************************************************************/
    /// <summary>
    /// Creates an AuthorizationCodeData from Claim objects.
    /// </summary>
    /**************************************************************/
    public static List<SerializableClaim> FromClaims(IEnumerable<Claim> claims)
    {
        return claims.Select(c => new SerializableClaim
        {
            Type = c.Type,
            Value = c.Value
        }).ToList();
    }
}

/**************************************************************/
/// <summary>
/// JSON-serializable representation of a security claim.
/// </summary>
/// <remarks>
/// System.Security.Claims.Claim does not serialize cleanly to JSON
/// due to circular references and non-public properties. This DTO
/// captures the essential Type/Value pair for cache persistence.
/// </remarks>
/**************************************************************/
internal class SerializableClaim
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

