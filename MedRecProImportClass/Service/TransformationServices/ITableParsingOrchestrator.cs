using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Orchestrates Stage 3 of the SPL Table Normalization pipeline: routes reconstructed
    /// tables to parsers, collects observations, and bulk-writes to tmp_FlattenedStandardizedTable.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Stage 2 (ITableReconstructionService) → **Stage 3 (this)** → tmp_FlattenedStandardizedTable
    ///
    /// ## Key Operations
    /// - <see cref="ProcessAllAsync"/>: Full corpus run — truncate + batch loop
    /// - <see cref="ProcessBatchAsync"/>: Single batch — reconstruct, route, parse, write
    /// - <see cref="ParseSingleTableAsync"/>: Debug path — parse without DB write
    /// - <see cref="TruncateAsync"/>: Wipe table for rerun
    /// </remarks>
    /// <seealso cref="ITableReconstructionService"/>
    /// <seealso cref="ITableParserRouter"/>
    /// <seealso cref="ITableParser"/>
    public interface ITableParsingOrchestrator
    {
        /**************************************************************/
        /// <summary>
        /// Processes a batch of tables matching the filter: reconstruct → route → parse → write.
        /// </summary>
        /// <param name="filter">Filter for table ID range or specific table.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Number of observations written in this batch.</returns>
        Task<int> ProcessBatchAsync(TableCellContextFilter filter, CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Full corpus run: truncates the output table, then processes all tables in batches.
        /// </summary>
        /// <param name="batchSize">Number of TextTableIDs per batch (default 1000).</param>
        /// <param name="progress">Optional progress callback invoked after each batch completes.</param>
        /// <param name="resumeFromId">Optional TextTableID to resume from (skips truncate, starts from this ID).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Total number of observations written.</returns>
        Task<int> ProcessAllAsync(
            int batchSize = 1000,
            IProgress<TransformBatchProgress>? progress = null,
            int? resumeFromId = null,
            CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Debug/test path: reconstructs and parses a single table, returns observations
        /// without writing to the database.
        /// </summary>
        /// <param name="textTableId">The TextTableID to parse.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of parsed observations (not persisted).</returns>
        Task<List<ParsedObservation>> ParseSingleTableAsync(int textTableId, CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Truncates the tmp_FlattenedStandardizedTable for a clean rerun.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        Task TruncateAsync(CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Full corpus run with Stage 4 validation: truncate → batch loop → validate → report.
        /// After all batches are written, runs <see cref="IBatchValidationService.GenerateReportFromDatabaseAsync"/>
        /// and <see cref="IBatchValidationService.CheckCrossVersionConcordanceAsync"/>.
        /// </summary>
        /// <param name="batchSize">Number of TextTableIDs per batch (default 1000).</param>
        /// <param name="progress">Optional progress callback invoked after each batch completes.</param>
        /// <param name="resumeFromId">Optional TextTableID to resume from (skips truncate, starts from this ID).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Validation report with coverage metrics, row/table issues, and concordance checks.</returns>
        Task<BatchValidationReport> ProcessAllWithValidationAsync(
            int batchSize = 1000,
            IProgress<TransformBatchProgress>? progress = null,
            int? resumeFromId = null,
            CancellationToken ct = default);
    }
}
