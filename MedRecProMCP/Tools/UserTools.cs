/**************************************************************/
/// <summary>
/// MCP tools for user account operations.
/// </summary>
/// <remarks>
/// These tools provide Claude with the ability to access user profile
/// and activity information from the MedRecPro API. All operations
/// require authentication.
/// </remarks>
/// <seealso cref="MedRecProApiClient"/>
/**************************************************************/

using MedRecProMCP.Services;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace MedRecProMCP.Tools;

/**************************************************************/
/// <summary>
/// MCP tools for user-related operations.
/// </summary>
/// <remarks>
/// The [Authorize] attribute ensures the MCP SDK returns a 401 challenge
/// with WWW-Authenticate headers when an unauthenticated client invokes
/// these tools, triggering the OAuth flow.
/// </remarks>
/**************************************************************/
[McpServerToolType]
#if !DEBUG
[Authorize]
#endif
public class UserTools
{
    private readonly MedRecProApiClient _apiClient;
    private readonly ILogger<UserTools> _logger;

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of UserTools.
    /// </summary>
    /**************************************************************/
    public UserTools(MedRecProApiClient apiClient, ILogger<UserTools> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /**************************************************************/
    /// <summary>
    /// Gets the current authenticated user's profile.
    /// </summary>
    /// <returns>User profile information including name, email, and roles.</returns>
    /**************************************************************/
    [McpServerTool]
    [Description("Get the current authenticated user's profile information including name, email, and roles.")]
    public async Task<string> GetMyProfile()
    {
        #region implementation
        _logger.LogInformation("[Tool] GetMyProfile");

        try
        {
            var endpoint = "/users/me";
            var result = await _apiClient.GetStringAsync(endpoint);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] GetMyProfile failed");
            return JsonSerializer.Serialize(new
            {
                error = "Failed to retrieve profile",
                message = ex.Message
            });
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the current user's recent activity log.
    /// </summary>
    /// <param name="pageNumber">Page number for pagination.</param>
    /// <param name="pageSize">Number of results per page.</param>
    /// <returns>List of recent activities performed by the user.</returns>
    /**************************************************************/
    [McpServerTool]
    [Description("Get the current user's recent activity log showing actions performed in the system.")]
    public async Task<string> GetMyActivity(
        [Description("Page number (1-based)")] int pageNumber = 1,
        [Description("Results per page (1-100)")] int pageSize = 20)
    {
        #region implementation
        _logger.LogInformation("[Tool] GetMyActivity: page={Page}, size={Size}", pageNumber, pageSize);

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        try
        {
            // First get the user's ID
            var profileResult = await _apiClient.GetStringAsync("/users/me");
            var profile = JsonSerializer.Deserialize<JsonElement>(profileResult);

            if (!profile.TryGetProperty("encryptedId", out var idElement))
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Could not determine user ID"
                });
            }

            var userId = idElement.GetString();
            var endpoint = $"/users/user/{userId}/activity?pageNumber={pageNumber}&pageSize={pageSize}";
            var result = await _apiClient.GetStringAsync(endpoint);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] GetMyActivity failed");
            return JsonSerializer.Serialize(new
            {
                error = "Failed to retrieve activity",
                message = ex.Message
            });
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the current user's activity within a date range.
    /// </summary>
    /// <param name="startDate">Start date for the activity range (ISO 8601 format).</param>
    /// <param name="endDate">End date for the activity range (ISO 8601 format).</param>
    /// <param name="pageNumber">Page number for pagination.</param>
    /// <param name="pageSize">Number of results per page.</param>
    /// <returns>List of activities within the specified date range.</returns>
    /**************************************************************/
    [McpServerTool]
    [Description("Get the current user's activity within a specific date range.")]
    public async Task<string> GetMyActivityByDateRange(
        [Description("Start date (ISO 8601 format, e.g., 2024-01-01)")] string startDate,
        [Description("End date (ISO 8601 format, e.g., 2024-12-31)")] string endDate,
        [Description("Page number (1-based)")] int pageNumber = 1,
        [Description("Results per page (1-100)")] int pageSize = 20)
    {
        #region implementation
        _logger.LogInformation("[Tool] GetMyActivityByDateRange: {Start} to {End}", startDate, endDate);

        if (!DateTime.TryParse(startDate, out var parsedStart) ||
            !DateTime.TryParse(endDate, out var parsedEnd))
        {
            return JsonSerializer.Serialize(new
            {
                error = "Invalid date format",
                message = "Please use ISO 8601 format (e.g., 2024-01-01)"
            });
        }

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        try
        {
            var profileResult = await _apiClient.GetStringAsync("/users/me");
            var profile = JsonSerializer.Deserialize<JsonElement>(profileResult);

            if (!profile.TryGetProperty("encryptedId", out var idElement))
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Could not determine user ID"
                });
            }

            var userId = idElement.GetString();
            var endpoint = $"/users/user/{userId}/activity/daterange" +
                $"?startDate={Uri.EscapeDataString(parsedStart.ToString("o"))}" +
                $"&endDate={Uri.EscapeDataString(parsedEnd.ToString("o"))}" +
                $"&pageNumber={pageNumber}&pageSize={pageSize}";

            var result = await _apiClient.GetStringAsync(endpoint);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] GetMyActivityByDateRange failed");
            return JsonSerializer.Serialize(new
            {
                error = "Failed to retrieve activity",
                message = ex.Message
            });
        }
        #endregion
    }
}
