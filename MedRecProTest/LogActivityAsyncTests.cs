using MedRecPro.Data;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    /*************************************************************/
    /// <summary>
    /// Unit tests for ActivityLogService functionality.
    /// </summary>
    /// <remarks>
    /// Tests cover authenticated users, anonymous users, endpoint filtering,
    /// and various query scenarios with nullable UserId support.
    /// </remarks>
    [TestClass]
    public class ActivityLogServiceTests
    {
        #region Helper Methods

        /*************************************************************/
        /// <summary>
        /// Creates a mock IConfiguration with feature flags enabled.
        /// </summary>
        private IConfiguration CreateMockConfiguration(bool activityTrackingEnabled = true)
        {
            var inMemorySettings = new Dictionary<string, string>
            {
                {"FeatureFlags:ActivityTrackingEnabled", activityTrackingEnabled.ToString()}
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();
        }

        /*************************************************************/
        /// <summary>
        /// Creates a new in-memory database context for testing.
        /// </summary>
        private ApplicationDbContext CreateTestContext(string databaseName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: databaseName)
                .Options;

            return new ApplicationDbContext(options);
        }

        #endregion

        #region LogActivityAsync Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that LogActivityAsync successfully persists authenticated user activity.
        /// </summary>
        [TestMethod]
        public async Task LogActivityAsync_AuthenticatedUser_ShouldPersistLog()
        {
            // Arrange
            using var context = CreateTestContext("LogActivity_AuthUser_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration();
            var service = new ActivityLogService(context, logger.Object, configuration);

            var log = new ActivityLog
            {
                UserId = 1,  // Authenticated user
                ActivityType = "Test",
                Description = "Test Activity",
                ActivityTimestamp = DateTime.UtcNow
            };

            // Act
            await service.LogActivityAsync(log);

            // Assert
            var savedLog = await context.ActivityLogs.FirstOrDefaultAsync();
            Assert.IsNotNull(savedLog, "Log should be persisted");
            Assert.AreEqual("Test", savedLog.ActivityType);
            Assert.AreEqual(1L, savedLog.UserId);
            Assert.AreEqual("Test Activity", savedLog.Description);
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that LogActivityAsync successfully persists anonymous user activity.
        /// </summary>
        [TestMethod]
        public async Task LogActivityAsync_AnonymousUser_ShouldPersistLog()
        {
            // Arrange
            using var context = CreateTestContext("LogActivity_AnonUser_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration();
            var service = new ActivityLogService(context, logger.Object, configuration);

            var log = new ActivityLog
            {
                UserId = null,  // Anonymous user
                ActivityType = "Read",
                Description = "Anonymous Access",
                ActivityTimestamp = DateTime.UtcNow,
                IpAddress = "192.168.1.100"
            };

            // Act
            await service.LogActivityAsync(log);

            // Assert
            var savedLog = await context.ActivityLogs.FirstOrDefaultAsync();
            Assert.IsNotNull(savedLog, "Anonymous log should be persisted");
            Assert.AreEqual("Read", savedLog.ActivityType);
            Assert.IsNull(savedLog.UserId, "UserId should be null for anonymous users");
            Assert.AreEqual("Anonymous Access", savedLog.Description);
            Assert.AreEqual("192.168.1.100", savedLog.IpAddress);
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that LogActivityAsync sets ActivityTimestamp if not provided.
        /// </summary>
        [TestMethod]
        public async Task LogActivityAsync_MissingTimestamp_ShouldSetUtcNow()
        {
            // Arrange
            using var context = CreateTestContext("LogActivity_Timestamp_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration();
            var service = new ActivityLogService(context, logger.Object, configuration);

            var beforeTime = DateTime.UtcNow;
            var log = new ActivityLog
            {
                UserId = 1,
                ActivityType = "Test",
                ActivityTimestamp = default  // Not set
            };

            // Act
            await service.LogActivityAsync(log);

            // Assert
            var savedLog = await context.ActivityLogs.FirstOrDefaultAsync();
            Assert.IsNotNull(savedLog);
            Assert.IsTrue(savedLog.ActivityTimestamp >= beforeTime,
                "Timestamp should be set to current UTC time");
            Assert.IsTrue(savedLog.ActivityTimestamp <= DateTime.UtcNow.AddSeconds(1),
                "Timestamp should be recent");
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that LogActivityAsync handles errors gracefully when logging is disabled.
        /// </summary>
        [TestMethod]
        public async Task LogActivityAsync_WhenDisabled_ShouldNotThrow()
        {
            // Arrange
            using var context = CreateTestContext("LogActivity_Disabled_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration(activityTrackingEnabled: false);
            var service = new ActivityLogService(context, logger.Object, configuration);

            var log = new ActivityLog
            {
                UserId = 1,
                ActivityType = "Test"
            };

            // Act & Assert - should not throw
            await service.LogActivityAsync(log);

            // Verify no log was saved
            var savedLog = await context.ActivityLogs.FirstOrDefaultAsync();
            Assert.IsNull(savedLog, "No log should be saved when feature is disabled");
        }

        #endregion

        #region GetUserActivityAsync Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that GetUserActivityAsync returns logs for specific authenticated user.
        /// </summary>
        [TestMethod]
        public async Task GetUserActivityAsync_AuthenticatedUser_ShouldReturnUserLogs()
        {
            // Arrange
            using var context = CreateTestContext("GetUserActivity_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration();
            var service = new ActivityLogService(context, logger.Object, configuration);

            // Add test data
            context.ActivityLogs.AddRange(
                new ActivityLog { UserId = 1, ActivityType = "Login", ActivityTimestamp = DateTime.UtcNow },
                new ActivityLog { UserId = 1, ActivityType = "Create", ActivityTimestamp = DateTime.UtcNow },
                new ActivityLog { UserId = 2, ActivityType = "Update", ActivityTimestamp = DateTime.UtcNow },
                new ActivityLog { UserId = null, ActivityType = "Read", ActivityTimestamp = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            // Act
            var results = await service.GetUserActivityAsync(1, 10);

            // Assert
            Assert.AreEqual(2, results.Count, "Should return exactly 2 logs for user 1");
            Assert.IsTrue(results.All(r => r.UserId == 1), "All results should be for user 1");
            Assert.IsFalse(results.Any(r => r.UserId == 2), "Should not include user 2 logs");
            Assert.IsFalse(results.Any(r => r.UserId == null), "Should not include anonymous logs");
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that GetUserActivityAsync respects the limit parameter.
        /// </summary>
        [TestMethod]
        public async Task GetUserActivityAsync_WithLimit_ShouldRespectLimit()
        {
            // Arrange
            using var context = CreateTestContext("GetUserActivity_Limit_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration();
            var service = new ActivityLogService(context, logger.Object, configuration);

            // Add 5 logs for user 1
            for (int i = 0; i < 5; i++)
            {
                context.ActivityLogs.Add(new ActivityLog
                {
                    UserId = 1,
                    ActivityType = "Test",
                    ActivityTimestamp = DateTime.UtcNow.AddMinutes(-i)
                });
            }
            await context.SaveChangesAsync();

            // Act
            var results = await service.GetUserActivityAsync(1, 3);

            // Assert
            Assert.AreEqual(3, results.Count, "Should return only 3 logs as specified by limit");
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that GetUserActivityAsync returns empty list for users with no activity.
        /// </summary>
        [TestMethod]
        public async Task GetUserActivityAsync_NoLogs_ShouldReturnEmptyList()
        {
            // Arrange
            using var context = CreateTestContext("GetUserActivity_NoLogs_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration();
            var service = new ActivityLogService(context, logger.Object, configuration);

            // Act
            var results = await service.GetUserActivityAsync(999, 10);

            // Assert
            Assert.IsNotNull(results, "Result should not be null");
            Assert.AreEqual(0, results.Count, "Should return empty list for non-existent user");
        }

        #endregion

        #region GetActivityByEndpointAsync Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that GetActivityByEndpointAsync filters by controller name.
        /// </summary>
        [TestMethod]
        public async Task GetActivityByEndpointAsync_ByController_ShouldReturnFilteredLogs()
        {
            // Arrange
            using var context = CreateTestContext("GetActivityByEndpoint_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration();
            var service = new ActivityLogService(context, logger.Object, configuration);

            // Add test data
            context.ActivityLogs.AddRange(
                new ActivityLog
                {
                    UserId = 1,
                    ControllerName = "Labels",
                    ActionName = "Create",
                    ActivityTimestamp = DateTime.UtcNow
                },
                new ActivityLog
                {
                    UserId = null,  // Anonymous
                    ControllerName = "Labels",
                    ActionName = "Update",
                    ActivityTimestamp = DateTime.UtcNow
                },
                new ActivityLog
                {
                    UserId = 1,
                    ControllerName = "Users",
                    ActionName = "Create",
                    ActivityTimestamp = DateTime.UtcNow
                }
            );
            await context.SaveChangesAsync();

            // Act
            var results = await service.GetActivityByEndpointAsync("Labels", null, 10);

            // Assert
            Assert.AreEqual(2, results.Count, "Should return 2 logs for Labels controller");
            Assert.IsTrue(results.All(r => r.ControllerName == "Labels"),
                "All results should be for Labels controller");
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that GetActivityByEndpointAsync filters by controller and action.
        /// </summary>
        [TestMethod]
        public async Task GetActivityByEndpointAsync_ByControllerAndAction_ShouldReturnFilteredLogs()
        {
            // Arrange
            using var context = CreateTestContext("GetActivityByEndpoint_Action_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration();
            var service = new ActivityLogService(context, logger.Object, configuration);

            // Add test data
            context.ActivityLogs.AddRange(
                new ActivityLog
                {
                    UserId = 1,
                    ControllerName = "Labels",
                    ActionName = "Create",
                    ActivityTimestamp = DateTime.UtcNow
                },
                new ActivityLog
                {
                    UserId = 1,
                    ControllerName = "Labels",
                    ActionName = "Update",
                    ActivityTimestamp = DateTime.UtcNow
                },
                new ActivityLog
                {
                    UserId = null,  // Anonymous
                    ControllerName = "Labels",
                    ActionName = "Create",
                    ActivityTimestamp = DateTime.UtcNow
                }
            );
            await context.SaveChangesAsync();

            // Act
            var results = await service.GetActivityByEndpointAsync("Labels", "Create", 10);

            // Assert
            Assert.AreEqual(2, results.Count, "Should return 2 logs for Labels/Create");
            Assert.IsTrue(results.All(r => r.ControllerName == "Labels" && r.ActionName == "Create"),
                "All results should be for Labels/Create endpoint");
        }

        #endregion

        #region Anonymous User Specific Tests

        /*************************************************************/
        /// <summary>
        /// Verifies that anonymous user logs can be queried separately.
        /// </summary>
        [TestMethod]
        public async Task GetActivityLogs_AnonymousOnly_ShouldReturnAnonymousLogs()
        {
            // Arrange
            using var context = CreateTestContext("Anonymous_Query_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration();

            // Add mixed data
            context.ActivityLogs.AddRange(
                new ActivityLog { UserId = 1, ActivityType = "Login", ActivityTimestamp = DateTime.UtcNow },
                new ActivityLog { UserId = null, ActivityType = "Read", ActivityTimestamp = DateTime.UtcNow },
                new ActivityLog { UserId = null, ActivityType = "Read", ActivityTimestamp = DateTime.UtcNow },
                new ActivityLog { UserId = 2, ActivityType = "Update", ActivityTimestamp = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            // Act
            var anonymousLogs = await context.ActivityLogs
                .Where(a => a.UserId == null)
                .ToListAsync();

            // Assert
            Assert.AreEqual(2, anonymousLogs.Count, "Should return exactly 2 anonymous logs");
            Assert.IsTrue(anonymousLogs.All(a => a.UserId == null),
                "All results should have null UserId");
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that activity statistics work correctly with nullable UserId.
        /// </summary>
        [TestMethod]
        public async Task GetActivityStatistics_WithNullableUserId_ShouldCalculateCorrectly()
        {
            // Arrange
            using var context = CreateTestContext("Statistics_Nullable_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration();

            // Add test data
            context.ActivityLogs.AddRange(
                new ActivityLog { UserId = 1, ActivityType = "Login", ActivityTimestamp = DateTime.UtcNow },
                new ActivityLog { UserId = 1, ActivityType = "Create", ActivityTimestamp = DateTime.UtcNow },
                new ActivityLog { UserId = null, ActivityType = "Read", ActivityTimestamp = DateTime.UtcNow },
                new ActivityLog { UserId = null, ActivityType = "Read", ActivityTimestamp = DateTime.UtcNow },
                new ActivityLog { UserId = 2, ActivityType = "Update", ActivityTimestamp = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            // Act
            var totalLogs = await context.ActivityLogs.CountAsync();
            var authenticatedLogs = await context.ActivityLogs
                .Where(a => a.UserId != null)
                .CountAsync();
            var anonymousLogs = await context.ActivityLogs
                .Where(a => a.UserId == null)
                .CountAsync();
            var uniqueUsers = await context.ActivityLogs
                .Where(a => a.UserId != null)
                .Select(a => a.UserId)
                .Distinct()
                .CountAsync();

            // Assert
            Assert.AreEqual(5, totalLogs, "Total should be 5");
            Assert.AreEqual(3, authenticatedLogs, "Should be 3 authenticated logs");
            Assert.AreEqual(2, anonymousLogs, "Should be 2 anonymous logs");
            Assert.AreEqual(2, uniqueUsers, "Should be 2 unique authenticated users");
        }

        #endregion

        #region Edge Cases

        /*************************************************************/
        /// <summary>
        /// Verifies that logging handles very long error messages.
        /// </summary>
        [TestMethod]
        public async Task LogActivityAsync_LongErrorMessage_ShouldPersist()
        {
            // Arrange
            using var context = CreateTestContext("LongError_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration();
            var service = new ActivityLogService(context, logger.Object, configuration);

            var longErrorMessage = new string('X', 5000);
            var log = new ActivityLog
            {
                UserId = 1,
                ActivityType = "Error",
                ErrorMessage = longErrorMessage,
                ActivityTimestamp = DateTime.UtcNow
            };

            // Act
            await service.LogActivityAsync(log);

            // Assert
            var savedLog = await context.ActivityLogs.FirstOrDefaultAsync();
            Assert.IsNotNull(savedLog);
            Assert.AreEqual(longErrorMessage, savedLog.ErrorMessage);
        }

        /*************************************************************/
        /// <summary>
        /// Verifies that concurrent logging operations work correctly.
        /// </summary>
        [TestMethod]
        public async Task LogActivityAsync_ConcurrentLogs_ShouldAllPersist()
        {
            // Arrange
            using var context = CreateTestContext("Concurrent_Test");
            var logger = new Mock<ILogger<ActivityLogService>>();
            var configuration = CreateMockConfiguration();
            var service = new ActivityLogService(context, logger.Object, configuration);

            // Act - Create 10 concurrent logging tasks
            var tasks = Enumerable.Range(1, 10).Select(async i =>
            {
                var log = new ActivityLog
                {
                    UserId = i % 3 == 0 ? null : (long?)i,  // Some anonymous, some authenticated
                    ActivityType = "Test",
                    Description = $"Concurrent log {i}",
                    ActivityTimestamp = DateTime.UtcNow
                };
                await service.LogActivityAsync(log);
            });

            await Task.WhenAll(tasks);

            // Assert
            var savedLogs = await context.ActivityLogs.ToListAsync();
            Assert.AreEqual(10, savedLogs.Count, "All 10 logs should be persisted");

            var anonymousCount = savedLogs.Count(l => l.UserId == null);
            Assert.IsTrue(anonymousCount > 0, "Should have some anonymous logs");
            Assert.IsTrue(anonymousCount < 10, "Should have some authenticated logs");
        }

        #endregion
    }
}