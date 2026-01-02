using MedRecProConsole.Models;
using Microsoft.Extensions.Configuration;

namespace MedRecProConsole.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Helper class for building and managing application configuration.
    /// Provides configuration required for MedRecPro services including encryption keys
    /// and feature flags.
    /// </summary>
    /// <remarks>
    /// Centralizes configuration management for the console application.
    /// Loads settings from appsettings.json and provides strongly-typed access
    /// through the ConsoleAppSettings model.
    /// </remarks>
    /// <seealso cref="IConfiguration"/>
    /// <seealso cref="ConsoleConsoleAppSettings"/>
    /// <seealso cref="Services.ImportService"/>
    public static class ConfigurationHelper
    {
        #region private fields

        /// <summary>
        /// Cached application settings instance.
        /// </summary>
        private static ConsoleAppSettings? _appSettings;

        /// <summary>
        /// Cached configuration instance.
        /// </summary>
        private static IConfiguration? _configuration;

        /// <summary>
        /// Lock object for thread-safe initialization.
        /// </summary>
        private static readonly object _lock = new();

        /// <summary>
        /// Path to an alternate configuration file (set via --config argument).
        /// </summary>
        private static string? _alternateConfigPath;

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Gets the application settings from appsettings.json.
        /// </summary>
        /// <returns>Populated ConsoleAppSettings instance</returns>
        /// <remarks>
        /// Settings are cached after first load for performance.
        /// </remarks>
        /// <seealso cref="ConsoleConsoleAppSettings"/>
        public static ConsoleAppSettings GetConsoleAppSettings()
        {
            #region implementation

            if (_appSettings == null)
            {
                lock (_lock)
                {
                    if (_appSettings == null)
                    {
                        var config = GetConfiguration();
                        _appSettings = bindConsoleAppSettings(config);
                    }
                }
            }

            return _appSettings;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the raw IConfiguration from appsettings.json.
        /// </summary>
        /// <returns>IConfiguration instance</returns>
        /// <remarks>
        /// Configuration is cached after first load for performance.
        /// </remarks>
        /// <seealso cref="IConfiguration"/>
        public static IConfiguration GetConfiguration()
        {
            #region implementation

            if (_configuration == null)
            {
                lock (_lock)
                {
                    if (_configuration == null)
                    {
                        _configuration = buildConfigurationFromFile();
                    }
                }
            }

            return _configuration;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the configuration required for MedRecPro services.
        /// Uses settings from appsettings.json.
        /// </summary>
        /// <returns>IConfiguration with required settings for MedRecPro</returns>
        /// <remarks>
        /// Includes the Security:DB:PKSecret key required by Repository and SplDataService.
        /// Feature flags are loaded from appsettings.json.
        /// </remarks>
        /// <seealso cref="IConfiguration"/>
        public static IConfiguration BuildMedRecProConfiguration()
        {
            #region implementation

            var settings = GetConsoleAppSettings();

            return BuildMedRecProConfiguration(
                settings.Security.EncryptionKey,
                settings.FeatureFlags.UseBulkOperations,
                settings.FeatureFlags.UseBulkStagingOperations,
                settings.FeatureFlags.UseBatchSaving,
                settings.FeatureFlags.UseEnhancedDebugging);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the configuration required for MedRecPro services with a custom encryption key.
        /// </summary>
        /// <param name="encryptionKey">Custom encryption key for database ID encryption</param>
        /// <returns>IConfiguration with required settings</returns>
        /// <remarks>
        /// Allows specifying a custom encryption key to match the main application's key.
        /// </remarks>
        /// <seealso cref="IConfiguration"/>
        public static IConfiguration BuildMedRecProConfiguration(string encryptionKey)
        {
            #region implementation

            var settings = GetConsoleAppSettings();

            return BuildMedRecProConfiguration(
                encryptionKey,
                settings.FeatureFlags.UseBulkOperations,
                settings.FeatureFlags.UseBulkStagingOperations,
                settings.FeatureFlags.UseBatchSaving,
                settings.FeatureFlags.UseEnhancedDebugging);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds configuration with custom feature flags for MedRecPro services.
        /// </summary>
        /// <param name="encryptionKey">Encryption key for database ID encryption</param>
        /// <param name="useBulkOperations">Enable bulk database operations</param>
        /// <param name="useBulkStagingOperations">Enable staged bulk operations</param>
        /// <param name="useBatchSaving">Enable batch saving mode</param>
        /// <param name="useEnhancedDebugging">Enable enhanced debugging output</param>
        /// <returns>IConfiguration with specified settings</returns>
        /// <remarks>
        /// Provides full control over feature flags for testing or specific scenarios.
        /// </remarks>
        /// <seealso cref="IConfiguration"/>
        public static IConfiguration BuildMedRecProConfiguration(
            string encryptionKey,
            bool useBulkOperations,
            bool useBulkStagingOperations,
            bool useBatchSaving,
            bool useEnhancedDebugging)
        {
            #region implementation

            var configValues = new Dictionary<string, string?>
            {
                { "Security:DB:PKSecret", encryptionKey },
                { "FeatureFlags:UseBulkOperations", useBulkOperations.ToString().ToLowerInvariant() },
                { "FeatureFlags:UseBulkStagingOperations", useBulkStagingOperations.ToString().ToLowerInvariant() },
                { "FeatureFlags:UseBatchSaving", useBatchSaving.ToString().ToLowerInvariant() },
                { "FeatureFlags:UseEnhancedDebugging", useEnhancedDebugging.ToString().ToLowerInvariant() }
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Reloads the configuration from appsettings.json.
        /// </summary>
        /// <remarks>
        /// Use this method to refresh settings after modifying appsettings.json
        /// while the application is running.
        /// </remarks>
        public static void ReloadConfiguration()
        {
            #region implementation

            lock (_lock)
            {
                _configuration = null;
                _appSettings = null;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Sets an alternate configuration file path for loading settings.
        /// Must be called before GetConsoleAppSettings() or GetConfiguration().
        /// </summary>
        /// <param name="configPath">Full path to the alternate configuration file</param>
        /// <returns>True if the file exists and path was set, false otherwise</returns>
        /// <remarks>
        /// Use this for automation scenarios where a separate config file contains
        /// job-specific settings. Call ReloadConfiguration() after setting this
        /// if configuration was already loaded.
        /// </remarks>
        /// <example>
        /// ConfigurationHelper.SetAlternateConfigPath("C:\Jobs\daily-import.json");
        /// var settings = ConfigurationHelper.GetConsoleAppSettings();
        /// </example>
        public static bool SetAlternateConfigPath(string configPath)
        {
            #region implementation

            if (!File.Exists(configPath))
            {
                return false;
            }

            lock (_lock)
            {
                _alternateConfigPath = configPath;
                // Force reload on next access
                _configuration = null;
                _appSettings = null;
            }

            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the currently configured alternate config path, if any.
        /// </summary>
        /// <returns>The alternate config path, or null if using default appsettings.json</returns>
        public static string? GetAlternateConfigPath()
        {
            #region implementation

            return _alternateConfigPath;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Clears the alternate configuration path and reverts to default appsettings.json.
        /// </summary>
        /// <remarks>
        /// Call ReloadConfiguration() after this to reload from default settings.
        /// </remarks>
        public static void ClearAlternateConfigPath()
        {
            #region implementation

            lock (_lock)
            {
                _alternateConfigPath = null;
                _configuration = null;
                _appSettings = null;
            }

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Builds the configuration from appsettings.json file or alternate config path.
        /// </summary>
        /// <returns>IConfiguration loaded from file</returns>
        /// <remarks>
        /// If an alternate config path is set, it layers on top of the default appsettings.json.
        /// This allows the alternate file to override only specific settings while inheriting
        /// the rest from appsettings.json.
        /// </remarks>
        private static IConfiguration buildConfigurationFromFile()
        {
            #region implementation

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

            // Layer alternate config on top of defaults if specified
            if (!string.IsNullOrEmpty(_alternateConfigPath) && File.Exists(_alternateConfigPath))
            {
                builder.AddJsonFile(_alternateConfigPath, optional: false, reloadOnChange: false);
            }

            return builder.Build();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Binds configuration to strongly-typed ConsoleAppSettings model.
        /// </summary>
        /// <param name="configuration">Configuration to bind</param>
        /// <returns>Populated ConsoleAppSettings instance</returns>
        /// <seealso cref="ConsoleConsoleAppSettings"/>
        private static ConsoleAppSettings bindConsoleAppSettings(IConfiguration configuration)
        {
            #region implementation

            var settings = new ConsoleAppSettings();

            // Bind Application section
            var appSection = configuration.GetSection("Application");
            if (appSection.Exists())
            {
                settings.Application.Version = appSection["Version"] ?? settings.Application.Version;
                settings.Application.Name = appSection["Name"] ?? settings.Application.Name;
                settings.Application.Description = appSection["Description"] ?? settings.Application.Description;
            }

            // Bind DatabaseConnections section
            var dbSection = configuration.GetSection("DatabaseConnections");
            if (dbSection.Exists())
            {
                settings.DatabaseConnections = dbSection.Get<List<DatabaseConnectionSettings>>() ?? new();
            }

            // Bind ImportSettings section
            var importSection = configuration.GetSection("ImportSettings");
            if (importSection.Exists())
            {
                var defaultRuntime = importSection["DefaultMaxRuntimeMinutes"];
                if (!string.IsNullOrEmpty(defaultRuntime) && int.TryParse(defaultRuntime, out var runtime))
                {
                    settings.ImportSettings.DefaultMaxRuntimeMinutes = runtime;
                }

                settings.ImportSettings.ValidFileExtensions =
                    importSection.GetSection("ValidFileExtensions").Get<List<string>>() ??
                    settings.ImportSettings.ValidFileExtensions;

                if (int.TryParse(importSection["MaxDisplayedErrors"], out var maxErrors))
                {
                    settings.ImportSettings.MaxDisplayedErrors = maxErrors;
                }

                if (bool.TryParse(importSection["RequireConfirmation"], out var requireConfirm))
                {
                    settings.ImportSettings.RequireConfirmation = requireConfirm;
                }

                if (bool.TryParse(importSection["ContinueOnError"], out var continueOnError))
                {
                    settings.ImportSettings.ContinueOnError = continueOnError;
                }
            }

            // Bind Logging section
            var loggingSection = configuration.GetSection("Logging");
            if (loggingSection.Exists())
            {
                settings.Logging.MinimumLevel = loggingSection["MinimumLevel"] ?? settings.Logging.MinimumLevel;
                settings.Logging.LogFilePath = loggingSection["LogFilePath"] ?? settings.Logging.LogFilePath;

                if (bool.TryParse(loggingSection["EnableConsoleLogging"], out var enableConsole))
                {
                    settings.Logging.EnableConsoleLogging = enableConsole;
                }

                if (bool.TryParse(loggingSection["EnableFileLogging"], out var enableFile))
                {
                    settings.Logging.EnableFileLogging = enableFile;
                }

                if (int.TryParse(loggingSection["MaxLogFileSizeMB"], out var maxSize))
                {
                    settings.Logging.MaxLogFileSizeMB = maxSize;
                }

                if (int.TryParse(loggingSection["RetainedLogFiles"], out var retained))
                {
                    settings.Logging.RetainedLogFiles = retained;
                }
            }

            // Bind Security section
            var securitySection = configuration.GetSection("Security");
            if (securitySection.Exists())
            {
                settings.Security.EncryptionKey = securitySection["EncryptionKey"] ?? settings.Security.EncryptionKey;
            }

            // Bind FeatureFlags section
            var flagsSection = configuration.GetSection("FeatureFlags");
            if (flagsSection.Exists())
            {
                if (bool.TryParse(flagsSection["UseBulkOperations"], out var useBulk))
                {
                    settings.FeatureFlags.UseBulkOperations = useBulk;
                }

                if (bool.TryParse(flagsSection["UseBulkStagingOperations"], out var useStaging))
                {
                    settings.FeatureFlags.UseBulkStagingOperations = useStaging;
                }

                if (bool.TryParse(flagsSection["UseBatchSaving"], out var useBatch))
                {
                    settings.FeatureFlags.UseBatchSaving = useBatch;
                }

                if (bool.TryParse(flagsSection["UseEnhancedDebugging"], out var useDebug))
                {
                    settings.FeatureFlags.UseEnhancedDebugging = useDebug;
                }
            }

            // Bind Display section
            var displaySection = configuration.GetSection("Display");
            if (displaySection.Exists())
            {
                if (bool.TryParse(displaySection["ClearConsoleOnStart"], out var clearConsole))
                {
                    settings.Display.ClearConsoleOnStart = clearConsole;
                }

                if (bool.TryParse(displaySection["ShowBanner"], out var showBanner))
                {
                    settings.Display.ShowBanner = showBanner;
                }

                settings.Display.BannerColor = displaySection["BannerColor"] ?? settings.Display.BannerColor;

                if (bool.TryParse(displaySection["ShowProgressBars"], out var showProgress))
                {
                    settings.Display.ShowProgressBars = showProgress;
                }

                if (bool.TryParse(displaySection["ShowSpinners"], out var showSpinners))
                {
                    settings.Display.ShowSpinners = showSpinners;
                }
            }

            // Bind Help section
            var helpSection = configuration.GetSection("Help");
            if (helpSection.Exists())
            {
                settings.Help.Topics = helpSection.GetSection("Topics").Get<List<HelpTopic>>() ?? new();
                settings.Help.CommandLineOptions = helpSection.GetSection("CommandLineOptions").Get<List<CommandLineOption>>() ?? new();
            }

            // Bind Automation section
            var automationSection = configuration.GetSection("Automation");
            if (automationSection.Exists())
            {
                if (bool.TryParse(automationSection["AutoQuitOnCompletion"], out var autoQuit))
                {
                    settings.Automation.AutoQuitOnCompletion = autoQuit;
                }

                settings.Automation.DefaultImportFolder = automationSection["DefaultImportFolder"];
                settings.Automation.DefaultConnectionName = automationSection["DefaultConnectionName"];

                var runtimeStr = automationSection["DefaultMaxRuntimeMinutes"];
                if (!string.IsNullOrEmpty(runtimeStr) && int.TryParse(runtimeStr, out var runtime))
                {
                    settings.Automation.DefaultMaxRuntimeMinutes = runtime;
                }

                if (bool.TryParse(automationSection["SuppressConfirmations"], out var suppress))
                {
                    settings.Automation.SuppressConfirmations = suppress;
                }

                if (bool.TryParse(automationSection["EnableUnattendedLogging"], out var enableLogging))
                {
                    settings.Automation.EnableUnattendedLogging = enableLogging;
                }

                settings.Automation.UnattendedLogPath = automationSection["UnattendedLogPath"] ?? settings.Automation.UnattendedLogPath;
            }

            return settings;

            #endregion
        }

        #endregion
    }
}
