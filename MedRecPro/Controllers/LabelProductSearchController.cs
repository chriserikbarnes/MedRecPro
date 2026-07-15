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
    /// <summary>Handles Label product summary, relationship, latest-label, and indication endpoints.</summary>
    /// <remarks>The inherited Label controller convention preserves the existing public Debug and Release routes.</remarks>
    /// <seealso cref="FeatureControllerNameAttribute"/>
    [ApiController]
    [FeatureControllerName("Label")]
    [SwaggerGroup(
        "Label Products",
        "Product-name search, latest labels, related products, and indications.")]
    public sealed class LabelProductSearchController : ApiControllerBase
    {
        #region implementation

        private readonly ILogger<LabelProductSearchController> _logger;
        private readonly IProductSearchService _productSearchService;
        private readonly ILabelContentQueryService _labelContentQueryService;
        private readonly ILabelMarkdownService _labelMarkdownService;

        /**************************************************************/
        /// <summary>Initializes a new instance of the controller.</summary>
        /// <param name="logger">Logger for endpoint diagnostics.</param>
        /// <param name="productSearchService">Product query service.</param>
        /// <param name="labelContentQueryService">Label content query service.</param>
        /// <param name="labelMarkdownService">Label markdown query service.</param>
        public LabelProductSearchController(ILogger<LabelProductSearchController> logger, IProductSearchService productSearchService, ILabelContentQueryService labelContentQueryService, ILabelMarkdownService labelMarkdownService)
        {
            #region implementation

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _productSearchService = productSearchService ?? throw new ArgumentNullException(nameof(productSearchService));
            _labelContentQueryService = labelContentQueryService ?? throw new ArgumentNullException(nameof(labelContentQueryService));
            _labelMarkdownService = labelMarkdownService ?? throw new ArgumentNullException(nameof(labelMarkdownService));

            #endregion
        }

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
        /// <seealso cref="LabelSectionNavigationController.SearchBySectionCode"/>
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
