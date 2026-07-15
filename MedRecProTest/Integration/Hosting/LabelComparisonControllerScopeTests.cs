using MedRecPro.Api.Controllers;
using MedRecPro.Models;
using MedRecPro.Models.Extensions;
using MedRecPro.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecProTest.Integration.Hosting;

/**************************************************************/
/// <summary>
/// Verifies comparison work uses a background-owned dependency-injection scope.
/// </summary>
/// <remarks>
/// The queued callback must resolve its comparison service only after the originating request scope is disposed.
/// </remarks>
/// <seealso cref="LabelComparisonController"/>
/// <seealso cref="IServiceScopeFactory"/>
[TestClass]
[TestCategory("Integration")]
public class LabelComparisonControllerScopeTests
{
    /**************************************************************/
    /// <summary>
    /// Verifies a queued comparison outlives the originating request scope and request-aborted token.
    /// </summary>
    /// <returns>A task representing the background callback verification.</returns>
    /// <seealso cref="LabelComparisonController.QueueDocumentComparisonAnalysis"/>
    /// <seealso cref="IBackgroundTaskQueueService"/>
    [TestMethod]
    public async Task QueueDocumentComparisonAnalysis_RequestCompleted_UsesFreshBackgroundScopeWithoutRequestCancellation()
    {
        #region implementation

        var documentGuid = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var queuedComparison = new Mock<IComparisonService>();
        queuedComparison
            .Setup(service => service.GenerateDocumentComparisonAsync(documentGuid))
            .ReturnsAsync(new DocumentComparisonResult { DocumentGuid = documentGuid });

        var services = new ServiceCollection();
        services.AddScoped<IComparisonService>(_ => queuedComparison.Object);
        using var rootProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var requestScope = rootProvider.CreateScope();
        var queue = new BackgroundTaskQueueService();
        var statusStore = new Mock<IOperationStatusStore>();
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(CancellationToken.None);
        var coordinator = new ComparisonJobCoordinator(
            queue,
            statusStore.Object,
            rootProvider.GetRequiredService<IServiceScopeFactory>(),
            applicationLifetime.Object,
            NullLogger<ComparisonJobCoordinator>.Instance);
        using var requestAborted = new CancellationTokenSource();
        var controller = new LabelComparisonController(
            new Mock<IComparisonService>().Object,
            NullLogger<LabelComparisonController>.Instance,
            coordinator,
            statusStore.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = requestScope.ServiceProvider,
                    RequestAborted = requestAborted.Token
                }
            },
            Url = new Mock<IUrlHelper>().Object
        };

        var queuedResult = controller.QueueDocumentComparisonAnalysis(documentGuid, CancellationToken.None);

        requestScope.Dispose();
        requestAborted.Cancel();
        Assert.IsInstanceOfType(queuedResult.Result, typeof(AcceptedResult));
        Assert.IsTrue(queue.TryDequeue(out var queuedWork));

        await queuedWork.Item2(CancellationToken.None);

        queuedComparison.Verify(
            service => service.GenerateDocumentComparisonAsync(documentGuid),
            Times.Once);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies every queued comparison status transition preserves the original enqueue timestamp.
    /// </summary>
    /// <returns>A task representing the queued status-transition verification.</returns>
    /// <remarks>
    /// The comparison service captures the analyzing state from the real operation store before returning a result;
    /// the terminal assertion then proves both an intermediate and final transition retained the initial timestamp.
    /// </remarks>
    /// <seealso cref="ComparisonJobCoordinator.Enqueue"/>
    /// <seealso cref="ComparisonOperationStatus.CreatedAt"/>
    [TestMethod]
    public async Task ComparisonJobCoordinator_StatusTransitions_PreserveEnqueueCreatedAt()
    {
        #region implementation

        var operationId = $"created-at-{Guid.NewGuid():N}";
        var documentGuid = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var statusStore = new InMemoryOperationStatusStore();
        ComparisonOperationStatus? analyzingStatus = null;
        var comparisonService = new Mock<IComparisonService>();
        comparisonService
            .Setup(service => service.GenerateDocumentComparisonAsync(documentGuid))
            .Callback(() => statusStore.TryGetComparisonStatus(operationId, out analyzingStatus))
            .ReturnsAsync(new DocumentComparisonResult { DocumentGuid = documentGuid });

        var services = new ServiceCollection();
        services.AddScoped<IComparisonService>(_ => comparisonService.Object);
        using var rootProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var queue = new BackgroundTaskQueueService();
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(CancellationToken.None);
        var coordinator = new ComparisonJobCoordinator(
            queue,
            statusStore,
            rootProvider.GetRequiredService<IServiceScopeFactory>(),
            applicationLifetime.Object,
            NullLogger<ComparisonJobCoordinator>.Instance);

        var initialStatus = coordinator.Enqueue(new ComparisonJobRequest(operationId, documentGuid, "/comparison/status"));
        Assert.IsTrue(queue.TryDequeue(out var queuedWork));

        await queuedWork.Item2(CancellationToken.None);
        var foundFinalStatus = statusStore.TryGetComparisonStatus(operationId, out var finalStatus);

        Assert.IsNotNull(analyzingStatus);
        Assert.AreEqual(ComparisonConstants.STATUS_ANALYZING, analyzingStatus.Status);
        Assert.AreEqual(initialStatus.CreatedAt, analyzingStatus.CreatedAt);
        Assert.IsTrue(foundFinalStatus);
        Assert.IsNotNull(finalStatus);
        Assert.AreEqual(ComparisonConstants.STATUS_COMPLETED, finalStatus.Status);
        Assert.AreEqual(initialStatus.CreatedAt, finalStatus.CreatedAt);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies application shutdown classifies queued comparison cancellation as canceled.
    /// </summary>
    /// <returns>A task representing the queued shutdown-cancellation verification.</returns>
    /// <remarks>
    /// Host or worker cancellation is an external stop signal rather than a comparison failure, so the coordinator
    /// must not enter the comparison body and must publish the established canceled terminal state.
    /// </remarks>
    /// <seealso cref="ComparisonJobCoordinator.Enqueue"/>
    /// <seealso cref="IHostApplicationLifetime.ApplicationStopping"/>
    [TestMethod]
    public async Task ComparisonJobCoordinator_ApplicationStoppingCancellation_SetsCanceledStatus()
    {
        #region implementation

        var operationId = $"shutdown-canceled-{Guid.NewGuid():N}";
        var documentGuid = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var statusStore = new InMemoryOperationStatusStore();
        var comparisonService = new Mock<IComparisonService>();
        var services = new ServiceCollection();
        services.AddScoped<IComparisonService>(_ => comparisonService.Object);
        using var rootProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var queue = new BackgroundTaskQueueService();
        using var applicationStopping = new CancellationTokenSource();
        applicationStopping.Cancel();
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(applicationStopping.Token);
        var coordinator = new ComparisonJobCoordinator(
            queue,
            statusStore,
            rootProvider.GetRequiredService<IServiceScopeFactory>(),
            applicationLifetime.Object,
            NullLogger<ComparisonJobCoordinator>.Instance);

        coordinator.Enqueue(new ComparisonJobRequest(operationId, documentGuid, "/comparison/status"));
        Assert.IsTrue(queue.TryDequeue(out var queuedWork));

        await queuedWork.Item2(CancellationToken.None);
        var foundStatus = statusStore.TryGetComparisonStatus(operationId, out var finalStatus);

        Assert.IsTrue(foundStatus);
        Assert.IsNotNull(finalStatus);
        Assert.AreEqual(ComparisonConstants.STATUS_CANCELED, finalStatus.Status);
        comparisonService.Verify(
            service => service.GenerateDocumentComparisonAsync(It.IsAny<Guid>()),
            Times.Never);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies an unrelated cancellation exception from the comparison body is classified as failed.
    /// </summary>
    /// <returns>A task representing the comparison-body failure classification verification.</returns>
    /// <remarks>
    /// Without a host or worker cancellation signal, an <see cref="OperationCanceledException"/> indicates that the
    /// comparison did not complete for an internal reason. The approved policy records that outcome as failed.
    /// </remarks>
    /// <seealso cref="ComparisonJobCoordinator.Enqueue"/>
    /// <seealso cref="OperationCanceledException"/>
    [TestMethod]
    public async Task ComparisonJobCoordinator_ComparisonBodyThrowsOperationCanceledException_SetsFailedStatus()
    {
        #region implementation

        var operationId = $"body-oce-failed-{Guid.NewGuid():N}";
        var documentGuid = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var statusStore = new InMemoryOperationStatusStore();
        var comparisonService = new Mock<IComparisonService>();
        comparisonService
            .Setup(service => service.GenerateDocumentComparisonAsync(documentGuid))
            .ThrowsAsync(new OperationCanceledException("The comparison body canceled independently."));
        var services = new ServiceCollection();
        services.AddScoped<IComparisonService>(_ => comparisonService.Object);
        using var rootProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var queue = new BackgroundTaskQueueService();
        var applicationLifetime = new Mock<IHostApplicationLifetime>();
        applicationLifetime.SetupGet(lifetime => lifetime.ApplicationStopping).Returns(CancellationToken.None);
        var coordinator = new ComparisonJobCoordinator(
            queue,
            statusStore,
            rootProvider.GetRequiredService<IServiceScopeFactory>(),
            applicationLifetime.Object,
            NullLogger<ComparisonJobCoordinator>.Instance);

        coordinator.Enqueue(new ComparisonJobRequest(operationId, documentGuid, "/comparison/status"));
        Assert.IsTrue(queue.TryDequeue(out var queuedWork));

        await queuedWork.Item2(CancellationToken.None);
        var foundStatus = statusStore.TryGetComparisonStatus(operationId, out var finalStatus);

        Assert.IsTrue(foundStatus);
        Assert.IsNotNull(finalStatus);
        Assert.AreEqual(ComparisonConstants.STATUS_FAILED, finalStatus.Status);
        Assert.AreEqual(ComparisonConstants.ERROR_ANALYSIS_FAILED, finalStatus.Error);

        #endregion
    }
}
