namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Root persisted state for the ML training store. Contains the training records list
    /// and retrain timestamps. Serialized to JSON via <c>System.Text.Json</c>.
    /// </summary>
    /// <remarks>
    /// ## Persistence
    /// Written atomically (tmp + rename) by <see cref="MedRecProImportClass.Service.TransformationServices.QCTrainingStore"/>.
    /// Thread-safe access via <c>SemaphoreSlim(1, 1)</c>.
    ///
    /// ## Schema History
    /// The adaptive-threshold fields (<c>AdaptiveThreshold</c>, <c>TotalSentToClaude</c>,
    /// <c>TotalCorrectedByClaude</c>, <c>LastThresholdEvaluatedAt</c>,
    /// <c>ThresholdAdjustmentCount</c>) were removed along with the Stage 4 anomaly pipeline
    /// on 2026-04-24 — the raw PCA scores they ratcheted against proved to have no absolute
    /// semantic. Older persisted store files may still carry those fields; they are ignored
    /// on load.
    ///
    /// ## Version Field
    /// <see cref="Version"/> allows future schema migrations without breaking existing stores.
    /// </remarks>
    /// <seealso cref="QCTrainingRecord"/>
    /// <seealso cref="MedRecProImportClass.Service.TransformationServices.IQCTrainingStore"/>
    public class QCTrainingStoreState
    {
        #region Schema version

        /**************************************************************/
        /// <summary>
        /// Schema version for forward compatibility. Increment when the state shape changes.
        /// Current version: 4 (removed Stage 4 anomaly adaptive-threshold fields).
        /// </summary>
        public int Version { get; set; } = 4;

        #endregion Schema version

        #region Training records

        /**************************************************************/
        /// <summary>
        /// Training records accumulated from high-confidence bootstrap rows and Claude-corrected
        /// ground truth. Capped at <see cref="QCNetCorrectionSettings.MaxAccumulatorRows"/>.
        /// </summary>
        public List<QCTrainingRecord> Records { get; set; } = new();

        #endregion Training records

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
