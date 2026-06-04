namespace MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening
{
    /**************************************************************/
    /// <summary>
    /// Stage 5 (Phase 2) service that projects adverse-event rows from
    /// <c>tmp_FlattenedStandardizedTable</c> into the denormalized
    /// <c>tmp_FlattenedAdverseEventCoverageTable</c> and RR-ready rows into
    /// <c>tmp_FlattenedAdverseEventTable</c>, then materializes
    /// <c>dbo.vw_AeRisk</c> into <c>tmp_FlattenedAdverseEventRiskTable</c>
    /// and <c>dbo.vw_AeDashboardProductCatalog</c> into
    /// <c>tmp_AeDashboardProductCatalog</c>.
    /// The AE table stores pre-computed Relative Risk (RR), Dose-Normalized RR
    /// (DNRR), 95% CI bounds, the row-level <c>IsPlaceboControlled</c> bit, and
    /// per-table trial-design diagnostics surfaced through <c>CalculationFlags</c>.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Runs after Stage 4 validation when an instance is provided to
    /// <c>TableParsingOrchestrator.ProcessAllWithValidationAsync</c>, or stand-alone
    /// when invoked from a CLI / host.
    ///
    /// ## Idempotency
    /// <see cref="PopulateAsync"/> truncates all Stage 5 destination tables at
    /// the start of every call so reruns produce identical state. Unlike Stage 3 the service is
    /// fail-fast on errors — a partial denormalized table is more dangerous than a
    /// failed run.
    /// </remarks>
    /// <seealso cref="AdverseEventDenormalizationService"/>
    /// <seealso cref="RelativeRiskCalculator"/>
    public interface IAdverseEventDenormalizationService
    {
        /**************************************************************/
        /// <summary>
        /// Truncates Stage 5 outputs, then streams AE rows from
        /// <c>tmp_FlattenedStandardizedTable</c>, classifies trial design per
        /// (DocumentGUID, TextTableID) for diagnostic flagging, selects comparators per
        /// study group, sets <c>IsPlaceboControlled</c> per-row from the comparator
        /// selection, audits coverage/null-RR outcomes, computes RR/DNRR/CI,
        /// bulk-writes RR-ready rows, and finally
        /// materializes <c>tmp_FlattenedAdverseEventRiskTable</c> from
        /// <c>dbo.vw_AeRisk</c> and <c>tmp_AeDashboardProductCatalog</c>
        /// from <c>dbo.vw_AeDashboardProductCatalog</c>.
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
        /// Truncates <c>tmp_AeDashboardProductCatalog</c>,
        /// <c>tmp_FlattenedAdverseEventRiskTable</c>,
        /// <c>tmp_FlattenedAdverseEventCoverageTable</c>, and
        /// <c>tmp_FlattenedAdverseEventTable</c> for a clean rerun.
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
