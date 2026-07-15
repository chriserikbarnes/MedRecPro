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
        #region Phase 2 Tests — DoseRegimen Triage

        /**************************************************************/
        /// <summary>
        /// Phase 2a: PK sub-parameter in DoseRegimen → initially routes to
        /// ParameterSubtype, then the PK column-contract enforcement
        /// (<c>applyPkCanonicalization</c>) promotes it to <c>ParameterName</c>
        /// per the data-dictionary contract (PK terms belong in Name only).
        /// </summary>
        [TestMethod]
        public async Task Phase2_DoseRegimenTriage_PkSubParam_RoutesToParameterName()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.DoseRegimen = "Cmax";
            obs.ParameterName = null;
            obs.ParameterSubtype = null;
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].DoseRegimen, "DoseRegimen should be null after PK sub-param routing");
            Assert.IsNull(result[0].Dose, "Dose should be cleared when DoseRegimen is routed away");
            Assert.IsNull(result[0].DoseUnit, "DoseUnit should be cleared when DoseRegimen is routed away");
            Assert.AreEqual("Cmax", result[0].ParameterName,
                "Per PK column contract: PK terms must land in ParameterName, not ParameterSubtype");
            Assert.IsNull(result[0].ParameterSubtype,
                "ParameterSubtype must be null after Name↔Subtype swap (Subtype reserved for qualifiers only)");
            assertHasFlag(result[0], "COL_STD:PK_SUBPARAM_ROUTED");
            assertHasFlag(result[0], "COL_STD:PK_NAME_SUBTYPE_SWAPPED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2a: Actual dose value in DoseRegimen is preserved.
        /// </summary>
        [TestMethod]
        public async Task Phase2_DoseRegimenTriage_ActualDose_Preserved()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.DoseRegimen = "50 mg once daily";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("50 mg once daily", result[0].DoseRegimen, "Actual dose should be preserved");
            Assert.AreEqual(50m, result[0].Dose, "Dose should be extracted from preserved DoseRegimen");
            Assert.AreEqual("mg/d", result[0].DoseUnit, "Frequency promotion: 'mg' + 'Once Daily' → 'mg/d'");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2a: Drug name in DoseRegimen for DDI → routes to ParameterSubtype.
        /// </summary>
        [TestMethod]
        public async Task Phase2_DoseRegimenTriage_CoAdminDrug_RoutesToParameterSubtype()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "DRUG_INTERACTION");
            obs.DoseRegimen = "Omeprazole";
            obs.ParameterSubtype = null;
            obs.PrimaryValueType = "Ratio";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].DoseRegimen);
            Assert.IsNull(result[0].Dose, "Dose should be cleared when DoseRegimen is routed away");
            Assert.IsNull(result[0].DoseUnit, "DoseUnit should be cleared when DoseRegimen is routed away");
            Assert.AreEqual("Omeprazole", result[0].ParameterSubtype);
            assertHasFlag(result[0], "COL_STD:COADMIN_ROUTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2a: Residual population in DoseRegimen → routes to Population.
        /// </summary>
        [TestMethod]
        public async Task Phase2_DoseRegimenTriage_ResidualPopulation_RoutesToPopulation()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.DoseRegimen = "elderly";
            obs.Population = null;
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].DoseRegimen);
            Assert.AreEqual("elderly", result[0].Population);
            assertHasFlag(result[0], "COL_STD:POPULATION_EXTRACTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2a: Residual timepoint in DoseRegimen → routes to Timepoint.
        /// </summary>
        [TestMethod]
        public async Task Phase2_DoseRegimenTriage_ResidualTimepoint_RoutesToTimepoint()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.DoseRegimen = "steady state";
            obs.Timepoint = null;
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].DoseRegimen);
            Assert.AreEqual("steady state", result[0].Timepoint);
            assertHasFlag(result[0], "COL_STD:TIMEPOINT_EXTRACTED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2 Tests — DoseRegimen Triage

        #region Phase 2 Tests — ParameterName Cleanup

        /**************************************************************/
        /// <summary>
        /// Phase 2b: Caption echo in ParameterName → nulled.
        /// </summary>
        [TestMethod]
        public async Task Phase2_ParameterName_CaptionEcho_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.ParameterName = "Table 3. Pharmacokinetic Parameters";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ParameterName);
            assertHasFlag(result[0], "COL_STD:ROW_TYPE=CAPTION");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2b: Header echo "n" in ParameterName → nulled.
        /// </summary>
        [TestMethod]
        public async Task Phase2_ParameterName_HeaderEcho_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.ParameterName = "n";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].ParameterName);
            assertHasFlag(result[0], "COL_STD:ROW_TYPE=HEADER");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2b: HTML entities in ParameterName → decoded.
        /// </summary>
        [TestMethod]
        public async Task Phase2_ParameterName_HtmlEntities_Decoded()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.ParameterName = "ALT &gt; 3x ULN";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ALT > 3x ULN", result[0].ParameterName);
            assertHasFlag(result[0], "COL_STD:HTML_ENTITY_DECODED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2 Tests — ParameterName Cleanup

        #region Phase 2 Tests — TreatmentArm Cleanup

        /**************************************************************/
        /// <summary>
        /// Phase 2c: Header echo "Number of Patients" in TreatmentArm → nulled.
        /// </summary>
        [TestMethod]
        public async Task Phase2_TreatmentArm_HeaderEcho_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation(null, category: "PK");
            obs.TreatmentArm = "Number of Patients";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_HEADER");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2c: Generic arm label "Treatment" → nulled.
        /// </summary>
        [TestMethod]
        public async Task Phase2_TreatmentArm_GenericLabel_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation(null, category: "PK");
            obs.TreatmentArm = "Treatment";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].TreatmentArm);
            assertHasFlag(result[0], "COL_STD:ARM_WAS_GENERIC");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2 Tests — TreatmentArm Cleanup

        #region Phase 2 Tests — Unit Scrub

        /**************************************************************/
        /// <summary>
        /// Phase 2d: Known unit passes through unchanged.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_KnownUnit_Preserved()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "mcg/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("mcg/mL", result[0].Unit);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2d: Long unit (> 30 chars) detected as header leak → nulled.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_HeaderLeak_LongString_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "Drug Delivery Rate Including Infusion Therapy";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_HEADER_LEAK");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2d: Unit containing header keyword → nulled.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_HeaderKeyword_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "Dosage Regimen";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_HEADER_LEAK");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2d: Variant unit spelling normalized to canonical form.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_VariantSpelling_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "mcg h/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("mcg·h/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #region R11 — Expanded Unit Variant Coverage

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: U+2219 BULLET OPERATOR variant of mcg·h/mL canonicalizes.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_BulletOperator_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "mcg\u2219h/mL"; // U+2219 BULLET OPERATOR

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("mcg·h/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: U+2022 BULLET variant of mcg·h/mL canonicalizes.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_Bullet_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "mcg\u2022h/mL"; // U+2022 BULLET

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("mcg·h/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: ASCII asterisk variant ng*h/mL canonicalizes to ng·h/mL.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_Asterisk_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "ng*h/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ng·h/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: Period variant ng.hr/mL canonicalizes to ng·h/mL.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_Period_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "ng.hr/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ng·h/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: U+00D7 MULTIPLICATION SIGN variant ng×hr/mL → ng·h/mL.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_MultiplicationSign_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "ng\u00D7hr/mL"; // U+00D7 MULTIPLICATION SIGN

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ng·h/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: Greek mu (U+03BC) and Micro sign (U+00B5) both canonicalize
        /// to the same form so μg/mL and µg/mL are identical post-normalization.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_GreekMu_Folded()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            // Greek mu input
            var obsGreek = createObservation("Placebo");
            obsGreek.Unit = "\u03BCg/mL"; // U+03BC GREEK SMALL LETTER MU

            // Micro sign input
            var obsMicro = createObservation("Placebo");
            obsMicro.Unit = "\u00B5g/mL"; // U+00B5 MICRO SIGN

            var result = service.Standardize(new List<ParsedObservation> { obsGreek, obsMicro });

            // Both canonicalize to the same form (Greek mu, post-NFKC)
            Assert.AreEqual("\u03BCg/mL", result[0].Unit, "Greek mu input should round-trip");
            Assert.AreEqual("\u03BCg/mL", result[1].Unit, "Micro sign input should fold to Greek mu");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: Time word "Hours" / "hours" / "hour" all canonicalize to "h".
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_HoursWord_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs1 = createObservation("Placebo");
            obs1.Unit = "Hours";

            var obs2 = createObservation("Placebo");
            obs2.Unit = "hour";

            var result = service.Standardize(new List<ParsedObservation> { obs1, obs2 });

            Assert.AreEqual("h", result[0].Unit, "'Hours' should normalize to 'h'");
            Assert.AreEqual("h", result[1].Unit, "'hour' should normalize to 'h'");
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");
            assertHasFlag(result[1], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: Long-form "nanogram per mL" canonicalizes to "ng/mL".
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_LongFormNanogram_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "nanogram per mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ng/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: Whitespace-defective unit "mcg /mL" (PDF extraction artifact)
        /// canonicalizes to "mcg/mL" via the whitespace-tolerant fallback.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_WhitespaceCollapse_Simple_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "mcg /mL"; // space before slash — common PDF extraction defect

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("mcg/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: Whitespace + period variant "mcg . hr /mL" canonicalizes
        /// to "mcg·h/mL" — combines whitespace strip and period→middle-dot mapping.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_WhitespaceCollapse_Compound_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "mcg . hr /mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("mcg·h/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: Reversed-order AUC "h·ng/mL" canonicalizes to "ng·h/mL".
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_ReversedAucOrder_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "h·ng/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ng·h/mL", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: New canonical mg·h/L preserved when input is exact match;
        /// bullet variant mg•h/L canonicalizes after Unicode fold.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_NewCanonical_MgHL()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obsExact = createObservation("Placebo");
            obsExact.Unit = "mg·h/L";

            var obsBullet = createObservation("Placebo");
            obsBullet.Unit = "mg\u2022h/L"; // U+2022 BULLET

            var result = service.Standardize(new List<ParsedObservation> { obsExact, obsBullet });

            Assert.AreEqual("mg·h/L", result[0].Unit, "Exact match should be preserved");
            Assert.AreEqual("mg·h/L", result[1].Unit, "Bullet variant should fold to canonical");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: Age descriptor "Ages 27-58 yrs" → null + UNIT_HEADER_LEAK.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_AgeDescriptor_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "Ages 27-58 yrs";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_HEADER_LEAK");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: Statistical descriptor "Mean ± SD" → null + UNIT_HEADER_LEAK.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_Statistics_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "Mean \u00B1 SD"; // U+00B1 PLUS-MINUS

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_HEADER_LEAK");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: AUC subscript "0-24" → null + UNIT_HEADER_LEAK.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_AucSubscript_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "0-24";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_HEADER_LEAK");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: Dose regimen leak "20mg/kg every 8 hours" → null + UNIT_HEADER_LEAK.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_DoseRegimenLeak_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "20mg/kg every 8 hours";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_HEADER_LEAK");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: HIV antiretroviral abbreviation "BIC" → null + UNIT_HEADER_LEAK
        /// via the drug-name detection rule (Rule 3).
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_DrugAbbreviation_BIC_Nulled()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "BIC";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.IsNull(result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_HEADER_LEAK");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R11 Phase 2d: CV% header form normalizes to %CV canonical.
        /// </summary>
        [TestMethod]
        public async Task Phase2_Unit_CvPercent_Normalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.Unit = "CV%";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("%CV", result[0].Unit);
            assertHasFlag(result[0], "COL_STD:UNIT_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion R11 — Expanded Unit Variant Coverage

        #endregion Phase 2 Tests — Unit Scrub

        #region Phase 2 Tests — SOC Mapping

        /**************************************************************/
        /// <summary>
        /// Phase 2e: SOC variant normalized to canonical name.
        /// </summary>
        [TestMethod]
        public async Task Phase2_SOC_VariantNormalized()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.ParameterCategory = "gastrointestinal";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Gastrointestinal Disorders", result[0].ParameterCategory);
            assertHasFlag(result[0], "COL_STD:SOC_NORMALIZED");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2e: SOC mapping only applies to ADVERSE_EVENT category.
        /// Phase 4 nulls ParameterCategory for PK (N/A), so the value is null — but NOT
        /// because of SOC normalization. Verify no SOC_NORMALIZED flag is present.
        /// </summary>
        [TestMethod]
        public async Task Phase2_SOC_NonAeCategory_Unchanged()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.ParameterCategory = "gastrointestinal";
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            // ParameterCategory is null due to Phase 4 contract enforcement (N/A for PK),
            // NOT because of SOC normalization — verify no SOC_NORMALIZED flag
            Assert.IsNull(result[0].ParameterCategory, "Phase 4 should null ParameterCategory for PK");
            Assert.IsFalse(result[0].ValidationFlags?.Contains("COL_STD:SOC_NORMALIZED") == true,
                "SOC normalization should not apply to PK category");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 2 Tests — SOC Mapping

        #region Phase 3 Tests — PrimaryValueType Migration

        /**************************************************************/
        /// <summary>
        /// Phase 3: "Mean" in PK context → "ArithmeticMean" (ArithmeticMean is the default for
        /// ALL categories; GeometricMean only when caption/header/footer explicitly says so).
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_MeanInPK_BecomesArithmeticMean()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "PK");
            obs.PrimaryValueType = "Mean";
            obs.Unit = "mcg/mL";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ArithmeticMean", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:PVT_MIGRATED:Mean→ArithmeticMean");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3: "Mean" in AE context → "ArithmeticMean".
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_MeanInAE_BecomesArithmeticMean()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.PrimaryValueType = "Mean";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("ArithmeticMean", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:PVT_MIGRATED:Mean→ArithmeticMean");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3: "Percentage" is already canonical — no migration, no flag.
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_Percentage_RemainsPercentage()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.PrimaryValueType = "Percentage";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Percentage", result[0].PrimaryValueType);

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3: "Numeric" in AE with Unit="%" → "Percentage".
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_NumericAeWithPercent_BecomesPercentageCount()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo");
            obs.PrimaryValueType = "Numeric";
            obs.Unit = "%";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("Percentage", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:PVT_MIGRATED:Numeric→Percentage");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3: "Numeric" in DDI → "GeometricMeanRatio".
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_NumericInDDI_BecomesGeometricMeanRatio()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "DRUG_INTERACTION");
            obs.PrimaryValueType = "Numeric";
            obs.Unit = "ratio";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("GeometricMeanRatio", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:PVT_MIGRATED:Numeric→GeometricMeanRatio");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3: "Ratio" in DDI → "GeometricMeanRatio".
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_RatioInDDI_BecomesGeometricMeanRatio()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "DRUG_INTERACTION");
            obs.PrimaryValueType = "Ratio";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("GeometricMeanRatio", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:PVT_MIGRATED:Ratio→GeometricMeanRatio");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3: "RelativeRiskReduction" with "hazard" in caption → "HazardRatio".
        /// </summary>
        [TestMethod]
        public async Task Phase3_PVT_RRR_WithHazardCaption_BecomesHazardRatio()
        {
            #region implementation

            var (service, context, sentinel) = await createInitializedServiceAsync();

            var obs = createObservation("Placebo", category: "EFFICACY");
            obs.PrimaryValueType = "RelativeRiskReduction";
            obs.Caption = "Hazard Ratio for Overall Survival";

            var result = service.Standardize(new List<ParsedObservation> { obs });

            Assert.AreEqual("HazardRatio", result[0].PrimaryValueType);
            assertHasFlag(result[0], "COL_STD:PVT_MIGRATED:RelativeRiskReduction→HazardRatio");

            context.Dispose();
            sentinel.Dispose();

            #endregion
        }

        #endregion Phase 3 Tests — PrimaryValueType Migration
    }
}

