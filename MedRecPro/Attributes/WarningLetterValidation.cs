using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using static MedRecPro.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.


namespace MedRecPro.Models.Validation
{
    /**************************************************************/
    /// <summary>
    /// Validates that warning letter product information conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 21.2.2.1-21.2.2.6 requirements.
    /// </summary>
    /// <seealso cref="WarningLetterProductInfo"/>
    /// <seealso cref="Label"/>
    public class WarningLetterProductInfoValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the warning letter product information against SPL requirements.
        /// </summary>
        /// <param name="value">The WarningLetterProductInfo object to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var productInfo = value as WarningLetterProductInfo;

            if (productInfo == null)
            {
                return ValidationResult.Success; // Let other validators handle null cases
            }

            var errors = new List<string>();

            // 21.2.2.1 - There is a name, i.e., proprietary name of the product
            validateProprietaryName(productInfo, errors);

            // 21.2.2.2 - There is a generic medicine name
            validateGenericName(productInfo, errors);

            // 21.2.2.3, 21.2.2.4 - Form code and code system validation
            validateFormCode(productInfo, errors);

            // 21.2.2.5 - Strength amounts validation
            validateStrength(productInfo, errors);

            // 21.2.2.6 - Product item codes validation
            validateItemCodes(productInfo, errors);

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that the proprietary name is properly specified.
        /// </summary>
        /// <param name="productInfo">The product information to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        private static void validateProprietaryName(WarningLetterProductInfo productInfo, List<string> errors)
        {
            #region implementation
            // 21.2.2.1 - Proprietary name is required
            if (string.IsNullOrWhiteSpace(productInfo.ProductName))
            {
                errors.Add("Proprietary name of the product is required (SPL IG 21.2.2.1).");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that the generic medicine name is properly specified.
        /// </summary>
        /// <param name="productInfo">The product information to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        private static void validateGenericName(WarningLetterProductInfo productInfo, List<string> errors)
        {
            #region implementation
            // 21.2.2.2 - Generic medicine name is required
            if (string.IsNullOrWhiteSpace(productInfo.GenericName))
            {
                errors.Add("Generic medicine name is required (SPL IG 21.2.2.2).");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that the form code and code system are properly specified.
        /// </summary>
        /// <param name="productInfo">The product information to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        private static void validateFormCode(WarningLetterProductInfo productInfo, List<string> errors)
        {
            #region implementation
            // 21.2.2.3 - Form code is required
            if (string.IsNullOrWhiteSpace(productInfo.FormCode))
            {
                errors.Add("Form code (dosage form) is required (SPL IG 21.2.2.3).");
            }

            // 21.2.2.4 - Form code system must be FDA SPL system
            if (string.IsNullOrWhiteSpace(productInfo.FormCodeSystem))
            {
                errors.Add("Form code system is required when form code is specified.");
            }
            else if (productInfo.FormCodeSystem != "2.16.840.1.113883.3.26.1.1")
            {
                errors.Add("Form code system must be 2.16.840.1.113883.3.26.1.1 for FDA SPL compliance (SPL IG 21.2.2.4).");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that the strength text contains required strength information.
        /// </summary>
        /// <param name="productInfo">The product information to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        private static void validateStrength(WarningLetterProductInfo productInfo, List<string> errors)
        {
            #region implementation
            // 21.2.2.5 - Strength amounts are required
            if (string.IsNullOrWhiteSpace(productInfo.StrengthText))
            {
                errors.Add("Strength amounts with numerator and denominator for active ingredients are required (SPL IG 21.2.2.5).");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that the item codes text contains required product item codes.
        /// </summary>
        /// <param name="productInfo">The product information to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        private static void validateItemCodes(WarningLetterProductInfo productInfo, List<string> errors)
        {
            #region implementation
            // 21.2.2.6 - Product item codes are required
            if (string.IsNullOrWhiteSpace(productInfo.ItemCodesText))
            {
                errors.Add("One or more product item codes are required (SPL IG 21.2.2.6).");
            }
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that warning letter form code conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 21.2.2.3-21.2.2.4 requirements.
    /// </summary>
    /// <seealso cref="WarningLetterProductInfo"/>
    /// <seealso cref="Label"/>
    public class WarningLetterFormCodeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// FDA SPL code system for dosage form codes.
        /// </summary>
        private const string FdaSplCodeSystem = "2.16.840.1.113883.3.26.1.1";

        /**************************************************************/
        /// <summary>
        /// Validates the form code against SPL requirements.
        /// </summary>
        /// <param name="value">The form code value to validate.</param>
        /// <param name="validationContext">The validation context containing the WarningLetterProductInfo model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var formCode = value as string;
            var productInfo = validationContext.ObjectInstance as WarningLetterProductInfo;

            if (productInfo == null)
            {
                return new ValidationResult("WarningLetterProductInfo context is required for validation.");
            }

            // 21.2.2.3 - Form code is required
            if (string.IsNullOrWhiteSpace(formCode))
            {
                return new ValidationResult("Form code (dosage form) is required (SPL IG 21.2.2.3).");
            }

            // 21.2.2.4 - Code system is required and must be FDA SPL system
            if (string.IsNullOrWhiteSpace(productInfo.FormCodeSystem))
            {
                return new ValidationResult("Form code system is required when form code is specified.");
            }

            if (productInfo.FormCodeSystem != FdaSplCodeSystem)
            {
                return new ValidationResult($"Form code system must be {FdaSplCodeSystem} for FDA SPL compliance (SPL IG 21.2.2.4).");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that warning letter item codes conform to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 21.2.2.6-21.2.2.10 requirements.
    /// </summary>
    /// <seealso cref="WarningLetterProductInfo"/>
    /// <seealso cref="Label"/>
    public class WarningLetterItemCodesValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// NDC/NHRIC root identifier for product codes.
        /// </summary>
        private const string NdcNhricRoot = "2.16.840.1.113883.6.69";

        /**************************************************************/
        /// <summary>
        /// Validates the item codes text against SPL requirements.
        /// </summary>
        /// <param name="value">The item codes text value to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var itemCodesText = value as string;

            // 21.2.2.6 - Product item codes are required
            if (string.IsNullOrWhiteSpace(itemCodesText))
            {
                return new ValidationResult("One or more product item codes are required (SPL IG 21.2.2.6).");
            }

            // 21.2.2.7-21.2.2.10 - NDC/NHRIC validation if applicable
            var ndcValidationResult = validateNdcNhricCodes(itemCodesText);
            if (ndcValidationResult != ValidationResult.Success)
            {
                return ndcValidationResult;
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates NDC/NHRIC codes if they are present in the item codes text.
        /// </summary>
        /// <param name="itemCodesText">The item codes text to validate.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        private static ValidationResult validateNdcNhricCodes(string itemCodesText)
        {
            #region implementation
            // Look for NDC-like patterns in the text (basic pattern matching)
            // This is a simplified check as the actual implementation would need more sophisticated parsing
            var ndcPattern = @"\b\d{4,5}-\d{3,4}\b";
            var matches = Regex.Matches(itemCodesText, ndcPattern);

            foreach (Match match in matches)
            {
                var ndcCode = match.Value;
                var validationResult = validateSingleNdcCode(ndcCode);
                if (validationResult != ValidationResult.Success)
                {
                    return validationResult;
                }
            }

            return ValidationResult.Success!;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates a single NDC code against SPL requirements.
        /// </summary>
        /// <param name="ndcCode">The NDC code to validate.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        private static ValidationResult validateSingleNdcCode(string ndcCode)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(ndcCode))
            {
                return ValidationResult.Success!; // Skip empty codes
            }

            // 21.2.2.8 - Code has two segments separated by a hyphen
            var segments = ndcCode.Split('-');
            if (segments.Length != 2)
            {
                return new ValidationResult($"NDC/NHRIC product code '{ndcCode}' must have two segments separated by a hyphen (SPL IG 21.2.2.8).");
            }

            var labelerCode = segments[0];
            var productCode = segments[1];

            // 21.2.2.9 - The first segment (labeler code) is numeric
            if (!int.TryParse(labelerCode, out _))
            {
                return new ValidationResult($"NDC/NHRIC labeler code '{labelerCode}' must be numeric (SPL IG 21.2.2.9).");
            }

            // 21.2.2.10 - Segments follow the pattern of 4-4, 5-4 or 5-3
            var labelerLength = labelerCode.Length;
            var productLength = productCode.Length;

            var validPatterns = new[] { (4, 4), (5, 4), (5, 3) };
            var currentPattern = (labelerLength, productLength);

            if (!validPatterns.Contains(currentPattern))
            {
                return new ValidationResult($"NDC/NHRIC code '{ndcCode}' must follow the pattern 4-4, 5-4, or 5-3 but found {labelerLength}-{productLength} (SPL IG 21.2.2.10).");
            }

            return ValidationResult.Success!;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that warning letter dates conform to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 21.2.3.1-21.2.3.4 requirements.
    /// </summary>
    /// <seealso cref="WarningLetterDate"/>
    /// <seealso cref="Label"/>
    public class WarningLetterDateValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the warning letter date against SPL requirements.
        /// </summary>
        /// <param name="value">The DateTime value to validate.</param>
        /// <param name="validationContext">The validation context containing the WarningLetterDate model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="WarningLetterDate"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var dateValue = value as DateTime?;
            var memberName = validationContext.MemberName;

            // Determine if this is AlertIssueDate or ResolutionDate based on property name
            if (string.Equals(memberName, nameof(WarningLetterDate.AlertIssueDate), StringComparison.OrdinalIgnoreCase))
            {
                // 21.2.3.1 - Alert issue date is required (effective time low boundary)
                if (!dateValue.HasValue)
                {
                    return new ValidationResult("Warning letter alert issue date is required (SPL IG 21.2.3.1).");
                }

                // 21.2.3.3 - Alert date has day precision in YYYYMMDD format
                var validationResult = validateDatePrecision(dateValue.Value, "alert issue date", "21.2.3.3");
                if (validationResult != ValidationResult.Success)
                {
                    return validationResult;
                }
            }
            else if (string.Equals(memberName, nameof(WarningLetterDate.ResolutionDate), StringComparison.OrdinalIgnoreCase))
            {
                // 21.2.3.2 - Resolution date is optional
                if (dateValue.HasValue)
                {
                    // 21.2.3.4 - Resolution date has day precision in YYYYMMDD format
                    var validationResult = validateDatePrecision(dateValue.Value, "alert closure date", "21.2.3.4");
                    if (validationResult != ValidationResult.Success)
                    {
                        return validationResult;
                    }
                }
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that a date has the required day precision in YYYYMMDD format.
        /// </summary>
        /// <param name="dateValue">The date value to validate.</param>
        /// <param name="dateType">The type of date for error messaging.</param>
        /// <param name="splReference">The SPL IG reference for error messaging.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="WarningLetterDate"/>
        /// <seealso cref="Label"/>
        private static ValidationResult validateDatePrecision(DateTime dateValue, string dateType, string splReference)
        {
            #region implementation
            // Validate day precision in YYYYMMDD format
            var dateString = dateValue.ToString("yyyyMMdd");
            if (dateString.Length != 8)
            {
                return new ValidationResult($"Warning letter {dateType} must have day precision in YYYYMMDD format (SPL IG {splReference}).");
            }

            // Validate the date is reasonable (not too far in the past or future)
            var currentDate = DateTime.Today;
            var minDate = currentDate.AddYears(-50);
            var maxDate = currentDate.AddYears(10);

            if (dateValue < minDate || dateValue > maxDate)
            {
                return new ValidationResult($"Warning letter {dateType} must be within a reasonable range.");
            }

            return ValidationResult.Success!;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that warning letter date relationships are consistent according to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 21.2.3.5 requirements.
    /// </summary>
    /// <seealso cref="WarningLetterDate"/>
    /// <seealso cref="Label"/>
    public class WarningLetterDateConsistencyValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the consistency of warning letter dates against SPL requirements.
        /// </summary>
        /// <param name="value">The WarningLetterDate object to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="WarningLetterDate"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var warningLetterDate = value as WarningLetterDate;

            if (warningLetterDate == null)
            {
                return ValidationResult.Success; // Let other validators handle null cases
            }

            var errors = new List<string>();

            // Validate date relationships
            validateDateRelationships(warningLetterDate, errors);

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the chronological relationships between warning letter dates.
        /// </summary>
        /// <param name="warningLetterDate">The warning letter date to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="WarningLetterDate"/>
        /// <seealso cref="Label"/>
        private static void validateDateRelationships(WarningLetterDate warningLetterDate, List<string> errors)
        {
            #region implementation
            var alertIssueDate = warningLetterDate.AlertIssueDate;
            var resolutionDate = warningLetterDate.ResolutionDate;

            // If both dates are present, ensure resolution date is not before alert issue date
            if (alertIssueDate.HasValue && resolutionDate.HasValue)
            {
                if (resolutionDate.Value < alertIssueDate.Value)
                {
                    errors.Add("Warning letter resolution date cannot be earlier than the alert issue date.");
                }

                // Ensure dates are not the same (resolution should be after issue)
                if (resolutionDate.Value.Date == alertIssueDate.Value.Date)
                {
                    errors.Add("Warning letter resolution date should be after the alert issue date.");
                }
            }

            // 21.2.3.5 - Alert date should be before reporting end date (current date as proxy)
            if (alertIssueDate.HasValue && alertIssueDate.Value.Date > DateTime.Today)
            {
                errors.Add("Warning letter alert date must be before the current reporting date (SPL IG 21.2.3.5).");
            }
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that all warning letter product information properties are consistent with each other according to SPL Implementation Guide requirements.
    /// Implements comprehensive validation across SPL Implementation Guide Section 21.2.2 requirements.
    /// </summary>
    /// <seealso cref="WarningLetterProductInfo"/>
    /// <seealso cref="Label"/>
    public class WarningLetterProductInfoConsistencyValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the overall consistency of warning letter product information properties against SPL requirements.
        /// </summary>
        /// <param name="value">The WarningLetterProductInfo object to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var productInfo = value as WarningLetterProductInfo;

            if (productInfo == null)
            {
                return ValidationResult.Success; // Let other validators handle null cases
            }

            var errors = new List<string>();

            // Validate form code consistency
            validateFormCodeConsistency(productInfo, errors);

            // Validate entity relationships
            validateEntityRelationships(productInfo, errors);

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates consistency between form code, code system, and display name.
        /// </summary>
        /// <param name="productInfo">The product information to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        private static void validateFormCodeConsistency(WarningLetterProductInfo productInfo, List<string> errors)
        {
            #region implementation
            var hasFormCode = !string.IsNullOrWhiteSpace(productInfo.FormCode);
            var hasFormCodeSystem = !string.IsNullOrWhiteSpace(productInfo.FormCodeSystem);
            var hasFormDisplayName = !string.IsNullOrWhiteSpace(productInfo.FormDisplayName);

            // All form properties should be specified together or none at all
            if (hasFormCode && !hasFormCodeSystem)
            {
                errors.Add("Form code system is required when form code is specified.");
            }

            if (hasFormCode && !hasFormDisplayName)
            {
                errors.Add("Form display name is recommended when form code is specified.");
            }

            // Validate code system value if specified
            if (hasFormCodeSystem && productInfo.FormCodeSystem != "2.16.840.1.113883.3.26.1.1")
            {
                errors.Add("Form code system must be 2.16.840.1.113883.3.26.1.1 for FDA SPL compliance.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates entity relationships and foreign key consistency.
        /// </summary>
        /// <param name="productInfo">The product information to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="WarningLetterProductInfo"/>
        /// <seealso cref="Label"/>
        private static void validateEntityRelationships(WarningLetterProductInfo productInfo, List<string> errors)
        {
            #region implementation
            // Primary key validation
            if (productInfo.WarningLetterProductInfoID.HasValue && productInfo.WarningLetterProductInfoID.Value <= 0)
            {
                errors.Add("Warning letter product info ID must be a positive integer when specified.");
            }

            // Foreign key validation
            if (productInfo.SectionID.HasValue && productInfo.SectionID.Value <= 0)
            {
                errors.Add("Section ID must be a positive integer when specified.");
            }
            #endregion
        }
        #endregion
    }
}
