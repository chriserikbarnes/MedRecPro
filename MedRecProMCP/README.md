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
9. MCP server issues authorization code, redirects to Claude
10. Claude exchanges code for tokens via /mcp/oauth/token
11. Claude sends authenticated POST to /mcp with Bearer token
12. MCP server forwards token to MedRecPro API for each tool call
```

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
  web.config                          # IIS configuration (OutOfProcess hosting)
  appsettings.json                    # Base configuration
  appsettings.Development.json        # Local dev overrides
  appsettings.Production.json         # Azure production overrides
  Configuration/
    McpServerSettings.cs              # MCP server config model
    MedRecProApiSettings.cs           # API client config model
    JwtSettings.cs                    # JWT config model
    AuthenticationSettings.cs         # OAuth provider config model
  Endpoints/
    OAuthEndpoints.cs                 # OAuth authorize, token, register, callbacks
    OAuthMetadataEndpoints.cs         # .well-known metadata endpoints
  Services/
    McpTokenService.cs                # JWT token generation and validation
    OAuthService.cs                   # OAuth flow orchestration
    ClientRegistrationService.cs      # Dynamic Client Registration (RFC 7591)
    MedRecProApiClient.cs             # Typed HTTP client for MedRecPro API
    PkceService.cs                    # PKCE implementation
  Handlers/
    TokenForwardingHandler.cs         # HTTP handler for token delegation
  Models/
    AiAgentDtos.cs                    # AI/Claude integration models
    WorkPlanModels.cs                 # Work plan and execution models
  Tools/                              # MCP tool implementations
  Templates/
    McpDocumentation.html             # Embedded HTML documentation template
  Properties/
    launchSettings.json               # Local development profiles
    PublishProfiles/
      MedRecProMCP - Web Deploy.pubxml  # Azure deployment profile
  Docs/
    DeploymentGuide.md                # Legacy deployment guide (subdomain approach)
```

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

### 405 Method Not Allowed on /mcp

This is **expected behavior**. The MCP Streamable HTTP transport at `/mcp` only accepts POST requests. Browsers send GET, which returns 405.

### OAuth callback errors

1. Verify redirect URIs are registered in Google Cloud Console and Microsoft Entra ID
2. Check that `McpServer:ServerUrl` includes `/mcp` (e.g., `https://www.medrecpro.com/mcp`)
3. Callback URLs are built as `{ServerUrl}/oauth/callback/{provider}`

### SSE connection drops

1. Verify Cloudflare transform rule sets `X-Accel-Buffering: no` for `/mcp` paths
2. Verify Cloudflare cache rule bypasses cache for `/mcp` paths
3. Check Azure App Service always-on is enabled

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
