using MedRecPro.Static.Models;
using MedRecPro.Static.Services;
using Microsoft.Extensions.Options;

namespace MedRecPro.Static.Middleware;

/**************************************************************/
/// <summary>
/// ASP.NET Core middleware that progressively delays 404 responses
/// from clients generating repeated not-found hits and throttles
/// rate-based abuse on configurable success-returning endpoints.
/// </summary>
/// <remarks>
/// This middleware runs AFTER the rest of the pipeline completes
/// (it calls <c>_next</c> first, then inspects the response status code).
///
/// **Pipeline placement:** After exception handling, before static files.
/// This ensures all 404s from any source (static files, routing, controllers)
/// are captured.
///
/// **Client IP resolution order (Cloudflare-aware):**
/// 1. CF-Connecting-IP header (Cloudflare real client IP)
/// 2. X-Forwarded-For header (first IP in chain)
/// 3. HttpContext.Connection.RemoteIpAddress (direct connection)
///
/// **Error handling:** All tarpit logic is wrapped in try-catch.
/// Exceptions are logged and swallowed — the middleware never crashes the request pipeline.
///
/// **Success response handling:**
/// - If the path matches a <see cref="TarpitSettings.MonitoredEndpoints"/> entry,
///   the hit is recorded in the endpoint abuse tracker. The 404 counter is NOT reset
///   because a bot hammering a monitored endpoint is not demonstrating legitimate behavior.
/// - If the path does not match a monitored endpoint and <see cref="TarpitSettings.ResetOnSuccess"/>
///   is enabled, the 404 counter is reset (existing behavior).
/// </remarks>
/// <seealso cref="TarpitService"/>
/// <seealso cref="TarpitSettings"/>
public class TarpitMiddleware
{
    #region Private Fields

    private readonly RequestDelegate _next;
    private readonly TarpitService _tarpitService;
    private readonly IOptionsMonitor<TarpitSettings> _settingsMonitor;
    private readonly ILogger<TarpitMiddleware> _logger;

    #endregion

    #region Constructor

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of the <see cref="TarpitMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="tarpitService">Singleton service managing IP tracking and delay calculation.</param>
    /// <param name="settingsMonitor">Options monitor for hot-reloadable tarpit configuration.</param>
    /// <param name="logger">Logger instance for this middleware.</param>
    /// <seealso cref="TarpitService"/>
    public TarpitMiddleware(
        RequestDelegate next,
        TarpitService tarpitService,
        IOptionsMonitor<TarpitSettings> settingsMonitor,
        ILogger<TarpitMiddleware> logger)
    {
        #region implementation

        _next = next ?? throw new ArgumentNullException(nameof(next));
        _tarpitService = tarpitService ?? throw new ArgumentNullException(nameof(tarpitService));
        _settingsMonitor = settingsMonitor ?? throw new ArgumentNullException(nameof(settingsMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        #endregion
    }

    #endregion

    #region Public Methods

    /**************************************************************/
    /// <summary>
    /// Processes an HTTP request through the pipeline, then applies tarpit
    /// logic based on the response status code and request path.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Execution flow:
    /// 1. Calls the next middleware in the pipeline.
    /// 2. If tarpit is disabled, returns immediately.
    /// 3. On 404: records the hit and applies progressive delay if threshold is met.
    /// 4. On success (status &lt; 400) for a monitored endpoint: records endpoint abuse
    ///    hit and applies delay if rate threshold is exceeded. Does NOT reset 404 counter.
    /// 5. On success for a non-monitored endpoint: resets 404 counter if ResetOnSuccess is enabled.
    /// 6. Any exception in tarpit logic is caught, logged, and swallowed.
    /// </remarks>
    public async Task InvokeAsync(HttpContext context)
    {
        #region implementation

        await _next(context);

        try
        {
            var settings = _settingsMonitor.CurrentValue;

            if (!settings.Enabled)
                return;

            var clientIp = getClientIp(context);

            if (context.Response.StatusCode == 404)
            {
                // 404 tracking — unchanged from original behavior
                _tarpitService.RecordHit(clientIp);
                var hitCount = _tarpitService.GetHitCount(clientIp);
                var delayMs = _tarpitService.CalculateDelay(hitCount);

                if (delayMs > 0)
                {
                    _logger.LogWarning(
                        "Tarpit: {IP} hit {Count} consecutive 404s, delaying {Delay}ms — {Path}",
                        clientIp, hitCount, delayMs, context.Request.Path);

                    await Task.Delay(delayMs, context.RequestAborted);
                }
            }
            else if (context.Response.StatusCode < 400)
            {
                // Success response — check if path matches a monitored endpoint
                var matchedEndpoint = getMatchedEndpoint(
                    context.Request.Path, settings.MonitoredEndpoints);

                if (matchedEndpoint != null)
                {
                    // Monitored endpoint abuse detection — do NOT reset 404 counter
                    _tarpitService.RecordEndpointHit(clientIp, matchedEndpoint);
                    var hitCount = _tarpitService.GetEndpointHitCount(clientIp, matchedEndpoint);
                    var delayMs = _tarpitService.CalculateEndpointDelay(hitCount);

                    if (delayMs > 0)
                    {
                        _logger.LogWarning(
                            "Tarpit: {IP} hit monitored endpoint {Endpoint} {Count} times in window, delaying {Delay}ms",
                            clientIp, matchedEndpoint, hitCount, delayMs);

                        await Task.Delay(delayMs, context.RequestAborted);
                    }
                }
                else if (settings.ResetOnSuccess)
                {
                    // Non-monitored success — reset 404 counter (existing behavior)
                    _tarpitService.ResetClient(clientIp);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected during delay — expected behavior, no action needed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TarpitMiddleware: Unexpected error during tarpit processing");
        }

        #endregion
    }

    #endregion

    #region Private Methods

    /**************************************************************/
    /// <summary>
    /// Checks if the request path matches any of the configured monitored endpoints.
    /// </summary>
    /// <param name="requestPath">The request path from the HTTP context.</param>
    /// <param name="monitoredEndpoints">The list of endpoint prefixes to match against.</param>
    /// <returns>
    /// The normalized (lowercase) matched endpoint path, or <c>null</c> if no match is found.
    /// </returns>
    /// <remarks>
    /// Matching is case-insensitive using <see cref="string.StartsWith(string, StringComparison)"/>
    /// with <see cref="StringComparison.OrdinalIgnoreCase"/>. Returns the first match found.
    /// </remarks>
    /// <seealso cref="TarpitSettings.MonitoredEndpoints"/>
    private static string? getMatchedEndpoint(string requestPath, List<string> monitoredEndpoints)
    {
        #region implementation

        if (monitoredEndpoints == null || monitoredEndpoints.Count == 0)
            return null;

        foreach (var endpoint in monitoredEndpoints)
        {
            if (requestPath.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase))
                return endpoint.ToLowerInvariant();
        }

        return null;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves the real client IP address behind Cloudflare proxy.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>
    /// The client IP address, resolved in order:
    /// CF-Connecting-IP → X-Forwarded-For (first IP) → RemoteIpAddress → "unknown".
    /// </returns>
    /// <remarks>
    /// Cloudflare sets CF-Connecting-IP to the true client IP. When behind
    /// other reverse proxies, X-Forwarded-For contains a comma-separated list
    /// of IPs where the first is the original client.
    /// </remarks>
    private static string getClientIp(HttpContext context)
    {
        #region implementation

        if (context.Request.Headers.TryGetValue("CF-Connecting-IP", out var cfIp)
            && !string.IsNullOrWhiteSpace(cfIp))
        {
            return cfIp.ToString().Trim();
        }

        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var xff)
            && !string.IsNullOrWhiteSpace(xff))
        {
            return xff.ToString().Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        #endregion
    }

    #endregion
}

/**************************************************************/
/// <summary>
/// Extension methods for registering the <see cref="TarpitMiddleware"/>
/// in the ASP.NET Core middleware pipeline.
/// </summary>
/// <seealso cref="TarpitMiddleware"/>
public static class TarpitMiddlewareExtensions
{
    /**************************************************************/
    /// <summary>
    /// Adds the <see cref="TarpitMiddleware"/> to the application's middleware pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    /// <example>
    /// <code>
    /// app.UseTarpitMiddleware();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseTarpitMiddleware(this IApplicationBuilder builder)
    {
        #region implementation

        return builder.UseMiddleware<TarpitMiddleware>();

        #endregion
    }
}
