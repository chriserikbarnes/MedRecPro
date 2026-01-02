
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;

namespace MedRecProImportClass.Helpers
{

    #region property classes

    /*************************************************************/
    /// <summary>
    /// Configuration settings for the in-memory logging system.
    /// </summary>
    /// <remarks>
    /// These settings control log retention based on time and size limits.
    /// Configure via the "LoggingSettings" section in appsettings.json.
    /// </remarks>
    /// <seealso cref="UserLoggerProvider"/>
    /// <seealso cref="UserLogger"/>
    public class LoggingSettings
    {
        /*************************************************************/
        /// <summary>
        /// Maximum time in minutes to retain log entries before automatic cleanup.
        /// </summary>
        /// <remarks>
        /// Default: 60 minutes. Logs older than this threshold are automatically removed
        /// during cleanup operations.
        /// </remarks>
        public int RetentionMinutes { get; set; } = 60;

        /*************************************************************/
        /// <summary>
        /// Maximum number of log entries to retain per logger category.
        /// </summary>
        /// <remarks>
        /// Default: 10000. When this limit is exceeded, oldest entries are removed first.
        /// </remarks>
        public int MaxEntriesPerCategory { get; set; } = 10000;

        /*************************************************************/
        /// <summary>
        /// Maximum total number of log entries across all categories.
        /// </summary>
        /// <remarks>
        /// Default: 50000. When exceeded, oldest entries across all categories are purged.
        /// </remarks>
        public int MaxTotalEntries { get; set; } = 50000;

        /*************************************************************/
        /// <summary>
        /// Whether to capture user identity information in log entries.
        /// </summary>
        /// <remarks>
        /// Default: true. When enabled, the current authenticated user's ID is captured
        /// with each log entry for audit and filtering purposes.
        /// </remarks>
        public bool CaptureUserContext { get; set; } = true;
    }

    /*************************************************************/
    /// <summary>
    /// Represents a single log entry with associated metadata.
    /// </summary>
    /// <remarks>
    /// Includes user context when available for audit and filtering purposes.
    /// </remarks>
    /// <seealso cref="UserLogger"/>
    /// <seealso cref="LoggingSettings"/>
    public class LogEntry
    {
        /*************************************************************/
        /// <summary>
        /// Nullable message content of the log.
        /// </summary>
        public string? Message { get; set; }

        /*************************************************************/
        /// <summary>
        /// Severity level of the log entry.
        /// </summary>
        public LogLevel Level { get; set; }

        /*************************************************************/
        /// <summary>
        /// UTC timestamp when the log was created.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /*************************************************************/
        /// <summary>
        /// Category/source of the log entry (e.g., class name).
        /// </summary>
        public string? Category { get; set; }

        /*************************************************************/
        /// <summary>
        /// Associated exception if any.
        /// </summary>
        public Exception? Exception { get; set; }

        /*************************************************************/
        /// <summary>
        /// The authenticated user's ID at the time of logging (if available).
        /// </summary>
        /// <remarks>
        /// This is captured automatically from the HttpContext when available.
        /// Value is null for system/background operations without user context.
        /// </remarks>
        public string? UserId { get; set; }

        /*************************************************************/
        /// <summary>
        /// The authenticated user's display name at the time of logging (if available).
        /// </summary>
        public string? UserName { get; set; }
    }

    /*************************************************************/
    /// <summary>
    /// Contains a collection of log messages and associated metrics for user tracing.
    /// </summary>
    public class UserLogTrace
    {
        // Collection of log messages for the trace
        public List<LogEntry> LogMessages { get; set; } = new();

        // Total number of log entries
        public int Count { get; set; }

        // Duration of initial operation in milliseconds
        public double InitDuration { get; set; }
    } 
    #endregion

    /*************************************************************/
    /// <summary>
    /// Custom logger provider that manages creation and lifecycle of UserLogger instances.
    /// </summary>
    /// <remarks>
    /// This provider maintains a thread-safe dictionary of loggers by category name.
    /// It also manages log retention based on time and size limits configured in
    /// <see cref="LoggingSettings"/>.
    /// </remarks>
    /// <seealso cref="UserLogger"/>
    /// <seealso cref="LoggingSettings"/>
    public class UserLoggerProvider : ILoggerProvider
    {
        #region fields

        // Thread-safe dictionary to store logger instances by category
        private readonly ConcurrentDictionary<string, UserLogger> _loggers = new();

        // HTTP context accessor for capturing user information
        private readonly IHttpContextAccessor? _httpContextAccessor;

        // Configuration settings for log retention
        private readonly LoggingSettings _settings;

        // Lock object for cleanup operations
        private readonly object _cleanupLock = new();

        // Last cleanup timestamp to avoid excessive cleanup operations
        private DateTime _lastCleanup = DateTime.UtcNow;

        #endregion

        #region constructor

        /*************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="UserLoggerProvider"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
        /// <param name="configuration">Application configuration for logging settings.</param>
        public UserLoggerProvider(
            IHttpContextAccessor? httpContextAccessor = null,
            IConfiguration? configuration = null)
        {
            #region implementation
            _httpContextAccessor = httpContextAccessor;
            _settings = new LoggingSettings();

            // Load settings from configuration if available
            configuration?.GetSection("LoggingSettings").Bind(_settings);
            #endregion
        }

        #endregion

        #region Public Methods

        /*************************************************************/
        /// <summary>
        /// Creates or retrieves an existing logger for the specified category.
        /// </summary>
        /// <param name="categoryName">The category name for the logger.</param>
        /// <returns>An ILogger instance for the specified category.</returns>
        /// <example>
        /// var logger = loggerProvider.CreateLogger("Authentication");
        /// </example>
        public ILogger CreateLogger(string categoryName)
        {
            #region implementation
            return _loggers.GetOrAdd(categoryName, name =>
                new UserLogger(name, _httpContextAccessor, _settings, this));
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Cleans up resources and clears all logger instances.
        /// </summary>
        public void Dispose()
        {
            #region implementation
            _loggers.Clear();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Retrieves all logged entries across all logger instances.
        /// </summary>
        /// <returns>A consolidated list of all log entries ordered by timestamp descending.</returns>
        public List<LogEntry> GetLogs()
        {
            #region implementation
            return _loggers.Values
                .SelectMany(logger => logger.GetLogs())
                .OrderByDescending(e => e.Timestamp)
                .ToList();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Retrieves log entries filtered by date range.
        /// </summary>
        /// <param name="startDate">Start of the date range (UTC).</param>
        /// <param name="endDate">End of the date range (UTC).</param>
        /// <returns>Log entries within the specified date range.</returns>
        public List<LogEntry> GetLogsByDateRange(DateTime startDate, DateTime endDate)
        {
            #region implementation
            return GetLogs()
                .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                .ToList();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Retrieves log entries filtered by category.
        /// </summary>
        /// <param name="category">The category name to filter by (case-insensitive partial match).</param>
        /// <returns>Log entries matching the specified category.</returns>
        public List<LogEntry> GetLogsByCategory(string category)
        {
            #region implementation
            return GetLogs()
                .Where(e => e.Category != null &&
                           e.Category.Contains(category, StringComparison.OrdinalIgnoreCase))
                .ToList();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Retrieves log entries filtered by user ID.
        /// </summary>
        /// <param name="userId">The user ID to filter by.</param>
        /// <returns>Log entries for the specified user.</returns>
        public List<LogEntry> GetLogsByUser(string userId)
        {
            #region implementation
            return GetLogs()
                .Where(e => e.UserId != null &&
                           e.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Retrieves log entries filtered by log level.
        /// </summary>
        /// <param name="level">The minimum log level to include.</param>
        /// <returns>Log entries at or above the specified level.</returns>
        public List<LogEntry> GetLogsByLevel(LogLevel level)
        {
            #region implementation
            return GetLogs()
                .Where(e => e.Level >= level)
                .ToList();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Gets a list of all unique log categories currently in memory.
        /// </summary>
        /// <returns>List of category names with entry counts.</returns>
        public List<CategorySummary> GetCategories()
        {
            #region implementation
            return GetLogs()
                .Where(e => e.Category != null)
                .GroupBy(e => e.Category!)
                .Select(g => new CategorySummary
                {
                    Category = g.Key,
                    EntryCount = g.Count(),
                    OldestEntry = g.Min(e => e.Timestamp),
                    NewestEntry = g.Max(e => e.Timestamp)
                })
                .OrderBy(c => c.Category)
                .ToList();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Gets a list of all unique users with log entries.
        /// </summary>
        /// <returns>List of user summaries.</returns>
        public List<UserLogSummary> GetUserSummaries()
        {
            #region implementation
            return GetLogs()
                .Where(e => e.UserId != null)
                .GroupBy(e => new { e.UserId, e.UserName })
                .Select(g => new UserLogSummary
                {
                    UserId = g.Key.UserId!,
                    UserName = g.Key.UserName,
                    EntryCount = g.Count(),
                    OldestEntry = g.Min(e => e.Timestamp),
                    NewestEntry = g.Max(e => e.Timestamp)
                })
                .OrderBy(u => u.UserName ?? u.UserId)
                .ToList();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Gets the current logging settings.
        /// </summary>
        /// <returns>The current <see cref="LoggingSettings"/>.</returns>
        public LoggingSettings GetSettings() => _settings;

        /*************************************************************/
        /// <summary>
        /// Gets statistics about the current log storage.
        /// </summary>
        /// <returns>Log storage statistics.</returns>
        public LogStatistics GetStatistics()
        {
            #region implementation
            var allLogs = GetLogs();
            return new LogStatistics
            {
                TotalEntries = allLogs.Count,
                CategoryCount = _loggers.Count,
                OldestEntry = allLogs.Any() ? allLogs.Min(e => e.Timestamp) : null,
                NewestEntry = allLogs.Any() ? allLogs.Max(e => e.Timestamp) : null,
                EntriesByLevel = allLogs.GroupBy(e => e.Level)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                UniqueUserCount = allLogs.Where(e => e.UserId != null)
                    .Select(e => e.UserId).Distinct().Count(),
                RetentionMinutes = _settings.RetentionMinutes,
                MaxEntriesPerCategory = _settings.MaxEntriesPerCategory,
                MaxTotalEntries = _settings.MaxTotalEntries
            };
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Performs cleanup of expired and excess log entries.
        /// </summary>
        /// <remarks>
        /// Called automatically by loggers during log operations, but can be
        /// called manually to force immediate cleanup.
        /// </remarks>
        public void PerformCleanup()
        {
            #region implementation
            // Avoid excessive cleanup operations (run at most every 30 seconds)
            if ((DateTime.UtcNow - _lastCleanup).TotalSeconds < 30)
                return;

            lock (_cleanupLock)
            {
                // Double-check after acquiring lock
                if ((DateTime.UtcNow - _lastCleanup).TotalSeconds < 30)
                    return;

                _lastCleanup = DateTime.UtcNow;

                var cutoffTime = DateTime.UtcNow.AddMinutes(-_settings.RetentionMinutes);

                // Clean up each logger
                foreach (var logger in _loggers.Values)
                {
                    logger.CleanupExpiredEntries(cutoffTime, _settings.MaxEntriesPerCategory);
                }

                // Check total entries and purge if needed
                var totalEntries = _loggers.Values.Sum(l => l.GetEntryCount());
                if (totalEntries > _settings.MaxTotalEntries)
                {
                    // Calculate how many entries to remove
                    var entriesToRemove = totalEntries - _settings.MaxTotalEntries;
                    purgeOldestEntries(entriesToRemove);
                }
            }
            #endregion
        }

        #endregion

        #region Private Methods

        /*************************************************************/
        /// <summary>
        /// Removes the oldest entries across all loggers to reduce total count.
        /// </summary>
        /// <param name="count">Number of entries to remove.</param>
        private void purgeOldestEntries(int count)
        {
            #region implementation
            // Get all entries with their logger reference, sorted by timestamp
            var oldestEntries = _loggers.Values
                .SelectMany(l => l.GetLogs().Select(e => new { Logger = l, Entry = e }))
                .OrderBy(x => x.Entry.Timestamp)
                .Take(count)
                .GroupBy(x => x.Logger);

            foreach (var loggerGroup in oldestEntries)
            {
                var entriesToRemove = loggerGroup.Count();
                loggerGroup.Key.RemoveOldestEntries(entriesToRemove);
            }
            #endregion
        }

        #endregion
    }

    /*************************************************************/
    /// <summary>
    /// Summary information for a log category.
    /// </summary>
    public class CategorySummary
    {
        /// <summary>The category name.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Number of log entries in this category.</summary>
        public int EntryCount { get; set; }

        /// <summary>Timestamp of the oldest entry.</summary>
        public DateTime OldestEntry { get; set; }

        /// <summary>Timestamp of the newest entry.</summary>
        public DateTime NewestEntry { get; set; }
    }

    /*************************************************************/
    /// <summary>
    /// Summary information for a user's log entries.
    /// </summary>
    public class UserLogSummary
    {
        /// <summary>The user ID.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>The user's display name (if available).</summary>
        public string? UserName { get; set; }

        /// <summary>Number of log entries for this user.</summary>
        public int EntryCount { get; set; }

        /// <summary>Timestamp of the oldest entry.</summary>
        public DateTime OldestEntry { get; set; }

        /// <summary>Timestamp of the newest entry.</summary>
        public DateTime NewestEntry { get; set; }
    }

    /*************************************************************/
    /// <summary>
    /// Statistics about the current log storage.
    /// </summary>
    public class LogStatistics
    {
        /// <summary>Total number of log entries in memory.</summary>
        public int TotalEntries { get; set; }

        /// <summary>Number of unique categories.</summary>
        public int CategoryCount { get; set; }

        /// <summary>Timestamp of the oldest entry (null if no entries).</summary>
        public DateTime? OldestEntry { get; set; }

        /// <summary>Timestamp of the newest entry (null if no entries).</summary>
        public DateTime? NewestEntry { get; set; }

        /// <summary>Counts of entries by log level.</summary>
        public Dictionary<string, int> EntriesByLevel { get; set; } = new();

        /// <summary>Number of unique users with log entries.</summary>
        public int UniqueUserCount { get; set; }

        /// <summary>Configured retention period in minutes.</summary>
        public int RetentionMinutes { get; set; }

        /// <summary>Configured max entries per category.</summary>
        public int MaxEntriesPerCategory { get; set; }

        /// <summary>Configured max total entries.</summary>
        public int MaxTotalEntries { get; set; }
    }

    /*************************************************************/
    /// <summary>
    /// Custom logger implementation that stores logs in memory with automatic cleanup.
    /// </summary>
    /// <remarks>
    /// Maintains a rolling window of logs based on configured retention period and size limits.
    /// Automatically captures user context when available from the HTTP context.
    /// </remarks>
    /// <seealso cref="UserLoggerProvider"/>
    /// <seealso cref="LoggingSettings"/>
    public class UserLogger : ILogger
    {
        #region fields

        private readonly string _categoryName;
        private readonly ConcurrentQueue<LogEntry> _logEntries = new();
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly LoggingSettings _settings;
        private readonly UserLoggerProvider? _provider;

        #endregion

        #region constructor

        /*************************************************************/
        /// <summary>
        /// Initializes a new instance of the UserLogger class.
        /// </summary>
        /// <param name="categoryName">The category name for this logger instance.</param>
        /// <param name="httpContextAccessor">Optional HTTP context accessor for user tracking.</param>
        /// <param name="settings">Logging settings for retention configuration.</param>
        /// <param name="provider">Parent provider for cleanup coordination.</param>
        public UserLogger(
            string categoryName,
            IHttpContextAccessor? httpContextAccessor = null,
            LoggingSettings? settings = null,
            UserLoggerProvider? provider = null)
        {
            #region implementation
            _categoryName = categoryName;
            _httpContextAccessor = httpContextAccessor;
            _settings = settings ?? new LoggingSettings();
            _provider = provider;
            #endregion
        }

        #endregion

        #region Public Methods

        /*************************************************************/
        /// <summary>
        /// Begins a logical operation scope.
        /// </summary>
        /// <typeparam name="TState">The type of the state to begin scope for.</typeparam>
        /// <param name="state">The identifier for the scope.</param>
        /// <returns>A disposable object that ends the logical operation scope on dispose.</returns>
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8633 // Nullability in constraints for type parameter doesn't match the constraints for type parameter in implicitly implemented interface method'.
        public IDisposable BeginScope<TState>(TState state) => null;
#pragma warning restore CS8633 // Nullability in constraints for type parameter doesn't match the constraints for type parameter in implicitly implemented interface method'.
#pragma warning restore CS8603 // Possible null reference return.

        /*************************************************************/
        /// <summary>
        /// Checks if the given LogLevel is enabled.
        /// </summary>
        /// <param name="logLevel">The log level to check.</param>
        /// <returns>True if enabled, false otherwise.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /*************************************************************/
        /// <summary>
        /// Logs a new entry with the specified parameters.
        /// </summary>
        /// <typeparam name="TState">The type of the object to be logged.</typeparam>
        /// <param name="logLevel">The level of the log entry.</param>
        /// <param name="eventId">The event id associated with the log.</param>
        /// <param name="state">The entry to be written.</param>
        /// <param name="exception">The exception related to this entry.</param>
        /// <param name="formatter">Function to create a string message of the state and exception.</param>
        /// <example>
        /// logger.Log(LogLevel.Error, new EventId(1), "Processing failed", exception,
        ///     (state, ex) => $"{state}: {ex.Message}");
        /// </example>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            #region implementation
            // Capture user context if enabled and available (non-blocking, fail-safe)
            string? userId = null;
            string? userName = null;

            if (_settings.CaptureUserContext && _httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                try
                {
                    var user = _httpContextAccessor.HttpContext.User;
                    // Get user ID from claims - try common claim types
                    userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value;
                    userName = user.FindFirst(ClaimTypes.Name)?.Value
                            ?? user.FindFirst("name")?.Value
                            ?? user.Identity?.Name;
                }
                catch
                {
                    // Silently ignore any errors during user context capture
                    // to avoid impacting logging performance
                }
            }

            var entry = new LogEntry
            {
                Message = formatter(state, exception),
                Level = logLevel,
                Timestamp = DateTime.UtcNow,
                Category = _categoryName,
                Exception = exception,
                UserId = userId,
                UserName = userName
            };

            // Add new log entry to the queue
            _logEntries.Enqueue(entry);

            // Trigger cleanup via provider (throttled internally)
            _provider?.PerformCleanup();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Retrieves all current log entries for this logger instance.
        /// </summary>
        /// <returns>A list of all current log entries.</returns>
        public List<LogEntry> GetLogs()
        {
            #region implementation
            return _logEntries.ToList();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Gets the current number of log entries.
        /// </summary>
        /// <returns>The count of entries in the queue.</returns>
        public int GetEntryCount()
        {
            #region implementation
            return _logEntries.Count;
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Removes entries older than the cutoff time and enforces max entries limit.
        /// </summary>
        /// <param name="cutoffTime">Remove entries older than this time.</param>
        /// <param name="maxEntries">Maximum entries to retain.</param>
        public void CleanupExpiredEntries(DateTime cutoffTime, int maxEntries)
        {
            #region implementation
            // Remove expired entries
            while (_logEntries.TryPeek(out var oldestEntry) &&
                   oldestEntry.Timestamp < cutoffTime)
            {
                _logEntries.TryDequeue(out _);
            }

            // Enforce max entries limit
            while (_logEntries.Count > maxEntries)
            {
                _logEntries.TryDequeue(out _);
            }
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Removes the oldest N entries from this logger.
        /// </summary>
        /// <param name="count">Number of entries to remove.</param>
        public void RemoveOldestEntries(int count)
        {
            #region implementation
            for (int i = 0; i < count && _logEntries.TryDequeue(out _); i++)
            {
                // Just dequeue and discard
            }
            #endregion
        }

        #endregion
    }

    /*************************************************************/
    /// <summary>
    /// Provides extension methods for registering the UserLogger in the dependency injection container.
    /// </summary>
    /// <seealso cref="UserLoggerProvider"/>
    /// <seealso cref="UserLogger"/>
    public static class LoggerExtensions
    {
        /*************************************************************/
        /// <summary>
        /// Adds the UserLogger to the specified IServiceCollection.
        /// </summary>
        /// <param name="services">The IServiceCollection to add the logger to.</param>
        /// <returns>The IServiceCollection for chaining.</returns>
        /// <remarks>
        /// This method registers the <see cref="UserLoggerProvider"/> as a singleton
        /// and configures it with <see cref="IHttpContextAccessor"/> for user tracking
        /// and <see cref="IConfiguration"/> for settings.
        /// </remarks>
        /// <example>
        /// services.AddUserLogger();
        /// </example>
        public static IServiceCollection AddUserLogger(this IServiceCollection services)
        {
            #region implementation

            // Register the UserLoggerProvider as a singleton with dependencies
            services.AddSingleton<UserLoggerProvider>(sp =>
            {
                var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
                var configuration = sp.GetService<IConfiguration>();
                return new UserLoggerProvider(httpContextAccessor, configuration);
            });

            services.AddLogging(builder =>
            {
                builder.Services.AddSingleton<ILoggerProvider>(
                    sp => sp.GetRequiredService<UserLoggerProvider>());
            });

            // Register raw ILogger
            services.AddTransient<ILogger>(serviceProvider =>
            {
                var loggerProvider = serviceProvider.GetRequiredService<UserLoggerProvider>();
                return loggerProvider.CreateLogger("Application");
            });

            // Register generic ILogger<T>
            services.AddTransient(typeof(ILogger<>), typeof(Logger<>));

            return services;

            #endregion
        }
    }
}