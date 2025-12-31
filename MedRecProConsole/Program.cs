using MedRecProConsole.Helpers;
using MedRecProConsole.Services;

namespace MedRecProConsole
{
    /**************************************************************/
    /// <summary>
    /// Console application for bulk importing SPL (Structured Product Labeling) ZIP files
    /// into the MedRecPro database. This application is designed for high-volume import
    /// operations that are not suitable for a web interface.
    /// </summary>
    /// <remarks>
    /// Uses Spectre.Console for styled console output and user parameter input.
    /// Leverages the existing MedRecPro services and data access layer for all import operations.
    /// Supports command line arguments for help (--help, -h) and version (--version, -v).
    /// </remarks>
    /// <seealso cref="ImportService"/>
    /// <seealso cref="ConsoleHelper"/>
    /// <seealso cref="ConfigurationHelper"/>
    /// <seealso cref="HelpDocumentation"/>
    public class Program
    {
        /**************************************************************/
        /// <summary>
        /// Main entry point for the MedRecPro Console Import application.
        /// Processes command line arguments and orchestrates the bulk import process.
        /// </summary>
        /// <param name="args">Command line arguments (--help, --version, etc.)</param>
        /// <returns>Exit code: 0 for success, 1 for failure</returns>
        /// <remarks>
        /// Supported arguments:
        /// --help, -h: Display help documentation
        /// --version, -v: Display version information
        /// No arguments: Run interactive import mode
        /// </remarks>
        /// <seealso cref="ImportService"/>
        /// <seealso cref="ConsoleHelper"/>
        /// <seealso cref="HelpDocumentation"/>
        public static async Task<int> Main(string[] args)
        {
            #region implementation

            // Load application settings
            var settings = ConfigurationHelper.GetConsoleAppSettings();

            // Check for version flag
            if (HelpDocumentation.IsVersionRequested(args))
            {
                HelpDocumentation.DisplayVersion(settings);
                return 0;
            }

            // Check for help flag
            if (HelpDocumentation.IsHelpRequested(args))
            {
                HelpDocumentation.DisplayHelp(settings);
                return 0;
            }

            // Check for verbose flag - overrides settings
            var verboseMode = HelpDocumentation.IsVerboseRequested(args);

            // Run interactive import mode
            return await RunInteractiveImportAsync(settings, verboseMode);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Runs the interactive import process with user prompts.
        /// Includes crash recovery and resume functionality via progress tracking.
        /// </summary>
        /// <param name="settings">Application settings</param>
        /// <param name="verboseMode">Whether to enable verbose output</param>
        /// <returns>Exit code: 0 for success, 1 for failure</returns>
        /// <remarks>
        /// Creates a progress file at the import folder root to enable:
        /// - Resume after timer expiration
        /// - Resume after application crash
        /// - Resume after manual exit
        /// - Respecting progress from nested subdirectory imports
        /// </remarks>
        /// <seealso cref="ImportService"/>
        /// <seealso cref="ImportProgressTracker"/>
        /// <seealso cref="ConsoleHelper"/>
        public static async Task<int> RunInteractiveImportAsync(Models.ConsoleAppSettings settings, bool verboseMode = false)
        {
            #region implementation

            // Display application header
            ConsoleHelper.DisplayHeader(settings);

            // Create progress tracker for crash recovery
            var progressTracker = new ImportProgressTracker();

            try
            {
                // Gather user parameters (now async to support resume detection)
                var (parameters, isResuming) = await ConsoleHelper.GatherUserParametersAsync(settings, progressTracker);

                if (parameters == null)
                {
                    ConsoleHelper.DisplayError("Import cancelled by user.");
                    return 1;
                }

                // Store verbose mode in parameters for downstream use
                parameters.VerboseMode = verboseMode;

                // Check if user declined to delete a complete queue - skip import and go to menu
                // This happens when ZipFiles is empty but parameters is valid
                Models.ImportResults results;
                if (parameters.ZipFiles == null || parameters.ZipFiles.Count == 0)
                {
                    // No files to import - create empty results and go directly to menu
                    results = new Models.ImportResults();
                }
                else
                {
                    // Confirm with user before proceeding (if required by settings and not resuming)
                    // When resuming, the user already confirmed via the resume prompt
                    if (!isResuming && settings.ImportSettings.RequireConfirmation && !ConsoleHelper.ConfirmImport(parameters))
                    {
                        ConsoleHelper.DisplayWarning("Import cancelled.");
                        return 0;
                    }

                    // Execute the import with progress tracking
                    var importService = new ImportService();
                    results = await importService.ExecuteImportAsync(parameters, progressTracker, isResuming);

                    // Display results
                    ConsoleHelper.DisplayResults(results, settings);

                    // Check if import completed fully or has remaining items
                    var progressFile = progressTracker.GetProgressFile();
                    if (progressFile != null && progressFile.IsComplete)
                    {
                        // Import is complete - inform user the queue file exists for their reference
                        Spectre.Console.AnsiConsole.MarkupLine(
                            $"[green]Import complete. Queue file saved at:[/] {progressFile.RootDirectory}");
                        Spectre.Console.AnsiConsole.MarkupLine(
                            "[grey]Delete the queue file manually when no longer needed.[/]");
                    }
                    else if (progressFile != null && progressFile.RemainingItems > 0)
                    {
                        // Import stopped with remaining items - inform user
                        Spectre.Console.AnsiConsole.MarkupLine(
                            $"[yellow]{progressFile.RemainingItems} file(s) remaining. " +
                            "Run the import again to resume.[/]");
                    }
                }

                // Interactive post-import menu (pass parameters and progress tracker for additional imports)
                await ConsoleHelper.RunPostImportMenuAsync(settings, results, parameters, progressTracker);

                return results.IsFullySuccessful ? 0 : 1;
            }
            catch (OperationCanceledException)
            {
                // User cancelled during parameter gathering
                ConsoleHelper.DisplayWarning("Import cancelled.");
                return 0;
            }
            catch (Exception ex)
            {
                ConsoleHelper.DisplayException(ex);

                // Try to save progress state on crash
                try
                {
                    await progressTracker.RecordInterruptionAsync($"Crash: {ex.Message}", TimeSpan.Zero);
                }
                catch
                {
                    // Ignore errors saving progress on crash
                }

                return 1;
            }

            #endregion
        }
    }
}
