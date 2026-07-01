using MedRecProImportClass.Service.ParsingServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MedRecProImportClass.Models.Label;

namespace MedRecPro.Service.Test.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Tests section content and media parser public contracts.
    /// </summary>
    /// <remarks>
    /// Exercises single, bulk, and staged content entry points plus direct media helper methods.
    /// </remarks>
    /// <seealso cref="SectionContentParser"/>
    /// <seealso cref="SectionMediaParser"/>
    [TestClass]
    public class SectionContentParserTests
    {
        /**************************************************************/
        /// <summary>
        /// Verifies the main content parser creates section text content in all parser modes.
        /// </summary>
        /// <param name="modeName">Parser mode name.</param>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="SectionContentParser.ParseAsync"/>
        [DataTestMethod]
        [DataRow(nameof(ParserMode.SingleCall))]
        [DataRow(nameof(ParserMode.Bulk))]
        [DataRow(nameof(ParserMode.StagedBulk))]
        public async Task SectionContentParser_ParseAsync_RichText_CreatesContentAcrossModes(string modeName)
        {
            #region implementation
            var mode = Enum.Parse<ParserMode>(modeName);
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext(mode);
            await database.SeedCommonContextAsync(parseContext);

            var result = await new SectionContentParser().ParseAsync(
                ParsingServiceTestHelper.SectionWithRichText(),
                parseContext,
                null);

            if (mode == ParserMode.StagedBulk)
            {
                await parseContext.CommitDeferredChangesAsync();
            }

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
            Assert.IsTrue(await database.DbContext.Set<SectionTextContent>().CountAsync() > 0);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies direct single and bulk content helper methods create content structures.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="SectionContentParser_SingleCalls.GetOrCreateSectionTextContentsAsync"/>
        /// <seealso cref="SectionContentParser_BulkCalls.GetOrCreateSectionTextContentsAsync"/>
        /// <seealso cref="SectionContentParser_StagedBulkCalls.StageSectionTextContentsAsync"/>
        [TestMethod]
        public async Task SectionContentDelegates_PublicContentHelpers_CreateOrStageContent()
        {
            #region implementation
            await assertContentHelperCreatesContentAsync(ParserMode.SingleCall);
            await assertContentHelperCreatesContentAsync(ParserMode.Bulk);
            await assertContentHelperCreatesContentAsync(ParserMode.StagedBulk);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies list and table helper methods create child structures for both single and bulk implementations.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="SectionContentParser_SingleCalls.GetOrCreateTextListAndItemsAsync"/>
        /// <seealso cref="SectionContentParser_SingleCalls.GetOrCreateTextTableAndChildrenAsync"/>
        /// <seealso cref="SectionContentParser_BulkCalls.GetOrCreateTextListAndItemsAsync"/>
        /// <seealso cref="SectionContentParser_BulkCalls.GetOrCreateTextTableAndChildrenAsync"/>
        [TestMethod]
        public async Task SectionContentDelegates_ListAndTableHelpers_CreateChildren()
        {
            #region implementation
            await assertListAndTableHelpersCreateChildrenAsync(useBulk: false);
            await assertListAndTableHelpersCreateChildrenAsync(useBulk: true);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies media parser creates observation media and rendered media links.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="SectionMediaParser.ParseObservationMediaAsync"/>
        /// <seealso cref="SectionMediaParser.ParseRenderedMediaAsync"/>
        /// <seealso cref="SectionMediaParser.ParseAsync"/>
        [TestMethod]
        public async Task SectionMediaParser_ParseObservationAndRenderedMedia_CreatesMediaRows()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var seed = await database.SeedCommonContextAsync(parseContext);
            var mediaSection = ParsingServiceTestHelper.SectionWithMedia();
            var parser = new SectionMediaParser();

            var parseResult = await parser.ParseAsync(mediaSection, parseContext);
            var textContent = new SectionTextContent
            {
                SectionID = seed.Section.SectionID,
                ContentType = "Paragraph",
                ContentText = "Image placeholder"
            };
            database.DbContext.Set<SectionTextContent>().Add(textContent);
            await database.DbContext.SaveChangesAsync();
            var paragraph = mediaSection.Descendants(ParsingServiceTestHelper.Spl + "paragraph").Single();
            var renderedCount = await parser.ParseRenderedMediaAsync(paragraph, textContent.SectionTextContentID!.Value, parseContext, true);

            Assert.IsTrue(parseResult.Success, string.Join(Environment.NewLine, parseResult.Errors));
            Assert.AreEqual(1, await database.DbContext.Set<ObservationMedia>().CountAsync());
            Assert.AreEqual(1, renderedCount);
            Assert.AreEqual(1, await database.DbContext.Set<RenderedMedia>().CountAsync());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Runs the content helper public method for the requested parser mode.
        /// </summary>
        /// <param name="mode">Parser mode to exercise.</param>
        /// <returns>A task representing the asynchronous assertion.</returns>
        /// <seealso cref="SectionContentParser_SingleCalls"/>
        /// <seealso cref="SectionContentParser_BulkCalls"/>
        /// <seealso cref="SectionContentParser_StagedBulkCalls"/>
        private static async Task assertContentHelperCreatesContentAsync(ParserMode mode)
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext(mode);
            var seed = await database.SeedCommonContextAsync(parseContext);
            var section = ParsingServiceTestHelper.SectionWithRichText();
            var text = section.Element(ParsingServiceTestHelper.Spl + "text")!;

            if (mode == ParserMode.SingleCall)
            {
                var parser = new SectionContentParser_SingleCalls();
                var result = await parser.GetOrCreateSectionTextContentsAsync(
                    text,
                    seed.Section.SectionID!.Value,
                    parseContext,
                    (_, _) => Task.FromResult(seed.Section));
                Assert.IsTrue(result.Item1.Count > 0);
            }
            else if (mode == ParserMode.Bulk)
            {
                var parser = new SectionContentParser_BulkCalls();
                var result = await parser.GetOrCreateSectionTextContentsAsync(
                    text,
                    seed.Section.SectionID!.Value,
                    parseContext);
                Assert.IsTrue(result.Item1.Count > 0);
            }
            else
            {
                var parser = new SectionContentParser_StagedBulkCalls();
                var result = await parser.StageSectionTextContentsAsync(
                    text,
                    seed.Section.SectionID!.Value,
                    parseContext);
                Assert.IsTrue(result.Item1.Count > 0);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Runs public list and table child helpers for single or bulk content parsers.
        /// </summary>
        /// <param name="useBulk">Whether to exercise the bulk implementation.</param>
        /// <returns>A task representing the asynchronous assertion.</returns>
        /// <seealso cref="SectionContentParser_SingleCalls"/>
        /// <seealso cref="SectionContentParser_BulkCalls"/>
        private static async Task assertListAndTableHelpersCreateChildrenAsync(bool useBulk)
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext(useBulk ? ParserMode.Bulk : ParserMode.SingleCall);
            var seed = await database.SeedCommonContextAsync(parseContext);
            var sectionTextContent = new SectionTextContent
            {
                SectionID = seed.Section.SectionID,
                ContentType = "Container",
                ContentText = "Parent"
            };
            database.DbContext.Set<SectionTextContent>().Add(sectionTextContent);
            await database.DbContext.SaveChangesAsync();
            var richText = ParsingServiceTestHelper.SectionWithRichText();
            var list = richText.Descendants(ParsingServiceTestHelper.Spl + "list").First();
            var table = richText.Descendants(ParsingServiceTestHelper.Spl + "table").First();

            if (useBulk)
            {
                var parser = new SectionContentParser_BulkCalls();
                Assert.IsTrue(await parser.GetOrCreateTextListAndItemsAsync(list, sectionTextContent.SectionTextContentID!.Value, parseContext) > 0);
                Assert.IsTrue(await parser.GetOrCreateTextTableAndChildrenAsync(table, sectionTextContent.SectionTextContentID!.Value, parseContext) > 0);
            }
            else
            {
                var parser = new SectionContentParser_SingleCalls();
                Assert.IsTrue(await parser.GetOrCreateTextListAndItemsAsync(list, sectionTextContent.SectionTextContentID!.Value, parseContext) > 0);
                Assert.IsTrue(await parser.GetOrCreateTextTableAndChildrenAsync(table, sectionTextContent.SectionTextContentID!.Value, parseContext) > 0);
            }
            #endregion
        }
    }
}
