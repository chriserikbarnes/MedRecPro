using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Service.Common;
using MedRecPro.Service.LabelQuery;
using MedRecPro.Service.LabelQuery.Common;
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
        /// Verifies a generic inventory query returns injected cache hits without database access and
        /// records exactly one managed miss write with the frozen key and ten-hour TTL.
        /// </summary>
        /// <seealso cref="LabelQueryDataAccess.GetInventorySummaryAsync"/>
        [TestMethod]
        public async Task InventorySummaryCache_HitAndMiss_PreserveKeyTtlAndDatabaseBoundary()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            DtoLabelAccessTestHelper.SeedInventorySummaryView(
                connection,
                category: "TOTALS",
                dimension: "Documents",
                dimensionValue: "All",
                itemCount: 1,
                sortOrder: 1);

            var key = new LegacyDtoLabelCacheKeyBuilder().BuildQueryKey(
                "GetInventorySummaryAsync",
                "TOTALS",
                null,
                null);
            var cachedSentinel = new List<InventorySummaryDto>
            {
                new() { InventorySummary = new Dictionary<string, object?>() }
            };
            var hitCache = new RecordingQueryCache();
            hitCache.Seed(key, cachedSentinel);
            var disposedContext = DtoLabelAccessTestHelper.CreateTestContext(connection);
            disposedContext.Dispose();

            var hit = await DtoLabelAccessTestHelper.CreateLabelQueryDataAccess(hitCache)
                .GetInventorySummaryAsync(disposedContext, "TOTALS", DtoLabelAccessTestHelper.CreateTestLogger());

            Assert.AreSame(cachedSentinel, hit);
            Assert.AreEqual(0, hitCache.ManagedWrites.Count);

            var missCache = new RecordingQueryCache();
            var miss = await DtoLabelAccessTestHelper.CreateLabelQueryDataAccess(missCache)
                .GetInventorySummaryAsync(context, "TOTALS", DtoLabelAccessTestHelper.CreateTestLogger());

            Assert.AreEqual(1, miss.Count);
            Assert.AreEqual(1, missCache.ManagedWrites.Count);
            Assert.AreEqual(key, missCache.ManagedWrites[0].Key);
            Assert.AreEqual(10.0, missCache.ManagedWrites[0].DurationHours);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies batch document graph cache hits bypass the database and misses preserve the
        /// loading-mode key, managed membership, and one-hour TTL.
        /// </summary>
        /// <seealso cref="LabelQueryDataAccess.BuildDocumentsAsync(ApplicationDbContext, string, ILogger, int?, int?, bool?)"/>
        [TestMethod]
        public async Task DocumentGraphCache_HitAndMiss_PreserveLoadingModeKeyAndTtl()
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
                "Cache Document");

            var key = new LegacyDtoLabelCacheKeyBuilder().BuildDocumentPageKey(null, null, useBatchLoading: true);
            var cachedSentinel = new List<DocumentDto>
            {
                new() { Document = new Dictionary<string, object?>() }
            };
            var hitCache = new RecordingQueryCache();
            hitCache.Seed(key, cachedSentinel);
            var disposedContext = DtoLabelAccessTestHelper.CreateTestContext(connection);
            disposedContext.Dispose();

            var hit = await DtoLabelAccessTestHelper.CreateLabelQueryDataAccess(hitCache).BuildDocumentsAsync(
                disposedContext,
                DtoLabelAccessTestHelper.TestPkSecret,
                DtoLabelAccessTestHelper.CreateTestLogger(),
                useBatchLoading: true);

            Assert.AreSame(cachedSentinel, hit);
            Assert.AreEqual(0, hitCache.ManagedWrites.Count);

            var missCache = new RecordingQueryCache();
            var miss = await DtoLabelAccessTestHelper.CreateLabelQueryDataAccess(missCache).BuildDocumentsAsync(
                context,
                DtoLabelAccessTestHelper.TestPkSecret,
                DtoLabelAccessTestHelper.CreateTestLogger(),
                useBatchLoading: true);

            Assert.AreEqual(1, miss.Count);
            Assert.AreEqual(1, missCache.ManagedWrites.Count);
            Assert.AreEqual(key, missCache.ManagedWrites[0].Key);
            Assert.AreEqual(1.0, missCache.ManagedWrites[0].DurationHours);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies Orange Book cache hits bypass the database and misses preserve the frozen
        /// composite key, managed membership, and one-hour TTL.
        /// </summary>
        /// <seealso cref="LabelQueryDataAccess.SearchOrangeBookPatentsAsync"/>
        [TestMethod]
        public async Task OrangeBookCache_HitAndMiss_PreserveCompositeKeyAndTtl()
        {
            #region implementation

            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            DtoLabelAccessTestHelper.SeedOrangeBookPatentView(
                connection,
                patentNo: "US-CACHE",
                patentExpireDate: new DateTime(2030, 1, 1));

            var key = new LegacyDtoLabelCacheKeyBuilder().BuildOrangeBookSearchKey(
                null, null, null, null, null, null, null, null, null, null, null);
            var cachedSentinel = new List<OrangeBookPatentDto>
            {
                new() { OrangeBookPatent = new Dictionary<string, object?>() }
            };
            var hitCache = new RecordingQueryCache();
            hitCache.Seed(key, cachedSentinel);
            var disposedContext = DtoLabelAccessTestHelper.CreateTestContext(connection);
            disposedContext.Dispose();

            var hit = await DtoLabelAccessTestHelper.CreateLabelQueryDataAccess(hitCache).SearchOrangeBookPatentsAsync(
                disposedContext,
                null, null, null, null, null, null, null, null, null,
                DtoLabelAccessTestHelper.TestPkSecret,
                DtoLabelAccessTestHelper.CreateTestLogger());

            Assert.AreSame(cachedSentinel, hit);
            Assert.AreEqual(0, hitCache.ManagedWrites.Count);

            var missCache = new RecordingQueryCache();
            var miss = await DtoLabelAccessTestHelper.CreateLabelQueryDataAccess(missCache).SearchOrangeBookPatentsAsync(
                context,
                null, null, null, null, null, null, null, null, null,
                DtoLabelAccessTestHelper.TestPkSecret,
                DtoLabelAccessTestHelper.CreateTestLogger());

            Assert.AreEqual(1, miss.Count);
            Assert.AreEqual(1, missCache.ManagedWrites.Count);
            Assert.AreEqual(key, missCache.ManagedWrites[0].Key);
            Assert.AreEqual(1.0, missCache.ManagedWrites[0].DurationHours);

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
                DtoLabelAccessTestHelper.CreateLabelQueryDataAccess(),
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
                DtoLabelAccessTestHelper.CreateLabelQueryDataAccess(),
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
                DtoLabelAccessTestHelper.CreateLabelQueryDataAccess(),
                NullLogger<LabelDocumentQueryService>.Instance);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records typed query-cache reads and managed writes for parity assertions.
        /// </summary>
        /// <seealso cref="IAppCache"/>
        private sealed class RecordingQueryCache : IAppCache
        {
            private readonly Dictionary<string, object> entries = new(StringComparer.Ordinal);

            /**************************************************************/
            /// <summary>
            /// Gets the managed writes recorded by this cache.
            /// </summary>
            public List<ManagedCacheWrite> ManagedWrites { get; } = new();

            /**************************************************************/
            /// <summary>
            /// Seeds a typed cache hit without recording a managed write.
            /// </summary>
            /// <param name="key">The opaque cache key.</param>
            /// <param name="value">The value returned for the key.</param>
            public void Seed(string key, object value)
            {
                #region implementation

                entries[key] = value;

                #endregion
            }

            /**************************************************************/
            /// <inheritdoc/>
            public object? Get(string key) => entries.TryGetValue(key, out var value) ? value : null;

            /**************************************************************/
            /// <inheritdoc/>
            public T? Get<T>(string key) => Get(key) is T value ? value : default;

            /**************************************************************/
            /// <inheritdoc/>
            public T? GetCachedJson<T>(string key) => default;

            /**************************************************************/
            /// <inheritdoc/>
            public void Set(string key, object value, double durationHours = 1.0) => entries[key] = value;

            /**************************************************************/
            /// <inheritdoc/>
            public void SetManaged(string key, object value, double durationHours = 4.0)
            {
                #region implementation

                entries[key] = value;
                ManagedWrites.Add(new ManagedCacheWrite(key, durationHours));

                #endregion
            }

            /**************************************************************/
            /// <inheritdoc/>
            public void Remove(string key) => entries.Remove(key);

            /**************************************************************/
            /// <inheritdoc/>
            public void ResetManaged()
            {
                #region implementation

                entries.Clear();
                ManagedWrites.Clear();

                #endregion
            }
        }

        /**************************************************************/
        /// <summary>
        /// Describes one managed cache write captured by a parity test.
        /// </summary>
        /// <param name="Key">The opaque key written by the query.</param>
        /// <param name="DurationHours">The requested absolute expiration in hours.</param>
        private sealed record ManagedCacheWrite(string Key, double DurationHours);
    }
}
