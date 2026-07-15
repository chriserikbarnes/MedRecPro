using MedRecPro.Helpers;
using MedRecPro.DataAccess;
using MedRecPro.Service;
using MedRecPro.Service.Common;
using MedRecPro.Service.LabelQuery.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Label.Queries
{
    /**************************************************************/
    /// <summary>
    /// Tests legacy key, shared cache, and encrypted-ID policies independently of the facade.
    /// </summary>
    /// <remarks>
    /// The tests pin decoded cache text, Base64 encoding, managed-cache duration,
    /// and captured token decryption so service extraction cannot alter behavior.
    /// </remarks>
    /// <seealso cref="LegacyDtoLabelCacheKeyBuilder"/>
    /// <seealso cref="AeDashboardEncryptedIdMapper"/>
    [TestClass]
    [TestCategory("Unit")]
    public class DtoLabelAccessPolicyTests
    {
        /**************************************************************/
        /// <summary>
        /// Verifies the common builder preserves generic, document, and Orange Book key shapes.
        /// </summary>
        /// <seealso cref="LegacyDtoLabelCacheKeyBuilder.BuildQueryKey"/>
        [TestMethod]
        public void LegacyDtoLabelCacheKeyBuilder_BuildsFrozenDecodedKeys()
        {
            #region implementation

            var builder = new LegacyDtoLabelCacheKeyBuilder();
            var documentGuid = Guid.Parse("f16d9f01-d515-40fe-a0ff-ac70627cb512");

            var generic = builder.BuildQueryKey("GetIngredientSummariesAsync", "aspirin sodium", 2, 25);
            var pagedDocument = builder.BuildDocumentPageKey(1, 10, useBatchLoading: true);
            var oneDocument = builder.BuildDocumentGuidKey(documentGuid, useBatchLoading: false);
            var orangeBook = builder.BuildOrangeBookSearchKey(
                expiringInMonths: null,
                documentGuid: null,
                applicationNumber: "020702",
                ingredient: "atorvastatin",
                tradeName: "Lipitor",
                patentNo: "12345",
                patentExpireDate: new DateOnly(2030, 1, 2),
                hasPediatricFlag: true,
                hasWithdrawnCommercialReasonFlag: false,
                page: 1,
                size: 50);

            Assert.AreEqual("DtoLabelAccess.GetIngredientSummariesAsync_aspirin_sodium_2_25", generic.Base64Decode());
            Assert.AreEqual("DtoLabelAccess.BuildDocumentsAsync_1_10_batch", pagedDocument.Base64Decode());
            Assert.AreEqual($"DtoLabelAccess.BuildDocumentsAsync.{documentGuid}_sequential", oneDocument.Base64Decode());
            Assert.AreEqual("DtoLabelAccess.SearchOrangeBookPatentsAsync_--020702-atorvastatin-Lipitor-12345-2030-01-02-True-False_1_50", orangeBook.Base64Decode());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the shared query policy reads an opaque caller-owned key without Label knowledge.
        /// </summary>
        /// <seealso cref="QueryCachePolicy"/>
        [TestMethod]
        public void QueryCachePolicy_GetByKey_ReturnsInjectedTypedValue()
        {
            #region implementation

            var sentinel = new[] { "cached" };
            var cache = new RecordingAppCache { ValueToReturn = sentinel };
            var policy = new QueryCachePolicy(cache);
            const string opaqueKey = "opaque-query-key";

            var result = policy.GetByKey<string[]>(opaqueKey);

            Assert.AreSame(sentinel, result);
            Assert.AreEqual(opaqueKey, cache.Key);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the shared policy preserves managed membership and caller-supplied TTLs.
        /// </summary>
        /// <seealso cref="QueryCachePolicy"/>
        /// <seealso cref="LegacyDtoLabelCacheKeyBuilder"/>
        [TestMethod]
        public void QueryCachePolicy_SetManagedByKey_UsesCallerKeyManagedMembershipAndDuration()
        {
            #region implementation

            var cache = new RecordingAppCache();
            var policy = new QueryCachePolicy(cache);
            var keyBuilder = new LegacyDtoLabelCacheKeyBuilder();
            var inventoryKey = keyBuilder.BuildQueryKey("GetInventorySummaryAsync", "TOP LABELERS", null, null);

            policy.SetManagedByKey(inventoryKey, new[] { "value" }, 10.0);

            Assert.IsTrue(cache.ManagedWrite);
            Assert.AreEqual(10.0, cache.DurationHours);
            Assert.AreEqual("DtoLabelAccess.GetInventorySummaryAsync_TOP_LABELERS__", cache.Key!.Base64Decode());
            CollectionAssert.AreEqual(new[] { "value" }, (string[])cache.Value!);

            var guideKey = keyBuilder.BuildQueryKey("GetAPIEndpointGuideAsync", "clinical", null, null);
            policy.SetManagedByKey(guideKey, new[] { "guide" }, 5.0);

            Assert.AreEqual(5.0, cache.DurationHours);
            Assert.AreEqual("DtoLabelAccess.GetAPIEndpointGuideAsync_clinical__", cache.Key!.Base64Decode());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the AE policy uses its injected cache and preserves its private-helper key name.
        /// </summary>
        /// <seealso cref="AeDashboardCachePolicy"/>
        [TestMethod]
        public void AeDashboardCachePolicy_InjectedCache_PreservesPrivateCatalogKeyAndTtl()
        {
            #region implementation

            var cache = new RecordingAppCache();
            var policy = new AeDashboardCachePolicy(cache);
            var key = policy.GenerateKey(
                "getCachedAeProductCatalogAsync",
                AeDashboardDataAccess.AnonymousCatalogByDocumentCacheDiscriminator,
                null,
                null);

            policy.Set(key, "catalog", 1.0);

            Assert.AreEqual("DtoLabelAccess.getCachedAeProductCatalogAsync_anonymous-catalog-by-document-v1__", key.Base64Decode());
            Assert.IsTrue(cache.ManagedWrite);
            Assert.AreEqual(key, cache.Key);
            Assert.AreEqual(1.0, cache.DurationHours);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a captured pre-refactor fast token still decrypts through the injected mapper.
        /// </summary>
        /// <seealso cref="AeDashboardEncryptedIdMapper.DecryptNullableInt"/>
        [TestMethod]
        public void AeDashboardEncryptedIdMapper_CapturedTokenAndInvalidInput_PreserveNullBehavior()
        {
            #region implementation

            const string capturedPreRefactorId = "F-AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyBLjjQW1p8HoYG7I6eDRbBb";
            var mapper = new AeDashboardEncryptedIdMapper();
            var logger = NullLogger.Instance;

            var captured = mapper.DecryptNullableInt(capturedPreRefactorId, DtoLabelAccessTestHelper.TestPkSecret, logger, "CapturedId");
            var invalid = mapper.DecryptNullableInt("not-a-token", DtoLabelAccessTestHelper.TestPkSecret, logger, "InvalidId");
            var nullable = mapper.EncryptNullableInt(null, DtoLabelAccessTestHelper.TestPkSecret, logger, "MissingId");

            Assert.AreEqual(30, captured);
            Assert.IsNull(invalid);
            Assert.IsNull(nullable);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records calls made through the injectable application-cache abstraction.
        /// </summary>
        /// <seealso cref="IAppCache"/>
        private sealed class RecordingAppCache : IAppCache
        {
            public string? Key { get; private set; }
            public object? Value { get; private set; }
            public object? ValueToReturn { get; init; }
            public double DurationHours { get; private set; }
            public bool ManagedWrite { get; private set; }

            public object? Get(string key)
            {
                Key = key;
                return ValueToReturn;
            }

            public T? Get<T>(string key)
            {
                Key = key;
                return ValueToReturn is T value ? value : default;
            }
            public T? GetCachedJson<T>(string key) => default;
            public void Set(string key, object value, double durationHours = 1.0) => record(key, value, durationHours, false);
            public void SetManaged(string key, object value, double durationHours = 4.0) => record(key, value, durationHours, true);
            public void Remove(string key) { }
            public void ResetManaged() { }

            /**************************************************************/
            /// <summary>
            /// Captures one cache write for assertion.
            /// </summary>
            private void record(string key, object value, double durationHours, bool managed)
            {
                #region implementation

                Key = key;
                Value = value;
                DurationHours = durationHours;
                ManagedWrite = managed;

                #endregion
            }
        }
    }
}
