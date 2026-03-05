using MedRecPro.Static.Models;
using MedRecPro.Static.Services;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

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

    /**************************************************************/
    /// <summary>
    /// Cookie name used for client tracking when
    /// <see cref="TarpitSettings.EnableClientTracking"/> is enabled.
    /// </summary>
    private const string ClientTrackingCookieName = "__tp";

    /**************************************************************/
    /// <summary>
    /// Validates that a tracking cookie value is exactly 32 lowercase hexadecimal characters.
    /// </summary>
    /// <remarks>
    /// This format matches a GUID with hyphens removed (<c>ToString("N")</c>).
    /// Any cookie value that does not match is treated as absent (falls through to IP).
    /// </remarks>
    private static readonly Regex ValidCookiePattern =
        new(@"^[0-9a-f]{32}$", RegexOptions.Compiled);

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
    /// 1. Resolves client identity and sets tracking cookie BEFORE the pipeline runs
    ///    (response headers must be written before downstream middleware starts the body).
    /// 2. Calls the next middleware in the pipeline.
    /// 3. If tarpit is disabled, returns immediately.
    /// 4. On 404: records the hit and applies progressive delay if threshold is met.
    /// 5. On success (status &lt; 400) for a monitored endpoint: records endpoint abuse
    ///    hit and applies delay if rate threshold is exceeded. Does NOT reset 404 counter.
    /// 6. On success for a non-monitored endpoint: resets 404 counter if ResetOnSuccess is enabled.
    /// 7. Any exception in tarpit logic is caught, logged, and swallowed.
    /// </remarks>
    /// <seealso cref="resolveClientId"/>
    public async Task InvokeAsync(HttpContext context)
    {
        #region implementation

        // Phase 1: Resolve client identity BEFORE the pipeline runs so the
        // tracking cookie is set on the response before downstream middleware
        // starts writing the response body.
        string? clientId = null;
        string? clientIp = null;

        try
        {
            var settingsSnapshot = _settingsMonitor.CurrentValue;

            if (settingsSnapshot.Enabled)
            {
                clientId = resolveClientId(context, settingsSnapshot);
                clientIp = getClientIp(context);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TarpitMiddleware: Error resolving client identity");
        }

        // Phase 2: Execute the rest of the pipeline
        await _next(context);

        // Phase 3: Tarpit evaluation using the pre-resolved client identity
        try
        {
            var settings = _settingsMonitor.CurrentValue;

            if (!settings.Enabled)
                return;

            // Fall back to IP if identity resolution failed above
            clientId ??= getClientIp(context);
            clientIp ??= clientId;

            if (context.Response.StatusCode == 404)
            {
                // 404 tracking
                _tarpitService.RecordHit(clientId);
                var hitCount = _tarpitService.GetHitCount(clientId);
                var delayMs = _tarpitService.CalculateDelay(hitCount);

                if (delayMs > 0)
                {
                    _logger.LogWarning(
                        "Tarpit: {ClientId} (IP: {IP}) hit {Count} consecutive 404s, delaying {Delay}ms — {Path}",
                        clientId, clientIp, hitCount, delayMs, context.Request.Path);

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
                    _tarpitService.RecordEndpointHit(clientId, matchedEndpoint);
                    var hitCount = _tarpitService.GetEndpointHitCount(clientId, matchedEndpoint);
                    var delayMs = _tarpitService.CalculateEndpointDelay(hitCount);

                    if (delayMs > 0)
                    {
                        _logger.LogWarning(
                            "Tarpit: {ClientId} (IP: {IP}) hit monitored endpoint {Endpoint} {Count} times in window, delaying {Delay}ms",
                            clientId, clientIp, matchedEndpoint, hitCount, delayMs);

                        await Task.Delay(delayMs, context.RequestAborted);
                    }
                }
                else if (settings.ResetOnSuccess)
                {
                    // Non-monitored success — reset 404 counter (existing behavior)
                    _tarpitService.ResetClient(clientId);
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
    /// Resolves a stable client identifier using cookie-based tracking
    /// with IP address fallback.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="settings">The current tarpit settings snapshot.</param>
    /// <returns>
    /// The client identifier: cookie value if a valid <c>__tp</c> cookie exists,
    /// otherwise the client IP address from <see cref="getClientIp"/>.
    /// </returns>
    /// <remarks>
    /// Resolution order:
    /// 1. If <see cref="TarpitSettings.EnableClientTracking"/> is disabled, returns IP.
    /// 2. If a valid <c>__tp</c> cookie exists (32 hex chars), renews the cookie
    ///    and returns the cookie value as the identifier.
    /// 3. If no valid cookie exists, generates a new cookie (set on the response)
    ///    and returns the IP address for this request only. The next request from
    ///    the same browser will use the cookie.
    ///
    /// This approach ensures that the very first request from a new browser uses
    /// the IP (since no cookie is available yet), but all subsequent requests use
    /// the stable cookie value even if the IP changes.
    ///
    /// **Graceful degradation:** Bots that reject cookies get a new GUID set on
    /// every response but never send it back, so they fall through to IP tracking
    /// on every request — identical to the previous behavior.
    /// </remarks>
    /// <seealso cref="getClientIp"/>
    /// <seealso cref="appendTrackingCookie"/>
    private string resolveClientId(HttpContext context, TarpitSettings settings)
    {
        #region implementation

        if (!settings.EnableClientTracking)
            return getClientIp(context);

        // Check for existing valid tracking cookie
        if (context.Request.Cookies.TryGetValue(ClientTrackingCookieName, out var cookieValue)
            && !string.IsNullOrWhiteSpace(cookieValue)
            && ValidCookiePattern.IsMatch(cookieValue))
        {
            // Valid cookie found — renew it and use as identifier
            appendTrackingCookie(context, cookieValue, settings);
            return cookieValue;
        }

        // No valid cookie — set a new one for future requests, use IP for this request
        var newId = Guid.NewGuid().ToString("N");
        appendTrackingCookie(context, newId, settings);
        return getClientIp(context);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Appends the <c>__tp</c> tracking cookie to the response with secure defaults.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="value">The 32-character hex cookie value.</param>
    /// <param name="settings">The current tarpit settings for MaxAge calculation.</param>
    /// <remarks>
    /// Cookie properties:
    /// - <see cref="CookieOptions.HttpOnly"/>: true (prevents JavaScript access).
    /// - <see cref="CookieOptions.Secure"/>: true (HTTPS only).
    /// - <see cref="CookieOptions.SameSite"/>: Strict (prevents cross-site transmission).
    /// - <see cref="CookieOptions.Path"/>: "/" (applies to all paths).
    /// - <see cref="CookieOptions.MaxAge"/>: Aligned with <see cref="TarpitSettings.StaleEntryTimeoutMinutes"/>
    ///   to ensure the cookie expires at roughly the same time the server-side entry becomes stale.
    ///   Renewed on every request to keep active clients tracked.
    /// </remarks>
    /// <seealso cref="TarpitSettings.StaleEntryTimeoutMinutes"/>
    private static void appendTrackingCookie(HttpContext context, string value, TarpitSettings settings)
    {
        #region implementation

        context.Response.Cookies.Append(ClientTrackingCookieName, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = TimeSpan.FromMinutes(Math.Max(1, settings.StaleEntryTimeoutMinutes))
        });

        #endregion
    }

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
