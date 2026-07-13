using MedRecProTest.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MedRecProTest.Contracts;

/**************************************************************/
/// <summary>
/// Guards the generated Debug or Release Label OpenAPI contract through a reviewed canonical snapshot.
/// </summary>
/// <remarks>
/// The snapshot intentionally projects only Label operations and their public contract details: paths,
/// methods, parameter defaults and sources, request bodies, responses, tags, operation IDs, and security.
/// Object keys are ordinal-sorted and line endings normalized; no test ever updates a checked-in snapshot.
/// </remarks>
/// <seealso cref="MedRecProWebApplicationFactory"/>
[TestClass]
[DoNotParallelize]
[TestCategory("Contract")]
public class LabelOpenApiContractTests
{
    #region implementation

#if DEBUG
    private const string LabelRoutePrefix = "/api/Label";
    private const string SnapshotFileName = "label-debug.contract.json";
#else
    private const string LabelRoutePrefix = "/Label";
    private const string SnapshotFileName = "label-release.contract.json";
#endif

    private static readonly HashSet<string> httpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head", "options"
    };

    /**************************************************************/
    /// <summary>
    /// Verifies all generated Label operations match the reviewed contract snapshot.
    /// </summary>
    /// <returns>A task representing the asynchronous host and snapshot assertion.</returns>
    /// <remarks>
    /// Set <c>MEDRECPRO_OPENAPI_CONTRACT_OUTPUT</c> to an external review path to export the current
    /// canonical candidate. The test still compares against the checked-in snapshot and therefore fails
    /// until a human reviews and intentionally applies the candidate.
    /// </remarks>
    /// <seealso cref="projectLabelContract"/>
    [TestMethod]
    public async Task LabelOperations_GeneratedOpenApi_MatchesReviewedSnapshot()
    {
        #region implementation

        var actual = await fetchCanonicalContractAsync();
        writeCandidateForReview(actual);

        var snapshotPath = Path.Combine(AppContext.BaseDirectory, "TestData", "OpenApi", SnapshotFileName);
        Assert.IsTrue(File.Exists(snapshotPath),
            $"Missing reviewed {SnapshotFileName}. Export a candidate for review; do not update snapshots automatically.");

        var expected = File.ReadAllText(snapshotPath).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.AreEqual(expected, actual,
            $"Generated Label OpenAPI contract differs from reviewed {SnapshotFileName}.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the generated document contains the complete 51-operation Label public surface.
    /// </summary>
    /// <returns>A task representing the asynchronous host and operation-count assertion.</returns>
    /// <seealso cref="LabelOperations_GeneratedOpenApi_MatchesReviewedSnapshot"/>
    [TestMethod]
    public async Task LabelOperations_GeneratedOpenApi_ContainsAllFiftyOneOperations()
    {
        #region implementation

        var contract = JsonNode.Parse(await fetchCanonicalContractAsync())!.AsObject();
        var operations = contract["operations"]!.AsObject();

        Assert.AreEqual(51, operations.Count,
            "The generated Label OpenAPI contract must expose all 51 public Label operations.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Fetches the real hosted Swagger document and converts it to canonical Label contract text.
    /// </summary>
    /// <returns>Canonical UTF-8-safe JSON text with LF line endings.</returns>
    /// <seealso cref="projectLabelContract"/>
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

        return canonicalize(projectLabelContract(swaggerJson));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Projects Label operations from a Swagger document without volatile host or cache-buster values.
    /// </summary>
    /// <param name="swaggerJson">Generated Swagger JSON from the real host.</param>
    /// <returns>A JSON object containing only the complete Label operation contract.</returns>
    /// <seealso cref="LabelRoutePrefix"/>
    private static JsonObject projectLabelContract(string swaggerJson)
    {
        #region implementation

        using var document = JsonDocument.Parse(swaggerJson);
        var contract = new JsonObject
        {
            ["routePrefix"] = LabelRoutePrefix,
            ["operations"] = new JsonObject()
        };
        var operations = contract["operations"]!.AsObject();

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            if (!path.Name.StartsWith(LabelRoutePrefix, StringComparison.OrdinalIgnoreCase))
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
    /// Extracts optional path- or operation-level OpenAPI parameters.
    /// </summary>
    /// <param name="element">Swagger path item or operation element.</param>
    /// <returns>A cloned parameter array that is empty when no parameters are declared.</returns>
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
    /// Combines path-level and operation-level parameters into one contract value.
    /// </summary>
    /// <param name="pathParameters">Parameters declared for the route path.</param>
    /// <param name="operationParameters">Parameters declared for the HTTP operation.</param>
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
    /// Copies one optional OpenAPI property into the compact contract projection.
    /// </summary>
    /// <param name="source">Swagger operation containing the property.</param>
    /// <param name="target">Contract projection receiving the cloned property.</param>
    /// <param name="propertyName">OpenAPI property name to copy.</param>
    /// <seealso cref="projectLabelContract"/>
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
    /// Sorts every JSON object key ordinally and emits fixed-indentation LF-normalized JSON.
    /// </summary>
    /// <param name="contract">Contract projection to canonicalize.</param>
    /// <returns>Deterministic canonical JSON text ending with one LF.</returns>
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
    /// Recursively sorts JSON object keys while retaining semantically meaningful array order.
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
    /// <param name="contract">Canonical contract candidate to export.</param>
    /// <remarks>
    /// This method never writes the checked-in snapshot path; review and source-control changes remain explicit.
    /// </remarks>
    /// <seealso cref="LabelOperations_GeneratedOpenApi_MatchesReviewedSnapshot"/>
    private static void writeCandidateForReview(string contract)
    {
        #region implementation

        var outputPath = Environment.GetEnvironmentVariable("MEDRECPRO_OPENAPI_CONTRACT_OUTPUT");

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
