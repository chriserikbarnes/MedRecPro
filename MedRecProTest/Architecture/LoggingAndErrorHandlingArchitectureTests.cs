using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace MedRecProTest.Architecture;

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
[TestCategory("Architecture")]
public class LoggingAndErrorHandlingArchitectureTests
{
    #region implementation

    private static readonly HashSet<string> loggerMethodNames = new(StringComparer.Ordinal)
    {
        "LogTrace",
        "LogDebug",
        "LogInformation",
        "LogWarning",
        "LogError",
        "LogCritical"
    };

    private static readonly IReadOnlyDictionary<string, int> controllerBroadCatchInventory =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["AdverseEventController.cs"] = 2,
            ["AuthController.cs"] = 1,
            ["LabelImportController.cs"] = 2,
            ["SettingsController.cs"] = 1,
            ["UsersController.cs"] = 7
        };

    private static readonly IReadOnlyDictionary<string, int> errorRethrowAllowlist =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.Combine("MedRecPro", "Service", "SplExportService.cs")] = 1
        };

    private static readonly string[] productionProjectNames =
    {
        "MedRecPro",
        "MedRecProConsole",
        "MedRecProImportClass",
        "MedRecProMCP",
        "MedRecProStatic"
    };

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
    public void ProductionLogging_UsesStructuredTemplates()
    {
        #region implementation

        var repositoryDirectory = findRepositoryDirectory();
        var violations = getProductionSourcePaths(repositoryDirectory)
            .SelectMany(sourcePath => getLoggerInvocations(sourcePath)
                .Where(invocation =>
                {
                    var messageExpression = getLoggerMessageExpression(invocation);
                    return messageExpression is InterpolatedStringExpressionSyntax ||
                           isRuntimeStringConcatenation(messageExpression);
                })
                .Select(invocation => $"{Path.GetRelativePath(repositoryDirectory, sourcePath)}:{getLineNumber(invocation)}"))
            .ToList();

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies every broad controller catch remains in the reviewed inventory and carries the mechanical allowlist marker.
    /// </summary>
    /// <remarks>
    /// The inventory intentionally pins catches that translate expected failures, own terminal background diagnostics, or preserve
    /// an established compatibility result. New broad catches require an explicit review and inventory update.
    /// </remarks>
    /// <seealso cref="MedRecPro.Exceptions.MedRecProExceptionHandler"/>
    [TestMethod]
    public void Controllers_BroadCatchesMatchReviewedAllowlist()
    {
        #region implementation

        var repositoryDirectory = findRepositoryDirectory();
        var controllerDirectory = Path.Combine(repositoryDirectory, "MedRecPro", "Controllers");
        var broadCatches = Directory.GetFiles(controllerDirectory, "*Controller.cs", SearchOption.TopDirectoryOnly)
            .SelectMany(sourcePath => getCompilationNodes<CatchClauseSyntax>(sourcePath)
                .Where(catchClause => catchClause.Declaration?.Type.ToString() == nameof(Exception))
                .Select(catchClause => new
                {
                    FileName = Path.GetFileName(sourcePath),
                    CatchClause = catchClause
                }))
            .ToList();

        var actualInventory = broadCatches
            .GroupBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var inventoryViolations = controllerBroadCatchInventory
            .Where(expected => !actualInventory.TryGetValue(expected.Key, out var actualCount) || actualCount != expected.Value)
            .Select(expected => $"{expected.Key}: expected {expected.Value}, actual {actualInventory.GetValueOrDefault(expected.Key)}")
            .Concat(actualInventory.Keys
                .Where(fileName => !controllerBroadCatchInventory.ContainsKey(fileName))
                .Select(fileName => $"{fileName}: unexpected {actualInventory[fileName]} broad catch(es)"))
            .ToList();
        var markerViolations = broadCatches
            .Where(item => !item.CatchClause.CatchKeyword.LeadingTrivia
                .ToFullString()
                .Contains("Broad-catch allowlist:", StringComparison.Ordinal))
            .Select(item => $"{item.FileName}:{getLineNumber(item.CatchClause)}: missing Broad-catch allowlist marker")
            .ToList();

        var violations = inventoryViolations.Concat(markerViolations).ToList();
        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies Error-plus-rethrow catches are limited to the reviewed, mechanically marked compatibility inventory.
    /// </summary>
    /// <remarks>
    /// Expected rethrows normally flow to the centralized HTTP handler or a background-operation boundary that owns the single
    /// error record. A retained site must provide diagnostic context required by a non-HTTP caller and carry the allowlist marker.
    /// </remarks>
    /// <seealso cref="MedRecPro.Exceptions.MedRecProExceptionHandler"/>
    [TestMethod]
    public void ProductionExceptions_ErrorRethrowsMatchReviewedAllowlist()
    {
        #region implementation

        var repositoryDirectory = findRepositoryDirectory();
        var productionDirectory = Path.Combine(repositoryDirectory, "MedRecPro");
        var errorRethrows = getSourcePaths(productionDirectory)
            .SelectMany(sourcePath =>
            {
                var catches = getCompilationNodes<CatchClauseSyntax>(sourcePath);

                return catches
                    .Where(catchClause => catchClause.Block.DescendantNodes()
                        .OfType<ThrowStatementSyntax>()
                        .Any(throwStatement => throwStatement.Expression == null &&
                            throwStatement.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault() == catchClause))
                    .Where(catchClause => catchClause.Block.DescendantNodes()
                        .OfType<InvocationExpressionSyntax>()
                        .Any(invocation => getInvokedMethodName(invocation) == "LogError" &&
                            invocation.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault() == catchClause))
                    .Select(catchClause => new
                    {
                        RelativePath = Path.GetRelativePath(repositoryDirectory, sourcePath),
                        CatchClause = catchClause
                    });
            })
            .ToList();

        var actualInventory = errorRethrows
            .GroupBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var inventoryViolations = errorRethrowAllowlist
            .Where(expected => !actualInventory.TryGetValue(expected.Key, out var actualCount) || actualCount != expected.Value)
            .Select(expected => $"{expected.Key}: expected {expected.Value}, actual {actualInventory.GetValueOrDefault(expected.Key)}")
            .Concat(actualInventory.Keys
                .Where(relativePath => !errorRethrowAllowlist.ContainsKey(relativePath))
                .Select(relativePath => $"{relativePath}: unexpected {actualInventory[relativePath]} Error-plus-rethrow catch(es)"))
            .ToList();
        var markerViolations = errorRethrows
            .Where(item => !item.CatchClause.Block
                .ToFullString()
                .Contains("Error-rethrow allowlist:", StringComparison.Ordinal))
            .Select(item => $"{item.RelativePath}:{getLineNumber(item.CatchClause)}: missing Error-rethrow allowlist marker")
            .ToList();

        var violations = inventoryViolations.Concat(markerViolations).ToList();
        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies MedRecPro production code uses the structured logging boundary instead of process-local debug output.
    /// </summary>
    /// <seealso cref="MedRecPro.Helpers.UserLoggerProvider"/>
    [TestMethod]
    public void MedRecProProduction_DoesNotUseDebugWrite()
    {
        #region implementation

        var repositoryDirectory = findRepositoryDirectory();
        var productionDirectory = Path.Combine(repositoryDirectory, "MedRecPro");
        var violations = getSourcePaths(productionDirectory)
            .SelectMany(sourcePath => getCompilationNodes<InvocationExpressionSyntax>(sourcePath)
                .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Expression.ToString() == nameof(System.Diagnostics.Debug) &&
                    memberAccess.Name.Identifier.ValueText is "Write" or "WriteLine")
                .Select(invocation => $"{Path.GetRelativePath(repositoryDirectory, sourcePath)}:{getLineNumber(invocation)}"))
            .ToList();

        Assert.AreEqual(0, violations.Count, string.Join(Environment.NewLine, violations));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Enumerates production C# source paths while excluding compiler output directories.
    /// </summary>
    /// <param name="repositoryDirectory">Absolute repository root containing the production projects.</param>
    /// <returns>The production C# source paths included in architecture scans.</returns>
    private static IEnumerable<string> getProductionSourcePaths(string repositoryDirectory)
    {
        #region implementation

        return productionProjectNames
            .Select(projectName => Path.Combine(repositoryDirectory, projectName))
            .Where(Directory.Exists)
            .SelectMany(getSourcePaths);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Enumerates C# source paths beneath one project directory while excluding compiler output directories.
    /// </summary>
    /// <param name="projectDirectory">Absolute project directory to scan.</param>
    /// <returns>The project C# source paths eligible for architecture checks.</returns>
    private static IEnumerable<string> getSourcePaths(string projectDirectory)
    {
        #region implementation

        return Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets logger extension invocations from one C# source file.
    /// </summary>
    /// <param name="sourcePath">Absolute source path to parse.</param>
    /// <returns>Logger invocations recognized by their standard extension-method names.</returns>
    private static IEnumerable<InvocationExpressionSyntax> getLoggerInvocations(string sourcePath)
    {
        #region implementation

        return getCompilationNodes<InvocationExpressionSyntax>(sourcePath)
            .Where(invocation => loggerMethodNames.Contains(getInvokedMethodName(invocation) ?? string.Empty));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets distinct syntax nodes from both Release-shaped and Debug-shaped preprocessing views of a source file.
    /// </summary>
    /// <typeparam name="TSyntax">Syntax node type to enumerate.</typeparam>
    /// <param name="sourcePath">Absolute source path to parse.</param>
    /// <returns>Distinct nodes keyed by their source spans across both compilation views.</returns>
    private static IEnumerable<TSyntax> getCompilationNodes<TSyntax>(string sourcePath)
        where TSyntax : SyntaxNode
    {
        #region implementation

        var sourceText = File.ReadAllText(sourcePath);
        var releaseRoot = CSharpSyntaxTree.ParseText(sourceText).GetRoot();
        var debugRoot = CSharpSyntaxTree.ParseText(
                sourceText,
                new CSharpParseOptions(preprocessorSymbols: new[] { "DEBUG" }))
            .GetRoot();

        return new[] { releaseRoot, debugRoot }
            .SelectMany(root => root.DescendantNodes().OfType<TSyntax>())
            .GroupBy(node => node.Span)
            .Select(group => group.First());

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the invoked method name from member-access or conditional-access syntax.
    /// </summary>
    /// <param name="invocation">Invocation syntax to inspect.</param>
    /// <returns>The invoked method name, or <see langword="null"/> when the syntax is not a supported call shape.</returns>
    private static string? getInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        #region implementation

        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Locates the message-template expression in a standard logger extension invocation.
    /// </summary>
    /// <param name="invocation">Logger invocation to inspect.</param>
    /// <returns>The first string-shaped argument used as the message template, or <see langword="null"/>.</returns>
    private static ExpressionSyntax? getLoggerMessageExpression(InvocationExpressionSyntax invocation)
    {
        #region implementation

        return invocation.ArgumentList.Arguments
            .Select(argument => argument.Expression)
            .FirstOrDefault(expression => expression is InterpolatedStringExpressionSyntax ||
                expression.IsKind(SyntaxKind.StringLiteralExpression) ||
                expression is BinaryExpressionSyntax binaryExpression &&
                binaryExpression.IsKind(SyntaxKind.AddExpression) &&
                binaryExpression.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>()
                    .Any(literal => literal.IsKind(SyntaxKind.StringLiteralExpression)));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Determines whether a message expression concatenates runtime values instead of using structured placeholders.
    /// </summary>
    /// <param name="expression">Message expression to classify.</param>
    /// <returns><see langword="true"/> for runtime string concatenation; otherwise <see langword="false"/>.</returns>
    private static bool isRuntimeStringConcatenation(ExpressionSyntax? expression)
    {
        #region implementation

        return expression is BinaryExpressionSyntax binaryExpression &&
               binaryExpression.IsKind(SyntaxKind.AddExpression) &&
               binaryExpression.DescendantNodesAndSelf()
                   .Where(node => node is not BinaryExpressionSyntax)
                   .OfType<ExpressionSyntax>()
                   .Any(operand => !operand.IsKind(SyntaxKind.StringLiteralExpression));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the one-based source line for a syntax node.
    /// </summary>
    /// <param name="node">Syntax node whose source location is required.</param>
    /// <returns>The one-based line number.</returns>
    private static int getLineNumber(SyntaxNode node)
    {
        #region implementation

        return node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Locates the repository directory from build-time assembly metadata.
    /// </summary>
    /// <returns>Absolute path containing the MedRecPro projects.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the repository cannot be located.</exception>
    private static string findRepositoryDirectory()
    {
        #region implementation

        return RepositorySourceRoot.RootPath;

        #endregion
    }

    #endregion
}
