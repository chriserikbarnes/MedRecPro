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
    /// <summary>Handles Label section-navigation search and content endpoints.</summary>
    /// <remarks>The inherited Label controller convention preserves the existing public Debug and Release routes.</remarks>
    /// <seealso cref="FeatureControllerNameAttribute"/>
    [ApiController]
    [FeatureControllerName("Label")]
    [SwaggerGroup("Label Search")]
    public sealed class LabelSectionNavigationController : ApiControllerBase
    {
        #region implementation

        private readonly ILogger<LabelSectionNavigationController> _logger;
        private readonly ILabelContentQueryService _labelContentQueryService;

        /**************************************************************/
        /// <summary>Initializes a new instance of the controller.</summary>
        /// <param name="logger">Logger for endpoint diagnostics.</param>
        /// <param name="labelContentQueryService">Label content query service.</param>
        public LabelSectionNavigationController(ILogger<LabelSectionNavigationController> logger, ILabelContentQueryService labelContentQueryService)
        {
            #region implementation

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _labelContentQueryService = labelContentQueryService ?? throw new ArgumentNullException(nameof(labelContentQueryService));

            #endregion
        }

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


        #endregion
    }
}
