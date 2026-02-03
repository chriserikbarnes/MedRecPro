# Test script for MCP server endpoints
# Ignore SSL certificate errors for localhost testing
add-type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(
        ServicePoint srvPoint, X509Certificate certificate,
        WebRequest request, int certificateProblem) {
        return true;
    }
}
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy

Write-Host "Testing MCP Server Endpoints..." -ForegroundColor Cyan
Write-Host ""

# Test 1: Root endpoint
Write-Host "1. Testing root endpoint (/):" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://localhost:7169/" -UseBasicParsing -TimeoutSec 10
    Write-Host "   Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "   Response:" -ForegroundColor Gray
    $response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 5
} catch {
    Write-Host "   ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 2: OAuth Protected Resource Metadata
Write-Host "2. Testing OAuth Protected Resource Metadata:" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://localhost:7169/.well-known/oauth-protected-resource" -UseBasicParsing -TimeoutSec 10
    Write-Host "   Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "   Response:" -ForegroundColor Gray
    $response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 5
} catch {
    Write-Host "   ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 3: MCP endpoint (should work without auth now)
Write-Host "3. Testing MCP endpoint (/mcp) - POST with initialize:" -ForegroundColor Yellow
try {
    $body = @{
        jsonrpc = "2.0"
        id = 1
        method = "initialize"
        params = @{
            protocolVersion = "2024-11-05"
            capabilities = @{}
            clientInfo = @{
                name = "test-client"
                version = "1.0.0"
            }
        }
    } | ConvertTo-Json -Depth 10

    $response = Invoke-WebRequest -Uri "https://localhost:7169/mcp" -Method POST -Body $body -ContentType "application/json" -UseBasicParsing -TimeoutSec 10
    Write-Host "   Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "   Response:" -ForegroundColor Gray
    $response.Content
} catch {
    Write-Host "   ERROR: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        Write-Host "   Status Code: $($_.Exception.Response.StatusCode)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Testing complete!" -ForegroundColor Cyan
