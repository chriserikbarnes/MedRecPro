using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// File-backed persistence layer for ML training records and adaptive threshold state.
    /// Enables the Claude-to-ML feedback loop by persisting ground-truth corrections and
    /// bootstrap training data across process restarts.
    /// </summary>
    /// <remarks>
    /// ## Thread Safety
    /// All mutating operations are serialized via <c>SemaphoreSlim(1, 1)</c>.
    /// Reads of <see cref="GetRecords"/> and <see cref="GetAdaptiveThreshold"/> return
    /// snapshot values and do not require locking.
    ///
    /// ## Persistence Strategy
    /// Uses atomic write (tmp + rename) to prevent corruption on crash.
    /// Follows the same pattern as <c>StandardizationProgressTracker</c>.
    ///
    /// ## Lifecycle
    /// 1. Call <see cref="LoadAsync"/> at startup (loads existing state or initializes empty).
    /// 2. Call <see cref="AddRecordsAsync"/> after each ML scoring pass (bootstrap records).
    /// 3. Call <see cref="RecordClaudeFeedbackAsync"/> after each Claude correction pass.
    /// 4. Call <see cref="RecordRetrainAsync"/> after each ML retrain completes.
    /// 5. Call <see cref="SaveAsync"/> to force a flush (also called internally by mutating methods).
    /// </remarks>
    /// <seealso cref="MlTrainingRecord"/>
    /// <seealso cref="MlTrainingStoreState"/>
    /// <seealso cref="MlNetCorrectionSettings"/>
    public interface IMlTrainingStore
    {
        /**************************************************************/
        /// <summary>
        /// Loads persisted state from disk, or initializes empty state if the file does not exist.
        /// Safe to call multiple times (reloads from disk each time).
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task that completes when state is loaded.</returns>
        Task LoadAsync(CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Appends training records to the store. Triggers eviction if the record count
        /// exceeds <see cref="MlNetCorrectionSettings.MaxAccumulatorRows"/> (bootstrap records
        /// evicted first, then oldest ground-truth).
        /// </summary>
        /// <param name="records">Records to add.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task that completes when records are added and state is saved.</returns>
        Task AddRecordsAsync(IEnumerable<MlTrainingRecord> records, CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Returns a read-only snapshot of all training records in the store.
        /// </summary>
        /// <returns>Read-only list of training records.</returns>
        IReadOnlyList<MlTrainingRecord> GetRecords();

        /**************************************************************/
        /// <summary>
        /// Returns the current adaptive anomaly score threshold.
        /// </summary>
        /// <returns>Current threshold value (0.0 to 1.0).</returns>
        float GetAdaptiveThreshold();

        /**************************************************************/
        /// <summary>
        /// Records batch metrics from a Claude correction pass and evaluates whether the
        /// adaptive threshold should be raised. The threshold increases when the correction
        /// rate drops below <see cref="MlNetCorrectionSettings.AdaptiveThresholdCorrectionRateFloor"/>.
        /// </summary>
        /// <param name="totalObservations">Total observations sent to Claude in this batch.</param>
        /// <param name="correctedCount">Number of observations Claude actually corrected.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// The new threshold value if it was raised; null if no change occurred.
        /// </returns>
        Task<float?> RecordClaudeFeedbackAsync(int totalObservations, int correctedCount, CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Records that a model retrain has completed. Updates <see cref="MlTrainingStoreState.LastRetrainAt"/>
        /// and persists state.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task that completes when state is saved.</returns>
        Task RecordRetrainAsync(CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Forces an immediate save of the current state to disk.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task that completes when state is persisted.</returns>
        Task SaveAsync(CancellationToken ct = default);
    }
}
