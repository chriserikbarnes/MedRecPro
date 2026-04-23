using MedRecProImportClass.Data;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Integration tests that verify <see cref="TableParsingOrchestrator"/> wires the
    /// optional <see cref="IBioequivalentLabelDedupService"/> correctly. Mocks the
    /// Stage 0 discovery service and the dedup service, then asserts that the batch
    /// loop sees the filtered GUID set (not the raw UNII-ordered set) and that the
    /// disable flag and null-service cases skip the call.
    /// </summary>
    /// <seealso cref="TableParsingOrchestrator"/>
    /// <seealso cref="IBioequivalentLabelDedupService"/>
    [TestClass]
    public class TableParsingOrchestratorDedupIntegrationTests
    {
        #region Helpers

        /**************************************************************/
        /// <summary>
        /// Synchronous <see cref="IProgress{T}"/> so unit tests can inspect reports
        /// without the ThreadPool-posting delay of <see cref="Progress{T}"/>.
        /// </summary>
        private sealed class SynchronousProgress<T> : IProgress<T>
        {
            private readonly Action<T> _handler;
            public SynchronousProgress(Action<T> handler) => _handler = handler;
            public void Report(T value) => _handler(value);
        }

        /**************************************************************/
        /// <summary>
        /// Builds a mocked orchestrator with configurable dedup service. The
        /// reconstruction service is mocked to return no tables, so ProcessAllAsync
        /// exits each batch with zero observations — we only care about WHICH guids
        /// it processes, not the downstream parse results.
        /// </summary>
        private static (TableParsingOrchestrator orchestrator,
            Mock<ITableReconstructionService> reconstructionMock,
            List<Guid> capturedProcessedGuids)
            createOrchestrator(
                List<Guid> discoveryGuids,
                IBioequivalentLabelDedupService? dedupService)
        {
            #region implementation

            var cellContextMock = new Mock<ITableCellContextService>();
            cellContextMock
                .Setup(x => x.GetDocumentGuidsOrderedByUniiAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(discoveryGuids);

            var capturedProcessedGuids = new List<Guid>();
            var reconstructionMock = new Mock<ITableReconstructionService>();
            reconstructionMock
                .Setup(x => x.ReconstructTablesAsync(
                    It.IsAny<TableCellContextFilter>(),
                    It.IsAny<CancellationToken>()))
                .Callback<TableCellContextFilter, CancellationToken>((filter, _) =>
                {
                    if (filter.DocumentGUIDs != null)
                    {
                        capturedProcessedGuids.AddRange(filter.DocumentGUIDs);
                    }
                })
                .ReturnsAsync(new List<ReconstructedTable>());

            var routerMock = new Mock<ITableParserRouter>();

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"DedupIntegration_{Guid.NewGuid()}")
                .Options;
            var dbContext = new ApplicationDbContext(dbOptions);

            var loggerMock = new Mock<ILogger<TableParsingOrchestrator>>();

            var orchestrator = new TableParsingOrchestrator(
                reconstructionMock.Object,
                cellContextMock.Object,
                routerMock.Object,
                dbContext,
                loggerMock.Object,
                bioequivalentDedup: dedupService);

            return (orchestrator, reconstructionMock, capturedProcessedGuids);

            #endregion
        }

        #endregion

        #region Tests

        /**************************************************************/
        /// <summary>
        /// When a dedup service is registered and the disable flag is false (default),
        /// the orchestrator must feed the filtered subset — not the raw discovery
        /// list — into the batch loop.
        /// </summary>
        [TestMethod]
        public async Task ProcessAllAsync_WithDedupService_FiltersBeforeBatching()
        {
            var allGuids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();
            var keptGuids = allGuids.Take(3).ToList();

            var dedupMock = new Mock<IBioequivalentLabelDedupService>();
            dedupMock
                .Setup(x => x.DeduplicateAsync(
                    It.IsAny<IReadOnlyList<Guid>>(),
                    It.IsAny<BioequivalentDedupOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BioequivalentDedupResult
                {
                    KeptDocumentGuids = keptGuids,
                    DroppedDocuments = Array.Empty<DroppedDocument>(),
                    GroupCount = 3,
                    NdaGroupCount = 0,
                    AndaGroupCount = 3
                });

            var (orchestrator, _, captured) = createOrchestrator(allGuids, dedupMock.Object);

            await orchestrator.ProcessAllAsync(batchSize: 100, resumeFromId: 1);

            // Dedup service was invoked exactly once with the raw 10-guid list.
            dedupMock.Verify(x => x.DeduplicateAsync(
                It.Is<IReadOnlyList<Guid>>(list => list.Count == 10),
                It.IsAny<BioequivalentDedupOptions?>(),
                It.IsAny<CancellationToken>()),
                Times.Once);

            // Only the 3 kept guids reached the reconstruction service.
            CollectionAssert.AreEquivalent(keptGuids, captured);
        }

        /**************************************************************/
        /// <summary>
        /// When the caller passes <c>disableBioequivalentDedup: true</c>, the dedup
        /// service must NOT be invoked and the full discovery list flows through.
        /// </summary>
        [TestMethod]
        public async Task ProcessAllAsync_WithDisableFlag_SkipsDedup()
        {
            var allGuids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();

            var dedupMock = new Mock<IBioequivalentLabelDedupService>();
            dedupMock
                .Setup(x => x.DeduplicateAsync(
                    It.IsAny<IReadOnlyList<Guid>>(),
                    It.IsAny<BioequivalentDedupOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BioequivalentDedupResult
                {
                    KeptDocumentGuids = Array.Empty<Guid>(),
                    DroppedDocuments = Array.Empty<DroppedDocument>()
                });

            var (orchestrator, _, captured) = createOrchestrator(allGuids, dedupMock.Object);

            await orchestrator.ProcessAllAsync(
                batchSize: 100, resumeFromId: 1,
                disableBioequivalentDedup: true);

            dedupMock.Verify(x => x.DeduplicateAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<BioequivalentDedupOptions?>(),
                It.IsAny<CancellationToken>()),
                Times.Never);

            CollectionAssert.AreEquivalent(allGuids, captured);
        }

        /**************************************************************/
        /// <summary>
        /// Backward compatibility: when no dedup service is registered (the DI
        /// resolution returns null), the orchestrator continues to work as it did
        /// before this feature was introduced.
        /// </summary>
        [TestMethod]
        public async Task ProcessAllAsync_WithoutDedupService_SkipsDedup()
        {
            var allGuids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

            var (orchestrator, _, captured) = createOrchestrator(allGuids, dedupService: null);

            await orchestrator.ProcessAllAsync(batchSize: 100, resumeFromId: 1);

            CollectionAssert.AreEquivalent(allGuids, captured);
        }

        /**************************************************************/
        /// <summary>
        /// Dedup preserves walk order. After filtering, the kept guids must appear in
        /// the same relative order as in the original discovery list so the ML
        /// anomaly-model training accumulator keeps UNII-locality.
        /// </summary>
        [TestMethod]
        public async Task ProcessAllAsync_PreservesUniiOrderingAfterDedup()
        {
            var allGuids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
            // Keep guids 0, 2, 4 — must stay in that relative order.
            var keptGuids = new List<Guid> { allGuids[0], allGuids[2], allGuids[4] };

            var dedupMock = new Mock<IBioequivalentLabelDedupService>();
            dedupMock
                .Setup(x => x.DeduplicateAsync(
                    It.IsAny<IReadOnlyList<Guid>>(),
                    It.IsAny<BioequivalentDedupOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new BioequivalentDedupResult
                {
                    KeptDocumentGuids = keptGuids,
                    DroppedDocuments = Array.Empty<DroppedDocument>()
                });

            var (orchestrator, _, captured) = createOrchestrator(allGuids, dedupMock.Object);

            // Batch size 1 forces 3 separate batches so ordering is observable.
            await orchestrator.ProcessAllAsync(batchSize: 1, resumeFromId: 1);

            Assert.AreEqual(3, captured.Count);
            Assert.AreEqual(keptGuids[0], captured[0]);
            Assert.AreEqual(keptGuids[1], captured[1]);
            Assert.AreEqual(keptGuids[2], captured[2]);
        }

        #endregion
    }
}
