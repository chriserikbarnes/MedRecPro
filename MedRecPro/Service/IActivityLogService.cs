using MedRecPro.Models;

namespace MedRecPro.Service
{
    /*************************************************************/
    /// <summary>
    /// Defines the contract for activity logging operations.
    /// </summary>
    /// <remarks>
    /// Provides methods for persisting activity logs to the database and retrieving
    /// user activity history. Implementations should handle exceptions gracefully
    /// to prevent logging failures from impacting application functionality.
    /// </remarks>
    /// <seealso cref="ActivityLog"/>
    public interface IActivityLogService
    {
        /*************************************************************/
        /// <summary>
        /// Asynchronously persists an activity log entry to the database.
        /// </summary>
        /// <param name="log">The activity log entry to persist.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method should not throw exceptions to the caller. Any database errors
        /// should be caught and logged internally to prevent activity logging from
        /// breaking the application flow.
        /// </remarks>
        /// <example>
        /// <code>
        /// var log = new ActivityLog
        /// {
        ///     UserId = 123,
        ///     ActivityType = "Login",
        ///     Description = "User logged in successfully"
        /// };
        /// await _activityLogService.LogActivityAsync(log);
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        Task LogActivityAsync(ActivityLog log);

        /*************************************************************/
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
        /// Useful for displaying user activity history, audit trails, and debugging user issues.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get first page (25 entries)
        /// var page1 = await _activityLogService.GetUserActivityAsync(userId: 123, pageSize: 25, skip: 0);
        /// 
        /// // Get second page (next 25 entries)
        /// var page2 = await _activityLogService.GetUserActivityAsync(userId: 123, pageSize: 25, skip: 25);
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        /// <seealso cref="GetUserActivityByDateRangeAsync"/>
        Task<List<ActivityLog>> GetUserActivityAsync(long userId, int pageSize, int skip = 0);

        /*************************************************************/
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
        /// Useful for generating date-specific reports and analyzing user activity patterns over specific periods.
        /// </remarks>
        /// <example>
        /// <code>
        /// var startDate = new DateTime(2024, 1, 1);
        /// var endDate = new DateTime(2024, 12, 31);
        /// 
        /// // Get first page of activities in date range
        /// var activities = await _activityLogService.GetUserActivityByDateRangeAsync(
        ///     userId: 123, 
        ///     startDate: startDate, 
        ///     endDate: endDate, 
        ///     pageSize: 50, 
        ///     skip: 0);
        /// 
        /// foreach (var activity in activities)
        /// {
        ///     Console.WriteLine($"{activity.ActivityTimestamp}: {activity.Description}");
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        /// <seealso cref="GetUserActivityAsync"/>
        Task<List<ActivityLog>> GetUserActivityByDateRangeAsync(long userId, DateTime startDate, DateTime endDate, int pageSize, int skip = 0);

        /*************************************************************/
        /// <summary>
        /// Retrieves all activity log entries within a specified time range.
        /// </summary>
        /// <param name="startTime">The start of the time range (inclusive).</param>
        /// <param name="endTime">The end of the time range (inclusive).</param>
        /// <returns>A task containing a list of activity log entries within the time range.</returns>
        /// <remarks>
        /// Useful for generating reports and analyzing activity patterns over specific periods.
        /// Returns results ordered by timestamp descending.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get activities from the last 24 hours
        /// var yesterday = DateTime.UtcNow.AddDays(-1);
        /// var activities = await _activityLogService.GetActivityByTimeRangeAsync(yesterday, DateTime.UtcNow);
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        Task<List<ActivityLog>> GetActivityByTimeRangeAsync(DateTime startTime, DateTime endTime);

        /*************************************************************/
        /// <summary>
        /// Retrieves activity log entries for a specific controller and action.
        /// </summary>
        /// <param name="controllerName">The name of the controller.</param>
        /// <param name="actionName">The name of the action method. Optional.</param>
        /// <param name="limit">The maximum number of log entries to return. Defaults to 100.</param>
        /// <returns>A task containing a list of activity log entries matching the specified criteria.</returns>
        /// <remarks>
        /// Used for analyzing usage patterns of specific endpoints and troubleshooting
        /// controller-specific issues. If actionName is null or empty, returns all activities
        /// for the specified controller. Returns empty list if no activities found or if an error occurs.
        /// Results are ordered by ActivityTimestamp in descending order (most recent first).
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get all Label controller activities (limit 50)
        /// var labelActivities = await _activityLogService.GetActivityByEndpointAsync("Labels", null, 50);
        /// 
        /// // Get specific action activities (limit 100)
        /// var createActivities = await _activityLogService.GetActivityByEndpointAsync("Labels", "Create", 100);
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        Task<List<ActivityLog>> GetActivityByEndpointAsync(string controllerName, string? actionName = null, int limit = 100);
    }
}