using MedRecProImportClass.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// File-backed implementation of <see cref="IMlTrainingStore"/>. Persists ML training records
    /// and adaptive threshold state to a JSON file using atomic writes (tmp + rename) and
    /// <c>SemaphoreSlim(1, 1)</c> for thread safety.
    /// </summary>
    /// <remarks>
    /// ## Persistence Pattern
    /// Follows the same pattern as <c>StandardizationProgressTracker</c>:
    /// - Write to <c>{path}.tmp</c>, then <c>File.Move(tmp, path, overwrite: true)</c>
    /// - <c>SemaphoreSlim(1, 1)</c> serializes all mutating operations
    /// - <c>System.Text.Json</c> with <c>WriteIndented = true</c> and <c>WhenWritingNull</c>
    ///
    /// ## Eviction Strategy
    /// When records exceed <see cref="MlNetCorrectionSettings.MaxAccumulatorRows"/>:
    /// 1. Evict oldest bootstrap records first (<c>IsClaudeGroundTruth == false</c>)
    /// 2. If still over capacity, evict oldest ground-truth records
    ///
    /// ## Adaptive Threshold Logic
    /// <see cref="RecordClaudeFeedbackAsync"/> evaluates the lifetime correction rate
    /// (corrected / sent) and raises the threshold when it drops below the configured floor.
    /// </remarks>
    /// <seealso cref="IMlTrainingStore"/>
    /// <seealso cref="MlTrainingStoreState"/>
    /// <seealso cref="MlNetCorrectionSettings"/>
    public class MlTrainingStore : IMlTrainingStore
    {
        #region Fields

        /**************************************************************/
        /// <summary>Logger for diagnostics.</summary>
        private readonly ILogger<MlTrainingStore> _logger;

        /**************************************************************/
        /// <summary>Configuration settings controlling max rows, thresholds, and file path.</summary>
        private readonly MlNetCorrectionSettings _settings;

        /**************************************************************/
        /// <summary>Absolute path to the JSON persistence file.</summary>
        private readonly string _filePath;

        /**************************************************************/
        /// <summary>Serialization lock — serializes all mutating operations.</summary>
        private readonly SemaphoreSlim _lock = new(1, 1);

        /**************************************************************/
        /// <summary>In-memory state. Loaded from disk on <see cref="LoadAsync"/>, written back on saves.</summary>
        private MlTrainingStoreState _state = new();

        /**************************************************************/
        /// <summary>JSON serializer options: indented, skip nulls.</summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #endregion Fields

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the training store with the given settings. Does NOT load state —
        /// call <see cref="LoadAsync"/> after construction.
        /// </summary>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <param name="settings">Configuration settings. <see cref="MlNetCorrectionSettings.TrainingStoreFilePath"/>
        /// must be set to a valid file path.</param>
        /// <exception cref="ArgumentException">Thrown if <c>TrainingStoreFilePath</c> is null or empty.</exception>
        public MlTrainingStore(
            ILogger<MlTrainingStore> logger,
            MlNetCorrectionSettings settings)
        {
            #region implementation

            _logger = logger;
            _settings = settings;

            if (string.IsNullOrWhiteSpace(settings.TrainingStoreFilePath))
                throw new ArgumentException(
                    "TrainingStoreFilePath must be set when using MlTrainingStore.",
                    nameof(settings));

            _filePath = settings.TrainingStoreFilePath;

            #endregion
        }

        #endregion Constructor

        #region IMlTrainingStore Implementation

        /**************************************************************/
        /// <inheritdoc/>
        public async Task LoadAsync(CancellationToken ct = default)
        {
            #region implementation

            await _lock.WaitAsync(ct);
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = await File.ReadAllTextAsync(_filePath, ct);
                    _state = JsonSerializer.Deserialize<MlTrainingStoreState>(json, _jsonOptions)
                             ?? new MlTrainingStoreState();
                    _logger.LogInformation(
                        "ML training store loaded: {Count} records, adaptive threshold={Threshold:F4}, " +
                        "lifetime sent={Sent}, corrected={Corrected}",
                        _state.Records.Count, _state.AdaptiveThreshold,
                        _state.TotalSentToClaude, _state.TotalCorrectedByClaude);
                }
                else
                {
                    _state = new MlTrainingStoreState();
                    _logger.LogInformation("ML training store initialized (no existing file at {Path})", _filePath);
                }
            }
            finally
            {
                _lock.Release();
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task AddRecordsAsync(IEnumerable<MlTrainingRecord> records, CancellationToken ct = default)
        {
            #region implementation

            await _lock.WaitAsync(ct);
            try
            {
                _state.Records.AddRange(records);

                // Eviction: enforce MaxAccumulatorRows
                evictIfOverCapacity();

                await saveInternalAsync(ct);
            }
            finally
            {
                _lock.Release();
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public IReadOnlyList<MlTrainingRecord> GetRecords()
        {
            #region implementation

            return _state.Records.AsReadOnly();

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public float GetAdaptiveThreshold()
        {
            #region implementation

            return _state.AdaptiveThreshold;

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<float?> RecordClaudeFeedbackAsync(int totalObservations, int correctedCount, CancellationToken ct = default)
        {
            #region implementation

            await _lock.WaitAsync(ct);
            try
            {
                _state.TotalSentToClaude += totalObservations;
                _state.TotalCorrectedByClaude += correctedCount;

                // Guard: not enough lifetime observations yet
                if (_state.TotalSentToClaude < _settings.AdaptiveThresholdMinObservations)
                {
                    await saveInternalAsync(ct);
                    return null;
                }

                // Guard: not enough new observations since last evaluation
                if (_state.TotalSentToClaude - _state.LastThresholdEvaluatedAt < _settings.AdaptiveThresholdEvaluationInterval)
                {
                    await saveInternalAsync(ct);
                    return null;
                }

                _state.LastThresholdEvaluatedAt = _state.TotalSentToClaude;
                var correctionRate = _state.TotalCorrectedByClaude / (double)_state.TotalSentToClaude;

                if (correctionRate < _settings.AdaptiveThresholdCorrectionRateFloor)
                {
                    _state.AdaptiveThreshold = Math.Min(
                        _state.AdaptiveThreshold + _settings.AdaptiveThresholdStep,
                        _settings.AdaptiveThresholdCeiling);
                    _state.ThresholdAdjustmentCount++;

                    _logger.LogInformation(
                        "Adaptive threshold raised to {Threshold:F4} (correction rate={Rate:P2}, " +
                        "adjustments={Count})",
                        _state.AdaptiveThreshold, correctionRate, _state.ThresholdAdjustmentCount);

                    await saveInternalAsync(ct);
                    return _state.AdaptiveThreshold;
                }

                await saveInternalAsync(ct);
                return null;
            }
            finally
            {
                _lock.Release();
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task RecordRetrainAsync(CancellationToken ct = default)
        {
            #region implementation

            await _lock.WaitAsync(ct);
            try
            {
                _state.LastRetrainAt = DateTime.UtcNow;
                await saveInternalAsync(ct);
            }
            finally
            {
                _lock.Release();
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task SaveAsync(CancellationToken ct = default)
        {
            #region implementation

            await _lock.WaitAsync(ct);
            try
            {
                await saveInternalAsync(ct);
            }
            finally
            {
                _lock.Release();
            }

            #endregion
        }

        #endregion IMlTrainingStore Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Writes state to disk using atomic tmp+rename pattern.
        /// Must be called within <see cref="_lock"/>.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        private async Task saveInternalAsync(CancellationToken ct)
        {
            #region implementation

            _state.LastSavedAt = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(_state, _jsonOptions);
            var tempPath = _filePath + ".tmp";

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(tempPath, json, ct);
            File.Move(tempPath, _filePath, overwrite: true);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Enforces <see cref="MlNetCorrectionSettings.MaxAccumulatorRows"/> by evicting
        /// oldest bootstrap records first, then oldest ground-truth records if still over capacity.
        /// Must be called within <see cref="_lock"/>.
        /// </summary>
        private void evictIfOverCapacity()
        {
            #region implementation

            if (_state.Records.Count <= _settings.MaxAccumulatorRows)
                return;

            var overflow = _state.Records.Count - _settings.MaxAccumulatorRows;

            // Phase 1: Evict oldest bootstrap records (IsClaudeGroundTruth == false)
            var bootstrapIndices = new List<int>();
            for (int i = 0; i < _state.Records.Count && bootstrapIndices.Count < overflow; i++)
            {
                if (!_state.Records[i].IsClaudeGroundTruth)
                    bootstrapIndices.Add(i);
            }

            // Remove in reverse order to preserve indices
            for (int i = bootstrapIndices.Count - 1; i >= 0; i--)
            {
                _state.Records.RemoveAt(bootstrapIndices[i]);
            }

            // Phase 2: If still over (rare — means mostly ground-truth), evict oldest overall
            if (_state.Records.Count > _settings.MaxAccumulatorRows)
            {
                var remaining = _state.Records.Count - _settings.MaxAccumulatorRows;
                _state.Records.RemoveRange(0, remaining);
            }

            _logger.LogDebug(
                "Eviction complete: removed {Evicted} records, {Remaining} remaining",
                overflow, _state.Records.Count);

            #endregion
        }

        #endregion Private Helpers
    }
}
