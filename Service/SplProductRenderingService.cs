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
        /// Prepares a complete ProductRendering object with all computed properties
        /// for efficient template rendering.
        /// </summary>
        /// <param name="product">The product to prepare for rendering</param>
        /// <param name="additionalParams">Additional context parameters as needed</param>
        /// <returns>A fully prepared ProductRendering object</returns>
        /// <seealso cref="ProductRendering"/>
        /// <seealso cref="ProductDto"/>
        ProductRendering PrepareForRendering(ProductDto product, object? additionalParams = null);

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
        /// Prepares a complete ProductRendering object with all computed properties
        /// for efficient template rendering. Pre-computes all filtering and ordering
        /// operations to minimize processing in the view layer.
        /// </summary>
        /// <param name="product">The product to prepare for rendering</param>
        /// <param name="additionalParams">Additional context parameters as needed</param>
        /// <returns>A fully prepared ProductRendering object with computed properties</returns>
        /// <seealso cref="ProductRendering"/>
        /// <seealso cref="ProductDto"/>
        /// <example>
        /// <code>
        /// var preparedProduct = service.PrepareForRendering(
        ///     product: productDto,
        ///     additionalParams: contextData
        /// );
        /// // preparedProduct now has all computed properties ready for rendering
        /// </code>
        /// </example>
        public ProductRendering PrepareForRendering(ProductDto product, object? additionalParams = null)
        {
            #region implementation

            if (product == null)
                throw new ArgumentNullException(nameof(product));

            return new ProductRendering
            {
                ProductDto = product,

                // Pre-compute all rendering properties
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
    }
}