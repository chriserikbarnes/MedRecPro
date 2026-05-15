using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Regression tests for AE Phase 3b treatment-arm recovery from generic headers,
    /// sibling columns, and leading body metadata rows.
    /// </summary>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="SimpleArmTableParser"/>
    /// <seealso cref="AeWithSocTableParser"/>
    [TestClass]
    public class AeArmRecoveryParserTests
    {
        #region Test Helpers

        /**************************************************************/
        /// <summary>
        /// Builds a golden-master projection for AE parser output.
        /// </summary>
        /// <remarks>
        /// The Phase C row-loop refactor is intended to move code without changing
        /// behavior. This projection keeps the parity gate focused on observable parser
        /// fields and suppression diagnostics rather than object identity.
        /// </remarks>
        /// <param name="observations">Parsed observations emitted by the parser.</param>
        /// <param name="diagnostics">Suppression diagnostics captured by the parser.</param>
        /// <returns>A stable line-oriented snapshot.</returns>
        /// <seealso cref="ParsedObservation"/>
        /// <seealso cref="ITableParserDiagnostics"/>
        private static string snapshotAeParserOutput(
            IEnumerable<ParsedObservation> observations,
            ITableParserDiagnostics diagnostics)
        {
            #region implementation

            var observationLines = observations
                .OrderBy(o => o.SourceRowSeq ?? int.MaxValue)
                .ThenBy(o => o.SourceCellSeq ?? int.MaxValue)
                .Select(o => string.Join("|",
                    "OBS",
                    o.TextTableID?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    o.SourceRowSeq?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    o.SourceCellSeq?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    o.ParameterName ?? string.Empty,
                    o.ParameterCategory ?? string.Empty,
                    o.ParameterSubtype ?? string.Empty,
                    o.TreatmentArm ?? string.Empty,
                    o.ArmN?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    formatNullableDouble(o.PrimaryValue),
                    o.PrimaryValueType ?? string.Empty,
                    o.Unit ?? string.Empty,
                    o.StudyContext ?? string.Empty,
                    o.Subpopulation ?? string.Empty,
                    o.ValidationFlags ?? string.Empty));

            var suppressionLines = diagnostics.SuppressedRows
                .OrderBy(r => r.SourceRowSeq ?? int.MaxValue)
                .ThenBy(r => r.SourceCellSeq ?? int.MaxValue)
                .Select(r => string.Join("|",
                    "SUP",
                    r.TextTableID?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    r.SourceRowSeq?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    r.SourceCellSeq?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    r.ValidationFlag,
                    r.ParameterName ?? string.Empty,
                    r.TreatmentArm ?? string.Empty,
                    r.RawValue ?? string.Empty,
                    r.StructuralLabel ?? string.Empty,
                    r.ContextTarget ?? string.Empty));

            return string.Join(Environment.NewLine, observationLines.Concat(suppressionLines));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats nullable doubles consistently for parser golden-master snapshots.
        /// </summary>
        /// <param name="value">Nullable numeric value.</param>
        /// <returns>Invariant-culture numeric text, or an empty string for null.</returns>
        /// <seealso cref="snapshotAeParserOutput"/>
        private static string formatNullableDouble(double? value)
        {
            #region implementation

            return value?.ToString("G17", CultureInfo.InvariantCulture) ?? string.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a minimal reconstructed AE table with one resolved header row.
        /// </summary>
        /// <param name="headerTexts">Resolved leaf header texts.</param>
        /// <param name="dataRows">Body rows to parse.</param>
        /// <param name="title">Optional document/product title.</param>
        /// <returns>Reconstructed table ready for parser tests.</returns>
        private static ReconstructedTable createAeTable(
            string?[] headerTexts,
            List<string?[]> dataRows,
            string? title = null)
        {
            #region implementation

            var columns = new List<HeaderColumn>();
            for (int i = 0; i < headerTexts.Length; i++)
            {
                columns.Add(new HeaderColumn
                {
                    ColumnIndex = i,
                    LeafHeaderText = headerTexts[i],
                    HeaderPath = new List<string> { headerTexts[i] ?? string.Empty },
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
                DocumentGUID = Guid.NewGuid(),
                Title = title ?? "Test Drug",
                VersionNumber = 1,
                ParentSectionCode = "34084-4",
                ParentSectionTitle = "6 ADVERSE REACTIONS",
                SectionTitle = "6.1 Clinical Trials Experience",
                LabelerName = "Test Lab",
                TotalColumnCount = headerTexts.Length,
                TotalRowCount = dataRows.Count + 1,
                HasExplicitHeader = true,
                HasInferredHeader = false,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 1,
                    ColumnCount = headerTexts.Length,
                    Columns = columns
                },
                Rows = rows
            };

            #endregion
        }

        #endregion Test Helpers

        #region Recovery Tests

        /**************************************************************/
        /// <summary>
        /// Generic parent headers such as <c>Number (%) of Patients</c> should recover
        /// real arm names from the first body row.
        /// </summary>
        [TestMethod]
        public void GenericHeaderRecoversArmsFromBodyRow()
        {
            var table = createAeTable(
                new[] { "Col 0", "Number (%) of Patients", "Number (%) of Patients" },
                new List<string?[]>
                {
                    new[] { "Adverse Reaction", "AndroGel 1.62% N=234", "Placebo N=40" },
                    new[] { "PSA increased", "26 (11.1%)", "0%" }
                });

            var parser = new AeWithSocTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "AndroGel 1.62%"));
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "Placebo"));
            Assert.IsTrue(results.All(r => r.ArmN.HasValue));
            Assert.IsTrue(results.All(r => r.PrimaryValueType == "Percentage"));
        }

        /**************************************************************/
        /// <summary>
        /// Repeated severity leaves should be retained as
        /// <see cref="ParsedObservation.ParameterSubtype"/> while arm names, N rows, and
        /// format rows are recovered from leading body metadata rows.
        /// </summary>
        [TestMethod]
        public void SeverityHeaderBodyRowsRecoverArmNAndSubtype()
        {
            var table = createAeTable(
                new[] { "Col 0", "Grades 1-4", "Grades 1-4", "Grades 3-4", "Grades 3-4" },
                new List<string?[]>
                {
                    new[] { "-", "Femara", "\u2194", "Tamoxifen", "\u2194" },
                    new[] { "Adverse Reactions", "N = 2448", "\u2194", "N = 2447", "\u2194" },
                    new[] { "-", "n (%)", "\u2194", "n (%)", "\u2194" },
                    new[] { "Patients with any adverse reaction", "2309", "(94.3)", "2212", "(90.4)" }
                });

            var parser = new AeWithSocTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.AreEqual(2, results.Count(r => r.TreatmentArm == "Femara"));
            Assert.AreEqual(2, results.Count(r => r.TreatmentArm == "Tamoxifen"));
            Assert.AreEqual(2, results.Count(r => r.ParameterSubtype == "Grades 1-4"));
            Assert.AreEqual(2, results.Count(r => r.ParameterSubtype == "Grades 3-4"));
            Assert.IsTrue(results.Where(r => r.TreatmentArm == "Femara").All(r => r.ArmN == 2448));
            Assert.IsTrue(results.Where(r => r.TreatmentArm == "Tamoxifen").All(r => r.ArmN == 2447));
        }

        /**************************************************************/
        /// <summary>
        /// Severity-axis leaf headers such as <c>Grade 3 or Higher (%)</c> and
        /// <c>Grades &gt;=3 (%)</c> should recover their parent treatment arm and
        /// remain as <see cref="ParsedObservation.ParameterSubtype"/>.
        /// </summary>
        [TestMethod]
        public void MultilevelSeverityAxisLeavesRecoverParentArmAndSubtype()
        {
            var table = createAeTable(
                new[] { "Adverse Reactions", "All Grades (%)", "Grade 3 or Higher (%)", "All Grades (%)", "Grades >=3 (%)" },
                new List<string?[]>
                {
                    new[] { "Neutropenia", "10 (7%)", "2 (1%)", "8 (6%)", "1 (1%)" }
                });

            var header = table.Header!;
            var columns = header.Columns!;
            header.HeaderRowCount = 2;
            columns[1].HeaderPath = new List<string> { "IMBRUVICA (N=135)", "All Grades (%)" };
            columns[2].HeaderPath = new List<string> { "IMBRUVICA (N=135)", "Grade 3 or Higher (%)" };
            columns[3].HeaderPath = new List<string> { "Chlorambucil (N=132)", "All Grades (%)" };
            columns[4].HeaderPath = new List<string> { "Chlorambucil (N=132)", "Grades >=3 (%)" };

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsFalse(results.Any(r =>
                string.Equals(r.TreatmentArm, "Grade 3 or Higher (%)", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.TreatmentArm, "Grades >=3 (%)", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.TreatmentArm, "All Grades (%)", StringComparison.OrdinalIgnoreCase)));
            Assert.AreEqual(2, results.Count(r => r.TreatmentArm == "IMBRUVICA"));
            Assert.AreEqual(2, results.Count(r => r.TreatmentArm == "Chlorambucil"));
            Assert.AreEqual(2, results.Count(r => r.ParameterSubtype == "All Grades"));
            Assert.AreEqual(1, results.Count(r => r.ParameterSubtype == "Grade 3 or Higher"));
            Assert.AreEqual(1, results.Count(r => r.ParameterSubtype == "Grades >=3"));
            Assert.IsTrue(results.Where(r => r.TreatmentArm == "IMBRUVICA").All(r => r.ArmN == 135));
            Assert.IsTrue(results.Where(r => r.TreatmentArm == "Chlorambucil").All(r => r.ArmN == 132));
        }

        /**************************************************************/
        /// <summary>
        /// Study headers plus body-row arm headers and value-axis rows should produce
        /// treatment arms, study context, and subtype instead of missing-arm rows.
        /// </summary>
        [TestMethod]
        public void StudyHeaderBodyArmRowsRecoverContextAndSubtype()
        {
            var table = createAeTable(
                new[]
                {
                    "Col 0",
                    "TAX323 (n=355)", "TAX323 (n=355)", "TAX323 (n=355)", "TAX323 (n=355)",
                    "TAX324 (n=494)", "TAX324 (n=494)", "TAX324 (n=494)", "TAX324 (n=494)"
                },
                new List<string?[]>
                {
                    new[]
                    {
                        "-",
                        "Docetaxel arm (n=174)", "\u2194", "Comparator arm (n=181)", "\u2194",
                        "Docetaxel arm (n=251)", "\u2194", "Comparator arm (n=243)", "\u2194"
                    },
                    new[]
                    {
                        "Adverse Reaction (by Body System)",
                        "Any %", "Grade 3/4 %", "Any %", "Grade 3/4 %",
                        "Any %", "Grade 3/4 %", "Any %", "Grade 3/4 %"
                    },
                    new[] { "Neutropenia", "93", "76", "87", "53", "95", "84", "84", "56" }
                });

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(8, results.Count);
            Assert.AreEqual(2, results.Count(r => r.TreatmentArm == "Docetaxel arm" && r.StudyContext == "TAX323"));
            Assert.AreEqual(2, results.Count(r => r.TreatmentArm == "Comparator arm" && r.StudyContext == "TAX323"));
            Assert.AreEqual(2, results.Count(r => r.TreatmentArm == "Docetaxel arm" && r.StudyContext == "TAX324"));
            Assert.AreEqual(2, results.Count(r => r.TreatmentArm == "Comparator arm" && r.StudyContext == "TAX324"));
            Assert.AreEqual(4, results.Count(r => r.ParameterSubtype == "Any"));
            Assert.AreEqual(4, results.Count(r => r.ParameterSubtype == "Grade 3/4"));
        }

        /**************************************************************/
        /// <summary>
        /// Drug names containing <c>or</c> should not be misclassified as odds-ratio
        /// statistic columns.
        /// </summary>
        [TestMethod]
        public void ArmContainingOrIsNotStatColumn()
        {
            var table = createAeTable(
                new[]
                {
                    "Adverse Reactions",
                    "Minocycline Hydrochloride Extended-Release Tablets (1 mg/kg) N = 674 (%)",
                    "PLACEBO N = 364 (%)"
                },
                new List<string?[]>
                {
                    new[] { "Fatigue", "62 (9)", "24 (7)" }
                });

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsFalse(results.Any(r => r.TreatmentArm == "Comparison"));
            Assert.IsTrue(results.Any(r => r.TreatmentArm!.StartsWith("Minocycline Hydrochloride", StringComparison.Ordinal)));
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "PLACEBO"));
        }

        /**************************************************************/
        /// <summary>
        /// Context-axis labels should stay out of TreatmentArm and use a
        /// conservative product-title fallback when exactly one product arm is
        /// available.
        /// </summary>
        [TestMethod]
        public void ContextAxisColumnsUseSingleProductFallback()
        {
            var table = createAeTable(
                new[] { "Adverse Reactions", "60-day Treatment", "90-day Treatment" },
                new List<string?[]>
                {
                    new[] { "Nausea", "12 (8%)", "18 (9%)" }
                },
                title: "ZYBAN");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.TreatmentArm == "ZYBAN"));
            Assert.IsTrue(results.Any(r => r.StudyContext == "60-day Treatment"));
            Assert.IsTrue(results.Any(r => r.StudyContext == "90-day Treatment"));
            Assert.IsFalse(results.Any(r => r.TreatmentArm == "60-day Treatment" || r.TreatmentArm == "90-day Treatment"));
        }

        /**************************************************************/
        /// <summary>
        /// Structural AE rows (header echoes and non-observation metrics) are
        /// suppressed before observation emission, but real AE rows whose value
        /// cell is a dash placeholder are preserved as sub-threshold (&lt;1%)
        /// observations so treatment/placebo arm pairing is not broken.
        /// </summary>
        [TestMethod]
        public void StructuralAeRowsAndCellsAreSuppressed()
        {
            var table = createAeTable(
                new[] { "Adverse Reactions", "Drug A" },
                new List<string?[]>
                {
                    new[] { "Adverse Drug Reaction", "n=991" },
                    new[] { "Mean Duration of Therapy (days)", "42" },
                    new[] { "Nausea", "---" }
                });

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            // Only the two structural rows ("Adverse Drug Reaction" header echo and
            // "Mean Duration of Therapy" non-observation metric) are suppressed.
            // "Nausea | ---" emits a sub-threshold observation derived from the
            // arm denominator recovered from the header-echo row (n=991).
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Nausea", results[0].ParameterName);
            Assert.AreEqual("---", results[0].RawValue);
            Assert.AreEqual("Percentage", results[0].PrimaryValueType);
            Assert.IsTrue(results[0].PrimaryValue.HasValue && results[0].PrimaryValue!.Value > 0,
                "Dash-placeholder cell should derive a positive sub-threshold midpoint.");
            Assert.IsTrue(results[0].ParseRule != null && results[0].ParseRule!.Contains("dash_placeholder"),
                "Parse rule should record dash_placeholder origin.");
            Assert.IsTrue(results[0].ValidationFlags != null && results[0].ValidationFlags!.Contains("PCT_DERIVED_FROM_DASH"),
                "Validation flags should record dash-derived percentage provenance.");

            Assert.AreEqual(2, ((ITableParserDiagnostics)parser).SuppressedRows.Count);
            Assert.IsTrue(((ITableParserDiagnostics)parser).SuppressedRows.All(
                r => r.ValidationFlag == "SUPPRESSED_STRUCTURAL_ROW"));
        }

        /**************************************************************/
        /// <summary>
        /// SOC/body-system rows that appear as DataBody rows should be preserved
        /// as category context for following observations, not emitted themselves.
        /// </summary>
        [TestMethod]
        public void DataBodySocRowIsSuppressedAndPreservedAsCategoryContext()
        {
            var table = createAeTable(
                new[] { "Adverse Reactions", "Drug A" },
                new List<string?[]>
                {
                    new[] { "Respiratory, thoracic, and mediastinal disorders", "Respiratory, thoracic, and mediastinal disorders" },
                    new[] { "Cough", "12" }
                });

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);
            var suppressed = ((ITableParserDiagnostics)parser).SuppressedRows;

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Respiratory, thoracic, and mediastinal disorders", results[0].ParameterCategory);
            Assert.AreEqual(1, suppressed.Count);
            Assert.AreEqual("SUPPRESSED_STRUCTURAL_ROW", suppressed[0].ValidationFlag);
        }

        /**************************************************************/
        /// <summary>
        /// Expanded grade and severity axis labels should remain subtypes while
        /// the real parent treatment arm is recovered.
        /// </summary>
        [TestMethod]
        public void ExpandedGradeAxisLabelsRouteToParameterSubtype()
        {
            var table = createAeTable(
                new[]
                {
                    "Adverse Reactions",
                    "\u2265Grade 3",
                    "NCI Grades 1-4",
                    "NCI Grades 3 & 4",
                    "Toxicity Grade >= 4n (%)",
                    "Severity Grade"
                },
                new List<string?[]>
                {
                    new[] { "Neutropenia", "4 (2%)", "12 (6%)", "5 (3%)", "1 (1%)", "8 (4%)" }
                });

            var header = table.Header!;
            var columns = header.Columns!;
            header.HeaderRowCount = 2;
            for (int i = 1; i < columns.Count; i++)
                columns[i].HeaderPath = new List<string> { "IMBRUVICA (N=135)", columns[i].LeafHeaderText ?? string.Empty };

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(5, results.Count);
            Assert.IsTrue(results.All(r => r.TreatmentArm == "IMBRUVICA"));
            Assert.IsTrue(results.All(r => r.ArmN == 135));
            Assert.IsTrue(results.Any(r => r.ParameterSubtype == "\u2265Grade 3"));
            Assert.IsTrue(results.Any(r => r.ParameterSubtype == "NCI Grades 1-4"));
            Assert.IsTrue(results.Any(r => r.ParameterSubtype == "NCI Grades 3 & 4"));
            Assert.IsTrue(results.Any(r => r.ParameterSubtype == "Toxicity Grade >= 4n"));
            Assert.IsTrue(results.Any(r => r.ParameterSubtype == "Severity Grade"));
            Assert.IsFalse(results.Any(r => r.TreatmentArm != null && r.TreatmentArm.Contains("Grade", StringComparison.OrdinalIgnoreCase)));
        }

        /**************************************************************/
        /// <summary>
        /// Table 42881 shape: paired incidence/discontinuation leaves should inherit
        /// the parent Lisinopril/HCTZ arm instead of becoming treatment arms.
        /// </summary>
        [TestMethod]
        public void Table42881_PairedIncidenceHeaders_InheritParentArm()
        {
            var table = createAeTable(
                new[] { "Adverse Reaction", "Incidence (discontinuation )", "Incidence (discontinuation )" },
                new List<string?[]>
                {
                    new[] { "Dizziness", "12 (6%)", "3 (1%)" }
                });

            var columns = table.Header!.Columns!;
            table.Header.HeaderRowCount = 2;
            columns[1].HeaderPath = new List<string> { "Lisinopril - Hydrochlorothiazide (N=200)", "Incidence (discontinuation )" };
            columns[2].HeaderPath = new List<string> { "Placebo (N=190)", "Incidence (discontinuation )" };

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsFalse(results.Any(r => r.TreatmentArm == "Incidence (discontinuation )"));
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "Lisinopril - Hydrochlorothiazide"));
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "Placebo"));
            Assert.IsTrue(results.All(r => r.ParameterSubtype == "Incidence (discontinuation )"));
        }

        /**************************************************************/
        /// <summary>
        /// Phase C golden-master fixture: the four canary AE row-loop shapes named in
        /// the maintainability plan must preserve observations and suppression
        /// diagnostics before and after the shared row-loop extraction.
        /// </summary>
        [TestMethod]
        public void PhaseC_AeRowLoopCanaries_PreserveGoldenMasterOutput()
        {
            #region implementation

            var table42881 = createAeTable(
                new[] { "Adverse Reaction", "Incidence (discontinuation )", "Incidence (discontinuation )" },
                new List<string?[]>
                {
                    new[] { "Dizziness", "12 (6%)", "3 (1%)" }
                });
            table42881.TextTableID = 42881;
            table42881.Header!.HeaderRowCount = 2;
            table42881.Header.Columns![1].HeaderPath = new List<string> { "Lisinopril - Hydrochlorothiazide (N=200)", "Incidence (discontinuation )" };
            table42881.Header.Columns![2].HeaderPath = new List<string> { "Placebo (N=190)", "Incidence (discontinuation )" };

            var parser42881 = new MultilevelAeTableParser();
            var actual42881 = snapshotAeParserOutput(parser42881.Parse(table42881), parser42881);
            var expected42881 = string.Join(Environment.NewLine,
                "OBS|42881|2|2|Dizziness||Incidence (discontinuation )|Lisinopril - Hydrochlorothiazide|200|6|Percentage|%|||PCT_CHECK:PASS",
                "OBS|42881|2|3|Dizziness||Incidence (discontinuation )|Placebo|190|1|Percentage|%|||PCT_CHECK:PASS");
            Assert.AreEqual(expected42881, actual42881);

            var table41668 = createAeTable(
                new[] { "Adverse Reaction", "Ocular" },
                new List<string?[]>
                {
                    new[] { "Eye pain", "12 (6%)" }
                },
                title: "Test Drug");
            table41668.TextTableID = 41668;

            var parser41668 = new SimpleArmTableParser();
            var actual41668 = snapshotAeParserOutput(parser41668.Parse(table41668), parser41668);
            Assert.AreEqual("SUP|41668|2||SUPPRESSED_AE_BODY_SYSTEM_ARM|Eye pain||Eye pain|Eye pain|ParameterCategory", actual41668);

            var table5725 = createAeTable(
                new[] { "Adverse Reaction", "%", "n" },
                new List<string?[]>
                {
                    new[] { "Headache", "8", "4" }
                });
            table5725.TextTableID = 5725;
            table5725.Header!.HeaderRowCount = 2;
            table5725.Header.Columns![1].HeaderPath = new List<string> { "Drug A (N=50)", "%" };
            table5725.Header.Columns![2].HeaderPath = new List<string> { "Drug A (N=50)", "n" };

            var parser5725 = new MultilevelAeTableParser();
            var actual5725 = snapshotAeParserOutput(parser5725.Parse(table5725), parser5725);
            var expected5725 = string.Join(Environment.NewLine,
                "OBS|5725|2|2|Headache|||Drug A|50|8|Percentage|%|||PCT_CONTEXT_PROMOTION",
                "OBS|5725|2|3|Headache|||Drug A|50|4|Percentage|%|||PCT_CONTEXT_PROMOTION");
            Assert.AreEqual(expected5725, actual5725);

            var captionArm = "Table 6: Adverse Reactions Reported in Clinical Trials";
            var table33633 = createAeTable(
                new[] { "Adverse Reaction", captionArm },
                new List<string?[]>
                {
                    new[] { "Nausea", "9 (4%)" }
                },
                title: "Test Drug");
            table33633.TextTableID = 33633;

            var parser33633 = new SimpleArmTableParser();
            var actual33633 = snapshotAeParserOutput(parser33633.Parse(table33633), parser33633);
            Assert.AreEqual("SUP|33633|2||SUPPRESSED_AE_CAPTION_ARM|Nausea||Nausea|Nausea|ParameterCategory", actual33633);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Headerless AE tables should still emit unresolved-arm suppression
        /// diagnostics after the row-loop extraction.
        /// </summary>
        [TestMethod]
        public void PhaseC_NoResolvedArms_PreservesUnresolvedSuppressionDiagnostics()
        {
            #region implementation

            var table = createAeTable(
                new[] { "Adverse Reaction" },
                new List<string?[]>
                {
                    new[] { "LOTENSIN HCT N = 665" },
                    new[] { "Dizziness" }
                });
            table.TextTableID = 21817;

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);
            var suppressed = ((ITableParserDiagnostics)parser).SuppressedRows;

            Assert.AreEqual(0, results.Count);
            Assert.AreEqual(2, suppressed.Count);
            Assert.IsTrue(suppressed.All(r => r.ValidationFlag == "SUPPRESSED_AE_UNRESOLVED_ARM"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Simple-arm AE statistic columns should continue to emit comparison rows
        /// when the shared AE loop is used.
        /// </summary>
        [TestMethod]
        public void PhaseC_SimpleArmAeStatColumns_PreserveComparisonEmission()
        {
            #region implementation

            var table = createAeTable(
                new[] { "Adverse Reaction", "Drug A", "Placebo", "Risk Difference" },
                new List<string?[]>
                {
                    new[] { "Nausea", "5", "4", "1.0" }
                });
            table.TextTableID = 27194;

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "Drug A"));
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "Placebo"));

            var comparison = results.Single(r => r.TreatmentArm == "Comparison");
            Assert.AreEqual("Nausea", comparison.ParameterName);
            Assert.AreEqual("RiskDifference", comparison.PrimaryValueType);
            Assert.AreEqual(1.0, comparison.PrimaryValue);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Simple-arm AE caption value hints should remain active so lab-style
        /// tables keep mean/SD semantics instead of reverting to count semantics.
        /// </summary>
        [TestMethod]
        public void PhaseC_SimpleArmAeCaptionHints_PreserveMeanValueTyping()
        {
            #region implementation

            var table = createAeTable(
                new[] { "Laboratory Parameter", "Drug A" },
                new List<string?[]>
                {
                    new[] { "LDL-Cholesterol", "105" }
                });
            table.TextTableID = 13165;
            table.Caption = "Mean Change from Baseline";

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Mean", results[0].PrimaryValueType);
            Assert.IsTrue(results[0].ParseRule != null && results[0].ParseRule!.Contains("caption", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(results[0].ValidationFlags != null && results[0].ValidationFlags!.Contains("CAPTION_HINT:caption:Mean"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Simple-arm AE generic arm columns should still reach column
        /// standardization, which owns the legacy nulling/flagging behavior.
        /// </summary>
        [TestMethod]
        public void PhaseC_SimpleArmAeGenericArmColumns_FlowToStandardization()
        {
            #region implementation

            var table = createAeTable(
                new[] { "Adverse Reaction", "Drug A", "Percentage" },
                new List<string?[]>
                {
                    new[] { "Nausea", "5", "2.7" }
                });
            table.TextTableID = 10342;

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "Drug A"));
            Assert.IsTrue(
                results.Any(r => r.SourceCellSeq == 3),
                string.Join("; ", results.Select(r => $"{r.SourceCellSeq}:{r.TreatmentArm}:{r.PrimaryValueType}:{r.RawValue}")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// SimpleArm AE context-axis columns such as <c>Overall</c> should not be
        /// reintroduced as treatment arms by the generic-column compatibility path.
        /// </summary>
        [TestMethod]
        public void PhaseC_SimpleArmAeOverallContextAxis_DoesNotEmitExtraArm()
        {
            #region implementation

            var table = createAeTable(
                new[] { "Adverse Reaction", "Drug A", "Overall" },
                new List<string?[]>
                {
                    new[] { "Nausea", "5 (5)", "6 (6)" }
                });
            table.TextTableID = 8387;

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Drug A", results[0].TreatmentArm);
            Assert.IsFalse(results.Any(r => r.TreatmentArm == "Overall"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// SimpleArm AE structural-cell suppressions should retain the original row
        /// label in diagnostics even when interspersed-label recovery inspects cells.
        /// </summary>
        [TestMethod]
        public void PhaseC_SimpleArmAeStructuralCellSuppression_UsesOriginalRowLabel()
        {
            #region implementation

            var table = createAeTable(
                new[] { "Adverse Reaction", "200", "300", "400", "600", "800", "900", "1200", "1600", "2400" },
                new List<string?[]>
                {
                    new[] { "Vomiting", "0", "0", "less than 1", "less than 1", "less than 1", "0", "1", "2", "3" }
                });
            table.TextTableID = 46283;

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);
            var suppressed = ((ITableParserDiagnostics)parser).SuppressedRows;

            Assert.AreEqual(6, results.Count);
            Assert.AreEqual(2, suppressed.Count);
            Assert.IsTrue(suppressed.All(r => r.ParameterName == "Vomiting"));
            Assert.IsTrue(suppressed.All(r => r.StructuralLabel == "Vomiting"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Table 42881 shape: composite leaves such as <c>Placebo Incidence</c>
        /// should split the arm from the value-axis subtype.
        /// </summary>
        [TestMethod]
        public void Table42881_PlaceboIncidence_SplitsArmAndSubtype()
        {
            var table = createAeTable(
                new[] { "Adverse Reaction", "Placebo Incidence" },
                new List<string?[]>
                {
                    new[] { "Dizziness", "3 (1%)" }
                });

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Placebo", results[0].TreatmentArm);
            Assert.AreEqual("Incidence", results[0].ParameterSubtype);
        }

        /**************************************************************/
        /// <summary>
        /// Table 41668 shape: bare SOC/body-system headers such as
        /// <c>Ocular</c> are context labels, never treatment arms.
        /// </summary>
        [TestMethod]
        public void Table41668_Ocular_IsSocContextNotTreatmentArm()
        {
            var table = createAeTable(
                new[] { "Adverse Reaction", "Ocular" },
                new List<string?[]>
                {
                    new[] { "Eye pain", "12 (6%)" }
                },
                title: "Test Drug");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);
            var suppressed = ((ITableParserDiagnostics)parser).SuppressedRows;

            Assert.AreEqual(0, results.Count);
            Assert.IsFalse(results.Any(r => r.TreatmentArm == "Ocular"));
            Assert.IsTrue(suppressed.Any(r => r.ValidationFlag == "SUPPRESSED_AE_BODY_SYSTEM_ARM"));
        }

        /**************************************************************/
        /// <summary>
        /// Table 5725 shape: percent/count paired leaves should inherit parent arms
        /// or suppress safely instead of emitting null-arm observations.
        /// </summary>
        [TestMethod]
        public void Table5725_PercentPairedColumns_RescueArmsOrSuppress()
        {
            var table = createAeTable(
                new[] { "Adverse Reaction", "%", "n" },
                new List<string?[]>
                {
                    new[] { "Headache", "8", "4" }
                });

            var columns = table.Header!.Columns!;
            table.Header.HeaderRowCount = 2;
            columns[1].HeaderPath = new List<string> { "Drug A (N=50)", "%" };
            columns[2].HeaderPath = new List<string> { "Drug A (N=50)", "n" };

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.TreatmentArm == "Drug A"));
            Assert.IsTrue(results.All(r => !string.IsNullOrWhiteSpace(r.TreatmentArm)));
        }

        /**************************************************************/
        /// <summary>
        /// Table 33633 shape: caption text must not be used as a treatment arm.
        /// </summary>
        [TestMethod]
        public void Table33633_CaptionArmLeak_IsRejectedOrResolved()
        {
            var captionArm = "Table 6: Adverse Reactions Reported in Clinical Trials";
            var table = createAeTable(
                new[] { "Adverse Reaction", captionArm },
                new List<string?[]>
                {
                    new[] { "Nausea", "9 (4%)" }
                },
                title: "Test Drug");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);
            var suppressed = ((ITableParserDiagnostics)parser).SuppressedRows;

            Assert.AreEqual(0, results.Count);
            Assert.IsFalse(results.Any(r => r.TreatmentArm == captionArm));
            Assert.IsTrue(suppressed.Any(r => r.ValidationFlag == "SUPPRESSED_AE_CAPTION_ARM"));
        }

        /**************************************************************/
        /// <summary>
        /// Unrescuable text/header-only AE rows should be suppressed with a stable
        /// unresolved-arm reason instead of emitted as fake observations.
        /// </summary>
        [TestMethod]
        public void AeTextRows_UnrescuableStructuralRows_AreSuppressed()
        {
            var table = createAeTable(
                new[] { "Adverse Reaction", "Incidence" },
                new List<string?[]>
                {
                    new[] { "Body System", "Event" }
                });

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);
            var suppressed = ((ITableParserDiagnostics)parser).SuppressedRows;

            Assert.AreEqual(0, results.Count);
            Assert.IsTrue(suppressed.Any(r => r.ValidationFlag == "SUPPRESSED_AE_UNRESOLVED_ARM"));
        }

        /**************************************************************/
        /// <summary>
        /// Real numeric AE rows with a recoverable parent arm should continue to
        /// emit observations after paired-leaf handling is applied.
        /// </summary>
        [TestMethod]
        public void AeRealNumericRows_WithRecoverableArm_AreNotSuppressed()
        {
            var table = createAeTable(
                new[] { "Adverse Reaction", "Incidence" },
                new List<string?[]>
                {
                    new[] { "Nausea", "12 (6%)" }
                });

            var columns = table.Header!.Columns!;
            table.Header.HeaderRowCount = 2;
            columns[1].HeaderPath = new List<string> { "Drug A (N=200)", "Incidence" };

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Drug A", results[0].TreatmentArm);
            Assert.AreEqual("Incidence", results[0].ParameterSubtype);
            Assert.AreEqual(6.0, results[0].PrimaryValue);
        }

        #endregion Recovery Tests
    }
}
