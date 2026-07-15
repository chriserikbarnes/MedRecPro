using MedRecPro.Controllers;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Models.Extensions;
using MedRecPro.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static MedRecPro.Models.UserRole;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Handles SPL document comparison analysis and progress endpoints while preserving the original Label route surface.
    /// </summary>
    /// <remarks>
    /// This controller keeps comparison queueing, progress polling, and scoped background execution together to preserve background task lifetimes.
    /// </remarks>
    /// <seealso cref="LabelFeatureControllerAttribute"/>
    /// <seealso cref="LabelController"/>
    [ApiController]
    [LabelFeatureController]
    [LabelFeatureSwaggerTag("Label Comparison")]
    public class LabelComparisonController : ApiControllerBase
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Comparison service used by synchronous comparison generation.
        /// </summary>
        /// <seealso cref="IComparisonService"/>
        private readonly IComparisonService _comparisonService;

        /**************************************************************/
        /// <summary>
        /// Logger instance for comparison endpoint diagnostics.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger<LabelComparisonController> _logger;

        /**************************************************************/
        /// <summary>
        /// Coordinator that owns queued comparison status and background scope management.
        /// </summary>
        /// <seealso cref="IComparisonJobCoordinator"/>
        private readonly IComparisonJobCoordinator _comparisonJobCoordinator;

        /**************************************************************/
        /// <summary>
        /// Store for comparison operation progress state.
        /// </summary>
        /// <seealso cref="IOperationStatusStore"/>
        private readonly IOperationStatusStore _statusStore;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="LabelComparisonController"/> class.
        /// </summary>
        /// <param name="comparisonService">Comparison service for synchronous analysis.</param>
        /// <param name="logger">Logger instance for comparison endpoint diagnostics.</param>
        /// <param name="comparisonJobCoordinator">Coordinator for queued comparison operations.</param>
        /// <param name="statusStore">Store for comparison operation progress state.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        /// <seealso cref="LabelController"/>
        public LabelComparisonController(
            IComparisonService comparisonService,
            ILogger<LabelComparisonController> logger,
            IComparisonJobCoordinator comparisonJobCoordinator,
            IOperationStatusStore statusStore)
        {
            #region implementation

            _comparisonService = comparisonService ?? throw new ArgumentNullException(nameof(comparisonService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _comparisonJobCoordinator = comparisonJobCoordinator ?? throw new ArgumentNullException(nameof(comparisonJobCoordinator));
            _statusStore = statusStore ?? throw new ArgumentNullException(nameof(statusStore));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds response headers for tracking comparison analysis operations.
        /// </summary>
        /// <param name="documentGuid">The document GUID being analyzed</param>
        /// <param name="operationId">The unique operation identifier</param>
        /// <param name="isAsynchronous">Whether this is an asynchronous operation</param>
        /// <seealso cref="Label"/>
        private void addComparisonResponseHeaders(Guid documentGuid, string operationId, bool isAsynchronous = true)
        {
            #region implementation
            Response.Headers.Append(ComparisonConstants.HEADER_DOCUMENT_GUID, documentGuid.ToString());
            Response.Headers.Append(ComparisonConstants.HEADER_OPERATION_ID, operationId);
            Response.Headers.Append(ComparisonConstants.HEADER_ANALYSIS_TYPE, ComparisonConstants.ANALYSIS_TYPE_DOCUMENT_COMPARISON);
            Response.Headers.Append(ComparisonConstants.HEADER_ANALYSIS_METHOD,
                isAsynchronous ? ComparisonConstants.ANALYSIS_METHOD_ASYNCHRONOUS : ComparisonConstants.ANALYSIS_METHOD_SYNCHRONOUS);
            Response.Headers.Append(ComparisonConstants.HEADER_ANALYSIS_TIMESTAMP, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the document GUID parameter and returns appropriate error response if invalid.
        /// </summary>
        /// <param name="documentGuid">The document GUID to validate</param>
        /// <returns>BadRequest result if invalid, null if valid</returns>
        /// <seealso cref="Label"/>
        private ActionResult? validateDocumentGuid(Guid documentGuid)
        {
            #region implementation
            if (documentGuid.IsNullOrEmpty())
            {
                _logger.LogWarning("Invalid empty GUID provided for document comparison analysis");
                return BadRequest(ComparisonConstants.ERROR_EMPTY_DOCUMENT_GUID);
            }
            return null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// NOTE: This is a long running process (e.g. > 30 sec). Consider
        /// Using the POST method to queue the job in the background and
        /// use polling to check on the progress.
        ///
        /// This generates an AI-powered comparison analysis between the original SPL XML data and the
        /// structured DTO representation for a specific document. This endpoint leverages
        /// Claude AI to identify data transformation differences, missing elements, and
        /// completeness metrics between the source XML and the processed Label entity structure.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique GUID identifier of the document to analyze. This corresponds to the
        /// DocumentGUID property in the Label.Document entity.
        /// </param>
        /// <returns>
        /// A comprehensive analysis report comparing XML source data with DTO representation,
        /// including completeness assessment, identified differences, and detailed metrics.
        /// </returns>
        /// <response code="200">Returns the comparison analysis results.</response>
        /// <response code="400">If the document GUID parameter is invalid.</response>
        /// <response code="404">If no document is found with the specified GUID.</response>
        /// <response code="500">If an internal server error occurs during analysis.</response>
        /// <remarks>
        /// **Limitation:** This endpoint only supports SPL Label document types. Other SPL sub-types
        /// (e.g., indexing files, establishment registrations, product listings) are not supported
        /// and will not produce accurate comparison results.
        ///
        /// This endpoint delegates the comparison analysis to the ComparisonService, which handles:
        /// - Retrieving the complete label DTO structure from the database
        /// - Finding the corresponding SplData record containing original XML
        /// - Converting the DTO > JSON > Rendered SPL for comparison with original
        /// - Using Claude AI to perform intelligent difference analysis
        /// - Parsing AI response into structured comparison results
        ///
        /// The analysis focuses on data preservation during XML-to-DTO transformation,
        /// identifying missing fields, structural differences, and completeness metrics
        /// critical for regulatory compliance and data integrity validation.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/label/comparison/analysis/12345678-1234-1234-1234-123456789012
        ///
        /// Response:
        /// {
        ///   "documentGuid": "12345678-1234-1234-1234-123456789012",
        ///   "isComplete": true,
        ///   "completionPercentage": 95.5,
        ///   "summary": "Analysis shows high data preservation with minor formatting differences",
        ///   "differences": [
        ///     {
        ///       "type": "Missing",
        ///       "section": "ClinicalPharmacology",
        ///       "description": "Pharmacokinetics subsection not fully preserved",
        ///       "severity": "Medium"
        ///     }
        ///   ],
        ///   "detailedAnalysis": "Full AI analysis text...",
        ///   "generatedAt": "2024-01-15T10:30:00Z"
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.Document.DocumentGUID"/>
        /// <seealso cref="IComparisonService.GenerateDocumentComparisonAsync(Guid)"/>
        /// <seealso cref="LabelDocumentController.GetSingleCompleteLabel(Guid)"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("comparison/analysis/{documentGuid}")]
        [ProducesResponseType(typeof(DocumentComparisonResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DocumentComparisonResult>> GetDocumentComparisonAnalysis(Guid documentGuid)
        {
            #region implementation

            #region input validation
            if (documentGuid == Guid.Empty)
            {
                _logger.LogWarning("Invalid empty GUID provided for document comparison analysis");
                return BadRequest("Document GUID cannot be empty.");
            }
            #endregion

            try
            {
                _logger.LogInformation("Starting document comparison analysis for GUID {DocumentGuid}", documentGuid);

                // Delegate to comparison service for business logic
                var analysisResult = await _comparisonService.GenerateDocumentComparisonAsync(documentGuid);

                _logger.LogInformation("Successfully completed document comparison analysis for GUID {DocumentGuid}", documentGuid);

                // Add response headers for tracking
                Response.Headers.Append("X-Document-Guid", documentGuid.ToString());
                Response.Headers.Append("X-Analysis-Type", "DocumentComparison");
                Response.Headers.Append("X-Analysis-Timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                return Ok(analysisResult);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument for document comparison analysis: {DocumentGuid}", documentGuid);
                return BadRequest("The document comparison request is invalid.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                _logger.LogWarning(ex, "Document or related data not found for GUID {DocumentGuid}", documentGuid);
                return NotFound("The document comparison data was not found.");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation during document comparison for GUID {DocumentGuid}", documentGuid);
                return BadRequest("The document comparison request is invalid.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the progress status of a document comparison operation.
        /// </summary>
        /// <param name="operationId">The unique identifier for the comparison operation to check.</param>
        /// <returns>
        /// Returns an <see cref="IActionResult"/> containing the operation status if found,
        /// or NotFound if the operation doesn't exist or has expired.
        /// </returns>
        /// <response code="200">Returns the current comparison operation status.</response>
        /// <response code="400">If model binding rejects an empty or whitespace operation ID.</response>
        /// <response code="404">If the operation ID is not found or has expired.</response>
        /// <response code="500">If an unexpected server error occurs.</response>
        /// <remarks>
        /// This endpoint allows clients to poll for the status of long-running comparison operations.
        /// The operation ID is typically obtained from the initial comparison request.
        /// </remarks>
        /// <example>
        /// GET /comparison/progress/12345678-1234-1234-1234-123456789012
        /// </example>
        /// <seealso cref="Label"/>
        /// <seealso cref="ComparisonOperationStatus"/>
        /// <seealso cref="QueueDocumentComparisonAnalysis"/>
        [ProducesResponseType(typeof(ComparisonOperationStatus), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        [HttpGet("comparison/progress/{operationId}")]
        public IActionResult GetComparisonProgress(string operationId)
        {
            #region implementation
            // Attempt to retrieve the operation status from the status store
            if (_statusStore.TryGet(operationId, out ComparisonOperationStatus? status) && status != null)
            {
                _logger.LogDebug("Retrieved comparison progress for operation {OperationId}: {Status}", operationId, status.Status);
                return Ok(status);
            }

            // Operation not found or has expired
            _logger.LogWarning("Comparison operation {OperationId} not found or has expired", operationId);
            return NotFound();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Queues an AI-powered comparison analysis operation for asynchronous processing to compare
        /// original SPL XML data with structured DTO representation. This endpoint leverages Claude AI
        /// to identify data transformation differences, missing elements, and completeness metrics
        /// between the source XML and the processed Label entity structure for regulatory compliance validation.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique GUID identifier of the document to analyze. This corresponds to the
        /// DocumentGUID property in the Label.Document entity and must match an existing
        /// document in the system.
        /// </param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests during the queuing process.</param>
        /// <returns>
        /// Returns an <see cref="ActionResult{T}"/> containing the initial <see cref="ComparisonOperationStatus"/>
        /// with a 202 Accepted response for successful queuing, or appropriate error responses for invalid requests.
        /// The status includes the operation ID for progress polling and the progress URL endpoint.
        /// </returns>
        /// <remarks>
        /// **Limitation:** This endpoint only supports SPL Label document types. Other SPL sub-types
        /// (e.g., indexing files, establishment registrations, product listings) are not supported
        /// and will not produce accurate comparison results.
        ///
        /// This endpoint initiates a long-running AI-powered document comparison analysis that:
        /// - Retrieves the complete Label DTO structure from the database
        /// - Finds the corresponding SplData record containing original XML
        /// - Converts the DTO to JSON for standardized comparison
        /// - Uses Claude AI to perform intelligent difference analysis
        /// - Parses AI response into structured comparison results with completeness metrics
        ///
        /// The analysis focuses on data preservation during XML-to-DTO transformation, identifying
        /// missing fields, structural differences, and completeness percentages critical for
        /// regulatory compliance and data integrity validation in pharmaceutical labeling.
        ///
        /// Clients should use the returned operation ID to poll for progress and results via
        /// the GetComparisonProgress endpoint. The background analysis may take several minutes
        /// depending on document complexity and AI processing time.
        /// </remarks>
        /// <example>
        /// <code>
        /// POST /comparison/analysis/12345678-1234-1234-1234-123456789012
        /// Content-Type: application/json
        ///
        /// Response: 202 Accepted
        /// {
        ///   "operationId": "op-67890123-4567-8901-2345-678901234567",
        ///   "status": "Queued",
        ///   "documentGuid": "12345678-1234-1234-1234-123456789012",
        ///   "progressUrl": "/comparison/progress/op-67890123-4567-8901-2345-678901234567",
        ///   "queuedAt": "2024-01-15T10:30:00Z"
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="Label"/>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="ComparisonOperationStatus"/>
        /// <seealso cref="GetComparisonProgress"/>
        /// <seealso cref="ComparisonConstants"/>
        /// <seealso cref="ComparisonService"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpPost("comparison/analysis/{documentGuid}")]
        [Authorize]
        [RequireUserRole(Admin)]
        [ProducesResponseType(typeof(ComparisonOperationStatus), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status499ClientClosedRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public ActionResult<ComparisonOperationStatus> QueueDocumentComparisonAnalysis(
            Guid documentGuid,
            CancellationToken cancellationToken)
        {
            #region implementation
            var validationResult = validateDocumentGuid(documentGuid);
            if (validationResult != null)
            {
                return validationResult;
            }

            // This parameter remains in the public action signature for route and binding compatibility. A queued
            // operation must not inherit it because ASP.NET cancels it with the originating HTTP request.
            _ = cancellationToken;
            var operationId = Guid.NewGuid().ToString();
            var progressUrl = Url.Action("GetComparisonProgress", new { operationId });
            var status = _comparisonJobCoordinator.Enqueue(new ComparisonJobRequest(operationId, documentGuid, progressUrl));

            addComparisonResponseHeaders(documentGuid, operationId, isAsynchronous: true);
            return Accepted(status);
            #endregion
        }

        #endregion
    }
}
