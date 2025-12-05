using MedRecPro.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Threading.Tasks;

namespace MedRecPro.Filters;

#region Enums

/**************************************************************/
/// <summary>
/// Defines the criticality levels for database operations.
/// </summary>
/// <remarks>
/// Used to determine at what throttle level an operation will be blocked or delayed.
/// 
/// | Level | Description | Throttle Behavior |
/// |-------|-------------|-------------------|
/// | Critical | Essential operations | Blocked only at CostLimit |
/// | Normal | Standard operations | Blocked at Aggressive+ |
/// | NonCritical | Deferrable operations | Blocked at Moderate+ |
/// </remarks>
/// <seealso cref="DatabaseIntensiveAttribute"/>
/// <seealso cref="DatabaseLimitAttribute"/>
public enum OperationCriticality
{
    #region implementation

    /// <summary>
    /// Critical operations that should only be blocked at the CostLimit throttle level.
    /// Examples: Authentication, essential reads, health checks.
    /// </summary>
    Critical = 0,

    /// <summary>
    /// Normal operations that can be blocked at Aggressive or higher levels.
    /// Examples: Standard CRUD operations, searches, user-initiated actions.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Non-critical operations that can be blocked at Moderate level or higher.
    /// Examples: Reports, bulk exports, analytics, background sync.
    /// </summary>
    NonCritical = 2

    #endregion
}

#endregion

#region DatabaseIntensiveAttribute

/**************************************************************/
/// <summary>
/// Attribute that marks a controller or action as database-intensive,
/// enabling automatic throttle checks that **block** requests when thresholds are exceeded.
/// </summary>
/// <remarks>
/// When applied to a controller or action, the <see cref="ThrottleCheckFilter"/>
/// will check <see cref="IThrottleStateService"/> before the action executes.
/// If throttling is active at the appropriate level, the request will be rejected
/// with a **503 Service Unavailable** response.
/// 
/// For rate-limiting behavior (delays instead of blocking), use <see cref="DatabaseLimitAttribute"/>.
/// 
/// ### Blocking Behavior by Criticality
/// 
/// | Criticality | Blocked At                                |
/// |-------------|-------------------------------------------|
/// | Critical    | CostLimit only                            |
/// | Normal      | Aggressive, Critical, CostLimit           |
/// | NonCritical | Moderate, Aggressive, Critical, CostLimit |
/// </remarks>
/// <example>
/// ```csharp
/// // Block this action when throttling reaches Aggressive or higher
/// [DatabaseIntensive]
/// [HttpPost("import")]
/// public async Task&lt;IActionResult&gt; ImportData() { ... }
/// 
/// // Block this action only at Critical/CostLimit level
/// [DatabaseIntensive(OperationCriticality.Critical)]
/// [HttpGet("summary")]
/// public async Task&lt;IActionResult&gt; GetSummary() { ... }
/// 
/// // Block this action at Moderate level or higher (most restrictive)
/// [DatabaseIntensive(OperationCriticality.NonCritical)]
/// [HttpPost("report")]
/// public async Task&lt;IActionResult&gt; GenerateReport() { ... }
/// ```
/// </example>
/// <seealso cref="ThrottleCheckFilter"/>
/// <seealso cref="DatabaseLimitAttribute"/>
/// <seealso cref="IThrottleStateService"/>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class DatabaseIntensiveAttribute : Attribute, IFilterFactory
{
    #region Properties

    /**************************************************************/
    /// <summary>
    /// Gets the criticality level of the operation.
    /// </summary>
    /// <remarks>
    /// Determines at what throttle level this operation will be blocked.
    /// </remarks>
    /// <seealso cref="OperationCriticality"/>
    public OperationCriticality Criticality { get; }

    /**************************************************************/
    /// <summary>
    /// Gets a value indicating whether the filter instance can be reused.
    /// </summary>
    /// <remarks>
    /// Returns `false` because the filter depends on scoped services.
    /// </remarks>
    public bool IsReusable => false;

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseIntensiveAttribute"/> class
    /// with the specified operation criticality.
    /// </summary>
    /// <param name="criticality">
    /// The criticality level of the operation. Defaults to <see cref="OperationCriticality.Normal"/>.
    /// </param>
    /// <seealso cref="OperationCriticality"/>
    public DatabaseIntensiveAttribute(OperationCriticality criticality = OperationCriticality.Normal)
    {
        Criticality = criticality;
    }

    #endregion

    #region IFilterFactory Implementation

    /**************************************************************/
    /// <summary>
    /// Creates an instance of the <see cref="ThrottleCheckFilter"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    /// <returns>A new instance of <see cref="ThrottleCheckFilter"/>.</returns>
    /// <seealso cref="IFilterFactory.CreateInstance"/>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        #region implementation

        var throttleState = serviceProvider.GetRequiredService<IThrottleStateService>();
        var logger = serviceProvider.GetRequiredService<ILogger<ThrottleCheckFilter>>();

        return new ThrottleCheckFilter(throttleState, logger, Criticality);

        #endregion
    }

    #endregion
}

#endregion

#region DatabaseLimitAttribute

/**************************************************************/
/// <summary>
/// Attribute that marks a controller or action as database-intensive,
/// enabling automatic throttle checks that **delay** requests to limit cost accrual.
/// </summary>
/// <remarks>
/// Unlike <see cref="DatabaseIntensiveAttribute"/> which blocks requests entirely,
/// this attribute imposes a thread-safe async delay based on the current throttle level.
/// This allows the application to continue functioning while naturally limiting
/// how quickly database costs can accrue.
/// 
/// ### Delay Scaling
/// 
/// The base wait time is multiplied by a factor based on throttle level:
/// 
/// | Throttle Level | Multiplier | Example (100ms base) |
/// |----------------|------------|----------------------|
/// | None           | 0x         | No delay             |
/// | Warning        | 1x         | 100ms                |
/// | Moderate       | 2x         | 200ms                |
/// | Aggressive     | 4x         | 400ms                |
/// | Critical       | 8x         | 800ms                |
/// | CostLimit      | 16x        | 1600ms               |
/// 
/// ### When to Use
/// 
/// - Use `[DatabaseLimit]` for operations that should slow down but not fail
/// - Use `[DatabaseIntensive]` for operations that should be blocked entirely
/// - Combine with `[DatabaseIntensive]` on the same action for tiered behavior
/// </remarks>
/// <example>
/// ```csharp
/// // Impose delays when throttling is active (default 100ms base)
/// [DatabaseLimit]
/// [HttpGet("search")]
/// public async Task&lt;IActionResult&gt; Search() { ... }
/// 
/// // Custom delay of 50ms base, scaled by throttle level
/// [DatabaseLimit(OperationCriticality.Normal, Wait = 50)]
/// [HttpGet("list")]
/// public async Task&lt;IActionResult&gt; ListItems() { ... }
/// 
/// // Non-critical with longer base delay
/// [DatabaseLimit(OperationCriticality.NonCritical, Wait = 200)]
/// [HttpPost("export")]
/// public async Task&lt;IActionResult&gt; Export() { ... }
/// 
/// // Combine: delay at lower levels, block at higher levels
/// [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
/// [DatabaseIntensive(OperationCriticality.Normal)]
/// [HttpPost("bulk-update")]
/// public async Task&lt;IActionResult&gt; BulkUpdate() { ... }
/// ```
/// </example>
/// <seealso cref="DatabaseLimitFilter"/>
/// <seealso cref="DatabaseIntensiveAttribute"/>
/// <seealso cref="IThrottleStateService"/>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class DatabaseLimitAttribute : Attribute, IFilterFactory
{
    #region Constants

    /// <summary>
    /// Default wait time in milliseconds when not specified.
    /// </summary>
    public const int DefaultWaitMs = 100;

    #endregion

    #region Properties

    /**************************************************************/
    /// <summary>
    /// Gets the criticality level of the operation.
    /// </summary>
    /// <remarks>
    /// Determines at what throttle level delays begin to be applied.
    /// 
    /// - **Critical**: Delays only at Warning level and above
    /// - **Normal**: Delays at Warning level and above
    /// - **NonCritical**: Delays at any throttle level above None
    /// </remarks>
    /// <seealso cref="OperationCriticality"/>
    public OperationCriticality Criticality { get; }

    /**************************************************************/
    /// <summary>
    /// Gets the base wait time in milliseconds.
    /// </summary>
    /// <remarks>
    /// This value is multiplied by the throttle level multiplier to calculate
    /// the actual delay. Default is 100ms.
    /// 
    /// **Example**: With `Wait = 100` at `Aggressive` level (4x multiplier),
    /// the actual delay would be 400ms.
    /// </remarks>
    public int Wait { get; set; } = DefaultWaitMs;

    /**************************************************************/
    /// <summary>
    /// Gets a value indicating whether the filter instance can be reused.
    /// </summary>
    /// <remarks>
    /// Returns `false` because the filter depends on scoped services.
    /// </remarks>
    public bool IsReusable => false;

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseLimitAttribute"/> class
    /// with the specified operation criticality.
    /// </summary>
    /// <param name="criticality">
    /// The criticality level of the operation. Defaults to <see cref="OperationCriticality.Normal"/>.
    /// </param>
    /// <remarks>
    /// Use the `Wait` property to specify a custom base delay:
    /// 
    /// ```csharp
    /// [DatabaseLimit(OperationCriticality.Normal, Wait = 50)]
    /// ```
    /// </remarks>
    /// <seealso cref="OperationCriticality"/>
    public DatabaseLimitAttribute(OperationCriticality criticality = OperationCriticality.Normal)
    {
        Criticality = criticality;
    }

    #endregion

    #region IFilterFactory Implementation

    /**************************************************************/
    /// <summary>
    /// Creates an instance of the <see cref="DatabaseLimitFilter"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    /// <returns>A new instance of <see cref="DatabaseLimitFilter"/>.</returns>
    /// <seealso cref="IFilterFactory.CreateInstance"/>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        #region implementation

        var throttleState = serviceProvider.GetRequiredService<IThrottleStateService>();
        var logger = serviceProvider.GetRequiredService<ILogger<DatabaseLimitFilter>>();

        return new DatabaseLimitFilter(throttleState, logger, Criticality, Wait);

        #endregion
    }

    #endregion
}

#endregion

#region ThrottleCheckFilter (Blocking)

/**************************************************************/
/// <summary>
/// Action filter that checks the current throttle state and **blocks** requests
/// when appropriate based on operation criticality.
/// </summary>
/// <remarks>
/// This filter is created by <see cref="DatabaseIntensiveAttribute"/> via the
/// <see cref="IFilterFactory"/> interface. It checks <see cref="IThrottleStateService"/>
/// before action execution and returns a **503 response** if throttling is active.
/// 
/// The filter includes the current throttle state and a `Retry-After` header
/// in the response to help clients understand the situation and when to retry.
/// 
/// For rate-limiting behavior instead of blocking, see <see cref="DatabaseLimitFilter"/>.
/// </remarks>
/// <example>
/// Response when throttled:
/// 
/// ```http
/// HTTP/1.1 503 Service Unavailable
/// Retry-After: 3600
/// Content-Type: application/json
/// 
/// {
///   "error": "Service temporarily unavailable due to database throttling",
///   "throttleLevel": "Aggressive",
///   "percentUsed": 92.5,
///   "retryAfterSeconds": 3600,
///   "message": "Aggressive: 92.5% of free tier consumed..."
/// }
/// ```
/// </example>
/// <seealso cref="IActionFilter"/>
/// <seealso cref="DatabaseIntensiveAttribute"/>
/// <seealso cref="IThrottleStateService"/>
public class ThrottleCheckFilter : IActionFilter
{
    #region Fields

    private readonly IThrottleStateService _throttleState;
    private readonly ILogger<ThrottleCheckFilter> _logger;
    private readonly OperationCriticality _criticality;

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="ThrottleCheckFilter"/> class.
    /// </summary>
    /// <param name="throttleState">The throttle state service to check.</param>
    /// <param name="logger">Logger for recording throttle events.</param>
    /// <param name="criticality">The criticality level of the operation being filtered.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <seealso cref="IThrottleStateService"/>
    /// <seealso cref="OperationCriticality"/>
    public ThrottleCheckFilter(
        IThrottleStateService throttleState,
        ILogger<ThrottleCheckFilter> logger,
        OperationCriticality criticality)
    {
        #region implementation

        _throttleState = throttleState ?? throw new ArgumentNullException(nameof(throttleState));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _criticality = criticality;

        #endregion
    }

    #endregion

    #region IActionFilter Implementation

    /**************************************************************/
    /// <summary>
    /// Called before the action executes. Checks throttle state and may short-circuit.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <remarks>
    /// Determines whether to block the request based on:
    /// 
    /// - Current throttle level from <see cref="IThrottleStateService"/>
    /// - Operation criticality from <see cref="DatabaseIntensiveAttribute"/>
    /// 
    /// ### Blocking Logic
    /// 
    /// | Criticality | Blocked At |
    /// |-------------|------------|
    /// | NonCritical | Moderate, Aggressive, Critical, CostLimit |
    /// | Normal | Aggressive, Critical, CostLimit |
    /// | Critical | CostLimit only |
    /// </remarks>
    /// <seealso cref="IActionFilter.OnActionExecuting"/>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        #region implementation

        var currentLevel = _throttleState.CurrentLevel;

        // Determine if we should block based on criticality
        bool shouldBlock = _criticality switch
        {
            OperationCriticality.NonCritical => currentLevel >= ThrottleLevel.Moderate,
            OperationCriticality.Normal => currentLevel >= ThrottleLevel.Aggressive,
            OperationCriticality.Critical => currentLevel >= ThrottleLevel.CostLimit,
            _ => false
        };

        if (!shouldBlock)
        {
            return;
        }

        // Log the throttled request
        var actionName = context.ActionDescriptor.DisplayName ?? "Unknown";

        _logger.LogWarning(
            "Request BLOCKED by throttle. Action: {Action}, Criticality: {Criticality}, " +
            "ThrottleLevel: {Level}, Usage: {Percent:F1}%",
            actionName,
            _criticality,
            currentLevel,
            _throttleState.PercentUsed);

        // Calculate retry-after based on throttle level
        var retryAfterSeconds = currentLevel switch
        {
            ThrottleLevel.CostLimit => 7200,    // 2 hours
            ThrottleLevel.Critical => 3600,     // 1 hour
            ThrottleLevel.Aggressive => 1800,   // 30 minutes
            ThrottleLevel.Moderate => 600,      // 10 minutes
            _ => 300                            // 5 minutes
        };

        // Add Retry-After header
        context.HttpContext.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();

        // Return 503 Service Unavailable
        context.Result = new ObjectResult(new
        {
            error = "Service temporarily unavailable due to database throttling",
            throttleLevel = currentLevel.ToString(),
            percentUsed = Math.Round(_throttleState.PercentUsed, 2),
            retryAfterSeconds = retryAfterSeconds,
            message = _throttleState.GetStateDescription()
        })
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Called after the action executes. No post-processing is performed.
    /// </summary>
    /// <param name="context">The action executed context.</param>
    /// <seealso cref="IActionFilter.OnActionExecuted"/>
    public void OnActionExecuted(ActionExecutedContext context)
    {
        // No post-processing needed
    }

    #endregion
}

#endregion

#region DatabaseLimitFilter (Rate Limiting)

/**************************************************************/
/// <summary>
/// Async action filter that imposes thread-safe delays based on throttle state
/// to limit database cost accrual while allowing operations to complete.
/// </summary>
/// <remarks>
/// This filter is created by <see cref="DatabaseLimitAttribute"/> via the
/// <see cref="IFilterFactory"/> interface. Unlike <see cref="ThrottleCheckFilter"/>
/// which blocks requests, this filter imposes an async delay before allowing
/// the action to proceed.
/// 
/// ### Delay Calculation
/// 
/// The actual delay is calculated as: `baseWait × throttleLevelMultiplier`
/// 
/// | Throttle Level | Multiplier | 100ms Base | 200ms Base |
/// |----------------|------------|------------|------------|
/// | None           | 0x         | 0ms        | 0ms        |
/// | Warning        | 1x         | 100ms      | 200ms      |
/// | Moderate       | 2x         | 200ms      | 400ms      |
/// | Aggressive     | 4x         | 400ms      | 800ms      |
/// | Critical       | 8x         | 800ms      | 1600ms     |
/// | CostLimit      | 16x        | 1600ms     | 3200ms     |
/// 
/// **Maximum delay is capped at 30 seconds** to prevent request timeouts.
/// 
/// ### Thread Safety
/// 
/// The delay is implemented using `Task.Delay()` which is non-blocking
/// and does not consume a thread during the wait period.
/// 
/// ### Cost Control Strategy
/// 
/// Azure SQL Serverless free tier provides 100,000 vCore-seconds/month.
/// Beyond that, cost is ~$0.000145 per vCore-second (~$14.50 per 100,000 vCore-seconds).
/// 
/// | Usage % | vCore-seconds | Monthly Cost |
/// |---------|---------------|--------------|
/// | 100%    | 100,000       | $0 (free)    |
/// | 110%    | 110,000       | ~$1.45       |
/// | 150%    | 150,000       | ~$7.25       |
/// | 170%    | 170,000       | ~$10.15      |
/// | 200%    | 200,000       | ~$14.50      |
/// | 300%    | 300,000       | ~$29.00      |
/// 
/// ### Choosing Wait Times and Limits
/// 
/// **Scenario 1: Current cost is $15, limit is $10, want to stay under $30**
/// 
/// You've already exceeded your $10 limit (at ~170% usage). To prevent reaching $30 (~300%):
/// 
/// ```json
/// {
///   "DatabaseUsageMonitor": {
///     "MaxMonthlyCostPercent": 300,
///     "CriticalThreshold": 170
///   }
/// }
/// ```
/// 
/// ```csharp
/// // Aggressive delays to slow down usage
/// [DatabaseLimit(OperationCriticality.Normal, Wait = 500)]
/// [HttpPost("process")]
/// public async Task&lt;IActionResult&gt; ProcessData() { ... }
/// ```
/// 
/// At 170% (current), this imposes 500ms × 8 (Critical) = 4 second delays,
/// significantly slowing request throughput and cost accrual.
/// 
/// ---
/// 
/// **Scenario 2: Current cost is $8, want hard limit at $10**
/// 
/// At $8, you're at ~155% of free tier. To hard-stop at $10 (~170%):
/// 
/// ```json
/// {
///   "DatabaseUsageMonitor": {
///     "MaxMonthlyCostPercent": 170,
///     "CriticalThreshold": 155,
///     "AggressiveThreshold": 140
///   }
/// }
/// ```
/// 
/// ```csharp
/// // Block non-critical at the limit, delay everything else
/// [DatabaseLimit(OperationCriticality.Normal, Wait = 200)]
/// [DatabaseIntensive(OperationCriticality.Normal)]
/// [HttpPost("reports")]
/// public async Task&lt;IActionResult&gt; GenerateReport() { ... }
/// ```
/// 
/// This will:
/// - Start delays at 140% (~$5.80)
/// - Heavy delays at 155% (~$8.00)
/// - **Block** non-critical requests at 170% (~$10.15)
/// 
/// ---
/// 
/// **Scenario 3: Essential logging that must run regardless of cost**
/// 
/// Background processes like API logging should use `OperationCriticality.Critical`
/// with minimal or no delays:
/// 
/// ```csharp
/// // Essential logging - never blocked, minimal delay
/// [DatabaseLimit(OperationCriticality.Critical, Wait = 10)]
/// [HttpPost("internal/log")]
/// public async Task&lt;IActionResult&gt; LogApiUsage([FromBody] ApiLogEntry entry) { ... }
/// 
/// // Or in a background service, check but don't block:
/// public async Task LogActivityAsync(ActivityLog log)
/// {
///     // Critical operations only get delayed at Aggressive+
///     // and are never blocked except at CostLimit
///     if (_throttleState.IsCostLimitExceeded)
///     {
///         // Queue for later instead of dropping
///         await _backgroundQueue.EnqueueAsync(log);
///         return;
///     }
///     
///     await _dbContext.ActivityLogs.AddAsync(log);
///     await _dbContext.SaveChangesAsync();
/// }
/// ```
/// 
/// | Criticality  | Delays Start At | Blocked At     |
/// |--------------|-----------------|----------------|
/// | NonCritical  | Warning (70%)   | Moderate+      |
/// | Normal       | Warning (70%)   | Aggressive+    |
/// | **Critical** | Aggressive (90%)| CostLimit only |
/// 
/// ---
/// 
/// **Scenario 4: Preventing database pause from halting the application**
/// 
/// Azure SQL Serverless auto-pauses after inactivity. When paused, the first
/// request triggers a resume (~1 minute delay). To handle this gracefully:
/// 
/// ```csharp
/// // Health check that doesn't wake the database
/// [HttpGet("health")]
/// public IActionResult HealthCheck()
/// {
///     // Don't hit the database - just return app status
///     return Ok(new { 
///         status = "healthy",
///         throttleLevel = _throttleState.CurrentLevel.ToString(),
///         isMonitoringActive = _throttleState.IsMonitoringActive
///     });
/// }
/// 
/// // Lightweight endpoint with timeout protection
/// [DatabaseLimit(OperationCriticality.Critical, Wait = 50)]
/// [HttpGet("status")]
/// public async Task&lt;IActionResult&gt; GetStatus(CancellationToken cancellationToken)
/// {
///     try
///     {
///         // Use a timeout to prevent long waits during resume
///         using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
///         cts.CancelAfter(TimeSpan.FromSeconds(10));
///         
///         var count = await _dbContext.Users.CountAsync(cts.Token);
///         return Ok(new { userCount = count });
///     }
///     catch (OperationCanceledException)
///     {
///         // Database likely resuming from pause
///         return StatusCode(503, new { 
///             error = "Database is waking up, please retry",
///             retryAfter = 60 
///         });
///     }
/// }
/// ```
/// 
/// ---
/// 
/// **Scenario 5: Tiered protection for different operation types**
/// 
/// ```csharp
/// // Authentication - always allowed, minimal delay
/// [DatabaseLimit(OperationCriticality.Critical, Wait = 25)]
/// [HttpPost("auth/login")]
/// public async Task&lt;IActionResult&gt; Login() { ... }
/// 
/// // Standard CRUD - moderate protection
/// [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
/// [HttpGet("api/items")]
/// public async Task&lt;IActionResult&gt; GetItems() { ... }
/// 
/// // Expensive operations - aggressive protection
/// [DatabaseLimit(OperationCriticality.NonCritical, Wait = 300)]
/// [DatabaseIntensive(OperationCriticality.NonCritical)]
/// [HttpPost("api/bulk-import")]
/// public async Task&lt;IActionResult&gt; BulkImport() { ... }
/// 
/// // Reports - block when throttled, heavy delays otherwise  
/// [DatabaseLimit(OperationCriticality.NonCritical, Wait = 500)]
/// [DatabaseIntensive(OperationCriticality.NonCritical)]
/// [HttpPost("api/reports/generate")]
/// public async Task&lt;IActionResult&gt; GenerateReport() { ... }
/// ```
/// 
/// ---
/// 
/// ### Recommended Configurations by Use Case
/// 
/// **Strict Cost Control (stay under $5/month):**
/// ```json
/// {
///   "DatabaseUsageMonitor": {
///     "WarningThreshold": 80,
///     "ModerateThreshold": 100,
///     "AggressiveThreshold": 120,
///     "CriticalThreshold": 130,
///     "MaxMonthlyCostPercent": 135
///   }
/// }
/// ```
/// 
/// **Balanced (allow up to $15/month):**
/// ```json
/// {
///   "DatabaseUsageMonitor": {
///     "WarningThreshold": 70,
///     "ModerateThreshold": 100,
///     "AggressiveThreshold": 150,
///     "CriticalThreshold": 180,
///     "MaxMonthlyCostPercent": 200
///   }
/// }
/// ```
/// 
/// **Relaxed (allow up to $30/month):**
/// ```json
/// {
///   "DatabaseUsageMonitor": {
///     "WarningThreshold": 100,
///     "ModerateThreshold": 150,
///     "AggressiveThreshold": 200,
///     "CriticalThreshold": 250,
///     "MaxMonthlyCostPercent": 300
///   }
/// }
/// ```
/// </remarks>
/// <example>
/// **Basic usage:**
/// 
/// ```csharp
/// // Default 100ms base delay
/// [DatabaseLimit]
/// [HttpGet("search")]
/// public async Task&lt;IActionResult&gt; Search() { ... }
/// ```
/// 
/// **Custom delay:**
/// 
/// ```csharp
/// // 200ms base delay (400ms at Moderate, 800ms at Aggressive)
/// [DatabaseLimit(OperationCriticality.Normal, Wait = 200)]
/// [HttpPost("process")]
/// public async Task&lt;IActionResult&gt; Process() { ... }
/// ```
/// 
/// **Essential operation with minimal delay:**
/// 
/// ```csharp
/// // Only delayed at Aggressive+, never blocked except CostLimit
/// [DatabaseLimit(OperationCriticality.Critical, Wait = 10)]
/// [HttpPost("log")]
/// public async Task&lt;IActionResult&gt; WriteLog() { ... }
/// ```
/// 
/// **Combined delay and block:**
/// 
/// ```csharp
/// // Delay at Warning+, block at Aggressive+
/// [DatabaseLimit(OperationCriticality.Normal, Wait = 150)]
/// [DatabaseIntensive(OperationCriticality.Normal)]
/// [HttpPost("expensive-operation")]
/// public async Task&lt;IActionResult&gt; ExpensiveOperation() { ... }
/// ```
/// </example>
/// <seealso cref="IAsyncActionFilter"/>
/// <seealso cref="DatabaseLimitAttribute"/>
/// <seealso cref="DatabaseIntensiveAttribute"/>
/// <seealso cref="IThrottleStateService"/>
/// <seealso cref="ThrottleLevel"/>
public class DatabaseLimitFilter : IAsyncActionFilter
{
    #region Fields

    private readonly IThrottleStateService _throttleState;
    private readonly ILogger<DatabaseLimitFilter> _logger;
    private readonly OperationCriticality _criticality;
    private readonly int _baseWaitMs;

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseLimitFilter"/> class.
    /// </summary>
    /// <param name="throttleState">The throttle state service to check.</param>
    /// <param name="logger">Logger for recording throttle events.</param>
    /// <param name="criticality">The criticality level of the operation being filtered.</param>
    /// <param name="baseWaitMs">The base wait time in milliseconds.</param>
    /// <exception cref="ArgumentNullException">Thrown when throttleState or logger is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when baseWaitMs is negative.</exception>
    /// <seealso cref="IThrottleStateService"/>
    /// <seealso cref="OperationCriticality"/>
    public DatabaseLimitFilter(
        IThrottleStateService throttleState,
        ILogger<DatabaseLimitFilter> logger,
        OperationCriticality criticality,
        int baseWaitMs)
    {
        #region implementation

        _throttleState = throttleState ?? throw new ArgumentNullException(nameof(throttleState));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _criticality = criticality;

        if (baseWaitMs < 0)
            throw new ArgumentOutOfRangeException(nameof(baseWaitMs), "Wait time cannot be negative");

        _baseWaitMs = baseWaitMs;

        #endregion
    }

    #endregion

    #region IAsyncActionFilter Implementation

    /**************************************************************/
    /// <summary>
    /// Called asynchronously before the action executes. Imposes delay if throttling is active.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <param name="next">The delegate to invoke the next filter or action.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// The delay is only applied when:
    /// 
    /// 1. Throttle level is above `None`
    /// 2. Operation criticality allows delays at the current level
    /// 
    /// Critical operations skip delays at lower throttle levels to ensure
    /// essential functionality remains responsive.
    /// </remarks>
    /// <seealso cref="IAsyncActionFilter.OnActionExecutionAsync"/>
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        #region implementation

        var currentLevel = _throttleState.CurrentLevel;

        // Determine if we should apply delay based on criticality
        bool shouldDelay = shouldApplyDelay(currentLevel, _criticality);

        if (shouldDelay && _baseWaitMs > 0)
        {
            // Calculate actual delay based on throttle level multiplier
            var multiplier = getDelayMultiplier(currentLevel);
            var actualDelayMs = _baseWaitMs * multiplier;

            // Cap maximum delay at 30 seconds to prevent excessive waits
            actualDelayMs = Math.Min(actualDelayMs, 30_000);

            if (actualDelayMs > 0)
            {
                var actionName = context.ActionDescriptor.DisplayName ?? "Unknown";

                _logger.LogInformation(
                    "Request DELAYED by throttle. Action: {Action}, Criticality: {Criticality}, " +
                    "ThrottleLevel: {Level}, Delay: {Delay}ms, Usage: {Percent:F1}%",
                    actionName,
                    _criticality,
                    currentLevel,
                    actualDelayMs,
                    _throttleState.PercentUsed);

                // Add header to inform client of the delay
                context.HttpContext.Response.Headers["X-Throttle-Delay-Ms"] = actualDelayMs.ToString();
                context.HttpContext.Response.Headers["X-Throttle-Level"] = currentLevel.ToString();

                // Non-blocking async delay
                await Task.Delay(actualDelayMs, context.HttpContext.RequestAborted);
            }
        }

        // Proceed with the action
        await next();

        #endregion
    }

    #endregion

    #region Private Methods

    /**************************************************************/
    /// <summary>
    /// Determines whether a delay should be applied based on throttle level and criticality.
    /// </summary>
    /// <param name="level">The current throttle level.</param>
    /// <param name="criticality">The operation criticality.</param>
    /// <returns>True if a delay should be applied; otherwise, false.</returns>
    /// <remarks>
    /// Delay application rules:
    /// 
    /// - **NonCritical**: Delays at Warning and above
    /// - **Normal**: Delays at Warning and above
    /// - **Critical**: Delays only at Aggressive and above
    /// </remarks>
    private bool shouldApplyDelay(ThrottleLevel level, OperationCriticality criticality)
    {
        #region implementation

        // No delay if throttle is at None
        if (level == ThrottleLevel.None)
            return false;

        return criticality switch
        {
            // Non-critical operations get delayed at any throttle level
            OperationCriticality.NonCritical => level >= ThrottleLevel.Warning,

            // Normal operations get delayed at Warning and above
            OperationCriticality.Normal => level >= ThrottleLevel.Warning,

            // Critical operations only get delayed at higher levels
            OperationCriticality.Critical => level >= ThrottleLevel.Aggressive,

            _ => false
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the delay multiplier for the specified throttle level.
    /// </summary>
    /// <param name="level">The current throttle level.</param>
    /// <returns>The multiplier to apply to the base wait time.</returns>
    /// <remarks>
    /// Multipliers increase exponentially to provide stronger rate limiting
    /// as throttle severity increases:
    /// 
    /// | Level | Multiplier | With 100ms base |
    /// |-------|------------|-----------------|
    /// | None | 0 | 0ms |
    /// | Warning | 1 | 100ms |
    /// | Moderate | 2 | 200ms |
    /// | Aggressive | 4 | 400ms |
    /// | Critical | 8 | 800ms |
    /// | CostLimit | 16 | 1600ms |
    /// </remarks>
    private int getDelayMultiplier(ThrottleLevel level)
    {
        #region implementation

        return level switch
        {
            ThrottleLevel.None => 0,
            ThrottleLevel.Warning => 1,
            ThrottleLevel.Moderate => 2,
            ThrottleLevel.Aggressive => 4,
            ThrottleLevel.Critical => 8,
            ThrottleLevel.CostLimit => 16,
            _ => 1
        };

        #endregion
    }

    #endregion
}

#endregion