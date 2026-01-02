using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using static MedRecProImportClass.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecProImportClass.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecProImportClass.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.


namespace MedRecProImportClass.Models.Validation
{
    /**************************************************************/
    /// <summary>
    /// Validates that the substance specification code conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 19.2.3.8-19.2.3.9 requirements.
    /// </summary>
    /// <seealso cref="SubstanceSpecification"/>
    /// <seealso cref="Label"/>
    public class SubstanceSpecificationCodeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// FDA SPL code system for substance specification codes.
        /// </summary>
        private const string FdaSplCodeSystem = "2.16.840.1.113883.3.149";

        /// <summary>
        /// Required prefix for substance specification codes.
        /// </summary>
        private const string RequiredPrefix = "40-CFR-";

        /**************************************************************/
        /// <summary>
        /// Validates the substance specification code against SPL requirements.
        /// </summary>
        /// <param name="value">The specification code value to validate.</param>
        /// <param name="validationContext">The validation context containing the SubstanceSpecification model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="SubstanceSpecification"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var specCode = value as string;
            var substanceSpec = validationContext.ObjectInstance as SubstanceSpecification;

            if (substanceSpec == null)
            {
                return new ValidationResult("SubstanceSpecification context is required for validation.");
            }

            // 19.2.3.8 - The substanceSpecification code is formed by using the fixed prefix "40-CFR-" followed by the section number
            if (string.IsNullOrWhiteSpace(specCode))
            {
                return new ValidationResult("Substance specification code is required (SPL IG 19.2.3.8).");
            }

            if (!specCode.StartsWith(RequiredPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult($"Substance specification code must start with '{RequiredPrefix}' followed by the section number (SPL IG 19.2.3.8).");
            }

            // 19.2.3.9 - Code system is 2.16.840.1.113883.3.149
            if (string.IsNullOrWhiteSpace(substanceSpec.SpecCodeSystem))
            {
                return new ValidationResult("Specification code system is required when specification code is specified.");
            }

            if (substanceSpec.SpecCodeSystem != FdaSplCodeSystem)
            {
                return new ValidationResult($"Specification code system must be {FdaSplCodeSystem} for FDA SPL compliance (SPL IG 19.2.3.9).");
            }

            // Validate section number format after prefix
            var sectionNumber = specCode.Substring(RequiredPrefix.Length);
            if (string.IsNullOrWhiteSpace(sectionNumber))
            {
                return new ValidationResult("Substance specification code must include a section number after the '40-CFR-' prefix (SPL IG 19.2.3.8).");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the enforcement analytical method code conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 19.2.3.10-19.2.3.12 requirements.
    /// </summary>
    /// <seealso cref="SubstanceSpecification"/>
    /// <seealso cref="Label"/>
    public class EnforcementMethodCodeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the enforcement analytical method code against SPL requirements.
        /// </summary>
        /// <param name="value">The method code value to validate.</param>
        /// <param name="validationContext">The validation context containing the SubstanceSpecification model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="SubstanceSpecification"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var methodCode = value as string;
            var substanceSpec = validationContext.ObjectInstance as SubstanceSpecification;

            if (substanceSpec == null)
            {
                return new ValidationResult("SubstanceSpecification context is required for validation.");
            }

            // 19.2.3.10 - There is a code (Enforcement Analytical Method)
            if (string.IsNullOrWhiteSpace(methodCode))
            {
                return new ValidationResult("Enforcement Analytical Method code is required (SPL IG 19.2.3.10).");
            }

            // Code system is required when method code is specified
            if (string.IsNullOrWhiteSpace(substanceSpec.EnforcementMethodCodeSystem))
            {
                return new ValidationResult("Enforcement method code system is required when method code is specified.");
            }

            // 19.2.3.11 - Code comes from the Enforcement Analytical Method list
            // Note: Full validation would require access to the actual method list
            // This validates the basic format requirements

            // 19.2.3.12 - Display name matches the code
            if (!string.IsNullOrWhiteSpace(substanceSpec.EnforcementMethodDisplayName))
            {
                // Basic validation that display name is present when code is present
                // Full validation would require mapping code to expected display name
                if (string.IsNullOrWhiteSpace(substanceSpec.EnforcementMethodDisplayName.Trim()))
                {
                    return new ValidationResult("Enforcement method display name must be meaningful when specified (SPL IG 19.2.3.12).");
                }
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that analyte relationships conform to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 19.2.3.13-19.2.3.15 requirements.
    /// </summary>
    /// <seealso cref="Analyte"/>
    /// <seealso cref="Label"/>
    public class AnalyteValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates analyte properties against SPL requirements.
        /// </summary>
        /// <param name="value">The analyte object to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="Analyte"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var analyte = value as Analyte;

            if (analyte == null)
            {
                return ValidationResult.Success; // Let other validators handle null cases
            }

            var errors = new List<string>();

            // Validate required relationships
            validateAnalyteRelationships(analyte, errors);

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that analyte has proper relationship to substance specification and analyte substance.
        /// </summary>
        /// <param name="analyte">The analyte to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="Analyte"/>
        /// <seealso cref="Label"/>
        private static void validateAnalyteRelationships(Analyte analyte, List<string> errors)
        {
            #region implementation
            // 19.2.3.13 - There are one or more analytes, the substance(s) being measured
            // 19.2.3.14 - Each analyte refers to one substance measured
            if (!analyte.SubstanceSpecificationID.HasValue)
            {
                errors.Add("Analyte must be linked to a SubstanceSpecification (SPL IG 19.2.3.13, 19.2.3.14).");
            }

            if (!analyte.AnalyteSubstanceID.HasValue)
            {
                errors.Add("Analyte must reference the substance being measured (SPL IG 19.2.3.14).");
            }

            // Validate foreign key values are positive integers
            if (analyte.SubstanceSpecificationID.HasValue && analyte.SubstanceSpecificationID.Value <= 0)
            {
                errors.Add("SubstanceSpecification ID must be a positive integer when specified.");
            }

            if (analyte.AnalyteSubstanceID.HasValue && analyte.AnalyteSubstanceID.Value <= 0)
            {
                errors.Add("Analyte substance ID must be a positive integer when specified.");
            }

            // Primary key validation
            if (analyte.AnalyteID.HasValue && analyte.AnalyteID.Value <= 0)
            {
                errors.Add("Analyte ID must be a positive integer when specified.");
            }
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that commodity codes conform to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 19.2.4.8-19.2.4.12 requirements.
    /// </summary>
    /// <seealso cref="Commodity"/>
    /// <seealso cref="Label"/>
    public class CommodityCodeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Required code system for commodity codes.
        /// </summary>
        private const string RequiredCodeSystem = "2.16.840.1.113883.6.275.1";

        /**************************************************************/
        /// <summary>
        /// Validates the commodity code against SPL requirements.
        /// </summary>
        /// <param name="value">The commodity code value to validate.</param>
        /// <param name="validationContext">The validation context containing the Commodity model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="Commodity"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var commodityCode = value as string;
            var commodity = validationContext.ObjectInstance as Commodity;

            if (commodity == null)
            {
                return new ValidationResult("Commodity context is required for validation.");
            }

            // 19.2.4.9 - There is a tolerance commodity code
            if (string.IsNullOrWhiteSpace(commodityCode))
            {
                return new ValidationResult("Tolerance commodity code is required (SPL IG 19.2.4.9).");
            }

            // 19.2.4.10 - Code system is 2.16.840.1.113883.6.275.1
            if (string.IsNullOrWhiteSpace(commodity.CommodityCodeSystem))
            {
                return new ValidationResult("Commodity code system is required when commodity code is specified.");
            }

            if (commodity.CommodityCodeSystem != RequiredCodeSystem)
            {
                return new ValidationResult($"Commodity code system must be {RequiredCodeSystem} for FDA SPL compliance (SPL IG 19.2.4.10).");
            }

            // 19.2.4.11 - Code comes from the Tolerance Commodity list
            // Note: Full validation would require access to the actual commodity list
            // This validates the basic format requirements

            // 19.2.4.12 - Display name matches code
            if (!string.IsNullOrWhiteSpace(commodity.CommodityDisplayName))
            {
                // Basic validation that display name is present and meaningful when code is present
                if (string.IsNullOrWhiteSpace(commodity.CommodityDisplayName.Trim()))
                {
                    return new ValidationResult("Commodity display name must be meaningful when specified (SPL IG 19.2.4.12).");
                }
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that application type codes conform to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 19.2.4.13-19.2.4.17 requirements.
    /// </summary>
    /// <seealso cref="ApplicationType"/>
    /// <seealso cref="Label"/>
    public class ApplicationTypeCodeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Required code system for application type codes.
        /// </summary>
        private const string RequiredCodeSystem = "2.16.840.1.113883.6.275.1";

        /**************************************************************/
        /// <summary>
        /// Validates the application type code against SPL requirements.
        /// </summary>
        /// <param name="value">The application type code value to validate.</param>
        /// <param name="validationContext">The validation context containing the ApplicationType model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ApplicationType"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var appTypeCode = value as string;
            var applicationType = validationContext.ObjectInstance as ApplicationType;

            if (applicationType == null)
            {
                return new ValidationResult("ApplicationType context is required for validation.");
            }

            // 19.2.4.14 - There is an application type (approval) code
            if (string.IsNullOrWhiteSpace(appTypeCode))
            {
                return new ValidationResult("Application type (approval) code is required (SPL IG 19.2.4.14).");
            }

            // 19.2.4.15 - Code system is 2.16.840.1.113883.6.275.1
            if (string.IsNullOrWhiteSpace(applicationType.AppTypeCodeSystem))
            {
                return new ValidationResult("Application type code system is required when application type code is specified.");
            }

            if (applicationType.AppTypeCodeSystem != RequiredCodeSystem)
            {
                return new ValidationResult($"Application type code system must be {RequiredCodeSystem} for FDA SPL compliance (SPL IG 19.2.4.15).");
            }

            // 19.2.4.16 - Code comes from the Application Type list
            // Note: Full validation would require access to the actual application type list
            // This validates the basic format requirements

            // 19.2.4.17 - Display name matches code
            if (!string.IsNullOrWhiteSpace(applicationType.AppTypeDisplayName))
            {
                // Basic validation that display name is present and meaningful when code is present
                if (string.IsNullOrWhiteSpace(applicationType.AppTypeDisplayName.Trim()))
                {
                    return new ValidationResult("Application type display name must be meaningful when specified (SPL IG 19.2.4.17).");
                }
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that tolerance high values conform to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 19.2.4.1-19.2.4.7 requirements.
    /// </summary>
    /// <seealso cref="ObservationCriterion"/>
    /// <seealso cref="Label"/>
    public class ToleranceHighValueValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Required unit for tolerance values.
        /// </summary>
        private const string RequiredUnit = "[ppm]";

        /**************************************************************/
        /// <summary>
        /// Validates the tolerance high value against SPL requirements.
        /// </summary>
        /// <param name="value">The tolerance high value to validate.</param>
        /// <param name="validationContext">The validation context containing the ObservationCriterion model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ObservationCriterion"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var toleranceHighValue = value as decimal?;
            var observationCriterion = validationContext.ObjectInstance as ObservationCriterion;

            if (observationCriterion == null)
            {
                return new ValidationResult("ObservationCriterion context is required for validation.");
            }

            // 19.2.4.1 - There are one or more reference ranges (tolerances)
            // 19.2.4.2 - Reference ranges have a value
            if (!toleranceHighValue.HasValue)
            {
                return new ValidationResult("Tolerance high value is required for reference ranges (SPL IG 19.2.4.1, 19.2.4.2).");
            }

            // 19.2.4.6 - High boundary value is a number
            if (toleranceHighValue.Value < 0)
            {
                return new ValidationResult("Tolerance high value must be a valid positive number (SPL IG 19.2.4.6).");
            }

            // 19.2.4.7 - High boundary unit is "[ppm]"
            if (string.IsNullOrWhiteSpace(observationCriterion.ToleranceHighUnit))
            {
                return new ValidationResult("Tolerance high unit is required when tolerance high value is specified.");
            }

            if (observationCriterion.ToleranceHighUnit != RequiredUnit)
            {
                return new ValidationResult($"Tolerance high unit must be '{RequiredUnit}' (SPL IG 19.2.4.7).");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that expiration dates conform to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 19.2.4.18-19.2.4.21 requirements.
    /// </summary>
    /// <seealso cref="ObservationCriterion"/>
    /// <seealso cref="Label"/>
    public class ToleranceExpirationDateValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the expiration date against SPL requirements.
        /// </summary>
        /// <param name="value">The expiration date value to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ObservationCriterion"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var expirationDate = value as DateTime?;

            // 19.2.4.18 - There may be an expiration or revocation date (optional)
            if (!expirationDate.HasValue)
            {
                return ValidationResult.Success; // Optional field
            }

            // 19.2.4.20 - Expiration or revocation date value has the format YYYYMMDD
            var dateString = expirationDate.Value.ToString("yyyyMMdd");
            if (dateString.Length != 8)
            {
                return new ValidationResult("Expiration or revocation date must have day precision in YYYYMMDD format (SPL IG 19.2.4.20).");
            }

            // 19.2.4.19 - Expiration or revocation date has a high boundary (this validation represents the high boundary)
            // 19.2.4.21 - Expiration or revocation date has no low boundary (validated by model structure)

            // Validate the date is reasonable (not too far in the past or future)
            var currentDate = DateTime.Today;
            var minDate = currentDate.AddYears(-50);
            var maxDate = currentDate.AddYears(50);

            if (expirationDate.Value < minDate || expirationDate.Value > maxDate)
            {
                return new ValidationResult("Expiration or revocation date must be within a reasonable range.");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that observation criterion consistency requirements are met according to SPL Implementation Guide.
    /// Implements comprehensive validation across SPL Implementation Guide Section 19.2.4 requirements.
    /// </summary>
    /// <seealso cref="ObservationCriterion"/>
    /// <seealso cref="Label"/>
    public class ObservationCriterionConsistencyValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the overall consistency of observation criterion properties against SPL requirements.
        /// </summary>
        /// <param name="value">The observation criterion object to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ObservationCriterion"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var observationCriterion = value as ObservationCriterion;

            if (observationCriterion == null)
            {
                return ValidationResult.Success; // Let other validators handle null cases
            }

            var errors = new List<string>();

            // Validate reference range consistency
            validateReferenceRangeConsistency(observationCriterion, errors);

            // Validate entity relationships
            validateEntityRelationships(observationCriterion, errors);

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates consistency between tolerance values and reference range requirements.
        /// </summary>
        /// <param name="observationCriterion">The observation criterion to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="ObservationCriterion"/>
        /// <seealso cref="Label"/>
        private static void validateReferenceRangeConsistency(ObservationCriterion observationCriterion, List<string> errors)
        {
            #region implementation
            var hasToleranceValue = observationCriterion.ToleranceHighValue.HasValue;
            var hasToleranceUnit = !string.IsNullOrWhiteSpace(observationCriterion.ToleranceHighUnit);

            // 19.2.4.3 - Value is of xsi type IVL_PQ (interval of physical quantity)
            // 19.2.4.4 - There is a high boundary with value and unit
            if (hasToleranceValue && !hasToleranceUnit)
            {
                errors.Add("Tolerance high unit is required when tolerance high value is specified (SPL IG 19.2.4.4).");
            }

            if (!hasToleranceValue && hasToleranceUnit)
            {
                errors.Add("Tolerance high value is required when tolerance high unit is specified (SPL IG 19.2.4.4).");
            }

            // 19.2.4.5 - There is no low boundary (validated by model design - no ToleranceLowValue property)
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates entity relationships and foreign key consistency.
        /// </summary>
        /// <param name="observationCriterion">The observation criterion to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="ObservationCriterion"/>
        /// <seealso cref="Label"/>
        private static void validateEntityRelationships(ObservationCriterion observationCriterion, List<string> errors)
        {
            #region implementation
            // Primary key validation
            if (observationCriterion.ObservationCriterionID.HasValue && observationCriterion.ObservationCriterionID.Value <= 0)
            {
                errors.Add("Observation criterion ID must be a positive integer when specified.");
            }

            // Required foreign key validation
            if (!observationCriterion.SubstanceSpecificationID.HasValue)
            {
                errors.Add("SubstanceSpecification ID is required for observation criterion.");
            }
            else if (observationCriterion.SubstanceSpecificationID.Value <= 0)
            {
                errors.Add("SubstanceSpecification ID must be a positive integer when specified.");
            }

            // Optional foreign key validation
            if (observationCriterion.CommodityID.HasValue && observationCriterion.CommodityID.Value <= 0)
            {
                errors.Add("Commodity ID must be a positive integer when specified.");
            }

            if (observationCriterion.ApplicationTypeID.HasValue && observationCriterion.ApplicationTypeID.Value <= 0)
            {
                errors.Add("Application type ID must be a positive integer when specified.");
            }
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that text notes conform to basic SPL requirements when present.
    /// Implements SPL Implementation Guide Section 19.2.4.22 requirements.
    /// </summary>
    /// <seealso cref="ObservationCriterion"/>
    /// <seealso cref="Label"/>
    public class ToleranceTextNoteValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the text note against basic SPL requirements.
        /// </summary>
        /// <param name="value">The text note value to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ObservationCriterion"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var textNote = value as string;

            // 19.2.4.22 - There may be a text annotation (optional)
            if (string.IsNullOrWhiteSpace(textNote))
            {
                return ValidationResult.Success; // Optional field
            }

            // Basic validation for text content
            if (textNote.Length > 4000) // Reasonable length limit
            {
                return new ValidationResult("Text note exceeds maximum length of 4000 characters.");
            }

            // Ensure text doesn't contain dangerous content
            if (textNote.Contains('<') || textNote.Contains('>'))
            {
                return new ValidationResult("Text note should not contain markup characters for security reasons.");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }
}