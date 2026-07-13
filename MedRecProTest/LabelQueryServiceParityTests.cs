using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Service.LabelQuery;
using MedRecPro.Service.LabelQuery.Implementation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Verifies the extracted Label query services preserve legacy facade results.
    /// </summary>
    /// <remarks>
    /// These tests use the relational SQLite harness so cache keys, provider-side queries, ordering, and DTO mapping
    /// execute through the same paths as the legacy compatibility facade.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="IProductSearchService"/>
    /// <seealso cref="ILabelDocumentQueryService"/>
    [TestClass]
    public class LabelQueryServiceParityTests
    {
        /**************************************************************/
        /// <summary>
        /// Clears global cache state before each parity scenario.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation

            DtoLabelAccessTestHelper.ClearCache();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the injected product search service matches the legacy application-number result.
        /// </summary>
        /// <seealso cref="IProductSearchService.SearchByApplicationNumberAsync"/>
        /// <seealso cref="DtoLabelAccess.SearchByApplicationNumberAsync"/>
        [TestMethod]
        public async Task ProductSearchService_ApplicationNumberSearch_MatchesLegacyFacade()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            DtoLabelAccessTestHelper.SeedProductsByApplicationNumberView(connection, "NDA014526", "ASPIRIN", 7);

            var legacy = await DtoLabelAccess.SearchByApplicationNumberAsync(
                context,
                "NDA014526",
                DtoLabelAccessTestHelper.TestPkSecret,
                DtoLabelAccessTestHelper.CreateTestLogger());

            DtoLabelAccessTestHelper.ClearCache();
            var service = createProductSearchService(context);
            var extracted = await service.SearchByApplicationNumberAsync("NDA014526");

            Assert.AreEqual(legacy.Count, extracted.Count);
            Assert.AreEqual(legacy[0].ApplicationNumber, extracted[0].ApplicationNumber);
            Assert.AreEqual(legacy[0].ProductName, extracted[0].ProductName);
            Assert.AreEqual(legacy[0].DocumentGUID, extracted[0].DocumentGUID);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the extracted Orange Book service preserves ordered patent search results.
        /// </summary>
        /// <seealso cref="IOrangeBookPatentQueryService.SearchOrangeBookPatentsAsync"/>
        /// <seealso cref="DtoLabelAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task OrangeBookPatentQueryService_Search_MatchesLegacyFacade()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(connection, patentNo: "US100", patentExpireDate: new DateTime(2030, 1, 1));
            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(connection, documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid2, patentNo: "US200", patentExpireDate: new DateTime(2031, 1, 1));

            var logger = DtoLabelAccessTestHelper.CreateTestLogger();
            var legacy = await DtoLabelAccess.SearchOrangeBookPatentsAsync(
                context, null, null, null, null, null, null, null, null, null,
                DtoLabelAccessTestHelper.TestPkSecret, logger);

            DtoLabelAccessTestHelper.ClearCache();
            var service = createOrangeBookService(context);
            var extracted = await service.SearchOrangeBookPatentsAsync(null, null, null, null, null, null, null, null, null, null);

            Assert.AreEqual(legacy.Count, extracted.Count);
            CollectionAssert.AreEqual(
                legacy.Select(patent => patent.LabelLink).ToList(),
                extracted.Select(patent => patent.LabelLink).ToList());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies batch and sequential document services produce the same graph identities as the facade.
        /// </summary>
        /// <seealso cref="ILabelDocumentQueryService.BuildDocumentsAsync"/>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync"/>
        [TestMethod]
        public async Task LabelDocumentQueryService_BatchAndSequentialGraphs_MatchLegacyFacade()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestSetGuid,
                "Parity Document");

            var logger = DtoLabelAccessTestHelper.CreateTestLogger();
            var legacy = await DtoLabelAccess.BuildDocumentsAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger,
                useBatchLoading: true);

            var service = createDocumentQueryService(context);
            DtoLabelAccessTestHelper.ClearCache();
            var batch = await service.BuildDocumentsAsync(useBatchLoading: true);
            DtoLabelAccessTestHelper.ClearCache();
            var sequential = await service.BuildDocumentsAsync(useBatchLoading: false);

            Assert.AreEqual(legacy.Count, batch.Count);
            Assert.AreEqual(batch.Count, sequential.Count);
            Assert.AreEqual(legacy[0].Document["DocumentGUID"], batch[0].Document["DocumentGUID"]);
            Assert.AreEqual(batch[0].Document["DocumentGUID"], sequential[0].Document["DocumentGUID"]);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates configuration consistent with the static facade test secret.
        /// </summary>
        /// <returns>In-memory configuration for direct service construction.</returns>
        private static IConfiguration createConfiguration()
        {
            #region implementation

            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:DB:PKSecret"] = DtoLabelAccessTestHelper.TestPkSecret
                })
                .Build();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an extracted product service for a relational parity test.
        /// </summary>
        /// <param name="context">The relational test context.</param>
        /// <returns>The configured product query service.</returns>
        private static IProductSearchService createProductSearchService(ApplicationDbContext context)
        {
            #region implementation

            return new ProductSearchService(
                context,
                createConfiguration(),
                new LabelQueryDataAccess(),
                NullLogger<ProductSearchService>.Instance);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an extracted Orange Book service for a relational parity test.
        /// </summary>
        /// <param name="context">The relational test context.</param>
        /// <returns>The configured Orange Book query service.</returns>
        private static IOrangeBookPatentQueryService createOrangeBookService(ApplicationDbContext context)
        {
            #region implementation

            return new OrangeBookPatentQueryService(
                context,
                createConfiguration(),
                new LabelQueryDataAccess(),
                NullLogger<OrangeBookPatentQueryService>.Instance);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an extracted document service for a relational graph-parity test.
        /// </summary>
        /// <param name="context">The relational test context.</param>
        /// <returns>The configured document query service.</returns>
        private static ILabelDocumentQueryService createDocumentQueryService(ApplicationDbContext context)
        {
            #region implementation

            return new LabelDocumentQueryService(
                context,
                createConfiguration(),
                new LabelQueryDataAccess(),
                NullLogger<LabelDocumentQueryService>.Instance);

            #endregion
        }
    }
}
