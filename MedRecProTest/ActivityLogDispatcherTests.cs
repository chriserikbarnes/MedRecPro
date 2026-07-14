using MedRecPro.Models;
using MedRecPro.Service;
using MedRecPro.Service.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Concurrent;

namespace MedRecProTest;

/**************************************************************/
/// <summary>
/// Verifies queued activity persistence remains non-blocking and drains during graceful shutdown.
/// </summary>
/// <seealso cref="ActivityLogDispatcher"/>
/// <seealso cref="IActivityLogDispatcher"/>
[TestClass]
public class ActivityLogDispatcherTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies shutdown completes the writer and drains every queued entry even when persistence is mid-flight.
    /// </summary>
    /// <returns>A task representing the asynchronous drain verification.</returns>
    /// <remarks>
    /// The first persistence call is held open while the remaining entries queue. Dispatch must return before the
    /// gate is released, and canceling the host token must let the consumer persist the entire tail before exiting.
    /// </remarks>
    /// <seealso cref="ActivityLogDispatcher.processQueueAsync"/>
    /// <seealso cref="IActivityLogDispatcher.Dispatch"/>
    [TestMethod]
    public async Task ActivityLogDispatcher_ShutdownMidQueue_DrainsAllEntriesWithoutBlockingDispatch()
    {
        #region implementation

        const int entryCount = 5;
        var persisted = new ConcurrentQueue<long>();
        var firstPersistenceStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstPersistence = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var persistenceCalls = 0;
        var activityLogService = new Mock<IActivityLogService>();
        activityLogService
            .Setup(service => service.LogActivityAsync(It.IsAny<ActivityLog>()))
            .Returns<ActivityLog>(async activityLog =>
            {
                persisted.Enqueue(activityLog.ActivityLogId);
                if (Interlocked.Increment(ref persistenceCalls) == 1)
                {
                    firstPersistenceStarted.TrySetResult();
                    await releaseFirstPersistence.Task;
                }
            });

        var services = new ServiceCollection();
        services.AddScoped<IActivityLogService>(_ => activityLogService.Object);
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var dispatcher = new ActivityLogDispatcher(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ActivityLogDispatcher>.Instance);
        using var stopping = new CancellationTokenSource();
        var consumer = dispatcher.processQueueAsync(stopping.Token);

        var dispatchTask = Task.Run(() =>
        {
            for (var index = 1; index <= entryCount; index++)
            {
                dispatcher.Dispatch(new ActivityLog
                {
                    ActivityLogId = index,
                    ActivityType = "ContractTest"
                });
            }
        });

        await dispatchTask.WaitAsync(TimeSpan.FromSeconds(1));
        await firstPersistenceStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        stopping.Cancel();
        releaseFirstPersistence.TrySetResult();
        await consumer.WaitAsync(TimeSpan.FromSeconds(1));

        CollectionAssert.AreEquivalent(
            Enumerable.Range(1, entryCount).Select(index => (long)index).ToArray(),
            persisted.ToArray());

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies a full bounded queue drops its oldest entry and emits an operator-visible warning.
    /// </summary>
    /// <returns>A task representing the bounded-queue verification.</returns>
    /// <seealso cref="ActivityLogDispatcher.DefaultQueueCapacity"/>
    /// <seealso cref="BoundedChannelFullMode.DropOldest"/>
    [TestMethod]
    public async Task ActivityLogDispatcher_QueueAtCapacity_DropsOldestAndLogsWarning()
    {
        #region implementation

        var persisted = new ConcurrentQueue<long>();
        var activityLogService = new Mock<IActivityLogService>();
        activityLogService
            .Setup(service => service.LogActivityAsync(It.IsAny<ActivityLog>()))
            .Callback<ActivityLog>(activityLog => persisted.Enqueue(activityLog.ActivityLogId))
            .Returns(Task.CompletedTask);
        var services = new ServiceCollection();
        services.AddScoped<IActivityLogService>(_ => activityLogService.Object);
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var logger = new RecordingLogger<ActivityLogDispatcher>();
        var dispatcher = new ActivityLogDispatcher(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            logger,
            queueCapacity: 2);

        dispatcher.Dispatch(new ActivityLog { ActivityLogId = 1, ActivityType = "ContractTest" });
        dispatcher.Dispatch(new ActivityLog { ActivityLogId = 2, ActivityType = "ContractTest" });
        dispatcher.Dispatch(new ActivityLog { ActivityLogId = 3, ActivityType = "ContractTest" });

        using var stopping = new CancellationTokenSource();
        var consumer = dispatcher.processQueueAsync(stopping.Token);
        stopping.Cancel();
        await consumer.WaitAsync(TimeSpan.FromSeconds(1));

        CollectionAssert.AreEqual(new long[] { 2, 3 }, persisted.ToArray());
        Assert.IsTrue(logger.WarningMessages.Any(message =>
            message.Contains("oldest queued entry was dropped", StringComparison.Ordinal)));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Records warning messages without requiring a dynamic proxy for an internal logging category type.
    /// </summary>
    /// <typeparam name="T">Logging category associated with the component under test.</typeparam>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        /**************************************************************/
        /// <summary>
        /// Gets warning messages formatted by the logger under test.
        /// </summary>
        public ConcurrentQueue<string> WarningMessages { get; } = new();

        /**************************************************************/
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            #region implementation

            return null;

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel)
        {
            #region implementation

            return true;

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            #region implementation

            if (logLevel == LogLevel.Warning)
            {
                WarningMessages.Enqueue(formatter(state, exception));
            }

            #endregion
        }
    }

    #endregion
}
