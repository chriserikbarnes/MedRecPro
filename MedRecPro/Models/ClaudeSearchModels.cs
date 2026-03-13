namespace MedRecPro.Models
{
    #region pharmacologic class search models

    /**************************************************************/
    /// <summary>
    /// Represents the result of matching user query terms to database class names.
    /// </summary>
    /// <seealso cref="MedRecPro.Service.IClaudeSearchService"/>
    public class PharmacologicClassMatchResult
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets whether the matching operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the list of matched class names from the database.
        /// These are the exact class names that can be used for product searches.
        /// </summary>
        public List<string> MatchedClassNames { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the explanation of how matches were determined.
        /// Useful for transparency and debugging.
        /// </summary>
        public string? Explanation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets any error message if matching failed.
        /// </summary>
        public string? Error { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets alternative suggestions if no exact matches were found.
        /// </summary>
        public List<string>? Suggestions { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents the complete result of a pharmacologic class search operation.
    /// Contains all matched products organized by class with label links.
    /// </summary>
    /// <seealso cref="MedRecPro.Service.IClaudeSearchService"/>
    public class PharmacologicClassSearchResult
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets whether the search operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the original user query that initiated the search.
        /// </summary>
        public string OriginalQuery { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the list of pharmacologic classes that matched the user query.
        /// </summary>
        public List<string> MatchedClasses { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the products found, organized by their pharmacologic class.
        /// Key is the class name, value is the list of products in that class.
        /// </summary>
        public Dictionary<string, List<PharmacologicClassProductInfo>> ProductsByClass { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the total number of products found across all classes.
        /// </summary>
        public int TotalProductCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets label links for all found products.
        /// Key is display name, value is the API URL.
        /// </summary>
        /// <example>
        /// {
        ///   "View Full Label (METOPROLOL TARTRATE)": "/api/Label/original/{guid}/true"
        /// }
        /// </example>
        public Dictionary<string, string> LabelLinks { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets any error message if the search failed.
        /// </summary>
        public string? Error { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the AI's explanation of the search results.
        /// </summary>
        public string? Explanation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets suggested follow-up queries.
        /// </summary>
        public List<string>? SuggestedFollowUps { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents product information for pharmacologic class search results.
    /// Contains the essential fields needed for display and label link generation.
    /// </summary>
    public class PharmacologicClassProductInfo
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the document GUID for generating label links.
        /// </summary>
        public string? DocumentGuid { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the pharmacologic class name this product belongs to.
        /// </summary>
        public string? PharmClassName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the active ingredient(s) in the product.
        /// </summary>
        public string? ActiveIngredient { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the labeler/manufacturer name.
        /// </summary>
        public string? LabelerName { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents the result of extracting a product/ingredient name from a description.
    /// Used for UNII resolution fallback when the interpret phase uses incorrect UNIIs.
    /// </summary>
    /// <seealso cref="MedRecPro.Service.IClaudeSearchService"/>
    public class ProductExtractionResult
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets whether the extraction operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the extracted product/ingredient names.
        /// The first name is the primary (most likely) extraction.
        /// Multiple names may be returned for combination products or
        /// when both brand and generic names are identified.
        /// </summary>
        public List<string> ProductNames { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the primary extracted product name (convenience property).
        /// Returns the first product name or null if none extracted.
        /// </summary>
        public string? PrimaryProductName => ProductNames.FirstOrDefault();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the confidence level of the extraction.
        /// Values: "high", "medium", "low"
        /// </summary>
        public string Confidence { get; set; } = "low";

        /**************************************************************/
        /// <summary>
        /// Gets or sets the AI's explanation of the extraction logic.
        /// Useful for debugging and transparency.
        /// </summary>
        public string? Explanation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets any error message if extraction failed.
        /// </summary>
        public string? Error { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether a brand-to-generic mapping was applied.
        /// </summary>
        public bool BrandMappingApplied { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the original brand name if a mapping was applied.
        /// </summary>
        public string? OriginalBrandName { get; set; }
    }

    #endregion

    #region indication search models

    /**************************************************************/
    /// <summary>
    /// Represents a parsed entry from the labelProductIndication.md reference file.
    /// Each entry contains one or more product names, a UNII code, and
    /// the summarized FDA indication text for that ingredient.
    /// </summary>
    /// <seealso cref="MedRecPro.Service.IClaudeSearchService"/>
    public class IndicationReferenceEntry
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets the product names (brand/generic) associated with this UNII.
        /// </summary>
        public List<string> ProductNames { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the FDA Unique Ingredient Identifier.
        /// </summary>
        public string UNII { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the combined indication text from all FDA labels for this UNII.
        /// </summary>
        public string IndicationsSummary { get; set; } = string.Empty;
    }

    /**************************************************************/
    /// <summary>
    /// Represents a single indication match from Claude AI (Stage 2).
    /// Contains the UNII, product names, and the AI's reasoning for the match.
    /// </summary>
    /// <seealso cref="IndicationMatchResult"/>
    public class IndicationMatch
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets the matched UNII code.
        /// </summary>
        public string UNII { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the product names associated with the matched UNII.
        /// </summary>
        public string ProductNames { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the AI's explanation for why this UNII matches the query.
        /// </summary>
        public string RelevanceReason { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the confidence level of the match (high, medium, low).
        /// </summary>
        public string Confidence { get; set; } = "low";
    }

    /**************************************************************/
    /// <summary>
    /// Represents the complete result of AI indication matching (Stage 2).
    /// Contains all matched indications and any explanatory context.
    /// </summary>
    /// <seealso cref="MedRecPro.Service.IClaudeSearchService"/>
    public class IndicationMatchResult
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets whether the matching operation succeeded.
        /// </summary>
        public bool Success { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the list of matched indications from the AI.
        /// </summary>
        public List<IndicationMatch> MatchedIndications { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the AI's explanation of how matches were determined.
        /// </summary>
        public string? Explanation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets any error message if matching failed.
        /// </summary>
        public string? Error { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets alternative query suggestions if no matches were found.
        /// </summary>
        public List<string>? Suggestions { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents product information for indication search results.
    /// Contains essential fields for display, label link generation, and validation context.
    /// </summary>
    /// <seealso cref="IndicationSearchResult"/>
    public class IndicationProductInfo
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the document GUID for generating label links.
        /// </summary>
        public string? DocumentGuid { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the UNII code for the active ingredient.
        /// </summary>
        public string? UNII { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the active ingredient name.
        /// </summary>
        public string? ActiveIngredient { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the labeler/manufacturer name.
        /// </summary>
        public string? LabelerName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets a truncated indication summary from the reference data.
        /// </summary>
        public string? IndicationSummary { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the validation reason from Stage 3 (why the match was confirmed).
        /// </summary>
        public string? ValidationReason { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the validation confidence from Stage 3 (high, medium, low, unverified).
        /// </summary>
        public string? ValidationConfidence { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents an entry sent to Stage 3 Claude validation.
    /// Contains the UNII, product name, and the actual FDA label indication text.
    /// </summary>
    /// <seealso cref="IndicationValidationResult"/>
    public class IndicationValidationEntry
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets the UNII code.
        /// </summary>
        public string UNII { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the document GUID for the product label.
        /// </summary>
        public string? DocumentGuid { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the actual FDA Indications &amp; Usage section text from the label.
        /// </summary>
        public string IndicationText { get; set; } = string.Empty;
    }

    /**************************************************************/
    /// <summary>
    /// Represents a single validation verdict from Stage 3 Claude validation.
    /// </summary>
    /// <seealso cref="IndicationValidationResult"/>
    public class ValidatedIndication
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets the UNII code.
        /// </summary>
        public string UNII { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether the indication match was confirmed against the actual label.
        /// </summary>
        public bool Confirmed { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the reason for the validation verdict.
        /// </summary>
        public string ValidationReason { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the confidence level of the validation (high, medium, low).
        /// </summary>
        public string Confidence { get; set; } = "low";
    }

    /**************************************************************/
    /// <summary>
    /// Represents the result of Stage 3 Claude validation.
    /// Contains verdicts for each product evaluated against actual label text.
    /// </summary>
    public class IndicationValidationResult
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets whether the validation operation succeeded.
        /// </summary>
        public bool Success { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the list of validated matches.
        /// </summary>
        public List<ValidatedIndication> ValidatedMatches { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the AI's summary of validation results.
        /// </summary>
        public string? Explanation { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents the complete result of an indication search operation.
    /// Contains all matched and validated products organized by indication
    /// with label links for source verification.
    /// </summary>
    /// <seealso cref="MedRecPro.Service.IClaudeSearchService"/>
    public class IndicationSearchResult
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets whether the search operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the original user query.
        /// </summary>
        public string OriginalQuery { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the list of matched indications from Stage 2.
        /// </summary>
        public List<IndicationMatch> MatchedIndications { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the products found, organized by UNII.
        /// Key is the UNII code, value is the list of products for that ingredient.
        /// </summary>
        public Dictionary<string, List<IndicationProductInfo>> ProductsByIndication { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the total number of products found across all indications.
        /// </summary>
        public int TotalProductCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets label links for all found products.
        /// Key is display name, value is the API URL.
        /// </summary>
        public Dictionary<string, string> LabelLinks { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets any error message if the search failed.
        /// </summary>
        public string? Error { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the AI's explanation of the search results.
        /// </summary>
        public string? Explanation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets suggested follow-up queries.
        /// </summary>
        public List<string>? SuggestedFollowUps { get; set; }
    }

    #endregion
}
