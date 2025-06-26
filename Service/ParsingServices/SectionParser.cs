using System.Xml.Linq;
using MedRecPro.Helpers;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants; // Constant class for SPL elements and attributes


namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses a section element and its children, like manufacturedProduct.
    /// NOTE: This is a simplified version that does not handle recursive sub-sections.
    /// </summary>
    /// <remarks>
    /// This parser handles section elements within SPL documents, extracting section
    /// metadata and coordinating the parsing of contained manufacturedProduct elements.
    /// It manages context switching to ensure that product parsers have access to the
    /// current section being processed. This implementation is simplified and does not
    /// support nested sub-sections, focusing on direct child manufacturedProduct elements.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Section"/>
    /// <seealso cref="ManufacturedProductParser"/>
    /// <seealso cref="SplParseContext"/>
    public class SectionParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser, representing the section element.
        /// </summary>
        public string SectionName => "section";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses a section element from an SPL document, creating the section entity
        /// and orchestrating the parsing of its associated manufacturedProduct elements.
        /// </summary>
        /// <param name="xEl">The XElement representing the section element to parse.</param>
        /// <param name="context">The current parsing context containing the structuredBody to link sections to.</param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <example>
        /// <code>
        /// var parser = new SectionParser();
        /// var result = await parser.ParseAsync(sectionElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Sections created: {result.SectionsCreated}");
        ///     Console.WriteLine($"Products created: {result.ProductsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the following operations:
        /// 1. Validates that a structuredBody context exists
        /// 2. Extracts section metadata (GUID, codes, title, effective time)
        /// 3. Creates and saves the Section entity
        /// 4. Sets up context for manufacturedProduct parsing
        /// 5. Delegates manufacturedProduct parsing to specialized parsers
        /// 6. Aggregates results from child parsers
        /// 7. Restores context to prevent side effects
        /// 
        /// The method maintains proper context isolation and supports the delegation
        /// pattern for hierarchical SPL document parsing.
        /// </remarks>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="ManufacturedProductParser"/>
        public async Task<SplParseResult> ParseAsync(XElement xEl, SplParseContext context)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate that we have a valid structuredBody context to link sections to
            if (context.StructuredBody?.StructuredBodyID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse section because no structuredBody context exists.");
                return result;
            }

            try
            {
                // Create the Section entity with extracted metadata
                var section = new Section
                {
                    StructuredBodyID = context.StructuredBody.StructuredBodyID.Value,

                    // Extract section GUID from the id element's root attribute, defaulting to empty GUID
                    SectionGUID = Util.ParseNullableGuid(xEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root) ?? string.Empty) ?? Guid.Empty,

                    // Extract section code value from the code element's codeValue attribute
                    SectionCode = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue),

                    // Extract section code system from the code element's codeSystem attribute
                    SectionCodeSystem = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem),

                    // Extract section display name from the code element's displayName attribute, defaulting to empty string
                    SectionDisplayName = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName) ?? string.Empty,

                    // Extract and trim section title from the title element
                    Title = xEl.GetSplElementVal(sc.E.Title)?.Trim(),

                    // Extract and parse effective time from the effectiveTime element's value attribute, defaulting to DateTime.MinValue
                    EffectiveTime = Util.ParseNullableDateTime(xEl.GetSplElementAttrVal(sc.E.EffectiveTime, sc.A.Value) ?? string.Empty) ?? DateTime.MinValue
                };

                // Save the section entity to the database
                var sectionRepo = context.GetRepository<Section>();
                await sectionRepo.CreateAsync(section);
                result.SectionsCreated++;

                // Set current section in context for child parsers
                // Store the previous section context to restore later
                var oldSection = context.CurrentSection;
                context.CurrentSection = section;

                // Delegate parsing of <manufacturedProduct> if it exists
                // Navigate through the SPL hierarchy: section/subject/manufacturedProduct
                var productEl = xEl.SplElement(sc.E.Subject, sc.E.ManufacturedProduct);

                if (productEl != null)
                {
                    // Create and delegate to the manufacturedProduct parser
                    var productParser = new ManufacturedProductParser();
                    var productResult = await productParser.ParseAsync(productEl, context);
                    result.MergeFrom(productResult); // Aggregate results from product parsing
                }

                // Restore the previous section context to avoid side effects on other parsers
                context.CurrentSection = oldSection;
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during section parsing
                result.Success = false;
                result.Errors.Add($"Error parsing section: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <section> element.");
            }

            return result;
            #endregion
        }
    }
}