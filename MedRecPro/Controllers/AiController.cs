using MedRecPro.Controllers;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MedRecPro.Api.Controllers
{
    #region ai controller

    /**************************************************************/
    /// <summary>
    /// API controller for AI-powered natural language interactions with the MedRecPro system.
    /// Provides endpoints for submitting queries, receiving interpreted API specifications,
    /// and synthesizing results into human-readable responses.
    /// </summary>
    /// <remarks>
    /// This controller implements an agentic AI pattern where:
    /// 
    /// <list type="number">
    /// <item>User submits a natural language query via <see cref="Interpret"/></item>
    /// <item>Claude AI interprets the query and returns API endpoint specifications</item>
    /// <item>Client executes the specified endpoints (preserving auth context)</item>
    /// <item>Results are sent back via <see cref="Synthesize"/> for human-readable response</item>
    /// </list>
    /// 
    /// This architecture maintains security by keeping API execution on the client side
    /// where authentication context is properly preserved, while leveraging AI for
    /// intelligent query interpretation and result synthesis.
    /// 
    /// The controller supports both authenticated and anonymous access, with certain
    /// operations (like data modification) requiring authentication. The AI agent
    /// is aware of authentication status and will suggest appropriate operations.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Step 1: User submits query
    /// POST /api/ai/interpret
    /// {
    ///   "userMessage": "Find all products containing aspirin",
    ///   "conversationId": "conv-123"
    /// }
    /// 
    /// // Response: API specifications to execute
    /// {
    ///   "success": true,
    ///   "endpoints": [{
    ///     "method": "GET",
    ///     "path": "/api/views/ingredient/search",
    ///     "queryParameters": { "substanceNameSearch": "aspirin" }
    ///   }],
    ///   "explanation": "Searching for products by ingredient name"
    /// }
    /// 
    /// // Step 2: Client executes endpoint, then synthesizes
    /// POST /api/ai/synthesize
    /// {
    ///   "originalQuery": "Find all products containing aspirin",
    ///   "executedEndpoints": [{
    ///     "specification": { ... },
    ///     "statusCode": 200,
    ///     "result": [...]
    ///   }]
    /// }
    /// 
    /// // Response: Human-readable synthesis
    /// {
    ///   "response": "I found 47 products containing aspirin...",
    ///   "suggestedFollowUps": ["Show details for Bayer Aspirin"]
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IClaudeApiService"/>
    /// <seealso cref="AiAgentRequest"/>
    /// <seealso cref="AiAgentInterpretation"/>
    /// <seealso cref="AiSynthesisRequest"/>
    /// <seealso cref="AiAgentSynthesis"/>
    [ApiController]
    public class AiController : ApiControllerBase
    {
        #region private properties

        /**************************************************************/
        /// <summary>
        /// Claude API service for AI interpretation and result synthesis.
        /// </summary>
        private readonly IClaudeApiService _claudeApiService;

        /**************************************************************/
        /// <summary>
        /// String cipher utility for encrypting user IDs.
        /// </summary>
        private readonly StringCipher _stringCipher;

        /**************************************************************/
        /// <summary>
        /// Configuration provider for accessing application settings.
        /// </summary>
        private readonly IConfiguration _configuration;

        /**************************************************************/
        /// <summary>
        /// Logger instance for this controller.
        /// </summary>
        private readonly ILogger<AiController> _logger;

        /**************************************************************/
        /// <summary>
        /// Secret key used for user ID encryption.
        /// </summary>
        private readonly string _pkEncryptionSecret;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the AiController with required dependencies.
        /// </summary>
        /// <param name="claudeApiService">Claude API service for interpretation and synthesis.</param>
        /// <param name="stringCipher">String cipher utility for encryption.</param>
        /// <param name="configuration">Configuration provider.</param>
        /// <param name="logger">Logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when encryption secret is not configured.</exception>
        public AiController(
            IClaudeApiService claudeApiService,
            StringCipher stringCipher,
            IConfiguration configuration,
            ILogger<AiController> logger)
        {
            #region implementation

            _claudeApiService = claudeApiService ?? throw new ArgumentNullException(nameof(claudeApiService));
            _stringCipher = stringCipher ?? throw new ArgumentNullException(nameof(stringCipher));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _pkEncryptionSecret = _configuration.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing or empty.");

            #endregion
        }

        #endregion

        #region public endpoints

        /**************************************************************/
        /// <summary>
        /// Gets the current system context including authentication status, demo mode state,
        /// and available capabilities. This context helps clients understand what operations
        /// are available and any limitations that apply to the current session.
        /// </summary>
        /// <returns>
        /// An <see cref="AiSystemContext"/> containing comprehensive system state information.
        /// </returns>
        /// <response code="200">Returns the system context.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/ai/context
        /// 
        /// This endpoint is useful for:
        /// - Determining if authentication is needed for certain operations
        /// - Checking if the database has data or needs SPL imports
        /// - Understanding demo mode limitations
        /// - Discovering available sections and views
        /// 
        /// Response (200):
        /// ```json
        /// {
        ///   "isAuthenticated": false,
        ///   "isDemoMode": true,
        ///   "demoModeMessage": "Database resets every 24 hours",
        ///   "isDatabaseEmpty": false,
        ///   "documentCount": 150,
        ///   "productCount": 450,
        ///   "availableSections": ["Document", "Organization", ...],
        ///   "availableViews": ["application-number", "ingredient", ...],
        ///   "importEnabled": false
        /// }
        /// ```
        /// </remarks>
        /// <seealso cref="AiSystemContext"/>
        [HttpGet("context")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AiSystemContext), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AiSystemContext>> GetContext()
        {
            #region implementation

            try
            {
                _logger.LogDebug("Retrieving system context for AI agent");

                var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
                var userId = isAuthenticated ? getEncryptedUserId() : null;

                var context = await _claudeApiService.GetSystemContextAsync(isAuthenticated, userId);

                // Add user name if authenticated
                if (isAuthenticated)
                {
                    context.UserName = User.FindFirst(ClaimTypes.Name)?.Value ??
                                       User.FindFirst(ClaimTypes.Email)?.Value;
                }

                return Ok(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system context");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving system context.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Interprets a natural language user request and returns structured API endpoint
        /// specifications that should be called to fulfill the request. This is the first
        /// step in the agentic workflow.
        /// </summary>
        /// <param name="request">
        /// The <see cref="AiAgentRequest"/> containing the user's natural language query,
        /// optional conversation history, and system context.
        /// </param>
        /// <returns>
        /// An <see cref="AiAgentInterpretation"/> containing API endpoint specifications,
        /// or clarifying questions if the request is ambiguous.
        /// </returns>
        /// <response code="200">Returns the interpretation with endpoint specifications.</response>
        /// <response code="400">If the request is invalid or missing required fields.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// POST /api/ai/interpret
        /// 
        /// Request Body:
        /// ```json
        /// {
        ///   "userMessage": "Find all drugs manufactured by Pfizer",
        ///   "conversationId": "conv-123",
        ///   "conversationHistory": [
        ///     { "role": "user", "content": "Previous message" },
        ///     { "role": "assistant", "content": "Previous response" }
        ///   ]
        /// }
        /// ```
        /// 
        /// Response (200):
        /// ```json
        /// {
        ///   "success": true,
        ///   "endpoints": [{
        ///     "method": "GET",
        ///     "path": "/api/views/labeler/search",
        ///     "queryParameters": { "labelerNameSearch": "Pfizer" },
        ///     "description": "Search products by labeler name",
        ///     "expectedResponseType": "array"
        ///   }],
        ///   "explanation": "I'll search for products manufactured by Pfizer.",
        ///   "requiresAuthentication": false
        /// }
        /// ```
        /// 
        /// If the request requires clarification:
        /// ```json
        /// {
        ///   "success": false,
        ///   "clarifyingQuestions": [
        ///     "Did you mean the ingredient aspirin, or a specific product brand?"
        ///   ]
        /// }
        /// ```
        /// 
        /// If the database is empty and user is asking data questions:
        /// ```json
        /// {
        ///   "success": true,
        ///   "isDirectResponse": true,
        ///   "directResponse": "The database is currently empty. To get started, import SPL ZIP files..."
        /// }
        /// ```
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example queries and expected interpretations:
        /// 
        /// "Show me all products containing aspirin"
        /// → GET /api/views/ingredient/search?substanceNameSearch=aspirin
        /// 
        /// "What application number is LIPITOR under?"
        /// → GET /api/views/document/search?productNameSearch=LIPITOR
        /// 
        /// "Import some SPL data"
        /// → Direct response with instructions for POST /api/labels/import
        /// 
        /// "What can you do?"
        /// → Direct response explaining capabilities
        /// </code>
        /// </example>
        /// <seealso cref="AiAgentRequest"/>
        /// <seealso cref="AiAgentInterpretation"/>
        [HttpPost("interpret")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AiAgentInterpretation), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AiAgentInterpretation>> Interpret([FromBody] AiAgentRequest request)
        {
            #region input validation

            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.UserMessage))
            {
                return BadRequest("User message is required.");
            }

            #endregion

            #region implementation

            try
            {
                _logger.LogInformation("Interpreting AI request: {MessagePreview}",
                    request.UserMessage.Length > 100
                        ? request.UserMessage[..100] + "..."
                        : request.UserMessage);

                // Build system context if not provided
                if (request.SystemContext == null)
                {
                    var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
                    var userId = isAuthenticated ? getEncryptedUserId() : null;
                    request.SystemContext = await _claudeApiService.GetSystemContextAsync(isAuthenticated, userId);
                }

                // Get interpretation from AI service
                var interpretation = await _claudeApiService.InterpretRequestAsync(request);

                return Ok(interpretation);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument in interpret request");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error interpreting AI request");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while processing your request.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Synthesizes API execution results into a coherent, human-readable response
        /// that addresses the user's original query. This is the final step in the
        /// agentic workflow after the client has executed the interpreted endpoints.
        /// </summary>
        /// <param name="request">
        /// The <see cref="AiSynthesisRequest"/> containing the original query,
        /// executed endpoint specifications, and their results.
        /// </param>
        /// <returns>
        /// An <see cref="AiAgentSynthesis"/> containing a natural language response,
        /// data highlights, and suggested follow-up queries.
        /// </returns>
        /// <response code="200">Returns the synthesized response.</response>
        /// <response code="400">If the request is invalid or missing required fields.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// POST /api/ai/synthesize
        /// 
        /// Request Body:
        /// ```json
        /// {
        ///   "originalQuery": "Find all drugs manufactured by Pfizer",
        ///   "conversationId": "conv-123",
        ///   "executedEndpoints": [{
        ///     "specification": {
        ///       "method": "GET",
        ///       "path": "/api/views/labeler/search",
        ///       "queryParameters": { "labelerNameSearch": "Pfizer" }
        ///     },
        ///     "statusCode": 200,
        ///     "result": [
        ///       { "ProductName": "LIPITOR", "ApplicationNumber": "NDA020702" },
        ///       { "ProductName": "VIAGRA", "ApplicationNumber": "NDA020895" }
        ///     ],
        ///     "executionTimeMs": 45
        ///   }]
        /// }
        /// ```
        /// 
        /// Response (200):
        /// ```json
        /// {
        ///   "response": "I found 47 products manufactured by Pfizer Inc. Some notable products include LIPITOR (atorvastatin calcium) under NDA020702 and VIAGRA (sildenafil citrate) under NDA020895.",
        ///   "dataHighlights": {
        ///     "totalProducts": 47,
        ///     "topProducts": ["LIPITOR", "VIAGRA", "ZOLOFT"]
        ///   },
        ///   "suggestedFollowUps": [
        ///     "Show me the full prescribing information for LIPITOR",
        ///     "Find generic alternatives to Pfizer products"
        ///   ],
        ///   "isComplete": true
        /// }
        /// ```
        /// </remarks>
        /// <seealso cref="AiSynthesisRequest"/>
        /// <seealso cref="AiAgentSynthesis"/>
        [HttpPost("synthesize")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AiAgentSynthesis), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AiAgentSynthesis>> Synthesize([FromBody] AiSynthesisRequest request)
        {
            #region input validation

            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.OriginalQuery))
            {
                return BadRequest("Original query is required.");
            }

            if (request.ExecutedEndpoints == null || !request.ExecutedEndpoints.Any())
            {
                return BadRequest("At least one executed endpoint result is required.");
            }

            #endregion

            #region implementation

            try
            {
                _logger.LogInformation("Synthesizing results for query: {QueryPreview}",
                    request.OriginalQuery.Length > 100
                        ? request.OriginalQuery[..100] + "..."
                        : request.OriginalQuery);

                // Build system context if not provided
                if (request.SystemContext == null)
                {
                    var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
                    var userId = isAuthenticated ? getEncryptedUserId() : null;
                    request.SystemContext = await _claudeApiService.GetSystemContextAsync(isAuthenticated, userId);
                }

                // Get synthesis from AI service
                var synthesis = await _claudeApiService.SynthesizeResultsAsync(request);

                return Ok(synthesis);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument in synthesize request");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error synthesizing AI results");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while synthesizing results.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the skills document describing all available API endpoints
        /// that the AI agent can interpret and suggest. This is useful for
        /// understanding the system's capabilities.
        /// </summary>
        /// <returns>
        /// The skills document as a formatted markdown string.
        /// </returns>
        /// <response code="200">Returns the skills document.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/ai/skills
        /// 
        /// Returns a comprehensive markdown document describing:
        /// - Navigation views (search by ingredient, NDC, labeler, etc.)
        /// - Label CRUD operations
        /// - Import/Export capabilities
        /// - Authentication endpoints
        /// - Available sections and parameters
        /// 
        /// This document is also used internally by Claude when interpreting requests.
        /// </remarks>   
        [HttpGet("skills")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<string>> GetSkills()
        {
            #region implementation

            try
            {
                _logger.LogDebug("Retrieving AI skills document");

                var skills = await _claudeApiService.GetSkillsDocumentAsync();

                return Ok(skills);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving skills document");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the skills document.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Convenience endpoint that combines interpretation and immediate execution
        /// for simple queries that don't require client-side API calls. This is useful
        /// for informational queries like "what can you do?" or "how do I import data?".
        /// </summary>
        /// <param name="message">The natural language query as a simple string.</param>
        /// <returns>
        /// Either a direct response or interpretation with endpoint specifications.
        /// </returns>
        /// <response code="200">Returns interpretation or direct response.</response>
        /// <response code="400">If the message is empty.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/ai/chat?message=What can you do?
        /// 
        /// This is a simplified endpoint for quick queries. For full conversation
        /// support with history, use the POST /api/ai/interpret endpoint.
        /// </remarks>
        [HttpGet("chat")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AiAgentInterpretation), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AiAgentInterpretation>> Chat([FromQuery] string message)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(message))
            {
                return BadRequest("Message is required.");
            }

            var request = new AiAgentRequest
            {
                UserMessage = message
            };

            return await Interpret(request);

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Gets the encrypted user ID for the current authenticated user.
        /// </summary>
        /// <returns>Encrypted user ID string, or null if not authenticated.</returns>
        private string? getEncryptedUserId()
        {
            #region implementation

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return null;
            }

            // Encrypt the user ID for external use
            return StringCipher.Encrypt(userIdClaim, _pkEncryptionSecret, StringCipher.EncryptionStrength.Fast);

            #endregion
        }

        #endregion

        #region conversation management endpoints

        /**************************************************************/
        /// <summary>
        /// Creates a new conversation session and returns the server-generated conversation ID.
        /// Use this endpoint to explicitly start a new conversation before sending messages.
        /// </summary>
        /// <returns>
        /// A new <see cref="Conversation"/> with the server-generated ID and metadata.
        /// </returns>
        /// <response code="200">Returns the new conversation.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// POST /api/ai/conversations
        /// 
        /// Creates a new conversation session. The returned conversation ID should be
        /// included in subsequent interpret requests to maintain context.
        /// 
        /// Conversations expire after 1 hour of inactivity. Each message or interaction
        /// resets the expiration timer.
        /// 
        /// <b>Note:</b> You don't need to call this endpoint explicitly. Calling
        /// POST /api/ai/interpret without a conversationId will automatically create
        /// a new conversation and return the ID in the response.
        /// </remarks>
        /// <example>
        /// <code>
        /// POST /api/ai/conversations
        /// 
        /// Response:
        /// {
        ///   "conversationId": "conv-abc123...",
        ///   "createdAt": "2024-01-15T10:30:00Z",
        ///   "expiresAt": "2024-01-15T11:30:00Z",
        ///   "messages": []
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="GetConversation"/>
        /// <seealso cref="Interpret"/>
        [HttpPost("conversations")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(Conversation), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Conversation>> CreateConversation()
        {
            #region implementation

            try
            {
                var userId = User.Identity?.IsAuthenticated == true ? getEncryptedUserId() : null;

                _logger.LogInformation("Creating new conversation for user {UserId}", userId ?? "anonymous");

                var conversation = await _claudeApiService.CreateConversationAsync(userId);

                return Ok(conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating conversation");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while creating the conversation.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves an existing conversation by ID, including its full message history.
        /// </summary>
        /// <param name="conversationId">The conversation ID to retrieve.</param>
        /// <returns>
        /// The <see cref="Conversation"/> if found, including all messages.
        /// </returns>
        /// <response code="200">Returns the conversation with message history.</response>
        /// <response code="404">If the conversation is not found or has expired.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/ai/conversations/{conversationId}
        /// 
        /// Retrieves a conversation and its complete message history. This endpoint
        /// does not reset the expiration timer - use it for read-only operations
        /// like displaying conversation history.
        /// 
        /// Conversations expire after 1 hour of inactivity. If the conversation
        /// has expired, a 404 response is returned.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/ai/conversations/conv-abc123
        /// 
        /// Response:
        /// {
        ///   "conversationId": "conv-abc123...",
        ///   "createdAt": "2024-01-15T10:30:00Z",
        ///   "lastActivityAt": "2024-01-15T10:45:00Z",
        ///   "expiresAt": "2024-01-15T11:45:00Z",
        ///   "messages": [
        ///     { "role": "user", "content": "Find products by Pfizer", "timestamp": "..." },
        ///     { "role": "assistant", "content": "I'll search for Pfizer products", "timestamp": "..." }
        ///   ],
        ///   "messageCount": 2
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="GetConversationHistory"/>
        /// <seealso cref="CreateConversation"/>
        [HttpGet("conversations/{conversationId}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(Conversation), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Conversation>> GetConversation(string conversationId)
        {
            #region implementation

            try
            {
                if (string.IsNullOrWhiteSpace(conversationId))
                {
                    return BadRequest("Conversation ID is required.");
                }

                _logger.LogDebug("Retrieving conversation {ConversationId}", conversationId);

                var conversation = await _claudeApiService.GetConversationAsync(conversationId);

                if (conversation == null)
                {
                    return NotFound($"Conversation '{conversationId}' not found or has expired.");
                }

                return Ok(conversation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving conversation {ConversationId}", conversationId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving the conversation.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the message history for a conversation with optional pagination.
        /// </summary>
        /// <param name="conversationId">The conversation ID.</param>
        /// <param name="maxMessages">Optional maximum number of recent messages to return.</param>
        /// <returns>
        /// List of messages in chronological order.
        /// </returns>
        /// <response code="200">Returns the message history.</response>
        /// <response code="404">If the conversation is not found or has expired.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/ai/conversations/{conversationId}/history?maxMessages=10
        /// 
        /// Retrieves message history for a conversation. When maxMessages is specified,
        /// returns only the most recent messages (useful for limiting context size).
        /// 
        /// Messages are returned in chronological order (oldest first).
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/ai/conversations/conv-abc123/history?maxMessages=5
        /// 
        /// Response:
        /// [
        ///   { "role": "user", "content": "Find products by Pfizer", "timestamp": "..." },
        ///   { "role": "assistant", "content": "I found 47 products...", "timestamp": "..." },
        ///   { "role": "user", "content": "Show me the beta blockers", "timestamp": "..." },
        ///   { "role": "assistant", "content": "Here are the beta blockers...", "timestamp": "..." }
        /// ]
        /// </code>
        /// </example>
        /// <seealso cref="GetConversation"/>
        [HttpGet("conversations/{conversationId}/history")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<AiConversationMessage>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<AiConversationMessage>>> GetConversationHistory(
            string conversationId,
            [FromQuery] int? maxMessages = null)
        {
            #region implementation

            try
            {
                if (string.IsNullOrWhiteSpace(conversationId))
                {
                    return BadRequest("Conversation ID is required.");
                }

                _logger.LogDebug("Retrieving history for conversation {ConversationId}, maxMessages: {MaxMessages}",
                    conversationId, maxMessages?.ToString() ?? "all");

                // First check if conversation exists
                var conversation = await _claudeApiService.GetConversationAsync(conversationId);

                if (conversation == null)
                {
                    return NotFound($"Conversation '{conversationId}' not found or has expired.");
                }

                var messages = await _claudeApiService.GetConversationHistoryAsync(conversationId, maxMessages);

                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving conversation history for {ConversationId}", conversationId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving conversation history.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Deletes a conversation and its message history.
        /// </summary>
        /// <param name="conversationId">The conversation ID to delete.</param>
        /// <returns>
        /// Success status indicating whether the conversation was deleted.
        /// </returns>
        /// <response code="200">Conversation was successfully deleted.</response>
        /// <response code="404">If the conversation is not found.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// DELETE /api/ai/conversations/{conversationId}
        /// 
        /// Permanently deletes a conversation and all its messages. This action
        /// cannot be undone.
        /// </remarks>
        /// <seealso cref="CreateConversation"/>
        [HttpDelete("conversations/{conversationId}")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteConversation(string conversationId)
        {
            #region implementation

            try
            {
                if (string.IsNullOrWhiteSpace(conversationId))
                {
                    return BadRequest("Conversation ID is required.");
                }

                _logger.LogInformation("Deleting conversation {ConversationId}", conversationId);

                var deleted = await _claudeApiService.DeleteConversationAsync(conversationId);

                if (!deleted)
                {
                    return NotFound($"Conversation '{conversationId}' not found.");
                }

                return Ok(new { message = "Conversation deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting conversation {ConversationId}", conversationId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while deleting the conversation.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets statistics about the conversation store, including active conversation
        /// counts and total message counts.
        /// </summary>
        /// <returns>
        /// <see cref="ConversationStoreStats"/> with current metrics.
        /// </returns>
        /// <response code="200">Returns conversation store statistics.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/ai/conversations/stats
        /// 
        /// Returns metrics about the conversation store for monitoring and debugging.
        /// Useful for understanding system load and conversation patterns.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/ai/conversations/stats
        /// 
        /// Response:
        /// {
        ///   "totalConversations": 150,
        ///   "activeConversations": 142,
        ///   "expiredConversations": 8,
        ///   "totalMessages": 1250,
        ///   "oldestConversation": "2024-01-15T08:00:00Z",
        ///   "newestConversation": "2024-01-15T10:45:00Z"
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="ConversationStoreStats"/>
        [HttpGet("conversations/stats")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ConversationStoreStats), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ConversationStoreStats>> GetConversationStats()
        {
            #region implementation

            try
            {
                _logger.LogDebug("Retrieving conversation store statistics");

                var stats = await _claudeApiService.GetConversationStatsAsync();

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving conversation statistics");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving conversation statistics.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retries interpretation when initial API endpoints fail.
        /// Uses Claude to suggest alternative endpoints based on the failure reasons.
        /// </summary>
        /// <param name="request">
        /// The <see cref="AiRetryRequest"/> containing the original request,
        /// failed endpoint results, and current attempt number.
        /// </param>
        /// <returns>
        /// An <see cref="AiAgentInterpretation"/> containing alternative endpoint specifications
        /// to try, or a direct response if no alternatives are available.
        /// </returns>
        /// <response code="200">Returns the retry interpretation with new endpoints.</response>
        /// <response code="400">If the request is invalid or missing required fields.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// POST /api/ai/retry
        /// 
        /// This endpoint implements recursive retry logic:
        /// 1. Analyzes why the original endpoints failed (404, 500, etc.)
        /// 2. Consults the skills document for alternative endpoints
        /// 3. Suggests fallback paths (e.g., views → label/section)
        /// 4. After 3 attempts, returns a direct response explaining the failure
        /// 
        /// Request Body:
        /// ```json
        /// {
        ///   "originalRequest": {
        ///     "userMessage": "What ingredients are available?",
        ///     "conversationId": "conv-123"
        ///   },
        ///   "failedResults": [{
        ///     "specification": { "method": "GET", "path": "/api/views/ingredient/summaries" },
        ///     "statusCode": 404,
        ///     "error": "Not Found"
        ///   }],
        ///   "attemptNumber": 1
        /// }
        /// ```
        /// 
        /// Response (200):
        /// ```json
        /// {
        ///   "success": true,
        ///   "endpoints": [{
        ///     "method": "GET",
        ///     "path": "/api/label/section/ActiveIngredient",
        ///     "queryParameters": { "pageNumber": "1", "pageSize": "50" },
        ///     "description": "Alternative: Get ingredients from direct table access"
        ///   }],
        ///   "explanation": "The view endpoint was not available, trying direct table access instead.",
        ///   "retryAttempt": 1
        /// }
        /// ```
        /// </remarks>
        /// <seealso cref="Interpret"/>
        /// <seealso cref="Synthesize"/>
        [HttpPost("retry")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(AiAgentInterpretation), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AiAgentInterpretation>> RetryInterpretation([FromBody] AiRetryRequest request)
        {
            #region input validation

            if (request == null)
            {
                return BadRequest("Request body is required.");
            }

            if (request.OriginalRequest == null)
            {
                return BadRequest("Original request is required.");
            }

            if (request.FailedResults == null || !request.FailedResults.Any())
            {
                return BadRequest("Failed results are required for retry.");
            }

            if (request.AttemptNumber <= 0)
            {
                request.AttemptNumber = 1;
            }

            #endregion

            #region implementation

            try
            {
                _logger.LogInformation("Retry interpretation attempt {Attempt} for query: {QueryPreview}",
                    request.AttemptNumber,
                    request.OriginalRequest.UserMessage?.Length > 100
                        ? request.OriginalRequest.UserMessage[..100] + "..."
                        : request.OriginalRequest.UserMessage);

                // Build system context if not provided
                if (request.OriginalRequest.SystemContext == null)
                {
                    var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
                    var userId = isAuthenticated ? getEncryptedUserId() : null;
                    request.OriginalRequest.SystemContext = await _claudeApiService.GetSystemContextAsync(isAuthenticated, userId);
                }

                // Call retry interpretation
                var interpretation = await _claudeApiService.RetryInterpretationAsync(
                    request.OriginalRequest,
                    request.FailedResults,
                    request.AttemptNumber);

                return Ok(interpretation);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument in retry request");
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in retry interpretation");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while processing your retry request.");
            }

            #endregion
        }

        #endregion
    }

    #region retry request model

    /**************************************************************/
    /// <summary>
    /// Request model for retry interpretation when initial endpoints fail.
    /// </summary>
    public class AiRetryRequest
    {
        /**************************************************************/
        /// <summary>
        /// The original user request that was interpreted.
        /// </summary>
        public AiAgentRequest OriginalRequest { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// The endpoint execution results that failed.
        /// </summary>
        public List<AiEndpointResult> FailedResults { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Current retry attempt number (1-based, max 3).
        /// </summary>
        public int AttemptNumber { get; set; } = 1;
    }

    #endregion

    #endregion
}