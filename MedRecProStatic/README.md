# MedRecPro.Static

Static marketing, documentation, and AI chat site for MedRecPro - Pharmaceutical Labeling Management System. Also serves OAuth/MCP discovery metadata for the MedRecPro MCP Server.

**Version:** 1.0.0
**Runtime:** ASP.NET Core 8.0
**Production URL:** https://www.medrecpro.com

## Architecture

```
                        Cloudflare (CDN/WAF/DNS)
                                |
                                v
    Azure App Service: "MedRecPro" (Windows, IIS)
   +--------------------------------------------------------+
   |                                                        |
   |  /          site\wwwroot       MedRecProStatic (this)  |
   |  /api       site\wwwroot\api   MedRecPro API           |
   |  /mcp       site\wwwroot\mcp   MedRecProMCP            |
   |                                                        |
   +--------------------------------------------------------+
                        |
                        v
               Azure Key Vault
              (medrecprovault)
```

MedRecProStatic runs at the **root path** (`/`) as the parent IIS application. The API (`/api`) and MCP server (`/mcp`) run as IIS virtual applications beneath it.

### Request Routing

| Path | Handled By | Description |
|---|---|---|
| `/` | MedRecProStatic | Home page, marketing content |
| `/Home/Chat` | MedRecProStatic | AI chat interface |
| `/Home/Terms` | MedRecProStatic | Terms of Service |
| `/Home/Privacy` | MedRecProStatic | Privacy Policy |
| `/.well-known/*` | MedRecProStatic | OAuth/MCP discovery metadata |
| `/api/*` | MedRecPro API | REST API (IIS virtual application) |
| `/mcp` | MedRecProMCP | MCP Streamable HTTP transport (IIS virtual application) |

## OAuth/MCP Discovery Endpoints

MedRecProStatic serves the OAuth and MCP metadata discovery endpoints at the **domain root**, not under `/mcp`. This is a deliberate architectural choice required by the MCP SDK.

### Why discovery lives here (not in MedRecProMCP)

The MCP SDK resolves `/.well-known/*` relative to the domain root. When Claude connects to `https://www.medrecpro.com/mcp`, the SDK looks for:
- `https://www.medrecpro.com/.well-known/oauth-protected-resource`
- `https://www.medrecpro.com/.well-known/oauth-authorization-server`

These must return JSON directly (not redirects). Attempts to redirect from the root site to `/mcp/.well-known/*` failed because:
1. **302 redirects lose the path suffix**, causing the MCP SDK to derive the wrong resource URI
2. **Server-to-server reverse proxying** through Cloudflare triggers Bot Fight Mode (403 errors)

The solution is to serve the metadata as static JSON responses directly from `Program.cs`.

### Discovery endpoints registered in Program.cs

```
GET /.well-known/oauth-protected-resource          -> JSON (RFC 9728)
GET /.well-known/oauth-authorization-server        -> JSON (RFC 8414)
GET /.well-known/openid-configuration              -> JSON
GET /.well-known/oauth-protected-resource/mcp      -> JSON (path-appended form, RFC 8615 s3.1)
GET /.well-known/oauth-authorization-server/mcp    -> JSON (path-appended form)
GET /.well-known/openid-configuration/mcp          -> JSON (path-appended form)
```

All endpoints return the same metadata pointing to the MCP server at `https://www.medrecpro.com/mcp` with OAuth endpoints under `/mcp/oauth/*`.

## IIS Configuration (web.config)

The `web.config` serves two critical purposes:

### 1. HTTP Error PassThrough

```xml
<httpErrors existingResponse="PassThrough" />
```

This is placed **outside** the `<location>` element so it is inherited by child virtual applications (`/api` and `/mcp`). Without this, IIS intercepts HTTP 401 responses and replaces them with HTML error pages, breaking the MCP OAuth challenge flow where the `WWW-Authenticate` header must reach the client.

### 2. Child Application Isolation

```xml
<location path="." inheritInChildApplications="false">
```

Prevents the ASP.NET Core handler configuration from being inherited by `/api` and `/mcp`, which have their own `web.config` files and run as separate processes.

## Project Structure

```
MedRecProStatic/
  Program.cs                          # Startup, middleware, OAuth discovery endpoints
  MedRecProStatic.csproj              # Project file (.NET 8.0, OutOfProcess hosting)
  web.config                          # IIS config (httpErrors PassThrough, handler isolation)
  appsettings.json                    # Base configuration
  appsettings.Development.json        # Local dev overrides
  Controllers/
    HomeController.cs                 # 4 actions: Index, Terms, Privacy, Chat
  Models/
    PageContent.cs                    # Strongly-typed content models (1100+ lines)
  Services/
    ContentService.cs                 # Singleton JSON content loader
  Views/
    Home/
      Index.cshtml                    # Home/landing page
      Terms.cshtml                    # Terms of Service
      Privacy.cshtml                  # Privacy Policy
      Chat.cshtml                     # AI chat interface
    Shared/
      _Layout.cshtml                  # Master layout template
  Content/
    config.json                       # Site config (URLs, branding, version)
    pages.json                        # Page content (home, terms, privacy)
  wwwroot/
    css/                              # Stylesheets
    js/
      site.js                         # Global site scripts
      chat/                           # AI chat interface modules (see below)
    lib/                              # Third-party libraries (Bootstrap, jQuery)
  Properties/
    launchSettings.json               # Development launch profiles
    PublishProfiles/                   # Azure deployment profiles
```

### Chat Interface JavaScript Modules

The AI chat interface at `/Home/Chat` uses a modular JavaScript architecture:

```
wwwroot/js/chat/
  index.js                 # Main orchestrator - coordinates all modules
  api-service.js           # HTTP communication with MedRecPro API
  endpoint-executor.js     # Executes API endpoints, handles responses
  batch-synthesizer.js     # Synthesizes API responses into readable text
  checkpoint-manager.js    # State management and checkpoints
  checkpoint-renderer.js   # Renders thinking process and checkpoints
  message-renderer.js      # Renders chat messages to DOM
  markdown.js              # Markdown to HTML rendering
  config.js                # Chat configuration loader
  state.js                 # Client-side state management
  progressive-config.js    # Progressive feature loading
  result-grouper.js        # Groups and organizes results
  settings-renderer.js     # UI settings and preferences
  file-handler.js          # ZIP file upload and processing
  ui-helpers.js            # DOM manipulation utilities
  utils.js                 # General utility functions
  util-test.js             # Utility tests
```

**Key API endpoints called by chat JS:**
- `GET /api/Ai/context` — System context and authentication status
- `POST /api/Ai/interpret` — NLP query to API endpoint mapping
- `POST /api/Ai/synthesize` — API results to human-readable responses
- `GET /api/Ai/chat` — Convenience endpoint for simple queries

## Content Management

All page content is managed via JSON files in the `Content/` directory. The `ContentService` singleton loads them once at startup.

### config.json

Site-wide configuration with environment-aware URLs:

| Key | Description |
|---|---|
| `siteName` | Site display name |
| `version` | Current version string |
| `apiUrl` / `apiUrlDev` | Production / development API URLs |
| `authUrl` / `authUrlDev` | Production / development auth URLs |
| `returnUrl` / `returnUrlDev` | Post-authentication redirect URLs |
| `swaggerUrl` / `swaggerUrlDev` | API documentation URLs |
| `contactEmail` | Support email address |

### pages.json

Page content for home, terms, and privacy pages. Supports:
- Hero sections with CTAs
- Feature cards with color variants
- Statistics display
- How-it-works step-by-step sections
- Use case cards
- Legal sections with subsections and itemized lists
- Subprocessor listings (GDPR compliance)

## Local Development

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code

### Running Locally

```bash
cd MedRecPro.Static
dotnet restore
dotnet run
```

The site runs at `http://localhost:5001`. The chat interface communicates with the API at `http://localhost:5093/api` (configured in `config.json`).

## Deployment

### Azure App Service

Deployed to the root path (`/`) of the shared Azure App Service:

| Virtual Path | Physical Path | Type |
|---|---|---|
| `/` | `site\wwwroot` | Site (this project) |
| `/api` | `site\wwwroot\api` | Application (MedRecPro API) |
| `/mcp` | `site\wwwroot\mcp` | Application (MedRecProMCP) |

### Publish from CLI

```bash
dotnet publish -c Release -o ./publish
```

Deploy to Azure via Web Deploy, zip deploy, or the Visual Studio publish profile.

### Cloudflare

The site runs behind Cloudflare for CDN, WAF, and DNS. No special Cloudflare configuration is needed for the static site itself. See the MedRecProMCP README for MCP-specific Cloudflare requirements.

## Related Projects

- **[MedRecPro](../MedRecPro)** — Main API with SPL processing, authentication, and AI endpoints
- **[MedRecProMCP](../MedRecProMCP)** — MCP server for Claude.ai integration (OAuth 2.1 gateway)

---

**Last Updated:** February 2026
**Status:** production
