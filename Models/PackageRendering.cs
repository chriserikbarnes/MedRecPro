using static MedRecPro.Models.Label;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Context object for rendering packaging levels with pre-computed properties.
    /// Provides packaging level data along with pre-computed rendering properties
    /// for efficient template rendering.
    /// </summary>
    /// <seealso cref="PackagingLevelDto"/>
    /// <seealso cref="PackageIdentifierDto"/>
    /// <seealso cref="PackagingHierarchyDto"/>
    public class PackageRendering
    {
        #region core properties

        /**************************************************************/
        /// <summary>
        /// The packaging level to be rendered.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        public required PackagingLevelDto PackagingLevelDto { get; set; }

        #endregion

        #region pre-computed rendering properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed display attributes for HTML rendering.
        /// Generated from packaging level properties with proper formatting.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        public string DisplayAttributes { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this packaging level has valid data.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        public bool HasValidData { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered package identifiers for efficient rendering.
        /// Null if no identifiers exist.
        /// </summary>
        /// <seealso cref="PackageIdentifierDto"/>
        public List<PackageIdentifierDto>? OrderedPackageIdentifiers { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered child packaging for efficient rendering.
        /// Null if no child packaging exists.
        /// </summary>
        /// <seealso cref="PackagingHierarchyDto"/>
        public List<PackagingHierarchyDto>? OrderedChildPackaging { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this packaging has identifiers to render.
        /// </summary>
        /// <seealso cref="OrderedPackageIdentifiers"/>
        public bool HasPackageIdentifiers { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this packaging has marketing statuses.
        /// </summary>
        /// <seealso cref="MarketingStatusDto"/>
        public bool HasMarketing { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this packaging has child packaging to render.
        /// </summary>
        /// <seealso cref="OrderedChildPackaging"/>
        public bool HasChildPackaging { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed translation code for the quantity denominator.
        /// Used to provide proper code system references in quantity translation elements.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="IngredientDto.DenominatorTranslationCode"/>
        public string? DenominatorTranslationCode { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed code system for the quantity denominator translation.
        /// Typically references the FDA code system for pharmaceutical units.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="IngredientDto.DenominatorCodeSystem"/>
        public string? DenominatorCodeSystem { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed display name for the quantity denominator translation.
        /// Provides human-readable unit description for pharmaceutical forms.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="IngredientDto.DenominatorDisplayName"/>
        public string? DenominatorDisplayName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed translation code for the quantity numerator.
        /// Used to provide proper code system references in numerator translation elements.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="IngredientDto.DenominatorTranslationCode"/>
        public string? NumeratorTranslationCode { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed code system for the quantity numerator translation.
        /// Typically references the FDA code system for pharmaceutical units.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="IngredientDto.DenominatorCodeSystem"/>
        public string? NumeratorCodeSystem { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed display name for the quantity numerator translation.
        /// Provides human-readable unit description for pharmaceutical forms.
        /// </summary>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="IngredientDto.DenominatorDisplayName"/>
        public string? NumeratorDisplayName { get; set; }

        #endregion

        #region formatted quantity properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed formatted quantity numerator using G29 format.
        /// Empty string if no numerator value exists.
        /// </summary>
        /// <seealso cref="PackagingLevelDto.QuantityNumerator"/>
        public string FormattedQuantityNumerator { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed formatted quantity denominator using G29 format.
        /// Empty string if no denominator value exists.
        /// </summary>
        /// <seealso cref="PackagingLevelDto.QuantityDenominator"/>
        public string FormattedQuantityDenominator { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed unit code for quantity numerator.
        /// </summary>
        /// <seealso cref="PackagingLevelDto.QuantityNumeratorUnit"/>
        public string? UnitCode { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed unit code system (typically UCUM).
        /// </summary>
        public string? UnitCodeSystem { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed unit display name.
        /// </summary>
        public string? NumeratorUnit { get; set; }

        #endregion

        #region package form properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed package form code.
        /// </summary>
        /// <seealso cref="PackagingLevelDto.PackageFormCode"/>
        public string? PackageFormCode { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed package form code system.
        /// </summary>
        /// <seealso cref="PackagingLevelDto.PackageFormCodeSystem"/>
        public string? PackageFormCodeSystem { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed package form display name.
        /// </summary>
        /// <seealso cref="PackagingLevelDto.PackageFormDisplayName"/>
        public string? PackageFormDisplayName { get; set; }

        #endregion



        #region convenience properties

        /**************************************************************/
        /// <summary>
        /// Gets whether this packaging has quantity information to render.
        /// </summary>
        /// <returns>True if quantity numerator or denominator exist</returns>
        public bool HasQuantity => PackagingLevelDto?.QuantityNumerator.HasValue == true ||
                                  PackagingLevelDto?.QuantityDenominator.HasValue == true;

        /**************************************************************/
        /// <summary>
        /// Gets whether this packaging has form code information to render.
        /// </summary>
        /// <returns>True if package form code exists</returns>
        public bool HasFormCode => !string.IsNullOrEmpty(PackageFormCode);

        /**************************************************************/
        /// <summary>
        /// Gets whether this packaging has unit code information for translation elements.
        /// </summary>
        /// <returns>True if unit code exists</returns>
        public bool HasUnitCode => !string.IsNullOrEmpty(UnitCode);

        /**************************************************************/
        /// <summary>
        /// Gets whether this packaging has numerator translation information to render.
        /// </summary>
        /// <returns>True if numerator translation code exists</returns>
        public bool HasNumeratorTranslation => !string.IsNullOrEmpty(NumeratorTranslationCode);

        #endregion

        #region nested packaging rendering properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed child package rendering contexts for efficient template rendering.
        /// Contains PackageRendering objects with all pre-computed properties instead of raw PackagingLevelDto objects.
        /// This collection provides optimized packaging data for template processing with pre-computed business logic.
        /// Null if no child packaging exists.
        /// </summary>
        /// <seealso cref="PackageRendering"/>
        /// <seealso cref="OrderedChildPackaging"/>
        /// <remarks>
        /// This collection contains child packaging with pre-computed properties for recursive rendering.
        /// Use this collection in preference to the raw OrderedChildPackaging for optimal template performance.
        /// Each PackageRendering object contains pre-computed flags, formatted values, and ordered collections 
        /// to eliminate template processing overhead in nested packaging structures.
        /// </remarks>
        public List<PackageRendering>? ChildPackageRendering { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this packaging has child package rendering contexts.
        /// </summary>
        /// <seealso cref="ChildPackageRendering"/>
        public bool HasChildPackageRendering { get; set; }

        #endregion

        #region legacy properties (for backward compatibility)

        /**************************************************************/
        /// <summary>
        /// Gets whether this packaging has packaging hierarchy to render.
        /// </summary>
        /// <returns>True if packaging hierarchy exists</returns>
        /// <seealso cref="PackagingHierarchyDto"/>
        /// <remarks>
        /// This method is maintained for backward compatibility.
        /// New code should prefer using the service to pre-compute data.
        /// </remarks>
        public bool HasPackagingHierarchy => PackagingLevelDto?.PackagingHierarchy?.Any() == true;

        /**************************************************************/
        /// <summary>
        /// Gets whether this packaging has identifiers to render.
        /// </summary>
        /// <returns>True if package identifiers exist</returns>
        /// <seealso cref="PackageIdentifierDto"/>
        /// <remarks>
        /// This method is maintained for backward compatibility.
        /// New code should prefer using the service to pre-compute data.
        /// </remarks>
        public bool HasIdentifiers => PackagingLevelDto?.PackageIdentifiers?.Any() == true;

        #endregion

       
    }
}