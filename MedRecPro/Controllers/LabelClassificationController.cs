using MedRecPro.Controllers;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Mappers;
using MedRecPro.Models;
using MedRecPro.Models.Extensions;
using MedRecPro.Service;
using MedRecPro.Service.Common;
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
    /// Handles Label pharmacologic-class, indication, extraction, and DEA discovery endpoints.
    /// </summary>
    /// <remarks>
    /// The inherited Label controller convention preserves the existing public Debug and Release routes.
    /// </remarks>
    /// <seealso cref="FeatureControllerNameAttribute"/>
    [ApiController]
    [FeatureControllerName("Label")]
    [SwaggerGroup(
        "Label Classification",
        "Pharmacologic class, indication, and DEA-schedule discovery.")]
    public sealed class LabelClassificationController : ApiControllerBase
    {
        #region implementation

        private readonly ILogger<LabelClassificationController> _logger;
        private readonly ILabelAiSearchService _labelAiSearchService;
        private readonly IPharmacologicClassSearchService _pharmacologicClassSearchService;
        private readonly ILabelContentQueryService _labelContentQueryService;
        private readonly IUserContextAccessor _userContextAccessor;

        /**************************************************************/
        /// <summary>Initializes a new instance of the controller.</summary>
        /// <param name="logger">Logger for endpoint diagnostics.</param>
        /// <param name="labelAiSearchService">AI search application service.</param>
        /// <param name="pharmacologicClassSearchService">Classification query service.</param>
        /// <param name="labelContentQueryService">Label content query service.</param>
        /// <param name="userContextAccessor">Current-user accessor.</param>
        public LabelClassificationController(
            ILogger<LabelClassificationController> logger,
            ILabelAiSearchService labelAiSearchService,
            IPharmacologicClassSearchService pharmacologicClassSearchService,
            ILabelContentQueryService labelContentQueryService,
            IUserContextAccessor userContextAccessor)
        {
            #region implementation

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _labelAiSearchService = labelAiSearchService ?? throw new ArgumentNullException(nameof(labelAiSearchService));
            _pharmacologicClassSearchService = pharmacologicClassSearchService ?? throw new ArgumentNullException(nameof(pharmacologicClassSearchService));
            _labelContentQueryService = labelContentQueryService ?? throw new ArgumentNullException(nameof(labelContentQueryService));
            _userContextAccessor = userContextAccessor ?? throw new ArgumentNullException(nameof(userContextAccessor));

            #endregion
        }

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
                var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
                var aiResult = await _labelAiSearchService.SearchByPharmacologicClassAsync(
                    query,
                    maxProductsPerClass,
                    isAuthenticated,
                    _userContextAccessor.GetCurrentUserId(HttpContext),
                    HttpContext.RequestAborted);
                if (aiResult is not null)
                {
                    return Ok(aiResult);
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
        /// // 1. Original query: /api/Label/product/latest?unii=WRONG_UNII â†’ empty
        /// // 2. Extract product: /api/Label/extract-product?description=...
        /// // 3. Search ingredient: /api/Label/ingredient/advanced?substanceNameSearch=finerenone
        /// // 4. Get correct UNII: WPE1M7X0GA
        /// // 5. Retry: /api/Label/product/latest?unii=WPE1M7X0GA â†’ success!
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

                var result = await _labelAiSearchService.ExtractProductAsync(
                    description,
                    HttpContext.RequestAborted);

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
            if (useAiCache)
            {
                var summaries = await _labelAiSearchService.GetCachedClassSummariesAsync(HttpContext.RequestAborted);
                if (summaries is not null)
                {
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

            try
            {
                _logger.LogInformation("[Label] AI indication search for: {Query}", query);

                var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
                var result = await _labelAiSearchService.SearchByIndicationAsync(
                    query,
                    maxProductsPerIndication,
                    isAuthenticated,
                    _userContextAccessor.GetCurrentUserId(HttpContext),
                    HttpContext.RequestAborted);

                if (result is null)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable,
                        "AI search service is not available. Indication search requires AI capabilities.");
                }

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


        #endregion
    }
}
