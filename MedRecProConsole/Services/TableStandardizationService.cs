using MedRecProImportClass.Data;
using MedRecProImportClass.DataAccess;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using MedRecProConsole.Helpers;
using MedRecProConsole.Models;
using MedRecProConsole.Services.Reporting;
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
    /// Supports five operations mapped to the pipeline stages:
    /// - truncate: Wipe the output table for a clean rerun (pre-pipeline)
    /// - parse-single: Stages 1→2→3→3.5 for a single table (debug, no DB write)
    /// - parse: Stage 3 batch — parse all tables, write to tmp_FlattenedStandardizedTable
    /// - parse-stages: Stages 1→2→3→3.5 batch with intermediate visibility
    /// - validate: Stage 3+4 batch — parse + validate with coverage report
    ///
    /// Pipeline stages (aligned with MedRecProImportClass):
    /// - Stage 1: Get Data (<see cref="ITableCellContextService"/>)
    /// - Stage 2: Pivot Table (<see cref="ITableReconstructionService"/>)
    /// - Stage 3: Standardize (<see cref="ITableParserRouter"/> + parsers)
    /// - Stage 3.5: Claude Enhance (<see cref="IClaudeApiCorrectionService"/>)
    /// - Stage 4: Validate (<see cref="IBatchValidationService"/>)
    ///
    /// Ctrl+C handling saves progress for resumption via <see cref="StandardizationProgressTracker"/>.
    /// </remarks>
    /// <seealso cref="ITableParsingOrchestrator"/>
    /// <seealso cref="StandardizationProgressTracker"/>
    /// <seealso cref="BatchValidationReport"/>
    public class TableStandardizationService
    {
        #region private types

        /**************************************************************/
        /// <summary>
        /// Encapsulates the shared initialization state for batch pipeline runs.
        /// Returned by <see cref="initializeRunAsync"/> to reduce duplication across
        /// <see cref="ExecuteParseAsync"/>, <see cref="ExecuteParseWithStagesAsync"/>,
        /// and <see cref="ExecuteValidateAsync"/>.
        /// </summary>
        private record RunContext(
            ITableParsingOrchestrator Orchestrator,
            ITableCellContextService CellContextService,
            CancellationTokenSource Cts,
            StandardizationProgressTracker ProgressTracker,
            Stopwatch Stopwatch,
            int? ResumeFromId,
            IServiceScope Scope,
            ServiceProvider ServiceProvider) : IDisposable
        {
            #region implementation

            public void Dispose()
            {
                Cts.Dispose();
                Scope.Dispose();
                ServiceProvider.Dispose();
            }

            #endregion
        }

        #endregion

        #region public methods — pre-pipeline

        /**************************************************************/
        /// <summary>
        /// Truncates the output table and removes any progress tracking file.
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

        #endregion

        #region public methods — Stage 1→2→3→3.5 single table

        /**************************************************************/
        /// <summary>
        /// Debug path: parse a single table with stage-by-stage visibility.
        /// Displays intermediate results from Stage 2 (Pivot Table), Stage 3 (Standardize),
        /// and optionally Stage 3.5 (Claude Enhance). No database write.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="textTableId">The TextTableID to parse.</param>
        /// <param name="verbose">Enable verbose output.</param>
        /// <param name="useClaude">Whether to apply Claude AI enhancement (Stage 3.5).</param>
        /// <param name="reportSink">Optional markdown report sink. When non-null, a
        /// per-table markdown section mirroring the console output is appended.</param>
        /// <param name="jsonSink">Optional NDJSON companion sink. When non-null, one
        /// JSON line per observation (plus one meta line per zero-obs table) is appended
        /// alongside the markdown log — enabling programmatic diffing and column-level
        /// audits.</param>
        /// <returns>Exit code: 0 for success, 1 for failure.</returns>
        /// <seealso cref="ITableParsingOrchestrator.ReconstructSingleTableAsync"/>
        /// <seealso cref="ITableParsingOrchestrator.RouteAndParseSingleTable"/>
        /// <seealso cref="ITableParsingOrchestrator.CorrectObservationsAsync"/>
        /// <seealso cref="MarkdownReportSink"/>
        /// <seealso cref="JsonReportSink"/>
        public async Task<int> ExecuteParseSingleAsync(string connectionString, int textTableId, bool verbose,
            bool useClaude = true, MarkdownReportSink? reportSink = null, JsonReportSink? jsonSink = null)
        {
            #region implementation

            var configuration = ConfigurationHelper.BuildMedRecProConfiguration(connectionString);
            using var serviceProvider = buildServiceProvider(connectionString, configuration, verbose,
                includeValidation: false, disableClaude: !useClaude);
            using var scope = serviceProvider.CreateScope();

            var orchestrator = scope.ServiceProvider.GetRequiredService<ITableParsingOrchestrator>();

            try
            {
                // Stage 2: Pivot Table
                AnsiConsole.Write(new Rule("[bold blue]Stage 2: Pivot Table[/]").RuleStyle("grey"));
                AnsiConsole.WriteLine();

                var table = await orchestrator.ReconstructSingleTableAsync(textTableId);

                if (table == null)
                {
                    AnsiConsole.MarkupLine(
                        $"[yellow]No table found for TextTableID={textTableId}. " +
                        $"The table may not exist in the database.[/]");
                    return 0;
                }

                displayReconstructedTable(table);

                // Stage 3: Standardize
                AnsiConsole.Write(new Rule("[bold blue]Stage 3: Standardize[/]").RuleStyle("grey"));
                AnsiConsole.WriteLine();

                var (category, parserName, observations) = orchestrator.RouteAndParseSingleTable(table);

                displayRoutingResult(category, parserName);

                Dictionary<int, string?>? beforeFlags = null;

                if (observations.Count == 0)
                {
                    AnsiConsole.MarkupLine(
                        $"[yellow]No observations produced. Table was {(parserName == null ? "skipped" : "parsed but returned empty results")}.[/]");

                    // Still emit a markdown / JSON section so the diagnostic log captures
                    // empty/skipped tables and their routing decisions.
                    if (reportSink != null || jsonSink != null)
                    {
                        var emptyEntry = BuildReportEntry(
                            table, category, parserName, observations, beforeFlags, claudeSkipped: true);
                        if (reportSink != null)
                            await reportSink.AppendAsync(emptyEntry);
                        if (jsonSink != null)
                            await jsonSink.AppendAsync(emptyEntry);
                    }
                    return 0;
                }

                displayParseSingleResults(observations, textTableId);

                // Stage 3.5: Claude Enhance (optional)
                if (useClaude)
                {
                    AnsiConsole.Write(new Rule("[bold blue]Stage 3.5: Claude Enhance[/]").RuleStyle("grey"));
                    AnsiConsole.WriteLine();

                    // Snapshot ValidationFlags before correction for diff display
                    beforeFlags = observations.ToDictionary(
                        o => (o.SourceRowSeq ?? 0) * 10000 + (o.SourceCellSeq ?? 0),
                        o => o.ValidationFlags);

                    observations = await orchestrator.CorrectObservationsAsync(observations);

                    displayClaudeCorrections(observations, beforeFlags);
                }
                else
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[grey]Stage 3.5: Claude Enhance skipped (--no-claude)[/]");
                    AnsiConsole.WriteLine();
                }

                if (reportSink != null || jsonSink != null)
                {
                    var finalEntry = BuildReportEntry(
                        table, category, parserName, observations, beforeFlags, claudeSkipped: !useClaude);
                    if (reportSink != null)
                        await reportSink.AppendAsync(finalEntry);
                    if (jsonSink != null)
                        await jsonSink.AppendAsync(finalEntry);
                }

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

        #region public methods — Stage 3 batch

        /**************************************************************/
        /// <summary>
        /// Executes Stage 3 (Standardize) in batch mode: truncate → batch loop → write observations.
        /// Uses the orchestrator's internal batch processing without intermediate visibility.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="batchSize">Tables per batch (default 1000).</param>
        /// <param name="verbose">Enable verbose logging output.</param>
        /// <param name="quiet">Suppress non-essential output.</param>
        /// <param name="disableClaude">Whether to disable Claude AI enhancement (Stage 3.5).</param>
        /// <param name="dropRowsMissingArmNOrPrimaryValue">
        /// When true, enables the Stage 3.25 quality gate that drops rows where either
        /// ArmN or PrimaryValue is null. Default false (backward compatible).
        /// </param>
        /// <param name="reportSink">Optional markdown report sink. Accepted for signature
        /// symmetry; this pipeline does not surface per-table intermediate data, so if
        /// non-null a one-line warning is shown and no markdown is written. Use
        /// <see cref="ExecuteParseWithStagesAsync"/> or <see cref="ExecuteParseSingleAsync"/>
        /// for markdown logging.</param>
        /// <param name="jsonSink">Optional NDJSON companion sink. Same semantics as
        /// <paramref name="reportSink"/> — accepted for signature symmetry but not
        /// written to in Stage 3 batch mode.</param>
        /// <returns>Exit code: 0 for success, 1 for failure.</returns>
        /// <seealso cref="ITableParsingOrchestrator.ProcessAllAsync"/>
        public async Task<int> ExecuteParseAsync(string connectionString, int batchSize, bool verbose, bool quiet,
            bool disableClaude = false, bool dropRowsMissingArmNOrPrimaryValue = false,
            MarkdownReportSink? reportSink = null, JsonReportSink? jsonSink = null,
            bool disableBioequivalentDedup = false)
        {
            #region implementation

            if (reportSink != null && !quiet)
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Warning:[/] [grey]--markdown-log is not supported in Stage 3 batch mode " +
                    "(no per-table pivot data). Use parse-single or parse-stages instead.[/]");
            }

            if (jsonSink != null && !quiet)
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Warning:[/] [grey]--json-log is not supported in Stage 3 batch mode " +
                    "(no per-table observation data). Use parse-single or parse-stages instead.[/]");
            }

            using var ctx = await initializeRunAsync(connectionString, "parse", batchSize, verbose, quiet,
                includeValidation: false, disableClaude: disableClaude,
                dropRowsMissingArmNOrPrimaryValue: dropRowsMissingArmNOrPrimaryValue);

            try
            {
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
                        new ElapsedTimeColumn(),
                        new SpinnerColumn()
                    })
                    .StartAsync(async pctx =>
                    {
                        var task = pctx.AddTask("Stage 3: Standardizing", maxValue: 100);

                        // Per-batch callback: persists progress to disk for resumption
                        var batchProgress = new Progress<TransformBatchProgress>(p =>
                        {
                            task.Description =
                                $"Batch {p.BatchNumber}/{p.TotalBatches} complete — " +
                                $"{p.CumulativeObservationCount:N0} obs";

                            ctx.ProgressTracker.UpdateProgressAsync(p).GetAwaiter().GetResult();
                        });

                        // Per-table callback: drives the UI progress bar within each batch.
                        // Must be synchronous — Progress<T> posts to ThreadPool in console apps,
                        // delaying updates until after the batch completes. Spectre.Console's
                        // auto-refresh timer (separate thread) handles rendering; we just set values.
                        var rowProgress = new SynchronousProgress<TransformBatchProgress>(p =>
                        {
                            var batchFraction = p.TotalTablesInBatch > 0
                                ? (double)p.TablesProcessedInBatch / p.TotalTablesInBatch
                                : 0.0;
                            var overallPct = p.TotalBatches > 0
                                ? ((p.BatchNumber - 1) + batchFraction) / p.TotalBatches * 100
                                : batchFraction * 100;

                            task.Value = overallPct;
                            task.Description =
                                $"Batch {p.BatchNumber}/{p.TotalBatches} " +
                                $"[[{p.RangeStart}-{p.RangeEnd}]] " +
                                $"Table {p.TablesProcessedInBatch}/{p.TotalTablesInBatch} — " +
                                $"{p.CumulativeObservationCount:N0} obs";
                        });

                        totalObs = await ctx.Orchestrator.ProcessAllAsync(
                            batchSize, batchProgress, ctx.ResumeFromId,
                            rowProgress: rowProgress,
                            disableBioequivalentDedup: disableBioequivalentDedup,
                            ct: ctx.Cts.Token);

                        task.Value = 100;
                        task.Description = $"Complete: {totalObs:N0} observations";
                    });

                return await handleCompletionAsync(ctx, totalObs, quiet);
            }
            catch (OperationCanceledException)
            {
                return await handleCancellationAsync(ctx, quiet);
            }
            catch (Exception ex)
            {
                return await handleErrorAsync(ctx, ex, verbose, quiet);
            }

            #endregion
        }

        #endregion

        #region public methods — Stage 1→2→3→3.5 batch with visibility

        /**************************************************************/
        /// <summary>
        /// Executes the full standardization pipeline (Stages 1→2→3→3.5) with stage-by-stage visibility.
        /// Each batch calls <see cref="ITableParsingOrchestrator.ProcessBatchWithStagesAsync"/>
        /// to capture intermediate results, then displays them according to the detail level.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="batchSize">Tables per batch (default 1000).</param>
        /// <param name="verbose">Enable verbose logging output.</param>
        /// <param name="quiet">Suppress non-essential output.</param>
        /// <param name="disableClaude">Whether to disable Claude AI enhancement (Stage 3.5).</param>
        /// <param name="maxBatches">Optional maximum number of batches. Null = all.</param>
        /// <param name="detailLevel">How much per-batch stage detail to display.</param>
        /// <param name="dropRowsMissingArmNOrPrimaryValue">
        /// When true, enables the Stage 3.25 quality gate that drops rows where either
        /// ArmN or PrimaryValue is null. Default false (backward compatible).
        /// </param>
        /// <returns>Exit code: 0 for success, 1 for failure.</returns>
        /// <seealso cref="ITableParsingOrchestrator.ProcessBatchWithStagesAsync"/>
        /// <seealso cref="StageDetailLevel"/>
        public async Task<int> ExecuteParseWithStagesAsync(
            string connectionString, int batchSize, bool verbose, bool quiet,
            bool disableClaude = false, int? maxBatches = null,
            StageDetailLevel detailLevel = StageDetailLevel.None,
            bool dropRowsMissingArmNOrPrimaryValue = false,
            MarkdownReportSink? reportSink = null,
            JsonReportSink? jsonSink = null,
            bool disableBioequivalentDedup = false)
        {
            #region implementation

            using var ctx = await initializeRunAsync(connectionString, "parse-stages", batchSize, verbose, quiet,
                includeValidation: false, disableClaude: disableClaude,
                dropRowsMissingArmNOrPrimaryValue: dropRowsMissingArmNOrPrimaryValue);

            try
            {
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
                        new ElapsedTimeColumn(),
                        new SpinnerColumn()
                    })
                    .StartAsync(async pctx =>
                    {
                        var task = pctx.AddTask("Stage 3+4: Standardize + Validate", maxValue: 100);
                        var statusTask = pctx.AddTask("Initializing...");
                        statusTask.IsIndeterminate = true;

                        // UNII-ordered document batching: process documents grouped by active ingredient
                        // so the ML training accumulator gets concentrated product data per batch
                        var orderedGuidsRaw = await ctx.CellContextService.GetDocumentGuidsOrderedByUniiAsync(ctx.Cts.Token);

                        // Stage 0: bioequivalent-ANDA dedup. This method drives its own batch loop,
                        // so the dedup service is resolved from DI here rather than relying on the
                        // orchestrator's internal filter.
                        var orderedGuids = orderedGuidsRaw;
                        if (!disableBioequivalentDedup)
                        {
                            var dedupService = ctx.Scope.ServiceProvider.GetService<IBioequivalentLabelDedupService>();
                            if (dedupService != null)
                            {
                                var dedupResult = await dedupService.DeduplicateAsync(
                                    orderedGuidsRaw, options: null, ctx.Cts.Token);
                                orderedGuids = dedupResult.KeptDocumentGuids.ToList();
                            }
                        }

                        var totalBatches = (int)Math.Ceiling((double)orderedGuids.Count / batchSize);
                        if (maxBatches.HasValue)
                            totalBatches = Math.Min(totalBatches, maxBatches.Value);

                        var totalObservations = 0;
                        var batchNumber = 0;

                        for (int i = 0; i < orderedGuids.Count; i += batchSize)
                        {
                            ctx.Cts.Token.ThrowIfCancellationRequested();
                            batchNumber++;

                            if (maxBatches.HasValue && batchNumber > maxBatches.Value)
                                break;

                            var guids = orderedGuids.Skip(i).Take(batchSize).ToList();
                            var filter = new TableCellContextFilter
                            {
                                DocumentGUIDs = guids
                            };

                            // Capture batch-local vars for the closure
                            var currentBatch = batchNumber;

                            // Intra-batch progress callback
                            var rowProgress = new SynchronousProgress<TransformBatchProgress>(p =>
                            {
                                // Scale intra-batch % to overall: (batchNumber-1 + intraPct/100) / totalBatches * 100
                                var overallPct = totalBatches > 0
                                    ? ((currentBatch - 1.0) + p.IntraBatchPercent / 100.0) / totalBatches * 100.0
                                    : p.IntraBatchPercent;
                                task.Value = Math.Min(overallPct, 100.0);
                                task.Description =
                                    $"Batch {currentBatch}/{totalBatches}";
                                statusTask.Description = p.CurrentOperation ?? "Processing...";
                            });

                            var stageResult = await ctx.Orchestrator.ProcessBatchWithStagesAsync(filter, rowProgress, ctx.Cts.Token);
                            totalObservations += stageResult.ObservationsWritten;

                            // Emit per-table markdown / JSON sections from this batch's intermediate data
                            if (reportSink != null || jsonSink != null)
                            {
                                await appendBatchToReportAsync(
                                    reportSink, jsonSink, stageResult, disableClaude, ctx.Cts.Token);
                            }

                            // Update progress bar after batch completes
                            var batchCompletePct = totalBatches > 0
                                ? (double)batchNumber / totalBatches * 100
                                : 0;
                            task.Value = batchCompletePct;
                            task.Description =
                                $"Batch {batchNumber}/{totalBatches} " +
                                $"{stageResult.ObservationsWritten} obs — " +
                                $"{totalObservations:N0} cumulative";
                            statusTask.Description = "Waiting for next batch...";

                            await ctx.ProgressTracker.UpdateProgressAsync(new TransformBatchProgress
                            {
                                BatchNumber = batchNumber,
                                TotalBatches = totalBatches,
                                BatchObservationCount = stageResult.ObservationsWritten,
                                CumulativeObservationCount = totalObservations,
                                TablesSkippedThisBatch = stageResult.SkipReasons.Count,
                                Elapsed = ctx.Stopwatch.Elapsed
                            });
                        }

                        totalObs = totalObservations;
                        task.Value = 100;
                        task.Description = $"Complete: {totalObs:N0} observations";
                        statusTask.Description = "Done";
                        statusTask.IsIndeterminate = false;
                        statusTask.Value = 100;
                    });

                // Display stage detail after progress bar completes (if requested)
                // Stage detail is shown post-run to avoid interfering with progress rendering

                return await handleCompletionAsync(ctx, totalObs, quiet);
            }
            catch (OperationCanceledException)
            {
                return await handleCancellationAsync(ctx, quiet);
            }
            catch (Exception ex)
            {
                return await handleErrorAsync(ctx, ex, verbose, quiet);
            }

            #endregion
        }

        #endregion

        #region public methods — Stage 3+4 batch with validation

        /**************************************************************/
        /// <summary>
        /// Executes Stage 3 (Standardize) + Stage 4 (Validate) in batch mode with coverage reporting.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="batchSize">Tables per batch (default 1000).</param>
        /// <param name="verbose">Enable verbose logging output.</param>
        /// <param name="quiet">Suppress non-essential output.</param>
        /// <param name="maxBatches">Optional maximum number of batches to process. Null = all.</param>
        /// <param name="disableClaude">Whether to disable Claude AI enhancement (Stage 3.5).</param>
        /// <param name="dropRowsMissingArmNOrPrimaryValue">
        /// When true, enables the Stage 3.25 quality gate that drops rows where either
        /// ArmN or PrimaryValue is null. Default false (backward compatible).
        /// </param>
        /// <param name="reportSink">Optional markdown report sink. Accepted for signature
        /// symmetry; this pipeline does not surface per-table intermediate data, so if
        /// non-null a one-line warning is shown and no markdown is written.</param>
        /// <param name="jsonSink">Optional NDJSON companion sink. Same semantics as
        /// <paramref name="reportSink"/> — accepted for signature symmetry but not
        /// written to in validate mode.</param>
        /// <returns>Exit code: 0 for success, 1 for failure.</returns>
        /// <seealso cref="ITableParsingOrchestrator.ProcessAllWithValidationAsync"/>
        /// <seealso cref="BatchValidationReport"/>
        public async Task<int> ExecuteValidateAsync(string connectionString, int batchSize, bool verbose, bool quiet,
            int? maxBatches = null, bool disableClaude = false,
            bool dropRowsMissingArmNOrPrimaryValue = false,
            MarkdownReportSink? reportSink = null,
            JsonReportSink? jsonSink = null,
            bool disableBioequivalentDedup = false)
        {
            #region implementation

            if (reportSink != null && !quiet)
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Warning:[/] [grey]--markdown-log is not supported in validate mode " +
                    "(no per-table pivot data). Use parse-single or parse-stages instead.[/]");
            }

            if (jsonSink != null && !quiet)
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Warning:[/] [grey]--json-log is not supported in validate mode " +
                    "(no per-table observation data). Use parse-single or parse-stages instead.[/]");
            }

            using var ctx = await initializeRunAsync(connectionString, "validate", batchSize, verbose, quiet,
                includeValidation: true, disableClaude: disableClaude,
                dropRowsMissingArmNOrPrimaryValue: dropRowsMissingArmNOrPrimaryValue);

            try
            {
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
                        new ElapsedTimeColumn(),
                        new SpinnerColumn()
                    })
                    .StartAsync(async pctx =>
                    {
                        var task = pctx.AddTask("Stage 3+4: Standardize + Validate", maxValue: 100);

                        // Per-batch callback: persists progress to disk for resumption
                        var batchProgress = new Progress<TransformBatchProgress>(p =>
                        {
                            task.Description =
                                $"Batch {p.BatchNumber}/{p.TotalBatches} complete — " +
                                $"{p.CumulativeObservationCount:N0} obs, " +
                                $"{p.TablesSkippedThisBatch} skipped";

                            ctx.ProgressTracker.UpdateProgressAsync(p).GetAwaiter().GetResult();
                        });

                        // Per-table callback: drives the UI progress bar within each batch.
                        // Must be synchronous — Progress<T> posts to ThreadPool in console apps,
                        // delaying updates until after the batch completes. Spectre.Console's
                        // auto-refresh timer (separate thread) handles rendering; we just set values.
                        var rowProgress = new SynchronousProgress<TransformBatchProgress>(p =>
                        {
                            var batchFraction = p.TotalTablesInBatch > 0
                                ? (double)p.TablesProcessedInBatch / p.TotalTablesInBatch
                                : 0.0;
                            var overallPct = p.TotalBatches > 0
                                ? ((p.BatchNumber - 1) + batchFraction) / p.TotalBatches * 100
                                : batchFraction * 100;

                            task.Value = overallPct;
                            task.Description =
                                $"Batch {p.BatchNumber}/{p.TotalBatches} " +
                                $"[[{p.RangeStart}-{p.RangeEnd}]] " +
                                $"Table {p.TablesProcessedInBatch}/{p.TotalTablesInBatch} — " +
                                $"{p.CumulativeObservationCount:N0} obs";
                        });

                        report = await ctx.Orchestrator.ProcessAllWithValidationAsync(
                            batchSize, batchProgress, ctx.ResumeFromId, maxBatches,
                            rowProgress: rowProgress,
                            disableBioequivalentDedup: disableBioequivalentDedup,
                            ct: ctx.Cts.Token);

                        task.Value = 100;
                        task.Description = $"Complete: {report.TotalObservations:N0} observations validated";
                    });

                ctx.Stopwatch.Stop();
                await ctx.ProgressTracker.DeleteProgressFileAsync();

                if (!quiet && report != null)
                {
                    displayValidationReport(report, verbose);
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                return await handleCancellationAsync(ctx, quiet);
            }
            catch (Exception ex)
            {
                return await handleErrorAsync(ctx, ex, verbose, quiet);
            }

            #endregion
        }

        #endregion

        #region private methods — run lifecycle

        /**************************************************************/
        /// <summary>
        /// Builds a <see cref="RunContext"/> with all shared state for batch runs:
        /// service provider, scope, cancellation, progress tracker, and resume detection.
        /// </summary>
        /// <param name="connectionString">Database connection string.</param>
        /// <param name="mode">Progress tracker mode name (e.g., "parse", "validate").</param>
        /// <param name="batchSize">Tables per batch.</param>
        /// <param name="verbose">Enable verbose logging.</param>
        /// <param name="quiet">Suppress non-essential output.</param>
        /// <param name="includeValidation">Whether to register Stage 4 validation services.</param>
        /// <param name="disableClaude">Whether to disable Claude AI enhancement (Stage 3.5).</param>
        /// <param name="dropRowsMissingArmNOrPrimaryValue">
        /// Whether to enable the Stage 3.25 quality gate (drops rows missing ArmN or PrimaryValue).
        /// </param>
        /// <returns>An initialized <see cref="RunContext"/> ready for batch processing.</returns>
        private async Task<RunContext> initializeRunAsync(
            string connectionString, string mode, int batchSize,
            bool verbose, bool quiet, bool includeValidation, bool disableClaude = false,
            bool dropRowsMissingArmNOrPrimaryValue = false)
        {
            #region implementation

            var configuration = ConfigurationHelper.BuildMedRecProConfiguration(connectionString);
            var serviceProvider = buildServiceProvider(connectionString, configuration, verbose,
                includeValidation: includeValidation, disableClaude: disableClaude,
                dropRowsMissingArmNOrPrimaryValue: dropRowsMissingArmNOrPrimaryValue);
            var scope = serviceProvider.CreateScope();

            var orchestrator = scope.ServiceProvider.GetRequiredService<ITableParsingOrchestrator>();
            var cellContextService = scope.ServiceProvider.GetRequiredService<ITableCellContextService>();

            var cts = new CancellationTokenSource();
            var progressTracker = new StandardizationProgressTracker();
            var stopwatch = Stopwatch.StartNew();

            setupCancellationHandler(cts, quiet);

            int? resumeFromId = null;
            if (progressTracker.ProgressFileExists())
            {
                var existingProgress = await progressTracker.LoadOrCreateAsync(connectionString, mode, batchSize);
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
                await progressTracker.LoadOrCreateAsync(connectionString, mode, batchSize);
            }

            return new RunContext(orchestrator, cellContextService, cts, progressTracker, stopwatch, resumeFromId, scope, serviceProvider);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles successful batch run completion: stops timer, deletes progress file, displays summary.
        /// </summary>
        /// <returns>Exit code 0 (success).</returns>
        private static async Task<int> handleCompletionAsync(RunContext ctx, int totalObservations, bool quiet)
        {
            #region implementation

            ctx.Stopwatch.Stop();
            await ctx.ProgressTracker.DeleteProgressFileAsync();

            if (!quiet)
            {
                displayParseResults(totalObservations, ctx.Stopwatch.Elapsed);
            }

            return 0;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles user cancellation (Ctrl+C): records interruption, displays resume message.
        /// </summary>
        /// <returns>Exit code 1 (cancelled).</returns>
        private static async Task<int> handleCancellationAsync(RunContext ctx, bool quiet)
        {
            #region implementation

            ctx.Stopwatch.Stop();
            await ctx.ProgressTracker.RecordInterruptionAsync("User cancellation", ctx.Stopwatch.Elapsed);
            displayCancellationMessage(quiet);
            return 1;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles unexpected errors: records interruption, displays error message.
        /// </summary>
        /// <returns>Exit code 1 (error).</returns>
        private static async Task<int> handleErrorAsync(RunContext ctx, Exception ex, bool verbose, bool quiet)
        {
            #region implementation

            ctx.Stopwatch.Stop();
            await ctx.ProgressTracker.RecordInterruptionAsync($"Error: {ex.Message}", ctx.Stopwatch.Elapsed);

            if (!quiet)
            {
                AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
                if (verbose)
                {
                    AnsiConsole.WriteException(ex);
                }
            }
            return 1;

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
        /// <param name="includeValidation">Whether to register Stage 4 (Validate) services.</param>
        /// <param name="disableClaude">Whether to disable Stage 3.5 (Claude Enhance).</param>
        /// <param name="dropRowsMissingArmNOrPrimaryValue">
        /// When true, enables the Stage 3.25 quality gate (drops rows missing ArmN or PrimaryValue).
        /// Forwarded to <see cref="TableParsingOrchestrator"/> via an explicit factory registration.
        /// </param>
        /// <returns>ServiceProvider with configured services.</returns>
        /// <seealso cref="ITableParsingOrchestrator"/>
        private static ServiceProvider buildServiceProvider(
            string connectionString,
            IConfiguration configuration,
            bool verbose,
            bool includeValidation,
            bool disableClaude = false,
            bool dropRowsMissingArmNOrPrimaryValue = false)
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
            // Forward DbContext to ApplicationDbContext so services depending on DbContext resolve correctly
            services.AddScoped<DbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

            // Add generic logger for Repository
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

            // Add core services
            services.AddScoped(typeof(Repository<>), typeof(Repository<>));
            services.AddTransient<StringCipher>();

            // Stage 1: Get Data + Stage 2: Pivot Table + Stage 3: Standardize
            services.AddScoped<ITableCellContextService, TableCellContextService>();
            services.AddScoped<ITableReconstructionService, TableReconstructionService>();
            services.AddScoped<ITableParser, PkTableParser>();
            services.AddScoped<ITableParser, SimpleArmTableParser>();
            services.AddScoped<ITableParser, MultilevelAeTableParser>();
            services.AddScoped<ITableParser, AeWithSocTableParser>();
            services.AddScoped<ITableParser, EfficacyMultilevelTableParser>();
            services.AddScoped<ITableParserRouter, TableParserRouter>();

            // Stage 4: Validate (optional)
            if (includeValidation)
            {
                services.AddScoped<IRowValidationService, RowValidationService>();
                services.AddScoped<ITableValidationService, TableValidationService>();
                services.AddScoped<IBatchValidationService, BatchValidationService>();
            }

            // Stage 3.5: Claude Enhance (optional — graceful no-op if API key missing)
            services.Configure<ClaudeApiCorrectionSettings>(
                compositeConfiguration.GetSection("ClaudeApiCorrectionSettings"));

            if (disableClaude)
            {
                services.PostConfigure<ClaudeApiCorrectionSettings>(s => s.Enabled = false);
            }

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

            // Stage 3.25 sub-service: AE ParameterName → SOC dictionary resolver (stateless singleton)
            services.AddSingleton<IAeParameterCategoryDictionaryService, AeParameterCategoryDictionaryService>();

            // Stage 3.25: Column standardization (deterministic, pre-AI)
            services.AddScoped<IColumnStandardizationService, ColumnStandardizationService>();

            // Stage 3.4: QC correction (ML.NET-backed, always enabled — runs independently of Claude)
            services.Configure<QCNetCorrectionSettings>(
                compositeConfiguration.GetSection("QCNetCorrectionSettings"));
            services.AddScoped<IQCTrainingStore>(sp =>
                new QCTrainingStore(
                    sp.GetRequiredService<ILogger<QCTrainingStore>>(),
                    sp.GetRequiredService<IOptions<QCNetCorrectionSettings>>().Value));
            // Parse-quality gate (Phase 2 replacement for retired Stage 4 anomaly scoring).
            // The registry parses column-contracts.md into per-TableCategory R/E/O/N sets
            // and the service applies the deterministic QC_PARSE_QUALITY formula.
            services.AddSingleton<IColumnContractRegistry, ColumnContractRegistry>();
            services.AddSingleton<IParseQualityService, ParseQualityService>();

            services.AddScoped<IQCNetCorrectionService>(sp =>
                new QCNetCorrectionService(
                    sp.GetRequiredService<ILogger<QCNetCorrectionService>>(),
                    sp.GetRequiredService<IOptions<QCNetCorrectionSettings>>().Value,
                    trainingStore: sp.GetRequiredService<IQCTrainingStore>(),
                    parseQualityService: sp.GetRequiredService<IParseQualityService>(),
                    claudeSettings: sp.GetRequiredService<IOptions<ClaudeApiCorrectionSettings>>().Value));

            // Stage 0: Bioequivalent-ANDA label dedup (prunes the document set before Stage 1).
            // Registered unconditionally — callers can disable at call time via the
            // disableBioequivalentDedup parameter on Execute*/ProcessAll* methods.
            services.AddScoped<IBioequivalentLabelDedupService, BioequivalentLabelDedupService>();

            // Orchestrator — IBatchValidationService and IClaudeApiCorrectionService are optional (nullable constructor params).
            // Use an explicit factory so we can forward the Stage 3.25 quality gate flag
            // to the orchestrator's constructor. sp.GetService<T>() returns null for services
            // that were not registered (e.g. IBatchValidationService when includeValidation = false),
            // matching the orchestrator's nullable ctor-arg contract.
            services.AddScoped<ITableParsingOrchestrator>(sp => new TableParsingOrchestrator(
                sp.GetRequiredService<ITableReconstructionService>(),
                sp.GetRequiredService<ITableCellContextService>(),
                sp.GetRequiredService<ITableParserRouter>(),
                sp.GetRequiredService<ApplicationDbContext>(),
                sp.GetRequiredService<ILogger<TableParsingOrchestrator>>(),
                batchValidator: sp.GetService<IBatchValidationService>(),
                columnStandardizer: sp.GetService<IColumnStandardizationService>(),
                qcNetCorrectionService: sp.GetService<IQCNetCorrectionService>(),
                correctionService: sp.GetService<IClaudeApiCorrectionService>(),
                dropRowsMissingArmNOrPrimaryValue: dropRowsMissingArmNOrPrimaryValue,
                bioequivalentDedup: sp.GetService<IBioequivalentLabelDedupService>()));

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
        /// Displays the full Stage 4 validation report with tables, confidence distribution, and issues.
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

            // Confidence distribution (5-band)
            var confidence = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold]Confidence Distribution[/]")
                .AddColumn(new TableColumn("[bold]Level[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Parse[/]"))
                .AddColumn(new TableColumn("[bold]Parse %[/]"))
                .AddColumn(new TableColumn("[bold]Adjusted[/]"))
                .AddColumn(new TableColumn("[bold]Adj %[/]"));

            var total = Math.Max(report.TotalObservations, 1);
            confidence.AddRow("[green]Very High (≥ 0.95)[/]",
                $"{report.VeryHighConfidenceCount:N0}", $"{(double)report.VeryHighConfidenceCount / total:P1}",
                $"{report.AdjustedVeryHighCount:N0}", $"{(double)report.AdjustedVeryHighCount / total:P1}");
            confidence.AddRow("[green]High (0.80–0.95)[/]",
                $"{report.HighConfidenceCount:N0}", $"{(double)report.HighConfidenceCount / total:P1}",
                $"{report.AdjustedHighCount:N0}", $"{(double)report.AdjustedHighCount / total:P1}");
            confidence.AddRow("[yellow]Medium (0.60–0.80)[/]",
                $"{report.MediumConfidenceCount:N0}", $"{(double)report.MediumConfidenceCount / total:P1}",
                $"{report.AdjustedMediumCount:N0}", $"{(double)report.AdjustedMediumCount / total:P1}");
            confidence.AddRow("[darkorange]Low (0.40–0.60)[/]",
                $"{report.LowConfidenceCount:N0}", $"{(double)report.LowConfidenceCount / total:P1}",
                $"{report.AdjustedLowCount:N0}", $"{(double)report.AdjustedLowCount / total:P1}");
            confidence.AddRow("[red]Very Low (< 0.40)[/]",
                $"{report.VeryLowConfidenceCount:N0}", $"{(double)report.VeryLowConfidenceCount / total:P1}",
                $"{report.AdjustedVeryLowCount:N0}", $"{(double)report.AdjustedVeryLowCount / total:P1}");

            AnsiConsole.Write(confidence);
            AnsiConsole.MarkupLine($"[dim]Average Field Completeness: {report.AverageFieldCompleteness:P1}[/]");
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
        /// Displays the pivoted table structure from Stage 2: metadata, header, body rows, and footnotes.
        /// </summary>
        /// <param name="table">The reconstructed/pivoted table from Stage 2.</param>
        /// <seealso cref="ITableParsingOrchestrator.ReconstructSingleTableAsync"/>
        private static void displayReconstructedTable(ReconstructedTable table)
        {
            #region implementation

            // Metadata panel
            var metadata = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold]Table Metadata[/]")
                .AddColumn(new TableColumn("[bold]Property[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Value[/]"));

            metadata.AddRow("TextTableID", table.TextTableID?.ToString() ?? "-");
            metadata.AddRow("Caption", Markup.Escape(table.Caption ?? "(none)"));
            metadata.AddRow("ParentSectionCode", Markup.Escape(table.ParentSectionCode ?? "-"));
            metadata.AddRow("SectionTitle", Markup.Escape(table.SectionTitle ?? "-"));
            metadata.AddRow("Dimensions", $"{table.TotalColumnCount ?? 0} columns x {table.TotalRowCount ?? 0} rows");

            var flags = new List<string>();
            if (table.HasExplicitHeader == true) flags.Add("ExplicitHeader");
            if (table.HasInferredHeader == true) flags.Add("InferredHeader");
            if (table.HasSocDividers == true) flags.Add("SocDividers");
            if (table.HasFooter == true) flags.Add("Footer");
            metadata.AddRow("Flags", flags.Count > 0 ? string.Join(", ", flags) : "(none)");

            AnsiConsole.Write(metadata);
            AnsiConsole.WriteLine();

            // Render the table grid
            var columnCount = table.TotalColumnCount ?? 1;
            var grid = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold]Pivoted Table Data[/]");

            for (int c = 0; c < columnCount; c++)
            {
                var headerText = table.Header?.Columns?.ElementAtOrDefault(c)?.LeafHeaderText ?? $"Col {c}";
                grid.AddColumn(new TableColumn(Markup.Escape(headerText)));
            }

            // Body rows only (skip header/footer classifications)
            var dataRows = table.Rows?.Where(r =>
                r.Classification is RowClassification.DataBody or RowClassification.SocDivider) ?? Enumerable.Empty<ReconstructedRow>();

            foreach (var row in dataRows)
            {
                var cellTexts = new string[columnCount];
                for (int c = 0; c < columnCount; c++)
                    cellTexts[c] = "-";

                if (row.Cells != null)
                {
                    foreach (var cell in row.Cells)
                    {
                        var start = cell.ResolvedColumnStart ?? 0;
                        var end = cell.ResolvedColumnEnd ?? (start + 1);
                        var text = Markup.Escape(cell.CleanedText ?? "");

                        for (int c = start; c < end && c < columnCount; c++)
                        {
                            cellTexts[c] = c == start ? text : "↔";
                        }
                    }
                }

                if (row.Classification == RowClassification.SocDivider)
                {
                    cellTexts[0] = $"[bold yellow]{Markup.Escape(row.SocName ?? cellTexts[0])}[/]";
                    for (int c = 1; c < columnCount; c++)
                        cellTexts[c] = "";
                }

                grid.AddRow(cellTexts);
            }

            AnsiConsole.Write(grid);
            AnsiConsole.WriteLine();

            // Footnotes
            if (table.Footnotes != null && table.Footnotes.Count > 0)
            {
                AnsiConsole.MarkupLine("[bold]Footnotes:[/]");
                foreach (var fn in table.Footnotes)
                {
                    AnsiConsole.MarkupLine($"  [[{Markup.Escape(fn.Key)}]] {Markup.Escape(fn.Value)}");
                }
                AnsiConsole.WriteLine();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays the Stage 3 routing decision: category and selected parser.
        /// </summary>
        /// <param name="category">The table category determined by the router.</param>
        /// <param name="parserName">The selected parser name, or null if skipped.</param>
        /// <seealso cref="ITableParsingOrchestrator.RouteAndParseSingleTable"/>
        private static void displayRoutingResult(TableCategory category, string? parserName)
        {
            #region implementation

            var categoryColor = category == TableCategory.SKIP ? "yellow" : "green";
            AnsiConsole.MarkupLine($"  Category: [{categoryColor}]{category}[/]");
            AnsiConsole.MarkupLine($"  Parser:   {(parserName != null ? $"[green]{Markup.Escape(parserName)}[/]" : "[yellow]None (skipped)[/]")}");
            AnsiConsole.WriteLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays Stage 3.5 Claude Enhance corrections by comparing before/after ValidationFlags.
        /// Corrections append <c>AI_CORRECTED:*</c> entries to <see cref="ParsedObservation.ValidationFlags"/>.
        /// </summary>
        /// <param name="observations">The corrected observations.</param>
        /// <param name="beforeFlags">Snapshot of ValidationFlags keyed by (RowSeq*10000 + CellSeq) before correction.</param>
        /// <seealso cref="ITableParsingOrchestrator.CorrectObservationsAsync"/>
        private static void displayClaudeCorrections(List<ParsedObservation> observations, Dictionary<int, string?> beforeFlags)
        {
            #region implementation

            var corrections = new List<(int row, int cell, string flag)>();

            foreach (var obs in observations)
            {
                var key = (obs.SourceRowSeq ?? 0) * 10000 + (obs.SourceCellSeq ?? 0);
                var before = beforeFlags.GetValueOrDefault(key);
                var after = obs.ValidationFlags;

                if (after != null && after != before)
                {
                    // Extract new AI_CORRECTED entries
                    var newFlags = after
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Where(f => f.Trim().StartsWith("AI_CORRECTED:"))
                        .Where(f => before == null || !before.Contains(f.Trim()));

                    foreach (var flag in newFlags)
                    {
                        corrections.Add((obs.SourceRowSeq ?? 0, obs.SourceCellSeq ?? 0, flag.Trim()));
                    }
                }
            }

            if (corrections.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]No corrections applied by Claude.[/]");
                AnsiConsole.WriteLine();
                return;
            }

            var corrTable = new Table()
                .Border(TableBorder.Rounded)
                .Title($"[bold]{corrections.Count} Correction(s) Applied[/]")
                .AddColumn(new TableColumn("[bold]Row[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Cell[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Correction[/]"));

            foreach (var (row, cell, flag) in corrections)
            {
                corrTable.AddRow(row.ToString(), cell.ToString(), Markup.Escape(flag));
            }

            AnsiConsole.Write(corrTable);
            AnsiConsole.WriteLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays stage-level detail for a completed batch based on the selected detail level.
        /// In <see cref="StageDetailLevel.Full"/> mode, also renders the pivoted table for each
        /// non-skipped table using <see cref="displayReconstructedTable"/>.
        /// </summary>
        /// <param name="result">Batch stage result with intermediate data.</param>
        /// <param name="batchNumber">Current batch number (1-based).</param>
        /// <param name="rangeStart">First TextTableID in this batch.</param>
        /// <param name="rangeEnd">Last TextTableID in this batch.</param>
        /// <param name="detailLevel">Level of detail to display.</param>
        /// <seealso cref="BatchStageResult"/>
        /// <seealso cref="StageDetailLevel"/>
        private static void displayBatchStageDetail(
            BatchStageResult result, int batchNumber, int rangeStart, int rangeEnd, StageDetailLevel detailLevel)
        {
            #region implementation

            if (detailLevel == StageDetailLevel.Concise)
            {
                // One summary line with category breakdown
                var categoryGroups = result.RoutingDecisions
                    .Where(d => d.Category != TableCategory.SKIP)
                    .GroupBy(d => d.Category)
                    .Select(g => $"{g.Key}:{g.Count()}")
                    .ToList();

                var categoryBreakdown = categoryGroups.Count > 0
                    ? string.Join(", ", categoryGroups)
                    : "none";

                AnsiConsole.MarkupLine(
                    $"[grey]Batch {batchNumber} [[{rangeStart}-{rangeEnd}]][/] " +
                    $"[white]{result.ReconstructedTables.Count}[/] tables, " +
                    $"parsed ({categoryBreakdown}), " +
                    $"[yellow]{result.SkipReasons.Count}[/] skipped, " +
                    $"[green]{result.ObservationsWritten}[/] obs" +
                    (result.CorrectionCount > 0 ? $", [blue]{result.CorrectionCount}[/] AI corrections" : ""));
            }
            else if (detailLevel == StageDetailLevel.Full)
            {
                AnsiConsole.Write(new Rule($"[bold]Batch {batchNumber} [[{rangeStart}-{rangeEnd}]][/]").RuleStyle("grey"));
                AnsiConsole.WriteLine();

                // Per-table routing table
                var routingTable = new Table()
                    .Border(TableBorder.Simple)
                    .AddColumn(new TableColumn("[bold]TableID[/]").NoWrap())
                    .AddColumn(new TableColumn("[bold]Category[/]"))
                    .AddColumn(new TableColumn("[bold]Parser[/]"))
                    .AddColumn(new TableColumn("[bold]Obs[/]"));

                foreach (var decision in result.RoutingDecisions)
                {
                    var categoryColor = decision.Category == TableCategory.SKIP ? "yellow" : "green";
                    routingTable.AddRow(
                        decision.TextTableID.ToString(),
                        $"[{categoryColor}]{decision.Category}[/]",
                        Markup.Escape(decision.ParserName ?? "-"),
                        decision.ObservationCount.ToString());
                }

                AnsiConsole.Write(routingTable);
                AnsiConsole.WriteLine();

                // Display pivoted table data for each non-skipped table
                var skippedIds = new HashSet<int>(result.SkipReasons.Keys);
                foreach (var table in result.ReconstructedTables)
                {
                    if (table.TextTableID.HasValue && skippedIds.Contains(table.TextTableID.Value))
                        continue;

                    AnsiConsole.Write(new Rule($"[blue]Stage 2: Pivoted Table — TextTableID={table.TextTableID}[/]").RuleStyle("grey"));
                    AnsiConsole.WriteLine();
                    displayReconstructedTable(table);
                }

                // Summary line
                AnsiConsole.MarkupLine(
                    $"  Written: [green]{result.ObservationsWritten}[/] | " +
                    $"Skipped: [yellow]{result.SkipReasons.Count}[/]" +
                    (result.CorrectionCount > 0 ? $" | AI Corrections: [blue]{result.CorrectionCount}[/]" : ""));
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

        #region private methods — markdown report

        /**************************************************************/
        /// <summary>
        /// Builds a <see cref="TableReportEntry"/> from the pieces captured during a
        /// single-table parse. Extracted for direct unit test coverage.
        /// </summary>
        /// <param name="table">Stage 2 reconstructed table (required).</param>
        /// <param name="category">Routing category from Stage 3.</param>
        /// <param name="parserName">Selected parser name (null when table was skipped).</param>
        /// <param name="observations">Stage 3 observations, possibly already Claude-corrected.</param>
        /// <param name="beforeClaudeFlags">Snapshot of ValidationFlags before Stage 3.5, or null when
        /// Claude was skipped or observations were empty.</param>
        /// <param name="claudeSkipped">True when Stage 3.5 did not run (disabled, --no-claude, or empty obs).</param>
        /// <seealso cref="TableReportEntry"/>
        public static TableReportEntry BuildReportEntry(
            ReconstructedTable table,
            TableCategory category,
            string? parserName,
            IReadOnlyList<ParsedObservation> observations,
            IReadOnlyDictionary<int, string?>? beforeClaudeFlags,
            bool claudeSkipped)
        {
            #region implementation

            return new TableReportEntry
            {
                Table = table,
                Category = category,
                ParserName = parserName,
                Observations = observations,
                BeforeClaudeFlags = claudeSkipped ? null : beforeClaudeFlags,
                ClaudeSkipped = claudeSkipped
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// For each non-skipped table in a batch stage result, builds a
        /// <see cref="TableReportEntry"/> (grouping observations by <see cref="ParsedObservation.TextTableID"/>)
        /// and enqueues it on the sink. Skipped tables are still emitted so the diagnostic log
        /// captures routing decisions for every input table.
        /// </summary>
        /// <param name="markdownSink">Target markdown sink, or null when markdown output is disabled.</param>
        /// <param name="jsonSink">Target JSON (NDJSON) sink, or null when JSON output is disabled.</param>
        /// <param name="stageResult">Batch result carrying per-table intermediate data.</param>
        /// <param name="claudeDisabled">True when Stage 3.5 was suppressed for this run.</param>
        /// <param name="ct">Cancellation token propagated to each sink's enqueue.</param>
        /// <seealso cref="BatchStageResult"/>
        private static async Task appendBatchToReportAsync(
            MarkdownReportSink? markdownSink,
            JsonReportSink? jsonSink,
            BatchStageResult stageResult,
            bool claudeDisabled,
            CancellationToken ct)
        {
            #region implementation

            // Nothing to do when both sinks are disabled — the caller is expected to gate
            // this call, but a redundant check keeps the method safe under future refactors.
            if (markdownSink == null && jsonSink == null)
                return;

            // Group observations by TextTableID so we can match them back to their source table.
            // Use dictionary lookups rather than repeated LINQ filters for O(tables + obs) total work.
            var preByTable = stageResult.PreCorrectionObservations
                .Where(o => o.TextTableID.HasValue)
                .GroupBy(o => o.TextTableID!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var postByTable = stageResult.PostCorrectionObservations
                .Where(o => o.TextTableID.HasValue)
                .GroupBy(o => o.TextTableID!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var routingByTable = stageResult.RoutingDecisions
                .Where(d => d.TextTableID > 0)
                .ToDictionary(d => d.TextTableID, d => d);

            foreach (var table in stageResult.ReconstructedTables)
            {
                if (!table.TextTableID.HasValue)
                    continue;

                var tableId = table.TextTableID.Value;

                routingByTable.TryGetValue(tableId, out var routing);
                var category = routing?.Category ?? TableCategory.SKIP;
                var parserName = routing?.ParserName;

                postByTable.TryGetValue(tableId, out var postObs);
                postObs ??= new List<ParsedObservation>();

                // Reconstruct the before-Stage-3.5 ValidationFlags snapshot from the pre-correction
                // observations so the markdown/JSON diff matches what displayClaudeCorrections would show.
                IReadOnlyDictionary<int, string?>? beforeFlags = null;
                if (!claudeDisabled && preByTable.TryGetValue(tableId, out var preObs))
                {
                    beforeFlags = preObs.ToDictionary(
                        o => (o.SourceRowSeq ?? 0) * 10000 + (o.SourceCellSeq ?? 0),
                        o => o.ValidationFlags);
                }

                var entry = BuildReportEntry(
                    table, category, parserName, postObs, beforeFlags, claudeSkipped: claudeDisabled);

                if (markdownSink != null)
                    await markdownSink.AppendAsync(entry, ct);
                if (jsonSink != null)
                    await jsonSink.AppendAsync(entry, ct);
            }

            #endregion
        }

        #endregion
    }
}
