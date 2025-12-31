namespace MedRecProConsole.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents the status of a file in the import queue.
    /// </summary>
    /// <remarks>
    /// Used to track progress of individual files during the import process.
    /// Transitions: Queued -> InProgress -> Completed/Failed/Skipped
    /// </remarks>
    /// <seealso cref="ImportQueueItem"/>
    /// <seealso cref="ImportProgressFile"/>
    public enum ImportQueueStatus
    {
        /// <summary>
        /// File is waiting to be processed.
        /// </summary>
        Queued = 0,

        /// <summary>
        /// File is currently being processed.
        /// </summary>
        InProgress = 1,

        /// <summary>
        /// File was successfully imported.
        /// </summary>
        Completed = 2,

        /// <summary>
        /// File import failed with an error.
        /// </summary>
        Failed = 3,

        /// <summary>
        /// File was skipped (e.g., already processed in a nested queue).
        /// </summary>
        Skipped = 4
    }

    /**************************************************************/
    /// <summary>
    /// Represents a single file item in the import queue with its current status and metadata.
    /// </summary>
    /// <remarks>
    /// This model is used for tracking individual file progress within an import operation.
    /// Designed for fast serialization/deserialization using System.Text.Json.
    /// </remarks>
    /// <example>
    /// <code>
    /// var item = new ImportQueueItem
    /// {
    ///     FilePath = @"C:\Imports\file1.zip",
    ///     Status = ImportQueueStatus.Queued
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="ImportQueueStatus"/>
    /// <seealso cref="ImportProgressFile"/>
    /// <seealso cref="Services.ImportProgressTracker"/>
    public class ImportQueueItem
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the absolute path to the file.
        /// </summary>
        /// <remarks>
        /// Stored as an absolute path to ensure consistency across sessions.
        /// </remarks>
        public string FilePath { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the current status of this file in the queue.
        /// </summary>
        /// <seealso cref="ImportQueueStatus"/>
        public ImportQueueStatus Status { get; set; } = ImportQueueStatus.Queued;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the timestamp when processing started for this file.
        /// </summary>
        /// <remarks>
        /// Null if the file has not yet started processing.
        /// Used to detect stale InProgress items on resume.
        /// </remarks>
        public DateTime? StartedAt { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the timestamp when processing completed for this file.
        /// </summary>
        /// <remarks>
        /// Null if the file has not yet completed processing.
        /// </remarks>
        public DateTime? CompletedAt { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the error message if the file failed to import.
        /// </summary>
        /// <remarks>
        /// Null or empty if the file has not failed.
        /// </remarks>
        public string? ErrorMessage { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of retry attempts for this file.
        /// </summary>
        /// <remarks>
        /// Incremented each time the file is retried after a failure.
        /// </remarks>
        public int RetryCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of documents created during import.
        /// </summary>
        /// <remarks>
        /// Populated on successful completion.
        /// </remarks>
        public int DocumentsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of organizations created during import.
        /// </summary>
        public int OrganizationsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of products created during import.
        /// </summary>
        public int ProductsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of sections created during import.
        /// </summary>
        public int SectionsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of ingredients created during import.
        /// </summary>
        public int IngredientsCreated { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        /// <remarks>
        /// Captured at queue creation time for progress estimation.
        /// </remarks>
        public long FileSizeBytes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the processing duration in milliseconds.
        /// </summary>
        /// <remarks>
        /// Calculated on completion for performance tracking.
        /// </remarks>
        public long ProcessingDurationMs { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the path to a nested queue file if this file is a directory containing its own queue.
        /// </summary>
        /// <remarks>
        /// When resuming, if this property is set, the nested queue file's progress should be respected.
        /// </remarks>
        public string? NestedQueueFilePath { get; set; }

        #endregion
    }
}
