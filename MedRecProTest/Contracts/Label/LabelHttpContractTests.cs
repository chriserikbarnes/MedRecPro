using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Models.Extensions;
using MedRecProTest.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using System.Text.Json;
using ImportRuntimeOperationStatus = MedRecProImportClass.Models.ImportOperationStatus;

namespace MedRecProTest.Contracts.Label;

/**************************************************************/
/// <summary>
/// Exercises representative Label HTTP contracts through the real host pipeline.
/// </summary>
/// <remarks>
/// Data is seeded through the SQLite fixture boundary, while every assertion enters through HTTP
/// so routing, binding, authorization, filters, serialization, and response headers remain covered.
/// The class remains non-parallel because it shares one seeded SQLite host fixture across all cases.
/// </remarks>
/// <seealso cref="MedRecProWebApplicationFactory"/>
[TestClass]
[TestCategory("Contract")]
[DoNotParallelize]
public class LabelHttpContractTests
{
    #region implementation

#if DEBUG
    private const string LabelRoutePrefix = "/api/Label";
#else
    private const string LabelRoutePrefix = "/Label";
#endif

    private static readonly Guid seededDocumentGuid = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid seededSetGuid = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private const long SeededAdminUserId = 7777L;

    /**************************************************************/
    /// <summary>
    /// Seeds deterministic entity and view data once for all HTTP contract tests in this class.
    /// </summary>
    /// <param name="testContext">MSTest context supplied for class initialization.</param>
    /// <seealso cref="DtoLabelAccessTestHelper.SeedFullDocumentHierarchyAsync"/>
    [ClassInitialize]
    public static void SeedHostData(TestContext testContext)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(testContext);

        MedRecProHostFixture.Factory.SeedApplicationDatabaseAsync(async context =>
        {
            var hierarchy = await DtoLabelAccessTestHelper.SeedFullDocumentHierarchyAsync(
                context,
                seededDocumentGuid,
                seededSetGuid,
                "HOST ASPIRIN");
            context.AppUsers.Add(new User
            {
                Id = SeededAdminUserId,
                PrimaryEmail = "label.contract.admin@example.com",
                UserName = "label.contract.admin@example.com",
                NormalizedUserName = "LABEL.CONTRACT.ADMIN@EXAMPLE.COM",
                Email = "label.contract.admin@example.com",
                NormalizedEmail = "LABEL.CONTRACT.ADMIN@EXAMPLE.COM",
                DisplayName = "Label Contract Admin",
                CanonicalUsername = "label.contract.admin",
                UserRole = "Admin",
                Timezone = "UTC",
                Locale = "en-US",
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString()
            });
            await context.SaveChangesAsync();
            var connection = (SqliteConnection)context.Database.GetDbConnection();

            DtoLabelAccessTestHelper.SeedProductSummaryView(
                connection,
                productName: "HOST ASPIRIN",
                productId: hierarchy.ProductID,
                documentId: hierarchy.DocumentID,
                documentGuid: seededDocumentGuid,
                setGuid: seededSetGuid);
            DtoLabelAccessTestHelper.SeedProductsByApplicationNumberView(
                connection,
                applicationNumber: "HOSTNDA001",
                productName: "HOST APPLICATION PRODUCT",
                productId: hierarchy.ProductID,
                documentId: hierarchy.DocumentID,
                documentGuid: seededDocumentGuid,
                setGuid: seededSetGuid);
            DtoLabelAccessTestHelper.SeedProductsByIngredientView(
                connection,
                substanceName: "HOST INGREDIENT",
                unii: "R16CO5Y76E",
                productId: hierarchy.ProductID,
                productName: "HOST INGREDIENT PRODUCT",
                documentId: hierarchy.DocumentID,
                documentGuid: seededDocumentGuid,
                setGuid: seededSetGuid);
            DtoLabelAccessTestHelper.SeedProductsByNDCView(
                connection,
                productCode: "99999-111",
                productName: "HOST NDC PRODUCT",
                productId: hierarchy.ProductID,
                documentId: hierarchy.DocumentID,
                documentGuid: seededDocumentGuid,
                setGuid: seededSetGuid);
            DtoLabelAccessTestHelper.SeedSectionNavigationView(
                connection,
                sectionCode: "99999-9",
                sectionType: "HOST SECTION",
                documentId: hierarchy.DocumentID,
                documentGuid: seededDocumentGuid,
                setGuid: seededSetGuid);
            DtoLabelAccessTestHelper.SeedDEAScheduleLookupView(
                connection,
                deaScheduleCode: "CII",
                productName: "HOST DEA PRODUCT",
                documentId: hierarchy.DocumentID,
                documentGuid: seededDocumentGuid,
                setGuid: seededSetGuid);
            DtoLabelAccessTestHelper.SeedAPIEndpointGuideView(
                connection,
                viewName: "HOST API GUIDE",
                endpointName: "HostGuide",
                category: "Navigation");
            DtoLabelAccessTestHelper.SeedLabelSectionMarkdownView(
                connection,
                documentGuid: seededDocumentGuid,
                setGuid: seededSetGuid,
                documentTitle: "HOST ASPIRIN",
                sectionCode: "34067-9",
                sectionTitle: "INDICATIONS AND USAGE",
                fullSectionText: "## INDICATIONS AND USAGE\nHost contract markdown content.",
                contentBlockCount: 1);
        }).GetAwaiter().GetResult();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies paged product search returns its DTO payload and pagination headers through MVC.
    /// </summary>
    /// <returns>A task representing the asynchronous HTTP assertion.</returns>
    /// <seealso cref="MedRecPro.DataAccess.DtoLabelAccess.SearchProductSummaryAsync"/>
    [TestMethod]
    public async Task ProductSearch_SeededProduct_ReturnsDtoAndPaginationHeaders()
    {
        #region implementation

        using var client = createClient();
        var response = await client.GetAsync(
            $"{LabelRoutePrefix}/product/search?productNameSearch=HOST%20ASPIRIN&pageNumber=1&pageSize=10");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("1", response.Headers.GetValues("X-Page-Number").Single());
        Assert.AreEqual("10", response.Headers.GetValues("X-Page-Size").Single());
        Assert.AreEqual("1", response.Headers.GetValues("X-Total-Count").Single());
        StringAssert.Contains(payload, "HOST ASPIRIN");
        StringAssert.Contains(payload, "productSummary");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies every extracted Label search family returns its seeded result through the real HTTP pipeline.
    /// </summary>
    /// <returns>A task representing the asynchronous HTTP assertions.</returns>
    /// <remarks>
    /// The endpoints deliberately cover the seven Phase 2 controller families without depending on optional AI services.
    /// </remarks>
    /// <seealso cref="LabelApplicationController"/>
    /// <seealso cref="LabelClassificationController"/>
    /// <seealso cref="LabelIngredientController"/>
    /// <seealso cref="LabelProductIdentifierController"/>
    /// <seealso cref="LabelSectionNavigationController"/>
    /// <seealso cref="LabelProductSearchController"/>
    /// <seealso cref="LabelMetadataController"/>
    [TestMethod]
    public async Task SearchFamilies_SeededRows_RemainReachableThroughHttp()
    {
        #region implementation

        var endpointAssertions = new (string RequestUri, string ExpectedContent)[]
        {
            ($"{LabelRoutePrefix}/application-number/search?applicationNumber=HOSTNDA001&pageNumber=1&pageSize=10", "HOST APPLICATION PRODUCT"),
            ($"{LabelRoutePrefix}/drug-safety/dea-schedule?scheduleCode=CII&pageNumber=1&pageSize=10", "HOST DEA PRODUCT"),
            ($"{LabelRoutePrefix}/ingredient/search?unii=R16CO5Y76E&pageNumber=1&pageSize=10", "HOST INGREDIENT PRODUCT"),
            ($"{LabelRoutePrefix}/ndc/search?productCode=99999-111&pageNumber=1&pageSize=10", "HOST NDC PRODUCT"),
            ($"{LabelRoutePrefix}/section/search?sectionCode=99999-9&pageNumber=1&pageSize=10", "HOST SECTION"),
            ($"{LabelRoutePrefix}/product/search?productNameSearch=HOST%20ASPIRIN&pageNumber=1&pageSize=10", "HOST ASPIRIN"),
            ($"{LabelRoutePrefix}/guide?category=Navigation", "HOST API GUIDE")
        };

        using var client = createClient();

        foreach (var endpoint in endpointAssertions)
        {
            using var response = await client.GetAsync(endpoint.RequestUri);
            var payload = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode, endpoint.RequestUri);
            StringAssert.Contains(payload, endpoint.ExpectedContent, endpoint.RequestUri);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies extracted search families preserve invalid-input HTTP boundaries.
    /// </summary>
    /// <returns>A task representing the asynchronous bad-request assertions.</returns>
    /// <seealso cref="SearchFamilies_SeededRows_RemainReachableThroughHttp"/>
    [TestMethod]
    public async Task SearchFamilies_InvalidInputs_KeepBadRequestBoundary()
    {
        #region implementation

        var invalidRequests = new[]
        {
            $"{LabelRoutePrefix}/application-number/search",
            $"{LabelRoutePrefix}/drug-safety/dea-schedule?pageNumber=0&pageSize=10",
            $"{LabelRoutePrefix}/ingredient/search",
            $"{LabelRoutePrefix}/ndc/search",
            $"{LabelRoutePrefix}/section/search",
            $"{LabelRoutePrefix}/product/search"
        };

        using var client = createClient();

        foreach (var requestUri in invalidRequests)
        {
            using var response = await client.GetAsync(requestUri);

            Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode, requestUri);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies malformed encrypted section identifiers preserve the established read, update, and delete boundaries.
    /// </summary>
    /// <returns>A task representing the asynchronous HTTP assertions.</returns>
    /// <remarks>
    /// Read continues to treat an undecryptable identifier as an unknown record, while protected mutations reject it
    /// as invalid input before the repository performs any database mutation.
    /// </remarks>
    /// <seealso cref="MedRecPro.Api.Controllers.LabelSectionController.GetByIdAsync"/>
    /// <seealso cref="MedRecPro.Api.Controllers.LabelSectionController.UpdateAsync"/>
    /// <seealso cref="MedRecPro.Api.Controllers.LabelSectionController.DeleteAsync"/>
    [TestMethod]
    public async Task SectionCrud_MalformedEncryptedId_PreservesReadAndMutationStatusCodes()
    {
        #region implementation

        const string malformedEncryptedId = "not-an-encrypted-id";
        using var client = createClient();
        client.DefaultRequestHeaders.Add("X-Test-User", SeededAdminUserId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using var readResponse = await client.GetAsync(
            $"{LabelRoutePrefix}/Document/{malformedEncryptedId}");
        using var updateResponse = await client.PutAsync(
            $"{LabelRoutePrefix}/Document/{malformedEncryptedId}",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        using var deleteResponse = await client.DeleteAsync(
            $"{LabelRoutePrefix}/Document/{malformedEncryptedId}");

        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, readResponse.StatusCode);
        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, updateResponse.StatusCode);
        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, deleteResponse.StatusCode);
        StringAssert.Contains(
            await deleteResponse.Content.ReadAsStringAsync(),
            "Invalid encrypted ID format for section Document.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies a model-bound empty GUID receives the established Label bad-request response.
    /// </summary>
    /// <returns>A task representing the asynchronous HTTP assertion.</returns>
    /// <seealso cref="MedRecPro.Controllers.LabelDocumentController.GetSingleCompleteLabel"/>
    [TestMethod]
    public async Task DocumentLookup_EmptyGuid_ReturnsBadRequest()
    {
        #region implementation

        using var client = createClient();
        var response = await client.GetAsync($"{LabelRoutePrefix}/single/{Guid.Empty:D}");

        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies a valid but unknown document receives the established Label not-found response.
    /// </summary>
    /// <returns>A task representing the asynchronous HTTP assertion.</returns>
    /// <seealso cref="MedRecPro.Controllers.LabelDocumentController.GetSingleCompleteLabel"/>
    [TestMethod]
    public async Task DocumentLookup_UnknownGuid_ReturnsNotFound()
    {
        #region implementation

        using var client = createClient();
        var response = await client.GetAsync(
            $"{LabelRoutePrefix}/single/{Guid.Parse("66666666-6666-6666-6666-666666666666"):D}");

        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies an anonymous request cannot invoke the protected comparison queue mutation.
    /// </summary>
    /// <returns>A task representing the asynchronous HTTP assertion.</returns>
    /// <seealso cref="MedRecPro.Controllers.LabelComparisonController.QueueDocumentComparisonAnalysis"/>
    [TestMethod]
    public async Task ComparisonQueue_AnonymousRequest_ReturnsUnauthorized()
    {
        #region implementation

        using var client = createClient();
        var response = await client.PostAsync(
            $"{LabelRoutePrefix}/comparison/analysis/{seededDocumentGuid:D}",
            content: null);

        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies comparison and import progress endpoints return their typed status payloads.
    /// </summary>
    /// <returns>A task representing the asynchronous HTTP assertions.</returns>
    /// <seealso cref="MedRecPro.Api.Controllers.LabelComparisonController.GetComparisonProgress"/>
    /// <seealso cref="MedRecPro.Api.Controllers.LabelImportController.GetImportProgress"/>
    [TestMethod]
    public async Task ProgressEndpoints_KnownOperationIds_ReturnTypedStatusPayloads()
    {
        #region implementation

        var comparisonOperationId = $"comparison-{Guid.NewGuid():N}";
        var importOperationId = $"import-{Guid.NewGuid():N}";
        var comparisonStore = MedRecProHostFixture.Factory.Services.GetRequiredService<IOperationStatusStore>();
        var importStore = MedRecProHostFixture.Factory.Services.GetRequiredService<IImportOperationStatusStore>();
        comparisonStore.SetComparisonStatus(comparisonOperationId, new ComparisonOperationStatus
        {
            OperationId = comparisonOperationId,
            Status = "Processing",
            PercentComplete = 42,
            DocumentGuid = seededDocumentGuid
        });
        importStore.Set(importOperationId, new ImportRuntimeOperationStatus
        {
            OperationId = importOperationId,
            Status = "Running",
            PercentComplete = 37,
            CurrentFile = 3,
            TotalFiles = 8
        });
        using var client = createClient();

        using var comparisonResponse = await client.GetAsync(
            $"{LabelRoutePrefix}/comparison/progress/{comparisonOperationId}");
        using var importResponse = await client.GetAsync(
            $"{LabelRoutePrefix}/import/progress/{importOperationId}");
        using var comparisonPayload = JsonDocument.Parse(await comparisonResponse.Content.ReadAsStringAsync());
        using var importPayload = JsonDocument.Parse(await importResponse.Content.ReadAsStringAsync());

        Assert.AreEqual(System.Net.HttpStatusCode.OK, comparisonResponse.StatusCode);
        Assert.AreEqual(System.Net.HttpStatusCode.OK, importResponse.StatusCode);
        Assert.AreEqual(comparisonOperationId, comparisonPayload.RootElement.GetProperty("operationId").GetString());
        Assert.AreEqual("Processing", comparisonPayload.RootElement.GetProperty("status").GetString());
        Assert.AreEqual(42, comparisonPayload.RootElement.GetProperty("percentComplete").GetInt32());
        Assert.AreEqual(importOperationId, importPayload.RootElement.GetProperty("operationId").GetString());
        Assert.AreEqual("Running", importPayload.RootElement.GetProperty("status").GetString());
        Assert.AreEqual(37, importPayload.RootElement.GetProperty("percentComplete").GetInt32());

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies unknown comparison and import operation IDs retain the empty 404 boundary.
    /// </summary>
    /// <returns>A task representing the asynchronous HTTP assertions.</returns>
    /// <seealso cref="MedRecPro.Api.Controllers.LabelComparisonController.GetComparisonProgress"/>
    /// <seealso cref="MedRecPro.Api.Controllers.LabelImportController.GetImportProgress"/>
    [TestMethod]
    public async Task ProgressEndpoints_UnknownOperationIds_ReturnNotFound()
    {
        #region implementation

        using var client = createClient();
        using var comparisonResponse = await client.GetAsync(
            $"{LabelRoutePrefix}/comparison/progress/missing-{Guid.NewGuid():N}");
        using var importResponse = await client.GetAsync(
            $"{LabelRoutePrefix}/import/progress/missing-{Guid.NewGuid():N}");
        using var comparisonPayload = JsonDocument.Parse(await comparisonResponse.Content.ReadAsStringAsync());
        using var importPayload = JsonDocument.Parse(await importResponse.Content.ReadAsStringAsync());

        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, comparisonResponse.StatusCode);
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, importResponse.StatusCode);
        Assert.AreEqual("application/problem+json", comparisonResponse.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual("application/problem+json", importResponse.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual(404, comparisonPayload.RootElement.GetProperty("status").GetInt32());
        Assert.AreEqual(404, importPayload.RootElement.GetProperty("status").GetInt32());

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies MVC binding rejects a whitespace comparison operation ID with validation ProblemDetails.
    /// </summary>
    /// <returns>A task representing the asynchronous HTTP assertion.</returns>
    /// <seealso cref="MedRecPro.Api.Controllers.LabelComparisonController.GetComparisonProgress"/>
    [TestMethod]
    public async Task ComparisonProgress_WhitespaceOperationId_ReturnsValidationProblemDetails()
    {
        #region implementation

        using var client = createClient();
        using var response = await client.GetAsync($"{LabelRoutePrefix}/comparison/progress/%20");
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual(400, payload.RootElement.GetProperty("status").GetInt32());
        StringAssert.Contains(
            payload.RootElement.GetProperty("errors").GetProperty("operationId")[0].GetString(),
            "required");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the markdown download is a real file response with its established content contract.
    /// </summary>
    /// <returns>A task representing the asynchronous file-response assertion.</returns>
    /// <remarks>
    /// The seeded markdown-view row prevents the endpoint from taking its intentional empty-document 404 path.
    /// </remarks>
    /// <seealso cref="MedRecPro.Controllers.LabelMarkdownController.DownloadLabelMarkdown"/>
    [TestMethod]
    public async Task MarkdownDownload_SeededDocument_ReturnsFileContract()
    {
        #region implementation

        using var client = createClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/markdown");
        var response = await client.GetAsync($"{LabelRoutePrefix}/markdown/download/{seededDocumentGuid:D}");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var disposition = response.Content.Headers.ContentDisposition;

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("text/markdown", response.Content.Headers.ContentType?.MediaType);
        Assert.IsTrue(bytes.Length > 0, "The markdown download must contain UTF-8 response bytes.");
        Assert.IsNotNull(disposition, "A download must declare Content-Disposition.");
        Assert.AreEqual("attachment", disposition.DispositionType);
        Assert.AreEqual("HOST-ASPIRIN-label.md", disposition.FileName?.Trim('"'));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies an unhandled request exception reaches the non-development exception middleware as sanitized ProblemDetails.
    /// </summary>
    /// <returns>A task representing the asynchronous production-middleware assertion.</returns>
    /// <remarks>
    /// The dedicated factory starts in the non-production Staging environment, which activates the same exception-handler
    /// branch without allowing the production-only Key Vault configuration source to start. This does not change the
    /// Debug/Release compilation symbol. Its opt-in startup filter throws only after the real pipeline is configured.
    /// </remarks>
    /// <seealso cref="MedRecPro.Exceptions.MedRecProExceptionHandler"/>
    /// <seealso cref="TestExceptionThrowingStartupFilter"/>
    [TestMethod]
    public async Task ExceptionProbe_NonDevelopmentHost_ReturnsSanitizedProblemDetails()
    {
        #region implementation

        using var factory = new MedRecProWebApplicationFactory(
            Environments.Staging,
            throwOnTestRequest: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync(TestExceptionThrowingStartupFilter.ThrowPath);
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        Assert.AreEqual(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual("An unexpected error occurred.", document.RootElement.GetProperty("title").GetString());
        Assert.IsTrue(document.RootElement.TryGetProperty("traceId", out var traceIdElement));
        Assert.IsFalse(
            payload.Contains("Phase 3 test-only exception detail", StringComparison.Ordinal),
            "The exception handler must not expose server exception details to API clients.");

        var logProvider = factory.Services.GetRequiredService<UserLoggerProvider>();
        var errorEntry = logProvider.GetLogs().Single(entry =>
            entry.Level == Microsoft.Extensions.Logging.LogLevel.Error
            && entry.Category == "MedRecPro.Exceptions.MedRecProExceptionHandler");

        Assert.AreEqual(traceIdElement.GetString(), errorEntry.TraceId);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a non-redirecting client for real HTTP contract assertions.
    /// </summary>
    /// <returns>Shared-host HTTP client configured to preserve authorization responses.</returns>
    /// <seealso cref="MedRecProHostFixture"/>
    private static HttpClient createClient()
    {
        #region implementation

        return MedRecProHostFixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        #endregion
    }

    #endregion
}
