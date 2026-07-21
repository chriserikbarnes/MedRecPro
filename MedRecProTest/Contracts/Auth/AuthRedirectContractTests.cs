using MedRecProTest.Integration.Hosting;
using MedRecProTest.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Text.Json;

namespace MedRecProTest.Contracts.Auth;

/**************************************************************/
/// <summary>
/// Verifies the direct Auth redirect routes expose stable ProblemDetails contracts.
/// </summary>
/// <remarks>
/// The tests exercise the hosted MVC result pipeline with redirects disabled. They
/// distinguish the parameterless API outcome routes from the provider-qualified OAuth
/// route and from cookie-authentication challenge events.
/// </remarks>
/// <seealso cref="MedRecProWebApplicationFactory"/>
/// <seealso cref="MedRecPro.Controllers.AuthController"/>
[TestClass]
[TestCategory("Contract")]
[DoNotParallelize]
public class AuthRedirectContractTests
{
    #region implementation

#if DEBUG
    private const string LoginRedirectPath = "/api/Auth/login";
    private const string AccessDeniedPath = "/api/Auth/accessdenied";
#else
    private const string LoginRedirectPath = "/Auth/login";
    private const string AccessDeniedPath = "/Auth/accessdenied";
#endif

    /**************************************************************/
    /// <summary>
    /// Verifies the direct login route returns an actionable 401 ProblemDetails response.
    /// </summary>
    /// <returns>A task representing the hosted login-contract assertion.</returns>
    /// <remarks>
    /// The request is anonymous and disables automatic redirects, proving this route does not
    /// initiate OAuth or use the cookie redirect behavior as an implicit response contract.
    /// </remarks>
    /// <seealso cref="MedRecPro.Controllers.AuthController.HandleLoginRedirect"/>
    [TestMethod]
    public async Task HandleLoginRedirect_DirectRequest_ReturnsUnauthorizedProblemDetails()
    {
        #region implementation

        using var client = createClient();
        var response = await client.GetAsync(LoginRedirectPath);

        await assertProblemDetailsResponseAsync(
            response,
            HttpStatusCode.Unauthorized,
            "Authentication required.",
            "/api/Auth/login/{provider}");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the direct access-denied route returns an actionable 403 ProblemDetails response.
    /// </summary>
    /// <returns>A task representing the hosted access-denied-contract assertion.</returns>
    /// <remarks>
    /// This request executes the MVC result pipeline that previously received a human-readable
    /// string as an authentication-scheme name, so a 500 regression cannot pass as a weak gate check.
    /// </remarks>
    /// <seealso cref="MedRecPro.Controllers.AuthController.HandleAccessDenied"/>
    [TestMethod]
    public async Task HandleAccessDenied_DirectRequest_ReturnsForbiddenProblemDetails()
    {
        #region implementation

        using var client = createClient();
        var response = await client.GetAsync(AccessDeniedPath);

        await assertProblemDetailsResponseAsync(
            response,
            HttpStatusCode.Forbidden,
            "Access denied.",
            "not authorized");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies Swagger declares the direct-route 401 and 403 ProblemDetails responses.
    /// </summary>
    /// <returns>A task representing the hosted OpenAPI assertion.</returns>
    /// <remarks>
    /// The assertions use the generated document from the real host so response metadata remains
    /// synchronized with the executable API contract.
    /// </remarks>
    /// <seealso cref="HandleLoginRedirect_DirectRequest_ReturnsUnauthorizedProblemDetails"/>
    /// <seealso cref="HandleAccessDenied_DirectRequest_ReturnsForbiddenProblemDetails"/>
    [TestMethod]
    public async Task AuthRedirects_HostedSwagger_DeclaresProblemDetailsResponses()
    {
        #region implementation

        using var swagger = await fetchSwaggerDocumentAsync();

        assertSwaggerProblemDetailsResponse(swagger.RootElement, LoginRedirectPath, "401");
        assertSwaggerProblemDetailsResponse(swagger.RootElement, AccessDeniedPath, "403");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates a host client that leaves redirect outcomes observable to the test.
    /// </summary>
    /// <returns>An HTTP client configured against the shared MedRecPro host.</returns>
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

    /**************************************************************/
    /// <summary>
    /// Asserts a non-redirecting ProblemDetails response matches its direct-route contract.
    /// </summary>
    /// <param name="response">Hosted response returned by a direct Auth route.</param>
    /// <param name="expectedStatus">Expected HTTP and JSON ProblemDetails status.</param>
    /// <param name="expectedTitle">Stable ProblemDetails title.</param>
    /// <param name="expectedDetailFragment">Actionable phrase required in the ProblemDetails detail.</param>
    /// <returns>A task representing the asynchronous content assertion.</returns>
    /// <seealso cref="assertSwaggerProblemDetailsResponse"/>
    private static async Task assertProblemDetailsResponseAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string expectedDetailFragment)
    {
        #region implementation

        Assert.AreEqual(expectedStatus, response.StatusCode);
        Assert.IsFalse(response.Headers.Contains("Location"), "Direct Auth outcome routes must not return a redirect location.");

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        Assert.IsTrue(
            string.Equals("application/problem+json", mediaType, StringComparison.OrdinalIgnoreCase),
            $"Expected application/problem+json; received {mediaType ?? "(none)"}.");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var payload = document.RootElement;
        var title = payload.GetProperty("title").GetString();
        var detail = payload.GetProperty("detail").GetString();

        Assert.AreEqual((int)expectedStatus, payload.GetProperty("status").GetInt32());
        Assert.AreEqual(expectedTitle, title);
        Assert.IsFalse(string.IsNullOrWhiteSpace(detail), "ProblemDetails detail must be nonempty.");
        StringAssert.Contains(detail!, expectedDetailFragment);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Fetches the hosted Swagger document through the shared application pipeline.
    /// </summary>
    /// <returns>A parsed Swagger document whose caller owns disposal.</returns>
    /// <seealso cref="MedRecProWebApplicationFactory"/>
    private static async Task<JsonDocument> fetchSwaggerDocumentAsync()
    {
        #region implementation

        using var client = createClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var swaggerJson = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(swaggerJson);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Asserts one Auth route's Swagger response maps to an application/problem+json ProblemDetails schema.
    /// </summary>
    /// <param name="swagger">Generated Swagger document root.</param>
    /// <param name="path">Auth route path to inspect.</param>
    /// <param name="statusCode">Expected response-map status-code key.</param>
    /// <seealso cref="fetchSwaggerDocumentAsync"/>
    private static void assertSwaggerProblemDetailsResponse(JsonElement swagger, string path, string statusCode)
    {
        #region implementation

        Assert.IsTrue(swagger.GetProperty("paths").TryGetProperty(path, out var pathItem),
            $"Swagger is missing Auth route {path}.");
        Assert.IsTrue(pathItem.TryGetProperty("get", out var operation),
            $"Swagger is missing GET operation for {path}.");
        Assert.IsTrue(operation.GetProperty("responses").TryGetProperty(statusCode, out var response),
            $"Swagger is missing the {statusCode} response for {path}.");
        Assert.IsTrue(response.TryGetProperty("content", out var content),
            $"Swagger {statusCode} response for {path} must declare response content.");
        Assert.IsTrue(content.TryGetProperty("application/problem+json", out var problemContent),
            $"Swagger {statusCode} response for {path} must declare application/problem+json.");
        Assert.IsTrue(problemContent.TryGetProperty("schema", out var schema),
            $"Swagger {statusCode} ProblemDetails response for {path} is missing its schema.");
        Assert.IsTrue(schema.TryGetProperty("$ref", out var reference),
            $"Swagger {statusCode} ProblemDetails response for {path} must reference a schema.");
        Assert.AreEqual("#/components/schemas/ProblemDetails", reference.GetString());

        #endregion
    }

    #endregion
}