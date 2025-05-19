
using System.Collections.Concurrent;

namespace MedRecPro.Helpers
{

    #region property classes
    /*************************************************************/
    /// <summary>
    /// Represents a single log entry with associated metadata.
    /// </summary>
    public class LogEntry
    {
        // Nullable message content of the log
        public string? Message { get; set; }

        // Severity level of the log entry
        public LogLevel Level { get; set; }

        // UTC timestamp when the log was created
        public DateTime Timestamp { get; set; }

        // Category/source of the log entry
        public string? Category { get; set; }

        // Associated exception if any
        public Exception? Exception { get; set; }
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
    /// </remarks>
    public class UserLoggerProvider : ILoggerProvider
    {
        // Thread-safe dictionary to store logger instances by category
        private readonly ConcurrentDictionary<string, UserLogger> _loggers = new();

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
            #region Implementation
            return _loggers.GetOrAdd(categoryName, name => new UserLogger(name));
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Cleans up resources and clears all logger instances.
        /// </summary>
        public void Dispose()
        {
            #region Implementation
            _loggers.Clear();
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Retrieves all logged entries across all logger instances.
        /// </summary>
        /// <returns>A consolidated list of all log entries.</returns>
        public List<LogEntry> GetLogs()
        {
            #region Implementation
            return _loggers.Values.SelectMany(logger => logger.GetLogs()).ToList();
            #endregion
        }

        #endregion
    }

    /*************************************************************/
    /// <summary>
    /// Custom logger implementation that stores logs in memory with automatic cleanup.
    /// </summary>
    /// <remarks>
    /// Maintains a rolling window of logs for the last 15 minutes.
    /// </remarks>
    public class UserLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly ConcurrentQueue<LogEntry> _logEntries = new();

        /// <summary>
        /// Initializes a new instance of the UserLogger class.
        /// </summary>
        /// <param name="categoryName">The category name for this logger instance.</param>
        public UserLogger(string categoryName)
        {
            _categoryName = categoryName;
        }

        #region Public Methods
        /*************************************************************/
        /// <summary>
        /// Begins a logical operation scope.
        /// </summary>
        /// <typeparam name="TState">The type of the state to begin scope for.</typeparam>
        /// <param name="state">The identifier for the scope.</param>
        /// <returns>A disposable object that ends the logical operation scope on dispose.</returns>
        public IDisposable BeginScope<TState>(TState state) => null;

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
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            #region Implementation
            var entry = new LogEntry
            {
                Message = formatter(state, exception),
                Level = logLevel,
                Timestamp = DateTime.UtcNow,
                Category = _categoryName,
                Exception = exception
            };

            // Add new log entry to the queue
            _logEntries.Enqueue(entry);

            // Remove entries older than 15 minutes
            var cutoffTime = DateTime.UtcNow.AddMinutes(-15);
            while (_logEntries.TryPeek(out var oldestEntry) &&
                   oldestEntry.Timestamp < cutoffTime)
            {
                _logEntries.TryDequeue(out _);
            }
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Retrieves all current log entries for this logger instance.
        /// </summary>
        /// <returns>A list of all current log entries.</returns>
        public List<LogEntry> GetLogs()
        {
            #region Implementation
            return _logEntries.ToList();
            #endregion
        }

        #endregion
    }

    /*************************************************************/
    /// <summary>
    /// Provides extension methods for registering the UserLogger in the dependency injection container.
    /// </summary>
    public static class LoggerExtensions
    {
        /*************************************************************/
        /// <summary>
        /// Adds the UserLogger to the specified IServiceCollection.
        /// </summary>
        /// <param name="services">The IServiceCollection to add the logger to.</param>
        /// <returns>The IServiceCollection for chaining.</returns>
        /// <example>
        /// services.AddUserLogger();
        /// </example>
        public static IServiceCollection AddUserLogger(this IServiceCollection services)
        {
            #region Implementation
            services.AddSingleton<UserLoggerProvider>();
            services.AddLogging(builder =>
            {
                builder.Services.AddSingleton<ILoggerProvider>(
                    sp => sp.GetRequiredService<UserLoggerProvider>());
            });
            return services;
            #endregion
        }
    }
}