using MedRecProTest.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MedRecProTest.Contracts.Swagger;

/**************************************************************/
/// <summary>
/// Guards the generated Debug or Release Settings and Users OpenAPI contracts through reviewed snapshots.
/// </summary>
/// <remarks>
/// The projection covers all 29 operations and retains paths, methods, parameters, operation IDs, tags, security,
/// request bodies, and responses while excluding volatile host values.
/// </remarks>
/// <seealso cref="MedRecProWebApplicationFactory"/>
[TestClass]
[TestCategory("Contract")]
[DoNotParallelize]
public class SettingsAndUsersOpenApiContractTests
{
    #region implementation

#if DEBUG
    private const string SettingsRoutePrefix = "/api/Settings";
    private const string UsersRoutePrefix = "/api/Users";
    private const string SnapshotFileName = "settings-users-debug.contract.json";
#else
    private const string SettingsRoutePrefix = "/Settings";
    private const string UsersRoutePrefix = "/Users";
    private const string SnapshotFileName = "settings-users-release.contract.json";
#endif

    private static readonly HashSet<string> httpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head", "options"
    };

    /**************************************************************/
    /// <summary>
    /// Verifies generated Settings and Users operations match the reviewed snapshot.
    /// </summary>
    /// <returns>A task representing the hosted Swagger and snapshot assertion.</returns>
    /// <remarks>
    /// Set <c>MEDRECPRO_SETTINGS_USERS_OPENAPI_CONTRACT_OUTPUT</c> to an external path to export a candidate.
    /// The test never overwrites the checked-in snapshot.
    /// </remarks>
    /// <seealso cref="projectContract"/>
    [TestMethod]
    public async Task SettingsAndUsersOperations_GeneratedOpenApi_MatchesReviewedSnapshot()
    {
        #region implementation

        var actual = await fetchCanonicalContractAsync();
        writeCandidateForReview(actual);

        var snapshotPath = Path.Combine(AppContext.BaseDirectory, "TestData", "OpenApi", SnapshotFileName);
        Assert.IsTrue(File.Exists(snapshotPath),
            $"Missing reviewed {SnapshotFileName}. Export a candidate for review; do not update snapshots automatically.");

        var expected = File.ReadAllText(snapshotPath).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.AreEqual(expected, actual,
            $"Generated Settings/Users OpenAPI contract differs from reviewed {SnapshotFileName}.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the generated document contains all 15 Settings and 14 Users operations.
    /// </summary>
    /// <returns>A task representing the hosted operation-count assertion.</returns>
    /// <seealso cref="SettingsAndUsersOperations_GeneratedOpenApi_MatchesReviewedSnapshot"/>
    [TestMethod]
    public async Task SettingsAndUsersOperations_GeneratedOpenApi_ContainsAllTwentyNineOperations()
    {
        #region implementation

        var contract = JsonNode.Parse(await fetchCanonicalContractAsync())!.AsObject();
        var operations = contract["operations"]!.AsObject();

        Assert.AreEqual(29, operations.Count,
            "The generated OpenAPI contract must expose all 15 Settings and 14 Users operations.");
        Assert.AreEqual(15, operations.Count(pair =>
                pair.Value!["path"]!.GetValue<string>().StartsWith(SettingsRoutePrefix, StringComparison.OrdinalIgnoreCase)),
            "The generated OpenAPI contract must expose all 15 Settings operations.");
        Assert.AreEqual(14, operations.Count(pair =>
                pair.Value!["path"]!.GetValue<string>().StartsWith(UsersRoutePrefix, StringComparison.OrdinalIgnoreCase)),
            "The generated OpenAPI contract must expose all 14 Users operations.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Fetches the hosted Swagger document and converts it to canonical contract text.
    /// </summary>
    /// <returns>Canonical JSON text with LF line endings.</returns>
    /// <seealso cref="projectContract"/>
    private static async Task<string> fetchCanonicalContractAsync()
    {
        #region implementation

        using var client = MedRecProHostFixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var swaggerJson = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);

        return canonicalize(projectContract(swaggerJson));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Projects Settings and Users operations from a generated Swagger document.
    /// </summary>
    /// <param name="swaggerJson">Swagger JSON from the real test host.</param>
    /// <returns>A compact JSON object containing all 29 public operation contracts.</returns>
    private static JsonObject projectContract(string swaggerJson)
    {
        #region implementation

        using var document = JsonDocument.Parse(swaggerJson);
        var contract = new JsonObject
        {
            ["routePrefixes"] = new JsonArray(SettingsRoutePrefix, UsersRoutePrefix),
            ["operations"] = new JsonObject()
        };
        var operations = contract["operations"]!.AsObject();

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            if (!isTargetPath(path.Name))
            {
                continue;
            }

            var pathParameters = getParameters(path.Value);

            foreach (var member in path.Value.EnumerateObject())
            {
                if (!httpMethods.Contains(member.Name))
                {
                    continue;
                }

                var operation = member.Value;
                var projection = new JsonObject
                {
                    ["path"] = path.Name,
                    ["method"] = member.Name.ToUpperInvariant(),
                    ["parameters"] = combineParameters(pathParameters, getParameters(operation))
                };

                copyProperty(operation, projection, "operationId");
                copyProperty(operation, projection, "tags");
                copyProperty(operation, projection, "security");
                copyProperty(operation, projection, "requestBody");
                copyProperty(operation, projection, "responses");

                operations[$"{member.Name.ToUpperInvariant()} {path.Name}"] = projection;
            }
        }

        return contract;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Determines whether a Swagger path belongs to Settings or Users.
    /// </summary>
    /// <param name="path">Swagger path to classify.</param>
    /// <returns><see langword="true"/> when the path belongs to either guarded route family.</returns>
    private static bool isTargetPath(string path)
    {
        #region implementation

        return path.StartsWith(SettingsRoutePrefix, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(UsersRoutePrefix, StringComparison.OrdinalIgnoreCase);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extracts optional path- or operation-level OpenAPI parameters.
    /// </summary>
    /// <param name="element">Swagger path item or operation element.</param>
    /// <returns>A cloned parameter array, or an empty array when none are present.</returns>
    /// <seealso cref="combineParameters"/>
    private static JsonArray getParameters(JsonElement element)
    {
        #region implementation

        return element.TryGetProperty("parameters", out var parameters)
            ? JsonNode.Parse(parameters.GetRawText())!.AsArray()
            : new JsonArray();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Combines path-level and operation-level OpenAPI parameters.
    /// </summary>
    /// <param name="pathParameters">Parameters declared on the path.</param>
    /// <param name="operationParameters">Parameters declared on the operation.</param>
    /// <returns>A combined array preserving Swagger declaration order.</returns>
    /// <seealso cref="getParameters"/>
    private static JsonArray combineParameters(JsonArray pathParameters, JsonArray operationParameters)
    {
        #region implementation

        var parameters = new JsonArray();
        foreach (var parameter in pathParameters.Concat(operationParameters))
        {
            parameters.Add(parameter?.DeepClone());
        }

        return parameters;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Copies one optional OpenAPI property into a compact operation projection.
    /// </summary>
    /// <param name="source">Swagger operation containing the property.</param>
    /// <param name="target">Projection receiving the cloned property.</param>
    /// <param name="propertyName">OpenAPI property name.</param>
    private static void copyProperty(JsonElement source, JsonObject target, string propertyName)
    {
        #region implementation

        if (source.TryGetProperty(propertyName, out var property))
        {
            target[propertyName] = JsonNode.Parse(property.GetRawText());
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Sorts JSON object keys and emits fixed-indentation LF-normalized text.
    /// </summary>
    /// <param name="contract">Contract projection to canonicalize.</param>
    /// <returns>Deterministic JSON ending with one LF.</returns>
    /// <seealso cref="sortKeysRecursively"/>
    private static string canonicalize(JsonNode contract)
    {
        #region implementation

        return sortKeysRecursively(contract)!
            .ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Recursively sorts JSON object keys while preserving array order.
    /// </summary>
    /// <param name="node">JSON node to clone and canonicalize.</param>
    /// <returns>A recursively canonicalized clone.</returns>
    /// <seealso cref="canonicalize"/>
    private static JsonNode? sortKeysRecursively(JsonNode? node)
    {
        #region implementation

        return node switch
        {
            JsonObject source => new JsonObject(source
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => sortKeysRecursively(pair.Value))),
            JsonArray source => new JsonArray(source.Select(sortKeysRecursively).ToArray()),
            _ => node?.DeepClone()
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Exports a canonical candidate only when an explicit external review path is supplied.
    /// </summary>
    /// <param name="contract">Canonical contract candidate.</param>
    /// <remarks>
    /// Checked-in snapshots are never selected implicitly or overwritten by this test.
    /// </remarks>
    /// <seealso cref="SettingsAndUsersOperations_GeneratedOpenApi_MatchesReviewedSnapshot"/>
    private static void writeCandidateForReview(string contract)
    {
        #region implementation

        var outputPath = Environment.GetEnvironmentVariable("MEDRECPRO_SETTINGS_USERS_OPENAPI_CONTRACT_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, contract);

        #endregion
    }

    #endregion
}
