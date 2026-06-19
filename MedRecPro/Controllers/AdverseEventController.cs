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
        /// <param name="sharedSignalsOnly">Whether rows without signals on both products should be removed.</param>
        /// <param name="comparator">Optional comparator filter. Omit or use <see cref="AeComparatorMix.Both"/> to include all comparator strata.</param>
        /// <returns>An interchange comparison between two dashboard products.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Replaces the prototype cross-product therapeutic interchange panel and compares AE signals across two dashboard-ready products.
        /// Comparator scoping matches the forest and quadrant endpoints so the
        /// dashboard can compare products against the same placebo, active-comparator,
        /// or mixed-strata evidence view selected elsewhere.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/interchange?documentGuidA=11111111-1111-1111-1111-111111111111&amp;documentGuidB=22222222-2222-2222-2222-222222222222&amp;differencesOnly=true&amp;sharedSignalsOnly=true&amp;comparator=Placebo
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
            [FromQuery] bool differencesOnly = false,
            [FromQuery] bool sharedSignalsOnly = false,
            [FromQuery] AeComparatorMix? comparator = null)
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
                    differencesOnly,
                    sharedSignalsOnly,
                    comparator);

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

        /**************************************************************/
        /// <summary>
        /// Gets the SOC × SOC adverse-event correlation map for one pharmacologic class.
        /// </summary>
        /// <param name="pharmClassCode">Pharmacologic class code (the public dashboard text form, like a UNII).</param>
        /// <param name="comparator">Comparator mix; defaults to placebo-controlled only.</param>
        /// <param name="includeNonSignificant">Whether RR-non-significant rows are kept before correlating.</param>
        /// <param name="excludeFragile">Whether fragile/wide-CI rows are dropped before correlating.</param>
        /// <param name="minDrugsPerCell">Minimum drugs a cell needs for a coefficient (server floor 3).</param>
        /// <param name="method">Correlation method; Spearman by default.</param>
        /// <param name="aggregation">Within-SOC per-drug aggregation; median LogRR by default.</param>
        /// <param name="seriousSocOnly">Whether the SOC axis is restricted to serious organ systems.</param>
        /// <param name="excludeCombos">Whether combination-product rows are dropped.</param>
        /// <param name="minEvents">Minimum total events a row needs to count.</param>
        /// <returns>The class-scoped correlation map.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Within a pharmacologic class, correlates each pair of MedDRA System Organ Classes on
        /// their per-drug LogRR profiles. The observation unit is a drug, so cells are usually
        /// thin; the response defaults to placebo-only, fragile-excluded, Spearman over median
        /// LogRR with a drugs-per-cell floor, and returns per-cell `n`, null thin cells, and
        /// honesty `Warnings` so the map renders truthfully.
        ///
        /// ## Backend notes
        /// This is a backend/API-only feature; no client renders it yet. Gotchas a consumer must
        /// know: cells below `minDrugsPerCell` return `Coefficient=null` (`InsufficientN=true`);
        /// pairwise deletion means the matrix is **not guaranteed positive semi-definite**, so do
        /// not cluster or run PCA on it without repair; `comparator=Both` mixes placebo and active
        /// estimands and adds a warning; `seriousSocOnly` is an approximate SOC keyword match;
        /// `includeNonSignificant=false` drops rows **before** aggregation/correlation; and
        /// `minEvents` is the total of treatment + comparator events for a row.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/correlation?pharmClassCode=N0000175076&amp;comparator=Placebo&amp;minDrugsPerCell=3
        /// </code>
        /// </example>
        /// <response code="200">Returns the correlation map.</response>
        /// <response code="400">If the pharmacologic class code is empty.</response>
        /// <response code="404">If the class has no AE rows for the dashboard.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationMapAsync"/>
        /// <seealso cref="AeCorrelationMapDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("correlation")]
        [ProducesResponseType(typeof(AeCorrelationMapDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<AeCorrelationMapDto>> GetCorrelationMap(
            [FromQuery] string? pharmClassCode,
            [FromQuery] AeComparatorMix? comparator,
            [FromQuery] bool includeNonSignificant = true,
            [FromQuery] bool excludeFragile = true,
            [FromQuery] int minDrugsPerCell = 4,
            [FromQuery] AeCorrelationMethod method = AeCorrelationMethod.Spearman,
            [FromQuery] AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            [FromQuery] bool seriousSocOnly = false,
            [FromQuery] bool excludeCombos = false,
            [FromQuery] int minEvents = 0)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            if (string.IsNullOrWhiteSpace(pharmClassCode))
            {
                return BadRequest("Pharmacologic class code is required.");
            }

            var filterValidation = validateCorrelationFilters(comparator, aggregation, minEvents, method);
            if (filterValidation != null)
            {
                return filterValidation;
            }

            try
            {
                var result = await DtoLabelAccess.GetAeCorrelationMapAsync(
                    _dbContext,
                    pharmClassCode.Trim(),
                    _pkSecret,
                    _logger,
                    comparator ?? AeComparatorMix.Placebo,
                    includeNonSignificant,
                    excludeFragile,
                    minDrugsPerCell,
                    method,
                    aggregation,
                    seriousSocOnly,
                    excludeCombos,
                    minEvents);

                if (result == null)
                {
                    return NotFound("The requested pharmacologic class is not available in the AE dashboard.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard correlation map for {PharmClassCode}.", pharmClassCode);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the AE dashboard correlation map.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the pharmacologic classes that have AE rows, for the correlation class picker.
        /// </summary>
        /// <param name="classSearch">Optional class code or name search text.</param>
        /// <param name="pageNumber">Optional 1-based page number.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <param name="comparator">Comparator mix; defaults to placebo-controlled only.</param>
        /// <param name="includeNonSignificant">Whether RR-non-significant rows are kept before class renderability checks.</param>
        /// <param name="excludeFragile">Whether fragile/wide-CI rows are dropped before class renderability checks.</param>
        /// <param name="excludeCombos">Whether combination-product rows are dropped before class renderability checks.</param>
        /// <param name="minEvents">Minimum total events a row needs to count.</param>
        /// <param name="minDrugsPerCell">Minimum drugs an off-diagonal SOC pair needs to render (server floor 3).</param>
        /// <returns>Pharmacologic class picker items ordered map-ready-first.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Backs the correlation class picker. Scoped to classes that actually have AE risk rows
        /// and flags which classes have at least one off-diagonal SOC pair that can render at the
        /// active/default map floor.
        /// The <c>X-Chartable-Count</c> response header reports matching classes with at least one
        /// renderable map cell under the active filters.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/correlation/classes?classSearch=kinase&amp;pageNumber=1&amp;pageSize=25&amp;minDrugsPerCell=4
        /// </code>
        /// </example>
        /// <response code="200">Returns the pharmacologic class picker items.</response>
        /// <response code="400">If pagination values are invalid.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationClassesAsync"/>
        /// <seealso cref="AePharmClassPickerItemDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("correlation/classes")]
        [ProducesResponseType(typeof(List<AePharmClassPickerItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<List<AePharmClassPickerItemDto>>> GetCorrelationClasses(
            [FromQuery] string? classSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] AeComparatorMix? comparator = null,
            [FromQuery] bool includeNonSignificant = true,
            [FromQuery] bool excludeFragile = true,
            [FromQuery] bool excludeCombos = false,
            [FromQuery] int minEvents = 0,
            [FromQuery] int minDrugsPerCell = 4)
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

            var filterValidation = validateCorrelationFilters(
                comparator,
                AeCorrelationAggregation.MedianLogRr,
                minEvents);
            if (filterValidation != null)
            {
                return filterValidation;
            }

            try
            {
                var results = await DtoLabelAccess.GetAeCorrelationClassesAsync(
                    _dbContext,
                    _pkSecret,
                    _logger,
                    classSearch,
                    pageNumber,
                    pageSize,
                    comparator ?? AeComparatorMix.Placebo,
                    includeNonSignificant,
                    excludeFragile,
                    excludeCombos,
                    minEvents,
                    minDrugsPerCell);

                addPaginationHeaders(pageNumber, pageSize, results.TotalCount);
                Response.Headers.Append("X-Chartable-Count", results.ChartableCount.ToString());
                return Ok(results.Items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard correlation classes.");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving AE dashboard correlation classes.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets MedDRA System Organ Classes available for system-scoped class correlation.
        /// </summary>
        /// <param name="systemSearch">Optional SOC display-name search text.</param>
        /// <param name="pageNumber">Optional 1-based page number.</param>
        /// <param name="pageSize">Optional page size.</param>
        /// <param name="comparator">Comparator mix; defaults to placebo-controlled only.</param>
        /// <param name="includeNonSignificant">Whether RR-non-significant rows are kept before renderability checks.</param>
        /// <param name="excludeFragile">Whether fragile/wide-CI rows are dropped before renderability checks.</param>
        /// <param name="excludeCombos">Whether combination-product rows are dropped before renderability checks.</param>
        /// <param name="minEvents">Minimum total events a row needs to count.</param>
        /// <param name="minTermsPerCell">Minimum shared selected-SOC terms a class-pair cell needs to render.</param>
        /// <returns>MedDRA system picker items ordered map-ready-first.</returns>
        /// <remarks>
        /// Backs the inverse correlation picker. The system key is the canonical
        /// <c>ParameterCategory</c> value already present in AE risk rows. The
        /// <c>X-Chartable-Count</c> response header reports matching systems with at least one
        /// renderable class-pair map cell under the active filters.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/correlation/systems?systemSearch=cardiac&amp;pageNumber=1&amp;pageSize=25
        /// </code>
        /// </example>
        /// <response code="200">Returns the MedDRA system picker items.</response>
        /// <response code="400">If pagination or filter values are invalid.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationSystemsAsync"/>
        /// <seealso cref="AeMeddraSystemPickerItemDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("correlation/systems")]
        [ProducesResponseType(typeof(List<AeMeddraSystemPickerItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<List<AeMeddraSystemPickerItemDto>>> GetCorrelationSystems(
            [FromQuery] string? systemSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize,
            [FromQuery] AeComparatorMix? comparator = null,
            [FromQuery] bool includeNonSignificant = true,
            [FromQuery] bool excludeFragile = true,
            [FromQuery] bool excludeCombos = false,
            [FromQuery] int minEvents = 0,
            [FromQuery] int minTermsPerCell = 4)
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

            var filterValidation = validateSystemCorrelationFilters(comparator, AeCorrelationAggregation.MedianLogRr, minEvents);
            if (filterValidation != null)
            {
                return filterValidation;
            }

            try
            {
                var results = await DtoLabelAccess.GetAeCorrelationSystemsAsync(
                    _dbContext,
                    _pkSecret,
                    _logger,
                    systemSearch,
                    pageNumber,
                    pageSize,
                    comparator ?? AeComparatorMix.Placebo,
                    includeNonSignificant,
                    excludeFragile,
                    excludeCombos,
                    minEvents,
                    minTermsPerCell);

                addPaginationHeaders(pageNumber, pageSize, results.TotalCount);
                Response.Headers.Append("X-Chartable-Count", results.ChartableCount.ToString());
                return Ok(results.Items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard correlation systems.");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving AE dashboard correlation systems.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets a system-scoped pharmacologic-class correlation map.
        /// </summary>
        /// <param name="systems">Selected MedDRA System Organ Class. Repeated query keys and comma-separated values are accepted for one value only.</param>
        /// <param name="classSearch">Optional class code or name search before axis paging.</param>
        /// <param name="classPageNumber">Optional 1-based class-axis page number.</param>
        /// <param name="classPageSize">Optional class-axis page size.</param>
        /// <param name="comparator">Comparator mix; defaults to placebo-controlled only.</param>
        /// <param name="includeNonSignificant">Whether RR-non-significant rows are kept before aggregation.</param>
        /// <param name="excludeFragile">Whether fragile/wide-CI rows are dropped before aggregation.</param>
        /// <param name="minTermsPerCell">Minimum shared selected-SOC terms a cell needs for a coefficient.</param>
        /// <param name="method">Correlation method; Spearman by default.</param>
        /// <param name="aggregation">Within-class term aggregation; median LogRR by default.</param>
        /// <param name="excludeCombos">Whether combination-product rows are dropped.</param>
        /// <param name="minEvents">Minimum total events a row needs to count.</param>
        /// <param name="includeFullMatrix">Whether to ignore class-axis paging and return every filtered class-pair cell.</param>
        /// <returns>The system-scoped class correlation map.</returns>
        /// <remarks>
        /// For one selected MedDRA system (<c>ParameterCategory</c> value), correlates
        /// pharmacologic classes over selected-SOC adverse-event term profiles, not shared
        /// drugs. By default the returned matrix is symmetric and page-windowed on one class
        /// axis. Set <c>includeFullMatrix=true</c> to ignore class-axis paging and return the
        /// complete filtered square matrix. <c>comparator=Both</c> mixes estimands and is
        /// warned; pairwise deletion means the matrix is not guaranteed positive semi-definite;
        /// <c>includeNonSignificant=false</c> drops rows before aggregation.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/correlation/systems/map?systems=Cardiac%20Disorders&amp;classPageSize=20
        /// GET /api/AdverseEvent/correlation/systems/map?systems=Cardiac%20Disorders&amp;includeFullMatrix=true
        /// </code>
        /// </example>
        /// <response code="200">Returns the system-scoped class correlation map.</response>
        /// <response code="400">If the selected system, paging, or filters are invalid.</response>
        /// <response code="404">If the selected system has no usable AE dashboard rows.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeSystemCorrelationMapAsync"/>
        /// <seealso cref="AeSystemClassCorrelationMapDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("correlation/systems/map")]
        [ProducesResponseType(typeof(AeSystemClassCorrelationMapDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<AeSystemClassCorrelationMapDto>> GetSystemCorrelationMap(
            [FromQuery] List<string>? systems,
            [FromQuery] string? classSearch,
            [FromQuery] int? classPageNumber,
            [FromQuery] int? classPageSize,
            [FromQuery] AeComparatorMix? comparator = null,
            [FromQuery] bool includeNonSignificant = true,
            [FromQuery] bool excludeFragile = true,
            [FromQuery] int minTermsPerCell = 4,
            [FromQuery] AeCorrelationMethod method = AeCorrelationMethod.Spearman,
            [FromQuery] AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            [FromQuery] bool excludeCombos = false,
            [FromQuery] int minEvents = 0,
            [FromQuery] bool includeFullMatrix = false)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            var selectedSystems = normalizeSystemQueryValues(systems);
            var systemValidation = validateSingleSelectedSystem(selectedSystems);
            if (systemValidation != null)
            {
                return systemValidation;
            }

            if (includeFullMatrix)
            {
                classPageNumber = 1;
                classPageSize ??= 20;
            }
            else
            {
                var pagingValidation = validateBoundedPagingParameters(ref classPageNumber, ref classPageSize, 20, 100, "class");
                if (pagingValidation != null)
                {
                    return pagingValidation;
                }
            }

            var filterValidation = validateSystemCorrelationFilters(comparator, aggregation, minEvents, method);
            if (filterValidation != null)
            {
                return filterValidation;
            }

            try
            {
                var result = await DtoLabelAccess.GetAeSystemCorrelationMapAsync(
                    _dbContext,
                    selectedSystems,
                    _pkSecret,
                    _logger,
                    classSearch,
                    classPageNumber!.Value,
                    classPageSize!.Value,
                    comparator ?? AeComparatorMix.Placebo,
                    includeNonSignificant,
                    excludeFragile,
                    minTermsPerCell,
                    method,
                    aggregation,
                    excludeCombos,
                    minEvents,
                    includeFullMatrix);

                if (result == null)
                {
                    return NotFound("The selected MedDRA system is not available in the AE dashboard.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard system correlation map for {Systems}.", string.Join(", ", selectedSystems));
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the AE dashboard system correlation map.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets a sparse system-scoped pharmacologic-class x drug heatmap.
        /// </summary>
        /// <param name="systems">Selected MedDRA System Organ Class. Repeated query keys and comma-separated values are accepted for one value only.</param>
        /// <param name="classSearch">Optional class code or name search.</param>
        /// <param name="drugSearch">Optional product or substance search.</param>
        /// <param name="classPageNumber">Optional 1-based class-row page number.</param>
        /// <param name="classPageSize">Optional class-row page size.</param>
        /// <param name="drugPageNumber">Optional 1-based drug-column page number.</param>
        /// <param name="drugPageSize">Optional drug-column page size.</param>
        /// <param name="comparator">Comparator mix; defaults to placebo-controlled only.</param>
        /// <param name="includeNonSignificant">Whether RR-non-significant rows are kept.</param>
        /// <param name="excludeFragile">Whether fragile/wide-CI rows are dropped.</param>
        /// <param name="aggregation">Within-class/drug aggregation; median LogRR by default.</param>
        /// <param name="excludeCombos">Whether combination-product rows are dropped.</param>
        /// <param name="minEvents">Minimum total events a row needs to count.</param>
        /// <returns>The sparse system-scoped class x drug heatmap.</returns>
        /// <remarks>
        /// Rows are pharmacologic classes and columns are drugs for one selected MedDRA system,
        /// both independently paged in the
        /// response body. Cells are sparse: absent class/drug intersections are omitted rather
        /// than serialized as null placeholders. Paging can hide classes or drugs outside the
        /// returned window.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/correlation/systems/heatmap?systems=Cardiac%20Disorders&amp;drugPageSize=50
        /// </code>
        /// </example>
        /// <response code="200">Returns the sparse heatmap.</response>
        /// <response code="400">If the selected system, paging, or filters are invalid.</response>
        /// <response code="404">If the selected system has no usable AE dashboard rows.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeSystemCorrelationHeatmapAsync"/>
        /// <seealso cref="AeSystemClassHeatmapDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("correlation/systems/heatmap")]
        [ProducesResponseType(typeof(AeSystemClassHeatmapDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<AeSystemClassHeatmapDto>> GetSystemCorrelationHeatmap(
            [FromQuery] List<string>? systems,
            [FromQuery] string? classSearch,
            [FromQuery] string? drugSearch,
            [FromQuery] int? classPageNumber,
            [FromQuery] int? classPageSize,
            [FromQuery] int? drugPageNumber,
            [FromQuery] int? drugPageSize,
            [FromQuery] AeComparatorMix? comparator = null,
            [FromQuery] bool includeNonSignificant = true,
            [FromQuery] bool excludeFragile = true,
            [FromQuery] AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            [FromQuery] bool excludeCombos = false,
            [FromQuery] int minEvents = 0)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            var selectedSystems = normalizeSystemQueryValues(systems);
            var systemValidation = validateSingleSelectedSystem(selectedSystems);
            if (systemValidation != null)
            {
                return systemValidation;
            }

            var classPagingValidation = validateBoundedPagingParameters(ref classPageNumber, ref classPageSize, 40, 100, "class");
            if (classPagingValidation != null)
            {
                return classPagingValidation;
            }

            var drugPagingValidation = validateBoundedPagingParameters(ref drugPageNumber, ref drugPageSize, 50, 200, "drug");
            if (drugPagingValidation != null)
            {
                return drugPagingValidation;
            }

            var filterValidation = validateSystemCorrelationFilters(comparator, aggregation, minEvents);
            if (filterValidation != null)
            {
                return filterValidation;
            }

            try
            {
                var result = await DtoLabelAccess.GetAeSystemCorrelationHeatmapAsync(
                    _dbContext,
                    selectedSystems,
                    _pkSecret,
                    _logger,
                    classSearch,
                    drugSearch,
                    classPageNumber!.Value,
                    classPageSize!.Value,
                    drugPageNumber!.Value,
                    drugPageSize!.Value,
                    comparator ?? AeComparatorMix.Placebo,
                    includeNonSignificant,
                    excludeFragile,
                    aggregation,
                    excludeCombos,
                    minEvents);

                if (result == null)
                {
                    return NotFound("The selected MedDRA system is not available in the AE dashboard.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard system heatmap for {Systems}.", string.Join(", ", selectedSystems));
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the AE dashboard system heatmap.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the per-term detail behind one system-scoped class-pair cell.
        /// </summary>
        /// <param name="systems">Selected MedDRA System Organ Class. Repeated query keys and comma-separated values are accepted for one value only.</param>
        /// <param name="classX">Row pharmacologic class code.</param>
        /// <param name="classY">Column pharmacologic class code.</param>
        /// <param name="comparator">Comparator mix; defaults to placebo-controlled only.</param>
        /// <param name="includeNonSignificant">Whether RR-non-significant rows are kept.</param>
        /// <param name="excludeFragile">Whether fragile/wide-CI rows are dropped.</param>
        /// <param name="minTermsPerCell">Minimum shared terms for the map-safe coefficient.</param>
        /// <param name="method">Correlation method used for the recomputed coefficient.</param>
        /// <param name="aggregation">Within-class/term aggregation; median LogRR by default.</param>
        /// <param name="excludeCombos">Whether combination-product rows are dropped.</param>
        /// <param name="minEvents">Minimum total events a row needs to count.</param>
        /// <param name="pageNumber">Optional 1-based term-pair page number.</param>
        /// <param name="pageSize">Optional term-pair page size.</param>
        /// <returns>The class-pair cell detail with shared selected-SOC term pairs.</returns>
        /// <remarks>
        /// Mirrors the map's single-system honesty contract. <c>Coefficient</c> is map-safe and suppressed
        /// below <c>minTermsPerCell</c>; <c>RawCoefficient</c> remains available for diagnostics.
        /// Diagonal cells are non-informative and forced to 1.0. Missing shared terms return
        /// warnings and empty <c>TermPairs</c> rather than a server error.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/correlation/systems/cell?systems=Cardiac%20Disorders&amp;classX=A&amp;classY=B
        /// </code>
        /// </example>
        /// <response code="200">Returns the class-pair cell detail.</response>
        /// <response code="400">If the selected system, class codes, paging, or filters are invalid.</response>
        /// <response code="404">If the selected system has no usable AE dashboard rows.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeSystemCorrelationCellDetailAsync"/>
        /// <seealso cref="AeSystemClassCorrelationCellDetailDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("correlation/systems/cell")]
        [ProducesResponseType(typeof(AeSystemClassCorrelationCellDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<AeSystemClassCorrelationCellDetailDto>> GetSystemCorrelationCell(
            [FromQuery] List<string>? systems,
            [FromQuery] string? classX,
            [FromQuery] string? classY,
            [FromQuery] AeComparatorMix? comparator = null,
            [FromQuery] bool includeNonSignificant = true,
            [FromQuery] bool excludeFragile = true,
            [FromQuery] int minTermsPerCell = 4,
            [FromQuery] AeCorrelationMethod method = AeCorrelationMethod.Spearman,
            [FromQuery] AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            [FromQuery] bool excludeCombos = false,
            [FromQuery] int minEvents = 0,
            [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            var selectedSystems = normalizeSystemQueryValues(systems);
            var systemValidation = validateSingleSelectedSystem(selectedSystems);
            if (systemValidation != null)
            {
                return systemValidation;
            }

            if (string.IsNullOrWhiteSpace(classX) || string.IsNullOrWhiteSpace(classY))
            {
                return BadRequest("Both classX and classY are required.");
            }

            var pagingValidation = validateBoundedPagingParameters(ref pageNumber, ref pageSize, 100, 500, "term pair");
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            var filterValidation = validateSystemCorrelationFilters(comparator, aggregation, minEvents, method);
            if (filterValidation != null)
            {
                return filterValidation;
            }

            try
            {
                var result = await DtoLabelAccess.GetAeSystemCorrelationCellDetailAsync(
                    _dbContext,
                    selectedSystems,
                    classX.Trim(),
                    classY.Trim(),
                    _pkSecret,
                    _logger,
                    comparator ?? AeComparatorMix.Placebo,
                    includeNonSignificant,
                    excludeFragile,
                    minTermsPerCell,
                    method,
                    aggregation,
                    excludeCombos,
                    minEvents,
                    pageNumber!.Value,
                    pageSize!.Value);

                if (result == null)
                {
                    return NotFound("The selected MedDRA system is not available in the AE dashboard.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error retrieving AE dashboard system cell for {Systems}, {ClassX}, {ClassY}.",
                    string.Join(", ", selectedSystems),
                    classX,
                    classY);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the AE dashboard system correlation cell detail.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the SOC × drug relative-risk heatmap for one pharmacologic class.
        /// </summary>
        /// <param name="pharmClassCode">Pharmacologic class code (the public dashboard text form).</param>
        /// <param name="comparator">Comparator mix; defaults to placebo-controlled only.</param>
        /// <param name="includeNonSignificant">Whether RR-non-significant rows are kept.</param>
        /// <param name="excludeFragile">Whether fragile/wide-CI rows are dropped.</param>
        /// <param name="aggregation">Within-SOC per-drug aggregation; median LogRR by default.</param>
        /// <param name="seriousSocOnly">Whether the SOC axis is restricted to serious organ systems.</param>
        /// <param name="excludeCombos">Whether combination-product rows are dropped.</param>
        /// <param name="minEvents">Minimum total events a row needs to count.</param>
        /// <returns>The class-scoped SOC × drug heatmap.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// The honest small-n companion to the correlation map: rows are SOCs, columns are drugs,
        /// and each populated cell is an aggregated LogRR. Stays meaningful when a class is too
        /// small to correlate.
        ///
        /// ## Backend notes
        /// Backend/API-only until a client is added. Cells are **sparse**: only populated
        /// `(SOC, drug)` pairs are emitted and the client fills the gaps. The same input filters
        /// as the map apply — `includeNonSignificant=false` drops rows before aggregation,
        /// `seriousSocOnly` is an approximate keyword match, `comparator=Both` mixes estimands
        /// (warned), and `minEvents` counts treatment + comparator events.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/correlation/heatmap?pharmClassCode=N0000175076&amp;comparator=Placebo
        /// </code>
        /// </example>
        /// <response code="200">Returns the heatmap.</response>
        /// <response code="400">If the pharmacologic class code is empty.</response>
        /// <response code="404">If the class has no AE rows for the dashboard.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationHeatmapAsync"/>
        /// <seealso cref="AeCorrelationHeatmapDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("correlation/heatmap")]
        [ProducesResponseType(typeof(AeCorrelationHeatmapDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<AeCorrelationHeatmapDto>> GetCorrelationHeatmap(
            [FromQuery] string? pharmClassCode,
            [FromQuery] AeComparatorMix? comparator,
            [FromQuery] bool includeNonSignificant = true,
            [FromQuery] bool excludeFragile = true,
            [FromQuery] AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            [FromQuery] bool seriousSocOnly = false,
            [FromQuery] bool excludeCombos = false,
            [FromQuery] int minEvents = 0)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            if (string.IsNullOrWhiteSpace(pharmClassCode))
            {
                return BadRequest("Pharmacologic class code is required.");
            }

            var filterValidation = validateCorrelationFilters(comparator, aggregation, minEvents);
            if (filterValidation != null)
            {
                return filterValidation;
            }

            try
            {
                var result = await DtoLabelAccess.GetAeCorrelationHeatmapAsync(
                    _dbContext,
                    pharmClassCode.Trim(),
                    _pkSecret,
                    _logger,
                    comparator ?? AeComparatorMix.Placebo,
                    includeNonSignificant,
                    excludeFragile,
                    aggregation,
                    seriousSocOnly,
                    excludeCombos,
                    minEvents);

                if (result == null)
                {
                    return NotFound("The requested pharmacologic class is not available in the AE dashboard.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard correlation heatmap for {PharmClassCode}.", pharmClassCode);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the AE dashboard correlation heatmap.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the per-drug drill-down behind one SOC × SOC correlation cell.
        /// </summary>
        /// <param name="pharmClassCode">Pharmacologic class code (the public dashboard text form).</param>
        /// <param name="socX">Row SOC of the cell.</param>
        /// <param name="socY">Column SOC of the cell.</param>
        /// <param name="comparator">Comparator mix; defaults to placebo-controlled only.</param>
        /// <param name="includeNonSignificant">Whether RR-non-significant rows are kept.</param>
        /// <param name="excludeFragile">Whether fragile/wide-CI rows are dropped.</param>
        /// <param name="minDrugsPerCell">Minimum drugs for the map-safe coefficient (server floor 3); the raw coefficient ignores it.</param>
        /// <param name="method">Correlation method used for the recomputed coefficient.</param>
        /// <param name="aggregation">Within-SOC per-drug aggregation; median LogRR by default.</param>
        /// <param name="seriousSocOnly">Whether the SOC axis is restricted to serious organ systems.</param>
        /// <param name="excludeCombos">Whether combination-product rows are dropped.</param>
        /// <param name="minEvents">Minimum total events a row needs to count.</param>
        /// <returns>The cell drill-down with the per-drug paired observations.</returns>
        /// <remarks>
        /// ## Dashboard Usage
        /// Mirrors the triage/forest/quadrant drill pattern. Returns the per-drug paired
        /// (SOC X LogRR, SOC Y LogRR) observations the cell's coefficient was computed over, with
        /// encrypted moiety provenance — the answer to "why is this cell 0.9?".
        ///
        /// ## Backend notes
        /// Backend/API-only until a client is added. The drill-down is honest the same way the map
        /// is: `Coefficient` is **map-safe** (null when the cell is below `minDrugsPerCell`, exactly
        /// as the map suppresses it), while `RawCoefficient` is the **diagnostic** unsuppressed
        /// value; `InsufficientN` explains any difference and a warning is added when the floor hides
        /// a value. When `socX == socY` the cell is the non-informative diagonal, forced to 1.0. All
        /// `DrugPairs` are preserved for inspection regardless of the floor.
        ///
        /// The endpoint is disabled when <c>FeatureFlags:AeDashboard:Enabled</c> is false.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/AdverseEvent/correlation/cell?pharmClassCode=N0000175076&amp;socX=Cardiac%20Disorders&amp;socY=Vascular%20Disorders
        /// </code>
        /// </example>
        /// <response code="200">Returns the cell drill-down.</response>
        /// <response code="400">If the class code or either SOC is empty.</response>
        /// <response code="404">If the class has no AE rows for the dashboard.</response>
        /// <response code="500">If an unexpected error occurs.</response>
        /// <response code="503">If the AE dashboard feature is disabled.</response>
        /// <seealso cref="DtoLabelAccess.GetAeCorrelationCellDetailAsync"/>
        /// <seealso cref="AeCorrelationCellDetailDto"/>
        [AllowAnonymous]
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("correlation/cell")]
        [ProducesResponseType(typeof(AeCorrelationCellDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<AeCorrelationCellDetailDto>> GetCorrelationCell(
            [FromQuery] string? pharmClassCode,
            [FromQuery] string? socX,
            [FromQuery] string? socY,
            [FromQuery] AeComparatorMix? comparator,
            [FromQuery] bool includeNonSignificant = true,
            [FromQuery] bool excludeFragile = true,
            [FromQuery] int minDrugsPerCell = 4,
            [FromQuery] AeCorrelationMethod method = AeCorrelationMethod.Spearman,
            [FromQuery] AeCorrelationAggregation aggregation = AeCorrelationAggregation.MedianLogRr,
            [FromQuery] bool seriousSocOnly = false,
            [FromQuery] bool excludeCombos = false,
            [FromQuery] int minEvents = 0)
        {
            #region implementation

            if (!isAeDashboardEnabled())
            {
                return disabledResult();
            }

            if (string.IsNullOrWhiteSpace(pharmClassCode))
            {
                return BadRequest("Pharmacologic class code is required.");
            }

            if (string.IsNullOrWhiteSpace(socX) || string.IsNullOrWhiteSpace(socY))
            {
                return BadRequest("Both socX and socY are required.");
            }

            var filterValidation = validateCorrelationFilters(comparator, aggregation, minEvents, method);
            if (filterValidation != null)
            {
                return filterValidation;
            }

            try
            {
                var result = await DtoLabelAccess.GetAeCorrelationCellDetailAsync(
                    _dbContext,
                    pharmClassCode.Trim(),
                    socX.Trim(),
                    socY.Trim(),
                    _pkSecret,
                    _logger,
                    comparator ?? AeComparatorMix.Placebo,
                    includeNonSignificant,
                    excludeFragile,
                    minDrugsPerCell,
                    method,
                    aggregation,
                    seriousSocOnly,
                    excludeCombos,
                    minEvents);

                if (result == null)
                {
                    return NotFound("The requested pharmacologic class is not available in the AE dashboard.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving AE dashboard correlation cell for {PharmClassCode}.", pharmClassCode);
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the AE dashboard correlation cell detail.");
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
        /// Validates the shared correlation filter inputs, returning a 400 response when any value is invalid.
        /// </summary>
        /// <param name="comparator">Optional comparator mix; null means the placebo default.</param>
        /// <param name="aggregation">Within-SOC aggregation enum.</param>
        /// <param name="minEvents">Minimum total events filter; must be non-negative.</param>
        /// <param name="method">Optional correlation method enum (the heatmap omits it).</param>
        /// <returns>A <see cref="BadRequestObjectResult"/> when a value is invalid; otherwise null.</returns>
        /// <remarks>
        /// Model binding can produce an undefined enum value from an out-of-range numeric query
        /// string (for example <c>comparator=99</c>), which would otherwise bind silently. This
        /// guard rejects those deterministically with 400 and also rejects a negative
        /// <paramref name="minEvents"/> (which would otherwise be equivalent to "no minimum").
        /// </remarks>
        /// <seealso cref="AeComparatorMix"/>
        /// <seealso cref="AeCorrelationMethod"/>
        /// <seealso cref="AeCorrelationAggregation"/>
        private ActionResult? validateCorrelationFilters(
            AeComparatorMix? comparator,
            AeCorrelationAggregation aggregation,
            int minEvents,
            AeCorrelationMethod? method = null)
        {
            #region implementation

            if (minEvents < 0)
            {
                return BadRequest("minEvents cannot be negative.");
            }

            if (comparator.HasValue && !Enum.IsDefined(typeof(AeComparatorMix), comparator.Value))
            {
                return BadRequest("Unsupported comparator value.");
            }

            if (!Enum.IsDefined(typeof(AeCorrelationAggregation), aggregation))
            {
                return BadRequest("Unsupported aggregation value.");
            }

            if (method.HasValue && !Enum.IsDefined(typeof(AeCorrelationMethod), method.Value))
            {
                return BadRequest("Unsupported method value.");
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the shared system-correlation filter inputs, returning a 400 response when invalid.
        /// </summary>
        /// <param name="comparator">Optional comparator mix; null means the placebo default.</param>
        /// <param name="aggregation">Within-class aggregation enum.</param>
        /// <param name="minEvents">Minimum total events filter; must be non-negative.</param>
        /// <param name="method">Optional correlation method enum (the heatmap omits it).</param>
        /// <returns>A <see cref="BadRequestObjectResult"/> when a value is invalid; otherwise null.</returns>
        /// <remarks>
        /// Uses the same enum and minimum-event semantics as the class-first correlation lane,
        /// while leaving <c>minTermsPerCell</c> to the server floor clamp.
        /// </remarks>
        /// <seealso cref="validateCorrelationFilters"/>
        /// <seealso cref="AeSystemCorrelationFilters"/>
        private ActionResult? validateSystemCorrelationFilters(
            AeComparatorMix? comparator,
            AeCorrelationAggregation aggregation,
            int minEvents,
            AeCorrelationMethod? method = null)
        {
            #region implementation

            return validateCorrelationFilters(comparator, aggregation, minEvents, method);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates and defaults a bounded page/page-size pair for body-level correlation axes.
        /// </summary>
        /// <param name="pageNumber">Optional page number, defaulted to 1.</param>
        /// <param name="pageSize">Optional page size, defaulted per axis.</param>
        /// <param name="defaultPageSize">Default page size for the axis.</param>
        /// <param name="maxPageSize">Maximum accepted page size for the axis.</param>
        /// <param name="axisName">Human-readable axis name used in validation errors.</param>
        /// <returns>A <see cref="BadRequestObjectResult"/> when invalid; otherwise null.</returns>
        /// <remarks>
        /// Dual-axis responses cannot rely on the base header paginator, so their page metadata
        /// is emitted in the response body and defaults are applied before data access.
        /// </remarks>
        /// <seealso cref="AeCorrelationAxisPageDto"/>
        private BadRequestObjectResult? validateBoundedPagingParameters(
            ref int? pageNumber,
            ref int? pageSize,
            int defaultPageSize,
            int maxPageSize,
            string axisName)
        {
            #region implementation

            if (pageNumber.HasValue && pageNumber.Value <= 0)
            {
                return BadRequest($"Invalid {axisName} page number: {pageNumber.Value}. Page number must be greater than 0 if provided.");
            }

            if (pageSize.HasValue && pageSize.Value <= 0)
            {
                return BadRequest($"Invalid {axisName} page size: {pageSize.Value}. Page size must be greater than 0 if provided.");
            }

            if (pageSize.HasValue && pageSize.Value > maxPageSize)
            {
                return BadRequest($"Invalid {axisName} page size: {pageSize.Value}. Page size cannot exceed {maxPageSize}.");
            }

            pageNumber ??= 1;
            pageSize ??= defaultPageSize;
            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes selected-system query values, accepting repeated keys or comma-separated values.
        /// </summary>
        /// <param name="systems">Raw selected-system query values.</param>
        /// <returns>Trimmed, de-duplicated selected systems in caller order.</returns>
        /// <remarks>
        /// ASP.NET Core binds repeated query parameters naturally; comma splitting keeps direct
        /// controller calls and ad hoc URL probes consistent with that model.
        /// </remarks>
        private static List<string> normalizeSystemQueryValues(IEnumerable<string>? systems)
        {
            #region implementation

            if (systems == null)
            {
                return new List<string>();
            }

            return systems
                .SelectMany(system => (system ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(system => !string.IsNullOrWhiteSpace(system))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that a system-scoped linear-correlation endpoint received exactly one MedDRA system.
        /// </summary>
        /// <param name="selectedSystems">Normalized selected-system values from the query string.</param>
        /// <returns>A bad-request result when the selected-system contract is violated; otherwise null.</returns>
        /// <remarks>
        /// The By System matrix, heatmap, and cell-detail views are intentionally scoped to one
        /// System Organ Class at a time so the pharmacologic-class axes stay interpretable.
        /// </remarks>
        /// <seealso cref="GetSystemCorrelationMap"/>
        /// <seealso cref="GetSystemCorrelationHeatmap"/>
        /// <seealso cref="GetSystemCorrelationCell"/>
        private BadRequestObjectResult? validateSingleSelectedSystem(IReadOnlyList<string> selectedSystems)
        {
            #region implementation

            if (selectedSystems.Count == 0)
            {
                return BadRequest("Exactly one selected MedDRA system is required.");
            }

            if (selectedSystems.Count > 1)
            {
                return BadRequest("System correlation endpoints accept exactly one selected MedDRA system.");
            }

            return null;

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
