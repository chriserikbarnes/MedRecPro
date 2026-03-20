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
            Assert.AreEqual("Components of primary endpoint", results[0].ParameterSubtype);
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
    }
}
