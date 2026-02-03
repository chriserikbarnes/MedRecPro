/**************************************************************/
/// <summary>
/// Typed HttpClient for communicating with the MedRecPro API.
/// </summary>
/// <remarks>
/// This client is used by MCP tool classes to make authenticated
/// HTTP calls to the MedRecPro API. The TokenForwardingHandler
/// automatically attaches the user's token to each request.
/// </remarks>
/// <seealso cref="Handlers.TokenForwardingHandler"/>
/// <seealso cref="MedRecProMCP.Models.AiAgentRequest"/>
/// <seealso cref="MedRecProMCP.Models.AiAgentInterpretation"/>
/**************************************************************/

using MedRecProMCP.Models;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Typed HTTP client for MedRecPro API interactions.
/// </summary>
/**************************************************************/
public class MedRecProApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MedRecProApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of MedRecProApiClient.
    /// </summary>
    /// <param name="httpClient">The configured HttpClient instance.</param>
    /// <param name="logger">Logger instance.</param>
    /**************************************************************/
    public MedRecProApiClient(HttpClient httpClient, ILogger<MedRecProApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /**************************************************************/
    /// <summary>
    /// Sends a GET request to the MedRecPro API.
    /// </summary>
    /// <typeparam name="T">The expected response type.</typeparam>
    /// <param name="endpoint">The API endpoint (relative to base URL).</param>
    /// <returns>The deserialized response.</returns>
    /**************************************************************/
    public async Task<T?> GetAsync<T>(string endpoint)
    {
        #region implementation
        try
        {
            _logger.LogDebug("[API] GET {Endpoint}", endpoint);

            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(content, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[API] GET {Endpoint} failed", endpoint);
            throw;
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Sends a GET request and returns the raw JSON string.
    /// </summary>
    /// <param name="endpoint">The API endpoint.</param>
    /// <returns>The raw JSON response string.</returns>
    /**************************************************************/
    public async Task<string> GetStringAsync(string endpoint)
    {
        #region implementation
        try
        {
            _logger.LogDebug("[API] GET {Endpoint}", endpoint);

            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[API] GET {Endpoint} failed", endpoint);
            throw;
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Sends a POST request to the MedRecPro API.
    /// </summary>
    /// <typeparam name="TRequest">The request body type.</typeparam>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="data">The request body data.</param>
    /// <returns>The deserialized response.</returns>
    /**************************************************************/
    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
    {
        #region implementation
        try
        {
            _logger.LogDebug("[API] POST {Endpoint}", endpoint);

            var json = JsonSerializer.Serialize(data, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TResponse>(responseContent, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[API] POST {Endpoint} failed", endpoint);
            throw;
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Sends a PUT request to the MedRecPro API.
    /// </summary>
    /// <typeparam name="TRequest">The request body type.</typeparam>
    /// <param name="endpoint">The API endpoint.</param>
    /// <param name="data">The request body data.</param>
    /**************************************************************/
    public async Task PutAsync<TRequest>(string endpoint, TRequest data)
    {
        #region implementation
        try
        {
            _logger.LogDebug("[API] PUT {Endpoint}", endpoint);

            var json = JsonSerializer.Serialize(data, JsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(endpoint, content);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[API] PUT {Endpoint} failed", endpoint);
            throw;
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Sends a DELETE request to the MedRecPro API.
    /// </summary>
    /// <param name="endpoint">The API endpoint.</param>
    /**************************************************************/
    public async Task DeleteAsync(string endpoint)
    {
        #region implementation
        try
        {
            _logger.LogDebug("[API] DELETE {Endpoint}", endpoint);

            var response = await _httpClient.DeleteAsync(endpoint);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[API] DELETE {Endpoint} failed", endpoint);
            throw;
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Checks if a resource exists (HEAD request).
    /// </summary>
    /// <param name="endpoint">The API endpoint.</param>
    /// <returns>True if the resource exists (200-299 status).</returns>
    /**************************************************************/
    public async Task<bool> ExistsAsync(string endpoint)
    {
        #region implementation
        try
        {
            _logger.LogDebug("[API] HEAD {Endpoint}", endpoint);

            var request = new HttpRequestMessage(HttpMethod.Head, endpoint);
            var response = await _httpClient.SendAsync(request);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
        #endregion
    }

    #region AI Agent Methods

    /**************************************************************/
    /// <summary>
    /// Gets the current system context from the MedRecPro AI API.
    /// </summary>
    /// <returns>The current system context including authentication and capabilities.</returns>
    /// <remarks>
    /// The context includes information about authentication status, demo mode,
    /// available data counts, and enabled features. This helps Claude provide
    /// appropriate responses based on system state.
    /// </remarks>
    /// <seealso cref="AiSystemContext"/>
    /**************************************************************/
    public async Task<AiSystemContext?> GetContextAsync()
    {
        #region implementation
        try
        {
            _logger.LogDebug("[API] GET /api/ai/context");
            return await GetAsync<AiSystemContext>("/api/ai/context");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[API] GetContextAsync failed");
            throw;
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Interprets a user query by delegating to the MedRecPro AI API.
    /// </summary>
    /// <param name="request">The AI agent request containing the user's query.</param>
    /// <returns>The interpretation containing endpoint specifications to execute.</returns>
    /// <remarks>
    /// The interpret endpoint uses Claude to analyze the user's natural language query
    /// and returns structured API endpoint specifications that can be executed to
    /// fulfill the request. This is the first step in the interpret → collect → synthesize flow.
    /// </remarks>
    /// <seealso cref="AiAgentRequest"/>
    /// <seealso cref="AiAgentInterpretation"/>
    /**************************************************************/
    public async Task<AiAgentInterpretation?> InterpretAsync(AiAgentRequest request)
    {
        #region implementation
        try
        {
            _logger.LogDebug("[API] POST /api/ai/interpret for query: {Query}",
                request.UserMessage.Length > 100
                    ? request.UserMessage[..100] + "..."
                    : request.UserMessage);

            return await PostAsync<AiAgentRequest, AiAgentInterpretation>("/api/ai/interpret", request);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[API] InterpretAsync failed for query: {Query}",
                request.UserMessage);
            throw;
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Synthesizes API execution results into a coherent response.
    /// </summary>
    /// <param name="request">The synthesis request containing executed endpoints and results.</param>
    /// <returns>The synthesized response addressing the user's original query.</returns>
    /// <remarks>
    /// The synthesize endpoint uses Claude to analyze the API execution results
    /// and produce a human-readable response that directly addresses the user's query.
    /// This is the final step in the interpret → collect → synthesize flow.
    /// </remarks>
    /// <seealso cref="AiSynthesisRequest"/>
    /// <seealso cref="AiAgentSynthesis"/>
    /**************************************************************/
    public async Task<AiAgentSynthesis?> SynthesizeAsync(AiSynthesisRequest request)
    {
        #region implementation
        try
        {
            _logger.LogDebug("[API] POST /api/ai/synthesize for query: {Query}",
                request.OriginalQuery.Length > 100
                    ? request.OriginalQuery[..100] + "..."
                    : request.OriginalQuery);

            return await PostAsync<AiSynthesisRequest, AiAgentSynthesis>("/api/ai/synthesize", request);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[API] SynthesizeAsync failed for query: {Query}",
                request.OriginalQuery);
            throw;
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Executes a single endpoint specification and returns the result.
    /// </summary>
    /// <param name="specification">The endpoint specification to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The endpoint result containing status code, response data, and timing.</returns>
    /// <remarks>
    /// This method dynamically executes an endpoint based on its specification,
    /// supporting GET, POST, PUT, and DELETE methods. Query parameters and
    /// request bodies are applied as specified. Used by the WorkPlanExecutor
    /// to execute work plan steps.
    /// </remarks>
    /// <seealso cref="AiEndpointSpecification"/>
    /// <seealso cref="AiEndpointResult"/>
    /**************************************************************/
    public async Task<AiEndpointResult> ExecuteEndpointAsync(
        AiEndpointSpecification specification,
        CancellationToken cancellationToken = default)
    {
        #region implementation
        var stopwatch = Stopwatch.StartNew();
        var result = new AiEndpointResult
        {
            Specification = specification
        };

        try
        {
            // Build the full URL with query parameters
            var url = buildEndpointUrl(specification);

            _logger.LogDebug("[API] {Method} {Url} (Step {Step})",
                specification.Method, url, specification.Step);

            // Create and send the request
            var request = new HttpRequestMessage(
                new HttpMethod(specification.Method.ToUpperInvariant()),
                url);

            // Add body for POST/PUT requests
            if (specification.Body != null &&
                (specification.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                 specification.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase)))
            {
                var json = JsonSerializer.Serialize(specification.Body, JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            stopwatch.Stop();
            result.StatusCode = (int)response.StatusCode;
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                // Parse JSON response if present
                if (!string.IsNullOrWhiteSpace(content))
                {
                    try
                    {
                        result.Result = JsonSerializer.Deserialize<JsonElement>(content, JsonOptions);
                    }
                    catch (JsonException)
                    {
                        // If JSON parsing fails, return raw string
                        result.Result = content;
                    }
                }

                _logger.LogDebug("[API] {Method} {Url} completed in {Time}ms with status {Status}",
                    specification.Method, url, result.ExecutionTimeMs, result.StatusCode);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                result.Error = $"HTTP {result.StatusCode}: {errorContent}";

                _logger.LogWarning("[API] {Method} {Url} failed with status {Status}: {Error}",
                    specification.Method, url, result.StatusCode, result.Error);
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            result.StatusCode = 0;
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.Error = "Request was cancelled";

            _logger.LogWarning("[API] Request cancelled for {Method} {Path}",
                specification.Method, specification.Path);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            result.StatusCode = (int)(ex.StatusCode ?? HttpStatusCode.InternalServerError);
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.Error = ex.Message;

            _logger.LogError(ex, "[API] Request failed for {Method} {Path}",
                specification.Method, specification.Path);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.StatusCode = 500;
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.Error = ex.Message;

            _logger.LogError(ex, "[API] Unexpected error for {Method} {Path}",
                specification.Method, specification.Path);
        }

        return result;
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Builds the full URL from an endpoint specification including query parameters.
    /// </summary>
    /// <param name="specification">The endpoint specification.</param>
    /// <returns>The complete URL with query string.</returns>
    /**************************************************************/
    private static string buildEndpointUrl(AiEndpointSpecification specification)
    {
        #region implementation
        var path = specification.Path;

        // Ensure path starts with /
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        // Add query parameters if present
        if (specification.QueryParameters != null && specification.QueryParameters.Count > 0)
        {
            var queryString = string.Join("&",
                specification.QueryParameters.Select(kvp =>
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

            path = path.Contains('?')
                ? $"{path}&{queryString}"
                : $"{path}?{queryString}";
        }

        return path;
        #endregion
    }

    #endregion
}
