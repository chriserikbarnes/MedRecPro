using System.Xml.Linq;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Helpers;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Main orchestrator for parsing SPL section elements and coordinating specialized parsers.
    /// This refactored parser delegates specific responsibilities to focused parsers while
    /// maintaining the core section creation and orchestration logic.
    /// </summary>
    /// <remarks>
    /// This parser serves as the main entry point for section parsing operations,
    /// coordinating the work of specialized parsers for content, media, indexing,
    /// and hierarchy processing. It maintains backward compatibility with existing
    /// interfaces while providing a cleaner, more maintainable architecture.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="SectionContentParser"/>
    /// <seealso cref="SectionMediaParser"/>
    /// <seealso cref="SectionIndexingParser"/>
    /// <seealso cref="SectionHierarchyParser"/>
    /// <seealso cref="Section"/>
    /// <seealso cref="SplParseContext"/>
    public class SectionParser : ISplSectionParser
    {
        #region Private Fields
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        /// <summary>
        /// Specialized parser for handling text content, lists, tables, and excerpts.
        /// </summary>
        private readonly SectionContentParser _contentParser;

        /// <summary>
        /// Specialized parser for handling indexing operations and cross-references.
        /// </summary>
        private readonly SectionIndexingParser _indexingParser;

        /// <summary>
        /// Specialized parser for handling section hierarchies and child relationships.
        /// </summary>
        private readonly SectionHierarchyParser _hierarchyParser;

        /// <summary>
        /// Specialized parser for handling multimedia content and media references.
        /// </summary>
        private readonly SectionMediaParser _mediaParser;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SectionParser with specialized parsers injected.
        /// </summary>
        /// <param name="contentParser">Parser for text content, lists, tables, and excerpts.</param>
        /// <param name="indexingParser">Parser for indexing operations and cross-references.</param>
        /// <param name="hierarchyParser">Parser for section hierarchies and child relationships.</param>
        /// <param name="mediaParser">Parser for multimedia content and media references.</param>
        public SectionParser(
            SectionContentParser? contentParser = null,
            SectionIndexingParser? indexingParser = null,
            SectionHierarchyParser? hierarchyParser = null,
            SectionMediaParser? mediaParser = null)
        {
            _contentParser = contentParser ?? new SectionContentParser();
            _indexingParser = indexingParser ?? new SectionIndexingParser();
            _hierarchyParser = hierarchyParser ?? new SectionHierarchyParser();
            _mediaParser = mediaParser ?? new SectionMediaParser();
        }

        /**************************************************************/
        /// <summary>
        /// Gets the section name for this parser, representing the main section element.
        /// </summary>
        public string SectionName => "section";

        /**************************************************************/
        /// <summary>
        /// Parses a section element from an SPL document, creating the section entity
        /// and orchestrating specialized parsers for different aspects of section processing.
        /// </summary>
        /// <param name="xEl">The XElement representing the section element to parse.</param>
        /// <param name="context">The current parsing context containing the structuredBody to link sections to.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <example>
        /// <code>
        /// var parser = new SectionParser(contentParser, indexingParser, hierarchyParser, mediaParser);
        /// var result = await parser.ParseAsync(sectionElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Sections created: {result.SectionsCreated}");
        ///     Console.WriteLine($"Content attributes: {result.SectionAttributesCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method orchestrates the following operations:
        /// 1. Validates the parsing context and creates the core Section entity
        /// 2. Delegates content processing to SectionContentParser
        /// 3. Delegates hierarchy processing to SectionHierarchyParser
        /// 4. Delegates media processing to SectionMediaParser
        /// 5. Delegates indexing processing to SectionIndexingParser
        /// 6. Handles product parsing and REMS protocol detection
        /// 7. Aggregates results from all specialized parsers
        /// </remarks>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="ManufacturedProductParser"/>
        /// <seealso cref="REMSParser"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement xEl,
            SplParseContext context,
            Action<string>? reportProgress = null)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate parsing context to ensure all required dependencies are available
            if (!ValidateContext(context, result))
            {
                return result;
            }

            try
            {
                if (xEl == null)
                {
                    result.Success = false;
                    result.Errors.Add("Invalid section element provided for parsing.");
                    return result;
                }

                // Report parsing start for monitoring and debugging purposes
                reportProgress?.Invoke($"Starting Section parsing for " +
                    $"{xEl?.GetSplElement(sc.E.Title)?.Value?.Replace("\t", " ") ?? xEl?.Name.LocalName ?? "Undefined"}, " +
                    $"file: {context.FileNameInZip}");

                // 1. Create the core Section entity from the XML element
                // Parse section metadata and persist the primary section entity
                var section = await CreateAndSaveSectionAsync(xEl, context);
                if (section?.SectionID == null)
                {
                    result.Success = false;
                    result.Errors.Add("Failed to create and save the Section entity.");
                    return result;
                }
                result.SectionsCreated++;

                // 2. Set context for child parsers and ensure it's restored
                // Manage parsing context state to provide section context to child parsers
                var oldSection = context.CurrentSection;
                context.CurrentSection = section;

                try
                {
                    // 3. Delegate to specialized parsers for different aspects of section processing

                    // Parse the content within this section (text, highlights, etc.)
                    var contentResult = await _contentParser.ParseAsync(xEl, context, reportProgress);
                    result.MergeFrom(contentResult);

                    // Parse section hierarchies and child sections
                    var hierarchyResult = await _hierarchyParser.ParseAsync(xEl, context, reportProgress);
                    result.MergeFrom(hierarchyResult);

                    // Parse media elements (observation media, rendered media)
                    var mediaResult = await _mediaParser.ParseAsync(xEl, context, reportProgress);
                    result.MergeFrom(mediaResult);

                    // Parse indexing information (pharmacologic classes, billing units, etc.)
                    var indexingResult = await _indexingParser.ParseAsync(xEl, context, reportProgress);
                    result.MergeFrom(indexingResult);

                    // 4. Parse the associated manufactured product, if it exists
                    // Process product information contained within the section
                    var productResult = await ParseManufacturedProductAsync(xEl, context, reportProgress);
                    result.MergeFrom(productResult);

                    // 5. Parse REMS protocols if applicable
                    // Check if this section contains REMS protocol elements
                    if (ContainsRemsProtocols(xEl))
                    {
                        var remsParser = new REMSParser();
                        var remsResult = await remsParser.ParseAsync(xEl, context, reportProgress);
                        result.MergeFrom(remsResult);
                    }
                }
                finally
                {
                    // Restore the context to prevent side effects for sibling or parent parsers
                    context.CurrentSection = oldSection;
                }

                // Report parsing completion for monitoring purposes
                reportProgress?.Invoke($"Section completed: {result.SectionAttributesCreated} attributes, " +
                    $"{result.SectionsCreated} sections for {context.FileNameInZip}");
            }
            catch (Exception ex)
            {
                // Handle unexpected errors and log them for debugging
                result.Success = false;
                result.Errors.Add($"An unexpected error occurred while parsing section: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing <section> element for {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        #region Core Section Processing Methods

        /**************************************************************/
        /// <summary>
        /// Validates the parsing context to ensure it's properly initialized.
        /// Checks for required dependencies and structured body context.
        /// </summary>
        /// <param name="context">The parsing context to validate.</param>
        /// <param name="result">The result object to add errors to if validation fails.</param>
        /// <returns>True if the context is valid; otherwise, false.</returns>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="Label"/>
        private bool ValidateContext(SplParseContext context, SplParseResult result)
        {
            #region implementation
            // Validate logger availability for error reporting and debugging
            if (context?.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context or its logger is null.");
                return false;
            }

            // Validate structured body context for section association
            if (context.StructuredBody?.StructuredBodyID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse section because no structuredBody context exists.");
                return false;
            }

            return true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new Section entity from the given XML element and saves it to the database.
        /// Extracts section metadata including GUID, codes, title, and effective time.
        /// </summary>
        /// <param name="xEl">The XElement representing the section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The created and saved Section entity, or null if creation failed.</returns>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<Section?> CreateAndSaveSectionAsync(XElement xEl, SplParseContext context)
        {
            #region implementation
            try
            {
                // Build section entity with extracted metadata from XML attributes and elements
                var section = new Section
                {
                    StructuredBodyID = context.StructuredBody!.StructuredBodyID!.Value,
                    SectionLinkGUID = xEl.GetAttrVal(sc.A.ID),
                    SectionGUID = Util.ParseNullableGuid(xEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root) ?? string.Empty) ?? Guid.Empty,
                    SectionCode = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue),
                    SectionCodeSystem = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem),
                    SectionDisplayName = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName) ?? string.Empty,
                    Title = xEl.GetSplElementVal(sc.E.Title)?.Trim(),
                    EffectiveTime = Util.ParseNullableDateTime(xEl.GetSplElementAttrVal(sc.E.EffectiveTime, sc.A.Value) ?? string.Empty) ?? DateTime.MinValue
                };

                // Persist section to database using repository pattern
                var sectionRepo = context.GetRepository<Section>();
                await sectionRepo.CreateAsync(section);

                // Return section if successfully created with valid ID
                return section.SectionID > 0 ? section : null;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error creating section entity");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds and delegates parsing of a manufacturedProduct element within the section.
        /// Navigates SPL hierarchy to locate product elements for specialized processing.
        /// </summary>
        /// <param name="sectionEl">The XElement of the section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>The SplParseResult from the product parser, or an empty result if no product exists.</returns>
        /// <seealso cref="ManufacturedProductParser"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> ParseManufacturedProductAsync(XElement sectionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            // Navigate through the SPL hierarchy: section/subject/manufacturedProduct
            // Follow standard SPL document structure to locate product elements
            var productEl = sectionEl.SplElement(sc.E.Subject, sc.E.ManufacturedProduct);

            // Delegate to specialized product parser if product element exists
            if (productEl != null)
            {
                var productParser = new ManufacturedProductParser();
                return await productParser.ParseAsync(productEl, context, reportProgress);
            }

            // Return a default successful result if no product element is found
            return new SplParseResult { Success = true };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if a section contains REMS protocol elements that should be parsed.
        /// </summary>
        /// <param name="sectionEl">The section XElement to check.</param>
        /// <returns>True if the section contains REMS protocols, false otherwise.</returns>
        /// <seealso cref="REMSParser"/>
        /// <seealso cref="Label"/>
        private static bool ContainsRemsProtocols(XElement sectionEl)
        {
            #region implementation
            // Check for REMS-specific elements that indicate this section should be processed by REMSParser
            return sectionEl.SplElements(sc.E.Subject2, sc.E.SubstanceAdministration, sc.E.ComponentOf, sc.E.Protocol).Any() ||
                   sectionEl.SplElements(sc.E.Subject, sc.E.ManufacturedProduct, sc.E.SubjectOf, sc.E.Document).Any();
            #endregion
        }

        #endregion
    }
}
