
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace MedRecPro.Helpers
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
        [Range(1, 10080)]
        public int RetentionMinutes { get; set; } = 60;

        /*************************************************************/
        /// <summary>
        /// Maximum number of log entries to retain per logger category.
        /// </summary>
        /// <remarks>
        /// Default: 10000. When this limit is exceeded, oldest entries are removed first.
        /// </remarks>
        [Range(100, 100000)]
        public int MaxEntriesPerCategory { get; set; } = 10000;

        /*************************************************************/
        /// <summary>
        /// Maximum total number of log entries across all categories.
        /// </summary>
        /// <remarks>
        /// Default: 50000. When exceeded, oldest entries across all categories are purged.
        /// </remarks>
        [Range(1000, 500000)]
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
        /// Correlation identifier supplied by the structured log state or an allowlisted logging scope.
        /// </summary>
        /// <remarks>
        /// This value is retained separately so administrative diagnostics can compare it directly with the
        /// <c>traceId</c> returned in RFC 7807 responses without parsing a rendered log message.
        /// </remarks>
        public string? TraceId { get; set; }

        /*************************************************************/
        /// <summary>
        /// Safe, redacted exception summary retained for administrative diagnostics.
        /// </summary>
        /// <remarks>
        /// The logger deliberately does not retain the live <see cref="Exception"/> object graph. This prevents
        /// captured stack frames, inner exceptions, tokens, and request state from remaining in memory for the
        /// configured retention period.
        /// </remarks>
        public string? ExceptionMessage { get; set; }

        /*************************************************************/
        /// <summary>
        /// Runtime type name of the exception captured with the entry.
        /// </summary>
        public string? ExceptionType { get; set; }

        /*************************************************************/
        /// <summary>
        /// Allowlisted structured scope values captured when the log entry was written.
        /// </summary>
        /// <remarks>
        /// Only request and operation correlation values are retained; arbitrary scope state is deliberately omitted
        /// to avoid retaining claims, request bodies, or other sensitive data.
        /// </remarks>
        public IReadOnlyDictionary<string, string>? ScopeValues { get; set; }

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
    [ProviderAlias("UserLogger")]
    public class UserLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        #region fields

        // Thread-safe dictionary to store logger instances by category
        private readonly ConcurrentDictionary<string, UserLogger> _loggers = new();

        // HTTP context accessor for capturing user information
        private readonly IHttpContextAccessor? _httpContextAccessor;

        // Configuration settings for log retention
        private readonly LoggingSettings _settings;

        // Source of deterministic timestamps for entry creation and retention.
        private readonly TimeProvider _timeProvider;

        // Logger filter rules supplied by the hosting logging system.
        private readonly IOptionsMonitor<LoggerFilterOptions>? _filterOptions;

        // Scope storage shared by the logger factory with this provider.
        private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

        // Lock object for cleanup operations
        private readonly object _cleanupLock = new();

        // Last cleanup timestamp to avoid excessive cleanup operations
        private DateTime _lastCleanup;

        #endregion

        #region constructor

        /*************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="UserLoggerProvider"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
        /// <param name="settings">Validated logging settings supplied by the ASP.NET Core Options system.</param>
        /// <param name="timeProvider">Clock used for deterministic timestamps and retention decisions.</param>
        /// <param name="filterOptions">Optional hosted logger filter rules.</param>
        public UserLoggerProvider(
            IHttpContextAccessor? httpContextAccessor = null,
            IOptions<LoggingSettings>? settings = null,
            TimeProvider? timeProvider = null,
            IOptionsMonitor<LoggerFilterOptions>? filterOptions = null)
        {
            #region implementation
            _httpContextAccessor = httpContextAccessor;
            _settings = settings?.Value ?? new LoggingSettings();
            _timeProvider = timeProvider ?? TimeProvider.System;
            _filterOptions = filterOptions;
            _lastCleanup = _timeProvider.GetUtcNow().UtcDateTime;
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
        /// Receives the host-provided scope store used to capture correlation values.
        /// </summary>
        /// <param name="scopeProvider">Scope provider supplied by the logging infrastructure.</param>
        /// <seealso cref="ISupportExternalScope"/>
        void ISupportExternalScope.SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            #region implementation

            _scopeProvider = scopeProvider ?? new LoggerExternalScopeProvider();

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
            lock (_cleanupLock)
            {
                _lastCleanup = _timeProvider.GetUtcNow().UtcDateTime;

                var cutoffTime = _lastCleanup.AddMinutes(-_settings.RetentionMinutes);

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

        /*************************************************************/
        /// <summary>
        /// Determines whether this provider should retain an entry for a category and level.
        /// </summary>
        /// <param name="categoryName">Logger category requesting the write.</param>
        /// <param name="logLevel">Severity of the potential entry.</param>
        /// <returns><see langword="true"/> when the configured provider/category rules enable the level.</returns>
        /// <remarks>
        /// The logger factory also applies these rules before it calls a provider. This check protects direct
        /// <see cref="UserLogger"/> use and prevents disabled levels from being retained in memory.
        /// </remarks>
        /// <seealso cref="UserLogger.IsEnabled(LogLevel)"/>
        internal bool isEnabled(string categoryName, LogLevel logLevel)
        {
            #region implementation

            if (logLevel == LogLevel.None)
            {
                return false;
            }

            if (_filterOptions == null)
            {
                return true;
            }

            var options = _filterOptions.CurrentValue;
            LogLevel minimumLevel = options.MinLevel;
            LoggerFilterRule? matchingRule = null;

            foreach (var rule in options.Rules)
            {
                bool providerMatches = string.IsNullOrWhiteSpace(rule.ProviderName)
                    || string.Equals(rule.ProviderName, nameof(UserLoggerProvider), StringComparison.Ordinal)
                    || string.Equals(rule.ProviderName, "UserLogger", StringComparison.Ordinal)
                    || string.Equals(rule.ProviderName, typeof(UserLoggerProvider).FullName, StringComparison.Ordinal);
                bool categoryMatches = string.IsNullOrWhiteSpace(rule.CategoryName)
                    || categoryName.StartsWith(rule.CategoryName, StringComparison.OrdinalIgnoreCase);

                if (providerMatches && categoryMatches)
                {
                    matchingRule = rule;
                }
            }

            if (matchingRule?.LogLevel is LogLevel configuredMinimumLevel)
            {
                minimumLevel = configuredMinimumLevel;
            }

            return logLevel >= minimumLevel;

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Begins a host-managed logging scope.
        /// </summary>
        /// <typeparam name="TState">Type of scope state.</typeparam>
        /// <param name="state">Scope state to push.</param>
        /// <returns>A disposable that removes the scope.</returns>
        internal IDisposable beginScope<TState>(TState state)
        {
            #region implementation

            return _scopeProvider.Push(state);

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Captures only the safe correlation values from the current nested scopes.
        /// </summary>
        /// <returns>Captured values, or <see langword="null"/> when no allowlisted scope values exist.</returns>
        internal IReadOnlyDictionary<string, string>? getScopeValues()
        {
            #region implementation

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            _scopeProvider.ForEachScope(static (scope, state) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> structuredScope)
                {
                    foreach (var pair in structuredScope)
                    {
                        if (pair.Key is "TraceId" or "OperationId" or "UserId" or "RequestMethod" or "RequestPath"
                            && pair.Value != null)
                        {
                            state[pair.Key] = sanitizeValue(pair.Value.ToString()) ?? string.Empty;
                        }
                    }
                }
            }, values);

            return values.Count == 0 ? null : values;

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Gets the current UTC timestamp from the provider clock.
        /// </summary>
        /// <returns>Current UTC date and time.</returns>
        internal DateTime getUtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

        /*************************************************************/
        /// <summary>
        /// Redacts common credential shapes before an entry is retained in memory.
        /// </summary>
        /// <param name="value">Potentially sensitive diagnostic text.</param>
        /// <returns>Safe bounded text suitable for the administrative log store.</returns>
        internal static string? sanitizeValue(string? value)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var sanitized = Regex.Replace(value, @"(?i)\bbearer\s+[^\s,;]+", "Bearer [REDACTED]");
            sanitized = Regex.Replace(
                sanitized,
                @"(?i)\b(password|passwd|secret|token|api[-_]?key|connection\s*string|authorization)\b\s*([:=])\s*([^\s,;\}\]]+)",
                "$1$2[REDACTED]");

            return sanitized.Length <= 1024 ? sanitized : sanitized[..1024];

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
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            #region implementation

            return _provider?.beginScope(state) ?? NoopScope.Instance;

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Checks if the given LogLevel is enabled.
        /// </summary>
        /// <param name="logLevel">The log level to check.</param>
        /// <returns>True if enabled, false otherwise.</returns>
        public bool IsEnabled(LogLevel logLevel) =>
            _provider?.isEnabled(_categoryName, logLevel) ?? logLevel != LogLevel.None;

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
            if (!IsEnabled(logLevel))
            {
                return;
            }

            // Capture user context if enabled and available (non-blocking, fail-safe)
            string? userId = null;
            string? userName = null;

            if (_settings.CaptureUserContext && _httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                try
                {
                    var user = _httpContextAccessor.HttpContext.User;
                    userId = ClaimHelper.GetUserIdFromClaims(user.Claims)?.ToString();
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
                Message = UserLoggerProvider.sanitizeValue(formatter(state, exception)),
                Level = logLevel,
                Timestamp = _provider?.getUtcNow() ?? DateTime.UtcNow,
                Category = _categoryName,
                TraceId = getTraceId(state),
                ExceptionMessage = exception == null
                    ? null
                    : "An exception was recorded. See the correlated server-side log event.",
                ExceptionType = exception?.GetType().Name,
                ScopeValues = _provider?.getScopeValues(),
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

        /*************************************************************/
        /// <summary>
        /// Extracts the correlation identifier from structured log state, falling back to a safe logging scope.
        /// </summary>
        /// <typeparam name="TState">The type of state supplied to the logger.</typeparam>
        /// <param name="state">Structured state supplied by the logging extension method.</param>
        /// <returns>A sanitized trace identifier, or <see langword="null"/> when none was supplied.</returns>
        private string? getTraceId<TState>(TState state)
        {
            #region implementation

            if (state is IEnumerable<KeyValuePair<string, object?>> structuredState)
            {
                var traceId = structuredState
                    .FirstOrDefault(pair => string.Equals(pair.Key, "TraceId", StringComparison.Ordinal))
                    .Value?
                    .ToString();

                if (!string.IsNullOrWhiteSpace(traceId))
                {
                    return UserLoggerProvider.sanitizeValue(traceId);
                }
            }

            return _provider?.getScopeValues()?.GetValueOrDefault("TraceId");

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Provides a non-null scope handle when a logger is used outside a provider.
        /// </summary>
        /// <seealso cref="UserLogger.BeginScope{TState}(TState)"/>
        private sealed class NoopScope : IDisposable
        {
            /*************************************************************/
            /// <summary>
            /// Gets the reusable empty scope instance.
            /// </summary>
            public static readonly NoopScope Instance = new();

            /*************************************************************/
            /// <summary>
            /// Completes the no-op scope.
            /// </summary>
            public void Dispose()
            {
                #region implementation

                #endregion
            }
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
        /// and configures it with <see cref="IHttpContextAccessor"/>, validated options, and the shared
        /// <see cref="TimeProvider"/> used by the application.
        /// </remarks>
        /// <example>
        /// services.AddUserLogger();
        /// </example>
        public static IServiceCollection AddUserLogger(this IServiceCollection services)
        {
            #region implementation

            services.AddOptions<LoggingSettings>()
                .BindConfiguration("LoggingSettings")
                .ValidateDataAnnotations()
                .Validate(
                    settings => settings.MaxTotalEntries >= settings.MaxEntriesPerCategory,
                    "MaxTotalEntries must be at least MaxEntriesPerCategory.")
                .ValidateOnStart();

            services.TryAddSingleton<UserLoggerProvider>();
            services.AddLogging(builder =>
            {
                // Resolve the provider through its concrete singleton so direct administrative queries and the
                // logging factory observe the same bounded in-memory entry store.
                builder.Services.AddSingleton<ILoggerProvider>(
                    serviceProvider => serviceProvider.GetRequiredService<UserLoggerProvider>());
            });

            return services;

            #endregion
        }
    }
}
