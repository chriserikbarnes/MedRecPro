using MedRecProConsole.Models;
using MedRecProConsole.Services;
using MedRecProImportClass.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace MedRecProTest.Unit.Standardization
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="StandardizationProgressTracker"/> — the progress
    /// tracking service that enables cancellation and resumption of table standardization.
    /// </summary>
    /// <remarks>
    /// Tests cover:
    /// - Creating new progress files
    /// - Loading existing progress files
    /// - Connection hash validation on resume
    /// - Updating progress after batch completion
    /// - Recording interruptions
    /// - Calculating resume start ID
    /// - Deleting progress files on completion
    ///
    /// Uses a temporary directory for file I/O isolation between tests.
    /// </remarks>
    /// <seealso cref="StandardizationProgressTracker"/>
    /// <seealso cref="StandardizationProgressFile"/>
    [TestClass]
    [TestCategory("Unit")]
    public class StandardizationProgressTrackerTests
    {
        #region private fields

        /**************************************************************/
        /// <summary>The isolated temporary directory owned by the current test.</summary>
        private string _testDirectory = null!;

        /**************************************************************/
        /// <summary>The isolated progress-file path used by the current test.</summary>
        private string _progressFilePath = null!;

        #endregion

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a unique temporary directory and progress-file path for the current test.
        /// </summary>
        [TestInitialize]
        public void CreateIsolatedProgressPath()
        {
            #region implementation

            _testDirectory = Path.Combine(
                Path.GetTempPath(),
                nameof(StandardizationProgressTrackerTests),
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            _progressFilePath = Path.Combine(
                _testDirectory,
                StandardizationProgressFile.DefaultFileName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Removes only the isolated temporary directory owned by the current test.
        /// </summary>
        [TestCleanup]
        public void RemoveIsolatedProgressDirectory()
        {
            #region implementation

            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Waits for a shared start signal before loading or creating progress through one tracker instance.
        /// </summary>
        /// <param name="tracker">The tracker participating in the concurrent operation.</param>
        /// <param name="startTask">The task that releases every participating tracker.</param>
        /// <param name="connectionString">The connection identity to load or create.</param>
        /// <returns>The loaded or created progress state.</returns>
        /// <seealso cref="StandardizationProgressTracker.LoadOrCreateAsync"/>
        private static async Task<StandardizationProgressFile> loadAfterStartAsync(
            StandardizationProgressTracker tracker,
            Task startTask,
            string connectionString)
        {
            #region implementation

            await startTask;
            return await tracker.LoadOrCreateAsync(connectionString, "parse", 1000);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Reads and deserializes the persisted progress JSON for disk-state assertions.
        /// </summary>
        /// <param name="progressPath">The progress JSON path to read.</param>
        /// <returns>The deserialized progress state.</returns>
        /// <seealso cref="StandardizationProgressFile"/>
        private static async Task<StandardizationProgressFile> readProgressFileAsync(string progressPath)
        {
            #region implementation

            var json = await File.ReadAllTextAsync(progressPath);
            return JsonSerializer.Deserialize<StandardizationProgressFile>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("The persisted progress JSON was empty.");

            #endregion
        }

        #endregion

        #region LoadOrCreate Tests

        /**************************************************************/
        /// <summary>
        /// LoadOrCreateAsync with no existing file creates a new progress file.
        /// </summary>
        [TestMethod]
        public async Task LoadOrCreate_NoExistingFile_CreatesNew()
        {
            var tracker = new StandardizationProgressTracker(_progressFilePath);

            var progress = await tracker.LoadOrCreateAsync("Server=test", "parse", 1000);

            Assert.IsNotNull(progress);
            Assert.AreEqual("parse", progress.Operation);
            Assert.AreEqual(1000, progress.BatchSize);
            Assert.AreEqual(0, progress.ResumeCount);
            Assert.AreEqual(0, progress.LastCompletedMaxId);
        }

        /**************************************************************/
        /// <summary>
        /// LoadOrCreateAsync with existing file loads and increments ResumeCount.
        /// </summary>
        [TestMethod]
        public async Task LoadOrCreate_ExistingFile_LoadsAndIncrementsResume()
        {
            var tracker = new StandardizationProgressTracker(_progressFilePath);
            var connectionString = "Server=test-resume";

            // Create initial
            await tracker.LoadOrCreateAsync(connectionString, "parse", 1000);

            // Simulate progress
            await tracker.UpdateProgressAsync(new TransformBatchProgress
            {
                BatchNumber = 5,
                TotalBatches = 10,
                RangeStart = 1,
                RangeEnd = 5000,
                CumulativeObservationCount = 1500,
                Elapsed = TimeSpan.FromMinutes(2)
            });

            // Create a new tracker instance (simulating app restart)
            var tracker2 = new StandardizationProgressTracker(_progressFilePath);
            var resumed = await tracker2.LoadOrCreateAsync(connectionString, "parse", 1000);

            Assert.AreEqual(1, resumed.ResumeCount);
            Assert.AreEqual(5000, resumed.LastCompletedMaxId);
            Assert.AreEqual(1500, resumed.TotalObservations);
        }

        /**************************************************************/
        /// <summary>
        /// LoadOrCreateAsync with different connection string throws InvalidOperationException.
        /// </summary>
        [TestMethod]
        public async Task LoadOrCreate_WrongConnectionHash_ThrowsException()
        {
            var tracker = new StandardizationProgressTracker(_progressFilePath);

            // Create initial with one connection
            await tracker.LoadOrCreateAsync("Server=original", "parse", 1000);

            // Try to load with different connection
            var tracker2 = new StandardizationProgressTracker(_progressFilePath);
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await tracker2.LoadOrCreateAsync("Server=different", "parse", 1000);
            });
        }

        /**************************************************************/
        /// <summary>
        /// Concurrent trackers for the same path and connection serialize creation and one resume transaction.
        /// </summary>
        /// <returns>A task representing the concurrent file-boundary assertions.</returns>
        [TestMethod]
        public async Task LoadOrCreate_ConcurrentSamePathAndConnection_SerializesOneResume()
        {
            #region implementation

            var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstTask = loadAfterStartAsync(
                new StandardizationProgressTracker(_progressFilePath),
                startGate.Task,
                "Server=shared");
            var secondTask = loadAfterStartAsync(
                new StandardizationProgressTracker(_progressFilePath),
                startGate.Task,
                "Server=shared");

            startGate.SetResult();
            await Task.WhenAll(firstTask, secondTask);

            var persisted = await readProgressFileAsync(_progressFilePath);
            Assert.AreEqual(1, persisted.ResumeCount);
            Assert.AreEqual(0, Directory.GetFiles(_testDirectory, "*.tmp").Length);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Concurrent trackers for the same path but different connections preserve one deterministic owner.
        /// </summary>
        /// <returns>A task representing the competing connection assertions.</returns>
        [TestMethod]
        public async Task LoadOrCreate_ConcurrentSamePathDifferentConnections_PreservesWinningOwner()
        {
            #region implementation

            var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstTask = loadAfterStartAsync(
                new StandardizationProgressTracker(_progressFilePath),
                startGate.Task,
                "Server=first");
            var secondTask = loadAfterStartAsync(
                new StandardizationProgressTracker(_progressFilePath),
                startGate.Task,
                "Server=second");

            startGate.SetResult();
            try
            {
                await Task.WhenAll(firstTask, secondTask);
            }
            catch (InvalidOperationException)
            {
                // Exactly one connection must lose after the other atomically creates the shared progress file.
            }

            var tasks = new[] { firstTask, secondTask };
            Assert.AreEqual(1, tasks.Count(task => task.Status == TaskStatus.RanToCompletion));
            Assert.AreEqual(1, tasks.Count(task => task.IsFaulted));
            Assert.IsInstanceOfType(
                tasks.Single(task => task.IsFaulted).Exception?.GetBaseException(),
                typeof(InvalidOperationException));

            var winningProgress = tasks.Single(task => task.Status == TaskStatus.RanToCompletion).Result;
            var persisted = await readProgressFileAsync(_progressFilePath);
            Assert.AreEqual(winningProgress.ConnectionStringHash, persisted.ConnectionStringHash);
            Assert.AreEqual(0, persisted.ResumeCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Trackers using different paths and connections persist and resume independently.
        /// </summary>
        /// <returns>A task representing the path-isolation assertions.</returns>
        [TestMethod]
        public async Task LoadOrCreate_ConcurrentDifferentPathsAndConnections_RemainIndependent()
        {
            #region implementation

            var secondProgressPath = Path.Combine(_testDirectory, "second-progress.json");
            var startGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstTask = loadAfterStartAsync(
                new StandardizationProgressTracker(_progressFilePath),
                startGate.Task,
                "Server=first-independent");
            var secondTask = loadAfterStartAsync(
                new StandardizationProgressTracker(secondProgressPath),
                startGate.Task,
                "Server=second-independent");

            startGate.SetResult();
            await Task.WhenAll(firstTask, secondTask);

            var firstPersisted = await readProgressFileAsync(_progressFilePath);
            var secondPersisted = await readProgressFileAsync(secondProgressPath);
            Assert.AreNotEqual(firstPersisted.ConnectionStringHash, secondPersisted.ConnectionStringHash);
            Assert.AreEqual(0, firstPersisted.ResumeCount);
            Assert.AreEqual(0, secondPersisted.ResumeCount);

            var firstResumed = await new StandardizationProgressTracker(_progressFilePath)
                .LoadOrCreateAsync("Server=first-independent", "parse", 1000);
            var secondResumed = await new StandardizationProgressTracker(secondProgressPath)
                .LoadOrCreateAsync("Server=second-independent", "parse", 1000);
            Assert.AreEqual(1, firstResumed.ResumeCount);
            Assert.AreEqual(1, secondResumed.ResumeCount);

            #endregion
        }

        #endregion

        #region UpdateProgress Tests

        /**************************************************************/
        /// <summary>
        /// UpdateProgressAsync updates LastCompletedMaxId and TotalObservations.
        /// </summary>
        [TestMethod]
        public async Task UpdateProgress_UpdatesLastCompletedMaxId()
        {
            var tracker = new StandardizationProgressTracker(_progressFilePath);

            await tracker.LoadOrCreateAsync("Server=update-test", "parse", 1000);

            await tracker.UpdateProgressAsync(new TransformBatchProgress
            {
                BatchNumber = 3,
                TotalBatches = 10,
                RangeStart = 2001,
                RangeEnd = 3000,
                CumulativeObservationCount = 750,
                Elapsed = TimeSpan.FromSeconds(30)
            });

            var progress = tracker.GetProgressFile();
            Assert.IsNotNull(progress);
            Assert.AreEqual(3000, progress!.LastCompletedMaxId);
            Assert.AreEqual(750, progress.TotalObservations);
            Assert.AreEqual(3, progress.TotalBatchesCompleted);
        }

        #endregion

        #region GetResumeStartId Tests

        /**************************************************************/
        /// <summary>
        /// GetResumeStartId returns null when no progress file is loaded.
        /// </summary>
        [TestMethod]
        public void GetResumeStartId_NoFile_ReturnsNull()
        {
            var tracker = new StandardizationProgressTracker(_progressFilePath);
            Assert.IsNull(tracker.GetResumeStartId());
        }

        /**************************************************************/
        /// <summary>
        /// GetResumeStartId returns LastCompletedMaxId + 1.
        /// </summary>
        [TestMethod]
        public async Task GetResumeStartId_WithProgress_ReturnsNextId()
        {
            var tracker = new StandardizationProgressTracker(_progressFilePath);

            await tracker.LoadOrCreateAsync("Server=resume-test", "parse", 1000);

            await tracker.UpdateProgressAsync(new TransformBatchProgress
            {
                BatchNumber = 1,
                TotalBatches = 5,
                RangeStart = 1,
                RangeEnd = 1000,
                CumulativeObservationCount = 200,
                Elapsed = TimeSpan.FromSeconds(10)
            });

            Assert.AreEqual(1001, tracker.GetResumeStartId());
        }

        #endregion

        #region RecordInterruption Tests

        /**************************************************************/
        /// <summary>
        /// RecordInterruptionAsync saves the reason and elapsed time.
        /// </summary>
        [TestMethod]
        public async Task RecordInterruption_SavesReasonAndElapsed()
        {
            var tracker = new StandardizationProgressTracker(_progressFilePath);

            await tracker.LoadOrCreateAsync("Server=interrupt-test", "parse", 1000);

            await tracker.RecordInterruptionAsync("User cancellation", TimeSpan.FromMinutes(5));

            var progress = tracker.GetProgressFile();
            Assert.IsNotNull(progress);
            Assert.AreEqual("User cancellation", progress!.LastInterruptionReason);
            Assert.AreEqual(TimeSpan.FromMinutes(5), progress.TotalElapsedTime);
        }

        #endregion

        #region DeleteProgressFile Tests

        /**************************************************************/
        /// <summary>
        /// DeleteProgressFileAsync removes the file and clears internal state.
        /// </summary>
        [TestMethod]
        public async Task DeleteProgressFile_RemovesFile()
        {
            var tracker = new StandardizationProgressTracker(_progressFilePath);

            await tracker.LoadOrCreateAsync("Server=delete-test", "parse", 1000);
            Assert.IsTrue(tracker.ProgressFileExists());

            await tracker.DeleteProgressFileAsync();
            Assert.IsFalse(tracker.ProgressFileExists());
            Assert.IsNull(tracker.GetProgressFile());
        }

        #endregion
    }
}
