# MedRecPro MCP Server

MCP (Model Context Protocol) server for the MedRecPro application. Acts as an OAuth 2.1 gateway between MCP clients (Claude, etc.) and the MedRecPro Web API.

**Version:** 0.0.1-alpha
**Runtime:** ASP.NET Core 8.0
**Production URL:** https://www.medrecpro.com/mcp

## Architecture

```
                             Azure App Service: "MedRecPro" (Windows, IIS)
                            +--------------------------------------------------+
                            |                                                  |
+------------------+        |  /          site\wwwroot       MedRecProStatic   |
|  Claude / MCP    |  HTTPS |  /api       site\wwwroot\api   MedRecPro API     |
|  Client          |------->|  /mcp       site\wwwroot\mcp   MedRecProMCP      |
+------------------+        |                                                  |
        |                   +--------------------------------------------------+
        v                                    |
+------------------+                         v
|  Cloudflare      |                +------------------+
|  (CDN/WAF/DNS)   |                |  Azure Key Vault |
+------------------+                |  (medrecprovault)|
                                    +------------------+
```

The MCP server runs as an **IIS virtual application** at `/mcp` on the same Azure App Service as the main MedRecPro UI and API. IIS handles path-based routing to three separate ASP.NET Core processes:

| Virtual Path | Physical Path | Project | Purpose |
|---|---|---|---|
| `/` | `site\wwwroot` | MedRecProStatic | UI / Static content |
| `/api` | `site\wwwroot\api` | MedRecPro | Web API |
| `/mcp` | `site\wwwroot\mcp` | MedRecProMCP | MCP Server (this project) |

## Endpoints

All endpoints are externally prefixed with `/mcp` by the IIS virtual application.

| External URL | Method | Description |
|---|---|---|
| `/mcp` | POST | MCP Streamable HTTP transport (JSON-RPC) |
| `/mcp/health` | GET | Health check (returns JSON status) |
| `/mcp/docs` | GET | HTML documentation page |
| `/mcp/.well-known/oauth-protected-resource` | GET | Protected Resource Metadata (RFC 9728) |
| `/mcp/.well-known/oauth-authorization-server` | GET | Authorization Server Metadata (RFC 8414) |
| `/mcp/oauth/authorize` | GET | OAuth authorization endpoint |
| `/mcp/oauth/token` | POST | Token exchange endpoint |
| `/mcp/oauth/register` | POST | Dynamic Client Registration (RFC 7591) |
| `/mcp/oauth/callback/google` | GET | Google OAuth callback |
| `/mcp/oauth/callback/microsoft` | GET | Microsoft OAuth callback |

> **Note:** `GET /mcp` returns HTTP 405 (Method Not Allowed). This is correct behavior. The MCP transport only accepts POST requests.

## Authentication Flow

```
1. Claude sends POST to /mcp (unauthenticated)
2. Server returns 401 with WWW-Authenticate header pointing to PRM
3. Claude fetches /mcp/.well-known/oauth-protected-resource
4. Claude fetches /mcp/.well-known/oauth-authorization-server
5. Claude registers via /mcp/oauth/register (Dynamic Client Registration)
6. Claude redirects user to /mcp/oauth/authorize?provider=google
7. User authenticates with Google/Microsoft
8. Provider redirects to /mcp/oauth/callback/{provider}
9. MCP server resolves upstream IdP email to numeric DB user ID via POST /api/users/resolve-mcp
   (auto-provisions user if they don't exist in the database)
10. MCP server issues authorization code with numeric `sub` claim (standard JWT name), redirects to Claude
11. Claude exchanges code for tokens via /mcp/oauth/token
12. Claude sends authenticated POST to /mcp with Bearer MCP JWT
13. MCP server forwards MCP JWT to MedRecPro API for each tool call (McpBearer auth scheme)
```

## User Resolution and Auto-Provisioning

During the OAuth callback (step 9), the MCP server must map the upstream identity provider's identifier (Google sub / Microsoft GUID) to a numeric MedRecPro database user ID. The API's `getEncryptedIdFromClaim()` method expects a numeric `NameIdentifier` claim — without this resolution, all API calls would fail with 401.

**How it works:**

1. `OAuthEndpoints.cs` extracts email, display name, and provider from upstream IdP claims
2. `UserResolutionService` generates a temporary MCP JWT and calls `POST /api/users/resolve-mcp`
3. The API's `UsersController.ResolveMcpUser()` looks up the user by email:
   - **User exists:** Returns their encrypted database user ID
   - **User doesn't exist:** Auto-provisions a new user record (matching `AuthController.createUserForExternalLogin()` defaults: EmailConfirmed=true, Timezone="UTC", Locale="en-US", no password), then returns the encrypted ID
4. `UserResolutionService` decrypts the encrypted ID using the shared `Security:DB:PKSecret` via `StringCipher`
5. `OAuthEndpoints.cs` replaces the upstream `NameIdentifier` claim with the numeric database user ID
6. `McpTokenService.normalizeClaimType()` converts `ClaimTypes.NameIdentifier` to the standard JWT `sub` claim during token creation, and `OutboundClaimTypeMap` is cleared to prevent double-mapping
7. The MCP JWT is issued with the numeric user ID as the `sub` claim, enabling all API calls to succeed

**Key architecture:**
- `TokenForwardingHandler` forwards the MCP JWT directly to the API (not the upstream Google/Microsoft token). The API's `McpBearer` authentication scheme validates these MCP-signed JWTs.
- Both the MCP server and API share `Security:DB:PKSecret` from Azure Key Vault for `StringCipher` encryption/decryption of user IDs.
- `UserResolutionService` uses a dedicated `"MedRecProApiDirect"` named HttpClient (without `TokenForwardingHandler`) to avoid circular token forwarding during resolution.
- **Claim normalization:** `McpTokenService` normalizes .NET `ClaimTypes` URIs to standard JWT short names (`sub`, `name`, `email`, `given_name`, `family_name`) before token creation. Both JWT handlers set `MapInboundClaims = false` so claims retain their short names when read back. The API's `ClaimHelper` resolves user IDs from both `ClaimTypes.NameIdentifier` (cookie auth) and `"sub"` (MCP JWT auth).

## Compiler Directives (#if DEBUG)

Route paths use compiler directives to handle IIS virtual application path stripping. This follows the same pattern as `ApiControllerBase` in the MedRecPro API project.

**Problem:** IIS strips the `/mcp` prefix from requests before forwarding to the app. A request to `www.medrecpro.com/mcp/oauth/authorize` arrives at Kestrel as `/oauth/authorize`. Routes must account for this.

| Component | DEBUG (local dev) | RELEASE (Azure/IIS) |
|---|---|---|
| MCP transport | `MapMcp("/mcp")` | `MapMcp("/")` |
| Health check | `GET /` | `GET /health` |
| Documentation | `GET /mcp/docs` | `GET /docs` |
| OAuth group | `/mcp/oauth` | `/oauth` |
| Well-known metadata | `/mcp/.well-known/*` | `/.well-known/*` |
| PRM redirect | `/mcp/.well-known/oauth-protected-resource` redirects to SDK path | Not needed (IIS handles) |

**Files with compiler directives:**
- `Program.cs` - MCP transport, health check, docs, PRM redirect
- `Endpoints/OAuthEndpoints.cs` - OAuth route group prefix
- `Endpoints/OAuthMetadataEndpoints.cs` - Well-known metadata paths

**Config-driven (no directives needed):**
- OAuth callback URLs - Built from `McpServer:ServerUrl` at runtime
- OAuth metadata document contents - Built from `McpServer:ServerUrl` at runtime
- Protected Resource Metadata - Built from `McpServer:ServerUrl` at runtime

---

## Setup Guide

### Prerequisites

- .NET 8.0 SDK
- Azure subscription with:
  - App Service (Windows, IIS) - shared with MedRecPro
  - Key Vault (`medrecprovault`)
  - Application Insights (optional)
- Cloudflare account with `medrecpro.com` domain
- Google Cloud Console project with OAuth 2.0 credentials
- Microsoft Entra ID app registration

### 1. Local Development Setup

#### Clone and restore

```bash
git clone <repo-url>
cd MedRecProMCP
dotnet restore
```

#### Configure user secrets

```bash
dotnet user-secrets init
dotnet user-secrets set "McpServer:JwtSigningKey" "<min-32-char-key>"
dotnet user-secrets set "Jwt:Key" "<min-32-char-key>"
dotnet user-secrets set "Jwt:Issuer" "http://localhost:5233/mcp"
dotnet user-secrets set "Jwt:Audience" "http://localhost:5233/mcp"
dotnet user-secrets set "Jwt:ExpirationMinutes" "60"
dotnet user-secrets set "Jwt:UpstreamTokenEncryptionKey" "<32-char-aes-key>"
dotnet user-secrets set "Authentication:Google:ClientId" "<google-client-id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<google-client-secret>"
dotnet user-secrets set "Authentication:Microsoft:ClientId" "<microsoft-app-id>"
dotnet user-secrets set "Authentication:Microsoft:ClientSecret:Dev" "<microsoft-client-secret>"
dotnet user-secrets set "Authentication:Microsoft:TenantId" "<azure-tenant-id>"
dotnet user-secrets set "Security:DB:PKSecret" "<encryption-key-must-match-api>"
```

#### Run locally

```bash
dotnet run
```

The server runs at `http://localhost:5233`. In DEBUG mode, endpoints include the `/mcp` prefix:
- Health check: http://localhost:5233/
- MCP transport: http://localhost:5233/mcp (POST only)
- OAuth metadata: http://localhost:5233/mcp/.well-known/oauth-authorization-server

### 2. Azure App Service Setup

#### Add virtual application

In Azure Portal:

1. Navigate to **App Service "MedRecPro"** > **Configuration** > **Path mappings**
2. Click **"+ Add virtual application or directory"**
3. Configure:
   - **Virtual path:** `/mcp`
   - **Physical path:** `site\wwwroot\mcp`
   - **Type:** Check **Application**
4. Click **Save**

#### Key Vault secrets

Ensure the following secrets exist in Azure Key Vault (`medrecprovault`). These are shared with the MedRecPro API since both apps use the same Key Vault:

| Secret Name | Description |
|---|---|
| `McpServer--JwtSigningKey` | JWT signing key for MCP tokens (min 32 chars) |
| `Jwt--Key` | JWT signing key |
| `Jwt--Issuer` | JWT issuer claim |
| `Jwt--Audience` | JWT audience claim |
| `Jwt--ExpirationMinutes` | Token expiration (e.g., `60`) |
| `Jwt--UpstreamTokenEncryptionKey` | AES encryption key for upstream tokens |
| `Authentication--Google--ClientId` | Google OAuth Client ID |
| `Authentication--Google--ClientSecret` | Google OAuth Client Secret |
| `Authentication--Microsoft--ClientId` | Microsoft Entra App ID |
| `Authentication--Microsoft--ClientSecret--Prod` | Microsoft client secret (production) |
| `Authentication--Microsoft--TenantId` | Azure AD Tenant ID |
| `Security--DB--PKSecret` | Shared encryption key for user ID encryption/decryption (must match API) |

> **Note:** Key Vault uses `--` as separator. ASP.NET Core maps these to `:` in configuration (e.g., `Jwt--Key` becomes `Jwt:Key`).

#### Managed identity

The App Service must have a system-assigned managed identity with `Get` and `List` permissions on Key Vault secrets. Since the MCP virtual application shares the same App Service as the API, the existing managed identity should already have access.

### 3. OAuth Provider Configuration

#### Google Cloud Console

1. Go to [Google Cloud Console](https://console.cloud.google.com/) > **APIs & Services** > **Credentials**
2. Edit your OAuth 2.0 Client ID
3. Add authorized redirect URIs:
   - `https://www.medrecpro.com/mcp/oauth/callback/google` (production)
   - `http://localhost:5233/mcp/oauth/callback/google` (development)

#### Microsoft Entra ID

1. Go to [Azure Portal](https://portal.azure.com/) > **Microsoft Entra ID** > **App registrations**
2. Select your app > **Authentication**
3. Add redirect URIs:
   - `https://www.medrecpro.com/mcp/oauth/callback/microsoft` (production)
   - `http://localhost:5233/mcp/oauth/callback/microsoft` (development)

### 4. Cloudflare Configuration

Since the MCP server runs on the same domain (`www.medrecpro.com`), no DNS changes are needed. Configure the following rules:

#### Cache Rule — Bypass cache for /mcp

**Caching > Cache Rules > Create rule**

- **Rule name:** `Bypass cache for MCP server`
- **When:** Hostname equals `www.medrecpro.com` AND URI Path starts with `/mcp`
- **Then:** Cache eligibility: **Bypass cache**

#### Transform Rule — Disable response buffering for SSE

**Rules > Transform Rules > Modify Response Header > Create rule**

- **Rule name:** `Disable buffering for MCP SSE`
- **When:** Hostname equals `www.medrecpro.com` AND URI Path starts with `/mcp`
- **Then:** Set static header `X-Accel-Buffering` = `no`

This prevents Cloudflare from buffering Server-Sent Events used by MCP Streamable HTTP transport.

#### WAF Rules (optional)

Only needed if you have existing WAF rules that might block POST requests to `/mcp` or `/mcp/oauth/*`. If your WAF has no custom rules, this step can be skipped.

### 5. Deployment

#### Publish from Visual Studio

1. Right-click **MedRecProMCP** in Solution Explorer > **Publish**
2. Select the **"MedRecProMCP - Web Deploy"** profile
3. Verify settings:
   - Configuration: **Release**
   - Target Framework: **net8.0**
   - Deployment Mode: **Framework-dependent**
4. Click **Publish**

The publish profile deploys to `site\wwwroot\mcp` via the `<DeployIisAppPath>MedRecPro/mcp</DeployIisAppPath>` setting.

#### Publish from CLI

```bash
dotnet publish -c Release -o ./publish
```

Then deploy via Azure CLI or zip deploy to the `/mcp` virtual application path.

### 6. Verification

After deployment, verify each endpoint:

```bash
# Health check — should return JSON with status "running"
curl https://www.medrecpro.com/mcp/health

# MCP transport — should return 405 (GET not allowed, POST only)
curl -i https://www.medrecpro.com/mcp

# OAuth Authorization Server Metadata — should return JSON
curl https://www.medrecpro.com/mcp/.well-known/oauth-authorization-server

# Protected Resource Metadata — should return JSON
curl https://www.medrecpro.com/mcp/.well-known/oauth-protected-resource

# Documentation page — should return HTML
curl https://www.medrecpro.com/mcp/docs

# Main site still works
curl https://www.medrecpro.com
curl https://www.medrecpro.com/api
```

---

## Configuration Reference

### appsettings.json (base)

Non-secret configuration shared across all environments.

| Key | Description | Default |
|---|---|---|
| `McpServer:ServerUrl` | Base URL of the MCP server (includes `/mcp`) | `https://www.medrecpro.com/mcp` |
| `McpServer:ServerName` | Display name | `MedRecPro MCP Server` |
| `McpServer:ScopesSupported` | OAuth scopes | `openid, profile, email, mcp:tools, mcp:read, mcp:write` |
| `McpServer:EnableDynamicClientRegistration` | Allow RFC 7591 registration | `true` |
| `McpServer:MaxRegisteredClients` | Max dynamic clients | `1000` |
| `McpServer:ClientRegistrationExpirationHours` | Client TTL | `24` |
| `MedRecProApi:BaseUrl` | MedRecPro API URL | `https://www.medrecpro.com/api` |
| `MedRecProApi:TimeoutSeconds` | HTTP client timeout | `30` |
| `MedRecProApi:RetryCount` | HTTP retry attempts | `3` |
| `Security:DB:PKSecret` | Shared encryption key for StringCipher (must match API) | _(Key Vault)_ |
| `KeyVaultUrl` | Azure Key Vault URL | _(empty in base, set in Production)_ |

### appsettings.Development.json

| Key | Override Value |
|---|---|
| `McpServer:ServerUrl` | `http://localhost:5233/mcp` |
| `MedRecProApi:BaseUrl` | `http://localhost:5093/api` |
| `MedRecProApi:ValidateSslCertificate` | `false` |

### appsettings.Production.json

| Key | Override Value |
|---|---|
| `McpServer:ServerUrl` | `https://www.medrecpro.com/mcp` |
| `MedRecProApi:BaseUrl` | `https://www.medrecpro.com/api` |
| `KeyVaultUrl` | `https://medrecprovault.vault.azure.net/` |

---

## Project Structure

```
MedRecProMCP/
  Program.cs                          # App startup, DI, middleware, endpoint mappings
  MedRecProMCP.csproj                 # Project file (.NET 8.0)
  MedRecProMCP.slnx                   # Solution file
  MedRecProMCP.http                   # HTTP request test file
  web.config                          # IIS configuration (OutOfProcess hosting)
  server.json                         # MCP registry metadata (see MCP Registry below)
  appsettings.json                    # Base configuration
  appsettings.Development.json        # Local dev overrides
  appsettings.Production.json         # Azure production overrides
  Configuration/
    McpServerSettings.cs              # MCP server config model
    MedRecProApiSettings.cs           # API client config model
    JwtSettings.cs                    # JWT config model
    OAuthProviderSettings.cs          # OAuth provider config model (Google, Microsoft)
  Endpoints/
    OAuthEndpoints.cs                 # OAuth authorize, token, register, callbacks
    OAuthMetadataEndpoints.cs         # .well-known metadata endpoints
  Services/
    IMcpTokenService.cs               # Token service interface
    McpTokenService.cs                # JWT token generation and validation
    IOAuthService.cs                  # OAuth service interface
    OAuthService.cs                   # OAuth flow orchestration
    IClientRegistrationService.cs     # Client registration interface
    ClientRegistrationService.cs      # Dynamic Client Registration (RFC 7591)
    IPkceService.cs                   # PKCE service interface
    PkceService.cs                    # PKCE implementation
    IPersistedCacheService.cs         # Persisted cache interface
    FilePersistedCacheService.cs      # File-based persistent cache (PKCE, registrations)
    MedRecProApiClient.cs             # Typed HTTP client for MedRecPro API
    IUserResolutionService.cs         # User resolution service interface
    UserResolutionService.cs          # Resolves upstream IdP email to numeric DB user ID
  Handlers/
    TokenForwardingHandler.cs         # Forwards MCP JWT to API (DelegatingHandler)
  Helpers/
    StringCipher.cs                   # AES encryption/decryption (copy from API for user ID handling)
  Models/
    AiAgentDtos.cs                    # AI/Claude integration models
    WorkPlanModels.cs                 # Work plan and execution models
  Tools/
    DrugLabelTools.cs                 # MCP tools for drug label search and export
    UserTools.cs                      # MCP tools for user/account operations
  Templates/
    McpDocumentation.html             # Embedded HTML documentation template
  PowerShell/
    test-mcp.ps1                      # MCP endpoint test script
    test-mcp-detailed.ps1             # Detailed MCP test with verbose output
    test-mcp-root.ps1                 # Root endpoint test script
  Properties/
    launchSettings.json               # Local development profiles
    PublishProfiles/
      MedRecProMCP - Web Deploy.pubxml  # Azure deployment profile
  Docs/
    DeploymentGuide.md                # Legacy deployment guide (subdomain approach)
```

## Claude.ai Connector Integration

This section documents the process of connecting the MCP server as a custom connector in Claude.ai's Settings > Connectors. This was a multi-session effort with numerous obstacles specific to the IIS virtual application + Cloudflare + Azure architecture.

### Connection Status

| Component | Status |
|---|---|
| OAuth 2.1 flow (PKCE S256) | Working |
| MCP session establishment (`initialize`) | Working |
| Tool listing (`tools/list`) | Working |
| Tool execution (`tools/call`) | Working |
| Downstream API calls (MCP -> MedRecPro API) | Working |
| MCP user resolution (email -> numeric DB ID) | Working |
| MCP user auto-provisioning (new users)       | Working |
| JWT claim normalization (sub, name, email)   | Working |
| UsersController [Authorize(Policy = "ApiAccess")] | Working |

### Issues Encountered and Fixes

The following issues were encountered in order during integration. Each had to be resolved before the next became visible.

#### 1. PKCE Validation Failure

**Symptom:** OAuth token exchange failed with PKCE mismatch.
**Root Cause:** The PKCE verifier/challenge pair was not being correctly stored and matched across the authorization flow.
**Fix:** Corrected the PKCE storage and validation logic in `PkceService.cs`.

#### 2. Discovery Endpoints Returning 302 Redirects

**Symptom:** Claude's MCP SDK received 302 redirects instead of JSON for `/.well-known/oauth-protected-resource` and `/.well-known/oauth-authorization-server`.
**Root Cause:** IIS path-based routing and the virtual application at `/mcp` caused discovery URL resolution issues. The MCP SDK expects `/.well-known/*` at the domain root, but the MCP server only handles requests under `/mcp`.
**Fix:** Added the well-known endpoints directly to `MedRecProStatic/Program.cs` (the root site), serving the OAuth metadata as static JSON responses. This avoids redirects entirely.

#### 3. Missing `[Authorize]` on MCP Transport

**Symptom:** Claude could connect without authentication — no 401 challenge was sent.
**Root Cause:** The `MapMcp()` endpoint lacked `.RequireAuthorization()`.
**Fix:** Added `.RequireAuthorization()` to the `MapMcp("/")` call in `Program.cs` (RELEASE configuration).

#### 4. `[AllowAnonymous]` on MCP Transport

**Symptom:** Even with `.RequireAuthorization()`, the MCP transport was not triggering 401.
**Root Cause:** The MCP SDK's default transport middleware was internally applying `[AllowAnonymous]`.
**Fix:** Used the `McpAuthenticationHandler` challenge scheme to ensure the `WWW-Authenticate` header with `resource_metadata` URI is returned on unauthenticated requests.

#### 5. IIS Replacing 401 with HTML Error Pages

**Symptom:** Claude received HTML error pages instead of the JSON 401 response with `WWW-Authenticate` header.
**Root Cause:** IIS intercepts HTTP error responses and replaces them with its own HTML pages.
**Fix:** Added `<httpErrors existingResponse="PassThrough" />` to `web.config` in **both** `MedRecProStatic` (root site) and `MedRecProMCP`. The root site's setting is critical because it is inherited by child virtual applications.

#### 6. Wrong `resource_metadata` URL in 401 Response

**Symptom:** Claude could not find the Protected Resource Metadata (PRM) endpoint.
**Root Cause:** The `resource_metadata` URI in the `WWW-Authenticate` header was pointing to the wrong path.
**Fix:** Explicitly set `ResourceMetadataUri` in the `McpAuthenticationHandler` configuration to `{ServerUrl}/.well-known/oauth-protected-resource`.

#### 7. PRM Endpoint 404

**Symptom:** The PRM endpoint returned 404 after the URL was corrected.
**Root Cause:** Route registration order and IIS path stripping resulted in the endpoint not being mapped.
**Fix:** Added explicit PRM endpoint registration in `OAuthMetadataEndpoints.cs` with correct routing.

#### 8. Cloudflare "Manage AI Bots" WAF Rule Blocking Claude

**Symptom:** Claude's requests never reached the server (Cloudflare returned 403).
**Root Cause:** Cloudflare's managed WAF rule "Block AI Bots" was blocking all requests from Claude-User (Anthropic's crawler user agent). The AI Crawl Control dashboard showed 149 blocked requests and 0 allowed.
**Fix (Cloudflare Dashboard):**
1. **Security > Settings > Block AI Bots Scope** — Changed to "Block only on hostnames with ads"
2. **Security > AI Crawl Control > Crawlers** — Set `Claude-User` (Anthropic) to **Allow**

#### 9. JWT Audience Validation Failed (IDX10214)

**Symptom:** `IDX10214: Audience validation failed. Audiences: 'https://www.medrecpro.com/mcp'. Did not match: validationParameters.ValidAudience: 'MedRecUsers'`
**Root Cause:** `McpTokenService` issues tokens with audience set to `ServerUrl` (`https://www.medrecpro.com/mcp`), but the JWT Bearer handler only accepted `MedRecUsers` (from `Jwt:Audience` config).
**Fix:** Changed `ValidAudience` (single) to `ValidAudiences` (array) accepting both `jwtSettings.Audience` and `mcpSettings.ServerUrl.TrimEnd('/')`.

#### 10. JWT Issuer Validation Failed (IDX10205)

**Symptom:** `IDX10205: Issuer validation failed. Issuer: 'https://www.medrecpro.com/mcp'. Did not match: validationParameters.ValidIssuer: 'MedRecPro'`
**Root Cause:** Same dual-token issue as audience — `McpTokenService` uses `ServerUrl` as issuer, but JWT handler only accepted `MedRecPro`.
**Fix:** Changed `ValidIssuer` (single) to `ValidIssuers` (array) accepting both `jwtSettings.Issuer` and `mcpSettings.ServerUrl.TrimEnd('/')`.

#### 11. Downstream API Calls Blocked by Cloudflare Bot Fight Mode

**Symptom:** MCP tools executed but returned 403. The MCP server's HTTP calls to `https://www.medrecpro.com/api/...` were blocked.
**Root Cause:** The MCP server (running on Azure) makes outbound HTTP requests to the public API URL. These requests go through Cloudflare, which flagged them via Bot Fight Mode because they came from Azure IP addresses with an empty `User-Agent` header.
**Fix (two-part):**
1. **Code** — Added `User-Agent: MedRecProMCP/1.0 (Internal-API-Client)` header to both HttpClient registrations in `Program.cs`
2. **Cloudflare** — Bot Fight Mode **cannot** be skipped via WAF custom rules, but it will not trigger if an **IP Access Rule** matches the request. Added IP Access Rules (action: Allow) for all 32 Azure App Service outbound IPs:
   ```bash
   # Get all possible outbound IPs for the App Service
   az webapp show --resource-group <rg> --name <app> \
     --query possibleOutboundIpAddresses --output tsv

   # Add each IP as a Cloudflare IP Access Rule (Allow)
   # Use Cloudflare API with Global API Key:
   for ip in $IPS; do
     curl -s -X POST \
       "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/firewall/access_rules/rules" \
       -H "X-Auth-Email: $EMAIL" \
       -H "X-Auth-Key: $API_KEY" \
       -H "Content-Type: application/json" \
       --data "{\"mode\":\"whitelist\",\"configuration\":{\"target\":\"ip\",\"value\":\"$ip\"},\"notes\":\"Azure App Service outbound IP\"}"
   done
   ```

#### 12. Azure SQL Database Cold Start Timeouts

**Symptom:** First tool calls after idle periods fail with `Database 'MedRecProDB' is not currently available` or `Execution Timeout Expired` (30-second command timeout).
**Root Cause:** Azure SQL Serverless auto-pauses after inactivity. Resuming takes 15-30+ seconds, exceeding both the HttpClient timeout (30s) and SQL command timeout (30s).
**Status:** Known issue. Subsequent requests succeed once the database warms up. Mitigation options include increasing the auto-pause delay, enabling `EnableRetryOnFailure` on `UseSqlServer`, or increasing the `TimeoutSeconds` in `MedRecProApi` settings.

#### 13. JWT Claim Type Mapping Mismatch (401 on Tool Calls)

**Symptom:** After OAuth login succeeds and MCP JWT is generated, all tool calls that hit the API return 401. Logs show `[Auth] Token validated for user: unknown` and `getEncryptedIdFromClaim()` throws `UnauthorizedAccessException: Unable to determine user ID from authentication context`.
**Root Cause:** `JwtSecurityTokenHandler.OutboundClaimTypeMap` silently converts `ClaimTypes.NameIdentifier` to `"sub"` and `ClaimTypes.Name` to `"unique_name"` during JWT serialization. On the API side, `getEncryptedIdFromClaim()` searched for `c.Type.Contains("NameIdentifier")` which does not match `"sub"`. Additionally, `NameClaimType = "name"` expected a `"name"` claim but the JWT contained `"unique_name"`, causing `Identity.Name` to be null.
**Fix (three-part):**
1. **McpTokenService** — Added `normalizeClaimType()` to explicitly map `ClaimTypes.*` URIs to standard JWT short names (`sub`, `name`, `email`, `given_name`, `family_name`) before token creation. Cleared `OutboundClaimTypeMap` to prevent double-mapping.
2. **Both JWT handlers** — Added `MapInboundClaims = false` on the MCP server and API `McpBearer` handlers so claims retain their original short names when read back into `ClaimsPrincipal`.
3. **API claim readers** — Created `ClaimHelper.cs` to resolve user IDs from both `ClaimTypes.NameIdentifier` (cookie auth) and `"sub"` (MCP JWT). Updated 9 scattered NameIdentifier lookups across `UsersController`, `AiController`, `AuthController`, `LabelController`, `RequireActorAttributeFilter`, `RequireUserRoleAttributeFilter`, `ActivityLogActionFilter`, and `LogHelper` to use the shared helper.

#### 14. Missing `[Authorize]` on UsersController (401 on MCP Tool Calls)

**Symptom:** After fixing JWT claim normalization (Issue #13), MCP-side logs now show `[Auth] Token validated for user: Christopher Barnes` correctly, but API tool calls still return 401. `ClaimHelper.GetEncryptedUserIdOrThrow` throws because `User.Claims` is empty. IIS logs show `Logon User: Anonymous`. No `[MCP Auth]` log from the API side.
**Root Cause:** `UsersController` had no `[Authorize]` attribute at the class level or on individual endpoints (except `resolve-mcp`). Without `[Authorize]`, ASP.NET Core's JWT bearer handler is never invoked for McpBearer tokens — the request arrives as anonymous and `User.Claims` is empty. Cookie auth worked without `[Authorize]` because Identity middleware runs globally and populates `User.Claims` from session cookies on every request, but JWT Bearer schemes require explicit `[Authorize]` to trigger validation.
**Fix:** Added `[Authorize(Policy = "ApiAccess")]` to the `UsersController` class. The `ApiAccess` policy (Program.cs) accepts both `IdentityConstants.ApplicationScheme` (cookies) and `"McpBearer"` (JWT), so this triggers the JWT handler for MCP requests while preserving existing cookie auth behavior. Added `[AllowAnonymous]` to `signup` and `authenticate` endpoints that must remain publicly accessible. Also fixed `OnAuthenticationFailed` handler that was silently suppressing `SecurityTokenException` log output.

#### 15. GetMyActivity / GetMyActivityByDateRange Failing Silently

**Symptom:** After fixing Issue #14, `GetMyProfile` returns 200 successfully, but `GetMyActivity` and `GetMyActivityByDateRange` complete quickly with no second HTTP request to the activity endpoint. The MCP tools return `{"error":"Could not determine user ID"}` to Claude.
**Root Cause (A — MCP side):** `UserTools.cs` calls `profile.TryGetProperty("encryptedId", ...)` but the API serializes with Newtonsoft.Json using `CamelCasePropertyNamesContractResolver` — the actual JSON property name is `"encryptedUserId"` (camelCase, and "User" was missing from the original lookup key). The case-sensitive `TryGetProperty` lookup fails and the second API call is never made.
**Root Cause (B — API side):** `UsersController.cs` GetUserActivity (line 452) and GetUserActivityByDateRange (line 630) used `if (!isSelf || !claimsUser.IsUserAdmin())` — OR instead of AND. This blocked non-admin users from viewing their own activity logs. Only users who were both the target AND an admin could pass the check.
**Fix:** Changed `"encryptedId"` → `"encryptedUserId"` in UserTools.cs (both occurrences) to match the camelCase JSON property name. Changed `||` → `&&` in both authorization checks in UsersController.cs so that self-access is allowed for all users, and admin access is allowed for any user's logs.

### Lessons Learned

1. **IIS virtual applications strip path prefixes.** A request to `/mcp/oauth/authorize` arrives at Kestrel as `/oauth/authorize`. All route mappings must account for this in RELEASE builds. Use `#if DEBUG` directives.

2. **OAuth discovery must be at the domain root.** The MCP SDK resolves `/.well-known/*` relative to the domain, not the MCP endpoint path. Serve these endpoints from the root site (`MedRecProStatic`), not the MCP virtual application.

3. **`httpErrors existingResponse="PassThrough"` is critical.** Without it, IIS replaces 401 responses (needed for OAuth challenge) with HTML error pages. This must be set in the **root site's** `web.config` because child virtual applications inherit the setting.

4. **Cloudflare has multiple independent bot-blocking systems:**
   - **"Block AI Bots"** (WAF managed rule) — Controls AI crawler access. Allow Claude-User via AI Crawl Control.
   - **"Bot Fight Mode"** — Blocks automated traffic from hosting provider IPs. Cannot be skipped with WAF rules. The only bypass is IP Access Rules.

5. **Server-to-server calls through Cloudflare require whitelisting.** When your server makes HTTP calls to your own public domain, those requests go through Cloudflare and will be flagged as bot traffic. Always set a `User-Agent` header on outbound HttpClients, and whitelist your server's outbound IPs in Cloudflare IP Access Rules.

6. **Azure App Service cannot make localhost connections.** The sandbox prevents outbound socket connections to `127.0.0.1:80`. Server-to-server calls within the same App Service must go through the public hostname.

7. **JWT tokens from different issuers need `ValidIssuers`/`ValidAudiences` arrays.** When the MCP server issues tokens with different issuer/audience values than the existing user auth system, the JWT Bearer handler must accept both. Use the plural array properties, not the singular ones.

8. **Azure SQL Serverless cold starts can cascade.** A paused database takes 15-30+ seconds to resume. If the MCP HttpClient timeout (30s) expires before the database wakes, the tool call fails. Consider `EnableRetryOnFailure` and generous timeouts for serverless databases.

9. **Cloudflare API authentication:** The Global API Key uses `X-Auth-Email` + `X-Auth-Key` headers, **not** `Authorization: Bearer`. API Tokens (scoped) use `Authorization: Bearer`. Using the wrong format returns `Unable to authenticate request`.

10. **`JwtSecurityTokenHandler` silently renames claim types.** The `OutboundClaimTypeMap` converts `ClaimTypes.NameIdentifier` to `"sub"` and `ClaimTypes.Name` to `"unique_name"` during JWT serialization. On the inbound side, `MapInboundClaims` (default `true`) may or may not reverse this depending on the handler implementation (`JsonWebTokenHandler` in .NET 8 does not). Always explicitly normalize claims to standard JWT short names before token creation, clear `OutboundClaimTypeMap`, and set `MapInboundClaims = false` on JWT handlers to ensure claim types are predictable end-to-end.

11. **JWT Bearer schemes require `[Authorize]` to trigger validation.** Unlike cookie/Identity middleware which runs globally on every request and populates `User.Claims` from session cookies, JWT Bearer handlers only validate tokens when an endpoint has `[Authorize]` (or a policy that references the scheme). Without it, the Bearer token in the `Authorization` header is ignored, the request arrives as anonymous, and `User.Claims` is empty. If a controller mixes public and protected endpoints, use class-level `[Authorize(Policy = "...")]` with `[AllowAnonymous]` on public endpoints.

12. **JSON property names must match the API's serialization casing.** The MedRecPro API uses Newtonsoft.Json with PascalCase (no camelCase conversion). When parsing API responses with `System.Text.Json` in the MCP project, `TryGetProperty()` is case-sensitive — `"encryptedId"` will not match `"EncryptedUserId"`. Always verify the exact property names returned by the API.

13. **De Morgan's law in authorization logic.** `if (!isSelf || !isAdmin)` is equivalent to `if (!(isSelf && isAdmin))` — this requires BOTH conditions to be true, not either. For "allow if self OR admin", use `if (!isSelf && !isAdmin)`.

## Troubleshooting

### 502 Bad Gateway

The MCP app is crashing on startup. Check:

1. **Kudu console** — Browse to `https://<app>.scm.azurewebsites.net`, navigate to `LogFiles/` and look for `stdout-mcp*` log files
2. **Enable stdout logging** — In `site\wwwroot\mcp\web.config`, set `stdoutLogEnabled="true"`, hit the URL again, check the log
3. **Common cause:** Key Vault not accessible. Verify `KeyVaultUrl` is set in `appsettings.Production.json` and managed identity has access

### IDX10703: key length is zero

The JWT signing key is empty. This means Key Vault secrets aren't loading:

1. Verify `KeyVaultUrl` is set to `https://medrecprovault.vault.azure.net/` in `appsettings.Production.json`
2. Verify the App Service managed identity has `Get` and `List` permissions on Key Vault secrets
3. Verify `ASPNETCORE_ENVIRONMENT` is set to `Production` in App Service environment variables

### IDX10214 / IDX10205: Audience or Issuer validation failed

JWT tokens issued by `McpTokenService` use `ServerUrl` as both issuer and audience. The JWT Bearer handler must accept both the MCP values and the existing user auth values:

```csharp
ValidIssuers = new[] { jwtSettings.Issuer, mcpSettings.ServerUrl.TrimEnd('/') }
ValidAudiences = new[] { jwtSettings.Audience, mcpSettings.ServerUrl.TrimEnd('/') }
```

### 405 Method Not Allowed on /mcp

This is **expected behavior**. The MCP Streamable HTTP transport at `/mcp` only accepts POST requests. Browsers send GET, which returns 405.

### 403 Forbidden on downstream API calls

The MCP server's outbound HTTP calls to `https://www.medrecpro.com/api/...` are being blocked by Cloudflare:

1. **Check Cloudflare Firewall Events** — Look for blocks with source `botFight` or `managedChallenge`
2. **Verify IP Access Rules** — All Azure App Service outbound IPs must be whitelisted in Cloudflare (Security > WAF > Tools > IP Access Rules, action: Allow)
3. **Verify User-Agent header** — The HttpClient must send a non-empty User-Agent. Check `Program.cs` for `client.DefaultRequestHeaders.Add("User-Agent", ...)`
4. **Get outbound IPs:** `az webapp show --resource-group <rg> --name <app> --query possibleOutboundIpAddresses --output tsv`

### OAuth callback errors

1. Verify redirect URIs are registered in Google Cloud Console and Microsoft Entra ID
2. Check that `McpServer:ServerUrl` includes `/mcp` (e.g., `https://www.medrecpro.com/mcp`)
3. Callback URLs are built as `{ServerUrl}/oauth/callback/{provider}`

### SSE connection drops

1. Verify Cloudflare transform rule sets `X-Accel-Buffering: no` for `/mcp` paths
2. Verify Cloudflare cache rule bypasses cache for `/mcp` paths
3. Check Azure App Service always-on is enabled

### Database timeout on first tool call

Azure SQL Serverless may be paused. The first request triggers a resume that takes 15-30+ seconds:

1. **Retry** — The second attempt usually succeeds after the database warms up
2. **Increase auto-pause delay** — In Azure Portal, set the serverless auto-pause delay to 10-15 minutes during testing
3. **Add retry logic** — Consider `EnableRetryOnFailure()` in `UseSqlServer()` configuration

---

## MCP Registry Publishing

The server is registered on the official [MCP Registry](https://registry.modelcontextprotocol.io/) for discoverability by MCP clients.

**Registry Entry:** `com.medrecpro/drug-label-server`
**Registry Search:** https://registry.modelcontextprotocol.io/v0.1/servers?search=medrecpro

### Prerequisites

1. **mcp-publisher CLI** — Install from [GitHub releases](https://github.com/modelcontextprotocol/registry/releases):
   ```powershell
   # Windows (PowerShell)
   $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -eq "Arm64") { "arm64" } else { "amd64" }
   Invoke-WebRequest -Uri "https://github.com/modelcontextprotocol/registry/releases/latest/download/mcp-publisher_windows_$arch.tar.gz" -OutFile "mcp-publisher.tar.gz"
   tar xf mcp-publisher.tar.gz mcp-publisher.exe
   rm mcp-publisher.tar.gz
   ```

2. **Ed25519 keypair** — Used for DNS domain verification (stored locally, NOT in repo)

3. **DNS TXT record** — Already configured in Cloudflare:
   ```
   medrecpro.com TXT "v=MCPv1; k=ed25519; p=<public-key>"
   ```

### server.json

The registry metadata file is at `MedRecProMCP/server.json`:

```json
{
  "$schema": "https://static.modelcontextprotocol.io/schemas/2025-12-11/server.schema.json",
  "name": "com.medrecpro/drug-label-server",
  "title": "MedRecPro Drug Label Server",
  "description": "Search and export FDA drug labels by brand name, generic ingredient, or UNII code.",
  "version": "0.0.1",
  "websiteUrl": "https://www.medrecpro.com",
  "repository": {
    "url": "https://github.com/chriserikbarnes/MedRecPro",
    "source": "github",
    "subfolder": "MedRecProMCP"
  },
  "remotes": [
    {
      "type": "streamable-http",
      "url": "https://www.medrecpro.com/mcp"
    }
  ]
}
```

Key points:
- **`name`** uses the `com.medrecpro/` namespace (verified via DNS)
- **`remotes`** declares this as a hosted server (not an npm/PyPI package)
- **`packages`** is omitted since the server isn't distributed as a package

### Publishing Process

1. **Authenticate with DNS verification:**
   ```powershell
   mcp-publisher.exe login dns --domain=medrecpro.com --private-key=<64-char-hex-private-key>
   ```

   > ⚠️ The private key is stored in a local script outside the repo. See `C:\Users\chris\Documents\MCP-Server-JSON-Publisher.ps1`

2. **Publish to the registry:**
   ```powershell
   mcp-publisher.exe publish server.json
   ```

3. **Verify the listing:**
   ```bash
   curl "https://registry.modelcontextprotocol.io/v0.1/servers?search=medrecpro"
   ```

### When to Publish Updates

Bump the `version` in `server.json` and re-publish when:
- Adding, removing, or renaming tools
- Changing the server URL
- Updating the description or metadata

The registry stores each version separately. Publishing does **not** deploy code — it only updates the catalog entry.

### Maintaining Backward Compatibility

Since the deployed server is a single instance (not versioned packages), avoid breaking changes:

| ✅ Safe Changes | ❌ Breaking Changes |
|---|---|
| Add new tools | Remove existing tools |
| Add optional parameters | Remove or rename parameters |
| Add new response fields | Change response structure |
| Fix bugs / improve logic | Change tool names |
| Improve descriptions | Remove required fields |

### DNS Verification Reference

If you need to re-generate the keypair or set up on a new machine:

```bash
# Generate Ed25519 keypair (use Git Bash on Windows)
openssl genpkey -algorithm Ed25519 -out mcp-key.pem

# Extract public key (Base64) — goes in DNS TXT record
openssl pkey -in mcp-key.pem -pubout -outform DER | tail -c 32 | base64

# Extract private key (64-char hex) — used with CLI
openssl pkey -in mcp-key.pem -noout -text 2>&1 | grep -A3 "priv:" | tail -n +2 | tr -d ' :\n'
```

DNS TXT record format (in Cloudflare):
- **Type:** TXT
- **Name:** `@` (root domain)
- **Content:** `v=MCPv1; k=ed25519; p=<base64-public-key>`
