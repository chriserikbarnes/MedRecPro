using System.Text.RegularExpressions;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for bone mineral density (BMD) tables where columns represent timepoints.
    /// Column 0 = anatomical site, data columns = percent change at each timepoint.
    /// </summary>
    /// <remarks>
    /// ## Table Structure
    /// - Header columns = timepoints: "12 Months", "24 Months", "36 Months"
    /// - Column 0 = anatomical site: "Lumbar Spine", "Femoral Neck"
    /// - Data values = mean percent change from baseline
    ///
    /// ## PrimaryValueType
    /// All values default to "MeanPercentChange" with Unit = "%".
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    public class BmdTableParser : BaseTableParser
    {
        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports BMD category.
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.BMD;

        /**************************************************************/
        /// <summary>
        /// Priority 10 — only BMD parser.
        /// </summary>
        public override int Priority => 10;

        /**************************************************************/
        /// <summary>
        /// Always returns true for BMD-categorized tables.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True.</returns>
        public override bool CanParse(ReconstructedTable table) => true;

        /**************************************************************/
        /// <summary>
        /// Parses a BMD table: columns are timepoints, each data cell becomes one
        /// observation with Timepoint from header and ParameterName from row label.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            var observations = new List<ParsedObservation>();
            var (population, popConfidence) = detectPopulation(table);

            // Extract timepoints from header columns (skip col 0 = site label)
            var timepoints = extractTimepoints(table);
            if (timepoints.Count == 0)
                return observations;

            // Iterate data rows
            var dataRows = getDataBodyRows(table);
            foreach (var row in dataRows)
            {
                if (row.Classification == RowClassification.SocDivider)
                    continue;

                var (siteName, fnMarkers) = getParameterName(row);
                if (string.IsNullOrWhiteSpace(siteName))
                    continue;

                // Fault-tolerant row processing: if any cell throws, the entire table is skipped
                parseRowSafe(table, row, observations, (r, obs) =>
                {
                    foreach (var tp in timepoints)
                    {
                        var cell = getCellAtColumn(r, tp.columnIndex);
                        if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                            continue;

                        var o = createBaseObservation(table, r, cell, TableCategory.BMD);
                        o.ParameterName = siteName;
                        o.Timepoint = tp.label;
                        o.Population = population;

                        // Extract numeric time from timepoint label
                        var (time, timeUnit) = parseTimepointNumeric(tp.label);
                        o.Time = time;
                        o.TimeUnit = timeUnit;
                        o.Unit = "%";

                        var parsed = ValueParser.Parse(cell.CleanedText);

                        // BMD-specific: default type is MeanPercentChange
                        if (parsed.PrimaryValueType == "Numeric" || parsed.PrimaryValueType == "Percentage")
                        {
                            parsed.PrimaryValueType = "MeanPercentChange";
                        }

                        applyParsedValue(o, parsed);
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
        /// Extracts timepoint labels from header columns.
        /// </summary>
        private static List<(int columnIndex, string label)> extractTimepoints(ReconstructedTable table)
        {
            #region implementation

            var timepoints = new List<(int columnIndex, string label)>();
            if (table.Header?.Columns == null)
                return timepoints;

            // Skip column 0 (site label)
            for (int i = 1; i < table.Header.Columns.Count; i++)
            {
                var col = table.Header.Columns[i];
                var text = col.LeafHeaderText?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // Clean trailing % from timepoint labels
                var cleaned = text.TrimEnd('%').Trim();
                timepoints.Add((col.ColumnIndex ?? i, cleaned));
            }

            return timepoints;

            #endregion
        }

        // Pattern for "12 Months", "24 Months", "36 Months" — number then unit
        private static readonly Regex _numUnitPattern = new(
            @"^(\d+(?:\.\d+)?)\s*(months?|weeks?|days?|hours?|years?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern for "Week 12", "Day 49", "Month 6" — unit then number
        private static readonly Regex _unitNumPattern = new(
            @"^(months?|weeks?|days?|hours?|years?)\s*(\d+(?:\.\d+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Extracts a numeric time value and unit from a timepoint label string.
        /// Handles both "12 Months" and "Week 12" patterns.
        /// </summary>
        /// <param name="label">Timepoint label (e.g., "12 Months", "Week 12", "Day 49").</param>
        /// <returns>Tuple of (time, timeUnit), both null if pattern not recognized.</returns>
        /// <example>
        /// <code>
        /// parseTimepointNumeric("12 Months") → (12, "months")
        /// parseTimepointNumeric("Week 12")   → (12, "weeks")
        /// parseTimepointNumeric("Day 49")    → (49, "days")
        /// </code>
        /// </example>
        internal static (double? time, string? timeUnit) parseTimepointNumeric(string? label)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(label))
                return (null, null);

            // Try "12 Months" pattern first (most common in BMD tables)
            var match = _numUnitPattern.Match(label);
            if (match.Success)
            {
                var value = double.Parse(match.Groups[1].Value);
                var unit = normalizeTimeUnit(match.Groups[2].Value);
                return (value, unit);
            }

            // Try "Week 12" pattern
            match = _unitNumPattern.Match(label);
            if (match.Success)
            {
                var value = double.Parse(match.Groups[2].Value);
                var unit = normalizeTimeUnit(match.Groups[1].Value);
                return (value, unit);
            }

            return (null, null);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes time unit strings to plural lowercase form.
        /// </summary>
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
