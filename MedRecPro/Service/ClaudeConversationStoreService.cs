using MedRecPro.Models;
using System.Collections.Concurrent;

namespace MedRecPro.Service
{
    #region conversation store

    /**************************************************************/
    /// <summary>
    /// Thread-safe in-memory store for AI conversation sessions with automatic expiration.
    /// Manages conversation history between users and the AI agent, enabling multi-turn
    /// contextual conversations with sliding expiration windows.
    /// </summary>
    /// <remarks>
    /// This sealed class provides centralized conversation state management for the AI agent system.
    /// Key characteristics include:
    /// 
    /// <list type="bullet">
    /// <item>Thread-safe operations using <see cref="ConcurrentDictionary{TKey, TValue}"/></item>
    /// <item>Sliding expiration: conversations expire after inactivity period (default 1 hour)</item>
    /// <item>Automatic cleanup of expired conversations on access operations</item>
    /// <item>Server-generated conversation IDs for reliable session tracking</item>
    /// <item>Designed for easy migration to database persistence if needed</item>
    /// </list>
    /// 
    /// The store is registered as a singleton in dependency injection to maintain
    /// conversation state across requests within the application lifetime.
    /// 
    /// <para>
    /// <b>Migration to Database:</b> To persist conversations to a database, implement
    /// <see cref="Conversation"/> class can be used as an Entity Framework entity.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Dependency injection registration (Program.cs)
    /// services.AddSingleton&lt;ConversationStore&gt;();
    /// 
    /// // Usage in service
    /// public class ClaudeApiService
    /// {
    ///     private readonly ConversationStore _conversationStore;
    ///     
    ///     public async Task&lt;AiAgentInterpretation&gt; InterpretRequestAsync(AiAgentRequest request)
    ///     {
    ///         // Get or create conversation
    ///         var conversation = request.ConversationId != null
    ///             ? _conversationStore.GetOrCreate(request.ConversationId)
    ///             : _conversationStore.Create();
    ///         
    ///         // Add user message to history
    ///         _conversationStore.AddMessage(conversation.ConversationId, "user", request.UserMessage);
    ///         
    ///         // ... process request ...
    ///         
    ///         // Add assistant response
    ///         _conversationStore.AddMessage(conversation.ConversationId, "assistant", response);
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="Conversation"/>
    /// <seealso cref="AiConversationMessage"/>
    /// <seealso cref="IClaudeApiService"/>
    public sealed class ConversationStore
    {
        #region private fields

        /**************************************************************/
        /// <summary>
        /// Thread-safe dictionary storing conversations keyed by conversation ID.
        /// </summary>
        private readonly ConcurrentDictionary<string, Conversation> _conversations = new();

        /**************************************************************/
        /// <summary>
        /// Default sliding expiration duration for conversations (1 hour).
        /// </summary>
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromHours(1);

        /**************************************************************/
        /// <summary>
        /// Logger instance for conversation store operations.
        /// </summary>
        private readonly ILogger<ConversationStore>? _logger;

        /**************************************************************/
        /// <summary>
        /// Timer for periodic cleanup of expired conversations.
        /// </summary>
        private readonly Timer _cleanupTimer;

        /**************************************************************/
        /// <summary>
        /// Interval between cleanup operations (15 minutes).
        /// </summary>
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(15);

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the ConversationStore with optional logging.
        /// </summary>
        /// <param name="logger">Optional logger for diagnostic output.</param>
        /// <remarks>
        /// Starts a background timer that periodically removes expired conversations
        /// to prevent unbounded memory growth.
        /// </remarks>
        public ConversationStore(ILogger<ConversationStore>? logger = null)
        {
            #region implementation

            _logger = logger;

            // Start periodic cleanup timer
            _cleanupTimer = new Timer(
                callback: _ => cleanupExpiredConversations(),
                state: null,
                dueTime: _cleanupInterval,
                period: _cleanupInterval);

            _logger?.LogInformation("ConversationStore initialized with {Expiration} expiration and {Cleanup} cleanup interval",
                _defaultExpiration, _cleanupInterval);

            #endregion
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Creates a new conversation with a server-generated unique identifier.
        /// </summary>
        /// <param name="userId">Optional user ID to associate with the conversation.</param>
        /// <returns>A new <see cref="Conversation"/> instance with initialized metadata.</returns>
        /// <remarks>
        /// The conversation is immediately stored and begins its expiration countdown.
        /// The expiration resets each time the conversation is updated via
        /// <see cref="AddMessage"/> or <see cref="Touch"/>.
        /// </remarks>
        /// <example>
        /// <code>
        /// var conversation = _conversationStore.Create(userId: "user-123");
        /// // conversation.ConversationId = "conv-abc123..."
        /// // conversation.ExpiresAt = DateTime.UtcNow + 1 hour
        /// </code>
        /// </example>
        /// <seealso cref="Conversation"/>
        public Conversation Create(string? userId = null)
        {
            #region implementation

            var conversation = new Conversation
            {
                ConversationId = generateConversationId(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_defaultExpiration),
                Messages = new List<AiConversationMessage>()
            };

            _conversations[conversation.ConversationId] = conversation;

            _logger?.LogDebug("Created new conversation {ConversationId} for user {UserId}",
                conversation.ConversationId, userId ?? "anonymous");

            return conversation;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves an existing conversation by ID, or creates a new one if not found or expired.
        /// </summary>
        /// <param name="conversationId">The conversation ID to retrieve.</param>
        /// <param name="userId">Optional user ID for new conversation creation.</param>
        /// <returns>
        /// The existing <see cref="Conversation"/> if found and not expired,
        /// otherwise a new conversation instance.
        /// </returns>
        /// <remarks>
        /// This method automatically handles expired conversations by removing them
        /// and creating fresh instances. The expiration is reset (touched) when
        /// an existing conversation is retrieved.
        /// </remarks>
        /// <example>
        /// <code>
        /// // First call creates new conversation
        /// var conv1 = _conversationStore.GetOrCreate("conv-123");
        /// 
        /// // Subsequent calls retrieve existing (if not expired)
        /// var conv2 = _conversationStore.GetOrCreate("conv-123");
        /// // conv1.ConversationId == conv2.ConversationId
        /// </code>
        /// </example>
        /// <seealso cref="Get"/>
        /// <seealso cref="Create"/>
        public Conversation GetOrCreate(string? conversationId, string? userId = null)
        {
            #region implementation

            // If no ID provided, create new
            if (string.IsNullOrWhiteSpace(conversationId))
            {
                return Create(userId);
            }

            // Try to get existing
            var existing = Get(conversationId);

            if (existing != null)
            {
                // Touch to reset expiration
                Touch(conversationId);
                return existing;
            }

            // Create new with provided ID (allows client-suggested IDs)
            var conversation = new Conversation
            {
                ConversationId = conversationId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_defaultExpiration),
                Messages = new List<AiConversationMessage>()
            };

            _conversations[conversationId] = conversation;

            _logger?.LogDebug("Created conversation with provided ID {ConversationId}", conversationId);

            return conversation;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a conversation by ID without modifying its expiration.
        /// </summary>
        /// <param name="conversationId">The conversation ID to retrieve.</param>
        /// <returns>
        /// The <see cref="Conversation"/> if found and not expired, otherwise null.
        /// </returns>
        /// <remarks>
        /// Unlike <see cref="GetOrCreate"/>, this method does not reset the
        /// expiration timer. Use this for read-only operations like viewing
        /// conversation history.
        /// </remarks>
        /// <seealso cref="GetOrCreate"/>
        /// <seealso cref="Exists"/>
        public Conversation? Get(string conversationId)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(conversationId))
            {
                return null;
            }

            if (_conversations.TryGetValue(conversationId, out var conversation))
            {
                // Check if expired
                if (conversation.ExpiresAt < DateTime.UtcNow)
                {
                    _logger?.LogDebug("Conversation {ConversationId} has expired, removing", conversationId);
                    _conversations.TryRemove(conversationId, out _);
                    return null;
                }

                return conversation;
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks whether a conversation exists and is not expired.
        /// </summary>
        /// <param name="conversationId">The conversation ID to check.</param>
        /// <returns>True if the conversation exists and is valid, false otherwise.</returns>
        /// <seealso cref="Get"/>
        public bool Exists(string conversationId)
        {
            #region implementation

            return Get(conversationId) != null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resets the expiration timer for a conversation, extending its lifetime
        /// by the default expiration duration from the current time.
        /// </summary>
        /// <param name="conversationId">The conversation ID to touch.</param>
        /// <returns>True if the conversation was found and updated, false otherwise.</returns>
        /// <remarks>
        /// This method is automatically called by <see cref="AddMessage"/> and
        /// <see cref="GetOrCreate"/>. Call it explicitly when you want to keep
        /// a conversation alive without adding messages.
        /// </remarks>
        /// <seealso cref="AddMessage"/>
        public bool Touch(string conversationId)
        {
            #region implementation

            if (_conversations.TryGetValue(conversationId, out var conversation))
            {
                if (conversation.ExpiresAt < DateTime.UtcNow)
                {
                    // Already expired
                    _conversations.TryRemove(conversationId, out _);
                    return false;
                }

                conversation.LastActivityAt = DateTime.UtcNow;
                conversation.ExpiresAt = DateTime.UtcNow.Add(_defaultExpiration);

                _logger?.LogDebug("Touched conversation {ConversationId}, new expiry: {ExpiresAt}",
                    conversationId, conversation.ExpiresAt);

                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds a message to an existing conversation's history and resets the expiration timer.
        /// </summary>
        /// <param name="conversationId">The conversation ID to add the message to.</param>
        /// <param name="role">The message role: "user" or "assistant".</param>
        /// <param name="content">The message content.</param>
        /// <returns>True if the message was added successfully, false if conversation not found.</returns>
        /// <remarks>
        /// This is the primary method for building conversation history. Each call
        /// resets the expiration timer, keeping active conversations alive.
        /// 
        /// Messages are stored in chronological order and can be retrieved via
        /// <see cref="GetMessages"/> or the <see cref="Conversation.Messages"/> property.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Add user message
        /// _conversationStore.AddMessage(convId, "user", "Find products by Pfizer");
        /// 
        /// // Process and add assistant response
        /// _conversationStore.AddMessage(convId, "assistant", "I found 47 products...");
        /// </code>
        /// </example>
        /// <seealso cref="GetMessages"/>
        /// <seealso cref="AiConversationMessage"/>
        public bool AddMessage(string conversationId, string role, string content)
        {
            #region implementation

            var conversation = Get(conversationId);

            if (conversation == null)
            {
                _logger?.LogWarning("Cannot add message to non-existent conversation {ConversationId}", conversationId);
                return false;
            }

            var message = new AiConversationMessage
            {
                Role = role,
                Content = content,
                Timestamp = DateTime.UtcNow
            };

            conversation.Messages.Add(message);

            // Reset expiration on activity
            conversation.LastActivityAt = DateTime.UtcNow;
            conversation.ExpiresAt = DateTime.UtcNow.Add(_defaultExpiration);

            _logger?.LogDebug("Added {Role} message to conversation {ConversationId}, total messages: {Count}",
                role, conversationId, conversation.Messages.Count);

            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the message history for a conversation.
        /// </summary>
        /// <param name="conversationId">The conversation ID.</param>
        /// <param name="maxMessages">Optional maximum number of recent messages to return.</param>
        /// <returns>
        /// List of messages in chronological order, or empty list if conversation not found.
        /// </returns>
        /// <remarks>
        /// When <paramref name="maxMessages"/> is specified, returns the most recent
        /// messages. This is useful for limiting context size when sending to Claude.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get all messages
        /// var allMessages = _conversationStore.GetMessages(convId);
        /// 
        /// // Get last 10 messages for context
        /// var recentMessages = _conversationStore.GetMessages(convId, maxMessages: 10);
        /// </code>
        /// </example>
        /// <seealso cref="AddMessage"/>
        public List<AiConversationMessage> GetMessages(string conversationId, int? maxMessages = null)
        {
            #region implementation

            var conversation = Get(conversationId);

            if (conversation == null)
            {
                return new List<AiConversationMessage>();
            }

            if (maxMessages.HasValue && maxMessages.Value > 0)
            {
                return conversation.Messages
                    .TakeLast(maxMessages.Value)
                    .ToList();
            }

            return conversation.Messages.ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Removes a conversation from the store.
        /// </summary>
        /// <param name="conversationId">The conversation ID to remove.</param>
        /// <returns>True if the conversation was found and removed, false otherwise.</returns>
        /// <seealso cref="Clear"/>
        public bool Remove(string conversationId)
        {
            #region implementation

            var removed = _conversations.TryRemove(conversationId, out _);

            if (removed)
            {
                _logger?.LogDebug("Removed conversation {ConversationId}", conversationId);
            }

            return removed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Removes all conversations from the store.
        /// </summary>
        /// <returns>The number of conversations removed.</returns>
        /// <remarks>
        /// Use with caution in production environments. This method is primarily
        /// useful for testing or administrative cleanup operations.
        /// </remarks>
        public int Clear()
        {
            #region implementation

            var count = _conversations.Count;
            _conversations.Clear();

            _logger?.LogInformation("Cleared all {Count} conversations from store", count);

            return count;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets statistics about the conversation store.
        /// </summary>
        /// <returns>A <see cref="ConversationStoreStats"/> object with current metrics.</returns>
        /// <seealso cref="ConversationStoreStats"/>
        public ConversationStoreStats GetStats()
        {
            #region implementation

            var now = DateTime.UtcNow;
            var conversations = _conversations.Values.ToList();

            return new ConversationStoreStats
            {
                TotalConversations = conversations.Count,
                ActiveConversations = conversations.Count(c => c.ExpiresAt > now),
                ExpiredConversations = conversations.Count(c => c.ExpiresAt <= now),
                TotalMessages = conversations.Sum(c => c.Messages.Count),
                OldestConversation = conversations.MinBy(c => c.CreatedAt)?.CreatedAt,
                NewestConversation = conversations.MaxBy(c => c.CreatedAt)?.CreatedAt
            };

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Generates a unique conversation identifier.
        /// </summary>
        /// <returns>A unique conversation ID string.</returns>
        private string generateConversationId()
        {
            #region implementation

            // Format: conv-{guid} for easy identification
            return $"conv-{Guid.NewGuid():N}";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Removes expired conversations from the store.
        /// Called periodically by the cleanup timer.
        /// </summary>
        private void cleanupExpiredConversations()
        {
            #region implementation

            var now = DateTime.UtcNow;
            var expiredIds = _conversations
                .Where(kvp => kvp.Value.ExpiresAt < now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var id in expiredIds)
            {
                _conversations.TryRemove(id, out _);
            }

            if (expiredIds.Count > 0)
            {
                _logger?.LogInformation("Cleanup removed {Count} expired conversations", expiredIds.Count);
            }

            #endregion
        }

        #endregion
    }

    #endregion

    #region conversation model

    /**************************************************************/
    /// <summary>
    /// Represents a conversation session between a user and the AI agent.
    /// Contains metadata and the full message history for the conversation.
    /// </summary>
    /// <remarks>
    /// This class is designed for easy migration to Entity Framework if database
    /// persistence is needed. Simply add [Key] and [Table] attributes.
    /// </remarks>
    /// <example>
    /// <code>
    /// // For EF Core persistence, add:
    /// [Table("Conversations")]
    /// public class Conversation
    /// {
    ///     [Key]
    ///     public string ConversationId { get; set; }
    ///     // ... rest of properties
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="ConversationStore"/>
    /// <seealso cref="AiConversationMessage"/>
    public class Conversation
    {
        /**************************************************************/
        /// <summary>
        /// Unique identifier for the conversation, server-generated.
        /// Format: "conv-{guid}"
        /// </summary>
        public string ConversationId { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Optional encrypted user ID associated with this conversation.
        /// Null for anonymous conversations.
        /// </summary>
        public string? UserId { get; set; }

        /**************************************************************/
        /// <summary>
        /// Timestamp when the conversation was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /**************************************************************/
        /// <summary>
        /// Timestamp of the last activity (message added or touched).
        /// Used for activity tracking and expiration calculation.
        /// </summary>
        public DateTime LastActivityAt { get; set; }

        /**************************************************************/
        /// <summary>
        /// Timestamp when this conversation will expire and be removed.
        /// Reset on each activity to implement sliding expiration.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /**************************************************************/
        /// <summary>
        /// Ordered list of messages in this conversation.
        /// Messages alternate between "user" and "assistant" roles.
        /// </summary>
        public List<AiConversationMessage> Messages { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Optional title for the conversation, derived from first message.
        /// Useful for conversation list display.
        /// </summary>
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Total number of messages in the conversation.
        /// </summary>
        public int MessageCount => Messages.Count;
    }

    #endregion

    #region conversation store stats

    /**************************************************************/
    /// <summary>
    /// Statistics about the conversation store's current state.
    /// </summary>
    /// <seealso cref="ConversationStore.GetStats"/>
    public class ConversationStoreStats
    {
        /**************************************************************/
        /// <summary>
        /// Total number of conversations in the store (including expired).
        /// </summary>
        public int TotalConversations { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of active (non-expired) conversations.
        /// </summary>
        public int ActiveConversations { get; set; }

        /**************************************************************/
        /// <summary>
        /// Number of expired conversations pending cleanup.
        /// </summary>
        public int ExpiredConversations { get; set; }

        /**************************************************************/
        /// <summary>
        /// Total messages across all conversations.
        /// </summary>
        public int TotalMessages { get; set; }

        /**************************************************************/
        /// <summary>
        /// Creation time of the oldest conversation.
        /// </summary>
        public DateTime? OldestConversation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Creation time of the newest conversation.
        /// </summary>
        public DateTime? NewestConversation { get; set; }
    }

    #endregion
}
