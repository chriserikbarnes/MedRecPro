using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Consolidated unit tests for all Stage 3 table parsers and the router.
    /// Tests cover: PkTableParser, SimpleArmTableParser, MultilevelAeTableParser,
    /// AeWithSocTableParser, EfficacyMultilevelTableParser, BmdTableParser,
    /// TissueRatioTableParser, DosingTableParser, and TableParserRouter.
    /// </summary>
    /// <remarks>
    /// Tests use in-memory ReconstructedTable objects — no database or mocking needed.
    /// </remarks>
    [TestClass]
    public class TableParserTests
    {
        #region Test Helpers

        /**************************************************************/
        /// <summary>
        /// Creates a minimal reconstructed table with the given header texts and data rows.
        /// Column 0 is always the parameter name column.
        /// </summary>
        private static ReconstructedTable createTestTable(
            string?[] headerTexts,
            List<string?[]> dataRows,
            string? caption = null,
            string? parentSectionCode = null,
            string? sectionTitle = null,
            string? parentSectionTitle = null,
            int? headerRowCount = 1)
        {
            #region implementation

            var columns = new List<HeaderColumn>();
            for (int i = 0; i < headerTexts.Length; i++)
            {
                columns.Add(new HeaderColumn
                {
                    ColumnIndex = i,
                    LeafHeaderText = headerTexts[i],
                    HeaderPath = new List<string> { headerTexts[i] ?? "" },
                    CombinedHeaderText = headerTexts[i]
                });
            }

            var rows = new List<ReconstructedRow>();
            for (int r = 0; r < dataRows.Count; r++)
            {
                var cells = new List<ProcessedCell>();
                for (int c = 0; c < dataRows[r].Length; c++)
                {
                    cells.Add(new ProcessedCell
                    {
                        SequenceNumber = c + 1,
                        ResolvedColumnStart = c,
                        ResolvedColumnEnd = c + 1,
                        CleanedText = dataRows[r][c],
                        CellType = "td"
                    });
                }

                rows.Add(new ReconstructedRow
                {
                    SequenceNumberTextTableRow = r + 2,
                    Classification = RowClassification.DataBody,
                    AbsoluteRowIndex = r + 1,
                    Cells = cells
                });
            }

            return new ReconstructedTable
            {
                TextTableID = 1,
                Caption = caption,
                DocumentGUID = Guid.NewGuid(),
                Title = "Test Drug",
                VersionNumber = 1,
                ParentSectionCode = parentSectionCode,
                ParentSectionTitle = parentSectionTitle,
                SectionTitle = sectionTitle,
                LabelerName = "Test Lab",
                TotalColumnCount = headerTexts.Length,
                TotalRowCount = dataRows.Count + 1,
                HasExplicitHeader = true,
                HasInferredHeader = false,
                HasFooter = false,
                HasSocDividers = false,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = headerRowCount,
                    ColumnCount = headerTexts.Length,
                    Columns = columns
                },
                Rows = rows
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a multi-level header table with study context paths.
        /// </summary>
        private static ReconstructedTable createMultilevelTable(
            string[] studyContexts,
            string[] armTexts,
            List<string?[]> dataRows,
            string? caption = null)
        {
            #region implementation

            var columns = new List<HeaderColumn>();
            // Column 0 = parameter name
            columns.Add(new HeaderColumn
            {
                ColumnIndex = 0,
                LeafHeaderText = "Adverse Reaction",
                HeaderPath = new List<string> { "Adverse Reaction" },
                CombinedHeaderText = "Adverse Reaction"
            });

            // Arm columns with study context paths
            for (int i = 0; i < armTexts.Length; i++)
            {
                var context = i < studyContexts.Length ? studyContexts[i] : studyContexts[^1];
                columns.Add(new HeaderColumn
                {
                    ColumnIndex = i + 1,
                    LeafHeaderText = armTexts[i],
                    HeaderPath = new List<string> { context, armTexts[i] },
                    CombinedHeaderText = $"{context} > {armTexts[i]}"
                });
            }

            var rows = new List<ReconstructedRow>();
            for (int r = 0; r < dataRows.Count; r++)
            {
                var cells = new List<ProcessedCell>();
                for (int c = 0; c < dataRows[r].Length; c++)
                {
                    cells.Add(new ProcessedCell
                    {
                        SequenceNumber = c + 1,
                        ResolvedColumnStart = c,
                        ResolvedColumnEnd = c + 1,
                        CleanedText = dataRows[r][c],
                        CellType = "td"
                    });
                }

                rows.Add(new ReconstructedRow
                {
                    SequenceNumberTextTableRow = r + 3,
                    Classification = RowClassification.DataBody,
                    AbsoluteRowIndex = r + 2,
                    Cells = cells
                });
            }

            return new ReconstructedTable
            {
                TextTableID = 41,
                Caption = caption,
                DocumentGUID = Guid.NewGuid(),
                Title = "Test Drug",
                VersionNumber = 1,
                ParentSectionCode = "34084-4",
                LabelerName = "Test Lab",
                TotalColumnCount = armTexts.Length + 1,
                TotalRowCount = dataRows.Count + 2,
                HasExplicitHeader = true,
                HasSocDividers = false,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 2,
                    ColumnCount = armTexts.Length + 1,
                    Columns = columns
                },
                Rows = rows
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Inserts a SOC divider row into a table's row list at the specified index.
        /// </summary>
        private static void insertSocDivider(ReconstructedTable table, int insertIndex, string socName)
        {
            #region implementation

            var row = new ReconstructedRow
            {
                SequenceNumberTextTableRow = insertIndex + 1,
                Classification = RowClassification.SocDivider,
                AbsoluteRowIndex = insertIndex,
                SocName = socName,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell
                    {
                        SequenceNumber = 1,
                        ResolvedColumnStart = 0,
                        ResolvedColumnEnd = table.TotalColumnCount ?? 1,
                        CleanedText = socName,
                        CellType = "td",
                        ColSpan = table.TotalColumnCount
                    }
                }
            };

            table.Rows!.Insert(insertIndex, row);
            table.HasSocDividers = true;

            #endregion
        }

        #endregion Test Helpers

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
        /// "h" from "Tmax (h)", "mcg/mL" from "Cmax (mcg/mL)", "mcgh/mL" from "AUC(0-96h)(mcgh/mL)".
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
            Assert.AreEqual("mcgh/mL", aucObs.Unit);
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
            Assert.AreEqual("mcgh/mL", unit);
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
            // Pick the pg·hr/mL-unit AUC row for the 0.025 mg/day dose (came from AUC84 row).
            var aucLow = results.First(r => r.ParameterName == "AUC"
                                         && r.DoseRegimen == "0.025 mg/day"
                                         && r.Unit == "pg·hr/mL");
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

        #endregion PkTableParser Compound Layout Tests

        #region SimpleArmTableParser Tests

        /**************************************************************/
        /// <summary>
        /// Simple arm parser extracts arms from header and maps data cells.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_BasicAeTable_ProducesCorrectObservations()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug A (N=188) n(%)", "Placebo (N=183) n(%)" },
                new List<string?[]>
                {
                    new[] { "Nausea", "33 (17.6)", "10 (5.5)" },
                    new[] { "Headache", "25 (13.3)", "20 (10.9)" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count); // 2 rows × 2 arms
            Assert.IsTrue(results.All(r => r.TableCategory == "ADVERSE_EVENT"));

            var nausea_drugA = results.First(r => r.ParameterName == "Nausea" && r.TreatmentArm == "Drug A");
            Assert.AreEqual(17.6, nausea_drugA.PrimaryValue);
            Assert.AreEqual(188, nausea_drugA.ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// Simple arm parser detects subtype rows (empty data cells with parameter name).
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_DetectsSubtypeRows()
        {
            var table = createTestTable(
                new[] { "Endpoint", "Drug A (N=100)", "Placebo (N=100)" },
                new List<string?[]>
                {
                    new[] { "Components of primary endpoint", null, null },
                    new[] { "Death", "5 (5.0)", "10 (10.0)" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count); // only Death row produces data
            // In AE context, empty-data rows set ParameterCategory (SOC) not ParameterSubtype
            Assert.AreEqual("Components of primary endpoint", results[0].ParameterCategory);
        }

        #endregion SimpleArmTableParser Tests

        #region MultilevelAeTableParser Tests

        /**************************************************************/
        /// <summary>
        /// Multilevel AE parser assigns study context from colspan header path.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_AssignsStudyContext()
        {
            var table = createMultilevelTable(
                new[] { "Treatment", "Treatment", "Prevention", "Prevention" },
                new[] { "EVISTA (N=2557) %", "Placebo (N=2576) %", "EVISTA (N=581) %", "Placebo (N=584) %" },
                new List<string?[]>
                {
                    new[] { "Hot Flashes", "24.6", "18.3", "28.7", "21.2" }
                });

            var parser = new MultilevelAeTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            var treatmentEvista = results.First(r => r.TreatmentArm == "EVISTA" && r.StudyContext == "Treatment");
            Assert.AreEqual(24.6, treatmentEvista.PrimaryValue);
            Assert.AreEqual("Percentage", treatmentEvista.PrimaryValueType);
        }

        /**************************************************************/
        /// <summary>
        /// Multilevel AE parser propagates SOC from divider rows.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_PropagatesSocCategory()
        {
            var table = createMultilevelTable(
                new[] { "Treatment", "Treatment" },
                new[] { "Drug (N=100) %", "Placebo (N=100) %" },
                new List<string?[]>
                {
                    new[] { "Nausea", "5.0", "2.0" }
                });

            insertSocDivider(table, 0, "Body as a Whole");

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            Assert.IsTrue(results.All(r => r.ParameterCategory == "Body as a Whole"));
        }

        #endregion MultilevelAeTableParser Tests

        #region AeWithSocTableParser Tests

        /**************************************************************/
        /// <summary>
        /// AE with SOC parser propagates SOC name to ParameterCategory.
        /// </summary>
        [TestMethod]
        public void AeWithSocParser_PropagatesSocName()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (N=100)", "Placebo (N=100)" },
                new List<string?[]>
                {
                    new[] { "Nausea", "10 (10.0)", "5 (5.0)" },
                    new[] { "Vomiting", "3 (3.0)", "1 (1.0)" }
                },
                parentSectionCode: "34084-4");

            table.HasSocDividers = true;
            insertSocDivider(table, 0, "Gastrointestinal");

            var parser = new AeWithSocTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);

            Assert.IsTrue(results.All(r => r.ParameterCategory == "Gastrointestinal"));
            Assert.AreEqual(4, results.Count);
        }

        #endregion AeWithSocTableParser Tests

        #region EfficacyMultilevelTableParser Tests

        /**************************************************************/
        /// <summary>
        /// Efficacy multilevel parser handles stat columns and emits Comparison rows.
        /// PrimaryValueType is derived from header ("Relative Risk" → "RelativeRisk").
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_EmitsComparisonRows()
        {
            var table = createTestTable(
                new[] { "Endpoint", "EVISTA (N=5129)", "Placebo (N=5133)", "P-value", "Relative Risk (95% CI)" },
                new List<string?[]>
                {
                    new[] { "Clinical Vertebral Fractures", "61", "93", "0.003", "0.65 (0.53, 0.79)" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);

            // Should have arm rows + comparison row
            Assert.IsTrue(results.Count >= 2);
            var comparison = results.FirstOrDefault(r => r.TreatmentArm == "Comparison");
            Assert.IsNotNull(comparison);
            Assert.AreEqual(0.65, comparison!.PrimaryValue);
            Assert.AreEqual("RelativeRisk", comparison.PrimaryValueType);
        }

        /**************************************************************/
        /// <summary>
        /// Column sub-header row sets PrimaryValueType and Unit on arm observations.
        /// "Absolute Risk per 10,000 Women-Years" → AbsoluteRisk + unit.
        /// Arrow symbol "↔" propagates the previous column's sub-header.
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_SubHeaderRow_SetsUnitAndType()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk (95% CI)", "CE (n = 5,310)", "Placebo (n = 5,429)" },
                new List<string?[]>
                {
                    new[] { "-", "-", "Absolute Risk per 10,000 Women-Years", "↔" },
                    new[] { "Deep vein thrombosis", "1.47 (1.06–2.06)", "23", "15" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var ceObs = results.FirstOrDefault(r => r.TreatmentArm == "CE");
            Assert.IsNotNull(ceObs);
            Assert.AreEqual("AbsoluteRisk", ceObs!.PrimaryValueType);
            Assert.AreEqual("per 10,000 Women-Years", ceObs.Unit);

            var placeboObs = results.FirstOrDefault(r => r.TreatmentArm == "Placebo");
            Assert.IsNotNull(placeboObs);
            Assert.AreEqual("AbsoluteRisk", placeboObs!.PrimaryValueType);
            Assert.AreEqual("per 10,000 Women-Years", placeboObs.Unit);
        }

        /**************************************************************/
        /// <summary>
        /// CI level is extracted from stat column header "(95% nCI)" → BoundType = "95CI".
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_CILevelFromHeader_Sets95CI()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk CE vs. Placebo (95% nCI)", "CE (n = 100)", "Placebo (n = 100)" },
                new List<string?[]>
                {
                    new[] { "Stroke", "1.33 (1.15–1.68)", "45", "33" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var comparison = results.FirstOrDefault(r => r.TreatmentArm == "Comparison");
            Assert.IsNotNull(comparison);
            Assert.AreEqual("95CI", comparison!.BoundType);
        }

        /**************************************************************/
        /// <summary>
        /// Comparison PrimaryValueType derived from header "Relative Risk" → "RelativeRisk".
        /// Not the hardcoded "RelativeRiskReduction".
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_ComparisonType_RelativeRisk()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk CE vs. Placebo (95% nCI)", "CE (n = 100)", "Placebo (n = 100)" },
                new List<string?[]>
                {
                    new[] { "DVT", "1.47 (1.06–2.06)", "23", "15" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var comparison = results.FirstOrDefault(r => r.TreatmentArm == "Comparison");
            Assert.IsNotNull(comparison);
            Assert.AreEqual("RelativeRisk", comparison!.PrimaryValueType);
        }

        /**************************************************************/
        /// <summary>
        /// For binary "X vs. Y" comparison with 2 arms, Comparison ArmN = sum of arm Ns.
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_ComparisonArmN_BinaryVs_SumsArms()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk CE vs. Placebo (95% CI)", "CE (n = 5,310)", "Placebo (n = 5,429)" },
                new List<string?[]>
                {
                    new[] { "DVT", "1.47 (1.06–2.06)", "23", "15" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var comparison = results.FirstOrDefault(r => r.TreatmentArm == "Comparison");
            Assert.IsNotNull(comparison);
            Assert.AreEqual(10739, comparison!.ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// Without "vs." in header, Comparison ArmN remains null even with 2 arms.
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_ComparisonArmN_NoVs_RemainsNull()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk (95% CI)", "CE (n = 100)", "Placebo (n = 200)" },
                new List<string?[]>
                {
                    new[] { "DVT", "1.47 (1.06–2.06)", "23", "15" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var comparison = results.FirstOrDefault(r => r.TreatmentArm == "Comparison");
            Assert.IsNotNull(comparison);
            Assert.IsNull(comparison!.ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// With 3 arms, Comparison ArmN remains null even with "vs." in header.
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_ComparisonArmN_ThreeArms_RemainsNull()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk CE vs. Placebo (95% CI)", "CE (n = 100)", "Placebo (n = 200)", "Drug B (n = 150)" },
                new List<string?[]>
                {
                    new[] { "DVT", "1.47 (1.06–2.06)", "23", "15", "18" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var comparison = results.FirstOrDefault(r => r.TreatmentArm == "Comparison");
            Assert.IsNotNull(comparison);
            Assert.IsNull(comparison!.ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// Full WHI-style table integration test: validates ArmN, PrimaryValueType,
        /// Unit, BoundType, and Comparison ArmN all derived correctly.
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_WHITable_Integration()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk CE vs. Placebo (95% nCI)", "CE n = 5,310", "Placebo n = 5,429" },
                new List<string?[]>
                {
                    new[] { "-", "-", "Absolute Risk per 10,000 Women-Years", "↔" },
                    new[] { "Deep vein thrombosis", "1.47 (1.06–2.06)", "23", "15" },
                    new[] { "Hip fracture", "0.65 (0.45–0.94)", "12", "19" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            Assert.IsTrue(parser.CanParse(table));
            var results = parser.Parse(table);

            // DVT arm observations
            var dvtCe = results.FirstOrDefault(r =>
                r.ParameterName == "Deep vein thrombosis" && r.TreatmentArm == "CE");
            Assert.IsNotNull(dvtCe);
            Assert.AreEqual(5310, dvtCe!.ArmN, "CE ArmN from comma-formatted header");
            Assert.AreEqual(23.0, dvtCe.PrimaryValue);
            Assert.AreEqual("AbsoluteRisk", dvtCe.PrimaryValueType, "Type from sub-header");
            Assert.AreEqual("per 10,000 Women-Years", dvtCe.Unit, "Unit from sub-header");

            var dvtPlacebo = results.FirstOrDefault(r =>
                r.ParameterName == "Deep vein thrombosis" && r.TreatmentArm == "Placebo");
            Assert.IsNotNull(dvtPlacebo);
            Assert.AreEqual(5429, dvtPlacebo!.ArmN, "Placebo ArmN from comma-formatted header");
            Assert.AreEqual(15.0, dvtPlacebo.PrimaryValue);
            Assert.AreEqual("AbsoluteRisk", dvtPlacebo.PrimaryValueType, "Type propagated via ↔");
            Assert.AreEqual("per 10,000 Women-Years", dvtPlacebo.Unit, "Unit propagated via ↔");

            // DVT comparison observation
            var dvtComp = results.FirstOrDefault(r =>
                r.ParameterName == "Deep vein thrombosis" && r.TreatmentArm == "Comparison");
            Assert.IsNotNull(dvtComp);
            Assert.AreEqual(1.47, dvtComp!.PrimaryValue);
            Assert.AreEqual("RelativeRisk", dvtComp.PrimaryValueType, "Header-derived type");
            Assert.AreEqual("95CI", dvtComp.BoundType, "CI level from header (95% nCI)");
            Assert.AreEqual(1.06, dvtComp.LowerBound);
            Assert.AreEqual(2.06, dvtComp.UpperBound);
            Assert.AreEqual(10739, dvtComp.ArmN, "Sum of arm Ns for binary vs. comparison");

            // Hip fracture — verify second data row also gets sub-header context
            var hipCe = results.FirstOrDefault(r =>
                r.ParameterName == "Hip fracture" && r.TreatmentArm == "CE");
            Assert.IsNotNull(hipCe);
            Assert.AreEqual("AbsoluteRisk", hipCe!.PrimaryValueType);
            Assert.AreEqual("per 10,000 Women-Years", hipCe.Unit);
        }

        #endregion EfficacyMultilevelTableParser Tests

        #region BmdTableParser Tests

        /**************************************************************/
        /// <summary>
        /// BMD parser assigns timepoints from header and MeanPercentChange type.
        /// </summary>
        [TestMethod]
        public void BmdParser_AssignsTimepointsAndType()
        {
            var table = createTestTable(
                new[] { "Site", "12 Months %", "24 Months %", "36 Months %" },
                new List<string?[]>
                {
                    new[] { "Lumbar Spine", "2.0", "2.5", "2.6" },
                    new[] { "Femoral Neck", "1.3", "1.6", "1.8" }
                });

            var parser = new BmdTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(6, results.Count); // 2 sites × 3 timepoints
            Assert.IsTrue(results.All(r => r.PrimaryValueType == "MeanPercentChange"));
            Assert.IsTrue(results.All(r => r.Unit == "%"));

            var lumbar12 = results.First(r => r.ParameterName == "Lumbar Spine" && r.Timepoint == "12 Months");
            Assert.AreEqual(2.0, lumbar12.PrimaryValue);
        }

        #endregion BmdTableParser Tests

        #region TissueRatioTableParser Tests

        /**************************************************************/
        /// <summary>
        /// Tissue ratio parser produces Ratio-typed observations from 2-column table.
        /// </summary>
        [TestMethod]
        public void TissueRatioParser_ProducesRatioType()
        {
            var table = createTestTable(
                new[] { "Tissue", "Ratio" },
                new List<string?[]>
                {
                    new[] { "Sputum", "1.2" },
                    new[] { "Skin", "0.8" }
                });

            var parser = new TissueRatioTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.PrimaryValueType == "Ratio"));
            Assert.IsTrue(results.All(r => r.Unit == "ratio"));
        }

        /**************************************************************/
        /// <summary>
        /// Tissue ratio parser rejects tables with more than 2 columns.
        /// </summary>
        [TestMethod]
        public void TissueRatioParser_RejectsWideTable()
        {
            var table = createTestTable(
                new[] { "Tissue", "Ratio", "Extra" },
                new List<string?[]>());

            var parser = new TissueRatioTableParser();
            Assert.IsFalse(parser.CanParse(table));
        }

        #endregion TissueRatioTableParser Tests

        #region DosingTableParser Tests

        /**************************************************************/
        /// <summary>
        /// Dosing parser uses header text as unit context.
        /// </summary>
        [TestMethod]
        public void DosingParser_UsesHeaderAsUnit()
        {
            var table = createTestTable(
                new[] { "Population", "3 mg/kg", "6 mg/kg", "12 mg/kg" },
                new List<string?[]>
                {
                    new[] { "Pediatric", "100", "200", "400" }
                },
                parentSectionCode: "34068-7");

            var parser = new DosingTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(3, results.Count);
            Assert.AreEqual("3 mg/kg", results[0].Unit);
            Assert.AreEqual("6 mg/kg", results[1].Unit);
            Assert.AreEqual("12 mg/kg", results[2].Unit);
        }

        #endregion DosingTableParser Tests

        #region TableParserRouter Tests

        /**************************************************************/
        /// <summary>
        /// Router correctly categorizes by ParentSectionCode.
        /// </summary>
        [TestMethod]
        public void Router_CategorizesByParentSectionCode()
        {
            var parsers = createAllParsers();
            var router = new TableParserRouter(parsers);

            var pkTable = createTestTable(
                new[] { "Dose", "Cmax" },
                new List<string?[]> { new[] { "50mg", "1.0" } },
                parentSectionCode: "34090-1");

            var (category, parser) = router.Route(pkTable);
            Assert.AreEqual(TableCategory.PK, category);
            Assert.IsNotNull(parser);
            Assert.IsInstanceOfType(parser, typeof(PkTableParser));
        }

        /**************************************************************/
        /// <summary>
        /// Router skips patient info tables.
        /// </summary>
        [TestMethod]
        public void Router_SkipsPatientInfoTables()
        {
            var parsers = createAllParsers();
            var router = new TableParserRouter(parsers);

            var table = createTestTable(
                new[] { "A", "B" },
                new List<string?[]> { new[] { "1", "2" } },
                parentSectionCode: "68498-5");

            var (category, parser) = router.Route(table);
            Assert.AreEqual(TableCategory.SKIP, category);
            Assert.IsNull(parser);
        }

        /**************************************************************/
        /// <summary>
        /// Router skips single-column tables.
        /// </summary>
        [TestMethod]
        public void Router_SkipsSingleColumnTables()
        {
            var parsers = createAllParsers();
            var router = new TableParserRouter(parsers);

            var table = createTestTable(
                new[] { "Only" },
                new List<string?[]>());
            table.TotalColumnCount = 1;

            var (category, parser) = router.Route(table);
            Assert.AreEqual(TableCategory.SKIP, category);
        }

        /**************************************************************/
        /// <summary>
        /// Router skips NDC caption tables.
        /// </summary>
        [TestMethod]
        public void Router_SkipsNdcCaptionTables()
        {
            var parsers = createAllParsers();
            var router = new TableParserRouter(parsers);

            var table = createTestTable(
                new[] { "A", "B" },
                new List<string?[]> { new[] { "1", "2" } },
                caption: "NDC Number and Package Description");

            var (category, parser) = router.Route(table);
            Assert.AreEqual(TableCategory.SKIP, category);
        }

        /**************************************************************/
        /// <summary>
        /// Router selects multilevel AE parser over simple parser when header has 2+ rows.
        /// </summary>
        [TestMethod]
        public void Router_SelectsMultilevelAeForTwoRowHeader()
        {
            var parsers = createAllParsers();
            var router = new TableParserRouter(parsers);

            var table = createMultilevelTable(
                new[] { "Treatment", "Treatment" },
                new[] { "Drug (N=100) %", "Placebo (N=100) %" },
                new List<string?[]> { new[] { "Nausea", "5.0", "2.0" } });

            var (category, parser) = router.Route(table);
            Assert.AreEqual(TableCategory.ADVERSE_EVENT, category);
            Assert.IsInstanceOfType(parser, typeof(MultilevelAeTableParser));
        }

        /**************************************************************/
        /// <summary>
        /// Router falls back to SectionTitle categorization for unclassified sections.
        /// </summary>
        [TestMethod]
        public void Router_FallsBackToSectionTitle()
        {
            var parsers = createAllParsers();
            var router = new TableParserRouter(parsers);

            var table = createTestTable(
                new[] { "Dose", "Cmax" },
                new List<string?[]> { new[] { "50mg", "1.0" } },
                parentSectionCode: "42229-5",
                sectionTitle: "Pharmacokinetics in Special Populations");

            var (category, parser) = router.Route(table);
            Assert.AreEqual(TableCategory.PK, category);
        }

        /**************************************************************/
        /// <summary>
        /// Creates one instance of every parser for router tests.
        /// </summary>
        private static List<ITableParser> createAllParsers()
        {
            return new List<ITableParser>
            {
                new PkTableParser(),
                new SimpleArmTableParser(),
                new MultilevelAeTableParser(),
                new AeWithSocTableParser(),
                new EfficacyMultilevelTableParser(),
                new BmdTableParser(),
                new TissueRatioTableParser(),
                new DosingTableParser()
            };
        }

        #endregion TableParserRouter Tests

        #region Arm Header Parsing Tests

        /**************************************************************/
        /// <summary>
        /// Verifies lowercase n with spaces in parenthesized format is parsed correctly.
        /// Covers Issue 1: "Paroxetine (n = 421) %" was not matched by original regex.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_LowercaseNWithSpaces_ExtractsCorrectly()
        {
            #region implementation

            var arm = ValueParser.ParseArmHeader("Paroxetine (n = 421) %");

            Assert.IsNotNull(arm);
            Assert.AreEqual("Paroxetine", arm.Name);
            Assert.AreEqual(421, arm.SampleSize);
            Assert.AreEqual("%", arm.FormatHint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies no-parentheses N format is parsed correctly.
        /// Covers Issue 4: "Placebo n = 51 %" has no parentheses around N clause.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_NoParentheses_ExtractsCorrectly()
        {
            #region implementation

            var arm = ValueParser.ParseArmHeader("Placebo n = 51 %");

            Assert.IsNotNull(arm);
            Assert.AreEqual("Placebo", arm.Name);
            Assert.AreEqual(51, arm.SampleSize);
            Assert.AreEqual("%", arm.FormatHint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Regression guard: original uppercase N with no spaces still works.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_UppercaseNNoSpaces_StillWorks()
        {
            #region implementation

            var arm = ValueParser.ParseArmHeader("EVISTA(N=2557)n(%)");

            Assert.IsNotNull(arm);
            Assert.AreEqual("EVISTA", arm.Name);
            Assert.AreEqual(2557, arm.SampleSize);
            Assert.AreEqual("n(%)", arm.FormatHint);

            #endregion
        }

        #endregion Arm Header Parsing Tests

        #region SimpleArmTableParser — AE Category Propagation Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that empty-data rows in AE tables set ParameterCategory (SOC)
        /// rather than ParameterSubtype.
        /// Covers Issue 2: ParameterCategory was NULL for all rows.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_AeTable_EmptyDataRowsSetsCategory()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Body System/ Adverse Reaction", "Paroxetine (n = 421) %", "Placebo (n = 421) %" },
                new List<string?[]>
                {
                    new[] { "Body as a Whole", null, null },
                    new[] { "Headache", "18", "17" },
                    new[] { "Asthenia", "15", "6" },
                    new[] { "Cardiovascular", null, null },
                    new[] { "Palpitation", "3", "1" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            // 3 data rows x 2 arms = 6 observations
            Assert.AreEqual(6, results.Count);

            var headache = results.First(r => r.ParameterName == "Headache" && r.TreatmentArm == "Paroxetine");
            Assert.AreEqual("Body as a Whole", headache.ParameterCategory);
            Assert.AreEqual(18.0, headache.PrimaryValue);
            Assert.AreEqual(421, headache.ArmN);

            var palpitation = results.First(r => r.ParameterName == "Palpitation" && r.TreatmentArm == "Paroxetine");
            Assert.AreEqual("Cardiovascular", palpitation.ParameterCategory);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies bare numbers are promoted to Percentage when arm header contains "%".
        /// Covers Issue 3: PrimaryValueType was "Numeric" instead of "Percentage".
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_LowercaseNHeader_PromotesToPercentage()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (n = 100) %", "Placebo (n = 100) %" },
                new List<string?[]>
                {
                    new[] { "Nausea", "15", "5" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.PrimaryValueType == "Percentage"));
            Assert.IsTrue(results.All(r => r.ArmN == 100));
            Assert.IsTrue(results.All(r => r.Unit == "%"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies no-parentheses N format extracts ArmN and promotes to Percentage.
        /// Covers Issue 4: "N = 51 %" in Table 7 was not parsed.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_NoParenNFormat_ExtractsArmN()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug n = 51 %", "Placebo n = 48 %" },
                new List<string?[]>
                {
                    new[] { "Headache", "10", "8" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            var drug = results.First(r => r.TreatmentArm == "Drug");
            Assert.AreEqual(51, drug.ArmN);
            Assert.AreEqual("Percentage", drug.PrimaryValueType);

            var placebo = results.First(r => r.TreatmentArm == "Placebo");
            Assert.AreEqual(48, placebo.ArmN);
            Assert.AreEqual("Percentage", placebo.PrimaryValueType);

            #endregion
        }

        #endregion SimpleArmTableParser — AE Category Propagation Tests

        #region MultilevelAeTableParser — Lowercase N Header Tests

        /**************************************************************/
        /// <summary>
        /// Verifies MultilevelAeTableParser correctly parses multi-indication tables
        /// with lowercase n arm headers. StudyContext should capture the indication.
        /// Covers Issue 5: Multi-indication table was entirely skipped.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_LowercaseNHeaders_ParsesCorrectly()
        {
            #region implementation

            var table = createMultilevelTable(
                new[] { "OCD", "OCD", "Panic Disorder", "Panic Disorder" },
                new[] { "Paroxetine (n = 542) %", "Placebo (n = 265) %",
                        "Paroxetine (n = 469) %", "Placebo (n = 324) %" },
                new List<string?[]>
                {
                    new[] { "Nausea", "23", "10", "22", "17" }
                });

            var parser = new MultilevelAeTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);

            var ocdParoxetine = results.First(r =>
                r.StudyContext == "OCD" && r.TreatmentArm == "Paroxetine");
            Assert.AreEqual(542, ocdParoxetine.ArmN);
            Assert.AreEqual("Percentage", ocdParoxetine.PrimaryValueType);
            Assert.AreEqual(23.0, ocdParoxetine.PrimaryValue);

            var panicPlacebo = results.First(r =>
                r.StudyContext == "Panic Disorder" && r.TreatmentArm == "Placebo");
            Assert.AreEqual(324, panicPlacebo.ArmN);
            Assert.AreEqual(17.0, panicPlacebo.PrimaryValue);

            #endregion
        }

        #endregion MultilevelAeTableParser — Lowercase N Header Tests

        #region Trailing Format Hint Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that headers like "Paroxetine %" strip the trailing "%"
        /// into FormatHint, leaving only the drug name as the arm Name.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_TrailingPercentHeader_StripsFormatHint()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Paroxetine %", "Placebo %" },
                new List<string?[]>
                {
                    new[] { "Nausea", "15", "5" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            var parox = results.First(r => r.TreatmentArm == "Paroxetine");
            Assert.AreEqual("Paroxetine", parox.TreatmentArm);
            Assert.AreEqual("Percentage", parox.PrimaryValueType);
            Assert.AreEqual("%", parox.Unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies type promotion works when FormatHint comes from trailing "%" stripping
        /// rather than from the N= regex.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_TrailingPercentHeader_PromotesToPercentage()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug A %", "Drug B %" },
                new List<string?[]>
                {
                    new[] { "Headache", "12", "8" },
                    new[] { "Nausea", "26", "9" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.All(r => r.PrimaryValueType == "Percentage"));

            #endregion
        }

        #endregion Trailing Format Hint Tests

        #region Body Row Enrichment Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that body rows with dose regimen cells (e.g., "10 mg", "20 mg")
        /// are consumed as enrichment and populate DoseRegimen on observations.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_DoseEnrichmentRows_ExtractsDoseRegimen()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Paroxetine", "Paroxetine" },
                new List<string?[]>
                {
                    new[] { "-", "10 mg", "20 mg" },           // dose enrichment row
                    new[] { "Headache", "5", "8" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            // Only the data row should produce observations (dose row consumed)
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("10 mg", results[0].DoseRegimen);
            Assert.AreEqual(10m, results[0].Dose, "Dose extracted from enrichment row");
            Assert.AreEqual("mg", results[0].DoseUnit);
            Assert.AreEqual("20 mg", results[1].DoseRegimen);
            Assert.AreEqual(20m, results[1].Dose, "Dose extracted from enrichment row");
            Assert.AreEqual("mg", results[1].DoseUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that body rows with n= cells are consumed as enrichment
        /// and set ArmN on observations.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_NEqualsEnrichmentRow_SetsArmN()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Paroxetine", "Placebo" },
                new List<string?[]>
                {
                    new[] { "-", "n = 102", "n = 50" },        // N= enrichment row
                    new[] { "Nausea", "15", "5" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(102, results.First(r => r.TreatmentArm == "Paroxetine").ArmN);
            Assert.AreEqual(50, results.First(r => r.TreatmentArm == "Placebo").ArmN);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that body rows with "%" cells are consumed as enrichment
        /// and set FormatHint which drives type promotion.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_FormatHintEnrichmentRow_SetsFormatHint()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Paroxetine", "Placebo" },
                new List<string?[]>
                {
                    new[] { "-", "%", "%" },                    // format hint enrichment row
                    new[] { "Nausea", "15", "5" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.PrimaryValueType == "Percentage"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that multiple consecutive enrichment rows (dose + N= + format)
        /// are all consumed and applied to arm definitions.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_MultiRowEnrichment_SkipsAllThree()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Paroxetine", "Paroxetine" },
                new List<string?[]>
                {
                    new[] { "-", "10 mg", "20 mg" },           // dose
                    new[] { "-", "n = 102", "n = 104" },       // N=
                    new[] { "-", "%", "%" },                    // format hint
                    new[] { "Nausea", "15", "25" }              // actual data
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            // Only the data row should produce observations
            Assert.AreEqual(2, results.Count);

            var arm10 = results.First(r => r.DoseRegimen == "10 mg");
            Assert.AreEqual(102, arm10.ArmN);
            Assert.AreEqual("Percentage", arm10.PrimaryValueType);
            Assert.AreEqual(15.0, arm10.PrimaryValue);

            var arm20 = results.First(r => r.DoseRegimen == "20 mg");
            Assert.AreEqual(104, arm20.ArmN);
            Assert.AreEqual(25.0, arm20.PrimaryValue);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies MultilevelAeTableParser strips trailing "%" from headers
        /// that have no N= value and promotes to Percentage.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_TrailingPercentHeader_ParsesCorrectly()
        {
            #region implementation

            var table = createMultilevelTable(
                new[] { "MDD", "MDD", "OCD", "OCD" },
                new[] { "Paroxetine %", "Placebo %", "Paroxetine %", "Placebo %" },
                new List<string?[]>
                {
                    new[] { "Nausea", "23", "10", "15", "8" }
                });

            var parser = new MultilevelAeTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);
            Assert.AreEqual(4, results.Count);

            var mddParox = results.First(r => r.StudyContext == "MDD" && r.TreatmentArm == "Paroxetine");
            Assert.AreEqual("Paroxetine", mddParox.TreatmentArm);
            Assert.AreEqual("Percentage", mddParox.PrimaryValueType);
            Assert.AreEqual(23.0, mddParox.PrimaryValue);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Body row with parenthesized N= cells (e.g., "(N=101)" / "(N=98)")
        /// must be consumed as enrichment and populate ArmN on the arms.
        /// This is the minimal regression test for the Table 9 (TextTableID
        /// 203, Topiramate pediatric epilepsy AE table) fix.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_ParenthesizedNEnrichmentRow_SetsArmN()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Placebo", "Topiramate" },
                new List<string?[]>
                {
                    new[] { "-", "(N=101)", "(N=98)" },       // parenthesized N= enrichment row
                    new[] { "Fatigue", "5", "16" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            // Only the data row produces observations; enrichment row is consumed
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(101, results.First(r => r.TreatmentArm == "Placebo").ArmN);
            Assert.AreEqual(98, results.First(r => r.TreatmentArm == "Topiramate").ArmN);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Exact shape seen in Table 9 of TextTableID 203: "(N =101 )" and
        /// "(N =98 )" — leading/trailing whitespace inside the parentheses.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_ParenthesizedNEnrichmentRow_WithInnerSpaces_SetsArmN()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Placebo", "Topiramate" },
                new List<string?[]>
                {
                    new[] { "-", "(N =101 )", "(N =98 )" },   // messy inner whitespace
                    new[] { "Injury", "13", "14" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(101, results.First(r => r.TreatmentArm == "Placebo").ArmN);
            Assert.AreEqual(98, results.First(r => r.TreatmentArm == "Topiramate").ArmN);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parenthesized N= enrichment row in an AE table with SOC dividers
        /// (the exact combination in TextTableID 203). Verifies that
        /// <see cref="AeWithSocTableParser"/> propagates ArmN to every
        /// observation after the enrichment row is consumed.
        /// </summary>
        [TestMethod]
        public void AeWithSocParser_ParenthesizedNEnrichmentRow_PropagatesArmN()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Placebo", "Topiramate" },
                new List<string?[]>
                {
                    new[] { "-", "(N =101 )", "(N =98 )" },
                    new[] { "Fatigue", "5", "16" },
                    new[] { "Injury", "13", "14" }
                },
                parentSectionCode: "34084-4");

            table.HasSocDividers = true;
            insertSocDivider(table, 1, "Body as a Whole - General Disorders");

            var parser = new AeWithSocTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count); // 2 params × 2 arms
            Assert.IsTrue(results.Where(r => r.TreatmentArm == "Placebo").All(r => r.ArmN == 101));
            Assert.IsTrue(results.Where(r => r.TreatmentArm == "Topiramate").All(r => r.ArmN == 98));
            Assert.IsTrue(results.All(r => r.ParameterCategory == "Body as a Whole - General Disorders"));

            #endregion
        }

        #endregion Body Row Enrichment Tests

        #region Caption StudyContext Extraction Tests

        // Concrete parser subclass used to exercise the protected-internal
        // BaseTableParser.extractStudyContextFromCaption helper directly.
        private sealed class CaptionStudyContextProbe : BaseTableParser
        {
            public override TableCategory SupportedCategory => TableCategory.ADVERSE_EVENT;
            public override int Priority => 999;
            public override bool CanParse(ReconstructedTable table) => false;
            public override List<ParsedObservation> Parse(ReconstructedTable table) => new();
            public static string? Extract(string? caption) => extractStudyContextFromCaption(caption);
        }

        /**************************************************************/
        /// <summary>
        /// The Table 9 (TextTableID 203) caption should produce a non-null
        /// StudyContext that preserves the full trial descriptor including
        /// the "Placebo-Controlled," qualifier.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_Table203Caption()
        {
            #region implementation

            const string caption =
                "Table 9: Incidence (%) of Treatment-Emergent Adverse Reactions in " +
                "Placebo-Controlled, Add-On Epilepsy Trials in Pediatric Patients " +
                "(Ages 2 -16 Years)<sup>*</sup>,<sup>+</sup>" +
                "(Reactions That Occurred in at Least 1% of Topiramate Tablets-Treated " +
                "Patients and Occurred More Frequently in Topiramate Tablets -Treated " +
                "Than Placebo-Treated Patients)";

            var result = CaptionStudyContextProbe.Extract(caption);

            Assert.IsNotNull(result);
            StringAssert.Contains(result!, "Add-On Epilepsy Trials");
            StringAssert.Contains(result, "Pediatric Patients");
            StringAssert.Contains(result, "Ages 2 -16 Years");
            StringAssert.StartsWith(result, "Placebo-Controlled");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Trailing <sup>*</sup> / <sup>†</sup> HTML footnote markers must
        /// be stripped from the extracted descriptor.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_StripsSupFootnote()
        {
            #region implementation

            const string caption =
                "Table 3: Adverse Reactions Reported in Clinical Trials of " +
                "Adult Patients With Hypertension<sup>*</sup>";

            var result = CaptionStudyContextProbe.Extract(caption);

            Assert.IsNotNull(result);
            Assert.IsFalse(result!.Contains("<sup>"), "HTML residue should be stripped");
            Assert.IsFalse(result.Contains('*'), "Bare footnote marker should be stripped");
            StringAssert.Contains(result, "Clinical Trials of Adult Patients With Hypertension");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Captions without an AE measure phrase (e.g., PK summaries) must
        /// return null — no guessing allowed.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_MissingMeasurePhrase_ReturnsNull()
        {
            #region implementation

            Assert.IsNull(CaptionStudyContextProbe.Extract(
                "Table 5: Clinical Pharmacology Summary for Healthy Volunteers"));
            Assert.IsNull(CaptionStudyContextProbe.Extract(
                "Table 1: Demographics of Study Population"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Captions with a measure phrase but no trial-descriptor connector
        /// (no "in"/"during"/"from"/etc.) must return null.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_MissingConnector_ReturnsNull()
        {
            #region implementation

            // Trailing punctuation only, no connector introducing a trial descriptor
            Assert.IsNull(CaptionStudyContextProbe.Extract("Table 4: Adverse Reactions."));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null / empty / whitespace captions short-circuit to null.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_NullOrEmpty_ReturnsNull()
        {
            #region implementation

            Assert.IsNull(CaptionStudyContextProbe.Extract(null));
            Assert.IsNull(CaptionStudyContextProbe.Extract(""));
            Assert.IsNull(CaptionStudyContextProbe.Extract("   "));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Real PK captions must return null — confirms the helper can be
        /// called indiscriminately without polluting non-AE parser output.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_PkCaption_ReturnsNull()
        {
            #region implementation

            Assert.IsNull(CaptionStudyContextProbe.Extract(
                "Table 2: Mean PK Parameters in Healthy Volunteers"));
            Assert.IsNull(CaptionStudyContextProbe.Extract(
                "Table 4: Pharmacokinetic Parameters Following Single-Dose " +
                "Administration in Subjects with Renal Impairment"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Locks in the accepted connector list: each connector word must
        /// successfully split the measure phrase from the trial descriptor.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_AlternateConnectors()
        {
            #region implementation

            Assert.AreEqual("the Double-Blind Phase of Study XYZ",
                CaptionStudyContextProbe.Extract(
                    "Table 5: Adverse Events During the Double-Blind Phase of Study XYZ"));

            Assert.AreEqual("Pooled Phase 3 Trials",
                CaptionStudyContextProbe.Extract(
                    "Table 6: Adverse Reactions Reported in Pooled Phase 3 Trials"));

            Assert.AreEqual("Patients with Renal Impairment",
                CaptionStudyContextProbe.Extract(
                    "Table 7: Adverse Events Observed in Patients with Renal Impairment"));

            Assert.AreEqual("Study ABC-123",
                CaptionStudyContextProbe.Extract(
                    "Table 8: Treatment-Emergent Adverse Reactions from Study ABC-123"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Full AeWithSocTableParser flow: when the header offers no
        /// StudyContext but the caption matches the AE grammar, every
        /// observation should inherit the caption-derived descriptor.
        /// </summary>
        [TestMethod]
        public void AeWithSocParser_CaptionStudyContextFallback_PopulatesObservations()
        {
            #region implementation

            const string caption =
                "Table 9: Incidence (%) of Treatment-Emergent Adverse Reactions in " +
                "Placebo-Controlled, Add-On Epilepsy Trials in Pediatric Patients " +
                "(Ages 2 -16 Years)";

            var table = createTestTable(
                new[] { "Adverse Reaction", "Placebo", "Topiramate" },
                new List<string?[]>
                {
                    new[] { "-", "(N =101 )", "(N =98 )" },
                    new[] { "Fatigue", "5", "16" }
                },
                caption: caption,
                parentSectionCode: "34084-4");

            table.HasSocDividers = true;
            insertSocDivider(table, 1, "Body as a Whole - General Disorders");

            var parser = new AeWithSocTableParser();
            var results = parser.Parse(table);

            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results.All(r => r.StudyContext != null));
            Assert.IsTrue(results.All(r => r.StudyContext!.Contains("Add-On Epilepsy Trials")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Multilevel AE parser: when both a header-derived StudyContext
        /// AND a caption-extractable context are available, the
        /// header-derived value must win.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_HeaderStudyContextWinsOverCaption()
        {
            #region implementation

            const string caption =
                "Table 10: Adverse Reactions Reported in Pooled Phase 3 Trials";

            var table = createMultilevelTable(
                new[] { "Treatment", "Treatment", "Prevention", "Prevention" },
                new[] { "EVISTA (N=2557) %", "Placebo (N=2576) %", "EVISTA (N=581) %", "Placebo (N=584) %" },
                new List<string?[]>
                {
                    new[] { "Hot Flashes", "24.6", "18.3", "28.7", "21.2" }
                },
                caption: caption);

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            // Header path carries "Treatment" / "Prevention" — caption fallback must NOT overwrite
            Assert.IsTrue(results.All(r => r.StudyContext == "Treatment" || r.StudyContext == "Prevention"));
            Assert.IsFalse(results.Any(r => r.StudyContext == "Pooled Phase 3 Trials"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// When the caption does not match the AE grammar, AeWithSocTableParser
        /// must leave StudyContext as null rather than inject garbage.
        /// </summary>
        [TestMethod]
        public void AeWithSocParser_NonAeCaption_LeavesStudyContextNull()
        {
            #region implementation

            const string caption = "Table 12: Demographics of Study Population";

            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (N=100)", "Placebo (N=100)" },
                new List<string?[]>
                {
                    new[] { "Nausea", "10 (10.0)", "5 (5.0)" }
                },
                caption: caption,
                parentSectionCode: "34084-4");

            table.HasSocDividers = true;
            insertSocDivider(table, 0, "Gastrointestinal");

            var parser = new AeWithSocTableParser();
            var results = parser.Parse(table);

            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results.All(r => r.StudyContext == null));

            #endregion
        }

        #endregion Caption StudyContext Extraction Tests

        #region PK Transposed Layout Relaxation — Long-Form English Row Labels

        /**************************************************************/
        /// <summary>
        /// Mirrors TextTableID=13202/22685/33852 (Ceftriaxone): col 0 header is
        /// blank/missing, dose headers occupy cols 1..n, row labels use long-form
        /// English PK names. Verifies the relaxed transposed-layout detection
        /// recovers observations and canonicalizes ParameterName via the
        /// <see cref="PkParameterDictionary"/>.
        /// </summary>
        [TestMethod]
        public void PkParser_TransposedLayout_BlankCol0Header_RecoversObservations()
        {
            var table = createTestTable(
                new[] { null, "50 mg/kg IV", "75 mg/kg IV" },
                new List<string?[]>
                {
                    new[] { "Maximum Plasma Concentrations (mcg/mL)", "216", "275" },
                    new[] { "Elimination Half-life (hour)", "4.6", "4.3" },
                    new[] { "Plasma Clearance (mL/hour/kg)", "49", "60" },
                    new[] { "Volume of Distribution (mL/kg)", "338", "373" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // 4 rows × 2 dose columns = 8 observations
            Assert.AreEqual(8, results.Count,
                "transposed layout should produce rows × dose-columns observations");

            // Every observation is PK
            Assert.IsTrue(results.All(r => r.TableCategory == "PK"));

            // Canonical names — collapsed via PkParameterDictionary
            var names = results.Select(r => r.ParameterName).Distinct().ToList();
            CollectionAssert.Contains(names, "Cmax");
            CollectionAssert.Contains(names, "t½");
            CollectionAssert.Contains(names, "CL");
            CollectionAssert.Contains(names, "Vd");

            // DoseRegimen carries the original column header, not the row label
            Assert.IsTrue(results.Any(r => r.DoseRegimen == "50 mg/kg IV"));
            Assert.IsTrue(results.Any(r => r.DoseRegimen == "75 mg/kg IV"));

            // Flagging
            Assert.IsTrue(
                results.Any(r => (r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_LAYOUT_SWAP")),
                "expected PK_TRANSPOSED_LAYOUT_SWAP on at least one observation");
            Assert.IsTrue(
                results.Any(r => (r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_CANONICALIZED")),
                "expected PK_TRANSPOSED_CANONICALIZED when long-form English is collapsed");
        }

        /**************************************************************/
        /// <summary>
        /// Regression test for the production 0-observation bug on TextTableID
        /// 13202/13203/22685/22686/33852 (Ceftriaxone). The trimmed 4-row test
        /// above passes because every row canonicalizes to PK; the production
        /// table has 7 rows where only 4 are PK metrics and the remaining 3
        /// (CSF Concentration, Range, Time after dose) are non-PK. Verifies the
        /// transposed-layout swap still fires at 4-of-7 majority and produces
        /// the expected 4 × 2 = 8 PK observations (non-PK rows may still be
        /// emitted but the swap must not be blocked by their presence).
        /// </summary>
        [TestMethod]
        public void PkParser_TransposedLayout_CeftriaxoneFullShape_RecoversObservations()
        {
            var table = createTestTable(
                new[] { null, "50 mg/kg IV", "75 mg/kg IV" },
                new List<string?[]>
                {
                    new[] { "Maximum Plasma Concentrations (mcg/mL)", "216", "275" },
                    new[] { "Elimination Half-life (hour)", "4.6", "4.3" },
                    new[] { "Plasma Clearance (mL/hour/kg)", "49", "60" },
                    new[] { "Volume of Distribution (mL/kg)", "338", "373" },
                    new[] { "CSF Concentration –inflamed meninges (mcg/mL)", "5.6", "6.4" },
                    new[] { "Range (mcg/mL)", "1.3 to 18.5", "1.3 to 44" },
                    new[] { "Time after dose (hour)", "3.7 (± 1.6)", "3.3 (± 1.4)" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // The 4 canonical PK rows × 2 dose columns MUST produce 8 observations.
            // Non-PK rows (CSF/Range/Time-after-dose) may add extras but the 4 canonical
            // metrics must be present after the swap.
            var pkCanonicalNames = new[] { "Cmax", "t½", "CL", "Vd" };
            foreach (var canonical in pkCanonicalNames)
            {
                Assert.IsTrue(
                    results.Any(r => r.ParameterName == canonical),
                    $"expected at least one observation with ParameterName == \"{canonical}\"");
            }

            // Each of the 4 canonical PK rows should have 2 dose observations.
            foreach (var canonical in pkCanonicalNames)
            {
                var dosesForParam = results
                    .Where(r => r.ParameterName == canonical)
                    .Select(r => r.DoseRegimen)
                    .ToList();
                Assert.IsTrue(dosesForParam.Contains("50 mg/kg IV"),
                    $"expected DoseRegimen=\"50 mg/kg IV\" for {canonical}");
                Assert.IsTrue(dosesForParam.Contains("75 mg/kg IV"),
                    $"expected DoseRegimen=\"75 mg/kg IV\" for {canonical}");
            }

            Assert.IsTrue(
                results.Any(r => (r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_LAYOUT_SWAP")),
                "expected PK_TRANSPOSED_LAYOUT_SWAP to fire at 4-of-7 majority");
        }

        #endregion PK Transposed Layout Relaxation

        #region PK Two-Column Population Routing

        /**************************************************************/
        /// <summary>
        /// Mirrors TextTableID=970 shape: col 0 carries CYP2C19 metabolizer
        /// phenotypes (Poor / Intermediate / Normal / Ultrarapid), col 1 is a
        /// dedicated dose column, and cols 2+ carry PK / PD parameters. Verifies
        /// phenotypes route to <c>Population</c> rather than being parked in
        /// <c>ParameterSubtype</c>.
        /// </summary>
        [TestMethod]
        public void PkParser_TwoColumnLayout_PhenotypeRowsRouteToPopulation()
        {
            var table = createTestTable(
                new[] { "Phenotype", "Dose", "Cmax (ng/mL)" },
                new List<string?[]>
                {
                    new[] { "Poor",          "300 mg (24 h)", "11 (4)" },
                    new[] { "Intermediate",  "300 mg (24 h)", "23 (11)" },
                    new[] { "Normal",        "300 mg (24 h)", "32 (21)" },
                    new[] { "Ultrarapid",    "300 mg (24 h)", "24 (10)" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.All(r => r.ParameterName == "Cmax"),
                "every row should carry canonical Cmax as ParameterName");

            // Population populated, ParameterSubtype empty
            Assert.IsTrue(results.Any(r => r.Population == "Poor Metabolizer"));
            Assert.IsTrue(results.Any(r => r.Population == "Intermediate Metabolizer"));
            Assert.IsTrue(results.Any(r => r.Population == "Normal Metabolizer"));
            Assert.IsTrue(results.Any(r => r.Population == "Ultrarapid Metabolizer"));
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.ParameterSubtype)));
            Assert.IsTrue(results.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_COL0_POP_ROUTED")));
        }

        #endregion PK Two-Column Population Routing

        #region PK R1 Row-Label Classification

        /**************************************************************/
        /// <summary>
        /// R1 — Two-column layout: col 0 = "Atorvastatin" (drug name) should
        /// route to TreatmentArm with the PK_COL0_ARM_ROUTED flag, not sit in
        /// ParameterSubtype (the pre-R1 fallback). ParameterName stays canonical.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_TwoColumn_BareDrugRoutesToTreatmentArm()
        {
            var table = createTestTable(
                new[] { "Co-administered Drug", "Dose", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Atorvastatin", "10 mg/day for 8 days", "0.83" },
                    new[] { "Carbamazepine", "200 mg twice a day", "0.97" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.ParameterName == "Cmax"));

            var atorv = results.First(r => r.TreatmentArm == "Atorvastatin");
            Assert.AreEqual("10 mg/day for 8 days", atorv.DoseRegimen);
            Assert.IsNull(atorv.ParameterSubtype);
            StringAssert.Contains(atorv.ValidationFlags ?? "", "PK_COL0_ARM_ROUTED");

            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.ParameterSubtype)));
        }

        /**************************************************************/
        /// <summary>
        /// R1 — Single-column layout: compound "Drug + Dose" row label splits
        /// into TreatmentArm (drug) + DoseRegimen (dose). Flag PK_COL0_ARM_DOSE_SPLIT.
        /// Simulates TID 571 shape after the Phase 2 context-column suppression
        /// (R2) has removed the explicit "Co-administered Drug" column.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_SingleColumn_DrugPlusDoseSplitsIntoArmAndRegimen()
        {
            var table = createTestTable(
                new[] { "Regimen", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Atorvastatin 10 mg/day for 8 days", "0.83", "1.01" },
                    new[] { "Carbamazepine 200 mg twice a day for 18 days", "0.97", "0.96" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);

            var atorvCmax = results.First(r => r.TreatmentArm == "Atorvastatin" && r.ParameterName == "Cmax");
            Assert.AreEqual("10 mg/day for 8 days", atorvCmax.DoseRegimen);
            StringAssert.Contains(atorvCmax.ValidationFlags ?? "", "PK_COL0_ARM_DOSE_SPLIT");

            var carbAuc = results.First(r => r.TreatmentArm == "Carbamazepine" && r.ParameterName == "AUC");
            Assert.AreEqual("200 mg twice a day for 18 days", carbAuc.DoseRegimen);
        }

        /**************************************************************/
        /// <summary>
        /// R1 — Single-column layout: row label is a population descriptor
        /// ("Healthy Subjects") even when the col 0 header doesn't match the
        /// population keyword set. Pre-R1: label went to DoseRegimen. Post-R1:
        /// label routes to Population with PK_COL0_POP_ROUTED flag.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_SingleColumn_PopulationLabelRoutesToPopulation()
        {
            var table = createTestTable(
                new[] { "Subject Group", "t½ (hours)", "CL (L/hour)" },
                new List<string?[]>
                {
                    new[] { "Healthy Subjects", "8.0", "1.0" },
                    new[] { "Renal Impairment", "14.7", "0.65" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.All(r => r.ParameterName == "t½" || r.ParameterName == "CL"));

            var healthy = results.Where(r => r.Population == "Healthy Volunteers").ToList();
            Assert.AreEqual(2, healthy.Count);
            Assert.IsTrue(healthy.All(r => string.IsNullOrWhiteSpace(r.DoseRegimen)));
            Assert.IsTrue(healthy.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_COL0_POP_ROUTED")));

            var renal = results.Where(r => r.Population == "Renal Impairment").ToList();
            Assert.AreEqual(2, renal.Count);
        }

        /**************************************************************/
        /// <summary>
        /// R1 — Single-column layout: row label matches the population regex
        /// second-pass (renal creatinine-clearance band) — should route to
        /// Population with the distinct PK_COL0_POP_ROUTED_REGEX flag so
        /// downstream reporting can distinguish dictionary vs regex matches.
        /// Mirrors TID 2069 "CLCR 50-80 mL/min" rows.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_SingleColumn_RenalBandRegexRoutesWithRegexFlag()
        {
            var table = createTestTable(
                new[] { "Subject Group", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Normal Creatinine Clearance greater than 80 mL/min", "5.5", "47.5" },
                    new[] { "Moderate Creatinine Clearance 30 to 50 mL/min",        "7.1", "182.1" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            // At least one row should have matched the regex second-pass
            Assert.IsTrue(results.Any(r =>
                (r.ValidationFlags ?? "").Contains("PK_COL0_POP_ROUTED_REGEX")),
                "expected at least one row with PK_COL0_POP_ROUTED_REGEX flag");
            Assert.IsTrue(results.All(r => !string.IsNullOrWhiteSpace(r.Population)));
        }

        /**************************************************************/
        /// <summary>
        /// R1 — Single-column layout: row label is a pure timepoint ("Day 14",
        /// "Single Dose") — should route to Timepoint / Time / TimeUnit and NOT
        /// pollute DoseRegimen. Flag PK_COL0_TIMEPOINT_ROUTED fires.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_SingleColumn_TimepointLabelRoutesToTimepoint()
        {
            var table = createTestTable(
                new[] { "Visit", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Day 14",      "0.44", "17.4" },
                    new[] { "Single Dose", "0.29", "1.2" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);

            var day14 = results.Where(r => r.Timepoint != null && r.Timepoint.Contains("14")).ToList();
            Assert.AreEqual(2, day14.Count);
            Assert.IsTrue(day14.All(r => string.IsNullOrWhiteSpace(r.DoseRegimen)));
            Assert.IsTrue(day14.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_COL0_TIMEPOINT_ROUTED")));

            var singleDose = results.Where(r => r.Timepoint != null && r.Timepoint.ToLower().Contains("single")).ToList();
            Assert.AreEqual(2, singleDose.Count);
        }

        /**************************************************************/
        /// <summary>
        /// R1 backward-compat guard: when col 0 is a pure dose regimen (no drug
        /// prefix, no population keyword, no timepoint pattern), the pre-R1
        /// behavior is preserved — DoseRegimen = col 0 text, no TreatmentArm,
        /// no special attribution flag. This is the dominant happy path for
        /// thousands of existing PK tables and must not regress.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_SingleColumn_PureDosePreservesBackwardCompat()
        {
            var table = createTestTable(
                new[] { "Dose", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "50 mg oral (once daily x 7 days)",  "0.29", "1.2" },
                    new[] { "100 mg oral (once daily x 14 days)", "0.58", "2.4" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            var first = results.First(r => r.DoseRegimen == "50 mg oral (once daily x 7 days)");
            Assert.AreEqual("Cmax", first.ParameterName);
            Assert.IsNull(first.TreatmentArm);
            // The happy-path dose row should not carry any of the R1 attribution flags
            Assert.IsFalse((first.ValidationFlags ?? "").Contains("PK_COL0_ARM_ROUTED"));
            Assert.IsFalse((first.ValidationFlags ?? "").Contains("PK_COL0_ARM_DOSE_SPLIT"));
            Assert.IsFalse((first.ValidationFlags ?? "").Contains("PK_COL0_TIMEPOINT_ROUTED"));
        }

        /**************************************************************/
        /// <summary>
        /// R1 backward-compat guard: col 0 text that doesn't match any classifier
        /// rule (a descriptive phrase like "Adults given 50 mg once daily N=12")
        /// falls through to Unknown. In single-column path this means the text
        /// lands in DoseRegimen just as it did pre-R1 — so existing Phase 2
        /// cleanups still fire on this content.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_SingleColumn_DescriptivePhraseFallsThroughToUnknown()
        {
            var table = createTestTable(
                new[] { "Regimen", "Cmax (ng/mL)" },
                new List<string?[]>
                {
                    new[] { "Adults given 50 mg once daily for 7 days N=12", "24.5" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            // Falls through to Unknown → col 0 lands in DoseRegimen (pre-R1 behavior)
            Assert.AreEqual("Adults given 50 mg once daily for 7 days N=12", results[0].DoseRegimen);
            Assert.IsNull(results[0].TreatmentArm);
            // No R1 attribution flag for Unknown
            Assert.IsFalse((results[0].ValidationFlags ?? "").Contains("PK_COL0_ARM_ROUTED"));
        }

        /**************************************************************/
        /// <summary>
        /// R1 backward-compat guard: when an AdverseEvent-shaped table is
        /// (incorrectly) routed to PkTableParser, the classifier must not
        /// misroute AE terms to Timepoint/TreatmentArm because of stray
        /// tokens. AdverseEvent tables are correctly handled by their own
        /// parsers; this test locks in that PkTableParser itself doesn't
        /// cross-contaminate AE rows if it ever sees them.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_AdverseEventShape_DoesNotCrossContaminate()
        {
            // AE-shaped row labels are typically MedDRA PTs like "Nausea" or
            // "Headache" — capitalized single tokens that could plausibly look
            // drug-shaped. Confirm they route to TreatmentArm ONLY when the PK
            // parser is explicitly invoked (router normally prevents this).
            var table = createTestTable(
                new[] { "Adverse Event", "Incidence (%)" },
                new List<string?[]>
                {
                    new[] { "Nausea",   "12.5" },
                    new[] { "Headache", "8.3" }
                },
                parentSectionCode: "34084-4"); // Adverse Reactions section code

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // The parser produces observations (no column-header unit), but critically
            // the R1 classifier should not generate spurious PK_COL0_TIMEPOINT_ROUTED
            // flags. Whatever routing happens, observations must remain internally
            // consistent (Timepoint empty when no timepoint pattern matched).
            Assert.IsTrue(results.All(r =>
                !((r.ValidationFlags ?? "").Contains("PK_COL0_TIMEPOINT_ROUTED"))),
                "AE terms must never be misrouted to Timepoint by the PK classifier");
        }

        #endregion PK R1 Row-Label Classification

        #region PK R1.1 PopulationDetector + Compound-Layout Routing

        /**************************************************************/
        /// <summary>
        /// R1.1 — Sex-stratum col 0 labels ("Male" / "Female") must route to
        /// Population via the expanded <see cref="PopulationDetector"/> dictionary,
        /// NOT fall through to the drug-name heuristic and land in TreatmentArm.
        /// This locks in the fix for the 196-row false-positive family observed
        /// in the 2026-04-21 corpus audit (TID 2069 Norfloxacin shape).
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_SexStratumRoutesToPopulation_NotTreatmentArm()
        {
            var table = createTestTable(
                new[] { "Regimen", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Male",   "5.5", "54.4" },
                    new[] { "Female", "7.0", "67.7" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.Any(r => r.Population == "Male"),
                "Male should route to Population");
            Assert.IsTrue(results.Any(r => r.Population == "Female"),
                "Female should route to Population");
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)),
                "Sex strata must NEVER route to TreatmentArm");
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — Bare age stratum labels ("Young", "Elderly") route to Population.
        /// Covers the 196-row false-positive family from the audit.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_BareAgeStratumRoutesToPopulation()
        {
            var table = createTestTable(
                new[] { "Regimen", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Young",   "5.5" },
                    new[] { "Elderly", "7.0" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Any(r => r.Population == "Young Adults"));
            Assert.IsTrue(results.Any(r => r.Population == "Elderly"));
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)));
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — Dialysis-status labels ("Hemodialysis", "CAPD") route to
        /// Population. Covers the 98-row "Hemodialysis" family from the audit.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_DialysisStatusRoutesToPopulation()
        {
            var table = createTestTable(
                new[] { "Regimen", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Hemodialysis", "5.7" },
                    new[] { "CAPD",         "6.9" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Any(r => r.Population == "Hemodialysis Patients"));
            Assert.IsTrue(results.Any(r => r.Population == "CAPD Patients"));
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)));
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — Multi-word population labels with descriptor trailers
        /// ("Elderly Subjects (mean age, 70.5 year)", "Healthy Subjects (N=18)")
        /// match the R1.1 age-qualified-Subjects regex and route to Population.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_ElderlySubjectsWithTrailerRoutesToPopulation()
        {
            var table = createTestTable(
                new[] { "Regimen", "t½ (hours)" },
                new List<string?[]>
                {
                    new[] { "Elderly Subjects (mean age, 70.5 year)", "8.9" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Elderly", results[0].Population);
            Assert.IsTrue(string.IsNullOrWhiteSpace(results[0].TreatmentArm));
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — ADME section-divider words ("Absorption", "Distribution",
        /// "Metabolism", "Elimination") must NOT route to TreatmentArm even
        /// though they pass the permissive drug-name heuristic. Negative list
        /// forces them to Unknown → DoseRegimen fallback. Covers the
        /// ~231-row false-positive family from the audit.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_AdmeDividerDoesNotRouteToTreatmentArm()
        {
            var table = createTestTable(
                new[] { "Section", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Absorption",   "5.5" },
                    new[] { "Distribution", "6.0" },
                    new[] { "Metabolism",   "4.2" },
                    new[] { "Elimination",  "3.8" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)),
                "ADME dividers must NEVER route to TreatmentArm");
            // They fall through to Unknown → col 0 → DoseRegimen (pre-R1 fallback)
            Assert.IsTrue(results.Any(r => r.DoseRegimen == "Absorption"));
            Assert.IsTrue(results.Any(r => r.DoseRegimen == "Metabolism"));
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — Generic header-echo words ("Parameter", "Subject", "Mean")
        /// must NOT route to TreatmentArm. Negative list catches them.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_GenericHeaderEchoDoesNotRouteToTreatmentArm()
        {
            var table = createTestTable(
                new[] { "Label", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Parameter", "5.5" },
                    new[] { "Subject",   "6.0" },
                    new[] { "Mean",      "4.2" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)),
                "Generic header echoes must NEVER route to TreatmentArm");
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — Food-state labels ("Fasted", "Light Breakfast", "High-Fat Meal")
        /// route to Timepoint via the extended <see cref="_timepointLabelPattern"/>
        /// instead of TreatmentArm. Covers the 72-row "Light Breakfast" family.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_FoodStateRoutesToTimepoint()
        {
            var table = createTestTable(
                new[] { "Condition", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Fasted",          "5.5" },
                    new[] { "Light Breakfast", "7.0" },
                    new[] { "High-Fat Meal",   "8.2" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.All(r => !string.IsNullOrWhiteSpace(r.Timepoint)),
                "Food state should populate Timepoint");
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)),
                "Food state must NEVER route to TreatmentArm");
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — Compound layout (InferredHeader + SocDividers + identical
        /// spanning header) now applies classifyRowLabel to col 0 labels.
        /// Population descriptors route to Population; bare drug names still
        /// route to TreatmentArm (the dominant happy path for compound tables).
        /// Validates the `parseCompoundLayout` R1.1 patch.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_CompoundLayout_PopulationRoutesCorrectly()
        {
            // Build a minimal compound layout fixture by reusing the existing
            // helper — rows have "Healthy Volunteers GFR..." which matches the
            // R1.1 age-qualified-Subjects regex via "Healthy Volunteers" prefix.
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // Healthy Volunteers row now routes to Population (was TreatmentArm
            // pre-R1.1), but ArmN extraction from "(n=6)" still works because
            // it's done on the col 0 text regardless of routing destination.
            var healthy = results.First(r =>
                r.Population != null && r.Population.Contains("Healthy Volunteers") &&
                r.ParameterCategory == "Renal Impairment");
            Assert.IsNull(healthy.TreatmentArm,
                "R1.1: Healthy Volunteers row should route Population, not TreatmentArm");
            Assert.AreEqual(6, healthy.ArmN,
                "R1.1: ArmN should still be extracted from col 0 '(n=6)' trailer");
            StringAssert.Contains(
                healthy.ValidationFlags ?? "",
                "PK_COMPOUND_POP_ROUTED",
                "R1.1: compound-layout population routing should emit attribution flag");
        }

        #endregion PK R1.1 PopulationDetector + Compound-Layout Routing

        #region Router PK Content Validation

        /**************************************************************/
        /// <summary>
        /// Narrative drug-interaction tables under LOINC 34090-1 (Clinical
        /// Pharmacology) should downgrade to <see cref="TableCategory.TEXT_DESCRIPTIVE"/>
        /// when no PK parameter appears in headers or rows and cells are prose-heavy.
        /// Mirrors TextTableID=6139/6140/6141.
        /// </summary>
        [TestMethod]
        public void Router_ClinicalPharmacology_NarrativeContent_DowngradesToTextDescriptive()
        {
            var longProse = string.Join(" ", Enumerable.Repeat("word", 30));

            var table = createTestTable(
                new[] { "Drug or Drug Class", "Effect" },
                new List<string?[]>
                {
                    new[] { "Carbamazepine Hydantoins", longProse },
                    new[] { "Other drugs: Phenobarbital Rifampin", longProse }
                },
                parentSectionCode: "34090-1");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser(),
                new TextDescriptiveTableParser()
            });

            var (category, parser) = router.Route(table);

            Assert.AreEqual(TableCategory.TEXT_DESCRIPTIVE, category);
            Assert.IsNotNull(parser);
            Assert.IsInstanceOfType(parser, typeof(TextDescriptiveTableParser));
        }

        /**************************************************************/
        /// <summary>
        /// Legitimate PK table content (PK parameter names in headers) keeps the
        /// PK category even under LOINC 34090-1 — content validation must not
        /// regress the happy path.
        /// </summary>
        [TestMethod]
        public void Router_ClinicalPharmacology_WithPkHeaders_ConfirmsPk()
        {
            var table = createTestTable(
                new[] { "Dose", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "50 mg", "0.29", "1.2" }
                },
                parentSectionCode: "34090-1");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser(),
                new TextDescriptiveTableParser()
            });

            var (category, parser) = router.Route(table);

            Assert.AreEqual(TableCategory.PK, category);
            Assert.IsInstanceOfType(parser, typeof(PkTableParser));
        }

        /**************************************************************/
        /// <summary>
        /// Transposed PK table (long-form English row labels, blank col-0 header)
        /// should still route to PK because row labels match the PK dictionary.
        /// </summary>
        [TestMethod]
        public void Router_ClinicalPharmacology_TransposedLongFormRowLabels_ConfirmsPk()
        {
            var table = createTestTable(
                new[] { null, "50 mg/kg IV", "75 mg/kg IV" },
                new List<string?[]>
                {
                    new[] { "Maximum Plasma Concentrations (mcg/mL)", "216", "275" },
                    new[] { "Elimination Half-life (hour)", "4.6", "4.3" }
                },
                parentSectionCode: "34090-1");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser(),
                new TextDescriptiveTableParser()
            });

            var (category, _) = router.Route(table);
            Assert.AreEqual(TableCategory.PK, category);
        }

        #endregion Router PK Content Validation

        #region Router Wave 3 R8 — DDI Downgrade

        /**************************************************************/
        /// <summary>
        /// R8 — A PK-coded section (34090-1) whose caption carries the classic
        /// "Effect of X on the Pharmacokinetics of Co-administered Drugs" DDI
        /// signal must route to DRUG_INTERACTION, not PK. Matches TID 13081 shape.
        /// </summary>
        [TestMethod]
        public void Router_R8_PkCodedSection_CoadministeredCaption_RoutesDrugInteraction()
        {
            var table = createTestTable(
                new[] { "Coadministered Drug", "Cmax Ratio", "AUC Ratio" },
                new List<string?[]>
                {
                    new[] { "Ketoconazole", "1.42", "2.13" }
                },
                parentSectionCode: "34090-1",
                caption: "Table 7. Effect of Phentermine/Topiramate on the Pharmacokinetics of Co-administered Drugs");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser(),
                new TextDescriptiveTableParser()
            });

            var (category, _) = router.Route(table);
            Assert.AreEqual(TableCategory.DRUG_INTERACTION, category,
                "DDI caption keyword 'Co-administered' must trump PK content.");
        }

        /**************************************************************/
        /// <summary>
        /// R8 — "Drug Interactions:" caption prefix routes to DRUG_INTERACTION.
        /// Matches TID 9921/9922 (Rilpivirine DDI table) shape.
        /// </summary>
        [TestMethod]
        public void Router_R8_DrugInteractionCaption_RoutesDrugInteraction()
        {
            var table = createTestTable(
                new[] { "Coadministered Drug", "Dose", "Cmax", "AUC" },
                new List<string?[]>
                {
                    new[] { "Rifabutin", "300 mg QD", "1820", "18400" }
                },
                parentSectionCode: "34090-1",
                caption: "Table 11: Drug Interactions: Pharmacokinetic Parameters for Rilpivirine in the Presence of Coadministered Drugs");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser(),
                new TextDescriptiveTableParser()
            });

            var (category, _) = router.Route(table);
            Assert.AreEqual(TableCategory.DRUG_INTERACTION, category);
        }

        /**************************************************************/
        /// <summary>
        /// R8 — "in the Presence of" caption matches the DDI pattern even when
        /// "Co-administered" isn't spelled out separately.
        /// </summary>
        [TestMethod]
        public void Router_R8_InThePresenceOfCaption_RoutesDrugInteraction()
        {
            var table = createTestTable(
                new[] { "Drug", "Cmax Change", "AUC Change" },
                new List<string?[]>
                {
                    new[] { "Atorvastatin", "↓ 15%", "↓ 25%" }
                },
                parentSectionCode: "34090-1",
                caption: "Table 3: Changes in Pharmacokinetic Parameters for RPV in the Presence of Coadministered Drugs");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser(),
                new TextDescriptiveTableParser()
            });

            var (category, _) = router.Route(table);
            Assert.AreEqual(TableCategory.DRUG_INTERACTION, category);
        }

        /**************************************************************/
        /// <summary>
        /// R8 — Defense-in-depth: a legitimate PK table whose caption mentions
        /// "Renal Impairment" (NOT a DDI signal) continues to route to PK.
        /// Guards against over-routing when the weaker "Effect of X on PK"
        /// phrase appears without any co-administration keyword.
        /// </summary>
        [TestMethod]
        public void Router_R8_RenalImpairmentCaption_StillRoutesPk()
        {
            var table = createTestTable(
                new[] { "Renal Function", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Normal", "5.5", "54.4" },
                    new[] { "Mild", "6.8", "67.7" }
                },
                parentSectionCode: "34090-1",
                caption: "Table 4. Effect of Renal Impairment on the Pharmacokinetics of Drug X");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser(),
                new TextDescriptiveTableParser()
            });

            var (category, _) = router.Route(table);
            Assert.AreEqual(TableCategory.PK, category,
                "Population-stratification captions must NOT be mistaken for DDI — only explicit co-admin / interaction keywords qualify.");
        }

        /**************************************************************/
        /// <summary>
        /// R8 — SectionTitle-carried DDI signal also routes correctly even when
        /// the Caption is silent.
        /// </summary>
        [TestMethod]
        public void Router_R8_SectionTitleDrugInteraction_RoutesDrugInteraction()
        {
            var table = createTestTable(
                new[] { "Drug", "Cmax", "AUC" },
                new List<string?[]>
                {
                    new[] { "Rifampin", "450", "3200" }
                },
                parentSectionCode: "42229-5",
                caption: "Table X",
                sectionTitle: "Drug Interactions");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser(),
                new TextDescriptiveTableParser()
            });

            var (category, _) = router.Route(table);
            Assert.AreEqual(TableCategory.DRUG_INTERACTION, category);
        }

        /**************************************************************/
        /// <summary>
        /// R8 — Direct unit test of the detector.
        /// </summary>
        [TestMethod]
        public void Router_R8_looksLikeDdi_ExactKeywordSet()
        {
            foreach (var caption in new[]
            {
                "Drug Interaction Parameters",
                "Drug-Interaction Table",
                "Effect of X on Co-administered Drugs",
                "Coadministered with Rifampin",
                "Coadministration with ketoconazole",
                "in the Presence of Rifampin",
                "in the presence of coadministered drugs",
                "DDI Analysis"
            })
            {
                var table = new ReconstructedTable { Caption = caption };
                Assert.IsTrue(TableParserRouter.looksLikeDdi(table),
                    $"Caption '{caption}' must be recognized as DDI.");
            }

            foreach (var caption in new[]
            {
                "Pharmacokinetics of Drug X",
                "Effect of Renal Impairment",
                "Cmax and AUC in Healthy Subjects",
                "Food Effect",
                "" // empty
            })
            {
                var table = new ReconstructedTable { Caption = caption };
                Assert.IsFalse(TableParserRouter.looksLikeDdi(table),
                    $"Caption '{caption}' must NOT be recognized as DDI.");
            }
        }

        #endregion Router Wave 3 R8 — DDI Downgrade

        #region PK R1.2 Transposed-Header Classification (Wave 1 R1.2)

        /**************************************************************/
        /// <summary>
        /// R1.2 — Transposed layout with food-state column headers. TID 22430
        /// (Tamsulosin) shape: row labels are PK metrics ("Cmin(ng/mL)",
        /// "Cmax(ng/mL)"), column headers are "Light Breakfast" / "Fasted" /
        /// "High-Fat Breakfast". Post-R1.2: Timepoint is populated, DoseRegimen
        /// stays null, flag <c>PK_TRANSPOSED_HEADER_TIMEPOINT_ROUTED</c> fires.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_Transposed_FoodStateHeaderRoutesToTimepoint()
        {
            var table = createTestTable(
                new[] { "Parameter", "Light Breakfast", "Fasted", "High-Fat Breakfast" },
                new List<string?[]>
                {
                    new[] { "Cmax (ng/mL)", "10.1", "12.3", "14.5" },
                    new[] { "Tmax (h)",     "2.0",  "1.0",  "4.0"  }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(6, results.Count);
            Assert.IsTrue(results.All(r => r.Timepoint != null),
                "every row should have Timepoint populated from the transposed food-state header");
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.DoseRegimen)),
                "DoseRegimen must stay null when header is a food-state timepoint");
            Assert.IsTrue(results.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_HEADER_TIMEPOINT_ROUTED")),
                "every row should carry the R1.2 timepoint attribution flag");
        }

        /**************************************************************/
        /// <summary>
        /// R1.2 — Transposed layout with population column headers. Post-R1.2:
        /// Population is populated from the column header, DoseRegimen stays null,
        /// flag <c>PK_TRANSPOSED_HEADER_POP_ROUTED</c> fires.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_Transposed_PopulationHeaderRoutesToPopulation()
        {
            var table = createTestTable(
                new[] { "Parameter", "Healthy Subjects", "Renal Impairment" },
                new List<string?[]>
                {
                    new[] { "Cmax (mcg/mL)", "5.5", "7.0" },
                    new[] { "AUC (mcg·h/mL)", "47.5", "62.1" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.Any(r => r.Population == "Healthy Volunteers"),
                "Healthy Subjects header should populate Population=Healthy Volunteers");
            Assert.IsTrue(results.Any(r => r.Population == "Renal Impairment"),
                "Renal Impairment header should populate Population");
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.DoseRegimen)),
                "DoseRegimen must be null when header is a population stratifier");
            Assert.IsTrue(results.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_HEADER_POP_ROUTED")),
                "every row should carry the R1.2 population attribution flag");
        }

        /**************************************************************/
        /// <summary>
        /// R1.2 backward-compat guard: transposed layout with dose-level column
        /// headers (TID 13202 Ceftriaxone shape: "50 mg/kg IV" / "75 mg/kg IV")
        /// preserves pre-R1.2 behavior — DoseRegimen populated from the column
        /// header, Dose/DoseUnit extracted. No food-state / population flag fires.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_Transposed_DoseHeader_PreservesPreR1_2Behavior()
        {
            var table = createTestTable(
                new[] { null, "50 mg/kg IV", "75 mg/kg IV" },
                new List<string?[]>
                {
                    new[] { "Maximum Plasma Concentrations (mcg/mL)", "216", "275" },
                    new[] { "Elimination Half-life (hour)", "4.6", "4.3" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.All(r => !string.IsNullOrWhiteSpace(r.DoseRegimen)),
                "Dose-level column headers must still populate DoseRegimen");
            Assert.IsTrue(results.All(r =>
                !((r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_HEADER_TIMEPOINT_ROUTED"))),
                "Dose headers must NOT fire the R1.2 timepoint flag");
            Assert.IsTrue(results.All(r =>
                !((r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_HEADER_POP_ROUTED"))),
                "Dose headers must NOT fire the R1.2 population flag");
            // Existing transposed-layout flags still fire
            Assert.IsTrue(results.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_LAYOUT_SWAP")));
        }

        #endregion PK R1.2 Transposed-Header Classification

        #region PK R2 Context-Column Suppression (Wave 1 R2)

        /**************************************************************/
        /// <summary>
        /// R2 — <see cref="PkTableParser.isContextColumnHeader"/> returns true
        /// for the non-PK context column labels the audit identified.
        /// </summary>
        [TestMethod]
        public void PkParser_R2_isContextColumnHeader_DetectsKnownPatterns()
        {
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Co-administered Drug"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Coadministered Drug"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Dose of Azithromycin"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Dose of Co-administered Drug"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Subject Group"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Patient Group"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Number of Subjects"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Condition"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Formulation"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Route of Administration"));
        }

        /**************************************************************/
        /// <summary>
        /// R2 — <see cref="PkTableParser.isContextColumnHeader"/> returns false
        /// for legitimate PK column headers. Guards against over-matching.
        /// </summary>
        [TestMethod]
        public void PkParser_R2_isContextColumnHeader_DoesNotMatchPkHeaders()
        {
            Assert.IsFalse(PkTableParser.isContextColumnHeader("Cmax (mcg/mL)"));
            Assert.IsFalse(PkTableParser.isContextColumnHeader("AUC0-inf"));
            Assert.IsFalse(PkTableParser.isContextColumnHeader("Dose")); // Dose alone is a dose column, not context
            Assert.IsFalse(PkTableParser.isContextColumnHeader("n"));    // Sample size — handled elsewhere
            Assert.IsFalse(PkTableParser.isContextColumnHeader(null));
            Assert.IsFalse(PkTableParser.isContextColumnHeader(""));
        }

        /**************************************************************/
        /// <summary>
        /// R2 — PK table with a "Co-administered Drug" context column mixed in
        /// with legitimate PK columns: the context column should produce no
        /// observations, only the PK columns (Cmax, AUC) should emit rows.
        /// Mirrors TID 571 shape after R1 routes col 0 to TreatmentArm.
        /// </summary>
        [TestMethod]
        public void PkParser_R2_ContextColumnSuppressed_NoSpuriousObservations()
        {
            var table = createTestTable(
                new[] { "Regimen", "Co-administered Drug", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Atorvastatin 10 mg/day", "Atorvastatin", "0.83", "1.01" },
                    new[] { "Carbamazepine 200 mg",   "Carbamazepine", "0.97", "0.96" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // Expect 2 rows × 2 PK params = 4 observations (NOT 6 that would include context col)
            Assert.AreEqual(4, results.Count,
                "Only PK columns (Cmax, AUC) should emit observations; context column suppressed.");
            Assert.IsTrue(results.All(r =>
                r.ParameterName == "Cmax" || r.ParameterName == "AUC"),
                "No observation should carry ParameterName='Co-administered Drug'.");
            Assert.IsFalse(results.Any(r => r.ParameterName == "Co-administered Drug"),
                "Context column 'Co-administered Drug' must not emit any observation.");
        }

        /**************************************************************/
        /// <summary>
        /// R2 — Multiple context columns (Subject Group + Dose of X + Number of
        /// Subjects) are all suppressed; only PK parameter columns remain.
        /// </summary>
        [TestMethod]
        public void PkParser_R2_MultipleContextColumns_AllSuppressed()
        {
            var table = createTestTable(
                new[] { "Regimen", "Subject Group", "Dose of Co-administered Drug",
                        "Number of Subjects", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Study Drug 50 mg", "Healthy", "placebo", "12", "5.5" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // 1 row × 1 PK column = 1 observation
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Cmax", results[0].ParameterName);
        }

        #endregion PK R2 Context-Column Suppression

        #region PK R3 Section-Divider Suppression (Wave 1 R3)

        /**************************************************************/
        /// <summary>
        /// R3 — <see cref="PkTableParser.detectSectionDivider"/> recognizes
        /// asterisk-wrapped single-cell divider rows with PK qualifier
        /// phrases and extracts both qualifier state and embedded dose.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_detectSectionDivider_AsteriskWrappedSingleDose()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0,
                                        CleanedText = "**Single dose**" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1,
                                        CleanedText = null },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2,
                                        CleanedText = null }
                }
            };

            var result = PkTableParser.detectSectionDivider(row);

            Assert.IsTrue(result.IsDivider);
            Assert.AreEqual("single_dose", result.StickyQualifier);
        }

        /**************************************************************/
        /// <summary>
        /// R3 — Divider with embedded dose and qualifier:
        /// "**500 mg oral tablet single dose, effects of gender and age:**"
        /// Returns qualifier="single_dose", stickyDoseRegimen="500 mg oral tablet".
        /// Mirrors TID 2069 shape.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_detectSectionDivider_DoseAndQualifierExtracted()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0,
                                        CleanedText = "**500 mg oral tablet single dose, effects of gender and age:**" }
                }
            };

            var result = PkTableParser.detectSectionDivider(row);

            Assert.IsTrue(result.IsDivider);
            Assert.AreEqual("single_dose", result.StickyQualifier);
            Assert.IsTrue(result.StickyDoseRegimen != null && result.StickyDoseRegimen.StartsWith("500 mg"),
                $"expected dose fragment to start with '500 mg', got '{result.StickyDoseRegimen}'");
        }

        /**************************************************************/
        /// <summary>
        /// R3 — A normal PK data row (col 0 label + numeric PK cells) must
        /// NOT be detected as a divider. Guards against suppressing legitimate rows.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_detectSectionDivider_DataRowNotMisclassified()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0,
                                        CleanedText = "Healthy Subjects" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1,
                                        CleanedText = "5.5" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2,
                                        CleanedText = "47.5" }
                }
            };

            var result = PkTableParser.detectSectionDivider(row);

            Assert.IsFalse(result.IsDivider,
                "A data row with multiple non-empty cells must not be classified as a divider");
        }

        /**************************************************************/
        /// <summary>
        /// R3 — A single-cell row whose text does NOT contain a PK qualifier
        /// or embedded dose (e.g., "Summary:") must NOT be detected as a
        /// divider. Prevents over-suppression of legitimate title rows.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_detectSectionDivider_NoQualifier_NotDivider()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0,
                                        CleanedText = "Summary:" }
                }
            };

            var result = PkTableParser.detectSectionDivider(row);

            Assert.IsFalse(result.IsDivider,
                "A bare title (no qualifier, no dose) must not be misclassified as a divider");
        }

        /**************************************************************/
        /// <summary>
        /// R3 — End-to-end: a single-column PK table with a "**Single dose**"
        /// section divider between groups of data rows. Pre-R3 the divider
        /// emitted 7 spurious text observations (one per PK column); post-R3
        /// the divider is suppressed AND subsequent data rows inherit the
        /// sticky qualifier as ParameterSubtype with flag
        /// <c>PK_SECTION_QUALIFIER_APPLIED</c>.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_EndToEnd_DividerSuppressedAndQualifierApplied()
        {
            // Build a mini TID 2069 shape — col 0 label + 2 PK value columns
            // with a single-dose divider row embedded between data rows.
            var table = createTestTable(
                new[] { "Regimen", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    // Row 0: section divider (single cell, asterisk-wrapped)
                    new[] { "**Single dose**", null, null },
                    // Row 1: actual data
                    new[] { "250 mg oral", "5.5", "54.4" },
                    new[] { "500 mg oral", "7.0", "67.7" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // Only 2 data rows × 2 PK params = 4 observations (divider suppressed)
            Assert.AreEqual(4, results.Count,
                "Section divider row must NOT emit observations");
            Assert.IsFalse(results.Any(r =>
                (r.RawValue ?? "").Contains("Single dose")),
                "No observation should carry the divider text as RawValue");
            Assert.IsTrue(results.All(r => r.ParameterSubtype == "single_dose"),
                "All post-divider observations should carry the sticky qualifier");
            Assert.IsTrue(results.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_SECTION_QUALIFIER_APPLIED")),
                "Sticky-qualifier attribution flag must fire");
        }

        /**************************************************************/
        /// <summary>
        /// R3.1 — Bare (post-upstream-bold-strip) qualifier text such as
        /// "Single dose" or "Multiple dose" must be detected as a divider.
        /// The Stage 1/2 cell cleaner strips <c>**…**</c> markers, so the
        /// detector sees the bare phrase and — pre-R3.1 — the shell pattern
        /// rejected it. The anchored <c>_bareQualifierDividerPattern</c>
        /// fallback restores divider recognition.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_1_BareAsteriskStrippedDivider_DetectedAsDivider()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0,
                                        CleanedText = "Single dose" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1,
                                        CleanedText = null },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2,
                                        CleanedText = null }
                }
            };

            var result = PkTableParser.detectSectionDivider(row);

            Assert.IsTrue(result.IsDivider,
                "Bare 'Single dose' in a single-cell row must be recognized as a divider");
            Assert.AreEqual("single_dose", result.StickyQualifier);

            // Also verify 'Multiple dose' — the second commonly observed form
            row.Cells[0].CleanedText = "Multiple dose";
            var result2 = PkTableParser.detectSectionDivider(row);
            Assert.IsTrue(result2.IsDivider,
                "Bare 'Multiple dose' in a single-cell row must be recognized as a divider");
            Assert.AreEqual("multiple_dose", result2.StickyQualifier);
        }

        /**************************************************************/
        /// <summary>
        /// R3.1 — End-to-end: a single-column PK table where the section
        /// divider row arrives as plain text "Single dose" (asterisks
        /// stripped by upstream cleaning). Post-R3.1 the divider is
        /// suppressed and subsequent data rows inherit ParameterSubtype =
        /// "single_dose" with flag PK_SECTION_QUALIFIER_APPLIED.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_1_BareAsteriskStrippedDivider_EndToEnd_Suppressed()
        {
            var table = createTestTable(
                new[] { "Regimen", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    // Row 0: bare (post-bold-strip) divider
                    new[] { "Single dose", null, null },
                    new[] { "250 mg oral", "5.5", "54.4" },
                    new[] { "500 mg oral", "7.0", "67.7" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count,
                "Bare divider row must NOT emit observations");
            Assert.IsFalse(results.Any(r =>
                (r.RawValue ?? "").Contains("Single dose")),
                "No observation should carry the divider text as RawValue");
            Assert.IsTrue(results.All(r => r.ParameterSubtype == "single_dose"),
                "All post-divider observations should carry the sticky qualifier");
            Assert.IsTrue(results.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_SECTION_QUALIFIER_APPLIED")),
                "Sticky-qualifier attribution flag must fire");
        }

        /**************************************************************/
        /// <summary>
        /// R3.1 — Over-suppression guard: a bare single-cell row whose text
        /// is NOT an exact canonical qualifier phrase (e.g., "Summary",
        /// "Results") must NOT be classified as a divider. The anchored
        /// bare pattern allows only the specific qualifier phrases through.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_1_BareTextWithoutQualifier_NotDivider()
        {
            foreach (var plainText in new[] { "Summary", "Results", "Notes", "Discussion" })
            {
                var row = new ReconstructedRow
                {
                    Classification = RowClassification.DataBody,
                    Cells = new List<ProcessedCell>
                    {
                        new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0,
                                            CleanedText = plainText }
                    }
                };

                var result = PkTableParser.detectSectionDivider(row);

                Assert.IsFalse(result.IsDivider,
                    $"Bare text '{plainText}' (no asterisks, no colon, not a qualifier) must NOT be a divider");
            }
        }

        #endregion PK R3 Section-Divider Suppression

        #region PK R1.2.1 Food-State Sub-Header Suppression

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — A row shaped <c>Food | Fasted | Fed | Fasted | Fed | Fasted | Fed</c>
        /// (TID 3239/29134/33314 shape) must be classified as a food-state sub-header
        /// and suppressed. Pre-R1.2.1 the compound-layout path treated col 0 = "Food"
        /// as a drug-name TreatmentArm and produced one `Arm=Food, Raw=Fasted/Fed`
        /// text_descriptive observation per non-col-0 cell.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_FoodWithFastedFedCells_DetectedAsSubHeader()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "Food" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "Fasted" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "Fed" },
                    new ProcessedCell { SequenceNumber = 4, ResolvedColumnStart = 3, CleanedText = "Fasted" },
                    new ProcessedCell { SequenceNumber = 5, ResolvedColumnStart = 4, CleanedText = "Fed" }
                }
            };

            Assert.IsTrue(PkTableParser.detectFoodStateSubHeader(row),
                "A Food sub-header row with only Fasted/Fed qualifier cells must be recognized");
        }

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — Additional col 0 labels recognized as food descriptors
        /// (Food Effect, Food State, Food Condition, Food Intake, Prandial State,
        /// Fed State). All variants should match when the data cells are food-state
        /// qualifiers.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_AlternateFoodLabels_AllDetected()
        {
            foreach (var label in new[] { "Food Effect", "Food State", "Food Condition", "Food Intake", "Prandial State", "Fed State" })
            {
                var row = new ReconstructedRow
                {
                    Classification = RowClassification.DataBody,
                    Cells = new List<ProcessedCell>
                    {
                        new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = label },
                        new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "Fasted" },
                        new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "Fed" }
                    }
                };

                Assert.IsTrue(PkTableParser.detectFoodStateSubHeader(row),
                    $"Col 0 label '{label}' with food-state cells must be recognized as a sub-header");
            }
        }

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — Food-state cell-pattern coverage: Light Breakfast, High-Fat Meal,
        /// With Food, After Meal variants all match. Anchored pattern means partial
        /// matches (e.g., "Fasted conditions were") should NOT match.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_VariousFoodStateCells_AllRecognized()
        {
            foreach (var cell in new[] { "Light Breakfast", "High-Fat Meal", "High Fat Breakfast", "Low-Fat Meal", "With Food", "After Meal", "Fasting", "Fed state" })
            {
                var row = new ReconstructedRow
                {
                    Classification = RowClassification.DataBody,
                    Cells = new List<ProcessedCell>
                    {
                        new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "Food" },
                        new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = cell }
                    }
                };

                Assert.IsTrue(PkTableParser.detectFoodStateSubHeader(row),
                    $"Food-state cell '{cell}' must be recognized as a qualifier");
            }
        }

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — Guard against over-suppression: a row where col 0 is "Food" but
        /// cols 1..N carry numeric PK values (e.g., from an aberrantly-labeled data
        /// row, not a sub-header) must NOT be detected.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_FoodColZeroWithNumericValues_NotDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "Food" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "5.5 ± 1.1" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "7.0 ± 1.6" }
                }
            };

            Assert.IsFalse(PkTableParser.detectFoodStateSubHeader(row),
                "Numeric PK values in data cells must prevent detection even when col 0 is 'Food'");
        }

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — Guard against over-suppression: a row where col 0 is a drug name
        /// (not a food descriptor) but cells happen to contain food-state words
        /// must NOT be detected. Col 0 allowlist is the gate.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_DrugNameColZero_NotDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "Atorvastatin" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "Fasted" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "Fed" }
                }
            };

            Assert.IsFalse(PkTableParser.detectFoodStateSubHeader(row),
                "Drug-name col 0 must not be treated as a food-state sub-header");
        }

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — Guard: a lone "Food" col 0 with no food-state qualifier cells
        /// (all value cells empty) is NOT a sub-header — prevents suppressing
        /// legitimate-but-empty rows.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_FoodColZeroAllEmptyCells_NotDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "Food" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = null },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "" }
                }
            };

            Assert.IsFalse(PkTableParser.detectFoodStateSubHeader(row),
                "At least one non-empty food-state qualifier cell is required");
        }

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — End-to-end: a compound-layout PK table shaped like TID 3239
        /// with a `Food | Fasted | Fed | Fasted | Fed` sub-header must suppress
        /// the sub-header row's observations entirely. Data rows (Cmax, Tmax)
        /// are emitted as normal.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_EndToEnd_CompoundLayoutFoodSubHeader_Suppressed()
        {
            // Compound layout: row-1 is sub-header (Col 0 | cols 1..4 are "Younger"/"Older"),
            // subsequent rows have col 0 as a metadata label. The `Food | Fasted | Fed`
            // row should be suppressed.
            var table = createTestTable(
                new[] { "", "Younger", "Younger", "Older", "Older" },
                new List<string?[]>
                {
                    // Row 0: compound sub-header — column labels
                    new[] { "Parameter", "Cmax", "Tmax", "Cmax", "Tmax" },
                    // Row 1: food-state sub-header — MUST be suppressed
                    new[] { "Food", "Fasted", "Fed", "Fasted", "Fed" },
                    // Row 2: actual PK data
                    new[] { "Cmax (ng/mL)", "1816", "3510", "2719", "2915" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.IsFalse(results.Any(r =>
                (r.TreatmentArm ?? "") == "Food" ||
                (r.RawValue ?? "") == "Fasted" ||
                (r.RawValue ?? "") == "Fed"),
                "No observation should carry 'Food' as Arm or 'Fasted'/'Fed' as RawValue after R1.2.1 suppresses the sub-header");
        }

        #endregion PK R1.2.1 Food-State Sub-Header Suppression

        #region PK Wave 3 R10 — Unit Extraction Gap

        /**************************************************************/
        /// <summary>
        /// R10 — Sub-header unit row detection: a row whose col 0 is empty and
        /// whose data cells are all recognized unit strings qualifies as a
        /// sub-header unit row. The detector returns a column→canonical-unit
        /// map that callers use to augment <c>paramDefs</c>.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SubHeaderUnitRow_EmptyCol0_UnitCellsDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "(ng/mL)" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "(mcg·h/mL)" },
                    new ProcessedCell { SequenceNumber = 4, ResolvedColumnStart = 3, CleanedText = "hr" }
                }
            };

            var result = PkTableParser.detectSubHeaderUnitRow(row);
            Assert.IsNotNull(result, "Row with pure-unit data cells and empty col 0 must be detected");
            Assert.AreEqual(3, result!.Count, "All three unit columns must be captured");
            Assert.AreEqual("ng/mL", result[1]);
            Assert.AreEqual("mcg·h/mL", result[2]);
            Assert.AreEqual("h", result[3], "'hr' must normalize to 'h'");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — The conservative two-unit guard: a row with only one unit cell
        /// is too ambiguous (could be a footer, figure annotation, or spillover)
        /// and must NOT be detected as a sub-header unit row.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SubHeaderUnitRow_SingleUnitCell_NotDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "(ng/mL)" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "" },
                    new ProcessedCell { SequenceNumber = 4, ResolvedColumnStart = 3, CleanedText = "" }
                }
            };

            Assert.IsNull(PkTableParser.detectSubHeaderUnitRow(row),
                "A single unit cell in otherwise-empty row is too ambiguous to suppress");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — A row with mixed content (one unit cell + one data cell) must
        /// NOT be detected as a sub-header unit row. The detector requires every
        /// non-empty cell at col &gt; 0 to be a recognized unit.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SubHeaderUnitRow_MixedContent_NotDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "(ng/mL)" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "13.9 ± 2.9" }
                }
            };

            Assert.IsNull(PkTableParser.detectSubHeaderUnitRow(row),
                "Mixed unit + data cells must not be treated as a sub-header unit row");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — Col 0 carrying a recognized label ("Unit", "Units", etc.) is
        /// allowed and does not disqualify the row.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SubHeaderUnitRow_UnitLabelCol0_Detected()
        {
            foreach (var label in new[] { "Unit", "Units", "Parameter", "Dose", "Regimen" })
            {
                var row = new ReconstructedRow
                {
                    Classification = RowClassification.DataBody,
                    Cells = new List<ProcessedCell>
                    {
                        new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = label },
                        new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "ng/mL" },
                        new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "mcg/mL" }
                    }
                };

                Assert.IsNotNull(PkTableParser.detectSubHeaderUnitRow(row),
                    $"Col 0 label '{label}' with unit cells must be detected");
            }
        }

        /**************************************************************/
        /// <summary>
        /// R10 — A drug-name or narrative in col 0 disqualifies the row, even
        /// when cells happen to be unit-shaped.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SubHeaderUnitRow_DrugNameCol0_NotDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "Fluconazole" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "ng/mL" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "hr" }
                }
            };

            Assert.IsNull(PkTableParser.detectSubHeaderUnitRow(row),
                "A drug-name col 0 must prevent sub-header unit detection");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — <c>applySubHeaderUnitAugmentation</c> fills null-unit entries
        /// in paramDefs but preserves non-null units extracted from the primary
        /// header. Returns the count of entries augmented.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_ApplySubHeaderUnitAugmentation_PreservesHeaderUnits()
        {
            var paramDefs = new List<(int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize)>
            {
                (1, "Cmax", null, false, false),       // No header unit → augmented
                (2, "AUC0-24", "mcg·h/mL", false, false), // Has header unit → preserved
                (3, "Tmax", null, false, false)         // No header unit → augmented
            };

            var unitsByColumn = new Dictionary<int, string>
            {
                [1] = "ng/mL",
                [2] = "ng·h/mL",   // Different unit — must be ignored (header wins)
                [3] = "h"
            };

            int augmented = PkTableParser.applySubHeaderUnitAugmentation(paramDefs, unitsByColumn);

            Assert.AreEqual(2, augmented, "Two null-unit entries must be augmented");
            Assert.AreEqual("ng/mL", paramDefs[0].unit, "Cmax null unit filled");
            Assert.AreEqual("mcg·h/mL", paramDefs[1].unit, "AUC0-24 header unit preserved (not overwritten)");
            Assert.AreEqual("h", paramDefs[2].unit, "Tmax null unit filled");
            Assert.IsTrue(paramDefs[2].isTimeMeasure, "Tmax with unit 'h' must be flagged as time-measure");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — Sibling-unit majority vote: when most observations sharing the
        /// same ParameterName have the same Unit, null-unit siblings are
        /// backfilled with that majority value and flagged
        /// <c>PK_UNIT_SIBLING_VOTED</c>.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SiblingUnitVote_MajorityBackfillsOrphans()
        {
            var observations = new List<ParsedObservation>
            {
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL" },
                new ParsedObservation { ParameterName = "Cmax", Unit = null },   // orphan
                new ParsedObservation { ParameterName = "AUC", Unit = "mcg·h/mL" },
                new ParsedObservation { ParameterName = "AUC", Unit = null }     // orphan
            };

            int backfilled = PkTableParser.applySiblingUnitVote(observations);

            Assert.AreEqual(2, backfilled, "Both orphan observations must be backfilled");
            Assert.AreEqual("ng/mL", observations[3].Unit);
            Assert.AreEqual("mcg·h/mL", observations[5].Unit);
            StringAssert.Contains(observations[3].ValidationFlags ?? "", "PK_UNIT_SIBLING_VOTED");
            StringAssert.Contains(observations[5].ValidationFlags ?? "", "PK_UNIT_SIBLING_VOTED");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — Sibling-vote guard: when siblings exhibit mixed units with no
        /// strict majority, orphan rows are left null (conservative — mixed
        /// groups are ambiguous).
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SiblingUnitVote_MixedUnitsNoMajority_LeavesOrphanNull()
        {
            var observations = new List<ParsedObservation>
            {
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "mcg/mL" },
                new ParsedObservation { ParameterName = "Cmax", Unit = null }
            };

            int backfilled = PkTableParser.applySiblingUnitVote(observations);

            Assert.AreEqual(0, backfilled, "No majority means no backfill");
            Assert.IsNull(observations[2].Unit);
        }

        /**************************************************************/
        /// <summary>
        /// R10 — Sibling-vote guard: a parameter group of size 1 does not
        /// qualify — cannot vote from a single sibling.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SiblingUnitVote_SingleObservation_NoOp()
        {
            var observations = new List<ParsedObservation>
            {
                new ParsedObservation { ParameterName = "Cmax", Unit = null }
            };

            int backfilled = PkTableParser.applySiblingUnitVote(observations);

            Assert.AreEqual(0, backfilled);
            Assert.IsNull(observations[0].Unit);
        }

        /**************************************************************/
        /// <summary>
        /// R10 — End-to-end: a PK table whose column headers carry param names
        /// without units AND whose second row is a sub-header unit row must
        /// produce observations with Unit populated from the sub-header.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_EndToEnd_SubHeaderUnitRow_AugmentsParamDefs()
        {
            // Primary header has no parenthesized units — just bare "Cmax", "Tmax", "AUC".
            // First data row is a sub-header unit row "(ng/mL) | hr | (ng·h/mL)".
            // Second data row carries actual PK values.
            var table = createTestTable(
                new[] { "Regimen", "Cmax", "Tmax", "AUC" },
                new List<string?[]>
                {
                    new[] { "", "(ng/mL)", "hr", "(ng·h/mL)" },      // sub-header unit row
                    new[] { "100 mg single oral", "5.5", "2.0", "45.2" } // data
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // Expect 3 observations (one per param column of the data row);
            // the sub-header unit row must produce no observations.
            Assert.AreEqual(3, results.Count, "Only the single data row should emit observations");

            var cmax = results.FirstOrDefault(r => r.ParameterName == "Cmax");
            var tmax = results.FirstOrDefault(r => r.ParameterName == "Tmax");
            var auc = results.FirstOrDefault(r => r.ParameterName == "AUC");

            Assert.IsNotNull(cmax, "Cmax observation missing");
            Assert.IsNotNull(tmax, "Tmax observation missing");
            Assert.IsNotNull(auc, "AUC observation missing");

            Assert.AreEqual("ng/mL", cmax!.Unit, "Cmax unit should flow from sub-header");
            Assert.AreEqual("h", tmax!.Unit, "Tmax unit should flow from sub-header (hr → h)");
            Assert.AreEqual("ng·h/mL", auc!.Unit, "AUC unit should flow from sub-header");

            // Sub-header unit row's own cells must not appear as raw values
            Assert.IsFalse(results.Any(r => (r.RawValue ?? "") == "(ng/mL)"),
                "Unit cells must not leak as data observations");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — End-to-end: inline cell-text unit scan picks up units embedded
        /// in data cells when the header does not provide one. E.g., the cell
        /// <c>"13.8 hr (6.4)"</c> must yield <c>Unit = "h"</c> with flag
        /// <c>PK_UNIT_FROM_CELL</c>.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_EndToEnd_CellInlineUnit_FlagsAppended()
        {
            // Header has bare "t½" with no unit, data cell has inline "hr".
            var table = createTestTable(
                new[] { "Regimen", "t½" },
                new List<string?[]>
                {
                    new[] { "100 mg single oral", "13.8 hr (6.4)" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            var obs = results[0];
            Assert.AreEqual("h", obs.Unit, "Cell-inline 'hr' should be extracted and normalized");
            StringAssert.Contains(obs.ValidationFlags ?? "", "PK_UNIT_FROM_CELL");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — Precedence guard: header-carried units (via <c>Name (unit)</c>
        /// pattern) must continue to win over cell-inline and sub-header scans.
        /// A cell like <c>"13.8 hr"</c> under header <c>"t½ (h)"</c> should get
        /// Unit = "h" without the <c>PK_UNIT_FROM_CELL</c> flag (header wins).
        /// </summary>
        [TestMethod]
        public void PkParser_R10_HeaderUnitTakesPrecedence_OverCellInline()
        {
            var table = createTestTable(
                new[] { "Regimen", "t½ (h)" },
                new List<string?[]>
                {
                    new[] { "100 mg single oral", "13.8 hr" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            var obs = results[0];
            Assert.AreEqual("h", obs.Unit, "Header unit should populate Unit");
            Assert.IsFalse((obs.ValidationFlags ?? "").Contains("PK_UNIT_FROM_CELL"),
                "Header unit precedence means cell-inline scan must not fire");
        }

        #endregion PK Wave 3 R10 — Unit Extraction Gap
    }
}
