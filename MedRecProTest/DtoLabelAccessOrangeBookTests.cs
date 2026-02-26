using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>.
    /// </summary>
    /// <remarks>
    /// Tests cover: empty database, no-filter return-all, individual filter isolation
    /// (ApplicationNumber, Ingredient, TradeName, PatentNo, DocumentGuid,
    /// HasPediatricFlag, HasWithdrawnCommercialReasonFlag), non-matching exact
    /// patent number, pagination, and multi-filter intersection.
    ///
    /// All tests use shared-cache named SQLite in-memory databases with a sentinel
    /// connection, seeded via <see cref="DtoLabelAccessTestHelper.SeedOrangeBookPatentView"/>.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="LabelView.OrangeBookPatent"/>
    /// <seealso cref="OrangeBookPatentDto"/>
    [TestClass]
    public class DtoLabelAccessOrangeBookTests
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

        #region SearchOrangeBookPatentsAsync — Empty Database

        /**************************************************************/
        /// <summary>
        /// Verifies that SearchOrangeBookPatentsAsync returns an empty list
        /// when the database contains no OrangeBookPatent rows.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task SearchOrangeBookPatentsAsync_EmptyDatabase_ReturnsEmptyList()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Act
            var result = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context,
                expiringInMonths: null,
                documentGuid: null,
                applicationNumber: null,
                ingredient: null,
                tradeName: null,
                patentNo: null,
                patentExpireDate: null,
                hasPediatricFlag: null,
                hasWithdrawnCommercialReasonFlag: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null even with empty database");
            Assert.AreEqual(0, result.Count, "Empty database should return zero patents");

            #endregion
        }

        #endregion SearchOrangeBookPatentsAsync — Empty Database

        #region SearchOrangeBookPatentsAsync — No Filters

        /**************************************************************/
        /// <summary>
        /// Verifies that SearchOrangeBookPatentsAsync returns all seeded patents
        /// when no filters are applied.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task SearchOrangeBookPatentsAsync_NoFilters_ReturnsAll()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Seed two distinct patents
            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                applicationNumber: "NDA014526",
                ingredient: "ASPIRIN",
                tradeName: "ASPIRIN",
                patentNo: "US1234567");

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2,
                applicationNumber: "NDA020702",
                ingredient: "ATORVASTATIN CALCIUM",
                tradeName: "LIPITOR",
                patentNo: "US7654321");

            // Act
            var result = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context,
                expiringInMonths: null,
                documentGuid: null,
                applicationNumber: null,
                ingredient: null,
                tradeName: null,
                patentNo: null,
                patentExpireDate: null,
                hasPediatricFlag: null,
                hasWithdrawnCommercialReasonFlag: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(2, result.Count, "Should return both seeded patents when no filters are applied");

            #endregion
        }

        #endregion SearchOrangeBookPatentsAsync — No Filters

        #region SearchOrangeBookPatentsAsync — Individual Filter Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that filtering by ApplicationNumber returns only the matching patent.
        /// ApplicationNumber uses exact match in the query.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task SearchOrangeBookPatentsAsync_ByApplicationNumber_ReturnsMatching()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA014526",
                ingredient: "ASPIRIN",
                tradeName: "ASPIRIN",
                patentNo: "US1234567");

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA020702",
                ingredient: "ATORVASTATIN CALCIUM",
                tradeName: "LIPITOR",
                patentNo: "US7654321");

            // Act
            var result = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context,
                expiringInMonths: null,
                documentGuid: null,
                applicationNumber: "NDA014526",
                ingredient: null,
                tradeName: null,
                patentNo: null,
                patentExpireDate: null,
                hasPediatricFlag: null,
                hasWithdrawnCommercialReasonFlag: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return exactly one patent matching ApplicationNumber NDA014526");
            Assert.AreEqual("ASPIRIN", result[0].Ingredient, "Matching patent should have Ingredient ASPIRIN");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that filtering by Ingredient returns patents matching the
        /// partial search term via FilterBySearchTerms.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task SearchOrangeBookPatentsAsync_ByIngredient_ReturnsMatching()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA014526",
                ingredient: "ASPIRIN",
                tradeName: "ASPIRIN",
                patentNo: "US1234567");

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA020702",
                ingredient: "ATORVASTATIN CALCIUM",
                tradeName: "LIPITOR",
                patentNo: "US7654321");

            // Act — search for ASPIRIN ingredient
            var result = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context,
                expiringInMonths: null,
                documentGuid: null,
                applicationNumber: null,
                ingredient: "ASPIRIN",
                tradeName: null,
                patentNo: null,
                patentExpireDate: null,
                hasPediatricFlag: null,
                hasWithdrawnCommercialReasonFlag: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return exactly one patent matching ingredient ASPIRIN");
            Assert.AreEqual("ASPIRIN", result[0].TradeName, "Matching patent should have TradeName ASPIRIN");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that filtering by TradeName returns patents matching the
        /// partial search term via FilterBySearchTerms.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task SearchOrangeBookPatentsAsync_ByTradeName_ReturnsMatching()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA014526",
                ingredient: "ASPIRIN",
                tradeName: "ASPIRIN",
                patentNo: "US1234567");

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA020702",
                ingredient: "ATORVASTATIN CALCIUM",
                tradeName: "LIPITOR",
                patentNo: "US7654321");

            // Act — search for LIPITOR trade name
            var result = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context,
                expiringInMonths: null,
                documentGuid: null,
                applicationNumber: null,
                ingredient: null,
                tradeName: "LIPITOR",
                patentNo: null,
                patentExpireDate: null,
                hasPediatricFlag: null,
                hasWithdrawnCommercialReasonFlag: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return exactly one patent matching trade name LIPITOR");
            Assert.AreEqual("ATORVASTATIN CALCIUM", result[0].Ingredient,
                "Matching patent should have Ingredient ATORVASTATIN CALCIUM");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that filtering by PatentNo returns only the exact-match patent.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task SearchOrangeBookPatentsAsync_ByPatentNumber_ReturnsExactMatch()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA014526",
                ingredient: "ASPIRIN",
                tradeName: "ASPIRIN",
                patentNo: "US1234567");

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA020702",
                ingredient: "ATORVASTATIN CALCIUM",
                tradeName: "LIPITOR",
                patentNo: "US7654321");

            // Act — exact match on patent number
            var result = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context,
                expiringInMonths: null,
                documentGuid: null,
                applicationNumber: null,
                ingredient: null,
                tradeName: null,
                patentNo: "US1234567",
                patentExpireDate: null,
                hasPediatricFlag: null,
                hasWithdrawnCommercialReasonFlag: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return exactly one patent matching patent number US1234567");
            Assert.AreEqual("ASPIRIN", result[0].TradeName, "Matching patent should have TradeName ASPIRIN");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that filtering by a non-existent PatentNo returns an empty list.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task SearchOrangeBookPatentsAsync_ByPatentNumber_NoMatch_ReturnsEmpty()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA014526",
                ingredient: "ASPIRIN",
                tradeName: "ASPIRIN",
                patentNo: "US1234567");

            // Act — search for a patent number that does not exist
            var result = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context,
                expiringInMonths: null,
                documentGuid: null,
                applicationNumber: null,
                ingredient: null,
                tradeName: null,
                patentNo: "US9999999",
                patentExpireDate: null,
                hasPediatricFlag: null,
                hasWithdrawnCommercialReasonFlag: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(0, result.Count, "Non-existent patent number should return zero results");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that filtering by DocumentGuid returns only the patent
        /// associated with the specified document GUID.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task SearchOrangeBookPatentsAsync_ByDocumentGuid_ReturnsMatching()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                applicationNumber: "NDA014526",
                ingredient: "ASPIRIN",
                tradeName: "ASPIRIN",
                patentNo: "US1234567");

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2,
                applicationNumber: "NDA020702",
                ingredient: "ATORVASTATIN CALCIUM",
                tradeName: "LIPITOR",
                patentNo: "US7654321");

            // Act — filter by the first document GUID
            var result = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context,
                expiringInMonths: null,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                applicationNumber: null,
                ingredient: null,
                tradeName: null,
                patentNo: null,
                patentExpireDate: null,
                hasPediatricFlag: null,
                hasWithdrawnCommercialReasonFlag: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return exactly one patent matching the specified DocumentGuid");
            Assert.AreEqual("ASPIRIN", result[0].TradeName,
                "Matching patent should have TradeName ASPIRIN for TestDocumentGuid");

            #endregion
        }

        #endregion SearchOrangeBookPatentsAsync — Individual Filter Tests

        #region SearchOrangeBookPatentsAsync — Boolean Flag Filter Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that filtering by HasPediatricFlag=true returns only patents
        /// where the pediatric exclusivity flag is set.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task SearchOrangeBookPatentsAsync_ByPediatricFlag_ReturnsMatching()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Seed patent WITH pediatric flag
            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA014526",
                ingredient: "ASPIRIN",
                tradeName: "ASPIRIN",
                patentNo: "US1234567",
                hasPediatricFlag: true);

            // Seed patent WITHOUT pediatric flag
            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA020702",
                ingredient: "ATORVASTATIN CALCIUM",
                tradeName: "LIPITOR",
                patentNo: "US7654321",
                hasPediatricFlag: false);

            // Act — filter for pediatric patents only
            var result = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context,
                expiringInMonths: null,
                documentGuid: null,
                applicationNumber: null,
                ingredient: null,
                tradeName: null,
                patentNo: null,
                patentExpireDate: null,
                hasPediatricFlag: true,
                hasWithdrawnCommercialReasonFlag: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return exactly one patent with HasPediatricFlag=true");
            Assert.AreEqual("ASPIRIN", result[0].TradeName,
                "Matching pediatric patent should have TradeName ASPIRIN");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that filtering by HasWithdrawnCommercialReasonFlag=true returns
        /// only patents where the withdrawn commercial reason flag is set.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task SearchOrangeBookPatentsAsync_ByWithdrawnFlag_ReturnsMatching()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Seed patent WITH withdrawn flag
            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA014526",
                ingredient: "ASPIRIN",
                tradeName: "ASPIRIN",
                patentNo: "US1234567",
                hasWithdrawnCommercialReasonFlag: true);

            // Seed patent WITHOUT withdrawn flag
            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA020702",
                ingredient: "ATORVASTATIN CALCIUM",
                tradeName: "LIPITOR",
                patentNo: "US7654321",
                hasWithdrawnCommercialReasonFlag: false);

            // Act — filter for withdrawn patents only
            var result = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context,
                expiringInMonths: null,
                documentGuid: null,
                applicationNumber: null,
                ingredient: null,
                tradeName: null,
                patentNo: null,
                patentExpireDate: null,
                hasPediatricFlag: null,
                hasWithdrawnCommercialReasonFlag: true,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count, "Should return exactly one patent with HasWithdrawnCommercialReasonFlag=true");
            Assert.AreEqual("ASPIRIN", result[0].TradeName,
                "Matching withdrawn patent should have TradeName ASPIRIN");

            #endregion
        }

        #endregion SearchOrangeBookPatentsAsync — Boolean Flag Filter Tests

        #region SearchOrangeBookPatentsAsync — Pagination Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that pagination returns the correct subset of results.
        /// Seeds three patents, requests page 1 with size 2, and asserts
        /// that exactly two results are returned.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task SearchOrangeBookPatentsAsync_Pagination_ReturnsCorrectPage()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Seed three patents with distinct trade names for deterministic ordering
            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid,
                applicationNumber: "NDA014526",
                ingredient: "ASPIRIN",
                tradeName: "ASPIRIN",
                patentNo: "US1111111");

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2,
                applicationNumber: "NDA020702",
                ingredient: "ATORVASTATIN CALCIUM",
                tradeName: "LIPITOR",
                patentNo: "US2222222");

            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid3,
                applicationNumber: "NDA021457",
                ingredient: "METFORMIN HYDROCHLORIDE",
                tradeName: "GLUCOPHAGE",
                patentNo: "US3333333");

            // Act — request page 1 with size 2
            var result = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context,
                expiringInMonths: null,
                documentGuid: null,
                applicationNumber: null,
                ingredient: null,
                tradeName: null,
                patentNo: null,
                patentExpireDate: null,
                hasPediatricFlag: null,
                hasWithdrawnCommercialReasonFlag: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger,
                page: 1,
                size: 2);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(2, result.Count, "Page 1 with size 2 should return exactly 2 patents out of 3 total");

            #endregion
        }

        #endregion SearchOrangeBookPatentsAsync — Pagination Tests

        #region SearchOrangeBookPatentsAsync — Multiple Filter Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that combining multiple filters returns only the intersection
        /// of all matching criteria using AND logic. Seeds three patents where
        /// only one satisfies both ApplicationNumber and HasPediatricFlag filters.
        /// </summary>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task SearchOrangeBookPatentsAsync_MultipleFilters_ReturnsIntersection()
        {
            #region implementation

            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();

            // Patent 1: NDA014526, pediatric=true — matches BOTH filters
            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA014526",
                ingredient: "ASPIRIN",
                tradeName: "ASPIRIN",
                patentNo: "US1234567",
                hasPediatricFlag: true);

            // Patent 2: NDA014526, pediatric=false — matches ApplicationNumber but NOT PediatricFlag
            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA014526",
                ingredient: "ASPIRIN",
                tradeName: "ASPIRIN DELAYED-RELEASE",
                patentNo: "US1234568",
                hasPediatricFlag: false);

            // Patent 3: NDA020702, pediatric=true — matches PediatricFlag but NOT ApplicationNumber
            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                applicationNumber: "NDA020702",
                ingredient: "ATORVASTATIN CALCIUM",
                tradeName: "LIPITOR",
                patentNo: "US7654321",
                hasPediatricFlag: true);

            // Act — combine ApplicationNumber AND HasPediatricFlag filters
            var result = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context,
                expiringInMonths: null,
                documentGuid: null,
                applicationNumber: "NDA014526",
                ingredient: null,
                tradeName: null,
                patentNo: null,
                patentExpireDate: null,
                hasPediatricFlag: true,
                hasWithdrawnCommercialReasonFlag: null,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            // Assert
            Assert.IsNotNull(result, "Result should not be null");
            Assert.AreEqual(1, result.Count,
                "Only one patent should match both ApplicationNumber=NDA014526 AND HasPediatricFlag=true");
            Assert.AreEqual("ASPIRIN", result[0].TradeName,
                "Matching patent should have TradeName ASPIRIN (the one with both filters satisfied)");

            #endregion
        }

        #endregion SearchOrangeBookPatentsAsync — Multiple Filter Tests
    }
}
