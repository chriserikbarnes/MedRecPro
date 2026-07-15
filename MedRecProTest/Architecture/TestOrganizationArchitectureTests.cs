using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Architecture;

/**************************************************************/
/// <summary>
/// Enforces the kind-first, feature-second MedRecPro test taxonomy.
/// </summary>
/// <remarks>
/// The guard keeps Test Explorer navigable and prevents test classes from drifting
/// back into deprecated catch-all or infrastructure namespaces.
/// </remarks>
/// <seealso cref="TestClassAttribute"/>
[TestClass]
[TestCategory("Architecture")]
public class TestOrganizationArchitectureTests
{
    private static readonly HashSet<string> KindCategories = new(StringComparer.Ordinal)
    {
        "Architecture",
        "Contract",
        "Integration",
        "Unit"
    };

    /**************************************************************/
    /// <summary>
    /// Verifies that discoverable test classes avoid deprecated catch-all namespaces.
    /// </summary>
    /// <seealso cref="TestClassAttribute"/>
    [TestMethod]
    public void DiscoverableTests_DoNotUseDeprecatedCatchAllNamespaces()
    {
        #region implementation

        var violations = getTestClassTypes()
            .Where(type =>
                string.Equals(type.Namespace, "MedRecProTest", StringComparison.Ordinal) ||
                type.Namespace?.StartsWith("MedRecPro.Service.Test", StringComparison.Ordinal) == true ||
                type.Namespace?.StartsWith("MedRecProTest.TestInfrastructure", StringComparison.Ordinal) == true)
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies that every test class declares exactly one kind category.
    /// </summary>
    /// <seealso cref="TestCategoryAttribute"/>
    [TestMethod]
    public void DiscoverableTests_DeclareExactlyOneKindCategory()
    {
        #region implementation

        var violations = getTestClassTypes()
            .Select(type => new
            {
                Type = type,
                Kinds = type
                    .GetCustomAttributes<TestCategoryAttribute>(inherit: false)
                    .SelectMany(attribute => attribute.TestCategories)
                    .Where(KindCategories.Contains)
                    .ToList()
            })
            .Where(item => item.Kinds.Count != 1)
            .Select(item => $"{item.Type.FullName}: [{string.Join(", ", item.Kinds)}]")
            .OrderBy(message => message, StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies that source folders mirror their declared test namespaces.
    /// </summary>
    /// <remarks>
    /// Non-discoverable helpers may remain in TestInfrastructure; this check applies
    /// only to source files containing a test class.
    /// </remarks>
    /// <seealso cref="RepositorySourceRoot.RootPath"/>
    [TestMethod]
    public void DiscoverableTestSource_FoldersMirrorNamespaces()
    {
        #region implementation

        var testProjectDirectory = Path.Combine(RepositorySourceRoot.RootPath, "MedRecProTest");
        var testClassPattern = new System.Text.RegularExpressions.Regex(
            @"(?m)^\s*\[TestClass\]\s*$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var namespacePattern = new System.Text.RegularExpressions.Regex(
            @"(?m)^namespace\s+(?<name>[^;{\r\n]+)",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var violations = new List<string>();

        foreach (var sourcePath in Directory
                     .EnumerateFiles(testProjectDirectory, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !isBuildArtifact(path)))
        {
            var content = File.ReadAllText(sourcePath);
            if (!testClassPattern.IsMatch(content))
            {
                continue;
            }

            var namespaceMatch = namespacePattern.Match(content);
            if (!namespaceMatch.Success)
            {
                violations.Add($"{Path.GetRelativePath(testProjectDirectory, sourcePath)}: namespace missing.");
                continue;
            }

            var namespaceName = namespaceMatch.Groups["name"].Value.Trim();
            var expectedDirectory = namespaceName
                .Substring("MedRecProTest.".Length)
                .Replace('.', Path.DirectorySeparatorChar);
            var actualDirectory = Path.GetDirectoryName(Path.GetRelativePath(testProjectDirectory, sourcePath)) ?? string.Empty;
            if (!string.Equals(expectedDirectory, actualDirectory, StringComparison.Ordinal))
            {
                violations.Add(
                    $"{Path.GetRelativePath(testProjectDirectory, sourcePath)}: namespace '{namespaceName}' expects '{expectedDirectory}'.");
            }
        }

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies that TestInfrastructure contains helper types only.
    /// </summary>
    /// <seealso cref="TestClassAttribute"/>
    [TestMethod]
    public void TestInfrastructure_ContainsNoTestClassTypes()
    {
        #region implementation

        var violations = getTestClassTypes()
            .Where(type => type.Namespace?.StartsWith("MedRecProTest.TestInfrastructure", StringComparison.Ordinal) == true)
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets test classes from the compiled MedRecProTest assembly.
    /// </summary>
    /// <returns>Types carrying the MSTest test-class attribute.</returns>
    /// <seealso cref="TestClassAttribute"/>
    private static IReadOnlyList<Type> getTestClassTypes()
    {
        #region implementation

        return typeof(TestOrganizationArchitectureTests)
            .Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<TestClassAttribute>(inherit: false) != null)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Determines whether a source path belongs to generated build output.
    /// </summary>
    /// <param name="path">Absolute source path.</param>
    /// <returns><see langword="true"/> for bin, obj, or local evidence output.</returns>
    private static bool isBuildArtifact(string path)
    {
        #region implementation

        return path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"{Path.DirectorySeparatorChar}.codex-build{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);

        #endregion
    }
}
