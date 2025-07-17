using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MedRecPro.Models;
using MedRecPro.Helpers;
using MedRecPro.Models.Validation;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Provides comprehensive validation services for ProductEvent entities according to SPL Implementation Guide Section 16.2.9 and 16.2.10.
    /// Implements DRY validation methods for product events, event codes, quantities, and effective times.
    /// </summary>
    /// <remarks>
    /// This service centralizes all product event validation logic to ensure consistency across the application.
    /// Validation rules are based on SPL Implementation Guide requirements for lot distribution reporting.
    /// </remarks>
    /// <seealso cref="ProductEvent"/>
    /// <seealso cref="Label"/>
    /// <seealso cref="PackagingLevel"/>
    /// <seealso cref="ProductEventParser"/>
    public class ProductEventValidationService
    {
        #region implementation
        private readonly ILogger _logger;
        private static readonly string FdaSplCodeSystem = "2.16.840.1.113883.3.26.1.1";

        /// <summary>
        /// Valid event codes for lot distribution reporting.
        /// </summary>
        private static readonly Dictionary<string, string> ValidEventCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["C106325"] = "Distributed per reporting interval",
            ["C106328"] = "Returned"
        };

        /// <summary>
        /// Initializes a new instance of the ProductEventValidationService.
        /// </summary>
        /// <param name="logger">Logger for validation messages and errors.</param>
        public ProductEventValidationService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        #endregion

        /**************************************************************/
        /// <summary>
        /// Validates a complete ProductEvent entity against all SPL Implementation Guide requirements.
        /// Performs comprehensive validation including event codes, quantities, and effective times.
        /// </summary>
        /// <param name="productEvent">The ProductEvent entity to validate.</param>
        /// <returns>A validation result containing success status and any error messages.</returns>
        /// <example>
        /// <code>
        /// var validationService = new ProductEventValidationService(logger);
        /// var result = validationService.ValidateProductEvent(productEvent);
        /// if (!result.IsValid)
        /// {
        ///     foreach (var error in result.Errors)
        ///         Console.WriteLine(error);
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="ValidationResult"/>
        /// <seealso cref="Label"/>
        public ValidationResult ValidateProductEvent(ProductEvent productEvent)
        {
            #region implementation
            var result = new ValidationResult();

            if (productEvent == null)
            {
                result.AddError("ProductEvent cannot be null.");
                return result;
            }

            try
            {
                // 16.2.9.1 - There are one or more product events (entity exists)
                _logger.LogDebug("Validating product event for PackagingLevelID {PackagingLevelID}", productEvent.PackagingLevelID);

                // 16.2.9.5, 16.2.9.6, 16.2.9.7 - Event code validation
                var eventCodeValidation = ValidateEventCode(productEvent.EventCode, productEvent.EventCodeSystem, productEvent.EventDisplayName);
                result.MergeWith(eventCodeValidation);

                // 16.2.9.2, 16.2.9.3, 16.2.9.4 - Quantity validation
                var quantityValidation = ValidateQuantity(productEvent.QuantityValue, productEvent.QuantityUnit);
                result.MergeWith(quantityValidation);

                // 16.2.9.9, 16.2.9.10, 16.2.9.11 - Effective time validation
                var effectiveTimeValidation = ValidateEffectiveTime(productEvent.EventCode, productEvent.EffectiveTimeLow);
                result.MergeWith(effectiveTimeValidation);

                if (result.IsValid)
                {
                    _logger.LogInformation("ProductEvent validation passed for PackagingLevelID {PackagingLevelID}", productEvent.PackagingLevelID);
                }
                else
                {
                    _logger.LogWarning("ProductEvent validation failed for PackagingLevelID {PackagingLevelID} with {ErrorCount} errors",
                        productEvent.PackagingLevelID, result.Errors.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during ProductEvent validation for PackagingLevelID {PackagingLevelID}", productEvent.PackagingLevelID);
                result.AddError($"Validation error: {ex.Message}");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates event code specifications according to SPL Implementation Guide Section 16.2.9.5-16.2.9.7.
        /// Ensures event codes are from the correct FDA SPL code system and have matching display names.
        /// </summary>
        /// <param name="eventCode">The event code value to validate.</param>
        /// <param name="eventCodeSystem">The code system for the event code.</param>
        /// <param name="eventDisplayName">The display name for the event code.</param>
        /// <returns>A validation result indicating success or specific event code errors.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Label"/>
        public ValidationResult ValidateEventCode(string? eventCode, string? eventCodeSystem, string? eventDisplayName)
        {
            #region implementation
            var result = new ValidationResult();

            // 16.2.9.5 - There is a product event code
            if (string.IsNullOrWhiteSpace(eventCode))
            {
                result.AddError("Event code is required for product events (SPL IG 16.2.9.5).");
                return result;
            }

            // 16.2.9.6 - Code system is 2.16.840.1.113883.3.26.1.1
            if (string.IsNullOrWhiteSpace(eventCodeSystem))
            {
                result.AddError("Event code system is required when event code is specified.");
            }
            else if (eventCodeSystem != FdaSplCodeSystem)
            {
                result.AddError($"Event code system must be {FdaSplCodeSystem} for FDA SPL compliance (SPL IG 16.2.9.6).");
            }

            // 16.2.9.7 - The code is from the LDD Distribution Codes list and display name matches
            if (!ValidEventCodes.ContainsKey(eventCode))
            {
                result.AddError($"Event code '{eventCode}' is not a valid LDD Distribution Code (SPL IG 16.2.9.7).");
            }
            else
            {
                var expectedDisplayName = ValidEventCodes[eventCode];
                if (!string.IsNullOrWhiteSpace(eventDisplayName) && 
                    !string.Equals(eventDisplayName, expectedDisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    result.AddError($"Event code display name '{eventDisplayName}' does not match expected '{expectedDisplayName}' (SPL IG 16.2.9.7).");
                }
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates quantity specifications according to SPL Implementation Guide Section 16.2.9.2-16.2.9.4.
        /// Ensures quantities have valid integer values and proper units.
        /// </summary>
        /// <param name="quantityValue">The numeric quantity value.</param>
        /// <param name="quantityUnit">The unit for the quantity.</param>
        /// <returns>A validation result indicating success or specific quantity errors.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Label"/>
        public ValidationResult ValidateQuantity(int? quantityValue, string? quantityUnit)
        {
            #region implementation
            var result = new ValidationResult();

            // 16.2.9.2 - There is one quantity (Final Containers Distributed)
            if (!quantityValue.HasValue)
            {
                result.AddError("Quantity value is required for product events (SPL IG 16.2.9.2).");
                return result;
            }

            // 16.2.9.3 - Quantity value is the integer number of final containers distributed
            if (quantityValue.Value < 0)
            {
                result.AddError("Quantity value cannot be negative (SPL IG 16.2.9.3).");
            }

            // 16.2.9.4 - Quantity unit is "1" or there is no unit
            if (!string.IsNullOrWhiteSpace(quantityUnit) && quantityUnit != "1")
            {
                result.AddError($"Quantity unit must be '1' or empty, but was '{quantityUnit}' (SPL IG 16.2.9.4).");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates effective time specifications according to SPL Implementation Guide Section 16.2.9.9-16.2.9.11 and 16.2.10.2.
        /// Ensures distribution events have proper initial distribution dates while returned events do not.
        /// </summary>
        /// <param name="eventCode">The event code to determine timing requirements.</param>
        /// <param name="effectiveTimeLow">The effective time low boundary.</param>
        /// <returns>A validation result indicating success or specific effective time errors.</returns>
        /// <seealso cref="ProductEvent"/>
        /// <seealso cref="Label"/>
        public ValidationResult ValidateEffectiveTime(string? eventCode, DateTime? effectiveTimeLow)
        {
            #region implementation
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(eventCode))
            {
                return result; // Event code validation will catch this
            }

            if (string.Equals(eventCode, "C106325", StringComparison.OrdinalIgnoreCase)) // Distributed
            {
                // 16.2.9.9 - Container distribution event has an effective time with low boundary
                // 16.2.9.11 - There should be an initial distribution date
                if (!effectiveTimeLow.HasValue)
                {
                    result.AddError("Distribution events must have an initial distribution date (SPL IG 16.2.9.9, 16.2.9.11).");
                }
                else
                {
                    // 16.2.9.10 - Initial distribution date has at least the precision of day
                    // Note: DateTime parsing from YYYYMMDD format should handle this, but we can validate precision
                    var dateString = effectiveTimeLow.Value.ToString("yyyyMMdd");
                    if (dateString.Length < 8)
                    {
                        result.AddError("Initial distribution date must have at least day precision (YYYYMMDD format) (SPL IG 16.2.9.10).");
                    }
                }
            }
            else if (string.Equals(eventCode, "C106328", StringComparison.OrdinalIgnoreCase)) // Returned
            {
                // 16.2.10.2 - Returned product event has no effective time
                if (effectiveTimeLow.HasValue)
                {
                    result.AddError("Returned events must not have an effective time (SPL IG 16.2.10.2).");
                }
            }

            return result;
            #endregion
        }
    }
}