
using Microsoft.AspNetCore.Mvc;
using MedRecPro.Helpers;
using Microsoft.Extensions.Configuration;


namespace MedRecPro.Controllers
{
    /**************************************************************/
    /// <summary>
    /// API controller for exposing application settings to clients.
    /// </summary>
    /// <remarks>
    /// Provides read-only access to non-sensitive configuration settings
    /// that clients need to adapt their behavior (e.g., demo mode status).
    /// </remarks>
    [ApiController]
    public class SettingsController : ApiControllerBase
    {
        #region fields

        private readonly IConfiguration _configuration;
        private readonly ILogger<SettingsController> _logger;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SettingsController class.
        /// </summary>
        /// <param name="configuration">Application configuration provider.</param>
        /// <param name="logger">Logger instance for this controller.</param>
        /// <exception cref="ArgumentNullException">Thrown when configuration or logger is null.</exception>
        /// <seealso cref="IConfiguration"/>
        /// <seealso cref="ILogger{TCategoryName}"/>
        public SettingsController(
            IConfiguration configuration,
            ILogger<SettingsController> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    }
}
