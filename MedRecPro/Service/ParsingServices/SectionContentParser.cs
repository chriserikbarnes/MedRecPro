using System.Xml.Linq;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Data;
using MedRecPro.Helpers;
using Microsoft.EntityFrameworkCore;
using static MedRecPro.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
using System.Diagnostics;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Main orchestrator for parsing section content elements within SPL sections.
    /// This refactored parser delegates to specialized implementations based on
    /// context configuration, routing to either single-call or bulk operations
    /// strategies for optimal performance based on document characteristics.
    /// </summary>
    /// <remarks>
    /// This parser serves as the main entry point for section content parsing operations,
    /// coordinating between two implementation strategies:
    /// 
    /// 1. Single-Call Operations (context.UseBulkOperations = false):
    ///    - Processes entities one at a time with individual database operations
    ///    - Simpler logic, better for small documents or debugging
    ///    - Implemented by <see cref="SectionContentParser_SingleCalls"/>
    /// 
    /// 2. Bulk Operations (context.UseBulkOperations = true):
    ///    - Collects all entities into memory, then performs batch operations
    ///    - 100-1000x performance improvement for large documents
    ///    - Implemented by <see cref="SectionContentParser_BulkCalls"/>
    /// 
    /// The feature flag approach allows runtime switching between strategies without
    /// code changes, enabling A/B testing, gradual rollouts, or environment-specific
    /// optimizations.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="SectionContentParserBase"/>
    /// <seealso cref="SectionContentParser_SingleCalls"/>
    /// <seealso cref="SectionContentParser_BulkCalls"/>
    /// <seealso cref="SectionTextContent"/>
    /// <seealso cref="TextList"/>
    /// <seealso cref="TextTable"/>
    /// <seealso cref="SplParseContext"/>
    public class SectionContentParser : SectionContentParserBase, ISplSectionParser
    {
        #region Private Delegate Fields

        /**************************************************************/
        /// <summary>
        /// Delegate for single-call (N+1) operations strategy.
        /// </summary>
        /// <seealso cref="SectionContentParser_SingleCalls"/>
        private readonly SectionContentParser_SingleCalls _singleCallsDelegate;

        /**************************************************************/
        /// <summary>
        /// Delegate for bulk operations strategy.
        /// </summary>
        /// <seealso cref="SectionContentParser_BulkCalls"/>
        private readonly SectionContentParser_BulkCalls _bulkCallsDelegate;

        private readonly SectionContentParser_StagedBulkCalls _stagedBulkCallsDelegate;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SectionContentParser with required dependencies.
        /// Creates both single-call and bulk-call delegates sharing the same media parser.
        /// </summary>
        /// <param name="mediaParser">Parser for handling multimedia content within text blocks.</param>
        /// <seealso cref="SectionMediaParser"/>
        /// <seealso cref="SectionContentParser_SingleCalls"/>
        /// <seealso cref="SectionContentParser_BulkCalls"/>
        public SectionContentParser(SectionMediaParser? mediaParser = null) : base(mediaParser)
        {
            #region implementation
            // Initialize both mode delegates with shared media parser
            _singleCallsDelegate = new SectionContentParser_SingleCalls(_mediaParser);
            _bulkCallsDelegate = new SectionContentParser_BulkCalls(_mediaParser);
            _stagedBulkCallsDelegate = new SectionContentParser_StagedBulkCalls(_mediaParser);
            #endregion
        }

        #endregion

        #region ISplSectionParser Implementation

        /**************************************************************/
        /// <summary>
        /// Gets the section name for this parser, representing content processing.
        /// </summary>
        /// <seealso cref="ISplSectionParser"/>
        public string SectionName => "content";

        /**************************************************************/
        /// <summary>
        /// Parses section text content elements, processing hierarchical structures
        /// including text, lists, tables, excerpts, and highlights. Routes to either
        /// single-call or bulk operations strategy based on context configuration.
        /// </summary>
        /// <param name="element">The XElement representing the section to parse for content.</param>
        /// <param name="context">The current parsing context containing section information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A SplParseResult indicating the success status and content elements created.</returns>
        /// <seealso cref="ParseSectionContentAsync"/>
        /// <seealso cref="SectionContentParser_SingleCalls.ParseAsync"/>
        /// <seealso cref="SectionContentParser_BulkCalls.ParseAsync"/>
        public async Task<SplParseResult> ParseAsync(
            XElement element,
            SplParseContext context,
            Action<string>? reportProgress = null,
            bool? isParentCallingForAllSubElements = false)
        {
            #region implementation
            var result = new SplParseResult();

            try
            {
                // Validate context and current section
                if (context?.CurrentSection?.SectionID == null)
                {
                    result.Success = false;
                    result.Errors.Add("No current section available for content parsing.");
                    return result;
                }

                if (context.UseBulkStagingOperations)
                {
                    // Mode 3: Staged Bulk operations for very large documents
                    return await _stagedBulkCallsDelegate.ParseAsync(element, context, reportProgress, isParentCallingForAllSubElements);
                }

                // Route to appropriate delegate based on context configuration
                else if (context.UseBulkOperations)
                {
                    // Mode 2: Bulk operations for high-performance parsing
                    return await _bulkCallsDelegate.ParseAsync(element, context, reportProgress, isParentCallingForAllSubElements);
                }
                else
                {
                    // Mode 1: Single-call operations
                    return await _singleCallsDelegate.ParseAsync(element, context, reportProgress, isParentCallingForAllSubElements);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing section content: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing section content for section {SectionId}", context.CurrentSection?.SectionID);
            }

            return result;
            #endregion
        }

        #endregion

        #region Section Content Parsing - Feature Switched Entry

        /**************************************************************/
        /// <summary>
        /// Parses the inner content of a section, such as text, lists, and highlights.
        /// Processes hierarchies, text content, excerpts, and highlight elements.
        /// Routes to either single-call or bulk operations strategy based on context configuration.
        /// </summary>
        /// <param name="xEl">The XElement for the section whose content is to be parsed.</param>
        /// <param name="sectionId">The database ID of the parent section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A SplParseResult containing the outcome of parsing the content.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionExcerptHighlight"/>
        /// <seealso cref="SectionContentParser_SingleCalls.ParseSectionContentAsync"/>
        /// <seealso cref="SectionContentParser_BulkCalls.ParseSectionContentAsync"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseSectionContentAsync(XElement xEl, int sectionId, SplParseContext context)
        {
            #region implementation

            var textEl = xEl.SplElement(sc.E.Text);
            if (textEl == null)
            {
                context.Logger?.LogWarning(
                    "Section {SectionId} has no <text> element - only excerpt/highlight content will be processed",
                    sectionId);

#if DEBUG
                Debug.WriteLine($"Section {sectionId} has no <text> element - only excerpt/highlight content will be processed");
#endif
            }

            // Route to appropriate delegate based on context configuration
            if (context.UseBulkStagingOperations)
            {
                // Mode 3: Staged Bulk operations for very large documents
                var ret = await _stagedBulkCallsDelegate.ParseSectionContentAsync(xEl, sectionId, context);

                return ret;
            }
            else if (context.UseBulkOperations)
            {
                // Mode 2: Bulk operations for high-performance parsing
                return await _bulkCallsDelegate.ParseSectionContentAsync(xEl, sectionId, context);
            }
            else
            {
                // Mode 1: Single-call operations
                return await _singleCallsDelegate.ParseSectionContentAsync(xEl, sectionId, context);
            }
            #endregion
        }

        #endregion
    }
}