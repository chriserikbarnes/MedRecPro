using MedRecPro.Data;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    [TestClass]
    public class ActivityLogServiceTests
    {
        [TestMethod]
        public async Task LogActivityAsync_ShouldPersistLog()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "MedRecPro_Test")
                .Options;

            using var context = new ApplicationDbContext(options);
            var logger = new Mock<ILogger<ActivityLogService>>();
            var service = new ActivityLogService(context, logger.Object);

            var log = new ActivityLog
            {
                UserId = 1,
                ActivityType = "Test",
                Description = "Test Activity"
            };

            // Act
            await service.LogActivityAsync(log);

            // Assert
            var savedLog = await context.ActivityLogs.FirstOrDefaultAsync();
            Assert.IsNotNull(savedLog);  // MSTest assertion
            Assert.AreEqual("Test", savedLog.ActivityType);  // MSTest assertion
            Assert.AreEqual(1, savedLog.UserId);
            Assert.AreEqual("Test Activity", savedLog.Description);
        }

        [TestMethod]
        public async Task GetUserActivityAsync_ShouldReturnUserLogs()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "MedRecPro_Test2")
                .Options;

            using var context = new ApplicationDbContext(options);
            var logger = new Mock<ILogger<ActivityLogService>>();
            var service = new ActivityLogService(context, logger.Object);

            // Add test data
            context.ActivityLogs.AddRange(
                new ActivityLog { UserId = 1, ActivityType = "Login", ActivityTimestamp = DateTime.UtcNow },
                new ActivityLog { UserId = 1, ActivityType = "Create", ActivityTimestamp = DateTime.UtcNow },
                new ActivityLog { UserId = 2, ActivityType = "Update", ActivityTimestamp = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            // Act
            var results = await service.GetUserActivityAsync(1, 10);

            // Assert
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.UserId == 1));
        }

        [TestMethod]
        public async Task GetActivityByEndpointAsync_ShouldReturnEndpointLogs()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "MedRecPro_Test3")
                .Options;

            using var context = new ApplicationDbContext(options);
            var logger = new Mock<ILogger<ActivityLogService>>();
            var service = new ActivityLogService(context, logger.Object);

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
            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.ControllerName == "Labels"));
        }
    }
}
