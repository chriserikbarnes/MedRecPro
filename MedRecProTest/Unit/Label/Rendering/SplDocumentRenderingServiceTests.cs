using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Label.Rendering
{
    /**************************************************************/
    /// <summary>
    /// Tests SPL document rendering behavior using the supplied label fixture.
    /// </summary>
    /// <seealso cref="DocumentRenderingService"/>
    /// <seealso cref="DocumentDto"/>
    [TestClass]
    [TestCategory("Unit")]
    public class SplDocumentRenderingServiceTests
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
        /// Verifies PrepareForRendering maps fixture document metadata and ordered children.
        /// </summary>
        /// <seealso cref="DocumentRenderingService.PrepareForRendering"/>
        /// <seealso cref="SplRenderingFixtureHelper.LoadFixtureDocument"/>
        [TestMethod]
        public void PrepareForRendering_FixtureDocument_ReturnsExpectedRenderingContext()
        {
            #region implementation
            var document = SplRenderingFixtureHelper.LoadFixtureDocument();
            var service = new DocumentRenderingService();

            var result = service.PrepareForRendering(document);

            Assert.AreEqual(SplRenderingFixtureHelper.FixtureDocumentGuid.ToString("D"), result.IdRoot);
            Assert.AreEqual(SplRenderingFixtureHelper.FixtureSetGuid.ToString("D"), result.SetIdRoot);
            Assert.AreEqual("20251003", result.EffectiveTimeFormatted);
            Assert.AreEqual("34391-3", result.DocumentCode);
            Assert.AreEqual("HUMAN PRESCRIPTION DRUG LABEL", result.DocumentDisplayName);
            Assert.AreEqual(24, result.VersionNumber);
            Assert.IsTrue(result.HasValidDocument);
            Assert.AreEqual("Lupin Pharmaceuticals, Inc.", result.PrimaryAuthorOrgName);
            Assert.AreEqual(1, service.GetOrderedAuthors(document)!.Count);
            Assert.AreEqual(1, service.GetOrderedStructuredBodies(document)!.Count);
            Assert.AreEqual(1, result.OrderedAuthors!.Count);
            Assert.AreEqual(1, result.OrderedStructuredBodies!.Count);
            Assert.IsTrue(result.DocumentTitle.Contains("AMLODIPINE AND BENAZEPRIL", StringComparison.OrdinalIgnoreCase));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies document validation reports missing identifiers.
        /// </summary>
        /// <seealso cref="DocumentRenderingService.HasValidDocument"/>
        /// <seealso cref="DocumentRenderingService.GenerateIdRootAttribute"/>
        [TestMethod]
        public void HasValidDocument_MissingIdentifiers_ReturnsFalse()
        {
            #region implementation
            var service = new DocumentRenderingService();
            var document = new DocumentDto
            {
                Document = new Dictionary<string, object?>
                {
                    [nameof(DocumentDto.DocumentCode)] = "34391-3"
                }
            };

            var result = service.PrepareForRendering(document);

            Assert.AreEqual(string.Empty, service.GenerateIdRootAttribute(document));
            Assert.IsFalse(service.HasValidDocument(document));
            Assert.AreEqual("DocumentGUID, SetGUID", result.ValidationErrorMessage);
            #endregion
        }
    }
}
