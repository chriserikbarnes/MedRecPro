using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Service interface for Stage 1 of the SPL Table Normalization pipeline.
    /// Provides access to the flat 26-column <see cref="TableCellContext"/> projection
    /// that joins cell, row, table, section, and document data.
    /// </summary>
    /// <remarks>
    /// ## Batch Processing
    /// Use <see cref="GetTextTableIdRangeAsync"/> to discover the TextTableID bounds,
    /// then iterate with <see cref="TableCellContextFilter.TextTableIdRangeStart"/>
    /// and <see cref="TableCellContextFilter.TextTableIdRangeEnd"/> for scalable processing.
    /// </remarks>
    /// <seealso cref="TableCellContext"/>
    /// <seealso cref="TableCellContextFilter"/>
    public interface ITableCellContextService
    {
        /**************************************************************/
        /// <summary>
        /// Retrieves a flat list of <see cref="TableCellContext"/> records matching the optional filter.
        /// </summary>
        /// <param name="filter">Optional filter for document, table, range, or row cap. Pass null for all rows.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of projected <see cref="TableCellContext"/> DTOs.</returns>
        /// <seealso cref="TableCellContextFilter"/>
        Task<List<TableCellContext>> GetTableCellContextsAsync(
            TableCellContextFilter? filter = null,
            CancellationToken cancellationToken = default);

        /**************************************************************/
        /// <summary>
        /// Retrieves <see cref="TableCellContext"/> records grouped by TextTableID.
        /// </summary>
        /// <param name="filter">Optional filter for document, table, range, or row cap.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dictionary keyed by TextTableID with lists of cells for each table.</returns>
        /// <seealso cref="TableCellContextFilter"/>
        Task<Dictionary<int, List<TableCellContext>>> GetTableCellContextsGroupedByTableAsync(
            TableCellContextFilter? filter = null,
            CancellationToken cancellationToken = default);

        /**************************************************************/
        /// <summary>
        /// Returns the minimum and maximum TextTableID values in the TextTable entity set.
        /// Supports batch orchestration — callers query the min/max and iterate in ranges.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Tuple of (MinId, MaxId) from the TextTable table.</returns>
        /// <seealso cref="Label.TextTable"/>
        Task<(int MinId, int MaxId)> GetTextTableIdRangeAsync(
            CancellationToken cancellationToken = default);
    }
}
