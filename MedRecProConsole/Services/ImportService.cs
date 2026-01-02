using MedRecProImportClass.Data;
using MedRecProImportClass.DataAccess;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service;
using MedRecProConsole.Helpers;
using MedRecProConsole.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Diagnostics;

namespace MedRecProConsole.Services
{
    /**************************************************************/
    /// <summary>
    /// Service responsible for orchestrating the bulk import of SPL ZIP files.
    /// Manages dependency injection setup, ZIP file processing, and result aggregation.
    /// </summary>
    /// <remarks>
    /// This service acts as the bridge between the console application and the MedRecPro
    /// import infrastructure. It sets up the required DI container and delegates actual
    /// import work to the SplImportService from MedRecPro.
    ///
    /// Uses an Orchestrator pattern where <see cref="ExecuteImportAsync(ImportParameters, ImportProgressTracker?, bool)"/>
    /// coordinates initialization, file processing, and finalization through delegated private methods.
    /// </remarks>
    /// <seealso cref="SplImportService"/>
    /// <seealso cref="ImportParameters"/>
    /// <seealso cref="ImportResults"/>
    public class ImportService
    {
        #region internal types

        /**************************************************************/
        /// <summary>
        /// Internal context class that holds state throughout the import operation.
        /// Encapsulates all state variables to avoid excessive parameter passing between orchestrated methods.
        /// </summary>
        /// <remarks>
        /// This context object is created at the start of each import operation and passed
        /// to the various orchestration methods, providing a clean way to share state without
        /// using instance fields that could cause concurrency issues.
        /// </remarks>
        /// <seealso cref="ImportParameters"/>
        /// <seealso cref="ImportResults"/>
        /// <seealso cref="ImportProgressTracker"/>
        private class ImportContext
        {
            #region implementation

            /**************************************************************/
            /// <summary>
            /// Gets or sets the import parameters containing configuration.
            /// </summary>
            public required ImportParameters Parameters { get; init; }

            /**************************************************************/
            /// <summary>
            /// Gets or sets the optional progress tracker for crash recovery.
            /// </summary>
            public ImportProgressTracker? ProgressTracker { get; init; }

            /**************************************************************/
            /// <summary>
            /// Gets or sets whether this is a resume operation.
            /// </summary>
            public bool IsResuming { get; init; }

            /**************************************************************/
            /// <summary>
            /// Gets or sets the stopwatch for tracking overall elapsed time.
            /// </summary>
            public Stopwatch Stopwatch { get; } = Stopwatch.StartNew();

            /**************************************************************/
            /// <summary>
            /// Gets or sets the cancellation token source for timeout handling.
            /// </summary>
            public CancellationTokenSource CancellationTokenSource { get; } = new();

            /**************************************************************/
            /// <summary>
            /// Gets or sets whether to stop after the current file completes.
            /// </summary>
            public bool ShouldStopAfterCurrentFile { get; set; }

            /**************************************************************/
            /// <summary>
            /// Gets or sets the reason for interruption if the import was stopped early.
            /// </summary>
            public string InterruptionReason { get; set; } = string.Empty;

            /**************************************************************/
            /// <summary>
            /// Gets or sets the original console output writer for restoration.
            /// </summary>
            public TextWriter? OriginalConsoleOut { get; set; }

            /**************************************************************/
            /// <summary>
            /// Gets or sets the service provider for dependency injection.
            /// </summary>
            public ServiceProvider? ServiceProvider { get; set; }

            /**************************************************************/
            /// <summary>
            /// Gets or sets the aggregated import results.
            /// </summary>
            public ImportResults Results { get; } = new();

            /**************************************************************/
            /// <summary>
            /// Gets or sets the cumulative processing time across all files.
            /// </summary>
            public TimeSpan CumulativeProcessingTime { get; set; } = TimeSpan.Zero;

            #endregion
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Executes the bulk import operation with progress tracking and error handling.
        /// </summary>
        /// <param name="parameters">Import parameters containing connection string, folder path, and options</param>
        /// <returns>ImportResults containing statistics and error information</returns>
        /// <remarks>
        /// Sets up dependency injection for MedRecPro services, processes each ZIP file,
        /// and reports progress using Spectre.Console. Failed ZIP imports are logged but
        /// do not stop the overall import process.
        /// This overload does not use crash recovery - use the overload with progressTracker
        /// for resume capability.
        /// </remarks>
        /// <seealso cref="SplImportService"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="ImportParameters"/>
        public async Task<ImportResults> ExecuteImportAsync(ImportParameters parameters)
        {
            // Delegate to the overload without a progress tracker
            return await ExecuteImportAsync(parameters, null, false);
        }

        /**************************************************************/
        /// <summary>
        /// Executes the bulk import operation with progress tracking, crash recovery, and error handling.
        /// </summary>
        /// <param name="parameters">Import parameters containing connection string, folder path, and options</param>
        /// <param name="progressTracker">Progress tracker for crash recovery and resume capability</param>
        /// <param name="isResuming">True if resuming from an existing queue, false for new import</param>
        /// <returns>ImportResults containing statistics and error information</returns>
        /// <remarks>
        /// Orchestrates the import operation through the following phases:
        /// <list type="number">
        /// <item><description>Initialize context and set up dependencies via <see cref="initializeContextAsync"/></description></item>
        /// <item><description>Display import header via <see cref="displayImportHeader"/></description></item>
        /// <item><description>Process files with progress tracking via <see cref="orchestrateFileProcessingAsync"/></description></item>
        /// <item><description>Finalize and clean up via <see cref="finalizeImportAsync"/></description></item>
        /// </list>
        ///
        /// When a progressTracker is provided:
        /// - Creates a queue file at the import folder root for crash recovery
        /// - Updates file status (queued/in-progress/completed/failed) after each operation
        /// - Completes the current file before exiting on timer expiration
        /// - Respects nested queue files found in subdirectories
        /// </remarks>
        /// <seealso cref="SplImportService"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="ImportParameters"/>
        /// <seealso cref="ImportProgressTracker"/>
        public async Task<ImportResults> ExecuteImportAsync(
            ImportParameters parameters,
            ImportProgressTracker? progressTracker,
            bool isResuming)
        {
            #region implementation

            // Create context to hold all state for this import operation
            var context = new ImportContext
            {
                Parameters = parameters,
                ProgressTracker = progressTracker,
                IsResuming = isResuming
            };

            try
            {
                // Phase 1: Initialize context, service provider, and progress tracker
                await initializeContextAsync(context);

                // Phase 2: Display the import header banner
                displayImportHeader(context);

                // Phase 3: Process all ZIP files with progress tracking
                await orchestrateFileProcessingAsync(context);

                // Phase 4: Finalize results and clean up
                return await finalizeImportAsync(context);
            }
            finally
            {
                // Ensure console output is always restored
                restoreConsoleOutput(context);

                // Dispose the service provider if it was created
                if (context.ServiceProvider != null)
                {
                    await context.ServiceProvider.DisposeAsync();
                }
            }

            #endregion
        }

        #endregion

        #region private methods - orchestration

        /**************************************************************/
        /// <summary>
        /// Initializes the import context with service provider, timeout, and progress tracker.
        /// </summary>
        /// <param name="context">The import context to initialize</param>
        /// <returns>Task representing the asynchronous operation</returns>
        /// <remarks>
        /// Performs the following initialization steps:
        /// <list type="bullet">
        /// <item><description>Sets up cancellation timeout if max runtime is specified</description></item>
        /// <item><description>Redirects console output for non-verbose mode</description></item>
        /// <item><description>Builds the DI service provider</description></item>
        /// <item><description>Initializes the progress tracker for new imports</description></item>
        /// </list>
        /// </remarks>
        /// <seealso cref="ImportContext"/>
        /// <seealso cref="buildServiceProvider"/>
        /// <seealso cref="ImportProgressTracker.LoadOrCreateQueueAsync"/>
        private async Task initializeContextAsync(ImportContext context)
        {
            #region implementation

            // Set up timeout if max runtime specified
            if (context.Parameters.MaxRuntimeMinutes.HasValue)
            {
                context.CancellationTokenSource.CancelAfter(
                    TimeSpan.FromMinutes(context.Parameters.MaxRuntimeMinutes.Value));
            }

            // Build configuration with required settings for MedRecPro services
            var configuration = ConfigurationHelper.BuildMedRecProConfiguration();

            // Redirect console output if not in verbose mode to suppress debug messages
            suppressConsoleOutput(context);

            // Build service provider with all required MedRecPro dependencies
            context.ServiceProvider = buildServiceProvider(
                context.Parameters.ConnectionString,
                configuration,
                context.Parameters.VerboseMode);

            // Initialize progress tracker if provided and not resuming
            if (context.ProgressTracker != null && !context.IsResuming)
            {
                await context.ProgressTracker.LoadOrCreateQueueAsync(
                    context.Parameters.ImportFolder,
                    context.Parameters.ConnectionString,
                    context.Parameters.ZipFiles,
                    context.Parameters.MaxRuntimeMinutes,
                    context.Parameters.VerboseMode);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays the import header banner indicating start or resume.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <remarks>
        /// Restores console output temporarily to display the Spectre.Console rule,
        /// then re-suppresses output for the import processing phase.
        /// </remarks>
        /// <seealso cref="ImportContext"/>
        private void displayImportHeader(ImportContext context)
        {
            #region implementation

            // Restore console output for Spectre.Console UI
            restoreConsoleOutput(context);

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule(context.IsResuming
                ? "[bold yellow]Resuming Import[/]"
                : "[bold green]Starting Import[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();

            // Re-redirect console output during import processing
            suppressConsoleOutput(context);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Orchestrates the processing of all ZIP files with progress tracking.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <returns>Task representing the asynchronous operation</returns>
        /// <remarks>
        /// Uses Spectre.Console Progress to display a live progress bar while processing
        /// each ZIP file. Handles timeout gracefully by completing the current file before stopping.
        /// </remarks>
        /// <seealso cref="ImportContext"/>
        /// <seealso cref="processSingleZipFileAsync"/>
        private async Task orchestrateFileProcessingAsync(ImportContext context)
        {
            #region implementation

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
                    // Create overall progress task - timer starts from task creation
                    var overallTask = ctx.AddTask("[green]Overall Progress[/]",
                        maxValue: context.Parameters.ZipFiles.Count);

                    // Process each ZIP file
                    foreach (var zipFilePath in context.Parameters.ZipFiles)
                    {
                        // Check if we should stop after completing the previous file
                        if (context.ShouldStopAfterCurrentFile)
                        {
                            break;
                        }

                        // Check for cancellation (timeout) BEFORE starting a new file
                        if (checkForCancellation(context))
                        {
                            break;
                        }

                        // Process this ZIP file with all tracking and error handling
                        await processSingleZipFileAsync(context, ctx, overallTask, zipFilePath);
                    }

                    // Final update to show total wall-clock time
                    updateOverallProgress(overallTask, context.Stopwatch.Elapsed, isFinal: true);

                    // Small delay to ensure the final description is rendered
                    await Task.Delay(100);
                });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes a single ZIP file with full tracking, error handling, and progress updates.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <param name="progressContext">The Spectre.Console progress context</param>
        /// <param name="overallTask">The overall progress task</param>
        /// <param name="zipFilePath">Path to the ZIP file to process</param>
        /// <returns>Task representing the asynchronous operation</returns>
        /// <remarks>
        /// Handles the complete lifecycle of processing a single ZIP file:
        /// <list type="bullet">
        /// <item><description>Checks for nested queue skip conditions</description></item>
        /// <item><description>Marks file as in-progress</description></item>
        /// <item><description>Processes the file and tracks timing</description></item>
        /// <item><description>Handles success, failure, and exception cases</description></item>
        /// <item><description>Updates progress tracking</description></item>
        /// </list>
        /// </remarks>
        /// <seealso cref="ImportContext"/>
        /// <seealso cref="handleSuccessfulImport"/>
        /// <seealso cref="handleFailedImport"/>
        private async Task processSingleZipFileAsync(
            ImportContext context,
            ProgressContext progressContext,
            ProgressTask overallTask,
            string zipFilePath)
        {
            #region implementation

            var zipFileName = Path.GetFileName(zipFilePath);
            var displayName = truncateDisplayName(zipFileName);
            var zipTask = progressContext.AddTask($"[blue]{displayName}[/]", maxValue: 100);

            // Start timing this file's processing
            var fileStopwatch = Stopwatch.StartNew();

            try
            {
                // Check if file should be skipped due to nested queue
                if (await checkAndHandleNestedQueueSkip(context, zipFilePath, zipTask, overallTask, displayName))
                {
                    return;
                }

                // Mark as in-progress in the queue file
                await markFileInProgress(context, zipFilePath);

                // Process the file - use CancellationToken.None for graceful shutdown
                var zipResult = await processZipFileAsync(
                    context.ServiceProvider!,
                    zipFilePath,
                    progress => zipTask.Value = progress,
                    CancellationToken.None);

                // Stop timing and accumulate
                fileStopwatch.Stop();
                context.CumulativeProcessingTime = context.CumulativeProcessingTime.Add(fileStopwatch.Elapsed);

                // Handle results based on success or failure
                context.Results.TotalZipsProcessed++;

                if (zipResult.OverallSuccess)
                {
                    await handleSuccessfulImport(context, zipFilePath, zipFileName, displayName, zipTask, zipResult);
                }
                else
                {
                    await handleFailedImport(context, zipFilePath, zipFileName, displayName, zipTask, zipResult);
                }

                // Aggregate statistics
                aggregateStatistics(context.Results, zipResult);
                zipTask.Value = 100;

                // Stop the per-file timer to freeze the elapsed time display
                zipTask.StopTask();
            }
            catch (OperationCanceledException)
            {
                // Accumulate timing even for cancelled operations
                fileStopwatch.Stop();
                context.CumulativeProcessingTime = context.CumulativeProcessingTime.Add(fileStopwatch.Elapsed);

                await handleCancelledImport(context, zipFilePath, zipFileName, displayName, zipTask);

                // Stop the per-file timer to freeze the elapsed time display
                zipTask.StopTask();
            }
            catch (Exception ex)
            {
                // Accumulate timing even for failed operations
                fileStopwatch.Stop();
                context.CumulativeProcessingTime = context.CumulativeProcessingTime.Add(fileStopwatch.Elapsed);

                await handleExceptionImport(context, zipFilePath, zipFileName, displayName, zipTask, ex);

                // Stop the per-file timer to freeze the elapsed time display
                zipTask.StopTask();
            }

            // Increment overall progress and update description with wall-clock time
            overallTask.Increment(1);
            updateOverallProgress(overallTask, context.Stopwatch.Elapsed, isFinal: false);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finalizes the import operation by recording results and cleaning up.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <returns>The final import results</returns>
        /// <remarks>
        /// Performs the following finalization steps:
        /// <list type="bullet">
        /// <item><description>Restores console output</description></item>
        /// <item><description>Records elapsed time</description></item>
        /// <item><description>Records interruption if applicable</description></item>
        /// <item><description>Flushes progress tracker state</description></item>
        /// </list>
        /// </remarks>
        /// <seealso cref="ImportContext"/>
        /// <seealso cref="ImportResults"/>
        private async Task<ImportResults> finalizeImportAsync(ImportContext context)
        {
            #region implementation

            // Restore console output after import processing
            restoreConsoleOutput(context);

            // Record elapsed time
            context.Stopwatch.Stop();
            context.Results.ElapsedTime = context.Stopwatch.Elapsed;

            // Record interruption if we stopped early
            if (context.ProgressTracker != null && !string.IsNullOrEmpty(context.InterruptionReason))
            {
                await context.ProgressTracker.RecordInterruptionAsync(
                    context.InterruptionReason,
                    context.Stopwatch.Elapsed);
            }

            // Ensure final progress state is saved
            if (context.ProgressTracker != null)
            {
                await context.ProgressTracker.FlushAsync();
            }

            return context.Results;

            #endregion
        }

        #endregion

        #region private methods - import result handlers

        /**************************************************************/
        /// <summary>
        /// Handles a successful ZIP import by updating results and progress tracking.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <param name="zipFilePath">Full path to the ZIP file</param>
        /// <param name="zipFileName">Name of the ZIP file</param>
        /// <param name="displayName">Truncated display name for console</param>
        /// <param name="zipTask">The progress task for this ZIP</param>
        /// <param name="zipResult">The import result</param>
        /// <returns>Task representing the asynchronous operation</returns>
        /// <seealso cref="ImportContext"/>
        /// <seealso cref="SplZipImportResult"/>
        private async Task handleSuccessfulImport(
            ImportContext context,
            string zipFilePath,
            string zipFileName,
            string displayName,
            ProgressTask zipTask,
            SplZipImportResult zipResult)
        {
            #region implementation

            context.Results.SuccessfulZips++;
            zipTask.Description = $"[green]{displayName} - Success ({zipResult.TotalFilesSucceeded} files)[/]";

            // Mark as completed in the queue file with statistics
            if (context.ProgressTracker != null)
            {
                var stats = aggregateZipStatistics(zipResult);
                await context.ProgressTracker.MarkItemCompletedAsync(
                    zipFilePath,
                    stats.documents,
                    stats.organizations,
                    stats.products,
                    stats.sections,
                    stats.ingredients);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles a failed ZIP import by recording errors and updating progress tracking.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <param name="zipFilePath">Full path to the ZIP file</param>
        /// <param name="zipFileName">Name of the ZIP file</param>
        /// <param name="displayName">Truncated display name for console</param>
        /// <param name="zipTask">The progress task for this ZIP</param>
        /// <param name="zipResult">The import result</param>
        /// <returns>Task representing the asynchronous operation</returns>
        /// <seealso cref="ImportContext"/>
        /// <seealso cref="SplZipImportResult"/>
        private async Task handleFailedImport(
            ImportContext context,
            string zipFilePath,
            string zipFileName,
            string displayName,
            ProgressTask zipTask,
            SplZipImportResult zipResult)
        {
            #region implementation

            context.Results.FailedZips++;
            context.Results.FailedZipNames.Add(zipFileName);

            // Collect error messages from failed file results
            var errorMessages = new List<string>();
            foreach (var fileResult in zipResult.FileResults.Where(f => !f.Success))
            {
                var errorMsg = $"{zipFileName}/{fileResult.FileName}: {fileResult.Message}";
                context.Results.Errors.Add(errorMsg);
                errorMessages.Add(fileResult.Message ?? "Unknown error");
            }

            // Mark as failed in the queue file
            if (context.ProgressTracker != null)
            {
                await context.ProgressTracker.MarkItemFailedAsync(
                    zipFilePath,
                    string.Join("; ", errorMessages.Take(3)));
            }

            zipTask.Description = $"[red]{displayName} - Failed ({zipResult.TotalFilesSucceeded}/{zipResult.TotalFilesProcessed} succeeded)[/]";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles a cancelled ZIP import by recording the cancellation.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <param name="zipFilePath">Full path to the ZIP file</param>
        /// <param name="zipFileName">Name of the ZIP file</param>
        /// <param name="displayName">Truncated display name for console</param>
        /// <param name="zipTask">The progress task for this ZIP</param>
        /// <returns>Task representing the asynchronous operation</returns>
        /// <remarks>
        /// This shouldn't normally happen since we pass CancellationToken.None to processZipFileAsync,
        /// but handles the case gracefully if it occurs.
        /// </remarks>
        /// <seealso cref="ImportContext"/>
        private async Task handleCancelledImport(
            ImportContext context,
            string zipFilePath,
            string zipFileName,
            string displayName,
            ProgressTask zipTask)
        {
            #region implementation

            context.Results.FailedZips++;
            context.Results.FailedZipNames.Add(zipFileName);
            context.Results.Errors.Add($"{zipFileName}: Import cancelled");
            zipTask.Description = $"[yellow]{displayName} - Cancelled[/]";

            if (context.ProgressTracker != null)
            {
                await context.ProgressTracker.MarkItemFailedAsync(zipFilePath, "Import cancelled");
            }

            context.ShouldStopAfterCurrentFile = true;
            context.InterruptionReason = "Operation cancelled";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles an exception during ZIP import by recording the error.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <param name="zipFilePath">Full path to the ZIP file</param>
        /// <param name="zipFileName">Name of the ZIP file</param>
        /// <param name="displayName">Truncated display name for console</param>
        /// <param name="zipTask">The progress task for this ZIP</param>
        /// <param name="ex">The exception that occurred</param>
        /// <returns>Task representing the asynchronous operation</returns>
        /// <seealso cref="ImportContext"/>
        private async Task handleExceptionImport(
            ImportContext context,
            string zipFilePath,
            string zipFileName,
            string displayName,
            ProgressTask zipTask,
            Exception ex)
        {
            #region implementation

            context.Results.FailedZips++;
            context.Results.FailedZipNames.Add(zipFileName);
            context.Results.Errors.Add($"{zipFileName}: {ex.Message}");
            zipTask.Description = $"[red]{displayName} - Error[/]";

            // Mark as failed in the queue file
            if (context.ProgressTracker != null)
            {
                await context.ProgressTracker.MarkItemFailedAsync(zipFilePath, ex.Message);
            }

            #endregion
        }

        #endregion

        #region private methods - utility

        /**************************************************************/
        /// <summary>
        /// Suppresses console output if not in verbose mode.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <remarks>
        /// Stores the original console output writer in the context for later restoration.
        /// </remarks>
        /// <seealso cref="ImportContext"/>
        /// <seealso cref="restoreConsoleOutput"/>
        private void suppressConsoleOutput(ImportContext context)
        {
            #region implementation

            if (!context.Parameters.VerboseMode && context.OriginalConsoleOut == null)
            {
                context.OriginalConsoleOut = Console.Out;
                Console.SetOut(TextWriter.Null);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Restores console output to the original writer.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <seealso cref="ImportContext"/>
        /// <seealso cref="suppressConsoleOutput"/>
        private void restoreConsoleOutput(ImportContext context)
        {
            #region implementation

            if (context.OriginalConsoleOut != null)
            {
                Console.SetOut(context.OriginalConsoleOut);
                context.OriginalConsoleOut = null;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks for cancellation and updates context state if timeout reached.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <returns>True if processing should stop, false otherwise</returns>
        /// <seealso cref="ImportContext"/>
        private bool checkForCancellation(ImportContext context)
        {
            #region implementation

            if (context.CancellationTokenSource.Token.IsCancellationRequested)
            {
                context.ShouldStopAfterCurrentFile = true;
                context.InterruptionReason = "Maximum runtime reached";

                // Temporarily restore console to show the message
                restoreConsoleOutput(context);
                AnsiConsole.MarkupLine("[yellow]Maximum runtime reached. Completing current file then stopping...[/]");
                suppressConsoleOutput(context);

                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Truncates the display name to fit within console width.
        /// </summary>
        /// <param name="zipFileName">The full ZIP file name</param>
        /// <returns>Truncated display name with ellipsis if needed</returns>
        private string truncateDisplayName(string zipFileName)
        {
            #region implementation

            var maxNameLength = Math.Max(30, AnsiConsole.Profile.Width / 3);
            return zipFileName.Length > maxNameLength
                ? zipFileName[..(maxNameLength - 3)] + "..."
                : zipFileName;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if a file should be skipped due to nested queue and handles the skip.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <param name="zipFilePath">Full path to the ZIP file</param>
        /// <param name="zipTask">The progress task for this ZIP</param>
        /// <param name="overallTask">The overall progress task</param>
        /// <param name="displayName">Truncated display name for console</param>
        /// <returns>True if the file was skipped, false otherwise</returns>
        /// <seealso cref="ImportProgressTracker.IsFileCompletedInNestedQueueAsync"/>
        private async Task<bool> checkAndHandleNestedQueueSkip(
            ImportContext context,
            string zipFilePath,
            ProgressTask zipTask,
            ProgressTask overallTask,
            string displayName)
        {
            #region implementation

            if (context.ProgressTracker != null &&
                await context.ProgressTracker.IsFileCompletedInNestedQueueAsync(zipFilePath))
            {
                await context.ProgressTracker.MarkItemSkippedAsync(zipFilePath, "Completed in nested queue");
                zipTask.Description = $"[grey]{displayName} - Skipped (nested queue)[/]";
                zipTask.Value = 100;
                overallTask.Increment(1);
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Marks a file as in-progress in the progress tracker if available.
        /// </summary>
        /// <param name="context">The import context</param>
        /// <param name="zipFilePath">Full path to the ZIP file</param>
        /// <returns>Task representing the asynchronous operation</returns>
        /// <seealso cref="ImportProgressTracker.MarkItemInProgressAsync"/>
        private async Task markFileInProgress(ImportContext context, string zipFilePath)
        {
            #region implementation

            if (context.ProgressTracker != null)
            {
                await context.ProgressTracker.MarkItemInProgressAsync(zipFilePath);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Updates the overall progress task description with wall-clock elapsed time.
        /// </summary>
        /// <param name="overallTask">The overall progress task</param>
        /// <param name="elapsedTime">The wall-clock elapsed time from import start</param>
        /// <param name="isFinal">Whether this is the final update</param>
        /// <remarks>
        /// Uses wall-clock time (from context.Stopwatch) rather than cumulative processing time
        /// to accurately represent the total duration of the import operation.
        /// </remarks>
        private void updateOverallProgress(ProgressTask overallTask, TimeSpan elapsedTime, bool isFinal)
        {
            #region implementation

            overallTask.Description = $"[green]Overall Progress[/] [blue]Total: {elapsedTime.Hours:D2}:{elapsedTime.Minutes:D2}:{elapsedTime.Seconds:D2}[/]";

            if (isFinal)
            {
                overallTask.Value = overallTask.MaxValue;
            }

            #endregion
        }

        #endregion

        #region private methods - service configuration

        /**************************************************************/
        /// <summary>
        /// Builds the dependency injection service provider with all required MedRecPro services.
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        /// <param name="configuration">Application configuration</param>
        /// <param name="verboseMode">Whether verbose logging is enabled</param>
        /// <returns>ServiceProvider with configured services</returns>
        /// <remarks>
        /// Registers ApplicationDbContext, Repository, SplImportService, SplDataService,
        /// SplXmlParser, and all parsing services required for SPL import operations.
        /// When verboseMode is false, logging is suppressed to minimize console noise.
        /// </remarks>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="SplImportService"/>
        /// <seealso cref="SplXmlParser"/>
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

            // Add MedRecPro services
            services.AddScoped(typeof(Repository<>), typeof(Repository<>));
            services.AddTransient<StringCipher>();
            services.AddScoped<SplDataService>();
            services.AddScoped<SplXmlParser>();
            services.AddScoped<SplImportService>();

            return services.BuildServiceProvider();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes a single ZIP file using the SplImportService.
        /// </summary>
        /// <param name="serviceProvider">DI service provider</param>
        /// <param name="zipFilePath">Full path to the ZIP file</param>
        /// <param name="reportProgress">Callback for reporting progress percentage</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>SplZipImportResult containing the import results for this ZIP file</returns>
        /// <seealso cref="SplImportService"/>
        /// <seealso cref="SplZipImportResult"/>
        /// <seealso cref="BufferedFile"/>
        private async Task<SplZipImportResult> processZipFileAsync(
            IServiceProvider serviceProvider,
            string zipFilePath,
            Action<int> reportProgress,
            CancellationToken token)
        {
            #region implementation

            // Create a scope for this ZIP file processing
            using var scope = serviceProvider.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<SplImportService>();

            // Create a BufferedFile representing the ZIP file on disk
            // The file is already on disk, so we use its path directly
            var bufferedFile = new BufferedFile
            {
                FileName = Path.GetFileName(zipFilePath),
                TempFilePath = zipFilePath
            };

            var bufferedFiles = new List<BufferedFile> { bufferedFile };

            // Process the ZIP file
            // userId is null for console import operations
            var results = await importService.ProcessZipFilesAsync(
                bufferedFiles,
                currentUserId: null,
                token: token,
                fileCounter: reportProgress,
                updateStatus: null,
                results: null);

            // Return the result for this single ZIP file
            return results.FirstOrDefault() ?? new SplZipImportResult
            {
                ZipFileName = Path.GetFileName(zipFilePath),
                FileResults = new List<SplFileImportResult>
                {
                    new SplFileImportResult
                    {
                        FileName = Path.GetFileName(zipFilePath),
                        Success = false,
                        Message = "No results returned from import service"
                    }
                }
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Aggregates statistics from a ZIP import result into the overall results.
        /// </summary>
        /// <param name="results">Overall import results to update</param>
        /// <param name="zipResult">ZIP import result to aggregate</param>
        /// <seealso cref="ImportResults"/>
        /// <seealso cref="SplZipImportResult"/>
        private void aggregateStatistics(ImportResults results, SplZipImportResult zipResult)
        {
            #region implementation

            foreach (var fileResult in zipResult.FileResults.Where(f => f.Success))
            {
                results.TotalDocuments += fileResult.DocumentsCreated;
                results.TotalOrganizations += fileResult.OrganizationsCreated;
                results.TotalProducts += fileResult.ProductsCreated;
                results.TotalSections += fileResult.SectionsCreated;
                results.TotalIngredients += fileResult.IngredientsCreated;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Aggregates statistics from a single ZIP import result.
        /// </summary>
        /// <param name="zipResult">ZIP import result to aggregate</param>
        /// <returns>Tuple containing entity counts for the ZIP file</returns>
        /// <seealso cref="SplZipImportResult"/>
        private (int documents, int organizations, int products, int sections, int ingredients) aggregateZipStatistics(
            SplZipImportResult zipResult)
        {
            #region implementation

            int documents = 0, organizations = 0, products = 0, sections = 0, ingredients = 0;

            foreach (var fileResult in zipResult.FileResults.Where(f => f.Success))
            {
                documents += fileResult.DocumentsCreated;
                organizations += fileResult.OrganizationsCreated;
                products += fileResult.ProductsCreated;
                sections += fileResult.SectionsCreated;
                ingredients += fileResult.IngredientsCreated;
            }

            return (documents, organizations, products, sections, ingredients);

            #endregion
        }

        #endregion
    }
}
