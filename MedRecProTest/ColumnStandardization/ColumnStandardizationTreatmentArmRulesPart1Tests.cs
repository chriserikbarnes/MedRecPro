using System.Globalization;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ImportDbContext = MedRecProImportClass.Data.ApplicationDbContext;
using static MedRecProTest.ColumnStandardizationTestFixture;

namespace MedRecProTest
{
    public partial class ColumnStandardizationServiceTests
    {
        #region Rule 1 Tests — TreatmentArm is N= Value

        /**************************************************************/
        /// <summary>
        /// Pattern 2: TreatmentArm="(N=267)", StudyContext="Placebo"
        /// Expected: ArmN=267, TreatmentArm="Placebo", StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule1_ArmIsParenthesizedN_MovesToArmN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(N=267)", "Placebo");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(267, result[0].ArmN);
            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 2 variant: TreatmentArm="(n=99)", StudyContext="Omeprazole 20 mg a.m."
        /// Expected: ArmN=99, TreatmentArm recovered from StudyContext.
        /// </summary>
        [TestMethod]
        public async Task Rule1_ArmIsLowercaseN_MovesToArmN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(n=99)", "Omeprazole");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(99, result[0].ArmN);
            Assert.AreEqual("Omeprazole", result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 2 variant: TreatmentArm="N=677", StudyContext="Dofetilide"
        /// (no parentheses). Expected: ArmN=677, TreatmentArm="Dofetilide".
        /// </summary>
        [TestMethod]
        public async Task Rule1_ArmIsBareNEquals_MovesToArmN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("N=677", "Dofetilide");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(677, result[0].ArmN);
            Assert.AreEqual("Dofetilide", result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 2 with non-drug StudyContext: TreatmentArm="(N=267)", StudyContext="Some Unknown"
        /// Expected: ArmN=267, TreatmentArm cleared (no recovery possible).
        /// </summary>
        [TestMethod]
        public async Task Rule1_ArmIsN_CtxNotDrug_ArmCleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(N=267)", "Some Unknown Context");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(267, result[0].ArmN);
            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 1 Tests

        #region Rule 2 Tests — TreatmentArm is Format Hint

        /**************************************************************/
        /// <summary>
        /// Pattern 3: TreatmentArm="%", StudyContext="Dofetilide"
        /// Expected: TreatmentArm="Dofetilide", StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule2_ArmIsPercent_RecoveredFromCtx()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("%", "Dofetilide");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Dofetilide", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_FMT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 3 variant: TreatmentArm="#", StudyContext is not a drug.
        /// Expected: TreatmentArm cleared, flag set.
        /// </summary>
        [TestMethod]
        public async Task Rule2_ArmIsHash_CtxNotDrug_ArmCleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("#", "Target Topiramate Tablets Dosage (mg per day)");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_FMT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 3 variant: TreatmentArm="n(%)", StudyContext="Placebo".
        /// Expected: TreatmentArm="Placebo".
        /// </summary>
        [TestMethod]
        public async Task Rule2_ArmIsNPct_RecoveredFromCtx()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("n(%)", "Placebo");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_FMT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 2 Tests

        #region Rule 3 Tests — TreatmentArm is Severity Grade

        /**************************************************************/
        /// <summary>
        /// Pattern 4: TreatmentArm="Severe", StudyContext="Dosing Regimen"
        /// Expected: ParameterSubtype="Severe", TreatmentArm cleared (no drug in ctx).
        /// </summary>
        [TestMethod]
        public async Task Rule3_ArmIsSevere_MovedToSubtype()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Severe", "Dosing Regimen");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Severe", result[0].ParameterSubtype);
            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_SEVERITY");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 4 variant: TreatmentArm="Grades 3/4", StudyContext="% of Patients"
        /// Expected: ParameterSubtype="Grades 3/4", TreatmentArm cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule3_ArmIsGrades_MovedToSubtype()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Grades 3/4", "% of Patients");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Grades 3/4", result[0].ParameterSubtype);
            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_SEVERITY");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 4 variant: TreatmentArm="Grades 1–4" (en-dash).
        /// Expected: ParameterSubtype="Grades 1–4".
        /// </summary>
        [TestMethod]
        public async Task Rule3_ArmIsGradesEnDash_MovedToSubtype()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Grades 1\u20134", "% of Patients");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Grades 1\u20134", result[0].ParameterSubtype);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_SEVERITY");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 4: TreatmentArm="Total", StudyContext="Dosing Regimen"
        /// Expected: ParameterSubtype="Total".
        /// </summary>
        [TestMethod]
        public async Task Rule3_ArmIsTotal_MovedToSubtype()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Total", "Dosing Regimen");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Total", result[0].ParameterSubtype);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_SEVERITY");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 3 Tests

        #region Rule 4 Tests — TreatmentArm is Pure Dose

        /**************************************************************/
        /// <summary>
        /// Pattern 1: TreatmentArm="10 mg", StudyContext="Placebo"
        /// Expected: DoseRegimen="10 mg", TreatmentArm="Placebo".
        /// </summary>
        [TestMethod]
        public async Task Rule4_ArmIsDose_MovedToDoseRegimen()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("10 mg", "Placebo");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("10 mg", result[0].DoseRegimen);
            Assert.AreEqual(10m, result[0].Dose, "Dose extracted from DoseRegimen after arm-to-dose swap");
            Assert.AreEqual("mg", result[0].DoseUnit);
            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 1 variant: TreatmentArm="500 mcg BID", StudyContext="Dofetilide Capsules Dose"
        /// Expected: DoseRegimen="500 mcg BID", TreatmentArm="Dofetilide Capsules" (from dose descriptor).
        /// </summary>
        [TestMethod]
        public async Task Rule4_ArmIsDose_CtxIsDoseDescriptor_ExtractsDrug()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("500 mcg BID", "Dofetilide Capsules Dose");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("500 mcg BID", result[0].DoseRegimen);
            // "Dofetilide Capsules" extracted from dose descriptor; "Dofetilide" is a known drug
            Assert.IsNotNull(result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 1 variant: TreatmentArm="1000 mg Once Daily", StudyContext="Placebo"
        /// Expected: DoseRegimen="1000 mg Once Daily", TreatmentArm="Placebo".
        /// </summary>
        [TestMethod]
        public async Task Rule4_ArmIsDose_CtxIsDrug_SwapsCorrectly()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("1000 mg Once Daily", "Placebo");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("1000 mg Once Daily", result[0].DoseRegimen);
            Assert.AreEqual(1000m, result[0].Dose, "Dose extracted from DoseRegimen after arm-to-dose swap");
            Assert.AreEqual("mg/d", result[0].DoseUnit, "Frequency promotion: 'mg' + 'Once Daily' → 'mg/d'");
            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 1: TreatmentArm="30 mg q12h subcutaneously", StudyContext="Dosing Regimen"
        /// Expected: DoseRegimen populated, arm recovered from ProductTitle.
        /// </summary>
        [TestMethod]
        public async Task Rule4_ArmIsDoseWithSchedule_MovedToDoseRegimen()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("30 mg q12h subcutaneously", "Dosing Regimen",
                productTitle: "Enoxaparin");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("30 mg q12h subcutaneously", result[0].DoseRegimen);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 4 Tests

        #region Rule 5 Tests — TreatmentArm is Bare Number

        /**************************************************************/
        /// <summary>
        /// Pattern 8: TreatmentArm="200", StudyContext="Target Topiramate Tablets Dosage (mg/day)"
        /// Expected: DoseRegimen="200 mg/day", TreatmentArm="Topiramate Tablets" or "Topiramate".
        /// </summary>
        [TestMethod]
        public async Task Rule5_ArmIsBareNumber_CtxIsDoseDescriptor_Reconstructs()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("200", "Target Topiramate Tablets Dosage (mg/day)");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("200 mg/day", result[0].DoseRegimen);
            Assert.IsNotNull(result[0].TreatmentArm);
            // Should extract "Topiramate Tablets" or similar
            Assert.IsTrue(result[0].TreatmentArm!.Contains("Topiramate"),
                $"Expected TreatmentArm to contain 'Topiramate' but was '{result[0].TreatmentArm}'");
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_BARE_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 8 variant: TreatmentArm="600", StudyContext="Target Topiramate Tablets Dosage (mg per day)"
        /// Expected: DoseRegimen reconstructed, drug extracted.
        /// </summary>
        [TestMethod]
        public async Task Rule5_ArmIsBareNumber600_Reconstructs()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("600", "Target Topiramate Tablets Dosage (mg per day)");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNotNull(result[0].DoseRegimen);
            Assert.IsTrue(result[0].DoseRegimen!.StartsWith("600"),
                $"Expected DoseRegimen to start with '600' but was '{result[0].DoseRegimen}'");
            assertHasFlag(result[0], "COL_STD:ARM_WAS_BARE_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Bare number WITHOUT a dose descriptor context should NOT trigger Rule 5.
        /// TreatmentArm="200", StudyContext="Heart Study" — no correction expected
        /// (200 is ambiguous without a dose descriptor).
        /// </summary>
        [TestMethod]
        public async Task Rule5_ArmIsBareNumber_CtxIsStudy_NoCorrection()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("200", "Heart Study");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            // No Phase 1 rule matches: bare number without dose descriptor context
            Assert.AreEqual("200", result[0].TreatmentArm);
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:ARM_") == true,
                "No Phase 1 arm correction should fire for bare number without dose descriptor");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 5 Tests

        #region Rule 6 Tests — TreatmentArm is Drug+Dose Combined

        /**************************************************************/
        /// <summary>
        /// Pattern 5: TreatmentArm="Mycophenolate Mofetil 2g/day"
        /// Expected: TreatmentArm="Mycophenolate Mofetil", DoseRegimen="2g/day".
        /// </summary>
        [TestMethod]
        public async Task Rule6_ArmIsDrugPlusDose_Splits()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Mycophenolate Mofetil 2g/day", "Kidney Studies", category: "EFFICACY");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Mycophenolate Mofetil", result[0].TreatmentArm);
            Assert.AreEqual("2g/day", result[0].DoseRegimen);
            Assert.AreEqual(2m, result[0].Dose, "Dose extracted from split DoseRegimen");
            Assert.AreEqual("g", result[0].DoseUnit);
            Assert.AreEqual("Kidney Studies", result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:SPLIT_DRUG_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 5: DoseRegimen already populated — should NOT overwrite.
        /// TreatmentArm="Mycophenolate Mofetil 2g/day", DoseRegimen="existing dose"
        /// Expected: No change (preserves existing DoseRegimen).
        /// </summary>
        [TestMethod]
        public async Task Rule6_ArmIsDrugPlusDose_DoseExists_NoSplit()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Mycophenolate Mofetil 2g/day", "Kidney Studies",
                doseRegimen: "existing dose",
                category: "EFFICACY");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Should not split because DoseRegimen already populated
            Assert.AreEqual("Mycophenolate Mofetil 2g/day", result[0].TreatmentArm);
            Assert.AreEqual("existing dose", result[0].DoseRegimen);
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:SPLIT_DRUG_DOSE") == true,
                "Should not split when DoseRegimen already populated");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 6 Tests

        #region Phase 3.5 Tests - AE Percent Dose Leakage

        /**************************************************************/
        /// <summary>
        /// Verifies that AE caption/context percentages remain incidence context
        /// and do not populate <see cref="ParsedObservation.DoseUnit"/>.
        /// </summary>
        /// <seealso cref="DoseExtractor.ScanAllColumnsForDose"/>
        [TestMethod]
        public async Task Phase35_AeStudyContextPercent_DoesNotPopulateDoseUnit()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation(
                "VARITHENA",
                "Table 1: Treatment-emergent adverse reactions (3% more on VARITHENA 1% than on placebo) through Week 8");
            obs.Unit = "%";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Dose,
                "AE StudyContext percentages should not populate Dose.");
            Assert.IsNull(result[0].DoseUnit,
                "AE StudyContext percentages should not populate DoseUnit.");
            Assert.AreEqual("%", result[0].Unit,
                "AE incidence Unit should remain available for percentage values.");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that chemotherapy shorthand such as <c>5-FU/LV</c> is treated
        /// as regimen text rather than a synthetic percent dose.
        /// </summary>
        /// <seealso cref="DoseExtractor.ScanAllColumnsForDose"/>
        [TestMethod]
        public async Task Phase35_AeFiveFuLvArm_DoesNotPopulatePercentDose()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("5-FU/LV", "Combination regimen");
            obs.Unit = "%";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("5-FU/LV", result[0].TreatmentArm);
            Assert.IsNull(result[0].Dose,
                "5-FU/LV should not create a synthetic Dose=5.");
            Assert.IsNull(result[0].DoseUnit,
                "5-FU/LV should not create a synthetic percent DoseUnit.");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that AE parameter or category percentage labels do not drive
        /// dose extraction.
        /// </summary>
        /// <seealso cref="DoseExtractor.ScanAllColumnsForDose"/>
        [TestMethod]
        public async Task Phase35_AeParameterPercentLabel_DoesNotPopulateDoseUnit()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Dofetilide");
            obs.ParameterName = "Adverse Reactions (%)";
            obs.ParameterCategory = "Adverse Reactions (%)";
            obs.Unit = "%";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Dose);
            Assert.IsNull(result[0].DoseUnit);
            Assert.AreEqual("%", result[0].Unit);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that placebo rows in AE tables do not inherit percent dose
        /// metadata from non-placebo rows.
        /// </summary>
        /// <seealso cref="DoseExtractor.BackfillPlaceboArms"/>
        [TestMethod]
        public async Task Phase35_AePlaceboBackfill_SkipsPercentDoseUnit()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var active = createObservation("VARITHENA");
            active.TextTableID = 40777;
            active.Dose = 1.0m;
            active.DoseUnit = "%";
            active.Unit = "%";

            var placebo = createObservation("Placebo");
            placebo.TextTableID = 40777;
            placebo.Unit = "%";

            var result = service.Standardize(new List<ParsedObservation> { active, placebo });
            var placeboResult = result.Single(o => string.Equals(o.TreatmentArm, "Placebo", StringComparison.OrdinalIgnoreCase));

            Assert.IsNull(placeboResult.Dose,
                "AE placebo rows should not receive Dose=0 when the inherited unit would be percent.");
            Assert.IsNull(placeboResult.DoseUnit,
                "AE placebo rows should not inherit DoseUnit=%.");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that Vivelle AE arm labels with spaced daily units retain the
        /// original arm text while downstream dose fields are populated.
        /// </summary>
        /// <seealso cref="DoseExtractor.ScanAllColumnsForDose"/>
        [TestMethod]
        public async Task Phase35_AeVivelleArmDose_PopulatesDoseFieldsAndPreservesArm()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Vivelle 0.025 mg/ day\u2020");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Vivelle 0.025 mg/ day\u2020", result[0].TreatmentArm);
            Assert.AreEqual(0.025m, result[0].Dose);
            Assert.AreEqual("mg/d", result[0].DoseUnit);
            Assert.AreEqual("0.025 mg/day", result[0].DoseRegimen);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that long-form three-times-daily AE arm labels retain
        /// frequency in DoseRegimen without collapsing the unit to <c>mg/d</c>.
        /// </summary>
        /// <seealso cref="DoseExtractor.ScanAllColumnsForDose"/>
        [TestMethod]
        public async Task Phase35_AeNatestoThreeTimesDaily_PopulatesDoseRegimenAndKeepsMg()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Natesto (11 mg of Testosterone) Three Times Daily");
            obs.Dose = 11m;
            obs.DoseUnit = "mg/d";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Natesto (11 mg of Testosterone) Three Times Daily", result[0].TreatmentArm);
            Assert.AreEqual(11m, result[0].Dose);
            Assert.AreEqual("mg", result[0].DoseUnit);
            Assert.AreEqual("11 mg of Testosterone Three Times Daily", result[0].DoseRegimen);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that shorthand TID AE arm labels populate DoseRegimen and
        /// keep the simple dose unit.
        /// </summary>
        /// <seealso cref="DoseExtractor.ScanAllColumnsForDose"/>
        [TestMethod]
        public async Task Phase35_AeNatestoTid_PopulatesDoseRegimenAndKeepsMg()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Natesto 11 mg TID");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Natesto 11 mg TID", result[0].TreatmentArm);
            Assert.AreEqual(11m, result[0].Dose);
            Assert.AreEqual("mg", result[0].DoseUnit);
            Assert.AreEqual("11 mg TID", result[0].DoseRegimen);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that once-daily AE arm dose extraction still promotes simple
        /// units to daily form.
        /// </summary>
        /// <seealso cref="DoseExtractor.ScanAllColumnsForDose"/>
        [TestMethod]
        public async Task Phase35_AeOnceDailyArmDose_StillPromotesDailyUnit()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Natesto 11 mg once daily");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Natesto 11 mg once daily", result[0].TreatmentArm);
            Assert.AreEqual(11m, result[0].Dose);
            Assert.AreEqual("mg/d", result[0].DoseUnit);
            Assert.AreEqual("11 mg once daily", result[0].DoseRegimen);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that non-AE dose extraction still recognizes the dosing units
        /// protected by recent extractor work.
        /// </summary>
        /// <param name="input">Dose text to parse.</param>
        /// <param name="expectedDose">Expected decimal dose value.</param>
        /// <param name="expectedUnit">Expected dose unit.</param>
        /// <seealso cref="DoseExtractor.Extract"/>
        [DataTestMethod]
        [DataRow("2g/day", "2", "g")]
        [DataRow("5 mg/mL", "5", "mg/mL")]
        [DataRow("10 mg/m^2", "10", "mg/m^2")]
        [DataRow("8 g/dL", "8", "g/dL")]
        [DataRow("1.5 mg/dL", "1.5", "mg/dL")]
        [DataRow("250 mg/5 mL", "250", "mg/5 mL")]
        public void Phase35_DoseExtractor_NonAeUnitsStillExtract(
            string input,
            string expectedDose,
            string expectedUnit)
        {
            #region implementation

            var (dose, doseUnit) = DoseExtractor.Extract(input);

            Assert.AreEqual(decimal.Parse(expectedDose, CultureInfo.InvariantCulture), dose);
            Assert.AreEqual(expectedUnit, doseUnit);

            #endregion
        }

        #endregion Phase 3.5 Tests - AE Percent Dose Leakage
    }
}

