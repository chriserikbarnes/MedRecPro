using MedRecPro.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Channels;

namespace MedRecPro.Service.Common;

/**************************************************************/
/// <summary>
/// Queues activity-log persistence independently of the request pipeline.
/// </summary>
/// <remarks>
/// The dispatcher owns a queue and short-lived persistence scopes. Keeping
/// this boundary separate from the action filter preserves non-blocking
/// requests while allowing focused tests to supply a synchronous dispatcher
/// without polling or wall-clock timeouts.
/// </remarks>
/// <seealso cref="MedRecPro.Filters.ActivityLogActionFilter"/>
/// <seealso cref="IActivityLogService"/>
public interface IActivityLogDispatcher
{
    /**************************************************************/
    /// <summary>
    /// Queues one captured activity log for background persistence.
    /// </summary>
    /// <param name="activityLog">The immutable request snapshot to persist.</param>
    /// <seealso cref="ActivityLog"/>
    void Dispatch(ActivityLog activityLog);
}

/**************************************************************/
/// <summary>
/// Persists queued activity logs through fresh dependency-injection scopes.
/// </summary>
/// <remarks>
/// A single-reader channel transfers work out of MVC before persistence begins.
/// Each item uses a fresh scope, preventing the filter from holding request-
/// scoped services after MVC completes. Exceptions are contained and logged so
/// activity logging never changes the outcome of the originating request.
/// </remarks>
/// <seealso cref="IActivityLogDispatcher"/>
/// <seealso cref="IActivityLogService"/>
internal sealed class ActivityLogDispatcher : IActivityLogDispatcher
{
    #region implementation

    // A bounded queue prevents a stalled persistence dependency from consuming memory without limit.
    internal const int DefaultQueueCapacity = 1024;

    // Hosted shutdown waits at most this long for normal queued persistence before yielding to host termination.
    internal static readonly TimeSpan ShutdownDrainTimeout = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ActivityLogDispatcher> _logger;
    private readonly Channel<ActivityLog> _activityLogs;

    /**************************************************************/
    /// <summary>
    /// Initializes a dispatcher with its scope factory and logger.
    /// </summary>
    /// <param name="serviceScopeFactory">Factory used to create an independent persistence scope.</param>
    /// <param name="logger">Logger used to report persistence failures.</param>
    /// <seealso cref="IServiceScopeFactory"/>
    public ActivityLogDispatcher(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ActivityLogDispatcher> logger)
        : this(serviceScopeFactory, logger, DefaultQueueCapacity)
    {
        #region implementation
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Initializes a dispatcher with an explicit bounded queue capacity for focused verification.
    /// </summary>
    /// <param name="serviceScopeFactory">Factory used to create an independent persistence scope.</param>
    /// <param name="logger">Logger used to report persistence failures and dropped entries.</param>
    /// <param name="queueCapacity">Maximum number of activity logs retained before the oldest entry is dropped.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="queueCapacity"/> is not positive.</exception>
    /// <seealso cref="DefaultQueueCapacity"/>
    internal ActivityLogDispatcher(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ActivityLogDispatcher> logger,
        int queueCapacity)
    {
        #region implementation

        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (queueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queueCapacity), queueCapacity, "Activity-log queue capacity must be positive.");
        }

        _activityLogs = Channel.CreateBounded<ActivityLog>(
            new BoundedChannelOptions(queueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            },
            _ => _logger.LogWarning(
                "Activity-log queue reached its capacity of {QueueCapacity}; the oldest queued entry was dropped",
                queueCapacity));

        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    public void Dispatch(ActivityLog activityLog)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(activityLog);

        if (!_activityLogs.Writer.TryWrite(activityLog))
        {
            _logger.LogWarning("Unable to queue an activity log for background persistence");
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Reads queued activity logs and persists them until the host stops.
    /// </summary>
    /// <param name="stoppingToken">Token raised when the application host is stopping.</param>
    /// <returns>A task that represents the background consumer lifetime.</returns>
    /// <seealso cref="Dispatch"/>
    internal async Task processQueueAsync(CancellationToken stoppingToken)
    {
        #region implementation

        using var completionRegistration = stoppingToken.Register(
            static state => ((ChannelWriter<ActivityLog>)state!).TryComplete(),
            _activityLogs.Writer);

        await foreach (var activityLog in _activityLogs.Reader.ReadAllAsync())
        {
            await persistAsync(activityLog).ConfigureAwait(false);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Persists one queued activity log and contains persistence failures.
    /// </summary>
    /// <param name="activityLog">Captured request activity to persist.</param>
    /// <returns>A task that completes after persistence succeeds or failure is logged.</returns>
    /// <remarks>
    /// This friend-assembly seam lets focused tests verify the background
    /// failure contract without starting a host or polling a queue.
    /// </remarks>
    /// <seealso cref="IActivityLogService"/>
    internal async Task persistAsync(ActivityLog activityLog)
    {
        #region implementation

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var activityLogService = scope.ServiceProvider.GetRequiredService<IActivityLogService>();
            await activityLogService.LogActivityAsync(activityLog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist activity log asynchronously");
        }

        #endregion
    }

    #endregion
}

/**************************************************************/
/// <summary>
/// Runs the activity-log dispatcher queue within the application host.
/// </summary>
/// <remarks>
/// This separate hosted-service wrapper keeps the dispatch abstraction
/// singleton while allowing test hosts to remove only the background consumer.
/// </remarks>
/// <seealso cref="ActivityLogDispatcher"/>
/// <seealso cref="IHostedService"/>
internal sealed class ActivityLogDispatcherHostedService : BackgroundService
{
    #region implementation

    private readonly ActivityLogDispatcher _dispatcher;

    /**************************************************************/
    /// <summary>
    /// Initializes the hosted queue consumer.
    /// </summary>
    /// <param name="dispatcher">Dispatcher that owns the queued activity logs.</param>
    /// <seealso cref="ActivityLogDispatcher"/>
    public ActivityLogDispatcherHostedService(ActivityLogDispatcher dispatcher)
    {
        #region implementation

        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Runs the dispatcher's single-reader queue until host shutdown.
    /// </summary>
    /// <param name="stoppingToken">Token raised when the host is stopping.</param>
    /// <returns>A task that represents the hosted consumer lifetime.</returns>
    /// <seealso cref="ActivityLogDispatcher.processQueueAsync"/>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        #region implementation

        return _dispatcher.processQueueAsync(stoppingToken);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Signals queue completion and waits within the documented shutdown drain window.
    /// </summary>
    /// <param name="cancellationToken">Host-provided token that can shorten the drain window.</param>
    /// <returns>A task representing the bounded hosted-service shutdown.</returns>
    /// <remarks>
    /// <see cref="BackgroundService.StopAsync"/> cancels the execution token, which completes the channel writer.
    /// The consumer then drains queued entries until it finishes, the host cancels, or the 30-second grace period elapses.
    /// </remarks>
    /// <seealso cref="ActivityLogDispatcher.ShutdownDrainTimeout"/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        #region implementation

        using var drainTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drainTimeout.CancelAfter(ActivityLogDispatcher.ShutdownDrainTimeout);
        await base.StopAsync(drainTimeout.Token).ConfigureAwait(false);

        #endregion
    }

    #endregion
}
