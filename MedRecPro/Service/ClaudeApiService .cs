using MedRecPro.Data;
using MedRecPro.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using MedRecPro.Helpers;
using Microsoft.Extensions.Logging;

namespace MedRecPro.Service
{
    #region Claude API service interface

    /**************************************************************/
    /// <summary>
    /// Defines the contract for Claude AI API integration services that provide artificial intelligence
    /// capabilities for analyzing and comparing SPL (Structured Product Labeling) medical documents.
    /// This interface enables AI-powered text analysis, data completeness validation, and intelligent
    /// comparison operations critical for medical record processing and pharmaceutical documentation systems.
    /// </summary>
    /// <remarks>
    /// The Claude API service serves as the core AI intelligence layer for the MedRecPro comparison system,
    /// leveraging Anthropic's Claude language model to perform sophisticated analysis of medical documents.
    /// This service provides natural language processing capabilities specifically tuned for healthcare
    /// and pharmaceutical documentation requirements.
    /// 
    /// Key capabilities provided through this interface include:
    /// - Intelligent comparison analysis between XML and JSON medical document formats
    /// - Natural language processing for medical terminology and pharmaceutical data
    /// - Structured data extraction from unstructured medical text
    /// - Completeness assessment for regulatory compliance documentation
    /// - Quality assurance validation for medical data transformations
    /// - Contextual understanding of medical document structures and relationships
    /// 
    /// The service integrates with Anthropic's Claude API to provide enterprise-grade AI analysis
    /// while maintaining data security and privacy standards required for medical information processing.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Dependency injection registration
    /// services.AddHttpClient&lt;IClaudeApiService, ClaudeApiService&gt;();
    /// services.Configure&lt;ClaudeApiSettings&gt;(configuration.GetSection("ClaudeApi"));
    /// 
    /// // Usage in comparison service
    /// public class ComparisonService
    /// {
    ///     private readonly IClaudeApiService _claudeApi;
    ///     
    ///     public async Task&lt;ComparisonResult&gt; AnalyzeDocuments(string xmlContent, string jsonContent)
    ///     {
    ///         var prompt = BuildMedicalComparisonPrompt(xmlContent, jsonContent);
    ///         var aiAnalysis = await _claudeApi.GenerateCompletionAsync(prompt);
    ///         return ParseMedicalAnalysis(aiAnalysis);
    ///     }
    /// }
    /// 
    /// // Direct usage for medical document analysis
    /// string medicalPrompt = @"
    ///     Analyze the following SPL document for completeness:
    ///     - Verify all required FDA sections are present
    ///     - Check drug interaction data completeness
    ///     - Validate clinical trial information accuracy
    ///     [Document content...]";
    /// 
    /// var analysis = await claudeService.GenerateCompletionAsync(medicalPrompt);
    /// </code>
    /// </example>
    /// <seealso cref="IComparisonService"/>
    /// <seealso cref="Models.ComparisonRequest"/>
    /// <seealso cref="Models.ComparisonResponse"/>
    /// <seealso cref="Models.ComparisonResult"/>
    public interface IClaudeApiService
    {
        #region ai completion methods

        /**************************************************************/
        /// <summary>
        /// Asynchronously generates an AI-powered text completion using Claude's language model
        /// capabilities, specifically optimized for medical document analysis and SPL comparison tasks.
        /// This method processes complex prompts containing medical terminology, pharmaceutical data,
        /// and regulatory documentation to provide intelligent analysis and structured responses.
        /// </summary>
        /// <param name="prompt">
        /// The comprehensive prompt string containing analysis instructions, medical document content,
        /// and specific requirements for AI processing. This should include clear directives for
        /// medical document comparison, data completeness assessment, and structured output formatting.
        /// The prompt should be optimized for medical terminology and pharmaceutical documentation
        /// analysis to ensure accurate and contextually appropriate responses.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous AI completion operation. The task result contains
        /// the Claude AI-generated response as a string, typically including structured analysis,
        /// completeness assessments, identified issues, and recommendations specific to medical
        /// document validation and SPL comparison requirements.
        /// </returns>
        /// <remarks>
        /// This method serves as the primary interface to Anthropic's Claude AI service for medical
        /// document processing operations. It handles the complete request lifecycle including:
        /// 
        /// <list type="number">
        /// <item>Prompt validation and medical context preparation</item>
        /// <item>API authentication and secure communication with Claude service</item>
        /// <item>Request formatting optimized for medical terminology processing</item>
        /// <item>Response parsing and error handling for AI service interactions</item>
        /// <item>Rate limiting and retry logic for enterprise-grade reliability</item>
        /// </list>
        /// 
        /// The method is specifically tuned for healthcare and pharmaceutical use cases, ensuring
        /// that AI responses maintain accuracy and contextual appropriateness when analyzing:
        /// - SPL (Structured Product Labeling) documents and FDA regulatory content
        /// - Clinical trial data and pharmaceutical research information  
        /// - Drug safety data, contraindications, and adverse event reporting
        /// - Dosage and administration instructions for medical professionals
        /// - Drug interaction data and pharmacokinetic information
        /// 
        /// Performance considerations include prompt size optimization, response time management,
        /// and efficient handling of large medical document analysis requests. The service
        /// implements appropriate caching strategies and request batching where applicable
        /// to minimize API usage costs while maintaining response quality.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Medical document comparison prompt
        /// string comparisonPrompt = @"
        ///     Compare the following SPL XML and JSON representations for data completeness:
        ///     
        ///     Analysis Requirements:
        ///     1. Verify all FDA-required sections are present in both formats
        ///     2. Check drug interaction completeness and accuracy
        ///     3. Validate clinical pharmacology data preservation
        ///     4. Assess contraindications and warnings completeness
        ///     5. Provide structured completion metrics
        ///     
        ///     XML Content: [SPL XML data...]
        ///     JSON Content: [Converted JSON data...]
        ///     
        ///     Provide response in structured format with completeness assessment.";
        /// 
        /// var medicalAnalysis = await claudeService.GenerateCompletionAsync(comparisonPrompt);
        /// 
        /// // Drug safety analysis prompt
        /// string safetyPrompt = @"
        ///     Analyze the following drug safety section for completeness:
        ///     - Identify missing adverse event categories
        ///     - Verify contraindication completeness  
        ///     - Check warning and precaution adequacy
        ///     - Assess drug interaction coverage
        ///     [Safety data content...]";
        /// 
        /// var safetyAnalysis = await claudeService.GenerateCompletionAsync(safetyPrompt);
        /// 
        /// // Regulatory compliance validation
        /// string compliancePrompt = @"
        ///     Validate SPL document compliance with FDA requirements:
        ///     - Check required section presence (21 CFR 201.57)
        ///     - Verify labeling content completeness
        ///     - Assess structured data formatting compliance
        ///     [Document content for validation...]";
        /// 
        /// var complianceReport = await claudeService.GenerateCompletionAsync(compliancePrompt);
        /// </code>
        /// </example>
        /// <exception cref="ArgumentException">
        /// Thrown when the prompt parameter is null, empty, or exceeds the maximum allowed
        /// token limit for Claude API processing. Medical document prompts should be optimized
        /// for size while maintaining necessary context for accurate analysis.
        /// </exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when network connectivity issues prevent communication with the Claude API
        /// service, or when API rate limits are exceeded during high-volume processing periods.
        /// </exception>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when API authentication fails due to invalid credentials, expired tokens,
        /// or insufficient permissions for accessing Claude AI services.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the Claude API service returns unexpected response formats or when
        /// service configuration is incomplete or invalid for medical document processing.
        /// </exception>
        /// <exception cref="TimeoutException">
        /// Thrown when AI completion requests exceed configured timeout limits, typically
        /// occurring with extremely large medical documents or complex analysis requirements.
        /// </exception>
        /// <seealso cref="IComparisonService.GenerateComparisonAsync(Guid, string)"/>
        /// <seealso cref="Models.ComparisonResult"/>
        /// <seealso cref="System.Net.Http.HttpClient"/>
        Task<string> GenerateDocumentComparisonAsync(string prompt);

        #endregion

        #region ai agent methods

        /**************************************************************/
        /// <summary>
        /// Creates a new conversation session and returns the server-generated conversation ID.
        /// </summary>
        /// <param name="userId">Optional encrypted user ID to associate with the conversation.</param>
        /// <returns>
        /// A task that resolves to a <see cref="Conversation"/> containing the new conversation
        /// metadata including the server-generated ID.
        /// </returns>
        /// <remarks>
        /// Use this method to explicitly start a new conversation session. The returned
        /// conversation ID should be included in subsequent <see cref="InterpretRequestAsync"/>
        /// calls to maintain context.
        /// 
        /// Alternatively, calling <see cref="InterpretRequestAsync"/> without a conversation ID
        /// will automatically create a new conversation.
        /// </remarks>
        /// <example>
        /// <code>
        /// var conversation = await claudeService.CreateConversationAsync(encryptedUserId);
        /// // Use conversation.ConversationId in subsequent requests
        /// </code>
        /// </example>
        /// <seealso cref="Conversation"/>
        /// <seealso cref="GetConversationAsync"/>
        Task<Conversation> CreateConversationAsync(string? userId = null);

        /**************************************************************/
        /// <summary>
        /// Retrieves an existing conversation by ID, including its full message history.
        /// </summary>
        /// <param name="conversationId">The conversation ID to retrieve.</param>
        /// <returns>
        /// A task that resolves to the <see cref="Conversation"/> if found and not expired,
        /// or null if the conversation does not exist or has expired.
        /// </returns>
        /// <remarks>
        /// This method does not reset the conversation's expiration timer. Use it for
        /// read-only operations like displaying conversation history to the user.
        /// </remarks>
        /// <seealso cref="Conversation"/>
        /// <seealso cref="GetConversationHistoryAsync"/>
        Task<Conversation?> GetConversationAsync(string conversationId);

        /**************************************************************/
        /// <summary>
        /// Retrieves the message history for a conversation.
        /// </summary>
        /// <param name="conversationId">The conversation ID.</param>
        /// <param name="maxMessages">Optional maximum number of recent messages to return.</param>
        /// <returns>
        /// A task that resolves to a list of messages in chronological order,
        /// or an empty list if the conversation is not found.
        /// </returns>
        /// <remarks>
        /// When <paramref name="maxMessages"/> is specified, returns the most recent messages.
        /// This is useful for limiting context window size when building prompts.
        /// </remarks>
        /// <seealso cref="AiConversationMessage"/>
        Task<List<AiConversationMessage>> GetConversationHistoryAsync(string conversationId, int? maxMessages = null);

        /**************************************************************/
        /// <summary>
        /// Deletes a conversation and its message history.
        /// </summary>
        /// <param name="conversationId">The conversation ID to delete.</param>
        /// <returns>True if the conversation was found and deleted, false otherwise.</returns>
        Task<bool> DeleteConversationAsync(string conversationId);

        /**************************************************************/
        /// <summary>
        /// Gets statistics about the conversation store.
        /// </summary>
        /// <returns>Statistics including conversation counts and message totals.</returns>
        /// <seealso cref="ConversationStoreStats"/>
        Task<ConversationStoreStats> GetConversationStatsAsync();

        /**************************************************************/
        /// <summary>
        /// Retrieves the current system context including authentication status, demo mode state,
        /// available capabilities, and database statistics. This context is used to inform Claude
        /// about the current operating environment and any limitations that apply to the session.
        /// </summary>
        /// <param name="isAuthenticated">Indicates whether the current user is authenticated.</param>
        /// <param name="userId">The encrypted user ID if authenticated, null otherwise.</param>
        /// <returns>
        /// A task that resolves to an <see cref="AiSystemContext"/> containing comprehensive
        /// system state information for AI context awareness.
        /// </returns>
        /// <remarks>
        /// The system context helps Claude provide appropriate responses based on:
        /// - Whether the user can perform write operations (requires authentication)
        /// - Whether the database is in demo mode (data may be reset periodically)
        /// - What data is available (document counts, available sections)
        /// - Rate limiting and usage quotas
        /// 
        /// This context is automatically included in interpretation requests to ensure
        /// Claude's suggestions are appropriate for the current session state.
        /// </remarks>
        /// <example>
        /// <code>
        /// var context = await claudeService.GetSystemContextAsync(User.Identity.IsAuthenticated, encryptedUserId);
        /// // Returns:
        /// // {
        /// //   IsAuthenticated: true,
        /// //   IsDemoMode: true,
        /// //   DemoModeMessage: "Database resets every 24 hours",
        /// //   DocumentCount: 150,
        /// //   AvailableSections: ["Document", "Organization", "Product", ...]
        /// // }
        /// </code>
        /// </example>
        /// <seealso cref="AiSystemContext"/>
        Task<AiSystemContext> GetSystemContextAsync(bool isAuthenticated, string? userId);

        /**************************************************************/
        /// <summary>
        /// Interprets a natural language user request and returns a structured specification
        /// of API endpoints that should be called to fulfill the request. Claude analyzes the
        /// user's intent and maps it to the available MedRecPro API operations.
        /// </summary>
        /// <param name="request">
        /// The <see cref="AiAgentRequest"/> containing the user's natural language query,
        /// conversation history, and system context.
        /// </param>
        /// <returns>
        /// A task that resolves to an <see cref="AiAgentInterpretation"/> containing:
        /// - One or more API endpoint specifications with methods, paths, and parameters
        /// - A brief explanation of the interpretation
        /// - Any clarifying questions if the request is ambiguous
        /// - Error information if the request cannot be fulfilled
        /// </returns>
        /// <remarks>
        /// The interpretation phase is the first step in the agentic workflow:
        /// 
        /// <list type="number">
        /// <item>User submits natural language request</item>
        /// <item><b>Claude interprets request → returns endpoint specifications</b></item>
        /// <item>Client executes endpoints using returned specifications</item>
        /// <item>Results sent back to Claude for synthesis</item>
        /// </list>
        /// 
        /// The interpretation includes authentication-aware suggestions - if an operation
        /// requires authentication and the user is not authenticated, Claude will indicate
        /// this rather than suggesting an endpoint that will fail.
        /// 
        /// For demo mode, Claude will note any limitations and may suggest importing
        /// sample data if the database is empty.
        /// </remarks>
        /// <example>
        /// <code>
        /// var request = new AiAgentRequest
        /// {
        ///     UserMessage = "Find all drugs manufactured by Pfizer",
        ///     ConversationId = "conv-123",
        ///     SystemContext = await GetSystemContextAsync(true, userId)
        /// };
        /// 
        /// var interpretation = await claudeService.InterpretRequestAsync(request);
        /// // Returns:
        /// // {
        /// //   Endpoints: [{
        /// //     Method: "GET",
        /// //     Path: "/api/Label/labeler/search",
        /// //     Parameters: { labelerNameSearch: "Pfizer" }
        /// //   }],
        /// //   Explanation: "I'll search for products by the labeler name 'Pfizer'",
        /// //   RequiresAuthentication: false
        /// // }
        /// </code>
        /// </example>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="request"/> or its UserMessage property is null.
        /// </exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when communication with Claude API fails.
        /// </exception>
        /// <seealso cref="AiAgentRequest"/>
        /// <seealso cref="AiAgentInterpretation"/>
        /// <seealso cref="AiEndpointSpecification"/>
        Task<AiAgentInterpretation> InterpretRequestAsync(AiAgentRequest request);

        /**************************************************************/
        /// <summary>
        /// Synthesizes API execution results into a coherent, human-readable response that
        /// addresses the user's original query. Claude analyzes the raw API data and presents
        /// it in a conversational format appropriate for the user's context.
        /// </summary>
        /// <param name="synthesisRequest">
        /// The <see cref="AiSynthesisRequest"/> containing the original user query,
        /// executed endpoint specifications, and their corresponding results.
        /// </param>
        /// <returns>
        /// A task that resolves to an <see cref="AiAgentSynthesis"/> containing:
        /// - A natural language response summarizing the results
        /// - Structured data highlights if applicable
        /// - Suggested follow-up queries
        /// - Any warnings or limitations encountered
        /// </returns>
        /// <remarks>
        /// The synthesis phase completes the agentic workflow:
        /// 
        /// <list type="number">
        /// <item>User submits natural language request</item>
        /// <item>Claude interprets request → returns endpoint specifications</item>
        /// <item>Client executes endpoints using returned specifications</item>
        /// <item><b>Results sent back to Claude → returns synthesized response</b></item>
        /// </list>
        /// 
        /// Claude considers the original user intent when synthesizing results, ensuring
        /// the response directly addresses what the user asked rather than simply
        /// dumping raw data. For complex queries, Claude may highlight key findings,
        /// provide counts and summaries, and suggest follow-up queries.
        /// </remarks>
        /// <example>
        /// <code>
        /// var synthesisRequest = new AiSynthesisRequest
        /// {
        ///     OriginalQuery = "Find all drugs manufactured by Pfizer",
        ///     ExecutedEndpoints = [{
        ///         Specification: { Method: "GET", Path: "/api/Label/labeler/search", ... },
        ///         Result: [{ ProductName: "LIPITOR", ... }, ...]
        ///     }]
        /// };
        /// 
        /// var synthesis = await claudeService.SynthesizeResultsAsync(synthesisRequest);
        /// // Returns:
        /// // {
        /// //   Response: "I found 47 products manufactured by Pfizer Inc. The most notable include 
        /// //              LIPITOR (atorvastatin), VIAGRA (sildenafil), and ZOLOFT (sertraline)...",
        /// //   DataHighlights: { TotalProducts: 47, TopProducts: [...] },
        /// //   SuggestedFollowUps: ["Show details for LIPITOR", "Find generic alternatives"]
        /// // }
        /// </code>
        /// </example>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="synthesisRequest"/> is null or missing required data.
        /// </exception>
        /// <exception cref="HttpRequestException">
        /// Thrown when communication with Claude API fails.
        /// </exception>
        /// <seealso cref="AiSynthesisRequest"/>
        /// <seealso cref="AiAgentSynthesis"/>
        Task<AiAgentSynthesis> SynthesizeResultsAsync(AiSynthesisRequest synthesisRequest);

        /**************************************************************/
        /// <summary>
        /// Retrieves the skills document containing all available API endpoint specifications
        /// that Claude can use when interpreting user requests. This document serves as
        /// Claude's reference for understanding what operations are possible within MedRecPro.
        /// </summary>
        /// <returns>
        /// A task that resolves to the skills document as a formatted string, containing
        /// comprehensive documentation of all available endpoints, their parameters,
        /// expected responses, and usage examples.
        /// </returns>
        /// <remarks>
        /// The skills document is a structured reference that tells Claude:
        /// - What API endpoints are available
        /// - What parameters each endpoint accepts
        /// - What data each endpoint returns
        /// - When to use each endpoint based on user intent
        /// - Authentication requirements for each operation
        /// 
        /// This document is included in the system prompt when interpreting requests,
        /// enabling Claude to make informed decisions about which endpoints to suggest.
        /// </remarks>
        /// <seealso cref="InterpretRequestAsync"/>
        Task<string> GetSkillsDocumentAsync();

        /**************************************************************/
        /// <summary>
        /// Attempts to re-interpret a user request when initial API endpoints fail.
        /// Uses Claude to suggest alternative endpoints based on the failed results,
        /// implementing a recursive retry strategy before giving up.
        /// </summary>
        /// <param name="originalRequest">The original user request that was interpreted.</param>
        /// <param name="failedResults">The endpoint execution results that failed (404, 500, etc.).</param>
        /// <param name="attemptNumber">Current retry attempt number (starts at 1, max 3).</param>
        /// <returns>
        /// A task that resolves to an <see cref="AiAgentInterpretation"/> containing
        /// alternative endpoint specifications to try, or a direct response explaining
        /// why the data cannot be retrieved.
        /// </returns>
        /// <remarks>
        /// This method implements intelligent retry logic for failed API calls:
        /// 
        /// <list type="number">
        /// <item>Analyzes why endpoints failed (404 = not found, 500 = server error)</item>
        /// <item>Consults the skills document for alternative endpoints</item>
        /// <item>Suggests fallback paths (e.g., views → label/section)</item>
        /// <item>After 3 attempts, returns a direct response with explanation</item>
        /// </list>
        /// 
        /// Common retry scenarios:
        /// - View endpoint returns 404 → Try label/section/{table}
        /// - Search returns empty → Try broader query or different table
        /// - Table name incorrect → Try case variations or related tables
        /// </remarks>
        /// <example>
        /// <code>
        /// // First interpretation suggested /api/Label/ingredient/summaries but it returned 404
        /// var failedResults = new List&lt;AiEndpointResult&gt; {
        ///     new AiEndpointResult {
        ///         Specification = new AiEndpointSpecification { Path = "/api/Label/ingredient/summaries" },
        ///         StatusCode = 404,
        ///         Error = "Not Found"
        ///     }
        /// };
        /// 
        /// var retryInterpretation = await claudeService.RetryInterpretationAsync(
        ///     originalRequest, failedResults, attemptNumber: 1);
        /// 
        /// // Returns new endpoints to try:
        /// // { Endpoints: [{ Path: "/api/label/section/ActiveIngredient" }] }
        /// </code>
        /// </example>
        /// <seealso cref="InterpretRequestAsync"/>
        /// <seealso cref="SynthesizeResultsAsync"/>
        Task<AiAgentInterpretation> RetryInterpretationAsync(
            AiAgentRequest originalRequest,
            List<AiEndpointResult> failedResults,
            int attemptNumber);

        #endregion
    }

    #endregion

    #region Claude API service implementation

    public class ClaudeApiService : IClaudeApiService
    {
        #region ai agent private fields

        /**************************************************************/
        /// <summary>
        /// Database context for querying system state (document/product counts).
        /// </summary>
        private readonly ApplicationDbContext _dbContext;

        /**************************************************************/
        /// <summary>
        /// Configuration provider for feature flags and settings.
        /// </summary>
        private readonly IConfiguration _configuration;

        /**************************************************************/
        /// <summary>
        /// Cached skills document content.
        /// </summary>
        private string? _skillsDocumentCache;

        /**************************************************************/
        /// <summary>
        /// Timestamp of last skills document cache refresh.
        /// </summary>
        private DateTime _skillsCacheTimestamp = DateTime.MinValue;

        /**************************************************************/
        /// <summary>
        /// Cache duration for skills document (1 hour).
        /// </summary>
        private readonly TimeSpan _skillsCacheDuration = TimeSpan.FromHours(1);

        /**************************************************************/
        /// <summary>
        /// In-memory conversation store for tracking AI conversation sessions.
        /// </summary>
        private readonly ConversationStore _conversationStore;

        /**************************************************************/
        /// <summary>
        /// Skill service for two-stage routing and skill document management.
        /// </summary>
        private readonly IClaudeSkillService _skillService;


        /**************************************************************/
        /// <summary>
        /// Maximum number of conversation messages to include in prompt context.
        /// </summary>
        private const int MaxConversationContextMessages = 10;

        #endregion

        private readonly HttpClient _httpClient;
        private readonly ILogger<ClaudeApiService> _logger;
        private readonly ClaudeApiSettings _settings;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the ClaudeApiService with required dependencies.
        /// </summary>
        /// <param name="httpClient">HTTP client for API communication.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="settings">Claude API settings.</param>
        /// <param name="dbContext">Database context for system state queries.</param>
        /// <param name="configuration">Configuration provider for feature flags.</param>
        /// <param name="conversationStore">In-memory store for conversation tracking.</param>
        /// <param name="skillService">Skill service for two-stage routing and skill document management.</param>
        public ClaudeApiService(
            HttpClient httpClient,
            ILogger<ClaudeApiService> logger,
            IOptions<ClaudeApiSettings> settings,
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            ConversationStore conversationStore,
            IClaudeSkillService skillService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = settings.Value;
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
            _skillService = skillService ?? throw new ArgumentNullException(nameof(skillService));
            _logger.LogInformation($"Claude API Key configured: {!string.IsNullOrEmpty(_settings.ApiKey)}");
        }

        /**************************************************************/
        /// <summary>
        /// Generates a document comparison analysis using Claude AI, returning the raw JSON response
        /// text for parsing by specialized document analysis methods.
        /// </summary>
        /// <param name="prompt">The comparison analysis prompt containing XML and JSON content.</param>
        /// <returns>The raw JSON response text from Claude AI for structured parsing.</returns>
        /// <exception cref="HttpRequestException">Thrown when Claude API request fails.</exception>
        public async Task<string> GenerateDocumentComparisonAsync(string prompt)
        {
            try
            {
                var request = new
                {
                    model = _settings.Model,
                    max_tokens = _settings.MaxTokens,
                    temperature = _settings.Temperature,
                    messages = new[]
                    {
                new { role = "user", content = prompt }
            }
                };

                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Claude API error: {response.StatusCode} - {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var claudeResponse = JsonConvert.DeserializeObject<ClaudeApiResponse>(responseContent);

                return claudeResponse?.Content?.FirstOrDefault()?.Text ?? "No response generated";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Claude API for document comparison");
                throw;
            }
        }

        #region ai agent public methods

        #region conversation management

        /**************************************************************/
        /// <inheritdoc/>
        public Task<Conversation> CreateConversationAsync(string? userId = null)
        {
            #region implementation

            var conversation = _conversationStore.Create(userId);

            _logger.LogInformation("Created new conversation {ConversationId} for user {UserId}",
                conversation.ConversationId, userId ?? "anonymous");

            return Task.FromResult(conversation);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<Conversation?> GetConversationAsync(string conversationId)
        {
            #region implementation

            var conversation = _conversationStore.Get(conversationId);

            return Task.FromResult(conversation);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<AiConversationMessage>> GetConversationHistoryAsync(string conversationId, int? maxMessages = null)
        {
            #region implementation

            var messages = _conversationStore.GetMessages(conversationId, maxMessages);

            return Task.FromResult(messages);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<bool> DeleteConversationAsync(string conversationId)
        {
            #region implementation

            var result = _conversationStore.Remove(conversationId);

            if (result)
            {
                _logger.LogInformation("Deleted conversation {ConversationId}", conversationId);
            }

            return Task.FromResult(result);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<ConversationStoreStats> GetConversationStatsAsync()
        {
            #region implementation

            var stats = _conversationStore.GetStats();

            return Task.FromResult(stats);

            #endregion
        }

        #endregion

        #region interpretation and synthesis

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<AiSystemContext> GetSystemContextAsync(bool isAuthenticated, string? userId)
        {
            #region implementation

            _logger.LogDebug("Building system context. Authenticated: {IsAuthenticated}", isAuthenticated);

            try
            {
                // Check demo mode from configuration
                var isDemoMode = _configuration.GetValue<bool>("DemoModeSettings:Enabled", false);
                var demoResetMinutes = _configuration.GetValue<int>("DemoModeSettings:ResetIntervalMinutes", 1440);
                var importEnabled = _configuration.GetValue<bool>("FeatureFlags:SplImportEnabled", true);
                var comparisonEnabled = _configuration.GetValue<bool>("FeatureFlags:ComparisonAnalysisEnabled", true);

                //// Query document and product counts
                var documentCount = _dbContext.Set<Label.Document>().Count();
                var productCount = _dbContext.Set<Label.Product>().Count();

                // Build list of available sections (entity types)
                var availableSections = getAvailableSections();

                // Build list of available navigation views
                var availableViews = new List<string>
                {
                    "application-number",
                    "pharmacologic-class",
                    "ingredient",
                    "ndc",
                    "labeler",
                    "document",
                    "section"
                };

                return new AiSystemContext
                {
                    IsAuthenticated = isAuthenticated,
                    UserId = userId,
                    IsDemoMode = isDemoMode,
                    DemoModeMessage = isDemoMode
                        ? $"Demo mode is active. Database resets every {demoResetMinutes / 60} hours. " +
                          "User data is preserved but label data may be removed."
                        : null,
                    IsDatabaseEmpty = documentCount == 0,
                    DocumentCount = documentCount,
                    ProductCount = productCount,
                    AvailableSections = availableSections,
                    AvailableViews = availableViews,
                    ImportEnabled = importEnabled && isAuthenticated,
                    ComparisonAnalysisEnabled = comparisonEnabled
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building system context");

                // Return minimal context on error
                return new AiSystemContext
                {
                    IsAuthenticated = isAuthenticated,
                    UserId = userId,
                    IsDemoMode = false,
                    IsDatabaseEmpty = true
                };
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<AiAgentInterpretation> InterpretRequestAsync(AiAgentRequest request)
        {
            #region input validation

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.UserMessage))
            {
                return new AiAgentInterpretation
                {
                    Success = false,
                    Error = "User message cannot be empty."
                };
            }

            #endregion

            #region implementation

            _logger.LogInformation("Interpreting user request: {MessagePreview}",
                request.UserMessage.Length > 100
                    ? request.UserMessage[..100] + "..."
                    : request.UserMessage);

            try
            {
                // Get or create conversation for server-side tracking
                var conversation = _conversationStore.GetOrCreate(
                    request.ConversationId,
                    request.SystemContext?.UserId);

                // Store the conversation ID back in the request for response
                request.ConversationId = conversation.ConversationId;

                // Add user message to conversation history
                _conversationStore.AddMessage(conversation.ConversationId, "user", request.UserMessage);

                // Load conversation history from server store (overrides any client-provided history)
                request.ConversationHistory = _conversationStore.GetMessages(
                    conversation.ConversationId,
                    MaxConversationContextMessages);

                // Build the interpretation prompt
                var prompt = await buildInterpretationPromptAsync(request);

                // Call Claude API using existing method
                var claudeResponse = await GenerateDocumentComparisonAsync(prompt);

                // Parse Claude's response into structured interpretation
                var interpretation = parseInterpretationResponse(claudeResponse, request.SystemContext);

                // Include conversation ID in response for client reference
                interpretation.ConversationId = conversation.ConversationId;

                // Add assistant response to conversation history
                var assistantMessage = interpretation.IsDirectResponse
                    ? interpretation.DirectResponse ?? interpretation.Explanation ?? "Response generated"
                    : interpretation.Explanation ?? "Endpoints suggested";

                _conversationStore.AddMessage(conversation.ConversationId, "assistant", assistantMessage);

                _logger.LogInformation("Successfully interpreted request. ConversationId: {ConversationId}, Endpoints: {EndpointCount}",
                    conversation.ConversationId, interpretation.Endpoints?.Count ?? 0);

                return interpretation;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Claude API communication error during interpretation");

                return new AiAgentInterpretation
                {
                    Success = false,
                    ConversationId = request.ConversationId,
                    Error = "Unable to process your request. The AI service is temporarily unavailable.",
                    Suggestions = new List<string>
                    {
                        "Try again in a few moments",
                        "Use the API documentation to construct your query manually"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during request interpretation");

                return new AiAgentInterpretation
                {
                    Success = false,
                    ConversationId = request.ConversationId,
                    Error = "An unexpected error occurred while processing your request."
                };
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<AiAgentSynthesis> SynthesizeResultsAsync(AiSynthesisRequest synthesisRequest)
        {
            #region input validation

            if (synthesisRequest == null)
            {
                throw new ArgumentNullException(nameof(synthesisRequest));
            }

            if (string.IsNullOrWhiteSpace(synthesisRequest.OriginalQuery))
            {
                return new AiAgentSynthesis
                {
                    Response = "Unable to synthesize results without the original query.",
                    IsComplete = false
                };
            }

            #endregion

            #region implementation

            _logger.LogInformation("Synthesizing results for query: {QueryPreview}",
                synthesisRequest.OriginalQuery.Length > 100
                    ? synthesisRequest.OriginalQuery[..100] + "..."
                    : synthesisRequest.OriginalQuery);

            try
            {
                // Build the synthesis prompt
                var prompt = buildSynthesisPrompt(synthesisRequest);

                // Call Claude API using existing method
                var claudeResponse = await GenerateDocumentComparisonAsync(prompt);

                // Parse Claude's response into structured synthesis
                var synthesis = parseSynthesisResponse(claudeResponse);

                // Include conversation ID in response
                synthesis.ConversationId = synthesisRequest.ConversationId;

                // Extract and populate document reference links for full label viewing
                synthesis.DataReferences = extractDocumentReferences(synthesisRequest);

                // Update conversation history with the synthesis result if we have a conversation ID
                if (!string.IsNullOrEmpty(synthesisRequest.ConversationId))
                {
                    // Touch the conversation to reset expiration
                    _conversationStore.Touch(synthesisRequest.ConversationId);

                    // Optionally add a summary of results to history
                    // (The interpretation already added the assistant's explanation,
                    // so we only add if the synthesis provides meaningful new content)
                    if (!string.IsNullOrEmpty(synthesis.Response) && synthesis.Response.Length < 500)
                    {
                        // Update the last assistant message with the actual synthesized response
                        // This overwrites the "Endpoints suggested" placeholder with real results
                    }
                }

                _logger.LogInformation("Successfully synthesized results for conversation {ConversationId}",
                    synthesisRequest.ConversationId ?? "none");

                return synthesis;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Claude API communication error during synthesis");

                // Provide fallback response with raw data
                var fallback = buildFallbackSynthesis(synthesisRequest);
                fallback.ConversationId = synthesisRequest.ConversationId;
                // Still provide document links even when synthesis fails
                fallback.DataReferences = extractDocumentReferences(synthesisRequest);
                return fallback;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during result synthesis");

                return new AiAgentSynthesis
                {
                    Response = "An error occurred while processing the results. Please review the raw data.",
                    ConversationId = synthesisRequest.ConversationId,
                    IsComplete = false,
                    Warnings = new List<string> { "Synthesis failed - showing raw results" }
                };
            }

            #endregion
        }

        #endregion

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<string> GetSkillsDocumentAsync()
        {
            #region implementation

            // Delegate to skill service for centralized skill management
            return await _skillService.GetFullSkillsDocumentAsync();

            #endregion
        }

        #endregion

        #region ai agent private methods

        /**************************************************************/
        /// <summary>
        /// Gets the list of available label sections (entity types).
        /// </summary>
        /// <returns>List of section names.</returns>
        private List<string> getAvailableSections()
        {
            #region implementation

            // These correspond to nested classes in Label model
            return new List<string>
            {
                "Document",
                "Organization",
                "Product",
                "ActiveMoiety",
                "ActiveIngredient",
                "InactiveIngredient",
                "Section",
                "Subsection",
                "PackagingLevel",
                "PackageItem",
                "ProductIdentifier",
                "PackageIdentifier",
                "Characteristic",
                "MarketingCategory",
                "Route",
                "EquivalentSubstance",
                "PharmacologicClass",
                "DrugInteraction",
                "ContraindicatedDrug",
                "ItemContains",
                "ContainedItem",
                "Address",
                "BusinessOperation"
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the prompt for Claude to interpret a user request.
        /// Uses two-stage routing to minimize token usage by loading only relevant skills.
        /// </summary>
        /// <param name="request">The user's request.</param>
        /// <returns>The complete prompt string.</returns>
        /// <remarks>
        /// Two-stage routing pattern:
        /// Stage 1: Use keyword-based skill selection (fast, no API call)
        /// Stage 2: Load only the selected skill(s) into the prompt
        /// This reduces prompt size from ~10,000+ tokens to only what's needed.
        /// </remarks>
        /// <seealso cref="IClaudeSkillService.SelectSkillsAsync"/>
        /// <seealso cref="IClaudeSkillService.GetSkillContentAsync"/>
        private async Task<string> buildInterpretationPromptAsync(AiAgentRequest request)
        {
            #region implementation

            // Stage 1: Select relevant skills based on user message
            var skillSelection = await _skillService.SelectSkillsAsync(
                request.UserMessage,
                request.SystemContext);

            // Stage 2: Load only the selected skills
            var skills = await _skillService.GetSkillContentAsync(skillSelection);

            _logger.LogDebug("Two-stage routing: Selected skills [{Skills}] for query",
                string.Join(", ", skillSelection.SelectedSkills));

            var sb = new StringBuilder();

            // System instructions
            sb.AppendLine("You are an AI assistant for MedRecPro, a pharmaceutical labeling management system.");
            sb.AppendLine("Your task is to interpret user requests and return structured API endpoint specifications.");
            sb.AppendLine();

            // Include selected skills document (not all skills)
            sb.AppendLine("=== AVAILABLE API ENDPOINTS ===");
            sb.AppendLine(skills);
            sb.AppendLine();

            // Include system context
            if (request.SystemContext != null)
            {
                sb.AppendLine("=== CURRENT SYSTEM STATE ===");
                sb.AppendLine($"User Authenticated: {request.SystemContext.IsAuthenticated}");
                sb.AppendLine($"Demo Mode: {request.SystemContext.IsDemoMode}");

                if (request.SystemContext.IsDemoMode)
                {
                    sb.AppendLine($"Demo Mode Note: {request.SystemContext.DemoModeMessage}");
                }

                if (request.SystemContext.IsDatabaseEmpty)
                {
                    sb.AppendLine("DATABASE IS EMPTY: User should import SPL label ZIP files to populate data.");
                    sb.AppendLine("Suggest using POST /api/label/import with ZIP files from DailyMed.");
                }
                else
                {
                    sb.AppendLine($"Documents Available: {request.SystemContext.DocumentCount}");
                    sb.AppendLine($"Products Available: {request.SystemContext.ProductCount}");
                }

                sb.AppendLine($"Import Enabled: {request.SystemContext.ImportEnabled}");
                sb.AppendLine();

                // Include import result context if present
                // Include import result context if present
                if (request.ImportResult != null)
                {
                    sb.AppendLine("=== RECENT IMPORT OPERATION ===");

                    if (request.ImportResult.Success)
                    {
                        sb.AppendLine($"Status: Import COMPLETED SUCCESSFULLY");
                        sb.AppendLine($"Documents Imported: {request.ImportResult.DocumentIds?.Count ?? 0}");
                        sb.AppendLine($"Files Processed: {request.ImportResult.TotalFilesProcessed}");
                        sb.AppendLine($"Files Succeeded: {request.ImportResult.TotalFilesSucceeded}");

                        // Include statistics if available
                        if (request.ImportResult.Statistics != null)
                        {
                            var stats = request.ImportResult.Statistics;
                            sb.AppendLine("Import Statistics:");
                            if (stats.DocumentsCreated > 0)
                                sb.AppendLine($"  - Documents Created: {stats.DocumentsCreated}");
                            if (stats.OrganizationsCreated > 0)
                                sb.AppendLine($"  - Organizations Created: {stats.OrganizationsCreated}");
                            if (stats.ProductsCreated > 0)
                                sb.AppendLine($"  - Products Created: {stats.ProductsCreated}");
                            if (stats.SectionsCreated > 0)
                                sb.AppendLine($"  - Sections Created: {stats.SectionsCreated}");
                            if (stats.IngredientsCreated > 0)
                                sb.AppendLine($"  - Ingredients Created: {stats.IngredientsCreated}");
                            if (stats.ProductElementsCreated > 0)
                                sb.AppendLine($"  - Product Elements Created: {stats.ProductElementsCreated}");
                        }

                        if (request.ImportResult.DocumentIds?.Any() == true)
                        {
                            sb.AppendLine("Imported Document GUIDs:");
                            foreach (var docId in request.ImportResult.DocumentIds)
                            {
                                sb.AppendLine($"  - {docId}");
                            }
                        }

                        if (request.ImportResult.DocumentNames?.Any() == true)
                        {
                            sb.AppendLine("Imported Document Files:");
                            foreach (var name in request.ImportResult.DocumentNames)
                            {
                                sb.AppendLine($"  - {name}");
                            }
                        }

                        // Include progress URL reference for successful imports (useful for documentation)
                        if (!string.IsNullOrEmpty(request.ImportResult.OperationId))
                        {
                            sb.AppendLine($"Operation ID: {request.ImportResult.OperationId}");
                            sb.AppendLine($"Progress endpoint used: GET /api/Label/import/progress/{request.ImportResult.OperationId}");
                        }

                        // Provide viewable document links for each imported document
                        if (request.ImportResult.DocumentIds?.Any() == true)
                        {
                            sb.AppendLine();
                            sb.AppendLine("=== DOCUMENT VIEW LINKS (include in dataReferences) ===");
                            foreach (var docGuid in request.ImportResult.DocumentIds)
                            {
                                sb.AppendLine($"  - View Label XML: /api/Label/generate/{docGuid}/true");
                                sb.AppendLine($"  - View Full Document: /api/Label/single/{docGuid}");
                            }
                        }

                        sb.AppendLine();
                        sb.AppendLine("IMPORTANT RESPONSE INSTRUCTIONS:");
                        sb.AppendLine("1. Acknowledge the successful import to the user with specific details.");
                        sb.AppendLine("2. Mention the number of documents, sections, and other entities created.");
                        sb.AppendLine("3. In your JSON response, include 'dataReferences' with clickable links to view each imported label.");
                        sb.AppendLine("   Format: { \"View Label - {documentGuid}\": \"/api/Label/generate/{documentGuid}/true\" }");

                        if (!string.IsNullOrEmpty(request.ImportResult.OperationId))
                        {
                            sb.AppendLine($"4. Include progress endpoint in dataReferences: {{ \"Check Import Progress\": \"/api/Label/import/progress/{request.ImportResult.OperationId}\" }}");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"Status: Import FAILED or INCOMPLETE");
                        sb.AppendLine($"Message: {request.ImportResult.Message}");

                        if (!string.IsNullOrEmpty(request.ImportResult.Error))
                        {
                            sb.AppendLine($"Error: {request.ImportResult.Error}");
                        }

                        // Include progress URL for checking import status
                        if (!string.IsNullOrEmpty(request.ImportResult.OperationId))
                        {
                            sb.AppendLine($"Operation ID: {request.ImportResult.OperationId}");
                        }

                        if (!string.IsNullOrEmpty(request.ImportResult.ProgressUrl))
                        {
                            sb.AppendLine($"Progress URL: {request.ImportResult.ProgressUrl}");
                            sb.AppendLine($"The user can check the current import status at: GET {request.ImportResult.ProgressUrl}");
                        }
                        else if (!string.IsNullOrEmpty(request.ImportResult.OperationId))
                        {
                            // Construct progress URL if we have operation ID
                            sb.AppendLine($"Progress URL: GET /api/Label/import/progress/{request.ImportResult.OperationId}");
                            sb.AppendLine($"The user can check the current import status using this endpoint.");
                        }

                        sb.AppendLine();
                        sb.AppendLine("IMPORTANT RESPONSE INSTRUCTIONS:");
                        sb.AppendLine("1. Inform the user about the import issue and suggest next steps.");
                        sb.AppendLine("2. In your JSON response, include 'dataReferences' with a link to check the import progress.");

                        if (!string.IsNullOrEmpty(request.ImportResult.OperationId))
                        {
                            sb.AppendLine($"3. Include in dataReferences: {{ \"Check Import Progress\": \"/api/Label/import/progress/{request.ImportResult.OperationId}\" }}");
                        }

                        sb.AppendLine("4. Possible suggestions: check import progress, wait for completion, try different files, check file format, verify authentication.");
                    }

                    sb.AppendLine();
                }
            }

            // Include conversation history if present
            if (request.ConversationHistory?.Any() == true)
            {
                sb.AppendLine("=== CONVERSATION HISTORY ===");

                foreach (var msg in request.ConversationHistory.TakeLast(5))
                {
                    sb.AppendLine($"{msg.Role}: {msg.Content}");
                }

                sb.AppendLine();
            }

            // Output format instructions
            sb.AppendLine("=== OUTPUT FORMAT ===");

            // Load prompt instructions from skills file
            sb.AppendLine(buildLabelSectionPromptSkills());

            // User request
            sb.AppendLine("=== USER REQUEST ===");
            sb.AppendLine(request.UserMessage);

            return sb.ToString();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the label section content query prompt instructions from the skills file.
        /// </summary>
        /// <remarks>
        /// This method delegates to the skill service for centralized skill management.
        /// Loads the AI prompt instructions for interpreting user queries about drug
        /// label content (side effects, warnings, dosing, etc.).
        /// </remarks>
        /// <example>
        /// var promptInstructions = buildLabelSectionPromptSkills();
        /// sb.AppendLine(promptInstructions);
        /// </example>
        /// <returns>The label section prompt skills document as a formatted string.</returns>
        /// <seealso cref="IClaudeSkillService.GetLabelSectionPromptSkills"/>
        private string buildLabelSectionPromptSkills()
        {
            #region implementation

            return _skillService.GetLabelSectionPromptSkills();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads the synthesis prompt skills document from the configured file path.
        /// </summary>
        /// <remarks>
        /// This method delegates to the skill service for centralized skill management.
        /// Loads the AI prompt instructions for synthesizing API results into helpful,
        /// conversational responses.
        /// </remarks>
        /// <example>
        /// var synthesisSkills = buildSynthesisPromptSkills();
        /// sb.AppendLine(synthesisSkills);
        /// </example>
        /// <returns>The synthesis prompt skills document as a formatted string.</returns>
        /// <seealso cref="IClaudeSkillService.GetSynthesisPromptSkills"/>
        private string buildSynthesisPromptSkills()
        {
            #region implementation

            return _skillService.GetSynthesisPromptSkills();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Loads the retry prompt skills document from the configured file path.
        /// </summary>
        /// <remarks>
        /// This method delegates to the skill service for centralized skill management.
        /// Loads the AI prompt instructions for re-interpreting failed API endpoint
        /// calls and suggesting alternative endpoints.
        /// </remarks>
        /// <example>
        /// var retrySkills = buildRetryPromptSkills();
        /// sb.AppendLine(retrySkills);
        /// </example>
        /// <returns>The retry prompt skills document as a formatted string.</returns>
        /// <seealso cref="IClaudeSkillService.GetRetryPromptSkills"/>
        private string buildRetryPromptSkills()
        {
            #region implementation

            return _skillService.GetRetryPromptSkills();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses Claude's response into an AiAgentInterpretation.
        /// </summary>
        /// <param name="response">Claude's raw response.</param>
        /// <param name="context">System context for validation.</param>
        /// <returns>Parsed interpretation.</returns>
        private AiAgentInterpretation parseInterpretationResponse(string response, AiSystemContext? context)
        {
            #region implementation

            try
            {
                // Find JSON in response (may be wrapped in markdown code blocks)
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart + 1);

                    var interpretation = JsonConvert.DeserializeObject<AiAgentInterpretation>(jsonContent);

                    if (interpretation != null)
                    {
                        // Validate authentication requirements
                        if (interpretation.RequiresAuthentication && context?.IsAuthenticated == false)
                        {
                            interpretation.Success = false;
                            interpretation.Error = "This operation requires authentication. Please log in first.";
                            interpretation.Endpoints.Clear();
                        }

                        return interpretation;
                    }
                }

                // Fallback if parsing fails
                _logger.LogWarning("Failed to parse Claude response as JSON. Response: {Response}",
                    response.Length > 500 ? response[..500] : response);

                return new AiAgentInterpretation
                {
                    Success = false,
                    Error = "Unable to parse AI response. Please try rephrasing your request.",
                    IsDirectResponse = true,
                    DirectResponse = response
                };
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                _logger.LogWarning(ex, "JSON parsing error in interpretation response");

                return new AiAgentInterpretation
                {
                    Success = false,
                    Error = "Unable to parse AI response format.",
                    IsDirectResponse = true,
                    DirectResponse = response
                };
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the prompt for Claude to synthesize API results.
        /// Includes guidance for navigating SPL label structures.
        /// </summary>
        /// <param name="request">The synthesis request.</param>
        /// <returns>The complete prompt string.</returns>
        /// <seealso cref="buildSynthesisPromptSkills"/>
        private string buildSynthesisPrompt(AiSynthesisRequest request)
        {
            #region implementation

            var sb = new StringBuilder();

            // Load synthesis prompt skills from file
            sb.AppendLine(buildSynthesisPromptSkills());
            sb.AppendLine();

            // Original query
            sb.AppendLine("=== ORIGINAL USER QUERY ===");
            sb.AppendLine(request.OriginalQuery);
            sb.AppendLine();

            // API results with smart truncation
            sb.AppendLine("=== API RESULTS ===");

            foreach (var result in request.ExecutedEndpoints)
            {
                sb.AppendLine($"Endpoint: {result.Specification.Method} {result.Specification.Path}");
                sb.AppendLine($"Status: {result.StatusCode}");
                sb.AppendLine($"Execution Time: {result.ExecutionTimeMs}ms");

                if (!string.IsNullOrEmpty(result.Error))
                {
                    sb.AppendLine($"Error: {result.Error}");
                }
                else if (result.Result != null)
                {
                    // Use token-efficient serialization for collections (pipe-delimited format)
                    // Falls back to JSON for single objects or when pipe serialization fails
                    var serializedResult = serializeResultForPrompt(result.Result);

                    // Increased truncation limit for label data, with smart handling
                    int maxLength = 50000;

                    if (serializedResult.Length > maxLength)
                    {
                        // Try to find and preserve important sections
                        var truncatedResult = smartTruncateLabelData(serializedResult, maxLength, request.OriginalQuery);
                        sb.AppendLine($"Result (smart truncation applied, {serializedResult.Length} chars total):");
                        sb.AppendLine(truncatedResult);
                    }
                    else
                    {
                        sb.AppendLine($"Result: {serializedResult}");
                    }
                }

                sb.AppendLine();
            }

            return sb.ToString();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Smart truncation that tries to preserve sections relevant to the user query.
        /// </summary>
        /// <param name="json">The full JSON string.</param>
        /// <param name="maxLength">Maximum length to return.</param>
        /// <param name="userQuery">The user's query to determine relevance.</param>
        /// <returns>Truncated JSON with relevant sections preserved.</returns>
        private string smartTruncateLabelData(string json, int maxLength, string userQuery)
        {
            #region implementation

            var queryLower = userQuery.ToLower();
            var sb = new StringBuilder();

            // Determine which section types are relevant based on query
            var relevantTerms = new List<string>();

            if (queryLower.Contains("side effect") || queryLower.Contains("adverse"))
            {
                relevantTerms.AddRange(new[] { "ADVERSE", "34084-4", "side effect" });
            }
            if (queryLower.Contains("warning") || queryLower.Contains("precaution"))
            {
                relevantTerms.AddRange(new[] { "WARNING", "PRECAUTION", "43685-7", "34071-1" });
            }
            if (queryLower.Contains("dose") || queryLower.Contains("dosage") || queryLower.Contains("how to"))
            {
                relevantTerms.AddRange(new[] { "DOSAGE", "ADMINISTRATION", "34068-7" });
            }
            if (queryLower.Contains("interact"))
            {
                relevantTerms.AddRange(new[] { "INTERACTION", "34073-7" });
            }
            if (queryLower.Contains("contraind") || queryLower.Contains("should not"))
            {
                relevantTerms.AddRange(new[] { "CONTRAINDICATION", "34070-3" });
            }

            // If no specific terms matched, include common clinical sections
            if (relevantTerms.Count == 0)
            {
                relevantTerms.AddRange(new[] { "ADVERSE", "WARNING", "INDICATION", "DOSAGE" });
            }

            // Try to extract relevant portions
            // First, include the document header (first ~2000 chars typically has document metadata)
            var headerLength = Math.Min(3000, json.Length);
            sb.AppendLine(json.Substring(0, headerLength));

            if (json.Length > headerLength)
            {
                sb.AppendLine("... [document header above, searching for relevant sections] ...");

                // Search for each relevant term and extract surrounding context
                foreach (var term in relevantTerms.Distinct())
                {
                    var searchIndex = 0;
                    while (searchIndex < json.Length && sb.Length < maxLength - 2000)
                    {
                        var foundIndex = json.IndexOf(term, searchIndex, StringComparison.OrdinalIgnoreCase);
                        if (foundIndex < 0) break;

                        // Extract context around the match (2000 chars before and after)
                        var contextStart = Math.Max(0, foundIndex - 500);
                        var contextEnd = Math.Min(json.Length, foundIndex + 2500);

                        // Find section boundaries if possible
                        var sectionStart = json.LastIndexOf("\"section\":", contextStart);
                        if (sectionStart >= 0 && sectionStart > contextStart - 1000)
                        {
                            contextStart = sectionStart;
                        }

                        sb.AppendLine();
                        sb.AppendLine($"... [section containing '{term}'] ...");
                        sb.AppendLine(json.Substring(contextStart, contextEnd - contextStart));

                        searchIndex = contextEnd;
                    }
                }

                if (sb.Length < maxLength - 1000)
                {
                    // Add trailer
                    var trailerStart = Math.Max(sb.Length, json.Length - 1000);
                    sb.AppendLine();
                    sb.AppendLine("... [end of document] ...");
                    sb.AppendLine(json.Substring(trailerStart));
                }
            }

            var result = sb.ToString();
            if (result.Length > maxLength)
            {
                result = result.Substring(0, maxLength) + "\n... (truncated)";
            }

            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses Claude's response into an AiAgentSynthesis.
        /// </summary>
        /// <param name="response">Claude's raw response.</param>
        /// <returns>Parsed synthesis.</returns>
        private AiAgentSynthesis parseSynthesisResponse(string response)
        {
            #region implementation

            try
            {
                // Find JSON in response
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart + 1);

                    var synthesis = JsonConvert.DeserializeObject<AiAgentSynthesis>(jsonContent);

                    if (synthesis != null)
                    {
                        return synthesis;
                    }
                }

                // Use raw response if JSON parsing fails
                return new AiAgentSynthesis
                {
                    Response = response,
                    IsComplete = true
                };
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return new AiAgentSynthesis
                {
                    Response = response,
                    IsComplete = true
                };
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a fallback synthesis when Claude API fails.
        /// </summary>
        /// <param name="request">The synthesis request.</param>
        /// <returns>Fallback synthesis with raw data summary.</returns>
        private AiAgentSynthesis buildFallbackSynthesis(AiSynthesisRequest request)
        {
            #region implementation

            var sb = new StringBuilder();
            sb.AppendLine("Here are the results from your query:");
            sb.AppendLine();

            foreach (var result in request.ExecutedEndpoints)
            {
                if (result.StatusCode >= 200 && result.StatusCode < 300)
                {
                    if (result.Result is JsonElement element)
                    {
                        if (element.ValueKind == JsonValueKind.Array)
                        {
                            sb.AppendLine($"Found {element.GetArrayLength()} results.");
                        }
                        else
                        {
                            sb.AppendLine("Request completed successfully.");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($"Request to {result.Specification.Path} returned status {result.StatusCode}.");

                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        sb.AppendLine($"Error: {result.Error}");
                    }
                }
            }

            return new AiAgentSynthesis
            {
                Response = sb.ToString(),
                IsComplete = true,
                Warnings = new List<string>
                {
                    "AI synthesis unavailable - showing basic summary"
                }
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts document GUIDs from synthesis request results and builds data reference links.
        /// </summary>
        /// <param name="request">The synthesis request containing executed endpoint results.</param>
        /// <returns>
        /// Dictionary of display names to API URLs for viewing full label documents.
        /// Returns null if no document GUIDs are found.
        /// </returns>
        /// <remarks>
        /// This method scans the executed endpoint results for document GUIDs and creates
        /// hyperlinks to the SPL XML generation endpoint. It supports:
        /// <list type="bullet">
        /// <item>Document GUIDs in path parameters (e.g., /api/Label/single/{documentGuid})</item>
        /// <item>Document GUIDs in JSON response objects (documentGuid, documentGUID, DocumentGUID fields)</item>
        /// <item>Document GUIDs in array results</item>
        /// </list>
        /// The generated links use minified XML format for faster loading.
        /// </remarks>
        /// <example>
        /// <code>
        /// var references = extractDocumentReferences(synthesisRequest);
        /// // Returns: { "View Full Label": "/api/Label/generate/abc123-def456/true" }
        /// </code>
        /// </example>
        /// <seealso cref="SynthesizeResultsAsync"/>
        /// <seealso cref="AiAgentSynthesis.DataReferences"/>
        /// <seealso cref="extractLabelDisplayName"/>
        private Dictionary<string, string>? extractDocumentReferences(AiSynthesisRequest request)
        {
            #region implementation

            var references = new Dictionary<string, string>();
            var guidPattern = new System.Text.RegularExpressions.Regex(
                @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
                System.Text.RegularExpressions.RegexOptions.Compiled);

            // Track seen GUIDs to avoid duplicates
            var seenGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // First pass: Build a metadata cache from Document list endpoints (contains title, documentDisplayName)
            var documentMetadataCache = buildDocumentMetadataCache(request.ExecutedEndpoints);

            foreach (var result in request.ExecutedEndpoints)
            {
                // Skip failed endpoints
                if (result.StatusCode < 200 || result.StatusCode >= 300)
                {
                    continue;
                }

                #region extract from path
                // Check if the path contains a document GUID
                var pathMatch = guidPattern.Match(result.Specification.Path);
                if (pathMatch.Success)
                {
                    var guid = pathMatch.Value;
                    if (!seenGuids.Contains(guid))
                    {
                        seenGuids.Add(guid);
                        // Use cached metadata first, then fall back to result extraction
                        var displayName = getDisplayNameFromCacheOrResult(
                            documentMetadataCache, guid, result.Result, seenGuids.Count);
                        references[$"View Full Label ({displayName})"] = $"/api/Label/generate/{guid}/true";
                    }
                }
                #endregion

                #region extract from result JSON (for Document list endpoints)
                // Also extract GUIDs from Document list results (step 1)
                if (result.Result != null && result.Specification.Path.Contains("/section/Document"))
                {
                    var resultJson = Newtonsoft.Json.JsonConvert.SerializeObject(result.Result);
                    var matches = guidPattern.Matches(resultJson);

                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        var guid = match.Value;
                        if (!seenGuids.Contains(guid))
                        {
                            // Check context to ensure it's a documentGuid field
                            var contextStart = Math.Max(0, match.Index - 50);
                            var contextLength = Math.Min(100, resultJson.Length - contextStart);
                            var context = resultJson.Substring(contextStart, contextLength).ToLower();

                            if (context.Contains("documentguid") || context.Contains("document_guid"))
                            {
                                seenGuids.Add(guid);
                                var displayName = getDisplayNameFromCacheOrResult(
                                    documentMetadataCache, guid, result.Result, seenGuids.Count);
                                references[$"View Full Label ({displayName})"] = $"/api/Label/generate/{guid}/true";
                            }
                        }
                    }
                }
                #endregion
            }

            #region extract from import context in original query
            // Also extract GUIDs from import context embedded in the original query
            // Format: [IMPORT COMPLETED SUCCESSFULLY: ... Document GUIDs: guid1, guid2, ...]
            if (!string.IsNullOrEmpty(request.OriginalQuery) &&
                (request.OriginalQuery.Contains("[IMPORT COMPLETED") || request.OriginalQuery.Contains("[IMPORT ISSUE")))
            {
                var matches = guidPattern.Matches(request.OriginalQuery);
                var importGuids = new List<string>();

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var guid = match.Value;
                    if (!seenGuids.Contains(guid))
                    {
                        seenGuids.Add(guid);
                        importGuids.Add(guid);
                        references[$"View Imported Label ({guid.Substring(0, 8)}...)"] = $"/api/Label/generate/{guid}/true";
                    }
                }

                // Add import progress link if we detected import context
                // The first GUID in the import context might be the operation ID
                if (importGuids.Count > 0)
                {
                    // Check if there's an operation ID pattern in the query (operationId: or similar context)
                    var operationIdMatch = System.Text.RegularExpressions.Regex.Match(
                        request.OriginalQuery,
                        @"operationId["":\s]+([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (operationIdMatch.Success)
                    {
                        var operationId = operationIdMatch.Groups[1].Value;
                        references["Check Import Progress"] = $"/api/Label/import/progress/{operationId}";
                    }
                }
            }
            #endregion

            return references.Count > 0 ? references : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a metadata cache mapping document GUIDs to their display names.
        /// Scans Document list endpoint results for title and documentDisplayName fields.
        /// </summary>
        /// <param name="endpoints">The executed endpoint results to scan.</param>
        /// <returns>Dictionary mapping lowercase GUIDs to display name strings.</returns>
        private Dictionary<string, string> buildDocumentMetadataCache(List<AiEndpointResult> endpoints)
        {
            #region implementation

            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Find Document list results (typically from /api/label/section/Document)
            foreach (var result in endpoints)
            {
                if (result.StatusCode < 200 || result.StatusCode >= 300 || result.Result == null)
                {
                    continue;
                }

                // Look for endpoints that return document lists
                if (!result.Specification.Path.Contains("/section/Document") &&
                    !result.Specification.Path.Contains("/document/search"))
                {
                    continue;
                }

                try
                {
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(result.Result);

                    // Use regex to find document entries with guid, title, and documentDisplayName
                    // Pattern matches JSON objects containing documentGUID
                    var docPattern = new System.Text.RegularExpressions.Regex(
                        @"\{[^{}]*""documentGUID""\s*:\s*""([^""]+)""[^{}]*\}",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    var matches = docPattern.Matches(json);
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (match.Groups.Count < 2) continue;

                        var guid = match.Groups[1].Value;
                        var docJson = match.Value;

                        // Extract title
                        var titleMatch = System.Text.RegularExpressions.Regex.Match(
                            docJson, @"""title""\s*:\s*""([^""]+)""",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        // Extract documentDisplayName
                        var displayNameMatch = System.Text.RegularExpressions.Regex.Match(
                            docJson, @"""documentDisplayName""\s*:\s*""([^""]+)""",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        string displayName = "";
                        if (titleMatch.Success)
                        {
                            var title = titleMatch.Groups[1].Value.Trim();
                            // Clean HTML tags and FDA boilerplate from title
                            title = cleanDocumentTitle(title);
                            // Truncate long titles
                            if (title.Length > 40)
                            {
                                title = title.Substring(0, 37) + "...";
                            }
                            displayName = title;
                        }

                        if (displayNameMatch.Success)
                        {
                            var labelType = formatLabelType(displayNameMatch.Groups[1].Value);
                            if (!string.IsNullOrEmpty(displayName) && !string.IsNullOrEmpty(labelType))
                            {
                                displayName = $"{displayName} ({labelType})";
                            }
                            else if (string.IsNullOrEmpty(displayName))
                            {
                                displayName = labelType;
                            }
                        }

                        if (!string.IsNullOrEmpty(displayName) && !cache.ContainsKey(guid))
                        {
                            cache[guid] = displayName;
                        }
                    }
                }
                catch
                {
                    // Ignore parsing errors
                }
            }

            return cache;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Cleans a document title by removing HTML tags and FDA boilerplate prefixes.
        /// </summary>
        /// <param name="title">The raw document title from the database.</param>
        /// <returns>A cleaned title suitable for display.</returns>
        /// <remarks>
        /// This method handles:
        /// <list type="bullet">
        /// <item>HTML tags using TextUtil.RemoveTags()</item>
        /// <item>FDA boilerplate like "These highlights do not include all the information needed to use"</item>
        /// <item>Other common prefixes that obscure the actual drug name</item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// var cleaned = cleanDocumentTitle("These highlights do not include all the information needed to use ASPIRIN safely.");
        /// // Returns: "ASPIRIN safely."
        /// </code>
        /// </example>
        private string cleanDocumentTitle(string title)
        {
            #region implementation

            if (string.IsNullOrEmpty(title))
            {
                return title;
            }

            // Remove HTML tags first
            title = title.RemoveTags();

            // Common FDA boilerplate prefixes to strip
            // These appear at the start of many drug label titles
            var boilerplatePrefixes = new[]
            {
                "These highlights do not include all the information needed to use",
                "HIGHLIGHTS OF PRESCRIBING INFORMATION These highlights do not include all the information needed to use",
                "HIGHLIGHTS OF PRESCRIBING INFORMATION",
                "Full prescribing information for"
            };

            foreach (var prefix in boilerplatePrefixes)
            {
                if (title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    title = title.Substring(prefix.Length).Trim();
                    // Remove leading punctuation after stripping prefix
                    title = title.TrimStart('.', ',', ':', ';', '-', ' ');
                    break;
                }
            }

            // Also handle cases where boilerplate appears mid-string
            // Look for drug name pattern after boilerplate (ALL CAPS word followed by other text)
            var drugNamePattern = new System.Text.RegularExpressions.Regex(
                @"^.*?(?:needed to use|information needed to use)\s+([A-Z][A-Z0-9\s\-]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var drugMatch = drugNamePattern.Match(title);
            if (drugMatch.Success && drugMatch.Groups.Count > 1)
            {
                // Extract just the drug name portion
                var drugNameStart = title.IndexOf(drugMatch.Groups[1].Value);
                if (drugNameStart >= 0)
                {
                    title = title.Substring(drugNameStart).Trim();
                }
            }

            // Clean up any remaining issues
            title = title.Trim();

            // If the title starts with a lowercase word after cleaning, capitalize it
            if (title.Length > 0 && char.IsLower(title[0]))
            {
                title = char.ToUpper(title[0]) + title.Substring(1);
            }

            return title;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets display name from cache or extracts from result as fallback.
        /// </summary>
        private string getDisplayNameFromCacheOrResult(
            Dictionary<string, string> cache,
            string guid,
            object? result,
            int index)
        {
            #region implementation

            // Try cache first
            if (cache.TryGetValue(guid, out var cachedName))
            {
                return cachedName;
            }

            // Fall back to result extraction
            return extractLabelDisplayName(result, guid, index);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts a product name and label type from an API result for display in data references.
        /// </summary>
        /// <param name="result">The API result object to search.</param>
        /// <param name="documentGuid">The document GUID to search for specific record data.</param>
        /// <param name="index">Fallback index number if no name is found.</param>
        /// <returns>A display-friendly label description combining product name and label type.</returns>
        /// <remarks>
        /// Searches for drug/product names and document types to build descriptive labels like:
        /// "ASPIRIN (Human OTC Drug)" or "LISINOPRIL TABLETS (Prescription Drug)"
        /// Falls back to generic numbered documents if specific names aren't found.
        /// </remarks>
        /// <seealso cref="extractDocumentReferences"/>
        private string extractLabelDisplayName(object? result, string documentGuid, int index)
        {
            #region implementation

            if (result == null)
            {
                return $"Document {index}";
            }

            try
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(result);

                string? productName = null;
                string? labelType = null;
                string? documentTitle = null;

                #region extract product name
                // Product name patterns in order of preference
                var productPatterns = new[]
                {
                    "\"productName\"\\s*:\\s*\"([^\"]+)\"",
                    "\"ProductName\"\\s*:\\s*\"([^\"]+)\"",
                    "\"product_name\"\\s*:\\s*\"([^\"]+)\""
                };

                foreach (var pattern in productPatterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(json, pattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        productName = match.Groups[1].Value.Trim();
                        break;
                    }
                }
                #endregion

                #region extract document title (fallback for product name)
                if (string.IsNullOrEmpty(productName))
                {
                    var titlePatterns = new[]
                    {
                        "\"title\"\\s*:\\s*\"([^\"]+)\"",
                        "\"Title\"\\s*:\\s*\"([^\"]+)\""
                    };

                    foreach (var pattern in titlePatterns)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(json, pattern,
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (match.Success && match.Groups.Count > 1)
                        {
                            // Clean the title of HTML and FDA boilerplate
                            documentTitle = cleanDocumentTitle(match.Groups[1].Value.Trim());
                            break;
                        }
                    }
                }
                #endregion

                #region extract label type from documentDisplayName
                // Document display name indicates the label type (e.g., "HUMAN PRESCRIPTION DRUG LABEL")
                var labelTypePatterns = new[]
                {
                    "\"documentDisplayName\"\\s*:\\s*\"([^\"]+)\"",
                    "\"DocumentDisplayName\"\\s*:\\s*\"([^\"]+)\""
                };

                foreach (var pattern in labelTypePatterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(json, pattern,
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        labelType = formatLabelType(match.Groups[1].Value);
                        break;
                    }
                }
                #endregion

                #region build display name
                // Build the display name combining product and type
                var displayParts = new List<string>();

                // Prefer product name, then document title
                var primaryName = productName ?? documentTitle;
                if (!string.IsNullOrEmpty(primaryName))
                {
                    // Truncate long names for display
                    if (primaryName.Length > 40)
                    {
                        primaryName = primaryName.Substring(0, 37) + "...";
                    }
                    displayParts.Add(primaryName);
                }

                // Add label type if available
                if (!string.IsNullOrEmpty(labelType))
                {
                    if (displayParts.Count > 0)
                    {
                        return $"{displayParts[0]} ({labelType})";
                    }
                    return labelType;
                }

                if (displayParts.Count > 0)
                {
                    return displayParts[0];
                }
                #endregion
            }
            catch
            {
                // Ignore serialization errors, use fallback
            }

            return $"Document {index}";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Serializes a result object to a string format optimized for LLM token efficiency.
        /// Uses pipe-delimited format for collections (reduces token count significantly),
        /// falls back to JSON for non-collection types or when pipe serialization fails.
        /// </summary>
        /// <param name="result">The result object to serialize (typically from API endpoint response).</param>
        /// <param name="usePipeFormat">Whether to attempt pipe-delimited format for collections (default: true).</param>
        /// <returns>
        /// A string representation of the result, either in pipe-delimited or JSON format.
        /// Pipe format includes a KEY header with property name mappings.
        /// </returns>
        /// <remarks>
        /// The pipe-delimited format significantly reduces token usage when sending large datasets
        /// to Claude API. For example, a list of 100 products might use:
        /// - JSON format: ~15,000 tokens
        /// - Pipe format: ~3,000 tokens (80% reduction)
        ///
        /// The format includes a key header that maps abbreviated column names to full property names,
        /// allowing Claude to understand the data structure while minimizing token count.
        ///
        /// Example output:
        /// <code>
        /// [KEY:PN=ProductName|DC=DocumentCount|LN=LabelerName]
        /// PN|DC|LN
        /// LIPITOR|5|Pfizer Inc
        /// VIAGRA|3|Pfizer Inc
        /// </code>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Collection - uses pipe format
        /// var products = new List&lt;Product&gt; { ... };
        /// var serialized = serializeResultForPrompt(products);
        /// // Returns pipe-delimited format
        ///
        /// // Single object - uses JSON format
        /// var product = new Product { ... };
        /// var serialized = serializeResultForPrompt(product, usePipeFormat: false);
        /// // Returns JSON format
        /// </code>
        /// </example>
        /// <seealso cref="TextUtil.ToPipe{T}(T, bool)"/>
        /// <seealso cref="JsonPipeHelper.TryConvertToPipe(object?)"/>
        /// <seealso cref="buildSynthesisPrompt"/>
        private string serializeResultForPrompt(object? result, bool usePipeFormat = true)
        {
            #region implementation

            if (result == null)
            {
                return "null";
            }

            // Attempt pipe-delimited format for collections when enabled
            if (usePipeFormat)
            {
                try
                {
                    // FIRST: Try JSON-specific conversion for JArray/JObject/JsonElement
                    // This handles cases where ToPipe() fails due to reflection limitations on JSON wrapper types
                    var jsonPipeResult = JsonPipeHelper.TryConvertToPipe(result);
                    if (!string.IsNullOrEmpty(jsonPipeResult))
                    {
                        _logger.LogDebug("Using JSON-to-pipe conversion for result serialization (token optimization)");
                        return jsonPipeResult;
                    }

                    // SECOND: Try ToPipe for strongly-typed collections
                    // Check if the result is a collection (array, list, etc.)
                    bool isCollection = result is System.Collections.IEnumerable && !(result is string);

                    if (isCollection)
                    {
                        // Use ToPipe extension method for token-efficient serialization
                        var pipeResult = result.ToPipe();

                        if (!string.IsNullOrEmpty(pipeResult))
                        {
                            _logger.LogDebug("Using pipe-delimited format for result serialization (token optimization)");
                            return pipeResult;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail - fall through to JSON serialization
                    _logger.LogDebug(ex, "Pipe serialization failed, falling back to JSON format");
                }
            }

            // THIRD: Fallback to JSON serialization for single objects or when pipe format fails
            return Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats a document display name into a shorter, user-friendly label type.
        /// </summary>
        /// <param name="displayName">The full document display name (e.g., "HUMAN PRESCRIPTION DRUG LABEL").</param>
        /// <returns>A shortened, formatted label type (e.g., "Prescription Drug").</returns>
        /// <remarks>
        /// Converts verbose FDA document type names to concise display labels:
        /// <list type="bullet">
        /// <item>"HUMAN PRESCRIPTION DRUG LABEL" → "Prescription Drug"</item>
        /// <item>"HUMAN OTC DRUG LABEL" → "OTC Drug"</item>
        /// <item>"INDEXING - PHARMACOLOGIC CLASS" → "Indexing File"</item>
        /// </list>
        /// </remarks>
        /// <seealso cref="extractLabelDisplayName"/>
        private string formatLabelType(string displayName)
        {
            #region implementation

            if (string.IsNullOrEmpty(displayName))
            {
                return string.Empty;
            }

            // Normalize to uppercase for matching
            var upper = displayName.ToUpperInvariant();

            // Map common FDA document types to friendly names
            if (upper.Contains("PRESCRIPTION DRUG"))
            {
                return "Prescription Drug";
            }
            if (upper.Contains("OTC DRUG"))
            {
                return "OTC Drug";
            }
            if (upper.Contains("HOMEOPATHIC"))
            {
                return "Homeopathic";
            }
            if (upper.Contains("VACCINE"))
            {
                return "Vaccine Label";
            }
            if (upper.Contains("PLASMA"))
            {
                return "Plasma Derivative";
            }
            if (upper.Contains("CELLULAR THERAPY"))
            {
                return "Cellular Therapy";
            }
            if (upper.Contains("INDEXING"))
            {
                return "Indexing File";
            }
            if (upper.Contains("ANIMAL DRUG"))
            {
                return "Animal Drug";
            }
            if (upper.Contains("MEDICAL DEVICE"))
            {
                return "Medical Device";
            }
            if (upper.Contains("DIETARY SUPPLEMENT"))
            {
                return "Dietary Supplement";
            }
            if (upper.Contains("COSMETIC"))
            {
                return "Cosmetic";
            }
            if (upper.Contains("BULK INGREDIENT"))
            {
                return "Bulk Ingredient";
            }

            // Default: clean up the raw name
            // Remove "HUMAN " prefix, " LABEL" suffix, convert to title case
            var cleaned = displayName
                .Replace("HUMAN ", "")
                .Replace(" LABEL", "")
                .Trim();

            if (cleaned.Length > 25)
            {
                cleaned = cleaned.Substring(0, 22) + "...";
            }

            // Convert to title case
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleaned.ToLower());

            #endregion
        }

        #endregion

        #region retry interpretation methods

        /**************************************************************/
        /// <inheritdoc/>
        /// <summary>
        /// Implements recursive retry logic for failed API endpoint calls.
        /// Analyzes failure reasons and suggests alternative endpoints from the skills document.
        /// </summary>
        public async Task<AiAgentInterpretation> RetryInterpretationAsync(
            AiAgentRequest originalRequest,
            List<AiEndpointResult> failedResults,
            int attemptNumber)
        {
            #region input validation

            if (originalRequest == null)
            {
                throw new ArgumentNullException(nameof(originalRequest));
            }

            if (failedResults == null || !failedResults.Any())
            {
                throw new ArgumentException("Failed results are required for retry interpretation.", nameof(failedResults));
            }

            // Maximum 3 retry attempts
            const int maxAttempts = 3;
            if (attemptNumber > maxAttempts)
            {
                _logger.LogWarning("Max retry attempts ({MaxAttempts}) reached for query: {Query}",
                    maxAttempts, originalRequest.UserMessage);

                return new AiAgentInterpretation
                {
                    Success = true,
                    IsDirectResponse = true,
                    DirectResponse = buildMaxRetryResponse(originalRequest.UserMessage, failedResults),
                    Explanation = $"Unable to retrieve data after {maxAttempts} attempts."
                };
            }

            #endregion

            #region implementation

            _logger.LogInformation("Retry attempt {AttemptNumber} for query: {QueryPreview}",
                attemptNumber,
                originalRequest.UserMessage.Length > 100
                    ? originalRequest.UserMessage[..100] + "..."
                    : originalRequest.UserMessage);

            try
            {
                // Build the retry prompt
                var prompt = await buildRetryPromptAsync(originalRequest, failedResults, attemptNumber);

                // Call Claude API
                var claudeResponse = await GenerateDocumentComparisonAsync(prompt);

                // Parse response into interpretation
                var interpretation = parseInterpretationResponse(claudeResponse, originalRequest.SystemContext);

                // Mark this as a retry attempt
                interpretation.RetryAttempt = attemptNumber;

                _logger.LogInformation("Retry interpretation successful. Attempt: {Attempt}, New endpoints: {EndpointCount}",
                    attemptNumber, interpretation.Endpoints?.Count ?? 0);

                return interpretation;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Claude API error during retry interpretation attempt {Attempt}", attemptNumber);

                return new AiAgentInterpretation
                {
                    Success = false,
                    Error = "Unable to process retry request due to API communication error.",
                    RetryAttempt = attemptNumber
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during retry interpretation attempt {Attempt}", attemptNumber);

                return new AiAgentInterpretation
                {
                    Success = false,
                    Error = "An unexpected error occurred while processing the retry request.",
                    RetryAttempt = attemptNumber
                };
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the prompt for Claude to suggest alternative endpoints after failures.
        /// </summary>
        /// <param name="originalRequest">The original user request.</param>
        /// <param name="failedResults">The endpoints that failed.</param>
        /// <param name="attemptNumber">Current retry attempt number.</param>
        /// <returns>Formatted prompt string for Claude.</returns>
        /// <seealso cref="buildRetryPromptSkills"/>
        /// <seealso cref="IClaudeSkillService.GetFullSkillsDocumentAsync"/>
        private async Task<string> buildRetryPromptAsync(
            AiAgentRequest originalRequest,
            List<AiEndpointResult> failedResults,
            int attemptNumber)
        {
            #region implementation

            var sb = new StringBuilder();

            // Load retry prompt skills from file (includes system role, fallback rules, and output format)
            sb.AppendLine(buildRetryPromptSkills());
            sb.AppendLine();

            // Include skills document for reference (use async result synchronously in retry context)
            sb.AppendLine("=== AVAILABLE API ENDPOINTS (SKILLS DOCUMENT) ===");
            sb.AppendLine(await _skillService.GetFullSkillsDocumentAsync());
            sb.AppendLine();

            // Original request
            sb.AppendLine("=== ORIGINAL USER QUERY ===");
            sb.AppendLine(originalRequest.UserMessage);
            sb.AppendLine();

            // What failed
            sb.AppendLine("=== FAILED ENDPOINTS (DO NOT SUGGEST THESE AGAIN) ===");
            foreach (var result in failedResults)
            {
                sb.AppendLine($"- {result.Specification.Method} {result.Specification.Path}");
                sb.AppendLine($"  Status: {result.StatusCode}");
                if (!string.IsNullOrEmpty(result.Error))
                {
                    sb.AppendLine($"  Error: {result.Error}");
                }
            }
            sb.AppendLine();

            // Retry attempt context
            sb.AppendLine("=== RETRY CONTEXT ===");
            sb.AppendLine($"This is retry attempt {attemptNumber} of 3.");

            return sb.ToString();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a response message when maximum retry attempts are exceeded.
        /// </summary>
        /// <param name="originalQuery">The user's original query.</param>
        /// <param name="failedResults">All endpoints that were attempted.</param>
        /// <returns>Formatted response explaining the failure.</returns>
        private string buildMaxRetryResponse(string originalQuery, List<AiEndpointResult> failedResults)
        {
            #region implementation

            var sb = new StringBuilder();

            sb.AppendLine("I apologize, but I was unable to retrieve the requested data after multiple attempts.");
            sb.AppendLine();
            sb.AppendLine("**What I tried:**");

            foreach (var result in failedResults)
            {
                sb.AppendLine($"- `{result.Specification.Method} {result.Specification.Path}` → Status {result.StatusCode}");
            }

            sb.AppendLine();
            sb.AppendLine("**Possible reasons:**");
            sb.AppendLine("- The requested data may not exist in the database");
            sb.AppendLine("- The database may be empty (try importing SPL data first)");
            sb.AppendLine("- There may be a system configuration issue");
            sb.AppendLine();
            sb.AppendLine("**Suggested next steps:**");
            sb.AppendLine("- Try asking for available tables: \"What tables are in the system?\"");
            sb.AppendLine("- Check if data exists: \"How many documents are available?\"");
            sb.AppendLine("- Import data if needed: \"How do I import SPL files?\"");

            return sb.ToString();

            #endregion
        }

        #endregion
    }

    public class ClaudeApiResponse
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("role")]
        public string? Role { get; set; }

        [JsonProperty("model")]
        public string? Model { get; set; }

        [JsonProperty("content")]
        public ClaudeContent[]? Content { get; set; }

        [JsonProperty("stop_reason")]
        public string? StopReason { get; set; }

        [JsonProperty("stop_sequence")]
        public string? StopSequence { get; set; }

        [JsonProperty("usage")]
        public ClaudeUsage? Usage { get; set; }
    }

    public class ClaudeContent
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("text")]
        public string? Text { get; set; }
    }

    public class ClaudeUsage
    {
        [JsonProperty("input_tokens")]
        public int InputTokens { get; set; }

        [JsonProperty("cache_creation_input_tokens")]
        public int CacheCreationInputTokens { get; set; }

        [JsonProperty("cache_read_input_tokens")]
        public int CacheReadInputTokens { get; set; }

        [JsonProperty("output_tokens")]
        public int OutputTokens { get; set; }

        [JsonProperty("service_tier")]
        public string? ServiceTier { get; set; }
    }

    #endregion
}