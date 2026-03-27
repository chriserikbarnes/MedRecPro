namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Progress report for a single batch in the table standardization pipeline.
    /// Used by <see cref="IProgress{TransformBatchProgress}"/> to feed Spectre.Console
    /// progress bars and enable resumption tracking in the console application.
    /// </summary>
    /// <remarks>
    /// Reported by <c>TableParsingOrchestrator.ProcessAllAsync</c> and
    /// <c>ProcessAllWithValidationAsync</c> after each batch completes.
    /// The consumer can use <see cref="TotalBatches"/> for percentage calculation
    /// and <see cref="RangeEnd"/> for resumption (restart from RangeEnd + 1).
    /// </remarks>
    /// <seealso cref="Service.TransformationServices.ITableParsingOrchestrator"/>
    public class TransformBatchProgress
    {
        #region implementation

        /**************************************************************/
        /// <summary>Current batch number (1-based).</summary>
        public int BatchNumber { get; set; }

        /**************************************************************/
        /// <summary>Estimated total batches for the full corpus run.</summary>
        public int TotalBatches { get; set; }

        /**************************************************************/
        /// <summary>First TextTableID in this batch's range.</summary>
        public int RangeStart { get; set; }

        /**************************************************************/
        /// <summary>Last TextTableID in this batch's range.</summary>
        public int RangeEnd { get; set; }

        /**************************************************************/
        /// <summary>Number of observations produced by this batch.</summary>
        public int BatchObservationCount { get; set; }

        /**************************************************************/
        /// <summary>Cumulative observation count across all completed batches.</summary>
        public int CumulativeObservationCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of tables skipped in this batch (SKIP category, parse errors, empty results).
        /// Only populated during validation runs (<c>ProcessAllWithValidationAsync</c>).
        /// </summary>
        public int TablesSkippedThisBatch { get; set; }

        /**************************************************************/
        /// <summary>Wall-clock time elapsed since the run started.</summary>
        public TimeSpan Elapsed { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of tables processed so far within the current batch (1-based).
        /// Zero indicates a batch-boundary report (not a within-batch update).
        /// </summary>
        public int TablesProcessedInBatch { get; set; }

        /**************************************************************/
        /// <summary>
        /// Total number of reconstructed tables in the current batch.
        /// Zero indicates a batch-boundary report (not a within-batch update).
        /// </summary>
        public int TotalTablesInBatch { get; set; }

        #endregion
    }
}
