using MedRecPro.Models;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Interface for preparing ingredient data for rendering by handling formatting,
    /// ordering, and attribute generation logic.
    /// </summary>
    /// <seealso cref="Label.Ingredient"/>
    /// <seealso cref="IngredientDto"/>
    /// <seealso cref="IngredientRendering"/>
    public interface IIngredientRenderingService
    {
        #region core methods

        /**************************************************************/
        /// <summary>
        /// Prepares a complete IngredientRendering object with all computed properties
        /// for efficient template rendering.
        /// </summary>
        /// <param name="ingredient">The ingredient to prepare for rendering</param>
        /// <param name="additionalParams">Additional context parameters as needed</param>
        /// <returns>A fully prepared IngredientRendering object</returns>
        /// <seealso cref="IngredientRendering"/>
        /// <seealso cref="Label.Ingredient"/>
        IngredientRendering PrepareForRendering(IngredientDto? ingredient, object? additionalParams = null);

        /**************************************************************/
        /// <summary>
        /// Generates appropriate class code attribute for inactive ingredients.
        /// </summary>
        /// <param name="ingredient">The ingredient to generate class code attribute for</param>
        /// <returns>Formatted class code attribute string</returns>
        /// <seealso cref="Label.Ingredient"/>
        string GenerateClassCodeAttribute(IngredientDto? ingredient);

        /**************************************************************/
        /// <summary>
        /// Determines if ingredient is an active ingredient based on originating element.
        /// </summary>
        /// <param name="ingredient">The ingredient to check</param>
        /// <returns>True if ingredient is an active ingredient</returns>
        /// <seealso cref="Label.Ingredient"/>
        bool IsActiveIngredient(IngredientDto? ingredient);

        /**************************************************************/
        /// <summary>
        /// Determines if ingredient has valid quantity data for rendering.
        /// </summary>
        /// <param name="ingredient">The ingredient to validate</param>
        /// <returns>True if ingredient has quantity data</returns>
        /// <seealso cref="Label.Ingredient"/>
        bool HasQuantityData(IngredientDto? ingredient);

        /**************************************************************/
        /// <summary>
        /// Determines if ingredient has valid substance data for rendering.
        /// </summary>
        /// <param name="ingredient">The ingredient to validate</param>
        /// <returns>True if ingredient has substance data</returns>
        /// <seealso cref="Label.Ingredient"/>
        bool HasSubstanceData(IngredientDto? ingredient);

        /**************************************************************/
        /// <summary>
        /// Gets specified substances ordered by business rules.
        /// </summary>
        /// <param name="ingredient">The ingredient containing specified substances</param>
        /// <returns>Ordered list of specified substances or null if none exists</returns>
        /// <seealso cref="Label.SpecifiedSubstance"/>
        List<SpecifiedSubstanceDto>? GetOrderedSpecifiedSubstances(IngredientDto? ingredient);

        /**************************************************************/
        /// <summary>
        /// Gets active moieties ordered by business rules.
        /// </summary>
        /// <param name="ingredient">The ingredient containing active moieties</param>
        /// <returns>Ordered list of active moieties or null if none exists</returns>
        /// <seealso cref="Label.ActiveMoiety"/>
        List<ActiveMoietyDto>? GetOrderedActiveMoieties(IngredientDto? ingredient);

        /**************************************************************/
        /// <summary>
        /// Formats substance name by removing unwanted characters and entities.
        /// </summary>
        /// <param name="substanceName">The substance name to format</param>
        /// <returns>Formatted substance name</returns>
        string FormatSubstanceName(string substanceName);

        /**************************************************************/
        /// <summary>
        /// Determines if numerator has translation data.
        /// </summary>
        /// <param name="ingredient">The ingredient to check</param>
        /// <returns>True if numerator has translation data</returns>
        /// <seealso cref="Label.Ingredient"/>
        bool HasNumeratorTranslation(IngredientDto? ingredient);

        /**************************************************************/
        /// <summary>
        /// Determines if denominator has translation data.
        /// </summary>
        /// <param name="ingredient">The ingredient to check</param>
        /// <returns>True if denominator has translation data</returns>
        /// <seealso cref="Label.Ingredient"/>
        bool HasDenominatorTranslation(IngredientDto? ingredient);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Service for preparing ingredient data for rendering by handling formatting,
    /// ordering, and attribute generation logic. Separates view logic from templates.
    /// </summary>
    /// <seealso cref="IIngredientRenderingService"/>
    /// <seealso cref="Label.Ingredient"/>
    /// <seealso cref="IngredientDto"/>
    /// <remarks>
    /// This service encapsulates all business logic that was previously
    /// embedded in Razor views, promoting better separation of concerns
    /// and testability.
    /// </remarks>
    public class IngredientRenderingService : IIngredientRenderingService
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// Default code system values for ingredient substances.
        /// </summary>
        private const string FDA_SRS_CODE_SYSTEM = "2.16.840.1.113883.4.9";
        private const string FDA_SRS_CODE_SYSTEM_NAME = "FDA SRS";
        private const string ACTIVE_INGREDIENT_ELEMENT = "activeIngredient";

        /**************************************************************/
        /// <summary>
        /// Numeric formatting string for quantity values.
        /// </summary>
        private const string QUANTITY_FORMAT = "G29";

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Prepares a complete IngredientRendering object with all computed properties
        /// for efficient template rendering. Pre-computes all formatting and ordering
        /// operations to minimize processing in the view layer.
        /// </summary>
        /// <param name="ingredient">The ingredient to prepare for rendering</param>
        /// <param name="additionalParams">Additional context parameters as needed</param>
        /// <returns>A fully prepared IngredientRendering object with computed properties</returns>
        /// <seealso cref="IngredientRendering"/>
        /// <seealso cref="Label.Ingredient"/>
        /// <example>
        /// <code>
        /// var preparedIngredient = service.PrepareForRendering(
        ///     ingredient: ingredientDto,
        ///     additionalParams: contextData
        /// );
        /// // preparedIngredient now has all computed properties ready for rendering
        /// </code>
        /// </example>
        public IngredientRendering PrepareForRendering(IngredientDto? ingredient, object? additionalParams = null)
        {
            #region implementation

            if (ingredient == null)
                throw new ArgumentNullException(nameof(ingredient));

            return new IngredientRendering
            {
                IngredientDto = ingredient,

                // Pre-compute all rendering properties
                IsActiveIngredient = IsActiveIngredient(ingredient),
                HasQuantity = HasQuantityData(ingredient),
                HasSubstance = HasSubstanceData(ingredient),
                ClassCodeAttribute = GenerateClassCodeAttribute(ingredient),
                OrderedSpecifiedSubstances = GetOrderedSpecifiedSubstances(ingredient),
                OrderedActiveMoieties = GetOrderedActiveMoieties(ingredient),
                FormattedSubstanceName = FormatSubstanceName(ingredient?.IngredientSubstance?.SubstanceName),

                // Pre-compute translation flags
                HasNumeratorTranslation = HasNumeratorTranslation(ingredient),
                HasDenominatorTranslation = HasDenominatorTranslation(ingredient),

                // Pre-compute availability flags
                HasSpecifiedSubstances = GetOrderedSpecifiedSubstances(ingredient)?.Any() == true,
                HasActiveMoieties = GetOrderedActiveMoieties(ingredient)?.Any() == true,

                // Pre-compute formatted quantity values
                FormattedQuantityNumerator = ingredient?.QuantityNumerator?.ToString(QUANTITY_FORMAT),
                FormattedQuantityDenominator = ingredient?.QuantityDenominator?.ToString(QUANTITY_FORMAT)
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates appropriate class code attribute for inactive ingredients based on
        /// business rules and formatting requirements.
        /// </summary>
        /// <param name="ingredient">The ingredient to generate class code attribute for</param>
        /// <returns>Formatted class code attribute string with appropriate fallbacks</returns>
        /// <seealso cref="Label.Ingredient"/>
        /// <example>
        /// <code>
        /// var classCode = service.GenerateClassCodeAttribute(ingredient);
        /// // Returns: "classCode=\"ACTIB\"" or similar formatted attribute
        /// </code>
        /// </example>
        public string GenerateClassCodeAttribute(IngredientDto? ingredient)
        {
            #region implementation

            if (ingredient == null || string.IsNullOrEmpty(ingredient.ClassCode))
                return string.Empty;

            return $"classCode=\"{ingredient.ClassCode}\"";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if ingredient is an active ingredient based on originating element.
        /// </summary>
        /// <param name="ingredient">The ingredient to check</param>
        /// <returns>True if ingredient is an active ingredient</returns>
        /// <seealso cref="Label.Ingredient"/>
        /// <example>
        /// <code>
        /// bool isActive = service.IsActiveIngredient(ingredient);
        /// if (isActive)
        /// {
        ///     // Render active ingredient markup
        /// }
        /// </code>
        /// </example>
        public bool IsActiveIngredient(IngredientDto? ingredient)
        {
            #region implementation

            return ingredient?.OriginatingElement?.Equals(ACTIVE_INGREDIENT_ELEMENT, StringComparison.OrdinalIgnoreCase) == true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if ingredient has valid quantity data for rendering.
        /// </summary>
        /// <param name="ingredient">The ingredient to validate</param>
        /// <returns>True if ingredient has quantity data</returns>
        /// <seealso cref="Label.Ingredient"/>
        /// <example>
        /// <code>
        /// bool hasQuantity = service.HasQuantityData(ingredient);
        /// if (hasQuantity)
        /// {
        ///     // Render quantity elements
        /// }
        /// </code>
        /// </example>
        public bool HasQuantityData(IngredientDto? ingredient)
        {
            #region implementation

            return ingredient != null &&
                   (ingredient.QuantityNumerator.HasValue || ingredient.QuantityDenominator.HasValue);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if ingredient has valid substance data for rendering.
        /// </summary>
        /// <param name="ingredient">The ingredient to validate</param>
        /// <returns>True if ingredient has substance data</returns>
        /// <seealso cref="Label.Ingredient"/>
        /// <example>
        /// <code>
        /// bool hasSubstance = service.HasSubstanceData(ingredient);
        /// if (hasSubstance)
        /// {
        ///     // Render substance elements
        /// }
        /// </code>
        /// </example>
        public bool HasSubstanceData(IngredientDto? ingredient)
        {
            #region implementation

            return ingredient?.IngredientSubstance != null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets specified substances ordered by business rules for consistent display.
        /// Returns null if no specified substances exist.
        /// </summary>
        /// <param name="ingredient">The ingredient containing specified substances</param>
        /// <returns>Ordered list of specified substances or null if none exists</returns>
        /// <seealso cref="Label.SpecifiedSubstance"/>
        /// <example>
        /// <code>
        /// var substances = service.GetOrderedSpecifiedSubstances(ingredient);
        /// if (substances != null)
        /// {
        ///     foreach (var substance in substances)
        ///     {
        ///         // Process ordered substances
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Specified substances are ordered by SpecifiedSubstanceID, with null values treated as 0.
        /// This ensures consistent display order across different rendering contexts.
        /// </remarks>
        public List<SpecifiedSubstanceDto>? GetOrderedSpecifiedSubstances(IngredientDto? ingredient)
        {
            #region implementation

            return ingredient?.SpecifiedSubstances?.Any() == true
                ? ingredient.SpecifiedSubstances.OrderBy(s => s.SpecifiedSubstanceID ?? 0).ToList()
                : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets active moieties ordered by business rules for consistent display.
        /// Returns null if no active moieties exist.
        /// </summary>
        /// <param name="ingredient">The ingredient containing active moieties</param>
        /// <returns>Ordered list of active moieties or null if none exists</returns>
        /// <seealso cref="Label.ActiveMoiety"/>
        /// <example>
        /// <code>
        /// var moieties = service.GetOrderedActiveMoieties(ingredient);
        /// if (moieties != null)
        /// {
        ///     foreach (var moiety in moieties)
        ///     {
        ///         // Process ordered moieties
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Active moieties are ordered by IngredientSubstanceID, with null values treated as 0.
        /// Returns List for efficient iteration in templates.
        /// </remarks>
        public List<ActiveMoietyDto>? GetOrderedActiveMoieties(IngredientDto? ingredient)
        {
            #region implementation

            return ingredient?.IngredientSubstance?.ActiveMoieties?.Any() == true
                ? ingredient.IngredientSubstance.ActiveMoieties.OrderBy(m => m.IngredientSubstanceID ?? 0).ToList()
                : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats substance name by removing unwanted characters and entities.
        /// Handles HTML entity replacement and other formatting rules.
        /// </summary>
        /// <param name="substanceName">The substance name to format</param>
        /// <returns>Formatted substance name with unwanted characters removed</returns>
        /// <example>
        /// <code>
        /// var formatted = service.FormatSubstanceName("Example &amp; Substance");
        /// // Returns: "Example & Substance"
        /// </code>
        /// </example>
        /// <remarks>
        /// Currently removes "amp;" entities. Additional formatting rules can be added here.
        /// </remarks>
        public string FormatSubstanceName(string? substanceName)
        {
            #region implementation

            if (string.IsNullOrEmpty(substanceName))
                return string.Empty;

            return substanceName.Replace("amp;", string.Empty);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if numerator has translation data for rendering.
        /// </summary>
        /// <param name="ingredient">The ingredient to check</param>
        /// <returns>True if numerator has translation data</returns>
        /// <seealso cref="Label.Ingredient"/>
        /// <example>
        /// <code>
        /// bool hasTranslation = service.HasNumeratorTranslation(ingredient);
        /// if (hasTranslation)
        /// {
        ///     // Render numerator translation element
        /// }
        /// </code>
        /// </example>
        public bool HasNumeratorTranslation(IngredientDto? ingredient)
        {
            #region implementation

            return !string.IsNullOrEmpty(ingredient?.NumeratorTranslationCode);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if denominator has translation data for rendering.
        /// </summary>
        /// <param name="ingredient">The ingredient to check</param>
        /// <returns>True if denominator has translation data</returns>
        /// <seealso cref="Label.Ingredient"/>
        /// <example>
        /// <code>
        /// bool hasTranslation = service.HasDenominatorTranslation(ingredient);
        /// if (hasTranslation)
        /// {
        ///     // Render denominator translation element
        /// }
        /// </code>
        /// </example>
        public bool HasDenominatorTranslation(IngredientDto? ingredient)
        {
            #region implementation

            return !string.IsNullOrEmpty(ingredient?.DenominatorTranslationCode);

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Gets the FDA SRS code system for UNII codes.
        /// </summary>
        /// <returns>FDA SRS code system constant</returns>
        private static string GetFdaSrsCodeSystem()
        {
            #region implementation

            return FDA_SRS_CODE_SYSTEM;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the FDA SRS code system name for UNII codes.
        /// </summary>
        /// <returns>FDA SRS code system name constant</returns>
        private static string GetFdaSrsCodeSystemName()
        {
            #region implementation

            return FDA_SRS_CODE_SYSTEM_NAME;

            #endregion
        }

        #endregion
    }
}