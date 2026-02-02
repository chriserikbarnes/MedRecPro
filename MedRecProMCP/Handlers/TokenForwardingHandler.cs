/**************************************************************/
/// <summary>
/// HTTP delegating handler that forwards user tokens to the MedRecPro API.
/// </summary>
/// <remarks>
/// This handler is added to the HttpClient pipeline for MedRecPro API calls.
/// It extracts the upstream IdP token from the current user's MCP token
/// and attaches it as a Bearer token to outgoing requests.
///
/// This preserves the user's identity end-to-end, allowing the MedRecPro API
/// to apply its existing authorization rules based on who the actual user is.
/// </remarks>
/// <seealso cref="IMcpTokenService"/>
/**************************************************************/

using MedRecProMCP.Services;
using System.Net.Http.Headers;

namespace MedRecProMCP.Handlers;

/**************************************************************/
/// <summary>
/// Delegating handler that forwards authentication tokens to downstream APIs.
/// </summary>
/**************************************************************/
public class TokenForwardingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMcpTokenService _tokenService;
    private readonly ILogger<TokenForwardingHandler> _logger;

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of TokenForwardingHandler.
    /// </summary>
    /// <param name="httpContextAccessor">Provides access to the current HTTP context.</param>
    /// <param name="tokenService">Service for token operations.</param>
    /// <param name="logger">Logger instance.</param>
    /**************************************************************/
    public TokenForwardingHandler(
        IHttpContextAccessor httpContextAccessor,
        IMcpTokenService tokenService,
        ILogger<TokenForwardingHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenService = tokenService;
        _logger = logger;
    }

    /**************************************************************/
    /// <summary>
    /// Processes an HTTP request by adding the user's token to the Authorization header.
    /// </summary>
    /// <param name="request">The HTTP request message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTTP response from the downstream service.</returns>
    /// <remarks>
    /// The handler:
    /// 1. Extracts the MCP access token from the current request's Authorization header
    /// 2. Decrypts and extracts the upstream IdP token from the MCP token
    /// 3. Attaches the upstream token to the outgoing request
    /// 4. Logs token forwarding for audit purposes (without exposing token values)
    /// </remarks>
    /**************************************************************/
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        #region implementation
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            // Try to get the MCP access token from the current request
            var mcpToken = extractBearerToken(httpContext);

            if (!string.IsNullOrEmpty(mcpToken))
            {
                // Extract the upstream IdP token from the MCP token
                var upstreamToken = _tokenService.ExtractUpstreamToken(mcpToken);

                if (!string.IsNullOrEmpty(upstreamToken))
                {
                    // Forward the upstream token to the MedRecPro API
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", upstreamToken);

                    _logger.LogDebug(
                        "[TokenForward] Forwarding upstream token to {Uri}",
                        request.RequestUri);
                }
                else
                {
                    _logger.LogWarning(
                        "[TokenForward] No upstream token found in MCP token for request to {Uri}",
                        request.RequestUri);
                }
            }
            else
            {
                _logger.LogDebug(
                    "[TokenForward] No MCP token found for request to {Uri}",
                    request.RequestUri);
            }
        }
        else
        {
            _logger.LogDebug(
                "[TokenForward] No HTTP context available for request to {Uri}",
                request.RequestUri);
        }

        // Send the request to the downstream service
        var response = await base.SendAsync(request, cancellationToken);

        // Log response status for troubleshooting
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "[TokenForward] Downstream API returned {StatusCode} for {Method} {Uri}",
                response.StatusCode, request.Method, request.RequestUri);
        }

        return response;
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts the Bearer token from the HTTP context.
    /// </summary>
    /**************************************************************/
    private static string? extractBearerToken(HttpContext httpContext)
    {
        #region implementation
        var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader))
        {
            return null;
        }

        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..].Trim();
        }

        return null;
        #endregion
    }
}
