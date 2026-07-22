# MedRecPro

MedRecPro is a pharmaceutical structured product label (SPL) management platform built with ASP.NET Core. It provides secure access to FDA drug label data through a RESTful API, an AI-powered chat interface, a Model Context Protocol (MCP) server for integration with AI assistants like Claude, and an interactive **adverse-event risk dashboard** built on a multi-stage table-standardization and risk-statistics pipeline.

## Specifications

- **HL7 Version**: HL7 Dec 2023 https://www.fda.gov/media/84201/download
- **Info**: https://www.fda.gov/industry/fda-data-standards-advisory-board/structured-product-labeling-resources
- **SPL Data Source**: https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm
- **Orange Book Data Source**: https://www.fda.gov/drugs/drug-approvals-and-databases/approved-drug-products-therapeutic-equivalence-evaluations-orange-book

## Technology Stack

- **Runtime**: ASP.NET Core (.NET 8.0 LTS)
- **Database**: Azure SQL Server (Serverless free tier) with Dapper + Entity Framework Core
- **Authentication**: Cookie-based auth with Google and Microsoft OAuth providers; JWT bearer tokens for API access; McpBearer JWT scheme for MCP server integration (claims normalized to standard JWT short names: `sub`, `name`, `email`)
- **AI Integration**: Claude API for natural language query interpretation and synthesis
- **MCP Protocol**: Model Context Protocol server with OAuth 2.1 (PKCE S256) for Claude.ai connector integration
- **Hosting**: Azure App Service (Windows, IIS) with Cloudflare CDN/WAF/DNS
- **Secrets**: Azure Key Vault
- **API Documentation**: Swagger/OpenAPI
- **Error Contracts**: Centralized ASP.NET Core `IExceptionHandler` with sanitized RFC 7807 `ProblemDetails` responses and request trace correlation
- **Testing**: MSTest, `WebApplicationFactory<Program>`, kept-open SQLite test databases, reviewed Debug/Release OpenAPI snapshots, and real-host HTTP contracts
- **Verification**: PowerShell gate runner plus GitHub Actions for fast architecture checks, Debug/Release contracts, and the full regression suite
- **SPL Rendering**: RazorLight templates for SPL XML-to-HTML generation

## Solution Architecture

The solution's three web projects are deployed to a single Azure App Service using IIS virtual applications (the console, library, React SPA source, prototypes, and test projects are not deployed as separate apps):

```
                        Cloudflare (CDN/WAF/DNS)
                                |
                                v
    Azure App Service: "MedRecPro" (Windows, IIS)
   +--------------------------------------------------------+
   |                                                        |
   |  /                site\wwwroot       MedRecProStatic   |
   |  /adverse-events  (MedRecProStatic, React island)      |
   |  /api             site\wwwroot\api   MedRecPro API     |
   |  /mcp             site\wwwroot\mcp   MedRecProMCP      |
   |                                                        |
   +--------------------------------------------------------+
                        |
                        v
               Azure Key Vault
              (medrecprovault)
```

| Virtual Path | Project | Purpose |
|---|---|---|
| `/` | **MedRecProStatic** | Static site, marketing pages, AI chat UI, OAuth/MCP discovery metadata, adverse-event dashboard host |
| `/adverse-events` | **MedRecProReact** (hosted by MedRecProStatic) | Adverse-event risk dashboard React island |
| `/api` | **MedRecPro** | REST API: SPL upload/progress boundary, label CRUD, authentication, AI interpret/synthesize, adverse-event dashboard data |
| `/mcp` | **MedRecProMCP** | MCP server: OAuth 2.1 gateway for Claude.ai integration |
| _(CLI)_ | **MedRecProConsole** | Standalone bulk import utility over the shared import library (SPL labels, FDA Orange Book, and table standardization) |
| _(library)_ | **MedRecProImportClass** | Shared class library: entity models, parsing services, table-standardization pipeline, and EF Core context for SPL and Orange Book import |
| _(SPA source)_ | **MedRecProReact** | React + Vite source for the adverse-event dashboard; builds into MedRecProStatic's web root |
| _(prototypes)_ | **MedRecProPrototypes** | Standalone HTML/JS prototypes (e.g. the AE dashboard) that seed production UI work |
| _(test)_ | **MedRecProTest** | MSTest unit, relational, real-host integration, architecture, and Debug/Release contract coverage |

### Build and Solution Boundary

`MedRecPro.sln` is the focused API/import/test solution. It intentionally contains `MedRecPro`, `MedRecProImportClass`, and `MedRecProTest`; the test project references `MedRecProConsole`, so the CLI stays in the regular regression build without making the solution responsible for every deployed app. `MedRecProStatic` and `MedRecProMCP` are separately deployed IIS virtual applications and should be built or published explicitly when their code changes:

```bash
dotnet build .\MedRecPro.sln --no-restore -p:UseAppHost=false
dotnet build .\MedRecProStatic\MedRecProStatic.csproj --no-restore -p:UseAppHost=false
dotnet build .\MedRecProMCP\MedRecProMCP.csproj --no-restore -p:UseAppHost=false
```

If a local apphost executable is locked by a running process, keep output inside the workspace and disable apphost generation for the verification pass, for example `dotnet build .\MedRecPro.sln --no-restore -p:UseAppHost=false -p:BaseOutputPath=.\MedRecPro\.codex-build\`. The API project excludes `bin/**` and `.codex-build/**` from default item globbing so copied RazorLight templates from generated output cannot re-enter compilation when `BaseOutputPath` is redirected.

### How the Projects Relate

**MedRecProStatic** is the user-facing front end. Its AI chat interface (`/Home/Chat`) communicates with the API using a request-interpret-execute-synthesize pattern: user queries are sent to the API's AI endpoints, which use Claude to map natural language to API calls. The static site also serves OAuth/MCP discovery metadata (`/.well-known/*`) at the domain root on behalf of the MCP server, because the MCP SDK resolves discovery URLs relative to the domain root rather than the `/mcp` path.

**MedRecPro (API)** is the core backend. It handles label data CRUD, user authentication, AI query interpretation via Claude, database views for navigation, SPL document rendering via RazorLight templates, and the HTTP/progress boundary for SPL ZIP uploads. The public Label surface remains one 51-operation `/api/Label` route family, but its implementation is split across small feature controllers and injected query/operation services. The former `DtoLabelAccess` implementation is now owned by those services; one forwarding-only static facade remains for external .NET compatibility. The actual SPL ZIP traversal, XML parsing, duplicate checks, and parser orchestration live in **MedRecProImportClass**; the API keeps thin compatibility adapters and maps import-library result DTOs back into the web import-progress models.

**MedRecProMCP** is an OAuth 2.1 gateway that exposes MedRecPro API capabilities as MCP tools. When Claude.ai connects, it authenticates users through Google/Microsoft OAuth, resolves upstream identity provider identities to numeric database user IDs (auto-provisioning new users if needed), then forwards authenticated MCP JWTs to the MedRecPro API. It uses JWT tokens, PKCE (S256), Dynamic Client Registration (RFC 7591), and a shared PKSecret for encrypted user ID exchange with the API.

**MedRecProReact** is the source for the adverse-event risk dashboard — a React + Vite single-page "island". Its Vite build emits a deterministic bundle directly into `MedRecProStatic/wwwroot/ae-dashboard`, which MedRecProStatic serves at `/adverse-events`. The dashboard reads only from the API's `/api/AdverseEvent` surface (`AdverseEventController`), which in turn queries the materialized AE risk tables produced by the table-standardization pipeline (Stage 5). Current dashboard focuses cover product-level risk, pharmacologic-class SOC correlation, and MedDRA-system-scoped class correlation. See the [MedRecProReact README](MedRecProReact/README.md) for the dashboard, and the [MedRecProImportClass README](MedRecProImportClass/README.md) for the risk-statistics contract.

### Backend Boundaries and Compatibility Safeguards

| Area | Current design |
|---|---|
| Startup | `Program.cs` is an ordered composition shell. Capability-focused extensions under `MedRecPro/Configuration` own data access, platform services, authentication, MVC, Swagger, middleware, rendering, import, and background-service registration. |
| Feature routing | `ApiControllerBase` retains the compile-time Debug/Release prefix split. `FeatureControllerNameConvention` applies each split controller's `FeatureControllerNameAttribute` value, so implementation class names never leak into routes. |
| Controller ownership | The original `LabelController` and `LabelSearchController` are empty compatibility shells. Search, document, section, markdown, import, comparison, and metadata operations live in feature controllers with no more than eight actions each. |
| Label data access | Scoped feature services own EF Core query and document-graph behavior. `DtoLabelAccess.Compatibility.cs` preserves the 57 public names/58 overloads as forwarding-only adapters; first-party runtime callers use DI services. |
| Queued work | Import progress crosses an explicit `IImportOperationStatusStore` boundary. Comparison jobs use a singleton coordinator that snapshots inputs, creates a fresh scope per job, and links cancellation to application shutdown instead of the originating request. |
| Encryption | `DatabaseSecurityOptions` is bound centrally and `IPrimaryKeyCipher` is the injected encryption boundary for migrated Label, authentication, AI, authorization-filter, Claude-search, and Orange Book paths. Architecture tests freeze the explicitly deferred legacy-reader inventory. |
| Errors and logs | `MedRecProExceptionHandler` owns unexpected HTTP failures in every environment, returning sanitized `ProblemDetails` and logging the same trace ID. Production logging uses structured templates, filtered/redacted in-memory administration records, and safe typed log DTOs; see [logging conventions](MedRecPro/docs/logging-conventions.md). |
| Verification | A deterministic real host replaces both SQL contexts with SQLite, removes reviewed unsafe workers, and supplies test authentication/configuration. Debug and separately compiled Release suites protect all 51 Label routes, OpenAPI, headers, files, validation, authorization, and error bodies; see [verification gates](MedRecPro/docs/verification-gates.md). |

## Repository File Structure

The tree below is intentionally curated around deployable projects and the current architecture/verification boundaries. Every named path was checked against the live repository on 2026-07-14.

```text
./
  README.md
  MedRecPro.sln                     # Focused API/import/test solution
  .github/
    workflows/
      medrecpro-verification.yml    # Fast + Debug/Release contract + full CI gates
  scripts/
    Invoke-MedRecProVerification.ps1

  MedRecPro/                        # ASP.NET Core Web API
    Program.cs                      # Ordered composition and middleware shell
    MedRecPro.csproj                # Excludes bin/** and .codex-build/** from source discovery
    Configuration/
      DatabaseSecurityOptions.cs
      MedRecProApplicationServiceExtensions.cs
      MedRecProAuthenticationExtensions.cs
      MedRecProMiddlewareExtensions.cs
      MedRecProMvcExtensions.cs
      MedRecProStartupDiagnosticsExtensions.cs
      MedRecProSwaggerExtensions.cs
    Controllers/
      ApiControllerBase.cs          # Debug api/[controller] vs Release [controller]
      LabelController.cs            # Empty compatibility shell
      LabelSearchController.cs      # Empty compatibility shell
      LabelApplicationController.cs
      LabelClassificationController.cs
      LabelComparisonController.cs
      LabelDocumentController.cs
      LabelImportController.cs
      LabelIngredientController.cs
      LabelMarkdownController.cs
      LabelMetadataController.cs
      LabelProductIdentifierController.cs
      LabelProductSearchController.cs
      LabelSectionController.cs
      LabelSectionNavigationController.cs
      FeatureControllerNameAttribute.cs
      SwaggerGroupAttribute.cs
      AdverseEventController.cs
      AiController.cs
      AuthController.cs
      OrangeBookController.cs
      SettingsController.cs
      UsersController.cs
    Service/
      AeDashboardServices.cs        # Injected AE dashboard use-case services and policies
      AppCacheService.cs            # IAppCache and user-context seams
      PrimaryKeyCipher.cs           # IPrimaryKeyCipher encryption boundary
      ClaudeSkillNameMapper.cs
      SplImportService.cs           # Web compatibility adapter over the import library
      SplParsingService.cs          # Legacy parser adapter over the import library
      Common/
        ActivityLogDispatcher.cs    # Channel dispatcher + scoped hosted consumer
      Label/
        ComparisonJobCoordinator.cs
        CompleteLabelService.cs
        LabelAiSearchService.cs
        LabelQueryServiceContracts.cs
        LabelQueryServices.cs
        LabelSectionCrudService.cs
        LabelXmlDocumentService.cs
        Common/
          LabelQueryCachePolicy.cs
          LegacyDtoLabelCacheKeyBuilder.cs
        Implementation/
          LabelQueryDataAccess.cs
          LabelQueryDataAccess-BatchLoaders.cs
          LabelQueryDataAccess-Document.cs
          LabelQueryDataAccess-Views.cs
          LabelQueryLegacyCompatibility.cs
    DataAccess/
      DtoLabelAccess.Compatibility.cs   # Forwarding-only 57-name/58-overload facade
      DtoLabelAccess-AeDashboard.cs     # AeDashboardDataAccess query implementation
      AeDashboardFavoriteAccess.cs
      AeDashboardDerivation.cs
      AeCorrelationPipelineModels.cs
      RepositoryDataAccess.cs
      UserDataAccess.cs
    Features/
      AeDashboard/
        Mapping/
          AeDashboardDtoMapper.cs
        Models/
          AeDashboardDto.cs
        Persistence/
          AeDashboardModelConfigurations.cs
      Label/
        Mapping/
          LabelDocumentAssembler.cs
    Exceptions/
      AuthorizationExceptions.cs
      MedRecProExceptionHandler.cs
      RequestCorrelation.cs
    Filters/
      ActivityLogActionFilter.cs
      RequireActorAttributeFilter.cs
      RequireUserRoleAttributeFilter.cs
    Mappers/
      ImportResultMapper.cs
    Models/
      ImportStatus.cs               # Web/import operation-status boundary
    Middleware/
      TarpitMiddleware.cs
    Skills/
      skills.md                     # Capability contracts
      selectors.md                  # Skill routing rules
      interfaces/                   # API and response mappings
    Views/
      SplTemplates/                 # RazorLight SPL templates
      Stylesheets/
    SQL/                            # Schema, views, indexes, import/export, AE materialization
    docs/
      logging-conventions.md
      verification-gates.md

  MedRecProImportClass/             # Shared SPL/Orange Book import and analytics library
    Models/
      Import.cs
      ImportStatus.cs
      OrangeBook.cs
    DataAccess/
      RepositoryDataAccess.cs
      UserDataAccess.cs
    Service/
      SplImportService.cs
      SplParsingService.cs
      SplDataService.cs
      ParsingServices/              # SPL and Orange Book parsers
      ParsingValidators/
      TransformationServices/
        TableStandardizationServiceCollectionExtensions.cs
        ClaudeCorrectionPayloadBuilder.cs
        BaseTableFlattening/
          ColumnStandardizationService.cs
        AdverseEventTableFlattening/
    TableStandards/
      normalization-rules.md
      column-contracts.md
      table-types.md
    Context/
      ApplicationDbContext.cs

  MedRecProConsole/                 # Bulk SPL/Orange Book import CLI
    Program.cs
    Services/
      ImportService.cs
      ImportProgressTracker.cs
      OrangeBookImportService.cs
    Models/
    Helpers/

  MedRecProStatic/                  # Static site, AI chat, and dashboard host
    Program.cs
    Controllers/
      HomeController.cs
      AdverseEventDashboardController.cs
    Views/
      Home/
      AdverseEventDashboard/
      Shared/
    wwwroot/
      ae-dashboard/                 # Committed MedRecProReact build
      js/
        chat/

  MedRecProReact/                   # React + Vite adverse-event dashboard source
    package.json
    vite.config.js
    src/
      App.jsx
      api/
      components/
      hooks/
      lib/
      test/

  MedRecProMCP/                     # OAuth 2.1 MCP gateway
    Program.cs
    Endpoints/
      OAuthEndpoints.cs
      OAuthMetadataEndpoints.cs
    Services/
    Tools/
      DrugLabelTools.cs
      UserTools.cs

  MedRecProPrototypes/              # Standalone UI prototypes

  MedRecProTest/                    # MSTest unit, relational, host, and contract suite
    MedRecProTest.csproj
    TestInfrastructure/
      MedRecProTestConfiguration.cs
      MedRecProWebApplicationFactory.cs
      MedRecProHostFixture.cs
      TestAuthenticationHandler.cs
      TestExceptionThrowingStartupFilter.cs
      CountingDbCommandInterceptor.cs
      ControllerArchitectureTests.cs
      LoggingAndErrorHandlingArchitectureTests.cs
      ReflectionUsageArchitectureTests.cs
      TestProjectDependencyGuardTests.cs
    Contracts/
      LabelOpenApiContractTests.cs
    Integration/
      LabelHttpContractTests.cs
      StartupSmokeTests.cs
    TestData/
      OpenApi/
        label-debug.contract.json
        label-release.contract.json
    ColumnStandardizationServiceTests.cs   # Thin shared-fixture shell
    ColumnStandardizationTestFixture.cs
    ColumnStandardization/
      ColumnStandardizationCleanupTests.cs
      ColumnStandardizationColumnContractTests.cs
      ColumnStandardizationInitializationAndCategoryTests.cs
      ColumnStandardizationPipelineTests.cs
      ColumnStandardizationPkAndDefectRegressionTests.cs
      ColumnStandardizationTreatmentArmRulesPart1Tests.cs
      ColumnStandardizationTreatmentArmRulesPart2Tests.cs
    TableParserTests.cs                    # Thin shared-parser shell
    TableParserTestHelper.cs
    TableParsing/
      TableParserEfficacyAndRouterTests.cs
      TableParserGeneralRegressionTests.cs
      TableParserPkBasicAndCompoundTests.cs
      TableParserPkHeaderRegressionTests.cs
      TableParserPkRoutingTests.cs
      TableParserPkWaveAndHygieneTests.cs
      TableParserSimpleArmAndAeTests.cs
    DtoLabelAccessFacadeArchitectureTests.cs
    DtoLabelAccessSignatureCompatibilityTests.cs
    LabelControllerRouteCompatibilityTests.cs
    MedRecProPublicSurfaceInventoryTests.cs
    ServiceRegistrationTests.cs
```

## API Endpoints Summary

All API endpoints are accessed under `/api` in production (IIS virtual application). API controllers inherit the `#if DEBUG` route in `ApiControllerBase`, preserving the local `/api/[controller]` prefix and the production app-relative route where IIS supplies and strips `/api`.

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

The compatibility-stable Label route family contains 51 operations covering navigation views, search, CRUD, import, rendering, and AI features. Internally, those actions are distributed across feature controllers; `LabelController` and `LabelSearchController` remain empty shells so the public controller name and Swagger/route contracts stay unchanged.

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
| GET | `indication/search` | AI-assisted indication search with label-text validation |
| GET | `comparison/analysis/{documentGuid}` | Get comparison analysis |
| POST | `comparison/analysis/{documentGuid}` | Start AI comparison analysis |
| GET | `comparison/progress/{operationId}` | Check comparison progress |

**CRUD & Import:**

| Method | Route | Description |
|---|---|---|
| GET | `{menuSelection}/{encryptedId}` | Get single entity by type |
| GET | `section/{menuSelection}` | Get records for a label entity type |
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

Administrative log endpoints remain Admin-only and return typed, redacted projections. Invalid log filters use validation `ProblemDetails`; unexpected failures use the global trace-correlated `ProblemDetails` contract.

### Adverse Event Dashboard (`/api/AdverseEvent`)

Backs the React adverse-event risk dashboard. The whole controller is gated by the `FeatureFlags:AeDashboard:Enabled` flag (returns 503 when disabled). Reads are anonymous (favorite state is enriched for authenticated users); favorite writes require `ApiAccess`. Class-picker and MedDRA system-picker responses expose pagination/aggregate totals via the `X-Page-Number`, `X-Page-Size`, `X-Total-Count`, and `X-Chartable-Count` headers.

| Method | Route | Description |
|---|---|---|
| GET | `products` | Dashboard product list with KPI/coverage data (paged) |
| GET | `products/catalog` | Slim cached product catalog for the picker |
| GET | `products/count` | Distinct product inventory count |
| GET | `products/favorites` | Authenticated user's favorite products |
| PUT | `products/{documentGuid}/favorite` | Add a favorite (idempotent, 204) |
| DELETE | `products/{documentGuid}/favorite` | Remove a favorite (idempotent, 204) |
| GET | `products/{documentGuid}/triage` | Tiered triage signals for one product |
| GET | `products/{documentGuid}/forest` | Forest-plot payload for one product |
| GET | `products/{documentGuid}/quadrant` | Risk-vs-precision quadrant payload |
| GET | `reverse-lookup` | Products reporting one or more exact AE terms |
| GET | `interchange` | Comparator-aware two-product therapeutic interchange comparison |
| GET | `correlation/classes` | Pharmacologic classes with AE data (class picker) |
| GET | `correlation` | SOC × SOC correlation map for one class |
| GET | `correlation/heatmap` | Sparse SOC × drug RR heatmap for one class |
| GET | `correlation/cell` | Per-drug drill-down for one correlation cell |
| GET | `correlation/systems` | MedDRA System Organ Classes with AE rows (system picker) |
| GET | `correlation/systems/map` | Selected-system pharmacologic-class × pharmacologic-class correlation map |
| GET | `correlation/systems/heatmap` | Selected-system pharmacologic-class × drug RR heatmap |
| GET | `correlation/systems/cell` | Per-term drill-down for one selected-system class-pair cell |

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
| `MedRecPro-Azure-Data-Refresh.ps1` | **Normal single entry point** for a manifest-bound local-to-Azure refresh |
| `MedRecPro-Export-Import.ps1` | Supported lower-level primary-domain BCP export/import tool |
| `MedRecPro-AzureStatus.sql` | Azure SQL status and diagnostics queries |
| `MedRecPro-AzureRebuildIndex.sql` | Index rebuild for Azure SQL |
| `MedRecPro-AzureDisableIndex.sql` | Disable indexes during bulk operations |
| `MedRecPro-AzureOnlineQueryEditorRebuildIndex.sql` | Index rebuild via Azure Query Editor |
| `MedRecPro-AzureNuke.sql` | Full database reset (destructive) |
| `MedRecPro-TableNames.sql` | List all table names |
| `MedRecPro-TableTruncate.sql` | Truncate tables for reimport |
| `MedRecPro-TableMissingIndexes.sql` | Identify missing indexes |
| `MedRecPro-TableCreate-OrangeBook.sql` | Orange Book table definitions (7 tables, indexes, extended properties) |
| `MedRecPro-AzureOrangeBookNuke.sql` | Targeted Orange Book truncation with safety preview mode |
| `MedRecPro-Table-tmp_FlattenedAdverseEventCoverageTable.sql` | Stage 5 AE source-row coverage / non-RR audit table |
| `MedRecPro-Table-tmp_FlattenedAdverseEventTable.sql` | Stage 5 RR-ready AE statistics (RR/DNRR/CI + PERSISTED log columns) |
| `MedRecPro-Table-tmp_FlattenedAdverseEventRiskTable.sql` | Materialization of `dbo.vw_AeRisk` for the dashboard |
| `MedRecPro-Table-tmp_AeDashboardProductCatalog.sql` | Materialization of `dbo.vw_AeDashboardProductCatalog` (picker) |
| `MedRecPro-AdverseEvent-Export-Import.ps1` | BCP full-refresh of the AE tables (local SQL Server → Azure SQL, truncate-then-import) |

### Azure data refresh

For a normal complete local-to-Azure data refresh, start only the unified entry point. It exports and validates the Core, Orange Book, materialized temp, and adverse-event domains before Azure mutation; shows the resolved source/target and inventory; then requires the exact `REFRESH server.database.windows.net/MedRecPro` confirmation token. It records only non-secret run state under `C:\MedRecPro-Migration\Refreshes` and attempts index recovery before an incomplete run exits.

```powershell
.\MedRecPro\SQL\MedRecPro-Azure-Data-Refresh.ps1 `
  -AzureServer "server.database.windows.net" `
  -AzureDatabase "MedRecPro" `
  -AzureUser "migration-user"
```

Use `-ExportOnly` to create a validated local snapshot without Azure mutation, `-ValidateOnly` for source/target preflight, `-RefreshExclusionRules` only when the normally preserved `PharmClassDosageFormExclusion` table must be refreshed, and `-ResumeRun <run-directory>` only for that run's unchanged manifest and data files. `-WhatIf` previews the stage graph and never acts as destructive authorization. The individual domain workers and nuke/index SQL files remain supported manual/recovery tools; do not combine them with a unified run.
When updating database schemas or views, modify the scripts in `MedRecPro/SQL/` and run them against the target database. The `MedRecPro_Views.sql` file is particularly important as the navigation view queries (ingredient search, labeler search, pharmacologic class hierarchy, etc.) are defined there and power many of the API search endpoints. The adverse-event dashboard is backed by the `dbo.vw_AeRisk`, `dbo.vw_AeDrugSummary`, and `dbo.vw_AeDashboardProductCatalog` views (also in `MedRecPro_Views.sql`), the last two materialized into `tmp_` tables by the Stage 5 pipeline so the dashboard reads without runtime statistics.

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

## Adverse Event Risk Dashboard

The platform turns the free-text adverse-event tables buried in SPL labels into comparable risk statistics and exposes them as an interactive dashboard at `/adverse-events`.

The data flow spans three projects:

1. **MedRecProImportClass / MedRecProConsole** — the SPL table-standardization pipeline parses heterogeneous label tables into a uniform analytical schema and, in Stage 5, pre-computes Relative Risk (RR), Dose-Normalized RR (DNRR), and 95% confidence intervals per adverse-event row. Results are materialized into the `tmp_FlattenedAdverseEvent*` tables. See the [MedRecProImportClass README](MedRecProImportClass/README.md) for the full pipeline and the statistical contract.
2. **MedRecPro (API)** — `AdverseEventController` (`/api/AdverseEvent`) serves dashboard-ready, encrypted-ID payloads from the materialized risk views: per-product triage/forest/quadrant, symptom reverse lookup, comparator-aware two-product therapeutic interchange, pharmacologic-class SOC × SOC correlation maps/heatmaps/cell drill-downs, and MedDRA-system-scoped class × class / class × drug correlation views. The feature is gated by `FeatureFlags:AeDashboard:Enabled`.
3. **MedRecProReact** — a React + Vite single-page island that renders the product, class, and By System dashboard focuses. Its build output is committed into `MedRecProStatic/wwwroot/ae-dashboard` and served by MedRecProStatic. See the [MedRecProReact README](MedRecProReact/README.md).

Because the observation units for correlation views are intentionally narrow — a single drug within a class for SOC × SOC maps, or shared selected-system terms for By System class-pair maps — sample sizes can be small. The dashboard is deliberately honesty-first: below-floor cells are suppressed rather than fabricated, non-renderable system matrices return warnings, the By System focus is single-system by design, and comparator-mixed payloads are explicitly flagged. The displayed figures are bounded by what each label discloses and what the parser can extract — absence of a signal is not evidence of its absence in practice.

## FDA Orange Book Integration

The platform imports and cross-references data from the FDA's [Approved Drug Products with Therapeutic Equivalence Evaluations (Orange Book)](https://www.fda.gov/drugs/drug-approvals-and-databases/approved-drug-products-therapeutic-equivalence-evaluations-orange-book), linking FDA approval records to existing SPL label data.

### Orange Book Database Schema

Seven normalized tables store Orange Book data, with three junction tables linking to existing SPL entities:

| Table | Purpose |
|---|---|
| `OrangeBookApplicant` | Pharmaceutical companies holding FDA approvals |
| `OrangeBookProduct` | Drug products (natural key: ApplType + ApplNo + ProductNo) |
| `OrangeBookPatent` | Patent records per product with expiration dates |
| `OrangeBookExclusivity` | Marketing exclusivity periods (NCE, ODE, RTO, etc.) |
| `OrangeBookProductMarketingCategory` | Junction: OB Product → SPL MarketingCategory (by application number) |
| `OrangeBookProductIngredientSubstance` | Junction: OB Product → SPL IngredientSubstance |
| `OrangeBookApplicantOrganization` | Junction: OB Applicant → SPL Organization |

No foreign key constraints are enforced; relationships are managed by the import module. The main nuke script (`MedRecPro-AzureNuke.sql`) excludes Orange Book tables, which have their own dedicated truncation script.

### Orange Book Import Process

The console application (`MedRecProConsole`) imports Orange Book data from FDA-published ZIP archives containing tilde-delimited text files. The import pipeline:

1. **Extract** `products.txt` from the ZIP archive
2. **Parse** tilde-delimited rows (14 columns) into normalized entities
3. **Upsert** applicants and products in batches (5,000 rows per batch)
4. **Match applicants to SPL organizations** using a two-tier strategy:
   - **Tier 1 (Normalized Exact)**: Strips corporate suffixes (INC, LLC, CORP, LTD, GMBH, etc.), punctuation, and whitespace; case-insensitive exact match
   - **Tier 2 (Token Similarity)**: Two-pass fuzzy matching using Jaccard/containment scoring with a 0.67 threshold; strips pharma noise words on the second pass only when sufficient tokens remain
5. **Match ingredients** to SPL IngredientSubstance records (semicolon-delimited ingredient lists parsed and matched individually)
6. **Match products to SPL MarketingCategory** records via application number

The import is idempotent (upsert-based), so re-running is safe without crash recovery queues. Real-time multi-phase progress is displayed via Spectre.Console.

### Orange Book CLI Usage

**Interactive mode:**

```bash
cd MedRecProConsole
dotnet run
# Select "orange-book" or "ob" from the menu
```

**Unattended mode:**

```bash
MedRecProConsole.exe --orange-book "path/to/EOBZIP_2026_01.zip" --nuke --auto-quit
```

| Argument | Description |
|---|---|
| `--orange-book <path>` | Path to Orange Book ZIP file |
| `--nuke` | Truncate all Orange Book tables before import |
| `--connection <name>` | Database connection name from appsettings.json |
| `--auto-quit` | Exit after completion |
| `--verbose` | Enable debug logging |

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

# Terminal 4: Adverse-event dashboard (Vite dev server, port 50346, optional)
cd MedRecProReact
npm install
npm run dev
```

The MVC-hosted dashboard at `http://localhost:5001/adverse-events` is served from the committed Vite bundle; the Vite dev server above is only for iterating on the React source. After changing React source, run `npm run build` and commit the regenerated `MedRecProStatic/wwwroot/ae-dashboard` assets (the .NET build does not run Vite).

### API diagnostic baseline

Use the Chat page test commands to exercise the browser-facing endpoint diagnostic. In local Debug, run the API at `http://localhost:5093` and the static site at `http://localhost:5001`; when deployed, the diagnostic uses the same-origin `/api` path.

| Command | Purpose |
|---|---|
| `/test api all` | Run the non-destructive baseline, including authenticated reads when signed in. |
| `/test api safe` | Run the complete safe anonymous profile; use a private window when the current browser is signed in. |
| `/test api read-auth` | Run authenticated read coverage without writes. |
| `/test api report` | Show the redacted summary for the latest API diagnostic. |

On a deployed site, safe commands use five-second inter-request pacing, suppress in-memory conversation lifecycle requests, and skip deliberate 404 probes unless an operator attests that tarpit mode is disabled. The baseline never starts paid-AI, import, logout, or mutation profiles; those profiles remain local-only and blocked until their exact confirmations are entered in the endpoint panel. OAuth provider initiation is verified manually through the normal login flow or Swagger rather than through the browser baseline.

The panel's `complete` count is the number of scheduled diagnostic records, not the number of API routes. `Accounted: 120` is the audited inventory of unique HTTP-method-and-route operations. One operation can have several records, for example a successful read, an invalid-input contract check, and an authorization check; a few records are internal manifest or seed checks. `Invoked` counts records that issued a request, while `Positive contracts` counts records that verified an expected successful contract. The current Debug acceptance baseline completed with 154 PASS, 0 FAIL, and 15 intentional SKIP across 169 diagnostic records; it retained all 120 operations in the inventory.

### 5. Import data

Upload SPL ZIP files through the API import endpoint or use the console importer:

```bash
cd MedRecProConsole
dotnet run -- --help
```

SPL ZIP files can be downloaded from the [DailyMed SPL Resources](https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm) page.

Orange Book ZIP files can be downloaded from the [FDA Orange Book Data Files](https://www.fda.gov/drugs/drug-approvals-and-databases/approved-drug-products-therapeutic-equivalence-evaluations-orange-book) page. See the [FDA Orange Book Integration](#fda-orange-book-integration) section for import details.

### 6. Run verification

The repository-level runner provides the same named gates used by CI:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-MedRecProVerification.ps1 -Gate Fast
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-MedRecProVerification.ps1 -Gate DebugContract
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-MedRecProVerification.ps1 -Gate ReleaseContract
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-MedRecProVerification.ps1 -Gate Full
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-MedRecProVerification.ps1 -Gate All
```

- `Fast` runs dependency, reflection, public-surface, and route guards for ordinary changes.
- `DebugContract` runs the reviewed 27-case Label contract lane in Debug: the original 24 public-contract/startup cases plus three progress-response contract cases, excluding isolated OpenAPI filter units.
- `ReleaseContract` separately compiles the same 27-case contract lane in Release under `MedRecPro/.codex-build/test-contract-release`.
- `Full` builds the solution, runs every MSTest test, performs deterministic-test/source inventories, and checks the diff.
- `All` executes every gate in order and is the phase/merge-boundary command used by the `MedRecPro Verification` workflow.

See [MedRecPro verification gates](MedRecPro/docs/verification-gates.md) for gate boundaries and CI behavior.

## Setup Pitfalls and Fixes

### IIS Virtual Application Path Stripping

IIS strips the virtual application prefix from requests before forwarding to ASP.NET Core. A request to `/api/Label/search` arrives at Kestrel as `/Label/search`. API controllers inherit the conditional route from `ApiControllerBase`:

```csharp
#if DEBUG
[Route("api/[controller]")]   // Local: full path
#else
[Route("[controller]")]        // Azure: IIS strips /api prefix
#endif
```

Split controllers do not declare type-level routes. `FeatureControllerNameConvention` resolves each annotated controller's `[controller]` token from `FeatureControllerNameAttribute`; the current Label feature controllers use `"Label"`, preserving the same 51 Debug and Release operations. `SwaggerGroupAttribute` independently controls documentation grouping without participating in routing. The same virtual-application consideration applies to MCP routes and Swagger paths.

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

Azure SQL Serverless auto-pauses after inactivity. Resuming takes 30-60 seconds, which can exceed default timeouts. Mitigations:
- **`DatabaseKeepAliveService`** pings the database with `SELECT 1` every 14 minutes during business hours (Mon-Fri, 8 AM - 8 PM Eastern) to prevent auto-pause. Each ping cycle includes 3 retry attempts with escalating delays (10s, 30s, 60s) and a 90-second connect timeout to accommodate cold resume. Configured via the `DatabaseKeepAlive` section in `appsettings.json`
- **`EnableRetryOnFailure()`** is configured on the EF Core `DbContext` with 3 retries and a 60-second command timeout to handle transient failures across all application database operations
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

1. Run `Invoke-MedRecProVerification.ps1 -Gate All`
2. Publish each project to its virtual application path on Azure App Service
3. Verify Azure Key Vault secrets are configured
4. Purge Cloudflare cache after deployment
5. Test authentication flows (Google and Microsoft OAuth)
6. Verify Swagger UI loads at `/api/swagger/index.html`
7. Verify MCP health check at `/mcp/health`
8. Test AI chat at the static site

## License

See the [LICENSE.txt](MedRecPro/LICENSE.txt) file for details.
