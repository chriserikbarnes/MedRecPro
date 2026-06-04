using Microsoft.Extensions.Options;

namespace MedRecPro.Models;

/**************************************************************/
/// <summary>
/// Configuration settings for the TarpitMiddleware that progressively delays
/// responses to clients generating repeated 404 errors and applies optional
/// rule-based monitoring to selected success-returning endpoints.
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
/// **Endpoint abuse delay formula:** Once a client exceeds the effective
/// endpoint rule threshold within the effective rule window, the delay is:
/// <c>min(2^(hitCount - threshold) * 1000, maxDelayMs)</c>
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
///     "EndpointMonitoring": {
///         "Enabled": true,
///         "DefaultRateThreshold": 60,
///         "DefaultWindowSeconds": 60,
///         "DefaultMaxDelayMs": 5000,
///         "ExcludedPathPrefixes": [ "/api/AdverseEvent/" ],
///         "Rules": [
///             {
///                 "Name": "home-index",
///                 "PathPrefix": "/Home/Index",
///                 "RateThreshold": 10,
///                 "WindowSeconds": 300,
///                 "MaxDelayMs": 30000
///             }
///         ]
///     },
///     "MonitoredEndpoints": []
/// }
/// </code>
/// </example>
/// <seealso cref="MedRecPro.Service.TarpitService"/>
/// <seealso cref="MedRecPro.Middleware.TarpitMiddleware"/>
/// <seealso cref="EndpointMonitoringSettings"/>
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
    /// Gets or sets rule-based endpoint monitoring settings for successful requests.
    /// </summary>
    /// <remarks>
    /// Endpoint monitoring is separate from repeated-404 tracking. Disabling this
    /// subsection stops success-returning endpoint abuse tracking while preserving
    /// the 404 tarpit behavior controlled by the top-level settings.
    /// </remarks>
    /// <seealso cref="EndpointMonitoringSettings"/>
    public EndpointMonitoringSettings EndpointMonitoring { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Legacy path prefixes of endpoints to monitor for rate-based abuse.
    /// </summary>
    /// <remarks>
    /// This property remains for backward compatibility with environment-specific
    /// configuration. New configuration should use <see cref="EndpointMonitoring"/>
    /// with explicit endpoint rules and exclusions. When no new endpoint rules are
    /// configured, these prefixes are translated into legacy endpoint policies.
    ///
    /// Example values: <c>"/api/"</c>, <c>"/Home/Index"</c>.
    /// An empty list disables endpoint abuse detection entirely.
    /// </remarks>
    /// <seealso cref="EndpointRateThreshold"/>
    /// <seealso cref="EndpointWindowSeconds"/>
    /// <seealso cref="EndpointMonitoring"/>
    public List<string> MonitoredEndpoints { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Legacy maximum number of hits allowed on a monitored endpoint within a
    /// single tumbling window before progressive delays begin.
    /// </summary>
    /// <remarks>
    /// Used only for <see cref="MonitoredEndpoints"/> fallback policies when
    /// <see cref="EndpointMonitoring"/> contains no rules. New endpoint rules
    /// should set <see cref="EndpointMonitoringSettings.DefaultRateThreshold"/>
    /// or <see cref="TarpitEndpointRule.RateThreshold"/>.
    /// </remarks>
    /// <seealso cref="MonitoredEndpoints"/>
    /// <seealso cref="EndpointWindowSeconds"/>
    /// <seealso cref="EndpointMonitoringSettings.DefaultRateThreshold"/>
    public int EndpointRateThreshold { get; set; } = 20;

    /**************************************************************/
    /// <summary>
    /// Legacy duration in seconds of the tumbling window for endpoint abuse detection.
    /// </summary>
    /// <remarks>
    /// Used only for <see cref="MonitoredEndpoints"/> fallback policies when
    /// <see cref="EndpointMonitoring"/> contains no rules. New endpoint rules
    /// should set <see cref="EndpointMonitoringSettings.DefaultWindowSeconds"/>
    /// or <see cref="TarpitEndpointRule.WindowSeconds"/>.
    /// </remarks>
    /// <seealso cref="MonitoredEndpoints"/>
    /// <seealso cref="EndpointRateThreshold"/>
    /// <seealso cref="EndpointMonitoringSettings.DefaultWindowSeconds"/>
    public int EndpointWindowSeconds { get; set; } = 60;

    /**************************************************************/
    /// <summary>
    /// Enables cookie-based client identification to maintain tracking
    /// continuity when client IP addresses rotate.
    /// </summary>
    /// <remarks>
    /// When enabled, the middleware sets an HttpOnly tracking cookie (<c>__tp</c>)
    /// on every response. Subsequent requests from the same browser are identified
    /// by the cookie value instead of the IP address. This solves the problem where
    /// Safari iCloud Private Relay, upstream agent services, or Cloudflare rotate
    /// the apparent client IP, causing each request to appear as a new client.
    ///
    /// When disabled, the middleware falls back to pure IP-based identification
    /// (the original behavior).
    ///
    /// **Cookie properties:** HttpOnly, Secure, SameSite=Strict, Path=/,
    /// MaxAge aligned with <see cref="StaleEntryTimeoutMinutes"/>.
    ///
    /// **Graceful degradation:** If the client blocks cookies, each request
    /// falls through to IP identification — identical to the current behavior.
    /// </remarks>
    /// <seealso cref="StaleEntryTimeoutMinutes"/>
    public bool EnableClientTracking { get; set; } = true;
}

/**************************************************************/
/// <summary>
/// Rule-based endpoint monitoring configuration for success-returning requests.
/// </summary>
/// <remarks>
/// Exclusions are evaluated before include rules. If a request path matches an
/// exclusion prefix, endpoint-abuse tracking is skipped and normal success
/// behavior, including <see cref="TarpitSettings.ResetOnSuccess"/>, can proceed.
/// </remarks>
/// <example>
/// <code>
/// "EndpointMonitoring": {
///   "Enabled": true,
///   "DefaultRateThreshold": 60,
///   "DefaultWindowSeconds": 60,
///   "DefaultMaxDelayMs": 5000,
///   "ExcludedPathPrefixes": [ "/api/AdverseEvent/" ],
///   "Rules": [
///     { "Name": "home-index", "PathPrefix": "/Home/Index" }
///   ]
/// }
/// </code>
/// </example>
/// <seealso cref="TarpitEndpointRule"/>
/// <seealso cref="TarpitEndpointPolicy"/>
public class EndpointMonitoringSettings
{
    /**************************************************************/
    /// <summary>
    /// Gets or sets whether success-returning endpoint monitoring is enabled.
    /// </summary>
    /// <remarks>
    /// This switch does not disable repeated-404 tracking. It only controls
    /// endpoint-abuse monitoring for successful responses.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /**************************************************************/
    /// <summary>
    /// Gets or sets the default number of hits allowed before endpoint delay begins.
    /// </summary>
    /// <remarks>
    /// Rules can override this value with <see cref="TarpitEndpointRule.RateThreshold"/>.
    /// </remarks>
    /// <seealso cref="TarpitEndpointRule.RateThreshold"/>
    public int DefaultRateThreshold { get; set; } = 60;

    /**************************************************************/
    /// <summary>
    /// Gets or sets the default endpoint monitoring window in seconds.
    /// </summary>
    /// <remarks>
    /// Rules can override this value with <see cref="TarpitEndpointRule.WindowSeconds"/>.
    /// </remarks>
    /// <seealso cref="TarpitEndpointRule.WindowSeconds"/>
    public int DefaultWindowSeconds { get; set; } = 60;

    /**************************************************************/
    /// <summary>
    /// Gets or sets the default maximum endpoint-abuse delay in milliseconds.
    /// </summary>
    /// <remarks>
    /// Rules can override this value with <see cref="TarpitEndpointRule.MaxDelayMs"/>.
    /// This cap is intentionally separate from the repeated-404 cap.
    /// </remarks>
    /// <seealso cref="TarpitEndpointRule.MaxDelayMs"/>
    public int DefaultMaxDelayMs { get; set; } = 5_000;

    /**************************************************************/
    /// <summary>
    /// Gets or sets path prefixes excluded from endpoint-abuse monitoring.
    /// </summary>
    /// <remarks>
    /// Prefix matching is case-insensitive and exclusions win over both new
    /// endpoint rules and legacy <see cref="TarpitSettings.MonitoredEndpoints"/>
    /// fallback policies.
    /// </remarks>
    /// <seealso cref="TarpitEndpointRule.PathPrefix"/>
    public List<string> ExcludedPathPrefixes { get; set; } = new();

    /**************************************************************/
    /// <summary>
    /// Gets or sets endpoint rules that should be monitored for rate-based abuse.
    /// </summary>
    /// <remarks>
    /// Rules are evaluated in configuration order. If this list contains enabled
    /// rules, legacy <see cref="TarpitSettings.MonitoredEndpoints"/> values are
    /// not used for endpoint matching.
    /// </remarks>
    /// <seealso cref="TarpitEndpointRule"/>
    public List<TarpitEndpointRule> Rules { get; set; } = new();
}

/**************************************************************/
/// <summary>
/// Configurable endpoint monitoring rule for a path prefix.
/// </summary>
/// <remarks>
/// A rule contributes a stable <see cref="Name"/> key plus optional threshold,
/// window, and delay overrides. Missing overrides fall back to
/// <see cref="EndpointMonitoringSettings"/> defaults.
/// </remarks>
/// <example>
/// <code>
/// { "Name": "home-index", "PathPrefix": "/Home/Index", "RateThreshold": 10 }
/// </code>
/// </example>
/// <seealso cref="EndpointMonitoringSettings"/>
/// <seealso cref="TarpitEndpointPolicy"/>
public class TarpitEndpointRule
{
    /**************************************************************/
    /// <summary>
    /// Gets or sets whether this endpoint rule is active.
    /// </summary>
    /// <remarks>
    /// Disabled rules are ignored by policy resolution and validation of rule-specific
    /// names and path prefixes.
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /**************************************************************/
    /// <summary>
    /// Gets or sets the stable tracking identity for this endpoint rule.
    /// </summary>
    /// <remarks>
    /// The service uses the normalized rule name as the endpoint tracking key,
    /// so path-prefix formatting changes do not create new endpoint buckets.
    /// Names must be unique case-insensitively among enabled rules.
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Gets or sets the request path prefix monitored by this rule.
    /// </summary>
    /// <remarks>
    /// Matching is case-insensitive using prefix semantics. Values should begin
    /// with <c>/</c>, for example <c>"/Home/Index"</c>.
    /// </remarks>
    public string PathPrefix { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Gets or sets an optional per-rule hit threshold.
    /// </summary>
    /// <remarks>
    /// When omitted, <see cref="EndpointMonitoringSettings.DefaultRateThreshold"/>
    /// is used.
    /// </remarks>
    /// <seealso cref="EndpointMonitoringSettings.DefaultRateThreshold"/>
    public int? RateThreshold { get; set; }

    /**************************************************************/
    /// <summary>
    /// Gets or sets an optional per-rule monitoring window in seconds.
    /// </summary>
    /// <remarks>
    /// When omitted, <see cref="EndpointMonitoringSettings.DefaultWindowSeconds"/>
    /// is used.
    /// </remarks>
    /// <seealso cref="EndpointMonitoringSettings.DefaultWindowSeconds"/>
    public int? WindowSeconds { get; set; }

    /**************************************************************/
    /// <summary>
    /// Gets or sets an optional per-rule maximum endpoint delay in milliseconds.
    /// </summary>
    /// <remarks>
    /// When omitted, <see cref="EndpointMonitoringSettings.DefaultMaxDelayMs"/>
    /// is used.
    /// </remarks>
    /// <seealso cref="EndpointMonitoringSettings.DefaultMaxDelayMs"/>
    public int? MaxDelayMs { get; set; }
}

/**************************************************************/
/// <summary>
/// Resolved endpoint monitoring policy for a specific request path.
/// </summary>
/// <remarks>
/// This DTO contains the effective values the middleware and service need after
/// applying exclusions, include rules, defaults, and legacy fallback settings.
/// </remarks>
/// <seealso cref="EndpointMonitoringSettings"/>
/// <seealso cref="TarpitEndpointRule"/>
/// <seealso cref="MedRecPro.Service.TarpitService"/>
public class TarpitEndpointPolicy
{
    /**************************************************************/
    /// <summary>
    /// Gets or sets the stable endpoint tracking key.
    /// </summary>
    /// <remarks>
    /// The value is normalized to lower case by policy resolution before it is
    /// combined with the client identifier in the endpoint tracker.
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Gets or sets the matched path prefix that produced this policy.
    /// </summary>
    /// <remarks>
    /// This value is retained for logging and diagnostics; it is not used as
    /// the endpoint dictionary key for new rules.
    /// </remarks>
    public string PathPrefix { get; set; } = string.Empty;

    /**************************************************************/
    /// <summary>
    /// Gets or sets the effective endpoint hit threshold.
    /// </summary>
    /// <remarks>
    /// Delays start when the active-window hit count reaches this value.
    /// </remarks>
    public int RateThreshold { get; set; }

    /**************************************************************/
    /// <summary>
    /// Gets or sets the effective endpoint hit-count window in seconds.
    /// </summary>
    /// <remarks>
    /// When this window expires, the next endpoint hit starts a new count window.
    /// </remarks>
    public int WindowSeconds { get; set; }

    /**************************************************************/
    /// <summary>
    /// Gets or sets the effective endpoint delay cap in milliseconds.
    /// </summary>
    /// <remarks>
    /// This cap applies only to endpoint-abuse delay for the resolved policy.
    /// </remarks>
    public int MaxDelayMs { get; set; }
}

/**************************************************************/
/// <summary>
/// Validates <see cref="TarpitSettings"/> after configuration binding.
/// </summary>
/// <remarks>
/// The validator supports nested endpoint monitoring rules that are too
/// contextual for simple data annotation attributes. Registered with
/// <c>ValidateOnStart()</c>, these checks fail application startup when
/// production configuration is malformed.
/// </remarks>
/// <seealso cref="TarpitSettings"/>
/// <seealso cref="EndpointMonitoringSettings"/>
/// <seealso cref="TarpitEndpointRule"/>
public class TarpitSettingsValidator : IValidateOptions<TarpitSettings>
{
    /**************************************************************/
    /// <summary>
    /// Validates a named <see cref="TarpitSettings"/> instance.
    /// </summary>
    /// <param name="name">The options name supplied by the options system.</param>
    /// <param name="options">The bound tarpit settings instance.</param>
    /// <returns>
    /// A success result when settings are valid; otherwise a failure result with
    /// all discovered configuration problems.
    /// </returns>
    /// <remarks>
    /// Validation keeps endpoint monitoring safe to hot-reload by enforcing
    /// positive thresholds and slash-prefixed include/exclude paths.
    /// </remarks>
    /// <seealso cref="ValidateOptionsResult"/>
    public ValidateOptionsResult Validate(string? name, TarpitSettings options)
    {
        #region implementation

        if (options == null)
        {
            return ValidateOptionsResult.Fail("TarpitSettings must be configured.");
        }

        var failures = new List<string>();

        if (options.TriggerThreshold <= 0)
            failures.Add("TarpitSettings:TriggerThreshold must be greater than 0.");

        if (options.MaxDelayMs < 0)
            failures.Add("TarpitSettings:MaxDelayMs must be greater than or equal to 0.");

        if (options.StaleEntryTimeoutMinutes <= 0)
            failures.Add("TarpitSettings:StaleEntryTimeoutMinutes must be greater than 0.");

        if (options.CleanupIntervalMinutes <= 0)
            failures.Add("TarpitSettings:CleanupIntervalMinutes must be greater than 0.");

        if (options.MaxTrackedIps <= 0)
            failures.Add("TarpitSettings:MaxTrackedIps must be greater than 0.");

        if (options.EndpointRateThreshold <= 0)
            failures.Add("TarpitSettings:EndpointRateThreshold must be greater than 0 for legacy endpoint policies.");

        if (options.EndpointWindowSeconds <= 0)
            failures.Add("TarpitSettings:EndpointWindowSeconds must be greater than 0 for legacy endpoint policies.");

        validateEndpointMonitoring(options.EndpointMonitoring, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Validates nested endpoint monitoring settings and appends failures.
    /// </summary>
    /// <param name="settings">The nested endpoint monitoring settings.</param>
    /// <param name="failures">The mutable failure list to append to.</param>
    /// <remarks>
    /// The method keeps nested-rule validation in one place while preserving the
    /// options validator contract used by ASP.NET Core.
    /// </remarks>
    /// <seealso cref="EndpointMonitoringSettings"/>
    /// <seealso cref="TarpitEndpointRule"/>
    private static void validateEndpointMonitoring(
        EndpointMonitoringSettings settings,
        List<string> failures)
    {
        #region implementation

        if (settings == null)
        {
            failures.Add("TarpitSettings:EndpointMonitoring must be configured.");
            return;
        }

        if (settings.DefaultRateThreshold <= 0)
            failures.Add("TarpitSettings:EndpointMonitoring:DefaultRateThreshold must be greater than 0.");

        if (settings.DefaultWindowSeconds <= 0)
            failures.Add("TarpitSettings:EndpointMonitoring:DefaultWindowSeconds must be greater than 0.");

        if (settings.DefaultMaxDelayMs < 0)
            failures.Add("TarpitSettings:EndpointMonitoring:DefaultMaxDelayMs must be greater than or equal to 0.");

        for (var index = 0; index < settings.ExcludedPathPrefixes.Count; index++)
        {
            var prefix = settings.ExcludedPathPrefixes[index];
            if (string.IsNullOrWhiteSpace(prefix) || !prefix.StartsWith('/'))
            {
                failures.Add(
                    $"TarpitSettings:EndpointMonitoring:ExcludedPathPrefixes[{index}] must be non-empty and begin with '/'.");
            }
        }

        var ruleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < settings.Rules.Count; index++)
        {
            var rule = settings.Rules[index];
            if (!rule.Enabled)
                continue;

            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                failures.Add($"TarpitSettings:EndpointMonitoring:Rules[{index}]:Name must be non-empty.");
            }
            else if (!ruleNames.Add(rule.Name.Trim()))
            {
                failures.Add(
                    $"TarpitSettings:EndpointMonitoring:Rules[{index}]:Name '{rule.Name}' must be unique case-insensitively.");
            }

            if (string.IsNullOrWhiteSpace(rule.PathPrefix) || !rule.PathPrefix.StartsWith('/'))
            {
                failures.Add($"TarpitSettings:EndpointMonitoring:Rules[{index}]:PathPrefix must be non-empty and begin with '/'.");
            }

            if (rule.RateThreshold.HasValue && rule.RateThreshold.Value <= 0)
                failures.Add($"TarpitSettings:EndpointMonitoring:Rules[{index}]:RateThreshold must be greater than 0 when supplied.");

            if (rule.WindowSeconds.HasValue && rule.WindowSeconds.Value <= 0)
                failures.Add($"TarpitSettings:EndpointMonitoring:Rules[{index}]:WindowSeconds must be greater than 0 when supplied.");

            if (rule.MaxDelayMs.HasValue && rule.MaxDelayMs.Value < 0)
                failures.Add($"TarpitSettings:EndpointMonitoring:Rules[{index}]:MaxDelayMs must be greater than or equal to 0 when supplied.");
        }

        #endregion
    }
}
