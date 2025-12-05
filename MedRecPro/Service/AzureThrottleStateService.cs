using System;
using System.Threading;

namespace MedRecPro.Service;

#region Throttle State Enums and EventArgs

/**************************************************************/
/// <summary>
/// Defines the throttle levels for database usage based on free tier consumption.
/// </summary>
/// <remarks>
/// Throttle levels are determined by the percentage of the monthly free tier (100,000 vCore seconds)
/// that has been consumed. Higher levels indicate more aggressive throttling of database operations.
/// 
/// ### Level Thresholds (Default)
/// 
/// | Level | Percent Used | Description |
/// |-------|--------------|-------------|
/// | None | &lt; 70% | Normal operations |
/// | Warning | 70-80% | Consider reducing non-essential operations |
/// | Moderate | 80-90% | Rate limit non-critical operations |
/// | Aggressive | 90-95% | Only essential operations |
/// | Critical | 95-100% | Block non-critical, delay others |
/// | CostLimit | &gt; MaxCostPercent | Exceeds cost limit, block all non-critical |
/// </remarks>
/// <seealso cref="IThrottleStateService"/>
/// <seealso cref="ThrottleStateService"/>
public enum ThrottleLevel
{
    #region implementation

    /// <summary>
    /// No throttling required. Usage is within normal limits (below 70% of free tier).
    /// </summary>
    None = 0,

    /// <summary>
    /// Warning level. Usage is between 70-80% of free tier.
    /// Consider reducing non-essential database operations.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Moderate throttling. Usage is between 80-90% of free tier.
    /// Non-critical operations should be delayed or rate-limited.
    /// </summary>
    Moderate = 2,

    /// <summary>
    /// Aggressive throttling. Usage is between 90-95% of free tier.
    /// Only essential operations should proceed without delay.
    /// </summary>
    Aggressive = 3,

    /// <summary>
    /// Critical level. Usage is between 95-100% of free tier.
    /// All non-critical database operations should be blocked.
    /// </summary>
    Critical = 4,

    /// <summary>
    /// Cost limit exceeded. Usage exceeds the configured maximum monthly cost threshold.
    /// All operations except authentication and critical reads should be blocked.
    /// This level protects against runaway costs when the free tier is exhausted.
    /// </summary>
    CostLimit = 5

    #endregion
}

/**************************************************************/
/// <summary>
/// Event arguments for throttle state change notifications.
/// </summary>
/// <remarks>
/// Provides details about the previous and current throttle states when
/// the state changes, allowing subscribers to react appropriately.
/// </remarks>
/// <seealso cref="ThrottleStateService"/>
/// <seealso cref="IThrottleStateService.ThrottleStateChanged"/>
public class ThrottleStateChangedEventArgs : EventArgs
{
    #region Properties

    /**************************************************************/
    /// <summary>
    /// Gets the previous throttle level before the state change.
    /// </summary>
    /// <seealso cref="ThrottleLevel"/>
    public ThrottleLevel PreviousLevel { get; }

    /**************************************************************/
    /// <summary>
    /// Gets the new (current) throttle level after the state change.
    /// </summary>
    /// <seealso cref="ThrottleLevel"/>
    public ThrottleLevel CurrentLevel { get; }

    /**************************************************************/
    /// <summary>
    /// Gets the percentage of the free tier that has been consumed.
    /// </summary>
    /// <remarks>
    /// Value ranges from 0 to 100+ (can exceed 100 if free tier is exhausted).
    /// </remarks>
    public double PercentUsed { get; }

    /**************************************************************/
    /// <summary>
    /// Gets the remaining vCore seconds in the free tier allocation.
    /// </summary>
    /// <remarks>
    /// This value will be 0 or negative if the free tier has been exhausted.
    /// </remarks>
    public double RemainingVCoreSeconds { get; }

    /**************************************************************/
    /// <summary>
    /// Gets the timestamp when this state change occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /**************************************************************/
    /// <summary>
    /// Gets a value indicating whether the cost limit has been exceeded.
    /// </summary>
    /// <remarks>
    /// True when <see cref="CurrentLevel"/> is <see cref="ThrottleLevel.CostLimit"/>.
    /// </remarks>
    public bool CostLimitExceeded => CurrentLevel == ThrottleLevel.CostLimit;

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="ThrottleStateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="previousLevel">The previous throttle level.</param>
    /// <param name="currentLevel">The new throttle level.</param>
    /// <param name="percentUsed">The percentage of free tier consumed.</param>
    /// <param name="remainingVCoreSeconds">The remaining vCore seconds.</param>
    /// <seealso cref="ThrottleLevel"/>
    public ThrottleStateChangedEventArgs(
        ThrottleLevel previousLevel,
        ThrottleLevel currentLevel,
        double percentUsed,
        double remainingVCoreSeconds)
    {
        #region implementation

        PreviousLevel = previousLevel;
        CurrentLevel = currentLevel;
        PercentUsed = percentUsed;
        RemainingVCoreSeconds = remainingVCoreSeconds;
        Timestamp = DateTimeOffset.UtcNow;

        #endregion
    }

    #endregion
}

#endregion

#region IThrottleStateService Interface

/**************************************************************/
/// <summary>
/// Provides read-only access to the current database throttle state.
/// </summary>
/// <remarks>
/// This interface allows services to check the current throttle level and decide
/// whether to proceed with database operations. The state is updated by the
/// <see cref="DatabaseUsageMonitorService"/> background service.
/// 
/// ### Usage Patterns
/// 
/// **Check before expensive operations:**
/// ```csharp
/// if (_throttleState.ShouldBlockNonCriticalOperations)
/// {
///     throw new ServiceUnavailableException("Database throttling is active.");
/// }
/// ```
/// 
/// **Check cost limit:**
/// ```csharp
/// if (_throttleState.IsCostLimitExceeded)
/// {
///     // Only allow critical operations
/// }
/// ```
/// </remarks>
/// <example>
/// ```csharp
/// public class MyService
/// {
///     private readonly IThrottleStateService _throttleState;
///     
///     public async Task PerformExpensiveOperationAsync()
///     {
///         if (_throttleState.ShouldBlockNonCriticalOperations)
///         {
///             throw new ServiceUnavailableException("Database throttling is active.");
///         }
///         
///         // Proceed with operation
///     }
/// }
/// ```
/// </example>
/// <seealso cref="ThrottleStateService"/>
/// <seealso cref="ThrottleLevel"/>
public interface IThrottleStateService
{
    #region Properties

    /**************************************************************/
    /// <summary>
    /// Gets the current throttle level based on database usage.
    /// </summary>
    /// <seealso cref="ThrottleLevel"/>
    ThrottleLevel CurrentLevel { get; }

    /**************************************************************/
    /// <summary>
    /// Gets the percentage of the monthly free tier that has been consumed.
    /// </summary>
    /// <remarks>
    /// Value ranges from 0 to 100+ (can exceed 100 if free tier is exhausted).
    /// </remarks>
    double PercentUsed { get; }

    /**************************************************************/
    /// <summary>
    /// Gets the remaining vCore seconds in the free tier allocation.
    /// </summary>
    double RemainingVCoreSeconds { get; }

    /**************************************************************/
    /// <summary>
    /// Gets the timestamp of the last successful metrics update.
    /// </summary>
    /// <remarks>
    /// Returns `null` if no update has occurred yet.
    /// </remarks>
    DateTimeOffset? LastUpdated { get; }

    /**************************************************************/
    /// <summary>
    /// Gets a value indicating whether the throttle state monitoring is active.
    /// </summary>
    /// <remarks>
    /// This may be `false` if the background service hasn't started
    /// or if metrics collection has failed.
    /// </remarks>
    bool IsMonitoringActive { get; }

    /**************************************************************/
    /// <summary>
    /// Gets a value indicating whether non-critical operations should be blocked.
    /// </summary>
    /// <remarks>
    /// Returns `true` when throttle level is <see cref="ThrottleLevel.Critical"/>,
    /// <see cref="ThrottleLevel.Aggressive"/>, or <see cref="ThrottleLevel.CostLimit"/>.
    /// </remarks>
    bool ShouldBlockNonCriticalOperations { get; }

    /**************************************************************/
    /// <summary>
    /// Gets a value indicating whether operations should be rate-limited.
    /// </summary>
    /// <remarks>
    /// Returns `true` when throttle level is <see cref="ThrottleLevel.Moderate"/> or higher.
    /// </remarks>
    bool ShouldRateLimitOperations { get; }

    /**************************************************************/
    /// <summary>
    /// Gets a value indicating whether the cost limit has been exceeded.
    /// </summary>
    /// <remarks>
    /// Returns `true` when usage exceeds the configured maximum monthly cost threshold
    /// (default: 110% of free tier, i.e., 10% into paid usage).
    /// 
    /// When this is true, only critical operations should be allowed to proceed.
    /// </remarks>
    bool IsCostLimitExceeded { get; }

    /**************************************************************/
    /// <summary>
    /// Gets the configured maximum monthly cost threshold as a percentage.
    /// </summary>
    /// <remarks>
    /// Default is 110 (meaning 110% of free tier = 10% overage allowed).
    /// This can be configured via `DatabaseUsageMonitor:MaxMonthlyCostPercent` in appsettings.
    /// </remarks>
    double MaxMonthlyCostPercent { get; }

    /**************************************************************/
    /// <summary>
    /// Gets the estimated monthly cost in USD based on current usage.
    /// </summary>
    /// <remarks>
    /// Calculated as overage vCore seconds × $0.000145 per vCore-second.
    /// Returns 0 if within the free tier.
    /// </remarks>
    double EstimatedMonthlyCost { get; }

    #endregion

    #region Events

    /**************************************************************/
    /// <summary>
    /// Occurs when the throttle state changes.
    /// </summary>
    /// <remarks>
    /// Subscribe to this event to receive notifications when the throttle level
    /// changes, allowing for proactive response to usage changes.
    /// </remarks>
    /// <seealso cref="ThrottleStateChangedEventArgs"/>
    event EventHandler<ThrottleStateChangedEventArgs>? ThrottleStateChanged;

    #endregion

    #region Methods

    /**************************************************************/
    /// <summary>
    /// Gets a human-readable description of the current throttle state.
    /// </summary>
    /// <returns>A string describing the current state and recommended actions.</returns>
    /// <example>
    /// ```csharp
    /// var description = _throttleState.GetStateDescription();
    /// // Returns: "Aggressive: 92.5% of free tier consumed (7,500 vCore seconds remaining). Only essential operations recommended."
    /// ```
    /// </example>
    string GetStateDescription();

    #endregion
}

#endregion

#region ThrottleStateService Implementation

/**************************************************************/
/// <summary>
/// Thread-safe singleton service for managing database throttle state.
/// </summary>
/// <remarks>
/// This service maintains the current throttle state based on Azure SQL Database
/// free tier usage. The state is updated by <see cref="DatabaseUsageMonitorService"/>
/// and can be queried by any service to determine if database operations should proceed.
/// 
/// The service uses <see cref="ReaderWriterLockSlim"/> for thread-safe access to state,
/// ensuring high read throughput while allowing atomic updates.
/// 
/// ### Configuration
/// 
/// Configure in `appsettings.json`:
/// 
/// ```json
/// {
///   "DatabaseUsageMonitor": {
///     "MaxMonthlyCostPercent": 110,
///     "WarningThreshold": 70,
///     "ModerateThreshold": 80,
///     "AggressiveThreshold": 90,
///     "CriticalThreshold": 95
///   }
/// }
/// ```
/// </remarks>
/// <example>
/// ```csharp
/// // In Program.cs
/// builder.Services.AddSingleton&lt;IThrottleStateService, ThrottleStateService&gt;();
/// 
/// // In a controller or service
/// if (_throttleState.ShouldBlockNonCriticalOperations)
/// {
///     return StatusCode(503, "Database throttling active");
/// }
/// ```
/// </example>
/// <seealso cref="IThrottleStateService"/>
/// <seealso cref="DatabaseUsageMonitorService"/>
/// <seealso cref="ThrottleLevel"/>
public class ThrottleStateService : IThrottleStateService, IDisposable
{
    #region Constants

    /// <summary>
    /// Cost per vCore-second for Azure SQL Database serverless beyond free tier.
    /// </summary>
    private const double CostPerVCoreSecond = 0.000145;

    /// <summary>
    /// Free tier monthly limit in vCore seconds.
    /// </summary>
    private const double FreeTierLimit = 100_000;

    #endregion

    #region Fields

    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly ILogger<ThrottleStateService> _logger;

    private ThrottleLevel _currentLevel = ThrottleLevel.None;
    private double _percentUsed = 0;
    private double _remainingVCoreSeconds = 100_000; // Default to full free tier
    private DateTimeOffset? _lastUpdated = null;
    private bool _isMonitoringActive = false;
    private bool _disposed = false;

    // Configurable thresholds
    private readonly double _maxMonthlyCostPercent;
    private readonly double _warningThreshold;
    private readonly double _moderateThreshold;
    private readonly double _aggressiveThreshold;
    private readonly double _criticalThreshold;

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="ThrottleStateService"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration for threshold settings.</param>
    /// <param name="logger">Logger for recording state changes and errors.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    /// <remarks>
    /// Configuration options (all optional, with defaults):
    /// 
    /// | Setting | Default | Description |
    /// |---------|---------|-------------|
    /// | MaxMonthlyCostPercent | 110 | Percent at which CostLimit activates |
    /// | WarningThreshold | 70 | Percent for Warning level |
    /// | ModerateThreshold | 80 | Percent for Moderate level |
    /// | AggressiveThreshold | 90 | Percent for Aggressive level |
    /// | CriticalThreshold | 95 | Percent for Critical level |
    /// </remarks>
    /// <seealso cref="ILogger{TCategoryName}"/>
    /// <seealso cref="IConfiguration"/>
    public ThrottleStateService(
        IConfiguration configuration,
        ILogger<ThrottleStateService> logger)
    {
        #region implementation

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load configurable thresholds with sensible defaults
        var section = configuration?.GetSection("DatabaseUsageMonitor");

        _maxMonthlyCostPercent = section?.GetValue("MaxMonthlyCostPercent", 110.0) ?? 110.0;
        _warningThreshold = section?.GetValue("WarningThreshold", 70.0) ?? 70.0;
        _moderateThreshold = section?.GetValue("ModerateThreshold", 80.0) ?? 80.0;
        _aggressiveThreshold = section?.GetValue("AggressiveThreshold", 90.0) ?? 90.0;
        _criticalThreshold = section?.GetValue("CriticalThreshold", 95.0) ?? 95.0;

        _logger.LogInformation(
            "ThrottleStateService initialized. Thresholds - Warning: {Warning}%, Moderate: {Moderate}%, " +
            "Aggressive: {Aggressive}%, Critical: {Critical}%, CostLimit: {CostLimit}%",
            _warningThreshold,
            _moderateThreshold,
            _aggressiveThreshold,
            _criticalThreshold,
            _maxMonthlyCostPercent);

        #endregion
    }

    #endregion

    #region IThrottleStateService Properties

    /**************************************************************/
    /// <inheritdoc/>
    public ThrottleLevel CurrentLevel
    {
        get
        {
            #region implementation

            _lock.EnterReadLock();
            try
            {
                return _currentLevel;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            #endregion
        }
    }

    /**************************************************************/
    /// <inheritdoc/>
    public double PercentUsed
    {
        get
        {
            #region implementation

            _lock.EnterReadLock();
            try
            {
                return _percentUsed;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            #endregion
        }
    }

    /**************************************************************/
    /// <inheritdoc/>
    public double RemainingVCoreSeconds
    {
        get
        {
            #region implementation

            _lock.EnterReadLock();
            try
            {
                return _remainingVCoreSeconds;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            #endregion
        }
    }

    /**************************************************************/
    /// <inheritdoc/>
    public DateTimeOffset? LastUpdated
    {
        get
        {
            #region implementation

            _lock.EnterReadLock();
            try
            {
                return _lastUpdated;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            #endregion
        }
    }

    /**************************************************************/
    /// <inheritdoc/>
    public bool IsMonitoringActive
    {
        get
        {
            #region implementation

            _lock.EnterReadLock();
            try
            {
                return _isMonitoringActive;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            #endregion
        }
    }

    /**************************************************************/
    /// <inheritdoc/>
    public bool ShouldBlockNonCriticalOperations
    {
        get
        {
            #region implementation

            _lock.EnterReadLock();
            try
            {
                // Block when at Aggressive, Critical, or CostLimit level
                return _currentLevel >= ThrottleLevel.Aggressive;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            #endregion
        }
    }

    /**************************************************************/
    /// <inheritdoc/>
    public bool ShouldRateLimitOperations
    {
        get
        {
            #region implementation

            _lock.EnterReadLock();
            try
            {
                // Rate limit at Moderate level or higher
                return _currentLevel >= ThrottleLevel.Moderate;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            #endregion
        }
    }

    /**************************************************************/
    /// <inheritdoc/>
    public bool IsCostLimitExceeded
    {
        get
        {
            #region implementation

            _lock.EnterReadLock();
            try
            {
                return _currentLevel == ThrottleLevel.CostLimit;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            #endregion
        }
    }

    /**************************************************************/
    /// <inheritdoc/>
    public double MaxMonthlyCostPercent => _maxMonthlyCostPercent;

    /**************************************************************/
    /// <inheritdoc/>
    public double EstimatedMonthlyCost
    {
        get
        {
            #region implementation

            _lock.EnterReadLock();
            try
            {
                // Calculate overage beyond free tier
                var usedVCoreSeconds = FreeTierLimit - _remainingVCoreSeconds;
                var overage = usedVCoreSeconds - FreeTierLimit;

                if (overage <= 0)
                    return 0;

                return overage * CostPerVCoreSecond;
            }
            finally
            {
                _lock.ExitReadLock();
            }

            #endregion
        }
    }

    #endregion

    #region IThrottleStateService Events

    /**************************************************************/
    /// <inheritdoc/>
    public event EventHandler<ThrottleStateChangedEventArgs>? ThrottleStateChanged;

    #endregion

    #region IThrottleStateService Methods

    /**************************************************************/
    /// <inheritdoc/>
    public string GetStateDescription()
    {
        #region implementation

        _lock.EnterReadLock();
        try
        {
            var levelDescription = _currentLevel switch
            {
                ThrottleLevel.None => "Normal operations permitted.",
                ThrottleLevel.Warning => "Consider reducing non-essential operations.",
                ThrottleLevel.Moderate => "Non-critical operations should be rate-limited.",
                ThrottleLevel.Aggressive => "Only essential operations recommended.",
                ThrottleLevel.Critical => "Critical: All non-essential operations should be blocked.",
                ThrottleLevel.CostLimit => $"COST LIMIT: Usage exceeds {_maxMonthlyCostPercent}% threshold. Only critical operations allowed.",
                _ => "Unknown state."
            };

            var costInfo = _percentUsed > 100
                ? $" Estimated cost: ${EstimatedMonthlyCost:F2}"
                : "";

            return $"{_currentLevel}: {_percentUsed:F1}% of free tier consumed " +
                   $"({_remainingVCoreSeconds:N0} vCore seconds remaining). {levelDescription}{costInfo}";
        }
        finally
        {
            _lock.ExitReadLock();
        }

        #endregion
    }

    #endregion

    #region Internal Methods (for DatabaseUsageMonitorService)

    /**************************************************************/
    /// <summary>
    /// Updates the throttle state with new metrics data.
    /// </summary>
    /// <param name="percentUsed">The current percentage of free tier consumed.</param>
    /// <param name="remainingVCoreSeconds">The remaining vCore seconds.</param>
    /// <remarks>
    /// This method is called by <see cref="DatabaseUsageMonitorService"/> when new
    /// metrics are available. It calculates the appropriate throttle level and
    /// raises <see cref="ThrottleStateChanged"/> if the level changes.
    /// 
    /// Throttle levels are determined by configurable thresholds:
    /// 
    /// | Level | Default Threshold |
    /// |-------|-------------------|
    /// | None | &lt; 70% |
    /// | Warning | 70-80% |
    /// | Moderate | 80-90% |
    /// | Aggressive | 90-95% |
    /// | Critical | 95-100% |
    /// | CostLimit | &gt; MaxMonthlyCostPercent |
    /// </remarks>
    /// <seealso cref="ThrottleLevel"/>
    /// <seealso cref="DatabaseUsageMonitorService"/>
    internal void UpdateState(double percentUsed, double remainingVCoreSeconds)
    {
        #region implementation

        ThrottleLevel newLevel = calculateThrottleLevel(percentUsed);
        ThrottleLevel previousLevel;
        bool levelChanged = false;

        _lock.EnterWriteLock();
        try
        {
            previousLevel = _currentLevel;
            levelChanged = previousLevel != newLevel;

            _currentLevel = newLevel;
            _percentUsed = percentUsed;
            _remainingVCoreSeconds = remainingVCoreSeconds;
            _lastUpdated = DateTimeOffset.UtcNow;
            _isMonitoringActive = true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        // Log and raise event outside of lock
        if (levelChanged)
        {
            // Use appropriate log level based on severity
            var logLevel = newLevel switch
            {
                ThrottleLevel.CostLimit => LogLevel.Critical,
                ThrottleLevel.Critical => LogLevel.Error,
                ThrottleLevel.Aggressive => LogLevel.Warning,
                _ => LogLevel.Warning
            };

            _logger.Log(
                logLevel,
                "Throttle level changed from {PreviousLevel} to {CurrentLevel}. " +
                "Usage: {PercentUsed:F1}%, Remaining: {Remaining:N0} vCore seconds",
                previousLevel,
                newLevel,
                percentUsed,
                remainingVCoreSeconds);

            // Raise event for subscribers
            raiseThrottleStateChanged(previousLevel, newLevel, percentUsed, remainingVCoreSeconds);
        }
        else
        {
            _logger.LogDebug(
                "Throttle state updated. Level: {Level}, Usage: {PercentUsed:F1}%, Remaining: {Remaining:N0}",
                newLevel,
                percentUsed,
                remainingVCoreSeconds);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Marks the monitoring service as inactive (e.g., after repeated failures).
    /// </summary>
    /// <remarks>
    /// Called by <see cref="DatabaseUsageMonitorService"/> when metrics collection fails.
    /// The throttle level is preserved, but <see cref="IsMonitoringActive"/> returns false.
    /// </remarks>
    /// <seealso cref="DatabaseUsageMonitorService"/>
    internal void SetMonitoringInactive()
    {
        #region implementation

        _lock.EnterWriteLock();
        try
        {
            _isMonitoringActive = false;
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        _logger.LogWarning("Throttle monitoring marked as inactive due to metrics collection failure.");

        #endregion
    }

    #endregion

    #region Private Methods

    /**************************************************************/
    /// <summary>
    /// Calculates the throttle level based on the percentage of free tier consumed.
    /// </summary>
    /// <param name="percentUsed">The percentage of free tier consumed (0-100+).</param>
    /// <returns>The appropriate throttle level for the given usage.</returns>
    /// <remarks>
    /// Uses configurable thresholds from constructor. Default values:
    /// 
    /// - CostLimit: &gt;= MaxMonthlyCostPercent (default 110%)
    /// - Critical: &gt;= 95%
    /// - Aggressive: &gt;= 90%
    /// - Moderate: &gt;= 80%
    /// - Warning: &gt;= 70%
    /// - None: &lt; 70%
    /// </remarks>
    /// <seealso cref="ThrottleLevel"/>
    private ThrottleLevel calculateThrottleLevel(double percentUsed)
    {
        #region implementation

        // Check CostLimit first (highest priority)
        if (percentUsed >= _maxMonthlyCostPercent)
            return ThrottleLevel.CostLimit;

        if (percentUsed >= _criticalThreshold)
            return ThrottleLevel.Critical;

        if (percentUsed >= _aggressiveThreshold)
            return ThrottleLevel.Aggressive;

        if (percentUsed >= _moderateThreshold)
            return ThrottleLevel.Moderate;

        if (percentUsed >= _warningThreshold)
            return ThrottleLevel.Warning;

        return ThrottleLevel.None;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Raises the <see cref="ThrottleStateChanged"/> event.
    /// </summary>
    /// <param name="previousLevel">The previous throttle level.</param>
    /// <param name="currentLevel">The new throttle level.</param>
    /// <param name="percentUsed">The current percentage used.</param>
    /// <param name="remainingVCoreSeconds">The remaining vCore seconds.</param>
    /// <seealso cref="ThrottleStateChangedEventArgs"/>
    private void raiseThrottleStateChanged(
        ThrottleLevel previousLevel,
        ThrottleLevel currentLevel,
        double percentUsed,
        double remainingVCoreSeconds)
    {
        #region implementation

        try
        {
            ThrottleStateChanged?.Invoke(
                this,
                new ThrottleStateChangedEventArgs(previousLevel, currentLevel, percentUsed, remainingVCoreSeconds));
        }
        catch (Exception ex)
        {
            // Don't let subscriber exceptions crash the service
            _logger.LogError(ex, "Error raising ThrottleStateChanged event.");
        }

        #endregion
    }

    #endregion

    #region IDisposable

    /**************************************************************/
    /// <summary>
    /// Releases resources used by the <see cref="ThrottleStateService"/>.
    /// </summary>
    /// <param name="disposing">True if called from Dispose; false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        #region implementation

        if (!_disposed)
        {
            if (disposing)
            {
                _lock.Dispose();
            }

            _disposed = true;
        }

        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    public void Dispose()
    {
        #region implementation

        Dispose(true);
        GC.SuppressFinalize(this);

        #endregion
    }

    #endregion
}

#endregion