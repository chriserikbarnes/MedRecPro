using MedRecPro.Models;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Interface for preparing product data for rendering by handling filtering,
    /// ordering, and attribute generation logic.
    /// </summary>
    /// <seealso cref="ProductDto"/>
    /// <seealso cref="ProductRendering"/>
    public interface IProductRenderingService
    {
        #region core methods

        /**************************************************************/
        /// <summary>
        /// Enhanced PrepareForRendering method with ingredient rendering integration.
        /// </summary>
        /// <param name="product">The product to prepare for rendering</param>
        /// <param name="additionalParams">Additional context parameters as needed</param>
        /// <param name="ingredientRenderingService">Optional ingredient rendering service for enhanced processing</param>
        /// <param name="packageRenderingService">Optional package rendering service</param>
        /// <returns>A fully prepared ProductRendering object with enhanced ingredients</returns>
        /// <seealso cref="ProductRendering"/>
        /// <seealso cref="IIngredientRenderingService"/>
        ProductRendering PrepareForRendering(ProductDto product,
            object? additionalParams = null,
            IIngredientRenderingService? ingredientRenderingService = null,
            IPackageRenderingService? packageRenderingService = null);

        /**************************************************************/
        /// <summary>
        /// Generates appropriate NDC product identifier for product.
        /// </summary>
        /// <param name="product">The product to generate NDC identifier for</param>
        /// <returns>NDC product identifier or null if none exists</returns>
        /// <seealso cref="ProductDto"/>
        /// <seealso cref="ProductIdentifierDto"/>
        ProductIdentifierDto? GetNdcProductIdentifier(ProductDto product);

        /**************************************************************/
        /// <summary>
        /// Determines if product has valid data for specific operations.
        /// </summary>
        /// <param name="product">The product to validate</param>
        /// <returns>True if product has valid data</returns>
        /// <seealso cref="ProductDto"/>
        bool HasValidData(ProductDto product);

        /**************************************************************/
        /// <summary>
        /// Gets active ingredients ordered by business rules.
        /// </summary>
        /// <param name="product">The product containing ingredients</param>
        /// <returns>Ordered list of active ingredients or null if none exists</returns>
        /// <seealso cref="IngredientDto"/>
        List<IngredientDto>? GetOrderedActiveIngredients(ProductDto product);

        /**************************************************************/
        /// <summary>
        /// Gets inactive ingredients ordered by business rules.
        /// </summary>
        /// <param name="product">The product containing ingredients</param>
        /// <returns>Ordered list of inactive ingredients or null if none exists</returns>
        /// <seealso cref="IngredientDto"/>
        List<IngredientDto>? GetOrderedInactiveIngredients(ProductDto product);

        /**************************************************************/
        /// <summary>
        /// Gets characteristics ordered by business rules.
        /// </summary>
        /// <param name="product">The product containing characteristics</param>
        /// <returns>Ordered list of characteristics or null if none exists</returns>
        /// <seealso cref="CharacteristicDto"/>
        List<CharacteristicDto>? GetOrderedCharacteristics(ProductDto product);

        /**************************************************************/
        /// <summary>
        /// Gets top-level packaging levels ordered by business rules.
        /// </summary>
        /// <param name="product">The product containing packaging levels</param>
        /// <returns>Ordered list of top-level packaging or null if none exists</returns>
        /// <seealso cref="PackagingLevelDto"/>
        List<PackagingLevelDto>? GetOrderedTopLevelPackaging(ProductDto product);

        /**************************************************************/
        /// <summary>
        /// Gets routes ordered by business rules.
        /// </summary>
        /// <param name="product">The product containing routes</param>
        /// <returns>Ordered list of routes or null if none exists</returns>
        /// <seealso cref="RouteDto"/>
        List<RouteDto>? GetOrderedRoutes(ProductDto product);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Service for preparing product data for rendering by handling filtering,
    /// ordering, and attribute generation logic. Separates view logic from templates.
    /// </summary>
    /// <seealso cref="IProductRenderingService"/>
    /// <seealso cref="ProductDto"/>
    /// <seealso cref="ProductDto"/>
    /// <remarks>
    /// This service encapsulates all business logic that was previously
    /// embedded in Razor views, promoting better separation of concerns
    /// and testability.
    /// </remarks>
    public class ProductRenderingService : IProductRenderingService
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// Ingredient type constants for filtering operations.
        /// </summary>
        private const string ACTIVE_INGREDIENT_TYPE = "activeIngredient";
        private const string INACTIVE_INGREDIENT_TYPE = "inactiveIngredient";

        /**************************************************************/
        /// <summary>
        /// NDC identifier type constants for product identifier filtering.
        /// </summary>
        private const string NDC_IDENTIFIER_TYPE = "NDC";
        private const string NDC_PRODUCT_IDENTIFIER_TYPE = "NDCProduct";

        private IPackageRenderingService? _packageRenderingService;

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Enhanced PrepareForRendering method with comprehensive packaging and ingredient rendering integration.
        /// Prepares a complete ProductRendering object with all computed properties including optimized packaging collections
        /// for efficient template rendering following the established ingredient pattern for backward compatibility.
        /// </summary>
        /// <param name="product">The product to prepare for rendering</param>
        /// <param name="additionalParams">Additional context parameters as needed</param>
        /// <param name="ingredientRenderingService">Optional ingredient rendering service for enhanced processing</param>
        /// <param name="packageRenderingService">Optional package rendering service for enhanced processing</param>
        /// <returns>A fully prepared ProductRendering object with enhanced ingredients and packaging</returns>
        /// <seealso cref="ProductRendering"/>
        /// <seealso cref="ProductDto"/>
        /// <seealso cref="IIngredientRenderingService"/>
        /// <seealso cref="IPackageRenderingService"/>
        /// <example>
        /// <code>
        /// var preparedProduct = service.PrepareForRendering(
        ///     product: productDto,
        ///     additionalParams: new { DocumentGuid = documentGuid },
        ///     ingredientRenderingService: ingredientService,
        ///     packageRenderingService: packageService
        /// );
        /// // preparedProduct now has all computed properties with enhanced ingredients and packaging ready for rendering
        /// </code>
        /// </example>
        /// <remarks>
        /// The enhanced preparation process follows the ingredient pattern:
        /// - All existing product property computation
        /// - Optional ingredient enhancement via ingredientRenderingService
        /// - Optional packaging enhancement via packageRenderingService
        /// - Maintains backward compatibility through optional parameters
        /// </remarks>
        public ProductRendering PrepareForRendering(ProductDto product,
            object? additionalParams = null,
            IIngredientRenderingService? ingredientRenderingService = null,
            IPackageRenderingService? packageRenderingService = null)
        {
            #region implementation

            if (product == null)
                throw new ArgumentNullException(nameof(product));

            // Use provided package rendering service or default to internal instance
            _packageRenderingService = packageRenderingService ?? new PackageRenderingService();

            // Create base product rendering with existing logic
            var productRendering = new ProductRendering
            {
                ProductDto = product,

                // Pre-compute existing rendering properties
                NdcProductIdentifier = GetNdcProductIdentifier(product),
                HasValidData = HasValidData(product),
                OrderedActiveIngredients = GetOrderedActiveIngredients(product),
                OrderedInactiveIngredients = GetOrderedInactiveIngredients(product),
                OrderedCharacteristics = GetOrderedCharacteristics(product),
                OrderedTopLevelPackaging = GetOrderedTopLevelPackaging(product),
                OrderedRoutes = GetOrderedRoutes(product),

                // Pre-compute availability flags
                HasNdcIdentifier = GetNdcProductIdentifier(product) != null,
                HasActiveIngredients = GetOrderedActiveIngredients(product)?.Any() == true,
                HasInactiveIngredients = GetOrderedInactiveIngredients(product)?.Any() == true,
                HasCharacteristics = GetOrderedCharacteristics(product)?.Any() == true,
                HasTopLevelPackaging = GetOrderedTopLevelPackaging(product)?.Any() == true,
                HasRoutes = GetOrderedRoutes(product)?.Any() == true,
                HasGenericMedicines = product.GenericMedicines?.Any() == true
            };

            additionalParams ??= new { product };

            // Process enhanced ingredients if service is provided
            if (ingredientRenderingService != null)
            {
                processIngredients(productRendering, ingredientRenderingService, additionalParams);
            }

            processPackagingForRendering(product, productRendering, additionalParams);

            return productRendering;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates appropriate NDC product identifier for product based on
        /// business rules and identifier type filtering.
        /// </summary>
        /// <param name="product">The product to generate NDC identifier for</param>
        /// <returns>NDC product identifier or null if none exists</returns>
        /// <seealso cref="ProductDto"/>
        /// <seealso cref="ProductIdentifierDto"/>
        /// <example>
        /// <code>
        /// var ndcIdentifier = service.GetNdcProductIdentifier(product);
        /// // Returns: NDC identifier or null
        /// </code>
        /// </example>
        public ProductIdentifierDto? GetNdcProductIdentifier(ProductDto product)
        {
            #region implementation

            if (product?.ProductIdentifiers == null)
                return null;

            // Find NDC or NDCProduct identifier using business logic from template
            return product.ProductIdentifiers
                .FirstOrDefault(pi =>
                    string.Equals(pi.IdentifierType, NDC_IDENTIFIER_TYPE, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(pi.IdentifierType, NDC_PRODUCT_IDENTIFIER_TYPE, StringComparison.OrdinalIgnoreCase));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if product has valid data for specific operations.
        /// </summary>
        /// <param name="product">The product to validate</param>
        /// <returns>True if product has valid data</returns>
        /// <seealso cref="ProductDto"/>
        public bool HasValidData(ProductDto product)
        {
            #region implementation

            if (product == null)
                return false;

            // Basic validation - can be extended based on business requirements
            return !string.IsNullOrEmpty(product.ProductName) ||
                   product.ProductIdentifiers?.Any() == true ||
                   product.Ingredients?.Any() == true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets active ingredients ordered by business rules.
        /// </summary>
        /// <param name="product">The product containing ingredients</param>
        /// <returns>Ordered list of active ingredients or null if none exists</returns>
        /// <seealso cref="IngredientDto"/>
        public List<IngredientDto>? GetOrderedActiveIngredients(ProductDto product)
        {
            #region implementation

            if (product?.Ingredients == null)
                return null;

            var activeIngredients = product.Ingredients
                .Where(i => string.Equals(i.OriginatingElement, ACTIVE_INGREDIENT_TYPE, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.SequenceNumber)
                .ToList();

            return activeIngredients.Any() ? activeIngredients : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets inactive ingredients ordered by business rules.
        /// </summary>
        /// <param name="product">The product containing ingredients</param>
        /// <returns>Ordered list of inactive ingredients or null if none exists</returns>
        /// <seealso cref="IngredientDto"/>
        public List<IngredientDto>? GetOrderedInactiveIngredients(ProductDto product)
        {
            #region implementation

            if (product?.Ingredients == null)
                return null;

            var inactiveIngredients = product.Ingredients
                .Where(i => string.Equals(i.OriginatingElement, INACTIVE_INGREDIENT_TYPE, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.SequenceNumber)
                .ToList();

            return inactiveIngredients.Any() ? inactiveIngredients : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets characteristics ordered by business rules.
        /// </summary>
        /// <param name="product">The product containing characteristics</param>
        /// <returns>Ordered list of characteristics or null if none exists</returns>
        /// <seealso cref="CharacteristicDto"/>
        public List<CharacteristicDto>? GetOrderedCharacteristics(ProductDto product)
        {
            #region implementation

            if (product?.Characteristics == null)
                return null;

            var orderedCharacteristics = product.Characteristics
                .OrderBy(c => c.CharacteristicID)
                .ToList();

            return orderedCharacteristics.Any() ? orderedCharacteristics : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets top-level packaging levels ordered by business rules.
        /// Filters for packaging that has no hierarchy parent (top-level only).
        /// A packaging level is considered top-level if its ID is not found as an InnerPackagingLevelID
        /// in any PackagingHierarchy entry across all packaging levels.
        /// </summary>
        /// <param name="product">The product containing packaging levels</param>
        /// <returns>Ordered list of top-level packaging or null if none exists</returns>
        /// <seealso cref="PackagingLevelDto"/>
        public List<PackagingLevelDto>? GetOrderedTopLevelPackaging(ProductDto product)
        {
            #region implementation
            if (product?.PackagingLevels == null)
                return null;

            // Collect all PackagingHierarchy entries from all packaging levels in the product
            var allHierarchyEntries = product.PackagingLevels
                .Where(p => p.PackagingHierarchy != null)
                .SelectMany(p => p.PackagingHierarchy)
                .ToList();

            // Get all InnerPackagingLevelIDs - these represent packaging levels that are children
            // in some hierarchy relationship (i.e., they are contained within other packages)
            var childPackagingLevelIds = allHierarchyEntries
                .Where(h => h != null 
                    && h.InnerPackagingLevelID != null 
                    && h.InnerPackagingLevelID.HasValue)
                .Select(h => h!.InnerPackagingLevelID!.Value)
                .ToHashSet();

            // Select packaging levels that are top-level, which includes:
            // 1. Packaging levels that are parents in hierarchy but never children (traditional tops)
            // 2. Packaging levels that don't appear in any hierarchy at all (standalone items)
            // Both cases are covered by: PackagingLevelID NOT in childPackagingLevelIds
            var topLevelPackaging = product.PackagingLevels
                .Where(p => p.PackagingLevelID.HasValue &&
                           !childPackagingLevelIds.Contains(p.PackagingLevelID.Value))
                .OrderBy(p => p.PackagingLevelID)
                .ToList();

            return topLevelPackaging.Any() ? topLevelPackaging : null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets routes ordered by business rules.
        /// </summary>
        /// <param name="product">The product containing routes</param>
        /// <returns>Ordered list of routes or null if none exists</returns>
        /// <seealso cref="RouteDto"/>
        public List<RouteDto>? GetOrderedRoutes(ProductDto product)
        {
            #region implementation

            if (product?.ProductRouteOfAdministrations == null || !product.ProductRouteOfAdministrations.Any())
                return null;

            return product.ProductRouteOfAdministrations
                .Select(pra => new RouteDto
                {
                    Route = new Dictionary<string, object?>
                    {
                        [nameof(RouteDto.RouteCode)] = pra.RouteCode,
                        [nameof(RouteDto.RouteCodeSystem)] = pra.RouteCodeSystem ?? product.FormCodeSystem,
                        [nameof(RouteDto.RouteDisplayName)] = pra.RouteDisplayName,
                        [nameof(RouteDto.ProductRouteOfAdministrationID)] = pra.ProductRouteOfAdministrationID,
                        [nameof(RouteDto.ProductID)] = product.ProductID
                    }
                })
                .OrderBy(r => r.ProductRouteOfAdministrationID ?? 0)
                .ToList();

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// NEW METHOD: Processes ingredients within a product for enhanced rendering contexts.
        /// Creates enhanced IngredientRendering objects and populates both unified and filtered collections
        /// for optimal template processing performance.
        /// </summary>
        /// <param name="productRendering">The product rendering context to enhance with ingredient data</param>
        /// <param name="ingredientRenderingService">Service for creating enhanced ingredient rendering contexts</param>
        /// <param name="additionalParams">Additional context parameters for ingredient processing</param>
        /// <seealso cref="ProductRendering.Ingredients"/>
        /// <seealso cref="ProductRendering.ActiveIngredients"/>
        /// <seealso cref="ProductRendering.InactiveIngredients"/>
        /// <seealso cref="IIngredientRenderingService.PrepareForRendering"/>
        /// <remarks>
        /// Enhanced ingredient processing workflow:
        /// - Process all ingredients with enhanced rendering service
        /// - Create unified EnhancedIngredients collection
        /// - Filter into separate active and inactive collections
        /// - Set appropriate availability flags for template optimization
        /// 
        /// The enhanced collections provide pre-computed properties and eliminate
        /// the need for complex logic in templates.
        /// </remarks>
        private static void processIngredients(
            ProductRendering productRendering,
            IIngredientRenderingService ingredientRenderingService,
            object? additionalParams)
        {
            #region implementation

            var product = productRendering.ProductDto;

            // Process ingredients if they exist
            if (product?.Ingredients?.Any() == true)
            {
                var ingredients = new List<IngredientRendering>();
                var activeIngredients = new List<IngredientRendering>();
                var inactiveIngredients = new List<IngredientRendering>();

                // Process each ingredient with enhanced service
                foreach (var ingredient in product.Ingredients)
                {
                    // Create enhanced ingredient rendering context
                    var enhancedIngredient = ingredientRenderingService.PrepareForRendering(
                        ingredient: ingredient,
                        additionalParams: additionalParams
                    );

                    // Add to unified collection
                    ingredients.Add(enhancedIngredient);

                    // Filter into appropriate type-specific collections
                    if (enhancedIngredient.IsActiveIngredient)
                    {
                        activeIngredients.Add(enhancedIngredient);
                    }
                    else
                    {
                        inactiveIngredients.Add(enhancedIngredient);
                    }
                }

                // Populate ingredient collections
                productRendering.Ingredients = ingredients;
                productRendering.ActiveIngredients = activeIngredients.Any() ? activeIngredients : null;
                productRendering.InactiveIngredients = inactiveIngredients.Any() ? inactiveIngredients : null;

                // Set ingredient availability flags  
                // Note: Use existing HasActiveIngredients and HasInactiveIngredients flags
                // which are already computed above in the main product preparation
            }
            else
            {
                // No ingredients - initialize ingredient collections as null
                productRendering.Ingredients = null;
                productRendering.ActiveIngredients = null;
                productRendering.InactiveIngredients = null;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes packaging within a product for enhanced rendering with comprehensive PackageRendering integration.
        /// Creates enhanced PackageRendering objects from OrderedTopLevelPackaging and stores them in the product's PackageRendering collection
        /// for optimal template processing performance with recursive packaging hierarchy processing.
        /// </summary>
        /// <param name="productDto">Required ProductDto for identifier correlation</param>
        /// <param name="productRendering">The product rendering context containing packaging to process</param>
        /// <param name="additionalParams">Additional context parameters for packaging processing</param>
        /// <seealso cref="ProductRendering.OrderedTopLevelPackaging"/>
        /// <seealso cref="ProductRendering.PackageRendering"/>
        /// <seealso cref="IPackageRenderingService.PrepareForRendering"/>
        /// <seealso cref="PackageRendering"/>
        /// <remarks>
        /// Packaging processing workflow:
        /// - Validation of existing packaging in OrderedTopLevelPackaging collection
        /// - Enhanced PackageRendering creation for each packaging level
        /// - Recursive processing of child packaging hierarchies
        /// - Additional parameters inclusion for context
        /// - Enhanced packaging collection population in product rendering context
        /// - Performance tracking and logging for monitoring
        /// 
        /// The enhanced packaging provides optimized template processing with pre-computed properties.
        /// If no packaging exists, the enhanced packaging collections are properly initialized as empty.
        /// </remarks>
        private void processPackagingForRendering(ProductDto productDto, ProductRendering productRendering, object? additionalParams)
        {
            #region implementation

            if (productRendering == null)
                throw new ArgumentNullException(nameof(productRendering));

            if (_packageRenderingService == null)
                return;

            // Process packaging if it exists within this product's OrderedTopLevelPackaging collection
            if (productRendering.HasTopLevelPackaging && productRendering.OrderedTopLevelPackaging?.Any() == true)
            {

                // Initialize enhanced packaging collection for optimized template processing
                var enhancedPackaging = new List<PackageRendering>();

                // Process each packaging level in the ordered collection for enhanced rendering preparation
                foreach (var packagingLevel in productRendering.OrderedTopLevelPackaging)
                {
                    // Create enhanced PackageRendering using the service with comprehensive property computation
                    var enhancedPackageRendering = _packageRenderingService.PrepareForRendering(
                        packagingLevel: packagingLevel,
                        parentProduct: productDto,
                        additionalParams: additionalParams
                    );

                    // Process child packaging recursively if it exists
                    processChildPackagingRecursively(productDto, enhancedPackageRendering, additionalParams);

                    // Add the enhanced packaging rendering to the collection
                    enhancedPackaging.Add(enhancedPackageRendering);
                }

                // Store the enhanced packaging in the product rendering context for template access
                productRendering.PackageRendering = enhancedPackaging;
                productRendering.HasPackageRendering = enhancedPackaging.Any();
            }
            else
            {
                // No packaging to process - initialize enhanced packaging collections as empty
                productRendering.PackageRendering = null;
                productRendering.HasPackageRendering = false;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes child packaging recursively for comprehensive hierarchy rendering.
        /// Creates enhanced PackageRendering objects for all levels of the packaging hierarchy
        /// to provide complete nested packaging optimization for template processing.
        /// </summary>
        /// <param name="productDto">Required ProductDto for identifier correlation</param>
        /// <param name="packageRendering">The parent package rendering context to process children for</param>
        /// <param name="additionalParams">Additional context parameters for child processing</param>
        /// <seealso cref="PackageRendering.OrderedChildPackaging"/>
        /// <seealso cref="PackageRendering.ChildPackageRendering"/>
        /// <seealso cref="IPackageRenderingService.PrepareForRendering"/>
        /// <remarks>
        /// Recursive processing ensures that all levels of packaging hierarchy are optimized:
        /// - Child packaging level creation through PackageRenderingService
        /// - Recursive descent through packaging hierarchy
        /// - Complete pre-computation at all levels
        /// - Optimal template performance for complex packaging structures
        /// </remarks>
        private void processChildPackagingRecursively(ProductDto productDto, PackageRendering packageRendering, object? additionalParams)
        {
            #region implementation

            if (packageRendering == null || _packageRenderingService == null)
                return;

            // Process child packaging if it exists
            if (packageRendering.HasChildPackaging && packageRendering.OrderedChildPackaging?.Any() == true)
            {
                var enhancedChildPackaging = new List<PackageRendering>();

                // Process each child packaging hierarchy
                foreach (var childHierarchy in packageRendering.OrderedChildPackaging)
                {
                    if (childHierarchy.ChildPackagingLevel != null)
                    {
                        // Create enhanced PackageRendering for the child level
                        var childPackageRendering = _packageRenderingService.PrepareForRendering(
                            packagingLevel: childHierarchy.ChildPackagingLevel,
                            parentProduct: productDto,
                            additionalParams: additionalParams
                        );

                        // Recursively process children of this child
                        processChildPackagingRecursively(productDto, childPackageRendering, additionalParams);

                        enhancedChildPackaging.Add(childPackageRendering);
                    }
                }

                // Store enhanced child packaging
                packageRendering.ChildPackageRendering = enhancedChildPackaging.Any() ? enhancedChildPackaging : null;
                packageRendering.HasChildPackageRendering = enhancedChildPackaging.Any();
            }
            else
            {
                packageRendering.ChildPackageRendering = null;
                packageRendering.HasChildPackageRendering = false;
            }

            #endregion
        }

        #endregion private methods
    }
}