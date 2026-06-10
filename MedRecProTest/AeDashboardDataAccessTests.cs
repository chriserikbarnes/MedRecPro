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
        /// Verifies that the materialized catalog drives provider-side product picker search, paging, and counts.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeProductCatalogAsync(ApplicationDbContext, string, Microsoft.Extensions.Logging.ILogger, string?, long?, int?, int?)"/>
        [TestMethod]
        public async Task GetAeProductCatalogAsync_MaterializedCatalog_SearchesPagesCountsAndMarksFavorites()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();
            var userId = 7002L;
            await seedUserAsync(context, userId);

            DtoLabelAccessTestHelper.SeedAeDashboardProductCatalogTable(
                connection,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                "CATALOG ASPIRIN",
                significantElevatedCount: 4);
            DtoLabelAccessTestHelper.SeedAeDashboardProductCatalogTable(
                connection,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                "CATALOG IBUPROFEN",
                substanceName: "Ibuprofen",
                significantElevatedCount: 12);
            DtoLabelAccessTestHelper.SeedAeDashboardProductCatalogTable(
                connection,
                DtoLabelAccessTestHelper.TestDocumentGuid3,
                "ADVAIR HFA",
                substanceName: "salmeterol xinafoate",
                unii: "2N7Z8O3E8T+O2GMZ0LF5W",
                pharmClassCode: "EPC-SAL",
                pharmClassName: "beta2-Adrenergic Agonist [EPC]",
                significantElevatedCount: 2,
                monoComboMix: "combo",
                activeIngredients: new[]
                {
                    new AeActiveIngredientDto
                    {
                        SubstanceName = "salmeterol xinafoate",
                        UNII = "2N7Z8O3E8T",
                        PharmClassCode = "EPC-SAL",
                        PharmClassName = "beta2-Adrenergic Agonist [EPC]"
                    },
                    new AeActiveIngredientDto
                    {
                        SubstanceName = "fluticasone propionate",
                        UNII = "O2GMZ0LF5W",
                        PharmClassCode = "EPC-FLU",
                        PharmClassName = "Corticosteroid [EPC]"
                    }
                });
            context.AspNetUserFavorites.Add(new AspNetUserFavorite
            {
                UserId = userId,
                DocumentGUID = DtoLabelAccessTestHelper.TestDocumentGuid2,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var firstPage = await DtoLabelAccess.GetAeProductCatalogAsync(
                context,
                PkSecret,
                logger,
                userId: userId,
                page: 1,
                size: 2);
            var ingredientSearch = await DtoLabelAccess.GetAeProductCatalogAsync(
                context,
                PkSecret,
                logger,
                productSearch: "fluticasone",
                page: 1,
                size: 10);
            var count = await DtoLabelAccess.GetAeProductCountAsync(context, logger);

            Assert.AreEqual(2, firstPage.Count);
            Assert.AreEqual("CATALOG IBUPROFEN", firstPage[0].ProductName);
            Assert.IsTrue(firstPage[0].IsFavorite);
            Assert.AreEqual(1, ingredientSearch.Count);
            Assert.AreEqual("ADVAIR HFA", ingredientSearch.Single().ProductName);
            Assert.AreEqual(2, ingredientSearch.Single().ActiveIngredients!.Count);
            Assert.AreEqual(3, count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that null pharmacologic-class risk rows still produce product catalog summaries.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeDrugSummariesAsync(ApplicationDbContext, string, ILogger, string?, long?, int?, int?)"/>
        [TestMethod]
        public async Task GetAeDrugSummariesAsync_NullPharmClassRiskRows_ReturnsFallbackProduct()
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
                riskId: 9101,
                adverseEventId: 9101,
                standardizedId: 9101,
                productName: "RUFINAMIDE",
                substanceName: "Rufinamide",
                unii: "PB6D638093",
                activeMoietyId: 910,
                ingredientSubstanceId: 920,
                pharmacologicClassId: null,
                pharmClassCode: null,
                pharmClassName: null,
                parameterName: "Somnolence",
                numberNeeded: 12);

            var products = await DtoLabelAccess.GetAeDrugSummariesAsync(
                context,
                PkSecret,
                logger,
                productSearch: "rufinamide",
                page: 1,
                size: 10);

            Assert.AreEqual(1, products.Count);
            var product = products.Single();
            Assert.AreEqual("RUFINAMIDE", product.ProductName);
            Assert.AreEqual("Rufinamide", product.SubstanceName);
            Assert.AreEqual("PB6D638093", product.UNII);
            Assert.AreEqual(910, product.ActiveMoietyID);
            Assert.AreEqual(920, product.IngredientSubstanceID);
            Assert.IsNull(product.EncryptedPharmacologicClassID);
            Assert.IsNull(product.PharmacologicClassID);
            Assert.AreEqual(1, product.RowCount);
            Assert.AreEqual(1, product.SignificantElevatedCount);
            Assert.IsTrue(product.Score.HasValue);

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

        /**************************************************************/
        /// <summary>
        /// Verifies that marking favorites for an authenticated request never mutates
        /// the shared cached catalog (clone-proof).
        /// </summary>
        /// <remarks>
        /// The catalog cache hands back live DTO instances, so a later anonymous
        /// request must still see IsFavorite=false even after an authenticated request
        /// marked the same product. Catches a regression where favorite marking leaks
        /// across users through the cache.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetAeDrugSummariesAsync(ApplicationDbContext, string, ILogger, string?, long?, int?, int?)"/>
        [TestMethod]
        public async Task GetAeDrugSummariesAsync_AuthenticatedFavoriteThenAnonymous_DoesNotPolluteCachedCatalog()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();
            var userId = 7003L;
            await seedUserAsync(context, userId);

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");
            context.AspNetUserFavorites.Add(new AspNetUserFavorite
            {
                UserId = userId,
                DocumentGUID = DtoLabelAccessTestHelper.TestDocumentGuid,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // Authenticated request marks the product as a favorite (on a clone).
            var authenticated = await DtoLabelAccess.GetAeDrugSummariesAsync(context, PkSecret, logger, userId: userId);

            // A later anonymous request reads the same cached base and must be clean.
            var anonymous = await DtoLabelAccess.GetAeDrugSummariesAsync(context, PkSecret, logger);

            Assert.IsTrue(authenticated.Single().IsFavorite);
            Assert.IsTrue(anonymous.All(product => !product.IsFavorite));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a combination product collapses to one row listing every
        /// active ingredient with its preferred ("[EPC]") class in ingredient order.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeDrugSummariesAsync(ApplicationDbContext, string, ILogger, string?, long?, int?, int?)"/>
        /// <seealso cref="AeDashboardDerivation.BuildActiveIngredients(System.Collections.Generic.IEnumerable{AeDrugSummaryDto})"/>
        [TestMethod]
        public async Task GetAeDrugSummariesAsync_CombinationProduct_CollapsesToOneRowWithEpcIngredients()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // ADVAIR-style combo: two ingredients, each with an [EPC] and a [MoA]
            // stratum. salmeterol uses ingredient id 100, fluticasone uses 200.
            var advair = DtoLabelAccessTestHelper.TestDocumentGuid;
            const string comboUnii = "6EW8Q962A5+O2GMZ0LF5W";
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, advair, "ADVAIR HFA", substanceName: "salmeterol xinafoate", unii: comboUnii, pharmClassCode: "EPC-SAL", pharmClassName: "beta2-Adrenergic Agonist [EPC]", activeMoietyId: 100, ingredientSubstanceId: 100, pharmacologicClassId: 1, monoComboMix: "combo");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, advair, "ADVAIR HFA", substanceName: "salmeterol xinafoate", unii: comboUnii, pharmClassCode: "MOA-SAL", pharmClassName: "Adrenergic beta2-Agonists [MoA]", activeMoietyId: 100, ingredientSubstanceId: 100, pharmacologicClassId: 2, monoComboMix: "combo");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, advair, "ADVAIR HFA", substanceName: "fluticasone propionate", unii: comboUnii, pharmClassCode: "EPC-FLU", pharmClassName: "Corticosteroid [EPC]", activeMoietyId: 200, ingredientSubstanceId: 200, pharmacologicClassId: 3, monoComboMix: "combo");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, advair, "ADVAIR HFA", substanceName: "fluticasone propionate", unii: comboUnii, pharmClassCode: "MOA-FLU", pharmClassName: "Corticosteroid Hormone Receptor Agonists [MoA]", activeMoietyId: 200, ingredientSubstanceId: 200, pharmacologicClassId: 4, monoComboMix: "combo");

            var products = await DtoLabelAccess.GetAeDrugSummariesAsync(context, PkSecret, logger);

            // The four strata collapse to a single product row.
            Assert.AreEqual(1, products.Count);
            var product = products.Single();
            Assert.IsNotNull(product.ActiveIngredients);
            Assert.AreEqual(2, product.ActiveIngredients!.Count);

            // Ingredients are ordered by ingredient id and standardized on the EPC class.
            Assert.AreEqual("salmeterol xinafoate", product.ActiveIngredients[0].SubstanceName);
            Assert.AreEqual("beta2-Adrenergic Agonist [EPC]", product.ActiveIngredients[0].PharmClassName);
            Assert.AreEqual("fluticasone propionate", product.ActiveIngredients[1].SubstanceName);
            Assert.AreEqual("Corticosteroid [EPC]", product.ActiveIngredients[1].PharmClassName);

            // The flat fields mirror the first ingredient's EPC values.
            Assert.AreEqual("salmeterol xinafoate", product.SubstanceName);
            Assert.AreEqual("beta2-Adrenergic Agonist [EPC]", product.PharmClassName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetAeProductCatalogAsync returns slim items carrying the active
        /// ingredient list and derived score for the picker.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeProductCatalogAsync(ApplicationDbContext, string, ILogger, string?, long?, int?, int?)"/>
        [TestMethod]
        public async Task GetAeProductCatalogAsync_ReturnsSlimItemsWithIngredients()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var advair = DtoLabelAccessTestHelper.TestDocumentGuid;
            const string comboUnii = "6EW8Q962A5+O2GMZ0LF5W";
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, advair, "ADVAIR HFA", substanceName: "salmeterol xinafoate", unii: comboUnii, pharmClassCode: "EPC-SAL", pharmClassName: "beta2-Adrenergic Agonist [EPC]", activeMoietyId: 100, ingredientSubstanceId: 100, pharmacologicClassId: 1, monoComboMix: "combo");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, advair, "ADVAIR HFA", substanceName: "fluticasone propionate", unii: comboUnii, pharmClassCode: "EPC-FLU", pharmClassName: "Corticosteroid [EPC]", activeMoietyId: 200, ingredientSubstanceId: 200, pharmacologicClassId: 3, monoComboMix: "combo");

            var catalog = await DtoLabelAccess.GetAeProductCatalogAsync(context, PkSecret, logger);

            Assert.AreEqual(1, catalog.Count);
            var item = catalog.Single();
            Assert.AreEqual("ADVAIR HFA", item.ProductName);
            Assert.IsTrue(item.Score.HasValue);
            Assert.IsNotNull(item.ActiveIngredients);
            Assert.AreEqual(2, item.ActiveIngredients!.Count);
            Assert.AreEqual("beta2-Adrenergic Agonist [EPC]", item.PharmClassName);

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

        #endregion signal and view tests

        #region correlation map tests

        /**************************************************************/
        /// <summary>
        /// Verifies that two SOCs with identical per-drug LogRR ranks correlate at +1.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_PerfectlyCorrelatedSocs_ReturnsCoefficientOne()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // Three placebo drugs; identical per-SOC RR so the two SOCs share one rank order.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socB, rr: 8.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);

            Assert.IsNotNull(map);
            Assert.AreEqual(2, map.Soc.Count);
            Assert.AreEqual(3, map.DrugCount);
            var offDiagonal = map.Cells.Single(cell => !cell.IsDiagonal);
            Assert.AreEqual(3, offDiagonal.PairCount);
            Assert.IsFalse(offDiagonal.InsufficientN);
            Assert.IsNotNull(offDiagonal.Coefficient);
            Assert.AreEqual(1.0, offDiagonal.Coefficient!.Value, 1e-9);
            Assert.IsTrue(map.Cells.Where(cell => cell.IsDiagonal).All(cell => cell.Coefficient == 1.0));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that two SOCs with reversed per-drug LogRR ranks correlate at -1.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_AntiCorrelatedSocs_ReturnsCoefficientNegativeOne()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // SOC A ascends 2,4,8 while SOC B descends 8,4,2 for the same drugs.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socB, rr: 2.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);

            Assert.IsNotNull(map);
            var offDiagonal = map.Cells.Single(cell => !cell.IsDiagonal);
            Assert.AreEqual(3, offDiagonal.PairCount);
            Assert.IsNotNull(offDiagonal.Coefficient);
            Assert.AreEqual(-1.0, offDiagonal.Coefficient!.Value, 1e-9);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a cell below the drugs-per-cell floor returns a null coefficient and the real n.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_BelowMinDrugsPerCell_ReturnsNullInsufficientN()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // Only two drugs overlap the pair, below the default floor of four.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger);

            Assert.IsNotNull(map);
            var offDiagonal = map.Cells.Single(cell => !cell.IsDiagonal);
            Assert.AreEqual(2, offDiagonal.PairCount);
            Assert.IsTrue(offDiagonal.InsufficientN);
            Assert.IsNull(offDiagonal.Coefficient);
            Assert.IsTrue(map.Warnings.Any(warning => warning.Contains("minimum")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a SOC with constant LogRR across drugs yields a null (not NaN) coefficient.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_ZeroVarianceSoc_ReturnsNullCoefficient()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // SOC A varies; SOC B is constant (rr 3.0) for all four drugs.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 3.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 3.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 6.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socB, rr: 3.0);
            seedCorrelationRow(connection, drugDoc(4), riskId: 7, activeMoietyId: 104, pharmClassCode: code, parameterCategory: socA, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(4), riskId: 8, activeMoietyId: 104, pharmClassCode: code, parameterCategory: socB, rr: 3.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);

            Assert.IsNotNull(map);
            var offDiagonal = map.Cells.Single(cell => !cell.IsDiagonal);
            Assert.AreEqual(4, offDiagonal.PairCount);
            Assert.IsFalse(offDiagonal.InsufficientN);
            Assert.IsNull(offDiagonal.Coefficient);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the comparator defaults to placebo and excludes active-comparator rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_ComparatorDefaultsToPlacebo_ExcludesActiveRows()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string placeboSocA = "Skin and Subcutaneous Tissue Disorders";
            const string placeboSocB = "Gastrointestinal Disorders";
            const string activeSocC = "Nervous System Disorders";
            const string activeSocD = "Psychiatric Disorders";

            // Placebo drugs populate SOC A/B; active-only drugs populate SOC C/D.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: placeboSocA, rr: 2.0, isPlaceboControlled: true);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: placeboSocB, rr: 2.0, isPlaceboControlled: true);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: placeboSocA, rr: 4.0, isPlaceboControlled: true);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: placeboSocB, rr: 4.0, isPlaceboControlled: true);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: activeSocC, rr: 3.0, isPlaceboControlled: false);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: activeSocD, rr: 3.0, isPlaceboControlled: false);
            seedCorrelationRow(connection, drugDoc(4), riskId: 7, activeMoietyId: 104, pharmClassCode: code, parameterCategory: activeSocC, rr: 5.0, isPlaceboControlled: false);
            seedCorrelationRow(connection, drugDoc(4), riskId: 8, activeMoietyId: 104, pharmClassCode: code, parameterCategory: activeSocD, rr: 5.0, isPlaceboControlled: false);

            var placeboMap = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);
            var activeMap = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, comparator: AeComparatorMix.Active, minDrugsPerCell: 3);

            Assert.IsNotNull(placeboMap);
            Assert.IsTrue(placeboMap.Soc.Contains(placeboSocA));
            Assert.IsFalse(placeboMap.Soc.Contains(activeSocC));
            Assert.IsNotNull(activeMap);
            Assert.IsTrue(activeMap.Soc.Contains(activeSocC));
            Assert.IsFalse(activeMap.Soc.Contains(placeboSocA));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that excluding non-significant rows drops them before correlating.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_ExcludeNonSignificant_DropsNotSignificantInputRows()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";
            const string socC = "Nervous System Disorders";

            // SOC A/B are elevated; SOC C carries only not-significant rows.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0, significance: "elevated");
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0, significance: "elevated");
            seedCorrelationRow(connection, drugDoc(1), riskId: 3, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socC, rr: 1.1, significance: "not significant");
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0, significance: "elevated");
            seedCorrelationRow(connection, drugDoc(2), riskId: 5, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0, significance: "elevated");
            seedCorrelationRow(connection, drugDoc(2), riskId: 6, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socC, rr: 1.1, significance: "not significant");

            var included = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);
            var excluded = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, includeNonSignificant: false, minDrugsPerCell: 3);

            Assert.IsNotNull(included);
            Assert.IsTrue(included.Soc.Contains(socC));
            Assert.IsNotNull(excluded);
            Assert.IsFalse(excluded.Soc.Contains(socC));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that fragile rows are excluded by default and surfaced when fragile is included.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_ExcludeFragile_DropsFragileInputRows()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // SOC A is sound; every SOC B row is fragile via a zero-cell correction flag.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0, calculationFlags: "ZERO_CELL_CORRECTED");
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0, calculationFlags: "ZERO_CELL_CORRECTED");
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socB, rr: 8.0, calculationFlags: "ZERO_CELL_CORRECTED");

            var strict = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);
            var lenient = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, excludeFragile: false, minDrugsPerCell: 3);

            Assert.IsNotNull(strict);
            Assert.IsFalse(strict.Soc.Contains(socB));
            Assert.IsNotNull(lenient);
            Assert.IsTrue(lenient.Soc.Contains(socB));
            var offDiagonal = lenient.Cells.Single(cell => !cell.IsDiagonal);
            Assert.IsTrue(offDiagonal.IsFragile);
            Assert.IsTrue(lenient.Warnings.Any(warning => warning.Contains("Fragile")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that moiety and class context survive collapse and that DrugCount counts moieties.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_RetainsMoietyAndClassFromEntity()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "N0000175076";
            const string className = "Protein Kinase Inhibitors [MoA]";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // Two moieties under class 203; one has a duplicate stratum that must collapse to one row.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 452, pharmacologicClassId: 203, pharmClassCode: code, pharmClassName: className, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 452, pharmacologicClassId: 203, pharmClassCode: code, pharmClassName: className, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 3, activeMoietyId: 452, pharmacologicClassId: 203, pharmClassCode: code, pharmClassName: className, parameterCategory: socB, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 853, pharmacologicClassId: 203, pharmClassCode: code, pharmClassName: className, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 5, activeMoietyId: 853, pharmacologicClassId: 203, pharmClassCode: code, pharmClassName: className, parameterCategory: socB, rr: 4.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 3);

            Assert.IsNotNull(map);
            Assert.AreEqual(className, map.PharmClassName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(map.EncryptedPharmacologicClassID));
            Assert.AreEqual(203, map.PharmacologicClassID);
            Assert.AreEqual(2, map.DrugCount);
            var socASummary = map.SocSummaries.Single(summary => summary.Soc == socA);
            Assert.AreEqual(2, socASummary.DrugCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that an unknown pharmacologic class returns null (controller 404).
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_UnknownClass_ReturnsNull()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: "TESTKINASE", parameterCategory: "Skin and Subcutaneous Tissue Disorders", rr: 2.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, "NO_SUCH_CLASS", PkSecret, logger);

            Assert.IsNull(map);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the method parameter is honored: Spearman is 1 and Pearson is below 1 on monotonic-nonlinear data.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_SpearmanVsPearson_MethodParamHonored()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // SOC A LogRR = 1,2,3,4; SOC B LogRR = 1,4,9,16 (monotonic but non-linear).
            double[] logA = { 1, 2, 3, 4 };
            double[] logB = { 1, 4, 9, 16 };
            for (var i = 0; i < 4; i++)
            {
                var moiety = 101 + i;
                seedCorrelationRow(connection, drugDoc(i + 1), riskId: i * 2 + 1, activeMoietyId: moiety, pharmClassCode: code, parameterCategory: socA, rr: Math.Exp(logA[i]));
                seedCorrelationRow(connection, drugDoc(i + 1), riskId: i * 2 + 2, activeMoietyId: moiety, pharmClassCode: code, parameterCategory: socB, rr: Math.Exp(logB[i]));
            }

            var spearman = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, method: AeCorrelationMethod.Spearman, minDrugsPerCell: 3);
            var pearson = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, method: AeCorrelationMethod.Pearson, minDrugsPerCell: 3);

            Assert.IsNotNull(spearman);
            Assert.IsNotNull(pearson);
            Assert.AreEqual(AeCorrelationMethod.Spearman, spearman.AppliedFilters.Method);
            Assert.AreEqual(AeCorrelationMethod.Pearson, pearson.AppliedFilters.Method);
            var spearmanCell = spearman.Cells.Single(cell => !cell.IsDiagonal);
            var pearsonCell = pearson.Cells.Single(cell => !cell.IsDiagonal);
            Assert.AreEqual(1.0, spearmanCell.Coefficient!.Value, 1e-9);
            Assert.IsTrue(pearsonCell.Coefficient!.Value < 0.999);
            Assert.IsTrue(pearsonCell.Coefficient!.Value > 0.95);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the class picker scopes to AE classes and flags which are correlatable.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationClassesAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationClassesAsync_ScopesToAeClasses_AndFlagsCorrelatable()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // A correlatable class: two drugs across two SOCs.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmacologicClassId: 500, pharmClassCode: "CORRCLASS", parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 2, activeMoietyId: 102, pharmacologicClassId: 500, pharmClassCode: "CORRCLASS", parameterCategory: socB, rr: 4.0);
            // A single-drug, single-SOC class.
            seedCorrelationRow(connection, drugDoc(3), riskId: 3, activeMoietyId: 201, pharmacologicClassId: 600, pharmClassCode: "SINGLECLASS", parameterCategory: socA, rr: 3.0);

            var classes = await DtoLabelAccess.GetAeCorrelationClassesAsync(context, PkSecret, logger);

            Assert.AreEqual(2, classes.Count);
            Assert.AreEqual("CORRCLASS", classes[0].PharmClassCode);
            var correlatable = classes.Single(item => item.PharmClassCode == "CORRCLASS");
            var single = classes.Single(item => item.PharmClassCode == "SINGLECLASS");
            Assert.IsTrue(correlatable.IsCorrelatable);
            Assert.AreEqual(2, correlatable.DrugCount);
            Assert.AreEqual(2, correlatable.SocCount);
            Assert.IsFalse(single.IsCorrelatable);
            Assert.IsFalse(string.IsNullOrWhiteSpace(correlatable.EncryptedPharmacologicClassID));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the heatmap builds a SOC × drug grid with aggregated values and dedups to moiety.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationHeatmapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationHeatmapAsync_BuildsSocByDrugGrid()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // Drug 101 has two terms in SOC A so its cell aggregates two rows.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, parameterName: "Rash", rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, parameterName: "Pruritus", rr: 3.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 3, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, parameterName: "Nausea", rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, parameterName: "Rash", rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 5, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, parameterName: "Nausea", rr: 4.0);

            var heatmap = await DtoLabelAccess.GetAeCorrelationHeatmapAsync(context, code, PkSecret, logger);

            Assert.IsNotNull(heatmap);
            Assert.AreEqual(2, heatmap.Soc.Count);
            Assert.AreEqual(2, heatmap.Drugs.Count);
            Assert.IsTrue(heatmap.Cells.Count > 0);
            Assert.IsTrue(heatmap.Cells.All(cell => cell.LogRr.HasValue && cell.Rr.HasValue && cell.Precision.HasValue));
            Assert.IsTrue(heatmap.Cells.Any(cell => cell.TermCount == 2));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the cell drill-down returns the per-drug paired observations with provenance.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationCellDetailAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationCellDetailAsync_ReturnsPerDrugPairs()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socB, rr: 8.0);

            // minDrugsPerCell:3 keeps the three-drug cell at or above the floor so the map-safe
            // coefficient is populated; the raw coefficient mirrors it.
            var detail = await DtoLabelAccess.GetAeCorrelationCellDetailAsync(context, code, socA, socB, PkSecret, logger, minDrugsPerCell: 3);

            Assert.IsNotNull(detail);
            Assert.AreEqual(3, detail.PairCount);
            Assert.AreEqual(3, detail.DrugPairs.Count);
            Assert.IsTrue(detail.DrugPairs.All(pair => pair.LogRrX.HasValue && pair.LogRrY.HasValue));
            Assert.IsTrue(detail.DrugPairs.All(pair => !string.IsNullOrWhiteSpace(pair.EncryptedActiveMoietyID)));
            Assert.IsFalse(detail.InsufficientN);
            Assert.IsNotNull(detail.Coefficient);
            Assert.AreEqual(1.0, detail.Coefficient!.Value, 1e-9);
            Assert.IsNotNull(detail.RawCoefficient);
            Assert.AreEqual(1.0, detail.RawCoefficient!.Value, 1e-9);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the serious-SOC and exclude-combos axis filters restrict the data.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_SeriousSocOnly_And_ExcludeCombos_FilterAxis()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string seriousSoc = "Cardiac Disorders";
            const string benignSoc = "General Disorders and Administration Site Conditions";

            // Two mono drugs and one combo drug, each in a serious and a non-serious SOC.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: seriousSoc, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: benignSoc, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: seriousSoc, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: benignSoc, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: seriousSoc, rr: 8.0, isCombo: true);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: benignSoc, rr: 8.0, isCombo: true);

            var seriousOnly = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, seriousSocOnly: true, minDrugsPerCell: 3);
            var noCombos = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, excludeCombos: true, minDrugsPerCell: 3);

            Assert.IsNotNull(seriousOnly);
            Assert.IsTrue(seriousOnly.Soc.Contains(seriousSoc));
            Assert.IsFalse(seriousOnly.Soc.Contains(benignSoc));
            Assert.IsNotNull(noCombos);
            Assert.AreEqual(2, noCombos.DrugCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the pure correlation math against hand calculations for known vectors.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.ComputeCorrelation"/>
        /// <seealso cref="AeDashboardDerivation.StudentTTwoSidedP"/>
        [TestMethod]
        public void ComputeCorrelation_KnownVectors_MatchHandCalc()
        {
            #region implementation

            // Perfect positive linear relationship: r = 1 for both methods.
            var positivePearson = AeDashboardDerivation.ComputeCorrelation(new[] { 1.0, 2.0, 3.0 }, new[] { 2.0, 4.0, 6.0 }, AeCorrelationMethod.Pearson);
            var positiveSpearman = AeDashboardDerivation.ComputeCorrelation(new[] { 1.0, 2.0, 3.0 }, new[] { 2.0, 4.0, 6.0 }, AeCorrelationMethod.Spearman);
            Assert.AreEqual(3, positivePearson.PairCount);
            Assert.AreEqual(1.0, positivePearson.Coefficient!.Value, 1e-9);
            Assert.AreEqual(1.0, positiveSpearman.Coefficient!.Value, 1e-9);

            // Perfect negative relationship: r = -1.
            var negative = AeDashboardDerivation.ComputeCorrelation(new[] { 1.0, 2.0, 3.0 }, new[] { 6.0, 4.0, 2.0 }, AeCorrelationMethod.Pearson);
            Assert.AreEqual(-1.0, negative.Coefficient!.Value, 1e-9);

            // Zero variance: undefined coefficient, never NaN.
            var constant = AeDashboardDerivation.ComputeCorrelation(new[] { 1.0, 2.0, 3.0 }, new[] { 2.0, 2.0, 2.0 }, AeCorrelationMethod.Pearson);
            Assert.IsFalse(constant.Coefficient.HasValue);
            Assert.IsFalse(constant.PValue.HasValue);

            // n = 2: coefficient is defined but the p-value is not.
            var twoPoints = AeDashboardDerivation.ComputeCorrelation(new[] { 1.0, 2.0 }, new[] { 2.0, 5.0 }, AeCorrelationMethod.Pearson);
            Assert.AreEqual(2, twoPoints.PairCount);
            Assert.IsTrue(twoPoints.Coefficient.HasValue);
            Assert.IsFalse(twoPoints.PValue.HasValue);

            // Student's t two-sided p-value: t = 0 -> p = 1, the 0.05 critical value at df = 10 -> p ~ 0.05.
            Assert.AreEqual(1.0, AeDashboardDerivation.StudentTTwoSidedP(0.0, 5), 1e-9);
            Assert.AreEqual(0.05, AeDashboardDerivation.StudentTTwoSidedP(2.228, 10), 0.005);
            Assert.IsTrue(AeDashboardDerivation.StudentTTwoSidedP(10.0, 5) < 0.001);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that one drug's SOC casing variants fold into a single aggregate
        /// instead of splitting and silently overwriting in the case-insensitive pivot.
        /// </summary>
        /// <seealso cref="AeDashboardDerivation.AggregatePerDrugSoc"/>
        [TestMethod]
        public void AggregatePerDrugSoc_MixedCaseSoc_FoldsToOneAggregate()
        {
            #region implementation

            var observations = new[]
            {
                new AeCorrelationObservation { DrugKey = "moiety:1", Soc = "Cardiac Disorders", LogRr = 1.0 },
                new AeCorrelationObservation { DrugKey = "moiety:1", Soc = "Cardiac disorders", LogRr = 3.0 }
            };

            var aggregates = AeDashboardDerivation.AggregatePerDrugSoc(observations, AeCorrelationAggregation.MedianLogRr);

            var aggregate = aggregates.Single();
            Assert.AreEqual(2, aggregate.Value.Count);
            Assert.AreEqual(2.0, aggregate.Value.Value, 1e-9);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the cell drill-down suppresses the map-safe coefficient below the floor while
        /// still exposing the raw diagnostic coefficient and all contributing pairs.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationCellDetailAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationCellDetailAsync_BelowMinDrugsPerCell_ReturnsNullMapCoefficientAndRawCoefficient()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // Three drugs overlap the pair, below the default floor of four.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 5, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 8.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 6, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socB, rr: 8.0);

            var detail = await DtoLabelAccess.GetAeCorrelationCellDetailAsync(context, code, socA, socB, PkSecret, logger);

            Assert.IsNotNull(detail);
            Assert.AreEqual(3, detail.PairCount);
            Assert.AreEqual(4, detail.MinDrugsPerCell);
            Assert.IsTrue(detail.InsufficientN);
            Assert.IsNull(detail.Coefficient);
            Assert.IsNull(detail.PValue);
            Assert.IsNotNull(detail.RawCoefficient);
            Assert.AreEqual(1.0, detail.RawCoefficient!.Value, 1e-9);
            // RawPValue is null here not because of the floor but because a perfect ±1
            // coefficient has no defined p-value; the raw pair stays internally consistent.
            Assert.IsNull(detail.RawPValue);
            Assert.AreEqual(3, detail.DrugPairs.Count);
            Assert.IsFalse(detail.IsDiagonal);
            Assert.IsTrue(detail.Warnings.Any(warning => warning.Contains("suppressed")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that requesting the same SOC for both axes returns a non-informative diagonal cell.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationCellDetailAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationCellDetailAsync_Diagonal_ReturnsNonInformativeDiagonal()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";

            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 2, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 3, activeMoietyId: 103, pharmClassCode: code, parameterCategory: socA, rr: 8.0);

            // Same SOC on both axes (case-insensitive) is the diagonal.
            var detail = await DtoLabelAccess.GetAeCorrelationCellDetailAsync(context, code, socA, socA.ToLowerInvariant(), PkSecret, logger);

            Assert.IsNotNull(detail);
            Assert.IsTrue(detail.IsDiagonal);
            Assert.IsNotNull(detail.Coefficient);
            Assert.AreEqual(1.0, detail.Coefficient!.Value, 1e-9);
            Assert.IsNotNull(detail.RawCoefficient);
            Assert.AreEqual(1.0, detail.RawCoefficient!.Value, 1e-9);
            Assert.IsNull(detail.PValue);
            Assert.IsFalse(detail.IsSignificant);
            Assert.IsTrue(detail.DrugPairs.Count > 0);
            Assert.IsTrue(detail.Warnings.Any(warning => warning.Contains("diagonal")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that requesting a SOC with no rows after filtering returns empty pairs, null
        /// coefficients, and an explanatory warning rather than an error.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationCellDetailAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationCellDetailAsync_MissingSoc_ReturnsEmptyPairsAndWarning()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";

            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);

            var detail = await DtoLabelAccess.GetAeCorrelationCellDetailAsync(context, code, socA, "Cardiac Disorders", PkSecret, logger);

            Assert.IsNotNull(detail);
            Assert.AreEqual(0, detail.DrugPairs.Count);
            Assert.AreEqual(0, detail.PairCount);
            Assert.IsNull(detail.Coefficient);
            Assert.IsNull(detail.RawCoefficient);
            Assert.IsNull(detail.PValue);
            Assert.IsTrue(detail.Warnings.Any(warning => warning.Contains("no rows")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the map clamps a below-floor drugs-per-cell request up to the hard floor of three and echoes it.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_MinDrugsPerCellBelowFloor_EchoesThree()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(1), riskId: 2, activeMoietyId: 101, pharmClassCode: code, parameterCategory: socB, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 3, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socA, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 4, activeMoietyId: 102, pharmClassCode: code, parameterCategory: socB, rr: 4.0);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, minDrugsPerCell: 1);

            Assert.IsNotNull(map);
            Assert.AreEqual(3, map.AppliedFilters.MinDrugsPerCell);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the Both comparator includes active and placebo rows and adds a mixed-estimand warning.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationMapAsync_BothComparator_AddsMixedComparatorWarning()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string code = "TESTKINASE";
            const string placeboSoc = "Skin and Subcutaneous Tissue Disorders";
            const string activeSoc = "Nervous System Disorders";

            // Placebo drugs populate one SOC; active-only drugs populate another.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmClassCode: code, parameterCategory: placeboSoc, rr: 2.0, isPlaceboControlled: true);
            seedCorrelationRow(connection, drugDoc(2), riskId: 2, activeMoietyId: 102, pharmClassCode: code, parameterCategory: placeboSoc, rr: 4.0, isPlaceboControlled: true);
            seedCorrelationRow(connection, drugDoc(3), riskId: 3, activeMoietyId: 103, pharmClassCode: code, parameterCategory: activeSoc, rr: 3.0, isPlaceboControlled: false);
            seedCorrelationRow(connection, drugDoc(4), riskId: 4, activeMoietyId: 104, pharmClassCode: code, parameterCategory: activeSoc, rr: 5.0, isPlaceboControlled: false);

            var map = await DtoLabelAccess.GetAeCorrelationMapAsync(context, code, PkSecret, logger, comparator: AeComparatorMix.Both, minDrugsPerCell: 3);

            Assert.IsNotNull(map);
            Assert.AreEqual(AeComparatorMix.Both, map.AppliedFilters.Comparator);
            Assert.IsTrue(map.Soc.Contains(placeboSoc));
            Assert.IsTrue(map.Soc.Contains(activeSoc));
            Assert.IsTrue(map.Warnings.Any(warning => warning.Contains("mix")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the class picker honors the search filter and in-memory paging.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationClassesAsync"/>
        [TestMethod]
        public async Task GetAeCorrelationClassesAsync_SearchAndPaging_ReturnsExpectedSlice()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            const string socA = "Skin and Subcutaneous Tissue Disorders";
            const string socB = "Gastrointestinal Disorders";

            // Two searchable, correlatable kinase classes plus one non-matching class.
            seedCorrelationRow(connection, drugDoc(1), riskId: 1, activeMoietyId: 101, pharmacologicClassId: 701, pharmClassCode: "KINASE-A", pharmClassName: "Alpha Kinase Inhibitors", parameterCategory: socA, rr: 2.0);
            seedCorrelationRow(connection, drugDoc(2), riskId: 2, activeMoietyId: 102, pharmacologicClassId: 701, pharmClassCode: "KINASE-A", pharmClassName: "Alpha Kinase Inhibitors", parameterCategory: socB, rr: 4.0);
            seedCorrelationRow(connection, drugDoc(3), riskId: 3, activeMoietyId: 201, pharmacologicClassId: 702, pharmClassCode: "KINASE-B", pharmClassName: "Beta Kinase Inhibitors", parameterCategory: socA, rr: 3.0);
            seedCorrelationRow(connection, drugDoc(4), riskId: 4, activeMoietyId: 202, pharmacologicClassId: 702, pharmClassCode: "KINASE-B", pharmClassName: "Beta Kinase Inhibitors", parameterCategory: socB, rr: 5.0);
            seedCorrelationRow(connection, drugDoc(5), riskId: 5, activeMoietyId: 301, pharmacologicClassId: 703, pharmClassCode: "OTHER-C", pharmClassName: "Gamma Other Agents", parameterCategory: socA, rr: 6.0);

            var firstPage = await DtoLabelAccess.GetAeCorrelationClassesAsync(context, PkSecret, logger, classSearch: "Kinase", page: 1, size: 1);
            var secondPage = await DtoLabelAccess.GetAeCorrelationClassesAsync(context, PkSecret, logger, classSearch: "Kinase", page: 2, size: 1);

            // Search restricts to the two kinase classes; correlatable-then-name ordering yields Alpha first.
            Assert.AreEqual(1, firstPage.Count);
            Assert.AreEqual("KINASE-A", firstPage[0].PharmClassCode);
            Assert.IsTrue(firstPage[0].IsCorrelatable);
            Assert.AreEqual(1, secondPage.Count);
            Assert.AreEqual("KINASE-B", secondPage[0].PharmClassCode);

            #endregion
        }

        #endregion correlation map tests

        #region helpers

        /**************************************************************/
        /// <summary>
        /// Builds a deterministic, distinct document GUID for a synthetic correlation drug.
        /// </summary>
        private static Guid drugDoc(int n) => Guid.Parse($"a1a1a1a1-0000-0000-0000-{n:D12}");

        /**************************************************************/
        /// <summary>
        /// Seeds one risk-table row for a synthetic correlation drug, keeping the CI tight and non-fragile.
        /// </summary>
        private static void seedCorrelationRow(
            SqliteConnection connection,
            Guid documentGuid,
            int riskId,
            int activeMoietyId,
            string pharmClassCode,
            string parameterCategory,
            double rr,
            int pharmacologicClassId = 500,
            string? parameterName = null,
            bool isPlaceboControlled = true,
            string significance = "elevated",
            string? calculationFlags = null,
            bool isCombo = false,
            string pharmClassName = "Test Pharmacologic Class",
            double? eventsTreatment = 30,
            double? eventsComparator = 10)
        {
            #region implementation

            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(
                connection,
                documentGuid: documentGuid,
                riskId: riskId,
                adverseEventId: riskId,
                standardizedId: riskId,
                activeMoietyId: activeMoietyId,
                ingredientSubstanceId: activeMoietyId + 1000,
                pharmacologicClassId: pharmacologicClassId,
                pharmClassCode: pharmClassCode,
                pharmClassName: pharmClassName,
                substanceName: $"Substance {activeMoietyId}",
                parameterCategory: parameterCategory,
                parameterName: parameterName ?? $"{parameterCategory} term",
                significance: significance,
                isPlaceboControlled: isPlaceboControlled,
                isCombo: isCombo,
                rr: rr,
                rrLowerBound: rr * 0.7,
                rrUpperBound: rr * 1.4,
                calculationFlags: calculationFlags,
                eventsTreatment: eventsTreatment,
                eventsComparator: eventsComparator);

            #endregion
        }

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
