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
    }

    #endregion
}
