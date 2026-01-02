using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MedRecProImportClass.Models;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models.Validation;
using static MedRecProImportClass.Models.Label;

namespace MedRecProImportClass.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Provides comprehensive validation services for DosingSpecification entities according to SPL Implementation Guide Section 16.2.4.
    /// Implements DRY validation methods for dosing specifications, route codes, dose quantities, and UCUM units.
    /// </summary>
    /// <remarks>
    /// This service centralizes all dosing specification validation logic to ensure consistency across the application.
    /// Validation rules are based on SPL Implementation Guide requirements for lot distribution calculations.
    /// </remarks>
    /// <seealso cref="DosingSpecification"/>
    /// <seealso cref="Label"/>
    /// <seealso cref="ProductRouteOfAdministration"/>
    /// <seealso cref="ManufacturedProductParser"/>
    public class DosingSpecificationValidationService
    {
        #region implementation
        private readonly ILogger _logger;
        private static readonly string FdaSplCodeSystem = "2.16.840.1.113883.3.26.1.1";

        /// <summary>
        /// Initializes a new instance of the DosingSpecificationValidationService.
        /// </summary>
        /// <param name="logger">Logger for validation messages and errors.</param>
        public DosingSpecificationValidationService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /**************************************************************/
        /// <summary>
        /// Validates a complete DosingSpecification entity against all SPL Implementation Guide requirements.
        /// Performs comprehensive validation including route codes, dose quantities, and unit specifications.
        /// </summary>
        /// <param name="dosingSpec">The DosingSpecification entity to validate.</param>
        /// <returns>A validation result containing success status and any error messages.</returns>
        /// <example>
        /// <code>
        /// var validationService = new DosingSpecificationValidationService(logger);
        /// var result = validationService.ValidateDosingSpecification(dosingSpec);
        /// if (!result.IsValid)
        /// {
        ///     foreach (var error in result.Errors)
        ///         Console.WriteLine(error);
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="DosingSpecification"/>
        /// <seealso cref="ValidationResult"/>
        /// <seealso cref="Label"/>
        public ValidationResult ValidateDosingSpecification(DosingSpecification dosingSpec)
        {
            #region implementation
            var result = new ValidationResult();

            if (dosingSpec == null)
            {
                result.AddError("DosingSpecification cannot be null.");
                return result;
            }

            try
            {
                // 16.2.4.1 - There is a dosing specification element (entity exists)
                _logger.LogDebug("Validating dosing specification for ProductID {ProductID}", dosingSpec.ProductID);

                // 16.2.4.2 - Route code validation
                var routeValidation = ValidateRouteCode(dosingSpec.RouteCode, dosingSpec.RouteCodeSystem);
                result.MergeWith(routeValidation);

                // 16.2.4.3, 16.2.4.4, 16.2.4.5, 16.2.4.6, 16.2.4.7 - Dose quantity validation
                var doseValidation = ValidateDoseQuantity(dosingSpec.DoseQuantityValue, dosingSpec.DoseQuantityUnit);
                result.MergeWith(doseValidation);

                if (result.IsValid)
                {
                    _logger.LogInformation("DosingSpecification validation passed for ProductID {ProductID}", dosingSpec.ProductID);
                }
                else
                {
                    _logger.LogWarning("DosingSpecification validation failed for ProductID {ProductID} with {ErrorCount} errors",
                        dosingSpec.ProductID, result.Errors.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during DosingSpecification validation for ProductID {ProductID}", dosingSpec.ProductID);
                result.AddError($"Validation error: {ex.Message}");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates route code specifications according to SPL Implementation Guide Section 3.2.20.2f.
        /// Ensures route codes are from the correct FDA SPL code system or have appropriate nullFlavor.
        /// </summary>
        /// <param name="routeCode">The route code value to validate.</param>
        /// <param name="routeCodeSystem">The code system for the route code.</param>
        /// <returns>A validation result indicating success or specific route code errors.</returns>
        /// <seealso cref="ProductRouteOfAdministration"/>
        /// <seealso cref="DosingSpecification"/>
        /// <seealso cref="Label"/>
        public ValidationResult ValidateRouteCode(string? routeCode, string? routeCodeSystem)
        {
            #region implementation
            var result = new ValidationResult();

            // Route code is required for dosing specifications
            if (string.IsNullOrWhiteSpace(routeCode))
            {
                result.AddError("Route code is required for dosing specifications (SPL IG 16.2.4.2).");
                return result;
            }

            // Code system validation - must be FDA SPL system
            if (string.IsNullOrWhiteSpace(routeCodeSystem))
            {
                result.AddError("Route code system is required when route code is specified.");
            }
            else if (routeCodeSystem != FdaSplCodeSystem)
            {
                result.AddError($"Route code system must be {FdaSplCodeSystem} for FDA SPL compliance.");
            }

            // Basic format validation
            if (!Regex.IsMatch(routeCode.Trim(), @"^[A-Za-z0-9]+$"))
            {
                result.AddError("Route code must contain only alphanumeric characters.");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates dose quantity specifications according to SPL Implementation Guide Section 16.2.4.3-16.2.4.7.
        /// Ensures dose quantities have valid numeric values and UCUM-compliant units.
        /// </summary>
        /// <param name="doseQuantityValue">The numeric dose quantity value.</param>
        /// <param name="doseQuantityUnit">The unit for the dose quantity.</param>
        /// <returns>A validation result indicating success or specific dose quantity errors.</returns>
        /// <seealso cref="DosingSpecification"/>
        /// <seealso cref="UCUMUnitValidationAttribute"/>
        /// <seealso cref="Label"/>
        public ValidationResult ValidateDoseQuantity(decimal? doseQuantityValue, string? doseQuantityUnit)
        {
            #region implementation
            var result = new ValidationResult();

            // 16.2.4.3 - Dose quantity specification with single value and unit
            // Note: Variable dose products may not have dose quantity, so this is conditional

            if (doseQuantityValue.HasValue || !string.IsNullOrWhiteSpace(doseQuantityUnit))
            {
                // If either value or unit is present, both should be present
                if (!doseQuantityValue.HasValue)
                {
                    result.AddError("Dose quantity value is required when dose quantity unit is specified (SPL IG 16.2.4.3).");
                }

                if (string.IsNullOrWhiteSpace(doseQuantityUnit))
                {
                    result.AddError("Dose quantity unit is required when dose quantity value is specified (SPL IG 16.2.4.3).");
                }

                if (doseQuantityValue.HasValue)
                {
                    // 16.2.4.4 - Value is a number (already validated by decimal type)
                    // 16.2.4.6 - Value may be the number "0"
                    if (doseQuantityValue.Value < 0)
                    {
                        result.AddError("Dose quantity value cannot be negative (SPL IG 16.2.4.4).");
                    }

                    // 16.2.4.7 - Value should not include spaces (check string representation)
                    var valueString = doseQuantityValue.Value.ToString();
                    if (valueString.Contains(' '))
                    {
                        result.AddError("Dose quantity value should not contain spaces (SPL IG 16.2.4.7).");
                    }
                }

                if (!string.IsNullOrWhiteSpace(doseQuantityUnit))
                {
                    // 16.2.4.5 - Unit comes from UCUM units of measures list
                    var unitValidation = ValidateUcumUnit(doseQuantityUnit);
                    result.MergeWith(unitValidation);
                }
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that a unit string conforms to UCUM (Unified Code for Units of Measure) standards.
        /// Ensures pharmaceutical units are standardized and interoperable.
        /// </summary>
        /// <param name="unit">The unit string to validate against UCUM standards.</param>
        /// <returns>A validation result indicating UCUM compliance status.</returns>
        /// <seealso cref="UCUMUnitValidationAttribute"/>
        /// <seealso cref="DosingSpecification"/>
        /// <seealso cref="Ingredient"/>
        /// <seealso cref="Label"/>
        public ValidationResult ValidateUcumUnit(string unit)
        {
            #region implementation
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(unit))
            {
                result.AddError("Unit cannot be null or empty when validating UCUM compliance.");
                return result;
            }

            var trimmedUnit = unit.Trim();

            // Common UCUM units for pharmaceutical products
            var validUnits = getValidUcumUnits();

            if (!validUnits.Contains(trimmedUnit, StringComparer.OrdinalIgnoreCase))
            {
                // Check for complex UCUM patterns
                if (!isValidUcumPattern(trimmedUnit))
                {
                    result.AddError($"Unit '{trimmedUnit}' is not a recognized UCUM unit (SPL IG 16.2.4.5).");
                }
            }

            // Additional validation for unit format
            if (trimmedUnit.Contains(' '))
            {
                result.AddError("UCUM units should not contain spaces.");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the collection of valid UCUM units commonly used in pharmaceutical products.
        /// Provides a centralized list for unit validation across the application.
        /// </summary>
        /// <returns>A HashSet of valid UCUM unit strings.</returns>
        /// <seealso cref="UCUMUnitValidationAttribute"/>
        /// <seealso cref="Label"/>
        private static HashSet<string> getValidUcumUnits()
        {
            #region implementation
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // Mass units
                "g", "kg", "mg", "ug", "ng", "pg",
                // Volume units
                "L", "mL", "uL", "dL", "cL",
                // Count units
                "1", "{count}", "{dose}", "{tablet}", "{capsule}", "{vial}", "{ampule}",
                // Concentration units
                "mg/mL", "g/L", "mg/L", "ug/mL", "mg/g", "g/100g", "mg/100mL",
                // Percent units
                "%", "{v/v}", "{w/w}", "{w/v}",
                // International units
                "IU", "[IU]", "U", "[U]",
                // Other pharmaceutical units
                "meq", "mOsm", "Ci", "Bq", "kBq", "MBq", "GBq"
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates complex UCUM unit patterns that may not be in the standard list.
        /// Handles compound units and special UCUM notation.
        /// </summary>
        /// <param name="unit">The unit string to validate for UCUM pattern compliance.</param>
        /// <returns>True if the unit follows valid UCUM patterns, false otherwise.</returns>
        /// <seealso cref="UCUMUnitValidationAttribute"/>
        /// <seealso cref="Label"/>
        private static bool isValidUcumPattern(string unit)
        {
            #region implementation
            // Basic UCUM pattern validation for common pharmaceutical units
            var ucumPattern = @"^[a-zA-Z0-9\{\}\[\]\/\*\.\-\+\(\)%]+$";

            if (!Regex.IsMatch(unit, ucumPattern))
            {
                return false;
            }

            // Check for common pharmaceutical unit patterns
            var commonPatterns = new[]
            {
                @"^\w+\/\w+$",      // Simple ratios like mg/mL
                @"^\w+\/100\w+$",   // Per 100 units like mg/100mL
                @"^\{\w+\}$",       // Curly brace units like {tablet}
                @"^\[\w+\]$"        // Square bracket units like [IU]
            };

            return commonPatterns.Any(pattern => Regex.IsMatch(unit, pattern));
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Represents the result of a validation operation with success status and error collection.
    /// Provides a standardized way to return validation results across the application.
    /// </summary>
    /// <seealso cref="DosingSpecificationValidationService"/>
    /// <seealso cref="Label"/>
    public class ValidationResult
    {
        #region implementation
        /// <summary>
        /// Gets or sets whether the validation was successful.
        /// </summary>
        public bool IsValid => !Errors.Any();

        /// <summary>
        /// Gets the collection of validation error messages.
        /// </summary>
        public List<string> Errors { get; } = new List<string>();

        /// <summary>
        /// Adds an error message to the validation result.
        /// </summary>
        /// <param name="error">The error message to add.</param>
        public void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                Errors.Add(error);
            }
        }

        /**************************************************************/
        /// <summary>
        /// Merges another validation result into this one.
        /// </summary>
        /// <param name="other">The other validation result to merge.</param>
        public void MergeWith(ValidationResult other)
        {
            if (other?.Errors != null)
            {
                Errors.AddRange(other.Errors);
            }
        }
        #endregion
    }
}