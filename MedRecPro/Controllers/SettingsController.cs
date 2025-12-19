
using Azure;
using Azure.Identity;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using static MedRecPro.Models.UserRole;
using System.Net;


namespace MedRecPro.Controllers
{
    /**************************************************************/
    /// <summary>
    /// API controller for exposing application settings to clients.
    /// </summary>
    /// <remarks>
    /// Provides read-only access to non-sensitive configuration settings
    /// that clients need to adapt their behavior (e.g., demo mode status).
    /// Also provides administrative access to in-memory application logs.
    /// </remarks>
    /// <seealso cref="UserLoggerProvider"/>
    /// <seealso cref="LoggingSettings"/>
    [ApiController]
    public class SettingsController : ApiControllerBase
    {
        #region fields

        private readonly IConfiguration _configuration;
        private readonly ILogger<SettingsController> _logger;
        private readonly IMemoryCache _cache;
        private readonly AzureSqlMetricsService _metricsService;
        private readonly AzureAppTokenProvider _appTokenProvider;
        private readonly UserLoggerProvider _loggerProvider;
        private readonly string _pkEncryptionSecret;

        #endregion

        #region constructor
        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsController"/> class.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="logger">Logger instance for this controller.</param>
        /// <param name="sqlMetricsService">Azure SQL metrics service.</param>
        /// <param name="cache">Memory cache instance.</param>
        /// <param name="appTokenProvider">Azure app token provider.</param>
        /// <param name="loggerProvider">User logger provider for log access.</param>
        /// <seealso cref="UserLoggerProvider"/>
        public SettingsController(
            IConfiguration configuration,
            ILogger<SettingsController> logger,
            AzureSqlMetricsService sqlMetricsService,
            IMemoryCache cache,
            AzureAppTokenProvider appTokenProvider,
            UserLoggerProvider loggerProvider)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _metricsService = sqlMetricsService ?? throw new ArgumentNullException(nameof(sqlMetricsService));
            _appTokenProvider = appTokenProvider ?? throw new ArgumentNullException(nameof(appTokenProvider));
            _loggerProvider = loggerProvider ?? throw new ArgumentNullException(nameof(loggerProvider));
            _pkEncryptionSecret = _configuration.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing or empty.");
        }

        #endregion

        #region public methods

      
        /**************************************************************/
        /// <summary>
        /// Gets the current demo mode status and configuration.
        /// </summary>
        /// <returns>
        /// An OK result containing demo mode status information.
        /// </returns>
        /// <remarks>
        /// This endpoint allows clients to query whether the application is running in demo mode
        /// and retrieve related configuration such as refresh interval and banner display settings.
        /// This information can be used by frontend applications to display appropriate warnings
        /// or adjust behavior accordingly.
        /// </remarks>
        /// <example>
        /// GET /api/settings/demomode
        /// Response:
        /// {
        ///   "enabled": true,
        ///   "refreshIntervalMinutes": 60,
        ///   "showBanner": true,
        ///   "autoTruncateOnStartup": false
        /// }
        /// </example>
        /// <seealso cref="IConfiguration"/>
        [HttpGet("demomode")]
        public IActionResult GetDemoModeStatus()
        {
            #region implementation

            try
            {
                var demoSettings = _configuration.GetSection("DemoModeSettings");
                var isAzure = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

                var response = new
                {
                    enabled = demoSettings.GetValue<bool>("Enabled", false),
                    refreshIntervalMinutes = demoSettings.GetValue<int>("RefreshIntervalMinutes", 60),
                    showBanner = demoSettings.GetValue<bool>("ShowDemoModeBanner", true),
                    autoTruncateOnStartup = demoSettings.GetValue<bool>("AutoTruncateOnStartup", false),
                    IsAzure = isAzure,
                    SiteName = isAzure ? Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") : "Local",
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    CurrentTime = DateTime.UtcNow,
                    Note = isAzure ? "Running in Azure App Service" : "Running locally"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving demo mode settings");
                return StatusCode(500, new { error = "Error retrieving settings" });
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets basic application information.
        /// </summary>
        /// <returns>
        /// An OK result containing application version and environment information.
        /// </returns>
        /// <remarks>
        /// This endpoint provides general application metadata that clients can use
        /// for display purposes or to determine compatible API versions.
        /// </remarks>
        /// <example>
        /// GET /api/settings/info
        /// Response:
        /// {
        ///   "applicationName": "MedRecPro API",
        ///   "version": "1.0.0",
        ///   "environment": "Development",
        ///   "demoMode": true
        /// }
        /// </example>
        [HttpGet("info")]
        public IActionResult GetApplicationInfo()
        {
            #region implementation

            try
            {
                var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
                var demoModeEnabled = _configuration.GetValue<bool>("DemoModeSettings:Enabled", false);

                var response = new
                {
                    applicationName = "MedRecPro API",
                    version = _configuration.GetValue<string>("Version"),
                    environment = environment,
                    demoMode = demoModeEnabled
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving application info");
                return StatusCode(500, new { error = "Error retrieving application information" });
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets supported API features and capabilities.
        /// </summary>
        /// <returns>
        /// An OK result containing feature flags and capability information.
        /// </returns>
        /// <remarks>
        /// This endpoint allows clients to discover what features are enabled
        /// in the current deployment, enabling adaptive UI and functionality.
        /// </remarks>
        /// <example>
        /// GET /api/settings/features
        /// Response:
        /// {
        ///   "demoMode": true,
        ///   "externalAuth": true,
        ///   "importEnabled": true,
        ///   "exportEnabled": true,
        ///   "comparisonAnalysis": true
        /// }
        /// </example>
        [HttpGet("features")]
        public IActionResult GetFeatures()
        {
            #region implementation

            try
            {

                var featureFlagsSection = _configuration.GetSection("FeatureFlags");

                var response = new
                {
                    demoMode = _configuration.GetValue<bool>("DemoModeSettings:Enabled", false),
                    truncateAtStart = _configuration.GetValue<bool>("DemoModeSettings:AutoTruncateOnStartup", false),
                    externalAuth = featureFlagsSection.GetValue<bool>("ExternalAuthEnabled", true),
                    importEnabled = featureFlagsSection.GetValue<bool>("SplImportEnabled", true),
                    exportEnabled = featureFlagsSection.GetValue<bool>("SplExportEnabled", true),
                    comparisonAnalysis = featureFlagsSection.GetValue<bool>("ComparisonAnalysisEnabled", true),
                    backgroundProcessing = featureFlagsSection.GetValue<bool>("BackgroundProcessingEnabled", true),
                    databaseBulkProcessing = featureFlagsSection.GetValue<bool?>("UseBulkOperations", true),
                    databaseStaging = featureFlagsSection.GetValue<bool?>("UseBulkStagingOperations", true),
                    databaseBatchSaving = featureFlagsSection.GetValue<bool?>("UseBatchSaving", true),
                    activityTracking = featureFlagsSection.GetValue<bool>("ActivityTrackingEnabled", true),
                    enhancedDebugging = featureFlagsSection.GetValue<bool>("UseEnhancedDebugging", true),
                    caching = _configuration.GetValue<bool>("ComparisonSettings:EnableCaching", true),
                    fileStorage = _configuration.GetSection("FileStorageSettings").Exists(),
                    razorTemplates = _configuration.GetSection("RazorLight").Exists()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving feature flags");
                return StatusCode(500, new { error = "Error retrieving features" });
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets database usage monitoring and throttling configuration settings.
        /// </summary>
        /// <returns>
        /// An OK result containing database limit thresholds and monitoring configuration.
        /// </returns>
        /// <remarks>
        /// This endpoint exposes the current database usage monitoring configuration,
        /// including throttle level thresholds and cost limit settings. Clients can use
        /// this information to understand current throttling behavior and display
        /// appropriate warnings to users when approaching usage limits.
        /// 
        /// **Throttle Levels:**
        /// - **None**: No throttling applied (below Warning threshold)
        /// - **Warning**: Consider reducing non-essential operations
        /// - **Moderate**: Rate-limit non-critical operations
        /// - **Aggressive**: Only essential operations proceed without delay
        /// - **Critical**: Block all non-essential operations
        /// - **CostLimit**: Block all except critical operations
        /// 
        /// **Cost Calculation:**
        /// Each 10% overage beyond 100% costs approximately $1.45/month.
        /// Formula: Max cost = (MaxMonthlyCostPercent - 100) × 1000 × $0.000145
        /// </remarks>
        /// <example>
        /// GET /api/settings/database-limits
        /// Response:
        /// {
        ///   "enabled": true,
        ///   "pollingIntervalHours": 2,
        ///   "initialDelaySeconds": 60,
        ///   "maxConsecutiveFailures": 5,
        ///   "thresholds": {
        ///     "warning": 70,
        ///     "moderate": 80,
        ///     "aggressive": 90,
        ///     "critical": 95
        ///   },
        ///   "costLimits": {
        ///     "maxMonthlyCostPercent": 500,
        ///     "estimatedMaxOverageCost": 58.00
        ///   }
        /// }
        /// </example>
        /// <seealso cref="GetFeatures"/>
        [HttpGet("database-limits")]
        public IActionResult GetDatabaseLimits()
        {
            #region implementation
            try
            {
                var monitorSection = _configuration.GetSection("DatabaseUsageMonitor");

                // Check if the section exists
                var sectionExists = monitorSection.Exists();

                // Read threshold values with defaults matching the configuration documentation
                var warningThreshold = monitorSection.GetValue<int>("WarningThreshold", 70);
                var moderateThreshold = monitorSection.GetValue<int>("ModerateThreshold", 80);
                var aggressiveThreshold = monitorSection.GetValue<int>("AggressiveThreshold", 90);
                var criticalThreshold = monitorSection.GetValue<int>("CriticalThreshold", 95);
                var maxMonthlyCostPercent = monitorSection.GetValue<int>("MaxMonthlyCostPercent", 110);

                // Calculate estimated max overage cost based on the formula:
                // Max cost = (MaxMonthlyCostPercent - 100) × 1000 × $0.000145
                var estimatedMaxOverageCost = (Math.Max(0, (maxMonthlyCostPercent - 100)) * 1000 * 0.000145m).ToString("C2");

                var response = new
                {
                    enabled = monitorSection.GetValue<bool>("Enabled", true),
                    pollingIntervalHours = monitorSection.GetValue<double>("PollingIntervalHours", 2.0),
                    initialDelaySeconds = monitorSection.GetValue<int>("InitialDelaySeconds", 60),
                    maxConsecutiveFailures = monitorSection.GetValue<int>("MaxConsecutiveFailures", 5),
                    thresholds = new
                    {
                        warning = warningThreshold,
                        moderate = moderateThreshold,
                        aggressive = aggressiveThreshold,
                        critical = criticalThreshold
                    },
                    costLimits = new
                    {
                        maxMonthlyCostPercent = maxMonthlyCostPercent,
                        estimatedMaxOverageCost = estimatedMaxOverageCost
                    },
                    configured = sectionExists
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving database limit settings");
                return StatusCode(500, new { error = "Error retrieving database limits" });
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets current database vCore usage, remaining free-tier quota, and cost
        /// projections for the MedRecPro Azure SQL database.
        /// </summary>
        /// <returns>
        /// An <see cref="IActionResult"/> containing the current month's vCore usage,
        /// remaining quota, projected end-of-month usage and overage cost, and
        /// throttling guidance.
        /// </returns>
        /// <remarks>
        /// This endpoint is a thin orchestration layer over <see cref="AzureSqlMetricsService"/>: 
        /// it does not talk to Azure directly, but instead delegates all metric aggregation to the service.
        ///
        /// **Authentication &amp; Identity**
        /// 
        /// This action requires an authenticated MedRecPro user with permission to access the configured Azure subscription. 
        /// The underlying Azure calls use <see cref="AzureManagementTokenProvider"/> to obtain a `TokenCredential`
        /// (for example via `DefaultAzureCredential` or an interactive browser credential, depending on your environment).
        ///
        /// **Configuration (appsettings.json)**
        /// 
        /// The Azure SQL resource to monitor is configured via `appsettings.json` (or environment configuration) using the following setting:
        ///
        /// ```json
        /// "Azure": {
        ///   "SqlDatabase": {
        ///     "ResourceId": "/subscriptions/your-id/resourceGroups/MedRecPro/.../your-DB"
        ///   }
        /// }
        /// ```
        ///
        /// <see cref="AzureSqlMetricsService"/> reads this `ResourceId` and uses it when querying Azure Monitor metrics 
        /// (either via the Metrics REST API or the ARM client SDK, falling back between them internally).
        ///
        /// **Processing Flow**
        /// 
        /// 1. The controller calls <see cref="AzureSqlMetricsService.GetFreeTierStatusAsync"/> to obtain total used and remaining vCore-seconds and the percent used of the monthly free tier.
        /// 2. It then calls <see cref="AzureSqlMetricsService.GetProjectedMonthlyCostAsync"/> to estimate end-of-month usage and overage cost based on elapsed days and current burn rate.
        /// 3. Finally, it calls <see cref="AzureSqlMetricsService.ShouldThrottleAsync"/> to determine whether the application should consider self-throttling and, if so, at what level.
        ///
        /// **Possible HTTP Status Codes**
        /// 
        /// - **200 OK** – Metrics retrieved successfully.
        /// - **400 Bad Request** – Configuration or usage error (for example, missing or invalid Azure resource configuration).
        /// - **401 Unauthorized** – User is not authenticated or Azure authentication failed.
        /// - **403 Forbidden** – Authenticated user lacks rights (e.g. missing Monitoring Reader/Contributor permissions on the Azure SQL resource).
        /// - **500 Internal Server Error** – Unexpected error while querying Azure Monitor or processing the results.
        /// 
        /// **Security Note:** Requires authentication and Adminstrative role membership
        /// </remarks>
        /// <example>
        /// Sample success payload:
        /// <code>
        /// {
        ///   "currentMonth": {
        ///     "usedVCoreSeconds": 7484,
        ///     "remainingVCoreSeconds": 92516,
        ///     "percentUsed": 7.48,
        ///     "daysElapsed": 2
        ///   },
        ///   "projection": {
        ///     "estimatedMonthlyUsage": 116002,
        ///     "estimatedCost": 2.32,
        ///     "costDescription": "$2.32 for overage"
        ///   },
        ///   "throttling": {
        ///     "shouldThrottle": false,
        ///     "throttleLevel": "None"
        ///   }
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="AzureSqlMetricsService"/>
        /// <seealso cref="AzureManagementTokenProvider"/>
        [HttpGet("metrics/database-cost")]
        [Authorize]
        [RequireUserRole(Admin)] // Using UserRole constants
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDatabaseMetrics()
        {
            #region implementation

            try
            {
                // Get current free-tier status (used, remaining, and percent of quota used).
                var (used, remaining, percentUsed) = await _metricsService.GetFreeTierStatusAsync();

                // Get projected end-of-month usage and cost based on current burn rate.
                var (projectedUsage, projectedCost, daysElapsed) = await _metricsService.GetProjectedMonthlyCostAsync();

                // Get throttling recommendation (if any) based on utilization thresholds.
                var (shouldThrottle, throttleLevel, _) = await _metricsService.ShouldThrottleAsync();

                // Return a compact, UI-friendly summary payload.
                return Ok(new
                {
                    CurrentMonth = new
                    {
                        UsedVCoreSeconds = Math.Round(used, 2),
                        RemainingVCoreSeconds = Math.Round(remaining, 2),
                        PercentUsed = Math.Round(percentUsed, 2),
                        DaysElapsed = daysElapsed
                    },
                    Projection = new
                    {
                        EstimatedMonthlyUsage = Math.Round(projectedUsage, 2),
                        EstimatedCost = Math.Round(projectedCost, 2),
                        CostDescription = projectedCost > 0
                            ? $"${projectedCost:F2} for overage"
                            : "Within free tier"
                    },
                    Throttling = new
                    {
                        ShouldThrottle = shouldThrottle,
                        ThrottleLevel = throttleLevel
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                // Treat service/configuration issues as a client error (e.g. bad or missing config).
                _logger.LogError(ex, "Configuration or usage error while retrieving database metrics.");
                return BadRequest(new
                {
                    error = "Invalid configuration or usage for Azure SQL metrics.",
                    detail = ex.Message
                });
            }
            catch (AuthenticationFailedException ex)
            {
                // User is authenticated in MedRecPro but Azure authentication failed.
                _logger.LogError(ex, "Failed to authenticate for Azure Monitor API.");
                return Unauthorized(new
                {
                    error = "Azure authentication failed. Please ensure you have access to the Azure subscription."
                });
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Forbidden)
            {
                // Azure explicitly denied access to the metrics endpoint.
                _logger.LogError(ex, "Azure Monitor API access forbidden for current user.");
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "Access to Azure metrics is forbidden. Ensure the signed-in identity has monitor permissions on the SQL resource."
                });
            }
            catch (Exception ex)
            {
                // Catch-all for anything unexpected coming from Azure or local processing.
                _logger.LogError(ex, "Failed to retrieve database metrics.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Failed to retrieve metrics."
                });
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests the AzureAppTokenProvider credential configuration.
        /// </summary>
        /// <returns>
        /// An OK result if the credential is working, or error details if it fails.
        /// </returns>
        /// <remarks>
        /// This endpoint tests the <see cref="DefaultAzureCredential"/>-based authentication:
        /// 
        /// * **In Azure:** Tests managed identity authentication
        /// * **Locally:** Tests Visual Studio or Azure CLI credential
        /// 
        /// Use this endpoint to verify credential configuration before enabling
        /// the background monitoring service.
        /// 
        /// **Security Note:** Requires authentication and Adminstrative role membership
        /// </remarks>
        /// <example>
        /// GET /api/settings/test/appcredential
        /// 
        /// **Response (200 OK):**
        /// ```json
        /// {
        ///   "success": true,
        ///   "environment": "Azure App Service",
        ///   "tokenAcquired": true,
        ///   "tokenExpiresOn": "2024-01-15T12:00:00Z",
        ///   "message": "DefaultAzureCredential is working correctly"
        /// }
        /// ```
        /// 
        /// **Response (500 Error):**
        /// ```json
        /// {
        ///   "success": false,
        ///   "environment": "Local Development",
        ///   "error": "No credential available...",
        ///   "troubleshooting": {
        ///     "azure": "Ensure managed identity is enabled on App Service",
        ///     "local": "Run 'az login' or sign in to Visual Studio Azure account"
        ///   }
        /// }
        /// ```
        /// </example>
        /// <seealso cref="RequireUserRoleAttribute"/>
        /// <seealso cref="MedRecPro.Models.UserRole"/>
        /// <seealso cref="AzureAppTokenProvider"/>
        /// <seealso cref="DefaultAzureCredential"/>dotnet list package | grep Azure.Identity
        [HttpGet("test/app-credential")]
        [Authorize]
        [RequireUserRole(Admin)] // Using UserRole constants
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestAppCredential()
        {
            #region implementation

            try
            {
                // Test the credential
                var token = await _appTokenProvider.GetAccessTokenWithMetadataAsync();
                var environment = _appTokenProvider.GetEnvironment();

                _logger.LogInformation(
                    "AzureAppTokenProvider test succeeded. Environment: {Environment}, ExpiresOn: {ExpiresOn}",
                    environment,
                    token.ExpiresOn);

                return Ok(new
                {
                    Success = true,
                    Environment = environment,
                    TokenAcquired = true,
                    TokenExpiresOn = token.ExpiresOn,
                    TokenLength = token.Token.Length,
                    Message = "DefaultAzureCredential is working correctly"
                });
            }
            catch (Azure.Identity.AuthenticationFailedException ex)
            {
                var environment = _appTokenProvider.GetEnvironment();

                _logger.LogError(
                    ex,
                    "AzureAppTokenProvider test failed in {Environment}",
                    environment);

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Success = false,
                    Environment = environment,
                    Error = ex.Message,
                    InnerError = ex.InnerException?.Message,
                    Troubleshooting = new
                    {
                        Azure = "Ensure managed identity is enabled on App Service and has 'Monitoring Reader' role on the SQL Database",
                        Local = "Run 'az login' in terminal, or sign in to Visual Studio (Tools → Options → Azure Service Authentication)"
                    }
                });
            }
            catch (Exception ex)
            {
                var environment = _appTokenProvider.GetEnvironment();

                _logger.LogError(
                    ex,
                    "Unexpected error testing AzureAppTokenProvider in {Environment}",
                    environment);

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Success = false,
                    Environment = environment,
                    Error = ex.Message,
                    Type = ex.GetType().Name
                });
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tests the full metrics retrieval pipeline using DefaultAzureCredential.
        /// </summary>
        /// <returns>
        /// An OK result with current metrics if successful, or error details if it fails.
        /// </returns>
        /// <remarks>
        /// This endpoint performs a complete end-to-end test:
        /// 
        /// 1. Acquires token using <see cref="AzureAppTokenProvider"/>
        /// 2. Queries Azure Monitor for SQL Database metrics
        /// 3. Returns current free tier usage
        /// 
        /// Unlike <see cref="TestAppCredential"/>, this tests the entire pipeline
        /// including the Monitoring Reader role assignment.
        /// 
        /// **Security Note:** Requires authentication and Adminstrative role membership
        /// </remarks>
        /// <example>
        /// GET /api/settings/test/app-metrics-pipeline
        /// 
        /// **Response (200 OK):**
        /// ```json
        /// {
        ///   "success": true,
        ///   "environment": "Azure App Service",
        ///   "metrics": {
        ///     "remainingVCoreSeconds": 85000,
        ///     "percentUsed": 15.0
        ///   }
        /// }
        /// ```
        /// </example>
        /// <seealso cref="AzureAppTokenProvider"/>
        /// <seealso cref="AzureSqlMetricsService"/>
        [HttpGet("test/app-metrics-pipeline")]
        [Authorize]
        [RequireUserRole(Admin)] // Using UserRole constants
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestAppMetricsPipeline()
        {
            #region implementation

            var environment = _appTokenProvider.GetEnvironment();
            var steps = new List<string>();

            try
            {
                // Step 1: Test credential
                steps.Add("Acquiring token...");
                var token = await _appTokenProvider.GetAccessTokenWithMetadataAsync();
                steps.Add($"Token acquired (expires: {token.ExpiresOn})");

                // Step 2: Query metrics using existing service
                steps.Add("Querying Azure Monitor metrics...");
                var (used, remaining, percentUsed) = await _metricsService.GetFreeTierStatusAsync();
                steps.Add($"Metrics retrieved successfully");

                _logger.LogInformation(
                    "Metrics pipeline test succeeded. Environment: {Environment}, Used: {Used:N0}, Remaining: {Remaining:N0}",
                    environment,
                    used,
                    remaining);

                return Ok(new
                {
                    Success = true,
                    Environment = environment,
                    Steps = steps,
                    Metrics = new
                    {
                        UsedVCoreSeconds = Math.Round(used, 2),
                        RemainingVCoreSeconds = Math.Round(remaining, 2),
                        PercentUsed = Math.Round(percentUsed, 2)
                    }
                });
            }
            catch (Exception ex)
            {
                steps.Add($"FAILED: {ex.Message}");

                _logger.LogError(
                    ex,
                    "Metrics pipeline test failed at step {StepCount}. Environment: {Environment}",
                    steps.Count,
                    environment);

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Success = false,
                    Environment = environment,
                    Steps = steps,
                    Error = ex.Message,
                    InnerError = ex.InnerException?.Message,
                    Type = ex.GetType().Name
                });
            }

            #endregion
        }


        /**************************************************************/
        /// <summary>
        /// Clears all managed cache entries from the performance cache.
        /// </summary>
        /// <returns>
        /// An OK result if the cache was successfully cleared, or an error status if the operation failed.
        /// </returns>
        /// <remarks>
        /// This endpoint triggers a managed cache reset, removing all cached items that have been
        /// registered in the managed key chain. This is typically used when database changes occur
        /// that require all users to see consistent, up-to-date data (e.g., assignment ownership changes,
        /// organization updates, or other critical data modifications).
        /// 
        /// Managed cache items are distinguished from regular cache items by their inclusion in the
        /// key chain, which allows for bulk removal without affecting unmanaged cached data.
        /// </remarks>
        /// <example>
        /// POST /api/settings/clearmanagedcache
        /// Response:
        /// {
        ///   "success": true,
        ///   "message": "Managed cache successfully cleared"
        /// }
        /// </example>
        /// <seealso cref="PerformanceHelper.ResetManagedCache"/>
        /// <seealso cref="IConfiguration"/>
        [HttpPost("clearmanagedcache")]
        public IActionResult ClearManagedCache()
        {
            #region implementation

            try
            {
                // Validate that the performance helper is available
                if (!PerformanceHelper.Initialized)
                {
                    _logger.LogWarning("Performance helper instance is not available");
                    return StatusCode(503, new
                    {
                        success = false,
                        error = "Cache service is not available"
                    });
                }

                // Call the performance helper to reset the managed cache
                PerformanceHelper.ResetManagedCache();

                _logger.LogInformation("Managed cache successfully cleared");

                return Ok(new
                {
                    success = true,
                    message = "Managed cache successfully cleared"
                });
            }
            catch (ArgumentNullException argEx)
            {
                _logger.LogError(argEx, "Argument null exception while clearing managed cache");
                return BadRequest(new
                {
                    success = false,
                    error = "Invalid cache operation parameters"
                });
            }
            catch (InvalidOperationException opEx)
            {
                _logger.LogError(opEx, "Invalid operation while clearing managed cache");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Cache operation failed due to invalid state"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while clearing managed cache");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Error clearing managed cache"
                });
            }

            #endregion
        }

        #endregion

        #region Log Viewing Endpoints

        /**************************************************************/
        /// <summary>
        /// Gets current log statistics and configuration settings.
        /// </summary>
        /// <returns>
        /// Log statistics including entry counts, retention settings, and level distribution.
        /// </returns>
        /// <remarks>
        /// This endpoint provides an overview of the in-memory logging system including:
        ///
        /// * Total number of log entries currently stored
        /// * Number of unique categories and users
        /// * Timestamps of oldest and newest entries
        /// * Distribution of entries by log level
        /// * Current retention configuration
        ///
        /// **Security Note:** Requires authentication and Admin role.
        /// </remarks>
        /// <example>
        /// GET /api/settings/logs/statistics
        ///
        /// **Response (200 OK):**
        /// ```json
        /// {
        ///   "totalEntries": 1250,
        ///   "categoryCount": 15,
        ///   "oldestEntry": "2024-01-15T10:00:00Z",
        ///   "newestEntry": "2024-01-15T11:00:00Z",
        ///   "entriesByLevel": {
        ///     "Information": 1000,
        ///     "Warning": 200,
        ///     "Error": 50
        ///   },
        ///   "uniqueUserCount": 5,
        ///   "retentionMinutes": 60,
        ///   "maxEntriesPerCategory": 10000,
        ///   "maxTotalEntries": 50000
        /// }
        /// ```
        /// </example>
        /// <seealso cref="UserLoggerProvider"/>
        /// <seealso cref="LogStatistics"/>
        [HttpGet("logs/statistics")]
        [Authorize]
        [RequireUserRole(Admin)]
        [Produces("application/json")]
        [ProducesResponseType(typeof(LogStatistics), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public IActionResult GetLogStatistics()
        {
            #region implementation
            try
            {
                var statistics = _loggerProvider.GetStatistics();
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving log statistics");
                return StatusCode(500, new { error = "Error retrieving log statistics" });
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the list of available log categories with entry counts.
        /// </summary>
        /// <returns>
        /// List of categories with their entry counts and time ranges.
        /// </returns>
        /// <remarks>
        /// Use this endpoint to discover available categories before filtering logs.
        /// Categories correspond to logger names (typically class names).
        ///
        /// **Security Note:** Requires authentication and Admin role.
        /// </remarks>
        /// <example>
        /// GET /api/settings/logs/categories
        ///
        /// **Response (200 OK):**
        /// ```json
        /// [
        ///   {
        ///     "category": "MedRecPro.Controllers.LabelsController",
        ///     "entryCount": 150,
        ///     "oldestEntry": "2024-01-15T10:00:00Z",
        ///     "newestEntry": "2024-01-15T11:00:00Z"
        ///   },
        ///   {
        ///     "category": "MedRecPro.Service.ClaudeApiService",
        ///     "entryCount": 75,
        ///     "oldestEntry": "2024-01-15T10:30:00Z",
        ///     "newestEntry": "2024-01-15T10:55:00Z"
        ///   }
        /// ]
        /// ```
        /// </example>
        /// <seealso cref="UserLoggerProvider.GetCategories"/>
        /// <seealso cref="CategorySummary"/>
        [HttpGet("logs/categories")]
        [Authorize]
        [RequireUserRole(Admin)]
        [Produces("application/json")]
        [ProducesResponseType(typeof(List<CategorySummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public IActionResult GetLogCategories()
        {
            #region implementation
            try
            {
                var categories = _loggerProvider.GetCategories();
                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving log categories");
                return StatusCode(500, new { error = "Error retrieving log categories" });
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the list of users with log entries.
        /// </summary>
        /// <returns>
        /// List of users with their log entry counts and time ranges.
        /// </returns>
        /// <remarks>
        /// Use this endpoint to discover users who have generated log entries.
        /// Only includes logs where user context was captured (authenticated requests).
        ///
        /// **Security Note:** Requires authentication and Admin role.
        /// </remarks>
        /// <example>
        /// GET /api/settings/logs/users
        ///
        /// **Response (200 OK):**
        /// ```json
        /// [
        ///   {
        ///     "userId": "12345",
        ///     "userName": "john.doe@example.com",
        ///     "entryCount": 250,
        ///     "oldestEntry": "2024-01-15T09:00:00Z",
        ///     "newestEntry": "2024-01-15T11:00:00Z"
        ///   }
        /// ]
        /// ```
        /// </example>
        /// <seealso cref="UserLoggerProvider.GetUserSummaries"/>
        /// <seealso cref="UserLogSummary"/>
        [HttpGet("logs/users")]
        [Authorize]
        [RequireUserRole(Admin)]
        [Produces("application/json")]
        [ProducesResponseType(typeof(List<UserLogSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public IActionResult GetLogUsers()
        {
            #region implementation
            try
            {
                var users = _loggerProvider.GetUserSummaries();
                var encryptedUsers = users.Select(u => u.ToEntityWithEncryptedId(_pkEncryptionSecret, _logger)).ToList();
                return Ok(encryptedUsers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving log users");
                return StatusCode(500, new { error = "Error retrieving log users" });
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets all log entries with optional pagination.
        /// </summary>
        /// <param name="pageNumber">Page number (1-based). Default: 1.</param>
        /// <param name="pageSize">Number of entries per page. Default: 100. Max: 1000.</param>
        /// <param name="minLevel">Minimum log level to include (Trace, Debug, Information, Warning, Error, Critical).</param>
        /// <returns>
        /// Paginated list of log entries sorted by timestamp descending.
        /// </returns>
        /// <remarks>
        /// Returns all log entries currently in memory, newest first.
        /// Use the minLevel parameter to filter out lower-severity entries.
        ///
        /// **Security Note:** Requires authentication and Admin role.
        /// </remarks>
        /// <example>
        /// GET /api/settings/logs?pageNumber=1&amp;pageSize=50&amp;minLevel=Warning
        ///
        /// **Response (200 OK):**
        /// ```json
        /// {
        ///   "entries": [
        ///     {
        ///       "message": "Request completed",
        ///       "level": "Information",
        ///       "timestamp": "2024-01-15T11:00:00Z",
        ///       "category": "MedRecPro.Controllers.LabelsController",
        ///       "userId": "12345",
        ///       "userName": "john.doe@example.com"
        ///     }
        ///   ],
        ///   "totalCount": 1250,
        ///   "pageNumber": 1,
        ///   "pageSize": 50,
        ///   "totalPages": 25
        /// }
        /// ```
        /// </example>
        /// <seealso cref="UserLoggerProvider.GetLogs"/>
        /// <seealso cref="LogEntry"/>
        [HttpGet("logs")]
        [Authorize]
        [RequireUserRole(Admin)]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public IActionResult GetLogs(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] string? minLevel = null)
        {
            #region implementation
            try
            {
                // Validate and constrain parameters
                pageNumber = Math.Max(1, pageNumber);
                pageSize = Math.Clamp(pageSize, 1, 1000);

                // Get logs with optional level filtering
                IEnumerable<LogEntry> logs;
                if (!string.IsNullOrEmpty(minLevel) && Enum.TryParse<LogLevel>(minLevel, true, out var level))
                {
                    logs = _loggerProvider.GetLogsByLevel(level);
                }
                else
                {
                    logs = _loggerProvider.GetLogs();
                }

                var totalCount = logs.Count();
                var entries = logs
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new
                    {
                        e.Message,
                        Level = e.Level.ToString(),
                        e.Timestamp,
                        e.Category,
                        UserId = !string.IsNullOrEmpty(e.UserId)
                            ? StringCipher.Encrypt(e.UserId, _pkEncryptionSecret, StringCipher.EncryptionStrength.Fast)
                            : e.UserId,
                        e.UserName,
                        ExceptionMessage = e.Exception?.Message,
                        ExceptionType = e.Exception?.GetType().Name
                    })
                    .ToList();

                return Ok(new
                {
                    Entries = entries,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving logs");
                return StatusCode(500, new { error = "Error retrieving logs" });
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets log entries filtered by date range.
        /// </summary>
        /// <param name="startDate">Start of date range (UTC). Required.</param>
        /// <param name="endDate">End of date range (UTC). Required.</param>
        /// <param name="pageNumber">Page number (1-based). Default: 1.</param>
        /// <param name="pageSize">Number of entries per page. Default: 100. Max: 1000.</param>
        /// <returns>
        /// Paginated list of log entries within the specified date range.
        /// </returns>
        /// <remarks>
        /// Filters logs by timestamp within the specified UTC date range.
        /// Both startDate and endDate are required.
        ///
        /// **Security Note:** Requires authentication and Admin role.
        /// </remarks>
        /// <example>
        /// GET /api/settings/logs/by-date?startDate=2024-01-15T10:00:00Z&amp;endDate=2024-01-15T11:00:00Z
        ///
        /// **Response (200 OK):**
        /// ```json
        /// {
        ///   "entries": [...],
        ///   "totalCount": 150,
        ///   "pageNumber": 1,
        ///   "pageSize": 100,
        ///   "totalPages": 2,
        ///   "filter": {
        ///     "startDate": "2024-01-15T10:00:00Z",
        ///     "endDate": "2024-01-15T11:00:00Z"
        ///   }
        /// }
        /// ```
        /// </example>
        /// <seealso cref="UserLoggerProvider.GetLogsByDateRange"/>
        [HttpGet("logs/by-date")]
        [Authorize]
        [RequireUserRole(Admin)]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public IActionResult GetLogsByDate(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 100)
        {
            #region implementation
            try
            {
                // Validate date range
                if (startDate > endDate)
                {
                    return BadRequest(new { error = "startDate must be before endDate" });
                }

                // Validate and constrain parameters
                pageNumber = Math.Max(1, pageNumber);
                pageSize = Math.Clamp(pageSize, 1, 1000);

                // Ensure dates are UTC
                startDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
                endDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);

                var logs = _loggerProvider.GetLogsByDateRange(startDate, endDate);
                var totalCount = logs.Count;
                var entries = logs
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new
                    {
                        e.Message,
                        Level = e.Level.ToString(),
                        e.Timestamp,
                        e.Category,
                        UserId = !string.IsNullOrEmpty(e.UserId)
                            ? StringCipher.Encrypt(e.UserId, _pkEncryptionSecret, StringCipher.EncryptionStrength.Fast)
                            : e.UserId,
                        e.UserName,
                        ExceptionMessage = e.Exception?.Message,
                        ExceptionType = e.Exception?.GetType().Name
                    })
                    .ToList();

                return Ok(new
                {
                    Entries = entries,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    Filter = new
                    {
                        StartDate = startDate,
                        EndDate = endDate
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving logs by date");
                return StatusCode(500, new { error = "Error retrieving logs by date" });
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets log entries filtered by category.
        /// </summary>
        /// <param name="category">Category name to filter by (case-insensitive partial match). Required.</param>
        /// <param name="pageNumber">Page number (1-based). Default: 1.</param>
        /// <param name="pageSize">Number of entries per page. Default: 100. Max: 1000.</param>
        /// <returns>
        /// Paginated list of log entries matching the specified category.
        /// </returns>
        /// <remarks>
        /// Filters logs by category name using case-insensitive partial matching.
        /// For example, "Controller" will match "MedRecPro.Controllers.LabelsController".
        ///
        /// Use GET /api/settings/logs/categories to discover available categories.
        ///
        /// **Security Note:** Requires authentication and Admin role.
        /// </remarks>
        /// <example>
        /// GET /api/settings/logs/by-category?category=ClaudeApiService
        ///
        /// **Response (200 OK):**
        /// ```json
        /// {
        ///   "entries": [...],
        ///   "totalCount": 75,
        ///   "pageNumber": 1,
        ///   "pageSize": 100,
        ///   "totalPages": 1,
        ///   "filter": {
        ///     "category": "ClaudeApiService"
        ///   }
        /// }
        /// ```
        /// </example>
        /// <seealso cref="UserLoggerProvider.GetLogsByCategory"/>
        /// <seealso cref="GetLogCategories"/>
        [HttpGet("logs/by-category")]
        [Authorize]
        [RequireUserRole(Admin)]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public IActionResult GetLogsByCategory(
            [FromQuery] string category,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 100)
        {
            #region implementation
            try
            {
                // Validate category
                if (string.IsNullOrWhiteSpace(category))
                {
                    return BadRequest(new { error = "Category parameter is required" });
                }

                // Validate and constrain parameters
                pageNumber = Math.Max(1, pageNumber);
                pageSize = Math.Clamp(pageSize, 1, 1000);

                var logs = _loggerProvider.GetLogsByCategory(category);
                var totalCount = logs.Count;
                var entries = logs
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new
                    {
                        e.Message,
                        Level = e.Level.ToString(),
                        e.Timestamp,
                        e.Category,
                        UserId = !string.IsNullOrEmpty(e.UserId)
                            ? StringCipher.Encrypt(e.UserId, _pkEncryptionSecret, StringCipher.EncryptionStrength.Fast)
                            : e.UserId,
                        e.UserName,
                        ExceptionMessage = e.Exception?.Message,
                        ExceptionType = e.Exception?.GetType().Name
                    })
                    .ToList();

                return Ok(new
                {
                    Entries = entries,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    Filter = new
                    {
                        Category = category
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving logs by category");
                return StatusCode(500, new { error = "Error retrieving logs by category" });
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets log entries filtered by user ID.
        /// </summary>
        /// <param name="userId">Encrypted user ID to filter by. Required. Obtain from GET /api/settings/logs/users.</param>
        /// <param name="pageNumber">Page number (1-based). Default: 1.</param>
        /// <param name="pageSize">Number of entries per page. Default: 100. Max: 1000.</param>
        /// <returns>
        /// Paginated list of log entries for the specified user.
        /// </returns>
        /// <remarks>
        /// Filters logs by the authenticated user's ID that was captured at log time.
        /// Only logs with user context captured will be included.
        ///
        /// The userId parameter should be the encrypted user ID returned from
        /// GET /api/settings/logs/users. The endpoint will decrypt it internally
        /// before querying. For backwards compatibility, plain text IDs are also
        /// accepted but will be logged as warnings.
        ///
        /// Use GET /api/settings/logs/users to discover users with log entries.
        ///
        /// **Security Note:** Requires authentication and Admin role.
        /// </remarks>
        /// <example>
        /// GET /api/settings/logs/by-user?userId={encryptedUserId}
        ///
        /// **Response (200 OK):**
        /// ```json
        /// {
        ///   "entries": [...],
        ///   "totalCount": 250,
        ///   "pageNumber": 1,
        ///   "pageSize": 100,
        ///   "totalPages": 3,
        ///   "filter": {
        ///     "userId": "{encryptedUserId}"
        ///   }
        /// }
        /// ```
        /// </example>
        /// <seealso cref="UserLoggerProvider.GetLogsByUser"/>
        /// <seealso cref="GetLogUsers"/>
        [HttpGet("logs/by-user")]
        [Authorize]
        [RequireUserRole(Admin)]
        [Produces("application/json")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public IActionResult GetLogsByUser(
            [FromQuery] string userId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 100)
        {
            #region implementation
            try
            {
                // Validate userId
                if (string.IsNullOrWhiteSpace(userId))
                {
                    return BadRequest(new { error = "UserId parameter is required" });
                }

                // Decrypt the userId parameter (it comes in encrypted from the client)
                string decryptedUserId;
                try
                {
                    decryptedUserId = userId.Decrypt(_pkEncryptionSecret) ?? userId;
                }
                catch (Exception decryptEx)
                {
                    _logger.LogWarning(decryptEx, "Failed to decrypt userId parameter. It may be in plain text format.");
                    // Fall back to using the userId as-is (for backwards compatibility or plain text usage)
                    decryptedUserId = userId;
                }

                // Validate and constrain parameters
                pageNumber = Math.Max(1, pageNumber);
                pageSize = Math.Clamp(pageSize, 1, 1000);

                var logs = _loggerProvider.GetLogsByUser(decryptedUserId);
                var totalCount = logs.Count;
                // Re-encrypt for consistent response format (use the decrypted value to ensure consistency)
                var encryptedFilterUserId = !string.IsNullOrEmpty(decryptedUserId)
                    ? StringCipher.Encrypt(decryptedUserId, _pkEncryptionSecret, StringCipher.EncryptionStrength.Fast)
                    : decryptedUserId;
                var entries = logs
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(e => new
                    {
                        e.Message,
                        Level = e.Level.ToString(),
                        e.Timestamp,
                        e.Category,
                        UserId = !string.IsNullOrEmpty(e.UserId)
                            ? StringCipher.Encrypt(e.UserId, _pkEncryptionSecret, StringCipher.EncryptionStrength.Fast)
                            : e.UserId,
                        e.UserName,
                        ExceptionMessage = e.Exception?.Message,
                        ExceptionType = e.Exception?.GetType().Name
                    })
                    .ToList();

                return Ok(new
                {
                    Entries = entries,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    Filter = new
                    {
                        UserId = encryptedFilterUserId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving logs by user");
                return StatusCode(500, new { error = "Error retrieving logs by user" });
            }
            #endregion
        }

        #endregion
    }
}
