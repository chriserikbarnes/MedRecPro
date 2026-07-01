using MedRecPro.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests public PerformanceHelper cache methods using unique cache keys.
    /// </summary>
    /// <seealso cref="PerformanceHelper"/>
    [TestClass]
    public class PerformanceHelperTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies cache set, get, generic get, JSON get, and removal behavior.
        /// </summary>
        /// <seealso cref="PerformanceHelper.SetCache(string, object)"/>
        /// <seealso cref="PerformanceHelper.SetCache(string, object, double)"/>
        /// <seealso cref="PerformanceHelper.GetCache"/>
        /// <seealso cref="PerformanceHelper.GetCache{T}"/>
        /// <seealso cref="PerformanceHelper.GetCachedJson{T}"/>
        /// <seealso cref="PerformanceHelper.RemoveCache"/>
        [TestMethod]
        public void CacheRoundTrip_SetGetJsonAndRemove_ReturnsExpectedValues()
        {
            #region implementation
            var key = $"PerformanceHelperTests:{Guid.NewGuid():N}";
            var jsonKey = $"{key}:json";
            var payload = new CachePayload { Name = "Alpha", Count = 2 };

            try
            {
                PerformanceHelper.SetCache(key, payload, 1.0);
                PerformanceHelper.SetCache(jsonKey, JsonConvert.SerializeObject(payload), 1.0);

                var raw = PerformanceHelper.GetCache(key);
                var typed = PerformanceHelper.GetCache<CachePayload>(key);
                var fromJson = PerformanceHelper.GetCachedJson<CachePayload>(jsonKey);

                Assert.AreSame(payload, raw);
                Assert.AreSame(payload, typed);
                Assert.IsNotNull(fromJson);
                Assert.AreEqual("Alpha", fromJson.Name);
                Assert.AreEqual(2, fromJson.Count);

                PerformanceHelper.RemoveCache(key);

                Assert.IsNull(PerformanceHelper.GetCache(key));
            }
            finally
            {
                PerformanceHelper.RemoveCache(key);
                PerformanceHelper.RemoveCache(jsonKey);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies managed cache keys are cleared by ResetManagedCache.
        /// </summary>
        /// <seealso cref="PerformanceHelper.SetCacheManageKey"/>
        /// <seealso cref="PerformanceHelper.ResetManagedCache"/>
        [TestMethod]
        public void ResetManagedCache_ManagedKey_RemovesCachedItem()
        {
            #region implementation
            var key = $"PerformanceHelperTests:managed:{Guid.NewGuid():N}";

            PerformanceHelper.SetCacheManageKey(key, "managed-value", 1.0);
            Assert.AreEqual("managed-value", PerformanceHelper.GetCache(key));

            PerformanceHelper.ResetManagedCache();

            Assert.IsNull(PerformanceHelper.GetCache(key));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies invalid JSON cache keys throw the documented exception.
        /// </summary>
        /// <seealso cref="PerformanceHelper.GetCachedJson{T}"/>
        [TestMethod]
        public void GetCachedJson_EmptyKey_ThrowsInvalidOperationException()
        {
            #region implementation
            Assert.ThrowsException<InvalidOperationException>(() => PerformanceHelper.GetCachedJson<CachePayload>(""));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Simple cache payload for JSON round-trip tests.
        /// </summary>
        private sealed class CachePayload
        {
            /**************************************************************/
            /// <summary>
            /// Gets or sets the payload name.
            /// </summary>
            public string Name { get; set; } = string.Empty;

            /**************************************************************/
            /// <summary>
            /// Gets or sets the payload count.
            /// </summary>
            public int Count { get; set; }
        }

        #endregion
    }
}
