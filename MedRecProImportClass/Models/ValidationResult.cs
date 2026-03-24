namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Three-tier validation outcome for Stage 4 validation checks.
    /// </summary>
    /// <remarks>
    /// Applied at row, table, and batch levels. <c>Valid</c> means all checks passed;
    /// <c>Warning</c> means non-blocking issues were found; <c>Error</c> means data
    /// integrity problems that require review.
    /// </remarks>
    /// <seealso cref="RowValidationResult"/>
    /// <seealso cref="TableValidationResult"/>
    /// <seealso cref="BatchValidationReport"/>
    public enum ValidationStatus
    {
        /**************************************************************/
        /// <summary>All checks passed.</summary>
        Valid,

        /**************************************************************/
        /// <summary>Non-blocking issues found (e.g., missing optional fields, low confidence).</summary>
        Warning,

        /**************************************************************/
        /// <summary>Data integrity problems requiring review (e.g., bound inversion, orphan row).</summary>
        Error
    }

    /**************************************************************/
    /// <summary>
    /// Row-level validation result for a single <see cref="ParsedObservation"/>.
    /// </summary>
    /// <remarks>
    /// Produced by <c>IRowValidationService.ValidateObservation</c>. Each issue string
    /// describes one failed check (e.g., "MISSING_FIELD:DoseRegimen", "BOUND_INVERSION").
    /// </remarks>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="ValidationStatus"/>
    public class RowValidationResult
    {
        /**************************************************************/
        /// <summary>FK to source TextTable.</summary>
        public int? TextTableID { get; set; }

        /**************************************************************/
        /// <summary>Source row sequence number.</summary>
        public int? SourceRowSeq { get; set; }

        /**************************************************************/
        /// <summary>Source cell sequence number.</summary>
        public int? SourceCellSeq { get; set; }

        /**************************************************************/
        /// <summary>Treatment arm associated with this observation.</summary>
        public string? TreatmentArm { get; set; }

        /**************************************************************/
        /// <summary>Parameter name associated with this observation.</summary>
        public string? ParameterName { get; set; }

        /**************************************************************/
        /// <summary>Overall validation status for this row.</summary>
        public ValidationStatus Status { get; set; }

        /**************************************************************/
        /// <summary>
        /// List of issue descriptions. Empty when <see cref="Status"/> is <c>Valid</c>.
        /// Format: "CHECK_NAME:detail" (e.g., "MISSING_FIELD:DoseRegimen", "BOUND_INVERSION").
        /// </summary>
        public List<string> Issues { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Field completeness score 0.0–1.0 based on the ratio of populated expected fields
        /// (required + desirable) for the observation's TableCategory.
        /// </summary>
        public double FieldCompletenessScore { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Table-level validation result for all observations within one TextTableID.
    /// </summary>
    /// <remarks>
    /// Produced by <c>ITableValidationService.ValidateTable</c>. Checks cross-row
    /// consistency such as duplicate observations and arm coverage.
    /// </remarks>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="ValidationStatus"/>
    public class TableValidationResult
    {
        /**************************************************************/
        /// <summary>TextTableID being validated.</summary>
        public int? TextTableID { get; set; }

        /**************************************************************/
        /// <summary>Table category (PK, ADVERSE_EVENT, etc.).</summary>
        public string? TableCategory { get; set; }

        /**************************************************************/
        /// <summary>Overall validation status for this table.</summary>
        public ValidationStatus Status { get; set; }

        /**************************************************************/
        /// <summary>Total observations in this table.</summary>
        public int ObservationCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Arms that have ArmN defined but no data rows with PrimaryValue.
        /// </summary>
        public List<string> MissingArms { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Duplicate observation keys: "(ParameterName, TreatmentArm, SourceRowSeq)".
        /// </summary>
        public List<string> DuplicateKeys { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// List of issue descriptions for this table.
        /// </summary>
        public List<string> Issues { get; set; } = new();
    }

    /**************************************************************/
    /// <summary>
    /// Batch-level coverage report aggregating validation results across all processed tables.
    /// </summary>
    /// <remarks>
    /// Produced by <c>IBatchValidationService.GenerateReportAsync</c>. Contains aggregate
    /// statistics, confidence distribution, flag summaries, and all row/table-level issues.
    /// Returned as an in-memory DTO — not persisted to database.
    /// </remarks>
    /// <seealso cref="RowValidationResult"/>
    /// <seealso cref="TableValidationResult"/>
    /// <seealso cref="CrossVersionDiscrepancy"/>
    public class BatchValidationReport
    {
        /**************************************************************/
        /// <summary>Number of distinct TextTableIDs that produced observations.</summary>
        public int TotalTablesProcessed { get; set; }

        /**************************************************************/
        /// <summary>Number of tables skipped (SKIP category, parse errors, etc.).</summary>
        public int TotalTablesSkipped { get; set; }

        /**************************************************************/
        /// <summary>
        /// Skip reasons keyed by description (e.g., "SKIP:PatientInfo" → count).
        /// </summary>
        public Dictionary<string, int> SkipReasons { get; set; } = new();

        /**************************************************************/
        /// <summary>Total observations across all tables.</summary>
        public int TotalObservations { get; set; }

        /**************************************************************/
        /// <summary>
        /// Observation count grouped by TableCategory (e.g., "PK" → 150, "ADVERSE_EVENT" → 320).
        /// </summary>
        public Dictionary<string, int> RowCountByCategory { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Observation count grouped by ParseRule (e.g., "n_pct" → 200, "plain_number" → 80).
        /// </summary>
        public Dictionary<string, int> RowCountByParseRule { get; set; } = new();

        #region ParseConfidence Distribution (5-band)

        /**************************************************************/
        /// <summary>Observations with ParseConfidence ≥ 0.95.</summary>
        public int VeryHighConfidenceCount { get; set; }

        /**************************************************************/
        /// <summary>Observations with 0.80 ≤ ParseConfidence &lt; 0.95.</summary>
        public int HighConfidenceCount { get; set; }

        /**************************************************************/
        /// <summary>Observations with 0.60 ≤ ParseConfidence &lt; 0.80.</summary>
        public int MediumConfidenceCount { get; set; }

        /**************************************************************/
        /// <summary>Observations with 0.40 ≤ ParseConfidence &lt; 0.60.</summary>
        public int LowConfidenceCount { get; set; }

        /**************************************************************/
        /// <summary>Observations with ParseConfidence &lt; 0.40.</summary>
        public int VeryLowConfidenceCount { get; set; }

        #endregion ParseConfidence Distribution (5-band)

        #region AdjustedConfidence Distribution (5-band)

        /**************************************************************/
        /// <summary>Observations with AdjustedConfidence ≥ 0.95.</summary>
        public int AdjustedVeryHighCount { get; set; }

        /**************************************************************/
        /// <summary>Observations with 0.80 ≤ AdjustedConfidence &lt; 0.95.</summary>
        public int AdjustedHighCount { get; set; }

        /**************************************************************/
        /// <summary>Observations with 0.60 ≤ AdjustedConfidence &lt; 0.80.</summary>
        public int AdjustedMediumCount { get; set; }

        /**************************************************************/
        /// <summary>Observations with 0.40 ≤ AdjustedConfidence &lt; 0.60.</summary>
        public int AdjustedLowCount { get; set; }

        /**************************************************************/
        /// <summary>Observations with AdjustedConfidence &lt; 0.40.</summary>
        public int AdjustedVeryLowCount { get; set; }

        #endregion AdjustedConfidence Distribution (5-band)

        /**************************************************************/
        /// <summary>Average field completeness score across all validated observations (0.0–1.0).</summary>
        public double AverageFieldCompleteness { get; set; }

        /**************************************************************/
        /// <summary>Count of ValidationFlags containing "PASS".</summary>
        public int PassFlagCount { get; set; }

        /**************************************************************/
        /// <summary>Count of ValidationFlags containing "WARN".</summary>
        public int WarnFlagCount { get; set; }

        /**************************************************************/
        /// <summary>All row-level issues (only rows with Warning or Error status).</summary>
        public List<RowValidationResult> RowIssues { get; set; } = new();

        /**************************************************************/
        /// <summary>All table-level issues (only tables with Warning or Error status).</summary>
        public List<TableValidationResult> TableIssues { get; set; } = new();

        /**************************************************************/
        /// <summary>Cross-version concordance discrepancies.</summary>
        public List<CrossVersionDiscrepancy> CrossVersionDiscrepancies { get; set; } = new();
    }

    /**************************************************************/
    /// <summary>
    /// Cross-version concordance check result for a single product-version-category group.
    /// </summary>
    /// <remarks>
    /// Produced by <c>IBatchValidationService.CheckCrossVersionConcordanceAsync</c>.
    /// Compares row counts across label versions for the same product, flagging divergences
    /// greater than 50% which may indicate parsing errors or legitimate label updates.
    /// </remarks>
    /// <seealso cref="BatchValidationReport"/>
    public class CrossVersionDiscrepancy
    {
        /**************************************************************/
        /// <summary>Product title for this group.</summary>
        public string? ProductTitle { get; set; }

        /**************************************************************/
        /// <summary>Labeler/manufacturer name for this group.</summary>
        public string? LabelerName { get; set; }

        /**************************************************************/
        /// <summary>First version number being compared.</summary>
        public int? VersionNumber { get; set; }

        /**************************************************************/
        /// <summary>Table category where the discrepancy was found.</summary>
        public string? TableCategory { get; set; }

        /**************************************************************/
        /// <summary>Row count for the first version.</summary>
        public int RowCount { get; set; }

        /**************************************************************/
        /// <summary>Second version number being compared.</summary>
        public int? ComparedVersionNumber { get; set; }

        /**************************************************************/
        /// <summary>Row count for the compared version.</summary>
        public int ComparedRowCount { get; set; }

        /**************************************************************/
        /// <summary>Description of the discrepancy (e.g., "Row count divergence: 150 vs 42").</summary>
        public string? Issue { get; set; }
    }
}
