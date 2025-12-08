
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
        #region Product Hierarchy Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of Product DTOs for the specified section with complete nested hierarchies.
        /// Constructs comprehensive product data including generic medicines, identifiers, routes, web links,
        /// business operation links, responsible person links, product instances, and ingredients.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find products for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of Product DTOs with complete nested collections.</returns>
        /// <seealso cref="Label.Product"/>
        private static async Task<List<ProductDto>> buildProductsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<ProductDto>();

            // Get all products for this section
            var products = await db.Set<Label.Product>()
                .AsNoTracking()
                .Where(p => p.SectionID == sectionId)
                .ToListAsync();

            if (products == null || !products.Any())
                return new List<ProductDto>();

            var productDtos = new List<ProductDto>();

            // For each product, build all its nested collections
            foreach (var product in products)
            {
                // Build all child collections for this product
                var additionalIds = await buildAdditionalIdentifiersAsync(db, product.ProductID, pkSecret, logger);
                var businessOpLinks = await buildBusinessOperationProductLinksAsync(db, product.ProductID, pkSecret, logger);
                var characteristics = await buildCharacteristicsDtoAsync(db, product.ProductID, pkSecret, logger, sectionId);
                var packageIdentifiers = await buildDirectPackageIdentifiersAsync(db, product.ProductID, sectionId, pkSecret, logger);
                var childLots = await buildLabelLotHierarchyDtoAsync(db, product.ProductID, pkSecret, logger);
                var dosingSpecs = await buildDosingSpecificationsAsync(db, product.ProductID, pkSecret, logger);
                var equivalents = await buildEquivalentEntitiesAsync(db, product.ProductID, pkSecret, logger);
                var genericMeds = await buildGenericMedicinesAsync(db, product.ProductID, pkSecret, logger);
                var ingredientInstances = await buildProductIngredientInstancesAsync(db, product.ProductID, pkSecret, logger);
                var ingredients = await buildIngredientsAsync(db, product.ProductID, pkSecret, logger);
                var marketingCats = await buildMarketingCategoriesDtoAsync(db, product.ProductID, pkSecret, logger);
                var marketingStatuses = await buildProductMarketingStatusesAsync(db, product.ProductID, pkSecret, logger);
                var packageLevels = await buildPackagingLevelsAsync(db, product.ProductID, pkSecret, logger);
                var parentLots = await buildFillLotHierarchyDtoAsync(db, product.ProductID, pkSecret, logger);
                var partsOfAssembly = await buildPartOfAssembliesAsync(db, product.ProductID, pkSecret, logger);
                var policies = await buildPoliciesAsync(db, product.ProductID, pkSecret, logger);
                var productIds = await buildProductIdentifiersAsync(db, product.ProductID, pkSecret, logger);
                var productInstances = await buildProductInstancesAsync(db, product.ProductID, pkSecret, logger);
                var productParts = await buildProductPartsDtoAsync(db, product.ProductID, pkSecret, logger);
                var productRoutes = await buildProductRouteOfAdministrationsAsync(db, product.ProductID, pkSecret, logger);
                var respPersonLinks = await buildResponsiblePersonLinksAsync(db, product.ProductID, pkSecret, logger);
                var specializedKinds = await buildSpecializedKindsAsync(db, product.ProductID, pkSecret, logger);
                var webLinks = await buildProductWebLinksAsync(db, product.ProductID, pkSecret, logger);

                // Assemble complete product DTO with all nested data
                productDtos.Add(new ProductDto
                {
                    Product = product.ToEntityWithEncryptedId(pkSecret, logger),
                    ProductParts = productParts,
                    GenericMedicines = genericMeds,
                    ParentLotHierarchies = parentLots,
                    ChildLotHierarchies = childLots,
                    MarketingCategories = marketingCats,
                    MarketingStatuses = marketingStatuses,
                    ProductIdentifiers = productIds,
                    PackageIdentifiers = packageIdentifiers,
                    PackagingLevels = packageLevels,
                    ProductRouteOfAdministrations = productRoutes,
                    ProductWebLinks = webLinks,
                    BusinessOperationProductLinks = businessOpLinks,
                    ResponsiblePersonLinks = respPersonLinks,
                    Characteristics = characteristics,
                    ProductInstances = productInstances,
                    Ingredients = ingredients,
                    IngredientInstances = ingredientInstances,
                    AdditionalIdentifiers = additionalIds,
                    DosingSpecifications = dosingSpecs,
                    EquivalentEntities = equivalents,
                    PartOfAssemblies = partsOfAssembly,
                    Policies = policies,
                    SpecializedKinds = specializedKinds
                });
            }
            return productDtos ?? new List<ProductDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves marketing status records for a specified product ID and 
        /// transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find marketing statuses for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of MarketingStatusDto objects representing the marketing statuses, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > MarketingStatusDto
        /// Marketing statuses track the regulatory and commercial status of products in various markets.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var marketingStatuses = await buildProductMarketingStatusesAsync(dbContext, 842, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.MarketingStatus"/>
        /// <seealso cref="MarketingStatusDto"/>
        private static async Task<List<MarketingStatusDto>> buildProductMarketingStatusesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null)
                return new List<MarketingStatusDto>();

            // Query marketing statuses for the specified product using read-only tracking
            var items = await db.Set<Label.MarketingStatus>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
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
        /// Additional identifiers for the product. Retrieves additional 
        /// identifier records for a specified product ID and transforms 
        /// them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find additional identifiers for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of AdditionalIdentifierDto objects representing the additional identifiers, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > AdditionalIdentifiersDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var additionalIdentifiers = await buildAdditionalIdentifiersAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.AdditionalIdentifier"/>
        /// <seealso cref="AdditionalIdentifierDto"/>
        private static async Task<List<AdditionalIdentifierDto>> buildAdditionalIdentifiersAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null) return new List<AdditionalIdentifierDto>();

            // Query additional identifiers for the specified product using read-only tracking
            var items = await db.Set<Label.AdditionalIdentifier>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return empty list if no additional identifiers found
            if (items == null || !items.Any())
                return new List<AdditionalIdentifierDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new AdditionalIdentifierDto { AdditionalIdentifier = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<AdditionalIdentifierDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Dosing specs for the product. Retrieves dosing specification records 
        /// for a specified product ID and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find dosing specifications for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of DosingSpecificationDto objects representing the dosing specifications, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > DosingSpecificationDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var dosingSpecs = await buildDosingSpecificationsAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.DosingSpecification"/>
        /// <seealso cref="DosingSpecificationDto"/>
        private static async Task<List<DosingSpecificationDto>> buildDosingSpecificationsAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null) return new List<DosingSpecificationDto>();

            // Query dosing specifications for the specified product using read-only tracking
            var items = await db.Set<Label.DosingSpecification>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return empty list if no dosing specifications found
            if (items == null || !items.Any())
                return new List<DosingSpecificationDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new DosingSpecificationDto { DosingSpecification = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<DosingSpecificationDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Equivalent entities for the product. Retrieves equivalent 
        /// entity records for a specified product ID and transforms 
        /// them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find equivalent entities for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of EquivalentEntityDto objects representing the equivalent entities, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > EquivalentEntityDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var equivalentEntities = await buildEquivalentEntitiesAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.EquivalentEntity"/>
        /// <seealso cref="EquivalentEntityDto"/>
        private static async Task<List<EquivalentEntityDto>> buildEquivalentEntitiesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null) return new List<EquivalentEntityDto>();

            // Query equivalent entities for the specified product using read-only tracking
            var items = await db.Set<Label.EquivalentEntity>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return empty list if no equivalent entities found
            if (items == null || !items.Any())
                return new List<EquivalentEntityDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new EquivalentEntityDto { EquivalentEntity = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<EquivalentEntityDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Equivalent entities for the product where ProductID equals PrimaryProductID. Retrieves 
        /// part of assembly records for a specified product ID and transforms them into 
        /// DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the primary product to find assembly parts for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of PartOfAssemblyDto objects representing the assembly parts, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > PartOfAssemblyDto
        /// The query filters by PrimaryProductID to find parts that belong to the specified product assembly.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var assemblyParts = await buildPartOfAssembliesAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.PartOfAssembly"/>
        /// <seealso cref="PartOfAssemblyDto"/>
        private static async Task<List<PartOfAssemblyDto>> buildPartOfAssembliesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null) return new List<PartOfAssemblyDto>();

            // Query assembly parts where the product is the primary product using read-only tracking
            var items = await db.Set<Label.PartOfAssembly>()
                .AsNoTracking()
                .Where(e => e.PrimaryProductID == productID)
                .ToListAsync();

            // Return empty list if no assembly parts found
            if (items == null || !items.Any())
                return new List<PartOfAssemblyDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new PartOfAssemblyDto { PartOfAssembly = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<PartOfAssemblyDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Policies for the product where ProductID equals productID. Retrieves 
        /// policy records for a specified product ID and transforms them into 
        /// DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find policies for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of PolicyDto objects representing the policies, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > PolicyDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var policies = await buildPoliciesAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.Policy"/>
        /// <seealso cref="PolicyDto"/>
        private static async Task<List<PolicyDto>> buildPoliciesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null) return new List<PolicyDto>();

            // Query policies for the specified product using read-only tracking
            var items = await db.Set<Label.Policy>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return empty list if no policies found
            if (items == null || !items.Any())
                return new List<PolicyDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new PolicyDto { Policy = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<PolicyDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores specialized kind information, like device product classification 
        /// or cosmetic category. Retrieves specialized kind records for a specified 
        /// product ID and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find specialized kinds for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of SpecializedKindDto objects representing the specialized kinds, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > SpecializedKindsDto
        /// Specialized kinds include information such as device product classifications or cosmetic categories.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var specializedKinds = await buildSpecializedKindsAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.SpecializedKind"/>
        /// <seealso cref="SpecializedKindDto"/>
        private static async Task<List<SpecializedKindDto>> buildSpecializedKindsAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null) return new List<SpecializedKindDto>();

            // Query specialized kinds for the specified product using read-only tracking
            var items = await db.Set<Label.SpecializedKind>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return empty list if no specialized kinds found
            if (items == null || !items.Any())
                return new List<SpecializedKindDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new SpecializedKindDto { SpecializedKind = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<SpecializedKindDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a product DTO for a specified product ID.
        /// Helper method to retrieve product entity by primary key.
        /// </summary>
        /// <param name="db">The database context for querying product entities.</param>
        /// <param name="productID">The product identifier to retrieve.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A ProductDto object with encrypted ID, or null if not found.</returns>
        /// <seealso cref="Label.Product"/>
        /// <seealso cref="ProductDto"/>
        /// <remarks>
        /// For full hierarchy, consider calling buildProductsAsync if you need full population.
        /// </remarks>
        private static async Task<ProductDto?> buildProductDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product ID is provided
            if (productID == null)
                return null;

            // Query product for the specified product ID
            var entity = await db.Set<Label.Product>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ProductID == productID);

            // Return null if no entity found
            if (entity == null)
                return null;

            // Transform entity to DTO with encrypted ID
            return new ProductDto
            {
                Product = entity.ToEntityWithEncryptedId(pkSecret, logger)
                // For full hierarchy, consider calling buildProductsAsync if you need full population.
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of GenericMedicine DTOs for the specified product.
        /// Retrieves non-proprietary medicine names associated with a product.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productID">The product ID to find generic medicines for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of GenericMedicine DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.GenericMedicine"/>
        private static async Task<List<GenericMedicineDto>> buildGenericMedicinesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productID == null) return new List<GenericMedicineDto>();

            // Query generic medicines for the specified product
            var items = await db.Set<Label.GenericMedicine>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<GenericMedicineDto>();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new GenericMedicineDto { GenericMedicine = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductIdentifier DTOs for the specified product.
        /// Retrieves various types of identifiers associated with a product such as NDC, GTIN, etc.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productID">The product ID to find product identifiers for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductIdentifier DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ProductIdentifier"/>
        private static async Task<List<ProductIdentifierDto>> buildProductIdentifiersAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productID == null) return new List<ProductIdentifierDto>();

            // Query product identifiers for the specified product
            var items = await db.Set<Label.ProductIdentifier>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ProductIdentifierDto { ProductIdentifier = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductRouteOfAdministration DTOs for the specified product.
        /// Retrieves routes of administration associated with a product or part.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productID">The product ID to find routes of administration for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductRouteOfAdministration DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ProductRouteOfAdministration"/>
        private static async Task<List<ProductRouteOfAdministrationDto>> buildProductRouteOfAdministrationsAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productID == null) return new List<ProductRouteOfAdministrationDto>();

            // Query product routes of administration for the specified product
            var items = await db.Set<Label.ProductRouteOfAdministration>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ProductRouteOfAdministrationDto { ProductRouteOfAdministration = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductWebLink DTOs for the specified product.
        /// Retrieves web page links for cosmetic products.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productID">The product ID to find web links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductWebLink DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ProductWebLink"/>
        private static async Task<List<ProductWebLinkDto>> buildProductWebLinksAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productID == null) return new List<ProductWebLinkDto>();

            // Query product web links for the specified product
            var items = await db.Set<Label.ProductWebLink>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ProductWebLinkDto { ProductWebLink = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of marketing category DTOs for a specified product ID.
        /// ProductID is used to get MarketingCategory, then fetch by MarketingCategoryID.
        /// </summary>
        /// <param name="db">The database context for querying marketing category entities.</param>
        /// <param name="productID">The product identifier to filter marketing categories.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of MarketingCategoryDto objects, or null if no product ID provided or no entities found.</returns>
        /// <seealso cref="Label.MarketingCategory"/>
        /// <seealso cref="MarketingCategoryDto"/>
        private static async Task<List<MarketingCategoryDto>> buildMarketingCategoriesDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product ID is provided
            if (productID == null)
                return new List<MarketingCategoryDto>();

            // Query marketing categories for the specified product
            var entity = await db.Set<Label.MarketingCategory>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return new List<MarketingCategoryDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new MarketingCategoryDto
            {
                MarketingCategory = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of product part DTOs for a specified product ID.
        /// ProductPart has a one-to-many relationship with Product (KitProductID == productID).
        /// </summary>
        /// <param name="db">The database context for querying product part entities.</param>
        /// <param name="productID">The product identifier used as kit product ID to filter product parts.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of ProductPartDto objects, or null if no product ID provided or no entities found.</returns>
        /// <seealso cref="Label.ProductPart"/>
        /// <seealso cref="ProductPartDto"/>
        private static async Task<List<ProductPartDto>> buildProductPartsDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product ID is provided
            if (productID == null)
                return new List<ProductPartDto>();

            // Query product parts for the specified kit product
            var entity = await db.Set<Label.ProductPart>()
                .AsNoTracking()
                .Where(e => e.KitProductID == productID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<ProductPartDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity
                .Select(e => new ProductPartDto { ProductPart = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<ProductPartDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of characteristic DTOs for a specified product ID or packaging level ID,
        /// including associated packaging levels. Stores characteristics of a product or package 
        /// (subjectOf characteristic).
        /// </summary>
        /// <param name="db">The database context for querying characteristic entities.</param>
        /// <param name="productID">The product identifier to filter characteristics. Optional if packagingLevelID is provided.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <param name="sectionId">Optional section ID used for compliance actions in indexing files.</param>
        /// <param name="packagingLevelID">Optional packaging level identifier to filter characteristics for package-level properties.</param>
        /// <returns>A list of CharacteristicDto objects with associated packaging levels.</returns>
        /// <seealso cref="Label.Characteristic"/>
        /// <seealso cref="CharacteristicDto"/>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="buildPackageIdentifierDtoAsync"/>
        /// <remarks>
        /// Based on Section 3.1.9 and enhanced for package-level characteristics per _Packaging.cshtml template.
        /// Relates to the Characteristic table (ProductID and PackagingLevelID foreign keys).
        /// Can filter by either ProductID or PackagingLevelID to support both product-level and package-level characteristics.
        /// When filtering by PackagingLevelID, retrieves characteristics specific to that packaging level.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get product-level characteristics
        /// var productChars = await buildCharacteristicsDtoAsync(db, 123, pkSecret, logger);
        /// 
        /// // Get package-level characteristics
        /// var packageChars = await buildCharacteristicsDtoAsync(db, null, pkSecret, logger, null, 456);
        /// </code>
        /// </example>
        private static async Task<List<CharacteristicDto>> buildCharacteristicsDtoAsync(
            ApplicationDbContext db,
            int? productID,
            string pkSecret,
            ILogger logger,
            int? sectionId = null,
            int? packagingLevelID = null)
        {
            #region implementation

            #region validation
            // Return empty list if neither product ID nor packaging level ID is provided
            if (productID == null && packagingLevelID == null)
                return new List<CharacteristicDto>();
            #endregion

            #region query construction
            // Build query based on provided parameters
            var query = db.Set<Label.Characteristic>()
                .AsNoTracking()
                .AsQueryable();

            // Apply appropriate filter based on which ID was provided
            if (productID != null)
            {
                query = query.Where(e => e.ProductID == productID);
            }
            else if (packagingLevelID != null)
            {
                query = query.Where(e => e.PackagingLevelID == packagingLevelID);
            }

            // Execute query to retrieve characteristic entities
            var entities = await query.ToListAsync();
            #endregion

            #region dto building
            var dtos = new List<CharacteristicDto>();
            List<PackagingLevelDto?> pkgLevelDtos = new List<PackagingLevelDto?>();

            // Process each characteristic and build associated packaging levels
            foreach (var item in entities)
            {
                // Build associated PackageIdentifiers if PackagingLevelID is present
                var packageIdentifiers = new List<PackageIdentifierDto?>();

                if (item.PackagingLevelID != null)
                {
                    // Build package identifier for this characteristic's packaging level
                    var pkgDto = await buildPackageIdentifierDtoAsync(
                        db,
                        item.PackagingLevelID,
                        pkSecret,
                        logger,
                        sectionId);

                    if (pkgDto?.PackageIdentifier != null)
                    {
                        packageIdentifiers.Add(pkgDto);
                    }

                    // Build packaging level details if needed for context
                    // Note: Avoid recursive loops - only fetch if characteristic is product-level
                    if (productID != null)
                    {
                        List<PackagingLevelDto>? itemPackagingLevels = (await buildPackagingLevelsDtoAsync(
                            db,
                            item.PackagingLevelID,
                            pkSecret,
                            logger))
                            ?.ToList();

                        if (itemPackagingLevels != null && itemPackagingLevels.Any())
                            pkgLevelDtos.AddRange(itemPackagingLevels);
                    }
                }

                // Create characteristic DTO with packaging levels and identifiers
                dtos.Add(new CharacteristicDto
                {
                    Characteristic = item.ToEntityWithEncryptedId(pkSecret, logger),
                    PackagingIdentifiers = packageIdentifiers,
                    PackagingLevels = pkgLevelDtos
                });
            }

            return dtos;
            #endregion

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of BusinessOperationProductLink DTOs for the specified product.
        /// Retrieves links between business operations performed by establishments and specific products.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productId">The product ID to find business operation links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of BusinessOperationProductLink DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.BusinessOperationProductLink"/>
        private static async Task<List<BusinessOperationProductLinkDto>> buildBusinessOperationProductLinksAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productId == null) return new List<BusinessOperationProductLinkDto>();

            // Query business operation product links for the specified product
            var items = await db.Set<Label.BusinessOperationProductLink>()
                .AsNoTracking()
                .Where(e => e.ProductID == productId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new BusinessOperationProductLinkDto { BusinessOperationProductLink = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ResponsiblePersonLink DTOs for the specified product.
        /// Retrieves links between cosmetic products and their responsible person organizations.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productId">The product ID to find responsible person links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ResponsiblePersonLink DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ResponsiblePersonLink"/>
        private static async Task<List<ResponsiblePersonLinkDto>> buildResponsiblePersonLinksAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productId == null) return new List<ResponsiblePersonLinkDto>();

            // Query responsible person links for the specified product
            var items = await db.Set<Label.ResponsiblePersonLink>()
                .AsNoTracking()
                .Where(e => e.ProductID == productId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ResponsiblePersonLinkDto { ResponsiblePersonLink = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }
        #endregion
    }
}