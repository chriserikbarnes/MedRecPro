using Microsoft.Extensions.Caching.Memory;
using Const = MedRecPro.Models.Constant;

namespace MedRecPro.Service;

/**************************************************************/
/// <summary>
/// Background hosted service that periodically monitors Azure SQL Database free tier usage
/// and updates the application's throttle state accordingly.
/// </summary>
/// <remarks>
/// This service runs as a singleton hosted service and performs the following:
/// <list type="bullet">
///   <item>Polls Azure Monitor metrics at a configurable interval (default: 2 hours)</item>
///   <item>Updates the shared <see cref="IThrottleStateService"/> with current usage</item>
///   <item>Logs warnings when usage approaches or exceeds thresholds</item>
///   <item>Implements exponential backoff on repeated failures</item>
/// </list>
/// 
/// The service uses <see cref="IServiceScopeFactory"/> to create scoped instances of
/// <see cref="AzureSqlMetricsService"/> for each polling cycle, ensuring proper
/// resource cleanup.
/// 
/// Configuration options (in appsettings.json):
/// <code>
/// {
///   "DatabaseUsageMonitor": {
///     "Enabled": true,
///     "PollingIntervalHours": 2,
///     "InitialDelaySeconds": 60,
///     "MaxConsecutiveFailures": 5
///   }
/// }
/// </code>
/// </remarks>
/// <example>
/// <code>
/// // In Program.cs
/// builder.Services.AddSingleton&lt;IThrottleStateService, ThrottleStateService&gt;();
/// builder.Services.AddHostedService&lt;DatabaseUsageMonitorService&gt;();
/// </code>
/// </example>
/// <seealso cref="IHostedService"/>
/// <seealso cref="IThrottleStateService"/>
/// <seealso cref="AzureSqlMetricsService"/>
public class DatabaseUsageMonitorService : BackgroundService
{
    #region Fields

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ThrottleStateService _throttleState;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseUsageMonitorService> _logger;

    private readonly bool _enabled;
    private readonly TimeSpan _pollingInterval;
    private readonly TimeSpan _initialDelay;
    private readonly int _maxConsecutiveFailures;

    private int _consecutiveFailures = 0;
    private const double FREE_TIER_MONTHLY_LIMIT = Const.FREE_TIER_MONTHLY_LIMIT;

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseUsageMonitorService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory for creating service scopes to resolve scoped services.</param>
    /// <param name="throttleState">The shared throttle state service to update.</param>
    /// <param name="configuration">Application configuration for monitor settings.</param>
    /// <param name="logger">Logger for recording monitoring events and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <remarks>
    /// The service reads configuration from the "DatabaseUsageMonitor" section:
    /// <list type="bullet">
    ///   <item><c>Enabled</c>: Whether monitoring is active (default: true)</item>
    ///   <item><c>PollingIntervalHours</c>: Hours between polls (default: 2)</item>
    ///   <item><c>InitialDelaySeconds</c>: Startup delay in seconds (default: 60)</item>
    ///   <item><c>MaxConsecutiveFailures</c>: Failures before pausing (default: 5)</item>
    /// </list>
    /// </remarks>
    /// <seealso cref="IServiceScopeFactory"/>
    /// <seealso cref="ThrottleStateService"/>
    /// <seealso cref="IConfiguration"/>
    public DatabaseUsageMonitorService(
        IServiceScopeFactory scopeFactory,
        ThrottleStateService throttleState,
        IConfiguration configuration,
        ILogger<DatabaseUsageMonitorService> logger)
    {
        #region implementation

        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _throttleState = throttleState ?? throw new ArgumentNullException(nameof(throttleState));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load configuration with defaults
        var section = configuration.GetSection("DatabaseUsageMonitor");

        _enabled = section.GetValue("Enabled", true);

        var pollingHours = section.GetValue("PollingIntervalHours", 2);
        _pollingInterval = TimeSpan.FromHours(Math.Max(pollingHours, 0.5)); // Minimum 30 minutes

        var initialDelaySeconds = section.GetValue("InitialDelaySeconds", 60);
        _initialDelay = TimeSpan.FromSeconds(Math.Max(initialDelaySeconds, 10)); // Minimum 10 seconds

        _maxConsecutiveFailures = section.GetValue("MaxConsecutiveFailures", 5);

        _logger.LogInformation(
            "DatabaseUsageMonitorService configured: Enabled={Enabled}, Interval={Interval}, InitialDelay={Delay}",
            _enabled,
            _pollingInterval,
            _initialDelay);

        #endregion
    }

    #endregion

    #region BackgroundService Implementation

    /**************************************************************/
    /// <summary>
    /// Executes the background monitoring loop.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token triggered when the host is stopping.</param>
    /// <returns>A task representing the background operation.</returns>
    /// <remarks>
    /// The monitoring loop:
    /// <list type="number">
    ///   <item>Waits for the initial delay to allow application startup</item>
    ///   <item>Polls metrics and updates throttle state</item>
    ///   <item>Waits for the polling interval before the next cycle</item>
    ///   <item>Implements exponential backoff on consecutive failures</item>
    /// </list>
    /// 
    /// If monitoring is disabled via configuration, the method returns immediately.
    /// </remarks>
    /// <seealso cref="BackgroundService.ExecuteAsync"/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        #region implementation

        if (!_enabled)
        {
            _logger.LogInformation("DatabaseUsageMonitorService is disabled via configuration.");
            return;
        }

        _logger.LogInformation(
            "DatabaseUsageMonitorService starting. Initial delay: {Delay}",
            _initialDelay);

        // Wait for initial delay to allow application to fully start
        try
        {
            await Task.Delay(_initialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _logger.LogInformation("DatabaseUsageMonitorService entering monitoring loop.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await pollAndUpdateStateAsync(stoppingToken);

                // Reset failure counter on success
                _consecutiveFailures = 0;

                // Wait for the next polling interval
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;

                _logger.LogError(
                    ex,
                    "DatabaseUsageMonitorService polling failed (attempt {Attempts}/{Max})",
                    _consecutiveFailures,
                    _maxConsecutiveFailures);

                if (_consecutiveFailures >= _maxConsecutiveFailures)
                {
                    _throttleState.SetMonitoringInactive();

                    _logger.LogCritical(
                        "DatabaseUsageMonitorService exceeded max failures ({Max}). " +
                        "Monitoring marked inactive. Will continue retrying with extended backoff.",
                        _maxConsecutiveFailures);
                }

                // Exponential backoff: min(pollingInterval * 2^failures, 24 hours)
                var backoffMultiplier = Math.Pow(2, Math.Min(_consecutiveFailures, 4));
                var backoffDelay = TimeSpan.FromTicks(
                    (long)Math.Min(
                        _pollingInterval.Ticks * backoffMultiplier,
                        TimeSpan.FromHours(24).Ticks));

                _logger.LogWarning(
                    "Backing off for {Backoff} before next polling attempt.",
                    backoffDelay);

                try
                {
                    await Task.Delay(backoffDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("DatabaseUsageMonitorService stopped.");

        #endregion
    }

    #endregion

    #region Private Methods

    /**************************************************************/
    /// <summary>
    /// Polls Azure Monitor metrics and updates the throttle state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the polling operation.</returns>
    /// <remarks>
    /// Creates a new service scope to resolve <see cref="AzureSqlMetricsService"/>,
    /// queries the current free tier status, and updates <see cref="ThrottleStateService"/>.
    /// 
    /// This method also logs informational messages about current usage levels
    /// and warnings when approaching critical thresholds.
    /// </remarks>
    /// <seealso cref="AzureSqlMetricsService.GetFreeTierStatusAsync"/>
    /// <seealso cref="ThrottleStateService.UpdateState"/>
    private async Task pollAndUpdateStateAsync(CancellationToken cancellationToken)
    {
        #region implementation

        _logger.LogDebug("Polling Azure SQL metrics...");

        // Create a scope to resolve the scoped AzureSqlMetricsService
        using var scope = _scopeFactory.CreateScope();

        var metricsService = scope.ServiceProvider.GetRequiredService<AzureSqlMetricsService>();

        // Get current free tier status
        var (used, remaining, percentUsed) = await metricsService.GetFreeTierStatusAsync();

        // Update the shared throttle state
        _throttleState.UpdateState(percentUsed, remaining);

        // Log current state
        _logger.LogInformation(
            "Database usage poll complete. Used: {Used:N0} vCore-sec ({Percent:F1}%), " +
            "Remaining: {Remaining:N0} vCore-sec",
            used,
            percentUsed,
            remaining);

        // Additional warnings for high usage
        if (percentUsed >= 95)
        {
            _logger.LogCritical(
                "CRITICAL: Database free tier is {Percent:F1}% consumed! " +
                "Only {Remaining:N0} vCore seconds remain. " +
                "Consider upgrading or reducing usage immediately.",
                percentUsed,
                remaining);
        }
        else if (percentUsed >= 90)
        {
            _logger.LogError(
                "WARNING: Database free tier is {Percent:F1}% consumed. " +
                "Aggressive throttling is recommended.",
                percentUsed);
        }
        else if (percentUsed >= 80)
        {
            _logger.LogWarning(
                "NOTICE: Database free tier is {Percent:F1}% consumed. " +
                "Consider enabling rate limiting for non-critical operations.",
                percentUsed);
        }

        #endregion
    }

    #endregion
}

/**************************************************************/
/// <summary>
/// Extension methods for registering database usage monitoring services.
/// </summary>
/// <remarks>
/// Provides a convenient way to register all required services for
/// database usage monitoring and throttling in the dependency injection container.
/// </remarks>
/// <seealso cref="IThrottleStateService"/>
/// <seealso cref="ThrottleStateService"/>
/// <seealso cref="DatabaseUsageMonitorService"/>
public static class DatabaseUsageMonitorExtensions
{
    /**************************************************************/
    /// <summary>
    /// Adds database usage monitoring and throttling services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method registers:
    /// <list type="bullet">
    ///   <item><see cref="ThrottleStateService"/> as a singleton implementing <see cref="IThrottleStateService"/></item>
    ///   <item><see cref="DatabaseUsageMonitorService"/> as a hosted background service</item>
    /// </list>
    /// 
    /// Prerequisites (must be registered before calling this method):
    /// <list type="bullet">
    ///   <item><see cref="AzureManagementTokenProvider"/> (singleton)</item>
    ///   <item><see cref="AzureSqlMetricsService"/> (scoped)</item>
    ///   <item><see cref="IMemoryCache"/> (typically via AddMemoryCache)</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // In Program.cs
    /// builder.Services.AddMemoryCache();
    /// builder.Services.AddSingleton&lt;AzureManagementTokenProvider&gt;();
    /// builder.Services.AddScoped&lt;AzureSqlMetricsService&gt;();
    /// builder.Services.AddDatabaseUsageMonitoring(); // Adds throttle state and monitor
    /// </code>
    /// </example>
    /// <seealso cref="IServiceCollection"/>
    public static IServiceCollection AddDatabaseUsageMonitoring(this IServiceCollection services)
    {
        #region implementation

        // Register the throttle state service as a singleton
        // Note: We register the concrete type AND the interface pointing to the same instance
        services.AddSingleton<ThrottleStateService>();
        services.AddSingleton<IThrottleStateService>(sp => sp.GetRequiredService<ThrottleStateService>());

        // Register the background monitoring service
        services.AddHostedService<DatabaseUsageMonitorService>();

        return services;

        #endregion
    }
}
