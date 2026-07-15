using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;

namespace MedRecProTest.Architecture;

/**************************************************************/
/// <summary>
/// Guards MedRecProTest against reintroducing developer-secret configuration dependencies.
/// </summary>
/// <remarks>
/// The test inspects the test project file rather than relying on a developer's local secret-store
/// state, making the configuration-isolation rule visible in ordinary suite execution.
/// </remarks>
/// <seealso cref="MedRecProTestConfiguration"/>
[TestClass]
[TestCategory("Architecture")]
public class TestProjectDependencyGuardTests
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Verifies the test project contains no developer-secret project metadata or package reference.
    /// </summary>
    /// <remarks>
    /// Dependency names are assembled to keep the source scan itself focused on production
    /// dependencies rather than this guard's assertion text.
    /// </remarks>
    /// <seealso cref="MedRecProTestConfiguration"/>
    [TestMethod]
    public void TestProject_HasNoDeveloperSecretDependency()
    {
        #region implementation

        var project = XDocument.Load(findTestProjectPath());
        var metadataName = string.Concat("User", "SecretsId");
        var packageName = string.Concat("Microsoft.Extensions.Configuration.", "User", "Secrets");
        var builderMethodName = string.Concat("Add", "User", "Secrets");

        var metadata = project.Descendants()
            .Where(element => element.Name.LocalName == metadataName)
            .ToList();
        var packages = project.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference" &&
                              string.Equals((string?)element.Attribute("Include"), packageName, StringComparison.Ordinal))
            .ToList();
        var sourceFiles = Directory.GetFiles(findTestProjectDirectory(), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        var sourceCallers = sourceFiles
            .Where(path => File.ReadAllText(path).Contains(builderMethodName, StringComparison.Ordinal))
            .ToList();

        Assert.AreEqual(0, metadata.Count, "Developer-secret project metadata must not return to MedRecProTest.");
        Assert.AreEqual(0, packages.Count, "Developer-secret package dependency must not return to MedRecProTest.");
        Assert.AreEqual(0, sourceCallers.Count, "Tests must use MedRecProTestConfiguration instead of developer-secret configuration.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Locates the test project file from the compiled test assembly path.
    /// </summary>
    /// <returns>Absolute path to MedRecProTest.csproj.</returns>
    /// <seealso cref="findTestProjectDirectory"/>
    private static string findTestProjectPath()
    {
        #region implementation

        return Path.Combine(findTestProjectDirectory(), "MedRecProTest.csproj");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Locates the test project directory from build-time assembly metadata.
    /// </summary>
    /// <returns>Absolute path to the MedRecProTest directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the test project cannot be located.</exception>
    /// <seealso cref="findTestProjectPath"/>
    private static string findTestProjectDirectory()
    {
        #region implementation

        return Path.Combine(RepositorySourceRoot.RootPath, "MedRecProTest");

        #endregion
    }

    #endregion
}
