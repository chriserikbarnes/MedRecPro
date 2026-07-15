using System.Reflection;

namespace MedRecProTest.TestInfrastructure;

/**************************************************************/
/// <summary>
/// Resolves repository source from build-time assembly metadata.
/// </summary>
/// <remarks>
/// Source-inventory tests use this marker instead of deriving repository layout from
/// the compiled output directory, so isolated <c>BaseOutputPath</c> runs remain valid.
/// </remarks>
/// <seealso cref="AssemblyMetadataAttribute"/>
internal static class RepositorySourceRoot
{
    private const string MetadataKey = "RepositoryRoot";

    /**************************************************************/
    /// <summary>
    /// Gets the canonical repository root embedded when the test assembly was built.
    /// </summary>
    /// <remarks>
    /// The path is validated against both the solution and test-project markers before
    /// it is exposed to source-inventory tests.
    /// </remarks>
    /// <seealso cref="Resolve(Assembly)"/>
    internal static string RootPath { get; } = Resolve(typeof(RepositorySourceRoot).Assembly);

    /**************************************************************/
    /// <summary>
    /// Resolves and validates the repository marker from an assembly.
    /// </summary>
    /// <remarks>
    /// A rebuild-oriented diagnostic is returned for missing or stale metadata. The
    /// resolver intentionally has no current-directory or ancestor-walk fallback.
    /// </remarks>
    /// <param name="assembly">Assembly containing the repository-root marker.</param>
    /// <returns>The canonical validated repository root.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the marker is missing or invalid.</exception>
    /// <seealso cref="AssemblyMetadataAttribute"/>
    internal static string Resolve(Assembly assembly)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(assembly);

        var markedPath = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute => string.Equals(attribute.Key, MetadataKey, StringComparison.Ordinal))
            ?.Value;
        var attemptedPath = string.IsNullOrWhiteSpace(markedPath)
            ? "<missing>"
            : Path.GetFullPath(markedPath);

        if (!string.IsNullOrWhiteSpace(markedPath) &&
            File.Exists(Path.Combine(attemptedPath, "MedRecPro.sln")) &&
            File.Exists(Path.Combine(attemptedPath, "MedRecProTest", "MedRecProTest.csproj")))
        {
            return new DirectoryInfo(attemptedPath).FullName;
        }

        throw new InvalidOperationException(
            $"Repository source metadata '{MetadataKey}' is missing or stale. " +
            $"Attempted path: '{attemptedPath}'. Rebuild MedRecProTest so the repository marker is regenerated.");

        #endregion
    }
}
