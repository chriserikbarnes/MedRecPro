using MedRecPro.Helpers;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace MedRecPro.Models
{
    /*************************************************************/
    /// <summary>
    /// Represents a comprehensive activity log entry for tracking user actions, 
    /// controller execution, and system events in the database.
    /// </summary>
    /// <remarks>
    /// This model persists activity data to the AspNetUserActivityLog table and provides
    /// detailed tracking of user interactions, request/response details, performance metrics,
    /// and error information. Used in conjunction with ActivityLogActionFilter for automatic
    /// controller action logging.
    /// </remarks>
    /// <seealso cref="User"/>
    [Table("AspNetUserActivityLog")]
    public class ActivityLog
    {
        #region Primary Key

        /*************************************************************/
        /// <summary>
        /// Gets or sets the unique identifier for the activity log entry.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ActivityLogId { get; set; }

        #endregion

        #region User and Activity Information

        /*************************************************************/
        /// <summary>
        /// Gets or sets the identifier of the user who performed the activity.
        /// </summary>
        /// <remarks>
        /// Foreign key reference to AspNetUsers table. For anonymous requests, 
        /// this may contain "Anonymous" or a similar indicator depending on implementation.
        /// </remarks>
        /// <seealso cref="User"/>
        [Required]
        public long UserId { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the type of activity performed.
        /// </summary>
        /// <remarks>
        /// Common values include: Login, Logout, Registration, Create, Read, Update, Delete, Other.
        /// </remarks>
        /// <example>
        /// "Login", "Create", "Update", "Delete"
        /// </example>
        [Required]
        [MaxLength(100)]
        public string ActivityType { get; set; } = string.Empty;

        /*************************************************************/
        /// <summary>
        /// Gets or sets the UTC timestamp when the activity occurred.
        /// </summary>
        /// <remarks>
        /// Automatically set to UTC time when the log is created. Used for temporal analysis
        /// and activity timeline reconstruction.
        /// </remarks>
        [Required]
        public DateTime ActivityTimestamp { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets a human-readable description of the activity.
        /// </summary>
        /// <remarks>
        /// Typically formatted as "{HttpMethod} {ControllerName}/{ActionName}" but can be
        /// customized for specific scenarios.
        /// </remarks>
        /// <example>
        /// "GET Labels/GetById", "POST Auth/Login"
        /// </example>
        [MaxLength(500)]
        public string? Description { get; set; }

        #endregion

        #region Request Details

        /*************************************************************/
        /// <summary>
        /// Gets or sets the IP address of the client making the request.
        /// </summary>
        /// <remarks>
        /// Supports both IPv4 and IPv6 addresses. Checks X-Forwarded-For header
        /// for requests behind proxies or load balancers.
        /// </remarks>
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the User-Agent string from the HTTP request header.
        /// </summary>
        /// <remarks>
        /// Contains browser/client information useful for analytics and debugging.
        /// </remarks>
        [MaxLength(500)]
        public string? UserAgent { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the request path (URL path component).
        /// </summary>
        /// <remarks>
        /// Does not include query string parameters. Example: "/api/Labels/123"
        /// </remarks>
        [MaxLength(500)]
        public string? RequestPath { get; set; }

        #endregion

        #region Controller and Endpoint Details

        /*************************************************************/
        /// <summary>
        /// Gets or sets the name of the controller that handled the request.
        /// </summary>
        /// <remarks>
        /// Extracted from route data. Example: "Labels", "Auth", "Users"
        /// </remarks>
        [MaxLength(100)]
        public string? ControllerName { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the name of the action method that was executed.
        /// </summary>
        /// <remarks>
        /// Extracted from route data. Example: "GetById", "Create", "Update"
        /// </remarks>
        [MaxLength(100)]
        public string? ActionName { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the HTTP method used for the request.
        /// </summary>
        /// <remarks>
        /// Standard HTTP methods: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS
        /// </remarks>
        /// <example>
        /// "GET", "POST", "PUT", "DELETE"
        /// </example>
        [MaxLength(10)]
        public string? HttpMethod { get; set; }

        #endregion

        #region Parameters and Performance

        /*************************************************************/
        /// <summary>
        /// Gets or sets the request parameters serialized as JSON.
        /// </summary>
        /// <remarks>
        /// Contains action method parameters, excluding sensitive data such as passwords,
        /// tokens, and secrets. Stored as JSON for flexibility and queryability.
        /// </remarks>
        [Column(TypeName = "nvarchar(max)")]
        public string? RequestParameters { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the HTTP response status code.
        /// </summary>
        /// <remarks>
        /// Standard HTTP status codes: 200 (OK), 201 (Created), 400 (Bad Request),
        /// 401 (Unauthorized), 404 (Not Found), 500 (Internal Server Error), etc.
        /// </remarks>
        /// <example>
        /// 200, 201, 400, 404, 500
        /// </example>
        public int? ResponseStatusCode { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the execution time of the action in milliseconds.
        /// </summary>
        /// <remarks>
        /// Measured from action execution start to completion. Useful for performance
        /// analysis and identifying slow endpoints.
        /// </remarks>
        public int? ExecutionTimeMs { get; set; }

        #endregion

        #region Result and Error Tracking

        /*************************************************************/
        /// <summary>
        /// Gets or sets the overall result status of the operation.
        /// </summary>
        /// <remarks>
        /// Typical values: "Success", "Error", "Warning". Used for high-level
        /// filtering and reporting.
        /// </remarks>
        /// <example>
        /// "Success", "Error"
        /// </example>
        [MaxLength(50)]
        public string? Result { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the error message if an exception occurred.
        /// </summary>
        /// <remarks>
        /// Contains the Exception.Message property. Only populated when an error occurs.
        /// </remarks>
        /// <seealso cref="ExceptionType"/>
        /// <seealso cref="StackTrace"/>
        [Column(TypeName = "nvarchar(max)")]
        public string? ErrorMessage { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the type name of the exception that occurred.
        /// </summary>
        /// <remarks>
        /// Contains the full type name (e.g., "System.NullReferenceException",
        /// "System.InvalidOperationException"). Useful for categorizing errors.
        /// </remarks>
        /// <seealso cref="ErrorMessage"/>
        /// <seealso cref="StackTrace"/>
        [MaxLength(200)]
        public string? ExceptionType { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the stack trace of the exception.
        /// </summary>
        /// <remarks>
        /// Full stack trace for debugging purposes. Only captured when an exception occurs.
        /// Can be very long for deeply nested call stacks.
        /// </remarks>
        /// <seealso cref="ErrorMessage"/>
        /// <seealso cref="ExceptionType"/>
        [Column(TypeName = "nvarchar(max)")]
        public string? StackTrace { get; set; }

        #endregion

        #region Additional Context

        /*************************************************************/
        /// <summary>
        /// Gets or sets the session identifier associated with the request.
        /// </summary>
        /// <remarks>
        /// Used to correlate multiple requests within the same user session.
        /// Only available if session middleware is enabled.
        /// </remarks>
        [MaxLength(100)]
        public string? SessionId { get; set; }

        #endregion

        #region Navigation Properties

        /*************************************************************/
        /// <summary>
        /// Gets or sets the User entity associated with this activity log.
        /// </summary>
        /// <remarks>
        /// Navigation property for Entity Framework. Enables eager loading of user data.
        /// </remarks>
        /// <seealso cref="UserId"/>
        /// <seealso cref="User"/>
        //[JsonIgnore]
        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        #endregion
    }

    /*************************************************************/
    /// <summary>
    /// Data Transfer Object for activity log entries with encrypted identifiers 
    /// and user information for secure client transmission.
    /// </summary>
    /// <remarks>
    /// This DTO provides a sanitized view of activity log data suitable for API responses.
    /// The ActivityLogId is encrypted to prevent enumeration attacks and unauthorized access.
    /// Includes associated user information (Email, DisplayName) for display purposes without
    /// exposing the full User entity. Sensitive information is excluded or masked.
    /// </remarks>
    /// <seealso cref="ActivityLog"/>
    /// <seealso cref="User"/>
    public class ActivityLogDto
    {
        #region Constructors

        /*************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityLogDto"/> class.
        /// </summary>
        /// <remarks>
        /// Parameterless constructor for object initialization, serialization, and model binding.
        /// All properties are set to their default values.
        /// </remarks>
        public ActivityLogDto()
        {
        }

        /*************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityLogDto"/> class from an ActivityLog entity.
        /// </summary>
        /// <param name="activityLog">The source ActivityLog entity to map from.</param>
        /// <remarks>
        /// Performs the transformation from ActivityLog entity to DTO, including:
        /// - Converting ActivityLogId to string format (note: encryption must be applied separately)
        /// - Denormalizing User.Email and User.DisplayName with null-coalescing
        /// - Mapping all relevant activity, request, and response properties
        /// This constructor does NOT perform ID encryption - that should be handled by 
        /// calling code using ToEntityWithEncryptedId or similar extension methods.
        /// </remarks>
        /// <example>
        /// <code>
        /// var activityLog = await _context.ActivityLogs
        ///     .Include(a => a.User)
        ///     .FirstOrDefaultAsync(a => a.ActivityLogId == id);
        /// var dto = new ActivityLogDto(activityLog);
        /// </code>
        /// </example>
        /// <seealso cref="ActivityLog"/>
        /// <seealso cref="User"/>
        public ActivityLogDto(ActivityLog activityLog)
        {
            #region null check
            if (activityLog == null)
            {
                throw new ArgumentNullException(nameof(activityLog), "ActivityLog cannot be null.");
            }
            #endregion

            #region identification mapping
            // Note: Id is set as string representation of ActivityLogId
            // Encryption should be applied separately via extension methods
            Id = activityLog.ActivityLogId.ToString();
            UserId = activityLog.UserId;
            #endregion

            #region user information mapping
            // Denormalize user data with null-coalescing to handle missing User navigation property
            Email = activityLog.User?.Email ?? string.Empty;
            DisplayName = activityLog.User?.DisplayName ?? string.Empty;
            #endregion

            #region activity details mapping
            ActivityType = activityLog.ActivityType;
            ActivityTimestamp = activityLog.ActivityTimestamp;
            Description = activityLog.Description;
            #endregion

            #region request information mapping
            IpAddress = activityLog.IpAddress;
            UserAgent = activityLog.UserAgent;
            RequestPath = activityLog.RequestPath;
            HttpMethod = activityLog.HttpMethod;
            RequestParameters = activityLog.RequestParameters;
            #endregion

            #region controller and endpoint mapping
            ControllerName = activityLog.ControllerName;
            ActionName = activityLog.ActionName;
            #endregion

            #region response and performance mapping
            ResponseStatusCode = activityLog.ResponseStatusCode;
            ExecutionTimeMs = activityLog.ExecutionTimeMs;
            #endregion

            #region result and error mapping
            Result = activityLog.Result;
            ErrorMessage = activityLog.ErrorMessage;
            #endregion
        }

        #endregion

        #region Factory Methods


        /*************************************************************/
        /// <summary>
        /// Creates a collection of encrypted dictionary representations from a list of ActivityLog entities
        /// for secure API transmission.
        /// </summary>
        /// <param name="activityLogs">The collection of ActivityLog entities to transform.</param>
        /// <param name="pkSecret">The private key secret used for encrypting activity log identifiers.</param>
        /// <param name="logger">The logger instance for recording transformation errors and warnings.</param>
        /// <returns>
        /// A list of dictionaries containing activity log data with encrypted IDs. Each dictionary contains
        /// key-value pairs representing the DTO properties, with the Id field encrypted.
        /// Returns an empty list if the input collection is null or empty.
        /// </returns>
        /// <remarks>
        /// This static factory method provides a convenient way to bulk-transform ActivityLog 
        /// entities to secure dictionary representations in a single operation. The transformation includes:
        /// - Denormalization of user information (Email, DisplayName) with null-coalescing
        /// - Mapping of all activity, request, response, and error properties
        /// - Automatic encryption of ActivityLogId values using the provided secret key
        /// - Conversion to Dictionary&lt;string, object?&gt; format for flexible serialization
        /// The encryption is performed by calling ToEntityWithEncryptedId on each individual DTO instance,
        /// which protects against enumeration attacks and obscures internal database identifiers.
        /// All parameters are validated to prevent null reference exceptions during transformation.
        /// The returned dictionaries are suitable for direct JSON serialization in API responses.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get user activities and transform to secure dictionary representations
        /// var activities = await _activityLogService.GetUserActivityAsync(userId, 50);
        /// var securedData = ActivityLogDto.FromActivityLogs(activities, _pkSecret, _logger);
        /// return Ok(securedData);
        /// </code>
        /// </example>
        /// <exception cref="ArgumentNullException">
        /// Thrown when pkSecret or logger parameters are null.
        /// </exception>
        /// <seealso cref="ActivityLog"/>
        /// <seealso cref="ActivityLogDto(ActivityLog)"/>
        public static List<Dictionary<string, object?>> FromActivityLogs(List<ActivityLog> activityLogs, string pkSecret, ILogger logger)
        {
            #region parameter validation
            if (string.IsNullOrWhiteSpace(pkSecret))
            {
                throw new ArgumentNullException(nameof(pkSecret), "Encryption secret key cannot be null or empty.");
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger), "Logger instance cannot be null.");
            }
            #endregion

            #region null or empty collection check
            if (activityLogs == null || activityLogs.Count == 0)
            {
                logger.LogInformation("FromActivityLogs called with null or empty collection. Returning empty list.");
                return new List<Dictionary<string, object?>>();
            }
            #endregion

            #region transformation with encryption
            try
            {
                // Transform entities to DTOs and apply ID encryption to each instance
                // ToEntityWithEncryptedId returns Dictionary<string, object?> for each DTO
                var securedData = activityLogs
                    .Select(a => new ActivityLogDto(a).ToEntityWithEncryptedId(pkSecret, logger))
                    .ToList();

                logger.LogDebug("Successfully transformed {Count} ActivityLog entities to encrypted dictionary representations.", activityLogs.Count);
                return securedData;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during ActivityLog to dictionary transformation with encryption.");
                throw;
            }
            #endregion
        }

        #endregion

        #region Identification

        /*************************************************************/
        /// <summary>
        /// Gets or sets the encrypted unique identifier for the activity log entry.
        /// </summary>
        /// <remarks>
        /// This is the ActivityLogId from the source entity, encrypted for secure transmission.
        /// The encryption prevents enumeration attacks and obscures the internal database identifiers.
        /// Must be decrypted server-side before querying the database.
        /// </remarks>
        /// <example>
        /// "AQIDBAUGBwgJCgsMDQ4PEA=="
        /// </example>
        /// <seealso cref="ActivityLog.ActivityLogId"/>
        [Required]
        public string Id { get; set; } = string.Empty;

        /*************************************************************/
        /// <summary>
        /// Gets or sets the identifier of the user who performed the activity.
        /// </summary>
        /// <remarks>
        /// Reference to the user in AspNetUsers table. This allows client-side 
        /// filtering and grouping of activities by user.
        /// </remarks>
        /// <seealso cref="ActivityLog.UserId"/>
        /// <seealso cref="User"/>
        [Required]
        public long UserId { get; set; }

        #endregion

        #region User Information

        /*************************************************************/
        /// <summary>
        /// Gets or sets the email address of the user who performed the activity.
        /// </summary>
        /// <remarks>
        /// Denormalized from the User entity for convenient display. 
        /// Empty string is used if the user relationship is null or email is unavailable.
        /// </remarks>
        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        /*************************************************************/
        /// <summary>
        /// Gets or sets the display name of the user who performed the activity.
        /// </summary>
        /// <remarks>
        /// Denormalized from the User entity for UI display purposes.
        /// Empty string is used if the user relationship is null or display name is unavailable.
        /// Typically formatted as "FirstName LastName".
        /// </remarks>
        /// <seealso cref="User.DisplayName"/>
        [Required]
        [MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;

        #endregion

        #region Activity Details

        /*************************************************************/
        /// <summary>
        /// Gets or sets the type of activity performed.
        /// </summary>
        /// <remarks>
        /// Common values include: Login, Logout, Registration, Create, Read, Update, Delete, Other.
        /// Used for categorizing and filtering activities in the user interface.
        /// </remarks>
        /// <example>
        /// "Login", "Create", "Update", "Delete"
        /// </example>
        /// <seealso cref="ActivityLog.ActivityType"/>
        [Required]
        [MaxLength(100)]
        public string ActivityType { get; set; } = string.Empty;

        /*************************************************************/
        /// <summary>
        /// Gets or sets the UTC timestamp when the activity occurred.
        /// </summary>
        /// <remarks>
        /// Stored in UTC format for consistency across time zones. Client applications
        /// should convert to local time for display purposes.
        /// </remarks>
        /// <seealso cref="ActivityLog.ActivityTimestamp"/>
        [Required]
        public DateTime ActivityTimestamp { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets a human-readable description of the activity.
        /// </summary>
        /// <remarks>
        /// Typically formatted as "{HttpMethod} {ControllerName}/{ActionName}".
        /// Provides context for the activity in audit trails and user activity feeds.
        /// </remarks>
        /// <example>
        /// "GET Labels/GetById", "POST Auth/Login"
        /// </example>
        /// <seealso cref="ActivityLog.Description"/>
        [MaxLength(500)]
        public string? Description { get; set; }

        #endregion

        #region Request Information

        /*************************************************************/
        /// <summary>
        /// Gets or sets the IP address of the client making the request.
        /// </summary>
        /// <remarks>
        /// Supports both IPv4 and IPv6 addresses. Useful for security auditing,
        /// detecting suspicious activity patterns, and geographic analysis.
        /// </remarks>
        /// <seealso cref="ActivityLog.IpAddress"/>
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the User-Agent string from the HTTP request header.
        /// </summary>
        /// <remarks>
        /// Contains browser/client information. Useful for analytics, compatibility
        /// testing, and identifying automated access patterns.
        /// </remarks>
        /// <seealso cref="ActivityLog.UserAgent"/>
        [MaxLength(500)]
        public string? UserAgent { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the request path (URL path component).
        /// </summary>
        /// <remarks>
        /// Does not include query string parameters. Example: "/api/Labels/123"
        /// Provides the endpoint that was accessed without exposing sensitive query data.
        /// </remarks>
        /// <seealso cref="ActivityLog.RequestPath"/>
        [MaxLength(500)]
        public string? RequestPath { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the HTTP method used for the request.
        /// </summary>
        /// <remarks>
        /// Standard HTTP methods: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS.
        /// Indicates the type of operation performed on the resource.
        /// </remarks>
        /// <example>
        /// "GET", "POST", "PUT", "DELETE"
        /// </example>
        /// <seealso cref="ActivityLog.HttpMethod"/>
        [MaxLength(10)]
        public string? HttpMethod { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the request parameters serialized as JSON.
        /// </summary>
        /// <remarks>
        /// Contains action method parameters, excluding sensitive data such as passwords,
        /// tokens, and secrets. Provides insight into what data was submitted with the request.
        /// </remarks>
        /// <seealso cref="ActivityLog.RequestParameters"/>
        public string? RequestParameters { get; set; }

        #endregion

        #region Controller and Endpoint Details

        /*************************************************************/
        /// <summary>
        /// Gets or sets the name of the controller that handled the request.
        /// </summary>
        /// <remarks>
        /// Example: "Labels", "Auth", "Users". Extracted from route data.
        /// Useful for grouping activities by functional area.
        /// </remarks>
        /// <seealso cref="ActivityLog.ControllerName"/>
        [MaxLength(100)]
        public string? ControllerName { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the name of the action method that was executed.
        /// </summary>
        /// <remarks>
        /// Example: "GetById", "Create", "Update". Extracted from route data.
        /// Combined with ControllerName provides the full endpoint identification.
        /// </remarks>
        /// <seealso cref="ActivityLog.ActionName"/>
        [MaxLength(100)]
        public string? ActionName { get; set; }

        #endregion

        #region Response and Performance

        /*************************************************************/
        /// <summary>
        /// Gets or sets the HTTP response status code.
        /// </summary>
        /// <remarks>
        /// Standard HTTP status codes: 200 (OK), 201 (Created), 400 (Bad Request),
        /// 401 (Unauthorized), 404 (Not Found), 500 (Internal Server Error), etc.
        /// Indicates the outcome of the request processing.
        /// </remarks>
        /// <example>
        /// 200, 201, 400, 404, 500
        /// </example>
        /// <seealso cref="ActivityLog.ResponseStatusCode"/>
        public int? ResponseStatusCode { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the execution time of the action in milliseconds.
        /// </summary>
        /// <remarks>
        /// Measured from action execution start to completion. Useful for performance
        /// monitoring, identifying slow operations, and optimizing user experience.
        /// </remarks>
        /// <seealso cref="ActivityLog.ExecutionTimeMs"/>
        public int? ExecutionTimeMs { get; set; }

        #endregion

        #region Result and Error Tracking

        /*************************************************************/
        /// <summary>
        /// Gets or sets the overall result status of the operation.
        /// </summary>
        /// <remarks>
        /// Typical values: "Success", "Error", "Warning". Used for high-level
        /// filtering in audit reports and dashboards.
        /// </remarks>
        /// <example>
        /// "Success", "Error"
        /// </example>
        /// <seealso cref="ActivityLog.Result"/>
        [MaxLength(50)]
        public string? Result { get; set; }

        /*************************************************************/
        /// <summary>
        /// Gets or sets the error message if an exception occurred.
        /// </summary>
        /// <remarks>
        /// Contains the Exception.Message property. Only populated when an error occurs.
        /// Sensitive information should be sanitized before including in the DTO.
        /// Note: Stack traces are excluded from this DTO for security reasons.
        /// </remarks>
        /// <seealso cref="ActivityLog.ErrorMessage"/>
        /// <seealso cref="ActivityLog.ExceptionType"/>
        public string? ErrorMessage { get; set; }

        #endregion
    }
}