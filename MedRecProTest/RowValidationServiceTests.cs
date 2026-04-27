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

        /**************************************************************/
        /// <summary>
        /// Multiple new flags appended in one validation pass are separated by the
        /// canonical "; " (semicolon + space) delimiter shared with
        /// <see cref="ValidationFlagExtensions.AppendValidationFlag"/>.
        /// </summary>
        /// <seealso cref="ValidationFlagExtensions"/>
        [TestMethod]
        public void ValidateObservation_AppendsFlags_UseSemicolonSpaceDelimiter()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.TextTableID = null; // ORPHAN_ROW
            obs.ParseConfidence = 0.3; // LOW_CONFIDENCE

            service.ValidateObservation(obs);

            Assert.IsTrue(obs.ValidationFlags!.Contains("; "),
                $"Expected '; ' delimiter between flags, got: {obs.ValidationFlags}");
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(obs.ValidationFlags, @";[^ ]"),
                $"No bare ';' (without trailing space) should remain. Got: {obs.ValidationFlags}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pre-existing Stage 3 flags joined to a single new failure flag use the
        /// "; " delimiter exactly — no bare ";" between segments.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_AppendsFlags_PreservesPreExistingFlagsWithSpaceDelimiter()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.ValidationFlags = "PCT_CHECK:PASS";
            obs.TextTableID = null; // single failure → ORPHAN_ROW

            service.ValidateObservation(obs);

            Assert.AreEqual("PCT_CHECK:PASS; ORPHAN_ROW", obs.ValidationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Multiple flags appended onto an empty ValidationFlags string are also
        /// "; "-delimited (not bare ";").
        /// </summary>
        [TestMethod]
        public void ValidateObservation_AppendsFlags_MultipleNewFlags_SeparatedBySemicolonSpace()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.ValidationFlags = null;
            obs.TextTableID = null;       // ORPHAN_ROW
            obs.ParseConfidence = 0.3;     // LOW_CONFIDENCE

            service.ValidateObservation(obs);

            Assert.IsNotNull(obs.ValidationFlags);
            Assert.IsTrue(obs.ValidationFlags.Contains("ORPHAN_ROW; LOW_CONFIDENCE")
                          || obs.ValidationFlags.Contains("LOW_CONFIDENCE; ORPHAN_ROW"),
                $"Expected '; '-joined flags, got: {obs.ValidationFlags}");

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

        #region Time/TimeUnit Validation Tests

        /**************************************************************/
        /// <summary>
        /// Time present but TimeUnit null produces TIME_UNIT_MISMATCH warning.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_TimePresentUnitNull_ReturnsWarning()
        {
            #region implementation

            var service = createService();
            var obs = createValidPkObservation();
            obs.Time = 7.0;
            obs.TimeUnit = null;

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Warning, result.Status);
            Assert.IsTrue(result.Issues.Any(i => i.Contains("TIME_UNIT_MISMATCH")));
            Assert.IsTrue(obs.ValidationFlags!.Contains("TIME_UNIT_MISMATCH"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TimeUnit present but Time null produces TIME_UNIT_MISMATCH warning.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_TimeNullUnitPresent_ReturnsWarning()
        {
            #region implementation

            var service = createService();
            var obs = createValidPkObservation();
            obs.Time = null;
            obs.TimeUnit = "days";

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(ValidationStatus.Warning, result.Status);
            Assert.IsTrue(result.Issues.Any(i => i.Contains("TIME_UNIT_MISMATCH")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Both Time and TimeUnit present with valid values produces no time-related warning.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_TimeAndUnitBothPresent_NoWarning()
        {
            #region implementation

            var service = createService();
            var obs = createValidPkObservation();
            obs.Time = 7.0;
            obs.TimeUnit = "days";

            var result = service.ValidateObservation(obs);

            Assert.IsFalse(result.Issues.Any(i => i.Contains("TIME_UNIT_MISMATCH")));
            Assert.IsFalse(result.Issues.Any(i => i.Contains("UNREASONABLE_TIME")));
            Assert.IsFalse(result.Issues.Any(i => i.Contains("INVALID_TIME_UNIT")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Time <= 0 produces UNREASONABLE_TIME warning.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_ZeroTime_ReturnsWarning()
        {
            #region implementation

            var service = createService();
            var obs = createValidPkObservation();
            obs.Time = 0;
            obs.TimeUnit = "days";

            var result = service.ValidateObservation(obs);

            Assert.IsTrue(result.Issues.Any(i => i.Contains("UNREASONABLE_TIME")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Unrecognized TimeUnit produces INVALID_TIME_UNIT warning.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_InvalidTimeUnit_ReturnsWarning()
        {
            #region implementation

            var service = createService();
            var obs = createValidPkObservation();
            obs.Time = 7.0;
            obs.TimeUnit = "fortnight";

            var result = service.ValidateObservation(obs);

            Assert.IsTrue(result.Issues.Any(i => i.Contains("INVALID_TIME_UNIT")));

            #endregion
        }

        #endregion Time/TimeUnit Validation Tests

        #region Field Completeness Tests

        /**************************************************************/
        /// <summary>
        /// Fully populated PK observation returns field completeness of 1.0.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_FullPk_FieldCompleteness1()
        {
            #region implementation

            var service = createService();
            var obs = createValidPkObservation();
            obs.Population = "Adult Healthy Volunteers";
            obs.Timepoint = "7 days";
            obs.Time = 7.0;
            obs.TimeUnit = "days";

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(1.0, result.FieldCompletenessScore, 0.01);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PK observation missing optional fields returns proportional completeness.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_PartialPk_FieldCompletenessProportional()
        {
            #region implementation

            var service = createService();
            var obs = createValidPkObservation();
            // Has: ParameterName, DoseRegimen, Unit (3 of 7 expected)
            // Missing: Population, Timepoint, Time, TimeUnit

            var result = service.ValidateObservation(obs);

            // 3 of 7 = ~0.43
            Assert.IsTrue(result.FieldCompletenessScore > 0.4);
            Assert.IsTrue(result.FieldCompletenessScore < 0.5);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Categories with an empty CompletenessFields list (DRUG_INTERACTION, TEXT_DESCRIPTIVE,
        /// OTHER, unknown) return a completeness score of 1.0 — preserves the legacy "unknown
        /// category — don't penalize" behavior after the migration to
        /// <see cref="MedRecProImportClass.Service.TransformationServices.Dictionaries.CategoryProfileRegistry"/>.
        /// </summary>
        [TestMethod]
        public void Completeness_ZeroFieldsCategory_Returns_1_0()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.TableCategory = "DRUG_INTERACTION"; // empty CompletenessFields in registry

            var result = service.ValidateObservation(obs);

            Assert.AreEqual(1.0, result.FieldCompletenessScore, 1e-9);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Categories whose AllowedValueTypes set is empty (e.g. unknown categories) skip the
        /// value-type appropriateness check entirely — no UNEXPECTED_VALUE_TYPE flag emitted.
        /// </summary>
        [TestMethod]
        public void AllowedValueTypes_EmptySet_NoConstraintCheck()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.TableCategory = "OTHER";          // OTHER profile has empty AllowedValueTypes
            obs.PrimaryValueType = "Bizarre";     // would fail any non-empty allowlist
            obs.TextTableID = 1;

            service.ValidateObservation(obs);

            Assert.IsFalse(obs.ValidationFlags?.Contains("UNEXPECTED_VALUE_TYPE") ?? false,
                $"OTHER (no allowed-types constraint) must not flag UNEXPECTED_VALUE_TYPE. Got: {obs.ValidationFlags}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Required-field check sources its list from
        /// <see cref="MedRecProImportClass.Service.TransformationServices.Dictionaries.CategoryProfileRegistry"/>;
        /// unknown categories produce no required-field flags (graceful empty path).
        /// </summary>
        [TestMethod]
        public void RowRequired_PullsFromRegistry_UnknownCategory_NoFlags()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();
            obs.TableCategory = "BOGUS_CATEGORY"; // returns CategoryProfile.Empty
            obs.ParameterName = null;
            obs.TreatmentArm = null;

            service.ValidateObservation(obs);

            Assert.IsFalse(obs.ValidationFlags?.Contains("MISSING_PARAMETERNAME") ?? false,
                $"Unknown category should produce no MISSING_* required-field flags. Got: {obs.ValidationFlags}");
            Assert.IsFalse(obs.ValidationFlags?.Contains("MISSING_TREATMENTARM") ?? false,
                $"Unknown category should produce no MISSING_* required-field flags. Got: {obs.ValidationFlags}");

            #endregion
        }

        #endregion Field Completeness Tests

        #region Adjusted Confidence Tests

        /**************************************************************/
        /// <summary>
        /// Valid observation with no issues has AdjustedConfidence equal to ParseConfidence.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_NoIssues_AdjustedEqualsParseConfidence()
        {
            #region implementation

            var service = createService();
            var obs = createValidAeObservation();

            service.ValidateObservation(obs);

            Assert.AreEqual(obs.ParseConfidence, obs.AdjustedConfidence);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Observation with missing required field has AdjustedConfidence reduced by penalty.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_MissingField_AdjustedConfidenceReduced()
        {
            #region implementation

            var service = createService();
            var obs = createValidPkObservation();
            obs.DoseRegimen = null; // Missing required field

            service.ValidateObservation(obs);

            Assert.IsNotNull(obs.AdjustedConfidence);
            Assert.IsTrue(obs.AdjustedConfidence < obs.ParseConfidence,
                $"AdjustedConfidence ({obs.AdjustedConfidence}) should be less than ParseConfidence ({obs.ParseConfidence})");
            // Expected: 1.0 * 0.85 = 0.85
            Assert.AreEqual(0.85, obs.AdjustedConfidence!.Value, 0.01);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Multiple issues compound the penalty multipliers.
        /// </summary>
        [TestMethod]
        public void ValidateObservation_MultipleIssues_AdjustedConfidenceCompounded()
        {
            #region implementation

            var service = createService();
            var obs = createValidPkObservation();
            obs.DoseRegimen = null; // MISSING_DOSEREGIMEN → ×0.85
            obs.PrimaryValueType = "Percentage"; // UNEXPECTED_VALUE_TYPE → ×0.90

            service.ValidateObservation(obs);

            // Expected: 1.0 * 0.85 * 0.90 = 0.765
            Assert.AreEqual(0.765, obs.AdjustedConfidence!.Value, 0.01);

            #endregion
        }

        #endregion Adjusted Confidence Tests
    }
}
