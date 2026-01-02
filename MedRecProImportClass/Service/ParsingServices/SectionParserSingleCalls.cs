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
    public class SectionParser_SingleCalls : SectionParserBase, ISplSectionParser
    {
        public SectionParser_SingleCalls(
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
                if (element == null)
                {
                    result.Success = false;
                    result.Errors.Add("Invalid section element provided for parsing.");
                    return result;
                }

                result = await parseSectionAsync(element, context, reportProgress);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"An unexpected error occurred while parsing section: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing <section> element for {FileName}", context.FileNameInZip);
            }

            return result;
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
        async Task<SplParseResult> parseSectionAsync(XElement xEl,
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
    }
}