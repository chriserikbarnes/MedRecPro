using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for DtoLabelAccess document building methods.
    /// </summary>
    /// <remarks>
    /// Tests cover: BuildDocumentsAsync (paginated), BuildDocumentsAsync (by GUID),
    /// GetPackageIdentifierAsync, pagination, caching, batch vs sequential loading.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    [TestClass]
    public class DtoLabelAccessDocumentTests
    {
        #region Test Initialization

        /**************************************************************/
        /// <summary>
        /// Clears the PerformanceHelper managed cache before each test
        /// to prevent cross-test pollution from cached results.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation

            DtoLabelAccessTestHelper.ClearCache();

            #endregion
        }

        #endregion Test Initialization

        #region BuildDocumentsAsync (Paginated) Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildDocumentsAsync returns an empty list when the database
        /// contains no Document entities.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, string, Microsoft.Extensions.Logging.ILogger, int?, int?, bool?)"/>
        [TestMethod]
        public async Task BuildDocumentsAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.BuildDocumentsAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero documents");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildDocumentsAsync returns exactly one document when a single
        /// Document entity is seeded into the database.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, string, Microsoft.Extensions.Logging.ILogger, int?, int?, bool?)"/>
        [TestMethod]
        public async Task BuildDocumentsAsync_SingleDocument_ReturnsOneDocument()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Single Document Test");

            // Act
            var result = await DtoLabelAccess.BuildDocumentsAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return exactly one document");
            Assert.IsNotNull(result[0].Document, "Document dictionary should not be null");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildDocumentsAsync returns all documents when multiple
        /// Document entities are seeded into the database without pagination.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, string, Microsoft.Extensions.Logging.ILogger, int?, int?, bool?)"/>
        [TestMethod]
        public async Task BuildDocumentsAsync_MultipleDocuments_ReturnsAll()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Document One");

            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Document Two");

            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid3,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Document Three");

            // Act
            var result = await DtoLabelAccess.BuildDocumentsAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(3, result.Count, "Should return all three seeded documents");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildDocumentsAsync respects page and size parameters,
        /// returning the correct number of documents for the first page.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, string, Microsoft.Extensions.Logging.ILogger, int?, int?, bool?)"/>
        [TestMethod]
        public async Task BuildDocumentsAsync_Pagination_ReturnsCorrectPage()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Document One");

            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Document Two");

            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid3,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Document Three");

            // Act — request page 1 with size 2
            var result = await DtoLabelAccess.BuildDocumentsAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger,
                page: 1,
                size: 2);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(2, result.Count, "First page with size 2 should return exactly 2 documents");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildDocumentsAsync returns the remaining documents
        /// on the second page when total documents exceed the page size.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, string, Microsoft.Extensions.Logging.ILogger, int?, int?, bool?)"/>
        [TestMethod]
        public async Task BuildDocumentsAsync_PaginationSecondPage_ReturnsRemaining()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Document One");

            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Document Two");

            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid3,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Document Three");

            // Act — request page 2 with size 2 (only 1 document remaining)
            var result = await DtoLabelAccess.BuildDocumentsAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger,
                page: 2,
                size: 2);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Second page should return the one remaining document");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildDocumentsAsync returns documents when batch loading
        /// is explicitly enabled via the useBatchLoading flag.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, string, Microsoft.Extensions.Logging.ILogger, int?, int?, bool?)"/>
        [TestMethod]
        public async Task BuildDocumentsAsync_BatchLoading_ReturnsDocuments()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            await DtoLabelAccessTestHelper.SeedFullDocumentHierarchyAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestSetGuid);

            // Act — explicitly enable batch loading
            var result = await DtoLabelAccess.BuildDocumentsAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger,
                useBatchLoading: true);

            // Assert
            Assert.IsNotNull(result, "Result should not be null with batch loading");
            Assert.AreEqual(1, result.Count, "Should return the seeded document using batch loading");
            Assert.IsNotNull(result[0].Document, "Document dictionary should be populated");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildDocumentsAsync returns documents when sequential loading
        /// is explicitly used via the useBatchLoading flag set to false.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, string, Microsoft.Extensions.Logging.ILogger, int?, int?, bool?)"/>
        [TestMethod]
        public async Task BuildDocumentsAsync_SequentialLoading_ReturnsDocuments()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            await DtoLabelAccessTestHelper.SeedFullDocumentHierarchyAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestSetGuid);

            // Act — explicitly use sequential loading
            var result = await DtoLabelAccess.BuildDocumentsAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger,
                useBatchLoading: false);

            // Assert
            Assert.IsNotNull(result, "Result should not be null with sequential loading");
            Assert.AreEqual(1, result.Count, "Should return the seeded document using sequential loading");
            Assert.IsNotNull(result[0].Document, "Document dictionary should be populated");

            #endregion
        }

        #endregion BuildDocumentsAsync (Paginated) Tests

        #region BuildDocumentsAsync (GUID) Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildDocumentsAsync by GUID returns an empty list when the
        /// database contains no Document entities.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, Guid, string, Microsoft.Extensions.Logging.ILogger, bool?)"/>
        [TestMethod]
        public async Task BuildDocumentsAsync_ByGuid_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.BuildDocumentsAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero documents");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildDocumentsAsync by GUID returns an empty list when the
        /// specified GUID does not match any document in the database.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, Guid, string, Microsoft.Extensions.Logging.ILogger, bool?)"/>
        [TestMethod]
        public async Task BuildDocumentsAsync_ByGuid_NonExistentGuid_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Seed a document with a different GUID
            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Existing Document");

            // Act — search for a GUID that does not exist
            var nonExistentGuid = Guid.Parse("99999999-9999-9999-9999-999999999999");
            var result = await DtoLabelAccess.BuildDocumentsAsync(
                context,
                nonExistentGuid,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null for non-existent GUID");
            Assert.AreEqual(0, result.Count, "Non-existent GUID should return zero documents");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildDocumentsAsync by GUID returns exactly one document
        /// when the specified GUID matches a seeded Document entity.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, Guid, string, Microsoft.Extensions.Logging.ILogger, bool?)"/>
        [TestMethod]
        public async Task BuildDocumentsAsync_ByGuid_ValidGuid_ReturnsOneDocument()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Target Document");

            // Seed a second document to ensure filtering is correct
            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Other Document");

            // Act
            var result = await DtoLabelAccess.BuildDocumentsAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return exactly one document matching the GUID");
            Assert.IsNotNull(result[0].Document, "Document dictionary should not be null");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that BuildDocumentsAsync by GUID returns a document when batch
        /// loading is explicitly enabled, ensuring the batch code path works correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync(ApplicationDbContext, Guid, string, Microsoft.Extensions.Logging.ILogger, bool?)"/>
        [TestMethod]
        public async Task BuildDocumentsAsync_ByGuid_BatchLoading_ReturnsDocument()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            await DtoLabelAccessTestHelper.SeedFullDocumentHierarchyAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestSetGuid);

            // Act — use batch loading for the GUID overload
            var result = await DtoLabelAccess.BuildDocumentsAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger,
                useBatchLoading: true);

            // Assert
            Assert.IsNotNull(result, "Result should not be null with batch loading");
            Assert.AreEqual(1, result.Count, "Should return exactly one document using batch loading");
            Assert.IsNotNull(result[0].Document, "Document dictionary should be populated");

            #endregion
        }

        #endregion BuildDocumentsAsync (GUID) Tests

        #region GetPackageIdentifierAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetPackageIdentifierAsync returns null when a null
        /// packaging level ID is provided.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetPackageIdentifierAsync"/>
        [TestMethod]
        public async Task GetPackageIdentifierAsync_NullPackagingLevelId_ReturnsNull()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.GetPackageIdentifierAsync(
                context,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNull(result, "Null packaging level ID should return null");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetPackageIdentifierAsync returns null when the specified
        /// packaging level ID does not exist in the database.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetPackageIdentifierAsync"/>
        [TestMethod]
        public async Task GetPackageIdentifierAsync_NonExistentId_ReturnsNull()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act — use an ID that does not exist
            var result = await DtoLabelAccess.GetPackageIdentifierAsync(
                context,
                99999,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNull(result, "Non-existent packaging level ID should return null");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetPackageIdentifierAsync returns a valid PackageIdentifierDto
        /// when a matching packaging level ID exists in the database.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetPackageIdentifierAsync"/>
        [TestMethod]
        public async Task GetPackageIdentifierAsync_ValidId_ReturnsDto()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Seed the full hierarchy: Document -> StructuredBody -> Section -> Product
            var (docId, bodyId, sectionId, productId) =
                await DtoLabelAccessTestHelper.SeedFullDocumentHierarchyAsync(
                    context,
                    DtoLabelAccessTestHelper.TestDocumentGuid,
                    DtoLabelAccessTestHelper.TestSetGuid);

            // Seed a packaging level under the product
            var packagingLevelId = await DtoLabelAccessTestHelper.SeedPackagingLevelAsync(
                context, productId);

            // Seed a package identifier under the packaging level
            await DtoLabelAccessTestHelper.SeedPackageIdentifierAsync(
                context,
                packagingLevelId,
                "12345-678-90",
                "NDCPackage");

            // Act
            var result = await DtoLabelAccess.GetPackageIdentifierAsync(
                context,
                packagingLevelId,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Valid packaging level ID should return a PackageIdentifierDto");
            Assert.IsNotNull(result.PackageIdentifier, "PackageIdentifier dictionary should not be null");

            #endregion
        }

        #endregion GetPackageIdentifierAsync Tests
    }
}
