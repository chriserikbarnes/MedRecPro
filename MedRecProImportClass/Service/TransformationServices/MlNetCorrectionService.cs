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
    /// ML.NET-based Stage 3.4 correction and anomaly scoring service. Trains classification
    /// and anomaly detection models from in-memory accumulated high-confidence rows, then
    /// applies 4-stage corrections and emits per-row anomaly scores that gate the Claude API
    /// correction pass (Stage 3.5).
    /// </summary>
    /// <remarks>
    /// ## Architecture
    /// No database dependency — training uses in-memory accumulation from processed batches.
    /// High-confidence rows (<see cref="MlNetCorrectionSettings.BootstrapMinParseConfidence"/>)
    /// are collected after each <see cref="ScoreAndCorrect"/> call. Models train/retrain when
    /// the accumulator grows by <see cref="MlNetCorrectionSettings.RetrainingBatchSize"/> rows
    /// and at least <see cref="MlNetCorrectionSettings.MinTrainingRowsPerCategory"/> rows exist
    /// per active category.
    ///
    /// ## Cold-Start
    /// Batch 1: No models → all rows emit <c>MLNET_ANOMALY_SCORE:NOMODEL</c>.
    /// Batch 2+: If accumulator meets threshold, models train before scoring.
    ///
    /// ## Thread Safety
    /// <c>PredictionEngine</c> is single-threaded — safe for current sequential batch processing.
    /// </remarks>
    /// <seealso cref="IMlNetCorrectionService"/>
    /// <seealso cref="MlNetCorrectionSettings"/>
    /// <seealso cref="ColumnStandardizationService"/>
    public class MlNetCorrectionService : IMlNetCorrectionService
    {
        #region Fields

        /**************************************************************/
        /// <summary>Logger for diagnostics.</summary>
        private readonly ILogger<MlNetCorrectionService> _logger;

        /**************************************************************/
        /// <summary>Configuration settings.</summary>
        private readonly MlNetCorrectionSettings _settings;

        /**************************************************************/
        /// <summary>ML.NET context with fixed seed for reproducibility.</summary>
        private readonly MLContext _mlContext = new MLContext(seed: 42);

        /**************************************************************/
        /// <summary>
        /// In-memory training accumulator. High-confidence rows from processed batches are
        /// collected here after each <see cref="ScoreAndCorrect"/> call to build training data.
        /// Uses <see cref="MlTrainingRecord"/> — the compact DTO with only the fields training needs.
        /// </summary>
        private List<MlTrainingRecord> _trainingAccumulator = new();

        /**************************************************************/
        /// <summary>
        /// Optional file-backed training store for persistence across restarts.
        /// Null when <see cref="MlNetCorrectionSettings.TrainingStoreFilePath"/> is not configured.
        /// </summary>
        private readonly IMlTrainingStore? _trainingStore;

        /**************************************************************/
        /// <summary>
        /// Optional reference to Claude API settings for adaptive threshold propagation.
        /// When the adaptive threshold fires, <see cref="ClaudeApiCorrectionSettings.MlAnomalyScoreThreshold"/>
        /// is mutated on this shared singleton so the next Claude batch uses the updated threshold.
        /// </summary>
        private readonly ClaudeApiCorrectionSettings? _claudeSettings;

        /**************************************************************/
        /// <summary>
        /// Configured floor for the Claude anomaly gate, captured at construction time.
        /// The persisted adaptive threshold may raise the effective gate value above this floor,
        /// but must never demote it below. Without this floor, a freshly persisted training store
        /// (whose <c>AdaptiveThreshold</c> defaults to <c>0.0f</c>) would silently overwrite a
        /// user-configured <c>MlAnomalyScoreThreshold</c> (e.g. <c>0.75f</c>) and disable the
        /// cost gate for every observation — the exact scenario where score-0.70 rows were
        /// observed leaking through to Claude despite a 0.75 configuration.
        /// </summary>
        /// <seealso cref="ClaudeApiCorrectionSettings.MlAnomalyScoreThreshold"/>
        private readonly float _configuredAnomalyFloor;

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
        /// <summary>Stage 4: Per-category PCA anomaly detection engines.</summary>
        /// <remarks>
        /// Each engine's baked-in pipeline includes a <c>Concatenate("Features", ...)</c> step
        /// that reads only the columns with real variance at training time. Constant-zero columns
        /// are excluded from the model entirely — no jitter needed.
        /// </remarks>
        private readonly Dictionary<string, PredictionEngine<AnomalyFeatureRow, AnomalyPrediction>>
            _anomalyEngines = new(StringComparer.OrdinalIgnoreCase);

        // /**************************************************************/
        // /// <summary>
        // /// Per-category sorted training-time anomaly scores for percentile calibration.
        // /// Built during <see cref="trainAnomalyModels"/> by scoring all training rows through
        // /// the freshly-trained PCA model and sorting the results. At scoring time,
        // /// <see cref="calibrateScore"/> uses binary search to convert a raw PCA score
        // /// to a percentile (0.0 = typical, 1.0 = extreme outlier).
        // /// </summary>
        // /// <seealso cref="calibrateScore"/>
        // /// <seealso cref="trainAnomalyModels"/>
        // private readonly Dictionary<string, float[]> _anomalyCalibration = new(StringComparer.OrdinalIgnoreCase);

        /**************************************************************/
        /// <summary>Categories that get per-category anomaly detection models.</summary>
        private static readonly string[] _anomalyCategories =
        {
            "ADVERSE_EVENT", "PK", "EFFICACY", "DRUG_INTERACTION",
            "BMD", "DOSING", "TISSUE_DISTRIBUTION", "DEMOGRAPHIC", "LABORATORY"
        };

        /**************************************************************/
        /// <summary>
        /// Column names corresponding to the 7 feature slots in <see cref="AnomalyFeatureRow"/>.
        /// Used by <see cref="trainAnomalyModels"/> to build the dynamic <c>Concatenate("Features", ...)</c>
        /// step that includes only columns with real variance.
        /// </summary>
        /// <seealso cref="computeActiveFeatureIndices"/>
        private static readonly string[] _featureColumnNames =
        {
            nameof(AnomalyFeatureRow.PrimaryValue),
            nameof(AnomalyFeatureRow.SecondaryValue),
            nameof(AnomalyFeatureRow.LowerBound),
            nameof(AnomalyFeatureRow.UpperBound),
            nameof(AnomalyFeatureRow.PValue),
            nameof(AnomalyFeatureRow.ParseConfidence),
            nameof(AnomalyFeatureRow.LogArmN)
        };

        /**************************************************************/
        /// <summary>Recommended PCA ranks per category (from architecture spec).</summary>
        private static readonly Dictionary<string, int> _pcaRanks = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ADVERSE_EVENT"] = 5,
            ["PK"] = 4,
            ["DRUG_INTERACTION"] = 4,
            ["EFFICACY"] = 4,
            ["BMD"] = 2,
            ["DOSING"] = 3,
            ["TISSUE_DISTRIBUTION"] = 2,
            ["DEMOGRAPHIC"] = 3,
            ["LABORATORY"] = 3
        };

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
        /// <param name="claudeSettings">Optional Claude API settings for adaptive threshold propagation.
        /// When provided, the adaptive threshold is propagated to
        /// <see cref="ClaudeApiCorrectionSettings.MlAnomalyScoreThreshold"/> at runtime.</param>
        public MlNetCorrectionService(
            ILogger<MlNetCorrectionService> logger,
            MlNetCorrectionSettings settings,
            IMlTrainingStore? trainingStore = null,
            ClaudeApiCorrectionSettings? claudeSettings = null)
        {
            #region implementation

            _logger = logger;
            _settings = settings;
            _trainingStore = trainingStore;
            _claudeSettings = claudeSettings;

            // Capture the configured floor BEFORE any adaptive-threshold propagation can
            // mutate _claudeSettings.MlAnomalyScoreThreshold. This frozen value is used as
            // a lower bound in every subsequent write to the live settings instance.
            _configuredAnomalyFloor = claudeSettings?.MlAnomalyScoreThreshold ?? 0f;

            #endregion
        }

        #endregion Constructor

        #region IMlNetCorrectionService Implementation

        /**************************************************************/
        /// <inheritdoc/>
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            #region implementation

            if (_trainingStore != null)
            {
                await _trainingStore.LoadAsync(ct);
                _trainingAccumulator = _trainingStore.GetRecords().ToList();

                // Propagate the persisted adaptive threshold to the live Claude settings,
                // but never below the configured floor captured at construction. The store's
                // AdaptiveThreshold defaults to 0.0f on a fresh install — writing it raw
                // would silently demote a user-configured 0.75 floor and disable the gate.
                var persistedAdaptive = _trainingStore.GetAdaptiveThreshold();
                var effectiveThreshold = clampAndApplyAnomalyThreshold(persistedAdaptive);

                _logger.LogInformation(
                    "Loaded {Count} training records from store. Effective anomaly threshold: {Effective:F4} " +
                    "(floor={Floor:F4}, persisted adaptive={Persisted:F4})",
                    _trainingAccumulator.Count, effectiveThreshold, _configuredAnomalyFloor, persistedAdaptive);
            }

            _initialized = true;
            _logger.LogInformation(
                "MlNetCorrectionService initialized — models train after {Min} rows/category accumulate",
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
                _logger.LogWarning("MlNetCorrectionService not initialized — passing {Count} observations through", observations.Count);
                return observations;
            }

            if (!_settings.Enabled)
            {
                _logger.LogDebug("ML correction disabled — passing {Count} observations through", observations.Count);
                return observations;
            }

            // Attempt retrain if accumulator has grown enough
            tryRetrain();

            // Apply 4-stage pipeline to each observation. R9 — each stage is gated by
            // its own enable toggle so the classifier stages can be disabled (or run in
            // shadow mode for Stage 1) without silencing the whole service.
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

                // Stage 4: Anomaly score — when enabled, ALWAYS emits flag (score or NOMODEL)
                if (_settings.EnableStage4AnomalyScoring)
                    applyAnomalyScore(obs);

                // Confidence provenance: summarize highest-confidence ML correction applied
                var correctionLabel = determineMlCorrectionLabel(preMlFlags, obs.ValidationFlags);
                appendFlag(obs, $"CONFIDENCE:ML:{obs.ParseConfidence ?? 0:F2}:{correctionLabel}");
            }

            // Accumulate high-confidence rows for future training
            accumulateBatch(observations);

            // Post-accumulation rescore: the current batch's data is now in the accumulator,
            // which may push UNII-specific keys past the MinTrainingRowsPerCategory threshold.
            // Retrain once more and rescore any NOMODEL observations. With UNII-ordered
            // batching, adjacent documents share active ingredients, so most keys qualify
            // for training by the end of the first batch.
            if (_settings.EnableStage4AnomalyScoring)
            {
                var noModelObs = observations
                    .Where(o => o.ValidationFlags != null
                              && o.ValidationFlags.Contains("MLNET_ANOMALY_SCORE:NOMODEL"))
                    .ToList();

                if (noModelObs.Count > 0)
                {
                    tryRetrain();

                    var rescored = 0;
                    foreach (var obs in noModelObs)
                    {
                        stripAnomalyScoreFlag(obs);
                        applyAnomalyScore(obs);
                        if (obs.ValidationFlags != null
                            && !obs.ValidationFlags.Contains("MLNET_ANOMALY_SCORE:NOMODEL"))
                            rescored++;
                    }

                    _logger.LogInformation(
                        "Post-accumulation rescore: {Rescored}/{Total} NOMODEL observations now have scores",
                        rescored, noModelObs.Count);
                }

                // R9 — NOMODEL coverage diagnostic: log aggregate stats per batch so
                // operators can quantify how often UNII-specific keys lack models. The
                // 66% NOMODEL rate observed on the 2026-04-23 corpus recompute was
                // diagnosed via this kind of aggregate — logging it inline makes the
                // signal visible on every production run without requiring JSONL mining.
                logAnomalyCoverageDiagnostics(observations);
            }

            return observations;

            #endregion
        }

        #endregion IMlNetCorrectionService Implementation

        #region Stage 1 — TableCategory Validation

        /**************************************************************/
        /// <summary>
        /// Stage 1: Validates and optionally corrects TableCategory using the multiclass classifier.
        /// Only overrides when the model's max score exceeds <see cref="MlNetCorrectionSettings.TableCategoryMinConfidence"/>
        /// and the predicted category differs from the current one.
        /// </summary>
        /// <remarks>
        /// ## R9 — Default-off + shadow mode
        /// When <see cref="MlNetCorrectionSettings.EnableStage1TableCategoryCorrection"/>
        /// is false (the R9 default), the classifier does NOT mutate
        /// <see cref="ParsedObservation.TableCategory"/> and does NOT emit a
        /// <c>MLNET:CATEGORY_CORRECTED</c> flag. If
        /// <see cref="MlNetCorrectionSettings.EnableStage1ShadowMode"/> is also true
        /// (the default), the same prediction pipeline runs and emits a
        /// <c>MLNET:CATEGORY_SHADOW:{label}:{score}</c> flag when the prediction WOULD
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
                        appendFlag(obs, $"MLNET:CATEGORY_CORRECTED:{prediction.PredictedLabel}:{maxScore:F2}");
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
                        appendFlag(obs, $"MLNET:CATEGORY_SHADOW:{prediction.PredictedLabel}:{maxScore:F2}");
                        _logger.LogDebug("Stage 1 [SHADOW]: would have corrected '{Old}' → '{New}' (score={Score:F2})",
                            obs.TableCategory, prediction.PredictedLabel, maxScore);
                    },
                    stageNumber: 1);
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
        /// <param name="obs">Observation to evaluate.</param>
        private void applyDoseRegimenRouting(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrEmpty(obs.DoseRegimen))
                return;

            // Skip if already routed by ColumnStandardizationService rules
            if (DoseRegimenRoutingPolicy.IsAlreadyRouted(obs.ValidationFlags))
                return;

            var input = new DoseRegimenRoutingInput
            {
                DoseRegimen = obs.DoseRegimen ?? string.Empty,
                TableCategory = obs.TableCategory ?? string.Empty,
                Caption = obs.Caption ?? string.Empty,
                ParameterName = obs.ParameterName ?? string.Empty,
                HasDose = obs.Dose.HasValue ? 1f : 0f
            };

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
                    appendFlag(obs, $"MLNET:DOSEREGIMEN_ROUTED_TO_{prediction.PredictedLabel!.ToUpperInvariant()}:{maxScore:F2}");
                    _logger.LogDebug("Stage 2: DoseRegimen routed to {Target} (score={Score:F2})",
                        prediction.PredictedLabel, maxScore);
                },
                stageNumber: 2);

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
                    appendFlag(obs, $"MLNET:PVTYPE_DISAMBIGUATED:{prediction.PredictedLabel}:{maxScore:F2}");
                    _logger.LogDebug("Stage 3: PrimaryValueType disambiguated '{Old}' → '{New}' (score={Score:F2})",
                        oldType, prediction.PredictedLabel, maxScore);
                },
                stageNumber: 3);

            #endregion
        }

        #endregion Stage 3

        #region Stage 4 — Anomaly Detection

        /**************************************************************/
        /// <summary>
        /// Stage 4: Computes an anomaly score for the observation using the per-category PCA model.
        /// Always emits a flag: <c>MLNET_ANOMALY_SCORE:{score:F4}</c> or <c>MLNET_ANOMALY_SCORE:NOMODEL</c>.
        /// </summary>
        /// <param name="obs">Observation to score.</param>
        private void applyAnomalyScore(ParsedObservation obs)
        {
            #region implementation

            // Look up UNII-specific model. If no model exists for this key, emit NOMODEL
            // (which forwards the observation to Claude as the safe default). Generic
            // cross-UNII models were intentionally removed — they mix different drugs'
            // distributions, producing scores whose meaning differs from UNII-specific
            // scores yet are consumed identically by the Claude threshold gate.
            var compositeKey = buildAnomalyModelKey(obs.UNII, obs.TableCategory, obs.PrimaryValueType, obs.SecondaryValueType);
            if (!_anomalyEngines.TryGetValue(compositeKey, out var engine))
            {
                appendFlag(obs, "MLNET_ANOMALY_SCORE:NOMODEL");
                return;
            }

            // All 7 columns are populated; the prediction engine's baked-in Concatenate
            // step reads only the columns that had real variance at training time.
            var input = new AnomalyFeatureRow
            {
                PrimaryValue = MlTrainingRecord.toSafeFloat(obs.PrimaryValue),
                SecondaryValue = MlTrainingRecord.toSafeFloat(obs.SecondaryValue),
                LowerBound = MlTrainingRecord.toSafeFloat(obs.LowerBound),
                UpperBound = MlTrainingRecord.toSafeFloat(obs.UpperBound),
                PValue = MlTrainingRecord.toSafeFloat(obs.PValue),
                ParseConfidence = MlTrainingRecord.toSafeFloat(obs.ParseConfidence),
                LogArmN = obs.ArmN.HasValue ? (float)Math.Log(obs.ArmN.Value + 1) : 0f
            };

            try
            {
                var prediction = engine.Predict(input);
                var score = prediction.Score;

                if (float.IsNaN(score) || float.IsInfinity(score))
                {
                    appendFlag(obs, "MLNET_ANOMALY_SCORE:ERROR");
                }
                else
                {
                    appendFlag(obs, $"MLNET_ANOMALY_SCORE:{score:F4}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Stage 4 anomaly prediction failed for SourceRowSeq={Row}", obs.SourceRowSeq);
                appendFlag(obs, "MLNET_ANOMALY_SCORE:ERROR");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R9 diagnostic — after a batch's anomaly scoring + post-accumulation rescore,
        /// logs aggregate coverage stats so operators can quantify how often UNII-specific
        /// models were missing. The 66% NOMODEL rate observed on the 2026-04-23 corpus
        /// recompute was first identified via JSONL mining; logging it inline surfaces
        /// the signal on every production run without requiring offline analysis.
        /// </summary>
        /// <remarks>
        /// ## Emitted fields (log message)
        /// - Total observations scored in the batch.
        /// - Count with real scores (excludes NOMODEL and ERROR sentinels).
        /// - Count with NOMODEL.
        /// - Count with ERROR.
        /// - Distinct UNII-specific model keys present in the batch.
        /// - Distinct keys that had a trained anomaly engine.
        /// - Coverage ratio (keys-with-models / total-keys).
        /// </remarks>
        /// <param name="observations">Observations from the current batch.</param>
        private void logAnomalyCoverageDiagnostics(List<ParsedObservation> observations)
        {
            #region implementation

            if (observations.Count == 0)
                return;

            var total = observations.Count;
            var noModelCount = 0;
            var errorCount = 0;
            var scoredCount = 0;
            var distinctKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var obs in observations)
            {
                var flags = obs.ValidationFlags ?? string.Empty;
                if (flags.Contains("MLNET_ANOMALY_SCORE:NOMODEL"))
                    noModelCount++;
                else if (flags.Contains("MLNET_ANOMALY_SCORE:ERROR"))
                    errorCount++;
                else if (flags.Contains("MLNET_ANOMALY_SCORE:"))
                    scoredCount++;

                distinctKeys.Add(buildAnomalyModelKey(obs.UNII, obs.TableCategory, obs.PrimaryValueType, obs.SecondaryValueType));
            }

            var keysWithModels = distinctKeys.Count(k => _anomalyEngines.ContainsKey(k));
            var keyCoverage = distinctKeys.Count > 0
                ? (double)keysWithModels / distinctKeys.Count
                : 0.0;

            _logger.LogInformation(
                "R9 anomaly coverage — batch={Total}: scored={Scored}, NOMODEL={NoModel}, ERROR={Errors}; " +
                "distinct keys={Keys}, with models={WithModels} ({Coverage:P1})",
                total, scoredCount, noModelCount, errorCount,
                distinctKeys.Count, keysWithModels, keyCoverage);

            #endregion
        }

        #endregion Stage 4

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

            // Check if any UNII-specific key qualifies for training.
            // With UNII-ordered batching, adjacent documents share active ingredients,
            // so keys reach MinTrainingRowsPerCategory within the first batch.
            var hasQualified = _trainingAccumulator
                .GroupBy(r => buildAnomalyModelKey(r.UNII, r.TableCategory, r.PrimaryValueType, r.SecondaryValueType),
                         StringComparer.OrdinalIgnoreCase)
                .Any(g => g.Count() >= _settings.MinTrainingRowsPerCategory);

            if (!hasQualified)
                return;

            _logger.LogInformation(
                "ML retrain triggered: {NewRows} new rows since last train",
                newRows);

            trainTableCategoryModel(_trainingAccumulator);
            trainDoseRegimenModel(_trainingAccumulator);
            trainPrimaryValueTypeModel(_trainingAccumulator);
            trainAnomalyModels(_trainingAccumulator);

            _accumulatorSizeAtLastTrain = _trainingAccumulator.Count;

            if (_trainingStore != null)
            {
                // Record retrain timestamp and persist state
                _ = _trainingStore.RecordRetrainAsync();
            }

            _logger.LogInformation(
                "ML retrain complete. Accumulator size: {Size}. Anomaly models: {ModelCount}",
                _trainingAccumulator.Count, _anomalyEngines.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Collects high-confidence rows from the batch into the training accumulator as
        /// <see cref="MlTrainingRecord"/> instances (bootstrap, not ground truth).
        /// Called at the end of each <see cref="ScoreAndCorrect"/> invocation.
        /// </summary>
        /// <param name="observations">Observations from the current batch.</param>
        private void accumulateBatch(IEnumerable<ParsedObservation> observations)
        {
            #region implementation

            var newRecords = new List<MlTrainingRecord>();

            foreach (var obs in observations)
            {
                if (obs.ParseConfidence >= _settings.BootstrapMinParseConfidence)
                {
                    newRecords.Add(MlTrainingRecord.FromObservation(obs, isGroundTruth: false));
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
        /// <see cref="MlNetCorrectionSettings.MaxAccumulatorRows"/>, shifting the retrain cursor
        /// so the gate delta in <see cref="tryRetrain"/> stays correct after oldest-first trim.
        /// </summary>
        /// <remarks>
        /// Oldest-first trim is sufficient because retrains happen frequently and recent rows
        /// dominate, so per-key categorical coverage is preserved. The retrain gate uses
        /// <c>_trainingAccumulator.Count - _accumulatorSizeAtLastTrain</c> as an absolute cursor
        /// into the list; removing N records from the front shrinks Count by N, so the cursor is
        /// shifted back by N to preserve the "new rows since last retrain" delta. Without this,
        /// the gate would become permanently false once the accumulator hit its cap, no further
        /// retrains would ever fire, and every UNII first seen after that point would receive
        /// NOMODEL. The method is a no-op when <paramref name="records"/> is empty.
        /// </remarks>
        /// <param name="records">Records to append.</param>
        private void appendAndCapAccumulator(List<MlTrainingRecord> records)
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
        /// Caller supplies the training-DTO projection, the label accessor used for the distinct
        /// guard, the pipeline build-and-fit step, and the engine replacement callback.
        /// </summary>
        /// <remarks>
        /// Shared scaffolding around the three per-stage trainers
        /// (<see cref="trainTableCategoryModel"/>, <see cref="trainDoseRegimenModel"/>,
        /// <see cref="trainPrimaryValueTypeModel"/>). The caller controls everything that varies
        /// per stage — featurizers, trainer choice (SDCA vs LBFGS), which engine to dispose/assign —
        /// while this helper owns the common skeleton: try/catch, distinct-label guard, and
        /// success/skip/failure log messages shaped by <paramref name="stagePrefix"/>,
        /// <paramref name="skipLabelKind"/>, and <paramref name="modelKind"/>. On training
        /// failure (<see cref="InvalidOperationException"/> or <see cref="ArgumentOutOfRangeException"/>),
        /// <paramref name="replaceEngine"/> is invoked with <c>null</c> so the caller nulls its
        /// engine field without disposing the outgoing instance — matching the prior per-method behavior.
        /// </remarks>
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
            List<MlTrainingRecord> rows,
            Func<IEnumerable<MlTrainingRecord>, IEnumerable<TInput>> project,
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
        private void trainTableCategoryModel(List<MlTrainingRecord> rows)
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
                        // Release native ML.NET buffers on the outgoing engine before replacing it —
                        // PredictionEngine<T,U> is IDisposable and would otherwise live until GC finalization.
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
        private void trainDoseRegimenModel(List<MlTrainingRecord> rows)
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
        private void trainPrimaryValueTypeModel(List<MlTrainingRecord> rows)
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

        /**************************************************************/
        /// <summary>
        /// Stage 4 training: Per-composite-key PCA anomaly detection models.
        /// Each unique combination of UNII, TableCategory, PrimaryValueType, and (when defined)
        /// SecondaryValueType gets its own model trained on only the features that have real
        /// variance for that key. Constant-zero features are excluded via a dynamic
        /// <c>Concatenate("Features", activeColumnNames)</c> pipeline step, eliminating the
        /// need for jitter and preventing noise dimensions from dominating scores.
        /// </summary>
        /// <param name="rows">Training data from accumulator.</param>
        /// <seealso cref="buildAnomalyModelKey"/>
        /// <seealso cref="applyAnomalyScore"/>
        /// <seealso cref="computeActiveFeatureIndices"/>
        /// <seealso cref="_featureColumnNames"/>
        private void trainAnomalyModels(List<MlTrainingRecord> rows)
        {
            #region implementation

            // Release native ML.NET buffers on outgoing engines before wiping the dictionary —
            // each entry holds a PredictionEngine<AnomalyFeatureRow, AnomalyPrediction> which is
            // IDisposable and would otherwise live until GC finalization.
            foreach (var engine in _anomalyEngines.Values)
            {
                (engine as IDisposable)?.Dispose();
            }
            _anomalyEngines.Clear();

            // Group by composite key discovered from the data
            var groups = rows
                .Where(r => !float.IsNaN(r.PrimaryValue) &&
                             !float.IsNaN(r.SecondaryValue) &&
                             !float.IsNaN(r.LowerBound) &&
                             !float.IsNaN(r.UpperBound) &&
                             !float.IsNaN(r.PValue) &&
                             !float.IsNaN(r.ParseConfidence) &&
                             !float.IsNaN(r.LogArmN))
                .GroupBy(r => buildAnomalyModelKey(r.UNII, r.TableCategory, r.PrimaryValueType, r.SecondaryValueType),
                         StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var compositeKey = group.Key;
                try
                {
                    // Build raw feature vectors for active-feature detection
                    var groupList = group.ToList();
                    var rawVectors = groupList
                        .Select(r => new float[]
                        {
                            r.PrimaryValue,
                            r.SecondaryValue,
                            r.LowerBound,
                            r.UpperBound,
                            r.PValue,
                            r.ParseConfidence,
                            r.LogArmN
                        })
                        .ToList();

                    if (rawVectors.Count < _settings.MinTrainingRowsPerCategory)
                    {
                        _logger.LogDebug("Stage 4 skipping {CompositeKey} — only {Count} rows (need {Min})",
                            compositeKey, rawVectors.Count, _settings.MinTrainingRowsPerCategory);
                        continue;
                    }

                    // Identify which feature slots have real variance vs constant columns.
                    // Constant columns are excluded from the Concatenate step entirely —
                    // no jitter needed because NormalizeMeanVariance never sees them.
                    var activeIndices = computeActiveFeatureIndices(rawVectors);
                    if (activeIndices.Length == 0)
                    {
                        _logger.LogDebug("Stage 4 skipping {CompositeKey} — no feature variance in {Count} rows",
                            compositeKey, rawVectors.Count);
                        continue;
                    }

                    // Map active indices to column names for dynamic Concatenate
                    var activeColumnNames = activeIndices
                        .Select(i => _featureColumnNames[i])
                        .ToArray();

                    // Build AnomalyFeatureRow list for ML.NET — all 7 columns populated,
                    // but the pipeline will only read the active ones
                    var featureRows = groupList
                        .Select(r => new AnomalyFeatureRow
                        {
                            PrimaryValue = r.PrimaryValue,
                            SecondaryValue = r.SecondaryValue,
                            LowerBound = r.LowerBound,
                            UpperBound = r.UpperBound,
                            PValue = r.PValue,
                            ParseConfidence = r.ParseConfidence,
                            LogArmN = r.LogArmN
                        })
                        .ToList();

                    var dataView = _mlContext.Data.LoadFromEnumerable(featureRows);

                    // PCA rank: composite key → category fallback → default 3
                    // Key format: "UNII|Category|PVT[|SVT]" — category is second segment
                    var segments = compositeKey.Split('|');
                    var category = segments.Length >= 2 ? segments[1] : compositeKey;
                    var rank = _pcaRanks.TryGetValue(compositeKey, out var r1) ? r1
                             : _pcaRanks.TryGetValue(category, out var r2) ? r2
                             : 3;
                    // Clamp rank to number of active features — only dimensions with
                    // real variance participate in PCA
                    rank = Math.Min(rank, activeIndices.Length);

                    // Pipeline: Concatenate only active columns → normalize → PCA
                    var pipeline = _mlContext.Transforms.Concatenate("Features", activeColumnNames)
                        .Append(_mlContext.Transforms.NormalizeMeanVariance("Features"))
                        .Append(_mlContext.AnomalyDetection.Trainers.RandomizedPca(
                            featureColumnName: "Features",
                            rank: rank));

                    var model = pipeline.Fit(dataView);
                    _anomalyEngines[compositeKey] = _mlContext.Model
                        .CreatePredictionEngine<AnomalyFeatureRow, AnomalyPrediction>(model);

                    _logger.LogDebug(
                        "Stage 4 anomaly model trained for {CompositeKey}: {Count} rows, rank={Rank}, activeFeatures=[{ActiveNames}]",
                        compositeKey, rawVectors.Count, rank, string.Join(", ", activeColumnNames));
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentOutOfRangeException)
                {
                    // Swallowed — intermittent PCA edge case. RandomizedPca can produce NaN eigenvectors
                    // on rank-deficient / collinear training subsets (varies with batch size, not reliably
                    // reproducible). Graceful degradation: the key stays out of _anomalyEngines and its
                    // rows receive NOMODEL. Downgraded from Warning to Debug so it does not pollute the
                    // default output window; still captured under verbose logging.
                    _logger.LogDebug(ex, "Stage 4 anomaly model training failed for {CompositeKey} (swallowed)", compositeKey);
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the indices of feature dimensions that have non-negligible variance.
        /// </summary>
        /// <remarks>
        /// Constant columns (variance ≈ 0) are excluded from the dynamic
        /// <c>Concatenate("Features", ...)</c> step so that <c>NormalizeMeanVariance</c>
        /// never sees zero-variance dimensions. This replaces the earlier jitter-based
        /// approach, which amplified noise to unit scale and corrupted PCA eigenvectors.
        /// </remarks>
        /// <param name="rows">Raw feature vectors (one <c>float[]</c> per training row).</param>
        /// <param name="epsilon">Minimum variance threshold for a dimension to be considered active. Default: 1e-6.</param>
        /// <returns>Array of feature indices with variance greater than <paramref name="epsilon"/>.</returns>
        /// <seealso cref="trainAnomalyModels"/>
        /// <seealso cref="_featureColumnNames"/>
        private static int[] computeActiveFeatureIndices(List<float[]> rows, float epsilon = 1e-6f)
        {
            #region implementation

            if (rows.Count == 0)
                return Array.Empty<int>();

            int featureCount = rows[0].Length;
            var active = new List<int>();

            for (int f = 0; f < featureCount; f++)
            {
                // Compute mean
                float mean = 0f;
                foreach (var row in rows)
                    mean += row[f];
                mean /= rows.Count;

                // Compute variance
                float variance = 0f;
                foreach (var row in rows)
                {
                    float delta = row[f] - mean;
                    variance += delta * delta;
                }
                variance /= rows.Count;

                if (variance > epsilon)
                    active.Add(f);
            }

            return active.ToArray();

            #endregion
        }

        // /**************************************************************/
        // /// <summary>
        // /// Converts a raw PCA reconstruction error to a percentile (0.0–1.0) using the
        // /// per-category calibration array built during training. Uses binary search for
        // /// O(log n) lookup.
        // /// </summary>
        // /// <remarks>
        // /// ## Percentile Semantics
        // /// - 0.0 = score at or below the minimum seen during training (least anomalous)
        // /// - 1.0 = score above the maximum seen during training (most anomalous)
        // /// - 0.75 = 75th percentile — 75% of training rows scored lower
        // ///
        // /// ## Edge Cases
        // /// - Empty or missing calibration array → returns raw score unchanged (cold-start fallback)
        // /// - Score below calibration minimum → returns 0.0 (less anomalous than anything in training)
        // /// - Score above calibration maximum → returns 1.0 (more anomalous than anything in training)
        // /// - Exact match with duplicates → walks forward to last duplicate, uses inclusive ranking
        // /// - Between two values → insertion point / count
        // /// </remarks>
        // /// <param name="category">TableCategory for calibration lookup.</param>
        // /// <param name="rawScore">Raw PCA reconstruction error score.</param>
        // /// <returns>Percentile in [0.0, 1.0], or raw score if no calibration is available.</returns>
        // /// <seealso cref="trainAnomalyModels"/>
        // /// <seealso cref="applyAnomalyScore"/>
        // private float calibrateScore(string category, float rawScore)
        // {
        //     #region implementation
        //
        //     if (!_anomalyCalibration.TryGetValue(category, out var sorted) || sorted.Length == 0)
        //         return rawScore; // Cold-start fallback: no calibration data
        //
        //     int index = Array.BinarySearch(sorted, rawScore);
        //
        //     if (index >= 0)
        //     {
        //         // Exact match found. Walk forward past any duplicates to get the
        //         // inclusive count (all values <= rawScore).
        //         while (index < sorted.Length - 1 && sorted[index + 1] == rawScore)
        //             index++;
        //         return (index + 1) / (float)sorted.Length;
        //     }
        //     else
        //     {
        //         // Not found: ~index = insertion point (count of elements < rawScore)
        //         int insertionPoint = ~index;
        //
        //         if (insertionPoint == 0)
        //             return 0f; // Below all training scores
        //
        //         if (insertionPoint >= sorted.Length)
        //             return 1f; // Above all training scores
        //
        //         return insertionPoint / (float)sorted.Length;
        //     }
        //
        //     #endregion
        // }

        #endregion Model Training Helpers

        #region Label Synthesis Helpers

        /**************************************************************/
        /// <summary>
        /// Checks whether a <see cref="MlTrainingRecord"/> has any DoseRegimen routing flag
        /// from Stage 3.25 stored in its <see cref="MlTrainingRecord.ValidationFlags"/>.
        /// Delegates to <see cref="DoseRegimenRoutingPolicy.HasRoutingFlag"/>.
        /// </summary>
        private static bool hasRoutingFlagOnRecord(MlTrainingRecord record)
        {
            #region implementation

            return DoseRegimenRoutingPolicy.HasRoutingFlag(record.ValidationFlags);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Synthesizes a DoseRegimen routing label from a <see cref="MlTrainingRecord"/>
        /// for Stage 2 training. When the row has already been routed by rules, infers the
        /// label from its routing flags; otherwise applies the ML-tuned regex decision tree.
        /// </summary>
        /// <param name="record">Training record to label.</param>
        /// <returns>Routing target label or null.</returns>
        private static string? labelDoseRegimenRoutingFromRecord(MlTrainingRecord record)
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

            var correctedCount = corrected.Count;
            var totalCount = observations.Count;

            // Convert corrected observations to ground-truth training records
            var records = corrected
                .Select(o => MlTrainingRecord.FromObservation(o, isGroundTruth: true))
                .ToList();

            if (_trainingStore != null)
            {
                if (records.Count > 0)
                {
                    await _trainingStore.AddRecordsAsync(records, ct);
                }

                var newThreshold = await _trainingStore.RecordClaudeFeedbackAsync(totalCount, correctedCount, ct);

                if (newThreshold != null && _claudeSettings != null)
                {
                    // Honor the configured floor on the runtime ratchet. The store's raw
                    // adaptive value may still be climbing from its 0.0f default and could
                    // otherwise demote a user-configured floor (e.g. 0.75).
                    var effectiveThreshold = clampAndApplyAnomalyThreshold(newThreshold.Value);
                    _logger.LogInformation(
                        "Adaptive threshold updated: effective={Effective:F4} " +
                        "(floor={Floor:F4}, ratcheted={Ratcheted:F4})",
                        effectiveThreshold, _configuredAnomalyFloor, newThreshold.Value);
                }
            }
            else
            {
                // Ephemeral mode — accumulate in memory with cap enforcement.
                appendAndCapAccumulator(records);
            }

            if (correctedCount > 0)
            {
                _logger.LogDebug(
                    "Fed {Corrected}/{Total} Claude-corrected observations as ground truth",
                    correctedCount, totalCount);
            }

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
        /// <remarks>
        /// Shared scaffolding around <see cref="applyTableCategoryCorrection"/>,
        /// <see cref="applyDoseRegimenRouting"/>, and <see cref="applyPrimaryValueTypeDisambiguation"/>.
        /// Stage-specific preconditions (e.g. "skip if already routed", "skip unless PrimaryValueType
        /// is Numeric") remain at the call site — the helper owns only the common Predict-and-gate
        /// skeleton. The threshold gate is
        /// <c>maxScore &gt;= minConfidence &amp;&amp; !OrdinalIgnoreCase.Equals(predictedLabel, noOpLabel)</c> —
        /// same shape as the three pre-existing methods. Exceptions from
        /// <c>PredictionEngine.Predict</c> (which can wrap schema or feature-shape errors) are caught
        /// and logged at Debug level with the observation's <see cref="ParsedObservation.SourceRowSeq"/>
        /// for traceability; the observation is left unchanged.
        /// </remarks>
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
        /// Clamps a candidate anomaly threshold to <see cref="_configuredAnomalyFloor"/> and,
        /// when <see cref="_claudeSettings"/> is present, writes the effective value to
        /// <see cref="ClaudeApiCorrectionSettings.MlAnomalyScoreThreshold"/>. Returns the
        /// effective value regardless of whether the write occurred.
        /// </summary>
        /// <remarks>
        /// The store's adaptive threshold starts at 0.0f on a fresh install — writing it raw
        /// would silently demote a user-configured floor (e.g. 0.75) and disable the anomaly
        /// gate. Centralizing the floor clamp keeps the initialization path
        /// (<see cref="InitializeAsync"/>) and the runtime ratchet path
        /// (<see cref="FeedClaudeCorrectedBatchAsync"/>) in sync.
        /// </remarks>
        /// <param name="candidate">Raw candidate threshold (e.g. persisted or newly computed).</param>
        /// <returns>Effective threshold after clamping to the configured floor.</returns>
        private float clampAndApplyAnomalyThreshold(float candidate)
        {
            #region implementation

            var effective = Math.Max(_configuredAnomalyFloor, candidate);
            if (_claudeSettings != null)
            {
                _claudeSettings.MlAnomalyScoreThreshold = effective;
            }
            return effective;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the composite key for anomaly model lookup and storage.
        /// Format: "Category|PrimaryValueType" when SecondaryValueType is null/empty,
        /// or "Category|PrimaryValueType|SecondaryValueType" when defined.
        /// </summary>
        /// <param name="unii">Plus-delimited active ingredient UNIIs, or null.</param>
        /// <param name="tableCategory">Table category (e.g., "PK").</param>
        /// <param name="primaryValueType">Primary value type (e.g., "ArithmeticMean").</param>
        /// <param name="secondaryValueType">Secondary value type (e.g., "SD"), or null.</param>
        /// <returns>Composite key string for anomaly engine dictionary lookup.</returns>
        /// <seealso cref="trainAnomalyModels"/>
        /// <seealso cref="applyAnomalyScore"/>
        internal static string buildAnomalyModelKey(
            string? unii,
            string? tableCategory,
            string? primaryValueType,
            string? secondaryValueType = null)
        {
            #region implementation

            var u   = unii ?? string.Empty;
            var cat = tableCategory ?? string.Empty;
            var pvt = primaryValueType ?? string.Empty;
            var svt = secondaryValueType ?? string.Empty;

            return string.IsNullOrEmpty(svt)
                ? $"{u}|{cat}|{pvt}"
                : $"{u}|{cat}|{pvt}|{svt}";

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
        /// Strips all <c>MLNET_ANOMALY_SCORE:*</c> flags from the observation's ValidationFlags.
        /// Used by the post-accumulation rescore pass to clear NOMODEL before retrying.
        /// </summary>
        /// <param name="obs">Observation whose anomaly score flag should be removed.</param>
        /// <seealso cref="applyAnomalyScore"/>
        private static void stripAnomalyScoreFlag(ParsedObservation obs)
        {
            #region implementation

            if (string.IsNullOrEmpty(obs.ValidationFlags))
                return;

            var flags = obs.ValidationFlags
                .Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries)
                .Where(f => !f.StartsWith("MLNET_ANOMALY_SCORE:"))
                .ToArray();

            obs.ValidationFlags = flags.Length > 0 ? string.Join("; ", flags) : string.Empty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines the highest-confidence ML correction label by comparing flags before
        /// and after the 4-stage pipeline. Returns the first correction type found
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

            if (newPart.Contains("MLNET:CATEGORY_CORRECTED"))
                return "CATEGORY_CORRECTED";
            if (newPart.Contains("MLNET:DOSEREGIMEN_ROUTED"))
                return "DOSEREGIMEN_ROUTED";
            if (newPart.Contains("MLNET:PVTYPE_DISAMBIGUATED"))
                return "PVTYPE_DISAMBIGUATED";

            return "no_correction";

            #endregion
        }

        #endregion Helper Methods
    }
}
