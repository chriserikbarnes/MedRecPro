namespace MedRecProConsole.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents the progress state for a table standardization operation.
    /// Serialized to disk to enable cancellation and resumption of long-running batch processes.
    /// </summary>
    /// <remarks>
    /// The progress file is stored in the application directory as
    /// ".medrecpro-standardization-progress.json". It tracks which batch/ID range
    /// was last successfully completed so the process can resume from that point.
    ///
    /// File Format Design Goals:
    /// - Fast reads/writes using System.Text.Json
    /// - Minimal state needed for resumption (just the last completed max ID)
    /// - Atomic writes to prevent corruption on crash
    /// - Connection string hash to prevent cross-database resumption
    /// </remarks>
    /// <example>
    /// <code>
    /// var progressFile = new StandardizationProgressFile
    /// {
    ///     Operation = "parse",
    ///     BatchSize = 1000,
    ///     ConnectionStringHash = "abc123..."
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="Services.StandardizationProgressTracker"/>
    public class StandardizationProgressFile
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// The default filename for the standardization progress file.
        /// </summary>
        public const string DefaultFileName = ".medrecpro-standardization-progress.json";

        /**************************************************************/
        /// <summary>
        /// The file format version for backwards compatibility.
        /// </summary>
        public const int CurrentVersion = 1;

        #endregion

        #region implementation

        /**************************************************************/
        /// <summary>Gets or sets the file format version.</summary>
        public int Version { get; set; } = CurrentVersion;

        /**************************************************************/
        /// <summary>Unique session identifier for this run.</summary>
        public Guid SessionId { get; set; } = Guid.NewGuid();

        /**************************************************************/
        /// <summary>
        /// SHA256 hash of the connection string used for this run.
        /// Validated on resume to prevent cross-database resumption.
        /// </summary>
        public string ConnectionStringHash { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// The standardization operation being performed: "parse" or "validate".
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>Batch size used for this run.</summary>
        public int BatchSize { get; set; }

        /**************************************************************/
        /// <summary>
        /// The highest TextTableID that was successfully processed.
        /// On resume, processing starts from this value + 1.
        /// </summary>
        public int LastCompletedMaxId { get; set; }

        /**************************************************************/
        /// <summary>Total observations written so far across all completed batches.</summary>
        public int TotalObservations { get; set; }

        /**************************************************************/
        /// <summary>Number of batches completed so far.</summary>
        public int TotalBatchesCompleted { get; set; }

        /**************************************************************/
        /// <summary>When this progress file was originally created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /**************************************************************/
        /// <summary>When this progress file was last updated.</summary>
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        /**************************************************************/
        /// <summary>
        /// Reason for the last interruption, if any (e.g., "User cancellation", "Crash: ...").
        /// </summary>
        public string? LastInterruptionReason { get; set; }

        /**************************************************************/
        /// <summary>Total wall-clock time spent processing across all sessions.</summary>
        public TimeSpan TotalElapsedTime { get; set; }

        /**************************************************************/
        /// <summary>Number of times this run has been resumed.</summary>
        public int ResumeCount { get; set; }

        #endregion
    }
}
