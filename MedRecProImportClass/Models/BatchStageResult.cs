namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Captures intermediate results from each stage of the SPL Table Normalization pipeline
    /// for a single batch. Used by <see cref="Service.TransformationServices.ITableParsingOrchestrator.ProcessBatchWithStagesAsync"/>
    /// to provide diagnostic visibility into the pipeline.
    /// </summary>
    /// <remarks>
    /// ## Stage Boundaries Captured
    /// - **Stage 2 (Pivot Table)**: Reconstructed tables (before routing)
    /// - **Stage 3 (Standardize)**: Routing decisions and parsed observations (before correction)
    /// - **Stage 3.5 (Claude Enhance)**: Post-correction observations (if Claude is enabled)
    /// - **DB Write**: Final observation count written
    ///
    /// Unlike <see cref="TransformBatchProgress"/> which reports aggregate counts,
    /// this DTO preserves the actual intermediate data structures for display.
    /// </remarks>
    /// <seealso cref="TransformBatchProgress"/>
    /// <seealso cref="ReconstructedTable"/>
    /// <seealso cref="ParsedObservation"/>
    public class BatchStageResult
    {
        #region Stage 2 — Pivot Table

        /**************************************************************/
        /// <summary>
        /// All tables pivoted in this batch by Stage 2 (Pivot Table).
        /// </summary>
        /// <seealso cref="ReconstructedTable"/>
        public List<ReconstructedTable> ReconstructedTables { get; set; } = new();

        #endregion Stage 2 — Pivot Table

        #region Stage 3 — Standardize

        /**************************************************************/
        /// <summary>
        /// Routing decision for each table: TextTableID, category, and selected parser name.
        /// Includes skipped tables (ParserName will be null for SKIP category).
        /// </summary>
        public List<TableRoutingDecision> RoutingDecisions { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// All observations produced by Stage 3 (Standardize) parsers before Claude correction.
        /// Empty if all tables were skipped.
        /// </summary>
        public List<ParsedObservation> PreCorrectionObservations { get; set; } = new();

        #endregion Stage 3 — Standardize

        #region Stage 3.5 — Claude Enhance

        /**************************************************************/
        /// <summary>
        /// Observations after Stage 3.5 (Claude Enhance). Same reference as
        /// <see cref="PreCorrectionObservations"/> if correction is disabled or no corrections were made.
        /// </summary>
        public List<ParsedObservation> PostCorrectionObservations { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Number of individual field corrections applied by Claude AI in Stage 3.5.
        /// </summary>
        public int CorrectionCount { get; set; }

        #endregion Stage 3.5 — Claude Enhance

        #region Final Results

        /**************************************************************/
        /// <summary>
        /// Number of observations successfully written to the database.
        /// May be 0 if SaveChangesAsync fails (batch skipped).
        /// </summary>
        public int ObservationsWritten { get; set; }

        /**************************************************************/
        /// <summary>
        /// Tables that were skipped, keyed by TextTableID with the skip reason.
        /// </summary>
        public Dictionary<int, string> SkipReasons { get; set; } = new();

        #endregion Final Results
    }

    /**************************************************************/
    /// <summary>
    /// Records the routing decision for a single table in Stage 3 (Standardize).
    /// </summary>
    /// <seealso cref="BatchStageResult"/>
    /// <seealso cref="TableCategory"/>
    public class TableRoutingDecision
    {
        /**************************************************************/
        /// <summary>TextTableID of the routed table.</summary>
        public int TextTableID { get; set; }

        /**************************************************************/
        /// <summary>Category assigned by the router.</summary>
        public TableCategory Category { get; set; }

        /**************************************************************/
        /// <summary>
        /// Concrete parser type name (e.g., "PkTableParser"), or null if skipped.
        /// </summary>
        public string? ParserName { get; set; }

        /**************************************************************/
        /// <summary>Number of observations produced by this table's parser.</summary>
        public int ObservationCount { get; set; }
    }
}
