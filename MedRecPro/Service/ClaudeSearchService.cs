using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MedRecPro.Service
{
    #region pharmacologic class search service interface

    /**************************************************************/
    /// <summary>
    /// Defines the contract for intelligent pharmacologic class search operations.
    /// This service enables context-aware matching between user queries and actual
    /// pharmacologic class names in the database, solving the mismatch problem where
    /// user terminology differs from database classification names.
    /// </summary>
    /// <remarks>
    /// The service implements a multi-step workflow:
    /// <list type="number">
    /// <item>Retrieve all pharmacologic class summaries from the database</item>
    /// <item>Use AI to match user query terms to actual class display names</item>
    /// <item>Iteratively search for products across all matched classes</item>
    /// <item>Produce a consolidated summary with label links to latest products</item>
    /// </list>
    ///
    /// This solves the problem where users ask "what medications are beta blockers" but
    /// the database stores the class as "Beta-Adrenergic Blockers [EPC]" or similar.
    /// </remarks>
    /// <example>
    /// <code>
    /// // User asks: "what medications are beta blockers"
    /// var result = await pharmacologicClassSearchService.SearchByUserQueryAsync(
    ///     "beta blockers",
    ///     systemContext);
    ///
    /// // Returns products from classes matching "beta blocker" terminology
    /// </code>
    /// </example>
    /// <seealso cref="IClaudeApiService"/>
    /// <seealso cref="DtoLabelAccess.GetPharmacologicClassSummariesAsync"/>
    /// <seealso cref="DtoLabelAccess.SearchByPharmacologicClassAsync"/>
    public interface IClaudeSearchService
    {
        #region search methods

        /**************************************************************/
        /// <summary>
        /// Retrieves all pharmacologic class summaries that have linked products.
        /// This provides the AI with the full vocabulary of available class names
        /// for matching against user queries.
        /// </summary>
        /// <returns>
        /// A task that resolves to a list of <see cref="PharmacologicClassSummaryDto"/>
        /// containing class names and product counts, filtered to exclude classes
        /// with no associated products.
        /// </returns>
        /// <remarks>
        /// Results are cached to optimize repeated queries. Only classes with
        /// <c>LinkedSubstanceCount > 0</c> are included since classes without
        /// products cannot provide useful search results.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetPharmacologicClassSummariesAsync"/>
        Task<List<PharmacologicClassSummaryDto>> GetAllClassSummariesAsync();

        /**************************************************************/
        /// <summary>
        /// Uses AI to match a user's query terms to actual pharmacologic class names
        /// in the database. Returns the list of class names that best match the
        /// user's intent.
        /// </summary>
        /// <param name="userQuery">
        /// The user's natural language query containing pharmacologic class terminology.
        /// Examples: "beta blockers", "ACE inhibitors", "SSRIs", "statins"
        /// </param>
        /// <param name="availableClasses">
        /// List of class summaries from the database to match against.
        /// </param>
        /// <returns>
        /// A task that resolves to a <see cref="PharmacologicClassMatchResult"/>
        /// containing the matched class names and any explanatory notes.
        /// </returns>
        /// <remarks>
        /// The AI interprets user terminology and maps it to actual database
        /// class names. For example:
        /// <list type="bullet">
        /// <item>"beta blockers" matches "Beta-Adrenergic Blockers [EPC]"</item>
        /// <item>"blood pressure meds" matches multiple antihypertensive classes</item>
        /// <item>"SSRIs" matches "Selective Serotonin Reuptake Inhibitors [EPC]"</item>
        /// </list>
        /// </remarks>
        /// <seealso cref="IClaudeApiService"/>
        Task<PharmacologicClassMatchResult> MatchUserQueryToClassesAsync(
            string userQuery,
            List<PharmacologicClassSummaryDto> availableClasses);

        /**************************************************************/
        /// <summary>
        /// Performs a complete search workflow: matches user query to classes,
        /// searches for products in matched classes, and returns consolidated results
        /// with label links.
        /// </summary>
        /// <param name="userQuery">
        /// The user's natural language query about pharmacologic classes.
        /// </param>
        /// <param name="systemContext">
        /// Optional system context for authentication state and capabilities.
        /// </param>
        /// <param name="maxProductsPerClass">
        /// Maximum number of products to return per matched class. Default is 25.
        /// </param>
        /// <returns>
        /// A task that resolves to a <see cref="PharmacologicClassSearchResult"/>
        /// containing all matched products organized by class with label links.
        /// </returns>
        /// <remarks>
        /// This is the primary method for answering user queries like
        /// "what medications are beta blockers". It orchestrates the full workflow:
        /// <list type="number">
        /// <item>Get all available classes from database</item>
        /// <item>Use AI to match user terms to class names</item>
        /// <item>Search each matched class for products</item>
        /// <item>Consolidate results with document GUIDs for label links</item>
        /// </list>
        /// </remarks>
        /// <seealso cref="GetAllClassSummariesAsync"/>
        /// <seealso cref="MatchUserQueryToClassesAsync"/>
        Task<PharmacologicClassSearchResult> SearchByUserQueryAsync(
            string userQuery,
            AiSystemContext? systemContext = null,
            int maxProductsPerClass = 25);

        /**************************************************************/
        /// <summary>
        /// Uses AI to extract a drug/ingredient name from a natural language description.
        /// This is used for UNII resolution fallback when the interpret phase provides
        /// an incorrect UNII but includes the product name in the description.
        /// </summary>
        /// <param name="description">
        /// The endpoint description containing the product/ingredient name.
        /// Examples:
        /// - "Search for sevelamer - phosphate binder for CKD"
        /// - "Search for finerenone (Kerendia) - non-steroidal MRA"
        /// - "Get metformin products for type 2 diabetes"
        /// </param>
        /// <returns>
        /// A task that resolves to a <see cref="ProductExtractionResult"/>
        /// containing the extracted product name(s) and confidence score.
        /// </returns>
        /// <remarks>
        /// The AI is instructed to:
        /// <list type="bullet">
        /// <item>Identify drug/ingredient names in the description</item>
        /// <item>Distinguish brand names from generic names</item>
        /// <item>Return the generic name preferred for database lookups</item>
        /// <item>Handle multi-word ingredient names (e.g., sevelamer carbonate)</item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// var result = await searchService.ExtractProductFromDescriptionAsync(
        ///     "Search for finerenone (Kerendia) - non-steroidal MRA for CKD");
        /// // Returns: { Success: true, ProductNames: ["finerenone"], ... }
        /// </code>
        /// </example>
        /// <seealso cref="IClaudeApiService"/>
        Task<ProductExtractionResult> ExtractProductFromDescriptionAsync(string description);

        #endregion

        #region indication search methods

        /**************************************************************/
        /// <summary>
        /// Loads and parses the indication reference data from the labelProductIndication.md file.
        /// Returns cached entries after initial load.
        /// </summary>
        /// <returns>
        /// A task that resolves to a list of <see cref="IndicationReferenceEntry"/>
        /// containing product names, UNII codes, and indication summaries.
        /// </returns>
        /// <remarks>
        /// Results are cached for 8 hours to minimize file I/O.
        /// The reference file contains ~1,000 entries covering FDA-labeled indications.
        /// </remarks>
        Task<List<IndicationReferenceEntry>> GetIndicationReferenceDataAsync();

        /**************************************************************/
        /// <summary>
        /// Uses AI to match a user's condition/indication query to candidate
        /// indication entries pre-filtered by keyword matching (Stage 2).
        /// </summary>
        /// <param name="userQuery">
        /// The user's natural language query about a medical condition.
        /// Examples: "high blood pressure", "type 2 diabetes", "depression"
        /// </param>
        /// <param name="candidates">
        /// Pre-filtered indication entries from Stage 1 keyword matching.
        /// </param>
        /// <returns>
        /// A task that resolves to an <see cref="IndicationMatchResult"/>
        /// containing matched UNIIs and relevance reasoning.
        /// </returns>
        /// <seealso cref="IClaudeApiService"/>
        Task<IndicationMatchResult> MatchUserQueryToIndicationsAsync(
            string userQuery,
            List<IndicationReferenceEntry> candidates);

        /**************************************************************/
        /// <summary>
        /// Performs a complete indication search workflow: keyword pre-filter,
        /// AI matching, product lookup, AI validation against label text,
        /// and consolidated results with label links.
        /// </summary>
        /// <param name="userQuery">
        /// The user's natural language query about a medical condition.
        /// </param>
        /// <param name="systemContext">
        /// Optional system context for authentication state.
        /// </param>
        /// <param name="maxProductsPerIndication">
        /// Maximum number of products to return per matched indication. Default is 25.
        /// </param>
        /// <returns>
        /// A task that resolves to an <see cref="IndicationSearchResult"/>
        /// containing validated products organized by UNII with label links.
        /// </returns>
        /// <remarks>
        /// This orchestrates a three-stage search:
        /// <list type="number">
        /// <item>Keyword pre-filter on reference data (no AI call)</item>
        /// <item>Claude AI matching on filtered candidates</item>
        /// <item>Claude AI validation against actual FDA label indication text</item>
        /// </list>
        /// </remarks>
        /// <seealso cref="GetIndicationReferenceDataAsync"/>
        /// <seealso cref="MatchUserQueryToIndicationsAsync"/>
        Task<IndicationSearchResult> SearchByIndicationAsync(
            string userQuery,
            AiSystemContext? systemContext = null,
            int maxProductsPerIndication = 25);

        #endregion
    }

    #endregion

    #region pharmacologic class search models

    /**************************************************************/
    /// <summary>
    /// Represents the result of matching user query terms to database class names.
    /// </summary>
    /// <seealso cref="IClaudeSearchService.MatchUserQueryToClassesAsync"/>
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
    /// <seealso cref="IClaudeSearchService.SearchByUserQueryAsync"/>
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
    /// <seealso cref="IClaudeSearchService.ExtractProductFromDescriptionAsync"/>
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
    /// <seealso cref="IClaudeSearchService.GetIndicationReferenceDataAsync"/>
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
    /// <seealso cref="IClaudeSearchService.MatchUserQueryToIndicationsAsync"/>
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
    /// <seealso cref="IClaudeSearchService.SearchByIndicationAsync"/>
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

    #region pharmacologic class search service implementation

    /**************************************************************/
    /// <summary>
    /// Implementation of the pharmacologic class search service that enables
    /// intelligent matching between user queries and database class names.
    /// </summary>
    /// <remarks>
    /// This service solves the context matching problem where users ask about
    /// drug classes using common terminology that differs from how classes are
    /// stored in the database. It uses AI to bridge the vocabulary gap.
    /// </remarks>
    /// <seealso cref="IClaudeSearchService"/>
    public class ClaudeSearchService : IClaudeSearchService
    {
        #region private fields

        /**************************************************************/
        /// <summary>
        /// Database context for querying pharmacologic class data.
        /// </summary>
        private readonly ApplicationDbContext _dbContext;

        /**************************************************************/
        /// <summary>
        /// Configuration provider for settings access.
        /// </summary>
        private readonly IConfiguration _configuration;

        /**************************************************************/
        /// <summary>
        /// Logger instance for diagnostic output.
        /// </summary>
        private readonly ILogger<ClaudeSearchService> _logger;

        /**************************************************************/
        /// <summary>
        /// Service scope factory for resolving scoped dependencies.
        /// </summary>
        /// <remarks>
        /// Used to create isolated scopes for resolving <see cref="IClaudeApiService"/>
        /// to avoid circular dependency issues.
        /// </remarks>
        private readonly IServiceScopeFactory _serviceScopeFactory;

        /**************************************************************/
        /// <summary>
        /// Encryption secret for ID encryption in DTOs.
        /// </summary>
        private readonly string _pkEncryptionSecret;

        /**************************************************************/
        /// <summary>
        /// Cache key for class summaries.
        /// </summary>
        private const string ClassSummariesCacheKey = "PharmacologicClassSearchService_AllSummaries";

        /**************************************************************/
        /// <summary>
        /// Cache duration for class summaries (4 hours).
        /// </summary>
        private const double CacheHours = 4.0;

        /**************************************************************/
        /// <summary>
        /// Configuration section key for Claude API settings.
        /// </summary>
        private const string SkillConfigSection = "ClaudeApiSettings";

        /**************************************************************/
        /// <summary>
        /// Cache duration for prompt templates (8 hours).
        /// </summary>
        private const double PromptCacheHours = 8.0;

        /**************************************************************/
        /// <summary>
        /// Cache key for indication reference data.
        /// </summary>
        private const string IndicationReferenceCacheKey = "ClaudeSearchService_IndicationReference";

        /**************************************************************/
        /// <summary>
        /// Cache duration for indication reference data (8 hours).
        /// </summary>
        private const double IndicationReferenceCacheHours = 8.0;

        /**************************************************************/
        /// <summary>
        /// Maximum number of candidates to send to Claude for indication matching.
        /// </summary>
        private const int MaxCandidatesForClaude = 50;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the PharmacologicClassSearchService.
        /// </summary>
        /// <param name="dbContext">Database context for data access.</param>
        /// <param name="configuration">Configuration provider.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="serviceScopeFactory">Scope factory for dependency resolution.</param>
        /// <seealso cref="IClaudeApiService"/>
        public ClaudeSearchService(
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            ILogger<ClaudeSearchService> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));

            // Get encryption secret from configuration
            _pkEncryptionSecret = _configuration.GetValue<string>("Security:DB:PKSecret")
                ?? throw new InvalidOperationException("PrimaryKeySecret not configured");
        }

        #endregion

        #region search methods

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// Retrieves all classes with no pagination to get complete vocabulary.
        /// Results are cached to minimize database load on repeated queries.
        /// Classes without linked products are filtered out.
        /// </remarks>
        public async Task<List<PharmacologicClassSummaryDto>> GetAllClassSummariesAsync()
        {
            #region implementation

            // Check cache first
            var cached = PerformanceHelper.GetCache<List<PharmacologicClassSummaryDto>>(ClassSummariesCacheKey);
            if (cached != null && cached.Count > 0)
            {
                _logger.LogDebug("Returning {Count} cached pharmacologic class summaries", cached.Count);
                return cached;
            }

            _logger.LogDebug("Retrieving all pharmacologic class summaries from database");

            // Get all summaries without pagination
            var allSummaries = await DtoLabelAccess.GetPharmacologicClassSummariesAsync(
                _dbContext,
                _pkEncryptionSecret,
                _logger,
                page: null,
                size: null);

            // Filter to only classes with products (ProductCount > 0)
            var filteredSummaries = allSummaries
                .Where(s => s.ProductCount.HasValue && s.ProductCount.Value > 0)
                .ToList();

            _logger.LogInformation("Retrieved {Total} class summaries, {Filtered} with products",
                allSummaries.Count, filteredSummaries.Count);

            // Cache for configured duration
            if (filteredSummaries.Count > 0)
            {
                PerformanceHelper.SetCacheManageKey(ClassSummariesCacheKey, filteredSummaries, CacheHours);
            }

            return filteredSummaries;

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// Uses Claude AI to interpret user terminology and map it to actual
        /// database class names. The AI is provided with the full list of
        /// available classes to ensure accurate matching.
        /// </remarks>
        public async Task<PharmacologicClassMatchResult> MatchUserQueryToClassesAsync(
            string userQuery,
            List<PharmacologicClassSummaryDto> availableClasses)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(userQuery))
            {
                return new PharmacologicClassMatchResult
                {
                    Success = false,
                    Error = "User query cannot be empty."
                };
            }

            if (availableClasses == null || availableClasses.Count == 0)
            {
                return new PharmacologicClassMatchResult
                {
                    Success = false,
                    Error = "No pharmacologic classes available in the database."
                };
            }

            _logger.LogDebug("Matching user query '{Query}' against {Count} available classes",
                userQuery, availableClasses.Count);

            try
            {
                // Build the class name list for AI matching
                var classNames = availableClasses
                    .Where(c => !string.IsNullOrWhiteSpace(c.PharmClassName))
                    .Select(c => c.PharmClassName!)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                // Create prompt for AI matching
                var matchPrompt = buildClassMatchingPrompt(userQuery, classNames);

                // Create scope to resolve IClaudeApiService
                using var scope = _serviceScopeFactory.CreateScope();
                var claudeApiService = scope.ServiceProvider.GetRequiredService<IClaudeApiService>();

                // Call Claude for matching (using GenerateDocumentComparisonAsync which accepts a prompt)
                var matchResponse = await claudeApiService.GenerateDocumentComparisonAsync(matchPrompt);

                // Parse the AI response
                return parseClassMatchResponse(matchResponse, classNames);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error matching user query to pharmacologic classes: {Query}", userQuery);

                // Attempt simple string matching as fallback
                return performSimpleMatching(userQuery, availableClasses);
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// Orchestrates the complete search workflow:
        /// 1. Gets all available classes from database
        /// 2. Uses AI to match user query to class names
        /// 3. Searches each matched class for products
        /// 4. Consolidates results with label links
        /// </remarks>
        public async Task<PharmacologicClassSearchResult> SearchByUserQueryAsync(
            string userQuery,
            AiSystemContext? systemContext = null,
            int maxProductsPerClass = 100)
        {
            #region implementation

            var result = new PharmacologicClassSearchResult
            {
                OriginalQuery = userQuery
            };

            if (string.IsNullOrWhiteSpace(userQuery))
            {
                result.Success = false;
                result.Error = "Search query cannot be empty.";
                return result;
            }

            _logger.LogInformation("[PHARM CLASS SEARCH] Starting search for user query: {Query}", userQuery);

            try
            {
                // Step 1: Get all available class summaries
                var allClasses = await GetAllClassSummariesAsync();
                if (allClasses.Count == 0)
                {
                    result.Success = false;
                    result.Error = "No pharmacologic classes found in the database.";
                    return result;
                }

                _logger.LogDebug("Retrieved {Count} pharmacologic classes for matching", allClasses.Count);

                // Step 2: Match user query to class names using AI
                var matchResult = await MatchUserQueryToClassesAsync(userQuery, allClasses);
                if (!matchResult.Success || matchResult.MatchedClassNames.Count == 0)
                {
                    result.Success = false;
                    result.Error = matchResult.Error ?? "No pharmacologic classes matched your query.";
                    result.Explanation = matchResult.Explanation;
                    result.SuggestedFollowUps = matchResult.Suggestions;
                    return result;
                }

                result.MatchedClasses = matchResult.MatchedClassNames;
                _logger.LogInformation("[PHARM CLASS SEARCH] Matched {Count} classes: [{Classes}]",
                    matchResult.MatchedClassNames.Count,
                    string.Join(", ", matchResult.MatchedClassNames));

                // Step 3: Search each matched class for products
                var allProducts = new List<PharmacologicClassProductInfo>();
                foreach (var className in matchResult.MatchedClassNames)
                {
                    var classProducts = await searchProductsInClassAsync(className, maxProductsPerClass);
                    if (classProducts.Count > 0)
                    {
                        result.ProductsByClass[className] = classProducts;
                        allProducts.AddRange(classProducts);
                    }
                }

                result.TotalProductCount = allProducts.Count;

                // Step 4: Build label links for all products
                foreach (var product in allProducts.Where(p => !string.IsNullOrEmpty(p.DocumentGuid)))
                {
                    var linkKey = $"View Full Label ({product.ProductName})";
                    var linkValue = $"/api/Label/original/{product.DocumentGuid}/true";

                    // Avoid duplicate keys
                    if (!result.LabelLinks.ContainsKey(linkKey))
                    {
                        result.LabelLinks[linkKey] = linkValue;
                    }
                }

                result.Success = true;
                result.Explanation = matchResult.Explanation;
                result.SuggestedFollowUps = generateFollowUpSuggestions(result);

                _logger.LogInformation("[PHARM CLASS SEARCH] Found {Count} products across {ClassCount} classes",
                    result.TotalProductCount, result.MatchedClasses.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during pharmacologic class search for query: {Query}", userQuery);
                result.Success = false;
                result.Error = $"Search failed: {ex.Message}";
                return result;
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// Uses Claude AI to intelligently extract drug/ingredient names from
        /// natural language descriptions. This is critical for UNII resolution
        /// fallback where the description contains the correct product name
        /// but the UNII from reference data was incorrect.
        ///
        /// The AI handles:
        /// - Brand to generic name resolution
        /// - Multi-word ingredient names (e.g., "sevelamer carbonate")
        /// - Drug class mentions vs specific drug names
        /// - Abbreviations (e.g., MRA, SGLT2, GLP-1)
        /// </remarks>
        public async Task<ProductExtractionResult> ExtractProductFromDescriptionAsync(string description)
        {
            #region implementation

            var result = new ProductExtractionResult();

            // Validate input
            if (string.IsNullOrWhiteSpace(description))
            {
                result.Success = false;
                result.Error = "Description cannot be empty.";
                return result;
            }

            _logger.LogDebug("[PRODUCT EXTRACTION] Extracting product from description: {Description}", description);

            try
            {
                // Build the AI prompt for product extraction
                var extractionPrompt = buildProductExtractionPrompt(description);

                // Create scope to resolve IClaudeApiService
                using var scope = _serviceScopeFactory.CreateScope();
                var claudeApiService = scope.ServiceProvider.GetRequiredService<IClaudeApiService>();

                // Call Claude for extraction
                var aiResponse = await claudeApiService.GenerateDocumentComparisonAsync(extractionPrompt);

                // Parse the AI response
                result = parseProductExtractionResponse(aiResponse, description);

                _logger.LogInformation("[PRODUCT EXTRACTION] Extracted products: [{Products}] from: {Description}",
                    string.Join(", ", result.ProductNames),
                    description.Length > 100 ? description.Substring(0, 100) + "..." : description);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PRODUCT EXTRACTION] Error extracting product from description: {Description}", description);

                // Attempt simple fallback extraction
                return performSimpleProductExtraction(description);
            }

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Loads a prompt template from the configured file path with caching.
        /// </summary>
        /// <param name="configKey">The configuration key (e.g., "Prompt-ProductExtraction").</param>
        /// <param name="cacheKeyPrefix">A prefix for the cache key.</param>
        /// <returns>The prompt template content or an error message.</returns>
        private string loadPromptTemplate(string configKey, string cacheKeyPrefix)
        {
            #region implementation

            // Check cache first
            var key = $"{cacheKeyPrefix}_{configKey}";
            var cachedPrompt = PerformanceHelper.GetCache<string>(key);
            if (!string.IsNullOrEmpty(cachedPrompt))
            {
                return cachedPrompt;
            }

            // Get the configured path from ClaudeApiSettings
            var promptFilePath = _configuration.GetValue<string>($"{SkillConfigSection}:{configKey}");

            if (string.IsNullOrEmpty(promptFilePath))
            {
                _logger.LogWarning("[PROMPT LOAD] Configuration key '{ConfigKey}' not found in {Section}",
                    configKey, SkillConfigSection);
                return $"{configKey} configuration not found in {SkillConfigSection}.";
            }

            var content = readPromptFileByPath(promptFilePath);

            // Cache for configured duration to reduce file I/O
            PerformanceHelper.SetCacheManageKey(key, content, PromptCacheHours);

            return content;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Reads a prompt file from the specified path.
        /// </summary>
        /// <param name="promptFilePath">The relative or absolute path to the prompt file.</param>
        /// <returns>The file content or an error message.</returns>
        private string readPromptFileByPath(string promptFilePath)
        {
            #region implementation

            // Resolve the path relative to the application's content root
            var fullPath = Path.Combine(AppContext.BaseDirectory, promptFilePath);

            if (!File.Exists(fullPath))
            {
                // Try relative to current directory as fallback
                fullPath = Path.Combine(Directory.GetCurrentDirectory(), promptFilePath);
            }

            if (!File.Exists(fullPath))
            {
                _logger.LogError("[PROMPT LOAD] File not found at: {PromptFilePath}", promptFilePath);
                return $"Prompt file not found at: {promptFilePath}";
            }

            var content = File.ReadAllText(fullPath);
            _logger.LogDebug("[PROMPT LOAD] Successfully loaded: {FullPath} ({Length} chars)",
                fullPath, content.Length);

            return content;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the AI prompt for extracting product names from a description.
        /// Loads the prompt template from file and substitutes the description placeholder.
        /// </summary>
        /// <param name="description">The description to extract from.</param>
        /// <returns>Formatted prompt for Claude AI.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the prompt template cannot be loaded.</exception>
        /// <seealso cref="ExtractProductFromDescriptionAsync"/>
        private string buildProductExtractionPrompt(string description)
        {
            #region implementation

            // Load the prompt template from file
            var template = loadPromptTemplate("Prompt-ProductExtraction", "ClaudeSearchService_ProductExtraction");

            // If template loading failed, throw an error - prompts should be in files
            if (template.Contains("not found") || template.Contains("configuration not found"))
            {
                _logger.LogError("[PRODUCT EXTRACTION] Prompt template file not found. Ensure Prompt-ProductExtraction is configured in appsettings.json");
                throw new InvalidOperationException(
                    "Product extraction prompt template not found. Ensure 'Prompt-ProductExtraction' is configured in ClaudeApiSettings.");
            }

            // Substitute the placeholder with the actual description
            return template.Replace("{{DESCRIPTION}}", description);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the AI response for product extraction.
        /// </summary>
        /// <param name="aiResponse">Raw AI response string.</param>
        /// <param name="originalDescription">Original description for fallback.</param>
        /// <returns>Parsed extraction result.</returns>
        /// <seealso cref="ExtractProductFromDescriptionAsync"/>
        private ProductExtractionResult parseProductExtractionResponse(string aiResponse, string originalDescription)
        {
            #region implementation

            try
            {
                // Extract JSON from response (handle markdown code blocks)
                var jsonContent = extractJsonFromResponse(aiResponse);

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogWarning("[PRODUCT EXTRACTION] Could not extract JSON from AI response");
                    return performSimpleProductExtraction(originalDescription);
                }

                // Parse the JSON response
                var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonContent);

                if (parsed == null)
                {
                    return performSimpleProductExtraction(originalDescription);
                }

                var result = new ProductExtractionResult
                {
                    Success = parsed.success ?? false,
                    Confidence = parsed.confidence?.ToString() ?? "low",
                    Explanation = parsed.explanation?.ToString(),
                    BrandMappingApplied = parsed.brandMappingApplied ?? false,
                    OriginalBrandName = parsed.originalBrandName?.ToString()
                };

                // Extract product names
                if (parsed.productNames != null)
                {
                    foreach (var productName in parsed.productNames)
                    {
                        string name = productName.ToString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            result.ProductNames.Add(name);
                        }
                    }
                }

                // Ensure success is true if we have products
                result.Success = result.ProductNames.Count > 0;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PRODUCT EXTRACTION] Error parsing AI response");
                return performSimpleProductExtraction(originalDescription);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs simple regex-based product extraction as a fallback.
        /// </summary>
        /// <param name="description">Description to extract from.</param>
        /// <returns>Extraction result from simple matching.</returns>
        /// <remarks>
        /// This is a fallback when AI extraction fails. Uses patterns similar
        /// to the JavaScript extractProductNameFromDescription function.
        /// </remarks>
        /// <seealso cref="ExtractProductFromDescriptionAsync"/>
        private ProductExtractionResult performSimpleProductExtraction(string description)
        {
            #region implementation

            _logger.LogDebug("[PRODUCT EXTRACTION] Falling back to simple extraction for: {Description}", description);

            var result = new ProductExtractionResult
            {
                Confidence = "low",
                Explanation = "Extracted using pattern matching fallback"
            };

            // Brand to generic mappings for fallback
            var brandMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "kerendia", "finerenone" },
                { "jardiance", "empagliflozin" },
                { "farxiga", "dapagliflozin" },
                { "invokana", "canagliflozin" },
                { "ozempic", "semaglutide" },
                { "wegovy", "semaglutide" },
                { "mounjaro", "tirzepatide" },
                { "trulicity", "dulaglutide" },
                { "victoza", "liraglutide" },
                { "renvela", "sevelamer" },
                { "renagel", "sevelamer" },
                { "lipitor", "atorvastatin" },
                { "crestor", "rosuvastatin" },
                { "zocor", "simvastatin" },
                { "plavix", "clopidogrel" },
                { "eliquis", "apixaban" },
                { "xarelto", "rivaroxaban" },
                { "pradaxa", "dabigatran" },
                { "entresto", "sacubitril" },
                { "januvia", "sitagliptin" },
                { "tradjenta", "linagliptin" }
            };

            // Words to skip
            var skipWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "search", "get", "find", "retrieve", "lookup", "fetch", "query",
                "show", "list", "display", "view", "check", "verify",
                "for", "the", "a", "an", "to", "of", "in", "on", "with", "from", "by",
                "products", "product", "medications", "medication", "drugs", "drug",
                "label", "labels", "information", "info", "details", "data",
                "treatment", "therapy", "indication", "indications", "used",
                "patients", "patient", "adults", "children", "pediatric",
                "chronic", "acute", "severe", "mild", "moderate",
                "kidney", "renal", "hepatic", "liver", "cardiac", "heart",
                "disease", "disorder", "syndrome", "condition", "type",
                "phosphate", "binder", "inhibitor", "blocker", "agonist", "antagonist",
                "receptor", "enzyme", "channel", "transporter"
            };

            // Pattern 1: "Search for X" or "search for X (Brand)"
            var searchForMatch = System.Text.RegularExpressions.Regex.Match(
                description,
                @"search\s+for\s+([a-zA-Z][a-zA-Z0-9\-]*)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (searchForMatch.Success)
            {
                var extracted = searchForMatch.Groups[1].Value.Trim().ToLowerInvariant();

                // Check if it's a brand name
                if (brandMappings.TryGetValue(extracted, out var generic))
                {
                    result.ProductNames.Add(generic);
                    result.BrandMappingApplied = true;
                    result.OriginalBrandName = extracted;
                }
                else if (!skipWords.Contains(extracted) && extracted.Length >= 4)
                {
                    result.ProductNames.Add(extracted);
                }
            }

            // Pattern 2: Look for known brand names anywhere in the description
            if (result.ProductNames.Count == 0)
            {
                foreach (var mapping in brandMappings)
                {
                    if (description.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        result.ProductNames.Add(mapping.Value);
                        result.BrandMappingApplied = true;
                        result.OriginalBrandName = mapping.Key;
                        break;
                    }
                }
            }

            // Pattern 3: Find first drug-like word
            if (result.ProductNames.Count == 0)
            {
                var words = description.Split(new[] { ' ', '-', '(', ')', ',', ';', ':' },
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in words)
                {
                    var cleanWord = word.Trim().ToLowerInvariant();
                    if (cleanWord.Length >= 4 &&
                        !skipWords.Contains(cleanWord) &&
                        System.Text.RegularExpressions.Regex.IsMatch(cleanWord, @"^[a-z]"))
                    {
                        result.ProductNames.Add(cleanWord);
                        break;
                    }
                }
            }

            result.Success = result.ProductNames.Count > 0;
            if (!result.Success)
            {
                result.Error = "Could not extract product name from description";
            }

            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the AI prompt for matching user query to class names.
        /// Loads the prompt template from file and substitutes placeholders.
        /// </summary>
        /// <param name="userQuery">The user's query text.</param>
        /// <param name="classNames">List of available class names from database.</param>
        /// <returns>Formatted prompt for Claude AI.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the prompt template cannot be loaded.</exception>
        private string buildClassMatchingPrompt(string userQuery, List<string> classNames)
        {
            #region implementation

            var classListFormatted = string.Join("\n", classNames.Select(c => $"- {c}"));

            // Load the prompt template from file
            var template = loadPromptTemplate("Prompt-PharmacologicClassMatching", "ClaudeSearchService_ClassMatching");

            // If template loading failed, throw an error - prompts should be in files
            if (template.Contains("not found") || template.Contains("configuration not found"))
            {
                _logger.LogError("[CLASS MATCHING] Prompt template file not found. Ensure Prompt-PharmacologicClassMatching is configured in appsettings.json");
                throw new InvalidOperationException(
                    "Pharmacologic class matching prompt template not found. Ensure 'Prompt-PharmacologicClassMatching' is configured in ClaudeApiSettings.");
            }

            // Substitute the placeholders with actual values
            return template
                .Replace("{{USER_QUERY}}", userQuery)
                .Replace("{{CLASS_LIST}}", classListFormatted);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the AI response for class matching.
        /// </summary>
        /// <param name="aiResponse">Raw AI response string.</param>
        /// <param name="availableClassNames">List of valid class names for validation.</param>
        /// <returns>Parsed match result.</returns>
        private PharmacologicClassMatchResult parseClassMatchResponse(
            string aiResponse,
            List<string> availableClassNames)
        {
            #region implementation

            try
            {
                // Extract JSON from response (handle markdown code blocks)
                var jsonContent = extractJsonFromResponse(aiResponse);

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogWarning("Could not extract JSON from AI response for class matching");
                    return new PharmacologicClassMatchResult
                    {
                        Success = false,
                        Error = "Failed to parse AI response for class matching."
                    };
                }

                // Parse the JSON response
                var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonContent);

                if (parsed == null)
                {
                    return new PharmacologicClassMatchResult
                    {
                        Success = false,
                        Error = "Invalid AI response format."
                    };
                }

                var result = new PharmacologicClassMatchResult
                {
                    Success = parsed.success ?? false,
                    Explanation = parsed.explanation?.ToString()
                };

                // Extract matched class names
                if (parsed.matchedClassNames != null)
                {
                    foreach (var className in parsed.matchedClassNames)
                    {
                        string name = className.ToString();
                        // Validate that the class name exists in our list (case-insensitive)
                        var matchedName = availableClassNames
                            .FirstOrDefault(c => c.Equals(name, StringComparison.OrdinalIgnoreCase));

                        if (matchedName != null)
                        {
                            result.MatchedClassNames.Add(matchedName);
                        }
                        else
                        {
                            // Try partial matching as fallback
                            var partialMatch = availableClassNames
                                .FirstOrDefault(c => c.Contains(name, StringComparison.OrdinalIgnoreCase)
                                    || name.Contains(c, StringComparison.OrdinalIgnoreCase));
                            if (partialMatch != null && !result.MatchedClassNames.Contains(partialMatch))
                            {
                                result.MatchedClassNames.Add(partialMatch);
                            }
                        }
                    }
                }

                // Extract suggestions if present
                if (parsed.suggestions != null)
                {
                    result.Suggestions = new List<string>();
                    foreach (var suggestion in parsed.suggestions)
                    {
                        result.Suggestions.Add(suggestion.ToString());
                    }
                }

                result.Success = result.MatchedClassNames.Count > 0;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing class match response");
                return new PharmacologicClassMatchResult
                {
                    Success = false,
                    Error = $"Failed to parse AI response: {ex.Message}"
                };
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts JSON content from AI response, handling markdown code blocks.
        /// </summary>
        /// <param name="response">Raw AI response.</param>
        /// <returns>Extracted JSON string or null.</returns>
        private string? extractJsonFromResponse(string response)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(response))
            {
                return null;
            }

            // Try to find JSON in markdown code blocks
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                response,
                @"```(?:json)?\s*\n?([\s\S]*?)\n?```",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (jsonMatch.Success)
            {
                return jsonMatch.Groups[1].Value.Trim();
            }

            // Try to find raw JSON object
            var jsonObjectMatch = System.Text.RegularExpressions.Regex.Match(
                response,
                @"\{[\s\S]*\}");

            if (jsonObjectMatch.Success)
            {
                return jsonObjectMatch.Value;
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs simple string matching as fallback when AI matching fails.
        /// </summary>
        /// <param name="userQuery">User query text.</param>
        /// <param name="availableClasses">Available class summaries.</param>
        /// <returns>Match result from simple matching.</returns>
        private PharmacologicClassMatchResult performSimpleMatching(
            string userQuery,
            List<PharmacologicClassSummaryDto> availableClasses)
        {
            #region implementation

            _logger.LogDebug("Falling back to simple string matching for query: {Query}", userQuery);

            var queryLower = userQuery.ToLowerInvariant();
            var matchedClasses = new List<string>();

            // Common term mappings for fallback
            var termMappings = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "beta blocker", new[] { "beta", "blocker", "adrenergic" } },
                { "ace inhibitor", new[] { "ace", "angiotensin", "converting", "enzyme" } },
                { "ssri", new[] { "ssri", "serotonin", "reuptake" } },
                { "statin", new[] { "statin", "hmg-coa", "reductase" } },
                { "calcium channel", new[] { "calcium", "channel" } },
                { "opioid", new[] { "opioid", "narcotic", "analgesic" } },
                { "benzodiazepine", new[] { "benzodiazepine", "benzo" } },
                { "antibiotic", new[] { "antibiotic", "antibacterial", "antimicrobial" } },
                { "antidepressant", new[] { "antidepressant", "depression" } },
                { "antipsychotic", new[] { "antipsychotic", "psychotic" } }
            };

            foreach (var cls in availableClasses)
            {
                if (string.IsNullOrWhiteSpace(cls.PharmClassName))
                    continue;

                var classNameLower = cls.PharmClassName.ToLowerInvariant();

                // Direct substring match
                if (classNameLower.Contains(queryLower) || queryLower.Contains(classNameLower))
                {
                    matchedClasses.Add(cls.PharmClassName);
                    continue;
                }

                // Check term mappings
                foreach (var mapping in termMappings)
                {
                    if (queryLower.Contains(mapping.Key))
                    {
                        // Check if any of the mapped terms appear in the class name
                        if (mapping.Value.Any(term => classNameLower.Contains(term)))
                        {
                            matchedClasses.Add(cls.PharmClassName);
                            break;
                        }
                    }
                }
            }

            return new PharmacologicClassMatchResult
            {
                Success = matchedClasses.Count > 0,
                MatchedClassNames = matchedClasses.Distinct().ToList(),
                Explanation = matchedClasses.Count > 0
                    ? $"Found {matchedClasses.Count} class(es) through simple string matching."
                    : "No classes matched using simple string matching.",
                Suggestions = matchedClasses.Count == 0
                    ? new List<string>
                    {
                        "Try using more specific class names",
                        "Browse available classes with 'show pharmacologic classes'"
                    }
                    : null
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches for products within a specific pharmacologic class.
        /// </summary>
        /// <param name="className">The exact class name to search.</param>
        /// <param name="maxProducts">Maximum products to return.</param>
        /// <returns>List of product information.</returns>
        private async Task<List<PharmacologicClassProductInfo>> searchProductsInClassAsync(
            string className,
            int maxProducts)
        {
            #region implementation

            try
            {
                _logger.LogDebug("Searching products in class: {ClassName}", className);

                var products = await DtoLabelAccess.SearchByPharmacologicClassExactAsync(
                    _dbContext,
                    className,
                    _pkEncryptionSecret,
                    _logger,
                    page: 1,
                    size: maxProducts);

                return products.Select(p => new PharmacologicClassProductInfo
                {
                    ProductName = p.ProductName ?? "Unknown Product",
                    DocumentGuid = extractDocumentGuid(p),
                    PharmClassName = p.PharmClassName,
                    ActiveIngredient = extractActiveIngredient(p),
                    LabelerName = extractLabelerName(p)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products in class: {ClassName}", className);
                return new List<PharmacologicClassProductInfo>();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the document GUID from a product DTO.
        /// </summary>
        /// <param name="product">The product DTO.</param>
        /// <returns>Document GUID string or null.</returns>
        private string? extractDocumentGuid(ProductsByPharmacologicClassDto product)
        {
            #region implementation

            if (product.ProductsByPharmacologicClass == null)
                return null;

            // Try DocumentGUID field
            if (product.ProductsByPharmacologicClass.TryGetValue("DocumentGUID", out var guidValue))
            {
                return guidValue?.ToString();
            }

            // Try documentGuid field (camelCase)
            if (product.ProductsByPharmacologicClass.TryGetValue("documentGuid", out var guidValue2))
            {
                return guidValue2?.ToString();
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the active ingredient from a product DTO.
        /// </summary>
        /// <param name="product">The product DTO.</param>
        /// <returns>Active ingredient string or null.</returns>
        private string? extractActiveIngredient(ProductsByPharmacologicClassDto product)
        {
            #region implementation

            if (product.ProductsByPharmacologicClass == null)
                return null;

            if (product.ProductsByPharmacologicClass.TryGetValue("ActiveIngredient", out var value) ||
                product.ProductsByPharmacologicClass.TryGetValue("activeIngredient", out value) ||
                product.ProductsByPharmacologicClass.TryGetValue("SubstanceName", out value))
            {
                return value?.ToString();
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the labeler name from a product DTO.
        /// </summary>
        /// <param name="product">The product DTO.</param>
        /// <returns>Labeler name string or null.</returns>
        private string? extractLabelerName(ProductsByPharmacologicClassDto product)
        {
            #region implementation

            if (product.ProductsByPharmacologicClass == null)
                return null;

            if (product.ProductsByPharmacologicClass.TryGetValue("LabelerName", out var value) ||
                product.ProductsByPharmacologicClass.TryGetValue("labelerName", out value))
            {
                return value?.ToString();
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates suggested follow-up queries based on search results.
        /// </summary>
        /// <param name="result">The search result.</param>
        /// <returns>List of follow-up suggestions.</returns>
        private List<string> generateFollowUpSuggestions(PharmacologicClassSearchResult result)
        {
            #region implementation

            var suggestions = new List<string>();

            if (result.MatchedClasses.Count > 0)
            {
                var firstClass = result.MatchedClasses.First();

                // Suggest viewing specific product details
                if (result.ProductsByClass.TryGetValue(firstClass, out var products) && products.Count > 0)
                {
                    var firstProduct = products.First();
                    suggestions.Add($"Tell me about the side effects of {firstProduct.ProductName}");
                }

                // Suggest related queries
                suggestions.Add("What are the contraindications for these medications?");
                suggestions.Add("Show me the dosing information");
            }

            if (suggestions.Count == 0)
            {
                suggestions.Add("Show me all pharmacologic classes");
                suggestions.Add("Search for products by ingredient");
            }

            return suggestions;

            #endregion
        }

        #endregion

        #region indication search methods

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// Loads the labelProductIndication.md reference file, parses it into
        /// structured entries, and caches the result for 8 hours.
        /// </remarks>
        public async Task<List<IndicationReferenceEntry>> GetIndicationReferenceDataAsync()
        {
            #region implementation

            // Check cache first
            var cached = PerformanceHelper.GetCache<List<IndicationReferenceEntry>>(IndicationReferenceCacheKey);
            if (cached != null && cached.Count > 0)
            {
                _logger.LogDebug("Returning {Count} cached indication reference entries", cached.Count);
                return cached;
            }

            _logger.LogDebug("Loading indication reference data from file");

            // Load the reference file content
            var content = loadPromptTemplate("Skill-LabelProductIndication", "ClaudeSearchService_IndicationRef");

            if (string.IsNullOrWhiteSpace(content) || content.Contains("not found"))
            {
                _logger.LogWarning("[INDICATION SEARCH] Reference file not found or empty");
                return new List<IndicationReferenceEntry>();
            }

            // Parse the file into structured entries
            var entries = parseIndicationReferenceFile(content);

            _logger.LogInformation("Parsed {Count} indication reference entries from file", entries.Count);

            // Cache for configured duration
            if (entries.Count > 0)
            {
                PerformanceHelper.SetCacheManageKey(IndicationReferenceCacheKey, entries, IndicationReferenceCacheHours);
            }

            return await Task.FromResult(entries);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the labelProductIndication.md reference file into structured entries.
        /// Splits on "---" separators and extracts product names, UNII, and indication text.
        /// </summary>
        /// <param name="content">Raw file content.</param>
        /// <returns>List of parsed entries.</returns>
        private List<IndicationReferenceEntry> parseIndicationReferenceFile(string content)
        {
            #region implementation

            var entries = new List<IndicationReferenceEntry>();

            if (string.IsNullOrWhiteSpace(content))
                return entries;

            // Split on --- separators
            var blocks = content.Split(new[] { "\n---\n", "\r\n---\r\n", "\n---\r\n", "\r\n---\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in blocks)
            {
                var trimmedBlock = block.Trim();
                if (string.IsNullOrWhiteSpace(trimmedBlock))
                    continue;

                // Skip header blocks (contain "## Data" or "Data Dictionary" or the header row "ProductNames|UNII|...")
                if (trimmedBlock.Contains("## Data") || trimmedBlock.Contains("Data Dictionary") || trimmedBlock.Contains("# Pharmaceutical"))
                    continue;

                var lines = trimmedBlock.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0)
                    continue;

                // First line should be pipe-delimited header: ProductNames|UNII|IndicationsSummary
                var firstLine = lines[0].Trim();

                // Skip if this is the column header row
                if (firstLine.StartsWith("ProductNames|UNII|", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Parse the header line
                var headerParts = firstLine.Split('|');
                if (headerParts.Length < 2)
                    continue;

                var productNamesRaw = headerParts[0].Trim();
                var unii = headerParts[1].Trim();

                // Validate UNII (should be alphanumeric, typically 10 chars)
                if (string.IsNullOrWhiteSpace(unii) || unii.Length < 5)
                    continue;

                // Parse product names (comma-separated)
                var productNames = productNamesRaw
                    .Split(',')
                    .Select(n => n.Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                // Remaining lines form the indication summary
                var indicationLines = lines.Skip(1).ToList();
                var indicationSummary = string.Join("\n", indicationLines).Trim();

                entries.Add(new IndicationReferenceEntry
                {
                    ProductNames = productNames,
                    UNII = unii,
                    IndicationsSummary = indicationSummary
                });
            }

            return entries;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 1: Pre-filters indication reference entries by keyword matching.
        /// Tokenizes the user query, expands with medical synonyms, and scores entries.
        /// No Claude API call is made — this is pure C# in-memory filtering.
        /// </summary>
        /// <param name="userQuery">User's natural language query.</param>
        /// <param name="allEntries">All indication reference entries.</param>
        /// <returns>Filtered and scored entries, capped at <see cref="MaxCandidatesForClaude"/>.</returns>
        private List<IndicationReferenceEntry> preFilterIndicationsByKeyword(
            string userQuery,
            List<IndicationReferenceEntry> allEntries)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(userQuery) || allEntries.Count == 0)
                return new List<IndicationReferenceEntry>();

            var queryLower = userQuery.ToLowerInvariant();

            // Tokenize query and remove stop words
            var stopWords = new HashSet<string> { "a", "an", "the", "for", "of", "in", "to", "and", "or", "is", "are", "what", "which", "that", "with", "my", "me", "i", "have", "has", "do", "does", "can", "will", "would", "should", "could", "treat", "treats", "treating", "medication", "medications", "medicine", "medicines", "drug", "drugs", "used", "help", "helps" };

            var queryTokens = queryLower
                .Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '-', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 1 && !stopWords.Contains(t))
                .Distinct()
                .ToList();

            // Expand synonyms via condition keyword map
            var synonymMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "hypertension", new[] { "hypertension", "antihypertensive", "blood pressure", "high blood pressure" } },
                { "high blood pressure", new[] { "hypertension", "antihypertensive", "blood pressure" } },
                { "blood pressure", new[] { "hypertension", "antihypertensive", "blood pressure" } },
                { "diabetes", new[] { "diabetes", "diabetic", "glycemic", "type 2", "hyperglycemia", "glucose", "insulin", "antidiabetic" } },
                { "type 2", new[] { "diabetes", "glycemic", "type 2", "hyperglycemia" } },
                { "depression", new[] { "depression", "depressive", "mdd", "major depressive", "antidepressant" } },
                { "anxiety", new[] { "anxiety", "anxiolytic", "panic", "generalized anxiety", "gad" } },
                { "allergies", new[] { "allergic", "rhinitis", "antihistamine", "seasonal", "allergy", "allergies" } },
                { "allergy", new[] { "allergic", "rhinitis", "antihistamine", "seasonal", "allergy" } },
                { "pain", new[] { "pain", "analgesic", "arthritis", "nociceptive", "neuropathic" } },
                { "cancer", new[] { "cancer", "carcinoma", "malignancy", "oncology", "tumor", "neoplasm", "metastatic" } },
                { "infection", new[] { "infection", "infectious", "bacterial", "antimicrobial", "antibiotic", "antifungal" } },
                { "cholesterol", new[] { "cholesterol", "hyperlipidemia", "dyslipidemia", "lipid", "triglyceride", "ldl", "statin" } },
                { "high cholesterol", new[] { "cholesterol", "hyperlipidemia", "dyslipidemia", "lipid", "statin" } },
                { "asthma", new[] { "asthma", "bronchospasm", "bronchodilator", "bronchoconstriction" } },
                { "seizure", new[] { "seizure", "epilepsy", "anticonvulsant", "antiepileptic", "convulsion" } },
                { "epilepsy", new[] { "seizure", "epilepsy", "anticonvulsant", "antiepileptic" } },
                { "heart failure", new[] { "heart failure", "cardiac failure", "congestive", "chf", "hfref", "hfpef" } },
                { "atrial fibrillation", new[] { "atrial fibrillation", "afib", "arrhythmia", "antiarrhythmic" } },
                { "arthritis", new[] { "arthritis", "rheumatoid", "osteoarthritis", "joint", "inflammatory" } },
                { "osteoporosis", new[] { "osteoporosis", "bone density", "bone loss", "bisphosphonate" } },
                { "insomnia", new[] { "insomnia", "sleep", "sedative", "hypnotic" } },
                { "hiv", new[] { "hiv", "aids", "antiretroviral", "human immunodeficiency" } },
                { "copd", new[] { "copd", "chronic obstructive", "bronchitis", "emphysema" } },
                { "migraine", new[] { "migraine", "headache", "cephalalgia" } },
                { "nausea", new[] { "nausea", "vomiting", "emesis", "antiemetic" } },
                { "gout", new[] { "gout", "uric acid", "hyperuricemia", "urate" } },
                { "thyroid", new[] { "thyroid", "hypothyroidism", "hyperthyroidism", "levothyroxine" } },
                { "psoriasis", new[] { "psoriasis", "plaque psoriasis", "psoriatic" } },
                { "schizophrenia", new[] { "schizophrenia", "psychosis", "psychotic", "antipsychotic" } },
                { "bipolar", new[] { "bipolar", "mania", "manic", "mood stabilizer" } },
                { "adhd", new[] { "adhd", "attention deficit", "hyperactivity" } },
                { "parkinsons", new[] { "parkinson", "dopamine", "dopaminergic" } },
                { "alzheimers", new[] { "alzheimer", "dementia", "cognitive", "cholinesterase" } },
                { "constipation", new[] { "constipation", "laxative", "bowel" } },
                { "diarrhea", new[] { "diarrhea", "antidiarrheal" } },
                { "ulcer", new[] { "ulcer", "peptic", "gastric", "duodenal", "helicobacter" } },
                { "pneumonia", new[] { "pneumonia", "pulmonary infection", "respiratory infection" } },
                { "malaria", new[] { "malaria", "antimalarial", "plasmodium" } },
                { "tuberculosis", new[] { "tuberculosis", "tb", "mycobacterium" } },
                { "hepatitis", new[] { "hepatitis", "liver", "hepatic", "hcv", "hbv" } },
                { "kidney", new[] { "kidney", "renal", "nephropathy", "ckd" } },
                { "glaucoma", new[] { "glaucoma", "intraocular pressure", "iop" } },
                { "acne", new[] { "acne", "acne vulgaris" } },
                { "eczema", new[] { "eczema", "dermatitis", "atopic" } },
                { "obesity", new[] { "obesity", "weight management", "bmi", "overweight" } },
                { "clot", new[] { "clot", "thrombosis", "embolism", "anticoagulant", "thromboembolic", "dvt" } },
                { "stroke", new[] { "stroke", "cerebrovascular", "thromboembolic" } },
                { "prostate", new[] { "prostate", "bph", "prostatic hyperplasia" } },
            };

            // Build expanded search terms
            var expandedTerms = new HashSet<string>(queryTokens, StringComparer.OrdinalIgnoreCase);

            // Check if the full query matches a synonym key
            foreach (var mapping in synonymMap)
            {
                if (queryLower.Contains(mapping.Key))
                {
                    foreach (var synonym in mapping.Value)
                        expandedTerms.Add(synonym.ToLowerInvariant());
                }
            }

            // Also check individual tokens
            foreach (var token in queryTokens)
            {
                if (synonymMap.TryGetValue(token, out var synonyms))
                {
                    foreach (var synonym in synonyms)
                        expandedTerms.Add(synonym.ToLowerInvariant());
                }
            }

            // Score each entry by keyword hit count
            var scored = new List<(IndicationReferenceEntry Entry, int Score)>();

            foreach (var entry in allEntries)
            {
                var searchText = $"{string.Join(" ", entry.ProductNames)} {entry.IndicationsSummary}".ToLowerInvariant();
                int score = 0;

                foreach (var term in expandedTerms)
                {
                    // Count occurrences of each term
                    var idx = 0;
                    while ((idx = searchText.IndexOf(term, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
                    {
                        score++;
                        idx += term.Length;
                    }
                }

                if (score > 0)
                    scored.Add((entry, score));
            }

            // Sort by score descending, cap at MaxCandidatesForClaude
            return scored
                .OrderByDescending(s => s.Score)
                .Take(MaxCandidatesForClaude)
                .Select(s => s.Entry)
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// Stage 2: Sends pre-filtered candidates to Claude for semantic matching.
        /// Falls back to simple keyword matching if the AI call fails.
        /// </remarks>
        public async Task<IndicationMatchResult> MatchUserQueryToIndicationsAsync(
            string userQuery,
            List<IndicationReferenceEntry> candidates)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(userQuery))
            {
                return new IndicationMatchResult
                {
                    Success = false,
                    Error = "User query cannot be empty."
                };
            }

            if (candidates == null || candidates.Count == 0)
            {
                return new IndicationMatchResult
                {
                    Success = false,
                    Error = "No candidate indications available for matching."
                };
            }

            _logger.LogDebug("Matching user query '{Query}' against {Count} indication candidates",
                userQuery, candidates.Count);

            try
            {
                // Format candidates for the prompt
                var formattedCandidates = formatCandidatesForPrompt(candidates);

                // Build the AI prompt
                var matchPrompt = buildIndicationMatchingPrompt(userQuery, formattedCandidates);

                // Create scope to resolve IClaudeApiService
                using var scope = _serviceScopeFactory.CreateScope();
                var claudeApiService = scope.ServiceProvider.GetRequiredService<IClaudeApiService>();

                // Call Claude for matching
                var matchResponse = await claudeApiService.GenerateDocumentComparisonAsync(matchPrompt);

                // Parse the AI response
                return parseIndicationMatchResponse(matchResponse, candidates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error matching user query to indications: {Query}", userQuery);

                // Fall back to simple matching
                return performSimpleIndicationMatching(candidates);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats candidate entries into a condensed string for the AI prompt.
        /// </summary>
        /// <param name="candidates">Pre-filtered candidates.</param>
        /// <returns>Formatted string for prompt inclusion.</returns>
        private string formatCandidatesForPrompt(List<IndicationReferenceEntry> candidates)
        {
            #region implementation

            var lines = new List<string>();

            foreach (var entry in candidates)
            {
                var productNameStr = string.Join(", ", entry.ProductNames.Take(3));
                var indicationTruncated = entry.IndicationsSummary.Length > 200
                    ? entry.IndicationsSummary.Substring(0, 200) + "..."
                    : entry.IndicationsSummary;

                // Remove markdown headers from indication text for prompt brevity
                indicationTruncated = indicationTruncated.Replace("# ", "").Replace("## ", "");

                lines.Add($"UNII: {entry.UNII} | Products: {productNameStr} | Indication: {indicationTruncated}");
            }

            return string.Join("\n", lines);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the AI prompt for indication matching (Stage 2).
        /// Loads the prompt template and substitutes placeholders.
        /// </summary>
        /// <param name="userQuery">The user's query text.</param>
        /// <param name="formattedCandidates">Formatted candidate list.</param>
        /// <returns>Formatted prompt for Claude AI.</returns>
        private string buildIndicationMatchingPrompt(string userQuery, string formattedCandidates)
        {
            #region implementation

            var template = loadPromptTemplate("Prompt-IndicationMatching", "ClaudeSearchService_IndicationMatching");

            if (template.Contains("not found") || template.Contains("configuration not found"))
            {
                _logger.LogError("[INDICATION MATCHING] Prompt template file not found. Ensure Prompt-IndicationMatching is configured in appsettings.json");
                throw new InvalidOperationException(
                    "Indication matching prompt template not found. Ensure 'Prompt-IndicationMatching' is configured in ClaudeApiSettings.");
            }

            return template
                .Replace("{{USER_QUERY}}", userQuery)
                .Replace("{{CANDIDATE_LIST}}", formattedCandidates);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the AI response for indication matching (Stage 2).
        /// Validates that returned UNIIs exist in the candidate list.
        /// </summary>
        /// <param name="aiResponse">Raw AI response string.</param>
        /// <param name="candidates">Original candidate list for UNII validation.</param>
        /// <returns>Parsed match result.</returns>
        private IndicationMatchResult parseIndicationMatchResponse(
            string aiResponse,
            List<IndicationReferenceEntry> candidates)
        {
            #region implementation

            try
            {
                var jsonContent = extractJsonFromResponse(aiResponse);

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogWarning("Could not extract JSON from AI response for indication matching");
                    return new IndicationMatchResult
                    {
                        Success = false,
                        Error = "Failed to parse AI response for indication matching."
                    };
                }

                var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonContent);

                if (parsed == null)
                {
                    return new IndicationMatchResult
                    {
                        Success = false,
                        Error = "Invalid AI response format."
                    };
                }

                var result = new IndicationMatchResult
                {
                    Success = parsed.success ?? false,
                    Explanation = parsed.explanation?.ToString()
                };

                // Build set of valid UNIIs from candidates
                var validUniis = new HashSet<string>(
                    candidates.Select(c => c.UNII),
                    StringComparer.OrdinalIgnoreCase);

                // Extract matched indications
                if (parsed.matchedIndications != null)
                {
                    foreach (var match in parsed.matchedIndications)
                    {
                        string unii = match.unii?.ToString() ?? "";

                        // Validate UNII exists in candidates (reject fabricated UNIIs)
                        if (!validUniis.Contains(unii))
                        {
                            _logger.LogWarning("[INDICATION MATCHING] Rejecting fabricated UNII: {UNII}", unii);
                            continue;
                        }

                        result.MatchedIndications.Add(new IndicationMatch
                        {
                            UNII = unii,
                            ProductNames = match.productNames?.ToString() ?? "",
                            RelevanceReason = match.relevanceReason?.ToString() ?? "",
                            Confidence = match.confidence?.ToString() ?? "low"
                        });
                    }
                }

                // Extract suggestions if present
                if (parsed.suggestions != null)
                {
                    result.Suggestions = new List<string>();
                    foreach (var suggestion in parsed.suggestions)
                    {
                        result.Suggestions.Add(suggestion.ToString());
                    }
                }

                result.Success = result.MatchedIndications.Count > 0;

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing indication match response");
                return new IndicationMatchResult
                {
                    Success = false,
                    Error = $"Failed to parse AI response: {ex.Message}"
                };
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Fallback: returns top candidates as matches when AI matching fails.
        /// </summary>
        /// <param name="candidates">Pre-filtered candidates from Stage 1.</param>
        /// <returns>Simple match result with low confidence.</returns>
        private IndicationMatchResult performSimpleIndicationMatching(
            List<IndicationReferenceEntry> candidates)
        {
            #region implementation

            _logger.LogDebug("Falling back to simple indication matching with {Count} candidates", candidates.Count);

            var matches = candidates
                .Take(10)
                .Select(c => new IndicationMatch
                {
                    UNII = c.UNII,
                    ProductNames = string.Join(", ", c.ProductNames.Take(3)),
                    RelevanceReason = "Matched by keyword (AI fallback)",
                    Confidence = "low"
                })
                .ToList();

            return new IndicationMatchResult
            {
                Success = matches.Count > 0,
                MatchedIndications = matches,
                Explanation = matches.Count > 0
                    ? $"Found {matches.Count} indication(s) through keyword matching (AI was unavailable)."
                    : "No indications matched using keyword matching.",
                Suggestions = matches.Count == 0
                    ? new List<string>
                    {
                        "Try using more specific medical terms",
                        "Try common condition names like 'diabetes', 'hypertension', 'depression'"
                    }
                    : null
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches for products by UNII using the existing data access layer.
        /// Returns product information with document GUIDs for label links.
        /// </summary>
        /// <param name="unii">The UNII code to search.</param>
        /// <param name="maxProducts">Maximum products to return.</param>
        /// <returns>List of product information for the given UNII.</returns>
        private async Task<List<IndicationProductInfo>> searchProductsByUniiAsync(
            string unii,
            int maxProducts)
        {
            #region implementation

            try
            {
                _logger.LogDebug("Searching products for UNII: {UNII}", unii);

                var products = await DtoLabelAccess.GetProductLatestLabelsAsync(
                    _dbContext,
                    unii: unii,
                    productNameSearch: null,
                    activeIngredientSearch: null,
                    _pkEncryptionSecret,
                    _logger,
                    page: 1,
                    size: maxProducts);

                return products.Select(p => new IndicationProductInfo
                {
                    ProductName = p.ProductName ?? "Unknown Product",
                    DocumentGuid = p.DocumentGUID?.ToString(),
                    UNII = p.UNII,
                    ActiveIngredient = p.ActiveIngredient,
                    LabelerName = p.ProductLatestLabel.TryGetValue("LabelerName", out var labeler)
                        ? labeler?.ToString()
                        : null
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products for UNII: {UNII}", unii);
                return new List<IndicationProductInfo>();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 3: Validates indication matches against actual FDA label text.
        /// Fetches the Indications &amp; Usage section (LOINC 34067-9) for each product
        /// and asks Claude to confirm whether the product genuinely treats the condition.
        /// </summary>
        /// <param name="userQuery">Original user query.</param>
        /// <param name="entries">Products with DocumentGUIDs to validate.</param>
        /// <returns>Validation result with confirmed/rejected verdicts.</returns>
        private async Task<IndicationValidationResult> validateIndicationMatchesAsync(
            string userQuery,
            List<IndicationValidationEntry> entries)
        {
            #region implementation

            if (entries.Count == 0)
            {
                return new IndicationValidationResult
                {
                    Success = true,
                    ValidatedMatches = new List<ValidatedIndication>()
                };
            }

            try
            {
                // Fetch actual Indications & Usage section text for each entry
                foreach (var entry in entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.DocumentGuid))
                        continue;

                    try
                    {
                        if (Guid.TryParse(entry.DocumentGuid, out var docGuid))
                        {
                            var sections = await DtoLabelAccess.GetLabelSectionMarkdownAsync(
                                _dbContext,
                                docGuid,
                                _pkEncryptionSecret,
                                _logger,
                                sectionCode: "34067-9"); // Indications & Usage LOINC code

                            if (sections != null && sections.Count > 0)
                            {
                                // Get first section's text, cap at 500 chars
                                var sectionDict = sections[0].LabelSectionMarkdown;
                                if (sectionDict != null && sectionDict.TryGetValue("FullSectionText", out var fullText))
                                {
                                    var text = fullText?.ToString() ?? "";
                                    entry.IndicationText = text.Length > 500
                                        ? text.Substring(0, 500)
                                        : text;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch indication section for DocumentGUID: {DocGuid}", entry.DocumentGuid);
                        // Leave IndicationText empty — will be marked as "unverified"
                    }
                }

                // Build validation prompt
                var formattedEntries = formatValidationEntriesForPrompt(entries);
                var validationPrompt = buildIndicationValidationPrompt(userQuery, formattedEntries);

                // Call Claude for validation
                using var scope = _serviceScopeFactory.CreateScope();
                var claudeApiService = scope.ServiceProvider.GetRequiredService<IClaudeApiService>();
                var validationResponse = await claudeApiService.GenerateDocumentComparisonAsync(validationPrompt);

                // Parse validation response
                return parseIndicationValidationResponse(validationResponse);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stage 3 validation failed — keeping all Stage 2 matches unfiltered");

                // Graceful degradation: keep all matches if validation fails
                return new IndicationValidationResult
                {
                    Success = true,
                    ValidatedMatches = entries.Select(e => new ValidatedIndication
                    {
                        UNII = e.UNII,
                        ProductName = e.ProductName,
                        Confirmed = true,
                        ValidationReason = "Validation unavailable — matched by indication reference data",
                        Confidence = "unverified"
                    }).ToList(),
                    Explanation = "Validation was skipped due to an error. All Stage 2 matches are included."
                };
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats validation entries for the Stage 3 prompt.
        /// </summary>
        /// <param name="entries">Validation entries with indication text.</param>
        /// <returns>Formatted string for prompt inclusion.</returns>
        private string formatValidationEntriesForPrompt(List<IndicationValidationEntry> entries)
        {
            #region implementation

            var lines = new List<string>();

            foreach (var entry in entries)
            {
                var indicationText = string.IsNullOrWhiteSpace(entry.IndicationText)
                    ? "(Indication text not available)"
                    : entry.IndicationText;

                lines.Add($"UNII: {entry.UNII} | Product: {entry.ProductName} | Indication Text: {indicationText}");
            }

            return string.Join("\n", lines);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the Stage 3 validation prompt.
        /// </summary>
        /// <param name="userQuery">Original user query.</param>
        /// <param name="formattedEntries">Formatted validation entries.</param>
        /// <returns>Formatted prompt for Claude AI.</returns>
        private string buildIndicationValidationPrompt(string userQuery, string formattedEntries)
        {
            #region implementation

            var template = loadPromptTemplate("Prompt-IndicationValidation", "ClaudeSearchService_IndicationValidation");

            if (template.Contains("not found") || template.Contains("configuration not found"))
            {
                _logger.LogError("[INDICATION VALIDATION] Prompt template file not found. Ensure Prompt-IndicationValidation is configured in appsettings.json");
                throw new InvalidOperationException(
                    "Indication validation prompt template not found. Ensure 'Prompt-IndicationValidation' is configured in ClaudeApiSettings.");
            }

            return template
                .Replace("{{USER_QUERY}}", userQuery)
                .Replace("{{VALIDATION_ENTRIES}}", formattedEntries);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the Stage 3 validation response from Claude.
        /// </summary>
        /// <param name="aiResponse">Raw AI response string.</param>
        /// <returns>Parsed validation result.</returns>
        private IndicationValidationResult parseIndicationValidationResponse(string aiResponse)
        {
            #region implementation

            try
            {
                var jsonContent = extractJsonFromResponse(aiResponse);

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogWarning("Could not extract JSON from validation response");
                    return new IndicationValidationResult { Success = false };
                }

                var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonContent);

                if (parsed == null)
                {
                    return new IndicationValidationResult { Success = false };
                }

                var result = new IndicationValidationResult
                {
                    Success = parsed.success ?? false,
                    Explanation = parsed.explanation?.ToString()
                };

                if (parsed.validatedMatches != null)
                {
                    foreach (var match in parsed.validatedMatches)
                    {
                        result.ValidatedMatches.Add(new ValidatedIndication
                        {
                            UNII = match.unii?.ToString() ?? "",
                            ProductName = match.productName?.ToString() ?? "",
                            Confirmed = match.confirmed ?? false,
                            ValidationReason = match.validationReason?.ToString() ?? "",
                            Confidence = match.confidence?.ToString() ?? "low"
                        });
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing indication validation response");
                return new IndicationValidationResult { Success = false };
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Looks up products for each matched indication UNII, attaches indication
        /// summaries from reference data, and builds validation entries for Stage 3.
        /// </summary>
        /// <remarks>
        /// For each matched UNII, calls <see cref="searchProductsByUniiAsync"/> to
        /// retrieve products, enriches them with the indication summary from reference
        /// data (truncated to 300 chars), and identifies the first product with a
        /// DocumentGuid to serve as the validation representative for that UNII.
        /// </remarks>
        /// <param name="matchResult">Stage 2 AI match results containing matched UNIIs.</param>
        /// <param name="candidates">Reference data candidates for indication summary lookup.</param>
        /// <param name="result">The search result to populate with ProductsByIndication.</param>
        /// <param name="maxProductsPerIndication">Maximum products to return per UNII.</param>
        /// <returns>
        /// A tuple of all products found and validation entries for Stage 3.
        /// </returns>
        /// <seealso cref="searchProductsByUniiAsync"/>
        /// <seealso cref="IndicationValidationEntry"/>
        private async Task<(List<IndicationProductInfo> allProducts, List<IndicationValidationEntry> validationEntries)>
            lookupProductsForMatchedIndicationsAsync(
                IndicationMatchResult matchResult,
                List<IndicationReferenceEntry> candidates,
                IndicationSearchResult result,
                int maxProductsPerIndication)
        {
            #region implementation

            var allProducts = new List<IndicationProductInfo>();
            var validationEntries = new List<IndicationValidationEntry>();

            // Build a lookup for indication summaries from reference data
            var indicationLookup = candidates.ToDictionary(c => c.UNII, c => c, StringComparer.OrdinalIgnoreCase);

            foreach (var match in matchResult.MatchedIndications)
            {
                var products = await searchProductsByUniiAsync(match.UNII, maxProductsPerIndication);

                if (products.Count > 0)
                {
                    // Attach indication summary from reference data
                    if (indicationLookup.TryGetValue(match.UNII, out var refEntry))
                    {
                        foreach (var product in products)
                        {
                            product.IndicationSummary = refEntry.IndicationsSummary.Length > 300
                                ? refEntry.IndicationsSummary.Substring(0, 300) + "..."
                                : refEntry.IndicationsSummary;
                        }
                    }

                    // Build validation entries (one per unique UNII, using first product with DocumentGuid)
                    var productWithGuid = products.FirstOrDefault(p => !string.IsNullOrEmpty(p.DocumentGuid));
                    if (productWithGuid != null)
                    {
                        validationEntries.Add(new IndicationValidationEntry
                        {
                            UNII = match.UNII,
                            ProductName = productWithGuid.ProductName,
                            DocumentGuid = productWithGuid.DocumentGuid
                        });
                    }

                    result.ProductsByIndication[match.UNII] = products;
                    allProducts.AddRange(products);
                }
            }

            return (allProducts, validationEntries);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies Stage 3 AI validation to filter matched indications against
        /// actual FDA label Indications &amp; Usage section text.
        /// </summary>
        /// <remarks>
        /// Calls <see cref="validateIndicationMatchesAsync"/> to verify each matched
        /// UNII against its real label text. Confirmed matches receive validation
        /// metadata (reason and confidence). Rejected UNIIs are removed from
        /// <see cref="IndicationSearchResult.ProductsByIndication"/> and
        /// <see cref="IndicationSearchResult.MatchedIndications"/>.
        ///
        /// If validation fails entirely (API error, no results), all Stage 2 matches
        /// are kept unfiltered as a graceful degradation strategy.
        /// </remarks>
        /// <param name="userQuery">The original user search query.</param>
        /// <param name="validationEntries">Entries to validate (one per UNII with DocumentGuid).</param>
        /// <param name="allProducts">All products found during product lookup.</param>
        /// <param name="result">The search result to filter in place.</param>
        /// <returns>The filtered list of all products (only confirmed UNIIs).</returns>
        /// <seealso cref="validateIndicationMatchesAsync"/>
        /// <seealso cref="ValidatedIndication"/>
        private async Task<List<IndicationProductInfo>> applyValidationFilterAsync(
            string userQuery,
            List<IndicationValidationEntry> validationEntries,
            List<IndicationProductInfo> allProducts,
            IndicationSearchResult result)
        {
            #region implementation

            _logger.LogInformation("[INDICATION SEARCH] Stage 3: Validating {Count} matches against label text",
                validationEntries.Count);

            var validationResult = await validateIndicationMatchesAsync(userQuery, validationEntries);

            if (!validationResult.Success || validationResult.ValidatedMatches.Count == 0)
            {
                // Graceful degradation: keep all Stage 2 matches unfiltered
                return allProducts;
            }

            // Build set of confirmed UNIIs
            var confirmedUniis = new HashSet<string>(
                validationResult.ValidatedMatches
                    .Where(v => v.Confirmed)
                    .Select(v => v.UNII),
                StringComparer.OrdinalIgnoreCase);

            // Build validation reason lookup
            var validationLookup = validationResult.ValidatedMatches
                .ToDictionary(v => v.UNII, v => v, StringComparer.OrdinalIgnoreCase);

            // Filter out rejected UNIIs from results
            var filteredProducts = new Dictionary<string, List<IndicationProductInfo>>();
            var confirmedProducts = new List<IndicationProductInfo>();

            foreach (var kvp in result.ProductsByIndication)
            {
                if (confirmedUniis.Contains(kvp.Key))
                {
                    // Attach validation metadata
                    if (validationLookup.TryGetValue(kvp.Key, out var validation))
                    {
                        foreach (var product in kvp.Value)
                        {
                            product.ValidationReason = validation.ValidationReason;
                            product.ValidationConfidence = validation.Confidence;
                        }
                    }

                    filteredProducts[kvp.Key] = kvp.Value;
                    confirmedProducts.AddRange(kvp.Value);
                }
                else
                {
                    _logger.LogDebug("[INDICATION SEARCH] Stage 3 rejected UNII: {UNII}", kvp.Key);
                }
            }

            result.ProductsByIndication = filteredProducts;

            // Also filter MatchedIndications to only include confirmed
            result.MatchedIndications = result.MatchedIndications
                .Where(m => confirmedUniis.Contains(m.UNII))
                .ToList();

            return confirmedProducts;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds label links for all products that have a DocumentGuid,
        /// populating the <see cref="IndicationSearchResult.LabelLinks"/> dictionary.
        /// </summary>
        /// <remarks>
        /// Each link maps a display name ("View Full Label (ProductName)") to the
        /// relative API path for the original label document. Duplicate keys are
        /// skipped to avoid overwriting when multiple products share a name.
        /// </remarks>
        /// <param name="allProducts">All products to generate links for.</param>
        /// <param name="result">The search result to populate with label links.</param>
        private void buildLabelLinks(
            List<IndicationProductInfo> allProducts,
            IndicationSearchResult result)
        {
            #region implementation

            foreach (var product in allProducts.Where(p => !string.IsNullOrEmpty(p.DocumentGuid)))
            {
                var linkKey = $"View Full Label ({product.ProductName})";
                var linkValue = $"/api/Label/original/{product.DocumentGuid}/true";

                if (!result.LabelLinks.ContainsKey(linkKey))
                {
                    result.LabelLinks[linkKey] = linkValue;
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        /// <remarks>
        /// Orchestrates the complete three-stage indication search:
        /// 1. Load reference data and keyword pre-filter
        /// 2. AI matching on filtered candidates
        /// 3. Product lookup + AI validation against actual label text
        /// </remarks>
        public async Task<IndicationSearchResult> SearchByIndicationAsync(
            string userQuery,
            AiSystemContext? systemContext = null,
            int maxProductsPerIndication = 25)
        {
            #region implementation

            var result = new IndicationSearchResult
            {
                OriginalQuery = userQuery
            };

            // Input validation
            if (string.IsNullOrWhiteSpace(userQuery))
            {
                result.Success = false;
                result.Error = "Search query cannot be empty.";
                return result;
            }

            // Sanitize query: cap length, strip control characters
            userQuery = new string(userQuery.Take(500).Where(c => !char.IsControl(c) || c == ' ').ToArray()).Trim();

            _logger.LogInformation("[INDICATION SEARCH] Starting search for: {Query}", userQuery);

            try
            {
                // Stage 1: Load reference data and pre-filter by keyword
                var allEntries = await GetIndicationReferenceDataAsync();
                if (allEntries.Count == 0)
                {
                    result.Success = false;
                    result.Error = "Indication reference data not available.";
                    return result;
                }

                var candidates = preFilterIndicationsByKeyword(userQuery, allEntries);
                if (candidates.Count == 0)
                {
                    result.Success = false;
                    result.Error = "No indications matched your query terms.";
                    result.SuggestedFollowUps = new List<string>
                    {
                        "Try using medical terminology (e.g., 'hypertension' instead of 'high blood pressure')",
                        "Try broader terms (e.g., 'diabetes' instead of 'type 2 diabetes mellitus')",
                        "Search by drug name instead using search_drug_labels"
                    };
                    return result;
                }

                _logger.LogInformation("[INDICATION SEARCH] Stage 1: {Count} candidates from keyword pre-filter", candidates.Count);

                // Stage 2: AI matching
                var matchResult = await MatchUserQueryToIndicationsAsync(userQuery, candidates);
                if (!matchResult.Success || matchResult.MatchedIndications.Count == 0)
                {
                    result.Success = false;
                    result.Error = matchResult.Error ?? "No indications matched your query.";
                    result.Explanation = matchResult.Explanation;
                    result.SuggestedFollowUps = matchResult.Suggestions;
                    return result;
                }

                result.MatchedIndications = matchResult.MatchedIndications;
                _logger.LogInformation("[INDICATION SEARCH] Stage 2: {Count} matched indications from AI",
                    matchResult.MatchedIndications.Count);

                // Product lookup for each matched UNII
                var (allProducts, validationEntries) = await lookupProductsForMatchedIndicationsAsync(
                    matchResult, candidates, result, maxProductsPerIndication);

                // Stage 3: AI validation against actual label text
                if (validationEntries.Count > 0)
                {
                    allProducts = await applyValidationFilterAsync(userQuery, validationEntries, allProducts, result);
                }

                // Build label links and finalize result
                result.TotalProductCount = allProducts.Count;
                buildLabelLinks(allProducts, result);

                result.Success = result.TotalProductCount > 0;
                result.Explanation = matchResult.Explanation;
                result.SuggestedFollowUps = generateIndicationFollowUpSuggestions(result);

                _logger.LogInformation("[INDICATION SEARCH] Found {Count} products across {IndicationCount} indications",
                    result.TotalProductCount, result.ProductsByIndication.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during indication search for query: {Query}", userQuery);
                result.Success = false;
                result.Error = $"Search failed: {ex.Message}";
                return result;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates suggested follow-up queries for indication search results.
        /// </summary>
        /// <param name="result">The indication search result.</param>
        /// <returns>List of follow-up suggestions.</returns>
        private List<string> generateIndicationFollowUpSuggestions(IndicationSearchResult result)
        {
            #region implementation

            var suggestions = new List<string>();

            if (result.ProductsByIndication.Count > 0)
            {
                var firstProducts = result.ProductsByIndication.Values.First();
                if (firstProducts.Count > 0)
                {
                    var firstProduct = firstProducts.First();
                    suggestions.Add($"Tell me about the side effects of {firstProduct.ProductName}");
                    suggestions.Add($"What is the dosage for {firstProduct.ProductName}?");
                }

                suggestions.Add("What are the contraindications for these medications?");
            }

            if (suggestions.Count == 0)
            {
                suggestions.Add("Search for drugs by pharmacologic class");
                suggestions.Add("Search for products by ingredient name");
            }

            return suggestions;

            #endregion
        }

        #endregion
    }

    #endregion
}
