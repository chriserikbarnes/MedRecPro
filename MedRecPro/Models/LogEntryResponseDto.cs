namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents one safe, administrative in-memory log entry returned by the Settings API.
    /// </summary>
    /// <remarks>
    /// The controller encrypts <see cref="UserId"/> before assigning it. Exception fields are safe summaries from
    /// <see cref="Helpers.LogEntry"/> and never expose the retained runtime exception graph.
    /// </remarks>
    /// <seealso cref="Helpers.LogEntry"/>
    public sealed class LogEntryResponseDto
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets the sanitized rendered log message.
        /// </summary>
        public string? Message { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the serialized log severity name.
        /// </summary>
        public string Level { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Gets or sets the UTC timestamp captured by the provider clock.
        /// </summary>
        public DateTime Timestamp { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the logger category.
        /// </summary>
        public string? Category { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the request correlation identifier captured with the log event.
        /// </summary>
        public string? TraceId { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the encrypted user identifier when user context exists.
        /// </summary>
        public string? UserId { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the captured user display name when available.
        /// </summary>
        public string? UserName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the safe exception summary for administrative diagnosis.
        /// </summary>
        public string? ExceptionMessage { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the exception runtime type name.
        /// </summary>
        public string? ExceptionType { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents a typed page of safe administrative log entries.
    /// </summary>
    /// <remarks>
    /// This response shape is shared by the unfiltered and filtered log endpoints so Swagger describes the retained
    /// entry contract without exposing the internal <see cref="Helpers.LogEntry"/> type.
    /// </remarks>
    /// <seealso cref="LogEntryResponseDto"/>
    public sealed class LogPageResponseDto
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets the safe entries in the requested page.
        /// </summary>
        public List<LogEntryResponseDto> Entries { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of entries matching the current filter.
        /// </summary>
        public int TotalCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the one-based page number.
        /// </summary>
        public int PageNumber { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of entries requested per page.
        /// </summary>
        public int PageSize { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the number of pages matching the current filter.
        /// </summary>
        public int TotalPages { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the typed filter values applied to the page, when applicable.
        /// </summary>
        public LogPageFilterResponseDto? Filter { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents the optional filter details applied to an administrative log page.
    /// </summary>
    /// <seealso cref="LogPageResponseDto"/>
    public sealed class LogPageFilterResponseDto
    {
        /**************************************************************/
        /// <summary>
        /// Gets or sets the UTC range start used by a date-filtered query.
        /// </summary>
        public DateTime? StartDate { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the UTC range end used by a date-filtered query.
        /// </summary>
        public DateTime? EndDate { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the category filter used by a category-filtered query.
        /// </summary>
        public string? Category { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the encrypted user identifier used by a user-filtered query.
        /// </summary>
        public string? UserId { get; set; }
    }
}
