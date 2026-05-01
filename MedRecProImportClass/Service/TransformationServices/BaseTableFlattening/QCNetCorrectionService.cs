using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Text.RegularExpressions;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// ML.NET-based Stage 3.4 correction service. Trains three classification models from
    /// in-memory accumulated high-confidence rows (TableCategory, DoseRegimen routing,
    /// PrimaryValueType disambiguation), applies them to each batch, and then delegates
    /// Claude-forwarding decisions to the deterministic
    /// <see cref="IParseQualityService"/> which emits
    /// <c>QC_PARSE_QUALITY:{score}</c> + <c>QC_PARSE_QUALITY:REVIEW_REASONS:{list}</c>
    /// flags the downstream Claude gate reads.
    /// </summary>
    /// <remarks>
    /// ## Architecture
    /// No database dependency — training uses in-memory accumulation from processed batches.
    /// High-confidence rows (<see cref="QCNetCorrectionSettings.BootstrapMinParseConfidence"/>)
    /// are collected after each <see cref="ScoreAndCorrect"/> call. Models train/retrain when
    /// the accumulator grows by <see cref="QCNetCorrectionSettings.RetrainingBatchSize"/> rows
    /// and at least <see cref="QCNetCorrectionSettings.MinTrainingRowsPerCategory"/> rows have
    /// accumulated overall.
    ///
    /// ## Cold-Start
    /// Batch 1: No models → classifiers are no-ops (nothing to correct). Parse-quality flags
    /// still emit on every observation since the quality service is rule-based.
    /// Batch 2+: If accumulator meets threshold, models train before scoring.
    ///
    /// ## Thread Safety
    /// <c>PredictionEngine</c> is single-threaded — safe for current sequential batch processing.
    ///
    /// ## Stage 4 Retirement (2026-04-24)
    /// The former Stage 4 PCA anomaly pipeline (PerKey + UnifiedGlobal strategies, per-UNII
    /// z-score features, adaptive threshold ratcheting) was retired because raw reconstruction-
    /// error scores cluster in a narrow band regardless of training-set shape, making any
    /// fixed threshold a continual tuning exercise. The parse-quality gate replaces it:
    /// deterministic, rule-based, targeting parse-alignment failures directly.
    /// </remarks>
    /// <seealso cref="IQCNetCorrectionService"/>
    /// <seealso cref="IParseQualityService"/>
    /// <seealso cref="QCNetCorrectionSettings"/>
    /// <seealso cref="ColumnStandardizationService"/>
    public class QCNetCorrectionService : IQCNetCorrectionService
    {
        #region Fields

        /**************************************************************/
        /// <summary>Logger for diagnostics.</summary>
        private readonly ILogger<QCNetCorrectionService> _logger;

        /**************************************************************/
        /// <summary>Configuration settings.</summary>
        private readonly QCNetCorrectionSettings _settings;

        /**************************************************************/
        /// <summary>ML.NET context with fixed seed for reproducibility.</summary>
        private readonly MLContext _mlContext = new MLContext(seed: 42);

        /**************************************************************/
        /// <summary>
        /// In-memory training accumulator. High-confidence rows from processed batches are
        /// collected here after each <see cref="ScoreAndCorrect"/> call to build training data.
        /// </summary>
        private List<QCTrainingRecord> _trainingAccumulator = new();

        /**************************************************************/
        /// <summary>
        /// Optional file-backed training store for persistence across restarts.
        /// Null when <see cref="QCNetCorrectionSettings.TrainingStoreFilePath"/> is not configured.
        /// </summary>
        private readonly IQCTrainingStore? _trainingStore;

        /**************************************************************/
        /// <summary>
        /// Optional parse-quality service. When provided, every scored observation receives
        /// a <c>QC_PARSE_QUALITY:{score}</c> flag and a companion REVIEW_REASONS flag
        /// when any rule penalty fires. The downstream Claude gate reads these flags to
        /// decide forwarding.
        /// </summary>
        /// <seealso cref="IParseQualityService"/>
        private readonly IParseQualityService? _parseQualityService;

        /**************************************************************/
        /// <summary>
        /// Parse-quality score below which the <c>QC_PARSE_QUALITY:REVIEW_REASONS</c>
        /// flag is emitted. Captured from
        /// <see cref="ClaudeApiCorrectionSettings.ClaudeReviewQualityThreshold"/> at
        /// construction time so the reason list only appears on rows that will actually
        /// be forwarded to Claude — rows scoring at or above this threshold record only
        /// the numeric score, keeping the reason breakdown as an honest Claude-burden
        /// indicator. Defaults to 0.75 when Claude settings are not injected.
        /// </summary>
        /// <remarks>
        /// Kept in sync with the downstream Claude gate by construction (reads the same
        /// settings value). The numeric <c>QC_PARSE_QUALITY:{score}</c> flag is still
        /// emitted on every observation regardless of threshold — only the reason list
        /// is gated.
        /// </remarks>
        private readonly float _reasonEmissionThreshold;

        /**************************************************************/
        /// <summary>Tracks accumulator size at last training to determine when to retrain.</summary>
        private int _accumulatorSizeAtLastTrain;

        /**************************************************************/
        /// <summary>Stage 1: TableCategory multiclass classifier.</summary>
        private PredictionEngine<TableCategoryInput, TableCategoryPrediction>? _tableCategoryEngine;

        /**************************************************************/
        /// <summary>Stage 2: DoseRegimen routing classifier.</summary>
        private PredictionEngine<DoseRegimenRoutingInput, DoseRegimenRoutingPrediction>? _doseRegimenEngine;

        /**************************************************************/
        /// <summary>Stage 3: PrimaryValueType disambiguation classifier.</summary>
        private PredictionEngine<PrimaryValueTypeInput, PrimaryValueTypePrediction>? _primaryValueTypeEngine;

        /**************************************************************/
        /// <summary>Whether <see cref="InitializeAsync"/> has been called.</summary>
        private bool _initialized;

        #endregion Fields

        #region DoseRegimen Label Synthesis Patterns

        // Former _pkSubParams / _pkSubParamPrefixPattern fields were migrated to
        // the shared PkParameterDictionary. Callers use
        // PkParameterDictionary.IsPkParameter / StartsWithPk.

        /**************************************************************/
        /// <summary>Actual dose pattern — digit followed by a unit.</summary>
        private static readonly Regex _actualDosePattern = new(
            @"\d+\s*(mg|mcg|µg|g|mL|units?|IU)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>Residual population pattern.</summary>
        private static readonly Regex _residualPopulationPattern = new(
            @"\b(adult|pediatric|elderly|renal|hepatic|healthy|volunteer|geriatric|obese)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>Residual timepoint pattern.</summary>
        private static readonly Regex _residualTimepointPattern = new(
            @"\b(day\s*\d|week\s*\d|month\s*\d|cycle\s*\d|baseline|steady[\.\-\s]?state|single[\.\-\s]?dose|pre[\-\s]?dose)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion DoseRegimen Label Synthesis Patterns

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the ML.NET correction service.
        /// </summary>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <param name="settings">Configuration settings.</param>
        /// <param name="trainingStore">Optional file-backed training store for persistence across restarts.
        /// Pass null to use ephemeral in-memory accumulation only.</param>
        /// <param name="parseQualityService">Optional parse-quality service. When provided,
        /// every scored observation receives <c>QC_PARSE_QUALITY</c> flags that drive
        /// the Claude forwarding gate.</param>
        /// <param name="claudeSettings">Optional Claude correction settings. Only the
        /// <see cref="ClaudeApiCorrectionSettings.ClaudeReviewQualityThreshold"/> value is
        /// read, at construction time, to gate <c>QC_PARSE_QUALITY:REVIEW_REASONS</c>
        /// emission so the reason list only appears on rows that will actually be
        /// forwarded to Claude. When not provided, defaults to 0.75.</param>
        public QCNetCorrectionService(
            ILogger<QCNetCorrectionService> logger,
            QCNetCorrectionSettings settings,
            IQCTrainingStore? trainingStore = null,
            IParseQualityService? parseQualityService = null,
            ClaudeApiCorrectionSettings? claudeSettings = null)
        {
            #region implementation

            _logger = logger;
            _settings = settings;
            _trainingStore = trainingStore;
            _parseQualityService = parseQualityService;
            _reasonEmissionThreshold = claudeSettings?.ClaudeReviewQualityThreshold ?? 0.75f;

            #endregion
        }

        #endregion Constructor

        #region IQCNetCorrectionService Implementation

        /**************************************************************/
        /// <inheritdoc/>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            #region implementation

            if (_trainingStore != null)
            {
                await _trainingStore.LoadAsync(ct);
                _trainingAccumulator = _trainingStore.GetRecords().ToList();

                _logger.LogInformation(
                    "Loaded {Count} training records from store",
                    _trainingAccumulator.Count);
            }

            _initialized = true;
            _logger.LogInformation(
                "QCNetCorrectionService initialized — classifiers train after {Min} rows accumulate",
                _settings.MinTrainingRowsPerCategory);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public List<ParsedObservation> ScoreAndCorrect(List<ParsedObservation> observations)
        {
            #region implementation

            if (observations.Count == 0)
                return observations;

            if (!_initialized)
            {
                _logger.LogWarning("QCNetCorrectionService not initialized — passing {Count} observations through", observations.Count);
                return observations;
            }

            if (!_settings.Enabled)
            {
                _logger.LogDebug("ML correction disabled — passing {Count} observations through", observations.Count);
                return observations;
            }

            // Attempt retrain if accumulator has grown enough
            tryRetrain();

            // Apply 3-stage pipeline + parse-quality gate to each observation. R9 — each
            // classifier stage is gated by its own enable toggle so stages can be disabled
            // (or run in shadow mode for Stage 1/2) without silencing the whole service.
            foreach (var obs in observations)
            {
                var preMlFlags = obs.ValidationFlags;

                // Stage 1: TableCategory validation (R9 gated — default-off until retrained).
                // Always calls applyTableCategoryCorrection; the method itself honors the
                // EnableStage1TableCategoryCorrection + EnableStage1ShadowMode toggles.
                applyTableCategoryCorrection(obs);

                // Stage 2: DoseRegimen routing (skip if already routed by rules)
                if (_settings.EnableStage2DoseRegimenRouting)
                    applyDoseRegimenRouting(obs);

                // Stage 3: PrimaryValueType disambiguation (only if "Numeric")
                if (_settings.EnableStage3PrimaryValueTypeDisambiguation)
                    applyPrimaryValueTypeDisambiguation(obs);

                // Stage 3.4 parse-quality gate — deterministic, replaces retired Stage 4
                // anomaly scoring. Emits QC_PARSE_QUALITY:{score} on every observation
                // and, when score < threshold, a QC_PARSE_QUALITY:REVIEW_REASONS:{list}
                // flag for audit. Only runs when the service is registered (null-safe to
                // keep the old test fixtures that construct QCNetCorrectionService without
                // the quality service still working).
                if (_parseQualityService != null)
                {
                    var quality = _parseQualityService.Evaluate(obs);
                    appendFlag(obs, $"QC_PARSE_QUALITY:{quality.Score:F4}");

                    // Only emit REVIEW_REASONS when the observation will actually be forwarded
                    // to Claude (score < threshold). Rows scoring at or above the threshold
                    // still get the numeric score for audit but skip the reason list — this
                    // keeps the aggregate reason breakdown an honest Claude-burden indicator
                    // instead of counting penalties on rows that aren't going to the API.
                    if (quality.Reasons.Count > 0 && quality.Score < _reasonEmissionThreshold)
                    {
                        appendFlag(obs, $"QC_PARSE_QUALITY:REVIEW_REASONS:{string.Join("|", quality.Reasons)}");
                    }
                }

                // Confidence provenance: summarize highest-confidence ML correction applied
                var correctionLabel = determineMlCorrectionLabel(preMlFlags, obs.ValidationFlags);
                appendFlag(obs, $"CONFIDENCE:ML:{obs.ParseConfidence ?? 0:F2}:{correctionLabel}");
            }

            // Accumulate high-confidence rows for future training
            accumulateBatch(observations);

            return observations;

            #endregion
        }

        #endregion IQCNetCorrectionService Implementation

        #region Stage 1 — TableCategory Validation

        /**************************************************************/
        /// <summary>
        /// Stage 1: Validates and optionally corrects TableCategory using the multiclass classifier.
        /// Only overrides when the model's max score exceeds <see cref="QCNetCorrectionSettings.TableCategoryMinConfidence"/>
        /// and the predicted category differs from the current one.
        /// </summary>
        /// <remarks>
        /// ## R9 — Default-off + shadow mode
        /// When <see cref="QCNetCorrectionSettings.EnableStage1TableCategoryCorrection"/>
        /// is false (the R9 default), the classifier does NOT mutate
        /// <see cref="ParsedObservation.TableCategory"/> and does NOT emit a
        /// <c>QC:CATEGORY_CORRECTED</c> flag. If
        /// <see cref="QCNetCorrectionSettings.EnableStage1ShadowMode"/> is also true
        /// (the default), the same prediction pipeline runs and emits a
        /// <c>QC:CATEGORY_SHADOW:{label}:{score}</c> flag when the prediction WOULD
        /// have triggered a correction — same confidence + label-differs gates.
        /// This lets the classifier's behavior be audited from JSONL without affecting
        /// downstream routing, category filtering, or compliance metrics.
        /// </remarks>
        /// <param name="obs">Observation to evaluate.</param>
        private void applyTableCategoryCorrection(ParsedObservation obs)
        {
            #region implementation

            // Fast-path: if Stage 1 correction AND shadow mode are both disabled,
            // there's nothing to do at all — skip the prediction call entirely.
            if (!_settings.EnableStage1TableCategoryCorrection &&
                !_settings.EnableStage1ShadowMode)
            {
                return;
            }

            var input = new TableCategoryInput
            {
                Caption = obs.Caption ?? string.Empty,
                SectionTitle = obs.SectionTitle ?? string.Empty,
                ParentSectionCode = obs.ParentSectionCode ?? string.Empty,
                ParseRule = obs.ParseRule ?? string.Empty
            };

            if (_settings.EnableStage1TableCategoryCorrection)
            {
                // Active correction path: mutate tableCategory, emit CATEGORY_CORRECTED.
                executePredictionStage(
                    obs,
                    _tableCategoryEngine,
                    input,
                    p => p.PredictedLabel,
                    p => p.Score?.Length > 0 ? p.Score.Max() : 0f,
                    _settings.TableCategoryMinConfidence,
                    obs.TableCategory,
                    (prediction, maxScore) =>
                    {
                        var oldCategory = obs.TableCategory;
                        obs.TableCategory = prediction.PredictedLabel;
                        appendFlag(obs, $"QC:CATEGORY_CORRECTED:{prediction.PredictedLabel}:{maxScore:F2}");
                        _logger.LogDebug("Stage 1: TableCategory corrected '{Old}' → '{New}' (score={Score:F2})",
                            oldCategory, prediction.PredictedLabel, maxScore);
                    },
                    stageNumber: 1);
            }
            else
            {
                // Shadow-only path: run prediction, emit CATEGORY_SHADOW flag, do NOT
                // mutate tableCategory. Same gates as the active path — only emits when
                // the prediction meets the confidence threshold AND differs from the
                // current category.
                executePredictionStage(
                    obs,
                    _tableCategoryEngine,
                    input,
                    p => p.PredictedLabel,
                    p => p.Score?.Length > 0 ? p.Score.Max() : 0f,
                    _settings.TableCategoryMinConfidence,
                    obs.TableCategory,
                    (prediction, maxScore) =>
                    {
                        // Shadow emission only — no mutation.
                        appendFlag(obs, $"QC:CATEGORY_SHADOW:{prediction.PredictedLabel}:{maxScore:F2}");
                        _logger.LogDebug("Stage 1 [SHADOW]: would have corrected '{Old}' → '{New}' (score={Score:F2})",
                            obs.TableCategory, prediction.PredictedLabel, maxScore);
                    },
                    stageNumber: 1);
            }

            // PR #6 dual-write audit — when both correction and shadow are on AND the
            // audit flag is set, emit a CATEGORY_SHADOW flag for every confident prediction,
            // including agreements. Lets the first few production runs after re-enable be
            // reconstructed end-to-end, not just the surprising disagreements.
            if (_settings.EnableStage1DualWriteAudit &&
                _settings.EnableStage1TableCategoryCorrection &&
                _settings.EnableStage1ShadowMode &&
                _tableCategoryEngine != null)
            {
                emitDualWriteCategoryShadow(obs, input);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PR #6 infrastructure — emits <c>QC:CATEGORY_SHADOW:{label}:{score}</c> for every
        /// Stage 1 prediction whose confidence clears
        /// <see cref="QCNetCorrectionSettings.TableCategoryMinConfidence"/>, regardless of
        /// whether the predicted label matches the current <c>TableCategory</c>. Used when
        /// <see cref="QCNetCorrectionSettings.EnableStage1DualWriteAudit"/> is enabled.
        /// </summary>
        /// <remarks>
        /// Already-emitted <c>CATEGORY_CORRECTED</c> / <c>CATEGORY_SHADOW</c> flags are left
        /// in place; the dual-write emission is additive. The intent is a complete audit trail
        /// for the first production runs after re-enabling the Stage 1 classifier — every
        /// confident prediction becomes inspectable in the resulting JSONL.
        /// </remarks>
        /// <param name="obs">Observation being audited.</param>
        /// <param name="input">Stage 1 input vector (already constructed by caller).</param>
        /// <seealso cref="QCNetCorrectionSettings.EnableStage1DualWriteAudit"/>
        private void emitDualWriteCategoryShadow(ParsedObservation obs, TableCategoryInput input)
        {
            #region implementation

            try
            {
                var prediction = _tableCategoryEngine!.Predict(input);
                var label = prediction.PredictedLabel;
                var maxScore = prediction.Score?.Length > 0 ? prediction.Score.Max() : 0f;

                if (string.IsNullOrEmpty(label) || maxScore < _settings.TableCategoryMinConfidence)
                    return;

                appendFlag(obs, $"QC:CATEGORY_SHADOW:{label}:{maxScore:F2}");
                _logger.LogDebug("Stage 1 [DUAL_WRITE]: predicted '{Label}' for current '{Cat}' (score={Score:F2})",
                    label, obs.TableCategory, maxScore);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Stage 1 dual-write audit prediction failed for SourceRowSeq={Row}",
                    obs.SourceRowSeq);
            }

            #endregion
        }

        #endregion Stage 1

        #region Stage 2 — DoseRegimen Routing

        /**************************************************************/
        /// <summary>
        /// Stage 2: Routes misplaced DoseRegimen content to the correct column using the
        /// routing classifier. Skips if the observation was already routed by Stage 3.25 rules
        /// (indicated by <c>COL_STD:DOSEREGIMEN_ROUTED_TO</c> in ValidationFlags).
        /// </summary>
        /// <remarks>
        /// ## PR #4 — Correction vs. shadow mode
        /// When <see cref="QCNetCorrectionSettings.EnableStage2DoseRegimenRoutingCorrection"/>
        /// is <c>true</c> (the default), the stage mutates the observation and emits
        /// <c>QC:DOSEREGIMEN_ROUTED_TO_*</c> — the original behaviour. When that flag is
        /// <c>false</c> but <see cref="QCNetCorrectionSettings.EnableStage2ShadowMode"/> is
        /// <c>true</c>, the stage emits <c>QC:DOSEREGIMEN_SHADOW:{target}:{score}</c> for
        /// audit without touching the observation. When both are off, the stage short-circuits
        /// without running a prediction.
        /// </remarks>
        /// <param name="obs">Observation to evaluate.</param>
        private void applyDoseRegimenRouting(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrEmpty(obs.DoseRegimen))
                return;

            // Skip if already routed by ColumnStandardizationService rules
            if (DoseRegimenRoutingPolicy.IsAlreadyRouted(obs.ValidationFlags))
                return;

            // Fast-path: both correction and shadow are off → no prediction work at all.
            if (!_settings.EnableStage2DoseRegimenRoutingCorrection && !_settings.EnableStage2ShadowMode)
                return;

            var input = new DoseRegimenRoutingInput
            {
                DoseRegimen = obs.DoseRegimen ?? string.Empty,
                TableCategory = obs.TableCategory ?? string.Empty,
                Caption = obs.Caption ?? string.Empty,
                ParameterName = obs.ParameterName ?? string.Empty,
                HasDose = obs.Dose.HasValue ? 1f : 0f
            };

            if (_settings.EnableStage2DoseRegimenRoutingCorrection)
            {
                executePredictionStage(
                    obs,
                    _doseRegimenEngine,
                    input,
                    p => p.PredictedLabel,
                    p => p.Score?.Length > 0 ? p.Score.Max() : 0f,
                    0.80f,
                    DoseRegimenRoutingPolicy.TargetLabelKeep,
                    (prediction, maxScore) =>
                    {
                        var target = DoseRegimenRoutingPolicy.ParseTarget(prediction.PredictedLabel);
                        DoseRegimenRoutingPolicy.ApplyRoute(obs, target);
                        appendFlag(obs, $"QC:DOSEREGIMEN_ROUTED_TO_{prediction.PredictedLabel!.ToUpperInvariant()}:{maxScore:F2}");
                        _logger.LogDebug("Stage 2: DoseRegimen routed to {Target} (score={Score:F2})",
                            prediction.PredictedLabel, maxScore);
                    },
                    stageNumber: 2);
            }
            else
            {
                // Shadow-only path: prediction runs, flag emits, observation stays untouched.
                // Mirrors the Stage 1 shadow-mode pattern for consistency.
                executePredictionStage(
                    obs,
                    _doseRegimenEngine,
                    input,
                    p => p.PredictedLabel,
                    p => p.Score?.Length > 0 ? p.Score.Max() : 0f,
                    0.80f,
                    DoseRegimenRoutingPolicy.TargetLabelKeep,
                    (prediction, maxScore) =>
                    {
                        appendFlag(obs, $"QC:DOSEREGIMEN_SHADOW:{prediction.PredictedLabel}:{maxScore:F2}");
                        _logger.LogDebug("Stage 2 [SHADOW]: would have routed DoseRegimen to {Target} (score={Score:F2})",
                            prediction.PredictedLabel, maxScore);
                    },
                    stageNumber: 2);
            }

            #endregion
        }

        #endregion Stage 2

        #region Stage 3 — PrimaryValueType Disambiguation

        /**************************************************************/
        /// <summary>
        /// Stage 3: Disambiguates <c>PrimaryValueType == "Numeric"</c> to a more specific type
        /// using the classification model. Only fires when the current type is "Numeric" and the
        /// model predicts a non-Numeric type with sufficient confidence.
        /// </summary>
        /// <param name="obs">Observation to evaluate.</param>
        private void applyPrimaryValueTypeDisambiguation(ParsedObservation obs)
        {
            #region implementation

            if (!string.Equals(obs.PrimaryValueType, "Numeric", StringComparison.OrdinalIgnoreCase))
                return;

            var input = new PrimaryValueTypeInput
            {
                Unit = obs.Unit ?? string.Empty,
                TableCategory = obs.TableCategory ?? string.Empty,
                ParseRule = obs.ParseRule ?? string.Empty,
                Caption = obs.Caption ?? string.Empty,
                HasLowerBound = obs.LowerBound.HasValue ? 1f : 0f,
                HasUpperBound = obs.UpperBound.HasValue ? 1f : 0f
            };

            executePredictionStage(
                obs,
                _primaryValueTypeEngine,
                input,
                p => p.PredictedLabel,
                p => p.Score?.Length > 0 ? p.Score.Max() : 0f,
                0.80f,
                "Numeric",
                (prediction, maxScore) =>
                {
                    var oldType = obs.PrimaryValueType;
                    obs.PrimaryValueType = prediction.PredictedLabel;
                    appendFlag(obs, $"QC:PVTYPE_DISAMBIGUATED:{prediction.PredictedLabel}:{maxScore:F2}");
                    _logger.LogDebug("Stage 3: PrimaryValueType disambiguated '{Old}' → '{New}' (score={Score:F2})",
                        oldType, prediction.PredictedLabel, maxScore);
                },
                stageNumber: 3);

            #endregion
        }

        #endregion Stage 3

        #region Training — Retrain Trigger and Accumulator

        /**************************************************************/
        /// <summary>
        /// Checks whether the accumulator has grown enough since last training to trigger a retrain.
        /// Called at the start of each <see cref="ScoreAndCorrect"/> invocation.
        /// </summary>
        private void tryRetrain()
        {
            #region implementation

            var newRows = _trainingAccumulator.Count - _accumulatorSizeAtLastTrain;
            if (newRows < _settings.RetrainingBatchSize)
                return;

            // Simple absolute floor — the three classifiers train across the whole accumulator
            // with text featurization and don't need per-category slicing. The former Stage 4
            // per-key gate (requiring MinTrainingRowsPerCategory rows per composite key) was
            // retired alongside the anomaly pipeline.
            if (_trainingAccumulator.Count < _settings.MinTrainingRowsPerCategory)
                return;

            _logger.LogInformation(
                "ML retrain triggered: {NewRows} new rows since last train (accumulator={Total})",
                newRows, _trainingAccumulator.Count);

            trainTableCategoryModel(_trainingAccumulator);
            trainDoseRegimenModel(_trainingAccumulator);
            trainPrimaryValueTypeModel(_trainingAccumulator);

            _accumulatorSizeAtLastTrain = _trainingAccumulator.Count;

            if (_trainingStore != null)
            {
                // Record retrain timestamp and persist state
                _ = _trainingStore.RecordRetrainAsync();
            }

            _logger.LogInformation(
                "ML retrain complete. Accumulator size: {Size}",
                _trainingAccumulator.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Collects high-confidence rows from the batch into the training accumulator as
        /// <see cref="QCTrainingRecord"/> instances (bootstrap, not ground truth).
        /// Called at the end of each <see cref="ScoreAndCorrect"/> invocation.
        /// </summary>
        /// <param name="observations">Observations from the current batch.</param>
        private void accumulateBatch(IEnumerable<ParsedObservation> observations)
        {
            #region implementation

            var newRecords = new List<QCTrainingRecord>();

            foreach (var obs in observations)
            {
                if (obs.ParseConfidence >= _settings.BootstrapMinParseConfidence)
                {
                    newRecords.Add(QCTrainingRecord.FromObservation(obs, isGroundTruth: false));
                }
            }

            if (newRecords.Count > 0)
            {
                appendAndCapAccumulator(newRecords);

                if (_trainingStore != null)
                {
                    // Fire-and-forget save — the store handles thread safety
                    _ = _trainingStore.AddRecordsAsync(newRecords);
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Appends training records to the in-memory accumulator and enforces
        /// <see cref="QCNetCorrectionSettings.MaxAccumulatorRows"/>, shifting the retrain cursor
        /// so the gate delta in <see cref="tryRetrain"/> stays correct after oldest-first trim.
        /// </summary>
        /// <param name="records">Records to append.</param>
        private void appendAndCapAccumulator(List<QCTrainingRecord> records)
        {
            #region implementation

            if (records.Count == 0)
                return;

            _trainingAccumulator.AddRange(records);

            var overflow = _trainingAccumulator.Count - _settings.MaxAccumulatorRows;
            if (overflow > 0)
            {
                _trainingAccumulator.RemoveRange(0, overflow);
                _accumulatorSizeAtLastTrain = Math.Max(0, _accumulatorSizeAtLastTrain - overflow);
            }

            #endregion
        }

        #endregion Training — Retrain Trigger and Accumulator

        #region Model Training Helpers

        /**************************************************************/
        /// <summary>
        /// Generic multiclass training driver — handles filter/project, label-cardinality guard,
        /// <c>LoadFromEnumerable</c>, pipeline fit, engine disposal/replace, and standardized logging.
        /// </summary>
        /// <typeparam name="TInput">Typed training input DTO (e.g. <c>TableCategoryInput</c>).</typeparam>
        /// <param name="rows">Source training records.</param>
        /// <param name="project">Filter + project step producing the typed training rows.</param>
        /// <param name="labelOf">Accessor returning the label string for the distinct-cardinality guard.</param>
        /// <param name="fitPipeline">Builds the ML.NET pipeline and fits it against the supplied <see cref="IDataView"/>.</param>
        /// <param name="replaceEngine">Disposes the outgoing engine and assigns a new one on success, or nulls it on failure.</param>
        /// <param name="stagePrefix">Log prefix — e.g. "Stage 1".</param>
        /// <param name="skipLabelKind">Label noun used in the "fewer than 2 distinct X labels" skip message.</param>
        /// <param name="modelKind">Model noun used in the success/failure messages.</param>
        private void trainMulticlassModel<TInput>(
            List<QCTrainingRecord> rows,
            Func<IEnumerable<QCTrainingRecord>, IEnumerable<TInput>> project,
            Func<TInput, string?> labelOf,
            Func<IDataView, ITransformer> fitPipeline,
            Action<ITransformer?> replaceEngine,
            string stagePrefix,
            string skipLabelKind,
            string modelKind) where TInput : class
        {
            #region implementation

            try
            {
                var trainingData = project(rows).ToList();

                // Guard: need at least 2 distinct labels for multiclass training to succeed.
                if (trainingData.Select(labelOf).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2)
                {
                    _logger.LogDebug("{Stage} training skipped — fewer than 2 distinct {Kind} labels",
                        stagePrefix, skipLabelKind);
                    return;
                }

                var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);
                var model = fitPipeline(dataView);
                replaceEngine(model);

                _logger.LogDebug("{Stage} {Kind} model trained on {Count} rows",
                    stagePrefix, modelKind, trainingData.Count);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentOutOfRangeException)
            {
                _logger.LogWarning(ex, "{Stage} {Kind} model training failed — engine remains null",
                    stagePrefix, modelKind);
                replaceEngine(null);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 1 training: Multiclass classifier for TableCategory prediction.
        /// Uses text featurization of Caption, SectionTitle, ParentSectionCode, and ParseRule.
        /// </summary>
        /// <param name="rows">Training data from accumulator.</param>
        private void trainTableCategoryModel(List<QCTrainingRecord> rows)
        {
            #region implementation

            trainMulticlassModel<TableCategoryInput>(
                rows,
                source => source
                    .Where(r => !string.IsNullOrEmpty(r.TableCategory))
                    .Select(r => new TableCategoryInput
                    {
                        Caption = r.Caption ?? string.Empty,
                        SectionTitle = r.SectionTitle ?? string.Empty,
                        ParentSectionCode = r.ParentSectionCode ?? string.Empty,
                        ParseRule = r.ParseRule ?? string.Empty,
                        TableCategory = r.TableCategory!
                    }),
                t => t.TableCategory,
                dataView =>
                {
                    var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(TableCategoryInput.TableCategory))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("CaptionFeatures", nameof(TableCategoryInput.Caption)))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("SectionFeatures", nameof(TableCategoryInput.SectionTitle)))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("LoincFeatures", nameof(TableCategoryInput.ParentSectionCode)))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("ParseRuleFeatures", nameof(TableCategoryInput.ParseRule)))
                        .Append(_mlContext.Transforms.Concatenate("Features", "CaptionFeatures", "SectionFeatures", "LoincFeatures", "ParseRuleFeatures"))
                        .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features"))
                        .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
                    return pipeline.Fit(dataView);
                },
                model =>
                {
                    if (model != null)
                    {
                        (_tableCategoryEngine as IDisposable)?.Dispose();
                        _tableCategoryEngine = _mlContext.Model.CreatePredictionEngine<TableCategoryInput, TableCategoryPrediction>(model);
                    }
                    else
                    {
                        _tableCategoryEngine = null;
                    }
                },
                stagePrefix: "Stage 1",
                skipLabelKind: "TableCategory",
                modelKind: "TableCategory");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 2 training: Multiclass classifier for DoseRegimen routing.
        /// Labels are synthesized using the same regex patterns as
        /// <see cref="ColumnStandardizationService.normalizeDoseRegimen"/>.
        /// </summary>
        /// <param name="rows">Training data from accumulator.</param>
        private void trainDoseRegimenModel(List<QCTrainingRecord> rows)
        {
            #region implementation

            trainMulticlassModel<DoseRegimenRoutingInput>(
                rows,
                source => source
                    .Where(r => !string.IsNullOrEmpty(r.DoseRegimen) || hasRoutingFlagOnRecord(r))
                    .Select(r => new DoseRegimenRoutingInput
                    {
                        DoseRegimen = r.DoseRegimen ?? string.Empty,
                        TableCategory = r.TableCategory ?? string.Empty,
                        Caption = r.Caption ?? string.Empty,
                        ParameterName = r.ParameterName ?? string.Empty,
                        HasDose = r.Dose.HasValue ? 1f : 0f,
                        RoutingTarget = labelDoseRegimenRoutingFromRecord(r)
                    })
                    .Where(r => r.RoutingTarget != null),
                t => t.RoutingTarget,
                dataView =>
                {
                    var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(DoseRegimenRoutingInput.RoutingTarget))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("DoseFeatures", nameof(DoseRegimenRoutingInput.DoseRegimen)))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("CategoryFeatures", nameof(DoseRegimenRoutingInput.TableCategory)))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("CaptionFeatures", nameof(DoseRegimenRoutingInput.Caption)))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("ParamFeatures", nameof(DoseRegimenRoutingInput.ParameterName)))
                        .Append(_mlContext.Transforms.Concatenate("Features", "DoseFeatures", "CategoryFeatures", "CaptionFeatures", "ParamFeatures", nameof(DoseRegimenRoutingInput.HasDose)))
                        .Append(_mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features"))
                        .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
                    return pipeline.Fit(dataView);
                },
                model =>
                {
                    if (model != null)
                    {
                        (_doseRegimenEngine as IDisposable)?.Dispose();
                        _doseRegimenEngine = _mlContext.Model.CreatePredictionEngine<DoseRegimenRoutingInput, DoseRegimenRoutingPrediction>(model);
                    }
                    else
                    {
                        _doseRegimenEngine = null;
                    }
                },
                stagePrefix: "Stage 2",
                skipLabelKind: "routing",
                modelKind: "DoseRegimen");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stage 3 training: Multiclass classifier for PrimaryValueType disambiguation.
        /// Trained only on rows where PrimaryValueType is NOT "Numeric" — those are ground truth.
        /// </summary>
        /// <param name="rows">Training data from accumulator.</param>
        private void trainPrimaryValueTypeModel(List<QCTrainingRecord> rows)
        {
            #region implementation

            trainMulticlassModel<PrimaryValueTypeInput>(
                rows,
                source => source
                    .Where(r => !string.IsNullOrEmpty(r.PrimaryValueType) &&
                                !string.Equals(r.PrimaryValueType, "Numeric", StringComparison.OrdinalIgnoreCase))
                    .Select(r => new PrimaryValueTypeInput
                    {
                        Unit = r.Unit ?? string.Empty,
                        TableCategory = r.TableCategory ?? string.Empty,
                        ParseRule = r.ParseRule ?? string.Empty,
                        Caption = r.Caption ?? string.Empty,
                        HasLowerBound = r.HasLowerBound ? 1f : 0f,
                        HasUpperBound = r.HasUpperBound ? 1f : 0f,
                        PrimaryValueType = r.PrimaryValueType!
                    }),
                t => t.PrimaryValueType,
                dataView =>
                {
                    var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(PrimaryValueTypeInput.PrimaryValueType))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("UnitFeatures", nameof(PrimaryValueTypeInput.Unit)))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("CategoryFeatures", nameof(PrimaryValueTypeInput.TableCategory)))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("ParseRuleFeatures", nameof(PrimaryValueTypeInput.ParseRule)))
                        .Append(_mlContext.Transforms.Text.FeaturizeText("CaptionFeatures", nameof(PrimaryValueTypeInput.Caption)))
                        .Append(_mlContext.Transforms.Concatenate("Features",
                            "UnitFeatures", "CategoryFeatures", "ParseRuleFeatures", "CaptionFeatures",
                            nameof(PrimaryValueTypeInput.HasLowerBound), nameof(PrimaryValueTypeInput.HasUpperBound)))
                        .Append(_mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features"))
                        .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));
                    return pipeline.Fit(dataView);
                },
                model =>
                {
                    if (model != null)
                    {
                        (_primaryValueTypeEngine as IDisposable)?.Dispose();
                        _primaryValueTypeEngine = _mlContext.Model.CreatePredictionEngine<PrimaryValueTypeInput, PrimaryValueTypePrediction>(model);
                    }
                    else
                    {
                        _primaryValueTypeEngine = null;
                    }
                },
                stagePrefix: "Stage 3",
                skipLabelKind: "PrimaryValueType",
                modelKind: "PrimaryValueType");

            #endregion
        }

        #endregion Model Training Helpers

        #region Label Synthesis Helpers

        /**************************************************************/
        /// <summary>
        /// Checks whether a <see cref="QCTrainingRecord"/> has any DoseRegimen routing flag
        /// from Stage 3.25 stored in its <see cref="QCTrainingRecord.ValidationFlags"/>.
        /// Delegates to <see cref="DoseRegimenRoutingPolicy.HasRoutingFlag"/>.
        /// </summary>
        private static bool hasRoutingFlagOnRecord(QCTrainingRecord record)
        {
            #region implementation

            return DoseRegimenRoutingPolicy.HasRoutingFlag(record.ValidationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Synthesizes a DoseRegimen routing label from a <see cref="QCTrainingRecord"/>
        /// for Stage 2 training. When the row has already been routed by rules, infers the
        /// label from its routing flags; otherwise applies the ML-tuned regex decision tree.
        /// </summary>
        /// <param name="record">Training record to label.</param>
        /// <returns>Routing target label or null.</returns>
        private static string? labelDoseRegimenRoutingFromRecord(QCTrainingRecord record)
        {
            #region implementation

            // If DoseRegimen was already routed by rules, infer label from flags
            var flagTarget = DoseRegimenRoutingPolicy.RouteTargetFromFlags(record.ValidationFlags);
            if (flagTarget != DoseRegimenRoutingPolicy.RouteTarget.None)
                return DoseRegimenRoutingPolicy.TargetLabel(flagTarget);

            if (string.IsNullOrWhiteSpace(record.DoseRegimen))
                return null;

            var val = record.DoseRegimen.Trim();

            // Priority 1: PK sub-parameter → ParameterSubtype
            if (PkParameterDictionary.IsPkParameter(val) || PkParameterDictionary.StartsWithPk(val))
                return DoseRegimenRoutingPolicy.TargetLabelParameterSubtype;

            // Priority 2: Actual dose → Keep
            if (_actualDosePattern.IsMatch(val))
                return DoseRegimenRoutingPolicy.TargetLabelKeep;

            // Priority 3: Population pattern
            if (_residualPopulationPattern.IsMatch(val))
                return DoseRegimenRoutingPolicy.TargetLabelPopulation;

            // Priority 4: Timepoint pattern
            if (_residualTimepointPattern.IsMatch(val))
                return DoseRegimenRoutingPolicy.TargetLabelTimepoint;

            // Default: Keep
            return DoseRegimenRoutingPolicy.TargetLabelKeep;

            #endregion
        }

        #endregion Label Synthesis Helpers

        #region Claude Feedback

        /**************************************************************/
        /// <inheritdoc/>
        public async Task FeedClaudeCorrectedBatchAsync(List<ParsedObservation> observations, CancellationToken ct = default)
        {
            #region implementation

            // Extract only Claude-corrected observations (those with AI_CORRECTED: flag)
            var corrected = observations
                .Where(o => o.ValidationFlags?.Contains("AI_CORRECTED:") == true)
                .ToList();

            if (corrected.Count == 0)
                return;

            // Convert corrected observations to ground-truth training records
            var records = corrected
                .Select(o => QCTrainingRecord.FromObservation(o, isGroundTruth: true))
                .ToList();

            if (_trainingStore != null)
            {
                await _trainingStore.AddRecordsAsync(records, ct);
            }
            else
            {
                // Ephemeral mode — accumulate in memory with cap enforcement.
                appendAndCapAccumulator(records);
            }

            _logger.LogDebug(
                "Fed {Corrected}/{Total} Claude-corrected observations as ground truth",
                corrected.Count, observations.Count);

            #endregion
        }

        #endregion Claude Feedback

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Generic prediction-stage executor — handles engine-null guard, Predict under try/catch,
        /// score computation, threshold + no-op-label gate, and consistent failure logging.
        /// Caller supplies accessors and the on-accept mutation block (which writes the observation,
        /// appends the flag, and emits the success log line).
        /// </summary>
        /// <typeparam name="TInput">Typed ML.NET input DTO for the stage.</typeparam>
        /// <typeparam name="TOutput">Typed ML.NET prediction DTO for the stage.</typeparam>
        /// <param name="obs">Observation being scored (used for the failure log context only).</param>
        /// <param name="engine">Prediction engine; the helper is a no-op when this is null.</param>
        /// <param name="input">Projected input DTO.</param>
        /// <param name="labelOf">Accessor returning the predicted label from the prediction DTO.</param>
        /// <param name="scoreOf">Accessor returning the scalar confidence from the prediction DTO.</param>
        /// <param name="minConfidence">Minimum confidence to trigger <paramref name="onAccept"/>.</param>
        /// <param name="noOpLabel">Label value against which the predicted label is compared; if equal (ordinal-ignore-case), <paramref name="onAccept"/> is not invoked.</param>
        /// <param name="onAccept">Callback invoked when the gate passes — mutates observation, appends flag, logs success.</param>
        /// <param name="stageNumber">Stage number used in the failure log ("Stage {N} prediction failed...").</param>
        private void executePredictionStage<TInput, TOutput>(
            ParsedObservation obs,
            PredictionEngine<TInput, TOutput>? engine,
            TInput input,
            Func<TOutput, string?> labelOf,
            Func<TOutput, float> scoreOf,
            float minConfidence,
            string? noOpLabel,
            Action<TOutput, float> onAccept,
            int stageNumber)
            where TInput : class
            where TOutput : class, new()
        {
            #region implementation

            if (engine == null)
                return;

            try
            {
                var prediction = engine.Predict(input);
                var maxScore = scoreOf(prediction);
                var label = labelOf(prediction);

                if (maxScore >= minConfidence &&
                    !string.Equals(label, noOpLabel, StringComparison.OrdinalIgnoreCase))
                {
                    onAccept(prediction, maxScore);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Stage {Stage} prediction failed for SourceRowSeq={Row}",
                    stageNumber, obs.SourceRowSeq);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Appends a flag to the observation's ValidationFlags field. Delegates to the shared
        /// <see cref="ValidationFlagExtensions.AppendValidationFlag"/> helper so the delimiter
        /// convention (<c>"; "</c>) stays in one place across services.
        /// </summary>
        /// <param name="obs">Observation to flag.</param>
        /// <param name="flag">Flag string to append.</param>
        private static void appendFlag(ParsedObservation obs, string flag) => obs.AppendValidationFlag(flag);

        /**************************************************************/
        /// <summary>
        /// Determines the highest-confidence ML correction label by comparing flags before
        /// and after the 3-stage classifier pipeline. Returns the first correction type found
        /// (CATEGORY_CORRECTED > DOSEREGIMEN_ROUTED > PVTYPE_DISAMBIGUATED) or "no_correction".
        /// </summary>
        /// <param name="preMlFlags">ValidationFlags before ML pipeline.</param>
        /// <param name="postMlFlags">ValidationFlags after ML pipeline.</param>
        /// <returns>A short label describing the highest-priority correction.</returns>
        private static string determineMlCorrectionLabel(string? preMlFlags, string? postMlFlags)
        {
            #region implementation

            if (postMlFlags == null || postMlFlags == preMlFlags)
                return "no_correction";

            // Check for new flags added by ML pipeline (not present before)
            var newPart = preMlFlags != null
                ? postMlFlags.Substring(preMlFlags.Length)
                : postMlFlags;

            if (newPart.Contains("QC:CATEGORY_CORRECTED"))
                return "CATEGORY_CORRECTED";
            if (newPart.Contains("QC:DOSEREGIMEN_ROUTED"))
                return "DOSEREGIMEN_ROUTED";
            if (newPart.Contains("QC:PVTYPE_DISAMBIGUATED"))
                return "PVTYPE_DISAMBIGUATED";

            return "no_correction";

            #endregion
        }

        #endregion Helper Methods
    }
}
