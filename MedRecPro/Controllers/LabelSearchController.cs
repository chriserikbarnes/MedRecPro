using MedRecPro.Controllers;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Mappers;
using MedRecPro.Models;
using MedRecPro.Models.Extensions;
using MedRecPro.Service;
using MedRecPro.Service.LabelQuery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Reflection;
using System.Security.Claims;
using static MedRecPro.Models.UserRole;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Handles search, lookup, and navigation Label endpoints while preserving the original Label route surface.
    /// </summary>
    /// <remarks>
    /// This controller owns read-only Label discovery endpoints and keeps the inherited Debug and Release route behavior through the Label feature convention.
    /// </remarks>
    /// <seealso cref="LabelFeatureControllerAttribute"/>
    /// <seealso cref="LabelController"/>
    [ApiController]
    [LabelFeatureController]
    [LabelFeatureSwaggerTag("Label Search")]
    public class LabelSearchController : ApiControllerBase
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Entity Framework database context used by label search and navigation endpoints.
        /// </summary>
        /// <seealso cref="ApplicationDbContext"/>
        private readonly ApplicationDbContext _dbContext;

        /**************************************************************/
        /// <summary>
        /// Logger instance for search endpoint diagnostics.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger<LabelSearchController> _logger;

        /**************************************************************/
        /// <summary>
        /// Secret key used for primary-key encryption during DTO projection.
        /// </summary>
        /// <seealso cref="DtoLabelAccess"/>
        private readonly string _pkEncryptionSecret;

        /**************************************************************/
        /// <summary>
        /// Service for Claude API context retrieval used by AI-assisted searches.
        /// </summary>
        /// <seealso cref="IClaudeApiService"/>
        private readonly IClaudeApiService _claudeApiService;

        /**************************************************************/
        /// <summary>
        /// Optional Claude search service for AI-assisted product, class, and indication search.
        /// </summary>
        /// <seealso cref="IClaudeSearchService"/>
        private readonly IClaudeSearchService? _claudeSearchService;

        /**************************************************************/
        /// <summary>Provides ingredient query operations for Label endpoints.</summary>
        private readonly IIngredientSearchService _ingredientSearchService;

        /**************************************************************/
        /// <summary>Provides pharmacologic-class query operations for Label endpoints.</summary>
        private readonly IPharmacologicClassSearchService _pharmacologicClassSearchService;

        /**************************************************************/
        /// <summary>Provides product and identifier query operations for Label endpoints.</summary>
        private readonly IProductSearchService _productSearchService;

        /**************************************************************/
        /// <summary>Provides label content query operations for Label endpoints.</summary>
        private readonly ILabelContentQueryService _labelContentQueryService;

        /**************************************************************/
        /// <summary>Provides label markdown query operations for Label endpoints.</summary>
        private readonly ILabelMarkdownService _labelMarkdownService;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="LabelSearchController"/> class.
        /// </summary>
        /// <param name="configuration">Configuration provider containing the primary-key encryption secret.</param>
        /// <param name="logger">Logger instance for search endpoint diagnostics.</param>
        /// <param name="applicationDbContext">Entity Framework database context for label read models.</param>
        /// <param name="claudeApiService">Claude API service used for AI context retrieval.</param>
        /// <param name="ingredientSearchService">Ingredient query service for Label endpoints.</param>
        /// <param name="pharmacologicClassSearchService">Pharmacologic-class query service for Label endpoints.</param>
        /// <param name="productSearchService">Product query service for Label endpoints.</param>
        /// <param name="labelContentQueryService">Label content query service for Label endpoints.</param>
        /// <param name="labelMarkdownService">Label markdown query service for Label endpoints.</param>
        /// <param name="claudeSearchService">Optional Claude search service used for AI-assisted search.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the primary-key encryption secret is missing.</exception>
        /// <seealso cref="LabelController"/>
        public LabelSearchController(
            IConfiguration configuration,
            ILogger<LabelSearchController> logger,
            ApplicationDbContext applicationDbContext,
            IClaudeApiService claudeApiService,
            IIngredientSearchService ingredientSearchService,
            IPharmacologicClassSearchService pharmacologicClassSearchService,
            IProductSearchService productSearchService,
            ILabelContentQueryService labelContentQueryService,
            ILabelMarkdownService labelMarkdownService,
            IClaudeSearchService? claudeSearchService = null)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(configuration);

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = applicationDbContext ?? throw new ArgumentNullException(nameof(applicationDbContext));
            _claudeApiService = claudeApiService ?? throw new ArgumentNullException(nameof(claudeApiService));
            _ingredientSearchService = ingredientSearchService ?? throw new ArgumentNullException(nameof(ingredientSearchService));
            _pharmacologicClassSearchService = pharmacologicClassSearchService ?? throw new ArgumentNullException(nameof(pharmacologicClassSearchService));
            _productSearchService = productSearchService ?? throw new ArgumentNullException(nameof(productSearchService));
            _labelContentQueryService = labelContentQueryService ?? throw new ArgumentNullException(nameof(labelContentQueryService));
            _labelMarkdownService = labelMarkdownService ?? throw new ArgumentNullException(nameof(labelMarkdownService));
            _claudeSearchService = claudeSearchService;
            _pkEncryptionSecret = configuration.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing or empty.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the current user ID from the HTTP context.
        /// This method should be called while the HTTP context is still available.
        /// </summary>
        /// <returns>The current user's ID if authenticated; otherwise, null.</returns>
        private long? getCurrentUserId()
        {
            try
            {
                if (HttpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    return ClaimHelper.GetUserIdFromClaims(HttpContext.User.Claims);
                }
                return null;
            }
            // Broad-catch allowlist: optional claim lookup must not prevent an otherwise anonymous search request.
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get current user ID from context");
                return null;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Gets the encrypted user ID for the current authenticated user.
        /// Used for building system context for AI-powered operations.
        /// </summary>
        /// <returns>Encrypted user ID string, or null if not authenticated.</returns>
        /// <seealso cref="StringCipher.Encrypt"/>
        private string? getEncryptedUserId()
        {
            #region implementation
            var userId = ClaimHelper.GetUserIdFromClaims(User.Claims);
            if (!userId.HasValue)
            {
                return null;
            }

            return StringCipher.Encrypt(
                userId.Value.ToString(),
                _pkEncryptionSecret,
                StringCipher.EncryptionStrength.Fast);
            #endregion
        }

        #region Application Number Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by regulatory application number (NDA, ANDA, BLA).
        /// Returns products sharing the same regulatory approval with navigation data.
        /// </summary>
        /// <param name="applicationNumber">
        /// The application number to search for (e.g., "NDA014526", "ANDA125669", "BLA103795").
        /// Supports flexible matching including exact match, prefix-only, and number-only searches.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// If provided, pageSize must also be provided for paging to apply.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// If provided, pageNumber must also be provided for paging to apply.
        /// </param>
        /// <returns>List of products matching the application number criteria with navigation data.</returns>
        /// <response code="200">Returns the list of products matching the application number.</response>
        /// <response code="400">If the applicationNumber is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/application-number/search?applicationNumber=NDA014526
        /// GET /api/Label/application-number/search?applicationNumber=ANDA&amp;pageNumber=1&amp;pageSize=25
        ///
        /// The search supports multiple matching strategies:
        /// - Exact match after normalization (e.g., "ANDA125669" == "ANDA125669")
        /// - Prefix-only search (e.g., "ANDA" matches all ANDA applications)
        /// - Number-only search (e.g., "125669" matches "ANDA125669")
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "LIPITOR",
        ///     "ApplicationNumber": "NDA020702",
        ///     "MarketingCategoryCode": "NDA"
        ///   }
        /// ]
        /// ```
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search for all products under NDA014526
        /// GET /api/Label/application-number/search?applicationNumber=NDA014526
        ///
        /// // Search for all ANDA products with pagination
        /// GET /api/Label/application-number/search?applicationNumber=ANDA&amp;pageNumber=1&amp;pageSize=50
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchByApplicationNumberAsync"/>
        /// <seealso cref="LabelView.ProductsByApplicationNumber"/>
        /// <seealso cref="Label.MarketingCategory"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("application-number/search")]
        [ProducesResponseType(typeof(IEnumerable<ProductsByApplicationNumberDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductsByApplicationNumberDto>>> SearchByApplicationNumber(
            [FromQuery] string applicationNumber,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate application number is provided
            if (string.IsNullOrWhiteSpace(applicationNumber))
            {
                return BadRequest("Application number is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching products by application number: {ApplicationNumber}, Page: {PageNumber}, Size: {PageSize}",
                    applicationNumber, pageNumber, pageSize);

                var results = await _productSearchService.SearchByApplicationNumberAsync(applicationNumber, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets aggregated summaries of application numbers with product and document counts.
        /// Useful for understanding the scope of regulatory approvals across the database.
        /// </summary>
        /// <param name="marketingCategory">
        /// Optional filter by marketing category code (e.g., "NDA", "ANDA", "BLA").
        /// If not provided, returns summaries for all marketing categories.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of application number summaries with aggregated counts.</returns>
        /// <response code="200">Returns the list of application number summaries.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/application-number/summaries
        /// GET /api/Label/application-number/summaries?marketingCategoryCode=NDA
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "ApplicationNumber": "NDA020702",
        ///     "MarketingCategoryCode": "NDA",
        ///     "ProductCount": 15,
        ///     "DocumentCount": 45
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by ProductCount in descending order.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get all application number summaries
        /// GET /api/Label/application-number/summaries
        ///
        /// // Get only NDA summaries
        /// GET /api/Label/application-number/summaries?marketingCategoryCode=NDA
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.GetApplicationNumberSummariesAsync"/>
        /// <seealso cref="LabelView.ApplicationNumberSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("application-number/summaries")]
        [ProducesResponseType(typeof(IEnumerable<ApplicationNumberSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ApplicationNumberSummaryDto>>> GetApplicationNumberSummaries(
            [FromQuery] string? marketingCategory,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting application number summaries. Category: {MarketingCategoryCode}, Page: {PageNumber}, Size: {PageSize}",
                    marketingCategory ?? "all", pageNumber, pageSize);

                var results = await _productSearchService.GetApplicationNumberSummariesAsync(marketingCategory, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        #endregion Application Number Navigation

        #region Pharmacologic Class Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by pharmacologic/therapeutic class using intelligent terminology matching.
        /// This endpoint solves the vocabulary mismatch problem where user queries (e.g., "beta blockers")
        /// differ from database class names (e.g., "Beta-Adrenergic Blockers [EPC]").
        /// </summary>
        /// <param name="query">
        /// Natural language query for AI-powered intelligent search.
        /// Examples: "beta blockers", "ACE inhibitors", "SSRIs", "statins"
        /// When provided, AI matches user terminology to actual database class names.
        /// </param>
        /// <param name="classNameSearch">
        /// Direct search term to match against pharmacologic class names.
        /// Supports partial matching for flexible searches. Used as fallback when AI is unavailable
        /// or when direct class name matching is preferred.
        /// </param>
        /// <param name="maxProductsPerClass">
        /// Maximum number of products to return per matched class when using AI search. Default is 1000.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve (used with classNameSearch).
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page (used with classNameSearch).
        /// </param>
        /// <returns>
        /// When using `query`: Returns a <see cref="PharmacologicClassSearchResult"/> with matched classes,
        /// products organized by class, and label links.
        /// When using `classNameSearch`: Returns list of products matching the therapeutic class criteria.
        /// </returns>
        /// <response code="200">
        /// When using `query`: Returns <see cref="PharmacologicClassSearchResult"/> with matched classes, products, and label links.
        /// When using `classNameSearch`: Returns <see cref="IEnumerable{ProductsByPharmacologicClassDto}"/> with raw product DTOs.
        /// </response>
        /// <response code="400">If neither query nor classNameSearch is provided, or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// **AI-Powered Search (Recommended):**
        ///
        /// GET /api/Label/pharmacologic-class/search?query=beta blockers
        ///
        /// **Workflow:**
        /// 1. Retrieves all pharmacologic classes from the database
        /// 2. Uses AI to match user terminology to actual database class names
        /// 3. Searches for products in each matched class
        /// 4. Returns consolidated results with label links
        ///
        /// **Example Response (AI Search):**
        /// ```json
        /// {
        ///   "success": true,
        ///   "originalQuery": "beta blockers",
        ///   "matchedClasses": ["Beta-Adrenergic Blockers [EPC]"],
        ///   "productsByClass": {
        ///     "Beta-Adrenergic Blockers [EPC]": [
        ///       {
        ///         "productName": "METOPROLOL TARTRATE",
        ///         "documentGuid": "abc-123-def",
        ///         "activeIngredient": "Metoprolol Tartrate"
        ///       }
        ///     ]
        ///   },
        ///   "totalProductCount": 47,
        ///   "labelLinks": {
        ///     "View Full Label (METOPROLOL TARTRATE)": "/api/Label/generate/abc-123-def/true"
        ///   }
        /// }
        /// ```
        ///
        /// **Direct Database Search (Legacy):**
        ///
        /// GET /api/Label/pharmacologic-class/search?classNameSearch=Beta-Blocker
        ///
        /// Response returns raw product DTOs ordered by PharmClassName, then ProductName.
        /// </remarks>
        /// <example>
        /// <code>
        /// // AI-powered search (recommended)
        /// GET /api/Label/pharmacologic-class/search?query=beta%20blockers
        /// GET /api/Label/pharmacologic-class/search?query=ACE%20inhibitors&amp;maxProductsPerClass=10
        ///
        /// // Direct database search (legacy)
        /// GET /api/Label/pharmacologic-class/search?classNameSearch=Beta-Blocker
        /// GET /api/Label/pharmacologic-class/search?classNameSearch=ACE&amp;pageNumber=1&amp;pageSize=25
        /// </code>
        /// </example>
        /// <seealso cref="IClaudeSearchService"/>
        /// <seealso cref="PharmacologicClassSearchResult"/>
        /// <seealso cref="DtoLabelAccess.SearchByPharmacologicClassAsync"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("pharmacologic-class/search")]
        [ProducesResponseType(typeof(PharmacologicClassSearchResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IEnumerable<ProductsByPharmacologicClassDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> SearchByPharmacologicClass(
            [FromQuery] string? query,
            [FromQuery] string? classNameSearch,
            [FromQuery] int maxProductsPerClass = 1000,
            [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null)
        {
            #region Input Validation

            // Validate that at least one search parameter is provided
            if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(classNameSearch))
            {
                return BadRequest("Either 'query' (for AI search) or 'classNameSearch' (for direct search) is required.");
            }

            // Validate paging parameters when using classNameSearch
            if (!string.IsNullOrWhiteSpace(classNameSearch))
            {
                var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
                if (pagingValidation != null)
                {
                    return pagingValidation;
                }
            }

            #endregion

            #region AI-Powered Search

            // If query parameter is provided, attempt AI-powered search first
            if (!string.IsNullOrWhiteSpace(query))
            {
                // Try AI-powered search if service is available
                if (_claudeSearchService != null)
                {
                    try
                    {
                        _logger.LogInformation("[Label] AI pharmacologic class search for: {Query}", query);

                        // Build system context
                        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
                        var userId = isAuthenticated ? getEncryptedUserId() : null;
                        var systemContext = await _claudeApiService.GetSystemContextAsync(isAuthenticated, userId);

                        // Execute the intelligent search
                        var result = await _claudeSearchService.SearchByUserQueryAsync(
                            query,
                            systemContext,
                            maxProductsPerClass);

                        return Ok(result);
                    }
                    catch (Exception ex)
                    {
                        // Log and fall through to legacy search
                        _logger.LogWarning(ex, "AI pharmacologic class search failed, falling back to database search: {Query}", query);
                    }
                }
                else
                {
                    _logger.LogDebug("AI search service unavailable, using database search for: {Query}", query);
                }

                // Fall back to using query as classNameSearch
                classNameSearch = query;
            }

            #endregion

            #region Database Search (Fallback/Legacy)

            try
            {
                _logger.LogInformation("Searching products by pharmacologic class: {ClassNameSearch}, Page: {PageNumber}, Size: {PageSize}",
                    classNameSearch, pageNumber, pageSize);

                var results = await _pharmacologicClassSearchService.SearchByPharmacologicClassAsync(classNameSearch!, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument for pharmacologic class search: {ClassNameSearch}", classNameSearch);
                return BadRequest("The pharmacologic class search request is invalid.");
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts drug/ingredient names from a natural language description using AI.
        /// Used for UNII resolution fallback when the interpret phase uses incorrect UNIIs
        /// but includes the correct product name in the endpoint description.
        /// </summary>
        /// <param name="description">
        /// The endpoint description containing product/ingredient information.
        /// Examples:
        /// - "Search for sevelamer - phosphate binder for CKD"
        /// - "Search for finerenone (Kerendia) - non-steroidal MRA"
        /// - "Get metformin products for type 2 diabetes"
        /// </param>
        /// <returns>
        /// A <see cref="ProductExtractionResult"/> containing the extracted product name(s),
        /// confidence level, and any brand-to-generic mappings applied.
        /// </returns>
        /// <response code="200">Returns the extraction result with product name(s).</response>
        /// <response code="400">If the description parameter is missing.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ## Purpose
        ///
        /// This endpoint bridges the gap between the AI interpret phase (which may use
        /// incorrect UNIIs from reference data) and the database. When a UNII-based
        /// product search returns empty, the frontend can:
        ///
        /// 1. Call this endpoint with the original description
        /// 2. Get the correct product name
        /// 3. Search by ingredient name to find the correct UNII
        /// 4. Retry the product search with the corrected UNII
        ///
        /// ## Request
        ///
        /// ```
        /// GET /api/Label/extract-product?description=Search+for+sevelamer+-+phosphate+binder+for+CKD
        /// ```
        ///
        /// ## Response (200)
        ///
        /// ```json
        /// {
        ///   "success": true,
        ///   "productNames": ["sevelamer"],
        ///   "primaryProductName": "sevelamer",
        ///   "confidence": "high",
        ///   "explanation": "Extracted 'sevelamer' from 'Search for sevelamer'",
        ///   "brandMappingApplied": false,
        ///   "originalBrandName": null
        /// }
        /// ```
        ///
        /// ## Response with Brand Mapping
        ///
        /// ```json
        /// {
        ///   "success": true,
        ///   "productNames": ["finerenone"],
        ///   "primaryProductName": "finerenone",
        ///   "confidence": "high",
        ///   "explanation": "Converted brand name Kerendia to generic finerenone",
        ///   "brandMappingApplied": true,
        ///   "originalBrandName": "Kerendia"
        /// }
        /// ```
        ///
        /// ## Error Response
        ///
        /// ```json
        /// {
        ///   "success": false,
        ///   "productNames": [],
        ///   "confidence": "low",
        ///   "error": "Could not extract product name from description"
        /// }
        /// ```
        /// </remarks>
        /// <example>
        /// <code>
        /// // Extract product from a description
        /// GET /api/Label/extract-product?description=Search+for+finerenone+(Kerendia)+-+MRA
        ///
        /// // Use in UNII fallback workflow
        /// // 1. Original query: /api/Label/product/latest?unii=WRONG_UNII → empty
        /// // 2. Extract product: /api/Label/extract-product?description=...
        /// // 3. Search ingredient: /api/Label/ingredient/advanced?substanceNameSearch=finerenone
        /// // 4. Get correct UNII: WPE1M7X0GA
        /// // 5. Retry: /api/Label/product/latest?unii=WPE1M7X0GA → success!
        /// </code>
        /// </example>
        /// <seealso cref="IClaudeSearchService.ExtractProductFromDescriptionAsync"/>
        /// <seealso cref="ProductExtractionResult"/>
        [HttpGet("extract-product")]
        [ProducesResponseType(typeof(ProductExtractionResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ProductExtractionResult>> ExtractProductFromDescription(
            [FromQuery] string description)
        {
            #region Input Validation

            if (string.IsNullOrWhiteSpace(description))
            {
                return BadRequest("The 'description' parameter is required.");
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("[EXTRACT PRODUCT] Extracting product from description: {Description}",
                    description.Length > 100 ? description.Substring(0, 100) + "..." : description);

                // Check if the search service is available
                if (_claudeSearchService == null)
                {
                    _logger.LogWarning("[EXTRACT PRODUCT] ClaudeSearchService not available, returning error");
                    return Ok(new ProductExtractionResult
                    {
                        Success = false,
                        Error = "Product extraction service not available"
                    });
                }

                // Call the service to extract the product name
                var result = await _claudeSearchService.ExtractProductFromDescriptionAsync(description);

                return Ok(result);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the pharmacologic class hierarchy showing parent-child relationships.
        /// Enables navigation through therapeutic classification levels.
        /// </summary>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of pharmacologic class hierarchy relationships.</returns>
        /// <response code="200">Returns the pharmacologic class hierarchy.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/pharmacologic-class/hierarchy
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "ParentClassID": "encrypted_string",
        ///     "ParentClassName": "Cardiovascular Agents",
        ///     "ChildClassID": "encrypted_string",
        ///     "ChildClassName": "Beta-Adrenergic Blockers"
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by ParentClassName, then ChildClassName.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetPharmacologicClassHierarchyAsync"/>
        /// <seealso cref="LabelView.PharmacologicClassHierarchy"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("pharmacologic-class/hierarchy")]
        [ProducesResponseType(typeof(IEnumerable<PharmacologicClassHierarchyViewDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<PharmacologicClassHierarchyViewDto>>> GetPharmacologicClassHierarchy(
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting pharmacologic class hierarchy. Page: {PageNumber}, Size: {PageSize}",
                    pageNumber, pageSize);

                var results = await _pharmacologicClassSearchService.GetPharmacologicClassHierarchyAsync(pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Lists all available pharmacologic classes with product counts.
        /// This endpoint is useful for browsing available drug categories and
        /// understanding the classification structure in the database.
        /// </summary>
        /// <param name="useAiCache">
        /// Optional. When true, uses AI service's cached summaries which are optimized
        /// for intelligent search operations. Default is false for backwards compatibility.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>
        /// A list of <see cref="PharmacologicClassSummaryDto"/> containing class names
        /// and product counts for classes that have associated products.
        /// </returns>
        /// <response code="200">Returns the list of pharmacologic classes.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/pharmacologic-class/summaries
        ///
        /// Returns all pharmacologic classes that have at least one associated product.
        /// Results are cached to optimize performance on repeated queries.
        ///
        /// **Example Response:**
        /// ```json
        /// [
        ///   {
        ///     "pharmClassName": "Beta-Adrenergic Blockers [EPC]",
        ///     "productCount": 47
        ///   },
        ///   {
        ///     "pharmClassName": "Angiotensin Converting Enzyme Inhibitors [EPC]",
        ///     "productCount": 32
        ///   }
        /// ]
        /// ```
        ///
        /// **Use Cases:**
        /// - Browse available drug categories before searching
        /// - Provide suggestions when no match is found in class search
        /// - Display classification hierarchy to users
        /// </remarks>
        /// <seealso cref="IClaudeSearchService"/>
        /// <seealso cref="DtoLabelAccess.GetPharmacologicClassSummariesAsync"/>
        /// <seealso cref="LabelView.PharmacologicClassSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("pharmacologic-class/summaries")]
        [ProducesResponseType(typeof(List<PharmacologicClassSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<PharmacologicClassSummaryDto>>> GetPharmacologicClassSummaries(
            [FromQuery] bool useAiCache = false,
            [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region AI Service (Cached Summaries)

            // Try AI service's cached summaries first if requested and available
            if (useAiCache && _claudeSearchService != null)
            {
                try
                {
                    _logger.LogDebug("[Label] Retrieving pharmacologic class summaries from AI cache");

                    var summaries = await _claudeSearchService.GetAllClassSummariesAsync();

                    // Apply pagination if requested
                    if (pageNumber.HasValue && pageSize.HasValue)
                    {
                        var skip = (pageNumber.Value - 1) * pageSize.Value;
                        var paged = summaries.Skip(skip).Take(pageSize.Value).ToList();
                        addPaginationHeaders(pageNumber, pageSize, paged.Count);
                        return Ok(paged);
                    }

                    return Ok(summaries);
                }
                catch (Exception ex)
                {
                    // Log and fall through to database query
                    _logger.LogWarning(ex, "AI cache retrieval failed, falling back to database query");
                }
            }

            #endregion

            #region Database Query (Default/Fallback)

            try
            {
                _logger.LogInformation("Getting pharmacologic class summaries. Page: {PageNumber}, Size: {PageSize}",
                    pageNumber, pageSize);

                var results = await _pharmacologicClassSearchService.GetPharmacologicClassSummariesAsync(pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        #endregion Pharmacologic Class Navigation

        #region Indication Navigation
        /**************************************************************/
        /// <summary>
        /// Searches for drugs by medical condition or indication using a three-stage
        /// AI-enhanced pipeline: keyword pre-filter, AI matching, and AI validation
        /// against actual FDA label indication text.
        /// </summary>
        /// <param name="query">
        /// Natural language condition query (e.g., "high blood pressure", "type 2 diabetes", "depression").
        /// </param>
        /// <param name="maxProductsPerIndication">
        /// Maximum number of products to return per matched indication. Default is 25.
        /// </param>
        /// <returns>
        /// An <see cref="IndicationSearchResult"/> containing matched products organized by UNII
        /// with label links, validation reasons, and follow-up suggestions.
        /// </returns>
        /// <remarks>
        /// ## Search Pipeline
        /// 1. **Stage 1 (Keyword Pre-Filter)**: Tokenizes the query, expands with medical synonyms,
        ///    and scores ~1,000 reference entries. No AI call. Caps at 50 candidates.
        /// 2. **Stage 2 (AI Matching)**: Sends candidates to Claude for semantic matching.
        ///    Returns ranked UNIIs with relevance reasoning.
        /// 3. **Stage 3 (AI Validation)**: Fetches actual FDA Indications &amp; Usage section text
        ///    and asks Claude to confirm each match. Filters out false positives.
        ///
        /// ## Examples
        /// - `GET /api/Label/indication/search?query=high+blood+pressure`
        /// - `GET /api/Label/indication/search?query=type+2+diabetes&amp;maxProductsPerIndication=10`
        /// </remarks>
        /// <seealso cref="IndicationSearchResult"/>
        /// <seealso cref="IClaudeSearchService.SearchByIndicationAsync"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("indication/search")]
        [ProducesResponseType(typeof(IndicationSearchResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> SearchByIndication(
            [FromQuery] string query,
            [FromQuery] int maxProductsPerIndication = 25)
        {
            #region Input Validation

            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("The 'query' parameter is required. Provide a medical condition (e.g., 'high blood pressure', 'diabetes', 'depression').");
            }

            #endregion

            #region AI-Powered Search

            if (_claudeSearchService == null)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "AI search service is not available. Indication search requires AI capabilities.");
            }

            try
            {
                _logger.LogInformation("[Label] AI indication search for: {Query}", query);

                // Build system context
                var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
                var userId = isAuthenticated ? getEncryptedUserId() : null;
                var systemContext = await _claudeApiService.GetSystemContextAsync(isAuthenticated, userId);

                // Execute the three-stage search
                var result = await _claudeSearchService.SearchByIndicationAsync(
                    query,
                    systemContext,
                    maxProductsPerIndication);

                return Ok(result);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }
        #endregion Indication Navigation

        #region Ingredient Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by ingredient UNII code or substance name.
        /// Enables drug composition queries and ingredient-based product discovery.
        /// </summary>
        /// <param name="unii">
        /// Optional UNII (Unique Ingredient Identifier) code to search for (e.g., "R16CO5Y76E" for aspirin).
        /// </param>
        /// <param name="substanceNameSearch">
        /// Optional substance name search term. Supports partial matching.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of products matching the ingredient criteria.</returns>
        /// <response code="200">Returns the list of products matching the ingredient.</response>
        /// <response code="400">If neither unii nor substanceNameSearch is provided, or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/ingredient/search?unii=R16CO5Y76E
        /// GET /api/Label/ingredient/search?substanceNameSearch=aspirin
        ///
        /// At least one of `unii` or `substanceNameSearch` must be provided.
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "BAYER ASPIRIN",
        ///     "UNII": "R16CO5Y76E",
        ///     "SubstanceName": "ASPIRIN"
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by SubstanceName, then ProductName.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search by UNII code
        /// GET /api/Label/ingredient/search?unii=R16CO5Y76E
        ///
        /// // Search by substance name
        /// GET /api/Label/ingredient/search?substanceNameSearch=aspirin
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchByIngredientAsync"/>
        /// <seealso cref="LabelView.ProductsByIngredient"/>
        /// <seealso cref="Label.Ingredient"/>
        /// <seealso cref="Label.IngredientSubstance"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/search")]
        [ProducesResponseType(typeof(IEnumerable<ProductsByIngredientDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductsByIngredientDto>>> SearchByIngredient(
            [FromQuery] string? unii,
            [FromQuery] string? substanceNameSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate at least one search parameter is provided
            if (string.IsNullOrWhiteSpace(unii) && string.IsNullOrWhiteSpace(substanceNameSearch))
            {
                return BadRequest("At least one search parameter (unii or substanceNameSearch) is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching products by ingredient. UNII: {UNII}, SubstanceName: {SubstanceName}, Page: {PageNumber}, Size: {PageSize}",
                    unii ?? "null", substanceNameSearch ?? "null", pageNumber, pageSize);

                var results = await _ingredientSearchService.SearchByIngredientAsync(unii, substanceNameSearch, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets ingredient summaries with product counts.
        /// Discover the most common ingredients across products in the database.
        /// </summary>
        /// <param name="ingredient">
        /// Optional. Filter by ingredient name (partial match on SubstanceName).
        /// Use this to vary results for AI skill discovery workflows.
        /// </param>
        /// <param name="minProductCount">
        /// Optional minimum product count filter. Only returns ingredients appearing in at least this many products.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of ingredient summaries with aggregated product counts.</returns>
        /// <response code="200">Returns the ingredient summaries.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/ingredient/summaries
        /// GET /api/Label/ingredient/summaries?minProductCount=10
        /// GET /api/Label/ingredient/summaries?ingredient=aspirin
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "UNII": "R16CO5Y76E",
        ///     "SubstanceName": "ASPIRIN",
        ///     "ProductCount": 250
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by ProductCount in descending order.
        /// The ingredient parameter enables varied results for paginated queries.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetIngredientSummariesAsync"/>
        /// <seealso cref="LabelView.IngredientSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/summaries")]
        [ProducesResponseType(typeof(IEnumerable<IngredientSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<IngredientSummaryDto>>> GetIngredientSummaries(
            [FromQuery] string? ingredient,
            [FromQuery] int? minProductCount,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate minProductCount if provided
            if (minProductCount.HasValue && minProductCount.Value < 0)
            {
                return BadRequest("Minimum product count cannot be negative.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting ingredient summaries. Ingredient: {Ingredient}, MinProductCount: {MinProductCount}, Page: {PageNumber}, Size: {PageSize}",
                    ingredient ?? "null", minProductCount, pageNumber, pageSize);

                var results = await _ingredientSearchService.GetIngredientSummariesAsync(minProductCount, ingredient, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets active ingredient summaries with product, document, and labeler counts.
        /// Discover the most common active ingredients across products in the database.
        /// </summary>
        /// <param name="ingredient">
        /// Optional. Filter by ingredient name (partial match on SubstanceName).
        /// Use this to vary results for AI skill discovery workflows.
        /// </param>
        /// <param name="minProductCount">
        /// Optional minimum product count filter. Only returns ingredients appearing in at least this many products.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of active ingredient summaries with aggregated counts.</returns>
        /// <response code="200">Returns the active ingredient summaries.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/ingredient/active/summaries
        /// GET /api/Label/ingredient/active/summaries?minProductCount=10
        /// GET /api/Label/ingredient/active/summaries?ingredient=aspirin
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedIngredientSubstanceID": "encrypted_string",
        ///     "UNII": "R16CO5Y76E",
        ///     "SubstanceName": "ASPIRIN",
        ///     "IngredientType": "activeIngredient",
        ///     "ProductCount": 250,
        ///     "DocumentCount": 180,
        ///     "LabelerCount": 45
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by ProductCount in descending order.
        /// The ingredient parameter enables varied results for paginated queries.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetIngredientActiveSummariesAsync"/>
        /// <seealso cref="LabelView.IngredientActiveSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/active/summaries")]
        [ProducesResponseType(typeof(IEnumerable<IngredientActiveSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<IngredientActiveSummaryDto>>> GetIngredientActiveSummaries(
            [FromQuery] string? ingredient,
            [FromQuery] int? minProductCount,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate minProductCount if provided
            if (minProductCount.HasValue && minProductCount.Value < 0)
            {
                return BadRequest("Minimum product count cannot be negative.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting active ingredient summaries. Ingredient: {Ingredient}, MinProductCount: {MinProductCount}, Page: {PageNumber}, Size: {PageSize}",
                    ingredient ?? "null", minProductCount, pageNumber, pageSize);

                var results = await _ingredientSearchService.GetIngredientActiveSummariesAsync(minProductCount, ingredient, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets inactive ingredient (excipient) summaries with product, document, and labeler counts.
        /// Discover the most common inactive ingredients across products in the database.
        /// </summary>
        /// <param name="ingredient">
        /// Optional. Filter by ingredient name (partial match on SubstanceName).
        /// Use this to vary results for AI skill discovery workflows.
        /// </param>
        /// <param name="minProductCount">
        /// Optional minimum product count filter. Only returns ingredients appearing in at least this many products.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of inactive ingredient summaries with aggregated counts.</returns>
        /// <response code="200">Returns the inactive ingredient summaries.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/ingredient/inactive/summaries
        /// GET /api/Label/ingredient/inactive/summaries?minProductCount=10
        /// GET /api/Label/ingredient/inactive/summaries?ingredient=starch
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedIngredientSubstanceID": "encrypted_string",
        ///     "UNII": "ETJ7Z6XBU4",
        ///     "SubstanceName": "SILICON DIOXIDE",
        ///     "IngredientType": "inactiveIngredient",
        ///     "ProductCount": 500,
        ///     "DocumentCount": 350,
        ///     "LabelerCount": 120
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by ProductCount in descending order.
        /// Inactive ingredients include excipients, fillers, binders, and other non-active substances.
        /// The ingredient parameter enables varied results for paginated queries.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetIngredientInactiveSummariesAsync"/>
        /// <seealso cref="LabelView.IngredientInactiveSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/inactive/summaries")]
        [ProducesResponseType(typeof(IEnumerable<IngredientInactiveSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<IngredientInactiveSummaryDto>>> GetIngredientInactiveSummaries(
            [FromQuery] string? ingredient,
            [FromQuery] int? minProductCount,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate minProductCount if provided
            if (minProductCount.HasValue && minProductCount.Value < 0)
            {
                return BadRequest("Minimum product count cannot be negative.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting inactive ingredient summaries. Ingredient: {Ingredient}, MinProductCount: {MinProductCount}, Page: {PageNumber}, Size: {PageSize}",
                    ingredient ?? "null", minProductCount, pageNumber, pageSize);

                var results = await _ingredientSearchService.GetIngredientInactiveSummariesAsync(minProductCount, ingredient, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Advanced ingredient search with application number filtering, document linkage, and product name matching.
        /// Uses the new vw_Ingredients, vw_ActiveIngredients, and vw_InactiveIngredients views.
        /// </summary>
        /// <param name="unii">
        /// Optional. FDA UNII code for exact ingredient match.
        /// </param>
        /// <param name="substanceNameSearch">
        /// Optional. Substance name for partial/phonetic matching (tolerates misspellings).
        /// </param>
        /// <param name="applicationNumber">
        /// Optional. Application number (e.g., NDA020702, 020702) for filtering by regulatory approval.
        /// </param>
        /// <param name="applicationType">
        /// Optional. Application type filter (NDA, ANDA, BLA).
        /// </param>
        /// <param name="productNameSearch">
        /// Optional. Product name for partial/phonetic matching (tolerates misspellings).
        /// </param>
        /// <param name="activeOnly">
        /// Optional. Filter by ingredient type: true = active only, false = inactive only, null = all.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of ingredient view results with document linkage.</returns>
        /// <response code="200">Returns the list of ingredients matching the criteria.</response>
        /// <response code="400">If no search criteria provided or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Enhanced Ingredient Search
        ///
        /// This endpoint provides advanced search capabilities beyond the basic `/ingredient/search`:
        ///
        /// - **Application Number filtering**: Find ingredients by NDA, ANDA, or BLA number
        /// - **Document linkage**: Results include DocumentGUID for direct label retrieval
        /// - **Product name search**: Find ingredients by product name with phonetic matching
        /// - **Active/Inactive filtering**: Separate search for active vs inactive ingredients
        ///
        /// ### Examples
        ///
        /// ```
        /// GET /api/Label/ingredient/advanced?unii=R16CO5Y76E
        /// GET /api/Label/ingredient/advanced?substanceNameSearch=aspirin&amp;applicationNumber=020702
        /// GET /api/Label/ingredient/advanced?productNameSearch=TYLENOL&amp;activeOnly=true
        /// GET /api/Label/ingredient/advanced?applicationType=ANDA&amp;activeOnly=false
        /// ```
        ///
        /// ### Response
        ///
        /// ```json
        /// [
        ///   {
        ///     "IngredientView": {
        ///       "DocumentGUID": "12345678-1234-1234-1234-123456789012",
        ///       "ProductName": "TYLENOL",
        ///       "SubstanceName": "ACETAMINOPHEN",
        ///       "UNII": "362O9ITL9D",
        ///       "ApplicationType": "NDA",
        ///       "ApplicationNumber": "019872",
        ///       "ClassCode": "ACTIM"
        ///     }
        ///   }
        /// ]
        /// ```
        ///
        /// Use `DocumentGUID` with `/api/label/single/{documentGuid}` to retrieve the full label.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.SearchIngredientsAdvancedAsync"/>
        /// <seealso cref="LabelView.IngredientView"/>
        /// <seealso cref="LabelView.ActiveIngredientView"/>
        /// <seealso cref="LabelView.InactiveIngredientView"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/advanced")]
        [ProducesResponseType(typeof(IEnumerable<IngredientViewDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<IngredientViewDto>>> SearchIngredientsAdvanced(
            [FromQuery] string? unii,
            [FromQuery] string? substanceNameSearch,
            [FromQuery] string? applicationNumber,
            [FromQuery] string? applicationType,
            [FromQuery] string? productNameSearch,
            [FromQuery] bool? activeOnly,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate at least one search parameter is provided
            if (string.IsNullOrWhiteSpace(unii) &&
                string.IsNullOrWhiteSpace(substanceNameSearch) &&
                string.IsNullOrWhiteSpace(applicationNumber) &&
                string.IsNullOrWhiteSpace(applicationType) &&
                string.IsNullOrWhiteSpace(productNameSearch))
            {
                return BadRequest("At least one search parameter (unii, substanceNameSearch, applicationNumber, applicationType, or productNameSearch) is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Advanced ingredient search. UNII: {UNII}, SubstanceName: {SubstanceName}, AppNum: {AppNum}, AppType: {AppType}, ProductName: {ProductName}, ActiveOnly: {ActiveOnly}",
                    unii ?? "null", substanceNameSearch ?? "null", applicationNumber ?? "null", applicationType ?? "null", productNameSearch ?? "null", activeOnly?.ToString() ?? "null");

                var results = await _ingredientSearchService.SearchIngredientsAdvancedAsync(unii, substanceNameSearch, applicationNumber, applicationType, productNameSearch, activeOnly, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds products that share the same active ingredient as a specified application number.
        /// Useful for finding generic equivalents or related brand products.
        /// </summary>
        /// <param name="applicationNumber">
        /// The application number to search (e.g., NDA020702, 020702).
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of products containing the same active ingredients.</returns>
        /// <response code="200">Returns the list of products with the same active ingredients.</response>
        /// <response code="400">If applicationNumber is not provided or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Find Products by Application Number with Same Ingredient
        ///
        /// This endpoint finds all products that contain the same active ingredient(s) as the specified application number.
        ///
        /// **Use Case:** Given an NDA or ANDA number, find all generic and brand products with the same active ingredient.
        ///
        /// ### Example
        ///
        /// ```
        /// GET /api/Label/ingredient/by-application?applicationNumber=020702
        /// ```
        ///
        /// This will:
        /// 1. Find the active ingredients for application number 020702
        /// 2. Return all products containing those same active ingredients
        ///
        /// ### Response
        ///
        /// ```json
        /// [
        ///   {
        ///     "IngredientView": {
        ///       "DocumentGUID": "12345678-1234-1234-1234-123456789012",
        ///       "ProductName": "LIPITOR",
        ///       "SubstanceName": "ATORVASTATIN CALCIUM",
        ///       "ApplicationNumber": "020702",
        ///       "ApplicationType": "NDA"
        ///     }
        ///   },
        ///   {
        ///     "IngredientView": {
        ///       "DocumentGUID": "87654321-4321-4321-4321-210987654321",
        ///       "ProductName": "ATORVASTATIN CALCIUM TABLETS",
        ///       "SubstanceName": "ATORVASTATIN CALCIUM",
        ///       "ApplicationNumber": "078456",
        ///       "ApplicationType": "ANDA"
        ///     }
        ///   }
        /// ]
        /// ```
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.FindProductsByApplicationNumberWithSameIngredientAsync"/>
        /// <seealso cref="LabelView.ActiveIngredientView"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/by-application")]
        [ProducesResponseType(typeof(IEnumerable<IngredientViewDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<IngredientViewDto>>> SearchIngredientByApplicationNumber(
            [FromQuery] string applicationNumber,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate application number is provided
            if (string.IsNullOrWhiteSpace(applicationNumber))
            {
                return BadRequest("Application number is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Finding products by application number with same ingredient. ApplicationNumber: {ApplicationNumber}",
                    applicationNumber);

                var results = await _ingredientSearchService.FindProductsByApplicationNumberWithSameIngredientAsync(applicationNumber, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets related ingredients for a specified ingredient.
        /// Given an ingredient (by UNII or name), finds all products containing it and their other ingredients.
        /// </summary>
        /// <param name="unii">
        /// Optional. FDA UNII code to search.
        /// </param>
        /// <param name="substanceNameSearch">
        /// Optional. Substance name for partial matching.
        /// </param>
        /// <param name="isActive">
        /// Optional. True if searching for an active ingredient, false for inactive. Default is true.
        /// </param>
        /// <returns>Related ingredient results including searched, related active, inactive, and products.</returns>
        /// <response code="200">Returns the related ingredient results.</response>
        /// <response code="400">If neither unii nor substanceNameSearch is provided.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Get Related Ingredients
        ///
        /// This endpoint finds all products containing a specified ingredient and returns:
        /// - **SearchedIngredients**: The ingredients that matched the search criteria
        /// - **RelatedActiveIngredients**: All active ingredients in those products
        /// - **RelatedInactiveIngredients**: All inactive ingredients (excipients) in those products
        /// - **RelatedProducts**: Summary of unique products found
        ///
        /// **Use Case:** Given an active ingredient, find all products containing it and their inactive ingredients.
        ///
        /// ### Examples
        ///
        /// ```
        /// GET /api/Label/ingredient/related?unii=R16CO5Y76E&amp;isActive=true
        /// GET /api/Label/ingredient/related?substanceNameSearch=aspirin&amp;isActive=true
        /// GET /api/Label/ingredient/related?substanceNameSearch=silicon dioxide&amp;isActive=false
        /// ```
        ///
        /// ### Response
        ///
        /// ```json
        /// {
        ///   "searchedIngredients": [...],
        ///   "relatedActiveIngredients": [...],
        ///   "relatedInactiveIngredients": [...],
        ///   "relatedProducts": [...],
        ///   "totalActiveCount": 5,
        ///   "totalInactiveCount": 25,
        ///   "totalProductCount": 10
        /// }
        /// ```
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.FindRelatedIngredientsAsync"/>
        /// <seealso cref="IngredientRelatedResultsDto"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/related")]
        [ProducesResponseType(typeof(IngredientRelatedResultsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IngredientRelatedResultsDto>> GetRelatedIngredients(
            [FromQuery] string? unii,
            [FromQuery] string? substanceNameSearch,
            [FromQuery] bool? isActive)
        {
            #region Input Validation

            // Validate at least one search parameter is provided
            if (string.IsNullOrWhiteSpace(unii) && string.IsNullOrWhiteSpace(substanceNameSearch))
            {
                return BadRequest("At least one search parameter (unii or substanceNameSearch) is required.");
            }

            #endregion

            #region Implementation

            try
            {
                // Default to searching for active ingredients
                bool searchingActive = isActive ?? true;

                _logger.LogInformation("Finding related ingredients. UNII: {UNII}, SubstanceName: {SubstanceName}, IsActive: {IsActive}",
                    unii ?? "null", substanceNameSearch ?? "null", searchingActive);

                var results = await _ingredientSearchService.FindRelatedIngredientsAsync(unii, substanceNameSearch, searchingActive);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        #endregion Ingredient Navigation

        #region Product Identifier Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by NDC (National Drug Code) or other product identifiers.
        /// Critical for pharmacy system integration and product lookup by code.
        /// </summary>
        /// <param name="productCode">
        /// The NDC or product code to search for (e.g., "12345-678-90").
        /// Supports partial matching for flexible searches.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of products matching the product code criteria.</returns>
        /// <response code="200">Returns the list of products matching the NDC code.</response>
        /// <response code="400">If productCode is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/ndc/search?productCode=12345-678
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "LIPITOR",
        ///     "ProductCode": "12345-678-90",
        ///     "LabelerName": "PFIZER INC"
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by ProductCode.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search by full NDC
        /// GET /api/Label/ndc/search?productCode=12345-678-90
        ///
        /// // Search by partial NDC
        /// GET /api/Label/ndc/search?productCode=12345
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchByNDCAsync"/>
        /// <seealso cref="LabelView.ProductsByNDC"/>
        /// <seealso cref="Label.ProductIdentifier"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ndc/search")]
        [ProducesResponseType(typeof(IEnumerable<ProductsByNDCDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductsByNDCDto>>> SearchByNDC(
            [FromQuery] string productCode,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate product code is provided
            if (string.IsNullOrWhiteSpace(productCode))
            {
                return BadRequest("Product code (NDC) is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching products by NDC: {ProductCode}, Page: {PageNumber}, Size: {PageSize}",
                    productCode, pageNumber, pageSize);

                var results = await _productSearchService.SearchByNDCAsync(productCode, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches package configurations by NDC package code.
        /// Shows packaging hierarchy and quantities for specific package codes.
        /// </summary>
        /// <param name="packageCode">
        /// The NDC package code to search for.
        /// Supports partial matching for flexible searches.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of package configurations matching the package code.</returns>
        /// <response code="200">Returns the list of packages matching the code.</response>
        /// <response code="400">If packageCode is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/ndc/package/search?packageCode=12345-678-90
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedPackagingLevelID": "encrypted_string",
        ///     "PackageCode": "12345-678-90",
        ///     "PackageDescription": "100 TABLETS in 1 BOTTLE",
        ///     "PackageQuantity": 100
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by PackageCode.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.SearchByPackageNDCAsync"/>
        /// <seealso cref="LabelView.PackageByNDC"/>
        /// <seealso cref="Label.PackageIdentifier"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ndc/package/search")]
        [ProducesResponseType(typeof(IEnumerable<PackageByNDCDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<PackageByNDCDto>>> SearchByPackageNDC(
            [FromQuery] string packageCode,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate package code is provided
            if (string.IsNullOrWhiteSpace(packageCode))
            {
                return BadRequest("Package code is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching packages by NDC: {PackageCode}, Page: {PageNumber}, Size: {PageSize}",
                    packageCode, pageNumber, pageSize);

                var results = await _productSearchService.SearchByPackageNDCAsync(packageCode, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        #endregion Product Identifier Navigation

        #region Organization Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by labeler (marketing organization) name.
        /// Lists products associated with a specific pharmaceutical company or distributor.
        /// </summary>
        /// <param name="labelerNameSearch">
        /// Search term to match against labeler names (e.g., "Pfizer", "Johnson").
        /// Supports partial matching for flexible searches.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of products matching the labeler name criteria.</returns>
        /// <response code="200">Returns the list of products by labeler.</response>
        /// <response code="400">If labelerNameSearch is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/labeler/search?labelerNameSearch=Pfizer
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "LIPITOR",
        ///     "LabelerName": "PFIZER INC",
        ///     "LabelerDUNS": "123456789"
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by LabelerName, then ProductName.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search for Pfizer products
        /// GET /api/Label/labeler/search?labelerNameSearch=Pfizer
        ///
        /// // Search with pagination
        /// GET /api/Label/labeler/search?labelerNameSearch=Johnson&amp;pageNumber=1&amp;pageSize=50
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchByLabelerAsync"/>
        /// <seealso cref="LabelView.ProductsByLabeler"/>
        /// <seealso cref="Label.Organization"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("labeler/search")]
        [ProducesResponseType(typeof(IEnumerable<ProductsByLabelerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductsByLabelerDto>>> SearchByLabeler(
            [FromQuery] string labelerNameSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate labeler name search term is provided
            if (string.IsNullOrWhiteSpace(labelerNameSearch))
            {
                return BadRequest("Labeler name search term is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching products by labeler: {LabelerNameSearch}, Page: {PageNumber}, Size: {PageSize}",
                    labelerNameSearch, pageNumber, pageSize);

                var results = await _productSearchService.SearchByLabelerAsync(labelerNameSearch, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets labeler (marketing organization) summaries with product counts.
        /// Discover which pharmaceutical companies have the most products in the database.
        /// </summary>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of labeler summaries with aggregated product counts.</returns>
        /// <response code="200">Returns the labeler summaries.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/labeler/summaries
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "LabelerName": "PFIZER INC",
        ///     "LabelerDUNS": "123456789",
        ///     "ProductCount": 500
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by ProductCount in descending order.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetLabelerSummariesAsync"/>
        /// <seealso cref="LabelView.LabelerSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("labeler/summaries")]
        [ProducesResponseType(typeof(IEnumerable<LabelerSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<LabelerSummaryDto>>> GetLabelerSummaries(
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting labeler summaries. Page: {PageNumber}, Size: {PageSize}",
                    pageNumber, pageSize);

                var results = await _productSearchService.GetLabelerSummariesAsync(pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        #endregion Organization Navigation

        #region Section Navigation

        /**************************************************************/
        /// <summary>
        /// Searches sections by LOINC section code.
        /// Enables navigation to specific labeling sections across documents.
        /// </summary>
        /// <param name="sectionCode">
        /// The LOINC section code to search for (e.g., "34066-1" for Boxed Warning, "34067-9" for Indications).
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of sections matching the section code.</returns>
        /// <response code="200">Returns the list of sections matching the code.</response>
        /// <response code="400">If sectionCode is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/section/search?sectionCode=34066-1
        ///
        /// Common LOINC section codes:
        /// - 34066-1: Boxed Warning
        /// - 34067-9: Indications and Usage
        /// - 34068-7: Dosage and Administration
        /// - 34069-5: How Supplied
        /// - 34070-3: Contraindications
        /// - 34071-1: Warnings
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedSectionID": "encrypted_string",
        ///     "SectionCode": "34066-1",
        ///     "SectionTitle": "BOXED WARNING",
        ///     "DocumentTitle": "LIPITOR Prescribing Information"
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by DocumentTitle.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search for all boxed warnings
        /// GET /api/Label/section/search?sectionCode=34066-1
        ///
        /// // Search for indications with pagination
        /// GET /api/Label/section/search?sectionCode=34067-9&amp;pageNumber=1&amp;pageSize=50
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchBySectionCodeAsync"/>
        /// <seealso cref="LabelView.SectionNavigation"/>
        /// <seealso cref="Label.Section"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("section/search")]
        [ProducesResponseType(typeof(IEnumerable<SectionNavigationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<SectionNavigationDto>>> SearchBySectionCode(
            [FromQuery] string sectionCode,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate section code is provided
            if (string.IsNullOrWhiteSpace(sectionCode))
            {
                return BadRequest("Section code (LOINC) is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching sections by code: {SectionCode}, Page: {PageNumber}, Size: {PageSize}",
                    sectionCode, pageNumber, pageSize);

                var results = await _labelContentQueryService.SearchBySectionCodeAsync(sectionCode, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets section type summaries with document counts.
        /// Discover which section types (LOINC codes) are most common across all documents.
        /// </summary>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of section type summaries with aggregated document counts.</returns>
        /// <response code="200">Returns the section type summaries.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/section/summaries
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "SectionCode": "34067-9",
        ///     "SectionTitle": "INDICATIONS AND USAGE",
        ///     "DocumentCount": 15000
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by DocumentCount in descending order.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetSectionTypeSummariesAsync"/>
        /// <seealso cref="LabelView.SectionTypeSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("section/summaries")]
        [ProducesResponseType(typeof(IEnumerable<SectionTypeSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<SectionTypeSummaryDto>>> GetSectionTypeSummaries(
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting section type summaries. Page: {PageNumber}, Size: {PageSize}",
                    pageNumber, pageSize);

                var results = await _labelContentQueryService.GetSectionTypeSummariesAsync(pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets section text content for AI summarization workflows.
        /// Provides efficient text retrieval from document sections.
        /// </summary>
        /// <param name="documentGuid">
        /// Required. The document GUID to retrieve section content for.
        /// </param>
        /// <param name="sectionGuid">
        /// Optional. Filter to a specific section by its GUID.
        /// </param>
        /// <param name="sectionCode">
        /// Optional. Filter by LOINC section code (e.g., "34084-4" for Adverse Reactions).
        /// Supports partial matching for flexible queries.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of section content with text for summarization.</returns>
        /// <response code="200">Returns the list of section content.</response>
        /// <response code="400">If documentGuid is empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ## Usage Examples
        ///
        /// Get all section content for a document:
        /// ```
        /// GET /api/Label/section/content/{documentGuid}
        /// ```
        ///
        /// Get specific section by GUID:
        /// ```
        /// GET /api/Label/section/content/{documentGuid}?sectionGuid={guid}
        /// ```
        ///
        /// Get all Adverse Reactions sections:
        /// ```
        /// GET /api/Label/section/content/{documentGuid}?sectionCode=34084-4
        /// ```
        ///
        /// ## Common LOINC Section Codes
        ///
        /// | Code | Section Name |
        /// |------|--------------|
        /// | 34066-1 | Boxed Warning |
        /// | 34067-9 | Indications and Usage |
        /// | 34068-7 | Dosage and Administration |
        /// | 34069-5 | How Supplied |
        /// | 43685-7 | Warnings and Precautions |
        /// | 34084-4 | Adverse Reactions |
        /// | 34073-7 | Drug Interactions |
        /// | 34088-5 | Overdosage |
        /// | 34090-1 | Clinical Pharmacology |
        ///
        /// ## Response Format (200)
        ///
        /// ```json
        /// [
        ///   {
        ///     "sectionContent": {
        ///       "EncryptedDocumentID": "encrypted_string",
        ///       "EncryptedSectionID": "encrypted_string",
        ///       "DocumentGUID": "guid",
        ///       "SectionGUID": "guid",
        ///       "SectionCode": "34084-4",
        ///       "SectionDisplayName": "ADVERSE REACTIONS",
        ///       "SectionTitle": "Adverse Reactions",
        ///       "ContentText": "The following adverse reactions...",
        ///       "SequenceNumber": 1,
        ///       "ContentType": "paragraph"
        ///     }
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by SectionCode, then SequenceNumber for proper reading order.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get warnings section for AI summarization
        /// GET /api/Label/section/content/12345678-1234-1234-1234-123456789012?sectionCode=43685-7
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.GetSectionContentAsync"/>
        /// <seealso cref="LabelView.SectionContent"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("section/content/{documentGuid}")]
        [ProducesResponseType(typeof(IEnumerable<SectionContentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<SectionContentDto>>> GetSectionContent(
            [FromRoute] Guid documentGuid,
            [FromQuery] Guid? sectionGuid,
            [FromQuery] string? sectionCode,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate documentGuid is provided
            if (documentGuid == Guid.Empty)
            {
                return BadRequest("A valid documentGuid is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation(
                    "Getting section content for DocumentGUID: {DocumentGuid}, SectionGUID: {SectionGuid}, SectionCode: {SectionCode}, Page: {PageNumber}, Size: {PageSize}",
                    documentGuid,
                    sectionGuid?.ToString() ?? "all",
                    sectionCode ?? "all",
                    pageNumber,
                    pageSize);

                var results = await _labelContentQueryService.GetSectionContentAsync(documentGuid, sectionGuid, sectionCode, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        #endregion Section Navigation

        #region Drug Safety Navigation

        /**************************************************************/
        /// <summary>
        /// Gets products with DEA controlled substance schedules.
        /// Important for pharmacy compliance and controlled substance management.
        /// </summary>
        /// <param name="scheduleCode">
        /// Optional filter by specific DEA schedule code (e.g., "CII", "CIII", "CIV", "CV").
        /// If not provided, returns all controlled substances.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of products with DEA schedule classifications.</returns>
        /// <response code="200">Returns the list of DEA scheduled products.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/drug-safety/dea-schedule
        /// GET /api/Label/drug-safety/dea-schedule?scheduleCode=CII
        ///
        /// DEA Schedule Codes:
        /// - CI: Schedule I (no accepted medical use)
        /// - CII: Schedule II (high potential for abuse)
        /// - CIII: Schedule III (moderate to low potential for abuse)
        /// - CIV: Schedule IV (low potential for abuse)
        /// - CV: Schedule V (lower potential for abuse than Schedule IV)
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "OXYCONTIN",
        ///     "DEAScheduleCode": "CII",
        ///     "DEAScheduleName": "Schedule II Controlled Substance"
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by DEAScheduleCode, then ProductName.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetDEAScheduleProductsAsync"/>
        /// <seealso cref="LabelView.DEAScheduleLookup"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("drug-safety/dea-schedule")]
        [ProducesResponseType(typeof(IEnumerable<DEAScheduleLookupDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<DEAScheduleLookupDto>>> GetDEAScheduleProducts(
            [FromQuery] string? scheduleCode,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting DEA schedule products. ScheduleCode: {ScheduleCode}, Page: {PageNumber}, Size: {PageSize}",
                    scheduleCode ?? "all", pageNumber, pageSize);

                var results = await _labelContentQueryService.GetDEAScheduleProductsAsync(scheduleCode, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        #endregion Drug Safety Navigation

        #region Product Summary and Cross-Reference

        /**************************************************************/
        /// <summary>
        /// Searches for products with comprehensive summary information.
        /// Provides a complete product overview with key attributes for quick reference.
        /// </summary>
        /// <param name="productNameSearch">
        /// Search term to match against product names (e.g., "Lipitor", "Aspirin").
        /// Supports partial matching for flexible searches.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of product summaries matching the product name.</returns>
        /// <response code="200">Returns the list of product summaries.</response>
        /// <response code="400">If productNameSearch is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/product/search?productNameSearch=Lipitor
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "LIPITOR",
        ///     "LabelerName": "PFIZER INC",
        ///     "ApplicationNumber": "NDA020702",
        ///     "ActiveIngredients": "ATORVASTATIN CALCIUM",
        ///     "DosageForm": "TABLET, FILM COATED"
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by ProductName.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search for Lipitor
        /// GET /api/Label/product/search?productNameSearch=Lipitor
        ///
        /// // Search with pagination
        /// GET /api/Label/product/search?productNameSearch=Aspirin&amp;pageNumber=1&amp;pageSize=50
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchProductSummaryAsync"/>
        /// <seealso cref="LabelView.ProductSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("product/search")]
        [ProducesResponseType(typeof(IEnumerable<ProductSummaryViewDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductSummaryViewDto>>> SearchProductSummary(
            [FromQuery] string productNameSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate product name search term is provided
            if (string.IsNullOrWhiteSpace(productNameSearch))
            {
                return BadRequest("Product name search term is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching product summaries: {ProductNameSearch}, Page: {PageNumber}, Size: {PageSize}",
                    productNameSearch, pageNumber, pageSize);

                var results = await _labelContentQueryService.SearchProductSummaryAsync(productNameSearch, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets related products by shared application number or active ingredient.
        /// Useful for finding alternatives, generics, or similar drugs.
        /// </summary>
        /// <param name="sourceProductId">
        /// Optional source product ID (decrypted) to find related products for.
        /// Either sourceProductId or sourceDocumentGuid must be provided.
        /// </param>
        /// <param name="sourceDocumentGuid">
        /// Optional source document GUID to find related products for.
        /// Either sourceProductId or sourceDocumentGuid must be provided.
        /// Use this parameter when you have the DocumentGUID from GetProductLatestLabels.
        /// </param>
        /// <param name="relationshipType">
        /// Optional filter by relationship type. Valid values:
        /// - "SameApplicationNumber": Products under the same NDA/ANDA/BLA
        /// - "SameActiveIngredient": Products with the same active ingredient(s)
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of products related to the source product.</returns>
        /// <response code="200">Returns the list of related products.</response>
        /// <response code="400">If neither sourceProductId nor sourceDocumentGuid is provided, or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ## Usage Examples
        ///
        /// Find related products by Product ID:
        /// ```
        /// GET /api/Label/product/related?sourceProductId=12345
        /// ```
        ///
        /// Find related products by Document GUID (from GetProductLatestLabels):
        /// ```
        /// GET /api/Label/product/related?sourceDocumentGuid=12345678-1234-1234-1234-123456789012
        /// ```
        ///
        /// Filter by relationship type:
        /// ```
        /// GET /api/Label/product/related?sourceDocumentGuid=12345678-1234-1234-1234-123456789012&amp;relationshipType=SameActiveIngredient
        /// ```
        ///
        /// ## Response Format (200)
        ///
        /// ```json
        /// [
        ///   {
        ///     "RelatedProducts": {
        ///       "EncryptedRelatedProductID": "encrypted_string",
        ///       "RelatedProductName": "GENERIC LIPITOR",
        ///       "RelatedDocumentGUID": "87654321-4321-4321-4321-210987654321",
        ///       "RelationshipType": "SameActiveIngredient",
        ///       "SharedValue": "ATORVASTATIN CALCIUM"
        ///     }
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by RelatedProductName.
        ///
        /// ## Workflow Integration
        ///
        /// Use this endpoint after GetProductLatestLabels to find alternative products:
        /// 1. Call `/api/Label/product/latest?productNameSearch=Lipitor` to get DocumentGUID
        /// 2. Call `/api/Label/product/related?sourceDocumentGuid={DocumentGUID}` to find related products
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetRelatedProductsAsync"/>
        /// <seealso cref="LabelView.RelatedProducts"/>
        /// <seealso cref="GetProductLatestLabels"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("product/related")]
        [ProducesResponseType(typeof(IEnumerable<RelatedProductsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<RelatedProductsDto>>> GetRelatedProducts(
            [FromQuery] int? sourceProductId,
            [FromQuery] Guid? sourceDocumentGuid,
            [FromQuery] string? relationshipType,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate that at least one identifier is provided
            if ((!sourceProductId.HasValue || sourceProductId.Value <= 0) && !sourceDocumentGuid.HasValue)
            {
                return BadRequest("Either sourceProductId or sourceDocumentGuid must be provided.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting related products for ProductID: {SourceProductId}, DocumentGUID: {SourceDocumentGuid}, RelationshipType: {RelationshipType}, Page: {PageNumber}, Size: {PageSize}",
                    sourceProductId, sourceDocumentGuid, relationshipType ?? "all", pageNumber, pageSize);

                var results = await _labelContentQueryService.GetRelatedProductsAsync(sourceProductId, sourceDocumentGuid, relationshipType, pageNumber, pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the API endpoint guide for AI-assisted endpoint discovery.
        /// Claude API and other AI integrations query this endpoint to understand
        /// available navigation views and usage patterns.
        /// </summary>
        /// <param name="category">
        /// Optional filter by endpoint category (e.g., "Navigation", "Search", "Summary").
        /// If not provided, returns all endpoint metadata.
        /// </param>
        /// <returns>List of API endpoint metadata for discovery purposes.</returns>
        /// <response code="200">Returns the API endpoint guide.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/guide
        /// GET /api/Label/guide?category=Navigation
        ///
        /// This endpoint provides metadata about available view-based endpoints,
        /// including descriptions, parameters, and usage examples. Designed for
        /// programmatic discovery by AI assistants and integration tools.
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "ViewName": "ProductsByApplicationNumber",
        ///     "Category": "Navigation",
        ///     "Description": "Search products by regulatory application number",
        ///     "EndpointPath": "/api/Label/application-number/search",
        ///     "Parameters": "applicationNumber (required), pageNumber, pageSize"
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by Category, then ViewName.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetAPIEndpointGuideAsync"/>
        /// <seealso cref="LabelView.APIEndpointGuide"/>
        [HttpGet("guide")]
        [ProducesResponseType(typeof(IEnumerable<APIEndpointGuideDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<APIEndpointGuideDto>>> GetAPIEndpointGuide(
            [FromQuery] string? category)
        {
            #region Implementation

            try
            {
                _logger.LogInformation("Getting API endpoint guide. Category: {Category}", category ?? "all");

                var results = await _labelContentQueryService.GetAPIEndpointGuideAsync(category);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets comprehensive inventory summary for answering "what products do you have" questions.
        /// Provides aggregated counts across multiple dimensions instead of paginated product lists.
        /// </summary>
        /// <param name="category">
        /// Optional filter by category. Valid values:
        /// - **TOTALS**: High-level entity counts (Documents, Products, Labelers, Active Ingredients, etc.)
        /// - **BY_MARKETING_CATEGORY**: Products by marketing category (NDA, ANDA, BLA, OTC, etc.)
        /// - **BY_DOSAGE_FORM**: Products by dosage form (top 15)
        /// - **TOP_LABELERS**: Top 10 labelers by product count
        /// - **TOP_PHARM_CLASSES**: Top 10 pharmacologic classes by product count
        /// - **TOP_INGREDIENTS**: Top 10 active ingredients by product count
        ///
        /// If not provided, returns all categories (~50 rows).
        /// </param>
        /// <returns>List of inventory summary items with aggregated counts.</returns>
        /// <response code="200">Returns the inventory summary.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ## Purpose
        ///
        /// Use this endpoint to answer questions like:
        /// - "What products do you have?"
        /// - "How many products are in the database?"
        /// - "What drug classes are available?"
        /// - "Who are the top manufacturers?"
        ///
        /// This endpoint provides accurate totals instead of paginated results that may give
        /// an incomplete impression of database size (e.g., "50 products" when there are actually thousands).
        ///
        /// ## Usage Examples
        ///
        /// Get full inventory summary:
        /// ```
        /// GET /api/Label/inventory/summary
        /// ```
        ///
        /// Get totals only:
        /// ```
        /// GET /api/Label/inventory/summary?category=TOTALS
        /// ```
        ///
        /// Get top labelers:
        /// ```
        /// GET /api/Label/inventory/summary?category=TOP_LABELERS
        /// ```
        ///
        /// ## Response Format (200)
        ///
        /// ```json
        /// [
        ///   {
        ///     "InventorySummary": {
        ///       "Category": "TOTALS",
        ///       "Dimension": "Documents",
        ///       "DimensionValue": null,
        ///       "ItemCount": 1234,
        ///       "SortOrder": 1
        ///     }
        ///   },
        ///   {
        ///     "InventorySummary": {
        ///       "Category": "TOTALS",
        ///       "Dimension": "Products",
        ///       "DimensionValue": null,
        ///       "ItemCount": 5678,
        ///       "SortOrder": 2
        ///     }
        ///   },
        ///   {
        ///     "InventorySummary": {
        ///       "Category": "TOP_LABELERS",
        ///       "Dimension": "Labeler",
        ///       "DimensionValue": "PFIZER INC",
        ///       "ItemCount": 150,
        ///       "SortOrder": 301
        ///     }
        ///   }
        /// ]
        /// ```
        ///
        /// Results are sorted by SortOrder for logical grouping.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get full inventory summary
        /// GET /api/Label/inventory/summary
        ///
        /// // Get totals only
        /// GET /api/Label/inventory/summary?category=TOTALS
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.GetInventorySummaryAsync"/>
        /// <seealso cref="LabelView.InventorySummary"/>
        [HttpGet("inventory/summary")]
        [ProducesResponseType(typeof(IEnumerable<InventorySummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<InventorySummaryDto>>> GetInventorySummary(
            [FromQuery] string? category)
        {
            #region Implementation

            try
            {
                _logger.LogInformation("Getting inventory summary. Category: {Category}", category ?? "all");

                var results = await _labelContentQueryService.GetInventorySummaryAsync(category);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        #endregion Product Summary and Cross-Reference

        #region Latest Label Navigation

        /**************************************************************/
        /// <summary>
        /// Gets the latest label for each product/active ingredient combination.
        /// Returns the most recent document based on EffectiveTime for each UNII/ProductName pair.
        /// </summary>
        /// <param name="unii">
        /// Optional UNII (Unique Ingredient Identifier) code for exact match filtering.
        /// Example: "R16CO5Y76E" for aspirin.
        /// </param>
        /// <param name="productNameSearch">
        /// Optional product name search term. Supports partial and phonetic matching.
        /// </param>
        /// <param name="activeIngredientSearch">
        /// Optional active ingredient name search term. Supports partial and phonetic matching.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of latest labels matching the search criteria.</returns>
        /// <response code="200">Returns the list of latest labels.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ## Usage Examples
        ///
        /// Get latest label by UNII:
        /// ```
        /// GET /api/Label/product/latest?unii=R16CO5Y76E
        /// ```
        ///
        /// Search by product name:
        /// ```
        /// GET /api/Label/product/latest?productNameSearch=Lipitor
        /// ```
        ///
        /// Search by active ingredient name:
        /// ```
        /// GET /api/Label/product/latest?activeIngredientSearch=atorvastatin
        /// ```
        ///
        /// ## Response Format (200)
        ///
        /// ```json
        /// [
        ///   {
        ///     "ProductLatestLabel": {
        ///       "ProductName": "LIPITOR",
        ///       "ActiveIngredient": "ATORVASTATIN CALCIUM",
        ///       "UNII": "A0JWA85V8F",
        ///       "DocumentGUID": "12345678-1234-1234-1234-123456789012"
        ///     }
        ///   }
        /// ]
        /// ```
        ///
        /// Use the DocumentGUID with `/api/label/single/{documentGuid}` to retrieve the complete label.
        ///
        /// This view returns only one row per UNII/ProductName combination, selecting the document
        /// with the most recent EffectiveTime.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Find latest label for aspirin
        /// GET /api/Label/product/latest?unii=R16CO5Y76E
        ///
        /// // Find latest label by product name
        /// GET /api/Label/product/latest?productNameSearch=Lipitor&amp;pageNumber=1&amp;pageSize=25
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.GetProductLatestLabelsAsync"/>
        /// <seealso cref="LabelView.ProductLatestLabel"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("product/latest")]
        [ProducesResponseType(typeof(IEnumerable<ProductLatestLabelDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductLatestLabelDto>>> GetProductLatestLabels(
            [FromQuery] string? unii,
            [FromQuery] string? productNameSearch,
            [FromQuery] string? activeIngredientSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null) return pagingValidation;

            #endregion

            #region implementation

            try
            {
                // Get latest labels using the data access method
                var results = await _productSearchService.GetProductLatestLabelsAsync(unii, productNameSearch, activeIngredientSearch, pageNumber, pageSize);

                // Add pagination headers if paging was requested
                addPaginationHeaders(pageNumber, pageSize, results.Count);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the latest labels for products with full section markdown content and absolute URLs to original XML.
        /// Combines product search results with all label sections for AI/MCP consumption.
        /// </summary>
        /// <param name="unii">
        /// Optional UNII (Unique Ingredient Identifier) code for exact match filtering.
        /// Example: "R16CO5Y76E" for aspirin.
        /// </param>
        /// <param name="productNameSearch">
        /// Optional product name search term. Supports partial matching.
        /// </param>
        /// <param name="activeIngredientSearch">
        /// Optional active ingredient search term. Supports partial matching.
        /// </param>
        /// <param name="sectionCode">
        /// Optional LOINC section code to filter sections. If not specified, all sections are returned.
        /// Common codes: 34067-9 (Indications), 34084-4 (Adverse Reactions), 34070-3 (Contraindications).
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve. Defaults to 1.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page. Defaults to 10.
        /// </param>
        /// <returns>List of products with full section details and absolute URLs.</returns>
        /// <response code="200">Returns the list of product label details.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ## Overview
        ///
        /// This endpoint is designed for **MCP (Model Context Protocol)** and **AI skill augmentation** workflows
        /// where you need complete product information with authoritative label content in a single call.
        ///
        /// The endpoint combines:
        /// - **Product search results** from `GetProductLatestLabels`
        /// - **Section markdown content** from `LabelMarkdownController.GetLabelSectionMarkdown`
        /// - **Absolute URLs** to original XML documents (required for MCP contexts where relative paths break)
        ///
        /// ## Response Format (200)
        ///
        /// ```json
        /// [
        ///   {
        ///     "ProductLatestLabel": {
        ///       "ProductName": "LIPITOR",
        ///       "ActiveIngredient": "ATORVASTATIN CALCIUM",
        ///       "UNII": "A0JWA85V8F",
        ///       "DocumentGUID": "052493C7-89A3-452E-8140-04DD95F0D9E2"
        ///     },
        ///     "ViewLabelUrl": "https://medrecpro.example.com/api/Label/original/052493C7-89A3-452E-8140-04DD95F0D9E2/false",
        ///     "ViewLabelMinifiedUrl": "https://medrecpro.example.com/api/Label/original/052493C7-89A3-452E-8140-04DD95F0D9E2/true",
        ///     "Sections": [
        ///       {
        ///         "LabelSectionMarkdown": {
        ///           "SectionCode": "34067-9",
        ///           "SectionTitle": "INDICATIONS AND USAGE",
        ///           "FullSectionText": "## INDICATIONS AND USAGE\n\nLIPITOR is indicated..."
        ///         }
        ///       }
        ///     ]
        ///   }
        /// ]
        /// ```
        ///
        /// ## Use Case
        ///
        /// Use this endpoint when building AI skills that need to:
        /// 1. Search for products by name, ingredient, or UNII
        /// 2. Display full label content to users in chat interfaces
        /// 3. Provide clickable links to original FDA label XML documents
        ///
        /// ## Performance Consideration
        ///
        /// This endpoint makes additional database calls to fetch section content for each product.
        /// For large result sets, consider using pagination or filtering by `sectionCode` to reduce payload size.
        ///
        /// **Common LOINC Section Codes:**
        /// - `34067-9` = Indications and Usage
        /// - `34084-4` = Adverse Reactions
        /// - `34070-3` = Contraindications
        /// - `43685-7` = Warnings and Precautions
        /// - `34068-7` = Dosage and Administration
        /// </remarks>
        /// <example>
        /// <code>
        /// // Find latest label details for aspirin with all sections
        /// GET /api/Label/product/latest/details?unii=R16CO5Y76E
        ///
        /// // Find latest label by product name with only indications section
        /// GET /api/Label/product/latest/details?productNameSearch=Lipitor&amp;sectionCode=34067-9
        ///
        /// // Paginated search for all products containing "statin"
        /// GET /api/Label/product/latest/details?activeIngredientSearch=statin&amp;pageNumber=1&amp;pageSize=5
        /// </code>
        /// </example>
        /// <seealso cref="GetProductLatestLabels"/>
        /// <seealso cref="LabelMarkdownController.GetLabelSectionMarkdown"/>
        /// <seealso cref="LabelDocumentController.OriginalXmlDocument"/>
        /// <seealso cref="DtoLabelAccess.GetProductLatestLabelsAsync"/>
        /// <seealso cref="DtoLabelAccess.GetLabelSectionMarkdownAsync"/>
        /// <seealso cref="ProductLatestLabelDetailsDto"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("product/latest/details")]
        [ProducesResponseType(typeof(IEnumerable<ProductLatestLabelDetailsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductLatestLabelDetailsDto>>> GetProductLatestLabelDetails(
            [FromQuery] string? unii,
            [FromQuery] string? productNameSearch,
            [FromQuery] string? activeIngredientSearch,
            [FromQuery] string? sectionCode,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null) return pagingValidation;

            #endregion

            #region implementation

            try
            {
                // Construct base URL from current request for absolute links
                var baseUrl = $"{Request.Scheme}://{Request.Host}";

                // Get latest labels using the data access method
                var productResults = await _productSearchService.GetProductLatestLabelsAsync(unii, productNameSearch, activeIngredientSearch, pageNumber, pageSize);

                // Build detailed results with sections and URLs for each product
                var detailedResults = new List<ProductLatestLabelDetailsDto>();

                foreach (var product in productResults)
                {
                    // Extract DocumentGUID from the product result
                    var documentGuid = product.DocumentGUID;

                    // Initialize the detailed DTO with product data
                    var detailedDto = new ProductLatestLabelDetailsDto
                    {
                        ProductLatestLabel = product.ProductLatestLabel
                    };

                    // Add absolute URLs to original XML if DocumentGUID is available
                    if (documentGuid.HasValue)
                    {
                        // Construct absolute URLs for original XML endpoints
                        // Note: Always use /api/ prefix since the virtual app path is included in Request.Host for production
                        // Construct absolute URLs for viewing the label in a browser
                        // These URLs render as formatted HTML via XSL stylesheet transformation
                        detailedDto.ViewLabelUrl = $"{baseUrl}/api/Label/original/{documentGuid.Value}/false";
                        detailedDto.ViewLabelMinifiedUrl = $"{baseUrl}/api/Label/original/{documentGuid.Value}/true";

                        // Fetch section markdown content for this document
                        var sections = await _labelMarkdownService.GetLabelSectionMarkdownAsync(documentGuid.Value, sectionCode);

                        detailedDto.Sections = sections;
                    }

                    detailedResults.Add(detailedDto);
                }

                // Add pagination headers if paging was requested
                addPaginationHeaders(pageNumber, pageSize, detailedResults.Count);

                return Ok(detailedResults);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets product indication text combined with active ingredients.
        /// Returns indication section content for products filtered by UNII, product name, substance name, or indication text.
        /// </summary>
        /// <param name="unii">
        /// Optional UNII (Unique Ingredient Identifier) code for exact match filtering.
        /// Example: "R16CO5Y76E" for aspirin.
        /// </param>
        /// <param name="productNameSearch">
        /// Optional product name search term. Supports partial matching.
        /// </param>
        /// <param name="substanceNameSearch">
        /// Optional substance name search term. Supports partial matching.
        /// </param>
        /// <param name="indicationSearch">
        /// Optional clinical indication search term. Searches within indication text content.
        /// Examples: "hypertension", "diabetes", "pain"
        /// Uses partial matching - any search term can match within the indication text.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of product indications matching the search criteria.</returns>
        /// <response code="200">Returns the list of product indications.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ## Usage Examples
        ///
        /// Get indications by UNII:
        /// ```
        /// GET /api/Label/product/indications?unii=R16CO5Y76E
        /// ```
        ///
        /// Search by product name:
        /// ```
        /// GET /api/Label/product/indications?productNameSearch=Lipitor
        /// ```
        ///
        /// Search by substance name:
        /// ```
        /// GET /api/Label/product/indications?substanceNameSearch=atorvastatin
        /// ```
        ///
        /// **Search by clinical indication:**
        /// ```
        /// GET /api/Label/product/indications?indicationSearch=hypertension
        /// GET /api/Label/product/indications?indicationSearch=type%202%20diabetes
        /// GET /api/Label/product/indications?indicationSearch=chronic%20pain
        /// ```
        ///
        /// ## Response Format (200)
        ///
        /// ```json
        /// [
        ///   {
        ///     "ProductIndications": {
        ///       "ProductName": "LIPITOR",
        ///       "SubstanceName": "ATORVASTATIN CALCIUM",
        ///       "UNII": "A0JWA85V8F",
        ///       "DocumentGUID": "12345678-1234-1234-1234-123456789012",
        ///       "ContentText": "LIPITOR is indicated as an adjunctive therapy to diet..."
        ///     }
        ///   }
        /// ]
        /// ```
        ///
        /// The view filters to INDICATION sections only and excludes inactive ingredients (IACT class).
        /// ContentText combines text from SectionTextContent and TextListItem.
        ///
        /// ## Indication Search Tips
        ///
        /// - Use medical terminology for best results (e.g., "hypertension" not "high blood pressure")
        /// - Multiple words are OR-matched (any term can match in indication text)
        /// - Combine with substanceNameSearch to narrow results to specific drug classes
        /// - For AI-assisted query interpretation, use POST /api/ai/interpret first
        /// </remarks>
        /// <example>
        /// <code>
        /// // Find indications for aspirin
        /// GET /api/Label/product/indications?unii=R16CO5Y76E
        ///
        /// // Find indications by product name with pagination
        /// GET /api/Label/product/indications?productNameSearch=Lipitor&amp;pageNumber=1&amp;pageSize=25
        ///
        /// // Find products indicated for hypertension
        /// GET /api/Label/product/indications?indicationSearch=hypertension
        ///
        /// // Find statins indicated for hypercholesterolemia
        /// GET /api/Label/product/indications?indicationSearch=hypercholesterolemia&amp;substanceNameSearch=statin
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.GetProductIndicationsAsync"/>
        /// <seealso cref="LabelView.ProductIndications"/>
        /// <seealso cref="SearchBySectionCode"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("product/indications")]
        [ProducesResponseType(typeof(IEnumerable<ProductIndicationsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductIndicationsDto>>> GetProductIndications(
            [FromQuery] string? unii,
            [FromQuery] string? productNameSearch,
            [FromQuery] string? substanceNameSearch,
            [FromQuery] string? indicationSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null) return pagingValidation;

            #endregion

            #region implementation

            try
            {
                // Get product indications using the data access method
                var results = await _productSearchService.GetProductIndicationsAsync(unii, productNameSearch, substanceNameSearch, indicationSearch, pageNumber, pageSize);

                // Add pagination headers if paging was requested
                addPaginationHeaders(pageNumber, pageSize, results.Count);

                return Ok(results);
            }
            // Global-handler bridge: unexpected failures are logged and translated once by MedRecProExceptionHandler.
            catch
            {
                throw;
            }

            #endregion
        }

        #endregion Latest Label Navigation

        #endregion
    }
}
