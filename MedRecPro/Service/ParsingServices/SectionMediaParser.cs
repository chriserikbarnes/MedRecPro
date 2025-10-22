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
    /// Specialized parser for handling media-related elements within SPL sections.
    /// Processes ObservationMedia and RenderedMedia elements while maintaining
    /// relationships between content and multimedia references.
    /// </summary>
    /// <remarks>
    /// This parser handles the extraction and database persistence of multimedia
    /// content referenced within SPL sections, including image metadata and
    /// rendering instructions for inline and block-level media.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="ObservationMedia"/>
    /// <seealso cref="RenderedMedia"/>
    /// <seealso cref="SplParseContext"/>
    public class SectionMediaParser : ISplSectionParser
    {
        #region Implementation
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
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
        /// <returns>A SplParseResult indicating the success status and media elements created.</returns>
        /// <remarks>
        /// This method is typically called as part of a larger section parsing operation
        /// and focuses specifically on multimedia content extraction and persistence.
        /// </remarks>
        /// <seealso cref="ParseObservationMediaAsync"/>
        /// <seealso cref="ParseRenderedMediaAsync"/>
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
            var mediaList = new List<ObservationMedia>();

            // Validate required input parameters to prevent null reference exceptions
            if (sectionEl == null || sectionId <= 0)
                return mediaList;

            // Validate required context dependencies to ensure proper service resolution
            if (context?.ServiceProvider == null)
                return mediaList;

            // Get database context and repository for ObservationMedia operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
        /// Finds or creates RenderedMedia records for all [renderMultimedia] tags within a given content block.
        /// This method links a SectionTextContent entry to its corresponding ObservationMedia entry.
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
    }
}