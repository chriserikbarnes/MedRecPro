using MedRecProImportClass.Models;
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
        private readonly Dictionary<string, PredictionEngine<AnomalyInput, AnomalyPrediction>>
            _anomalyEngines = new(StringComparer.OrdinalIgnoreCase);

        /**************************************************************/
        /// <summary>Categories that get per-category anomaly detection models.</summary>
        private static readonly string[] _anomalyCategories =
        {
            "ADVERSE_EVENT", "PK", "EFFICACY", "DRUG_INTERACTION",
            "BMD", "DOSING", "TISSUE_DISTRIBUTION", "DEMOGRAPHIC", "LABORATORY"
        };

        /**************************************************************/
        /// <summary>Recommended PCA ranks per category (from architecture spec).</summary>
        private static readonly Dictionary<string, int> _pcaRanks = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ADVERSE_EVENT"] = 6,
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

        /**************************************************************/
        /// <summary>PK sub-parameter names for DoseRegimen routing label synthesis.</summary>
        private static readonly HashSet<string> _pkSubParams = new(StringComparer.OrdinalIgnoreCase)
        {
            "Cmax", "Cmin", "Tmax", "AUC", "AUC0-inf", "AUC0-t", "AUC0-24",
            "AUCinf", "AUCtau", "AUClast", "t1/2", "t½", "CL/F", "CL",
            "V/F", "Vss", "Vd", "ke", "MRT", "MAT", "bioavailability", "CV(%)"
        };

        /**************************************************************/
        /// <summary>PK sub-parameter prefix pattern for fuzzy matching.</summary>
        private static readonly Regex _pkSubParamPrefixPattern = new(
            @"^(AUC|Cmax|Cmin|Tmax|CL|Vd|Vss|MRT|MAT|t1/2|t½|ke)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

                if (_claudeSettings != null)
                {
                    _claudeSettings.MlAnomalyScoreThreshold = _trainingStore.GetAdaptiveThreshold();
                }

                _logger.LogInformation(
                    "Loaded {Count} training records from store. Adaptive threshold: {Threshold:F4}",
                    _trainingAccumulator.Count, _trainingStore.GetAdaptiveThreshold());
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
                ParameterName = obs.ParameterName ?? string.Empty
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

            if (_anomalyEngines.TryGetValue(obs.TableCategory ?? string.Empty, out var engine))
            {
                var input = new AnomalyInput
                {
                    Features = new float[]
                    {
                        MlTrainingRecord.toSafeFloat(obs.PrimaryValue),
                        MlTrainingRecord.toSafeFloat(obs.SecondaryValue),
                        MlTrainingRecord.toSafeFloat(obs.LowerBound),
                        MlTrainingRecord.toSafeFloat(obs.UpperBound),
                        MlTrainingRecord.toSafeFloat(obs.PValue),
                        MlTrainingRecord.toSafeFloat(obs.ParseConfidence)
                    }
                };

                try
                {
                    var prediction = engine.Predict(input);
                    appendFlag(obs, $"MLNET_ANOMALY_SCORE:{prediction.Score:F4}");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Stage 4 anomaly prediction failed for SourceRowSeq={Row}", obs.SourceRowSeq);
                    appendFlag(obs, "MLNET_ANOMALY_SCORE:ERROR");
                }
            }
            else
            {
                appendFlag(obs, "MLNET_ANOMALY_SCORE:NOMODEL");
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

            var qualifiedCategories = _trainingAccumulator
                .GroupBy(r => r.TableCategory)
                .Where(g => g.Count() >= _settings.MinTrainingRowsPerCategory)
                .ToList();

            if (qualifiedCategories.Count == 0)
                return;

            _logger.LogInformation(
                "ML retrain triggered: {NewRows} new rows, {Categories} qualified categories",
                newRows, qualifiedCategories.Count);

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

            _logger.LogInformation("ML retrain complete. Accumulator size: {Size}", _trainingAccumulator.Count);

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
                    .Append(_mlContext.Transforms.Concatenate("Features", "DoseFeatures", "CategoryFeatures", "CaptionFeatures", "ParamFeatures"))
                    .Append(_mlContext.MulticlassClassification.Trainers.LbfgsMaximumEntropy(labelColumnName: "Label", featureColumnName: "Features"))
                    .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

                var model = pipeline.Fit(dataView);
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
        /// Stage 4 training: Per-category PCA anomaly detection models.
        /// Each category gets its own model trained on 6 numeric features:
        /// PrimaryValue, SecondaryValue, LowerBound, UpperBound, PValue, ParseConfidence.
        /// </summary>
        /// <param name="rows">Training data from accumulator.</param>
        private void trainAnomalyModels(List<MlTrainingRecord> rows)
        {
            #region implementation

            _anomalyEngines.Clear();

            foreach (var cat in _anomalyCategories)
            {
                try
                {
                    var catRows = rows
                        .Where(r => string.Equals(r.TableCategory, cat, StringComparison.OrdinalIgnoreCase) &&
                                    r.PrimaryValue != 0f &&
                                    !float.IsNaN(r.PrimaryValue) &&
                                    !float.IsNaN(r.SecondaryValue) &&
                                    !float.IsNaN(r.LowerBound) &&
                                    !float.IsNaN(r.UpperBound) &&
                                    !float.IsNaN(r.PValue) &&
                                    !float.IsNaN(r.ParseConfidence))
                        .Select(r => new AnomalyInput
                        {
                            Features = new float[]
                            {
                                r.PrimaryValue,
                                r.SecondaryValue,
                                r.LowerBound,
                                r.UpperBound,
                                r.PValue,
                                r.ParseConfidence
                            }
                        })
                        .ToList();

                    if (catRows.Count < _settings.MinTrainingRowsPerCategory)
                    {
                        _logger.LogDebug("Stage 4 skipping {Category} — only {Count} rows (need {Min})",
                            cat, catRows.Count, _settings.MinTrainingRowsPerCategory);
                        continue;
                    }

                    var dataView = _mlContext.Data.LoadFromEnumerable(catRows);

                    var rank = _pcaRanks.TryGetValue(cat, out var r) ? r : 3;
                    // Ensure rank doesn't exceed feature count (6)
                    rank = Math.Min(rank, 6);

                    var pipeline = _mlContext.Transforms.NormalizeMeanVariance("Features")
                        .Append(_mlContext.AnomalyDetection.Trainers.RandomizedPca(
                            featureColumnName: "Features",
                            rank: rank));

                    var model = pipeline.Fit(dataView);
                    _anomalyEngines[cat] = _mlContext.Model.CreatePredictionEngine<AnomalyInput, AnomalyPrediction>(model);

                    _logger.LogDebug("Stage 4 anomaly model trained for {Category}: {Count} rows, rank={Rank}",
                        cat, catRows.Count, rank);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentOutOfRangeException)
                {
                    _logger.LogWarning(ex, "Stage 4 anomaly model training failed for {Category}", cat);
                }
            }

            #endregion
        }

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
            if (_pkSubParams.Contains(val) || _pkSubParamPrefixPattern.IsMatch(val))
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
                    _claudeSettings.MlAnomalyScoreThreshold = newThreshold.Value;
                    _logger.LogInformation(
                        "Adaptive threshold raised to {Threshold:F4}",
                        newThreshold.Value);
                }
            }
            else
            {
                // Ephemeral mode — just add to in-memory accumulator
                _trainingAccumulator.AddRange(records);
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
