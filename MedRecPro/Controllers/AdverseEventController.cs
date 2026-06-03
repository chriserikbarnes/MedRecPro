using MedRecPro.Controllers;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Exposes adverse-event dashboard data for product selection, visualizations, reverse lookup, interchange comparison, and favorites.
    /// </summary>
    /// <remarks>
    /// This controller is intentionally thin: it validates HTTP inputs, enforces feature and user gates, and delegates all AE dashboard query,
    /// derivation, sorting, score, comparator, and favorite persistence behavior to <see cref="DtoLabelAccess"/>.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="AeDrugSummaryDto"/>
    /// <seealso cref="AspNetUserFavorite"/>
    [ApiController]
    public class AdverseEventController : ApiControllerBase
    {
        #region private fields

        /**************************************************************/
        /// <summary>
        /// Configuration provider used for feature flags and primary-key encryption settings.
        /// </summary>
        private readonly IConfiguration _configuration;

        /**************************************************************/
        /// <summary>
        /// Logger used for controller-level diagnostics and unexpected failures.
        /// </summary>
        private readonly ILogger<AdverseEventController> _logger;

        /**************************************************************/
        /// <summary>
        /// Entity Framework context used by dashboard data-access methods.
        /// </summary>
        private readonly ApplicationDbContext _dbContext;

        /**************************************************************/
        /// <summary>
        /// User data-access service used to resolve the authenticated claims user before favorite operations.
        /// </summary>
        private readonly UserDataAccess _userDataAccess;

        /**************************************************************/
        /// <summary>
        /// Secret used to encrypt and decrypt server-side primary keys that appear in claims or DTO helper properties.
        /// </summary>
        private readonly string _pkSecret;

        #endregion private fields

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="AdverseEventController"/> class.
        /// </summary>
        /// <param name="configuration">Configuration provider for feature flags and encryption settings.</param>
        /// <param name="logger">Logger instance for controller diagnostics.</param>
        /// <param name="applicationDbContext">Application database context used by dashboard queries.</param>
        /// <param name="userDataAccess">User data-access service used to resolve authenticated claims users.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is not supplied.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <c>Security:DB:PKSecret</c> is missing or whitespace.</exception>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="UserDataAccess"/>
        public AdverseEventController(
            IConfiguration configuration,
            ILogger<AdverseEventController> logger,
            ApplicationDbContext applicationDbContext,
            UserDataAccess userDataAccess)
        {
            #region implementation

            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = applicationDbContext ?? throw new ArgumentNullException(nameof(applicationDbContext));
            _userDataAccess = userDataAccess ?? throw new ArgumentNullException(nameof(userDataAccess));

            _pkSecret = _configuration.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing or empty.");

            if (string.IsNullOrWhiteSpace(_pkSecret))
            {
                throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing or empty.");
            }

            #endregion
        }

        #endregion constructor

        #region public dashboard actions

        /**************************************************************/
        /// <summary>
        /// Gets AE dashboard product summaries for the product picker.
        /// </summary>
        /// <param name="productSearch">Optional product, substance, UNII, or pharmacologic-class search text.</param>
        /// <param name="pageNumber">Optional 1-based page number.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <returns>Product summaries shaped for the AE dashboard picker.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Replaces the prototype product catalog and supplies product coverage, score, score reason, and optional favorite state.
        ///
        /// Authenticated callers are resolved from their claims and receive user-specific favorite flags. Anonymous callers receive the same
        /// catalog without favorite state. If an authenticated principal cannot be resolved to a current user, the endpoint returns 401.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/products?productSearch=aspirin&amp;pageNumber=1&amp;pageSize=25
        /// </code>
        /// </example>
        /// <response code="200">Returns product summaries.</response>
        /// <response code="400">If pagination values are invalid.</response>
        /// <response code="401">If an authenticated principal cannot be resolved.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeDrugSummariesAsync"/>
        /// <seealso cref="AeDrugSummaryDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("products")]
        [ProducesResponseType(typeof(List<AeDrugSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<List<AeDrugSummaryDto>>> GetProducts(
            [FromQuery] string? productSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            try
            {
                var optionalUser = await tryGetOptionalDashboardUserIdAsync();
                if (optionalUser.ErrorResult != null)
                {
                    return optionalUser.ErrorResult;
                }

                var results = await DtoLabelAccess.GetAeDrugSummariesAsync(
                    _dbContext,
                    _pkSecret,
                    _logger,
                    productSearch,
                    optionalUser.UserId,
                    pageNumber,
                    pageSize);

                addPaginationHeaders(pageNumber, pageSize, results.Count);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard product summaries.");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving AE dashboard product summaries.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the slim AE dashboard product catalog for the product picker.
        /// </summary>
        /// <param name="productSearch">Optional product, substance, UNII, or pharmacologic-class search text.</param>
        /// <param name="pageNumber">Optional 1-based page number.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <returns>Slim catalog items carrying only the fields the picker renders.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Backs the product picker. Returns one row per product (combination products
        /// list every active ingredient with its preferred *[EPC]* class) and is served
        /// from a shared, cached, per-document catalog so repeat opens are fast.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/products/catalog?productSearch=advair&amp;pageNumber=1&amp;pageSize=25
        /// </code>
        /// </example>
        /// <response code="200">Returns slim product catalog items.</response>
        /// <response code="400">If pagination values are invalid.</response>
        /// <response code="401">If an authenticated principal cannot be resolved.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeProductCatalogAsync"/>
        /// <seealso cref="AeProductCatalogItemDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("products/catalog")]
        [ProducesResponseType(typeof(List<AeProductCatalogItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<List<AeProductCatalogItemDto>>> GetProductCatalog(
            [FromQuery] string? productSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            try
            {
                var optionalUser = await tryGetOptionalDashboardUserIdAsync();
                if (optionalUser.ErrorResult != null)
                {
                    return optionalUser.ErrorResult;
                }

                var results = await DtoLabelAccess.GetAeProductCatalogAsync(
                    _dbContext,
                    _pkSecret,
                    _logger,
                    productSearch,
                    optionalUser.UserId,
                    pageNumber,
                    pageSize);

                addPaginationHeaders(pageNumber, pageSize, results.Count);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard product catalog.");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the AE dashboard product catalog.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the count of distinct AE dashboard products available to the picker.
        /// </summary>
        /// <returns>The real distinct-product inventory count.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Backs the product-picker count badge so it reflects actual inventory rather
        /// than a hard-coded page size. Returns <c>COUNT(DISTINCT ProductName)</c> from
        /// the materialized AE risk table. No search, paging, or user resolution applies.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/products/count
        /// </code>
        /// </example>
        /// <response code="200">Returns the distinct product count.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeProductCountAsync"/>
        /// <seealso cref="GetProductCatalog"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [HttpGet("products/count")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<int>> GetProductCount()
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            try
            {
                var count = await DtoLabelAccess.GetAeProductCountAsync(_dbContext, _logger);
                return Ok(count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard product count.");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the AE dashboard product count.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the authenticated user's favorited AE dashboard products.
        /// </summary>
        /// <param name="pageNumber">Optional 1-based page number.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <returns>Favorite product summaries for the current claims user.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Replaces client-local favorites for logged-in users. The current user is resolved only from authenticated claims; no route, query,
        /// or body user identifier is accepted.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/products/favorites?pageNumber=1&amp;pageSize=25
        /// </code>
        /// </example>
        /// <response code="200">Returns the current user's favorite products.</response>
        /// <response code="400">If pagination values are invalid.</response>
        /// <response code="401">If the current claims user cannot be resolved.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeFavoriteDrugSummariesAsync"/>
        /// <seealso cref="AeDrugSummaryDto"/>
        [Authorize(Policy = "ApiAccess")]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [HttpGet("products/favorites")]
        [ProducesResponseType(typeof(List<AeDrugSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<List<AeDrugSummaryDto>>> GetFavoriteProducts(
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            var pagingValidation = validatePagingParameters(ref pageNumber, ref pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            try
            {
                var authenticatedUser = await getAuthenticatedDashboardUserAsync();
                if (authenticatedUser.ErrorResult != null)
                {
                    return authenticatedUser.ErrorResult;
                }

                var results = await DtoLabelAccess.GetAeFavoriteDrugSummariesAsync(
                    _dbContext,
                    authenticatedUser.DashboardUser!.Id,
                    _pkSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                addPaginationHeaders(pageNumber, pageSize, results.Count);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard favorite products.");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving AE dashboard favorite products.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds an AE dashboard product to the authenticated user's favorites.
        /// </summary>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <returns>No content when favorite state is saved, or status information when the request is invalid.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Persists the product-picker star state for the current claims user. The user identifier is resolved server-side from claims and is
        /// never accepted from the client.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// PUT /api/AdverseEvent/products/11111111-1111-1111-1111-111111111111/favorite
        /// </code>
        /// </example>
        /// <response code="204">Favorite state was saved or was already present.</response>
        /// <response code="400">If the document GUID is empty.</response>
        /// <response code="401">If the current claims user cannot be resolved.</response>
        /// <response code="404">If the document is not present in the AE dashboard summary source.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.SetAeProductFavoriteAsync"/>
        /// <seealso cref="AspNetUserFavorite"/>
        [Authorize(Policy = "ApiAccess")]
        [ServiceFilter(typeof(ActivityLogActionFilter))]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [HttpPut("products/{documentGuid:guid}/favorite")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> FavoriteProduct(Guid documentGuid)
        {
            #region implementation

            return await setFavoriteStateAsync(documentGuid, true);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Removes an AE dashboard product from the authenticated user's favorites.
        /// </summary>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <returns>No content when favorite state is removed, or status information when the request is invalid.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Removes the product-picker star state for the current claims user. The user identifier is resolved server-side from claims and is
        /// never accepted from the client.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// DELETE /api/AdverseEvent/products/11111111-1111-1111-1111-111111111111/favorite
        /// </code>
        /// </example>
        /// <response code="204">Favorite state was removed or was already absent.</response>
        /// <response code="400">If the document GUID is empty.</response>
        /// <response code="401">If the current claims user cannot be resolved.</response>
        /// <response code="404">If the document is not present in the AE dashboard summary source.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.SetAeProductFavoriteAsync"/>
        /// <seealso cref="AspNetUserFavorite"/>
        [Authorize(Policy = "ApiAccess")]
        [ServiceFilter(typeof(ActivityLogActionFilter))]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [HttpDelete("products/{documentGuid:guid}/favorite")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> UnfavoriteProduct(Guid documentGuid)
        {
            #region implementation

            return await setFavoriteStateAsync(documentGuid, false);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the tiered AE dashboard triage view for one product.
        /// </summary>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="comparator">Optional comparator filter; omit to include all rows.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be included.</param>
        /// <returns>A tiered triage view for counseling-priority dashboard rendering.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Replaces the prototype counseling-priority tab and returns product context plus tiered AE signals.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/products/11111111-1111-1111-1111-111111111111/triage?comparator=Placebo&amp;includeFragile=false
        /// </code>
        /// </example>
        /// <response code="200">Returns a triage view.</response>
        /// <response code="400">If the document GUID is empty.</response>
        /// <response code="404">If the document is not dashboard-ready.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeTriageViewAsync"/>
        /// <seealso cref="AeTriageViewDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("products/{documentGuid:guid}/triage")]
        [ProducesResponseType(typeof(AeTriageViewDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<AeTriageViewDto>> GetTriage(
            Guid documentGuid,
            [FromQuery] AeComparatorMix? comparator,
            [FromQuery] bool includeFragile = true)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            if (documentGuid == Guid.Empty)
            {
                return BadRequest("Document GUID cannot be empty.");
            }

            try
            {
                var result = await DtoLabelAccess.GetAeTriageViewAsync(
                    _dbContext,
                    documentGuid,
                    _pkSecret,
                    _logger,
                    comparator,
                    includeFragile);

                if (result == null)
                {
                    return NotFound("The requested product is not available in the AE dashboard.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard triage view for {DocumentGuid}.", documentGuid);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the AE dashboard triage view.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the AE dashboard forest plot view for one product.
        /// </summary>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="comparator">Optional comparator filter; omit to include all rows.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be included.</param>
        /// <returns>A forest plot DTO with sorted signals and static axis ticks.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Replaces the prototype forest plot tab and returns chart-ready AE signal rows.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/products/11111111-1111-1111-1111-111111111111/forest?includeFragile=true
        /// </code>
        /// </example>
        /// <response code="200">Returns a forest plot view.</response>
        /// <response code="400">If the document GUID is empty.</response>
        /// <response code="404">If the document is not dashboard-ready.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeForestPlotAsync"/>
        /// <seealso cref="AeForestPlotDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("products/{documentGuid:guid}/forest")]
        [ProducesResponseType(typeof(AeForestPlotDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<AeForestPlotDto>> GetForest(
            Guid documentGuid,
            [FromQuery] AeComparatorMix? comparator,
            [FromQuery] bool includeFragile = true)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            if (documentGuid == Guid.Empty)
            {
                return BadRequest("Document GUID cannot be empty.");
            }

            try
            {
                var result = await DtoLabelAccess.GetAeForestPlotAsync(
                    _dbContext,
                    documentGuid,
                    _pkSecret,
                    _logger,
                    comparator,
                    includeFragile);

                if (result == null)
                {
                    return NotFound("The requested product is not available in the AE dashboard.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard forest view for {DocumentGuid}.", documentGuid);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the AE dashboard forest view.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the AE dashboard quadrant view for one product.
        /// </summary>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="comparator">Optional comparator filter; omit to include all rows.</param>
        /// <param name="includeFragile">Whether fragile-precision rows should be included.</param>
        /// <returns>A quadrant view DTO with clamped coordinates and bubble sizes.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Replaces the prototype risk-vs-precision tab and returns chart-ready quadrant points.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/products/11111111-1111-1111-1111-111111111111/quadrant?comparator=Active
        /// </code>
        /// </example>
        /// <response code="200">Returns a quadrant view.</response>
        /// <response code="400">If the document GUID is empty.</response>
        /// <response code="404">If the document is not dashboard-ready.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeQuadrantViewAsync"/>
        /// <seealso cref="AeQuadrantViewDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("products/{documentGuid:guid}/quadrant")]
        [ProducesResponseType(typeof(AeQuadrantViewDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<AeQuadrantViewDto>> GetQuadrant(
            Guid documentGuid,
            [FromQuery] AeComparatorMix? comparator,
            [FromQuery] bool includeFragile = true)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            if (documentGuid == Guid.Empty)
            {
                return BadRequest("Document GUID cannot be empty.");
            }

            try
            {
                var result = await DtoLabelAccess.GetAeQuadrantViewAsync(
                    _dbContext,
                    documentGuid,
                    _pkSecret,
                    _logger,
                    comparator,
                    includeFragile);

                if (result == null)
                {
                    return NotFound("The requested product is not available in the AE dashboard.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard quadrant view for {DocumentGuid}.", documentGuid);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the AE dashboard quadrant view.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets reverse-lookup AE dashboard matches for one symptom term.
        /// </summary>
        /// <param name="symptom">Adverse-event term to search for.</param>
        /// <param name="documentGuids">Optional repeated product scope parameters limiting candidate documents.</param>
        /// <returns>A reverse-lookup result containing matching products and AE signals.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Replaces the prototype reverse-lookup term index and can optionally scope results to selected regimen products through repeated
        /// <c>documentGuids</c> query parameters.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/reverse-lookup?symptom=nausea
        /// GET /api/AdverseEvent/reverse-lookup?symptom=nausea&amp;documentGuids=11111111-1111-1111-1111-111111111111&amp;documentGuids=22222222-2222-2222-2222-222222222222
        /// </code>
        /// </example>
        /// <response code="200">Returns reverse-lookup matches, including an empty match collection for valid terms with no matches.</response>
        /// <response code="400">If symptom is empty.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeReverseLookupAsync"/>
        /// <seealso cref="AeReverseLookupResultDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("reverse-lookup")]
        [ProducesResponseType(typeof(AeReverseLookupResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<AeReverseLookupResultDto>> GetReverseLookup(
            [FromQuery] string? symptom,
            [FromQuery] List<Guid>? documentGuids)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            if (string.IsNullOrWhiteSpace(symptom))
            {
                return BadRequest("Symptom is required.");
            }

            try
            {
                var result = await DtoLabelAccess.GetAeReverseLookupAsync(
                    _dbContext,
                    symptom,
                    _pkSecret,
                    _logger,
                    documentGuids);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard reverse lookup for {Symptom}.", symptom);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving AE dashboard reverse lookup matches.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an AE dashboard interchange comparison for two products.
        /// </summary>
        /// <param name="documentGuidA">SPL document identifier for product A.</param>
        /// <param name="documentGuidB">SPL document identifier for product B.</param>
        /// <param name="differencesOnly">Whether rows classified as similar should be removed.</param>
        /// <returns>An interchange comparison between two dashboard products.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Replaces the prototype cross-product therapeutic interchange panel and compares AE signals across two dashboard-ready products.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/interchange?documentGuidA=11111111-1111-1111-1111-111111111111&amp;documentGuidB=22222222-2222-2222-2222-222222222222&amp;differencesOnly=true
        /// </code>
        /// </example>
        /// <response code="200">Returns an interchange comparison.</response>
        /// <response code="400">If either GUID is empty or both GUIDs are identical.</response>
        /// <response code="404">If either product is not dashboard-ready.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeInterchangeAsync"/>
        /// <seealso cref="AeInterchangeComparisonDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("interchange")]
        [ProducesResponseType(typeof(AeInterchangeComparisonDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<AeInterchangeComparisonDto>> GetInterchange(
            [FromQuery] Guid documentGuidA,
            [FromQuery] Guid documentGuidB,
            [FromQuery] bool differencesOnly = false)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            if (documentGuidA == Guid.Empty || documentGuidB == Guid.Empty)
            {
                return BadRequest("Both document GUIDs are required.");
            }

            if (documentGuidA == documentGuidB)
            {
                return BadRequest("Document GUIDs must identify two different products.");
            }

            try
            {
                var result = await DtoLabelAccess.GetAeInterchangeAsync(
                    _dbContext,
                    documentGuidA,
                    documentGuidB,
                    _pkSecret,
                    _logger,
                    differencesOnly);

                if (result == null)
                {
                    return NotFound("One or both requested products are not available in the AE dashboard.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving AE dashboard interchange comparison for {DocumentGuidA} and {DocumentGuidB}.",
                    documentGuidA,
                    documentGuidB);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the AE dashboard interchange comparison.");
            }

            #endregion
        }

        #endregion public dashboard actions

        #region private helpers

        /**************************************************************/
        /// <summary>
        /// Determines whether the AE dashboard API surface is enabled by configuration.
        /// </summary>
        /// <returns>True when the dashboard is enabled; otherwise false.</returns>
        /// <remarks>
        /// The feature defaults to enabled to match the existing dashboard data-access and settings behavior.
        /// </remarks>
        /// <seealso cref="IConfiguration"/>
        private bool isAeDashboardEnabled()
        {
            #region implementation

            return _configuration.GetValue<bool>("FeatureFlags:AeDashboard:Enabled", true);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the consistent service-unavailable response used when the AE dashboard feature is disabled.
        /// </summary>
        /// <returns>A 503 Service Unavailable response.</returns>
        /// <seealso cref="StatusCodes.Status503ServiceUnavailable"/>
        private ObjectResult disabledResult()
        {
            #region implementation

            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "The AE dashboard feature is disabled.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the encrypted current-user identifier from the authenticated claims principal.
        /// </summary>
        /// <returns>The encrypted user identifier from the current request claims.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when the claims principal does not contain a valid numeric user identifier.</exception>
        /// <remarks>
        /// The helper mirrors <see cref="UsersController"/> claim resolution so favorites are always bound to the acting user from the token or
        /// identity cookie.
        /// </remarks>
        /// <seealso cref="ClaimHelper.GetEncryptedUserIdOrThrow"/>
        private string getEncryptedIdFromClaim()
        {
            #region implementation

            try
            {
                return ClaimHelper.GetEncryptedUserIdOrThrow(User.Claims, _pkSecret);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get encrypted id from claims for AE dashboard favorite access.");
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves the authenticated dashboard user from request claims.
        /// </summary>
        /// <returns>A tuple containing the resolved user, or the response that should be returned when resolution fails.</returns>
        /// <remarks>
        /// Missing, malformed, undecryptable, or stale user claims are treated as 401 responses. Database failures remain server errors.
        /// </remarks>
        /// <seealso cref="UserDataAccess.GetByIdAsync"/>
        private async Task<(User? DashboardUser, ActionResult? ErrorResult)> getAuthenticatedDashboardUserAsync()
        {
            #region implementation

            if (User?.Identity?.IsAuthenticated != true)
            {
                return (null, Unauthorized("Authentication is required for AE dashboard favorites."));
            }

            string encryptedClaimUserId;
            try
            {
                encryptedClaimUserId = getEncryptedIdFromClaim();
            }
            catch (UnauthorizedAccessException)
            {
                return (null, Unauthorized("Unable to determine user ID from authentication context."));
            }
            catch (Exception)
            {
                return (null, Unauthorized("Unable to determine user ID from authentication context."));
            }

            try
            {
                var claimsUser = await _userDataAccess.GetByIdAsync(encryptedClaimUserId);
                if (claimsUser == null)
                {
                    return (null, Unauthorized("Unable to identify the acting user from the provided token."));
                }

                return (claimsUser, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolving AE dashboard claims user.");
                return (null, StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while resolving the authenticated user."));
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to resolve an optional authenticated user for public product catalog favorite enrichment.
        /// </summary>
        /// <returns>A tuple containing a user ID when the caller is authenticated, or an error response when an authenticated principal is invalid.</returns>
        /// <remarks>
        /// Anonymous requests return a null user ID and no error. Authenticated requests must resolve to a current user so stale or malformed
        /// tokens do not silently become anonymous catalog reads.
        /// </remarks>
        /// <seealso cref="getAuthenticatedDashboardUserAsync"/>
        private async Task<(long? UserId, ActionResult? ErrorResult)> tryGetOptionalDashboardUserIdAsync()
        {
            #region implementation

            if (User?.Identity?.IsAuthenticated != true)
            {
                return (null, null);
            }

            var authenticatedUser = await getAuthenticatedDashboardUserAsync();
            if (authenticatedUser.ErrorResult != null)
            {
                return (null, authenticatedUser.ErrorResult);
            }

            return (authenticatedUser.DashboardUser!.Id, null);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds or removes favorite state for the current claims user.
        /// </summary>
        /// <param name="documentGuid">SPL document identifier for the dashboard product.</param>
        /// <param name="isFavorite">True to add the favorite; false to remove it.</param>
        /// <returns>The HTTP result for the favorite mutation.</returns>
        /// <remarks>
        /// Shared mutation logic keeps add and remove security behavior identical and ensures both actions use only the authenticated claims user.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.SetAeProductFavoriteAsync"/>
        private async Task<IActionResult> setFavoriteStateAsync(Guid documentGuid, bool isFavorite)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            if (documentGuid == Guid.Empty)
            {
                return BadRequest("Document GUID cannot be empty.");
            }

            try
            {
                var authenticatedUser = await getAuthenticatedDashboardUserAsync();
                if (authenticatedUser.ErrorResult != null)
                {
                    return authenticatedUser.ErrorResult;
                }

                var saved = await DtoLabelAccess.SetAeProductFavoriteAsync(
                    _dbContext,
                    authenticatedUser.DashboardUser!.Id,
                    documentGuid,
                    isFavorite,
                    _logger);

                if (!saved)
                {
                    return NotFound("The requested product is not available in the AE dashboard.");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting AE dashboard favorite state for {DocumentGuid}.", documentGuid);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while updating AE dashboard favorite state.");
            }

            #endregion
        }

        #endregion private helpers
    }
}
