using MedRecPro.Data;
using MedRecPro.Models;
using MedRecPro.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecPro.Service.Test;

/**************************************************************/
/// <summary>
/// Covers the narrow services introduced while decomposing the Label controller surface.
/// </summary>
/// <remarks>
/// These tests exercise real encrypted-ID formatting and unavailable-AI outcomes without relying on
/// an external database or Claude endpoint.
/// </remarks>
/// <seealso cref="IPrimaryKeyCipher"/>
/// <seealso cref="ILabelAiSearchService"/>
[TestClass]
public class LabelControllerDecompositionServiceTests
{
    /**************************************************************/
    /// <summary>
    /// Verifies the primary-key cipher preserves the established fast encrypted identifier format.
    /// </summary>
    /// <seealso cref="PrimaryKeyCipher"/>
    [TestMethod]
    public void PrimaryKeyCipher_RoundTripsNumericIdsAndRejectsMalformedInput()
    {
        #region implementation

        var cipher = createCipher();

        var encrypted = cipher.Encrypt(42);

        Assert.IsTrue(encrypted.StartsWith("F-", StringComparison.Ordinal));
        Assert.AreEqual(42L, cipher.TryDecrypt(encrypted));
        Assert.IsNull(cipher.TryDecrypt("not-an-encrypted-identifier"));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the dynamic section seam classifies malformed and unsupported requests without touching a repository.
    /// </summary>
    /// <seealso cref="LabelSectionCrudService"/>
    [TestMethod]
    public async Task LabelSectionCrudService_InvalidRequests_ReturnEstablishedOutcomes()
    {
        #region implementation

        var cipher = createCipher();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var service = new LabelSectionCrudService(
            provider,
            NullLogger<LabelSectionCrudService>.Instance,
            cipher);

        var documentation = service.GetDocumentation("not-a-label-section");
        var invalidPage = await service.GetAsync(nameof(Label.Document), 0, 10, CancellationToken.None);
        var invalidRead = await service.GetByIdAsync("not-a-label-section", "invalid", CancellationToken.None);
        var invalidCreate = await service.CreateAsync("not-a-label-section", "{}", CancellationToken.None);
        var invalidUpdate = await service.UpdateAsync("not-a-label-section", "invalid", "{}", CancellationToken.None);
        var invalidDelete = await service.DeleteAsync("not-a-label-section", "invalid", CancellationToken.None);

        Assert.AreEqual(SectionCrudStatus.InvalidInput, documentation.Status);
        Assert.AreEqual(SectionCrudStatus.InvalidInput, invalidPage.Status);
        Assert.AreEqual(SectionCrudStatus.InvalidInput, invalidRead.Status);
        Assert.AreEqual(SectionCrudStatus.InvalidInput, invalidCreate.Status);
        Assert.AreEqual(SectionCrudStatus.InvalidInput, invalidUpdate.Status);
        Assert.AreEqual(SectionCrudStatus.InvalidInput, invalidDelete.Status);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies optional AI search capabilities retain their established fallback outcomes when unavailable.
    /// </summary>
    /// <seealso cref="LabelAiSearchService"/>
    [TestMethod]
    public async Task LabelAiSearchService_OptionalClaudeUnavailable_ReturnsEstablishedFallbacks()
    {
        #region implementation

        var service = new LabelAiSearchService(
            new Mock<IClaudeApiService>().Object,
            createCipher(),
            NullLogger<LabelAiSearchService>.Instance);

        var classResult = await service.SearchByPharmacologicClassAsync(
            "beta blockers", 10, false, null, CancellationToken.None);
        var summaries = await service.GetCachedClassSummariesAsync(CancellationToken.None);
        var extraction = await service.ExtractProductAsync("Search for aspirin", CancellationToken.None);
        var indication = await service.SearchByIndicationAsync(
            "hypertension", 10, false, null, CancellationToken.None);

        Assert.IsNull(classResult);
        Assert.IsNull(summaries);
        Assert.IsFalse(extraction.Success);
        Assert.AreEqual("Product extraction service not available", extraction.Error);
        Assert.IsNull(indication);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a primary-key cipher with deterministic test configuration.
    /// </summary>
    /// <returns>A cipher that uses the test-only secret.</returns>
    /// <seealso cref="PrimaryKeyCipher"/>
    private static IPrimaryKeyCipher createCipher()
    {
        #region implementation

        return new PrimaryKeyCipher(Options.Create(new MedRecPro.Configuration.DatabaseSecurityOptions
        {
            PKSecret = "label-controller-decomposition-test-secret"
        }));

        #endregion
    }
}
