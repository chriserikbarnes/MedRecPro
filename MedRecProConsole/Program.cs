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
        /// </summary>
        /// <param name="settings">Application settings</param>
        /// <param name="verboseMode">Whether to enable verbose output</param>
        /// <returns>Exit code: 0 for success, 1 for failure</returns>
        /// <seealso cref="ImportService"/>
        /// <seealso cref="ConsoleHelper"/>
        public static async Task<int> RunInteractiveImportAsync(Models.ConsoleAppSettings settings, bool verboseMode = false)
        {
            #region implementation

            // Display application header
            ConsoleHelper.DisplayHeader(settings);

            try
            {
                // Gather user parameters
                var parameters = ConsoleHelper.GatherUserParameters(settings);

                if (parameters == null)
                {
                    ConsoleHelper.DisplayError("Import cancelled by user.");
                    return 1;
                }

                // Store verbose mode in parameters for downstream use
                parameters.VerboseMode = verboseMode;

                // Confirm with user before proceeding (if required by settings)
                if (settings.ImportSettings.RequireConfirmation && !ConsoleHelper.ConfirmImport(parameters))
                {
                    ConsoleHelper.DisplayWarning("Import cancelled.");
                    return 0;
                }

                // Execute the import
                var importService = new ImportService();
                var results = await importService.ExecuteImportAsync(parameters);

                // Display results
                ConsoleHelper.DisplayResults(results, settings);

                // Interactive post-import menu (pass parameters for additional imports)
                await ConsoleHelper.RunPostImportMenuAsync(settings, results, parameters);

                return results.IsFullySuccessful ? 0 : 1;
            }
            catch (Exception ex)
            {
                ConsoleHelper.DisplayException(ex);
                return 1;
            }

            #endregion
        }
    }
}
