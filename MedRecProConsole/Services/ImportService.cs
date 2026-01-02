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
    /// </remarks>
    /// <seealso cref="SplImportService"/>
    /// <seealso cref="ImportParameters"/>
    /// <seealso cref="ImportResults"/>
    public class ImportService
    {
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
        /// Sets up dependency injection for MedRecPro services, processes each ZIP file,
        /// and reports progress using Spectre.Console. Failed ZIP imports are logged but
        /// do not stop the overall import process.
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

            var stopwatch = Stopwatch.StartNew();
            var cts = new CancellationTokenSource();

            // Track if we've been signaled to stop (but allow current file to complete)
            var shouldStopAfterCurrentFile = false;
            var interruptionReason = string.Empty;

            // Set up timeout if max runtime specified
            if (parameters.MaxRuntimeMinutes.HasValue)
            {
                cts.CancelAfter(TimeSpan.FromMinutes(parameters.MaxRuntimeMinutes.Value));
            }

            // Build configuration with required settings for MedRecPro services
            var configuration = ConfigurationHelper.BuildMedRecProConfiguration();

            // Redirect console output if not in verbose mode to suppress debug messages
            TextWriter? originalConsoleOut = null;
            if (!parameters.VerboseMode)
            {
                originalConsoleOut = Console.Out;
                Console.SetOut(TextWriter.Null);
            }

            // Build service provider with all required MedRecPro dependencies
            await using var serviceProvider = buildServiceProvider(parameters.ConnectionString, configuration, parameters.VerboseMode);

            // Initialize progress tracker if provided and not resuming
            if (progressTracker != null && !isResuming)
            {
                await progressTracker.LoadOrCreateQueueAsync(
                    parameters.ImportFolder,
                    parameters.ConnectionString,
                    parameters.ZipFiles,
                    parameters.MaxRuntimeMinutes,
                    parameters.VerboseMode);
            }

            // Track overall results
            var results = new ImportResults();

            // Restore console output for Spectre.Console UI
            if (originalConsoleOut != null)
            {
                Console.SetOut(originalConsoleOut);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule(isResuming
                ? "[bold yellow]Resuming Import[/]"
                : "[bold green]Starting Import[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();

            // Re-redirect console output during import processing
            if (!parameters.VerboseMode)
            {
                originalConsoleOut = Console.Out;
                Console.SetOut(TextWriter.Null);
            }

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
                    // Create overall progress task
                    var overallTask = ctx.AddTask("[green]Overall Progress[/]", maxValue: parameters.ZipFiles.Count);

                    foreach (var zipFilePath in parameters.ZipFiles)
                    {
                        // Check if we should stop after completing the previous file
                        if (shouldStopAfterCurrentFile)
                        {
                            break;
                        }

                        // Check for cancellation (timeout) BEFORE starting a new file
                        // This allows the current file to complete before stopping
                        if (cts.Token.IsCancellationRequested)
                        {
                            shouldStopAfterCurrentFile = true;
                            interruptionReason = "Maximum runtime reached";
                            AnsiConsole.MarkupLine("[yellow]Maximum runtime reached. Completing current file then stopping...[/]");
                            // Don't break here - we haven't started this file yet
                            // Instead, we'll check again at the top of the next iteration
                            break;
                        }

                        var zipFileName = Path.GetFileName(zipFilePath);

                        // Truncate display name to prevent wrapping on narrow consoles
                        var maxNameLength = Math.Max(30, AnsiConsole.Profile.Width / 3);
                        var displayName = zipFileName.Length > maxNameLength
                            ? zipFileName[..(maxNameLength - 3)] + "..."
                            : zipFileName;

                        var zipTask = ctx.AddTask($"[blue]{displayName}[/]", maxValue: 100);

                        try
                        {
                            // Check if file should be skipped due to nested queue
                            if (progressTracker != null &&
                                await progressTracker.IsFileCompletedInNestedQueueAsync(zipFilePath))
                            {
                                await progressTracker.MarkItemSkippedAsync(zipFilePath, "Completed in nested queue");
                                zipTask.Description = $"[grey]{displayName} - Skipped (nested queue)[/]";
                                zipTask.Value = 100;
                                overallTask.Increment(1);
                                continue;
                            }

                            // Mark as in-progress in the queue file
                            if (progressTracker != null)
                            {
                                await progressTracker.MarkItemInProgressAsync(zipFilePath);
                            }

                            // Process the file - use a separate token that we don't cancel mid-file
                            var zipResult = await processZipFileAsync(
                                serviceProvider,
                                zipFilePath,
                                progress => zipTask.Value = progress,
                                CancellationToken.None);  // Don't cancel mid-file for graceful shutdown

                            results.TotalZipsProcessed++;

                            if (zipResult.OverallSuccess)
                            {
                                results.SuccessfulZips++;
                                zipTask.Description = $"[green]{displayName} - Success ({zipResult.TotalFilesSucceeded} files)[/]";

                                // Mark as completed in the queue file with statistics
                                if (progressTracker != null)
                                {
                                    var stats = aggregateZipStatistics(zipResult);
                                    await progressTracker.MarkItemCompletedAsync(
                                        zipFilePath,
                                        stats.documents,
                                        stats.organizations,
                                        stats.products,
                                        stats.sections,
                                        stats.ingredients);
                                }
                            }
                            else
                            {
                                results.FailedZips++;
                                results.FailedZipNames.Add(zipFileName);

                                // Collect error messages
                                var errorMessages = new List<string>();
                                foreach (var fileResult in zipResult.FileResults.Where(f => !f.Success))
                                {
                                    var errorMsg = $"{zipFileName}/{fileResult.FileName}: {fileResult.Message}";
                                    results.Errors.Add(errorMsg);
                                    errorMessages.Add(fileResult.Message ?? "Unknown error");
                                }

                                // Mark as failed in the queue file
                                if (progressTracker != null)
                                {
                                    await progressTracker.MarkItemFailedAsync(
                                        zipFilePath,
                                        string.Join("; ", errorMessages.Take(3)));
                                }

                                zipTask.Description = $"[red]{displayName} - Failed ({zipResult.TotalFilesSucceeded}/{zipResult.TotalFilesProcessed} succeeded)[/]";
                            }

                            // Aggregate statistics
                            aggregateStatistics(results, zipResult);

                            zipTask.Value = 100;
                        }
                        catch (OperationCanceledException)
                        {
                            // This shouldn't happen now since we pass CancellationToken.None
                            // But handle it gracefully anyway
                            results.FailedZips++;
                            results.FailedZipNames.Add(zipFileName);
                            results.Errors.Add($"{zipFileName}: Import cancelled");
                            zipTask.Description = $"[yellow]{displayName} - Cancelled[/]";

                            if (progressTracker != null)
                            {
                                await progressTracker.MarkItemFailedAsync(zipFilePath, "Import cancelled");
                            }

                            shouldStopAfterCurrentFile = true;
                            interruptionReason = "Operation cancelled";
                            break;
                        }
                        catch (Exception ex)
                        {
                            results.FailedZips++;
                            results.FailedZipNames.Add(zipFileName);
                            results.Errors.Add($"{zipFileName}: {ex.Message}");
                            zipTask.Description = $"[red]{displayName} - Error[/]";

                            // Mark as failed in the queue file
                            if (progressTracker != null)
                            {
                                await progressTracker.MarkItemFailedAsync(zipFilePath, ex.Message);
                            }
                        }

                        overallTask.Increment(1);
                    }

                    overallTask.Value = overallTask.MaxValue;
                });

            // Restore console output after import processing
            if (originalConsoleOut != null)
            {
                Console.SetOut(originalConsoleOut);
            }

            stopwatch.Stop();
            results.ElapsedTime = stopwatch.Elapsed;

            // Record interruption if we stopped early
            if (progressTracker != null && !string.IsNullOrEmpty(interruptionReason))
            {
                await progressTracker.RecordInterruptionAsync(interruptionReason, stopwatch.Elapsed);
            }

            // Ensure final progress state is saved
            if (progressTracker != null)
            {
                await progressTracker.FlushAsync();

                // If all files are processed (completed/failed/skipped), optionally clean up
                var progressFile = progressTracker.GetProgressFile();
                if (progressFile != null && progressFile.IsComplete)
                {
                    // Queue is complete - it will be deleted when user confirms
                    // or left for debugging purposes
                }
            }

            return results;

            #endregion
        }

        #endregion

        #region private methods

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
