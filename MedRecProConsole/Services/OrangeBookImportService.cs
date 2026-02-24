using MedRecProImportClass.Data;
using MedRecProImportClass.DataAccess;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Service;
using MedRecProImportClass.Service.ParsingServices;
using MedRecProConsole.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace MedRecProConsole.Services
{
    /**************************************************************/
    /// <summary>
    /// Service responsible for orchestrating the import of FDA Orange Book products.txt,
    /// patent.txt, and exclusivity.txt from a ZIP file. Manages dependency injection setup,
    /// ZIP extraction, optional table truncation, and progress-tracked import via
    /// <see cref="OrangeBookProductParsingService"/>, <see cref="OrangeBookPatentParsingService"/>,
    /// and <see cref="OrangeBookExclusivityParsingService"/>.
    /// </summary>
    /// <remarks>
    /// Follows the same orchestrator pattern as <see cref="ImportService"/> but tailored for
    /// the Orange Book multi-file import workflow. Products are imported first, then patents
    /// and exclusivity records are imported and linked to products via natural key
    /// (ApplType, ApplNo, ProductNo). The import is idempotent (upsert-based), so crash
    /// recovery via <see cref="ImportProgressTracker"/> is not needed.
    ///
    /// Uses Spectre.Console for styled progress display. Console output from EF Core and
    /// logging is suppressed unless verbose mode is enabled.
    /// </remarks>
    /// <seealso cref="OrangeBookProductParsingService"/>
    /// <seealso cref="OrangeBookPatentParsingService"/>
    /// <seealso cref="OrangeBookExclusivityParsingService"/>
    /// <seealso cref="OrangeBookImportResult"/>
    /// <seealso cref="ImportService"/>
    public class OrangeBookImportService
    {
        #region private fields

        /**************************************************************/
        /// <summary>
        /// Stores the original console output writer during suppression.
        /// Used to restore console output after EF Core/logging noise is suppressed.
        /// </summary>
        /// <seealso cref="suppressConsoleOutput"/>
        /// <seealso cref="restoreConsoleOutput"/>
        private TextWriter? _originalConsoleOut;

        /**************************************************************/
        /// <summary>
        /// Whether verbose logging is enabled for this import operation.
        /// </summary>
        private bool _verboseMode;

        #endregion

        #region constants

        /// <summary>
        /// Orange Book tables to truncate, ordered junctions first then fact tables.
        /// </summary>
        private static readonly string[] OrangeBookTableNames =
        {
            "OrangeBookApplicantOrganization",
            "OrangeBookProductIngredientSubstance",
            "OrangeBookProductMarketingCategory",
            "OrangeBookExclusivity",
            "OrangeBookPatent",
            "OrangeBookProduct",
            "OrangeBookApplicant"
        };

        /// <summary>
        /// Regex pattern to extract progress row counts from batch messages.
        /// Matches strings like "(10000/15000 rows processed)".
        /// </summary>
        private static readonly Regex BatchProgressPattern = new(
            @"\((\d+)/(\d+) rows processed\)",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Regex pattern to extract ingredient substring query progress.
        /// Matches strings like "Ingredients (substring): 25/100 names queried (5 matches)".
        /// </summary>
        /// <seealso cref="BatchProgressPattern"/>
        /// <seealso cref="CategorySubstringPattern"/>
        private static readonly Regex IngredientSubstringPattern = new(
            @"Ingredients \(substring\): (\d+)/(\d+) names queried",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Regex pattern to extract category substring query progress.
        /// Matches strings like "Categories (substring): 50/200 numbers queried (10 matches)".
        /// </summary>
        /// <seealso cref="BatchProgressPattern"/>
        /// <seealso cref="IngredientSubstringPattern"/>
        private static readonly Regex CategorySubstringPattern = new(
            @"Categories \(substring\): (\d+)/(\d+) numbers queried",
            RegexOptions.Compiled);

        /// <summary>
        /// Maximum characters for the progress task description to avoid overflow.
        /// </summary>
        private const int MaxDescriptionLength = 60;

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Executes the full Orange Book products.txt import pipeline: optional truncation,
        /// ZIP extraction, parsing, and database upsert with live progress display.
        /// </summary>
        /// <param name="connectionString">Database connection string for the target MedRecPro database.</param>
        /// <param name="zipFilePath">Full path to the Orange Book ZIP file containing products.txt.</param>
        /// <param name="truncateBeforeImport">When true, truncates all Orange Book tables before importing.</param>
        /// <param name="verboseMode">When true, enables verbose console and EF Core logging.</param>
        /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
        /// <returns>An <see cref="OrangeBookImportResult"/> with counts, match stats, and error details.</returns>
        /// <example>
        /// <code>
        /// var service = new OrangeBookImportService();
        /// var result = await service.ExecuteImportAsync(
        ///     connectionString, @"C:\OrangeBook\EOBZIP_2026_01.zip",
        ///     truncateBeforeImport: true, verboseMode: false);
        /// </code>
        /// </example>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        /// <seealso cref="OrangeBookPatentParsingService.ProcessPatentsFileAsync"/>
        /// <seealso cref="OrangeBookExclusivityParsingService.ProcessExclusivityFileAsync"/>
        /// <seealso cref="OrangeBookImportResult"/>
        public async Task<OrangeBookImportResult> ExecuteImportAsync(
            string connectionString,
            string zipFilePath,
            bool truncateBeforeImport,
            bool verboseMode,
            CancellationToken cancellationToken = default)
        {
            #region implementation

            var stopwatch = Stopwatch.StartNew();
            _verboseMode = verboseMode;
            _originalConsoleOut = null;
            ServiceProvider? serviceProvider = null;

            try
            {
                // Phase 1: Build DI container
                var configuration = ConfigurationHelper.BuildMedRecProConfiguration();

                // Suppress console output if not verbose (prevents EF Core noise)
                suppressConsoleOutput();

                serviceProvider = buildServiceProvider(connectionString, configuration, verboseMode);

                // Phase 2: Display header
                restoreConsoleOutput();

                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("[bold orange1]Orange Book Import[/]").RuleStyle("grey"));
                AnsiConsole.MarkupLine($"[grey]Source: {Markup.Escape(Path.GetFileName(zipFilePath))}[/]");
                AnsiConsole.WriteLine();

                // Phase 3: Truncate if requested
                if (truncateBeforeImport)
                {
                    await executeTruncationAsync(serviceProvider);
                }

                // Phase 4: Extract products.txt and patent.txt from ZIP
                AnsiConsole.MarkupLine("[grey]Extracting products.txt from ZIP...[/]");
                var productsContent = extractFileFromZip(zipFilePath, "products.txt");
                var totalProductRows = countDataRows(productsContent);
                AnsiConsole.MarkupLine($"[grey]Found {totalProductRows:N0} product data rows to process.[/]");

                AnsiConsole.MarkupLine("[grey]Extracting patent.txt from ZIP...[/]");
                var patentContent = extractFileFromZip(zipFilePath, "patent.txt");
                var totalPatentRows = countDataRows(patentContent);
                AnsiConsole.MarkupLine($"[grey]Found {totalPatentRows:N0} patent data rows to process.[/]");

                AnsiConsole.MarkupLine("[grey]Extracting exclusivity.txt from ZIP...[/]");
                var exclusivityContent = extractFileFromZip(zipFilePath, "exclusivity.txt");
                var totalExclusivityRows = countDataRows(exclusivityContent);
                AnsiConsole.MarkupLine($"[grey]Found {totalExclusivityRows:N0} exclusivity data rows to process.[/]");
                AnsiConsole.WriteLine();

                // Suppress console again during import processing
                suppressConsoleOutput();

                // Phase 5: Import with progress tracking (products, matching, patents, exclusivity)
                var result = await executeImportWithProgressAsync(
                    serviceProvider, productsContent, totalProductRows,
                    patentContent, totalPatentRows,
                    exclusivityContent, totalExclusivityRows, cancellationToken);

                // Phase 6: Finalize
                restoreConsoleOutput();
                stopwatch.Stop();

                return result;
            }
            catch (OperationCanceledException)
            {
                restoreConsoleOutput();
                stopwatch.Stop();

                AnsiConsole.MarkupLine("[yellow]Orange Book import was cancelled.[/]");

                return new OrangeBookImportResult
                {
                    Success = false,
                    Message = $"Import cancelled after {stopwatch.Elapsed.TotalSeconds:F1} seconds."
                };
            }
            catch (Exception ex)
            {
                restoreConsoleOutput();
                stopwatch.Stop();

                AnsiConsole.WriteException(ex);

                return new OrangeBookImportResult
                {
                    Success = false,
                    Message = $"Import failed: {ex.Message}",
                    Errors = { ex.Message }
                };
            }
            finally
            {
                // Ensure console output is always restored
                restoreConsoleOutput();

                // Dispose the service provider
                if (serviceProvider != null)
                {
                    await serviceProvider.DisposeAsync();
                }
            }

            #endregion
        }

        #endregion

        #region private methods - orchestration

        /**************************************************************/
        /// <summary>
        /// Executes truncation of all Orange Book tables with console feedback.
        /// Attempts TRUNCATE TABLE first, falling back to DELETE FROM if constraints prevent truncation.
        /// </summary>
        /// <param name="serviceProvider">DI service provider for resolving <see cref="ApplicationDbContext"/>.</param>
        /// <returns>Task representing the asynchronous truncation operation.</returns>
        /// <seealso cref="OrangeBookTableNames"/>
        private async Task executeTruncationAsync(ServiceProvider serviceProvider)
        {
            #region implementation

            restoreConsoleOutput();

            AnsiConsole.Write(new Rule("[bold red]Truncating Orange Book Tables[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();

            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if(context == null)
            {
                AnsiConsole.MarkupLine("[red]Error: Unable to resolve ApplicationDbContext for truncation.[/]");
                return;
            }
            if(context.Database == null)
            {
                AnsiConsole.MarkupLine("[red]Error: Database property is not available in ApplicationDbContext.[/]");
                return;
            }
            if(context.Database.GetDbConnection() == null)
            {
                AnsiConsole.MarkupLine("[red]Error: Database connection is not available in ApplicationDbContext.[/]");
                return;
            }
         
            foreach (var tableName in OrangeBookTableNames)
            {
                try
                {
                    // Try TRUNCATE first (faster, resets identity columns)
                    await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE [dbo].[{tableName}]");
                    AnsiConsole.MarkupLine($"  [green][[T]][/] {Markup.Escape(tableName)}");
                }
                catch
                {
                    try
                    {
                        // Fall back to DELETE if TRUNCATE fails (indexed views, etc.)
                        await context.Database.ExecuteSqlRawAsync($"DELETE FROM [dbo].[{tableName}]");
                        AnsiConsole.MarkupLine($"  [yellow][[D]][/] {Markup.Escape(tableName)}");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"  [red][[E]][/] {Markup.Escape(tableName)} - {Markup.Escape(ex.Message)}");
                    }
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Truncation complete.[/]");
            AnsiConsole.WriteLine();

            suppressConsoleOutput();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the contents of a named file from the Orange Book ZIP archive.
        /// </summary>
        /// <param name="zipFilePath">Full path to the Orange Book ZIP archive.</param>
        /// <param name="fileName">Name of the file to extract (e.g., "products.txt", "patent.txt").</param>
        /// <returns>The full text content of the requested file.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the named file is not found in the ZIP archive.</exception>
        /// <seealso cref="ZipFile"/>
        /// <seealso cref="ZipArchive"/>
        private string extractFileFromZip(string zipFilePath, string fileName)
        {
            #region implementation

            using var archive = ZipFile.OpenRead(zipFilePath);
            var entry = archive.Entries.FirstOrDefault(e =>
                e.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                throw new FileNotFoundException(
                    $"{fileName} not found in ZIP file. " +
                    $"Archive contains: {string.Join(", ", archive.Entries.Select(e => e.Name))}");
            }

            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Counts the number of data rows in the products.txt content (excluding header).
        /// Used to set the progress bar maximum value for accurate percentage display.
        /// </summary>
        /// <param name="fileContent">The full text content of products.txt.</param>
        /// <returns>The number of data rows (total lines minus header, excluding trailing empty lines).</returns>
        private int countDataRows(string fileContent)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(fileContent))
                return 0;

            // Count non-empty lines, subtract 1 for the header row
            var lines = fileContent.Split('\n');
            var nonEmptyLines = lines.Count(line => !string.IsNullOrWhiteSpace(line));

            // Subtract header row
            return Math.Max(0, nonEmptyLines - 1);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Executes the import via <see cref="OrangeBookProductParsingService"/>,
        /// <see cref="OrangeBookPatentParsingService"/>, and
        /// <see cref="OrangeBookExclusivityParsingService"/> with Spectre.Console multi-task
        /// progress bars that track each import phase independently: product upsert,
        /// organization matching, ingredient matching, category matching, patent upsert,
        /// and exclusivity upsert.
        /// </summary>
        /// <param name="serviceProvider">DI service provider for resolving the parsing services.</param>
        /// <param name="productsContent">The full text content of products.txt.</param>
        /// <param name="totalProductRows">Total data rows for the product import progress bar max value.</param>
        /// <param name="patentContent">The full text content of patent.txt.</param>
        /// <param name="totalPatentRows">Total data rows for the patent import progress bar max value.</param>
        /// <param name="exclusivityContent">The full text content of exclusivity.txt.</param>
        /// <param name="totalExclusivityRows">Total data rows for the exclusivity import progress bar max value.</param>
        /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
        /// <returns>An <see cref="OrangeBookImportResult"/> with import statistics.</returns>
        /// <remarks>
        /// Progress tasks are created lazily as each phase begins. Matching phases start as
        /// indeterminate spinners and switch to determinate progress bars when the first
        /// queried/total count message arrives. Patents are imported after all product phases
        /// complete, using a separate progress task.
        /// </remarks>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        /// <seealso cref="OrangeBookPatentParsingService.ProcessPatentsFileAsync"/>
        /// <seealso cref="OrangeBookExclusivityParsingService.ProcessExclusivityFileAsync"/>
        /// <seealso cref="BatchProgressPattern"/>
        /// <seealso cref="IngredientSubstringPattern"/>
        /// <seealso cref="CategorySubstringPattern"/>
        private async Task<OrangeBookImportResult> executeImportWithProgressAsync(
            ServiceProvider serviceProvider,
            string productsContent,
            int totalProductRows,
            string patentContent,
            int totalPatentRows,
            string exclusivityContent,
            int totalExclusivityRows,
            CancellationToken cancellationToken)
        {
            #region implementation

            // Restore console for Spectre.Console progress display
            restoreConsoleOutput();

            OrangeBookImportResult? result = null;

            await AnsiConsole.Progress()
                .AutoRefresh(true)
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new ElapsedTimeColumn(),
                    new SpinnerColumn()
                })
                .StartAsync(async ctx =>
                {
                    // ── Phase A: Products + Matching ──
                    var productTask = ctx.AddTask(
                        "[orange1]Importing products[/]",
                        maxValue: Math.Max(1, totalProductRows));

                    var matchingTasks = new ProductMatchingTasks();
                    var productProgressCallback = buildProductProgressCallback(ctx, productTask, matchingTasks);

                    using var scope = serviceProvider.CreateScope();
                    var productParsingService = scope.ServiceProvider
                        .GetRequiredService<OrangeBookProductParsingService>();

                    result = await productParsingService.ProcessProductsFileAsync(
                        productsContent, cancellationToken, productProgressCallback);

                    // Ensure all product-phase tasks show completion
                    completeProgressTask(productTask, "Products imported");
                    completeProgressTask(matchingTasks.OrgMatchTask, "Organizations matched");
                    completeProgressTask(matchingTasks.IngredientMatchTask, "Ingredients matched");
                    completeProgressTask(matchingTasks.CategoryMatchTask, "Categories matched");

                    // ── Phase B: Patents ──
                    var patentTask = ctx.AddTask(
                        "[orange1]Importing patents[/]",
                        maxValue: Math.Max(1, totalPatentRows));

                    var patentProgressCallback = buildPatentProgressCallback(patentTask);

                    var patentParsingService = scope.ServiceProvider
                        .GetRequiredService<OrangeBookPatentParsingService>();

                    result = await patentParsingService.ProcessPatentsFileAsync(
                        patentContent, result!, cancellationToken, patentProgressCallback);

                    // Finalize patent task
                    completeProgressTask(patentTask, "Patents imported");

                    // ── Phase C: Exclusivity ──
                    var exclusivityTask = ctx.AddTask(
                        "[orange1]Importing exclusivity[/]",
                        maxValue: Math.Max(1, totalExclusivityRows));

                    var exclusivityProgressCallback = buildExclusivityProgressCallback(exclusivityTask);

                    var exclusivityParsingService = scope.ServiceProvider
                        .GetRequiredService<OrangeBookExclusivityParsingService>();

                    result = await exclusivityParsingService.ProcessExclusivityFileAsync(
                        exclusivityContent, result!, cancellationToken, exclusivityProgressCallback);

                    // Finalize exclusivity task
                    completeProgressTask(exclusivityTask,
                        result!.Success ? "Exclusivity imported" : "Import completed with errors",
                        result!.Success ? "green" : "red");

                    // Small delay to ensure the final state is rendered
                    await Task.Delay(100);
                });

            return result!;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the progress callback for the product import phase. Routes each progress
        /// message from <see cref="OrangeBookProductParsingService"/> to the appropriate
        /// Spectre.Console progress task based on message prefix and regex pattern matching.
        /// </summary>
        /// <param name="ctx">The Spectre.Console progress context for creating new tasks.</param>
        /// <param name="productTask">The primary product import progress task.</param>
        /// <param name="matchingTasks">Mutable holder for lazily-created matching-phase tasks.</param>
        /// <returns>An <see cref="Action{String}"/> callback to pass to the product parsing service.</returns>
        /// <remarks>
        /// The callback handles five distinct message types:
        /// <list type="bullet">
        /// <item><description>Batch progress ("X/Y rows processed") — updates product task value</description></item>
        /// <item><description>Phase transitions ("Matching applicants...") — completes previous task, creates next</description></item>
        /// <item><description>Exact match results ("Ingredients (exact)") — updates description only</description></item>
        /// <item><description>Substring progress ("Ingredients (substring): X/Y") — updates progress bar</description></item>
        /// <item><description>Default messages — updates product task description</description></item>
        /// </list>
        /// </remarks>
        /// <seealso cref="tryUpdateBatchProgress"/>
        /// <seealso cref="tryUpdateSubstringProgress"/>
        /// <seealso cref="completeProgressTask"/>
        /// <seealso cref="ProductMatchingTasks"/>
        private Action<string> buildProductProgressCallback(
            ProgressContext ctx,
            ProgressTask productTask,
            ProductMatchingTasks matchingTasks)
        {
            #region implementation

            return (message) =>
            {
                // Product batch progress: "(X/Y rows processed)"
                if (tryUpdateBatchProgress(productTask, message))
                    return;

                // Organization matching phase start
                if (message.StartsWith("Matching applicants to organizations"))
                {
                    completeProgressTask(productTask, "Products imported");
                    matchingTasks.OrgMatchTask = ctx.AddTask("[orange1]Matching organizations[/]", maxValue: 1);
                    matchingTasks.OrgMatchTask.IsIndeterminate = true;
                    return;
                }

                // Ingredient matching phase start
                if (message.StartsWith("Matching products to ingredient substances"))
                {
                    completeProgressTask(matchingTasks.OrgMatchTask, "Organizations matched");
                    matchingTasks.IngredientMatchTask = ctx.AddTask("[orange1]Matching ingredients[/]", maxValue: 100);
                    matchingTasks.IngredientMatchTask.IsIndeterminate = true;
                    return;
                }

                // Ingredient exact results — update description only
                if (message.StartsWith("Ingredients (exact)") && matchingTasks.IngredientMatchTask != null)
                {
                    matchingTasks.IngredientMatchTask.Description = formatActiveDescription(message);
                    return;
                }

                // Ingredient substring progress
                if (tryUpdateSubstringProgress(matchingTasks.IngredientMatchTask, IngredientSubstringPattern, message))
                    return;

                // Category matching phase start
                if (message.StartsWith("Matching products to marketing categories"))
                {
                    completeProgressTask(matchingTasks.IngredientMatchTask, "Ingredients matched");
                    matchingTasks.CategoryMatchTask = ctx.AddTask("[orange1]Matching categories[/]", maxValue: 100);
                    matchingTasks.CategoryMatchTask.IsIndeterminate = true;
                    return;
                }

                // Category exact results — update description only
                if (message.StartsWith("Categories (exact)") && matchingTasks.CategoryMatchTask != null)
                {
                    matchingTasks.CategoryMatchTask.Description = formatActiveDescription(message);
                    return;
                }

                // Category substring progress
                if (tryUpdateSubstringProgress(matchingTasks.CategoryMatchTask, CategorySubstringPattern, message))
                    return;

                // Default: update product task for early-phase messages
                // (e.g., "Parsing products.txt lines...", "Upserting applicants...")
                productTask.Description = formatActiveDescription(message);
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the progress callback for the patent import phase. Routes batch progress
        /// messages to the patent progress task.
        /// </summary>
        /// <param name="patentTask">The patent import progress task.</param>
        /// <returns>An <see cref="Action{String}"/> callback to pass to the patent parsing service.</returns>
        /// <seealso cref="tryUpdateBatchProgress"/>
        /// <seealso cref="buildProductProgressCallback"/>
        private Action<string> buildPatentProgressCallback(ProgressTask patentTask)
        {
            #region implementation

            return (message) =>
            {
                if (tryUpdateBatchProgress(patentTask, message))
                    return;

                // Default: update patent task description for non-batch messages
                patentTask.Description = formatActiveDescription(message);
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the progress callback for the exclusivity import phase. Routes batch progress
        /// messages to the exclusivity progress task.
        /// </summary>
        /// <param name="exclusivityTask">The exclusivity import progress task.</param>
        /// <returns>An <see cref="Action{String}"/> callback to pass to the exclusivity parsing service.</returns>
        /// <seealso cref="tryUpdateBatchProgress"/>
        /// <seealso cref="buildPatentProgressCallback"/>
        /// <seealso cref="buildProductProgressCallback"/>
        private Action<string> buildExclusivityProgressCallback(ProgressTask exclusivityTask)
        {
            #region implementation

            return (message) =>
            {
                if (tryUpdateBatchProgress(exclusivityTask, message))
                    return;

                // Default: update exclusivity task description for non-batch messages
                exclusivityTask.Description = formatActiveDescription(message);
            };

            #endregion
        }

        #endregion

        #region private methods - service configuration

        /**************************************************************/
        /// <summary>
        /// Builds the dependency injection service provider with all required services
        /// for <see cref="OrangeBookProductParsingService"/>, <see cref="OrangeBookPatentParsingService"/>,
        /// and <see cref="OrangeBookExclusivityParsingService"/>.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="verboseMode">Whether verbose logging is enabled.</param>
        /// <returns>Configured <see cref="ServiceProvider"/> with all required registrations.</returns>
        /// <remarks>
        /// Registers the same base services as <see cref="ImportService"/> (ApplicationDbContext,
        /// Repository, StringCipher) but replaces SPL-specific services with the Orange Book
        /// parsing services.
        /// </remarks>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="OrangeBookProductParsingService"/>
        /// <seealso cref="OrangeBookPatentParsingService"/>
        /// <seealso cref="OrangeBookExclusivityParsingService"/>
        private ServiceProvider buildServiceProvider(string connectionString, IConfiguration configuration, bool verboseMode)
        {
            #region implementation

            var services = new ServiceCollection();

            // Add configuration
            services.AddSingleton<IConfiguration>(configuration);

            // Add logging - suppress most output unless verbose mode
            services.AddLogging(builder =>
            {
                if (verboseMode)
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                }
                else
                {
                    // Suppress all logging except Critical errors
                    builder.SetMinimumLevel(LogLevel.None);
                }
            });

            // Add DbContext
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
                options.EnableSensitiveDataLogging(false);

                // Suppress EF Core warnings unless verbose mode
                if (!verboseMode)
                {
                    options.ConfigureWarnings(warnings =>
                        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.AmbientTransactionWarning)
                    );
                    options.LogTo(_ => { }, LogLevel.None);
                }
            });

            // Add generic logger for Repository
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            // Add MedRecPro services required by Orange Book parsing services
            services.AddScoped(typeof(Repository<>), typeof(Repository<>));
            services.AddTransient<StringCipher>();
            services.AddScoped<OrangeBookProductParsingService>();
            services.AddScoped<OrangeBookPatentParsingService>();
            services.AddScoped<OrangeBookExclusivityParsingService>();

            return services.BuildServiceProvider();

            #endregion
        }

        #endregion

        #region private methods - utility

        /**************************************************************/
        /// <summary>
        /// Suppresses console output if not in verbose mode by redirecting to <see cref="TextWriter.Null"/>.
        /// Stores the original writer in <see cref="_originalConsoleOut"/> for later restoration.
        /// </summary>
        /// <seealso cref="restoreConsoleOutput"/>
        /// <seealso cref="_originalConsoleOut"/>
        private void suppressConsoleOutput()
        {
            #region implementation

            if (!_verboseMode && _originalConsoleOut == null)
            {
                _originalConsoleOut = Console.Out;
                Console.SetOut(TextWriter.Null);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Restores console output to the original writer stored in <see cref="_originalConsoleOut"/>.
        /// </summary>
        /// <seealso cref="suppressConsoleOutput"/>
        /// <seealso cref="_originalConsoleOut"/>
        private void restoreConsoleOutput()
        {
            #region implementation

            if (_originalConsoleOut != null)
            {
                Console.SetOut(_originalConsoleOut);
                _originalConsoleOut = null;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Truncates a progress message for display in the Spectre.Console progress task description.
        /// Prevents long messages from overflowing the console width.
        /// </summary>
        /// <param name="message">The full progress message from the parsing service.</param>
        /// <returns>The message truncated to <see cref="MaxDescriptionLength"/> characters with ellipsis if needed.</returns>
        private string truncateForDisplay(string message)
        {
            #region implementation

            if (string.IsNullOrEmpty(message))
                return string.Empty;

            if (message.Length <= MaxDescriptionLength)
                return message;

            return message[..(MaxDescriptionLength - 3)] + "...";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats a progress message as an active (in-progress) Spectre.Console markup description.
        /// Truncates the message for display and wraps it in orange1 markup tags.
        /// </summary>
        /// <param name="message">The raw progress message from the parsing service.</param>
        /// <returns>A Spectre.Console markup string with orange1 styling.</returns>
        /// <seealso cref="truncateForDisplay"/>
        /// <seealso cref="completeProgressTask"/>
        private string formatActiveDescription(string message)
        {
            #region implementation

            return $"[orange1]{Markup.Escape(truncateForDisplay(message))}[/]";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Marks a Spectre.Console progress task as completed by setting it to determinate mode,
        /// advancing its value to the maximum, and applying a completed-state description.
        /// Safely handles null tasks (no-op when task is null).
        /// </summary>
        /// <param name="task">The progress task to complete, or null if the phase was never started.</param>
        /// <param name="completedLabel">The label to display (e.g., "Products imported").</param>
        /// <param name="color">The markup color for the completed label. Defaults to "green".</param>
        /// <remarks>
        /// Used to finalize progress tasks for both normal completion (callback-driven phase transitions)
        /// and forced completion (ensuring all tasks show completed after the parsing service returns).
        /// </remarks>
        /// <seealso cref="formatActiveDescription"/>
        /// <seealso cref="executeImportWithProgressAsync"/>
        private void completeProgressTask(ProgressTask? task, string completedLabel, string color = "green")
        {
            #region implementation

            if (task == null)
                return;

            task.IsIndeterminate = false;
            task.Value = task.MaxValue;
            task.Description = $"[{color}]{completedLabel}[/]";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to parse a batch progress message (e.g., "(10000/15000 rows processed)")
        /// and update the given progress task's value and description accordingly.
        /// </summary>
        /// <param name="task">The progress task to update.</param>
        /// <param name="message">The progress message to parse.</param>
        /// <returns>True if the message was a batch progress message and the task was updated; false otherwise.</returns>
        /// <seealso cref="BatchProgressPattern"/>
        /// <seealso cref="formatActiveDescription"/>
        private bool tryUpdateBatchProgress(ProgressTask task, string message)
        {
            #region implementation

            var batchMatch = BatchProgressPattern.Match(message);
            if (batchMatch.Success && int.TryParse(batchMatch.Groups[1].Value, out var currentRow))
            {
                task.Value = currentRow;
                task.Description = formatActiveDescription(message);
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to parse a substring-matching progress message (e.g., "Ingredients (substring): 25/100 names queried")
        /// and update the given progress task's value, max value, and description.
        /// Transitions the task from indeterminate to determinate mode on the first successful parse.
        /// </summary>
        /// <param name="task">The progress task to update, or null if the matching phase has not started.</param>
        /// <param name="pattern">The compiled regex pattern to match against the message (must have two capture groups for queried/total).</param>
        /// <param name="message">The progress message to parse.</param>
        /// <returns>True if the message matched the pattern and the task was updated; false otherwise.</returns>
        /// <seealso cref="IngredientSubstringPattern"/>
        /// <seealso cref="CategorySubstringPattern"/>
        /// <seealso cref="formatActiveDescription"/>
        private bool tryUpdateSubstringProgress(ProgressTask? task, Regex pattern, string message)
        {
            #region implementation

            if (task == null)
                return false;

            var match = pattern.Match(message);
            if (!match.Success)
                return false;

            if (int.TryParse(match.Groups[1].Value, out var queried) &&
                int.TryParse(match.Groups[2].Value, out var total))
            {
                task.IsIndeterminate = false;
                task.MaxValue = Math.Max(1, total);
                task.Value = queried;
            }

            task.Description = formatActiveDescription(message);
            return true;

            #endregion
        }

        #endregion

        #region private types

        /**************************************************************/
        /// <summary>
        /// Holds references to the lazily-created matching-phase progress tasks
        /// so they can be accessed by both the progress callback and the post-import
        /// completion logic. Tasks are null until their respective phase begins.
        /// </summary>
        /// <seealso cref="buildProductProgressCallback"/>
        /// <seealso cref="completeProgressTask"/>
        private class ProductMatchingTasks
        {
            /**************************************************************/
            /// <summary>
            /// Progress task for the organization matching phase.
            /// </summary>
            public ProgressTask? OrgMatchTask { get; set; }

            /**************************************************************/
            /// <summary>
            /// Progress task for the ingredient matching phase.
            /// </summary>
            public ProgressTask? IngredientMatchTask { get; set; }

            /**************************************************************/
            /// <summary>
            /// Progress task for the category matching phase.
            /// </summary>
            public ProgressTask? CategoryMatchTask { get; set; }
        }

        #endregion
    }
}
