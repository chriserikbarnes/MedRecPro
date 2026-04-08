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

            // All observations should have TreatmentArm populated
            Assert.IsTrue(results.All(r => !string.IsNullOrWhiteSpace(r.TreatmentArm)),
                "All observations should have TreatmentArm");

            // Check specific arm labels
            var armLabels = results.Select(r => r.TreatmentArm).Distinct().ToList();
            Assert.IsTrue(armLabels.Any(a => a!.Contains("Healthy Volunteers")));
            Assert.IsTrue(armLabels.Any(a => a!.Contains("Alcoholic Cirrhosis")));
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

            // Healthy Volunteers (n=6)
            var healthyRenal = results.First(r =>
                r.TreatmentArm!.Contains("Healthy Volunteers") &&
                r.ParameterCategory == "Renal Impairment");
            Assert.AreEqual(6, healthyRenal.ArmN);

            // Severe Renal Impairment (n=7)
            var severe = results.First(r =>
                r.TreatmentArm!.Contains("Severe"));
            Assert.AreEqual(7, severe.ArmN);

            // Alcoholic Cirrhosis (n=18)
            var cirrhosis = results.First(r =>
                r.TreatmentArm!.Contains("Cirrhosis"));
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

            var tmaxObs = results.First(r =>
                r.ParameterName == "Tmax" &&
                r.TreatmentArm!.Contains("Healthy Volunteers") &&
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
        /// ParameterName = PK metric, DoseRegimen = dose header, Unit extracted from the
        /// parenthesized metric, and the PK_TRANSPOSED_LAYOUT_SWAP flag.
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

            // ParameterName is the PK metric name (unit stripped)
            var paramNames = results.Select(r => r.ParameterName).Distinct().OrderBy(n => n).ToList();
            CollectionAssert.AreEqual(
                new[] { "AUC120", "AUC84", "Cmax", "Tmax" },
                paramNames);

            // DoseRegimen carries the dose header, and Dose/DoseUnit are extracted
            var auc84Low = results.First(r => r.ParameterName == "AUC84" && r.DoseRegimen == "0.025 mg/day");
            Assert.AreEqual("pg·hr/mL", auc84Low.Unit);
            Assert.AreEqual(0.025m, auc84Low.Dose);
            Assert.AreEqual("mg/d", auc84Low.DoseUnit);
            Assert.IsTrue(auc84Low.ValidationFlags?.Contains("PK_TRANSPOSED_LAYOUT_SWAP") == true,
                $"Expected PK_TRANSPOSED_LAYOUT_SWAP flag, got '{auc84Low.ValidationFlags}'");

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

            // Rows with row-label-derived ArmN must keep their original values
            var healthyRenal = results.FirstOrDefault(r =>
                r.TreatmentArm != null && r.TreatmentArm.Contains("Healthy Volunteers")
                && r.ParameterCategory == "Renal Impairment");
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
    }
}
