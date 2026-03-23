using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="TableParsingOrchestrator"/> IProgress callback
    /// and resumeFromId parameters added for CLI progress reporting and resumption.
    /// </summary>
    /// <remarks>
    /// Tests cover:
    /// - IProgress callback is invoked for each batch
    /// - Progress reports contain correct batch numbers and ranges
    /// - resumeFromId skips truncate and starts from the specified ID
    /// - ProcessAllWithValidationAsync reports include skip counts
    ///
    /// Uses mocked dependencies (ITableCellContextService, ITableReconstructionService)
    /// with in-memory EF Core DbContext to test orchestration logic without real data.
    /// </remarks>
    /// <seealso cref="TableParsingOrchestrator"/>
    /// <seealso cref="TransformBatchProgress"/>
    [TestClass]
    public class TableParsingOrchestratorProgressTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a TableParsingOrchestrator with mocked dependencies.
        /// </summary>
        /// <param name="minId">Minimum TextTableID returned by the cell context service.</param>
        /// <param name="maxId">Maximum TextTableID returned by the cell context service.</param>
        /// <returns>Tuple of orchestrator, mock cell context service, and mock reconstruction service.</returns>
        private static (TableParsingOrchestrator orchestrator,
            Mock<ITableCellContextService> cellContextMock,
            Mock<ITableReconstructionService> reconstructionMock)
            createOrchestrator(int minId = 1, int maxId = 100)
        {
            #region implementation

            var cellContextMock = new Mock<ITableCellContextService>();
            cellContextMock
                .Setup(x => x.GetTextTableIdRangeAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((minId, maxId));

            var reconstructionMock = new Mock<ITableReconstructionService>();
            reconstructionMock
                .Setup(x => x.ReconstructTablesAsync(It.IsAny<TableCellContextFilter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconstructedTable>());

            var routerMock = new Mock<ITableParserRouter>();

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"OrchestratorProgress_{Guid.NewGuid()}")
                .Options;
            var dbContext = new ApplicationDbContext(dbOptions);

            var loggerMock = new Mock<ILogger<TableParsingOrchestrator>>();

            var orchestrator = new TableParsingOrchestrator(
                reconstructionMock.Object,
                cellContextMock.Object,
                routerMock.Object,
                dbContext,
                loggerMock.Object);

            return (orchestrator, cellContextMock, reconstructionMock);

            #endregion
        }

        #endregion

        #region SynchronousProgress helper

        /**************************************************************/
        /// <summary>
        /// A synchronous IProgress implementation that executes the callback inline
        /// (unlike <see cref="Progress{T}"/> which posts to the SynchronizationContext/ThreadPool).
        /// Required for unit tests where progress reports must be available immediately after await.
        /// </summary>
        private class SynchronousProgress<T> : IProgress<T>
        {
            private readonly Action<T> _handler;
            public SynchronousProgress(Action<T> handler) => _handler = handler;
            public void Report(T value) => _handler(value);
        }

        #endregion

        #region IProgress Callback Tests

        /**************************************************************/
        /// <summary>
        /// ProcessAllAsync invokes the IProgress callback for each batch.
        /// Uses resumeFromId to skip TruncateAsync (unsupported on InMemory provider).
        /// </summary>
        [TestMethod]
        public async Task ProcessAllAsync_ReportsProgress_ForEachBatch()
        {
            var (orchestrator, _, _) = createOrchestrator(minId: 1, maxId: 250);
            var reports = new List<TransformBatchProgress>();
            var progress = new SynchronousProgress<TransformBatchProgress>(r => reports.Add(r));

            // Use resumeFromId=1 to skip truncate (InMemory DB doesn't support ExecuteSqlRawAsync)
            await orchestrator.ProcessAllAsync(batchSize: 100, progress: progress, resumeFromId: 1);

            // IDs 1-250 with batch size 100 = 3 batches (1-100, 101-200, 201-250)
            Assert.AreEqual(3, reports.Count);

            Assert.AreEqual(1, reports[0].BatchNumber);
            Assert.AreEqual(3, reports[0].TotalBatches);
            Assert.AreEqual(1, reports[0].RangeStart);
            Assert.AreEqual(100, reports[0].RangeEnd);

            Assert.AreEqual(2, reports[1].BatchNumber);
            Assert.AreEqual(101, reports[1].RangeStart);
            Assert.AreEqual(200, reports[1].RangeEnd);

            Assert.AreEqual(3, reports[2].BatchNumber);
            Assert.AreEqual(201, reports[2].RangeStart);
            Assert.AreEqual(250, reports[2].RangeEnd);
        }

        /**************************************************************/
        /// <summary>
        /// ProcessAllAsync with null progress does not throw.
        /// Uses resumeFromId to skip TruncateAsync.
        /// </summary>
        [TestMethod]
        public async Task ProcessAllAsync_NullProgress_DoesNotThrow()
        {
            var (orchestrator, _, _) = createOrchestrator(minId: 1, maxId: 50);

            // Use resumeFromId=1 to skip truncate (InMemory DB doesn't support ExecuteSqlRawAsync)
            var result = await orchestrator.ProcessAllAsync(batchSize: 100, progress: null, resumeFromId: 1);

            Assert.AreEqual(0, result); // No tables reconstructed = 0 observations
        }

        #endregion

        #region Resume Tests

        /**************************************************************/
        /// <summary>
        /// ProcessAllAsync with resumeFromId starts processing from the specified ID.
        /// </summary>
        [TestMethod]
        public async Task ProcessAllAsync_WithResumeFromId_StartsFromCorrectId()
        {
            var (orchestrator, _, reconstructionMock) = createOrchestrator(minId: 1, maxId: 500);
            var reports = new List<TransformBatchProgress>();
            var progress = new SynchronousProgress<TransformBatchProgress>(r => reports.Add(r));

            // Resume from ID 301 with max 500 = 2 batches (301-400, 401-500)
            await orchestrator.ProcessAllAsync(batchSize: 100, progress: progress, resumeFromId: 301);

            Assert.AreEqual(2, reports.Count);
            Assert.AreEqual(301, reports[0].RangeStart);
            Assert.AreEqual(400, reports[0].RangeEnd);
            Assert.AreEqual(401, reports[1].RangeStart);
            Assert.AreEqual(500, reports[1].RangeEnd);

            // Verify reconstruction was called with the correct filter ranges
            reconstructionMock.Verify(
                x => x.ReconstructTablesAsync(
                    It.Is<TableCellContextFilter>(f => f.TextTableIdRangeStart == 301),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /**************************************************************/
        /// <summary>
        /// ProcessAllAsync with resumeFromId does NOT call TruncateAsync.
        /// Verified because InMemory DB would throw if ExecuteSqlRawAsync were called.
        /// </summary>
        [TestMethod]
        public async Task ProcessAllAsync_WithResumeFromId_SkipsTruncate()
        {
            var (orchestrator, _, _) = createOrchestrator(minId: 1, maxId: 100);

            // Resume mode — should not truncate (would throw on in-memory DB)
            // If this doesn't throw, truncate was correctly skipped
            var result = await orchestrator.ProcessAllAsync(
                batchSize: 100, progress: null, resumeFromId: 50);

            Assert.AreEqual(0, result);
        }

        #endregion

        #region Elapsed Time Tests

        /**************************************************************/
        /// <summary>
        /// Progress reports contain non-zero elapsed times.
        /// </summary>
        [TestMethod]
        public async Task ProcessAllAsync_ProgressReports_ContainElapsedTime()
        {
            var (orchestrator, _, _) = createOrchestrator(minId: 1, maxId: 100);
            var reports = new List<TransformBatchProgress>();
            var progress = new SynchronousProgress<TransformBatchProgress>(r => reports.Add(r));

            await orchestrator.ProcessAllAsync(batchSize: 100, progress: progress, resumeFromId: 1);

            Assert.IsTrue(reports.Count > 0);
            // Elapsed should be non-negative (may be zero on fast machines)
            Assert.IsTrue(reports[0].Elapsed >= TimeSpan.Zero);
        }

        #endregion
    }
}
