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
        /// <returns>A SplParseResult indicating the success status and content elements created.</returns>
        /// <seealso cref="ParseSectionContentAsync"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress = null)
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
        public async Task<Tuple<List<SectionTextContent>, int>> GetOrCreateSectionTextContentsAsync(
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
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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

        #region List Processing Methods

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
        private async Task<int> getOrCreateTextListAndItemsAsync(
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
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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

        #region Table Processing Methods

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
        private async Task<int> getOrCreateTextTableAndChildrenAsync(
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
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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