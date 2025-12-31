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

            // Track if we displayed command-line info (to prevent clearing it)
            var displayedCommandLineInfo = false;

            // Check for version flag
            if (HelpDocumentation.IsVersionRequested(args))
            {
                HelpDocumentation.DisplayVersion(settings);
                displayedCommandLineInfo = true;
                Spectre.Console.AnsiConsole.WriteLine();
            }

            // Check for help flag
            if (HelpDocumentation.IsHelpRequested(args))
            {
                HelpDocumentation.DisplayHelp(settings);
                displayedCommandLineInfo = true;
                Spectre.Console.AnsiConsole.WriteLine();
            }

            // Check for verbose flag - overrides settings
            var verboseMode = HelpDocumentation.IsVerboseRequested(args);

            // Run interactive import mode (skip header if we already displayed info)
            return await RunInteractiveImportAsync(settings, verboseMode, skipHeader: displayedCommandLineInfo);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Runs the interactive import process with user prompts.
        /// Includes crash recovery and resume functionality via progress tracking.
        /// </summary>
        /// <param name="settings">Application settings</param>
        /// <param name="verboseMode">Whether to enable verbose output</param>
        /// <param name="skipHeader">Whether to skip displaying the header (used when command-line info was already shown)</param>
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
        public static async Task<int> RunInteractiveImportAsync(Models.ConsoleAppSettings settings, bool verboseMode = false, bool skipHeader = false)
        {
            #region implementation

            // Display application header (unless command-line info was already shown)
            if (!skipHeader)
            {
                ConsoleHelper.DisplayHeader(settings);
            }

            // Create progress tracker for crash recovery
            var progressTracker = new ImportProgressTracker();

            try
            {
                // Run the main interactive loop with command menu
                return await ConsoleHelper.RunMainMenuAsync(settings, verboseMode, progressTracker);
            }
            catch (OperationCanceledException)
            {
                // User cancelled during parameter gathering
                ConsoleHelper.DisplayWarning("Exiting application.");
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
