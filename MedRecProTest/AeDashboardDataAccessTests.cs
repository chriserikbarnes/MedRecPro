using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// SQLite-backed tests for AE dashboard read data-access methods.
    /// </summary>
    /// <remarks>
    /// View entities are seeded through <see cref="DtoLabelAccessTestHelper"/> raw
    /// SQL helpers because EF Core keyless views cannot be added through DbSet.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="LabelView.AeDrugSummary"/>
    /// <seealso cref="LabelView.FlattenedAdverseEventRiskTable"/>
    [TestClass]
    public class AeDashboardDataAccessTests
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// Encryption secret used by dashboard data-access tests.
        /// </summary>
        private const string PkSecret = DtoLabelAccessTestHelper.TestPkSecret;

        #endregion constants

        #region initialization

        /**************************************************************/
        /// <summary>
        /// Clears static caches and encryption state before each test.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation

            DtoLabelAccessTestHelper.ClearCache();

            #endregion
        }

        #endregion initialization

        #region product catalog tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetAeDrugSummariesAsync maps, encrypts, searches, pages, scores, and marks favorites.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeDrugSummariesAsync(ApplicationDbContext, string, ILogger, string?, long?, int?, int?)"/>
        [TestMethod]
        public async Task GetAeDrugSummariesAsync_MapsEncryptsSearchesPagesScoresAndMarksFavorites()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();
            var userId = 7001L;
            await seedUserAsync(context, userId);

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN", significantElevatedCount: 10);
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, "IBUPROFEN", substanceName: "Ibuprofen", activeMoietyId: 40, ingredientSubstanceId: 50, pharmacologicClassId: 60);
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid3, "ACETAMINOPHEN", substanceName: "Acetaminophen");
            context.AspNetUserFavorites.Add(new AspNetUserFavorite
            {
                UserId = userId,
                DocumentGUID = DtoLabelAccessTestHelper.TestDocumentGuid2,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var allProducts = await DtoLabelAccess.GetAeDrugSummariesAsync(context, PkSecret, logger, userId: userId);
            var searched = await DtoLabelAccess.GetAeDrugSummariesAsync(context, PkSecret, logger, productSearch: "ibu", page: 1, size: 1);

            Assert.AreEqual(3, allProducts.Count);
            var favorite = allProducts.Single(product => product.DocumentGUID == DtoLabelAccessTestHelper.TestDocumentGuid2);
            Assert.IsTrue(favorite.IsFavorite);
            Assert.IsFalse(string.IsNullOrWhiteSpace(favorite.EncryptedActiveMoietyID));
            Assert.AreEqual(40, favorite.ActiveMoietyID);
            Assert.IsTrue(favorite.Score.HasValue);
            Assert.IsFalse(string.IsNullOrWhiteSpace(favorite.ScoreReason));
            Assert.AreEqual(1, searched.Count);
            Assert.AreEqual("IBUPROFEN", searched.Single().ProductName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that user-specific favorite enrichment is not served from anonymous cached catalog data.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeDrugSummariesAsync(ApplicationDbContext, string, ILogger, string?, long?, int?, int?)"/>
        [TestMethod]
        public async Task GetAeDrugSummariesAsync_AnonymousCacheThenUserFavorite_DoesNotReturnSharedFavoriteState()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();
            var userId = 7002L;
            await seedUserAsync(context, userId);

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, "IBUPROFEN");

            var anonymous = await DtoLabelAccess.GetAeDrugSummariesAsync(context, PkSecret, logger);
            context.AspNetUserFavorites.Add(new AspNetUserFavorite
            {
                UserId = userId,
                DocumentGUID = DtoLabelAccessTestHelper.TestDocumentGuid2,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var userSpecific = await DtoLabelAccess.GetAeDrugSummariesAsync(context, PkSecret, logger, userId: userId);

            Assert.IsTrue(anonymous.All(product => !product.IsFavorite));
            Assert.IsTrue(userSpecific.Single(product => product.DocumentGUID == DtoLabelAccessTestHelper.TestDocumentGuid2).IsFavorite);

            #endregion
        }

        #endregion product catalog tests

        #region signal and view tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetAeRiskSignalsByDocumentAsync maps encrypted IDs and applies comparator and fragile filters.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        [TestMethod]
        public async Task GetAeRiskSignalsByDocumentAsync_MapsEncryptsFiltersAndDerivesSignals()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 1, parameterName: "Headache", isPlaceboControlled: true);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 2, parameterName: "Nausea", isPlaceboControlled: false, calculationFlags: "LOW_EVENT_COUNT");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, riskId: 3, parameterName: "Rash");

            var placeboSignals = await DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                PkSecret,
                logger,
                AeComparatorMix.Placebo,
                includeFragile: false);
            var activeSignals = await DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                PkSecret,
                logger,
                AeComparatorMix.Active,
                includeFragile: true);

            Assert.AreEqual(1, placeboSignals.Count);
            Assert.AreEqual("Headache", placeboSignals.Single().ParameterName);
            Assert.AreEqual(1, placeboSignals.Single().FlattenedAdverseEventRiskTableID);
            Assert.AreNotEqual(AePrecisionClass.Fragile, placeboSignals.Single().PrecisionClass);
            Assert.AreEqual(1, activeSignals.Count);
            Assert.AreEqual(AePrecisionClass.Fragile, activeSignals.Single().PrecisionClass);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetAeTriageViewAsync, GetAeForestPlotAsync, and GetAeQuadrantViewAsync assemble containers.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeTriageViewAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        /// <seealso cref="DtoLabelAccess.GetAeForestPlotAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        /// <seealso cref="DtoLabelAccess.GetAeQuadrantViewAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        [TestMethod]
        public async Task GetAeTriageForestAndQuadrantViews_WithSeededProduct_AssembleExpectedContainers()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 1, parameterName: "Headache", rr: 5.0, numberNeeded: 10);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 2, parameterName: "Nausea", rr: 2.0, numberNeeded: 30);

            var triage = await DtoLabelAccess.GetAeTriageViewAsync(context, DtoLabelAccessTestHelper.TestDocumentGuid, PkSecret, logger);
            var forest = await DtoLabelAccess.GetAeForestPlotAsync(context, DtoLabelAccessTestHelper.TestDocumentGuid, PkSecret, logger);
            var quadrant = await DtoLabelAccess.GetAeQuadrantViewAsync(context, DtoLabelAccessTestHelper.TestDocumentGuid, PkSecret, logger);

            Assert.IsNotNull(triage);
            Assert.AreEqual("ASPIRIN", triage.Product!.ProductName);
            Assert.AreEqual(4, triage.Tiers.Count);
            Assert.IsNotNull(forest);
            Assert.AreEqual("Headache", forest.Signals.First().ParameterName);
            Assert.IsNotNull(quadrant);
            Assert.AreEqual(2, quadrant.Points.Count);
            Assert.IsTrue(quadrant.Points.All(point => point.PrecisionX >= 0 && point.PrecisionX <= 1));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetAeReverseLookupAsync respects the supplied document scope.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeReverseLookupAsync(ApplicationDbContext, string, string, ILogger, IEnumerable{Guid}?)"/>
        [TestMethod]
        public async Task GetAeReverseLookupAsync_WithDocumentScope_ReturnsOnlyScopedMatches()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, "IBUPROFEN");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid, riskId: 1, parameterName: "Nausea", rr: 3.0);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, riskId: 2, parameterName: "Nausea", rr: 2.0);

            var result = await DtoLabelAccess.GetAeReverseLookupAsync(
                context,
                "nausea",
                PkSecret,
                logger,
                new[] { DtoLabelAccessTestHelper.TestDocumentGuid2 });

            Assert.AreEqual(1, result.Matches.Count);
            Assert.AreEqual("IBUPROFEN", result.Matches.Single().Drug!.ProductName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetAeInterchangeAsync returns comparisons and null for missing products.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeInterchangeAsync(ApplicationDbContext, Guid, Guid, string, ILogger, bool)"/>
        [TestMethod]
        public async Task GetAeInterchangeAsync_WithPresentAndMissingProducts_ReturnsComparisonOrNull()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN", placeboCoverage: true, activeCoverage: false);
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, "IBUPROFEN", placeboCoverage: false, activeCoverage: true);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid, riskId: 1, parameterName: "Headache", rr: 4.0);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, riskId: 2, parameterName: "Headache", rr: 1.5);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, riskId: 3, parameterName: "Cough", rr: 2.0);

            var comparison = await DtoLabelAccess.GetAeInterchangeAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                PkSecret,
                logger);
            var missing = await DtoLabelAccess.GetAeInterchangeAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                Guid.Parse("99999999-9999-9999-9999-999999999999"),
                PkSecret,
                logger);

            Assert.IsNotNull(comparison);
            Assert.AreEqual(1, comparison.BWorseCount + comparison.AWorseCount);
            Assert.AreEqual(1, comparison.OnlyBCount);
            Assert.IsNull(missing);

            #endregion
        }

        #endregion signal and view tests

        #region helpers

        /**************************************************************/
        /// <summary>
        /// Seeds a minimal authenticated user for favorite FK-compatible tests.
        /// </summary>
        private static async Task seedUserAsync(ApplicationDbContext context, long userId)
        {
            #region implementation

            context.Users.Add(new User
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

        #endregion helpers
    }
}
