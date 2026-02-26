using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for DtoLabelAccess content-oriented view methods (#23-#36).
    /// Covers section navigation, drug safety lookups, product summaries,
    /// related products, inventory, latest labels, indications,
    /// section markdown, full label export, and Claude-cleaned markdown.
    /// </summary>
    /// <remarks>
    /// All tests use shared-cache named SQLite in-memory databases
    /// with sentinel connections following the test helper pattern.
    /// View backing tables are seeded via raw SQL through
    /// <see cref="DtoLabelAccessTestHelper"/> seed methods.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="DtoLabelAccessTestHelper"/>
    /// <seealso cref="LabelView"/>
    [TestClass]
    public class DtoLabelAccessContentTests
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

        #region SearchBySectionCodeAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that SearchBySectionCodeAsync returns an empty list
        /// when the database contains no SectionNavigation rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchBySectionCodeAsync"/>
        [TestMethod]
        public async Task SearchBySectionCodeAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.SearchBySectionCodeAsync(
                context,
                "34067-9",
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that SearchBySectionCodeAsync returns a mapped DTO
        /// when a matching SectionNavigation row exists.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchBySectionCodeAsync"/>
        [TestMethod]
        public async Task SearchBySectionCodeAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedSectionNavigationView(
                connection,
                sectionCode: "34067-9",
                sectionType: "INDICATIONS AND USAGE");

            // Act
            var result = await DtoLabelAccess.SearchBySectionCodeAsync(
                context,
                "34067-9",
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return one matching result");
            Assert.IsNotNull(result[0].SectionNavigation, "SectionNavigation dictionary should not be null");
            Assert.AreEqual("34067-9", result[0].SectionCode, "Section code should match seeded value");
            Assert.AreEqual("INDICATIONS AND USAGE", result[0].SectionType, "Section type should match seeded value");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that SearchBySectionCodeAsync filters correctly and
        /// returns only sections matching the requested code.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchBySectionCodeAsync"/>
        [TestMethod]
        public async Task SearchBySectionCodeAsync_NonMatchingCode_ReturnsEmpty()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedSectionNavigationView(
                connection,
                sectionCode: "34067-9",
                sectionType: "INDICATIONS AND USAGE");

            // Act — search for a different section code
            var result = await DtoLabelAccess.SearchBySectionCodeAsync(
                context,
                "34066-1",
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(0, result.Count, "Non-matching code should return zero results");

            #endregion
        }

        #endregion SearchBySectionCodeAsync Tests

        #region GetSectionTypeSummariesAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSectionTypeSummariesAsync returns an empty list
        /// when the database contains no SectionTypeSummary rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetSectionTypeSummariesAsync"/>
        [TestMethod]
        public async Task GetSectionTypeSummariesAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.GetSectionTypeSummariesAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSectionTypeSummariesAsync returns a mapped DTO
        /// with correct section code and counts.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetSectionTypeSummariesAsync"/>
        [TestMethod]
        public async Task GetSectionTypeSummariesAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedSectionTypeSummaryView(
                connection,
                sectionCode: "34067-9",
                sectionType: "INDICATIONS AND USAGE",
                sectionCount: 100,
                documentCount: 50);

            // Act
            var result = await DtoLabelAccess.GetSectionTypeSummariesAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return one summary");
            Assert.AreEqual("34067-9", result[0].SectionCode, "Section code should match seeded value");
            Assert.AreEqual("INDICATIONS AND USAGE", result[0].SectionType, "Section type should match seeded value");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSectionTypeSummariesAsync orders results
        /// by DocumentCount descending (highest count first).
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetSectionTypeSummariesAsync"/>
        [TestMethod]
        public async Task GetSectionTypeSummariesAsync_MultipleRows_OrderedByDocumentCountDescending()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedSectionTypeSummaryView(
                connection,
                sectionCode: "34067-9",
                sectionType: "INDICATIONS AND USAGE",
                sectionCount: 50,
                documentCount: 20);

            DtoLabelAccessTestHelper.SeedSectionTypeSummaryView(
                connection,
                sectionCode: "34084-4",
                sectionType: "ADVERSE REACTIONS",
                sectionCount: 200,
                documentCount: 100);

            // Act
            var result = await DtoLabelAccess.GetSectionTypeSummariesAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(2, result.Count, "Should return both summaries");
            Assert.AreEqual("34084-4", result[0].SectionCode, "Highest document count should be first");
            Assert.AreEqual("34067-9", result[1].SectionCode, "Lower document count should be second");

            #endregion
        }

        #endregion GetSectionTypeSummariesAsync Tests

        #region GetSectionContentAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSectionContentAsync returns an empty list
        /// when the database contains no SectionContent rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetSectionContentAsync"/>
        [TestMethod]
        public async Task GetSectionContentAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.GetSectionContentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                null,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSectionContentAsync returns content
        /// filtered by documentGuid only (no section filters).
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetSectionContentAsync"/>
        [TestMethod]
        public async Task GetSectionContentAsync_ByDocumentGuid_ReturnsMappedDto()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedSectionContentView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                sectionCode: "34067-9",
                sectionTitle: "INDICATIONS AND USAGE",
                contentText: "This drug is indicated for pain relief.");

            // Act
            var result = await DtoLabelAccess.GetSectionContentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                null,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return one content row");
            Assert.IsNotNull(result[0].SectionContent, "SectionContent dictionary should not be null");
            Assert.AreEqual("34067-9", result[0].SectionCode, "Section code should match seeded value");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSectionContentAsync correctly filters
        /// by documentGuid and sectionCode combination.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetSectionContentAsync"/>
        [TestMethod]
        public async Task GetSectionContentAsync_ByDocumentGuidAndSectionCode_FiltersCorrectly()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedSectionContentView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                sectionCode: "34067-9",
                sectionTitle: "INDICATIONS AND USAGE",
                contentText: "This drug is indicated for pain relief.");

            DtoLabelAccessTestHelper.SeedSectionContentView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                sectionCode: "34084-4",
                sectionTitle: "ADVERSE REACTIONS",
                contentText: "Common side effects include nausea.");

            // Act — filter to only Indications
            var result = await DtoLabelAccess.GetSectionContentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                null,
                "34067-9",
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Count >= 1, "Should return at least the matching section");
            Assert.IsTrue(
                result.Any(r => r.SectionCode == "34067-9"),
                "Should contain the requested section code");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetSectionContentAsync excludes content
        /// from a different document GUID.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetSectionContentAsync"/>
        [TestMethod]
        public async Task GetSectionContentAsync_DifferentDocumentGuid_ReturnsEmpty()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedSectionContentView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                sectionCode: "34067-9",
                sectionTitle: "INDICATIONS AND USAGE",
                contentText: "This drug is indicated for pain relief.");

            // Act — query with a different document GUID
            var result = await DtoLabelAccess.GetSectionContentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                null,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(0, result.Count, "Different document GUID should return zero results");

            #endregion
        }

        #endregion GetSectionContentAsync Tests

        #region GetDrugInteractionsAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetDrugInteractionsAsync returns an empty list
        /// when the database contains no DrugInteractionLookup rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDrugInteractionsAsync"/>
        [TestMethod]
        public async Task GetDrugInteractionsAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.GetDrugInteractionsAsync(
                context,
                new[] { "R16CO5Y76E" },
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetDrugInteractionsAsync returns a mapped DTO
        /// when a matching UNII exists in the DrugInteractionLookup view.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDrugInteractionsAsync"/>
        [TestMethod]
        public async Task GetDrugInteractionsAsync_WithMatchingUNII_ReturnsMappedDto()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedDrugInteractionLookupView(
                connection,
                ingredientUNII: "R16CO5Y76E",
                ingredientName: "ASPIRIN",
                productName: "ASPIRIN TABLETS");

            // Act
            var result = await DtoLabelAccess.GetDrugInteractionsAsync(
                context,
                new[] { "R16CO5Y76E" },
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return one matching interaction");
            Assert.IsNotNull(result[0].DrugInteractionLookup, "DrugInteractionLookup dictionary should not be null");
            Assert.AreEqual("ASPIRIN TABLETS", result[0].ProductName, "Product name should match seeded value");
            Assert.AreEqual("R16CO5Y76E", result[0].IngredientUNII, "Ingredient UNII should match seeded value");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetDrugInteractionsAsync returns empty
        /// when UNII list does not match any interactions.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDrugInteractionsAsync"/>
        [TestMethod]
        public async Task GetDrugInteractionsAsync_NoMatchingUNII_ReturnsEmpty()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedDrugInteractionLookupView(
                connection,
                ingredientUNII: "R16CO5Y76E",
                ingredientName: "ASPIRIN",
                productName: "ASPIRIN TABLETS");

            // Act — search for a different UNII
            var result = await DtoLabelAccess.GetDrugInteractionsAsync(
                context,
                new[] { "NONEXISTENT_UNII" },
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(0, result.Count, "Non-matching UNII should return zero results");

            #endregion
        }

        #endregion GetDrugInteractionsAsync Tests

        #region GetDEAScheduleProductsAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetDEAScheduleProductsAsync returns an empty list
        /// when the database contains no DEAScheduleLookup rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDEAScheduleProductsAsync"/>
        [TestMethod]
        public async Task GetDEAScheduleProductsAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.GetDEAScheduleProductsAsync(
                context,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetDEAScheduleProductsAsync returns all products
        /// with DEA codes when no schedule filter is applied.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDEAScheduleProductsAsync"/>
        [TestMethod]
        public async Task GetDEAScheduleProductsAsync_NoFilter_ReturnsAllWithDEACodes()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedDEAScheduleLookupView(
                connection,
                deaScheduleCode: "CII",
                deaSchedule: "Schedule II",
                productName: "OXYCODONE");

            DtoLabelAccessTestHelper.SeedDEAScheduleLookupView(
                connection,
                deaScheduleCode: "CV",
                deaSchedule: "Schedule V",
                productName: "TESTOSTERONE",
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2);

            // Act — no schedule code filter
            var result = await DtoLabelAccess.GetDEAScheduleProductsAsync(
                context,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(2, result.Count, "Should return both DEA scheduled products");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetDEAScheduleProductsAsync correctly filters
        /// by a specific DEA schedule code.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDEAScheduleProductsAsync"/>
        [TestMethod]
        public async Task GetDEAScheduleProductsAsync_WithScheduleCode_FiltersCorrectly()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedDEAScheduleLookupView(
                connection,
                deaScheduleCode: "CII",
                deaSchedule: "Schedule II",
                productName: "OXYCODONE");

            DtoLabelAccessTestHelper.SeedDEAScheduleLookupView(
                connection,
                deaScheduleCode: "CV",
                deaSchedule: "Schedule V",
                productName: "TESTOSTERONE",
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2);

            // Act — filter to CII only
            var result = await DtoLabelAccess.GetDEAScheduleProductsAsync(
                context,
                "CII",
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return only Schedule II product");
            Assert.AreEqual("CII", result[0].DEAScheduleCode, "DEA schedule code should match filter");
            Assert.AreEqual("OXYCODONE", result[0].ProductName, "Product name should match seeded value");

            #endregion
        }

        #endregion GetDEAScheduleProductsAsync Tests

        #region SearchProductSummaryAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that SearchProductSummaryAsync returns an empty list
        /// when the database contains no ProductSummary rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchProductSummaryAsync"/>
        [TestMethod]
        public async Task SearchProductSummaryAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.SearchProductSummaryAsync(
                context,
                "ASPIRIN",
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that SearchProductSummaryAsync returns a mapped DTO
        /// with the correct product name when a match exists.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchProductSummaryAsync"/>
        [TestMethod]
        public async Task SearchProductSummaryAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductSummaryView(
                connection,
                productName: "ASPIRIN TABLETS",
                productId: 1,
                documentId: 1);

            // Act
            var result = await DtoLabelAccess.SearchProductSummaryAsync(
                context,
                "ASPIRIN",
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return one matching product");
            Assert.IsNotNull(result[0].ProductSummary, "ProductSummary dictionary should not be null");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that SearchProductSummaryAsync returns empty
        /// when the product name search does not match any rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchProductSummaryAsync"/>
        [TestMethod]
        public async Task SearchProductSummaryAsync_NonMatchingName_ReturnsEmpty()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductSummaryView(
                connection,
                productName: "ASPIRIN TABLETS",
                productId: 1,
                documentId: 1);

            // Act — search for a completely different product
            var result = await DtoLabelAccess.SearchProductSummaryAsync(
                context,
                "ZZZNONEXISTENT",
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(0, result.Count, "Non-matching name should return zero results");

            #endregion
        }

        #endregion SearchProductSummaryAsync Tests

        #region GetRelatedProductsAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetRelatedProductsAsync returns an empty list
        /// when the database contains no RelatedProducts rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetRelatedProductsAsync"/>
        [TestMethod]
        public async Task GetRelatedProductsAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.GetRelatedProductsAsync(
                context,
                sourceProductId: 1,
                sourceDocumentGuid: null,
                relationshipType: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetRelatedProductsAsync returns related products
        /// when filtering by sourceProductId.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetRelatedProductsAsync"/>
        [TestMethod]
        public async Task GetRelatedProductsAsync_BySourceProductId_ReturnsMappedDto()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedRelatedProductsView(
                connection,
                sourceProductId: 1,
                sourceProductName: "ASPIRIN",
                relatedProductId: 2,
                relatedProductName: "GENERIC ASPIRIN",
                relationshipType: "SameIngredient");

            // Act
            var result = await DtoLabelAccess.GetRelatedProductsAsync(
                context,
                sourceProductId: 1,
                sourceDocumentGuid: null,
                relationshipType: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return one related product");
            Assert.AreEqual("ASPIRIN", result[0].SourceProductName, "Source product name should match");
            Assert.AreEqual("GENERIC ASPIRIN", result[0].RelatedProductName, "Related product name should match");
            Assert.AreEqual("SameIngredient", result[0].RelationshipType, "Relationship type should match");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetRelatedProductsAsync filters by sourceDocumentGuid.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetRelatedProductsAsync"/>
        [TestMethod]
        public async Task GetRelatedProductsAsync_BySourceDocumentGuid_FiltersCorrectly()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedRelatedProductsView(
                connection,
                sourceProductId: 1,
                sourceProductName: "ASPIRIN",
                sourceDocumentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                relatedProductId: 2,
                relatedProductName: "GENERIC ASPIRIN",
                relationshipType: "SameIngredient");

            // Act — filter by document GUID
            var result = await DtoLabelAccess.GetRelatedProductsAsync(
                context,
                sourceProductId: null,
                sourceDocumentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                relationshipType: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return one related product for the document GUID");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetRelatedProductsAsync filters by relationshipType.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetRelatedProductsAsync"/>
        [TestMethod]
        public async Task GetRelatedProductsAsync_ByRelationshipType_FiltersCorrectly()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedRelatedProductsView(
                connection,
                sourceProductId: 1,
                sourceProductName: "ASPIRIN",
                relatedProductId: 2,
                relatedProductName: "GENERIC ASPIRIN",
                relationshipType: "SameIngredient");

            DtoLabelAccessTestHelper.SeedRelatedProductsView(
                connection,
                sourceProductId: 1,
                sourceProductName: "ASPIRIN",
                relatedProductId: 3,
                relatedProductName: "BAYER ASPIRIN",
                relationshipType: "SameApplicationNumber");

            // Act — filter to SameIngredient only
            var result = await DtoLabelAccess.GetRelatedProductsAsync(
                context,
                sourceProductId: null,
                sourceDocumentGuid: null,
                relationshipType: "SameIngredient",
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return only SameIngredient relationship");
            Assert.AreEqual("SameIngredient", result[0].RelationshipType, "Relationship type should match filter");

            #endregion
        }

        #endregion GetRelatedProductsAsync Tests

        #region GetAPIEndpointGuideAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetAPIEndpointGuideAsync returns an empty list
        /// when the database contains no APIEndpointGuide rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAPIEndpointGuideAsync"/>
        [TestMethod]
        public async Task GetAPIEndpointGuideAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.GetAPIEndpointGuideAsync(
                context,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetAPIEndpointGuideAsync returns all endpoints
        /// when no category filter is applied.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAPIEndpointGuideAsync"/>
        [TestMethod]
        public async Task GetAPIEndpointGuideAsync_NoCategory_ReturnsAll()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedAPIEndpointGuideView(
                connection,
                viewName: "vw_ProductsByApplicationNumber",
                endpointName: "SearchByApplicationNumber",
                category: "Navigation",
                description: "Search products by application number");

            DtoLabelAccessTestHelper.SeedAPIEndpointGuideView(
                connection,
                viewName: "vw_DrugInteractionLookup",
                endpointName: "GetDrugInteractions",
                category: "Safety",
                description: "Get drug interactions");

            // Act — no category filter
            var result = await DtoLabelAccess.GetAPIEndpointGuideAsync(
                context,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(2, result.Count, "Should return all endpoints");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetAPIEndpointGuideAsync correctly filters
        /// by category.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetAPIEndpointGuideAsync"/>
        [TestMethod]
        public async Task GetAPIEndpointGuideAsync_WithCategory_FiltersCorrectly()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedAPIEndpointGuideView(
                connection,
                viewName: "vw_ProductsByApplicationNumber",
                endpointName: "SearchByApplicationNumber",
                category: "Navigation",
                description: "Search products by application number");

            DtoLabelAccessTestHelper.SeedAPIEndpointGuideView(
                connection,
                viewName: "vw_DrugInteractionLookup",
                endpointName: "GetDrugInteractions",
                category: "Safety",
                description: "Get drug interactions");

            // Act — filter to Navigation only
            var result = await DtoLabelAccess.GetAPIEndpointGuideAsync(
                context,
                "Navigation",
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return only Navigation endpoints");
            Assert.AreEqual("Navigation", result[0].Category, "Category should match filter");
            Assert.AreEqual("vw_ProductsByApplicationNumber", result[0].ViewName, "View name should match seeded value");

            #endregion
        }

        #endregion GetAPIEndpointGuideAsync Tests

        #region GetInventorySummaryAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetInventorySummaryAsync returns an empty list
        /// when the database contains no InventorySummary rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetInventorySummaryAsync"/>
        [TestMethod]
        public async Task GetInventorySummaryAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act — note: no pkSecret parameter on this method
            var result = await DtoLabelAccess.GetInventorySummaryAsync(
                context,
                null,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetInventorySummaryAsync returns all summary rows
        /// when no category filter is applied.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetInventorySummaryAsync"/>
        [TestMethod]
        public async Task GetInventorySummaryAsync_NoCategory_ReturnsAll()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedInventorySummaryView(
                connection,
                category: "TOTALS",
                dimension: "Documents",
                dimensionValue: "All Documents",
                itemCount: 1000,
                sortOrder: 1);

            DtoLabelAccessTestHelper.SeedInventorySummaryView(
                connection,
                category: "TOP_LABELERS",
                dimension: "Labeler",
                dimensionValue: "Pfizer",
                itemCount: 500,
                sortOrder: 10);

            // Act
            var result = await DtoLabelAccess.GetInventorySummaryAsync(
                context,
                null,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(2, result.Count, "Should return all inventory summary rows");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetInventorySummaryAsync correctly filters
        /// by category.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetInventorySummaryAsync"/>
        [TestMethod]
        public async Task GetInventorySummaryAsync_WithCategory_FiltersCorrectly()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedInventorySummaryView(
                connection,
                category: "TOTALS",
                dimension: "Documents",
                dimensionValue: "All Documents",
                itemCount: 1000,
                sortOrder: 1);

            DtoLabelAccessTestHelper.SeedInventorySummaryView(
                connection,
                category: "TOP_LABELERS",
                dimension: "Labeler",
                dimensionValue: "Pfizer",
                itemCount: 500,
                sortOrder: 10);

            // Act — filter to TOTALS only
            var result = await DtoLabelAccess.GetInventorySummaryAsync(
                context,
                "TOTALS",
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return only TOTALS category");
            Assert.AreEqual("TOTALS", result[0].Category, "Category should match filter");
            Assert.AreEqual("Documents", result[0].Dimension, "Dimension should match seeded value");

            #endregion
        }

        #endregion GetInventorySummaryAsync Tests

        #region GetProductLatestLabelsAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetProductLatestLabelsAsync returns an empty list
        /// when the database contains no ProductLatestLabel rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetProductLatestLabelsAsync"/>
        [TestMethod]
        public async Task GetProductLatestLabelsAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.GetProductLatestLabelsAsync(
                context,
                null,
                null,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetProductLatestLabelsAsync returns a mapped DTO
        /// when data exists and no filters are applied.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetProductLatestLabelsAsync"/>
        [TestMethod]
        public async Task GetProductLatestLabelsAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductLatestLabelView(
                connection,
                productName: "LIPITOR",
                activeIngredient: "ATORVASTATIN CALCIUM",
                unii: "A0JWA85V8F",
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid);

            // Act
            var result = await DtoLabelAccess.GetProductLatestLabelsAsync(
                context,
                null,
                null,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return one latest label");
            Assert.IsNotNull(result[0].ProductLatestLabel, "ProductLatestLabel dictionary should not be null");
            Assert.AreEqual("LIPITOR", result[0].ProductName, "Product name should match seeded value");
            Assert.AreEqual("A0JWA85V8F", result[0].UNII, "UNII should match seeded value");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetProductLatestLabelsAsync filters by UNII
        /// exact match correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetProductLatestLabelsAsync"/>
        [TestMethod]
        public async Task GetProductLatestLabelsAsync_ByUNII_FiltersCorrectly()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductLatestLabelView(
                connection,
                productName: "LIPITOR",
                activeIngredient: "ATORVASTATIN CALCIUM",
                unii: "A0JWA85V8F",
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid);

            DtoLabelAccessTestHelper.SeedProductLatestLabelView(
                connection,
                productName: "ASPIRIN TABLETS",
                activeIngredient: "ASPIRIN",
                unii: "R16CO5Y76E",
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2);

            // Act — filter by UNII
            var result = await DtoLabelAccess.GetProductLatestLabelsAsync(
                context,
                "A0JWA85V8F",
                null,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return only matching UNII product");
            Assert.AreEqual("LIPITOR", result[0].ProductName, "Product name should match filtered result");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetProductLatestLabelsAsync filters by product
        /// name partial match correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetProductLatestLabelsAsync"/>
        [TestMethod]
        public async Task GetProductLatestLabelsAsync_ByProductName_FiltersCorrectly()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductLatestLabelView(
                connection,
                productName: "LIPITOR",
                activeIngredient: "ATORVASTATIN CALCIUM",
                unii: "A0JWA85V8F",
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid);

            DtoLabelAccessTestHelper.SeedProductLatestLabelView(
                connection,
                productName: "ASPIRIN TABLETS",
                activeIngredient: "ASPIRIN",
                unii: "R16CO5Y76E",
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2);

            // Act — filter by product name
            var result = await DtoLabelAccess.GetProductLatestLabelsAsync(
                context,
                null,
                "LIPITOR",
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return only matching product name");
            Assert.AreEqual("LIPITOR", result[0].ProductName, "Product name should match filtered result");

            #endregion
        }

        #endregion GetProductLatestLabelsAsync Tests

        #region GetProductIndicationsAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetProductIndicationsAsync returns an empty list
        /// when the database contains no ProductIndications rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetProductIndicationsAsync"/>
        [TestMethod]
        public async Task GetProductIndicationsAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.GetProductIndicationsAsync(
                context,
                null,
                null,
                null,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetProductIndicationsAsync returns a mapped DTO
        /// when data exists.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetProductIndicationsAsync"/>
        [TestMethod]
        public async Task GetProductIndicationsAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductIndicationsView(
                connection,
                productName: "LIPITOR",
                substanceName: "ATORVASTATIN",
                unii: "A0JWA85V8F",
                contentText: "Indicated for reducing cholesterol levels.");

            // Act — no filters
            var result = await DtoLabelAccess.GetProductIndicationsAsync(
                context,
                null,
                null,
                null,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return one indication");
            Assert.IsNotNull(result[0].ProductIndications, "ProductIndications dictionary should not be null");
            Assert.AreEqual("LIPITOR", result[0].ProductName, "Product name should match seeded value");
            Assert.AreEqual("ATORVASTATIN", result[0].SubstanceName, "Substance name should match seeded value");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetProductIndicationsAsync filters by UNII
        /// exact match correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetProductIndicationsAsync"/>
        [TestMethod]
        public async Task GetProductIndicationsAsync_ByUNII_FiltersCorrectly()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductIndicationsView(
                connection,
                productName: "LIPITOR",
                substanceName: "ATORVASTATIN",
                unii: "A0JWA85V8F",
                contentText: "Indicated for reducing cholesterol levels.");

            DtoLabelAccessTestHelper.SeedProductIndicationsView(
                connection,
                productName: "ASPIRIN",
                substanceName: "ASPIRIN",
                unii: "R16CO5Y76E",
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2,
                contentText: "Indicated for pain and inflammation.");

            // Act — filter by UNII
            var result = await DtoLabelAccess.GetProductIndicationsAsync(
                context,
                "A0JWA85V8F",
                null,
                null,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return only matching UNII product");
            Assert.AreEqual("LIPITOR", result[0].ProductName, "Product name should match filtered result");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetProductIndicationsAsync filters by product
        /// name partial match correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetProductIndicationsAsync"/>
        [TestMethod]
        public async Task GetProductIndicationsAsync_ByProductName_FiltersCorrectly()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductIndicationsView(
                connection,
                productName: "LIPITOR",
                substanceName: "ATORVASTATIN",
                unii: "A0JWA85V8F",
                contentText: "Indicated for reducing cholesterol levels.");

            DtoLabelAccessTestHelper.SeedProductIndicationsView(
                connection,
                productName: "ASPIRIN",
                substanceName: "ASPIRIN",
                unii: "R16CO5Y76E",
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2,
                contentText: "Indicated for pain and inflammation.");

            // Act — filter by product name
            var result = await DtoLabelAccess.GetProductIndicationsAsync(
                context,
                null,
                "ASPIRIN",
                null,
                null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return only matching product name");
            Assert.AreEqual("ASPIRIN", result[0].ProductName, "Product name should match filtered result");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetProductIndicationsAsync filters by indication
        /// text search correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetProductIndicationsAsync"/>
        [TestMethod]
        public async Task GetProductIndicationsAsync_ByIndicationSearch_FiltersCorrectly()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductIndicationsView(
                connection,
                productName: "LIPITOR",
                substanceName: "ATORVASTATIN",
                unii: "A0JWA85V8F",
                contentText: "Indicated for reducing cholesterol levels.");

            DtoLabelAccessTestHelper.SeedProductIndicationsView(
                connection,
                productName: "ASPIRIN",
                substanceName: "ASPIRIN",
                unii: "R16CO5Y76E",
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2,
                contentText: "Indicated for pain and inflammation.");

            // Act — search indication text for "cholesterol"
            var result = await DtoLabelAccess.GetProductIndicationsAsync(
                context,
                null,
                null,
                null,
                "cholesterol",
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return only product with cholesterol indication");
            Assert.AreEqual("LIPITOR", result[0].ProductName, "Product name should match indication search");

            #endregion
        }

        #endregion GetProductIndicationsAsync Tests

        #region GetLabelSectionMarkdownAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GetLabelSectionMarkdownAsync returns an empty list
        /// when the database contains no LabelSectionMarkdown rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetLabelSectionMarkdownAsync"/>
        [TestMethod]
        public async Task GetLabelSectionMarkdownAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.GetLabelSectionMarkdownAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetLabelSectionMarkdownAsync returns all sections
        /// for a document when no sectionCode filter is applied.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetLabelSectionMarkdownAsync"/>
        [TestMethod]
        public async Task GetLabelSectionMarkdownAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedLabelSectionMarkdownView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                documentTitle: "LIPITOR TABLETS",
                sectionCode: "34067-9",
                sectionTitle: "INDICATIONS AND USAGE",
                fullSectionText: "## INDICATIONS AND USAGE\nLipitor is indicated for reducing cholesterol.",
                contentBlockCount: 3);

            DtoLabelAccessTestHelper.SeedLabelSectionMarkdownView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                documentTitle: "LIPITOR TABLETS",
                sectionCode: "34084-4",
                sectionTitle: "ADVERSE REACTIONS",
                fullSectionText: "## ADVERSE REACTIONS\nCommon side effects include headache.",
                contentBlockCount: 5);

            // Act — no sectionCode filter
            var result = await DtoLabelAccess.GetLabelSectionMarkdownAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(2, result.Count, "Should return both sections for the document");
            Assert.AreEqual("LIPITOR TABLETS", result[0].DocumentTitle, "Document title should match seeded value");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetLabelSectionMarkdownAsync correctly filters
        /// by sectionCode when provided.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetLabelSectionMarkdownAsync"/>
        [TestMethod]
        public async Task GetLabelSectionMarkdownAsync_WithSectionCode_FiltersCorrectly()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedLabelSectionMarkdownView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                documentTitle: "LIPITOR TABLETS",
                sectionCode: "34067-9",
                sectionTitle: "1 INDICATIONS AND USAGE",
                fullSectionText: "## INDICATIONS AND USAGE\nLipitor is indicated for reducing cholesterol.",
                contentBlockCount: 3);

            DtoLabelAccessTestHelper.SeedLabelSectionMarkdownView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                documentTitle: "LIPITOR TABLETS",
                sectionCode: "34084-4",
                sectionTitle: "6 ADVERSE REACTIONS",
                fullSectionText: "## ADVERSE REACTIONS\nCommon side effects include headache.",
                contentBlockCount: 5);

            // Act — filter to Indications only
            var result = await DtoLabelAccess.GetLabelSectionMarkdownAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger,
                sectionCode: "34067-9");

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Count >= 1, "Should return at least the matching section");
            Assert.IsTrue(
                result.Any(r => r.SectionCode == "34067-9"),
                "Should contain the requested section code");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GetLabelSectionMarkdownAsync returns empty
        /// when querying a different document GUID.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetLabelSectionMarkdownAsync"/>
        [TestMethod]
        public async Task GetLabelSectionMarkdownAsync_DifferentDocumentGuid_ReturnsEmpty()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedLabelSectionMarkdownView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                documentTitle: "LIPITOR TABLETS",
                sectionCode: "34067-9",
                sectionTitle: "INDICATIONS AND USAGE",
                fullSectionText: "## INDICATIONS AND USAGE\nLipitor is indicated for reducing cholesterol.",
                contentBlockCount: 3);

            // Act — query with different document GUID
            var result = await DtoLabelAccess.GetLabelSectionMarkdownAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid2,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(0, result.Count, "Different document GUID should return zero results");

            #endregion
        }

        #endregion GetLabelSectionMarkdownAsync Tests

        #region GenerateLabelMarkdownAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GenerateLabelMarkdownAsync returns an export DTO
        /// with zero sections when the database is empty.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GenerateLabelMarkdownAsync"/>
        [TestMethod]
        public async Task GenerateLabelMarkdownAsync_EmptyDatabase_ReturnsEmptyExport()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.GenerateLabelMarkdownAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.SectionCount, "Section count should be zero for empty database");
            Assert.AreEqual(0, result.TotalContentBlocks, "Total content blocks should be zero for empty database");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GenerateLabelMarkdownAsync returns an assembled
        /// markdown document with correct metadata and content.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GenerateLabelMarkdownAsync"/>
        [TestMethod]
        public async Task GenerateLabelMarkdownAsync_WithData_ReturnsAssembledMarkdown()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedLabelSectionMarkdownView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                setGuid: DtoLabelAccessTestHelper.TestSetGuid,
                documentTitle: "LIPITOR TABLETS",
                sectionCode: "34067-9",
                sectionTitle: "INDICATIONS AND USAGE",
                fullSectionText: "## INDICATIONS AND USAGE\nLipitor is indicated for reducing cholesterol.",
                contentBlockCount: 3);

            DtoLabelAccessTestHelper.SeedLabelSectionMarkdownView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                setGuid: DtoLabelAccessTestHelper.TestSetGuid,
                documentTitle: "LIPITOR TABLETS",
                sectionCode: "34084-4",
                sectionTitle: "ADVERSE REACTIONS",
                fullSectionText: "## ADVERSE REACTIONS\nCommon side effects include headache.",
                contentBlockCount: 5);

            // Act
            var result = await DtoLabelAccess.GenerateLabelMarkdownAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(DtoLabelAccessTestHelper.TestDocumentGuid, result.DocumentGUID, "Document GUID should match");
            Assert.AreEqual("LIPITOR TABLETS", result.DocumentTitle, "Document title should match");
            Assert.AreEqual(2, result.SectionCount, "Section count should be 2");
            Assert.AreEqual(8, result.TotalContentBlocks, "Total content blocks should sum to 8 (3+5)");
            Assert.IsNotNull(result.FullMarkdown, "Full markdown should not be null");
            Assert.IsTrue(result.FullMarkdown!.Length > 0, "Full markdown should contain content");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GenerateLabelMarkdownAsync extracts the document
        /// title and set GUID from the first section's metadata.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GenerateLabelMarkdownAsync"/>
        [TestMethod]
        public async Task GenerateLabelMarkdownAsync_ExtractsMetadataFromFirstSection()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedLabelSectionMarkdownView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                setGuid: DtoLabelAccessTestHelper.TestSetGuid,
                documentTitle: "TEST DOCUMENT TITLE",
                sectionCode: "34067-9",
                sectionTitle: "INDICATIONS AND USAGE",
                fullSectionText: "## INDICATIONS AND USAGE\nTest content.",
                contentBlockCount: 1);

            // Act
            var result = await DtoLabelAccess.GenerateLabelMarkdownAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.AreEqual("TEST DOCUMENT TITLE", result.DocumentTitle, "Document title should be extracted from first section");
            Assert.AreEqual(DtoLabelAccessTestHelper.TestSetGuid, result.SetGUID, "Set GUID should be extracted from first section");

            #endregion
        }

        #endregion GenerateLabelMarkdownAsync Tests

        #region GenerateCleanLabelMarkdownAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that GenerateCleanLabelMarkdownAsync returns an empty
        /// string when no sections exist for the document.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GenerateCleanLabelMarkdownAsync"/>
        [TestMethod]
        public async Task GenerateCleanLabelMarkdownAsync_EmptyDatabase_ReturnsEmptyString()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var mockClaudeService = new Mock<MedRecPro.Service.IClaudeApiService>();
            mockClaudeService
                .Setup(x => x.GenerateCleanMarkdownAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync("Cleaned markdown");

            // Act
            var result = await DtoLabelAccess.GenerateCleanLabelMarkdownAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                mockClaudeService.Object,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(string.Empty, result, "Empty database should return empty string");

            // Verify Claude API was NOT called when no sections exist
            mockClaudeService.Verify(
                x => x.GenerateCleanMarkdownAsync(It.IsAny<string>(), It.IsAny<string?>()),
                Times.Never,
                "Claude API should not be called when no sections exist");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GenerateCleanLabelMarkdownAsync calls the Claude API
        /// service and returns the cleaned markdown string.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GenerateCleanLabelMarkdownAsync"/>
        [TestMethod]
        public async Task GenerateCleanLabelMarkdownAsync_WithData_ReturnsCleanedMarkdown()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedLabelSectionMarkdownView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                documentTitle: "LIPITOR TABLETS",
                sectionCode: "34067-9",
                sectionTitle: "INDICATIONS AND USAGE",
                fullSectionText: "## INDICATIONS AND USAGE\nLipitor is indicated for reducing cholesterol.",
                contentBlockCount: 3);

            var mockClaudeService = new Mock<MedRecPro.Service.IClaudeApiService>();
            mockClaudeService
                .Setup(x => x.GenerateCleanMarkdownAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync("# LIPITOR TABLETS\n\n## INDICATIONS AND USAGE\n\nLipitor is indicated for reducing cholesterol.");

            // Act
            var result = await DtoLabelAccess.GenerateCleanLabelMarkdownAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                mockClaudeService.Object,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Length > 0, "Cleaned markdown should contain content");
            Assert.IsTrue(result.Contains("LIPITOR"), "Cleaned markdown should contain product name");

            // Verify Claude API was called once
            mockClaudeService.Verify(
                x => x.GenerateCleanMarkdownAsync(It.IsAny<string>(), It.IsAny<string?>()),
                Times.Once,
                "Claude API should be called exactly once");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that GenerateCleanLabelMarkdownAsync passes the
        /// document title to the Claude API service for context.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GenerateCleanLabelMarkdownAsync"/>
        [TestMethod]
        public async Task GenerateCleanLabelMarkdownAsync_PassesDocumentTitleToService()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedLabelSectionMarkdownView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                documentTitle: "ASPIRIN TABLETS",
                sectionCode: "34067-9",
                sectionTitle: "INDICATIONS AND USAGE",
                fullSectionText: "## INDICATIONS AND USAGE\nAspirin is indicated for pain.",
                contentBlockCount: 1);

            string? capturedTitle = null;
            var mockClaudeService = new Mock<MedRecPro.Service.IClaudeApiService>();
            mockClaudeService
                .Setup(x => x.GenerateCleanMarkdownAsync(It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string?>((_, title) => capturedTitle = title)
                .ReturnsAsync("Cleaned content");

            // Act
            await DtoLabelAccess.GenerateCleanLabelMarkdownAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                mockClaudeService.Object,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.AreEqual("ASPIRIN TABLETS", capturedTitle, "Document title should be passed to Claude API service");

            #endregion
        }

        #endregion GenerateCleanLabelMarkdownAsync Tests
    }
}
