using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Stage 3.4 ML.NET-based correction and anomaly scoring service for the SPL Table
    /// Normalization pipeline. Applies trained classification models to correct TableCategory,
    /// route DoseRegimen content, disambiguate PrimaryValueType, and score each observation
    /// for anomalousness using per-category PCA models.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Stage 3.25 (ColumnStandardization) → **Stage 3.4 ML Correction (this)** → Stage 3.5 (Claude Correction) → DB
    ///
    /// ## Why This Exists
    /// The Claude API correction pass (Stage 3.5) is cost- and time-prohibitive at scale.
    /// This service applies ML-based corrections first and emits an anomaly score per row
    /// that gates which rows are forwarded to Claude — reducing API calls to only the rows
    /// that truly need AI review.
    ///
    /// ## 4-Stage Scoring Pipeline
    /// 1. **TableCategory validation** — multiclass classifier confirms or overrides category
    /// 2. **DoseRegimen routing** — classifier routes misplaced content to correct columns
    /// 3. **PrimaryValueType disambiguation** — resolves "Numeric" to specific types
    /// 4. **Anomaly detection** — per-category PCA model scores each row
    ///
    /// ## Training Strategy
    /// Uses in-memory accumulation of high-confidence rows across batches. Models train
    /// once the accumulator reaches configured thresholds. Batch 1 always emits NOMODEL.
    ///
    /// All corrections are flagged in <see cref="ParsedObservation.ValidationFlags"/>
    /// with <c>MLNET:</c> prefixed flags for audit trail.
    /// </remarks>
    /// <seealso cref="IColumnStandardizationService"/>
    /// <seealso cref="IClaudeApiCorrectionService"/>
    /// <seealso cref="MlNetCorrectionSettings"/>
    public interface IMlNetCorrectionService
    {
        /**************************************************************/
        /// <summary>
        /// Lightweight startup initialization. Sets the service to ready state.
        /// No database query or model training occurs — training is deferred until the
        /// in-memory accumulator has enough data (triggered inside <see cref="ScoreAndCorrect"/>).
        /// Safe to call multiple times (no-ops after first call).
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task that completes when initialization is done.</returns>
        Task InitializeAsync(CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Applies 4-stage ML correction and anomaly scoring to the given observations.
        /// Modifies observations in-place and returns the same list. Each observation receives
        /// an <c>MLNET_ANOMALY_SCORE:{value}</c> or <c>MLNET_ANOMALY_SCORE:NOMODEL</c> flag
        /// in <see cref="ParsedObservation.ValidationFlags"/>.
        /// </summary>
        /// <param name="observations">Parsed observations from Stage 3.25.</param>
        /// <returns>The same list with ML corrections applied and anomaly scores appended.</returns>
        List<ParsedObservation> ScoreAndCorrect(List<ParsedObservation> observations);

        /**************************************************************/
        /// <summary>
        /// Feeds Claude-corrected observations back into ML as ground-truth training data and
        /// updates adaptive threshold metrics. Only rows with <c>AI_CORRECTED:</c> in
        /// <see cref="ParsedObservation.ValidationFlags"/> are extracted as ground truth.
        /// These bypass ParseConfidence thresholds (Claude is authoritative).
        /// </summary>
        /// <remarks>
        /// Called by <see cref="TableParsingOrchestrator"/> after each
        /// <see cref="IClaudeApiCorrectionService.CorrectBatchAsync"/> invocation.
        /// The observation list should contain the full post-Claude batch (both corrected
        /// and uncorrected rows); this method filters internally.
        /// </remarks>
        /// <param name="observations">Full post-Claude observation list.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task that completes when feedback is recorded.</returns>
        /// <seealso cref="IClaudeApiCorrectionService"/>
        Task FeedClaudeCorrectedBatchAsync(List<ParsedObservation> observations, CancellationToken ct = default);
    }
}
