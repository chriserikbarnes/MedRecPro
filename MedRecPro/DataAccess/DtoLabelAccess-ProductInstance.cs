
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
        #region Product Instance & Lot Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductInstance DTOs for the specified product with nested lot hierarchies.
        /// Constructs product instance data including parent and child hierarchy relationships for lot distribution.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productId">The product ID to find product instances for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductInstance DTOs with nested hierarchy data.</returns>
        /// <seealso cref="Label.ProductInstance"/>
        /// <seealso cref="Label.LotHierarchy"/>
        private static async Task<List<ProductInstanceDto>> buildProductInstancesAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productId == null) return new List<ProductInstanceDto>();

            // Get all product instances for this product
            var instances = await db.Set<Label.ProductInstance>()
                .AsNoTracking()
                .Where(e => e.ProductID == productId)
                .ToListAsync();

            var dtos = new List<ProductInstanceDto>();

            // For each instance, build its lot hierarchies
            foreach (var instance in instances)
            {
                var lot = await buildLotIdentifierDtoAsync(db, instance.LotIdentifierID, pkSecret, logger);

                // Build parent and child hierarchy relationships
                var parentHierarchies = await buildLotHierarchiesAsParentAsync(db, instance.ProductInstanceID, pkSecret, logger);

                var childHierarchies = await buildLotHierarchiesAsChildAsync(db, instance.ProductInstanceID, pkSecret, logger);

                var packagingLevels = await buildPackagingLevelsDtoAsync(db, instance.ProductInstanceID, pkSecret, logger);

                // Assemble product instance DTO with hierarchy data
                dtos.Add(new ProductInstanceDto
                {
                    ProductInstance = instance.ToEntityWithEncryptedId(pkSecret, logger),
                    LotIdentifier = lot,
                    ParentHierarchies = parentHierarchies,
                    ChildHierarchies = childHierarchies,
                    PackagingLevels = packagingLevels
                });
            }
            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a lot identifier DTO for a specified lot identifier ID.
        /// The lot identifier can be related to either the Product or the Ingredient.
        /// </summary>
        /// <param name="db">The database context for querying lot identifier entities.</param>
        /// <param name="lotIdentifierId">The lot identifier ID to retrieve.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A LotIdentifierDto object with encrypted ID, or null if not found.</returns>
        /// <seealso cref="Label.LotIdentifier"/>
        /// <seealso cref="LotIdentifierDto"/>
        /// <remarks>
        /// LotIdentifierDto can be related to either the Product or the Ingredient.
        /// </remarks>
        private static async Task<LotIdentifierDto?> buildLotIdentifierDtoAsync(ApplicationDbContext db, int? lotIdentifierId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no lot identifier ID is provided
            if (lotIdentifierId == null) return null;

            // Query lot identifier for the specified lot identifier ID
            var entity = await db.Set<Label.LotIdentifier>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.LotIdentifierID == lotIdentifierId);

            // Return null if entity not found
            if (entity == null)
                return null;

            // Transform entity to DTO with encrypted ID
            return new LotIdentifierDto
            {
                LotIdentifier = entity.ToEntityWithEncryptedId(pkSecret, logger)
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of LotHierarchy DTOs where the specified instance acts as a parent.
        /// Retrieves relationships between Fill/Package Lots and their member Label Lots.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="parentInstanceId">The parent instance ID to find child hierarchies for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of LotHierarchy DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.LotHierarchy"/>
        private static async Task<List<LotHierarchyDto>> buildLotHierarchiesAsParentAsync(ApplicationDbContext db, int? parentInstanceId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (parentInstanceId == null) return new List<LotHierarchyDto>();

            // Query lot hierarchies where this instance is the parent
            var items = await db.Set<Label.LotHierarchy>()
                .AsNoTracking()
                .Where(e => e.ParentInstanceID == parentInstanceId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new LotHierarchyDto { LotHierarchy = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of LotHierarchy DTOs where the specified instance acts as a child.
        /// Retrieves relationships where this instance is a member of other Fill/Package Lots.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="childInstanceId">The child instance ID to find parent hierarchies for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of LotHierarchy DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.LotHierarchy"/>
        private static async Task<List<LotHierarchyDto>> buildLotHierarchiesAsChildAsync(ApplicationDbContext db, int? childInstanceId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (childInstanceId == null) return new List<LotHierarchyDto>();

            // Query lot hierarchies where this instance is the child
            var items = await db.Set<Label.LotHierarchy>()
                .AsNoTracking()
                .Where(e => e.ChildInstanceID == childInstanceId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new LotHierarchyDto { LotHierarchy = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of lot hierarchy DTOs for fill/package lots based on product instance ID.
        /// Establishes the relationship between Fill/Package Lots and Label Lots where the parent is the package/fill lot.
        /// </summary>
        /// <param name="db">The database context for querying lot hierarchy entities.</param>
        /// <param name="productID">The product instance identifier used as parent instance ID.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of LotHierarchyDto objects representing fill lot hierarchies.</returns>
        /// <seealso cref="Label.LotHierarchy"/>
        /// <seealso cref="LotHierarchyDto"/>
        /// <remarks>
        /// Based on Section 16.2.7, 16.2.11. The parent is the package/fill lot and the child is the label lot.
        /// LotHierarchy has a one-to-many relationship with ProductInstance.
        /// </remarks>
        private static async Task<List<LotHierarchyDto>> buildFillLotHierarchyDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no product ID is provided
            if (productID == null) return new List<LotHierarchyDto>();

            // Query lot hierarchies for the specified product instance as parent
            var items = await db.Set<Label.LotHierarchy>()
                .AsNoTracking()
                .Where(e => e.ParentInstanceID == productID)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<LotHierarchyDto>();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new LotHierarchyDto { LotHierarchy = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<LotHierarchyDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of lot hierarchy DTOs for label lots based on product instance ID.
        /// Establishes the relationship between Fill/Package Lots and Label Lots where the child is the label lot.
        /// </summary>
        /// <param name="db">The database context for querying lot hierarchy entities.</param>
        /// <param name="productID">The product instance identifier used as child instance ID.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of LotHierarchyDto objects representing label lot hierarchies.</returns>
        /// <seealso cref="Label.LotHierarchy"/>
        /// <seealso cref="LotHierarchyDto"/>
        /// <remarks>
        /// Based on Section 16.2.7, 16.2.11. The parent is the package/fill lot and the child is the label lot.
        /// LotHierarchy has a one-to-many relationship with ProductInstance.
        /// </remarks>
        private static async Task<List<LotHierarchyDto>> buildLabelLotHierarchyDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no product ID is provided
            if (productID == null) return new List<LotHierarchyDto>();

            // Query lot hierarchies for the specified product instance as child
            var items = await db.Set<Label.LotHierarchy>()
                .AsNoTracking()
                .Where(e => e.ChildInstanceID == productID)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<LotHierarchyDto>();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new LotHierarchyDto { LotHierarchy = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<LotHierarchyDto>();
            #endregion
        }
        #endregion
    }
}