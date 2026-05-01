using MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for the pure-function statistics utility
    /// <see cref="RelativeRiskCalculator"/> covering Katz log-method RR + 95% CI,
    /// Haldane-Anscombe continuity correction, derived event-count conversion,
    /// dose-normalized RR, placebo-arm detection, and Document-level trial-design
    /// classification.
    /// </summary>
    /// <remarks>
    /// These tests are pure-function: no DI, no DB, no mocks. Float comparisons use
    /// a small delta (1e-6) to absorb rounding noise.
    /// </remarks>
    /// <seealso cref="RelativeRiskCalculator"/>
    [TestClass]
    public class RelativeRiskCalculatorTests
    {
        #region Constants

        /**************************************************************/
        /// <summary>Tolerance for float equality on RR/CI bounds.</summary>
        private const double Eps = 1e-6;

        #endregion Constants

        #region Compute — Standard Path

        /**************************************************************/
        /// <summary>
        /// Standard Katz computation: 20/100 vs 10/100 → RR = 2.0, with 95% CI
        /// computed via SE(logRR) = sqrt(1/a − 1/n1 + 1/c − 1/n2).
        /// </summary>
        [TestMethod]
        public void Compute_PercentageStandard_ProducesExpectedRr()
        {
            #region implementation

            // a=20 (20% of 100), n1=100; c=10 (10% of 100), n2=100
            var r = RelativeRiskCalculator.Compute(20.0, 100, 10.0, 100);

            Assert.IsNotNull(r.Rr);
            Assert.AreEqual(2.0, r.Rr!.Value, Eps, "RR should equal 2.0 for 20% vs 10%");

            // Manual: SE = sqrt(1/20 - 1/100 + 1/10 - 1/100) = sqrt(0.05 - 0.01 + 0.1 - 0.01)
            //            = sqrt(0.13) ≈ 0.360555
            // ln(2) ≈ 0.693147; CI half-width = 1.959963984540054 * 0.360555 ≈ 0.706679
            // Lower = exp(0.693147 - 0.706679) = exp(-0.013532) ≈ 0.986559
            // Upper = exp(0.693147 + 0.706679) = exp(1.399826)  ≈ 4.054054
            Assert.AreEqual(0.986559, r.RrLower!.Value, 1e-3);
            Assert.AreEqual(4.054054, r.RrUpper!.Value, 1e-2);

            // Raw event counts pass through unchanged
            Assert.AreEqual(20.0, r.EventsTreatmentRaw);
            Assert.AreEqual(10.0, r.EventsComparatorRaw);
            Assert.IsNull(r.Flags);

            #endregion
        }

        /**************************************************************/
        /// <summary>RR equals 1 when both incidences match; CI brackets 1.</summary>
        [TestMethod]
        public void Compute_IdenticalIncidence_RrEqualsOne()
        {
            #region implementation

            var r = RelativeRiskCalculator.Compute(10.0, 100, 10.0, 100);
            Assert.AreEqual(1.0, r.Rr!.Value, Eps);
            Assert.IsTrue(r.RrLower! < 1.0 && r.RrUpper! > 1.0, "CI should bracket 1");

            #endregion
        }

        /**************************************************************/
        /// <summary>RR less than 1 when treatment incidence is lower than comparator.</summary>
        [TestMethod]
        public void Compute_TreatmentSafer_RrLessThanOne()
        {
            #region implementation

            var r = RelativeRiskCalculator.Compute(5.0, 100, 20.0, 100);
            Assert.IsNotNull(r.Rr);
            Assert.AreEqual(0.25, r.Rr!.Value, Eps);

            #endregion
        }

        #endregion Compute — Standard Path

        #region Compute — Zero-cell Continuity Correction

        /**************************************************************/
        /// <summary>
        /// When treatment events are 0, Haldane-Anscombe correction adds 0.5 to both
        /// event counts and 1 to both arm Ns. The correction applies to BOTH the
        /// point estimate and the CI (per review feedback) — using adjusted values
        /// throughout. Raw counts remain in EventsTreatmentRaw / EventsComparatorRaw.
        /// </summary>
        [TestMethod]
        public void Compute_ZeroEventsTreatment_AppliesHaldaneAnscombeToPointAndCi()
        {
            #region implementation

            var r = RelativeRiskCalculator.Compute(0.0, 100, 5.0, 100);

            Assert.IsNotNull(r.Rr);
            Assert.IsNotNull(r.RrLower);
            Assert.IsNotNull(r.RrUpper);

            // Adjusted: a'=0.5, c'=5.5, n1'=101, n2'=101
            // RR = (0.5/101) / (5.5/101) = 0.5/5.5 ≈ 0.0909091
            Assert.AreEqual(0.5 / 5.5, r.Rr!.Value, Eps,
                "Point estimate should use adjusted locals when zero cell present");

            // Raw event counts preserve audit trail (NOT 0.5)
            Assert.AreEqual(0.0, r.EventsTreatmentRaw, "Raw a must remain 0 for audit");
            Assert.AreEqual(5.0, r.EventsComparatorRaw, "Raw c must remain 5 for audit");

            // ZERO_CELL_CORRECTED flag set
            Assert.AreEqual("ZERO_CELL_CORRECTED", r.Flags);

            #endregion
        }

        /**************************************************************/
        /// <summary>Symmetric: zero comparator events also triggers correction.</summary>
        [TestMethod]
        public void Compute_ZeroEventsComparator_AppliesHaldaneAnscombe()
        {
            #region implementation

            var r = RelativeRiskCalculator.Compute(5.0, 100, 0.0, 100);

            Assert.IsNotNull(r.Rr);
            // Adjusted: a'=5.5, c'=0.5, n1'=101, n2'=101
            Assert.AreEqual(5.5 / 0.5, r.Rr!.Value, Eps);
            Assert.AreEqual("ZERO_CELL_CORRECTED", r.Flags);
            Assert.AreEqual(5.0, r.EventsTreatmentRaw);
            Assert.AreEqual(0.0, r.EventsComparatorRaw);

            #endregion
        }

        #endregion Compute — Zero-cell Continuity Correction

        #region Compute — Hard Guards

        /**************************************************************/
        /// <summary>ArmN of 0 (or negative or null) yields NO_ARMN flag with all-null stats.</summary>
        [TestMethod]
        public void Compute_ZeroArmN_ReturnsNullWithNoArmnFlag()
        {
            #region implementation

            var r = RelativeRiskCalculator.Compute(5.0, 0, 10.0, 100);
            Assert.IsNull(r.Rr);
            Assert.IsNull(r.RrLower);
            Assert.IsNull(r.RrUpper);
            Assert.AreEqual("NO_ARMN", r.Flags);

            // Null armN
            var r2 = RelativeRiskCalculator.Compute(5.0, null, 10.0, 100);
            Assert.IsNull(r2.Rr);
            Assert.AreEqual("NO_ARMN", r2.Flags);

            #endregion
        }

        /**************************************************************/
        /// <summary>ComparatorN of 0 yields NO_COMPARATOR_N flag.</summary>
        [TestMethod]
        public void Compute_ZeroComparatorN_ReturnsFlag()
        {
            #region implementation

            var r = RelativeRiskCalculator.Compute(5.0, 100, 10.0, 0);
            Assert.IsNull(r.Rr);
            Assert.AreEqual("NO_COMPARATOR_N", r.Flags);

            var r2 = RelativeRiskCalculator.Compute(5.0, 100, 10.0, null);
            Assert.IsNull(r2.Rr);
            Assert.AreEqual("NO_COMPARATOR_N", r2.Flags);

            #endregion
        }

        /**************************************************************/
        /// <summary>Negative event counts trigger INVALID_EVENT_COUNT.</summary>
        [TestMethod]
        public void Compute_NegativeEvents_ReturnsInvalidEventCountFlag()
        {
            #region implementation

            var r = RelativeRiskCalculator.Compute(-1.0, 100, 5.0, 100);
            Assert.IsNull(r.Rr);
            Assert.AreEqual("INVALID_EVENT_COUNT", r.Flags);

            #endregion
        }

        /**************************************************************/
        /// <summary>Events exceeding ArmN trigger EVENTS_EXCEED_ARMN.</summary>
        [TestMethod]
        public void Compute_EventsExceedArmN_ReturnsExceedFlag()
        {
            #region implementation

            var r = RelativeRiskCalculator.Compute(150.0, 100, 5.0, 100);
            Assert.IsNull(r.Rr);
            Assert.AreEqual("EVENTS_EXCEED_ARMN", r.Flags);

            #endregion
        }

        #endregion Compute — Hard Guards

        #region DeriveEventCount

        /**************************************************************/
        /// <summary>Percentage path with ArmN: events = ArmN × pv / 100.</summary>
        [TestMethod]
        public void DeriveEventCount_PercentageWithArmN_ComputesEvents()
        {
            #region implementation

            var (events, flag) = RelativeRiskCalculator.DeriveEventCount(14.0, "Percentage", 188);
            Assert.IsNotNull(events);
            Assert.AreEqual(188 * 14.0 / 100.0, events!.Value, Eps);
            Assert.IsNull(flag);

            #endregion
        }

        /**************************************************************/
        /// <summary>Percentage with missing ArmN cannot derive an event count → NO_ARMN.</summary>
        [TestMethod]
        public void DeriveEventCount_PercentageMissingArmN_ReturnsNoArmnFlag()
        {
            #region implementation

            var (events, flag) = RelativeRiskCalculator.DeriveEventCount(14.0, "Percentage", null);
            Assert.IsNull(events);
            Assert.AreEqual("NO_ARMN", flag);

            #endregion
        }

        /**************************************************************/
        /// <summary>Percentage greater than 100 is rejected.</summary>
        [TestMethod]
        public void DeriveEventCount_PercentageOver100_ReturnsOutOfRangeFlag()
        {
            #region implementation

            var (events, flag) = RelativeRiskCalculator.DeriveEventCount(120.0, "Percentage", 100);
            Assert.IsNull(events);
            Assert.AreEqual("PERCENT_OUT_OF_RANGE", flag);

            #endregion
        }

        /**************************************************************/
        /// <summary>Canonical Count value type passes through directly.</summary>
        [TestMethod]
        public void DeriveEventCount_Count_PassthroughDirect()
        {
            #region implementation

            var (events, flag) = RelativeRiskCalculator.DeriveEventCount(7.0, "Count", 100);
            Assert.AreEqual(7.0, events);
            Assert.IsNull(flag);

            #endregion
        }

        /**************************************************************/
        /// <summary>Bare "Numeric" type is uncomparable — does not assume count semantics.</summary>
        [TestMethod]
        public void DeriveEventCount_Numeric_ReturnsUncomparableFlag()
        {
            #region implementation

            var (events, flag) = RelativeRiskCalculator.DeriveEventCount(7.0, "Numeric", 100);
            Assert.IsNull(events);
            Assert.AreEqual("UNCOMPARABLE_VALUE_TYPE", flag);

            #endregion
        }

        /**************************************************************/
        /// <summary>Mean and similar non-count types are uncomparable.</summary>
        [TestMethod]
        public void DeriveEventCount_Mean_ReturnsUncomparableFlag()
        {
            #region implementation

            var (events, flag) = RelativeRiskCalculator.DeriveEventCount(2.21, "Mean", 100);
            Assert.IsNull(events);
            Assert.AreEqual("UNCOMPARABLE_VALUE_TYPE", flag);

            #endregion
        }

        /**************************************************************/
        /// <summary>Negative PrimaryValue rejected.</summary>
        [TestMethod]
        public void DeriveEventCount_NegativeValue_ReturnsInvalidFlag()
        {
            #region implementation

            var (events, flag) = RelativeRiskCalculator.DeriveEventCount(-1.0, "Percentage", 100);
            Assert.IsNull(events);
            Assert.AreEqual("INVALID_EVENT_COUNT", flag);

            #endregion
        }

        /**************************************************************/
        /// <summary>Null PrimaryValue rejected.</summary>
        [TestMethod]
        public void DeriveEventCount_NullPrimaryValue_ReturnsInvalidFlag()
        {
            #region implementation

            var (events, flag) = RelativeRiskCalculator.DeriveEventCount(null, "Percentage", 100);
            Assert.IsNull(events);
            Assert.AreEqual("INVALID_EVENT_COUNT", flag);

            #endregion
        }

        #endregion DeriveEventCount

        #region ComputeDnrr

        /**************************************************************/
        /// <summary>
        /// Standard log-linear DNRR: with RR=2.0 and dose ratio 2 (rowDose=100, ref=50),
        /// logDNRR = ln(2)/ln(2) = 1 → DNRR = e ≈ 2.71828.
        /// Bounds derived from RR CI bounds the same way.
        /// </summary>
        [TestMethod]
        public void ComputeDnrr_StandardLogLinear_ComputesCorrectly()
        {
            #region implementation

            var rr = new RelativeRiskCalculator.RrResult(
                Rr: 2.0, RrLower: 1.0, RrUpper: 4.0,
                EventsTreatmentRaw: 20.0, EventsComparatorRaw: 10.0, Flags: null);

            var d = RelativeRiskCalculator.ComputeDnrr(rr, 100m, "mg", 50m, "mg");

            Assert.IsNotNull(d.Dnrr);
            Assert.AreEqual(Math.E, d.Dnrr!.Value, 1e-3);
            // Lower bound: exp(ln(1)/ln(2)) = exp(0) = 1
            Assert.AreEqual(1.0, d.DnrrLower!.Value, 1e-3);
            // Upper bound: exp(ln(4)/ln(2)) = exp(2) = e^2 ≈ 7.389
            Assert.AreEqual(Math.Exp(2), d.DnrrUpper!.Value, 1e-3);
            Assert.IsNull(d.Flags);

            #endregion
        }

        /**************************************************************/
        /// <summary>Row at the reference dose: ln(1)=0 denominator, DNRR is undefined.</summary>
        [TestMethod]
        public void ComputeDnrr_RowDoseEqualsReference_ReturnsIsReferenceFlag()
        {
            #region implementation

            var rr = new RelativeRiskCalculator.RrResult(2.0, 1.0, 4.0, 20.0, 10.0, null);
            var d = RelativeRiskCalculator.ComputeDnrr(rr, 50m, "mg", 50m, "mg");

            Assert.IsNull(d.Dnrr);
            Assert.AreEqual("IS_REFERENCE_DOSE", d.Flags);

            #endregion
        }

        /**************************************************************/
        /// <summary>Reference dose undefined yields NO_DOSE_RANGE.</summary>
        [TestMethod]
        public void ComputeDnrr_RefDoseUndefined_ReturnsNoDoseRangeFlag()
        {
            #region implementation

            var rr = new RelativeRiskCalculator.RrResult(2.0, 1.0, 4.0, 20.0, 10.0, null);
            var d = RelativeRiskCalculator.ComputeDnrr(rr, 100m, "mg", null, null);

            Assert.IsNull(d.Dnrr);
            Assert.AreEqual("NO_DOSE_RANGE", d.Flags);

            #endregion
        }

        /**************************************************************/
        /// <summary>Differing dose units yield DOSE_UNIT_MISMATCH (per review feedback).</summary>
        [TestMethod]
        public void ComputeDnrr_DoseUnitMismatch_ReturnsMismatchFlag()
        {
            #region implementation

            var rr = new RelativeRiskCalculator.RrResult(2.0, 1.0, 4.0, 20.0, 10.0, null);
            var d = RelativeRiskCalculator.ComputeDnrr(rr, 100m, "mg", 50m, "mg/kg");

            Assert.IsNull(d.Dnrr);
            Assert.AreEqual("DOSE_UNIT_MISMATCH", d.Flags);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null RR propagates without adding a flag (the cause is in the upstream
        /// <see cref="RelativeRiskCalculator.RrResult"/>).
        /// </summary>
        [TestMethod]
        public void ComputeDnrr_NullRr_PropagatesNullNoFlag()
        {
            #region implementation

            var rr = new RelativeRiskCalculator.RrResult(null, null, null, null, null, "NO_ARMN");
            var d = RelativeRiskCalculator.ComputeDnrr(rr, 100m, "mg", 50m, "mg");

            Assert.IsNull(d.Dnrr);
            Assert.IsNull(d.DnrrLower);
            Assert.IsNull(d.DnrrUpper);
            Assert.IsNull(d.Flags);

            #endregion
        }

        #endregion ComputeDnrr

        #region IsPlaceboArm

        /**************************************************************/
        /// <summary>"Placebo" matches placebo pattern.</summary>
        [TestMethod]
        public void IsPlaceboArm_PlaceboName_ReturnsTrue()
        {
            #region implementation

            Assert.IsTrue(RelativeRiskCalculator.IsPlaceboArm("Placebo", null));
            Assert.IsTrue(RelativeRiskCalculator.IsPlaceboArm("placebo", null));
            Assert.IsTrue(RelativeRiskCalculator.IsPlaceboArm("PLACEBO 50mg", null));

            #endregion
        }

        /**************************************************************/
        /// <summary>"Sham" matches placebo pattern.</summary>
        [TestMethod]
        public void IsPlaceboArm_ShamName_ReturnsTrue()
        {
            Assert.IsTrue(RelativeRiskCalculator.IsPlaceboArm("Sham", null));
        }

        /**************************************************************/
        /// <summary>"Vehicle" matches placebo pattern.</summary>
        [TestMethod]
        public void IsPlaceboArm_VehicleName_ReturnsTrue()
        {
            Assert.IsTrue(RelativeRiskCalculator.IsPlaceboArm("Vehicle", null));
        }

        /**************************************************************/
        /// <summary>Dose=0 marks the arm as placebo even with a non-placebo name.</summary>
        [TestMethod]
        public void IsPlaceboArm_DoseZero_ReturnsTrue()
        {
            Assert.IsTrue(RelativeRiskCalculator.IsPlaceboArm("Drug A", 0m));
        }

        /**************************************************************/
        /// <summary>Drug arm with non-zero dose is not placebo.</summary>
        [TestMethod]
        public void IsPlaceboArm_DrugName_ReturnsFalse()
        {
            Assert.IsFalse(RelativeRiskCalculator.IsPlaceboArm("Drug A", 50m));
            Assert.IsFalse(RelativeRiskCalculator.IsPlaceboArm("Active comparator", 100m));
        }

        /**************************************************************/
        /// <summary>Null name + null dose defaults to non-placebo.</summary>
        [TestMethod]
        public void IsPlaceboArm_NullName_NoDose_ReturnsFalse()
        {
            Assert.IsFalse(RelativeRiskCalculator.IsPlaceboArm(null, null));
            Assert.IsFalse(RelativeRiskCalculator.IsPlaceboArm("", null));
        }

        #endregion IsPlaceboArm

        #region ClassifyTrialDesign

        /**************************************************************/
        /// <summary>Drug + Placebo only → PLACEBO_ONLY, IsPlaceboControlled=true.</summary>
        [TestMethod]
        public void ClassifyTrialDesign_DrugPlusPlacebo_ReturnsPlaceboOnlyTrue()
        {
            #region implementation

            var arms = new[]
            {
                new RelativeRiskCalculator.ArmInfo("Drug A 50mg", 50m, "mg"),
                new RelativeRiskCalculator.ArmInfo("Placebo", 0m, null)
            };

            var result = RelativeRiskCalculator.ClassifyTrialDesign(arms);

            Assert.IsTrue(result.IsPlaceboControlled);
            Assert.AreEqual(RelativeRiskCalculator.TrialDesignKind.PLACEBO_ONLY, result.Kind);
            Assert.IsNull(result.Flag);

            #endregion
        }

        /**************************************************************/
        /// <summary>Multiple doses of one drug + placebo → STEPPED_DOSE_PLUS_PLACEBO, true.</summary>
        [TestMethod]
        public void ClassifyTrialDesign_SteppedDosePlusPlacebo_ReturnsTrue()
        {
            #region implementation

            var arms = new[]
            {
                new RelativeRiskCalculator.ArmInfo("Drug A 50mg", 50m, "mg"),
                new RelativeRiskCalculator.ArmInfo("Drug A 100mg", 100m, "mg"),
                new RelativeRiskCalculator.ArmInfo("Placebo", 0m, null)
            };

            var result = RelativeRiskCalculator.ClassifyTrialDesign(arms);

            Assert.IsTrue(result.IsPlaceboControlled);
            Assert.AreEqual(RelativeRiskCalculator.TrialDesignKind.STEPPED_DOSE_PLUS_PLACEBO, result.Kind);

            #endregion
        }

        /**************************************************************/
        /// <summary>Drug + active comparator + placebo (distinct roots) → false per user spec.</summary>
        [TestMethod]
        public void ClassifyTrialDesign_PlaceboPlusActive_ReturnsFalse()
        {
            #region implementation

            var arms = new[]
            {
                new RelativeRiskCalculator.ArmInfo("Drug A 50mg", 50m, "mg"),
                new RelativeRiskCalculator.ArmInfo("Active Comparator 100mg", 100m, "mg"),
                new RelativeRiskCalculator.ArmInfo("Placebo", 0m, null)
            };

            var result = RelativeRiskCalculator.ClassifyTrialDesign(arms);

            Assert.IsFalse(result.IsPlaceboControlled);
            Assert.AreEqual(RelativeRiskCalculator.TrialDesignKind.PLACEBO_PLUS_ACTIVE, result.Kind);

            #endregion
        }

        /**************************************************************/
        /// <summary>Stepped-dose monotherapy without placebo → false.</summary>
        [TestMethod]
        public void ClassifyTrialDesign_SteppedDoseMonotherapy_ReturnsFalse()
        {
            #region implementation

            var arms = new[]
            {
                new RelativeRiskCalculator.ArmInfo("Drug A 50mg", 50m, "mg"),
                new RelativeRiskCalculator.ArmInfo("Drug A 100mg", 100m, "mg")
            };

            var result = RelativeRiskCalculator.ClassifyTrialDesign(arms);

            Assert.IsFalse(result.IsPlaceboControlled);
            Assert.AreEqual(RelativeRiskCalculator.TrialDesignKind.STEPPED_DOSE_MONOTHERAPY, result.Kind);

            #endregion
        }

        /**************************************************************/
        /// <summary>Single arm → SINGLE_ARM, false.</summary>
        [TestMethod]
        public void ClassifyTrialDesign_SingleArm_ReturnsFalse()
        {
            #region implementation

            var arms = new[]
            {
                new RelativeRiskCalculator.ArmInfo("Drug A 50mg", 50m, "mg")
            };

            var result = RelativeRiskCalculator.ClassifyTrialDesign(arms);

            Assert.IsFalse(result.IsPlaceboControlled);
            Assert.AreEqual(RelativeRiskCalculator.TrialDesignKind.SINGLE_ARM, result.Kind);

            #endregion
        }

        /**************************************************************/
        /// <summary>Two distinct drugs without placebo → ACTIVE_ONLY, false.</summary>
        [TestMethod]
        public void ClassifyTrialDesign_ActiveOnly_ReturnsFalse()
        {
            #region implementation

            var arms = new[]
            {
                new RelativeRiskCalculator.ArmInfo("Drug A 50mg", 50m, "mg"),
                new RelativeRiskCalculator.ArmInfo("Drug B 100mg", 100m, "mg")
            };

            var result = RelativeRiskCalculator.ClassifyTrialDesign(arms);

            Assert.IsFalse(result.IsPlaceboControlled);
            Assert.AreEqual(RelativeRiskCalculator.TrialDesignKind.ACTIVE_ONLY, result.Kind);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Arms with names that strip to nothing after dose-token removal yield
        /// AMBIGUOUS_TRIAL_DESIGN with the conservative IsPlaceboControlled=false.
        /// </summary>
        [TestMethod]
        public void ClassifyTrialDesign_AmbiguousNoUsableRoot_ReturnsAmbiguousFlag()
        {
            #region implementation

            // Arm names that consist only of dose tokens — nothing left after stripping
            var arms = new[]
            {
                new RelativeRiskCalculator.ArmInfo("50 mg", 50m, "mg"),
                new RelativeRiskCalculator.ArmInfo("100 mg", 100m, "mg"),
                new RelativeRiskCalculator.ArmInfo("Placebo", 0m, null)
            };

            var result = RelativeRiskCalculator.ClassifyTrialDesign(arms);

            Assert.IsFalse(result.IsPlaceboControlled);
            Assert.AreEqual(RelativeRiskCalculator.TrialDesignKind.AMBIGUOUS, result.Kind);
            Assert.AreEqual("AMBIGUOUS_TRIAL_DESIGN", result.Flag);

            #endregion
        }

        /**************************************************************/
        /// <summary>Empty arm collection → SINGLE_ARM, false (defensive).</summary>
        [TestMethod]
        public void ClassifyTrialDesign_EmptyCollection_ReturnsSingleArmFalse()
        {
            #region implementation

            var result = RelativeRiskCalculator.ClassifyTrialDesign(Array.Empty<RelativeRiskCalculator.ArmInfo>());
            Assert.IsFalse(result.IsPlaceboControlled);
            Assert.AreEqual(RelativeRiskCalculator.TrialDesignKind.SINGLE_ARM, result.Kind);

            #endregion
        }

        #endregion ClassifyTrialDesign
    }
}
