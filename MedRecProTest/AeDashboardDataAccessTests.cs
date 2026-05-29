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
        /// Verifies that GetAeRiskSignalsByDocumentAsync collapses pharmacologic-class
        /// cartesian duplicates while preserving genuinely distinct signals.
        /// </summary>
        /// <remarks>
        /// vw_AeRisk joins the pharmacologic-class subquery on DocumentGUID only, so a
        /// product with multiple ingredient substances/classes emits each source AE
        /// statistic once per class. Those copies share one tmp_FlattenedAdverseEventTableID
        /// and must collapse to a single signal; rows with different source identifiers
        /// (e.g. the same term at a different study arm) must be retained.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        [TestMethod]
        public async Task GetAeRiskSignalsByDocumentAsync_DeduplicatesPharmacologicClassFanout()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Three class-join copies of one AE statistic: distinct risk-row PKs but a
            // shared tmp_FlattenedAdverseEventTableID, exactly as vw_AeRisk emits for a
            // product with multiple ingredient substances/pharmacologic classes.
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 1, adverseEventId: 11, parameterName: "Nausea");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 2, adverseEventId: 11, parameterName: "Nausea");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 3, adverseEventId: 11, parameterName: "Nausea");

            // A genuinely distinct signal (different source AE statistic) must survive.
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 4, adverseEventId: 22, parameterName: "Vomiting");

            var signals = await DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                PkSecret,
                logger);

            // Four physical rows collapse to two unique signals.
            Assert.AreEqual(2, signals.Count);
            Assert.AreEqual(1, signals.Count(signal => signal.ParameterName == "Nausea"));
            Assert.AreEqual(1, signals.Count(signal => signal.ParameterName == "Vomiting"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that same-term rows at the same dose, comparator, and (empty) population
        /// context collapse to the most statistically powered arm.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        [TestMethod]
        public async Task GetAeRiskSignalsByDocumentAsync_CollapsesMultiArmKeepingMostPowered()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Same term/dose/comparator reported for a pooled arm and a smaller subgroup arm
            // with no distinguishing population context: indistinguishable on screen, so they
            // must collapse to the most-powered (largest-N) arm.
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 1, adverseEventId: 201, parameterName: "Nausea", armN: 2116, comparatorN: 1261, rr: 2.75);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 2, adverseEventId: 202, parameterName: "Nausea", armN: 133, comparatorN: 67, rr: 2.33);

            var signals = await DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                PkSecret,
                logger);

            // Collapses to one row, keeping the pooled (largest-N) arm.
            Assert.AreEqual(1, signals.Count);
            var kept = signals.Single();
            Assert.AreEqual("Nausea", kept.ParameterName);
            Assert.AreEqual(2116, kept.ArmN!.Value);
            Assert.AreEqual(2.75, kept.RR!.Value, 1e-9);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that same-term rows carrying distinct labeled subpopulations are preserved.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        [TestMethod]
        public async Task GetAeRiskSignalsByDocumentAsync_PreservesDistinctSubpopulations()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Same term but genuinely different labeled subpopulations are distinct evidence
            // a reader can tell apart, so they must not be merged.
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 1, adverseEventId: 301, parameterName: "Nausea", armN: 2116, subpopulation: "Pooled");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 2, adverseEventId: 302, parameterName: "Nausea", armN: 133, subpopulation: "Pediatric");

            var signals = await DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                PkSecret,
                logger);

            Assert.AreEqual(2, signals.Count);
            Assert.IsTrue(signals.All(signal => signal.ParameterName == "Nausea"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the same adverse-event term reported across different study
        /// contexts is preserved as separate clinical outcomes, not merged.
        /// </summary>
        /// <remarks>
        /// Mirrors the Afinitor case where a single term (e.g. Stomatitis) is reported in
        /// several trials (BOLERO-2, RADIANT-3, EXIST-1); each trial is a distinct outcome
        /// the prescriber must see.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        [TestMethod]
        public async Task GetAeRiskSignalsByDocumentAsync_PreservesDistinctStudyContexts()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Same term, same dose/comparator, but three different trials. studyContext is in
            // the merge key, so all three are distinct outcomes and must survive.
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 1, adverseEventId: 401, parameterName: "Stomatitis", armN: 482, studyContext: "BOLERO-2", population: null);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 2, adverseEventId: 402, parameterName: "Stomatitis", armN: 204, studyContext: "RADIANT-3", population: null);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 3, adverseEventId: 403, parameterName: "Stomatitis", armN: 78, studyContext: "EXIST-1", population: null);

            var signals = await DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                PkSecret,
                logger);

            Assert.AreEqual(3, signals.Count);
            Assert.IsTrue(signals.All(signal => signal.ParameterName == "Stomatitis"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the same adverse-event term reported for different populations is
        /// preserved as separate clinical outcomes, not merged.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        [TestMethod]
        public async Task GetAeRiskSignalsByDocumentAsync_PreservesDistinctPopulations()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Same term, same (null) studyContext, but different cohorts. population is in the
            // merge key, so both cohorts are kept.
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 1, adverseEventId: 501, parameterName: "Rash", armN: 482, studyContext: null, population: "Breast Cancer");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 2, adverseEventId: 502, parameterName: "Rash", armN: 204, studyContext: null, population: "PNET");

            var signals = await DtoLabelAccess.GetAeRiskSignalsByDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                PkSecret,
                logger);

            Assert.AreEqual(2, signals.Count);
            Assert.IsTrue(signals.All(signal => signal.ParameterName == "Rash"));

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
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 1, adverseEventId: 11, parameterName: "Headache", rr: 5.0, numberNeeded: 10);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 2, adverseEventId: 12, parameterName: "Nausea", rr: 2.0, numberNeeded: 30);

            var triage = await DtoLabelAccess.GetAeTriageViewAsync(context, DtoLabelAccessTestHelper.TestDocumentGuid, PkSecret, logger);
            var forest = await DtoLabelAccess.GetAeForestPlotAsync(context, DtoLabelAccessTestHelper.TestDocumentGuid, PkSecret, logger);
            var quadrant = await DtoLabelAccess.GetAeQuadrantViewAsync(context, DtoLabelAccessTestHelper.TestDocumentGuid, PkSecret, logger);

            Assert.IsNotNull(triage);
            Assert.AreEqual("ASPIRIN", triage.Product!.ProductName);
            Assert.AreEqual(4, triage.Tiers.Count);
            // Header row count is reconciled with the de-duplicated signal set (two distinct
            // terms), not the seeded summary-view aggregate.
            Assert.AreEqual(2, triage.Product!.RowCount);
            Assert.IsNotNull(forest);
            Assert.AreEqual("Headache", forest.Signals.First().ParameterName);
            Assert.IsNotNull(quadrant);
            Assert.AreEqual(2, quadrant.Points.Count);
            Assert.IsTrue(quadrant.Points.All(point => point.PrecisionX >= 0 && point.PrecisionX <= 1));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that triage tier signals cluster by adverse-event term, with clusters
        /// ordered by each term's lowest NNH and rows ascending by NNH within a cluster.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeTriageViewAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        [TestMethod]
        public async Task GetAeTriageViewAsync_ClustersSignalsByTermOrderedByLowestNnh()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");

            // Two terms, two trials each. All rows are elevated with a tight CI and low NNH so
            // they land in the same (Counsel) tier. "Bravo" has the lowest NNH (2), so its
            // cluster must lead even though "Alpha" sorts first alphabetically.
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 1, adverseEventId: 11, parameterName: "Bravo", numberNeeded: 2, rrLowerBound: 2.0, rrUpperBound: 4.0, studyContext: "Trial-1");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 2, adverseEventId: 12, parameterName: "Bravo", numberNeeded: 5, rrLowerBound: 2.0, rrUpperBound: 4.0, studyContext: "Trial-2");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 3, adverseEventId: 13, parameterName: "Alpha", numberNeeded: 3, rrLowerBound: 2.0, rrUpperBound: 4.0, studyContext: "Trial-1");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 4, adverseEventId: 14, parameterName: "Alpha", numberNeeded: 4, rrLowerBound: 2.0, rrUpperBound: 4.0, studyContext: "Trial-2");

            var triage = await DtoLabelAccess.GetAeTriageViewAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                PkSecret,
                logger);

            Assert.IsNotNull(triage);
            var counsel = triage!.Tiers.Single(tier => tier.Tier == AeCounselingTier.Counsel);

            // Clustered by term (Bravo before Alpha because Bravo's lowest NNH is smaller),
            // ascending by NNH within each cluster.
            CollectionAssert.AreEqual(
                new[] { "Bravo", "Bravo", "Alpha", "Alpha" },
                counsel.Signals.Select(signal => signal.ParameterName).ToList());
            CollectionAssert.AreEqual(
                new[] { 2.0, 5.0, 3.0, 4.0 },
                counsel.Signals.Select(signal => signal.NumberNeeded!.Value).ToList());

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
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid, riskId: 1, adverseEventId: 11, parameterName: "Headache", rr: 4.0);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, riskId: 2, adverseEventId: 12, parameterName: "Headache", rr: 1.5);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, riskId: 3, adverseEventId: 13, parameterName: "Cough", rr: 2.0);

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
