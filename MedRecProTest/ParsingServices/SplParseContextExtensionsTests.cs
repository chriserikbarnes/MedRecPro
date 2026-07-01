using MedRecProImportClass.Data;
using MedRecProImportClass.Service.ParsingServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MedRecProImportClass.Models.Label;

namespace MedRecPro.Service.Test.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Tests public helper methods on <see cref="SplParseContext"/> and <see cref="SplParseContextExtensions"/>.
    /// </summary>
    /// <remarks>
    /// These tests cover context resolution, EF tracking ID extraction, deferred saves, file-result aggregation,
    /// repository resolution, parser-mode flags, and parse-result merging.
    /// </remarks>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="SplParseContextExtensions"/>
    /// <seealso cref="SplParseResult"/>
    [TestClass]
    public class SplParseContextExtensionsTests
    {
        #region GetDbContext Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that a context with a shared DbContext returns that instance.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="SplParseContextExtensions.GetDbContext"/>
        [TestMethod]
        public async Task GetDbContext_SharedContextPresent_ReturnsSharedInstance()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            await database.SeedCommonContextAsync(parseContext);

            var result = parseContext.GetDbContext();

            Assert.AreSame(database.DbContext, result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that context resolution falls back to the service provider.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="SplParseContextExtensions.GetDbContext"/>
        [TestMethod]
        public async Task GetDbContext_NoSharedContext_ResolvesFromServiceProvider()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            await database.SeedCommonContextAsync(parseContext);
            parseContext.DbContext = null;

            var result = parseContext.GetDbContext();

            Assert.AreSame(database.DbContext, result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a context without a DbContext or service provider fails clearly.
        /// </summary>
        /// <seealso cref="SplParseContextExtensions.GetDbContext"/>
        [TestMethod]
        public void GetDbContext_NoContextOrProvider_ThrowsInvalidOperationException()
        {
            #region implementation
            var parseContext = new SplParseContext();

            Assert.ThrowsException<InvalidOperationException>(() => parseContext.GetDbContext());
            #endregion
        }

        #endregion

        #region Tracking And Save Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that EF tracked added entities expose their current key value.
        /// </summary>
        /// <seealso cref="SplParseContextExtensions.GetTrackedEntityId"/>
        [TestMethod]
        public void GetTrackedEntityId_AddedEntity_ReturnsTemporaryOrCurrentTrackedId()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var document = new Document { Title = "Tracked document" };
            database.DbContext.Set<Document>().Add(document);

            var result = database.DbContext.GetTrackedEntityId(document, nameof(Document.DocumentID));

            Assert.IsNotNull(result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that detached entities return no tracked key.
        /// </summary>
        /// <seealso cref="SplParseContextExtensions.GetTrackedEntityId"/>
        [TestMethod]
        public void GetTrackedEntityId_DetachedEntity_ReturnsNull()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var document = new Document { Title = "Detached document" };

            var result = database.DbContext.GetTrackedEntityId(document, nameof(Document.DocumentID));

            Assert.IsNull(result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a missing key name returns null instead of throwing.
        /// </summary>
        /// <seealso cref="SplParseContextExtensions.GetTrackedEntityId"/>
        [TestMethod]
        public void GetTrackedEntityId_MissingKey_ReturnsNull()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var document = new Document { Title = "Tracked document" };
            database.DbContext.Set<Document>().Add(document);

            var result = database.DbContext.GetTrackedEntityId(document, "MissingKey");

            Assert.IsNull(result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that immediate-save mode persists pending changes.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        [TestMethod]
        public async Task SaveChangesIfAllowedAsync_BatchSavingFalse_PersistsChanges()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext(ParserMode.SingleCall);
            database.DbContext.Set<Document>().Add(new Document { Title = "Immediate save" });

            await parseContext.SaveChangesIfAllowedAsync();

            Assert.AreEqual(1, await database.DbContext.Set<Document>().CountAsync());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that batch-saving mode defers pending changes.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        [TestMethod]
        public async Task SaveChangesIfAllowedAsync_BatchSavingTrue_DefersChanges()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext(ParserMode.StagedBulk);
            database.DbContext.Set<Document>().Add(new Document { Title = "Deferred save" });

            await parseContext.SaveChangesIfAllowedAsync();

            Assert.AreEqual(EntityState.Added, database.DbContext.ChangeTracker.Entries<Document>().Single().State);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that deferred changes persist when explicitly committed.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="SplParseContextExtensions.CommitDeferredChangesAsync"/>
        [TestMethod]
        public async Task CommitDeferredChangesAsync_BatchSavingTrue_PersistsDeferredChanges()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext(ParserMode.StagedBulk);
            database.DbContext.Set<Document>().Add(new Document { Title = "Commit deferred" });

            await parseContext.CommitDeferredChangesAsync();

            Assert.AreEqual(1, await database.DbContext.Set<Document>().CountAsync());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that committing deferred changes is a no-op when batch-saving is disabled.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="SplParseContextExtensions.CommitDeferredChangesAsync"/>
        [TestMethod]
        public async Task CommitDeferredChangesAsync_BatchSavingFalse_NoOps()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext(ParserMode.SingleCall);
            database.DbContext.Set<Document>().Add(new Document { Title = "No-op commit" });

            await parseContext.CommitDeferredChangesAsync();

            Assert.AreEqual(EntityState.Added, database.DbContext.ChangeTracker.Entries<Document>().Single().State);
            #endregion
        }

        #endregion

        #region Context Result Tests

        /**************************************************************/
        /// <summary>
        /// Verifies parser mode flag setters update the public context flags.
        /// </summary>
        /// <seealso cref="SplParseContext.SetBulkOperationsFlag"/>
        /// <seealso cref="SplParseContext.SetBulkStagingFlag"/>
        /// <seealso cref="SplParseContext.SetBatchSavingFlag"/>
        [TestMethod]
        public void SetModeFlags_TrueValues_UpdateContextFlags()
        {
            #region implementation
            var context = new SplParseContext();

            context.SetBulkOperationsFlag(true);
            context.SetBulkStagingFlag(true);
            context.SetBatchSavingFlag(true);

            Assert.IsTrue(context.UseBulkOperations);
            Assert.IsTrue(context.UseBulkStagingOperations);
            Assert.IsTrue(context.UseBatchSaving);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that repository resolution returns the configured generic repository.
        /// </summary>
        /// <seealso cref="SplParseContext.GetRepository{T}"/>
        [TestMethod]
        public void GetRepository_ConfiguredServiceProvider_ReturnsRepository()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();

            var repository = parseContext.GetRepository<Document>();

            Assert.IsNotNull(repository);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that repository resolution fails clearly when no provider is configured.
        /// </summary>
        /// <seealso cref="SplParseContext.GetRepository{T}"/>
        [TestMethod]
        public void GetRepository_NoServiceProvider_ThrowsInvalidOperationException()
        {
            #region implementation
            var parseContext = new SplParseContext();

            Assert.ThrowsException<InvalidOperationException>(() => parseContext.GetRepository<Document>());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that file result aggregation includes counts, errors, and failure state.
        /// </summary>
        /// <seealso cref="SplParseContext.UpdateFileResult"/>
        [TestMethod]
        public void UpdateFileResult_MergesCountsErrorsAndFailure()
        {
            #region implementation
            var parseContext = new SplParseContext();
            var result = new SplParseResult
            {
                Success = false,
                DocumentsCreated = 1,
                OrganizationsCreated = 2,
                ProductsCreated = 3,
                SectionsCreated = 4,
                IngredientsCreated = 5,
                ProductElementsCreated = 6,
                Errors = { "boom" }
            };

            parseContext.UpdateFileResult(result);

            Assert.IsNotNull(parseContext.FileResult);
            Assert.IsFalse(parseContext.FileResult.Success);
            Assert.AreEqual(1, parseContext.FileResult.DocumentsCreated);
            Assert.AreEqual(2, parseContext.FileResult.OrganizationsCreated);
            Assert.AreEqual(3, parseContext.FileResult.ProductsCreated);
            Assert.AreEqual(4, parseContext.FileResult.SectionsCreated);
            Assert.AreEqual(5, parseContext.FileResult.IngredientsCreated);
            Assert.AreEqual(6, parseContext.FileResult.ProductElementsCreated);
            CollectionAssert.Contains(parseContext.FileResult.Errors, "boom");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that SplParseResult merges all public counters and failure state.
        /// </summary>
        /// <seealso cref="SplParseResult.MergeFrom"/>
        [TestMethod]
        public void MergeFrom_CombinesCountsErrorsAndFailure()
        {
            #region implementation
            var aggregate = new SplParseResult { DocumentsCreated = 1 };
            var other = new SplParseResult
            {
                Success = false,
                DocumentsCreated = 2,
                DocumentAttributesCreated = 3,
                OrganizationsCreated = 4,
                OrganizationAttributesCreated = 5,
                ProductsCreated = 6,
                SectionsCreated = 7,
                SectionAttributesCreated = 8,
                ProductElementsCreated = 9,
                LicensesCreated = 10,
                LotHierarchiesCreated = 11,
                IngredientsCreated = 12,
                DisciplinaryActionsCreated = 13,
                AttachedDocumentsCreated = 14,
                Errors = { "nested failure" }
            };

            aggregate.MergeFrom(other);

            Assert.IsFalse(aggregate.Success);
            Assert.AreEqual(3, aggregate.DocumentsCreated);
            Assert.AreEqual(3, aggregate.DocumentAttributesCreated);
            Assert.AreEqual(4, aggregate.OrganizationsCreated);
            Assert.AreEqual(5, aggregate.OrganizationAttributesCreated);
            Assert.AreEqual(6, aggregate.ProductsCreated);
            Assert.AreEqual(7, aggregate.SectionsCreated);
            Assert.AreEqual(8, aggregate.SectionAttributesCreated);
            Assert.AreEqual(9, aggregate.ProductElementsCreated);
            Assert.AreEqual(10, aggregate.LicensesCreated);
            Assert.AreEqual(11, aggregate.LotHierarchiesCreated);
            Assert.AreEqual(12, aggregate.IngredientsCreated);
            Assert.AreEqual(13, aggregate.DisciplinaryActionsCreated);
            Assert.AreEqual(14, aggregate.AttachedDocumentsCreated);
            CollectionAssert.Contains(aggregate.Errors, "nested failure");
            #endregion
        }

        #endregion
    }
}
