using System.Collections.Generic;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Rendering context for text content with pre-computed properties for efficient template processing.
    /// Contains the original text content data along with all rendering-specific computations
    /// to minimize processing overhead in view templates.
    /// </summary>
    /// <seealso cref="SectionTextContentDto"/>
    public class TextContentRendering
    {
        /// <summary>
        /// The original text content data.
        /// </summary>
        /// <seealso cref="SectionTextContentDto"/>
        public required SectionTextContentDto TextContent { get; set; }

        #region Pre-computed Rendering Properties

        /// <summary>
        /// Pre-computed normalized content type for rendering decisions.
        /// Standardizes content type values for consistent template processing.
        /// </summary>
        public string NormalizedContentType { get; set; } = string.Empty;

        /// <summary>
        /// Pre-computed content text for rendering optimization.
        /// Contains the processed content text ready for output.
        /// </summary>
        public string ProcessedContentText { get; set; } = string.Empty;

        /// <summary>
        /// Pre-computed flag indicating whether this content has valid text content.
        /// </summary>
        public bool HasContentText { get; set; }

        /// <summary>
        /// Pre-computed flag indicating whether this content has list data.
        /// </summary>
        public bool HasLists { get; set; }

        /// <summary>
        /// Pre-computed flag indicating whether this content has table data.
        /// </summary>
        public bool HasTables { get; set; }

        /// <summary>
        /// Pre-computed flag indicating whether the content contains referenced objects.
        /// </summary>
        public bool HasReferencedObject { get; set; }

        /// <summary>
        /// Pre-computed flag indicating whether this content has structured content (lists or tables).
        /// </summary>
        public bool HasStructuredContent { get; set; }

        /// <summary>
        /// Pre-computed rendering action to take for this content item.
        /// Determines which template section should handle this content.
        /// </summary>
        public TextContentRenderingAction RenderingAction { get; set; }

        /// <summary>
        /// Pre-computed referenced object identifier for multimedia content rendering.
        /// Contains the resolved MediaID that should be used as the referencedObject attribute
        /// in multimedia rendering templates.
        /// </summary>
        /// <seealso cref="ObservationMediaDto.MediaID"/>
        /// <seealso cref="ContentCharacteristics.ReferencedObjectId"/>
        public string? ReferencedObjectId { get; set; }

        #endregion

        #region Ordered Collections

        /// <summary>
        /// Pre-computed and ordered text lists for efficient rendering.
        /// Null if no lists exist.
        /// </summary>
        public IEnumerable<TextListDto>? OrderedTextLists { get; set; }

        /// <summary>
        /// Pre-computed and ordered text tables for efficient rendering.
        /// Null if no tables exist.
        /// </summary>
        public IEnumerable<TextTableDto>? OrderedTextTables { get; set; }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Defines the rendering action to take for a text content item.
    /// Determines which rendering path the template should follow.
    /// </summary>
    public enum TextContentRenderingAction
    {
        /// <summary>
        /// Render as paragraph content.
        /// </summary>
        RenderParagraph,

        /// <summary>
        /// Render as multimedia content with special handling.
        /// </summary>
        RenderMultiMedia,

        /// <summary>
        /// Render as multimedia content with referenced object.
        /// </summary>
        RenderMultiMediaReferenced,

        /// <summary>
        /// Render lists only.
        /// </summary>
        RenderLists,

        /// <summary>
        /// Render tables only.
        /// </summary>
        RenderTables,

        /// <summary>
        /// Render as caption content.
        /// </summary>
        RenderCaption,

        /// <summary>
        /// Render as footnote content.
        /// </summary>
        RenderFootnote,

        /// <summary>
        /// Render structured content (lists and tables).
        /// </summary>
        RenderStructuredContent,

        /// <summary>
        /// Render as default paragraph for fallback cases.
        /// </summary>
        RenderDefaultParagraph,

        /// <summary>
        /// Skip rendering - no content to display.
        /// </summary>
        SkipRendering
    }

    /**************************************************************/
    /// <summary>
    /// Contains analysis results for content characteristics.
    /// Provides detailed information about content properties for rendering decisions.
    /// </summary>
    public class ContentCharacteristics
    {
        /// <summary>
        /// Whether the content has valid text content.
        /// </summary>
        public bool HasContentText { get; set; }

        /// <summary>
        /// Whether the content has list data.
        /// </summary>
        public bool HasLists { get; set; }

        /// <summary>
        /// Whether the content has table data.
        /// </summary>
        public bool HasTables { get; set; }

        /// <summary>
        /// Whether the content contains referenced objects.
        /// </summary>
        public bool HasReferencedObject { get; set; }

        /// <summary>
        /// Whether the content has any structured content (lists or tables).
        /// </summary>
        public bool HasStructuredContent => HasLists || HasTables;

        /// <summary>
        /// The processed content text ready for rendering.
        /// </summary>
        public string ProcessedContentText { get; set; } = string.Empty;

        /// <summary>
        /// The normalized content type.
        /// </summary>
        public string NormalizedContentType { get; set; } = string.Empty;

        /// <summary>
        /// The resolved referenced object identifier for multimedia content.
        /// Contains the MediaID from observation media when multimedia content references external objects.
        /// </summary>
        /// <seealso cref="ObservationMediaDto.MediaID"/>
        /// <seealso cref="TextContentRendering.ReferencedObjectId"/>
        public string? ReferencedObjectId { get; set; }
    }
}