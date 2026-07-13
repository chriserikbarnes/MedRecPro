using MedRecPro.Controllers;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service;
using MedRecPro.Service.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Controller-level tests for application settings endpoints.
    /// </summary>
    /// <remarks>
    /// These tests invoke <see cref="SettingsController"/> directly so endpoint
    /// behavior can be verified without starting Kestrel or contacting Azure services.
    /// </remarks>
    /// <seealso cref="SettingsController"/>
    [TestClass]
    public class SettingsControllerTests
    {
        #region cache tests

        /**************************************************************/
        /// <summary>
        /// Verifies the managed cache clear endpoint uses the injected cache seam and returns success.
        /// </summary>
        /// <remarks>
        /// The endpoint should not depend on <see cref="MedRecPro.Helpers.PerformanceHelper.Initialized"/>
        /// because the application cache is now registered through dependency injection.
        /// </remarks>
        /// <seealso cref="SettingsController.ClearManagedCache"/>
        /// <seealso cref="IAppCache.ResetManaged"/>
        [TestMethod]
        public void ClearManagedCache_UsesAppCacheAndReturnsOk()
        {
            #region implementation
            var appCache = new Mock<IAppCache>(MockBehavior.Strict);
            appCache
                .Setup(cache => cache.ResetManaged());

            var controller = createController(appCache.Object);

            var result = controller.ClearManagedCache();

            var okResult = result as OkObjectResult;
            Assert.IsNotNull(okResult);
            Assert.AreEqual(StatusCodes.Status200OK, okResult.StatusCode ?? StatusCodes.Status200OK);
            Assert.AreEqual(true, getResponseProperty<bool>(okResult.Value!, "success"));
            Assert.AreEqual(
                "Managed cache successfully cleared",
                getResponseProperty<string>(okResult.Value!, "message"));
            appCache.Verify(cache => cache.ResetManaged(), Times.Once);

            #endregion
        }

        #endregion

        #region log endpoint tests

        /**************************************************************/
        /// <summary>
        /// Verifies invalid administrative log paging and level inputs return RFC 7807 validation details rather than
        /// being silently coerced to a different query.
        /// </summary>
        /// <seealso cref="SettingsController.GetLogs(int, int, string)"/>
        [TestMethod]
        public void GetLogs_InvalidPagingAndLevel_ReturnsValidationProblemDetails()
        {
            #region implementation

            var appCache = new Mock<IAppCache>(MockBehavior.Loose);
            var controller = createController(appCache.Object);

            var invalidPaging = controller.GetLogs(pageNumber: 0, pageSize: 100);
            var pagingProblem = (invalidPaging as ObjectResult)?.Value as ValidationProblemDetails;
            var invalidLevel = controller.GetLogs(pageNumber: 1, pageSize: 100, minLevel: "UnknownLevel");
            var levelProblem = (invalidLevel as ObjectResult)?.Value as ValidationProblemDetails;

            Assert.IsNotNull(pagingProblem);
            Assert.AreEqual(StatusCodes.Status400BadRequest, pagingProblem.Status);
            Assert.IsTrue(pagingProblem.Errors.ContainsKey("pageNumber"));
            Assert.IsNotNull(levelProblem);
            Assert.AreEqual(StatusCodes.Status400BadRequest, levelProblem.Status);
            Assert.IsTrue(levelProblem.Errors.ContainsKey("minLevel"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the log endpoint returns the typed administrative DTO and only the safe exception summary.
        /// </summary>
        /// <seealso cref="LogPageResponseDto"/>
        /// <seealso cref="LogEntryResponseDto"/>
        [TestMethod]
        public void GetLogs_RetainedException_ReturnsTypedSafeEntry()
        {
            #region implementation

            var appCache = new Mock<IAppCache>(MockBehavior.Loose);
            var logProvider = new UserLoggerProvider(settings: Options.Create(new LoggingSettings()));
            logProvider.CreateLogger("SettingsControllerTests")
                .LogError(new InvalidOperationException("token=must-not-be-exposed"), "Administrative log retrieval test");
            var controller = createController(appCache.Object, logProvider);

            var result = controller.GetLogs();
            var page = (result as OkObjectResult)?.Value as LogPageResponseDto;

            Assert.IsNotNull(page);
            Assert.AreEqual(1, page.TotalCount);
            Assert.AreEqual("InvalidOperationException", page.Entries[0].ExceptionType);
            Assert.AreEqual(
                "An exception was recorded. See the correlated server-side log event.",
                page.Entries[0].ExceptionMessage);
            Assert.IsFalse(page.Entries[0].Message!.Contains("must-not-be-exposed", StringComparison.Ordinal));

            #endregion
        }

        #endregion

        #region helpers

        /**************************************************************/
        /// <summary>
        /// Creates a settings controller with inert dependencies for direct endpoint tests.
        /// </summary>
        /// <param name="appCache">Application cache seam supplied by the test.</param>
        /// <returns>A configured <see cref="SettingsController"/> instance.</returns>
        /// <seealso cref="SettingsController"/>
        /// <seealso cref="IAppCache"/>
        private static SettingsController createController(
            IAppCache appCache,
            UserLoggerProvider? loggerProvider = null)
        {
            #region implementation
            var configuration = createConfiguration();
            var memoryCache = new MemoryCache(new MemoryCacheOptions());
            var tokenProvider = new AzureManagementTokenProvider(configuration);
            var metricsService = new AzureSqlMetricsService(configuration, memoryCache, tokenProvider);
            var appTokenProvider = new AzureAppTokenProvider(
                configuration,
                NullLogger<AzureAppTokenProvider>.Instance);
            loggerProvider ??= new UserLoggerProvider(
                settings: Options.Create(new LoggingSettings()));

            return new SettingsController(
                configuration,
                NullLogger<SettingsController>.Instance,
                metricsService,
                appCache,
                appTokenProvider,
                loggerProvider);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the configuration values required by settings controller dependencies.
        /// </summary>
        /// <returns>An in-memory <see cref="IConfiguration"/> instance.</returns>
        /// <seealso cref="SettingsController"/>
        /// <seealso cref="AzureSqlMetricsService"/>
        private static IConfiguration createConfiguration()
        {
            #region implementation
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Development",
                    ["Security:DB:PKSecret"] = "unit-test-pk-secret",
                    ["Azure:SqlDatabase:ResourceId"] = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/test/providers/Microsoft.Sql/servers/test/databases/test",
                    ["Azure:SqlDatabase:MetricsRegion"] = "eastus",
                    ["Authentication:Microsoft:TenantId"] = "00000000-0000-0000-0000-000000000000",
                    ["Authentication:Microsoft:ClientId"] = "11111111-1111-1111-1111-111111111111",
                    ["Authentication:Microsoft:ClientSecret:Dev"] = "unit-test-secret"
                })
                .Build();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Reads a property from an anonymous response object.
        /// </summary>
        /// <typeparam name="T">Expected property type.</typeparam>
        /// <param name="response">Response object returned by the controller.</param>
        /// <param name="propertyName">Name of the property to read.</param>
        /// <returns>The typed property value.</returns>
        /// <exception cref="AssertFailedException">Thrown when the property is missing or has an unexpected type.</exception>
        /// <seealso cref="OkObjectResult"/>
        private static T getResponseProperty<T>(object response, string propertyName)
        {
            #region implementation
            var property = response.GetType().GetProperty(propertyName);
            Assert.IsNotNull(property, $"Response property '{propertyName}' was not found.");

            var value = property.GetValue(response);
            Assert.IsInstanceOfType<T>(value);

            return (T)value!;

            #endregion
        }

        #endregion
    }
}
