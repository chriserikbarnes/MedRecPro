using MedRecProConsole.Models;
using MedRecProConsole.Services;
using Spectre.Console;

namespace MedRecProConsole.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Helper class for console UI operations using Spectre.Console.
    /// Provides methods for displaying headers, gathering user input, and showing results.
    /// </summary>
    /// <remarks>
    /// Centralizes all Spectre.Console UI operations to keep Program.cs clean
    /// and allow for consistent styling across the application.
    /// Uses settings from ConsoleAppSettings for configurable behavior.
    /// </remarks>
    /// <seealso cref="ImportParameters"/>
    /// <seealso cref="ImportResults"/>
    /// <seealso cref="ConsoleAppSettings"/>
    public static class ConsoleHelper
    {
        #region public methods

        /**************************************************************/
        /// <summary>
        /// Displays the application header with styled console output.
        /// </summary>
        /// <param name="settings">Application settings for display configuration</param>
        /// <seealso cref="AnsiConsole"/>
        /// <seealso cref="DisplaySettings"/>
        public static void DisplayHeader(ConsoleAppSettings settings)
        {
            #region implementation

            // Clear console if configured
            if (settings.Display.ClearConsoleOnStart)
            {
                AnsiConsole.Clear();
            }

            // Show banner if configured
            if (settings.Display.ShowBanner)
            {
                var bannerColor = getBannerColor(settings.Display.BannerColor);

                AnsiConsole.Write(
                    new FigletText("MedRecPro")
                        .LeftJustified()
                        .Color(bannerColor));
            }

            AnsiConsole.Write(
                new Rule($"[bold blue]{Markup.Escape(settings.Application.Description)}[/]")
                    .RuleStyle("grey")
                    .LeftJustified());

            AnsiConsole.MarkupLine($"[grey]Version {Markup.Escape(settings.Application.Version)}[/]");
            AnsiConsole.WriteLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gathers import parameters from the user using Spectre.Console prompts.
        /// Checks for existing queue files to enable resume functionality.
        /// </summary>
        /// <param name="settings">Application settings for database connections and defaults</param>
        /// <param name="progressTracker">Progress tracker instance for resume detection</param>
        /// <returns>
        /// Tuple containing:
        /// - ImportParameters: User selections or null if cancelled
        /// - bool: True if resuming from existing queue, false if new import
        /// </returns>
        /// <seealso cref="ImportParameters"/>
        /// <seealso cref="DatabaseConnectionSettings"/>
        /// <seealso cref="ImportProgressTracker"/>
        public static async Task<(ImportParameters? Parameters, bool IsResuming)> GatherUserParametersAsync(
            ConsoleAppSettings settings,
            ImportProgressTracker progressTracker)
        {
            #region implementation

            var parameters = new ImportParameters();

            // Build database choices from settings
            var dbChoices = buildDatabaseChoices(settings);
            var defaultChoice = dbChoices.FirstOrDefault(c => c.IsDefault) ?? dbChoices.FirstOrDefault();

            // Database selection
            var prompt = new SelectionPrompt<DatabaseChoice>()
                .Title("[green]Select database connection:[/]")
                .PageSize(10)
                .UseConverter(c => c.DisplayName)
                .AddChoices(dbChoices);

            var databaseChoice = AnsiConsole.Prompt(prompt);

            if (databaseChoice.IsCustom)
            {
                parameters.ConnectionString = AnsiConsole.Prompt(
                    new TextPrompt<string>("[green]Enter connection string:[/]")
                        .PromptStyle("white")
                        .Validate(cs =>
                        {
                            if (string.IsNullOrWhiteSpace(cs))
                                return ValidationResult.Error("[red]Connection string cannot be empty[/]");
                            return ValidationResult.Success();
                        }));
            }
            else
            {
                parameters.ConnectionString = databaseChoice.ConnectionString;
            }

            AnsiConsole.WriteLine();

            // Folder selection
            parameters.ImportFolder = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]Enter folder path to import (contains ZIP files):[/]")
                    .PromptStyle("white")
                    .Validate(path =>
                    {
                        if (string.IsNullOrWhiteSpace(path))
                            return ValidationResult.Error("[red]Path cannot be empty[/]");
                        if (!Directory.Exists(path))
                            return ValidationResult.Error($"[red]Directory does not exist: {path}[/]");
                        return ValidationResult.Success();
                    }));

            AnsiConsole.WriteLine();

            // Check for existing queue file (resume capability)
            var isResuming = false;
            if (progressTracker.QueueFileExists(parameters.ImportFolder))
            {
                isResuming = await handleExistingQueueFileAsync(parameters, progressTracker, settings);

                // If resuming, the parameters are already populated from the queue file
                if (isResuming)
                {
                    // Check if this was a "skip" signal (complete queue, user declined delete)
                    // Return with empty ZipFiles - caller should skip import and go to post-import menu
                    if (parameters.ZipFiles != null && parameters.ZipFiles.Count == 0)
                    {
                        return (parameters, false); // Not resuming, but valid params with empty files
                    }

                    return (parameters, true);
                }
            }

            // Optional max runtime (with default from settings if configured)
            var defaultRuntime = settings.ImportSettings.DefaultMaxRuntimeMinutes;
            var useMaxRuntime = AnsiConsole.Confirm("[green]Set a maximum runtime limit?[/]", defaultRuntime.HasValue);

            if (useMaxRuntime)
            {
                parameters.MaxRuntimeMinutes = AnsiConsole.Prompt(
                    new TextPrompt<int>("[green]Enter maximum runtime in minutes:[/]")
                        .PromptStyle("white")
                        .DefaultValue(defaultRuntime ?? 60)
                        .Validate(minutes =>
                        {
                            if (minutes < 1)
                                return ValidationResult.Error("[red]Runtime must be at least 1 minute[/]");
                            if (minutes > 1440)
                                return ValidationResult.Error("[red]Runtime cannot exceed 24 hours (1440 minutes)[/]");
                            return ValidationResult.Success();
                        }));
            }

            // Scan folder for ZIP files
            parameters.ZipFiles = scanForZipFiles(parameters.ImportFolder, settings.Display.ShowSpinners);

            if (parameters.ZipFiles.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No ZIP files found in: {Markup.Escape(parameters.ImportFolder)}[/]");
                return (null, false);
            }

            return (parameters, false);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Legacy synchronous version for backwards compatibility.
        /// </summary>
        /// <param name="settings">Application settings for database connections and defaults</param>
        /// <returns>ImportParameters object containing user selections, or null if cancelled</returns>
        /// <seealso cref="ImportParameters"/>
        /// <seealso cref="DatabaseConnectionSettings"/>
        [Obsolete("Use GatherUserParametersAsync instead for resume support")]
        public static ImportParameters? GatherUserParameters(ConsoleAppSettings settings)
        {
            #region implementation

            var parameters = new ImportParameters();

            // Build database choices from settings
            var dbChoices = buildDatabaseChoices(settings);
            var defaultChoice = dbChoices.FirstOrDefault(c => c.IsDefault) ?? dbChoices.FirstOrDefault();

            // Database selection
            var prompt = new SelectionPrompt<DatabaseChoice>()
                .Title("[green]Select database connection:[/]")
                .PageSize(10)
                .UseConverter(c => c.DisplayName)
                .AddChoices(dbChoices);

            var databaseChoice = AnsiConsole.Prompt(prompt);

            if (databaseChoice.IsCustom)
            {
                parameters.ConnectionString = AnsiConsole.Prompt(
                    new TextPrompt<string>("[green]Enter connection string:[/]")
                        .PromptStyle("white")
                        .Validate(cs =>
                        {
                            if (string.IsNullOrWhiteSpace(cs))
                                return ValidationResult.Error("[red]Connection string cannot be empty[/]");
                            return ValidationResult.Success();
                        }));
            }
            else
            {
                parameters.ConnectionString = databaseChoice.ConnectionString;
            }

            AnsiConsole.WriteLine();

            // Folder selection
            parameters.ImportFolder = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]Enter folder path to import (contains ZIP files):[/]")
                    .PromptStyle("white")
                    .Validate(path =>
                    {
                        if (string.IsNullOrWhiteSpace(path))
                            return ValidationResult.Error("[red]Path cannot be empty[/]");
                        if (!Directory.Exists(path))
                            return ValidationResult.Error($"[red]Directory does not exist: {path}[/]");
                        return ValidationResult.Success();
                    }));

            AnsiConsole.WriteLine();

            // Optional max runtime (with default from settings if configured)
            var defaultRuntime = settings.ImportSettings.DefaultMaxRuntimeMinutes;
            var useMaxRuntime = AnsiConsole.Confirm("[green]Set a maximum runtime limit?[/]", defaultRuntime.HasValue);

            if (useMaxRuntime)
            {
                parameters.MaxRuntimeMinutes = AnsiConsole.Prompt(
                    new TextPrompt<int>("[green]Enter maximum runtime in minutes:[/]")
                        .PromptStyle("white")
                        .DefaultValue(defaultRuntime ?? 60)
                        .Validate(minutes =>
                        {
                            if (minutes < 1)
                                return ValidationResult.Error("[red]Runtime must be at least 1 minute[/]");
                            if (minutes > 1440)
                                return ValidationResult.Error("[red]Runtime cannot exceed 24 hours (1440 minutes)[/]");
                            return ValidationResult.Success();
                        }));
            }

            // Scan folder for ZIP files
            parameters.ZipFiles = scanForZipFiles(parameters.ImportFolder, settings.Display.ShowSpinners);

            if (parameters.ZipFiles.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No ZIP files found in: {Markup.Escape(parameters.ImportFolder)}[/]");
                return null;
            }

            return parameters;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays import parameters and prompts user for confirmation.
        /// </summary>
        /// <param name="parameters">Import parameters to display</param>
        /// <returns>True if user confirms, false otherwise</returns>
        /// <seealso cref="ImportParameters"/>
        public static bool ConfirmImport(ImportParameters parameters)
        {
            #region implementation

            AnsiConsole.WriteLine();

            // Display summary table with flexible width
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("[bold]Setting[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Value[/]"));

            // Mask connection string for display - use console width to determine truncation
            var maxWidth = Math.Max(40, AnsiConsole.Profile.Width - 30);
            var displayConnectionString = parameters.ConnectionString.Length > maxWidth
                ? parameters.ConnectionString[..maxWidth] + "..."
                : parameters.ConnectionString;

            table.AddRow("Database", Markup.Escape(displayConnectionString));
            table.AddRow("Import Folder", Markup.Escape(parameters.ImportFolder));
            table.AddRow("ZIP Files Found", parameters.ZipFiles.Count.ToString());
            table.AddRow("Max Runtime", parameters.MaxRuntimeMinutes.HasValue
                ? $"{parameters.MaxRuntimeMinutes} minutes"
                : "No limit");

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            return AnsiConsole.Confirm("[yellow]Proceed with import?[/]", true);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays the final import results with styled console output.
        /// </summary>
        /// <param name="results">Import results to display</param>
        /// <param name="settings">Application settings for display configuration</param>
        /// <seealso cref="ImportResults"/>
        /// <seealso cref="ConsoleAppSettings"/>
        public static void DisplayResults(ImportResults results, ConsoleAppSettings settings)
        {
            #region implementation

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold green]Import Complete[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();

            // Summary table - use Expand() for flexible width
            var summaryTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("[bold]Metric[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Value[/]").NoWrap());

            summaryTable.AddRow("Total ZIP Files", results.TotalZipsProcessed.ToString());
            summaryTable.AddRow("[green]Successful[/]", results.SuccessfulZips.ToString());
            summaryTable.AddRow("[red]Failed[/]", results.FailedZips.ToString());
            summaryTable.AddRow("Elapsed Time", results.ElapsedTime.ToString(@"hh\:mm\:ss"));

            AnsiConsole.Write(summaryTable);
            AnsiConsole.WriteLine();

            // Entity counts table - use Expand() for flexible width
            var entityTable = new Table()
                .Border(TableBorder.Rounded)
                .Title("[bold blue]Entities Created[/]")
                .AddColumn(new TableColumn("[bold]Entity Type[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Count[/]").NoWrap());

            entityTable.AddRow("Documents", results.TotalDocuments.ToString("N0"));
            entityTable.AddRow("Organizations", results.TotalOrganizations.ToString("N0"));
            entityTable.AddRow("Products", results.TotalProducts.ToString("N0"));
            entityTable.AddRow("Sections", results.TotalSections.ToString("N0"));
            entityTable.AddRow("Ingredients", results.TotalIngredients.ToString("N0"));

            AnsiConsole.Write(entityTable);

            // Display errors if any (limited by settings)
            if (results.Errors.Count > 0)
            {
                displayErrors(results.Errors, settings.ImportSettings.MaxDisplayedErrors);
            }

            // Final status message
            AnsiConsole.WriteLine();

            if (results.IsFullySuccessful)
            {
                AnsiConsole.MarkupLine("[bold green]Import completed successfully![/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[bold yellow]Import completed with {results.FailedZips} failed ZIP file(s).[/]");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays an error message in styled console output.
        /// </summary>
        /// <param name="message">Error message to display</param>
        public static void DisplayError(string message)
        {
            #region implementation

            AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays a warning message in styled console output.
        /// </summary>
        /// <param name="message">Warning message to display</param>
        public static void DisplayWarning(string message)
        {
            #region implementation

            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays an exception in styled console output.
        /// </summary>
        /// <param name="ex">Exception to display</param>
        public static void DisplayException(Exception ex)
        {
            #region implementation

            AnsiConsole.WriteException(ex);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Runs the interactive post-import menu allowing user to quit or get help.
        /// </summary>
        /// <param name="settings">Application settings</param>
        /// <param name="results">Import results for display</param>
        /// <param name="currentParameters">Current import parameters (for additional imports)</param>
        /// <param name="progressTracker">Progress tracker for crash recovery (optional for backward compatibility)</param>
        /// <returns>Task representing the async operation</returns>
        /// <seealso cref="ConsoleAppSettings"/>
        /// <seealso cref="ImportResults"/>
        /// <seealso cref="ImportProgressTracker"/>
        public static async Task RunPostImportMenuAsync(
            ConsoleAppSettings settings,
            ImportResults results,
            ImportParameters? currentParameters = null,
            ImportProgressTracker? progressTracker = null)
        {
            #region implementation

            AnsiConsole.WriteLine();

            while (true)
            {
                var command = AnsiConsole.Prompt(
                    new TextPrompt<string>("[grey]Enter command ([green]quit[/] to exit, [blue]help[/] for options):[/]")
                        .PromptStyle("white")
                        .AllowEmpty());

                var normalizedCommand = command.Trim().ToLowerInvariant();

                switch (normalizedCommand)
                {
                    case "quit":
                    case "q":
                    case "exit":
                        AnsiConsole.MarkupLine("[grey]Exiting...[/]");
                        return;

                    case "help":
                    case "h":
                    case "?":
                        displayPostImportHelp();
                        break;

                    case "summary":
                    case "s":
                        DisplayResults(results, settings);
                        break;

                    case "errors":
                    case "e":
                        if (results.Errors.Count > 0)
                        {
                            displayErrors(results.Errors, settings.ImportSettings.MaxDisplayedErrors);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[green]No errors to display.[/]");
                        }
                        break;

                    case "import":
                    case "i":
                        if (currentParameters != null)
                        {
                            var additionalResults = await runAdditionalImportAsync(settings, currentParameters, progressTracker);
                            if (additionalResults != null)
                            {
                                // Merge results
                                results.TotalZipsProcessed += additionalResults.TotalZipsProcessed;
                                results.SuccessfulZips += additionalResults.SuccessfulZips;
                                results.FailedZips += additionalResults.FailedZips;
                                results.TotalDocuments += additionalResults.TotalDocuments;
                                results.TotalOrganizations += additionalResults.TotalOrganizations;
                                results.TotalProducts += additionalResults.TotalProducts;
                                results.TotalSections += additionalResults.TotalSections;
                                results.TotalIngredients += additionalResults.TotalIngredients;
                                results.Errors.AddRange(additionalResults.Errors);
                                results.FailedZipNames.AddRange(additionalResults.FailedZipNames);

                                // Display combined results
                                DisplayResults(results, settings);
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[yellow]Import parameters not available. Please restart the application.[/]");
                        }
                        break;

                    case "":
                        // Empty input - show hint
                        AnsiConsole.MarkupLine("[grey]Type 'help' for available commands or 'quit' to exit.[/]");
                        break;

                    default:
                        AnsiConsole.MarkupLine($"[yellow]Unknown command: {Markup.Escape(command)}[/]");
                        AnsiConsole.MarkupLine("[grey]Type 'help' for available commands.[/]");
                        break;
                }

                // Small delay to allow console to settle
                await Task.Delay(100);
            }

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Gets the Spectre.Console Color for the banner from string name.
        /// </summary>
        /// <param name="colorName">Color name from settings</param>
        /// <returns>Spectre.Console Color</returns>
        private static Color getBannerColor(string colorName)
        {
            #region implementation

            return colorName.ToLowerInvariant() switch
            {
                "blue" => Color.Blue,
                "green" => Color.Green,
                "red" => Color.Red,
                "yellow" => Color.Yellow,
                "cyan" => Color.Cyan1,
                "magenta" => Color.Magenta1,
                "white" => Color.White,
                "grey" or "gray" => Color.Grey,
                _ => Color.Blue
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds database choices from settings for the selection prompt.
        /// </summary>
        /// <param name="settings">Application settings</param>
        /// <returns>List of database choices</returns>
        private static List<DatabaseChoice> buildDatabaseChoices(ConsoleAppSettings settings)
        {
            #region implementation

            var choices = new List<DatabaseChoice>();

            // Add configured database connections
            foreach (var db in settings.DatabaseConnections)
            {
                choices.Add(new DatabaseChoice
                {
                    DisplayName = db.Name,
                    ConnectionString = db.ConnectionString,
                    IsDefault = db.IsDefault,
                    IsCustom = false
                });
            }

            // Always add custom option at the end
            choices.Add(new DatabaseChoice
            {
                DisplayName = "Custom Connection String",
                ConnectionString = string.Empty,
                IsDefault = false,
                IsCustom = true
            });

            return choices;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles an existing queue file by prompting the user to resume or start fresh.
        /// </summary>
        /// <param name="parameters">Import parameters to populate on resume</param>
        /// <param name="progressTracker">Progress tracker for loading the queue</param>
        /// <param name="settings">Application settings</param>
        /// <returns>True if user chose to resume, false to start fresh</returns>
        /// <seealso cref="ImportProgressFile"/>
        /// <seealso cref="ImportProgressTracker"/>
        private static async Task<bool> handleExistingQueueFileAsync(
            ImportParameters parameters,
            ImportProgressTracker progressTracker,
            ConsoleAppSettings settings)
        {
            #region implementation

            // Load the existing queue to get its status
            ImportProgressFile? existingQueue = null;

            try
            {
                existingQueue = await progressTracker.LoadOrCreateQueueAsync(
                    parameters.ImportFolder,
                    parameters.ConnectionString);
            }
            catch (InvalidOperationException ex)
            {
                // Connection string mismatch - inform user and offer options
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Existing import queue found but cannot be used:[/]");
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
                AnsiConsole.WriteLine();

                var deleteChoice = AnsiConsole.Confirm("[yellow]Delete the existing queue and start fresh?[/]", true);

                if (deleteChoice)
                {
                    // Delete the queue file and return false to start fresh
                    var queuePath = Path.Combine(parameters.ImportFolder, ImportProgressFile.DefaultFileName);
                    if (File.Exists(queuePath))
                    {
                        File.Delete(queuePath);
                    }
                    return false;
                }
                else
                {
                    // User declined to delete, they need to use the original connection
                    AnsiConsole.MarkupLine("[grey]Please use the original database connection or delete the queue file manually.[/]");
                    throw;
                }
            }

            // Display queue status and prompt user
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold yellow]Existing Import Queue Found[/]").RuleStyle("yellow"));
            AnsiConsole.WriteLine();

            // Display queue status table
            var statusTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("[bold]Property[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Value[/]"));

            statusTable.AddRow("Created", existingQueue.CreatedAt.ToLocalTime().ToString("g"));
            statusTable.AddRow("Last Updated", existingQueue.LastUpdatedAt.ToLocalTime().ToString("g"));
            statusTable.AddRow("Total Files", existingQueue.TotalItems.ToString());
            statusTable.AddRow("[green]Completed[/]", existingQueue.CompletedItems.ToString());
            statusTable.AddRow("[yellow]Pending[/]", existingQueue.QueuedItems.ToString());
            statusTable.AddRow("[red]Failed[/]", existingQueue.FailedItems.ToString());
            statusTable.AddRow("Completion", $"{existingQueue.CompletionPercentage:F1}%");
            statusTable.AddRow("Resume Count", existingQueue.ResumeCount.ToString());

            if (!string.IsNullOrEmpty(existingQueue.LastInterruptionReason))
            {
                statusTable.AddRow("Last Interruption", existingQueue.LastInterruptionReason);
            }

            AnsiConsole.Write(statusTable);
            AnsiConsole.WriteLine();

            // Check if import is already complete
            if (existingQueue.IsComplete)
            {
                AnsiConsole.MarkupLine("[green]This import is already complete![/]");
                var deleteCompleted = AnsiConsole.Confirm("[yellow]Delete the queue file and start a new import?[/]", true);

                if (deleteCompleted)
                {
                    await progressTracker.DeleteQueueFileAsync();
                    return false;
                }
                else
                {
                    // User doesn't want to delete the complete queue - signal to skip this folder
                    // by setting a special flag in parameters that will be handled upstream
                    parameters.ZipFiles = new List<string>(); // Empty list signals skip
                    return true; // Return true with empty ZipFiles to signal "skip this folder"
                }
            }

            // Prompt for action
            var resumeChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]What would you like to do?[/]")
                    .AddChoices(new[]
                    {
                        "Resume import from where it left off",
                        "Start fresh (delete existing progress)",
                        "Cancel"
                    }));

            switch (resumeChoice)
            {
                case "Resume import from where it left off":
                    // Populate parameters from the queue
                    parameters.MaxRuntimeMinutes = existingQueue.MaxRuntimeMinutes;
                    parameters.VerboseMode = existingQueue.VerboseMode;

                    // Get pending files from the queue
                    parameters.ZipFiles = progressTracker.GetPendingFiles();

                    AnsiConsole.MarkupLine($"[green]Resuming with {parameters.ZipFiles.Count} remaining file(s)[/]");
                    return true;

                case "Start fresh (delete existing progress)":
                    await progressTracker.DeleteQueueFileAsync();
                    AnsiConsole.MarkupLine("[yellow]Queue deleted. Starting fresh...[/]");
                    return false;

                default:
                    throw new OperationCanceledException("Import cancelled by user");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recursively scans a directory for ZIP files.
        /// </summary>
        /// <param name="folderPath">Root folder path to scan</param>
        /// <param name="showSpinner">Whether to show spinner animation</param>
        /// <returns>List of full paths to ZIP files found</returns>
        private static List<string> scanForZipFiles(string folderPath, bool showSpinner)
        {
            #region implementation

            var zipFiles = new List<string>();

            if (showSpinner)
            {
                AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .Start("[yellow]Scanning for ZIP files...[/]", ctx =>
                    {
                        zipFiles = performZipScan(folderPath);
                    });
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Scanning for ZIP files...[/]");
                zipFiles = performZipScan(folderPath);
            }

            AnsiConsole.MarkupLine($"[green]Found {zipFiles.Count} ZIP file(s)[/]");

            return zipFiles;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Performs the actual ZIP file scan.
        /// </summary>
        /// <param name="folderPath">Folder to scan</param>
        /// <returns>List of ZIP file paths</returns>
        private static List<string> performZipScan(string folderPath)
        {
            #region implementation

            try
            {
                return Directory.GetFiles(folderPath, "*.zip", SearchOption.AllDirectories)
                    .ToList();
            }
            catch (UnauthorizedAccessException ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Access denied to some directories: {Markup.Escape(ex.Message)}[/]");
                return new List<string>();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays error messages in a styled panel.
        /// </summary>
        /// <param name="errors">List of error messages</param>
        /// <param name="maxDisplayed">Maximum number of errors to display</param>
        private static void displayErrors(List<string> errors, int maxDisplayed)
        {
            #region implementation

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold red]Errors[/]").RuleStyle("red"));
            AnsiConsole.WriteLine();

            var panel = new Panel(
                new Rows(errors.Take(maxDisplayed).Select(e => new Text(e, new Style(Color.Red)))))
                .Header("[bold red]Error Details[/]")
                .Border(BoxBorder.Rounded)
                .Expand();

            AnsiConsole.Write(panel);

            if (errors.Count > maxDisplayed)
            {
                AnsiConsole.MarkupLine($"[yellow]... and {errors.Count - maxDisplayed} more errors (see log for details)[/]");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays the help menu for post-import commands.
        /// </summary>
        private static void displayPostImportHelp()
        {
            #region implementation

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold blue]Available Commands[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Command[/]")
                .AddColumn("[bold]Description[/]")
                .Expand();

            table.AddRow("[green]quit, q, exit[/]", "Exit the application");
            table.AddRow("[blue]help, h, ?[/]", "Display this help message");
            table.AddRow("[cyan]summary, s[/]", "Display import summary again");
            table.AddRow("[yellow]errors, e[/]", "Display error details");
            table.AddRow("[magenta]import, i[/]", "Import additional folder (uses same database)");

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Runs an additional import operation from a new folder using the same database connection.
        /// Supports progress tracking for crash recovery.
        /// </summary>
        /// <param name="settings">Application settings</param>
        /// <param name="currentParameters">Current import parameters containing database connection</param>
        /// <param name="progressTracker">Progress tracker for crash recovery (can be null for legacy behavior)</param>
        /// <returns>Import results, or null if cancelled</returns>
        /// <seealso cref="ImportParameters"/>
        /// <seealso cref="ImportResults"/>
        /// <seealso cref="ImportProgressTracker"/>
        private static async Task<ImportResults?> runAdditionalImportAsync(
            ConsoleAppSettings settings,
            ImportParameters currentParameters,
            ImportProgressTracker? progressTracker)
        {
            #region implementation

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold magenta]Additional Import[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();

            // Prompt for new folder path
            var newFolderPath = AnsiConsole.Prompt(
                new TextPrompt<string>("[green]Enter folder path to import (contains ZIP files):[/]")
                    .PromptStyle("white")
                    .AllowEmpty()
                    .Validate(path =>
                    {
                        if (string.IsNullOrWhiteSpace(path))
                            return ValidationResult.Success(); // Allow empty to cancel
                        if (!Directory.Exists(path))
                            return ValidationResult.Error($"[red]Directory does not exist: {path}[/]");
                        return ValidationResult.Success();
                    }));

            // Check if user cancelled
            if (string.IsNullOrWhiteSpace(newFolderPath))
            {
                AnsiConsole.MarkupLine("[grey]Import cancelled.[/]");
                return null;
            }

            // Create or use progress tracker for this import
            var useExistingTracker = progressTracker != null &&
                newFolderPath.Equals(currentParameters.ImportFolder, StringComparison.OrdinalIgnoreCase);

            // If same folder and we have a tracker, check for resume
            var isResuming = false;
            List<string> zipFiles;

            if (useExistingTracker && progressTracker!.QueueFileExists(newFolderPath))
            {
                // Same folder with existing queue - offer to resume
                var tempParams = new ImportParameters { ImportFolder = newFolderPath, ConnectionString = currentParameters.ConnectionString };
                isResuming = await handleExistingQueueFileAsync(
                    tempParams,
                    progressTracker,
                    settings);

                if (isResuming)
                {
                    // Check if this was a "skip" signal (complete queue, user declined delete)
                    if (tempParams.ZipFiles != null && tempParams.ZipFiles.Count == 0)
                    {
                        // User declined to delete complete queue - return to post-import menu
                        return null;
                    }

                    zipFiles = progressTracker.GetPendingFiles();
                }
                else
                {
                    // User chose to start fresh - scan for files
                    zipFiles = scanForZipFiles(newFolderPath, settings.Display.ShowSpinners);
                }
            }
            else
            {
                // Different folder or no existing tracker - scan for ZIP files
                zipFiles = scanForZipFiles(newFolderPath, settings.Display.ShowSpinners);

                // Create a new progress tracker for this folder if different from original
                if (!useExistingTracker)
                {
                    progressTracker = new ImportProgressTracker();
                }
            }

            if (zipFiles.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No ZIP files found in: {Markup.Escape(newFolderPath)}[/]");
                return null;
            }

            // Create new parameters with the new folder but same connection
            var newParameters = new ImportParameters
            {
                ConnectionString = currentParameters.ConnectionString,
                ImportFolder = newFolderPath,
                MaxRuntimeMinutes = currentParameters.MaxRuntimeMinutes,
                ZipFiles = zipFiles,
                VerboseMode = currentParameters.VerboseMode
            };

            // Confirm import (skip if resuming - user already confirmed)
            if (!isResuming && settings.ImportSettings.RequireConfirmation && !ConfirmImport(newParameters))
            {
                AnsiConsole.MarkupLine("[grey]Import cancelled.[/]");
                return null;
            }

            // Execute import with progress tracking
            var importService = new Services.ImportService();
            var results = await importService.ExecuteImportAsync(newParameters, progressTracker, isResuming);

            // Show remaining items if any
            var progressFile = progressTracker?.GetProgressFile();
            if (progressFile != null && progressFile.RemainingItems > 0)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]{progressFile.RemainingItems} file(s) remaining. " +
                    "Use 'import' command again to continue.[/]");
            }

            return results;

            #endregion
        }

        #endregion

        #region private classes

        /**************************************************************/
        /// <summary>
        /// Internal class for database selection choices.
        /// </summary>
        private class DatabaseChoice
        {
            public string DisplayName { get; set; } = string.Empty;
            public string ConnectionString { get; set; } = string.Empty;
            public bool IsDefault { get; set; }
            public bool IsCustom { get; set; }
        }

        #endregion
    }
}
