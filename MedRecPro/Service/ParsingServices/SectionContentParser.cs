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
using System.Linq;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Specialized parser for handling text content elements within SPL sections.
    /// Processes hierarchical content structures including paragraphs, lists, tables,
    /// and excerpts while maintaining proper nesting and relationships.
    /// </summary>
    /// <remarks>
    /// This parser handles the complex nested structures found in SPL section text,
    /// creating appropriate database entities for content blocks, list items, table
    /// cells, and excerpt highlights while preserving document structure for
    /// round-trip fidelity.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="SectionTextContent"/>
    /// <seealso cref="TextList"/>
    /// <seealso cref="TextTable"/>
    /// <seealso cref="SplParseContext"/>
    public class SectionContentParser : ISplSectionParser
    {
        #region Data Transfer Objects for Bulk Operations

        /**************************************************************/
        /// <summary>
        /// Data Transfer Object representing a table column parsed from XML.
        /// Used for in-memory collection before bulk database operations.
        /// </summary>
        /// <seealso cref="TextTableColumn"/>
        /// <seealso cref="Label"/>
        private class TableColumnDto
        {
            public int SequenceNumber { get; set; }
            public int? ColGroupSequenceNumber { get; set; }
            public string? ColGroupStyleCode { get; set; }
            public string? ColGroupAlign { get; set; }
            public string? ColGroupVAlign { get; set; }
            public string? Width { get; set; }
            public string? Align { get; set; }
            public string? VAlign { get; set; }
            public string? StyleCode { get; set; }
        }

        /**************************************************************/
        /// <summary>
        /// Data Transfer Object representing a table row parsed from XML.
        /// Contains row metadata and a list of associated cell DTOs.
        /// Used for in-memory collection before bulk database operations.
        /// </summary>
        /// <seealso cref="TextTableRow"/>
        /// <seealso cref="TableCellDto"/>
        /// <seealso cref="Label"/>
        private class TableRowDto
        {
            public string RowGroupType { get; set; } = string.Empty;
            public int SequenceNumber { get; set; }
            public string? StyleCode { get; set; }
            public List<TableCellDto> Cells { get; set; } = new();
        }

        /**************************************************************/
        /// <summary>
        /// Data Transfer Object representing a table cell parsed from XML.
        /// Used for in-memory collection before bulk database operations.
        /// </summary>
        /// <seealso cref="TextTableCell"/>
        /// <seealso cref="Label"/>
        private class TableCellDto
        {
            public string CellType { get; set; } = string.Empty;
            public int SequenceNumber { get; set; }
            public string? CellText { get; set; }
            public int? RowSpan { get; set; }
            public int? ColSpan { get; set; }
            public string? StyleCode { get; set; }
            public string? Align { get; set; }
            public string? VAlign { get; set; }
        }

        /**************************************************************/
        /// <summary>
        /// Data Transfer Object representing a SectionTextContent entity during parsing.
        /// Used in Phase 1 to collect all content blocks before database operations.
        /// </summary>
        /// <seealso cref="SectionTextContent"/>
        private class SectionTextContentDto
        {
            public int SectionID { get; set; }
            public int? ParentSectionTextContentID { get; set; }
            public string ContentType { get; set; } = string.Empty;
            public string? StyleCode { get; set; }
            public int SequenceNumber { get; set; }
            public string? ContentText { get; set; }
            public XElement SourceElement { get; set; } = null!;
            public List<SectionTextContentDto> Children { get; set; } = new();

            /// <summary>
            /// Temporary identifier used to establish parent-child relationships before database IDs are assigned.
            /// </summary>
            public string TempId { get; set; } = string.Empty;

            /// <summary>
            /// Reference to parent's temporary identifier for building hierarchy.
            /// </summary>
            public string? ParentTempId { get; set; }
        }

        /**************************************************************/
        /// <summary>
        /// Data transfer object for TextListItem parsing and bulk operations.
        /// </summary>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="Label"/>
        private class TextListItemDto
        {
            public int SequenceNumber { get; set; }
            public string? ItemCaption { get; set; }
            public string? ItemText { get; set; }
        }

        #endregion

        #region Private Fields and Helper Classes
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        /// <summary>
        /// Media parser for handling multimedia content within text blocks.
        /// </summary>
        private readonly SectionMediaParser _mediaParser;

        /**************************************************************/
        /// <summary>
        /// Helper class to encapsulate the results of processing a single content block.
        /// Contains the main content entity, nested content, and count of grandchild entities.
        /// </summary>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="Label"/>
        private class ProcessBlockResult
        {
            #region implementation
            /// <summary>
            /// The primary SectionTextContent entity created for this content block.
            /// </summary>
            /// <seealso cref="SectionTextContent"/>
            public SectionTextContent? MainContent { get; set; }

            /// <summary>
            /// Collection of nested SectionTextContent entities within this block.
            /// </summary>
            /// <seealso cref="SectionTextContent"/>
            public List<SectionTextContent> NestedContent { get; set; } = new List<SectionTextContent>();

            /// <summary>
            /// Count of grandchild entities created (list items, table cells, etc.).
            /// </summary>
            public int GrandchildEntityCount { get; set; }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Represents the repositories needed for text list processing.
        /// </summary>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="TextList"/>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="Label"/>
        private class TextListRepositories
        {
            #region implementation
            /// <summary>
            /// Gets or sets the database context.
            /// </summary>
            /// <seealso cref="ApplicationDbContext"/>
            /// <seealso cref="Label"/>
            public ApplicationDbContext? DbContext { get; set; }

            /// <summary>
            /// Gets or sets the TextList repository.
            /// </summary>
            /// <seealso cref="TextList"/>
            /// <seealso cref="Label"/>
            public Repository<TextList>? TextListRepo { get; set; }

            /// <summary>
            /// Gets or sets the TextListItem repository.
            /// </summary>
            /// <seealso cref="TextListItem"/>
            /// <seealso cref="Label"/>
            public Repository<TextListItem>? TextListItemRepo { get; set; }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Represents the result of processing a text list item.
        /// </summary>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="Label"/>
        private class TextListItemProcessResult
        {
            #region implementation
            /// <summary>
            /// Gets or sets a value indicating whether the item was processed (had content).
            /// </summary>
            /// <seealso cref="Label"/>
            public bool WasProcessed { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether a new item was created.
            /// </summary>
            /// <seealso cref="Label"/>
            public bool WasCreated { get; set; }
            #endregion
        }
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SectionContentParser with required dependencies.
        /// </summary>
        /// <param name="mediaParser">Parser for handling multimedia content within text blocks.</param>
        public SectionContentParser(SectionMediaParser? mediaParser = null)
        {
            _mediaParser = mediaParser ?? new SectionMediaParser();
        }

        /**************************************************************/
        /// <summary>
        /// Gets the section name for this parser, representing content processing.
        /// </summary>
        public string SectionName => "content";

        /**************************************************************/
        /// <summary>
        /// Parses section text content elements, processing hierarchical structures
        /// including text, lists, tables, excerpts, and highlights.
        /// </summary>
        /// <param name="element">The XElement representing the section to parse for content.</param>
        /// <param name="context">The current parsing context containing section information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A SplParseResult indicating the success status and content elements created.</returns>
        /// <seealso cref="ParseSectionContentAsync"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress = null, bool? isParentCallingForAllSubElements = false)
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

                reportProgress?.Invoke("Processing section content...");

                // Parse section content
                var contentResult = await ParseSectionContentAsync(element, context.CurrentSection.SectionID.Value, context);
                result.MergeFrom(contentResult);

                reportProgress?.Invoke($"Processed {contentResult.SectionAttributesCreated} content attributes");
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

        #region Section Content Parsing - Feature Switched Entry

        /**************************************************************/
        /// <summary>
        /// Parses the inner content of a section, such as text, lists, and highlights.
        /// Processes hierarchies, text content, excerpts, and highlight elements.
        /// </summary>
        /// <param name="xEl">The XElement for the section whose content is to be parsed.</param>
        /// <param name="sectionId">The database ID of the parent section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A SplParseResult containing the outcome of parsing the content.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionExcerptHighlight"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseSectionContentAsync(XElement xEl, int sectionId, SplParseContext context)
        {
            #region implementation
            var result = new SplParseResult();

            // Process main text content including paragraphs, lists, tables, etc.
            var textEl = xEl.SplElement(sc.E.Text);
            if (textEl != null)
            {
                var (textContents, listEntityCount) = await GetOrCreateSectionTextContentsAsync(textEl, sectionId, context, parseAndSaveSectionAsync);
                result.SectionAttributesCreated += textContents.Count;
                result.SectionAttributesCreated += listEntityCount;
            }

            // Process excerpt elements with nested content structure
            var excerptEl = xEl.SplElement(sc.E.Excerpt);
            if (excerptEl != null)
            {
                var (excerptTextContents, listEntityCount) = await GetOrCreateSectionTextContentsAsync(excerptEl, sectionId, context, parseAndSaveSectionAsync);
                result.SectionAttributesCreated += excerptTextContents.Count;

                // Extract highlighted text within excerpts for specialized processing
                var eHighlights = await getOrCreateSectionExcerptHighlightsAsync(excerptEl, sectionId, context);
                result.SectionAttributesCreated += eHighlights.Count;
            }

            // Process direct highlights not contained within excerpts
            var directHighlights = await getOrCreateSectionExcerptHighlightsAsync(xEl, sectionId, context);
            result.SectionAttributesCreated += directHighlights.Count;

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Orchestrates the recursive parsing of a section's [text] element by delegating
        /// to either a bulk operations strategy or single-call strategy based on context settings.
        /// This public method serves as the entry point for section content parsing, routing
        /// to the appropriate implementation based on performance requirements.
        /// </summary>
        /// <param name="parentEl">The XElement to start parsing (typically the [text] element).</param>
        /// <param name="sectionId">The SectionID owning this content.</param>
        /// <param name="context">Parsing context containing service provider, logger, and configuration flags.</param>
        /// <param name="parseAndSaveSectionAsync">Function delegate for parsing and saving child sections.</param>
        /// <param name="parentSectionTextContentId">Parent SectionTextContentID for hierarchy, or null for top-level blocks.</param>
        /// <param name="sequence">The sequence number to start from (default 1).</param>
        /// <returns>A tuple containing the complete list of all created/found SectionTextContent objects and the total count of grandchild entities (e.g., list items, table cells).</returns>
        /// <remarks>
        /// The method uses a strategy pattern to optimize database operations. When bulk operations
        /// are enabled via context.UseBulkOperations, it delegates to the high-performance bulk
        /// implementation which reduces database round-trips by 100-1000x. Otherwise, it uses
        /// the traditional single-call approach for simpler scenarios or backwards compatibility.
        /// </remarks>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        public async Task<Tuple<List<SectionTextContent>, int>> GetOrCreateSectionTextContentsAsync(XElement parentEl,
            int sectionId,
            SplParseContext context,
            Func<XElement, SplParseContext, Task<Section>> parseAndSaveSectionAsync,
            int? parentSectionTextContentId = null,
            int sequence = 1)
        {
            #region implementation
            // Route to bulk operations strategy for high-performance parsing scenarios
            if (context.UseBulkOperations)
            {
                return await getOrCreateSectionTextContentsAsync_bulkCalls(parentEl, sectionId, context, parseAndSaveSectionAsync, parentSectionTextContentId, sequence);
            }
            else
            {
                // Route to single-call strategy for traditional processing or compatibility
                return await getOrCreateSectionTextContentsAsync_singleCalls(parentEl,
                    sectionId, context,
                    parseAndSaveSectionAsync,
                    parentSectionTextContentId,
                    sequence);
            }
            #endregion
        }

        #endregion

        #region Section Content Parsing - Individual Operations (N + 1)

        /**************************************************************/
        /// <summary>
        /// Orchestrates the recursive parsing of a section's [text] element. It iterates
        /// through top-level content blocks and delegates the processing of each block
        /// to specialized helpers, preserving the nested hierarchy for SPL round-tripping.
        /// </summary>
        /// <param name="parentEl">The XElement to start parsing (typically the [text] element).</param>
        /// <param name="sectionId">The SectionID owning this content.</param>
        /// <param name="context">Parsing context.</param>
        /// <param name="parseAndSaveSectionAsync">Function delegate for parsing and saving child sections.</param>
        /// <param name="parentSectionTextContentId">Parent SectionTextContentID for hierarchy, or null for top-level blocks.</param>
        /// <param name="sequence">The sequence number to start from (default 1).</param>
        /// <returns>A tuple containing the complete list of all created/found SectionTextContent objects and the total count of grandchild entities (e.g., list items, table cells).</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<Tuple<List<SectionTextContent>, int>> getOrCreateSectionTextContentsAsync_singleCalls(
            XElement parentEl,
            int sectionId,
            SplParseContext context,
            Func<XElement, SplParseContext, Task<Section>> parseAndSaveSectionAsync,
            int? parentSectionTextContentId = null,
            int sequence = 1)
        {
            #region implementation
            var allCreatedContent = new List<SectionTextContent>();
            int totalGrandChildEntities = 0;

            // Validate all required input parameters and context dependencies
            if (parentEl == null || sectionId <= 0 || context?.ServiceProvider == null || context.Logger == null)
            {
                return Tuple.Create(allCreatedContent, totalGrandChildEntities);
            }

            // Initialize sequence counter for maintaining content block order
            int seq = sequence;

            // Process each content block in the section using tree building helper
            foreach (var block in parentEl.SplBuildSectionContentTree())
            {
                // Delegate the processing of a single block to a dedicated handler
                // Use specialized handler for individual content block processing
                var blockResult = await processContentBlockAsync(
                    block,
                    sectionId,
                    context,
                    parseAndSaveSectionAsync,
                    parentSectionTextContentId,
                    seq);

                // Aggregate results from the processed block
                // Combine results from current block processing into overall collection
                if (blockResult != null
                    && blockResult.MainContent != null)
                {
                    allCreatedContent.Add(blockResult.MainContent);
                    allCreatedContent.AddRange(blockResult.NestedContent);
                    totalGrandChildEntities += blockResult.GrandchildEntityCount;
                }

                // Increment sequence for next content block
                seq++;
            }

            return Tuple.Create(allCreatedContent, totalGrandChildEntities);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes a single content block ([paragraph], [list], etc.), creating its
        /// corresponding SectionTextContent record, handling specialized content, and
        /// initiating recursion for any nested blocks.
        /// </summary>
        /// <param name="block">The XElement representing the content block to process.</param>
        /// <param name="sectionId">The owning section ID.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="parseAndSaveSectionAsync">Function delegate for parsing child sections.</param>
        /// <param name="parentSectionTextContentId">Parent content ID for hierarchy.</param>
        /// <param name="sequence">Sequence number for ordering.</param>
        /// <returns>A ProcessBlockResult containing the created content and metadata, or null if the block is skipped.</returns>
        /// <seealso cref="ProcessBlockResult"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<ProcessBlockResult?> processContentBlockAsync(
            XElement block,
            int sectionId,
            SplParseContext context,
            Func<XElement, SplParseContext, Task<Section>> parseAndSaveSectionAsync,
            int? parentSectionTextContentId,
            int sequence)
        {
            #region implementation
            // Highlights are processed by getOrCreateSectionExcerptHighlightsAsync within an Excerpt.
            // Skipping them here prevents creating a duplicate SectionTextContent record.
            // Skip highlight elements as they are handled separately within excerpt processing
            if (block.Name.LocalName.Equals(sc.E.Highlight, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // 1. Find or create the primary SectionTextContent record for this block
            // Create or retrieve the main content entity for this block
            var stc = await getOrCreateSectionTextContentRecordAsync(block, sectionId, context, parentSectionTextContentId, sequence);
            if (stc == null) return null;

            // Initialize result container with main content entity
            var result = new ProcessBlockResult { MainContent = stc };

            // 2. Process specialized content (e.g., parse list items, table cells, or excerpt highlights)
            // Handle complex nested structures like lists, tables, and excerpts
            result.GrandchildEntityCount = await processSpecializedContentAsync(block, stc, context);

            // 3. Recurse for any nested child blocks, but ONLY if the block is not a type
            // that was fully handled by the specialized parser. This prevents the dual-parsing conflict.
            var contentType = stc.ContentType ?? string.Empty;
            if (!contentType.Equals(sc.E.List, StringComparison.OrdinalIgnoreCase) &&
                !contentType.Equals(sc.E.Table, StringComparison.OrdinalIgnoreCase))
            {
                // Process nested content blocks within the current block
                var childResult = await processChildContentBlocksAsync(block, stc, context, parseAndSaveSectionAsync);
                result.NestedContent.AddRange(childResult.Item1);
                result.GrandchildEntityCount += childResult.Item2;
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing SectionTextContent record in the database or creates a new one.
        /// This method encapsulates the database look-up and creation logic.
        /// </summary>
        /// <param name="block">The XElement representing the content block.</param>
        /// <param name="sectionId">The owning section ID.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="parentId">Parent content ID for hierarchy.</param>
        /// <param name="sequence">Sequence number for ordering.</param>
        /// <returns>The found or newly created SectionTextContent entity.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<SectionTextContent?> getOrCreateSectionTextContentRecordAsync(
            XElement block,
            int sectionId,
            SplParseContext context,
            int? parentId,
            int sequence)
        {
            #region implementation
            if (context == null || context.ServiceProvider == null)
                return null;

            // Get database context and repository for content operations
            var dbContext = context.GetDbContext();
            var repo = context.GetRepository<SectionTextContent>();

            // Standardize content type name with proper capitalization
            var contentType = char.ToUpper(block.Name.LocalName[0]) + block.Name.LocalName.Substring(1);

            // Extract optional style code for formatting information
            var styleCode = block.Attribute(sc.A.StyleCode)?.Value?.Trim();

            // ContentText is null for container types like List or Table.
            // Extract content text only for non-container elements
            string? contentText = null;
            if (!contentType.Equals(sc.E.List, StringComparison.OrdinalIgnoreCase) &&
                !contentType.Equals(sc.E.Table, StringComparison.OrdinalIgnoreCase))
            {
                contentText = block.GetSplHtml(stripNamespaces: true);
            }

            // Deduplication: Find existing record based on a unique signature
            // Search for existing content with matching attributes and hierarchy
            var existing = await dbContext.Set<SectionTextContent>().FirstOrDefaultAsync(c =>
                c.SectionID == sectionId &&
                c.ContentType == contentType &&
                c.SequenceNumber == sequence &&
                c.ParentSectionTextContentID == parentId &&
                (contentType.ToLower() != sc.E.Paragraph || c.ContentText == contentText));

            // Return existing content if found to avoid duplicates
            if (existing != null)
            {
                return existing;
            }

            // Create a new record if not found
            // Build new content entity with extracted attributes and hierarchy
            var newStc = new SectionTextContent
            {
                SectionID = sectionId,
                ParentSectionTextContentID = parentId,
                ContentType = contentType,
                StyleCode = styleCode,
                SequenceNumber = sequence,
                ContentText = contentText
            };

            // Persist new content to database
            await repo.CreateAsync(newStc);
            return newStc;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Dispatches processing for special content types that have nested data structures,
        /// such as Lists, Tables, and Excerpts with Highlights.
        /// </summary>
        /// <param name="block">The XElement representing the content block.</param>
        /// <param name="stc">The SectionTextContent entity for this block.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The number of grandchild entities created (e.g., list items, table cells).</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> processSpecializedContentAsync(XElement block,
            SectionTextContent stc,
            SplParseContext context)
        {
            #region implementation
            // Validate content entity has valid ID before processing children
            if (!stc.SectionTextContentID.HasValue) return 0;

            int grandchildEntitiesCount = 0;
            var contentType = stc.ContentType ?? string.Empty;

            // Dispatch to appropriate specialized handler based on content type
            if (contentType.Equals(sc.E.List, StringComparison.OrdinalIgnoreCase))
            {
                // Process list structure and create list item entities
                grandchildEntitiesCount += await getOrCreateTextListAndItemsAsync(block, stc.SectionTextContentID.Value, context);
            }
            else if (contentType.Equals(sc.E.Table, StringComparison.OrdinalIgnoreCase))
            {
                // Process table structure and create row/cell entities
                grandchildEntitiesCount += await getOrCreateTextTableAndChildrenAsync(block, stc.SectionTextContentID.Value, context);
            }
            else if (contentType.Equals(sc.E.Excerpt, StringComparison.OrdinalIgnoreCase))
            {
                // This method doesn't return a count, but we call it for its side effect.
                // Process excerpt highlights for specialized content extraction
                if (stc.SectionID > 0)
                    await getOrCreateSectionExcerptHighlightsAsync(block, (int)stc.SectionID, context);
            }
            else if (contentType.Equals(sc.E.RenderMultimedia, StringComparison.OrdinalIgnoreCase))
            {
                // Handle block-level images, where <renderMultimedia> is its own content block.
                grandchildEntitiesCount += await _mediaParser.ParseRenderedMediaAsync(block, stc.SectionTextContentID.Value, context, isInline: false);
            }

            // Check for INLINE images inside other content types, like Paragraph.
            // This runs in addition to the handlers above.
            if (block.Descendants(ns + sc.E.RenderMultimedia).Any())
            {
                // If the block itself isn't a RenderMultiMedia tag, any images inside it must be inline.
                bool isInline = !contentType.Equals(sc.E.RenderMultimedia, StringComparison.OrdinalIgnoreCase);
                if (isInline)
                {
                    grandchildEntitiesCount += await _mediaParser.ParseRenderedMediaAsync(block, stc.SectionTextContentID.Value, context, isInline: true);
                }
            }

            return grandchildEntitiesCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles the recursive processing of nested content blocks within a given parent block.
        /// Manages hierarchy relationships and sequence numbering for child content.
        /// </summary>
        /// <param name="parentBlock">The parent XElement containing child blocks.</param>
        /// <param name="parentStc">The parent SectionTextContent entity.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="parseAndSaveSectionAsync">Function delegate for parsing child sections.</param>
        /// <returns>A tuple containing the list of nested SectionTextContent objects and the count of their grandchild entities.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<Tuple<List<SectionTextContent>, int>> processChildContentBlocksAsync(
            XElement parentBlock,
            SectionTextContent parentStc,
            SplParseContext context,
            Func<XElement, SplParseContext, Task<Section>> parseAndSaveSectionAsync)
        {
            #region implementation
            // Find child content blocks within the parent element
            var childBlocks = parentBlock.SplBuildSectionContentTree().ToList();
            if (!childBlocks.Any() || parentStc == null || parentStc.SectionID == null)
            {
                // Return empty results if no child blocks found
                return Tuple.Create(new List<SectionTextContent>(), 0);
            }

            // Recurse by calling the main orchestrator for the children of the current block.
            // The current block's ID becomes the parent ID for the next level.
            // Recursively process child blocks with current block as parent
            return await GetOrCreateSectionTextContentsAsync(
                parentBlock,
                (int)parentStc.SectionID,
                context,
                parseAndSaveSectionAsync,
                parentStc.SectionTextContentID,
                1); // Child sequence always restarts at 1 for each new parent.
            #endregion
        }

        #endregion

        #region Section Content Parsing - Bulk Operations

        /**************************************************************/
        /// <summary>
        /// Orchestrates the recursive parsing of a section's [text] element. It iterates
        /// through top-level content blocks and delegates the processing of each block
        /// to specialized helpers, preserving the nested hierarchy for SPL round-tripping.
        /// REFACTORED: Uses bulk operations pattern for 100-1000x performance improvement.
        /// </summary>
        /// <param name="parentEl">The XElement to start parsing (typically the [text] element).</param>
        /// <param name="sectionId">The SectionID owning this content.</param>
        /// <param name="context">Parsing context.</param>
        /// <param name="parseAndSaveSectionAsync">Function delegate for parsing and saving child sections.</param>
        /// <param name="parentSectionTextContentId">Parent SectionTextContentID for hierarchy, or null for top-level blocks.</param>
        /// <param name="sequence">The sequence number to start from (default 1).</param>
        /// <returns>A tuple containing the complete list of all created/found SectionTextContent objects and the total count of grandchild entities (e.g., list items, table cells).</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<Tuple<List<SectionTextContent>, int>> getOrCreateSectionTextContentsAsync_bulkCalls(
            XElement parentEl,
            int sectionId,
            SplParseContext context,
            Func<XElement, SplParseContext, Task<Section>> parseAndSaveSectionAsync,
            int? parentSectionTextContentId = null,
            int sequence = 1)
        {
            #region implementation
            var allContent = new List<SectionTextContent>();
            int totalGrandChildEntities = 0;

            // Validate all required input parameters and context dependencies
            if (parentEl == null || sectionId <= 0 || context?.ServiceProvider == null || context.Logger == null)
            {
                return Tuple.Create(allContent, totalGrandChildEntities);
            }

            var dbContext = context.GetDbContext();

            // PHASE 1: Parse all content blocks to DTOs (0 DB calls)
            var dtos = parseContentBlocksToMemory(parentEl, sectionId, parentSectionTextContentId?.ToString(), sequence);

            if (!dtos.Any())
            {
                return Tuple.Create(allContent, totalGrandChildEntities);
            }

            // PHASE 2: Bulk query existing content (1 query)
            var existingKeys = await getExistingSectionTextContentKeysAsync(dbContext, sectionId);

            // PHASE 3: Bulk insert missing content (1-2 inserts for parent/child hierarchy)
            var (createdEntities, idLookup) = await bulkCreateSectionTextContentAsync(dbContext, dtos, existingKeys);

            // Retrieve all SectionTextContent entities for this section (including existing ones)
            var allEntities = await dbContext.Set<SectionTextContent>()
                .Where(c => c.SectionID == sectionId)
                .ToListAsync();

            allContent.AddRange(allEntities);

            // PHASE 4: Process specialized content (lists, tables, excerpts) using the ID lookup
            foreach (var dto in dtos)
            {
                // Get the database ID for this content block
                int? contentId = null;
                if (idLookup.ContainsKey(dto.TempId))
                {
                    contentId = idLookup[dto.TempId];
                }
                else
                {
                    // Find in existing entities
                    var existing = allEntities.FirstOrDefault(e =>
                        e.SectionID == dto.SectionID &&
                        e.ContentType == dto.ContentType &&
                        e.SequenceNumber == dto.SequenceNumber &&
                        e.ParentSectionTextContentID == dto.ParentSectionTextContentID &&
                        (dto.ContentType.ToLower() != sc.E.Paragraph || e.ContentText == dto.ContentText));

                    contentId = existing?.SectionTextContentID;
                }

                if (contentId.HasValue)
                {
                    // Find the actual entity
                    var stc = allEntities.FirstOrDefault(e => e.SectionTextContentID == contentId.Value);
                    if (stc != null)
                    {
                        // Process specialized content
                        totalGrandChildEntities += await processSpecializedContentAsync(dto.SourceElement, stc, context);
                    }
                }
            }

            return Tuple.Create(allContent, totalGrandChildEntities);
            #endregion
        }

        #region Phase 1: Parse to DTOs (Memory Only)

        /**************************************************************/
        /// <summary>
        /// Phase 1: Parses all content blocks from XML into DTOs without any database calls.
        /// Recursively processes nested hierarchy and assigns temporary IDs for relationship tracking.
        /// </summary>
        /// <param name="parentEl">The XElement to start parsing (typically the [text] element).</param>
        /// <param name="sectionId">The SectionID owning this content.</param>
        /// <param name="parentTempId">Parent's temporary identifier for hierarchy, or null for top-level blocks.</param>
        /// <param name="sequence">The sequence number to start from (default 1).</param>
        /// <returns>A flattened list of all DTOs including nested content.</returns>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private List<SectionTextContentDto> parseContentBlocksToMemory(
            XElement parentEl,
            int sectionId,
            string? parentTempId = null,
            int sequence = 1)
        {
            #region implementation
            var allDtos = new List<SectionTextContentDto>();

            if (parentEl == null || sectionId <= 0)
            {
                return allDtos;
            }

            int seq = sequence;
            int tempIdCounter = 0;

            // Process each content block in the section
            foreach (var block in parentEl.SplBuildSectionContentTree())
            {
                // Skip highlight elements as they are handled separately
                if (block.Name.LocalName.Equals(sc.E.Highlight, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Generate temporary ID for this block
                var tempId = parentTempId != null
                    ? $"{parentTempId}_{seq}_{tempIdCounter++}"
                    : $"root_{seq}_{tempIdCounter++}";

                // Extract content type and standardize capitalization
                var contentType = char.ToUpper(block.Name.LocalName[0]) + block.Name.LocalName.Substring(1);

                // Extract optional style code
                var styleCode = block.Attribute(sc.A.StyleCode)?.Value?.Trim();

                // ContentText is null for container types like List or Table
                string? contentText = null;
                if (!contentType.Equals(sc.E.List, StringComparison.OrdinalIgnoreCase) &&
                    !contentType.Equals(sc.E.Table, StringComparison.OrdinalIgnoreCase))
                {
                    contentText = block.GetSplHtml(stripNamespaces: true);
                }

                // Create DTO for this block
                var dto = new SectionTextContentDto
                {
                    SectionID = sectionId,
                    ParentSectionTextContentID = null, // Will be set later via lookup
                    ContentType = contentType,
                    StyleCode = styleCode,
                    SequenceNumber = seq,
                    ContentText = contentText,
                    SourceElement = block,
                    TempId = tempId,
                    ParentTempId = parentTempId
                };

                allDtos.Add(dto);

                // Recursively process child blocks for non-specialized content types
                // Lists and Tables handle their own children differently
                if (!contentType.Equals(sc.E.List, StringComparison.OrdinalIgnoreCase) &&
                    !contentType.Equals(sc.E.Table, StringComparison.OrdinalIgnoreCase))
                {
                    var childBlocks = block.SplBuildSectionContentTree().ToList();
                    if (childBlocks.Any())
                    {
                        // Parse children recursively
                        var childDtos = parseContentBlocksToMemory(block, sectionId, tempId, 1);
                        allDtos.AddRange(childDtos);
                    }
                }

                seq++;
            }

            return allDtos;
            #endregion
        }

        #endregion

        #region Phase 2: Bulk Query Existing

        /**************************************************************/
        /// <summary>
        /// Phase 2: Queries database once to get all existing SectionTextContent records for the section.
        /// Returns a HashSet of composite keys for efficient duplicate detection.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="sectionId">The section ID to query.</param>
        /// <returns>A HashSet containing composite keys of existing content blocks.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)>> getExistingSectionTextContentKeysAsync(
        ApplicationDbContext dbContext,
        int sectionId)
        {
            #region implementation
            var existing = await dbContext.Set<SectionTextContent>()
                .Where(c => c.SectionID == sectionId)
                .Select(c => new
                {
                    c.SectionID,
                    c.ContentType,
                    c.SequenceNumber,
                    c.ParentSectionTextContentID,
                    c.ContentText
                })
                .ToListAsync();

            var keys = new HashSet<(int, string, int, int?, string?)>(existing
                .Where(e => e.SectionID.HasValue && e.SequenceNumber.HasValue)
                .Select(e => (
                    e.SectionID!.Value,
                    e.ContentType ?? string.Empty,
                    e.SequenceNumber!.Value,
                    e.ParentSectionTextContentID,
                    e.ContentText
                ))
            );

            return keys;
            #endregion
        }

        #endregion

        #region Phase 3: Bulk Insert Missing

        /**************************************************************/
        /// <summary>
        /// Phase 3: Orchestrates bulk insertion of SectionTextContent records with hierarchical relationships.
        /// Coordinates two-pass processing: top-level entities first, then child entities.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="dtos">The list of DTOs to insert.</param>
        /// <param name="existingKeys">HashSet of existing content keys for deduplication.</param>
        /// <returns>A tuple containing the created entities and a lookup dictionary mapping temp IDs to database IDs.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<(List<SectionTextContent> entities, Dictionary<string, int> idLookup)>
            bulkCreateSectionTextContentAsync(
                ApplicationDbContext dbContext,
                List<SectionTextContentDto> dtos,
                HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)> existingKeys)
        {
            #region implementation
            var createdEntities = new List<SectionTextContent>();
            var tempIdToDbIdLookup = new Dictionary<string, int>(StringComparer.Ordinal);

            if (!dtos.Any())
            {
                return (createdEntities, tempIdToDbIdLookup);
            }

            // Pass 1: Process top-level entities (no parent)
            var topLevelDtos = filterTopLevelDtos(dtos);
            await createTopLevelEntitiesAsync(
                dbContext,
                topLevelDtos,
                existingKeys,
                createdEntities,
                tempIdToDbIdLookup);

            await addExistingTopLevelToLookupAsync(
                dbContext,
                topLevelDtos,
                existingKeys,
                dtos.First().SectionID,
                tempIdToDbIdLookup);

            // Pass 2: Process child entities (with parent references)
            var childDtos = filterChildDtos(dtos);
            await createChildEntitiesAsync(
                dbContext,
                childDtos,
                existingKeys,
                tempIdToDbIdLookup,
                createdEntities);

            return (createdEntities, tempIdToDbIdLookup);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Filters DTOs to return only top-level entities (those without a parent).
        /// </summary>
        /// <param name="dtos">The complete list of DTOs.</param>
        /// <returns>List of DTOs where ParentTempId is null.</returns>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        private List<SectionTextContentDto> filterTopLevelDtos(List<SectionTextContentDto> dtos)
        {
            #region implementation
            return dtos.Where(dto => dto.ParentTempId == null).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Filters DTOs to return only child entities (those with a parent reference).
        /// </summary>
        /// <param name="dtos">The complete list of DTOs.</param>
        /// <returns>List of DTOs where ParentTempId is not null.</returns>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        private List<SectionTextContentDto> filterChildDtos(List<SectionTextContentDto> dtos)
        {
            #region implementation
            return dtos.Where(dto => dto.ParentTempId != null).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates and persists top-level SectionTextContent entities, building ID lookup mappings.
        /// Filters out duplicates using the existingKeys set before creating entities.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="topLevelDtos">DTOs representing top-level entities.</param>
        /// <param name="existingKeys">HashSet for deduplication checking.</param>
        /// <param name="createdEntities">Output collection of created entities.</param>
        /// <param name="tempIdToDbIdLookup">Output dictionary mapping temporary IDs to database IDs.</param>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task createTopLevelEntitiesAsync(
            ApplicationDbContext dbContext,
            List<SectionTextContentDto> topLevelDtos,
            HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)> existingKeys,
            List<SectionTextContent> createdEntities,
            Dictionary<string, int> tempIdToDbIdLookup)
        {
            #region implementation
            // Filter new entities not already in database
            var newDtos = filterNewDtos(topLevelDtos, existingKeys);

            if (!newDtos.Any())
            {
                return;
            }

            // Create entity instances
            var newEntities = newDtos
                .Select(dto => createEntityFromDto(dto, parentId: null))
                .ToList();

            // Persist to database
            dbContext.Set<SectionTextContent>().AddRange(newEntities);
            await dbContext.SaveChangesAsync();

            // Build temp ID to DB ID lookup
            buildIdLookup(newEntities, newDtos, tempIdToDbIdLookup);

            createdEntities.AddRange(newEntities);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Queries existing top-level entities from database and adds them to the ID lookup.
        /// Matches DTOs to existing entities by deduplication key.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="topLevelDtos">DTOs representing top-level entities.</param>
        /// <param name="existingKeys">HashSet for deduplication checking.</param>
        /// <param name="sectionId">The section ID to filter by.</param>
        /// <param name="tempIdToDbIdLookup">Dictionary to update with existing entity mappings.</param>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task addExistingTopLevelToLookupAsync(
            ApplicationDbContext dbContext,
            List<SectionTextContentDto> topLevelDtos,
            HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)> existingKeys,
            int sectionId,
            Dictionary<string, int> tempIdToDbIdLookup)
        {
            #region implementation
            // Query existing top-level entities
            var existingTopLevel = await dbContext.Set<SectionTextContent>()
                .Where(c => c.SectionID == sectionId && c.ParentSectionTextContentID == null)
                .ToListAsync();

            // Match existing entities to DTOs and build lookup
            foreach (var existing in existingTopLevel)
            {
                var matchingDto = findMatchingDto(existing, topLevelDtos, existingKeys);

                if (matchingDto != null && existing.SectionTextContentID.HasValue)
                {
                    tempIdToDbIdLookup[matchingDto.TempId] = existing.SectionTextContentID.Value;
                }
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates and persists child SectionTextContent entities with resolved parent references.
        /// Resolves parent IDs from lookup before creating entities.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="childDtos">DTOs representing child entities.</param>
        /// <param name="existingKeys">HashSet for deduplication checking.</param>
        /// <param name="tempIdToDbIdLookup">Dictionary mapping temporary IDs to database IDs.</param>
        /// <param name="createdEntities">Output collection of created entities.</param>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task createChildEntitiesAsync(
            ApplicationDbContext dbContext,
            List<SectionTextContentDto> childDtos,
            HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)> existingKeys,
            Dictionary<string, int> tempIdToDbIdLookup,
            List<SectionTextContent> createdEntities)
        {
            #region implementation
            if (!childDtos.Any())
            {
                return;
            }

            // Resolve parent IDs and filter new entities
            var newChildDtos = resolveParentIdsAndFilterNew(childDtos, tempIdToDbIdLookup, existingKeys);

            if (!newChildDtos.Any())
            {
                return;
            }

            // Create entity instances with resolved parent IDs
            var newEntities = newChildDtos
                .Select(dto => createEntityFromDto(dto, dto.ParentSectionTextContentID))
                .ToList();

            // Persist to database
            dbContext.Set<SectionTextContent>().AddRange(newEntities);
            await dbContext.SaveChangesAsync();

            // Build temp ID to DB ID lookup for children
            buildIdLookup(newEntities, newChildDtos, tempIdToDbIdLookup);

            createdEntities.AddRange(newEntities);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Filters DTOs to include only those not already in the database.
        /// Uses deduplication key matching to identify new records.
        /// </summary>
        /// <param name="dtos">DTOs to filter.</param>
        /// <param name="existingKeys">HashSet of existing content keys.</param>
        /// <returns>List of DTOs representing new entities.</returns>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        private List<SectionTextContentDto> filterNewDtos(
            List<SectionTextContentDto> dtos,
            HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)> existingKeys)
        {
            #region implementation
            return dtos
                .Where(dto =>
                {
                    var key = buildDeduplicationKey(dto);
                    return !existingKeys.Contains(key);
                })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a deduplication key tuple from a DTO for uniqueness checking.
        /// </summary>
        /// <param name="dto">The DTO to build key from.</param>
        /// <returns>Tuple containing key fields for deduplication.</returns>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        private (int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)
            buildDeduplicationKey(SectionTextContentDto dto)
        {
            #region implementation
            return (
                dto.SectionID,
                dto.ContentType,
                dto.SequenceNumber,
                dto.ParentSectionTextContentID,
                dto.ContentText
            );
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a SectionTextContent entity from a DTO with specified parent ID.
        /// </summary>
        /// <param name="dto">The DTO to convert.</param>
        /// <param name="parentId">The resolved parent ID or null for top-level entities.</param>
        /// <returns>New SectionTextContent entity instance.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        private SectionTextContent createEntityFromDto(SectionTextContentDto dto, int? parentId)
        {
            #region implementation
            return new SectionTextContent
            {
                SectionID = dto.SectionID,
                ParentSectionTextContentID = parentId,
                ContentType = dto.ContentType,
                StyleCode = dto.StyleCode,
                SequenceNumber = dto.SequenceNumber,
                ContentText = dto.ContentText
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds mapping from temporary DTO IDs to persisted database IDs.
        /// Matches entities to DTOs by index position.
        /// </summary>
        /// <param name="entities">List of persisted entities with database IDs.</param>
        /// <param name="dtos">List of DTOs with temporary IDs.</param>
        /// <param name="tempIdToDbIdLookup">Dictionary to populate with ID mappings.</param>
        /// <remarks>
        /// Assumes entities and DTOs are in the same order after filtering.
        /// </remarks>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        private void buildIdLookup(
            List<SectionTextContent> entities,
            List<SectionTextContentDto> dtos,
            Dictionary<string, int> tempIdToDbIdLookup)
        {
            #region implementation
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                var dto = dtos.ElementAtOrDefault(i);

                if (dto != null && entity.SectionTextContentID.HasValue)
                {
                    tempIdToDbIdLookup[dto.TempId] = entity.SectionTextContentID.Value;
                }
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds a DTO that matches an existing entity by deduplication key.
        /// </summary>
        /// <param name="entity">The existing entity from database.</param>
        /// <param name="dtos">List of DTOs to search.</param>
        /// <param name="existingKeys">HashSet of existing content keys.</param>
        /// <returns>Matching DTO or null if no match found.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        private SectionTextContentDto? findMatchingDto(
            SectionTextContent entity,
            List<SectionTextContentDto> dtos,
            HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)> existingKeys)
        {
            #region implementation
            return dtos.FirstOrDefault(dto =>
            {
                var key = buildDeduplicationKey(dto);

                // Must be in existingKeys and all fields must match
                return existingKeys.Contains(key) &&
                       entity.SectionID == dto.SectionID &&
                       entity.ContentType == dto.ContentType &&
                       entity.SequenceNumber == dto.SequenceNumber &&
                       entity.ParentSectionTextContentID == dto.ParentSectionTextContentID &&
                       entity.ContentText == dto.ContentText;
            });
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves parent temporary IDs to database IDs and filters for new child entities.
        /// Updates DTO ParentSectionTextContentID properties with resolved IDs.
        /// </summary>
        /// <param name="childDtos">DTOs representing child entities.</param>
        /// <param name="tempIdToDbIdLookup">Dictionary mapping temporary IDs to database IDs.</param>
        /// <param name="existingKeys">HashSet for deduplication checking.</param>
        /// <returns>List of child DTOs with resolved parent IDs that are new to database.</returns>
        /// <remarks>
        /// Mutates the ParentSectionTextContentID property of DTOs as side effect.
        /// </remarks>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        private List<SectionTextContentDto> resolveParentIdsAndFilterNew(
            List<SectionTextContentDto> childDtos,
            Dictionary<string, int> tempIdToDbIdLookup,
            HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)> existingKeys)
        {
            #region implementation
            return childDtos
                .Where(dto =>
                {
                    // Only process if parent ID exists in lookup
                    if (!tempIdToDbIdLookup.ContainsKey(dto.ParentTempId!))
                        return false;

                    // Resolve parent ID (mutates DTO)
                    dto.ParentSectionTextContentID = tempIdToDbIdLookup[dto.ParentTempId!];

                    // Check if this is a new entity
                    var key = buildDeduplicationKey(dto);
                    return !existingKeys.Contains(key);
                })
                .ToList();
            #endregion
        }

        #endregion

        #endregion

        #region List Processing Methods - Feature Switched Entry

        /**************************************************************/
        /// <summary>
        /// Feature-switched entry point for parsing a [list] element and its child [item] elements.
        /// Routes to either bulk operations or single-call implementation based on context configuration.
        /// </summary>
        /// <param name="listEl">The XElement representing the [list] element.</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent record (where ContentType='List').</param>
        /// <param name="context">The current parsing context containing configuration flags and service dependencies.</param>
        /// <returns>A task that resolves to the total number of TextList and TextListItem entities created.</returns>
        /// <remarks>
        /// Routes between bulk operations (optimized for large lists) and single-call operations (simpler logic).
        /// Bulk operations reduce database calls from N to 2 per entity type.
        /// </remarks>
        /// <seealso cref="TextList"/>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> getOrCreateTextListAndItemsAsync(
            XElement listEl,
            int sectionTextContentId,
            SplParseContext context)
        {
            #region implementation

            if (context.UseBulkOperations)
            {
                return await getOrCreateTextListAndItemsAsync_bulkCalls(
                    listEl,
                    sectionTextContentId,
                    context);
            }
            else
            {
                return await getOrCreateTextListAndItemsAsync_singleCalls(
                    listEl,
                    sectionTextContentId,
                    context);
            }

            #endregion
        }

        #endregion

        #region List Processing Methods - Individual Operations (N + 1)

        /**************************************************************/
        /// <summary>
        /// Parses a [list] element and its child [item] elements, creating and saving
        /// TextList and TextListItem records to the database. This method handles the
        /// specific structure of SPL lists, including attributes and nested content.
        /// It performs deduplication to avoid creating duplicate records for the same content.
        /// </summary>
        /// <param name="listEl">The XElement representing the [list] element.</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent record (where ContentType='List').</param>
        /// <param name="context">The current parsing context for database access.</param>
        /// <returns>A task that resolves to the total number of TextList and TextListItem entities created.</returns>
        /// <remarks>
        /// Assumes the SplConstants class (aliased as 'sc') contains constants for list elements and attributes:
        /// sc.A.ListType ("listType"), sc.A.StyleCode ("styleCode"), sc.E.Item ("item"), sc.E.Caption ("caption").
        /// </remarks>
        /// <seealso cref="TextList"/>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> getOrCreateTextListAndItemsAsync_singleCalls(
            XElement listEl,
            int sectionTextContentId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate inputs using existing validation pattern
            if (!validateTextListInputs(listEl, sectionTextContentId, context))
            {
                return 0;
            }

            // Get database context and repositories
            var repositories = getTextListRepositories(context);

            if (repositories != null
                && repositories.TextListRepo != null
                && repositories.TextListItemRepo != null)
            {
                var dbContext = repositories.DbContext;

                // Find or create the main TextList record
                if (dbContext != null)
                {
                    var textList = await getOrCreateTextListAsync(dbContext, repositories.TextListRepo, listEl, sectionTextContentId);
                    if (textList?.TextListID == null)
                    {
                        context.Logger?.LogError("Failed to create or retrieve TextList for SectionTextContentID {id}", sectionTextContentId);
                        return createdCount;
                    }

                    // Increment count if a new TextList was created
                    if (textList.TextListID > 0)
                    {
                        createdCount++;
                    }

                    // Process all list items
                    createdCount += await processTextListItems(dbContext, repositories.TextListItemRepo, listEl, textList, context);
                }
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the input parameters for text list processing.
        /// </summary>
        /// <param name="listEl">The XElement representing the list.</param>
        /// <param name="sectionTextContentId">The section text content ID.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>True if all inputs are valid, false otherwise.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static bool validateTextListInputs(XElement listEl, int sectionTextContentId, SplParseContext context)
        {
            #region implementation
            // Check for null or invalid parameters
            return listEl != null &&
                   sectionTextContentId > 0 &&
                   context?.ServiceProvider != null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the required repositories and database context for text list processing.
        /// </summary>
        /// <param name="context">The parsing context.</param>
        /// <returns>A tuple containing the database context and repositories.</returns>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="TextList"/>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="Label"/>
        private static TextListRepositories? getTextListRepositories(SplParseContext context)
        {
            #region implementation
            // Validate context and service provider
            if (context == null || context.ServiceProvider == null)
            {
                return null;
            }

            // Get database context and repositories from service provider
            var dbContext = context.GetDbContext();
            var textListRepo = context.GetRepository<TextList>();
            var textListItemRepo = context.GetRepository<TextListItem>();

            return new TextListRepositories
            {
                DbContext = dbContext,
                TextListRepo = textListRepo,
                TextListItemRepo = textListItemRepo
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates a TextList record for the given list element and section content.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="textListRepo">The TextList repository.</param>
        /// <param name="listEl">The XElement representing the list.</param>
        /// <param name="sectionTextContentId">The section text content ID.</param>
        /// <returns>The existing or newly created TextList entity.</returns>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="TextList"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private static async Task<TextList> getOrCreateTextListAsync(
            ApplicationDbContext dbContext,
            Repository<TextList> textListRepo,
            XElement listEl,
            int sectionTextContentId)
        {
            #region implementation
            // Check if TextList already exists for this section content
            var textListDbSet = dbContext.Set<TextList>();
            var textList = await textListDbSet.FirstOrDefaultAsync(l => l.SectionTextContentID == sectionTextContentId);

            // Create new TextList if it doesn't exist
            if (textList == null)
            {
                textList = createTextListEntity(listEl, sectionTextContentId);
                await textListRepo.CreateAsync(textList);
            }

            return textList;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new TextList entity from the provided list element and section content ID.
        /// </summary>
        /// <param name="listEl">The XElement representing the list.</param>
        /// <param name="sectionTextContentId">The section text content ID.</param>
        /// <returns>A new TextList entity with populated attributes.</returns>
        /// <seealso cref="TextList"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private static TextList createTextListEntity(XElement listEl, int sectionTextContentId)
        {
            #region implementation
            // Extract list attributes and create new entity
            return new TextList
            {
                SectionTextContentID = sectionTextContentId,
                ListType = listEl.Attribute(sc.A.ListType)?.Value,
                StyleCode = listEl.Attribute(sc.A.StyleCode)?.Value
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes all list items within the list element, creating TextListItem records as needed.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="textListItemRepo">The TextListItem repository.</param>
        /// <param name="listEl">The XElement representing the list.</param>
        /// <param name="textList">The parent TextList entity.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>The count of TextListItem records created.</returns>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="TextList"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static async Task<int> processTextListItems(
            ApplicationDbContext dbContext,
            Repository<TextListItem> textListItemRepo,
            XElement listEl,
            TextList textList,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;
            var textListItemDbSet = dbContext.Set<TextListItem>();

            // Get all item elements from the list
            var itemElements = listEl.SplElements(sc.E.Item).ToList();
            int seqNum = 1;

            // Process each item element
            foreach (var itemEl in itemElements)
            {
                var itemProcessResult = await processTextListItem(
                    textListItemDbSet, textListItemRepo, itemEl, textList, seqNum);

                // Only increment sequence if item was processed (had content)
                if (itemProcessResult.WasProcessed)
                {
                    if (itemProcessResult.WasCreated)
                    {
                        createdCount++;
                    }
                    seqNum++;
                }
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes a single list item element, creating a TextListItem record if needed.
        /// </summary>
        /// <param name="textListItemDbSet">The TextListItem DbSet for querying.</param>
        /// <param name="textListItemRepo">The TextListItem repository for creation.</param>
        /// <param name="itemEl">The XElement representing the item.</param>
        /// <param name="textList">The parent TextList entity.</param>
        /// <param name="seqNum">The sequence number for this item.</param>
        /// <returns>A result indicating whether the item was processed and created.</returns>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="TextList"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private static async Task<TextListItemProcessResult> processTextListItem(
            DbSet<TextListItem> textListItemDbSet,
            Repository<TextListItem> textListItemRepo,
            XElement itemEl,
            TextList textList,
            int seqNum)
        {
            #region implementation
            // Extract the content of the item
            var itemText = itemEl?.GetSplHtml(stripNamespaces: true);

            // Skip items with empty content to prevent creating empty records
            if (string.IsNullOrWhiteSpace(itemText))
            {
                return new TextListItemProcessResult { WasProcessed = false, WasCreated = false };
            }

            // Check for existing item with same sequence number
            var existingItem = await textListItemDbSet.FirstOrDefaultAsync(i =>
                i.TextListID == textList.TextListID &&
                i.SequenceNumber == seqNum);

            // Create new item if it doesn't exist
            if (existingItem == null && itemEl != null)
            {
                var newItem = createTextListItemEntity(itemEl, textList, seqNum, itemText);
                await textListItemRepo.CreateAsync(newItem);
                return new TextListItemProcessResult { WasProcessed = true, WasCreated = true };
            }

            return new TextListItemProcessResult { WasProcessed = true, WasCreated = false };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new TextListItem entity from the provided item element and metadata.
        /// </summary>
        /// <param name="itemEl">The XElement representing the item.</param>
        /// <param name="textList">The parent TextList entity.</param>
        /// <param name="seqNum">The sequence number for this item.</param>
        /// <param name="itemText">The extracted item text content.</param>
        /// <returns>A new TextListItem entity with populated properties.</returns>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="TextList"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private static TextListItem createTextListItemEntity(
            XElement itemEl,
            TextList textList,
            int seqNum,
            string itemText)
        {
            #region implementation
            // Extract caption and create new entity
            return new TextListItem
            {
                TextListID = textList.TextListID,
                SequenceNumber = seqNum,
                ItemCaption = itemEl.SplElement(sc.E.Caption)?.Value?.Trim(),
                ItemText = itemText?.Trim()
            };
            #endregion
        }

        #endregion

        #region List Processing Methods - Bulk Operations

        /**************************************************************/
        /// <summary>
        /// Parses a [list] element using bulk operations pattern. Collects all list items into memory,
        /// deduplicates against existing entities, then performs batch insert for optimal performance.
        /// </summary>
        /// <param name="listEl">The XElement representing the [list] element.</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent record (where ContentType='List').</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A task that resolves to the total number of TextList and TextListItem entities created.</returns>
        /// <remarks>
        /// Performance Pattern:
        /// - Before: N database calls (one per item)
        /// - After: 2 queries + 2 inserts (one per entity type)
        /// </remarks>
        /// <seealso cref="TextList"/>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> getOrCreateTextListAndItemsAsync_bulkCalls(
            XElement listEl,
            int sectionTextContentId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            if (!validateTextListInputs(listEl, sectionTextContentId, context))
            {
                return 0;
            }

            var dbContext = context.ServiceProvider!.GetRequiredService<ApplicationDbContext>();

            #region parse list structure into memory

            var itemDtos = parseTextListItemsToMemory(listEl);

            #endregion

            #region check existing entities and create missing

            var textListDbSet = dbContext.Set<TextList>();

            var existingList = await textListDbSet
                .FirstOrDefaultAsync(l => l.SectionTextContentID == sectionTextContentId);

            TextList textList;

            if (existingList == null)
            {
                textList = createTextListEntity(listEl, sectionTextContentId);
                textListDbSet.Add(textList);
                await dbContext.SaveChangesAsync();
                createdCount++;
            }
            else
            {
                textList = existingList;
            }

            if (textList.TextListID == null)
            {
                context.Logger?.LogError("Failed to create or retrieve TextList for SectionTextContentID {id}", sectionTextContentId);
                return createdCount;
            }

            createdCount += await bulkCreateTextListItemsAsync(dbContext, textList.TextListID.Value, itemDtos);

            #endregion

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all list items from a list element into memory without database operations.
        /// </summary>
        /// <param name="listEl">The XElement representing the [list] element to parse.</param>
        /// <returns>A list of TextListItemDto objects representing all items with content.</returns>
        /// <seealso cref="TextListItemDto"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private List<TextListItemDto> parseTextListItemsToMemory(XElement listEl)
        {
            #region implementation

            var itemDtos = new List<TextListItemDto>();
            var itemElements = listEl.SplElements(sc.E.Item).ToList();
            int seqNum = 1;

            foreach (var itemEl in itemElements)
            {
                if (itemEl == null)
                    continue;

                var itemText = itemEl?.GetSplHtml(stripNamespaces: true);

                if (!string.IsNullOrWhiteSpace(itemText))
                {
                    itemDtos.Add(new TextListItemDto
                    {
                        SequenceNumber = seqNum,
                        ItemCaption = itemEl?.SplElement(sc.E.Caption)?.Value?.Trim(),
                        ItemText = itemText?.Trim()
                    });

                    seqNum++;
                }
            }

            return itemDtos;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs bulk creation of TextListItem entities, checking for existing items
        /// and creating only missing ones in a single batch operation.
        /// </summary>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="textListId">The foreign key ID of the parent TextList.</param>
        /// <param name="itemDtos">The list of item DTOs parsed from XML.</param>
        /// <returns>The count of newly created TextListItem entities.</returns>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="TextListItemDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> bulkCreateTextListItemsAsync(
            ApplicationDbContext dbContext,
            int textListId,
            List<TextListItemDto> itemDtos)
        {
            #region implementation

            if (!itemDtos.Any())
                return 0;

            var itemDbSet = dbContext.Set<TextListItem>();

            var existingItems = await itemDbSet
                .Where(i => i.TextListID == textListId)
                .Select(i => new { i.TextListID, i.SequenceNumber })
                .ToListAsync();

            var existingKeys = new HashSet<(int ListId, int SeqNum)>(
                existingItems
                    .Where(i => i.TextListID.HasValue && i.SequenceNumber.HasValue)
                    .Select(i => (i.TextListID!.Value, i.SequenceNumber!.Value))
            );

            var newItems = itemDtos
                .Where(dto => !existingKeys.Contains((textListId, dto.SequenceNumber)))
                .Select(dto => new TextListItem
                {
                    TextListID = textListId,
                    SequenceNumber = dto.SequenceNumber,
                    ItemCaption = dto.ItemCaption,
                    ItemText = dto.ItemText
                })
                .ToList();

            if (newItems.Any())
            {
                itemDbSet.AddRange(newItems);
                await dbContext.SaveChangesAsync();
            }

            return newItems.Count;

            #endregion
        }

        #endregion

        #region Table Processing Methods - Feature Switched Entry

        /**************************************************************/
        /// <summary>
        /// Feature-switched entry point for parsing a [table] element and its child elements.
        /// Routes to either bulk operations or single-call implementation based on context configuration.
        /// </summary>
        /// <param name="tableEl">The XElement representing the [table] element to parse.</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent record (where ContentType='Table'). Each table has its own SectionTextContent record, so this ID uniquely identifies this specific table.</param>
        /// <param name="context">The current parsing context containing configuration flags and service dependencies.</param>
        /// <returns>A task that resolves to the total number of table, column, row, and cell entities created.</returns>
        /// <remarks>
        /// This method acts as a strategy pattern implementation, delegating to one of two parsing strategies:
        /// 
        /// 1. Bulk Operations (context.UseBulkOperations = true):
        ///    - Parses entire table structure into memory first
        ///    - Performs batch queries for existing entities
        ///    - Uses HashSet lookups for O(1) duplicate detection
        ///    - Executes bulk inserts (4 queries + 4 inserts total)
        ///    - Optimal for large tables or batch processing scenarios
        /// 
        /// 2. Single Call Operations (context.UseBulkOperations = false):
        ///    - Creates entities one at a time with immediate persistence
        ///    - Performs individual queries and inserts per entity
        ///    - Simpler logic but more database round-trips
        ///    - Better for small tables or when memory is constrained
        /// 
        /// The feature flag approach allows runtime switching between strategies without code changes,
        /// enabling A/B testing, gradual rollouts, or environment-specific optimizations.
        /// </remarks>
        /// <seealso cref="TextTable"/>
        /// <seealso cref="TextTableColumn"/>
        /// <seealso cref="TextTableRow"/>
        /// <seealso cref="TextTableCell"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        private async Task<int> getOrCreateTextTableAndChildrenAsync(
            XElement tableEl,
            int sectionTextContentId,
            SplParseContext context)
        {
            #region implementation

            // Route to bulk operations strategy when enabled for improved performance
            if (context.UseBulkOperations)
            {
                return await getOrCreateTextTableAndChildrenAsync_bulkCalls(
                    tableEl,
                    sectionTextContentId,
                    context);
            }
            // Fall back to single-call strategy for simpler execution path
            else
            {
                return await getOrCreateTextTableAndChildrenAsync_singleCalls(
                    tableEl,
                    sectionTextContentId,
                    context);
            }

            #endregion
        }

        #endregion

        #region Table Processing Methods - Individual Operations (N + 1)

        /**************************************************************/
        /// <summary>
        /// Parses a [table] element, creating the main TextTable record, column definitions,
        /// and then delegating the parsing of its header, body, and footer rows.
        /// </summary>
        /// <param name="tableEl">The XElement representing the [table] element.</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent record (where ContentType='Table'). Each table has its own SectionTextContent record, so this ID uniquely identifies this specific table.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A task that resolves to the total number of table, column, row, and cell entities created.</returns>
        /// <remarks>
        /// This method expects a one-to-one relationship between SectionTextContent (ContentType='Table') and TextTable.
        /// Each table element in the SPL creates a separate SectionTextContent record, which then gets one TextTable record.
        /// </remarks>
        /// <seealso cref="TextTable"/>
        /// <seealso cref="TextTableColumn"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> getOrCreateTextTableAndChildrenAsync_singleCalls(
            XElement tableEl,
            int sectionTextContentId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate all required input parameters and context dependencies
            if (tableEl == null || sectionTextContentId <= 0 || context?.ServiceProvider == null)
            {
                return 0;
            }

            // Get database context and repository for table operations
            var dbContext = context.GetDbContext();
            var tableRepo = context.GetRepository<TextTable>();
            var tableDbSet = dbContext.Set<TextTable>();

            // Find or Create the TextTable record
            // Note: SectionTextContentID uniquely identifies this table because each table element
            // creates its own SectionTextContent record with ContentType='Table'
            var textTable = await tableDbSet.FirstOrDefaultAsync(t => t.SectionTextContentID == sectionTextContentId);

            if (textTable == null)
            {
                // Extract caption if present
                var captionEl = tableEl.SplElement(sc.E.Caption);
                string? captionText = null;
                if (captionEl != null)
                {
                    // Get caption content preserving inner formatting
                    captionText = captionEl.GetSplHtml(stripNamespaces: true);
                }

                // Create new table entity with metadata about structure and styling
                textTable = new TextTable
                {
                    SectionTextContentID = sectionTextContentId,
                    SectionTableLink = tableEl.Attribute(sc.A.ID)?.Value,
                    Width = tableEl.Attribute(sc.A.Width)?.Value,
                    Caption = captionText,
                    HasHeader = tableEl.SplElement(sc.E.Thead) != null,
                    HasFooter = tableEl.SplElement(sc.E.Tfoot) != null
                };

                // Persist new table to database
                await tableRepo.CreateAsync(textTable);
                createdCount++;
            }

            // Validate table creation was successful before proceeding
            if (textTable.TextTableID == null)
            {
                context.Logger?.LogError("Failed to create or retrieve TextTable for SectionTextContentID {id}", sectionTextContentId);
                return createdCount;
            }

            // Parse column definitions before processing rows
            createdCount += await parseAndCreateColumnsAsync(tableEl, textTable.TextTableID.Value, context);

            // Process table header section if present
            var theadEl = tableEl.SplElement(sc.E.Thead);
            if (theadEl != null)
            {
                createdCount += await parseAndCreateRowsAsync(theadEl, textTable.TextTableID.Value, "Header", context);
            }

            // Process table body section if present
            var tbodyEl = tableEl.SplElement(sc.E.Tbody);
            if (tbodyEl != null)
            {
                createdCount += await parseAndCreateRowsAsync(tbodyEl, textTable.TextTableID.Value, "Body", context);
            }

            // Process table footer section if present
            var tfootEl = tableEl.SplElement(sc.E.Tfoot);
            if (tfootEl != null)
            {
                createdCount += await parseAndCreateRowsAsync(tfootEl, textTable.TextTableID.Value, "Footer", context);
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and creates TextTableColumn entities from [col] and [colgroup] elements in a table.
        /// Handles both standalone [col] elements and [col] elements nested within [colgroup].
        /// </summary>
        /// <param name="tableEl">The XElement representing the [table] element to parse.</param>
        /// <param name="textTableId">The foreign key ID linking columns to their parent TextTable.</param>
        /// <param name="context">The SPL parse context containing database services and repositories.</param>
        /// <returns>
        /// The total count of newly created TextTableColumn entities.
        /// Returns 0 if context is invalid or no columns are found.
        /// </returns>
        /// <remarks>
        /// Per Section 2.2.2.5 of SPL Implementation Guide:
        /// - [colgroup] elements are optional and uncommon but must be supported
        /// - [col] elements can exist standalone or within [colgroup]
        /// - [colgroup] attributes provide defaults that individual [col] can override
        /// - Column width, alignment, and styleCode attributes must be preserved
        /// 
        /// Parsing Logic:
        /// 1. First processes all [colgroup] elements and their child [col] elements
        /// 2. Then processes standalone [col] elements not within any [colgroup]
        /// 3. Maintains sequence numbering across both types for proper rendering order
        /// 
        /// Backwards Compatibility:
        /// - Tables without [colgroup] work identically to previous implementation
        /// - ColGroupSequenceNumber remains null for standalone columns
        /// </remarks>
        /// <example>
        /// XML with colgroup:
        /// &lt;table&gt;
        ///   &lt;colgroup align="center" styleCode="Rrule"&gt;
        ///     &lt;col width="20%" /&gt;
        ///     &lt;col width="30%" align="left" /&gt;
        ///   &lt;/colgroup&gt;
        ///   &lt;col width="50%" /&gt;
        /// &lt;/table&gt;
        /// 
        /// Results in 3 columns:
        /// - Column 1: In colgroup #1, inherits center align and Rrule styleCode, 20% width
        /// - Column 2: In colgroup #1, overrides with left align, keeps Rrule styleCode, 30% width
        /// - Column 3: Standalone (no colgroup), 50% width
        /// </example>
        /// <seealso cref="TextTableColumn"/>
        /// <seealso cref="TextTable"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndCreateColumnsAsync(
            XElement tableEl,
            int textTableId,
            SplParseContext context)
        {
            #region implementation

            int createdCount = 0;

            // Validate required context dependencies for database operations
            if (context == null || context.ServiceProvider == null)
                return 0;

            // Get database context and repository for column operations
            var dbContext = context.GetDbContext();
            var columnRepo = context.GetRepository<TextTableColumn>();
            var columnDbSet = dbContext.Set<TextTableColumn>();

            // Initialize sequence number for maintaining column order across the entire table
            int overallSeqNum = 1;

            // Track colgroup sequence for identifying which columns belong to which colgroup
            int colgroupSeqNum = 1;

            #region process colgroup elements

            // Extract all colgroup elements to process columns within groups first
            var colgroupElements = tableEl.Elements(ns + sc.E.Colgroup).ToList();

            // Check if any colgroup elements exist
            if (colgroupElements.Any())
                // Process each colgroup and its child col elements
                foreach (var colgroupEl in colgroupElements)
                {
                    // Extract colgroup-level attributes that serve as defaults for child columns
                    var colgroupStyleCode = colgroupEl.Attribute(sc.A.StyleCode)?.Value;
                    var colgroupAlign = colgroupEl.Attribute(sc.A.Align)?.Value;
                    var colgroupVAlign = colgroupEl.Attribute(sc.A.VAlign)?.Value;

                    // Extract all col elements within this colgroup
                    var colElementsInGroup = colgroupEl.Elements(ns + sc.E.Col).ToList();

                    // Process each column definition within the colgroup
                    foreach (var colEl in colElementsInGroup)
                    {
                        // Create column entity with colgroup context
                        var createdCol = await createColumnEntityAsync(
                            columnDbSet,
                            columnRepo,
                            textTableId,
                            overallSeqNum,
                            colEl,
                            colgroupSeqNum,
                            colgroupStyleCode,
                            colgroupAlign,
                            colgroupVAlign);

                        // Track creation and increment sequence for next column
                        if (createdCol)
                        {
                            createdCount++;
                        }
                        overallSeqNum++;
                    }

                    // Increment colgroup sequence for next colgroup
                    colgroupSeqNum++;
                }

            #endregion

            #region process standalone col elements

            // Extract standalone col elements (those directly under table, not in colgroup)
            var standaloneColElements = tableEl.Elements(ns + sc.E.Col).ToList();

            // Check if any standalone col elements exist
            if (standaloneColElements.Any())
                // Process each standalone column definition
                foreach (var colEl in standaloneColElements)
                {
                    // Create column entity without colgroup context (all colgroup params null)
                    var createdCol = await createColumnEntityAsync(
                        columnDbSet,
                        columnRepo,
                        textTableId,
                        overallSeqNum,
                        colEl,
                        colgroupSequence: null,
                        colgroupStyleCode: null,
                        colgroupAlign: null,
                        colgroupVAlign: null);

                    // Track creation and increment sequence for next column
                    if (createdCol)
                    {
                        createdCount++;
                    }
                    overallSeqNum++;
                }

            #endregion

            return createdCount;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a single TextTableColumn entity from a [col] element, optionally with colgroup context.
        /// Checks for existing columns to avoid duplicates before creating new entities.
        /// </summary>
        /// <param name="columnDbSet">The DbSet for querying existing TextTableColumn entities.</param>
        /// <param name="columnRepo">The repository for creating new TextTableColumn entities.</param>
        /// <param name="textTableId">The foreign key ID of the parent TextTable.</param>
        /// <param name="sequenceNumber">The overall sequence number for this column within the table.</param>
        /// <param name="colElement">The XElement representing the [col] element to parse.</param>
        /// <param name="colgroupSequence">
        /// The colgroup sequence number if this column is within a [colgroup], null otherwise.
        /// </param>
        /// <param name="colgroupStyleCode">
        /// The styleCode attribute from parent [colgroup], null if standalone or not specified.
        /// </param>
        /// <param name="colgroupAlign">
        /// The align attribute from parent [colgroup], null if standalone or not specified.
        /// </param>
        /// <param name="colgroupVAlign">
        /// The valign attribute from parent [colgroup], null if standalone or not specified.
        /// </param>
        /// <returns>
        /// True if a new column entity was created, false if column already existed.
        /// </returns>
        /// <remarks>
        /// Individual [col] attributes always take precedence over [colgroup] attributes.
        /// When both are present, the column-level value is stored in the direct property
        /// (Width, Align, VAlign, StyleCode) while colgroup-level values are stored in
        /// ColGroup* properties for reference.
        /// 
        /// During rendering, the GetEffective* methods can be used to retrieve the
        /// appropriate value considering both levels.
        /// </remarks>
        /// <seealso cref="TextTableColumn"/>
        /// <seealso cref="parseAndCreateColumnsAsync"/>
        /// <seealso cref="Label"/>
        private async Task<bool> createColumnEntityAsync(
            DbSet<TextTableColumn> columnDbSet,
            Repository<TextTableColumn> columnRepo,
            int textTableId,
            int sequenceNumber,
            XElement colElement,
            int? colgroupSequence,
            string? colgroupStyleCode,
            string? colgroupAlign,
            string? colgroupVAlign)
        {
            #region implementation

            // Check for existing column to avoid duplicates based on table and sequence
            var existingCol = await columnDbSet.FirstOrDefaultAsync(c =>
                c.TextTableID == textTableId &&
                c.SequenceNumber == sequenceNumber);

            // Skip creation if column already exists at this position
            if (existingCol != null)
                return false;

            // Extract individual col element attributes
            var colWidth = colElement.Attribute(sc.A.Width)?.Value;
            var colAlign = colElement.Attribute(sc.A.Align)?.Value;
            var colVAlign = colElement.Attribute(sc.A.VAlign)?.Value;
            var colStyleCode = colElement.Attribute(sc.A.StyleCode)?.Value;

            // Build new column entity with all extracted attributes
            var newColumn = new TextTableColumn
            {
                // Core identification fields
                TextTableID = textTableId,
                SequenceNumber = sequenceNumber,

                // Colgroup membership and inherited attributes
                ColGroupSequenceNumber = colgroupSequence,
                ColGroupStyleCode = colgroupStyleCode,
                ColGroupAlign = colgroupAlign,
                ColGroupVAlign = colgroupVAlign,

                // Individual col element attributes (these take precedence)
                Width = colWidth,
                Align = colAlign,
                VAlign = colVAlign,
                StyleCode = colStyleCode
            };

            // Persist new column to database
            await columnRepo.CreateAsync(newColumn);

            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all rows ([tr]) within a given table group ([thead], [tbody], [tfoot]),
        /// creating TextTableRow records and delegating cell parsing.
        /// </summary>
        /// <param name="rowGroupEl">The XElement for the group (e.g., [tbody]).</param>
        /// <param name="textTableId">The database ID of the parent TextTable.</param>
        /// <param name="rowGroupType">The type of group: 'Header', 'Body', or 'Footer'.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A task that resolves to the total number of row and cell entities created.</returns>
        /// <seealso cref="TextTableRow"/>
        /// <seealso cref="TextTable"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndCreateRowsAsync(
            XElement rowGroupEl,
            int textTableId,
            string rowGroupType,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate required context dependencies for database operations
            if (context == null || context.ServiceProvider == null)
                return 0;

            // Get database context and repository for table row operations
            var dbContext = context.GetDbContext();
            var rowRepo = context.GetRepository<TextTableRow>();
            var rowDbSet = dbContext.Set<TextTableRow>();

            // Extract all table row elements from the current group
            var rowElements = rowGroupEl.SplElements(sc.E.Tr).ToList();

            // Initialize sequence number for maintaining row order within group
            int seqNum = 1;

            // Process each row element within the table group
            foreach (var rowEl in rowElements)
            {
                // Check for existing row to avoid duplicates based on table, group type, and sequence
                var existingRow = await rowDbSet.FirstOrDefaultAsync(r =>
                    r.TextTableID == textTableId &&
                    r.RowGroupType == rowGroupType &&
                    r.SequenceNumber == seqNum);

                TextTableRow textTableRow;

                // Use existing row if found, otherwise create new one
                if (existingRow != null)
                {
                    textTableRow = existingRow;
                }
                else
                {
                    // Create new table row entity with group classification and styling
                    textTableRow = new TextTableRow
                    {
                        TextTableID = textTableId,
                        RowGroupType = rowGroupType, // 'Header', 'Body', or 'Footer'
                        SequenceNumber = seqNum,
                        StyleCode = rowEl.Attribute(sc.A.StyleCode)?.Value
                    };

                    // Persist new row to database
                    await rowRepo.CreateAsync(textTableRow);
                    createdCount++;
                }

                // Process cells within this row if row was successfully created/retrieved
                if (textTableRow.TextTableRowID.HasValue)
                {
                    // Delegate cell parsing and accumulate created cell count
                    createdCount += await parseAndCreateCellsAsync(rowEl, textTableRow.TextTableRowID.Value, context);
                }

                // Increment sequence for next row in group
                seqNum++;
            }
            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all cells ([td], [th]) within a given table row ([tr]), creating
        /// TextTableCell records. It skips empty cells and maintains correct sequencing.
        /// </summary>
        /// <param name="rowEl">The XElement for the table row ([tr]).</param>
        /// <param name="textTableRowId">The database ID of the parent TextTableRow.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A task that resolves to the number of TextTableCell entities created.</returns>
        /// <seealso cref="TextTableCell"/>
        /// <seealso cref="TextTableRow"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndCreateCellsAsync(
            XElement rowEl,
            int textTableRowId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate required context dependencies for database operations
            if (context == null || context.ServiceProvider == null)
                return 0;

            // Get database context and repository for table cell operations
            var dbContext = context.GetDbContext();
            var cellRepo = context.GetRepository<TextTableCell>();
            var cellDbSet = dbContext.Set<TextTableCell>();

            // Process both <th> and <td> elements in document order.
            // Extract both header and data cells while preserving their sequence
            var cellElements = rowEl.Elements()
                .Where(e => e.Name.LocalName == sc.E.Th || e.Name.LocalName == sc.E.Td)
                .ToList();

            // Initialize sequence number for maintaining cell order within row
            int seqNum = 1;

            // Process each cell element and create database entities
            foreach (var cellEl in cellElements)
            {

                // Check for existing cell to avoid duplicates based on row and sequence
                var existingCell = await cellDbSet.FirstOrDefaultAsync(c =>
                    c.TextTableRowID == textTableRowId &&
                    c.SequenceNumber == seqNum);

                // Create new cell if none exists at this position
                if (existingCell == null)
                {
                    // Safely parse integer attributes
                    // Extract rowspan and colspan attributes with safe parsing
                    _ = int.TryParse(cellEl.Attribute(sc.A.Rowspan)?.Value, out int rs);
                    _ = int.TryParse(cellEl.Attribute(sc.A.Colspan)?.Value, out int cs);

                    // Build new table cell entity with all extracted attributes
                    var newCell = new TextTableCell
                    {
                        TextTableRowID = textTableRowId,
                        CellType = cellEl.Name.LocalName, // "th" or "td"
                        SequenceNumber = seqNum,
                        CellText = cellEl.GetSplHtml(stripNamespaces: true),
                        RowSpan = rs > 0 ? rs : null, // Only store valid span values
                        ColSpan = cs > 0 ? cs : null, // Only store valid span values
                        StyleCode = cellEl.Attribute(sc.A.StyleCode)?.Value,
                        Align = cellEl.Attribute(sc.A.Align)?.Value,
                        VAlign = cellEl.Attribute(sc.A.VAlign)?.Value
                    };

                    // Persist new cell to database
                    await cellRepo.CreateAsync(newCell);
                    createdCount++;
                }

                // Increment sequence for next cell regardless of creation status
                seqNum++;
            }
            return createdCount;
            #endregion
        }

        #endregion

        #region Table Processing Methods - Bulk Operations

        /**************************************************************/
        /// <summary>
        /// Parses a [table] element using bulk operations pattern. Collects all table data into memory,
        /// deduplicates against existing entities, then performs batch inserts for optimal performance.
        /// </summary>
        /// <param name="tableEl">The XElement representing the [table] element.</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent record (where ContentType='Table'). Each table has its own SectionTextContent record, so this ID uniquely identifies this specific table.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A task that resolves to the total number of table, column, row, and cell entities created.</returns>
        /// <remarks>
        /// This method implements the bulk operations pattern to minimize database round-trips:
        /// 1. Parse entire table structure into memory (DTOs)
        /// 2. Query for existing entities in bulk (one query per entity type)
        /// 3. Build HashSet lookups for O(1) duplicate detection
        /// 4. Create missing entities in batch operations
        /// 
        /// Performance Impact:
        /// - Before: N database calls (one per entity)
        /// - After: 4 queries + 4 bulk inserts (one per entity type)
        /// 
        /// This method expects a one-to-one relationship between SectionTextContent (ContentType='Table') and TextTable.
        /// Each table element in the SPL creates a separate SectionTextContent record, which then gets one TextTable record.
        /// </remarks>
        /// <seealso cref="TextTable"/>
        /// <seealso cref="TextTableColumn"/>
        /// <seealso cref="TextTableRow"/>
        /// <seealso cref="TextTableCell"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> getOrCreateTextTableAndChildrenAsync_bulkCalls(
            XElement tableEl,
            int sectionTextContentId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate all required input parameters and context dependencies
            if (tableEl == null || sectionTextContentId <= 0 || context?.ServiceProvider == null)
            {
                return 0;
            }

            // Get database context for bulk operations
            var dbContext = context.GetDbContext();

            #region parse table structure into memory

            // Parse table metadata
            var captionEl = tableEl.SplElement(sc.E.Caption);
            string? captionText = captionEl?.GetSplHtml(stripNamespaces: true);

            var tableData = new
            {
                SectionTextContentID = sectionTextContentId,
                SectionTableLink = tableEl.Attribute(sc.A.ID)?.Value,
                Width = tableEl.Attribute(sc.A.Width)?.Value,
                Caption = captionText,
                HasHeader = tableEl.SplElement(sc.E.Thead) != null,
                HasFooter = tableEl.SplElement(sc.E.Tfoot) != null
            };

            // Parse all column definitions into memory
            var columnDtos = parseColumnsToMemory(tableEl);

            // Parse all rows and cells into memory
            var rowDtos = new List<TableRowDto>();

            var theadEl = tableEl.SplElement(sc.E.Thead);
            if (theadEl != null)
            {
                rowDtos.AddRange(parseRowsToMemory(theadEl, "Header"));
            }

            var tbodyEl = tableEl.SplElement(sc.E.Tbody);
            if (tbodyEl != null)
            {
                rowDtos.AddRange(parseRowsToMemory(tbodyEl, "Body"));
            }

            var tfootEl = tableEl.SplElement(sc.E.Tfoot);
            if (tfootEl != null)
            {
                rowDtos.AddRange(parseRowsToMemory(tfootEl, "Footer"));
            }

            #endregion

            #region check existing entities and create missing

            // Check for existing TextTable
            var textTableDbSet = dbContext.Set<TextTable>();
            var existingTable = await textTableDbSet
                .FirstOrDefaultAsync(t => t.SectionTextContentID == sectionTextContentId);

            TextTable textTable;
            if (existingTable == null)
            {
                // Create new table entity
                textTable = new TextTable
                {
                    SectionTextContentID = tableData.SectionTextContentID,
                    SectionTableLink = tableData.SectionTableLink,
                    Width = tableData.Width,
                    Caption = tableData.Caption,
                    HasHeader = tableData.HasHeader,
                    HasFooter = tableData.HasFooter
                };

                textTableDbSet.Add(textTable);
                await dbContext.SaveChangesAsync(); // Need ID for foreign keys
                createdCount++;
            }
            else
            {
                textTable = existingTable;
            }

            // Validate table creation was successful before proceeding
            if (textTable.TextTableID == null)
            {
                context.Logger?.LogError("Failed to create or retrieve TextTable for SectionTextContentID {id}", sectionTextContentId);
                return createdCount;
            }

            int tableId = textTable.TextTableID.Value;

            // Bulk create columns
            createdCount += await bulkCreateColumnsAsync(dbContext, tableId, columnDtos);

            // Bulk create rows and cells
            createdCount += await bulkCreateRowsAndCellsAsync(dbContext, tableId, rowDtos);

            #endregion

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all column definitions from a table element into memory without database operations.
        /// Handles both standalone [col] elements and [col] elements nested within [colgroup].
        /// </summary>
        /// <param name="tableEl">The XElement representing the [table] element to parse.</param>
        /// <returns>
        /// A list of TableColumnDto objects representing all columns in document order.
        /// </returns>
        /// <remarks>
        /// Per Section 2.2.2.5 of SPL Implementation Guide:
        /// - [colgroup] elements are optional and uncommon but must be supported
        /// - [col] elements can exist standalone or within [colgroup]
        /// - [colgroup] attributes provide defaults that individual [col] can override
        /// - Column width, alignment, and styleCode attributes must be preserved
        /// 
        /// Parsing Logic:
        /// 1. First processes all [colgroup] elements and their child [col] elements
        /// 2. Then processes standalone [col] elements not within any [colgroup]
        /// 3. Maintains sequence numbering across both types for proper rendering order
        /// </remarks>
        /// <seealso cref="TextTableColumn"/>
        /// <seealso cref="TableColumnDto"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private List<TableColumnDto> parseColumnsToMemory(XElement tableEl)
        {
            #region implementation

            var columnDtos = new List<TableColumnDto>();
            int overallSeqNum = 1;
            int colgroupSeqNum = 1;

            #region process colgroup elements

            var colgroupElements = tableEl.Elements(ns + sc.E.Colgroup).ToList();

            foreach (var colgroupEl in colgroupElements)
            {
                // Extract colgroup-level attributes that serve as defaults for child columns
                var colgroupStyleCode = colgroupEl.Attribute(sc.A.StyleCode)?.Value;
                var colgroupAlign = colgroupEl.Attribute(sc.A.Align)?.Value;
                var colgroupVAlign = colgroupEl.Attribute(sc.A.VAlign)?.Value;

                var colElementsInGroup = colgroupEl.Elements(ns + sc.E.Col).ToList();

                foreach (var colEl in colElementsInGroup)
                {
                    columnDtos.Add(new TableColumnDto
                    {
                        SequenceNumber = overallSeqNum,
                        ColGroupSequenceNumber = colgroupSeqNum,
                        ColGroupStyleCode = colgroupStyleCode,
                        ColGroupAlign = colgroupAlign,
                        ColGroupVAlign = colgroupVAlign,
                        Width = colEl.Attribute(sc.A.Width)?.Value,
                        Align = colEl.Attribute(sc.A.Align)?.Value,
                        VAlign = colEl.Attribute(sc.A.VAlign)?.Value,
                        StyleCode = colEl.Attribute(sc.A.StyleCode)?.Value
                    });

                    overallSeqNum++;
                }

                colgroupSeqNum++;
            }

            #endregion

            #region process standalone col elements

            var standaloneColElements = tableEl.Elements(ns + sc.E.Col).ToList();

            foreach (var colEl in standaloneColElements)
            {
                columnDtos.Add(new TableColumnDto
                {
                    SequenceNumber = overallSeqNum,
                    ColGroupSequenceNumber = null,
                    ColGroupStyleCode = null,
                    ColGroupAlign = null,
                    ColGroupVAlign = null,
                    Width = colEl.Attribute(sc.A.Width)?.Value,
                    Align = colEl.Attribute(sc.A.Align)?.Value,
                    VAlign = colEl.Attribute(sc.A.VAlign)?.Value,
                    StyleCode = colEl.Attribute(sc.A.StyleCode)?.Value
                });

                overallSeqNum++;
            }

            #endregion

            return columnDtos;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all rows and their cells from a table group element into memory without database operations.
        /// Processes [thead], [tbody], or [tfoot] elements, extracting all [tr] rows and their [td]/[th] cells.
        /// </summary>
        /// <param name="rowGroupEl">The XElement for the group (e.g., [tbody]).</param>
        /// <param name="rowGroupType">The type of group: 'Header', 'Body', or 'Footer'.</param>
        /// <returns>
        /// A list of TableRowDto objects containing row metadata and their associated cell DTOs.
        /// </returns>
        /// <remarks>
        /// This method performs pure XML parsing without any database operations, enabling
        /// bulk processing later. All row and cell attributes are preserved for database persistence.
        /// </remarks>
        /// <seealso cref="TableRowDto"/>
        /// <seealso cref="TableCellDto"/>
        /// <seealso cref="TextTableRow"/>
        /// <seealso cref="TextTableCell"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private List<TableRowDto> parseRowsToMemory(XElement rowGroupEl, string rowGroupType)
        {
            #region implementation

            var rowDtos = new List<TableRowDto>();
            var rowElements = rowGroupEl.SplElements(sc.E.Tr).ToList();
            int rowSeqNum = 1;

            foreach (var rowEl in rowElements)
            {
                // Parse all cells for this row
                var cellDtos = new List<TableCellDto>();
                var cellElements = rowEl.Elements()
                    .Where(e => e.Name.LocalName == sc.E.Th || e.Name.LocalName == sc.E.Td)
                    .ToList();

                int cellSeqNum = 1;
                foreach (var cellEl in cellElements)
                {
                    // Extract rowspan and colspan attributes with safe parsing
                    _ = int.TryParse(cellEl.Attribute(sc.A.Rowspan)?.Value, out int rs);
                    _ = int.TryParse(cellEl.Attribute(sc.A.Colspan)?.Value, out int cs);

                    cellDtos.Add(new TableCellDto
                    {
                        CellType = cellEl.Name.LocalName,
                        SequenceNumber = cellSeqNum,
                        CellText = cellEl.GetSplHtml(stripNamespaces: true),
                        RowSpan = rs > 0 ? rs : null,
                        ColSpan = cs > 0 ? cs : null,
                        StyleCode = cellEl.Attribute(sc.A.StyleCode)?.Value,
                        Align = cellEl.Attribute(sc.A.Align)?.Value,
                        VAlign = cellEl.Attribute(sc.A.VAlign)?.Value
                    });

                    cellSeqNum++;
                }

                rowDtos.Add(new TableRowDto
                {
                    RowGroupType = rowGroupType,
                    SequenceNumber = rowSeqNum,
                    StyleCode = rowEl.Attribute(sc.A.StyleCode)?.Value,
                    Cells = cellDtos
                });

                rowSeqNum++;
            }

            return rowDtos;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs bulk creation of TextTableColumn entities, checking for existing columns
        /// and creating only missing ones in a single batch operation.
        /// </summary>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="textTableId">The foreign key ID of the parent TextTable.</param>
        /// <param name="columnDtos">The list of column DTOs parsed from XML.</param>
        /// <returns>
        /// The count of newly created TextTableColumn entities.
        /// </returns>
        /// <remarks>
        /// Performance Pattern:
        /// 1. Single query to fetch all existing columns for this table
        /// 2. Build HashSet of existing (TableId, SequenceNumber) pairs for O(1) lookup
        /// 3. Filter DTOs to only those not already in database
        /// 4. Single bulk insert for all new columns
        /// 
        /// This reduces N database calls to 2 calls regardless of column count.
        /// </remarks>
        /// <seealso cref="TextTableColumn"/>
        /// <seealso cref="TableColumnDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> bulkCreateColumnsAsync(
            ApplicationDbContext dbContext,
            int textTableId,
            List<TableColumnDto> columnDtos)
        {
            #region implementation

            if (!columnDtos.Any())
                return 0;

            // Query all existing columns for this table in one call
            var columnDbSet = dbContext.Set<TextTableColumn>();
            var existingColumns = await columnDbSet
                .Where(c => c.TextTableID == textTableId)
                .Select(c => new { c.TextTableID, c.SequenceNumber })
                .ToListAsync();

            // Build HashSet for O(1) existence checking
            // Filter nulls if TextTableID or SequenceNumber are nullable in your schema
            var existingKeys = new HashSet<(int TableId, int SeqNum)>(
                existingColumns
                    .Where(c => c.TextTableID.HasValue && c.SequenceNumber.HasValue)
                    .Select(c => (c.TextTableID!.Value, c.SequenceNumber!.Value))
            );

            // Filter to only columns that don't exist yet
            var newColumns = columnDtos
                .Where(dto => !existingKeys.Contains((textTableId, dto.SequenceNumber)))
                .Select(dto => new TextTableColumn
                {
                    TextTableID = textTableId,
                    SequenceNumber = dto.SequenceNumber,
                    ColGroupSequenceNumber = dto.ColGroupSequenceNumber,
                    ColGroupStyleCode = dto.ColGroupStyleCode,
                    ColGroupAlign = dto.ColGroupAlign,
                    ColGroupVAlign = dto.ColGroupVAlign,
                    Width = dto.Width,
                    Align = dto.Align,
                    VAlign = dto.VAlign,
                    StyleCode = dto.StyleCode
                })
                .ToList();

            if (newColumns.Any())
            {
                // Bulk insert all new columns in one operation
                columnDbSet.AddRange(newColumns);
                await dbContext.SaveChangesAsync();
            }

            return newColumns.Count;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs bulk creation of TextTableRow and TextTableCell entities, checking for existing
        /// entities and creating only missing ones in batch operations.
        /// </summary>
        /// <param name="dbContext">The database context for querying and persisting entities.</param>
        /// <param name="textTableId">The foreign key ID of the parent TextTable.</param>
        /// <param name="rowDtos">The list of row DTOs (including cell DTOs) parsed from XML.</param>
        /// <returns>
        /// The count of newly created TextTableRow and TextTableCell entities.
        /// </returns>
        /// <remarks>
        /// Performance Pattern:
        /// 1. Single query to fetch all existing rows for this table
        /// 2. Build HashSet of existing (TableId, GroupType, SeqNum) tuples for O(1) lookup
        /// 3. Filter DTOs to only rows not already in database
        /// 4. Single bulk insert for all new rows
        /// 5. Single query to fetch all existing cells for this table
        /// 6. Build HashSet of existing (RowId, SeqNum) pairs
        /// 7. Single bulk insert for all new cells
        /// 
        /// This reduces potentially thousands of database calls to 4 calls total.
        /// </remarks>
        /// <seealso cref="TextTableRow"/>
        /// <seealso cref="TextTableCell"/>
        /// <seealso cref="TableRowDto"/>
        /// <seealso cref="TableCellDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> bulkCreateRowsAndCellsAsync(
            ApplicationDbContext dbContext,
            int textTableId,
            List<TableRowDto> rowDtos)
        {
            #region implementation

            int createdCount = 0;

            if (!rowDtos.Any())
                return 0;

            #region bulk create rows

            var rowDbSet = dbContext.Set<TextTableRow>();

            // Query all existing rows for this table in one call
            var existingRows = await rowDbSet
                .Where(r => r.TextTableID == textTableId)
                .Select(r => new { r.TextTableID, r.RowGroupType, r.SequenceNumber, r.TextTableRowID })
                .ToListAsync();

            // Build HashSet for O(1) existence checking
            // Filter nulls if properties are nullable in your schema
            var existingRowKeys = new HashSet<(int TableId, string GroupType, int SeqNum)>(
                existingRows
                    .Where(r => r.TextTableID.HasValue && r.RowGroupType != null && r.SequenceNumber.HasValue)
                    .Select(r => (r.TextTableID!.Value, r.RowGroupType!, r.SequenceNumber!.Value))
            );

            // Filter to only rows that don't exist yet
            var newRows = rowDtos
                .Where(dto => !existingRowKeys.Contains((textTableId, dto.RowGroupType, dto.SequenceNumber)))
                .Select(dto => new TextTableRow
                {
                    TextTableID = textTableId,
                    RowGroupType = dto.RowGroupType,
                    SequenceNumber = dto.SequenceNumber,
                    StyleCode = dto.StyleCode
                })
                .ToList();

            if (newRows.Any())
            {
                // Bulk insert all new rows in one operation
                rowDbSet.AddRange(newRows);
                await dbContext.SaveChangesAsync(); // Need IDs for cells
                createdCount += newRows.Count;
            }

            #endregion

            #region build row lookup dictionary

            // Re-query all rows to get complete set with IDs (including newly created)
            var allRows = await rowDbSet
                .Where(r => r.TextTableID == textTableId)
                .Select(r => new { r.TextTableID, r.RowGroupType, r.SequenceNumber, r.TextTableRowID })
                .ToListAsync();

            // Build lookup dictionary for linking cells to rows
            // Filter to ensure all key components are non-null
            var rowLookup = allRows
                .Where(r => r.TextTableID.HasValue
                    && r.RowGroupType != null
                    && r.SequenceNumber.HasValue
                    && r.TextTableRowID.HasValue)
                .ToDictionary(
                    r => (r.TextTableID!.Value, r.RowGroupType!, r.SequenceNumber!.Value),
                    r => r.TextTableRowID!.Value
                );

            #endregion

            #region bulk create cells

            var cellDbSet = dbContext.Set<TextTableCell>();

            // Query all existing cells for this table's rows in one call
            var rowIds = rowLookup.Values.ToList();
            var existingCells = await cellDbSet
                .Where(c => c.TextTableRowID.HasValue && rowIds.Contains(c.TextTableRowID.Value))
                .Select(c => new { c.TextTableRowID, c.SequenceNumber })
                .ToListAsync();

            // Build HashSet for O(1) existence checking
            // Filter nulls if properties are nullable in your schema
            var existingCellKeys = new HashSet<(int RowId, int SeqNum)>(
                existingCells
                    .Where(c => c.TextTableRowID.HasValue && c.SequenceNumber.HasValue)
                    .Select(c => (c.TextTableRowID!.Value, c.SequenceNumber!.Value))
            );

            // Flatten all cells from all rows and link to their row IDs
            var newCells = new List<TextTableCell>();
            foreach (var rowDto in rowDtos)
            {
                var rowKey = (textTableId, rowDto.RowGroupType, rowDto.SequenceNumber);
                if (!rowLookup.TryGetValue(rowKey, out int rowId))
                    continue; // Skip if row doesn't exist (shouldn't happen)

                foreach (var cellDto in rowDto.Cells)
                {
                    // Only create cell if it doesn't already exist
                    if (!existingCellKeys.Contains((rowId, cellDto.SequenceNumber)))
                    {
                        newCells.Add(new TextTableCell
                        {
                            TextTableRowID = rowId,
                            CellType = cellDto.CellType,
                            SequenceNumber = cellDto.SequenceNumber,
                            CellText = cellDto.CellText,
                            RowSpan = cellDto.RowSpan,
                            ColSpan = cellDto.ColSpan,
                            StyleCode = cellDto.StyleCode,
                            Align = cellDto.Align,
                            VAlign = cellDto.VAlign
                        });
                    }
                }
            }

            if (newCells.Any())
            {
                // Bulk insert all new cells in one operation
                cellDbSet.AddRange(newCells);
                await dbContext.SaveChangesAsync();
                createdCount += newCells.Count;
            }

            #endregion

            return createdCount;

            #endregion
        }

        #endregion

        #region Highlight Processing Methods

        /**************************************************************/
        /// <summary>
        /// Finds or creates SectionExcerptHighlight records for all highlight text nodes
        /// within excerpt elements of a section, capturing the complete inner XML content
        /// including tables, lists, and paragraphs for database storage.
        /// </summary>
        /// <param name="excerptEl">The XElement to search for excerpt/highlight/text patterns.</param>
        /// <param name="sectionId">The SectionID owning this highlight content.</param>
        /// <param name="context">Parsing context for repository and database access.</param>
        /// <returns>List of SectionExcerptHighlight objects (created or found).</returns>
        /// <seealso cref="SectionExcerptHighlight"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        /// <remarks>
        /// This method must capture the complete inner XML of the text element,
        /// including complex nested structures like tables. The content is stored as raw XML
        /// in the database for later rendering.
        /// </remarks>
        public async Task<List<SectionExcerptHighlight>> getOrCreateSectionExcerptHighlightsAsync(
            XElement excerptEl,
            int sectionId,
            SplParseContext context)
        {
            #region implementation
            var highlights = new List<SectionExcerptHighlight>();

            // Validate required input parameters
            if (excerptEl == null || sectionId <= 0)
                return highlights;

            // Validate required context dependencies
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return highlights;

            // Get database context and repository for section excerpt highlight operations
            var dbContext = context.GetDbContext();
            var repo = context.GetRepository<SectionExcerptHighlight>();
            var dbSet = dbContext.Set<SectionExcerptHighlight>();

            // Find all highlight elements directly under this excerpt
            // Use Elements (not Descendants) to avoid finding nested highlights in child structures
            var highlightElements = excerptEl.Elements(ns + sc.E.Highlight);

            foreach (var highlightEl in highlightElements)
            {
                // Get the text element within this specific highlight
                var textEl = highlightEl.Element(ns + sc.E.Text);

                if (textEl == null)
                {
                    // No text element - this shouldn't happen in valid SPL but handle gracefully
                    context.Logger?.LogWarning($"Highlight element without text child in SectionID {sectionId}");
                    continue;
                }

                // Extract the complete inner XML from the text element
                // This preserves tables, lists, paragraphs, and all nested structures
                string? txt = null;

                try
                {
                    // Get all child nodes of the text element and serialize them
                    var innerNodes = textEl.Nodes();

                    if (innerNodes != null && innerNodes.Any())
                    {
                        // Concatenate all inner XML preserving structure
                        txt = string
                            .Concat(innerNodes.Select(n => n.ToString()))
                            ?.NormalizeXmlWhitespace();
                    }
                    else
                    {
                        // Text element exists but has no children - check for direct text content
                        txt = textEl.Value?.Trim();
                    }
                }
                catch (Exception ex)
                {
                    context.Logger?.LogError(ex, $"Error extracting highlight XML for SectionID {sectionId}");
                    continue;
                }

                // Skip if no actual content was extracted
                if (string.IsNullOrWhiteSpace(txt))
                {
                    context.Logger?.LogWarning($"Empty highlight text extracted for SectionID {sectionId}");
                    continue;
                }

                // Dedupe: SectionID + HighlightText
                var existing = await dbSet
                    .Where(eh => eh.SectionID == sectionId && eh.HighlightText == txt)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    highlights.Add(existing);
                    continue;
                }

                // Create new section excerpt highlight with the complete XML content
                var newHighlight = new SectionExcerptHighlight
                {
                    SectionID = sectionId,
                    HighlightText = txt
                };

                await repo.CreateAsync(newHighlight);
                highlights.Add(newHighlight);

                context.Logger?.LogInformation($"Created SectionExcerptHighlight for SectionID {sectionId} with {txt.Length} characters");
            }

            return highlights;
            #endregion
        }

        #endregion

        #region Static Helper Method
        /**************************************************************/
        /// <summary>
        /// Parses and saves a Section entity from an XElement, extracting metadata and establishing
        /// relationships with the structured body context. Performs deduplication based on section GUID.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the section to parse.</param>
        /// <param name="context">Parsing context containing structured body and repository access.</param>
        /// <returns>The Section entity (created or existing) with populated metadata.</returns>
        /// <exception cref="ArgumentNullException">Thrown when sectionEl or context is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when structured body context is invalid or section GUID is missing.</exception>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<Section> parseAndSaveSectionAsync(XElement sectionEl, SplParseContext context)
        {
            #region implementation
            // Validate required input parameters
            if (sectionEl == null)
                throw new ArgumentNullException(nameof(sectionEl));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.StructuredBody?.StructuredBodyID == null)
                throw new InvalidOperationException("No valid structured body context.");

            int? documentId = context.StructuredBody.DocumentID;

            // Get section GUID
            // Extract unique identifier for section from XML element
            var sectionGuidStr = sectionEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root);
            if (!Guid.TryParse(sectionGuidStr, out var sectionGuid) || sectionGuid == Guid.Empty)
                throw new InvalidOperationException("Section <id root> is missing or not a valid GUID.");

            // Get repo/db
            // Get database context and repository for section operations
            var dbContext = context!.ServiceProvider!.GetRequiredService<ApplicationDbContext>();
            var sectionRepo = context.GetRepository<Section>();
            var sectionDbSet = dbContext.Set<Section>();

            // Deduplicate by GUID and StructuredBodyID (or just by GUID if global)
            // Search for existing section with matching GUID and structured body context
            var existing = await sectionDbSet
                .FirstOrDefaultAsync(s => s.SectionGUID == sectionGuid && s.StructuredBodyID == context.StructuredBody.StructuredBodyID.Value);

            // Return existing section if found to avoid duplicates
            if (existing != null)
                return existing;

            // Extract additional metadata
            // Parse section code information and metadata from XML attributes
            var sectionCode = sectionEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
            var sectionLinkGuid = sectionEl.GetAttrVal(sc.A.ID);
            var sectionCodeSystem = sectionEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem);
            var sectionCodeSystemName = sectionEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystemName);
            var sectionDisplayName = sectionEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName) ?? string.Empty;
            var sectionTitle = sectionEl.GetSplElementVal(sc.E.Title)?.Trim();

            // Parse effective time with fallback to minimum value if not present
            var sectionEffectiveTime = Util.ParseNullableDateTime(sectionEl.GetSplElementAttrVal(sc.E.EffectiveTime, sc.A.Value) ?? string.Empty) ?? DateTime.MinValue;

            // Create new Section entity
            // Build new section entity with all extracted metadata and context relationships
            var newSection = new Section
            {
                DocumentID = documentId,
                StructuredBodyID = context.StructuredBody.StructuredBodyID.Value,
                SectionGUID = sectionGuid,
                SectionCode = sectionCode,
                SectionCodeSystem = sectionCodeSystem,
                SectionCodeSystemName = sectionCodeSystemName,
                SectionDisplayName = sectionDisplayName,
                Title = sectionTitle,
                EffectiveTime = sectionEffectiveTime
            };

            // Persist new section to database and return
            await sectionRepo.CreateAsync(newSection);
            return newSection;
            #endregion
        }
        #endregion
    }
}