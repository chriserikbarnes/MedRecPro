namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Context object for rendering products with pre-computed properties.
    /// Provides product data along with pre-computed rendering properties
    /// for efficient template rendering.
    /// </summary>
    /// <seealso cref="ProductDto"/>
    /// <seealso cref="Label"/>
    public class ProductRendering
    {
        #region core properties

        /**************************************************************/
        /// <summary>
        /// The product to be rendered.
        /// </summary>
        /// <seealso cref="ProductDto"/>
        public required ProductDto ProductDto { get; set; }

        #endregion

        #region pre-computed rendering properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed NDC product identifier for HTML rendering.
        /// Generated from product identifiers with NDC type filtering.
        /// </summary>
        /// <seealso cref="ProductIdentifierDto"/>
        /// <seealso cref="Label"/>
        public ProductIdentifierDto? NdcProductIdentifier { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has valid data.
        /// </summary>
        /// <seealso cref="ProductDto"/>
        /// <seealso cref="Label"/>
        public bool HasValidData { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered active ingredients for efficient rendering.
        /// Null if no active ingredients exist.
        /// </summary>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="Label"/>
        public List<IngredientDto>? OrderedActiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered inactive ingredients for efficient rendering.
        /// Null if no inactive ingredients exist.
        /// </summary>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="Label"/>
        public List<IngredientDto>? OrderedInactiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered characteristics for efficient rendering.
        /// Null if no characteristics exist.
        /// </summary>
        /// <seealso cref="CharacteristicDto"/>
        /// <seealso cref="Label"/>
        public List<CharacteristicDto>? OrderedCharacteristics { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered top-level packaging for efficient rendering.
        /// Null if no top-level packaging exists.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="Label"/>
        public List<PackagingLevelDto>? OrderedTopLevelPackaging { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered routes for efficient rendering.
        /// Null if no routes exist.
        /// </summary>
        /// <seealso cref="RouteDto"/>
        /// <seealso cref="Label"/>
        public List<RouteDto>? OrderedRoutes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered marketing statuses (marketing acts) for efficient rendering.
        /// Contains MarketingStatusDto objects with product-level marketing act information.
        /// Null if no marketing statuses exist.
        /// </summary>
        /// <seealso cref="MarketingStatusDto"/>
        /// <seealso cref="Label"/>
        public List<MarketingStatusDto>? OrderedMarketingStatuses { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has marketing acts to render.
        /// </summary>
        public bool HasMarketingAct { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has NDC identifier.
        /// </summary>
        /// <seealso cref="NdcProductIdentifier"/>
        /// <seealso cref="Label"/>
        public bool HasNdcIdentifier { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has active ingredients to render.
        /// </summary>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="Label"/>
        public bool HasActiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has inactive ingredients to render.
        /// </summary>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="Label"/>
        public bool HasInactiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has characteristics to render.
        /// </summary>
        /// <seealso cref="CharacteristicDto"/>
        /// <seealso cref="Label"/>
        public bool HasCharacteristics { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has top-level packaging to render.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="Label"/>
        public bool HasTopLevelPackaging { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has routes to render.
        /// </summary>
        /// <seealso cref="RouteDto"/>
        /// <seealso cref="Label"/>
        public bool HasRoutes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has generic medicines to render.
        /// </summary>
        /// <seealso cref="GenericMedicineDto"/>
        /// <seealso cref="Label"/>
        public bool HasGenericMedicines { get; set; }

        #endregion

        #region policy properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered policies for efficient rendering.
        /// Contains PolicyDto objects with product-level Policy information.
        /// Null if no marketing statuses exist.
        /// </summary>
        /// <seealso cref="PolicyDto"/>
        /// <seealso cref="Label"/>
        public List<PolicyDto>? OrderedPolicies { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has Policies to render.
        /// </summary>
        public bool HasPolicy { get; set; }
        #endregion

        #region equivalent entity properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered equivalent entities for efficient rendering.
        /// Contains EquivalentEntityDto objects representing product equivalencies.
        /// Null if no equivalent entities exist.
        /// </summary>
        /// <seealso cref="EquivalentEntityDto"/>
        /// <seealso cref="Label"/>
        public List<EquivalentEntityDto>? OrderedEquivalentEntities { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has equivalent entities to render.
        /// </summary>
        /// <seealso cref="EquivalentEntityDto"/>
        /// <seealso cref="Label"/>
        public bool HasEquivalentEntities { get; set; }

        #endregion

        #region marketing status properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered marketing categories for efficient rendering.
        /// Contains MarketingCategoryDto objects with marketing status information.
        /// Null if no marketing categories exist.
        /// </summary>
        /// <seealso cref="MarketingCategoryDto"/>
        /// <seealso cref="Label"/>
        public List<MarketingCategoryDto>? OrderedMarketingCategories { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has marketing status to render.
        /// Based on the presence of marketing categories with valid status information.
        /// </summary>
        /// <seealso cref="MarketingCategoryDto"/>
        /// <seealso cref="Label"/>
        public bool HasMarketingStatus { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed primary marketing category for efficient rendering.
        /// Contains the primary MarketingCategoryDto object for single-status scenarios.
        /// Null if no primary marketing category exists.
        /// </summary>
        /// <seealso cref="MarketingCategoryDto"/>
        /// <seealso cref="Label"/>
        public MarketingCategoryDto? PrimaryMarketingCategory { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has a primary marketing category.
        /// Used for template optimization in single-status rendering scenarios.
        /// </summary>
        /// <seealso cref="PrimaryMarketingCategory"/>
        /// <seealso cref="Label"/>
        public bool HasPrimaryMarketingCategory { get; set; }

        #endregion

        #region ingredient rendering properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed ingredient rendering contexts for efficient template rendering.
        /// Contains IngredientRendering objects with all pre-computed properties instead of raw IngredientDto objects.
        /// This collection provides optimized ingredient data for template processing with pre-computed business logic.
        /// Null if no ingredients exist.
        /// </summary>
        /// <seealso cref="IngredientRendering"/>
        /// <seealso cref="OrderedActiveIngredients"/>
        /// <seealso cref="OrderedInactiveIngredients"/>
        /// <seealso cref="Label"/>
        /// <remarks>
        /// This collection combines both active and inactive ingredients with pre-computed properties.
        /// Use this collection in preference to the raw OrderedActiveIngredients and OrderedInactiveIngredients 
        /// for optimal template performance. Each IngredientRendering object contains pre-computed flags,
        /// formatted values, and ordered collections to eliminate template processing overhead.
        /// </remarks>
        public List<IngredientRendering>? Ingredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed active ingredient rendering contexts for efficient template rendering.
        /// Contains only active IngredientRendering objects filtered and prepared for optimal processing.
        /// Null if no active ingredients exist.
        /// </summary>
        /// <seealso cref="IngredientRendering"/>
        /// <seealso cref="Ingredients"/>
        /// <seealso cref="OrderedActiveIngredients"/>
        /// <seealso cref="Label"/>
        /// <remarks>
        /// This filtered collection contains only active ingredients with optimized properties.
        /// Provides direct access to active ingredients without requiring filtering in templates.
        /// Each object has pre-computed IsActiveIngredient=true and associated active ingredient properties.
        /// </remarks>
        public List<IngredientRendering>? ActiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed inactive ingredient rendering contexts for efficient template rendering.
        /// Contains only inactive IngredientRendering objects filtered and prepared for optimal processing.
        /// Null if no inactive ingredients exist.
        /// </summary>
        /// <seealso cref="IngredientRendering"/>
        /// <seealso cref="Ingredients"/>
        /// <seealso cref="OrderedInactiveIngredients"/>
        /// <seealso cref="Label"/>
        /// <remarks>
        /// This filtered collection contains only inactive ingredients with optimized properties.
        /// Provides direct access to inactive ingredients without requiring filtering in templates.
        /// Each object has pre-computed IsActiveIngredient=false and associated inactive ingredient properties.
        /// </remarks>
        public List<IngredientRendering>? InactiveIngredients { get; set; }

        #endregion

        #region packaging rendering properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed packaging rendering contexts for efficient template rendering.
        /// Contains PackageRendering objects with all pre-computed properties instead of raw PackagingLevelDto objects.
        /// This collection provides optimized packaging data for template processing with pre-computed business logic.
        /// Null if no packaging exists.
        /// </summary>
        /// <seealso cref="PackageRendering"/>
        /// <seealso cref="OrderedTopLevelPackaging"/>
        /// <seealso cref="Label"/>
        /// <remarks>
        /// This collection contains top-level packaging with pre-computed properties for optimal performance.
        /// Use this collection in preference to the raw OrderedTopLevelPackaging for optimal template performance.
        /// Each PackageRendering object contains pre-computed flags, formatted values, and ordered collections 
        /// to eliminate template processing overhead including nested packaging hierarchy processing.
        /// </remarks>
        public List<PackageRendering>? PackageRendering { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has package rendering contexts.
        /// </summary>
        /// <seealso cref="PackageRendering"/>
        /// <seealso cref="Label"/>
        public bool HasPackageRendering { get; set; }

        #endregion

        #region characteristic rendering properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed characteristic rendering contexts for efficient template rendering.
        /// Contains CharacteristicRendering objects with all pre-computed properties instead of raw CharacteristicDto objects.
        /// This collection provides optimized characteristic data for template processing with pre-computed business logic.
        /// Null if no characteristics exist.
        /// </summary>
        /// <seealso cref="CharacteristicRendering"/>
        /// <seealso cref="OrderedCharacteristics"/>
        /// <seealso cref="Label"/>
        /// <remarks>
        /// This collection contains characteristics with pre-computed properties for optimal performance.
        /// Use this collection in preference to the raw OrderedCharacteristics for optimal template performance.
        /// Each CharacteristicRendering object contains pre-computed value type flags, rendering logic decisions,
        /// formatted values, and display flags to eliminate template processing overhead and complex conditional logic.
        /// </remarks>
        public List<CharacteristicRendering>? CharacteristicRendering { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has characteristic rendering contexts.
        /// </summary>
        /// <seealso cref="CharacteristicRendering"/>
        /// <seealso cref="Label"/>
        public bool HasCharacteristicRendering { get; set; }

        #endregion

        #region kit product part properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product is a Kit (formCode = C47916).
        /// Kit products contain parts that should be rendered using the part element structure.
        /// </summary>
        /// <seealso cref="ProductPartRendering"/>
        /// <seealso cref="Label.ProductPart"/>
        public bool IsKit { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed product part rendering contexts for Kit products.
        /// Contains ProductPartRendering objects with pre-computed properties for each part.
        /// Null if this is not a Kit product or has no parts.
        /// </summary>
        /// <seealso cref="ProductPartRendering"/>
        /// <seealso cref="Label.ProductPart"/>
        public List<ProductPartRendering>? ProductParts { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this Kit product has parts to render.
        /// </summary>
        /// <seealso cref="ProductParts"/>
        /// <seealso cref="IsKit"/>
        public bool HasProductParts { get; set; }

        #endregion

        #region legacy properties (for backward compatibility)

        /**************************************************************/
        /// <summary>
        /// Gets whether this product has ingredients to render.
        /// </summary>
        /// <returns>True if any ingredients exist</returns>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="Label"/>
        /// <remarks>
        /// This method is maintained for backward compatibility.
        /// New code should prefer using the service to pre-compute data.
        /// </remarks>
        public bool HasIngredients => ProductDto?.Ingredients?.Any() == true;

        /**************************************************************/
        /// <summary>
        /// Gets whether this product has packaging levels to render.
        /// </summary>
        /// <returns>True if packaging levels exist</returns>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="Label"/>
        /// <remarks>
        /// This method is maintained for backward compatibility.
        /// New code should prefer using the service to pre-compute data.
        /// </remarks>
        public bool HasPackagingLevels => ProductDto?.PackagingLevels?.Any() == true;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Context object for rendering product parts (Kit components) with pre-computed properties.
    /// Used for rendering the part element structure in Kit products.
    /// </summary>
    /// <seealso cref="ProductPartDto"/>
    /// <seealso cref="Label.ProductPart"/>
    public class ProductPartRendering
    {
        /**************************************************************/
        /// <summary>
        /// The product part DTO containing the part metadata.
        /// </summary>
        /// <seealso cref="ProductPartDto"/>
        public required ProductPartDto ProductPartDto { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed quantity numerator value for the part.
        /// </summary>
        public string? QuantityNumerator { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed quantity numerator unit for the part.
        /// </summary>
        public string? QuantityNumeratorUnit { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed quantity denominator value for the part (typically "1").
        /// </summary>
        public string? QuantityDenominator { get; set; }

        /**************************************************************/
        /// <summary>
        /// Flag indicating whether this part has quantity information.
        /// </summary>
        public bool HasQuantity { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed product name for the part product.
        /// </summary>
        public string? PartProductName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed form code for the part product.
        /// </summary>
        public string? PartFormCode { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed form code system for the part product.
        /// </summary>
        public string? PartFormCodeSystem { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed form display name for the part product.
        /// </summary>
        public string? PartFormDisplayName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered active ingredients for the part product.
        /// </summary>
        public List<IngredientRendering>? ActiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered inactive ingredients for the part product.
        /// </summary>
        public List<IngredientRendering>? InactiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered generic medicines for the part product.
        /// </summary>
        public List<GenericMedicineDto>? GenericMedicines { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered characteristics for the part product.
        /// </summary>
        public List<CharacteristicRendering>? Characteristics { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered routes for the part product.
        /// </summary>
        public List<RouteDto>? Routes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed marketing categories for the part product (approval info).
        /// </summary>
        public List<MarketingCategoryDto>? MarketingCategories { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed marketing statuses for the part product (marketing act).
        /// </summary>
        public List<MarketingStatusDto>? MarketingStatuses { get; set; }

        /**************************************************************/
        /// <summary>
        /// Flag indicating whether part has active ingredients.
        /// </summary>
        public bool HasActiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Flag indicating whether part has inactive ingredients.
        /// </summary>
        public bool HasInactiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Flag indicating whether part has generic medicines.
        /// </summary>
        public bool HasGenericMedicines { get; set; }

        /**************************************************************/
        /// <summary>
        /// Flag indicating whether part has characteristics.
        /// </summary>
        public bool HasCharacteristics { get; set; }

        /**************************************************************/
        /// <summary>
        /// Flag indicating whether part has routes.
        /// </summary>
        public bool HasRoutes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Flag indicating whether part has marketing categories (approval).
        /// </summary>
        public bool HasMarketingCategories { get; set; }

        /**************************************************************/
        /// <summary>
        /// Flag indicating whether part has marketing statuses (marketing act).
        /// </summary>
        public bool HasMarketingStatuses { get; set; }
    }
}