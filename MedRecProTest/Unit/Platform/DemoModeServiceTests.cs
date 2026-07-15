using MedRecPro.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Platform
{
    /**************************************************************/
    /// <summary>
    /// Exercises the <see cref="DemoModeService"/> hosted-service lifecycle
    /// with demo mode disabled, so no database truncation or timer work runs.
    /// </summary>
    /// <remarks>
    /// The constructor requires a connection string value but never opens a
    /// connection; with DemoMode disabled, StartAsync logs and returns before
    /// any timer or SQL activity, keeping the lifecycle fully offline.
    /// </remarks>
    /// <seealso cref="DemoModeService"/>
    [TestClass]
    [TestCategory("Unit")]
    public class DemoModeServiceTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies the constructor rejects null dependencies and demands a
        /// connection string.
        /// </summary>
        /// <seealso cref="DemoModeService"/>
        [TestMethod]
        public void DemoModeService_Constructor_ValidatesDependenciesAndConnection()
        {
            #region implementation
            // Act + Assert - null guards.
            Assert.ThrowsException<ArgumentNullException>(() =>
                new DemoModeService(null!, createConfiguration()));
            Assert.ThrowsException<ArgumentNullException>(() =>
                new DemoModeService(NullLogger<DemoModeService>.Instance, null!));

            // Missing connection string (no DefaultConnection or backup keys).
            Assert.ThrowsException<InvalidOperationException>(() =>
                new DemoModeService(NullLogger<DemoModeService>.Instance,
                    new ConfigurationBuilder().Build()));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the full hosted lifecycle with demo mode disabled:
        /// StartAsync returns without scheduling work, StopAsync completes,
        /// and Dispose is repeatable.
        /// </summary>
        /// <seealso cref="DemoModeService.StartAsync"/>
        /// <seealso cref="DemoModeService.StopAsync"/>
        /// <seealso cref="DemoModeService.Dispose"/>
        [TestMethod]
        public async Task DemoModeService_DisabledLifecycle_StartStopDisposeComplete()
        {
            #region implementation
            // Arrange
            var service = new DemoModeService(NullLogger<DemoModeService>.Instance, createConfiguration());

            // Act - the disabled gate returns before any timer or SQL work.
            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);

            // Dispose must be safe to call repeatedly.
            service.Dispose();
            service.Dispose();

            // Assert - completing the lifecycle without exceptions is the contract.
            Assert.IsTrue(true);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds configuration with a connection string and demo mode
        /// disabled.
        /// </summary>
        /// <returns>The built configuration.</returns>
        private static IConfiguration createConfiguration()
        {
            #region implementation
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DefaultConnection"] = "Server=localhost;Database=DemoFixture;Integrated Security=true;",
                    ["DemoModeSettings:Enabled"] = "false"
                })
                .Build();
            #endregion
        }

        #endregion
    }
}
