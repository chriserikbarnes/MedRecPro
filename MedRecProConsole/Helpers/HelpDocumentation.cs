using MedRecProConsole.Models;
using Spectre.Console;

namespace MedRecProConsole.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Helper class for displaying help documentation and version information.
    /// Renders help content from configuration using Spectre.Console styling.
    /// </summary>
    /// <remarks>
    /// Help content is loaded from appsettings.json to allow customization
    /// without code changes.
    /// </remarks>
    /// <seealso cref="ConsoleAppSettings"/>
    /// <seealso cref="HelpSettings"/>
    /// <seealso cref="ConfigurationHelper"/>
    public static class HelpDocumentation
    {
        #region public methods

        /**************************************************************/
        /// <summary>
        /// Displays the application version information.
        /// </summary>
        /// <param name="settings">Application settings containing version info</param>
        /// <seealso cref="ApplicationSettings"/>
        public static void DisplayVersion(ConsoleAppSettings settings)
        {
            #region implementation

            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(settings.Application.Name)}[/]");
            AnsiConsole.MarkupLine($"Version: [green]{Markup.Escape(settings.Application.Version)}[/]");
            AnsiConsole.MarkupLine($"{Markup.Escape(settings.Application.Description)}");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays the full help documentation including usage, topics, and options.
        /// </summary>
        /// <param name="settings">Application settings containing help content</param>
        /// <seealso cref="HelpSettings"/>
        /// <seealso cref="HelpTopic"/>
        /// <seealso cref="CommandLineOption"/>
        public static void DisplayHelp(ConsoleAppSettings settings)
        {
            #region implementation

            // Header
            AnsiConsole.Write(
                new FigletText(settings.Application.Name.Split(' ')[0])
                    .LeftJustified()
                    .Color(Color.Blue));

            AnsiConsole.MarkupLine($"[bold]{Markup.Escape(settings.Application.Name)}[/] v{Markup.Escape(settings.Application.Version)}");
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(settings.Application.Description)}[/]");
            AnsiConsole.WriteLine();

            // Usage section
            displayUsageSection();

            // Help topics
            if (settings.Help.Topics.Count > 0)
            {
                displayHelpTopics(settings.Help.Topics);
            }

            // Command line options
            if (settings.Help.CommandLineOptions.Count > 0)
            {
                displayCommandLineOptions(settings.Help.CommandLineOptions);
            }

            // Footer
            displayFooter();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if the command line arguments contain a help flag.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>True if help flag is present</returns>
        public static bool IsHelpRequested(string[] args)
        {
            #region implementation

            return args.Any(a =>
                a.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/h", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/?", StringComparison.OrdinalIgnoreCase));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if the command line arguments contain a version flag.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>True if version flag is present</returns>
        public static bool IsVersionRequested(string[] args)
        {
            #region implementation

            return args.Any(a =>
                a.Equals("--version", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-v", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/v", StringComparison.OrdinalIgnoreCase));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if the command line arguments contain a verbose flag.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>True if verbose flag is present</returns>
        public static bool IsVerboseRequested(string[] args)
        {
            #region implementation

            return args.Any(a =>
                a.Equals("--verbose", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-V", StringComparison.Ordinal));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays command-line argument errors in styled console output.
        /// </summary>
        /// <param name="errors">List of error messages</param>
        /// <seealso cref="Models.CommandLineArgs"/>
        public static void DisplayArgumentErrors(List<string> errors)
        {
            #region implementation

            AnsiConsole.MarkupLine("[bold red]Error parsing command-line arguments:[/]");
            AnsiConsole.WriteLine();

            foreach (var error in errors)
            {
                AnsiConsole.MarkupLine($"  [red]* {Markup.Escape(error)}[/]");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Use --help for usage information.[/]");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays information about unattended operation mode.
        /// </summary>
        /// <param name="folderPath">Import folder path</param>
        /// <param name="connectionName">Database connection name</param>
        /// <param name="maxRuntime">Max runtime in minutes, or null for no limit</param>
        /// <param name="autoQuit">Whether auto-quit is enabled</param>
        /// <seealso cref="Models.CommandLineArgs"/>
        public static void DisplayUnattendedModeInfo(
            string folderPath,
            string connectionName,
            int? maxRuntime,
            bool autoQuit)
        {
            #region implementation

            AnsiConsole.Write(new Rule("[bold cyan]Unattended Mode[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn(new TableColumn("[bold]Setting[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Value[/]"));

            table.AddRow("Import Folder", Markup.Escape(folderPath));
            table.AddRow("Database", Markup.Escape(connectionName));
            table.AddRow("Max Runtime", maxRuntime.HasValue ? $"{maxRuntime} minutes" : "No limit");
            table.AddRow("Auto-Quit", autoQuit ? "[green]Yes[/]" : "[grey]No[/]");

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Displays the usage section of the help documentation.
        /// </summary>
        private static void displayUsageSection()
        {
            #region implementation

            AnsiConsole.Write(new Rule("[bold yellow]Usage[/]").LeftJustified().RuleStyle("grey"));
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("  [white]MedRecProConsole[/] [grey][[options]][/]");
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("  [bold]Interactive Mode[/] (default):");
            AnsiConsole.MarkupLine("    [white]MedRecProConsole[/]");
            AnsiConsole.MarkupLine("    [grey]Run without arguments for menu-driven interface[/]");
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("  [bold]SPL Unattended Mode[/] (for Task Scheduler):");
            AnsiConsole.MarkupLine("    [white]MedRecProConsole[/] [green]--folder[/] [cyan]<path>[/]");
            AnsiConsole.MarkupLine("    [white]MedRecProConsole[/] [green]--folder[/] [cyan]<path>[/] [green]--connection[/] [cyan]<name>[/]");
            AnsiConsole.MarkupLine("    [white]MedRecProConsole[/] [green]--folder[/] [cyan]<path>[/] [green]--time[/] [cyan]120[/] [green]--auto-quit[/]");
            AnsiConsole.MarkupLine("    [white]MedRecProConsole[/] [green]--config[/] [cyan]<path>[/] [green]--folder[/] [cyan]<path>[/]");
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("  [bold]Orange Book Import[/]:");
            AnsiConsole.MarkupLine("    [white]MedRecProConsole[/] [orange1]--orange-book[/] [cyan]<zippath>[/]");
            AnsiConsole.MarkupLine("    [white]MedRecProConsole[/] [orange1]--orange-book[/] [cyan]<zippath>[/] [red]--nuke[/]");
            AnsiConsole.MarkupLine("    [white]MedRecProConsole[/] [orange1]--orange-book[/] [cyan]<zippath>[/] [green]--connection[/] [cyan]<name>[/] [red]--nuke[/]");
            AnsiConsole.MarkupLine("    [grey]Import Orange Book products.txt from ZIP file[/]");
            AnsiConsole.WriteLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays the help topics section.
        /// </summary>
        /// <param name="topics">List of help topics</param>
        /// <seealso cref="HelpTopic"/>
        private static void displayHelpTopics(List<HelpTopic> topics)
        {
            #region implementation

            AnsiConsole.Write(new Rule("[bold yellow]Topics[/]").LeftJustified().RuleStyle("grey"));
            AnsiConsole.WriteLine();

            foreach (var topic in topics)
            {
                AnsiConsole.MarkupLine($"  [bold green]{Markup.Escape(topic.Title)}[/]");
                AnsiConsole.MarkupLine($"    [grey]{Markup.Escape(topic.Description)}[/]");
                AnsiConsole.WriteLine();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays the command line options section.
        /// </summary>
        /// <param name="options">List of command line options</param>
        /// <seealso cref="CommandLineOption"/>
        private static void displayCommandLineOptions(List<CommandLineOption> options)
        {
            #region implementation

            AnsiConsole.Write(new Rule("[bold yellow]Options[/]").LeftJustified().RuleStyle("grey"));
            AnsiConsole.WriteLine();

            // Create a table for options
            var table = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .AddColumn(new TableColumn("Option").Width(25))
                .AddColumn(new TableColumn("Description"));

            foreach (var option in options)
            {
                // Check if this is a future feature
                var description = option.Description;
                if (description.Contains("(future)", StringComparison.OrdinalIgnoreCase))
                {
                    table.AddRow(
                        $"[grey]{Markup.Escape(option.Option)}[/]",
                        $"[grey]{Markup.Escape(description)}[/]");
                }
                else
                {
                    table.AddRow(
                        $"[green]{Markup.Escape(option.Option)}[/]",
                        Markup.Escape(description));
                }
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Displays the help footer with additional information.
        /// </summary>
        private static void displayFooter()
        {
            #region implementation

            AnsiConsole.Write(new Rule("[bold yellow]More Information[/]").LeftJustified().RuleStyle("grey"));
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("  [grey]Configuration:[/] appsettings.json");
            AnsiConsole.MarkupLine("  [grey]Documentation:[/] README.md");
            AnsiConsole.WriteLine();

            #endregion
        }

        #endregion
    }
}
