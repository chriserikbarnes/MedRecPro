using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MedRecPro.Services
{
    /**************************************************************/
    /// <summary>
    /// Background service that keeps the Azure SQL Serverless database awake during business hours
    /// by sending lightweight SELECT 1 pings at configurable intervals.
    /// </summary>
    /// <remarks>
    /// Azure SQL Serverless (GP_S_Gen5) auto-pauses after 60 minutes of inactivity. Resuming from
    /// a paused state takes 30-60 seconds, which exceeds MCP connector timeout limits. This service
    /// prevents auto-pause by pinging the database every 55 minutes (configurable) during business
    /// hours (Mon-Fri, 8 AM - 5 PM Eastern by default).
    ///
    /// The service always executes an immediate ping on startup to wake the database for incoming
    /// requests, regardless of business hours. Subsequent timer-driven pings are gated by the
    /// business hours window.
    ///
    /// Configuration is read from the "DatabaseKeepAlive" section in appsettings.json and supports
    /// hot-reload via Azure App Service Configuration without redeploy.
    /// </remarks>
    /// <example>
    /// Register in Program.cs:
    /// <code>
    /// builder.Services.AddHostedService&lt;DatabaseKeepAliveService&gt;();
    /// </code>
    /// </example>
    /// <seealso cref="IHostedService"/>
    /// <seealso cref="IConfiguration"/>
    /// <seealso cref="DemoModeService"/>
    public class DatabaseKeepAliveService : IHostedService, IDisposable
    {
        #region fields

        private readonly ILogger<DatabaseKeepAliveService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private Timer? _timer;
        private bool _isEnabled;
        private int _intervalMinutes;
        private int _businessHoursStart;
        private int _businessHoursEnd;
        private string _timeZoneId;
        private bool _businessDaysOnly;
        private int _maxConsecutiveFailures;
        private bool _disposed;

        /**************************************************************/
        /// <summary>
        /// Tracks the last successful ping time for diagnostic purposes.
        /// </summary>
        /// <seealso cref="executePing"/>
        private DateTime? _lastPingTime;

        /**************************************************************/
        /// <summary>
        /// Tracks the next scheduled ping time for diagnostic purposes.
        /// </summary>
        /// <seealso cref="StartAsync"/>
        private DateTime? _nextScheduledPing;

        /**************************************************************/
        /// <summary>
        /// Counter for consecutive failures to help diagnose persistent issues.
        /// </summary>
        /// <seealso cref="timerCallbackAsync"/>
        private int _consecutiveFailures;

        /**************************************************************/
        /// <summary>
        /// Total successful pings since service startup.
        /// </summary>
        /// <seealso cref="timerCallbackAsync"/>
        private int _totalPings;

        /**************************************************************/
        /// <summary>
        /// Total business-hours skips since service startup.
        /// </summary>
        /// <seealso cref="timerCallbackAsync"/>
        private int _totalSkips;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the DatabaseKeepAliveService class.
        /// </summary>
        /// <param name="logger">Logger instance for tracking service operations and errors.</param>
        /// <param name="configuration">Application configuration containing keep-alive settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or configuration is null.</exception>
        /// <seealso cref="ILogger{TCategoryName}"/>
        /// <seealso cref="IConfiguration"/>
        public DatabaseKeepAliveService(
            ILogger<DatabaseKeepAliveService> logger,
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

            // Load keep-alive settings
            loadKeepAliveSettings();

            #endregion
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Starts the keep-alive service and initializes the ping timer if enabled.
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous start operation.</returns>
        /// <remarks>
        /// Always executes an immediate database ping on startup to wake the database for
        /// incoming requests, regardless of business hours. The timer then takes over for
        /// scheduled business-hours-only pings. Uses a synchronous callback that properly
        /// handles async operations to avoid unobserved exceptions in Azure App Service.
        /// </remarks>
        /// <seealso cref="StopAsync"/>
        /// <seealso cref="timerCallbackAsync"/>
        /// <seealso cref="executePing"/>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            #region implementation

            // Log startup with environment details for Azure diagnostics
            _logger.LogInformation(
                "[DatabaseKeepAliveService] StartAsync invoked at {UtcTime}. Environment: {MachineName}, ProcessId: {ProcessId}",
                DateTime.UtcNow.ToString("o"),
                Environment.MachineName,
                Environment.ProcessId);

            if (!_isEnabled)
            {
                _logger.LogInformation("[DatabaseKeepAliveService] Keep-alive is disabled. Service will not run.");
                return;
            }

            _logger.LogInformation(
                "[DatabaseKeepAliveService] Keep-alive is ENABLED. Database ping scheduled every {Interval} minutes " +
                "during business hours ({Start}:00-{End}:00 {TimeZone}, {Days}).",
                _intervalMinutes,
                _businessHoursStart,
                _businessHoursEnd,
                _timeZoneId,
                _businessDaysOnly ? "Mon-Fri" : "Every day");

            // Always ping on startup to wake the database for incoming requests
            _logger.LogInformation("[DatabaseKeepAliveService] Executing initial startup ping to wake database...");
            try
            {
                await executePing(cancellationToken);
                _lastPingTime = DateTime.UtcNow;
                _totalPings++;
                _logger.LogInformation("[DatabaseKeepAliveService] Initial database ping successful.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DatabaseKeepAliveService] Initial database ping failed: {Message}. Will retry on next timer tick.", ex.Message);
                // Continue with timer setup even if initial ping fails
            }

            // Calculate and log next scheduled execution
            var interval = TimeSpan.FromMinutes(_intervalMinutes);
            _nextScheduledPing = DateTime.UtcNow.Add(interval);

            _logger.LogInformation(
                "[DatabaseKeepAliveService] Setting up timer with interval {IntervalMinutes} minutes. " +
                "Next ping scheduled for {NextRun} UTC.",
                _intervalMinutes,
                _nextScheduledPing?.ToString("yyyy-MM-dd HH:mm:ss"));

            // Setup timer with synchronous callback that properly marshals to async
            // This avoids the async void pattern which loses exceptions
            _timer = new Timer(
                callback: timerCallback,
                state: null,
                dueTime: interval,
                period: interval);

            _logger.LogInformation(
                "[DatabaseKeepAliveService] Service started successfully at {UtcTime}. Timer is active.",
                DateTime.UtcNow.ToString("o"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stops the keep-alive service and disposes of the timer.
        /// </summary>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>A task representing the asynchronous stop operation.</returns>
        /// <remarks>
        /// Logs comprehensive diagnostic information when stopping, including
        /// total pings, skips, and failure statistics for Azure troubleshooting.
        /// </remarks>
        /// <seealso cref="StartAsync"/>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            #region implementation

            _logger.LogWarning(
                "[DatabaseKeepAliveService] StopAsync invoked at {UtcTime}. " +
                "Last successful ping: {LastRun}. Total pings: {TotalPings}. Total skips: {TotalSkips}. " +
                "Consecutive failures at shutdown: {Failures}. " +
                "Environment: {MachineName}, ProcessId: {ProcessId}",
                DateTime.UtcNow.ToString("o"),
                _lastPingTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never",
                _totalPings,
                _totalSkips,
                _consecutiveFailures,
                Environment.MachineName,
                Environment.ProcessId);

            // Stop and dispose the timer
            _timer?.Change(Timeout.Infinite, 0);
            _timer?.Dispose();
            _timer = null;

            _logger.LogInformation("[DatabaseKeepAliveService] Service stopped and timer disposed at {UtcTime}.",
                DateTime.UtcNow.ToString("o"));

            return Task.CompletedTask;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Releases all resources used by the DatabaseKeepAliveService.
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
        /// Synchronous timer callback that safely invokes the async ping operation.
        /// </summary>
        /// <param name="state">Timer state object (unused).</param>
        /// <remarks>
        /// This method bridges the synchronous Timer callback to async operations safely.
        /// It uses Task.Run to avoid blocking the timer thread and properly observes
        /// any exceptions to prevent silent failures in Azure App Service.
        /// </remarks>
        /// <seealso cref="timerCallbackAsync"/>
        /// <seealso cref="executePing"/>
        private void timerCallback(object? state)
        {
            #region implementation

            // Log that the timer fired - critical for Azure diagnostics
            _logger.LogInformation(
                "[DatabaseKeepAliveService] Timer callback fired at {UtcTime}. Last ping: {LastRun}, Consecutive failures: {Failures}",
                DateTime.UtcNow.ToString("o"),
                _lastPingTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never",
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
                        "[DatabaseKeepAliveService] Unhandled exception in timer callback wrapper: {Message}",
                        ex.Message);
                }
            });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Async implementation of the timer callback with business hours gating and error handling.
        /// </summary>
        /// <returns>A task representing the async operation.</returns>
        /// <remarks>
        /// First checks whether the current time falls within the configured business hours window.
        /// If outside business hours, the ping is skipped. If inside, executes a lightweight database
        /// ping and tracks success/failure metrics for Azure diagnostics.
        /// </remarks>
        /// <seealso cref="timerCallback"/>
        /// <seealso cref="isWithinBusinessHours"/>
        /// <seealso cref="executePing"/>
        private async Task timerCallbackAsync()
        {
            #region implementation

            // Check business hours before pinging
            if (!isWithinBusinessHours())
            {
                _totalSkips++;
                _nextScheduledPing = DateTime.UtcNow.AddMinutes(_intervalMinutes);

                _logger.LogDebug(
                    "[DatabaseKeepAliveService] Outside business hours. Skipping ping. " +
                    "Total skips: {TotalSkips}. Next check: {NextRun} UTC",
                    _totalSkips,
                    _nextScheduledPing?.ToString("yyyy-MM-dd HH:mm:ss"));

                return;
            }

            _logger.LogInformation(
                "[DatabaseKeepAliveService] Beginning scheduled ping at {UtcTime}",
                DateTime.UtcNow.ToString("o"));

            try
            {
                await executePing(CancellationToken.None);

                // Success - reset failure counter and update timestamps
                _consecutiveFailures = 0;
                _totalPings++;
                _lastPingTime = DateTime.UtcNow;
                _nextScheduledPing = DateTime.UtcNow.AddMinutes(_intervalMinutes);

                _logger.LogInformation(
                    "[DatabaseKeepAliveService] Scheduled ping completed successfully at {UtcTime}. " +
                    "Total pings: {TotalPings}. Next scheduled: {NextRun} UTC",
                    DateTime.UtcNow.ToString("o"),
                    _totalPings,
                    _nextScheduledPing?.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _nextScheduledPing = DateTime.UtcNow.AddMinutes(_intervalMinutes);

                _logger.LogError(ex,
                    "[DatabaseKeepAliveService] Scheduled ping FAILED at {UtcTime}. " +
                    "Error: {Message}. Consecutive failures: {Failures}. " +
                    "Next attempt: {NextRun} UTC",
                    DateTime.UtcNow.ToString("o"),
                    ex.Message,
                    _consecutiveFailures,
                    _nextScheduledPing?.ToString("yyyy-MM-dd HH:mm:ss"));

                // Log additional details for Azure SQL specific errors
                if (ex is SqlException sqlEx)
                {
                    _logger.LogError(
                        "[DatabaseKeepAliveService] SQL Error Details - Number: {Number}, State: {State}, " +
                        "Server: {Server}, Procedure: {Procedure}, LineNumber: {LineNumber}",
                        sqlEx.Number,
                        sqlEx.State,
                        sqlEx.Server,
                        sqlEx.Procedure,
                        sqlEx.LineNumber);
                }

                // Alert if failures exceed threshold
                if (_consecutiveFailures >= _maxConsecutiveFailures)
                {
                    _logger.LogCritical(
                        "[DatabaseKeepAliveService] Exceeded max consecutive failures ({Max}). " +
                        "Database may be unreachable. Will continue retrying on next timer tick.",
                        _maxConsecutiveFailures);
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads keep-alive configuration settings from appsettings.json.
        /// </summary>
        /// <remarks>
        /// Reads the DatabaseKeepAlive section and populates internal fields with configuration values.
        /// Provides default values if settings are not specified. Validates timezone ID and logs all
        /// loaded settings for Azure diagnostics and troubleshooting.
        /// </remarks>
        /// <seealso cref="IConfiguration"/>
        /// <seealso cref="TimeZoneInfo"/>
        private void loadKeepAliveSettings()
        {
            #region implementation

            var settings = _configuration.GetSection("DatabaseKeepAlive");

            _isEnabled = settings.GetValue<bool>("Enabled", true);
            _intervalMinutes = settings.GetValue<int>("IntervalMinutes", 55);
            _businessHoursStart = settings.GetValue<int>("BusinessHoursStart", 8);
            _businessHoursEnd = settings.GetValue<int>("BusinessHoursEnd", 17);
            _timeZoneId = settings.GetValue<string>("TimeZoneId", "Eastern Standard Time") ?? "Eastern Standard Time";
            _businessDaysOnly = settings.GetValue<bool>("BusinessDaysOnly", true);
            _maxConsecutiveFailures = settings.GetValue<int>("MaxConsecutiveFailures", 3);

            // Validate interval
            if (_intervalMinutes < 1)
            {
                _logger.LogWarning(
                    "[DatabaseKeepAliveService] IntervalMinutes value of {Value} is invalid. Using default of 55 minutes.",
                    _intervalMinutes);
                _intervalMinutes = 55;
            }

            // Validate business hours range
            if (_businessHoursStart < 0 || _businessHoursStart > 23 ||
                _businessHoursEnd < 0 || _businessHoursEnd > 23 ||
                _businessHoursStart >= _businessHoursEnd)
            {
                _logger.LogWarning(
                    "[DatabaseKeepAliveService] Business hours {Start}-{End} are invalid. Using defaults 8-17.",
                    _businessHoursStart,
                    _businessHoursEnd);
                _businessHoursStart = 8;
                _businessHoursEnd = 17;
            }

            // Validate timezone ID
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                _logger.LogWarning(
                    "[DatabaseKeepAliveService] TimeZoneId '{TimeZone}' not found. Falling back to 'Eastern Standard Time'.",
                    _timeZoneId);
                _timeZoneId = "Eastern Standard Time";
            }

            // Log loaded configuration for Azure diagnostics
            _logger.LogInformation(
                "[DatabaseKeepAliveService] Configuration loaded - Enabled: {Enabled}, " +
                "IntervalMinutes: {Interval}, BusinessHours: {Start}:00-{End}:00 {TimeZone}, " +
                "BusinessDaysOnly: {DaysOnly}, MaxConsecutiveFailures: {MaxFailures}",
                _isEnabled,
                _intervalMinutes,
                _businessHoursStart,
                _businessHoursEnd,
                _timeZoneId,
                _businessDaysOnly,
                _maxConsecutiveFailures);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether the current time falls within the configured business hours window.
        /// </summary>
        /// <returns>True if within business hours; false if outside the window or on a weekend (when BusinessDaysOnly is enabled).</returns>
        /// <remarks>
        /// Converts the current UTC time to the configured timezone using <see cref="TimeZoneInfo"/>.
        /// The Windows timezone ID "Eastern Standard Time" automatically handles DST transitions,
        /// returning EST (UTC-5) in winter and EDT (UTC-4) in summer.
        /// </remarks>
        /// <seealso cref="TimeZoneInfo.ConvertTimeFromUtc"/>
        /// <seealso cref="loadKeepAliveSettings"/>
        private bool isWithinBusinessHours()
        {
            #region implementation

            var utcNow = DateTime.UtcNow;
            var tz = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneId);
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);

            // Check if today is a business day
            if (_businessDaysOnly && (localTime.DayOfWeek == DayOfWeek.Saturday || localTime.DayOfWeek == DayOfWeek.Sunday))
            {
                return false;
            }

            // Check if current hour falls within business hours
            return localTime.Hour >= _businessHoursStart && localTime.Hour < _businessHoursEnd;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Executes a lightweight database ping by opening a connection and running SELECT 1.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when database connection string is not configured.</exception>
        /// <exception cref="SqlException">Thrown when the SQL operation fails.</exception>
        /// <remarks>
        /// Opens a raw <see cref="SqlConnection"/> and executes SELECT 1 with a 30-second command timeout.
        /// The connection string is re-resolved on each ping to support hot-reload via Azure App Service
        /// Configuration. Logs connection details and latency for Azure diagnostics.
        /// </remarks>
        /// <seealso cref="SqlConnection"/>
        /// <seealso cref="timerCallbackAsync"/>
        private async Task executePing(CancellationToken cancellationToken)
        {
            #region implementation

            var startTime = DateTime.UtcNow;
            _logger.LogDebug(
                "[DatabaseKeepAliveService] executePing started at {UtcTime}",
                startTime.ToString("o"));

            // Re-resolve connection string to support hot-reload
            string? connectionStringBackup;
            string? defaultConnectionString;

            defaultConnectionString = _configuration.GetValue<string?>("DefaultConnection");
#if DEBUG
            connectionStringBackup = _configuration.GetValue<string?>("Dev:DB:Connection");
#else
            connectionStringBackup = _configuration.GetValue<string?>("Prod:DB:Connection");
#endif

            var connectionString = defaultConnectionString
                   ?? connectionStringBackup
                   ?? throw new InvalidOperationException("DefaultConnection string not found in configuration.");

            await using var connection = new SqlConnection(connectionString);

            try
            {
                await connection.OpenAsync(cancellationToken);
                _logger.LogDebug(
                    "[DatabaseKeepAliveService] Database connection opened. Server: {Server}, Database: {Database}",
                    connection.DataSource,
                    connection.Database);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex,
                    "[DatabaseKeepAliveService] Failed to open database connection. Server: {Server}, Error: {Message}",
                    connection.DataSource,
                    ex.Message);
                throw;
            }

            // Execute lightweight ping query
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 30;
            await command.ExecuteScalarAsync(cancellationToken);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "[DatabaseKeepAliveService] Database ping successful. Latency: {Latency:F0}ms",
                duration.TotalMilliseconds);

            #endregion
        }

        #endregion
    }
}
