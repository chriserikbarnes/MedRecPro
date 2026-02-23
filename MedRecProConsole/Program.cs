using MedRecProConsole.Helpers;
using MedRecProConsole.Models;
using MedRecProConsole.Services;

namespace MedRecProConsole
{
    /**************************************************************/
    /// <summary>
    /// Console application for bulk importing SPL (Structured Product Labeling) ZIP files
    /// and FDA Orange Book data into the MedRecPro database. This application is designed
    /// for high-volume import operations that are not suitable for a web interface.
    /// </summary>
    /// <remarks>
    /// Uses Spectre.Console for styled console output and user parameter input.
    /// Leverages the existing MedRecPro services and data access layer for all import operations.
    /// Supports both interactive and unattended (Task Scheduler) operation modes.
    ///
    /// Interactive mode: Run without arguments for menu-driven interface.
    /// SPL unattended mode: Use --folder argument to enable automated SPL processing.
    /// Orange Book mode: Use --orange-book argument to import products.txt from ZIP.
    /// </remarks>
    /// <seealso cref="ImportService"/>
    /// <seealso cref="OrangeBookImportService"/>
    /// <seealso cref="ConsoleHelper"/>
    /// <seealso cref="ConfigurationHelper"/>
    /// <seealso cref="HelpDocumentation"/>
    /// <seealso cref="CommandLineArgs"/>
    public class Program
    {
        /**************************************************************/
        /// <summary>
        /// Main entry point for the MedRecPro Console Import application.
        /// Processes command line arguments and orchestrates the bulk import process.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Exit code: 0 for success, 1 for failure</returns>
        /// <remarks>
        /// Supported arguments:
        /// --help, -h: Display help documentation
        /// --version, -v: Display version information
        /// --verbose, -V: Enable verbose output
        /// --quiet, -q: Minimal output mode
        /// --folder path: Import folder (enables SPL unattended mode)
        /// --orange-book path: Orange Book ZIP file (enables Orange Book mode)
        /// --nuke: Truncate Orange Book tables before import (use with --orange-book)
        /// --connection name: Database connection name
        /// --time minutes: Maximum runtime in minutes
        /// --auto-quit: Exit immediately after import
        /// --config path: Use alternate configuration file
        ///
        /// No arguments: Run interactive import mode
        /// </remarks>
        /// <seealso cref="ImportService"/>
        /// <seealso cref="ConsoleHelper"/>
        /// <seealso cref="HelpDocumentation"/>
        /// <seealso cref="CommandLineArgs"/>
        public static async Task<int> Main(string[] args)
        {
            #region implementation

            // Parse command-line arguments
            var cmdArgs = CommandLineArgs.Parse(args);

            // Handle alternate config file first (before loading settings)
            if (!string.IsNullOrEmpty(cmdArgs.ConfigPath))
            {
                if (!ConfigurationHelper.SetAlternateConfigPath(cmdArgs.ConfigPath))
                {
                    Spectre.Console.AnsiConsole.MarkupLine($"[red]Error: Config file not found: {cmdArgs.ConfigPath}[/]");
                    return 1;
                }
            }

            // Load application settings (will use alternate config if specified)
            var settings = ConfigurationHelper.GetConsoleAppSettings();

            // Track if we displayed command-line info (to prevent clearing it)
            var displayedCommandLineInfo = false;

            // Check for version flag
            if (cmdArgs.ShowVersion)
            {
                HelpDocumentation.DisplayVersion(settings);
                displayedCommandLineInfo = true;
                Spectre.Console.AnsiConsole.WriteLine();
            }

            // Check for help flag
            if (cmdArgs.ShowHelp)
            {
                HelpDocumentation.DisplayHelp(settings);
                displayedCommandLineInfo = true;
                Spectre.Console.AnsiConsole.WriteLine();
            }

            // If only help/version requested, exit
            if ((cmdArgs.ShowHelp || cmdArgs.ShowVersion) && !cmdArgs.IsUnattendedMode && !cmdArgs.IsOrangeBookMode)
            {
                return 0;
            }

            // Display argument errors if any
            if (cmdArgs.HasErrors)
            {
                HelpDocumentation.DisplayArgumentErrors(cmdArgs.Errors);
                return 1;
            }

            // Determine operation mode
            if (cmdArgs.IsOrangeBookMode)
            {
                // Orange Book mode - import products.txt from ZIP
                return await runOrangeBookModeAsync(settings, cmdArgs);
            }
            else if (cmdArgs.IsUnattendedMode)
            {
                // SPL unattended mode - process folder and exit
                return await runUnattendedModeAsync(settings, cmdArgs);
            }
            else
            {
                // Interactive mode
                return await RunInteractiveImportAsync(settings, cmdArgs.VerboseMode, skipHeader: displayedCommandLineInfo);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Runs the application in unattended mode for Task Scheduler or automation.
        /// </summary>
        /// <param name="settings">Application settings</param>
        /// <param name="cmdArgs">Parsed command-line arguments</param>
        /// <returns>Exit code: 0 for full success, 1 for any failures</returns>
        /// <remarks>
        /// In unattended mode:
        /// - Uses --folder as the import path
        /// - Uses --connection or default database connection
        /// - Uses --time or settings for max runtime
        /// - Auto-quits based on --auto-quit flag or settings
        /// - Suppresses user prompts
        /// </remarks>
        /// <seealso cref="CommandLineArgs"/>
        /// <seealso cref="ConsoleHelper.RunUnattendedImportAsync"/>
        private static async Task<int> runUnattendedModeAsync(ConsoleAppSettings settings, CommandLineArgs cmdArgs)
        {
            #region implementation

            // Display header (respect quiet mode)
            if (!cmdArgs.QuietMode)
            {
                ConsoleHelper.DisplayHeader(settings);
            }

            // Resolve database connection
            string? connectionString = null;
            string connectionName = "Unknown";

            if (!string.IsNullOrEmpty(cmdArgs.ConnectionName))
            {
                // Use specified connection name
                var conn = settings.DatabaseConnections.FirstOrDefault(
                    c => c.Name.Equals(cmdArgs.ConnectionName, StringComparison.OrdinalIgnoreCase));

                if (conn == null)
                {
                    Spectre.Console.AnsiConsole.MarkupLine(
                        $"[red]Error: Database connection not found: {cmdArgs.ConnectionName}[/]");
                    Spectre.Console.AnsiConsole.MarkupLine("[grey]Available connections:[/]");
                    foreach (var db in settings.DatabaseConnections)
                    {
                        Spectre.Console.AnsiConsole.MarkupLine($"  [cyan]{db.Name}[/]");
                    }
                    return 1;
                }

                connectionString = conn.ConnectionString;
                connectionName = conn.Name;
            }
            else if (!string.IsNullOrEmpty(settings.Automation.DefaultConnectionName))
            {
                // Use automation default connection
                var conn = settings.DatabaseConnections.FirstOrDefault(
                    c => c.Name.Equals(settings.Automation.DefaultConnectionName, StringComparison.OrdinalIgnoreCase));

                if (conn != null)
                {
                    connectionString = conn.ConnectionString;
                    connectionName = conn.Name;
                }
            }

            // Fall back to default database if not found
            if (string.IsNullOrEmpty(connectionString))
            {
                var defaultConn = settings.DatabaseConnections.FirstOrDefault(c => c.IsDefault)
                    ?? settings.DatabaseConnections.FirstOrDefault();

                if (defaultConn == null)
                {
                    Spectre.Console.AnsiConsole.MarkupLine(
                        "[red]Error: No database connection configured[/]");
                    return 1;
                }

                connectionString = defaultConn.ConnectionString;
                connectionName = defaultConn.Name;
            }

            // Resolve max runtime
            int? maxRuntime = cmdArgs.MaxRuntimeMinutes
                ?? settings.Automation.DefaultMaxRuntimeMinutes
                ?? settings.ImportSettings.DefaultMaxRuntimeMinutes;

            // Determine auto-quit behavior
            bool autoQuit = cmdArgs.AutoQuit || settings.Automation.AutoQuitOnCompletion;

            // Display unattended mode info (unless quiet)
            if (!cmdArgs.QuietMode)
            {
                HelpDocumentation.DisplayUnattendedModeInfo(
                    cmdArgs.FolderPath!,
                    connectionName,
                    maxRuntime,
                    autoQuit);
            }

            // Run the unattended import
            var exitCode = await ConsoleHelper.RunUnattendedImportAsync(
                settings,
                cmdArgs.FolderPath!,
                connectionString,
                maxRuntime,
                cmdArgs.VerboseMode,
                cmdArgs.QuietMode);

            return exitCode;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Runs the application in Orange Book import mode.
        /// Imports products.txt from the specified ZIP file with optional table truncation.
        /// </summary>
        /// <param name="settings">Application settings</param>
        /// <param name="cmdArgs">Parsed command-line arguments</param>
        /// <returns>Exit code: 0 for success, 1 for failure</returns>
        /// <remarks>
        /// In Orange Book mode:
        /// - Uses --orange-book as the ZIP file path
        /// - Uses --nuke to truncate tables before import
        /// - Uses --connection or default database connection
        /// - Suppresses user prompts (unattended operation)
        /// </remarks>
        /// <seealso cref="OrangeBookImportService"/>
        /// <seealso cref="CommandLineArgs"/>
        private static async Task<int> runOrangeBookModeAsync(ConsoleAppSettings settings, CommandLineArgs cmdArgs)
        {
            #region implementation

            // Display header (respect quiet mode)
            if (!cmdArgs.QuietMode)
            {
                ConsoleHelper.DisplayHeader(settings);
            }

            // Resolve database connection (same pattern as runUnattendedModeAsync)
            string? connectionString = null;
            string connectionName = "Unknown";

            if (!string.IsNullOrEmpty(cmdArgs.ConnectionName))
            {
                // Use specified connection name
                var conn = settings.DatabaseConnections.FirstOrDefault(
                    c => c.Name.Equals(cmdArgs.ConnectionName, StringComparison.OrdinalIgnoreCase));

                if (conn == null)
                {
                    Spectre.Console.AnsiConsole.MarkupLine(
                        $"[red]Error: Database connection not found: {cmdArgs.ConnectionName}[/]");
                    Spectre.Console.AnsiConsole.MarkupLine("[grey]Available connections:[/]");
                    foreach (var db in settings.DatabaseConnections)
                    {
                        Spectre.Console.AnsiConsole.MarkupLine($"  [cyan]{db.Name}[/]");
                    }
                    return 1;
                }

                connectionString = conn.ConnectionString;
                connectionName = conn.Name;
            }
            else if (!string.IsNullOrEmpty(settings.Automation.DefaultConnectionName))
            {
                // Use automation default connection
                var conn = settings.DatabaseConnections.FirstOrDefault(
                    c => c.Name.Equals(settings.Automation.DefaultConnectionName, StringComparison.OrdinalIgnoreCase));

                if (conn != null)
                {
                    connectionString = conn.ConnectionString;
                    connectionName = conn.Name;
                }
            }

            // Fall back to default database if not found
            if (string.IsNullOrEmpty(connectionString))
            {
                var defaultConn = settings.DatabaseConnections.FirstOrDefault(c => c.IsDefault)
                    ?? settings.DatabaseConnections.FirstOrDefault();

                if (defaultConn == null)
                {
                    Spectre.Console.AnsiConsole.MarkupLine(
                        "[red]Error: No database connection configured[/]");
                    return 1;
                }

                connectionString = defaultConn.ConnectionString;
                connectionName = defaultConn.Name;
            }

            // Display mode info (unless quiet)
            if (!cmdArgs.QuietMode)
            {
                Spectre.Console.AnsiConsole.MarkupLine($"[grey]Database: {Spectre.Console.Markup.Escape(connectionName)}[/]");
                Spectre.Console.AnsiConsole.MarkupLine($"[grey]ZIP File: {Spectre.Console.Markup.Escape(cmdArgs.OrangeBookZipPath!)}[/]");
                if (cmdArgs.OrangeBookNuke)
                {
                    Spectre.Console.AnsiConsole.MarkupLine("[red]Truncation: Enabled (--nuke)[/]");
                }
                Spectre.Console.AnsiConsole.WriteLine();
            }

            // Execute the import
            var importService = new OrangeBookImportService();
            var result = await importService.ExecuteImportAsync(
                connectionString,
                cmdArgs.OrangeBookZipPath!,
                cmdArgs.OrangeBookNuke,
                cmdArgs.VerboseMode);

            // Display results (unless quiet)
            if (!cmdArgs.QuietMode)
            {
                ConsoleHelper.DisplayOrangeBookResults(result, settings);
            }

            return result.Success ? 0 : 1;

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
