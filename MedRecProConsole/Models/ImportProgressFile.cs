namespace MedRecProConsole.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents the complete progress state for an import operation.
    /// This is the root model serialized to the queue file on disk.
    /// </summary>
    /// <remarks>
    /// The progress file is stored at the root of the directory being imported
    /// as ".medrecpro-import-queue.json". It contains all metadata needed to
    /// resume an import operation after interruption.
    ///
    /// File Format Design Goals:
    /// - Fast reads/writes using System.Text.Json
    /// - Indexed access to items by file path using a dictionary structure
    /// - Minimal file size while maintaining human readability
    /// - Atomic writes to prevent corruption on crash
    /// </remarks>
    /// <example>
    /// <code>
    /// var progressFile = new ImportProgressFile
    /// {
    ///     RootDirectory = @"C:\Imports",
    ///     ConnectionString = "Server=...;Database=...;",
    ///     CreatedAt = DateTime.UtcNow
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="ImportQueueItem"/>
    /// <seealso cref="ImportQueueStatus"/>
    /// <seealso cref="Services.ImportProgressTracker"/>
    public class ImportProgressFile
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// The default filename for the progress file.
        /// </summary>
        /// <remarks>
        /// Uses a dot prefix to hide the file on Unix-like systems.
        /// </remarks>
        public const string DefaultFileName = ".medrecpro-import-queue.json";

        /**************************************************************/
        /// <summary>
        /// The file format version for backwards compatibility.
        /// </summary>
        /// <remarks>
        /// Increment when making breaking changes to the file format.
        /// </remarks>
        public const int CurrentVersion = 1;

        #endregion

        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets the file format version.
        /// </summary>
        /// <remarks>
        /// Used for backwards compatibility when loading older queue files.
        /// </remarks>
        public int Version { get; set; } = CurrentVersion;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the unique identifier for this import session.
        /// </summary>
        /// <remarks>
        /// Used to correlate logs and detect if a different import session
        /// created this queue file.
        /// </remarks>
        public Guid SessionId { get; set; } = Guid.NewGuid();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the root directory being imported.
        /// </summary>
        /// <remarks>
        /// Stored as an absolute path for verification on resume.
        /// </remarks>
        public string RootDirectory { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the database connection string (hashed for security).
        /// </summary>
        /// <remarks>
        /// Only the hash is stored to verify the same database is being used on resume
        /// without exposing sensitive connection details.
        /// </remarks>
        public string ConnectionStringHash { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the timestamp when the queue was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the timestamp when the queue was last updated.
        /// </summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the machine name where the import was started.
        /// </summary>
        /// <remarks>
        /// Useful for diagnosing issues when queue files are shared across machines.
        /// </remarks>
        public string MachineName { get; set; } = Environment.MachineName;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the username who started the import.
        /// </summary>
        public string UserName { get; set; } = Environment.UserName;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the maximum runtime limit in minutes, if any.
        /// </summary>
        public int? MaxRuntimeMinutes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the total elapsed time across all sessions.
        /// </summary>
        /// <remarks>
        /// Accumulated time spent processing across all resume sessions.
        /// </remarks>
        public TimeSpan TotalElapsedTime { get; set; } = TimeSpan.Zero;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the count of resume attempts.
        /// </summary>
        /// <remarks>
        /// Incremented each time the import is resumed from this queue file.
        /// </remarks>
        public int ResumeCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the list of queue items.
        /// </summary>
        /// <remarks>
        /// Each item represents a file to be imported with its current status.
        /// Items are stored in the order they were discovered during directory scan.
        /// </remarks>
        /// <seealso cref="ImportQueueItem"/>
        public List<ImportQueueItem> Items { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the paths to nested queue files found in subdirectories.
        /// </summary>
        /// <remarks>
        /// When a subdirectory contains its own queue file, the parent import
        /// should respect that progress. This list tracks those nested queues.
        /// </remarks>
        public List<string> NestedQueueFiles { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets verbose mode flag for consistency across resumes.
        /// </summary>
        public bool VerboseMode { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the reason for the last interruption.
        /// </summary>
        /// <remarks>
        /// Populated when import stops due to timer, cancellation, or crash.
        /// </remarks>
        public string? LastInterruptionReason { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the timestamp of the last interruption.
        /// </summary>
        public DateTime? LastInterruptedAt { get; set; }

        #endregion

        #region computed properties

        /**************************************************************/
        /// <summary>
        /// Gets the total number of items in the queue.
        /// </summary>
        public int TotalItems => Items.Count;

        /**************************************************************/
        /// <summary>
        /// Gets the number of items that are queued (pending).
        /// </summary>
        public int QueuedItems => Items.Count(i => i.Status == ImportQueueStatus.Queued);

        /**************************************************************/
        /// <summary>
        /// Gets the number of items currently in progress.
        /// </summary>
        public int InProgressItems => Items.Count(i => i.Status == ImportQueueStatus.InProgress);

        /**************************************************************/
        /// <summary>
        /// Gets the number of completed items.
        /// </summary>
        public int CompletedItems => Items.Count(i => i.Status == ImportQueueStatus.Completed);

        /**************************************************************/
        /// <summary>
        /// Gets the number of failed items.
        /// </summary>
        public int FailedItems => Items.Count(i => i.Status == ImportQueueStatus.Failed);

        /**************************************************************/
        /// <summary>
        /// Gets the number of skipped items.
        /// </summary>
        public int SkippedItems => Items.Count(i => i.Status == ImportQueueStatus.Skipped);

        /**************************************************************/
        /// <summary>
        /// Gets the number of items remaining to process.
        /// </summary>
        /// <remarks>
        /// Includes queued and in-progress items (in-progress items are reset on resume).
        /// </remarks>
        public int RemainingItems => Items.Count(i =>
            i.Status == ImportQueueStatus.Queued ||
            i.Status == ImportQueueStatus.InProgress);

        /**************************************************************/
        /// <summary>
        /// Gets a value indicating whether the import is complete.
        /// </summary>
        /// <remarks>
        /// True when all items are either completed, failed, or skipped.
        /// </remarks>
        public bool IsComplete => RemainingItems == 0;

        /**************************************************************/
        /// <summary>
        /// Gets the completion percentage (0-100).
        /// </summary>
        public double CompletionPercentage => TotalItems > 0
            ? (double)(CompletedItems + FailedItems + SkippedItems) / TotalItems * 100
            : 0;

        #endregion
    }
}
