using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="TableValidationService"/> (Stage 4 table-level validation).
    /// </summary>
    /// <remarks>
    /// Tests cover duplicate detection, arm coverage gap detection, count reasonableness,
    /// and correct grouping by TextTableID.
    ///
    /// No database or complex mocking needed — TableValidationService operates on
    /// in-memory <see cref="ParsedObservation"/> lists with only an ILogger dependency.
    /// </remarks>
    /// <seealso cref="TableValidationService"/>
    /// <seealso cref="ITableValidationService"/>
    /// <seealso cref="TableValidationResult"/>
    [TestClass]
    public class TableValidationServiceTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="TableValidationService"/> with a mocked logger.
        /// </summary>
        /// <returns>Configured service instance.</returns>
        private static TableValidationService createService()
        {
            #region implementation

            var mockLogger = new Mock<ILogger<TableValidationService>>();
            return new TableValidationService(mockLogger.Object);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a standard AE observation for a given table, parameter, and arm.
        /// </summary>
        private static ParsedObservation createAeObservation(
            int textTableId, string parameterName, string treatmentArm, int armN,
            double primaryValue, int sourceRowSeq, int sourceCellSeq)
        {
            #region implementation

            return new ParsedObservation
            {
                TextTableID = textTableId,
                TableCategory = "ADVERSE_EVENT",
                ParameterName = parameterName,
                TreatmentArm = treatmentArm,
                ArmN = armN,
                PrimaryValue = primaryValue,
                PrimaryValueType = "Percentage",
                ParseConfidence = 1.0,
                ParseRule = "n_pct",
                SourceRowSeq = sourceRowSeq,
                SourceCellSeq = sourceCellSeq
            };

            #endregion
        }

        #endregion Helper Methods

        #region No Issues Tests

        /**************************************************************/
        /// <summary>
        /// A table with no duplicates and proper arm coverage returns Valid.
        /// </summary>
        [TestMethod]
        public void ValidateTable_NoDuplicates_ReturnsValid()
        {
            #region implementation

            var service = createService();
            var observations = new List<ParsedObservation>
            {
                createAeObservation(1, "Nausea", "Drug", 188, 14.0, 3, 2),
                createAeObservation(1, "Nausea", "Placebo", 173, 16.0, 3, 3),
                createAeObservation(1, "Headache", "Drug", 188, 8.0, 4, 2),
                createAeObservation(1, "Headache", "Placebo", 173, 9.0, 4, 3)
            };

            var result = service.ValidateTable(1, observations);

            Assert.AreEqual(ValidationStatus.Valid, result.Status);
            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(0, result.DuplicateKeys.Count);
            Assert.AreEqual(4, result.ObservationCount);

            #endregion
        }

        #endregion No Issues Tests

        #region Duplicate Detection Tests

        /**************************************************************/
        /// <summary>
        /// Duplicate observations (same ParameterName + TreatmentArm + SourceRowSeq)
        /// are flagged as Warning.
        /// </summary>
        [TestMethod]
        public void ValidateTable_DuplicateObservations_FlagsDuplicates()
        {
            #region implementation

            var service = createService();
            var observations = new List<ParsedObservation>
            {
                createAeObservation(1, "Nausea", "Drug", 188, 14.0, 3, 2),
                createAeObservation(1, "Nausea", "Drug", 188, 14.5, 3, 2), // duplicate
                createAeObservation(1, "Nausea", "Placebo", 173, 16.0, 3, 3)
            };

            var result = service.ValidateTable(1, observations);

            Assert.AreEqual(ValidationStatus.Warning, result.Status);
            Assert.AreEqual(1, result.DuplicateKeys.Count);
            Assert.IsTrue(result.Issues.Any(i => i.StartsWith("DUPLICATE_OBSERVATION")));

            #endregion
        }

        #endregion Duplicate Detection Tests

        #region Arm Coverage Tests

        /**************************************************************/
        /// <summary>
        /// An arm with ArmN defined but no rows with PrimaryValue is flagged.
        /// </summary>
        [TestMethod]
        public void ValidateTable_AeTable_ArmCoverageGap_FlagsMissing()
        {
            #region implementation

            var service = createService();
            var observations = new List<ParsedObservation>
            {
                createAeObservation(1, "Nausea", "Drug", 188, 14.0, 3, 2),
                createAeObservation(1, "Headache", "Drug", 188, 8.0, 4, 2),
                // Placebo arm has ArmN but null PrimaryValue (no data)
                new ParsedObservation
                {
                    TextTableID = 1, TableCategory = "ADVERSE_EVENT",
                    ParameterName = "Nausea", TreatmentArm = "Placebo",
                    ArmN = 173, PrimaryValue = null, PrimaryValueType = null,
                    SourceRowSeq = 3, SourceCellSeq = 3
                }
            };

            var result = service.ValidateTable(1, observations);

            Assert.AreEqual(ValidationStatus.Warning, result.Status);
            Assert.AreEqual(1, result.MissingArms.Count);
            Assert.AreEqual("Placebo", result.MissingArms[0]);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Comparison arm without data does NOT trigger arm coverage gap.
        /// </summary>
        [TestMethod]
        public void ValidateTable_ComparisonArm_NoCoverageGap()
        {
            #region implementation

            var service = createService();
            var observations = new List<ParsedObservation>
            {
                createAeObservation(1, "Nausea", "Drug", 188, 14.0, 3, 2),
                createAeObservation(1, "Nausea", "Placebo", 173, 16.0, 3, 3),
                // Comparison arm with no PrimaryValue — this is normal
                new ParsedObservation
                {
                    TextTableID = 1, TableCategory = "ADVERSE_EVENT",
                    ParameterName = "Nausea", TreatmentArm = "Comparison",
                    PrimaryValue = null, SourceRowSeq = 3, SourceCellSeq = 4
                }
            };

            var result = service.ValidateTable(1, observations);

            Assert.AreEqual(0, result.MissingArms.Count);

            #endregion
        }

        #endregion Arm Coverage Tests

        #region Count Reasonableness Tests

        /**************************************************************/
        /// <summary>
        /// Count deviation >20% from expected (arms × params) produces Warning.
        /// </summary>
        [TestMethod]
        public void ValidateTable_CountDeviation_AeTable_FlagsWarning()
        {
            #region implementation

            var service = createService();
            // 2 arms × 3 params = expected 6, but only 3 observations (50% deviation)
            var observations = new List<ParsedObservation>
            {
                createAeObservation(1, "Nausea", "Drug", 188, 14.0, 3, 2),
                createAeObservation(1, "Headache", "Drug", 188, 8.0, 4, 2),
                createAeObservation(1, "Diarrhea", "Drug", 188, 5.0, 5, 2),
                // Placebo arm has ArmN and data but only for Nausea
                createAeObservation(1, "Nausea", "Placebo", 173, 16.0, 3, 3)
            };

            var result = service.ValidateTable(1, observations);

            Assert.IsTrue(result.Issues.Any(i => i.StartsWith("COUNT_DEVIATION")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PK tables (non-arm-based) do not trigger count reasonableness checks.
        /// </summary>
        [TestMethod]
        public void ValidateTable_PkTable_NoCountCheck()
        {
            #region implementation

            var service = createService();
            var observations = new List<ParsedObservation>
            {
                new ParsedObservation
                {
                    TextTableID = 2, TableCategory = "PK",
                    ParameterName = "Cmax", DoseRegimen = "50 mg",
                    PrimaryValue = 2.21, PrimaryValueType = "Mean",
                    SourceRowSeq = 2, SourceCellSeq = 2
                }
            };

            var result = service.ValidateTable(2, observations);

            Assert.AreEqual(ValidationStatus.Valid, result.Status);
            Assert.IsFalse(result.Issues.Any(i => i.StartsWith("COUNT_DEVIATION")));

            #endregion
        }

        #endregion Count Reasonableness Tests

        #region Grouping Tests

        /**************************************************************/
        /// <summary>
        /// ValidateTables groups observations by TextTableID and returns one result per table.
        /// </summary>
        [TestMethod]
        public void ValidateTables_GroupsByTextTableId_ReturnsMultipleResults()
        {
            #region implementation

            var service = createService();
            var observations = new List<ParsedObservation>
            {
                createAeObservation(1, "Nausea", "Drug", 188, 14.0, 3, 2),
                createAeObservation(1, "Nausea", "Placebo", 173, 16.0, 3, 3),
                createAeObservation(2, "Headache", "Drug", 188, 8.0, 3, 2),
                createAeObservation(2, "Headache", "Placebo", 173, 9.0, 3, 3)
            };

            var results = service.ValidateTables(observations);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Any(r => r.TextTableID == 1));
            Assert.IsTrue(results.Any(r => r.TextTableID == 2));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Observations with null TextTableID are excluded from table-level validation.
        /// </summary>
        [TestMethod]
        public void ValidateTables_NullTextTableId_Excluded()
        {
            #region implementation

            var service = createService();
            var observations = new List<ParsedObservation>
            {
                createAeObservation(1, "Nausea", "Drug", 188, 14.0, 3, 2),
                new ParsedObservation
                {
                    TextTableID = null, TableCategory = "ADVERSE_EVENT",
                    ParameterName = "Orphan", SourceRowSeq = 1, SourceCellSeq = 1
                }
            };

            var results = service.ValidateTables(observations);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(1, results[0].TextTableID);

            #endregion
        }

        #endregion Grouping Tests
    }
}
