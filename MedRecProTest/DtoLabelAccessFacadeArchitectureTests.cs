using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Features.AeDashboard.Mapping;
using MedRecPro.Models;
using MedRecPro.Service;
using MedRecPro.Service.Common;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace MedRecProTest
{
    /**************************************************************/
    /// <summary>
    /// Guards Phase 2 ownership direction between AE services and the static facade.
    /// </summary>
    /// <remarks>
    /// Source-level assertions prevent future implementation logic from migrating
    /// back into the compatibility facade or from reintroducing service-to-facade calls.
    /// </remarks>
    /// <seealso cref="DtoLabelAccess"/>
    /// <seealso cref="AeDashboardDataAccess"/>
    [TestClass]
    public class DtoLabelAccessFacadeArchitectureTests
    {
        /**************************************************************/
        /// <summary>
        /// Verifies the concrete AE services do not execute static facade calls.
        /// </summary>
        /// <seealso cref="AeDashboardProductCatalogService"/>
        [TestMethod]
        public void AeDashboardServices_ContainNoExecutableDtoLabelAccessCalls()
        {
            #region implementation

            var source = File.ReadAllText(findRepoFile(@"MedRecPro\Service\AeDashboardServices.cs"));

            Assert.IsFalse(
                Regex.IsMatch(source, @"(?:return|=>)\s*DtoLabelAccess\.", RegexOptions.CultureInvariant),
                "Concrete AE services must call the injected feature implementation, not DtoLabelAccess.");
            Assert.IsFalse(source.Contains(".Shared", StringComparison.Ordinal),
                "AE service code must not use shared compatibility policy state.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the static AE facade file contains forwarding-only compatibility code.
        /// </summary>
        /// <seealso cref="AeDashboardLegacyCompatibility"/>
        [TestMethod]
        public void AeDashboardCompatibilityFacade_ContainsNoEfCacheEncryptionOrMappingLogic()
        {
            #region implementation

            var source = File.ReadAllText(findRepoFile(@"MedRecPro\DataAccess\DtoLabelAccess.Compatibility.cs"));
            var forbiddenTokens = new[]
            {
                "AsNoTracking",
                "ToListAsync",
                "PerformanceHelper",
                "StringCipher",
                "SetCacheManageKey",
                "Base64Encode",
                "AeDashboardDerivation"
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.IsFalse(source.Contains(token, StringComparison.Ordinal),
                    $"The compatibility facade must not contain implementation token '{token}'.");
            }

            Assert.IsTrue(source.Contains("=> AeDashboardLegacyCompatibility.Create", StringComparison.Ordinal),
                "Legacy static methods must forward through the isolated compatibility adapter.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the relocated non-AE facade contains forwarding-only compatibility code.
        /// </summary>
        /// <seealso cref="MedRecPro.Service.LabelQuery.Implementation.LabelQueryLegacyCompatibility"/>
        [TestMethod]
        public void NonAeCompatibilityFacade_ContainsNoEfCacheEncryptionMappingOrGraphLogic()
        {
            #region implementation

            var source = File.ReadAllText(findRepoFile(@"MedRecPro\DataAccess\DtoLabelAccess.Compatibility.cs"));
            var forbiddenTokens = new[]
            {
                "AsNoTracking",
                "ToListAsync",
                "PerformanceHelper",
                "StringCipher",
                "SetCacheManageKey",
                "Base64Encode",
                "buildDocument",
                "batchLoad"
            };

            foreach (var token in forbiddenTokens)
            {
                Assert.IsFalse(source.Contains(token, StringComparison.Ordinal),
                    $"The non-AE compatibility facade must not contain implementation token '{token}'.");
            }

            Assert.IsTrue(source.Contains("=> LabelQueryLegacyCompatibility.Create", StringComparison.Ordinal),
                "Legacy non-AE methods must forward through the isolated compatibility adapter.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the legacy facade has exactly one forwarding-only source file.
        /// </summary>
        /// <remarks>
        /// This protects the Phase 5 consolidation from quietly reintroducing a
        /// second static partial implementation during a future feature change.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess"/>
        [TestMethod]
        public void CompatibilityFacade_IsTheOnlyStaticPartialSource()
        {
            #region implementation

            var dataAccessDirectory = Path.GetDirectoryName(
                findRepoFile(@"MedRecPro\DataAccess\DtoLabelAccess.Compatibility.cs"))!;
            var facadeSources = Directory.EnumerateFiles(dataAccessDirectory, "DtoLabelAccess*.cs")
                .Where(path => File.ReadAllText(path).Contains(
                    "public static partial class DtoLabelAccess",
                    StringComparison.Ordinal))
                .Select(Path.GetFileName)
                .OrderBy(fileName => fileName, StringComparer.Ordinal)
                .ToList();
            var source = File.ReadAllText(Path.Combine(dataAccessDirectory, "DtoLabelAccess.Compatibility.cs"));

            CollectionAssert.AreEqual(
                new[] { "DtoLabelAccess.Compatibility.cs" },
                facadeSources,
                "The supported legacy facade must remain in its one explicit compatibility source file.");
            Assert.AreEqual(
                1,
                Regex.Matches(source, @"public\s+static\s+partial\s+class\s+DtoLabelAccess").Count,
                "The compatibility file must declare one consolidated facade type.");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies direct injected-service and legacy-static product/detail results stay equivalent.
        /// </summary>
        /// <seealso cref="AeDashboardProductCatalogService"/>
        /// <seealso cref="DtoLabelAccess.GetAeDrugSummariesAsync"/>
        [TestMethod]
        public async Task AeDashboardServiceAndLegacyFacade_SeededCatalogAndDetail_AreEquivalent()
        {
            #region implementation

            DtoLabelAccessTestHelper.ClearCache();
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel;
            using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var logger = DtoLabelAccessTestHelper.CreateTestLogger();
            var service = createProductDetailService(context, logger);
            var catalogService = createCatalogService(context, logger);

            DtoLabelAccessTestHelper.SeedAeDrugSummaryView(
                connection,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                productName: "PARITY ASPIRIN");
            DtoLabelAccessTestHelper.SeedAeRiskSignalTable(
                connection,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                productName: "PARITY ASPIRIN",
                parameterName: "Headache");

            var directCatalog = await catalogService.GetDrugSummariesAsync(DtoLabelAccessTestHelper.TestPkSecret);
            var legacyCatalog = await DtoLabelAccess.GetAeDrugSummariesAsync(context, DtoLabelAccessTestHelper.TestPkSecret, logger);
            var directDetail = await service.GetProductDetailDataAsync(DtoLabelAccessTestHelper.TestDocumentGuid, DtoLabelAccessTestHelper.TestPkSecret);
            var legacyDetail = await DtoLabelAccess.GetAeProductDetailDataAsync(
                context,
                DtoLabelAccessTestHelper.TestDocumentGuid,
                DtoLabelAccessTestHelper.TestPkSecret,
                logger);

            Assert.AreEqual(legacyCatalog.Count, directCatalog.Count);
            Assert.AreEqual(legacyCatalog.Single().DocumentGUID, directCatalog.Single().DocumentGUID);
            Assert.AreEqual(legacyCatalog.Single().ProductName, directCatalog.Single().ProductName);
            Assert.IsNotNull(directDetail);
            Assert.IsNotNull(legacyDetail);
            Assert.AreEqual(legacyDetail.Product.DocumentGUID, directDetail.Product.DocumentGUID);
            CollectionAssert.AreEqual(
                legacyDetail.Signals.Select(signal => signal.ParameterName).ToList(),
                directDetail.Signals.Select(signal => signal.ParameterName).ToList());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates one direct catalog service using the same injectable policies as DI.
        /// </summary>
        /// <param name="context">The seeded relational context.</param>
        /// <param name="logger">The test diagnostics logger.</param>
        /// <returns>A direct catalog service.</returns>
        private static AeDashboardProductCatalogService createCatalogService(ApplicationDbContext context, ILogger logger)
        {
            #region implementation

            return new AeDashboardProductCatalogService(context, logger, createDataAccess());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates one direct product-detail service using the same injectable policies as DI.
        /// </summary>
        /// <param name="context">The seeded relational context.</param>
        /// <param name="logger">The test diagnostics logger.</param>
        /// <returns>A direct product-detail service.</returns>
        private static AeDashboardProductDetailService createProductDetailService(ApplicationDbContext context, ILogger logger)
        {
            #region implementation

            return new AeDashboardProductDetailService(context, logger, createDataAccess());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the direct feature implementation with explicit test dependencies.
        /// </summary>
        /// <returns>The feature data implementation.</returns>
        private static AeDashboardDataAccess createDataAccess()
        {
            #region implementation

            var encryptedIdMapper = new AeDashboardEncryptedIdMapper();
            return new AeDashboardDataAccess(
                new AeDashboardCachePolicy(new PerformanceAppCache()),
                encryptedIdMapper,
                new AeDashboardCorrelationPolicy(),
                new AeDashboardDtoMapper(encryptedIdMapper));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves a repository-relative file from the test output directory.
        /// </summary>
        /// <param name="relativePath">The repository-relative file path.</param>
        /// <returns>The absolute file path.</returns>
        /// <seealso cref="DirectoryInfo"/>
        private static string findRepoFile(string relativePath)
        {
            #region implementation

            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "MedRecPro.sln");
                if (File.Exists(candidate))
                {
                    return Path.Combine(directory.FullName, relativePath);
                }
            }

            throw new DirectoryNotFoundException("Unable to locate the MedRecPro repository root.");

            #endregion
        }
    }
}
