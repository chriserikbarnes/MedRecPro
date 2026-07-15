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
    /// <summary>Handles Label identifier and labeler discovery endpoints.</summary>
    /// <remarks>The inherited Label controller convention preserves the existing public Debug and Release routes.</remarks>
    /// <seealso cref="FeatureControllerNameAttribute"/>
    [ApiController]
    [FeatureControllerName("Label")]
    [SwaggerGroup(
        "Label Product Identifiers",
        "NDC product/package code and labeler discovery.")]
    public sealed class LabelProductIdentifierController : ApiControllerBase
    {
        #region implementation

        private readonly ILogger<LabelProductIdentifierController> _logger;
        private readonly IProductSearchService _productSearchService;

        /**************************************************************/
        /// <summary>Initializes a new instance of the controller.</summary>
        /// <param name="logger">Logger for endpoint diagnostics.</param>
        /// <param name="productSearchService">Product query service.</param>
        public LabelProductIdentifierController(ILogger<LabelProductIdentifierController> logger, IProductSearchService productSearchService)
        {
            #region implementation

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _productSearchService = productSearchService ?? throw new ArgumentNullException(nameof(productSearchService));

            #endregion
        }

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


        #endregion
    }
}
