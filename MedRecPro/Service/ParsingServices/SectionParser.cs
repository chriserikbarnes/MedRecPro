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
#pragma warning restore 
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
    public class SectionParser : SectionParserBase, ISplSectionParser
    {
        private readonly SectionParser_SingleCalls _singleCallsDelegate;
        private readonly SectionParser_BulkCalls _bulkCallsDelegate;
        private readonly SectionParser_StagedBulk _stagedBulkDelegate;

        public SectionParser(
            SectionContentParser? contentParser = null,
            SectionIndexingParser? indexingParser = null,
            SectionHierarchyParser? hierarchyParser = null,
            SectionMediaParser? mediaParser = null,
            ToleranceSpecificationParser? toleranceParser = null)
            : base(
                contentParser ?? new SectionContentParser(),
                indexingParser ?? new SectionIndexingParser(),
                hierarchyParser ?? new SectionHierarchyParser(),
                mediaParser ?? new SectionMediaParser(),
                toleranceParser ?? new ToleranceSpecificationParser())
        {
            // Initialize all three mode delegates with shared parsers
            _singleCallsDelegate = new SectionParser_SingleCalls(
                _contentParser, _indexingParser, _hierarchyParser, _mediaParser, _toleranceParser);

            _bulkCallsDelegate = new SectionParser_BulkCalls(
                _contentParser, _indexingParser, _hierarchyParser, _mediaParser, _toleranceParser);

            _stagedBulkDelegate = new SectionParser_StagedBulk(
                _contentParser, _indexingParser, _hierarchyParser, _mediaParser, _toleranceParser);
        }

        public string SectionName => "section";

        public async Task<SplParseResult> ParseAsync(
            XElement xEl,
            SplParseContext context,
            Action<string>? reportProgress = null,
            bool? isParentCallingForAllSubElements = false)
        {
            if (!validateContext(context, new SplParseResult() { }, out var result))
                return result;

            if (xEl == null)
            {
                result.Success = false;
                result.Errors.Add("Invalid section element provided for parsing.");
                return result;
            }

            try
            {
                if (isParentCallingForAllSubElements ?? false)
                {
                    var sectionElements = xEl.SplElements(sc.E.Component, sc.E.Section);

                    if (context.UseBulkStaging)
                    {
                        // Mode 3: Staged bulk operations
                        return await _stagedBulkDelegate.ParseAsync(xEl, context, reportProgress, true);
                    }
                    else if (sectionElements != null && sectionElements.Any())
                    {
                        if (context.UseBulkOperations)
                        {
                            // Mode 2: Nested bulk operations
                            return await _bulkCallsDelegate.ParseAsync(xEl, context, reportProgress, true);
                        }
                        else
                        {
                            // Mode 1: N+1 pattern (legacy)
                            foreach (var sectionEl in sectionElements)
                            {
                                result.MergeFrom(await _singleCallsDelegate.ParseAsync(sectionEl, context, reportProgress, false));
                            }
                            return result;
                        }
                    }
                }
                else
                {
                    return await _singleCallsDelegate.ParseAsync(xEl, context, reportProgress, false);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"An unexpected error occurred while parsing section: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing <section> element for {FileName}", context.FileNameInZip);
            }

            return result;
        }
    }
}