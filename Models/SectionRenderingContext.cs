using System.Collections.Generic;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Context object for rendering sections with hierarchical information.
    /// Provides section data along with its children for proper template rendering.
    /// </summary>
    /// <seealso cref="SectionDto"/>
    public class SectionRenderingContext
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
        /// Indicates whether this section is standalone (not part of any hierarchy).
        /// Used for rendering optimization and structure validation.
        /// </summary>
        public bool IsStandalone { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets whether this section has child sections to render.
        /// </summary>
        /// <returns>True if children exist and should be rendered</returns>
        /// <seealso cref="Children"/>
        public bool HasChildren => Children?.Any() == true;

        /**************************************************************/
        /// <summary>
        /// Gets ordered children by sequence number for consistent rendering.
        /// </summary>
        /// <returns>Children ordered by sequence number</returns>
        /// <seealso cref="SectionHierarchyDto.SequenceNumber"/>
        public List<SectionDto> GetOrderedChildren()
        {
            if (!HasChildren)
                return new List<SectionDto>();

            // Since children are already ordered by the hierarchy service,
            // return them as-is. In future versions, could add additional sorting logic.
            return Children;
        }
    }
}