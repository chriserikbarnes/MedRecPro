using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Stage 3.4 ML.NET-based correction service for the SPL Table Normalization pipeline.
    /// Applies three trained classification models (TableCategory, DoseRegimen routing,
    /// PrimaryValueType disambiguation) to correct parse output and emits a deterministic
    /// parse-quality signal (<c>QC_PARSE_QUALITY:{score}</c> + REVIEW_REASONS) that gates
    /// which observations are forwarded to the Claude API in Stage 3.5.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Stage 3.25 (ColumnStandardization) → **Stage 3.4 ML Correction (this)** → Stage 3.5 (Claude Correction) → DB
    ///
    /// ## Why This Exists
    /// The Claude API correction pass (Stage 3.5) is cost- and time-prohibitive at scale.
    /// This service applies deterministic ML-based corrections first and emits a parse-quality
    /// score per row that gates which rows are forwarded to Claude — reducing API calls to
    /// only the rows whose parse output carries structural failure signal.
    ///
    /// ## 3-Stage Classifier Pipeline
    /// 1. **TableCategory validation** — multiclass classifier confirms or overrides category
    /// 2. **DoseRegimen routing** — classifier routes misplaced content to correct columns
    /// 3. **PrimaryValueType disambiguation** — resolves "Numeric" to specific types
    ///
    /// ## Parse-Quality Gate
    /// After the 3 classifier stages, <see cref="IParseQualityService"/> computes a
    /// deterministic score from the observation's column-contract conformance (Required
    /// fields populated, PrimaryValue / PrimaryValueType present, ParameterName non-null
    /// where applicable, Unit / ParameterSubtype free of structural garbage, soft repair
    /// flags). Score is emitted as <c>QC_PARSE_QUALITY:{score:F4}</c> with a companion
    /// <c>QC_PARSE_QUALITY:REVIEW_REASONS:{pipe-delimited list}</c> when any penalty
    /// fires. Claude forwarding is driven by a simple <c>score &lt; threshold</c> check.
    ///
    /// ## Training Strategy
    /// Uses in-memory accumulation of high-confidence rows across batches. Classifiers train
    /// once the accumulator reaches configured thresholds. Batch 1 classifiers are no-ops
    /// (nothing trained yet), but the parse-quality gate still emits on every observation
    /// since it is rule-based.
    ///
    /// All corrections are flagged in <see cref="ParsedObservation.ValidationFlags"/>
    /// with <c>QC:</c> prefixed flags for audit trail.
    ///
    /// ## Stage 4 Retirement (2026-04-24)
    /// The former Stage 4 PCA anomaly scoring was retired because raw reconstruction-error
    /// scores clustered in a narrow band regardless of training-set shape, making any fixed
    /// threshold a continual tuning exercise. The parse-quality gate above replaces it and
    /// targets parse-alignment failures directly.
    /// </remarks>
    /// <seealso cref="IColumnStandardizationService"/>
    /// <seealso cref="IClaudeApiCorrectionService"/>
    /// <seealso cref="IParseQualityService"/>
    /// <seealso cref="QCNetCorrectionSettings"/>
    public interface IQCNetCorrectionService
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
        /// Applies the 3-stage classifier pipeline + the parse-quality gate to the given
        /// observations. Modifies observations in-place and returns the same list. When the
        /// quality service is registered, each observation receives a
        /// <c>QC_PARSE_QUALITY:{score:F4}</c> flag in
        /// <see cref="ParsedObservation.ValidationFlags"/> plus a companion
        /// <c>QC_PARSE_QUALITY:REVIEW_REASONS:{list}</c> when any penalty fires.
        /// </summary>
        /// <param name="observations">Parsed observations from Stage 3.25.</param>
        /// <returns>The same list with ML corrections + parse-quality flags applied.</returns>
        List<ParsedObservation> ScoreAndCorrect(List<ParsedObservation> observations);

        /**************************************************************/
        /// <summary>
        /// Feeds Claude-corrected observations back into ML as ground-truth training data.
        /// Only rows with <c>AI_CORRECTED:</c> in
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
