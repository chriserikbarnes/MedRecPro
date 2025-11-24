using System.Xml.Linq;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

using static MedRecPro.Models.Label;
using MedRecPro.Helpers;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses the structuredBody element and orchestrates the parsing of its child sections.
    /// </summary>
    /// <remarks>
    /// This parser handles the structuredBody element within SPL documents, which serves as
    /// the container for all section content. It creates the StructuredBody entity and
    /// coordinates the parsing of child section elements through delegation to specialized
    /// section parsers. The structuredBody acts as an intermediate layer in the SPL hierarchy
    /// between the document and its sections.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="StructuredBody"/>
    /// <seealso cref="SectionParser"/>
    /// <seealso cref="SplParseContext"/>
    public class StructuredBodySectionParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser, representing the structuredBody element.
        /// </summary>
        public string SectionName => "structuredbody";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        private SectionParser _sectionParser;

        /**************************************************************/
        public StructuredBodySectionParser(SectionParser? sectionParser = null)
        {
            _sectionParser = sectionParser ?? new SectionParser();
        }
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses a structuredBody element from an SPL document, creating the structuredBody entity
        /// and orchestrating the parsing of its associated section elements.
        /// </summary>
        /// <param name="element">The XElement representing the structuredBody element to parse.</param>
        /// <param name="context">The current parsing context containing the document to link the structuredBody to.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <example>
        /// <code>
        /// var parser = new StructuredBodySectionParser();
        /// var result = await parser.ParseAsync(structuredBodyElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Sections created: {result.SectionsCreated}");
        ///     Console.WriteLine($"Products created: {result.ProductsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the following operations:
        /// 1. Validates that a document context exists
        /// 2. Creates and saves the StructuredBody entity
        /// 3. Updates the parsing context with the new structuredBody
        /// 4. Locates all section elements within component wrappers
        /// 5. Delegates section parsing to specialized parsers
        /// 6. Aggregates results from all child section parsers
        /// 
        /// The method follows the SPL hierarchy where sections are contained within
        /// component elements inside the structuredBody.
        /// </remarks>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="StructuredBody"/>
        /// <seealso cref="SectionParser"/>
        public async Task<SplParseResult> ParseAsync(XElement element,
            SplParseContext context,
            Action<string>? reportProgress = null,
            bool? isParentCallingForAllSubElements = false)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate context
            if (context == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context is null or logger is null.");
                return result;
            }

            // Validate that we have a valid document context to link the structuredBody to
            if (context.Document?.DocumentID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse structuredBody because no document context exists.");
                return result;
            }

            try
            {
                reportProgress?.Invoke($"Starting Structured Body XML Elements {context.FileNameInZip}");

                // Create the StructuredBody entity linked to the current document
                var structuredBody = new StructuredBody { DocumentID = context.Document.DocumentID.Value };

                // Save the structuredBody entity to the database
                var sbRepo = context.GetRepository<StructuredBody>();
                await sbRepo.CreateAsync(structuredBody);

                // Update the parsing context with the newly created structuredBody
                context.StructuredBody = structuredBody;

                // CRITICAL: Perform section discovery if staged bulk operations are enabled
                // This must happen HERE (not in SectionParser) because this is the only place
                // where we have the complete structuredBody element. When SectionParser is called
                // recursively through SectionHierarchyParser, it only has a child section elements.
                if (context.UseBulkStagingOperations)
                {
                    context.Logger?.LogInformation("Starting section discovery for staged bulk operations");
                    reportProgress?.Invoke("Discovering all sections (Pass 1)...");

                    var discovery = XElementExtensions.DiscoverAllSections(element, context.Logger);

                    if (discovery != null && discovery.AllSections.Any())
                    {
                        context.Logger?.LogInformation(
                            "Section discovery complete: {SectionCount} sections at {MaxLevel} nesting levels",
                            discovery.AllSections.Count,
                            discovery.AllSections.Any() ? discovery.AllSections.Max(s => s.NestingLevel) + 1 : 0);

                        // Store discovery results in context for use by all section parsing operations
                        context.SectionDiscovery = discovery;

                        reportProgress?.Invoke($"Discovered {discovery.AllSections.Count} sections. Ready for bulk processing...");
                    }
                    else
                    {
                        context.Logger?.LogWarning("No sections discovered during Pass 1");
                    }
                }

                var parentSectionResult = await _sectionParser.ParseAsync(element,
                  context,
                  reportProgress,
                  isParentCallingForAllSubElements: true
                  );

                result.MergeFrom(parentSectionResult);

                reportProgress?.Invoke($"Completed Structured Body XML Elements {context.FileNameInZip}");
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during structuredBody parsing
                result.Success = false;
                result.Errors.Add($"Error parsing structuredBody: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <structuredBody> element.");
            }

            return result;
            #endregion
        }
    }
}