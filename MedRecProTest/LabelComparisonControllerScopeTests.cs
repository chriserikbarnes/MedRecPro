using MedRecPro.Api.Controllers;
using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecProTest;

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
}
