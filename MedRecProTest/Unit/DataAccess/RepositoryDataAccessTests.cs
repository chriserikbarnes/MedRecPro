using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecProTest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System.Text;
using LabelModel = MedRecPro.Models.Label;

namespace MedRecProTest.Unit.DataAccess
{
    /**************************************************************/
    /// <summary>
    /// Exercises the generic <see cref="Repository{T}"/> CRUD surface and both
    /// <c>GetCompleteLabelsAsync</c> overloads against a SQLite in-memory
    /// <c>ApplicationDbContext</c>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="DtoLabelAccessTestHelper"/> for the shared-cache SQLite
    /// database, Label-schema seeding, and cache/Util reset. Encrypted primary
    /// keys are never compared as ciphertext (salt/IV are random per call);
    /// tests decrypt with the fixed secret instead. Note that
    /// <c>CreateAsync</c> returns a Strong-encrypted key ("S-" prefix) because
    /// the <c>TextUtil.Encrypt</c> extension drops the requested Fast strength;
    /// tests therefore keep Strong round-trips to a minimum.
    /// </remarks>
    /// <seealso cref="Repository{T}"/>
    /// <seealso cref="DtoLabelAccessTestHelper"/>
    /// <seealso cref="SplData"/>
    /// <seealso cref="LabelModel.Document"/>
    [TestClass]
    [TestCategory("Unit")]
    public class RepositoryDataAccessTests
    {
        #region implementation

        /// <summary>
        /// Fixed PK secret shared with <see cref="DtoLabelAccessTestHelper"/>.
        /// </summary>
        private const string TestPkSecret = DtoLabelAccessTestHelper.TestPkSecret;

        /**************************************************************/
        /// <summary>
        /// Clears the process-wide managed cache and rebinds Util statics so
        /// complete-label results never leak between tests.
        /// </summary>
        /// <seealso cref="DtoLabelAccessTestHelper.ClearCache"/>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation
            DtoLabelAccessTestHelper.ClearCache();
            #endregion
        }

        #region CreateAsync

        /**************************************************************/
        /// <summary>
        /// Verifies CreateAsync rejects a null entity.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task CreateAsync_NullEntity_ThrowsArgumentNullException()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var repository = createRepository<SplData>(context);

            // Act + Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => repository.CreateAsync(null!));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies CreateAsync persists the row and returns an encrypted
        /// primary key that decrypts back to the generated identity value.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task CreateAsync_ValidSplData_ReturnsEncryptedPrimaryKeyAndPersistsRow()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var repository = createRepository<SplData>(context);
            var splGuid = Guid.NewGuid();
            var entity = new SplData("<document>fixture</document>", splGuid);

            // Act
            var encryptedId = await repository.CreateAsync(entity);

            // Assert - encrypted key round-trips to the generated PK.
            Assert.IsFalse(string.IsNullOrWhiteSpace(encryptedId));
            var decrypted = long.Parse(encryptedId!.Decrypt(TestPkSecret)!);
            Assert.AreEqual(entity.SplDataID, decrypted);
            Assert.IsTrue(decrypted > 0);

            // Row persisted with the ctor-supplied values.
            var saved = context.SplData.Single();
            Assert.AreEqual(splGuid, saved.SplDataGUID);
            Assert.AreEqual("<document>fixture</document>", saved.SplXML);
            Assert.IsFalse(saved.Archive ?? true, "SplData ctor defaults Archive to false.");
            #endregion
        }

        #endregion

        #region ReadByIdAsync

        /**************************************************************/
        /// <summary>
        /// Verifies undecryptable IDs return null instead of throwing: garbage
        /// text and ciphertext produced with the wrong secret both fail soft.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task ReadByIdAsync_InvalidEncryptedId_ReturnsNullAndDoesNotThrow()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var repository = createRepository<LabelModel.Document>(context);
            var wrongSecretId = StringCipher.Encrypt("1", "Some-Other-Secret-Entirely!", StringCipher.EncryptionStrength.Fast);

            // Act
            var fromGarbage = await repository.ReadByIdAsync("not-encrypted-at-all");
            var fromWrongSecret = await repository.ReadByIdAsync(wrongSecretId);
            var fromWhitespace = await repository.ReadByIdAsync("   ");

            // Assert
            Assert.IsNull(fromGarbage);
            Assert.IsNull(fromWrongSecret);
            Assert.IsNull(fromWhitespace);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a correctly encrypted existing ID resolves the entity for
        /// an int-keyed type.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task ReadByIdAsync_EncryptedExistingId_ReturnsEntity()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var documentId = await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context, DtoLabelAccessTestHelper.TestDocumentGuid, DtoLabelAccessTestHelper.TestSetGuid, "Read Fixture");
            var repository = createRepository<LabelModel.Document>(context);
            var encryptedId = StringCipher.Encrypt(documentId.ToString(), TestPkSecret, StringCipher.EncryptionStrength.Fast);

            // Act
            var entity = await repository.ReadByIdAsync(encryptedId);

            // Assert
            Assert.IsNotNull(entity);
            Assert.AreEqual(documentId, entity!.DocumentID);
            Assert.AreEqual("Read Fixture", entity.Title);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies long-keyed entities round-trip through ReadByIdAsync: the
        /// repository converts the decrypted key to the entity's actual primary
        /// key type before the FindAsync lookup.
        /// </summary>
        /// <remarks>
        /// Historically the repository boxed every key as int, which made
        /// FindAsync throw ArgumentException for SplData's long primary key.
        /// This test covers the fix that converts the key to the mapped
        /// property type.
        /// </remarks>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task ReadByIdAsync_SplDataLongPrimaryKey_ReturnsEntity()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var repository = createRepository<SplData>(context);
            var splGuid = Guid.NewGuid();
            var encryptedId = await repository.CreateAsync(new SplData("<document>long-pk</document>", splGuid));

            // Act
            var entity = await repository.ReadByIdAsync(encryptedId!);

            // Assert - long PK resolves after the key-type conversion fix.
            Assert.IsNotNull(entity);
            Assert.AreEqual(splGuid, entity!.SplDataGUID);
            #endregion
        }

        #endregion

        #region ReadAllAsync

        /**************************************************************/
        /// <summary>
        /// Verifies ReadAllAsync returns every row when paging is not supplied
        /// (including when only one paging argument is present).
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task ReadAllAsync_NoPaging_ReturnsAllRows()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            seedSplDataRows(context, 3);
            var repository = createRepository<SplData>(context);

            // Act
            var all = (await repository.ReadAllAsync(null, null)).ToList();
            var halfPaged = (await repository.ReadAllAsync(null, 2)).ToList();

            // Assert - paging applies only when BOTH arguments have values.
            Assert.AreEqual(3, all.Count);
            Assert.AreEqual(3, halfPaged.Count, "A lone pageSize without pageNumber must not page.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies ReadAllAsync returns the expected slice when both paging
        /// arguments are supplied.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task ReadAllAsync_WithPaging_ReturnsExpectedPage()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            seedSplDataRows(context, 3);
            var repository = createRepository<SplData>(context);

            // Act
            var firstPage = (await repository.ReadAllAsync(1, 2)).ToList();
            var secondPage = (await repository.ReadAllAsync(2, 2)).ToList();

            // Assert
            Assert.AreEqual(2, firstPage.Count);
            Assert.AreEqual(1, secondPage.Count);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies invalid paging arguments are rejected with
        /// ArgumentOutOfRangeException.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task ReadAllAsync_InvalidPaging_ThrowsArgumentOutOfRangeException()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var repository = createRepository<SplData>(context);

            // Act + Assert - zero page number and non-positive page size.
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => repository.ReadAllAsync(0, 5));
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => repository.ReadAllAsync(1, 0));
            #endregion
        }

        #endregion

        #region UpdateAsync

        /**************************************************************/
        /// <summary>
        /// Verifies UpdateAsync rejects a null entity.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task UpdateAsync_NullEntity_ThrowsArgumentNullException()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var repository = createRepository<SplData>(context);

            // Act + Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => repository.UpdateAsync(null!));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies UpdateAsync marks the tracked entity modified, saves, and
        /// reports one affected row.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task UpdateAsync_ValidEntity_ReturnsAffectedRowAndPersistsChange()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            seedSplDataRows(context, 1);
            var repository = createRepository<SplData>(context);
            var entity = context.SplData.Single();

            // Act - flip the archive flag on the tracked instance.
            entity.Archive = true;
            var affected = await repository.UpdateAsync(entity);

            // Assert
            Assert.AreEqual(1, affected);
            Assert.IsTrue(context.SplData.Single().Archive ?? false);
            #endregion
        }

        #endregion

        #region DeleteAsync (surface-guard extras)

        /**************************************************************/
        /// <summary>
        /// Verifies DeleteAsync(string) throws InvalidOperationException for an
        /// undecryptable ID (unlike ReadByIdAsync's soft null).
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task DeleteAsync_InvalidEncryptedId_ThrowsInvalidOperationException()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var repository = createRepository<LabelModel.Document>(context);

            // Act + Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => repository.DeleteAsync("not-encrypted-at-all"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies DeleteAsync(string) throws KeyNotFoundException when the
        /// decrypted ID has no matching row.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task DeleteAsync_MissingEntity_ThrowsKeyNotFoundException()
        {
            #region implementation
            // Arrange - valid ciphertext for an ID that does not exist.
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var repository = createRepository<LabelModel.Document>(context);
            var encryptedMissing = StringCipher.Encrypt("999", TestPkSecret, StringCipher.EncryptionStrength.Fast);

            // Act + Assert
            await Assert.ThrowsExceptionAsync<KeyNotFoundException>(
                () => repository.DeleteAsync(encryptedMissing));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the entity overload validates its argument (null entity and
        /// unpopulated primary key) and that a seeded entity deletes cleanly
        /// end-to-end through both overloads.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task DeleteAsync_EntityOverload_ValidatesAndRemovesSeededRow()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var documentId = await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context, DtoLabelAccessTestHelper.TestDocumentGuid, DtoLabelAccessTestHelper.TestSetGuid, "Delete Fixture");
            var repository = createRepository<LabelModel.Document>(context);

            // Act + Assert - null entity.
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => repository.DeleteAsync((LabelModel.Document)null!));

            // Act + Assert - unpopulated (non-positive) primary key.
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => repository.DeleteAsync(new LabelModel.Document()));

            // Act - delete the seeded row through the entity overload, which
            // encrypts the PK and forwards to DeleteAsync(string).
            var tracked = context.Set<LabelModel.Document>().Single(d => d.DocumentID == documentId);
            var affected = await repository.DeleteAsync(tracked);

            // Assert
            Assert.AreEqual(1, affected);
            Assert.AreEqual(0, context.Set<LabelModel.Document>().Count(d => d.DocumentID == documentId));
            #endregion
        }

        #endregion

        #region GetCompleteLabelsAsync

        /**************************************************************/
        /// <summary>
        /// Verifies the paged overload builds a complete DocumentDto page from
        /// seeded Label-schema rows.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        /// <seealso cref="DtoLabelAccessTestHelper.SeedFullDocumentHierarchyAsync"/>
        [TestMethod]
        public async Task GetCompleteLabelsAsync_PageOverload_WithSeededLabel_ReturnsExpectedDocumentPage()
        {
            #region implementation
            // Arrange - full hierarchy plus labeler, packaging/NDC, and text.
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);

            var (documentId, _, sectionId, productId) = await DtoLabelAccessTestHelper.SeedFullDocumentHierarchyAsync(
                context, DtoLabelAccessTestHelper.TestDocumentGuid, DtoLabelAccessTestHelper.TestSetGuid, "ASPIRIN");
            var organizationId = await DtoLabelAccessTestHelper.SeedOrganizationAsync(context, "TEST PHARMACEUTICALS INC");
            await DtoLabelAccessTestHelper.SeedDocumentAuthorAsync(context, documentId, organizationId);
            var packagingLevelId = await DtoLabelAccessTestHelper.SeedPackagingLevelAsync(context, productId);
            DtoLabelAccessTestHelper.SeedPackageIdentifierAsync(context, packagingLevelId, "12345-678-90", "NDCPackage").Wait();
            await DtoLabelAccessTestHelper.SeedSectionTextContentAsync(context, sectionId, "Test section content text.");

            var repository = createRepository<LabelModel.Document>(context);

            // Act
            var page = await repository.GetCompleteLabelsAsync(pageNumber: 1, pageSize: 10);

            // Assert - one complete document with children attached.
            Assert.AreEqual(1, page.Count);
            var dto = page[0];
            Assert.AreEqual(DtoLabelAccessTestHelper.TestDocumentGuid, dto.DocumentGUID);
            Assert.AreEqual(1, dto.StructuredBodies.Count, "Seeded hierarchy carries one structured body.");
            Assert.AreEqual(1, dto.DocumentAuthors.Count, "Seeded labeler author should be attached.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the paged overload's static-cache branch by pre-seeding the
        /// exact managed cache key with a sentinel JSON payload.
        /// </summary>
        /// <remarks>
        /// Repo-layer key plaintext is
        /// "GetCompleteLabelsAsync_{page}_{size}_{loadingMode}" Base64-encoded;
        /// the cached value is a JSON string of the result list.
        /// </remarks>
        /// <seealso cref="Repository{T}"/>
        /// <seealso cref="PerformanceHelper.SetCacheManageKey"/>
        [TestMethod]
        public async Task GetCompleteLabelsAsync_PageOverload_CacheHit_ReturnsCachedListWithoutQuerying()
        {
            #region implementation
            // Arrange - empty database; only the cache holds a sentinel.
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var repository = createRepository<LabelModel.Document>(context);

            var cacheKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("GetCompleteLabelsAsync_3_7_sequential"));
            var sentinelList = new List<DocumentDto>
            {
                new DocumentDto { Document = new Dictionary<string, object?> { ["Title"] = "CACHED-SENTINEL" } }
            };
            PerformanceHelper.SetCacheManageKey(cacheKey, JsonConvert.SerializeObject(sentinelList), 1.0);

            // Act - same page/size/loading-mode as the seeded key.
            var result = await repository.GetCompleteLabelsAsync(pageNumber: 3, pageSize: 7);

            // Assert - the sentinel came back even though the DB is empty.
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("CACHED-SENTINEL", result[0].Document["Title"]?.ToString());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the GUID overload returns the seeded document.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task GetCompleteLabelsAsync_GuidOverload_WithSeededGuid_ReturnsExpectedDocument()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context, DtoLabelAccessTestHelper.TestDocumentGuid2, DtoLabelAccessTestHelper.TestSetGuid2, "Guid Overload Fixture");
            var repository = createRepository<LabelModel.Document>(context);

            // Act
            var result = await repository.GetCompleteLabelsAsync(DtoLabelAccessTestHelper.TestDocumentGuid2);

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(DtoLabelAccessTestHelper.TestDocumentGuid2, result[0].DocumentGUID);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the GUID overload returns an empty list when nothing
        /// matches.
        /// </summary>
        /// <seealso cref="Repository{T}"/>
        [TestMethod]
        public async Task GetCompleteLabelsAsync_NoMatches_ReturnsEmptyList()
        {
            #region implementation
            // Arrange - empty database and an unknown GUID.
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var repository = createRepository<LabelModel.Document>(context);

            // Act
            var result = await repository.GetCompleteLabelsAsync(Guid.NewGuid());

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
            #endregion
        }

        #endregion

        #region Harness helpers

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="Repository{T}"/> over the supplied context with
        /// the fixed PK secret and sequential loading mode.
        /// </summary>
        /// <typeparam name="T">Entity type under test.</typeparam>
        /// <param name="context">SQLite-backed application context.</param>
        /// <returns>A functional repository.</returns>
        /// <seealso cref="Repository{T}"/>
        private static Repository<T> createRepository<T>(MedRecPro.Data.ApplicationDbContext context) where T : class
        {
            #region implementation
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:DB:PKSecret"] = TestPkSecret,
                    ["FeatureFlags:UseBatchDocumentLoading"] = "false"
                })
                .Build();

            return new Repository<T>(context, new StringCipher(), new Mock<ILogger<T>>().Object, configuration);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Inserts the requested number of SplData rows directly through the
        /// context.
        /// </summary>
        /// <param name="context">Context to insert into.</param>
        /// <param name="count">Number of rows to create.</param>
        private static void seedSplDataRows(MedRecPro.Data.ApplicationDbContext context, int count)
        {
            #region implementation
            for (var i = 0; i < count; i++)
            {
                context.SplData.Add(new SplData($"<document>row {i}</document>", Guid.NewGuid()));
            }

            context.SaveChanges();
            #endregion
        }

        #endregion

        #endregion
    }
}
