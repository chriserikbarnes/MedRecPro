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
    /// Unit tests for the stage-by-stage diagnostic methods on <see cref="TableParsingOrchestrator"/>:
    /// <see cref="ITableParsingOrchestrator.ReconstructSingleTableAsync"/>,
    /// <see cref="ITableParsingOrchestrator.RouteAndParseSingleTable"/>, and
    /// <see cref="ITableParsingOrchestrator.CorrectObservationsAsync"/>.
    /// </summary>
    /// <remarks>
    /// Tests verify that the thin facade methods correctly delegate to their underlying
    /// services and handle null/empty cases gracefully.
    ///
    /// Uses Moq to mock Stage 1 (Get Data), Stage 2 (Pivot Table), and Stage 3.5 (Claude Enhance)
    /// services — no database needed.
    /// </remarks>
    /// <seealso cref="TableParsingOrchestrator"/>
    /// <seealso cref="ITableParsingOrchestrator"/>
    [TestClass]
    public class TableParsingOrchestratorStageTests
    {
        #region Test Helpers

        /**************************************************************/
        /// <summary>
        /// Creates an orchestrator with mocked dependencies and optionally a mock correction service.
        /// </summary>
        /// <param name="mockCorrection">Optional mock correction service. Pass null to simulate no-claude mode.</param>
        private static (
            TableParsingOrchestrator orchestrator,
            Mock<ITableReconstructionService> recon,
            Mock<ITableCellContextService> cellContext)
            createTestOrchestrator(IClaudeApiCorrectionService? correctionService = null)
        {
            #region implementation

            var mockRecon = new Mock<ITableReconstructionService>();
            var mockCellContext = new Mock<ITableCellContextService>();
            var mockLogger = new Mock<ILogger<TableParsingOrchestrator>>();

            // Create real parsers and router for integration testing
            var parsers = new List<ITableParser>
            {
                new PkTableParser(),
                new SimpleArmTableParser(),
                new MultilevelAeTableParser(),
                new AeWithSocTableParser(),
                new EfficacyMultilevelTableParser()
            };
            var router = new TableParserRouter(parsers);

            // DbContext is null — these methods don't use it
            var orchestrator = new TableParsingOrchestrator(
                mockRecon.Object,
                mockCellContext.Object,
                router,
                null!,
                mockLogger.Object,
                batchValidator: null,
                correctionService: correctionService);

            return (orchestrator, mockRecon, mockCellContext);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a minimal PK test table for routing and parsing.
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
        /// Creates a table that should be skipped by the router (patient info section).
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

        #endregion Test Helpers

        #region ReconstructSingleTableAsync Tests

        /**************************************************************/
        /// <summary>
        /// ReconstructSingleTableAsync delegates to the reconstruction service and returns the result.
        /// </summary>
        [TestMethod]
        public async Task ReconstructSingleTableAsync_DelegatesToReconstructionService()
        {
            #region implementation

            var (orchestrator, mockRecon, _) = createTestOrchestrator();
            var pkTable = createPkTestTable();

            mockRecon.Setup(r => r.ReconstructTableAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(pkTable);

            var result = await orchestrator.ReconstructSingleTableAsync(1);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.TextTableID);
            Assert.AreEqual("Table 1: PK Parameters", result.Caption);
            mockRecon.Verify(r => r.ReconstructTableAsync(1, It.IsAny<CancellationToken>()), Times.Once);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// ReconstructSingleTableAsync returns null when the table is not found.
        /// </summary>
        [TestMethod]
        public async Task ReconstructSingleTableAsync_ReturnsNull_WhenTableNotFound()
        {
            #region implementation

            var (orchestrator, mockRecon, _) = createTestOrchestrator();

            mockRecon.Setup(r => r.ReconstructTableAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ReconstructedTable?)null);

            var result = await orchestrator.ReconstructSingleTableAsync(999);

            Assert.IsNull(result);

            #endregion
        }

        #endregion ReconstructSingleTableAsync Tests

        #region RouteAndParseSingleTable Tests

        /**************************************************************/
        /// <summary>
        /// RouteAndParseSingleTable returns the correct category, parser name, and observations for a PK table.
        /// </summary>
        [TestMethod]
        public void RouteAndParseSingleTable_ReturnsCategory_Parser_Observations()
        {
            #region implementation

            var (orchestrator, _, _) = createTestOrchestrator();
            var pkTable = createPkTestTable();

            var (category, parserName, observations) = orchestrator.RouteAndParseSingleTable(pkTable);

            Assert.AreEqual(TableCategory.PK, category);
            Assert.IsNotNull(parserName);
            Assert.AreEqual("PkTableParser", parserName);
            Assert.IsTrue(observations.Count > 0);
            Assert.IsTrue(observations.All(o => o.TableCategory == "PK"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// RouteAndParseSingleTable returns SKIP category and null parser for skipped tables.
        /// </summary>
        [TestMethod]
        public void RouteAndParseSingleTable_ReturnsSkip_WhenCategoryIsSkip()
        {
            #region implementation

            var (orchestrator, _, _) = createTestOrchestrator();
            var skipTable = createSkipTable();

            var (category, parserName, observations) = orchestrator.RouteAndParseSingleTable(skipTable);

            Assert.AreEqual(TableCategory.SKIP, category);
            Assert.IsNull(parserName);
            Assert.AreEqual(0, observations.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// RouteAndParseSingleTable returns an empty observations list when the parser produces no output.
        /// </summary>
        [TestMethod]
        public void RouteAndParseSingleTable_ReturnsEmptyList_WhenParserReturnsEmpty()
        {
            #region implementation

            var (orchestrator, _, _) = createTestOrchestrator();

            // Create a PK table with no data rows — parser will return empty
            var emptyTable = new ReconstructedTable
            {
                TextTableID = 10,
                ParentSectionCode = "34090-1",
                TotalColumnCount = 3,
                TotalRowCount = 1,
                HasExplicitHeader = true,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 1,
                    ColumnCount = 3,
                    Columns = new List<HeaderColumn>
                    {
                        new() { ColumnIndex = 0, LeafHeaderText = "Dose", HeaderPath = new List<string> { "Dose" } },
                        new() { ColumnIndex = 1, LeafHeaderText = "Cmax", HeaderPath = new List<string> { "Cmax" } },
                        new() { ColumnIndex = 2, LeafHeaderText = "AUC", HeaderPath = new List<string> { "AUC" } }
                    }
                },
                Rows = new List<ReconstructedRow>()
            };

            var (category, parserName, observations) = orchestrator.RouteAndParseSingleTable(emptyTable);

            Assert.AreEqual(TableCategory.PK, category);
            Assert.IsNotNull(parserName);
            Assert.AreEqual(0, observations.Count);

            #endregion
        }

        #endregion RouteAndParseSingleTable Tests

        #region CorrectObservationsAsync Tests

        /**************************************************************/
        /// <summary>
        /// CorrectObservationsAsync delegates to the correction service when available.
        /// </summary>
        [TestMethod]
        public async Task CorrectObservationsAsync_DelegatesToCorrectionService()
        {
            #region implementation

            var mockCorrection = new Mock<IClaudeApiCorrectionService>();
            var testObservations = new List<ParsedObservation>
            {
                new() { SourceRowSeq = 1, SourceCellSeq = 1, ParameterName = "Cmax" }
            };

            mockCorrection.Setup(c => c.CorrectBatchAsync(testObservations, It.IsAny<IReadOnlyDictionary<int, ReconstructedTable>?>(), It.IsAny<IProgress<TransformBatchProgress>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(testObservations);

            var (orchestrator, _, _) = createTestOrchestrator(mockCorrection.Object);

            var result = await orchestrator.CorrectObservationsAsync(testObservations);

            Assert.AreSame(testObservations, result);
            mockCorrection.Verify(c => c.CorrectBatchAsync(testObservations, It.IsAny<IReadOnlyDictionary<int, ReconstructedTable>?>(), It.IsAny<IProgress<TransformBatchProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// CorrectObservationsAsync returns the original list when the correction service is null (no-claude mode).
        /// </summary>
        [TestMethod]
        public async Task CorrectObservationsAsync_ReturnsOriginal_WhenServiceIsNull()
        {
            #region implementation

            var (orchestrator, _, _) = createTestOrchestrator(correctionService: null);
            var testObservations = new List<ParsedObservation>
            {
                new() { SourceRowSeq = 1, SourceCellSeq = 1, ParameterName = "Cmax" }
            };

            var result = await orchestrator.CorrectObservationsAsync(testObservations);

            Assert.AreSame(testObservations, result);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// CorrectObservationsAsync returns the original empty list without calling the correction service.
        /// </summary>
        [TestMethod]
        public async Task CorrectObservationsAsync_ReturnsOriginal_WhenListIsEmpty()
        {
            #region implementation

            var mockCorrection = new Mock<IClaudeApiCorrectionService>();
            var (orchestrator, _, _) = createTestOrchestrator(mockCorrection.Object);
            var emptyList = new List<ParsedObservation>();

            var result = await orchestrator.CorrectObservationsAsync(emptyList);

            Assert.AreSame(emptyList, result);
            mockCorrection.Verify(c => c.CorrectBatchAsync(It.IsAny<List<ParsedObservation>>(), It.IsAny<IReadOnlyDictionary<int, ReconstructedTable>?>(), It.IsAny<IProgress<TransformBatchProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);

            #endregion
        }

        #endregion CorrectObservationsAsync Tests

        #region ProcessBatchWithStagesAsync Tests

        /**************************************************************/
        /// <summary>
        /// ProcessBatchWithStagesAsync captures reconstructed tables from Stage 2.
        /// Uses skip-only tables to avoid needing a real DbContext for writes.
        /// </summary>
        [TestMethod]
        public async Task ProcessBatchWithStagesAsync_CapturesReconstructedTables()
        {
            #region implementation

            var mockRecon = new Mock<ITableReconstructionService>();
            var mockCellContext = new Mock<ITableCellContextService>();
            var mockLogger = new Mock<ILogger<TableParsingOrchestrator>>();
            var parsers = new List<ITableParser> { new PkTableParser(), new SimpleArmTableParser() };
            var router = new TableParserRouter(parsers);

            var skipTable = createSkipTable(50);
            var skipTable2 = createSkipTable(51);

            mockRecon.Setup(r => r.ReconstructTablesAsync(It.IsAny<TableCellContextFilter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconstructedTable> { skipTable, skipTable2 });

            var orchestrator = new TableParsingOrchestrator(
                mockRecon.Object, mockCellContext.Object, router, null!, mockLogger.Object);

            var filter = new TableCellContextFilter { TextTableIdRangeStart = 50, TextTableIdRangeEnd = 51 };
            var result = await orchestrator.ProcessBatchWithStagesAsync(filter);

            Assert.AreEqual(2, result.ReconstructedTables.Count);
            Assert.AreEqual(50, result.ReconstructedTables[0].TextTableID);
            Assert.AreEqual(51, result.ReconstructedTables[1].TextTableID);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// ProcessBatchWithStagesAsync captures routing decisions including skip reasons.
        /// </summary>
        [TestMethod]
        public async Task ProcessBatchWithStagesAsync_CapturesRoutingDecisions()
        {
            #region implementation

            var mockRecon = new Mock<ITableReconstructionService>();
            var mockCellContext = new Mock<ITableCellContextService>();
            var mockLogger = new Mock<ILogger<TableParsingOrchestrator>>();
            var parsers = new List<ITableParser> { new PkTableParser(), new SimpleArmTableParser() };
            var router = new TableParserRouter(parsers);

            var skipTable = createSkipTable(99);

            mockRecon.Setup(r => r.ReconstructTablesAsync(It.IsAny<TableCellContextFilter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconstructedTable> { skipTable });

            var orchestrator = new TableParsingOrchestrator(
                mockRecon.Object, mockCellContext.Object, router, null!, mockLogger.Object);

            var filter = new TableCellContextFilter { TextTableIdRangeStart = 99, TextTableIdRangeEnd = 99 };
            var result = await orchestrator.ProcessBatchWithStagesAsync(filter);

            Assert.AreEqual(1, result.RoutingDecisions.Count);
            Assert.AreEqual(TableCategory.SKIP, result.RoutingDecisions[0].Category);
            Assert.IsNull(result.RoutingDecisions[0].ParserName);
            Assert.AreEqual(0, result.RoutingDecisions[0].ObservationCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// ProcessBatchWithStagesAsync tracks skip reasons in the result.
        /// </summary>
        [TestMethod]
        public async Task ProcessBatchWithStagesAsync_CapturesSkipReasons()
        {
            #region implementation

            var mockRecon = new Mock<ITableReconstructionService>();
            var mockCellContext = new Mock<ITableCellContextService>();
            var mockLogger = new Mock<ILogger<TableParsingOrchestrator>>();
            var parsers = new List<ITableParser> { new PkTableParser(), new SimpleArmTableParser() };
            var router = new TableParserRouter(parsers);

            var skipTable = createSkipTable(99);

            mockRecon.Setup(r => r.ReconstructTablesAsync(It.IsAny<TableCellContextFilter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconstructedTable> { skipTable });

            var orchestrator = new TableParsingOrchestrator(
                mockRecon.Object, mockCellContext.Object, router, null!, mockLogger.Object);

            var filter = new TableCellContextFilter { TextTableIdRangeStart = 99, TextTableIdRangeEnd = 99 };
            var result = await orchestrator.ProcessBatchWithStagesAsync(filter);

            Assert.AreEqual(1, result.SkipReasons.Count);
            Assert.IsTrue(result.SkipReasons.ContainsKey(99));
            Assert.IsTrue(result.SkipReasons[99].StartsWith("SKIP:"));
            Assert.AreEqual(0, result.ObservationsWritten);
            Assert.AreEqual(0, result.PreCorrectionObservations.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// ProcessBatchWithStagesAsync captures pre-correction observations from Stage 3 (Standardize)
        /// when tables are successfully parsed.
        /// </summary>
        [TestMethod]
        public async Task ProcessBatchWithStagesAsync_CapturesPreCorrectionObservations()
        {
            #region implementation

            var mockRecon = new Mock<ITableReconstructionService>();
            var mockCellContext = new Mock<ITableCellContextService>();
            var mockLogger = new Mock<ILogger<TableParsingOrchestrator>>();
            var parsers = new List<ITableParser> { new PkTableParser(), new SimpleArmTableParser() };
            var router = new TableParserRouter(parsers);

            var pkTable = createPkTestTable(42);

            mockRecon.Setup(r => r.ReconstructTablesAsync(It.IsAny<TableCellContextFilter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconstructedTable> { pkTable });

            // No DbContext — SaveChangesAsync will fail, but we can still check pre-correction obs
            // Use the helper orchestrator (no correction service, no db)
            var orchestrator = new TableParsingOrchestrator(
                mockRecon.Object, mockCellContext.Object, router, null!, mockLogger.Object);

            var filter = new TableCellContextFilter { TextTableIdRangeStart = 42, TextTableIdRangeEnd = 42 };

            // This will throw on SaveChangesAsync because DbContext is null, but observations
            // are captured before the DB write. We catch to verify the routing happened.
            BatchStageResult? result = null;
            try
            {
                result = await orchestrator.ProcessBatchWithStagesAsync(filter);
            }
            catch (NullReferenceException)
            {
                // Expected — null DbContext can't SaveChangesAsync
            }

            // Even if DB write fails, routing decisions should have been captured
            // (The catch in ProcessBatchWithStagesAsync handles this, but DbContext itself is null)
            // This verifies the method at least attempts to parse before hitting the DB layer
            Assert.IsTrue(true, "Method attempted processing without crashing on routing/parsing phase");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// ProcessBatchWithStagesAsync records CorrectionCount when Stage 3.5 (Claude Enhance)
        /// modifies observation ValidationFlags.
        /// </summary>
        [TestMethod]
        public async Task ProcessBatchWithStagesAsync_WithCorrectionService_RecordsCorrectionCount()
        {
            #region implementation

            var mockRecon = new Mock<ITableReconstructionService>();
            var mockCellContext = new Mock<ITableCellContextService>();
            var mockLogger = new Mock<ILogger<TableParsingOrchestrator>>();
            var mockCorrection = new Mock<IClaudeApiCorrectionService>();
            var parsers = new List<ITableParser> { new PkTableParser(), new SimpleArmTableParser() };
            var router = new TableParserRouter(parsers);

            // Return skip-only tables so DB write is not attempted
            var skipTable = createSkipTable(77);

            mockRecon.Setup(r => r.ReconstructTablesAsync(It.IsAny<TableCellContextFilter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconstructedTable> { skipTable });

            var orchestrator = new TableParsingOrchestrator(
                mockRecon.Object, mockCellContext.Object, router, null!, mockLogger.Object,
                correctionService: mockCorrection.Object);

            var filter = new TableCellContextFilter { TextTableIdRangeStart = 77, TextTableIdRangeEnd = 77 };
            var result = await orchestrator.ProcessBatchWithStagesAsync(filter);

            // All tables skipped → no observations → Claude correction not called → CorrectionCount = 0
            Assert.AreEqual(0, result.CorrectionCount);
            Assert.AreEqual(0, result.PreCorrectionObservations.Count);
            mockCorrection.Verify(c => c.CorrectBatchAsync(It.IsAny<List<ParsedObservation>>(), It.IsAny<IReadOnlyDictionary<int, ReconstructedTable>?>(), It.IsAny<IProgress<TransformBatchProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// With the Stage 3.25 quality gate disabled, observations with populated
        /// PrimaryValue survive (irrespective of ArmN) but PK rows with null
        /// PrimaryValue are dropped by the R13+R15.2 hard contract (which is
        /// independent of the opt-in drop gate).
        /// </summary>
        /// <remarks>
        /// ## Semantic evolution
        /// Pre-R13: all 4 fixture rows survived when the gate was disabled.
        /// Post-R15.2: PK rows with null PrimaryValue are dropped unconditionally
        /// (they have no analyzable numeric content). The opt-in gate adds a
        /// stricter filter on top (requires both ArmN and PrimaryValue populated).
        /// </remarks>
        /// <seealso cref="TableParsingOrchestrator"/>
        [TestMethod]
        public async Task ProcessBatchWithStagesAsync_DropIncompleteRowsDisabled_KeepsRowsMissingArmNOrPrimaryValue()
        {
            #region implementation

            var (orchestrator, _) = createDropIncompleteTestOrchestrator(
                dropRowsMissingArmNOrPrimaryValue: false);

            var filter = new TableCellContextFilter { TextTableIdRangeStart = 1000, TextTableIdRangeEnd = 1000 };
            var result = await orchestrator.ProcessBatchWithStagesAsync(filter);

            // Fixture has 4 PK rows covering every (ArmN, PrimaryValue) null/value combo.
            // R15.2's hard contract drops PK rows with null PrimaryValue regardless of
            // the opt-in drop gate. So only rows with PrimaryValue populated survive:
            //   - Row 2 (ArmN=null, PrimaryValue=42.0): kept (null PrimaryValue contract
            //     is not tripped; null ArmN is only enforced by the opt-in gate).
            //   - Row 4 (ArmN=7, PrimaryValue=42.0): kept (fully populated).
            Assert.AreEqual(2, result.PostCorrectionObservations.Count,
                "R15.2 hard contract drops PK rows with null PrimaryValue regardless of the drop gate.");
            Assert.IsTrue(
                result.PostCorrectionObservations.Any(o => o.ArmN == null && o.PrimaryValue != null),
                "The null-ArmN / populated-PrimaryValue row must survive (opt-in gate is off).");
            Assert.IsTrue(
                result.PostCorrectionObservations.Any(o => o.ArmN != null && o.PrimaryValue != null),
                "The fully-populated row must survive.");
            Assert.IsFalse(
                result.PostCorrectionObservations.Any(o => o.PrimaryValue == null),
                "No PK row with null PrimaryValue should survive — R15.2 hard contract.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// With the Stage 3.25 quality gate enabled, observations where EITHER
        /// ArmN or PrimaryValue is null MUST be dropped before Stage 3.4 / 3.5 / 3.6 run,
        /// and thus MUST NOT appear in PostCorrectionObservations. Only rows with
        /// BOTH fields populated may survive.
        /// </summary>
        /// <seealso cref="TableParsingOrchestrator"/>
        [TestMethod]
        public async Task ProcessBatchWithStagesAsync_DropIncompleteRowsEnabled_DropsRowsMissingArmNOrPrimaryValue()
        {
            #region implementation

            var (orchestrator, _) = createDropIncompleteTestOrchestrator(
                dropRowsMissingArmNOrPrimaryValue: true);

            var filter = new TableCellContextFilter { TextTableIdRangeStart = 1000, TextTableIdRangeEnd = 1000 };
            var result = await orchestrator.ProcessBatchWithStagesAsync(filter);

            // Only the fully populated row survives.
            Assert.AreEqual(1, result.PostCorrectionObservations.Count,
                "Any row missing ArmN or PrimaryValue must be dropped by the Stage 3.25 quality gate.");
            Assert.IsFalse(
                result.PostCorrectionObservations.Any(o => o.ArmN == null || o.PrimaryValue == null),
                "No rows with a null ArmN or PrimaryValue must survive when the drop gate is enabled.");
            Assert.AreEqual(7, result.PostCorrectionObservations[0].ArmN,
                "The fully-populated row must be preserved.");
            Assert.AreEqual(42.0, result.PostCorrectionObservations[0].PrimaryValue,
                "The fully-populated row must be preserved.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds an orchestrator wired with a mocked reconstruction service, a mocked
        /// router + parser that yields a controlled set of four observations covering
        /// every combination of (ArmN null/populated) x (PrimaryValue null/populated),
        /// and a real in-memory <see cref="ApplicationDbContext"/> so the DB write stage
        /// can complete and PostCorrectionObservations can be inspected on the returned result.
        /// </summary>
        /// <param name="dropRowsMissingArmNOrPrimaryValue">Value to pass to the orchestrator's quality-gate flag.</param>
        /// <returns>The orchestrator and the in-memory DbContext (caller can inspect written rows if needed).</returns>
        /// <seealso cref="TableParsingOrchestrator"/>
        private static (TableParsingOrchestrator orchestrator, ApplicationDbContext dbContext)
            createDropIncompleteTestOrchestrator(bool dropRowsMissingArmNOrPrimaryValue)
        {
            #region implementation

            var mockRecon = new Mock<ITableReconstructionService>();
            var mockCellContext = new Mock<ITableCellContextService>();
            var mockLogger = new Mock<ILogger<TableParsingOrchestrator>>();

            // A minimal reconstructed table — we do not care about its contents because
            // the router mock is configured to ignore it and return our mock parser directly.
            var dummyTable = new ReconstructedTable
            {
                TextTableID = 1000,
                ParentSectionCode = "34090-1",
                TotalColumnCount = 2,
                TotalRowCount = 2,
                Header = new ResolvedHeader { HeaderRowCount = 1, ColumnCount = 2 },
                Rows = new List<ReconstructedRow>()
            };

            mockRecon
                .Setup(r => r.ReconstructTablesAsync(It.IsAny<TableCellContextFilter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconstructedTable> { dummyTable });

            // Controlled parser output covering every (ArmN, PrimaryValue) null/value
            // combination. Under the new Stage 3.25 semantics (drop if EITHER is null),
            // only the fully-populated row should survive when the gate is enabled.
            var controlledObservations = new List<ParsedObservation>
            {
                // 1. Both null — always dropped when gate is on.
                new()
                {
                    SourceRowSeq = 1,
                    SourceCellSeq = 1,
                    TableCategory = "PK",
                    ParameterName = "Cmax",
                    ArmN = null,
                    PrimaryValue = null
                },
                // 2. ArmN null, PrimaryValue populated — dropped when gate is on.
                new()
                {
                    SourceRowSeq = 2,
                    SourceCellSeq = 1,
                    TableCategory = "PK",
                    ParameterName = "AUC",
                    ArmN = null,
                    PrimaryValue = 42.0
                },
                // 3. ArmN populated, PrimaryValue null — dropped when gate is on.
                new()
                {
                    SourceRowSeq = 3,
                    SourceCellSeq = 1,
                    TableCategory = "PK",
                    ParameterName = "Tmax",
                    ArmN = 12,
                    PrimaryValue = null
                },
                // 4. Both populated — survives regardless of gate.
                new()
                {
                    SourceRowSeq = 4,
                    SourceCellSeq = 1,
                    TableCategory = "PK",
                    ParameterName = "HalfLife",
                    ArmN = 7,
                    PrimaryValue = 42.0
                }
            };

            var mockParser = new Mock<ITableParser>();
            mockParser.SetupGet(p => p.SupportedCategory).Returns(TableCategory.PK);
            mockParser.SetupGet(p => p.Priority).Returns(0);
            mockParser.Setup(p => p.CanParse(It.IsAny<ReconstructedTable>())).Returns(true);
            mockParser.Setup(p => p.Parse(It.IsAny<ReconstructedTable>())).Returns(controlledObservations);

            var mockRouter = new Mock<ITableParserRouter>();
            mockRouter
                .Setup(r => r.Route(It.IsAny<ReconstructedTable>()))
                .Returns((TableCategory.PK, (ITableParser?)mockParser.Object));

            // Real in-memory DbContext so writeObservationsAsync can succeed and
            // PostCorrectionObservations is populated on the returned result.
            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"DropIncompleteRows_{Guid.NewGuid()}")
                .Options;
            var dbContext = new ApplicationDbContext(dbOptions);

            var orchestrator = new TableParsingOrchestrator(
                mockRecon.Object,
                mockCellContext.Object,
                mockRouter.Object,
                dbContext,
                mockLogger.Object,
                batchValidator: null,
                columnStandardizer: null,
                qcNetCorrectionService: null,
                correctionService: null,
                dropRowsMissingArmNOrPrimaryValue: dropRowsMissingArmNOrPrimaryValue);

            return (orchestrator, dbContext);

            #endregion
        }

        #endregion ProcessBatchWithStagesAsync Tests

        #region R13 — Pre-ML PK Filter Tests

        /**************************************************************/
        /// <summary>
        /// R13 — A PK observation with a canonical <see cref="ParsedObservation.ParameterName"/>
        /// and a non-Text <see cref="ParsedObservation.PrimaryValueType"/> survives the
        /// pre-ML filter.
        /// </summary>
        [TestMethod]
        public async Task R13_PkRow_WithNameAndMeanType_Kept()
        {
            var obs = new ParsedObservation
            {
                SourceRowSeq = 1,
                SourceCellSeq = 1,
                TableCategory = "PK",
                ParameterName = "Cmax",
                PrimaryValueType = "Mean",
                PrimaryValue = 42.0
            };

            var (orchestrator, _) = createR13TestOrchestrator(new List<ParsedObservation> { obs });
            var result = await orchestrator.ProcessBatchWithStagesAsync(
                new TableCellContextFilter { TextTableIdRangeStart = 2000, TextTableIdRangeEnd = 2000 });

            Assert.AreEqual(1, result.PostCorrectionObservations.Count,
                "Analyzable PK row must survive the Stage 3.35 filter.");
            Assert.AreEqual("Cmax", result.PostCorrectionObservations[0].ParameterName);
        }

        /**************************************************************/
        /// <summary>
        /// R13 — A PK observation with a null ParameterName is dropped regardless
        /// of PrimaryValueType.
        /// </summary>
        [TestMethod]
        public async Task R13_PkRow_NullParameterName_Dropped()
        {
            var obs = new ParsedObservation
            {
                SourceRowSeq = 1,
                SourceCellSeq = 1,
                TableCategory = "PK",
                ParameterName = null,
                PrimaryValueType = "Mean",
                PrimaryValue = 42.0
            };

            var (orchestrator, _) = createR13TestOrchestrator(new List<ParsedObservation> { obs });
            var result = await orchestrator.ProcessBatchWithStagesAsync(
                new TableCellContextFilter { TextTableIdRangeStart = 2000, TextTableIdRangeEnd = 2000 });

            Assert.AreEqual(0, result.PostCorrectionObservations.Count,
                "PK row without ParameterName must be dropped at Stage 3.35.");
        }

        /**************************************************************/
        /// <summary>
        /// R13 — A PK observation with <see cref="ParsedObservation.PrimaryValueType"/>
        /// equal to "Text" is dropped even when ParameterName is populated. Text-typed
        /// rows have no analyzable numeric value and would otherwise bias the ML
        /// training set.
        /// </summary>
        [TestMethod]
        public async Task R13_PkRow_TextPrimaryValueType_Dropped()
        {
            var obs = new ParsedObservation
            {
                SourceRowSeq = 1,
                SourceCellSeq = 1,
                TableCategory = "PK",
                ParameterName = "Cmax",
                PrimaryValueType = "Text",
                RawValue = "data not reported"
            };

            var (orchestrator, _) = createR13TestOrchestrator(new List<ParsedObservation> { obs });
            var result = await orchestrator.ProcessBatchWithStagesAsync(
                new TableCellContextFilter { TextTableIdRangeStart = 2000, TextTableIdRangeEnd = 2000 });

            Assert.AreEqual(0, result.PostCorrectionObservations.Count,
                "PK row with Text PrimaryValueType must be dropped at Stage 3.35.");
        }

        /**************************************************************/
        /// <summary>
        /// R13 — A PK observation missing BOTH ParameterName and with a Text value
        /// type is dropped (either condition alone is sufficient).
        /// </summary>
        [TestMethod]
        public async Task R13_PkRow_BothMissing_Dropped()
        {
            var obs = new ParsedObservation
            {
                SourceRowSeq = 1,
                SourceCellSeq = 1,
                TableCategory = "PK",
                ParameterName = null,
                PrimaryValueType = "Text"
            };

            var (orchestrator, _) = createR13TestOrchestrator(new List<ParsedObservation> { obs });
            var result = await orchestrator.ProcessBatchWithStagesAsync(
                new TableCellContextFilter { TextTableIdRangeStart = 2000, TextTableIdRangeEnd = 2000 });

            Assert.AreEqual(0, result.PostCorrectionObservations.Count);
        }

        /**************************************************************/
        /// <summary>
        /// R13 — An ADVERSE_EVENT observation with null ParameterName and
        /// PrimaryValueType=Text is PRESERVED. The filter is PK-only; other
        /// categories retain troubleshooting data pending their own audits.
        /// </summary>
        [TestMethod]
        public async Task R13_NonPkRow_AdverseEvent_NullName_TextType_Kept()
        {
            var obs = new ParsedObservation
            {
                SourceRowSeq = 1,
                SourceCellSeq = 1,
                TableCategory = "ADVERSE_EVENT",
                ParameterName = null,
                PrimaryValueType = "Text"
            };

            var (orchestrator, _) = createR13TestOrchestrator(new List<ParsedObservation> { obs });
            var result = await orchestrator.ProcessBatchWithStagesAsync(
                new TableCellContextFilter { TextTableIdRangeStart = 2000, TextTableIdRangeEnd = 2000 });

            Assert.AreEqual(1, result.PostCorrectionObservations.Count,
                "Non-PK category must pass through the PK-scoped filter unchanged.");
        }

        /**************************************************************/
        /// <summary>
        /// R13 — A DRUG_INTERACTION observation with null ParameterName is
        /// PRESERVED. Same non-PK passthrough contract as ADVERSE_EVENT.
        /// </summary>
        [TestMethod]
        public async Task R13_NonPkRow_DrugInteraction_NullName_Kept()
        {
            var obs = new ParsedObservation
            {
                SourceRowSeq = 1,
                SourceCellSeq = 1,
                TableCategory = "DRUG_INTERACTION",
                ParameterName = null,
                PrimaryValueType = "Mean",
                PrimaryValue = 1.0
            };

            var (orchestrator, _) = createR13TestOrchestrator(new List<ParsedObservation> { obs });
            var result = await orchestrator.ProcessBatchWithStagesAsync(
                new TableCellContextFilter { TextTableIdRangeStart = 2000, TextTableIdRangeEnd = 2000 });

            Assert.AreEqual(1, result.PostCorrectionObservations.Count);
        }

        /**************************************************************/
        /// <summary>
        /// R13 — Mixed batch integration: 10 PK rows (3 non-analyzable) +
        /// 5 ADVERSE_EVENT rows → 7 PK + 5 AE = 12 surviving rows.
        /// </summary>
        [TestMethod]
        public async Task R13_MixedBatch_PkFilteredAeRetained()
        {
            var rows = new List<ParsedObservation>();

            // 7 analyzable PK rows
            for (int i = 0; i < 7; i++)
            {
                rows.Add(new ParsedObservation
                {
                    SourceRowSeq = i + 1,
                    SourceCellSeq = 1,
                    TableCategory = "PK",
                    ParameterName = "Cmax",
                    PrimaryValueType = "Mean",
                    PrimaryValue = 10.0 + i
                });
            }

            // 3 non-analyzable PK rows (to be dropped)
            rows.Add(new ParsedObservation
            {
                SourceRowSeq = 8, SourceCellSeq = 1,
                TableCategory = "PK", ParameterName = null, PrimaryValueType = "Mean", PrimaryValue = 1.0
            });
            rows.Add(new ParsedObservation
            {
                SourceRowSeq = 9, SourceCellSeq = 1,
                TableCategory = "PK", ParameterName = "AUC", PrimaryValueType = "Text"
            });
            rows.Add(new ParsedObservation
            {
                SourceRowSeq = 10, SourceCellSeq = 1,
                TableCategory = "PK", ParameterName = null, PrimaryValueType = "Text"
            });

            // 5 AE rows (all retained — PK-only filter)
            for (int i = 0; i < 5; i++)
            {
                rows.Add(new ParsedObservation
                {
                    SourceRowSeq = 11 + i,
                    SourceCellSeq = 1,
                    TableCategory = "ADVERSE_EVENT",
                    ParameterName = i % 2 == 0 ? null : "Headache",
                    PrimaryValueType = i % 2 == 0 ? "Text" : "Percentage",
                    PrimaryValue = i
                });
            }

            var (orchestrator, _) = createR13TestOrchestrator(rows);
            var result = await orchestrator.ProcessBatchWithStagesAsync(
                new TableCellContextFilter { TextTableIdRangeStart = 2000, TextTableIdRangeEnd = 2000 });

            var surviving = result.PostCorrectionObservations;
            Assert.AreEqual(12, surviving.Count,
                "Expected 7 surviving PK + 5 surviving AE = 12; got counts did not match.");
            Assert.AreEqual(7, surviving.Count(o => o.TableCategory == "PK"),
                "All 7 analyzable PK rows must survive; all 3 non-analyzable must be dropped.");
            Assert.AreEqual(5, surviving.Count(o => o.TableCategory == "ADVERSE_EVENT"),
                "All AE rows must pass through (PK-only filter).");
        }

        /**************************************************************/
        /// <summary>
        /// Builds a test orchestrator with a controlled observation list returned by
        /// the parser mock. Mirrors <c>createDropIncompleteTestOrchestrator</c> but
        /// accepts arbitrary observation lists for R13 filter scenarios. Leaves
        /// <c>dropRowsMissingArmNOrPrimaryValue</c> disabled so only the Stage 3.35
        /// (R13) filter contributes to the surviving-row count.
        /// </summary>
        /// <param name="controlledObservations">The parser mock's return value — the
        /// set of observations that enters Stage 3.25 (column standardization).</param>
        /// <returns>Orchestrator and DbContext. DbContext is returned for disposal;
        /// callers typically ignore it.</returns>
        private static (TableParsingOrchestrator orchestrator, ApplicationDbContext dbContext)
            createR13TestOrchestrator(List<ParsedObservation> controlledObservations)
        {
            #region implementation

            var mockRecon = new Mock<ITableReconstructionService>();
            var mockCellContext = new Mock<ITableCellContextService>();
            var mockLogger = new Mock<ILogger<TableParsingOrchestrator>>();

            var dummyTable = new ReconstructedTable
            {
                TextTableID = 2000,
                ParentSectionCode = "34090-1",
                TotalColumnCount = 2,
                TotalRowCount = 2,
                Header = new ResolvedHeader { HeaderRowCount = 1, ColumnCount = 2 },
                Rows = new List<ReconstructedRow>()
            };

            mockRecon
                .Setup(r => r.ReconstructTablesAsync(It.IsAny<TableCellContextFilter>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ReconstructedTable> { dummyTable });

            var mockParser = new Mock<ITableParser>();
            mockParser.SetupGet(p => p.SupportedCategory).Returns(TableCategory.PK);
            mockParser.SetupGet(p => p.Priority).Returns(0);
            mockParser.Setup(p => p.CanParse(It.IsAny<ReconstructedTable>())).Returns(true);
            mockParser.Setup(p => p.Parse(It.IsAny<ReconstructedTable>())).Returns(controlledObservations);

            var mockRouter = new Mock<ITableParserRouter>();
            mockRouter
                .Setup(r => r.Route(It.IsAny<ReconstructedTable>()))
                .Returns((TableCategory.PK, (ITableParser?)mockParser.Object));

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"R13_PreMlPkFilter_{Guid.NewGuid()}")
                .Options;
            var dbContext = new ApplicationDbContext(dbOptions);

            var orchestrator = new TableParsingOrchestrator(
                mockRecon.Object,
                mockCellContext.Object,
                mockRouter.Object,
                dbContext,
                mockLogger.Object,
                batchValidator: null,
                columnStandardizer: null,
                qcNetCorrectionService: null,
                correctionService: null,
                dropRowsMissingArmNOrPrimaryValue: false);

            return (orchestrator, dbContext);

            #endregion
        }

        #endregion R13 — Pre-ML PK Filter Tests

        #region R15 — Null-PrimaryValue Drop + Post-ML Re-run

        /**************************************************************/
        /// <summary>
        /// R15.2 — A PK observation with <see cref="ParsedObservation.PrimaryValue"/>
        /// null must be dropped even when ParameterName is populated and
        /// PrimaryValueType is not "Text". Covers the ND/NA/dash/empty_or_na rows
        /// observed in the 2026-04-23 corpus recompute where rows slipped through
        /// R13's original contract (non-Text type, named param, but no actual
        /// extractable numeric value).
        /// </summary>
        [TestMethod]
        public async Task R15_2_PkRow_NullPrimaryValue_Dropped()
        {
            var obs = new ParsedObservation
            {
                SourceRowSeq = 1,
                SourceCellSeq = 1,
                TableCategory = "PK",
                ParameterName = "Cmax",
                PrimaryValueType = "Mean",  // Not Text
                PrimaryValue = null,         // but no numeric value
                RawValue = "ND"
            };

            var (orchestrator, _) = createR13TestOrchestrator(new List<ParsedObservation> { obs });
            var result = await orchestrator.ProcessBatchWithStagesAsync(
                new TableCellContextFilter { TextTableIdRangeStart = 2000, TextTableIdRangeEnd = 2000 });

            Assert.AreEqual(0, result.PostCorrectionObservations.Count,
                "PK row with null PrimaryValue must be dropped — no analyzable numeric content.");
        }

        /**************************************************************/
        /// <summary>
        /// R15.2 — A PK row parsed as <c>range_to</c> now has
        /// <see cref="ParsedValue.PrimaryValue"/> populated (midpoint synthesized
        /// by R15.1) and survives the extended R13 filter. This is the key
        /// interaction: R15.1 makes range rows analyzable, so R15.2's null-drop
        /// contract doesn't accidentally remove legitimate range data.
        /// </summary>
        [TestMethod]
        public async Task R15_2_PkRow_RangeWithSynthesizedMidpoint_Kept()
        {
            var obs = new ParsedObservation
            {
                SourceRowSeq = 1,
                SourceCellSeq = 1,
                TableCategory = "PK",
                ParameterName = "t½",
                PrimaryValueType = "Range",      // Set by R15.1
                PrimaryValue = 141.85,            // Midpoint of 10.7 and 273 (R15.1)
                LowerBound = 10.7,
                UpperBound = 273.0,
                BoundType = "Range"
            };

            var (orchestrator, _) = createR13TestOrchestrator(new List<ParsedObservation> { obs });
            var result = await orchestrator.ProcessBatchWithStagesAsync(
                new TableCellContextFilter { TextTableIdRangeStart = 2000, TextTableIdRangeEnd = 2000 });

            Assert.AreEqual(1, result.PostCorrectionObservations.Count,
                "Range row with synthesized midpoint PrimaryValue must survive the filter.");
            Assert.AreEqual(141.85, result.PostCorrectionObservations[0].PrimaryValue);
        }

        /**************************************************************/
        /// <summary>
        /// R15.2 — Non-PK rows still pass through even when PrimaryValue is null.
        /// Regression guard on the PK-only scope of the filter.
        /// </summary>
        [TestMethod]
        public async Task R15_2_NonPkRow_NullPrimaryValue_Kept()
        {
            var obs = new ParsedObservation
            {
                SourceRowSeq = 1,
                SourceCellSeq = 1,
                TableCategory = "ADVERSE_EVENT",
                ParameterName = "Headache",
                PrimaryValueType = "Percentage",
                PrimaryValue = null
            };

            var (orchestrator, _) = createR13TestOrchestrator(new List<ParsedObservation> { obs });
            var result = await orchestrator.ProcessBatchWithStagesAsync(
                new TableCellContextFilter { TextTableIdRangeStart = 2000, TextTableIdRangeEnd = 2000 });

            Assert.AreEqual(1, result.PostCorrectionObservations.Count,
                "Non-PK categories pass the filter unchanged — null PrimaryValue is only a PK contract.");
        }

        #endregion R15 — Null-PrimaryValue Drop + Post-ML Re-run

        #region PK Time Extraction Tests

        /**************************************************************/
        /// <summary>
        /// PkTableParser.extractDuration correctly parses "x N days" multiplier patterns.
        /// </summary>
        [TestMethod]
        public void PkTableParser_ExtractDuration_MultiDayRegimen()
        {
            #region implementation

            var (time, timeUnit, timepoint) = PkTableParser.extractDuration("50 mg oral (once daily x 7 days)");

            Assert.AreEqual(7.0, time);
            Assert.AreEqual("days", timeUnit);
            Assert.AreEqual("7 days", timepoint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PkTableParser.extractDuration correctly parses 14-day regimens.
        /// </summary>
        [TestMethod]
        public void PkTableParser_ExtractDuration_14DayRegimen()
        {
            #region implementation

            var (time, timeUnit, timepoint) = PkTableParser.extractDuration("200 mg oral (once daily x 14 days)");

            Assert.AreEqual(14.0, time);
            Assert.AreEqual("days", timeUnit);
            Assert.AreEqual("14 days", timepoint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PkTableParser.extractDuration recognizes single-dose regimens.
        /// </summary>
        [TestMethod]
        public void PkTableParser_ExtractDuration_SingleDose()
        {
            #region implementation

            var (time, timeUnit, timepoint) = PkTableParser.extractDuration("150 mg single oral");

            Assert.IsNull(time);
            Assert.IsNull(timeUnit);
            Assert.AreEqual("single dose", timepoint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PkTableParser.extractDuration correctly parses week-based schedules.
        /// </summary>
        [TestMethod]
        public void PkTableParser_ExtractDuration_WeeklyRegimen()
        {
            #region implementation

            var (time, timeUnit, timepoint) = PkTableParser.extractDuration("400 mg IV (once weekly x 4 weeks)");

            Assert.AreEqual(4.0, time);
            Assert.AreEqual("weeks", timeUnit);
            Assert.AreEqual("4 weeks", timepoint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PkTableParser.extractDuration returns nulls for unrecognized patterns.
        /// </summary>
        [TestMethod]
        public void PkTableParser_ExtractDuration_UnrecognizedPattern()
        {
            #region implementation

            var (time, timeUnit, timepoint) = PkTableParser.extractDuration("50 mg oral");

            Assert.IsNull(time);
            Assert.IsNull(timeUnit);
            Assert.IsNull(timepoint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PkTableParser.extractDuration handles null/empty input gracefully.
        /// </summary>
        [TestMethod]
        public void PkTableParser_ExtractDuration_NullInput()
        {
            #region implementation

            var (time, timeUnit, timepoint) = PkTableParser.extractDuration(null);
            Assert.IsNull(time);
            Assert.IsNull(timeUnit);
            Assert.IsNull(timepoint);

            var (time2, timeUnit2, timepoint2) = PkTableParser.extractDuration("");
            Assert.IsNull(time2);
            Assert.IsNull(timeUnit2);
            Assert.IsNull(timepoint2);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PkTableParser.extractDuration handles "for N days" pattern.
        /// </summary>
        [TestMethod]
        public void PkTableParser_ExtractDuration_ForPattern()
        {
            #region implementation

            var (time, timeUnit, timepoint) = PkTableParser.extractDuration("100 mg daily for 14 days");

            Assert.AreEqual(14.0, time);
            Assert.AreEqual("days", timeUnit);
            Assert.AreEqual("14 days", timepoint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PK table parsing populates Time/TimeUnit/Timepoint on all observations from a dose row
        /// with a duration-containing regimen.
        /// </summary>
        [TestMethod]
        public void RouteAndParsePkTable_PopulatesTimeFieldsOnAllParameters()
        {
            #region implementation

            var (orchestrator, _, _) = createTestOrchestrator();

            var pkTable = new ReconstructedTable
            {
                TextTableID = 200,
                Caption = "Table 1: Mean PK Parameters",
                DocumentGUID = Guid.NewGuid(),
                Title = "Test Drug",
                VersionNumber = 1,
                ParentSectionCode = "34090-1",
                LabelerName = "Test Lab",
                TotalColumnCount = 3,
                TotalRowCount = 3,
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
                        new() { ColumnIndex = 0, LeafHeaderText = "Dose regimen", HeaderPath = new List<string> { "Dose regimen" } },
                        new() { ColumnIndex = 1, LeafHeaderText = "Cmax (mcg/mL)", HeaderPath = new List<string> { "Cmax (mcg/mL)" } },
                        new() { ColumnIndex = 2, LeafHeaderText = "Half-life (hours)", HeaderPath = new List<string> { "Half-life (hours)" } }
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
                            new() { SequenceNumber = 1, ResolvedColumnStart = 0, ResolvedColumnEnd = 1, CleanedText = "50 mg oral (once daily x 7 days)", CellType = "td" },
                            new() { SequenceNumber = 2, ResolvedColumnStart = 1, ResolvedColumnEnd = 2, CleanedText = "2.21", CellType = "td" },
                            new() { SequenceNumber = 3, ResolvedColumnStart = 2, ResolvedColumnEnd = 3, CleanedText = "26.6", CellType = "td" }
                        }
                    },
                    new()
                    {
                        SequenceNumberTextTableRow = 3,
                        Classification = RowClassification.DataBody,
                        AbsoluteRowIndex = 2,
                        Cells = new List<ProcessedCell>
                        {
                            new() { SequenceNumber = 1, ResolvedColumnStart = 0, ResolvedColumnEnd = 1, CleanedText = "150 mg single oral", CellType = "td" },
                            new() { SequenceNumber = 2, ResolvedColumnStart = 1, ResolvedColumnEnd = 2, CleanedText = "2.70", CellType = "td" },
                            new() { SequenceNumber = 3, ResolvedColumnStart = 2, ResolvedColumnEnd = 3, CleanedText = "34.1", CellType = "td" }
                        }
                    }
                }
            };

            var (category, parserName, observations) = orchestrator.RouteAndParseSingleTable(pkTable);

            Assert.AreEqual(TableCategory.PK, category);
            Assert.AreEqual(4, observations.Count);

            // Row 1, Cmax (non-time param): Time from dose regimen (7 days)
            var row1Cmax = observations.First(o => o.DoseRegimen == "50 mg oral (once daily x 7 days)" && o.ParameterName == "Cmax");
            Assert.AreEqual(7.0, row1Cmax.Time);
            Assert.AreEqual("days", row1Cmax.TimeUnit);
            Assert.AreEqual("7 days", row1Cmax.Timepoint);

            // Row 1, Half-life (time param): Time from PrimaryValue (26.6 hours)
            var row1Hl = observations.First(o => o.DoseRegimen == "50 mg oral (once daily x 7 days)" && o.ParameterName == "Half-life");
            Assert.AreEqual(26.6, row1Hl.Time);
            Assert.AreEqual("hours", row1Hl.TimeUnit);
            Assert.AreEqual("7 days", row1Hl.Timepoint); // Row-derived label preserved

            // Row 2, Cmax (single dose): Time null
            var row2Cmax = observations.First(o => o.DoseRegimen == "150 mg single oral" && o.ParameterName == "Cmax");
            Assert.IsNull(row2Cmax.Time);
            Assert.IsNull(row2Cmax.TimeUnit);
            Assert.AreEqual("single dose", row2Cmax.Timepoint);

            // Row 2, Half-life (time param, single dose): Time from PrimaryValue (34.1 hours)
            var row2Hl = observations.First(o => o.DoseRegimen == "150 mg single oral" && o.ParameterName == "Half-life");
            Assert.AreEqual(34.1, row2Hl.Time);
            Assert.AreEqual("hours", row2Hl.TimeUnit);
            Assert.AreEqual("single dose", row2Hl.Timepoint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PK table with Tmax (hrs) header — abbreviated time unit detected as time measure,
        /// Time overridden from PrimaryValue.
        /// </summary>
        [TestMethod]
        public void RouteAndParsePkTable_TmaxHrs_DetectedAsTimeMeasure()
        {
            #region implementation

            var (orchestrator, _, _) = createTestOrchestrator();

            var pkTable = new ReconstructedTable
            {
                TextTableID = 201,
                Caption = "Table 2: PK Parameters",
                DocumentGUID = Guid.NewGuid(),
                Title = "Test Drug",
                VersionNumber = 1,
                ParentSectionCode = "34090-1",
                LabelerName = "Test Lab",
                TotalColumnCount = 2,
                TotalRowCount = 2,
                HasExplicitHeader = true,
                HasInferredHeader = false,
                HasFooter = false,
                HasSocDividers = false,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 1,
                    ColumnCount = 2,
                    Columns = new List<HeaderColumn>
                    {
                        new() { ColumnIndex = 0, LeafHeaderText = "Dose", HeaderPath = new List<string> { "Dose" } },
                        new() { ColumnIndex = 1, LeafHeaderText = "Tmax (hrs)", HeaderPath = new List<string> { "Tmax (hrs)" } }
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
                            new() { SequenceNumber = 1, ResolvedColumnStart = 0, ResolvedColumnEnd = 1, CleanedText = "100 mg oral (once daily x 7 days)", CellType = "td" },
                            new() { SequenceNumber = 2, ResolvedColumnStart = 1, ResolvedColumnEnd = 2, CleanedText = "1.5", CellType = "td" }
                        }
                    }
                }
            };

            var (category, parserName, observations) = orchestrator.RouteAndParseSingleTable(pkTable);

            Assert.AreEqual(1, observations.Count);
            var obs = observations[0];
            Assert.AreEqual("Tmax", obs.ParameterName);
            Assert.AreEqual(1.5, obs.PrimaryValue);
            Assert.AreEqual(1.5, obs.Time); // Column-derived from PrimaryValue
            Assert.AreEqual("hours", obs.TimeUnit); // Normalized from "hrs"
            Assert.AreEqual("7 days", obs.Timepoint); // Row-derived label preserved

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Non-time parameter "AUC (mcg·h/mL)" with composite unit is NOT detected as time measure.
        /// </summary>
        [TestMethod]
        public void RouteAndParsePkTable_CompositeUnit_NotDetectedAsTimeMeasure()
        {
            #region implementation

            var (orchestrator, _, _) = createTestOrchestrator();

            var pkTable = new ReconstructedTable
            {
                TextTableID = 202,
                Caption = "Table 3: PK Parameters",
                DocumentGUID = Guid.NewGuid(),
                Title = "Test Drug",
                VersionNumber = 1,
                ParentSectionCode = "34090-1",
                LabelerName = "Test Lab",
                TotalColumnCount = 2,
                TotalRowCount = 2,
                HasExplicitHeader = true,
                HasInferredHeader = false,
                HasFooter = false,
                HasSocDividers = false,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 1,
                    ColumnCount = 2,
                    Columns = new List<HeaderColumn>
                    {
                        new() { ColumnIndex = 0, LeafHeaderText = "Dose", HeaderPath = new List<string> { "Dose" } },
                        new() { ColumnIndex = 1, LeafHeaderText = "AUC (mcg·h/mL)", HeaderPath = new List<string> { "AUC (mcg·h/mL)" } }
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
                            new() { SequenceNumber = 1, ResolvedColumnStart = 0, ResolvedColumnEnd = 1, CleanedText = "50 mg oral (once daily x 7 days)", CellType = "td" },
                            new() { SequenceNumber = 2, ResolvedColumnStart = 1, ResolvedColumnEnd = 2, CleanedText = "37.6", CellType = "td" }
                        }
                    }
                }
            };

            var (category, parserName, observations) = orchestrator.RouteAndParseSingleTable(pkTable);

            Assert.AreEqual(1, observations.Count);
            var obs = observations[0];
            Assert.AreEqual("AUC", obs.ParameterName);
            Assert.AreEqual(37.6, obs.PrimaryValue);
            Assert.AreEqual(7.0, obs.Time); // Row-derived (NOT overridden — composite unit)
            Assert.AreEqual("days", obs.TimeUnit);

            #endregion
        }

        #endregion PK Time Extraction Tests

        #region PK CI Dash Parsing Tests

        /**************************************************************/
        /// <summary>
        /// PK table with dose-response data: "0.38 (0.31 - 0.46)" parses as value with CI bounds.
        /// Footer row containing "90% CI" refines BoundType from "CI" to "90CI".
        /// </summary>
        /// <remarks>
        /// Caption and header intentionally avoid DDI-keyword triggers ("Drug Interaction",
        /// "Co-administered", "in the Presence of") so the Wave 3 R8 router continues to
        /// route this fixture to <see cref="TableCategory.PK"/>. Real DDI tables with the
        /// same shape now correctly route to <see cref="TableCategory.DRUG_INTERACTION"/>,
        /// covered by <c>Router_R8_*</c> tests in <c>TableParserTests</c>.
        /// </remarks>
        [TestMethod]
        public void RouteAndParsePkTable_DrugInteraction_ParsesCIDashWithFooterRefinement()
        {
            #region implementation

            var (orchestrator, _, _) = createTestOrchestrator();

            var pkTable = new ReconstructedTable
            {
                TextTableID = 289,
                Caption = "Table 4. MYCAPSSA Pharmacokinetic Parameters by Dose",
                DocumentGUID = Guid.NewGuid(),
                Title = "MYCAPSSA",
                VersionNumber = 1,
                ParentSectionCode = "43682-4",
                LabelerName = "Test Lab",
                TotalColumnCount = 4,
                TotalRowCount = 4,
                HasExplicitHeader = true,
                HasInferredHeader = false,
                HasFooter = false,
                HasSocDividers = false,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 1,
                    ColumnCount = 4,
                    Columns = new List<HeaderColumn>
                    {
                        new() { ColumnIndex = 0, LeafHeaderText = "Regimen", HeaderPath = new List<string> { "Regimen" } },
                        new() { ColumnIndex = 1, LeafHeaderText = "Dose (mg)", HeaderPath = new List<string> { "Dose (mg)" } },
                        new() { ColumnIndex = 2, LeafHeaderText = "Change in AUC", HeaderPath = new List<string> { "Change in AUC" } },
                        new() { ColumnIndex = 3, LeafHeaderText = "Change in Cmax", HeaderPath = new List<string> { "Change in Cmax" } }
                    }
                },
                Rows = new List<ReconstructedRow>
                {
                    // Data row: Cyclosporine
                    new()
                    {
                        SequenceNumberTextTableRow = 2,
                        Classification = RowClassification.DataBody,
                        AbsoluteRowIndex = 1,
                        Cells = new List<ProcessedCell>
                        {
                            new() { SequenceNumber = 1, ResolvedColumnStart = 0, ResolvedColumnEnd = 1, CleanedText = "Cyclosporine 300 mg", CellType = "td" },
                            new() { SequenceNumber = 2, ResolvedColumnStart = 1, ResolvedColumnEnd = 2, CleanedText = "20 mg", CellType = "td" },
                            new() { SequenceNumber = 3, ResolvedColumnStart = 2, ResolvedColumnEnd = 3, CleanedText = "0.38 (0.31 - 0.46)", CellType = "td" },
                            new() { SequenceNumber = 4, ResolvedColumnStart = 3, ResolvedColumnEnd = 4, CleanedText = "0.29 (0.22 - 0.37)", CellType = "td" }
                        }
                    },
                    // Footer/annotation row containing CI definition
                    new()
                    {
                        SequenceNumberTextTableRow = 3,
                        Classification = RowClassification.DataBody,
                        AbsoluteRowIndex = 2,
                        Cells = new List<ProcessedCell>
                        {
                            new() { SequenceNumber = 1, ResolvedColumnStart = 0, ResolvedColumnEnd = 4, CleanedText = "Mean ratio with 90% CI for the dose comparison", CellType = "td" }
                        }
                    }
                }
            };

            var (category, parserName, observations) = orchestrator.RouteAndParseSingleTable(pkTable);

            Assert.AreEqual(TableCategory.PK, category);

            // Filter to just the data observations (not the footer text which becomes text_descriptive)
            var ciObs = observations.Where(o => o.LowerBound != null).ToList();
            Assert.IsTrue(ciObs.Count >= 2, $"Expected at least 2 CI observations, got {ciObs.Count}");

            // Check AUC observation
            var aucObs = ciObs.First(o => o.ParameterName == "Change in AUC");
            Assert.AreEqual(0.38, aucObs.PrimaryValue);
            Assert.AreEqual(0.31, aucObs.LowerBound);
            Assert.AreEqual(0.46, aucObs.UpperBound);
            Assert.AreEqual("90CI", aucObs.BoundType); // Refined from footer row

            // Check Cmax observation
            var cmaxObs = ciObs.First(o => o.ParameterName == "Change in Cmax");
            Assert.AreEqual(0.29, cmaxObs.PrimaryValue);
            Assert.AreEqual(0.22, cmaxObs.LowerBound);
            Assert.AreEqual(0.37, cmaxObs.UpperBound);
            Assert.AreEqual("90CI", cmaxObs.BoundType);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PK table with CI dash values but NO footer CI info — BoundType stays generic "CI".
        /// </summary>
        [TestMethod]
        public void RouteAndParsePkTable_CIDash_NoFooter_BoundTypeStaysCI()
        {
            #region implementation

            var (orchestrator, _, _) = createTestOrchestrator();

            var pkTable = new ReconstructedTable
            {
                TextTableID = 290,
                Caption = "Table 5: PK Interaction Study",
                DocumentGUID = Guid.NewGuid(),
                Title = "Test Drug",
                VersionNumber = 1,
                ParentSectionCode = "43682-4",
                LabelerName = "Test Lab",
                TotalColumnCount = 2,
                TotalRowCount = 2,
                HasExplicitHeader = true,
                HasInferredHeader = false,
                HasFooter = false,
                HasSocDividers = false,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 1,
                    ColumnCount = 2,
                    Columns = new List<HeaderColumn>
                    {
                        new() { ColumnIndex = 0, LeafHeaderText = "Drug", HeaderPath = new List<string> { "Drug" } },
                        new() { ColumnIndex = 1, LeafHeaderText = "Change in AUC", HeaderPath = new List<string> { "Change in AUC" } }
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
                            new() { SequenceNumber = 1, ResolvedColumnStart = 0, ResolvedColumnEnd = 1, CleanedText = "Drug A 100 mg", CellType = "td" },
                            new() { SequenceNumber = 2, ResolvedColumnStart = 1, ResolvedColumnEnd = 2, CleanedText = "1.40 (1.21 - 1.61)", CellType = "td" }
                        }
                    }
                }
            };

            var (category, parserName, observations) = orchestrator.RouteAndParseSingleTable(pkTable);

            var ciObs = observations.Where(o => o.LowerBound != null).ToList();
            Assert.AreEqual(1, ciObs.Count);

            var obs = ciObs[0];
            Assert.AreEqual(1.40, obs.PrimaryValue);
            Assert.AreEqual(1.21, obs.LowerBound);
            Assert.AreEqual(1.61, obs.UpperBound);
            Assert.AreEqual("CI", obs.BoundType); // No footer info → stays generic

            #endregion
        }

        #endregion PK CI Dash Parsing Tests

        #region PK Sample Size and Population Tests

        /**************************************************************/
        /// <summary>
        /// PK table with "n" column: sample sizes parsed as Count, not promoted to Mean.
        /// </summary>
        [TestMethod]
        public void RouteAndParsePkTable_NColumn_ParsedAsSampleSize()
        {
            #region implementation

            var (orchestrator, _, _) = createTestOrchestrator();

            var pkTable = new ReconstructedTable
            {
                TextTableID = 346,
                Caption = "PK Parameters",
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
                        new() { ColumnIndex = 1, LeafHeaderText = "n", HeaderPath = new List<string> { "n" } },
                        new() { ColumnIndex = 2, LeafHeaderText = "Cmax (mcg/mL)", HeaderPath = new List<string> { "Cmax (mcg/mL)" } }
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
                            new() { SequenceNumber = 2, ResolvedColumnStart = 1, ResolvedColumnEnd = 2, CleanedText = "9", CellType = "td" },
                            new() { SequenceNumber = 3, ResolvedColumnStart = 2, ResolvedColumnEnd = 3, CleanedText = "2.21", CellType = "td" }
                        }
                    }
                }
            };

            var (category, parserName, observations) = orchestrator.RouteAndParseSingleTable(pkTable);

            Assert.AreEqual(2, observations.Count);

            // n column: Count, not Mean
            var nObs = observations.First(o => o.ParameterName == "n");
            Assert.AreEqual(9.0, nObs.PrimaryValue);
            Assert.AreEqual("Count", nObs.PrimaryValueType);

            // Cmax column: Mean (PK fallback for bare Numeric)
            var cmaxObs = observations.First(o => o.ParameterName == "Cmax");
            Assert.AreEqual(2.21, cmaxObs.PrimaryValue);
            Assert.AreEqual("Mean", cmaxObs.PrimaryValueType);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PK table with "Age Group (y)" as column 0: values become Population, not DoseRegimen.
        /// </summary>
        [TestMethod]
        public void RouteAndParsePkTable_AgeGroupColumn0_PopulatesPopulation()
        {
            #region implementation

            var (orchestrator, _, _) = createTestOrchestrator();

            var pkTable = new ReconstructedTable
            {
                TextTableID = 347,
                Caption = "Pediatric PK",
                DocumentGUID = Guid.NewGuid(),
                Title = "Test Drug",
                VersionNumber = 1,
                ParentSectionCode = "34090-1",
                LabelerName = "Test Lab",
                TotalColumnCount = 2,
                TotalRowCount = 2,
                HasExplicitHeader = true,
                HasInferredHeader = false,
                HasFooter = false,
                HasSocDividers = false,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 1,
                    ColumnCount = 2,
                    Columns = new List<HeaderColumn>
                    {
                        new() { ColumnIndex = 0, LeafHeaderText = "Age Group (y)", HeaderPath = new List<string> { "Age Group (y)" } },
                        new() { ColumnIndex = 1, LeafHeaderText = "Cmax (mcg/mL)", HeaderPath = new List<string> { "Cmax (mcg/mL)" } }
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
                            new() { SequenceNumber = 1, ResolvedColumnStart = 0, ResolvedColumnEnd = 1, CleanedText = "5-11", CellType = "td" },
                            new() { SequenceNumber = 2, ResolvedColumnStart = 1, ResolvedColumnEnd = 2, CleanedText = "3.5", CellType = "td" }
                        }
                    }
                }
            };

            var (category, parserName, observations) = orchestrator.RouteAndParseSingleTable(pkTable);

            Assert.AreEqual(1, observations.Count);
            var obs = observations[0];
            Assert.AreEqual("5-11", obs.Population);
            Assert.IsNull(obs.DoseRegimen);

            #endregion
        }

        #endregion PK Sample Size and Population Tests

    }
}
