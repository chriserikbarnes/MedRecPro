using MedRecProImportClass.Data;
using MedRecProImportClass.DataAccess;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Service;
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
        /// Executes the import via <see cref="OrangeBookProductParsingService"/> with a
        /// Spectre.Console progress bar that parses batch progress messages for real-time
        /// percentage tracking.
        /// </summary>
        /// <param name="serviceProvider">DI service provider for resolving the parsing service.</param>
        /// <param name="fileContent">The full text content of products.txt.</param>
        /// <param name="totalDataRows">Total data rows for progress bar max value.</param>
        /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
        /// <returns>An <see cref="OrangeBookImportResult"/> with import statistics.</returns>
        /// <seealso cref="OrangeBookProductParsingService.ProcessProductsFileAsync"/>
        /// <seealso cref="BatchProgressPattern"/>
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
                    // Create the progress task with row count as max value
                    var importTask = ctx.AddTask(
                        "[orange1]Orange Book Products Import[/]",
                        maxValue: Math.Max(1, totalDataRows));

                    // Progress callback: parse batch messages for row counts,
                    // update description with current step
                    Action<string> progressCallback = (message) =>
                    {
                        // Try to parse batch progress: "(X/Y rows processed)"
                        var match = BatchProgressPattern.Match(message);
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var currentRow))
                        {
                            importTask.Value = currentRow;
                        }

                        // Update the description with the current step message
                        var displayMessage = truncateForDisplay(message);
                        importTask.Description = $"[orange1]{Markup.Escape(displayMessage)}[/]";
                    };

                    // Resolve the parsing service and execute
                    using var scope = serviceProvider.CreateScope();
                    var parsingService = scope.ServiceProvider
                        .GetRequiredService<OrangeBookProductParsingService>();

                    result = await parsingService.ProcessProductsFileAsync(
                        fileContent, cancellationToken, progressCallback);

                    // Ensure progress bar shows 100% at completion
                    importTask.Value = importTask.MaxValue;
                    importTask.Description = result.Success
                        ? "[green]Import complete[/]"
                        : "[red]Import completed with errors[/]";

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
