using Azure.Monitor.Query.Metrics;
using Azure.Monitor.Query.Metrics.Models;
using Azure.Identity;
using Azure.Core;
using Microsoft.Extensions.Caching.Memory;

namespace MedRecPro.Service;

/**************************************************************/
/// <summary>
/// Service for querying Azure SQL Database metrics using Azure Monitor Query Metrics API.
/// Tracks vCore usage against the monthly free tier limit (100,000 vCore seconds).
/// </summary>
/// <remarks>
/// This service uses the Azure.Monitor.Query.Metrics library to query serverless database
/// vCore usage. Results are cached to minimize API calls and reduce overhead.
/// The free tier provides 100,000 vCore seconds per month per database.
/// </remarks>
/// <example>
/// <code>
/// var service = new AzureSqlMetricsService(configuration, cache);
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
public class AzureSqlMetricsService
{
    #region Fields

    private readonly MetricsClient _metricsClient;
    private readonly string _resourceId;
    private readonly IMemoryCache _cache;
    private const double FREE_TIER_MONTHLY_LIMIT = 100000.0;
    private const string VCORE_METRIC_NAME = "serverless_database_vcore_seconds_used";
    private const string CACHE_KEY_PREFIX = "AzureSqlMetrics_";

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureSqlMetricsService"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration containing Azure resource settings.</param>
    /// <param name="cache">Memory cache for storing metric query results.</param>
    /// <exception cref="ArgumentNullException">Thrown when configuration or cache is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when required configuration values are missing.</exception>
    /// <remarks>
    /// The service requires the following configuration values:
    /// - Azure:SqlDatabase:ResourceId - Full Azure resource ID
    /// - Azure:SqlDatabase:MetricsRegion - Azure region (e.g., "eastus", "westus3")
    /// </remarks>
    /// <seealso cref="MetricsClient"/>
    /// <seealso cref="DefaultAzureCredential"/>
    public AzureSqlMetricsService(IConfiguration configuration, IMemoryCache cache)
    {
        #region implementation

        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        if (cache == null)
            throw new ArgumentNullException(nameof(cache));

        _cache = cache;

        // Retrieve resource ID from configuration
        _resourceId = configuration["Azure:SqlDatabase:ResourceId"]
            ?? throw new InvalidOperationException("Azure:SqlDatabase:ResourceId configuration is missing");

        // Retrieve metrics region from configuration
        var region = configuration["Azure:SqlDatabase:MetricsRegion"]
            ?? throw new InvalidOperationException("Azure:SqlDatabase:MetricsRegion configuration is missing");

        // Initialize MetricsClient with regional endpoint
        var endpoint = new Uri($"https://{region}.metrics.monitor.azure.com");
        _metricsClient = new MetricsClient(endpoint, new DefaultAzureCredential());

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
    /// Gets the remaining vCore seconds in the monthly free tier allocation.
    /// </summary>
    /// <returns>The number of vCore seconds remaining in the free tier (0 if exceeded).</returns>
    /// <exception cref="Azure.RequestFailedException">Thrown when the Azure Monitor API request fails.</exception>
    /// <remarks>
    /// This is a convenience method that calculates: FREE_TIER_MONTHLY_LIMIT - used.
    /// If the free tier has been exceeded, returns 0.
    /// </remarks>
    /// <seealso cref="GetUsedVCoreSecondsThisMonthAsync"/>
    public async Task<double> GetRemainingFreeTierVCoreSecondsAsync()
    {
        #region implementation

        var used = await GetUsedVCoreSecondsThisMonthAsync();
        var remaining = FREE_TIER_MONTHLY_LIMIT - used;

        return remaining > 0 ? remaining : 0;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the total vCore seconds used in the current calendar month.
    /// </summary>
    /// <returns>Total vCore seconds consumed from the 1st of the month to now.</returns>
    /// <exception cref="Azure.RequestFailedException">Thrown when the Azure Monitor API request fails.</exception>
    /// <remarks>
    /// Queries the Azure Monitor Metrics API for the "serverless_database_vcore_seconds_used" metric.
    /// Aggregates all timeseries data points from the start of the month to the current time.
    /// Results are cached for 5 minutes to reduce API calls.
    /// </remarks>
    /// <example>
    /// <code>
    /// var used = await service.GetUsedVCoreSecondsThisMonthAsync();
    /// Console.WriteLine($"Used this month: {used:N0} vCore seconds");
    /// </code>
    /// </example>
    public async Task<double> GetUsedVCoreSecondsThisMonthAsync()
    {
        #region implementation

        var cacheKey = $"{CACHE_KEY_PREFIX}MonthlyUsage_{DateTime.UtcNow:yyyyMMdd_HH}";

        // Check cache first
        if (_cache.TryGetValue(cacheKey, out double cachedUsage))
        {
            return cachedUsage;
        }

        // Calculate time range: 1st of current month to now
        var startTime = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var endTime = DateTimeOffset.UtcNow;

        // Configure query options with string-based aggregations
        var options = new MetricsQueryResourcesOptions
        {
            StartTime = startTime,
            EndTime = endTime,
            Granularity = TimeSpan.FromHours(1) // Hourly granularity
        };

        // Add Total aggregation to the list
        options.Aggregations.Add("Total");

        // Query the metrics
        var response = await _metricsClient.QueryResourcesAsync(
            resourceIds: new List<ResourceIdentifier> { new ResourceIdentifier(_resourceId) },
            metricNames: new List<string> { VCORE_METRIC_NAME },
            metricNamespace: "Microsoft.Sql/servers/databases",
            options: options);

        var metricsResult = response.Value;

        // Aggregate all data points
        double totalUsed = 0.0;

        foreach (var queryResult in metricsResult.Values)
        {
            foreach (var metric in queryResult.Metrics)
            {
                foreach (var timeSeries in metric.TimeSeries)
                {
                    foreach (var dataPoint in timeSeries.Values)
                    {
                        // Sum the Total aggregation values
                        if (dataPoint.Total.HasValue)
                        {
                            totalUsed += dataPoint.Total.Value;
                        }
                    }
                }
            }
        }

        // Cache for 5 minutes
        _cache.Set(cacheKey, totalUsed, TimeSpan.FromMinutes(5));

        return totalUsed;


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
    /// <param name="aggressiveThreshold">Percentage threshold for aggressive throttling (default: 90%).</param>
    /// <param name="warningThreshold">Percentage threshold for warning state (default: 80%).</param>
    /// <returns>
    /// A tuple containing:
    /// - shouldThrottle: True if throttling should be applied
    /// - throttleLevel: "None", "Warning", or "Aggressive"
    /// - percentUsed: Current percentage of free tier consumed
    /// </returns>
    /// <remarks>
    /// Throttle levels:
    /// - None: Below warning threshold, no action needed
    /// - Warning: 80-90% used, consider rate limiting expensive operations
    /// - Aggressive: Above 90% used, apply strict rate limits
    /// </remarks>
    /// <example>
    /// <code>
    /// var (shouldThrottle, level, percent) = await service.ShouldThrottleAsync();
    /// 
    /// if (level == "Aggressive")
    /// {
    ///     // Apply strict rate limiting
    /// }
    /// </code>
    /// </example>
    public async Task<(bool shouldThrottle, string throttleLevel, double percentUsed)> ShouldThrottleAsync(
        double aggressiveThreshold = 90.0,
        double warningThreshold = 80.0)
    {
        #region implementation

        var (_, _, percentUsed) = await GetFreeTierStatusAsync();

        if (percentUsed >= aggressiveThreshold)
        {
            return (true, "Aggressive", percentUsed);
        }

        if (percentUsed >= warningThreshold)
        {
            return (true, "Warning", percentUsed);
        }

        return (false, "None", percentUsed);

        #endregion
    }

    #endregion
}