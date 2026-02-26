using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Tests for the View Navigation methods (#4-#22) of <see cref="DtoLabelAccess"/>.
    /// All methods query LabelView.* entities backed by SQLite tables in tests.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="DtoLabelAccessTestHelper"/> for shared SQLite database setup,
    /// view backing table creation, entity seeding, and cache management.
    ///
    /// Test naming convention: {MethodName}_{Condition}_{Expected}
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="DtoLabelAccessTestHelper"/>
    /// <seealso cref="LabelView"/>
    [TestClass]
    public class DtoLabelAccessViewNavigationTests
    {
        #region Test Constants

        /**************************************************************/
        /// <summary>
        /// Shorthand reference to the test PK secret from the helper.
        /// </summary>
        private static readonly string PkSecret = DtoLabelAccessTestHelper.TestPkSecret;

        #endregion Test Constants

        #region Test Initialization

        /**************************************************************/
        /// <summary>
        /// Clears the PerformanceHelper managed cache before each test to prevent cross-test pollution.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation

            DtoLabelAccessTestHelper.ClearCache();

            #endregion
        }

        #endregion Test Initialization

        #region SearchByApplicationNumberAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByApplicationNumberAsync"/>
        [TestMethod]
        public async Task SearchByApplicationNumberAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.SearchByApplicationNumberAsync(
                context, "NDA014526", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByApplicationNumberAsync"/>
        [TestMethod]
        public async Task SearchByApplicationNumberAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByApplicationNumberView(
                connection, "NDA014526", "ASPIRIN", productId: 1);

            var result = await DtoLabelAccess.SearchByApplicationNumberAsync(
                context, "NDA014526", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("NDA014526", result[0].ApplicationNumber);
            Assert.AreEqual("ASPIRIN", result[0].ProductName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that numeric-only input matches application numbers containing that numeric part.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByApplicationNumberAsync"/>
        [TestMethod]
        public async Task SearchByApplicationNumberAsync_NumericOnlySearch_MatchesContainingNumber()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByApplicationNumberView(
                connection, "NDA014526", "ASPIRIN", productId: 1);

            var result = await DtoLabelAccess.SearchByApplicationNumberAsync(
                context, "014526", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0, "Numeric-only search should match application numbers containing the number.");
            Assert.AreEqual("NDA014526", result[0].ApplicationNumber);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a non-matching application number returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByApplicationNumberAsync"/>
        [TestMethod]
        public async Task SearchByApplicationNumberAsync_NoMatch_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByApplicationNumberView(
                connection, "NDA014526", "ASPIRIN", productId: 1);

            var result = await DtoLabelAccess.SearchByApplicationNumberAsync(
                context, "BLA999999", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a partial/contains match works for application number search.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByApplicationNumberAsync"/>
        [TestMethod]
        public async Task SearchByApplicationNumberAsync_PartialMatch_ReturnsResults()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByApplicationNumberView(
                connection, "ANDA125669", "IBUPROFEN", productId: 1);

            var result = await DtoLabelAccess.SearchByApplicationNumberAsync(
                context, "ANDA", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0, "Prefix-only search should match application numbers starting with the prefix.");

            #endregion
        }

        #endregion SearchByApplicationNumberAsync Tests

        #region GetApplicationNumberSummariesAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetApplicationNumberSummariesAsync"/>
        [TestMethod]
        public async Task GetApplicationNumberSummariesAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.GetApplicationNumberSummariesAsync(
                context, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded summary data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetApplicationNumberSummariesAsync"/>
        [TestMethod]
        public async Task GetApplicationNumberSummariesAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedApplicationNumberSummaryView(
                connection, "NDA014526", "NDA", "New Drug Application", productCount: 5, documentCount: 3);

            var result = await DtoLabelAccess.GetApplicationNumberSummariesAsync(
                context, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("NDA014526", result[0].ApplicationNumber);
            Assert.AreEqual("NDA", result[0].MarketingCategoryCode);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the marketingCategory filter narrows results correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetApplicationNumberSummariesAsync"/>
        [TestMethod]
        public async Task GetApplicationNumberSummariesAsync_WithMarketingCategoryFilter_FiltersResults()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedApplicationNumberSummaryView(
                connection, "NDA014526", "NDA", "New Drug Application");
            DtoLabelAccessTestHelper.SeedApplicationNumberSummaryView(
                connection, "ANDA125669", "ANDA", "Abbreviated New Drug Application");

            var result = await DtoLabelAccess.GetApplicationNumberSummariesAsync(
                context, "NDA", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.IsTrue(result.All(r => r.MarketingCategoryCode == "NDA"),
                "All results should be filtered to NDA marketing category.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that null filter returns all summaries.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetApplicationNumberSummariesAsync"/>
        [TestMethod]
        public async Task GetApplicationNumberSummariesAsync_NoFilter_ReturnsAll()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedApplicationNumberSummaryView(
                connection, "NDA014526", "NDA", "New Drug Application");
            DtoLabelAccessTestHelper.SeedApplicationNumberSummaryView(
                connection, "ANDA125669", "ANDA", "Abbreviated New Drug Application");

            var result = await DtoLabelAccess.GetApplicationNumberSummariesAsync(
                context, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);

            #endregion
        }

        #endregion GetApplicationNumberSummariesAsync Tests

        #region SearchByPharmacologicClassAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByPharmacologicClassAsync"/>
        [TestMethod]
        public async Task SearchByPharmacologicClassAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.SearchByPharmacologicClassAsync(
                context, "Cyclooxygenase", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByPharmacologicClassAsync"/>
        [TestMethod]
        public async Task SearchByPharmacologicClassAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByPharmacologicClassView(
                connection, "Cyclooxygenase Inhibitors", productId: 1, productName: "ASPIRIN");

            var result = await DtoLabelAccess.SearchByPharmacologicClassAsync(
                context, "Cyclooxygenase Inhibitors", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("Cyclooxygenase Inhibitors", result[0].PharmClassName);
            Assert.AreEqual("ASPIRIN", result[0].ProductName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that partial match (PartialMatchAny) works for pharmacologic class search.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByPharmacologicClassAsync"/>
        [TestMethod]
        public async Task SearchByPharmacologicClassAsync_PartialMatch_ReturnsResults()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByPharmacologicClassView(
                connection, "Cyclooxygenase Inhibitors", productId: 1, productName: "ASPIRIN");

            var result = await DtoLabelAccess.SearchByPharmacologicClassAsync(
                context, "Cyclooxygenase", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0, "Partial match search should return results containing the search term.");

            #endregion
        }

        #endregion SearchByPharmacologicClassAsync Tests

        #region SearchByPharmacologicClassExactAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByPharmacologicClassExactAsync"/>
        [TestMethod]
        public async Task SearchByPharmacologicClassExactAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.SearchByPharmacologicClassExactAsync(
                context, "Cyclooxygenase Inhibitors", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that exact match returns results.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByPharmacologicClassExactAsync"/>
        [TestMethod]
        public async Task SearchByPharmacologicClassExactAsync_ExactMatch_ReturnsResults()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByPharmacologicClassView(
                connection, "Cyclooxygenase Inhibitors", productId: 1, productName: "ASPIRIN");

            var result = await DtoLabelAccess.SearchByPharmacologicClassExactAsync(
                context, "Cyclooxygenase Inhibitors", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("Cyclooxygenase Inhibitors", result[0].PharmClassName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a partial search does NOT return results for exact match method.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByPharmacologicClassExactAsync"/>
        [TestMethod]
        public async Task SearchByPharmacologicClassExactAsync_PartialMatch_ReturnsEmpty()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByPharmacologicClassView(
                connection, "Cyclooxygenase Inhibitors", productId: 1, productName: "ASPIRIN");

            var result = await DtoLabelAccess.SearchByPharmacologicClassExactAsync(
                context, "Cyclooxygenase", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count, "Exact match should not return partial matches.");

            #endregion
        }

        #endregion SearchByPharmacologicClassExactAsync Tests

        #region GetPharmacologicClassHierarchyAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetPharmacologicClassHierarchyAsync"/>
        [TestMethod]
        public async Task GetPharmacologicClassHierarchyAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.GetPharmacologicClassHierarchyAsync(
                context, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded hierarchy data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetPharmacologicClassHierarchyAsync"/>
        [TestMethod]
        public async Task GetPharmacologicClassHierarchyAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedPharmacologicClassHierarchyView(
                connection, "Cyclooxygenase Inhibitors", "Anti-Inflammatory Agents");

            var result = await DtoLabelAccess.GetPharmacologicClassHierarchyAsync(
                context, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("Cyclooxygenase Inhibitors", result[0].ChildClassName);
            Assert.AreEqual("Anti-Inflammatory Agents", result[0].ParentClassName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that multiple hierarchy rows are returned with correct ordering.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetPharmacologicClassHierarchyAsync"/>
        [TestMethod]
        public async Task GetPharmacologicClassHierarchyAsync_MultipleRows_ReturnsAll()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedPharmacologicClassHierarchyView(
                connection, "Cyclooxygenase Inhibitors", "Anti-Inflammatory Agents", childClassId: 1, parentClassId: 2);
            DtoLabelAccessTestHelper.SeedPharmacologicClassHierarchyView(
                connection, "Beta-Adrenergic Blockers", "Anti-Inflammatory Agents", childClassId: 3, parentClassId: 2);

            var result = await DtoLabelAccess.GetPharmacologicClassHierarchyAsync(
                context, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);

            #endregion
        }

        #endregion GetPharmacologicClassHierarchyAsync Tests

        #region GetPharmacologicClassSummariesAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetPharmacologicClassSummariesAsync"/>
        [TestMethod]
        public async Task GetPharmacologicClassSummariesAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.GetPharmacologicClassSummariesAsync(
                context, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded summary data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetPharmacologicClassSummariesAsync"/>
        [TestMethod]
        public async Task GetPharmacologicClassSummariesAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedPharmacologicClassSummaryView(
                connection, "Cyclooxygenase Inhibitors", productCount: 10);

            var result = await DtoLabelAccess.GetPharmacologicClassSummariesAsync(
                context, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("Cyclooxygenase Inhibitors", result[0].PharmClassName);
            Assert.AreEqual(10, result[0].ProductCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that results are ordered by ProductCount descending.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetPharmacologicClassSummariesAsync"/>
        [TestMethod]
        public async Task GetPharmacologicClassSummariesAsync_MultipleRows_OrderedByProductCountDesc()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedPharmacologicClassSummaryView(
                connection, "Small Class", productCount: 2);
            DtoLabelAccessTestHelper.SeedPharmacologicClassSummaryView(
                connection, "Large Class", productCount: 50);

            var result = await DtoLabelAccess.GetPharmacologicClassSummariesAsync(
                context, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result[0].ProductCount >= result[1].ProductCount,
                "Results should be ordered by ProductCount descending.");

            #endregion
        }

        #endregion GetPharmacologicClassSummariesAsync Tests

        #region GetIngredientActiveSummariesAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetIngredientActiveSummariesAsync"/>
        [TestMethod]
        public async Task GetIngredientActiveSummariesAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.GetIngredientActiveSummariesAsync(
                context, null, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetIngredientActiveSummariesAsync"/>
        [TestMethod]
        public async Task GetIngredientActiveSummariesAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedIngredientActiveSummaryView(
                connection, ingredientSubstanceId: 1, substanceName: "ASPIRIN", unii: "R16CO5Y76E", productCount: 10);

            var result = await DtoLabelAccess.GetIngredientActiveSummariesAsync(
                context, null, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("ASPIRIN", result[0].SubstanceName);
            Assert.AreEqual("R16CO5Y76E", result[0].UNII);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the minProductCount filter works correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetIngredientActiveSummariesAsync"/>
        [TestMethod]
        public async Task GetIngredientActiveSummariesAsync_MinProductCountFilter_FiltersCorrectly()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedIngredientActiveSummaryView(
                connection, ingredientSubstanceId: 1, substanceName: "ASPIRIN", unii: "R16CO5Y76E", productCount: 10);
            DtoLabelAccessTestHelper.SeedIngredientActiveSummaryView(
                connection, ingredientSubstanceId: 2, substanceName: "RARE DRUG", unii: "XXXXXXXXXX", productCount: 2);

            var result = await DtoLabelAccess.GetIngredientActiveSummariesAsync(
                context, minProductCount: 5, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.All(r => r.ProductCount >= 5),
                "All results should have ProductCount >= 5.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the ingredient name filter works correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetIngredientActiveSummariesAsync"/>
        [TestMethod]
        public async Task GetIngredientActiveSummariesAsync_IngredientNameFilter_FiltersCorrectly()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedIngredientActiveSummaryView(
                connection, ingredientSubstanceId: 1, substanceName: "ASPIRIN", unii: "R16CO5Y76E", productCount: 10);
            DtoLabelAccessTestHelper.SeedIngredientActiveSummaryView(
                connection, ingredientSubstanceId: 2, substanceName: "IBUPROFEN", unii: "WK2XYI10QM", productCount: 8);

            var result = await DtoLabelAccess.GetIngredientActiveSummariesAsync(
                context, null, "ASPIRIN", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.IsTrue(result.All(r => r.SubstanceName != null && r.SubstanceName.Contains("ASPIRIN")),
                "All results should contain the ingredient name filter.");

            #endregion
        }

        #endregion GetIngredientActiveSummariesAsync Tests

        #region GetIngredientInactiveSummariesAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetIngredientInactiveSummariesAsync"/>
        [TestMethod]
        public async Task GetIngredientInactiveSummariesAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.GetIngredientInactiveSummariesAsync(
                context, null, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetIngredientInactiveSummariesAsync"/>
        [TestMethod]
        public async Task GetIngredientInactiveSummariesAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedIngredientInactiveSummaryView(
                connection, ingredientSubstanceId: 1, substanceName: "STARCH", unii: "O8232NY3SJ", productCount: 5);

            var result = await DtoLabelAccess.GetIngredientInactiveSummariesAsync(
                context, null, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("STARCH", result[0].SubstanceName);
            Assert.AreEqual("O8232NY3SJ", result[0].UNII);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the minProductCount filter works for inactive summaries.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetIngredientInactiveSummariesAsync"/>
        [TestMethod]
        public async Task GetIngredientInactiveSummariesAsync_MinProductCountFilter_FiltersCorrectly()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedIngredientInactiveSummaryView(
                connection, ingredientSubstanceId: 1, substanceName: "STARCH", unii: "O8232NY3SJ", productCount: 20);
            DtoLabelAccessTestHelper.SeedIngredientInactiveSummaryView(
                connection, ingredientSubstanceId: 2, substanceName: "RARE EXCIPIENT", unii: "YYYYYYYYYY", productCount: 1);

            var result = await DtoLabelAccess.GetIngredientInactiveSummariesAsync(
                context, minProductCount: 10, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.All(r => r.ProductCount >= 10),
                "All results should have ProductCount >= 10.");

            #endregion
        }

        #endregion GetIngredientInactiveSummariesAsync Tests

        #region SearchByIngredientAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByIngredientAsync"/>
        [TestMethod]
        public async Task SearchByIngredientAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.SearchByIngredientAsync(
                context, "R16CO5Y76E", null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that UNII exact match returns the correct product.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByIngredientAsync"/>
        [TestMethod]
        public async Task SearchByIngredientAsync_UNIIExactMatch_ReturnsResults()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByIngredientView(
                connection, "ASPIRIN", "R16CO5Y76E", productId: 1, productName: "ASPIRIN");

            var result = await DtoLabelAccess.SearchByIngredientAsync(
                context, "R16CO5Y76E", null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("R16CO5Y76E", result[0].UNII);
            Assert.AreEqual("ASPIRIN", result[0].SubstanceName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that substance name search returns matching products.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByIngredientAsync"/>
        [TestMethod]
        public async Task SearchByIngredientAsync_SubstanceNameSearch_ReturnsResults()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByIngredientView(
                connection, "ASPIRIN", "R16CO5Y76E", productId: 1, productName: "ASPIRIN");

            var result = await DtoLabelAccess.SearchByIngredientAsync(
                context, null, "ASPIRIN", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("ASPIRIN", result[0].SubstanceName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a non-matching UNII returns no results.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByIngredientAsync"/>
        [TestMethod]
        public async Task SearchByIngredientAsync_NoMatch_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByIngredientView(
                connection, "ASPIRIN", "R16CO5Y76E", productId: 1, productName: "ASPIRIN");

            var result = await DtoLabelAccess.SearchByIngredientAsync(
                context, "ZZZZZZZZZZ", null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        #endregion SearchByIngredientAsync Tests

        #region GetIngredientSummariesAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetIngredientSummariesAsync"/>
        [TestMethod]
        public async Task GetIngredientSummariesAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.GetIngredientSummariesAsync(
                context, null, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetIngredientSummariesAsync"/>
        [TestMethod]
        public async Task GetIngredientSummariesAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedIngredientSummaryView(
                connection, "ASPIRIN", "R16CO5Y76E", productCount: 10);

            var result = await DtoLabelAccess.GetIngredientSummariesAsync(
                context, null, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("ASPIRIN", result[0].SubstanceName);
            Assert.AreEqual("R16CO5Y76E", result[0].UNII);
            Assert.AreEqual(10, result[0].ProductCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the ingredient name filter narrows results.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetIngredientSummariesAsync"/>
        [TestMethod]
        public async Task GetIngredientSummariesAsync_WithIngredientFilter_FiltersCorrectly()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedIngredientSummaryView(
                connection, "ASPIRIN", "R16CO5Y76E", productCount: 10);
            DtoLabelAccessTestHelper.SeedIngredientSummaryView(
                connection, "IBUPROFEN", "WK2XYI10QM", productCount: 8);

            var result = await DtoLabelAccess.GetIngredientSummariesAsync(
                context, null, "ASPIRIN", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.IsTrue(result.All(r => r.SubstanceName != null && r.SubstanceName.Contains("ASPIRIN")));

            #endregion
        }

        #endregion GetIngredientSummariesAsync Tests

        #region SearchIngredientsAdvancedAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchIngredientsAdvancedAsync"/>
        [TestMethod]
        public async Task SearchIngredientsAdvancedAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.SearchIngredientsAdvancedAsync(
                context, "R16CO5Y76E", null, null, null, null, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded ingredient view data is returned by UNII search.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchIngredientsAdvancedAsync"/>
        [TestMethod]
        public async Task SearchIngredientsAdvancedAsync_WithUNII_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedIngredientView(
                connection, "ASPIRIN", "R16CO5Y76E", classCode: "ACTIB", productName: "ASPIRIN");

            var result = await DtoLabelAccess.SearchIngredientsAdvancedAsync(
                context, "R16CO5Y76E", null, null, null, null, null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("R16CO5Y76E", result[0].UNII);
            Assert.AreEqual("ASPIRIN", result[0].SubstanceName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that activeOnly=true filters to active ingredients only.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchIngredientsAdvancedAsync"/>
        [TestMethod]
        public async Task SearchIngredientsAdvancedAsync_ActiveOnly_FiltersCorrectly()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedActiveIngredientView(
                connection, "ASPIRIN", "R16CO5Y76E", productName: "ASPIRIN");

            var result = await DtoLabelAccess.SearchIngredientsAdvancedAsync(
                context, "R16CO5Y76E", null, null, null, null, activeOnly: true, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.IsTrue(result[0].IsActive, "Result should be an active ingredient.");

            #endregion
        }

        #endregion SearchIngredientsAdvancedAsync Tests

        #region FindProductsByApplicationNumberWithSameIngredientAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.FindProductsByApplicationNumberWithSameIngredientAsync"/>
        [TestMethod]
        public async Task FindProductsByApplicationNumberWithSameIngredientAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.FindProductsByApplicationNumberWithSameIngredientAsync(
                context, "NDA014526", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded active ingredients with matching application number are returned.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.FindProductsByApplicationNumberWithSameIngredientAsync"/>
        [TestMethod]
        public async Task FindProductsByApplicationNumberWithSameIngredientAsync_WithData_ReturnsResults()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Seed active ingredient for the target application number
            DtoLabelAccessTestHelper.SeedActiveIngredientView(
                connection, "ASPIRIN", "R16CO5Y76E", productName: "ASPIRIN", applicationNumber: "NDA014526");

            // Seed another active ingredient with same UNII but different application number
            DtoLabelAccessTestHelper.SeedActiveIngredientView(
                connection, "ASPIRIN", "R16CO5Y76E", productName: "GENERIC ASPIRIN",
                applicationNumber: "ANDA125669",
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2);

            var result = await DtoLabelAccess.FindProductsByApplicationNumberWithSameIngredientAsync(
                context, "NDA014526", PkSecret, logger);

            Assert.IsNotNull(result);
            // Should find products sharing the same ingredient UNII
            Assert.IsTrue(result.Count > 0, "Should find products with the same ingredient.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a non-matching application number returns empty.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.FindProductsByApplicationNumberWithSameIngredientAsync"/>
        [TestMethod]
        public async Task FindProductsByApplicationNumberWithSameIngredientAsync_NoMatch_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedActiveIngredientView(
                connection, "ASPIRIN", "R16CO5Y76E", productName: "ASPIRIN", applicationNumber: "NDA014526");

            var result = await DtoLabelAccess.FindProductsByApplicationNumberWithSameIngredientAsync(
                context, "BLA999999", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        #endregion FindProductsByApplicationNumberWithSameIngredientAsync Tests

        #region FindRelatedIngredientsAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty result DTO.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.FindRelatedIngredientsAsync"/>
        [TestMethod]
        public async Task FindRelatedIngredientsAsync_EmptyDatabase_ReturnsEmptyResult()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.FindRelatedIngredientsAsync(
                context, "R16CO5Y76E", null, isSearchingActive: true, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.SearchedIngredients.Count);
            Assert.AreEqual(0, result.TotalProductCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded active ingredient data is returned in the composite DTO.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.FindRelatedIngredientsAsync"/>
        [TestMethod]
        public async Task FindRelatedIngredientsAsync_WithActiveIngredient_ReturnsSearchedIngredients()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedActiveIngredientView(
                connection, "ASPIRIN", "R16CO5Y76E", productName: "ASPIRIN", applicationNumber: "NDA014526");

            var result = await DtoLabelAccess.FindRelatedIngredientsAsync(
                context, "R16CO5Y76E", null, isSearchingActive: true, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.SearchedIngredients.Count > 0, "Should find the searched active ingredient.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that searching for inactive ingredients works.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.FindRelatedIngredientsAsync"/>
        [TestMethod]
        public async Task FindRelatedIngredientsAsync_WithInactiveIngredient_ReturnsSearchedIngredients()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedInactiveIngredientView(
                connection, "STARCH", "O8232NY3SJ", productName: "ASPIRIN", applicationNumber: "NDA014526");

            var result = await DtoLabelAccess.FindRelatedIngredientsAsync(
                context, "O8232NY3SJ", null, isSearchingActive: false, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.SearchedIngredients.Count > 0, "Should find the searched inactive ingredient.");

            #endregion
        }

        #endregion FindRelatedIngredientsAsync Tests

        #region SearchByNDCAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByNDCAsync"/>
        [TestMethod]
        public async Task SearchByNDCAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.SearchByNDCAsync(
                context, "12345-678", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded NDC data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByNDCAsync"/>
        [TestMethod]
        public async Task SearchByNDCAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByNDCView(
                connection, "12345-678", "ASPIRIN", productId: 1);

            var result = await DtoLabelAccess.SearchByNDCAsync(
                context, "12345-678", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("12345-678", result[0].ProductCode);
            Assert.AreEqual("ASPIRIN", result[0].ProductName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that partial NDC match returns results.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByNDCAsync"/>
        [TestMethod]
        public async Task SearchByNDCAsync_PartialMatch_ReturnsResults()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByNDCView(
                connection, "12345-678", "ASPIRIN", productId: 1);

            var result = await DtoLabelAccess.SearchByNDCAsync(
                context, "12345", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0, "Partial NDC search should return results.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a non-matching NDC returns empty.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByNDCAsync"/>
        [TestMethod]
        public async Task SearchByNDCAsync_NoMatch_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByNDCView(
                connection, "12345-678", "ASPIRIN", productId: 1);

            var result = await DtoLabelAccess.SearchByNDCAsync(
                context, "99999-999", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        #endregion SearchByNDCAsync Tests

        #region SearchByPackageNDCAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByPackageNDCAsync"/>
        [TestMethod]
        public async Task SearchByPackageNDCAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.SearchByPackageNDCAsync(
                context, "12345-678-90", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded package NDC data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByPackageNDCAsync"/>
        [TestMethod]
        public async Task SearchByPackageNDCAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedPackageByNDCView(
                connection, "12345-678-90", "ASPIRIN", productId: 1);

            var result = await DtoLabelAccess.SearchByPackageNDCAsync(
                context, "12345-678-90", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("12345-678-90", result[0].PackageCode);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that partial package code match returns results.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByPackageNDCAsync"/>
        [TestMethod]
        public async Task SearchByPackageNDCAsync_PartialMatch_ReturnsResults()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedPackageByNDCView(
                connection, "12345-678-90", "ASPIRIN", productId: 1);

            var result = await DtoLabelAccess.SearchByPackageNDCAsync(
                context, "12345", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0, "Partial package NDC search should return results.");

            #endregion
        }

        #endregion SearchByPackageNDCAsync Tests

        #region SearchByLabelerAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByLabelerAsync"/>
        [TestMethod]
        public async Task SearchByLabelerAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.SearchByLabelerAsync(
                context, "Pfizer", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded labeler data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByLabelerAsync"/>
        [TestMethod]
        public async Task SearchByLabelerAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByLabelerView(
                connection, "TEST PHARMA INC", productId: 1, productName: "ASPIRIN");

            var result = await DtoLabelAccess.SearchByLabelerAsync(
                context, "TEST PHARMA", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("TEST PHARMA INC", result[0].LabelerName);
            Assert.AreEqual("ASPIRIN", result[0].ProductName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a non-matching labeler name returns empty.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchByLabelerAsync"/>
        [TestMethod]
        public async Task SearchByLabelerAsync_NoMatch_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedProductsByLabelerView(
                connection, "TEST PHARMA INC", productId: 1, productName: "ASPIRIN");

            var result = await DtoLabelAccess.SearchByLabelerAsync(
                context, "NONEXISTENT CORP", PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        #endregion SearchByLabelerAsync Tests

        #region GetLabelerSummariesAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetLabelerSummariesAsync"/>
        [TestMethod]
        public async Task GetLabelerSummariesAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.GetLabelerSummariesAsync(
                context, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded labeler summary data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetLabelerSummariesAsync"/>
        [TestMethod]
        public async Task GetLabelerSummariesAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedLabelerSummaryView(
                connection, "TEST PHARMA INC", productCount: 10, documentCount: 5);

            var result = await DtoLabelAccess.GetLabelerSummariesAsync(
                context, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("TEST PHARMA INC", result[0].LabelerName);
            Assert.AreEqual(10, result[0].ProductCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that results are ordered by ProductCount descending.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetLabelerSummariesAsync"/>
        [TestMethod]
        public async Task GetLabelerSummariesAsync_MultipleRows_OrderedByProductCountDesc()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedLabelerSummaryView(
                connection, "SMALL PHARMA", productCount: 2);
            DtoLabelAccessTestHelper.SeedLabelerSummaryView(
                connection, "BIG PHARMA", productCount: 50);

            var result = await DtoLabelAccess.GetLabelerSummariesAsync(
                context, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result[0].ProductCount >= result[1].ProductCount,
                "Results should be ordered by ProductCount descending.");

            #endregion
        }

        #endregion GetLabelerSummariesAsync Tests

        #region GetDocumentNavigationAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDocumentNavigationAsync"/>
        [TestMethod]
        public async Task GetDocumentNavigationAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.GetDocumentNavigationAsync(
                context, latestOnly: false, setGuid: null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that seeded document navigation data is returned and mapped to the DTO correctly.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDocumentNavigationAsync"/>
        [TestMethod]
        public async Task GetDocumentNavigationAsync_WithData_ReturnsMappedDto()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedDocumentNavigationView(
                connection, documentId: 1, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                setGuid: DtoLabelAccessTestHelper.TestSetGuid, versionNumber: 1,
                documentTitle: "Test Document", isLatestVersion: 1);

            var result = await DtoLabelAccess.GetDocumentNavigationAsync(
                context, latestOnly: false, setGuid: null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual("Test Document", result[0].DocumentTitle);
            Assert.AreEqual(DtoLabelAccessTestHelper.TestDocumentGuid, result[0].DocumentGUID);
            Assert.AreEqual(DtoLabelAccessTestHelper.TestSetGuid, result[0].SetGUID);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that latestOnly=true filters to only the latest version documents.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDocumentNavigationAsync"/>
        [TestMethod]
        public async Task GetDocumentNavigationAsync_LatestOnly_FiltersToLatestVersion()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Seed a latest version document
            DtoLabelAccessTestHelper.SeedDocumentNavigationView(
                connection, documentId: 1, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                setGuid: DtoLabelAccessTestHelper.TestSetGuid, versionNumber: 2,
                documentTitle: "Latest Version", isLatestVersion: 1);

            // Seed an older version document
            DtoLabelAccessTestHelper.SeedDocumentNavigationView(
                connection, documentId: 2, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2,
                setGuid: DtoLabelAccessTestHelper.TestSetGuid, versionNumber: 1,
                documentTitle: "Old Version", isLatestVersion: 0);

            var result = await DtoLabelAccess.GetDocumentNavigationAsync(
                context, latestOnly: true, setGuid: null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.IsTrue(result.All(r => r.IsLatestVersion >= 1),
                "All results should be latest versions when latestOnly=true.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that the setGuid filter narrows results to a specific set.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDocumentNavigationAsync"/>
        [TestMethod]
        public async Task GetDocumentNavigationAsync_SetGuidFilter_FiltersToSpecificSet()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Seed document in set 1
            DtoLabelAccessTestHelper.SeedDocumentNavigationView(
                connection, documentId: 1, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                setGuid: DtoLabelAccessTestHelper.TestSetGuid, versionNumber: 1,
                documentTitle: "Set 1 Doc", isLatestVersion: 1);

            // Seed document in set 2
            DtoLabelAccessTestHelper.SeedDocumentNavigationView(
                connection, documentId: 2, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2,
                setGuid: DtoLabelAccessTestHelper.TestSetGuid2, versionNumber: 1,
                documentTitle: "Set 2 Doc", isLatestVersion: 1);

            var result = await DtoLabelAccess.GetDocumentNavigationAsync(
                context, latestOnly: false, setGuid: DtoLabelAccessTestHelper.TestSetGuid, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.IsTrue(result.All(r => r.SetGUID == DtoLabelAccessTestHelper.TestSetGuid),
                "All results should belong to the specified SetGUID.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that latestOnly=false returns all versions.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDocumentNavigationAsync"/>
        [TestMethod]
        public async Task GetDocumentNavigationAsync_NotLatestOnly_ReturnsAllVersions()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedDocumentNavigationView(
                connection, documentId: 1, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                setGuid: DtoLabelAccessTestHelper.TestSetGuid, versionNumber: 2,
                documentTitle: "Latest", isLatestVersion: 1);

            DtoLabelAccessTestHelper.SeedDocumentNavigationView(
                connection, documentId: 2, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2,
                setGuid: DtoLabelAccessTestHelper.TestSetGuid, versionNumber: 1,
                documentTitle: "Older", isLatestVersion: 0);

            var result = await DtoLabelAccess.GetDocumentNavigationAsync(
                context, latestOnly: false, setGuid: null, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count, "Should return all versions when latestOnly=false.");

            #endregion
        }

        #endregion GetDocumentNavigationAsync Tests

        #region GetDocumentVersionHistoryAsync Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that an empty database returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDocumentVersionHistoryAsync"/>
        [TestMethod]
        public async Task GetDocumentVersionHistoryAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            var result = await DtoLabelAccess.GetDocumentVersionHistoryAsync(
                context, DtoLabelAccessTestHelper.TestSetGuid, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that searching by SetGUID returns version history.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDocumentVersionHistoryAsync"/>
        [TestMethod]
        public async Task GetDocumentVersionHistoryAsync_BySetGUID_ReturnsVersionHistory()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedDocumentVersionHistoryView(
                connection, setGuid: DtoLabelAccessTestHelper.TestSetGuid,
                documentId: 1, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                versionNumber: 1, documentTitle: "Version 1");

            DtoLabelAccessTestHelper.SeedDocumentVersionHistoryView(
                connection, setGuid: DtoLabelAccessTestHelper.TestSetGuid,
                documentId: 2, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2,
                versionNumber: 2, documentTitle: "Version 2");

            var result = await DtoLabelAccess.GetDocumentVersionHistoryAsync(
                context, DtoLabelAccessTestHelper.TestSetGuid, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(DtoLabelAccessTestHelper.TestSetGuid, result[0].SetGUID);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that searching by DocumentGUID returns version history.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDocumentVersionHistoryAsync"/>
        [TestMethod]
        public async Task GetDocumentVersionHistoryAsync_ByDocumentGUID_ReturnsVersionHistory()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedDocumentVersionHistoryView(
                connection, setGuid: DtoLabelAccessTestHelper.TestSetGuid,
                documentId: 1, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                versionNumber: 1, documentTitle: "Version 1");

            var result = await DtoLabelAccess.GetDocumentVersionHistoryAsync(
                context, DtoLabelAccessTestHelper.TestDocumentGuid, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.AreEqual(DtoLabelAccessTestHelper.TestDocumentGuid, result[0].DocumentGUID);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that a non-matching GUID returns empty.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDocumentVersionHistoryAsync"/>
        [TestMethod]
        public async Task GetDocumentVersionHistoryAsync_NoMatch_ReturnsEmptyList()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedDocumentVersionHistoryView(
                connection, setGuid: DtoLabelAccessTestHelper.TestSetGuid,
                documentId: 1, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                versionNumber: 1);

            var nonMatchGuid = Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");
            var result = await DtoLabelAccess.GetDocumentVersionHistoryAsync(
                context, nonMatchGuid, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that version history is ordered by VersionNumber descending.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.GetDocumentVersionHistoryAsync"/>
        [TestMethod]
        public async Task GetDocumentVersionHistoryAsync_MultipleVersions_OrderedByVersionDesc()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedDocumentVersionHistoryView(
                connection, setGuid: DtoLabelAccessTestHelper.TestSetGuid,
                documentId: 1, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                versionNumber: 1, documentTitle: "Version 1");

            DtoLabelAccessTestHelper.SeedDocumentVersionHistoryView(
                connection, setGuid: DtoLabelAccessTestHelper.TestSetGuid,
                documentId: 2, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2,
                versionNumber: 3, documentTitle: "Version 3");

            DtoLabelAccessTestHelper.SeedDocumentVersionHistoryView(
                connection, setGuid: DtoLabelAccessTestHelper.TestSetGuid,
                documentId: 3, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid3,
                versionNumber: 2, documentTitle: "Version 2");

            var result = await DtoLabelAccess.GetDocumentVersionHistoryAsync(
                context, DtoLabelAccessTestHelper.TestSetGuid, PkSecret, logger);

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(result[0].VersionNumber >= result[1].VersionNumber,
                "Results should be ordered by VersionNumber descending.");
            Assert.IsTrue(result[1].VersionNumber >= result[2].VersionNumber,
                "Results should be ordered by VersionNumber descending.");

            #endregion
        }

        #endregion GetDocumentVersionHistoryAsync Tests
    }
}
