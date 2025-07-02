using MedRecPro.Helpers;
using static MedRecPro.Models.Label;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Defines contract for storing and retrieving operation status information.
    /// </summary>
    /// <seealso cref="ImportOperationStatus"/>
    /// <seealso cref="InMemoryOperationStatusStore"/>
    /// <seealso cref="Label"/>
    public interface IOperationStatusStore
    {
        /**************************************************************/
        /// <summary>
        /// Stores or updates the status information for a specific operation.
        /// </summary>
        /// <param name="opId">The unique operation identifier.</param>
        /// <param name="status">The status information to store.</param>
        /// <seealso cref="ImportOperationStatus"/>
        /// <seealso cref="Label"/>
        void Set(string opId, ImportOperationStatus status);

        /**************************************************************/
        /// <summary>
        /// Attempts to retrieve the status information for a specific operation.
        /// </summary>
        /// <param name="opId">The unique operation identifier to look up.</param>
        /// <param name="status">The retrieved status information, or null if not found.</param>
        /// <returns>True if the operation status was found, false otherwise.</returns>
        /// <seealso cref="ImportOperationStatus"/>
        /// <seealso cref="Label"/>
        bool TryGet(string opId, out ImportOperationStatus? status);
    }

    /**************************************************************/
    /// <summary>
    /// Represents the current status and progress information for a long-running import operation.
    /// </summary>
    /// <remarks>
    /// This class tracks the complete lifecycle of an import operation including queued, running,
    /// completed, canceled, and failed states. Provides progress tracking, result storage,
    /// and error information for comprehensive operation monitoring.
    /// </remarks>
    /// <seealso cref="SplZipImportResult"/>
    /// <seealso cref="IOperationStatusStore"/>
    /// <seealso cref="Label"/>
    public class ImportOperationStatus
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the current status of the operation (Pending, Queued, Running, Completed, Canceled, Failed).
        /// </summary>
        /// <seealso cref="Label"/>
        public string Status { get; set; } = "Pending";

        /**************************************************************/
        /// <summary>
        /// Gets or sets the completion percentage of the operation (0-100).
        /// </summary>
        /// <seealso cref="Label"/>
        public int PercentComplete { get; set; } = 0;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the collection of import results from processed ZIP files.
        /// </summary>
        /// <seealso cref="SplZipImportResult"/>
        /// <seealso cref="Label"/>
        public List<SplZipImportResult>? Results { get; set; } = new List<SplZipImportResult>();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the unique identifier for this operation.
        /// </summary>
        /// <seealso cref="Label"/>
        public string? OperationId { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the URL for monitoring operation progress.
        /// </summary>
        /// <seealso cref="Label"/>
        public string? ProgressUrl { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets any error message if the operation failed.
        /// </summary>
        /// <seealso cref="Label"/>
        public string? Error { get; set; }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// In-memory implementation of operation status storage using application cache.
    /// </summary>
    /// <remarks>
    /// Provides temporary storage for import operation status with configurable expiration.
    /// Uses the PerformanceHelper cache infrastructure with prefixed keys for easy management.
    /// Default cache duration is 1 hour, which can be adjusted based on operational requirements.
    /// </remarks>
    /// <seealso cref="IOperationStatusStore"/>
    /// <seealso cref="ImportOperationStatus"/>
    /// <seealso cref="PerformanceHelper"/>
    /// <seealso cref="Label"/>
    public class InMemoryOperationStatusStore : IOperationStatusStore
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Generates a prefixed cache key for the specified operation ID.
        /// All cache keys will be prefixed for easy identification and bulk removal if needed.
        /// </summary>
        /// <param name="opId">The operation ID to create a cache key for.</param>
        /// <returns>A prefixed cache key string.</returns>
        /// <seealso cref="Label"/>
        private static string getCacheKey(string opId) => $"OperationStatus:{opId}";

        /**************************************************************/
        /// <summary>
        /// Stores or updates the status information for a specific operation in cache.
        /// </summary>
        /// <param name="opId">The unique operation identifier.</param>
        /// <param name="status">The status information to store.</param>
        /// <remarks>
        /// Cache duration is set to 1 hour. You can adjust this as needed based on
        /// how long you want to retain operation status information.
        /// </remarks>
        /// <seealso cref="ImportOperationStatus"/>
        /// <seealso cref="PerformanceHelper"/>
        /// <seealso cref="Label"/>
        public void Set(string opId, ImportOperationStatus status)
        {
            #region implementation
            // Cache for 1 hour. You can adjust this as needed.
            PerformanceHelper.SetCacheManageKey(getCacheKey(opId), status, 1.0);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to retrieve the status information for a specific operation from cache.
        /// </summary>
        /// <param name="opId">The unique operation identifier to look up.</param>
        /// <param name="status">The retrieved status information, or null if not found.</param>
        /// <returns>True if the operation status was found in cache, false otherwise.</returns>
        /// <seealso cref="ImportOperationStatus"/>
        /// <seealso cref="PerformanceHelper"/>
        /// <seealso cref="Label"/>
        public bool TryGet(string opId, out ImportOperationStatus? status)
        {
            #region implementation
            // Attempt to retrieve the status from cache
            status = PerformanceHelper.GetCache<ImportOperationStatus>(getCacheKey(opId));
            return status != null;
            #endregion
        }

        #endregion
    }
}