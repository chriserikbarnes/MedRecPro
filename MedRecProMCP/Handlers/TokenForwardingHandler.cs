/**************************************************************/
/// <summary>
/// HTTP delegating handler that forwards user tokens to the MedRecPro API.
/// </summary>
/// <remarks>
/// This handler is added to the HttpClient pipeline for MedRecPro API calls.
/// It extracts the MCP JWT from the current request's Authorization header
/// and forwards it directly to the downstream MedRecPro API.
///
/// The API's "McpBearer" authentication scheme validates MCP-signed JWTs,
/// so the MCP JWT is forwarded as-is rather than extracting the upstream IdP token.
/// </remarks>
/**************************************************************/

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
    private readonly ILogger<TokenForwardingHandler> _logger;

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of TokenForwardingHandler.
    /// </summary>
    /// <param name="httpContextAccessor">Provides access to the current HTTP context.</param>
    /// <param name="logger">Logger instance.</param>
    /**************************************************************/
    public TokenForwardingHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<TokenForwardingHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
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
    /// The handler forwards the MCP JWT directly to the downstream API.
    /// The API's "McpBearer" scheme validates MCP-signed JWTs, so no
    /// upstream token extraction is needed.
    /// </remarks>
    /// <seealso cref="IHttpContextAccessor"/>
    /**************************************************************/
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        #region implementation
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext != null)
        {
            // Get the MCP access token from the current request
            var mcpToken = extractBearerToken(httpContext);

            if (!string.IsNullOrEmpty(mcpToken))
            {
                // Forward the MCP JWT directly to the MedRecPro API
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mcpToken);

                _logger.LogDebug(
                    "[TokenForward] Forwarding MCP token to {Uri}",
                    request.RequestUri);
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
