
using MedRecPro.Data;
using MedRecPro.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        /// //     Path: "/api/views/labeler/search",
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
        ///         Specification: { Method: "GET", Path: "/api/views/labeler/search", ... },
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
        public ClaudeApiService(
            HttpClient httpClient,
            ILogger<ClaudeApiService> logger,
            IOptions<ClaudeApiSettings> settings,
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            ConversationStore conversationStore)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = settings.Value;
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
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

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Claude API error: {response.StatusCode} - {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var claudeResponse = JsonSerializer.Deserialize<ClaudeApiResponse>(responseContent);

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
                var isDemoMode = _configuration.GetValue<bool>("DemoMode:Enabled", false);
                var demoResetMinutes = _configuration.GetValue<int>("DemoMode:ResetIntervalMinutes", 1440);
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

            // Check cache validity
            if (_skillsDocumentCache != null &&
                DateTime.UtcNow - _skillsCacheTimestamp < _skillsCacheDuration)
            {
                return _skillsDocumentCache;
            }

            _logger.LogDebug("Refreshing skills document cache");

            // Build the skills document
            _skillsDocumentCache = buildSkillsDocument();
            _skillsCacheTimestamp = DateTime.UtcNow;

            return await Task.FromResult(_skillsDocumentCache);

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
        /// </summary>
        /// <param name="request">The user's request.</param>
        /// <returns>The complete prompt string.</returns>
        private async Task<string> buildInterpretationPromptAsync(AiAgentRequest request)
        {
            #region implementation

            var skills = await GetSkillsDocumentAsync();
            var sb = new StringBuilder();

            // System instructions
            sb.AppendLine("You are an AI assistant for MedRecPro, a pharmaceutical labeling management system.");
            sb.AppendLine("Your task is to interpret user requests and return structured API endpoint specifications.");
            sb.AppendLine();

            // Include skills document
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
                    sb.AppendLine("Suggest using POST /api/labels/import with ZIP files from DailyMed.");
                }
                else
                {
                    sb.AppendLine($"Documents Available: {request.SystemContext.DocumentCount}");
                    sb.AppendLine($"Products Available: {request.SystemContext.ProductCount}");
                }

                sb.AppendLine($"Import Enabled: {request.SystemContext.ImportEnabled}");
                sb.AppendLine();
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
            sb.AppendLine("Respond with a JSON object in the following format:");
            sb.AppendLine(@"{
              ""success"": true/false,
              ""endpoints"": [
                {
                  ""method"": ""GET/POST/PUT/DELETE"",
                  ""path"": ""/api/..."",
                  ""queryParameters"": { ""param1"": ""value1"" },
                  ""body"": null,
                  ""description"": ""What this call does"",
                  ""expectedResponseType"": ""array/object"",
                  ""executionOrder"": 0
                }
              ],
              ""explanation"": ""Brief explanation of the interpretation"",
              ""requiresAuthentication"": true/false,
              ""clarifyingQuestions"": [""Question if request is ambiguous""],
              ""isDirectResponse"": false,
              ""directResponse"": null
            }");
            sb.AppendLine();
            sb.AppendLine("If the user is asking a general question that doesn't require API calls,");
            sb.AppendLine("set isDirectResponse=true and provide the answer in directResponse.");
            sb.AppendLine();

            // User request
            sb.AppendLine("=== USER REQUEST ===");
            sb.AppendLine(request.UserMessage);

            return sb.ToString();

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

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var interpretation = JsonSerializer.Deserialize<AiAgentInterpretation>(jsonContent, options);

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
            catch (JsonException ex)
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
        /// </summary>
        /// <param name="request">The synthesis request.</param>
        /// <returns>The complete prompt string.</returns>
        private string buildSynthesisPrompt(AiSynthesisRequest request)
        {
            #region implementation

            var sb = new StringBuilder();

            sb.AppendLine("You are an AI assistant for MedRecPro, a pharmaceutical labeling management system.");
            sb.AppendLine("Your task is to synthesize API results into a helpful, conversational response.");
            sb.AppendLine();

            // Original query
            sb.AppendLine("=== ORIGINAL USER QUERY ===");
            sb.AppendLine(request.OriginalQuery);
            sb.AppendLine();

            // API results
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
                    var resultJson = JsonSerializer.Serialize(result.Result, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        MaxDepth = 5
                    });

                    // Truncate large results
                    if (resultJson.Length > 10000)
                    {
                        resultJson = resultJson[..10000] + "\n... (truncated)";
                    }

                    sb.AppendLine($"Result: {resultJson}");
                }

                sb.AppendLine();
            }

            // Output format
            sb.AppendLine("=== OUTPUT FORMAT ===");
            sb.AppendLine("Respond with a JSON object in the following format:");
            sb.AppendLine(@"{
              ""response"": ""Natural language response addressing the user's query"",
              ""dataHighlights"": { ""key"": ""value"" },
              ""suggestedFollowUps"": [""Suggested next query""],
              ""warnings"": [""Any warnings or limitations""],
              ""isComplete"": true/false
            }");

            return sb.ToString();

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

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var synthesis = JsonSerializer.Deserialize<AiAgentSynthesis>(jsonContent, options);

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
            catch (JsonException)
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
        /// Builds the comprehensive skills document describing all available API endpoints.
        /// </summary>
        /// <returns>The skills document as a formatted string.</returns>
        private string buildSkillsDocument()
        {
            #region implementation

            var sb = new StringBuilder();

            sb.AppendLine("# MedRecPro API Skills Document");
            sb.AppendLine();
            sb.AppendLine("This document describes the available API endpoints for querying and managing");
            sb.AppendLine("SPL (Structured Product Labeling) pharmaceutical data in MedRecPro.");
            sb.AppendLine();

            // Navigation Views
            sb.AppendLine("## Navigation Views (Search & Discovery)");
            sb.AppendLine();

            sb.AppendLine("### Application Number Search");
            sb.AppendLine("Search products by FDA application number (NDA, ANDA, BLA).");
            sb.AppendLine("- `GET /api/views/application-number/search?applicationNumber={value}&pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - Parameters: applicationNumber (required), pageNumber, pageSize");
            sb.AppendLine("  - Example: Find all products under NDA020702");
            sb.AppendLine("- `GET /api/views/application-number/summaries?marketingCategory={code}&pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - Get aggregated summaries with product/document counts");
            sb.AppendLine();

            sb.AppendLine("### Pharmacologic Class Search");
            sb.AppendLine("Search by therapeutic/pharmacologic class.");
            sb.AppendLine("- `GET /api/views/pharmacologic-class/search?classNameSearch={value}&pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - Example: Find all beta blockers, ACE inhibitors");
            sb.AppendLine("- `GET /api/views/pharmacologic-class/summaries?pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - Get class summaries with product counts");
            sb.AppendLine();

            sb.AppendLine("### Ingredient Search");
            sb.AppendLine("Search products by active ingredient (UNII or substance name).");
            sb.AppendLine("- `GET /api/views/ingredient/search?unii={code}&substanceNameSearch={name}&pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - At least one of unii or substanceNameSearch required");
            sb.AppendLine("  - Example: Find products containing aspirin, acetaminophen");
            sb.AppendLine("- `GET /api/views/ingredient/summaries?minProductCount={n}&pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - Get ingredient summaries ranked by frequency");
            sb.AppendLine();

            sb.AppendLine("### NDC (National Drug Code) Search");
            sb.AppendLine("Search by product or package NDC code.");
            sb.AppendLine("- `GET /api/views/ndc/search?productCode={ndc}&pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - Example: 12345-678-90");
            sb.AppendLine("- `GET /api/views/ndc/package/search?packageCode={ndc}&pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - Search package configurations by NDC");
            sb.AppendLine();

            sb.AppendLine("### Labeler (Manufacturer) Search");
            sb.AppendLine("Search products by marketing organization/labeler name.");
            sb.AppendLine("- `GET /api/views/labeler/search?labelerNameSearch={name}&pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - Example: Find all Pfizer products");
            sb.AppendLine("- `GET /api/views/labeler/summaries?pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - Get labeler summaries with product counts");
            sb.AppendLine();

            sb.AppendLine("### Document Navigation");
            sb.AppendLine("Navigate SPL documents and version history.");
            sb.AppendLine("- `GET /api/views/document/search?productNameSearch={name}&pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - Search documents by product name");
            sb.AppendLine("- `GET /api/views/document/version-history/{setGuidOrDocumentGuid}`");
            sb.AppendLine("  - Get version history for a document set");
            sb.AppendLine();

            sb.AppendLine("### Section Navigation");
            sb.AppendLine("Search labeling sections by LOINC code.");
            sb.AppendLine("- `GET /api/views/section/search?sectionCode={loinc}&pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - Common codes: 34066-1 (Boxed Warning), 34067-9 (Indications), 34068-7 (Dosage)");
            sb.AppendLine("- `GET /api/views/section/summaries?pageNumber={n}&pageSize={n}`");
            sb.AppendLine("  - Get section type frequency statistics");
            sb.AppendLine();

            // Label CRUD Operations
            sb.AppendLine("## Label Data Operations (CRUD)");
            sb.AppendLine();
            sb.AppendLine("Dynamic CRUD operations for label sections. menuSelection = entity name.");
            sb.AppendLine();

            sb.AppendLine("### Discovery");
            sb.AppendLine("- `GET /api/labels/sectionMenu` - List all available sections");
            sb.AppendLine("- `GET /api/labels/{menuSelection}/documentation` - Get schema for a section");
            sb.AppendLine();

            sb.AppendLine("### Read Operations");
            sb.AppendLine("- `GET /api/labels/section/{menuSelection}?pageNumber={n}&pageSize={n}` - Get all records for section");
            sb.AppendLine("- `GET /api/labels/{menuSelection}/{encryptedId}` - Get single record by encrypted ID");
            sb.AppendLine("- `GET /api/labels/single/{documentGuid}` - Get complete document by GUID");
            sb.AppendLine("- `GET /api/labels/complete/{pageNumber}/{pageSize}` - Get all complete documents");
            sb.AppendLine();

            sb.AppendLine("### Write Operations (Requires Authentication)");
            sb.AppendLine("- `POST /api/labels/{menuSelection}` - Create new record (body: JSON object)");
            sb.AppendLine("- `PUT /api/labels/{menuSelection}/{encryptedId}` - Update record (body: JSON object)");
            sb.AppendLine("- `DELETE /api/labels/{menuSelection}/{encryptedId}` - Delete record");
            sb.AppendLine();

            sb.AppendLine("### Available Sections (menuSelection values):");
            sb.AppendLine("Document, Organization, Product, ActiveMoiety, ActiveIngredient, InactiveIngredient,");
            sb.AppendLine("Section, Subsection, PackagingLevel, PackageItem, ProductIdentifier, PackageIdentifier,");
            sb.AppendLine("Characteristic, MarketingCategory, Route, EquivalentSubstance, PharmacologicClass,");
            sb.AppendLine("DrugInteraction, ContraindicatedDrug, ItemContains, ContainedItem, Address, BusinessOperation");
            sb.AppendLine();

            // Import/Export
            sb.AppendLine("## Import/Export Operations");
            sb.AppendLine();
            sb.AppendLine("### SPL Import (Requires Authentication)");
            sb.AppendLine("- `POST /api/labels/import` - Import SPL data from ZIP file(s)");
            sb.AppendLine("  - Body: multipart/form-data with 'files' containing ZIP files");
            sb.AppendLine("  - Returns: operationId for progress tracking");
            sb.AppendLine("- `GET /api/labels/import/progress/{operationId}` - Check import progress");
            sb.AppendLine("  - Note: ZIP files containing SPL XML can be obtained from DailyMed");
            sb.AppendLine("  - URL: https://dailymed.nlm.nih.gov/dailymed/spl-resources-all-drug-labels.cfm");
            sb.AppendLine();

            sb.AppendLine("### SPL Export");
            sb.AppendLine("- `GET /api/labels/generate/{documentGuid}/{minify}` - Generate SPL XML from document");
            sb.AppendLine("  - minify: true/false for compact output");
            sb.AppendLine();

            // Comparison Analysis
            sb.AppendLine("## AI Comparison Analysis");
            sb.AppendLine();
            sb.AppendLine("Compare original SPL XML with database representation.");
            sb.AppendLine("- `POST /api/labels/comparison/analysis/{documentGuid}` - Queue analysis");
            sb.AppendLine("- `GET /api/labels/comparison/analysis/{documentGuid}` - Get cached results");
            sb.AppendLine("- `GET /api/labels/comparison/progress/{operationId}` - Check analysis progress");
            sb.AppendLine();

            // Authentication
            sb.AppendLine("## Authentication");
            sb.AppendLine();
            sb.AppendLine("- `GET /api/auth/login/{provider}` - Initiate OAuth login (Google, Microsoft)");
            sb.AppendLine("- `GET /api/auth/user` - Get current user info");
            sb.AppendLine("- `POST /api/auth/logout` - Log out");
            sb.AppendLine();

            // User Management
            sb.AppendLine("## User Management");
            sb.AppendLine();
            sb.AppendLine("- `GET /api/users/me` - Get current user profile");
            sb.AppendLine("- `GET /api/users/{encryptedUserId}` - Get user by ID");
            sb.AppendLine("- `GET /api/users/user/{encryptedUserId}/activity` - Get user activity log");
            sb.AppendLine();

            // Notes
            sb.AppendLine("## Important Notes");
            sb.AppendLine();
            sb.AppendLine("1. All IDs are encrypted - use the encrypted ID values returned by the API");
            sb.AppendLine("2. Pagination: pageNumber is 1-based, default pageSize is 10");
            sb.AppendLine("3. Write operations (POST, PUT, DELETE) require authentication");
            sb.AppendLine("4. Demo mode: Database may be periodically reset");
            sb.AppendLine("5. Empty database: Suggest importing SPL ZIP files from DailyMed");

            return sb.ToString();

            #endregion
        }

        #endregion
    }

    public class ClaudeApiResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("content")]
        public ClaudeContent[]? Content { get; set; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }

        [JsonPropertyName("stop_sequence")]
        public string? StopSequence { get; set; }

        [JsonPropertyName("usage")]
        public ClaudeUsage? Usage { get; set; }
    }

    public class ClaudeContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    public class ClaudeUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("cache_creation_input_tokens")]
        public int CacheCreationInputTokens { get; set; }

        [JsonPropertyName("cache_read_input_tokens")]
        public int CacheReadInputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }

        [JsonPropertyName("service_tier")]
        public string? ServiceTier { get; set; }
    } 

    #endregion
}