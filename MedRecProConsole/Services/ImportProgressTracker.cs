using MedRecProConsole.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedRecProConsole.Services
{
    /**************************************************************/
    /// <summary>
    /// Service for tracking and persisting import progress to enable crash recovery and resume functionality.
    /// Manages the queue file lifecycle including creation, updates, and atomic writes.
    /// </summary>
    /// <remarks>
    /// Design Goals:
    /// - Fast async file I/O using System.Text.Json
    /// - Atomic writes to prevent corruption on crash (write to temp, then rename)
    /// - In-memory caching to minimize disk reads
    /// - Batch updates to reduce write frequency
    /// - Thread-safe operations for concurrent access
    ///
    /// The queue file is stored at the root of the import directory and contains
    /// the complete state needed to resume an interrupted import.
    /// </remarks>
    /// <example>
    /// <code>
    /// var tracker = new ImportProgressTracker();
    /// var progressFile = await tracker.LoadOrCreateQueueAsync(importFolder, connectionString);
    /// await tracker.UpdateItemStatusAsync(filePath, ImportQueueStatus.InProgress);
    /// await tracker.MarkItemCompletedAsync(filePath, result);
    /// </code>
    /// </example>
    /// <seealso cref="ImportProgressFile"/>
    /// <seealso cref="ImportQueueItem"/>
    /// <seealso cref="ImportQueueStatus"/>
    public class ImportProgressTracker
    {
        #region private fields

        /**************************************************************/
        /// <summary>
        /// The current progress file loaded in memory.
        /// </summary>
        private ImportProgressFile? _progressFile;

        /**************************************************************/
        /// <summary>
        /// The path to the queue file on disk.
        /// </summary>
        private string? _queueFilePath;

        /**************************************************************/
        /// <summary>
        /// Lock object for thread-safe operations.
        /// </summary>
        private readonly SemaphoreSlim _lock = new(1, 1);

        /**************************************************************/
        /// <summary>
        /// Counter for pending updates to batch writes.
        /// </summary>
        private int _pendingUpdates;

        /**************************************************************/
        /// <summary>
        /// Threshold for auto-saving after a number of updates.
        /// </summary>
        private const int AutoSaveThreshold = 5;

        /**************************************************************/
        /// <summary>
        /// JSON serializer options for fast, consistent serialization.
        /// </summary>
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
        /// Checks if a queue file exists at the specified directory.
        /// </summary>
        /// <param name="importFolder">The import directory to check</param>
        /// <returns>True if a queue file exists, false otherwise</returns>
        /// <seealso cref="ImportProgressFile"/>
        public bool QueueFileExists(string importFolder)
        {
            #region implementation

            var queueFilePath = getQueueFilePath(importFolder);
            return File.Exists(queueFilePath);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads an existing queue file or creates a new one for the import operation.
        /// </summary>
        /// <param name="importFolder">The root import directory</param>
        /// <param name="connectionString">The database connection string</param>
        /// <param name="zipFiles">List of discovered ZIP files (used only for new queues)</param>
        /// <param name="maxRuntimeMinutes">Optional maximum runtime in minutes</param>
        /// <param name="verboseMode">Whether verbose mode is enabled</param>
        /// <returns>The loaded or created progress file</returns>
        /// <remarks>
        /// If an existing queue file is found, it validates the connection string hash
        /// and resets any in-progress items to queued status (they may have been
        /// interrupted mid-process).
        /// </remarks>
        /// <seealso cref="ImportProgressFile"/>
        /// <seealso cref="ImportQueueItem"/>
        public async Task<ImportProgressFile> LoadOrCreateQueueAsync(
            string importFolder,
            string connectionString,
            List<string>? zipFiles = null,
            int? maxRuntimeMinutes = null,
            bool verboseMode = false)
        {
            #region implementation

            await _lock.WaitAsync();
            try
            {
                _queueFilePath = getQueueFilePath(importFolder);
                var connectionHash = computeConnectionHash(connectionString);

                // Check if queue file exists
                if (File.Exists(_queueFilePath))
                {
                    // Load existing queue
                    _progressFile = await loadQueueFileAsync(_queueFilePath);

                    // Validate connection string matches
                    if (_progressFile.ConnectionStringHash != connectionHash)
                    {
                        throw new InvalidOperationException(
                            "Queue file was created with a different database connection. " +
                            "Delete the queue file to start fresh or use the original connection.");
                    }

                    // Reset any in-progress items to queued (they were interrupted)
                    resetInProgressItems();

                    // Increment resume count
                    _progressFile.ResumeCount++;
                    _progressFile.LastUpdatedAt = DateTime.UtcNow;

                    // Check for nested queue files
                    await scanForNestedQueueFilesAsync(importFolder);

                    // Save the updated queue file
                    await saveQueueFileAsync();
                }
                else
                {
                    // Create new queue file
                    if (zipFiles == null || zipFiles.Count == 0)
                    {
                        throw new ArgumentException("zipFiles must be provided when creating a new queue", nameof(zipFiles));
                    }

                    _progressFile = await createNewQueueAsync(
                        importFolder,
                        connectionHash,
                        zipFiles,
                        maxRuntimeMinutes,
                        verboseMode);

                    await saveQueueFileAsync();
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
        /// Gets the list of files that need to be processed (queued status).
        /// </summary>
        /// <returns>List of file paths that are queued for processing</returns>
        /// <seealso cref="ImportQueueItem"/>
        public List<string> GetPendingFiles()
        {
            #region implementation

            if (_progressFile == null)
            {
                return new List<string>();
            }

            return _progressFile.Items
                .Where(i => i.Status == ImportQueueStatus.Queued)
                .Select(i => i.FilePath)
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the current progress file (cached in memory).
        /// </summary>
        /// <returns>The current progress file or null if not loaded</returns>
        /// <seealso cref="ImportProgressFile"/>
        public ImportProgressFile? GetProgressFile()
        {
            return _progressFile;
        }

        /**************************************************************/
        /// <summary>
        /// Marks a file as in-progress and records the start timestamp.
        /// </summary>
        /// <param name="filePath">The file path to update</param>
        /// <seealso cref="ImportQueueStatus"/>
        public async Task MarkItemInProgressAsync(string filePath)
        {
            #region implementation

            await _lock.WaitAsync();
            try
            {
                var item = findItem(filePath);
                if (item != null)
                {
                    item.Status = ImportQueueStatus.InProgress;
                    item.StartedAt = DateTime.UtcNow;
                    _progressFile!.LastUpdatedAt = DateTime.UtcNow;

                    // Always save immediately when marking in-progress for crash safety
                    await saveQueueFileAsync();
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
        /// Marks a file as completed and records the result statistics.
        /// </summary>
        /// <param name="filePath">The file path to update</param>
        /// <param name="documentsCreated">Number of documents created</param>
        /// <param name="organizationsCreated">Number of organizations created</param>
        /// <param name="productsCreated">Number of products created</param>
        /// <param name="sectionsCreated">Number of sections created</param>
        /// <param name="ingredientsCreated">Number of ingredients created</param>
        /// <seealso cref="ImportQueueStatus"/>
        public async Task MarkItemCompletedAsync(
            string filePath,
            int documentsCreated = 0,
            int organizationsCreated = 0,
            int productsCreated = 0,
            int sectionsCreated = 0,
            int ingredientsCreated = 0)
        {
            #region implementation

            await _lock.WaitAsync();
            try
            {
                var item = findItem(filePath);
                if (item != null)
                {
                    item.Status = ImportQueueStatus.Completed;
                    item.CompletedAt = DateTime.UtcNow;
                    item.DocumentsCreated = documentsCreated;
                    item.OrganizationsCreated = organizationsCreated;
                    item.ProductsCreated = productsCreated;
                    item.SectionsCreated = sectionsCreated;
                    item.IngredientsCreated = ingredientsCreated;

                    // Calculate processing duration
                    if (item.StartedAt.HasValue)
                    {
                        item.ProcessingDurationMs = (long)(item.CompletedAt.Value - item.StartedAt.Value).TotalMilliseconds;
                    }

                    _progressFile!.LastUpdatedAt = DateTime.UtcNow;
                    await incrementAndMaybeSaveAsync();
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
        /// Marks a file as failed and records the error message.
        /// </summary>
        /// <param name="filePath">The file path to update</param>
        /// <param name="errorMessage">The error message</param>
        /// <seealso cref="ImportQueueStatus"/>
        public async Task MarkItemFailedAsync(string filePath, string errorMessage)
        {
            #region implementation

            await _lock.WaitAsync();
            try
            {
                var item = findItem(filePath);
                if (item != null)
                {
                    item.Status = ImportQueueStatus.Failed;
                    item.CompletedAt = DateTime.UtcNow;
                    item.ErrorMessage = errorMessage;
                    item.RetryCount++;

                    // Calculate processing duration
                    if (item.StartedAt.HasValue)
                    {
                        item.ProcessingDurationMs = (long)(item.CompletedAt.Value - item.StartedAt.Value).TotalMilliseconds;
                    }

                    _progressFile!.LastUpdatedAt = DateTime.UtcNow;
                    await incrementAndMaybeSaveAsync();
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
        /// Marks a file as skipped (e.g., processed by nested queue).
        /// </summary>
        /// <param name="filePath">The file path to update</param>
        /// <param name="reason">Reason for skipping</param>
        /// <seealso cref="ImportQueueStatus"/>
        public async Task MarkItemSkippedAsync(string filePath, string? reason = null)
        {
            #region implementation

            await _lock.WaitAsync();
            try
            {
                var item = findItem(filePath);
                if (item != null)
                {
                    item.Status = ImportQueueStatus.Skipped;
                    item.CompletedAt = DateTime.UtcNow;
                    item.ErrorMessage = reason;
                    _progressFile!.LastUpdatedAt = DateTime.UtcNow;
                    await incrementAndMaybeSaveAsync();
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
        /// Records an interruption (timer expiration, cancellation, etc.).
        /// </summary>
        /// <param name="reason">The reason for interruption</param>
        /// <param name="elapsedTime">Time spent in this session</param>
        public async Task RecordInterruptionAsync(string reason, TimeSpan elapsedTime)
        {
            #region implementation

            await _lock.WaitAsync();
            try
            {
                if (_progressFile != null)
                {
                    _progressFile.LastInterruptionReason = reason;
                    _progressFile.LastInterruptedAt = DateTime.UtcNow;
                    _progressFile.TotalElapsedTime += elapsedTime;
                    _progressFile.LastUpdatedAt = DateTime.UtcNow;

                    // Reset any in-progress items to queued
                    resetInProgressItems();

                    await saveQueueFileAsync();
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
        /// Forces an immediate save of the queue file.
        /// </summary>
        public async Task FlushAsync()
        {
            #region implementation

            await _lock.WaitAsync();
            try
            {
                _pendingUpdates = 0;
                await saveQueueFileAsync();
            }
            finally
            {
                _lock.Release();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Deletes the queue file (called when import completes successfully).
        /// </summary>
        public async Task DeleteQueueFileAsync()
        {
            #region implementation

            await _lock.WaitAsync();
            try
            {
                if (_queueFilePath != null && File.Exists(_queueFilePath))
                {
                    File.Delete(_queueFilePath);
                    _progressFile = null;
                    _queueFilePath = null;
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
        /// Adds newly discovered ZIP files to the existing queue.
        /// Files that already exist in the queue are ignored.
        /// </summary>
        /// <param name="newZipFiles">List of newly discovered ZIP file paths</param>
        /// <returns>Number of new files added to the queue</returns>
        /// <remarks>
        /// This method is used when resuming an import to add any files that were
        /// added to the import folder since the queue was last processed.
        /// New files are added with Queued status.
        /// </remarks>
        /// <seealso cref="ImportQueueItem"/>
        public async Task<int> AddNewFilesToQueueAsync(List<string> newZipFiles)
        {
            #region implementation

            if (_progressFile == null || newZipFiles == null || newZipFiles.Count == 0)
            {
                return 0;
            }

            await _lock.WaitAsync();
            try
            {
                var addedCount = 0;

                // Get set of existing file paths for fast lookup (case-insensitive)
                var existingPaths = new HashSet<string>(
                    _progressFile.Items.Select(i => i.FilePath),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var zipFile in newZipFiles)
                {
                    // Skip if already in queue
                    if (existingPaths.Contains(zipFile))
                    {
                        continue;
                    }

                    // Create new queue item
                    var item = new ImportQueueItem
                    {
                        FilePath = zipFile,
                        Status = ImportQueueStatus.Queued
                    };

                    // Try to get file size for progress estimation
                    try
                    {
                        var fileInfo = new FileInfo(zipFile);
                        if (fileInfo.Exists)
                        {
                            item.FileSizeBytes = fileInfo.Length;
                        }
                    }
                    catch
                    {
                        // Ignore file info errors
                    }

                    _progressFile.Items.Add(item);
                    addedCount++;
                }

                if (addedCount > 0)
                {
                    _progressFile.LastUpdatedAt = DateTime.UtcNow;
                    await saveQueueFileAsync();
                }

                return addedCount;
            }
            finally
            {
                _lock.Release();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if a file should be skipped due to nested queue progress.
        /// </summary>
        /// <param name="filePath">The file path to check</param>
        /// <returns>True if the file is marked as completed/skipped in a nested queue</returns>
        public async Task<bool> IsFileCompletedInNestedQueueAsync(string filePath)
        {
            #region implementation

            if (_progressFile == null)
            {
                return false;
            }

            // Check each nested queue file
            foreach (var nestedQueuePath in _progressFile.NestedQueueFiles)
            {
                if (!File.Exists(nestedQueuePath))
                {
                    continue;
                }

                try
                {
                    var nestedQueue = await loadQueueFileAsync(nestedQueuePath);
                    var item = nestedQueue.Items.FirstOrDefault(i =>
                        i.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

                    if (item != null &&
                        (item.Status == ImportQueueStatus.Completed || item.Status == ImportQueueStatus.Skipped))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore errors reading nested queue files
                }
            }

            return false;

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Gets the full path to the queue file for a given directory.
        /// </summary>
        /// <param name="importFolder">The import folder</param>
        /// <returns>Full path to the queue file</returns>
        private static string getQueueFilePath(string importFolder)
        {
            return Path.Combine(importFolder, ImportProgressFile.DefaultFileName);
        }

        /**************************************************************/
        /// <summary>
        /// Computes a SHA256 hash of the connection string for secure comparison.
        /// </summary>
        /// <param name="connectionString">The connection string to hash</param>
        /// <returns>Base64-encoded hash</returns>
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
        /// Loads a queue file from disk asynchronously.
        /// </summary>
        /// <param name="filePath">Path to the queue file</param>
        /// <returns>The loaded progress file</returns>
        private static async Task<ImportProgressFile> loadQueueFileAsync(string filePath)
        {
            #region implementation

            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var progressFile = await JsonSerializer.DeserializeAsync<ImportProgressFile>(stream, JsonOptions);
            return progressFile ?? throw new InvalidOperationException("Failed to deserialize queue file");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Saves the queue file to disk atomically using write-to-temp-then-rename pattern.
        /// </summary>
        private async Task saveQueueFileAsync()
        {
            #region implementation

            if (_progressFile == null || _queueFilePath == null)
            {
                return;
            }

            // Write to a temporary file first
            var tempFilePath = _queueFilePath + ".tmp";

            await using (var stream = new FileStream(
                tempFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, _progressFile, JsonOptions);
            }

            // Atomic rename (overwrites existing file)
            File.Move(tempFilePath, _queueFilePath, overwrite: true);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new queue file with the discovered ZIP files.
        /// </summary>
        /// <param name="importFolder">The root import folder</param>
        /// <param name="connectionHash">Hash of the connection string</param>
        /// <param name="zipFiles">List of ZIP file paths</param>
        /// <param name="maxRuntimeMinutes">Optional max runtime</param>
        /// <param name="verboseMode">Verbose mode flag</param>
        /// <returns>The new progress file</returns>
        private async Task<ImportProgressFile> createNewQueueAsync(
            string importFolder,
            string connectionHash,
            List<string> zipFiles,
            int? maxRuntimeMinutes,
            bool verboseMode)
        {
            #region implementation

            var progressFile = new ImportProgressFile
            {
                RootDirectory = Path.GetFullPath(importFolder),
                ConnectionStringHash = connectionHash,
                MaxRuntimeMinutes = maxRuntimeMinutes,
                VerboseMode = verboseMode,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            // Create queue items for each ZIP file
            foreach (var zipFile in zipFiles)
            {
                var item = new ImportQueueItem
                {
                    FilePath = zipFile,
                    Status = ImportQueueStatus.Queued
                };

                // Try to get file size for progress estimation
                try
                {
                    var fileInfo = new FileInfo(zipFile);
                    if (fileInfo.Exists)
                    {
                        item.FileSizeBytes = fileInfo.Length;
                    }
                }
                catch
                {
                    // Ignore file info errors
                }

                progressFile.Items.Add(item);
            }

            // Scan for nested queue files
            await scanForNestedQueueFilesAsync(importFolder, progressFile);

            return progressFile;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Scans for nested queue files in subdirectories.
        /// </summary>
        /// <param name="importFolder">The import folder to scan</param>
        /// <param name="progressFile">Optional progress file to update (uses _progressFile if null)</param>
        private async Task scanForNestedQueueFilesAsync(string importFolder, ImportProgressFile? progressFile = null)
        {
            #region implementation

            var targetFile = progressFile ?? _progressFile;
            if (targetFile == null)
            {
                return;
            }

            targetFile.NestedQueueFiles.Clear();

            try
            {
                // Search all subdirectories for queue files
                var nestedFiles = Directory.GetFiles(
                    importFolder,
                    ImportProgressFile.DefaultFileName,
                    SearchOption.AllDirectories);

                // Exclude the root queue file
                var rootQueuePath = getQueueFilePath(importFolder);
                foreach (var nestedFile in nestedFiles)
                {
                    if (!nestedFile.Equals(rootQueuePath, StringComparison.OrdinalIgnoreCase))
                    {
                        targetFile.NestedQueueFiles.Add(nestedFile);

                        // Mark files covered by nested queue as skipped
                        await markNestedQueueFilesAsSkippedAsync(nestedFile, targetFile);
                    }
                }
            }
            catch
            {
                // Ignore directory access errors
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Marks files that are already handled by a nested queue as skipped.
        /// </summary>
        /// <param name="nestedQueuePath">Path to the nested queue file</param>
        /// <param name="progressFile">The progress file to update</param>
        private async Task markNestedQueueFilesAsSkippedAsync(string nestedQueuePath, ImportProgressFile progressFile)
        {
            #region implementation

            try
            {
                var nestedQueue = await loadQueueFileAsync(nestedQueuePath);

                // Mark all completed/in-progress items from nested queue as skipped in parent
                foreach (var nestedItem in nestedQueue.Items.Where(i =>
                    i.Status == ImportQueueStatus.Completed ||
                    i.Status == ImportQueueStatus.InProgress ||
                    i.Status == ImportQueueStatus.Skipped))
                {
                    var parentItem = progressFile.Items.FirstOrDefault(i =>
                        i.FilePath.Equals(nestedItem.FilePath, StringComparison.OrdinalIgnoreCase));

                    if (parentItem != null && parentItem.Status == ImportQueueStatus.Queued)
                    {
                        parentItem.Status = ImportQueueStatus.Skipped;
                        parentItem.NestedQueueFilePath = nestedQueuePath;
                        parentItem.ErrorMessage = "Processed by nested queue";
                    }
                }
            }
            catch
            {
                // Ignore errors reading nested queue
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resets any in-progress items back to queued status.
        /// </summary>
        /// <remarks>
        /// Called on resume to handle items that were interrupted mid-process.
        /// </remarks>
        private void resetInProgressItems()
        {
            #region implementation

            if (_progressFile == null)
            {
                return;
            }

            foreach (var item in _progressFile.Items.Where(i => i.Status == ImportQueueStatus.InProgress))
            {
                item.Status = ImportQueueStatus.Queued;
                item.StartedAt = null;
                item.RetryCount++;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an item by file path.
        /// </summary>
        /// <param name="filePath">The file path to find</param>
        /// <returns>The matching item or null</returns>
        private ImportQueueItem? findItem(string filePath)
        {
            return _progressFile?.Items.FirstOrDefault(i =>
                i.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        }

        /**************************************************************/
        /// <summary>
        /// Increments the pending update counter and saves if threshold is reached.
        /// </summary>
        private async Task incrementAndMaybeSaveAsync()
        {
            #region implementation

            _pendingUpdates++;

            // Auto-save after threshold number of updates for performance
            if (_pendingUpdates >= AutoSaveThreshold)
            {
                _pendingUpdates = 0;
                await saveQueueFileAsync();
            }

            #endregion
        }

        #endregion
    }
}
