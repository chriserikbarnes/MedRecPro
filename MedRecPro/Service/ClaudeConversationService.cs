using MedRecPro.Models;

namespace MedRecPro.Service
{
    #region interface

    /**************************************************************/
    /// <summary>
    /// Service interface for managing AI conversation sessions.
    /// Provides methods for creating, retrieving, updating, and deleting
    /// conversations and their message histories.
    /// </summary>
    /// <remarks>
    /// This service encapsulates conversation management functionality
    /// that was previously part of <see cref="IClaudeApiService"/>, providing
    /// better separation of concerns between AI API operations and
    /// conversation state management.
    ///
    /// The implementation delegates to <see cref="ConversationStore"/> for
    /// thread-safe, in-memory storage with automatic expiration.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Creating a new conversation
    /// var conversation = await _conversationService.CreateConversationAsync(userId);
    ///
    /// // Retrieving conversation history
    /// var messages = await _conversationService.GetConversationHistoryAsync(
    ///     conversationId,
    ///     maxMessages: 10);
    ///
    /// // Getting conversation statistics
    /// var stats = await _conversationService.GetConversationStatsAsync();
    /// </code>
    /// </example>
    /// <seealso cref="ClaudeConversationService"/>
    /// <seealso cref="ConversationStore"/>
    /// <seealso cref="Conversation"/>
    /// <seealso cref="AiConversationMessage"/>
    public interface IClaudeConversationService
    {
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
        /// conversation ID should be included in subsequent requests to maintain context.
        ///
        /// Conversations expire after 1 hour of inactivity. Each message or interaction
        /// resets the expiration timer.
        /// </remarks>
        /// <example>
        /// <code>
        /// var conversation = await _conversationService.CreateConversationAsync(encryptedUserId);
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
        /// <remarks>
        /// This operation is permanent and cannot be undone. The conversation
        /// and all its messages will be immediately removed from the store.
        /// </remarks>
        Task<bool> DeleteConversationAsync(string conversationId);

        /**************************************************************/
        /// <summary>
        /// Gets statistics about the conversation store.
        /// </summary>
        /// <returns>Statistics including conversation counts and message totals.</returns>
        /// <remarks>
        /// Returns metrics about the conversation store for monitoring and debugging,
        /// including active conversation counts, expired conversation counts, and
        /// total message counts.
        /// </remarks>
        /// <seealso cref="ConversationStoreStats"/>
        Task<ConversationStoreStats> GetConversationStatsAsync();
    }

    #endregion

    #region implementation

    /**************************************************************/
    /// <summary>
    /// Implementation of <see cref="IClaudeConversationService"/> that manages
    /// AI conversation sessions using an in-memory <see cref="ConversationStore"/>.
    /// </summary>
    /// <remarks>
    /// This service provides a focused interface for conversation management,
    /// extracted from the larger <see cref="ClaudeApiService"/> to maintain
    /// separation of concerns. It delegates all storage operations to the
    /// singleton <see cref="ConversationStore"/>.
    ///
    /// Key features:
    /// <list type="bullet">
    /// <item>Thread-safe operations via <see cref="ConversationStore"/></item>
    /// <item>Automatic conversation expiration (1 hour sliding window)</item>
    /// <item>Logging of all conversation operations</item>
    /// <item>Server-generated conversation IDs</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Dependency injection registration (Program.cs)
    /// builder.Services.AddScoped&lt;IClaudeConversationService, ClaudeConversationService&gt;();
    ///
    /// // Usage in controller
    /// public class AiController : ControllerBase
    /// {
    ///     private readonly IClaudeConversationService _conversationService;
    ///
    ///     public async Task&lt;ActionResult&gt; CreateConversation()
    ///     {
    ///         var conversation = await _conversationService.CreateConversationAsync();
    ///         return Ok(conversation);
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="IClaudeConversationService"/>
    /// <seealso cref="ConversationStore"/>
    /// <seealso cref="Conversation"/>
    public class ClaudeConversationService : IClaudeConversationService
    {
        #region private properties

        /**************************************************************/
        /// <summary>
        /// The conversation store for managing conversation state.
        /// </summary>
        /// <seealso cref="ConversationStore"/>
        private readonly ConversationStore _conversationStore;

        /**************************************************************/
        /// <summary>
        /// Logger instance for conversation service operations.
        /// </summary>
        private readonly ILogger<ClaudeConversationService> _logger;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="ClaudeConversationService"/> class.
        /// </summary>
        /// <param name="conversationStore">The conversation store for managing conversation state.</param>
        /// <param name="logger">Logger instance for diagnostic output.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="conversationStore"/> or <paramref name="logger"/> is null.
        /// </exception>
        /// <seealso cref="ConversationStore"/>
        public ClaudeConversationService(
            ConversationStore conversationStore,
            ILogger<ClaudeConversationService> logger)
        {
            #region implementation

            _conversationStore = conversationStore ?? throw new ArgumentNullException(nameof(conversationStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            #endregion
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <inheritdoc/>
        public Task<Conversation> CreateConversationAsync(string? userId = null)
        {
            #region implementation

            // Create new conversation via the store
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

            // Retrieve conversation from the store
            var conversation = _conversationStore.Get(conversationId);

            return Task.FromResult(conversation);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<List<AiConversationMessage>> GetConversationHistoryAsync(string conversationId, int? maxMessages = null)
        {
            #region implementation

            // Get messages from the conversation store
            var messages = _conversationStore.GetMessages(conversationId, maxMessages);

            return Task.FromResult(messages);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public Task<bool> DeleteConversationAsync(string conversationId)
        {
            #region implementation

            // Remove conversation from the store
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

            // Get statistics from the conversation store
            var stats = _conversationStore.GetStats();

            return Task.FromResult(stats);

            #endregion
        }

        #endregion
    }

    #endregion
}
