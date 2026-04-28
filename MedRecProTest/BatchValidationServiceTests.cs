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
    /// Unit tests for <see cref="BatchValidationService"/> (Stage 4 batch-level validation).
    /// </summary>
    /// <remarks>
    /// Tests cover aggregate statistics computation (category counts, confidence distribution,
    /// flag summaries), skip reason pass-through, and delegation to row/table sub-services.
    ///
    /// Uses Moq for <see cref="IRowValidationService"/> and <see cref="ITableValidationService"/>,
    /// and EF Core InMemoryDatabase for <see cref="ApplicationDbContext"/>.
    /// </remarks>
    /// <seealso cref="BatchValidationService"/>
    /// <seealso cref="IBatchValidationService"/>
    /// <seealso cref="BatchValidationReport"/>
    [TestClass]
    public class BatchValidationServiceTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="BatchValidationService"/> with mocked sub-services
        /// that return no issues by default.
        /// </summary>
        /// <param name="rowResults">Optional row results to return from mock.</param>
        /// <param name="tableResults">Optional table results to return from mock.</param>
        /// <returns>Tuple of (service, mockRowValidator, mockTableValidator).</returns>
        private static (BatchValidationService service, Mock<IRowValidationService> mockRow, Mock<ITableValidationService> mockTable)
            createService(
                List<RowValidationResult>? rowResults = null,
                List<TableValidationResult>? tableResults = null)
        {
            #region implementation

            var mockRowValidator = new Mock<IRowValidationService>();
            mockRowValidator
                .Setup(r => r.ValidateObservations(It.IsAny<List<ParsedObservation>>()))
                .Returns(rowResults ?? new List<RowValidationResult>());

            var mockTableValidator = new Mock<ITableValidationService>();
            mockTableValidator
                .Setup(t => t.ValidateTables(It.IsAny<List<ParsedObservation>>()))
                .Returns(tableResults ?? new List<TableValidationResult>());

            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"BatchValidation_{Guid.NewGuid()}")
                .Options;
            var dbContext = new ApplicationDbContext(dbOptions);

            var mockLogger = new Mock<ILogger<BatchValidationService>>();

            var service = new BatchValidationService(
                mockRowValidator.Object,
                mockTableValidator.Object,
                dbContext,
                mockLogger.Object);

            return (service, mockRowValidator, mockTableValidator);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a list of sample observations across multiple categories.
        /// </summary>
        /// <returns>List of diverse observations for aggregate testing.</returns>
        private static List<ParsedObservation> createSampleObservations()
        {
            #region implementation

            return new List<ParsedObservation>
            {
                // AE observation, high confidence, PCT_CHECK:PASS flag
                new ParsedObservation
                {
                    TextTableID = 1, TableCategory = "ADVERSE_EVENT",
                    ParameterName = "Nausea", TreatmentArm = "Drug", ArmN = 188,
                    PrimaryValue = 14.0, PrimaryValueType = "Percentage",
                    ParseConfidence = 1.0, ParseRule = "n_pct",
                    ValidationFlags = "PCT_CHECK:PASS",
                    SourceRowSeq = 3, SourceCellSeq = 2
                },
                // AE observation, high confidence, PCT_CHECK:WARN flag
                new ParsedObservation
                {
                    TextTableID = 1, TableCategory = "ADVERSE_EVENT",
                    ParameterName = "Nausea", TreatmentArm = "Placebo", ArmN = 173,
                    PrimaryValue = 16.0, PrimaryValueType = "Percentage",
                    ParseConfidence = 1.0, ParseRule = "n_pct",
                    ValidationFlags = "PCT_CHECK:WARN:16.2",
                    SourceRowSeq = 3, SourceCellSeq = 3
                },
                // PK observation, high confidence
                new ParsedObservation
                {
                    TextTableID = 2, TableCategory = "PK",
                    ParameterName = "Cmax", DoseRegimen = "50 mg oral",
                    PrimaryValue = 2.21, PrimaryValueType = "Mean",
                    ParseConfidence = 0.9, ParseRule = "plain_number",
                    SourceRowSeq = 2, SourceCellSeq = 2
                },
                // PK observation, medium confidence
                new ParsedObservation
                {
                    TextTableID = 2, TableCategory = "PK",
                    ParameterName = "AUC", DoseRegimen = "50 mg oral",
                    PrimaryValue = 37.6, PrimaryValueType = "Mean",
                    ParseConfidence = 0.7, ParseRule = "range_to",
                    SourceRowSeq = 2, SourceCellSeq = 3
                },
                // EFFICACY observation, low confidence
                new ParsedObservation
                {
                    TextTableID = 3, TableCategory = "EFFICACY",
                    ParameterName = "Overall Survival", TreatmentArm = "Drug",
                    PrimaryValueType = "Text",
                    ParseConfidence = 0.3, ParseRule = "text_descriptive",
                    SourceRowSeq = 1, SourceCellSeq = 1
                }
            };

            #endregion
        }

        #endregion Helper Methods

        #region Aggregate Statistics Tests

        /**************************************************************/
        /// <summary>
        /// Report correctly counts observations by TableCategory.
        /// </summary>
        [TestMethod]
        public async Task GenerateReport_PopulatesRowCountByCategory()
        {
            #region implementation

            var (service, _, _) = createService();
            var observations = createSampleObservations();

            var report = await service.GenerateReportAsync(observations);

            Assert.AreEqual(5, report.TotalObservations);
            Assert.AreEqual(2, report.RowCountByCategory["ADVERSE_EVENT"]);
            Assert.AreEqual(2, report.RowCountByCategory["PK"]);
            Assert.AreEqual(1, report.RowCountByCategory["EFFICACY"]);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Report correctly distributes observations across confidence tiers.
        /// </summary>
        [TestMethod]
        public async Task GenerateReport_PopulatesConfidenceDistribution()
        {
            #region implementation

            var (service, _, _) = createService();
            var observations = createSampleObservations();

            var report = await service.GenerateReportAsync(observations);

            // VeryHigh (>=0.95): 2 observations (1.0, 1.0)
            Assert.AreEqual(2, report.VeryHighConfidenceCount);
            // High (0.80-0.95): 1 observation (0.9)
            Assert.AreEqual(1, report.HighConfidenceCount);
            // Medium (0.60-0.80): 1 observation (0.7)
            Assert.AreEqual(1, report.MediumConfidenceCount);
            // Low (0.40-0.60): 0 observations
            Assert.AreEqual(0, report.LowConfidenceCount);
            // VeryLow (<0.40): 1 observation (0.3)
            Assert.AreEqual(1, report.VeryLowConfidenceCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Report correctly counts PASS and WARN validation flags.
        /// </summary>
        [TestMethod]
        public async Task GenerateReport_PopulatesValidationFlagsSummary()
        {
            #region implementation

            var (service, _, _) = createService();
            var observations = createSampleObservations();

            var report = await service.GenerateReportAsync(observations);

            Assert.AreEqual(1, report.PassFlagCount);
            Assert.AreEqual(1, report.WarnFlagCount);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Report correctly counts distinct TextTableIDs.
        /// </summary>
        [TestMethod]
        public async Task GenerateReport_CountsDistinctTables()
        {
            #region implementation

            var (service, _, _) = createService();
            var observations = createSampleObservations();

            var report = await service.GenerateReportAsync(observations);

            Assert.AreEqual(3, report.TotalTablesProcessed);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Report correctly groups observations by ParseRule.
        /// </summary>
        [TestMethod]
        public async Task GenerateReport_PopulatesRowCountByParseRule()
        {
            #region implementation

            var (service, _, _) = createService();
            var observations = createSampleObservations();

            var report = await service.GenerateReportAsync(observations);

            Assert.AreEqual(2, report.RowCountByParseRule["n_pct"]);
            Assert.AreEqual(1, report.RowCountByParseRule["plain_number"]);
            Assert.AreEqual(1, report.RowCountByParseRule["range_to"]);
            Assert.AreEqual(1, report.RowCountByParseRule["text_descriptive"]);

            #endregion
        }

        #endregion Aggregate Statistics Tests

        #region Skip Reasons Tests

        /**************************************************************/
        /// <summary>
        /// Skip reasons dictionary is correctly passed through to the report.
        /// </summary>
        [TestMethod]
        public async Task GenerateReport_IncludesSkipReasons()
        {
            #region implementation

            var (service, _, _) = createService();
            var observations = createSampleObservations();
            var skipReasons = new Dictionary<int, string>
            {
                { 10, "SKIP:PatientInfo" },
                { 11, "SKIP:PatientInfo" },
                { 12, "SKIP:NDC" },
                { 13, "ERROR:ParseFailed" }
            };

            var report = await service.GenerateReportAsync(observations, skipReasons);

            Assert.AreEqual(4, report.TotalTablesSkipped);
            Assert.AreEqual(2, report.SkipReasons["SKIP:PatientInfo"]);
            Assert.AreEqual(1, report.SkipReasons["SKIP:NDC"]);
            Assert.AreEqual(1, report.SkipReasons["ERROR:ParseFailed"]);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null skip reasons produces zero skipped count.
        /// </summary>
        [TestMethod]
        public async Task GenerateReport_NullSkipReasons_ZeroSkipped()
        {
            #region implementation

            var (service, _, _) = createService();
            var observations = createSampleObservations();

            var report = await service.GenerateReportAsync(observations, null);

            Assert.AreEqual(0, report.TotalTablesSkipped);
            Assert.AreEqual(0, report.SkipReasons.Count);

            #endregion
        }

        #endregion Skip Reasons Tests

        #region Sub-Service Delegation Tests

        /**************************************************************/
        /// <summary>
        /// Report includes row-level issues from IRowValidationService.
        /// </summary>
        [TestMethod]
        public async Task GenerateReport_IntegratesRowIssues()
        {
            #region implementation

            var rowResults = new List<RowValidationResult>
            {
                new RowValidationResult
                {
                    TextTableID = 1, ParameterName = "Nausea",
                    Status = ValidationStatus.Warning,
                    Issues = new List<string> { "MISSING_ARM_N" }
                },
                new RowValidationResult
                {
                    TextTableID = 1, ParameterName = "Headache",
                    Status = ValidationStatus.Valid,
                    Issues = new List<string>()
                }
            };

            var (service, _, _) = createService(rowResults: rowResults);
            var observations = createSampleObservations();

            var report = await service.GenerateReportAsync(observations);

            Assert.AreEqual(1, report.RowIssues.Count);
            Assert.AreEqual("Nausea", report.RowIssues[0].ParameterName);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Report includes table-level issues from ITableValidationService.
        /// </summary>
        [TestMethod]
        public async Task GenerateReport_IntegratesTableIssues()
        {
            #region implementation

            var tableResults = new List<TableValidationResult>
            {
                new TableValidationResult
                {
                    TextTableID = 1, TableCategory = "ADVERSE_EVENT",
                    Status = ValidationStatus.Warning,
                    Issues = new List<string> { "DUPLICATE_OBSERVATION:(Nausea, Drug, Row=3) × 2" }
                },
                new TableValidationResult
                {
                    TextTableID = 2, TableCategory = "PK",
                    Status = ValidationStatus.Valid,
                    Issues = new List<string>()
                }
            };

            var (service, _, _) = createService(tableResults: tableResults);
            var observations = createSampleObservations();

            var report = await service.GenerateReportAsync(observations);

            Assert.AreEqual(1, report.TableIssues.Count);
            Assert.AreEqual(1, report.TableIssues[0].TextTableID);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// ValidateObservations is called on the row validator with the correct observations.
        /// </summary>
        [TestMethod]
        public async Task GenerateReport_DelegatesToRowValidator()
        {
            #region implementation

            var (service, mockRow, _) = createService();
            var observations = createSampleObservations();

            await service.GenerateReportAsync(observations);

            mockRow.Verify(r => r.ValidateObservations(
                It.Is<List<ParsedObservation>>(o => o.Count == 5)),
                Times.Once);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// ValidateTables is called on the table validator with the correct observations.
        /// </summary>
        [TestMethod]
        public async Task GenerateReport_DelegatesToTableValidator()
        {
            #region implementation

            var (service, _, mockTable) = createService();
            var observations = createSampleObservations();

            await service.GenerateReportAsync(observations);

            mockTable.Verify(t => t.ValidateTables(
                It.Is<List<ParsedObservation>>(o => o.Count == 5)),
                Times.Once);

            #endregion
        }

        #endregion Sub-Service Delegation Tests

        #region Empty Input Tests

        /**************************************************************/
        /// <summary>
        /// Empty observation list produces a valid report with zero counts.
        /// </summary>
        [TestMethod]
        public async Task GenerateReport_EmptyObservations_ReturnsZeroCounts()
        {
            #region implementation

            var (service, _, _) = createService();
            var observations = new List<ParsedObservation>();

            var report = await service.GenerateReportAsync(observations);

            Assert.AreEqual(0, report.TotalObservations);
            Assert.AreEqual(0, report.TotalTablesProcessed);
            Assert.AreEqual(0, report.HighConfidenceCount);
            Assert.AreEqual(0, report.MediumConfidenceCount);
            Assert.AreEqual(0, report.LowConfidenceCount);
            Assert.AreEqual(0, report.PassFlagCount);
            Assert.AreEqual(0, report.WarnFlagCount);

            #endregion
        }

        #endregion Empty Input Tests
    }
}
