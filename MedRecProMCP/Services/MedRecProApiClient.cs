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
/**************************************************************/

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
}
