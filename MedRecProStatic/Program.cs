using MedRecPro.Static.Services;

var builder = WebApplication.CreateBuilder(args);

#region services configuration

// Add MVC services
builder.Services.AddControllersWithViews();

// Register content service as singleton (loads JSON once at startup)
builder.Services.AddSingleton<ContentService>();

#endregion

var app = builder.Build();

#region middleware pipeline

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ─────────────────────────────────────────────────────────────────────────────
// OAuth / MCP Discovery Endpoints
// ─────────────────────────────────────────────────────────────────────────────
// MCP clients (e.g. Claude) probe well-known discovery endpoints at the host
// root per RFC 9728 and RFC 8414, but the MCP server runs as an IIS virtual
// application at /mcp.
//
// We serve the metadata JSON directly from the static app rather than
// redirecting or proxying, because:
//   1. 302 redirects lose the path suffix, causing the MCP SDK to derive the
//      wrong resource URI (https://www.medrecpro.com instead of .../mcp).
//   2. Server-to-server proxying is blocked by Cloudflare bot challenge (403).
//   3. The metadata is static configuration — it doesn't change at runtime.
//
// The ServerUrl for the MCP server is https://www.medrecpro.com/mcp.
// ─────────────────────────────────────────────────────────────────────────────

const string mcpServerUrl = "https://www.medrecpro.com/mcp";

// Protected Resource Metadata (RFC 9728)
// Served at both the standard and path-appended forms.
var protectedResourceJson = System.Text.Json.JsonSerializer.Serialize(new
{
    resource = mcpServerUrl,
    authorization_servers = new[] { mcpServerUrl },
    scopes_supported = new[]
    {
        "mcp:tools",
        "mcp:read",
        "mcp:write",
        "openid",
        "profile",
        "email"
    },
    bearer_methods_supported = new[] { "header" },
    resource_documentation = $"{mcpServerUrl}/docs"
});

// Authorization Server Metadata (RFC 8414)
// Served at both the standard and path-appended forms.
var authServerJson = System.Text.Json.JsonSerializer.Serialize(new
{
    issuer = mcpServerUrl,
    authorization_endpoint = $"{mcpServerUrl}/oauth/authorize",
    token_endpoint = $"{mcpServerUrl}/oauth/token",
    registration_endpoint = $"{mcpServerUrl}/oauth/register",
    jwks_uri = $"{mcpServerUrl}/.well-known/jwks.json",
    scopes_supported = new[]
    {
        "openid",
        "profile",
        "email",
        "mcp:tools",
        "mcp:read",
        "mcp:write"
    },
    response_types_supported = new[] { "code" },
    response_modes_supported = new[] { "query" },
    grant_types_supported = new[] { "authorization_code", "refresh_token" },
    token_endpoint_auth_methods_supported = new[] { "client_secret_post", "client_secret_basic", "none" },
    code_challenge_methods_supported = new[] { "S256" },
    service_documentation = $"{mcpServerUrl}/docs",
    ui_locales_supported = new[] { "en" },
    client_id_metadata_document_supported = true,
    subject_types_supported = new[] { "public" },
    id_token_signing_alg_values_supported = new[] { "RS256", "HS256" }
});

// Standard form (RFC 9728 / RFC 8414):
app.MapGet("/.well-known/oauth-protected-resource",
    () => Results.Content(protectedResourceJson, "application/json"));
app.MapGet("/.well-known/oauth-authorization-server",
    () => Results.Content(authServerJson, "application/json"));
app.MapGet("/.well-known/openid-configuration",
    () => Results.Content(authServerJson, "application/json"));

// Path-appended form (RFC 8615 section 3.1 — /.well-known/{type}/{resource-path}):
app.MapGet("/.well-known/oauth-protected-resource/mcp",
    () => Results.Content(protectedResourceJson, "application/json"));
app.MapGet("/.well-known/oauth-authorization-server/mcp",
    () => Results.Content(authServerJson, "application/json"));
app.MapGet("/.well-known/openid-configuration/mcp",
    () => Results.Content(authServerJson, "application/json"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

#endregion

app.Run();
