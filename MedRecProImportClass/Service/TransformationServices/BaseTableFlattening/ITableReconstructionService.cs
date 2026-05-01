using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Service interface for Stage 2 of the SPL Table Normalization pipeline.
    /// Reconstructs logical table structures from the flat TableCellContext projection
    /// produced by Stage 1, including row classification, ColSpan/RowSpan resolution,
    /// multi-level header resolution, and footnote extraction.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Stage 1 (ITableCellContextService) → **Stage 2 (this)** → Stage 3 (section-aware parsing)
    ///
    /// ## Key Operations
    /// - Groups cells by TextTableID
    /// - Extracts footnote markers from &lt;sup&gt; tags before HTML stripping
    /// - Classifies rows: Header, Body (first body row promoted), Footer, SOC divider
    /// - Resolves ColSpan/RowSpan into absolute column positions via occupancy grid
    /// - Builds multi-level header structures with column paths
    /// </remarks>
    /// <seealso cref="ReconstructedTable"/>
    /// <seealso cref="TableCellContext"/>
    /// <seealso cref="ITableCellContextService"/>
    /// <seealso cref="TableCellContextFilter"/>
    public interface ITableReconstructionService
    {
        /**************************************************************/
        /// <summary>
        /// Reconstructs a single table by its TextTableID.
        /// </summary>
        /// <param name="textTableId">The TextTableID to reconstruct.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A ReconstructedTable with classified rows and resolved headers,
        /// or null if no cells exist for the given TextTableID.
        /// </returns>
        /// <seealso cref="ReconstructedTable"/>
        Task<ReconstructedTable?> ReconstructTableAsync(
            int textTableId,
            CancellationToken cancellationToken = default);

        /**************************************************************/
        /// <summary>
        /// Reconstructs all tables matching the optional filter.
        /// </summary>
        /// <param name="filter">
        /// Optional filter for document, table, range, or row cap.
        /// Pass null to reconstruct all tables (caution: large corpus).
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of ReconstructedTable DTOs.</returns>
        /// <seealso cref="ReconstructedTable"/>
        /// <seealso cref="TableCellContextFilter"/>
        Task<List<ReconstructedTable>> ReconstructTablesAsync(
            TableCellContextFilter? filter = null,
            CancellationToken cancellationToken = default);
    }
}
