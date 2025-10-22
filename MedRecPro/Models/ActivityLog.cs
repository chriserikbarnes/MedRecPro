using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [ForeignKey(nameof(UserId))]
        public virtual User? User { get; set; }

        #endregion
    }
}