using MedRecPro.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            var provider = new UserLoggerProvider(createAccessor(), createConfiguration());
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
            Assert.AreEqual(5, provider.GetSettings().MaxEntriesPerCategory);
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

            Assert.IsNull(logger.BeginScope("scope"));
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
            Assert.IsNotNull(provider.GetRequiredService<ILogger>());
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
                    ["LoggingSettings:MaxEntriesPerCategory"] = "5",
                    ["LoggingSettings:MaxTotalEntries"] = "20",
                    ["LoggingSettings:CaptureUserContext"] = "true"
                })
                .Build();
            #endregion
        }

        #endregion
    }
}
