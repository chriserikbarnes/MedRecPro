using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// SQLite-backed tests for AE dashboard favorite persistence.
    /// </summary>
    /// <remarks>
    /// These tests cover the EF mapping and user-scoped favorite add, remove,
    /// idempotency, uniqueness, validity, ordering, and DTO enrichment behavior.
    /// </remarks>
    /// <seealso cref="AspNetUserFavorite"/>
    /// <seealso cref="DtoLabelAccess"/>
    [TestClass]
    public class AeDashboardFavoriteAccessTests
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// Encryption secret used by dashboard favorite tests.
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

        #region mapping tests

        /**************************************************************/
        /// <summary>
        /// Verifies that AspNetUserFavorite has the expected table, key, index, and user relationship mapping.
        /// </summary>
        /// <seealso cref="AspNetUserFavorite"/>
        [TestMethod]
        public void AspNetUserFavorite_ModelMapping_UsesExpectedTableKeyIndexAndRelationship()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);

            var entityType = context.Model.FindEntityType(typeof(AspNetUserFavorite));
            var primaryKey = entityType!.FindPrimaryKey();
            var userIdProperty = entityType.FindProperty(nameof(AspNetUserFavorite.UserId));
            var uniqueIndex = entityType.GetIndexes()
                .SingleOrDefault(index => index.Properties.Select(property => property.Name)
                    .SequenceEqual(new[] { nameof(AspNetUserFavorite.UserId), nameof(AspNetUserFavorite.DocumentGUID) }));
            var relationship = entityType.GetForeignKeys()
                .SingleOrDefault(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(User));

            Assert.AreEqual("AspNetUserFavorite", entityType.GetTableName());
            Assert.AreEqual(nameof(AspNetUserFavorite.AspNetUserFavoriteID), primaryKey!.Properties.Single().Name);
            Assert.AreEqual(typeof(long), userIdProperty!.ClrType);
            Assert.IsNotNull(uniqueIndex);
            Assert.IsTrue(uniqueIndex!.IsUnique);
            Assert.IsNotNull(relationship);
            Assert.AreEqual(nameof(AspNetUserFavorite.User), relationship!.DependentToPrincipal!.Name);

            #endregion
        }

        #endregion mapping tests

        #region favorite persistence tests

        /**************************************************************/
        /// <summary>
        /// Verifies SetAeProductFavoriteAsync add, duplicate add, remove, cross-user, and missing-product behavior.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SetAeProductFavoriteAsync(ApplicationDbContext, long, Guid, bool, ILogger)"/>
        [TestMethod]
        public async Task SetAeProductFavoriteAsync_AddRemoveIdempotentCrossUserAndMissingProduct_BehavesAsExpected()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();
            await seedUserAsync(context, 8001);
            await seedUserAsync(context, 8002);
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");

            var add = await DtoLabelAccess.SetAeProductFavoriteAsync(context, 8001, DtoLabelAccessTestHelper.TestDocumentGuid, true, logger);
            var addDuplicate = await DtoLabelAccess.SetAeProductFavoriteAsync(context, 8001, DtoLabelAccessTestHelper.TestDocumentGuid, true, logger);
            var addSecondUser = await DtoLabelAccess.SetAeProductFavoriteAsync(context, 8002, DtoLabelAccessTestHelper.TestDocumentGuid, true, logger);
            var countAfterAdds = await context.AspNetUserFavorites.CountAsync();
            var remove = await DtoLabelAccess.SetAeProductFavoriteAsync(context, 8001, DtoLabelAccessTestHelper.TestDocumentGuid, false, logger);
            var removeDuplicate = await DtoLabelAccess.SetAeProductFavoriteAsync(context, 8001, DtoLabelAccessTestHelper.TestDocumentGuid, false, logger);
            var missing = await DtoLabelAccess.SetAeProductFavoriteAsync(context, 8001, Guid.Parse("99999999-9999-9999-9999-999999999999"), true, logger);

            Assert.IsTrue(add);
            Assert.IsTrue(addDuplicate);
            Assert.IsTrue(addSecondUser);
            Assert.AreEqual(2, countAfterAdds);
            Assert.IsTrue(remove);
            Assert.IsTrue(removeDuplicate);
            Assert.IsFalse(missing);
            Assert.AreEqual(1, await context.AspNetUserFavorites.CountAsync());
            Assert.AreEqual(8002, (await context.AspNetUserFavorites.SingleAsync()).UserId);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetAeFavoriteDrugSummariesAsync returns only one user's favorites ordered by CreatedAt descending.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAeFavoriteDrugSummariesAsync(ApplicationDbContext, long, string, ILogger, int?, int?)"/>
        [TestMethod]
        public async Task GetAeFavoriteDrugSummariesAsync_UserScopedFavorites_ReturnsOrderedFavoriteDtos()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();
            await seedUserAsync(context, 8101);
            await seedUserAsync(context, 8102);
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid, "ASPIRIN");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid2, "IBUPROFEN");
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(connection, DtoLabelAccessTestHelper.TestDocumentGuid3, "ACETAMINOPHEN");
            context.AspNetUserFavorites.AddRange(
                new AspNetUserFavorite
                {
                    UserId = 8101,
                    DocumentGUID = DtoLabelAccessTestHelper.TestDocumentGuid,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-10)
                },
                new AspNetUserFavorite
                {
                    UserId = 8101,
                    DocumentGUID = DtoLabelAccessTestHelper.TestDocumentGuid2,
                    CreatedAt = DateTime.UtcNow
                },
                new AspNetUserFavorite
                {
                    UserId = 8102,
                    DocumentGUID = DtoLabelAccessTestHelper.TestDocumentGuid3,
                    CreatedAt = DateTime.UtcNow
                });
            await context.SaveChangesAsync();

            var favorites = await DtoLabelAccess.GetAeFavoriteDrugSummariesAsync(context, 8101, PkSecret, logger);
            var paged = await DtoLabelAccess.GetAeFavoriteDrugSummariesAsync(context, 8101, PkSecret, logger, page: 1, size: 1);

            Assert.AreEqual(2, favorites.Count);
            Assert.AreEqual("IBUPROFEN", favorites.First().ProductName);
            Assert.IsTrue(favorites.All(favorite => favorite.IsFavorite));
            Assert.IsFalse(favorites.Any(favorite => favorite.DocumentGUID == DtoLabelAccessTestHelper.TestDocumentGuid3));
            Assert.AreEqual(1, paged.Count);
            Assert.AreEqual("IBUPROFEN", paged.Single().ProductName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetAeFavoriteDrugSummariesAsync collapses a combination product's
        /// multiple summary-view strata into a single favorite row instead of throwing
        /// a duplicate-key error.
        /// </summary>
        /// <remarks>
        /// A combination product fans out into one vw_AeDrugSummary row per
        /// (substance × pharmacologic class). Before the collapse fix, the duplicate
        /// DocumentGUID rows reached a ToDictionary keyed by DocumentGUID and threw
        /// ArgumentException while loading favorites.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetAeFavoriteDrugSummariesAsync(ApplicationDbContext, long, string, ILogger, int?, int?)"/>
        [TestMethod]
        public async Task GetAeFavoriteDrugSummariesAsync_CombinationProductMultipleStrata_CollapsesToSingleFavorite()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();
            await seedUserAsync(context, 8201);

            // Two strata for the same document simulate a combination product that fans
            // out across two pharmacologic classes in vw_AeDrugSummary.
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(
                connection,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                productName: "COMBO RELIEF",
                substanceName: "Aspirin",
                unii: "R16CO5Y76E",
                pharmClassCode: "N0000175722",
                pharmClassName: "Nonsteroidal Anti-inflammatory Drug",
                ingredientSubstanceId: 20);
            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(
                connection,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                productName: "COMBO RELIEF",
                substanceName: "Caffeine",
                unii: "3G6A5W338E",
                pharmClassCode: "N0000175555",
                pharmClassName: "Central Nervous System Stimulant",
                ingredientSubstanceId: 21);

            var added = await DtoLabelAccess.SetAeProductFavoriteAsync(
                context, 8201, DtoLabelAccessTestHelper.TestDocumentGuid, true, logger);

            var favorites = await DtoLabelAccess.GetAeFavoriteDrugSummariesAsync(context, 8201, PkSecret, logger);

            Assert.IsTrue(added);
            Assert.AreEqual(1, favorites.Count);
            Assert.AreEqual(DtoLabelAccessTestHelper.TestDocumentGuid, favorites.Single().DocumentGUID);
            Assert.IsTrue(favorites.Single().IsFavorite);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies SetAeProductFavoriteAsync accepts a null-class product that exists
        /// only in the risk-table fallback, and that the favorite round-trips back
        /// through GetAeFavoriteDrugSummariesAsync.
        /// </summary>
        /// <remarks>
        /// Null-class products are absent from vw_AeDrugSummary and surface only through
        /// the tmp_FlattenedAdverseEventRiskTable fallback the picker uses. Before the
        /// eligibility fix, the favorite write rejected them as "not found".
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.SetAeProductFavoriteAsync(ApplicationDbContext, long, Guid, bool, ILogger)"/>
        [TestMethod]
        public async Task SetAeProductFavoriteAsync_NullClassProductInRiskTableOnly_SavesAndLoadsFavorite()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();
            await seedUserAsync(context, 8301);

            // Seed the product only in the risk table with no pharmacologic class, and
            // deliberately do not seed vw_AeDrugSummary, mirroring a null-class product.
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(
                connection,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                productName: "UNCLASSED DRUG",
                pharmClassCode: null,
                pharmClassName: null);

            var added = await DtoLabelAccess.SetAeProductFavoriteAsync(
                context, 8301, DtoLabelAccessTestHelper.TestDocumentGuid, true, logger);
            var persistedCount = await context.AspNetUserFavorites.CountAsync();

            var favorites = await DtoLabelAccess.GetAeFavoriteDrugSummariesAsync(context, 8301, PkSecret, logger);

            Assert.IsTrue(added);
            Assert.AreEqual(1, persistedCount);
            Assert.AreEqual(1, favorites.Count);
            Assert.AreEqual(DtoLabelAccessTestHelper.TestDocumentGuid, favorites.Single().DocumentGUID);
            Assert.IsTrue(favorites.Single().IsFavorite);

            #endregion
        }

        #endregion favorite persistence tests

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
