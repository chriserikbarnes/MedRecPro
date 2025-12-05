using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MedRecPro.Service;

/**************************************************************/
/// <summary>
/// Provides Azure Management API tokens using DefaultAzureCredential for background services.
/// </summary>
/// <remarks>
/// This provider automatically selects the appropriate credential based on the runtime environment:
/// 
/// * **Azure App Service:** Uses system-assigned or user-assigned managed identity
/// * **Local Development (Visual Studio):** Uses Visual Studio signed-in account
/// * **Local Development (Azure CLI):** Uses `az login` account
/// 
/// Unlike <see cref="AzureManagementTokenProvider"/>, this provider requires no client secrets in any environment.
/// 
/// **Prerequisites:**
/// * **Azure:** Enable managed identity on App Service and grant Monitoring Reader role
/// * **Local:** Sign in via Visual Studio or Azure CLI with an account that has Monitoring Reader
/// </remarks>
public class AzureAppTokenProvider
{
    #region Fields

    private readonly TokenCredential _credential;
    private readonly ILogger<AzureAppTokenProvider>? _logger;
    private readonly string _environment;
    private const string ManagementScope = "https://management.azure.com/.default";

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAppTokenProvider"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration for optional settings.</param>
    /// <param name="logger">Logger for credential diagnostics and troubleshooting.</param>
    /// <remarks>
    /// Optional configuration values in `appsettings.json`:
    /// 
    /// ```json
    /// {
    ///   "Azure": {
    ///     "ManagedIdentityClientId": "guid-for-user-assigned-identity",
    ///     "TenantId": "your-tenant-id"
    ///   }
    /// }
    /// ```
    /// 
    /// If `ManagedIdentityClientId` is specified, uses that user-assigned managed identity.
    /// Otherwise, uses system-assigned managed identity in Azure or developer credentials locally.
    /// </remarks>
    public AzureAppTokenProvider(
        IConfiguration configuration,
        ILogger<AzureAppTokenProvider>? logger = null)
    {
        _logger = logger;

        // Detect runtime environment for logging purposes
        _environment = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"))
            ? "Azure App Service"
            : "Local Development";

        var options = new DefaultAzureCredentialOptions
        {
            // Exclude credentials that add latency or aren't useful for this scenario
            ExcludeInteractiveBrowserCredential = true,
            ExcludeSharedTokenCacheCredential = true,

            // Diagnostics for troubleshooting authentication issues
            Diagnostics =
            {
                LoggedHeaderNames = { "x-ms-request-id" },
                LoggedQueryParameters = { "api-version" },
                IsLoggingContentEnabled = false // Set to true only for debugging
            }
        };

        // Optional: Use specific user-assigned managed identity
        var managedIdentityClientId = configuration?["Azure:ManagedIdentityClientId"];
        if (!string.IsNullOrEmpty(managedIdentityClientId))
        {
            options.ManagedIdentityClientId = managedIdentityClientId;

            _logger?.LogInformation(
                "AzureAppTokenProvider configured with user-assigned managed identity: {ClientId}",
                managedIdentityClientId);
        }

        // Optional: Restrict to specific tenant
        var tenantId = configuration?["Azure:TenantId"]
                    ?? configuration?["Authentication:Microsoft:TenantId"];
        if (!string.IsNullOrEmpty(tenantId))
        {
            options.TenantId = tenantId;
        }

        _credential = new DefaultAzureCredential(options);

        _logger?.LogInformation(
            "AzureAppTokenProvider initialized. Environment: {Environment}, " +
            "UserAssignedIdentity: {HasUserAssigned}, TenantId: {HasTenant}",
            _environment,
            !string.IsNullOrEmpty(managedIdentityClientId),
            !string.IsNullOrEmpty(tenantId));
    }

    #endregion

    #region Public Methods

    /**************************************************************/
    /// <summary>
    /// Acquires an access token for Azure Management API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Access token string for Azure Management API.</returns>
    /// <exception cref="AuthenticationFailedException">
    /// Thrown when no suitable credential is available or authentication fails.
    /// </exception>
    /// <remarks>
    /// The credential selection order for <see cref="DefaultAzureCredential"/>:
    /// 1. Environment variables (AZURE_CLIENT_ID, etc.)
    /// 2. Managed Identity (in Azure)
    /// 3. Visual Studio credential
    /// 4. Azure CLI credential
    /// 5. Azure PowerShell credential
    /// 
    /// Tokens are cached automatically and refreshed before expiration.
    /// </remarks>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Build scope fresh to avoid any encoding issues
            var scope = string.Concat("https://management.azure.com/", ".default");
            var tokenRequestContext = new TokenRequestContext(new[] { scope });

            _logger?.LogDebug("Requesting token with scope: {Scope}", scope);

            var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);

            _logger?.LogDebug(
                "Acquired Azure Management token. ExpiresOn: {ExpiresOn}, Environment: {Environment}",
                token.ExpiresOn,
                _environment);

            return token.Token;
        }
        catch (CredentialUnavailableException eex)
        {
            _logger?.LogError(
                eex,
                "No suitable credential available in {Environment}. " +
                "Azure: Enable managed identity. Local: Run 'az login' or sign in to Visual Studio.",
                _environment);
            throw new AuthenticationFailedException(
                $"No Azure credential available in {_environment}. See inner exception for details.", eex);
        }
        catch (AuthenticationFailedException ex)
        {
            _logger?.LogError(
                ex,
                "Failed to acquire Azure Management token in {Environment}. " +
                "Ensure managed identity is enabled (Azure) or you're signed in (local).",
                _environment);
            throw;
        }
    }

    /**************************************************************/
    /// <summary>
    /// Acquires an access token with full <see cref="AccessToken"/> metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// An <see cref="AccessToken"/> containing the token string and expiration time.
    /// </returns>
    /// <remarks>
    /// Use this method when you need the token expiration time for caching decisions.
    /// </remarks>
    public async Task<AccessToken> GetAccessTokenWithMetadataAsync(CancellationToken cancellationToken = default)
    {
        var tokenRequestContext = new TokenRequestContext(new[] { ManagementScope });
        return await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
    }

    /**************************************************************/
    /// <summary>
    /// Gets the underlying <see cref="TokenCredential"/> for use with Azure SDK clients.
    /// </summary>
    /// <returns>The configured <see cref="DefaultAzureCredential"/> instance.</returns>
    /// <remarks>
    /// Use this when creating Azure SDK clients that accept <see cref="TokenCredential"/> directly,
    /// such as `MetricsClient`.
    /// 
    /// Example:
    /// ```csharp
    /// var credential = _appTokenProvider.GetCredential();
    /// var metricsClient = new MetricsClient(endpoint, credential);
    /// ```
    /// </remarks>
    public TokenCredential GetCredential() => _credential;

    /**************************************************************/
    /// <summary>
    /// Gets the detected runtime environment.
    /// </summary>
    /// <returns>
    /// A string indicating the runtime environment: "Azure App Service" or "Local Development".
    /// </returns>
    /// <remarks>
    /// Useful for logging and diagnostics to understand which credential type is being used.
    /// </remarks>
    public string GetEnvironment() => _environment;

    /**************************************************************/
    /// <summary>
    /// Gets estimated token expiration time.
    /// </summary>
    /// <returns>Estimated expiration time (tokens are typically valid for 1 hour).</returns>
    /// <remarks>
    /// For accurate expiration time, use <see cref="GetAccessTokenWithMetadataAsync"/> instead.
    /// Azure Management API tokens are typically valid for 60-90 minutes.
    /// </remarks>
    public DateTimeOffset? GetTokenExpiration() => DateTimeOffset.UtcNow.AddHours(1);

    /**************************************************************/
    /// <summary>
    /// Tests the credential by attempting to acquire a token.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// A tuple containing success status, environment info, and error message if failed.
    /// </returns>
    /// <remarks>
    /// Use this method to verify credential configuration without throwing exceptions.
    /// Useful for health checks and diagnostic endpoints.
    /// 
    /// Example:
    /// ```csharp
    /// var (success, environment, error) = await _appTokenProvider.TestCredentialAsync();
    /// if (!success)
    /// {
    ///     _logger.LogError("Credential test failed: {Error}", error);
    /// }
    /// ```
    /// </remarks>
    public async Task<(bool Success, string Environment, string? ErrorMessage)> TestCredentialAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetAccessTokenWithMetadataAsync(cancellationToken);

            return (
                Success: true,
                Environment: _environment,
                ErrorMessage: null
            );
        }
        catch (Exception ex)
        {
            return (
                Success: false,
                Environment: _environment,
                ErrorMessage: ex.Message
            );
        }
    }

    #endregion
}