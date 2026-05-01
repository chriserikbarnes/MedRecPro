using MedRecProImportClass.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// File-backed implementation of <see cref="IQCTrainingStore"/>. Persists ML training records
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
    /// Two independent caps are enforced — whichever binds first wins:
    /// - **Row cap** (<see cref="QCNetCorrectionSettings.MaxAccumulatorRows"/>): fires in <c>AddRecordsAsync</c>
    /// - **Size cap** (<see cref="QCNetCorrectionSettings.MaxTrainingStoreSizeBytes"/>): fires in every <c>saveInternalAsync</c> call and on load
    ///
    /// Both caps use the same bootstrap-first priority:
    /// 1. Evict oldest bootstrap records first (<c>IsClaudeGroundTruth == false</c>)
    /// 2. If still over capacity, evict oldest ground-truth records
    ///
    /// ## Stage 4 Retirement Note
    /// The adaptive-threshold path (<c>GetAdaptiveThreshold</c> / <c>RecordClaudeFeedbackAsync</c>)
    /// was removed on 2026-04-24 along with Stage 4 anomaly scoring. Claude forwarding is
    /// now driven by the deterministic parse-quality gate, not a ratcheted ML threshold.
    /// </remarks>
    /// <seealso cref="IQCTrainingStore"/>
    /// <seealso cref="QCTrainingStoreState"/>
    /// <seealso cref="QCNetCorrectionSettings"/>
    public class QCTrainingStore : IQCTrainingStore
    {
        #region Fields

        /**************************************************************/
        /// <summary>Logger for diagnostics.</summary>
        private readonly ILogger<QCTrainingStore> _logger;

        /**************************************************************/
        /// <summary>Configuration settings controlling max rows, thresholds, and file path.</summary>
        private readonly QCNetCorrectionSettings _settings;

        /**************************************************************/
        /// <summary>Absolute path to the JSON persistence file.</summary>
        private readonly string _filePath;

        /**************************************************************/
        /// <summary>Serialization lock — serializes all mutating operations.</summary>
        private readonly SemaphoreSlim _lock = new(1, 1);

        /**************************************************************/
        /// <summary>In-memory state. Loaded from disk on <see cref="LoadAsync"/>, written back on saves.</summary>
        private QCTrainingStoreState _state = new();

        /**************************************************************/
        /// <summary>JSON serializer options: compact, skip nulls.</summary>
        /// <remarks>
        /// <c>WriteIndented</c> is <c>false</c> to halve per-save bytes and serialization cost —
        /// the store is not a human-readable artifact during runtime, so indentation is pure overhead.
        /// </remarks>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
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
        /// <param name="settings">Configuration settings. <see cref="QCNetCorrectionSettings.TrainingStoreFilePath"/>
        /// must be set to a valid file path.</param>
        /// <exception cref="ArgumentException">Thrown if <c>TrainingStoreFilePath</c> is null or empty.</exception>
        public QCTrainingStore(
            ILogger<QCTrainingStore> logger,
            QCNetCorrectionSettings settings)
        {
            #region implementation

            _logger = logger;
            _settings = settings;

            if (string.IsNullOrWhiteSpace(settings.TrainingStoreFilePath))
                throw new ArgumentException(
                    "TrainingStoreFilePath must be set when using QCTrainingStore.",
                    nameof(settings));

            _filePath = settings.TrainingStoreFilePath;

            #endregion
        }

        #endregion Constructor

        #region IQCTrainingStore Implementation

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
                    _state = JsonSerializer.Deserialize<QCTrainingStoreState>(json, _jsonOptions)
                             ?? new QCTrainingStoreState();

                    // Trim oversized files written by older builds before the size cap existed
                    var fileLen = new FileInfo(_filePath).Length;
                    if (fileLen > _settings.MaxTrainingStoreSizeBytes && _state.Records.Count > 0)
                    {
                        _logger.LogWarning(
                            "Training store ({Size:F1} MB) exceeds {Max:F1} MB limit; evicting oldest records on load.",
                            fileLen / 1_048_576.0, _settings.MaxTrainingStoreSizeBytes / 1_048_576.0);
                        await saveInternalAsync(ct);
                    }

                    _logger.LogInformation(
                        "ML training store loaded: {Count} records",
                        _state.Records.Count);
                }
                else
                {
                    _state = new QCTrainingStoreState();
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
        public async Task AddRecordsAsync(IEnumerable<QCTrainingRecord> records, CancellationToken ct = default)
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
        public IReadOnlyList<QCTrainingRecord> GetRecords()
        {
            #region implementation

            return _state.Records.AsReadOnly();

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

        #endregion IQCTrainingStore Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Writes state to disk using atomic tmp+rename pattern. Serializes to bytes first,
        /// enforces <see cref="QCNetCorrectionSettings.MaxTrainingStoreSizeBytes"/>, then writes
        /// the trimmed result — the on-disk file never exceeds the configured limit.
        /// Must be called within <see cref="_lock"/>.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        private async Task saveInternalAsync(CancellationToken ct)
        {
            #region implementation

            _state.LastSavedAt = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(_state, _jsonOptions);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

            // Size-based eviction: enforce MaxTrainingStoreSizeBytes before writing to disk
            long maxBytes = _settings.MaxTrainingStoreSizeBytes;
            if (bytes.Length > maxBytes && _state.Records.Count > 0)
            {
                double bytesPerRecord = (double)bytes.Length / _state.Records.Count;
                int toEvict = (int)Math.Ceiling((bytes.Length - maxBytes) / bytesPerRecord * 1.1);
                toEvict = Math.Min(toEvict, _state.Records.Count);

                int before = _state.Records.Count;
                evictOldest(toEvict);

                _logger.LogInformation(
                    "Size-based eviction: removed {Evicted} records to stay under {Max:F1} MB; {Remaining} remaining.",
                    before - _state.Records.Count, maxBytes / 1_048_576.0, _state.Records.Count);

                // Re-serialize with evicted records removed
                json = JsonSerializer.Serialize(_state, _jsonOptions);
                bytes = System.Text.Encoding.UTF8.GetBytes(json);
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var tempPath = _filePath + ".tmp";
            await File.WriteAllBytesAsync(tempPath, bytes, ct);
            File.Move(tempPath, _filePath, overwrite: true);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Enforces <see cref="QCNetCorrectionSettings.MaxAccumulatorRows"/> by evicting
        /// oldest bootstrap records first, then oldest ground-truth records if still over capacity.
        /// Must be called within <see cref="_lock"/>.
        /// </summary>
        private void evictIfOverCapacity()
        {
            #region implementation

            if (_state.Records.Count <= _settings.MaxAccumulatorRows)
                return;

            var overflow = _state.Records.Count - _settings.MaxAccumulatorRows;
            evictOldest(overflow);

            _logger.LogDebug(
                "Row-cap eviction: removed {Evicted} records, {Remaining} remaining",
                overflow, _state.Records.Count);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Evicts up to <paramref name="count"/> records using bootstrap-first priority:
        /// Phase 1 removes the oldest bootstrap records (<c>IsClaudeGroundTruth == false</c>);
        /// Phase 2 removes the oldest overall records if more eviction is still needed.
        /// Must be called within <see cref="_lock"/>.
        /// </summary>
        /// <param name="count">Maximum number of records to remove.</param>
        private void evictOldest(int count)
        {
            #region implementation

            if (count <= 0 || _state.Records.Count == 0) return;

            // Phase 1: evict oldest bootstrap records first
            var bootstrapIndices = new List<int>();
            for (int i = 0; i < _state.Records.Count && bootstrapIndices.Count < count; i++)
            {
                if (!_state.Records[i].IsClaudeGroundTruth)
                    bootstrapIndices.Add(i);
            }

            // Remove in reverse order to preserve indices
            for (int i = bootstrapIndices.Count - 1; i >= 0; i--)
                _state.Records.RemoveAt(bootstrapIndices[i]);

            // Phase 2: if still need to evict, remove oldest overall (rare — mostly ground-truth)
            int remaining = count - bootstrapIndices.Count;
            if (remaining > 0 && _state.Records.Count > 0)
                _state.Records.RemoveRange(0, Math.Min(remaining, _state.Records.Count));

            #endregion
        }

        #endregion Private Helpers
    }
}
