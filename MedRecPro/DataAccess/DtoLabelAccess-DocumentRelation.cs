
using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using static MedRecPro.Models.Label;
using Cached = MedRecPro.Helpers.PerformanceHelper;

namespace MedRecPro.DataAccess
{
    /// <summary>
    /// Provides helper methods for building Data Transfer Objects (DTOs) from SPL Label entities.
    /// Constructs complete hierarchical data structures representing medical product documents
    /// and their associated metadata, relationships, and compliance information.
    /// </summary>
    /// <seealso cref="Label"/>
    /// <seealso cref="DocumentDto"/>
    public static partial class DtoLabelAccess
    {
        #region Document Relationship Child Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of BusinessOperation DTOs for the specified document 
        /// relationship. Retrieves business operation details for establishments
        /// or labelers linked through document relationships and enriches them
        /// with licenses and qualifiers, then transforms them into DTOs with 
        /// encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="docRelId">The unique identifier of the document relationship to find business operations for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of BusinessOperationDto objects representing the business operations with their associated data, or an empty list if none found</returns>
        /// <remarks>
        /// Each business operation is enriched with its licenses and business operation qualifiers.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var businessOperations = await buildBusinessOperationsAsync(dbContext, 159, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.BusinessOperation"/>
        /// <seealso cref="Label.License"/>
        /// <seealso cref="Label.BusinessOperationQualifier"/>
        /// <seealso cref="LicenseDto"/>
        /// <seealso cref="BusinessOperationQualifier"/>
        /// <seealso cref="BusinessOperationDto"/>
        /// <seealso cref="buildLicensesAsync"/>
        /// <seealso cref="buildBusinessOperationQualifiersAsync"/>
        private static async Task<List<BusinessOperationDto>> buildBusinessOperationsAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no document relationship ID provided
            if (docRelId == null)
                return new List<BusinessOperationDto>();

            var dtos = new List<BusinessOperationDto>();

            // Query business operations for the specified document relationship using read-only tracking
            var entities = await db.Set<Label.BusinessOperation>()
                .AsNoTracking()
                .Where(e => e.DocumentRelationshipID == docRelId)
                .ToListAsync();

            // Return empty list if no business operations found
            if (entities == null || !entities.Any())
                return new List<BusinessOperationDto>();

            // Process each business operation and build associated data
            foreach (var e in entities)
            {
                // Skip entities without valid IDs
                if (e.BusinessOperationID == null)
                    continue;

                // Build licenses and business operation qualifiers for each business operation
                var licenses = await buildLicensesAsync(db, e.BusinessOperationID, pkSecret, logger);
                var qualifiers = await buildBusinessOperationQualifiersAsync(db, e.BusinessOperationID, pkSecret, logger);

                // Create business operation DTO with encrypted ID and associated data
                dtos.Add(new BusinessOperationDto
                {
                    BusinessOperation = e.ToEntityWithEncryptedId(pkSecret, logger),
                    Licenses = licenses,
                    BusinessOperationQualifiers = qualifiers
                });
            }

            // Return processed business operations with associated data, ensuring non-null result
            return dtos ?? new List<BusinessOperationDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds licenses to the business operation. Retrieves license 
        /// records for a specified business operation ID and enriches 
        /// them with disciplinary actions, then transforms them into DTOs 
        /// with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="businessOperationId">The unique identifier of the business operation to find licenses for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of LicenseDto objects representing the licenses with their associated data, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: BusinessOperationDto > LicenseDto
        /// Each license is enriched with its disciplinary actions.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var licenses = await buildLicensesAsync(dbContext, 357, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.License"/>
        /// <seealso cref="Label.DisciplinaryAction"/>
        /// <seealso cref="LicenseDto"/>
        /// <seealso cref="DisciplinaryActionDto"/>"/>
        /// <seealso cref="buildDisciplinaryActionsAsync"/>
        private static async Task<List<LicenseDto>> buildLicensesAsync(ApplicationDbContext db, int? businessOperationId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no business operation ID provided
            if (businessOperationId == null)
                return new List<LicenseDto>();

            var dtos = new List<LicenseDto>();

            // Query licenses for the specified business operation using read-only tracking
            var entities = await db.Set<Label.License>()
                .AsNoTracking()
                .Where(e => e.BusinessOperationID == businessOperationId)
                .ToListAsync();

            // Return empty list if no licenses found
            if (entities == null || !entities.Any())
                return new List<LicenseDto>();

            // Process each license entity and build its DTO with associated data
            foreach (var e in entities)
            {
                // Skip entities without valid IDs
                if (e.LicenseID == null)
                    continue;

                // Build disciplinary actions for this license
                var disciplinaryActions = await buildDisciplinaryActionsAsync(db, e.LicenseID, pkSecret, logger);

                var govAuthorities = await buildTerritorialAuthoritiesDtoAsync(db, e.TerritorialAuthorityID, pkSecret, logger)
                    ?? new List<TerritorialAuthorityDto>();

                // Create license DTO with encrypted ID and associated data
                dtos.Add(new LicenseDto
                {
                    License = e.ToEntityWithEncryptedId(pkSecret, logger),
                    DisciplinaryActions = disciplinaryActions,
                    TerritorialAuthorities = govAuthorities
                });
            }

            // Return processed licenses with associated data, ensuring non-null result
            return dtos ?? new List<LicenseDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds DisciplinaryAction DTOs for the specified license. Retrieves 
        /// disciplinary action records for a specified license ID and 
        /// transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="licenseId">The unique identifier of the license to find disciplinary actions for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of DisciplinaryActionDto objects representing the disciplinary actions, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: BusinessOperationDto > LicenseDto > DisciplinaryActionDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var disciplinaryActions = await buildDisciplinaryActionsAsync(dbContext, 468, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.DisciplinaryAction"/>
        /// <seealso cref="DisciplinaryActionDto"/>
        private static async Task<List<DisciplinaryActionDto>> buildDisciplinaryActionsAsync(ApplicationDbContext db, int? licenseId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no license ID provided
            if (licenseId == null)
                return new List<DisciplinaryActionDto>();

            // Query disciplinary actions for the specified license using read-only tracking
            var entities = await db.Set<Label.DisciplinaryAction>()
                .AsNoTracking()
                .Where(e => e.LicenseID == licenseId)
                .ToListAsync();

            // Return empty list if no disciplinary actions found
            if (entities == null || !entities.Any())
                return new List<DisciplinaryActionDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entities
                .Select(item => new DisciplinaryActionDto { DisciplinaryAction = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<DisciplinaryActionDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds BusinessOperationQualifier DTOs for the specified business 
        /// operation. Retrieves business operation qualifier records 
        /// for a specified business operation ID and transforms them into
        /// DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="businessOperationId">The unique identifier of the business operation to find qualifiers for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of BusinessOperationQualifierDto objects representing the business operation qualifiers, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: BusinessOperationDto > BusinessOperationQualifierDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var qualifiers = await buildBusinessOperationQualifiersAsync(dbContext, 357, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.BusinessOperationQualifier"/>
        /// <seealso cref="BusinessOperationQualifierDto"/>
        private static async Task<List<BusinessOperationQualifierDto>> buildBusinessOperationQualifiersAsync(ApplicationDbContext db, int? businessOperationId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no business operation ID provided
            if (businessOperationId == null)
                return new List<BusinessOperationQualifierDto>();

            // Query business operation qualifiers for the specified business operation using read-only tracking
            var entities = await db.Set<Label.BusinessOperationQualifier>()
                .AsNoTracking()
                .Where(e => e.BusinessOperationID == businessOperationId)
                .ToListAsync();

            // Return empty list if no business operation qualifiers found
            if (entities == null || !entities.Any())
                return new List<BusinessOperationQualifierDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entities
                .Select(item => new BusinessOperationQualifierDto { BusinessOperationQualifier = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<BusinessOperationQualifierDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of CertificationProductLink DTOs for the specified 
        /// document relationship. Retrieves links between establishments 
        /// and products being certified in Blanket No Changes Certification documents.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="docRelId">The document relationship ID to find certification product links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of CertificationProductLink DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.CertificationProductLink"/>
        private static async Task<List<CertificationProductLinkDto>> buildCertificationProductLinksAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (docRelId == null) return new List<CertificationProductLinkDto>();

            // Query certification product links for the specified document relationship
            var items = await db.Set<Label.CertificationProductLink>()
                .AsNoTracking()
                .Where(e => e.DocumentRelationshipID == docRelId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<CertificationProductLinkDto>();

            // Transform entities to DTOs with encrypted IDs
            return items.Select(item => new CertificationProductLinkDto { CertificationProductLink = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList() ?? new List<CertificationProductLinkDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ComplianceAction DTOs for the specified document relationship.
        /// Retrieves FDA-initiated inactivation/reactivation status for establishment registrations.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="docRelId">The document relationship ID to find compliance actions for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ComplianceAction DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ComplianceAction"/>
        private static async Task<List<ComplianceActionDto>> buildComplianceActionsForRelationshipAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (docRelId == null) return new List<ComplianceActionDto>();

            // Query compliance actions for the specified document relationship
            var items = await db.Set<Label.ComplianceAction>()
                .AsNoTracking()
                .Where(e => e.DocumentRelationshipID == docRelId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<ComplianceActionDto>();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ComplianceActionDto { ComplianceAction = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of FacilityProductLink DTOs for the specified document relationship.
        /// Retrieves links between facilities and cosmetic products in registration or listing documents.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="docRelId">The document relationship ID to find facility product links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of FacilityProductLink DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.FacilityProductLink"/>
        private static async Task<List<FacilityProductLinkDto>> buildFacilityProductLinksAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (docRelId == null) return new List<FacilityProductLinkDto>();

            List<FacilityProductLinkDto> retSet = new List<FacilityProductLinkDto>();

            // Query facility product links for the specified document relationship
            var items = await db.Set<Label.FacilityProductLink>()
                .AsNoTracking()
                .Where(e => e.DocumentRelationshipID == docRelId)
                .ToListAsync();

            if (!items.Any())
                return new List<FacilityProductLinkDto>();

            // Use HashSet for O(1) lookup performance instead of O(n) with List.Contains
            var productNamesSet = items.Select(x => x.ProductName).ToHashSet();

            // Get distinct ProductIDs to avoid duplicate calls
            var distinctProductIds = items
                .Where(i => i != null && i.ProductID != null)
                .Select(i => i.ProductID)
                .Distinct()
                .ToList();

            // prod identifiers batch fetch
            var allProductIdentifiers = await buildProductIdentifiersBatchAsync(db, distinctProductIds, pkSecret, logger);

            // Filter product identifiers efficiently using HashSet
            var filteredProductIdentifiers = allProductIdentifiers
                .Where(pi => pi?.IdentifierValue != null
                    && productNamesSet.Contains(pi.IdentifierValue))
                .ToList();

            // Map each FacilityProductLink to its corresponding ProductIdentifier
            foreach (var item in items)
            {
                // Get the matching ProductIdentifier for this FacilityProductLink
                var prodIdentifier = filteredProductIdentifiers.FirstOrDefault(pi => pi?.IdentifierValue == item.ProductName);

                // Create and add the DTO to the result set
                retSet.Add(new FacilityProductLinkDto
                {
                    FacilityProductLink = item.ToEntityWithEncryptedId(pkSecret, logger),
                    ProductIdentifier = prodIdentifier
                });

            }

            // return processed facility product links with associated product identifiers
            return retSet;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Helper method to batch fetch ProductIdentifier entities for multiple ProductIDs.
        /// </summary>
        /// <param name="db"></param>
        /// <param name="productIds"></param>
        /// <param name="pkSecret"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static async Task<List<ProductIdentifierDto>> buildProductIdentifiersBatchAsync(
            DbContext db,
            List<int?> productIds,
            string pkSecret,
            ILogger logger)
        {

            #region implementation
            // Single query to get all product identifiers for multiple ProductIDs
            return await db.Set<ProductIdentifier>()
                 .AsNoTracking()
                 .Where(pi => pi != null && pi.ProductID != null && productIds.Contains(pi.ProductID))
                 .Select(pi => new ProductIdentifierDto { ProductIdentifier = pi.ToEntityWithEncryptedId(pkSecret, logger) })
                 .ToListAsync();
            #endregion
        }
        #endregion
    }
}