using System.Collections.Generic;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Context object for rendering sections with hierarchical information.
    /// Provides section data along with its children and pre-computed rendering properties
    /// for efficient template rendering.
    /// </summary>
    /// <seealso cref="SectionDto"/>
    public class SectionRendering
    {
        /// <summary>
        /// The section to be rendered.
        /// </summary>
        /// <seealso cref="SectionDto"/>
        public required SectionDto Section { get; set; }

        /// <summary>
        /// Child sections to be rendered within this section.
        /// Empty list for leaf sections or standalone sections.
        /// </summary>
        /// <seealso cref="SectionDto"/>
        public List<SectionDto> Children { get; set; } = new();

        /// <summary>
        /// Nested hierarchical children for multi-level section structures.
        /// </summary>
        public List<SectionRendering> HierarchicalChildren { get; set; } = new List<SectionRendering>(); // N-level hierarchy

        /// <summary>
        /// Indicates whether this section is standalone (not part of any hierarchy).
        /// Used for rendering optimization and structure validation.
        /// </summary>
        public bool IsStandalone { get; set; }

        #region Pre-computed Rendering Properties

        /// <summary>
        /// Pre-computed section ID attribute for HTML rendering.
        /// Generated from SectionLinkGUID or SectionGUID with proper formatting.
        /// </summary>
        public string SectionIdAttribute { get; set; } = string.Empty;

        /// <summary>
        /// Pre-computed flag indicating whether this section has valid code data.
        /// </summary>
        public bool HasSectionCode { get; set; }

        /// <summary>
        /// Pre-computed section code system name with appropriate defaults applied.
        /// </summary>
        public string SectionCodeSystemName { get; set; } = string.Empty;

        /// <summary>
        /// Pre-computed and ordered text content for efficient rendering.
        /// Null if no text content exists.
        /// </summary>
        public List<SectionTextContentDto>? OrderedTextContent { get; set; }

        /// <summary>
        /// Pre-computed and ordered products for efficient rendering.
        /// Null if no products exist.
        /// </summary>
        public IEnumerable<ProductDto>? OrderedProducts { get; set; }

        #region enhanced product rendering properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed and enhanced product rendering contexts for efficient template rendering.
        /// Contains ProductRendering objects with all pre-computed properties instead of raw ProductDto objects.
        /// This collection corresponds to the OrderedProducts but with enhanced rendering data.
        /// Null if no products exist.
        /// </summary>
        /// <seealso cref="ProductRendering"/>
        /// <seealso cref="OrderedProducts"/>
        public List<ProductRendering>? RenderedProducts { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this section has enhanced products to render.
        /// </summary>
        /// <seealso cref="RenderedProducts"/>
        public bool HasRenderedProducts { get; set; }

        #endregion

        /// <summary>
        /// Pre-computed and ordered observation media for efficient rendering.
        /// Null if no media exists.
        /// </summary>
        public IEnumerable<ObservationMediaDto>? OrderedMedia { get; set; }

        /// <summary>
        /// Pre-computed flag indicating whether this section has text content to render.
        /// </summary>
        public bool HasTextContent { get; set; }

        /// <summary>
        /// Pre-computed flag indicating whether this section has products to render.
        /// </summary>
        public bool HasProducts { get; set; }

        /// <summary>
        /// Pre-computed flag indicating whether this section has media to render.
        /// </summary>
        public bool HasMedia { get; set; }

        #endregion

        #region Legacy Properties (for backward compatibility)

        /**************************************************************/
        /// <summary>
        /// Gets whether this section has child sections to render.
        /// </summary>
        /// <returns>True if children exist and should be rendered</returns>
        /// <seealso cref="Children"/>
        public bool HasChildren => Children?.Any() == true;

        /// <summary>
        /// Gets whether this section has hierarchical children to render.
        /// </summary>
        /// <returns>True if hierarchical children exist and should be rendered</returns>
        /// <seealso cref="HierarchicalChildren"/>
        public bool HasHierarchicalChildren => HierarchicalChildren?.Any() == true;

        /**************************************************************/
        /// <summary>
        /// Gets ordered children by sequence number for consistent rendering.
        /// </summary>
        /// <returns>Children ordered by sequence number</returns>
        /// <seealso cref="SectionHierarchyDto.SequenceNumber"/>
        /// <remarks>
        /// This method is maintained for backward compatibility.
        /// New code should prefer using the service to pre-compute ordered data.
        /// </remarks>
        public List<SectionDto> GetOrderedChildren()
        {
            if (!HasChildren)
                return new List<SectionDto>();

            // Since children are already ordered by the hierarchy service,
            // return them as-is. In future versions, could add additional sorting logic.
            return Children;
        }

        #endregion
    }
}