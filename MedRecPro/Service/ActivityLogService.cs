using MedRecPro.Data;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;

namespace MedRecPro.Service
{
    /*************************************************************/
    /// <summary>
    /// Provides database operations for activity logging functionality.
    /// </summary>
    /// <remarks>
    /// Implements IActivityLogService to persist and retrieve activity logs from
    /// the AspNetUserActivityLog table. Includes error handling to ensure logging
    /// failures do not impact application functionality.
    /// </remarks>
    /// <seealso cref="IActivityLogService"/>
    /// <seealso cref="ActivityLog"/>
    /// <seealso cref="ApplicationDbContext"/>
    public class ActivityLogService : IActivityLogService
    {
        #region Fields

        private readonly ApplicationDbContext _context;
        private readonly ILogger<ActivityLogService> _logger;

        #endregion

        #region Constructor

        /*************************************************************/
        /// <summary>
        /// Initializes a new instance of the ActivityLogService class.
        /// </summary>
        /// <param name="context">The database context for accessing activity logs.</param>
        /// <param name="logger">The logger for recording service-level events and errors.</param>
        /// <seealso cref="ApplicationDbContext"/>
        public ActivityLogService(
            ApplicationDbContext context,
            ILogger<ActivityLogService> logger)
        {
            #region Implementation
            _context = context;
            _logger = logger;
            #endregion
        }

        #endregion

        #region Public Methods

        /*************************************************************/
        /// <summary>
        /// Asynchronously persists an activity log entry to the database.
        /// </summary>
        /// <param name="log">The activity log entry to persist.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Sets the ActivityTimestamp to current UTC time before saving.
        /// Catches and logs any exceptions to prevent logging failures from
        /// disrupting the application. Does not rethrow exceptions.
        /// </remarks>
        /// <example>
        /// <code>
        /// var log = new ActivityLog
        /// {
        ///     UserId = 123,
        ///     ActivityType = "Create",
        ///     ControllerName = "Labels",
        ///     ActionName = "Create"
        /// };
        /// await _activityLogService.LogActivityAsync(log);
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        public async Task LogActivityAsync(ActivityLog log)
        {
            #region Implementation
            try
            {
                // Ensure timestamp is set
                if (log.ActivityTimestamp == default)
                {
                    log.ActivityTimestamp = DateTime.UtcNow;
                }

                _context.ActivityLogs.Add(log);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Activity logged: {ActivityType} - {Description}",
                    log.ActivityType, log.Description);
            }
            catch (Exception ex)
            {
                // Don't let logging failures break the application
                _logger.LogError(ex,
                    "Failed to log activity: {ActivityType} - {Description}",
                    log.ActivityType, log.Description);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves activity log entries for a specific user with paging support.
        /// </summary>
        /// <param name="userId">The identifier of the user whose activity to retrieve.</param>
        /// <param name="pageSize">The maximum number of log entries to return.</param>
        /// <param name="skip">The number of entries to skip for paging. Defaults to 0.</param>
        /// <returns>A task containing a list of activity log entries, ordered by timestamp descending.</returns>
        /// <remarks>
        /// Returns empty list if no activities found or if an error occurs.
        /// Results are ordered by ActivityTimestamp in descending order (most recent first).
        /// Use skip parameter for pagination by calculating: skip = (pageNumber - 1) * pageSize.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get first page (25 entries)
        /// var page1 = await _activityLogService.GetUserActivityAsync(123, 25, 0);
        /// 
        /// // Get second page (next 25 entries)
        /// var page2 = await _activityLogService.GetUserActivityAsync(123, 25, 25);
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        /// <seealso cref="GetUserActivityByDateRangeAsync"/>
        public async Task<List<ActivityLog>> GetUserActivityAsync(long userId, int pageSize, int skip = 0)
        {
            #region implementation
            try
            {
                // Validate parameters
                if (userId <= 0)
                {
                    _logger.LogWarning("GetUserActivityAsync called with invalid userId: {UserId}", userId);
                    return new List<ActivityLog>();
                }

                if (pageSize <= 0)
                {
                    _logger.LogWarning("GetUserActivityAsync called with invalid pageSize: {PageSize}", pageSize);
                    return new List<ActivityLog>();
                }

                if (skip < 0)
                {
                    _logger.LogWarning("GetUserActivityAsync called with invalid skip: {Skip}", skip);
                    skip = 0; // Reset to 0 if negative
                }

                return await _context.ActivityLogs
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.ActivityTimestamp)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to retrieve activity logs for user {UserId} with pageSize {PageSize} and skip {Skip}",
                    userId, pageSize, skip);
                return new List<ActivityLog>();
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves activity log entries for a specific user filtered by date range with paging support.
        /// </summary>
        /// <param name="userId">The identifier of the user whose activity to retrieve.</param>
        /// <param name="startDate">The start date for filtering activity logs (inclusive).</param>
        /// <param name="endDate">The end date for filtering activity logs (inclusive).</param>
        /// <param name="pageSize">The maximum number of log entries to return.</param>
        /// <param name="skip">The number of entries to skip for paging. Defaults to 0.</param>
        /// <returns>A task containing a list of activity log entries within the date range, ordered by timestamp descending.</returns>
        /// <remarks>
        /// Returns empty list if no activities found within the date range or if an error occurs.
        /// Results are ordered by ActivityTimestamp in descending order (most recent first).
        /// Both startDate and endDate are inclusive. The time component is considered for comparison.
        /// Use skip parameter for pagination by calculating: skip = (pageNumber - 1) * pageSize.
        /// </remarks>
        /// <example>
        /// <code>
        /// var startDate = new DateTime(2024, 1, 1);
        /// var endDate = new DateTime(2024, 12, 31);
        /// 
        /// // Get first page of activities in date range
        /// var activities = await _activityLogService.GetUserActivityByDateRangeAsync(123, startDate, endDate, 50, 0);
        /// 
        /// foreach (var activity in activities)
        /// {
        ///     Console.WriteLine($"{activity.ActivityTimestamp}: {activity.Description}");
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        /// <seealso cref="GetUserActivityAsync"/>
        public async Task<List<ActivityLog>> GetUserActivityByDateRangeAsync(
            long userId,
            DateTime startDate,
            DateTime endDate,
            int pageSize,
            int skip = 0)
        {
            #region implementation
            try
            {
                // Validate parameters
                if (userId <= 0)
                {
                    _logger.LogWarning("GetUserActivityByDateRangeAsync called with invalid userId: {UserId}", userId);
                    return new List<ActivityLog>();
                }

                if (startDate > endDate)
                {
                    _logger.LogWarning("GetUserActivityByDateRangeAsync called with invalid date range: {StartDate} to {EndDate}",
                        startDate, endDate);
                    return new List<ActivityLog>();
                }

                if (pageSize <= 0)
                {
                    _logger.LogWarning("GetUserActivityByDateRangeAsync called with invalid pageSize: {PageSize}", pageSize);
                    return new List<ActivityLog>();
                }

                if (skip < 0)
                {
                    _logger.LogWarning("GetUserActivityByDateRangeAsync called with invalid skip: {Skip}", skip);
                    skip = 0; // Reset to 0 if negative
                }

                return await _context.ActivityLogs
                    .Where(a => a.UserId == userId
                        && a.ActivityTimestamp >= startDate
                        && a.ActivityTimestamp <= endDate)
                    .OrderByDescending(a => a.ActivityTimestamp)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to retrieve activity logs for user {UserId} with date range {StartDate} to {EndDate}, pageSize {PageSize} and skip {Skip}",
                    userId, startDate, endDate, pageSize, skip);
                return new List<ActivityLog>();
            }
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Retrieves all activity log entries within a specified time range.
        /// </summary>
        /// <param name="startTime">The start of the time range (inclusive).</param>
        /// <param name="endTime">The end of the time range (inclusive).</param>
        /// <returns>A task containing a list of activity log entries within the time range.</returns>
        /// <remarks>
        /// Returns empty list if no activities found or if an error occurs.
        /// Both startTime and endTime are inclusive. Results are ordered by
        /// ActivityTimestamp in descending order.
        /// </remarks>
        /// <example>
        /// <code>
        /// var start = new DateTime(2025, 1, 1);
        /// var end = new DateTime(2025, 1, 31);
        /// var januaryActivities = await _activityLogService.GetActivityByTimeRangeAsync(start, end);
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        public async Task<List<ActivityLog>> GetActivityByTimeRangeAsync(DateTime startTime, DateTime endTime)
        {
            #region Implementation
            try
            {
                return await _context.ActivityLogs
                    .Where(a => a.ActivityTimestamp >= startTime && a.ActivityTimestamp <= endTime)
                    .OrderByDescending(a => a.ActivityTimestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to retrieve activity logs for time range {StartTime} to {EndTime}",
                    startTime, endTime);
                return new List<ActivityLog>();
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves activity log entries for a specific controller and action.
        /// </summary>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="actionName">The name of the action method. Optional.</param>
        /// <param name="limit">The maximum number of log entries to return. Defaults to 100.</param>
        /// <returns>A task containing a list of activity log entries matching the specified criteria.</returns>
        /// <remarks>
        /// If actionName is null or empty, returns all activities for the specified controller.
        /// Returns empty list if no activities found or if an error occurs.
        /// Results are ordered by ActivityTimestamp in descending order (most recent first).
        /// Used for analyzing usage patterns of specific endpoints and troubleshooting controller-specific issues.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get all activities for Labels controller
        /// var labelActivities = await _activityLogService.GetActivityByEndpointAsync("Labels");
        /// 
        /// // Get activities for specific action
        /// var createActivities = await _activityLogService.GetActivityByEndpointAsync("Labels", "Create");
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        public async Task<List<ActivityLog>> GetActivityByEndpointAsync(
            string controllerName,
            string? actionName = null,
            int limit = 100)
        {
            #region implementation
            try
            {
                // Validate parameters
                if (string.IsNullOrWhiteSpace(controllerName))
                {
                    _logger.LogWarning("GetActivityByEndpointAsync called with null or empty controllerName");
                    return new List<ActivityLog>();
                }

                if (limit <= 0)
                {
                    _logger.LogWarning("GetActivityByEndpointAsync called with invalid limit: {Limit}", limit);
                    return new List<ActivityLog>();
                }

                // Build query starting with controller name filter
                var query = _context.ActivityLogs
                    .Where(a => a.ControllerName == controllerName);

                // Filter by action name if provided
                if (!string.IsNullOrWhiteSpace(actionName))
                {
                    query = query.Where(a => a.ActionName == actionName);
                }

                // Execute query with ordering and limit
                return await query
                    .OrderByDescending(a => a.ActivityTimestamp)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to retrieve activity logs for endpoint {Controller}/{Action} with limit {Limit}",
                    controllerName, actionName ?? "All", limit);
                return new List<ActivityLog>();
            }
            #endregion
        }

        #endregion
    }
}