using MedRecPro.Api.Controllers;
using MedRecPro.Controllers;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Reflection;
using System.Security.Claims;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Controller-level tests for the AE dashboard API endpoints.
    /// </summary>
    /// <remarks>
    /// These tests invoke <see cref="AdverseEventController"/> directly while
    /// seeding the same SQLite-backed dashboard view tables used by the focused
    /// AE dashboard data-access tests.
    /// </remarks>
    /// <seealso cref="AdverseEventController"/>
    /// <seealso cref="DtoLabelAccessTestHelper"/>
    [TestClass]
    public class AdverseEventControllerTests
    {
        #region test constants

        /**************************************************************/
        /// <summary>
        /// Encryption secret used for controller claim and DTO encryption tests.
        /// </summary>
        private const string PkSecret = DtoLabelAccessTestHelper.TestPkSecret;

        #endregion test constants

        #region initialization

        /**************************************************************/
        /// <summary>
        /// Resets shared caches and user encryption configuration before each controller test.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation

            DtoLabelAccessTestHelper.ClearCache();
            User.SetConfiguration(createConfiguration());

            #endregion
        }

        #endregion initialization

        #region product catalog tests

        /**************************************************************/
        /// <summary>
        /// Verifies anonymous product catalog reads return product rows and pagination headers.
        /// </summary>
        /// <seealso cref="AdverseEventController.GetProducts(string?, int?, int?)"/>
        [TestMethod]
        public async Task GetProducts_AnonymousPaged_ReturnsProductsAndPaginationHeaders()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var controller = createController(context, createConfiguration());

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN", significantElevatedCount: 10);
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, "IBUPROFEN", significantElevatedCount: 5);
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid3, "ACETAMINOPHEN", significantElevatedCount: 1);

            var result = await controller.GetProducts(null, 1, 2);
            var products = getOkValue<List<AeDrugSummaryDto>>(result);

            Assert.AreEqual(2, products.Count);
            Assert.IsTrue(products.All(product => !product.IsFavorite));
            Assert.AreEqual("1", controller.Response.Headers["X-Page-Number"].ToString());
            Assert.AreEqual("2", controller.Response.Headers["X-Page-Size"].ToString());
            Assert.AreEqual("2", controller.Response.Headers["X-Total-Count"].ToString());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies product catalog reads materialize SQL decimal dose coverage values and return the DTO double shape.
        /// </summary>
        /// <seealso cref="AdverseEventController.GetProducts(string?, int?, int?)"/>
        /// <seealso cref="LabelView.AeDrugSummary.DoseCoverage"/>
        [TestMethod]
        public async Task GetProducts_DecimalDoseCoverage_ReturnsDoubleDtoCoverage()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var controller = createController(context, createConfiguration());

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(
                connection,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                "ASPIRIN",
                doseCoverage: 0.333333m);

            var result = await controller.GetProducts(null, null, null);
            var product = getOkValue<List<AeDrugSummaryDto>>(result).Single();

            Assert.AreEqual(typeof(decimal), typeof(LabelView.AeDrugSummary).GetProperty(nameof(LabelView.AeDrugSummary.DoseCoverage))!.PropertyType);
            Assert.AreEqual(0.333333d, product.DoseCoverage, 0.000001d);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies authenticated product catalog reads enrich the current user's favorite state.
        /// </summary>
        /// <seealso cref="AdverseEventController.GetProducts(string?, int?, int?)"/>
        [TestMethod]
        public async Task GetProducts_AuthenticatedUser_EnrichesFavoriteState()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var userId = 9101L;
            await seedUserAsync(context, userId);
            var controller = createController(context, createConfiguration(), createAuthenticatedPrincipal(userId));

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, "IBUPROFEN");
            context.AspNetUserFavorites.Add(new AspNetUserFavorite
            {
                UserId = userId,
                DocumentGUID = DtoLabelAccessTestHelper.TestDocumentGuid2,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var result = await controller.GetProducts(null, null, null);
            var products = getOkValue<List<AeDrugSummaryDto>>(result);
            var favorite = products.Single(product => product.DocumentGUID == DtoLabelAccessTestHelper.TestDocumentGuid2);

            Assert.IsTrue(favorite.IsFavorite);
            Assert.IsFalse(products.Single(product => product.DocumentGUID == DtoLabelAccessTestHelper.TestDocumentGuid).IsFavorite);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies authenticated catalog reads return unauthorized when the claim user cannot be resolved.
        /// </summary>
        /// <seealso cref="AdverseEventController.GetProducts(string?, int?, int?)"/>
        [TestMethod]
        public async Task GetProducts_AuthenticatedUserCannotResolve_ReturnsUnauthorized()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var controller = createController(context, createConfiguration(), createAuthenticatedPrincipal(9999));

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");

            var result = await controller.GetProducts(null, null, null);

            assertStatus(result.Result!, StatusCodes.Status401Unauthorized);

            #endregion
        }

        #endregion product catalog tests

        #region feature gate tests

        /**************************************************************/
        /// <summary>
        /// Verifies every AE dashboard endpoint returns service unavailable when the feature flag is disabled.
        /// </summary>
        /// <seealso cref="AdverseEventController"/>
        [TestMethod]
        public async Task AllEndpoints_FeatureDisabled_ReturnServiceUnavailable()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var controller = createController(context, createConfiguration(enabled: false));

            assertStatus((await controller.GetProducts(null, null, null)).Result!, StatusCodes.Status503ServiceUnavailable);
            assertStatus((await controller.GetFavoriteProducts(null, null)).Result!, StatusCodes.Status503ServiceUnavailable);
            assertStatus(await controller.FavoriteProduct(DtoLabelAccessTestHelper.TestDocumentGuid), StatusCodes.Status503ServiceUnavailable);
            assertStatus(await controller.UnfavoriteProduct(DtoLabelAccessTestHelper.TestDocumentGuid), StatusCodes.Status503ServiceUnavailable);
            assertStatus((await controller.GetTriage(DtoLabelAccessTestHelper.TestDocumentGuid, null, true)).Result!, StatusCodes.Status503ServiceUnavailable);
            assertStatus((await controller.GetForest(DtoLabelAccessTestHelper.TestDocumentGuid, null, true)).Result!, StatusCodes.Status503ServiceUnavailable);
            assertStatus((await controller.GetQuadrant(DtoLabelAccessTestHelper.TestDocumentGuid, null, true)).Result!, StatusCodes.Status503ServiceUnavailable);
            assertStatus((await controller.GetReverseLookup("nausea", null)).Result!, StatusCodes.Status503ServiceUnavailable);
            assertStatus((await controller.GetInterchange(
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                false)).Result!, StatusCodes.Status503ServiceUnavailable);

            #endregion
        }

        #endregion feature gate tests

        #region favorite tests

        /**************************************************************/
        /// <summary>
        /// Verifies favorite product reads require a resolved claims user and return only that user's favorites.
        /// </summary>
        /// <seealso cref="AdverseEventController.GetFavoriteProducts(int?, int?)"/>
        [TestMethod]
        public async Task GetFavoriteProducts_AuthenticatedUser_ReturnsOnlyCurrentUserFavorites()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var userId = 9201L;
            await seedUserAsync(context, userId);
            await seedUserAsync(context, 9202L);

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, "IBUPROFEN");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid3, "ACETAMINOPHEN");
            context.AspNetUserFavorites.AddRange(
                new AspNetUserFavorite
                {
                    UserId = userId,
                    DocumentGUID = DtoLabelAccessTestHelper.TestDocumentGuid,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10)
                },
                new AspNetUserFavorite
                {
                    UserId = userId,
                    DocumentGUID = DtoLabelAccessTestHelper.TestDocumentGuid2,
                    CreatedAt = DateTime.UtcNow
                },
                new AspNetUserFavorite
                {
                    UserId = 9202,
                    DocumentGUID = DtoLabelAccessTestHelper.TestDocumentGuid3,
                    CreatedAt = DateTime.UtcNow
                });
            await context.SaveChangesAsync();

            var anonymousController = createController(context, createConfiguration());
            var anonymous = await anonymousController.GetFavoriteProducts(null, null);
            var authenticatedController = createController(context, createConfiguration(), createAuthenticatedPrincipal(userId));
            var result = await authenticatedController.GetFavoriteProducts(1, 1);
            var favorites = getOkValue<List<AeDrugSummaryDto>>(result);

            assertStatus(anonymous.Result!, StatusCodes.Status401Unauthorized);
            Assert.AreEqual(1, favorites.Count);
            Assert.AreEqual("IBUPROFEN", favorites.Single().ProductName);
            Assert.IsTrue(favorites.Single().IsFavorite);
            Assert.AreEqual("1", authenticatedController.Response.Headers["X-Page-Number"].ToString());
            Assert.AreEqual("1", authenticatedController.Response.Headers["X-Page-Size"].ToString());
            Assert.AreEqual("1", authenticatedController.Response.Headers["X-Total-Count"].ToString());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies favorite mutations bind to the claims user and return not found for dashboard-ineligible products.
        /// </summary>
        /// <seealso cref="AdverseEventController.FavoriteProduct(Guid)"/>
        /// <seealso cref="AdverseEventController.UnfavoriteProduct(Guid)"/>
        [TestMethod]
        public async Task FavoriteMutations_UseClaimUserAndReportMissingProducts()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var userId = 9301L;
            await seedUserAsync(context, userId);
            await seedUserAsync(context, 9302L);
            var controller = createController(context, createConfiguration(), createAuthenticatedPrincipal(userId));

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");

            var add = await controller.FavoriteProduct(DtoLabelAccessTestHelper.TestDocumentGuid);
            var saved = await context.AspNetUserFavorites.SingleAsync();
            var remove = await controller.UnfavoriteProduct(DtoLabelAccessTestHelper.TestDocumentGuid);
            var missing = await controller.FavoriteProduct(DtoLabelAccessTestHelper.TestDocumentGuid2);

            assertStatus(add, StatusCodes.Status204NoContent);
            Assert.AreEqual(userId, saved.UserId);
            Assert.AreEqual(DtoLabelAccessTestHelper.TestDocumentGuid, saved.DocumentGUID);
            assertStatus(remove, StatusCodes.Status204NoContent);
            Assert.AreEqual(0, await context.AspNetUserFavorites.CountAsync());
            assertStatus(missing, StatusCodes.Status404NotFound);

            #endregion
        }

        #endregion favorite tests

        #region product visualization tests

        /**************************************************************/
        /// <summary>
        /// Verifies product visualization endpoints validate empty GUIDs, report missing products, and return seeded views.
        /// </summary>
        /// <seealso cref="AdverseEventController.GetTriage(Guid, AeComparatorMix?, bool)"/>
        /// <seealso cref="AdverseEventController.GetForest(Guid, AeComparatorMix?, bool)"/>
        /// <seealso cref="AdverseEventController.GetQuadrant(Guid, AeComparatorMix?, bool)"/>
        [TestMethod]
        public async Task ProductViews_ValidateMissingProductsAndReturnSeededViews()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var controller = createController(context, createConfiguration());

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 1, parameterName: "Headache", rr: 5.0, numberNeeded: 10);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 2, parameterName: "Nausea", rr: 2.0, numberNeeded: 30);

            var badTriage = await controller.GetTriage(Guid.Empty, null, true);
            var missingForest = await controller.GetForest(DtoLabelAccessTestHelper.TestDocumentGuid2, null, true);
            var triage = getOkValue<AeTriageViewDto>(await controller.GetTriage(DtoLabelAccessTestHelper.TestDocumentGuid, null, true));
            var forest = getOkValue<AeForestPlotDto>(await controller.GetForest(DtoLabelAccessTestHelper.TestDocumentGuid, null, true));
            var quadrant = getOkValue<AeQuadrantViewDto>(await controller.GetQuadrant(DtoLabelAccessTestHelper.TestDocumentGuid, null, true));

            assertStatus(badTriage.Result!, StatusCodes.Status400BadRequest);
            assertStatus(missingForest.Result!, StatusCodes.Status404NotFound);
            Assert.AreEqual("ASPIRIN", triage.Product!.ProductName);
            Assert.AreEqual("Headache", forest.Signals.First().ParameterName);
            Assert.AreEqual(2, quadrant.Points.Count);

            #endregion
        }

        #endregion product visualization tests

        #region reverse lookup and interchange tests

        /**************************************************************/
        /// <summary>
        /// Verifies reverse lookup rejects empty symptoms and respects repeated document scope values.
        /// </summary>
        /// <seealso cref="AdverseEventController.GetReverseLookup(string?, List{Guid}?)"/>
        [TestMethod]
        public async Task GetReverseLookup_RejectsEmptyAndAcceptsDocumentScope()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var controller = createController(context, createConfiguration());

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, "IBUPROFEN");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid, riskId: 1, parameterName: "Nausea", rr: 3.0);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, riskId: 2, parameterName: "Nausea", rr: 2.0);

            var empty = await controller.GetReverseLookup(" ", null);
            var scoped = getOkValue<AeReverseLookupResultDto>(await controller.GetReverseLookup(
                "nausea",
                new List<Guid> { DtoLabelAccessTestHelper.TestDocumentGuid2 }));

            assertStatus(empty.Result!, StatusCodes.Status400BadRequest);
            Assert.AreEqual(1, scoped.Matches.Count);
            Assert.AreEqual("IBUPROFEN", scoped.Matches.Single().Drug!.ProductName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies interchange comparison validates GUIDs, reports missing products, and returns a comparison for seeded products.
        /// </summary>
        /// <seealso cref="AdverseEventController.GetInterchange(Guid, Guid, bool)"/>
        [TestMethod]
        public async Task GetInterchange_ValidatesGuidsAndReturnsComparisonOrNotFound()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var controller = createController(context, createConfiguration());

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, "IBUPROFEN");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid, riskId: 1, parameterName: "Headache", rr: 4.0);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, riskId: 2, parameterName: "Headache", rr: 1.5);

            var empty = await controller.GetInterchange(Guid.Empty, DtoLabelAccessTestHelper.TestDocumentGuid2, false);
            var identical = await controller.GetInterchange(
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                false);
            var missing = await controller.GetInterchange(
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestDocumentGuid3,
                false);
            var comparison = getOkValue<AeInterchangeComparisonDto>(await controller.GetInterchange(
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                false));

            assertStatus(empty.Result!, StatusCodes.Status400BadRequest);
            assertStatus(identical.Result!, StatusCodes.Status400BadRequest);
            assertStatus(missing.Result!, StatusCodes.Status404NotFound);
            Assert.AreEqual("ASPIRIN", comparison.ProductA!.ProductName);
            Assert.AreEqual("IBUPROFEN", comparison.ProductB!.ProductName);

            #endregion
        }

        #endregion reverse lookup and interchange tests

        #region metadata tests

        /**************************************************************/
        /// <summary>
        /// Verifies controller metadata exposes the requested auth, audit, route, and Swagger response attributes.
        /// </summary>
        /// <seealso cref="AdverseEventController"/>
        [TestMethod]
        public void ControllerMetadata_ExposesExpectedAttributes()
        {
            #region implementation

            Assert.IsTrue(typeof(AdverseEventController).IsSubclassOf(typeof(ApiControllerBase)));

            var getProducts = getAction(nameof(AdverseEventController.GetProducts));
            var favoriteProduct = getAction(nameof(AdverseEventController.FavoriteProduct));
            var getInterchange = getAction(nameof(AdverseEventController.GetInterchange));

            Assert.AreEqual("products", getProducts.GetCustomAttribute<HttpGetAttribute>()!.Template);
            assertProduces(getProducts, StatusCodes.Status200OK, typeof(List<AeDrugSummaryDto>));
            assertProduces(getProducts, StatusCodes.Status503ServiceUnavailable);
            Assert.IsTrue(getProducts.GetCustomAttributes<DatabaseLimitAttribute>().Any());
            Assert.IsTrue(getProducts.GetCustomAttributes<DatabaseIntensiveAttribute>().Any());

            Assert.AreEqual("ApiAccess", favoriteProduct.GetCustomAttribute<AuthorizeAttribute>()!.Policy);
            Assert.AreEqual(typeof(ActivityLogActionFilter), favoriteProduct.GetCustomAttribute<ServiceFilterAttribute>()!.ServiceType);
            assertProduces(favoriteProduct, StatusCodes.Status204NoContent);
            assertProduces(favoriteProduct, StatusCodes.Status404NotFound);

            Assert.AreEqual("interchange", getInterchange.GetCustomAttribute<HttpGetAttribute>()!.Template);
            assertProduces(getInterchange, StatusCodes.Status200OK, typeof(AeInterchangeComparisonDto));
            assertProduces(getInterchange, StatusCodes.Status400BadRequest);
            assertProduces(getInterchange, StatusCodes.Status404NotFound);

            #endregion
        }

        #endregion metadata tests

        #region helpers

        /**************************************************************/
        /// <summary>
        /// Creates test configuration with PK encryption and AE dashboard feature settings.
        /// </summary>
        /// <param name="enabled">Whether the AE dashboard feature flag should be enabled.</param>
        /// <returns>An in-memory configuration instance.</returns>
        private static IConfiguration createConfiguration(bool enabled = true)
        {
            #region implementation

            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:DB:PKSecret"] = PkSecret,
                    ["FeatureFlags:AeDashboard:Enabled"] = enabled.ToString()
                })
                .Build();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an authenticated claims principal with a numeric user identifier claim.
        /// </summary>
        /// <param name="userId">User identifier to place into the claims principal.</param>
        /// <returns>An authenticated claims principal.</returns>
        private static ClaimsPrincipal createAuthenticatedPrincipal(long userId)
        {
            #region implementation

            return new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                "TestAuthentication"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="AdverseEventController"/> with a test HTTP context.
        /// </summary>
        /// <param name="context">Database context used by the controller.</param>
        /// <param name="configuration">Configuration used by the controller and user access.</param>
        /// <param name="principal">Optional request principal.</param>
        /// <returns>A configured controller instance.</returns>
        private static AdverseEventController createController(
            ApplicationDbContext context,
            IConfiguration configuration,
            ClaimsPrincipal? principal = null)
        {
            #region implementation

            var logger = new Mock<ILogger<AdverseEventController>>();
            var userDataAccess = createUserDataAccess(context, configuration);
            var controller = new AdverseEventController(
                configuration,
                logger.Object,
                context,
                userDataAccess);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal ?? new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            return controller;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a user data-access service for controller claim-user resolution.
        /// </summary>
        /// <param name="context">Database context used by user access.</param>
        /// <param name="configuration">Configuration with the test PK secret.</param>
        /// <returns>A configured user data-access service.</returns>
        private static UserDataAccess createUserDataAccess(
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            #region implementation

            return new UserDataAccess(
                context,
                new PasswordHasher<User>(),
                new Mock<ILogger<UserDataAccess>>().Object,
                configuration);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Seeds a minimal active user for authenticated controller tests.
        /// </summary>
        /// <param name="context">Database context to seed.</param>
        /// <param name="userId">User identifier to assign.</param>
        private static async Task seedUserAsync(ApplicationDbContext context, long userId)
        {
            #region implementation

            context.AppUsers.Add(new User
            {
                Id = userId,
                UserName = $"user{userId}@example.test",
                NormalizedUserName = $"USER{userId}@EXAMPLE.TEST",
                Email = $"user{userId}@example.test",
                NormalizedEmail = $"USER{userId}@EXAMPLE.TEST",
                PrimaryEmail = $"user{userId}@example.test",
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString()
            });

            await context.SaveChangesAsync();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts an OK result value from an action result.
        /// </summary>
        /// <typeparam name="T">Expected response DTO type.</typeparam>
        /// <param name="result">Action result returned by the controller.</param>
        /// <returns>The typed OK result value.</returns>
        private static T getOkValue<T>(ActionResult<T> result)
        {
            #region implementation

            Assert.IsInstanceOfType(result.Result, typeof(OkObjectResult));
            var ok = (OkObjectResult)result.Result!;
            Assert.IsInstanceOfType(ok.Value, typeof(T));
            return (T)ok.Value!;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts that an action result carries the expected HTTP status code.
        /// </summary>
        /// <param name="result">Action result to inspect.</param>
        /// <param name="statusCode">Expected HTTP status code.</param>
        private static void assertStatus(IActionResult result, int statusCode)
        {
            #region implementation

            var actual = result switch
            {
                ObjectResult objectResult => objectResult.StatusCode,
                StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
                _ => null
            };

            Assert.AreEqual(statusCode, actual);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets a public controller action by name.
        /// </summary>
        /// <param name="name">Action method name.</param>
        /// <returns>The method information for the named action.</returns>
        private static MethodInfo getAction(string name)
        {
            #region implementation

            return typeof(AdverseEventController).GetMethod(name)
                ?? throw new InvalidOperationException($"Unable to find action method '{name}'.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts a controller action declares a Swagger response metadata attribute.
        /// </summary>
        /// <param name="method">Action method to inspect.</param>
        /// <param name="statusCode">Expected status code.</param>
        /// <param name="responseType">Optional expected response type.</param>
        private static void assertProduces(MethodInfo method, int statusCode, Type? responseType = null)
        {
            #region implementation

            var matches = method
                .GetCustomAttributes<ProducesResponseTypeAttribute>()
                .Where(attribute => attribute.StatusCode == statusCode)
                .ToList();

            Assert.IsTrue(matches.Count > 0, $"Missing ProducesResponseType for {statusCode} on {method.Name}.");

            if (responseType != null)
            {
                Assert.IsTrue(
                    matches.Any(attribute => attribute.Type == responseType),
                    $"Missing ProducesResponseType type {responseType.Name} for {statusCode} on {method.Name}.");
            }

            #endregion
        }

        #endregion helpers
    }
}
