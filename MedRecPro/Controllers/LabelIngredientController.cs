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
    /// <summary>Handles Label ingredient search and navigation endpoints.</summary>
    /// <remarks>The inherited Label controller convention preserves the existing public Debug and Release routes.</remarks>
    /// <seealso cref="FeatureControllerNameAttribute"/>
    [ApiController]
    [FeatureControllerName("Label")]
    [SwaggerGroup("Label Search")]
    public sealed class LabelIngredientController : ApiControllerBase
    {
        #region implementation

        private readonly ILogger<LabelIngredientController> _logger;
        private readonly IIngredientSearchService _ingredientSearchService;

        /**************************************************************/
        /// <summary>Initializes a new instance of the controller.</summary>
        /// <param name="logger">Logger for endpoint diagnostics.</param>
        /// <param name="ingredientSearchService">Ingredient query service.</param>
        public LabelIngredientController(ILogger<LabelIngredientController> logger, IIngredientSearchService ingredientSearchService)
        {
            #region implementation

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ingredientSearchService = ingredientSearchService ?? throw new ArgumentNullException(nameof(ingredientSearchService));

            #endregion
        }

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


        #endregion
    }
}
