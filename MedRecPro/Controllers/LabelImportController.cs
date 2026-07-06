using MedRecPro.Controllers;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Filters;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Models.Extensions;
using MedRecPro.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Reflection;
using System.Security.Claims;
using static MedRecPro.Models.UserRole;
using ImportOperationStatus = MedRecProImportClass.Models.ImportOperationStatus;
using ImportSplZipImportResult = MedRecProImportClass.Models.SplZipImportResult;
using WebImportOperationStatus = MedRecPro.Models.ImportOperationStatus;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// Handles SPL ZIP import queueing and progress endpoints while preserving the original Label route surface.
    /// </summary>
    /// <remarks>
    /// This controller keeps import background queuing and progress polling together so progress link generation remains action-name compatible.
    /// </remarks>
    /// <seealso cref="LabelFeatureControllerAttribute"/>
    /// <seealso cref="LabelController"/>
    [ApiController]
    [LabelFeatureController]
    [LabelFeatureSwaggerTag("Label Import")]
    public class LabelImportController : ApiControllerBase
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Configuration provider for SPL import feature flags.
        /// </summary>
        /// <seealso cref="IConfiguration"/>
        private readonly IConfiguration _configuration;

        /**************************************************************/
        /// <summary>
        /// Logger instance for import endpoint diagnostics.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger<LabelImportController> _logger;

        /**************************************************************/
        /// <summary>
        /// Service for importing SPL data from ZIP files containing XML files.
        /// </summary>
        /// <seealso cref="SplImportService"/>
        private readonly SplImportService _splImportService;

        /**************************************************************/
        /// <summary>
        /// Queue service for long-running import operations.
        /// </summary>
        /// <seealso cref="IBackgroundTaskQueueService"/>
        private readonly IBackgroundTaskQueueService _queue;

        /**************************************************************/
        /// <summary>
        /// Store for import operation progress state.
        /// </summary>
        /// <seealso cref="IImportOperationStatusStore"/>
        private readonly IImportOperationStatusStore _statusStore;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="LabelImportController"/> class.
        /// </summary>
        /// <param name="configuration">Configuration provider for SPL import feature flags.</param>
        /// <param name="logger">Logger instance for import endpoint diagnostics.</param>
        /// <param name="splImportService">SPL ZIP import service.</param>
        /// <param name="queue">Queue service for long-running import operations.</param>
        /// <param name="statusStore">Store for import operation progress state.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
        /// <seealso cref="LabelController"/>
        public LabelImportController(
            IConfiguration configuration,
            ILogger<LabelImportController> logger,
            SplImportService splImportService,
            IBackgroundTaskQueueService queue,
            IImportOperationStatusStore statusStore)
        {
            #region implementation

            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _splImportService = splImportService ?? throw new ArgumentNullException(nameof(splImportService));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _statusStore = statusStore ?? throw new ArgumentNullException(nameof(statusStore));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Imports SPL data from one or more ZIP files.
        /// Each ZIP file should contain SPL XML files.
        /// </summary>
        /// <param name="files">List of ZIP files to import.</param>
        /// <param name="cancellationToken">Disconnect cancelation</param>
        /// <returns>A summary of the import operation.</returns>
        /// <response code="202">Import process queued. Check results for details.</response>
        /// <response code="400">If no files are provided or files are invalid.</response>
        /// <response code="500">If an unexpected error occurs during processing.</response>
        /// <remarks>
        /// This endpoint provides asynchronous processing of SPL ZIP file imports. The operation is queued
        /// immediately and returns an operation ID that can be used to track progress. The ZIP files are
        /// processed in background, extracting and parsing individual SPL XML files within each archive.
        /// Progress updates and status changes are tracked via the status store and can be monitored
        /// using the progress endpoint.
        /// </remarks>
        /// <example>
        /// <code>
        /// POST /api/label/import
        /// Content-Type: multipart/form-data
        ///
        /// // Upload multiple ZIP files containing SPL XML documents
        /// // Returns: { "OperationId": "guid", "ProgressUrl": "/api/spl/import/progress/guid", ... }
        /// </code>
        /// </example>
        /// <seealso cref="SplImportService"/>
        /// <seealso cref="WebImportOperationStatus"/>
        /// <seealso cref="SplZipImportResult"/>
        /// <seealso cref="Label"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpPost("import")]
        [Authorize]
        [ProducesResponseType(typeof(WebImportOperationStatus), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status499ClientClosedRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadSplZips(List<IFormFile> files, CancellationToken cancellationToken)
        {
            #region Implementation
            List<BufferedFile>? bufferedFiles = null;

            var importEnabled = _configuration.GetValue<bool>("FeatureFlags:SplImportEnabled", true);

            if (!importEnabled)
            {
                return StatusCode(503, new
                {
                    error = "Import functionality is currently disabled"
                });
            }

            // Validate that files were provided in the request
            if (files == null || !files.Any())
            {
                return BadRequest("No files uploaded.");
            }

            _logger.LogInformation("Received {FileCount} files for SPL import.", files.Count);

            // Generate unique operation ID for tracking this import process
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInformation("Queuing import for {FileCount} files, opId: {OpId}", files.Count, operationId);

            CancellationToken disconnectedToken = HttpContext.RequestAborted;

            CancellationTokenSource source = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, disconnectedToken);

            long? currentUserId = getCurrentUserId();

            try
            {
                try
                {
                    bufferedFiles = await new BufferedFile().BufferFilesToTempAsync(files, cancellationToken);

                    if (bufferedFiles == null || !bufferedFiles.Any())
                    {
                        _logger.LogWarning("No valid files were buffered for import.");
                        return BadRequest("No valid files uploaded.");
                    }
                }
                catch (OperationCanceledException)
                {
                    // Clean up any partially buffered files
                    if (bufferedFiles != null)
                    {
                        foreach (var buffered in bufferedFiles)
                        {
                            try { System.IO.File.Delete(buffered.TempFilePath); } catch { }
                        }
                    }
                    return StatusCode(StatusCodes.Status499ClientClosedRequest); // 499 (non-standard) = Client Closed Request
                }

                var progressUrl = Url.Action("GetImportProgress", new { operationId });

                // Queue the background processing task
                _queue.Enqueue(operationId, async token =>
                {
                    // Update status to indicate processing has started
                    var status = new ImportOperationStatus
                    {
                        Status = "Queued",
                        PercentComplete = 0,
                        OperationId = operationId,
                        ProgressUrl = progressUrl,
                        TotalFiles = bufferedFiles.Count,
                        CurrentFile = 0
                    };

                    _statusStore.Set(operationId, status);

                    try
                    {
                        // Track current file being processed
                        int currentFileIndex = 0;

                        // Process ZIP files with progress and status callbacks
                        List<ImportSplZipImportResult> results = await _splImportService.ProcessZipFilesAsync(
                            bufferedFiles,
                            currentUserId,
                            source.Token,
                            progress =>
                            {
                                // Update progress percentage during processing
                                status.PercentComplete = progress;
                                status.OperationId = operationId;
                                status.ProgressUrl = progressUrl;
                                _statusStore.Set(operationId, status);
                            },
                            message =>
                            {
                                // Detect file transitions - only increment on "Starting Document" which indicates a new XML file
                                // Other "Starting" messages (Author, Body, Product, etc.) are for different parsing phases of the same file
                                if (message.StartsWith("Starting Document XML Elements"))
                                {
                                    currentFileIndex++;
                                    status.CurrentFile = Math.Min(currentFileIndex, status.TotalFiles);
                                }

                                // Update status message during processing
                                status.Status = message;
                                status.OperationId = operationId;
                                status.ProgressUrl = progressUrl;
                                _statusStore.Set(operationId, status);
                            },
                            results =>
                            {
                                // Store results when processing is complete
                                status.Results = results;
                                status.OperationId = operationId;
                                status.ProgressUrl = progressUrl;
                                _statusStore.Set(operationId, status);
                            }
                        );

                        // Mark operation as completed and store results
                        status.Status = "Completed";
                        status.PercentComplete = 100;
                        status.Results = results;
                    }
                    catch (OperationCanceledException)
                    {
                        // Handle cancellation gracefully
                        status.Status = "Canceled";
                    }
                    catch (Exception ex)
                    {
                        // Handle any processing errors
                        status.Status = "Failed";
                        status.Error = ex.Message;
                    }
                    finally
                    {
                        foreach (var buffered in bufferedFiles)
                        {
                            try { System.IO.File.Delete(buffered.TempFilePath); } catch { }
                        }
                    }

                    // Persist final status regardless of outcome
                    _statusStore.Set(operationId, status);
                });

                // Return accepted response with operation tracking information
                return Accepted(new
                {
                    OperationId = operationId,
                    ProgressUrl = Url.Action("GetImportProgress", new { operationId })
                });
            }
            catch (System.Exception ex)
            {
                // Handle any unexpected errors during queue setup
                _logger.LogError(ex, "Unhandled exception during SPL ZIP import.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred during import.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the current progress and status of a previously queued SPL import operation.
        /// The results include the operation's current status, completion percentage, errors, and
        /// the file parsing outcomes during the import. The result summary is cached with a lifetime
        /// of one hour i.e. the URL provided in the import operation is transient.
        /// </summary>
        /// <param name="operationId">The unique identifier for the import operation to check.</param>
        /// <returns>The current status and progress information for the specified operation.</returns>
        /// <response code="200">Returns the current operation status and progress.</response>
        /// <response code="404">If the operation ID is not found or has expired.</response>
        /// <remarks>
        /// This endpoint allows clients to poll for updates on long-running import operations.
        /// The status includes completion percentage, current processing stage, any error messages,
        /// and final results once the operation completes. Operation statuses include: Queued,
        /// Running, Completed, Canceled, and Failed.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/spl/import/progress/{operationId}
        ///
        /// // Returns: ImportOperationStatus with current progress and status
        /// // { "Status": "Running", "PercentComplete": 45, "Results": null, "Error": null }
        /// </code>
        /// </example>
        /// <seealso cref="WebImportOperationStatus"/>
        /// <seealso cref="SplZipImportResult"/>
        /// <seealso cref="Label"/>
        [ProducesResponseType(typeof(WebImportOperationStatus), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("import/progress/{operationId}")]
        public IActionResult GetImportProgress(string operationId)
        {
            #region Implementation
            // Attempt to retrieve the operation status from the store
            if (_statusStore.TryGet(operationId, out var status))
                return Ok(status);

            // Return 404 if the operation ID is not found or has expired
            return NotFound();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the current user ID from the HTTP context when an authenticated import request is processed.
        /// </summary>
        /// <returns>The current user's ID when claims are available; otherwise, null.</returns>
        /// <remarks>
        /// The import operation buffers this value before queueing background work because the HTTP context is not available inside the queued operation.
        /// </remarks>
        /// <seealso cref="ClaimHelper"/>
        private long? getCurrentUserId()
        {
            #region implementation

            try
            {
                if (HttpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    return ClaimHelper.GetUserIdFromClaims(HttpContext.User.Claims);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get current user ID from context.");
                return null;
            }

            #endregion
        }

        #endregion
    }
}
