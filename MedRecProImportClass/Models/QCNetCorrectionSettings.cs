namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Configuration settings for the ML.NET correction service (Stage 3.4).
    /// Controls model training thresholds and per-stage enablement of ML-based
    /// observation correction in the SPL Table Normalization pipeline.
    /// </summary>
    /// <remarks>
    /// The ML correction service runs after Stage 3.25 (ColumnStandardization) and before
    /// Stage 3.5 (ClaudeApiCorrection). It uses in-memory accumulated high-confidence rows
    /// to train three classification stages (TableCategory, DoseRegimen routing,
    /// PrimaryValueType disambiguation) and applies corrections to each batch. Downstream
    /// gating of Claude correction is driven by the deterministic parse-quality gate in
    /// <see cref="MedRecProImportClass.Service.TransformationServices.IParseQualityService"/>,
    /// not by ML.NET output.
    ///
    /// ## Stage 4 Retirement (2026-04-24)
    /// The former Stage 4 anomaly-scoring pipeline (PerKey + UnifiedGlobal PCA, adaptive
    /// threshold ratcheting) was retired because PCA reconstruction-error scores cluster
    /// in a narrow band regardless of training-set shape, making any fixed threshold a
    /// tuning exercise rather than a durable gate. Claude's actual job is correcting
    /// parse-alignment errors, not flagging value extremity — the deterministic parse-quality
    /// gate targets parse failures directly.
    /// </remarks>
    /// <seealso cref="MedRecProImportClass.Service.TransformationServices.IQCNetCorrectionService"/>
    /// <seealso cref="ClaudeApiCorrectionSettings"/>
    public class QCNetCorrectionSettings
    {
        #region enablement properties

        /**************************************************************/
        /// <summary>
        /// Master enable/disable switch. When false, the ML correction service is a no-op
        /// and all observations pass through unmodified.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// R9 — Per-stage toggle for Stage 1 TableCategory correction. When false, the
        /// Stage 1 classifier does NOT mutate <see cref="ParsedObservation.TableCategory"/>
        /// and does NOT emit <c>QC:CATEGORY_CORRECTED</c> flags. Introduced in Wave 3 R9
        /// to gate the classifier off until its training data can be audited — corpus
        /// validation on 2026-04-23 showed it was flipping correctly-routed rows in the
        /// wrong direction (e.g., HSV mutation tables corrected TO PK with 0.99 confidence;
        /// genuine PK rows corrected AWAY to ADVERSE_EVENT at 0.98).
        /// </summary>
        /// <remarks>
        /// Default <c>false</c> — Stage 1 is disabled by default pending model retraining.
        /// When paired with <see cref="EnableStage1ShadowMode"/>=true (the default), the
        /// classifier still runs in prediction-only mode and emits
        /// <c>QC:CATEGORY_SHADOW:{label}:{score}</c> flags so the would-be corrections
        /// can be audited without affecting routing.
        /// </remarks>
        /// <seealso cref="EnableStage1ShadowMode"/>
        public bool EnableStage1TableCategoryCorrection { get; set; } = false;

        /**************************************************************/
        /// <summary>
        /// R9 — When Stage 1 correction is disabled via
        /// <see cref="EnableStage1TableCategoryCorrection"/>=false, the classifier still
        /// runs in shadow mode and emits <c>QC:CATEGORY_SHADOW:{label}:{score}</c>
        /// flags when its prediction would have triggered a correction (same
        /// confidence + label-differs gates). <c>TableCategory</c> is never mutated.
        /// </summary>
        /// <remarks>
        /// Default <c>true</c>. Setting both toggles to <c>false</c> silences Stage 1
        /// entirely. Setting <c>EnableStage1TableCategoryCorrection</c>=true makes shadow
        /// mode moot — the regular correction path runs instead.
        /// </remarks>
        /// <seealso cref="EnableStage1TableCategoryCorrection"/>
        public bool EnableStage1ShadowMode { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// PR #6 infrastructure — when <c>true</c> AND both
        /// <see cref="EnableStage1TableCategoryCorrection"/> and
        /// <see cref="EnableStage1ShadowMode"/> are <c>true</c>, the Stage 1 classifier
        /// emits <c>QC:CATEGORY_SHADOW:{label}:{score}</c> even when the prediction
        /// AGREES with the current <c>TableCategory</c>. Under the normal shadow rules the
        /// flag fires only on disagreement; this dual-write mode is an audit aid so the
        /// first production runs after Stage 1 is re-enabled can be fully reconstructed —
        /// every row records what the model saw, not just the surprising cases.
        /// </summary>
        /// <remarks>
        /// Default <c>false</c> — the existing disagreement-only shadow behaviour is
        /// preserved. Flip to <c>true</c> temporarily after re-enabling the classifier
        /// so the bulk of predictions are auditable end-to-end, then flip off once the
        /// audit passes.
        /// </remarks>
        public bool EnableStage1DualWriteAudit { get; set; } = false;

        /**************************************************************/
        /// <summary>
        /// R9 — Per-stage master toggle for Stage 2 DoseRegimen routing. Default <c>true</c>.
        /// When <c>false</c>, the stage is skipped entirely and neither correction nor shadow
        /// emission runs. For finer-grained control, see
        /// <see cref="EnableStage2DoseRegimenRoutingCorrection"/> and
        /// <see cref="EnableStage2ShadowMode"/>.
        /// </summary>
        public bool EnableStage2DoseRegimenRouting { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// PR #4 — When <see cref="EnableStage2DoseRegimenRouting"/> is true, controls
        /// whether Stage 2 actually mutates the observation (<c>true</c>, the default — current
        /// behaviour) or runs in prediction-only shadow mode (<c>false</c>). The shadow mode
        /// exists to audit Stage 2's decisions — on the 2026-04-24 corpus, Stage 2 was routing
        /// semantically diverse content (dose values, food status, metabolite names) into
        /// <c>ParameterSubtype</c>, and the shadow split lets operators quantify correctness
        /// before re-enabling active correction.
        /// </summary>
        /// <remarks>
        /// When this flag is <c>false</c> AND <see cref="EnableStage2ShadowMode"/> is true,
        /// the stage emits a <c>QC:DOSEREGIMEN_SHADOW:{target}:{score}</c> flag where it
        /// would have routed — observations are not mutated. When both flags are false, the
        /// stage runs no prediction at all.
        /// </remarks>
        /// <seealso cref="EnableStage2DoseRegimenRouting"/>
        /// <seealso cref="EnableStage2ShadowMode"/>
        public bool EnableStage2DoseRegimenRoutingCorrection { get; set; } = true;

        /**************************************************************/
        /// <summary>
        /// PR #4 — When <see cref="EnableStage2DoseRegimenRoutingCorrection"/> is <c>false</c>,
        /// the Stage 2 classifier still runs in shadow mode when this flag is <c>true</c>,
        /// emitting <c>QC:DOSEREGIMEN_SHADOW:{target}:{score}</c> on flags where the
        /// prediction would have fired. Default <c>false</c> — no shadow emission unless
        /// correction is explicitly disabled and an audit trail is wanted.
        /// </summary>
        /// <seealso cref="EnableStage2DoseRegimenRoutingCorrection"/>
        public bool EnableStage2ShadowMode { get; set; } = false;

        /**************************************************************/
        /// <summary>
        /// R9 — Per-stage toggle for Stage 3 PrimaryValueType disambiguation. Default
        /// <c>true</c>. Like Stage 2, not affected by the R9 classifier issues.
        /// </summary>
        public bool EnableStage3PrimaryValueTypeDisambiguation { get; set; } = true;

        #endregion

        #region classification threshold properties

        /**************************************************************/
        /// <summary>
        /// Minimum prediction confidence for Stage 1 TableCategory override.
        /// Only corrects the category when the ML model's max class score exceeds this threshold
        /// AND disagrees with the current category. Default 0.90 (high bar to avoid false corrections).
        /// </summary>
        public float TableCategoryMinConfidence { get; set; } = 0.90f;

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
        /// Minimum rows required before Stage 1/2/3 classifiers can be trained. Applied as an
        /// absolute row-count floor against the full accumulator — no per-category slicing,
        /// since the classifiers train across the whole distribution.
        /// Default 10.
        /// </summary>
        public int MinTrainingRowsPerCategory { get; set; } = 10;

        /**************************************************************/
        /// <summary>
        /// Number of new rows that must accumulate since last training before a retrain is triggered.
        /// Prevents retraining on every batch while ensuring models improve as more data arrives.
        /// Default 100.
        /// </summary>
        public int RetrainingBatchSize { get; set; } = 100;

        #endregion

        #region training store settings

        /**************************************************************/
        /// <summary>
        /// Path to the ML training store JSON file. When set, training records persist across
        /// process restarts. Null = ephemeral (current behavior, accumulator is in-memory only
        /// and lost on restart).
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
        /// Default 60,000. At ~800–900 bytes/record (indented JSON) ≈ 50–55 MB on disk;
        /// use <see cref="MaxTrainingStoreSizeBytes"/> to cap the actual file size.
        /// </summary>
        public int MaxAccumulatorRows { get; set; } = 60_000;

        /**************************************************************/
        /// <summary>
        /// Maximum serialized file size for the training store JSON, in bytes.
        /// When the file would exceed this limit, oldest bootstrap records are evicted first,
        /// then oldest ground-truth records, until the serialized output fits within the cap.
        /// Enforced on every save and also at load time (to trim files written by older builds).
        /// Default 40 MB.
        /// </summary>
        public long MaxTrainingStoreSizeBytes { get; set; } = 40L * 1024 * 1024; // 40 MB

        #endregion
    }
}
