using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Architecture;

/**************************************************************/
/// <summary>
/// Verifies output-independent repository-source resolution.
/// </summary>
/// <remarks>
/// These tests run from both ordinary output and an externally rooted isolated
/// <c>BaseOutputPath</c> to prove the embedded marker is the only source of truth.
/// </remarks>
/// <seealso cref="RepositorySourceRoot"/>
[TestClass]
[TestCategory("Architecture")]
public class RepositorySourceRootTests
{
    /**************************************************************/
    /// <summary>
    /// Verifies that the embedded marker resolves the live solution and test project.
    /// </summary>
    /// <seealso cref="RepositorySourceRoot.RootPath"/>
    [TestMethod]
    public void RootPath_EmbeddedMarker_ResolvesRepositorySource()
    {
        #region implementation

        Assert.IsTrue(File.Exists(Path.Combine(RepositorySourceRoot.RootPath, "MedRecPro.sln")));
        Assert.IsTrue(File.Exists(Path.Combine(
            RepositorySourceRoot.RootPath,
            "MedRecProTest",
            "MedRecProTest.csproj")));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies that an assembly without the marker receives a rebuild diagnostic.
    /// </summary>
    /// <seealso cref="RepositorySourceRoot.Resolve(Assembly)"/>
    [TestMethod]
    public void Resolve_MissingMarker_ReportsMarkerAndAttemptedPath()
    {
        #region implementation

        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => RepositorySourceRoot.Resolve(typeof(string).Assembly));

        StringAssert.Contains(exception.Message, "RepositoryRoot");
        StringAssert.Contains(exception.Message, "Attempted path: '<missing>'");
        StringAssert.Contains(exception.Message, "Rebuild MedRecProTest");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Prevents repository-source tests from deriving source paths from build output.
    /// </summary>
    /// <remarks>
    /// Output-local fixture reads remain allowed; only ancestor-walk source discovery is
    /// prohibited because it fails when output is outside the checkout.
    /// </remarks>
    /// <seealso cref="RepositorySourceRoot.RootPath"/>
    [TestMethod]
    public void RepositorySourceInventories_DoNotWalkAncestorsFromBaseDirectory()
    {
        #region implementation

        var testProjectDirectory = Path.Combine(RepositorySourceRoot.RootPath, "MedRecProTest");
        var directoryInfoWalk = "new DirectoryInfo(" + "AppContext.BaseDirectory)";
        var getParentWalk = "Directory.GetParent(" + "AppContext.BaseDirectory)";
        var violations = Directory
            .EnumerateFiles(testProjectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(path => new { Path = path, Content = File.ReadAllText(path) })
            .Where(file =>
                file.Content.Contains(directoryInfoWalk, StringComparison.Ordinal) ||
                file.Content.Contains(getParentWalk, StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(testProjectDirectory, file.Path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }
}
