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
    /// Contains AE dashboard cache-policy, token, product-catalog, and favorite-isolation tests.
    /// </summary>
    /// <remarks>
    /// This partial preserves the original AE dashboard test identities and setup.
    /// </remarks>
    /// <seealso cref="AeDashboardDataAccessTests"/>
    public partial class AeDashboardDataAccessTests
    {
        #region policy tests

        /**************************************************************/
        /// <summary>
        /// Verifies AE dashboard cache keys keep the legacy DtoLabelAccess prefix after service extraction.
        /// </summary>
        /// <seealso cref="AeDashboardCachePolicy.GenerateKey(string, string?, int?, int?)"/>
        [TestMethod]
        public void AeDashboardCachePolicy_GenerateKey_PreservesLegacyDtoLabelAccessPrefix()
        {
            #region implementation

            var cacheKey = new AeDashboardCachePolicy(new PerformanceAppCache()).GenerateKey(
                nameof(DtoLabelAccess.GetAeProductDetailDataAsync),
                "document guid",
                1,
                25);

            Assert.AreEqual(
                "RHRvTGFiZWxBY2Nlc3MuR2V0QWVQcm9kdWN0RGV0YWlsRGF0YUFzeW5jX2RvY3VtZW50X2d1aWRfMV8yNQ==",
                cacheKey);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the AE dashboard ID mapper decrypts a captured pre-refactor fast encrypted identifier.
        /// </summary>
        /// <seealso cref="AeDashboardEncryptedIdMapper.DecryptNullableInt(string?, string, Microsoft.Extensions.Logging.ILogger, string)"/>
        [TestMethod]
        public void AeDashboardEncryptedIdMapper_CapturedFastToken_DecryptsToOriginalId()
        {
            #region implementation

            const string capturedPreRefactorId = "F-AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyBLjjQW1p8HoYG7I6eDRbBb";
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var value = new AeDashboardEncryptedIdMapper().DecryptNullableInt(
                capturedPreRefactorId,
                PkSecret,
                logger,
                nameof(AeDrugSummaryDto.EncryptedPharmacologicClassID));

            Assert.AreEqual(30, value);

            #endregion
        }

        #endregion policy tests

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

    }
}
