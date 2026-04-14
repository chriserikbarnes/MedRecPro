namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Root persisted state for the ML training store. Contains the training records list,
    /// adaptive threshold state, and lifetime metrics for the Claude-to-ML feedback loop.
    /// Serialized to JSON via <c>System.Text.Json</c>.
    /// </summary>
    /// <remarks>
    /// ## Persistence
    /// Written atomically (tmp + rename) by <see cref="MedRecProImportClass.Service.TransformationServices.MlTrainingStore"/>.
    /// Thread-safe access via <c>SemaphoreSlim(1, 1)</c>.
    ///
    /// ## Adaptive Threshold Fields
    /// <see cref="AdaptiveThreshold"/> tracks the current ML anomaly score threshold used to gate
    /// observations to Claude. As ML accuracy improves (low correction rate), the threshold rises
    /// and fewer rows are sent to Claude.
    ///
    /// ## Version Field
    /// <see cref="Version"/> allows future schema migrations without breaking existing stores.
    /// </remarks>
    /// <seealso cref="MlTrainingRecord"/>
    /// <seealso cref="MedRecProImportClass.Service.TransformationServices.IMlTrainingStore"/>
    public class MlTrainingStoreState
    {
        #region Schema version

        /**************************************************************/
        /// <summary>
        /// Schema version for forward compatibility. Increment when the state shape changes.
        /// Current version: 3 (added UNII to MlTrainingRecord for product-level anomaly key grouping).
        /// </summary>
        public int Version { get; set; } = 3;

        #endregion Schema version

        #region Training records

        /**************************************************************/
        /// <summary>
        /// Training records accumulated from high-confidence bootstrap rows and Claude-corrected
        /// ground truth. Capped at <see cref="MlNetCorrectionSettings.MaxAccumulatorRows"/>.
        /// </summary>
        public List<MlTrainingRecord> Records { get; set; } = new();

        #endregion Training records

        #region Adaptive threshold state

        /**************************************************************/
        /// <summary>
        /// Current adaptive ML anomaly score threshold. Propagated to
        /// <see cref="ClaudeApiCorrectionSettings.MlAnomalyScoreThreshold"/> at runtime.
        /// Default 0.0 (all observations pass to Claude — backward-compatible).
        /// </summary>
        public float AdaptiveThreshold { get; set; } = 0.0f;

        /**************************************************************/
        /// <summary>Lifetime count of observations sent to Claude (denominator for correction rate).</summary>
        public long TotalSentToClaude { get; set; }

        /**************************************************************/
        /// <summary>Lifetime count of observations actually corrected by Claude.</summary>
        public long TotalCorrectedByClaude { get; set; }

        /**************************************************************/
        /// <summary>
        /// Value of <see cref="TotalSentToClaude"/> at the last threshold evaluation.
        /// Used to enforce <see cref="MlNetCorrectionSettings.AdaptiveThresholdEvaluationInterval"/>.
        /// </summary>
        public long LastThresholdEvaluatedAt { get; set; }

        /**************************************************************/
        /// <summary>Number of times the adaptive threshold has been raised.</summary>
        public int ThresholdAdjustmentCount { get; set; }

        #endregion Adaptive threshold state

        #region Timestamps

        /**************************************************************/
        /// <summary>UTC timestamp of the last successful model retrain.</summary>
        public DateTime? LastRetrainAt { get; set; }

        /**************************************************************/
        /// <summary>UTC timestamp of the last successful save to disk.</summary>
        public DateTime? LastSavedAt { get; set; }

        #endregion Timestamps
    }
}
