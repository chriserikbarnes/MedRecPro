using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="RowValidationService"/> (Stage 4 row-level validation).
    /// </summary>
    /// <remarks>
    /// Tests cover all six validation checks: orphan detection, required fields by category,
    /// PrimaryValueType appropriateness, ArmN with TreatmentArm, bound consistency,
    /// and low confidence flagging.
    ///
    /// No database or complex mocking needed — RowValidationService operates on
    /// in-memory <see cref="ParsedObservation"/> DTOs with only an ILogger dependency.
    /// </remarks>
    /// <seealso cref="RowValidationService"/>
    /// <seealso cref="IRowValidationService"/>
    /// <seealso cref="RowValidationResult"/>
    [TestClass]
    public class RowValidationServiceTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a <see cref="RowValidationService"/> with a mocked logger.
        /// </summary>
        /// <returns>Configured service instance.</returns>
        private static RowValidationService createService()
        {
            #region implementation

            var mockLogger = new Mock<ILogger<RowValidationService>>();
            return new RowValidationService(mockLogger.Object);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a valid adverse event observation with all required fields populated.
        /// </summary>
        /// <returns>A complete <see cref="ParsedObservation"/> that passes all checks.</returns>
        private static ParsedObservation createValidAeObservation()
        {
            #region implementation

            return new ParsedObservation
            {
                TextTableID = 1,
                DocumentGUID = Guid.NewGuid(),
                LabelerName = "TestLabeler",
                ProductTitle = "TestDrug",
                VersionNumber = 1,
                TableCategory = "ADVERSE_EVENT",
                ParameterName = "Nausea",
                TreatmentArm = "TestDrug",
                ArmN = 188,
                PrimaryValue = 14.0,
                PrimaryValueType = "Percentage",
                Unit = "%",
                ParseConfidence = 1.0,
                ParseRule = "n_pct",
                SourceRowSeq = 3,
                SourceCellSeq = 2
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a valid PK observation with all required fields populated.
        /// </summary>
        /// <returns>A complete PK <see cref="ParsedObservation"/> that passes all checks.</returns>
        private static ParsedObservation createValidPkObservation()
        {
            #region implementation

            return new ParsedObservation
            {
                TextTableID = 2,
                DocumentGUID = Guid.NewGuid(),
                LabelerName = "TestLabeler",
                ProductTitle = "TestDrug",
                VersionNumber = 1,
                TableCategory = "PK",
                ParameterName = "Cmax",
                DoseRegimen = "50 mg oral",
                PrimaryValue = 2.21,
                PrimaryValueType = "Mean",
                Unit = "mcg/mL",
                ParseConfidence = 1.0,
                ParseRule = "plain_number",
                SourceRowSeq = 2,
                SourceCellSeq = 2
            };

            #endregion
        }

        #endregion Helper Methods

        #region Valid Observation Tests

        /**************************************************************/
        /// <summary>
        /// A complete AE observation with all fields passes validation as Valid.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_ValidAeRow_ReturnsValid()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Valid, result.Status);
            Assert.AreEqual(0, result.Issues.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// A complete PK observation with all fields passes validation as Valid.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_ValidPkRow_ReturnsValid()
        {
            #region implementation

            var service = createService();
            var obs = createValidPkObservation();

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Valid, result.Status);
            Assert.AreEqual(0, result.Issues.Count);

            #endregion
        }

        #endregion Valid Observation Tests

        #region Orphan Detection Tests

        /**************************************************************/
        /// <summary>
        /// Null TextTableID produces Error status with ORPHAN_ROW issue.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_NullTextTableId_ReturnsError()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.TextTableID = null;

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Error, result.Status);
            Assert.IsTrue(result.Issues.Any(i => i.StartsWith("ORPHAN_ROW")));
            Assert.IsTrue(obs.ValidationFlags!.Contains("ORPHAN_ROW"));

            #endregion
        }

        #endregion Orphan Detection Tests

        #region Required Fields Tests

        /**************************************************************/
        /// <summary>
        /// PK observation missing DoseRegimen produces Warning.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_PkMissingDoseRegimen_ReturnsWarning()
        {
            #region implementation

            var service = createService();
            var obs = createValidPkObservation();
            obs.DoseRegimen = null;

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Warning, result.Status);
            Assert.IsTrue(result.Issues.Any(i => i.Contains("MISSING_FIELD:DoseRegimen")));
            Assert.IsTrue(obs.ValidationFlags!.Contains("MISSING_DOSEREGIMEN"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// AE observation missing TreatmentArm produces Warning.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_AeMissingTreatmentArm_ReturnsWarning()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.TreatmentArm = null;
            obs.ArmN = null; // also clear ArmN to avoid MISSING_ARM_N

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Warning, result.Status);
            Assert.IsTrue(result.Issues.Any(i => i.Contains("MISSING_FIELD:TreatmentArm")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// BMD observation missing Timepoint produces Warning.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_BmdMissingTimepoint_ReturnsWarning()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.TableCategory = "BMD";
            obs.PrimaryValueType = "MeanPercentChange";
            obs.TreatmentArm = null;
            obs.ArmN = null;
            obs.Timepoint = null;

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Warning, result.Status);
            Assert.IsTrue(result.Issues.Any(i => i.Contains("MISSING_FIELD:Timepoint")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Missing TableCategory produces Warning with MISSING_CATEGORY.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_NullTableCategory_ReturnsWarning()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.TableCategory = null;

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Warning, result.Status);
            Assert.IsTrue(result.Issues.Any(i => i.Contains("MISSING_FIELD:TableCategory")));

            #endregion
        }

        #endregion Required Fields Tests

        #region Value Type Appropriateness Tests

        /**************************************************************/
        /// <summary>
        /// PK observation with Percentage as PrimaryValueType produces Warning
        /// since PK tables typically contain Mean values, not percentages.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_PkWithPercentage_ReturnsWarning()
        {
            #region implementation

            var service = createService();
            var obs = createValidPkObservation();
            obs.PrimaryValueType = "Percentage";

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Warning, result.Status);
            Assert.IsTrue(result.Issues.Any(i => i.Contains("UNEXPECTED_VALUE_TYPE")));

            #endregion
        }

        #endregion Value Type Appropriateness Tests

        #region ArmN With TreatmentArm Tests

        /**************************************************************/
        /// <summary>
        /// TreatmentArm set without ArmN produces Warning.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_ArmNMissing_WhenTreatmentArmSet_ReturnsWarning()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.ArmN = null;

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Warning, result.Status);
            Assert.IsTrue(result.Issues.Any(i => i.Contains("MISSING_ARM_N")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Comparison arm without ArmN does NOT produce a warning (expected case).
        /// </summary>
        [TestMethod]
        public void ValidateObservation_ComparisonArm_NoArmN_ReturnsValid()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.TreatmentArm = "Comparison";
            obs.ArmN = null;

            var result = service.ValidateObservation(obs);

            Assert.IsFalse(result.Issues.Any(i => i.Contains("MISSING_ARM_N")));

            #endregion
        }

        #endregion ArmN With TreatmentArm Tests

        #region Bound Consistency Tests

        /**************************************************************/
        /// <summary>
        /// LowerBound > UpperBound produces Error status with BOUND_INVERSION.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_BoundInversion_ReturnsError()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.LowerBound = 10.0;
            obs.UpperBound = 5.0;
            obs.BoundType = "95CI";

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Error, result.Status);
            Assert.IsTrue(result.Issues.Any(i => i.StartsWith("BOUND_INVERSION")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Valid bounds (Lower &lt; Upper) do not produce an issue.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_ValidBounds_NoIssue()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.LowerBound = -12.6;
            obs.UpperBound = 3.8;
            obs.BoundType = "95CI";

            var result = service.ValidateObservation(obs);

            Assert.IsFalse(result.Issues.Any(i => i.Contains("BOUND_INVERSION")));

            #endregion
        }

        #endregion Bound Consistency Tests

        #region Low Confidence Tests

        /**************************************************************/
        /// <summary>
        /// ParseConfidence below 0.5 produces Warning with LOW_CONFIDENCE.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_LowConfidence_FlagsWarning()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.ParseConfidence = 0.3;

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Warning, result.Status);
            Assert.IsTrue(result.Issues.Any(i => i.StartsWith("LOW_CONFIDENCE")));
            Assert.IsTrue(obs.ValidationFlags!.Contains("LOW_CONFIDENCE"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// ParseConfidence at exactly 0.5 does NOT flag as low confidence.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_ConfidenceAtThreshold_NoFlag()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.ParseConfidence = 0.5;

            var result = service.ValidateObservation(obs);

            Assert.IsFalse(result.Issues.Any(i => i.Contains("LOW_CONFIDENCE")));

            #endregion
        }

        #endregion Low Confidence Tests

        #region Flag Append Tests

        /**************************************************************/
        /// <summary>
        /// New validation flags are appended to existing Stage 3 flags with semicolon delimiter.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_AppendsToExistingFlags()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.ValidationFlags = "PCT_CHECK:PASS";
            obs.ParseConfidence = 0.3; // triggers LOW_CONFIDENCE

            service.ValidateObservation(obs);

            Assert.IsTrue(obs.ValidationFlags.StartsWith("PCT_CHECK:PASS;"));
            Assert.IsTrue(obs.ValidationFlags.Contains("LOW_CONFIDENCE"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Valid observations do not modify existing ValidationFlags.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_ValidRow_PreservesExistingFlags()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.ValidationFlags = "PCT_CHECK:PASS";

            service.ValidateObservation(obs);

            Assert.AreEqual("PCT_CHECK:PASS", obs.ValidationFlags);

            #endregion
        }

        #endregion Flag Append Tests

        #region Batch Validation Tests

        /**************************************************************/
        /// <summary>
        /// ValidateObservations processes a mixed batch and returns correct counts.
        /// </summary>
        [TestMethod]
        public void ValidateObservations_MixedBatch_ReturnsCorrectCounts()
        {
            #region implementation

            var service = createService();
            var observations = new List<ParsedObservation>
            {
                createValidAeObservation(),                                    // Valid
                createValidPkObservation(),                                    // Valid
                new ParsedObservation                                          // Error (orphan)
                {
                    TextTableID = null, TableCategory = "PK",
                    ParameterName = "Cmax", DoseRegimen = "50 mg",
                    PrimaryValueType = "Mean", ParseConfidence = 1.0
                },
                new ParsedObservation                                          // Warning (missing arm)
                {
                    TextTableID = 3, TableCategory = "ADVERSE_EVENT",
                    ParameterName = "Nausea", TreatmentArm = "Drug",
                    ArmN = null, PrimaryValueType = "Percentage",
                    ParseConfidence = 0.9
                }
            };

            var results = service.ValidateObservations(observations);

            Assert.AreEqual(4, results.Count);
            Assert.AreEqual(2, results.Count(r => r.Status == ValidationStatus.Valid));
            Assert.AreEqual(1, results.Count(r => r.Status == ValidationStatus.Error));
            Assert.AreEqual(1, results.Count(r => r.Status == ValidationStatus.Warning));

            #endregion
        }

        #endregion Batch Validation Tests
    }
}
