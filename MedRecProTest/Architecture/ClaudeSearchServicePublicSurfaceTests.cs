using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service.LabelQuery;
using MedRecPro.Service.LabelQuery.Implementation;
using MedRecProTest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace MedRecProTest.Architecture
{
    /**************************************************************/
    /// <summary>
    /// Exercises the six planned <see cref="ClaudeSearchService"/> public
    /// methods: class summaries (cache + SQLite-seeded view), class matching,
    /// full user-query search, product extraction, and the indication
    /// reference/matching pair.
    /// </summary>
    /// <remarks>
    /// The AI is faked through a mocked <see cref="IClaudeApiService"/>
    /// resolved via a real service-scope factory (mirroring production
    /// resolution). Keyless view entities cannot be seeded through EF, so
    /// database-backed scenarios use the SQLite backing-table harness from
    /// <see cref="DtoLabelAccessTestHelper"/>. PerformanceHelper's process-wide
    /// managed cache is reset before every test — prompt templates and result
    /// caches otherwise leak across tests.
    /// </remarks>
    /// <seealso cref="ClaudeSearchService"/>
    /// <seealso cref="ExternalServiceTestHarness"/>
    /// <seealso cref="DtoLabelAccessTestHelper"/>
    [TestClass]
    [TestCategory("Architecture")]
    public class ClaudeSearchServicePublicSurfaceTests
    {
        #region implementation

        /// <summary>
        /// Class name used across the pharmacologic-class scenarios.
        /// </summary>
        private const string BetaBlockerClass = "Beta-Adrenergic Blockers [EPC]";

        /**************************************************************/
        /// <summary>
        /// Resets the process-wide managed cache and Util bindings so cached
        /// summaries, prompts, and reference data never leak between tests.
        /// </summary>
        /// <seealso cref="DtoLabelAccessTestHelper.ClearCache"/>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation
            DtoLabelAccessTestHelper.ClearCache();
            #endregion
        }

        #region GetAllClassSummariesAsync

        /**************************************************************/
        /// <summary>
        /// Verifies a warm cache short-circuits the database entirely.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.GetAllClassSummariesAsync"/>
        [TestMethod]
        public async Task GetAllClassSummariesAsync_CacheHit_ReturnsCachedSummaries()
        {
            #region implementation
            // Arrange - empty InMemory DB; only the cache holds a summary.
            var service = createService(createInMemoryContext(), new Mock<IClaudeApiService>().Object);
            var cached = new List<PharmacologicClassSummaryDto> { buildSummaryDto(BetaBlockerClass, 10) };
            PerformanceHelper.SetCacheManageKey("PharmacologicClassSearchService_AllSummaries", cached, 4.0);

            // Act
            var summaries = await service.GetAllClassSummariesAsync();

            // Assert - the cached list came back untouched.
            Assert.AreEqual(1, summaries.Count);
            Assert.AreEqual(BetaBlockerClass, summaries[0].PharmClassName);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies seeded view rows are returned ordered by product count
        /// with zero-product classes filtered out.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.GetAllClassSummariesAsync"/>
        [TestMethod]
        public async Task GetAllClassSummariesAsync_SeededDatabase_ReturnsOrderedSummaries()
        {
            #region implementation
            // Arrange - three view rows; the zero-count row must be filtered.
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            DtoLabelAccessTestHelper.SeedPharmacologicClassSummaryView(connection, BetaBlockerClass, productCount: 10, documentCount: 5);
            DtoLabelAccessTestHelper.SeedPharmacologicClassSummaryView(connection, "Cyclooxygenase Inhibitors", productCount: 25, documentCount: 9);
            DtoLabelAccessTestHelper.SeedPharmacologicClassSummaryView(connection, "Empty Class", productCount: 0, documentCount: 0);

            var service = createService(context, new Mock<IClaudeApiService>().Object);

            // Act
            var summaries = await service.GetAllClassSummariesAsync();

            // Assert - descending by product count, zero-count filtered.
            Assert.AreEqual(2, summaries.Count);
            Assert.AreEqual("Cyclooxygenase Inhibitors", summaries[0].PharmClassName);
            Assert.AreEqual(BetaBlockerClass, summaries[1].PharmClassName);
            Assert.AreEqual(25, summaries[0].ProductCount);
            #endregion
        }

        #endregion

        #region MatchUserQueryToClassesAsync

        /**************************************************************/
        /// <summary>
        /// Verifies the two validation-failure shapes: empty query and empty
        /// class list.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.MatchUserQueryToClassesAsync"/>
        [TestMethod]
        public async Task MatchUserQueryToClassesAsync_EmptyQueryOrNoClasses_ReturnsValidationFailure()
        {
            #region implementation
            // Arrange
            var service = createService(createInMemoryContext(), new Mock<IClaudeApiService>().Object);
            var classes = new List<PharmacologicClassSummaryDto> { buildSummaryDto(BetaBlockerClass, 10) };

            // Act
            var emptyQuery = await service.MatchUserQueryToClassesAsync("  ", classes);
            var noClasses = await service.MatchUserQueryToClassesAsync("beta blockers", new List<PharmacologicClassSummaryDto>());

            // Assert
            Assert.IsFalse(emptyQuery.Success);
            Assert.AreEqual("User query cannot be empty.", emptyQuery.Error);
            Assert.IsFalse(noClasses.Success);
            Assert.AreEqual("No pharmacologic classes available in the database.", noClasses.Error);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the AI matching path parses the fake Claude JSON, validates
        /// names against the available classes, and reports the AI explanation.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.MatchUserQueryToClassesAsync"/>
        [TestMethod]
        public async Task MatchUserQueryToClassesAsync_FakeClaudeResponse_ReturnsMatchedClassNames()
        {
            #region implementation
            // Arrange
            var claudeMock = createClaudeMock(
                "{\"success\":true,\"matchedClassNames\":[\"" + BetaBlockerClass + "\",\"Fabricated Class\"]," +
                "\"explanation\":\"ai-matched\",\"confidence\":\"high\"}");
            var service = createService(createInMemoryContext(), claudeMock.Object);
            var classes = new List<PharmacologicClassSummaryDto> { buildSummaryDto(BetaBlockerClass, 10) };

            // Act
            var result = await service.MatchUserQueryToClassesAsync("beta blockers", classes);

            // Assert - AI path ran (explanation is the AI's, not the fallback's).
            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(new[] { BetaBlockerClass }, result.MatchedClassNames,
                "Fabricated class names must be dropped during validation.");
            Assert.AreEqual("ai-matched", result.Explanation);
            claudeMock.Verify(c => c.GenerateDocumentComparisonAsync(It.IsAny<string>()), Times.Once);
            #endregion
        }

        #endregion

        #region SearchByUserQueryAsync

        /**************************************************************/
        /// <summary>
        /// Verifies an empty query is rejected up front.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.SearchByUserQueryAsync"/>
        [TestMethod]
        public async Task SearchByUserQueryAsync_EmptyQuery_ReturnsValidationFailure()
        {
            #region implementation
            // Arrange
            var service = createService(createInMemoryContext(), new Mock<IClaudeApiService>().Object);

            // Act
            var result = await service.SearchByUserQueryAsync("   ");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Search query cannot be empty.", result.Error);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the no-classes error when the summary view is empty.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.SearchByUserQueryAsync"/>
        [TestMethod]
        public async Task SearchByUserQueryAsync_NoClasses_ReturnsNoClassesError()
        {
            #region implementation
            // Arrange - backing tables exist but hold no rows.
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var service = createService(context, new Mock<IClaudeApiService>().Object);

            // Act
            var result = await service.SearchByUserQueryAsync("beta blockers");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual("No pharmacologic classes found in the database.", result.Error);
            Assert.AreEqual("beta blockers", result.OriginalQuery);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the full search pipeline: seeded summaries, AI class
        /// matching, product lookup, label links, and follow-up suggestions.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.SearchByUserQueryAsync"/>
        [TestMethod]
        public async Task SearchByUserQueryAsync_MatchedClasses_ReturnsProductsAndLabelLinks()
        {
            #region implementation
            // Arrange - one class summary plus one product row in the views.
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            DtoLabelAccessTestHelper.SeedPharmacologicClassSummaryView(connection, BetaBlockerClass, productCount: 10, documentCount: 5);
            DtoLabelAccessTestHelper.SeedProductsByPharmacologicClassView(connection,
                pharmClassName: BetaBlockerClass,
                productId: 1,
                productName: "METOPROLOL TARTRATE",
                documentId: 1,
                documentGuid: DtoLabelAccessTestHelper.TestDocumentGuid);

            var claudeMock = createClaudeMock(
                "{\"success\":true,\"matchedClassNames\":[\"" + BetaBlockerClass + "\"],\"explanation\":\"matched\",\"confidence\":\"high\"}");
            var service = createService(context, claudeMock.Object);

            // Act
            var result = await service.SearchByUserQueryAsync("beta blockers");

            // Assert - matched class carries the seeded product.
            Assert.IsTrue(result.Success, result.Error);
            CollectionAssert.AreEqual(new[] { BetaBlockerClass }, result.MatchedClasses);
            Assert.AreEqual(1, result.TotalProductCount);

            var products = result.ProductsByClass[BetaBlockerClass];
            Assert.AreEqual("METOPROLOL TARTRATE", products[0].ProductName);
            Assert.AreEqual(BetaBlockerClass, products[0].PharmClassName);
            Assert.AreEqual("11111111-1111-1111-1111-111111111111", products[0].DocumentGuid);

            // Label link and product-aware follow-up.
            Assert.AreEqual("/api/Label/original/11111111-1111-1111-1111-111111111111/true",
                result.LabelLinks["View Full Label (METOPROLOL TARTRATE)"]);
            Assert.IsNotNull(result.SuggestedFollowUps);
            Assert.AreEqual("Tell me about the side effects of METOPROLOL TARTRATE", result.SuggestedFollowUps![0]);
            #endregion
        }

        #endregion

        #region ExtractProductFromDescriptionAsync

        /**************************************************************/
        /// <summary>
        /// Verifies an empty description is rejected.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.ExtractProductFromDescriptionAsync"/>
        [TestMethod]
        public async Task ExtractProductFromDescriptionAsync_EmptyDescription_ReturnsValidationFailure()
        {
            #region implementation
            // Arrange
            var service = createService(createInMemoryContext(), new Mock<IClaudeApiService>().Object);

            // Act
            var result = await service.ExtractProductFromDescriptionAsync("  ");

            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Description cannot be empty.", result.Error);
            Assert.AreEqual("low", result.Confidence);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the AI extraction path maps product names, confidence, and
        /// brand-mapping metadata from the fake Claude JSON.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.ExtractProductFromDescriptionAsync"/>
        [TestMethod]
        public async Task ExtractProductFromDescriptionAsync_FakeClaudeResponse_ReturnsProductNames()
        {
            #region implementation
            // Arrange
            var claudeMock = createClaudeMock(
                "{\"success\":true,\"productNames\":[\"finerenone\"],\"confidence\":\"high\"," +
                "\"explanation\":\"brand mapped\",\"brandMappingApplied\":true,\"originalBrandName\":\"Kerendia\"}");
            var service = createService(createInMemoryContext(), claudeMock.Object);

            // Act
            var result = await service.ExtractProductFromDescriptionAsync("search for Kerendia products");

            // Assert
            Assert.IsTrue(result.Success);
            CollectionAssert.AreEqual(new[] { "finerenone" }, result.ProductNames);
            Assert.AreEqual("finerenone", result.PrimaryProductName);
            Assert.AreEqual("high", result.Confidence);
            Assert.IsTrue(result.BrandMappingApplied);
            Assert.AreEqual("Kerendia", result.OriginalBrandName);
            claudeMock.Verify(c => c.GenerateDocumentComparisonAsync(It.IsAny<string>()), Times.Once);
            #endregion
        }

        #endregion

        #region GetIndicationReferenceDataAsync

        /**************************************************************/
        /// <summary>
        /// Verifies a warm cache returns the entries without touching the
        /// reference file.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.GetIndicationReferenceDataAsync"/>
        [TestMethod]
        public async Task GetIndicationReferenceDataAsync_CacheHit_ReturnsCachedEntries()
        {
            #region implementation
            // Arrange
            var service = createService(createInMemoryContext(), new Mock<IClaudeApiService>().Object);
            var cached = new List<IndicationReferenceEntry>
            {
                new IndicationReferenceEntry
                {
                    UNII = "B025Y34C54",
                    ProductNames = new List<string> { "Acebutolol Hydrochloride" },
                    IndicationsSummary = "For hypertension"
                }
            };
            PerformanceHelper.SetCacheManageKey("ClaudeSearchService_IndicationReference", cached, 8.0);

            // Act
            var entries = await service.GetIndicationReferenceDataAsync();

            // Assert
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("B025Y34C54", entries[0].UNII);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a missing reference file yields an empty list rather than
        /// an exception.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.GetIndicationReferenceDataAsync"/>
        [TestMethod]
        public async Task GetIndicationReferenceDataAsync_NoReferenceFile_ReturnsEmptyList()
        {
            #region implementation
            // Arrange - configuration points at a file that does not exist.
            var service = createService(
                createInMemoryContext(),
                new Mock<IClaudeApiService>().Object,
                new Dictionary<string, string?>
                {
                    ["ClaudeApiSettings:Skill-LabelProductIndication"] = "Skills/does-not-exist-fixture.md"
                });

            // Act
            var entries = await service.GetIndicationReferenceDataAsync();

            // Assert
            Assert.IsNotNull(entries);
            Assert.AreEqual(0, entries.Count);
            #endregion
        }

        #endregion

        #region MatchUserQueryToIndicationsAsync

        /**************************************************************/
        /// <summary>
        /// Verifies the validation failures for empty query and missing
        /// candidates.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.MatchUserQueryToIndicationsAsync"/>
        [TestMethod]
        public async Task MatchUserQueryToIndicationsAsync_EmptyOrNoCandidates_ReturnsValidationFailure()
        {
            #region implementation
            // Arrange
            var service = createService(createInMemoryContext(), new Mock<IClaudeApiService>().Object);
            var candidates = new List<IndicationReferenceEntry> { buildCandidate() };

            // Act
            var emptyQuery = await service.MatchUserQueryToIndicationsAsync("  ", candidates);
            var noCandidates = await service.MatchUserQueryToIndicationsAsync("hypertension", new List<IndicationReferenceEntry>());

            // Assert
            Assert.IsFalse(emptyQuery.Success);
            Assert.AreEqual("User query cannot be empty.", emptyQuery.Error);
            Assert.IsFalse(noCandidates.Success);
            Assert.AreEqual("No candidate indications available for matching.", noCandidates.Error);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the AI indication-matching path keeps only candidate UNIIs
        /// and maps the match metadata.
        /// </summary>
        /// <seealso cref="ClaudeSearchService.MatchUserQueryToIndicationsAsync"/>
        [TestMethod]
        public async Task MatchUserQueryToIndicationsAsync_FakeClaudeResponse_ReturnsMatches()
        {
            #region implementation
            // Arrange - one real candidate UNII plus one fabricated by the AI.
            var claudeMock = createClaudeMock(
                "{\"success\":true,\"matchedIndications\":[" +
                "{\"unii\":\"B025Y34C54\",\"productNames\":\"Acebutolol Hydrochloride\"," +
                "\"relevanceReason\":\"treats hypertension\",\"confidence\":\"high\"}," +
                "{\"unii\":\"FAKE999999\",\"productNames\":\"Made Up\",\"relevanceReason\":\"x\",\"confidence\":\"low\"}]," +
                "\"explanation\":\"matched indications\",\"confidence\":\"high\"}");
            var service = createService(createInMemoryContext(), claudeMock.Object);
            var candidates = new List<IndicationReferenceEntry> { buildCandidate() };

            // Act
            var result = await service.MatchUserQueryToIndicationsAsync("blood pressure medication", candidates);

            // Assert - fabricated UNII dropped, real match mapped fully.
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.MatchedIndications.Count);
            Assert.AreEqual("B025Y34C54", result.MatchedIndications[0].UNII);
            Assert.AreEqual("treats hypertension", result.MatchedIndications[0].RelevanceReason);
            Assert.AreEqual("high", result.MatchedIndications[0].Confidence);
            claudeMock.Verify(c => c.GenerateDocumentComparisonAsync(It.IsAny<string>()), Times.Once);
            #endregion
        }

        #endregion

        #region Harness helpers

        /**************************************************************/
        /// <summary>
        /// Creates a uniquely named EF InMemory context for scenarios that do
        /// not query the label views.
        /// </summary>
        /// <returns>A fresh context.</returns>
        private static ApplicationDbContext createInMemoryContext()
        {
            #region implementation
            return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"ClaudeSearchTests_{Guid.NewGuid():N}")
                .Options);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates the search service under test with the shared PK secret,
        /// real prompt-template paths (present in the test output under
        /// Skills/), and a scope factory resolving the supplied AI service.
        /// </summary>
        /// <param name="context">Backing application context.</param>
        /// <param name="claudeApiService">Fake or mocked AI service.</param>
        /// <param name="configOverrides">Optional configuration overrides.</param>
        /// <returns>The service under test.</returns>
        /// <seealso cref="ExternalServiceTestHarness.CreateScopeFactoryFor"/>
        private static ClaudeSearchService createService(
            ApplicationDbContext context,
            IClaudeApiService claudeApiService,
            Dictionary<string, string?>? configOverrides = null)
        {
            #region implementation
            var values = new Dictionary<string, string?>
            {
                ["Security:DB:PKSecret"] = DtoLabelAccessTestHelper.TestPkSecret,
                ["ClaudeApiSettings:Prompt-PharmacologicClassMatching"] = "Skills/prompts/pharmacologic-class-matching-prompt.md",
                ["ClaudeApiSettings:Prompt-ProductExtraction"] = "Skills/prompts/product-extraction-prompt.md",
                ["ClaudeApiSettings:Prompt-IndicationMatching"] = "Skills/prompts/indication-matching-prompt.md",
                ["ClaudeApiSettings:Skill-LabelProductIndication"] = "Skills/labelProductIndication.md"
            };

            if (configOverrides != null)
            {
                foreach (var pair in configOverrides)
                {
                    values[pair.Key] = pair.Value;
                }
            }

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            var query = DtoLabelAccessTestHelper.CreateLabelQueryDataAccess();
            var pharmacologicClassSearchService = new PharmacologicClassSearchService(
                context,
                configuration,
                query,
                NullLogger<PharmacologicClassSearchService>.Instance);
            var productSearchService = new ProductSearchService(
                context,
                configuration,
                query,
                NullLogger<ProductSearchService>.Instance);
            var labelMarkdownService = new LabelMarkdownService(
                context,
                configuration,
                query,
                NullLogger<LabelMarkdownService>.Instance);

            return new ClaudeSearchService(
                context,
                configuration,
                NullLogger<ClaudeSearchService>.Instance,
                ExternalServiceTestHarness.CreateScopeFactoryFor(claudeApiService),
                pharmacologicClassSearchService,
                productSearchService,
                labelMarkdownService);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a mocked AI service whose comparison call returns the
        /// supplied response text.
        /// </summary>
        /// <param name="responseText">Text the fake Claude returns.</param>
        /// <returns>The configured mock.</returns>
        private static Mock<IClaudeApiService> createClaudeMock(string responseText)
        {
            #region implementation
            var mock = new Mock<IClaudeApiService>();
            mock.Setup(c => c.GenerateDocumentComparisonAsync(It.IsAny<string>()))
                .ReturnsAsync(responseText);

            return mock;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a hand-made class summary DTO (no database required).
        /// </summary>
        /// <param name="className">Pharmacologic class name.</param>
        /// <param name="productCount">Product count exposed by the DTO.</param>
        /// <returns>The summary DTO.</returns>
        private static PharmacologicClassSummaryDto buildSummaryDto(string className, int productCount)
        {
            #region implementation
            return new PharmacologicClassSummaryDto
            {
                PharmacologicClassSummary = new Dictionary<string, object?>
                {
                    ["PharmClassName"] = className,
                    ["ProductCount"] = (object)productCount
                }
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the canonical indication candidate used across tests.
        /// </summary>
        /// <returns>An acebutolol indication reference entry.</returns>
        private static IndicationReferenceEntry buildCandidate()
        {
            #region implementation
            return new IndicationReferenceEntry
            {
                UNII = "B025Y34C54",
                ProductNames = new List<string> { "Acebutolol Hydrochloride" },
                IndicationsSummary = "For the management of hypertension."
            };
            #endregion
        }

        #endregion

        #endregion
    }
}
