using System.Xml.Linq;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Data;
using Microsoft.EntityFrameworkCore;
using static MedRecPro.Models.Label;
using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses license information and territorial authorities from SPL approval elements.
    /// Handles both state-issued licenses and federal licenses (like DEA) according to 
    /// SPL Implementation Guide Section 18.1.5.
    /// </summary>
    /// <remarks>
    /// This parser extracts license data from approval elements within business operations,
    /// creating both License and TerritorialAuthority entities. It supports both state
    /// licensing authorities (using ISO 3166-2 codes) and federal agencies (using DUNS
    /// numbers for identification). All data is validated against SPL Implementation Guide
    /// requirements before being saved to the database.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="License"/>
    /// <seealso cref="TerritorialAuthority"/>
    /// <seealso cref="BusinessOperation"/>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="Label"/>
    public class LicenseParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        public string SectionName => "license";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses approval elements to extract license and territorial authority information.
        /// </summary>
        /// <param name="element">The XElement containing approval elements to parse.</param>
        /// <param name="context">The current parsing context, which must contain the CurrentBusinessOperation.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and the count of created entities.</returns>
        /// <example>
        /// <code>
        /// var licenseParser = new LicenseParser();
        /// var result = await licenseParser.ParseAsync(approvalElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Licenses created: {result.LicensesCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method processes approval elements within business operations to create:
        /// 1. TerritorialAuthority entities for licensing jurisdictions
        /// 2. License entities linked to the current business operation
        /// 
        /// The method handles both state and federal licensing scenarios as specified
        /// in SPL Implementation Guide Section 18.1.5. All entities are validated before
        /// being saved to ensure compliance with SPL requirements.
        /// </remarks>
        /// <seealso cref="License"/>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate that the context contains a current business operation
            if (context?.CurrentBusinessOperation?.BusinessOperationID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse licenses without a valid business operation context.");
                context?.Logger?.LogError("LicenseParser was called without a valid business operation in the context.");
                return result;
            }

            if (context.ServiceProvider == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse licenses due to invalid service provider or logger context.");
                return result;
            }

            try
            {
                reportProgress?.Invoke($"Starting License XML Elements {context.FileNameInZip}");

                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var licenseCount = await parseAndSaveLicensesAsync(element, context.CurrentBusinessOperation, dbContext, context.Logger);

                result.LicensesCreated += licenseCount;
                result.Success = true;

                reportProgress?.Invoke($"Completed License XML Elements {context.FileNameInZip}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing licenses: {ex.Message}");
                context.Logger.LogError(ex, "Error processing license elements.");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves license entities from approval elements within a business operation.
        /// </summary>
        /// <param name="parentEl">The parent XML element containing approval elements.</param>
        /// <param name="businessOperation">The business operation to associate licenses with.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="logger">Logger for information and warning messages.</param>
        /// <returns>The number of licenses created.</returns>
        /// <remarks>
        /// This method searches for approval elements that contain licensing information
        /// and creates both the associated territorial authority and license records.
        /// It follows SPL Implementation Guide Section 18.1.5 for license structure and
        /// validates all data before saving to the database.
        /// </remarks>
        /// <seealso cref="License"/>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveLicensesAsync(
            XElement parentEl,
            BusinessOperation businessOperation,
            ApplicationDbContext dbContext,
            ILogger logger)
        {
            #region implementation
            int licensesCreated = 0;

            // Find all approval elements that represent licenses
            foreach (var approvalEl in parentEl.SplElements(sc.E.SubjectOf, sc.E.Approval))
            {
                try
                {
                    // Check if this approval element represents a license
                    var licenseTypeCodeEl = approvalEl.GetSplElement(sc.E.Code);
                    if (licenseTypeCodeEl == null) continue;

                    string? licenseTypeCode = licenseTypeCodeEl.GetAttrVal(sc.A.CodeValue);

                    // Only process licensing approvals (C118777 = licensing)
                    if (string.IsNullOrWhiteSpace(licenseTypeCode) ||
                        !string.Equals(licenseTypeCode, "C118777", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var license = await parseLicenseFromApprovalAsync(approvalEl, businessOperation, dbContext, logger);
                    if (license?.LicenseID != null)
                    {
                        licensesCreated++;
                        logger.LogInformation($"Created License ID {license.LicenseID} for BusinessOperation {businessOperation.BusinessOperationID}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error parsing individual license from approval element.");
                }
            }

            return licensesCreated;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a license entity from an approval XML element.
        /// </summary>
        /// <param name="approvalEl">The approval XML element containing license information.</param>
        /// <param name="businessOperation">The business operation to associate the license with.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="logger">Logger for information and warning messages.</param>
        /// <returns>The created or existing License entity, or null if parsing failed.</returns>
        /// <remarks>
        /// This method extracts license metadata from the approval element and creates
        /// the associated territorial authority. It follows SPL Implementation Guide 
        /// Section 18.1.5 for license data structure and validates all data before saving.
        /// </remarks>
        /// <seealso cref="License"/>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="BusinessOperation"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<License?> parseLicenseFromApprovalAsync(
            XElement approvalEl,
            BusinessOperation businessOperation,
            ApplicationDbContext dbContext,
            ILogger logger)
        {
            #region implementation
            try
            {
                // Extract license identification information
                var idEl = approvalEl.GetSplElement(sc.E.Id);
                string? licenseNumber = idEl?.GetAttrVal(sc.A.Extension);
                string? licenseRootOid = idEl?.GetAttrVal(sc.A.Root);

                if (string.IsNullOrWhiteSpace(licenseNumber) || string.IsNullOrWhiteSpace(licenseRootOid))
                {
                    logger.LogWarning("License missing required identification information (number or root OID).");
                    return null;
                }

                // Extract license type information
                var codeEl = approvalEl.GetSplElement(sc.E.Code);
                string? licenseTypeCode = codeEl?.GetAttrVal(sc.A.CodeValue);
                string? licenseTypeCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
                string? licenseTypeDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);

                // Extract license status
                var statusCodeEl = approvalEl.GetSplElement(sc.E.StatusCode);
                string? statusCode = statusCodeEl?.GetAttrVal(sc.A.CodeValue);

                // Extract expiration date
                DateTime? expirationDate = extractExpirationDate(approvalEl, logger);

                // Parse territorial authority from author element
                var territorialAuthority = await parseTerritorialAuthorityAsync(approvalEl, dbContext, logger);
                if (territorialAuthority?.TerritorialAuthorityID == null)
                {
                    logger.LogWarning("Could not create or find territorial authority for license.");
                    return null;
                }

                // Create or get existing license
                var license = await getOrCreateLicenseAsync(
                    dbContext,
                    businessOperation.BusinessOperationID,
                    territorialAuthority.TerritorialAuthorityID,
                    licenseNumber,
                    licenseRootOid,
                    licenseTypeCode,
                    licenseTypeCodeSystem,
                    licenseTypeDisplayName,
                    statusCode,
                    expirationDate,
                    logger);

                return license;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error parsing license from approval element.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a territorial authority entity from the author element of an approval.
        /// </summary>
        /// <param name="approvalEl">The approval XML element containing author/territorialAuthority information.</param>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="logger">Logger for information and warning messages.</param>
        /// <returns>The created or existing TerritorialAuthority entity, or null if parsing failed.</returns>
        /// <remarks>
        /// This method handles both state territorial authorities (using ISO 3166-2 codes)
        /// and federal agencies (using DUNS numbers). It follows SPL Implementation Guide
        /// Section 18.1.5.5 and 18.1.5.23-18.1.5.26 requirements and validates all data
        /// before saving to the database.
        /// </remarks>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<TerritorialAuthority?> parseTerritorialAuthorityAsync(
            XElement approvalEl,
            ApplicationDbContext dbContext,
            ILogger logger)
        {
            #region implementation
            try
            {
                var authorEl = approvalEl.GetSplElement(sc.E.Author);
                var territorialAuthorityEl = authorEl?.GetSplElement(sc.E.TerritorialAuthority);

                if (territorialAuthorityEl == null)
                {
                    logger.LogWarning("No territorial authority element found in approval author.");
                    return null;
                }

                // Extract territory information
                var territoryEl = territorialAuthorityEl.GetSplElement(sc.E.Territory);
                var territoryCodeEl = territoryEl?.GetSplElement(sc.E.Code);

                string? territoryCode = territoryCodeEl?.GetAttrVal(sc.A.CodeValue);
                string? territoryCodeSystem = territoryCodeEl?.GetAttrVal(sc.A.CodeSystem);

                if (string.IsNullOrWhiteSpace(territoryCode) || string.IsNullOrWhiteSpace(territoryCodeSystem))
                {
                    logger.LogWarning("Territory code or code system missing from territorial authority.");
                    return null;
                }

                // Extract governing agency information (for federal licenses)
                string? governingAgencyDuns = null;
                string? governingAgencyIdRoot = null;
                string? governingAgencyName = null;

                var governingAgencyEl = territorialAuthorityEl.GetSplElement(sc.E.GoverningAgency);
                if (governingAgencyEl != null)
                {
                    var agencyIdEl = governingAgencyEl.GetSplElement(sc.E.Id);
                    governingAgencyDuns = agencyIdEl?.GetAttrVal(sc.A.Extension);
                    governingAgencyIdRoot = agencyIdEl?.GetAttrVal(sc.A.Root);
                    governingAgencyName = governingAgencyEl.GetSplElementVal(sc.E.Name);
                }

                // Create or get existing territorial authority with validation
                var territorialAuthority = await getOrCreateTerritorialAuthorityAsync(
                    dbContext,
                    territoryCode,
                    territoryCodeSystem,
                    governingAgencyDuns,
                    governingAgencyIdRoot,
                    governingAgencyName,
                    logger);

                return territorialAuthority;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error parsing territorial authority from approval element.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the license expiration date from the effective time element.
        /// </summary>
        /// <param name="approvalEl">The approval XML element containing effective time information.</param>
        /// <param name="logger">Logger for warning messages.</param>
        /// <returns>The parsed expiration date, or null if not found or invalid.</returns>
        /// <remarks>
        /// This method extracts the high value from the effective time element, which
        /// represents the license expiration date according to SPL Implementation Guide
        /// Section 18.1.5.10-18.1.5.12.
        /// </remarks>
        /// <seealso cref="Label"/>
        private DateTime? extractExpirationDate(XElement approvalEl, ILogger logger)
        {
            #region implementation
            try
            {
                var effectiveTimeEl = approvalEl.GetSplElement(sc.E.EffectiveTime);
                var highEl = effectiveTimeEl?.GetSplElement(sc.E.High);
                string? expirationValue = highEl?.GetAttrVal(sc.A.Value);

                if (string.IsNullOrWhiteSpace(expirationValue))
                {
                    logger.LogWarning("No expiration date found in license effective time.");
                    return null;
                }

                // SPL dates are typically in YYYYMMDD format
                if (expirationValue.Length >= 8 &&
                    DateTime.TryParseExact(expirationValue.Substring(0, 8), "yyyyMMdd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime expirationDate))
                {
                    return expirationDate;
                }

                logger.LogWarning($"Could not parse expiration date: {expirationValue}");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error extracting expiration date from approval element.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing TerritorialAuthority or creates and saves it if not found.
        /// Validates the territorial authority data before saving.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="territoryCode">The ISO territory code (e.g., "US-MD" or "USA").</param>
        /// <param name="territoryCodeSystem">The code system for the territory code.</param>
        /// <param name="governingAgencyDuns">The DUNS number for federal agencies (if applicable).</param>
        /// <param name="governingAgencyIdRoot">The ID root for federal agencies (if applicable).</param>
        /// <param name="governingAgencyName">The name of the federal agency (if applicable).</param>
        /// <param name="logger">Logger for validation warning and error messages.</param>
        /// <returns>The existing or newly created TerritorialAuthority entity, or null if validation failed.</returns>
        /// <remarks>
        /// This method implements the DRY principle by checking for existing territorial
        /// authorities before creating new ones. It handles both state and federal
        /// licensing authorities according to SPL Implementation Guide requirements and
        /// validates all data against SPL compliance rules before saving.
        /// </remarks>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<TerritorialAuthority?> getOrCreateTerritorialAuthorityAsync(
            ApplicationDbContext dbContext,
            string? territoryCode,
            string? territoryCodeSystem,
            string? governingAgencyDuns,
            string? governingAgencyIdRoot,
            string? governingAgencyName,
            ILogger logger)
        {
            #region implementation
            try
            {
                // Try to find existing territorial authority
                var existing = await dbContext.Set<TerritorialAuthority>().FirstOrDefaultAsync(ta =>
                    ta.TerritoryCode == territoryCode &&
                    ta.TerritoryCodeSystem == territoryCodeSystem &&
                    ta.GoverningAgencyIdExtension == governingAgencyDuns &&
                    ta.GoverningAgencyIdRoot == governingAgencyIdRoot &&
                    ta.GoverningAgencyName == governingAgencyName);

                if (existing != null)
                {
                    // Validate existing entity before returning
                    if (validateTerritorialAuthority(existing, logger))
                    {
                        return existing;
                    }
                    else
                    {
                        logger.LogWarning($"Existing territorial authority failed validation: {existing.TerritorialAuthorityID}");
                        return null;
                    }
                }

                // Create new territorial authority
                var newTerritorialAuthority = new TerritorialAuthority
                {
                    TerritoryCode = territoryCode,
                    TerritoryCodeSystem = territoryCodeSystem,
                    GoverningAgencyIdExtension = governingAgencyDuns,
                    GoverningAgencyIdRoot = governingAgencyIdRoot,
                    GoverningAgencyName = governingAgencyName
                };

                // Validate before saving
                if (!validateTerritorialAuthority(newTerritorialAuthority, logger))
                {
                    logger.LogWarning("New territorial authority failed validation and will not be saved.");
                    return null;
                }

                dbContext.Set<TerritorialAuthority>().Add(newTerritorialAuthority);
                await dbContext.SaveChangesAsync();

                logger.LogInformation($"Created new territorial authority: {newTerritorialAuthority.TerritorialAuthorityID}");
                return newTerritorialAuthority;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating or retrieving territorial authority.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing License or creates and saves it if not found.
        /// Validates the license data before saving.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="businessOperationId">The business operation ID to associate with the license.</param>
        /// <param name="territorialAuthorityId">The territorial authority ID for the license issuer.</param>
        /// <param name="licenseNumber">The license number.</param>
        /// <param name="licenseRootOid">The root OID for the license.</param>
        /// <param name="licenseTypeCode">The license type code.</param>
        /// <param name="licenseTypeCodeSystem">The code system for the license type.</param>
        /// <param name="licenseTypeDisplayName">The display name for the license type.</param>
        /// <param name="statusCode">The license status code.</param>
        /// <param name="expirationDate">The license expiration date.</param>
        /// <param name="logger">Logger for validation warning and error messages.</param>
        /// <returns>The existing or newly created License entity, or null if validation failed.</returns>
        /// <remarks>
        /// This method implements the DRY principle by checking for existing licenses
        /// before creating new ones. It follows SPL Implementation Guide Section 18.1.5
        /// for license uniqueness validation and validates all data against SPL compliance
        /// rules before saving.
        /// </remarks>
        /// <seealso cref="License"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<License?> getOrCreateLicenseAsync(
            ApplicationDbContext dbContext,
            int? businessOperationId,
            int? territorialAuthorityId,
            string? licenseNumber,
            string? licenseRootOid,
            string? licenseTypeCode,
            string? licenseTypeCodeSystem,
            string? licenseTypeDisplayName,
            string? statusCode,
            DateTime? expirationDate,
            ILogger logger)
        {
            #region implementation
            try
            {
                // Try to find existing license
                var existing = await dbContext.Set<License>().FirstOrDefaultAsync(l =>
                    l.BusinessOperationID == businessOperationId &&
                    l.TerritorialAuthorityID == territorialAuthorityId &&
                    l.LicenseNumber == licenseNumber &&
                    l.LicenseRootOID == licenseRootOid);

                if (existing != null)
                {
                    // Update existing license with current information
                    existing.LicenseTypeCode = licenseTypeCode;
                    existing.LicenseTypeCodeSystem = licenseTypeCodeSystem;
                    existing.LicenseTypeDisplayName = licenseTypeDisplayName;
                    existing.StatusCode = statusCode;
                    existing.ExpirationDate = expirationDate;

                    // Validate updated license before saving
                    if (validateLicense(existing, logger))
                    {
                        await dbContext.SaveChangesAsync();
                        logger.LogInformation($"Updated existing license: {existing.LicenseID}");
                        return existing;
                    }
                    else
                    {
                        logger.LogWarning($"Updated license failed validation: {existing.LicenseID}");
                        return null;
                    }
                }

                // Create new license
                var newLicense = new License
                {
                    BusinessOperationID = businessOperationId,
                    TerritorialAuthorityID = territorialAuthorityId,
                    LicenseNumber = licenseNumber,
                    LicenseRootOID = licenseRootOid,
                    LicenseTypeCode = licenseTypeCode,
                    LicenseTypeCodeSystem = licenseTypeCodeSystem,
                    LicenseTypeDisplayName = licenseTypeDisplayName,
                    StatusCode = statusCode,
                    ExpirationDate = expirationDate
                };

                // Validate before saving
                if (!validateLicense(newLicense, logger))
                {
                    logger.LogWarning("New license failed validation and will not be saved.");
                    return null;
                }

                dbContext.Set<License>().Add(newLicense);
                await dbContext.SaveChangesAsync();

                logger.LogInformation($"Created new license: {newLicense.LicenseID}");
                return newLicense;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating or retrieving license.");
                return null;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates a License entity against SPL Implementation Guide requirements.
        /// </summary>
        /// <param name="license">The license to validate.</param>
        /// <param name="logger">Logger for validation messages.</param>
        /// <returns>True if the license is valid, false otherwise.</returns>
        /// <remarks>
        /// This method uses the validation attributes defined in the validation namespace
        /// to ensure compliance with SPL Implementation Guide Section 18.1.5.
        /// </remarks>
        /// <seealso cref="License"/>
        /// <seealso cref="Label"/>
        private static bool validateLicense(License license, ILogger logger)
        {
            #region implementation
            if (license == null)
            {
                logger.LogWarning("License is null and cannot be validated.");
                return false;
            }

            var validationContext = new ValidationContext(license);
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            // Perform comprehensive validation using validation attributes
            bool isValid = Validator.TryValidateObject(license, validationContext, validationResults, validateAllProperties: true);

            if (!isValid)
            {
                foreach (var validationResult in validationResults)
                {
                    // Handle the case where ErrorMessage might be null and safely access member names
                    var errorMessage = validationResult.ErrorMessage ?? "Unknown validation error";
                    logger.LogWarning($"License validation error: {errorMessage}");
                }
                return false;
            }

            logger.LogDebug("License validation successful.");
            return true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates a TerritorialAuthority entity against SPL Implementation Guide requirements.
        /// </summary>
        /// <param name="territorialAuthority">The territorial authority to validate.</param>
        /// <param name="logger">Logger for validation messages.</param>
        /// <returns>True if the territorial authority is valid, false otherwise.</returns>
        /// <remarks>
        /// This method uses the validation attributes defined in the validation namespace
        /// to ensure compliance with SPL Implementation Guide Sections 18.1.5.5 and 
        /// 18.1.5.23-18.1.5.26.
        /// </remarks>
        /// <seealso cref="TerritorialAuthority"/>
        /// <seealso cref="Label"/>
        private static bool validateTerritorialAuthority(TerritorialAuthority territorialAuthority, ILogger logger)
        {
            #region implementation
            if (territorialAuthority == null)
            {
                logger.LogWarning("TerritorialAuthority is null and cannot be validated.");
                return false;
            }

            var validationContext = new ValidationContext(territorialAuthority);
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            // Perform comprehensive validation using validation attributes
            bool isValid = Validator.TryValidateObject(territorialAuthority, validationContext, validationResults, validateAllProperties: true);

            if (!isValid)
            {
                foreach (var validationResult in validationResults)
                {
                    // Handle the case where ErrorMessage might be null
                    var errorMessage = validationResult.ErrorMessage ?? "Unknown validation error";
                    logger.LogWarning($"TerritorialAuthority validation error: {errorMessage}");
                }
                return false;
            }

            logger.LogDebug("TerritorialAuthority validation successful.");
            return true;
            #endregion
        }
    }
}