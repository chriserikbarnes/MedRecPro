using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.AI
{
    /**************************************************************/
    /// <summary>
    /// Tests public conversation store lifecycle and statistics methods.
    /// </summary>
    /// <seealso cref="ConversationStore"/>
    [TestClass]
    [TestCategory("Unit")]
    public class ClaudeConversationStoreTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies create, get, touch, message, removal, clear, and stats behavior.
        /// </summary>
        /// <seealso cref="ConversationStore.Create"/>
        /// <seealso cref="ConversationStore.GetOrCreate"/>
        /// <seealso cref="ConversationStore.Get"/>
        /// <seealso cref="ConversationStore.Exists"/>
        /// <seealso cref="ConversationStore.Touch"/>
        /// <seealso cref="ConversationStore.AddMessage"/>
        /// <seealso cref="ConversationStore.GetMessages"/>
        /// <seealso cref="ConversationStore.Remove"/>
        /// <seealso cref="ConversationStore.Clear"/>
        /// <seealso cref="ConversationStore.GetStats"/>
        [TestMethod]
        public void ConversationStore_PublicLifecycleMethods_ReturnExpectedState()
        {
            #region implementation
            var sut = new ConversationStore(NullLogger<ConversationStore>.Instance);

            var created = sut.Create("user-1");
            var custom = sut.GetOrCreate("conversation-fixed", "user-2");
            var fetched = sut.Get(created.ConversationId);
            var touched = sut.Touch(created.ConversationId);

            sut.AddMessage(created.ConversationId, "user", "hello");
            sut.AddMessage(created.ConversationId, "assistant", "hi");
            sut.AddMessage(created.ConversationId, "user", "again");

            var recentMessages = sut.GetMessages(created.ConversationId, maxMessages: 2);
            var stats = sut.GetStats();

            Assert.IsTrue(created.ConversationId.StartsWith("conv-", StringComparison.Ordinal));
            Assert.AreEqual("conversation-fixed", custom.ConversationId);
            Assert.AreSame(created, fetched);
            Assert.IsTrue(sut.Exists(created.ConversationId));
            Assert.IsTrue(touched);
            Assert.AreEqual(2, recentMessages.Count);
            Assert.AreEqual("assistant", recentMessages[0].Role);
            Assert.AreEqual(2, stats.TotalConversations);
            Assert.AreEqual(3, stats.TotalMessages);

            Assert.IsTrue(sut.Remove(created.ConversationId));
            Assert.IsNull(sut.Get(created.ConversationId));
            Assert.AreEqual(1, sut.Clear());
            Assert.AreEqual(0, sut.GetStats().TotalConversations);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies missing conversations return safe false/empty values.
        /// </summary>
        /// <seealso cref="ConversationStore.Touch"/>
        /// <seealso cref="ConversationStore.AddMessage"/>
        /// <seealso cref="ConversationStore.GetMessages"/>
        /// <seealso cref="ConversationStore.Remove"/>
        [TestMethod]
        public void ConversationStore_MissingConversation_ReturnsSafeDefaults()
        {
            #region implementation
            var sut = new ConversationStore(NullLogger<ConversationStore>.Instance);

            Assert.IsFalse(sut.Touch("missing"));
            Assert.IsFalse(sut.AddMessage("missing", "user", "hello"));
            Assert.AreEqual(0, sut.GetMessages("missing").Count);
            Assert.IsFalse(sut.Remove("missing"));
            #endregion
        }

        #endregion
    }
}
