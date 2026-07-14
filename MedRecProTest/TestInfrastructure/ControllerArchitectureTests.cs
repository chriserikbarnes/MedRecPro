using MedRecPro.Controllers;
using MedRecPro.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace MedRecProTest.TestInfrastructure;

/**************************************************************/
/// <summary>
/// Guards controller-size and routing boundaries.
/// </summary>
/// <remarks>
/// The reviewed exceptions identify currently stable endpoint families whose
/// decomposition is intentionally deferred until their dedicated contract
/// baseline work. Secret-boundary enforcement is isolated in
/// <see cref="SecretBoundaryArchitectureTests"/> so it can scan every
/// production project without conflating that concern with controller shape.
/// </remarks>
/// <seealso cref="LabelControllerRouteCompatibilityTests"/>
[TestClass]
public class ControllerArchitectureTests
{
    #region implementation

    private static readonly IReadOnlyDictionary<string, int> ReviewedActionCountExceptions =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["AdverseEventController"] = 19,
            ["SettingsController"] = 15,
            ["UsersController"] = 14
        };

    /**************************************************************/
    /// <summary>
    /// Verifies each controller is limited to ten declared HTTP actions unless
    /// its exact count has a reviewed exception.
    /// </summary>
    /// <remarks>
    /// Exact exception counts prevent silent endpoint growth in deferred
    /// controller families while preserving their existing public contracts.
    /// </remarks>
    [TestMethod]
    [TestCategory("Architecture")]
    public void Controllers_DeclaredActionCounts_AreLimitedOrExplicitlyReviewed()
    {
        #region implementation

        var actualExceptions = new Dictionary<string, int>(StringComparer.Ordinal);
        var violations = new List<string>();

        foreach (var controllerType in getControllerTypes())
        {
            var actionCount = controllerType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Count(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: false).Any());

            if (actionCount <= 10)
            {
                continue;
            }

            actualExceptions[controllerType.Name] = actionCount;
            if (!ReviewedActionCountExceptions.TryGetValue(controllerType.Name, out var reviewedCount))
            {
                violations.Add($"{controllerType.Name}: {actionCount} actions has no reviewed exception.");
            }
            else if (reviewedCount != actionCount)
            {
                violations.Add($"{controllerType.Name}: expected reviewed count {reviewedCount}, actual {actionCount}.");
            }
        }

        CollectionAssert.AreEquivalent(
            ReviewedActionCountExceptions.Keys.OrderBy(name => name).ToArray(),
            actualExceptions.Keys.OrderBy(name => name).ToArray(),
            "Reviewed exception inventory drifted.");
        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies Label controllers do not declare a type-level route.
    /// </summary>
    /// <seealso cref="LabelControllerRouteCompatibilityTests"/>
    [TestMethod]
    [TestCategory("Architecture")]
    public void LabelControllers_DoNotDeclareTypeLevelRoutes()
    {
        #region implementation

        var violations = getControllerTypes()
            .Where(type => type.Name.StartsWith("Label", StringComparison.Ordinal))
            .Where(type => type.GetCustomAttributes(inherit: false).OfType<IRouteTemplateProvider>().Any())
            .Select(type => type.FullName)
            .ToArray();

        Assert.AreEqual(0, violations.Length, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies Label controllers expose explicit application-service seams
    /// rather than framework service-location or database constructor
    /// dependencies. Configuration remains permitted for feature settings;
    /// raw primary-key secret access is guarded separately.
    /// </summary>
    [TestMethod]
    [TestCategory("Architecture")]
    public void LabelControllers_DoNotInjectForbiddenInfrastructureDependencies()
    {
        #region implementation

        var forbiddenTypes = new[]
        {
            typeof(IServiceProvider),
            typeof(ApplicationDbContext)
        };

        var violations = getControllerTypes()
            .Where(type => type.Name.StartsWith("Label", StringComparison.Ordinal))
            .SelectMany(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters()
                .Where(parameter => forbiddenTypes.Contains(parameter.ParameterType))
                .Select(parameter => $"{type.Name}: {parameter.ParameterType.Name} ({parameter.Name})")))
            .ToArray();

        Assert.AreEqual(0, violations.Length, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets concrete MVC controllers from the production assembly.
    /// </summary>
    /// <returns>Controller types with declared HTTP action metadata.</returns>
    private static IEnumerable<Type> getControllerTypes()
    {
        #region implementation

        return typeof(ApiControllerBase).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract)
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Where(type => type.Namespace is "MedRecPro.Controllers" or "MedRecPro.Api.Controllers");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Locates the repository directory from the compiled test assembly output.
    /// </summary>
    /// <returns>Absolute repository path containing the solution file.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the solution cannot be found.</exception>
    private static string findRepositoryDirectory()
    {
        #region implementation

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MedRecPro.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate MedRecPro.sln from the test assembly output directory.");

        #endregion
    }

    #endregion
}
