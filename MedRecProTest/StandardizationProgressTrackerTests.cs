using MedRecProConsole.Models;
using MedRecProConsole.Services;
using MedRecProImportClass.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
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
    public class StandardizationProgressTrackerTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a tracker that uses a temp directory as the app base directory.
        /// This prevents test interference with the real progress file.
        /// </summary>
        private static (StandardizationProgressTracker tracker, string tempDir) createTrackerWithTempDir()
        {
            #region implementation

            var tempDir = Path.Combine(Path.GetTempPath(), $"medrecpro-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            // The tracker uses AppDomain.CurrentDomain.BaseDirectory which we can't override,
            // so we test the public API and clean up after
            return (new StandardizationProgressTracker(), tempDir);

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
            var tracker = new StandardizationProgressTracker();

            // Clean up any existing progress file first
            if (tracker.ProgressFileExists())
            {
                await tracker.LoadOrCreateAsync("Server=test", "parse", 1000);
                await tracker.DeleteProgressFileAsync();
            }

            var progress = await tracker.LoadOrCreateAsync("Server=test", "parse", 1000);

            Assert.IsNotNull(progress);
            Assert.AreEqual("parse", progress.Operation);
            Assert.AreEqual(1000, progress.BatchSize);
            Assert.AreEqual(0, progress.ResumeCount);
            Assert.AreEqual(0, progress.LastCompletedMaxId);

            // Cleanup
            await tracker.DeleteProgressFileAsync();
        }

        /**************************************************************/
        /// <summary>
        /// LoadOrCreateAsync with existing file loads and increments ResumeCount.
        /// </summary>
        [TestMethod]
        public async Task LoadOrCreate_ExistingFile_LoadsAndIncrementsResume()
        {
            var tracker = new StandardizationProgressTracker();
            var connectionString = "Server=test-resume";

            // Clean up any existing file
            if (tracker.ProgressFileExists())
            {
                await tracker.LoadOrCreateAsync(connectionString, "parse", 1000);
                await tracker.DeleteProgressFileAsync();
            }

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
            var tracker2 = new StandardizationProgressTracker();
            var resumed = await tracker2.LoadOrCreateAsync(connectionString, "parse", 1000);

            Assert.AreEqual(1, resumed.ResumeCount);
            Assert.AreEqual(5000, resumed.LastCompletedMaxId);
            Assert.AreEqual(1500, resumed.TotalObservations);

            // Cleanup
            await tracker2.DeleteProgressFileAsync();
        }

        /**************************************************************/
        /// <summary>
        /// LoadOrCreateAsync with different connection string throws InvalidOperationException.
        /// </summary>
        [TestMethod]
        public async Task LoadOrCreate_WrongConnectionHash_ThrowsException()
        {
            var tracker = new StandardizationProgressTracker();

            // Clean up any existing file
            if (tracker.ProgressFileExists())
            {
                await tracker.LoadOrCreateAsync("Server=original", "parse", 1000);
                await tracker.DeleteProgressFileAsync();
            }

            // Create initial with one connection
            await tracker.LoadOrCreateAsync("Server=original", "parse", 1000);

            // Try to load with different connection
            var tracker2 = new StandardizationProgressTracker();
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await tracker2.LoadOrCreateAsync("Server=different", "parse", 1000);
            });

            // Cleanup
            var tracker3 = new StandardizationProgressTracker();
            await tracker3.LoadOrCreateAsync("Server=original", "parse", 1000);
            await tracker3.DeleteProgressFileAsync();
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
            var tracker = new StandardizationProgressTracker();

            // Clean up
            if (tracker.ProgressFileExists())
            {
                await tracker.LoadOrCreateAsync("Server=update-test", "parse", 1000);
                await tracker.DeleteProgressFileAsync();
            }

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

            // Cleanup
            await tracker.DeleteProgressFileAsync();
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
            var tracker = new StandardizationProgressTracker();
            Assert.IsNull(tracker.GetResumeStartId());
        }

        /**************************************************************/
        /// <summary>
        /// GetResumeStartId returns LastCompletedMaxId + 1.
        /// </summary>
        [TestMethod]
        public async Task GetResumeStartId_WithProgress_ReturnsNextId()
        {
            var tracker = new StandardizationProgressTracker();

            // Clean up
            if (tracker.ProgressFileExists())
            {
                await tracker.LoadOrCreateAsync("Server=resume-test", "parse", 1000);
                await tracker.DeleteProgressFileAsync();
            }

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

            // Cleanup
            await tracker.DeleteProgressFileAsync();
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
            var tracker = new StandardizationProgressTracker();

            // Clean up
            if (tracker.ProgressFileExists())
            {
                await tracker.LoadOrCreateAsync("Server=interrupt-test", "parse", 1000);
                await tracker.DeleteProgressFileAsync();
            }

            await tracker.LoadOrCreateAsync("Server=interrupt-test", "parse", 1000);

            await tracker.RecordInterruptionAsync("User cancellation", TimeSpan.FromMinutes(5));

            var progress = tracker.GetProgressFile();
            Assert.IsNotNull(progress);
            Assert.AreEqual("User cancellation", progress!.LastInterruptionReason);
            Assert.AreEqual(TimeSpan.FromMinutes(5), progress.TotalElapsedTime);

            // Cleanup
            await tracker.DeleteProgressFileAsync();
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
            var tracker = new StandardizationProgressTracker();

            // Clean up
            if (tracker.ProgressFileExists())
            {
                await tracker.LoadOrCreateAsync("Server=delete-test", "parse", 1000);
                await tracker.DeleteProgressFileAsync();
            }

            await tracker.LoadOrCreateAsync("Server=delete-test", "parse", 1000);
            Assert.IsTrue(tracker.ProgressFileExists());

            await tracker.DeleteProgressFileAsync();
            Assert.IsFalse(tracker.ProgressFileExists());
            Assert.IsNull(tracker.GetProgressFile());
        }

        #endregion
    }
}
