using MedRecPro.Models;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Interface for preparing section data for rendering by handling formatting,
    /// ordering, and attribute generation logic.
    /// </summary>
    /// <seealso cref="SectionDto"/>
    /// <seealso cref="SectionRendering"/>
    public interface ISectionRenderingService
    {
        /**************************************************************/
        /// <summary>
        /// Prepares a complete SectionRendering object with all computed properties
        /// for efficient template rendering.
        /// </summary>
        /// <param name="section">The section to prepare for rendering</param>
        /// <param name="children">Optional child sections</param>
        /// <param name="hierarchicalChildren">Optional hierarchical children</param>
        /// <param name="productRenderingService">Service for processing products within the section</param>
        /// <param name="textContentRenderingService">Service for processing text content within the section</param>
        /// <param name="isStandalone">Whether this section is standalone</param>
        /// <returns>A fully prepared SectionRendering object</returns>
        /// <seealso cref="SectionRendering"/>
        SectionRendering PrepareSectionForRendering(
            SectionDto section,
            List<SectionDto>? children = null,
            List<SectionRendering>? hierarchicalChildren = null,
            IProductRenderingService? productRenderingService = null,
            ITextContentRenderingService? textContentRenderingService = null,
            bool isStandalone = false);

        /**************************************************************/
        /// <summary>
        /// Generates the appropriate ID attribute for a section based on
        /// LinkGUID or SectionGUID.
        /// </summary>
        /// <param name="section">The section to generate ID for</param>
        /// <returns>Formatted section ID attribute</returns>
        string GenerateSectionIdAttribute(SectionDto section);

        /**************************************************************/
        /// <summary>
        /// Determines if a section has valid code data (code and code system).
        /// </summary>
        /// <param name="section">The section to check</param>
        /// <returns>True if section has valid code data</returns>
        bool HasSectionCodeData(SectionDto section);

        /**************************************************************/
        /// <summary>
        /// Gets section text content ordered by sequence number.
        /// </summary>
        /// <param name="section">The section containing text content</param>
        /// <returns>Ordered list of text content or null if none exists</returns>
        List<SectionTextContentDto>? GetOrderedTextContent(SectionDto section);

        /**************************************************************/
        /// <summary>
        /// Gets section products ordered by product ID.
        /// </summary>
        /// <param name="section">The section containing products</param>
        /// <returns>Ordered enumerable of products or null if none exists</returns>
        IEnumerable<ProductDto>? GetOrderedProducts(SectionDto section);

        /**************************************************************/
        /// <summary>
        /// Gets section observation media ordered by media ID.
        /// </summary>
        /// <param name="section">The section containing observation media</param>
        /// <returns>Ordered enumerable of observation media or null if none exists</returns>
        IEnumerable<ObservationMediaDto>? GetOrderedMedia(SectionDto section);

        /**************************************************************/
        /// <summary>
        /// Gets the appropriate section code system name, defaulting to "LOINC" if not specified.
        /// </summary>
        /// <param name="section">The section to get code system name for</param>
        /// <returns>Section code system name or "LOINC" as default</returns>
        string GetSectionCodeSystemName(SectionDto section);

        /**************************************************************/
        /// <summary>
        /// Gets section excerpt highlights ordered by highlight ID.
        /// </summary>
        /// <param name="section">The section containing excerpt highlights</param>
        /// <returns>Ordered enumerable of excerpt highlights or null if none exists</returns>
        IEnumerable<SectionExcerptHighlightDto>? GetOrderedExcerptHighlights(SectionDto section);
    }


    /**************************************************************/
    /// <summary>
    /// Service for preparing section data for rendering by handling formatting,
    /// ordering, and attribute generation logic. Separates view logic from templates.
    /// </summary>
    /// <seealso cref="ISectionRenderingService"/>
    /// <seealso cref="SectionDto"/>
    /// <seealso cref="SectionRendering"/>
    /// <remarks>
    /// This service encapsulates all the business logic that was previously
    /// embedded in Razor views, promoting better separation of concerns
    /// and testability.
    /// </remarks>
    public class SectionRenderingService : ISectionRenderingService
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// Default code system name used when section doesn't specify one.
        /// </summary>
        private const string DEFAULT_CODE_SYSTEM_NAME = "LOINC";

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Prepares a complete SectionRendering object with all computed properties
        /// for efficient template rendering. Pre-computes all formatting and ordering
        /// operations to minimize processing in the view layer.
        /// </summary>
        /// <param name="section">The section to prepare for rendering</param>
        /// <param name="children">Optional child sections</param>
        /// <param name="hierarchicalChildren">Optional hierarchical children</param>
        /// <param name="productRenderingService">Service for processing products within the section</param>
        /// <param name="textContentRenderingService">Service for processing text content within the section</param>
        /// <param name="isStandalone">Whether this section is standalone</param>
        /// <returns>A fully prepared SectionRendering object with computed properties</returns>
        /// <seealso cref="SectionRendering"/>
        /// <example>
        /// <code>
        /// var preparedSection = service.PrepareSectionForRendering(
        ///     section: sectionDto,
        ///     children: childSections,
        ///     textContentRenderingService: textContentService,
        ///     isStandalone: true
        /// );
        /// // preparedSection now has all computed properties ready for rendering
        /// </code>
        /// </example>
        public SectionRendering PrepareSectionForRendering(
            SectionDto section,
            List<SectionDto>? children = null,
            List<SectionRendering>? hierarchicalChildren = null,
            IProductRenderingService? productRenderingService = null,
            ITextContentRenderingService? textContentRenderingService = null,
            bool isStandalone = false)
        {
            #region implementation

            if (section == null)
                throw new ArgumentNullException(nameof(section));

            // Get ordered text content and observation media for reference resolution
            var orderedTextContent = GetOrderedTextContent(section);
            var orderedMedia = GetOrderedMedia(section);
            var orderedExcerptHighlights = GetOrderedExcerptHighlights(section);

            // Process text content with media context for multimedia reference resolution
            List<TextContentRendering>? renderedTextContent = null;
            bool hasRenderedTextContent = false;

            if (textContentRenderingService != null && orderedTextContent?.Any() == true)
            {
                // Pass observation media context for multimedia reference resolution
                renderedTextContent = textContentRenderingService.PrepareTextContentForRendering(
                    orderedTextContent, orderedMedia);
                hasRenderedTextContent = renderedTextContent?.Any() == true;
            }

            return new SectionRendering
            {
                Section = section,
                Children = children ?? new List<SectionDto>(),
                HierarchicalChildren = hierarchicalChildren ?? new List<SectionRendering>(),
                IsStandalone = isStandalone,

                // Pre-compute all rendering properties
                SectionIdAttribute = GenerateSectionIdAttribute(section),
                HasSectionCode = HasSectionCodeData(section),
                SectionCodeSystemName = GetSectionCodeSystemName(section),
                OrderedTextContent = orderedTextContent,
                RenderedTextContent = renderedTextContent,
                HasRenderedTextContent = hasRenderedTextContent,
                OrderedProducts = GetOrderedProducts(section),
                OrderedMedia = orderedMedia,
                OrderedExcerptHighlights = orderedExcerptHighlights,
                HasExcerptHighlights = orderedExcerptHighlights?.Any() == true,

                // Pre-compute availability flags
                HasTextContent = orderedTextContent?.Any() == true,
                HasProducts = GetOrderedProducts(section)?.Any() == true,
                HasMedia = orderedMedia?.Any() == true
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets section excerpt highlights ordered by highlight ID with cleaned content.
        /// Removes redundant namespace declarations that interfere with rendering.
        /// Returns null if no highlights exist.
        /// </summary>
        /// <param name="section">The section containing excerpt highlights</param>
        /// <returns>Ordered enumerable of excerpt highlights with cleaned content or null if none exists</returns>
        /// <seealso cref="SectionExcerptHighlightDto"/>
        /// <remarks>
        /// The cleaning process removes xmlns declarations that are redundant because they're
        /// already declared on the document root element. This prevents namespace conflicts
        /// during rendering.
        /// </remarks>
        public IEnumerable<SectionExcerptHighlightDto>? GetOrderedExcerptHighlights(SectionDto section)
        {
            #region implementation

            if (section?.ExcerptHighlights?.Any() != true)
                return null;

            // Order and clean the highlights
            var orderedHighlights = section.ExcerptHighlights
                .OrderBy(eh => eh.SectionExcerptHighlightID ?? 0)
                .ToList();

            // Clean namespace declarations from each highlight
            foreach (var highlight in orderedHighlights)
            {
                if (!string.IsNullOrWhiteSpace(highlight.HighlightText))
                {
                    // Create a cleaned copy by removing redundant namespace declarations
                    var cleanedDict = new Dictionary<string, object?>(highlight.SectionExcerptHighlight);
                    cleanedDict["HighlightText"] = CleanHighlightXml(highlight.HighlightText);

                    // Update the DTO with cleaned content
                    highlight.SectionExcerptHighlight.Clear();
                    foreach (var kvp in cleanedDict)
                    {
                        highlight.SectionExcerptHighlight[kvp.Key] = kvp.Value;
                    }
                }
            }

            return orderedHighlights;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Cleans XML content by removing redundant namespace declarations.
        /// </summary>
        /// <param name="xmlContent">The XML content to clean</param>
        /// <returns>Cleaned XML content</returns>
        /// <remarks>
        /// Removes xmlns="urn:hl7-org:v3" declarations since they're redundant when the
        /// document root already declares this namespace.
        /// </remarks>
        private static string CleanHighlightXml(string xmlContent)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(xmlContent))
                return xmlContent;

            // Remove the redundant namespace declaration
            return xmlContent.Replace(" xmlns=\"urn:hl7-org:v3\"", string.Empty);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates the appropriate ID attribute for a section with conditional logic.
        /// Now conditionally omits ID for specific section types based on business rules.
        /// </summary>
        /// <param name="section">The section to generate ID for</param>
        /// <returns>Formatted section ID attribute or empty string if should be omitted</returns>
        public string GenerateSectionIdAttribute(SectionDto section)
        {
            #region implementation

            if (section == null)
                return string.Empty;

            // FIX: Conditionally omit ID for specific section types
            if (ShouldOmitSectionId(section))
                return string.Empty;

            // Existing logic for generating ID when appropriate
            return !string.IsNullOrEmpty(section.SectionLinkGUID)
                ? section.SectionLinkGUID
                : section.SectionGUID?.ToString()?.Replace("-", "_") ?? string.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// NEW: Determines if section ID should be omitted based on business rules.
        /// Implements conditional logic for section ID generation.
        /// </summary>
        /// <param name="section">The section to check</param>
        /// <returns>True if section ID should be omitted</returns>
        private static bool ShouldOmitSectionId(SectionDto section)
        {
            #region implementation

            if (section == null)
                return true;

            // Omit if LinkGUID is missing/empty
            if (string.IsNullOrEmpty(section.SectionLinkGUID))
                return true;

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if a section has valid code data by checking for
        /// both section code and code system presence.
        /// </summary>
        /// <param name="section">The section to check</param>
        /// <returns>True if both section code and code system are present</returns>
        /// <seealso cref="SectionDto"/>
        /// <example>
        /// <code>
        /// bool hasCode = service.HasSectionCodeData(section);
        /// if (hasCode)
        /// {
        ///     // Render code elements in template
        /// }
        /// </code>
        /// </example>
        public bool HasSectionCodeData(SectionDto section)
        {
            #region implementation

            return section != null &&
                   !string.IsNullOrEmpty(section.SectionCode) &&
                   !string.IsNullOrEmpty(section.SectionCodeSystem);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets section text content ordered by sequence number for consistent display.
        /// Returns null if no text content exists.
        /// </summary>
        /// <param name="section">The section containing text content</param>
        /// <returns>Ordered list of text content or null if none exists</returns>
        /// <seealso cref="SectionTextContentDto"/>
        /// <example>
        /// <code>
        /// var textContent = service.GetOrderedTextContent(section);
        /// if (textContent != null)
        /// {
        ///     foreach (var content in textContent)
        ///     {
        ///         // Process ordered content
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Text content is ordered by SequenceNumber, with null values treated as 0.
        /// This ensures consistent display order across different rendering contexts.
        /// </remarks>
        public List<SectionTextContentDto>? GetOrderedTextContent(SectionDto section)
        {
            #region implementation

            return section?.TextContents?.Any() == true
                ? section.TextContents.OrderBy(tc => tc.SequenceNumber ?? 0).ToList()
                : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets section products ordered by product ID for consistent display.
        /// Returns null if no products exist.
        /// </summary>
        /// <param name="section">The section containing products</param>
        /// <returns>Ordered enumerable of products or null if none exists</returns>
        /// <seealso cref="ProductDto"/>
        /// <example>
        /// <code>
        /// var products = service.GetOrderedProducts(section);
        /// if (products != null)
        /// {
        ///     foreach (var product in products)
        ///     {
        ///         // Process ordered products
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Products are ordered by ProductID, with null values treated as 0.
        /// Returns IEnumerable for efficient iteration in templates.
        /// </remarks>
        public IEnumerable<ProductDto>? GetOrderedProducts(SectionDto section)
        {
            #region implementation

            return section?.Products?.Any() == true
                ? section.Products.OrderBy(p => p.ProductID ?? 0)
                : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets section observation media ordered by media ID for consistent display.
        /// Returns null if no media exists.
        /// </summary>
        /// <param name="section">The section containing observation media</param>
        /// <returns>Ordered enumerable of observation media or null if none exists</returns>
        /// <seealso cref="ObservationMediaDto"/>
        /// <example>
        /// <code>
        /// var media = service.GetOrderedMedia(section);
        /// if (media != null)
        /// {
        ///     foreach (var mediaItem in media)
        ///     {
        ///         // Process ordered media
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Media is ordered by ObservationMediaID, with null values treated as 0.
        /// Returns IEnumerable for efficient iteration in templates.
        /// </remarks>
        public IEnumerable<ObservationMediaDto>? GetOrderedMedia(SectionDto section)
        {
            #region implementation

            return section?.ObservationMedia?.Any() == true
                ? section.ObservationMedia.OrderBy(m => m.ObservationMediaID ?? 0)
                : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the appropriate section code system name, providing a default
        /// of "LOINC" when not specified by the section.
        /// </summary>
        /// <param name="section">The section to get code system name for</param>
        /// <returns>Section code system name or "LOINC" as default</returns>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="DEFAULT_CODE_SYSTEM_NAME"/>
        /// <example>
        /// <code>
        /// var codeSystemName = service.GetSectionCodeSystemName(section);
        /// // Returns section's code system name or "LOINC" if not specified
        /// </code>
        /// </example>
        public string GetSectionCodeSystemName(SectionDto section)
        {
            #region implementation

            return section?.SectionCodeSystemName ?? DEFAULT_CODE_SYSTEM_NAME;

            #endregion
        }

        #endregion
    }
}