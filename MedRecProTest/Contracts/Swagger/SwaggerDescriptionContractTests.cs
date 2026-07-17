using MedRecPro.Configuration;
using MedRecProTest.Integration.Hosting;
using MedRecProTest.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MedRecProTest.Contracts.Swagger;

/**************************************************************/
/// <summary>
/// Verifies the Swagger information description remains resource-backed, concise, and consumer-facing.
/// </summary>
/// <remarks>
/// These hosted contract assertions guard the resource token inventory, demo-mode substitution, heading
/// structure, and route-drift controls without adding a second OpenAPI snapshot.
/// </remarks>
/// <seealso cref="MedRecProSwaggerExtensions"/>
/// <seealso cref="MedRecProWebApplicationFactory"/>
[TestClass]
[TestCategory("Contract")]
[DoNotParallelize]
public class SwaggerDescriptionContractTests
{
    #region implementation

    private const string swaggerDescriptionResourceName = "MedRecPro.SwaggerDocs.txt";

#if DEBUG
    private const string expectedEnvironment = "Dev";
#else
    private const string expectedEnvironment = "Prod";
#endif

    private static readonly string[] expectedTokens =
    {
        "{demoModeWarning}",
        "{environment}",
        "{serverName}"
    };

    private static readonly string[] expectedSectionHeadings =
    {
        "🚀 Getting Started",
        "🗺️ API Surface Map",
        "🔑 Conventions",
        "⚡ Operational Notes"
    };

    private static readonly string[] expectedApiSurfaces =
    {
        "AdverseEvent",
        "Ai",
        "Auth",
        "Label (12 groups)",
        "OrangeBook",
        "Settings (4 groups)",
        "Users (6 groups)",
        "MedRecPro (Release only)"
    };

    /**************************************************************/
    /// <summary>
    /// Verifies the Swagger description template is embedded with exactly the supported token inventory.
    /// </summary>
    /// <remarks>
    /// The template stays below the approved source-line budget so it remains an orientation aid rather than a
    /// second per-endpoint documentation surface.
    /// </remarks>
    /// <seealso cref="MedRecProSwaggerExtensions"/>
    [TestMethod]
    public void SwaggerDescription_EmbeddedResource_HasExactTokenInventoryAndLineBudget()
    {
        #region implementation

        var assembly = typeof(MedRecProSwaggerExtensions).Assembly;
        CollectionAssert.Contains(assembly.GetManifestResourceNames(), swaggerDescriptionResourceName);

        var template = readTemplate();
        var tokenMatches = Regex.Matches(template, @"\{[^{}]+\}")
            .Select(match => match.Value)
            .ToArray();

        Assert.AreEqual(expectedTokens.Length, tokenMatches.Length,
            "The Swagger description template must contain only the three supported substitution tokens.");
        CollectionAssert.AreEquivalent(expectedTokens, tokenMatches);
        Assert.IsTrue(getSourceLineCount(template) <= 90,
            "The Swagger description template must stay within the approved concise-description line budget.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the default hosted Swagger description is fully substituted, concise, and free of stale routes.
    /// </summary>
    /// <returns>A task representing the hosted description assertions.</returns>
    /// <seealso cref="SwaggerDescription_EmbeddedResource_HasExactTokenInventoryAndLineBudget"/>
    [TestMethod]
    public async Task SwaggerDescription_HostedDefault_ContainsExpectedSectionsAndNoRouteDrift()
    {
        #region implementation

        var description = await getHostedDescriptionAsync(MedRecProHostFixture.Factory);
        Assert.IsFalse(string.IsNullOrWhiteSpace(description));

        foreach (var token in expectedTokens)
        {
            Assert.IsFalse(description.Contains(token, StringComparison.Ordinal),
                $"Hosted Swagger description retained the {token} template token.");
        }

        Assert.IsFalse(description.Contains("/api/", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(description.Contains("api/labels", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(description.Contains("## ⚠️ **DEMO MODE ACTIVE** ⚠️", StringComparison.Ordinal));
        StringAssert.Contains(description, $"Environment: SQL Server {expectedEnvironment}");

        foreach (var surface in expectedApiSurfaces)
        {
            StringAssert.Contains(description, surface);
        }

        var headings = description
            .Split('\n')
            .Where(line => line.StartsWith("## ", StringComparison.Ordinal))
            .Select(line => line[3..].Trim())
            .ToArray();

        CollectionAssert.AreEquivalent(expectedSectionHeadings, headings);
        Assert.IsTrue(headings.All(heading => !string.IsNullOrWhiteSpace(heading)));
        Assert.AreEqual(headings.Length, headings.Distinct(StringComparer.Ordinal).Count(),
            "Every collapsible Swagger description heading must be unique.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies a configured host renders the demo warning and refresh interval without leaving a template token.
    /// </summary>
    /// <returns>A task representing the demo-mode hosted description assertions.</returns>
    /// <seealso cref="MedRecProWebApplicationFactory"/>
    [TestMethod]
    public async Task SwaggerDescription_HostedDemoMode_RendersWarningAndConfiguredInterval()
    {
        #region implementation

        using var factory = new MedRecProWebApplicationFactory(configurationOverrides: new Dictionary<string, string?>
        {
            ["DemoModeSettings:Enabled"] = "true",
            ["DemoModeSettings:RefreshIntervalMinutes"] = "120"
        });

        var description = await getHostedDescriptionAsync(factory);

        StringAssert.Contains(description, "## ⚠️ **DEMO MODE ACTIVE** ⚠️");
        StringAssert.Contains(description, "**120 minutes**");
        Assert.IsFalse(description.Contains("{demoModeWarning}", StringComparison.Ordinal));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Reads the Swagger description template from the MedRecPro assembly resource.
    /// </summary>
    /// <returns>The UTF-8 Markdown template.</returns>
    /// <seealso cref="MedRecProSwaggerExtensions"/>
    private static string readTemplate()
    {
        #region implementation

        var assembly = typeof(MedRecProSwaggerExtensions).Assembly;
        using var stream = assembly.GetManifestResourceStream(swaggerDescriptionResourceName);
        Assert.IsNotNull(stream, "The Swagger description resource must resolve from the MedRecPro assembly.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Retrieves the generated Swagger information description from a real MedRecPro test host.
    /// </summary>
    /// <param name="factory">Configured MedRecPro host factory to query.</param>
    /// <returns>A task containing the generated OpenAPI information description.</returns>
    /// <seealso cref="MedRecProWebApplicationFactory"/>
    private static async Task<string> getHostedDescriptionAsync(MedRecProWebApplicationFactory factory)
    {
        #region implementation

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        var swaggerJson = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(swaggerJson);
        return document.RootElement
            .GetProperty("info")
            .GetProperty("description")
            .GetString() ?? string.Empty;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Counts Markdown source lines after normalizing Windows line endings.
    /// </summary>
    /// <param name="template">Swagger description template to measure.</param>
    /// <returns>The total number of source lines.</returns>
    /// <seealso cref="SwaggerDescription_EmbeddedResource_HasExactTokenInventoryAndLineBudget"/>
    private static int getSourceLineCount(string template)
    {
        #region implementation

        return template.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Length;

        #endregion
    }

    #endregion
}
