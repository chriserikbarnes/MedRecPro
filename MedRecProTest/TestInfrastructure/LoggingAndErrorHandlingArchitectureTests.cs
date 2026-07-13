using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace MedRecProTest.TestInfrastructure;

/**************************************************************/
/// <summary>
/// Guards the centralized HTTP error and structured-logging boundaries against regression.
/// </summary>
/// <remarks>
/// This lightweight source inventory deliberately checks the patterns that previously caused duplicate controller
/// error records, non-RFC-7807 500 bodies, and unqueryable message text. Behavioral tests own response and provider
/// semantics; this class keeps the removal inventory visible at review time.
/// </remarks>
/// <seealso cref="MedRecPro.Exceptions.MedRecProExceptionHandler"/>
/// <seealso cref="MedRecPro.Helpers.UserLoggerProvider"/>
[TestClass]
public class LoggingAndErrorHandlingArchitectureTests
{
    #region implementation

    private static readonly Regex interpolatedLoggerCall = new(
        @"\.Log(?:Trace|Debug|Information|Warning|Error|Critical)\s*\(\s*\$""",
        RegexOptions.Compiled);

    /**************************************************************/
    /// <summary>
    /// Verifies controller actions do not manufacture their own HTTP 500 result bodies and advertise centralized
    /// failures as RFC 7807 problem details.
    /// </summary>
    /// <remarks>
    /// Expected business outcomes can still return their own 4xx result. Unexpected failures must propagate to the
    /// global exception handler so one log record and one public contract are produced.
    /// </remarks>
    /// <seealso cref="MedRecPro.Exceptions.MedRecProExceptionHandler"/>
    [TestMethod]
    [TestCategory("Architecture")]
    public void Controllers_UnexpectedFailuresUseGlobalProblemDetailsBoundary()
    {
        #region implementation

        var controllerDirectory = Path.Combine(findRepositoryDirectory(), "MedRecPro", "Controllers");
        var violations = new List<string>();

        foreach (var sourcePath in Directory.GetFiles(controllerDirectory, "*Controller.cs", SearchOption.TopDirectoryOnly))
        {
            var content = File.ReadAllText(sourcePath);
            var fileName = Path.GetFileName(sourcePath);

            if (content.Contains("StatusCode(500", StringComparison.Ordinal) ||
                content.Contains("StatusCode(StatusCodes.Status500InternalServerError", StringComparison.Ordinal))
            {
                violations.Add($"{fileName}: explicit controller HTTP 500 result.");
            }

            foreach (Match match in Regex.Matches(
                         content,
                         @"\[ProducesResponseType\((?!typeof\(ProblemDetails\), StatusCodes\.Status500InternalServerError\))[^\]]*StatusCodes\.Status500InternalServerError[^\]]*\)\]"))
            {
                violations.Add($"{fileName}: 500 Swagger metadata must declare ProblemDetails ({match.Value}).");
            }
        }

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies production logger calls use stable structured templates rather than interpolated message text.
    /// </summary>
    /// <seealso cref="MedRecPro.Helpers.UserLoggerProvider"/>
    [TestMethod]
    [TestCategory("Architecture")]
    public void ProductionLogging_UsesStructuredTemplates()
    {
        #region implementation

        var productionDirectory = Path.Combine(findRepositoryDirectory(), "MedRecPro");
        var violations = Directory.GetFiles(productionDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => interpolatedLoggerCall.IsMatch(File.ReadAllText(path)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Locates the repository directory from the compiled test assembly output path.
    /// </summary>
    /// <returns>Absolute path containing the MedRecPro projects.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the repository cannot be located.</exception>
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
