using static MedRecPro.Models.Label;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Context object for rendering products with pre-computed properties.
    /// Provides product data along with pre-computed rendering properties
    /// for efficient template rendering.
    /// </summary>
    /// <seealso cref="ProductDto"/>
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
        public ProductIdentifierDto? NdcProductIdentifier { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has valid data.
        /// </summary>
        /// <seealso cref="ProductDto"/>
        public bool HasValidData { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered active ingredients for efficient rendering.
        /// Null if no active ingredients exist.
        /// </summary>
        /// <seealso cref="IngredientDto"/>
        public List<IngredientDto>? OrderedActiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered inactive ingredients for efficient rendering.
        /// Null if no inactive ingredients exist.
        /// </summary>
        /// <seealso cref="IngredientDto"/>
        public List<IngredientDto>? OrderedInactiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered characteristics for efficient rendering.
        /// Null if no characteristics exist.
        /// </summary>
        /// <seealso cref="CharacteristicDto"/>
        public List<CharacteristicDto>? OrderedCharacteristics { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered top-level packaging for efficient rendering.
        /// Null if no top-level packaging exists.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        public List<PackagingLevelDto>? OrderedTopLevelPackaging { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered routes for efficient rendering.
        /// Null if no routes exist.
        /// </summary>
        /// <seealso cref="RouteDto"/>
        public List<RouteDto>? OrderedRoutes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has NDC identifier.
        /// </summary>
        /// <seealso cref="NdcProductIdentifier"/>
        public bool HasNdcIdentifier { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has active ingredients to render.
        /// </summary>
        /// <seealso cref="IngredientDto"/>
        public bool HasActiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has inactive ingredients to render.
        /// </summary>
        /// <seealso cref="IngredientDto"/>
        public bool HasInactiveIngredients { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has characteristics to render.
        /// </summary>
        /// <seealso cref="CharacteristicDto"/>
        public bool HasCharacteristics { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has top-level packaging to render.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        public bool HasTopLevelPackaging { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has routes to render.
        /// </summary>
        /// <seealso cref="RouteDto"/>
        public bool HasRoutes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this product has generic medicines to render.
        /// </summary>
        /// <seealso cref="GenericMedicineDto"/>
        public bool HasGenericMedicines { get; set; }

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
        /// <remarks>
        /// This filtered collection contains only inactive ingredients with optimized properties.
        /// Provides direct access to inactive ingredients without requiring filtering in templates.
        /// Each object has pre-computed IsActiveIngredient=false and associated inactive ingredient properties.
        /// </remarks>
        public List<IngredientRendering>? InactiveIngredients { get; set; }

        #endregion

        #region packaging rendering properties (NEW)

        /**************************************************************/
        /// <summary>
        /// Pre-computed packaging rendering contexts for efficient template rendering.
        /// Contains PackageRendering objects with all pre-computed properties instead of raw PackagingLevelDto objects.
        /// This collection provides optimized packaging data for template processing with pre-computed business logic.
        /// Null if no packaging exists.
        /// </summary>
        /// <seealso cref="PackageRendering"/>
        /// <seealso cref="OrderedTopLevelPackaging"/>
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
        public bool HasPackageRendering { get; set; }

        #endregion

        #region legacy properties (for backward compatibility)

        /**************************************************************/
        /// <summary>
        /// Gets whether this product has ingredients to render.
        /// </summary>
        /// <returns>True if any ingredients exist</returns>
        /// <seealso cref="IngredientDto"/>
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
        /// <remarks>
        /// This method is maintained for backward compatibility.
        /// New code should prefer using the service to pre-compute data.
        /// </remarks>
        public bool HasPackagingLevels => ProductDto?.PackagingLevels?.Any() == true;

        #endregion
    }
}