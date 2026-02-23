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
    /// Service responsible for orchestrating the import of FDA Orange Book products.txt
    /// from a ZIP file. Manages dependency injection setup, ZIP extraction, optional
    /// table truncation, and progress-tracked import via <see cref="OrangeBookProductParsingService"/>.
    /// </summary>
    /// <remarks>
    /// Follows the same orchestrator pattern as <see cref="ImportService"/> but tailored for
    /// the Orange Book single-file import workflow. The import is idempotent (upsert-based),
    /// so crash recovery via <see cref="ImportProgressTracker"/> is not needed.
    ///
    /// Uses Spectre.Console for styled progress display. Console output from EF Core and
    /// logging is suppressed unless verbose mode is enabled.
    /// </remarks>
    /// <seealso cref="OrangeBookProductParsingService"/>
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

                // Phase 4: Extract products.txt from ZIP
                AnsiConsole.MarkupLine("[grey]Extracting products.txt from ZIP...[/]");
                var fileContent = extractProductsFileFromZip(zipFilePath);
                var totalDataRows = countDataRows(fileContent);
                AnsiConsole.MarkupLine($"[grey]Found {totalDataRows:N0} data rows to process.[/]");
                AnsiConsole.WriteLine();

                // Suppress console again during import processing
                suppressConsoleOutput();

                // Phase 5: Import with progress tracking
                var result = await executeImportWithProgressAsync(
                    serviceProvider, fileContent, totalDataRows, cancellationToken);

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
        /// Extracts the contents of products.txt from the Orange Book ZIP file.
        /// </summary>
        /// <param name="zipFilePath">Full path to the Orange Book ZIP archive.</param>
        /// <returns>The full text content of products.txt.</returns>
        /// <exception cref="FileNotFoundException">Thrown when products.txt is not found in the ZIP archive.</exception>
        /// <seealso cref="ZipFile"/>
        /// <seealso cref="ZipArchive"/>
        private string extractProductsFileFromZip(string zipFilePath)
        {
            #region implementation

            using var archive = ZipFile.OpenRead(zipFilePath);
            var entry = archive.Entries.FirstOrDefault(e =>
                e.Name.Equals("products.txt", StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                throw new FileNotFoundException(
                    "products.txt not found in ZIP file. " +
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
        /// Executes the import via <see cref="OrangeBookProductParsingService"/> with
        /// Spectre.Console multi-task progress bars that track each import phase independently:
        /// product upsert, organization matching, ingredient matching, and category matching.
        /// </summary>
        /// <param name="serviceProvider">DI service provider for resolving the parsing service.</param>
        /// <param name="fileContent">The full text content of products.txt.</param>
        /// <param name="totalDataRows">Total data rows for the product import progress bar max value.</param>
        /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
        /// <returns>An <see cref="OrangeBookImportResult"/> with import statistics.</returns>
        /// <remarks>
        /// Progress tasks are created lazily as each phase begins. Matching phases start as
        /// indeterminate spinners and switch to determinate progress bars when the first
        /// queried/total count message arrives.
        /// </remarks>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        /// <seealso cref="BatchProgressPattern"/>
        /// <seealso cref="IngredientSubstringPattern"/>
        /// <seealso cref="CategorySubstringPattern"/>
        private async Task<OrangeBookImportResult> executeImportWithProgressAsync(
            ServiceProvider serviceProvider,
            string fileContent,
            int totalDataRows,
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
                    // Create the primary product import task with row count as max value
                    var productTask = ctx.AddTask(
                        "[orange1]Importing products[/]",
                        maxValue: Math.Max(1, totalDataRows));

                    // Matching phase tasks — created lazily when each phase begins
                    ProgressTask? orgMatchTask = null;
                    ProgressTask? ingredientMatchTask = null;
                    ProgressTask? categoryMatchTask = null;

                    // Progress callback: routes each message to the appropriate progress task
                    // based on message prefix and regex pattern matching
                    Action<string> progressCallback = (message) =>
                    {
                        // Product batch progress: "(X/Y rows processed)"
                        var batchMatch = BatchProgressPattern.Match(message);
                        if (batchMatch.Success && int.TryParse(batchMatch.Groups[1].Value, out var currentRow))
                        {
                            productTask.Value = currentRow;
                            productTask.Description = $"[orange1]{Markup.Escape(truncateForDisplay(message))}[/]";
                            return;
                        }

                        // Organization matching phase start
                        if (message.StartsWith("Matching applicants to organizations"))
                        {
                            // Complete the product task
                            productTask.Value = productTask.MaxValue;
                            productTask.Description = "[green]Products imported[/]";

                            // Create org matching task (indeterminate — no progress count available)
                            orgMatchTask = ctx.AddTask("[orange1]Matching organizations[/]", maxValue: 1);
                            orgMatchTask.IsIndeterminate = true;
                            return;
                        }

                        // Ingredient matching phase start
                        if (message.StartsWith("Matching products to ingredient substances"))
                        {
                            // Complete org matching task
                            if (orgMatchTask != null)
                            {
                                orgMatchTask.IsIndeterminate = false;
                                orgMatchTask.Value = orgMatchTask.MaxValue;
                                orgMatchTask.Description = "[green]Organizations matched[/]";
                            }

                            // Create ingredient matching task (indeterminate until first X/Y message)
                            ingredientMatchTask = ctx.AddTask("[orange1]Matching ingredients[/]", maxValue: 100);
                            ingredientMatchTask.IsIndeterminate = true;
                            return;
                        }

                        // Ingredient exact results — update description only
                        if (message.StartsWith("Ingredients (exact)") && ingredientMatchTask != null)
                        {
                            ingredientMatchTask.Description = $"[orange1]{Markup.Escape(truncateForDisplay(message))}[/]";
                            return;
                        }

                        // Ingredient substring progress: "Ingredients (substring): X/Y names queried"
                        var ingredientMatch = IngredientSubstringPattern.Match(message);
                        if (ingredientMatch.Success && ingredientMatchTask != null)
                        {
                            if (int.TryParse(ingredientMatch.Groups[1].Value, out var queried) &&
                                int.TryParse(ingredientMatch.Groups[2].Value, out var total))
                            {
                                ingredientMatchTask.IsIndeterminate = false;
                                ingredientMatchTask.MaxValue = Math.Max(1, total);
                                ingredientMatchTask.Value = queried;
                            }
                            ingredientMatchTask.Description = $"[orange1]{Markup.Escape(truncateForDisplay(message))}[/]";
                            return;
                        }

                        // Category matching phase start
                        if (message.StartsWith("Matching products to marketing categories"))
                        {
                            // Complete ingredient matching task
                            if (ingredientMatchTask != null)
                            {
                                ingredientMatchTask.IsIndeterminate = false;
                                ingredientMatchTask.Value = ingredientMatchTask.MaxValue;
                                ingredientMatchTask.Description = "[green]Ingredients matched[/]";
                            }

                            // Create category matching task (indeterminate until first X/Y message)
                            categoryMatchTask = ctx.AddTask("[orange1]Matching categories[/]", maxValue: 100);
                            categoryMatchTask.IsIndeterminate = true;
                            return;
                        }

                        // Category exact results — update description only
                        if (message.StartsWith("Categories (exact)") && categoryMatchTask != null)
                        {
                            categoryMatchTask.Description = $"[orange1]{Markup.Escape(truncateForDisplay(message))}[/]";
                            return;
                        }

                        // Category substring progress: "Categories (substring): X/Y numbers queried"
                        var categoryMatch = CategorySubstringPattern.Match(message);
                        if (categoryMatch.Success && categoryMatchTask != null)
                        {
                            if (int.TryParse(categoryMatch.Groups[1].Value, out var queried) &&
                                int.TryParse(categoryMatch.Groups[2].Value, out var total))
                            {
                                categoryMatchTask.IsIndeterminate = false;
                                categoryMatchTask.MaxValue = Math.Max(1, total);
                                categoryMatchTask.Value = queried;
                            }
                            categoryMatchTask.Description = $"[orange1]{Markup.Escape(truncateForDisplay(message))}[/]";
                            return;
                        }

                        // Default: update the product task description for early phase messages
                        // (e.g., "Parsing products.txt lines...", "Upserting applicants...")
                        productTask.Description = $"[orange1]{Markup.Escape(truncateForDisplay(message))}[/]";
                    };

                    // Resolve the parsing service and execute
                    using var scope = serviceProvider.CreateScope();
                    var parsingService = scope.ServiceProvider
                        .GetRequiredService<OrangeBookProductParsingService>();

                    result = await parsingService.ProcessProductsFileAsync(
                        fileContent, cancellationToken, progressCallback);

                    // Ensure all tasks show completion
                    productTask.Value = productTask.MaxValue;
                    productTask.Description = "[green]Products imported[/]";

                    if (orgMatchTask != null)
                    {
                        orgMatchTask.IsIndeterminate = false;
                        orgMatchTask.Value = orgMatchTask.MaxValue;
                        orgMatchTask.Description = "[green]Organizations matched[/]";
                    }
                    if (ingredientMatchTask != null)
                    {
                        ingredientMatchTask.IsIndeterminate = false;
                        ingredientMatchTask.Value = ingredientMatchTask.MaxValue;
                        ingredientMatchTask.Description = "[green]Ingredients matched[/]";
                    }
                    if (categoryMatchTask != null)
                    {
                        categoryMatchTask.IsIndeterminate = false;
                        categoryMatchTask.Value = categoryMatchTask.MaxValue;
                        categoryMatchTask.Description = result!.Success
                            ? "[green]Categories matched[/]"
                            : "[red]Import completed with errors[/]";
                    }

                    // Small delay to ensure the final state is rendered
                    await Task.Delay(100);
                });

            return result!;

            #endregion
        }

        #endregion

        #region private methods - service configuration

        /**************************************************************/
        /// <summary>
        /// Builds the dependency injection service provider with all required services
        /// for <see cref="OrangeBookProductParsingService"/>.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="verboseMode">Whether verbose logging is enabled.</param>
        /// <returns>Configured <see cref="ServiceProvider"/> with all required registrations.</returns>
        /// <remarks>
        /// Registers the same base services as <see cref="ImportService"/> (ApplicationDbContext,
        /// Repository, StringCipher) but replaces SPL-specific services with
        /// <see cref="OrangeBookProductParsingService"/>.
        /// </remarks>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="OrangeBookProductParsingService"/>
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

            // Add MedRecPro services required by OrangeBookProductParsingService
            services.AddScoped(typeof(Repository<>), typeof(Repository<>));
            services.AddTransient<StringCipher>();
            services.AddScoped<OrangeBookProductParsingService>();

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

        #endregion
    }
}
