using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Read-only inspection helpers for <see cref="ReconstructedTable"/> that are
    /// needed outside of <see cref="BaseTableParser"/> — notably the router's
    /// content-based category validation, which must count PK hits in header
    /// columns and row labels without being itself a parser.
    /// </summary>
    /// <remarks>
    /// BaseTableParser keeps its equivalent helpers protected static so that each
    /// parser can share them via inheritance. This extension class exposes the
    /// same iteration patterns publicly for non-parser callers. Do not duplicate
    /// parser state logic here — add it once and share.
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    public static class ReconstructedTableExtensions
    {
        /**************************************************************/
        /// <summary>
        /// Returns DataBody and SocDivider rows in order. Matches the filtering
        /// applied by <c>BaseTableParser.getDataBodyRows</c>.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>Data rows; empty when the table has no rows.</returns>
        public static IReadOnlyList<ReconstructedRow> DataRows(this ReconstructedTable table)
        {
            #region implementation

            if (table.Rows == null)
                return Array.Empty<ReconstructedRow>();

            return table.Rows
                .Where(r => r.Classification == RowClassification.DataBody
                         || r.Classification == RowClassification.SocDivider)
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the cell that covers the given resolved column index, or null
        /// if the row has no such coverage. Uses the same grid-aware lookup as
        /// <c>BaseTableParser.getCellAtColumn</c>.
        /// </summary>
        /// <param name="row">Row to search.</param>
        /// <param name="columnIndex">Zero-based resolved column index.</param>
        /// <returns>The covering cell, or null.</returns>
        public static ProcessedCell? CellAt(this ReconstructedRow row, int columnIndex)
        {
            #region implementation

            if (row.Cells == null)
                return null;

            return row.Cells.FirstOrDefault(c =>
                c.ResolvedColumnStart != null
                && c.ResolvedColumnEnd != null
                && c.ResolvedColumnStart <= columnIndex
                && c.ResolvedColumnEnd > columnIndex);

            #endregion
        }
    }
}
