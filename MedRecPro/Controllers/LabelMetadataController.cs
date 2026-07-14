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
    /// <summary>Handles Label API-guide and inventory-summary metadata endpoints.</summary>
    /// <remarks>The inherited Label controller convention preserves the existing public Debug and Release routes.</remarks>
    /// <seealso cref="LabelFeatureControllerAttribute"/>
    [ApiController]
    [LabelFeatureController]
    [LabelFeatureSwaggerTag("Label Search")]
    public sealed class LabelMetadataController : ApiControllerBase
    {
        #region implementation

        private readonly ILogger<LabelMetadataController> _logger;
        private readonly ILabelContentQueryService _labelContentQueryService;

        /**************************************************************/
        /// <summary>Initializes a new instance of the controller.</summary>
        /// <param name="logger">Logger for endpoint diagnostics.</param>
        /// <param name="labelContentQueryService">Label content query service.</param>
        public LabelMetadataController(ILogger<LabelMetadataController> logger, ILabelContentQueryService labelContentQueryService)
        {
            #region implementation

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _labelContentQueryService = labelContentQueryService ?? throw new ArgumentNullException(nameof(labelContentQueryService));

            #endregion
        }

        #region Metadata

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
        /// This endpoint provides metadata about available view-based endpoints, including descriptions,
        /// parameters, and usage examples for programmatic discovery by AI assistants and integrations.
        /// </remarks>
        /// <seealso cref="ILabelContentQueryService.GetAPIEndpointGuideAsync"/>
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


        #endregion

        #endregion
    }
}
