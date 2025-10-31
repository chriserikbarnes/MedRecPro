using MedRecPro.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MedRecPro.Services
{
    /**************************************************************/
    /// <summary>
    /// Background service that manages demo mode functionality, including scheduled database truncation
    /// to maintain a clean demo environment while preserving user accounts and activity logs.
    /// </summary>
    /// <remarks>
    /// This service runs in the background when demo mode is enabled and executes database
    /// truncation at specified intervals to prevent junk data accumulation during public previews.
    /// All AspNet authentication tables and activity logs are preserved.
    /// </remarks>
    /// <example>
    /// Register in Program.cs or Startup.cs:
    /// <code>
    /// services.AddHostedService&lt;DemoModeService&gt;();
    /// </code>
    /// </example>
    /// <seealso cref="IHostedService"/>
    /// <seealso cref="IConfiguration"/>
    public class DemoModeService : IHostedService, IDisposable
    {
        #region fields

        private readonly ILogger<DemoModeService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private Timer? _timer;
        private bool _isEnabled;
        private int _refreshIntervalMinutes;
        private bool _autoTruncateOnStartup;
        private List<string>? _preserveTables;
        private bool _disposed;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the DemoModeService class.
        /// </summary>
        /// <param name="logger">Logger instance for tracking service operations and errors.</param>
        /// <param name="configuration">Application configuration containing demo mode settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or configuration is null.</exception>
        /// <seealso cref="ILogger{TCategoryName}"/>
        /// <seealso cref="IConfiguration"/>
        public DemoModeService(
            ILogger<DemoModeService> logger,
            IConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            string? connectionStringBackup;

            #region implementation

#if DEBUG
            connectionStringBackup = _configuration.GetSection("Dev:DB:Connection");
#else
            connectionStringBackup = _configuration.GetConnectionString("Prod:DB:Connection");
#endif

            // Load connection string from configuration
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                   ?? connectionStringBackup
                   ?? throw new InvalidOperationException("DefaultConnection string not found in configuration.");

            // Load demo mode settings
            loadDemoModeSettings();

            #endregion
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Starts the demo mode service and initializes the truncation timer if enabled.
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous start operation.</returns>
        /// <remarks>
        /// If AutoTruncateOnStartup is enabled and demo mode is active, this method will
        /// execute an immediate database truncation before starting the scheduled interval.
        /// </remarks>
        /// <seealso cref="StopAsync"/>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            #region implementation

            if (!_isEnabled)
            {
                _logger.LogInformation("Demo mode is disabled. DemoModeService will not run.");
                return;
            }

            _logger.LogInformation(
                "Demo mode is enabled. Database truncation will occur every {Interval} minutes.",
                _refreshIntervalMinutes);

            // Execute immediate truncation on startup if configured
            if (_autoTruncateOnStartup)
            {
                _logger.LogInformation("AutoTruncateOnStartup is enabled. Executing initial truncation...");
                await executeTruncation(cancellationToken);
            }

            // Setup timer for periodic truncation
            var interval = TimeSpan.FromMinutes(_refreshIntervalMinutes);
            _timer = new Timer(
                callback: async _ => await executeTruncation(CancellationToken.None),
                state: null,
                dueTime: interval,
                period: interval);

            _logger.LogInformation("Demo mode service started successfully.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stops the demo mode service and disposes of the timer.
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous stop operation.</returns>
        /// <seealso cref="StartAsync"/>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            #region implementation

            _logger.LogInformation("Demo mode service is stopping...");

            // Stop and dispose the timer
            _timer?.Change(Timeout.Infinite, 0);
            _timer?.Dispose();
            _timer = null;

            _logger.LogInformation("Demo mode service stopped.");

            return Task.CompletedTask;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Releases all resources used by the DemoModeService.
        /// </summary>
        /// <seealso cref="IDisposable"/>
        public void Dispose()
        {
            #region implementation

            if (_disposed)
                return;

            _timer?.Dispose();
            _disposed = true;

            GC.SuppressFinalize(this);

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Loads demo mode configuration settings from appsettings.json.
        /// </summary>
        /// <remarks>
        /// Reads the DemoModeSettings section and populates internal fields with configuration values.
        /// Provides default values if settings are not specified.
        /// </remarks>
        /// <seealso cref="IConfiguration"/>
        private void loadDemoModeSettings()
        {
            #region implementation

            var demoSettings = _configuration.GetSection("DemoModeSettings");

            _isEnabled = demoSettings.GetValue<bool>("Enabled", false);
            _refreshIntervalMinutes = demoSettings.GetValue<int>("RefreshIntervalMinutes", 60);
            _autoTruncateOnStartup = demoSettings.GetValue<bool>("AutoTruncateOnStartup", false);

            // Load preserved tables list
            _preserveTables = demoSettings.GetSection("PreserveTables")
                .Get<List<string>>() ?? new List<string>
                {
                    "__EFMigrationsHistory",
                    "AspNetUsers",
                    "AspNetRoles",
                    "AspNetUserRoles",
                    "AspNetUserClaims",
                    "AspNetRoleClaims",
                    "AspNetUserLogins",
                    "AspNetUserTokens",
                    "AspNetUserActivityLog"
                };

            // Validate refresh interval
            if (_refreshIntervalMinutes < 1)
            {
                _logger.LogWarning(
                    "RefreshIntervalMinutes value of {Value} is invalid. Using default of 60 minutes.",
                    _refreshIntervalMinutes);
                _refreshIntervalMinutes = 60;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Executes the database truncation operation.
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous truncation operation.</returns>
        /// <remarks>
        /// This method connects to the database, disables foreign key constraints,
        /// truncates all tables except those in the preserve list, and re-enables constraints.
        /// All operations are wrapped in a transaction for safety.
        /// </remarks>
        /// <seealso cref="buildTruncationScript"/>
        private async Task executeTruncation(CancellationToken cancellationToken)
        {
            #region implementation

            try
            {
                _logger.LogInformation("Starting scheduled database truncation for demo mode...");

                // Build the dynamic SQL script
                var sqlScript = buildTruncationScript();

                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);

                await using var command = new SqlCommand(sqlScript, connection)
                {
                    CommandTimeout = 300 // 5 minutes timeout
                };

                // Execute the truncation script
                await command.ExecuteNonQueryAsync(cancellationToken);

                _logger.LogInformation("Database truncation completed successfully.");
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(
                    sqlEx,
                    "SQL error occurred during database truncation: {Message}",
                    sqlEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error occurred during database truncation: {Message}",
                    ex.Message);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the SQL truncation script dynamically based on preserved tables configuration.
        /// </summary>
        /// <returns>A SQL script string that truncates all tables except preserved ones.</returns>
        /// <remarks>
        /// The generated script includes transaction management, foreign key constraint handling,
        /// and conditional logic to skip preserved tables. The script is safe to execute
        /// multiple times and includes rollback on error.
        /// </remarks>
        /// <seealso cref="executeTruncation"/>
        private string buildTruncationScript()
        {
            #region implementation

            var sb = new StringBuilder();

            // Build WHERE clause for excluded tables
            var excludeConditions = _preserveTables
                .Select(table => $"t.name <> '{table}'")
                .ToList();

            var whereClause = string.Join(" AND ", excludeConditions);

            // Build the dynamic SQL script
            sb.AppendLine("BEGIN TRANSACTION;");
            sb.AppendLine("BEGIN TRY");
            sb.AppendLine();
            sb.AppendLine("    -- Disable all foreign key constraints");
            sb.AppendLine("    EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';");
            sb.AppendLine();
            sb.AppendLine("    -- Truncate tables");
            sb.AppendLine("    DECLARE @tableName NVARCHAR(MAX);");
            sb.AppendLine("    DECLARE @schemaName NVARCHAR(MAX);");
            sb.AppendLine();
            sb.AppendLine("    DECLARE table_cursor CURSOR FOR");
            sb.AppendLine("    SELECT s.name, t.name");
            sb.AppendLine("    FROM sys.tables t");
            sb.AppendLine("    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id");
            sb.AppendLine("    WHERE t.type = 'U'");
            sb.AppendLine($"      AND {whereClause};");
            sb.AppendLine();
            sb.AppendLine("    OPEN table_cursor;");
            sb.AppendLine("    FETCH NEXT FROM table_cursor INTO @schemaName, @tableName;");
            sb.AppendLine();
            sb.AppendLine("    WHILE @@FETCH_STATUS = 0");
            sb.AppendLine("    BEGIN");
            sb.AppendLine("        DECLARE @fullTableName NVARCHAR(MAX) = QUOTENAME(@schemaName) + '.' + QUOTENAME(@tableName);");
            sb.AppendLine("        DECLARE @truncateCommand NVARCHAR(MAX) = 'TRUNCATE TABLE ' + @fullTableName;");
            sb.AppendLine("        EXEC sp_executesql @truncateCommand;");
            sb.AppendLine("        FETCH NEXT FROM table_cursor INTO @schemaName, @tableName;");
            sb.AppendLine("    END");
            sb.AppendLine();
            sb.AppendLine("    CLOSE table_cursor;");
            sb.AppendLine("    DEALLOCATE table_cursor;");
            sb.AppendLine();
            sb.AppendLine("    -- Re-enable all foreign key constraints");
            sb.AppendLine("    EXEC sp_msforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';");
            sb.AppendLine();
            sb.AppendLine("    COMMIT TRANSACTION;");
            sb.AppendLine("END TRY");
            sb.AppendLine("BEGIN CATCH");
            sb.AppendLine("    IF @@TRANCOUNT > 0");
            sb.AppendLine("        ROLLBACK TRANSACTION;");
            sb.AppendLine("    THROW;");
            sb.AppendLine("END CATCH;");

            return sb.ToString();

            #endregion
        }

        #endregion
    }
}