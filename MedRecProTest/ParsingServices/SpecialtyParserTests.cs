using MedRecProImportClass.Service.ParsingServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Tests relationship, compliance, licensing, attachment, certification, and specialty parser public contracts.
    /// </summary>
    /// <remarks>
    /// These parser families have narrow context requirements. The tests verify guard behavior
    /// and representative no-op/success paths through public ParseAsync entry points.
    /// </remarks>
    /// <seealso cref="BusinessOperationParser"/>
    /// <seealso cref="ToleranceSpecificationParser"/>
    [TestClass]
    public class SpecialtyParserTests
    {
        /**************************************************************/
        /// <summary>
        /// Verifies relationship and regulatory parsers return clear results for missing context.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="DocumentRelationshipParser.ParseAsync"/>
        /// <seealso cref="BusinessOperationParser.ParseAsync"/>
        /// <seealso cref="LicenseParser.ParseAsync"/>
        /// <seealso cref="ComplianceActionParser.ParseAsync"/>
        /// <seealso cref="DisciplinaryActionParser.ParseAsync"/>
        /// <seealso cref="AttachedDocumentParser.ParseAsync"/>
        /// <seealso cref="CertificationProductLinkParser.ParseAsync"/>
        [TestMethod]
        public async Task ContextSensitiveParsers_ParseAsync_MissingContext_ReturnResultsWithoutThrowing()
        {
            #region implementation
            var element = ParsingServiceTestHelper.Element("<subject xmlns=\"urn:hl7-org:v3\" />");
            var parseContext = new SplParseContext();
            var results = new[]
            {
                await new DocumentRelationshipParser().ParseAsync(element, parseContext, null),
                await new BusinessOperationParser().ParseAsync(element, parseContext, null),
                await new LicenseParser().ParseAsync(element, parseContext, null),
                await new ComplianceActionParser().ParseAsync(element, parseContext, null),
                await new DisciplinaryActionParser().ParseAsync(element, parseContext, null),
                await new AttachedDocumentParser().ParseAsync(element, parseContext, null),
                await new CertificationProductLinkParser().ParseAsync(element, parseContext, null)
            };

            Assert.IsTrue(results.All(result => result != null));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies specialty section parsers no-op successfully for nonmatching sections.
        /// </summary>
        /// <returns>A task representing the asynchronous test.</returns>
        /// <seealso cref="ToleranceSpecificationParser.ParseAsync"/>
        /// <seealso cref="REMSParser.ParseAsync"/>
        /// <seealso cref="WarningLetterParser.ParseAsync"/>
        [TestMethod]
        public async Task SpecialtySectionParsers_ParseAsync_NonmatchingSection_ReturnResults()
        {
            #region implementation
            using var database = ParsingServiceTestHelper.CreateDatabase();
            var parseContext = database.CreateParseContext();
            await database.SeedCommonContextAsync(parseContext);
            var section = ParsingServiceTestHelper.SectionWithRichText();
            var results = new[]
            {
                await new ToleranceSpecificationParser().ParseAsync(section, parseContext),
                await new REMSParser().ParseAsync(section, parseContext, null),
                await new WarningLetterParser().ParseAsync(section, parseContext)
            };

            Assert.IsTrue(results.All(result => result != null));
            #endregion
        }
    }
}
