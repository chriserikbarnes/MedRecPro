using MedRecProImportClass.Data;
using MedRecProImportClass.DataAccess;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using static MedRecProImportClass.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecProImportClass.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecProImportClass.Models.Constant;
using AngleSharp.Dom;
using System.Data;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

namespace MedRecProImportClass.Service.ParsingServices
{
    public class SectionParser_BulkCalls : SectionParserBase, ISplSectionParser
    {
        public SectionParser_BulkCalls(
            SectionContentParser contentParser,
            SectionIndexingParser indexingParser,
            SectionHierarchyParser hierarchyParser,
            SectionMediaParser mediaParser,
            ToleranceSpecificationParser toleranceParser)
            : base(contentParser, indexingParser, hierarchyParser, mediaParser, toleranceParser)
        { }

        public string SectionName => "section";

        public async Task<SplParseResult> ParseAsync(
            XElement element,
            SplParseContext context,
            Action<string>? reportProgress,
            bool? isParentCallingForAllSubElements = false)
        {
            var result = new SplParseResult();

            if (!validateContext(context, result))
                return result;

            try
            {
                var sectionElements = element.SplElements(sc.E.Component, sc.E.Section);

                if (sectionElements != null && sectionElements.Any())
                {
                    result = await parseSectionAsync(sectionElements.ToList(), context, reportProgress);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"An unexpected error occurred while parsing sections: {ex.Message}");
                context?.Logger?.LogError(ex, "Error in bulk section processing for {FileName}", context.FileNameInZip);
            }

            return result;
        }


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
        private async Task<SplParseResult> parseSectionAsync(
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

        #endregion
    }
}