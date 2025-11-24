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
    /// Bulk operations implementation for section content parsing.
    /// Contains methods that collect all entities into memory first, then perform
    /// batch queries and bulk inserts for optimal database performance.
    /// </summary>
    /// <remarks>
    /// This class implements the bulk operations pattern where:
    /// - Phase 1: Parse all content to in-memory DTOs (0 database calls)
    /// - Phase 2: Bulk query existing entities (1 query per entity type)
    /// - Phase 3: Bulk insert missing entities (1 insert per entity type)
    /// 
    /// Performance Impact:
    /// - Before (N+1): N database calls (one per entity)
    /// - After (Bulk): 2-4 calls total (one query + one insert per entity type)
    /// 
    /// Best suited for:
    /// - Large documents with many content elements
    /// - Batch processing scenarios
    /// - Production environments where performance is critical
    /// 
    /// For simpler scenarios or debugging, use <see cref="SectionContentParserSingleCalls"/> instead.
    /// </remarks>
    /// <seealso cref="SectionContentParserBase"/>
    /// <seealso cref="SectionContentParserSingleCalls"/>
    /// <seealso cref="SectionTextContent"/>
    /// <seealso cref="TextList"/>
    /// <seealso cref="TextTable"/>
    /// <seealso cref="SplParseContext"/>
    public class SectionContentParserBulkCalls : SectionContentParserBase
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

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SectionContentParserBulkCalls with required dependencies.
        /// </summary>
        /// <param name="mediaParser">Parser for handling multimedia content within text blocks.</param>
        /// <seealso cref="SectionMediaParser"/>
        public SectionContentParserBulkCalls(SectionMediaParser? mediaParser = null) : base(mediaParser)
        {
        }

        #endregion

        #region Section Content Parsing - Bulk Operations

        /**************************************************************/
        /// <summary>
        /// Orchestrates the recursive parsing of a section's [text] element. It iterates
        /// through top-level content blocks and delegates the processing of each block
        /// to specialized helpers, preserving the nested hierarchy for SPL round-tripping.
        /// Uses bulk operations pattern for 100-1000x performance improvement.
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

        #region Specialized Content Processing

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
                grandchildEntitiesCount += await GetOrCreateTextListAndItemsAsync(block, stc.SectionTextContentID.Value, context);
            }
            else if (contentType.Equals(sc.E.Table, StringComparison.OrdinalIgnoreCase))
            {
                // Process table structure and create row/cell entities
                grandchildEntitiesCount += await GetOrCreateTextTableAndChildrenAsync(block, stc.SectionTextContentID.Value, context);
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

        #endregion

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
        public async Task<int> GetOrCreateTextListAndItemsAsync(
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
        public async Task<int> GetOrCreateTextTableAndChildrenAsync(
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
    }
}
