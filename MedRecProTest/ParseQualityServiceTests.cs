using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Tests for <see cref="ParseQualityService"/> — the deterministic parse-quality
    /// evaluator that replaces Stage 4 anomaly scoring. Verifies that hard failures,
    /// per-category Required misses, structural garbage, soft repair signals, and the
    /// ParseConfidence floor all produce the expected score and reason output.
    /// </summary>
    /// <seealso cref="IParseQualityService"/>
    /// <seealso cref="ColumnContractRegistry"/>
    [TestClass]
    public class ParseQualityServiceTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Builds a clean, "perfect" AdverseEvent observation — every Required field populated
        /// with plausible values, no soft-repair flags, no structural garbage. Tests then mutate
        /// individual fields to isolate specific penalties.
        /// </summary>
        private static ParsedObservation buildCleanAdverseEvent()
        {
            #region implementation

            return new ParsedObservation
            {
                TableCategory = "AdverseEvent",
                ParameterName = "Nausea",
                ParameterCategory = "Gastrointestinal Disorders",
                TreatmentArm = "Drug X",
                ArmN = 200,
                PrimaryValue = 12.5,
                PrimaryValueType = "Percentage",
                Unit = "%",
                ParseConfidence = 1.0,
                SourceRowSeq = 1,
                SourceCellSeq = 1,
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a clean PK observation. Required columns per contract: ParameterName,
        /// PrimaryValue, PrimaryValueType, Unit.
        /// </summary>
        private static ParsedObservation buildCleanPk()
        {
            #region implementation

            return new ParsedObservation
            {
                TableCategory = "PK",
                ParameterName = "Cmax",
                DoseRegimen = "50 mg oral once daily",
                Dose = 50m,
                DoseUnit = "mg",
                PrimaryValue = 125.0,
                PrimaryValueType = "GeometricMean",
                SecondaryValue = 12.3,
                SecondaryValueType = "SD",
                Unit = "ng/mL",
                ParseConfidence = 1.0,
                SourceRowSeq = 1,
                SourceCellSeq = 1,
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the service under test with the real registry.
        /// </summary>
        private static ParseQualityService buildService()
        {
            return new ParseQualityService(new ColumnContractRegistry());
        }

        #endregion Helper Methods

        #region Valid / clean observations

        /**************************************************************/
        /// <summary>
        /// Clean AdverseEvent with every Required column populated and no soft repairs should
        /// score 1.0 with no reasons.
        /// </summary>
        [TestMethod]
        public void Evaluate_ValidRow_ReturnsHighScore()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();

            var result = service.Evaluate(obs);

            Assert.AreEqual(1.0f, result.Score, 0.001f,
                $"Clean row should score 1.0 but got {result.Score:F4}; reasons: {string.Join("|", result.Reasons)}");
            Assert.AreEqual(0, result.Reasons.Count, "No penalties should fire on a clean row.");

            #endregion
        }

        #endregion Valid / clean observations

        #region Hard failures

        /**************************************************************/
        /// <summary>
        /// Null PrimaryValue on an otherwise clean row drops the score deep below threshold
        /// and fires the <c>PrimaryValueNull</c> reason.
        /// </summary>
        [TestMethod]
        public void Evaluate_NullPrimaryValue_ReturnsBelowThreshold()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.PrimaryValue = null;

            var result = service.Evaluate(obs);

            Assert.IsTrue(result.Score < 0.75f,
                $"Null PrimaryValue must drop score below 0.75 threshold; got {result.Score:F4}");
            CollectionAssert.Contains(result.Reasons, "PrimaryValueNull");
            Assert.IsFalse(result.Reasons.Contains("MissingRequired:PrimaryValue"),
                "Missing-required should NOT fire for PrimaryValue — hard-failure double-count guard failed.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// <c>PrimaryValueType = "Text"</c> fires the <c>PrimaryValueTypeText</c> penalty.
        /// </summary>
        [TestMethod]
        public void Evaluate_PrimaryValueTypeText_ReturnsBelowThreshold()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.PrimaryValueType = "Text";

            var result = service.Evaluate(obs);

            Assert.IsTrue(result.Score < 0.75f,
                $"Text PrimaryValueType must drop score below 0.75 threshold; got {result.Score:F4}");
            CollectionAssert.Contains(result.Reasons, "PrimaryValueTypeText");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null PrimaryValueType fires the <c>PrimaryValueTypeNull</c> penalty and drives
        /// the score below threshold.
        /// </summary>
        [TestMethod]
        public void Evaluate_NullPrimaryValueType_ReturnsBelowThreshold()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.PrimaryValueType = null;

            var result = service.Evaluate(obs);

            Assert.IsTrue(result.Score < 0.75f,
                $"Null PrimaryValueType must drop score below 0.75 threshold; got {result.Score:F4}");
            CollectionAssert.Contains(result.Reasons, "PrimaryValueTypeNull");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null ParameterName fires the dedicated penalty for categories where ParameterName
        /// is Required (AdverseEvent, PK, …). Drives score below threshold.
        /// </summary>
        [TestMethod]
        public void Evaluate_NullParameterName_BelowThreshold_ForAdverseEvent()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.ParameterName = null;

            var result = service.Evaluate(obs);

            Assert.IsTrue(result.Score < 0.75f,
                $"Null ParameterName (Required for AE) must drop score below 0.75; got {result.Score:F4}");
            CollectionAssert.Contains(result.Reasons, "ParameterNameNull");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null ParameterName on a TextDescriptive row — where ParameterName is Expected but
        /// not Required — does NOT fire the hard ParameterNameNull penalty. The row receives
        /// no penalty from this field.
        /// </summary>
        [TestMethod]
        public void Evaluate_NullParameterName_NoPenalty_WhenNotRequiredForCategory()
        {
            #region implementation

            var service = buildService();
            var obs = new ParsedObservation
            {
                TableCategory = "TextDescriptive",
                ParameterName = null,
                PrimaryValueType = "Text",
                RawValue = "Take with food at bedtime.",
                ParseConfidence = 1.0,
            };

            var result = service.Evaluate(obs);

            // PrimaryValueType=Text still fires, but ParameterNameNull should not.
            Assert.IsFalse(result.Reasons.Contains("ParameterNameNull"),
                "ParameterName is Expected (not Required) for TextDescriptive; penalty must be suppressed.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null TableCategory fires <c>TableCategoryNull</c>. Because Required is empty under
        /// unknown category, no per-column misses fire, keeping the test focused on the
        /// single penalty.
        /// </summary>
        [TestMethod]
        public void Evaluate_NullTableCategory_FiresTableCategoryNullPenalty()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.TableCategory = null;

            var result = service.Evaluate(obs);

            CollectionAssert.Contains(result.Reasons, "TableCategoryNull");
            Assert.IsTrue(result.Score < 0.5f,
                $"Null TableCategory (× 0.4) must drop score to ≤ 0.5; got {result.Score:F4}");

            #endregion
        }

        #endregion Hard failures

        #region Per-category Required misses

        /**************************************************************/
        /// <summary>
        /// Null <c>Unit</c> on a PK row fires <c>MissingRequired:Unit</c> since PK's contract
        /// marks Unit as Required. (For AdverseEvent it would be Expected, not Required, so
        /// the penalty would not fire there.)
        /// </summary>
        [TestMethod]
        public void Evaluate_PkWithNullUnit_FiresMissingRequiredUnit()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanPk();
            obs.Unit = null;

            var result = service.Evaluate(obs);

            CollectionAssert.Contains(result.Reasons, "MissingRequired:Unit");
            Assert.IsTrue(result.Score < 0.75f,
                $"PK with null Unit must drop below threshold; got {result.Score:F4}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// DrugInteraction Required columns include LowerBound/UpperBound/BoundType (it's the
        /// only category that requires CI bounds). Null LowerBound on a DDI row fires the
        /// corresponding Required-miss reason.
        /// </summary>
        [TestMethod]
        public void Evaluate_DrugInteractionMissingBounds_FiresRequiredMisses()
        {
            #region implementation

            var service = buildService();
            var obs = new ParsedObservation
            {
                TableCategory = "DrugInteraction",
                ParameterName = "Cmax",
                ParameterSubtype = "Rifampin",
                TreatmentArm = "Dolutegravir",
                PrimaryValue = 0.85,
                PrimaryValueType = "GeometricMeanRatio",
                LowerBound = null,
                UpperBound = null,
                BoundType = null,
                ParseConfidence = 1.0,
            };

            var result = service.Evaluate(obs);

            CollectionAssert.Contains(result.Reasons, "MissingRequired:LowerBound");
            CollectionAssert.Contains(result.Reasons, "MissingRequired:UpperBound");
            CollectionAssert.Contains(result.Reasons, "MissingRequired:BoundType");

            #endregion
        }

        #endregion Per-category Required misses

        #region Structural garbage

        /**************************************************************/
        /// <summary>
        /// Unit field containing digits (indicating a caption leak) fires the <c>BadUnit</c>
        /// penalty. Example from corpus: <c>"10 mg/kg every 12 hours"</c> leaked into the
        /// Unit column.
        /// </summary>
        [TestMethod]
        public void Evaluate_UnitContainsDigits_AppliesBadUnitPenalty()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.Unit = "10 mg/kg every 12 hours";

            var result = service.Evaluate(obs);

            CollectionAssert.Contains(result.Reasons, "BadUnit");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Unit field holding an age-range caption fragment ("Ages 27-58 yrs") fires the
        /// <c>BadUnit</c> penalty.
        /// </summary>
        [TestMethod]
        public void Evaluate_UnitHoldsAgeRange_AppliesBadUnitPenalty()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.Unit = "Ages 27-58 yrs";

            var result = service.Evaluate(obs);

            CollectionAssert.Contains(result.Reasons, "BadUnit");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// ParameterSubtype holding a statistical-format label ("Mean ± Standard Deviation")
        /// fires the <c>BadSubtype</c> penalty — one of the six corpus leak categories.
        /// </summary>
        [TestMethod]
        public void Evaluate_ParameterSubtypeIsStatFormat_AppliesBadSubtypePenalty()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.ParameterSubtype = "Mean ± Standard Deviation";

            var result = service.Evaluate(obs);

            CollectionAssert.Contains(result.Reasons, "BadSubtype");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// ParameterSubtype holding a food-status word ("FED") fires <c>BadSubtype</c>.
        /// </summary>
        [TestMethod]
        public void Evaluate_ParameterSubtypeFedStatus_AppliesBadSubtypePenalty()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.ParameterSubtype = "FED";

            var result = service.Evaluate(obs);

            CollectionAssert.Contains(result.Reasons, "BadSubtype");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Negative LowerBound on a PrimaryValueType that cannot be negative (ArithmeticMean
        /// of concentrations, Percentage, Count) fires the physics-violation penalty.
        /// </summary>
        [TestMethod]
        public void Evaluate_NegativeLowerBoundOnArithmeticMean_AppliesPenalty()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanPk();
            obs.LowerBound = -1.5;
            obs.UpperBound = 5.0;
            obs.BoundType = "95CI";
            obs.PrimaryValueType = "ArithmeticMean"; // PK default-safe

            var result = service.Evaluate(obs);

            CollectionAssert.Contains(result.Reasons, "NegativeBoundOnNonNegativeType");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Negative LowerBound on a HazardRatio (bidirectional risk measure) does NOT fire
        /// the negative-bound penalty — legitimately signed ratio types are exempt.
        /// </summary>
        [TestMethod]
        public void Evaluate_NegativeLowerBoundOnHazardRatio_NoPenalty()
        {
            #region implementation

            var service = buildService();
            var obs = new ParsedObservation
            {
                TableCategory = "Efficacy",
                ParameterName = "Overall Survival",
                TreatmentArm = "Drug X",
                PrimaryValue = 0.85,
                PrimaryValueType = "HazardRatio",
                LowerBound = -0.1,
                UpperBound = 1.8,
                BoundType = "95CI",
                ParseConfidence = 1.0,
            };

            var result = service.Evaluate(obs);

            Assert.IsFalse(result.Reasons.Contains("NegativeBoundOnNonNegativeType"),
                "HazardRatio tolerates negative bounds — penalty must be suppressed.");

            #endregion
        }

        #endregion Structural garbage

        #region Soft repair signals

        /**************************************************************/
        /// <summary>
        /// Three stacked soft-repair multipliers (PVT_MIGRATED + BOUND_TYPE_INFERRED +
        /// CAPTION_REINTERPRET at 0.9 each) produce a cumulative score of 1.0 × 0.9³ = 0.729,
        /// which falls below the 0.75 Claude-review threshold — confirming that even rows
        /// with no hard failures can accumulate enough soft signals to warrant review.
        /// </summary>
        [TestMethod]
        public void Evaluate_StacksSoftRepairs_AccumulatesToReviewThreshold()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.ValidationFlags = "PVT_MIGRATED:Percentage; BOUND_TYPE_INFERRED:95CI; CAPTION_REINTERPRET:n_pct";

            var result = service.Evaluate(obs);

            // 1.0 × 0.9 × 0.9 × 0.9 = 0.729
            Assert.IsTrue(result.Score < 0.75f,
                $"Three stacked soft repairs must fall below 0.75; got {result.Score:F4}");
            CollectionAssert.Contains(result.Reasons, "SoftRepair:PVT_MIGRATED");
            CollectionAssert.Contains(result.Reasons, "SoftRepair:BOUND_TYPE_INFERRED");
            CollectionAssert.Contains(result.Reasons, "SoftRepair:CAPTION_REINTERPRET");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// <c>PK_UNIT_SIBLING_VOTED:RESCUE_BOOST</c> applies the stricter 0.85 multiplier and
        /// suppresses the plain-sibling-voted 0.95 multiplier (they would otherwise both fire
        /// since the string contains both patterns).
        /// </summary>
        [TestMethod]
        public void Evaluate_RescueBoostSubsumesSiblingVotedPenalty()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanPk();
            obs.ValidationFlags = "PK_UNIT_SIBLING_VOTED:RESCUE_BOOST:mcg/mL";

            var result = service.Evaluate(obs);

            CollectionAssert.Contains(result.Reasons, "SoftRepair:PK_UNIT_SIBLING_VOTED:RESCUE_BOOST");
            Assert.IsFalse(result.Reasons.Contains("SoftRepair:PK_UNIT_SIBLING_VOTED"),
                "Plain sibling-voted penalty must be suppressed when RESCUE_BOOST is present.");
            Assert.AreEqual(0.85f, result.Score, 0.001f);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Plain <c>PK_UNIT_SIBLING_VOTED</c> without rescue-boost applies the lighter 0.95
        /// multiplier.
        /// </summary>
        [TestMethod]
        public void Evaluate_PlainSiblingVoted_AppliesLighterPenalty()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanPk();
            obs.ValidationFlags = "PK_UNIT_SIBLING_VOTED:mcg/mL";

            var result = service.Evaluate(obs);

            CollectionAssert.Contains(result.Reasons, "SoftRepair:PK_UNIT_SIBLING_VOTED");
            Assert.AreEqual(0.95f, result.Score, 0.001f);

            #endregion
        }

        #endregion Soft repair signals

        #region ParseConfidence floor

        /**************************************************************/
        /// <summary>
        /// A row with no specific penalties but low ParseConfidence (0.5) is floored at that
        /// value. Prevents low-confidence rows from skipping review.
        /// </summary>
        [TestMethod]
        public void Evaluate_LowParseConfidence_FloorsScore()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.ParseConfidence = 0.5;

            var result = service.Evaluate(obs);

            Assert.AreEqual(0.5f, result.Score, 0.001f,
                $"ParseConfidence floor should clamp score to 0.5; got {result.Score:F4}");
            CollectionAssert.Contains(result.Reasons, "ParseConfidenceFloor");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// When ParseConfidence is higher than the rule-computed score, the floor does NOT
        /// raise the score — it's a min, not a max.
        /// </summary>
        [TestMethod]
        public void Evaluate_HighParseConfidence_DoesNotRaiseLowScore()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.PrimaryValueType = "Text";   // × 0.3 → score = 0.3
            obs.ParseConfidence = 1.0;

            var result = service.Evaluate(obs);

            Assert.AreEqual(0.3f, result.Score, 0.001f,
                $"Rule-computed score 0.3 should stand; floor 1.0 is higher and does not apply. Got {result.Score:F4}");
            Assert.IsFalse(result.Reasons.Contains("ParseConfidenceFloor"));

            #endregion
        }

        #endregion ParseConfidence floor

        #region Reasons audit

        /**************************************************************/
        /// <summary>
        /// Every reason token that fires is present in the returned <see cref="ParseQualityScore.Reasons"/>
        /// list, allowing the caller to emit an audit-traceable
        /// <c>QC_PARSE_QUALITY:REVIEW_REASONS</c> flag.
        /// </summary>
        [TestMethod]
        public void Evaluate_ReasonsListPopulated_MatchesTriggeredRules()
        {
            #region implementation

            var service = buildService();
            var obs = buildCleanAdverseEvent();
            obs.PrimaryValue = null;         // PrimaryValueNull
            obs.Unit = "Ages 27-58 yrs";     // BadUnit
            obs.ValidationFlags = "PVT_MIGRATED:Percentage";  // SoftRepair:PVT_MIGRATED

            var result = service.Evaluate(obs);

            CollectionAssert.Contains(result.Reasons, "PrimaryValueNull");
            CollectionAssert.Contains(result.Reasons, "BadUnit");
            CollectionAssert.Contains(result.Reasons, "SoftRepair:PVT_MIGRATED");

            #endregion
        }

        #endregion Reasons audit
    }
}
