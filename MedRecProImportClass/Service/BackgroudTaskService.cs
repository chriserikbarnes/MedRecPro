using System.Collections.Concurrent;
using MedRecProImportClass.Models;
using static MedRecProImportClass.Models.Label;

namespace MedRecProImportClass.Service
{
    /**************************************************************/
    /// <summary>
    /// Defines contract for managing background task queue operations.
    /// </summary>
    /// <remarks>
    /// Provides methods for enqueueing work items with operation IDs and dequeuing them
    /// for processing by background services. Supports asynchronous task execution with
    /// cancellation token support for graceful shutdown scenarios.
    /// </remarks>
    /// <seealso cref="BackgroundTaskQueueService"/>
    /// <seealso cref="ZipImportWorkerService"/>
    /// <seealso cref="Label"/>
    public interface IBackgroundTaskQueueService
    {
        /**************************************************************/
        /// <summary>
        /// Adds a new work item to the background processing queue.
        /// </summary>
        /// <param name="operationId">Unique identifier for tracking the operation.</param>
        /// <param name="workItem">The asynchronous work function to execute.</param>
        /// <seealso cref="CancellationToken"/>
        /// <seealso cref="Label"/>
        void Enqueue(string operationId, Func<CancellationToken, Task> workItem);

        /**************************************************************/
        /// <summary>
        /// Attempts to remove and return the next work item from the queue.
        /// </summary>
        /// <param name="item">The dequeued operation ID and work item tuple, or default if queue is empty.</param>
        /// <returns>True if an item was successfully dequeued, false if the queue is empty.</returns>
        /// <seealso cref="CancellationToken"/>
        /// <seealso cref="Label"/>
        bool TryDequeue(out (string operationId, Func<CancellationToken, Task> workItem) item);
    }

    /**************************************************************/
    /// <summary>
    /// Thread-safe implementation of background task queue using concurrent collections.
    /// </summary>
    /// <remarks>
    /// Provides a simple FIFO (First In, First Out) queue for background task management.
    /// Uses ConcurrentQueue for thread-safe operations without requiring explicit locking.
    /// Suitable for scenarios where tasks should be processed in the order they were submitted.
    /// The queue stores tuples containing operation IDs and their corresponding work functions.
    /// </remarks>
    /// <seealso cref="IBackgroundTaskQueueService"/>
    /// <seealso cref="ConcurrentQueue{T}"/>
    /// <seealso cref="ZipImportWorkerService"/>
    /// <seealso cref="Label"/>
    public class BackgroundTaskQueueService : IBackgroundTaskQueueService
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Thread-safe queue for storing operation IDs and their associated work items.
        /// </summary>
        /// <seealso cref="ConcurrentQueue{T}"/>
        /// <seealso cref="Label"/>
        private readonly ConcurrentQueue<(string, Func<CancellationToken, Task>)> _queue = new();

        #endregion

        /**************************************************************/
        /// <summary>
        /// Adds a new work item to the background processing queue in a thread-safe manner.
        /// </summary>
        /// <param name="operationId">Unique identifier for tracking the operation throughout its lifecycle.</param>
        /// <param name="workItem">The asynchronous work function to execute, accepting a cancellation token.</param>
        /// <remarks>
        /// The work item will be added to the end of the queue and processed in FIFO order.
        /// This operation is thread-safe and can be called concurrently from multiple threads.
        /// </remarks>
        /// <seealso cref="CancellationToken"/>
        /// <seealso cref="ConcurrentQueue{T}"/>
        /// <seealso cref="Label"/>
        public void Enqueue(string operationId, Func<CancellationToken, Task> workItem)
        {
            #region implementation
            // Add the operation and work item as a tuple to the concurrent queue
            _queue.Enqueue((operationId, workItem));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to remove and return the next work item from the queue in a thread-safe manner.
        /// </summary>
        /// <param name="item">The dequeued operation ID and work item tuple, or default if queue is empty.</param>
        /// <returns>True if an item was successfully dequeued, false if the queue is empty.</returns>
        /// <remarks>
        /// This operation follows FIFO ordering, returning the oldest enqueued item first.
        /// The method is thread-safe and will not block if the queue is empty.
        /// If the queue is empty, the item parameter will contain default values.
        /// </remarks>
        /// <seealso cref="CancellationToken"/>
        /// <seealso cref="ConcurrentQueue{T}"/>
        /// <seealso cref="Label"/>
        public bool TryDequeue(out (string, Func<CancellationToken, Task>) item)
        {
            #region implementation
            // Attempt to dequeue the next item from the concurrent queue
            return _queue.TryDequeue(out item);
            #endregion
        }
    }
}