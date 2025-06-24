
using System.Xml.Linq;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Service.ParsingServices.SplConstants; // Constant class for SPL elements and attributes
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.ParsingServices
{
    /// <summary>
    /// Parses the <structuredBody> element and orchestrates the parsing of its child sections.
    /// </summary>
    public class StructuredBodySectionParser : ISplSectionParser
    {
        public string SectionName => "structuredbody";

        private static readonly XNamespace ns = c.XML_NAMESPACE;

        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context)
        {
            var result = new SplParseResult();
            if (context.Document?.DocumentID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse structuredBody because no document context exists.");
                return result;
            }

            try
            {
                var structuredBody = new StructuredBody { DocumentID = context.Document.DocumentID.Value };

                var sbRepo = context.GetRepository<StructuredBody>();

                await sbRepo.CreateAsync(structuredBody);
                context.StructuredBody = structuredBody;

                // Delegate parsing of each top-level section to the SectionParser
                var sectionParser = new SectionParser();

                var sectionElements = element
                    .Elements(ns + sc.E.Component).Elements(ns + sc.E.Section);

                foreach (var sectionEl in sectionElements)
                {
                    var sectionResult = await sectionParser.ParseAsync(sectionEl, context);
                    result.MergeFrom(sectionResult);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing structuredBody: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <structuredBody> element.");
            }
            return result;
        }
    }
}
