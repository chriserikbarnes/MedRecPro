using MedRecProConsole.Models;
using MedRecProImportClass.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedRecProConsole.Services
{
    /**************************************************************/
    /// <summary>
    /// Tracks and persists table standardization progress to enable cancellation and resumption.
    /// Manages the progress file lifecycle including creation, updates, and atomic writes.
    /// </summary>
    /// <remarks>
    /// Design Goals:
    /// - Fast async file I/O using System.Text.Json
    /// - Atomic writes to prevent corruption on crash (write to temp, then rename)
    /// - In-memory caching to minimize disk reads
    /// - Thread-safe operations via SemaphoreSlim
    /// - Connection string hash validation on resume to prevent cross-database errors
    ///
    /// The progress file is stored in the application directory and contains
    /// the last completed TextTableID range for resumption.
    /// </remarks>
    /// <example>
    /// <code>
    /// var tracker = new StandardizationProgressTracker();
    /// var progressFile = await tracker.LoadOrCreateAsync(connectionString, "parse", 1000);
    /// await tracker.UpdateProgressAsync(batchProgress);
    /// var resumeId = tracker.GetResumeStartId();
    /// </code>
    /// </example>
    /// <seealso cref="StandardizationProgressFile"/>
    /// <seealso cref="TransformBatchProgress"/>
    public class StandardizationProgressTracker
    {
        #region private fields

        /**************************************************************/
        /// <summary>The current progress file loaded in memory.</summary>
        private StandardizationProgressFile? _progressFile;

        /**************************************************************/
        /// <summary>The path to the progress file on disk.</summary>
        private string? _filePath;

        /**************************************************************/
        /// <summary>Lock object for thread-safe operations.</summary>
        private readonly SemaphoreSlim _lock = new(1, 1);

        /**************************************************************/
        /// <summary>JSON serializer options for consistent serialization.</summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Loads an existing progress file or creates a new one.
        /// </summary>
        /// <param name="connectionString">Database connection string (hashed for validation).</param>
        /// <param name="operation">Operation type: "parse" or "validate".</param>
        /// <param name="batchSize">Batch size for this run.</param>
        /// <returns>The loaded or created progress file.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when an existing progress file was created with a different connection string.
        /// </exception>
        /// <seealso cref="StandardizationProgressFile"/>
        public async Task<StandardizationProgressFile> LoadOrCreateAsync(
            string connectionString,
            string operation,
            int batchSize)
        {
            #region implementation

            await _lock.WaitAsync();
            try
            {
                _filePath = getProgressFilePath();
                var connectionHash = computeConnectionHash(connectionString);

                if (File.Exists(_filePath))
                {
                    // Load existing progress file
                    _progressFile = await loadFileAsync(_filePath);

                    // Validate connection string matches
                    if (_progressFile.ConnectionStringHash != connectionHash)
                    {
                        throw new InvalidOperationException(
                            "Progress file was created with a different database connection. " +
                            "Delete the progress file to start fresh or use the original connection.");
                    }

                    // Increment resume count
                    _progressFile.ResumeCount++;
                    _progressFile.LastUpdatedAt = DateTime.UtcNow;
                    await saveFileAsync();
                }
                else
                {
                    // Create new progress file
                    _progressFile = new StandardizationProgressFile
                    {
                        ConnectionStringHash = connectionHash,
                        Operation = operation,
                        BatchSize = batchSize
                    };
                    await saveFileAsync();
                }

                return _progressFile;
            }
            finally
            {
                _lock.Release();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Updates progress after a batch completes.
        /// </summary>
        /// <param name="batch">The batch progress report from the orchestrator.</param>
        /// <seealso cref="TransformBatchProgress"/>
        public async Task UpdateProgressAsync(TransformBatchProgress batch)
        {
            #region implementation

            await _lock.WaitAsync();
            try
            {
                if (_progressFile != null)
                {
                    _progressFile.LastCompletedMaxId = batch.RangeEnd;
                    _progressFile.TotalObservations = batch.CumulativeObservationCount;
                    _progressFile.TotalBatchesCompleted = batch.BatchNumber;
                    _progressFile.TotalElapsedTime = batch.Elapsed;
                    _progressFile.LastUpdatedAt = DateTime.UtcNow;
                    await saveFileAsync();
                }
            }
            finally
            {
                _lock.Release();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records an interruption (user cancellation, crash, etc.) for diagnostics.
        /// </summary>
        /// <param name="reason">Description of why the run was interrupted.</param>
        /// <param name="elapsed">Total elapsed time for this session.</param>
        public async Task RecordInterruptionAsync(string reason, TimeSpan elapsed)
        {
            #region implementation

            await _lock.WaitAsync();
            try
            {
                if (_progressFile != null)
                {
                    _progressFile.LastInterruptionReason = reason;
                    _progressFile.TotalElapsedTime = elapsed;
                    _progressFile.LastUpdatedAt = DateTime.UtcNow;
                    await saveFileAsync();
                }
            }
            finally
            {
                _lock.Release();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Deletes the progress file (called on successful completion).
        /// </summary>
        public async Task DeleteProgressFileAsync()
        {
            #region implementation

            await _lock.WaitAsync();
            try
            {
                if (_filePath != null && File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                    _progressFile = null;
                    _filePath = null;
                }
            }
            finally
            {
                _lock.Release();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the TextTableID to resume from, or null if no progress file exists.
        /// </summary>
        /// <returns>The next TextTableID to process (LastCompletedMaxId + 1), or null.</returns>
        public int? GetResumeStartId()
        {
            #region implementation

            if (_progressFile == null || _progressFile.LastCompletedMaxId == 0)
            {
                return null;
            }

            return _progressFile.LastCompletedMaxId + 1;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the current progress file (cached in memory).
        /// </summary>
        /// <returns>The current progress file or null if not loaded.</returns>
        public StandardizationProgressFile? GetProgressFile()
        {
            return _progressFile;
        }

        /**************************************************************/
        /// <summary>
        /// Checks if a progress file exists in the application directory.
        /// </summary>
        /// <returns>True if a progress file exists.</returns>
        public bool ProgressFileExists()
        {
            return File.Exists(getProgressFilePath());
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Gets the full path to the progress file in the application directory.
        /// </summary>
        /// <returns>Full path to the progress file.</returns>
        private static string getProgressFilePath()
        {
            #region implementation

            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDir, StandardizationProgressFile.DefaultFileName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes a SHA256 hash of the connection string for secure comparison.
        /// </summary>
        /// <param name="connectionString">The connection string to hash.</param>
        /// <returns>Base64-encoded hash.</returns>
        private static string computeConnectionHash(string connectionString)
        {
            #region implementation

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(connectionString);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads a progress file from disk.
        /// </summary>
        /// <param name="filePath">Path to the progress file.</param>
        /// <returns>Deserialized progress file.</returns>
        private static async Task<StandardizationProgressFile> loadFileAsync(string filePath)
        {
            #region implementation

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<StandardizationProgressFile>(json, JsonOptions)
                ?? new StandardizationProgressFile();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Saves the progress file to disk atomically using write-to-temp-then-rename pattern.
        /// </summary>
        private async Task saveFileAsync()
        {
            #region implementation

            if (_progressFile == null || _filePath == null)
            {
                return;
            }

            var tempPath = _filePath + ".tmp";
            var json = JsonSerializer.Serialize(_progressFile, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);

            #endregion
        }

        #endregion
    }
}
