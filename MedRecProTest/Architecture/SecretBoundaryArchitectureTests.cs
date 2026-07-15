using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Architecture;

/**************************************************************/
/// <summary>
/// Guards raw primary-key-secret configuration reads across every production project.
/// </summary>
/// <remarks>
/// This source-level guard records each deferred reader independently so a new raw
/// configuration access cannot hide inside an already reviewed file. It parses both
/// Debug and Release views to keep compiler-conditional code inside the inventory.
/// </remarks>
/// <seealso cref="MedRecPro.Configuration.DatabaseSecurityOptions"/>
/// <seealso cref="MedRecPro.Service.IPrimaryKeyCipher"/>
[TestClass]
[TestCategory("Architecture")]
public class SecretBoundaryArchitectureTests
{
    #region implementation

    private const string primaryKeySecretPath = "Security:DB:PKSecret";

    private static readonly string[] productionProjectNames =
    {
        "MedRecPro",
        "MedRecProImportClass",
        "MedRecProMCP",
        "MedRecProConsole",
        "MedRecProStatic"
    };

    private static readonly string[] expectedSecretReadOccurrenceIdentities =
    {
        "MedRecPro/Controllers/AdverseEventController.cs|AdverseEventController..ctor|GetSection|1",
        "MedRecPro/Controllers/SettingsController.cs|SettingsController..ctor|GetSection|1",
        "MedRecPro/Controllers/UsersController.cs|UsersController..ctor|Indexer|1",
        "MedRecPro/DataAccess/RepositoryDataAccess.cs|Repository.getPkSecret|GetSection|1",
        "MedRecPro/DataAccess/UserDataAccess.cs|UserDataAccess.getPkSecret|GetSection|1",
        "MedRecPro/Models/LabelDto.cs|SectionDto..ctor|GetSection|1",
        "MedRecPro/Models/LabelDto.cs|SectionHierarchyDto..ctor|GetSection|1",
        "MedRecPro/Models/LabelDto.cs|StructuredBodyDto..ctor|GetSection|1",
        "MedRecPro/Models/User.cs|User.getPkSecret|Indexer|1",
        "MedRecPro/Service/CommonService.cs|EncryptionService..ctor|GetSection|1",
        "MedRecPro/Service/Label/LabelQueryServices.cs|LabelQueryServiceBase..ctor|GetSection|1",
        "MedRecPro/Service/PermissionService.cs|PermissionService..ctor|Indexer|1",
        "MedRecPro/Service/SplDataService.cs|SplDataService..ctor|Indexer|1",
        "MedRecProImportClass/DataAccess/RepositoryDataAccess.cs|Repository.getPkSecret|GetSection|1",
        "MedRecProImportClass/DataAccess/UserDataAccess.cs|UserDataAccess.getPkSecret|GetSection|1",
        "MedRecProImportClass/Models/LabelDto.cs|SectionDto..ctor|GetSection|1",
        "MedRecProImportClass/Models/LabelDto.cs|SectionHierarchyDto..ctor|GetSection|1",
        "MedRecProImportClass/Models/LabelDto.cs|StructuredBodyDto..ctor|GetSection|1",
        "MedRecProImportClass/Models/User.cs|User.getPkSecret|Indexer|1",
        "MedRecProImportClass/Service/SplDataService.cs|SplDataService..ctor|Indexer|1",
        "MedRecProMCP/Services/UserResolutionService.cs|UserResolutionService..ctor|Indexer|1"
    };

    /**************************************************************/
    /// <summary>
    /// Verifies every MedRecPro C# project is classified for secret-boundary scanning.
    /// </summary>
    /// <remarks>
    /// The production projects are intentionally declared rather than inferred from
    /// <c>MedRecPro.sln</c>, because the MCP, console, and static projects are outside
    /// that solution. A new project must be classified explicitly before this test passes.
    /// </remarks>
    [TestMethod]
    public void ProductionProjects_AreExplicitlyClassifiedForSecretBoundaryScanning()
    {
        #region implementation

        var repositoryDirectory = findRepositoryDirectory();
        var actualProjectNames = Directory.GetDirectories(repositoryDirectory)
            .Select(directory => Path.Combine(directory, $"{Path.GetFileName(directory)}.csproj"))
            .Where(File.Exists)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(projectName => projectName.StartsWith("MedRecPro", StringComparison.Ordinal))
            .OrderBy(projectName => projectName, StringComparer.Ordinal)
            .ToArray();
        var expectedProjectNames = productionProjectNames
            .Append("MedRecProTest")
            .OrderBy(projectName => projectName, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            expectedProjectNames,
            actualProjectNames,
            "Every MedRecPro C# project must be classified as production or test before secret-boundary scanning can continue.");
        Assert.IsTrue(
            productionProjectNames.All(projectName => Directory.Exists(Path.Combine(repositoryDirectory, projectName))),
            "Every classified production project must have a source directory.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies deferred raw primary-key-secret reads match the reviewed occurrence-level inventory.
    /// </summary>
    /// <remarks>
    /// Identity is independent of line numbers and includes project-relative path, containing owner,
    /// normalized configuration access shape, and an ordinal for identical accesses in one owner.
    /// That ordinal prevents a second same-file read from being masked by a file-level allowlist.
    /// </remarks>
    [TestMethod]
    public void ProductionRawPrimaryKeySecretReads_MatchReviewedOccurrenceAllowlist()
    {
        #region implementation

        var scanResult = scanProductionSources(findRepositoryDirectory());

        Assert.AreEqual(0, scanResult.Diagnostics.Count, string.Join(Environment.NewLine, scanResult.Diagnostics));
        var expected = expectedSecretReadOccurrenceIdentities.OrderBy(identity => identity, StringComparer.Ordinal).ToArray();
        var actual = scanResult.Occurrences.Select(occurrence => occurrence.Identity).OrderBy(identity => identity, StringComparer.Ordinal).ToArray();
        var missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
        var unexpected = actual.Except(expected, StringComparer.Ordinal).ToArray();

        Assert.AreEqual(
            0,
            missing.Length + unexpected.Length,
            $"Raw primary-key-secret reader inventory drifted. Add an approved occurrence-level deferral or migrate the reader through IPrimaryKeyCipher.{Environment.NewLine}" +
            $"Missing:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}{Environment.NewLine}" +
            $"Unexpected:{Environment.NewLine}{string.Join(Environment.NewLine, unexpected)}");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies every supported direct and composed raw-secret read is detected in both compilation views.
    /// </summary>
    /// <remarks>
    /// The partitions cover direct indexer, section, and value access; composed sections; nested sections;
    /// inline string concatenation; and local or field constant concatenation.
    /// </remarks>
    [TestMethod]
    public void SecretReadDetector_DetectsSupportedDirectAndComposedAccessesInBothCompilationViews()
    {
        #region implementation

        var positivePartitions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["direct indexer"] = "class Example { string Read() => configuration[\"Security:DB:PKSecret\"]; }",
            ["direct section"] = "class Example { string Read() => configuration.GetSection(\"Security:DB:PKSecret\").Value; }",
            ["conditional section"] = "class Example { string Read() => configuration?.GetSection(\"Security:DB:PKSecret\").Value; }",
            ["direct value"] = "class Example { string Read() => configuration.GetValue<string>(\"Security:DB:PKSecret\"); }",
            ["composed section indexer"] = "class Example { string Read() => configuration.GetSection(\"Security:DB\")[\"PKSecret\"]; }",
            ["composed section value"] = "class Example { string Read() => configuration.GetSection(\"Security:DB\").GetValue<string>(\"PKSecret\"); }",
            ["nested sections"] = "class Example { string Read() => configuration.GetSection(\"Security\").GetSection(\"DB\").GetValue<string>(\"PKSecret\"); }",
            ["inline concatenation"] = "class Example { string Read() => configuration[\"Security:\" + \"DB:\" + \"PKSecret\"]; }",
            ["local constant concatenation"] = "class Example { string Read() { const string section = \"Security:DB\"; const string key = section + \":PKSecret\"; return configuration[key]; } }",
            ["field constant concatenation"] = "class Example { const string Section = \"Security\"; const string Key = Section + \":DB:PKSecret\"; string Read() => configuration[Key]; }"
        };

        foreach (var parseView in getParseViews())
        {
            foreach (var partition in positivePartitions)
            {
                var scanResult = scanSource("Synthetic", $"{partition.Key}.cs", partition.Value, parseView.PreprocessorSymbols);

                Assert.AreEqual(1, scanResult.Occurrences.Count, $"{parseView.Name}: {partition.Key}");
                Assert.AreEqual(0, scanResult.Diagnostics.Count, $"{parseView.Name}: {partition.Key}: {string.Join(Environment.NewLine, scanResult.Diagnostics)}");
            }
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies non-reader mentions of the primary-key-secret path do not create false positives.
    /// </summary>
    /// <remarks>
    /// Documentation, comments, diagnostics, unrelated keys, dictionary initialization, and configuration assignment
    /// describe or write a value; they do not retrieve the raw secret.
    /// </remarks>
    [TestMethod]
    public void SecretReadDetector_ExcludesNonReaderMentionsAndConfigurationWritesInBothCompilationViews()
    {
        #region implementation

        const string source = """
            /// <summary>Security:DB:PKSecret documentation.</summary>
            class Example
            {
                void Write()
                {
                    // Security:DB:PKSecret comment.
                    var diagnostic = "Security:DB:PKSecret missing";
                    var values = new Dictionary<string, string> { ["Security:DB:PKSecret"] = "configured" };
                    configuration["Security:DB:PKSecret"] = "configured";
                    var unrelated = configuration["Security:DB:Connection"];
                }
            }
            """;

        foreach (var parseView in getParseViews())
        {
            var scanResult = scanSource("Synthetic", "NegativePartitions.cs", source, parseView.PreprocessorSymbols);

            Assert.AreEqual(0, scanResult.Occurrences.Count, parseView.Name);
            Assert.AreEqual(0, scanResult.Diagnostics.Count, parseView.Name);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies multiple raw-secret reads in one source file remain independently inventoried.
    /// </summary>
    [TestMethod]
    public void SecretReadDetector_ReturnsDistinctOccurrencesForTwoReadsInOneMember()
    {
        #region implementation

        const string source = """
            class Example
            {
                string Read()
                {
                    var first = configuration["Security:DB:PKSecret"];
                    return configuration.GetSection("Security:DB:PKSecret").Value;
                }
            }
            """;

        var scanResult = scanSource("Synthetic", "TwoReads.cs", source, Array.Empty<string>());

        Assert.AreEqual(2, scanResult.Occurrences.Count);
        Assert.AreEqual(2, scanResult.Occurrences.Select(occurrence => occurrence.Identity).Distinct(StringComparer.Ordinal).Count());

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies unresolved expressions that visibly compose the primary-key-secret path produce a detector diagnostic.
    /// </summary>
    [TestMethod]
    public void SecretReadDetector_ReportsSecretShapedPathThatCannotBeConstantFolded()
    {
        #region implementation

        const string source = "class Example { string Read(string key) => configuration.GetSection(\"Security:DB\").GetValue<string>(key); }";

        foreach (var parseView in getParseViews())
        {
            var scanResult = scanSource("Synthetic", "DynamicSecretPath.cs", source, parseView.PreprocessorSymbols);

            Assert.AreEqual(0, scanResult.Occurrences.Count, parseView.Name);
            Assert.AreEqual(1, scanResult.Diagnostics.Count, parseView.Name);
            StringAssert.Contains(scanResult.Diagnostics.Single(), "cannot be constant-folded", StringComparison.Ordinal);
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Verifies the options binding and singleton cipher registration retain one production source owner each.
    /// </summary>
    /// <remarks>
    /// Runtime lifetime verification remains in <see cref="ServiceRegistrationTests"/>. This complementary source
    /// inventory prevents a second raw options binding or cipher singleton registration from being introduced silently.
    /// </remarks>
    /// <seealso cref="ServiceRegistrationTests"/>
    [TestMethod]
    public void DatabaseSecurityOptionsAndPrimaryKeyCipher_KeepSingleProductionBindingAndRegistration()
    {
        #region implementation

        var syntaxTrees = getProductionSyntaxTrees(findRepositoryDirectory(), Array.Empty<string>());
        var invocations = syntaxTrees.SelectMany(sourceFile => sourceFile.SyntaxTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()).ToList();
        var databaseSecurityBindings = invocations.Where(isDatabaseSecurityOptionsBinding).ToArray();
        var cipherRegistrations = invocations.Where(isPrimaryKeyCipherSingletonRegistration).ToArray();

        Assert.AreEqual(1, databaseSecurityBindings.Length, "DatabaseSecurityOptions must have exactly one production binding point.");
        Assert.AreEqual(1, cipherRegistrations.Length, "IPrimaryKeyCipher must have exactly one singleton registration.");

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Scans all classified production projects for raw secret reads across Debug and Release parse views.
    /// </summary>
    /// <param name="repositoryDirectory">Absolute repository path containing the classified projects.</param>
    /// <returns>Deduplicated raw-read occurrences and unresolved-path diagnostics.</returns>
    private static SecretBoundaryScanResult scanProductionSources(string repositoryDirectory)
    {
        #region implementation

        var scanResults = getParseViews()
            .Select(parseView => scanSyntaxTrees(getProductionSyntaxTrees(repositoryDirectory, parseView.PreprocessorSymbols)))
            .ToArray();
        var occurrences = scanResults
            .SelectMany(scanResult => scanResult.Occurrences)
            .GroupBy(occurrence => $"{occurrence.ProjectName}|{occurrence.RelativePath}|{occurrence.SpanStart}|{occurrence.AccessShape}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        var diagnostics = scanResults
            .SelectMany(scanResult => scanResult.Diagnostics)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(diagnostic => diagnostic, StringComparer.Ordinal)
            .ToList();

        return new SecretBoundaryScanResult(assignOccurrenceOrdinals(occurrences), diagnostics);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Scans one synthetic source snippet with a selected preprocessor view.
    /// </summary>
    /// <param name="projectName">Synthetic project identity used by the occurrence inventory.</param>
    /// <param name="relativePath">Synthetic project-relative source path.</param>
    /// <param name="source">C# source to inspect.</param>
    /// <param name="preprocessorSymbols">Preprocessor symbols active for the parse view.</param>
    /// <returns>Detected occurrences and diagnostics for the supplied source.</returns>
    private static SecretBoundaryScanResult scanSource(string projectName, string relativePath, string source, IEnumerable<string> preprocessorSymbols)
    {
        #region implementation

        var parseOptions = new CSharpParseOptions(preprocessorSymbols: preprocessorSymbols);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, relativePath);
        var sourceFile = new SecretBoundarySourceFile(projectName, relativePath, syntaxTree);
        var scanResult = scanSyntaxTrees(new[] { sourceFile });

        return new SecretBoundaryScanResult(assignOccurrenceOrdinals(scanResult.Occurrences), scanResult.Diagnostics);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Scans parsed source trees with semantic constant evaluation for configuration access paths.
    /// </summary>
    /// <param name="sourceFiles">Parsed production or synthetic source files in one compilation view.</param>
    /// <returns>Unindexed raw-read occurrences and diagnostics.</returns>
    private static SecretBoundaryScanResult scanSyntaxTrees(IReadOnlyCollection<SecretBoundarySourceFile> sourceFiles)
    {
        #region implementation

        var compilation = CSharpCompilation.Create(
            "SecretBoundaryArchitectureGuard",
            sourceFiles.Select(sourceFile => sourceFile.SyntaxTree),
            getMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var occurrences = new List<SecretReadOccurrence>();
        var diagnostics = new List<string>();

        foreach (var sourceFile in sourceFiles)
        {
            var semanticModel = compilation.GetSemanticModel(sourceFile.SyntaxTree);
            var root = sourceFile.SyntaxTree.GetRoot();

            foreach (var elementAccess in root.DescendantNodes().OfType<ElementAccessExpressionSyntax>())
            {
                inspectConfigurationRead(sourceFile, semanticModel, elementAccess, getElementAccessReceiver(elementAccess), elementAccess.ArgumentList.Arguments.FirstOrDefault()?.Expression, "Indexer", occurrences, diagnostics);
            }

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var methodName = getInvokedMethodName(invocation);
                if (methodName is not "GetSection" and not "GetValue")
                {
                    continue;
                }

                var accessShape = methodName == "GetValue" ? getGetValueAccessShape(invocation) : methodName;
                inspectConfigurationRead(sourceFile, semanticModel, invocation, getInvocationReceiver(invocation), invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression, accessShape, occurrences, diagnostics);
            }
        }

        return new SecretBoundaryScanResult(occurrences, diagnostics);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Classifies one configuration indexer, section call, or value call as a raw-secret read or unresolved diagnostic.
    /// </summary>
    /// <param name="sourceFile">Source file that owns the candidate expression.</param>
    /// <param name="semanticModel">Semantic model used to evaluate constant path expressions.</param>
    /// <param name="candidate">Potential configuration-reading syntax node.</param>
    /// <param name="receiver">Configuration or configuration-section receiver.</param>
    /// <param name="keyExpression">Key or section argument supplied by the candidate.</param>
    /// <param name="accessShape">Normalized read shape used in occurrence identity.</param>
    /// <param name="occurrences">Mutable collection receiving detected raw-reader occurrences.</param>
    /// <param name="diagnostics">Mutable collection receiving unresolved secret-shaped path diagnostics.</param>
    private static void inspectConfigurationRead(
        SecretBoundarySourceFile sourceFile,
        SemanticModel semanticModel,
        SyntaxNode candidate,
        ExpressionSyntax? receiver,
        ExpressionSyntax? keyExpression,
        string accessShape,
        ICollection<SecretReadOccurrence> occurrences,
        ICollection<string> diagnostics)
    {
        #region implementation

        if (receiver == null || keyExpression == null || isAssignmentTarget(candidate))
        {
            return;
        }

        var receiverPath = resolveConfigurationPath(receiver, semanticModel);
        if (!receiverPath.IsConfiguration)
        {
            return;
        }

        var key = getConstantString(keyExpression, semanticModel);
        if (key == null || receiverPath.Path == null)
        {
            if (isSecretShapedPath(candidate, receiverPath.Path))
            {
                diagnostics.Add($"{sourceFile.ProjectName}/{sourceFile.RelativePath}:{getLineNumber(candidate)}: secret-key-shaped configuration expression cannot be constant-folded ({candidate}).");
            }

            return;
        }

        var resolvedPath = combineConfigurationPath(receiverPath.Path, key);
        if (!string.Equals(resolvedPath, primaryKeySecretPath, StringComparison.Ordinal))
        {
            return;
        }

        occurrences.Add(new SecretReadOccurrence(
            sourceFile.ProjectName,
            sourceFile.RelativePath,
            getContainingOwner(candidate),
            accessShape,
            candidate.SpanStart,
            getLineNumber(candidate),
            0));

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Resolves a configuration root or section expression to its compile-time path.
    /// </summary>
    /// <param name="expression">Configuration or section expression to resolve.</param>
    /// <param name="semanticModel">Semantic model used for compile-time string evaluation.</param>
    /// <returns>Whether the expression is configuration-shaped and its resolved path when available.</returns>
    private static ConfigurationPathResolution resolveConfigurationPath(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        #region implementation

        expression = unwrapParentheses(expression);

        if (expression is InvocationExpressionSyntax invocation && getInvokedMethodName(invocation) == "GetSection")
        {
            var receiver = getInvocationReceiver(invocation);
            var parentResolution = receiver == null
                ? ConfigurationPathResolution.NotConfiguration
                : resolveConfigurationPath(receiver, semanticModel);
            var sectionKey = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            var section = sectionKey == null ? null : getConstantString(sectionKey, semanticModel);

            if (!parentResolution.IsConfiguration)
            {
                return ConfigurationPathResolution.NotConfiguration;
            }

            return section == null || parentResolution.Path == null
                ? new ConfigurationPathResolution(true, null)
                : new ConfigurationPathResolution(true, combineConfigurationPath(parentResolution.Path, section));
        }

        if (expression is MemberAccessExpressionSyntax memberAccess &&
            string.Equals(memberAccess.Name.Identifier.ValueText, "Configuration", StringComparison.Ordinal))
        {
            return new ConfigurationPathResolution(true, string.Empty);
        }

        if (isConfigurationIdentifier(expression, semanticModel))
        {
            return new ConfigurationPathResolution(true, string.Empty);
        }

        return ConfigurationPathResolution.NotConfiguration;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Determines whether an expression represents a configuration root based on resolved type or established naming.
    /// </summary>
    /// <param name="expression">Expression to classify.</param>
    /// <param name="semanticModel">Semantic model that may resolve the expression type.</param>
    /// <returns><c>true</c> when the expression is an IConfiguration-shaped source.</returns>
    private static bool isConfigurationIdentifier(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        #region implementation

        var type = semanticModel.GetTypeInfo(expression).Type;
        if (type != null && type.TypeKind != TypeKind.Error)
        {
            if (type.Name is "IConfiguration" or "IConfigurationRoot" or "IConfigurationSection")
            {
                return true;
            }

            if (type.Name.Contains("Dictionary", StringComparison.Ordinal))
            {
                return false;
            }
        }

        var identifier = expression switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => string.Empty
        };

        return identifier.Contains("configuration", StringComparison.OrdinalIgnoreCase) ||
            identifier.EndsWith("config", StringComparison.OrdinalIgnoreCase);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets a compile-time string value through Roslyn semantic constant evaluation.
    /// </summary>
    /// <param name="expression">String expression that may be literal or const-composed.</param>
    /// <param name="semanticModel">Semantic model that evaluates constants across local and field declarations.</param>
    /// <returns>The evaluated string value, or <see langword="null"/> when it is runtime-composed.</returns>
    private static string? getConstantString(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        #region implementation

        var constantValue = semanticModel.GetConstantValue(expression);
        return constantValue.HasValue && constantValue.Value is string value ? value : null;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Determines whether a candidate is an indexer assignment target rather than a configuration read.
    /// </summary>
    /// <param name="candidate">Configuration candidate syntax node.</param>
    /// <returns><c>true</c> when the candidate writes a configuration value.</returns>
    private static bool isAssignmentTarget(SyntaxNode candidate)
    {
        #region implementation

        return candidate.Parent is AssignmentExpressionSyntax assignment && assignment.Left == candidate;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Determines whether an unresolved candidate visibly contains the primary-key-secret path or path prefix.
    /// </summary>
    /// <param name="candidate">Candidate syntax whose source text may expose a secret-shaped expression.</param>
    /// <param name="resolvedReceiverPath">Resolved receiver path before the unresolved key segment.</param>
    /// <returns><c>true</c> when silently ignoring the dynamic expression could hide a raw-secret read.</returns>
    private static bool isSecretShapedPath(SyntaxNode candidate, string? resolvedReceiverPath)
    {
        #region implementation

        var candidateText = candidate.ToString();
        return string.Equals(resolvedReceiverPath, "Security:DB", StringComparison.Ordinal) ||
            candidateText.Contains("PKSecret", StringComparison.OrdinalIgnoreCase) ||
            candidateText.Contains("Security:DB", StringComparison.OrdinalIgnoreCase);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Combines a configuration section path and a child key using configuration's colon separator.
    /// </summary>
    /// <param name="parentPath">Resolved parent configuration path.</param>
    /// <param name="childKey">Resolved child section or key.</param>
    /// <returns>The resolved full configuration path.</returns>
    private static string combineConfigurationPath(string parentPath, string childKey)
    {
        #region implementation

        return string.IsNullOrEmpty(parentPath) ? childKey : $"{parentPath}:{childKey}";

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the receiver expression of a configuration method invocation, including conditional access.
    /// </summary>
    /// <param name="invocation">Method invocation whose receiver is required.</param>
    /// <returns>The receiver expression, or <see langword="null"/> for unsupported invocation shapes.</returns>
    private static ExpressionSyntax? getInvocationReceiver(InvocationExpressionSyntax invocation)
    {
        #region implementation

        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Expression,
            MemberBindingExpressionSyntax => invocation.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression,
            _ => null
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the receiver expression of an indexer access.
    /// </summary>
    /// <param name="elementAccess">Indexer syntax to inspect.</param>
    /// <returns>The expression indexed by the candidate access.</returns>
    private static ExpressionSyntax getElementAccessReceiver(ElementAccessExpressionSyntax elementAccess)
    {
        #region implementation

        return elementAccess.Expression;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the simple invoked method name from ordinary or conditional member access.
    /// </summary>
    /// <param name="invocation">Invocation syntax to inspect.</param>
    /// <returns>The method name, or <see langword="null"/> when the shape is unsupported.</returns>
    private static string? getInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        #region implementation

        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax genericName => genericName.Identifier.ValueText,
            _ => null
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets a normalized GetValue access shape, including its type argument when supplied.
    /// </summary>
    /// <param name="invocation">GetValue invocation to normalize.</param>
    /// <returns>Stable method-and-type access description for the occurrence identity.</returns>
    private static string getGetValueAccessShape(InvocationExpressionSyntax invocation)
    {
        #region implementation

        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            _ => null
        };

        return name is GenericNameSyntax genericName
            ? $"GetValue<{string.Join(",", genericName.TypeArgumentList.Arguments.Select(argument => argument.ToString()))}>"
            : "GetValue";

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Assigns stable per-owner access ordinals after source spans have been deduplicated across parse views.
    /// </summary>
    /// <param name="occurrences">Raw occurrences to normalize into occurrence-level inventory entries.</param>
    /// <returns>Occurrences carrying ordinals that distinguish identical reads in one owner.</returns>
    private static IReadOnlyList<SecretReadOccurrence> assignOccurrenceOrdinals(IEnumerable<SecretReadOccurrence> occurrences)
    {
        #region implementation

        return occurrences
            .GroupBy(occurrence => $"{occurrence.ProjectName}|{occurrence.RelativePath}|{occurrence.Owner}|{occurrence.AccessShape}", StringComparer.Ordinal)
            .SelectMany(group => group.OrderBy(occurrence => occurrence.SpanStart)
                .Select((occurrence, index) => occurrence with { OccurrenceOrdinal = index + 1 }))
            .OrderBy(occurrence => occurrence.Identity, StringComparer.Ordinal)
            .ToArray();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the containing type and member for occurrence-level allowlist identity.
    /// </summary>
    /// <param name="node">Candidate raw-secret access node.</param>
    /// <returns>Stable type/member owner description without line-number dependence.</returns>
    private static string getContainingOwner(SyntaxNode node)
    {
        #region implementation

        var typeName = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "<top-level>";
        var method = node.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
        if (method != null)
        {
            var methodName = method switch
            {
                ConstructorDeclarationSyntax => ".ctor",
                MethodDeclarationSyntax methodDeclaration => methodDeclaration.Identifier.ValueText,
                OperatorDeclarationSyntax operatorDeclaration => operatorDeclaration.OperatorToken.ValueText,
                ConversionOperatorDeclarationSyntax conversionOperator => conversionOperator.ImplicitOrExplicitKeyword.ValueText,
                DestructorDeclarationSyntax => "Finalize",
                _ => method.Kind().ToString()
            };
            return $"{typeName}.{methodName}";
        }

        var property = node.Ancestors().OfType<PropertyDeclarationSyntax>().FirstOrDefault();
        if (property != null)
        {
            return $"{typeName}.{property.Identifier.ValueText}";
        }

        var field = node.Ancestors().OfType<FieldDeclarationSyntax>().FirstOrDefault();
        return field == null
            ? $"{typeName}.<initializer>"
            : $"{typeName}.{field.Declaration.Variables.FirstOrDefault()?.Identifier.ValueText ?? "<field>"}";

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets the one-based line number for a syntax node's source location.
    /// </summary>
    /// <param name="node">Syntax node whose line number is required.</param>
    /// <returns>One-based source line number used only for diagnostics.</returns>
    private static int getLineNumber(SyntaxNode node)
    {
        #region implementation

        return node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Gets Debug and Release preprocessor configurations used by source architecture guards.
    /// </summary>
    /// <returns>Named parse views with their active preprocessor symbols.</returns>
    private static IEnumerable<ParseView> getParseViews()
    {
        #region implementation

        return new[]
        {
            new ParseView("Release", Array.Empty<string>()),
            new ParseView("Debug", new[] { "DEBUG" })
        };

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Parses all C# source files beneath the explicitly classified production projects.
    /// </summary>
    /// <param name="repositoryDirectory">Absolute repository path containing the production projects.</param>
    /// <param name="preprocessorSymbols">Preprocessor symbols active for the requested compilation view.</param>
    /// <returns>Parsed production source files keyed by project and project-relative path.</returns>
    private static IReadOnlyCollection<SecretBoundarySourceFile> getProductionSyntaxTrees(string repositoryDirectory, IEnumerable<string> preprocessorSymbols)
    {
        #region implementation

        var parseOptions = new CSharpParseOptions(preprocessorSymbols: preprocessorSymbols);

        return productionProjectNames
            .SelectMany(projectName => getProjectSourcePaths(repositoryDirectory, projectName)
                .Select(sourcePath => new SecretBoundarySourceFile(
                    projectName,
                    Path.GetRelativePath(Path.Combine(repositoryDirectory, projectName), sourcePath).Replace(Path.DirectorySeparatorChar, '/'),
                    CSharpSyntaxTree.ParseText(File.ReadAllText(sourcePath), parseOptions, sourcePath))))
            .ToArray();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Enumerates one production project's C# source files while excluding compiler outputs.
    /// </summary>
    /// <param name="repositoryDirectory">Absolute repository path containing the project.</param>
    /// <param name="projectName">Explicitly classified production project name.</param>
    /// <returns>Ordered absolute C# source paths in the project.</returns>
    private static IEnumerable<string> getProjectSourcePaths(string repositoryDirectory, string projectName)
    {
        #region implementation

        var projectDirectory = Path.Combine(repositoryDirectory, projectName);
        return Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Creates the metadata references required for Roslyn semantic constant evaluation.
    /// </summary>
    /// <returns>Framework metadata references sufficient to resolve string constants and collections.</returns>
    private static IReadOnlyCollection<MetadataReference> getMetadataReferences()
    {
        #region implementation

        return new[]
            {
                typeof(object).Assembly.Location,
                typeof(Console).Assembly.Location,
                typeof(Enumerable).Assembly.Location,
                typeof(Dictionary<,>).Assembly.Location
            }
            .Distinct(StringComparer.Ordinal)
            .Select(location => MetadataReference.CreateFromFile(location))
            .ToArray();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Determines whether an invocation is the one approved DatabaseSecurityOptions configuration binding.
    /// </summary>
    /// <param name="invocation">Production invocation to classify.</param>
    /// <returns><c>true</c> when the invocation retrieves the DatabaseSecurityOptions section.</returns>
    private static bool isDatabaseSecurityOptionsBinding(InvocationExpressionSyntax invocation)
    {
        #region implementation

        return getInvokedMethodName(invocation) == "GetSection" &&
            invocation.ArgumentList.Arguments.Count == 1 &&
            string.Equals(invocation.ArgumentList.Arguments[0].Expression.ToString(), "DatabaseSecurityOptions.SectionName", StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Determines whether an invocation registers IPrimaryKeyCipher as a singleton.
    /// </summary>
    /// <param name="invocation">Production invocation to classify.</param>
    /// <returns><c>true</c> when the invocation has the approved generic singleton registration shape.</returns>
    private static bool isPrimaryKeyCipherSingletonRegistration(InvocationExpressionSyntax invocation)
    {
        #region implementation

        var genericName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax name } => name,
            _ => null
        };

        return genericName?.Identifier.ValueText == "AddSingleton" &&
            genericName.TypeArgumentList.Arguments.Count == 2 &&
            string.Equals(genericName.TypeArgumentList.Arguments[0].ToString(), "IPrimaryKeyCipher", StringComparison.Ordinal) &&
            string.Equals(genericName.TypeArgumentList.Arguments[1].ToString(), "PrimaryKeyCipher", StringComparison.Ordinal);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Removes parenthesized wrappers before expression classification.
    /// </summary>
    /// <param name="expression">Expression that may be parenthesized.</param>
    /// <returns>The underlying unparenthesized expression.</returns>
    private static ExpressionSyntax unwrapParentheses(ExpressionSyntax expression)
    {
        #region implementation

        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Locates the repository directory from build-time assembly metadata.
    /// </summary>
    /// <returns>Absolute repository path containing the MedRecPro project directories.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the repository cannot be located.</exception>
    private static string findRepositoryDirectory()
    {
        #region implementation

        return RepositorySourceRoot.RootPath;

        #endregion
    }

    private sealed record ParseView(string Name, IReadOnlyCollection<string> PreprocessorSymbols);

    private sealed record ConfigurationPathResolution(bool IsConfiguration, string? Path)
    {
        public static ConfigurationPathResolution NotConfiguration { get; } = new(false, null);
    }

    private sealed record SecretBoundarySourceFile(string ProjectName, string RelativePath, SyntaxTree SyntaxTree);

    private sealed record SecretBoundaryScanResult(IReadOnlyList<SecretReadOccurrence> Occurrences, IReadOnlyList<string> Diagnostics);

    private sealed record SecretReadOccurrence(
        string ProjectName,
        string RelativePath,
        string Owner,
        string AccessShape,
        int SpanStart,
        int LineNumber,
        int OccurrenceOrdinal)
    {
        public string Identity => $"{ProjectName}/{RelativePath}|{Owner}|{AccessShape}|{OccurrenceOrdinal}";
    }

    #endregion
}
