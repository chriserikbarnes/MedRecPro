/**************************************************************/
/// <summary>
/// MCP tools for user account operations including profile retrieval and activity logging.
/// </summary>
/// <remarks>
/// These tools provide Claude with the ability to access the current authenticated
/// user's profile and activity information from the MedRecPro API. All operations
/// require OAuth authentication and are scoped to the current user only.
///
/// ## Tool Workflow
///
/// ```
/// get_my_profile ──► encryptedId, name, email, roles
///        │
///        └──► (used internally by activity tools to resolve user identity)
///
/// get_my_activity ──► activity log entries (most recent first)
///        │
///        └──► get_my_activity_by_date_range ──► filtered activity log entries
/// ```
///
/// ## Common Scenarios
///
/// **View your profile:**
/// get_my_profile → returns name, email, roles
///
/// **Check recent activity:**
/// get_my_activity (page 1) → browse pages with pageNumber parameter
///
/// **Find activity in a specific period:**
/// get_my_activity_by_date_range (startDate, endDate) → filtered results
/// </remarks>
/// <seealso cref="MedRecProApiClient"/>
/**************************************************************/

using MedRecProMCP.Services;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace MedRecProMCP.Tools;

/**************************************************************/
/// <summary>
/// MCP tools for user-related operations scoped to the current authenticated user.
/// </summary>
/// <remarks>
/// The [Authorize] attribute ensures the MCP SDK returns a 401 challenge
/// with WWW-Authenticate headers when an unauthenticated client invokes
/// these tools, triggering the OAuth flow.
///
/// ## Limitations
/// - All tools return data for the **current authenticated user only**
/// - Cannot view other users' profiles or activity logs
/// - Activity logs are read-only; no create/update/delete operations
/// - Pagination is required for large activity histories
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
    /// Initializes a new instance of UserTools with required dependencies.
    /// </summary>
    /// <param name="apiClient">HTTP client for MedRecPro API calls.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /**************************************************************/
    public UserTools(MedRecProApiClient apiClient, ILogger<UserTools> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /**************************************************************/
    /// <summary>
    /// Gets the current authenticated user's profile information.
    /// </summary>
    /// <returns>
    /// JSON string containing user profile fields: encryptedId, displayName,
    /// email, userRole, timezone, locale, and account status flags.
    /// </returns>
    /// <remarks>
    /// ## Purpose
    /// Retrieves the profile of whoever is currently authenticated via OAuth.
    /// This is the identity discovery tool — use it to confirm who is logged in.
    ///
    /// ## Workflow
    /// This is an entry-point tool with no prerequisites.
    /// The encryptedId in the response is used internally by activity tools.
    ///
    /// ## Returns
    /// JSON with: encryptedId, displayName, email, userRole, timezone, locale,
    /// phoneNumber, mfaEnabled, createdDate, lastLoginDate.
    /// </remarks>
    /**************************************************************/
    [McpServerTool(Name = "get_my_profile")]
    [Description("""
        👤 RETRIEVE: Get the current authenticated user's profile information.

        📋 WORKFLOW: Start here to identify who is logged in.
        ├── Returns: displayName, email, userRole, timezone, locale, account status
        ├── Related: Use 'get_my_activity' to see what this user has done
        └── Related: Use 'get_my_activity_by_date_range' to see activity in a time period

        ⚠️ LIMITATIONS:
        • Returns data for the CURRENT USER ONLY (whoever authenticated via OAuth)
        • Cannot look up other users' profiles

        🎯 EXAMPLE QUESTIONS THAT TRIGGER THIS TOOL:
        • "Who am I logged in as?"
        • "What is my email address?"
        • "What role do I have?"
        • "Show me my profile"
        • "What's my account information?"
        """)]
    public async Task<string> GetMyProfile()
    {
        #region implementation
        _logger.LogInformation("[Tool] GetMyProfile");

        try
        {
            var endpoint = "api/users/me";
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
    /// Gets the current user's recent activity log with pagination.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The number of activity entries per page (1-100).</param>
    /// <returns>
    /// JSON array of activity log entries ordered by timestamp descending (most recent first).
    /// Each entry includes: activityType, controllerName, actionName, description,
    /// activityTimestamp, executionTimeMs, ipAddress.
    /// </returns>
    /// <remarks>
    /// ## Purpose
    /// Retrieves a paginated list of actions the current user has performed in MedRecPro.
    /// Results are sorted most-recent-first. Internally resolves the user's encrypted ID
    /// from their profile before fetching activity.
    ///
    /// ## Workflow
    /// Can be called independently — no prerequisite tools needed.
    /// For date-filtered results, use 'get_my_activity_by_date_range' instead.
    ///
    /// ## Returns
    /// Array of activity log DTOs. Each page returns up to pageSize entries.
    /// If fewer results than pageSize are returned, you've reached the last page.
    ///
    /// ## Pagination
    /// - Page 1 is the default (most recent activity)
    /// - Increase pageNumber to see older activity
    /// - If a page returns fewer results than pageSize, there are no more pages
    /// - Maximum 100 results per page
    /// </remarks>
    /**************************************************************/
    [McpServerTool(Name = "get_my_activity")]
    [Description("""
        📋 LIST: Get the current user's recent activity log showing actions performed in the system.

        📋 WORKFLOW: Use to review your own recent actions in MedRecPro.
        ├── Returns: activityType, controllerName, actionName, description, timestamp, executionTimeMs
        ├── Related: Use 'get_my_activity_by_date_range' to filter by specific dates
        └── Ordered: Most recent activity first (descending by timestamp)

        ⚠️ LIMITATIONS:
        • Returns activity for the CURRENT USER ONLY
        • Cannot view other users' activity logs
        • Read-only — no create/update/delete of activity entries
        • Maximum 100 results per page

        📄 PAGINATION:
        • pageNumber=1 returns the most recent activity (default)
        • Increase pageNumber to see older activity
        • If results returned < pageSize, you are on the last page
        • Example: pageNumber=2, pageSize=20 → skips the 20 most recent, shows next 20

        🎯 EXAMPLE QUESTIONS THAT TRIGGER THIS TOOL:
        • "What have I done recently?"
        • "Show me my activity log"
        • "What actions have I performed?"
        • "Show my recent actions in the system"

        🎯 EXAMPLE QUESTIONS THAT TRIGGER PAGING:
        • "Show me more activity" → increment pageNumber by 1
        • "Show me older activity" → increment pageNumber by 1
        • "Go to page 3 of my activity" → pageNumber=3
        • "Show me the next page" → increment pageNumber by 1
        """)]
    public async Task<string> GetMyActivity(
        [Description("Page number, 1-based. Page 1 = most recent activity. Increase to see older entries. Default: 1")]
        [Range(1, int.MaxValue)]
        int pageNumber = 1,

        [Description("Results per page (1-100). Fewer results than this value indicates the last page. Default: 20")]
        [Range(1, 100)]
        int pageSize = 20)
    {
        #region implementation
        _logger.LogInformation("[Tool] GetMyActivity: page={Page}, size={Size}", pageNumber, pageSize);

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        try
        {
            // First get the user's ID
            var profileResult = await _apiClient.GetStringAsync("api/users/me");
            var profile = JsonSerializer.Deserialize<JsonElement>(profileResult);

            if (!profile.TryGetProperty("encryptedUserId", out var idElement))
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Could not determine user ID"
                });
            }

            var userId = idElement.GetString();
            var endpoint = $"api/users/user/{userId}/activity?pageNumber={pageNumber}&pageSize={pageSize}";
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
    /// Gets the current user's activity within a specific date range.
    /// </summary>
    /// <param name="startDate">The inclusive start date for filtering (ISO 8601 format).</param>
    /// <param name="endDate">The inclusive end date for filtering (ISO 8601 format).</param>
    /// <param name="pageNumber">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The number of activity entries per page (1-100).</param>
    /// <returns>
    /// JSON array of activity log entries filtered to the specified date range,
    /// ordered by timestamp descending (most recent first).
    /// </returns>
    /// <remarks>
    /// ## Purpose
    /// Retrieves activity for the current user filtered to a specific time period.
    /// Use when the user asks about what they did on a particular day, week, or month.
    ///
    /// ## Workflow
    /// Can be called independently — no prerequisite tools needed.
    /// For unfiltered recent activity, use 'get_my_activity' instead.
    ///
    /// ## Returns
    /// Same structure as get_my_activity but filtered to the date range.
    /// Both startDate and endDate are inclusive. Maximum date range is 365 days.
    ///
    /// ## Pagination
    /// Same pagination behavior as get_my_activity.
    /// </remarks>
    /**************************************************************/
    [McpServerTool(Name = "get_my_activity_by_date_range")]
    [Description("""
        📋 LIST: Get the current user's activity within a specific date range.

        📋 WORKFLOW: Use when the user asks about activity during a specific time period.
        ├── Returns: activityType, controllerName, actionName, description, timestamp, executionTimeMs
        ├── Filtered: Only includes activity between startDate and endDate (both inclusive)
        ├── Ordered: Most recent activity first (descending by timestamp)
        └── Related: Use 'get_my_activity' for recent activity without date filtering

        ⚠️ LIMITATIONS:
        • Returns activity for the CURRENT USER ONLY
        • Maximum date range: 365 days
        • Both startDate and endDate are required
        • startDate must be before or equal to endDate
        • Maximum 100 results per page

        📄 PAGINATION:
        • Same behavior as 'get_my_activity'
        • pageNumber=1 returns the most recent activity within the date range
        • If results returned < pageSize, you are on the last page

        🎯 EXAMPLE QUESTIONS THAT TRIGGER THIS TOOL:
        • "What did I do last week?"
        • "Show my activity from January 2025"
        • "What actions did I perform between March 1 and March 15?"
        • "Show me what I did yesterday"
        • "Any activity from last month?"

        🎯 EXAMPLE QUESTIONS THAT TRIGGER PAGING:
        • "Show me more activity from that date range" → increment pageNumber by 1
        • "Next page of January activity" → increment pageNumber by 1
        """)]
    public async Task<string> GetMyActivityByDateRange(
        [Description("Start date (inclusive). Format: ISO 8601 (yyyy-MM-dd). Example: '2025-01-01'")]
        [Required]
        string startDate,

        [Description("End date (inclusive). Format: ISO 8601 (yyyy-MM-dd). Must be >= startDate. Max 365 days range. Example: '2025-01-31'")]
        [Required]
        string endDate,

        [Description("Page number, 1-based. Page 1 = most recent activity in range. Default: 1")]
        [Range(1, int.MaxValue)]
        int pageNumber = 1,

        [Description("Results per page (1-100). Fewer results than this value indicates the last page. Default: 20")]
        [Range(1, 100)]
        int pageSize = 20)
    {
        #region implementation
        _logger.LogInformation("[Tool] GetMyActivityByDateRange: start={Start}, end={End}, page={Page}, size={Size}",
            startDate, endDate, pageNumber, pageSize);

        // Validate date formats
        if (!DateTime.TryParse(startDate, out var parsedStart))
        {
            return JsonSerializer.Serialize(new
            {
                error = "Invalid startDate format. Use ISO 8601 format (yyyy-MM-dd). Example: '2025-01-01'"
            });
        }

        if (!DateTime.TryParse(endDate, out var parsedEnd))
        {
            return JsonSerializer.Serialize(new
            {
                error = "Invalid endDate format. Use ISO 8601 format (yyyy-MM-dd). Example: '2025-01-31'"
            });
        }

        if (parsedStart > parsedEnd)
        {
            return JsonSerializer.Serialize(new
            {
                error = "startDate must be before or equal to endDate"
            });
        }

        if ((parsedEnd - parsedStart).TotalDays > 365)
        {
            return JsonSerializer.Serialize(new
            {
                error = "Date range cannot exceed 365 days"
            });
        }

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        try
        {
            // First get the user's ID
            var profileResult = await _apiClient.GetStringAsync("api/users/me");
            var profile = JsonSerializer.Deserialize<JsonElement>(profileResult);

            if (!profile.TryGetProperty("encryptedUserId", out var idElement))
            {
                return JsonSerializer.Serialize(new
                {
                    error = "Could not determine user ID"
                });
            }

            var userId = idElement.GetString();
            var endpoint = $"api/users/user/{userId}/activity/daterange?startDate={parsedStart:yyyy-MM-dd}&endDate={parsedEnd:yyyy-MM-dd}&pageNumber={pageNumber}&pageSize={pageSize}";
            var result = await _apiClient.GetStringAsync(endpoint);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Tool] GetMyActivityByDateRange failed");
            return JsonSerializer.Serialize(new
            {
                error = "Failed to retrieve activity by date range",
                message = ex.Message
            });
        }
        #endregion
    }

}
