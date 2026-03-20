using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="TableParsingOrchestrator"/> (Stage 3 orchestrator).
    /// </summary>
    /// <remarks>
    /// Tests cover:
    /// - ParseSingleTableAsync with various routing outcomes
    /// - Skip filtering (SKIP category, null parser)
    /// - Error handling (parser exceptions logged, not thrown)
    /// - Router integration (correct parser selection)
    ///
    /// Uses Moq to mock Stage 1 and Stage 2 services — no database needed.
    /// The ParseSingleTableAsync path is tested since it doesn't require DbContext.
    /// </remarks>
    /// <seealso cref="TableParsingOrchestrator"/>
    /// <seealso cref="ITableParserRouter"/>
    [TestClass]
    public class TableParsingOrchestratorTests
    {
        #region Test Helpers

        /**************************************************************/
        /// <summary>
        /// Creates a minimal PK test table for orchestrator tests.
        /// </summary>
        private static ReconstructedTable createPkTestTable(int textTableId = 1)
        {
            #region implementation

            return new ReconstructedTable
            {
                TextTableID = textTableId,
                Caption = "Table 1: PK Parameters",
                DocumentGUID = Guid.NewGuid(),
                Title = "Test Drug",
                VersionNumber = 1,
                ParentSectionCode = "34090-1",
                LabelerName = "Test Lab",
                TotalColumnCount = 3,
                TotalRowCount = 2,
                HasExplicitHeader = true,
                HasInferredHeader = false,
                HasFooter = false,
                HasSocDividers = false,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 1,
                    ColumnCount = 3,
                    Columns = new List<HeaderColumn>
                    {
                        new() { ColumnIndex = 0, LeafHeaderText = "Dose", HeaderPath = new List<string> { "Dose" } },
                        new() { ColumnIndex = 1, LeafHeaderText = "Cmax (mcg/mL)", HeaderPath = new List<string> { "Cmax (mcg/mL)" } },
                        new() { ColumnIndex = 2, LeafHeaderText = "AUC (mcg·h/mL)", HeaderPath = new List<string> { "AUC (mcg·h/mL)" } }
                    }
                },
                Rows = new List<ReconstructedRow>
                {
                    new()
                    {
                        SequenceNumberTextTableRow = 2,
                        Classification = RowClassification.DataBody,
                        AbsoluteRowIndex = 1,
                        Cells = new List<ProcessedCell>
                        {
                            new() { SequenceNumber = 1, ResolvedColumnStart = 0, ResolvedColumnEnd = 1, CleanedText = "50 mg oral", CellType = "td" },
                            new() { SequenceNumber = 2, ResolvedColumnStart = 1, ResolvedColumnEnd = 2, CleanedText = "0.29 (35%)", CellType = "td" },
                            new() { SequenceNumber = 3, ResolvedColumnStart = 2, ResolvedColumnEnd = 3, CleanedText = "1.2 (28%)", CellType = "td" }
                        }
                    }
                }
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a table that should be skipped (patient info section).
        /// </summary>
        private static ReconstructedTable createSkipTable(int textTableId = 99)
        {
            #region implementation

            return new ReconstructedTable
            {
                TextTableID = textTableId,
                ParentSectionCode = "68498-5",
                TotalColumnCount = 2,
                TotalRowCount = 1,
                Header = new ResolvedHeader { HeaderRowCount = 1, ColumnCount = 2 },
                Rows = new List<ReconstructedRow>()
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the orchestrator with mocked dependencies for ParseSingleTableAsync tests.
        /// </summary>
        private static (TableParsingOrchestrator orchestrator, Mock<ITableReconstructionService> recon)
            createTestOrchestrator()
        {
            #region implementation

            var mockRecon = new Mock<ITableReconstructionService>();
            var mockCellContext = new Mock<ITableCellContextService>();
            var mockLogger = new Mock<ILogger<TableParsingOrchestrator>>();

            // Create real parsers and router
            var parsers = new List<ITableParser>
            {
                new PkTableParser(),
                new SimpleArmTableParser(),
                new MultilevelAeTableParser(),
                new AeWithSocTableParser(),
                new EfficacyMultilevelTableParser(),
                new BmdTableParser(),
                new TissueRatioTableParser(),
                new DosingTableParser()
            };
            var router = new TableParserRouter(parsers);

            // DbContext is null — ParseSingleTableAsync doesn't use it
            var orchestrator = new TableParsingOrchestrator(
                mockRecon.Object,
                mockCellContext.Object,
                router,
                null!,
                mockLogger.Object);

            return (orchestrator, mockRecon);

            #endregion
        }

        #endregion Test Helpers

        #region ParseSingleTableAsync Tests

        /**************************************************************/
        /// <summary>
        /// ParseSingleTableAsync routes PK table and returns observations.
        /// </summary>
        [TestMethod]
        public async Task ParseSingleTable_PkTable_ReturnsObservations()
        {
            var (orchestrator, mockRecon) = createTestOrchestrator();
            var pkTable = createPkTestTable();

            mockRecon.Setup(r => r.ReconstructTableAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pkTable);

            var results = await orchestrator.ParseSingleTableAsync(1);

            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results.All(r => r.TableCategory == "PK"));
        }

        /**************************************************************/
        /// <summary>
        /// ParseSingleTableAsync returns empty list when table is not found.
        /// </summary>
        [TestMethod]
        public async Task ParseSingleTable_NotFound_ReturnsEmpty()
        {
            var (orchestrator, mockRecon) = createTestOrchestrator();

            mockRecon.Setup(r => r.ReconstructTableAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ReconstructedTable?)null);

            var results = await orchestrator.ParseSingleTableAsync(999);

            Assert.AreEqual(0, results.Count);
        }

        /**************************************************************/
        /// <summary>
        /// ParseSingleTableAsync returns empty list when table is categorized as SKIP.
        /// </summary>
        [TestMethod]
        public async Task ParseSingleTable_SkipTable_ReturnsEmpty()
        {
            var (orchestrator, mockRecon) = createTestOrchestrator();
            var skipTable = createSkipTable();

            mockRecon.Setup(r => r.ReconstructTableAsync(99, It.IsAny<CancellationToken>()))
                .ReturnsAsync(skipTable);

            var results = await orchestrator.ParseSingleTableAsync(99);

            Assert.AreEqual(0, results.Count);
        }

        /**************************************************************/
        /// <summary>
        /// ParseSingleTableAsync correctly populates provenance fields.
        /// </summary>
        [TestMethod]
        public async Task ParseSingleTable_PopulatesProvenance()
        {
            var (orchestrator, mockRecon) = createTestOrchestrator();
            var pkTable = createPkTestTable();

            mockRecon.Setup(r => r.ReconstructTableAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pkTable);

            var results = await orchestrator.ParseSingleTableAsync(1);

            Assert.IsTrue(results.Count > 0);
            var first = results[0];
            Assert.AreEqual(1, first.TextTableID);
            Assert.AreEqual("Test Lab", first.LabelerName);
            Assert.AreEqual("34090-1", first.ParentSectionCode);
        }

        #endregion ParseSingleTableAsync Tests

        #region Router Integration Tests

        /**************************************************************/
        /// <summary>
        /// Verifies the full routing pipeline: section code → category → parser → observations.
        /// </summary>
        [TestMethod]
        public async Task RouterIntegration_AeTable_CorrectlyRouted()
        {
            var (orchestrator, mockRecon) = createTestOrchestrator();

            var aeTable = new ReconstructedTable
            {
                TextTableID = 5,
                ParentSectionCode = "34084-4",
                LabelerName = "Test Lab",
                Title = "Test Drug",
                TotalColumnCount = 3,
                TotalRowCount = 2,
                HasExplicitHeader = true,
                HasSocDividers = false,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 1,
                    ColumnCount = 3,
                    Columns = new List<HeaderColumn>
                    {
                        new() { ColumnIndex = 0, LeafHeaderText = "Adverse Reaction", HeaderPath = new List<string> { "Adverse Reaction" } },
                        new() { ColumnIndex = 1, LeafHeaderText = "Drug (N=100) n(%)", HeaderPath = new List<string> { "Drug (N=100) n(%)" } },
                        new() { ColumnIndex = 2, LeafHeaderText = "Placebo (N=100) n(%)", HeaderPath = new List<string> { "Placebo (N=100) n(%)" } }
                    }
                },
                Rows = new List<ReconstructedRow>
                {
                    new()
                    {
                        SequenceNumberTextTableRow = 2,
                        Classification = RowClassification.DataBody,
                        AbsoluteRowIndex = 1,
                        Cells = new List<ProcessedCell>
                        {
                            new() { SequenceNumber = 1, ResolvedColumnStart = 0, ResolvedColumnEnd = 1, CleanedText = "Nausea", CellType = "td" },
                            new() { SequenceNumber = 2, ResolvedColumnStart = 1, ResolvedColumnEnd = 2, CleanedText = "10 (10.0)", CellType = "td" },
                            new() { SequenceNumber = 3, ResolvedColumnStart = 2, ResolvedColumnEnd = 3, CleanedText = "5 (5.0)", CellType = "td" }
                        }
                    }
                }
            };

            mockRecon.Setup(r => r.ReconstructTableAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(aeTable);

            var results = await orchestrator.ParseSingleTableAsync(5);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.TableCategory == "ADVERSE_EVENT"));
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "Drug"));
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "Placebo"));
        }

        #endregion Router Integration Tests
    }
}
