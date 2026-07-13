using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Consolidated unit tests for all Stage 3 table parsers and the router.
    /// Tests cover: PkTableParser, SimpleArmTableParser, MultilevelAeTableParser,
    /// AeWithSocTableParser, EfficacyMultilevelTableParser, and TableParserRouter.
    /// </summary>
    /// <remarks>
    /// Tests use in-memory ReconstructedTable objects — no database or mocking needed.
    /// </remarks>
    [TestClass]
    public partial class TableParserTests
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

        /**************************************************************/
        /// <summary>
        /// Test harness exposing protected duplicate-comparison suppression for exact-key checks.
        /// </summary>
        private sealed class DuplicateComparisonHarness : BaseTableParser
        {
            /**************************************************************/
            /// <inheritdoc/>
            public override TableCategory SupportedCategory => TableCategory.EFFICACY;

            /**************************************************************/
            /// <inheritdoc/>
            public override int Priority => 0;

            /**************************************************************/
            /// <inheritdoc/>
            public override bool CanParse(ReconstructedTable table) => false;

            /**************************************************************/
            /// <inheritdoc/>
            public override List<ParsedObservation> Parse(ReconstructedTable table) => new();

            /**************************************************************/
            /// <summary>
            /// Applies the duplicate-comparison suppressor to the supplied observations.
            /// </summary>
            /// <param name="observations">Observation list to mutate.</param>
            public static void Suppress(List<ParsedObservation> observations)
            {
                #region implementation

                suppressDuplicateComparisonEmissions(observations);

                #endregion
            }
        }

        #endregion Test Helpers

    }
}
