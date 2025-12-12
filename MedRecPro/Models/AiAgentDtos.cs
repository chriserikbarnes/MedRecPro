using MedRecPro.Service;
using Newtonsoft.Json;

namespace MedRecPro.Models
{
    #region ai agent dtos

    /**************************************************************/
    /// <summary>
    /// Represents the system context information provided to the AI agent for context-aware
    /// interpretation of user requests. Contains authentication state, demo mode status,
    /// and available system capabilities.
    /// </summary>
    /// <remarks>
    /// This context enables Claude to provide appropriate responses based on:
    /// - User authentication status (determines available operations)
    /// - Demo mode state (indicates data persistence limitations)
    /// - Available data (helps Claude suggest relevant queries)
    /// - System capabilities (informs what operations are possible)
    /// </remarks>
    /// <seealso cref="IClaudeApiService"/>
    /// <seealso cref="AiAgentRequest"/>
    public class AiSystemContext
    {
        #region authentication properties

        /**************************************************************/
        /// <summary>
        /// Indicates whether the current user session is authenticated.
        /// Unauthenticated users have read-only access to public data.
        /// </summary>
        [JsonProperty("isAuthenticated")]
        public bool IsAuthenticated { get; set; }

        /**************************************************************/
        /// <summary>
        /// The encrypted user identifier if authenticated, null otherwise.
        /// Used for user-specific operations and activity tracking.
        /// </summary>
        [JsonProperty("userId")]
        public string? UserId { get; set; }

        /**************************************************************/
        /// <summary>
        /// The display name of the authenticated user, null if not authenticated.
        /// </summary>
        [JsonProperty("userName")]
        public string? UserName { get; set; }

        #endregion

        #region demo mode properties

        /**************************************************************/
        /// <summary>
        /// Indicates whether the system is operating in demo mode.
        /// Demo mode has periodic data resets and limited persistence.
        /// </summary>
        [JsonProperty("isDemoMode")]
        public bool IsDemoMode { get; set; }

        /**************************************************************/
        /// <summary>
        /// Human-readable message describing demo mode limitations,
        /// such as reset frequency and data persistence constraints.
        /// </summary>
        [JsonProperty("demoModeMessage")]
        public string? DemoModeMessage { get; set; }

        /**************************************************************/
        /// <summary>
        /// Indicates whether the database is currently empty or has minimal data.
        /// When true, Claude should suggest importing SPL data to get started.
        /// </summary>
        [JsonProperty("isDatabaseEmpty")]
        public bool IsDatabaseEmpty { get; set; }

        #endregion

        #region capability properties

        /**************************************************************/
        /// <summary>
        /// Total count of documents available in the database.
        /// Helps Claude understand the scope of available data.
        /// </summary>
        [JsonProperty("documentCount")]
        public int DocumentCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Total count of products available in the database.
        /// </summary>
        [JsonProperty("productCount")]
        public int ProductCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// List of available label sections (entity types) that can be queried.
        /// Maps to the menuSelection parameter in Label API endpoints.
        /// </summary>
        [JsonProperty("availableSections")]
        public List<string> AvailableSections { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// List of available navigation views (e.g., application-number, ingredient, ndc).
        /// These provide aggregate and search capabilities across the data.
        /// </summary>
        [JsonProperty("availableViews")]
        public List<string> AvailableViews { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Indicates whether SPL import functionality is enabled.
        /// When disabled, Claude should not suggest import operations.
        /// </summary>
        [JsonProperty("importEnabled")]
        public bool ImportEnabled { get; set; }

        /**************************************************************/
        /// <summary>
        /// Indicates whether AI comparison analysis is available.
        /// </summary>
        [JsonProperty("comparisonAnalysisEnabled")]
        public bool ComparisonAnalysisEnabled { get; set; }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Represents a user request to the AI agent containing the natural language query,
    /// conversation context, and system state information.
    /// </summary>
    /// <remarks>
    /// The request includes all information Claude needs to interpret the user's intent:
    /// - The actual user message/query
    /// - Conversation history for context
    /// - System state for capability awareness
    /// - Any previous interpretations for refinement
    /// </remarks>
    /// <seealso cref="IClaudeApiService.InterpretRequestAsync"/>
    /// <seealso cref="AiAgentInterpretation"/>
    public class AiAgentRequest
    {
        #region request properties

        /**************************************************************/
        /// <summary>
        /// The natural language message from the user to be interpreted.
        /// This is the primary input for Claude's interpretation.
        /// </summary>
        /// <example>"Show me all products containing aspirin"</example>
        [JsonProperty("userMessage")]
        public string UserMessage { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Unique identifier for the conversation session.
        /// Used to maintain context across multiple exchanges.
        /// </summary>
        [JsonProperty("conversationId")]
        public string? ConversationId { get; set; }

        /**************************************************************/
        /// <summary>
        /// Previous messages in the conversation for context.
        /// Enables Claude to understand follow-up queries and references.
        /// </summary>
        [JsonProperty("conversationHistory")]
        public List<AiConversationMessage>? ConversationHistory { get; set; }

        /**************************************************************/
        /// <summary>
        /// Current system context including authentication and capabilities.
        /// </summary>
        [JsonProperty("systemContext")]
        public AiSystemContext? SystemContext { get; set; }

        /**************************************************************/
        /// <summary>
        /// Contains the results of a file import operation, if applicable.
        /// </summary>
        /// <remarks>
        /// When files are uploaded through the chat interface, this property
        /// contains information about the import operation including success
        /// status, imported document IDs, and any error messages.
        /// </remarks>
        [JsonProperty("importResult")]
        public ImportResultContext? ImportResult { get; set; }


        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Represents a single message in the conversation history between user and AI agent.
    /// </summary>
    /// <seealso cref="AiAgentRequest"/>
    public class AiConversationMessage
    {
        /**************************************************************/
        /// <summary>
        /// The role of the message sender: "user" or "assistant".
        /// </summary>
        [JsonProperty("role")]
        public string Role { get; set; } = "user";

        /**************************************************************/
        /// <summary>
        /// The content of the message.
        /// </summary>
        [JsonProperty("content")]
        public string Content { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Timestamp when the message was sent.
        /// </summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /**************************************************************/
    /// <summary>
    /// Represents Claude's interpretation of a user request, containing the API endpoints
    /// that should be called to fulfill the request.
    /// </summary>
    /// <remarks>
    /// The interpretation provides:
    /// - Structured endpoint specifications for the client to execute
    /// - Human-readable explanation of the interpretation
    /// - Any clarifying questions if the request is ambiguous
    /// - Authentication requirements for the suggested operations
    /// - Server-generated conversation ID for session tracking
    /// </remarks>
    /// <seealso cref="IClaudeApiService.InterpretRequestAsync"/>
    /// <seealso cref="AiEndpointSpecification"/>
    public class AiAgentInterpretation
    {
        #region interpretation properties

        /**************************************************************/
        /// <summary>
        /// Server-generated conversation ID for tracking this conversation session.
        /// Include this ID in subsequent requests to maintain conversation context.
        /// </summary>
        [JsonProperty("conversationId")]
        public string? ConversationId { get; set; }

        /**************************************************************/
        /// <summary>
        /// Indicates whether the interpretation was successful.
        /// False indicates the request could not be mapped to any API operation.
        /// </summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /**************************************************************/
        /// <summary>
        /// List of API endpoints to be called to fulfill the user's request.
        /// May contain multiple endpoints for complex queries requiring aggregation.
        /// </summary>
        [JsonProperty("endpoints")]
        public List<AiEndpointSpecification> Endpoints { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Human-readable explanation of how Claude interpreted the request.
        /// Helps users understand what operations will be performed.
        /// </summary>
        [JsonProperty("explanation")]
        public string? Explanation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Clarifying questions if the user's request is ambiguous.
        /// When present, the client should prompt the user for more information.
        /// </summary>
        [JsonProperty("clarifyingQuestions")]
        public List<string>? ClarifyingQuestions { get; set; }

        /**************************************************************/
        /// <summary>
        /// Indicates whether any of the suggested endpoints require authentication.
        /// If true and user is not authenticated, client should prompt for login.
        /// </summary>
        [JsonProperty("requiresAuthentication")]
        public bool RequiresAuthentication { get; set; }

        /**************************************************************/
        /// <summary>
        /// Error message if the interpretation failed.
        /// </summary>
        [JsonProperty("error")]
        public string? Error { get; set; }

        /**************************************************************/
        /// <summary>
        /// Suggested alternative queries if the original request cannot be fulfilled.
        /// </summary>
        [JsonProperty("suggestions")]
        public List<string>? Suggestions { get; set; }

        /**************************************************************/
        /// <summary>
        /// Indicates this is a direct response that doesn't require API calls.
        /// Used for informational queries like "what can you do?" or demo mode guidance.
        /// </summary>
        [JsonProperty("isDirectResponse")]
        public bool IsDirectResponse { get; set; }

        /**************************************************************/
        /// <summary>
        /// Direct response content when no API calls are needed.
        /// </summary>
        [JsonProperty("directResponse")]
        public string? DirectResponse { get; set; }

        /**************************************************************/
        /// <summary>
        /// The retry attempt number if this interpretation was from a retry (1-3), or 0 if original.
        /// </summary>
        public int RetryAttempt { get; set; } = 0;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Specifies a single API endpoint to be called, including HTTP method, path,
    /// and all required parameters.
    /// </summary>
    /// <remarks>
    /// This specification provides everything the client needs to construct and
    /// execute an HTTP request to the MedRecPro API. The client is responsible
    /// for actual execution to maintain authentication context.
    /// </remarks>
    /// <seealso cref="AiAgentInterpretation"/>
    public class AiEndpointSpecification
    {
        #region endpoint properties

        /**************************************************************/
        /// <summary>
        /// HTTP method for the request (GET, POST, PUT, DELETE).
        /// </summary>
        [JsonProperty("method")]
        public string Method { get; set; } = "GET";

        /**************************************************************/
        /// <summary>
        /// API endpoint path including route parameters.
        /// Example: "/api/views/ingredient/search" or "/api/labels/Document/{encryptedId}"
        /// </summary>
        [JsonProperty("path")]
        public string Path { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Query string parameters as key-value pairs.
        /// Example: { "substanceNameSearch": "aspirin", "pageSize": "25" }
        /// </summary>
        [JsonProperty("queryParameters")]
        public Dictionary<string, string>? QueryParameters { get; set; }

        /**************************************************************/
        /// <summary>
        /// Request body for POST/PUT operations, serialized as JSON object.
        /// </summary>
        [JsonProperty("body")]
        public object? Body { get; set; }

        /**************************************************************/
        /// <summary>
        /// Human-readable description of what this endpoint call will accomplish.
        /// </summary>
        [JsonProperty("description")]
        public string? Description { get; set; }

        /**************************************************************/
        /// <summary>
        /// Expected response type hint for client-side handling.
        /// Examples: "array", "object", "file", "stream"
        /// </summary>
        [JsonProperty("expectedResponseType")]
        public string? ExpectedResponseType { get; set; }

        /**************************************************************/
        /// <summary>
        /// Order of execution if multiple endpoints are specified.
        /// Lower numbers execute first. Used for dependent queries.
        /// </summary>
        [JsonProperty("executionOrder")]
        public int ExecutionOrder { get; set; } = 0;

        /**************************************************************/
        /// <summary>
        /// Identifier for this endpoint specification, used to reference
        /// results in dependent queries.
        /// </summary>
        [JsonProperty("id")]
        public string? Id { get; set; }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Request to synthesize API execution results into a coherent response
    /// that addresses the user's original query.
    /// </summary>
    /// <seealso cref="IClaudeApiService.SynthesizeResultsAsync"/>
    /// <seealso cref="AiAgentSynthesis"/>
    public class AiSynthesisRequest
    {
        #region synthesis request properties

        /**************************************************************/
        /// <summary>
        /// The original user query that initiated the operation.
        /// Provides context for how to frame the synthesized response.
        /// </summary>
        [JsonProperty("originalQuery")]
        public string OriginalQuery { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Unique identifier for the conversation session.
        /// </summary>
        [JsonProperty("conversationId")]
        public string? ConversationId { get; set; }

        /**************************************************************/
        /// <summary>
        /// List of executed endpoints and their results.
        /// </summary>
        [JsonProperty("executedEndpoints")]
        public List<AiEndpointResult> ExecutedEndpoints { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Current system context for response formatting decisions.
        /// </summary>
        [JsonProperty("systemContext")]
        public AiSystemContext? SystemContext { get; set; }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Contains an executed endpoint specification along with its result data.
    /// </summary>
    /// <seealso cref="AiSynthesisRequest"/>
    public class AiEndpointResult
    {
        /**************************************************************/
        /// <summary>
        /// The endpoint specification that was executed.
        /// </summary>
        [JsonProperty("specification")]
        public AiEndpointSpecification Specification { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// The HTTP status code returned by the API.
        /// </summary>
        [JsonProperty("statusCode")]
        public int StatusCode { get; set; }

        /**************************************************************/
        /// <summary>
        /// The response body from the API call, typically deserialized JSON.
        /// </summary>
        [JsonProperty("result")]
        public object? Result { get; set; }

        /**************************************************************/
        /// <summary>
        /// Error message if the API call failed.
        /// </summary>
        [JsonProperty("error")]
        public string? Error { get; set; }

        /**************************************************************/
        /// <summary>
        /// Execution time in milliseconds for performance tracking.
        /// </summary>
        [JsonProperty("executionTimeMs")]
        public long ExecutionTimeMs { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents Claude's synthesized response to the user's query based on
    /// executed API results.
    /// </summary>
    /// <seealso cref="IClaudeApiService.SynthesizeResultsAsync"/>
    public class AiAgentSynthesis
    {
        #region synthesis response properties

        /**************************************************************/
        /// <summary>
        /// The conversation ID this synthesis belongs to.
        /// </summary>
        [JsonProperty("conversationId")]
        public string? ConversationId { get; set; }

        /**************************************************************/
        /// <summary>
        /// The natural language response summarizing the results.
        /// Directly addresses the user's original query.
        /// </summary>
        [JsonProperty("response")]
        public string Response { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Structured highlights extracted from the results for display.
        /// May include counts, key items, or summary statistics.
        /// </summary>
        [JsonProperty("dataHighlights")]
        public Dictionary<string, object>? DataHighlights { get; set; }

        /**************************************************************/
        /// <summary>
        /// Suggested follow-up queries the user might find helpful.
        /// </summary>
        [JsonProperty("suggestedFollowUps")]
        public List<string>? SuggestedFollowUps { get; set; }

        /**************************************************************/
        /// <summary>
        /// Any warnings or limitations encountered during synthesis.
        /// </summary>
        [JsonProperty("warnings")]
        public List<string>? Warnings { get; set; }

        /**************************************************************/
        /// <summary>
        /// Indicates whether the response fully addressed the user's query.
        /// False may indicate partial results or limitations.
        /// </summary>
        [JsonProperty("isComplete")]
        public bool IsComplete { get; set; } = true;

        #endregion
    }

    #endregion

}