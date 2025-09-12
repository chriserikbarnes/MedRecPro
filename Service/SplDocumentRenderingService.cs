using MedRecPro.Models;
using MedRecPro.Helpers;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Interface for preparing SPL document data for rendering by handling formatting,
    /// ordering, and attribute generation logic.
    /// </summary>
    /// <seealso cref="DocumentDto"/>
    /// <seealso cref="DocumentRendering"/>
    public interface IDocumentRenderingService
    {
        #region core methods

        /**************************************************************/
        /// <summary>
        /// Prepares a complete DocumentRendering object with all computed properties
        /// for efficient template rendering.
        /// </summary>
        /// <param name="documentDto">The document to prepare for rendering</param>
        /// <returns>A fully prepared DocumentRendering object</returns>
        /// <seealso cref="DocumentRendering"/>
        /// <seealso cref="DocumentDto"/>
        DocumentRendering PrepareForRendering(DocumentDto documentDto);

        /**************************************************************/
        /// <summary>
        /// Generates appropriate display attributes for document root ID.
        /// </summary>
        /// <param name="documentDto">The document to generate attributes for</param>
        /// <returns>Formatted ID root attribute string</returns>
        /// <seealso cref="DocumentDto"/>
        string GenerateIdRootAttribute(DocumentDto documentDto);

        /**************************************************************/
        /// <summary>
        /// Determines if document has valid data for SPL generation.
        /// </summary>
        /// <param name="documentDto">The document to validate</param>
        /// <returns>True if document has valid data</returns>
        /// <seealso cref="DocumentDto"/>
        bool HasValidDocument(DocumentDto documentDto);

        /**************************************************************/
        /// <summary>
        /// Gets document authors ordered by business rules.
        /// </summary>
        /// <param name="documentDto">The document containing authors</param>
        /// <returns>Ordered list of authors or null if none exists</returns>
        /// <seealso cref="DocumentAuthorDto"/>
        List<DocumentAuthorDto>? GetOrderedAuthors(DocumentDto documentDto);

        /**************************************************************/
        /// <summary>
        /// Gets structured bodies ordered by business rules.
        /// </summary>
        /// <param name="documentDto">The document containing structured bodies</param>
        /// <returns>Ordered list of structured bodies or null if none exists</returns>
        /// <seealso cref="StructuredBodyDto"/>
        List<StructuredBodyDto>? GetOrderedStructuredBodies(DocumentDto documentDto);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Service for preparing SPL document data for rendering by handling formatting,
    /// ordering, and attribute generation logic. Separates view logic from templates.
    /// </summary>
    /// <seealso cref="IDocumentRenderingService"/>
    /// <seealso cref="DocumentDto"/>
    /// <seealso cref="DocumentRendering"/>
    /// <remarks>
    /// This service encapsulates all business logic that was previously
    /// embedded in Razor views, promoting better separation of concerns
    /// and testability.
    /// </remarks>
    public class DocumentRenderingService : IDocumentRenderingService
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// Default values used when document doesn't specify required data.
        /// </summary>
        private const string DEFAULT_DOC_CODE = "LOINC";
        private const string DEFAULT_DOC_CODE_SYSTEM = "2.16.840.1.113883.6.1";
        private const string DEFAULT_DOC_CODE_SYSTEM_NAME = "LOINC";
        private const int DEFAULT_VERSION_NUMBER = 1;

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Prepares a complete DocumentRendering object with all computed properties
        /// for efficient template rendering. Pre-computes all formatting and ordering
        /// operations to minimize processing in the view layer.
        /// </summary>
        /// <param name="documentDto">The document to prepare for rendering</param>
        /// <returns>A fully prepared DocumentRendering object with computed properties</returns>
        /// <seealso cref="DocumentRendering"/>
        /// <seealso cref="DocumentDto"/>
        /// <example>
        /// <code>
        /// var preparedDocument = service.PrepareForRendering(documentDto);
        /// // preparedDocument now has all computed properties ready for rendering
        /// </code>
        /// </example>
        public DocumentRendering PrepareForRendering(DocumentDto documentDto)
        {
            #region implementation

            if (documentDto == null)
                throw new ArgumentNullException(nameof(documentDto));

            return new DocumentRendering
            {
                DocumentDto = documentDto,

                // Pre-compute all rendering properties
                IdRoot = GenerateIdRootAttribute(documentDto),
                SetIdRoot = generateSetIdRootAttribute(documentDto),
                EffectiveTimeFormatted = formatEffectiveTime(documentDto),

                // Pre-compute document code properties with defaults
                DocumentCode = documentDto.DocumentCode ?? DEFAULT_DOC_CODE,
                DocumentCodeSystem = documentDto.DocumentCodeSystem ?? DEFAULT_DOC_CODE_SYSTEM,
                DocumentCodeSystemName = documentDto.DocumentCodeSystemName ?? DEFAULT_DOC_CODE_SYSTEM_NAME,
                DocumentDisplayName = documentDto.DocumentDisplayName ?? string.Empty,

                // Pre-compute document title handling
                DocumentTitle = documentDto.Title ?? string.Empty,
                HasDocumentTitle = !string.IsNullOrWhiteSpace(documentDto.Title),

                // Pre-compute author information
                PrimaryAuthorOrgName = getPrimaryAuthorOrganizationName(documentDto),
                OrderedAuthors = GetOrderedAuthors(documentDto),
                HasAuthors = documentDto.DocumentAuthors?.Any() == true,
                HasRenderedAuthors = documentDto.RenderedAuthors?.Any() == true,
                RenderedAuthors = documentDto.RenderedAuthors,

                // Pre-compute version handling
                VersionNumber = documentDto.VersionNumber ?? DEFAULT_VERSION_NUMBER,

                // Pre-compute validation flags
                HasValidDocument = HasValidDocument(documentDto),

                // Pre-compute structured bodies
                OrderedStructuredBodies = GetOrderedStructuredBodies(documentDto),
                HasStructuredBodies = documentDto.StructuredBodies?.Any() == true,

                // Pre-compute error messaging
                ValidationErrorMessage = generateValidationErrorMessage(documentDto)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates appropriate display attributes for document root ID based on
        /// business rules and formatting requirements.
        /// </summary>
        /// <param name="documentDto">The document to generate attributes for</param>
        /// <returns>Formatted ID root attribute string with appropriate fallbacks</returns>
        /// <seealso cref="DocumentDto"/>
        /// <example>
        /// <code>
        /// var idRoot = service.GenerateIdRootAttribute(documentDto);
        /// // Returns: formatted GUID string or empty string
        /// </code>
        /// </example>
        public string GenerateIdRootAttribute(DocumentDto documentDto)
        {
            #region implementation

            if (documentDto?.DocumentGUID == null)
                return string.Empty;

            return SplTemplateHelpers.GuidUp(documentDto.DocumentGUID) ?? string.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if document has valid data for SPL generation.
        /// Validates required elements for successful document processing.
        /// </summary>
        /// <param name="documentDto">The document to validate</param>
        /// <returns>True if document has valid data for rendering</returns>
        /// <seealso cref="DocumentDto"/>
        public bool HasValidDocument(DocumentDto documentDto)
        {
            #region implementation

            if (documentDto == null)
                return false;

            var idRoot = GenerateIdRootAttribute(documentDto);
            var setIdRoot = generateSetIdRootAttribute(documentDto);

            return !string.IsNullOrEmpty(idRoot) &&
                   !string.IsNullOrEmpty(setIdRoot);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets document authors ordered by business rules.
        /// Applies filtering and ordering logic for consistent author rendering.
        /// </summary>
        /// <param name="documentDto">The document containing authors</param>
        /// <returns>Ordered list of authors or null if none exists</returns>
        /// <seealso cref="DocumentAuthorDto"/>
        public List<DocumentAuthorDto>? GetOrderedAuthors(DocumentDto documentDto)
        {
            #region implementation

            var hasAuthors = documentDto?.DocumentAuthors?.Any() == true;

            if (!hasAuthors)
                return null;

            return documentDto.DocumentAuthors
                .Where(a => a?.Organization?.OrganizationName != null)
                .OrderBy(a => a.DocumentAuthorID ?? 0)
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets structured bodies ordered by business rules.
        /// Applies ordering logic for consistent structured body rendering.
        /// </summary>
        /// <param name="documentDto">The document containing structured bodies</param>
        /// <returns>Ordered list of structured bodies or null if none exists</returns>
        /// <seealso cref="StructuredBodyDto"/>
        public List<StructuredBodyDto>? GetOrderedStructuredBodies(DocumentDto documentDto)
        {
            #region implementation

            var hasStructuredBodies = documentDto?.StructuredBodies?.Any() == true;

            if (!hasStructuredBodies)
                return null;

            return documentDto.StructuredBodies
                .OrderBy(sb => sb.StructuredBodyID ?? 0)
                .ToList();

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Generates set ID root attribute according to SPL formatting rules.
        /// </summary>
        /// <param name="documentDto">The document to generate set ID for</param>
        /// <returns>Formatted set ID root string</returns>
        /// <seealso cref="DocumentDto"/>
        private string generateSetIdRootAttribute(DocumentDto documentDto)
        {
            #region implementation

            if (documentDto?.SetGUID == null)
                return string.Empty;

            return SplTemplateHelpers.GuidUp(documentDto.SetGUID) ?? string.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats effective time according to SPL date formatting rules.
        /// </summary>
        /// <param name="documentDto">The document to format effective time for</param>
        /// <returns>Formatted effective time string</returns>
        /// <seealso cref="DocumentDto"/>
        private string formatEffectiveTime(DocumentDto documentDto)
        {
            #region implementation

            if (documentDto?.EffectiveTime == null)
                return string.Empty;

            return SplTemplateHelpers.ToSplDate(documentDto.EffectiveTime) ?? string.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the primary author organization name for fallback scenarios.
        /// </summary>
        /// <param name="documentDto">The document to extract primary author from</param>
        /// <returns>Primary author organization name or empty string</returns>
        /// <seealso cref="DocumentAuthorDto"/>
        private string getPrimaryAuthorOrganizationName(DocumentDto documentDto)
        {
            #region implementation

            var primaryAuthor = documentDto?.DocumentAuthors?
                .FirstOrDefault(a => a?.Organization?.OrganizationName != null);

            return primaryAuthor?.Organization?.OrganizationName ?? string.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates validation error message for invalid documents.
        /// </summary>
        /// <param name="documentDto">The document to generate error message for</param>
        /// <returns>Error message describing missing elements</returns>
        /// <seealso cref="DocumentDto"/>
        private string generateValidationErrorMessage(DocumentDto documentDto)
        {
            #region implementation

            if (HasValidDocument(documentDto))
                return string.Empty;

            var missingElements = new List<string>();

            var idRoot = GenerateIdRootAttribute(documentDto);
            var setIdRoot = generateSetIdRootAttribute(documentDto);

            if (string.IsNullOrEmpty(idRoot))
                missingElements.Add("DocumentGUID");
            if (string.IsNullOrEmpty(setIdRoot))
                missingElements.Add("SetGUID");

            return string.Join(", ", missingElements);

            #endregion
        }

        #endregion
    }
}