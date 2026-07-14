using MedRecPro.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Claims;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests public user logging provider, logger, and registration methods.
    /// </summary>
    /// <seealso cref="UserLoggerProvider"/>
    /// <seealso cref="UserLogger"/>
    /// <seealso cref="LoggerExtensions"/>
    [TestClass]
    public class LogHelperTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies provider query methods return filtered log entries and statistics.
        /// </summary>
        /// <seealso cref="UserLoggerProvider.CreateLogger"/>
        /// <seealso cref="UserLoggerProvider.GetLogs"/>
        /// <seealso cref="UserLoggerProvider.GetLogsByDateRange"/>
        /// <seealso cref="UserLoggerProvider.GetLogsByCategory"/>
        /// <seealso cref="UserLoggerProvider.GetLogsByUser"/>
        /// <seealso cref="UserLoggerProvider.GetLogsByLevel"/>
        /// <seealso cref="UserLoggerProvider.GetCategories"/>
        /// <seealso cref="UserLoggerProvider.GetUserSummaries"/>
        /// <seealso cref="UserLoggerProvider.GetSettings"/>
        /// <seealso cref="UserLoggerProvider.GetStatistics"/>
        [TestMethod]
        public void UserLoggerProvider_QueryMethods_ReturnFilteredLogsAndStatistics()
        {
            #region implementation
            var provider = new UserLoggerProvider(
                createAccessor(),
                Options.Create(createLoggingSettings()));
            var logger = provider.CreateLogger("Coverage.Category");

            logger.LogInformation("info message");
            logger.LogError(new InvalidOperationException("boom"), "error message");

            var logs = provider.GetLogs();
            var now = DateTime.UtcNow;
            var statistics = provider.GetStatistics();

            provider.PerformCleanup();

            Assert.AreEqual(2, logs.Count);
            Assert.AreEqual(2, provider.GetLogsByDateRange(now.AddMinutes(-1), now.AddMinutes(1)).Count);
            Assert.AreEqual(2, provider.GetLogsByCategory("coverage").Count);
            Assert.AreEqual(2, provider.GetLogsByUser("42").Count);
            Assert.AreEqual(1, provider.GetLogsByLevel(LogLevel.Error).Count);
            Assert.AreEqual(1, provider.GetCategories().Count);
            Assert.AreEqual(1, provider.GetUserSummaries().Count);
            Assert.AreEqual(100, provider.GetSettings().MaxEntriesPerCategory);
            Assert.AreEqual(2, statistics.TotalEntries);
            Assert.AreEqual(1, statistics.CategoryCount);
            Assert.AreEqual(1, statistics.UniqueUserCount);
            provider.Dispose();
            Assert.AreEqual(0, provider.GetLogs().Count);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies logger direct methods expose entries and cleanup behavior.
        /// </summary>
        /// <seealso cref="UserLogger.BeginScope"/>
        /// <seealso cref="UserLogger.IsEnabled"/>
        /// <seealso cref="UserLogger.GetLogs"/>
        /// <seealso cref="UserLogger.GetEntryCount"/>
        /// <seealso cref="UserLogger.CleanupExpiredEntries"/>
        /// <seealso cref="UserLogger.RemoveOldestEntries"/>
        [TestMethod]
        public void UserLogger_DirectMethods_ReturnEntriesAndCleanup()
        {
            #region implementation
            var logger = new UserLogger(
                "Direct.Category",
                settings: new LoggingSettings { CaptureUserContext = false, MaxEntriesPerCategory = 10 });

            logger.LogInformation("first");
            logger.LogWarning("second");
            logger.LogError("third");

            using var scope = logger.BeginScope("scope");
            Assert.IsNotNull(scope);
            Assert.IsTrue(logger.IsEnabled(LogLevel.Trace));
            Assert.AreEqual(3, logger.GetEntryCount());

            logger.RemoveOldestEntries(2);

            Assert.AreEqual(1, logger.GetLogs().Count);

            logger.CleanupExpiredEntries(DateTime.UtcNow.AddMinutes(1), maxEntries: 10);

            Assert.AreEqual(0, logger.GetEntryCount());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies user logger registration adds resolvable provider and logger services.
        /// </summary>
        /// <seealso cref="LoggerExtensions.AddUserLogger"/>
        [TestMethod]
        public void AddUserLogger_ServiceCollection_RegistersProviderAndLoggers()
        {
            #region implementation
            var services = new ServiceCollection();
            services.AddSingleton<IHttpContextAccessor>(createAccessor());
            services.AddSingleton<IConfiguration>(createConfiguration());

            var result = services.AddUserLogger();
            using var provider = services.BuildServiceProvider();

            Assert.AreSame(services, result);
            Assert.IsNotNull(provider.GetRequiredService<UserLoggerProvider>());
            Assert.IsNotNull(provider.GetRequiredService<ILoggerProvider>());
            Assert.IsNull(provider.GetService<ILogger>());
            Assert.IsNotNull(provider.GetRequiredService<ILogger<LogHelperTests>>());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an authenticated HTTP context accessor for user-context logging.
        /// </summary>
        /// <returns>HTTP context accessor with user claims.</returns>
        /// <seealso cref="IHttpContextAccessor"/>
        private static IHttpContextAccessor createAccessor()
        {
            #region implementation
            var identity = new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "42"),
                    new Claim(ClaimTypes.Name, "Ada Lovelace")
                },
                authenticationType: "Test");

            return new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates in-memory logging configuration.
        /// </summary>
        /// <returns>Logging configuration.</returns>
        /// <seealso cref="LoggingSettings"/>
        private static IConfiguration createConfiguration()
        {
            #region implementation
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LoggingSettings:RetentionMinutes"] = "60",
                    ["LoggingSettings:MaxEntriesPerCategory"] = "100",
                    ["LoggingSettings:MaxTotalEntries"] = "1000",
                    ["LoggingSettings:CaptureUserContext"] = "true"
                })
                .Build();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies provider filtering, safe scope capture, and redaction happen before an entry is retained.
        /// </summary>
        /// <seealso cref="UserLogger.IsEnabled(LogLevel)"/>
        /// <seealso cref="UserLogger.BeginScope{TState}(TState)"/>
        [TestMethod]
        public void UserLoggerProvider_FilterScopeAndRedaction_StoresOnlyEnabledSafeDiagnosticValues()
        {
            #region implementation

            var filterOptions = new LoggerFilterOptions
            {
                MinLevel = LogLevel.Warning
            };
            var provider = new UserLoggerProvider(
                settings: Options.Create(createLoggingSettings()),
                timeProvider: new FakeTimeProvider(DateTimeOffset.Parse("2026-07-13T18:00:00Z")),
                filterOptions: new StaticOptionsMonitor<LoggerFilterOptions>(filterOptions));
            var logger = provider.CreateLogger("Coverage.Security");

            logger.LogInformation("This disabled information entry must not be retained.");
            using (logger.BeginScope(new Dictionary<string, object?>
            {
                ["TraceId"] = "trace-123",
                ["OperationId"] = "operation-456",
                ["UnapprovedScopeValue"] = "must-not-be-retained"
            }))
            {
                logger.LogError(
                    new InvalidOperationException("connection string=Server=secret; token=abc123"),
                    "Authorization: Bearer abc123 secret=top-secret");
            }

            var entries = provider.GetLogs();

            Assert.AreEqual(1, entries.Count);
            Assert.IsFalse(entries[0].Message!.Contains("abc123", StringComparison.Ordinal));
            Assert.IsFalse(entries[0].ExceptionMessage!.Contains("secret", StringComparison.OrdinalIgnoreCase));
            StringAssert.Contains(entries[0].ExceptionMessage, "connection string=[REDACTED]");
            Assert.AreEqual(nameof(InvalidOperationException), entries[0].ExceptionType);
            Assert.IsNotNull(entries[0].ScopeValues);
            var scopeValues = entries[0].ScopeValues!;
            Assert.AreEqual("trace-123", scopeValues["TraceId"]);
            Assert.AreEqual("operation-456", scopeValues["OperationId"]);
            Assert.IsFalse(scopeValues.ContainsKey("UnapprovedScopeValue"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a provider-specific filter delegate added through <c>AddFilter</c> controls direct admin-store writes.
        /// </summary>
        /// <remarks>
        /// The logger factory evaluates this delegate before ordinary writes, while <see cref="UserLogger.IsEnabled(LogLevel)"/>
        /// must also honor it for callers that resolve the concrete provider and write directly.
        /// </remarks>
        /// <seealso cref="FilterLoggingBuilderExtensions.AddFilter{T}(ILoggingBuilder, string, Func{LogLevel, bool})"/>
        /// <seealso cref="UserLoggerProvider.CreateLogger"/>
        [TestMethod]
        public void UserLoggerProvider_CodeAddedFilterDelegate_IsHonoredForDirectWrites()
        {
            #region implementation

            var services = new ServiceCollection();
            services.AddSingleton<IHttpContextAccessor>(createAccessor());
            services.AddSingleton<IConfiguration>(createConfiguration());
            services.AddUserLogger();
            services.AddLogging(builder => builder.AddFilter<UserLoggerProvider>(
                "Coverage.CodeAdded",
                level => level >= LogLevel.Error));
            using var serviceProvider = services.BuildServiceProvider();
            var provider = serviceProvider.GetRequiredService<UserLoggerProvider>();
            var logger = provider.CreateLogger("Coverage.CodeAdded.Component");

            logger.LogWarning("The code-added delegate rejects this warning.");
            logger.LogError("The code-added delegate retains this error.");

            var entries = provider.GetLogs();

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(LogLevel.Error, entries[0].Level);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies provider-specific rules beat global rules and the longest matching provider category wins.
        /// </summary>
        /// <remarks>
        /// The alias-specific rules intentionally precede a more specific global rule and place the shorter provider
        /// category last. Framework best-rule selection must still choose the alias rule for the longest prefix.
        /// </remarks>
        /// <seealso cref="ProviderAliasAttribute"/>
        /// <seealso cref="LoggerFilterRule"/>
        [TestMethod]
        public void UserLoggerProvider_ProviderAliasAndLongestCategory_SelectBestRule()
        {
            #region implementation

            var filterOptions = new LoggerFilterOptions
            {
                MinLevel = LogLevel.Trace
            };
            filterOptions.Rules.Add(new LoggerFilterRule(
                "UserLogger",
                "Coverage.Semantics.Deep",
                LogLevel.Error,
                filter: null));
            filterOptions.Rules.Add(new LoggerFilterRule(
                "UserLogger",
                "Coverage.Semantics",
                LogLevel.Warning,
                filter: null));
            filterOptions.Rules.Add(new LoggerFilterRule(
                providerName: null,
                "Coverage.Semantics.Deep",
                LogLevel.Critical,
                filter: null));
            var provider = new UserLoggerProvider(
                settings: Options.Create(createLoggingSettings()),
                filterOptions: new StaticOptionsMonitor<LoggerFilterOptions>(filterOptions));
            var logger = provider.CreateLogger("Coverage.Semantics.Deep.Component");

            logger.LogWarning("The selected Error rule rejects this warning.");
            logger.LogError("The longest provider-alias rule retains this error.");

            var entries = provider.GetLogs();

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(LogLevel.Error, entries[0].Level);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the admin-visible in-memory store intentionally excludes Debug diagnostics by default.
        /// </summary>
        /// <remarks>
        /// The application-wide default remains Information. Debug events stay available to providers configured for
        /// them, but are not retained by the queryable admin store unless an explicit UserLogger rule lowers the floor.
        /// </remarks>
        /// <seealso cref="LoggerFilterOptions.MinLevel"/>
        /// <seealso cref="UserLogger.IsEnabled(LogLevel)"/>
        [TestMethod]
        public void UserLoggerProvider_DefaultInformationPolicy_DoesNotRetainDebugDiagnostics()
        {
            #region implementation

            var filterOptions = new LoggerFilterOptions
            {
                MinLevel = LogLevel.Information
            };
            var provider = new UserLoggerProvider(
                settings: Options.Create(createLoggingSettings()),
                filterOptions: new StaticOptionsMonitor<LoggerFilterOptions>(filterOptions));
            var logger = provider.CreateLogger("Coverage.AdminRetention");

            logger.LogDebug("Debug diagnostics are intentionally not admin-visible by default.");
            logger.LogInformation("Information diagnostics remain admin-visible.");

            var entries = provider.GetLogs();

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(LogLevel.Information, entries[0].Level);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies safe exception diagnostics remain useful while SQL, HTTP, and IO secret shapes are redacted.
        /// </summary>
        /// <seealso cref="LogEntry.ExceptionMessage"/>
        /// <seealso cref="UserLoggerProvider.sanitizeValue"/>
        [TestMethod]
        public void UserLogger_ExceptionMessages_KeepSafeSummaryAndRedactSensitiveDetails()
        {
            #region implementation

            var provider = new UserLoggerProvider(
                settings: Options.Create(createLoggingSettings()),
                timeProvider: new FakeTimeProvider(DateTimeOffset.Parse("2026-07-14T12:00:00Z")));
            var logger = provider.CreateLogger("Coverage.ExceptionMessages");

            logger.LogError(
                new InvalidOperationException("SQL command failed safely; connection string=Server=db;Password=sql-secret; @UserId=42"),
                "SQL failure");
            logger.LogError(
                new HttpRequestException("HTTP dependency rejected Authorization: Bearer http-secret"),
                "HTTP failure");
            logger.LogError(
                new IOException(@"IO failure reading C:\outside-app\private-file.txt and /var/secrets/private-file.txt token=io-secret"),
                "IO failure");

            var entries = provider.GetLogs();
            var exceptionMessages = entries.Select(entry => entry.ExceptionMessage).ToList();

            Assert.AreEqual(3, entries.Count);
            Assert.IsTrue(exceptionMessages.Any(message => message!.Contains("SQL command failed safely", StringComparison.Ordinal)));
            Assert.IsTrue(exceptionMessages.Any(message => message!.Contains("HTTP dependency rejected", StringComparison.Ordinal)));
            Assert.IsTrue(exceptionMessages.Any(message => message!.Contains("IO failure reading", StringComparison.Ordinal)));
            Assert.IsTrue(exceptionMessages.All(message => !message!.Contains("sql-secret", StringComparison.Ordinal)));
            Assert.IsTrue(exceptionMessages.All(message => !message!.Contains("http-secret", StringComparison.Ordinal)));
            Assert.IsTrue(exceptionMessages.All(message => !message!.Contains("io-secret", StringComparison.Ordinal)));
            Assert.IsTrue(exceptionMessages.All(message => !message!.Contains("@UserId=42", StringComparison.Ordinal)));
            Assert.IsTrue(exceptionMessages.All(message => !message!.Contains(@"C:\outside-app\private-file.txt", StringComparison.Ordinal)));
            Assert.IsTrue(exceptionMessages.All(message => !message!.Contains("/var/secrets/private-file.txt", StringComparison.Ordinal)));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies automatic cleanup remains off the hot path within the throttle window and runs after it elapses.
        /// </summary>
        /// <remarks>
        /// Capacity eviction is the observable signal: entries may temporarily exceed the category bound inside the
        /// throttle window, then the first later write restores the configured bound without inspecting locks.
        /// </remarks>
        /// <seealso cref="UserLoggerProvider.PerformCleanup"/>
        /// <seealso cref="TimeProvider"/>
        [TestMethod]
        public void UserLoggerProvider_AutomaticCleanup_ThrottlesHotPathAndRunsAfterWindow()
        {
            #region implementation

            var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-14T12:00:00Z"));
            var provider = new UserLoggerProvider(
                settings: Options.Create(new LoggingSettings
                {
                    RetentionMinutes = 60,
                    MaxEntriesPerCategory = 100,
                    MaxTotalEntries = 1000,
                    CaptureUserContext = false
                }),
                timeProvider: timeProvider);
            var logger = provider.CreateLogger("Coverage.CleanupThrottle");

            for (var index = 0; index < 101; index++)
            {
                logger.LogInformation("Entry {EntryIndex}", index);
            }

            Assert.AreEqual(101, provider.GetLogs().Count);

            timeProvider.Advance(TimeSpan.FromSeconds(31));
            logger.LogInformation("Entry after throttle window");

            Assert.AreEqual(100, provider.GetLogs().Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies deterministic time retention and concurrent per-category and total capacity enforcement.
        /// </summary>
        /// <seealso cref="UserLoggerProvider.PerformCleanup"/>
        /// <seealso cref="TimeProvider"/>
        [TestMethod]
        public void UserLoggerProvider_ConcurrentCapacityAndRetention_RemainsBoundedWithoutSleeping()
        {
            #region implementation

            var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-13T18:00:00Z"));
            var provider = new UserLoggerProvider(
                settings: Options.Create(new LoggingSettings
                {
                    RetentionMinutes = 1,
                    MaxEntriesPerCategory = 100,
                    MaxTotalEntries = 1000,
                    CaptureUserContext = false
                }),
                timeProvider: timeProvider);

            Parallel.For(0, 2400, index =>
            {
                provider.CreateLogger($"Coverage.Concurrent.{index % 12}")
                    .LogWarning("Concurrent entry {EntryIndex}", index);
            });
            provider.PerformCleanup();

            Assert.IsTrue(provider.GetLogs().Count <= 1000);
            Assert.IsTrue(provider.GetCategories().All(category => category.EntryCount <= 100));

            timeProvider.Advance(TimeSpan.FromMinutes(2));
            provider.PerformCleanup();

            Assert.AreEqual(0, provider.GetLogs().Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the validated settings values supplied to direct provider tests.
        /// </summary>
        /// <returns>Bounded in-memory logging settings.</returns>
        /// <seealso cref="LoggingSettings"/>
        private static LoggingSettings createLoggingSettings()
        {
            #region implementation

            return new LoggingSettings
            {
                RetentionMinutes = 60,
                MaxEntriesPerCategory = 100,
                MaxTotalEntries = 1000,
                CaptureUserContext = true
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Provides an immutable options monitor for direct component tests.
        /// </summary>
        /// <typeparam name="TOptions">Options type exposed to the component under test.</typeparam>
        /// <seealso cref="IOptionsMonitor{TOptions}"/>
        private sealed class StaticOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
            where TOptions : class
        {
            /**************************************************************/
            /// <summary>
            /// Initializes a monitor that always returns one options instance.
            /// </summary>
            /// <param name="value">Options value supplied to the test subject.</param>
            public StaticOptionsMonitor(TOptions value)
            {
                #region implementation

                CurrentValue = value;

                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Gets the immutable current options value.
            /// </summary>
            public TOptions CurrentValue { get; }

            /**************************************************************/
            /// <summary>
            /// Gets the immutable value for any options name.
            /// </summary>
            /// <param name="name">Ignored options name.</param>
            /// <returns>The supplied options value.</returns>
            public TOptions Get(string? name)
            {
                #region implementation

                return CurrentValue;

                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Returns a no-op registration because the test monitor is immutable.
            /// </summary>
            /// <param name="listener">Ignored value-change listener.</param>
            /// <returns>A disposable no-op registration.</returns>
            public IDisposable OnChange(Action<TOptions, string?> listener)
            {
                #region implementation

                return EmptyDisposable.Instance;

                #endregion
            }
        }

        /**************************************************************/
        /// <summary>
        /// Provides the disposable handle used by immutable test options.
        /// </summary>
        private sealed class EmptyDisposable : IDisposable
        {
            /**************************************************************/
            /// <summary>
            /// Gets the reusable no-op instance.
            /// </summary>
            public static readonly EmptyDisposable Instance = new();

            /**************************************************************/
            /// <summary>
            /// Completes the no-op disposal operation.
            /// </summary>
            public void Dispose()
            {
                #region implementation

                #endregion
            }
        }

        #endregion
    }
}
