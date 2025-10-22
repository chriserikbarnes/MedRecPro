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
        /// Retrieves the most recent activity log entries for a specific user.
        /// </summary>
        /// <param name="userId">The identifier of the user whose activity to retrieve.</param>
        /// <param name="count">The maximum number of log entries to return. Defaults to 100.</param>
        /// <returns>A task containing a list of activity log entries, ordered by timestamp descending.</returns>
        /// <remarks>
        /// Returns the most recent activities first. Useful for displaying user activity
        /// history, audit trails, and debugging user issues.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get last 50 activities for user
        /// var activities = await _activityLogService.GetUserActivityAsync(userId: 123, count: 50);
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        Task<List<ActivityLog>> GetUserActivityAsync(long userId, int count = 100);

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
        /// <param name="count">The maximum number of log entries to return. Defaults to 100.</param>
        /// <returns>A task containing a list of activity log entries matching the specified criteria.</returns>
        /// <remarks>
        /// Used for analyzing usage patterns of specific endpoints and troubleshooting
        /// controller-specific issues. If actionName is null, returns all activities
        /// for the specified controller.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get all Label controller activities
        /// var labelActivities = await _activityLogService.GetActivityByEndpointAsync("Labels", null, 50);
        /// 
        /// // Get specific action activities
        /// var createActivities = await _activityLogService.GetActivityByEndpointAsync("Labels", "Create", 50);
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        Task<List<ActivityLog>> GetActivityByEndpointAsync(string controllerName, string? actionName = null, int count = 100);
    }
}