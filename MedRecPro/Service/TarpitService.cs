using MedRecPro.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace MedRecPro.Service;

/**************************************************************/
/// <summary>
/// Singleton service that tracks 404 hit counts per client IP address,
/// monitors rate-based abuse on configurable endpoints, and provides
/// delay calculations for the tarpit middleware.
/// </summary>
/// <remarks>
/// Uses two <see cref="ConcurrentDictionary{TKey,TValue}"/> instances for thread-safe tracking:
/// one for 404 hits (keyed by bare IP) and one for endpoint abuse (keyed by "IP|path").
/// A <see cref="Timer"/> runs periodic cleanup of stale entries to prevent memory leaks.
///
/// **404 delay formula:** <c>min(2^(hitCount - TriggerThreshold) * 1000, MaxDelayMs)</c>
/// **Endpoint delay formula:** <c>min(2^(hitCount - EndpointRateThreshold) * 1000, MaxDelayMs)</c>
///
/// **Memory management:**
/// - Timer fires every <see cref="TarpitSettings.CleanupIntervalMinutes"/> to purge stale entries from both dictionaries.
/// - Hard cap of <see cref="TarpitSettings.MaxTrackedIps"/> evicts oldest entries across both dictionaries when exceeded.
///
/// Follows the same singleton-with-state pattern used by
/// <see cref="ThrottleStateService"/>.
/// </remarks>
/// <seealso cref="TarpitSettings"/>
/// <seealso cref="MedRecPro.Middleware.TarpitMiddleware"/>
/// <seealso cref="ThrottleStateService"/>
public class TarpitService : IDisposable
{
    #region Nested Types

    /**************************************************************/
    /// <summary>
    /// Represents a tracked IP entry with hit count and last activity timestamp.
    /// </summary>
    internal readonly record struct TarpitEntry(int Count, DateTime LastHit);

    /**************************************************************/
    /// <summary>
    /// Represents a tracked endpoint abuse entry with hit count, tumbling window
    /// start time, and last activity timestamp.
    /// </summary>
    /// <remarks>
    /// When the current time exceeds <c>WindowStart + EndpointWindowSeconds</c>,
    /// the window is considered expired and the count resets to 1 on the next hit.
    /// </remarks>
    /// <seealso cref="TarpitSettings.EndpointWindowSeconds"/>
    internal readonly record struct EndpointAbuseEntry(int Count, DateTime WindowStart, DateTime LastHit);

    #endregion

    #region Private Fields

    private readonly ConcurrentDictionary<string, TarpitEntry> _tracker = new();
    private readonly ConcurrentDictionary<string, EndpointAbuseEntry> _endpointTracker = new();
    private readonly IOptionsMonitor<TarpitSettings> _settingsMonitor;
    private readonly ILogger<TarpitService> _logger;
    private Timer? _cleanupTimer;
    private bool _disposed;

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="TarpitService"/> class.
    /// </summary>
    /// <param name="settingsMonitor">Options monitor for hot-reloadable tarpit configuration.</param>
    /// <param name="logger">Logger instance for this service.</param>
    /// <seealso cref="TarpitSettings"/>
    public TarpitService(
        IOptionsMonitor<TarpitSettings> settingsMonitor,
        ILogger<TarpitService> logger)
    {
        #region implementation

        _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException(nameof(settingsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var settings = _settingsMonitor.CurrentValue;
        var intervalMs = Math.Max(1, settings.CleanupIntervalMinutes) * 60_000;

        _cleanupTimer = new Timer(
            purgeStaleEntries,
            state: null,
            dueTime: intervalMs,
            period: intervalMs);

        _logger.LogInformation(
            "TarpitService initialized — Enabled: {Enabled}, Threshold: {Threshold}, " +
            "MaxDelay: {MaxDelay}ms, CleanupInterval: {Interval}min, MaxTrackedIps: {MaxIps}",
            settings.Enabled, settings.TriggerThreshold,
            settings.MaxDelayMs, settings.CleanupIntervalMinutes, settings.MaxTrackedIps);

        #endregion
    }

    #endregion

    #region Public Properties

    /**************************************************************/
    /// <summary>
    /// Gets the current number of tracked IP addresses (404 tracker).
    /// </summary>
    /// <remarks>
    /// Useful for diagnostics and the GetFeatures() endpoint.
    /// </remarks>
    public int TrackedIpCount => _tracker.Count;

    /**************************************************************/
    /// <summary>
    /// Gets the current number of tracked endpoint abuse entries.
    /// </summary>
    /// <remarks>
    /// Each entry represents a unique client IP + monitored path combination.
    /// Useful for diagnostics.
    /// </remarks>
    /// <seealso cref="TarpitSettings.MonitoredEndpoints"/>
    public int TrackedEndpointCount => _endpointTracker.Count;

    #endregion

    #region Public Methods

    /**************************************************************/
    /// <summary>
    /// Records a 404 hit for the specified client IP, incrementing
    /// its counter and updating the last-hit timestamp.
    /// </summary>
    /// <param name="clientIp">The client IP address.</param>
    /// <remarks>
    /// Uses <see cref="ConcurrentDictionary{TKey,TValue}.AddOrUpdate"/>
    /// for atomic, thread-safe increment. If the dictionary exceeds
    /// <see cref="TarpitSettings.MaxTrackedIps"/>, oldest entries are evicted.
    /// </remarks>
    public void RecordHit(string clientIp)
    {
        #region implementation

        _tracker.AddOrUpdate(
            clientIp,
            _ => new TarpitEntry(1, DateTime.UtcNow),
            (_, existing) => new TarpitEntry(existing.Count + 1, DateTime.UtcNow));

        var settings = _settingsMonitor.CurrentValue;
        if (_tracker.Count + _endpointTracker.Count > settings.MaxTrackedIps)
        {
            evictOldestCombinedEntries(settings.MaxTrackedIps);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resets (removes) the tracked entry for the specified client IP.
    /// </summary>
    /// <param name="clientIp">The client IP address to reset.</param>
    /// <remarks>
    /// Called when <see cref="TarpitSettings.ResetOnSuccess"/> is enabled
    /// and the client makes a successful (non-404) request.
    /// Safe to call with an unknown IP — no exception is thrown.
    /// </remarks>
    public void ResetClient(string clientIp)
    {
        #region implementation

        _tracker.TryRemove(clientIp, out _);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the current 404 hit count for the specified client IP.
    /// </summary>
    /// <param name="clientIp">The client IP address.</param>
    /// <returns>The hit count, or 0 if the IP is not tracked.</returns>
    public int GetHitCount(string clientIp)
    {
        #region implementation

        return _tracker.TryGetValue(clientIp, out var entry) ? entry.Count : 0;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Calculates the delay in milliseconds for the given hit count
    /// using exponential backoff.
    /// </summary>
    /// <param name="hitCount">The number of consecutive 404 hits.</param>
    /// <returns>
    /// Delay in milliseconds: 0 if below threshold, otherwise
    /// <c>min(2^(hitCount - threshold) * 1000, MaxDelayMs)</c>.
    /// </returns>
    /// <remarks>
    /// Produces the progression: 1s, 2s, 4s, 8s, 16s, 30s (capped).
    /// </remarks>
    public int CalculateDelay(int hitCount)
    {
        #region implementation

        var settings = _settingsMonitor.CurrentValue;

        if (hitCount < settings.TriggerThreshold)
            return 0;

        var exponent = hitCount - settings.TriggerThreshold;
        var delayMs = (int)Math.Min(
            settings.MaxDelayMs,
            1000.0 * Math.Pow(2, exponent));

        return delayMs;

        #endregion
    }

    #endregion

    #region Endpoint Abuse Methods

    /**************************************************************/
    /// <summary>
    /// Records a hit on a monitored endpoint for the specified client IP,
    /// using a tumbling window to track request rate.
    /// </summary>
    /// <param name="clientIp">The client IP address.</param>
    /// <param name="path">The normalized (lowercase) matched endpoint path.</param>
    /// <remarks>
    /// The composite key is <c>"{clientIp}|{path}"</c>. If the tumbling window
    /// (defined by <see cref="TarpitSettings.EndpointWindowSeconds"/>) has expired,
    /// the counter resets to 1 with a new window start. Otherwise, the counter increments.
    ///
    /// After recording, checks the combined entry count across both dictionaries
    /// and evicts oldest entries if <see cref="TarpitSettings.MaxTrackedIps"/> is exceeded.
    /// </remarks>
    /// <seealso cref="GetEndpointHitCount"/>
    /// <seealso cref="CalculateEndpointDelay"/>
    public void RecordEndpointHit(string clientIp, string path)
    {
        #region implementation

        var key = $"{clientIp}|{path}";
        var settings = _settingsMonitor.CurrentValue;
        var windowDuration = TimeSpan.FromSeconds(Math.Max(1, settings.EndpointWindowSeconds));

        _endpointTracker.AddOrUpdate(
            key,
            _ => new EndpointAbuseEntry(1, DateTime.UtcNow, DateTime.UtcNow),
            (_, existing) =>
            {
                // If the window has expired, reset the counter
                if (DateTime.UtcNow - existing.WindowStart >= windowDuration)
                    return new EndpointAbuseEntry(1, DateTime.UtcNow, DateTime.UtcNow);

                return new EndpointAbuseEntry(existing.Count + 1, existing.WindowStart, DateTime.UtcNow);
            });

        if (_tracker.Count + _endpointTracker.Count > settings.MaxTrackedIps)
        {
            evictOldestCombinedEntries(settings.MaxTrackedIps);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the current hit count for the specified client IP on a monitored endpoint
    /// within the active tumbling window.
    /// </summary>
    /// <param name="clientIp">The client IP address.</param>
    /// <param name="path">The normalized (lowercase) matched endpoint path.</param>
    /// <returns>
    /// The hit count within the current window, or 0 if the IP+path is not tracked
    /// or the window has expired.
    /// </returns>
    /// <seealso cref="RecordEndpointHit"/>
    public int GetEndpointHitCount(string clientIp, string path)
    {
        #region implementation

        var key = $"{clientIp}|{path}";
        if (!_endpointTracker.TryGetValue(key, out var entry))
            return 0;

        var settings = _settingsMonitor.CurrentValue;
        var windowDuration = TimeSpan.FromSeconds(Math.Max(1, settings.EndpointWindowSeconds));

        // If window expired, the count is effectively 0
        if (DateTime.UtcNow - entry.WindowStart >= windowDuration)
            return 0;

        return entry.Count;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Calculates the delay in milliseconds for endpoint abuse using
    /// exponential backoff based on <see cref="TarpitSettings.EndpointRateThreshold"/>.
    /// </summary>
    /// <param name="hitCount">The number of hits within the current tumbling window.</param>
    /// <returns>
    /// Delay in milliseconds: 0 if below <see cref="TarpitSettings.EndpointRateThreshold"/>,
    /// otherwise <c>min(2^(hitCount - EndpointRateThreshold) * 1000, MaxDelayMs)</c>.
    /// </returns>
    /// <remarks>
    /// Uses the same exponential backoff formula as <see cref="CalculateDelay"/>
    /// but with <see cref="TarpitSettings.EndpointRateThreshold"/> as the threshold.
    /// </remarks>
    /// <seealso cref="CalculateDelay"/>
    public int CalculateEndpointDelay(int hitCount)
    {
        #region implementation

        var settings = _settingsMonitor.CurrentValue;

        if (hitCount < settings.EndpointRateThreshold)
            return 0;

        var exponent = hitCount - settings.EndpointRateThreshold;
        var delayMs = (int)Math.Min(
            settings.MaxDelayMs,
            1000.0 * Math.Pow(2, exponent));

        return delayMs;

        #endregion
    }

    #endregion

    #region Private Methods

    /**************************************************************/
    /// <summary>
    /// Timer callback that purges stale entries from both tracking dictionaries.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    /// <remarks>
    /// Iterates both the 404 tracker and endpoint abuse tracker, removing any
    /// entry whose last-hit timestamp exceeds <see cref="TarpitSettings.StaleEntryTimeoutMinutes"/>.
    /// Runs on a ThreadPool thread and does not block request processing.
    /// </remarks>
    private void purgeStaleEntries(object? state)
    {
        #region implementation

        try
        {
            var settings = _settingsMonitor.CurrentValue;
            var cutoff = DateTime.UtcNow.AddMinutes(-settings.StaleEntryTimeoutMinutes);
            var purgedCount = 0;

            // Sweep 404 tracker
            foreach (var kvp in _tracker)
            {
                if (kvp.Value.LastHit < cutoff)
                {
                    if (_tracker.TryRemove(kvp.Key, out _))
                        purgedCount++;
                }
            }

            // Sweep endpoint abuse tracker
            foreach (var kvp in _endpointTracker)
            {
                if (kvp.Value.LastHit < cutoff)
                {
                    if (_endpointTracker.TryRemove(kvp.Key, out _))
                        purgedCount++;
                }
            }

            if (purgedCount > 0)
            {
                _logger.LogInformation(
                    "TarpitService: Purged {PurgedCount} stale entries, {Remaining404} 404 + {RemainingEndpoint} endpoint entries remaining",
                    purgedCount, _tracker.Count, _endpointTracker.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TarpitService: Error during stale entry purge");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Evicts the oldest entries across both dictionaries when the combined count
    /// exceeds the maximum tracked IP cap.
    /// </summary>
    /// <param name="maxEntries">The maximum combined number of entries to retain.</param>
    /// <remarks>
    /// Merges entries from both the 404 tracker and endpoint abuse tracker,
    /// sorts by LastHit ascending, and removes enough from whichever dictionary
    /// owns each entry to bring the combined count at or below <paramref name="maxEntries"/>.
    /// </remarks>
    private void evictOldestCombinedEntries(int maxEntries)
    {
        #region implementation

        try
        {
            var combinedCount = _tracker.Count + _endpointTracker.Count;
            var excess = combinedCount - maxEntries;
            if (excess <= 0) return;

            // Merge both dictionaries into a unified list with source tag
            var combined = _tracker
                .Select(kvp => new { Key = kvp.Key, LastHit = kvp.Value.LastHit, Source = "404" })
                .Concat(_endpointTracker
                    .Select(kvp => new { Key = kvp.Key, LastHit = kvp.Value.LastHit, Source = "endpoint" }))
                .OrderBy(x => x.LastHit)
                .Take(excess)
                .ToList();

            foreach (var entry in combined)
            {
                if (entry.Source == "404")
                    _tracker.TryRemove(entry.Key, out _);
                else
                    _endpointTracker.TryRemove(entry.Key, out _);
            }

            _logger.LogWarning(
                "TarpitService: Evicted {EvictedCount} oldest entries — combined count exceeded MaxTrackedIps ({MaxIps})",
                combined.Count, maxEntries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TarpitService: Error during oldest entry eviction");
        }

        #endregion
    }

    #endregion

    #region IDisposable

    /**************************************************************/
    /// <summary>
    /// Disposes the cleanup timer and releases resources.
    /// </summary>
    public void Dispose()
    {
        #region implementation

        if (_disposed) return;

        _cleanupTimer?.Dispose();
        _cleanupTimer = null;
        _tracker.Clear();
        _endpointTracker.Clear();
        _disposed = true;

        _logger.LogInformation("TarpitService disposed");

        #endregion
    }

    #endregion
}
