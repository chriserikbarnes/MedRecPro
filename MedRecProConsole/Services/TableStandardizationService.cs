using MedRecProImportClass.Data;
using MedRecProImportClass.DataAccess;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using MedRecProConsole.Helpers;
using MedRecProConsole.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using System.Diagnostics;

namespace MedRecProConsole.Services
{
    /**************************************************************/
    /// <summary>
    /// Service for executing SPL table standardization operations from the CLI.
    /// Bridges command-line arguments to <see cref="ITableParsingOrchestrator"/> with
    /// Spectre.Console progress bars, resumption tracking, and validation report display.
    /// </summary>
    /// <remarks>
    /// Supports four operations:
    /// - parse: Stage 3 only — parse all tables in batches, write to tmp_FlattenedStandardizedTable
    /// - validate: Stage 3+4 — parse + validate with coverage report
    /// - truncate: Wipe the output table for a clean rerun
    /// - parse-single: Debug a single table without DB write
    ///
    /// Ctrl+C handling saves progress for resumption via <see cref="StandardizationProgressTracker"/>.
    /// </remarks>
    /// <seealso cref="ITableParsingOrchestrator"/>
    /// <seealso cref="StandardizationProgressTracker"/>
    /// <seealso cref="BatchValidationReport"/>
    public class TableStandardizationService
    {
        #region public methods

        /**************************************************************/
        /// <summary>
        /// Executes Stage 3 parsing: truncate → batch loop → write observations.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="batchSize">Tables per batch (default 1000).</param>
        /// <param name="verbose">Enable verbose logging output.</param>
        /// <param name="quiet">Suppress non-essential output.</param>
        /// <returns>Exit code: 0 for success, 1 for failure.</returns>
        /// <seealso cref="ITableParsingOrchestrator.ProcessAllAsync"/>
        public async Task<int> ExecuteParseAsync(string connectionString, int batchSize, bool verbose, bool quiet)
        {
            #region implementation

            var configuration = ConfigurationHelper.BuildMedRecProConfiguration(connectionString);
            using var serviceProvider = buildServiceProvider(connectionString, configuration, verbose, includeValidation: false);
            using var scope = serviceProvider.CreateScope();

            var orchestrator = scope.ServiceProvider.GetRequiredService<ITableParsingOrchestrator>();

            // Set up cancellation
            using var cts = new CancellationTokenSource();
            var progressTracker = new StandardizationProgressTracker();
            var stopwatch = Stopwatch.StartNew();

            setupCancellationHandler(cts, quiet);

            try
            {
                // Check for existing progress file (resume scenario)
                int? resumeFromId = null;
                if (progressTracker.ProgressFileExists())
                {
                    var existingProgress = await progressTracker.LoadOrCreateAsync(connectionString, "parse", batchSize);
                    resumeFromId = progressTracker.GetResumeStartId();

                    if (!quiet && resumeFromId.HasValue)
                    {
                        AnsiConsole.MarkupLine(
                            $"[yellow]Resuming from TextTableID {resumeFromId.Value} " +
                            $"(session {existingProgress.ResumeCount}, " +
                            $"{existingProgress.TotalObservations:N0} observations so far)[/]");
                        AnsiConsole.WriteLine();
                    }
                }
                else
                {
                    await progressTracker.LoadOrCreateAsync(connectionString, "parse", batchSize);
                }

                int totalObs = 0;

                await AnsiConsole.Progress()
                    .AutoRefresh(true)
                    .AutoClear(false)
                    .HideCompleted(false)
                    .Columns(new ProgressColumn[]
                    {
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new RemainingTimeColumn(),
                        new SpinnerColumn()
                    })
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("Stage 3: Parsing tables", maxValue: 100);

                        var progress = new Progress<TransformBatchProgress>(p =>
                        {
                            task.Value = p.TotalBatches > 0
                                ? (double)p.BatchNumber / p.TotalBatches * 100
                                : 0;
                            task.Description =
                                $"Batch {p.BatchNumber}/{p.TotalBatches} " +
                                $"[[{p.RangeStart}-{p.RangeEnd}]] " +
                                $"{p.CumulativeObservationCount:N0} obs";

                            progressTracker.UpdateProgressAsync(p).GetAwaiter().GetResult();
                        });

                        totalObs = await orchestrator.ProcessAllAsync(
                            batchSize, progress, resumeFromId, ct: cts.Token);

                        task.Value = 100;
                        task.Description = $"Complete: {totalObs:N0} observations";
                    });

                stopwatch.Stop();

                // Success — clean up progress file
                await progressTracker.DeleteProgressFileAsync();

                if (!quiet)
                {
                    displayParseResults(totalObs, stopwatch.Elapsed);
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                await progressTracker.RecordInterruptionAsync("User cancellation", stopwatch.Elapsed);
                displayCancellationMessage(quiet);
                return 1;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await progressTracker.RecordInterruptionAsync($"Error: {ex.Message}", stopwatch.Elapsed);

                if (!quiet)
                {
                    AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
                    if (verbose)
                    {
                        AnsiConsole.WriteException(ex);
                    }
                }
                return 1;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Executes Stage 3+4: parse all tables with validation and coverage reporting.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="batchSize">Tables per batch (default 1000).</param>
        /// <param name="verbose">Enable verbose logging output.</param>
        /// <param name="quiet">Suppress non-essential output.</param>
        /// <param name="maxBatches">Optional maximum number of batches to process. Null = all.</param>
        /// <returns>Exit code: 0 for success, 1 for failure.</returns>
        /// <seealso cref="ITableParsingOrchestrator.ProcessAllWithValidationAsync"/>
        /// <seealso cref="BatchValidationReport"/>
        public async Task<int> ExecuteValidateAsync(string connectionString, int batchSize, bool verbose, bool quiet, int? maxBatches = null)
        {
            #region implementation

            var configuration = ConfigurationHelper.BuildMedRecProConfiguration(connectionString);
            using var serviceProvider = buildServiceProvider(connectionString, configuration, verbose, includeValidation: true);
            using var scope = serviceProvider.CreateScope();

            var orchestrator = scope.ServiceProvider.GetRequiredService<ITableParsingOrchestrator>();

            // Set up cancellation
            using var cts = new CancellationTokenSource();
            var progressTracker = new StandardizationProgressTracker();
            var stopwatch = Stopwatch.StartNew();

            setupCancellationHandler(cts, quiet);

            try
            {
                // Check for existing progress file (resume scenario)
                int? resumeFromId = null;
                if (progressTracker.ProgressFileExists())
                {
                    var existingProgress = await progressTracker.LoadOrCreateAsync(connectionString, "validate", batchSize);
                    resumeFromId = progressTracker.GetResumeStartId();

                    if (!quiet && resumeFromId.HasValue)
                    {
                        AnsiConsole.MarkupLine(
                            $"[yellow]Resuming from TextTableID {resumeFromId.Value} " +
                            $"(session {existingProgress.ResumeCount}, " +
                            $"{existingProgress.TotalObservations:N0} observations so far)[/]");
                        AnsiConsole.WriteLine();
                    }
                }
                else
                {
                    await progressTracker.LoadOrCreateAsync(connectionString, "validate", batchSize);
                }

                BatchValidationReport? report = null;

                await AnsiConsole.Progress()
                    .AutoRefresh(true)
                    .AutoClear(false)
                    .HideCompleted(false)
                    .Columns(new ProgressColumn[]
                    {
                        new TaskDescriptionColumn(),
                        new ProgressBarColumn(),
                        new PercentageColumn(),
                        new RemainingTimeColumn(),
                        new SpinnerColumn()
                    })
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("Stage 3+4: Parsing + Validating", maxValue: 100);

                        var progress = new Progress<TransformBatchProgress>(p =>
                        {
                            task.Value = p.TotalBatches > 0
                                ? (double)p.BatchNumber / p.TotalBatches * 100
                                : 0;
                            task.Description =
                                $"Batch {p.BatchNumber}/{p.TotalBatches} " +
                                $"[[{p.RangeStart}-{p.RangeEnd}]] " +
                                $"{p.CumulativeObservationCount:N0} obs, " +
                                $"{p.TablesSkippedThisBatch} skipped";

                            progressTracker.UpdateProgressAsync(p).GetAwaiter().GetResult();
                        });

                        report = await orchestrator.ProcessAllWithValidationAsync(
                            batchSize, progress, resumeFromId, maxBatches, cts.Token);

                        task.Value = 100;
                        task.Description = $"Complete: {report.TotalObservations:N0} observations validated";
                    });

                stopwatch.Stop();

                // Success — clean up progress file
                await progressTracker.DeleteProgressFileAsync();

                if (!quiet && report != null)
                {
                    displayValidationReport(report, verbose);
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                await progressTracker.RecordInterruptionAsync("User cancellation", stopwatch.Elapsed);
                displayCancellationMessage(quiet);
                return 1;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await progressTracker.RecordInterruptionAsync($"Error: {ex.Message}", stopwatch.Elapsed);

                if (!quiet)
                {
                    AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
                    if (verbose)
                    {
                        AnsiConsole.WriteException(ex);
                    }
                }
                return 1;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Truncates the tmp_FlattenedStandardizedTable for a clean rerun.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="quiet">Suppress non-essential output.</param>
        /// <returns>Exit code: 0 for success, 1 for failure.</returns>
        /// <seealso cref="ITableParsingOrchestrator.TruncateAsync"/>
        public async Task<int> ExecuteTruncateAsync(string connectionString, bool quiet)
        {
            #region implementation

            var configuration = ConfigurationHelper.BuildMedRecProConfiguration(connectionString);
            using var serviceProvider = buildServiceProvider(connectionString, configuration, verbose: false, includeValidation: false);
            using var scope = serviceProvider.CreateScope();

            var orchestrator = scope.ServiceProvider.GetRequiredService<ITableParsingOrchestrator>();

            try
            {
                await orchestrator.TruncateAsync();

                // Also clean up any progress file since we're starting fresh
                var progressTracker = new StandardizationProgressTracker();
                if (progressTracker.ProgressFileExists())
                {
                    await progressTracker.LoadOrCreateAsync(connectionString, "truncate", 0);
                    await progressTracker.DeleteProgressFileAsync();
                }

                if (!quiet)
                {
                    AnsiConsole.MarkupLine("[green]tmp_FlattenedStandardizedTable truncated successfully.[/]");
                }

                return 0;
            }
            catch (Exception ex)
            {
                if (!quiet)
                {
                    AnsiConsole.MarkupLine($"[red]Error truncating table: {Markup.Escape(ex.Message)}[/]");
                }
                return 1;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Debug path: parse a single table and display observations without DB write.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="textTableId">The TextTableID to parse.</param>
        /// <param name="verbose">Enable verbose output.</param>
        /// <returns>Exit code: 0 for success, 1 for failure.</returns>
        /// <seealso cref="ITableParsingOrchestrator.ParseSingleTableAsync"/>
        public async Task<int> ExecuteParseSingleAsync(string connectionString, int textTableId, bool verbose)
        {
            #region implementation

            var configuration = ConfigurationHelper.BuildMedRecProConfiguration(connectionString);
            using var serviceProvider = buildServiceProvider(connectionString, configuration, verbose, includeValidation: false);
            using var scope = serviceProvider.CreateScope();

            var orchestrator = scope.ServiceProvider.GetRequiredService<ITableParsingOrchestrator>();

            try
            {
                var observations = await orchestrator.ParseSingleTableAsync(textTableId);

                if (observations.Count == 0)
                {
                    AnsiConsole.MarkupLine(
                        $"[yellow]No observations produced for TextTableID={textTableId}. " +
                        $"Table may not exist, was skipped, or produced empty results.[/]");
                    return 0;
                }

                displayParseSingleResults(observations, textTableId);
                return 0;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error parsing TextTableID={textTableId}: {Markup.Escape(ex.Message)}[/]");
                if (verbose)
                {
                    AnsiConsole.WriteException(ex);
                }
                return 1;
            }

            #endregion
        }

        #endregion

        #region private methods — DI

        /**************************************************************/
        /// <summary>
        /// Builds a service provider with all dependencies needed for table standardization.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="verbose">Whether verbose logging is enabled.</param>
        /// <param name="includeValidation">Whether to register Stage 4 validation services.</param>
        /// <returns>ServiceProvider with configured services.</returns>
        /// <seealso cref="ITableParsingOrchestrator"/>
        private static ServiceProvider buildServiceProvider(
            string connectionString,
            IConfiguration configuration,
            bool verbose,
            bool includeValidation)
        {
            #region implementation

            // Build composite configuration: in-memory settings + appsettings.json + user secrets
            // appsettings.json provides ClaudeApiCorrectionSettings; user secrets provides the API key
            var compositeConfiguration = new ConfigurationBuilder()
                .AddConfiguration(configuration)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddUserSecrets(typeof(Program).Assembly, optional: true)
                .Build();

            var services = new ServiceCollection();

            // Add configuration
            services.AddSingleton<IConfiguration>(compositeConfiguration);

            // Add logging — always show warnings (parse errors, skipped tables) to console;
            // verbose mode lowers to Debug for full detail
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning);
            });

            // Add DbContext
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
                options.EnableSensitiveDataLogging(false);

                if (!verbose)
                {
                    options.ConfigureWarnings(warnings =>
                        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.AmbientTransactionWarning)
                    );
                    options.LogTo(_ => { }, LogLevel.None);
                }
            });

            // Add generic logger for Repository
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            // Add core services
            services.AddScoped(typeof(Repository<>), typeof(Repository<>));
            services.AddTransient<StringCipher>();

            // Stage 3: SPL Table Normalization — Section-Aware Parsing
            services.AddScoped<ITableCellContextService, TableCellContextService>();
            services.AddScoped<ITableReconstructionService, TableReconstructionService>();
            services.AddScoped<ITableParser, PkTableParser>();
            services.AddScoped<ITableParser, SimpleArmTableParser>();
            services.AddScoped<ITableParser, MultilevelAeTableParser>();
            services.AddScoped<ITableParser, AeWithSocTableParser>();
            services.AddScoped<ITableParser, EfficacyMultilevelTableParser>();
            services.AddScoped<ITableParser, BmdTableParser>();
            services.AddScoped<ITableParser, TissueRatioTableParser>();
            services.AddScoped<ITableParser, DosingTableParser>();
            services.AddScoped<ITableParserRouter, TableParserRouter>();

            // Stage 4: Validation (optional)
            if (includeValidation)
            {
                services.AddScoped<IRowValidationService, RowValidationService>();
                services.AddScoped<ITableValidationService, TableValidationService>();
                services.AddScoped<IBatchValidationService, BatchValidationService>();
            }

            // Stage 3.5: Claude API Correction (optional — graceful no-op if API key missing)
            services.Configure<ClaudeApiCorrectionSettings>(
                compositeConfiguration.GetSection("ClaudeApiCorrectionSettings"));
            services.AddHttpClient<IClaudeApiCorrectionService, ClaudeApiCorrectionService>(
                (sp, client) =>
                {
                    var settings = sp.GetRequiredService<IOptions<ClaudeApiCorrectionSettings>>().Value;
                    if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                    {
                        client.DefaultRequestHeaders.Add("x-api-key", settings.ApiKey);
                    }
                    client.DefaultRequestHeaders.Add("anthropic-version", settings.AnthropicVersion);
                    client.BaseAddress = new Uri("https://api.anthropic.com/");
                });

            // Orchestrator — IBatchValidationService and IClaudeApiCorrectionService are optional (nullable constructor params)
            services.AddScoped<ITableParsingOrchestrator, TableParsingOrchestrator>();

            return services.BuildServiceProvider();

            #endregion
        }

        #endregion

        #region private methods — display

        /**************************************************************/
        /// <summary>
        /// Sets up Ctrl+C handler for graceful cancellation.
        /// </summary>
        private static void setupCancellationHandler(CancellationTokenSource cts, bool quiet)
        {
            #region implementation

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                if (!quiet)
                {
                    AnsiConsole.MarkupLine("[yellow]Cancellation requested... finishing current batch.[/]");
                }
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays the cancellation message with resume instructions.
        /// </summary>
        private static void displayCancellationMessage(bool quiet)
        {
            #region implementation

            if (!quiet)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Operation cancelled. Progress has been saved.[/]");
                AnsiConsole.MarkupLine("[grey]Re-run the same command to resume from where you left off.[/]");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays parse results summary.
        /// </summary>
        private static void displayParseResults(int totalObservations, TimeSpan elapsed)
        {
            #region implementation

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold green]Parse Complete[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("[bold]Metric[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Value[/]"));

            table.AddRow("Total Observations", $"[green]{totalObservations:N0}[/]");
            table.AddRow("Elapsed Time", elapsed.ToString(@"hh\:mm\:ss\.fff"));

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays the full validation report with tables, confidence distribution, and issues.
        /// </summary>
        /// <param name="report">The batch validation report.</param>
        /// <param name="verbose">Whether to show individual issues.</param>
        /// <seealso cref="BatchValidationReport"/>
        private static void displayValidationReport(BatchValidationReport report, bool verbose)
        {
            #region implementation

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold green]Validation Report[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();

            // Summary table
            var summary = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold]Summary[/]")
                .AddColumn(new TableColumn("[bold]Metric[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Value[/]"));

            summary.AddRow("Tables Processed", $"[green]{report.TotalTablesProcessed:N0}[/]");
            summary.AddRow("Tables Skipped", $"[yellow]{report.TotalTablesSkipped:N0}[/]");
            summary.AddRow("Total Observations", $"[green]{report.TotalObservations:N0}[/]");
            summary.AddRow("Pass Flags", $"[green]{report.PassFlagCount:N0}[/]");
            summary.AddRow("Warning Flags", $"[yellow]{report.WarnFlagCount:N0}[/]");

            AnsiConsole.Write(summary);
            AnsiConsole.WriteLine();

            // Skip reasons breakdown — always show when tables were skipped
            if (report.SkipReasons.Count > 0)
            {
                var skipTable = new Table()
                    .Border(TableBorder.Rounded)
                    .Title("[bold yellow]Skip Reasons[/]")
                    .AddColumn(new TableColumn("[bold]Reason[/]").NoWrap())
                    .AddColumn(new TableColumn("[bold]Count[/]"));

                foreach (var kvp in report.SkipReasons.OrderByDescending(x => x.Value))
                {
                    skipTable.AddRow(Markup.Escape(kvp.Key), $"{kvp.Value:N0}");
                }

                AnsiConsole.Write(skipTable);
                AnsiConsole.WriteLine();
            }

            // Confidence distribution
            var confidence = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold]Confidence Distribution[/]")
                .AddColumn(new TableColumn("[bold]Level[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Count[/]"))
                .AddColumn(new TableColumn("[bold]Percentage[/]"));

            var total = Math.Max(report.TotalObservations, 1);
            confidence.AddRow("[green]High (>= 0.9)[/]",
                $"{report.HighConfidenceCount:N0}",
                $"{(double)report.HighConfidenceCount / total:P1}");
            confidence.AddRow("[yellow]Medium (0.5-0.9)[/]",
                $"{report.MediumConfidenceCount:N0}",
                $"{(double)report.MediumConfidenceCount / total:P1}");
            confidence.AddRow("[red]Low (< 0.5)[/]",
                $"{report.LowConfidenceCount:N0}",
                $"{(double)report.LowConfidenceCount / total:P1}");

            AnsiConsole.Write(confidence);
            AnsiConsole.WriteLine();

            // Category breakdown
            if (report.RowCountByCategory.Count > 0)
            {
                var categories = new Table()
                    .Border(TableBorder.Rounded)
                    .Title("[bold]By Category[/]")
                    .AddColumn(new TableColumn("[bold]Category[/]").NoWrap())
                    .AddColumn(new TableColumn("[bold]Count[/]"));

                foreach (var kvp in report.RowCountByCategory.OrderByDescending(x => x.Value))
                {
                    categories.AddRow(Markup.Escape(kvp.Key), $"{kvp.Value:N0}");
                }

                AnsiConsole.Write(categories);
                AnsiConsole.WriteLine();
            }

            // Parse rule breakdown
            if (report.RowCountByParseRule.Count > 0)
            {
                var rules = new Table()
                    .Border(TableBorder.Rounded)
                    .Title("[bold]By Parse Rule[/]")
                    .AddColumn(new TableColumn("[bold]Rule[/]").NoWrap())
                    .AddColumn(new TableColumn("[bold]Count[/]"));

                foreach (var kvp in report.RowCountByParseRule.OrderByDescending(x => x.Value))
                {
                    rules.AddRow(Markup.Escape(kvp.Key), $"{kvp.Value:N0}");
                }

                AnsiConsole.Write(rules);
                AnsiConsole.WriteLine();
            }

            // Issues summary
            var issueCount = report.RowIssues.Count + report.TableIssues.Count;
            var discrepancyCount = report.CrossVersionDiscrepancies.Count;

            if (issueCount > 0 || discrepancyCount > 0)
            {
                AnsiConsole.Write(new Rule("[bold yellow]Issues[/]").RuleStyle("grey"));
                AnsiConsole.WriteLine();

                AnsiConsole.MarkupLine($"  Row Issues: [yellow]{report.RowIssues.Count:N0}[/]");
                AnsiConsole.MarkupLine($"  Table Issues: [yellow]{report.TableIssues.Count:N0}[/]");
                AnsiConsole.MarkupLine($"  Cross-Version Discrepancies: [yellow]{discrepancyCount:N0}[/]");
                AnsiConsole.WriteLine();

                if (verbose)
                {
                    displayDetailedIssues(report);
                }
                else
                {
                    AnsiConsole.MarkupLine("[grey]Use --verbose to see individual issues.[/]");
                    AnsiConsole.WriteLine();
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays detailed individual issues when verbose mode is enabled.
        /// </summary>
        private static void displayDetailedIssues(BatchValidationReport report)
        {
            #region implementation

            // Row issues (show first 50)
            if (report.RowIssues.Count > 0)
            {
                var rowTable = new Table()
                    .Border(TableBorder.Simple)
                    .Title("[bold]Row Issues (first 50)[/]")
                    .AddColumn("TableID")
                    .AddColumn("Row")
                    .AddColumn("Arm")
                    .AddColumn("Issues");

                foreach (var row in report.RowIssues.Take(50))
                {
                    rowTable.AddRow(
                        row.TextTableID?.ToString() ?? "-",
                        row.SourceRowSeq?.ToString() ?? "-",
                        Markup.Escape(row.TreatmentArm ?? "-"),
                        Markup.Escape(string.Join("; ", row.Issues)));
                }

                AnsiConsole.Write(rowTable);
                AnsiConsole.WriteLine();
            }

            // Cross-version discrepancies
            if (report.CrossVersionDiscrepancies.Count > 0)
            {
                var cvTable = new Table()
                    .Border(TableBorder.Simple)
                    .Title("[bold]Cross-Version Discrepancies[/]")
                    .AddColumn("Product")
                    .AddColumn("Category")
                    .AddColumn("V1")
                    .AddColumn("Count1")
                    .AddColumn("V2")
                    .AddColumn("Count2");

                foreach (var d in report.CrossVersionDiscrepancies.Take(50))
                {
                    cvTable.AddRow(
                        Markup.Escape(d.ProductTitle ?? "-"),
                        Markup.Escape(d.TableCategory ?? "-"),
                        d.VersionNumber?.ToString() ?? "-",
                        d.RowCount.ToString("N0"),
                        d.ComparedVersionNumber?.ToString() ?? "-",
                        d.ComparedRowCount.ToString("N0"));
                }

                AnsiConsole.Write(cvTable);
                AnsiConsole.WriteLine();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays parse-single results as a table of observations.
        /// </summary>
        private static void displayParseSingleResults(List<ParsedObservation> observations, int textTableId)
        {
            #region implementation

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[bold green]TextTableID={textTableId} — {observations.Count} observations[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("[bold]Parameter[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Arm[/]"))
                .AddColumn(new TableColumn("[bold]Raw Value[/]"))
                .AddColumn(new TableColumn("[bold]Primary[/]"))
                .AddColumn(new TableColumn("[bold]Type[/]"))
                .AddColumn(new TableColumn("[bold]Confidence[/]"))
                .AddColumn(new TableColumn("[bold]Rule[/]"));

            foreach (var obs in observations)
            {
                var confidenceColor = obs.ParseConfidence switch
                {
                    >= 0.9 => "green",
                    >= 0.5 => "yellow",
                    _ => "red"
                };

                table.AddRow(
                    Markup.Escape(obs.ParameterName ?? "-"),
                    Markup.Escape(obs.TreatmentArm ?? "-"),
                    Markup.Escape(obs.RawValue ?? "-"),
                    obs.PrimaryValue?.ToString("G") ?? "-",
                    Markup.Escape(obs.PrimaryValueType ?? "-"),
                    $"[{confidenceColor}]{obs.ParseConfidence:F2}[/]",
                    Markup.Escape(obs.ParseRule ?? "-"));
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            #endregion
        }

        #endregion
    }
}
