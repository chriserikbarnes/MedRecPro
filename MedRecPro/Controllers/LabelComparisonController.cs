using MedRecPro.Controllers;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Mappers;
using MedRecPro.Models;
using MedRecPro.Models.Extensions;
using MedRecPro.Service;
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
        /// Service provider used by synchronous comparison generation while the legacy dynamic seam remains in place.
        /// </summary>
        /// <seealso cref="IComparisonService"/>
        private readonly IServiceProvider _serviceProvider;

        /**************************************************************/
        /// <summary>
        /// Logger instance for comparison endpoint diagnostics.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger<LabelComparisonController> _logger;

        /**************************************************************/
        /// <summary>
        /// Queue service for long-running comparison operations.
        /// </summary>
        /// <seealso cref="IBackgroundTaskQueueService"/>
        private readonly IBackgroundTaskQueueService _queue;

        /**************************************************************/
        /// <summary>
        /// Store for comparison operation progress state.
        /// </summary>
        /// <seealso cref="IOperationStatusStore"/>
        private readonly IOperationStatusStore _statusStore;

        /**************************************************************/
        /// <summary>
        /// Scope factory used by background comparison work after the HTTP request completes.
        /// </summary>
        /// <seealso cref="IServiceScopeFactory"/>
        private readonly IServiceScopeFactory _scopeFactory;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="LabelComparisonController"/> class.
        /// </summary>
        /// <param name="serviceProvider">Service provider for synchronous comparison service resolution.</param>
        /// <param name="logger">Logger instance for comparison endpoint diagnostics.</param>
        /// <param name="queue">Queue service for long-running comparison operations.</param>
        /// <param name="statusStore">Store for comparison operation progress state.</param>
        /// <param name="scopeFactory">Scope factory for background comparison work.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        /// <seealso cref="LabelController"/>
        public LabelComparisonController(
            IServiceProvider serviceProvider,
            ILogger<LabelComparisonController> logger,
            IBackgroundTaskQueueService queue,
            IOperationStatusStore statusStore,
            IServiceScopeFactory scopeFactory)
        {
            #region implementation

            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _statusStore = statusStore ?? throw new ArgumentNullException(nameof(statusStore));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the initial status object for a new comparison operation.
        /// </summary>
        /// <param name="operationId">The unique operation identifier</param>
        /// <param name="documentGuid">The document GUID being analyzed</param>
        /// <param name="progressUrl">The URL for progress monitoring</param>
        /// <returns>A new ComparisonOperationStatus with initial values</returns>
        /// <seealso cref="ComparisonOperationStatus"/>
        /// <seealso cref="Label"/>
        private ComparisonOperationStatus createInitialComparisonStatus(string operationId, Guid documentGuid, string? progressUrl)
        {
            #region implementation
            return new ComparisonOperationStatus
            {
                OperationId = operationId,
                DocumentGuid = documentGuid,
                Status = ComparisonConstants.STATUS_QUEUED,
                PercentComplete = ComparisonConstants.PROGRESS_QUEUED,
                ProgressUrl = progressUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
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
        /// Updates the comparison operation status with new information using extension methods.
        /// </summary>
        /// <param name="operationId">The unique operation identifier</param>
        /// <param name="status">The status message</param>
        /// <param name="percentComplete">The completion percentage</param>
        /// <param name="progressUrl">The progress monitoring URL</param>
        /// <param name="documentGuid">The document GUID being analyzed</param>
        /// <param name="result">The analysis result when complete</param>
        /// <param name="error">Any error message</param>
        /// <remarks>
        /// Uses the generic extension method to store ComparisonOperationStatus while maintaining
        /// backward compatibility with the strongly-typed IOperationStatusStore interface.
        /// </remarks>
        /// <seealso cref="ComparisonOperationStatus"/>
        /// <seealso cref="OperationStatusStoreExtensions"/>
        /// <seealso cref="Label"/>
        private void updateComparisonStatus(
            string operationId,
            string status,
            int percentComplete,
            string? progressUrl,
            Guid documentGuid,
            DocumentComparisonResult? result = null,
            string? error = null)
        {
            #region implementation
            var comparisonStatus = new ComparisonOperationStatus
            {
                OperationId = operationId,
                DocumentGuid = documentGuid,
                Status = status,
                PercentComplete = percentComplete,
                ProgressUrl = progressUrl,
                Result = result,
                Error = error,
                UpdatedAt = DateTime.UtcNow
            };

            // Preserve creation time if status exists using generic extension method
            if (_statusStore.TryGet<ComparisonOperationStatus>(operationId, out ComparisonOperationStatus? existingStatus) &&
                existingStatus is not null)
            {
                comparisonStatus.CreatedAt = existingStatus.CreatedAt;
            }

            // Use generic extension method to store the status
            _statusStore.Set(operationId, comparisonStatus);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Executes the comparison analysis operation in the background using a scoped service provider.
        /// </summary>
        /// <param name="operationId">The unique operation identifier</param>
        /// <param name="documentGuid">The document GUID to analyze</param>
        /// <param name="progressUrl">The progress monitoring URL</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <remarks>
        /// Creates a new service scope for background processing to ensure proper dependency injection
        /// container lifecycle management. This prevents "ObjectDisposedException" errors that occur
        /// when accessing services from a disposed scope in background tasks.
        /// </remarks>
        /// <seealso cref="IComparisonService"/>
        /// <seealso cref="DocumentComparisonResult"/>
        /// <seealso cref="IServiceScopeFactory"/>
        /// <seealso cref="Label"/>
        private async Task executeComparisonAnalysisAsync(
            string operationId,
            Guid documentGuid,
            string? progressUrl,
            CancellationToken cancellationToken)
        {
            #region implementation
            try
            {
                // Update status to indicate processing has started
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_PROCESSING,
                    ComparisonConstants.PROGRESS_PROCESSING_STARTED, progressUrl, documentGuid);

                _logger.LogInformation("Starting background comparison analysis for document {DocumentGuid}, operation {OperationId}",
                    documentGuid, operationId);

                // Create a new scope for background processing to avoid disposed context issues
                using var scope = _scopeFactory.CreateScope();
                var comparisonService = scope.ServiceProvider.GetRequiredService<IComparisonService>();

                // Update progress during analysis
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_ANALYZING,
                    ComparisonConstants.PROGRESS_ANALYZING, progressUrl, documentGuid);

                var analysisResult = await comparisonService.GenerateDocumentComparisonAsync(documentGuid);

                // Update progress as analysis completes
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_FINALIZING,
                    ComparisonConstants.PROGRESS_FINALIZING, progressUrl, documentGuid);

                // Mark operation as completed and store results
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_COMPLETED,
                    ComparisonConstants.PROGRESS_COMPLETED, progressUrl, documentGuid, analysisResult);

                _logger.LogInformation("Successfully completed background comparison analysis for document {DocumentGuid}, operation {OperationId}",
                    documentGuid, operationId);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_CANCELED,
                    ComparisonConstants.PROGRESS_QUEUED, progressUrl, documentGuid);
                _logger.LogInformation("Comparison analysis was canceled for document {DocumentGuid}, operation {OperationId}",
                    documentGuid, operationId);
            }
            catch (ArgumentException ex)
            {
                // Handle validation errors
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_FAILED,
                    ComparisonConstants.PROGRESS_QUEUED, progressUrl, documentGuid, error: "The comparison operation failed.");
                _logger.LogWarning(ex, "Invalid argument during comparison analysis for document {DocumentGuid}, operation {OperationId}",
                    documentGuid, operationId);
            }
            catch (InvalidOperationException ex)
            {
                // Handle business logic errors
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_FAILED,
                    ComparisonConstants.PROGRESS_QUEUED, progressUrl, documentGuid, error: "The comparison operation failed.");
                _logger.LogWarning(ex, "Invalid operation during comparison analysis for document {DocumentGuid}, operation {OperationId}",
                    documentGuid, operationId);
            }
            // Broad-catch allowlist: this background callback owns the terminal operation status after the HTTP
            // request has ended, so the failure must be recorded here rather than by request middleware.
            catch (Exception ex)
            {
                // Handle any unexpected processing errors
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_FAILED,
                    ComparisonConstants.PROGRESS_QUEUED, progressUrl, documentGuid,
                    error: ComparisonConstants.ERROR_ANALYSIS_FAILED);
                _logger.LogError(ex, "Unexpected error during comparison analysis for document {DocumentGuid}, operation {OperationId}",
                    documentGuid, operationId);
            }
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
        /// Validates the operation ID parameter and returns appropriate error response if invalid.
        /// </summary>
        /// <param name="operationId">The operation ID to validate</param>
        /// <returns>BadRequest result if invalid, null if valid</returns>
        /// <seealso cref="Label"/>
        private ActionResult? validateOperationId(string operationId)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(operationId))
            {
                return BadRequest(ComparisonConstants.ERROR_EMPTY_OPERATION_ID);
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
                var comparisonService = _serviceProvider.GetRequiredService<IComparisonService>();
                var analysisResult = await comparisonService.GenerateDocumentComparisonAsync(documentGuid);

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
        [HttpGet("comparison/progress/{operationId}")]
        public IActionResult GetComparisonProgress(string operationId)
        {
            #region implementation
            // Use helper method for validation
            var validationResult = validateOperationId(operationId);
            if (validationResult != null) return validationResult;

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
            // Use helper method for validation
            var validationResult = validateDocumentGuid(documentGuid);
            if (validationResult != null) return validationResult;

            try
            {
                _logger.LogInformation("Queuing document comparison analysis for GUID {DocumentGuid}", documentGuid);

                // Generate unique operation identifier
                var operationId = Guid.NewGuid().ToString();
                var progressUrl = Url.Action("GetComparisonProgress", new { operationId });

                // Create linked cancellation token to handle client disconnection
                var disconnectedToken = HttpContext.RequestAborted;
                var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disconnectedToken);

                // Use helper method to create initial status
                var status = createInitialComparisonStatus(operationId, documentGuid, progressUrl);

                // Store the initial status for client polling
                _statusStore.Set(operationId, status);

                // Queue the background processing task
                _queue.Enqueue(operationId, async token =>
                {
                    await executeComparisonAnalysisAsync(operationId, documentGuid, progressUrl, linkedTokenSource.Token);
                });

                _logger.LogInformation("Successfully queued document comparison analysis for GUID {DocumentGuid}", documentGuid);

                // Use helper method for response headers
                addComparisonResponseHeaders(documentGuid, operationId, isAsynchronous: true);

                return Accepted(status);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
                _logger.LogInformation("Document comparison analysis queuing was canceled for GUID {DocumentGuid}", documentGuid);
                return StatusCode(StatusCodes.Status499ClientClosedRequest);
            }
            #endregion
        }

        #endregion
    }
}
