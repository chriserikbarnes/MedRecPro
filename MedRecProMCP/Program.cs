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
/// Key Endpoints:
/// - /mcp - MCP Streamable HTTP transport (auto-mapped by MapMcp())
/// - /.well-known/oauth-protected-resource - PRM document (RFC 9728)
/// - /.well-known/oauth-authorization-server - AS metadata (RFC 8414)
/// - /oauth/authorize - Authorization endpoint (redirects to Google/Microsoft)
/// - /oauth/token - Token endpoint (exchanges codes for MCP access tokens)
/// - /oauth/register - Dynamic Client Registration (RFC 7591)
/// - /oauth/callback/google - Callback from Google
/// - /oauth/callback/microsoft - Callback from Microsoft
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
    client.Timeout = TimeSpan.FromSeconds(medrecProApiSettings.TimeoutSeconds);
})
.AddHttpMessageHandler<TokenForwardingHandler>();

// Typed HttpClient for tool classes
builder.Services.AddHttpClient<MedRecProApiClient>(client =>
{
    client.BaseAddress = new Uri(medrecProApiSettings.BaseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(medrecProApiSettings.TimeoutSeconds);
})
.AddHttpMessageHandler<TokenForwardingHandler>();
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
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        // Use Jwt:Issuer and Jwt:Audience from secrets.json, fall back to ServerUrl
        ValidIssuer = !string.IsNullOrEmpty(jwtSettings.Issuer)
            ? jwtSettings.Issuer
            : mcpSettings.ServerUrl.TrimEnd('/'),
        ValidAudience = !string.IsNullOrEmpty(jwtSettings.Audience)
            ? jwtSettings.Audience
            : mcpSettings.ServerUrl.TrimEnd('/'),
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
    /// </remarks>
    /**************************************************************/
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
/**************************************************************/

// Root endpoint for health checks
app.MapGet("/", () => Results.Ok(new
{
    name = "MedRecPro MCP Server",
    version = configuration.GetValue<string>("Version") ?? "1.0.0",
    status = "running",
    mcp = "/mcp",
    documentation = $"{mcpSettings.ServerUrl}/docs"
}));

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
/// </remarks>
/// <seealso cref="MedRecProMCP.Templates.McpDocumentation.html"/>
/**************************************************************/
app.MapGet("/docs", async (HttpContext context) =>
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
    var version = configuration.GetValue<string>("Version") ?? "0.0.1-alpha";

    var html = template
        .Replace("{{ServerUrl}}", serverUrl)
        .Replace("{{ServerName}}", serverName)
        .Replace("{{Version}}", version);

    return Results.Content(html, "text/html");
    #endregion
});

// OAuth 2.1 Authorization Server Metadata (RFC 8414)
app.MapOAuthMetadataEndpoints();

// OAuth endpoints (authorize, token, register, callbacks)
app.MapOAuthEndpoints();

// Protected Resource Metadata is automatically served by McpAuthenticationHandler
// at /.well-known/oauth-protected-resource

// MCP endpoint - anonymous access allowed; API handles auth per-endpoint
// Route pattern "/mcp" maps the MCP Streamable HTTP transport to /mcp
app.MapMcp("/mcp").AllowAnonymous();
#endregion

app.Run();
