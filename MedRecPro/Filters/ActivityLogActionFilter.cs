using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;

namespace MedRecPro.Filters
{
    /*************************************************************/
    /// <summary>
    /// Action filter that automatically logs controller action executions to the database.
    /// </summary>
    /// <remarks>
    /// This filter captures comprehensive information about each request including:
    /// user identity, request details, controller/action information, execution time,
    /// response status, parameters, and any errors that occur. Logs are persisted
    /// asynchronously to avoid impacting response times.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Apply globally in Program.cs
    /// builder.Services.AddControllers(options =>
    /// {
    ///     options.Filters.Add&lt;ActivityLogActionFilter&gt;();
    /// });
    /// 
    /// // Or apply to specific controllers
    /// [ServiceFilter(typeof(ActivityLogActionFilter))]
    /// public class MyController : ControllerBase { }
    /// </code>
    /// </example>
    /// <seealso cref="ActivityLog"/>
    /// <seealso cref="IActivityLogService"/>
    public class ActivityLogActionFilter : IAsyncActionFilter
    {
        #region Fields

        private readonly IActivityLogService _activityLogService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ActivityLogActionFilter> _logger;

        #endregion

        #region Constructor

        /*************************************************************/
        /// <summary>
        /// Initializes a new instance of the ActivityLogActionFilter class.
        /// </summary>
        /// <param name="activityLogService">Service for persisting activity logs.</param>
        /// <param name="httpContextAccessor">Accessor for HTTP context information.</param>
        /// <param name="logger">Logger for filter-level events and errors.</param>
        /// <seealso cref="IActivityLogService"/>
        public ActivityLogActionFilter(
            IActivityLogService activityLogService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ActivityLogActionFilter> logger)
        {
            #region Implementation
            _activityLogService = activityLogService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            #endregion
        }

        #endregion

        #region IAsyncActionFilter Implementation

        /*************************************************************/
        /// <summary>
        /// Executes the filter logic to capture and log action execution details.
        /// </summary>
        /// <param name="context">The context for the action being executed.</param>
        /// <param name="next">The delegate representing the next action filter or the action itself.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// Measures execution time using Stopwatch, captures request/response details,
        /// and handles both successful executions and exceptions. Logging is performed
        /// asynchronously via fire-and-forget to minimize performance impact.
        /// </remarks>
        /// <seealso cref="ActivityLog"/>
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            #region Implementation

            var stopwatch = Stopwatch.StartNew();

            try
            {

                var httpContext = context.HttpContext;

                // Get user ID (handle both authenticated and anonymous)
                var userIdClaim = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                long userId = 0;

                // Try to parse user ID, default to 0 for anonymous
                if (!string.IsNullOrEmpty(userIdClaim))
                {
                    long.TryParse(userIdClaim, out userId);
                }

                // Execute the action
                var resultContext = await next();

                stopwatch.Stop();

                // Prepare the activity log
                var log = new ActivityLog
                {
                    UserId = userId,
                    ActivityType = getActivityType(context, resultContext),
                    ActivityTimestamp = DateTime.UtcNow,

                    // Request Details
                    IpAddress = getClientIpAddress(httpContext),
                    UserAgent = httpContext.Request.Headers["User-Agent"].ToString(),
                    RequestPath = httpContext.Request.Path,

                    // Controller/Endpoint Details
                    ControllerName = context.RouteData.Values["controller"]?.ToString(),
                    ActionName = context.RouteData.Values["action"]?.ToString(),
                    HttpMethod = httpContext.Request.Method,

                    // Parameters and Performance
                    RequestParameters = serializeParameters(context.ActionArguments),
                    ResponseStatusCode = httpContext.Response.StatusCode,
                    ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds,

                    // Result and Error Tracking
                    Result = resultContext.Exception == null ? "Success" : "Error",
                    SessionId = httpContext.Session?.Id
                };

                // Handle exceptions
                if (resultContext.Exception != null)
                {
                    log.ErrorMessage = resultContext.Exception.Message;
                    log.ExceptionType = resultContext.Exception.GetType().Name;
                    log.StackTrace = resultContext.Exception.StackTrace;
                }

                // Set description
                log.Description = $"{log.HttpMethod} {log.ControllerName}/{log.ActionName}";

                // Log asynchronously (fire and forget to not slow down the response)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _activityLogService.LogActivityAsync(log);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to persist activity log asynchronously");
                    }
                });
            }
            finally
            {
                stopwatch = null;
            }
            #endregion
        }

        #endregion

        #region Private Helper Methods

        /*************************************************************/
        /// <summary>
        /// Determines the activity type based on the request and execution context.
        /// </summary>
        /// <param name="context">The action executing context.</param>
        /// <param name="resultContext">The action executed context.</param>
        /// <returns>A string representing the activity type.</returns>
        /// <remarks>
        /// Maps HTTP methods to CRUD operations and identifies special cases
        /// like login, logout, and registration based on action names.
        /// </remarks>
        private string getActivityType(ActionExecutingContext context, ActionExecutedContext resultContext)
        {
            #region Implementation
            var method = context.HttpContext.Request.Method;
            var action = context.RouteData.Values["action"]?.ToString();

            // Custom logic for specific endpoints
            if (action?.ToLower().Contains("login") == true)
                return "Login";
            if (action?.ToLower().Contains("logout") == true)
                return "Logout";
            if (action?.ToLower().Contains("register") == true)
                return "Registration";

            // Default to HTTP method mapping
            return method switch
            {
                "GET" => "Read",
                "POST" => "Create",
                "PUT" => "Update",
                "PATCH" => "Update",
                "DELETE" => "Delete",
                _ => "Other"
            };
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Serializes action parameters to JSON format, excluding sensitive data.
        /// </summary>
        /// <param name="parameters">Dictionary of parameter names and values.</param>
        /// <returns>JSON string of sanitized parameters, or null if no parameters.</returns>
        /// <remarks>
        /// Filters out parameters with names containing: password, token, secret,
        /// key, credit, ssn. Returns error message if serialization fails.
        /// </remarks>
        private string? serializeParameters(IDictionary<string, object?> parameters)
        {
            #region Implementation
            if (parameters == null || parameters.Count == 0)
                return null;

            try
            {
                // Filter out sensitive parameters
                var sanitizedParams = parameters
                    .Where(p => !isSensitiveParameter(p.Key))
                    .ToDictionary(p => p.Key, p => p.Value);

                if (sanitizedParams.Count == 0)
                    return "[All parameters filtered - sensitive data]";

                return JsonSerializer.Serialize(sanitizedParams, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to serialize action parameters");
                return "[Unable to serialize parameters]";
            }
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Determines if a parameter name indicates sensitive data.
        /// </summary>
        /// <param name="paramName">The name of the parameter to check.</param>
        /// <returns>True if the parameter is sensitive, false otherwise.</returns>
        /// <remarks>
        /// Checks for common sensitive keywords: password, token, secret, key,
        /// credit, ssn. Case-insensitive comparison.
        /// </remarks>
        private bool isSensitiveParameter(string paramName)
        {
            #region Implementation
            // Don't log sensitive data
            var sensitiveNames = new[] { "password", "token", "secret", "key", "credit", "ssn", "apikey", "authorization" };
            return sensitiveNames.Any(s => paramName.ToLower().Contains(s));
            #endregion
        }

        /*************************************************************/
        /// <summary>
        /// Extracts the client's IP address from the HTTP context.
        /// </summary>
        /// <param name="context">The HTTP context containing request information.</param>
        /// <returns>The client IP address, or null if not available.</returns>
        /// <remarks>
        /// Checks X-Forwarded-For header first (for requests behind proxies or
        /// load balancers), falling back to RemoteIpAddress if not present.
        /// </remarks>
        private string? getClientIpAddress(HttpContext context)
        {
            #region Implementation
            // Check for forwarded IP first (if behind proxy/load balancer)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var ips = forwardedFor.Split(',');
                return ips[0].Trim();
            }

            return context.Connection.RemoteIpAddress?.ToString();
            #endregion
        }

        #endregion
    }
}