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
            var endpoint = $"/labels/search?q={Uri.EscapeDataString(query)}&page={pageNumber}&pageSize={pageSize}";
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

    /**************************************************************/
    /// <summary>
    /// Gets a complete drug label document by its GUID.
    /// </summary>
    /// <param name="documentGuid">The unique identifier of the drug label document.</param>
    /// <returns>Complete drug label with all sections and structured data.</returns>
    /**************************************************************/
    [McpServerTool]
    [Description("Get a complete drug label document by its unique identifier. Returns all sections including dosage, warnings, and ingredients.")]
    public async Task<string> GetDrugLabel(
        [Description("The document GUID (unique identifier)")] string documentGuid)
    {
        #region implementation
        _logger.LogInformation("[Tool] GetDrugLabel: guid={Guid}", documentGuid);

        if (!Guid.TryParse(documentGuid, out _))
        {
            return JsonSerializer.Serialize(new
            {
                error = "Invalid GUID format",
                message = "Please provide a valid document GUID"
            });
        }

        try
        {
            var endpoint = $"/labels/single/{documentGuid}";
            var result = await _apiClient.GetStringAsync(endpoint);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] GetDrugLabel failed for guid: {Guid}", documentGuid);
            return JsonSerializer.Serialize(new
            {
                error = "Failed to retrieve drug label",
                message = ex.Message
            });
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets documentation and schema for a specific label section type.
    /// </summary>
    /// <param name="sectionType">The label section type (e.g., "Document", "Organization", "ActiveMoiety").</param>
    /// <returns>JSON schema and property descriptions for the section.</returns>
    /**************************************************************/
    [McpServerTool]
    [Description("Get schema documentation for a specific drug label section type. Useful for understanding the structure of label data.")]
    public async Task<string> GetLabelSectionSchema(
        [Description("Section type name (e.g., Document, Organization, ActiveMoiety)")] string sectionType)
    {
        #region implementation
        _logger.LogInformation("[Tool] GetLabelSectionSchema: section={Section}", sectionType);

        try
        {
            var endpoint = $"/labels/{sectionType}/documentation";
            var result = await _apiClient.GetStringAsync(endpoint);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] GetLabelSectionSchema failed for section: {Section}", sectionType);
            return JsonSerializer.Serialize(new
            {
                error = "Failed to retrieve section schema",
                message = ex.Message
            });
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Lists all available label section types.
    /// </summary>
    /// <returns>Array of section type names that can be queried.</returns>
    /**************************************************************/
    [McpServerTool]
    [Description("List all available drug label section types. These types can be used with other label tools.")]
    public async Task<string> ListLabelSectionTypes()
    {
        #region implementation
        _logger.LogInformation("[Tool] ListLabelSectionTypes");

        try
        {
            var endpoint = "/labels/sectionMenu";
            var result = await _apiClient.GetStringAsync(endpoint);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] ListLabelSectionTypes failed");
            return JsonSerializer.Serialize(new
            {
                error = "Failed to retrieve section types",
                message = ex.Message
            });
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets records from a specific label section with pagination.
    /// </summary>
    /// <param name="sectionType">The section type to query.</param>
    /// <param name="pageNumber">Page number for pagination.</param>
    /// <param name="pageSize">Number of results per page.</param>
    /// <returns>Paginated list of records from the specified section.</returns>
    /**************************************************************/
    [McpServerTool]
    [Description("Get records from a specific label section type with pagination. Use ListLabelSectionTypes to see available sections.")]
    public async Task<string> GetLabelSection(
        [Description("Section type name")] string sectionType,
        [Description("Page number (1-based)")] int pageNumber = 1,
        [Description("Results per page (1-50)")] int pageSize = 10)
    {
        #region implementation
        _logger.LogInformation("[Tool] GetLabelSection: section={Section}, page={Page}, size={Size}",
            sectionType, pageNumber, pageSize);

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);

        try
        {
            var endpoint = $"/labels/section/{sectionType}?pageNumber={pageNumber}&pageSize={pageSize}";
            var result = await _apiClient.GetStringAsync(endpoint);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] GetLabelSection failed for section: {Section}", sectionType);
            return JsonSerializer.Serialize(new
            {
                error = "Failed to retrieve section records",
                message = ex.Message
            });
        }
        #endregion
    }
}
