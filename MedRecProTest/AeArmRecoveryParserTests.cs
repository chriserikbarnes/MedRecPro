using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
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
        /// Creates a minimal reconstructed AE table with one resolved header row.
        /// </summary>
        /// <param name="headerTexts">Resolved leaf header texts.</param>
        /// <param name="dataRows">Body rows to parse.</param>
        /// <returns>Reconstructed table ready for parser tests.</returns>
        private static ReconstructedTable createAeTable(string?[] headerTexts, List<string?[]> dataRows)
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
                Title = "Test Drug",
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

        #endregion Recovery Tests
    }
}
