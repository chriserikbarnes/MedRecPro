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
        #region Phase 1 Pipeline Ordering Tests

        /**************************************************************/
        /// <summary>
        /// Phase 1 should run Rule 11 before Rule 10 so a bracketed-N arm that also
        /// carries a trailing percent hint keeps both corrections.
        /// </summary>
        [TestMethod]
        public async Task Phase1Pipeline_Rule11RunsBeforeRule10()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo % [N=459]");
            obs.PrimaryValueType = "Numeric";
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.AreEqual(459, result[0].ArmN);
            Assert.AreEqual("Percentage", result[0].PrimaryValueType);
            Assert.AreEqual("%", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:ARM_BRACKET_N");
            assertHasFlag(result[0], "COL_STD:ARM_STRIP_PCT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 1 should stop after the first matching arm rule while still allowing
        /// context-cleanup rules to run against the corrected observation.
        /// </summary>
        [TestMethod]
        public async Task Phase1Pipeline_FirstArmMatchStillRunsContextRules()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(N=267)", "% of Patients");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            Assert.AreEqual(267, result[0].ArmN);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_N");
            assertHasFlag(result[0], "COL_STD:CTX_WAS_DESC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 1 Pipeline Ordering Tests

        #region Phase 2 Pipeline Ordering Tests

        /**************************************************************/
        /// <summary>
        /// Phase 2 should strip inline N values before DoseRegimen triage so a
        /// PK term with an attached sample size can still route into ParameterName.
        /// </summary>
        [TestMethod]
        public async Task Phase2Pipeline_InlineNBeforeDoseRegimenTriage()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.DoseRegimen = "Cmax (N=12)";
            obs.ParameterName = null;
            obs.ParameterSubtype = null;
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(12, result[0].ArmN);
            Assert.IsNull(result[0].DoseRegimen);
            Assert.AreEqual("Cmax", result[0].ParameterName);
            Assert.IsNull(result[0].ParameterSubtype);
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:DoseRegimen");
            assertHasFlag(result[0], "COL_STD:PK_SUBPARAM_ROUTED");
            assertHasFlag(result[0], "COL_STD:PK_NAME_SUBTYPE_SWAPPED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2 should clear or route existing DoseRegimen content before
        /// ParameterName cleanup promotes a bare dose into DoseRegimen.
        /// </summary>
        [TestMethod]
        public async Task Phase2Pipeline_DoseRegimenTriageBeforeParameterNameCleanup()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.DoseRegimen = "steady state";
            obs.ParameterName = "50";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ParameterName);
            Assert.AreEqual("50", result[0].DoseRegimen);
            Assert.AreEqual("steady state", result[0].Timepoint);
            assertHasFlag(result[0], "COL_STD:TIMEPOINT_EXTRACTED");
            assertHasFlag(result[0], "COL_STD:PARAM_WAS_DOSE");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2 should extract a trailing unit from ParameterSubtype before PK
        /// canonicalization moves the PK term into ParameterName.
        /// </summary>
        [TestMethod]
        public async Task Phase2Pipeline_UnitExtractionBeforePkCanonicalization()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.ParameterName = null;
            obs.ParameterSubtype = "Cmax(mcg /mL)";
            obs.Unit = null;
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName);
            Assert.IsNull(result[0].ParameterSubtype);
            Assert.AreEqual("mcg/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:PK_SUBPARAM_UNIT_EXTRACTED");
            assertHasFlag(result[0], "COL_STD:PK_NAME_SUBTYPE_SWAPPED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2 should normalize an existing AE SOC value before dictionary
        /// resolution gets a chance to fill genuinely missing categories.
        /// </summary>
        [TestMethod]
        public async Task Phase2Pipeline_CategoryNormalizationBeforeAeDictionaryResolution()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceWithDictionaryAsync();

            var obs = createObservation("Placebo");
            obs.ParameterName = "Dyspepsia";
            obs.ParameterCategory = "gastrointestinal";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Gastrointestinal Disorders", result[0].ParameterCategory);
            assertHasFlag(result[0], "COL_STD:SOC_NORMALIZED");
            Assert.IsFalse(result[0].ValidationFlags?.Contains("DICT:SOC_RESOLVED") == true,
                "Dictionary resolution should not overwrite an existing normalized SOC.");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2 should scan for dose values after column movements, including
        /// TreatmentArm dose extraction into DoseRegimen.
        /// </summary>
        [TestMethod]
        public async Task Phase2Pipeline_DoseScanRunsAfterColumnMovements()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation(null, category: "PK");
            obs.TreatmentArm = "Pregabalin 50 mg once daily";
            obs.DoseRegimen = null;
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Pregabalin", result[0].TreatmentArm);
            Assert.AreEqual("50 mg", result[0].DoseRegimen);
            Assert.AreEqual(50m, result[0].Dose);
            Assert.AreEqual("mg", result[0].DoseUnit);
            assertHasFlag(result[0], "COL_STD:DOSE_EXTRACTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2 Pipeline Ordering Tests

        #region Edge Case Tests

        /**************************************************************/
        /// <summary>
        /// Already-correct data should pass through without modification.
        /// TreatmentArm="EVISTA", StudyContext="Kidney Studies", ArmN=2557
        /// Expected: No changes, no flags.
        /// </summary>
        [TestMethod]
        public async Task Standardize_AlreadyCorrect_NoChanges()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("EVISTA", "Kidney Studies", armN: 2557);
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("EVISTA", result[0].TreatmentArm);
            Assert.AreEqual("Kidney Studies", result[0].StudyContext);
            Assert.AreEqual(2557, result[0].ArmN);
            // Phase 1 should not fire — arm is already a drug name with correct placement
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:ARM_") == true,
                "No Phase 1 arm correction should fire for already-correct data");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null TreatmentArm and StudyContext should not throw.
        /// </summary>
        [TestMethod]
        public async Task Standardize_NullFields_NoException()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation(null, null);
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].TreatmentArm);
            // Phase 1 should not fire on null fields
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:ARM_") == true,
                "No Phase 1 arm correction should fire for null TreatmentArm");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Empty observations list should not throw.
        /// </summary>
        [TestMethod]
        public async Task Standardize_EmptyList_ReturnsEmpty()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var result = service.Standardize(new List<ParsedObservation>());

            Assert.AreEqual(0, result.Count);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Known abbreviation "AZA" should be recognized as a drug name.
        /// TreatmentArm="Some text", StudyContext="AZA"
        /// Expected: Rule 8 swap — TreatmentArm="AZA".
        /// </summary>
        [TestMethod]
        public async Task Standardize_KnownAbbreviation_RecognizedAsDrug()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Some non-drug text", "AZA");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("AZA", result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:SWAP_ARM_CTX");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Multiple observations in a single batch — each should be corrected independently.
        /// </summary>
        [TestMethod]
        public async Task Standardize_MultipleMixedObservations_EachCorrected()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var observations = new List<ParsedObservation>
            {
                createObservation("(N=267)", "Placebo"),           // Rule 1
                createObservation("%", "Dofetilide"),              // Rule 2
                createObservation("Severe", "Dosing Regimen"),     // Rule 3
                createObservation("EVISTA", "Kidney Studies"),     // Already correct
                createObservation("10 mg", "Placebo"),             // Rule 4
            };

            var result = service.Standardize(observations);

            // Rule 1
            Assert.AreEqual(267, result[0].ArmN);
            Assert.AreEqual("Placebo", result[0].TreatmentArm);

            // Rule 2
            Assert.AreEqual("Dofetilide", result[1].TreatmentArm);

            // Rule 3
            Assert.AreEqual("Severe", result[2].ParameterSubtype);

            // Already correct — no Phase 1 flag
            Assert.AreEqual("EVISTA", result[3].TreatmentArm);
            Assert.IsFalse(result[3].ValidationFlags?.Contains("COL_STD:ARM_") == true,
                "No Phase 1 arm correction should fire for already-correct EVISTA");

            // Rule 4
            Assert.AreEqual("10 mg", result[4].DoseRegimen);
            Assert.AreEqual("Placebo", result[4].TreatmentArm);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Existing ValidationFlags should be preserved and appended to, not overwritten.
        /// </summary>
        [TestMethod]
        public async Task Standardize_ExistingFlags_Appended()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(N=267)", "Placebo");
            obs.ValidationFlags = "PCT_CHECK:PASS";
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNotNull(result[0].ValidationFlags);
            Assert.IsTrue(result[0].ValidationFlags!.Contains("PCT_CHECK:PASS"),
                "Original flags should be preserved");
            Assert.IsTrue(result[0].ValidationFlags!.Contains("COL_STD:ARM_WAS_N"),
                "New flag should be appended");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// StudyContext that looks like a study name should NOT be cleared.
        /// StudyContext="Heart Study" — a legitimate study context.
        /// </summary>
        [TestMethod]
        public async Task Standardize_LegitimateStudyContext_Preserved()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Heart Study");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Heart Study", result[0].StudyContext);
            // Phase 1 should not fire — study context is legitimate
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:ARM_") == true,
                "No Phase 1 arm correction should fire for legitimate study context");
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:CTX_") == true,
                "No Phase 1 context correction should fire for legitimate study context");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Edge Case Tests
    }
}

