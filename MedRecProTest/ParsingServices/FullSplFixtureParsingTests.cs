using MedRecProImportClass.Service.ParsingServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MedRecProImportClass.Models.Label;

namespace MedRecPro.Service.Test.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// End-to-end fixture tests for the copied ciprofloxacin SPL XML sample.
    /// </summary>
    /// <remarks>
    /// These tests keep stable fixture facts near the parser tests and verify the fixture is loaded
    /// from repo-local test data instead of a user-specific attachment path.
    /// </remarks>
    /// <seealso cref="DocumentSectionParser"/>
    /// <seealso cref="AuthorSectionParser"/>
    [TestClass]
    public class FullSplFixtureParsingTests
    {
        /**************************************************************/
        /// <summary>
        /// Verifies the copied SPL fixture exposes the expected stable raw XML counts.
        /// </summary>
        /// <seealso cref="ParsingServiceTestHelper.LoadFullSplDocument"/>
        [TestMethod]
        public void FullFixture_LoadedFromRepoLocalTestData_HasExpectedRawCounts()
        {
            #region implementation
            var document = ParsingServiceTestHelper.LoadFullSplDocument();

            Assert.AreEqual("document", document.Root!.Name.LocalName);
            Assert.AreEqual(77, document.Descendants(ParsingServiceTestHelper.Spl + "section").Count());
            Assert.AreEqual(4, document.Descendants(ParsingServiceTestHelper.Spl + "manufacturedProduct").Count());
            Assert.AreEqual(9, document.Descendants(ParsingServiceTestHelper.Spl + "ingredient").Count());
            Assert.AreEqual(3, document.Descendants(ParsingServiceTestHelper.Spl + "asContent").Count());
            Assert.AreEqual(4, document.Descendants(ParsingServiceTestHelper.Spl + "marketingAct").Count());
            Assert.AreEqual(3, document.Descendants(ParsingServiceTestHelper.Spl + "observationMedia").Count());
            Assert.AreEqual(0, document.Descendants(ParsingServiceTestHelper.Spl + "renderMultimedia").Count());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies document and author parsers extract fixture metadata and author organization.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="DocumentSectionParser.ParseAsync"/>
        /// <seealso cref="AuthorSectionParser.ParseAsync"/>
        [TestMethod]
        public async Task FullFixture_DocumentAndAuthorParsers_CreateExpectedCoreRows()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var document = ParsingServiceTestHelper.LoadFullSplDocument().Root!;
            parseContext.DocumentElement = document;

            var documentResult = await new DocumentSectionParser().ParseAsync(document, parseContext);
            var authorResult = await new AuthorSectionParser().ParseAsync(
                document.Elements(ParsingServiceTestHelper.Spl + "author").First(),
                parseContext,
                null);

            Assert.IsTrue(documentResult.Success, string.Join(Environment.NewLine, documentResult.Errors));
            Assert.IsTrue(authorResult.Success, string.Join(Environment.NewLine, authorResult.Errors));
            Assert.IsTrue(await database.DbContext.Set<Document>().AnyAsync(x => x.DocumentGUID == Guid.Parse("f16d9f01-d515-40fe-a0ff-ac70627cb512")));
            Assert.IsTrue(await database.DbContext.Set<Organization>().AnyAsync(x => x.OrganizationName == "A-S Medication Solutions"));
            #endregion
        }
    }
}
