# MedRecPro API Changes for MCP Authentication Support

This document describes the changes needed to the existing MedRecPro API (`Program.cs`) to accept tokens issued by the MCP server.

## Overview

The MedRecPro API needs to accept two types of tokens:
1. **Existing tokens** - From direct Google/Microsoft OAuth flows (current behavior)
2. **MCP tokens** - JWTs issued by the MCP server that encapsulate upstream IdP tokens

## Changes to Program.cs

### 1. Add Required NuGet Packages

```xml
<!-- Add to MedRecPro.csproj if not already present -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="8.0.*" />
```

### 2. Add MCP JWT Configuration Section

Add the following after the existing authentication configuration (around line 444):

```csharp
#region MCP Server JWT Authentication
/**************************************************************/
/// <summary>
/// Configures JWT Bearer authentication for tokens issued by the MCP Server.
/// </summary>
/// <remarks>
/// This allows the MedRecPro API to accept tokens from the MCP server,
/// enabling Claude and other MCP clients to access the API through
/// the MCP gateway while preserving user identity.
///
/// The MCP server issues JWTs that contain:
/// - Standard claims (iss, aud, exp, iat, jti)
/// - User identity claims from upstream IdP (email, name, sub)
/// - Encrypted upstream token for pass-through scenarios
/// </remarks>
/**************************************************************/

// Get MCP server settings from configuration
var mcpServerUrl = builder.Configuration["McpServer:Url"];
var mcpJwtSigningKey = builder.Configuration["McpServer:JwtSigningKey"];

if (!string.IsNullOrEmpty(mcpServerUrl) && !string.IsNullOrEmpty(mcpJwtSigningKey))
{
    builder.Services.AddAuthentication()
        .AddJwtBearer("McpBearer", options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = mcpServerUrl.TrimEnd('/'),
                ValidAudience = mcpServerUrl.TrimEnd('/'),
                IssuerSigningKey = new SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(mcpJwtSigningKey)),
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
                        .CreateLogger("McpJwtAuth");

                    // Only log if this scheme was actually attempted
                    if (context.Exception is not SecurityTokenException)
                    {
                        logger.LogDebug(
                            "[MCP Auth] Token validation skipped: {Error}",
                            context.Exception.Message);
                    }
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("McpJwtAuth");

                    var userName = context.Principal?.Identity?.Name ?? "unknown";
                    var provider = context.Principal?.FindFirst("provider")?.Value ?? "unknown";

                    logger.LogInformation(
                        "[MCP Auth] Token validated for user: {User} (provider: {Provider})",
                        userName, provider);

                    return Task.CompletedTask;
                }
            };
        });

    Console.WriteLine($"[Startup] MCP JWT authentication enabled for issuer: {mcpServerUrl}");
}
else
{
    Console.WriteLine("[Startup] MCP JWT authentication not configured (McpServer:Url or McpServer:JwtSigningKey missing)");
}
#endregion
```

### 3. Update Authorization Policies

Update the authorization configuration to include the MCP bearer scheme:

```csharp
// Update the existing AddAuthorization section
builder.Services.AddAuthorization(options =>
{
    // Existing BasicAuthPolicy
    options.AddPolicy("BasicAuthPolicy", new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("BasicAuthentication")
        .RequireAuthenticatedUser()
        .Build());

    // New: Policy that accepts both Identity cookies and MCP JWT tokens
    options.AddPolicy("ApiAccess", new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(
            IdentityConstants.ApplicationScheme,
            "McpBearer")
        .RequireAuthenticatedUser()
        .Build());

    // Make ApiAccess the default policy for API controllers
    options.DefaultPolicy = options.GetPolicy("ApiAccess")
        ?? new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
});
```

### 4. Add Configuration to appsettings.json

Add the following to both `appsettings.json` and `appsettings.Production.json`:

```json
{
  "McpServer": {
    "Url": "https://mcp.medrecpro.com",
    "JwtSigningKey": "{{STORED_IN_KEY_VAULT}}"
  }
}
```

### 5. Add Key Vault Reference (Production)

In Azure Key Vault, add a secret named `McpServer--JwtSigningKey` with the same value as the MCP server's `Jwt:SigningKey`.

### 6. CORS Configuration (Optional)

If the MCP server makes browser-initiated requests to the API (unlikely for server-to-server calls), update CORS:

```csharp
// Add MCP server to Production CORS policy
options.AddPolicy("Production", policy =>
{
    policy.WithOrigins(
        "https://www.medrecpro.com",
        "https://medrecpro.com",
        "https://mcp.medrecpro.com"  // Add this line
    )
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials();
});
```

## Important Notes

### Token Flow

1. User authenticates with Google/Microsoft via the MCP server
2. MCP server issues its own JWT containing user claims
3. MCP server stores the upstream token encrypted in the JWT
4. When Claude invokes an MCP tool, the MCP server extracts the upstream token
5. The MCP server forwards the upstream token to the MedRecPro API
6. MedRecPro API validates the token using the existing Google/Microsoft configuration

### Security Considerations

1. **Shared Signing Key**: The JWT signing key must be identical between the MCP server and MedRecPro API. Store it in Azure Key Vault and reference it from both applications.

2. **Token Validation**: The API validates:
   - Issuer matches the MCP server URL
   - Audience matches the MCP server URL
   - Token is not expired
   - Signature is valid

3. **No Upstream Token Passthrough in JWT**: For security, the MCP server should extract and decrypt the upstream token server-side, not pass it through the JWT to the API. The API trusts the MCP server's JWT directly.

### Alternative Approach: Upstream Token Forwarding

If you prefer the API to continue using Google/Microsoft tokens directly (for existing authorization rules that inspect IdP-specific claims), the MCP server can:

1. Extract the upstream IdP token from the MCP JWT
2. Forward that token directly to the API in the Authorization header
3. The API continues to validate against Google/Microsoft as it does today

This approach doesn't require any changes to the MedRecPro API, but requires the MCP server to handle token refresh when upstream tokens expire.

## Testing

1. **Local Development**: Use the same JWT signing key in both projects' `appsettings.Development.json`

2. **Verify Token Flow**:
   ```bash
   # Get a token from the MCP server
   curl -X POST https://mcp.medrecpro.com/oauth/token \
     -d "grant_type=authorization_code&code=XXX&..."

   # Use the token with the MedRecPro API
   curl https://www.medrecpro.com/api/users/me \
     -H "Authorization: Bearer <mcp_token>"
   ```

3. **Check Logs**: Both applications log authentication events for troubleshooting.
