using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Service;
using MedRecPro.Service.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Integration.Persistence
{
    /**************************************************************/
    /// <summary>
    /// Contains AE dashboard risk-signal, triage, reverse-lookup, interchange, and view tests.
    /// </summary>
    /// <remarks>
    /// This partial preserves the original AE dashboard test identities and setup.
    /// </remarks>
    /// <seealso cref="AeDashboardDataAccessTests"/>
    public partial class AeDashboardDataAccessTests
    {
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
        /// Class-enriched vw_AeRisk rows join pharmacologic-class context on
        /// DocumentGUID only, so a product with multiple ingredient substances/classes
        /// emits each source AE statistic once per class. Those copies share one
        /// tmp_FlattenedAdverseEventTableID and must collapse to a single signal;
        /// rows with different source identifiers (e.g. the same term at a different
        /// study arm) must be retained.
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
        /// Verifies that product detail data can be loaded from the materialized catalog and reused by tab views.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeProductDetailDataAsync(ApplicationDbContext, Guid, string, Microsoft.Extensions.Logging.ILogger, AeComparatorMix?, bool)"/>
        [TestMethod]
        public async Task GetAeProductDetailDataAsync_WithMaterializedCatalog_LoadsSharedPayload()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedAeDashboardProductCatalogTable(
                connection,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                "CATALOG ASPIRIN",
                significantElevatedCount: 2);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 1, adverseEventId: 11, parameterName: "Headache", rr: 5.0, numberNeeded: 10);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, riskId: 2, adverseEventId: 12, parameterName: "Nausea", rr: 2.0, numberNeeded: 30);

            var detail = await DtoLabelAccess.GetAeProductDetailDataAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                PkSecret,
                logger);
            var triage = await DtoLabelAccess.GetAeTriageViewAsync(context, DtoLabelAccessTestHelper.TestDocumentGuid, PkSecret, logger);
            var forest = await DtoLabelAccess.GetAeForestPlotAsync(context, DtoLabelAccessTestHelper.TestDocumentGuid, PkSecret, logger);
            var quadrant = await DtoLabelAccess.GetAeQuadrantViewAsync(context, DtoLabelAccessTestHelper.TestDocumentGuid, PkSecret, logger);

            Assert.IsNotNull(detail);
            Assert.AreEqual("CATALOG ASPIRIN", detail.Product.ProductName);
            Assert.AreEqual(2, detail.Signals.Count);
            Assert.IsNotNull(triage);
            Assert.AreEqual("CATALOG ASPIRIN", triage.Product!.ProductName);
            Assert.IsNotNull(forest);
            Assert.AreEqual(2, forest.Signals.Count);
            Assert.IsNotNull(quadrant);
            Assert.AreEqual(2, quadrant.Points.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that product-level dashboard views load when pharmacologic class context is null.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeTriageViewAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        /// <seealso cref="DtoLabelAccess.GetAeForestPlotAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        /// <seealso cref="DtoLabelAccess.GetAeQuadrantViewAsync(ApplicationDbContext, Guid, string, ILogger, AeComparatorMix?, bool)"/>
        [TestMethod]
        public async Task GetAeTriageForestAndQuadrantViews_WithNullPharmClassRiskRows_LoadsProduct()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(
                connection,
                DtoLabelAccessTestHelper.TestDocumentGuid3,
                riskId: 9201,
                adverseEventId: 9201,
                standardizedId: 9201,
                productName: "RUFINAMIDE",
                substanceName: "Rufinamide",
                unii: "PB6D638093",
                activeMoietyId: 910,
                ingredientSubstanceId: 920,
                pharmacologicClassId: null,
                pharmClassCode: null,
                pharmClassName: null,
                parameterName: "Somnolence",
                rr: 3.5,
                numberNeeded: 12);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(
                connection,
                DtoLabelAccessTestHelper.TestDocumentGuid3,
                riskId: 9202,
                adverseEventId: 9202,
                standardizedId: 9202,
                productName: "RUFINAMIDE",
                substanceName: "Rufinamide",
                unii: "PB6D638093",
                activeMoietyId: 910,
                ingredientSubstanceId: 920,
                pharmacologicClassId: null,
                pharmClassCode: null,
                pharmClassName: null,
                parameterName: "Dizziness",
                rr: 2.0,
                numberNeeded: 20);

            var triage = await DtoLabelAccess.GetAeTriageViewAsync(context, DtoLabelAccessTestHelper.TestDocumentGuid3, PkSecret, logger);
            var forest = await DtoLabelAccess.GetAeForestPlotAsync(context, DtoLabelAccessTestHelper.TestDocumentGuid3, PkSecret, logger);
            var quadrant = await DtoLabelAccess.GetAeQuadrantViewAsync(context, DtoLabelAccessTestHelper.TestDocumentGuid3, PkSecret, logger);

            Assert.IsNotNull(triage);
            Assert.AreEqual("RUFINAMIDE", triage.Product!.ProductName);
            Assert.AreEqual("Rufinamide", triage.Product.SubstanceName);
            Assert.IsNull(triage.Product.EncryptedPharmacologicClassID);
            Assert.AreEqual(2, triage.Product.RowCount);
            Assert.IsTrue(triage.Tiers.SelectMany(tier => tier.Signals).Any(signal => signal.NumberNeeded == 12));
            Assert.IsNotNull(forest);
            Assert.AreEqual(2, forest.Signals.Count);
            Assert.IsNotNull(quadrant);
            Assert.AreEqual(2, quadrant.Points.Count);

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
        /// <seealso cref="DtoLabelAccess.GetAeInterchangeAsync(ApplicationDbContext, Guid, Guid, string, ILogger, bool, bool)"/>
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
            var sharedComparison = await DtoLabelAccess.GetAeInterchangeAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                PkSecret,
                logger,
                differencesOnly: false,
                sharedSignalsOnly: true);
            var missing = await DtoLabelAccess.GetAeInterchangeAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                Guid.Parse("99999999-9999-9999-9999-999999999999"),
                PkSecret,
                logger);

            Assert.IsNotNull(comparison);
            Assert.IsNotNull(sharedComparison);
            Assert.AreEqual(1, comparison.BWorseCount + comparison.AWorseCount);
            Assert.AreEqual(1, comparison.OnlyBCount);
            Assert.AreEqual(0, sharedComparison.OnlyBCount);
            Assert.IsTrue(sharedComparison.Rows.All(row => row.SignalA != null && row.SignalB != null));
            Assert.IsNull(missing);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that interchange signal loading applies optional comparator scoping to both products.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeInterchangeAsync(ApplicationDbContext, Guid, Guid, string, ILogger, bool, bool, AeComparatorMix?)"/>
        [TestMethod]
        public async Task GetAeInterchangeAsync_WithComparatorScope_FiltersBothProductSignals()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, "IBUPROFEN");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid, riskId: 10, adverseEventId: 10, parameterName: "Headache", rr: 4.0, isPlaceboControlled: true);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, riskId: 11, adverseEventId: 11, parameterName: "Headache", rr: 1.5, isPlaceboControlled: true);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid, riskId: 20, adverseEventId: 20, parameterName: "Dizziness", rr: 5.0, isPlaceboControlled: false);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, riskId: 21, adverseEventId: 21, parameterName: "Dizziness", rr: 2.0, isPlaceboControlled: false);
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, riskId: 22, adverseEventId: 22, parameterName: "Cough", rr: 2.4, isPlaceboControlled: false);

            var placeboComparison = await DtoLabelAccess.GetAeInterchangeAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                PkSecret,
                logger,
                comparator: AeComparatorMix.Placebo);
            var activeComparison = await DtoLabelAccess.GetAeInterchangeAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                PkSecret,
                logger,
                comparator: AeComparatorMix.Active);
            var bothComparison = await DtoLabelAccess.GetAeInterchangeAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                PkSecret,
                logger,
                comparator: AeComparatorMix.Both);
            var unfilteredComparison = await DtoLabelAccess.GetAeInterchangeAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                PkSecret,
                logger);

            Assert.IsNotNull(placeboComparison);
            Assert.IsNotNull(activeComparison);
            Assert.IsNotNull(bothComparison);
            Assert.IsNotNull(unfilteredComparison);
            Assert.AreEqual(1, placeboComparison.Rows.Count);
            Assert.AreEqual("Headache", placeboComparison.Rows.Single().ParameterName);
            Assert.IsTrue(placeboComparison.Rows.SelectMany(row => new[] { row.SignalA, row.SignalB }).Where(signal => signal != null).All(signal => signal!.IsPlaceboControlled));
            Assert.AreEqual(2, activeComparison.Rows.Count);
            Assert.IsFalse(activeComparison.Rows.SelectMany(row => new[] { row.SignalA, row.SignalB }).Where(signal => signal != null).Any(signal => signal!.IsPlaceboControlled));
            Assert.AreEqual(3, bothComparison.Rows.Count);
            Assert.AreEqual(bothComparison.Rows.Count, unfilteredComparison.Rows.Count);

            #endregion
        }

        #endregion signal and view tests

    }
}
