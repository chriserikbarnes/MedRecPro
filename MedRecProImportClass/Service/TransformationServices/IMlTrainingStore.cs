using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// File-backed persistence layer for ML training records. Enables the
    /// Claude-to-ML feedback loop by persisting ground-truth corrections and
    /// bootstrap training data across process restarts.
    /// </summary>
    /// <remarks>
    /// ## Thread Safety
    /// All mutating operations are serialized via <c>SemaphoreSlim(1, 1)</c>.
    /// Reads of <see cref="GetRecords"/> return a snapshot and do not require locking.
    ///
    /// ## Persistence Strategy
    /// Uses atomic write (tmp + rename) to prevent corruption on crash.
    /// Follows the same pattern as <c>StandardizationProgressTracker</c>.
    ///
    /// ## Lifecycle
    /// 1. Call <see cref="LoadAsync"/> at startup (loads existing state or initializes empty).
    /// 2. Call <see cref="AddRecordsAsync"/> after each ML scoring pass (bootstrap records)
    ///    or after each Claude correction pass (ground-truth records).
    /// 3. Call <see cref="RecordRetrainAsync"/> after each ML retrain completes.
    /// 4. Call <see cref="SaveAsync"/> to force a flush (also called internally by mutating methods).
    ///
    /// ## Stage 4 Retirement Note
    /// The adaptive-threshold API (<c>GetAdaptiveThreshold</c>,
    /// <c>RecordClaudeFeedbackAsync</c>) was removed along with Stage 4 anomaly scoring on
    /// 2026-04-24. Claude forwarding is now driven by the deterministic parse-quality gate
    /// in <see cref="IParseQualityService"/>, not a ratcheted ML threshold.
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
