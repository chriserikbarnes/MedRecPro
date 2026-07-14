using MedRecPro.Models;
using MedRecPro.Models.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Snapshots the HTTP-independent inputs for a queued document comparison operation.
    /// </summary>
    /// <param name="OperationId">The already-routed operation identifier used for status storage.</param>
    /// <param name="DocumentGuid">The Label document to compare.</param>
    /// <param name="ProgressUrl">The established URL used by clients to poll progress.</param>
    /// <seealso cref="ComparisonOperationStatus"/>
    public sealed record ComparisonJobRequest(string OperationId, Guid DocumentGuid, string? ProgressUrl);

    /**************************************************************/
    /// <summary>
    /// Queues document comparisons using a background-owned dependency-injection scope.
    /// </summary>
    /// <remarks>
    /// The coordinator owns operation status mutation and creates the comparison scope inside the queued
    /// delegate. It never captures HttpContext, a controller dependency, or RequestAborted, so the work
    /// remains valid after the original request completes.
    /// </remarks>
    /// <seealso cref="IComparisonService"/>
    /// <seealso cref="IBackgroundTaskQueueService"/>
    public interface IComparisonJobCoordinator
    {
        /**************************************************************/
        /// <summary>Queues a document comparison and returns its initial polling state.</summary>
        /// <param name="request">The by-value input snapshot for the queued comparison.</param>
        /// <returns>The initial queued operation state.</returns>
        /// <seealso cref="ComparisonJobRequest"/>
        ComparisonOperationStatus Enqueue(ComparisonJobRequest request);
    }

    /**************************************************************/
    /// <summary>
    /// Coordinates background document comparisons with a fresh DI scope for every job.
    /// </summary>
    /// <seealso cref="IComparisonJobCoordinator"/>
    public sealed class ComparisonJobCoordinator : IComparisonJobCoordinator
    {
        private readonly IBackgroundTaskQueueService _queue;
        private readonly IOperationStatusStore _statusStore;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly ILogger<ComparisonJobCoordinator> _logger;

        /**************************************************************/
        /// <summary>Initializes the background comparison coordinator.</summary>
        /// <param name="queue">The application background work queue.</param>
        /// <param name="statusStore">The polling status store.</param>
        /// <param name="scopeFactory">Factory used only inside the queued job delegate.</param>
        /// <param name="applicationLifetime">Application shutdown token source.</param>
        /// <param name="logger">Logger for background job transitions and terminal failures.</param>
        /// <seealso cref="IServiceScopeFactory"/>
        public ComparisonJobCoordinator(
            IBackgroundTaskQueueService queue,
            IOperationStatusStore statusStore,
            IServiceScopeFactory scopeFactory,
            IHostApplicationLifetime applicationLifetime,
            ILogger<ComparisonJobCoordinator> logger)
        {
            #region implementation

            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _statusStore = statusStore ?? throw new ArgumentNullException(nameof(statusStore));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public ComparisonOperationStatus Enqueue(ComparisonJobRequest request)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.OperationId))
            {
                throw new ArgumentException("An operation identifier is required.", nameof(request));
            }

            var operationId = request.OperationId;
            var status = new ComparisonOperationStatus
            {
                OperationId = operationId,
                DocumentGuid = request.DocumentGuid,
                Status = ComparisonConstants.STATUS_QUEUED,
                PercentComplete = ComparisonConstants.PROGRESS_QUEUED,
                ProgressUrl = request.ProgressUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _statusStore.SetComparisonStatus(operationId, status);
            _queue.Enqueue(operationId, queueCancellation => executeAsync(operationId, request, queueCancellation));
            return status;

            #endregion
        }

        /**************************************************************/
        /// <summary>Executes one queued comparison in a dedicated dependency-injection scope.</summary>
        /// <param name="operationId">The operation status identifier.</param>
        /// <param name="request">The HTTP-independent job input snapshot.</param>
        /// <param name="queueCancellation">The queue or worker cancellation token.</param>
        /// <returns>A task that completes when the terminal operation state is stored.</returns>
        /// <seealso cref="IServiceScopeFactory.CreateScope"/>
        private async Task executeAsync(string operationId, ComparisonJobRequest request, CancellationToken queueCancellation)
        {
            #region implementation

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                queueCancellation,
                _applicationLifetime.ApplicationStopping);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var comparisonService = scope.ServiceProvider.GetRequiredService<IComparisonService>();
                updateStatus(operationId, request, ComparisonConstants.STATUS_PROCESSING, ComparisonConstants.PROGRESS_PROCESSING_STARTED);
                linkedCancellation.Token.ThrowIfCancellationRequested();

                updateStatus(operationId, request, ComparisonConstants.STATUS_ANALYZING, ComparisonConstants.PROGRESS_ANALYZING);
                var result = await comparisonService.GenerateDocumentComparisonAsync(request.DocumentGuid).ConfigureAwait(false);

                updateStatus(operationId, request, ComparisonConstants.STATUS_FINALIZING, ComparisonConstants.PROGRESS_FINALIZING);
                updateStatus(operationId, request, ComparisonConstants.STATUS_COMPLETED, ComparisonConstants.PROGRESS_COMPLETED, result: result);
            }
            // Only host/worker cancellation is a canceled operation. An unrelated OCE from the comparison body
            // falls through to the terminal failure boundary because the requested analysis did not complete.
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                updateStatus(operationId, request, ComparisonConstants.STATUS_CANCELED, ComparisonConstants.PROGRESS_QUEUED);
            }
            catch (ArgumentException exception)
            {
                _logger.LogWarning(exception, "Invalid comparison input for operation {OperationId}.", operationId);
                updateStatus(operationId, request, ComparisonConstants.STATUS_FAILED, ComparisonConstants.PROGRESS_QUEUED, error: "The comparison operation failed.");
            }
            catch (InvalidOperationException exception)
            {
                _logger.LogWarning(exception, "Comparison operation {OperationId} could not be completed.", operationId);
                updateStatus(operationId, request, ComparisonConstants.STATUS_FAILED, ComparisonConstants.PROGRESS_QUEUED, error: "The comparison operation failed.");
            }
            // A background callback has no active HTTP pipeline. It must persist a terminal status rather than
            // letting an unobserved exception abandon the operation state.
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected comparison failure for operation {OperationId}.", operationId);
                updateStatus(operationId, request, ComparisonConstants.STATUS_FAILED, ComparisonConstants.PROGRESS_QUEUED, error: ComparisonConstants.ERROR_ANALYSIS_FAILED);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>Updates the current comparison polling state.</summary>
        /// <param name="operationId">The operation status identifier.</param>
        /// <param name="request">The job input snapshot.</param>
        /// <param name="status">The new status value.</param>
        /// <param name="percentComplete">The new completion percentage.</param>
        /// <param name="result">The optional completed comparison result.</param>
        /// <param name="error">The optional terminal failure message.</param>
        /// <seealso cref="ComparisonOperationStatus"/>
        private void updateStatus(
            string operationId,
            ComparisonJobRequest request,
            string status,
            int percentComplete,
            DocumentComparisonResult? result = null,
            string? error = null)
        {
            #region implementation

            var createdAt = _statusStore.TryGetComparisonStatus(operationId, out var existingStatus)
                && existingStatus is not null
                    ? existingStatus.CreatedAt
                    : DateTime.UtcNow;

            _statusStore.SetComparisonStatus(operationId, new ComparisonOperationStatus
            {
                OperationId = operationId,
                DocumentGuid = request.DocumentGuid,
                Status = status,
                PercentComplete = percentComplete,
                ProgressUrl = request.ProgressUrl,
                Result = result,
                Error = error,
                CreatedAt = createdAt,
                UpdatedAt = DateTime.UtcNow
            });

            #endregion
        }
    }
}
