using MedRecPro.Models;
using MedRecProTest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using RazorLight;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Model consumed by the RazorLight test template. Public so the
    /// dynamically compiled template assembly can bind to it.
    /// </summary>
    /// <seealso cref="TemplateRenderingService"/>
    public sealed class SplTemplateFixtureModel
    {
        /// <summary>
        /// Value the test template renders.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    /**************************************************************/
    /// <summary>
    /// Exercises the SPL export pipeline services from SplExportService.cs:
    /// <see cref="DocumentDataService.GetDocumentAsync"/>,
    /// <see cref="TemplateRenderingService.RenderAsync"/> (plus Dispose), and
    /// <see cref="SplExportService.ExportDocumentToSplAsync"/>.
    /// </summary>
    /// <remarks>
    /// DocumentDataService runs against real SQLite-seeded Label data.
    /// TemplateRenderingService binds RazorLight to
    /// Directory.GetCurrentDirectory()/Views/SplTemplates at construction, so
    /// those tests swap the current directory to a disposable temp root and
    /// restore it in a finally block. The export orchestrator is exercised
    /// with mocked collaborators because its full pipeline spans eleven
    /// rendering services.
    /// </remarks>
    /// <seealso cref="DocumentDataService"/>
    /// <seealso cref="TemplateRenderingService"/>
    /// <seealso cref="SplExportService"/>
    /// <seealso cref="DtoLabelAccessTestHelper"/>
    [TestClass]
    public class SplExportServiceTests
    {
        #region implementation

        /// <summary>
        /// Fixed PK secret shared with <see cref="DtoLabelAccessTestHelper"/>.
        /// </summary>
        private const string TestPkSecret = DtoLabelAccessTestHelper.TestPkSecret;

        /**************************************************************/
        /// <summary>
        /// Clears the process-wide managed cache (DtoLabelAccess caches
        /// documents by GUID) and rebinds Util statics before each test.
        /// </summary>
        /// <seealso cref="DtoLabelAccessTestHelper.ClearCache"/>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation
            DtoLabelAccessTestHelper.ClearCache();
            #endregion
        }

        #region DocumentDataService

        /**************************************************************/
        /// <summary>
        /// Verifies GetDocumentAsync returns the seeded document DTO for a
        /// known GUID.
        /// </summary>
        /// <seealso cref="DocumentDataService.GetDocumentAsync"/>
        [TestMethod]
        public async Task DocumentDataService_GetDocumentAsync_SeededGuid_ReturnsDocumentDto()
        {
            #region implementation
            // Arrange
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            await DtoLabelAccessTestHelper.SeedDocumentAsync(
                context, DtoLabelAccessTestHelper.TestDocumentGuid, DtoLabelAccessTestHelper.TestSetGuid, "Export Fixture");
            var service = new DocumentDataService(context, createConfiguration(), DtoLabelAccessTestHelper.CreateTestLogger());

            // Act
            var document = await service.GetDocumentAsync(DtoLabelAccessTestHelper.TestDocumentGuid);

            // Assert
            Assert.IsNotNull(document);
            Assert.AreEqual(DtoLabelAccessTestHelper.TestDocumentGuid, document!.DocumentGUID);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetDocumentAsync returns null for an unknown GUID.
        /// </summary>
        /// <seealso cref="DocumentDataService.GetDocumentAsync"/>
        [TestMethod]
        public async Task DocumentDataService_GetDocumentAsync_UnknownGuid_ReturnsNull()
        {
            #region implementation
            // Arrange - empty database.
            var (sentinel, connection) = DtoLabelAccessTestHelper.CreateSharedMemoryDb();
            using var _sentinel = sentinel; using var _connection = connection;
            using var context = DtoLabelAccessTestHelper.CreateTestContext(connection);
            var service = new DocumentDataService(context, createConfiguration(), DtoLabelAccessTestHelper.CreateTestLogger());

            // Act
            var document = await service.GetDocumentAsync(Guid.NewGuid());

            // Assert
            Assert.IsNull(document);
            #endregion
        }

        #endregion

        #region TemplateRenderingService

        /**************************************************************/
        /// <summary>
        /// Verifies RenderAsync compiles and renders a minimal template with
        /// the supplied model value.
        /// </summary>
        /// <seealso cref="TemplateRenderingService.RenderAsync"/>
        [TestMethod]
        public async Task TemplateRenderingService_RenderAsync_MinimalTemplate_RendersModelValue()
        {
            #region implementation
            // Arrange + Act
            var rendered = await withTemplateDirectoryAsync(async service =>
                await service.RenderAsync("TestTemplate", new SplTemplateFixtureModel { Name = "Fixture" }));

            // Assert
            Assert.AreEqual("Hello Fixture!", rendered.Trim());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies RenderAsync throws for a missing template and logs the
        /// failure at Error level before rethrowing.
        /// </summary>
        /// <seealso cref="TemplateRenderingService.RenderAsync"/>
        [TestMethod]
        public async Task TemplateRenderingService_RenderAsync_MissingTemplate_ThrowsAndLogs()
        {
            #region implementation
            // Arrange
            var logger = new Mock<ILogger>();

            // Act + Assert - RazorLight reports the unknown key.
            await withTemplateDirectoryAsync<object?>(async service =>
            {
                await Assert.ThrowsExceptionAsync<TemplateNotFoundException>(
                    () => service.RenderAsync("NoSuchTemplate", new SplTemplateFixtureModel { Name = "x" }));
                return null;
            }, logger);

            // The failure must be logged at Error level before the rethrow.
            logger.Verify(l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, type) => true),
                    It.IsAny<Exception?>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.AtLeastOnce);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies Dispose is safe to call (including repeatedly) as the
        /// service participates in using statements and DI disposal.
        /// </summary>
        /// <seealso cref="TemplateRenderingService.Dispose"/>
        [TestMethod]
        public async Task TemplateRenderingService_Dispose_IsSafeToCallRepeatedly()
        {
            #region implementation
            // Arrange + Act - construct inside the template directory, then
            // dispose twice; neither call may throw.
            await withTemplateDirectoryAsync<object?>(service =>
            {
                service.Dispose();
                service.Dispose();
                return Task.FromResult<object?>(null);
            });

            // Assert - reaching this point without exceptions is the contract.
            Assert.IsTrue(true);
            #endregion
        }

        #endregion

        #region SplExportService

        /**************************************************************/
        /// <summary>
        /// Verifies ExportDocumentToSplAsync throws InvalidOperationException
        /// when the document does not exist.
        /// </summary>
        /// <seealso cref="SplExportService.ExportDocumentToSplAsync"/>
        [TestMethod]
        public async Task SplExportService_ExportDocumentToSplAsync_DocumentNotFound_ThrowsInvalidOperation()
        {
            #region implementation
            // Arrange
            var harness = new ExportHarness();
            harness.DocumentDataService
                .Setup(s => s.GetDocumentAsync(It.IsAny<Guid>()))
                .ReturnsAsync((DocumentDto?)null);
            var documentGuid = Guid.NewGuid();

            // Act + Assert
            var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => harness.Service.ExportDocumentToSplAsync(documentGuid, minify: false));

            StringAssert.Contains(exception.Message, documentGuid.ToString());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the orchestrated export path renders the GenerateSpl
        /// template with the prepared document rendering context.
        /// </summary>
        /// <seealso cref="SplExportService.ExportDocumentToSplAsync"/>
        [TestMethod]
        public async Task SplExportService_ExportDocumentToSplAsync_MockedPipeline_ReturnsRenderedXml()
        {
            #region implementation
            // Arrange - found document with no authors or structured bodies,
            // so the pipeline reduces to prepare + render.
            var harness = new ExportHarness();
            var documentGuid = Guid.NewGuid();
            harness.DocumentDataService
                .Setup(s => s.GetDocumentAsync(documentGuid))
                .ReturnsAsync(new DocumentDto
                {
                    Document = new Dictionary<string, object?> { ["DocumentGUID"] = documentGuid }
                });
            harness.TemplateRenderingService
                .Setup(t => t.RenderAsync("GenerateSpl", It.IsAny<DocumentRendering>()))
                .ReturnsAsync("<spl/>");

            // Act
            var xml = await harness.Service.ExportDocumentToSplAsync(documentGuid, minify: false);

            // Assert
            Assert.AreEqual("<spl/>", xml);
            harness.DocumentRenderingService.Verify(s => s.PrepareForRendering(It.IsAny<DocumentDto>()), Times.Once);
            harness.TemplateRenderingService.Verify(t => t.RenderAsync("GenerateSpl", It.IsAny<DocumentRendering>()), Times.Once);
            #endregion
        }

        #endregion

        #region Harness helpers

        /**************************************************************/
        /// <summary>
        /// Builds the in-memory configuration carrying the shared PK secret
        /// and sequential loading mode.
        /// </summary>
        /// <returns>Configuration for DocumentDataService.</returns>
        private static IConfiguration createConfiguration()
        {
            #region implementation
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:DB:PKSecret"] = TestPkSecret,
                    ["FeatureFlags:UseBatchDocumentLoading"] = "false"
                })
                .Build();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Runs a callback against a <see cref="TemplateRenderingService"/>
        /// constructed inside a disposable temp directory that contains
        /// Views/SplTemplates/TestTemplate.cshtml, restoring the original
        /// current directory afterwards.
        /// </summary>
        /// <typeparam name="TResult">Callback result type.</typeparam>
        /// <param name="action">Callback receiving the constructed service.</param>
        /// <param name="logger">Optional logger mock to observe error logging.</param>
        /// <returns>The callback result.</returns>
        /// <remarks>
        /// The service captures Directory.GetCurrentDirectory() at
        /// construction, so the swap only needs to span the constructor call;
        /// it is restored in a finally block to keep other tests unaffected.
        /// </remarks>
        /// <seealso cref="TemplateRenderingService"/>
        private static async Task<TResult> withTemplateDirectoryAsync<TResult>(
            Func<TemplateRenderingService, Task<TResult>> action,
            Mock<ILogger>? logger = null)
        {
            #region implementation
            var tempRoot = Path.Combine(Path.GetTempPath(), $"MedRecProSplTemplates_{Guid.NewGuid():N}");
            var templateDirectory = Path.Combine(tempRoot, "Views", "SplTemplates");
            Directory.CreateDirectory(templateDirectory);
            File.WriteAllText(Path.Combine(templateDirectory, "TestTemplate.cshtml"), "Hello @Model.Name!");

            var originalDirectory = Directory.GetCurrentDirectory();
            TemplateRenderingService service;

            try
            {
                // The constructor binds RazorLight to cwd/Views/SplTemplates.
                Directory.SetCurrentDirectory(tempRoot);
                service = new TemplateRenderingService((logger ?? new Mock<ILogger>()).Object);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
            }

            try
            {
                return await action(service);
            }
            finally
            {
                service.Dispose();

                try
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup; temp roots are unique per test run.
                }
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Bundles the eleven mocked collaborators required by
        /// <see cref="SplExportService"/> and exposes the ones tests assert on.
        /// </summary>
        /// <seealso cref="SplExportService"/>
        private sealed class ExportHarness
        {
            #region implementation

            /**************************************************************/
            /// <summary>Document data source mock.</summary>
            public Mock<IDocumentDataService> DocumentDataService { get; } = new();

            /**************************************************************/
            /// <summary>Document rendering preparation mock.</summary>
            public Mock<IDocumentRenderingService> DocumentRenderingService { get; } = new();

            /**************************************************************/
            /// <summary>Template rendering mock.</summary>
            public Mock<ITemplateRenderingService> TemplateRenderingService { get; } = new();

            /**************************************************************/
            /// <summary>Service under test.</summary>
            public SplExportService Service { get; }

            /**************************************************************/
            /// <summary>
            /// Constructs the service with loose mocks for every collaborator.
            /// </summary>
            public ExportHarness()
            {
                #region implementation
                Service = new SplExportService(
                    DocumentDataService.Object,
                    DocumentRenderingService.Object,
                    TemplateRenderingService.Object,
                    new Mock<IStructuredBodyViewModelFactory>().Object,
                    new Mock<ISectionRenderingService>().Object,
                    new Mock<IProductRenderingService>().Object,
                    new Mock<IIngredientRenderingService>().Object,
                    new Mock<IPackageRenderingService>().Object,
                    new Mock<ITextContentRenderingService>().Object,
                    new Mock<ICharacteristicRenderingService>().Object,
                    new Mock<IAuthorRenderingService>().Object,
                    new Mock<ILogger>().Object);
                #endregion
            }

            #endregion
        }

        #endregion

        #endregion
    }
}
