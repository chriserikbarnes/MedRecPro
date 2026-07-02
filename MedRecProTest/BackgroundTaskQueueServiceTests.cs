using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests background task queue public enqueue and dequeue behavior.
    /// </summary>
    /// <seealso cref="BackgroundTaskQueueService"/>
    [TestClass]
    public class BackgroundTaskQueueServiceTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies queued work items are dequeued in FIFO order and empty queues return false.
        /// </summary>
        /// <seealso cref="BackgroundTaskQueueService.Enqueue"/>
        /// <seealso cref="BackgroundTaskQueueService.TryDequeue"/>
        [TestMethod]
        public async Task Enqueue_TryDequeue_WorkItems_ReturnsItemsInOrder()
        {
            #region implementation
            var sut = new BackgroundTaskQueueService();
            var executed = new List<string>();

            sut.Enqueue("op-1", _ =>
            {
                executed.Add("first");
                return Task.CompletedTask;
            });
            sut.Enqueue("op-2", _ =>
            {
                executed.Add("second");
                return Task.CompletedTask;
            });

            Assert.IsTrue(sut.TryDequeue(out var first));
            Assert.AreEqual("op-1", first.Item1);
            await first.Item2(CancellationToken.None);

            Assert.IsTrue(sut.TryDequeue(out var second));
            Assert.AreEqual("op-2", second.Item1);
            await second.Item2(CancellationToken.None);

            Assert.IsFalse(sut.TryDequeue(out _));
            CollectionAssert.AreEqual(new List<string> { "first", "second" }, executed);
            #endregion
        }

        #endregion
    }
}
