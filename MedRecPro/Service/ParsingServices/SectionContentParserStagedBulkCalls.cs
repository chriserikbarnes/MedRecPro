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
    /// Staged bulk operations implementation for section content parsing.
    /// Stages all database changes in memory first, then performs batch operations
    /// to minimize database connection overhead and improve performance.
    /// </summary>
    /// <remarks>
    /// This class implements the staged bulk operations pattern where:
    /// - Discovery Phase: Section discovery happens once in parent parser
    /// - Staging Phase: Parse all content to in-memory DTOs (0 database calls)
    /// - Query Phase: Bulk query existing entities (1 query per entity type across ALL sections)
    /// - Insert Phase: Bulk insert missing entities (1 insert per entity type across ALL sections)
    /// - Commit Phase: Single SaveChangesAsync at the end
    ///
    /// Performance Impact:
    /// - Before (Bulk per section): N sections × 4-8 calls = potentially hundreds of database operations
    /// - After (Staged): 8-12 calls total for ALL sections regardless of count
    ///
    /// Best suited for:
    /// - Large documents with many sections
    /// - Production environments where performance is critical
    /// - Scenarios where connection overhead is significant
    ///
    /// Uses SplParseContextExtensions methods for deferred saves:
    /// - SaveChangesIfAllowedAsync defers saves when UseBatchSaving is true
    /// - CommitDeferredChangesAsync commits all changes at the end
    /// </remarks>
    /// <seealso cref="SectionContentParserBase"/>
    /// <seealso cref="SectionContentParser_BulkCalls"/>
    /// <seealso cref="SectionParser_StagedBulk"/>
    /// <seealso cref="SplParseContextExtensions"/>
    /// <seealso cref="SectionTextContent"/>
    /// <seealso cref="TextList"/>
    /// <seealso cref="TextTable"/>
    /// <seealso cref="SplParseContext"/>
    public class SectionContentParser_StagedBulkCalls : SectionContentParserBase, ISplSectionParser
    {
        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SectionContentParserStagedBulkCalls with required dependencies.
        /// </summary>
        /// <param name="mediaParser">Parser for handling multimedia content within text blocks.</param>
        /// <seealso cref="SectionMediaParser"/>
        public SectionContentParser_StagedBulkCalls(SectionMediaParser? mediaParser = null) : base(mediaParser)
        {
        }

        public string SectionName => "section";

        #endregion

        #region Main Parsing Interface

        /**************************************************************/
        /// <summary>
        /// Parses section text content elements using staged bulk operations pattern.
        /// This method is called by the parent section parser and processes content
        /// for a single section, but uses deferred saves for staging.
        /// </summary>
        /// <param name="element">The XElement representing the section to parse for content.</param>
        /// <param name="context">The current parsing context containing section information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A SplParseResult indicating the success status and content elements created.</returns>
        /// <remarks>
        /// When called from SectionParser_StagedBulk, the context.UseBatchSaving flag is set to true,
        /// which causes all database saves to be deferred until CommitDeferredChangesAsync is called
        /// at the end of the entire document parsing operation.
        /// </remarks>
        /// <seealso cref="ParseSectionContentAsync"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
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

                reportProgress?.Invoke("Processing section content with staged bulk operations...");

                // Parse section content using staged operations
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
        /// Parses the inner content of a section using staged bulk operations.
        /// Processes hierarchies, text content, excerpts, and highlight elements.
        /// All database operations are staged via SaveChangesIfAllowedAsync.
        /// </summary>
        /// <param name="xEl">The XElement for the section whose content is to be parsed.</param>
        /// <param name="sectionId">The database ID of the parent section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A SplParseResult containing the outcome of parsing the content.</returns>
        /// <remarks>
        /// This method stages all content creation operations. When context.UseBatchSaving is true,
        /// all saves are deferred and committed in a single operation at the end.
        /// </remarks>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionExcerptHighlight"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseSectionContentAsync(XElement xEl, int sectionId, SplParseContext context)
        {
            #region implementation
            var result = new SplParseResult();

            // Process main text content including paragraphs, lists, tables, etc.
            var textEl = xEl.SplElement(sc.E.Text);
            if (textEl != null)
            {
                var (textContents, listEntityCount) = await StageSectionTextContentsAsync(textEl, sectionId, context);
                result.SectionAttributesCreated += textContents.Count;
                result.SectionAttributesCreated += listEntityCount;
            }

            // Process excerpt elements with nested content structure
            var excerptEl = xEl.SplElement(sc.E.Excerpt);
            if (excerptEl != null)
            {
                var (excerptTextContents, listEntityCount) = await StageSectionTextContentsAsync(excerptEl, sectionId, context);
                result.SectionAttributesCreated += excerptTextContents.Count;

                // Extract highlighted text within excerpts for specialized processing
                var eHighlights = await stageExcerptHighlightsAsync(excerptEl, sectionId, context);
                result.SectionAttributesCreated += eHighlights.Count;
            }

            // Process direct highlights not contained within excerpts
            var directHighlights = await stageExcerptHighlightsAsync(xEl, sectionId, context);
            result.SectionAttributesCreated += directHighlights.Count;

            return result;
            #endregion
        }

        #endregion

        #region Staged Section Content Parsing

        /**************************************************************/
        /// <summary>
        /// Stages the parsing of section text contents using deferred database operations.
        /// Orchestrates the recursive parsing with staging pattern for optimal performance.
        /// </summary>
        /// <param name="parentEl">The XElement to start parsing (typically the [text] element).</param>
        /// <param name="sectionId">The SectionID owning this content.</param>
        /// <param name="context">Parsing context with deferred save settings.</param>
        /// <param name="parentSectionTextContentId">Parent SectionTextContentID for hierarchy, or null for top-level blocks.</param>
        /// <param name="sequence">The sequence number to start from (default 1).</param>
        /// <returns>A tuple containing the complete list of all created/found SectionTextContent objects and the total count of grandchild entities.</returns>
        /// <remarks>
        /// This method uses SaveChangesIfAllowedAsync which defers saves when context.UseBatchSaving is true.
        /// All changes are committed in a single operation at the end of document parsing.
        ///
        /// Performance Pattern:
        /// - Phase 1: Parse all content blocks to DTOs (0 DB calls)
        /// - Phase 2: Bulk query existing content (1 query per section, but deferred commit)
        /// - Phase 3: Stage insert operations (tracked in change tracker, not yet saved)
        /// - Phase 4: Process specialized content (lists, tables) with staging
        /// - Commit: Single SaveChangesAsync at end of all sections (via CommitDeferredChangesAsync)
        /// </remarks>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        public async Task<Tuple<List<SectionTextContent>, int>> StageSectionTextContentsAsync(
            XElement parentEl,
            int sectionId,
            SplParseContext context,
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

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"→ parseContentBlocksToMemory for SectionID {sectionId} found {dtos.Count} content blocks");
            if (!dtos.Any())
            {
                System.Diagnostics.Debug.WriteLine($"⚠ No content blocks found. Element name: {parentEl.Name.LocalName}, Children: {string.Join(", ", parentEl.Elements().Select(e => e.Name.LocalName))}");
            }
#endif

            if (!dtos.Any())
            {
                return Tuple.Create(allContent, totalGrandChildEntities);
            }

            // PHASE 2: Bulk query existing content (1 query)
            var existingKeys = await getExistingSectionTextContentKeysAsync(dbContext, sectionId, context);

            // PHASE 3: Stage insert operations for missing content (tracked in change tracker, not yet saved)
            var (createdEntities, idLookup, entityLookup) = await stageCreateSectionTextContentAsync(dbContext, dtos, existingKeys, context);

            // Combine all entities for return value
            allContent.AddRange(createdEntities);

            // Also query existing entities from database
            var existingEntities = await dbContext.Set<SectionTextContent>()
                .Where(c => c.SectionID == sectionId)
                .ToListAsync();

            allContent.AddRange(existingEntities.Where(e => !createdEntities.Contains(e)));

            // PHASE 4: Process specialized content (lists, tables, excerpts)
            // Use entity lookup to get both new entities (with temp IDs) and existing entities
            foreach (var dto in dtos)
            {
                if (!entityLookup.TryGetValue(dto.TempId, out var stc))
                {
                    continue;
                }

                // Get the ID - either from idLookup (may be temp) or from entity (if saved)
                int? contentId = null;
                if (idLookup.TryGetValue(dto.TempId, out var lookupId))
                {
                    contentId = lookupId;
                }
                else if (stc.SectionTextContentID.HasValue)
                {
                    contentId = stc.SectionTextContentID.Value;
                }
                else
                {
                    // Try to get temp ID from change tracker
                    contentId = dbContext.GetTrackedEntityId(stc, nameof(SectionTextContent.SectionTextContentID));
                }

                if (contentId.HasValue)
                {
                    // Process specialized content - pass both entity and explicit ID
                    totalGrandChildEntities += await processSpecializedContentStagedAsync(dto.SourceElement, stc, contentId.Value, context);
                }
            }

            return Tuple.Create(allContent, totalGrandChildEntities);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Queries database once to get all existing SectionTextContent records for the section.
        /// Returns a HashSet of composite keys for efficient duplicate detection.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="sectionId">The section ID to query.</param>
        /// <param name="context">The parsing context</param>
        /// <returns>A HashSet containing composite keys of existing content blocks.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)>>
        getExistingSectionTextContentKeysAsync(
             ApplicationDbContext dbContext,
             int sectionId,
             SplParseContext context)
        {
            #region implementation
            var keys = new HashSet<(int, string, int, int?, string?)>();

            // Include tracked (staged) entities
            if (context?.UseBatchSaving == true)
            {
                var trackedEntities = dbContext.ChangeTracker.Entries<SectionTextContent>()
                    .Where(e => e.State == EntityState.Added &&
                                e.Entity.SectionID == sectionId)
                    .Select(e => e.Entity);

                foreach (var e in trackedEntities)
                {
                    if (e.SectionID.HasValue && e.SequenceNumber.HasValue)
                    {
                        keys.Add((e.SectionID.Value, e.ContentType ?? "",
                                 e.SequenceNumber.Value, e.ParentSectionTextContentID,
                                 e.ContentText));
                    }
                }
            }

            // Also query database for previously committed entities
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

            foreach (var e in existing.Where(e => e.SectionID.HasValue && e.SequenceNumber.HasValue))
            {
                keys.Add((e.SectionID!.Value, e.ContentType ?? "", e.SequenceNumber!.Value,
                         e.ParentSectionTextContentID, e.ContentText));
            }

            return keys;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stages bulk insertion of SectionTextContent records with hierarchical relationships.
        /// Uses deferred saves via SaveChangesIfAllowedAsync for staging pattern.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="dtos">The list of DTOs to insert.</param>
        /// <param name="existingKeys">HashSet of existing content keys for deduplication.</param>
        /// <param name="context">The parsing context for deferred save control.</param>
        /// <returns>A tuple containing the created entities and a lookup dictionary mapping temp IDs to database IDs.</returns>
        /// <remarks>
        /// This method stages entities in the DbContext change tracker but defers the actual
        /// database save operation via SaveChangesIfAllowedAsync. When context.UseBatchSaving is true,
        /// the save is deferred until CommitDeferredChangesAsync is called at the end.
        /// </remarks>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        private async Task<(List<SectionTextContent> entities, Dictionary<string, int> idLookup, Dictionary<string, SectionTextContent> entityLookup)>
            stageCreateSectionTextContentAsync(
                ApplicationDbContext dbContext,
                List<SectionTextContentDto> dtos,
                HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)> existingKeys,
                SplParseContext context)
        {
            #region implementation
            var createdEntities = new List<SectionTextContent>();
            var tempIdToDbIdLookup = new Dictionary<string, int>(StringComparer.Ordinal);
            var tempIdToEntityLookup = new Dictionary<string, SectionTextContent>(StringComparer.Ordinal);

            if (!dtos.Any())
            {
                return (createdEntities, tempIdToDbIdLookup, tempIdToEntityLookup);
            }

            // Pass 1: Process top-level entities (no parent)
            var topLevelDtos = filterTopLevelDtos(dtos);
            await stageTopLevelEntitiesAsync(
                dbContext,
                topLevelDtos,
                existingKeys,
                createdEntities,
                tempIdToDbIdLookup,
                tempIdToEntityLookup,
                context);

            await addExistingTopLevelToLookupAsync(
                dbContext,
                topLevelDtos,
                existingKeys,
                dtos.First().SectionID,
                tempIdToDbIdLookup,
                tempIdToEntityLookup);

            // Pass 2: Process child entities (with parent references)
            var childDtos = filterChildDtos(dtos);
            await stageChildEntitiesAsync(
                dbContext,
                childDtos,
                existingKeys,
                tempIdToDbIdLookup,
                tempIdToEntityLookup,
                createdEntities,
                context);

            return (createdEntities, tempIdToDbIdLookup, tempIdToEntityLookup);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stages creation of top-level SectionTextContent entities with deferred save.
        /// Adds entities to change tracker and uses SaveChangesIfAllowedAsync for staging.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="topLevelDtos">DTOs representing top-level entities.</param>
        /// <param name="existingKeys">HashSet for deduplication checking.</param>
        /// <param name="createdEntities">Output collection of created entities.</param>
        /// <param name="tempIdToDbIdLookup">Output dictionary mapping temporary IDs to database IDs.</param>
        /// <param name="tempIdToEntityLookup">Output dictionary mapping temporary IDs to entity references.</param>
        /// <param name="context">The parsing context for deferred save control.</param>
        /// <remarks>
        /// Uses SaveChangesIfAllowedAsync which defers the actual save when context.UseBatchSaving is true.
        /// </remarks>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        private async Task stageTopLevelEntitiesAsync(
            ApplicationDbContext dbContext,
            List<SectionTextContentDto> topLevelDtos,
            HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)> existingKeys,
            List<SectionTextContent> createdEntities,
            Dictionary<string, int> tempIdToDbIdLookup,
            Dictionary<string, SectionTextContent> tempIdToEntityLookup,
            SplParseContext context)
        {
            #region implementation
            // Filter new entities not already in database
            var newDtos = filterNewDtos(topLevelDtos, existingKeys);

            if (!newDtos.Any())
            {
                return;
            }

            // Create entity instances and track them
            var newEntities = new List<SectionTextContent>();
            foreach (var dto in newDtos)
            {
                var entity = createEntityFromDto(dto, parentId: null);
                newEntities.Add(entity);
                // Track entity reference by TempId
                tempIdToEntityLookup[dto.TempId] = entity;
            }

            // Stage in database context (add to change tracker)
            dbContext.Set<SectionTextContent>().AddRange(newEntities);

            // Use deferred save - only saves if batch saving is disabled
            await context.SaveChangesIfAllowedAsync();

            // Build temp ID to DB ID lookup (uses change tracker for temp IDs)
            buildIdLookup(newEntities, newDtos, tempIdToDbIdLookup, dbContext);

            createdEntities.AddRange(newEntities);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stages creation of child SectionTextContent entities with resolved parent references.
        /// Uses deferred save via SaveChangesIfAllowedAsync for staging pattern.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="childDtos">DTOs representing child entities.</param>
        /// <param name="existingKeys">HashSet for deduplication checking.</param>
        /// <param name="tempIdToDbIdLookup">Dictionary mapping temporary IDs to database IDs.</param>
        /// <param name="tempIdToEntityLookup">Dictionary mapping temporary IDs to entity references.</param>
        /// <param name="createdEntities">Output collection of created entities.</param>
        /// <param name="context">The parsing context for deferred save control.</param>
        /// <remarks>
        /// Uses SaveChangesIfAllowedAsync which defers the actual save when context.UseBatchSaving is true.
        /// </remarks>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        private async Task stageChildEntitiesAsync(
            ApplicationDbContext dbContext,
            List<SectionTextContentDto> childDtos,
            HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)> existingKeys,
            Dictionary<string, int> tempIdToDbIdLookup,
            Dictionary<string, SectionTextContent> tempIdToEntityLookup,
            List<SectionTextContent> createdEntities,
            SplParseContext context)
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

            // Create entity instances with resolved parent IDs and track them
            var newEntities = new List<SectionTextContent>();
            foreach (var dto in newChildDtos)
            {
                var entity = createEntityFromDto(dto, dto.ParentSectionTextContentID);
                newEntities.Add(entity);
                // Track entity reference by TempId
                tempIdToEntityLookup[dto.TempId] = entity;
            }

            // Stage in database context (add to change tracker)
            dbContext.Set<SectionTextContent>().AddRange(newEntities);

            // Use deferred save - only saves if batch saving is disabled
            await context.SaveChangesIfAllowedAsync();

            // Build temp ID to DB ID lookup for children (uses change tracker for temp IDs)
            buildIdLookup(newEntities, newChildDtos, tempIdToDbIdLookup, dbContext);

            createdEntities.AddRange(newEntities);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Queries existing top-level entities from database and adds them to the lookups.
        /// Matches DTOs to existing entities by deduplication key.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="topLevelDtos">DTOs representing top-level entities.</param>
        /// <param name="existingKeys">HashSet for deduplication checking.</param>
        /// <param name="sectionId">The section ID to filter by.</param>
        /// <param name="tempIdToDbIdLookup">Dictionary to update with existing entity ID mappings.</param>
        /// <param name="tempIdToEntityLookup">Dictionary to update with existing entity references.</param>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        private async Task addExistingTopLevelToLookupAsync(
            ApplicationDbContext dbContext,
            List<SectionTextContentDto> topLevelDtos,
            HashSet<(int sectionId, string contentType, int sequenceNumber, int? parentId, string? contentText)> existingKeys,
            int sectionId,
            Dictionary<string, int> tempIdToDbIdLookup,
            Dictionary<string, SectionTextContent> tempIdToEntityLookup)
        {
            #region implementation
            // Query existing top-level entities
            var existingTopLevel = await dbContext.Set<SectionTextContent>()
                .Where(c => c.SectionID == sectionId && c.ParentSectionTextContentID == null)
                .ToListAsync();

            // Match existing entities to DTOs and build lookups
            foreach (var existing in existingTopLevel)
            {
                var matchingDto = findMatchingDto(existing, topLevelDtos, existingKeys);

                if (matchingDto != null)
                {
                    // Add entity reference to lookup
                    tempIdToEntityLookup[matchingDto.TempId] = existing;

                    // Add ID to lookup
                    if (existing.SectionTextContentID.HasValue)
                    {
                        tempIdToDbIdLookup[matchingDto.TempId] = existing.SectionTextContentID.Value;
                    }
                }
            }
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
        /// Builds mapping from temporary DTO IDs to entity IDs (real or temporary).
        /// Uses EF Core change tracker to get temporary IDs for unsaved entities.
        /// </summary>
        /// <param name="entities">List of entities (may be unsaved with temp IDs).</param>
        /// <param name="dtos">List of DTOs with temporary IDs.</param>
        /// <param name="tempIdToDbIdLookup">Dictionary to populate with ID mappings.</param>
        /// <param name="dbContext">Database context for change tracker access.</param>
        /// <remarks>
        /// When entities are added to DbContext but not yet saved, EF Core assigns
        /// temporary negative IDs. These IDs can be used for FK relationships.
        /// On SaveChanges, EF Core automatically fixes up the FK values.
        /// 
        /// FIX: The original code checked entity.SectionTextContentID.HasValue,
        /// which is always FALSE when saves are deferred. Now we get the temp ID
        /// from the change tracker, enabling parent-child relationships before save.
        /// </remarks>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="Label"/>
        private void buildIdLookup(
            List<SectionTextContent> entities,
            List<SectionTextContentDto> dtos,
            Dictionary<string, int> tempIdToDbIdLookup,
            ApplicationDbContext dbContext)
        {
            #region implementation
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                var dto = dtos.ElementAtOrDefault(i);

                if (dto == null)
                    continue;

                // Try to get the ID - either real (if saved) or temporary (if not)
                int? entityId = null;

                // First check if entity already has a real ID (immediate save mode)
                if (entity.SectionTextContentID.HasValue)
                {
                    entityId = entity.SectionTextContentID.Value;
                }
                else
                {
                    // Entity hasn't been saved - get temporary ID from change tracker
                    // EF Core assigns negative temp IDs to Added entities
                    try
                    {
                        var entry = dbContext.Entry(entity);
                        if (entry.State != EntityState.Detached)
                        {
                            var property = entry.Property(nameof(SectionTextContent.SectionTextContentID));
                            var currentValue = property.CurrentValue;

                            if (currentValue is int intValue)
                            {
                                entityId = intValue;
#if DEBUG
                                System.Diagnostics.Debug.WriteLine($"→ SectionTextContent.SectionTextContentID = {intValue} (IsTemporary: {property.IsTemporary}, State: {entry.State})");
#endif
                            }
                        }
                    }
                    catch
                    {
                        // Fallback - entity not tracked, skip
                    }
                }

                if (entityId.HasValue)
                {
                    tempIdToDbIdLookup[dto.TempId] = entityId.Value;
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

        #region Specialized Content Processing - Staged

        /**************************************************************/
        /// <summary>
        /// Dispatches processing for special content types with staging pattern.
        /// Processes Lists, Tables, and Excerpts with deferred database saves.
        /// </summary>
        /// <param name="block">The XElement representing the content block.</param>
        /// <param name="stc">The SectionTextContent entity for this block.</param>
        /// <param name="contentId">The content ID (may be temp negative or real positive).</param>
        /// <param name="context">The current parsing context with staging settings.</param>
        /// <returns>The number of grandchild entities created (e.g., list items, table cells).</returns>
        /// <remarks>
        /// All database operations use SaveChangesIfAllowedAsync for deferred saves.
        /// The contentId parameter is passed explicitly because for new entities,
        /// the SectionTextContentID property may not be set yet (temp ID is in change tracker).
        /// </remarks>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> processSpecializedContentStagedAsync(XElement block,
            SectionTextContent stc,
            int contentId,
            SplParseContext context)
        {
            #region implementation
            int grandchildEntitiesCount = 0;
            var contentType = stc.ContentType ?? string.Empty;

            // Dispatch to appropriate specialized handler based on content type
            if (contentType.Equals(sc.E.List, StringComparison.OrdinalIgnoreCase))
            {
                // Process list structure with staging
                grandchildEntitiesCount += await stageTextListAndItemsAsync(block, contentId, context);
            }
            else if (contentType.Equals(sc.E.Table, StringComparison.OrdinalIgnoreCase))
            {
                // Process table structure with staging
                grandchildEntitiesCount += await stageTextTableAndChildrenAsync(block, contentId, context);
            }
            else if (contentType.Equals(sc.E.Excerpt, StringComparison.OrdinalIgnoreCase))
            {
                // Process excerpt highlights with staging
                if (stc.SectionID > 0)
                    await stageExcerptHighlightsAsync(block, (int)stc.SectionID, context);
            }
            else if (contentType.Equals(sc.E.RenderMultimedia, StringComparison.OrdinalIgnoreCase))
            {
                // Handle block-level images with staging
                grandchildEntitiesCount += await _mediaParser.ParseRenderedMediaAsync(block, contentId, context, isInline: false);
            }

            // Check for INLINE images inside other content types
            if (block.Descendants(ns + sc.E.RenderMultimedia).Any())
            {
                bool isInline = !contentType.Equals(sc.E.RenderMultimedia, StringComparison.OrdinalIgnoreCase);
                if (isInline)
                {
                    grandchildEntitiesCount += await _mediaParser.ParseRenderedMediaAsync(block, contentId, context, isInline: true);
                }
            }

            return grandchildEntitiesCount;
            #endregion
        }

        #endregion

        #region List Processing - Staged

        /**************************************************************/
        /// <summary>
        /// Stages creation of TextList and TextListItem entities using deferred saves.
        /// Parses list structure to memory, then stages database operations.
        /// </summary>
        /// <param name="listEl">The XElement representing the [list] element.</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent record.</param>
        /// <param name="context">The current parsing context with staging settings.</param>
        /// <returns>The total number of TextList and TextListItem entities staged for creation.</returns>
        /// <remarks>
        /// Uses SaveChangesIfAllowedAsync which defers saves when context.UseBatchSaving is true.
        /// All changes are committed at the end via CommitDeferredChangesAsync.
        /// </remarks>
        /// <seealso cref="TextList"/>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> stageTextListAndItemsAsync(
            XElement listEl,
            int sectionTextContentId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate inputs - allow negative temp IDs (sectionTextContentId != 0)
            if (listEl == null || sectionTextContentId == 0 || context?.ServiceProvider == null)
            {
                return 0;
            }

            var dbContext = context.GetDbContext();

            // Parse list structure into memory
            var itemDtos = parseTextListItemsToMemory(listEl);

            // For temp IDs (negative), we know there's no existing list in database
            // Only query for existing if we have a real positive ID
            TextList? existingList = null;
            if (sectionTextContentId > 0)
            {
                var textListDbSet = dbContext.Set<TextList>();
                existingList = await textListDbSet
                    .FirstOrDefaultAsync(l => l.SectionTextContentID == sectionTextContentId);
            }

            TextList textList;

            if (existingList == null)
            {
                // Create and stage new list entity
                textList = createTextListEntity(listEl, sectionTextContentId);
                dbContext.Set<TextList>().Add(textList);

                // Use deferred save
                await context.SaveChangesIfAllowedAsync();
                createdCount++;
            }
            else
            {
                textList = existingList;
            }

            // Get the TextList ID - either from entity property (if saved) or from change tracker (if temp)
            int? textListId = textList.TextListID > 0 ? textList.TextListID : null;
            if (!textListId.HasValue)
            {
                // Get temp ID from change tracker
                textListId = dbContext.GetTrackedEntityId(textList, nameof(TextList.TextListID));
            }

            if (!textListId.HasValue)
            {
                context.Logger?.LogError("Failed to create or retrieve TextList ID for SectionTextContentID {id}", sectionTextContentId);


#if DEBUG
                Debug.WriteLine($"SectionContentParser_StagedBulkCalls.stageTextListAndItemsAsync: Failed to create or retrieve TextList ID for SectionTextContentID {sectionTextContentId}");
#endif

                return createdCount;
            }

            // Stage list items with deferred save
            createdCount += await stageTextListItemsAsync(dbContext, textListId.Value, itemDtos, context);

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stages bulk creation of TextListItem entities using deferred saves.
        /// Checks for existing items and stages only missing ones.
        /// </summary>
        /// <param name="dbContext">The database context for querying and staging entities.</param>
        /// <param name="textListId">The foreign key ID of the parent TextList.</param>
        /// <param name="itemDtos">The list of item DTOs parsed from XML.</param>
        /// <param name="context">The parsing context for deferred save control.</param>
        /// <returns>The count of newly staged TextListItem entities.</returns>
        /// <remarks>
        /// Uses SaveChangesIfAllowedAsync which defers saves when context.UseBatchSaving is true.
        /// </remarks>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> stageTextListItemsAsync(
            ApplicationDbContext dbContext,
            int textListId,
            List<TextListItemDto> itemDtos,
            SplParseContext context)
        {
            #region implementation

            if (!itemDtos.Any())
                return 0;

            var itemDbSet = dbContext.Set<TextListItem>();

#if DEBUG
            Debug.WriteLine($"→ Entering stageTextListItemsAsync: called with textListId={textListId}, itemDtos.Count={itemDtos?.Count ?? -1}");
#endif

            // For temp IDs (negative), there won't be existing items in database
            var existingKeys = new HashSet<(int ListId, int SeqNum)>();
            if (textListId > 0)
            {
                var existingItems = await itemDbSet
                    .Where(i => i.TextListID == textListId)
                    .Select(i => new { i.TextListID, i.SequenceNumber })
                    .ToListAsync();

                existingKeys = new HashSet<(int ListId, int SeqNum)>(
                    existingItems
                        .Where(i => i.TextListID.HasValue && i.SequenceNumber.HasValue)
                        .Select(i => (i.TextListID!.Value, i.SequenceNumber!.Value))
                );
            }

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

                // Use deferred save
                await context.SaveChangesIfAllowedAsync();
            }

            return newItems.Count;

            #endregion
        }

        #endregion

        #region Table Processing - Staged

        /**************************************************************/
        /// <summary>
        /// Stages creation of TextTable and all child entities using deferred saves.
        /// Parses table structure to memory, then stages all database operations.
        /// </summary>
        /// <param name="tableEl">The XElement representing the [table] element.</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent record.</param>
        /// <param name="context">The current parsing context with staging settings.</param>
        /// <returns>The total number of table, column, row, and cell entities staged for creation.</returns>
        /// <remarks>
        /// This method stages all table-related entities in the DbContext change tracker.
        /// Uses SaveChangesIfAllowedAsync which defers actual database saves when
        /// context.UseBatchSaving is true. All changes are committed at the end via
        /// CommitDeferredChangesAsync.
        /// </remarks>
        /// <seealso cref="TextTable"/>
        /// <seealso cref="TextTableColumn"/>
        /// <seealso cref="TextTableRow"/>
        /// <seealso cref="TextTableCell"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> stageTextTableAndChildrenAsync(
            XElement tableEl,
            int sectionTextContentId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate inputs - allow negative temp IDs (sectionTextContentId != 0)
            if (tableEl == null || sectionTextContentId == 0 || context?.ServiceProvider == null)
            {
                return 0;
            }

            var dbContext = context.GetDbContext();

            // Parse table structure into memory
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

            // Parse all columns, rows, and cells into memory
            var columnDtos = parseColumnsToMemory(tableEl);

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

            // For temp IDs (negative), we know there's no existing table in database
            // Only query for existing if we have a real positive ID
            TextTable? existingTable = null;
            if (sectionTextContentId > 0)
            {
                var textTableDbSet = dbContext.Set<TextTable>();
                existingTable = await textTableDbSet
                    .FirstOrDefaultAsync(t => t.SectionTextContentID == sectionTextContentId);
            }

            TextTable textTable;
            if (existingTable == null)
            {
                // Create and stage new table entity
                textTable = new TextTable
                {
                    SectionTextContentID = tableData.SectionTextContentID,
                    SectionTableLink = tableData.SectionTableLink,
                    Width = tableData.Width,
                    Caption = tableData.Caption,
                    HasHeader = tableData.HasHeader,
                    HasFooter = tableData.HasFooter
                };

                dbContext.Set<TextTable>().Add(textTable);

                // Use deferred save
                await context.SaveChangesIfAllowedAsync();
                createdCount++;
            }
            else
            {
                textTable = existingTable;
            }

            // Get the TextTable ID - either from entity property (if saved) or from change tracker (if temp)
            int? tableId = textTable.TextTableID > 0 ? textTable.TextTableID : null;
            if (!tableId.HasValue)
            {
                // Get temp ID from change tracker
                tableId = dbContext.GetTrackedEntityId(textTable, nameof(TextTable.TextTableID));
            }

            if (!tableId.HasValue)
            {
                context.Logger?.LogError("Failed to create or retrieve TextTable ID for SectionTextContentID {id}", sectionTextContentId);

#if DEBUG
                Debug.WriteLine($"SectionContentParser_StagedBulkCalls.stageTextTableAndChildrenAsync: Failed to create or retrieve TextTable ID for SectionTextContentID {sectionTextContentId}");
#endif
                return createdCount;
            }

            // Stage columns with deferred save
            createdCount += await stageColumnsAsync(dbContext, tableId.Value, columnDtos, context);

            // Stage rows and cells with deferred save
            createdCount += await stageRowsAndCellsAsync(dbContext, tableId.Value, rowDtos, context);

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stages bulk creation of TextTableColumn entities using deferred saves.
        /// Checks for existing columns and stages only missing ones.
        /// </summary>
        /// <param name="dbContext">The database context for querying and staging entities.</param>
        /// <param name="textTableId">The foreign key ID of the parent TextTable.</param>
        /// <param name="columnDtos">The list of column DTOs parsed from XML.</param>
        /// <param name="context">The parsing context for deferred save control.</param>
        /// <returns>The count of newly staged TextTableColumn entities.</returns>
        /// <remarks>
        /// Uses SaveChangesIfAllowedAsync which defers saves when context.UseBatchSaving is true.
        /// </remarks>
        /// <seealso cref="TextTableColumn"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> stageColumnsAsync(
            ApplicationDbContext dbContext,
            int textTableId,
            List<TableColumnDto> columnDtos,
            SplParseContext context)
        {
            #region implementation

            if (!columnDtos.Any())
                return 0;

            var columnDbSet = dbContext.Set<TextTableColumn>();

            // For temp IDs (negative), there won't be existing columns in database
            var existingKeys = new HashSet<(int TableId, int SeqNum)>();
            if (textTableId > 0)
            {
                var existingColumns = await columnDbSet
                    .Where(c => c.TextTableID == textTableId)
                    .Select(c => new { c.TextTableID, c.SequenceNumber })
                    .ToListAsync();

                existingKeys = new HashSet<(int TableId, int SeqNum)>(
                    existingColumns
                        .Where(c => c.TextTableID.HasValue && c.SequenceNumber.HasValue)
                        .Select(c => (c.TextTableID!.Value, c.SequenceNumber!.Value))
                );
            }

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
                columnDbSet.AddRange(newColumns);

                // Use deferred save
                await context.SaveChangesIfAllowedAsync();
            }

            return newColumns.Count;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stages bulk creation of TextTableRow and TextTableCell entities using deferred saves.
        /// Checks for existing entities and stages only missing ones in batch operations.
        /// </summary>
        /// <param name="dbContext">The database context for querying and staging entities.</param>
        /// <param name="textTableId">The foreign key ID of the parent TextTable.</param>
        /// <param name="rowDtos">The list of row DTOs (including cell DTOs) parsed from XML.</param>
        /// <param name="context">The parsing context for deferred save control.</param>
        /// <returns>The count of newly staged TextTableRow and TextTableCell entities.</returns>
        /// <remarks>
        /// Uses SaveChangesIfAllowedAsync which defers saves when context.UseBatchSaving is true.
        /// </remarks>
        /// <seealso cref="TextTableRow"/>
        /// <seealso cref="TextTableCell"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> stageRowsAndCellsAsync(
            ApplicationDbContext dbContext,
            int textTableId,
            List<TableRowDto> rowDtos,
            SplParseContext context)
        {
            #region implementation

            int createdCount = 0;

            if (!rowDtos.Any())
                return 0;

            // Stage rows
            var rowDbSet = dbContext.Set<TextTableRow>();

            // For temp IDs (negative), there won't be existing rows in database
            var existingRowKeys = new HashSet<(int TableId, string GroupType, int SeqNum)>();
            if (textTableId > 0)
            {
                var existingRows = await rowDbSet
                    .Where(r => r.TextTableID == textTableId)
                    .Select(r => new { r.TextTableID, r.RowGroupType, r.SequenceNumber, r.TextTableRowID })
                    .ToListAsync();

                existingRowKeys = new HashSet<(int TableId, string GroupType, int SeqNum)>(
                    existingRows
                        .Where(r => r.TextTableID.HasValue && r.RowGroupType != null && r.SequenceNumber.HasValue)
                        .Select(r => (r.TextTableID!.Value, r.RowGroupType!, r.SequenceNumber!.Value))
                );
            }

            // Create new row entities and track them
            var newRowEntities = new List<(TextTableRow Entity, TableRowDto Dto)>();
            foreach (var dto in rowDtos)
            {
                if (!existingRowKeys.Contains((textTableId, dto.RowGroupType, dto.SequenceNumber)))
                {
                    var entity = new TextTableRow
                    {
                        TextTableID = textTableId,
                        RowGroupType = dto.RowGroupType,
                        SequenceNumber = dto.SequenceNumber,
                        StyleCode = dto.StyleCode
                    };
                    newRowEntities.Add((entity, dto));
                }
            }

            if (newRowEntities.Any())
            {
                rowDbSet.AddRange(newRowEntities.Select(x => x.Entity));

                // Use deferred save
                await context.SaveChangesIfAllowedAsync();
                createdCount += newRowEntities.Count;
            }

            // Build row lookup from tracked entities (includes temp IDs)
            var rowLookup = new Dictionary<(int TableId, string GroupType, int SeqNum), int>();

            // Add newly created rows with their temp IDs from change tracker
            foreach (var (entity, dto) in newRowEntities)
            {
                int? rowId = entity.TextTableRowID > 0 ? entity.TextTableRowID : null;

                if (!rowId.HasValue)
                {
                    // Get temp ID from change tracker
                    rowId = dbContext.GetTrackedEntityId(entity, nameof(TextTableRow.TextTableRowID));
                }

                if (rowId.HasValue)
                {
                    var key = (textTableId, dto.RowGroupType, dto.SequenceNumber);
                    rowLookup[key] = rowId.Value;
                }

#if DEBUG
                if (!rowId.HasValue)
                {
                    Debug.WriteLine($"SectionContentParser_StagedBulkCalls.stageRowsAndCellsAsync. Failed to get a row id from either GetTrackedEntityId nor rowLookup[key] = rowId.Value");
                } 
#endif
            }

            // Also add existing rows from database (if any)
            if (textTableId > 0)
            {
                var dbRows = await rowDbSet
                    .Where(r => r.TextTableID == textTableId && r.TextTableRowID.HasValue)
                    .Select(r => new { r.TextTableID, r.RowGroupType, r.SequenceNumber, r.TextTableRowID })
                    .ToListAsync();

                foreach (var r in dbRows)
                {
                    if (r.TextTableID.HasValue && r.RowGroupType != null && r.SequenceNumber.HasValue && r.TextTableRowID.HasValue)
                    {
                        var key = (r.TextTableID.Value, r.RowGroupType, r.SequenceNumber.Value);
                        if (!rowLookup.ContainsKey(key))
                        {
                            rowLookup[key] = r.TextTableRowID.Value;
                        }
                    }
                }
            }

            // Stage cells using row lookup (includes temp row IDs)
            var cellDbSet = dbContext.Set<TextTableCell>();

            // For temp row IDs, there won't be existing cells in database
            var existingCellKeys = new HashSet<(int RowId, int SeqNum)>();
            var realRowIds = rowLookup.Values.Where(id => id > 0).ToList();
            if (realRowIds.Any())
            {
                var existingCells = await cellDbSet
                    .Where(c => c.TextTableRowID.HasValue && realRowIds.Contains(c.TextTableRowID.Value))
                    .Select(c => new { c.TextTableRowID, c.SequenceNumber })
                    .ToListAsync();

                existingCellKeys = new HashSet<(int RowId, int SeqNum)>(
                    existingCells
                        .Where(c => c.TextTableRowID.HasValue && c.SequenceNumber.HasValue)
                        .Select(c => (c.TextTableRowID!.Value, c.SequenceNumber!.Value))
                );
            }

            var newCells = new List<TextTableCell>();
            foreach (var rowDto in rowDtos)
            {
                var rowKey = (textTableId, rowDto.RowGroupType, rowDto.SequenceNumber);
                if (!rowLookup.TryGetValue(rowKey, out int rowId))
                    continue;

                foreach (var cellDto in rowDto.Cells)
                {
                    // Only check for existing if rowId is positive (real ID)
                    if (rowId > 0 && existingCellKeys.Contains((rowId, cellDto.SequenceNumber)))
                        continue;

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

            if (newCells.Any())
            {
                cellDbSet.AddRange(newCells);

                // Use deferred save
                await context.SaveChangesIfAllowedAsync();
                createdCount += newCells.Count;
            }

            return createdCount;

            #endregion
        }

        #endregion

        #region Highlight Processing - Staged

        /**************************************************************/
        /// <summary>
        /// Stages creation of SectionExcerptHighlight records using deferred saves.
        /// Finds or creates highlights for all highlight text nodes within excerpt elements.
        /// </summary>
        /// <param name="excerptEl">The XElement to search for excerpt/highlight/text patterns.</param>
        /// <param name="sectionId">The SectionID owning this highlight content.</param>
        /// <param name="context">Parsing context with deferred save settings.</param>
        /// <returns>List of SectionExcerptHighlight objects (created or found).</returns>
        /// <remarks>
        /// Uses SaveChangesIfAllowedAsync which defers saves when context.UseBatchSaving is true.
        /// This method captures the complete inner XML of highlight text elements for database storage.
        /// 
        /// FIX APPLIED: Replaced repo.CreateAsync with dbSet.Add + SaveChangesIfAllowedAsync.
        /// repo.CreateAsync was triggering an immediate SaveChanges on the context, which caused
        /// premature persistence of partially staged graph entities (with temp IDs), causing 
        /// foreign key constraint failures and subsequent data loss.
        /// </remarks>
        /// <seealso cref="SectionExcerptHighlight"/>
        /// <seealso cref="SplParseContextExtensions.SaveChangesIfAllowedAsync"/>
        /// <seealso cref="Label"/>
        private async Task<List<SectionExcerptHighlight>> stageExcerptHighlightsAsync(
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

            // Get database context directly (bypass repository to avoid auto-save)
            var dbContext = context.GetDbContext();
            var dbSet = dbContext.Set<SectionExcerptHighlight>();

            // Find all highlight elements directly under this excerpt
            var highlightElements = excerptEl.Elements(ns + sc.E.Highlight);

            foreach (var highlightEl in highlightElements)
            {
                var textEl = highlightEl.Element(ns + sc.E.Text);
                if (textEl == null)
                {
                    context.Logger?.LogWarning($"Highlight element without text child in SectionID {sectionId}");
                    continue;
                }

                string? txt = null;
                try
                {
                    var innerNodes = textEl.Nodes();
                    if (innerNodes != null && innerNodes.Any())
                    {
                        txt = string.Concat(innerNodes.Select(n => n.ToString()))?.NormalizeXmlWhitespace();
                    }
                    else
                    {
                        txt = textEl.Value?.Trim();
                    }
                }
                catch (Exception ex)
                {
                    context.Logger?.LogError(ex, $"Error extracting highlight XML for SectionID {sectionId}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(txt)) continue;

                // Check existing locally in DB (for staging, we usually skip this or cache keys, 
                // but for now we keep the check to match logic)
                var existing = await dbSet
                    .Where(eh => eh.SectionID == sectionId && eh.HighlightText == txt)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    highlights.Add(existing);
                    continue;
                }

                var newHighlight = new SectionExcerptHighlight
                {
                    SectionID = sectionId,
                    HighlightText = txt
                };

                // FIX: Use DbSet.Add + Deferred Save
                dbSet.Add(newHighlight);
                await context.SaveChangesIfAllowedAsync();

                highlights.Add(newHighlight);

                context.Logger?.LogInformation($"Staged SectionExcerptHighlight for SectionID {sectionId} with {txt.Length} characters");
            }

            return highlights;
            #endregion
        }

        #endregion
    }
}