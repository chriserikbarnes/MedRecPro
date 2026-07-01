using MedRecProImportClass.Service.ParsingServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MedRecProImportClass.Models.Label;

namespace MedRecPro.Service.Test.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Tests structured body, section orchestration, hierarchy, and indexing parser contracts.
    /// </summary>
    /// <remarks>
    /// The tests verify mode routing and representative section persistence without testing private
    /// orchestration internals directly.
    /// </remarks>
    /// <seealso cref="StructuredBodySectionParser"/>
    /// <seealso cref="SectionParser"/>
    [TestClass]
    public class SectionOrchestrationParserTests
    {
        /**************************************************************/
        /// <summary>
        /// Verifies structured body parsing creates a structured body and delegates section parsing.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="StructuredBodySectionParser.ParseAsync"/>
        [TestMethod]
        public async Task StructuredBodySectionParser_ParseAsync_MinimalBody_CreatesStructuredBodyAndSection()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            await database.SeedCommonContextAsync(parseContext);
            parseContext.StructuredBody = null;
            var structuredBody = ParsingServiceTestHelper.Element("""
                <structuredBody xmlns="urn:hl7-org:v3">
                  <component>
                    <section>
                      <id root="44444444-4444-4444-4444-444444444444" />
                      <title>Minimal section</title>
                      <text><paragraph>Hello.</paragraph></text>
                    </section>
                  </component>
                </structuredBody>
                """);

            var result = await new StructuredBodySectionParser().ParseAsync(structuredBody, parseContext);

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
            Assert.IsNotNull(parseContext.StructuredBody);
            Assert.IsTrue(await database.DbContext.Set<Section>().CountAsync() >= 2);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies section parser mode routing returns successful results for representative sections.
        /// </summary>
        /// <param name="modeName">Parser mode name.</param>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="SectionParser.ParseAsync"/>
        /// <seealso cref="SectionParser_SingleCalls.ParseAsync"/>
        /// <seealso cref="SectionParser_BulkCalls.ParseAsync"/>
        /// <seealso cref="SectionParser_StagedBulk.ParseAsync"/>
        [DataTestMethod]
        [DataRow(nameof(ParserMode.SingleCall))]
        [DataRow(nameof(ParserMode.Bulk))]
        [DataRow(nameof(ParserMode.StagedBulk))]
        public async Task SectionParser_ParseAsync_RichTextSection_SucceedsAcrossModes(string modeName)
        {
            #region implementation
            var mode = Enum.Parse<ParserMode>(modeName);
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext(mode);
            await database.SeedCommonContextAsync(parseContext);

            var result = await new SectionParser().ParseAsync(
                ParsingServiceTestHelper.SectionWithRichText(),
                parseContext,
                null);

            if (mode == ParserMode.StagedBulk)
            {
                await parseContext.CommitDeferredChangesAsync();
            }

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
            Assert.IsTrue(await database.DbContext.Set<Section>().CountAsync() >= 2);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies hierarchy and indexing parsers return results for representative section XML.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="SectionHierarchyParser.ParseAsync"/>
        /// <seealso cref="SectionIndexingParser.ParseAsync"/>
        [TestMethod]
        public async Task HierarchyAndIndexingParsers_ParseAsync_RepresentativeSection_ReturnResults()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            await database.SeedCommonContextAsync(parseContext);
            var section = ParsingServiceTestHelper.SectionWithRichText();

            var hierarchyResult = await new SectionHierarchyParser().ParseAsync(section, parseContext, null);
            var indexingResult = await new SectionIndexingParser().ParseAsync(section, parseContext, null);

            Assert.IsNotNull(hierarchyResult);
            Assert.IsNotNull(indexingResult);
            #endregion
        }
    }
}
