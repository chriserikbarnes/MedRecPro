using MedRecProConsole.Models;
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
        /// </summary>
        /// <param name="settings">Application settings for database connections and defaults</param>
        /// <returns>ImportParameters object containing user selections, or null if cancelled</returns>
        /// <seealso cref="ImportParameters"/>
        /// <seealso cref="DatabaseConnectionSettings"/>
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
        /// <returns>Task representing the async operation</returns>
        /// <seealso cref="ConsoleAppSettings"/>
        /// <seealso cref="ImportResults"/>
        public static async Task RunPostImportMenuAsync(ConsoleAppSettings settings, ImportResults results, ImportParameters? currentParameters = null)
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
                            var additionalResults = await runAdditionalImportAsync(settings, currentParameters);
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
        /// </summary>
        /// <param name="settings">Application settings</param>
        /// <param name="currentParameters">Current import parameters containing database connection</param>
        /// <returns>Import results, or null if cancelled</returns>
        /// <seealso cref="ImportParameters"/>
        /// <seealso cref="ImportResults"/>
        private static async Task<ImportResults?> runAdditionalImportAsync(ConsoleAppSettings settings, ImportParameters currentParameters)
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

            // Scan for ZIP files
            var zipFiles = scanForZipFiles(newFolderPath, settings.Display.ShowSpinners);

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

            // Confirm import
            if (settings.ImportSettings.RequireConfirmation && !ConfirmImport(newParameters))
            {
                AnsiConsole.MarkupLine("[grey]Import cancelled.[/]");
                return null;
            }

            // Execute import
            var importService = new Services.ImportService();
            var results = await importService.ExecuteImportAsync(newParameters);

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
