using MedRecPro.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests text content rendering behavior with fixture text and media references.
    /// </summary>
    /// <seealso cref="TextContentRenderingService"/>
    /// <seealso cref="SectionTextContentDto"/>
    [TestClass]
    public class SplTextContentRenderingServiceTests
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
        /// Verifies fixture paragraph text content is analyzed and rendered as a paragraph.
        /// </summary>
        /// <seealso cref="TextContentRenderingService.DetermineContentType"/>
        /// <seealso cref="TextContentRenderingService.AnalyzeContentCharacteristics(SectionTextContentDto)"/>
        /// <seealso cref="TextContentRenderingService.PrepareTextContentItemForRendering(SectionTextContentDto)"/>
        [TestMethod]
        public void PrepareTextContentItemForRendering_FixtureParagraph_ReturnsParagraphRendering()
        {
            #region implementation
            var textContent = SplRenderingFixtureHelper.LoadFirstTextSection().TextContents.First();
            var service = SplRenderingFixtureHelper.CreateTextContentRenderingService();

            var characteristics = service.AnalyzeContentCharacteristics(textContent);
            var result = service.PrepareTextContentItemForRendering(textContent);
            var collectionResult = service.PrepareTextContentForRendering(new[] { textContent });

            Assert.AreEqual("Paragraph", service.DetermineContentType(textContent));
            Assert.IsTrue(characteristics.HasContentText);
            Assert.IsFalse(characteristics.HasReferencedObject);
            Assert.AreEqual(TextContentRenderingAction.RenderParagraph, result.RenderingAction);
            Assert.AreEqual(1, collectionResult.Count);
            Assert.IsTrue(result.ProcessedContentText.Contains("pregnancy", StringComparison.OrdinalIgnoreCase));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies multimedia content resolves rendered media references against observation media.
        /// </summary>
        /// <seealso cref="TextContentRenderingService.PrepareTextContentForRendering(IEnumerable{SectionTextContentDto}?, IEnumerable{ObservationMediaDto}?)"/>
        /// <seealso cref="TextContentRenderingService.AnalyzeContentCharacteristics(SectionTextContentDto, IEnumerable{ObservationMediaDto}?)"/>
        [TestMethod]
        public void PrepareTextContentForRendering_MultimediaWithObservationMedia_ResolvesMediaIds()
        {
            #region implementation
            var service = SplRenderingFixtureHelper.CreateTextContentRenderingService();
            var media = new ObservationMediaDto
            {
                ObservationMedia = new Dictionary<string, object?>
                {
                    ["EncryptedObservationMediaID"] = SplRenderingFixtureHelper.EncryptedId(9001),
                    ["EncryptedMediaID"] = SplRenderingFixtureHelper.EncryptedText("IMGID3621"),
                    [nameof(ObservationMediaDto.FileName)] = SplRenderingFixtureHelper.FixtureDocumentGuid + "-01.jpg"
                }
            };
            var multimedia = SplRenderingFixtureHelper.CreateMultimediaTextContent(8001, 9001);

            var characteristics = service.AnalyzeContentCharacteristics(multimedia, new[] { media });
            var itemResult = service.PrepareTextContentItemForRendering(multimedia, new[] { media });
            var result = service.PrepareTextContentForRendering(new[] { multimedia }, new[] { media }).Single();

            Assert.IsTrue(characteristics.HasReferencedObject);
            Assert.AreEqual("IMGID3621", characteristics.ReferencedObjectId);
            Assert.AreEqual("IMGID3621", itemResult.ReferencedObjectId);
            Assert.AreEqual(TextContentRenderingAction.RenderMultiMedia, result.RenderingAction);
            CollectionAssert.AreEqual(new List<string> { "IMGID3621" }, result.ResolvedMediaIds!.ToList());
            #endregion
        }
    }
}
