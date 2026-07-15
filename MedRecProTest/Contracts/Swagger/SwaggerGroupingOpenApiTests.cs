using MedRecPro.Api.Controllers;
using MedRecProTest.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace MedRecProTest.Contracts.Swagger;

/**************************************************************/
/// <summary>
/// Verifies the production Swagger pipeline preserves the exact reviewed grouping map for all guarded operations.
/// </summary>
/// <remarks>
/// Configuration-specific reviewed snapshots provide the exact 80-operation expectation while hosted document-tag
/// assertions prove the generic grouping filters publish the reviewed descriptions.
/// </remarks>
/// <seealso cref="SwaggerGroupOperationFilter"/>
/// <seealso cref="SwaggerGroupDocumentFilter"/>
[TestClass]
[TestCategory("Contract")]
[DoNotParallelize]
public class SwaggerGroupingOpenApiTests
{
    #region implementation

#if DEBUG
    private const string LabelRoutePrefix = "/api/Label";
    private const string SettingsRoutePrefix = "/api/Settings";
    private const string UsersRoutePrefix = "/api/Users";
    private const string LabelSnapshotFileName = "label-debug.contract.json";
    private const string SettingsUsersSnapshotFileName = "settings-users-debug.contract.json";
#else
    private const string LabelRoutePrefix = "/Label";
    private const string SettingsRoutePrefix = "/Settings";
    private const string UsersRoutePrefix = "/Users";
    private const string LabelSnapshotFileName = "label-release.contract.json";
    private const string SettingsUsersSnapshotFileName = "settings-users-release.contract.json";
#endif

    private static readonly HashSet<string> httpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head", "options"
    };

    private static readonly IReadOnlyDictionary<string, string> expectedGroupDescriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Label Application Numbers"] = "FDA application-number discovery and marketing-category summaries.",
            ["Label Classification"] = "Pharmacologic class, indication, and DEA-schedule discovery.",
            ["Label Ingredients"] = "Active/inactive ingredient search, summaries, and relationships.",
            ["Label Metadata"] = "API endpoint guide and dataset inventory summaries.",
            ["Label Product Identifiers"] = "NDC product/package code and labeler discovery.",
            ["Label Products"] = "Product-name search, latest labels, related products, and indications.",
            ["Label Section Navigation"] = "Section-code search, summaries, and section content retrieval.",
            ["Settings Application Info"] =
                "Non-sensitive runtime configuration for clients: demo mode, application info, feature flags, database limits.",
            ["Settings Cache"] = "Managed-cache administration.",
            ["Settings Diagnostics"] = "Admin-only Azure SQL cost metrics and credential/metrics pipeline tests.",
            ["Settings Logs"] = "Admin-only in-memory application log queries.",
            ["User Activity"] = "Per-user activity history and endpoint usage statistics.",
            ["User Administration"] = "Elevated account maintenance: admin updates, deletion, password rotation.",
            ["User Authentication"] = "Account creation and credential authentication.",
            ["User Directory"] = "User lookup by identifier, email, and paged listing.",
            ["User MCP Integration"] =
                "MCP-server identity resolution with auto-provisioning (McpBearer scheme).",
            ["User Profile"] = "Current-user profile retrieval and self-service profile updates."
        };

    /**************************************************************/
    /// <summary>
    /// Verifies every hosted Label, Settings, and Users operation retains its reviewed tag and group description.
    /// </summary>
    /// <returns>A task representing the hosted Swagger and exact-map assertion.</returns>
    /// <seealso cref="SwaggerGroupOperationFilter"/>
    [TestMethod]
    public async Task SwaggerGroups_HostedDocument_MatchesReviewedEightyOperationTagMap()
    {
        #region implementation

        using var client = MedRecProHostFixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var swaggerJson = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);

        var expected = loadExpectedTagMap();
        var actual = projectHostedTagMap(swaggerJson);

        Assert.AreEqual(80, expected.Count, "Reviewed snapshots must cover all 80 guarded operations.");
        Assert.AreEqual(expected.Count, actual.Count,
            "Hosted Swagger must expose the same guarded operation count as the reviewed snapshots.");

        foreach (var operation in expected.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Assert.IsTrue(actual.TryGetValue(operation.Key, out var actualTag),
                $"Hosted Swagger is missing {operation.Key}.");
            Assert.AreEqual(operation.Value, actualTag, $"Swagger tag changed for {operation.Key}.");
        }

        var tagCounts = actual.Values
            .GroupBy(tag => tag, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.AreEqual(2, tagCounts["Label Application Numbers"]);
        Assert.AreEqual(6, tagCounts["Label Classification"]);
        Assert.AreEqual(3, tagCounts["Label Comparison"]);
        Assert.AreEqual(6, tagCounts["Label Documents"]);
        Assert.AreEqual(2, tagCounts["Label Import"]);
        Assert.AreEqual(7, tagCounts["Label Ingredients"]);
        Assert.AreEqual(4, tagCounts["Label Markdown"]);
        Assert.AreEqual(2, tagCounts["Label Metadata"]);
        Assert.AreEqual(4, tagCounts["Label Product Identifiers"]);
        Assert.AreEqual(5, tagCounts["Label Products"]);
        Assert.AreEqual(3, tagCounts["Label Section Navigation"]);
        Assert.AreEqual(7, tagCounts["Label Sections"]);
        Assert.AreEqual(4, tagCounts["Settings Application Info"]);
        Assert.AreEqual(1, tagCounts["Settings Cache"]);
        Assert.AreEqual(3, tagCounts["Settings Diagnostics"]);
        Assert.AreEqual(7, tagCounts["Settings Logs"]);
        Assert.AreEqual(3, tagCounts["User Activity"]);
        Assert.AreEqual(3, tagCounts["User Administration"]);
        Assert.AreEqual(2, tagCounts["User Authentication"]);
        Assert.AreEqual(3, tagCounts["User Directory"]);
        Assert.AreEqual(1, tagCounts["User MCP Integration"]);
        Assert.AreEqual(2, tagCounts["User Profile"]);
        Assert.IsFalse(tagCounts.ContainsKey("Label Search"),
            "The actionless compatibility shell must not contribute a rendered Label Search operation.");
        Assert.IsFalse(tagCounts.ContainsKey("Settings"),
            "All Settings operations must render under intent groups.");
        Assert.IsFalse(tagCounts.ContainsKey("Users"),
            "All Users operations must render under intent groups.");

        var actualDescriptions = projectHostedGroupDescriptions(swaggerJson);
        foreach (var expectedDescription in expectedGroupDescriptions)
        {
            Assert.IsTrue(actualDescriptions.TryGetValue(expectedDescription.Key, out var actualDescription),
                $"Hosted Swagger is missing the described {expectedDescription.Key} document tag.");
            Assert.AreEqual(expectedDescription.Value, actualDescription,
                $"Swagger description changed for {expectedDescription.Key}.");
        }

        Assert.IsFalse(actualDescriptions.ContainsKey("Label Search"),
            "The actionless compatibility shell must not contribute a described Label Search document tag.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the production Swagger options register both generic grouping filters.
    /// </summary>
    /// <remarks>
    /// Descriptor inspection is limited to public options metadata and does not instantiate or mutate filters.
    /// </remarks>
    /// <seealso cref="SwaggerGenOptions"/>
    [TestMethod]
    public void SwaggerGroups_ProductionOptions_RegisterOperationAndDocumentFilters()
    {
        #region implementation

        var options = MedRecProHostFixture.Factory.Services
            .GetRequiredService<IOptions<SwaggerGenOptions>>()
            .Value;
        var registeredTypes = getRegisteredFilterTypes(options);

        CollectionAssert.Contains(registeredTypes, typeof(SwaggerGroupOperationFilter));
        CollectionAssert.Contains(registeredTypes, typeof(SwaggerGroupDocumentFilter));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Loads exact operation-to-tag expectations from both configuration-specific reviewed snapshots.
    /// </summary>
    /// <returns>A combined 80-operation tag map.</returns>
    private static Dictionary<string, string> loadExpectedTagMap()
    {
        #region implementation

        var expected = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var snapshotFileName in new[] { LabelSnapshotFileName, SettingsUsersSnapshotFileName })
        {
            var snapshotPath = Path.Combine(AppContext.BaseDirectory, "TestData", "OpenApi", snapshotFileName);
            Assert.IsTrue(File.Exists(snapshotPath), $"Missing reviewed snapshot {snapshotFileName}.");

            using var document = JsonDocument.Parse(File.ReadAllText(snapshotPath));
            foreach (var operation in document.RootElement.GetProperty("operations").EnumerateObject())
            {
                var tags = operation.Value.GetProperty("tags").EnumerateArray().ToArray();
                Assert.AreEqual(1, tags.Length, $"Reviewed {operation.Name} must carry exactly one tag.");
                expected.Add(operation.Name, tags[0].GetString()!);
            }
        }

        return expected;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Projects the hosted Swagger document into an exact guarded operation-to-tag map.
    /// </summary>
    /// <param name="swaggerJson">Swagger JSON returned by the real test host.</param>
    /// <returns>A map keyed by uppercase HTTP method and resolved path.</returns>
    private static Dictionary<string, string> projectHostedTagMap(string swaggerJson)
    {
        #region implementation

        using var document = JsonDocument.Parse(swaggerJson);
        var actual = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            if (!isGuardedPath(path.Name))
            {
                continue;
            }

            foreach (var member in path.Value.EnumerateObject().Where(member => httpMethods.Contains(member.Name)))
            {
                var tags = member.Value.GetProperty("tags").EnumerateArray().ToArray();
                Assert.AreEqual(1, tags.Length,
                    $"Hosted {member.Name.ToUpperInvariant()} {path.Name} must carry exactly one tag.");
                actual.Add($"{member.Name.ToUpperInvariant()} {path.Name}", tags[0].GetString()!);
            }
        }

        return actual;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Projects nonblank document-level Swagger tag descriptions by group name.
    /// </summary>
    /// <param name="swaggerJson">Swagger JSON returned by the real test host.</param>
    /// <returns>A map of described document tags keyed by exact group name.</returns>
    /// <seealso cref="SwaggerGroupDocumentFilter"/>
    private static Dictionary<string, string> projectHostedGroupDescriptions(string swaggerJson)
    {
        #region implementation

        using var document = JsonDocument.Parse(swaggerJson);
        var descriptions = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!document.RootElement.TryGetProperty("tags", out var tags))
        {
            return descriptions;
        }

        foreach (var tag in tags.EnumerateArray())
        {
            if (tag.TryGetProperty("name", out var name) &&
                tag.TryGetProperty("description", out var description) &&
                !string.IsNullOrWhiteSpace(description.GetString()))
            {
                descriptions[name.GetString()!] = description.GetString()!;
            }
        }

        return descriptions;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Determines whether a Swagger path belongs to one of the three guarded API families.
    /// </summary>
    /// <param name="path">Swagger path.</param>
    /// <returns><see langword="true"/> for Label, Settings, or Users paths.</returns>
    private static bool isGuardedPath(string path)
    {
        #region implementation

        return path.StartsWith(LabelRoutePrefix, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(SettingsRoutePrefix, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(UsersRoutePrefix, StringComparison.OrdinalIgnoreCase);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts registered Swagger filter types from public filter-descriptor collections.
    /// </summary>
    /// <param name="options">Configured production Swagger options.</param>
    /// <returns>An array of registered filter implementation types.</returns>
    /// <seealso cref="SwaggerGenOptions"/>
    private static Type[] getRegisteredFilterTypes(SwaggerGenOptions options)
    {
        #region implementation

        return typeof(SwaggerGenOptions)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.Name.EndsWith("FilterDescriptors", StringComparison.Ordinal))
            .Select(property => property.GetValue(options))
            .OfType<IEnumerable>()
            .SelectMany(values => values.Cast<object>())
            .Select(descriptor => descriptor.GetType().GetProperty("Type")?.GetValue(descriptor) as Type)
            .Where(type => type != null)
            .Select(type => type!)
            .Distinct()
            .ToArray();

        #endregion
    }

    #endregion
}
