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
        /// <param name="rowProgress">Optional per-table progress callback for UI updates within the batch.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Number of observations written in this batch.</returns>
        Task<int> ProcessBatchAsync(TableCellContextFilter filter, IProgress<TransformBatchProgress>? rowProgress = null, CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Full corpus run: truncates the output table, then processes all tables in batches.
        /// </summary>
        /// <param name="batchSize">Number of TextTableIDs per batch (default 1000).</param>
        /// <param name="progress">Optional progress callback invoked after each batch completes (persisted to disk).</param>
        /// <param name="resumeFromId">Optional TextTableID to resume from (skips truncate, starts from this ID).</param>
        /// <param name="maxBatches">Optional maximum number of batches to process. Null = all.</param>
        /// <param name="rowProgress">Optional per-table progress callback for UI updates within each batch.</param>
        /// <param name="disableBioequivalentDedup">When true, skip the bioequivalent-ANDA dedup filter and
        /// process every discovered DocumentGUID. Default false — dedup is on when an
        /// <see cref="IBioequivalentLabelDedupService"/> is registered.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Total number of observations written.</returns>
        Task<int> ProcessAllAsync(
            int batchSize = 1000,
            IProgress<TransformBatchProgress>? progress = null,
            int? resumeFromId = null,
            int? maxBatches = null,
            IProgress<TransformBatchProgress>? rowProgress = null,
            bool disableBioequivalentDedup = false,
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
        /// <param name="progress">Optional progress callback invoked after each batch completes (persisted to disk).</param>
        /// <param name="resumeFromId">Optional TextTableID to resume from (skips truncate, starts from this ID).</param>
        /// <param name="maxBatches">Optional maximum number of batches to process. Null = all.</param>
        /// <param name="rowProgress">Optional per-table progress callback for UI updates within each batch.</param>
        /// <param name="disableBioequivalentDedup">When true, skip the bioequivalent-ANDA dedup filter and
        /// process every discovered DocumentGUID. Default false — dedup is on when an
        /// <see cref="IBioequivalentLabelDedupService"/> is registered.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Validation report with coverage metrics, row/table issues, and concordance checks.</returns>
        Task<BatchValidationReport> ProcessAllWithValidationAsync(
            int batchSize = 1000,
            IProgress<TransformBatchProgress>? progress = null,
            int? resumeFromId = null,
            int? maxBatches = null,
            IProgress<TransformBatchProgress>? rowProgress = null,
            bool disableBioequivalentDedup = false,
            CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Processes a batch of tables with full stage visibility: Stage 2 (Pivot) → Stage 3
        /// (Standardize) → Stage 3.5 (Claude Enhance) → write, capturing intermediate results.
        /// </summary>
        /// <param name="filter">Filter for table ID range.</param>
        /// <param name="rowProgress">Optional intra-batch progress callback for per-table and per-stage updates.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// A <see cref="BatchStageResult"/> containing reconstructed tables, routing decisions,
        /// pre/post-correction observations, and skip reasons.
        /// </returns>
        /// <seealso cref="ProcessBatchAsync"/>
        /// <seealso cref="BatchStageResult"/>
        Task<BatchStageResult> ProcessBatchWithStagesAsync(
            TableCellContextFilter filter,
            IProgress<TransformBatchProgress>? rowProgress = null,
            CancellationToken ct = default);

        #region Stage-by-Stage Diagnostic Methods

        /**************************************************************/
        /// <summary>
        /// Stage 2 (Pivot Table) only: reconstructs a single table by TextTableID without routing or parsing.
        /// Use for diagnostic visibility into the pivoted table structure.
        /// </summary>
        /// <param name="textTableId">The TextTableID to reconstruct.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// A <see cref="ReconstructedTable"/> with classified rows and resolved headers,
        /// or null if no cells exist for the given TextTableID.
        /// </returns>
        /// <seealso cref="ITableReconstructionService.ReconstructTableAsync"/>
        /// <seealso cref="RouteAndParseSingleTable"/>
        Task<ReconstructedTable?> ReconstructSingleTableAsync(int textTableId, CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Stage 3 (Standardize) only: routes a pivoted table to a parser and standardizes it.
        /// Does not write to the database or apply Claude enhancement. Use for diagnostic
        /// visibility into the routing decision and raw parse output.
        /// </summary>
        /// <param name="table">A reconstructed table from <see cref="ReconstructSingleTableAsync"/>.</param>
        /// <returns>
        /// Tuple of (category, parserName, observations):
        /// - category: the <see cref="TableCategory"/> determined by the router
        /// - parserName: the concrete parser type name, or null if SKIP
        /// - observations: parsed observations, or empty list if skipped
        /// </returns>
        /// <seealso cref="ITableParserRouter.Route"/>
        /// <seealso cref="ReconstructSingleTableAsync"/>
        /// <seealso cref="CorrectObservationsAsync"/>
        (TableCategory category, string? parserName, List<ParsedObservation> observations) RouteAndParseSingleTable(ReconstructedTable table);

        /**************************************************************/
        /// <summary>
        /// Stage 3.5 (Claude Enhance) only: applies Claude AI enhancement to parsed observations.
        /// Returns the original list unmodified if the correction service is not configured.
        /// Use for diagnostic visibility into AI corrections applied post-standardization.
        /// </summary>
        /// <param name="observations">Observations from <see cref="RouteAndParseSingleTable"/>.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// The corrected observations. Modified in-place with <c>AI_CORRECTED:*</c> flags
        /// appended to <see cref="ParsedObservation.ValidationFlags"/> for each correction.
        /// </returns>
        /// <seealso cref="IClaudeApiCorrectionService.CorrectBatchAsync"/>
        /// <seealso cref="RouteAndParseSingleTable"/>
        Task<List<ParsedObservation>> CorrectObservationsAsync(List<ParsedObservation> observations, CancellationToken ct = default);

        #endregion Stage-by-Stage Diagnostic Methods
    }
}
