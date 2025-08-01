using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;

namespace MedRecPro.Models.Validation
{
    /**************************************************************/
    /// <summary>
    /// Validates that attached document properties conform to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.7.16-18.1.7.17 and 23.2.9.4-23.2.9.5 requirements.
    /// </summary>
    /// <seealso cref="AttachedDocument"/>
    /// <seealso cref="Label"/>
    public class AttachedDocumentValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the attached document properties against SPL requirements.
        /// </summary>
        /// <param name="value">The attached document to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var attachedDocument = value as AttachedDocument;

            if (attachedDocument == null)
            {
                return ValidationResult.Success; // Let other validators handle null cases
            }

            var errors = new List<string>();

            // 18.1.7.16, 23.2.9.4 - Document reference has a text element with mediaType and reference
            validateMediaTypeAndFileName(attachedDocument, errors);

            // 18.1.7.17, 23.2.9.5 - Reference value is the file name for a valid document attachment
            validateFileName(attachedDocument, errors);

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that mediaType and fileName are properly specified.
        /// </summary>
        /// <param name="attachedDocument">The attached document to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        private static void validateMediaTypeAndFileName(AttachedDocument attachedDocument, List<string> errors)
        {
            #region implementation
            // MediaType is required
            if (string.IsNullOrWhiteSpace(attachedDocument.MediaType))
            {
                errors.Add("Document media type is required (SPL IG 18.1.7.16, 23.2.9.4).");
            }

            // FileName is required
            if (string.IsNullOrWhiteSpace(attachedDocument.FileName))
            {
                errors.Add("Document file name is required (SPL IG 18.1.7.17, 23.2.9.5).");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the file name format and characters.
        /// </summary>
        /// <param name="attachedDocument">The attached document to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        private static void validateFileName(AttachedDocument attachedDocument, List<string> errors)
        {
            #region implementation
            var fileName = attachedDocument.FileName;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return; // Already validated above
            }

            // Basic file name validation - must have an extension
            if (!fileName.Contains('.'))
            {
                errors.Add("Document file name must include a file extension.");
            }

            // Check for invalid file name characters
            var invalidChars = Path.GetInvalidFileNameChars();
            if (fileName.Any(c => invalidChars.Contains(c)))
            {
                errors.Add("Document file name contains invalid characters.");
            }

            // Check for reasonable file name length
            if (fileName.Length > 255)
            {
                errors.Add("Document file name exceeds maximum length of 255 characters.");
            }
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that attached document file properties conform to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.7.18-18.1.7.19 and 23.2.9.6 requirements.
    /// </summary>
    /// <seealso cref="AttachedDocument"/>
    /// <seealso cref="Label"/>
    public class AttachedDocumentFileValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Maximum file size in bytes (1 MB).
        /// </summary>
        private const long MaxFileSizeBytes = 1024 * 1024; // 1 MB

        /// <summary>
        /// Valid media type to file extension mappings.
        /// </summary>
        private static readonly Dictionary<string, HashSet<string>> ValidMediaTypeExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            [c.PDF_MEDIA_TYPE] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" }
        };

        /**************************************************************/
        /// <summary>
        /// Validates the attached document file properties against SPL requirements.
        /// </summary>
        /// <param name="value">The attached document to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var attachedDocument = value as AttachedDocument;

            if (attachedDocument == null)
            {
                return ValidationResult.Success; // Let other validators handle null cases
            }

            var errors = new List<string>();

            // 18.1.7.19, 23.2.9.6 - File name extension matches the media type
            validateMediaTypeExtensionMatch(attachedDocument, errors);

            // Note: 18.1.7.18 - Size validation would require actual file access
            // This would typically be handled at the service layer where file content is available

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that the file extension matches the media type.
        /// </summary>
        /// <param name="attachedDocument">The attached document to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        private static void validateMediaTypeExtensionMatch(AttachedDocument attachedDocument, List<string> errors)
        {
            #region implementation
            var mediaType = attachedDocument.MediaType;
            var fileName = attachedDocument.FileName;

            if (string.IsNullOrWhiteSpace(mediaType) || string.IsNullOrWhiteSpace(fileName))
            {
                return; // Other validators will handle these cases
            }

            var fileExtension = Path.GetExtension(fileName);

            if (string.IsNullOrWhiteSpace(fileExtension))
            {
                errors.Add("Document file name must have a file extension.");
                return;
            }

            // Check if the media type has defined valid extensions
            if (ValidMediaTypeExtensions.ContainsKey(mediaType))
            {
                var validExtensions = ValidMediaTypeExtensions[mediaType];
                if (!validExtensions.Contains(fileExtension))
                {
                    var expectedExtensions = string.Join(", ", validExtensions);
                    errors.Add($"File extension '{fileExtension}' does not match media type '{mediaType}'. Expected: {expectedExtensions} (SPL IG 18.1.7.19, 23.2.9.6).");
                }
            }
            else
            {
                // For unknown media types, perform basic validation
                if (string.Equals(mediaType, c.PDF_MEDIA_TYPE, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(fileExtension, c.PDF_FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("PDF documents must have .pdf file extension (SPL IG 18.1.7.19, 23.2.9.6).");
                }
            }
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that REMS material documents conform to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 23.2.9.1-23.2.9.3 requirements.
    /// </summary>
    /// <remarks>
    /// This validation applies specifically to documents where ParentEntityType indicates REMS materials.
    /// REMS documents have additional requirements for document IDs and title references.
    /// </remarks>
    /// <seealso cref="AttachedDocument"/>
    /// <seealso cref="Label"/>
    public class AttachedDocumentREMSValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Parent entity type indicating REMS material document.
        /// </summary>
        private const string REMSMaterialEntityType = "REMSMaterial";

        /**************************************************************/
        /// <summary>
        /// Validates REMS-specific document requirements against SPL requirements.
        /// </summary>
        /// <param name="value">The attached document to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var attachedDocument = value as AttachedDocument;

            if (attachedDocument == null)
            {
                return ValidationResult.Success; // Let other validators handle null cases
            }

            // Only apply REMS validations to REMS material documents
            if (!string.Equals(attachedDocument.ParentEntityType, REMSMaterialEntityType, StringComparison.OrdinalIgnoreCase))
            {
                return ValidationResult.Success;
            }

            var errors = new List<string>();

            // 23.2.9.1 - Each reference document has an id root
            // Note: This would require additional document ID properties in the model
            // For now, we'll note that this validation should be implemented when document ID is available

            // 23.2.9.2 - Document reference has a title element with reference (title reference)
            // Note: This would require title and title reference properties in the model

            // 23.2.9.3 - Title reference value is present in the content ID of the section text
            // Note: This would require access to section text content for validation

            // Additional REMS-specific file name validation
            validateREMSFileName(attachedDocument, errors);

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates REMS document file name format and characteristics.
        /// </summary>
        /// <param name="attachedDocument">The attached document to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        private static void validateREMSFileName(AttachedDocument attachedDocument, List<string> errors)
        {
            #region implementation
            var fileName = attachedDocument.FileName;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return; // Other validators will handle this
            }

            // REMS file names often follow specific patterns - validate common patterns
            if (fileName.Length < 5) // Minimum reasonable length
            {
                errors.Add("REMS material file name appears to be too short to be valid.");
            }

            // Check for descriptive naming (should contain meaningful words)
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            if (fileNameWithoutExtension.Length < 3)
            {
                errors.Add("REMS material file name should be descriptive of the content.");
            }
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates document uniqueness requirements for attached documents according to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 23.2.9.7-23.2.9.8 requirements.
    /// </summary>
    /// <remarks>
    /// This validation ensures that document IDs and file names maintain proper uniqueness constraints
    /// as required for REMS material documents and other SPL document references.
    /// </remarks>
    /// <seealso cref="AttachedDocument"/>
    /// <seealso cref="Label"/>
    public class AttachedDocumentUniquenessValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates document uniqueness requirements against SPL requirements.
        /// </summary>
        /// <param name="value">The collection of attached documents to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            if (value is not IEnumerable<AttachedDocument> attachedDocuments)
            {
                return ValidationResult.Success; // Not applicable if not a collection
            }

            var documentsList = attachedDocuments.Where(d => d != null).ToList();
            var errors = new List<string>();

            // 23.2.9.7 - Same file name cannot occur under a different document id 
            // and the same document id cannot be used with different file name
            validateDocumentIdFileNameConsistency(documentsList, errors);

            // Additional validation for file name uniqueness within the same parent entity
            validateFileNameUniquenessWithinParent(documentsList, errors);

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates consistency between document IDs and file names.
        /// </summary>
        /// <param name="documents">The list of documents to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        private static void validateDocumentIdFileNameConsistency(List<AttachedDocument> documents, List<string> errors)
        {
            #region implementation
            // Group by file name to check for different document IDs
            var fileNameGroups = documents
                .Where(d => !string.IsNullOrWhiteSpace(d.FileName))
                .GroupBy(d => d.FileName, StringComparer.OrdinalIgnoreCase);

            foreach (var group in fileNameGroups)
            {
                var documentIds = group.Select(d => d.AttachedDocumentID).Distinct().Where(id => id.HasValue).ToList();
                if (documentIds.Count > 1)
                {
                    errors.Add($"File name '{group.Key}' cannot be used with different document IDs (SPL IG 23.2.9.7).");
                }
            }

            // Group by document ID to check for different file names
            var documentIdGroups = documents
                .Where(d => d != null 
                    && d.AttachedDocumentID != null 
                    && d.AttachedDocumentID.HasValue)
                .GroupBy(d => d.AttachedDocumentID!.Value);

            foreach (var group in documentIdGroups)
            {
                var fileNames = group.Select(d => d.FileName).Where(fn => !string.IsNullOrWhiteSpace(fn)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (fileNames.Count > 1)
                {
                    errors.Add($"Document ID '{group.Key}' cannot be used with different file names (SPL IG 23.2.9.7).");
                }
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates file name uniqueness within the same parent entity.
        /// </summary>
        /// <param name="documents">The list of documents to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        private static void validateFileNameUniquenessWithinParent(List<AttachedDocument> documents, List<string> errors)
        {
            #region implementation
            // Group by parent entity to check file name uniqueness within each parent
            var parentGroups = documents
                .Where(d => d.ParentEntityID.HasValue && !string.IsNullOrWhiteSpace(d.ParentEntityType) && !string.IsNullOrWhiteSpace(d.FileName))
                .GroupBy(d => new { d.ParentEntityType, d.ParentEntityID });

            foreach (var parentGroup in parentGroups)
            {
                var fileNames = parentGroup.Select(d => d.FileName).ToList();
                var duplicateFileNames = fileNames.GroupBy(fn => fn, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                foreach (var duplicateFileName in duplicateFileNames)
                {
                    errors.Add($"File name '{duplicateFileName}' appears multiple times within the same {parentGroup.Key.ParentEntityType}.");
                }
            }
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates parent entity relationship consistency for attached documents according to SPL Implementation Guide requirements.
    /// Ensures that ParentEntityType and ParentEntityID are properly specified and consistent.
    /// </summary>
    /// <seealso cref="AttachedDocument"/>
    /// <seealso cref="Label"/>
    public class AttachedDocumentParentEntityValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Valid parent entity types for attached documents.
        /// </summary>
        private static readonly HashSet<string> ValidParentEntityTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            c.DISCIPLINARY_ACTION_ENTITY_TYPE,
            "REMSMaterial"
        };

        /**************************************************************/
        /// <summary>
        /// Validates parent entity relationship consistency against SPL requirements.
        /// </summary>
        /// <param name="value">The attached document to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var attachedDocument = value as AttachedDocument;

            if (attachedDocument == null)
            {
                return ValidationResult.Success; // Let other validators handle null cases
            }

            var errors = new List<string>();

            // Validate parent entity type and ID consistency
            validateParentEntityConsistency(attachedDocument, errors);

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates consistency between parent entity type and ID.
        /// </summary>
        /// <param name="attachedDocument">The attached document to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="AttachedDocument"/>
        /// <seealso cref="Label"/>
        private static void validateParentEntityConsistency(AttachedDocument attachedDocument, List<string> errors)
        {
            #region implementation
            var hasParentType = !string.IsNullOrWhiteSpace(attachedDocument.ParentEntityType);
            var hasParentId = attachedDocument.ParentEntityID.HasValue;

            // Both parent type and ID should be specified together
            if (hasParentType && !hasParentId)
            {
                errors.Add("Parent entity ID is required when parent entity type is specified.");
            }

            if (!hasParentType && hasParentId)
            {
                errors.Add("Parent entity type is required when parent entity ID is specified.");
            }

            // Validate parent entity type is from approved list
            if (hasParentType 
                && attachedDocument != null
                && !string.IsNullOrWhiteSpace(attachedDocument.ParentEntityType)
                && !ValidParentEntityTypes.Contains(attachedDocument.ParentEntityType))
            {
                var validTypes = string.Join(", ", ValidParentEntityTypes);
                errors.Add($"Parent entity type '{attachedDocument.ParentEntityType}' is not valid. Must be one of: {validTypes}.");
            }

            // Validate parent entity ID is positive
            if (hasParentId
                && attachedDocument != null
                && !string.IsNullOrWhiteSpace(attachedDocument.ParentEntityType)
                && attachedDocument.ParentEntityID <= 0)
            {
                errors.Add("Parent entity ID must be a positive integer.");
            }
            #endregion
        }
        #endregion
    }
}