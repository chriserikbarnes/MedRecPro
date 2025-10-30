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
  "Authentication:Microsoft:ClientSecret:Dev": your-microsoft-client-secret-here",
  "Authentication:Microsoft:ClientSecret:Prod": "your-microsoft-client-secret-here",
  "Authentication:Microsoft:TenantId": "your-microsoft-tenant-here",

  "Security:DB:PKSecret": "your-encryption-key-here", //changing this will break urls/favorites/bookmarks/links user's have created
 
  "Jwt:Key": "your-super-strong-key",
  "Jwt:Issuer": "MedRecPro",
  "Jwt:Audience": "MedRecUsers",
  "Jwt:ExpirationMinutes": 60,

  "ClaudeApiSettings:ApiKey": "your-claude-api-key-here",

  "Dev:DB:Connection": Server=localhost;Database=your-database;User Id=your-user;Password=your-password-here;",
  "Prod:DB:Connection": "Server=tcp:yourdb.database.windows.net,9999;Initial Catalog=yourdb;Persist Security Info=False;User ID=your-admin;Password=your-password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
}
```


## Production Deployment (Azure + Cloudflare)

### Overview

MedRecPro is deployed on Azure App Service with Cloudflare as the CDN/DNS provider. This section documents the complete setup process and common pitfalls.

### Infrastructure Setup

#### 1. Custom Domain Configuration

**Cloudflare DNS Setup:**
1. Add A records pointing to Azure App Service IP address (20.0.0.1) your root domain:
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


## License

See the [LICENSE.txt](LICENSE.txt) file for details.
