namespace MedRecProConsole.Models
{
    /**************************************************************/
    /// <summary>
    /// Root configuration model for the application settings.
    /// Maps to the structure defined in appsettings.json.
    /// </summary>
    /// <remarks>
    /// Use ConfigurationHelper.GetAppSettings() to retrieve a populated instance.
    /// </remarks>
    /// <seealso cref="ApplicationSettings"/>
    /// <seealso cref="DatabaseConnectionSettings"/>
    /// <seealso cref="ImportSettings"/>
    /// <seealso cref="Helpers.ConfigurationHelper"/>
    public class ConsoleAppSettings
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the application metadata settings.
        /// </summary>
        public ApplicationSettings Application { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the list of predefined database connections.
        /// </summary>
        public List<DatabaseConnectionSettings> DatabaseConnections { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the import operation settings.
        /// </summary>
        public ImportSettings ImportSettings { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the logging configuration.
        /// </summary>
        public LoggingSettings Logging { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the security settings.
        /// </summary>
        public SecuritySettings Security { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the feature flags.
        /// </summary>
        public FeatureFlagSettings FeatureFlags { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the display settings.
        /// </summary>
        public DisplaySettings Display { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the help documentation settings.
        /// </summary>
        public HelpSettings Help { get; set; } = new();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Application metadata settings including version and name.
    /// </summary>
    /// <seealso cref="ConsoleAppSettings"/>
    public class ApplicationSettings
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the application version string.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /**************************************************************/
        /// <summary>
        /// Gets or sets the application display name.
        /// </summary>
        public string Name { get; set; } = "MedRecPro Console";

        /**************************************************************/
        /// <summary>
        /// Gets or sets the application description.
        /// </summary>
        public string Description { get; set; } = "Bulk SPL ZIP Import Utility";

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Database connection configuration for predefined connections.
    /// </summary>
    /// <seealso cref="ConsoleAppSettings"/>
    public class DatabaseConnectionSettings
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the display name for this connection.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the full connection string.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether this is the default connection.
        /// </summary>
        public bool IsDefault { get; set; }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Import operation configuration settings.
    /// </summary>
    /// <seealso cref="ConsoleAppSettings"/>
    public class ImportSettings
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the default maximum runtime in minutes.
        /// Null indicates no limit.
        /// </summary>
        public int? DefaultMaxRuntimeMinutes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the valid file extensions to process within ZIP files.
        /// </summary>
        public List<string> ValidFileExtensions { get; set; } = new() { ".xml" };

        /**************************************************************/
        /// <summary>
        /// Gets or sets the maximum number of errors to display in the console.
        /// </summary>
        public int MaxDisplayedErrors { get; set; } = 50;

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether to require user confirmation before import.
        /// </summary>
        public bool RequireConfirmation { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether to continue processing after a ZIP failure.
        /// </summary>
        public bool ContinueOnError { get; set; } = true;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Logging configuration settings.
    /// </summary>
    /// <seealso cref="ConsoleAppSettings"/>
    public class LoggingSettings
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the minimum log level.
        /// Valid values: Trace, Debug, Information, Warning, Error, Critical
        /// </summary>
        public string MinimumLevel { get; set; } = "Warning";

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether console logging is enabled.
        /// </summary>
        public bool EnableConsoleLogging { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether file logging is enabled.
        /// </summary>
        public bool EnableFileLogging { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the log file path.
        /// </summary>
        public string LogFilePath { get; set; } = "logs/import.log";

        /**************************************************************/
        /// <summary>
        /// Gets or sets the maximum log file size in MB before rotation.
        /// </summary>
        public int MaxLogFileSizeMB { get; set; } = 10;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of log files to retain.
        /// </summary>
        public int RetainedLogFiles { get; set; } = 5;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Security configuration settings.
    /// </summary>
    /// <seealso cref="ConsoleAppSettings"/>
    public class SecuritySettings
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the encryption key for database ID encryption.
        /// </summary>
        /// <remarks>
        /// This should match the key used by the main MedRecPro application.
        /// </remarks>
        public string EncryptionKey { get; set; } = "MedRecProConsoleImportKey2024!";

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Feature flag configuration settings.
    /// </summary>
    /// <seealso cref="ConsoleAppSettings"/>
    public class FeatureFlagSettings
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether to use bulk database operations.
        /// </summary>
        public bool UseBulkOperations { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether to use staged bulk operations.
        /// </summary>
        public bool UseBulkStagingOperations { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether to use batch saving mode.
        /// </summary>
        public bool UseBatchSaving { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether to enable enhanced debugging output.
        /// </summary>
        public bool UseEnhancedDebugging { get; set; }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Display configuration settings for console UI.
    /// </summary>
    /// <seealso cref="ConsoleAppSettings"/>
    public class DisplaySettings
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether to clear the console on startup.
        /// </summary>
        public bool ClearConsoleOnStart { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether to show the figlet banner.
        /// </summary>
        public bool ShowBanner { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the banner color.
        /// </summary>
        public string BannerColor { get; set; } = "Blue";

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether to show progress bars.
        /// </summary>
        public bool ShowProgressBars { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether to show spinner animations.
        /// </summary>
        public bool ShowSpinners { get; set; } = true;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Help documentation configuration settings.
    /// </summary>
    /// <seealso cref="ConsoleAppSettings"/>
    public class HelpSettings
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the help topics.
        /// </summary>
        public List<HelpTopic> Topics { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the command line options documentation.
        /// </summary>
        public List<CommandLineOption> CommandLineOptions { get; set; } = new();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// A single help topic with title and description.
    /// </summary>
    /// <seealso cref="HelpSettings"/>
    public class HelpTopic
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the topic title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the topic description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// A command line option with its description.
    /// </summary>
    /// <seealso cref="HelpSettings"/>
    public class CommandLineOption
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the option flags (e.g., "--help, -h").
        /// </summary>
        public string Option { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the option description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        #endregion
    }
}
