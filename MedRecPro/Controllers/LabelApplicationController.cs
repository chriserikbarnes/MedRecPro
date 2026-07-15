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
    /// Handles Label application-number discovery endpoints.
    /// </summary>
    /// <remarks>
    /// The inherited Label controller convention preserves the existing public Debug and Release routes.
    /// </remarks>
    /// <seealso cref="FeatureControllerNameAttribute"/>
    [ApiController]
    [FeatureControllerName("Label")]
    [SwaggerGroup("Label Search")]
    public sealed class LabelApplicationController : ApiControllerBase
    {
        #region implementation

        /**************************************************************/
        /// <summary>Logger for application-number endpoint diagnostics.</summary>
        private readonly ILogger<LabelApplicationController> _logger;

        /**************************************************************/
        /// <summary>Product query service.</summary>
        private readonly IProductSearchService _productSearchService;

        /**************************************************************/
        /// <summary>Initializes a new instance of the controller.</summary>
        /// <param name="logger">Logger for endpoint diagnostics.</param>
        /// <param name="productSearchService">Product query service.</param>
        public LabelApplicationController(
            ILogger<LabelApplicationController> logger,
            IProductSearchService productSearchService)
        {
            #region implementation

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _productSearchService = productSearchService ?? throw new ArgumentNullException(nameof(productSearchService));

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


        #endregion
    }
}
