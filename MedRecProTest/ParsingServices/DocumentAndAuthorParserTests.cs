using MedRecProImportClass.Service.ParsingServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MedRecProImportClass.Models.Label;

namespace MedRecPro.Service.Test.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Tests document and author parser public contracts.
    /// </summary>
    /// <remarks>
    /// Uses both the full SPL fixture for stable document metadata and focused author snippets
    /// for organization get-or-create behavior.
    /// </remarks>
    /// <seealso cref="DocumentSectionParser"/>
    /// <seealso cref="AuthorSectionParser"/>
    [TestClass]
    public class DocumentAndAuthorParserTests
    {
        #region Document Tests

        /**************************************************************/
        /// <summary>
        /// Verifies missing document parser dependencies return a failure result.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="DocumentSectionParser.ParseAsync"/>
        [TestMethod]
        public async Task DocumentSectionParser_ParseAsync_MissingLogger_ReturnsFailure()
        {
            #region implementation
            var parser = new DocumentSectionParser();

            var result = await parser.ParseAsync(ParsingServiceTestHelper.Element("<document xmlns=\"urn:hl7-org:v3\" />"), new SplParseContext());

            Assert.IsFalse(result.Success);
            StringAssert.Contains(result.Errors.Single(), "logger");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the full fixture document metadata is persisted through the parser.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="DocumentSectionParser.ParseAsync"/>
        [TestMethod]
        public async Task DocumentSectionParser_ParseAsync_FullFixture_CreatesExpectedMetadata()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var document = ParsingServiceTestHelper.LoadFullSplDocument().Root!;
            parseContext.DocumentElement = document;

            var result = await new DocumentSectionParser().ParseAsync(document, parseContext);

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
            Assert.AreEqual(1, result.DocumentsCreated);
            Assert.IsNotNull(parseContext.Document);
            Assert.AreEqual(Guid.Parse("f16d9f01-d515-40fe-a0ff-ac70627cb512"), parseContext.Document.DocumentGUID);
            Assert.AreEqual(Guid.Parse("cc768d3e-a7cc-46a2-ae6d-0f5eb2f05406"), parseContext.Document.SetGUID);
            Assert.AreEqual(9, parseContext.Document.VersionNumber);
            Assert.AreEqual(new DateTime(2025, 4, 30), parseContext.Document.EffectiveTime);
            StringAssert.Contains(parseContext.Document.Title!, "CIPROFLOXACIN");
            #endregion
        }

        #endregion

        #region Author Tests

        /**************************************************************/
        /// <summary>
        /// Verifies author parsing creates an organization and document-author link.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="AuthorSectionParser.ParseAsync"/>
        [TestMethod]
        public async Task AuthorSectionParser_ParseAsync_MinimalAuthor_CreatesOrganizationAndLink()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            await database.SeedCommonContextAsync(parseContext);

            var result = await new AuthorSectionParser().ParseAsync(
                ParsingServiceTestHelper.MinimalAuthor(),
                parseContext,
                null);

            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
            Assert.IsTrue(await database.DbContext.Set<Organization>().AnyAsync(x => x.OrganizationName == "A-S Medication Solutions"));
            Assert.AreEqual(1, await database.DbContext.Set<DocumentAuthor>().CountAsync());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies identifier-based organization lookup reuses the same organization.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="AuthorSectionParser.GetOrCreateOrganizationByIdentifierAsync"/>
        [TestMethod]
        public async Task GetOrCreateOrganizationByIdentifierAsync_ExistingIdentifier_ReusesOrganization()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            await database.SeedCommonContextAsync(parseContext);
            var orgElement = ParsingServiceTestHelper.MinimalAuthor()
                .Descendants(ParsingServiceTestHelper.Spl + "representedOrganization")
                .Single();

            var first = await AuthorSectionParser.GetOrCreateOrganizationByIdentifierAsync(orgElement, parseContext);
            var second = await AuthorSectionParser.GetOrCreateOrganizationByIdentifierAsync(orgElement, parseContext);

            Assert.IsTrue(first.Created);
            Assert.IsFalse(second.Created);
            Assert.AreEqual(first.Organization!.OrganizationID, second.Organization!.OrganizationID);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies name-based organization lookup reuses an existing organization name.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="AuthorSectionParser.GetOrCreateOrganizationByNameAsync"/>
        [TestMethod]
        public async Task GetOrCreateOrganizationByNameAsync_ExistingName_ReusesOrganization()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            var orgElement = ParsingServiceTestHelper.Element("""
                <representedOrganization xmlns="urn:hl7-org:v3">
                  <name>Reusable Organization</name>
                </representedOrganization>
                """);

            var first = await AuthorSectionParser.GetOrCreateOrganizationByNameAsync(orgElement, parseContext);
            var second = await AuthorSectionParser.GetOrCreateOrganizationByNameAsync(orgElement, parseContext);

            Assert.IsTrue(first.Created);
            Assert.IsFalse(second.Created);
            Assert.AreEqual(first.Organization!.OrganizationID, second.Organization!.OrganizationID);
            #endregion
        }

        #endregion
    }
}
