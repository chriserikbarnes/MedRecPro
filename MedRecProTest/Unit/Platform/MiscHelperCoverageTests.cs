using MedRecPro.Configuration;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Platform
{
    /**************************************************************/
    /// <summary>
    /// Covers small public helpers that had no direct test signal:
    /// <see cref="ConnectionString.Get"/> and
    /// <see cref="StructuredBodyViewModelFactory.Create"/>.
    /// </summary>
    /// <seealso cref="ConnectionString"/>
    /// <seealso cref="StructuredBodyViewModelFactory"/>
    [TestClass]
    [TestCategory("Unit")]
    public class MiscHelperCoverageTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies ConnectionString.Get never throws: empty names and unknown
        /// connection names both resolve to null or empty instead of erroring.
        /// </summary>
        /// <remarks>
        /// The helper swallows every failure by design (logging through
        /// ErrorHelper) and returns whatever it accumulated, which is null for
        /// an empty name and null-or-empty for an unknown name.
        /// </remarks>
        /// <seealso cref="ConnectionString.Get"/>
        [TestMethod]
        public void ConnectionString_Get_EmptyOrUnknownName_ReturnsNullOrEmptyWithoutThrowing()
        {
            #region implementation
            // Act
            var fromEmpty = ConnectionString.Get(string.Empty);
            var fromUnknown = ConnectionString.Get($"NoSuchConnectionFixture_{Guid.NewGuid():N}");

            // Assert - fail-soft contract: no exception, nothing usable returned.
            Assert.IsNull(fromEmpty);
            Assert.IsTrue(string.IsNullOrEmpty(fromUnknown),
                "Unknown connection names must resolve to a null or empty string.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the structured body view model factory rejects null input
        /// and produces a complete view model with organized (empty) section
        /// collections for a minimal DTO.
        /// </summary>
        /// <remarks>
        /// The factory is resolved through the production
        /// AddDocumentRenderingServices registration so the real hierarchy and
        /// structured-body services drive the organization logic.
        /// </remarks>
        /// <seealso cref="StructuredBodyViewModelFactory.Create"/>
        /// <seealso cref="SplRenderingServiceRegistration"/>
        [TestMethod]
        public void StructuredBodyViewModelFactory_Create_MinimalDto_ReturnsOrganizedViewModel()
        {
            #region implementation
            // Arrange - resolve the factory with its real collaborators.
            using var provider = new ServiceCollection()
                .AddLogging()
                .AddDocumentRenderingServices()
                .BuildServiceProvider();
            var factory = provider.GetRequiredService<IStructuredBodyViewModelFactory>();

            var dto = new StructuredBodyDto
            {
                StructuredBody = new Dictionary<string, object?>()
            };

            // Act
            var viewModel = factory.Create(dto);

            // Assert - complete view model with empty organized collections.
            Assert.IsNotNull(viewModel);
            Assert.AreSame(dto, viewModel.StructuredBody);
            Assert.IsFalse(viewModel.HasStandaloneSections);
            Assert.IsFalse(viewModel.HasHierarchicalSections);
            Assert.AreEqual(0, viewModel.AllSectionContexts?.Count ?? 0);

            // Null input is rejected.
            Assert.ThrowsException<ArgumentNullException>(() => factory.Create(null!));
            #endregion
        }

        #endregion
    }
}
