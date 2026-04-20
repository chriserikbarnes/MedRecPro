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
                var effectiveThreshold = Math.Max(_configuredAnomalyFloor, persistedAdaptive);

                if (_claudeSettings != null)
                {
                    _claudeSettings.MlAnomalyScoreThreshold = effectiveThreshold;
                }

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

            // Apply 4-stage pipeline to each observation
            foreach (var obs in observations)
            {
                var preMlFlags = obs.ValidationFlags;

                // Stage 1: TableCategory validation
                applyTableCategoryCorrection(obs);

                // Stage 2: DoseRegimen routing (skip if already routed by rules)
                applyDoseRegimenRouting(obs);

                // Stage 3: PrimaryValueType disambiguation (only if "Numeric")
                applyPrimaryValueTypeDisambiguation(obs);

                // Stage 4: Anomaly score — ALWAYS executes, ALWAYS emits flag
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
        /// <param name="obs">Observation to evaluate.</param>
        private void applyTableCategoryCorrection(ParsedObservation obs)
        {
            #region implementation

            if (_tableCategoryEngine == null)
                return;

            var input = new TableCategoryInput
            {
                Caption = obs.Caption ?? string.Empty,
                SectionTitle = obs.SectionTitle ?? string.Empty,
                ParentSectionCode = obs.ParentSectionCode ?? string.Empty,
                ParseRule = obs.ParseRule ?? string.Empty
            };

            try
            {
                var prediction = _tableCategoryEngine.Predict(input);
                var maxScore = prediction.Score?.Length > 0 ? prediction.Score.Max() : 0f;

                if (maxScore >= _settings.TableCategoryMinConfidence &&
                    !string.Equals(prediction.PredictedLabel, obs.TableCategory, StringComparison.OrdinalIgnoreCase))
                {
                    var oldCategory = obs.TableCategory;
                    obs.TableCategory = prediction.PredictedLabel;
                    appendFlag(obs, $"MLNET:CATEGORY_CORRECTED:{prediction.PredictedLabel}:{maxScore:F2}");
                    _logger.LogDebug("Stage 1: TableCategory corrected '{Old}' → '{New}' (score={Score:F2})",
                        oldCategory, prediction.PredictedLabel, maxScore);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Stage 1 prediction failed for SourceRowSeq={Row}", obs.SourceRowSeq);
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

            if (_doseRegimenEngine == null)
                return;

            if (string.IsNullOrEmpty(obs.DoseRegimen))
                return;

            // Skip if already routed by ColumnStandardizationService rules
            if (obs.ValidationFlags?.Contains("COL_STD:DOSEREGIMEN_ROUTED_TO") == true ||
                obs.ValidationFlags?.Contains("COL_STD:PK_SUBPARAM_ROUTED") == true ||
                obs.ValidationFlags?.Contains("COL_STD:COADMIN_ROUTED") == true ||
                obs.ValidationFlags?.Contains("COL_STD:POPULATION_EXTRACTED") == true ||
                obs.ValidationFlags?.Contains("COL_STD:TIMEPOINT_EXTRACTED") == true)
                return;

            var input = new DoseRegimenRoutingInput
            {
                DoseRegimen = obs.DoseRegimen ?? string.Empty,
                TableCategory = obs.TableCategory ?? string.Empty,
                Caption = obs.Caption ?? string.Empty,
                ParameterName = obs.ParameterName ?? string.Empty,
                HasDose = obs.Dose.HasValue ? 1f : 0f
            };

            try
            {
                var prediction = _doseRegimenEngine.Predict(input);
                var maxScore = prediction.Score?.Length > 0 ? prediction.Score.Max() : 0f;

                if (maxScore >= 0.80f && !string.Equals(prediction.PredictedLabel, "Keep", StringComparison.OrdinalIgnoreCase))
                {
                    routeDoseRegimen(obs, prediction.PredictedLabel!);
                    appendFlag(obs, $"MLNET:DOSEREGIMEN_ROUTED_TO_{prediction.PredictedLabel!.ToUpperInvariant()}:{maxScore:F2}");
                    _logger.LogDebug("Stage 2: DoseRegimen routed to {Target} (score={Score:F2})",
                        prediction.PredictedLabel, maxScore);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Stage 2 prediction failed for SourceRowSeq={Row}", obs.SourceRowSeq);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Routes DoseRegimen content to the specified target column, nulling the source.
        /// </summary>
        /// <param name="obs">Observation to modify.</param>
        /// <param name="target">Target column: "ParameterSubtype", "Population", or "Timepoint".</param>
        private static void routeDoseRegimen(ParsedObservation obs, string target)
        {
            #region implementation

            var value = obs.DoseRegimen;

            switch (target.ToLowerInvariant())
            {
                case "parametersubtype":
                    if (string.IsNullOrEmpty(obs.ParameterSubtype))
                        obs.ParameterSubtype = value;
                    break;
                case "population":
                    if (string.IsNullOrEmpty(obs.Population))
                        obs.Population = value;
                    break;
                case "timepoint":
                    if (string.IsNullOrEmpty(obs.Timepoint))
                        obs.Timepoint = value;
                    break;
            }

            obs.DoseRegimen = null;
            obs.Dose = null;
            obs.DoseUnit = null;

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

            if (_primaryValueTypeEngine == null)
                return;

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

            try
            {
                var prediction = _primaryValueTypeEngine.Predict(input);
                var maxScore = prediction.Score?.Length > 0 ? prediction.Score.Max() : 0f;

                if (maxScore >= 0.80f &&
                    !string.Equals(prediction.PredictedLabel, "Numeric", StringComparison.OrdinalIgnoreCase))
                {
                    var oldType = obs.PrimaryValueType;
                    obs.PrimaryValueType = prediction.PredictedLabel;
                    appendFlag(obs, $"MLNET:PVTYPE_DISAMBIGUATED:{prediction.PredictedLabel}:{maxScore:F2}");
                    _logger.LogDebug("Stage 3: PrimaryValueType disambiguated '{Old}' → '{New}' (score={Score:F2})",
                        oldType, prediction.PredictedLabel, maxScore);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Stage 3 prediction failed for SourceRowSeq={Row}", obs.SourceRowSeq);
            }

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
                _trainingAccumulator.AddRange(newRecords);

                // Enforce MaxAccumulatorRows on the in-memory copy so it stays bounded in
                // parallel with the persistent store (which already caps via evictIfOverCapacity).
                // Oldest-first trim is sufficient here — retrains happen frequently and recent
                // rows dominate, so per-key categorical coverage is preserved.
                var overflow = _trainingAccumulator.Count - _settings.MaxAccumulatorRows;
                if (overflow > 0)
                {
                    _trainingAccumulator.RemoveRange(0, overflow);

                    // The retrain gate in tryRetrain() uses
                    //     newRows = _trainingAccumulator.Count - _accumulatorSizeAtLastTrain
                    // as an absolute cursor into the list. Removing N records from the front
                    // shrinks Count by N, so we must shift the cursor back by N to preserve
                    // the "new rows since last retrain" delta. Without this, the gate
                    // becomes permanently false once the accumulator hits its cap and no
                    // further retrains ever fire — every UNII first seen after that point
                    // would receive NOMODEL.
                    _accumulatorSizeAtLastTrain = Math.Max(0, _accumulatorSizeAtLastTrain - overflow);
                }

                if (_trainingStore != null)
                {
                    // Fire-and-forget save — the store handles thread safety
                    _ = _trainingStore.AddRecordsAsync(newRecords);
                }
            }

            #endregion
        }

        #endregion Training — Retrain Trigger and Accumulator

        #region Model Training Helpers

        /**************************************************************/
        /// <summary>
        /// Stage 1 training: Multiclass classifier for TableCategory prediction.
        /// Uses text featurization of Caption, SectionTitle, ParentSectionCode, and ParseRule.
        /// </summary>
        /// <param name="rows">Training data from accumulator.</param>
        private void trainTableCategoryModel(List<MlTrainingRecord> rows)
        {
            #region implementation

            try
            {
                var trainingData = rows
                    .Where(r => !string.IsNullOrEmpty(r.TableCategory))
                    .Select(r => new TableCategoryInput
                    {
                        Caption = r.Caption ?? string.Empty,
                        SectionTitle = r.SectionTitle ?? string.Empty,
                        ParentSectionCode = r.ParentSectionCode ?? string.Empty,
                        ParseRule = r.ParseRule ?? string.Empty,
                        TableCategory = r.TableCategory!
                    })
                    .ToList();

                // Guard: need at least 2 distinct labels
                if (trainingData.Select(d => d.TableCategory).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2)
                {
                    _logger.LogDebug("Stage 1 training skipped — fewer than 2 distinct TableCategory labels");
                    return;
                }

                var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

                var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(TableCategoryInput.TableCategory))
                    .Append(_mlContext.Transforms.Text.FeaturizeText("CaptionFeatures", nameof(TableCategoryInput.Caption)))
                    .Append(_mlContext.Transforms.Text.FeaturizeText("SectionFeatures", nameof(TableCategoryInput.SectionTitle)))
                    .Append(_mlContext.Transforms.Text.FeaturizeText("LoincFeatures", nameof(TableCategoryInput.ParentSectionCode)))
                    .Append(_mlContext.Transforms.Text.FeaturizeText("ParseRuleFeatures", nameof(TableCategoryInput.ParseRule)))
                    .Append(_mlContext.Transforms.Concatenate("Features", "CaptionFeatures", "SectionFeatures", "LoincFeatures", "ParseRuleFeatures"))
                    .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features"))
                    .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

                var model = pipeline.Fit(dataView);

                // Release native ML.NET buffers on the outgoing engine before replacing it —
                // PredictionEngine<T,U> is IDisposable and would otherwise live until GC finalization.
                (_tableCategoryEngine as IDisposable)?.Dispose();
                _tableCategoryEngine = _mlContext.Model.CreatePredictionEngine<TableCategoryInput, TableCategoryPrediction>(model);

                _logger.LogDebug("Stage 1 TableCategory model trained on {Count} rows", trainingData.Count);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentOutOfRangeException)
            {
                _logger.LogWarning(ex, "Stage 1 TableCategory model training failed — engine remains null");
                _tableCategoryEngine = null;
            }

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

            try
            {
                var trainingData = rows
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
                    .Where(r => r.RoutingTarget != null)
                    .ToList();

                // Guard: need at least 2 distinct labels
                if (trainingData.Select(d => d.RoutingTarget).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2)
                {
                    _logger.LogDebug("Stage 2 training skipped — fewer than 2 distinct routing labels");
                    return;
                }

                var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

                var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(DoseRegimenRoutingInput.RoutingTarget))
                    .Append(_mlContext.Transforms.Text.FeaturizeText("DoseFeatures", nameof(DoseRegimenRoutingInput.DoseRegimen)))
                    .Append(_mlContext.Transforms.Text.FeaturizeText("CategoryFeatures", nameof(DoseRegimenRoutingInput.TableCategory)))
                    .Append(_mlContext.Transforms.Text.FeaturizeText("CaptionFeatures", nameof(DoseRegimenRoutingInput.Caption)))
                    .Append(_mlContext.Transforms.Text.FeaturizeText("ParamFeatures", nameof(DoseRegimenRoutingInput.ParameterName)))
                    .Append(_mlContext.Transforms.Concatenate("Features", "DoseFeatures", "CategoryFeatures", "CaptionFeatures", "ParamFeatures", nameof(DoseRegimenRoutingInput.HasDose)))
                    .Append(_mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features"))
                    .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

                var model = pipeline.Fit(dataView);

                // Release native ML.NET buffers on the outgoing engine before replacing it.
                (_doseRegimenEngine as IDisposable)?.Dispose();
                _doseRegimenEngine = _mlContext.Model.CreatePredictionEngine<DoseRegimenRoutingInput, DoseRegimenRoutingPrediction>(model);

                _logger.LogDebug("Stage 2 DoseRegimen model trained on {Count} rows", trainingData.Count);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentOutOfRangeException)
            {
                _logger.LogWarning(ex, "Stage 2 DoseRegimen model training failed — engine remains null");
                _doseRegimenEngine = null;
            }

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

            try
            {
                var trainingData = rows
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
                    })
                    .ToList();

                // Guard: need at least 2 distinct labels
                if (trainingData.Select(d => d.PrimaryValueType).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2)
                {
                    _logger.LogDebug("Stage 3 training skipped — fewer than 2 distinct PrimaryValueType labels");
                    return;
                }

                var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

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

                var model = pipeline.Fit(dataView);

                // Release native ML.NET buffers on the outgoing engine before replacing it.
                (_primaryValueTypeEngine as IDisposable)?.Dispose();
                _primaryValueTypeEngine = _mlContext.Model.CreatePredictionEngine<PrimaryValueTypeInput, PrimaryValueTypePrediction>(model);

                _logger.LogDebug("Stage 3 PrimaryValueType model trained on {Count} rows", trainingData.Count);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentOutOfRangeException)
            {
                _logger.LogWarning(ex, "Stage 3 PrimaryValueType model training failed — engine remains null");
                _primaryValueTypeEngine = null;
            }

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
        /// </summary>
        private static bool hasRoutingFlagOnRecord(MlTrainingRecord record)
        {
            #region implementation

            return record.ValidationFlags != null &&
                   (record.ValidationFlags.Contains("COL_STD:PK_SUBPARAM_ROUTED") ||
                    record.ValidationFlags.Contains("COL_STD:COADMIN_ROUTED") ||
                    record.ValidationFlags.Contains("COL_STD:POPULATION_EXTRACTED") ||
                    record.ValidationFlags.Contains("COL_STD:TIMEPOINT_EXTRACTED"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Synthesizes a DoseRegimen routing label from a <see cref="MlTrainingRecord"/>
        /// for Stage 2 training. Same logic as <see cref="labelDoseRegimenRouting"/> but
        /// operates on the compact training record type.
        /// </summary>
        /// <param name="record">Training record to label.</param>
        /// <returns>Routing target label or null.</returns>
        private static string? labelDoseRegimenRoutingFromRecord(MlTrainingRecord record)
        {
            #region implementation

            // If DoseRegimen was already routed by rules, infer label from flags
            if (hasRoutingFlagOnRecord(record))
            {
                if (record.ValidationFlags!.Contains("COL_STD:PK_SUBPARAM_ROUTED") ||
                    record.ValidationFlags!.Contains("COL_STD:COADMIN_ROUTED"))
                    return "ParameterSubtype";
                if (record.ValidationFlags!.Contains("COL_STD:POPULATION_EXTRACTED"))
                    return "Population";
                if (record.ValidationFlags!.Contains("COL_STD:TIMEPOINT_EXTRACTED"))
                    return "Timepoint";
            }

            if (string.IsNullOrWhiteSpace(record.DoseRegimen))
                return null;

            var val = record.DoseRegimen.Trim();

            // Priority 1: PK sub-parameter → ParameterSubtype
            if (PkParameterDictionary.IsPkParameter(val) || PkParameterDictionary.StartsWithPk(val))
                return "ParameterSubtype";

            // Priority 2: Actual dose → Keep
            if (_actualDosePattern.IsMatch(val))
                return "Keep";

            // Priority 3: Population pattern
            if (_residualPopulationPattern.IsMatch(val))
                return "Population";

            // Priority 4: Timepoint pattern
            if (_residualTimepointPattern.IsMatch(val))
                return "Timepoint";

            // Default: Keep
            return "Keep";

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
                    var effectiveThreshold = Math.Max(_configuredAnomalyFloor, newThreshold.Value);
                    _claudeSettings.MlAnomalyScoreThreshold = effectiveThreshold;
                    _logger.LogInformation(
                        "Adaptive threshold updated: effective={Effective:F4} " +
                        "(floor={Floor:F4}, ratcheted={Ratcheted:F4})",
                        effectiveThreshold, _configuredAnomalyFloor, newThreshold.Value);
                }
            }
            else
            {
                // Ephemeral mode — just add to in-memory accumulator
                _trainingAccumulator.AddRange(records);

                // Enforce MaxAccumulatorRows so the in-memory copy stays bounded even when
                // there is no persistent store to fall back on. Oldest-first trim matches
                // the semantics used in accumulateBatch.
                var overflow = _trainingAccumulator.Count - _settings.MaxAccumulatorRows;
                if (overflow > 0)
                {
                    _trainingAccumulator.RemoveRange(0, overflow);

                    // Shift the retrain cursor back by the same amount — see accumulateBatch
                    // for the full explanation. Without this, tryRetrain would gate off
                    // permanently once the accumulator hits its cap.
                    _accumulatorSizeAtLastTrain = Math.Max(0, _accumulatorSizeAtLastTrain - overflow);
                }
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
        /// Appends a flag to the observation's ValidationFlags field.
        /// Follows the existing semicolon-delimited convention used by ColumnStandardizationService.
        /// </summary>
        /// <param name="obs">Observation to flag.</param>
        /// <param name="flag">Flag string to append.</param>
        private static void appendFlag(ParsedObservation obs, string flag)
        {
            #region implementation

            obs.ValidationFlags = string.IsNullOrEmpty(obs.ValidationFlags)
                ? flag
                : $"{obs.ValidationFlags}; {flag}";

            #endregion
        }

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
