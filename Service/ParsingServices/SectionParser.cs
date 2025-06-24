using System.Xml.Linq;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.ParsingServices
{
    /// <summary>
    /// Parses a <section> element and its children, like <manufacturedProduct>.
    /// NOTE: This is a simplified version that does not handle recursive sub-sections.
    /// </summary>
    public class SectionParser : ISplSectionParser
    {
        public string SectionName => "section";
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context)
        {
            var result = new SplParseResult();
            if (context.StructuredBody?.StructuredBodyID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse section because no structuredBody context exists.");
                return result;
            }

            try
            {
                var section = new Section
                {
                    StructuredBodyID = context.StructuredBody.StructuredBodyID.Value,
                    Title = element.Element(ns + "title")?.Value.Trim(),
                    SectionCode = element.Element(ns + "code")?.Attribute("code")?.Value
                    // ... other section properties
                };

                var sectionRepo = context.GetRepository<Section>();
                await sectionRepo.CreateAsync(section);
                result.SectionsCreated++;

                // Set current section in context for child parsers
                var oldSection = context.CurrentSection;
                context.CurrentSection = section;

                // Delegate parsing of <manufacturedProduct> if it exists
                var productEl = element.Element(ns + "subject")?.Element(ns + "manufacturedProduct");
                if (productEl != null)
                {
                    var productParser = new ManufacturedProductParser();
                    var productResult = await productParser.ParseAsync(productEl, context);
                    result.MergeFrom(productResult);
                }

                // Restore context
                context.CurrentSection = oldSection;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing section: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <section> element.");
            }
            return result;
        }
    }
}
