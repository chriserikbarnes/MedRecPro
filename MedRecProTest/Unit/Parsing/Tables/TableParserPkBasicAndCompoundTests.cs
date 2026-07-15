using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Parsing.Tables
{
    public partial class TableParserTests
    {
        #region PkTableParser Tests

        /**************************************************************/
        /// <summary>
        /// PK parser produces one observation per data cell with correct parameter and dose.
        /// </summary>
        [TestMethod]
        public void PkParser_BasicTable_ProducesCorrectObservations()
        {
            var table = createTestTable(
                new[] { "Dose", "Cmax (mcg/mL)", "AUC (mcg·h/mL)", "t½ (hours)" },
                new List<string?[]>
                {
                    new[] { "50 mg oral", "0.29 (35%)", "1.2 (28%)", "30" },
                    new[] { "100 mg oral", "0.58 (32%)", "2.4 (25%)", "31" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(6, results.Count); // 2 rows × 3 params
            Assert.IsTrue(results.All(r => r.TableCategory == "PK"));

            var cmax50 = results.First(r => r.DoseRegimen == "50 mg oral" && r.ParameterName == "Cmax");
            Assert.AreEqual("mcg/mL", cmax50.Unit);
            Assert.AreEqual("Mean", cmax50.PrimaryValueType);
        }

        /**************************************************************/
        /// <summary>
        /// PK parser extracts unit from header parenthetical.
        /// </summary>
        [TestMethod]
        public void PkParser_ExtractsUnitFromHeader()
        {
            var table = createTestTable(
                new[] { "Dose", "Cmax (mcg/mL)" },
                new List<string?[]> { new[] { "50 mg", "0.29 (35%)" } },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("mcg/mL", results[0].Unit);
        }

        #endregion PkTableParser Tests

        #region PkTableParser Compound Layout Tests

        /**************************************************************/
        /// <summary>
        /// Creates a compound header PK table mimicking TextTableID 185:
        /// spanning header repeated across all columns, embedded sub-header row,
        /// SocDivider with context reset and refreshed sub-headers.
        /// </summary>
        private static ReconstructedTable createCompoundPkTable()
        {
            #region implementation

            var spanningHeader = "Pharmacokinetic Parameters for Renal Impairment";

            // All header columns have identical spanning text (as Stage 2 would produce)
            var table = createTestTable(
                new[]
                {
                    spanningHeader,
                    spanningHeader,
                    spanningHeader,
                    spanningHeader,
                    spanningHeader
                },
                new List<string?[]>
                {
                    // Row 0: Sub-header row (consumed, not emitted as observations)
                    new[] { null, "Dose", "Tmax (h)", "Cmax (mcg/mL)", "AUC(0-96h)(mcgh/mL)" },
                    // Row 1: Healthy Volunteers (renal section)
                    new[] { "Healthy Volunteers GFR greater than 80 mL/min/1.73 m (n=6)", "1 g", "0.75 (±0.27)", "25.3 (±7.99)", "45.0 (±22.6)" },
                    // Row 2: Mild Renal Impairment
                    new[] { "Mild Renal Impairment GFR 50 to 80 mL/min/1.73 m (n=6)", "1 g", "0.75 (±0.27)", "26.0 (±3.82)", "59.9 (±12.9)" },
                    // Row 3: Moderate Renal Impairment
                    new[] { "Moderate Renal Impairment GFR 25 to 49 mL/min/1.73 m (n=6)", "1 g", "0.75 (±0.27)", "19.0 (±13.2)", "52.9 (±25.5)" },
                    // Row 4: Severe Renal Impairment
                    new[] { "Severe Renal Impairment GFR less than 25 mL/min/1.73 m (n=7)", "1 g", "1.00 (±0.41)", "16.3 (±10.8)", "78.6 (±46.4)" },
                    // Row 5 (after SocDivider): Sub-header row for hepatic section
                    new[] { null, "Dose", "Tmax (h)", "Cmax(mcg/mL)", "AUC(0-48h)(mcgh/mL)" },
                    // Row 6: Healthy Volunteers (hepatic section)
                    new[] { "Healthy Volunteers (n=6)", "1 g", "0.63 (±0.14)", "24.3 (±5.73)", "29.0 (±5.78)" },
                    // Row 7: Alcoholic Cirrhosis
                    new[] { "Alcoholic Cirrhosis (n=18)", "1 g", "0.85 (±0.58)", "22.4 (±10.1)", "29.8 (±10.7)" }
                },
                parentSectionCode: "34090-1",
                sectionTitle: "12.3 Pharmacokinetics");

            // Override flags to match Stage 2 output for compound tables
            table.HasInferredHeader = true;
            table.HasExplicitHeader = false;
            table.HasSocDividers = true;

            // Insert SocDivider between renal and hepatic sections (after row index 4 = Severe Renal)
            // Data rows start at index 0 in the Rows list, so after 5 rows (sub-header + 4 data)
            insertSocDivider(table, 5, "Pharmacokinetic Parameters for Hepatic Impairment");

            return table;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Compound header layout is detected for table with identical spanning headers,
        /// SocDividers flag, InferredHeader flag, and sub-header first data row.
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_DetectedCorrectly()
        {
            var table = createCompoundPkTable();
            Assert.IsTrue(PkTableParser.detectCompoundHeaderLayout(table));
        }

        /**************************************************************/
        /// <summary>
        /// Compound header layout NOT detected for standard PK table with distinct headers.
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_NotDetected_NormalPk()
        {
            var table = createTestTable(
                new[] { "Dose", "Cmax (mcg/mL)", "AUC (mcg·h/mL)", "t½ (hours)" },
                new List<string?[]>
                {
                    new[] { "50 mg oral", "0.29 (35%)", "1.2 (28%)", "30" }
                },
                parentSectionCode: "34090-1");

            Assert.IsFalse(PkTableParser.detectCompoundHeaderLayout(table));
        }

        /**************************************************************/
        /// <summary>
        /// Compound header layout NOT detected when HasSocDividers is false,
        /// even if headers are identical.
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_NotDetected_NoSocDividers()
        {
            var table = createTestTable(
                new[] { "Same Header", "Same Header", "Same Header" },
                new List<string?[]>
                {
                    new[] { null, "Dose", "Cmax (mcg/mL)" },
                    new[] { "Group A (n=5)", "1 g", "25.3 (±7.99)" }
                },
                parentSectionCode: "34090-1");

            table.HasInferredHeader = true;
            table.HasSocDividers = false; // Explicitly false

            Assert.IsFalse(PkTableParser.detectCompoundHeaderLayout(table));
        }

        /**************************************************************/
        /// <summary>
        /// Compound layout produces correct ParameterName values from sub-header row.
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_ParsesParameterNames()
        {
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            var paramNames = results.Select(r => r.ParameterName).Distinct().OrderBy(n => n).ToList();
            CollectionAssert.AreEqual(
                new[] { "AUC", "Cmax", "Tmax" },
                paramNames);
        }

        /**************************************************************/
        /// <summary>
        /// ParameterCategory = "Renal Impairment" for rows from first section
        /// (before SocDivider).
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_ParsesCategory()
        {
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            var renalRows = results.Where(r =>
                r.TreatmentArm != null &&
                r.TreatmentArm.Contains("Renal", StringComparison.OrdinalIgnoreCase)).ToList();

            Assert.IsTrue(renalRows.Count > 0, "Should have renal rows");
            Assert.IsTrue(renalRows.All(r => r.ParameterCategory == "Renal Impairment"),
                $"Expected 'Renal Impairment', got '{renalRows.FirstOrDefault()?.ParameterCategory}'");
        }

        /**************************************************************/
        /// <summary>
        /// After SocDivider, ParameterCategory resets to "Hepatic Impairment".
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_SocDividerResetsCategory()
        {
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            var hepaticRows = results.Where(r =>
                r.TreatmentArm != null &&
                (r.TreatmentArm.Contains("Cirrhosis", StringComparison.OrdinalIgnoreCase) ||
                 (r.TreatmentArm.Contains("Volunteers", StringComparison.OrdinalIgnoreCase) &&
                  r.ParameterCategory == "Hepatic Impairment"))).ToList();

            Assert.IsTrue(hepaticRows.Count > 0, "Should have hepatic rows");
            Assert.IsTrue(hepaticRows.All(r => r.ParameterCategory == "Hepatic Impairment"),
                $"Expected 'Hepatic Impairment', got '{hepaticRows.FirstOrDefault()?.ParameterCategory}'");
        }

        /**************************************************************/
        /// <summary>
        /// After SocDivider, sub-header refresh produces different ParameterSubtype:
        /// AUC(0-48h) in hepatic section vs AUC(0-96h) in renal section.
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_SocDividerRefreshesParams()
        {
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // Renal section AUC should have subtype "AUC(0-96h)"
            var renalAuc = results.FirstOrDefault(r =>
                r.ParameterName == "AUC" && r.ParameterCategory == "Renal Impairment");
            Assert.IsNotNull(renalAuc, "Should have a renal AUC observation");
            Assert.AreEqual("AUC(0-96h)", renalAuc.ParameterSubtype);

            // Hepatic section AUC should have subtype "AUC(0-48h)"
            var hepaticAuc = results.FirstOrDefault(r =>
                r.ParameterName == "AUC" && r.ParameterCategory == "Hepatic Impairment");
            Assert.IsNotNull(hepaticAuc, "Should have a hepatic AUC observation");
            Assert.AreEqual("AUC(0-48h)", hepaticAuc.ParameterSubtype);
        }

        /**************************************************************/
        /// <summary>
        /// Column 0 row labels map to TreatmentArm (not DoseRegimen).
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_RowLabelToTreatmentArm()
        {
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // R1.1 — Every observation gets col 0 routed to its contract-correct
            // column. Population descriptors ("Healthy Volunteers GFR...") go to
            // Population; unclassified labels ("Alcoholic Cirrhosis (n=18)") keep
            // going to TreatmentArm (pre-R1.1 behavior via Unknown fallback).
            Assert.IsTrue(
                results.All(r =>
                    !string.IsNullOrWhiteSpace(r.TreatmentArm) ||
                    !string.IsNullOrWhiteSpace(r.Population)),
                "Every observation should have TreatmentArm OR Population populated");

            // "Healthy Volunteers ..." rows route to Population (per PK contract —
            // this is a population descriptor, not a drug name)
            var populations = results.Select(r => r.Population).Where(p => p != null).Distinct().ToList();
            Assert.IsTrue(populations.Any(p => p!.Contains("Healthy Volunteers")),
                "Healthy Volunteers rows should route to Population");

            // "Alcoholic Cirrhosis" is not a recognized population → falls to Unknown
            // → lands in TreatmentArm (compound-layout pre-R1.1 fallback)
            var armLabels = results.Select(r => r.TreatmentArm).Where(a => a != null).Distinct().ToList();
            Assert.IsTrue(armLabels.Any(a => a!.Contains("Alcoholic Cirrhosis")),
                "Unclassified labels like 'Alcoholic Cirrhosis' should fall through to TreatmentArm");
        }

        /**************************************************************/
        /// <summary>
        /// ArmN extracted from "(n=X)" suffix in row labels.
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_ArmNExtraction()
        {
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // R1.1 — ArmN is extracted from col 0 "(n=X)" regardless of whether
            // col 0 routes to TreatmentArm or Population. Queries updated to look
            // in Population for recognized population labels.

            // Healthy Volunteers (n=6) — routes to Population
            var healthyRenal = results.First(r =>
                (r.Population?.Contains("Healthy Volunteers") == true) &&
                r.ParameterCategory == "Renal Impairment");
            Assert.AreEqual(6, healthyRenal.ArmN);

            // "Severe Renal Impairment GFR less than 25 mL/min/1.73 m (n=7)" —
            // the GFR form doesn't match the Creatinine-Clearance regex, so it
            // falls to Unknown and lands in TreatmentArm (pre-R1.1 fallback).
            var severe = results.First(r =>
                r.TreatmentArm != null && r.TreatmentArm.Contains("Severe"));
            Assert.AreEqual(7, severe.ArmN);

            // Alcoholic Cirrhosis (n=18) — not a recognized population → TreatmentArm
            var cirrhosis = results.First(r =>
                r.TreatmentArm != null && r.TreatmentArm.Contains("Cirrhosis"));
            Assert.AreEqual(18, cirrhosis.ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// DoseRegimen = "1 g" from the Dose column (not from col 0 row labels).
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_DoseFromDoseColumn()
        {
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.IsTrue(results.All(r => r.DoseRegimen == "1 g"),
                $"Expected DoseRegimen '1 g', got '{results.FirstOrDefault()?.DoseRegimen}'");
        }

        /**************************************************************/
        /// <summary>
        /// Unit correctly extracted from sub-header parentheticals:
        /// "h" from "Tmax (h)", "mcg/mL" from "Cmax (mcg/mL)", "mcg·h/mL" from "AUC(0-96h)(mcgh/mL)".
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_UnitExtraction()
        {
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            var tmaxObs = results.First(r => r.ParameterName == "Tmax");
            Assert.AreEqual("h", tmaxObs.Unit);

            var cmaxObs = results.First(r => r.ParameterName == "Cmax");
            Assert.AreEqual("mcg/mL", cmaxObs.Unit);

            var aucObs = results.First(r => r.ParameterName == "AUC");
            Assert.AreEqual("mcg·h/mL", aucObs.Unit);
        }

        /**************************************************************/
        /// <summary>
        /// Compound header produces ParameterSubtype only for compound headers (AUC),
        /// not for simple headers (Tmax, Cmax).
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_CompoundSubtype()
        {
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // Tmax and Cmax should have null subtype
            var tmaxObs = results.First(r => r.ParameterName == "Tmax");
            Assert.IsNull(tmaxObs.ParameterSubtype);

            var cmaxObs = results.First(r => r.ParameterName == "Cmax");
            Assert.IsNull(cmaxObs.ParameterSubtype);

            // AUC should have subtype
            Assert.IsTrue(results.Where(r => r.ParameterName == "AUC")
                .All(r => r.ParameterSubtype != null));
        }

        /**************************************************************/
        /// <summary>
        /// Full compound table produces exactly 18 observations:
        /// 4 renal arms × 3 params + 2 hepatic arms × 3 params = 18.
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_CorrectObservationCount()
        {
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(18, results.Count,
                $"Expected 18 observations, got {results.Count}");
        }

        /**************************************************************/
        /// <summary>
        /// SocDivider rows do NOT produce observation rows.
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_NoSocDividerObservations()
        {
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // No observation should have the SocDivider text as its TreatmentArm or ParameterName
            Assert.IsFalse(results.Any(r =>
                r.TreatmentArm?.Contains("Pharmacokinetic Parameters") == true ||
                r.ParameterName?.Contains("Pharmacokinetic") == true));
        }

        /**************************************************************/
        /// <summary>
        /// Tmax with unit "h" is detected as time measurement: Time and TimeUnit populated.
        /// </summary>
        [TestMethod]
        public void PkParser_CompoundHeader_TimeParamDetected()
        {
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // R1.1 — "Healthy Volunteers ..." routes to Population; query updated
            // accordingly. Tmax column should still yield Time / TimeUnit from the
            // PrimaryValue override in parseAndApplyPkValue.
            var tmaxObs = results.First(r =>
                r.ParameterName == "Tmax" &&
                (r.Population?.Contains("Healthy Volunteers") == true) &&
                r.ParameterCategory == "Renal Impairment");

            Assert.AreEqual(0.75, tmaxObs.Time);
            Assert.AreEqual("hours", tmaxObs.TimeUnit);
        }

        /**************************************************************/
        /// <summary>
        /// parseCompoundParameterHeader correctly handles compound AUC headers.
        /// </summary>
        [TestMethod]
        public void PkParser_ParseCompoundParameterHeader_CompoundAuc()
        {
            var (name, unit, subtype) = PkTableParser.parseCompoundParameterHeader("AUC(0-96h)(mcgh/mL)");
            Assert.AreEqual("AUC", name);
            Assert.AreEqual("mcg·h/mL", unit);
            Assert.AreEqual("AUC(0-96h)", subtype);
        }

        /**************************************************************/
        /// <summary>
        /// parseCompoundParameterHeader correctly handles simple single-parenthetical headers.
        /// </summary>
        [TestMethod]
        public void PkParser_ParseCompoundParameterHeader_Simple()
        {
            var (name, unit, subtype) = PkTableParser.parseCompoundParameterHeader("Cmax (mcg/mL)");
            Assert.AreEqual("Cmax", name);
            Assert.AreEqual("mcg/mL", unit);
            Assert.IsNull(subtype);
        }

        /**************************************************************/
        /// <summary>
        /// parseCompoundParameterHeader correctly handles headers with no parentheticals.
        /// </summary>
        [TestMethod]
        public void PkParser_ParseCompoundParameterHeader_NoUnit()
        {
            var (name, unit, subtype) = PkTableParser.parseCompoundParameterHeader("Dose");
            Assert.AreEqual("Dose", name);
            Assert.IsNull(unit);
            Assert.IsNull(subtype);
        }

        /**************************************************************/
        /// <summary>
        /// extractArmNFromLabel extracts sample size from "(n=X)" suffix.
        /// </summary>
        [TestMethod]
        public void PkParser_ExtractArmNFromLabel_WithN()
        {
            Assert.AreEqual(6, PkTableParser.extractArmNFromLabel("Healthy Volunteers (n=6)"));
            Assert.AreEqual(18, PkTableParser.extractArmNFromLabel("Alcoholic Cirrhosis (n=18)"));
            Assert.AreEqual(7, PkTableParser.extractArmNFromLabel("Severe Renal Impairment GFR less than 25 mL/min/1.73 m (n=7)"));
        }

        /**************************************************************/
        /// <summary>
        /// extractArmNFromLabel returns null when no "(n=X)" suffix is present.
        /// </summary>
        [TestMethod]
        public void PkParser_ExtractArmNFromLabel_WithoutN()
        {
            Assert.IsNull(PkTableParser.extractArmNFromLabel("Healthy Volunteers"));
            Assert.IsNull(PkTableParser.extractArmNFromLabel("Severe Renal Impairment GFR less than 25 mL/min/1.73 m"));
            Assert.IsNull(PkTableParser.extractArmNFromLabel(null));
        }

        #region PkTableParser Transposed Layout & Caption ArmN Tests

        /**************************************************************/
        /// <summary>
        /// Builds the Estradiol-style transposed PK table used by the transposed-layout
        /// sanity check: col 0 header is "Parameter", column headers are doses, and
        /// row labels are PK metrics like "AUC84(pg·hr/mL)".
        /// </summary>
        private static ReconstructedTable createTransposedPkTable(string? caption = null)
        {
            #region implementation

            return createTestTable(
                new[] { "Parameter", "0.1 mg/day", "0.05 mg/day", "0.025 mg/day" },
                new List<string?[]>
                {
                    new[] { "AUC84(pg·hr/mL)",  "5875 (1857)", "3057 (980)",  "1763 (600)" },
                    new[] { "AUC120(pg·hr/mL)", "6252 (1938)", "3320 (1038)", "1979 (648)" },
                    new[] { "Cmax(pg/mL)",      "117 (39.3)",  "56.6 (17.6)", "30.3 (11.1)" },
                    new[] { "Tmax(hr)",         "24.0 (8-60)", "24.0 (8-60)", "36.0 (8-84)" }
                },
                caption: caption,
                parentSectionCode: "34090-1",
                sectionTitle: "12.3 Pharmacokinetics");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// detectTransposedPkLayout returns true for an Estradiol-style table where col 0
        /// header is generic, all other headers are doses, and row labels are PK metrics.
        /// </summary>
        [TestMethod]
        public void PkParser_TransposedLayout_Detected()
        {
            var table = createTransposedPkTable();
            Assert.IsTrue(PkTableParser.detectTransposedPkLayout(table));
        }

        /**************************************************************/
        /// <summary>
        /// detectTransposedPkLayout returns false for a standard PK table — guards against
        /// accidental activation on well-formed canonical-layout PK tables.
        /// </summary>
        [TestMethod]
        public void PkParser_TransposedLayout_NotDetected_StandardLayout()
        {
            var table = createTestTable(
                new[] { "Dose", "Cmax (mcg/mL)", "AUC (mcg·h/mL)", "t½ (hours)" },
                new List<string?[]>
                {
                    new[] { "50 mg oral", "0.29 (35%)", "1.2 (28%)", "30" },
                    new[] { "100 mg oral", "0.58 (32%)", "2.4 (25%)", "31" }
                },
                parentSectionCode: "34090-1");

            Assert.IsFalse(PkTableParser.detectTransposedPkLayout(table));
        }

        /**************************************************************/
        /// <summary>
        /// detectTransposedPkLayout returns false when col 0 header is a population
        /// descriptor rather than a generic "Parameter" label.
        /// </summary>
        [TestMethod]
        public void PkParser_TransposedLayout_NotDetected_PopulationCol0()
        {
            var table = createTestTable(
                new[] { "Age Group", "0.1 mg/day", "0.05 mg/day" },
                new List<string?[]>
                {
                    new[] { "18-40",  "5875 (1857)", "3057 (980)" },
                    new[] { "41-65",  "5210 (1500)", "2800 (900)" }
                },
                parentSectionCode: "34090-1");

            Assert.IsFalse(PkTableParser.detectTransposedPkLayout(table));
        }

        /**************************************************************/
        /// <summary>
        /// detectTransposedPkLayout returns false when col 0 header is "Parameter" but
        /// the non-col-0 headers are not dose-shaped (e.g., still PK metric names).
        /// </summary>
        [TestMethod]
        public void PkParser_TransposedLayout_NotDetected_NonDoseHeaders()
        {
            var table = createTestTable(
                new[] { "Parameter", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "50 mg", "0.29 (35%)", "1.2 (28%)" }
                },
                parentSectionCode: "34090-1");

            Assert.IsFalse(PkTableParser.detectTransposedPkLayout(table));
        }

        /**************************************************************/
        /// <summary>
        /// End-to-end parse of the transposed Estradiol table produces observations with
        /// ParameterName = canonical PK metric, DoseRegimen = dose header, Unit extracted
        /// from the parenthesized metric, and the PK_TRANSPOSED_LAYOUT_SWAP flag. Both
        /// AUC84 and AUC120 collapse to the generic canonical AUC (non-standard intervals
        /// aren't in the 26-term canonical list per the data dictionary).
        /// </summary>
        [TestMethod]
        public void PkParser_TransposedLayout_SwapProducesCorrectObservations()
        {
            var table = createTransposedPkTable(
                caption: "Table 2: Mean (SD) Serum Pharmacokinetic Parameters of Baseline-Uncorrected Estradiol following a Single Dose of ESTRADIOL TRANSDERMAL SYSTEM (N=36)");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // 4 metrics × 3 doses = 12 observations
            Assert.AreEqual(12, results.Count);

            // ParameterName values: 3 distinct canonicals (AUC / Cmax / Tmax).
            // AUC84 and AUC120 both collapse to the generic "AUC" canonical.
            var paramNames = results.Select(r => r.ParameterName).Distinct().OrderBy(n => n).ToList();
            CollectionAssert.AreEqual(
                new[] { "AUC", "Cmax", "Tmax" },
                paramNames);

            // 2 metrics × 3 doses = 6 AUC observations after collapse
            Assert.AreEqual(6, results.Count(r => r.ParameterName == "AUC"));
            Assert.AreEqual(3, results.Count(r => r.ParameterName == "Cmax"));
            Assert.AreEqual(3, results.Count(r => r.ParameterName == "Tmax"));

            // DoseRegimen carries the dose header, and Dose/DoseUnit are extracted.
            // Pick the pg·h/mL-unit AUC row for the 0.025 mg/day dose (came from AUC84 row).
            var aucLow = results.First(r => r.ParameterName == "AUC"
                                         && r.DoseRegimen == "0.025 mg/day"
                                         && r.Unit == "pg·h/mL");
            Assert.AreEqual(0.025m, aucLow.Dose);
            Assert.AreEqual("mg/d", aucLow.DoseUnit);
            Assert.IsTrue(aucLow.ValidationFlags?.Contains("PK_TRANSPOSED_LAYOUT_SWAP") == true,
                $"Expected PK_TRANSPOSED_LAYOUT_SWAP flag, got '{aucLow.ValidationFlags}'");

            // Cmax has a different unit
            var cmaxMid = results.First(r => r.ParameterName == "Cmax" && r.DoseRegimen == "0.05 mg/day");
            Assert.AreEqual("pg/mL", cmaxMid.Unit);
            Assert.AreEqual(0.05m, cmaxMid.Dose);

            // Caption ArmN fallback also applied (same test — N=36 in caption)
            Assert.IsTrue(results.All(r => r.ArmN == 36),
                "All observations should have ArmN=36 from caption fallback");
            Assert.IsTrue(results.All(r => r.ValidationFlags?.Contains("PK_CAPTION_ARMN_FALLBACK:36") == true));
        }

        /**************************************************************/
        /// <summary>
        /// applyCaptionArmNFallback populates ArmN on all observations when parser
        /// leaves ArmN null and the caption contains "(N=X)".
        /// </summary>
        [TestMethod]
        public void PkParser_CaptionArmN_Fallback_PopulatesNullArmN()
        {
            var table = createTestTable(
                new[] { "Dose", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "50 mg oral", "0.29 (35%)", "1.2 (28%)" },
                    new[] { "100 mg oral", "0.58 (32%)", "2.4 (25%)" }
                },
                caption: "Table 1: Mean PK Parameters in Healthy Subjects (N=24)",
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.All(r => r.ArmN == 24),
                "All observations should pick up ArmN=24 from caption");
            Assert.IsTrue(results.All(r => r.ValidationFlags?.Contains("PK_CAPTION_ARMN_FALLBACK:24") == true));
        }

        /**************************************************************/
        /// <summary>
        /// applyCaptionArmNFallback does NOT override an ArmN value the parser already
        /// derived from the row label. Compound-header rows carry "(n=X)" per arm, and
        /// those values must be preserved.
        /// </summary>
        [TestMethod]
        public void PkParser_CaptionArmN_Fallback_DoesNotOverrideExisting()
        {
            var table = createCompoundPkTable();
            // Inject a conflicting caption N
            table.Caption = "Table X: Mean PK Parameters (N=99)";

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // R1.1 — Row-label-derived ArmN must be preserved regardless of
            // whether col 0 routed to TreatmentArm or Population.

            // Healthy Volunteers (n=6) — now in Population after R1.1
            var healthyRenal = results.FirstOrDefault(r =>
                (r.Population?.Contains("Healthy Volunteers") == true) &&
                r.ParameterCategory == "Renal Impairment");
            Assert.IsNotNull(healthyRenal);
            Assert.AreEqual(6, healthyRenal.ArmN, "Row-label ArmN must not be overridden");
            Assert.IsFalse(healthyRenal.ValidationFlags?.Contains("PK_CAPTION_ARMN_FALLBACK") == true,
                "No fallback flag should be appended when ArmN was already set");

            var cirrhosis = results.FirstOrDefault(r =>
                r.TreatmentArm != null && r.TreatmentArm.Contains("Cirrhosis"));
            Assert.IsNotNull(cirrhosis);
            Assert.AreEqual(18, cirrhosis.ArmN, "Row-label ArmN must not be overridden");
        }

        /**************************************************************/
        /// <summary>
        /// extractArmNFromCaption extracts the parenthesized N= value from caption text.
        /// Case-insensitive and comma-friendly.
        /// </summary>
        [TestMethod]
        public void PkParser_ExtractArmNFromCaption_ParenthesizedN()
        {
            Assert.AreEqual(36, PkTableParser.extractArmNFromCaption(
                "Table 2: Mean (SD) Serum PK Parameters following a Single Dose (N=36)"));
            Assert.AreEqual(1234, PkTableParser.extractArmNFromCaption(
                "Summary (n=1,234)"));
            Assert.AreEqual(24, PkTableParser.extractArmNFromCaption(
                "Healthy Subjects (N = 24)"));
        }

        /**************************************************************/
        /// <summary>
        /// extractArmNFromCaption returns null for captions that do not contain a
        /// parenthesized N= expression.
        /// </summary>
        [TestMethod]
        public void PkParser_ExtractArmNFromCaption_NoMatch()
        {
            Assert.IsNull(PkTableParser.extractArmNFromCaption("Table 2: Mean PK Parameters"));
            Assert.IsNull(PkTableParser.extractArmNFromCaption(""));
            Assert.IsNull(PkTableParser.extractArmNFromCaption(null));
            // Unparenthesized "N = 36" is intentionally not matched — avoids false positives
            Assert.IsNull(PkTableParser.extractArmNFromCaption("Population was N = 36 subjects"));
        }

        #endregion PkTableParser Transposed Layout & Caption ArmN Tests

        /**************************************************************/
        /// <summary>
        /// Existing basic PK test still passes — backward compatibility.
        /// </summary>
        [TestMethod]
        public void PkParser_BasicTable_StillWorks()
        {
            var table = createTestTable(
                new[] { "Dose", "Cmax (mcg/mL)", "AUC (mcg·h/mL)", "t½ (hours)" },
                new List<string?[]>
                {
                    new[] { "50 mg oral", "0.29 (35%)", "1.2 (28%)", "30" },
                    new[] { "100 mg oral", "0.58 (32%)", "2.4 (25%)", "31" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(6, results.Count);
            Assert.IsTrue(results.All(r => r.TableCategory == "PK"));

            var cmax50 = results.First(r => r.DoseRegimen == "50 mg oral" && r.ParameterName == "Cmax");
            Assert.AreEqual("mcg/mL", cmax50.Unit);
            Assert.AreEqual("Mean", cmax50.PrimaryValueType);
        }

        /**************************************************************/
        /// <summary>
        /// Existing unit extraction test still passes — backward compatibility.
        /// </summary>
        [TestMethod]
        public void PkParser_ExtractsUnitFromHeader_StillWorks()
        {
            var table = createTestTable(
                new[] { "Dose", "Cmax (mcg/mL)" },
                new List<string?[]> { new[] { "50 mg", "0.29 (35%)" } },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("mcg/mL", results[0].Unit);
        }

        /**************************************************************/
        /// <summary>
        /// PK parenthetical statistics over 100 are not emitted as percentages.
        /// </summary>
        [TestMethod]
        public void PkParser_ParentheticalStatisticOver100_IsDemotedFromPercentage()
        {
            var table = createTestTable(
                new[] { "Dose", "Cmax (ng/mL)" },
                new List<string?[]>
                {
                    new[] { "10 mg", "1086 (556)" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(1086.0, results[0].PrimaryValue);
            Assert.AreEqual("Mean", results[0].PrimaryValueType);
            Assert.AreEqual(556.0, results[0].SecondaryValue);
            Assert.AreNotEqual("Percentage", results[0].PrimaryValueType);

            var validationFlags = results[0].ValidationFlags;
            if (validationFlags is null)
            {
                Assert.Fail("Expected validation flags for demoted percent-like PK value but was null");
                return;
            }

            Assert.IsTrue(validationFlags.Contains("PCT_GT100_REJECTED:556"));
            Assert.IsTrue(validationFlags.Contains("PK_PCT_GT100_DEMOTED"));
        }

        #endregion PkTableParser Compound Layout Tests
    }
}

