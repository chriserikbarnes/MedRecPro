using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using static MedRecProImportClass.Models.Label;

namespace MedRecProImportClass.Models.Validation
{
    /**************************************************************/
    /// <summary>
    /// Validates that the territory code conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.5.5 and 18.1.5.23 requirements.
    /// </summary>
    /// <seealso cref="TerritorialAuthority"/>
    /// <seealso cref="Label"/>
    public class TerritoryCodeValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// ISO 3166-2 code system for US state codes.
        /// </summary>
        private const string Iso31662CodeSystem = "1.0.3166.2";

        /// <summary>
        /// ISO 3166-1 code system for country codes.
        /// </summary>
        private const string Iso31661CodeSystem = "1.0.3166.1.2.3";

        /// <summary>
        /// Validates the territory code against SPL requirements.
        /// </summary>
        /// <param name="value">The territory code value to validate.</param>
        /// <param name="validationContext">The validation context containing the TerritorialAuthority model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var territoryCode = value as string;
            var territorialAuthority = validationContext.ObjectInstance as TerritorialAuthority;

            if (territorialAuthority == null)
            {
                return new ValidationResult("TerritorialAuthority context is required for validation.");
            }

            // Territory code is required
            if (string.IsNullOrWhiteSpace(territoryCode))
            {
                return new ValidationResult("Territory code is required (SPL IG 18.1.5.5).");
            }

            // 18.1.5.23 - If the territory code is "USA", validate as country code
            if (string.Equals(territoryCode, "USA", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(territorialAuthority.TerritoryCodeSystem))
                {
                    return new ValidationResult("Territory code system is required when territory code is specified.");
                }

                if (territorialAuthority.TerritoryCodeSystem != Iso31661CodeSystem)
                {
                    return new ValidationResult($"Territory code system must be {Iso31661CodeSystem} when territory code is 'USA' (SPL IG 18.1.5.23).");
                }

                return ValidationResult.Success;
            }

            // 18.1.5.5 - Validate as ISO 3166-2 US state code (format: US-XX)
            if (!isValidUsStateCode(territoryCode))
            {
                return new ValidationResult($"Territory code '{territoryCode}' must be a valid ISO 3166-2 US state code (format: US-XX) or 'USA' for country code (SPL IG 18.1.5.5).");
            }

            // Validate code system for state codes
            if (string.IsNullOrWhiteSpace(territorialAuthority.TerritoryCodeSystem))
            {
                return new ValidationResult("Territory code system is required when territory code is specified.");
            }

            if (territorialAuthority.TerritoryCodeSystem != Iso31662CodeSystem)
            {
                return new ValidationResult($"Territory code system must be {Iso31662CodeSystem} for US state codes (SPL IG 18.1.5.5).");
            }

            return ValidationResult.Success;
            #endregion
        }

        /// <summary>
        /// Validates if the territory code follows ISO 3166-2 US state format.
        /// </summary>
        /// <param name="territoryCode">The territory code to validate.</param>
        /// <returns>True if valid US state code format, false otherwise.</returns>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="Label"/>
        private static bool isValidUsStateCode(string territoryCode)
        {
            #region implementation
            // US state codes follow format "US-XX" where XX is the 2-letter state abbreviation
            if (string.IsNullOrWhiteSpace(territoryCode) || territoryCode.Length != 5)
            {
                return false;
            }

            return Regex.IsMatch(territoryCode, @"^US-[A-Z]{2}$", RegexOptions.IgnoreCase);
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the territory code system matches the territory code type according to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.5.5 and 18.1.5.23 requirements.
    /// </summary>
    /// <seealso cref="TerritorialAuthority"/>
    /// <seealso cref="Label"/>
    public class TerritoryCodeSystemValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// ISO 3166-2 code system for US state codes.
        /// </summary>
        private const string Iso31662CodeSystem = "1.0.3166.2";

        /// <summary>
        /// ISO 3166-1 code system for country codes.
        /// </summary>
        private const string Iso31661CodeSystem = "1.0.3166.1.2.3";

        /// <summary>
        /// Valid code systems for territorial authority.
        /// </summary>
        private static readonly HashSet<string> ValidCodeSystems = new(StringComparer.OrdinalIgnoreCase)
        {
            Iso31662CodeSystem,
            Iso31661CodeSystem
        };

        /// <summary>
        /// Validates the territory code system against SPL requirements.
        /// </summary>
        /// <param name="value">The territory code system value to validate.</param>
        /// <param name="validationContext">The validation context containing the TerritorialAuthority model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var territoryCodeSystem = value as string;
            var territorialAuthority = validationContext.ObjectInstance as TerritorialAuthority;

            if (territorialAuthority == null)
            {
                return new ValidationResult("TerritorialAuthority context is required for validation.");
            }

            // Territory code system is required when territory code is specified
            if (!string.IsNullOrWhiteSpace(territorialAuthority.TerritoryCode) && string.IsNullOrWhiteSpace(territoryCodeSystem))
            {
                return new ValidationResult("Territory code system is required when territory code is specified.");
            }

            // Skip validation if both are empty
            if (string.IsNullOrWhiteSpace(territoryCodeSystem) && string.IsNullOrWhiteSpace(territorialAuthority.TerritoryCode))
            {
                return ValidationResult.Success;
            }

            // Validate code system is from approved list
            if (!string.IsNullOrWhiteSpace(territoryCodeSystem) && !ValidCodeSystems.Contains(territoryCodeSystem))
            {
                return new ValidationResult($"Territory code system '{territoryCodeSystem}' must be either {Iso31662CodeSystem} for state codes or {Iso31661CodeSystem} for country codes (SPL IG 18.1.5.5, 18.1.5.23).");
            }

            // Validate code system matches territory code type
            if (!string.IsNullOrWhiteSpace(territorialAuthority.TerritoryCode))
            {
                var isUsaCountryCode = string.Equals(territorialAuthority.TerritoryCode, "USA", StringComparison.OrdinalIgnoreCase);

                if (isUsaCountryCode && territoryCodeSystem != Iso31661CodeSystem)
                {
                    return new ValidationResult($"Territory code system must be {Iso31661CodeSystem} when territory code is 'USA' (SPL IG 18.1.5.23).");
                }

                if (!isUsaCountryCode && territoryCodeSystem != Iso31662CodeSystem)
                {
                    return new ValidationResult($"Territory code system must be {Iso31662CodeSystem} for US state codes (SPL IG 18.1.5.5).");
                }
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the governing agency DUNS number conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.5.24, 18.1.5.26 requirements.
    /// </summary>
    /// <seealso cref="TerritorialAuthority"/>
    /// <seealso cref="Label"/>
    public class GoverningAgencyDunsNumberValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// DEA DUNS number as specified in SPL IG 18.1.5.26.
        /// </summary>
        private const string DeaDunsNumber = "004234790";

        /// <summary>
        /// Validates the governing agency DUNS number against SPL requirements.
        /// </summary>
        /// <param name="value">The DUNS number value to validate.</param>
        /// <param name="validationContext">The validation context containing the TerritorialAuthority model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var dunsNumber = value as string;
            var territorialAuthority = validationContext.ObjectInstance as TerritorialAuthority;

            if (territorialAuthority == null)
            {
                return new ValidationResult("TerritorialAuthority context is required for validation.");
            }

            var territoryCode = territorialAuthority.TerritoryCode;
            var isUsaTerritory = string.Equals(territoryCode, "USA", StringComparison.OrdinalIgnoreCase);

            // 18.1.5.24 - If territory is "USA", DUNS number is required
            if (isUsaTerritory && string.IsNullOrWhiteSpace(dunsNumber))
            {
                return new ValidationResult("Governing agency DUNS number is required when territory code is 'USA' (SPL IG 18.1.5.24).");
            }

            // 18.1.5.25 - If territory is not "USA", no governing agency specified
            if (!isUsaTerritory && !string.IsNullOrWhiteSpace(dunsNumber))
            {
                return new ValidationResult("Governing agency DUNS number must not be specified when territory code is not 'USA' (SPL IG 18.1.5.25).");
            }

            // Validate DUNS number format if specified
            if (!string.IsNullOrWhiteSpace(dunsNumber))
            {
                // DUNS numbers are 9 digits
                if (!Regex.IsMatch(dunsNumber, @"^\d{9}$"))
                {
                    return new ValidationResult("Governing agency DUNS number must be exactly 9 digits.");
                }

                // 18.1.5.26 - If agency is DEA, validate specific DUNS number
                var agencyName = territorialAuthority.GoverningAgencyName;
                if (!string.IsNullOrWhiteSpace(agencyName) &&
                    string.Equals(agencyName, "DEA", StringComparison.OrdinalIgnoreCase) &&
                    dunsNumber != DeaDunsNumber)
                {
                    return new ValidationResult($"DEA governing agency must have DUNS number {DeaDunsNumber} (SPL IG 18.1.5.26).");
                }
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the governing agency ID root conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.5.24 requirements.
    /// </summary>
    /// <seealso cref="TerritorialAuthority"/>
    /// <seealso cref="Label"/>
    public class GoverningAgencyIdRootValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// Required root OID for governing agency IDs.
        /// </summary>
        private const string RequiredIdRoot = "1.3.6.1.4.1.519.1";

        /// <summary>
        /// Validates the governing agency ID root against SPL requirements.
        /// </summary>
        /// <param name="value">The ID root value to validate.</param>
        /// <param name="validationContext">The validation context containing the TerritorialAuthority model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var idRoot = value as string;
            var territorialAuthority = validationContext.ObjectInstance as TerritorialAuthority;

            if (territorialAuthority == null)
            {
                return new ValidationResult("TerritorialAuthority context is required for validation.");
            }

            var territoryCode = territorialAuthority.TerritoryCode;
            var isUsaTerritory = string.Equals(territoryCode, "USA", StringComparison.OrdinalIgnoreCase);

            // 18.1.5.24 - If territory is "USA", ID root is required
            if (isUsaTerritory && string.IsNullOrWhiteSpace(idRoot))
            {
                return new ValidationResult("Governing agency ID root is required when territory code is 'USA' (SPL IG 18.1.5.24).");
            }

            // 18.1.5.25 - If territory is not "USA", no governing agency specified
            if (!isUsaTerritory && !string.IsNullOrWhiteSpace(idRoot))
            {
                return new ValidationResult("Governing agency ID root must not be specified when territory code is not 'USA' (SPL IG 18.1.5.25).");
            }

            // Validate ID root value if specified
            if (!string.IsNullOrWhiteSpace(idRoot))
            {
                if (idRoot != RequiredIdRoot)
                {
                    return new ValidationResult($"Governing agency ID root must be {RequiredIdRoot} (SPL IG 18.1.5.24).");
                }
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that the governing agency name conforms to SPL Implementation Guide requirements.
    /// Implements SPL Implementation Guide Section 18.1.5.24, 18.1.5.26 requirements.
    /// </summary>
    /// <seealso cref="TerritorialAuthority"/>
    /// <seealso cref="Label"/>
    public class GoverningAgencyNameValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// DEA DUNS number as specified in SPL IG 18.1.5.26.
        /// </summary>
        private const string DeaDunsNumber = "004234790";

        /// <summary>
        /// Validates the governing agency name against SPL requirements.
        /// </summary>
        /// <param name="value">The agency name value to validate.</param>
        /// <param name="validationContext">The validation context containing the TerritorialAuthority model.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var agencyName = value as string;
            var territorialAuthority = validationContext.ObjectInstance as TerritorialAuthority;

            if (territorialAuthority == null)
            {
                return new ValidationResult("TerritorialAuthority context is required for validation.");
            }

            var territoryCode = territorialAuthority.TerritoryCode;
            var isUsaTerritory = string.Equals(territoryCode, "USA", StringComparison.OrdinalIgnoreCase);

            // 18.1.5.24 - If territory is "USA", agency name is required
            if (isUsaTerritory && string.IsNullOrWhiteSpace(agencyName))
            {
                return new ValidationResult("Governing agency name is required when territory code is 'USA' (SPL IG 18.1.5.24).");
            }

            // 18.1.5.25 - If territory is not "USA", no governing agency specified
            if (!isUsaTerritory && !string.IsNullOrWhiteSpace(agencyName))
            {
                return new ValidationResult("Governing agency name must not be specified when territory code is not 'USA' (SPL IG 18.1.5.25).");
            }

            // 18.1.5.26 - If DUNS number is DEA, validate agency name
            if (!string.IsNullOrWhiteSpace(agencyName))
            {
                var dunsNumber = territorialAuthority.GoverningAgencyIdExtension;
                if (!string.IsNullOrWhiteSpace(dunsNumber) &&
                    dunsNumber == DeaDunsNumber &&
                    !string.Equals(agencyName, "DEA", StringComparison.OrdinalIgnoreCase))
                {
                    return new ValidationResult($"Governing agency with DUNS number {DeaDunsNumber} must have name 'DEA' (SPL IG 18.1.5.26).");
                }

                // Basic format validation
                if (agencyName.Contains('<') || agencyName.Contains('>') || agencyName.Contains('&'))
                {
                    return new ValidationResult("Governing agency name contains invalid characters.");
                }
            }

            return ValidationResult.Success;
            #endregion
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates that all territorial authority properties are consistent with each other according to SPL Implementation Guide requirements.
    /// Implements comprehensive validation across SPL Implementation Guide Section 18.1.5.5, 18.1.5.23-18.1.5.26 requirements.
    /// </summary>
    /// <seealso cref="TerritorialAuthority"/>
    /// <seealso cref="Label"/>
    public class TerritorialAuthorityConsistencyValidationAttribute : ValidationAttribute
    {
        #region implementation
        /// <summary>
        /// DEA DUNS number as specified in SPL IG 18.1.5.26.
        /// </summary>
        private const string DeaDunsNumber = "004234790";

        /// <summary>
        /// Required root OID for governing agency IDs.
        /// </summary>
        private const string RequiredIdRoot = "1.3.6.1.4.1.519.1";

        /// <summary>
        /// Validates the overall consistency of territorial authority properties against SPL requirements.
        /// </summary>
        /// <param name="value">The territorial authority object to validate.</param>
        /// <param name="validationContext">The validation context containing validation information.</param>
        /// <returns>ValidationResult indicating success or specific validation errors.</returns>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="Label"/>
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            #region implementation
            var territorialAuthority = value as TerritorialAuthority;

            if (territorialAuthority == null)
            {
                return ValidationResult.Success; // Let other validators handle null cases
            }

            var errors = new List<string>();

            // Validate territory code and code system consistency
            validateTerritoryCodeConsistency(territorialAuthority, errors);

            // Validate governing agency consistency
            validateGoverningAgencyConsistency(territorialAuthority, errors);

            // Validate DEA-specific requirements
            validateDeaSpecificRequirements(territorialAuthority, errors);

            if (errors.Any())
            {
                return new ValidationResult(string.Join(" ", errors));
            }

            return ValidationResult.Success;
            #endregion
        }

        /// <summary>
        /// Validates consistency between territory code and territory code system.
        /// </summary>
        /// <param name="territorialAuthority">The territorial authority to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="Label"/>
        private static void validateTerritoryCodeConsistency(TerritorialAuthority territorialAuthority, List<string> errors)
        {
            #region implementation
            var territoryCode = territorialAuthority.TerritoryCode;
            var territoryCodeSystem = territorialAuthority.TerritoryCodeSystem;

            // Both should be specified or both should be empty
            var hasCode = !string.IsNullOrWhiteSpace(territoryCode);
            var hasCodeSystem = !string.IsNullOrWhiteSpace(territoryCodeSystem);

            if (hasCode && !hasCodeSystem)
            {
                errors.Add("Territory code system is required when territory code is specified.");
            }

            if (!hasCode && hasCodeSystem)
            {
                errors.Add("Territory code is required when territory code system is specified.");
            }
            #endregion
        }

        /// <summary>
        /// Validates consistency of governing agency properties with territory code.
        /// </summary>
        /// <param name="territorialAuthority">The territorial authority to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="Label"/>
        private static void validateGoverningAgencyConsistency(TerritorialAuthority territorialAuthority, List<string> errors)
        {
            #region implementation
            var territoryCode = territorialAuthority.TerritoryCode;
            var hasIdExtension = !string.IsNullOrWhiteSpace(territorialAuthority.GoverningAgencyIdExtension);
            var hasIdRoot = !string.IsNullOrWhiteSpace(territorialAuthority.GoverningAgencyIdRoot);
            var hasName = !string.IsNullOrWhiteSpace(territorialAuthority.GoverningAgencyName);

            if (string.IsNullOrWhiteSpace(territoryCode))
            {
                return; // Skip if no territory code specified
            }

            var isUsaTerritory = string.Equals(territoryCode, "USA", StringComparison.OrdinalIgnoreCase);

            // 18.1.5.24 - USA territory requires all governing agency properties
            if (isUsaTerritory)
            {
                if (!hasIdExtension)
                {
                    errors.Add("Governing agency DUNS number is required for USA territory (SPL IG 18.1.5.24).");
                }
                if (!hasIdRoot)
                {
                    errors.Add("Governing agency ID root is required for USA territory (SPL IG 18.1.5.24).");
                }
                if (!hasName)
                {
                    errors.Add("Governing agency name is required for USA territory (SPL IG 18.1.5.24).");
                }
            }
            // 18.1.5.25 - Non-USA territories must not have governing agency
            else
            {
                if (hasIdExtension)
                {
                    errors.Add("Governing agency DUNS number must not be specified for non-USA territories (SPL IG 18.1.5.25).");
                }
                if (hasIdRoot)
                {
                    errors.Add("Governing agency ID root must not be specified for non-USA territories (SPL IG 18.1.5.25).");
                }
                if (hasName)
                {
                    errors.Add("Governing agency name must not be specified for non-USA territories (SPL IG 18.1.5.25).");
                }
            }

            // If any governing agency property is specified, all should be consistent
            if (hasIdExtension || hasIdRoot || hasName)
            {
                if (!hasIdExtension || !hasIdRoot || !hasName)
                {
                    errors.Add("All governing agency properties (DUNS number, ID root, and name) must be specified together.");
                }

                // Validate ID root value
                if (hasIdRoot && territorialAuthority.GoverningAgencyIdRoot != RequiredIdRoot)
                {
                    errors.Add($"Governing agency ID root must be {RequiredIdRoot} (SPL IG 18.1.5.24).");
                }
            }
            #endregion
        }

        /// <summary>
        /// Validates DEA-specific requirements according to SPL Implementation Guide.
        /// </summary>
        /// <param name="territorialAuthority">The territorial authority to validate.</param>
        /// <param name="errors">List to collect validation errors.</param>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="Label"/>
        private static void validateDeaSpecificRequirements(TerritorialAuthority territorialAuthority, List<string> errors)
        {
            #region implementation
            var dunsNumber = territorialAuthority.GoverningAgencyIdExtension;
            var agencyName = territorialAuthority.GoverningAgencyName;

            // 18.1.5.26 - DEA specific validations
            if (!string.IsNullOrWhiteSpace(dunsNumber) && dunsNumber == DeaDunsNumber)
            {
                if (string.IsNullOrWhiteSpace(agencyName) || !string.Equals(agencyName, "DEA", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Governing agency with DUNS number {DeaDunsNumber} must have name 'DEA' (SPL IG 18.1.5.26).");
                }
            }

            if (!string.IsNullOrWhiteSpace(agencyName) && string.Equals(agencyName, "DEA", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(dunsNumber) || dunsNumber != DeaDunsNumber)
                {
                    errors.Add($"DEA governing agency must have DUNS number {DeaDunsNumber} (SPL IG 18.1.5.26).");
                }
            }
            #endregion
        }
        #endregion
    }
}