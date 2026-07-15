using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Label.Rendering
{
    /**************************************************************/
    /// <summary>
    /// Tests section rendering behavior using fixture-derived sections.
    /// </summary>
    /// <seealso cref="SectionRenderingService"/>
    /// <seealso cref="SectionDto"/>
    [TestClass]
    [TestCategory("Unit")]
    public class SplSectionRenderingServiceTests
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
        /// Verifies PrepareSectionForRendering computes ordered text, media, products, and highlight flags.
        /// </summary>
        /// <seealso cref="SectionRenderingService.PrepareSectionForRendering"/>
        /// <seealso cref="SplRenderingFixtureHelper.LoadFirstTextSection"/>
        [TestMethod]
        public void PrepareSectionForRendering_FixtureSection_ReturnsExpectedRenderingContext()
        {
            #region implementation
            var section = SplRenderingFixtureHelper.LoadFirstTextSection();
            section.ObservationMedia.AddRange(SplRenderingFixtureHelper.LoadFirstMediaSection().ObservationMedia);
            var service = new SectionRenderingService();
            var textService = SplRenderingFixtureHelper.CreateTextContentRenderingService();

            var result = service.PrepareSectionForRendering(
                section,
                textContentRenderingService: textService,
                isStandalone: true);

            Assert.AreEqual(section, result.Section);
            Assert.IsTrue(result.IsStandalone);
            Assert.AreEqual(section.SectionLinkGUID, result.SectionIdAttribute);
            Assert.IsTrue(result.HasSectionCode);
            Assert.IsTrue(result.HasTextContent);
            Assert.IsTrue(result.HasMedia);
            Assert.IsTrue(result.HasExcerptHighlights);
            Assert.IsTrue(result.HasRenderedTextContent);
            Assert.AreEqual("LOINC", result.SectionCodeSystemName);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies section helper methods handle ordering and missing data.
        /// </summary>
        /// <seealso cref="SectionRenderingService.GetOrderedTextContent"/>
        /// <seealso cref="SectionRenderingService.GetOrderedProducts"/>
        /// <seealso cref="SectionRenderingService.GetOrderedMedia"/>
        /// <seealso cref="SectionRenderingService.GetOrderedExcerptHighlights"/>
        [TestMethod]
        public void SectionHelpers_FixtureAndEmptySections_ReturnExpectedValues()
        {
            #region implementation
            var textSection = SplRenderingFixtureHelper.LoadFirstTextSection();
            var productSection = SplRenderingFixtureHelper.LoadFixtureDocument().StructuredBodies.Single().Sections.First(section => section.Products.Any());
            var mediaSection = SplRenderingFixtureHelper.LoadFirstMediaSection();
            var service = new SectionRenderingService();

            var highlights = service.GetOrderedExcerptHighlights(textSection)!.ToList();

            Assert.IsTrue(service.HasSectionCodeData(textSection));
            Assert.AreEqual(textSection.SectionLinkGUID, service.GenerateSectionIdAttribute(textSection));
            Assert.IsTrue(service.GetOrderedTextContent(textSection)!.Count > 0);
            Assert.IsTrue(service.GetOrderedProducts(productSection)!.Any());
            Assert.IsTrue(service.GetOrderedMedia(mediaSection)!.Any());
            Assert.AreEqual("<paragraph>Fixture warning highlight.</paragraph>", highlights[0].HighlightText);
            Assert.AreEqual("LOINC", service.GetSectionCodeSystemName(new SectionDto { Section = new Dictionary<string, object?>() }));
            Assert.AreEqual(string.Empty, service.GenerateSectionIdAttribute(new SectionDto { Section = new Dictionary<string, object?>() }));
            #endregion
        }
    }
}
