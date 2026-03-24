using System.Text.RegularExpressions;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for pharmacokinetic (PK) tables in Stage 3 of the SPL Table Normalization pipeline.
    /// PK tables have columns as parameters (Cmax, AUC, t½) and rows as dose regimens.
    /// </summary>
    /// <remarks>
    /// ## Table Structure
    /// - Header columns = PK parameters with optional units: "Cmax (mcg/mL)", "AUC (mcg·h/mL)"
    /// - Column 0 = dose regimen label (e.g., "50 mg oral (once daily x 7 days)")
    /// - Data cells typically use value(CV%) format
    ///
    /// ## Unpivot Pattern
    /// One observation per (row, paramColumn): a 5-parameter table with 3 dose rows
    /// produces ~15 observations.
    ///
    /// ## PrimaryValueType
    /// Defaults to "Mean" when ValueParser returns "Numeric" (PK values are means).
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="ValueParser"/>
    public class PkTableParser : BaseTableParser
    {
        // Pattern for extracting parameter name and unit from header: "Cmax (mcg/mL)"
        private static readonly Regex _paramUnitPattern = new(
            @"^(.+?)\s*\((.+?)\)\s*$",
            RegexOptions.Compiled);

        // Pattern for "x 7 days", "x 14 days", "x 4 weeks" — multiplier schedules
        private static readonly Regex _durationMultiplierPattern = new(
            @"x\s*(\d+)\s*(days?|weeks?|months?|hours?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern for "for 14 days", "for 4 weeks" — duration phrases
        private static readonly Regex _durationForPattern = new(
            @"for\s+(\d+)\s*(days?|weeks?|months?|hours?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern for single-dose regimens
        private static readonly Regex _singleDosePattern = new(
            @"\b(single)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports PK table category.
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.PK;

        /**************************************************************/
        /// <summary>
        /// Priority 10 — only PK parser for this category.
        /// </summary>
        public override int Priority => 10;

        /**************************************************************/
        /// <summary>
        /// Always returns true for PK-categorized tables.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True.</returns>
        public override bool CanParse(ReconstructedTable table) => true;

        /**************************************************************/
        /// <summary>
        /// Parses a PK table: header columns are parameters, data rows are dose regimens.
        /// Each data cell becomes one observation with DoseRegimen from row label.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            var observations = new List<ParsedObservation>();
            var (population, popConfidence) = detectPopulation(table);
            var captionHint = detectCaptionValueHint(table.Caption);

            // Extract parameter definitions from header (skip col 0 = dose label)
            var paramDefs = extractParameterDefinitions(table);
            if (paramDefs.Count == 0)
                return observations;

            // Iterate data rows
            var dataRows = getDataBodyRows(table);
            foreach (var row in dataRows)
            {
                // Column 0 = dose regimen label
                var doseCell = getCellAtColumn(row, 0);
                var doseRegimen = doseCell?.CleanedText?.Trim();

                // Skip empty dose label rows
                if (string.IsNullOrWhiteSpace(doseRegimen))
                    continue;

                // Extract duration from dose regimen text (once per row)
                var (time, timeUnit, timepoint) = extractDuration(doseRegimen);

                // Fault-tolerant row processing: if any cell throws, the entire table is skipped
                parseRowSafe(table, row, observations, (r, obs) =>
                {
                    // Each parameter column produces one observation
                    foreach (var param in paramDefs)
                    {
                        var cell = getCellAtColumn(r, param.columnIndex);
                        if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                            continue;

                        var o = createBaseObservation(table, r, cell, TableCategory.PK);
                        o.ParameterName = param.name;
                        o.DoseRegimen = doseRegimen;
                        o.Population = population;
                        o.Timepoint = timepoint;
                        o.Time = time;
                        o.TimeUnit = timeUnit;
                        o.Unit = param.unit;

                        // Parse value — PK cells often use value(CV%) format
                        var parsed = ValueParser.Parse(cell.CleanedText);

                        // Caption-based type inference (e.g., "Mean (SD)" reinterprets n_pct → mean_sd)
                        if (!captionHint.IsEmpty)
                        {
                            parsed = applyCaptionHint(parsed, captionHint);
                        }

                        // PK fallback: bare Numeric → Mean (only if caption didn't already set it)
                        if (parsed.PrimaryValueType == "Numeric")
                        {
                            parsed.PrimaryValueType = "Mean";
                            // Reduce confidence — fallback without caption confirmation
                            parsed.ParseConfidence = parsed.ParseConfidence * 0.8;
                        }

                        applyParsedValue(o, parsed);

                        // Unit from header takes precedence over parsed unit
                        if (!string.IsNullOrEmpty(param.unit))
                            o.Unit = param.unit;

                        obs.Add(o);
                    }
                });
            }

            return observations;

            #endregion
        }

        #endregion ITableParser Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Extracts parameter name and unit from header columns.
        /// Parses patterns like "Cmax (mcg/mL)" into name="Cmax", unit="mcg/mL".
        /// </summary>
        private static List<(int columnIndex, string name, string? unit)> extractParameterDefinitions(
            ReconstructedTable table)
        {
            #region implementation

            var defs = new List<(int columnIndex, string name, string? unit)>();
            if (table.Header?.Columns == null)
                return defs;

            // Skip column 0 (dose regimen label)
            for (int i = 1; i < table.Header.Columns.Count; i++)
            {
                var col = table.Header.Columns[i];
                var text = col.LeafHeaderText?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var match = _paramUnitPattern.Match(text);
                if (match.Success)
                {
                    defs.Add((col.ColumnIndex ?? i, match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim()));
                }
                else
                {
                    defs.Add((col.ColumnIndex ?? i, text, null));
                }
            }

            return defs;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the dosing duration from a dose regimen string.
        /// Recognizes "x N days/weeks/months", "for N days/weeks", and "single" dose patterns.
        /// </summary>
        /// <param name="doseRegimen">Dose regimen text (e.g., "50 mg oral (once daily x 7 days)").</param>
        /// <returns>
        /// Tuple of (time, timeUnit, timepoint):
        /// - time: numeric duration value, or null for single/unrecognized doses
        /// - timeUnit: normalized unit ("days", "weeks", "months", "hours"), or null
        /// - timepoint: human-readable label ("7 days", "single dose"), or null if unrecognized
        /// </returns>
        /// <example>
        /// <code>
        /// extractDuration("50 mg oral (once daily x 7 days)")  → (7, "days", "7 days")
        /// extractDuration("150 mg single oral")                → (null, null, "single dose")
        /// extractDuration("400 mg IV (once weekly x 4 weeks)") → (4, "weeks", "4 weeks")
        /// extractDuration("unknown format")                    → (null, null, null)
        /// </code>
        /// </example>
        internal static (double? time, string? timeUnit, string? timepoint) extractDuration(string? doseRegimen)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(doseRegimen))
                return (null, null, null);

            // Try "x N days/weeks/months/hours" pattern first (most common in PK tables)
            var match = _durationMultiplierPattern.Match(doseRegimen);
            if (match.Success)
            {
                var value = double.Parse(match.Groups[1].Value);
                var unit = normalizeTimeUnit(match.Groups[2].Value);
                return (value, unit, $"{(int)value} {unit}");
            }

            // Try "for N days/weeks/months" pattern
            match = _durationForPattern.Match(doseRegimen);
            if (match.Success)
            {
                var value = double.Parse(match.Groups[1].Value);
                var unit = normalizeTimeUnit(match.Groups[2].Value);
                return (value, unit, $"{(int)value} {unit}");
            }

            // Check for single-dose pattern
            if (_singleDosePattern.IsMatch(doseRegimen))
            {
                return (null, null, "single dose");
            }

            return (null, null, null);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes time unit strings to plural lowercase form.
        /// </summary>
        /// <example>
        /// <code>
        /// normalizeTimeUnit("day")   → "days"
        /// normalizeTimeUnit("Week")  → "weeks"
        /// normalizeTimeUnit("months") → "months"
        /// </code>
        /// </example>
        private static string normalizeTimeUnit(string unit)
        {
            #region implementation

            var lower = unit.ToLowerInvariant().TrimEnd('s');
            return lower + "s";

            #endregion
        }

        #endregion Private Helpers
    }
}
