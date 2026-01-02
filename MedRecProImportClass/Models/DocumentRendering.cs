
namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Context object for rendering SPL documents with pre-computed properties.
    /// Provides document data along with pre-computed rendering properties
    /// for efficient template rendering.
    /// </summary>
    /// <seealso cref="DocumentDto"/>
    public class DocumentRendering
    {
        #region core properties

        /**************************************************************/
        /// <summary>
        /// The document to be rendered.
        /// </summary>
        /// <seealso cref="DocumentDto"/>
        public required DocumentDto DocumentDto { get; set; }

        #endregion

        #region pre-computed rendering properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether document has authors.
        /// </summary>
        /// <seealso cref="DocumentAuthorDto"/>
        public bool HasRenderedAuthors { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed author renderings with child organizations and business operations
        /// for hierarchical author section rendering. Null if no authors exist.
        /// </summary>
        /// <seealso cref="AuthorRendering"/>
        /// <seealso cref="DocumentAuthorDto"/>
        public List<AuthorRendering>? RenderedAuthors { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed ID root attribute for SPL document rendering.
        /// Generated from DocumentGUID with proper formatting.
        /// </summary>
        /// <seealso cref="DocumentDto.DocumentGUID"/>
        public string IdRoot { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed set ID root attribute for SPL document rendering.
        /// Generated from SetGUID with proper formatting.
        /// </summary>
        /// <seealso cref="DocumentDto.SetGUID"/>
        public string SetIdRoot { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed effective time formatted for SPL rendering.
        /// Generated from EffectiveTime with SPL date formatting.
        /// </summary>
        /// <seealso cref="DocumentDto.EffectiveTime"/>
        public string EffectiveTimeFormatted { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed document code with fallback to default LOINC.
        /// </summary>
        /// <seealso cref="DocumentDto.DocumentCode"/>
        public string DocumentCode { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed document code system with fallback to default LOINC system.
        /// </summary>
        /// <seealso cref="DocumentDto.DocumentCodeSystem"/>
        public string DocumentCodeSystem { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed document code system name with fallback to "LOINC".
        /// </summary>
        /// <seealso cref="DocumentDto.DocumentCodeSystemName"/>
        public string DocumentCodeSystemName { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed document display name.
        /// </summary>
        /// <seealso cref="DocumentDto.DocumentDisplayName"/>
        public string DocumentDisplayName { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed document title for rendering.
        /// </summary>
        /// <seealso cref="DocumentDto.Title"/>
        public string DocumentTitle { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether document has a title.
        /// </summary>
        /// <seealso cref="DocumentDto.Title"/>
        public bool HasDocumentTitle { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed primary author organization name for fallback scenarios.
        /// </summary>
        /// <seealso cref="DocumentAuthorDto"/>
        public string PrimaryAuthorOrgName { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered authors for efficient rendering.
        /// Null if no authors exist.
        /// </summary>
        /// <seealso cref="DocumentAuthorDto"/>
        public List<DocumentAuthorDto>? OrderedAuthors { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether document has authors.
        /// </summary>
        /// <seealso cref="DocumentAuthorDto"/>
        public bool HasAuthors { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed version number with fallback to default of 1.
        /// </summary>
        /// <seealso cref="DocumentDto.VersionNumber"/>
        public int VersionNumber { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether document has valid required data.
        /// </summary>
        /// <seealso cref="DocumentDto"/>
        public bool HasValidDocument { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered structured bodies for efficient rendering.
        /// Null if no structured bodies exist.
        /// </summary>
        /// <seealso cref="StructuredBodyDto"/>
        public List<StructuredBodyDto>? OrderedStructuredBodies { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether document has structured bodies.
        /// </summary>
        /// <seealso cref="StructuredBodyDto"/>
        public bool HasStructuredBodies { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed validation error message for invalid documents.
        /// Empty string if document is valid.
        /// </summary>
        public string ValidationErrorMessage { get; set; } = string.Empty;

        #endregion

        #region convenience properties

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether document code information is available.
        /// </summary>
        public bool HasDocumentCode =>
            !string.IsNullOrWhiteSpace(DocumentCode) &&
            !string.IsNullOrWhiteSpace(DocumentCodeSystem);

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether effective time is available.
        /// </summary>
        public bool HasEffectiveTime => !string.IsNullOrWhiteSpace(EffectiveTimeFormatted);

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether primary author organization is available.
        /// </summary>
        public bool HasPrimaryAuthorOrg => !string.IsNullOrWhiteSpace(PrimaryAuthorOrgName);

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether validation errors exist.
        /// </summary>
        public bool HasValidationErrors => !string.IsNullOrWhiteSpace(ValidationErrorMessage);

        #endregion
    }
}