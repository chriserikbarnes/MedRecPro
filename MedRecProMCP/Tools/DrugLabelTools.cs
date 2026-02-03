/**************************************************************/
/// <summary>
/// MCP tools for drug label operations.
/// </summary>
/// <remarks>
/// These tools provide Claude with the ability to search and retrieve
/// drug label information from the MedRecPro API. All operations
/// require authentication and forward the user's token to the API.
/// </remarks>
/// <seealso cref="MedRecProApiClient"/>
/**************************************************************/

using MedRecProMCP.Services;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace MedRecProMCP.Tools;

/**************************************************************/
/// <summary>
/// MCP tools for interacting with drug label data.
/// </summary>
/**************************************************************/
[McpServerToolType]
public class DrugLabelTools
{
    private readonly MedRecProApiClient _apiClient;
    private readonly ILogger<DrugLabelTools> _logger;

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of DrugLabelTools.
    /// </summary>
    /// <param name="apiClient">The MedRecPro API client.</param>
    /// <param name="logger">Logger instance.</param>
    /**************************************************************/
    public DrugLabelTools(MedRecProApiClient apiClient, ILogger<DrugLabelTools> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /**************************************************************/
    /// <summary>
    /// Searches drug labels by name, NDC code, or active ingredient.
    /// </summary>
    /// <param name="query">The search query (drug name, NDC, or ingredient).</param>
    /// <param name="pageNumber">Page number for pagination (default: 1).</param>
    /// <param name="pageSize">Number of results per page (default: 10, max: 50).</param>
    /// <returns>JSON array of matching drug labels with summary information.</returns>
    /// <remarks>
    /// Search matches against:
    /// - Drug product name
    /// - NDC (National Drug Code)
    /// - Active ingredient names
    /// - Pharmacologic class
    /// - Manufacturer name
    /// </remarks>
    /**************************************************************/
    [McpServerTool]
    [Description("Search drug labels by name, NDC code, or active ingredient. Returns matching drug products with summary information.")]
    public async Task<string> SearchDrugLabels(
        [Description("Search query - drug name, NDC code, or active ingredient")] string query,
        [Description("Page number (1-based)")] int pageNumber = 1,
        [Description("Results per page (1-50)")] int pageSize = 10)
    {
        #region implementation
        _logger.LogInformation("[Tool] SearchDrugLabels: query={Query}, page={Page}, size={Size}",
            query, pageNumber, pageSize);

        // Validate and constrain parameters
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);

        try
        {
            var endpoint = $"api/Label/product/search?productNameSearch={Uri.EscapeDataString(query)}&pageNumber={pageNumber}&pageSize={pageSize}";
            var result = await _apiClient.GetStringAsync(endpoint);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] SearchDrugLabels failed for query: {Query}", query);
            return JsonSerializer.Serialize(new
            {
                error = "Search failed",
                message = ex.Message
            });
        }
        #endregion
    }
}
