namespace MedRecProConsole.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents parsed command-line arguments for the application.
    /// Supports both interactive and unattended operation modes.
    /// </summary>
    /// <remarks>
    /// Unattended mode is triggered when --folder or --orange-book is specified.
    /// In unattended mode, the application processes imports without user interaction
    /// and exits when complete (if --auto-quit or settings specify).
    /// </remarks>
    /// <example>
    /// Interactive mode (default):
    ///   MedRecProConsole.exe
    ///   MedRecProConsole.exe --help
    ///   MedRecProConsole.exe --verbose
    ///
    /// Unattended SPL mode (for Task Scheduler):
    ///   MedRecProConsole.exe --folder "C:\SPL\Imports"
    ///   MedRecProConsole.exe --folder "C:\SPL\Imports" --connection "Local Database Dev"
    ///   MedRecProConsole.exe --folder "C:\SPL\Imports" --time 120 --auto-quit
    ///   MedRecProConsole.exe --config "C:\Jobs\daily-import.json" --folder "C:\SPL\Imports"
    ///
    /// Orange Book mode:
    ///   MedRecProConsole.exe --orange-book "C:\OrangeBook\EOBZIP_2026_01.zip"
    ///   MedRecProConsole.exe --orange-book "C:\OrangeBook\EOBZIP_2026_01.zip" --nuke
    ///   MedRecProConsole.exe --orange-book "C:\OrangeBook\EOBZIP_2026_01.zip" --connection "Local Database Dev" --nuke
    ///
    /// Table Standardization with Stage 3.25 quality gate:
    ///   MedRecProConsole.exe --standardize-tables parse --drop-incomplete-rows
    ///   MedRecProConsole.exe --standardize-tables validate --drop-incomplete-rows --batch-size 500
    /// </example>
    /// <seealso cref="Helpers.HelpDocumentation"/>
    /// <seealso cref="ConsoleAppSettings"/>
    public class CommandLineArgs
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether help was requested (--help, -h).
        /// </summary>
        public bool ShowHelp { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether version info was requested (--version, -v).
        /// </summary>
        public bool ShowVersion { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether verbose mode is enabled (--verbose, -V).
        /// </summary>
        public bool VerboseMode { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether quiet mode is enabled (--quiet, -q).
        /// Suppresses non-essential console output.
        /// </summary>
        public bool QuietMode { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the import folder path (--folder).
        /// When set, enables unattended operation mode.
        /// </summary>
        public string? FolderPath { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the database connection name (--connection).
        /// Must match a name from DatabaseConnections in appsettings.json.
        /// </summary>
        public string? ConnectionName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the maximum runtime in minutes (--time).
        /// Valid range: 1-1440 (1 minute to 24 hours).
        /// </summary>
        public int? MaxRuntimeMinutes { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether to auto-quit after completion (--auto-quit).
        /// When true, overrides settings to exit immediately after import.
        /// </summary>
        public bool AutoQuit { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the alternate config file path (--config).
        /// </summary>
        public string? ConfigPath { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the Orange Book ZIP file path (--orange-book).
        /// When set, enables Orange Book import mode instead of SPL import.
        /// </summary>
        /// <seealso cref="IsOrangeBookMode"/>
        public string? OrangeBookZipPath { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether to truncate all Orange Book tables before import (--nuke).
        /// Only valid when <see cref="OrangeBookZipPath"/> is specified.
        /// </summary>
        /// <seealso cref="OrangeBookZipPath"/>
        public bool OrangeBookNuke { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the table standardization operation (--standardize-tables).
        /// Valid values: parse, validate, truncate, parse-single.
        /// </summary>
        /// <seealso cref="IsStandardizeTablesMode"/>
        public string? StandardizeTablesOperation { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the TextTableID for parse-single debug mode (--table-id).
        /// Only valid when <see cref="StandardizeTablesOperation"/> is "parse-single".
        /// </summary>
        /// <seealso cref="StandardizeTablesOperation"/>
        public int? StandardizeTableId { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets the batch size for standardization operations (--batch-size).
        /// Valid range: 1-50000. Default: 1000.
        /// </summary>
        public int? BatchSize { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether Claude AI correction (Stage 3.5) is disabled (--no-claude).
        /// Only valid with --standardize-tables operations.
        /// </summary>
        /// <seealso cref="StandardizeTablesOperation"/>
        public bool NoClaude { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets or sets whether rows missing ArmN or PrimaryValue are dropped
        /// at the conclusion of Stage 3.25 (--drop-incomplete-rows).
        /// Only valid with --standardize-tables parse or validate operations.
        /// </summary>
        /// <remarks>
        /// Enables the Stage 3.25 quality gate: observations where EITHER ArmN or
        /// PrimaryValue is null are removed before ML.NET / Claude / post-processing.
        /// Cross-product meta-analysis requires both fields populated, so any row
        /// missing either one is considered unrecoverable for downstream processing.
        ///
        /// When this flag is absent, the effective value falls back to
        /// <c>ConsoleAppSettings.Standardization.DropRowsMissingArmNOrPrimaryValue</c>
        /// (default false).
        /// </remarks>
        /// <seealso cref="StandardizeTablesOperation"/>
        public bool DropRowsMissingArmNOrPrimaryValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// Gets whether unattended SPL mode is enabled.
        /// True when --folder is specified.
        /// </summary>
        public bool IsUnattendedMode => !string.IsNullOrEmpty(FolderPath);

        /**************************************************************/
        /// <summary>
        /// Gets whether Orange Book import mode is enabled.
        /// True when --orange-book is specified.
        /// </summary>
        /// <seealso cref="OrangeBookZipPath"/>
        public bool IsOrangeBookMode => !string.IsNullOrEmpty(OrangeBookZipPath);

        /**************************************************************/
        /// <summary>
        /// Gets whether table standardization mode is enabled.
        /// True when --standardize-tables is specified.
        /// </summary>
        /// <seealso cref="StandardizeTablesOperation"/>
        public bool IsStandardizeTablesMode => !string.IsNullOrEmpty(StandardizeTablesOperation);

        /**************************************************************/
        /// <summary>
        /// Gets or sets any parsing errors encountered.
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Gets whether any parsing errors occurred.
        /// </summary>
        public bool HasErrors => Errors.Count > 0;

        #endregion

        #region static methods

        /**************************************************************/
        /// <summary>
        /// Parses command-line arguments into a CommandLineArgs instance.
        /// </summary>
        /// <param name="args">Raw command-line arguments</param>
        /// <returns>Parsed CommandLineArgs instance</returns>
        /// <remarks>
        /// Supports both GNU-style (--option) and short flags (-o).
        /// Arguments with values can use space or equals sign:
        ///   --folder C:\Path or --folder=C:\Path
        /// </remarks>
        public static CommandLineArgs Parse(string[] args)
        {
            #region implementation

            var result = new CommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                var lowerArg = arg.ToLowerInvariant();

                // Handle help flags
                if (lowerArg is "--help" or "-h" or "/h" or "/?")
                {
                    result.ShowHelp = true;
                    continue;
                }

                // Handle version flags
                if (lowerArg is "--version" or "-v" or "/v")
                {
                    result.ShowVersion = true;
                    continue;
                }

                // Handle verbose flag (case-sensitive for -V)
                if (arg is "--verbose" or "-V")
                {
                    result.VerboseMode = true;
                    continue;
                }

                // Handle quiet flag
                if (lowerArg is "--quiet" or "-q")
                {
                    result.QuietMode = true;
                    continue;
                }

                // Handle auto-quit flag
                if (lowerArg is "--auto-quit" or "--autoquit")
                {
                    result.AutoQuit = true;
                    continue;
                }

                // Handle folder argument
                if (lowerArg.StartsWith("--folder"))
                {
                    result.FolderPath = extractArgumentValue(args, ref i, arg, "--folder", result.Errors);
                    continue;
                }

                // Handle connection argument
                if (lowerArg.StartsWith("--connection"))
                {
                    result.ConnectionName = extractArgumentValue(args, ref i, arg, "--connection", result.Errors);
                    continue;
                }

                // Handle time argument
                if (lowerArg.StartsWith("--time"))
                {
                    var timeValue = extractArgumentValue(args, ref i, arg, "--time", result.Errors);
                    if (!string.IsNullOrEmpty(timeValue))
                    {
                        if (int.TryParse(timeValue, out var minutes))
                        {
                            if (minutes < 1 || minutes > 1440)
                            {
                                result.Errors.Add($"--time value must be between 1 and 1440 minutes: {timeValue}");
                            }
                            else
                            {
                                result.MaxRuntimeMinutes = minutes;
                            }
                        }
                        else
                        {
                            result.Errors.Add($"--time requires a numeric value: {timeValue}");
                        }
                    }
                    continue;
                }

                // Handle config argument
                if (lowerArg.StartsWith("--config"))
                {
                    result.ConfigPath = extractArgumentValue(args, ref i, arg, "--config", result.Errors);
                    continue;
                }

                // Handle orange-book argument
                if (lowerArg.StartsWith("--orange-book"))
                {
                    result.OrangeBookZipPath = extractArgumentValue(args, ref i, arg, "--orange-book", result.Errors);
                    continue;
                }

                // Handle nuke flag (truncate Orange Book tables before import)
                if (lowerArg is "--nuke")
                {
                    result.OrangeBookNuke = true;
                    continue;
                }

                // Handle standardize-tables argument
                if (lowerArg.StartsWith("--standardize-tables"))
                {
                    result.StandardizeTablesOperation = extractArgumentValue(args, ref i, arg, "--standardize-tables", result.Errors);
                    continue;
                }

                // Handle batch-size argument
                if (lowerArg.StartsWith("--batch-size"))
                {
                    var bsValue = extractArgumentValue(args, ref i, arg, "--batch-size", result.Errors);
                    if (!string.IsNullOrEmpty(bsValue))
                    {
                        if (int.TryParse(bsValue, out var bs))
                        {
                            if (bs < 1 || bs > 50000)
                            {
                                result.Errors.Add($"--batch-size value must be between 1 and 50000: {bsValue}");
                            }
                            else
                            {
                                result.BatchSize = bs;
                            }
                        }
                        else
                        {
                            result.Errors.Add($"--batch-size requires a numeric value: {bsValue}");
                        }
                    }
                    continue;
                }

                // Handle table-id argument (for parse-single debug mode)
                if (lowerArg.StartsWith("--table-id"))
                {
                    var tidValue = extractArgumentValue(args, ref i, arg, "--table-id", result.Errors);
                    if (!string.IsNullOrEmpty(tidValue))
                    {
                        if (int.TryParse(tidValue, out var tid))
                        {
                            result.StandardizeTableId = tid;
                        }
                        else
                        {
                            result.Errors.Add($"--table-id requires a numeric value: {tidValue}");
                        }
                    }
                    continue;
                }

                // Handle no-claude flag (disable Claude AI correction)
                if (lowerArg is "--no-claude")
                {
                    result.NoClaude = true;
                    continue;
                }

                // Handle drop-incomplete-rows flag (Stage 3.25 quality gate)
                if (lowerArg is "--drop-incomplete-rows")
                {
                    result.DropRowsMissingArmNOrPrimaryValue = true;
                    continue;
                }

                // Unknown argument
                if (arg.StartsWith("-") || arg.StartsWith("/"))
                {
                    result.Errors.Add($"Unknown argument: {arg}");
                }
            }

            // Validate folder path exists if specified
            if (!string.IsNullOrEmpty(result.FolderPath) && !Directory.Exists(result.FolderPath))
            {
                result.Errors.Add($"Folder does not exist: {result.FolderPath}");
            }

            // Validate config path exists if specified
            if (!string.IsNullOrEmpty(result.ConfigPath) && !File.Exists(result.ConfigPath))
            {
                result.Errors.Add($"Config file does not exist: {result.ConfigPath}");
            }

            // Validate Orange Book ZIP file exists if specified
            if (!string.IsNullOrEmpty(result.OrangeBookZipPath) && !File.Exists(result.OrangeBookZipPath))
            {
                result.Errors.Add($"Orange Book ZIP file does not exist: {result.OrangeBookZipPath}");
            }

            // Validate --orange-book and --folder are mutually exclusive
            if (!string.IsNullOrEmpty(result.OrangeBookZipPath) && !string.IsNullOrEmpty(result.FolderPath))
            {
                result.Errors.Add("Cannot use --orange-book and --folder together. Choose one import type.");
            }

            // Validate --nuke requires --orange-book
            if (result.OrangeBookNuke && string.IsNullOrEmpty(result.OrangeBookZipPath))
            {
                result.Errors.Add("--nuke can only be used with --orange-book.");
            }

            // Validate --standardize-tables operation value
            if (!string.IsNullOrEmpty(result.StandardizeTablesOperation))
            {
                var validOps = new[] { "parse", "validate", "truncate", "parse-single" };
                if (!validOps.Contains(result.StandardizeTablesOperation.ToLowerInvariant()))
                {
                    result.Errors.Add(
                        $"--standardize-tables operation must be one of: {string.Join(", ", validOps)}. Got: {result.StandardizeTablesOperation}");
                }
                else
                {
                    // Normalize to lowercase
                    result.StandardizeTablesOperation = result.StandardizeTablesOperation.ToLowerInvariant();
                }
            }

            // Validate --standardize-tables is mutually exclusive with --folder and --orange-book
            if (result.IsStandardizeTablesMode && result.IsUnattendedMode)
            {
                result.Errors.Add("Cannot use --standardize-tables and --folder together. Choose one operation.");
            }
            if (result.IsStandardizeTablesMode && result.IsOrangeBookMode)
            {
                result.Errors.Add("Cannot use --standardize-tables and --orange-book together. Choose one operation.");
            }

            // Validate --table-id requires --standardize-tables parse-single
            if (result.StandardizeTableId.HasValue && result.StandardizeTablesOperation != "parse-single")
            {
                result.Errors.Add("--table-id can only be used with --standardize-tables parse-single.");
            }

            // Validate --standardize-tables parse-single requires --table-id
            if (result.StandardizeTablesOperation == "parse-single" && !result.StandardizeTableId.HasValue)
            {
                result.Errors.Add("--standardize-tables parse-single requires --table-id <id>.");
            }

            // Validate --no-claude requires --standardize-tables
            if (result.NoClaude && !result.IsStandardizeTablesMode)
            {
                result.Errors.Add("--no-claude can only be used with --standardize-tables.");
            }

            // Validate --drop-incomplete-rows requires --standardize-tables (parse or validate only)
            if (result.DropRowsMissingArmNOrPrimaryValue && !result.IsStandardizeTablesMode)
            {
                result.Errors.Add("--drop-incomplete-rows can only be used with --standardize-tables.");
            }
            else if (result.DropRowsMissingArmNOrPrimaryValue
                && result.StandardizeTablesOperation is not ("parse" or "validate"))
            {
                result.Errors.Add("--drop-incomplete-rows can only be used with --standardize-tables parse or validate.");
            }

            // Validate --batch-size only valid with parse or validate
            if (result.BatchSize.HasValue && !string.IsNullOrEmpty(result.StandardizeTablesOperation))
            {
                if (result.StandardizeTablesOperation is not ("parse" or "validate"))
                {
                    result.Errors.Add("--batch-size can only be used with --standardize-tables parse or validate.");
                }
            }
            if (result.BatchSize.HasValue && string.IsNullOrEmpty(result.StandardizeTablesOperation))
            {
                result.Errors.Add("--batch-size requires --standardize-tables.");
            }

            return result;

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Extracts the value for an argument that requires a value.
        /// Supports both --arg=value and --arg value formats.
        /// </summary>
        /// <param name="args">Full args array</param>
        /// <param name="index">Current index (may be incremented)</param>
        /// <param name="currentArg">Current argument string</param>
        /// <param name="argName">Expected argument name (e.g., "--folder")</param>
        /// <param name="errors">Error list to add to if value is missing</param>
        /// <returns>The extracted value, or null if not found</returns>
        private static string? extractArgumentValue(string[] args, ref int index, string currentArg, string argName, List<string> errors)
        {
            #region implementation

            // Check for --arg=value format
            if (currentArg.Contains('='))
            {
                var parts = currentArg.Split('=', 2);
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    return parts[1].Trim('"', '\'');
                }
                else
                {
                    errors.Add($"{argName} requires a value");
                    return null;
                }
            }

            // Check for --arg value format
            if (index + 1 < args.Length && !args[index + 1].StartsWith("-"))
            {
                index++;
                return args[index].Trim('"', '\'');
            }

            errors.Add($"{argName} requires a value");
            return null;

            #endregion
        }

        #endregion
    }
}
