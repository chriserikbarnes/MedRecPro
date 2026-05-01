using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Stage 4 batch-level validation and coverage reporting service for the SPL Table
    /// Normalization pipeline. Aggregates row/table validation results, computes coverage
    /// metrics, and checks cross-version concordance.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Stage 3 (Orchestrator) → **Stage 4 Batch Validation (this)** → BatchValidationReport
    ///
    /// ## Key Operations
    /// - <see cref="GenerateReportAsync"/>: In-memory path for per-batch validation
    /// - <see cref="GenerateReportFromDatabaseAsync"/>: DB-read path for full-corpus runs
    /// - <see cref="CheckCrossVersionConcordanceAsync"/>: Cross-version row count comparison
    ///
    /// Results returned as in-memory DTOs and logged — not persisted to database.
    /// </remarks>
    /// <seealso cref="IRowValidationService"/>
    /// <seealso cref="ITableValidationService"/>
    /// <seealso cref="BatchValidationReport"/>
    public interface IBatchValidationService
    {
        /**************************************************************/
        /// <summary>
        /// Generates a validation report from in-memory observations.
        /// Used for per-batch validation during processing.
        /// </summary>
        /// <param name="observations">Parsed observations to validate.</param>
        /// <param name="skipReasons">Optional dictionary of skipped TextTableIDs and reasons.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Complete validation report with row/table issues and coverage metrics.</returns>
        Task<BatchValidationReport> GenerateReportAsync(
            List<ParsedObservation> observations,
            Dictionary<int, string>? skipReasons = null,
            CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Generates a validation report by reading from tmp_FlattenedStandardizedTable.
        /// Used for full-corpus runs to avoid holding all observations in memory.
        /// </summary>
        /// <param name="skipReasons">Optional dictionary of skipped TextTableIDs and reasons.</param>
        /// <param name="batchSize">Read batch size (default 5000).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Complete validation report.</returns>
        Task<BatchValidationReport> GenerateReportFromDatabaseAsync(
            Dictionary<int, string>? skipReasons = null,
            int batchSize = 5000,
            CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Checks cross-version concordance by comparing row counts across label versions
        /// for the same product (keyed by ProductTitle + LabelerName).
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of discrepancies where row counts diverge by more than 50%.</returns>
        Task<List<CrossVersionDiscrepancy>> CheckCrossVersionConcordanceAsync(CancellationToken ct = default);
    }
}
