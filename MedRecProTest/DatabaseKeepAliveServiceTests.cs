using MedRecPro.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    /*************************************************************/
    /// <summary>
    /// Unit tests for <see cref="DatabaseKeepAliveService"/> configuration loading,
    /// retry settings validation, and service lifecycle.
    /// </summary>
    /// <remarks>
    /// Tests cover configuration loading (including new retry settings),
    /// validation with fallback to defaults, service disabled behavior,
    /// and startup/shutdown lifecycle. Database connectivity is not tested
    /// here since <see cref="DatabaseKeepAliveService.executePing"/> creates
    /// real <see cref="Microsoft.Data.SqlClient.SqlConnection"/> instances.
    /// </remarks>
    /// <seealso cref="DatabaseKeepAliveService"/>
    [TestClass]
    public class DatabaseKeepAliveServiceTests
    {
        #region Helper Methods

        /*************************************************************/
        /// <summary>
        /// Creates an <see cref="IConfiguration"/> with the specified DatabaseKeepAlive settings
        /// and a dummy connection string.
        /// </summary>
        /// <param name="overrides">Key-value pairs to override default settings.</param>
        /// <returns>An <see cref="IConfiguration"/> instance with the configured settings.</returns>
        private static IConfiguration createConfiguration(Dictionary<string, string?>? overrides = null)
        {
            #region implementation

            var defaults = new Dictionary<string, string?>
            {
                ["DefaultConnection"] = "Server=fake;Database=TestDb;Trusted_Connection=True;",
                ["DatabaseKeepAlive:Enabled"] = "true",
                ["DatabaseKeepAlive:IntervalMinutes"] = "14",
                ["DatabaseKeepAlive:BusinessHoursStart"] = "8",
                ["DatabaseKeepAlive:BusinessHoursEnd"] = "20",
                ["DatabaseKeepAlive:TimeZoneId"] = "Eastern Standard Time",
                ["DatabaseKeepAlive:BusinessDaysOnly"] = "true",
                ["DatabaseKeepAlive:MaxConsecutiveFailures"] = "3",
                ["DatabaseKeepAlive:RetryAttempts"] = "3",
                ["DatabaseKeepAlive:RetryDelaySeconds:0"] = "10",
                ["DatabaseKeepAlive:RetryDelaySeconds:1"] = "30",
                ["DatabaseKeepAlive:RetryDelaySeconds:2"] = "60",
            };

            if (overrides != null)
            {
                foreach (var kvp in overrides)
                    defaults[kvp.Key] = kvp.Value;
            }

            return new ConfigurationBuilder()
                .AddInMemoryCollection(defaults)
                .Build();

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Creates a <see cref="DatabaseKeepAliveService"/> instance with optional configuration overrides.
        /// </summary>
        /// <param name="overrides">Key-value pairs to override default settings.</param>
        /// <param name="logger">Optional mock logger to capture log output.</param>
        /// <returns>A new <see cref="DatabaseKeepAliveService"/> instance.</returns>
        private static DatabaseKeepAliveService createService(
            Dictionary<string, string?>? overrides = null,
            Mock<ILogger<DatabaseKeepAliveService>>? logger = null)
        {
            #region implementation

            var config = createConfiguration(overrides);
            var log = logger ?? new Mock<ILogger<DatabaseKeepAliveService>>();
            return new DatabaseKeepAliveService(log.Object, config);

            #endregion
        }

        #endregion

        #region Constructor Tests

        /*************************************************************/
        /// <summary>
        /// Verifies the service constructs successfully with valid configuration
        /// including the new retry settings.
        /// </summary>
        [TestMethod]
        public void Constructor_ValidConfig_CreatesServiceSuccessfully()
        {
            #region implementation

            var service = createService();
            Assert.IsNotNull(service);
            service.Dispose();

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies the constructor throws when logger is null.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            #region implementation

            var config = createConfiguration();
            _ = new DatabaseKeepAliveService(null!, config);

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies the constructor throws when configuration is null.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullConfiguration_ThrowsArgumentNullException()
        {
            #region implementation

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();
            _ = new DatabaseKeepAliveService(logger.Object, null!);

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies the constructor throws when no connection string is configured.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void Constructor_MissingConnectionString_ThrowsInvalidOperationException()
        {
            #region implementation

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseKeepAlive:Enabled"] = "true",
                })
                .Build();

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();
            _ = new DatabaseKeepAliveService(logger.Object, config);

            #endregion
        }

        #endregion

        #region Configuration Loading Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that retry settings are loaded from configuration and logged
        /// during startup.
        /// </summary>
        [TestMethod]
        public void LoadSettings_RetryConfig_LogsRetryAttemptsAndDelays()
        {
            #region implementation

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();
            var service = createService(logger: logger);

            // Verify the configuration loaded log includes retry settings
            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("RetryAttempts: 3") &&
                        v.ToString()!.Contains("RetryDelays: [10, 30, 60]s")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            service.Dispose();

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that the interval of 14 minutes is loaded from configuration.
        /// </summary>
        [TestMethod]
        public void LoadSettings_IntervalMinutes_LogsConfiguredInterval()
        {
            #region implementation

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();
            var service = createService(logger: logger);

            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("IntervalMinutes: 14")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            service.Dispose();

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that business hours end at 20 (8 PM) is loaded from configuration.
        /// </summary>
        [TestMethod]
        public void LoadSettings_BusinessHoursEnd_LogsExtendedHours()
        {
            #region implementation

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();
            var service = createService(logger: logger);

            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("BusinessHours: 8:00-20:00")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            service.Dispose();

            #endregion
        }

        #endregion

        #region Configuration Validation Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that a negative RetryAttempts value falls back to default of 3
        /// with a warning log.
        /// </summary>
        [TestMethod]
        public void LoadSettings_NegativeRetryAttempts_FallsBackToDefault()
        {
            #region implementation

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();
            var service = createService(
                overrides: new Dictionary<string, string?>
                {
                    ["DatabaseKeepAlive:RetryAttempts"] = "-1"
                },
                logger: logger);

            // Should log a warning about invalid value
            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("RetryAttempts") &&
                        v.ToString()!.Contains("invalid")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Should still log config with the default value of 3
            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("RetryAttempts: 3")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            service.Dispose();

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that when no RetryDelaySeconds are configured, the default
        /// array [10, 30, 60] is used.
        /// </summary>
        [TestMethod]
        public void LoadSettings_MissingRetryDelays_FallsBackToDefaults()
        {
            #region implementation

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();

            // Build config without RetryDelaySeconds keys entirely
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DefaultConnection"] = "Server=fake;Database=TestDb;Trusted_Connection=True;",
                    ["DatabaseKeepAlive:Enabled"] = "true",
                    ["DatabaseKeepAlive:IntervalMinutes"] = "14",
                    ["DatabaseKeepAlive:BusinessHoursStart"] = "8",
                    ["DatabaseKeepAlive:BusinessHoursEnd"] = "20",
                    ["DatabaseKeepAlive:TimeZoneId"] = "Eastern Standard Time",
                    ["DatabaseKeepAlive:BusinessDaysOnly"] = "true",
                    ["DatabaseKeepAlive:MaxConsecutiveFailures"] = "3",
                    ["DatabaseKeepAlive:RetryAttempts"] = "3",
                    // RetryDelaySeconds intentionally omitted
                })
                .Build();

            var service = new DatabaseKeepAliveService(logger.Object, config);

            // Should log config with the default delays
            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("RetryDelays: [10, 30, 60]s")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            service.Dispose();

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that an invalid IntervalMinutes value (0) falls back to 55
        /// with a warning log.
        /// </summary>
        [TestMethod]
        public void LoadSettings_ZeroInterval_FallsBackToDefault()
        {
            #region implementation

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();
            var service = createService(
                overrides: new Dictionary<string, string?>
                {
                    ["DatabaseKeepAlive:IntervalMinutes"] = "0"
                },
                logger: logger);

            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("IntervalMinutes") &&
                        v.ToString()!.Contains("invalid")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            service.Dispose();

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that custom retry delay values are loaded correctly from config.
        /// </summary>
        [TestMethod]
        public void LoadSettings_CustomRetryDelays_LogsCustomValues()
        {
            #region implementation

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();
            var service = createService(
                overrides: new Dictionary<string, string?>
                {
                    ["DatabaseKeepAlive:RetryDelaySeconds:0"] = "5",
                    ["DatabaseKeepAlive:RetryDelaySeconds:1"] = "15",
                    ["DatabaseKeepAlive:RetryDelaySeconds:2"] = "45",
                },
                logger: logger);

            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("RetryDelays: [5, 15, 45]s")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            service.Dispose();

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that RetryAttempts of 0 is accepted (disables retries, initial attempt only).
        /// </summary>
        [TestMethod]
        public void LoadSettings_ZeroRetryAttempts_IsAccepted()
        {
            #region implementation

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();
            var service = createService(
                overrides: new Dictionary<string, string?>
                {
                    ["DatabaseKeepAlive:RetryAttempts"] = "0"
                },
                logger: logger);

            // Should NOT log a warning — 0 is valid (no retries, just the initial attempt)
            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("RetryAttempts")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);

            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("RetryAttempts: 0")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            service.Dispose();

            #endregion
        }

        #endregion

        #region Service Lifecycle Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that StartAsync returns immediately without setting up a timer
        /// when the service is disabled.
        /// </summary>
        [TestMethod]
        public async Task StartAsync_Disabled_DoesNotStartTimer()
        {
            #region implementation

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();
            var service = createService(
                overrides: new Dictionary<string, string?>
                {
                    ["DatabaseKeepAlive:Enabled"] = "false"
                },
                logger: logger);

            await service.StartAsync(CancellationToken.None);

            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("disabled")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Should NOT log timer setup
            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("Timer is active")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);

            service.Dispose();

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that StartAsync logs the initial ping attempt even when
        /// the database is unreachable (fake connection string). The service
        /// should not throw and should continue to set up the timer.
        /// </summary>
        [TestMethod]
        public async Task StartAsync_UnreachableDatabase_ContinuesWithTimerSetup()
        {
            #region implementation

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();
            var service = createService(logger: logger);

            // StartAsync will attempt the initial ping which will fail (fake connection string),
            // but it should catch the error and still set up the timer
            await service.StartAsync(CancellationToken.None);

            // Should log the initial ping failure
            logger.Verify(
                l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("Initial database ping failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Should still set up the timer
            logger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("Timer is active")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            await service.StopAsync(CancellationToken.None);
            service.Dispose();

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that StopAsync logs diagnostic summary including ping and
        /// failure statistics.
        /// </summary>
        [TestMethod]
        public async Task StopAsync_LogsDiagnosticSummary()
        {
            #region implementation

            var logger = new Mock<ILogger<DatabaseKeepAliveService>>();
            var service = createService(logger: logger);

            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);

            // StopAsync should log at Warning level with diagnostic summary
            logger.Verify(
                l => l.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) =>
                        v.ToString()!.Contains("StopAsync") &&
                        v.ToString()!.Contains("Total pings")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            service.Dispose();

            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that Dispose can be called multiple times without throwing.
        /// </summary>
        [TestMethod]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            #region implementation

            var service = createService();

            service.Dispose();
            service.Dispose(); // Second call should be a no-op

            #endregion
        }

        #endregion
    }
}
