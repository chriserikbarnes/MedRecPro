using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Platform
{
    /**************************************************************/
    /// <summary>
    /// Exercises <see cref="AzureSqlMetricsService"/> deterministically by
    /// pre-seeding its injected <see cref="IMemoryCache"/> with the exact
    /// hourly cache keys, so every public method short-circuits before any
    /// Azure REST or SDK call.
    /// </summary>
    /// <remarks>
    /// Cache keys embed the UTC date and hour
    /// ("AzureSqlMetrics_{segment}_{yyyyMMdd_HH}"). Each scenario runs inside
    /// an hour-rollover guard: if the UTC hour changes mid-scenario, the
    /// scenario reruns with fresh keys instead of flaking. Cached values are
    /// seeded as doubles (or the exact value tuple) because IMemoryCache
    /// unboxing is type-exact.
    /// </remarks>
    /// <seealso cref="AzureSqlMetricsService"/>
    /// <seealso cref="AzureManagementTokenProvider"/>
    [TestClass]
    [TestCategory("Unit")]
    public class AzureSqlMetricsServiceTests
    {
        #region implementation

        /// <summary>
        /// Free-tier monthly limit mirrored from Constant.FREE_TIER_MONTHLY_LIMIT.
        /// </summary>
        private const double FreeTierLimit = 100000.0;

        /**************************************************************/
        /// <summary>
        /// Verifies the constructor rejects null dependencies in order.
        /// </summary>
        /// <seealso cref="AzureSqlMetricsService"/>
        [TestMethod]
        public void AzureSqlMetricsService_Constructor_NullArguments_Throw()
        {
            #region implementation
            // Arrange
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var configuration = createConfiguration();
            var tokenProvider = createTokenProvider();

            // Act + Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new AzureSqlMetricsService(null!, cache, tokenProvider));
            Assert.ThrowsException<ArgumentNullException>(() =>
                new AzureSqlMetricsService(configuration, null!, tokenProvider));
            Assert.ThrowsException<ArgumentNullException>(() =>
                new AzureSqlMetricsService(configuration, cache, null!));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the constructor demands the resource ID and metrics region
        /// configuration keys.
        /// </summary>
        /// <seealso cref="AzureSqlMetricsService"/>
        [TestMethod]
        public void AzureSqlMetricsService_Constructor_MissingConfiguration_Throws()
        {
            #region implementation
            // Arrange
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var tokenProvider = createTokenProvider();

            // Act + Assert - missing resource ID.
            var noResource = Assert.ThrowsException<InvalidOperationException>(() =>
                new AzureSqlMetricsService(
                    new ConfigurationBuilder().Build(), cache, tokenProvider));
            StringAssert.Contains(noResource.Message, "ResourceId");

            // Missing metrics region.
            var noRegion = Assert.ThrowsException<InvalidOperationException>(() =>
                new AzureSqlMetricsService(
                    new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Azure:SqlDatabase:ResourceId"] = "/subscriptions/s/resourceGroups/g/providers/Microsoft.Sql/servers/x/databases/d"
                    }).Build(), cache, tokenProvider));
            StringAssert.Contains(noRegion.Message, "MetricsRegion");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the remaining-free-tier getter returns the pre-seeded
        /// cached value without touching Azure.
        /// </summary>
        /// <seealso cref="AzureSqlMetricsService.GetRemainingFreeTierVCoreSecondsAsync"/>
        [TestMethod]
        public async Task AzureSqlMetricsService_GetRemainingFreeTierVCoreSecondsAsync_CacheHit_ReturnsCachedRemaining()
        {
            #region implementation
            await runWithStableHourAsync(async () =>
            {
                // Arrange
                using var cache = new MemoryCache(new MemoryCacheOptions());
                var service = createService(cache);
                cache.Set(cacheKey("RemainingFree"), 87500.0, TimeSpan.FromMinutes(5));

                // Act
                var remaining = await service.GetRemainingFreeTierVCoreSecondsAsync();

                // Assert
                Assert.AreEqual(87500.0, remaining);
            });
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the monthly-usage getter returns the pre-seeded cached
        /// value directly.
        /// </summary>
        /// <seealso cref="AzureSqlMetricsService.GetUsedVCoreSecondsThisMonthAsync"/>
        [TestMethod]
        public async Task AzureSqlMetricsService_GetUsedVCoreSecondsThisMonthAsync_CacheHit_ReturnsCachedUsed()
        {
            #region implementation
            await runWithStableHourAsync(async () =>
            {
                // Arrange
                using var cache = new MemoryCache(new MemoryCacheOptions());
                var service = createService(cache);
                cache.Set(cacheKey("MonthlyUsage"), 12500.0, TimeSpan.FromMinutes(5));

                // Act
                var used = await service.GetUsedVCoreSecondsThisMonthAsync();

                // Assert
                Assert.AreEqual(12500.0, used);
            });
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies monthly usage is derived from the cached remaining value
        /// (limit minus remaining) and cached under its own key.
        /// </summary>
        /// <seealso cref="AzureSqlMetricsService.GetUsedVCoreSecondsThisMonthAsync"/>
        [TestMethod]
        public async Task AzureSqlMetricsService_GetUsedVCoreSecondsThisMonthAsync_DerivedFromRemaining_ComputesAndCaches()
        {
            #region implementation
            await runWithStableHourAsync(async () =>
            {
                // Arrange - only the remaining key is seeded.
                using var cache = new MemoryCache(new MemoryCacheOptions());
                var service = createService(cache);
                cache.Set(cacheKey("RemainingFree"), 87500.0, TimeSpan.FromMinutes(5));

                // Act
                var used = await service.GetUsedVCoreSecondsThisMonthAsync();

                // Assert - derived (100000 - 87500) and now cached.
                Assert.AreEqual(FreeTierLimit - 87500.0, used);
                Assert.IsTrue(cache.TryGetValue(cacheKey("MonthlyUsage"), out double cachedUsed));
                Assert.AreEqual(12500.0, cachedUsed);
            });
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies derived usage clamps to zero when the reported remaining
        /// amount exceeds the free-tier limit.
        /// </summary>
        /// <seealso cref="AzureSqlMetricsService.GetUsedVCoreSecondsThisMonthAsync"/>
        [TestMethod]
        public async Task AzureSqlMetricsService_GetUsedVCoreSecondsThisMonthAsync_RemainingAboveLimit_ClampsToZero()
        {
            #region implementation
            await runWithStableHourAsync(async () =>
            {
                // Arrange
                using var cache = new MemoryCache(new MemoryCacheOptions());
                var service = createService(cache);
                cache.Set(cacheKey("RemainingFree"), 120000.0, TimeSpan.FromMinutes(5));

                // Act
                var used = await service.GetUsedVCoreSecondsThisMonthAsync();

                // Assert
                Assert.AreEqual(0.0, used);
            });
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the free-tier status tuple is computed from cached monthly
        /// usage.
        /// </summary>
        /// <seealso cref="AzureSqlMetricsService.GetFreeTierStatusAsync"/>
        [TestMethod]
        public async Task AzureSqlMetricsService_GetFreeTierStatusAsync_FromSeededMonthlyUsage_ComputesTuple()
        {
            #region implementation
            await runWithStableHourAsync(async () =>
            {
                // Arrange
                using var cache = new MemoryCache(new MemoryCacheOptions());
                var service = createService(cache);
                cache.Set(cacheKey("MonthlyUsage"), 25000.0, TimeSpan.FromMinutes(5));

                // Act
                var status = await service.GetFreeTierStatusAsync();

                // Assert
                Assert.AreEqual(25000.0, status.used);
                Assert.AreEqual(75000.0, status.remaining);
                Assert.AreEqual(25.0, status.percentUsed);
            });
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the throttle decision ladder using seeded usage: 75%
        /// engages the Warning level, 10% stays at None.
        /// </summary>
        /// <seealso cref="AzureSqlMetricsService.ShouldThrottleAsync"/>
        [TestMethod]
        public async Task AzureSqlMetricsService_ShouldThrottleAsync_ThresholdLadder_ReturnsExpectedLevels()
        {
            #region implementation
            await runWithStableHourAsync(async () =>
            {
                // Arrange - 75% usage exceeds the default 70% warning threshold.
                using var warningCache = new MemoryCache(new MemoryCacheOptions());
                var warningService = createService(warningCache);
                warningCache.Set(cacheKey("MonthlyUsage"), 75000.0, TimeSpan.FromMinutes(5));

                // Act
                var warning = await warningService.ShouldThrottleAsync();

                // Assert
                Assert.IsTrue(warning.shouldThrottle);
                Assert.AreEqual(ThrottleLevel.Warning, warning.throttleLevel);
                Assert.AreEqual(75.0, warning.percentUsed);

                // Arrange - 10% usage is below every threshold.
                using var quietCache = new MemoryCache(new MemoryCacheOptions());
                var quietService = createService(quietCache);
                quietCache.Set(cacheKey("MonthlyUsage"), 10000.0, TimeSpan.FromMinutes(5));

                // Act
                var quiet = await quietService.ShouldThrottleAsync();

                // Assert
                Assert.IsFalse(quiet.shouldThrottle);
                Assert.AreEqual(ThrottleLevel.None, quiet.throttleLevel);
                Assert.AreEqual(10.0, quiet.percentUsed);
            });
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the projected-cost calculation reports zero cost while the
        /// projection stays under the free-tier limit.
        /// </summary>
        /// <remarks>
        /// With 1000 vCore-seconds used, the projection cannot exceed the
        /// 100000 limit on any calendar day, so the zero-cost branch is
        /// deterministic year-round.
        /// </remarks>
        /// <seealso cref="AzureSqlMetricsService.GetProjectedMonthlyCostAsync"/>
        [TestMethod]
        public async Task AzureSqlMetricsService_GetProjectedMonthlyCostAsync_LowUsage_ProjectsZeroCost()
        {
            #region implementation
            await runWithStableHourAsync(async () =>
            {
                // Arrange
                using var cache = new MemoryCache(new MemoryCacheOptions());
                var service = createService(cache);
                cache.Set(cacheKey("MonthlyUsage"), 1000.0, TimeSpan.FromMinutes(5));

                // Act
                var projection = await service.GetProjectedMonthlyCostAsync();

                // Assert - no overage, so no cost; days elapsed mirrors UTC today.
                Assert.AreEqual(0.0, projection.projectedCost);
                Assert.AreEqual(DateTime.UtcNow.Day, projection.daysElapsed);
                Assert.IsTrue(projection.projectedMonthlyUsage > 0.0);
                Assert.IsTrue(projection.projectedMonthlyUsage < FreeTierLimit);
            });
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes the exact hourly cache key the service uses for a segment.
        /// </summary>
        /// <param name="segment">Key segment (RemainingFree, MonthlyUsage, FreeTierStatus).</param>
        /// <returns>The full cache key including the UTC hour stamp.</returns>
        private static string cacheKey(string segment)
        {
            #region implementation
            return $"AzureSqlMetrics_{segment}_{DateTime.UtcNow:yyyyMMdd_HH}";
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Runs a scenario with an hour-rollover guard: if the UTC hour stamp
        /// changed while the scenario ran and it failed, the scenario reruns
        /// with fresh keys (up to two retries).
        /// </summary>
        /// <param name="scenario">The seeded cache scenario to execute.</param>
        private static async Task runWithStableHourAsync(Func<Task> scenario)
        {
            #region implementation
            for (var attempt = 0; ; attempt++)
            {
                var stampBefore = DateTime.UtcNow.ToString("yyyyMMdd_HH");

                try
                {
                    await scenario();
                    return;
                }
                catch (Exception) when (attempt < 2 && stampBefore != DateTime.UtcNow.ToString("yyyyMMdd_HH"))
                {
                    // The hour rolled over mid-scenario, invalidating the seeded
                    // keys; rerun with freshly computed keys.
                }
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a complete offline configuration for the metrics service and
        /// its token provider.
        /// </summary>
        /// <returns>The built configuration.</returns>
        private static IConfiguration createConfiguration()
        {
            #region implementation
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Azure:SqlDatabase:ResourceId"] = "/subscriptions/s/resourceGroups/g/providers/Microsoft.Sql/servers/x/databases/d",
                    ["Azure:SqlDatabase:MetricsRegion"] = "eastus",
                    ["Authentication:Microsoft:TenantId"] = "tenant-id",
                    ["Authentication:Microsoft:ClientId"] = "client-id",
                    ["Authentication:Microsoft:ClientSecret:Prod"] = "prod-secret"
                })
                .Build();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a real management token provider from the offline config
        /// (no network at construction).
        /// </summary>
        /// <returns>The token provider.</returns>
        /// <seealso cref="AzureManagementTokenProvider"/>
        private static AzureManagementTokenProvider createTokenProvider()
        {
            #region implementation
            return new AzureManagementTokenProvider(createConfiguration());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the metrics service under test over the supplied cache.
        /// </summary>
        /// <param name="cache">Memory cache to pre-seed.</param>
        /// <returns>The service under test.</returns>
        /// <seealso cref="AzureSqlMetricsService"/>
        private static AzureSqlMetricsService createService(IMemoryCache cache)
        {
            #region implementation
            return new AzureSqlMetricsService(createConfiguration(), cache, createTokenProvider());
            #endregion
        }

        #endregion
    }
}
