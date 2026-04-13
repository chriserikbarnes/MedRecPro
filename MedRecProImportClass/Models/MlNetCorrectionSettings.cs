namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Configuration settings for the ML.NET correction service (Stage 3.4).
    /// Controls model training thresholds, anomaly scoring, and enablement of ML-based
    /// observation correction in the SPL Table Normalization pipeline.
    /// </summary>
    /// <remarks>
    /// The ML correction service runs after Stage 3.25 (ColumnStandardization) and before
    /// Stage 3.5 (ClaudeApiCorrection). It uses in-memory accumulated high-confidence rows
    /// to train classification and anomaly detection models, then applies corrections and
    /// scores to each batch. The anomaly score gates which rows are forwarded to Claude.
    ///
    /// ## Cold-Start Behavior
    /// Batch 1 always emits <c>MLNET_ANOMALY_SCORE:NOMODEL</c> because no training data
    /// exists yet. Models train once the accumulator reaches <see cref="MinTrainingRowsPerCategory"/>
    /// rows per category and <see cref="RetrainingBatchSize"/> total new rows.
    /// </remarks>
    /// <seealso cref="MedRecProImportClass.Service.TransformationServices.IMlNetCorrectionService"/>
    /// <seealso cref="ClaudeApiCorrectionSettings"/>
    public class MlNetCorrectionSettings
    {
        #region enablement properties

        /**************************************************************/
        /// <summary>
        /// Master enable/disable switch. When false, the ML correction service is a no-op
        /// and all observations pass through unmodified with no anomaly scores.
        /// </summary>
        public bool Enabled { get; set; } = true;

        #endregion

        #region classification threshold properties

        /**************************************************************/
        /// <summary>
        /// Minimum prediction confidence for Stage 1 TableCategory override.
        /// Only corrects the category when the ML model's max class score exceeds this threshold
        /// AND disagrees with the current category. Default 0.90 (high bar to avoid false corrections).
        /// </summary>
        public float TableCategoryMinConfidence { get; set; } = 0.90f;

        /**************************************************************/
        /// <summary>
        /// Anomaly score threshold used by <see cref="ClaudeApiCorrectionSettings"/> to gate
        /// which observations are forwarded to the Claude API. Observations with anomaly scores
        /// below this threshold are considered "normal" and skip the expensive AI correction pass.
        /// Default 0.75. Set to 0.0 for backward-compatible behavior (all observations pass).
        /// </summary>
        public float AnomalyScoreClaudeThreshold { get; set; } = 0.75f;

        #endregion

        #region training configuration properties

        /**************************************************************/
        /// <summary>
        /// Minimum <see cref="ParsedObservation.ParseConfidence"/> required for a row to be
        /// added to the in-memory training accumulator. Rows below this confidence are excluded
        /// from training data to avoid poisoning models with low-quality parse results.
        /// Default 0.85.
        /// </summary>
        public double BootstrapMinParseConfidence { get; set; } = 0.85;

        /**************************************************************/
        /// <summary>
        /// Minimum rows per TableCategory required before models can be trained.
        /// Categories with fewer accumulated rows than this threshold are skipped during training.
        /// Default 20.
        /// </summary>
        public int MinTrainingRowsPerCategory { get; set; } = 20;

        /**************************************************************/
        /// <summary>
        /// Number of new rows that must accumulate since last training before a retrain is triggered.
        /// Prevents retraining on every batch while ensuring models improve as more data arrives.
        /// Default 200.
        /// </summary>
        public int RetrainingBatchSize { get; set; } = 200;

        #endregion

        #region training store settings

        /**************************************************************/
        /// <summary>
        /// Path to the ML training store JSON file. When set, training records and adaptive
        /// threshold state persist across process restarts. Null = ephemeral (current behavior,
        /// accumulator is in-memory only and lost on restart).
        /// </summary>
        /// <remarks>
        /// The file is written atomically (tmp + rename) and loaded on service initialization.
        /// Example: <c>Resources/.medrecpro-ml-training-store.json</c>
        /// </remarks>
        public string? TrainingStoreFilePath { get; set; } = ".medrecpro-ml-training-store.json";

        /**************************************************************/
        /// <summary>
        /// Maximum records in the training accumulator. When exceeded, oldest non-ground-truth
        /// (bootstrap) records are evicted first, then oldest ground-truth records.
        /// Default 100,000. At ~800–900 bytes/record (indented JSON) ≈ 80–90 MB on disk;
        /// use <see cref="MaxTrainingStoreSizeBytes"/> to cap the actual file size.
        /// </summary>
        public int MaxAccumulatorRows { get; set; } = 100_000;

        /**************************************************************/
        /// <summary>
        /// Maximum serialized file size for the training store JSON, in bytes.
        /// When the file would exceed this limit, oldest bootstrap records are evicted first,
        /// then oldest ground-truth records, until the serialized output fits within the cap.
        /// Enforced on every save and also at load time (to trim files written by older builds).
        /// Default 31,457,280 (30 MB). With <c>WriteIndented = true</c> producing ~800–900 bytes
        /// per record, 30 MB ≈ ~35,000 records.
        /// </summary>
        public long MaxTrainingStoreSizeBytes { get; set; } = 50L * 1024 * 1024; // 50 MB

        #endregion

        #region adaptive threshold settings

        /**************************************************************/
        /// <summary>
        /// Minimum lifetime Claude observations before adaptive threshold evaluation starts.
        /// Prevents threshold changes based on insufficient data. Default 2,000.
        /// </summary>
        public int AdaptiveThresholdMinObservations { get; set; } = 2_000;

        /**************************************************************/
        /// <summary>
        /// Correction rate (corrected / sent) below which the threshold is raised.
        /// A low correction rate means ML is handling most cases correctly, so
        /// fewer rows need Claude review. Default 0.10 (10%).
        /// </summary>
        public float AdaptiveThresholdCorrectionRateFloor { get; set; } = 0.10f;

        /**************************************************************/
        /// <summary>
        /// Step size per threshold increase. Each time the threshold is raised,
        /// it increases by this amount. Default 0.05.
        /// </summary>
        public float AdaptiveThresholdStep { get; set; } = 0.05f;

        /**************************************************************/
        /// <summary>
        /// Hard ceiling on the adaptive threshold — prevents all rows from being gated away
        /// from Claude entirely. Default 0.95.
        /// </summary>
        public float AdaptiveThresholdCeiling { get; set; } = 0.95f;

        /**************************************************************/
        /// <summary>
        /// Minimum new Claude observations between threshold evaluations.
        /// Prevents excessive re-evaluation. Default 1,000.
        /// </summary>
        public int AdaptiveThresholdEvaluationInterval { get; set; } = 1_000;

        #endregion
    }
}
