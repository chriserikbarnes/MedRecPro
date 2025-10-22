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

        /*************************************************************/
        /// <summary>
        /// Retrieves the most recent activity log entries for a specific user.
        /// </summary>
        /// <param name="userId">The identifier of the user whose activity to retrieve.</param>
        /// <param name="count">The maximum number of log entries to return. Defaults to 100.</param>
        /// <returns>A task containing a list of activity log entries, ordered by timestamp descending.</returns>
        /// <remarks>
        /// Returns empty list if no activities found or if an error occurs.
        /// Results are ordered by ActivityTimestamp in descending order (most recent first).
        /// </remarks>
        /// <example>
        /// <code>
        /// var recentActivity = await _activityLogService.GetUserActivityAsync(123, 50);
        /// foreach (var activity in recentActivity)
        /// {
        ///     Console.WriteLine($"{activity.ActivityTimestamp}: {activity.Description}");
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        public async Task<List<ActivityLog>> GetUserActivityAsync(long userId, int count = 100)
        {
            #region Implementation
            try
            {
                return await _context.ActivityLogs
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.ActivityTimestamp)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to retrieve activity logs for user {UserId}", userId);
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

        /*************************************************************/
        /// <summary>
        /// Retrieves activity log entries for a specific controller and action.
        /// </summary>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="actionName">The name of the action method. Optional.</param>
        /// <param name="count">The maximum number of log entries to return. Defaults to 100.</param>
        /// <returns>A task containing a list of activity log entries matching the specified criteria.</returns>
        /// <remarks>
        /// If actionName is null or empty, returns all activities for the specified controller.
        /// Returns empty list if no activities found or if an error occurs.
        /// Results are ordered by ActivityTimestamp in descending order.
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
            int count = 100)
        {
            #region Implementation
            try
            {
                var query = _context.ActivityLogs
                    .Where(a => a.ControllerName == controllerName);

                // Filter by action name if provided
                if (!string.IsNullOrWhiteSpace(actionName))
                {
                    query = query.Where(a => a.ActionName == actionName);
                }

                return await query
                    .OrderByDescending(a => a.ActivityTimestamp)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to retrieve activity logs for endpoint {Controller}/{Action}",
                    controllerName, actionName ?? "All");
                return new List<ActivityLog>();
            }
            #endregion
        }

        #endregion
    }
}