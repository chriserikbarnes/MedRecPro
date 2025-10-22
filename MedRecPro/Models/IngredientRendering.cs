namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Context object for rendering ingredients with pre-computed properties.
    /// Provides ingredient data along with pre-computed rendering properties
    /// for efficient template rendering.
    /// </summary>
    /// <seealso cref="Label.Ingredient"/>
    /// <seealso cref="IngredientDto"/>
    public class IngredientRendering
    {
        #region core properties

        /**************************************************************/
        /// <summary>
        /// The ingredient to be rendered.
        /// </summary>
        /// <seealso cref="Label.Ingredient"/>
        /// <seealso cref="IngredientDto"/>
        public required IngredientDto IngredientDto { get; set; }

        #endregion

        #region pre-computed rendering properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this ingredient is an active ingredient.
        /// Generated from OriginatingElement with proper case-insensitive comparison.
        /// </summary>
        /// <seealso cref="Label.Ingredient"/>
        public bool IsActiveIngredient { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this ingredient has quantity data.
        /// </summary>
        /// <seealso cref="Label.Ingredient"/>
        public bool HasQuantity { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this ingredient has substance data.
        /// </summary>
        /// <seealso cref="Label.Ingredient"/>
        public bool HasSubstance { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed class code attribute for HTML rendering.
        /// Generated from ClassCode with proper formatting for inactive ingredients.
        /// </summary>
        /// <seealso cref="Label.Ingredient"/>
        public string ClassCodeAttribute { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered specified substances for efficient rendering.
        /// Null if no specified substances exist.
        /// </summary>
        /// <seealso cref="Label.SpecifiedSubstance"/>
        public List<SpecifiedSubstanceDto>? OrderedSpecifiedSubstances { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed and ordered active moieties for efficient rendering.
        /// Null if no active moieties exist.
        /// </summary>
        /// <seealso cref="Label.ActiveMoiety"/>
        public List<ActiveMoietyDto>? OrderedActiveMoieties { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed formatted substance name with unwanted characters removed.
        /// </summary>
        /// <seealso cref="Label.IngredientSubstance"/>
        public string FormattedSubstanceName { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether numerator has translation data.
        /// </summary>
        /// <seealso cref="Label.Ingredient"/>
        public bool HasNumeratorTranslation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether denominator has translation data.
        /// </summary>
        /// <seealso cref="Label.Ingredient"/>
        public bool HasDenominatorTranslation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this ingredient has specified substances to render.
        /// </summary>
        /// <seealso cref="Label.SpecifiedSubstance"/>
        public bool HasSpecifiedSubstances { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this ingredient has active moieties to render.
        /// </summary>
        /// <seealso cref="Label.ActiveMoiety"/>
        public bool HasActiveMoieties { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed formatted quantity numerator value for rendering.
        /// </summary>
        /// <seealso cref="Label.Ingredient"/>
        public string? FormattedQuantityNumerator { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed formatted quantity denominator value for rendering.
        /// </summary>
        /// <seealso cref="Label.Ingredient"/>
        public string? FormattedQuantityDenominator { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed flag indicating whether this ingredient requires reference substance rendering.
        /// True when ClassCode is ACTIR and reference substances exist.
        /// </summary>
        /// <seealso cref="Label.Ingredient"/>
        /// <seealso cref="Label.ReferenceSubstance"/>
        public bool RequiresReferenceSubstance { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed primary reference substance for ACTIR ingredients.
        /// Null if no reference substance exists or ClassCode is not ACTIR.
        /// </summary>
        /// <seealso cref="Label.ReferenceSubstance"/>
        public ReferenceSubstanceDto? PrimaryReferenceSubstance { get; set; }

        #endregion

        #region legacy properties (for backward compatibility)

        /**************************************************************/
        /// <summary>
        /// Gets whether this ingredient has class code data to render.
        /// </summary>
        /// <returns>True if class code attribute is not empty</returns>
        /// <seealso cref="ClassCodeAttribute"/>
        /// <remarks>
        /// This method is maintained for backward compatibility.
        /// New code should prefer using the service to pre-compute data.
        /// </remarks>
        public bool HasClassCode => !string.IsNullOrEmpty(ClassCodeAttribute);

        /**************************************************************/
        /// <summary>
        /// Gets whether this ingredient has UNII code available from substance.
        /// </summary>
        /// <returns>True if substance has UNII code</returns>
        /// <seealso cref="Label.IngredientSubstance"/>
        /// <remarks>
        /// This method is maintained for backward compatibility.
        /// New code should prefer using the service to pre-compute data.
        /// </remarks>
        public bool HasUniiCode => HasSubstance && !string.IsNullOrEmpty(IngredientDto?.IngredientSubstance?.UNII);

        /**************************************************************/
        /// <summary>
        /// Gets the UNII code from the ingredient substance.
        /// </summary>
        /// <returns>UNII code or empty string if not available</returns>
        /// <seealso cref="Label.IngredientSubstance"/>
        /// <remarks>
        /// This method is maintained for backward compatibility.
        /// New code should prefer using the service to pre-compute data.
        /// </remarks>
        public string UniiCode => IngredientDto?.IngredientSubstance?.UNII ?? string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets the FDA SRS code system for UNII codes.
        /// </summary>
        /// <returns>FDA SRS code system constant</returns>
        /// <remarks>
        /// This method is maintained for backward compatibility.
        /// New code should prefer using constants from the service.
        /// </remarks>
        public string FdaSrsCodeSystem => Constant.FDA_UNII_CODE_SYSTEM;

        /**************************************************************/
        /// <summary>
        /// Gets the FDA SRS code system name for UNII codes.
        /// </summary>
        /// <returns>FDA SRS code system name constant</returns>
        /// <remarks>
        /// This method is maintained for backward compatibility.
        /// New code should prefer using constants from the service.
        /// </remarks>
        public string FdaSrsCodeSystemName => "FDA SRS";

        #endregion
    }
}