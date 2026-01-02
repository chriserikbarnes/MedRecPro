using System.ComponentModel.DataAnnotations;
using MedRecProImportClass.Models;
using static MedRecProImportClass.Models.Label;

namespace MedRecProImportClass.Models.Validation
{
    /**************************************************************/
    /// <summary>
    /// Validates that the event code is from the FDA SPL code system and matches allowed LDD Distribution Codes.
    /// Implements SPL Implementation Guide Section 16.2.9.5-16.2.9.7 requirements.
    /// </summary>
    /// <seealso cref="ProductEvent"/>
    /// <seealso cref="Label"/>
    public class ProductEventCodeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// FDA SPL code system for product event codes.
        /// </summary>
        private const string FdaSplCodeSystem = "2.16.840.1.113883.3.26.1.1";

        /// <summary>
        /// Valid event codes for lot distribution reporting.
        /// </summary>
        private static readonly Dictionary<string, string> ValidEventCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["C106325"] = "Distributed per reporting interval",
            ["C106328"] = "Returned"
        };

        /// <summary>
        /// Validates the event code against FDA SPL requirements.
        /// </summary>
        /// <param name="value">The event code value to validate.</param>
        /// <param name="validationContext">The validation context containing the ProductEvent model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var eventCode = value as string;
            var productEvent = validationContext.ObjectInstance as ProductEvent;

            if (productEvent == null)
            {
                return new ValidationResult("ProductEvent context is required for validation.");
            }

            // 16.2.9.5 - There is a product event code
            if (string.IsNullOrWhiteSpace(eventCode))
            {
                return new ValidationResult("Event code is required for product events (SPL IG 16.2.9.5).");
            }

            // 16.2.9.6 - Code system is 2.16.840.1.113883.3.26.1.1
            if (string.IsNullOrWhiteSpace(productEvent.EventCodeSystem))
            {
                return new ValidationResult("Event code system is required when event code is specified.");
            }

            if (productEvent.EventCodeSystem != FdaSplCodeSystem)
            {
                return new ValidationResult($"Event code system must be {FdaSplCodeSystem} for FDA SPL compliance (SPL IG 16.2.9.6).");
            }

            // 16.2.9.7 - The code is from the LDD Distribution Codes list and display name matches
            if (!ValidEventCodes.ContainsKey(eventCode))
            {
                return new ValidationResult($"Event code '{eventCode}' is not a valid LDD Distribution Code (SPL IG 16.2.9.7).");
            }

            var expectedDisplayName = ValidEventCodes[eventCode];
            if (!string.IsNullOrWhiteSpace(productEvent.EventDisplayName) && 
                !string.Equals(productEvent.EventDisplayName, expectedDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult($"Event code display name '{productEvent.EventDisplayName}' does not match expected '{expectedDisplayName}' (SPL IG 16.2.9.7).");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the quantity value is a valid integer and unit follows SPL requirements.
    /// Implements SPL Implementation Guide Section 16.2.9.2-16.2.9.4 requirements.
    /// </summary>
    /// <seealso cref="ProductEvent"/>
    /// <seealso cref="Label"/>
    public class ProductEventQuantityValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Validates the quantity value against SPL requirements.
        /// </summary>
        /// <param name="value">The quantity value to validate.</param>
        /// <param name="validationContext">The validation context containing the ProductEvent model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var quantityValue = value as int?;
            var productEvent = validationContext.ObjectInstance as ProductEvent;

            if (productEvent == null)
            {
                return new ValidationResult("ProductEvent context is required for validation.");
            }

            // 16.2.9.2 - There is one quantity (Final Containers Distributed)
            if (!quantityValue.HasValue)
            {
                return new ValidationResult("Quantity value is required for product events (SPL IG 16.2.9.2).");
            }

            // 16.2.9.3 - Quantity value is the integer number of final containers distributed
            if (quantityValue.Value < 0)
            {
                return new ValidationResult("Quantity value cannot be negative (SPL IG 16.2.9.3).");
            }

            // 16.2.9.4 - Quantity unit is "1" or there is no unit
            if (!string.IsNullOrWhiteSpace(productEvent.QuantityUnit) && productEvent.QuantityUnit != "1")
            {
                return new ValidationResult($"Quantity unit must be '1' or empty, but was '{productEvent.QuantityUnit}' (SPL IG 16.2.9.4).");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the effective time follows SPL requirements based on event code.
    /// Implements SPL Implementation Guide Section 16.2.9.9-16.2.9.11 and 16.2.10.2 requirements.
    /// </summary>
    /// <seealso cref="ProductEvent"/>
    /// <seealso cref="Label"/>
    public class ProductEventEffectiveTimeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Validates the effective time against SPL requirements.
        /// </summary>
        /// <param name="value">The effective time value to validate.</param>
        /// <param name="validationContext">The validation context containing the ProductEvent model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var effectiveTimeLow = value as DateTime?;
            var productEvent = validationContext.ObjectInstance as ProductEvent;

            if (productEvent == null)
            {
                return new ValidationResult("ProductEvent context is required for validation.");
            }

            var eventCode = productEvent.EventCode;
            if (string.IsNullOrWhiteSpace(eventCode))
            {
                return ValidationResult.Success; // Event code validation will catch this
            }

            if (string.Equals(eventCode, "C106325", StringComparison.OrdinalIgnoreCase)) // Distributed
            {
                // 16.2.9.9 - Container distribution event has an effective time with low boundary
                // 16.2.9.11 - There should be an initial distribution date
                if (!effectiveTimeLow.HasValue)
                {
                    return new ValidationResult("Distribution events must have an initial distribution date (SPL IG 16.2.9.9, 16.2.9.11).");
                }

                // 16.2.9.10 - Initial distribution date has at least the precision of day
                var dateString = effectiveTimeLow.Value.ToString("yyyyMMdd");
                if (dateString.Length < 8)
                {
                    return new ValidationResult("Initial distribution date must have at least day precision (YYYYMMDD format) (SPL IG 16.2.9.10).");
                }
            }
            else if (string.Equals(eventCode, "C106328", StringComparison.OrdinalIgnoreCase)) // Returned
            {
                // 16.2.10.2 - Returned product event has no effective time
                if (effectiveTimeLow.HasValue)
                {
                    return new ValidationResult("Returned events must not have an effective time (SPL IG 16.2.10.2).");
                }
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }
}