using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MedRecPro.Helpers;
using static MedRecPro.Models.Label;

namespace MedRecPro.Models.Validation
{
    /**************************************************************/
    /// <summary>
    /// Validates that route codes conform to SPL Implementation Guide requirements for route of administration.
    /// Ensures the route code is from the correct FDA SPL code system (2.16.840.1.113883.3.26.1.1) or has a valid nullFlavor.
    /// </summary>
    /// <remarks>
    /// Implements validation rules from SPL IG Section 3.2.20.2f for route of administration codes.
    /// Route codes must be from the FDA SPL code system or include a nullFlavor attribute for unknown routes.
    /// </remarks>
    /// <seealso cref="Label"/>
    /// <seealso cref="DosingSpecification"/>
    /// <seealso cref="ProductRouteOfAdministration"/>
    public class RouteCodeValidationAttribute : ValidationAttribute
    {
        #region implementation
        private static readonly string FdaSplCodeSystem = "2.16.840.1.113883.3.26.1.1";

        /// <summary>
        /// Validates that the route code meets SPL requirements.
        /// </summary>
        /// <param name="value">The route code value to validate.</param>
        /// <param name="validationContext">The validation context containing additional validation information.</param>
        /// <returns>ValidationResult indicating success or failure with error message.</returns>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // Null or empty values are considered valid (handled by Required attribute if needed)
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success;
            }

            string? routeCode = value.ToString()?.Trim();

            // Get the associated code system for validation
            var instance = validationContext.ObjectInstance;
            var routeCodeSystemProperty = instance?.GetType().GetProperty("RouteCodeSystem");
            var routeCodeSystem = routeCodeSystemProperty?.GetValue(instance)?.ToString();

            // Check for nullFlavor property to allow unknown routes
            var nullFlavorProperty = instance?.GetType().GetProperty("RouteNullFlavor");
            var nullFlavor = nullFlavorProperty?.GetValue(instance)?.ToString();

            // If nullFlavor is present, route code validation is relaxed
            if (!string.IsNullOrWhiteSpace(nullFlavor))
            {
                return ValidationResult.Success;
            }

            // Route code system must be the FDA SPL system
            if (string.IsNullOrWhiteSpace(routeCodeSystem) || routeCodeSystem != FdaSplCodeSystem)
            {
                return new ValidationResult(
                    $"Route code system must be {FdaSplCodeSystem} or a nullFlavor must be specified.",
                    new[] { validationContext.MemberName ?? "RouteCode" });
            }

            // Basic format validation for route codes (alphanumeric, no special characters)
            if (!string.IsNullOrWhiteSpace(routeCode) && !Regex.IsMatch(routeCode, @"^[A-Za-z0-9]+$"))
            {
                return new ValidationResult(
                    "Route code must contain only alphanumeric characters.",
                    new[] { validationContext.MemberName ?? "RouteCode" });
            }

            return ValidationResult.Success;
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that numeric values do not contain spaces, as required by SPL Implementation Guide Section 16.2.4.7.
    /// Ensures dose quantity values and similar numeric fields meet SPL formatting requirements.
    /// </summary>
    /// <remarks>
    /// SPL numeric values should not include spaces to ensure proper parsing and compliance.
    /// This validation applies to dose quantities, concentrations, and other numeric specifications.
    /// </remarks>
    /// <seealso cref="Label"/>
    /// <seealso cref="DosingSpecification"/>
    /// <seealso cref="Ingredient"/>
    public class NoSpacesValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Validates that the value does not contain spaces.
        /// </summary>
        /// <param name="value">The value to validate for spaces.</param>
        /// <param name="validationContext">The validation context containing additional validation information.</param>
        /// <returns>ValidationResult indicating success or failure with error message.</returns>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // Null values are considered valid (handled by Required attribute if needed)
            if (value == null)
            {
                return ValidationResult.Success;
            }

            var stringValue = value.ToString();

            // Empty strings are valid
            if (string.IsNullOrEmpty(stringValue))
            {
                return ValidationResult.Success;
            }

            // Check for any whitespace characters
            if (stringValue.Contains(' ') || Regex.IsMatch(stringValue, @"\s"))
            {
                return new ValidationResult(
                    "Value should not contain spaces as per SPL Implementation Guide requirements.",
                    new[] { validationContext.MemberName ?? "Value" });
            }

            return ValidationResult.Success;
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that unit values conform to UCUM (Unified Code for Units of Measure) standards as required by SPL Implementation Guide Section 16.2.4.5.
    /// Ensures dose quantity units and other measurement units are from the approved UCUM units list.
    /// </summary>
    /// <remarks>
    /// UCUM validation ensures interoperability and standardization of units across healthcare systems.
    /// Common valid units include mg, mL, g, L, etc. Complex units like mg/mL are also supported.
    /// </remarks>
    /// <seealso cref="Label"/>
    /// <seealso cref="DosingSpecification"/>
    /// <seealso cref="Ingredient"/>
    /// <seealso cref="Characteristic"/>
    public class UCUMUnitValidationAttribute : ValidationAttribute
    {
        #region implementation
        // Common UCUM units for pharmaceutical products
        private static readonly HashSet<string> validUcumUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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

        /**************************************************************/
        /// <summary>
        /// Validates that the unit is a recognized UCUM unit.
        /// </summary>
        /// <param name="value">The unit value to validate against UCUM standards.</param>
        /// <param name="validationContext">The validation context containing additional validation information.</param>
        /// <returns>ValidationResult indicating success or failure with error message.</returns>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // Null or empty values are considered valid (handled by Required attribute if needed)
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return ValidationResult.Success;
            }

            string? unit = value.ToString()?.Trim();

            // Check against known UCUM units
            if (!string.IsNullOrWhiteSpace(unit) && !validUcumUnits.Contains(unit))
            {
                // Additional validation for complex units using UCUM pattern
                if (!isValidUcumPattern(unit))
                {
                    return new ValidationResult(
                        $"Unit '{unit}' is not a recognized UCUM unit. Please use standard UCUM units of measure.",
                        new[] { validationContext.MemberName ?? "Unit" });
                }
            }

            return ValidationResult.Success;
        }

        /**************************************************************/
        /// <summary>
        /// Validates complex UCUM unit patterns that may not be in the static list.
        /// </summary>
        /// <param name="unit">The unit string to validate.</param>
        /// <returns>True if the unit follows valid UCUM patterns, false otherwise.</returns>
        private static bool isValidUcumPattern(string unit)
        {
            // Basic UCUM pattern validation for common pharmaceutical units
            // This is a simplified check - full UCUM validation would require a more comprehensive parser

            // Allow basic arithmetic operators and parentheses for complex units
            var ucumPattern = @"^[a-zA-Z0-9\{\}\[\]\/\*\.\-\+\(\)%]+$";

            if (!Regex.IsMatch(unit, ucumPattern))
            {
                return false;
            }

            // Check for common pharmaceutical unit patterns
            var commonPatterns = new[]
            {
                @"^\w+\/\w+$",  // Simple ratios like mg/mL
                @"^\w+\/100\w+$",  // Per 100 units like mg/100mL
                @"^\{\w+\}$",  // Curly brace units like {tablet}
                @"^\[\w+\]$"   // Square bracket units like [IU]
            };

            return commonPatterns.Any(pattern => Regex.IsMatch(unit, pattern));
        }
        #endregion
    }
}