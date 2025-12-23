# MedRecPro

MedRecPro is a structured product label management system built with ASP.NET Core, providing secure access to data through a RESTful API.

## Specifications

- **HL7 Version**: HL7 Dec 2023 https://www.fda.gov/media/84201/download
- **Info**: https://www.fda.gov/industry/fda-data-standards-advisory-board/structured-product-labeling-resources
- **Data Source**: https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm

## Features

- **User Authentication**: Secure authentication using ASP.NET Identity with support for external providers
- **Document Management**: Full CRUD operations for medical labels based on SPL (Structured Product Labeling) standards
- **Data Security**: Encrypted user identifiers and sensitive information
- **Role-based Access Control**: Granular permissions system for different user roles
- **API Documentation**: Swagger/OpenAPI integration

## Technology Stack

- **Backend**: ASP.NET Core (.NET 6+)
- **Database**: SQL Server with Entity Framework Core
- **Authentication**: Cookie-based authentication with external provider support
- **API Documentation**: Swagger/OpenAPI
- **Security**: 
  - Identity framework for authentication
  - Encryption mechanisms for sensitive data
  - Bearer token authentication for API endpoints

## API Endpoints

### Authentication
- `GET /api/auth/login/{provider}` - Initiates login with external provider
- `GET /api/auth/external-logincallback` - Callback for external authentication
- `GET /api/auth/user` - Retrieves current user info
- `POST /api/auth/logout` - Logs out the current user

### Users
- `GET /api/users/GetUser/{encryptedUserId}` - Retrieves user information
- `POST /api/users/CreateUser` - Creates a new user

### Labels
- `GET /api/label` - Retrieves all labels
- `GET /api/label/{encryptedId}` - Retrieves a specific item
- `POST /api/label` - Creates a new item
- `PUT /api/label/{encryptedId}` - Updates an existing item
- `DELETE /api/label/{encryptedId}` - Deletes a item

## Database Schema

The database includes tables for:

- Users and authentication
- SPL labels and metadata
- Organizations and contacts
- Relationships between entities

## Getting Started

1. Clone the repository
2. Configure your database connection in `appsettings.json`
3. Run database migrations:
   ```
   dotnet ef database update
   ```
4. Run the application:
   ```
   dotnet run
   ```
5. Access the API at `https://localhost:5001` or as configured

## Security Configuration

Security settings including encryption keys should be configured in your user secrets or environment variables:

```json
{
  "Authentication:Google:ClientSecret": "your-google-client-secret-here",
  "Authentication:Google:ClientId": "your-google-client-id-here.apps.googleusercontent.com",

  "Authentication:Microsoft:ClientId": "your-microsoft-client/application-id-here",
  "Authentication:Microsoft:ClientSecret:Dev": "your-microsoft-client-secret-here",
  "Authentication:Microsoft:ClientSecret:Prod": "your-microsoft-client-secret-here",
  "Authentication:Microsoft:TenantId": "your-microsoft-tenant-here",

  "Security:DB:PKSecret": "your-encryption-key-here", //changing this will break urls/favorites/bookmarks/links user's have created
 
  "Jwt:Key": "your-super-strong-key",
  "Jwt:Issuer": "MedRecPro",
  "Jwt:Audience": "MedRecUsers",
  "Jwt:ExpirationMinutes": 60,

  "ClaudeApiSettings:ApiKey": "your-claude-api-key-here",

  "Dev:DB:Connection": "Server=localhost;Database=your-database;User Id=your-user;Password=your-password-here;",
  "Prod:DB:Connection": "Server=tcp:yourdb.database.windows.net,9999;Initial Catalog=yourdb;Persist Security Info=False;User ID=your-admin;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

  "AzureAd:Domain": "your-domain.onmicrosoft.com",
  "Azure:SqlDatabase:ResourceId": "/subscriptions/your-azure-subscription-id/resourceGroups/your-group/providers/Microsoft.Sql/servers/your-server-sql/databases/your-database"
}
```


## Production Deployment (Azure + Cloudflare)

### Overview

MedRecPro is deployed on Azure App Service with Cloudflare as the CDN/DNS provider. This section documents the complete setup process and common pitfalls.

### Infrastructure Setup

#### 1. Custom Domain Configuration

**Cloudflare DNS Setup:**
1. Add A records pointing to Azure App Service IP address ({20.0.0.1} your IP) root domain:
   - **Type**: A
   - **Name**: @
   - **IPv4 address**:
2. Create CNAME record for www subdomain:
   - **Type**: CNAME
   - **Name**: www
   - **Target**: your-domain.com
   - **Proxy status**: Proxied (orange cloud)

**Azure Custom Domains:**
1. Navigate to App Service → Custom domains
2. Add both domains:
   - your-domain.com
   - www.your-domain.com
3. Validate domain ownership via CNAME record

**⚠️ Common Pitfall:** Azure requires both the root domain AND www subdomain to be explicitly added as custom domains, even if DNS is configured correctly.

---

#### 2. SSL/TLS Configuration

**Cloudflare SSL Settings:**
1. Set encryption mode to **Full (strict)**
   - Navigate to SSL/TLS → Overview
   - Select "Full (strict)" for end-to-end encryption

**Cloudflare Origin Certificates:**
1. Create Origin Certificate:
   - SSL/TLS → Origin Server → Create Certificate
   - Hostnames: `your-domain.com` and `*.your-domain.com`
   - Validity: 15 years
   - Download both certificate and private key

2. Convert to PFX format:
   ```bash
   openssl pkcs12 -export -out cloudflare-origin.pfx -inkey private-key.pem -in certificate.pem
   ```

**Azure Certificate Binding:**
1. Upload PFX certificate:
   - App Service → Certificates → Bring your own certificates
   - Upload the PFX file with password
   
2. Bind to custom domains:
   - App Service → Custom domains
   - Click each domain → Add binding
   - Select uploaded certificate
   - TLS/SSL type: SNI SSL

**⚠️ Common Pitfall:** Azure App Service Managed Certificates don't work with Cloudflare proxy enabled. Must use Cloudflare Origin Certificates instead.

---

#### 3. OAuth Authentication Setup

**Google Cloud Console:**
1. Create OAuth 2.0 Client ID for web application
2. Configure authorized JavaScript origins:
   ```
   https://your-domain.com
   https://www.your-domain.com
   https://your-app.azurewebsites.net
   http://localhost:5093 (development)
   ```

3. Configure authorized redirect URIs:
   ```
   https://your-domain.com/api/signin-google
   https://www.your-domain.com/api/signin-google
   https://your-app.azurewebsites.net/api/signin-google
   http://localhost:5093/signin-google (development)
   ```

**Microsoft Entra ID (Azure AD):**
1. Create App Registration:
   - Supported account types: Multi-tenant and personal Microsoft accounts
   - Redirect URIs (Web platform):
     ```
     https://your-domain.com/api/signin-microsoft
     https://www.your-domain.com/api/signin-microsoft
     https://your-app.azurewebsites.net/api/signin-microsoft
     http://localhost:5093/signin-microsoft (development)
     ```

2. API Permissions required:
   - Microsoft Graph → User.Read (Delegated)
   - OpenID permissions: openid, profile, email

3. Create Client Secret:
   - Certificates & secrets → New client secret
   - Store securely in Azure Key Vault

**⚠️ Critical Pitfalls:**

1. **Redirect URI Path Prefix**: Production URIs must include `/api/` prefix due to application routing:
   - ✅ Correct: `https://your-domain.com/api/signin-google`
   - ❌ Wrong: `https://your-domain.com/signin-google`

2. **Key Vault Secret Format**: Ensure no extra characters in secrets:
   - ✅ Correct: `c5a76e9f-dce0-499d-94cd-121eea3a2d34`
   - ❌ Wrong: `c5a76e9f-dce0-499d-94cd-121eea3a2d34",` (extra quote and comma)
   
3. **Authentication Code Configuration**: Use compiler directives for environment-specific paths:
   ```csharp
   #if DEBUG
   var swaggerPath = "/swagger/index.html";  // Local development
   #else
   var swaggerPath = "/api/swagger/index.html";  // Azure production
   #endif
   ```

---

#### 4. Azure Key Vault Configuration

**Store Secrets in Key Vault:**
1. Navigate to Azure Key Vault → Secrets
2. Add secrets with proper naming (use `--` as separator):
   ```
   Authentication--Google--ClientId
   Authentication--Google--ClientSecret
   Authentication--Microsoft--ClientId
   Authentication--Microsoft--ClientSecret--Prod
   Authentication--Microsoft--TenantId
   Security--DB--PKSecret
   ClaudeApiSettings--ApiKey
   ConnectionStrings--DefaultConnection
   Jwt--Key
   ```

**Reference in App Service:**
1. Navigate to App Service → Environment variables
2. Add Key Vault references:
   ```
   Name: Authentication:Google:ClientId
   Value: @Microsoft.KeyVault(VaultName=medrecprovault;SecretName=Authentication--Google--ClientId)
   ```

**⚠️ Important Notes:**
- Key Vault uses `--` (double dash) as separator
- App Service configuration uses `:` (colon) as separator
- App Service automatically maps colons to double dashes when reading from Key Vault

---

#### 5. Cloudflare Cache Management

**Cache Purging:**
After each deployment, purge Cloudflare cache to prevent serving stale static files:

1. Cloudflare Dashboard → Caching → Configuration
2. Use one of:
   - **Purge Everything** (simplest for deployments)
   - **Custom Purge** with specific URLs:
     ```
     https://www.your-domain.com/api/swagger/swagger-ui-bundle.js
     https://www.your-domain.com/api/swagger/swagger-ui-standalone-preset.js
     https://www.your-domain.com/api/swagger/index.html
     ```

**⚠️ Critical Issue:** Cloudflare caches files with incorrect content-types during errors. Always purge cache after deployment to prevent JavaScript files being served as HTML.

**Deployment Checklist:**
1. ✅ Deploy to Azure
2. ✅ Verify deployment completed
3. ✅ Purge Cloudflare cache
4. ✅ Test authentication flows
5. ✅ Verify Swagger UI loads correctly

---

### Environment-Specific Configuration

**Development (Local):**
- Uses appsettings.Development.json
- Authentication redirects to `/swagger/index.html`
- Secrets stored in User Secrets

**Production (Azure):**
- Uses appsettings.json + Azure App Settings
- Authentication redirects to `/api/swagger/index.html`
- Secrets stored in Azure Key Vault
- SSL via Cloudflare Origin Certificates

### Testing Production Authentication

**Test URLs:**
```
https://www.your-domain.com/api/auth/login/Google
https://www.your-domain.com/api/auth/login/Microsoft
```

**Verify User Info:**
```
GET https://www.your-domain.com/api/auth/user
```

Expected response:
```json
{
  "encryptedUserId": "...",
  "name": "user@example.com",
  "claims": [...]
}
```

### Troubleshooting

**Issue: "redirect_uri_mismatch" error**
- Solution: Verify redirect URIs include `/api/` prefix for production
- Check both Google Cloud Console and Microsoft Entra ID configurations

**Issue: "unauthorized_client" error (Microsoft)**
- Solution: Check supported account types (should be Multi-tenant)
- Verify Client ID has no extra characters in Key Vault

**Issue: Swagger UI shows JavaScript errors**
- Solution: Purge Cloudflare cache
- Verify static files are being served with correct content-type

**Issue: SSL certificate errors**
- Solution: Verify both domains are bound to certificates in Azure
- Check Cloudflare SSL mode is "Full (strict)"

**Issue: Authentication succeeds but redirects to wrong page**
- Solution: Check `AuthController.cs` uses correct path based on build configuration
- Verify `#if DEBUG` / `#else` directives are properly set

## Azure SQL Free Tier Monitoring

MedRecPro includes built-in monitoring for Azure SQL Database's serverless free tier, enabling the application to track vCore consumption and implement intelligent throttling to stay within budget.

### Overview

Azure SQL Database's serverless tier includes a monthly free allowance of **100,000 vCore seconds**. The `AzureSqlMetricsService` queries Azure Monitor Metrics to track consumption in real-time, enabling:

- Dashboard display of current free tier usage
- Projected monthly cost estimates
- Automatic throttling when approaching budget limits
- Cost optimization through usage awareness

### Configuration

Add the following to your `appsettings.json`:

```json
{
  "Azure": {
    "SqlDatabase": {
      "ResourceId": "/subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Sql/servers/{server-name}/databases/{database-name}",
      "MetricsRegion": "eastus"
    }
  }
}
```

| Setting | Description |
|---------|-------------|
| `ResourceId` | Full Azure Resource Manager path to your SQL database |
| `MetricsRegion` | Azure region for the metrics endpoint (e.g., `eastus`, `westus3`) |

### Prerequisites

**NuGet Packages:**
```bash
dotnet add package Azure.Monitor.Query.Metrics
dotnet add package Azure.Identity
```

**Azure RBAC Requirements:**

The application's managed identity (or service principal) requires the **Monitoring Reader** role on the SQL database resource to query metrics.

### Service Registration

```csharp
// Program.cs
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<AzureManagementTokenProvider>();
builder.Services.AddSingleton<AzureSqlMetricsService>();
```

### Usage Examples

**Get Current Free Tier Status:**
```csharp
var (used, remaining, percentUsed) = await _metricsService.GetFreeTierStatusAsync();

// Example output:
// Used: 45,230 vCore seconds
// Remaining: 54,770 vCore seconds  
// Percent Used: 45.2%
```

**Project Monthly Costs:**
```csharp
var (projectedUsage, projectedCost, daysElapsed) = await _metricsService.GetProjectedMonthlyCostAsync();

// Example output after 15 days:
// Projected Usage: 90,460 vCore seconds
// Projected Cost: $0.00 (within free tier)
// Days Elapsed: 15
```

**Check If Throttling Needed:**
```csharp
var (shouldThrottle, level, percent) = await _metricsService.ShouldThrottleAsync();

switch (level)
{
    case "Aggressive":  // >90% used
        // Block expensive operations
        break;
    case "Warning":     // 80-90% used
        // Rate limit heavy queries
        break;
    case "None":        // <80% used
        // Normal operation
        break;
}
```

### Key Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetFreeTierStatusAsync()` | `(used, remaining, percentUsed)` | Current consumption status |
| `GetUsedVCoreSecondsThisMonthAsync()` | `double` | Total vCore seconds used this month |
| `GetRemainingFreeTierVCoreSecondsAsync()` | `double` | vCore seconds remaining in free tier |
| `GetProjectedMonthlyCostAsync()` | `(projected, cost, days)` | Estimated end-of-month usage and cost |
| `ShouldThrottleAsync()` | `(shouldThrottle, level, percent)` | Throttling recommendation |

### Architecture Notes

The service uses a dual-query strategy for reliability:

1. **Primary:** REST API call to `management.azure.com` for `free_amount_remaining` metric
2. **Fallback:** Azure Monitor Query SDK (`MetricsClient`) if REST returns no data

Results are cached for 5 minutes to minimize API overhead. The service queries 15-minute granularity buckets and uses the minimum remaining value to ensure accurate tracking of consumption.

### Cost Reference

| Metric                   | Value                          |
|--------------------------|--------------------------------|
| Monthly Free Allowance   | 100,000 vCore seconds          |
| Overage Rate             | ~$0.000145 per vCore second    |
| Break-even (100% usage)  | $0.00/month                    |
| 2x free tier usage       | ~$14.50/month                  |


# MedRecPro AI Skills System

## Overview

This package implements an agentic AI layer for the MedRecPro pharmaceutical labeling management system. It enables natural language interaction with the system, allowing users to query pharmaceutical data, manage SPL documents, and perform system operations through conversational requests processed by Claude AI.

## Architecture

The system implements a **request-interpret-execute-synthesize** pattern:

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Client Application                         │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                    1. User submits natural language query
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    POST /api/ai/interpret                           │
│                         AiController                                │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                    2. Claude interprets → returns API specs
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    AiAgentInterpretation                            │
│   { endpoints: [{ method, path, queryParameters, ... }] }           │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                    3. Client executes specified endpoints
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    MedRecPro API Endpoints                          │
│         /api/views/..., /api/labels/..., etc.                       │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                    4. Client sends results back
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    POST /api/ai/synthesize                          │
│                         AiController                                │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                    5. Claude synthesizes → human-readable response
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    AiAgentSynthesis                                 │
│   { response: "I found 47 products...", suggestedFollowUps: [...] } │
└─────────────────────────────────────────────────────────────────────┘
```

## Files Included

### Core Service Files

| File                 | Description                                                    |
|----------------------|----------------------------------------------------------------|
| `AiAgentDtos.cs`     | Data transfer objects for requests/responses                   |
| `AiController.cs`    | API controller exposing AI endpoints                           |

### Skills Documentation

| File                    | Description                                                  |
|------|---------------------------------------------------------------------------------|
| `SKILLS.md`             | Human-readable markdown documentation of available endpoints |
| `medrecpro-skills.json` | Structured JSON skills document for AI interpretation        |


## API Endpoints

### GET /api/ai/context
Returns current system state (authentication, demo mode, data counts).

### POST /api/ai/interpret
Interprets natural language query and returns API endpoint specifications.

**Request:**
```json
{
  "userMessage": "Find all products containing aspirin",
  "conversationId": "conv-123"
}
```

**Response:**
```json
{
  "success": true,
  "endpoints": [{
    "method": "GET",
    "path": "/api/views/ingredient/search",
    "queryParameters": { "substanceNameSearch": "aspirin" },
    "description": "Search products by ingredient name"
  }],
  "explanation": "I'll search for products containing aspirin."
}
```

### POST /api/ai/synthesize
Synthesizes API execution results into human-readable response.

**Request:**
```json
{
  "originalQuery": "Find all products containing aspirin",
  "executedEndpoints": [{
    "specification": { "method": "GET", "path": "/api/views/ingredient/search", "..." },
    "statusCode": 200,
    "result": [{ "ProductName": "Bayer Aspirin", "Next Product" }]
  }]
}
```

**Response:**
```json
{
  "response": "I found 47 products containing aspirin...",
  "dataHighlights": { "totalProducts": 47 },
  "suggestedFollowUps": ["Show details for Bayer Aspirin"]
}
```

### GET /api/ai/skills
Returns the skills document describing available API capabilities.

### GET /api/ai/chat?message={query}
Convenience endpoint for simple queries (calls interpret internally).

## Key Features

### 1. System Context Awareness
The AI agent checks:
- Authentication status (determines available operations)
- Demo mode state (warns about data persistence)
- Database population (suggests import if empty)
- Available capabilities

### 2. Demo Mode Detection
When `isDatabaseEmpty == true`, the agent suggests importing SPL data from DailyMed.

### 3. Authentication Enforcement
Write operations are flagged as requiring authentication. If user is not authenticated, the interpretation includes appropriate guidance.

### 4. Comprehensive Skills Document
The skills document includes:
- All navigation view endpoints
- CRUD operations for all label sections
- Import/export capabilities
- Common LOINC section codes
- Trigger phrases for intent matching

## Example Conversations

### Query: "Find all drugs manufactured by Pfizer"
```json
// Interpretation
{
  "endpoints": [{
    "method": "GET",
    "path": "/api/views/labeler/search",
    "queryParameters": { "labelerNameSearch": "Pfizer" }
  }]
}

// Synthesis (after execution)
{
  "response": "I found 47 products manufactured by Pfizer Inc. Notable products include LIPITOR, VIAGRA, and ZOLOFT...",
  "suggestedFollowUps": ["Show details for LIPITOR"]
}
```

### Query: "Import some SPL data" (not authenticated)
```json
{
  "success": false,
  "requiresAuthentication": true,
  "error": "This operation requires authentication. Please log in first."
}
```

### Query: "What can you do?" (empty database)
```json
{
  "isDirectResponse": true,
  "directResponse": "The database is currently empty. To get started, import SPL ZIP files from DailyMed..."
}
```

## Notes

1. **Security**: The client executes API calls, preserving authentication context. The AI only returns specifications.

2. **Encrypted IDs**: All IDs in responses are encrypted. Use these values in subsequent requests.

3. **Error Handling**: Both interpretation and synthesis include fallback responses when Claude API fails.

4. **Caching**: Skills document is cached for 1 hour to minimize overhead.


## License

See the [LICENSE.txt](LICENSE.txt) file for details.
