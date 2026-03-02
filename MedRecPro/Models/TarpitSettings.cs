namespace MedRecPro.Models;

/**************************************************************/
/// <summary>
/// Configuration settings for the TarpitMiddleware that progressively delays
/// responses to clients generating repeated 404 errors and throttles abuse
/// on configurable success-returning endpoints.
/// </summary>
/// <remarks>
/// Bound to the "TarpitSettings" section in appsettings.json via the Options pattern.
/// All properties have sensible defaults, and the middleware can be disabled
/// entirely via the <see cref="Enabled"/> property.
///
/// **404 delay formula:** Once a client exceeds <see cref="TriggerThreshold"/> consecutive
/// 404 responses, the delay is calculated as:
/// <c>min(2^(hitCount - TriggerThreshold) * 1000, MaxDelayMs)</c>
///
/// **Endpoint abuse delay formula:** Once a client exceeds <see cref="EndpointRateThreshold"/>
/// hits on a monitored endpoint within <see cref="EndpointWindowSeconds"/>, the delay is:
/// <c>min(2^(hitCount - EndpointRateThreshold) * 1000, MaxDelayMs)</c>
///
/// **Memory management:** A background timer purges entries older than
/// <see cref="StaleEntryTimeoutMinutes"/>, and the combined dictionaries are hard-capped
/// at <see cref="MaxTrackedIps"/> entries to prevent unbounded growth.
/// </remarks>
/// <example>
/// <code>
/// // appsettings.json
/// "TarpitSettings": {
///     "Enabled": true,
///     "TriggerThreshold": 5,
///     "MaxDelayMs": 30000,
///     "StaleEntryTimeoutMinutes": 10,
///     "CleanupIntervalMinutes": 5,
///     "MaxTrackedIps": 10000,
///     "ResetOnSuccess": true,
///     "MonitoredEndpoints": [ "/api/", "/Home/Index" ],
///     "EndpointRateThreshold": 20,
///     "EndpointWindowSeconds": 60
/// }
/// </code>
/// </example>
/// <seealso cref="MedRecPro.Service.TarpitService"/>
/// <seealso cref="MedRecPro.Middleware.TarpitMiddleware"/>
public class TarpitSettings
{
    /**************************************************************/
    /// <summary>
    /// Master switch to enable or disable the tarpit middleware.
    /// </summary>
    /// <remarks>
    /// When disabled, the middleware passes through without any tracking or delay.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /**************************************************************/
    /// <summary>
    /// Number of consecutive 404 responses from the same client IP
    /// before progressive delays begin.
    /// </summary>
    /// <remarks>
    /// The first N hits are tracked but not delayed, allowing for
    /// legitimate typos or broken bookmarks.
    /// </remarks>
    public int TriggerThreshold { get; set; } = 5;

    /**************************************************************/
    /// <summary>
    /// Maximum delay in milliseconds applied to any single response.
    /// </summary>
    /// <remarks>
    /// The exponential backoff is capped at this value to prevent
    /// excessively long delays that could trigger upstream timeouts.
    /// Default: 30,000ms (30 seconds).
    /// </remarks>
    public int MaxDelayMs { get; set; } = 30_000;

    /**************************************************************/
    /// <summary>
    /// Minutes of inactivity after which a tracked IP entry is considered
    /// stale and eligible for cleanup.
    /// </summary>
    /// <remarks>
    /// The cleanup timer periodically sweeps the dictionary and removes
    /// entries whose last hit timestamp exceeds this threshold.
    /// </remarks>
    public int StaleEntryTimeoutMinutes { get; set; } = 10;

    /**************************************************************/
    /// <summary>
    /// Interval in minutes between cleanup timer sweeps.
    /// </summary>
    /// <remarks>
    /// The timer fires on a ThreadPool thread and does not block
    /// request processing. A shorter interval reduces peak memory
    /// but adds slightly more background work.
    /// </remarks>
    public int CleanupIntervalMinutes { get; set; } = 5;

    /**************************************************************/
    /// <summary>
    /// Hard cap on the number of tracked IP entries in the dictionary.
    /// </summary>
    /// <remarks>
    /// When exceeded, the oldest entries (by last hit timestamp) are evicted
    /// to bring the count back under the limit. At 10,000 entries the memory
    /// footprint is approximately 500–700 KB.
    /// </remarks>
    public int MaxTrackedIps { get; set; } = 10_000;

    /**************************************************************/
    /// <summary>
    /// Whether to reset a client's 404 counter when they make a successful
    /// (non-404) request.
    /// </summary>
    /// <remarks>
    /// When true, a legitimate user who hits one bad URL is forgiven as soon
    /// as they make any successful request. When false, counters only decay
    /// via the stale entry timeout.
    /// </remarks>
    public bool ResetOnSuccess { get; set; } = true;

    /**************************************************************/
    /// <summary>
    /// Path prefixes of endpoints to monitor for rate-based abuse.
    /// </summary>
    /// <remarks>
    /// Matching is case-insensitive using <c>StartsWith</c>. When a successful
    /// response (status &lt; 400) is returned for a monitored endpoint, the hit
    /// is tracked in a separate endpoint abuse dictionary instead of resetting
    /// the 404 counter.
    ///
    /// Example values: <c>"/api/"</c>, <c>"/Home/Index"</c>.
    /// An empty list disables endpoint abuse detection entirely.
    /// </remarks>
    /// <seealso cref="EndpointRateThreshold"/>
    /// <seealso cref="EndpointWindowSeconds"/>
    public List<string> MonitoredEndpoints { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Maximum number of hits allowed on a monitored endpoint within a single
    /// tumbling window before progressive delays begin.
    /// </summary>
    /// <remarks>
    /// When a client IP exceeds this threshold on any single monitored endpoint
    /// within <see cref="EndpointWindowSeconds"/>, the same exponential backoff
    /// formula is applied using this value as the threshold.
    /// </remarks>
    /// <seealso cref="MonitoredEndpoints"/>
    /// <seealso cref="EndpointWindowSeconds"/>
    public int EndpointRateThreshold { get; set; } = 20;

    /**************************************************************/
    /// <summary>
    /// Duration in seconds of the tumbling window for endpoint abuse detection.
    /// </summary>
    /// <remarks>
    /// When the window expires, the hit counter for that client+endpoint pair
    /// resets to 1. A shorter window is more forgiving (allows bursts) while a
    /// longer window catches sustained abuse.
    /// </remarks>
    /// <seealso cref="MonitoredEndpoints"/>
    /// <seealso cref="EndpointRateThreshold"/>
    public int EndpointWindowSeconds { get; set; } = 60;
}
