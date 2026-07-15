using MedRecPro.Service;
using MedRecPro.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecProTest.Unit.Label.Sections;

/**************************************************************/
/// <summary>
/// Covers XML retrieval classification extracted from the Label document controller.
/// </summary>
/// <seealso cref="LabelXmlDocumentService"/>
/// <seealso cref="ILabelXmlDocumentService"/>
[TestClass]
[TestCategory("Unit")]
public class LabelXmlDocumentServiceTests
{
    /**************************************************************/
    /// <summary>
    /// Verifies generated XML is normalized to UTF-8 while the export gate returns the established disabled outcome.
    /// </summary>
    /// <returns>A task representing the asynchronous service assertion.</returns>
    /// <seealso cref="ILabelXmlDocumentService.GetGeneratedAsync"/>
    [TestMethod]
    public async Task GetGeneratedAsync_EnabledAndDisabled_ReturnsEstablishedOutcomes()
    {
        #region implementation

        var exporter = new Mock<ISplExportService>();
        exporter
            .Setup(service => service.ExportDocumentToSplAsync(It.IsAny<Guid>(), false))
            .ReturnsAsync("<?xml version=\"1.0\" encoding=\"UTF-16\"?><document />");

        var enabled = createService(true, exporter.Object, new Mock<SplDataService>().Object);
        var disabled = createService(false, exporter.Object, new Mock<SplDataService>().Object);

        var generated = await enabled.GetGeneratedAsync(Guid.NewGuid(), false, CancellationToken.None);
        var gated = await disabled.GetGeneratedAsync(Guid.NewGuid(), false, CancellationToken.None);

        Assert.AreEqual(LabelXmlDocumentStatus.Success, generated.Status);
        StringAssert.Contains(generated.Xml!, "encoding=\"UTF-8\"");
        Assert.AreEqual(LabelXmlDocumentStatus.Disabled, gated.Status);
        Assert.AreEqual("Export functionality is currently disabled", gated.Error);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies missing original XML maps to the existing not-found outcome instead of an exception.
    /// </summary>
    /// <returns>A task representing the asynchronous service assertion.</returns>
    /// <seealso cref="ILabelXmlDocumentService.GetOriginalAsync"/>
    [TestMethod]
    public async Task GetOriginalAsync_MissingImportedXml_ReturnsNotFound()
    {
        #region implementation

        var originalXml = new Mock<SplDataService>();
        originalXml
            .Setup(service => service.GetSplDataByGuidAsync(It.IsAny<Guid>()))
            .ReturnsAsync((SplData?)null);

        var service = createService(true, new Mock<ISplExportService>().Object, originalXml.Object);

        var result = await service.GetOriginalAsync(Guid.NewGuid(), false, CancellationToken.None);

        Assert.AreEqual(LabelXmlDocumentStatus.NotFound, result.Status);
        StringAssert.StartsWith(result.Error!, "Original XML document not found for GUID:");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates the XML retrieval service with an explicit in-memory feature gate.
    /// </summary>
    /// <param name="exportEnabled">Whether the test feature gate should permit export.</param>
    /// <param name="exportService">The configured generated-XML service double.</param>
    /// <param name="splDataService">The configured original-XML service double.</param>
    /// <returns>A configured XML retrieval service.</returns>
    /// <seealso cref="LabelXmlDocumentService"/>
    private static LabelXmlDocumentService createService(bool exportEnabled, ISplExportService exportService, SplDataService splDataService)
    {
        #region implementation

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:SplExportEnabled"] = exportEnabled.ToString()
            })
            .Build();

        return new LabelXmlDocumentService(
            configuration,
            exportService,
            splDataService,
            NullLogger<LabelXmlDocumentService>.Instance);

        #endregion
    }
}
