using MedRecProConsole.Models;
using MedRecProImportClass.Models;
using System.Collections.Concurrent;
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
    /// - In-process transaction coordination by canonical progress-file path
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
        private readonly string _filePath;

        /**************************************************************/
        /// <summary>Compares canonical paths according to the host operating system's path semantics.</summary>
        private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        /**************************************************************/
        /// <summary>Coordinates complete disk transactions for each canonical path within this process.</summary>
        /// <remarks>
        /// Entries intentionally remain for the process lifetime so a path can never acquire two active semaphores.
        /// Production uses one default path and the bounded test process uses short-lived isolated paths.
        /// </remarks>
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathLocks = new(PathComparer);

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

        #region constructors

        /**************************************************************/
        /// <summary>
        /// Initializes a tracker that uses the established application-directory progress file.
        /// </summary>
        /// <remarks>
        /// This constructor preserves the public default path and filename used by the console application.
        /// </remarks>
        /// <example>
        /// <code>
        /// var tracker = new StandardizationProgressTracker();
        /// </code>
        /// </example>
        /// <seealso cref="StandardizationProgressFile.DefaultFileName"/>
        public StandardizationProgressTracker()
            : this(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                StandardizationProgressFile.DefaultFileName))
        {
        }

        /**************************************************************/
        /// <summary>
        /// Initializes a tracker with an explicit progress-file path for internal integration and isolated tests.
        /// </summary>
        /// <remarks>
        /// The full path is canonicalized once. This seam is internal so it cannot become a public storage setting.
        /// </remarks>
        /// <param name="progressFilePath">The full or relative path to the progress JSON file.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="progressFilePath"/> is blank.</exception>
        /// <seealso cref="StandardizationProgressFile"/>
        internal StandardizationProgressTracker(string progressFilePath)
        {
            #region implementation

            ArgumentException.ThrowIfNullOrWhiteSpace(progressFilePath);
            _filePath = Path.GetFullPath(progressFilePath);

            #endregion
        }

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

            var pathLock = getPathLock();
            await pathLock.WaitAsync();
            try
            {
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
                pathLock.Release();
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

            var pathLock = getPathLock();
            await pathLock.WaitAsync();
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
                pathLock.Release();
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

            var pathLock = getPathLock();
            await pathLock.WaitAsync();
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
                pathLock.Release();
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

            var pathLock = getPathLock();
            await pathLock.WaitAsync();
            try
            {
                if (File.Exists(_filePath))
                {
                    File.Delete(_filePath);
                    _progressFile = null;
                }
            }
            finally
            {
                pathLock.Release();
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
            #region implementation

            var pathLock = getPathLock();
            pathLock.Wait();
            try
            {
                return File.Exists(_filePath);
            }
            finally
            {
                pathLock.Release();
            }

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Gets the process-wide transaction lock for this tracker's canonical path.
        /// </summary>
        /// <returns>The retained semaphore shared by every tracker using the same canonical path.</returns>
        private SemaphoreSlim getPathLock()
        {
            #region implementation

            return PathLocks.GetOrAdd(_filePath, static _ => new SemaphoreSlim(1, 1));

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

            if (_progressFile == null)
            {
                return;
            }

            var directory = Path.GetDirectoryName(_filePath)
                ?? throw new InvalidOperationException("The progress file path has no parent directory.");
            var tempPath = Path.Combine(
                directory,
                $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");
            var json = JsonSerializer.Serialize(_progressFile, JsonOptions);
            try
            {
                await File.WriteAllTextAsync(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            finally
            {
                deleteTemporaryFileBestEffort(tempPath);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Deletes only the exact temporary file created by the current atomic write when it still exists.
        /// </summary>
        /// <remarks>
        /// Cleanup failures are intentionally suppressed so they cannot replace the primary write or move exception.
        /// </remarks>
        /// <param name="tempPath">The unique same-directory temporary file path owned by the current write.</param>
        private static void deleteTemporaryFileBestEffort(string tempPath)
        {
            #region implementation

            try
            {
                File.Delete(tempPath);
            }
            catch (IOException)
            {
                // Preserve the original atomic-write failure; a later operator cleanup may remove this exact orphan.
            }
            catch (UnauthorizedAccessException)
            {
                // Preserve the original atomic-write failure when the filesystem denies cleanup.
            }

            #endregion
        }

        #endregion
    }
}
