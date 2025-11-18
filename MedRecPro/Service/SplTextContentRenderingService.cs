using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Interface for preparing text content data for rendering by handling formatting,
    /// content type processing, and attribute generation logic.
    /// </summary>
    /// <seealso cref="SectionTextContentDto"/>
    /// <seealso cref="TextContentRendering"/>
    public interface ITextContentRenderingService
    {
        /**************************************************************/
        /// <summary>
        /// Prepares a collection of text content items for optimized rendering with observation media context.
        /// Pre-computes all rendering properties and content type classifications for efficient template processing,
        /// including resolution of referenced multimedia objects from observation media.
        /// </summary>
        /// <param name="textContents">The text content items to prepare for rendering</param>
        /// <param name="observationMedia">Optional observation media for resolving multimedia references</param>
        /// <returns>A list of prepared text content rendering objects</returns>
        /// <seealso cref="TextContentRendering"/>
        /// <seealso cref="ObservationMediaDto"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <example>
        /// <code>
        /// var textContents = section.TextContents;
        /// var observationMedia = section.ObservationMedia;
        /// var renderedContents = service.PrepareTextContentForRendering(textContents, observationMedia);
        /// // renderedContents now contains pre-computed rendering data with resolved media references
        /// </code>
        /// </example>
        /// <remarks>
        /// This overload enables proper resolution of referencedObject attributes in multimedia content
        /// by providing access to the section's observation media collection.
        /// </remarks>
        List<TextContentRendering> PrepareTextContentForRendering(
            IEnumerable<SectionTextContentDto>? textContents,
            IEnumerable<ObservationMediaDto>? observationMedia = null);

        /**************************************************************/
        /// <summary>
        /// Prepares a collection of text content items for optimized rendering.
        /// Pre-computes all rendering properties and content type classifications
        /// for efficient template processing.
        /// </summary>
        /// <param name="textContents">The text content items to prepare for rendering</param>
        /// <returns>A list of prepared text content rendering objects</returns>
        /// <seealso cref="TextContentRendering"/>
        List<TextContentRendering> PrepareTextContentForRendering(IEnumerable<SectionTextContentDto>? textContents);

        /**************************************************************/
        /// <summary>
        /// Prepares a single text content item for rendering with all computed properties.
        /// </summary>
        /// <param name="textContent">The text content item to prepare</param>
        /// <returns>A prepared text content rendering object</returns>
        /// <seealso cref="TextContentRendering"/>
        TextContentRendering PrepareTextContentItemForRendering(SectionTextContentDto textContent);

        /**************************************************************/
        /// <summary>
        /// Determines the normalized content type for a text content item.
        /// </summary>
        /// <param name="textContent">The text content item to analyze</param>
        /// <returns>Normalized content type string</returns>
        string DetermineContentType(SectionTextContentDto textContent);

        /**************************************************************/
        /// <summary>
        /// Analyzes content characteristics for rendering optimization.
        /// </summary>
        /// <param name="textContent">The text content item to analyze</param>
        /// <returns>Content characteristics object</returns>
        ContentCharacteristics AnalyzeContentCharacteristics(SectionTextContentDto textContent);
    }

    /**************************************************************/
    /// <summary>
    /// Service for preparing text content data for rendering by handling formatting,
    /// content type processing, and attribute generation logic. Separates view logic from templates.
    /// </summary>
    /// <seealso cref="ITextContentRenderingService"/>
    /// <seealso cref="SectionTextContentDto"/>
    /// <seealso cref="TextContentRendering"/>
    /// <remarks>
    /// This service encapsulates all the business logic that was previously
    /// embedded in the _TextContent Razor view, promoting better separation of concerns
    /// and testability.
    /// </remarks>
    public class TextContentRenderingService : ITextContentRenderingService
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// Content type constant for paragraph rendering.
        /// </summary>
        private const string CONTENT_TYPE_PARAGRAPH = "Paragraph";

        /**************************************************************/
        /// <summary>
        /// Content type constant for multimedia rendering.
        /// </summary>
        private const string CONTENT_TYPE_MULTIMEDIA = "RenderMultiMedia";

        /**************************************************************/
        /// <summary>
        /// Content type constant for list rendering.
        /// </summary>
        private const string CONTENT_TYPE_LIST = "List";

        /**************************************************************/
        /// <summary>
        /// Content type constant for table rendering.
        /// </summary>
        private const string CONTENT_TYPE_TABLE = "Table";

        /**************************************************************/
        /// <summary>
        /// Content type constant for caption rendering.
        /// </summary>
        private const string CONTENT_TYPE_CAPTION = "Caption";

        /**************************************************************/
        /// <summary>
        /// Content type constant for footnote rendering.
        /// </summary>
        private const string CONTENT_TYPE_FOOTNOTE = "Footnote";

        /**************************************************************/
        /// <summary>
        /// Content type constant for generic content.
        /// </summary>
        private const string CONTENT_TYPE_CONTENT = "Content";

        /**************************************************************/
        /// <summary>
        /// Content type constant for text content.
        /// </summary>
        private const string CONTENT_TYPE_TEXT = "Text";

        /**************************************************************/
        /// <summary>
        /// Referenced object identifier for special multimedia handling.
        /// </summary>
        private const string REFERENCED_OBJECT = "referencedObject";

        #endregion

        private readonly ApplicationDbContext _dbContext;

        private readonly IConfiguration _configuration;

        private static bool useExpandedDebugLog;

        #region constructors

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the TextContentRenderingService class.
        /// </summary>
        /// <param name="dbContext">The database context for accessing observation media data.</param>
        /// <param name="configuration"></param>
        /// <exception cref="ArgumentNullException">Thrown if dbContext is null.</exception>
        public TextContentRenderingService(ApplicationDbContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _configuration = configuration ?? throw new ArgumentException(nameof(configuration));

            useExpandedDebugLog = _configuration.GetValue<bool>("FeatureFlags:UseEnhancedDebugging");
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Prepares a collection of text content items for optimized rendering with observation media context.
        /// Orders content by sequence number and pre-computes all rendering properties including
        /// multimedia reference resolution for efficient template processing.
        /// </summary>
        /// <param name="textContents">The text content items to prepare for rendering</param>
        /// <param name="observationMedia">Optional observation media for resolving multimedia references</param>
        /// <returns>A list of prepared text content rendering objects ordered by sequence</returns>
        /// <seealso cref="TextContentRendering"/>
        /// <seealso cref="ObservationMediaDto"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <example>
        /// <code>
        /// var textContents = section.TextContents;
        /// var observationMedia = section.ObservationMedia;
        /// var renderedContents = service.PrepareTextContentForRendering(textContents, observationMedia);
        /// foreach (var content in renderedContents)
        /// {
        ///     if (content.HasReferencedObject)
        ///     {
        ///         // Multimedia content with resolved reference
        ///         Console.WriteLine($"Referenced Object: {content.ReferencedObjectId}");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method enables proper resolution of multimedia references by providing access to
        /// the section's observation media collection. The MediaID from observation media is used
        /// to populate the ReferencedObjectId property for multimedia content.
        /// </remarks>
        public List<TextContentRendering> PrepareTextContentForRendering(
            IEnumerable<SectionTextContentDto>? textContents,
            IEnumerable<ObservationMediaDto>? observationMedia = null)
        {
            #region implementation

#if DEBUG
            if (useExpandedDebugLog)
            {

                Debug.WriteLine($"=== PrepareTextContentForRendering ===");
                Debug.WriteLine($"TextContents count: {textContents?.Count() ?? 0}");
                Debug.WriteLine($"ObservationMedia count: {observationMedia?.Count() ?? 0}");

                if (observationMedia?.Any() == true)
                {
                    foreach (var om in observationMedia)
                    {
                        Debug.WriteLine($"  ObservationMedia: ID={om.ObservationMediaID}, MediaID={om.MediaID}");
                    }
                }
            }
#endif
            if (textContents?.Any() != true && observationMedia?.Any() != true)
                return new List<TextContentRendering>();

            var renderedContents = new List<TextContentRendering>();

            if (textContents?.Any() == true)
            {
                var orderedContent = textContents.OrderBy(c => c.SequenceNumber ?? 0);

                foreach (var content in orderedContent)
                {
#if DEBUG
                    if (useExpandedDebugLog)
                    {
                        Debug.WriteLine($"\nProcessing TextContent ID={content.SectionTextContentID}");
                        Debug.WriteLine($"  ContentType: {content.ContentType}");
                        Debug.WriteLine($"  SequenceNumber: {content.SequenceNumber}");
                        Debug.WriteLine($"  RenderedMedias count: {content.RenderedMedias?.Count ?? 0}");

                        if (content.RenderedMedias?.Any() == true)
                        {
                            foreach (var rm in content.RenderedMedias)
                            {
                                Debug.WriteLine($"    RenderedMedia: ID={rm.RenderedMediaID}, ObsMediaID={rm.ObservationMediaID}");
                            }
                        }
                    }
#endif
                    var renderedContent = prepareTextContentItemForRendering(content, observationMedia);
#if DEBUG
                    if (useExpandedDebugLog)
                    {
                        Debug.WriteLine($"  Result:");
                        Debug.WriteLine($"    RenderingAction: {renderedContent.RenderingAction}");
                        Debug.WriteLine($"    HasRenderedMedia: {renderedContent.HasRenderedMedia}");
                        Debug.WriteLine($"    ReferencedObjectId: {renderedContent.ReferencedObjectId ?? "NULL"}");
                        Debug.WriteLine($"    ResolvedMediaIds count: {renderedContent.ResolvedMediaIds?.Count() ?? 0}");

                        if (renderedContent.ResolvedMediaIds?.Any() == true)
                        {
                            foreach (var mediaId in renderedContent.ResolvedMediaIds)
                            {
                                Debug.WriteLine($"      Resolved MediaID: {mediaId}");
                            }
                        }
                    }
#endif
                    renderedContents.Add(renderedContent);
                }
            }

            if (useExpandedDebugLog)
            {
                Debug.WriteLine($"=== End PrepareTextContentForRendering ===\n");
            }

            return renderedContents;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Prepares a collection of text content items for optimized rendering without media context.
        /// Provides backward compatibility for existing code that doesn't require multimedia reference resolution.
        /// </summary>
        /// <param name="textContents">The text content items to prepare for rendering</param>
        /// <returns>A list of prepared text content rendering objects ordered by sequence</returns>
        /// <seealso cref="TextContentRendering"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <remarks>
        /// This method maintains backward compatibility while internally delegating to the enhanced
        /// version that supports media context resolution.
        /// </remarks>
        public List<TextContentRendering> PrepareTextContentForRendering(IEnumerable<SectionTextContentDto>? textContents)
        {
            #region implementation

            // Delegate to enhanced version without media context
            return PrepareTextContentForRendering(textContents, null);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Prepares a single text content item for rendering with observation media context.
        /// Analyzes content characteristics and determines appropriate rendering actions
        /// including multimedia reference resolution for optimal template processing performance.
        /// </summary>
        /// <param name="textContent">The text content item to prepare</param>
        /// <param name="observationMedia">Optional observation media for resolving multimedia references</param>
        /// <returns>A prepared text content rendering object with computed properties</returns>
        /// <seealso cref="TextContentRendering"/>
        /// <seealso cref="ObservationMediaDto"/>
        /// <seealso cref="AnalyzeContentCharacteristics(SectionTextContentDto, IEnumerable{ObservationMediaDto})"/>
        /// <seealso cref="determineRenderingAction"/>
        /// <example>
        /// <code>
        /// var contentItem = new SectionTextContentDto { ContentType = "RenderMultiMedia", ContentText = "&lt;caption&gt;Sample&lt;/caption&gt;" };
        /// var mediaItems = new[] { new ObservationMediaDto { MediaID = "MEDIA_123" } };
        /// var renderedItem = service.PrepareTextContentItemForRendering(contentItem, mediaItems);
        /// // renderedItem.ReferencedObjectId will be "MEDIA_123" for multimedia content
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs comprehensive analysis including multimedia reference resolution
        /// when observation media context is provided. The MediaID from the first matching
        /// observation media item is used as the referenced object identifier.
        /// </remarks>
        public TextContentRendering PrepareTextContentItemForRendering(
            SectionTextContentDto textContent,
            IEnumerable<ObservationMediaDto>? observationMedia = null)
        {
            #region implementation

            if (textContent == null)
                throw new ArgumentNullException(nameof(textContent));

            // Analyze content characteristics with media context for reference resolution
            var characteristics = analyzeContentCharacteristics(textContent, observationMedia);

            // Determine appropriate rendering action based on content type and characteristics
            var renderingAction = determineRenderingAction(characteristics);

            return new TextContentRendering
            {
                TextContent = textContent,
                NormalizedContentType = characteristics.NormalizedContentType,
                ProcessedContentText = characteristics.ProcessedContentText,
                HasContentText = characteristics.HasContentText,
                HasLists = characteristics.HasLists,
                HasTables = characteristics.HasTables,
                HasReferencedObject = characteristics.HasReferencedObject,
                HasStructuredContent = characteristics.HasStructuredContent,
                RenderingAction = renderingAction,
                OrderedTextLists = getOrderedTextLists(textContent),
                OrderedTextTables = getOrderedTextTables(textContent),
                ReferencedObjectId = characteristics.ReferencedObjectId
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Prepares a single text content item for rendering without media context.
        /// Provides backward compatibility for existing code that doesn't require multimedia reference resolution.
        /// </summary>
        /// <param name="textContent">The text content item to prepare</param>
        /// <returns>A prepared text content rendering object with computed properties</returns>
        /// <seealso cref="TextContentRendering"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <remarks>
        /// This method maintains backward compatibility while internally delegating to the enhanced
        /// version that supports media context resolution.
        /// </remarks>
        public TextContentRendering PrepareTextContentItemForRendering(SectionTextContentDto textContent)
        {
            #region implementation

            // Delegate to enhanced version without media context
            return PrepareTextContentItemForRendering(textContent, null);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines the normalized content type for a text content item.
        /// Provides consistent content type classification for rendering decisions.
        /// </summary>
        /// <param name="textContent">The text content item to analyze</param>
        /// <returns>Normalized content type string</returns>
        /// <seealso cref="SectionTextContentDto"/>
        /// <example>
        /// <code>
        /// var contentType = service.DetermineContentType(textContent);
        /// // Returns standardized content type for rendering logic
        /// </code>
        /// </example>
        /// <remarks>
        /// Content type is trimmed and standardized for consistent processing.
        /// Returns empty string if content type is null or whitespace.
        /// </remarks>
        public string DetermineContentType(SectionTextContentDto textContent)
        {
            #region implementation

            return textContent?.ContentType?.Trim() ?? string.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Analyzes content characteristics for rendering optimization with observation media context.
        /// Examines all aspects of the content including multimedia reference resolution
        /// to determine rendering requirements and provides comprehensive characteristic analysis.
        /// </summary>
        /// <param name="textContent">The text content item to analyze</param>
        /// <param name="observationMedia">Optional observation media for resolving multimedia references</param>
        /// <returns>Content characteristics object with detailed analysis including resolved references</returns>
        /// <seealso cref="ContentCharacteristics"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="ObservationMediaDto"/>
        /// <example>
        /// <code>
        /// var characteristics = service.AnalyzeContentCharacteristics(textContent, observationMedia);
        /// if (characteristics.HasReferencedObject)
        /// {
        ///     Console.WriteLine($"Multimedia content references: {characteristics.ReferencedObjectId}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Enhanced analysis includes multimedia reference resolution when observation media context
        /// is provided. For multimedia content, the MediaID from the first available observation
        /// media item is used as the referenced object identifier.
        /// </remarks>
        public ContentCharacteristics AnalyzeContentCharacteristics(
            SectionTextContentDto textContent,
            IEnumerable<ObservationMediaDto>? observationMedia = null)
        {
            #region implementation

            // Delegate to private implementation method
            return analyzeContentCharacteristics(textContent, observationMedia);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Analyzes content characteristics for rendering optimization without media context.
        /// Provides backward compatibility for existing code that doesn't require multimedia reference resolution.
        /// </summary>
        /// <param name="textContent">The text content item to analyze</param>
        /// <returns>Content characteristics object with detailed analysis</returns>
        /// <seealso cref="ContentCharacteristics"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <remarks>
        /// This method maintains backward compatibility while internally delegating to the enhanced
        /// version that supports media context resolution.
        /// </remarks>
        public ContentCharacteristics AnalyzeContentCharacteristics(SectionTextContentDto textContent)
        {
            #region implementation

            // Delegate to enhanced version without media context
            return analyzeContentCharacteristics(textContent, null);

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Internal implementation for analyzing content characteristics with optional media context.
        /// Performs comprehensive content analysis including rendered media reference resolution
        /// by coordinating multiple resolution strategies in sequence.
        /// </summary>
        /// <param name="textContent">The text content item to analyze</param>
        /// <param name="observationMedia">Optional observation media collection for resolving media ID references</param>
        /// <returns>Content characteristics object with detailed analysis including resolved media references</returns>
        /// <seealso cref="ContentCharacteristics"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="ObservationMediaDto"/>
        /// <remarks>
        /// The method attempts reference resolution in order of preference:
        /// 1. Primary: RenderedMedia collection to lookup MediaID from ObservationMedia
        /// 2. Secondary: First available MediaID for multimedia content without RenderedMedia
        /// 3. Tertiary: Database query for disjointed RenderedMedia references
        /// 4. Final: XML parsing fallback when media definitions are in different sections
        /// </remarks>
        private ContentCharacteristics analyzeContentCharacteristics(
            SectionTextContentDto textContent,
            IEnumerable<ObservationMediaDto>? observationMedia)
        {
            #region implementation

            if (textContent == null)
                throw new ArgumentNullException(nameof(textContent));

            var contentText = textContent.ContentText?.ToString() ?? string.Empty;
            var normalizedContentType = DetermineContentType(textContent);

            // Initialize reference resolution variables
            string? referencedObjectId = null;
            bool hasReferencedObject = false;
            bool hasRenderedMedia = textContent.RenderedMedias?.Any() == true;

            // Attempt to resolve referenced object through sequential fallback strategies
            referencedObjectId = resolveReferencedObjectId(
                textContent,
                observationMedia,
                contentText,
                normalizedContentType,
                out hasReferencedObject
            );

            return buildContentCharacteristics(
                textContent,
                contentText,
                normalizedContentType,
                hasRenderedMedia,
                hasReferencedObject,
                referencedObjectId
            );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to resolve the referenced object ID through sequential fallback strategies.
        /// Coordinates multiple resolution approaches in order of preference.
        /// </summary>
        /// <param name="textContent">The text content item containing media references</param>
        /// <param name="observationMedia">Optional observation media collection for lookup</param>
        /// <param name="contentText">The normalized content text</param>
        /// <param name="normalizedContentType">The determined content type</param>
        /// <param name="hasReferencedObject">Output parameter indicating if reference was resolved</param>
        /// <returns>The resolved referenced object ID, or null if unresolved</returns>
        /// <seealso cref="resolvePrimaryMediaReference"/>
        /// <seealso cref="resolveSecondaryMediaReference"/>
        /// <seealso cref="resolveTertiaryMediaReference"/>
        /// <seealso cref="resolveFinalMediaReference"/>
        /// <remarks>
        /// Resolution strategies are attempted in descending priority order.
        /// Each strategy may be skipped based on data availability and content type.
        /// </remarks>
        private string? resolveReferencedObjectId(
            SectionTextContentDto textContent,
            IEnumerable<ObservationMediaDto>? observationMedia,
            string contentText,
            string normalizedContentType,
            out bool hasReferencedObject)
        {
            hasReferencedObject = false;
            string? referencedObjectId = null;

            // Primary resolution: Use RenderedMedia collection to lookup MediaID from ObservationMedia
            referencedObjectId = resolvePrimaryMediaReference(textContent, observationMedia);
            if (!string.IsNullOrWhiteSpace(referencedObjectId))
            {
                hasReferencedObject = true;
                return referencedObjectId;
            }

            // Secondary fallback: For multimedia content without RenderedMedia, use first available MediaID
            referencedObjectId = resolveSecondaryMediaReference(normalizedContentType, observationMedia);
            if (!string.IsNullOrWhiteSpace(referencedObjectId))
            {
                hasReferencedObject = true;
                return referencedObjectId;
            }

            // Tertiary fallback: For RenderedMedia references disjointed from observationMedia section
            referencedObjectId = resolveTertiaryMediaReference(textContent);
            if (!string.IsNullOrWhiteSpace(referencedObjectId))
            {
                hasReferencedObject = true;
                return referencedObjectId;
            }

            // Final fallback: Extract referencedObject from ContentText XML when observationMedia unavailable
            referencedObjectId = resolveFinalMediaReference(contentText);
            if (!string.IsNullOrWhiteSpace(referencedObjectId))
            {
                hasReferencedObject = true;
                return referencedObjectId;
            }

            return null;
        }

        /**************************************************************/
        /// <summary>
        /// Resolves media reference using primary strategy: RenderedMedia collection lookup.
        /// Matches RenderedMedia.ObservationMediaID to ObservationMedia to extract MediaID.
        /// </summary>
        /// <param name="textContent">The text content item containing rendered media</param>
        /// <param name="observationMedia">Observation media collection for matching</param>
        /// <returns>Resolved MediaID value, or null if primary resolution fails</returns>
        /// <seealso cref="RenderedMediaDto"/>
        /// <seealso cref="ObservationMediaDto"/>
        /// <remarks>
        /// This is the preferred resolution strategy as it leverages explicit media references.
        /// Resolution path: RenderedMedia.ObservationMediaID → ObservationMedia.ObservationMediaID → ObservationMedia.MediaID
        /// </remarks>
        private string? resolvePrimaryMediaReference(
            SectionTextContentDto textContent,
            IEnumerable<ObservationMediaDto>? observationMedia)
        {
            // Verify both RenderedMedia and ObservationMedia collections are available
            if (textContent?.RenderedMedias?.Any() != true || observationMedia?.Any() != true)
                return null;

            // Get the first rendered media reference, ordered by sequence
            var firstRenderedMedia = textContent.RenderedMedias
                .OrderBy(rm => rm.SequenceInContent ?? 0)
                .FirstOrDefault();

            if (firstRenderedMedia?.ObservationMediaID == null)
                return null;

            // Lookup the corresponding ObservationMedia by matching ObservationMediaID
            var matchingObservationMedia = observationMedia.FirstOrDefault(
                om => om.ObservationMediaID == firstRenderedMedia.ObservationMediaID
            );

            // Extract the MediaID (e.g., "MM8") which is the actual referencedObject value
            return matchingObservationMedia?.MediaID;
        }

        /**************************************************************/
        /// <summary>
        /// Resolves media reference using secondary strategy: first available ObservationMedia.
        /// Applied when multimedia content exists but RenderedMedia collection is unavailable.
        /// </summary>
        /// <param name="normalizedContentType">The determined content type</param>
        /// <param name="observationMedia">Observation media collection for extraction</param>
        /// <returns>First available MediaID from observation media, or null if unavailable</returns>
        /// <seealso cref="ObservationMediaDto"/>
        /// <remarks>
        /// This fallback applies only to multimedia content types and requires
        /// at least one item in the observationMedia collection.
        /// </remarks>
        private string? resolveSecondaryMediaReference(
            string normalizedContentType,
            IEnumerable<ObservationMediaDto>? observationMedia)
        {
            // Apply only to multimedia content with available observation media
            if (normalizedContentType != CONTENT_TYPE_MULTIMEDIA || observationMedia?.Any() != true)
                return null;

            return observationMedia.FirstOrDefault()?.MediaID;
        }

        /**************************************************************/
        /// <summary>
        /// Resolves media reference using tertiary strategy: database query for disjointed references.
        /// Handles cases where RenderedMedia references exist separately from ObservationMedia definitions.
        /// </summary>
        /// <param name="textContent">The text content item containing rendered media references</param>
        /// <returns>Resolved MediaID from database lookup, or null if resolution fails</returns>
        /// <seealso cref="ObservationMedia"/>
        /// <seealso cref="RenderedMediaDto"/>
        /// <remarks>
        /// This strategy queries the database directly when RenderedMedia references exist
        /// but are not co-located with their ObservationMedia definitions.
        /// Example: renderMultiMedia referencedObject="MM74347" on separate line.
        /// </remarks>
        private string? resolveTertiaryMediaReference(SectionTextContentDto textContent)
        {
            // Verify RenderedMedia collection exists and has items
            if (textContent?.RenderedMedias?.Any() != true)
                return null;

            var firstRenderedMediaId = textContent.RenderedMedias.FirstOrDefault()?.ObservationMediaID;
            if (firstRenderedMediaId == null)
                return null;

            // Query the database to find matching ObservationMedia by ObservationMediaID
            var matchingMedia = _dbContext.Set<ObservationMedia>()
                .FirstOrDefault(om => om.ObservationMediaID == firstRenderedMediaId);

            // Extract and return the MediaID if found
            return matchingMedia?.MediaID;
        }

        /**************************************************************/
        /// <summary>
        /// Resolves media reference using final fallback strategy: XML parsing.
        /// Extracts referencedObject attribute from ContentText when media definitions unavailable.
        /// </summary>
        /// <param name="contentText">The content text to parse for media references</param>
        /// <returns>Extracted referencedObject value from XML, or null if not found</returns>
        /// <seealso cref="extractReferencedObjectFromXml"/>
        /// <remarks>
        /// This final fallback enables rendering of multimedia content even when
        /// media definitions are in different sections or unavailable.
        /// Searches for renderMultiMedia tags with referencedObject attributes.
        /// </remarks>
        private string? resolveFinalMediaReference(string contentText)
        {
            // Check for renderMultiMedia tag with referencedObject attribute
            if (string.IsNullOrWhiteSpace(contentText) || !contentText.Contains(REFERENCED_OBJECT))
                return null;

            // Extract the referencedObject value from the XML
            return extractReferencedObjectFromXml(contentText);
        }

        /**************************************************************/
        /// <summary>
        /// Builds the ContentCharacteristics object from analyzed content properties.
        /// Consolidates all content metadata into a single characteristics container.
        /// </summary>
        /// <param name="textContent">The source text content item</param>
        /// <param name="contentText">The normalized content text</param>
        /// <param name="normalizedContentType">The determined content type</param>
        /// <param name="hasRenderedMedia">Indicates if rendered media exists</param>
        /// <param name="hasReferencedObject">Indicates if referenced object was resolved</param>
        /// <param name="referencedObjectId">The resolved referenced object ID</param>
        /// <returns>ContentCharacteristics object with all analyzed properties</returns>
        /// <seealso cref="ContentCharacteristics"/>
        /// <seealso cref="SectionTextContentDto"/>
        private ContentCharacteristics buildContentCharacteristics(
            SectionTextContentDto textContent,
            string contentText,
            string normalizedContentType,
            bool hasRenderedMedia,
            bool hasReferencedObject,
            string? referencedObjectId)
        {
            return new ContentCharacteristics
            {
                HasContentText = !string.IsNullOrWhiteSpace(contentText),
                HasLists = textContent?.TextLists?.Any() == true,
                HasTables = textContent?.TextTables?.Any() == true,
                HasRenderedMedia = hasRenderedMedia,
                HasReferencedObject = hasReferencedObject,
                ProcessedContentText = contentText,
                NormalizedContentType = normalizedContentType,
                ReferencedObjectId = referencedObjectId
            };
        }

        /**************************************************************/
        /// <summary>
        /// Resolves all rendered media references to their corresponding MediaID values.
        /// Performs lookup from RenderedMedia.ObservationMediaID to ObservationMedia.MediaID.
        /// </summary>
        /// <param name="renderedMedias">Collection of rendered media references to resolve</param>
        /// <param name="observationMedia">Collection of observation media containing MediaID values</param>
        /// <returns>List of resolved MediaID values (e.g., "MM8", "MM9") in sequence order</returns>
        /// <seealso cref="RenderedMediaDto"/>
        /// <seealso cref="ObservationMediaDto"/>
        /// <remarks>
        /// This method enables proper rendering of multiple multimedia references within a single
        /// text content block by resolving the junction table relationships to actual MediaID values.
        /// </remarks>
        private static List<string> resolveRenderedMediaReferences(
            IEnumerable<RenderedMediaDto>? renderedMedias,
            IEnumerable<ObservationMediaDto>? observationMedia)
        {
            #region implementation

#if DEBUG
            if (useExpandedDebugLog)
            {
                Debug.WriteLine($"  === resolveRenderedMediaReferences ===");
                Debug.WriteLine($"    RenderedMedias count: {renderedMedias?.Count() ?? 0}");
                Debug.WriteLine($"    ObservationMedia count: {observationMedia?.Count() ?? 0}");
            }
#endif
            var resolvedMediaIds = new List<string>();

            if (renderedMedias?.Any() != true || observationMedia?.Any() != true)
            {
#if DEBUG
                if (useExpandedDebugLog)
                {
                    Debug.WriteLine($"    Early return: Missing data");
                    Debug.WriteLine($"  === End resolveRenderedMediaReferences ===");
                }
#endif
                return resolvedMediaIds;
            }

            var orderedRenderedMedia = renderedMedias.OrderBy(rm => rm.SequenceInContent ?? 0);

            foreach (var renderedMedia in orderedRenderedMedia)
            {
#if DEBUG
                if (useExpandedDebugLog)
                {
                    Debug.WriteLine($"    Processing RenderedMedia ID={renderedMedia.RenderedMediaID}");
                    Debug.WriteLine($"      ObservationMediaID: {renderedMedia.ObservationMediaID}");
                }
#endif
                if (renderedMedia.ObservationMediaID == null)
                {
#if DEBUG
                    if (useExpandedDebugLog)
                    {
                        Debug.WriteLine($"      Skipping: ObservationMediaID is null");
                    }
#endif
                    continue;
                }

                var matchingObservationMedia = observationMedia.FirstOrDefault(
                    om => om.ObservationMediaID == renderedMedia.ObservationMediaID
                );

#if DEBUG
                if (useExpandedDebugLog)
                {
                    Debug.WriteLine($"      Matching ObservationMedia found: {matchingObservationMedia != null}");

                    if (matchingObservationMedia != null)
                    {
                        Debug.WriteLine($"        ObservationMedia.MediaID: {matchingObservationMedia.MediaID ?? "NULL"}");
                    }
                }
#endif
                if (!string.IsNullOrWhiteSpace(matchingObservationMedia?.MediaID))
                {
                    resolvedMediaIds.Add(matchingObservationMedia.MediaID);
#if DEBUG
                    if (useExpandedDebugLog)
                    {
                        Debug.WriteLine($"      Added MediaID: {matchingObservationMedia.MediaID}");
                    }
#endif
                }
                else
                {
#if DEBUG
                    if (useExpandedDebugLog)
                    {
                        Debug.WriteLine($"      Skipping: MediaID is null or whitespace");
                    }
#endif
                }
            }
#if DEBUG
            if (useExpandedDebugLog)
            {
                Debug.WriteLine($"    Total resolved: {resolvedMediaIds.Count}");
                Debug.WriteLine($"  === End resolveRenderedMediaReferences ===");
            }
#endif

            return resolvedMediaIds;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Prepares a single text content item for rendering with optional media context.
        /// Analyzes content characteristics and resolves rendered media references to MediaID values.
        /// </summary>
        /// <param name="textContent">The text content item to prepare</param>
        /// <param name="observationMedia">Optional observation media for resolving multimedia references</param>
        /// <returns>A prepared text content rendering object with computed properties and resolved media IDs</returns>
        /// <seealso cref="TextContentRendering"/>
        /// <seealso cref="SectionTextContentDto"/>
        /// <seealso cref="ObservationMediaDto"/>
        /// <remarks>
        /// This method performs the critical lookup from RenderedMedia junction table to ObservationMedia
        /// to resolve the actual MediaID values needed for renderMultiMedia referencedObject attributes.
        /// </remarks>
        private TextContentRendering prepareTextContentItemForRendering(
            SectionTextContentDto textContent,
            IEnumerable<ObservationMediaDto>? observationMedia)
        {
            #region implementation

            if (textContent == null)
                throw new ArgumentNullException(nameof(textContent));

            // Analyze content characteristics with media context for reference resolution
            var characteristics = analyzeContentCharacteristics(textContent, observationMedia);

            // Determine appropriate rendering action based on content type and characteristics
            var renderingAction = determineRenderingAction(characteristics);

            // Resolve all rendered media references to their MediaID values
            var resolvedMediaIds = resolveRenderedMediaReferences(
                textContent.RenderedMedias,
                observationMedia
            );

            return new TextContentRendering
            {
                TextContent = textContent,
                NormalizedContentType = characteristics.NormalizedContentType,
                ProcessedContentText = characteristics.ProcessedContentText,
                HasContentText = characteristics.HasContentText,
                HasLists = characteristics.HasLists,
                HasTables = characteristics.HasTables,
                HasRenderedMedia = characteristics.HasRenderedMedia,
                HasReferencedObject = characteristics.HasReferencedObject,
                HasStructuredContent = characteristics.HasStructuredContent,
                RenderingAction = renderingAction,
                OrderedTextLists = getOrderedTextLists(textContent),
                OrderedTextTables = getOrderedTextTables(textContent),
                ResolvedMediaIds = resolvedMediaIds.Any() ? resolvedMediaIds : null,
                ReferencedObjectId = characteristics.ReferencedObjectId
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines the appropriate rendering action based on content characteristics.
        /// Implements the rendering logic that was previously in the Razor view
        /// for consistent and testable content processing.
        /// </summary>
        /// <param name="characteristics">Content characteristics to analyze</param>
        /// <returns>Appropriate rendering action for the content</returns>
        /// <seealso cref="ContentCharacteristics"/>
        /// <seealso cref="TextContentRenderingAction"/>
        /// <remarks>
        /// This method encapsulates the complex conditional logic from the original view:
        /// - Content type-specific handling (Paragraph, RenderMultiMedia, List, Table, etc.)
        /// - Referenced object detection for multimedia content
        /// - Structured content prioritization
        /// - Fallback logic for unknown content types
        /// </remarks>
        private static TextContentRenderingAction determineRenderingAction(ContentCharacteristics characteristics)
        {
            #region implementation

            // Handle content with explicit content type
            if (!string.IsNullOrWhiteSpace(characteristics.NormalizedContentType))
            {
                switch (characteristics.NormalizedContentType)
                {
                    case CONTENT_TYPE_PARAGRAPH:
                        return characteristics.HasContentText
                            ? TextContentRenderingAction.RenderParagraph
                            : TextContentRenderingAction.SkipRendering;

                    case CONTENT_TYPE_MULTIMEDIA:
                        return characteristics.HasContentText ||
                           characteristics.HasReferencedObject ||
                           characteristics.HasRenderedMedia
                                ? TextContentRenderingAction.RenderMultiMedia
                                : TextContentRenderingAction.SkipRendering;

                    case CONTENT_TYPE_LIST:
                        return characteristics.HasLists
                            ? TextContentRenderingAction.RenderLists
                            : TextContentRenderingAction.SkipRendering;

                    case CONTENT_TYPE_TABLE:
                        return characteristics.HasTables
                            ? TextContentRenderingAction.RenderTables
                            : TextContentRenderingAction.SkipRendering;

                    case CONTENT_TYPE_CAPTION:
                        return characteristics.HasContentText
                            ? TextContentRenderingAction.RenderCaption
                            : TextContentRenderingAction.SkipRendering;

                    case CONTENT_TYPE_FOOTNOTE:
                        return characteristics.HasContentText
                            ? TextContentRenderingAction.RenderFootnote
                            : TextContentRenderingAction.SkipRendering;

                    case CONTENT_TYPE_CONTENT:
                    case CONTENT_TYPE_TEXT:
                    default:
                        // Handle Content/Text or unknown content types
                        return determineDefaultRenderingAction(characteristics);
                }
            }
            else
            {
                // Handle content without explicit content type
                return determineDefaultRenderingAction(characteristics);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines rendering action for default content types or content without explicit types.
        /// Implements fallback logic prioritizing structured content over plain text.
        /// </summary>
        /// <param name="characteristics">Content characteristics to analyze</param>
        /// <returns>Appropriate default rendering action</returns>
        /// <seealso cref="ContentCharacteristics"/>
        /// <seealso cref="TextContentRenderingAction"/>
        /// <remarks>
        /// Default logic prioritizes structured content (lists and tables) over plain text.
        /// If both structured content and text exist, structured content takes precedence.
        /// </remarks>
        private static TextContentRenderingAction determineDefaultRenderingAction(ContentCharacteristics characteristics)
        {
            #region implementation

            // Prioritize structured content over plain text
            if (characteristics.HasStructuredContent)
            {
                return TextContentRenderingAction.RenderStructuredContent;
            }
            else if (characteristics.HasContentText)
            {
                return TextContentRenderingAction.RenderDefaultParagraph;
            }
            else
            {
                return TextContentRenderingAction.SkipRendering;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets ordered text lists from a content item for efficient rendering.
        /// Returns null if no lists exist to avoid unnecessary processing.
        /// </summary>
        /// <param name="textContent">Content item containing potential lists</param>
        /// <returns>Ordered lists or null if none exist</returns>
        /// <seealso cref="TextListDto"/>
        /// <remarks>
        /// Lists are returned in their existing order as they should already be properly sequenced.
        /// Future enhancements could add specific ordering logic if needed.
        /// </remarks>
        private static IEnumerable<TextListDto>? getOrderedTextLists(SectionTextContentDto textContent)
        {
            #region implementation

            return textContent?.TextLists?.Any() == true
                ? textContent.TextLists
                : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets ordered text tables from a content item for efficient rendering.
        /// Returns null if no tables exist to avoid unnecessary processing.
        /// </summary>
        /// <param name="textContent">Content item containing potential tables</param>
        /// <returns>Ordered tables or null if none exist</returns>
        /// <seealso cref="TextTableDto"/>
        /// <remarks>
        /// Tables are returned in their existing order as they should already be properly sequenced.
        /// Future enhancements could add specific ordering logic if needed.
        /// </remarks>
        private static IEnumerable<TextTableDto>? getOrderedTextTables(SectionTextContentDto textContent)
        {
            #region implementation

            return textContent?.TextTables?.Any() == true
                ? textContent.TextTables
                : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the referencedObject attribute value from renderMultiMedia XML content.
        /// Parses the ContentText to find and extract the MediaID reference when observationMedia
        /// is not available during rendering.
        /// </summary>
        /// <param name="contentText">The XML content containing renderMultiMedia tags</param>
        /// <returns>The extracted referencedObject value (e.g., "MM58560"), or null if not found</returns>
        /// <seealso cref="analyzeContentCharacteristics"/>
        /// <remarks>
        /// This method enables rendering of multimedia content even when the observation media
        /// collection is in a different section. It uses simple string parsing to extract the
        /// referencedObject attribute value from the XML.
        /// </remarks>
        private static string? extractReferencedObjectFromXml(string contentText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(contentText))
                return null;

            try
            {
                // Look for referencedObject="..." pattern in the XML
                const string pattern = $"{REFERENCED_OBJECT}=";
                int startIndex = contentText.IndexOf(pattern, StringComparison.Ordinal);

                if (startIndex >= 0)
                {
                    startIndex += pattern.Length;
                    int endIndex = contentText.IndexOf('"', startIndex);

                    if (endIndex > startIndex)
                    {
                        return contentText.Substring(startIndex, endIndex - startIndex);
                    }
                }
            }
            catch
            {
                // If parsing fails, return null - the content will still render without the reference
                return null;
            }

            return null;

            #endregion
        }

        #endregion
    }
}