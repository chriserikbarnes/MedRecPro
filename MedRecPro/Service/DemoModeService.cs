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

        /**************************************************************/
        /// <summary>
        /// Tracks the last successful truncation time for diagnostic purposes.
        /// </summary>
        /// <seealso cref="executeTruncation"/>
        private DateTime? _lastTruncationTime;

        /**************************************************************/
        /// <summary>
        /// Tracks the next scheduled truncation time for diagnostic purposes.
        /// </summary>
        /// <seealso cref="StartAsync"/>
        private DateTime? _nextScheduledTruncation;

        /**************************************************************/
        /// <summary>
        /// Counter for consecutive failures to help diagnose persistent issues.
        /// </summary>
        /// <seealso cref="timerCallbackAsync"/>
        private int _consecutiveFailures;

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
            #region implementation
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            string? connectionStringBackup;
            string? defaultConnectionString;

            defaultConnectionString = _configuration.GetValue<string?>("DefaultConnection");
#if DEBUG
            connectionStringBackup = _configuration.GetValue<string?>("Dev:DB:Connection");
#else
            connectionStringBackup = _configuration.GetValue<string?>("Prod:DB:Connection");
#endif

            // Load connection string from configuration
            _connectionString = defaultConnectionString
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
        /// The timer uses a synchronous callback that properly handles async operations
        /// to avoid unobserved exceptions in Azure App Service environments.
        /// </remarks>
        /// <seealso cref="StopAsync"/>
        /// <seealso cref="timerCallbackAsync"/>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            #region implementation

            // Log startup with environment details for Azure diagnostics
            _logger.LogInformation(
                "[DemoModeService] StartAsync invoked at {UtcTime}. Environment: {MachineName}, ProcessId: {ProcessId}",
                DateTime.UtcNow.ToString("o"),
                Environment.MachineName,
                Environment.ProcessId);

            if (!_isEnabled)
            {
                _logger.LogInformation("[DemoModeService] Demo mode is disabled. Service will not run.");
                return;
            }

            _logger.LogInformation(
                "[DemoModeService] Demo mode is ENABLED. Database truncation scheduled every {Interval} minutes.",
                _refreshIntervalMinutes);

            // Execute immediate truncation on startup if configured
            if (_autoTruncateOnStartup)
            {
                _logger.LogInformation("[DemoModeService] AutoTruncateOnStartup is enabled. Executing initial truncation...");
                try
                {
                    await executeTruncation(cancellationToken);
                    _lastTruncationTime = DateTime.UtcNow;
                    _logger.LogInformation("[DemoModeService] Initial truncation completed successfully.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DemoModeService] Initial truncation failed: {Message}", ex.Message);
                    // Continue with timer setup even if initial truncation fails
                }
            }

            // Calculate and log next scheduled execution
            var interval = TimeSpan.FromMinutes(_refreshIntervalMinutes);
            _nextScheduledTruncation = DateTime.UtcNow.Add(interval);

            _logger.LogInformation(
                "[DemoModeService] Setting up timer with interval {IntervalMinutes} minutes ({IntervalHours:F2} hours). " +
                "Next truncation scheduled for {NextRun} UTC.",
                _refreshIntervalMinutes,
                interval.TotalHours,
                _nextScheduledTruncation?.ToString("yyyy-MM-dd HH:mm:ss"));

            // Setup timer with synchronous callback that properly marshals to async
            // This avoids the async void pattern which loses exceptions
            _timer = new Timer(
                callback: timerCallback,
                state: null,
                dueTime: interval,
                period: interval);

            _logger.LogInformation(
                "[DemoModeService] Service started successfully at {UtcTime}. Timer is active.",
                DateTime.UtcNow.ToString("o"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Synchronous timer callback that safely invokes the async truncation operation.
        /// </summary>
        /// <param name="state">Timer state object (unused).</param>
        /// <remarks>
        /// This method bridges the synchronous Timer callback to async operations safely.
        /// It uses Task.Run to avoid blocking the timer thread and properly observes
        /// any exceptions to prevent silent failures in Azure App Service.
        /// </remarks>
        /// <seealso cref="timerCallbackAsync"/>
        /// <seealso cref="executeTruncation"/>
        private void timerCallback(object? state)
        {
            #region implementation

            // Log that the timer fired - critical for Azure diagnostics
            _logger.LogInformation(
                "[DemoModeService] Timer callback fired at {UtcTime}. Last truncation: {LastRun}, Consecutive failures: {Failures}",
                DateTime.UtcNow.ToString("o"),
                _lastTruncationTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never",
                _consecutiveFailures);

            // Fire and forget with proper exception observation
            // Use Task.Run to avoid blocking the timer thread pool
            _ = Task.Run(async () =>
            {
                try
                {
                    await timerCallbackAsync();
                }
                catch (Exception ex)
                {
                    // This catch ensures exceptions are observed even if timerCallbackAsync throws unexpectedly
                    _logger.LogCritical(ex,
                        "[DemoModeService] Unhandled exception in timer callback wrapper: {Message}",
                        ex.Message);
                }
            });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Async implementation of the timer callback with comprehensive error handling.
        /// </summary>
        /// <returns>A task representing the async operation.</returns>
        /// <remarks>
        /// This method wraps the truncation execution with proper exception handling,
        /// retry tracking, and detailed logging for Azure diagnostics.
        /// </remarks>
        /// <seealso cref="timerCallback"/>
        /// <seealso cref="executeTruncation"/>
        private async Task timerCallbackAsync()
        {
            #region implementation

            _logger.LogInformation(
                "[DemoModeService] Beginning scheduled truncation at {UtcTime}",
                DateTime.UtcNow.ToString("o"));

            try
            {
                await executeTruncation(CancellationToken.None);

                // Success - reset failure counter and update timestamps
                _consecutiveFailures = 0;
                _lastTruncationTime = DateTime.UtcNow;
                _nextScheduledTruncation = DateTime.UtcNow.AddMinutes(_refreshIntervalMinutes);

                _logger.LogInformation(
                    "[DemoModeService] Scheduled truncation completed successfully at {UtcTime}. " +
                    "Next scheduled: {NextRun} UTC",
                    DateTime.UtcNow.ToString("o"),
                    _nextScheduledTruncation?.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _nextScheduledTruncation = DateTime.UtcNow.AddMinutes(_refreshIntervalMinutes);

                _logger.LogError(ex,
                    "[DemoModeService] Scheduled truncation FAILED at {UtcTime}. " +
                    "Error: {Message}. Consecutive failures: {Failures}. " +
                    "Next attempt: {NextRun} UTC",
                    DateTime.UtcNow.ToString("o"),
                    ex.Message,
                    _consecutiveFailures,
                    _nextScheduledTruncation?.ToString("yyyy-MM-dd HH:mm:ss"));

                // Log additional details for Azure SQL specific errors
                if (ex is SqlException sqlEx)
                {
                    _logger.LogError(
                        "[DemoModeService] SQL Error Details - Number: {Number}, State: {State}, " +
                        "Server: {Server}, Procedure: {Procedure}, LineNumber: {LineNumber}",
                        sqlEx.Number,
                        sqlEx.State,
                        sqlEx.Server,
                        sqlEx.Procedure,
                        sqlEx.LineNumber);
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stops the demo mode service and disposes of the timer.
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous stop operation.</returns>
        /// <remarks>
        /// Logs comprehensive diagnostic information when stopping, including
        /// last truncation time and failure statistics for Azure troubleshooting.
        /// </remarks>
        /// <seealso cref="StartAsync"/>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            #region implementation

            _logger.LogWarning(
                "[DemoModeService] StopAsync invoked at {UtcTime}. " +
                "Last successful truncation: {LastRun}. Total consecutive failures at shutdown: {Failures}. " +
                "Environment: {MachineName}, ProcessId: {ProcessId}",
                DateTime.UtcNow.ToString("o"),
                _lastTruncationTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never",
                _consecutiveFailures,
                Environment.MachineName,
                Environment.ProcessId);

            // Stop and dispose the timer
            _timer?.Change(Timeout.Infinite, 0);
            _timer?.Dispose();
            _timer = null;

            _logger.LogInformation("[DemoModeService] Service stopped and timer disposed at {UtcTime}.",
                DateTime.UtcNow.ToString("o"));

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
        /// Provides default values if settings are not specified. Logs all loaded settings for
        /// Azure diagnostics and troubleshooting.
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
                    "[DemoModeService] RefreshIntervalMinutes value of {Value} is invalid. Using default of 60 minutes.",
                    _refreshIntervalMinutes);
                _refreshIntervalMinutes = 60;
            }

            // Log loaded configuration for Azure diagnostics
            _logger.LogInformation(
                "[DemoModeService] Configuration loaded - Enabled: {Enabled}, " +
                "RefreshIntervalMinutes: {Interval}, AutoTruncateOnStartup: {AutoTruncate}, " +
                "PreservedTablesCount: {PreservedCount}",
                _isEnabled,
                _refreshIntervalMinutes,
                _autoTruncateOnStartup,
                _preserveTables?.Count ?? 0);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Executes database truncation operations in demo mode.
        /// Coordinates the disabling of constraints, table truncation, and constraint re-enabling.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when database connection fails.</exception>
        /// <exception cref="SqlException">Thrown when SQL operations fail.</exception>
        /// <remarks>
        /// This method orchestrates the complete truncation process by calling specialized methods
        /// for each phase of the operation. All operations are performed within the context of
        /// a single database connection to ensure consistency.
        /// </remarks>
        /// <seealso cref="disableForeignKeyConstraints"/>
        /// <seealso cref="getTablesToTruncate"/>
        /// <seealso cref="truncateTable"/>
        /// <seealso cref="enableForeignKeyConstraints"/>
        private async Task executeTruncation(CancellationToken cancellationToken)
        {
            #region implementation

            var startTime = DateTime.UtcNow;
            _logger.LogInformation(
                "[DemoModeService] executeTruncation started at {UtcTime}",
                startTime.ToString("o"));

            string? connectionStringBackup;
            string? defaultConnectionString;

            defaultConnectionString = _configuration.GetValue<string?>("DefaultConnection");
#if DEBUG
            connectionStringBackup = _configuration.GetValue<string?>("Dev:DB:Connection");
#else
            connectionStringBackup = _configuration.GetValue<string?>("Prod:DB:Connection");
#endif

            // Load connection string from configuration
            var connectionString = defaultConnectionString
                   ?? connectionStringBackup
                   ?? throw new InvalidOperationException("DefaultConnection string not found in configuration.");

            _logger.LogDebug("[DemoModeService] Attempting to open database connection...");

            await using var connection = new SqlConnection(connectionString);

            try
            {
                await connection.OpenAsync(cancellationToken);
                _logger.LogInformation(
                    "[DemoModeService] Database connection opened successfully. Server: {Server}, Database: {Database}",
                    connection.DataSource,
                    connection.Database);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex,
                    "[DemoModeService] Failed to open database connection. Server: {Server}, Error: {Message}",
                    connection.DataSource,
                    ex.Message);
                throw;
            }

            try
            {
                // Disable all foreign key constraints before truncation
                _logger.LogInformation("[DemoModeService] Step 1/4: Disabling foreign key constraints...");
                await disableForeignKeyConstraints(connection, cancellationToken);

                // Get list of tables that should be truncated
                _logger.LogInformation("[DemoModeService] Step 2/4: Retrieving tables to truncate...");
                var tablesToTruncate = await getTablesToTruncate(connection, cancellationToken);

                _logger.LogInformation(
                    "[DemoModeService] Found {TableCount} tables to truncate (excluding {PreservedCount} preserved tables)",
                    tablesToTruncate.Count,
                    _preserveTables?.Count ?? 0);

                // Truncate each table individually
                _logger.LogInformation("[DemoModeService] Step 3/4: Truncating {TableCount} tables...", tablesToTruncate.Count);
                var truncatedCount = 0;
                foreach (var (schema, tableName) in tablesToTruncate)
                {
                    await truncateTable(connection, schema, tableName, cancellationToken);
                    truncatedCount++;
                }

                // Re-enable all foreign key constraints after truncation
                _logger.LogInformation("[DemoModeService] Step 4/4: Re-enabling foreign key constraints...");
                await enableForeignKeyConstraints(connection, cancellationToken);

                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "[DemoModeService] Database truncation completed successfully. " +
                    "Tables truncated: {TruncatedCount}. Duration: {Duration:F2} seconds.",
                    truncatedCount,
                    duration.TotalSeconds);
            }
            catch (SqlException ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex,
                    "[DemoModeService] SQL error during truncation after {Duration:F2} seconds. " +
                    "Error Number: {Number}, State: {State}, Message: {Message}",
                    duration.TotalSeconds,
                    ex.Number,
                    ex.State,
                    ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                var duration = DateTime.UtcNow - startTime;
                _logger.LogError(ex,
                    "[DemoModeService] Unexpected error during truncation after {Duration:F2} seconds: {Message}",
                    duration.TotalSeconds,
                    ex.Message);
                throw;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Disables all foreign key constraints in the database.
        /// </summary>
        /// <param name="connection">The open SQL connection to use.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="SqlException">Thrown when the SQL operation fails.</exception>
        /// <remarks>
        /// Uses dynamic SQL to iterate through all foreign keys in the database and disable them.
        /// This is necessary before truncating tables that have foreign key relationships.
        /// The operation is compatible with Azure SQL Database.
        /// </remarks>
        /// <example>
        /// await disableForeignKeyConstraints(connection, cancellationToken);
        /// </example>
        /// <seealso cref="enableForeignKeyConstraints"/>
        /// <seealso cref="executeTruncation"/>
        private async Task disableForeignKeyConstraints(SqlConnection connection, CancellationToken cancellationToken)
        {
            #region implementation
            _logger.LogInformation("Disabling all foreign key constraints...");

            // Build dynamic SQL to disable all foreign key constraints
            var disableConstraintsSql = @"
                DECLARE @sql NVARCHAR(MAX) = N'';
                SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) 
                            + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) 
                            + ' NOCHECK CONSTRAINT ' + QUOTENAME(name) + ';' + CHAR(13)
                FROM sys.foreign_keys;
                EXEC sp_executesql @sql;";

            await using var command = connection.CreateCommand();
            command.CommandText = disableConstraintsSql;
            command.CommandTimeout = 300; // 5 minutes for large databases
            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug("Successfully disabled all foreign key constraints.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a list of all user tables in the database excluding preserved tables.
        /// </summary>
        /// <param name="connection">The open SQL connection to use.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A list of tuples containing schema name and table name pairs.</returns>
        /// <exception cref="SqlException">Thrown when the SQL operation fails.</exception>
        /// <remarks>
        /// Queries the INFORMATION_SCHEMA.TABLES view to get all base tables, then filters out
        /// tables specified in the _preserveTables collection. This ensures system tables
        /// and migration history tables are not truncated.
        /// </remarks>
        /// <example>
        /// var tables = await getTablesToTruncate(connection, cancellationToken);
        /// foreach (var (schema, tableName) in tables)
        /// {
        ///     // Process each table
        /// }
        /// </example>
        /// <seealso cref="executeTruncation"/>
        /// <seealso cref="truncateTable"/>
        private async Task<List<(string Schema, string TableName)>> getTablesToTruncate(
            SqlConnection connection,
            CancellationToken cancellationToken)
        {
            #region implementation
            _logger.LogInformation("Retrieving table list for truncation...");

            var getTablesSql = @"
                SELECT TABLE_SCHEMA, TABLE_NAME 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_NAME";

            var tablesToTruncate = new List<(string Schema, string TableName)>();

            await using var command = connection.CreateCommand();
            command.CommandText = getTablesSql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var schema = reader.GetString(0);
                var tableName = reader.GetString(1);

                // Skip tables that are in the preservation list
                if (_preserveTables?.Contains(tableName, StringComparer.OrdinalIgnoreCase) != true)
                {
                    tablesToTruncate.Add((schema, tableName));
                }
            }

            return tablesToTruncate;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Truncates a single table in the database, with fallback to DELETE if truncation fails.
        /// </summary>
        /// <param name="connection">The open SQL connection to use.</param>
        /// <param name="schema">The schema name of the table.</param>
        /// <param name="tableName">The name of the table to truncate.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="SqlException">Thrown when both TRUNCATE and DELETE operations fail.</exception>
        /// <remarks>
        /// First attempts to use TRUNCATE TABLE for performance. If that fails due to foreign key
        /// constraints (SQL error 4712), falls back to DELETE FROM which is slower but works
        /// with all table configurations.
        /// </remarks>
        /// <example>
        /// await truncateTable(connection, "dbo", "Products", cancellationToken);
        /// </example>
        /// <seealso cref="executeTruncation"/>
        /// <seealso cref="getTablesToTruncate"/>
        private async Task truncateTable(
            SqlConnection connection,
            string schema,
            string tableName,
            CancellationToken cancellationToken)
        {
            #region implementation
            try
            {
                _logger.LogInformation("Truncating table: {Schema}.{TableName}", schema, tableName);

                await using var command = connection.CreateCommand();
                command.CommandText = $"TRUNCATE TABLE [{schema}].[{tableName}]";
                command.CommandTimeout = 60; // 1 minute per table
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex) when (ex.Number == 4712)
            {
                // SQL Error 4712: Cannot truncate table because it's referenced by a foreign key constraint
                // Fall back to DELETE which works with foreign keys
                _logger.LogWarning(
                    "Cannot truncate {Schema}.{TableName} due to foreign key, using DELETE instead",
                    schema,
                    tableName);

                await deleteTableContents(connection, schema, tableName, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to truncate table {Schema}.{TableName}", schema, tableName);
                throw;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Deletes all contents from a table using DELETE FROM statement.
        /// </summary>
        /// <param name="connection">The open SQL connection to use.</param>
        /// <param name="schema">The schema name of the table.</param>
        /// <param name="tableName">The name of the table to delete from.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="SqlException">Thrown when the DELETE operation fails.</exception>
        /// <remarks>
        /// This method is used as a fallback when TRUNCATE TABLE fails due to foreign key constraints.
        /// DELETE is slower than TRUNCATE but works with all table configurations. Note that DELETE
        /// does not reset identity columns, unlike TRUNCATE.
        /// </remarks>
        /// <seealso cref="truncateTable"/>
        /// <seealso cref="executeTruncation"/>
        private async Task deleteTableContents(
            SqlConnection connection,
            string schema,
            string tableName,
            CancellationToken cancellationToken)
        {
            #region implementation
            _logger.LogDebug("Deleting contents from table: {Schema}.{TableName}", schema, tableName);

            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = $"DELETE FROM [{schema}].[{tableName}]";
            deleteCommand.CommandTimeout = 120; // 2 minutes for DELETE operations
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Re-enables all foreign key constraints in the database and validates them.
        /// </summary>
        /// <param name="connection">The open SQL connection to use.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="SqlException">Thrown when the SQL operation fails.</exception>
        /// <remarks>
        /// Uses dynamic SQL to iterate through all foreign keys and re-enable them with validation.
        /// The WITH CHECK CHECK CONSTRAINT syntax both enables the constraint and validates
        /// existing data against it. This is the final step in the truncation process.
        /// </remarks>
        /// <example>
        /// await enableForeignKeyConstraints(connection, cancellationToken);
        /// </example>
        /// <seealso cref="disableForeignKeyConstraints"/>
        /// <seealso cref="executeTruncation"/>
        private async Task enableForeignKeyConstraints(SqlConnection connection, CancellationToken cancellationToken)
        {
            #region implementation
            _logger.LogInformation("Re-enabling all foreign key constraints...");

            // Build dynamic SQL to re-enable and validate all foreign key constraints
            var enableConstraintsSql = @"
                DECLARE @sql NVARCHAR(MAX) = N'';
                SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) 
                            + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) 
                            + ' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(name) + ';' + CHAR(13)
                FROM sys.foreign_keys;
                EXEC sp_executesql @sql;";

            await using var command = connection.CreateCommand();
            command.CommandText = enableConstraintsSql;
            command.CommandTimeout = 300; // 5 minutes for constraint validation
            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug("Successfully re-enabled all foreign key constraints.");
            #endregion
        }

        #endregion
    }
}