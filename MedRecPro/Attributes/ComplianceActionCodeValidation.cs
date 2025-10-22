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
    /// Validates that the compliance action code conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 30.2.3.2 and 31.1.4.2 requirements.
    /// </summary>
    /// <seealso cref="ComplianceAction"/>
    /// <seealso cref="Label"/>
    public class ComplianceActionCodeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// FDA SPL code system for regulatory action codes.
        /// </summary>
        private const string FdaSplCodeSystem = "2.16.840.1.113883.3.26.1.1";

        /// <summary>
        /// Valid regulatory action codes with their display names from the Regulatory Action list.
        /// </summary>
        private static readonly Dictionary<string, string> ValidActionCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            [c.INACTIVATED_CODE] = "Inactivated",
            [c.REACTIVATED_CODE] = "Reactivated",
        };

        /**************************************************************/
        /// <summary>
        /// Validates the compliance action code against SPL requirements.
        /// </summary>
        /// <param name="value">The action code value to validate.</param>
        /// <param name="validationContext">The validation context containing the ComplianceAction model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var actionCode = value as string;
            var complianceAction = validationContext.ObjectInstance as ComplianceAction;

            if (complianceAction == null)
            {
                return new ValidationResult("ComplianceAction context is required for validation.");
            }

            // Action code is required for compliance actions (30.2.3.2, 31.1.4.2)
            if (string.IsNullOrWhiteSpace(actionCode))
            {
                return new ValidationResult("Compliance action code is required (SPL IG 30.2.3.2, 31.1.4.2).");
            }

            // Code system is required and must be FDA SPL system
            if (string.IsNullOrWhiteSpace(complianceAction.ActionCodeSystem))
            {
                return new ValidationResult("Action code system is required when action code is specified.");
            }

            if (complianceAction.ActionCodeSystem != FdaSplCodeSystem)
            {
                return new ValidationResult($"Action code system must be {FdaSplCodeSystem} for FDA SPL compliance (SPL IG 30.2.3.2, 31.1.4.2).");
            }

            // 30.2.3.2, 31.1.4.2 - Code is from the Regulatory Action list
            if (!ValidActionCodes.ContainsKey(actionCode))
            {
                return new ValidationResult($"Action code '{actionCode}' is not from the Regulatory Action list (SPL IG 30.2.3.2, 31.1.4.2).");
            }

            // 30.2.3.2, 31.1.4.2 - Display name matches the code
            var expectedDisplayName = ValidActionCodes[actionCode];
            if (!string.IsNullOrWhiteSpace(complianceAction.ActionDisplayName) &&
                !string.Equals(complianceAction.ActionDisplayName, expectedDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult($"Action display name '{complianceAction.ActionDisplayName}' does not match expected '{expectedDisplayName}' (SPL IG 30.2.3.2, 31.1.4.2).");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the compliance action effective time low (inactivation date) conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 30.2.3.3, 30.2.3.4 and 31.1.4.3, 31.1.4.4 requirements.
    /// </summary>
    /// <seealso cref="ComplianceAction"/>
    /// <seealso cref="Label"/>
    public class ComplianceActionEffectiveTimeLowValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the effective time low against SPL requirements.
        /// </summary>
        /// <param name="value">The effective time low value to validate.</param>
        /// <param name="validationContext">The validation context containing the ComplianceAction model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var effectiveTimeLow = value as DateTime?;

            // 30.2.3.3, 31.1.4.3 - There is an effective time with at least a low value (inactivation date)
            if (!effectiveTimeLow.HasValue)
            {
                return new ValidationResult("Compliance action effective time low (inactivation date) is required (SPL IG 30.2.3.3, 31.1.4.3).");
            }

            // 30.2.3.4, 31.1.4.4 - The effective time low has at least the precision of day in the format YYYYMMDD
            var dateString = effectiveTimeLow.Value.ToString("yyyyMMdd");
            if (dateString.Length != 8)
            {
                return new ValidationResult("Compliance action effective time low must have day precision in YYYYMMDD format (SPL IG 30.2.3.4, 31.1.4.4).");
            }

            // Validate the date is reasonable (not too far in the past or future)
            var currentDate = DateTime.Today;
            var minDate = currentDate.AddYears(-100);
            var maxDate = currentDate.AddYears(10);

            if (effectiveTimeLow.Value < minDate || effectiveTimeLow.Value > maxDate)
            {
                return new ValidationResult("Compliance action effective time low must be within a reasonable range.");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the compliance action effective time high (reactivation date) conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 30.2.3.4, 30.2.3.5, 30.2.3.6 and 31.1.4.4, 31.1.4.5, 31.1.4.6 requirements.
    /// </summary>
    /// <seealso cref="ComplianceAction"/>
    /// <seealso cref="Label"/>
    public class ComplianceActionEffectiveTimeHighValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the effective time high against SPL requirements.
        /// </summary>
        /// <param name="value">The effective time high value to validate.</param>
        /// <param name="validationContext">The validation context containing the ComplianceAction model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var effectiveTimeHigh = value as DateTime?;
            var complianceAction = validationContext.ObjectInstance as ComplianceAction;

            if (complianceAction == null)
            {
                return new ValidationResult("ComplianceAction context is required for validation.");
            }

            // If effective time high is not specified, it's valid (sustained inactivation)
            if (!effectiveTimeHigh.HasValue)
            {
                return ValidationResult.Success;
            }

            // 30.2.3.4, 31.1.4.4 - The effective time high has at least the precision of day in the format YYYYMMDD
            var dateString = effectiveTimeHigh.Value.ToString("yyyyMMdd");
            if (dateString.Length != 8)
            {
                return new ValidationResult("Compliance action effective time high must have day precision in YYYYMMDD format (SPL IG 30.2.3.4, 31.1.4.4).");
            }

            // 30.2.3.5, 31.1.4.5 - If there is an effective time high value, then it is not less than the low value
            if (complianceAction.EffectiveTimeLow.HasValue && effectiveTimeHigh.Value < complianceAction.EffectiveTimeLow.Value)
            {
                return new ValidationResult("Compliance action effective time high (reactivation date) cannot be earlier than effective time low (inactivation date) (SPL IG 30.2.3.5, 31.1.4.5).");
            }

            // 30.2.3.6, 31.1.4.6 - If there is an effective time high value, then it is not later than the document effective time
            // Note: Document effective time validation would require access to the document context
            // This validation should be implemented at the service layer where document context is available

            // Validate the date is reasonable (not too far in the future)
            var currentDate = DateTime.Today;
            var maxDate = currentDate.AddYears(10);

            if (effectiveTimeHigh.Value > maxDate)
            {
                return new ValidationResult("Compliance action effective time high must be within a reasonable range.");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that compliance action context and relationships are consistent according to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 30.2.3.1 and 31.1.4.1 requirements for proper entity relationships.
    /// </summary>
    /// <seealso cref="ComplianceAction"/>
    /// <seealso cref="Label"/>
    public class ComplianceActionContextValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the compliance action context and relationships against SPL requirements.
        /// </summary>
        /// <param name="value">The compliance action to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var complianceAction = value as ComplianceAction;

            if (complianceAction == null)
            {
                return ValidationResult.Success; // Let other validators handle null cases
            }

            var errors = new List<string>();

            // Validate that the compliance action has proper context relationships
            validateContextRelationships(complianceAction, errors);

            // Validate that section and entity relationships are consistent
            validateEntityRelationships(complianceAction, errors);

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that compliance action has proper context relationships.
        /// </summary>
        /// <param name="complianceAction">The compliance action to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="Label"/>
        private static void validateContextRelationships(ComplianceAction complianceAction, List<string> errors)
        {
            #region implementation
            var hasPackageIdentifier = complianceAction.PackageIdentifierID.HasValue;
            var hasDocumentRelationship = complianceAction.DocumentRelationshipID.HasValue;

            // 30.2.3.1, 31.1.4.1 - Compliance action must be linked to either a package (Section 30) or establishment (Section 31)
            if (!hasPackageIdentifier && !hasDocumentRelationship)
            {
                errors.Add("Compliance action must be linked to either a package (PackageIdentifierID) for drug listing inactivation or establishment (DocumentRelationshipID) for establishment registration inactivation (SPL IG 30.2.3.1, 31.1.4.1).");
            }

            // Should not be linked to both package and establishment
            if (hasPackageIdentifier && hasDocumentRelationship)
            {
                errors.Add("Compliance action cannot be linked to both package and establishment simultaneously.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that section and entity relationships are consistent.
        /// </summary>
        /// <param name="complianceAction">The compliance action to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="Label"/>
        private static void validateEntityRelationships(ComplianceAction complianceAction, List<string> errors)
        {
            #region implementation
            var hasSectionId = complianceAction.SectionID.HasValue;
            var hasPackageIdentifier = complianceAction.PackageIdentifierID.HasValue;
            var hasDocumentRelationship = complianceAction.DocumentRelationshipID.HasValue;

            // Section 30 (Drug Listing) should have SectionID and PackageIdentifierID
            if (hasPackageIdentifier && !hasSectionId)
            {
                errors.Add("Drug listing compliance action (with PackageIdentifierID) must have a SectionID reference (SPL IG Section 30.2.3).");
            }

            // Section 31 (Establishment Registration) should have DocumentRelationshipID
            if (hasDocumentRelationship && hasPackageIdentifier)
            {
                errors.Add("Establishment registration compliance action should use DocumentRelationshipID, not PackageIdentifierID (SPL IG Section 31.1.4).");
            }
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that all compliance action properties are consistent with each other according to SPL Implementation Guide requirements.
    /// Implements comprehensive validation across SPL Implementation Guide Section 30.2.3 and 31.1.4 requirements.
    /// </summary>
    /// <seealso cref="ComplianceAction"/>
    /// <seealso cref="Label"/>
    public class ComplianceActionConsistencyValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// FDA SPL code system for regulatory action codes.
        /// </summary>
        private const string FdaSplCodeSystem = "2.16.840.1.113883.3.26.1.1";

        /**************************************************************/
        /// <summary>
        /// Validates the overall consistency of compliance action properties against SPL requirements.
        /// </summary>
        /// <param name="value">The compliance action object to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var complianceAction = value as ComplianceAction;

            if (complianceAction == null)
            {
                return ValidationResult.Success; // Let other validators handle null cases
            }

            var errors = new List<string>();

            // Validate action code and code system consistency
            validateActionCodeConsistency(complianceAction, errors);

            // Validate effective time consistency
            validateEffectiveTimeConsistency(complianceAction, errors);

            // Validate entity relationship consistency
            validateEntityConsistency(complianceAction, errors);

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates consistency between action code, code system, and display name.
        /// </summary>
        /// <param name="complianceAction">The compliance action to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="Label"/>
        private static void validateActionCodeConsistency(ComplianceAction complianceAction, List<string> errors)
        {
            #region implementation
            var hasActionCode = !string.IsNullOrWhiteSpace(complianceAction.ActionCode);
            var hasActionCodeSystem = !string.IsNullOrWhiteSpace(complianceAction.ActionCodeSystem);
            var hasActionDisplayName = !string.IsNullOrWhiteSpace(complianceAction.ActionDisplayName);

            // All action properties should be specified together
            if (hasActionCode && !hasActionCodeSystem)
            {
                errors.Add("Action code system is required when action code is specified.");
            }

            if (hasActionCode && !hasActionDisplayName)
            {
                errors.Add("Action display name is required when action code is specified.");
            }

            // Validate code system value
            if (hasActionCodeSystem && complianceAction.ActionCodeSystem != FdaSplCodeSystem)
            {
                errors.Add($"Action code system must be {FdaSplCodeSystem} for FDA SPL compliance.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates consistency of effective time values.
        /// </summary>
        /// <param name="complianceAction">The compliance action to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="Label"/>
        private static void validateEffectiveTimeConsistency(ComplianceAction complianceAction, List<string> errors)
        {
            #region implementation
            var hasComplianceAction = complianceAction != null;
            var hasEffectiveTimeLow = complianceAction?.EffectiveTimeLow.HasValue ?? false;
            var hasEffectiveTimeHigh = complianceAction?.EffectiveTimeHigh.HasValue ?? false;

            // Effective time low is required
            if (!hasEffectiveTimeLow)
            {
                errors.Add("Effective time low (inactivation date) is required for compliance actions.");
            }

            // If both dates are present, validate chronological order
            if (hasComplianceAction 
                && hasEffectiveTimeLow 
                && hasEffectiveTimeHigh)
            {
                if (complianceAction!.EffectiveTimeHigh!.Value < complianceAction!.EffectiveTimeLow!.Value)
                {
                    errors.Add("Effective time high (reactivation date) cannot be earlier than effective time low (inactivation date).");
                }

                // Validate same date scenario
                if (complianceAction.EffectiveTimeHigh.Value.Date == complianceAction.EffectiveTimeLow.Value.Date)
                {
                    errors.Add("Effective time high and low cannot be the same date - use only effective time low for single-day inactivations.");
                }
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates consistency of entity relationships and context.
        /// </summary>
        /// <param name="complianceAction">The compliance action to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="ComplianceAction"/>
        /// <seealso cref="Label"/>
        private static void validateEntityConsistency(ComplianceAction complianceAction, List<string> errors)
        {
            #region implementation
            var hasComplianceAction = complianceAction != null;
            var hasComplianceActionId = complianceAction?.ComplianceActionID.HasValue ?? false;
            var hasSectionId = complianceAction?.SectionID.HasValue ?? false;
            var hasPackageIdentifier = complianceAction?.PackageIdentifierID.HasValue ?? false;
            var hasDocumentRelationship = complianceAction?.DocumentRelationshipID.HasValue ?? false;

            // Primary key validation
            if (hasComplianceAction 
                && hasComplianceActionId 
                && complianceAction!.ComplianceActionID!.Value <= 0)
            {
                errors.Add("Compliance action ID must be a positive integer when specified.");
            }

            // Foreign key validation
            if (hasComplianceAction 
                && hasSectionId 
                && complianceAction!.SectionID!.Value <= 0)
            {
                errors.Add("Section ID must be a positive integer when specified.");
            }

            if (hasComplianceAction 
                && hasPackageIdentifier 
                && complianceAction!.PackageIdentifierID!.Value <= 0)
            {
                errors.Add("Package identifier ID must be a positive integer when specified.");
            }

            if (hasComplianceAction 
                && hasDocumentRelationship 
                && complianceAction!.DocumentRelationshipID!.Value <= 0)
            {
                errors.Add("Document relationship ID must be a positive integer when specified.");
            }

            // Validate proper entity relationship patterns
            if (hasPackageIdentifier && hasDocumentRelationship)
            {
                errors.Add("Compliance action cannot reference both package and document relationship simultaneously.");
            }

            if (!hasPackageIdentifier && !hasDocumentRelationship)
            {
                errors.Add("Compliance action must reference either a package (for drug listing) or document relationship (for establishment registration).");
            }
            #endregion
        }
        #endregion
    }
}
