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
    /// Base class for section content parsing providing shared functionality
    /// used by both bulk and single-call parsing strategies. Contains only
    /// methods that are genuinely common to both implementation approaches.
    /// </summary>
    /// <remarks>
    /// This abstract base class encapsulates the minimal set of shared methods
    /// between bulk operations and single-call operations patterns. Strategy-specific
    /// methods, DTOs, and helper classes remain in the derived SectionContentParser class.
    /// </remarks>
    /// <seealso cref="SectionContentParser"/>
    /// <seealso cref="SectionTextContent"/>
    /// <seealso cref="TextList"/>
    /// <seealso cref="SplParseContext"/>
    public abstract class SectionContentParserBase
    {
        #region Protected Fields

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        protected static readonly XNamespace ns = c.XML_NAMESPACE;

        /// <summary>
        /// Media parser for handling multimedia content within text blocks.
        /// </summary>
        /// <seealso cref="SectionMediaParser"/>
        protected readonly SectionMediaParser _mediaParser;

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SectionContentParserBase with required dependencies.
        /// </summary>
        /// <param name="mediaParser">Parser for handling multimedia content within text blocks.</param>
        /// <seealso cref="SectionMediaParser"/>
        protected SectionContentParserBase(SectionMediaParser? mediaParser = null)
        {
            _mediaParser = mediaParser ?? new SectionMediaParser();
        }

        #endregion

        #region Shared Validation Methods

        /**************************************************************/
        /// <summary>
        /// Validates the input parameters for text list processing.
        /// Used by both single-call and bulk-call list processing methods.
        /// </summary>
        /// <param name="listEl">The XElement representing the list.</param>
        /// <param name="sectionTextContentId">The section text content ID.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>True if all inputs are valid, false otherwise.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        protected static bool validateTextListInputs(XElement listEl, int sectionTextContentId, SplParseContext context)
        {
            #region implementation
            // Check for null or invalid parameters
            return listEl != null &&
                   sectionTextContentId > 0 &&
                   context?.ServiceProvider != null;
            #endregion
        }

        #endregion

        #region Shared Entity Creation Methods

        /**************************************************************/
        /// <summary>
        /// Creates a new TextList entity from the provided list element and section content ID.
        /// Used by both single-call (via getOrCreateTextListAsync) and bulk-call implementations.
        /// </summary>
        /// <param name="listEl">The XElement representing the list.</param>
        /// <param name="sectionTextContentId">The section text content ID.</param>
        /// <returns>A new TextList entity with populated attributes.</returns>
        /// <seealso cref="TextList"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        protected static TextList createTextListEntity(XElement listEl, int sectionTextContentId)
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

        #endregion

        #region Shared Highlight Processing Methods

        /**************************************************************/
        /// <summary>
        /// Finds or creates SectionExcerptHighlight records for all highlight text nodes
        /// within excerpt elements of a section, capturing the complete inner XML content
        /// including tables, lists, and paragraphs for database storage.
        /// Used by both single-call and bulk-call implementations via processSpecializedContentAsync.
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
        protected async Task<List<SectionExcerptHighlight>> getOrCreateSectionExcerptHighlightsAsync(
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

#if DEBUG
                if (context.UseBatchSaving)
                {
                    Debug.WriteLine($"SectionContentParserBase.getOrCreateSectionExcerptHighlightsAsync. BatchSaving {context.UseBatchSaving}. " +
                        $"This shouldn't be happening. This will fire a premature database save");
                }
#endif

                await repo.CreateAsync(newHighlight);
                highlights.Add(newHighlight);

                context.Logger?.LogInformation($"Created SectionExcerptHighlight for SectionID {sectionId} with {txt.Length} characters");
            }

            return highlights;
            #endregion
        }

        #endregion

        #region Shared Static Helper Methods

        /**************************************************************/
        /// <summary>
        /// Parses and saves a Section entity from an XElement, extracting metadata and establishing
        /// relationships with the structured body context. Performs deduplication based on section GUID.
        /// Used by both single-call and bulk-call implementations as a delegate parameter.
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
        protected static async Task<Section> parseAndSaveSectionAsync(XElement sectionEl, SplParseContext context)
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

        /**************************************************************/
        /**************************************************************/
        /**************************************************************/
        // SHARED METHODS FOR BULK AND STAGED BULK OPERATIONS
        /**************************************************************/
        /**************************************************************/
        /**************************************************************/

        #region Shared Data Transfer Objects

        /**************************************************************/
        /// <summary>
        /// Data Transfer Object representing a table column parsed from XML.
        /// Used for in-memory collection before bulk database operations.
        /// Shared between bulk calls and staged bulk calls implementations.
        /// </summary>
        /// <seealso cref="TextTableColumn"/>
        /// <seealso cref="Label"/>
        protected class TableColumnDto
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
        /// Shared between bulk calls and staged bulk calls implementations.
        /// </summary>
        /// <seealso cref="TextTableRow"/>
        /// <seealso cref="TableCellDto"/>
        /// <seealso cref="Label"/>
        protected class TableRowDto
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
        /// Shared between bulk calls and staged bulk calls implementations.
        /// </summary>
        /// <seealso cref="TextTableCell"/>
        /// <seealso cref="Label"/>
        protected class TableCellDto
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
        /// Shared between bulk calls and staged bulk calls implementations.
        /// </summary>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="Label"/>
        protected class SectionTextContentDto
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
        /// Shared between bulk calls and staged bulk calls implementations.
        /// </summary>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="Label"/>
        protected class TextListItemDto
        {
            public int SequenceNumber { get; set; }
            public string? ItemCaption { get; set; }
            public string? ItemText { get; set; }
        }

        #endregion

        #region Shared Parsing Methods (XML to DTOs)

        /**************************************************************/
        /// <summary>
        /// Parses all content blocks from XML into DTOs without any database calls.
        /// Recursively processes nested hierarchy and assigns temporary IDs for relationship tracking.
        /// Shared between bulk calls and staged bulk calls implementations.
        /// </summary>
        /// <param name="parentEl">The XElement to start parsing (typically the [text] element).</param>
        /// <param name="sectionId">The SectionID owning this content.</param>
        /// <param name="parentTempId">Parent's temporary identifier for hierarchy, or null for top-level blocks.</param>
        /// <param name="sequence">The sequence number to start from (default 1).</param>
        /// <returns>A flattened list of all DTOs including nested content.</returns>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        protected List<SectionTextContentDto> parseContentBlocksToMemory(
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

        /**************************************************************/
        /// <summary>
        /// Parses all list items from a list element into memory without database operations.
        /// Shared between bulk calls and staged bulk calls implementations.
        /// </summary>
        /// <param name="listEl">The XElement representing the [list] element to parse.</param>
        /// <returns>A list of TextListItemDto objects representing all items with content.</returns>
        /// <seealso cref="TextListItemDto"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        protected List<TextListItemDto> parseTextListItemsToMemory(XElement listEl)
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
        /// Parses all column definitions from a table element into memory without database operations.
        /// Handles both standalone [col] elements and [col] elements nested within [colgroup].
        /// Shared between bulk calls and staged bulk calls implementations.
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
        protected List<TableColumnDto> parseColumnsToMemory(XElement tableEl)
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
        /// Shared between bulk calls and staged bulk calls implementations.
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
        protected List<TableRowDto> parseRowsToMemory(XElement rowGroupEl, string rowGroupType)
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

        #endregion

        #region Shared Helper Methods

        /**************************************************************/
        /// <summary>
        /// Filters DTOs to return only top-level entities (those without a parent).
        /// Shared between bulk calls and staged bulk calls implementations.
        /// </summary>
        /// <param name="dtos">The complete list of DTOs.</param>
        /// <returns>List of DTOs where ParentTempId is null.</returns>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        protected List<SectionTextContentDto> filterTopLevelDtos(List<SectionTextContentDto> dtos)
        {
            #region implementation
            return dtos.Where(dto => dto.ParentTempId == null).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Filters DTOs to return only child entities (those with a parent reference).
        /// Shared between bulk calls and staged bulk calls implementations.
        /// </summary>
        /// <param name="dtos">The complete list of DTOs.</param>
        /// <returns>List of DTOs where ParentTempId is not null.</returns>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        protected List<SectionTextContentDto> filterChildDtos(List<SectionTextContentDto> dtos)
        {
            #region implementation
            return dtos.Where(dto => dto.ParentTempId != null).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a deduplication key tuple from a DTO for uniqueness checking.
        /// Shared between bulk calls and staged bulk calls implementations.
        /// </summary>
        /// <param name="dto">The DTO to build key from.</param>
        /// <returns>Tuple containing key fields for deduplication.</returns>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        protected (int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)
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
        /// Shared between bulk calls and staged bulk calls implementations.
        /// </summary>
        /// <param name="dto">The DTO to convert.</param>
        /// <param name="parentId">The resolved parent ID or null for top-level entities.</param>
        /// <returns>New SectionTextContent entity instance.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        protected SectionTextContent createEntityFromDto(SectionTextContentDto dto, int? parentId)
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

        #endregion
    }
}