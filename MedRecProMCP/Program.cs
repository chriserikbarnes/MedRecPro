/**************************************************************/
/// <summary>
/// MedRecPro MCP Server Application.
///
/// This is a standalone ASP.NET Core MCP server that acts as a thin gateway
/// in front of the existing MedRecPro Web API (https://www.medrecpro.com/api).
/// It implements OAuth 2.1 pass-through authentication, delegating actual
/// authentication to Google and Microsoft identity providers while issuing
/// its own MCP access tokens for Claude and other MCP clients.
///
/// Architecture:
/// - MCP Server: Translates MCP tool invocations into HTTP calls against MedRecPro API
/// - MedRecPro API: Existing ASP.NET Core 8 Web API with full business logic
///
/// Authentication Flow:
/// 1. Claude (MCP client) authenticates the end user via OAuth
/// 2. MCP server receives and validates the access token
/// 3. MCP server forwards token to MedRecPro API on every request
/// 4. MedRecPro API validates token and applies authorization rules
/// </summary>
/// <remarks>
/// Key Endpoints (external URLs when deployed at /mcp virtual application):
/// - /mcp - MCP Streamable HTTP transport (MapMcp)
/// - /mcp/.well-known/oauth-protected-resource - PRM document (RFC 9728)
/// - /mcp/.well-known/oauth-authorization-server - AS metadata (RFC 8414)
/// - /mcp/oauth/authorize - Authorization endpoint (redirects to Google/Microsoft)
/// - /mcp/oauth/token - Token endpoint (exchanges codes for MCP access tokens)
/// - /mcp/oauth/register - Dynamic Client Registration (RFC 7591)
/// - /mcp/oauth/callback/google - Callback from Google
/// - /mcp/oauth/callback/microsoft - Callback from Microsoft
///
/// Route paths use compiler directives (#if DEBUG) to handle the IIS virtual
/// application path stripping, following the same pattern as ApiControllerBase.
/// In DEBUG, routes include the /mcp prefix. In RELEASE, IIS strips it.
/// </remarks>
/// <seealso cref="MedRecProMCP.Services.McpTokenService"/>
/// <seealso cref="MedRecProMCP.Services.OAuthService"/>
/// <seealso cref="MedRecProMCP.Handlers.TokenForwardingHandler"/>
/**************************************************************/

using Azure.Identity;
using MedRecProMCP.Configuration;
using MedRecProMCP.Endpoints;
using MedRecProMCP.Handlers;
using MedRecProMCP.Models;
using MedRecProMCP.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

#region Azure Key Vault Configuration
/**************************************************************/
/// <summary>
/// Configures Azure Key Vault for production secrets management.
/// </summary>
/// <remarks>
/// In production, all OAuth client secrets and JWT signing keys are
/// stored in Azure Key Vault and retrieved using managed identity.
/// </remarks>
/**************************************************************/
if (builder.Environment.IsProduction())
{
    var keyVaultUrl = configuration["KeyVaultUrl"];
    if (!string.IsNullOrEmpty(keyVaultUrl))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultUrl),
            new DefaultAzureCredential());
    }
}
#endregion

#region Configuration Bindings
/**************************************************************/
/// <summary>
/// Binds configuration sections to strongly-typed options classes.
/// </summary>
/// <remarks>
/// Configuration paths match secrets.json key structure:
/// - McpServer: McpServer:*
/// - MedRecProApi: MedRecProApi:*
/// - Authentication: Authentication:Google:*, Authentication:Microsoft:*
/// - Jwt: Jwt:Key, Jwt:Issuer, Jwt:Audience, Jwt:ExpirationMinutes
/// </remarks>
/**************************************************************/
builder.Services.Configure<McpServerSettings>(configuration.GetSection("McpServer"));
builder.Services.Configure<MedRecProApiSettings>(configuration.GetSection("MedRecProApi"));
builder.Services.Configure<AuthenticationSettings>(configuration.GetSection("Authentication"));
builder.Services.Configure<JwtSettings>(configuration.GetSection("Jwt"));


#pragma warning disable CS0618 // Type or member is obsolete - backward compatibility
builder.Services.Configure<OAuthProviderSettings>(configuration.GetSection("Authentication"));
#pragma warning restore CS0618

var mcpSettings = configuration.GetSection("McpServer").Get<McpServerSettings>()
    ?? throw new InvalidOperationException("McpServer configuration section is required.");
var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt configuration section is required.");
var medrecProApiSettings = configuration.GetSection("MedRecProApi").Get<MedRecProApiSettings>()
    ?? throw new InvalidOperationException("MedRecProApi configuration section is required.");
var authSettings = configuration.GetSection("Authentication").Get<AuthenticationSettings>()
    ?? throw new InvalidOperationException("Authentication configuration section is required.");
#endregion

#region Core Services
/**************************************************************/
/// <summary>
/// Registers core application services.
/// </summary>
/**************************************************************/
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

// Register persistent file-based cache (survives Kestrel process restarts)
builder.Services.AddSingleton<IPersistedCacheService, FilePersistedCacheService>();

// Register token and OAuth services
builder.Services.AddSingleton<IMcpTokenService, McpTokenService>();
builder.Services.AddSingleton<IOAuthService, OAuthService>();
builder.Services.AddSingleton<IClientRegistrationService, ClientRegistrationService>();
builder.Services.AddSingleton<IPkceService, PkceService>();

#endregion

#region HTTP Client Configuration
/**************************************************************/
/// <summary>
/// Configures HttpClient for MedRecPro API calls with token forwarding.
/// </summary>
/// <remarks>
/// The TokenForwardingHandler automatically extracts the current user's
/// bearer token from the MCP session context and attaches it to all
/// outgoing requests to the MedRecPro API.
/// </remarks>
/// <seealso cref="TokenForwardingHandler"/>
/**************************************************************/
builder.Services.AddTransient<TokenForwardingHandler>();

builder.Services.AddHttpClient("MedRecProApi", client =>
{
    client.BaseAddress = new Uri(medrecProApiSettings.BaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "MedRecProMCP/1.0 (Internal-API-Client)");
    client.Timeout = TimeSpan.FromSeconds(medrecProApiSettings.TimeoutSeconds);
})
.AddHttpMessageHandler<TokenForwardingHandler>();

// Typed HttpClient for tool classes
builder.Services.AddHttpClient<MedRecProApiClient>(client =>
{
    client.BaseAddress = new Uri(medrecProApiSettings.BaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "MedRecProMCP/1.0 (Internal-API-Client)");
    client.Timeout = TimeSpan.FromSeconds(medrecProApiSettings.TimeoutSeconds);
})
.AddHttpMessageHandler<TokenForwardingHandler>();

/**************************************************************/
/// <summary>
/// Configures a direct HttpClient for MedRecPro API calls without token forwarding.
/// </summary>
/// <remarks>
/// Used by <see cref="MedRecProMCP.Services.UserResolutionService"/> during the OAuth
/// callback to call the resolve-mcp endpoint with a manually attached temporary MCP JWT.
/// This client does NOT use <see cref="TokenForwardingHandler"/> since the caller
/// manages its own Authorization header.
/// </remarks>
/// <seealso cref="MedRecProMCP.Services.IUserResolutionService"/>
/**************************************************************/
builder.Services.AddHttpClient("MedRecProApiDirect", client =>
{
    client.BaseAddress = new Uri(medrecProApiSettings.BaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "MedRecProMCP/1.0 (Internal-API-Client)");
    client.Timeout = TimeSpan.FromSeconds(medrecProApiSettings.TimeoutSeconds);
});

// Register StringCipher for user ID decryption and user resolution service
builder.Services.AddSingleton<MedRecProMCP.Helpers.StringCipher>();
builder.Services.AddSingleton<IUserResolutionService, UserResolutionService>();
#endregion

#region Authentication Configuration
/**************************************************************/
/// <summary>
/// Configures dual authentication schemes: JWT Bearer for token validation
/// and MCP authentication for Protected Resource Metadata.
/// </summary>
/// <remarks>
/// The MCP server acts as both:
/// - An OAuth Authorization Server (from Claude's perspective)
/// - An OAuth Resource Server (validating MCP tokens)
///
/// JWT tokens issued by this server contain:
/// - User identity claims from the upstream IdP (Google/Microsoft)
/// - The upstream IdP token for forwarding to MedRecPro API
/// - Standard JWT claims (iss, aud, exp, iat, jti)
/// </remarks>
/// <seealso cref="McpAuthenticationDefaults"/>
/**************************************************************/
// Use McpServer:JwtSigningKey for MCP token signing (this key is expected by the API)
// Falls back to Jwt:Key if McpServer key is not configured
var signingKeyValue = !string.IsNullOrEmpty(mcpSettings.JwtSigningKey)
    ? mcpSettings.JwtSigningKey
    : jwtSettings.Key;
var jwtSigningKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(signingKeyValue));

builder.Services.AddAuthentication(options =>
{
    // MCP auth handles the 401 challenge with resource_metadata
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    // JWT Bearer validates incoming tokens
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    // Disable Microsoft's default claim type mapping so JWT claims retain
    // their original short names (sub, name, email) rather than being
    // re-mapped to long .NET URI types.
    options.MapInboundClaims = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        // Use Jwt:Issuer and Jwt:Audience from secrets.json, fall back to ServerUrl
        ValidIssuer = null,
        ValidIssuers = new[]
        {
            jwtSettings.Issuer,                     // existing user tokens ("MedRecPro")
            mcpSettings.ServerUrl.TrimEnd('/')       // MCP tokens ("https://www.medrecpro.com/mcp")
        }.Where(i => !string.IsNullOrEmpty(i)).ToArray(),
        ValidAudience = null,
        ValidAudiences = new[]
        {
            jwtSettings.Audience,                   // existing user tokens ("MedRecUsers")
            mcpSettings.ServerUrl.TrimEnd('/')       // MCP tokens ("https://www.medrecpro.com/mcp")
        }.Where(a => !string.IsNullOrEmpty(a)).ToArray(),
        IssuerSigningKey = jwtSigningKey,
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = "name",
        RoleClaimType = "roles"
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("JwtBearerAuth");

            logger.LogWarning(
                "[Auth] JWT validation failed: {Error}",
                context.Exception.Message);

            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("JwtBearerAuth");

            var userName = context.Principal?.Identity?.Name ?? "unknown";
            logger.LogInformation(
                "[Auth] Token validated for user: {User}",
                userName);

            return Task.CompletedTask;
        }
    };
})
.AddMcp(options =>
{
    /**************************************************************/
    /// <summary>
    /// Configures the Protected Resource Metadata (RFC 9728).
    /// </summary>
    /// <remarks>
    /// This metadata document is served at /.well-known/oauth-protected-resource
    /// and tells MCP clients (like Claude) where to find the authorization
    /// server and what scopes are available.
    ///
    /// ResourceMetadataUri MUST be set explicitly for IIS virtual app hosting.
    /// Without it, the SDK generates the WWW-Authenticate resource_metadata URL
    /// from the incoming request (scheme + host + pathBase + path), which behind
    /// IIS out-of-process produces http://host/.well-known/oauth-protected-resource
    /// (wrong scheme, missing /mcp path prefix). Setting it to the full external
    /// URL ensures the 401 challenge header points to the correct PRM endpoint.
    /// </remarks>
    /**************************************************************/
    options.ResourceMetadataUri = new Uri($"{mcpSettings.ServerUrl}/.well-known/oauth-protected-resource");
    options.ResourceMetadata = new()
    {
        Resource = new Uri(mcpSettings.ServerUrl),
        AuthorizationServers = { new Uri(mcpSettings.ServerUrl) },
        ScopesSupported = mcpSettings.ScopesSupported?.ToList() ?? new List<string>
        {
            "mcp:tools",
            "mcp:read",
            "mcp:write",
            "openid",
            "profile",
            "email"
        },
        BearerMethodsSupported = new List<string> { "header" },
        ResourceDocumentation = new Uri($"{mcpSettings.ServerUrl}/docs")
    };
});

builder.Services.AddAuthorization(options =>
{
    // Default policy requires authenticated user
    options.AddPolicy("McpAccess", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
    });
});
#endregion

#region CORS Configuration
/**************************************************************/
/// <summary>
/// Configures CORS for cross-origin requests from Claude and other clients.
/// </summary>
/**************************************************************/
builder.Services.AddCors(options =>
{
    options.AddPolicy("McpCors", policy =>
    {
        policy.WithOrigins(
            "https://claude.ai",
            "https://claude.com",
            "https://www.medrecpro.com",
            "https://medrecpro.com"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });

    // Development policy
    options.AddPolicy("Development", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
#endregion

#region MCP Server Configuration
/**************************************************************/
/// <summary>
/// Configures the MCP server with HTTP transport in stateless mode.
/// </summary>
/// <remarks>
/// Stateless mode is required for Azure App Service compatibility
/// as it doesn't require session affinity (sticky sessions).
///
/// Tools are loaded from the assembly containing the MCP tool classes.
/// Authorization filters enable [Authorize] and [AllowAnonymous] attributes.
/// </remarks>
/**************************************************************/
builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithToolsFromAssembly()
    .AddAuthorizationFilters();
#endregion

#region Logging Configuration
/**************************************************************/
/// <summary>
/// Configures logging for the application.
/// </summary>
/**************************************************************/
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (builder.Environment.IsProduction())
{
    // Add Application Insights in production
    var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];
    if (!string.IsNullOrEmpty(appInsightsConnectionString))
    {
        builder.Services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = appInsightsConnectionString;
        });
    }
}
#endregion

var app = builder.Build();

#region Middleware Pipeline
/**************************************************************/
/// <summary>
/// Configures the HTTP request pipeline.
/// </summary>
/**************************************************************/
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Request/Response logging — captures every inbound request and outbound status/headers
// before auth runs. Critical for debugging MCP OAuth flow (what does Claude actually send?).
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("HttpTraffic");

    var method = context.Request.Method;
    var path = context.Request.Path + context.Request.QueryString;
    var hasAuth = context.Request.Headers.ContainsKey("Authorization");
    var authHeader = hasAuth
        ? context.Request.Headers["Authorization"].ToString()[..Math.Min(40, context.Request.Headers["Authorization"].ToString().Length)] + "..."
        : "(none)";
    var accept = context.Request.Headers["Accept"].ToString();
    var contentType = context.Request.ContentType ?? "(none)";

    logger.LogInformation(
        "[Traffic] >>> {Method} {Path} | Auth: {Auth} | Accept: {Accept} | Content-Type: {ContentType}",
        method, path, authHeader, accept, contentType);

    await next();

    var statusCode = context.Response.StatusCode;
    var wwwAuth = context.Response.Headers.ContainsKey("WWW-Authenticate")
        ? context.Response.Headers["WWW-Authenticate"].ToString()
        : "(none)";
    var responseContentType = context.Response.ContentType ?? "(none)";

    logger.LogInformation(
        "[Traffic] <<< {StatusCode} | WWW-Authenticate: {WwwAuth} | Content-Type: {ResponseContentType}",
        statusCode, wwwAuth, responseContentType);
});

// CORS must come before routing
if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}
else
{
    app.UseCors("McpCors");
}

app.UseRouting();

// Authentication & Authorization must come after UseRouting
app.UseAuthentication();
app.UseAuthorization();
#endregion

#region Endpoint Mappings
/**************************************************************/
/// <summary>
/// Maps all application endpoints.
/// </summary>
/// <remarks>
/// Route paths change based on build configuration to handle IIS virtual
/// application path stripping, following the same pattern as ApiControllerBase.
///
/// DEBUG (local development):
///   App runs standalone at root. Routes include /mcp prefix so the
///   full URL is http://localhost:5233/mcp, /mcp/oauth/*, etc.
///
/// RELEASE (Azure App Service):
///   App runs as IIS virtual application at /mcp. IIS strips the /mcp
///   prefix before forwarding to Kestrel, so routes are relative to root:
///   / (MCP transport), /oauth/*, /.well-known/*, etc.
///   External URLs remain https://www.medrecpro.com/mcp, /mcp/oauth/*, etc.
/// </remarks>
/// <seealso cref="MedRecPro.Controllers.ApiControllerBase"/>
/**************************************************************/

#if DEBUG
// DEBUG: Health check at root; MCP transport at /mcp; OAuth at /mcp/oauth/*
// All paths include the /mcp prefix since the app runs standalone
app.MapGet("/", () => Results.Ok(new
{
    name = "MedRecPro MCP Server",
    version = configuration.GetValue<string>("Version") ?? "1.0.0",
    status = "running",
    mcp = "/mcp",
    documentation = $"{mcpSettings.ServerUrl}/docs",
    gettingStarted = $"{mcpSettings.ServerUrl}/getting-started"
}));
#else
// RELEASE: Health check at /health; MCP transport at root /
// IIS virtual application at /mcp strips the prefix, so root = /mcp externally
app.MapGet("/health", () => Results.Ok(new
{
    name = "MedRecPro MCP Server",
    version = configuration.GetValue<string>("Version") ?? "1.0.0",
    status = "running",
    mcp = "/",
    documentation = $"{mcpSettings.ServerUrl}/docs",
    gettingStarted = $"{mcpSettings.ServerUrl}/getting-started"
}));
#endif

// Error handling endpoint
app.MapGet("/error", () => Results.Problem(
    statusCode: 500,
    title: "An error occurred"));

/**************************************************************/
/// <summary>
/// Serves static HTML documentation for the MCP server.
/// </summary>
/// <remarks>
/// Loads the HTML template from an embedded resource and replaces
/// placeholders with configuration values. Referenced in Protected Resource Metadata.
///
/// DEBUG: /mcp/docs (standalone, full path)
/// RELEASE: /docs (IIS virtual app adds /mcp prefix externally)
/// </remarks>
/// <seealso cref="MedRecProMCP.Templates.McpDocumentation.html"/>
/**************************************************************/
#if DEBUG
app.MapGet("/mcp/docs", async (HttpContext context) =>
#else
app.MapGet("/docs", async (HttpContext context) =>
#endif
{
    #region implementation
    // Load the HTML template from embedded resource
    var assembly = typeof(Program).Assembly;
    var resourceName = "MedRecProMCP.Templates.McpDocumentation.html";

    await using var stream = assembly.GetManifestResourceStream(resourceName);
    if (stream == null)
    {
        return Results.Problem(
            statusCode: 500,
            title: "Documentation template not found");
    }

    using var reader = new StreamReader(stream);
    var template = await reader.ReadToEndAsync();

    // Replace placeholders with configuration values
    var serverUrl = mcpSettings.ServerUrl;
    var serverName = mcpSettings.ServerName ?? "MedRecPro MCP Server";
    var version = configuration.GetValue<string>("Version") ?? "1.0.0";

    var html = template
        .Replace("{{ServerUrl}}", serverUrl)
        .Replace("{{ServerName}}", serverName)
        .Replace("{{Version}}", version);

    return Results.Content(html, "text/html");
    #endregion
});

/**************************************************************/
/// <summary>
/// Serves the user-facing getting-started page for the MCP directory listing.
/// </summary>
/// <remarks>
/// Loads the HTML template from an embedded resource and replaces
/// placeholders with configuration values. This page targets end-users
/// discovering MedRecPro through the Claude connectors directory.
///
/// DEBUG: /mcp/getting-started (standalone, full path)
/// RELEASE: /getting-started (IIS virtual app adds /mcp prefix externally)
/// </remarks>
/// <seealso cref="MedRecProMCP.Templates.McpGettingStarted.html"/>
/**************************************************************/
#if DEBUG
app.MapGet("/mcp/getting-started", async (HttpContext context) =>
#else
app.MapGet("/getting-started", async (HttpContext context) =>
#endif
{
    #region implementation
    var assembly = typeof(Program).Assembly;
    var resourceName = "MedRecProMCP.Templates.McpGettingStarted.html";

    await using var stream = assembly.GetManifestResourceStream(resourceName);
    if (stream == null)
    {
        return Results.Problem(
            statusCode: 500,
            title: "Getting started template not found");
    }

    using var reader = new StreamReader(stream);
    var template = await reader.ReadToEndAsync();

    var serverUrl = mcpSettings.ServerUrl;
    var serverName = mcpSettings.ServerName ?? "MedRecPro MCP Server";
    var version = configuration.GetValue<string>("Version") ?? "1.0.0";

    var html = template
        .Replace("{{ServerUrl}}", serverUrl)
        .Replace("{{ServerName}}", serverName)
        .Replace("{{Version}}", version);

    return Results.Content(html, "text/html");
    #endregion
});

/**************************************************************/
/// <summary>
/// Serves embedded images for the documentation and getting-started pages.
/// </summary>
/// <remarks>
/// Returns images from embedded resources at Templates/Images/.
/// Supports PNG and JPG formats with appropriate content types.
///
/// DEBUG: /mcp/docs/images/{filename}
/// RELEASE: /docs/images/{filename}
/// </remarks>
/**************************************************************/
#if DEBUG
app.MapGet("/mcp/docs/images/{filename}", async (string filename) =>
#else
app.MapGet("/docs/images/{filename}", async (string filename) =>
#endif
{
    #region implementation
    // Sanitize filename to prevent path traversal
    if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
    {
        return Results.BadRequest("Invalid filename");
    }

    var assembly = typeof(Program).Assembly;
    var resourceName = $"MedRecProMCP.Templates.Images.{filename}";

    var stream = assembly.GetManifestResourceStream(resourceName);
    if (stream == null)
    {
        return Results.NotFound();
    }

    // Determine content type from file extension
    var contentType = filename.ToLowerInvariant() switch
    {
        var f when f.EndsWith(".png") => "image/png",
        var f when f.EndsWith(".jpg") || f.EndsWith(".jpeg") => "image/jpeg",
        var f when f.EndsWith(".gif") => "image/gif",
        var f when f.EndsWith(".svg") => "image/svg+xml",
        _ => "application/octet-stream"
    };

    return Results.Stream(stream, contentType);
    #endregion
});

// OAuth 2.1 Authorization Server Metadata (RFC 8414)
app.MapOAuthMetadataEndpoints();

// OAuth endpoints (authorize, token, register, callbacks)
app.MapOAuthEndpoints();

// Protected Resource Metadata (PRM) is automatically served by McpAuthenticationHandler
// at /.well-known/oauth-protected-resource (RFC 9728).
//
// In RELEASE, IIS virtual app at /mcp strips the prefix, so the SDK's auto-mapped
// path /.well-known/oauth-protected-resource is externally /mcp/.well-known/... — correct.
//
// In DEBUG, the SDK serves PRM at /.well-known/oauth-protected-resource but clients
// discover it via WWW-Authenticate header which references ServerUrl (includes /mcp).
// We add a redirect so /mcp/.well-known/oauth-protected-resource → the SDK's path.
#if DEBUG
app.MapGet("/mcp/.well-known/oauth-protected-resource",
    () => Results.Redirect("/.well-known/oauth-protected-resource", permanent: false))
    .WithTags("OAuth Metadata")
    .WithSummary("DEBUG redirect to SDK-served Protected Resource Metadata")
    .ExcludeFromDescription();
#endif

#if DEBUG
// DEBUG: MCP transport at /mcp (standalone app, full path required)
// AllowAnonymous lets tools run without OAuth during local development
app.MapMcp("/mcp").AllowAnonymous();
#else
// RELEASE: MCP transport at root / (IIS virtual app at /mcp strips the prefix)
// RequireAuthorization ensures ASP.NET Core returns HTTP 401 + WWW-Authenticate
// on ALL unauthenticated requests (including initialize). The McpAuthenticationHandler
// (challenge scheme) adds: WWW-Authenticate: Bearer resource_metadata="<PRM url>"
// which triggers Claude's OAuth discovery flow per the MCP spec.
app.MapMcp("/").RequireAuthorization();
#endif
#endregion

app.Run();
