using Azure.Core;
using Azure.Monitor.Query.Metrics;
using Azure.Monitor.Query.Metrics.Models;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Const = MedRecPro.Models.Constant;

namespace MedRecPro.Service;

/**************************************************************/
/// <summary>
/// Service for querying Azure SQL Database metrics using Azure Monitor Query Metrics API
/// and the Azure Management REST Metrics endpoint.
/// Tracks vCore usage against the monthly free tier limit (100,000 vCore seconds).
/// </summary>
/// <remarks>
/// This service first attempts to query Azure Monitor Metrics via the REST API
/// (management.azure.com). If that call does not return usable datapoints, it
/// falls back to the Azure.Monitor.Query.Metrics SDK-based implementation.
/// Results are cached to minimize API calls and reduce overhead.
/// The free tier provides 100,000 vCore seconds per month per database.
/// </remarks>
/// <example>
/// <code>
/// var service = new AzureSqlMetricsService(configuration, cache, tokenProvider);
/// var (used, remaining, percentUsed) = await service.GetFreeTierStatusAsync();
/// 
/// if (percentUsed > 90)
/// {
///     // Apply throttling
/// }
/// </code>
/// </example>
/// <seealso cref="MetricsClient"/>
/// <seealso cref="MetricsQueryResourcesResult"/>
/// <seealso cref="AzureManagementTokenProvider"/>
public class AzureSqlMetricsService
{
    #region Fields

    private readonly MetricsClient _metricsClient;
    private readonly string _resourceId;
    private readonly IMemoryCache _cache;
    private readonly AzureManagementTokenProvider _tokenProvider;
    private readonly HttpClient _httpClient;

    private readonly double _warningThreshold;
    private readonly double _moderateThreshold;
    private readonly double _aggressiveThreshold;
    private readonly double _criticalThreshold;
    private readonly double _maxMonthlyCostPercent;

    private const double FREE_TIER_MONTHLY_LIMIT = Const.FREE_TIER_MONTHLY_LIMIT;
    private const string FREE_AMOUNT_CONSUMED_METRIC = Const.FREE_AMOUNT_CONSUMED_METRIC;
    private const string FREE_AMOUNT_REMAINING_METRIC = Const.FREE_AMOUNT_REMAINING_METRIC;
    private const string CACHE_KEY_PREFIX = Const.CACHE_KEY_PREFIX;

    // API version for Azure Monitor metrics REST endpoint
    private const string METRICS_API_VERSION = Const.METRICS_API_VERSION;

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureSqlMetricsService"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration containing Azure resource settings.</param>
    /// <param name="cache">Memory cache for storing metric query results.</param>
    /// <param name="tokenProvider">Token provider for Azure Management API.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required configuration values are missing.</exception>
    /// <remarks>
    /// The service requires the following configuration values:
    /// - Azure:SqlDatabase:ResourceId - Full Azure resource ID
    /// - Azure:SqlDatabase:MetricsRegion - Azure region (e.g., "eastus", "westus3")
    /// Uses app-only authentication via <see cref="AzureManagementTokenProvider"/> for both
    /// REST and SDK-based metrics queries.
    /// </remarks>
    /// <seealso cref="MetricsClient"/>
    /// <seealso cref="AzureManagementTokenProvider"/>
    public AzureSqlMetricsService(
        IConfiguration configuration,
        IMemoryCache cache,
        AzureManagementTokenProvider tokenProvider)
    {
        #region implementation

        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        if (cache == null)
            throw new ArgumentNullException(nameof(cache));

        if (tokenProvider == null)
            throw new ArgumentNullException(nameof(tokenProvider));

        _cache = cache;
        _tokenProvider = tokenProvider;

        // HttpClient is used for REST-based metrics queries against management.azure.com
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://management.azure.com")
        };

        // Retrieve resource ID from configuration
        _resourceId = configuration["Azure:SqlDatabase:ResourceId"]
            ?? throw new InvalidOperationException("Azure:SqlDatabase:ResourceId configuration is missing");

        // Retrieve metrics region from configuration
        var region = configuration["Azure:SqlDatabase:MetricsRegion"]
            ?? throw new InvalidOperationException("Azure:SqlDatabase:MetricsRegion configuration is missing");

        // Initialize MetricsClient with regional endpoint and app-only credential
        var endpoint = new Uri($"https://{region}.metrics.monitor.azure.com");
        var credential = new AppOnlyTokenCredential(tokenProvider);

        _metricsClient = new MetricsClient(endpoint, credential);

        // Load configurable thresholds from DatabaseUsageMonitor section
        var monitorSection = configuration.GetSection("DatabaseUsageMonitor");

        _warningThreshold = monitorSection.GetValue("WarningThreshold", 70.0);
        _moderateThreshold = monitorSection.GetValue("ModerateThreshold", 80.0);
        _aggressiveThreshold = monitorSection.GetValue("AggressiveThreshold", 90.0);
        _criticalThreshold = monitorSection.GetValue("CriticalThreshold", 95.0);
        _maxMonthlyCostPercent = monitorSection.GetValue("MaxMonthlyCostPercent", 110.0);

        #endregion
    }

    #endregion

    #region Private

    /**************************************************************/
    /// <summary>
    /// Writes a debug message and a formatted JSON representation of the specified object to the debug output when
    /// running in a debug build.
    /// </summary>
    /// <remarks>This method only produces output in debug builds. The object is serialized using indented
    /// JSON formatting for readability.</remarks>
    /// <typeparam name="T">The type of the object to be serialized and written to the debug output.</typeparam>
    /// <param name="obj">The object to serialize and output. If <paramref name="obj"/> is <see langword="null"/>, no output is written.</param>
    /// <param name="msg">An optional message to precede the serialized object in the debug output. If <paramref name="msg"/> is <see
    /// langword="null"/>, a default message is used.</param>
    private void toDebug<T>(T obj, string msg)
    {

#if DEBUG
        if (obj != null)
        {
            var debugJson = JsonConvert.SerializeObject(obj, Formatting.Indented);

            Debug.WriteLine(msg ?? "Azure SQL Metrics Query Result:");
            Debug.Write(debugJson);
        }
#endif
    }

    /**************************************************************/
    /// <summary>
    /// Queries the Azure Monitor Metrics REST endpoint for <c>free_amount_remaining</c>
    /// using 15-minute granularity for the specified time window.
    /// </summary>
    /// <param name="startTime">Start time of the query window (UTC).</param>
    /// <param name="endTime">End time of the query window (UTC).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// The minimum <c>free_amount_remaining</c> value in the time window, or
    /// <see langword="null"/> if no datapoints are returned or the call fails.
    /// </returns>
    /// <remarks>
    /// This method calls:
    /// <code>
    /// GET https://management.azure.com/{resourceId}/providers/microsoft.insights/metrics
    ///     ?metricnames=free_amount_remaining
    ///     &amp;timespan={start}/{end}
    ///     &amp;interval=PT15M
    ///     &amp;aggregation=Minimum
    ///     &amp;api-version=2018-01-01
    /// </code>
    /// and parses the <c>value[*].timeseries[*].data[*].minimum</c> samples.
    /// Any exception or non-success HTTP status results in a <see langword="null"/>
    /// return so that callers can transparently fall back to other mechanisms.
    /// </remarks>
    /// <example>
    /// <code>
    /// var remaining = await getRemainingFreeTierViaRestAsync(start, end);
    /// if (remaining.HasValue)
    /// {
    ///     // Use REST-based value
    /// }
    /// </code>
    /// </example>
    private async Task<double?> getRemainingFreeTierViaRestAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        #region implementation

        try
        {
            // Acquire bearer token for Azure Management API
            var accessToken = await _tokenProvider.GetAccessTokenAsync(cancellationToken);

            // Build query string for metrics REST call
            var timespan = $"{startTime.UtcDateTime:O}/{endTime.UtcDateTime:O}";

            var query = new StringBuilder();
            query.Append(_resourceId);
            query.Append("/providers/microsoft.insights/metrics?");
            query.Append("metricnames=");
            query.Append(Uri.EscapeDataString(FREE_AMOUNT_REMAINING_METRIC));
            query.Append("&timespan=");
            query.Append(Uri.EscapeDataString(timespan));
            query.Append("&interval=");
            query.Append(Uri.EscapeDataString("PT15M"));
            query.Append("&aggregation=");
            query.Append(Uri.EscapeDataString("Minimum"));
            query.Append("&api-version=");
            query.Append(Uri.EscapeDataString(METRICS_API_VERSION));

            var request = new HttpRequestMessage(HttpMethod.Get, query.ToString());
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // If REST call fails, log in debug and return null so callers can fall back
                toDebug(
                    new
                    {
                        StatusCode = response.StatusCode,
                        ReasonPhrase = response.ReasonPhrase
                    },
                    "Azure SQL Metrics REST call failed.");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            // Log raw JSON for troubleshooting
            toDebug(content, "Azure SQL Metrics REST JSON (free_amount_remaining):");

            var root = JObject.Parse(content);
            var remainingSamples = new List<double>();

            var valueArray = root["value"] as JArray;
            if (valueArray != null)
            {
                foreach (var metric in valueArray)
                {
                    var timeSeriesArray = metric["timeseries"] as JArray;
                    if (timeSeriesArray == null) continue;

                    foreach (var ts in timeSeriesArray)
                    {
                        var dataArray = ts["data"] as JArray;
                        if (dataArray == null) continue;

                        foreach (var dataPoint in dataArray)
                        {
                            var minimumToken = dataPoint["minimum"];
                            if (minimumToken != null && minimumToken.Type != JTokenType.Null)
                            {
                                if (double.TryParse(minimumToken.ToString(), out var minValue))
                                {
                                    remainingSamples.Add(minValue);
                                }
                            }
                        }
                    }
                }
            }

            if (remainingSamples.Count == 0)
            {
                // No usable datapoints → allow fallback
                return null;
            }

            // Lowest remaining over the month → current "worst case" remaining free vCore seconds
            var minRemaining = remainingSamples.Min();

            // Clamp to [0, FREE_TIER_MONTHLY_LIMIT] just in case
            if (minRemaining < 0) minRemaining = 0;
            if (minRemaining > FREE_TIER_MONTHLY_LIMIT) minRemaining = FREE_TIER_MONTHLY_LIMIT;

            return minRemaining;
        }
        catch (Exception ex)
        {
            // Any exception should not break the caller; log and fall back
            toDebug(
                new
                {
                    Exception = ex.GetType().FullName,
                    ex.Message,
                    ex.StackTrace
                },
                "Azure SQL Metrics REST call threw an exception.");
            return null;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Queries the Azure Monitor Metrics SDK for <c>free_amount_remaining</c>
    /// using 15-minute granularity for the specified time window.
    /// </summary>
    /// <param name="startTime">Start time of the query window (UTC).</param>
    /// <param name="endTime">End time of the query window (UTC).</param>
    /// <returns>The minimum <c>free_amount_remaining</c> value in the time window, or
    /// <see cref="FREE_TIER_MONTHLY_LIMIT"/> if no datapoints are returned.</returns>
    /// <remarks>
    /// This is the original implementation that uses <see cref="MetricsClient.QueryResourcesAsync"/>.
    /// It is retained as a fallback in case the REST endpoint returns nulls or is unavailable.
    /// </remarks>
    /// <example>
    /// <code>
    /// var remaining = await getRemainingFreeTierViaMetricsClientAsync(start, end);
    /// </code>
    /// </example>
    private async Task<double> getRemainingFreeTierViaMetricsClientAsync(
        DateTimeOffset startTime,
        DateTimeOffset endTime)
    {
        #region implementation

        // Query every 15 minutes to catch bursty usage
        var options = new MetricsQueryResourcesOptions
        {
            StartTime = startTime,
            EndTime = endTime,
            Granularity = TimeSpan.FromMinutes(15)
        };

        // We want the lowest remaining value in each bucket
        options.Aggregations.Add("Minimum");

        // Query free_amount_remaining
        var response = await _metricsClient.QueryResourcesAsync(
            resourceIds: new List<ResourceIdentifier> { new ResourceIdentifier(_resourceId) },
            metricNames: new List<string> { FREE_AMOUNT_REMAINING_METRIC },
            metricNamespace: "Microsoft.Sql/servers/databases",
            options: options);

        var metricsResult = response.Value;

        toDebug(metricsResult, "Azure SQL Metrics Query Result (free_amount_remaining):");

        var remainingSamples = new List<double>();

        foreach (var queryResult in metricsResult.Values)
        {
            foreach (var metric in queryResult.Metrics)
            {
                foreach (var timeSeries in metric.TimeSeries)
                {
                    // free_amount_remaining is a Count; we use Minimum because it monotonically decreases
                    remainingSamples.AddRange(
                        timeSeries.Values
                            .Where(v => v.Minimum.HasValue)
                            .Select(v => v.Minimum.Value));
                }
            }
        }

        if (remainingSamples.Count > 0)
        {
            var minRemaining = remainingSamples.Min();

            if (minRemaining < 0) minRemaining = 0;
            if (minRemaining > FREE_TIER_MONTHLY_LIMIT) minRemaining = FREE_TIER_MONTHLY_LIMIT;

            return minRemaining;
        }

        // No datapoints returned (very new / totally idle DB) → assume full allowance remaining
        return FREE_TIER_MONTHLY_LIMIT;

        #endregion
    }

    #endregion

    #region Public Methods

    /**************************************************************/
    /// <summary>
    /// Gets the current free tier status including used, remaining, and percentage consumed.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// - used: Total vCore seconds consumed this month
    /// - remaining: vCore seconds remaining in free tier
    /// - percentUsed: Percentage of free tier consumed (0-100)
    /// </returns>
    /// <exception cref="Azure.RequestFailedException">Thrown when the Azure Monitor API request fails.</exception>
    /// <remarks>
    /// This method caches results for 5 minutes to reduce API calls. The monthly free tier
    /// limit is 100,000 vCore seconds. Once exceeded, standard serverless rates apply
    /// (~$0.000145 per vCore second).
    /// </remarks>
    /// <example>
    /// <code>
    /// var (used, remaining, percentUsed) = await service.GetFreeTierStatusAsync();
    /// Console.WriteLine($"Used: {used:N0} | Remaining: {remaining:N0} | {percentUsed:F1}% consumed");
    /// </code>
    /// </example>
    /// <seealso cref="GetRemainingFreeTierVCoreSecondsAsync"/>
    /// <seealso cref="GetUsedVCoreSecondsThisMonthAsync"/>
    public async Task<(double used, double remaining, double percentUsed)> GetFreeTierStatusAsync()
    {
        #region implementation

        var cacheKey = $"{CACHE_KEY_PREFIX}FreeTierStatus_{DateTime.UtcNow:yyyyMMdd_HH}";

        // Check cache first to avoid excessive API calls
        if (_cache.TryGetValue(cacheKey, out (double, double, double) cachedResult))
        {
            return cachedResult;
        }

        var used = await GetUsedVCoreSecondsThisMonthAsync();
        var remaining = FREE_TIER_MONTHLY_LIMIT - used;
        var percentUsed = (used / FREE_TIER_MONTHLY_LIMIT) * 100.0;

        // Ensure remaining doesn't go negative
        if (remaining < 0)
            remaining = 0;

        var result = (used, remaining, percentUsed);

        // Cache for 5 minutes
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

        return result;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the total vCore seconds used in the current calendar month.
    /// </summary>
    /// <returns>Total vCore seconds consumed from the 1st of the month to now.</returns>
    /// <exception cref="Azure.RequestFailedException">
    /// Thrown when the Azure Monitor API request fails.
    /// </exception>
    /// <remarks>
    /// This method derives usage from the <c>free_amount_remaining</c> metric
    /// instead of querying <c>free_amount_consumed</c> directly.
    /// <para>
    /// The free offer provides <see cref="FREE_TIER_MONTHLY_LIMIT"/> vCore seconds
    /// per month. We query <see cref="GetRemainingFreeTierVCoreSecondsAsync"/> to
    /// get the minimum remaining value over 15-minute buckets from the start of
    /// the month, and compute:
    /// </para>
    /// <code>
    /// used = FREE_TIER_MONTHLY_LIMIT - remaining;
    /// </code>
    /// <para>
    /// This matches the Azure portal behavior where you see "Free amount remaining"
    /// as the primary metric for tracking free tier usage.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var used = await service.GetUsedVCoreSecondsThisMonthAsync();
    /// Console.WriteLine($"Used this month: {used:N0} vCore seconds");
    /// </code>
    /// </example>
    /// <seealso cref="GetRemainingFreeTierVCoreSecondsAsync"/>
    public async Task<double> GetUsedVCoreSecondsThisMonthAsync()
    {
        #region implementation

        var cacheKey = $"{CACHE_KEY_PREFIX}MonthlyUsage_{DateTime.UtcNow:yyyyMMdd_HH}";

        // Check cache first to avoid excessive API calls
        if (_cache.TryGetValue(cacheKey, out double cachedUsage))
        {
            return cachedUsage;
        }

        // Get remaining from free_amount_remaining (15-min buckets, Min)
        var remaining = await GetRemainingFreeTierVCoreSecondsAsync();

        // Derive used from the known monthly limit
        var used = FREE_TIER_MONTHLY_LIMIT - remaining;

        if (used < 0)
            used = 0;

        // Cache for 5 minutes
        _cache.Set(cacheKey, used, TimeSpan.FromMinutes(5));

        return used;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the remaining vCore seconds in the monthly free tier allocation.
    /// </summary>
    /// <returns>The number of vCore seconds remaining in the free tier (0 if exceeded).</returns>
    /// <exception cref="Azure.RequestFailedException">
    /// Thrown when the Azure Monitor API request fails.
    /// </exception>
    /// <remarks>
    /// This method first attempts to query the Azure Monitor Metrics REST endpoint
    /// for <c>free_amount_remaining</c> using a 15-minute time grain. If that call
    /// returns no datapoints or fails, it falls back to the SDK-based
    /// <see cref="MetricsClient"/> implementation.
    /// <para>
    /// <c>free_amount_remaining</c> is the number of vCore seconds remaining
    /// in the current calendar month and is monotonically non-increasing.
    /// By taking the minimum value across the time range, we obtain the
    /// most up-to-date remaining free allowance.
    /// </para>
    /// If no datapoints are returned by either mechanism (for example, a brand
    /// new database with zero usage), this method returns
    /// <see cref="FREE_TIER_MONTHLY_LIMIT"/> (full free allowance remaining).
    /// </remarks>
    /// <example>
    /// <code>
    /// var remaining = await service.GetRemainingFreeTierVCoreSecondsAsync();
    /// Console.WriteLine($"Remaining: {remaining:N0} vCore seconds");
    /// </code>
    /// </example>
    /// <seealso cref="GetUsedVCoreSecondsThisMonthAsync"/>
    public async Task<double> GetRemainingFreeTierVCoreSecondsAsync()
    {
        #region implementation

        var cacheKey = $"{CACHE_KEY_PREFIX}RemainingFree_{DateTime.UtcNow:yyyyMMdd_HH}";

        // Check cache first to avoid excessive API calls
        if (_cache.TryGetValue(cacheKey, out double cachedRemaining))
        {
            return cachedRemaining;
        }

        // Time range: 1st of the current month to now (UTC)
        var now = DateTime.UtcNow;
        var startTime = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var endTime = DateTimeOffset.UtcNow;

        double remaining;

        // 1. Try REST endpoint first (management.azure.com)
        var restRemaining = await getRemainingFreeTierViaRestAsync(startTime, endTime);

        if (restRemaining.HasValue)
        {
            remaining = restRemaining.Value;
        }
        else
        {
            // 2. Fall back to SDK-based MetricsClient implementation
            remaining = await getRemainingFreeTierViaMetricsClientAsync(startTime, endTime);
        }

        // Cache for 5 minutes
        _cache.Set(cacheKey, remaining, TimeSpan.FromMinutes(5));

        return remaining;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Calculates the projected monthly cost based on current usage patterns.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// - projectedMonthlyUsage: Estimated total vCore seconds for the month
    /// - projectedCost: Estimated cost in USD for overage
    /// - daysElapsed: Number of days elapsed in the current month
    /// </returns>
    /// <remarks>
    /// Calculates projected cost by:
    /// 1. Determining current daily average usage
    /// 2. Projecting to end of month
    /// 3. Subtracting free tier (100,000 vCore seconds)
    /// 4. Multiplying overage by $0.000145/vCore-second
    /// 
    /// This is an estimate and actual costs may vary based on usage patterns.
    /// </remarks>
    /// <example>
    /// <code>
    /// var (projected, cost, days) = await service.GetProjectedMonthlyCostAsync();
    /// Console.WriteLine($"After {days} days, projected cost: ${cost:F2}");
    /// </code>
    /// </example>
    public async Task<(double projectedMonthlyUsage, double projectedCost, int daysElapsed)> GetProjectedMonthlyCostAsync()
    {
        #region implementation

        var used = await GetUsedVCoreSecondsThisMonthAsync();

        // Calculate days elapsed and days in month
        var now = DateTime.UtcNow;
        var daysElapsed = now.Day;
        var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);

        // Calculate daily average and project to end of month
        var dailyAverage = used / daysElapsed;
        var projectedMonthlyUsage = dailyAverage * daysInMonth;

        // Calculate overage and cost
        var overage = projectedMonthlyUsage - FREE_TIER_MONTHLY_LIMIT;
        var projectedCost = overage > 0 ? overage * 0.000145 : 0.0;

        return (projectedMonthlyUsage, projectedCost, daysElapsed);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Determines if throttling should be applied based on current budget consumption.
    /// </summary>
    /// <returns>
    /// A tuple containing:
    /// - shouldThrottle: True if throttling should be applied (Warning level or above)
    /// - throttleLevel: The current <see cref="ThrottleLevel"/> based on configured thresholds
    /// - percentUsed: Current percentage of free tier consumed
    /// </returns>
    /// <remarks>
    /// Throttle levels are determined by configurable thresholds in `appsettings.json`:
    /// 
    /// ```json
    /// {
    ///   "DatabaseUsageMonitor": {
    ///     "WarningThreshold": 70,
    ///     "ModerateThreshold": 80,
    ///     "AggressiveThreshold": 90,
    ///     "CriticalThreshold": 95,
    ///     "MaxMonthlyCostPercent": 110
    ///   }
    /// }
    /// ```
    /// 
    /// | Level | Default Threshold | Recommended Action |
    /// |-------|-------------------|-------------------|
    /// | None | &lt; 70% | Normal operations |
    /// | Warning | 70-80% | Consider reducing non-essential operations |
    /// | Moderate | 80-90% | Rate-limit non-critical operations |
    /// | Aggressive | 90-95% | Only essential operations |
    /// | Critical | 95-100% | Block non-critical operations |
    /// | CostLimit | &gt; MaxMonthlyCostPercent | Block all except critical |
    /// </remarks>
    /// <example>
    /// ```csharp
    /// var (shouldThrottle, level, percent) = await service.ShouldThrottleAsync();
    /// 
    /// if (level == ThrottleLevel.CostLimit)
    /// {
    ///     // Only allow critical operations
    /// }
    /// else if (level >= ThrottleLevel.Aggressive)
    /// {
    ///     // Apply strict rate limiting
    /// }
    /// ```
    /// </example>
    /// <seealso cref="ThrottleLevel"/>
    /// <seealso cref="GetFreeTierStatusAsync"/>
    public async Task<(bool shouldThrottle, ThrottleLevel throttleLevel, double percentUsed)> ShouldThrottleAsync()
    {
        #region implementation

        var (_, _, percentUsed) = await GetFreeTierStatusAsync();

        // Determine throttle level based on configured thresholds
        ThrottleLevel level;

        if (percentUsed >= _maxMonthlyCostPercent)
        {
            level = ThrottleLevel.CostLimit;
        }
        else if (percentUsed >= _criticalThreshold)
        {
            level = ThrottleLevel.Critical;
        }
        else if (percentUsed >= _aggressiveThreshold)
        {
            level = ThrottleLevel.Aggressive;
        }
        else if (percentUsed >= _moderateThreshold)
        {
            level = ThrottleLevel.Moderate;
        }
        else if (percentUsed >= _warningThreshold)
        {
            level = ThrottleLevel.Warning;
        }
        else
        {
            level = ThrottleLevel.None;
        }

        // Throttling is active at Warning level or above
        var shouldThrottle = level >= ThrottleLevel.Warning;

        return (shouldThrottle, level, percentUsed);

        #endregion
    }

    #endregion
}
