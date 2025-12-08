
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
        #region Packaging Hierarchy Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of packaging level DTOs for a specified product, including packaging 
        /// hierarchy data and package-level characteristics.
        /// </summary>
        /// <param name="db">The database context for querying packaging level entities.</param>
        /// <param name="productID">The product identifier to filter packaging levels.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of PackagingLevelDto objects with encrypted IDs, packaging hierarchy data, and characteristics.</returns>
        /// <seealso cref="Label.PackagingLevel"/>
        /// <seealso cref="Label.ProductEvent"/>
        /// <seealso cref="Label.PackagingHierarchy"/>
        /// <seealso cref="Label.MarketingStatus"/>
        /// <seealso cref="Label.Characteristic"/>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="ProductEventDto"/>
        /// <seealso cref="PackagingHierarchyDto"/>
        /// <seealso cref="MarketingStatusDto"/>
        /// <seealso cref="CharacteristicDto"/>
        /// <seealso cref="buildPackagingHierarchyDtoAsync"/>
        /// <seealso cref="buildProductEventsAsync"/>
        /// <seealso cref="buildPackageMarketingStatusesAsync"/>
        /// <seealso cref="buildCharacteristicsDtoAsync"/>
        /// <remarks>
        /// Enhanced to include package-level characteristics as specified in _Packaging.cshtml template.
        /// Package-level characteristics may include container type, labeling information, or other
        /// package-specific properties that differ from product-level characteristics.
        /// </remarks>
        /// <example>
        /// <code>
        /// var packagingLevels = await buildPackagingLevelsAsync(dbContext, 123, "secretKey", logger);
        /// </code>
        /// </example>
        private static async Task<List<PackagingLevelDto>> buildPackagingLevelsAsync(
            ApplicationDbContext db,
            int? productID,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            #region validation
            // Return empty list if no product ID is provided
            if (productID == null) return new List<PackagingLevelDto>();
            #endregion

            #region query execution
            var dtos = new List<PackagingLevelDto>();
            List<PackageIdentifierDto> packageIdentifierDtos = new List<PackageIdentifierDto>();

            // Query packaging levels for the specified product
            var items = await db.Set<Label.PackagingLevel>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<PackagingLevelDto>();
            #endregion

            #region dto building

            // Build packaging level DTOs with all associated data
            foreach (var item in items)
            {
                #region nested collections

                // Fetch package identifiers for this packaging level
                var packageIdentifier = await db.Set<Label.PackageIdentifier>()
                    .AsNoTracking()
                    .Where(e => e.PackagingLevelID == item.PackagingLevelID)
                    .ToListAsync();

                packageIdentifierDtos = new List<PackageIdentifierDto>();

                // Build packaging hierarchy, events, and marketing statuses
                var pack = await buildPackagingHierarchyDtoAsync(db, item.PackagingLevelID, pkSecret, logger);
                var events = await buildProductEventsAsync(db, item.PackagingLevelID, pkSecret, logger);
                var marketingStatuses = await buildPackageMarketingStatusesAsync(db, item.PackagingLevelID, pkSecret, logger);

                // Build package-level characteristics for this packaging level
                var characteristics = await buildCharacteristicsDtoAsync(db, null, pkSecret, logger, null, item.PackagingLevelID);

                #endregion

                #region package identifiers
                // Process package identifiers if present
                if (packageIdentifier != null && packageIdentifier.Any())
                {
                    foreach (var pk in packageIdentifier)
                    {
                        var itemPk = await buildPackageIdentifierDtoAsync(db, pk.PackageIdentifierID, pkSecret, logger);

                        if (itemPk == null)
                            continue;

                        packageIdentifierDtos.Add(itemPk);
                    }
                }

                #endregion

                #region dto assembly

                // Assemble complete packaging level DTO with all collections
                dtos.Add(new PackagingLevelDto
                {
                    PackagingLevel = item.ToEntityWithEncryptedId(pkSecret, logger),
                    PackagingHierarchy = pack,
                    ProductEvents = events,
                    MarketingStatuses = marketingStatuses,
                    PackageIdentifiers = packageIdentifierDtos,
                    Characteristics = characteristics
                });

                #endregion
            }
            #endregion

            // Return completed DTOs with encrypted IDs
            return dtos ?? new List<PackagingLevelDto>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of packaging level DTOs for a specified product instance ID.
        /// Helper method for retrieving packaging levels associated with product instances.
        /// </summary>
        /// <param name="db">The database context for querying packaging level entities.</param>
        /// <param name="productInstanceID">The product instance identifier to filter packaging levels.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of PackagingLevelDto objects, or null if no product instance ID provided or no entities found.</returns>
        /// <seealso cref="Label.PackagingLevel"/>
        /// <seealso cref="PackagingLevelDto"/>
        private static async Task<List<PackagingLevelDto>> buildPackagingLevelsDtoAsync(ApplicationDbContext db, int? productInstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product instance ID is provided
            if (productInstanceID == null)
                return new List<PackagingLevelDto>();

            // Query packaging levels for the specified product instance
            var entity = await db.Set<Label.PackagingLevel>()
                .AsNoTracking()
                .Where(e => e.ProductInstanceID == productInstanceID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<PackagingLevelDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new PackagingLevelDto
            {
                PackagingLevel = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<PackagingLevelDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves marketing status records for a specified packaging 
        /// level ID and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="packagingLevelID">The unique identifier of the packaging level to find marketing statuses for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of MarketingStatusDto objects representing the marketing statuses, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: PackagingLevelDto > MarketingStatusDto
        /// Marketing statuses track the regulatory and commercial status of packaging levels in various markets.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var marketingStatuses = await buildPackageMarketingStatusesAsync(dbContext, 396, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.MarketingStatus"/>
        /// <seealso cref="MarketingStatusDto"/>
        private static async Task<List<MarketingStatusDto>> buildPackageMarketingStatusesAsync(ApplicationDbContext db, int? packagingLevelID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no packaging level ID provided
            if (packagingLevelID == null)
                return new List<MarketingStatusDto>();

            // Query marketing statuses for the specified packaging level using read-only tracking
            var items = await db.Set<Label.MarketingStatus>()
                .AsNoTracking()
                .Where(e => e.PackagingLevelID == packagingLevelID)
                .ToListAsync();

            // Return empty list if no marketing statuses found
            if (items == null || !items.Any())
                return new List<MarketingStatusDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new MarketingStatusDto { MarketingStatus = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<MarketingStatusDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of packaging hierarchy DTOs for a specified outer packaging level ID.
        /// Contains both OuterPackagingLevelID and InnerPackagingLevelID relationships.
        /// </summary>
        /// <param name="db">The database context for querying packaging hierarchy entities.</param>
        /// <param name="outerPackagingLevelID">The outer packaging level identifier to filter packaging hierarchies.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of PackagingHierarchyDto objects, or null if no outer packaging level ID provided or no entities found.</returns>
        /// <seealso cref="Label.PackagingHierarchy"/>
        /// <seealso cref="PackagingHierarchyDto"/>
        private static async Task<List<PackagingHierarchyDto>> buildPackagingHierarchyDtoAsync(ApplicationDbContext db, int? outerPackagingLevelID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no outer packaging level ID is provided
            if (outerPackagingLevelID == null)
                return new List<PackagingHierarchyDto>();

            // Query packaging hierarchies for the specified outer packaging level
            var entity = await db.Set<Label.PackagingHierarchy>()
                .AsNoTracking()
                .Where(e => e.OuterPackagingLevelID == outerPackagingLevelID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return new List<PackagingHierarchyDto>();

            var dtos = new List<PackagingHierarchyDto>();

            // Build each hierarchy DTO with its child packaging level
            foreach (var hierarchy in entity)
            {
                var dto = new PackagingHierarchyDto
                {
                    PackagingHierarchy = hierarchy.ToEntityWithEncryptedId(pkSecret, logger)
                };

                // Build the child packaging level if InnerPackagingLevelID exists       
                if (hierarchy.InnerPackagingLevelID != null)
                {
                    var innerPackagingLevels = await buildInnerPackagingLevelsDtoAsync(db, hierarchy.InnerPackagingLevelID, pkSecret, logger);
                    dto.ChildPackagingLevel = innerPackagingLevels?.FirstOrDefault();
                }

                dtos.Add(dto);
            }

            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of packaging level DTOs for a specified packaging level id.
        /// Helper method for retrieving packaging levels associated with package hierarchy.
        /// </summary>
        /// <param name="db">The database context for querying packaging level entities.</param>
        /// <param name="packingLevelId">The packing hierarchy identifier to filter packaging levels.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of PackagingLevelDto objects, or null if no product instance ID provided or no entities found.</returns>
        /// <seealso cref="Label.PackagingLevel"/>
        /// <seealso cref="PackagingLevelDto"/>
        private static async Task<List<PackagingLevelDto>> buildInnerPackagingLevelsDtoAsync(ApplicationDbContext db, int? packingLevelId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product instance ID is provided
            if (packingLevelId == null)
                return new List<PackagingLevelDto>();

            // Query packaging levels for the specified product instance
            var entity = await db.Set<Label.PackagingLevel>()
                .AsNoTracking()
                .Where(e => e.PackagingLevelID == packingLevelId)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<PackagingLevelDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new PackagingLevelDto
            {
                PackagingLevel = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<PackagingLevelDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores product events like distribution or return quantities. Retrieves 
        /// product event records for a specified packaging level ID and transforms 
        /// them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="packagingLevelID">The unique identifier of the packaging level to find product events for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of ProductEventDto objects representing the product events, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: PackagingLevelDto > ProductEventDto
        /// Product events include activities like distribution or return quantities associated with packaging levels.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var productEvents = await buildProductEventsAsync(dbContext, 753, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ProductEvent"/>
        /// <seealso cref="ProductEventDto"/>
        private static async Task<List<ProductEventDto>> buildProductEventsAsync(ApplicationDbContext db, int? packagingLevelID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no packaging level ID provided
            if (packagingLevelID == null)
                return new List<ProductEventDto>();

            // Query product events for the specified packaging level using read-only tracking
            var entity = await db.Set<Label.ProductEvent>()
                .AsNoTracking()
                .Where(e => e.PackagingLevelID == packagingLevelID)
                .ToListAsync();

            // Return empty list if no product events found
            if (entity == null || !entity.Any())
                return new List<ProductEventDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new ProductEventDto { ProductEvent = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<ProductEventDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a package identifier DTO for a specified packaging level ID.
        /// </summary>
        /// <param name="db">The database context for querying package identifier entities.</param>
        /// <param name="packagingLevelID">The packaging level identifier to retrieve package identifier for.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <param name="sectionId">OPTIONAL for indexing files with compliance actions</param>
        /// <returns>A PackageIdentifierDto object with encrypted ID, or null if not found.</returns>
        /// <seealso cref="Label.PackageIdentifier"/>
        /// <seealso cref="PackageIdentifierDto"/>
        private static async Task<PackageIdentifierDto?> buildPackageIdentifierDtoAsync(ApplicationDbContext db,
            int? packagingLevelID,
            string pkSecret,
            ILogger logger,
            int? sectionId = null)
        {
            #region implementation
            // Return null if no packaging level ID is provided
            if (packagingLevelID == null)
                return null;

            // Query package identifier for the specified packaging level
            var entity = await db.Set<Label.PackageIdentifier>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.PackagingLevelID == packagingLevelID);

            // Return null if no entity found
            if (entity == null)
                return null;

            //  Build ComplianceActions for this PackageIdentifier
            var complianceActions = await buildComplianceActionsForPackageAsync(
                db,
                entity.PackageIdentifierID,
                pkSecret,
                logger,
                sectionId);

            // Transform entity to DTO with encrypted ID
            return new PackageIdentifierDto
            {
                PackageIdentifier = entity.ToEntityWithEncryptedId(pkSecret, logger),
                ComplianceActions = complianceActions
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asynchronously builds a list of compliance action DTOs for a specified package identifier.
        /// Retrieves compliance actions from the database and converts them to encrypted DTOs.
        /// </summary>
        /// <param name="db">The application database context for data access</param>
        /// <param name="packageIdentifierId">The unique identifier of the package to retrieve compliance actions for. If null, returns an empty list.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs in the response DTOs</param>
        /// <param name="logger">The logger instance for recording any errors or information during processing</param>
        /// <param name="sectionId">OPTIONAL used for spl indexing files for compliance actions</param>
        /// <returns>A list of ComplianceActionDto objects with encrypted IDs, or an empty list if no package identifier is provided</returns>
        /// <remarks>
        /// This method uses AsNoTracking() for better performance when the entities won't be modified.
        /// The method handles null package identifiers gracefully by returning an empty collection.
        /// </remarks>
        /// <example>
        /// <code>
        /// var complianceActions = await buildComplianceActionsForPackageAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ComplianceAction"/>
        /// <seealso cref="ComplianceActionDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        private static async Task<List<ComplianceActionDto>> buildComplianceActionsForPackageAsync(
            ApplicationDbContext db,
            int? packageIdentifierId,
            string pkSecret,
            ILogger logger,
            int? sectionId = null)
        {
            #region implementation

            #region validation
            // Return empty list immediately if no package identifier provided
            if (packageIdentifierId == null)
                return new List<ComplianceActionDto>();
            #endregion

            #region data retrieval
            // Query compliance actions for the specified package identifier
            // Using AsNoTracking for performance since we're not modifying entities
            var query = db.Set<Label.ComplianceAction>()
                .AsNoTracking()
                .Where(e => e.PackageIdentifierID == packageIdentifierId);

            // Add section filter if provided
            if (sectionId != null)
            {
                query = query.Where(e => e.SectionID == sectionId);
            }

            var items = await query.ToListAsync();
            #endregion

            #region dto conversion
            // Transform database entities to DTOs with encrypted IDs
            return items
                .Select(item => new ComplianceActionDto
                {
                    // Convert entity to DTO with encrypted ID using the provided secret
                    ComplianceAction = item.ToEntityWithEncryptedId(pkSecret, logger)
                })
                .ToList();
            #endregion

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds PackageIdentifier DTOs directly for a product, bypassing Characteristics.
        /// Used for indexing documents and other cases where PackageIdentifiers exist 
        /// without going through the Characteristic → PackagingLevel path.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productID">The product ID to find package identifiers for.</param>
        /// <param name="sectionId">The section ID for context when building compliance actions.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of PackageIdentifier DTOs with ComplianceActions.</returns>
        private static async Task<List<PackageIdentifierDto>> buildDirectPackageIdentifiersAsync(
            ApplicationDbContext db, int? productID, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productID == null) return new List<PackageIdentifierDto>();

            // Find PackageIdentifiers through join with PackagingLevel → Product relationship
            var packageIdentifiers = await (from pi in db.Set<Label.PackageIdentifier>()
                                            join pl in db.Set<Label.PackagingLevel>() on pi.PackagingLevelID equals pl.PackagingLevelID
                                            where pl.ProductID == productID
                                            select pi)
                .AsNoTracking()
                .ToListAsync();

            var dtos = new List<PackageIdentifierDto>();

            foreach (var pkgId in packageIdentifiers)
            {
                // Build ComplianceActions for this PackageIdentifier with section context
                var complianceActions = await buildComplianceActionsForPackageAsync(
                    db, pkgId.PackageIdentifierID, pkSecret, logger, sectionId);

                dtos.Add(new PackageIdentifierDto
                {
                    PackageIdentifier = pkgId.ToEntityWithEncryptedId(pkSecret, logger),
                    ComplianceActions = complianceActions
                });
            }

            return dtos;
            #endregion
        }

        #endregion
    }
}