# MedRecPro

MedRecPro is a pharmaceutical structured product label (SPL) management platform built with ASP.NET Core. It provides secure access to FDA drug label data through a RESTful API, an AI-powered chat interface, and a Model Context Protocol (MCP) server for integration with AI assistants like Claude.

## Specifications

- **HL7 Version**: HL7 Dec 2023 https://www.fda.gov/media/84201/download
- **Info**: https://www.fda.gov/industry/fda-data-standards-advisory-board/structured-product-labeling-resources
- **Data Source**: https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm

## Technology Stack

- **Runtime**: ASP.NET Core (.NET 8.0 LTS)
- **Database**: Azure SQL Server (Serverless free tier) with Dapper + Entity Framework Core
- **Authentication**: Cookie-based auth with Google and Microsoft OAuth providers; JWT bearer tokens for API access; McpBearer JWT scheme for MCP server integration (claims normalized to standard JWT short names: `sub`, `name`, `email`)
- **AI Integration**: Claude API for natural language query interpretation and synthesis
- **MCP Protocol**: Model Context Protocol server with OAuth 2.1 (PKCE S256) for Claude.ai connector integration
- **Hosting**: Azure App Service (Windows, IIS) with Cloudflare CDN/WAF/DNS
- **Secrets**: Azure Key Vault
- **API Documentation**: Swagger/OpenAPI
- **SPL Rendering**: RazorLight templates for SPL XML-to-HTML generation

## Solution Architecture

The solution consists of five projects deployed to a single Azure App Service using IIS virtual applications:

```
                        Cloudflare (CDN/WAF/DNS)
                                |
                                v
    Azure App Service: "MedRecPro" (Windows, IIS)
   +--------------------------------------------------------+
   |                                                        |
   |  /          site\wwwroot       MedRecProStatic         |
   |  /api       site\wwwroot\api   MedRecPro API           |
   |  /mcp       site\wwwroot\mcp   MedRecProMCP            |
   |                                                        |
   +--------------------------------------------------------+
                        |
                        v
               Azure Key Vault
              (medrecprovault)
```

| Virtual Path | Project | Purpose |
|---|---|---|
| `/` | **MedRecProStatic** | Static site, marketing pages, AI chat UI, OAuth/MCP discovery metadata |
| `/api` | **MedRecPro** | REST API: SPL parsing, label CRUD, authentication, AI interpret/synthesize |
| `/mcp` | **MedRecProMCP** | MCP server: OAuth 2.1 gateway for Claude.ai integration |
| _(CLI)_ | **MedRecProConsole** | Standalone bulk SPL import utility |
| _(test)_ | **MedRecProTest** | Unit and integration tests |

### How the Projects Relate

**MedRecProStatic** is the user-facing front end. Its AI chat interface (`/Home/Chat`) communicates with the API using a request-interpret-execute-synthesize pattern: user queries are sent to the API's AI endpoints, which use Claude to map natural language to API calls. The static site also serves OAuth/MCP discovery metadata (`/.well-known/*`) at the domain root on behalf of the MCP server, because the MCP SDK resolves discovery URLs relative to the domain root rather than the `/mcp` path.

**MedRecPro (API)** is the core backend. It handles SPL XML parsing and import, label data CRUD, user authentication, AI query interpretation via Claude, database views for navigation, and SPL document rendering via RazorLight templates.

**MedRecProMCP** is an OAuth 2.1 gateway that exposes MedRecPro API capabilities as MCP tools. When Claude.ai connects, it authenticates users through Google/Microsoft OAuth, resolves upstream identity provider identities to numeric database user IDs (auto-provisioning new users if needed), then forwards authenticated MCP JWTs to the MedRecPro API. It uses JWT tokens, PKCE (S256), Dynamic Client Registration (RFC 7591), and a shared PKSecret for encrypted user ID exchange with the API.

## Repository File Structure

```
MedRecPro/                          # Root repository
  README.md                         # This file
  .gitignore
  LICENSE.txt

  MedRecPro/                        # ASP.NET Core Web API
    Program.cs                      # App startup, DI, middleware
    MedRecPro.csproj                # .NET 8.0 project file
    appsettings.json                # Base configuration
    appsettings.Development.json    # Local dev overrides
    web.Release.config              # IIS release config
    Controllers/
      ApiControllerBase.cs          # Base controller (route prefix, #if DEBUG directives)
      AuthController.cs             # OAuth login/logout, user info
      UsersController.cs            # User CRUD, activity logs, authentication, MCP user resolution/provisioning
                                      #   [Authorize(Policy = "ApiAccess")] — accepts both cookie auth and McpBearer JWT
                                      #   signup and authenticate endpoints use [AllowAnonymous]
      LabelController.cs            # Label CRUD, views, search, import, AI endpoints
      AiController.cs               # AI interpret/synthesize, conversations, context
      SettingsController.cs         # App info, feature flags, metrics, logs, cache
    Service/
      SplImportService.cs           # SPL ZIP file import and parsing orchestration
      SplParsingService.cs          # Core SPL XML parsing
      SplDataService.cs             # Database operations for label data
      SplContextService.cs          # SPL document context management
      SplDocumentRenderingService.cs    # SPL-to-HTML rendering via RazorLight
      SplStructuredBodyRenderingService.cs
      SplSectionRenderingService.cs
      SplIngredientRenderingService.cs
      SplPackageRenderingService.cs
      SplCharacteristicRenderingService.cs
      SplAuthorRenderingService.cs
      SplTextContentRenderingService.cs
      SplRenderingRegistrationService.cs
      ViewRenderService.cs          # Razor view rendering
      ZipImportWorkerService.cs     # Background ZIP import worker
      BackgroudTaskService.cs       # Background task management
      DatabaseKeepAliveService.cs   # Keeps Azure SQL Serverless awake during business hours
      AzureTokenCredentialService.cs
      AzureAppHostTokenCredentialService.cs
      ParsingServices/              # 20+ specialized SPL XML parsers
        SectionParser.cs
        ProductIdentityParser.cs
        PackagingParser.cs
        ... (and more)
      ParsingValidators/            # SPL validation services
    DataAccess/
      RepositoryDataAccess.cs       # Core data access layer
      UserDataAccess.cs             # User-specific queries
      DtoLabelAccess.cs             # Label DTO queries (base)
      DtoLabelAccess-Views.cs       # Database view queries
      DtoLabelAccess-Document.cs    # Document queries
      DtoLabelAccess-Ingredient.cs  # Ingredient queries
      DtoLabelAccess-Organization.cs
      DtoLabelAccess-ProductHierarchy.cs
      DtoLabelAccess-ContentHierarchy.cs
      DtoLabelAccess-BatchLoaders.cs
      ... (and more)
    Models/                         # Domain models, DTOs, enums
      Labels.cs                     # Core label entities
      User.cs                       # User model
      Import.cs                     # Import models
      Comparison.cs                 # Label comparison models
      SectionStructure.cs           # Section hierarchy
      DocumentRendering.cs          # Rendering models
      ... (and more)
    Skills/                         # AI skill definitions (markdown prompts for Claude)
      skills.md                     # Master skill index
      selectors.md                  # Query routing rules
      retryPrompt.md                # Retry logic prompt
      labelProductIndication.md     # Indication discovery skill
      equianalgesicConversion.md    # Opioid conversion skill
      product-extraction.md         # Product extraction skill
      pharmacologic-class-matching.md
      interfaces/                   # Modular skill interface definitions
        response-format.md
        synthesis-rules.md
        api/                        # API-specific skill docs
          indication-discovery.md
          label-content.md
          equianalgesic-conversion.md
          pharmacologic-class.md
          product-extraction-api.md
          user-activity.md
          cache-management.md
          session-management.md
          data-rescue.md
          retry-fallback.md
      prompts/                      # AI prompt templates
        product-extraction-prompt.md
        pharmacologic-class-matching-prompt.md
    Views/
      SplTemplates/                 # RazorLight templates for SPL XML rendering
        GenerateSpl.cshtml          # Main SPL generation template
        _Section.cshtml             # Section partial
        _Product.cshtml             # Product partial
        _Ingredient.cshtml          # Ingredient partial
        _Packaging.cshtml           # Packaging partial
        _Author.cshtml              # Author partial
        ... (18 templates total)
      Stylesheets/                  # SPL rendering stylesheets
    Helpers/                        # Utility classes
      ClaimHelper.cs                # Centralized claim extraction (cookie auth + MCP JWT)
      EncryptionHelper.cs           # ID encryption/decryption
      ConnectionStringHelper.cs     # DB connection management
      XmlHelpers.cs                 # XML parsing utilities
      ... (and more)
    Auth/
      BasicAuthenticationHandler.cs # Basic auth handler
    Attributes/                     # Custom validation attributes for SPL fields
    Filters/
      ActivityLogActionFilter.cs    # Request activity logging
      RequireActorAttributeFilter.cs      # Actor-based authorization filter
      RequireUserRoleAttributeFilter.cs   # Role-based authorization filter
    Migrations/                     # EF Core migrations
    Exceptions/
    SQL/                            # Database schema and maintenance scripts
      MedRecPro.sql                 # Full database schema
      MedRecPro_Views.sql           # View definitions
      MedRecPro_Indexes.sql         # Index definitions
      MedRecPro-Deployment.sql      # Deployment scripts
      DbTriggerSetup.sql            # Database triggers
      MedRecPro-Export-Import.ps1   # PowerShell export/import script
      MedRecPro-AzureStatus.sql     # Azure status queries
      MedRecPro-AzureRebuildIndex.sql
      MedRecPro-AzureDisableIndex.sql
      MedRecPro-AzureNuke.sql       # Full database reset (use with caution)
      MedRecPro-AzureOnlineQueryEditorRebuildIndex.sql
      MedRecPro-TableNames.sql
      MedRecPro-TableTruncate.sql
      MedRecPro-TableMissingIndexes.sql

  MedRecProStatic/                  # Static site and AI chat interface
    Program.cs                      # Startup, middleware, OAuth discovery endpoints
    MedRecProStatic.csproj          # .NET 8.0 project file
    web.config                      # IIS config (httpErrors PassThrough, handler isolation)
    appsettings.json
    appsettings.Development.json
    Controllers/
      HomeController.cs             # Index, Terms, Privacy, Chat pages
    Models/
      PageContent.cs                # Strongly-typed content models
    Services/
      ContentService.cs             # JSON content loader
    Views/
      Home/
        Index.cshtml                # Landing page
        Terms.cshtml                # Terms of Service
        Privacy.cshtml              # Privacy Policy
        Chat.cshtml                 # AI chat interface
      Shared/
        _Layout.cshtml              # Master layout
    Content/
      config.json                   # Site config (URLs, branding, version)
      pages.json                    # Page content (home, terms, privacy)
    wwwroot/
      css/                          # Stylesheets
      js/
        site.js                     # Global scripts
        chat/                       # AI chat modules (18 files)
          index.js                  # Main orchestrator
          api-service.js            # API communication
          endpoint-executor.js      # API endpoint execution
          batch-synthesizer.js      # Response synthesis
          checkpoint-manager.js     # State checkpoints
          checkpoint-renderer.js    # Progress UI rendering
          message-renderer.js       # Chat message rendering
          markdown.js               # Markdown-to-HTML
          config.js                 # Chat configuration
          state.js                  # Client state management
          ... (and more)
      lib/                          # Third-party (Bootstrap, jQuery)

  MedRecProMCP/                     # MCP Server (OAuth 2.1 gateway)
    Program.cs                      # Startup, DI, endpoint mappings
    MedRecProMCP.csproj             # .NET 8.0 project file
    server.json                     # MCP registry metadata
    web.config                      # IIS config
    appsettings.json / .Development.json / .Production.json
    Configuration/
      McpServerSettings.cs
      MedRecProApiSettings.cs
      JwtSettings.cs
      OAuthProviderSettings.cs
    Endpoints/
      OAuthEndpoints.cs             # OAuth authorize, token, register, callbacks
      OAuthMetadataEndpoints.cs     # .well-known metadata
    Services/
      McpTokenService.cs            # JWT token generation/validation
      OAuthService.cs               # OAuth flow orchestration
      ClientRegistrationService.cs  # Dynamic Client Registration (RFC 7591)
      PkceService.cs                # PKCE implementation
      FilePersistedCacheService.cs  # File-based persistent cache
      MedRecProApiClient.cs         # HTTP client for API calls
      UserResolutionService.cs      # Resolves upstream IdP email to numeric DB user ID
    Handlers/
      TokenForwardingHandler.cs     # Forwards MCP JWT to API (DelegatingHandler)
    Helpers/
      StringCipher.cs               # AES encryption (copy from API for user ID decryption)
    Tools/
      DrugLabelTools.cs             # MCP tools: drug label search and export
      UserTools.cs                  # MCP tools: user/account operations
    Models/
      AiAgentDtos.cs                # AI integration models
      WorkPlanModels.cs             # Work plan models
    Templates/
      McpDocumentation.html         # Embedded docs page

  MedRecProConsole/                 # Bulk import CLI tool
    Program.cs                      # Entry point
    Services/
      ImportService.cs              # Import orchestration
      ImportProgressTracker.cs      # Progress tracking
    Models/
      AppSettings.cs
      CommandLineArgs.cs
      ImportParameters.cs
      ImportQueueItem.cs
      ImportResults.cs
      ImportProgressFile.cs
    Helpers/
      ConfigurationHelper.cs
      ConsoleHelper.cs
      HelpDocumentation.cs

  MedRecProTest/                    # Unit and integration tests
    SplImportServiceTests.cs
    ProductRenderingServiceTests.cs
    ComparisonServiceTests.cs
    UserDataAccessTests.cs
    LogActivityAsyncTests.cs
    StringCipherTests.cs
    ResolveMcpUserTests.cs          # MCP user resolution and auto-provisioning tests
```

## API Endpoints Summary

All API endpoints are accessed under `/api` in production (IIS virtual application). Controllers use `#if DEBUG` directives to handle the path prefix difference between local development (`/api/[controller]`) and production where IIS strips the `/api` prefix.

### Authentication (`/api/Auth`)

| Method | Route | Description |
|---|---|---|
| GET | `login/{provider}` | Start OAuth flow (Google or Microsoft) |
| GET | `external-logincallback` | OAuth callback handler |
| GET | `user` | Get current authenticated user info |
| POST | `logout` | Log out current user |
| POST | `token-placeholder` | Token exchange |
| GET | `login` | Login page |
| GET | `loginfailure` | Login failure handler |
| GET | `lockout` | Account lockout handler |
| GET | `accessdenied` | Access denied handler |

### Users (`/api/Users`)

| Method | Route | Description |
|---|---|---|
| GET | `me` | Get current user profile |
| GET | `{encryptedUserId}` | Get user by encrypted ID |
| GET | `byemail` | Get user by email |
| POST | `signup` | Create new user account |
| POST | `authenticate` | Authenticate user |
| PUT | `{encryptedUserId}/profile` | Update user profile |
| DELETE | `{encryptedUserId}` | Delete user account |
| PUT | `admin-update` | Administrative user update |
| POST | `rotate-password` | Rotate user password |
| GET | `user/{encryptedUserId}/activity` | Get user activity log |
| GET | `user/{encryptedUserId}/activity/daterange` | Get activity within date range |
| GET | `endpoint-stats` | Get endpoint performance statistics |
| POST | `resolve-mcp` | Resolve email to encrypted user ID (McpBearer auth; auto-provisions new users) |

### Labels (`/api/Label`)

The main data controller with 40+ endpoints covering navigation views, search, CRUD, import, rendering, and AI features.

**Navigation & Search Views:**

| Method | Route | Description |
|---|---|---|
| GET | `product/search` | Search products |
| GET | `product/related` | Related products |
| GET | `product/latest` | Latest product labels |
| GET | `product/latest/details` | Latest product label details |
| GET | `product/indications` | Product indications search |
| GET | `ingredient/search` | Search by ingredient (active/inactive) |
| GET | `ingredient/summaries` | Ingredient summary list |
| GET | `ingredient/active/summaries` | Active ingredients only |
| GET | `ingredient/inactive/summaries` | Inactive ingredients only |
| GET | `ingredient/advanced` | Advanced ingredient search |
| GET | `ingredient/by-application` | Ingredients by application number |
| GET | `ingredient/related` | Related ingredients |
| GET | `labeler/search` | Search by manufacturer/labeler |
| GET | `labeler/summaries` | Labeler summary list |
| GET | `ndc/search` | Search by NDC code |
| GET | `ndc/package/search` | Search by NDC package code |
| GET | `application-number/search` | Search by application number (NDA/ANDA) |
| GET | `application-number/summaries` | Application number summaries |
| GET | `pharmacologic-class/search` | Search by pharmacologic class |
| GET | `pharmacologic-class/hierarchy` | Pharmacologic class hierarchy |
| GET | `pharmacologic-class/summaries` | Pharmacologic class summaries |
| GET | `section/search` | Search by LOINC section code |
| GET | `section/summaries` | Section summaries |
| GET | `document/navigation` | Document navigation tree |
| GET | `document/version-history/{setGuidOrDocumentGuid}` | Document version history |

**Label Content & Rendering:**

| Method | Route | Description |
|---|---|---|
| GET | `section/content/{documentGuid}` | Get section content for a document |
| GET | `markdown/sections/{documentGuid}` | Get label sections as markdown |
| GET | `markdown/export/{documentGuid}` | Export full label as markdown |
| GET | `markdown/download/{documentGuid}` | Download label markdown file |
| GET | `markdown/display/{documentGuid}` | Render label as HTML from markdown |
| GET | `generate/{documentGuid}/{minify}` | Generate updated SPL XML |
| GET | `original/{documentGuid}/{minify}` | Get original SPL XML |
| GET | `single/{documentGuid}` | Get single label details |
| GET | `complete/{pageNumber?}/{pageSize?}` | Paginated complete label list |

**Drug Safety:**

| Method | Route | Description |
|---|---|---|
| GET | `drug-safety/dea-schedule` | DEA schedule classification |

**AI-Powered Endpoints:**

| Method | Route | Description |
|---|---|---|
| GET | `extract-product` | AI-powered product extraction from text |
| GET | `comparison/analysis/{documentGuid}` | Get comparison analysis |
| POST | `comparison/analysis/{documentGuid}` | Start AI comparison analysis |
| GET | `comparison/progress/{operationId}` | Check comparison progress |

**CRUD & Import:**

| Method | Route | Description |
|---|---|---|
| GET | `{menuSelection}/{encryptedId}` | Get single entity by type |
| POST | `{menuSelection}` | Create entity by type |
| PUT | `{menuSelection}/{encryptedId}` | Update entity by type |
| DELETE | `{menuSelection}/{encryptedId}` | Delete entity by type |
| POST | `import` | Bulk SPL ZIP import |
| GET | `import/progress/{operationId}` | Check import progress |

**Reference:**

| Method | Route | Description |
|---|---|---|
| GET | `guide` | API usage guide |
| GET | `inventory/summary` | Database inventory overview |
| GET | `sectionMenu` | Available section menu items |
| GET | `{menuSelection}/documentation` | Documentation for a data type |

### AI (`/api/Ai`)

| Method | Route | Description |
|---|---|---|
| GET | `context` | Get AI context (auth status, demo mode, data counts) |
| POST | `interpret` | Interpret natural language query into API endpoint specs |
| POST | `synthesize` | Synthesize API results into human-readable response |
| GET | `chat` | Convenience endpoint for simple queries |
| POST | `conversations` | Create new conversation |
| GET | `conversations/{conversationId}` | Get conversation |
| GET | `conversations/{conversationId}/history` | Get conversation history |
| DELETE | `conversations/{conversationId}` | Delete conversation |
| GET | `conversations/stats` | Get conversation statistics |
| POST | `retry` | Retry last AI operation |

### Settings (`/api/Settings`)

| Method | Route | Description |
|---|---|---|
| GET | `demomode` | Check demo mode status |
| GET | `info` | Application info |
| GET | `features` | Feature flags |
| GET | `database-limits` | Database limits |
| GET | `metrics/database-cost` | Azure SQL free tier usage and cost projections |
| POST | `clearmanagedcache` | Clear managed cache |
| GET | `logs` | Activity logs |
| GET | `logs/statistics` | Log statistics |
| GET | `logs/categories` | Log categories |
| GET | `logs/by-date` | Logs filtered by date |
| GET | `logs/by-category` | Logs filtered by category |
| GET | `logs/by-user` | Logs filtered by user |
| GET | `logs/users` | Users with log entries |
| GET | `test/app-credential` | Test Azure credentials |
| GET | `test/app-metrics-pipeline` | Test metrics pipeline |

### MCP Server (`/mcp`)

The MCP server exposes its own endpoints. See the [MedRecProMCP README](MedRecProMCP/README.md) for full details.

| Method | Route | Description |
|---|---|---|
| POST | `/mcp` | MCP Streamable HTTP transport (JSON-RPC) |
| GET | `/mcp/health` | Health check |
| GET | `/mcp/docs` | HTML documentation page |
| GET | `/mcp/.well-known/oauth-protected-resource` | Protected Resource Metadata (RFC 9728) |
| GET | `/mcp/.well-known/oauth-authorization-server` | Authorization Server Metadata (RFC 8414) |
| GET | `/mcp/oauth/authorize` | OAuth authorization endpoint |
| POST | `/mcp/oauth/token` | Token exchange endpoint |
| POST | `/mcp/oauth/register` | Dynamic Client Registration (RFC 7591) |
| GET | `/mcp/oauth/callback/google` | Google OAuth callback |
| GET | `/mcp/oauth/callback/microsoft` | Microsoft OAuth callback |

## MedRecProStatic and MCP Relationship

MedRecProStatic serves the OAuth/MCP discovery metadata at the domain root because the MCP SDK resolves `/.well-known/*` relative to the domain, not the MCP endpoint path. When Claude connects to `https://www.medrecpro.com/mcp`, the SDK looks for discovery at `https://www.medrecpro.com/.well-known/oauth-protected-resource` and `/.well-known/oauth-authorization-server`.

These endpoints are registered directly in MedRecProStatic's `Program.cs` as static JSON responses. Attempts to redirect from the root site to `/mcp/.well-known/*` failed because 302 redirects cause the MCP SDK to derive the wrong resource URI, and reverse proxying through Cloudflare triggers Bot Fight Mode (403 errors).

MedRecProStatic also has a critical `web.config` setting (`httpErrors existingResponse="PassThrough"`) placed outside the `<location>` element so it is inherited by the MCP and API virtual applications. Without this, IIS replaces 401 responses with HTML error pages, breaking the MCP OAuth challenge flow.

## Database Schema and SQL Scripts

Database schema definitions and maintenance scripts are maintained in `MedRecPro/SQL/`. These are the authoritative source for schema updates, view definitions, and index management.

| Script | Purpose |
|---|---|
| `MedRecPro.sql` | Full database schema (tables, constraints, relationships) |
| `MedRecPro_Views.sql` | View definitions used by navigation and search endpoints |
| `MedRecPro_Indexes.sql` | Index definitions for query performance |
| `MedRecPro-Deployment.sql` | Deployment-time schema updates |
| `DbTriggerSetup.sql` | Database trigger configuration |
| `MedRecPro-Export-Import.ps1` | PowerShell script for database export/import |
| `MedRecPro-AzureStatus.sql` | Azure SQL status and diagnostics queries |
| `MedRecPro-AzureRebuildIndex.sql` | Index rebuild for Azure SQL |
| `MedRecPro-AzureDisableIndex.sql` | Disable indexes during bulk operations |
| `MedRecPro-AzureOnlineQueryEditorRebuildIndex.sql` | Index rebuild via Azure Query Editor |
| `MedRecPro-AzureNuke.sql` | Full database reset (destructive) |
| `MedRecPro-TableNames.sql` | List all table names |
| `MedRecPro-TableTruncate.sql` | Truncate tables for reimport |
| `MedRecPro-TableMissingIndexes.sql` | Identify missing indexes |

When updating database schemas or views, modify the scripts in `MedRecPro/SQL/` and run them against the target database. The `MedRecPro_Views.sql` file is particularly important as the navigation view queries (ingredient search, labeler search, pharmacologic class hierarchy, etc.) are defined there and power many of the API search endpoints.

## AI Skills System

The API includes an agentic AI layer that enables natural language interaction with pharmaceutical data. The system follows a **request-interpret-execute-synthesize** pattern:

1. User submits a natural language query to `POST /api/Ai/interpret`
2. Claude interprets the query and returns API endpoint specifications
3. The client executes the specified API endpoints
4. Results are sent to `POST /api/Ai/synthesize`
5. Claude produces a human-readable response with suggested follow-ups

AI skills are defined as markdown prompt files in `MedRecPro/Skills/`. Key skills include:
- **Indication Discovery** - Find drugs by indication/use case
- **Equianalgesic Conversion** - Opioid dose conversion calculations
- **Product Extraction** - AI-powered extraction of product details from text
- **Pharmacologic Class Matching** - Map drugs to pharmacologic classifications
- **Label Content** - Retrieve and synthesize label sections
- **Data Rescue** - Fallback strategies for missing or incomplete data

## Getting Started

### Prerequisites

- .NET 8.0 SDK (LTS)
- SQL Server (local) or Azure SQL Database
- Visual Studio 2022 or VS Code

### 1. Clone the repository

```bash
git clone <repo-url>
cd MedRecPro
```

### 2. Configure the API

Create user secrets for the MedRecPro API project:

```bash
cd MedRecPro
dotnet user-secrets init
dotnet user-secrets set "Dev:DB:Connection" "Server=localhost;Database=MedRecProDB;User Id=sa;Password=your-password;"
dotnet user-secrets set "Security:DB:PKSecret" "your-encryption-key"
dotnet user-secrets set "Jwt:Key" "your-jwt-signing-key-min-32-chars"
dotnet user-secrets set "Jwt:Issuer" "MedRecPro"
dotnet user-secrets set "Jwt:Audience" "MedRecUsers"
dotnet user-secrets set "Authentication:Google:ClientId" "your-google-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-google-client-secret"
dotnet user-secrets set "Authentication:Microsoft:ClientId" "your-microsoft-app-id"
dotnet user-secrets set "Authentication:Microsoft:ClientSecret:Dev" "your-microsoft-secret"
dotnet user-secrets set "Authentication:Microsoft:TenantId" "your-tenant-id"
dotnet user-secrets set "ClaudeApiSettings:ApiKey" "your-claude-api-key"
```

### 3. Set up the database

Run the schema script from `MedRecPro/SQL/MedRecPro.sql` against your SQL Server instance, then apply views and indexes:

```bash
# Apply schema, views, and indexes in order
sqlcmd -S localhost -d MedRecProDB -i MedRecPro/SQL/MedRecPro.sql
sqlcmd -S localhost -d MedRecProDB -i MedRecPro/SQL/MedRecPro_Views.sql
sqlcmd -S localhost -d MedRecProDB -i MedRecPro/SQL/MedRecPro_Indexes.sql
```

Or run EF Core migrations:

```bash
cd MedRecPro
dotnet ef database update
```

### 4. Run the projects

```bash
# Terminal 1: API (port 5093)
cd MedRecPro
dotnet run

# Terminal 2: Static site (port 5001)
cd MedRecProStatic
dotnet run

# Terminal 3: MCP server (port 5233, optional)
cd MedRecProMCP
dotnet run
```

### 5. Import SPL data

Upload SPL ZIP files through the API import endpoint or use the console importer:

```bash
cd MedRecProConsole
dotnet run -- --help
```

SPL ZIP files can be downloaded from the [DailyMed SPL Resources](https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm) page.

## Setup Pitfalls and Fixes

### IIS Virtual Application Path Stripping

IIS strips the virtual application prefix from requests before forwarding to ASP.NET Core. A request to `/api/Label/search` arrives at Kestrel as `/Label/search`. All controllers use `#if DEBUG` compiler directives to handle this:

```csharp
#if DEBUG
[Route("api/[controller]")]   // Local: full path
#else
[Route("[controller]")]        // Azure: IIS strips /api prefix
#endif
```

The same pattern applies to MCP routes and Swagger paths.

### Cloudflare + Azure App Service Managed Certificates

Azure App Service Managed Certificates do not work with Cloudflare proxy enabled. Use Cloudflare Origin Certificates instead:

1. Create an Origin Certificate in Cloudflare (SSL/TLS > Origin Server)
2. Convert to PFX: `openssl pkcs12 -export -out origin.pfx -inkey key.pem -in cert.pem`
3. Upload to Azure App Service > Certificates
4. Bind to custom domains with SNI SSL

### OAuth Redirect URI Prefix

Production redirect URIs must include the `/api/` prefix:
- Correct: `https://your-domain.com/api/signin-google`
- Wrong: `https://your-domain.com/signin-google`

### Cloudflare Bot Blocking

Cloudflare has multiple independent bot-blocking systems that can interfere with MCP and server-to-server calls:

- **"Block AI Bots"** (WAF managed rule) - Must allow Claude-User via AI Crawl Control settings
- **"Bot Fight Mode"** - Blocks requests from hosting provider IPs. Cannot be bypassed with WAF rules. Whitelist Azure App Service outbound IPs via IP Access Rules (Security > WAF > Tools)
- Always set a `User-Agent` header on outbound HttpClients to avoid bot detection

### Azure SQL Serverless Cold Starts

Azure SQL Serverless auto-pauses after inactivity. Resuming takes 15-30+ seconds, which can exceed default timeouts. Mitigations:
- **`DatabaseKeepAliveService`** pings the database with `SELECT 1` every 55 minutes during business hours (Mon-Fri, 8 AM - 5 PM Eastern) to prevent auto-pause. Configured via the `DatabaseKeepAlive` section in `appsettings.json`
- Increase HttpClient and SQL command timeouts
- Use `EnableRetryOnFailure()` in `UseSqlServer()` configuration
- Increase the auto-pause delay during active development

### Key Vault Secret Naming

Key Vault uses `--` (double dash) as separator; ASP.NET Core configuration uses `:` (colon). The framework maps between them automatically:
- Key Vault: `Authentication--Google--ClientId`
- Config: `Authentication:Google:ClientId`

Ensure no extra characters (trailing commas, quotes) in Key Vault secret values.

### IIS httpErrors PassThrough

Without `<httpErrors existingResponse="PassThrough" />` in the root site's `web.config`, IIS replaces HTTP 401 responses with HTML error pages. This breaks OAuth challenge flows where the `WWW-Authenticate` header must reach the client. This setting must be in the root site (`MedRecProStatic`) because child virtual applications inherit it.

### JWT Multi-Issuer/Audience

When both the API and MCP server issue JWT tokens with different issuer/audience values, the JWT Bearer handler must accept both using `ValidIssuers` and `ValidAudiences` arrays instead of the singular properties.

## Azure SQL Free Tier Monitoring

The API includes built-in monitoring for Azure SQL Database's serverless free tier (100,000 vCore seconds/month). The `AzureSqlMetricsService` queries Azure Monitor Metrics to track consumption, project monthly costs, and recommend throttling levels. See the `GET /api/Settings/metrics/database-cost` endpoint.

## Security Configuration

Security settings should be stored in user secrets (development) or Azure Key Vault (production):

```json
{
  "Authentication:Google:ClientId": "your-google-client-id.apps.googleusercontent.com",
  "Authentication:Google:ClientSecret": "your-google-client-secret",
  "Authentication:Microsoft:ClientId": "your-microsoft-app-id",
  "Authentication:Microsoft:ClientSecret:Dev": "your-microsoft-secret",
  "Authentication:Microsoft:ClientSecret:Prod": "your-microsoft-secret",
  "Authentication:Microsoft:TenantId": "your-tenant-id",
  "Security:DB:PKSecret": "your-encryption-key",
  "Jwt:Key": "your-jwt-signing-key",
  "Jwt:Issuer": "MedRecPro",
  "Jwt:Audience": "MedRecUsers",
  "Jwt:ExpirationMinutes": 60,
  "ClaudeApiSettings:ApiKey": "your-claude-api-key",
  "Dev:DB:Connection": "your-dev-connection-string",
  "Prod:DB:Connection": "your-prod-connection-string"
}
```

Changing `Security:DB:PKSecret` will break all existing encrypted URLs, favorites, and bookmarks.

## Production Deployment

See the detailed deployment guides in each project's README:
- **[MedRecProMCP README](MedRecProMCP/README.md)** - MCP server setup, OAuth provider config, Cloudflare rules, Claude.ai connector integration, and troubleshooting
- **[MedRecProStatic README](MedRecProStatic/README.md)** - Static site deployment, content management, and IIS configuration

### Deployment Checklist

1. Publish each project to its virtual application path on Azure App Service
2. Verify Azure Key Vault secrets are configured
3. Purge Cloudflare cache after deployment
4. Test authentication flows (Google and Microsoft OAuth)
5. Verify Swagger UI loads at `/api/swagger/index.html`
6. Verify MCP health check at `/mcp/health`
7. Test AI chat at the static site

## License

See the [LICENSE.txt](MedRecPro/LICENSE.txt) file for details.
