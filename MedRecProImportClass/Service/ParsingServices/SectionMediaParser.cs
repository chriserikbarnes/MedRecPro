using System.Xml.Linq;
using MedRecProImportClass.DataAccess;
using MedRecProImportClass.Models;
using MedRecProImportClass.Data;
using MedRecProImportClass.Helpers;
using Microsoft.EntityFrameworkCore;
using static MedRecProImportClass.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecProImportClass.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecProImportClass.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.


namespace MedRecProImportClass.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Specialized parser for handling media-related elements within SPL sections.
    /// Processes ObservationMedia and RenderedMedia elements while maintaining
    /// relationships between content and multimedia references.
    /// </summary>
    /// <remarks>
    /// This parser handles the extraction and database persistence of multimedia
    /// content referenced within SPL sections, including image metadata and
    /// rendering instructions for inline and block-level media.
    /// Supports both legacy (one-at-a-time) and bulk operations modes via context.UseBulkOperations.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="ObservationMedia"/>
    /// <seealso cref="RenderedMedia"/>
    /// <seealso cref="SplParseContext"/>
    public class SectionMediaParser : ISplSectionParser
    {
        #region Fields

        /**************************************************************/
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecProImportClass.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        #endregion

        /**************************************************************/
        /// <summary>
        /// Gets the section name for this parser, representing media processing.
        /// </summary>
        public string SectionName => "media";

        /**************************************************************/
        /// <summary>
        /// Parses media elements from an SPL section, processing both ObservationMedia
        /// and RenderedMedia elements according to SPL specifications.
        /// </summary>
        /// <param name="element">The XElement representing the section to parse for media.</param>
        /// <param name="context">The current parsing context containing section information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <param name="isParentCallingForAllSubElements">(DEFAULT = false) Indicates whether the delegate will loop on outer Element</param>
        /// <returns>A SplParseResult indicating the success status and media elements created.</returns>
        /// <remarks>
        /// This method is typically called as part of a larger section parsing operation
        /// and focuses specifically on multimedia content extraction and persistence.
        /// </remarks>
        /// <seealso cref="ParseObservationMediaAsync"/>
        /// <seealso cref="ParseRenderedMediaAsync"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
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
                    result.Errors.Add("No current section available for media parsing.");
                    return result;
                }

                reportProgress?.Invoke("Processing media elements...");

                // Parse ObservationMedia elements
                var observationMedia = await ParseObservationMediaAsync(element, context.CurrentSection.SectionID.Value, context);
                result.SectionAttributesCreated += observationMedia.Count;

                reportProgress?.Invoke($"Processed {observationMedia.Count} observation media elements");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing media elements: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing media elements for section {SectionId}",
                    context.CurrentSection?.SectionID);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates ObservationMedia records for all [observationMedia] elements
        /// within a section, capturing image metadata for database storage.
        /// Routes to bulk or legacy implementation based on context.UseBulkOperations.
        /// </summary>
        /// <param name="sectionEl">The XElement for the parent [section].</param>
        /// <param name="sectionId">The SectionID of the parent section (already saved).</param>
        /// <param name="context">Parsing context for repo/db access.</param>
        /// <returns>List of ObservationMedia objects (created or found).</returns>
        /// <seealso cref="ObservationMedia"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        public async Task<List<ObservationMedia>> ParseObservationMediaAsync(
            XElement sectionEl,
            int sectionId,
            SplParseContext context)
        {
            #region implementation
            // Route to bulk or legacy based on context flag
            if (context.UseBulkOperations)
            {
                return await parseObservationMediaAsync_bulkCalls(sectionEl, sectionId, context);
            }
            else
            {
                return await parseObservationMediaAsync_singleCalls(sectionEl, sectionId, context!);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates RenderedMedia records for all [renderMultimedia] tags within a given content block.
        /// This method links a SectionTextContent entry to its corresponding ObservationMedia entry.
        /// Routes to bulk or legacy implementation based on context.UseBulkOperations.
        /// </summary>
        /// <param name="contentBlockEl">The XElement for the content block (e.g., a paragraph or a block-level renderMultimedia).</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent record.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="isInline">Indicates if the media is rendered inline within text or as a standalone block.</param>
        /// <returns>The number of RenderedMedia entities created.</returns>
        /// <seealso cref="RenderedMedia"/>
        /// <seealso cref="ObservationMedia"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        public async Task<int> ParseRenderedMediaAsync(
            XElement contentBlockEl,
            int sectionTextContentId,
            SplParseContext context,
            bool isInline)
        {
            #region implementation
            // Route to bulk or legacy based on context flag
            if (context.UseBulkOperations)
            {
                return await parseRenderedMediaAsync_bulkCalls(contentBlockEl, sectionTextContentId, context, isInline);
            }
            else
            {
                return await parseRenderedMediaAsync_singleCalls(contentBlockEl, sectionTextContentId, context, isInline);
            }
            #endregion
        }

        #region Single Call Implementations

        /**************************************************************/
        /// <summary>
        /// Single call implementation of ObservationMedia parsing (one-at-a-time).
        /// Processes each media element individually with separate database calls.
        /// </summary>
        /// <param name="sectionEl">The XElement for the parent section.</param>
        /// <param name="sectionId">The SectionID of the parent section.</param>
        /// <param name="context">Parsing context for database access.</param>
        /// <returns>List of ObservationMedia objects (created or found).</returns>
        /// <remarks>
        /// This method maintains backward compatibility with the original parsing logic.
        /// Each media element results in a separate database query and insert operation.
        /// </remarks>
        /// <seealso cref="ParseObservationMediaAsync"/>
        /// <seealso cref="ObservationMedia"/>
        private async Task<List<ObservationMedia>> parseObservationMediaAsync_singleCalls(
            XElement sectionEl,
            int sectionId,
            SplParseContext context)
        {
            #region implementation
            var mediaList = new List<ObservationMedia>();

            // Validate required input parameters to prevent null reference exceptions
            if (sectionEl == null || sectionId <= 0)
                return mediaList;

            // Validate required context dependencies to ensure proper service resolution
            if (context?.ServiceProvider == null)
                return mediaList;

            // Get database context and repository for ObservationMedia operations
            var dbContext = context.GetDbContext();
            var repo = context.GetRepository<ObservationMedia>();
            var dbSet = dbContext.Set<ObservationMedia>();

            // Find all <component><observationMedia> children within the section
            var mediaElements = sectionEl.SplElements(sc.E.Component, sc.E.ObservationMedia).ToList();

            // This handles cases where the section itself is wrapped in a component
            var parentEl = sectionEl.Parent;
            if (parentEl != null)
            {
                var siblingMediaElements = parentEl.Parent?.SplElements(sc.E.Component, sc.E.ObservationMedia) ?? Enumerable.Empty<XElement>();
                mediaElements.AddRange(siblingMediaElements.Where(media => !mediaElements.Contains(media)));
            }

            // Process each media element individually
            foreach (var mediaEl in mediaElements)
            {
                if (mediaEl == null) continue;

                // Extract data from the XML element
                var mediaId = mediaEl.GetAttrVal(sc.A.ID);
                if (string.IsNullOrWhiteSpace(mediaId))
                {
                    // ID is crucial for linking, skip if missing
                    continue;
                }

                // Deduplicate based on SectionID and the media's own ID attribute
                var existingMedia = await dbSet.FirstOrDefaultAsync(m =>
                    m.SectionID == sectionId &&
                    m.MediaID == mediaId);

                if (existingMedia != null)
                {
                    // Use existing media record instead of creating duplicate
                    mediaList.Add(existingMedia);
                    continue;
                }

                // Not found, so create a new one with extracted XML data
                var newMedia = new ObservationMedia
                {
                    SectionID = sectionId,
                    DocumentID = context?.Document?.DocumentID,
                    MediaID = mediaId,
                    DescriptionText = mediaEl.GetSplElementVal(sc.E.Text),
                    MediaType = mediaEl.GetSplElementAttrVal(sc.E.Value, sc.A.MediaType),
                    XsiType = mediaEl.SplElement(sc.E.Value)?.GetXsiType(),
                    // Path: observationMedia > value > reference
                    FileName = mediaEl.SplElement(sc.E.Value)
                        ?.SplElement(sc.E.Reference)
                        ?.Attribute(sc.A.Value)?.Value
                };

                // Save new media record to database
                await repo.CreateAsync(newMedia);
                mediaList.Add(newMedia);
            }

            return mediaList;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Legacy implementation of RenderedMedia parsing (one-at-a-time).
        /// Processes each renderMultimedia element individually with separate database calls.
        /// </summary>
        /// <param name="contentBlockEl">The XElement for the content block.</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="isInline">Whether the media is inline or block-level.</param>
        /// <returns>Count of RenderedMedia entities created.</returns>
        /// <remarks>
        /// This method maintains backward compatibility with the original parsing logic.
        /// Each renderMultimedia element results in separate database queries.
        /// </remarks>
        /// <seealso cref="ParseRenderedMediaAsync"/>
        /// <seealso cref="RenderedMedia"/>
        private async Task<int> parseRenderedMediaAsync_singleCalls(
            XElement contentBlockEl,
            int sectionTextContentId,
            SplParseContext context,
            bool isInline)
        {
            #region implementation
            int createdCount = 0;

            // Validate context to ensure required services and current section are available
            if (context == null || context?.ServiceProvider == null || context?.CurrentSection == null) return 0;

            int documentId = context?.Document?.DocumentID ?? 0;

            // Get DB context and repositories for RenderedMedia and ObservationMedia operations
            var dbContext = context!.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var renderedMediaRepo = context.GetRepository<RenderedMedia>();
            var renderedMediaDbSet = dbContext.Set<RenderedMedia>();
            var observationMediaDbSet = dbContext.Set<ObservationMedia>();

            // Find all <renderMultimedia> elements. If the block is the element itself, this will be the only one.
            // If it's a paragraph, it will find all descendants.
            var renderedElements = contentBlockEl.Name.LocalName.Equals(sc.E.RenderMultimedia, StringComparison.OrdinalIgnoreCase)
                ? new List<XElement> { contentBlockEl }
                : contentBlockEl.Descendants(ns + sc.E.RenderMultimedia).ToList();

            // Return early if no renderMultimedia elements found
            if (!renderedElements.Any()) return 0;

            // Process each renderMultimedia element individually
            int seqNum = 1;
            foreach (var el in renderedElements)
            {
                // Extract the referenced object ID from the renderMultimedia element
                var referencedObjectId = el.Attribute(sc.A.ReferencedObject)?.Value;

                if (string.IsNullOrWhiteSpace(referencedObjectId))
                {
                    // Log warning for missing referencedObject attribute
                    context!.Logger?.LogWarning("Found <renderMultimedia> tag with no referencedObject attribute in file {FileName}.", context.FileNameInZip);
                    continue;
                }

                // Find the ObservationMedia this tag refers to.
                // Note: This assumes ObservationMedia for the entire section has already been parsed.
                var observationMedia = await observationMediaDbSet
                    .FirstOrDefaultAsync(om =>
                        om != null
                        && om.MediaID == referencedObjectId
                        && om.DocumentID != null
                        && om.DocumentID == documentId);

                if (observationMedia?.ObservationMediaID == null)
                {
                    // Log warning for dangling reference when no matching ObservationMedia found
                    context?.Logger?.LogWarning("Dangling reference: <renderMultimedia referencedObject='{RefId}'> found, but no matching <observationMedia> was found in the same section in file {FileName}.",
                        referencedObjectId, context.FileNameInZip);
                    continue;
                }

                // Deduplicate: Check if this link already exists to avoid duplicate records
                var existingLink = await renderedMediaDbSet.FirstOrDefaultAsync(rm =>
                    rm.SectionTextContentID == sectionTextContentId &&
                    rm.DocumentID == documentId &&
                    rm.ObservationMediaID == observationMedia.ObservationMediaID &&
                    rm.SequenceInContent == seqNum);

                if (existingLink == null)
                {
                    // Create new RenderedMedia link between SectionTextContent and ObservationMedia
                    var newLink = new RenderedMedia
                    {
                        SectionTextContentID = sectionTextContentId,
                        ObservationMediaID = observationMedia.ObservationMediaID,
                        DocumentID = documentId,
                        SequenceInContent = seqNum,
                        IsInline = isInline
                    };
                    await renderedMediaRepo.CreateAsync(newLink);
                    createdCount++;
                }
                // Increment sequence number for proper ordering of media within content
                seqNum++;
            }

            return createdCount;
            #endregion
        }

        #endregion

        #region Bulk Operations

        /**************************************************************/
        /// <summary>
        /// Data transfer object for ObservationMedia during bulk parsing.
        /// </summary>
        /// <remarks>
        /// Used to hold parsed data in memory before bulk database operations.
        /// </remarks>
        /// <seealso cref="ObservationMedia"/>
        private class ObservationMediaDto
        {
            public string MediaID { get; set; } = string.Empty;
            public string? DescriptionText { get; set; }
            public string? MediaType { get; set; }
            public string? XsiType { get; set; }
            public string? FileName { get; set; }
        }

        /**************************************************************/
        /// <summary>
        /// Data transfer object for RenderedMedia during bulk parsing.
        /// </summary>
        /// <remarks>
        /// Used to hold parsed data in memory before bulk database operations.
        /// </remarks>
        /// <seealso cref="RenderedMedia"/>
        private class RenderedMediaDto
        {
            public string ReferencedObjectId { get; set; } = string.Empty;
            public int SequenceInContent { get; set; }
        }

        /**************************************************************/
        /// <summary>
        /// PHASE 1: Parse ObservationMedia elements to DTOs (0 DB calls).
        /// Extracts all media data from XML into memory structures.
        /// </summary>
        /// <param name="sectionEl">The XElement for the parent section.</param>
        /// <returns>List of ObservationMediaDto objects with parsed data.</returns>
        /// <remarks>
        /// This method performs no database operations, only XML parsing.
        /// Handles edge cases where section is wrapped in component elements.
        /// </remarks>
        /// <seealso cref="ObservationMediaDto"/>
        /// <seealso cref="XElementExtensions"/>
        private List<ObservationMediaDto> parseObservationMediaToMemory(XElement sectionEl)
        {
            #region implementation
            var dtos = new List<ObservationMediaDto>();

            // Find all <component><observationMedia> children within the section
            var mediaElements = sectionEl.SplElements(sc.E.Component, sc.E.ObservationMedia).ToList();

            // This handles cases where the section itself is wrapped in a component
            var parentEl = sectionEl.Parent;
            if (parentEl != null)
            {
                var siblingMediaElements = parentEl.Parent?.SplElements(sc.E.Component, sc.E.ObservationMedia) ?? Enumerable.Empty<XElement>();
                mediaElements.AddRange(siblingMediaElements.Where(media => !mediaElements.Contains(media)));
            }

            // Extract data from each media element into DTOs
            foreach (var mediaEl in mediaElements)
            {
                if (mediaEl == null) continue;

                // Extract data from the XML element
                var mediaId = mediaEl.GetAttrVal(sc.A.ID);
                if (string.IsNullOrWhiteSpace(mediaId))
                {
                    // ID is crucial for linking, skip if missing
                    continue;
                }

                // Create DTO with all extracted properties
                dtos.Add(new ObservationMediaDto
                {
                    MediaID = mediaId,
                    DescriptionText = mediaEl.GetSplElementVal(sc.E.Text),
                    MediaType = mediaEl.GetSplElementAttrVal(sc.E.Value, sc.A.MediaType),
                    XsiType = mediaEl.SplElement(sc.E.Value)?.GetXsiType(),
                    // Path: observationMedia > value > reference > value attribute
                    FileName = mediaEl.SplElement(sc.E.Value)
                        ?.SplElement(sc.E.Reference)
                        ?.Attribute(sc.A.Value)?.Value
                });
            }

            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PHASE 2: Bulk query existing ObservationMedia (1 query).
        /// Retrieves all existing media IDs for the section to avoid duplicates.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="sectionId">The section ID to query for.</param>
        /// <returns>HashSet of existing MediaID values (case-insensitive).</returns>
        /// <remarks>
        /// Uses case-insensitive comparison for MediaID strings.
        /// Single database query regardless of number of media elements.
        /// </remarks>
        /// <seealso cref="ObservationMedia"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<HashSet<string>> getExistingObservationMediaKeysAsync(
            ApplicationDbContext dbContext,
            int sectionId)
        {
            #region implementation
            // Query all existing MediaID values for this section
            var existing = await dbContext.Set<ObservationMedia>()
                .Where(m => m.SectionID == sectionId)
                .Select(m => m.MediaID)
                .ToListAsync();

            // Return as case-insensitive HashSet for efficient lookup
            return new HashSet<string>(existing.Where(e => e != null)!, StringComparer.OrdinalIgnoreCase);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PHASE 3: Bulk insert missing ObservationMedia (1 insert).
        /// Creates database records for all media elements not already in the database.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="sectionId">The section ID for the media.</param>
        /// <param name="documentId">The document ID for the media.</param>
        /// <param name="dtos">List of parsed DTOs.</param>
        /// <param name="existingKeys">HashSet of existing MediaID values.</param>
        /// <returns>Count of new ObservationMedia records created.</returns>
        /// <remarks>
        /// Uses AddRange for efficient bulk insert operation.
        /// Only creates records for media not found in existingKeys.
        /// </remarks>
        /// <seealso cref="ObservationMedia"/>
        /// <seealso cref="ObservationMediaDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<int> bulkCreateObservationMediaAsync(
            ApplicationDbContext dbContext,
            int sectionId,
            int? documentId,
            List<ObservationMediaDto> dtos,
            HashSet<string> existingKeys)
        {
            #region implementation
            // Filter to only DTOs not already in database
            var newMedia = dtos
                .Where(dto => !existingKeys.Contains(dto.MediaID))
                .Select(dto => new ObservationMedia
                {
                    SectionID = sectionId,
                    DocumentID = documentId,
                    MediaID = dto.MediaID,
                    DescriptionText = dto.DescriptionText,
                    MediaType = dto.MediaType,
                    XsiType = dto.XsiType,
                    FileName = dto.FileName
                })
                .ToList();

            // Bulk insert if any new records
            if (newMedia.Any())
            {
                dbContext.Set<ObservationMedia>().AddRange(newMedia);
                await dbContext.SaveChangesAsync();
            }

            return newMedia.Count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Bulk implementation of ObservationMedia parsing.
        /// Uses three-phase pattern: parse to memory, bulk query, bulk insert.
        /// </summary>
        /// <param name="sectionEl">The XElement for the parent section.</param>
        /// <param name="sectionId">The SectionID of the parent section.</param>
        /// <param name="context">Parsing context for database access.</param>
        /// <returns>List of all ObservationMedia objects (existing + newly created).</returns>
        /// <remarks>
        /// Reduces database calls from N queries + N inserts to 2 queries + 1 insert.
        /// Approximately 10-100x faster than legacy implementation for documents with multiple media elements.
        /// </remarks>
        /// <seealso cref="parseObservationMediaToMemory"/>
        /// <seealso cref="getExistingObservationMediaKeysAsync"/>
        /// <seealso cref="bulkCreateObservationMediaAsync"/>
        /// <seealso cref="ObservationMedia"/>
        private async Task<List<ObservationMedia>> parseObservationMediaAsync_bulkCalls(
            XElement sectionEl,
            int sectionId,
            SplParseContext context)
        {
            #region implementation
            // Validate required input parameters
            if (sectionEl == null || sectionId <= 0 || context?.ServiceProvider == null)
                return new List<ObservationMedia>();

            var dbContext = context.GetDbContext();

            // PHASE 1: Parse to DTOs (0 DB calls)
            var dtos = parseObservationMediaToMemory(sectionEl);

            if (!dtos.Any())
                return new List<ObservationMedia>();

            // PHASE 2: Bulk query existing (1 query)
            var existingKeys = await getExistingObservationMediaKeysAsync(dbContext, sectionId);

            // PHASE 3: Bulk insert missing (1 insert)
            await bulkCreateObservationMediaAsync(dbContext, sectionId, context.Document?.DocumentID, dtos, existingKeys);

            // Return all media for this section (existing + newly created)
            var allMedia = await dbContext.Set<ObservationMedia>()
                .Where(m => m.SectionID == sectionId)
                .ToListAsync();

            return allMedia;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PHASE 1: Parse RenderedMedia elements to DTOs (0 DB calls).
        /// Extracts all renderMultimedia references from XML into memory structures.
        /// </summary>
        /// <param name="contentBlockEl">The XElement for the content block.</param>
        /// <returns>List of RenderedMediaDto objects with parsed data.</returns>
        /// <remarks>
        /// This method performs no database operations, only XML parsing.
        /// Handles both block-level renderMultimedia elements and inline descendants.
        /// Assigns sequence numbers for proper ordering within content.
        /// </remarks>
        /// <seealso cref="RenderedMediaDto"/>
        /// <seealso cref="XElementExtensions"/>
        private List<RenderedMediaDto> parseRenderedMediaToMemory(XElement contentBlockEl)
        {
            #region implementation
            var dtos = new List<RenderedMediaDto>();

            // Find all <renderMultimedia> elements
            // If the block is the element itself, use it directly; otherwise find descendants
            var renderedElements = contentBlockEl.Name.LocalName.Equals(sc.E.RenderMultimedia, StringComparison.OrdinalIgnoreCase)
                ? new List<XElement> { contentBlockEl }
                : contentBlockEl.Descendants(ns + sc.E.RenderMultimedia).ToList();

            // Extract referenced object IDs with sequence numbers
            int seqNum = 1;
            foreach (var el in renderedElements)
            {
                var referencedObjectId = el.Attribute(sc.A.ReferencedObject)?.Value;

                if (!string.IsNullOrWhiteSpace(referencedObjectId))
                {
                    dtos.Add(new RenderedMediaDto
                    {
                        ReferencedObjectId = referencedObjectId,
                        SequenceInContent = seqNum
                    });
                }

                seqNum++;
            }

            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PHASE 2: Bulk query existing RenderedMedia and build ObservationMedia lookup (2 queries).
        /// Retrieves existing links and creates MediaID to ObservationMediaID mapping.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="sectionTextContentId">The section text content ID to query for.</param>
        /// <param name="documentId">The document ID to query for.</param>
        /// <param name="dtos">List of parsed DTOs to determine which ObservationMedia to query.</param>
        /// <returns>Tuple containing existing keys HashSet and MediaID to ObservationMediaID lookup dictionary.</returns>
        /// <remarks>
        /// First query retrieves existing RenderedMedia to avoid duplicates.
        /// Second query builds lookup from MediaID (from XML) to ObservationMediaID (database key).
        /// Both queries use nullable foreign key handling with HasValue checks.
        /// </remarks>
        /// <seealso cref="RenderedMedia"/>
        /// <seealso cref="ObservationMedia"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<(HashSet<(int, int)>, Dictionary<string, int>)> getExistingRenderedMediaKeysAsync(
            ApplicationDbContext dbContext,
            int sectionTextContentId,
            int documentId,
            List<RenderedMediaDto> dtos)
        {
            #region implementation
            // Query existing RenderedMedia for this content block
            var existing = await dbContext.Set<RenderedMedia>()
                .Where(rm => rm.SectionTextContentID == sectionTextContentId)
                .Select(rm => new { rm.ObservationMediaID, rm.SequenceInContent })
                .ToListAsync();

            // Create composite key HashSet (ObservationMediaID, SequenceInContent)
            // Filter nullable ObservationMediaID using HasValue before selecting
            var existingKeys = new HashSet<(int, int)>(
                existing
                    .Where(e => e.ObservationMediaID.HasValue && e.SequenceInContent.HasValue)
                    .Select(e => (e.ObservationMediaID!.Value, e.SequenceInContent!.Value))
            );

            // Build lookup from MediaID to ObservationMediaID
            var distinctMediaIds = dtos.Select(d => d.ReferencedObjectId).Distinct().ToList();

            // Query ObservationMedia to get ObservationMediaID for each MediaID reference
            // Filter by documentId to ensure we're getting the right media for this document
            var observationMediaLookup = await dbContext.Set<ObservationMedia>()
                .Where(om => om.DocumentID.HasValue 
                    && om.DocumentID.Value == documentId 
                    && !string.IsNullOrWhiteSpace(om.MediaID)
                    && distinctMediaIds.Contains(om.MediaID))
                .Select(om => new { om.MediaID, om.ObservationMediaID })
                .Where(om => om.ObservationMediaID.HasValue && om.MediaID != null)
                .ToDictionaryAsync(
                    om => om.MediaID!, // Use ! to assert non-null, since we filtered out nulls
                    om => om.ObservationMediaID!.Value
                );

            return (existingKeys, observationMediaLookup);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PHASE 3: Bulk insert missing RenderedMedia (1 insert).
        /// Creates database records for all media links not already in the database.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="sectionTextContentId">The section text content ID for the links.</param>
        /// <param name="documentId">The document ID for the links.</param>
        /// <param name="isInline">Whether the media is inline or block-level.</param>
        /// <param name="dtos">List of parsed DTOs.</param>
        /// <param name="existingKeys">HashSet of existing composite keys.</param>
        /// <param name="observationMediaLookup">Dictionary mapping MediaID to ObservationMediaID.</param>
        /// <param name="context">Parsing context for logging.</param>
        /// <returns>Count of new RenderedMedia records created.</returns>
        /// <remarks>
        /// Uses AddRange for efficient bulk insert operation.
        /// Logs warnings for dangling references (media IDs not found in ObservationMedia).
        /// Only creates records for links not found in existingKeys.
        /// </remarks>
        /// <seealso cref="RenderedMedia"/>
        /// <seealso cref="RenderedMediaDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="SplParseContext"/>
        private async Task<int> bulkCreateRenderedMediaAsync(
            ApplicationDbContext dbContext,
            int sectionTextContentId,
            int documentId,
            bool isInline,
            List<RenderedMediaDto> dtos,
            HashSet<(int, int)> existingKeys,
            Dictionary<string, int> observationMediaLookup,
            SplParseContext context)
        {
            #region implementation
            var newLinks = new List<RenderedMedia>();

            // Process each DTO and create entity if not already in database
            foreach (var dto in dtos)
            {
                // Resolve ObservationMediaID from lookup dictionary
                if (!observationMediaLookup.TryGetValue(dto.ReferencedObjectId, out var observationMediaId))
                {
                    // Log warning for dangling reference (no matching ObservationMedia)
                    context?.Logger?.LogWarning(
                        "Dangling reference: <renderMultimedia referencedObject='{RefId}'> found, but no matching <observationMedia> in file {FileName}.",
                        dto.ReferencedObjectId, context.FileNameInZip);
                    continue;
                }

                // Check if this link already exists using composite key
                if (existingKeys.Contains((observationMediaId, dto.SequenceInContent)))
                    continue;

                // Create new RenderedMedia link
                newLinks.Add(new RenderedMedia
                {
                    SectionTextContentID = sectionTextContentId,
                    ObservationMediaID = observationMediaId,
                    DocumentID = documentId,
                    SequenceInContent = dto.SequenceInContent,
                    IsInline = isInline
                });
            }

            // Bulk insert if any new records
            if (newLinks.Any())
            {
                dbContext.Set<RenderedMedia>().AddRange(newLinks);
                await dbContext.SaveChangesAsync();
            }

            return newLinks.Count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Bulk implementation of RenderedMedia parsing.
        /// Uses three-phase pattern: parse to memory, bulk query, bulk insert.
        /// </summary>
        /// <param name="contentBlockEl">The XElement for the content block.</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="isInline">Whether the media is inline or block-level.</param>
        /// <returns>Count of RenderedMedia entities created.</returns>
        /// <remarks>
        /// Reduces database calls from N lookups + N inserts to 2 queries + 1 insert.
        /// Approximately 10-100x faster than legacy implementation for content blocks with multiple media references.
        /// </remarks>
        /// <seealso cref="parseRenderedMediaToMemory"/>
        /// <seealso cref="getExistingRenderedMediaKeysAsync"/>
        /// <seealso cref="bulkCreateRenderedMediaAsync"/>
        /// <seealso cref="RenderedMedia"/>
        private async Task<int> parseRenderedMediaAsync_bulkCalls(
            XElement contentBlockEl,
            int sectionTextContentId,
            SplParseContext context,
            bool isInline)
        {
            #region implementation
            // Validate context requirements
            if (context?.ServiceProvider == null || context?.CurrentSection == null)
                return 0;

            int documentId = context.Document?.DocumentID ?? 0;
            var dbContext = context.GetDbContext();

            // PHASE 1: Parse to DTOs (0 DB calls)
            var dtos = parseRenderedMediaToMemory(contentBlockEl);

            if (!dtos.Any())
                return 0;

            // PHASE 2: Bulk query existing + build lookup (2 queries)
            var (existingKeys, observationMediaLookup) =
                await getExistingRenderedMediaKeysAsync(dbContext, sectionTextContentId, documentId, dtos);

            // PHASE 3: Bulk insert missing (1 insert)
            var createdCount = await bulkCreateRenderedMediaAsync(
                dbContext, sectionTextContentId, documentId, isInline,
                dtos, existingKeys, observationMediaLookup, context);

            return createdCount;
            #endregion
        }

        #endregion
    }
}