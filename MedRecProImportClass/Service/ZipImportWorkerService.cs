using MedRecProImportClass.Models;
using Microsoft.Extensions.Hosting;
using static MedRecProImportClass.Models.Label;

namespace MedRecProImportClass.Service
{
    /**************************************************************/
    /// <summary>
    /// Background service that continuously processes queued ZIP import tasks.
    /// </summary>
    /// <remarks>
    /// This service runs as a hosted background service that monitors the task queue for
    /// pending import operations. It processes tasks sequentially, handling errors gracefully
    /// to ensure one failed import doesn't stop the processing of subsequent tasks.
    /// The service implements a polling pattern with a 500ms delay when the queue is empty
    /// to balance responsiveness with resource efficiency.
    /// </remarks>
    /// <seealso cref="BackgroundService"/>
    /// <seealso cref="IBackgroundTaskQueueService"/>
    /// <seealso cref="Label"/>
    public class ZipImportWorkerService : BackgroundService
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// The background task queue service for retrieving queued import operations.
        /// </summary>
        /// <seealso cref="IBackgroundTaskQueueService"/>
        /// <seealso cref="Label"/>
        private readonly IBackgroundTaskQueueService _queue;

        /**************************************************************/
        /// <summary>
        /// Logger instance for tracking service operations and errors.
        /// </summary>
        /// <seealso cref="ILogger"/>
        /// <seealso cref="Label"/>
        private readonly ILogger<ZipImportWorkerService> _logger;

        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the ZipImportWorkerService class.
        /// </summary>
        /// <param name="queue">The background task queue service for retrieving import tasks.</param>
        /// <param name="logger">The logger instance for tracking operations and errors.</param>
        /// <seealso cref="IBackgroundTaskQueueService"/>
        /// <seealso cref="ILogger"/>
        /// <seealso cref="Label"/>
        public ZipImportWorkerService(IBackgroundTaskQueueService queue, ILogger<ZipImportWorkerService> logger)
        {
            #region implementation
            _queue = queue;
            _logger = logger;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Executes the background service loop that continuously processes queued import tasks.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token that signals when the service should stop.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method implements the main processing loop that:
        /// 1. Continuously monitors the task queue while the service is running
        /// 2. Dequeues and executes available import tasks
        /// 3. Handles errors gracefully to prevent service interruption
        /// 4. Implements a 500ms polling delay when no tasks are available
        /// 
        /// The service will continue running until the cancellation token is triggered,
        /// typically during application shutdown.
        /// </remarks>
        /// <seealso cref="IBackgroundTaskQueueService"/>
        /// <seealso cref="CancellationToken"/>
        /// <seealso cref="Label"/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            #region implementation
            // Continue processing until cancellation is requested
            while (!stoppingToken.IsCancellationRequested)
            {
                // Attempt to dequeue the next available import task
                if (_queue.TryDequeue(out var item))
                {
                    try
                    {
                        // Log the start of task execution
                        _logger.LogInformation("Running import task {OpId}", item.operationId);

                        // Execute the import work item with the stopping token
                        await item.workItem(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        // Log errors but continue processing other tasks
                        _logger.LogError(ex, "Error executing task {OpId}", item.operationId);
                    }
                }
                else
                {
                    // Wait if queue is empty to avoid busy waiting
                    await Task.Delay(500, stoppingToken);
                }
            }
            #endregion
        }
    }
}
