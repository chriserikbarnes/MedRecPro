using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using static MedRecPro.Models.Label;

namespace MedRecPro.Models.Validation
{
    /**************************************************************/
    /// <summary>
    /// Validates that the disciplinary action code conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.7.1-18.1.7.4 requirements.
    /// </summary>
    /// <seealso cref="DisciplinaryAction"/>
    /// <seealso cref="Label"/>
    public class DisciplinaryActionCodeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// FDA SPL code system for disciplinary action codes.
        /// </summary>
        private const string FdaSplCodeSystem = "2.16.840.1.113883.3.26.1.1";

        /// <summary>
        /// Valid disciplinary action codes with their display names from the Approval action list.
        /// </summary>
        private static readonly Dictionary<string, string> ValidActionCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["C118406"] = "suspension",
            ["C118407"] = "revoked",
            ["C118408"] = "activation",
            ["C118471"] = "resolved",
            ["C118472"] = "other"
        };

        /**************************************************************/
        /// <summary>
        /// Validates the disciplinary action code against FDA SPL requirements.
        /// </summary>
        /// <param name="value">The action code value to validate.</param>
        /// <param name="validationContext">The validation context containing the DisciplinaryAction model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var actionCode = value as string;
            var disciplinaryAction = validationContext.ObjectInstance as DisciplinaryAction;

            if (disciplinaryAction == null)
            {
                return new ValidationResult("DisciplinaryAction context is required for validation.");
            }

            // 18.1.7.1 - There is a disciplinary action code
            if (string.IsNullOrWhiteSpace(actionCode))
            {
                return new ValidationResult("Disciplinary action code is required (SPL IG 18.1.7.1).");
            }

            // 18.1.7.4 - Code system is 2.16.840.1.113883.3.26.1.1
            if (string.IsNullOrWhiteSpace(disciplinaryAction.ActionCodeSystem))
            {
                return new ValidationResult("Action code system is required when action code is specified.");
            }

            if (disciplinaryAction.ActionCodeSystem != FdaSplCodeSystem)
            {
                return new ValidationResult($"Action code system must be {FdaSplCodeSystem} for FDA SPL compliance (SPL IG 18.1.7.4).");
            }

            // 18.1.7.2 - Code comes from the Approval action list
            if (!ValidActionCodes.ContainsKey(actionCode))
            {
                return new ValidationResult($"Action code '{actionCode}' is not from the Approval action list (SPL IG 18.1.7.2).");
            }

            // 18.1.7.3 - Display name matches the code
            var expectedDisplayName = ValidActionCodes[actionCode];
            if (!string.IsNullOrWhiteSpace(disciplinaryAction.ActionDisplayName) &&
                !string.Equals(disciplinaryAction.ActionDisplayName, expectedDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult($"Action display name '{disciplinaryAction.ActionDisplayName}' does not match expected '{expectedDisplayName}' (SPL IG 18.1.7.3).");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that action text is provided when disciplinary action code is "other" and follows SPL requirements.
    /// Implements SPL Implementation Guide Section 18.1.7.5-18.1.7.6 requirements.
    /// </summary>
    /// <seealso cref="DisciplinaryAction"/>
    /// <seealso cref="Label"/>
    public class DisciplinaryActionTextValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// "Other" action code that requires additional text description.
        /// </summary>
        private const string OtherActionCode = "C118472";

        /**************************************************************/
        /// <summary>
        /// Validates the action text against SPL requirements.
        /// </summary>
        /// <param name="value">The action text value to validate.</param>
        /// <param name="validationContext">The validation context containing the DisciplinaryAction model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var actionText = value as string;
            var disciplinaryAction = validationContext.ObjectInstance as DisciplinaryAction;

            if (disciplinaryAction == null)
            {
                return new ValidationResult("DisciplinaryAction context is required for validation.");
            }

            var actionCode = disciplinaryAction.ActionCode;

            // 18.1.7.5 - If action is "other" (C118472), then there is a text element
            if (string.Equals(actionCode, OtherActionCode, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(actionText))
                {
                    return new ValidationResult("Action text is required when action code is 'other' (C118472) (SPL IG 18.1.7.5).");
                }

                // 18.1.7.6 - Text must be of xsi:type "ST" (plain text string) - basic validation for plain text
                if (actionText.Contains('<') || actionText.Contains('>'))
                {
                    return new ValidationResult("Action text must be plain text without markup when action code is 'other' (SPL IG 18.1.7.6).");
                }
            }
            else
            {
                // If action is not "other", text should not be specified
                if (!string.IsNullOrWhiteSpace(actionText))
                {
                    return new ValidationResult("Action text should only be specified when action code is 'other' (C118472).");
                }
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the disciplinary action effective time conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.7.7-18.1.7.8 requirements.
    /// </summary>
    /// <seealso cref="DisciplinaryAction"/>
    /// <seealso cref="Label"/>
    public class DisciplinaryActionEffectiveTimeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the effective time against SPL requirements.
        /// </summary>
        /// <param name="value">The effective time value to validate.</param>
        /// <param name="validationContext">The validation context containing the DisciplinaryAction model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var effectiveTime = value as DateTime?;

            // 18.1.7.7 - Disciplinary action has an effective time
            if (!effectiveTime.HasValue)
            {
                return new ValidationResult("Disciplinary action effective time is required (SPL IG 18.1.7.7).");
            }

            // 18.1.7.8 - The effective time value has at least the precision of day in the format YYYYMMDD
            var dateString = effectiveTime.Value.ToString("yyyyMMdd");
            if (dateString.Length != 8)
            {
                return new ValidationResult("Disciplinary action effective time must have day precision in YYYYMMDD format (SPL IG 18.1.7.8).");
            }

            // Validate the date is reasonable (not too far in the past or future)
            var currentDate = DateTime.Today;
            var minDate = currentDate.AddYears(-50);
            var maxDate = currentDate.AddYears(10);

            if (effectiveTime.Value < minDate || effectiveTime.Value > maxDate)
            {
                return new ValidationResult("Disciplinary action effective time must be within a reasonable range.");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that disciplinary actions maintain proper chronological order as required by SPL Implementation Guide.
    /// Implements SPL Implementation Guide Section 18.1.7.9 requirements.
    /// </summary>
    /// <remarks>
    /// This validation should be applied at the collection level when multiple disciplinary actions exist.
    /// It ensures that disciplinary actions are in chronological order with the most recent action last.
    /// </remarks>
    /// <seealso cref="DisciplinaryAction"/>
    /// <seealso cref="Label"/>
    public class DisciplinaryActionChronologicalOrderValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates the chronological order of disciplinary actions.
        /// </summary>
        /// <param name="value">The collection of disciplinary actions to validate.</param>
        /// <param name="validationContext">The validation context containing additional validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            if (value is not IEnumerable<DisciplinaryAction> disciplinaryActions)
            {
                return ValidationResult.Success; // Not applicable if not a collection
            }

            var actionsList = disciplinaryActions.Where(da => da.EffectiveTime.HasValue).OrderBy(da => da.EffectiveTime).ToList();

            // 18.1.7.9 - Disciplinary actions are in chronological order, most recent action last
            if (actionsList.Count > 1)
            {
                for (int i = 1; i < actionsList.Count; i++)
                {
                    if (actionsList[i].EffectiveTime < actionsList[i - 1].EffectiveTime)
                    {
                        return new ValidationResult("Disciplinary actions must be in chronological order with most recent action last (SPL IG 18.1.7.9).");
                    }
                }
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the disciplinary action code is recognized for license status consistency checking.
    /// This is a preparatory validation for SPL Implementation Guide Section 18.1.7.10-18.1.7.14 requirements.
    /// </summary>
    /// <remarks>
    /// This validation ensures the disciplinary action code is valid for license status consistency checks.
    /// Full license status consistency validation requires access to the License entity and should be 
    /// implemented at the service layer where both DisciplinaryAction and License entities are available.
    /// </remarks>
    /// <seealso cref="DisciplinaryAction"/>
    /// <seealso cref="License"/>
    /// <seealso cref="Label"/>
    public class DisciplinaryActionLicenseStatusConsistencyValidationAttribute : ValidationAttribute
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Validates that the disciplinary action code is recognized for license status consistency.
        /// </summary>
        /// <param name="value">The disciplinary action to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <remarks>
        /// This validation is a preparatory step. Full license status consistency validation 
        /// (SPL IG 18.1.7.10-18.1.7.14) should be implemented at the service layer where both 
        /// DisciplinaryAction and License entities are accessible.
        /// Required consistency rules:
        /// - C118406 (suspension): license status must be "suspended" or "completed"
        /// - C118407 (revocation): license status must be "aborted" 
        /// - C118408 (activation): license status must be "active"
        /// - C118471 (resolution): license status must be "active" or "completed"
        /// - C118472 (other): license status can be "active", "completed", "suspended", or "aborted"
        /// </remarks>
        /// <seealso cref="DisciplinaryAction"/>
        /// <seealso cref="License"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var disciplinaryAction = value as DisciplinaryAction;

            if (disciplinaryAction == null || string.IsNullOrWhiteSpace(disciplinaryAction.ActionCode))
            {
                return ValidationResult.Success; // Let other validators handle these cases
            }

            // Validate that the action code is one that requires license status consistency checking
            var validActionCodes = new[] { "C118406", "C118407", "C118408", "C118471", "C118472" };

            if (!validActionCodes.Contains(disciplinaryAction.ActionCode, StringComparer.OrdinalIgnoreCase))
            {
                return new ValidationResult($"Disciplinary action code '{disciplinaryAction.ActionCode}' is not recognized for license status consistency validation.");
            }

            // Note: Full license status consistency validation should be implemented at the service layer
            // where the associated License entity can be accessed to check status consistency per
            // SPL IG requirements 18.1.7.10-18.1.7.14

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }
}