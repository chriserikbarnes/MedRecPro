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
        /// <returns>A fully prepared ProductRendering object with enhanced ingredients</returns>
        /// <seealso cref="ProductRendering"/>
        /// <seealso cref="IIngredientRenderingService"/>
        ProductRendering PrepareForRendering(ProductDto product, 
            object? additionalParams = null, 
            IIngredientRenderingService? ingredientRenderingService = null);

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

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Enhanced PrepareForRendering method with comprehensive ingredient rendering preparation.
        /// Creates ProductRendering object with all computed properties including enhanced ingredients
        /// for optimal template rendering performance.
        /// </summary>
        /// <param name="product">The product to prepare for rendering</param>
        /// <param name="additionalParams">Additional context parameters as needed</param>
        /// <param name="ingredientRenderingService">Optional ingredient rendering service for enhanced processing</param>
        /// <returns>A fully prepared ProductRendering object with enhanced ingredients</returns>
        /// <seealso cref="ProductRendering"/>
        /// <seealso cref="IIngredientRenderingService"/>
        /// <seealso cref="processIngredients"/>
        /// <example>
        /// <code>
        /// var preparedProduct = service.PrepareForRendering(
        ///     product: productDto,
        ///     additionalParams: contextData,
        ///     ingredientRenderingService: ingredientService
        /// );
        /// // preparedProduct now has all computed properties and enhanced ingredients ready for rendering
        /// </code>
        /// </example>
        public ProductRendering PrepareForRendering(ProductDto product, 
            object? additionalParams = null, 
            IIngredientRenderingService? ingredientRenderingService = null)
        {
            #region implementation

            if (product == null)
                throw new ArgumentNullException(nameof(product));

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

            // Process enhanced ingredients if service is provided
            if (ingredientRenderingService != null)
            {
                processIngredients(productRendering, ingredientRenderingService, additionalParams);
            }

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
        /// Filters for packaging that has no hierarchy (top-level only).
        /// </summary>
        /// <param name="product">The product containing packaging levels</param>
        /// <returns>Ordered list of top-level packaging or null if none exists</returns>
        /// <seealso cref="PackagingLevelDto"/>
        public List<PackagingLevelDto>? GetOrderedTopLevelPackaging(ProductDto product)
        {
            #region implementation

            if (product?.PackagingLevels == null)
                return null;

            var topLevelPackaging = product.PackagingLevels
                .Where(p => p.PackagingHierarchy == null || !p.PackagingHierarchy.Any())
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

            if (product?.Routes == null)
                return null;

            var orderedRoutes = product.Routes
                .OrderBy(r => r.ProductRouteOfAdministrationID)
                .ToList();

            return orderedRoutes.Any() ? orderedRoutes : null;

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

        #endregion private methods
    }
}