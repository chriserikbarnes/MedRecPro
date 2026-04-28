using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Tests for <see cref="DoseExtractor"/> — the static utility that extracts structured
    /// <c>Dose</c> (decimal) and <c>DoseUnit</c> (string) from free-text dose descriptions.
    /// </summary>
    /// <remarks>
    /// ## Test Strategy
    /// Pure unit tests — no database or mocking required. DoseExtractor is a static class
    /// with deterministic regex-based extraction.
    ///
    /// ## Test Organization
    /// - **Extract**: Core extraction, ranges, frequency promotion, edge cases, precision
    /// - **NormalizeUnit**: Unit canonicalization and idempotency
    /// - **BackfillPlaceboArms**: Table-scoped placebo dose backfill
    /// - **ScanAllColumnsForDose**: Multi-column dose discovery
    /// </remarks>
    /// <seealso cref="DoseExtractor"/>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="ArmDefinition"/>
    [TestClass]
    public class DoseExtractorTests
    {
        #region Extract — Core Dose Patterns

        /**************************************************************/
        /// <summary>
        /// Simple dose with compound unit: "600 mg/d" → (600, "mg/d").
        /// </summary>
        [TestMethod]
        public void Extract_SimpleMgPerDay_ReturnsDoseAndUnit()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("600 mg/d");

            Assert.AreEqual(600m, dose);
            Assert.AreEqual("mg/d", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Simple mg dose: "50 mg" → (50, "mg").
        /// </summary>
        [TestMethod]
        public void Extract_SimpleMg_ReturnsDoseAndUnit()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("50 mg");

            Assert.AreEqual(50m, dose);
            Assert.AreEqual("mg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// mg/day normalization: "50 mg/day" → (50, "mg/d").
        /// </summary>
        [TestMethod]
        public void Extract_MgPerDayLongForm_NormalizesToMgD()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("50 mg/day");

            Assert.AreEqual(50m, dose);
            Assert.AreEqual("mg/d", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// mcg/day normalization: "500 mcg/day" → (500, "mcg/d").
        /// </summary>
        [TestMethod]
        public void Extract_McgPerDay_NormalizesToMcgD()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("500 mcg/day");

            Assert.AreEqual(500m, dose);
            Assert.AreEqual("mcg/d", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Weight-based dose: "1 mg/kg" → (1, "mg/kg").
        /// </summary>
        [TestMethod]
        public void Extract_MgPerKg_PreservesCompoundUnit()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("1 mg/kg q12h");

            Assert.AreEqual(1m, dose);
            Assert.AreEqual("mg/kg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// BSA-based dose: "75 mg/m²" → (75, "mg/m²").
        /// </summary>
        [TestMethod]
        public void Extract_MgPerMSquared_PreservesCompoundUnit()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("75 mg/m²");

            Assert.AreEqual(75m, dose);
            Assert.AreEqual("mg/m²", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// IU dose: "100 IU" → (100, "IU").
        /// </summary>
        [TestMethod]
        public void Extract_InternationalUnits_ReturnsIU()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("100 IU");

            Assert.AreEqual(100m, dose);
            Assert.AreEqual("IU", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Units dose: "50 units" → (50, "U") after normalization.
        /// </summary>
        [TestMethod]
        public void Extract_Units_NormalizesToU()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("50 units");

            Assert.AreEqual(50m, dose);
            Assert.AreEqual("U", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// mL dose: "5 mL" → (5, "mL").
        /// </summary>
        [TestMethod]
        public void Extract_MilliLiters_ReturnsMl()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("5 mL oral");

            Assert.AreEqual(5m, dose);
            Assert.AreEqual("mL", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// µg → mcg normalization: "100 µg" → (100, "mcg").
        /// </summary>
        [TestMethod]
        public void Extract_MicroSign_NormalizesToMcg()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("100 µg");

            Assert.AreEqual(100m, dose);
            Assert.AreEqual("mcg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Embedded dose in treatment arm text: "Vivelle 0.025 mg/day" → (0.025, "mg/d").
        /// </summary>
        [TestMethod]
        public void Extract_EmbeddedDose_ExtractsCorrectly()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("Vivelle 0.025 mg/day");

            Assert.AreEqual(0.025m, dose);
            Assert.AreEqual("mg/d", unit);

            #endregion
        }

        #endregion Extract — Core Dose Patterns

        #region Extract — Range and Titration

        /**************************************************************/
        /// <summary>
        /// Range pattern takes max: "10-20 mg" → (20, "mg").
        /// </summary>
        [TestMethod]
        public void Extract_RangeHyphen_TakesMaxDose()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("10-20 mg");

            Assert.AreEqual(20m, dose);
            Assert.AreEqual("mg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Range with en-dash: "150–600 mg/d" → (600, "mg/d").
        /// </summary>
        [TestMethod]
        public void Extract_RangeEnDash_TakesMaxDose()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("150–600 mg/d");

            Assert.AreEqual(600m, dose);
            Assert.AreEqual("mg/d", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Range with em-dash: "5—20 mcg" → (20, "mcg").
        /// </summary>
        [TestMethod]
        public void Extract_RangeEmDash_TakesMaxDose()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("5—20 mcg");

            Assert.AreEqual(20m, dose);
            Assert.AreEqual("mcg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Decimal range: "0.5-2.0 mg/d" → (2.0, "mg/d").
        /// </summary>
        [TestMethod]
        public void Extract_DecimalRange_TakesMaxDose()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("0.5-2.0 mg/d");

            Assert.AreEqual(2.0m, dose);
            Assert.AreEqual("mg/d", unit);

            #endregion
        }

        #endregion Extract — Range and Titration

        #region Extract — Frequency Promotion

        /**************************************************************/
        /// <summary>
        /// "Once Daily" promotes mg → mg/d: "2 mg Once Daily" → (2, "mg/d").
        /// </summary>
        [TestMethod]
        public void Extract_OnceDaily_PromotesMgToMgD()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("2 mg Once Daily");

            Assert.AreEqual(2m, dose);
            Assert.AreEqual("mg/d", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// "daily" promotes mcg → mcg/d: "500 mcg daily" → (500, "mcg/d").
        /// </summary>
        [TestMethod]
        public void Extract_Daily_PromotesMcgToMcgD()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("500 mcg daily");

            Assert.AreEqual(500m, dose);
            Assert.AreEqual("mcg/d", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// "QD" promotes mg → mg/d: "50 mg QD" → (50, "mg/d").
        /// </summary>
        [TestMethod]
        public void Extract_QD_PromotesMgToMgD()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("50 mg QD");

            Assert.AreEqual(50m, dose);
            Assert.AreEqual("mg/d", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Compound units NOT promoted: "1 mg/kg daily" stays mg/kg.
        /// </summary>
        [TestMethod]
        public void Extract_CompoundUnitDaily_NoPromotion()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("1 mg/kg daily");

            Assert.AreEqual(1m, dose);
            Assert.AreEqual("mg/kg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// "1000 mg Once Daily" → (1000, "mg/d") — frequency promotion with large dose.
        /// </summary>
        [TestMethod]
        public void Extract_LargeDoseOnceDaily_PromotesCorrectly()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("1000 mg Once Daily");

            Assert.AreEqual(1000m, dose);
            Assert.AreEqual("mg/d", unit);

            #endregion
        }

        #endregion Extract — Frequency Promotion

        #region Extract — Percent Arm Pattern

        /**************************************************************/
        /// <summary>
        /// Percent arm: "% 10 mg" → (10, "mg").
        /// </summary>
        [TestMethod]
        public void Extract_PercentArm_ExtractsDose()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("% 10 mg");

            Assert.AreEqual(10m, dose);
            Assert.AreEqual("mg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Percent arm with mcg: "% 40 mcg" → (40, "mcg").
        /// </summary>
        [TestMethod]
        public void Extract_PercentArmMcg_ExtractsDose()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("% 40 mcg");

            Assert.AreEqual(40m, dose);
            Assert.AreEqual("mcg", unit);

            #endregion
        }

        #endregion Extract — Percent Arm Pattern

        #region Extract — Footnote Stripping

        /**************************************************************/
        /// <summary>
        /// Footnote dagger stripped: "600 mg†" → (600, "mg").
        /// </summary>
        [TestMethod]
        public void Extract_FootnoteDagger_StrippedBeforeExtraction()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("600 mg†");

            Assert.AreEqual(600m, dose);
            Assert.AreEqual("mg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Multiple footnote markers: "50 mg‡§" → (50, "mg").
        /// </summary>
        [TestMethod]
        public void Extract_MultipleFootnotes_AllStripped()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("50 mg‡§");

            Assert.AreEqual(50m, dose);
            Assert.AreEqual("mg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Footnote on drug+dose text: "Vivelle 0.075 mg/day†" → (0.075, "mg/d").
        /// Validates footnote stripping and /day normalization together.
        /// </summary>
        [TestMethod]
        public void Extract_EmbeddedDoseWithFootnote_StripsAndExtracts()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("Vivelle 0.075 mg/day†");

            Assert.AreEqual(0.075m, dose);
            Assert.AreEqual("mg/d", unit);

            #endregion
        }

        #endregion Extract — Footnote Stripping

        #region Extract — Sub-Cent Precision (Regression)

        /**************************************************************/
        /// <summary>
        /// REGRESSION: "0.025 mg" must return exactly 0.025, NOT 0.03.
        /// EF Core decimal(18,2) default caused silent rounding — this validates the extraction
        /// itself produces exact values. See <see cref="DoseExtractor.Extract"/>.
        /// </summary>
        [TestMethod]
        public void Extract_SubCentPrecision_0025_ExactDecimal()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("0.025 mg");

            Assert.AreEqual(0.025m, dose, "0.025 must not be rounded to 0.03");
            Assert.AreEqual("mg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// REGRESSION: "0.0375 mg" must return exactly 0.0375, NOT 0.04.
        /// </summary>
        [TestMethod]
        public void Extract_SubCentPrecision_00375_ExactDecimal()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("0.0375 mg");

            Assert.AreEqual(0.0375m, dose, "0.0375 must not be rounded to 0.04");
            Assert.AreEqual("mg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// REGRESSION: "0.075 mg" must return exactly 0.075, NOT 0.08.
        /// </summary>
        [TestMethod]
        public void Extract_SubCentPrecision_0075_ExactDecimal()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("0.075 mg");

            Assert.AreEqual(0.075m, dose, "0.075 must not be rounded to 0.08");
            Assert.AreEqual("mg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// REGRESSION: Full Vivelle arm text "Vivelle 0.025 mg/day†" must extract exactly 0.025.
        /// Exercises footnote stripping + embedded dose + /day normalization + precision together.
        /// </summary>
        [TestMethod]
        public void Extract_Vivelle0025_ExactPrecision()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("Vivelle 0.025 mg/day†");

            Assert.AreEqual(0.025m, dose, "Vivelle 0.025 must extract exactly 0.025");
            Assert.AreEqual("mg/d", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// REGRESSION: Full Vivelle arm text "Vivelle 0.0375 mg/day†" must extract exactly 0.0375.
        /// </summary>
        [TestMethod]
        public void Extract_Vivelle00375_ExactPrecision()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("Vivelle 0.0375 mg/day†");

            Assert.AreEqual(0.0375m, dose, "Vivelle 0.0375 must extract exactly 0.0375");
            Assert.AreEqual("mg/d", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Decimal dose "0.1 mg" → exactly 0.1.
        /// </summary>
        [TestMethod]
        public void Extract_DecimalDose_01_ExactDecimal()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("0.1 mg");

            Assert.AreEqual(0.1m, dose);
            Assert.AreEqual("mg", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Six decimal places: "0.000625 mg" → exactly 0.000625.
        /// Tests the full DECIMAL(18,6) range.
        /// </summary>
        [TestMethod]
        public void Extract_SixDecimalPlaces_ExactDecimal()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("0.000625 mg");

            Assert.AreEqual(0.000625m, dose, "Six-decimal-place values must not be rounded");
            Assert.AreEqual("mg", unit);

            #endregion
        }

        #endregion Extract — Sub-Cent Precision (Regression)

        #region Extract — Guard Clauses

        /**************************************************************/
        /// <summary>
        /// Null input → (null, null).
        /// </summary>
        [TestMethod]
        public void Extract_NullInput_ReturnsNullTuple()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract(null);

            Assert.IsNull(dose);
            Assert.IsNull(unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Empty string → (null, null).
        /// </summary>
        [TestMethod]
        public void Extract_EmptyString_ReturnsNullTuple()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("");

            Assert.IsNull(dose);
            Assert.IsNull(unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Whitespace-only → (null, null).
        /// </summary>
        [TestMethod]
        public void Extract_Whitespace_ReturnsNullTuple()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("   ");

            Assert.IsNull(dose);
            Assert.IsNull(unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// No digits (abbreviation-only): "PGB" → (null, null).
        /// </summary>
        [TestMethod]
        public void Extract_NoDigits_ReturnsNullTuple()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("PGB");

            Assert.IsNull(dose);
            Assert.IsNull(unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// "Placebo" — no digits → (null, null).
        /// </summary>
        [TestMethod]
        public void Extract_Placebo_ReturnsNullTuple()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("Placebo");

            Assert.IsNull(dose);
            Assert.IsNull(unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// "Any dose" → (null, null) — explicit guard.
        /// </summary>
        [TestMethod]
        public void Extract_AnyDose_ReturnsNullTuple()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("Any dose");

            Assert.IsNull(dose);
            Assert.IsNull(unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// "All doses" → (null, null) — explicit guard.
        /// </summary>
        [TestMethod]
        public void Extract_AllDoses_ReturnsNullTuple()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("All doses combined");

            Assert.IsNull(dose);
            Assert.IsNull(unit);

            #endregion
        }

        #endregion Extract — Guard Clauses

        #region NormalizeUnit

        /**************************************************************/
        /// <summary>
        /// mg/day → mg/d.
        /// </summary>
        [TestMethod]
        public void NormalizeUnit_MgPerDay_ReturnsMgD()
        {
            Assert.AreEqual("mg/d", DoseExtractor.NormalizeUnit("mg/day"));
        }

        /**************************************************************/
        /// <summary>
        /// mcg/day → mcg/d.
        /// </summary>
        [TestMethod]
        public void NormalizeUnit_McgPerDay_ReturnsMcgD()
        {
            Assert.AreEqual("mcg/d", DoseExtractor.NormalizeUnit("mcg/day"));
        }

        /**************************************************************/
        /// <summary>
        /// µg → mcg (micro sign normalization).
        /// </summary>
        [TestMethod]
        public void NormalizeUnit_MicroSign_ReturnsMcg()
        {
            Assert.AreEqual("mcg", DoseExtractor.NormalizeUnit("µg"));
        }

        /**************************************************************/
        /// <summary>
        /// µg/day → mcg/d (double normalization).
        /// </summary>
        [TestMethod]
        public void NormalizeUnit_MicroPerDay_ReturnsMcgD()
        {
            Assert.AreEqual("mcg/d", DoseExtractor.NormalizeUnit("µg/day"));
        }

        /**************************************************************/
        /// <summary>
        /// "units" → "U".
        /// </summary>
        [TestMethod]
        public void NormalizeUnit_UnitsLowercase_ReturnsU()
        {
            Assert.AreEqual("U", DoseExtractor.NormalizeUnit("units"));
        }

        /**************************************************************/
        /// <summary>
        /// "Unit" → "U".
        /// </summary>
        [TestMethod]
        public void NormalizeUnit_UnitSingular_ReturnsU()
        {
            Assert.AreEqual("U", DoseExtractor.NormalizeUnit("Unit"));
        }

        /**************************************************************/
        /// <summary>
        /// Idempotent: "mg/d" → "mg/d".
        /// </summary>
        [TestMethod]
        public void NormalizeUnit_AlreadyNormalized_Idempotent()
        {
            Assert.AreEqual("mg/d", DoseExtractor.NormalizeUnit("mg/d"));
        }

        /**************************************************************/
        /// <summary>
        /// "mg" unchanged.
        /// </summary>
        [TestMethod]
        public void NormalizeUnit_SimpleMg_Unchanged()
        {
            Assert.AreEqual("mg", DoseExtractor.NormalizeUnit("mg"));
        }

        /**************************************************************/
        /// <summary>
        /// Null → null.
        /// </summary>
        [TestMethod]
        public void NormalizeUnit_Null_ReturnsNull()
        {
            Assert.IsNull(DoseExtractor.NormalizeUnit(null));
        }

        /**************************************************************/
        /// <summary>
        /// Empty → null.
        /// </summary>
        [TestMethod]
        public void NormalizeUnit_Empty_ReturnsNull()
        {
            Assert.IsNull(DoseExtractor.NormalizeUnit(""));
        }

        /**************************************************************/
        /// <summary>
        /// Footnote in unit: "mg†" → "mg".
        /// </summary>
        [TestMethod]
        public void NormalizeUnit_FootnoteInUnit_Stripped()
        {
            Assert.AreEqual("mg", DoseExtractor.NormalizeUnit("mg†"));
        }

        /**************************************************************/
        /// <summary>
        /// Space in /day: "mg/ day" → "mg/d".
        /// </summary>
        [TestMethod]
        public void NormalizeUnit_SpaceInSlashDay_NormalizesToMgD()
        {
            Assert.AreEqual("mg/d", DoseExtractor.NormalizeUnit("mg/ day"));
        }

        #endregion NormalizeUnit

        #region BackfillPlaceboArms

        /**************************************************************/
        /// <summary>
        /// Two treatment arms with "mg" + one placebo → placebo gets Dose=0.0, DoseUnit="mg".
        /// </summary>
        [TestMethod]
        public void BackfillPlaceboArms_SimplePlacebo_GetsMajorityUnit()
        {
            #region implementation

            var observations = new List<ParsedObservation>
            {
                new() { TextTableID = 1, TreatmentArm = "Drug A", Dose = 10m, DoseUnit = "mg" },
                new() { TextTableID = 1, TreatmentArm = "Drug B", Dose = 20m, DoseUnit = "mg" },
                new() { TextTableID = 1, TreatmentArm = "Placebo", Dose = null, DoseUnit = null }
            };

            DoseExtractor.BackfillPlaceboArms(observations);

            var placebo = observations[2];
            Assert.AreEqual(0.0m, placebo.Dose);
            Assert.AreEqual("mg", placebo.DoseUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Mixed units: 2×mg, 1×mcg → placebo gets "mg" (majority wins).
        /// </summary>
        [TestMethod]
        public void BackfillPlaceboArms_MixedUnits_MajorityWins()
        {
            #region implementation

            var observations = new List<ParsedObservation>
            {
                new() { TextTableID = 1, TreatmentArm = "Arm A", Dose = 10m, DoseUnit = "mg" },
                new() { TextTableID = 1, TreatmentArm = "Arm B", Dose = 20m, DoseUnit = "mg" },
                new() { TextTableID = 1, TreatmentArm = "Arm C", Dose = 100m, DoseUnit = "mcg" },
                new() { TextTableID = 1, TreatmentArm = "Placebo", Dose = null, DoseUnit = null }
            };

            DoseExtractor.BackfillPlaceboArms(observations);

            Assert.AreEqual(0.0m, observations[3].Dose);
            Assert.AreEqual("mg", observations[3].DoseUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// No non-placebo arms have units → placebo left unchanged.
        /// </summary>
        [TestMethod]
        public void BackfillPlaceboArms_NoUnitsAvailable_PlaceboUnchanged()
        {
            #region implementation

            var observations = new List<ParsedObservation>
            {
                new() { TextTableID = 1, TreatmentArm = "Drug A", Dose = null, DoseUnit = null },
                new() { TextTableID = 1, TreatmentArm = "Placebo", Dose = null, DoseUnit = null }
            };

            DoseExtractor.BackfillPlaceboArms(observations);

            Assert.IsNull(observations[1].Dose);
            Assert.IsNull(observations[1].DoseUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Placebo already has Dose → not overwritten.
        /// </summary>
        [TestMethod]
        public void BackfillPlaceboArms_PlaceboAlreadyHasDose_NotOverwritten()
        {
            #region implementation

            var observations = new List<ParsedObservation>
            {
                new() { TextTableID = 1, TreatmentArm = "Drug A", Dose = 10m, DoseUnit = "mg" },
                new() { TextTableID = 1, TreatmentArm = "Placebo", Dose = 0.0m, DoseUnit = "mg" }
            };

            DoseExtractor.BackfillPlaceboArms(observations);

            Assert.AreEqual(0.0m, observations[1].Dose, "Should not overwrite existing dose");
            Assert.AreEqual("mg", observations[1].DoseUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Different TextTableIDs → scoped per table.
        /// </summary>
        [TestMethod]
        public void BackfillPlaceboArms_DifferentTables_ScopedPerTable()
        {
            #region implementation

            var observations = new List<ParsedObservation>
            {
                // Table 1: mg
                new() { TextTableID = 1, TreatmentArm = "Drug A", Dose = 10m, DoseUnit = "mg" },
                new() { TextTableID = 1, TreatmentArm = "Placebo", Dose = null, DoseUnit = null },
                // Table 2: mcg
                new() { TextTableID = 2, TreatmentArm = "Drug B", Dose = 500m, DoseUnit = "mcg" },
                new() { TextTableID = 2, TreatmentArm = "Placebo", Dose = null, DoseUnit = null }
            };

            DoseExtractor.BackfillPlaceboArms(observations);

            Assert.AreEqual("mg", observations[1].DoseUnit, "Table 1 placebo gets mg");
            Assert.AreEqual("mcg", observations[3].DoseUnit, "Table 2 placebo gets mcg");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null TextTableID → excluded from grouping, no backfill.
        /// </summary>
        [TestMethod]
        public void BackfillPlaceboArms_NullTextTableId_Excluded()
        {
            #region implementation

            var observations = new List<ParsedObservation>
            {
                new() { TextTableID = null, TreatmentArm = "Drug A", Dose = 10m, DoseUnit = "mg" },
                new() { TextTableID = null, TreatmentArm = "Placebo", Dose = null, DoseUnit = null }
            };

            DoseExtractor.BackfillPlaceboArms(observations);

            Assert.IsNull(observations[1].Dose, "Null TextTableID should not participate in grouping");

            #endregion
        }

        #endregion BackfillPlaceboArms

        #region ScanAllColumnsForDose

        /**************************************************************/
        /// <summary>
        /// Already has Dose → returns false, no change.
        /// </summary>
        [TestMethod]
        public void ScanAllColumns_AlreadyHasDose_ReturnsFalse()
        {
            #region implementation

            var obs = new ParsedObservation
            {
                Dose = 50m,
                DoseUnit = "mg",
                TreatmentArm = "100 mg arm"
            };

            var result = DoseExtractor.ScanAllColumnsForDose(obs);

            Assert.IsFalse(result);
            Assert.AreEqual(50m, obs.Dose, "Existing dose must not be overwritten");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Placebo arm → returns false (deferred to BackfillPlaceboArms).
        /// </summary>
        [TestMethod]
        public void ScanAllColumns_PlaceboArm_ReturnsFalse()
        {
            #region implementation

            var obs = new ParsedObservation
            {
                Dose = null,
                TreatmentArm = "Placebo",
                DoseRegimen = "10 mg"
            };

            var result = DoseExtractor.ScanAllColumnsForDose(obs);

            Assert.IsFalse(result);
            Assert.IsNull(obs.Dose);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Dose in DoseRegimen → extracted first (priority 1).
        /// </summary>
        [TestMethod]
        public void ScanAllColumns_DoseInDoseRegimen_ExtractedFirst()
        {
            #region implementation

            var obs = new ParsedObservation
            {
                Dose = null,
                DoseRegimen = "50 mg once daily",
                TreatmentArm = "100 mg fallback"
            };

            var result = DoseExtractor.ScanAllColumnsForDose(obs);

            Assert.IsTrue(result);
            Assert.AreEqual(50m, obs.Dose, "Should extract from DoseRegimen, not TreatmentArm");
            Assert.AreEqual("mg/d", obs.DoseUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Dose in TreatmentArm (DoseRegimen empty) → extracted from arm.
        /// </summary>
        [TestMethod]
        public void ScanAllColumns_DoseInTreatmentArm_ExtractedFromArm()
        {
            #region implementation

            var obs = new ParsedObservation
            {
                Dose = null,
                DoseRegimen = null,
                TreatmentArm = "Vivelle 0.025 mg/day"
            };

            var result = DoseExtractor.ScanAllColumnsForDose(obs);

            Assert.IsTrue(result);
            Assert.AreEqual(0.025m, obs.Dose);
            Assert.AreEqual("mg/d", obs.DoseUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Dose in ParameterName → extracted (priority 3).
        /// </summary>
        [TestMethod]
        public void ScanAllColumns_DoseInParameterName_ExtractedFromParam()
        {
            #region implementation

            var obs = new ParsedObservation
            {
                Dose = null,
                DoseRegimen = null,
                TreatmentArm = "Drug A",
                ParameterName = "10 mg dose level"
            };

            var result = DoseExtractor.ScanAllColumnsForDose(obs);

            Assert.IsTrue(result);
            Assert.AreEqual(10m, obs.Dose);
            Assert.AreEqual("mg", obs.DoseUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Dose in StudyContext (nothing in earlier columns) → extracted (priority 5).
        /// </summary>
        [TestMethod]
        public void ScanAllColumns_DoseInStudyContext_ExtractedAsLastResort()
        {
            #region implementation

            var obs = new ParsedObservation
            {
                Dose = null,
                DoseRegimen = null,
                TreatmentArm = "Drug A",
                ParameterName = "Headache",
                ParameterSubtype = null,
                StudyContext = "Target Dosage 200 mg/day"
            };

            var result = DoseExtractor.ScanAllColumnsForDose(obs);

            Assert.IsTrue(result);
            Assert.AreEqual(200m, obs.Dose);
            Assert.AreEqual("mg/d", obs.DoseUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// No dose anywhere → returns false.
        /// </summary>
        [TestMethod]
        public void ScanAllColumns_NoDoseAnywhere_ReturnsFalse()
        {
            #region implementation

            var obs = new ParsedObservation
            {
                Dose = null,
                DoseRegimen = null,
                TreatmentArm = "Drug A",
                ParameterName = "Headache",
                StudyContext = "Study Phase III"
            };

            var result = DoseExtractor.ScanAllColumnsForDose(obs);

            Assert.IsFalse(result);
            Assert.IsNull(obs.Dose);
            Assert.IsNull(obs.DoseUnit);

            #endregion
        }

        #endregion ScanAllColumnsForDose

        #region Extract - Expanded Dosing and Lab Units

        /**************************************************************/
        /// <summary>
        /// Comma-formatted dose values are parsed without losing the thousands
        /// separator.
        /// </summary>
        [TestMethod]
        public void Extract_CommaFormattedDose_ReturnsDoseAndUnit()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("1,000 mg once daily");

            Assert.AreEqual(1000m, dose);
            Assert.AreEqual("mg/d", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Concentration and volume-normalized dose units are recognized as
        /// complete units.
        /// </summary>
        [TestMethod]
        public void Extract_ExpandedDoseUnits_ReturnsCompleteUnit()
        {
            #region implementation

            var (suspensionDose, suspensionUnit) = DoseExtractor.Extract("250 mg/5 mL");
            var (rateDose, rateUnit) = DoseExtractor.Extract("2 mcg/kg/min");
            var (volumeDose, volumeUnit) = DoseExtractor.Extract("0.1 mL/kg");

            Assert.AreEqual(250m, suspensionDose);
            Assert.AreEqual("mg/5 mL", suspensionUnit);
            Assert.AreEqual(2m, rateDose);
            Assert.AreEqual("mcg/kg/min", rateUnit);
            Assert.AreEqual(0.1m, volumeDose);
            Assert.AreEqual("mL/kg", volumeUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Legacy arm split values such as 2g/day still expose the dose amount
        /// and simple gram unit expected by downstream standardization.
        /// </summary>
        [TestMethod]
        public void Extract_GPerDay_ReturnsSimpleGramUnit()
        {
            #region implementation

            var (dose, unit) = DoseExtractor.Extract("Mycophenolate Mofetil 2g/day");

            Assert.AreEqual(2m, dose);
            Assert.AreEqual("g", unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Clinical-lab CONCENTRATION units (g/dL, mg/dL) have a left numerator
        /// and are recognized structurally by Extract. The dosing parser
        /// intercepts these as lab thresholds at row level (via
        /// PopulationDetector.LooksLikeLabThresholdDoseModification) before
        /// the value is committed as a medication dose; this test only
        /// documents Extract's structural behavior.
        /// </summary>
        [TestMethod]
        public void Extract_ConcentrationLabUnits_StructurallyExtracts()
        {
            #region implementation

            var (hemoglobin, hemoglobinUnit) = DoseExtractor.Extract("Hemoglobin <8 g/dL");
            var (creatinine, creatinineUnit) = DoseExtractor.Extract("Creatinine >2 mg/dL");

            Assert.AreEqual(8m, hemoglobin);
            Assert.AreEqual("g/dL", hemoglobinUnit);
            Assert.AreEqual(2m, creatinine);
            Assert.AreEqual("mg/dL", creatinineUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Bare-denominator lab COUNT units (/mcL, /μL) have no left numerator
        /// and are therefore not dose units. Extract must return (null, null)
        /// — these tokens are recognized exclusively by
        /// PopulationDetector.LooksLikeLabThresholdDoseModification.
        /// </summary>
        [TestMethod]
        public void Extract_BareCountUnits_ReturnsNull()
        {
            #region implementation

            var (platelets, plateletUnit) = DoseExtractor.Extract("Platelet count <50,000/mcL");
            var (neutrophils, neutrophilUnit) = DoseExtractor.Extract("Neutrophil count <1,000/μL");

            Assert.IsNull(platelets);
            Assert.IsNull(plateletUnit);
            Assert.IsNull(neutrophils);
            Assert.IsNull(neutrophilUnit);

            #endregion
        }

        #endregion Extract - Expanded Dosing and Lab Units
    }
}
