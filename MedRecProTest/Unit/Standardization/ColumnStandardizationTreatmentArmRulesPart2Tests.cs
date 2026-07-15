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
using static MedRecProTest.Unit.Standardization.ColumnStandardizationTestFixture;

namespace MedRecProTest.Unit.Standardization
{
    public partial class ColumnStandardizationServiceTests
    {
        #region Rule 7 Tests — StudyContext Contains Arm+N

        /**************************************************************/
        /// <summary>
        /// Pattern 9: StudyContext="Doxazosin N=339", TreatmentArm="Some non-drug text"
        /// Expected: ArmN=339, TreatmentArm="Doxazosin", StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule7_CtxIsArmWithN_Splits()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Some non-drug text", "Doxazosin N=339");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(339, result[0].ArmN);
            Assert.AreEqual("Doxazosin", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_ARM_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 9 variant: StudyContext="KANUMA N = 36" (with spaces around =)
        /// Expected: ArmN=36, StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule7_CtxIsArmWithSpacedN_Splits()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Nausea", "KANUMA N = 36");
            // TreatmentArm is "Nausea" — not a drug, so Rule 7 can overwrite
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(36, result[0].ArmN);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_ARM_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 9 variant: StudyContext="Placebo N=300"
        /// Rule 1 fires first (arm="(N=267)" → ArmN=267, arm cleared).
        /// Then Rule 7 fires (ctx="Placebo N=300" → ArmN=300, TreatmentArm="Placebo").
        /// Final ArmN=300 (Rule 7 overwrites Rule 1's value).
        /// </summary>
        [TestMethod]
        public async Task Rule7_CtxIsPlaceboWithN_ExtractsArmAndN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(N=267)", "Placebo N=300");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Rule 1 fires: ArmN=267, arm cleared. Rule 7 fires: ArmN=300, arm="Placebo"
            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.AreEqual(300, result[0].ArmN);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_N");
            assertHasFlag(result[0], "COL_STD:CTX_WAS_ARM_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 9 variant: StudyContext="Control Arm (N=18) n (%)"
        /// Expected: ArmN=18, StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule7_CtxIsArmWithNAndHint_Splits()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Nausea", "Control Arm (N=18) n (%)");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(18, result[0].ArmN);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_ARM_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 7 Tests

        #region Rule 8 Tests — StudyContext is Drug Name (Swap)

        /**************************************************************/
        /// <summary>
        /// Pattern 6: StudyContext="Alogliptin" (drug), TreatmentArm="Nausea" (not a drug)
        /// Expected: Swap — TreatmentArm="Alogliptin", StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule8_CtxIsDrug_ArmIsNot_Swaps()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Some Non-Drug Text", "Alogliptin");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Alogliptin", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:SWAP_ARM_CTX");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Both arm and ctx are drug names — no swap should occur.
        /// TreatmentArm="Placebo", StudyContext="Alogliptin"
        /// Expected: No change (arm is already a drug).
        /// </summary>
        [TestMethod]
        public async Task Rule8_BothAreDrugs_NoSwap()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Alogliptin");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.AreEqual("Alogliptin", result[0].StudyContext);
            // No swap flag should be set
            assertFlagAbsent(result[0], "COL_STD:SWAP_ARM_CTX",
                "Should not swap when both are drug names");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 8 Tests

        #region Rule 9 Tests — StudyContext is Descriptor Hint

        /**************************************************************/
        /// <summary>
        /// Pattern 7: StudyContext="Reaction"
        /// Expected: StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule9_CtxIsReaction_Cleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Reaction");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_DESC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 7 variant: StudyContext="Incidence"
        /// Expected: StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule9_CtxIsIncidence_Cleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("EVISTA", "Incidence");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("EVISTA", result[0].TreatmentArm);
            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_DESC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 7 variant: StudyContext="Adverse Event"
        /// Expected: StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule9_CtxIsAdverseEvent_Cleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Adverse Event");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_DESC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 7 variant: StudyContext="Dosing Regimen"
        /// Expected: StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule9_CtxIsDosingRegimen_Cleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Dosing Regimen");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_DESC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Pattern 7 variant: StudyContext="Discontinuation"
        /// Expected: StudyContext cleared.
        /// </summary>
        [TestMethod]
        public async Task Rule9_CtxIsDiscontinuation_Cleared()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Discontinuation");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].StudyContext);
            assertHasFlag(result[0], "COL_STD:CTX_WAS_DESC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 9 Tests

        #region Rule 10 Tests — TreatmentArm Has Trailing %

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="MYCAPSSA %", PrimaryValueType="Numeric"
        /// Expected: TreatmentArm="MYCAPSSA", PrimaryValueType="Percentage" (Phase 1 sets Percentage, Phase 3 migrates to Percentage).
        /// </summary>
        [TestMethod]
        public async Task Rule10_ArmHasTrailingPercent_StrippedAndPromoted()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("MYCAPSSA %", "Heart Study");
            obs.PrimaryValueType = "Numeric";
            obs.PrimaryValue = 29;
            obs.Unit = null;
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("MYCAPSSA", result[0].TreatmentArm);
            Assert.AreEqual("Percentage", result[0].PrimaryValueType);
            Assert.AreEqual("%", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:ARM_STRIP_PCT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="PLACEBO %", PrimaryValueType="Numeric"
        /// Expected: TreatmentArm="PLACEBO", PrimaryValueType="Percentage" (Phase 1 sets Percentage, Phase 3 migrates to Percentage).
        /// </summary>
        [TestMethod]
        public async Task Rule10_PlaceboWithPercent_StrippedAndPromoted()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("PLACEBO %", null);
            obs.PrimaryValueType = "Numeric";
            obs.PrimaryValue = 21;
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("PLACEBO", result[0].TreatmentArm);
            Assert.AreEqual("Percentage", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:ARM_STRIP_PCT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="Drug n(%)", PrimaryValueType="Numeric"
        /// Expected: TreatmentArm="Drug", PrimaryValueType="Percentage" (Phase 1 sets Percentage, Phase 3 migrates to Percentage).
        /// </summary>
        [TestMethod]
        public async Task Rule10_ArmHasTrailingNPct_StrippedAndPromoted()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo n(%)", null);
            obs.PrimaryValueType = "Numeric";
            obs.PrimaryValue = 7;
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.AreEqual("Percentage", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:ARM_STRIP_PCT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="MYCAPSSA %", PrimaryValueType="Percentage" (already set by source).
        /// Expected: Arm stripped, PrimaryValueType migrated to "Percentage" by Phase 3.
        /// </summary>
        [TestMethod]
        public async Task Rule10_ArmHasPercent_AlreadyPercentage_StillStrips()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("MYCAPSSA %", null);
            obs.PrimaryValueType = "Percentage";
            obs.PrimaryValue = 29;
            obs.Unit = "%";
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("MYCAPSSA", result[0].TreatmentArm);
            Assert.AreEqual("Percentage", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:ARM_STRIP_PCT");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="Pimecrolimus Cream; 1%" — the % is part of a concentration, not a format hint.
        /// The trailing format hint pattern requires whitespace before %, so "1%" should NOT match.
        /// Expected: No change.
        /// </summary>
        [TestMethod]
        public async Task Rule10_ArmWithConcentrationPercent_NoChange()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Pimecrolimus Cream; 1%", "Some Study");
            obs.PrimaryValueType = "Numeric";
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Pimecrolimus Cream; 1%", result[0].TreatmentArm);
            Assert.AreEqual("Numeric", result[0].PrimaryValueType);
            // Should not have Rule 10 flag
            assertFlagAbsent(result[0], "COL_STD:ARM_STRIP_PCT",
                "Should not strip % from concentration like '1%'");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 10 Tests

        #region Rule 11 Tests — TreatmentArm Has Bracketed [N=xxx]

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="Placebo [N=459]"
        /// Expected: TreatmentArm="Placebo", ArmN=459.
        /// </summary>
        [TestMethod]
        public async Task Rule11_ArmIsPlaceboWithBracketN_ExtractsArmAndN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo [N=459]", null,
                productTitle: "LYRICA- pregabalin capsule");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.AreEqual(459, result[0].ArmN);
            assertHasFlag(result[0], "COL_STD:ARM_BRACKET_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="75 mg/day [N=77]"
        /// Expected: DoseRegimen="75 mg/day", ArmN=77, TreatmentArm resolved from drug dictionary.
        /// </summary>
        [TestMethod]
        public async Task Rule11_ArmIsDoseWithBracketN_ExtractsDoseAndN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("75 mg/day [N=77]", null,
                productTitle: "LYRICA- pregabalin capsule");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("75 mg/day", result[0].DoseRegimen);
            Assert.AreEqual(77, result[0].ArmN);
            // TreatmentArm should be resolved from drug dictionary via ProductTitle
            Assert.IsNotNull(result[0].TreatmentArm,
                "TreatmentArm should be resolved from drug dictionary");
            // "pregabalin" is in our seed substance names
            Assert.IsTrue(
                result[0].TreatmentArm!.Contains("pregabalin", StringComparison.OrdinalIgnoreCase),
                $"Expected TreatmentArm to contain 'pregabalin' but was '{result[0].TreatmentArm}'");
            assertHasFlag(result[0], "COL_STD:ARM_BRACKET_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="600 mg/day [N=369]"
        /// Expected: DoseRegimen="600 mg/day", ArmN=369.
        /// </summary>
        [TestMethod]
        public async Task Rule11_ArmIs600mgWithBracketN_ExtractsDoseAndN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("600 mg/day [N=369]", null,
                productTitle: "LYRICA- pregabalin capsule");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("600 mg/day", result[0].DoseRegimen);
            Assert.AreEqual(369, result[0].ArmN);
            Assert.IsNotNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_BRACKET_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="All PGB [N=979]" — "All" prefix should be stripped,
        /// "PGB" is a known abbreviation → TreatmentArm="PGB".
        /// </summary>
        [TestMethod]
        public async Task Rule11_ArmIsAllPgbWithBracketN_StripsAllAndResolves()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("All PGB [N=979]", null,
                productTitle: "LYRICA- pregabalin capsule");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("PGB", result[0].TreatmentArm);
            Assert.AreEqual(979, result[0].ArmN);
            assertHasFlag(result[0], "COL_STD:ARM_BRACKET_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm="150 mg/day [N=212]" — dose with drug recovery from dictionary.
        /// ProductTitle="Pregabalin Tablets" (seed name "Pregabalin" is in substance list).
        /// </summary>
        [TestMethod]
        public async Task Rule11_ArmIs150mgWithBracketN_ResolvesDrugFromDictionary()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("150 mg/day [N=212]", null,
                productTitle: "Pregabalin Extended-Release Tablets");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("150 mg/day", result[0].DoseRegimen);
            Assert.AreEqual(212, result[0].ArmN);
            // Should resolve "pregabalin" from the drug dictionary via ProductTitle
            Assert.IsNotNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_BRACKET_N");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Multiple bracketed-N observations in a batch — consistent handling.
        /// Simulates the actual table from the screenshot.
        /// </summary>
        [TestMethod]
        public async Task Rule11_BatchOfBracketedN_AllCorrected()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var observations = new List<ParsedObservation>
            {
                createObservation("All PGB [N=979]", null, productTitle: "LYRICA- pregabalin capsule"),
                createObservation("Placebo [N=459]", null, productTitle: "LYRICA- pregabalin capsule"),
                createObservation("75 mg/day [N=77]", null, productTitle: "LYRICA- pregabalin capsule"),
                createObservation("150 mg/day [N=212]", null, productTitle: "LYRICA- pregabalin capsule"),
                createObservation("300 mg/day [N=321]", null, productTitle: "LYRICA- pregabalin capsule"),
                createObservation("600 mg/day [N=369]", null, productTitle: "LYRICA- pregabalin capsule"),
            };

            var result = service.Standardize(observations);

            // All PGB — drug abbreviation
            Assert.AreEqual("PGB", result[0].TreatmentArm);
            Assert.AreEqual(979, result[0].ArmN);

            // Placebo — drug name
            Assert.AreEqual("Placebo", result[1].TreatmentArm);
            Assert.AreEqual(459, result[1].ArmN);

            // Dose arms — should all have DoseRegimen populated and drug resolved
            for (int i = 2; i < 6; i++)
            {
                Assert.IsNotNull(result[i].DoseRegimen,
                    $"Observation {i}: DoseRegimen should be populated");
                Assert.IsNotNull(result[i].ArmN,
                    $"Observation {i}: ArmN should be populated");
                Assert.IsNotNull(result[i].TreatmentArm,
                    $"Observation {i}: TreatmentArm should be resolved from dictionary");
                assertHasFlag(result[i], "COL_STD:ARM_BRACKET_N");
            }

            // Verify specific doses
            Assert.AreEqual("75 mg/day", result[2].DoseRegimen);
            Assert.AreEqual("150 mg/day", result[3].DoseRegimen);
            Assert.AreEqual("300 mg/day", result[4].DoseRegimen);
            Assert.AreEqual("600 mg/day", result[5].DoseRegimen);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// TreatmentArm without brackets should NOT trigger Rule 11.
        /// </summary>
        [TestMethod]
        public async Task Rule11_ArmWithoutBrackets_NoChange()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", "Heart Study",
                productTitle: "LYRICA- pregabalin capsule");
            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Placebo", result[0].TreatmentArm);
            Assert.IsNull(result[0].ArmN);
            assertFlagAbsent(result[0], "COL_STD:ARM_BRACKET_N",
                "Rule 11 should not fire without brackets");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Rule 11 Tests
    }
}

