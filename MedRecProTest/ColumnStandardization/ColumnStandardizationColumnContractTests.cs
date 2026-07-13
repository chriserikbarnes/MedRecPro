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
        #region Phase 4 Tests — Column Contract Enforcement

        /**************************************************************/
        /// <summary>
        /// Phase 4: N/A columns are nulled for PK (e.g., ParameterCategory).
        /// </summary>
        [TestMethod]
        public async Task Phase4_NullEnforcement_PK_ParameterCategoryNulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.ParameterCategory = "Some leftover category";
            obs.PrimaryValueType = "Mean";
            obs.Unit = "mcg/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ParameterCategory, "ParameterCategory should be NULL for PK");
            assertHasFlag(result[0], "COL_STD:NULL_ParameterCategory");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 4: N/A columns are nulled for AE (Timepoint, Time, TimeUnit).
        /// </summary>
        [TestMethod]
        public async Task Phase4_NullEnforcement_AE_TimepointColumnsNulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Timepoint = "Week 24";
            obs.Time = 24.0;
            obs.TimeUnit = "weeks";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Timepoint, "Timepoint should be NULL for AE");
            Assert.IsNull(result[0].Time, "Time should be NULL for AE");
            Assert.IsNull(result[0].TimeUnit, "TimeUnit should be NULL for AE");
            assertHasFlag(result[0], "COL_STD:NULL_Timepoint");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 4: Missing required column is flagged.
        /// </summary>
        [TestMethod]
        public async Task Phase4_MissingRequired_AE_MissingTreatmentArm_Flagged()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation(null);

            var result = service.Standardize(new List<ParsedObservation> { obs });

            assertHasFlag(result[0], "COL_STD:MISSING_R_TreatmentArm");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 4 consumes <see cref="IColumnContractRegistry"/> without widening the
        /// legacy report-facing missing-required flag surface to value columns.
        /// </summary>
        [TestMethod]
        public async Task Phase4_MissingRequired_AE_MissingPrimaryValue_NotFlagged()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.PrimaryValue = null;

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsFalse(
                result[0].ValidationFlags?.Contains("COL_STD:MISSING_R_PrimaryValue") ?? false,
                "Phase 4 must preserve the pre-refactor flag surface and leave value-column null penalties to QC_PARSE_QUALITY.");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 4: Default BoundType applied for DDI with bounds but no BoundType.
        /// </summary>
        [TestMethod]
        public async Task Phase4_DefaultBoundType_DDI_Gets90CI()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "DRUG_INTERACTION");
            obs.PrimaryValueType = "Ratio";
            obs.LowerBound = 0.80;
            obs.UpperBound = 1.25;
            obs.BoundType = null;

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("90CI", result[0].BoundType);
            assertHasFlag(result[0], "COL_STD:BOUND_TYPE_INFERRED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 4: Default BoundType applied for EFFICACY with bounds → 95CI.
        /// </summary>
        [TestMethod]
        public async Task Phase4_DefaultBoundType_Efficacy_Gets95CI()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "EFFICACY");
            obs.PrimaryValueType = "RelativeRiskReduction";
            obs.LowerBound = 0.45;
            obs.UpperBound = 0.88;
            obs.BoundType = null;

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("95CI", result[0].BoundType);
            assertHasFlag(result[0], "COL_STD:BOUND_TYPE_INFERRED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 4: Existing BoundType is NOT overwritten.
        /// </summary>
        [TestMethod]
        public async Task Phase4_DefaultBoundType_ExistingBoundType_Preserved()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.PrimaryValueType = "Mean";
            obs.LowerBound = 10.0;
            obs.UpperBound = 20.0;
            obs.BoundType = "90CI";
            obs.Unit = "mcg/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("90CI", result[0].BoundType, "Existing BoundType should not be overwritten");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 4 Tests — Column Contract Enforcement

        #region Cross-Category Tests

        /**************************************************************/
        /// <summary>
        /// PK observations are now processed (not skipped).
        /// </summary>
        [TestMethod]
        public async Task CrossCategory_PK_NowProcessed()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.PrimaryValueType = "Mean";
            obs.Unit = "mcg/mL";
            obs.ParameterCategory = "ShouldBeNulled";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Phase 3 should migrate Mean → ArithmeticMean for PK (per commit 1e80942)
            Assert.AreEqual("ArithmeticMean", result[0].PrimaryValueType);
            // Phase 4 should null ParameterCategory (N/A for PK)
            Assert.IsNull(result[0].ParameterCategory);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// SKIP category is still skipped by all phases.
        /// </summary>
        [TestMethod]
        public async Task CrossCategory_Skip_StillSkipped()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "SKIP");
            obs.PrimaryValueType = "Mean";
            obs.ParameterCategory = "ShouldNotBeNulled";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            // Nothing should change for SKIP
            Assert.AreEqual("Mean", result[0].PrimaryValueType);
            Assert.AreEqual("ShouldNotBeNulled", result[0].ParameterCategory);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Cross-Category Tests

        #region Phase 2 Pre-Pass: Inline N= Extraction

        /**************************************************************/
        /// <summary>
        /// Standalone (n=178) in non-AE TreatmentArm → ArmN=178, TreatmentArm nulled.
        /// Validates the gap where _bracketedNPattern (square brackets only) and
        /// _embeddedNPattern (trailing Name N=xxx) both miss parenthesized standalone N=.
        /// </summary>
        [TestMethod]
        public async Task InlineN_StandaloneParenN_NonAE_ExtractsArmN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(n=178)", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(178, result[0].ArmN);
            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:TreatmentArm");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Standalone [N=60] in non-AE TreatmentArm → ArmN=60, TreatmentArm nulled.
        /// Uses PK category where ArmN is Optional.
        /// </summary>
        [TestMethod]
        public async Task InlineN_StandaloneBracketN_NonAE_ExtractsArmN()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("[N=60]", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(60, result[0].ArmN);
            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:TreatmentArm");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// DoseRegimen with embedded (n=963) mid-string → ArmN=963, N stripped from DoseRegimen.
        /// </summary>
        [TestMethod]
        public async Task InlineN_DoseRegimenEmbeddedN_ExtractsAndCleans()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", doseRegimen: "23 mg/day Donezepil Hydrochloride (n=963) %", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(963, result[0].ArmN);
            Assert.IsNotNull(result[0].DoseRegimen);
            Assert.IsFalse(result[0].DoseRegimen!.Contains("963"),
                $"Expected N=963 stripped from DoseRegimen but was '{result[0].DoseRegimen}'");
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:DoseRegimen");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// RawValue containing N= must NOT be touched by the pre-pass (RawValue is excluded).
        /// </summary>
        [TestMethod]
        public async Task InlineN_RawValueWithN_Untouched()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.RawValue = "1.31 (±0.76) (n=25)";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("1.31 (±0.76) (n=25)", result[0].RawValue);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 1 AE row with "Placebo [N=459]" is handled by Phase 1 Rule 11.
        /// The pre-pass should be a no-op since Phase 1 already cleaned TreatmentArm.
        /// ArmN should still be 459 from Phase 1.
        /// </summary>
        [TestMethod]
        public async Task InlineN_Phase1AE_BracketN_HandledByPhase1()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo [N=459]", category: "ADVERSE_EVENT");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(459, result[0].ArmN);
            Assert.AreEqual("Placebo", result[0].TreatmentArm);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// When ArmN is already set from Phase 1, the pre-pass should strip N from
        /// other columns but NOT overwrite the existing ArmN value.
        /// </summary>
        [TestMethod]
        public async Task InlineN_ArmNAlreadySet_DoesNotOverwrite()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Phase 1 sets ArmN from TreatmentArm "(N=267)" for AE category
            var obs = createObservation("(N=267)", studyContext: "Placebo", category: "ADVERSE_EVENT");
            obs.DoseRegimen = "50 mg (n=100)";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            // ArmN should be 267 from Phase 1 (TreatmentArm), NOT 100 from DoseRegimen
            Assert.AreEqual(267, result[0].ArmN);
            // DoseRegimen N should still be stripped
            if (result[0].DoseRegimen != null)
            {
                Assert.IsFalse(result[0].DoseRegimen!.Contains("100"),
                    $"Expected N=100 stripped from DoseRegimen but was '{result[0].DoseRegimen}'");
            }
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:DoseRegimen");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// StudyContext with embedded (N=50) → ArmN=50, N stripped from StudyContext.
        /// Validates that non-TreatmentArm columns are also scanned.
        /// </summary>
        [TestMethod]
        public async Task InlineN_StudyContextEmbeddedN_ExtractsAndCleans()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", studyContext: "Study 1 (N=50)", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(50, result[0].ArmN);
            if (result[0].StudyContext != null)
            {
                Assert.IsFalse(result[0].StudyContext!.Contains("50"),
                    $"Expected N=50 stripped from StudyContext but was '{result[0].StudyContext}'");
            }
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:StudyContext");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2 Pre-Pass: Inline N= Extraction

        #region Issue 2: Comma-Formatted ArmN Extraction

        /**************************************************************/
        /// <summary>
        /// _nValuePattern matches comma-formatted N values: "(n = 8,506)" → ArmN=8506.
        /// </summary>
        [TestMethod]
        public async Task NValuePattern_MatchesCommaFormattedNumber()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("(n = 8,506)", category: "ADVERSE_EVENT");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(8506, result[0].ArmN,
                "Expected comma-formatted N=8,506 to parse as ArmN=8506");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// _embeddedNPattern matches comma-formatted N values: "Placebo N=8,506" → ArmN=8506.
        /// Uses PK category to exercise the normalizeTreatmentArm path (non-Phase1 category).
        /// </summary>
        [TestMethod]
        public async Task EmbeddedNPattern_MatchesCommaFormattedNumber()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo N=8,506", category: "PK");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(8506, result[0].ArmN,
                "Expected comma-formatted N=8,506 to parse as ArmN=8506");
            Assert.AreEqual("Placebo", result[0].TreatmentArm?.Trim(),
                "Expected TreatmentArm to be 'Placebo' after embedded N extraction");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// _bracketedNPattern matches comma-formatted N values: "Placebo [N=8,102]" → ArmN=8102.
        /// </summary>
        [TestMethod]
        public async Task BracketedNPattern_MatchesCommaFormattedNumber()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo [N=8,102]", category: "ADVERSE_EVENT");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(8102, result[0].ArmN,
                "Expected comma-formatted [N=8,102] to parse as ArmN=8102");
            Assert.AreEqual("Placebo", result[0].TreatmentArm?.Trim(),
                "Expected TreatmentArm to be 'Placebo' after bracket stripping");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// _inlineNPattern matches comma-formatted N values embedded in text:
        /// "CE/MPA (n = 8,506)" → stripped + ArmN=8506.
        /// </summary>
        [TestMethod]
        public async Task InlineNPattern_MatchesCommaFormattedNumber()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Use DoseRegimen to exercise the inline N pre-pass (Phase 2)
            var obs = createObservation("Placebo", doseRegimen: "CE/MPA (n = 8,506)", category: "ADVERSE_EVENT");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(8506, result[0].ArmN,
                "Expected comma-formatted inline (n = 8,506) to parse as ArmN=8506");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// _rawValueTrailingNPattern matches comma-formatted N values:
        /// "2.9 (22%) N=1,234" → ArmN=1234.
        /// </summary>
        [TestMethod]
        public async Task RawValueTrailingN_MatchesCommaFormattedNumber()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.RawValue = "2.9 (22%) N=1,234";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(1234, result[0].ArmN,
                "Expected comma-formatted trailing N=1,234 to parse as ArmN=1234");
            assertHasFlag(result[0], "COL_STD:N_STRIPPED:RawValue");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// _standaloneBracketNPattern matches comma-formatted N values:
        /// "[N=8,506]" → ArmN=8506.
        /// </summary>
        [TestMethod]
        public async Task StandaloneBracketN_MatchesCommaFormattedNumber()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Put standalone bracket N in DoseRegimen to exercise the inline N pre-pass
            var obs = createObservation("Placebo", doseRegimen: "[N=8,506]", category: "ADVERSE_EVENT");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual(8506, result[0].ArmN,
                "Expected comma-formatted standalone [N=8,506] to parse as ArmN=8506");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Issue 2: Comma-Formatted ArmN Extraction

        #region Issue 1: Extract Units from ParameterSubtype

        /**************************************************************/
        /// <summary>
        /// Cmax(pg/mL) → after unit extraction (Unit="pg/mL", Subtype="Cmax")
        /// the PK column-contract enforcement promotes "Cmax" into ParameterName
        /// and nulls ParameterSubtype per the data-dictionary contract.
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_CmaxWithUnit()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "Cmax(pg/mL)", category: "PK");
            obs.ParameterName = null; // ensure Name is empty so the Subtype→Name promotion fires

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName,
                "Per PK contract, Cmax must land in ParameterName after enforcement");
            Assert.IsNull(result[0].ParameterSubtype,
                "ParameterSubtype must be null after Subtype→Name promotion");
            Assert.AreEqual("pg/mL", result[0].Unit,
                "Expected Unit to be 'pg/mL'");
            assertHasFlag(result[0], "COL_STD:PK_SUBPARAM_UNIT_EXTRACTED");
            assertHasFlag(result[0], "COL_STD:PK_NAME_SUBTYPE_SWAPPED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// AUC120(pg·hr/mL) → after unit extraction (Unit="pg·h/mL", Subtype="AUC120")
        /// the PK column-contract enforcement canonicalizes AUC120 (non-standard
        /// interval) to the generic AUC canonical and promotes to ParameterName.
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_AUC120WithHrVariant()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "AUC120(pg·hr/mL)", category: "PK");
            obs.ParameterName = null;

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("AUC", result[0].ParameterName,
                "Non-standard AUC interval collapses to the generic AUC canonical");
            Assert.IsNull(result[0].ParameterSubtype,
                "ParameterSubtype must be null after Subtype→Name promotion");
            Assert.AreEqual("pg·h/mL", result[0].Unit,
                "Expected Unit to be 'pg·h/mL' (normalized from pg·hr/mL)");
            assertHasFlag(result[0], "COL_STD:PK_SUBPARAM_UNIT_EXTRACTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Cmax(serum, mcg/mL) → after unit extraction Subtype="Cmax, serum"
        /// and Unit="mcg/mL". The PK column-contract enforcement then scrubs
        /// the embedded "Cmax" from Subtype and promotes to ParameterName; the
        /// "serum" qualifier is preserved as the residual Subtype.
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_CmaxWithQualifierAndUnit()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "Cmax(serum, mcg/mL)", category: "PK");
            obs.ParameterName = null;

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax", result[0].ParameterName,
                "Cmax should land in ParameterName per PK contract");
            Assert.AreEqual("mcg/mL", result[0].Unit,
                "Expected Unit to be 'mcg/mL'");
            // Subtype must NOT contain a PK term — the "Cmax" is gone. "serum"
            // is a qualifier; the enforcement may keep it or drop it. Assert
            // only the invariant: no PK term in Subtype.
            Assert.IsFalse(
                PkParameterDictionary.ContainsPkParameter(result[0].ParameterSubtype),
                $"ParameterSubtype must not contain a PK term, got: '{result[0].ParameterSubtype}'");
            assertHasFlag(result[0], "COL_STD:PK_SUBPARAM_UNIT_EXTRACTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Serum AUC0-∞(mcg·hr/mL) → after unit extraction (Unit="mcg·h/mL",
        /// Subtype="Serum AUC0-∞") the PK column-contract enforcement extracts
        /// AUC0-inf from the embedded PK term and promotes to ParameterName.
        /// The "Serum" prefix is dropped (sample matrix — not a PK qualifier).
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_PrefixedAUC()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "Serum AUC0-∞(mcg·hr/mL)", category: "PK");
            obs.ParameterName = null;

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("AUC0-inf", result[0].ParameterName,
                "Embedded canonical PK term extracted from phrase and promoted");
            Assert.IsFalse(
                PkParameterDictionary.ContainsPkParameter(result[0].ParameterSubtype),
                $"ParameterSubtype must not contain a PK term, got: '{result[0].ParameterSubtype}'");
            Assert.AreEqual("mcg·h/mL", result[0].Unit,
                "Expected Unit to be 'mcg·h/mL'");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// AUC84 (no parentheses) → the AUC&lt;digits&gt; catch-all in the PK
        /// dictionary maps this non-standard interval to the generic AUC canonical;
        /// the PK column-contract enforcement promotes it into ParameterName.
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_NoParentheses()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "AUC84", category: "PK");
            obs.ParameterName = null;

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("AUC", result[0].ParameterName,
                "Non-standard AUC interval collapses to the generic AUC canonical");
            Assert.IsNull(result[0].ParameterSubtype,
                "ParameterSubtype must be null after Subtype→Name promotion");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Tmax(hr) → after unit extraction (Unit="h", Subtype="Tmax") the PK
        /// column-contract enforcement promotes Tmax into ParameterName.
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_TmaxHr()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "Tmax(hr)", category: "PK");
            obs.ParameterName = null;

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Tmax", result[0].ParameterName,
                "Tmax should land in ParameterName per PK contract");
            Assert.IsNull(result[0].ParameterSubtype,
                "ParameterSubtype must be null after Subtype→Name promotion");
            Assert.AreEqual("h", result[0].Unit,
                "Expected Unit to be 'h' (normalized from 'hr')");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// When Unit is already set, extraction should NOT overwrite it, but
        /// ParameterSubtype is still cleaned — the embedded Cmax is promoted
        /// into ParameterName per the PK column contract.
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_DoesNotOverwriteExistingUnit()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "Cmax(pg/mL)", category: "PK");
            obs.ParameterName = null;
            obs.Unit = "ng/mL";  // Pre-existing unit

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ng/mL", result[0].Unit,
                "Expected existing Unit to be preserved, not overwritten");
            Assert.AreEqual("Cmax", result[0].ParameterName,
                "Cmax should be promoted to ParameterName per PK contract");
            Assert.IsNull(result[0].ParameterSubtype,
                "ParameterSubtype must be null after Subtype→Name promotion");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Non-PK category (ADVERSE_EVENT) → no extraction, ParameterSubtype unchanged.
        /// </summary>
        [TestMethod]
        public async Task ExtractUnit_NonPKCategory_Skipped()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", parameterSubtype: "Cmax(pg/mL)", category: "ADVERSE_EVENT");

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Cmax(pg/mL)", result[0].ParameterSubtype,
                "Expected ParameterSubtype unchanged for ADVERSE_EVENT category");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Issue 1: Extract Units from ParameterSubtype

        #region Issue 3: Post-Processing Stage 3.6

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction catches a unit in ParameterSubtype that was missed
        /// by the earlier Standardize phase (simulating Claude restoring it).
        /// </summary>
        [TestMethod]
        public async Task PostProcess_ExtractsUnitMissedByEarlierStage()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Simulate an observation that went through Standardize already (no unit extracted)
            // then Claude corrected ParameterSubtype to an extractable form
            var obs = createObservation("Placebo", category: "PK");
            obs.ParameterSubtype = "Cmax(pg/mL)";
            obs.Unit = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("pg/mL", result[0].Unit,
                "Expected PostProcessExtraction to extract unit from ParameterSubtype");
            Assert.AreEqual("Cmax", result[0].ParameterSubtype,
                "Expected ParameterSubtype cleaned after unit extraction");
            assertHasFlag(result[0], "COL_STD:POST_PK_SUBPARAM_UNIT_EXTRACTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction catches a comma-formatted ArmN value in TreatmentArm
        /// that was missed by the earlier Standardize phase (simulating Claude restoring it).
        /// </summary>
        [TestMethod]
        public async Task PostProcess_ExtractsCommaArmNMissedByEarlierStage()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Simulate an observation where Claude restored an N= value in DoseRegimen
            var obs = createObservation("Placebo", category: "ADVERSE_EVENT");
            obs.DoseRegimen = "Drug (n = 8,506)";
            obs.ArmN = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual(8506, result[0].ArmN,
                "Expected PostProcessExtraction to extract comma-formatted ArmN");
            assertHasFlag(result[0], "COL_STD:POST_N_STRIPPED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction corrects PrimaryValueType from "Count" to "Percentage"
        /// when TreatmentArm contains a "%" indicator and value is &lt;= 100.
        /// </summary>
        [TestMethod]
        public async Task PostProcess_CorrectsCountToPercentage_WhenTreatmentArmHasPercentHint()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("% Any Dose", category: "ADVERSE_EVENT");
            obs.PrimaryValueType = "Count";
            obs.PrimaryValue = 2.5;
            obs.SecondaryValueType = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Percentage", result[0].PrimaryValueType,
                "Expected PrimaryValueType corrected from Count to Percentage");
            assertHasFlag(result[0], "COL_STD:POST_PCT_TYPE_CORRECTED:TreatmentArm");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction corrects PrimaryValueType from "Count" to "Percentage"
        /// when ParameterName contains "incidence".
        /// </summary>
        [TestMethod]
        public async Task PostProcess_CorrectsCountToPercentage_WhenParameterNameHasIncidence()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "ADVERSE_EVENT");
            obs.ParameterName = "Incidence of Nausea";
            obs.PrimaryValueType = "Count";
            obs.PrimaryValue = 24.4;
            obs.SecondaryValueType = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Percentage", result[0].PrimaryValueType,
                "Expected PrimaryValueType corrected from Count to Percentage");
            assertHasFlag(result[0], "COL_STD:POST_PCT_TYPE_CORRECTED:ParameterName");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction does NOT correct when SecondaryValueType is already set,
        /// indicating the parser already resolved the type pairing.
        /// </summary>
        [TestMethod]
        public async Task PostProcess_NoCorrection_WhenSecondaryValueTypePresent()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("% Placebo", category: "ADVERSE_EVENT");
            obs.PrimaryValueType = "Count";
            obs.PrimaryValue = 15.0;
            obs.SecondaryValueType = "Count";

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Count", result[0].PrimaryValueType,
                "Expected PrimaryValueType to remain Count when SecondaryValueType is set");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction does NOT correct when PrimaryValue exceeds 100.
        /// </summary>
        [TestMethod]
        public async Task PostProcess_NoCorrection_WhenPrimaryValueOver100()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("% Any Dose", category: "ADVERSE_EVENT");
            obs.PrimaryValueType = "Count";
            obs.PrimaryValue = 150.0;
            obs.SecondaryValueType = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Count", result[0].PrimaryValueType,
                "Expected PrimaryValueType to remain Count when value > 100");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction does NOT correct when PrimaryValueType is not "Count".
        /// </summary>
        [TestMethod]
        public async Task PostProcess_NoCorrection_WhenPrimaryValueTypeIsNotCount()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("% Placebo", category: "ADVERSE_EVENT");
            obs.PrimaryValueType = "Mean";
            obs.PrimaryValue = 50.0;
            obs.SecondaryValueType = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Mean", result[0].PrimaryValueType,
                "Expected PrimaryValueType to remain Mean");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction corrects PrimaryValueType when ParameterCategory
        /// contains "PROPORTION" (case-insensitive match).
        /// </summary>
        [TestMethod]
        public async Task PostProcess_CorrectsCountToPercentage_CaseInsensitiveKeyword()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "ADVERSE_EVENT");
            obs.ParameterCategory = "PROPORTION of patients";
            obs.PrimaryValueType = "Count";
            obs.PrimaryValue = 88.0;
            obs.SecondaryValueType = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Percentage", result[0].PrimaryValueType,
                "Expected PrimaryValueType corrected from Count to Percentage");
            assertHasFlag(result[0], "COL_STD:POST_PCT_TYPE_CORRECTED:ParameterCategory");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PostProcessExtraction corrects PrimaryValueType when ParameterSubtype
        /// contains "rate of" (two-word keyword match).
        /// </summary>
        [TestMethod]
        public async Task PostProcess_CorrectsCountToPercentage_RateOfKeyword()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "ADVERSE_EVENT");
            obs.ParameterSubtype = "Rate of occurrence";
            obs.PrimaryValueType = "Count";
            obs.PrimaryValue = 5.0;
            obs.SecondaryValueType = null;

            var result = service.PostProcessExtraction(new List<ParsedObservation> { obs });

            Assert.AreEqual("Percentage", result[0].PrimaryValueType,
                "Expected PrimaryValueType corrected from Count to Percentage");
            assertHasFlag(result[0], "COL_STD:POST_PCT_TYPE_CORRECTED:ParameterSubtype");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Issue 3: Post-Processing Stage 3.6

        #region Issue 4: Confidence Provenance

        /**************************************************************/
        /// <summary>
        /// After Standardize, every processed observation should have a CONFIDENCE:PATTERN: flag
        /// with format CONFIDENCE:PATTERN:{score}:{reason}({correctionCount}).
        /// </summary>
        [TestMethod]
        public async Task Standardize_AppendsConfidencePatternFlag()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "ADVERSE_EVENT");
            obs.ParseConfidence = 0.90;

            var result = service.Standardize(new List<ParsedObservation> { obs });

            var validationFlags = result[0].ValidationFlags;
            if (validationFlags is null)
            {
                Assert.Fail("Expected ValidationFlags to not be null after standardization");
                return;
            }

            Assert.IsTrue(validationFlags.Contains("CONFIDENCE:PATTERN:"),
                $"Expected CONFIDENCE:PATTERN: flag but got: '{validationFlags}'");

            // Verify format: CONFIDENCE:PATTERN:0.90:clean(0) or similar
            var flagParts = validationFlags.Split("; ")
                .FirstOrDefault(f => f.StartsWith("CONFIDENCE:PATTERN:"));
            if (flagParts is null)
            {
                Assert.Fail("CONFIDENCE:PATTERN flag should be present");
                return;
            }

            var segments = flagParts.Split(':');
            Assert.AreEqual(4, segments.Length,
                $"Expected 4 colon-separated segments in CONFIDENCE:PATTERN flag but got: '{flagParts}'");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Issue 4: Confidence Provenance
    }
}

