# MedRecPro MCP Server Deployment Guide

This guide covers deploying the MedRecPro MCP Server to Azure App Service behind Cloudflare.

## Architecture Overview

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Claude / MCP   │────▶│   Cloudflare    │────▶│  Azure App Svc  │
│     Client      │     │   (CDN/WAF)     │     │   MCP Server    │
└─────────────────┘     └─────────────────┘     └────────┬────────┘
                                                         │
                        ┌─────────────────┐              │
                        │   Cloudflare    │◀─────────────┘
                        │   (CDN/WAF)     │
                        └────────┬────────┘
                                 │
                        ┌────────▼────────┐
                        │  Azure App Svc  │
                        │  MedRecPro API  │
                        └─────────────────┘
```

## Prerequisites

1. Azure subscription with App Service plan
2. Cloudflare account with domain configured
3. Azure Key Vault for secrets
4. Google Cloud Console project with OAuth configured
5. Azure/Entra ID app registration for Microsoft OAuth

---

## 1. Azure App Service Setup

### Create App Service

```bash
# Variables
RESOURCE_GROUP="MedRecProResources"
LOCATION="eastus"
APP_SERVICE_PLAN="MedRecProPlan"
MCP_APP_NAME="mcp-medrecpro"

# Create resource group (if needed)
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create App Service Plan (Linux, B1 for dev, P1V3+ for production)
az appservice plan create \
  --name $APP_SERVICE_PLAN \
  --resource-group $RESOURCE_GROUP \
  --sku P1V3 \
  --is-linux

# Create Web App
az webapp create \
  --name $MCP_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --runtime "DOTNETCORE:8.0"

# Enable managed identity for Key Vault access
az webapp identity assign \
  --name $MCP_APP_NAME \
  --resource-group $RESOURCE_GROUP
```

### Configure App Settings

```bash
# Configure app settings
az webapp config appsettings set \
  --name $MCP_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    "McpServer__ServerUrl=https://mcp.medrecpro.com" \
    "MedRecProApi__BaseUrl=https://www.medrecpro.com/api" \
    "KeyVaultUrl=https://your-keyvault.vault.azure.net/"
```

### Configure for SSE/Streaming

MCP uses Server-Sent Events which require specific configuration:

```bash
# Disable ARR affinity (not needed for stateless mode)
az webapp config set \
  --name $MCP_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --generic-configurations '{"alwaysOn": true, "clientAffinityEnabled": false}'

# Set request timeout for long-running connections
az webapp config appsettings set \
  --name $MCP_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    "WEBSITE_WEBDEPLOY_USE_SCM=false" \
    "SCM_COMMAND_IDLE_TIMEOUT=3600"
```

---

## 2. Azure Key Vault Configuration

### Grant Access to App Service

```bash
KEYVAULT_NAME="your-keyvault"
APP_PRINCIPAL_ID=$(az webapp identity show \
  --name $MCP_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId -o tsv)

az keyvault set-policy \
  --name $KEYVAULT_NAME \
  --object-id $APP_PRINCIPAL_ID \
  --secret-permissions get list
```

### Add Required Secrets

Add these secrets to Key Vault (using Azure Portal or CLI):

| Secret Name | Description |
|-------------|-------------|
| `Jwt--SigningKey` | JWT signing key (min 32 chars) |
| `Jwt--UpstreamTokenEncryptionKey` | AES encryption key for upstream tokens |
| `OAuth--Providers--Google--ClientId` | Google OAuth Client ID |
| `OAuth--Providers--Google--ClientSecret` | Google OAuth Client Secret |
| `OAuth--Providers--Microsoft--ClientId` | Microsoft/Entra App ID |
| `OAuth--Providers--Microsoft--ClientSecret` | Microsoft/Entra Client Secret |

```bash
# Example: Add JWT signing key
az keyvault secret set \
  --vault-name $KEYVAULT_NAME \
  --name "Jwt--SigningKey" \
  --value "YourSuperSecureSigningKeyThatIsAtLeast32CharactersLong"
```

---

## 3. Cloudflare Configuration

### DNS Records

Add DNS records in Cloudflare dashboard:

| Type | Name | Content | Proxy Status |
|------|------|---------|--------------|
| CNAME | mcp | mcp-medrecpro.azurewebsites.net | Proxied (orange cloud) |

### SSL/TLS Settings

1. Go to **SSL/TLS** → **Overview**
2. Set encryption mode to **Full (strict)**
3. Enable **Always Use HTTPS**

### Origin Certificates (Azure App Service)

1. In Cloudflare, go to **SSL/TLS** → **Origin Server**
2. Create an **Origin Certificate** for `mcp.medrecpro.com`
3. Download the certificate and private key
4. Upload to Azure App Service:
   - Convert to PFX format: `openssl pkcs12 -export -out cert.pfx -inkey key.pem -in cert.pem`
   - In Azure Portal, go to App Service → **TLS/SSL settings** → **Private Key Certificates**
   - Upload the PFX file
   - Add custom domain binding with SNI SSL

### Cache Rules

Create a cache rule to bypass caching for MCP/OAuth endpoints:

**Rule 1: Bypass cache for MCP endpoint**
- **When**: Hostname equals `mcp.medrecpro.com` AND URI Path starts with `/mcp`
- **Then**:
  - Cache eligibility: Bypass cache
  - Disable Apps (optional)

**Rule 2: Bypass cache for OAuth endpoints**
- **When**: Hostname equals `mcp.medrecpro.com` AND URI Path starts with `/oauth`
- **Then**:
  - Cache eligibility: Bypass cache

### Page Rules (Legacy)

If using Page Rules instead of Cache Rules:

```
URL Pattern: mcp.medrecpro.com/mcp*
Settings:
  - Cache Level: Bypass
  - Disable Apps: On
  - Disable Performance: On

URL Pattern: mcp.medrecpro.com/oauth/*
Settings:
  - Cache Level: Bypass
```

### WAF Configuration

Configure Web Application Firewall to allow OAuth flows:

1. Go to **Security** → **WAF**
2. Create a custom rule to skip rate limiting for OAuth endpoints if needed
3. Ensure the rule allows POST to `/oauth/token` and `/oauth/register`

### Response Buffering (SSE)

Cloudflare may buffer Server-Sent Events. To prevent this:

1. **Transform Rules** → **Modify Response Header**
2. Create rule:
   - **When**: Hostname equals `mcp.medrecpro.com` AND URI Path starts with `/mcp`
   - **Then**: Set header `X-Accel-Buffering` = `no`

Alternatively, the application sets the `X-Accel-Buffering: no` header programmatically.

---

## 4. OAuth Provider Configuration

### Google Cloud Console

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Select your project or create a new one
3. Navigate to **APIs & Services** → **Credentials**
4. Create or edit an **OAuth 2.0 Client ID**
5. Add authorized redirect URIs:
   - `https://mcp.medrecpro.com/oauth/callback/google`
   - `http://localhost:7100/oauth/callback/google` (for development)

### Microsoft Entra ID (Azure AD)

1. Go to [Azure Portal](https://portal.azure.com/)
2. Navigate to **Microsoft Entra ID** → **App registrations**
3. Select your app or create a new one
4. Under **Authentication**, add redirect URIs:
   - `https://mcp.medrecpro.com/oauth/callback/microsoft`
   - `http://localhost:7100/oauth/callback/microsoft` (for development)
5. Under **Certificates & secrets**, create a client secret
6. Note the **Application (client) ID** and **Directory (tenant) ID**

---

## 5. Deployment

### Using GitHub Actions

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy MCP Server

on:
  push:
    branches: [main]
  workflow_dispatch:

env:
  AZURE_WEBAPP_NAME: mcp-medrecpro
  AZURE_WEBAPP_PACKAGE_PATH: '.'
  DOTNET_VERSION: '8.0.x'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --configuration Release --no-restore

    - name: Publish
      run: dotnet publish --configuration Release --no-build --output ./publish

    - name: Deploy to Azure Web App
      uses: azure/webapps-deploy@v3
      with:
        app-name: ${{ env.AZURE_WEBAPP_NAME }}
        publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
        package: ./publish
```

### Using Azure CLI

```bash
# Build and publish
dotnet publish -c Release -o ./publish

# Deploy
az webapp deploy \
  --name $MCP_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --src-path ./publish.zip \
  --type zip
```

---

## 6. Verification

### Health Check

```bash
# Check root endpoint
curl https://mcp.medrecpro.com/

# Expected response:
# {
#   "name": "MedRecPro MCP Server",
#   "version": "1.0.0",
#   "status": "running",
#   "mcp": "/mcp",
#   "documentation": "https://mcp.medrecpro.com/docs"
# }
```

### OAuth Metadata

```bash
# Check Authorization Server metadata
curl https://mcp.medrecpro.com/.well-known/oauth-authorization-server

# Check Protected Resource metadata
curl https://mcp.medrecpro.com/.well-known/oauth-protected-resource
```

### MCP Endpoint (Unauthenticated)

```bash
# Should return 401 with WWW-Authenticate header
curl -i https://mcp.medrecpro.com/mcp

# Expected response:
# HTTP/2 401
# WWW-Authenticate: Bearer resource_metadata="https://mcp.medrecpro.com/.well-known/oauth-protected-resource"
```

---

## 7. Monitoring

### Application Insights

1. Create Application Insights resource in Azure
2. Add connection string to Key Vault: `ApplicationInsights--ConnectionString`
3. View logs in Azure Portal → Application Insights → Logs

### Log Queries

```kusto
// Authentication failures
traces
| where message contains "[Auth]" and severityLevel >= 2
| order by timestamp desc
| take 100

// OAuth flow events
traces
| where message contains "[OAuth]"
| order by timestamp desc
| take 100

// API call errors
requests
| where success == false
| summarize count() by name, resultCode
| order by count_ desc
```

---

## 8. Security Checklist

- [ ] JWT signing key is at least 256 bits (32 characters)
- [ ] All secrets stored in Azure Key Vault
- [ ] Cloudflare SSL set to Full (strict)
- [ ] OAuth redirect URIs are HTTPS only (except localhost)
- [ ] CORS configured for allowed origins only
- [ ] Rate limiting enabled in Cloudflare
- [ ] Managed identity enabled for Key Vault access
- [ ] Application Insights enabled for monitoring
- [ ] SSE buffering disabled for `/mcp` endpoint

---

## Troubleshooting

### Claude Cannot Authenticate

1. Check Protected Resource Metadata is accessible
2. Verify OAuth callback URLs are registered with providers
3. Check Cloudflare is not blocking OAuth callbacks
4. Review Application Insights logs for errors

### Token Validation Fails

1. Verify JWT signing key matches between MCP server config and Key Vault
2. Check clock synchronization (tokens are time-sensitive)
3. Ensure issuer/audience match server URL exactly

### SSE Connection Drops

1. Verify Cloudflare buffering is disabled
2. Check Azure App Service timeout settings
3. Ensure WebSocket/SSE is not blocked by WAF rules
