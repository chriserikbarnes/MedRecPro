namespace MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening
{
    /**************************************************************/
    /// <summary>
    /// Stage 5 (Phase 2) service that projects adverse-event rows from
    /// <c>tmp_FlattenedStandardizedTable</c> into the denormalized
    /// <c>tmp_FlattenedAdverseEventTable</c> with pre-computed Relative Risk (RR),
    /// Dose-Normalized RR (DNRR), 95% CI bounds, and Document-level trial-design
    /// classification.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Runs after Stage 4 validation when an instance is provided to
    /// <c>TableParsingOrchestrator.ProcessAllWithValidationAsync</c>, or stand-alone
    /// when invoked from a CLI / host.
    ///
    /// ## Idempotency
    /// <see cref="PopulateAsync"/> truncates the destination table at the start of
    /// every call so reruns produce identical state. Unlike Stage 3 the service is
    /// fail-fast on errors — a partial denormalized table is more dangerous than a
    /// failed run.
    /// </remarks>
    /// <seealso cref="AdverseEventDenormalizationService"/>
    /// <seealso cref="RelativeRiskCalculator"/>
    public interface IAdverseEventDenormalizationService
    {
        /**************************************************************/
        /// <summary>
        /// Truncates <c>tmp_FlattenedAdverseEventTable</c>, then streams AE rows from
        /// <c>tmp_FlattenedStandardizedTable</c>, classifies trial design per Document,
        /// selects comparators per study group, computes RR/DNRR/CI, and bulk-writes
        /// the result.
        /// </summary>
        /// <param name="batchSize">Documents per batch (default 5000).</param>
        /// <param name="progress">Optional progress callback invoked after each batch completes.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Total rows written.</returns>
        /// <exception cref="OperationCanceledException">Cancellation observed.</exception>
        Task<int> PopulateAsync(
            int batchSize = 5000,
            IProgress<DenormProgress>? progress = null,
            CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Truncates <c>tmp_FlattenedAdverseEventTable</c> for a clean rerun.
        /// Falls back to <c>RemoveRange</c> + <c>SaveChanges</c> on the
        /// EF Core InMemoryDatabase test provider, which does not support raw SQL.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        Task TruncateAsync(CancellationToken ct = default);
    }

    /**************************************************************/
    /// <summary>
    /// Per-batch progress payload for <see cref="IAdverseEventDenormalizationService.PopulateAsync"/>.
    /// </summary>
    public sealed class DenormProgress
    {
        /**************************************************************/
        /// <summary>1-based batch number.</summary>
        public int BatchNumber { get; set; }

        /**************************************************************/
        /// <summary>Total batches in this run (computed from document count and batch size).</summary>
        public int TotalBatches { get; set; }

        /**************************************************************/
        /// <summary>Output rows written by this batch.</summary>
        public int BatchRowsWritten { get; set; }

        /**************************************************************/
        /// <summary>Cumulative output rows written so far in this run.</summary>
        public int CumulativeRowsWritten { get; set; }

        /**************************************************************/
        /// <summary>Elapsed time since the start of <see cref="IAdverseEventDenormalizationService.PopulateAsync"/>.</summary>
        public TimeSpan Elapsed { get; set; }
    }
}
