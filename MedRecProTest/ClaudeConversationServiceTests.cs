using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Exercises <see cref="ClaudeConversationService"/> — a thin delegation
    /// layer over the in-memory <see cref="ConversationStore"/>.
    /// </summary>
    /// <remarks>
    /// The store is a concrete sealed class, so the tests run against a real
    /// store instance and assert delegation semantics (state visible through
    /// the store equals state reported by the service) rather than
    /// re-testing store internals already covered by
    /// ClaudeConversationStoreTests.
    /// </remarks>
    /// <seealso cref="ClaudeConversationService"/>
    /// <seealso cref="ConversationStore"/>
    [TestClass]
    public class ClaudeConversationServiceTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies the constructor rejects null dependencies.
        /// </summary>
        /// <seealso cref="ClaudeConversationService"/>
        [TestMethod]
        public void ClaudeConversationService_Constructor_NullDependencies_Throw()
        {
            #region implementation
            // Act + Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new ClaudeConversationService(null!, NullLogger<ClaudeConversationService>.Instance));
            Assert.ThrowsException<ArgumentNullException>(() =>
                new ClaudeConversationService(createStore(), null!));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies CreateConversationAsync creates a store-backed
        /// conversation carrying the supplied user ID.
        /// </summary>
        /// <seealso cref="ClaudeConversationService.CreateConversationAsync"/>
        [TestMethod]
        public async Task CreateConversationAsync_CreatesConversationForUser()
        {
            #region implementation
            // Arrange
            var store = createStore();
            var service = createService(store);

            // Act
            var conversation = await service.CreateConversationAsync("user-42");

            // Assert - identity, ownership, and store delegation.
            StringAssert.StartsWith(conversation.ConversationId, "conv-");
            Assert.AreEqual("user-42", conversation.UserId);
            Assert.AreEqual(0, conversation.MessageCount);
            Assert.AreSame(conversation, store.Get(conversation.ConversationId),
                "The service must create through the shared store.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetConversationAsync returns the stored conversation for a
        /// known ID and null for an unknown ID.
        /// </summary>
        /// <seealso cref="ClaudeConversationService.GetConversationAsync"/>
        [TestMethod]
        public async Task GetConversationAsync_KnownConversation_ReturnsConversation()
        {
            #region implementation
            // Arrange
            var store = createStore();
            var service = createService(store);
            var created = store.Create("user-1");

            // Act
            var found = await service.GetConversationAsync(created.ConversationId);
            var missing = await service.GetConversationAsync("conv-does-not-exist");

            // Assert
            Assert.AreSame(created, found);
            Assert.IsNull(missing);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetConversationHistoryAsync returns the stored messages
        /// and honors the max-message window.
        /// </summary>
        /// <seealso cref="ClaudeConversationService.GetConversationHistoryAsync"/>
        [TestMethod]
        public async Task GetConversationHistoryAsync_ReturnsStoredMessages()
        {
            #region implementation
            // Arrange
            var store = createStore();
            var service = createService(store);
            var conversation = store.Create();
            store.AddMessage(conversation.ConversationId, "user", "first");
            store.AddMessage(conversation.ConversationId, "assistant", "second");
            store.AddMessage(conversation.ConversationId, "user", "third");

            // Act
            var full = await service.GetConversationHistoryAsync(conversation.ConversationId);
            var windowed = await service.GetConversationHistoryAsync(conversation.ConversationId, maxMessages: 2);

            // Assert - full history plus the trailing window.
            Assert.AreEqual(3, full.Count);
            Assert.AreEqual(2, windowed.Count);
            Assert.AreEqual("second", windowed[0].Content);
            Assert.AreEqual("third", windowed[1].Content);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies DeleteConversationAsync removes the conversation from the
        /// store and reports missing IDs as false.
        /// </summary>
        /// <seealso cref="ClaudeConversationService.DeleteConversationAsync"/>
        [TestMethod]
        public async Task DeleteConversationAsync_RemovesConversation()
        {
            #region implementation
            // Arrange
            var store = createStore();
            var service = createService(store);
            var conversation = store.Create();

            // Act
            var removed = await service.DeleteConversationAsync(conversation.ConversationId);
            var removedAgain = await service.DeleteConversationAsync(conversation.ConversationId);

            // Assert
            Assert.IsTrue(removed);
            Assert.IsNull(store.Get(conversation.ConversationId));
            Assert.IsFalse(removedAgain, "Deleting an already-removed conversation reports false.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetConversationStatsAsync surfaces the store's aggregate
        /// statistics.
        /// </summary>
        /// <seealso cref="ClaudeConversationService.GetConversationStatsAsync"/>
        [TestMethod]
        public async Task GetConversationStatsAsync_ReturnsStoreStats()
        {
            #region implementation
            // Arrange - two conversations with three messages total.
            var store = createStore();
            var service = createService(store);
            var first = store.Create("user-1");
            var second = store.Create("user-2");
            store.AddMessage(first.ConversationId, "user", "hello");
            store.AddMessage(first.ConversationId, "assistant", "hi");
            store.AddMessage(second.ConversationId, "user", "hey");

            // Act
            var stats = await service.GetConversationStatsAsync();

            // Assert
            Assert.AreEqual(2, stats.TotalConversations);
            Assert.AreEqual(2, stats.ActiveConversations);
            Assert.AreEqual(3, stats.TotalMessages);
            Assert.IsNotNull(stats.OldestConversation);
            Assert.IsNotNull(stats.NewestConversation);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a fresh in-memory conversation store.
        /// </summary>
        /// <returns>A store isolated to the calling test.</returns>
        private static ConversationStore createStore()
        {
            #region implementation
            return new ConversationStore(NullLogger<ConversationStore>.Instance);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the service under test over the supplied store.
        /// </summary>
        /// <param name="store">Backing conversation store.</param>
        /// <returns>The service under test.</returns>
        private static ClaudeConversationService createService(ConversationStore store)
        {
            #region implementation
            return new ClaudeConversationService(store, NullLogger<ClaudeConversationService>.Instance);
            #endregion
        }

        #endregion
    }
}
