using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Security
{
    /**************************************************************/
    /// <summary>
    /// Tests public throttle-state description output for each throttle level.
    /// </summary>
    /// <remarks>
    /// The test project uses the service's existing internal state-update boundary through
    /// an explicit friend-assembly relationship, then asserts the public description contract.
    /// </remarks>
    /// <seealso cref="ThrottleStateService"/>
    /// <seealso cref="ThrottleLevel"/>
    [TestClass]
    [TestCategory("Unit")]
    public class ThrottleStateServiceTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies state descriptions include level-specific guidance and usage data.
        /// </summary>
        /// <seealso cref="ThrottleStateService.GetStateDescription"/>
        [TestMethod]
        public void GetStateDescription_AllThrottleLevels_ReturnsExpectedGuidance()
        {
            #region implementation
            var sut = createService();

            assertDescription(sut, ThrottleLevel.None, 10, 90000, "Normal operations permitted.");
            assertDescription(sut, ThrottleLevel.Warning, 75, 25000, "Consider reducing non-essential operations.");
            assertDescription(sut, ThrottleLevel.Moderate, 85, 15000, "Non-critical operations should be rate-limited.");
            assertDescription(sut, ThrottleLevel.Aggressive, 92, 8000, "Only essential operations recommended.");
            assertDescription(sut, ThrottleLevel.Critical, 98, 2000, "Critical: All non-essential operations should be blocked.");
            assertDescription(sut, ThrottleLevel.CostLimit, 115, -15000, "COST LIMIT");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asserts the public description after the production state-update boundary applies metrics.
        /// </summary>
        /// <param name="sut">Service under test.</param>
        /// <param name="level">Throttle level to set.</param>
        /// <param name="percentUsed">Percent-used value to set.</param>
        /// <param name="remaining">Remaining vCore seconds to set.</param>
        /// <param name="expectedText">Expected level-specific text.</param>
        /// <seealso cref="ThrottleStateService.GetStateDescription"/>
        private static void assertDescription(
            ThrottleStateService sut,
            ThrottleLevel level,
            double percentUsed,
            double remaining,
            string expectedText)
        {
            #region implementation
            sut.UpdateState(percentUsed, remaining);

            var description = sut.GetStateDescription();

            Assert.AreEqual(level, sut.CurrentLevel);
            StringAssert.Contains(description, level.ToString());
            StringAssert.Contains(description, $"{percentUsed:F1}%");
            StringAssert.Contains(description, expectedText);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the throttle state service with deterministic thresholds.
        /// </summary>
        /// <returns>A throttle state service.</returns>
        /// <seealso cref="ThrottleStateService"/>
        private static ThrottleStateService createService()
        {
            #region implementation
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseUsageMonitor:MaxMonthlyCostPercent"] = "110"
                })
                .Build();

            return new ThrottleStateService(
                configuration,
                NullLogger<ThrottleStateService>.Instance);
            #endregion
        }

        #endregion
    }
}
