using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using static MedRecPro.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
using AngleSharp.Dom;
using System.Data;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

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

        /// <summary>
        /// Specialized parser for handling tolerance specifications and observation criteria.
        /// </summary>
        private readonly ToleranceSpecificationParser _toleranceParser;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SectionParser with specialized parsers injected.
        /// </summary>
        /// <param name="contentParser">Parser for text content, lists, tables, and excerpts.</param>
        /// <param name="indexingParser">Parser for indexing operations and cross-references.</param>
        /// <param name="hierarchyParser">Parser for section hierarchies and child relationships.</param>
        /// <param name="mediaParser">Parser for multimedia content and media references.</param>
        /// <param name="toleranceParser">Parser for tolerance specifications and observation criteria.</param>
        public SectionParser(
            SectionContentParser? contentParser = null,
            SectionIndexingParser? indexingParser = null,
            SectionHierarchyParser? hierarchyParser = null,
            SectionMediaParser? mediaParser = null,
            ToleranceSpecificationParser? toleranceParser = null)
        {
            _contentParser = contentParser ?? new SectionContentParser();
            _indexingParser = indexingParser ?? new SectionIndexingParser();
            _hierarchyParser = hierarchyParser ?? new SectionHierarchyParser();
            _mediaParser = mediaParser ?? new SectionMediaParser();
            _toleranceParser = toleranceParser ?? new ToleranceSpecificationParser();
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
        /// Routes to either bulk operations or single-call implementation based on context configuration.
        /// </summary>
        /// <param name="xEl">The XElement representing the section element to parse.</param>
        /// <param name="context">The current parsing context containing the structuredBody to link sections to.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
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
        /// 
        /// Routes between bulk operations (optimized for large documents) and single-call operations (simpler logic).
        /// Bulk operations reduce database calls from N to 2-3 per entity type.
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
            Action<string>? reportProgress = null,
            bool? isParentCallingForAllSubElements = false
           )
        {
            #region implementation

            var result = new SplParseResult();

            // Validate parsing context to ensure all required dependencies are available
            if (!validateContext(context, result))
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

                if (isParentCallingForAllSubElements ?? false)
                {
                    // Navigate through the SPL hierarchy to find section elements
                    // Path: structuredBody/component/section
                    var sectionElements = xEl.SplElements(sc.E.Component, sc.E.Section);

                    // Process sections based on feature flags - three-mode routing
                    if (sectionElements != null && sectionElements.Any())
                    {
                        if (context.UseBulkStaging)
                        {
                            // Mode 3: Staged bulk operations (discovery + flat processing)
                            // Enables the most optimized path with single XML traversal and flat bulk operations
                            // Pass the structuredBody element (xEl) so discovery can traverse entire tree
                            result = await parseAsync_StagedBulk(xEl, context, reportProgress);
                        }
                        else if (context.UseBulkOperations)
                        {
                            // Mode 2: Nested bulk operations (current working path)
                            // Uses bulk operations at each nesting level with recursive orchestration
                            result = await parseSectionAsync_bulkCalls(sectionElements.ToList(), context, reportProgress);
                        }
                        else
                        {
                            // Mode 1: N+1 pattern (legacy compatibility)
                            // Traditional single-call pattern for each section individually
                            foreach (var sectionEl in sectionElements)
                            {
                                result.MergeFrom(await parseSectionAsync_singleCalls(sectionEl, context, reportProgress));
                            }
                        }
                    }
                }
                else
                {
                    result = await parseSectionAsync_singleCalls(xEl, context, reportProgress);
                }
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

        #region Section Processing Methods - Single Operations (N + 1)

        /**************************************************************/
        /// <summary>
        /// Parses a single section element using individual database operations.
        /// </summary>
        /// <param name="xEl">The XElement representing the section to parse.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and metrics.</returns>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        async Task<SplParseResult> parseSectionAsync_singleCalls(XElement xEl,
           SplParseContext context,
           Action<string>? reportProgress)
        {
            #region implementation

            var result = new SplParseResult();

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
            var section = await createAndSaveSectionAsync(xEl, context);

            if (section?.SectionID == null)
            {
                result.Success = false;
                result.Errors.Add("Failed to create and save the Section entity.");
                return result;
            }

            result.SectionsCreated++;

            if (section != null)
                result.MergeFrom(await buildSectionContent(xEl, context, reportProgress, section));

            // Report parsing completion for monitoring purposes
            reportProgress?.Invoke($"Section completed: {result.SectionAttributesCreated} attributes, " +
                $"{result.SectionsCreated} sections for {context.FileNameInZip}");

            return result;

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
        private async Task<Section?> createAndSaveSectionAsync(XElement xEl, SplParseContext context)
        {
            #region implementation
            try
            {
                int? documentID = context.Document?.DocumentID ?? 0;

                // Build section entity with extracted metadata from XML attributes and elements
                var section = new Section
                {
                    DocumentID = documentID,
                    StructuredBodyID = context.StructuredBody!.StructuredBodyID!.Value,
                    SectionLinkGUID = xEl.GetAttrVal(sc.A.ID),
                    SectionGUID = Util.ParseNullableGuid(xEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root) ?? string.Empty) ?? Guid.Empty,
                    SectionCode = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue),
                    SectionCodeSystem = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem),
                    SectionCodeSystemName = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystemName),
                    SectionDisplayName = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName) ?? string.Empty,
                    Title = xEl.GetSplElementVal(sc.E.Title)?.Trim()
                };

                // Enhanced EffectiveTime parsing to handle both simple and complex structures
                parseEffectiveTime(xEl, section);

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

        #endregion

        #region Section Processing Methods - Bulk Operations

        /**************************************************************/
        /// <summary>
        /// Parses multiple section elements using bulk operations pattern. Collects all sections into memory,
        /// deduplicates against existing entities, then performs batch insert for optimal performance.
        /// </summary>
        /// <param name="sectionElements">The list of XElements representing section elements to parse.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and metrics for all sections processed.</returns>
        /// <remarks>
        /// Performance Pattern:
        /// - Before: N database calls (one per section)
        /// - After: 2 queries + 2 inserts (one per entity type)
        /// This method processes sections in bulk to minimize database round-trips and improve performance
        /// for documents with many sections.
        /// </remarks>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseSectionAsync_bulkCalls(
            List<XElement> sectionElements,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate inputs
            if (!validateBulkSectionInputs(sectionElements, context))
            {
                result.Success = false;
                result.Errors.Add("Invalid inputs for bulk section processing.");
                return result;
            }

            try
            {
                reportProgress?.Invoke($"Starting bulk section parsing for {sectionElements.Count} sections in {context.FileNameInZip}");

                var dbContext = context.ServiceProvider!.GetRequiredService<ApplicationDbContext>();

                #region parse sections structure into memory

                var sectionDtos = parseSectionsToMemory(sectionElements, context);

                #endregion

                #region bulk create sections

                var createdSections = await bulkCreateSectionsAsync(dbContext, sectionDtos, context);
                result.SectionsCreated = createdSections.Count;

                #endregion

                #region DEPRICATED process section content for all sections. Handled with phased parsing now.

                //foreach (var kvp in createdSections)
                //{
                //    var sectionEl = kvp.Key;
                //    var section = kvp.Value;

                //    if (section?.SectionID != null)
                //    {
                //        result.MergeFrom(await buildSectionContent(sectionEl, context, reportProgress, section));
                //    }
                //}

                #endregion

                #region process section content for all sections - phased approach

                // Phase 1: Document relationships and related documents
                await executeParserPhaseAsync(createdSections, context, reportProgress,
                    async (sectionEl, ctx, progress) =>
                    {
                        var phaseResult = new SplParseResult();
                        var docRelResult = await parseDocumentRelationshipAsync(sectionEl, ctx, progress);
                        phaseResult.MergeFrom(docRelResult);
                        var docLevelRelatedDocResult = await parseDocumentLevelRelatedDocumentsAsync(ctx);
                        phaseResult.MergeFrom(docLevelRelatedDocResult);
                        return phaseResult;
                    },
                    result);

                // Phase 2: Media parsing (must precede content parsing)
                await executeParserPhaseAsync(createdSections, context, reportProgress,
                    async (sectionEl, ctx, progress) => await _mediaParser.ParseAsync(sectionEl, ctx, progress),
                    result);

                // Phase 3: Content parsing (depends on media)
                await executeParserPhaseAsync(createdSections, context, reportProgress,
                    async (sectionEl, ctx, progress) => await _contentParser.ParseAsync(sectionEl, ctx, progress),
                    result);

                // Phase 4: Hierarchy parsing
                await executeParserPhaseAsync(createdSections, context, reportProgress,
                    async (sectionEl, ctx, progress) => await _hierarchyParser.ParseAsync(sectionEl, ctx, progress),
                    result);

                // Phase 5: Indexing parsing
                await executeParserPhaseAsync(createdSections, context, reportProgress,
                    async (sectionEl, ctx, progress) => await _indexingParser.ParseAsync(sectionEl, ctx, progress),
                    result);

                // Phase 6: Tolerance specifications (conditional)
                await executeParserPhaseAsync(createdSections, context, reportProgress,
                    async (sectionEl, ctx, progress) => await _toleranceParser.ParseAsync(sectionEl, ctx, progress),
                    result,
                    sectionFilter: (sectionEl, section) => containsToleranceSpecifications(sectionEl));

                // Phase 7: Warning letters
                await executeParserPhaseAsync(createdSections, context, reportProgress,
                    async (sectionEl, ctx, progress) => await parseWarningLetterContentAsync(sectionEl, ctx, progress),
                    result);

                // Phase 8: Compliance actions
                await executeParserPhaseAsync(createdSections, context, reportProgress,
                    async (sectionEl, ctx, progress) => await parseComplianceActionsAsync(sectionEl, ctx, progress),
                    result);

                // Phase 9: Certification links (conditional)
                await executeParserPhaseAsync(createdSections, context, reportProgress,
                    async (sectionEl, ctx, progress) => await parseCertificationLinksAsync(sectionEl, ctx, progress),
                    result,
                    sectionFilter: (sectionEl, section) => section.SectionCode == c.BLANKET_NO_CHANGES_CERTIFICATION_CODE);

                // Phase 10: Manufactured products
                await executeParserPhaseAsync(createdSections, context, reportProgress,
                    async (sectionEl, ctx, progress) => await parseManufacturedProductsAsync(sectionEl, ctx, progress),
                    result);

                // Phase 11: REMS protocols (conditional)
                await executeParserPhaseAsync(createdSections, context, reportProgress,
                    async (sectionEl, ctx, progress) =>
                    {
                        var remsParser = new REMSParser();
                        return await remsParser.ParseAsync(sectionEl, ctx, progress);
                    },
                    result,
                    sectionFilter: (sectionEl, section) => containsRemsProtocols(sectionEl));

                #endregion

                reportProgress?.Invoke($"Bulk section parsing completed: {result.SectionsCreated} sections, " +
                    $"{result.SectionAttributesCreated} attributes for {context.FileNameInZip}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"An unexpected error occurred during bulk section parsing: {ex.Message}");
                context?.Logger?.LogError(ex, "Error in bulk section processing for {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the input parameters for bulk section processing.
        /// </summary>
        /// <param name="sectionElements">The list of section XElements to validate.</param>
        /// <param name="context">The parsing context to validate.</param>
        /// <returns>True if all inputs are valid, false otherwise.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static bool validateBulkSectionInputs(List<XElement>? sectionElements, SplParseContext context)
        {
            #region implementation
            // Check for null or invalid parameters
            return sectionElements != null &&
                   sectionElements.Any() &&
                   context?.ServiceProvider != null &&
                   context?.StructuredBody?.StructuredBodyID != null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all section elements into memory without database operations.
        /// Extracts section metadata and creates DTO objects for bulk processing.
        /// </summary>
        /// <param name="sectionElements">The list of XElements representing sections to parse.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A list of tuples containing XElement and corresponding SectionDto for each section.</returns>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private List<(XElement Element, SectionDto Dto)> parseSectionsToMemory(
            List<XElement> sectionElements,
            SplParseContext context)
        {
            #region implementation

            var sectionDtos = new List<(XElement, SectionDto)>();
            int? documentID = context.Document?.DocumentID ?? 0;
            int structuredBodyID = context.StructuredBody!.StructuredBodyID!.Value;

            foreach (var xEl in sectionElements)
            {
                if (xEl == null)
                    continue;

                try
                {
                    // Extract section metadata from XML element
                    var dto = new SectionDto
                    {
                        DocumentID = documentID,
                        StructuredBodyID = structuredBodyID,
                        SectionLinkGUID = xEl.GetAttrVal(sc.A.ID),
                        SectionGUID = Util.ParseNullableGuid(xEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root) ?? string.Empty) ?? Guid.Empty,
                        SectionCode = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue),
                        SectionCodeSystem = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem),
                        SectionCodeSystemName = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystemName),
                        SectionDisplayName = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName) ?? string.Empty,
                        Title = xEl.GetSplElementVal(sc.E.Title)?.Trim()
                    };

                    // Parse effective time into DTO
                    parseSectionEffectiveTimeToDto(xEl, dto);

                    sectionDtos.Add((xEl, dto));
                }
                catch (Exception ex)
                {
                    context?.Logger?.LogWarning(ex, "Error parsing section element to memory, skipping section");
                }
            }

            return sectionDtos;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the effective time element from a section XElement into a SectionDto.
        /// Handles both simple value and low/high range structures.
        /// </summary>
        /// <param name="xEl">The XElement representing the section.</param>
        /// <param name="dto">The SectionDto to populate with effective time data.</param>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Util"/>
        /// <seealso cref="Label"/>
        private static void parseSectionEffectiveTimeToDto(XElement xEl, SectionDto dto)
        {
            #region implementation

            var effectiveTimeEl = xEl.GetSplElement(sc.E.EffectiveTime);
            if (effectiveTimeEl == null)
            {
                dto.EffectiveTime = DateTime.MinValue;
                return;
            }

            // Check for simple value attribute first
            var simpleValue = effectiveTimeEl.GetAttrVal(sc.A.Value);
            if (!string.IsNullOrEmpty(simpleValue))
            {
                dto.EffectiveTime = Util.ParseNullableDateTime(simpleValue) ?? DateTime.MinValue;
                return;
            }

            // Check for low/high structure
            var lowEl = effectiveTimeEl.GetSplElement(sc.E.Low);
            var highEl = effectiveTimeEl.GetSplElement(sc.E.High);

            if (lowEl != null || highEl != null)
            {
                // Parse low boundary
                dto.EffectiveTimeLow = lowEl != null
                    ? Util.ParseNullableDateTime(lowEl.GetAttrVal(sc.A.Value) ?? string.Empty)
                    : null;

                // Parse high boundary  
                dto.EffectiveTimeHigh = highEl != null
                    ? Util.ParseNullableDateTime(highEl.GetAttrVal(sc.A.Value) ?? string.Empty)
                    : null;

                // Set the main EffectiveTime to the low value for backward compatibility
                dto.EffectiveTime = dto.EffectiveTimeLow ?? DateTime.MinValue;
            }
            else
            {
                dto.EffectiveTime = DateTime.MinValue;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs bulk creation of Section entities, checking for existing sections
        /// and creating only missing ones in a single batch operation.
        /// </summary>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="sectionDtos">The list of tuples containing XElements and section DTOs parsed from XML.</param>
        /// <param name="context">The current parsing context for logging.</param>
        /// <returns>A dictionary mapping XElements to their corresponding created or existing Section entities.</returns>
        /// <seealso cref="Section"/>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<Dictionary<XElement, Section>> bulkCreateSectionsAsync(
            ApplicationDbContext dbContext,
            List<(XElement Element, SectionDto Dto)> sectionDtos,
            SplParseContext context)
        {
            #region implementation

            var resultMap = new Dictionary<XElement, Section>();

            if (!sectionDtos.Any())
                return resultMap;

            var sectionDbSet = dbContext.Set<Section>();
            int structuredBodyID = context.StructuredBody!.StructuredBodyID!.Value;

            // Query existing sections for this structured body
            var existingSections = await sectionDbSet
                .Where(s => s.StructuredBodyID == structuredBodyID)
                .Select(s => new
                {
                    s.SectionID,
                    s.SectionLinkGUID,
                    s.SectionGUID,
                    s.SectionCode,
                    s.Title
                })
                .ToListAsync();

            // Create lookup for existing sections by key attributes
            var existingLookup = new Dictionary<string, int>();
            foreach (var existing in existingSections)
            {
                // Use combination of key attributes to identify duplicates
                var key = createSectionLookupKey(
                    existing.SectionLinkGUID,
                    existing.SectionGUID,
                    existing.SectionCode,
                    existing.Title);

                if (!string.IsNullOrEmpty(key) && existing.SectionID.HasValue)
                {
                    existingLookup[key] = existing.SectionID.Value;
                }
            }

            // Identify new sections that don't exist in database
            var newSections = new List<Section>();
            var newSectionElements = new List<XElement>();

            foreach (var (element, dto) in sectionDtos)
            {
                var lookupKey = createSectionLookupKey(
                    dto.SectionLinkGUID,
                    dto.SectionGUID,
                    dto.SectionCode,
                    dto.Title);

                if (!string.IsNullOrEmpty(lookupKey) && existingLookup.ContainsKey(lookupKey))
                {
                    // Section already exists, retrieve it for content processing
                    var existingSectionId = existingLookup[lookupKey];
                    var existingSection = await sectionDbSet.FindAsync(existingSectionId);
                    if (existingSection != null)
                    {
                        resultMap[element] = existingSection;
                    }
                }
                else
                {
                    // Create new section entity from DTO
                    var newSection = createSectionFromDto(dto);
                    newSections.Add(newSection);
                    newSectionElements.Add(element);
                }
            }

            // Bulk insert new sections
            if (newSections.Any())
            {
                sectionDbSet.AddRange(newSections);
                await dbContext.SaveChangesAsync();

                // Map newly created sections to their elements
                for (int i = 0; i < newSections.Count; i++)
                {
                    if (newSections[i].SectionID > 0)
                    {
                        resultMap[newSectionElements[i]] = newSections[i];
                    }
                }
            }

            return resultMap;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a lookup key for section deduplication based on key identifying attributes.
        /// </summary>
        /// <param name="sectionLinkGUID">The section link GUID attribute.</param>
        /// <param name="sectionGUID">The section GUID.</param>
        /// <param name="sectionCode">The section code.</param>
        /// <param name="title">The section title.</param>
        /// <returns>A string key for lookup, or null if insufficient data to create key.</returns>
        /// <seealso cref="Section"/>
        /// <seealso cref="Label"/>
        private static string? createSectionLookupKey(
            string? sectionLinkGUID,
            Guid? sectionGUID,
            string? sectionCode,
            string? title)
        {
            #region implementation

            // Prefer SectionLinkGUID as primary identifier
            if (!string.IsNullOrWhiteSpace(sectionLinkGUID))
            {
                return $"LINK:{sectionLinkGUID}";
            }

            // Fall back to SectionGUID if available
            if (sectionGUID.HasValue && sectionGUID != Guid.Empty)
            {
                return $"GUID:{sectionGUID}";
            }

            // Use combination of code and title as last resort
            if (!string.IsNullOrWhiteSpace(sectionCode) && !string.IsNullOrWhiteSpace(title))
            {
                return $"CODE_TITLE:{sectionCode}:{title}";
            }

            // Unable to create reliable key
            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new Section entity from a SectionDto object.
        /// </summary>
        /// <param name="dto">The SectionDto containing section metadata.</param>
        /// <returns>A new Section entity with populated properties.</returns>
        /// <seealso cref="Section"/>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="Label"/>
        private static Section createSectionFromDto(SectionDto dto)
        {
            #region implementation

            var section = new Section
            {
                DocumentID = dto.DocumentID,
                StructuredBodyID = dto.StructuredBodyID,
                SectionLinkGUID = dto.SectionLinkGUID,
                SectionGUID = dto.SectionGUID,
                SectionCode = dto.SectionCode,
                SectionCodeSystem = dto.SectionCodeSystem,
                SectionCodeSystemName = dto.SectionCodeSystemName,
                SectionDisplayName = dto.SectionDisplayName,
                Title = dto.Title,
                EffectiveTime = dto.EffectiveTime,
                EffectiveTimeLow = dto.EffectiveTimeLow,
                EffectiveTimeHigh = dto.EffectiveTimeHigh
            };

            return section;

            #endregion
        }

        #endregion

        #region Section Processing Methods - Staged Bulk Operations

        /**************************************************************/
        /// <summary>
        /// Parses multiple section elements using staged bulk operations pattern.
        /// Implements a two-pass architecture: Pass 1 (Discovery) traverses entire section tree once,
        /// Pass 2 (Processing) processes all discovered sections with flat bulk operations.
        /// </summary>
        /// <param name="structuredBodyEl">The XElement representing the structuredBody containing all sections.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and metrics for all sections processed.</returns>
        /// <remarks>
        /// Performance Pattern:
        /// - Pass 1: Single XML traversal discovering all sections and hierarchies (memory only, no DB calls)
        /// - Pass 2: Flat bulk operations across all nesting levels (15-20 database operations total)
        /// 
        /// Improvement over Nested Bulk:
        /// - Before (Nested): ~200 database operations for 100 sections across 5 levels
        /// - After (Staged): ~15-20 database operations for same document
        /// - Result: 5-6× faster, 93% fewer database operations
        /// 
        /// This method eliminates recursive orchestration by processing all sections discovered
        /// in Pass 1 through flat bulk operations, regardless of nesting level.
        /// </remarks>
        /// <seealso cref="Section"/>
        /// <seealso cref="SectionDiscoveryResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElementExtensions.DiscoverAllSections"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseAsync_StagedBulk(
            XElement structuredBodyEl,
            SplParseContext context,
            Action<string>? reportProgress)
        {
            #region implementation

            var result = new SplParseResult();

            try
            {
                // Validate inputs
                if (structuredBodyEl == null)
                {
                    result.Success = false;
                    result.Errors.Add("StructuredBody element is null for staged bulk processing.");
                    return result;
                }

                if (context?.ServiceProvider == null || context.StructuredBody == null)
                {
                    result.Success = false;
                    result.Errors.Add("Required context dependencies not available for staged bulk processing.");
                    return result;
                }

                context.Logger?.LogInformation("Starting staged bulk section processing with discovery phase");
                reportProgress?.Invoke("Starting section discovery (Pass 1)...");

                // PASS 1: Discovery Phase - Single XML traversal to collect all sections and hierarchies
                // This phase performs no database operations, just collects metadata to memory
                var discovery = XElementExtensions.DiscoverAllSections(structuredBodyEl, context.Logger);

                if (discovery == null || !discovery.AllSections.Any())
                {
                    context.Logger?.LogWarning("No sections discovered during Pass 1");
                    return result; // Empty result, but success
                }

                context.Logger?.LogInformation(
                    "Discovery complete: {SectionCount} sections at {MaxLevel} nesting levels discovered",
                    discovery.AllSections.Count,
                    discovery.AllSections.Any() ? discovery.AllSections.Max(s => s.NestingLevel) + 1 : 0);

                // Store discovery results in context for use by all parsers
                context.SectionDiscovery = discovery;

                reportProgress?.Invoke($"Discovered {discovery.AllSections.Count} sections. Starting bulk processing (Pass 2)...");

                // PASS 2: Processing Phase - Flat bulk operations across all discovered sections

                // Task 5.1: Bulk create all sections ✅
                reportProgress?.Invoke("Creating sections (Pass 2a)...");
                await bulkCreateAllSectionsAsync(discovery, context, result);

                if (!result.Success)
                {
                    context.Logger?.LogError("Section creation failed, aborting staged bulk processing");
                    return result;
                }

                context.Logger?.LogInformation(
                    "Section creation complete: {Count} sections created",
                    result.SectionsCreated);

                // TODO: Implement remaining bulk processing methods in subsequent tasks:
                // Task 5.2: await bulkCreateAllHierarchiesAsync(discovery, context, result);
                // Task 5.3: await bulkProcessAllContentAsync(discovery, context, result, reportProgress);
                // Task 5.4: await bulkProcessAllMediaAsync(discovery, context, result, reportProgress);
                // Task 5.5: await bulkProcessAllIndexingAsync(discovery, context, result, reportProgress);

                // TEMPORARY: For now, continue with hierarchies and content using existing methods
                // This will be replaced as Tasks 5.2-5.5 are implemented
                context.Logger?.LogWarning(
                    "Tasks 5.2-5.5 not yet implemented. " +
                    "Hierarchies and content will be skipped for now.");

                reportProgress?.Invoke($"Staged bulk processing complete: {result.SectionsCreated} sections created");


            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error in staged bulk section processing: {ex.Message}");
                context?.Logger?.LogError(ex, "Error in parseAsync_StagedBulk");
            }

            return result;

            #endregion
        }

        #endregion

        #region Staged Bulk Helper Methods

        /**************************************************************/
        /// <summary>
        /// Orchestrates the bulk creation of all sections discovered during Pass 1.
        /// Coordinates validation, parsing, entity creation, persistence, and ID mapping.
        /// </summary>
        /// <param name="discovery">The discovery result containing all sections to create.</param>
        /// <param name="context">The parsing context containing database and logging services.</param>
        /// <param name="result">The parse result to update with metrics.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This orchestrator implements Phase 2a of staged bulk operations by coordinating:
        /// 1. Context validation and setup
        /// 2. Section parsing from XML elements
        /// 3. Entity conversion from DTOs
        /// 4. Bulk database insertion
        /// 5. ID mapping back to discovery GUIDs
        /// 
        /// Performance: O(1) database operation regardless of section count or nesting depth.
        /// Before: ~100 operations for 100 sections across 5 levels
        /// After: 1 operation for all sections
        /// 
        /// The orchestrator pattern enables:
        /// - Clear separation of concerns
        /// - Independent testing of each operation
        /// - Better error isolation and handling
        /// - Easier maintenance and modification
        /// </remarks>
        /// <seealso cref="SectionDiscoveryResult"/>
        /// <seealso cref="SectionDiscoveryDto"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        private async Task bulkCreateAllSectionsAsync(
            SectionDiscoveryResult discovery,
            SplParseContext context,
            SplParseResult result)
        {
            #region implementation

            if(context == null)
            {
                return;
            }

            try
            {
                // Step 1: Validate context and prerequisites
                if (!validateBulkOperationContext(discovery, context, result))
                {
                    return;
                }

                context.Logger?.LogInformation(
                    "Starting bulk section creation for {SectionCount} sections",
                    discovery.AllSections.Count);

                var dbContext = context?.ServiceProvider?.GetRequiredService<ApplicationDbContext>();

                // Step 2: Parse sections from XML elements into DTOs
                var (sectionDtos, sectionGuids) = parseSectionsFromDiscovery(
                    discovery,
                    context!,
                    result);

                if (!sectionDtos.Any())
                {
                    context?.Logger?.LogWarning("No valid sections to insert after parsing");
                    return;
                }

                // Step 3: Convert DTOs to entity models
                var sectionsToCreate = convertDtosToEntities(sectionDtos, context);

                // Step 4: Perform bulk insert operation
                await performBulkInsertAsync(sectionsToCreate, dbContext, context);

                // Step 5: Map database-generated IDs back to discovery GUIDs
                mapGeneratedIdsToGuids(
                    sectionsToCreate,
                    sectionGuids,
                    discovery,
                    context);

                // Update metrics
                result.SectionsCreated = sectionsToCreate.Count;

                context.Logger?.LogInformation(
                    "Bulk section creation complete: {Count} sections created, {MappedCount} IDs mapped",
                    sectionsToCreate.Count,
                    discovery.SectionIdsByGuid.Count);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error in bulk section creation: {ex.Message}");
                context.Logger?.LogError(ex, "Error in bulkCreateAllSectionsAsync");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that all required context and data are available for bulk section creation.
        /// </summary>
        /// <param name="discovery">The section discovery result to validate.</param>
        /// <param name="context">The parsing context to validate.</param>
        /// <param name="result">The parse result to update with validation errors.</param>
        /// <returns>True if validation passes; otherwise, false.</returns>
        /// <remarks>
        /// Validates:
        /// - Discovery result is not null and contains sections
        /// - Context and service provider are available
        /// - Document and structured body IDs are valid
        /// 
        /// Logs warnings for failed validations to aid debugging.
        /// </remarks>
        /// <seealso cref="SectionDiscoveryResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        private bool validateBulkOperationContext(
            SectionDiscoveryResult discovery,
            SplParseContext context,
            SplParseResult result)
        {
            #region implementation

            if (context == null || context.ServiceProvider == null)
            {
                context?.Logger?.LogWarning("Invalid context in bulkCreateAllSectionsAsync");
                return false;
            }

            if (discovery == null || !discovery.AllSections.Any())
            {
                context.Logger?.LogWarning("No sections to create in bulkCreateAllSectionsAsync");
                return false;
            }

            int documentID = context.Document?.DocumentID ?? 0;
            int structuredBodyID = context.StructuredBody?.StructuredBodyID ?? 0;

            if (documentID == 0 || structuredBodyID == 0)
            {
                result.Errors.Add("Invalid document or structured body ID for section creation");
                context.Logger?.LogWarning(
                    "Invalid IDs - DocumentID: {DocumentID}, StructuredBodyID: {StructuredBodyID}",
                    documentID,
                    structuredBodyID);
                return false;
            }

            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses section data from discovered XML elements into section DTOs.
        /// </summary>
        /// <param name="discovery">The discovery result containing XML elements to parse.</param>
        /// <param name="context">The parsing context with document IDs and logging.</param>
        /// <param name="result">The parse result to update with parsing errors.</param>
        /// <returns>A tuple containing the list of parsed section DTOs and their corresponding GUIDs.</returns>
        /// <remarks>
        /// Iterates through all discovered sections and:
        /// - Parses section metadata from source XElement
        /// - Creates SectionDto with document and structured body IDs
        /// - Tracks the original GUID for ID mapping
        /// - Logs warnings for sections that fail to parse
        /// 
        /// The GUID tracking is critical for mapping database-generated IDs back
        /// to the discovery result in subsequent operations.
        /// </remarks>
        /// <seealso cref="SectionDiscoveryResult"/>
        /// <seealso cref="SectionDiscoveryDto"/>
        /// <seealso cref="parseSectionFromElement"/>
        /// <seealso cref="SplParseContext"/>
        private (List<SectionDto> Dtos, List<Guid> Guids) parseSectionsFromDiscovery(
            SectionDiscoveryResult discovery,
            SplParseContext context,
            SplParseResult result)
        {
            #region implementation

            var sectionDtos = new List<SectionDto>();
            var sectionGuids = new List<Guid>();

            int? documentID = context.Document?.DocumentID;
            int? structuredBodyID = context.StructuredBody?.StructuredBodyID;

            if (documentID.HasValue && structuredBodyID.HasValue)
            {
                foreach (var discoveredSection in discovery.AllSections)
                {
                    try
                    {
                        // Parse section metadata from source XElement
                        var sectionDto = parseSectionFromElement(
                            discoveredSection.SourceElement,
                            documentID.Value,
                            structuredBodyID.Value);

                        sectionDtos.Add(sectionDto);
                        sectionGuids.Add(discoveredSection.SectionGuid);
                    }
                    catch (Exception ex)
                    {
                        context.Logger?.LogWarning(ex,
                            "Failed to parse section for GUID {SectionGuid}",
                            discoveredSection.SectionGuid);
                        result.Errors.Add($"Failed to parse section: {ex.Message}");
                    }
                }
            }
            return (sectionDtos, sectionGuids);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts section DTOs to Section entity models for database persistence.
        /// </summary>
        /// <param name="sectionDtos">The list of section DTOs to convert.</param>
        /// <param name="context">The parsing context for logging conversion issues.</param>
        /// <returns>A list of Section entities ready for bulk insertion.</returns>
        /// <remarks>
        /// Transforms parsed section data into entity models that match the database schema.
        /// Each DTO is converted using the createSectionFromDto method which handles:
        /// - Property mapping
        /// - Relationship setup
        /// - Entity initialization
        /// 
        /// Logs warnings for any DTOs that fail conversion to aid debugging.
        /// </remarks>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="createSectionFromDto"/>
        /// <seealso cref="SplParseContext"/>
        private List<Section> convertDtosToEntities(
            List<SectionDto> sectionDtos,
            SplParseContext context)
        {
            #region implementation

            var sectionsToCreate = new List<Section>();

            foreach (var sectionDto in sectionDtos)
            {
                try
                {
                    var section = createSectionFromDto(sectionDto);
                    sectionsToCreate.Add(section);
                }
                catch (Exception ex)
                {
                    context.Logger?.LogWarning(ex,
                        "Failed to create section entity from DTO");
                }
            }

            return sectionsToCreate;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs bulk insertion of section entities into the database.
        /// </summary>
        /// <param name="sections">The list of section entities to insert.</param>
        /// <param name="dbContext">The database context for the insert operation.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>A task representing the asynchronous insert operation.</returns>
        /// <remarks>
        /// Uses EF Core's AddRange for efficient batch insertion with OUTPUT clause
        /// to retrieve database-generated IDs. This is a single database round-trip
        /// regardless of the number of sections.
        /// 
        /// After SaveChangesAsync, EF Core automatically populates the SectionID
        /// property on each entity with the database-generated value.
        /// 
        /// Performance: O(1) database operation for any number of sections.
        /// </remarks>
        /// <seealso cref="Section"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="SplParseContext"/>
        private async Task performBulkInsertAsync(
            List<Section> sections,
            ApplicationDbContext dbContext,
            SplParseContext context)
        {
            #region implementation

            context.Logger?.LogInformation(
                "Performing bulk INSERT for {Count} sections",
                sections.Count);

            var sectionDbSet = dbContext.Set<Section>();
            sectionDbSet.AddRange(sections);
            await dbContext.SaveChangesAsync();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps database-generated section IDs back to their original discovery GUIDs.
        /// </summary>
        /// <param name="sections">The list of section entities with populated database IDs.</param>
        /// <param name="guids">The list of GUIDs in the same order as the sections.</param>
        /// <param name="discovery">The discovery result to update with ID mappings.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <remarks>
        /// Creates bidirectional mapping between:
        /// - XML GUIDs (from original SPL document)
        /// - Database IDs (generated during INSERT)
        /// 
        /// Updates:
        /// 1. discovery.SectionIdsByGuid - Lookup dictionary for subsequent bulk operations
        /// 2. discovery.SectionsByGuid[].SectionID - Individual DTO database IDs
        /// 
        /// This mapping is critical for all subsequent bulk operations that need to
        /// reference sections using either their XML GUID or database ID.
        /// 
        /// Logs warnings for any sections where ID population failed.
        /// </remarks>
        /// <seealso cref="Section"/>
        /// <seealso cref="SectionDiscoveryResult"/>
        /// <seealso cref="SectionDiscoveryDto"/>
        /// <seealso cref="SplParseContext"/>
        private void mapGeneratedIdsToGuids(
            List<Section> sections,
            List<Guid> guids,
            SectionDiscoveryResult discovery,
            SplParseContext context)
        {
            #region implementation

            for (int i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                var guid = guids[i];

                if (section.SectionID > 0)
                {
                    // Store in discovery lookup for use by all subsequent operations
                    discovery.SectionIdsByGuid[guid] = section.SectionID.Value;

                    // Update the discovery DTO with database ID
                    if (discovery.SectionsByGuid.TryGetValue(guid, out var discoveryDto))
                    {
                        discoveryDto.SectionID = section.SectionID.Value;
                    }
                }
                else
                {
                    context.Logger?.LogWarning(
                        "Section ID not populated for GUID {Guid}",
                        guid);
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a section XElement to create a SectionDto with all metadata.
        /// Helper method for bulk section creation.
        /// </summary>
        /// <param name="xEl">The XElement representing the section.</param>
        /// <param name="documentID">The document ID foreign key.</param>
        /// <param name="structuredBodyID">The structured body ID foreign key.</param>
        /// <returns>A SectionDto with parsed metadata.</returns>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        private SectionDto parseSectionFromElement(
            XElement xEl,
            int documentID,
            int structuredBodyID)
        {
            #region implementation

            var dto = new SectionDto
            {
                DocumentID = documentID,
                StructuredBodyID = structuredBodyID,
                SectionLinkGUID = xEl.GetAttrVal(sc.A.ID),
                SectionGUID = Util.ParseNullableGuid(xEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root) ?? string.Empty) ?? Guid.Empty,
                SectionCode = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue),
                SectionCodeSystem = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem),
                SectionCodeSystemName = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystemName),
                SectionDisplayName = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName) ?? string.Empty,
                Title = xEl.GetSplElementVal(sc.E.Title)?.Trim()
            };

            // Parse effective time into DTO
            parseSectionEffectiveTimeToDto(xEl, dto);

            return dto;

            #endregion
        }

        #endregion

        #region Supporting Classes

        /**************************************************************/
        /// <summary>
        /// Data transfer object for Section entity used in bulk operations.
        /// Contains all metadata needed to create a Section without database dependencies.
        /// </summary>
        /// <seealso cref="Section"/>
        /// <seealso cref="Label"/>
        private class SectionDto
        {
            /// <summary>
            /// Gets or sets the document ID foreign key.
            /// </summary>
            public int? DocumentID { get; set; }

            /// <summary>
            /// Gets or sets the structured body ID foreign key.
            /// </summary>
            public int StructuredBodyID { get; set; }

            /// <summary>
            /// Gets or sets the section link GUID attribute from XML.
            /// </summary>
            public string? SectionLinkGUID { get; set; }

            /// <summary>
            /// Gets or sets the section GUID identifier.
            /// </summary>
            public Guid SectionGUID { get; set; }

            /// <summary>
            /// Gets or sets the section code value.
            /// </summary>
            public string? SectionCode { get; set; }

            /// <summary>
            /// Gets or sets the section code system.
            /// </summary>
            public string? SectionCodeSystem { get; set; }

            /// <summary>
            /// Gets or sets the section code system name.
            /// </summary>
            public string? SectionCodeSystemName { get; set; }

            /// <summary>
            /// Gets or sets the section display name.
            /// </summary>
            public string SectionDisplayName { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the section title.
            /// </summary>
            public string? Title { get; set; }

            /// <summary>
            /// Gets or sets the effective time value.
            /// </summary>
            public DateTime? EffectiveTime { get; set; }

            /// <summary>
            /// Gets or sets the effective time low value.
            /// </summary>
            public DateTime? EffectiveTimeLow { get; set; }

            /// <summary>
            /// Gets or sets the effective time high value.
            /// </summary>
            public DateTime? EffectiveTimeHigh { get; set; }
        }

        #endregion

        #region Core Section Processing Methods

        /**************************************************************/
        /// <summary>
        /// Builds the content and child elements for a section by orchestrating specialized parsers
        /// to process different aspects of the section's data including media, content, hierarchies,
        /// indexing, products, and REMS protocols.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the section element to build content for.</param>
        /// <param name="context">The current parsing context containing the structuredBody and document information.</param>
        /// <param name="reportProgress">Optional action to report progress during content building.</param>
        /// <param name="result">The SplParseResult to merge child parsing results into.</param>
        /// <param name="section">The Section entity that has been created and saved to the database.</param>
        /// <returns>A SplParseResult aggregating the results from all specialized content parsers.</returns>
        /// <example>
        /// <code>
        /// var result = new SplParseResult();
        /// var section = new Label.Section { SectionID = 123, HasValue = true };
        /// var contentResult = await buildSectionContent(sectionElement, parseContext, null, result, section);
        /// if (contentResult.Success)
        /// {
        ///     Console.WriteLine($"Content attributes: {contentResult.SectionAttributesCreated}");
        ///     Console.WriteLine($"Media elements: {contentResult.MediaObservationsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method temporarily sets the CurrentSection in the parsing context to ensure all child
        /// parsers have access to the correct section context. The original context is always restored
        /// in the finally block to prevent side effects.
        /// 
        /// The method orchestrates the following specialized parsers in sequence:
        /// 1. DocumentRelationship parser - Establishes document relationship context
        /// 2. Document-level related documents parser - Processes index file relationships
        /// 3. SectionMediaParser - Processes observation media and rendered media elements
        /// 4. SectionContentParser - Processes text content, highlights, and formatting
        /// 5. SectionHierarchyParser - Processes child sections and hierarchical structure
        /// 6. SectionIndexingParser - Processes pharmacologic classes and billing units
        /// 7. ToleranceParser - Conditionally processes 40 CFR 180 tolerance specifications
        /// 8. Warning letter parser - Processes warning letter alert content
        /// 9. Compliance actions parser - Processes regulatory compliance actions
        /// 10. Certification links parser - Conditionally processes blanket certification links
        /// 11. ManufacturedProductParser - Processes product information within the section
        /// 12. REMSParser - Conditionally processes REMS protocol elements
        /// 
        /// Media parsing must precede content parsing to ensure media references are available
        /// when processing content elements.
        /// </remarks>
        /// <seealso cref="Label"/>
        /// <seealso cref="Label.Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SectionMediaParser"/>
        /// <seealso cref="SectionContentParser"/>
        /// <seealso cref="SectionHierarchyParser"/>
        /// <seealso cref="SectionIndexingParser"/>
        /// <seealso cref="ToleranceSpecificationParser"/>
        /// <seealso cref="ManufacturedProductParser"/>
        /// <seealso cref="REMSParser"/>
        /// <seealso cref="XElement"/>
        private async Task<SplParseResult> buildSectionContent(XElement sectionEl,
            SplParseContext context,
            Action<string>? reportProgress,
            Label.Section section)
        {
            #region implementation

            var result = new SplParseResult();

            // Set context for child parsers and ensure it's restored
            // Manage parsing context state to provide section context to child parsers
            var oldSection = context.CurrentSection;
            context.CurrentSection = section;

            try
            {
                // Before parsing products, check if this section requires a DocumentRelationship context.
                var docRelResult = await parseDocumentRelationshipAsync(sectionEl, context, reportProgress);
                result.MergeFrom(docRelResult);

                // Related docs for index files
                var docLevelRelatedDocResult = await parseDocumentLevelRelatedDocumentsAsync(context);
                result.MergeFrom(docLevelRelatedDocResult);

                // 3. Delegate to specialized parsers for different aspects of section processing

                // Parse media elements (observation media, rendered media) NOTE: this must
                // precede contentParser.
                var mediaResult = await _mediaParser.ParseAsync(sectionEl, context, reportProgress);
                result.MergeFrom(mediaResult);

                // Parse the content within this section (text, highlights, etc.)
                var contentResult = await _contentParser.ParseAsync(sectionEl, context, reportProgress);
                result.MergeFrom(contentResult);

                // Parse section hierarchies and child sections
                var hierarchyResult = await _hierarchyParser.ParseAsync(sectionEl, context, reportProgress);
                result.MergeFrom(hierarchyResult);

                // Parse indexing information (pharmacologic classes, billing units, etc.)
                var indexingResult = await _indexingParser.ParseAsync(sectionEl, context, reportProgress);
                result.MergeFrom(indexingResult);

                // Parse tolerance specifications and observation criteria for 40 CFR 180 documents
                // Check if this section contains tolerance specification elements
                if (containsToleranceSpecifications(sectionEl))
                {
                    var toleranceResult = await _toleranceParser.ParseAsync(sectionEl, context, reportProgress);
                    result.MergeFrom(toleranceResult);
                }

                // Parse warning letter information if this is a warning letter alert section 
                var warningLetterResult = await parseWarningLetterContentAsync(sectionEl, context, reportProgress);
                result.MergeFrom(warningLetterResult);

                // Parse compliance actions for this section
                var complianceResult = await parseComplianceActionsAsync(sectionEl, context, reportProgress);
                result.MergeFrom(complianceResult);

                // Parse certification links if this is a certification section
                if (context.CurrentSection?.SectionCode == c.BLANKET_NO_CHANGES_CERTIFICATION_CODE)
                {
                    var certificationResult = await parseCertificationLinksAsync(sectionEl, context, reportProgress);
                    result.MergeFrom(certificationResult);
                }

                // 4. Parse the associated manufactured product, if it exists
                // Process product information contained within the section
                var productResult = await parseManufacturedProductsAsync(sectionEl, context, reportProgress);
                result.MergeFrom(productResult);

                // 5. Parse REMS protocols if applicable
                // Check if this section contains REMS protocol elements
                if (containsRemsProtocols(sectionEl))
                {
                    var remsParser = new REMSParser();
                    var remsResult = await remsParser.ParseAsync(sectionEl, context, reportProgress);
                    result.MergeFrom(remsResult);
                }
            }
            finally
            {
                // Restore the context to prevent side effects for sibling or parent parsers
                context.CurrentSection = oldSection;
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses Warning Letter Alert content if the current section is a warning letter section (48779-3).
        /// Delegates to the specialized WarningLetterParser for processing product and date information.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the section to parse for warning letter content.</param>
        /// <param name="context">The current parsing context containing section information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult containing the results from warning letter parsing operations.</returns>
        /// <example>
        /// <code>
        /// var result = await parseWarningLetterContentAsync(sectionElement, parseContext, progress);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Warning letter elements created: {result.SectionAttributesCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method checks if the current section is a Warning Letter Alert section (48779-3)
        /// and delegates processing to the specialized WarningLetterParser. If the section is not
        /// a warning letter section, it returns a successful result without processing.
        /// </remarks>
        /// <seealso cref="WarningLetterParser"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseWarningLetterContentAsync(XElement sectionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            try
            {
                // Delegate to specialized warning letter parser
                var warningLetterParser = new WarningLetterParser();
                return await warningLetterParser.ParseAsync(sectionEl, context, reportProgress);
            }
            catch (Exception ex)
            {
                // Handle unexpected errors during warning letter parsing
                var result = new SplParseResult
                {
                    Success = false
                };
                result.Errors.Add($"Error parsing warning letter content: {ex.Message}");
                context?.Logger?.LogError(ex, "Error parsing warning letter content for section in {FileName}", context.FileNameInZip);
                return result;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Conditionally parses a DocumentRelationship if the section requires it (e.g., for certifications).
        /// </summary>
        /// <param name="sectionEl">The XElement of the section.</param>
        /// <param name="context">The current parsing context. This will be populated with the CurrentDocumentRelationship.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>The SplParseResult from the relationship parser.</returns>
        /// <seealso cref="DocumentRelationshipParser"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseDocumentRelationshipAsync(XElement sectionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            // The section code "BNCC" is an example for "Blanket No Changes Certification".
            if (context.CurrentSection?.SectionCode == c.BLANKET_NO_CHANGES_CERTIFICATION_CODE)
            {
                var subjectEl = sectionEl.SplElement(sc.E.Subject);
                if (subjectEl != null)
                {
                    var relationshipParser = new DocumentRelationshipParser();
                    return await relationshipParser.ParseAsync(subjectEl, context, reportProgress);
                }
            }
            return new SplParseResult { Success = true };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses document-level relatedDocument elements if they haven't been processed yet.
        /// This method processes XML relatedDocument elements at the document level, extracts relationship
        /// information, and creates RelatedDocument entities in the database. It includes duplicate
        /// prevention logic to avoid reprocessing already handled documents.
        /// </summary>
        /// <param name="context">The parsing context containing document information and services</param>
        /// <returns>A SplParseResult indicating success/failure and containing processing statistics</returns>
        /// <remarks>
        /// This method is designed to handle document-level related document parsing that may have
        /// been missed in previous processing steps. The method checks for existing related documents
        /// before processing to prevent duplicates.
        /// 
        /// The method expects the XML structure to contain relatedDocument elements with typeCode attributes
        /// and nested relatedDocument elements containing setId elements with root attributes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var context = new SplParseContext 
        /// { 
        ///     Document = document, 
        ///     ServiceProvider = serviceProvider,
        ///     DocumentElement = xmlElement 
        /// };
        /// var result = await parseDocumentLevelRelatedDocumentsAsync(context);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Created {result.ProductElementsCreated} related documents");
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="RelatedDocument"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseDocumentLevelRelatedDocumentsAsync(SplParseContext context)
        {
            #region implementation

            // Initialize result object to track processing outcome
            var result = new SplParseResult();

            try
            {
                #region duplicate prevention check

                // Check if we've already processed related documents for this document
                // This prevents duplicate processing and maintains data integrity
                if (context.ServiceProvider != null && context.Document?.DocumentID != null)
                {
                    // Get database context from service provider for data access
                    var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // Query existing related documents count for this source document
                    var existingCount = await dbContext.Set<RelatedDocument>()
                        .CountAsync(rd => rd.SourceDocumentID == context.Document.DocumentID);

                    if (existingCount > 0)
                    {
                        // Already processed, skip to avoid duplicates
                        return new SplParseResult { Success = true };
                    }
                }

                #endregion

                #region xml document validation

                // Use the stored document root element from context
                var documentEl = context.DocumentElement;
                if (documentEl == null)
                {
                    // No document element available, return success as this is not an error condition
                    return new SplParseResult { Success = true };
                }

                #endregion

                #region related document processing

                // Use XmlHelpers and constants to find relatedDocument elements at document level
                var relatedDocElements = documentEl.Elements(ns + sc.E.RelatedDocument);

                // Process each related document element found in the XML
                foreach (var relatedDocEl in relatedDocElements)
                {
                    #region extract relationship data

                    // Extract the type code that defines the relationship type
                    var typeCode = relatedDocEl.GetAttrVal(sc.A.TypeCode);

                    // Get the nested relatedDocument element containing reference information
                    var innerRelatedDocEl = relatedDocEl.GetSplElement(sc.E.RelatedDocument);

                    // Extract the setId root attribute which contains the referenced document GUID
                    var setIdRoot = innerRelatedDocEl?.GetSplElementAttrVal(sc.E.SetId, sc.A.Root);

                    #endregion

                    #region create related document entity

                    // Only create entity if we have valid setId root data
                    if (!string.IsNullOrEmpty(setIdRoot))
                    {
                        // Create new RelatedDocument entity with extracted data
                        var relatedDoc = new RelatedDocument
                        {
                            SourceDocumentID = context.Document?.DocumentID,
                            RelationshipTypeCode = typeCode,
                            ReferencedSetGUID = Util.ParseNullableGuid(setIdRoot)
                        };

                        // Get repository instance and persist the related document
                        var relatedDocRepo = context.GetRepository<RelatedDocument>();
                        await relatedDocRepo.CreateAsync(relatedDoc);

                        // Increment counter to track created elements
                        result.ProductElementsCreated++;
                    }

                    #endregion
                }

                #endregion
            }
            catch (Exception ex)
            {
                #region error handling

                // Set failure state and capture error information
                result.Success = false;
                result.Errors.Add($"Error parsing document-level related documents: {ex.Message}");

                // Log error with context information for debugging
                context?.Logger?.LogError(ex, "Error parsing document-level related documents for {FileName}", context.FileNameInZip);

                #endregion
            }

            // Return processing result with success status and statistics
            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if a section contains tolerance specification elements that should be parsed.
        /// </summary>
        /// <param name="sectionEl">The section XElement to check.</param>
        /// <returns>True if the section contains tolerance specifications, false otherwise.</returns>
        /// <seealso cref="ToleranceSpecificationParser"/>
        /// <seealso cref="Label"/>
        private static bool containsToleranceSpecifications(XElement sectionEl)
        {
            #region implementation
            // Check for tolerance-specific elements that indicate this section should be processed by ToleranceSpecificationParser
            // Look for 40-CFR- prefix codes in substance specifications
            var substanceSpecElements = sectionEl.SplElements(sc.E.Subject, sc.E.IdentifiedSubstance, sc.E.SubjectOf, sc.E.SubstanceSpecification);

            foreach (var specEl in substanceSpecElements)
            {
                var codeEl = specEl.GetSplElement(sc.E.Code);
                var specCode = codeEl?.GetAttrVal(sc.A.CodeValue);

                // Check for 40-CFR- prefix as specified in SPL IG 19.2.3.8
                if (!string.IsNullOrEmpty(specCode) && specCode.StartsWith("40-CFR-", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Also check for observation criteria with tolerance ranges
            return sectionEl.SplElements(sc.E.ReferenceRange, sc.E.ObservationCriterion).Any();
            #endregion
        }

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
        private bool validateContext(SplParseContext context, SplParseResult result)
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
        /// Parses effectiveTime element handling both simple value and low/high range structures.
        /// </summary>
        /// <param name="xEl">The section XElement containing effectiveTime information.</param>
        /// <param name="section">The Section entity to populate with effectiveTime data.</param>
        private static void parseEffectiveTime(XElement xEl, Section section)
        {
            #region implementation

            var effectiveTimeEl = xEl.GetSplElement(sc.E.EffectiveTime);

            if (effectiveTimeEl == null)
            {
                section.EffectiveTime = DateTime.MinValue;
                return;
            }

            // Check for simple value attribute first
            var simpleValue = effectiveTimeEl.GetAttrVal(sc.A.Value);
            if (!string.IsNullOrEmpty(simpleValue))
            {
                section.EffectiveTime = Util.ParseNullableDateTime(simpleValue) ?? DateTime.MinValue;
                return;
            }

            // Check for low/high structure
            var lowEl = effectiveTimeEl.GetSplElement(sc.E.Low);
            var highEl = effectiveTimeEl.GetSplElement(sc.E.High);

            if (lowEl != null || highEl != null)
            {
                // Parse low boundary
                section.EffectiveTimeLow = lowEl != null
                    ? Util.ParseNullableDateTime(lowEl.GetAttrVal(sc.A.Value) ?? string.Empty)
                    : null;

                // Parse high boundary  
                section.EffectiveTimeHigh = highEl != null
                    ? Util.ParseNullableDateTime(highEl.GetAttrVal(sc.A.Value) ?? string.Empty)
                    : null;

                // Set the main EffectiveTime to the low value for backward compatibility
                section.EffectiveTime = section.EffectiveTimeLow ?? DateTime.MinValue;
            }
            else
            {
                section.EffectiveTime = DateTime.MinValue;
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
        private async Task<SplParseResult> parseManufacturedProductsAsync(XElement sectionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            // CHANGE HERE: Create a result object to aggregate results from all products found.
            var combinedResult = new SplParseResult { Success = true };

            // Find ALL <subject> elements within the section.
            var subjectElements = sectionEl.SplElements(sc.E.Subject);

            // If there are no subjects, there's nothing to do.
            if (subjectElements == null || !subjectElements.Any())
            {
                return combinedResult; // Return the empty success result.
            }

            // Create the parser once, outside the loop, for efficiency.
            var productParser = new ManufacturedProductParser();

            // Loop through each <subject> element found.
            foreach (var subjectEl in subjectElements)
            {
                // Find the <manufacturedProduct> within the current <subject>.
                var productEl = subjectEl.SplElement(sc.E.ManufacturedProduct);

                if (productEl != null)
                {
                    // Parse the single product.
                    var singleProductResult = await productParser.ParseAsync(productEl, context, reportProgress);

                    // Merge the results of this product into our combined result.
                    combinedResult.MergeFrom(singleProductResult);
                }
            }

            // Return the final result containing all parsed products.
            return combinedResult;
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
        private static bool containsRemsProtocols(XElement sectionEl)
        {
            #region implementation
            // Check for REMS-specific elements that indicate this section should be processed by REMSParser
            return sectionEl.SplElements(sc.E.Subject2, sc.E.SubstanceAdministration, sc.E.ComponentOf, sc.E.Protocol).Any() ||
                   sectionEl.SplElements(sc.E.Subject, sc.E.ManufacturedProduct, sc.E.SubjectOf, sc.E.Document).Any();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses compliance actions contained within the section by looking for subjectOf elements
        /// that contain action elements, delegating to the specialized ComplianceActionParser.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the section to parse for compliance actions.</param>
        /// <param name="context">The current parsing context containing the section and other contextual information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult containing the aggregated results from all compliance action parsing operations.</returns>
        /// <example>
        /// <code>
        /// var result = await parseComplianceActionsAsync(sectionElement, parseContext, progress);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Compliance actions created: {result.ProductElementsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method searches for XML structures matching the pattern:
        /// &lt;section&gt;&lt;subjectOf&gt;&lt;action&gt;...&lt;/action&gt;&lt;/subjectOf&gt;&lt;/section&gt;
        /// Each found action element is processed by the ComplianceActionParser to create
        /// ComplianceAction entities and any associated AttachedDocument entities.
        /// The method ensures proper context management for DocumentRelationship when available.
        /// </remarks>
        /// <seealso cref="ComplianceActionParser"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseComplianceActionsAsync(XElement sectionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            try
            {
                // Look for compliance actions in subjectOf elements within the section
                // This follows the SPL document structure: <section><subjectOf><action>...</action></subjectOf></section>
                var subjectOfElements = sectionEl.SplElements(sc.E.SubjectOf);

                foreach (var subjectEl in subjectOfElements)
                {
                    // Check if this subjectOf element contains an action element
                    if (subjectEl.SplElement(sc.E.Action) != null)
                    {
                        // --- START: CONTEXT MANAGEMENT AND ORCHESTRATION ---
                        // The ComplianceActionParser requires either CurrentDocumentRelationship or CurrentPackageIdentifier
                        // For section-level compliance actions, we typically use DocumentRelationship context if available
                        // No need to set context here as it should already be established by ParseDocumentRelationshipAsync
                        // or inherited from parent parsing context

                        try
                        {
                            // Delegate to specialized compliance action parser
                            var complianceParser = new ComplianceActionParser();
                            var complianceResult = await complianceParser.ParseAsync(subjectEl, context, reportProgress);

                            // Merge results to accumulate counts and errors
                            result.MergeFrom(complianceResult);

                            // Log errors if compliance parsing failed
                            if (!complianceResult.Success)
                            {
                                context?.Logger?.LogError("Failed to parse compliance action for SectionID {SectionID} in file {FileName}.",
                                    context.CurrentSection?.SectionID, context.FileNameInZip);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Handle errors during individual compliance action parsing
                            result.Success = false;
                            result.Errors.Add($"Error parsing individual compliance action: {ex.Message}");
                            context?.Logger?.LogError(ex, "Error parsing compliance action in section for {FileName}", context.FileNameInZip);
                        }
                        // --- END: CONTEXT MANAGEMENT AND ORCHESTRATION ---
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle unexpected errors during compliance action parsing
                result.Success = false;
                result.Errors.Add($"Error parsing compliance actions in section: {ex.Message}");
                context?.Logger?.LogError(ex, "Error parsing compliance actions for section in {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses certification links for Blanket No Changes Certification (BNCC) sections by
        /// finding product identifiers and setting appropriate context for the specialized parser.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the BNCC section to parse for certification links.</param>
        /// <param name="context">The current parsing context containing the section and document relationship information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult containing the results from certification link parsing operations.</returns>
        /// <example>
        /// <code>
        /// if (context.CurrentSection?.SectionCode == c.BLANKET_NO_CHANGES_CERTIFICATION_CODE)
        /// {
        ///     var result = await parseCertificationLinksAsync(sectionElement, parseContext, progress);
        ///     if (result.Success)
        ///     {
        ///         Console.WriteLine($"Certification links created: {result.ProductElementsCreated}");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method is specifically designed for BNCC sections and looks for product-related
        /// elements within the section that can be linked to certifications. It expects the
        /// DocumentRelationship context to be properly set before calling. The method finds
        /// product identifiers and sets the CurrentProductIdentifier context for each certification
        /// link parsing operation, following the established context management pattern.
        /// </remarks>
        /// <seealso cref="CertificationProductLinkParser"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseCertificationLinksAsync(XElement sectionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            try
            {
                // Validate that we have the required DocumentRelationship context for certification links
                if (context.CurrentDocumentRelationship?.DocumentRelationshipID == null)
                {
                    // This is not necessarily an error - some BNCC sections may not have certification links
                    return new SplParseResult { Success = true };
                }

                // Look for product-related elements within the section that can have certification links
                // In BNCC sections, we need to find manufactured products or product identifiers
                var productElements = sectionEl.SplElements(sc.E.Subject, sc.E.ManufacturedProduct);

                foreach (var productEl in productElements)
                {
                    // Look for product identification codes within the manufactured product
                    var codeElements = productEl.SplElements(sc.E.Code);

                    foreach (var codeEl in codeElements)
                    {
                        // Check if this code element represents a product identifier with certification potential
                        if (!string.IsNullOrEmpty(codeEl.GetAttrVal(sc.A.CodeValue)))
                        {
                            // Create a ProductIdentifier to set the required context for certification link parsing
                            // This provides the ProductIdentifier context that CertificationProductLinkParser expects
                            var productIdentifier = await createProductIdentifierForContextAsync(codeEl, context);

                            if (productIdentifier?.ProductIdentifierID.HasValue == true)
                            {
                                // --- START: CONTEXT MANAGEMENT AND ORCHESTRATION ---
                                var oldIdentifier = context.CurrentProductIdentifier;
                                context.CurrentProductIdentifier = productIdentifier; // Set context for the child parser
                                try
                                {
                                    // Delegate to specialized certification link parser
                                    var certLinkParser = new CertificationProductLinkParser();
                                    var certLinkResult = await certLinkParser.ParseAsync(codeEl, context, reportProgress);

                                    // Merge results to accumulate counts and errors
                                    result.MergeFrom(certLinkResult);

                                    // Log errors if certification link parsing failed
                                    if (!certLinkResult.Success)
                                    {
                                        context?.Logger?.LogError("Failed to parse certification link for ProductIdentifierID {ProductIdentifierID} in file {FileName}.",
                                            productIdentifier.ProductIdentifierID, context.FileNameInZip);
                                    }
                                }
                                finally
                                {
                                    context.CurrentProductIdentifier = oldIdentifier; // Restore context
                                }
                                // --- END: CONTEXT MANAGEMENT AND ORCHESTRATION ---
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle unexpected errors during certification link parsing
                result.Success = false;
                result.Errors.Add($"Error parsing certification links in BNCC section: {ex.Message}");
                context?.Logger?.LogError(ex, "Error parsing certification links for BNCC section in {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a temporary ProductIdentifier from the given code element to establish
        /// the proper context for certification link parsing.
        /// </summary>
        /// <param name="codeEl">The XElement containing product identification information.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A ProductIdentifier entity with a valid ProductIdentifierID, or null if creation failed.</returns>
        /// <remarks>
        /// This helper method creates a ProductIdentifier entity to provide the required context
        /// for CertificationProductLinkParser. Since repository FindAsync is not available,
        /// this method creates new ProductIdentifier entities based on the code element data.
        /// The ProductID will be set if a current product exists in the parsing context.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<ProductIdentifier?> createProductIdentifierForContextAsync(XElement codeEl, SplParseContext context)
        {
            #region implementation
            try
            {
                var codeValue = codeEl.GetAttrVal(sc.A.CodeValue);
                var codeSystem = codeEl.GetAttrVal(sc.A.CodeSystem);

                if (string.IsNullOrEmpty(codeValue))
                {
                    return null;
                }

                // Create a new ProductIdentifier using the correct model properties
                var newIdentifier = new ProductIdentifier
                {
                    ProductID = context.CurrentProduct?.ProductID, // Link to current product if available
                    IdentifierValue = codeValue, // Maps to [code code=] attribute
                    IdentifierSystemOID = codeSystem, // Maps to [code codeSystem=] attribute
                    IdentifierType = determineIdentifierType(codeSystem) // Classify based on OID
                };

                // Get repository for ProductIdentifier operations
                var identifierRepo = context.GetRepository<ProductIdentifier>();
                await identifierRepo.CreateAsync(newIdentifier);

                return newIdentifier.ProductIdentifierID > 0 ? newIdentifier : null;
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error creating ProductIdentifier for certification link context");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines the identifier type classification based on the OID system.
        /// </summary>
        /// <param name="oidSystem">The OID system string from the code element.</param>
        /// <returns>A string classification of the identifier type, or null if not recognized.</returns>
        /// <remarks>
        /// This method maps common OID systems to their corresponding identifier types
        /// for proper classification in the ProductIdentifier entity.
        /// </remarks>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Label"/>
        private static string? determineIdentifierType(string? oidSystem)
        {
            #region implementation
            if (string.IsNullOrEmpty(oidSystem))
            {
                return null;
            }

            // Map common OID systems to identifier types
            // These mappings should be updated based on your system's OID registry
            return oidSystem switch
            {
                "2.16.840.1.113883.6.69" => "NDC", // National Drug Code
                "1.3.160" => "GTIN", // Global Trade Item Number
                "2.16.840.1.113883.6.162" => "UPC", // Universal Product Code
                _ => "OTHER" // Generic classification for unrecognized OIDs
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Helper method to execute a parser operation across all sections in a single phase.
        /// This enables batching of database operations by parser type rather than by section.
        /// </summary>
        /// <param name="createdSections">Dictionary mapping section XElements to their created Section entities.</param>
        /// <param name="context">The parsing context.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <param name="parseOperation">The async operation to execute for each section.</param>
        /// <param name="result">The result object to merge parse results into.</param>
        /// <param name="sectionFilter">Optional filter to determine which sections should be processed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This helper reduces code duplication and ensures consistent context management
        /// across all parser phases. Each phase processes ALL sections before moving to the next,
        /// allowing parsers to implement bulk operations internally.
        /// </remarks>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="Label.Section"/>
        private async Task executeParserPhaseAsync(
            Dictionary<XElement, Label.Section> createdSections,
            SplParseContext context,
            Action<string>? reportProgress,
            Func<XElement, SplParseContext, Action<string>?, Task<SplParseResult>> parseOperation,
            SplParseResult result,
            Func<XElement, Label.Section, bool>? sectionFilter = null)
        {
            #region implementation

            foreach (var kvp in createdSections)
            {
                var sectionEl = kvp.Key;
                var section = kvp.Value;

                // Skip if section is invalid or doesn't pass filter
                if (section?.SectionID == null)
                    continue;

                if (sectionFilter != null && !sectionFilter(sectionEl, section))
                    continue;

                // Set section context and ensure it's restored
                var oldSection = context.CurrentSection;
                context.CurrentSection = section;

                try
                {
                    var parseResult = await parseOperation(sectionEl, context, reportProgress);
                    result.MergeFrom(parseResult);
                }
                finally
                {
                    context.CurrentSection = oldSection;
                }
            }

            #endregion
        }
        #endregion
    }
}