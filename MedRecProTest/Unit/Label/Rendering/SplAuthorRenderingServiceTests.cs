using MedRecPro.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Label.Rendering
{
    /**************************************************************/
    /// <summary>
    /// Tests SPL author rendering behavior using fixture-derived author data.
    /// </summary>
    /// <seealso cref="AuthorRenderingService"/>
    /// <seealso cref="DocumentAuthorDto"/>
    [TestClass]
    [TestCategory("Unit")]
    public class SplAuthorRenderingServiceTests
    {
        /**************************************************************/
        /// <summary>
        /// Initializes deterministic DTO decryption before each test.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation
            SplRenderingFixtureHelper.InitializeUtil();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies PrepareForRendering carries author organization data and handles absent child relationships.
        /// </summary>
        /// <seealso cref="AuthorRenderingService.PrepareForRendering"/>
        /// <seealso cref="SplRenderingFixtureHelper.LoadFixtureDocument"/>
        [TestMethod]
        public void PrepareForRendering_FixtureAuthor_ReturnsAuthorContext()
        {
            #region implementation
            var document = SplRenderingFixtureHelper.LoadFixtureDocument();
            var author = document.DocumentAuthors.Single();
            var service = new AuthorRenderingService(NullLogger.Instance);

            var result = service.PrepareForRendering(
                author,
                new List<DocumentRelationshipDto>(),
                new List<BusinessOperationDto>(),
                new List<FacilityProductLinkDto>());

            Assert.AreEqual("Lupin Pharmaceuticals, Inc.", result.AuthorOrganizationName);
            Assert.AreEqual("Labeler", result.AuthorType);
            Assert.AreEqual(0, result.AuthorIdentifiers.Count);
            Assert.AreEqual(0, result.ChildOrganizations.Count);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies PrepareAuthorsForRendering handles empty and populated author collections.
        /// </summary>
        /// <seealso cref="AuthorRenderingService.PrepareAuthorsForRendering"/>
        [TestMethod]
        public void PrepareAuthorsForRendering_EmptyAndPopulatedCollections_ReturnsExpectedCounts()
        {
            #region implementation
            var document = SplRenderingFixtureHelper.LoadFixtureDocument();
            var service = new AuthorRenderingService(NullLogger.Instance);

            var empty = service.PrepareAuthorsForRendering(
                new List<DocumentAuthorDto>(),
                new List<DocumentRelationshipDto>(),
                new List<BusinessOperationDto>(),
                new List<FacilityProductLinkDto>());
            var populated = service.PrepareAuthorsForRendering(
                document.DocumentAuthors,
                new List<DocumentRelationshipDto>(),
                new List<BusinessOperationDto>(),
                new List<FacilityProductLinkDto>());

            Assert.AreEqual(0, empty.Count);
            Assert.AreEqual(1, populated.Count);
            Assert.AreEqual("Lupin Pharmaceuticals, Inc.", populated[0].AuthorOrganizationName);
            #endregion
        }
    }
}
