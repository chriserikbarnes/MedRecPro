using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using static MedRecProImportClass.Models.Label;

namespace MedRecProImportClass.Models.Validation
{
    /**************************************************************/
    /// <summary>
    /// Validates that the license type code conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.5.2-18.1.5.4 requirements.
    /// </summary>
    /// <seealso cref="License"/>
    /// <seealso cref="Label"/>
    public class LicenseTypeCodeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// FDA SPL code system for license type codes.
        /// </summary>
        private const string FdaSplCodeSystem = "2.16.840.1.113883.3.26.1.1";

        /// <summary>
        /// Valid license type codes with their display names.
        /// </summary>
        private static readonly Dictionary<string, string> ValidLicenseTypeCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["C118777"] = "licensing"
        };

        /// <summary>
        /// Validates the license type code against FDA SPL requirements.
        /// </summary>
        /// <param name="value">The license type code value to validate.</param>
        /// <param name="validationContext">The validation context containing the License model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="License"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var licenseTypeCode = value as string;
            var license = validationContext.ObjectInstance as License;

            if (license == null)
            {
                return new ValidationResult("License context is required for validation.");
            }

            // License type code is not required but if present must be valid
            if (string.IsNullOrWhiteSpace(licenseTypeCode))
            {
                return ValidationResult.Success;
            }

            // 18.1.5.4 - Code system is 2.16.840.1.113883.3.26.1.1
            if (string.IsNullOrWhiteSpace(license.LicenseTypeCodeSystem))
            {
                return new ValidationResult("License type code system is required when license type code is specified.");
            }

            if (license.LicenseTypeCodeSystem != FdaSplCodeSystem)
            {
                return new ValidationResult($"License type code system must be {FdaSplCodeSystem} for FDA SPL compliance (SPL IG 18.1.5.4).");
            }

            // 18.1.5.2 - License Code is from the License Type Code list
            if (!ValidLicenseTypeCodes.ContainsKey(licenseTypeCode))
            {
                return new ValidationResult($"License type code '{licenseTypeCode}' is not from the License Type Code list (SPL IG 18.1.5.2).");
            }

            // 18.1.5.3 - Display name matches the code
            var expectedDisplayName = ValidLicenseTypeCodes[licenseTypeCode];
            if (!string.IsNullOrWhiteSpace(license.LicenseTypeDisplayName) &&
                !string.Equals(license.LicenseTypeDisplayName, expectedDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult($"License type display name '{license.LicenseTypeDisplayName}' does not match expected '{expectedDisplayName}' (SPL IG 18.1.5.3).");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the license status code conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.5.9, 18.1.5.13-18.1.5.14 requirements.
    /// </summary>
    /// <seealso cref="License"/>
    /// <seealso cref="Label"/>
    public class LicenseStatusCodeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Valid status codes for licenses.
        /// </summary>
        private static readonly HashSet<string> ValidStatusCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "active",
            "suspended",
            "aborted",   // meaning "revoked"
            "completed"  // meaning "expired"
        };

        /// <summary>
        /// Validates the license status code against SPL requirements.
        /// </summary>
        /// <param name="value">The status code value to validate.</param>
        /// <param name="validationContext">The validation context containing the License model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="License"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var statusCode = value as string;
            var license = validationContext.ObjectInstance as License;

            if (license == null)
            {
                return new ValidationResult("License context is required for validation.");
            }

            // Status code is required
            if (string.IsNullOrWhiteSpace(statusCode))
            {
                return new ValidationResult("License status code is required (SPL IG 18.1.5.9).");
            }

            // 18.1.5.9 - License has a status code with values active, suspended, aborted (revoked), or completed (expired)
            if (!ValidStatusCodes.Contains(statusCode))
            {
                return new ValidationResult($"License status code '{statusCode}' must be one of: active, suspended, aborted, completed (SPL IG 18.1.5.9).");
            }

            // 18.1.5.13 - If the status code is completed, then the current date is later than the effective time high value
            if (string.Equals(statusCode, "completed", StringComparison.OrdinalIgnoreCase))
            {
                if (license.ExpirationDate.HasValue && license.ExpirationDate.Value >= DateTime.Today)
                {
                    return new ValidationResult("License with status 'completed' must have an expiration date in the past (SPL IG 18.1.5.13).");
                }
            }

            // 18.1.5.14 - If the current date is later than the effective time high value, then the status code is not active
            if (license.ExpirationDate.HasValue && license.ExpirationDate.Value < DateTime.Today)
            {
                if (string.Equals(statusCode, "active", StringComparison.OrdinalIgnoreCase))
                {
                    return new ValidationResult("License with expiration date in the past cannot have status 'active' (SPL IG 18.1.5.14).");
                }
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the license number conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.5.6-18.1.5.7 requirements.
    /// </summary>
    /// <seealso cref="License"/>
    /// <seealso cref="Label"/>
    public class LicenseNumberValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Validates the license number against SPL requirements.
        /// </summary>
        /// <param name="value">The license number value to validate.</param>
        /// <param name="validationContext">The validation context containing the License model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="License"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var licenseNumber = value as string;

            // 18.1.5.6 - License has an id with the license number
            // 18.1.5.7 - id has an extension, the license number
            if (string.IsNullOrWhiteSpace(licenseNumber))
            {
                return new ValidationResult("License number is required (SPL IG 18.1.5.6, 18.1.5.7).");
            }

            // Basic format validation - no special characters that could cause issues
            if (licenseNumber.Contains('<') || licenseNumber.Contains('>') || licenseNumber.Contains('&'))
            {
                return new ValidationResult("License number contains invalid characters.");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the license root OID conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.5.8, 18.1.5.16-18.1.5.22, 18.1.5.27 requirements.
    /// </summary>
    /// <seealso cref="License"/>
    /// <seealso cref="Label"/>
    public class LicenseRootOIDValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Base OID for state-issued licenses.
        /// </summary>
        private const string StateOidBase = "1.3.6.1.4.1.32366.4.840";

        /// <summary>
        /// DEA license OID.
        /// </summary>
        private const string DeaOid = "1.3.6.1.4.1.32366.4.840.1";

        /// <summary>
        /// Valid OID suffixes for different business operations and locations.
        /// </summary>
        private static readonly HashSet<string> ValidSuffixes = new(StringComparer.OrdinalIgnoreCase)
        {
            ".2", // Third-Party Logistics Provider in licensing state
            ".3", // Wholesale Drug Distributor in licensing state  
            ".4", // Third-Party Logistics Provider out of licensing state
            ".5"  // Wholesale Drug Distributor out of licensing state
        };

        /// <summary>
        /// Validates the license root OID against SPL requirements.
        /// </summary>
        /// <param name="value">The root OID value to validate.</param>
        /// <param name="validationContext">The validation context containing the License model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="License"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var rootOid = value as string;

            if (string.IsNullOrWhiteSpace(rootOid))
            {
                return new ValidationResult("License root OID is required.");
            }

            // 18.1.5.27 - If the issuing governing agency is DEA then the license id root OID is 1.3.6.1.4.1.32366.4.840.1
            if (rootOid == DeaOid)
            {
                return ValidationResult.Success; // DEA OID is valid
            }

            // 18.1.5.8 - State license OID validation
            if (!rootOid.StartsWith(StateOidBase, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult($"License root OID must start with {StateOidBase} for state licenses or be {DeaOid} for DEA licenses (SPL IG 18.1.5.8, 18.1.5.27).");
            }

            // Extract the state code portion and suffix
            var afterBase = rootOid.Substring(StateOidBase.Length);
            if (string.IsNullOrEmpty(afterBase) || !afterBase.StartsWith("."))
            {
                return new ValidationResult("License root OID must include a state code after the base OID (SPL IG 18.1.5.8).");
            }

            var parts = afterBase.Split('.');
            if (parts.Length < 2)
            {
                return new ValidationResult("License root OID format is invalid (SPL IG 18.1.5.8).");
            }

            // Validate state code is numeric (converted from base 36)
            if (!int.TryParse(parts[1], out var stateCode) || stateCode <= 0)
            {
                return new ValidationResult("License root OID state code must be a valid positive number (SPL IG 18.1.5.8).");
            }

            // 18.1.5.16, 18.1.5.22 - Validate suffix if present
            if (parts.Length > 2)
            {
                var suffix = "." + parts[2];
                if (!ValidSuffixes.Contains(suffix))
                {
                    return new ValidationResult($"License root OID suffix '{suffix}' is not valid. Must be .2, .3, .4, or .5 (SPL IG 18.1.5.16, 18.1.5.22).");
                }
            }

            // Basic OID format validation
            if (!Regex.IsMatch(rootOid, @"^[\d\.]+$"))
            {
                return new ValidationResult("License root OID must contain only digits and periods.");
            }

            return ValidationResult.Success;
            #endregion
        }


        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the license expiration date conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.5.10-18.1.5.12 requirements.
    /// </summary>
    /// <seealso cref="License"/>
    /// <seealso cref="Label"/>
    public class LicenseExpirationDateValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Validates the license expiration date against SPL requirements.
        /// </summary>
        /// <param name="value">The expiration date value to validate.</param>
        /// <param name="validationContext">The validation context containing the License model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="License"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var expirationDate = value as DateTime?;

            // 18.1.5.10 - License has an effective time high value (expiration date)
            if (!expirationDate.HasValue)
            {
                return new ValidationResult("License expiration date is required (SPL IG 18.1.5.10).");
            }

            // 18.1.5.12 - The effective time high boundary has at least the precision of day in the format YYYYMMDD
            var dateString = expirationDate.Value.ToString("yyyyMMdd");
            if (dateString.Length != 8)
            {
                return new ValidationResult("License expiration date must have day precision in YYYYMMDD format (SPL IG 18.1.5.12).");
            }

            // Validate the date is reasonable (not too far in the past or future)
            var currentDate = DateTime.Today;
            var minDate = currentDate.AddYears(-50);
            var maxDate = currentDate.AddYears(50);

            if (expirationDate.Value < minDate || expirationDate.Value > maxDate)
            {
                return new ValidationResult("License expiration date must be within a reasonable range.");
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }


}