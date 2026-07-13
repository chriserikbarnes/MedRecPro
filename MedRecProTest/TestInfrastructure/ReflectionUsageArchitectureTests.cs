using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.TestInfrastructure;

/**************************************************************/
/// <summary>
/// Guards behavior tests against private-state reflection and method invocation.
/// </summary>
/// <remarks>
/// Reflection remains valid for the documented route, public-surface, and EF
/// metadata inventories. Behavior tests instead use constructor seams, focused
/// collaborators, or friend-assembly internals so refactoring stays visible to
/// the compiler and test runner.
/// </remarks>
/// <seealso cref="MedRecProPublicSurfaceInventoryTests"/>
/// <seealso cref="LabelControllerRouteCompatibilityTests"/>
[TestClass]
public class ReflectionUsageArchitectureTests
{
    #region implementation

    private static readonly HashSet<string> AllowedInventoryFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "AdverseEventControllerTests.cs",
        "ApplicationDbContextFunctionTests.cs",
        "DtoLabelAccessTestHelper.cs",
        "DtoLabelAccessSignatureCompatibilityTests.cs",
        "LabelControllerRouteCompatibilityTests.cs",
        "MedRecProPublicSurfaceInventoryTests.cs",
        "ParsingServicesPublicSurfaceInventoryTests.cs"
    };

    /**************************************************************/
    /// <summary>
    /// Verifies reflection use is limited to documented metadata inventories.
    /// </summary>
    /// <remarks>
    /// Private binding flags, reflective field access, reflective invocation,
    /// and private-state assignment are prohibited across behavior tests. The
    /// remaining public reflection APIs must reside in the allowlisted metadata
    /// inventory files above.
    /// </remarks>
    [TestMethod]
    [TestCategory("Unit")]
    public void TestProject_ReflectionIsLimitedToDocumentedMetadataInventories()
    {
        #region implementation

        var projectDirectory = findTestProjectDirectory();
        var privateBinding = string.Concat("BindingFlags.", "Non", "Public");
        var reflectiveField = string.Concat("Get", "Field(");
        var reflectiveAssignment = string.Concat("Set", "Value(");
        var reflectiveInvocation = string.Concat(".Inv", "oke(");
        var inventoryMarkers = new[]
        {
            string.Concat("Get", "Method("),
            string.Concat("Get", "Methods("),
            string.Concat("Get", "Properties(")
        };

        var violations = new List<string>();

        foreach (var sourcePath in Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                     .Where(path => !string.Equals(Path.GetFileName(path), nameof(ReflectionUsageArchitectureTests) + ".cs", StringComparison.Ordinal)))
        {
            var content = File.ReadAllText(sourcePath);
            var fileName = Path.GetFileName(sourcePath);

            if (content.Contains(privateBinding, StringComparison.Ordinal) ||
                content.Contains(reflectiveField, StringComparison.Ordinal) ||
                content.Contains(reflectiveAssignment, StringComparison.Ordinal) ||
                content.Contains(reflectiveInvocation, StringComparison.Ordinal))
            {
                violations.Add($"{fileName}: private-state reflection or reflective invocation is forbidden.");
                continue;
            }

            if (inventoryMarkers.Any(marker => content.Contains(marker, StringComparison.Ordinal)) &&
                !AllowedInventoryFiles.Contains(fileName))
            {
                violations.Add($"{fileName}: reflection must be a documented route, public-surface, or EF metadata inventory.");
            }
        }

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Locates the MedRecProTest project directory from the test assembly output path.
    /// </summary>
    /// <returns>Absolute path to the test project directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the project directory cannot be located.</exception>
    private static string findTestProjectDirectory()
    {
        #region implementation

        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MedRecProTest.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate MedRecProTest.csproj from the test assembly output directory.");

        #endregion
    }

    #endregion
}
