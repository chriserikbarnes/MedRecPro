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

        /**************************************************************/
        /// <summary>
        /// Known pure time-unit strings. When a column header's unit matches one of these,
        /// the parameter is a time measurement (e.g., Half-life, Tmax) and its PrimaryValue
        /// should override the row-derived Time/TimeUnit.
        /// </summary>
        /// <remarks>
        /// Composite units like "mcg·h/mL" are NOT matched — only pure time units.
        /// </remarks>
        private static readonly HashSet<string> _timeUnitStrings = new(StringComparer.OrdinalIgnoreCase)
        {
            "hours", "hrs", "hr", "h",
            "minutes", "min",
            "seconds", "sec",
            "days", "weeks", "months"
        };

        /**************************************************************/
        /// <summary>
        /// Sample size column names. When a column header matches one of these exactly,
        /// the column contains sample sizes (counts), not PK parameter measurements.
        /// </summary>
        private static readonly HashSet<string> _sampleSizeHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "n", "N", "n=", "sample size"
        };

        /**************************************************************/
        /// <summary>
        /// Column 0 header keywords indicating a population descriptor rather than dose regimen.
        /// When column 0 header contains one of these, row labels are treated as Population
        /// instead of DoseRegimen.
        /// </summary>
        private static readonly HashSet<string> _populationHeaderKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "age group", "age", "population", "patient group", "subgroup", "cohort",
            "volunteers", "subjects"
        };

        /**************************************************************/
        /// <summary>
        /// Column 1 header keywords indicating a dedicated dose/route column (not a PK parameter).
        /// When column 1 header matches, the table uses a two-column context layout:
        /// col 0 = category/subtype, col 1 = dose regimen, cols 2+ = PK parameters.
        /// </summary>
        private static readonly HashSet<string> _doseColumnHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Dose/Route", "Dose / Route", "Dose", "Regimen", "Dose Regimen", "Dosage", "Route"
        };

        /**************************************************************/
        /// <summary>
        /// Regex patterns for ± dispersion type resolution. Scanned against header paths,
        /// footnotes, and caption text to determine what the ± symbol represents.
        /// Ordered by specificity: full phrase before abbreviation.
        /// </summary>
        private static readonly (Regex pattern, string secondaryValueType, string boundType)[] _dispersionKeywords =
        {
            (new Regex(@"\bstandard\s+deviation\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "SD", "SD"),
            (new Regex(@"\bS\.?D\.?\b", RegexOptions.Compiled), "SD", "SD"),
            (new Regex(@"\bstandard\s+error\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "SE", "SE"),
            (new Regex(@"\bS\.?E\.?M?\.?\b", RegexOptions.Compiled), "SE", "SE"),
            (new Regex(@"\bconfidence\s+interval\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "CI", "CI"),
            (new Regex(@"\b\d+\s*%\s*CI\b", RegexOptions.Compiled | RegexOptions.IgnoreCase), "CI", "CI"),
        };

        /**************************************************************/
        /// <summary>
        /// Pattern for extracting all parenthetical groups from compound headers
        /// like "AUC(0-96h)(mcgh/mL)". Used by <see cref="parseCompoundParameterHeader"/>
        /// to separate subtype qualifiers from units.
        /// </summary>
        private static readonly Regex _allParentheticalsPattern = new(
            @"\(([^)]+)\)",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Pattern for stripping common PK category prefixes from spanning header
        /// and SocDivider text. Matches "Pharmacokinetic Parameters for/in/of".
        /// </summary>
        private static readonly Regex _pkCategoryPrefixPattern = new(
            @"^Pharmacokinetic\s+Parameters?\s+(?:for|in|of)\s+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Pattern for extracting sample size (n=X) from treatment arm row labels.
        /// Matches "(n=6)", "(n = 18)", "(n=1,234)" with optional whitespace and commas.
        /// </summary>
        private static readonly Regex _armNFromLabelPattern = new(
            @"\(\s*n\s*=\s*(\d[\d,]*)\s*\)",
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

            // Detect compound header layout (spanning header + embedded sub-headers + SocDividers)
            if (detectCompoundHeaderLayout(table))
                return parseCompoundLayout(table, observations, population, captionHint);

            // Detect whether column 1 is a dedicated dose column (two-column context layout)
            var (doseColumnIndex, paramStartColumn) = detectDoseColumn(table);
            var hasDoseColumn = doseColumnIndex >= 0;

            // Extract parameter definitions from header
            var paramDefs = extractParameterDefinitions(table, paramStartColumn);
            if (paramDefs.Count == 0)
                return observations;

            // Detect whether column 0 is a population descriptor (single-column layout only)
            var col0IsPopulation = !hasDoseColumn && isColumn0Population(table);

            // Two-column layout state: category header tracking
            string? currentCategory = null;
            if (hasDoseColumn)
            {
                // Col 0 header text is the initial category (e.g., "Healthy Volunteers")
                currentCategory = table.Header?.Columns?.Count > 0
                    ? table.Header.Columns[0].LeafHeaderText?.Trim()
                    : null;
            }

            // Iterate data rows
            var dataRows = getDataBodyRows(table);
            foreach (var row in dataRows)
            {
                // Column 0 text (always read for both layouts)
                var col0Cell = getCellAtColumn(row, 0);
                var col0Text = col0Cell?.CleanedText?.Trim();

                // Skip empty label rows
                if (string.IsNullOrWhiteSpace(col0Text))
                    continue;

                // --- Two-column context layout ---
                if (hasDoseColumn)
                {
                    // Check for sub-header row (category divider echoing column headers)
                    var categoryFromSubHeader = detectSubHeaderRow(row, paramDefs, doseColumnIndex);
                    if (categoryFromSubHeader != null)
                    {
                        currentCategory = categoryFromSubHeader;
                        continue;
                    }

                    // Data row: col 0 = subtype, col doseColumnIndex = dose regimen
                    var doseCell = getCellAtColumn(row, doseColumnIndex);
                    var doseRegimen = doseCell?.CleanedText?.Trim();
                    var (time, timeUnit, timepoint) = extractDuration(doseRegimen);

                    parseRowSafe(table, row, observations, (r, obs) =>
                    {
                        foreach (var param in paramDefs)
                        {
                            var cell = getCellAtColumn(r, param.columnIndex);
                            if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                                continue;

                            var o = createBaseObservation(table, r, cell, TableCategory.PK);
                            o.ParameterName = param.name;
                            o.ParameterCategory = currentCategory;
                            o.ParameterSubtype = col0Text;
                            o.DoseRegimen = doseRegimen;
                            o.Population = population;
                            o.Timepoint = timepoint;
                            o.Time = time;
                            o.TimeUnit = timeUnit;
                            o.Unit = param.unit;

                            parseAndApplyPkValue(table, o, cell, param, captionHint);

                            obs.Add(o);
                        }
                    });
                }
                // --- Standard single-column layout (existing behavior) ---
                else
                {
                    // Determine DoseRegimen vs Population based on column 0 header
                    var doseRegimen = col0IsPopulation ? null : col0Text;
                    var rowPopulation = col0IsPopulation ? col0Text : population;
                    var (time, timeUnit, timepoint) = extractDuration(doseRegimen);

                    parseRowSafe(table, row, observations, (r, obs) =>
                    {
                        foreach (var param in paramDefs)
                        {
                            var cell = getCellAtColumn(r, param.columnIndex);
                            if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                                continue;

                            var o = createBaseObservation(table, r, cell, TableCategory.PK);
                            o.ParameterName = param.name;
                            o.DoseRegimen = doseRegimen;
                            o.Population = rowPopulation;
                            o.Timepoint = timepoint;
                            o.Time = time;
                            o.TimeUnit = timeUnit;
                            o.Unit = param.unit;

                            parseAndApplyPkValue(table, o, cell, param, captionHint);

                            obs.Add(o);
                        }
                    });
                }
            }

            // Post-parse: refine generic "CI" bound type from table context
            // (footer rows, spanning data rows that contain "N% CI" text)
            if (observations.Any(o => o.BoundType == "CI"))
            {
                var ciLevel = detectCILevelFromTableText(table);
                if (ciLevel != null)
                {
                    foreach (var o in observations.Where(o => o.BoundType == "CI"))
                        o.BoundType = ciLevel;
                }
            }

            return observations;

            #endregion
        }

        #endregion ITableParser Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Shared value parsing logic for PK cells. Handles ValueParser dispatch, caption hint
        /// application, ± dispersion type resolution, PK fallback (Numeric → Mean), and time
        /// measurement override. Used by both single-column and two-column layout paths.
        /// </summary>
        /// <param name="table">Table for footnote/header context in dispersion resolution.</param>
        /// <param name="o">Target observation to populate.</param>
        /// <param name="cell">Source cell with text to parse.</param>
        /// <param name="param">Parameter definition (column index, name, unit, flags).</param>
        /// <param name="captionHint">Caption-derived value type hint.</param>
        private static void parseAndApplyPkValue(
            ReconstructedTable table,
            ParsedObservation o,
            ProcessedCell cell,
            (int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize) param,
            CaptionValueHint captionHint)
        {
            #region implementation

            // Parse value — PK cells use value(CV%), value (±SD) (n=X), plain numbers, etc.
            var parsed = ValueParser.Parse(cell.CleanedText);

            // Sample size column: override Numeric → Count
            if (param.isSampleSize && parsed.PrimaryValueType == "Numeric")
            {
                parsed.PrimaryValueType = "Count";
                parsed.ParseConfidence = 0.90;
            }

            // Caption-based type inference (e.g., "Mean (SD)" reinterprets n_pct → mean_sd)
            if (!captionHint.IsEmpty)
            {
                parsed = applyCaptionHint(parsed, captionHint);
            }

            // PK fallback: bare Numeric → Mean (only if caption didn't already set it)
            // Skip for Count (sample size) — should not be promoted to Mean
            if (parsed.PrimaryValueType == "Numeric")
            {
                parsed.PrimaryValueType = "Mean";
                // Reduce confidence — fallback without caption confirmation
                parsed.ParseConfidence = parsed.ParseConfidence * 0.8;
            }

            applyParsedValue(o, parsed);

            // Resolve ± dispersion type from context when SecondaryValueType is unresolved
            if (o.SecondaryValue.HasValue && string.IsNullOrEmpty(o.SecondaryValueType)
                && parsed.ParseRule == "value_plusminus_sample")
            {
                var (svt, bt, flag) = resolveDispersionType(table, o, param.columnIndex);
                o.SecondaryValueType = svt;
                o.BoundType = bt;
                if (flag != null)
                    o.ValidationFlags = appendFlag(o.ValidationFlags, flag);
            }

            // Unit from header takes precedence over parsed unit
            if (!string.IsNullOrEmpty(param.unit))
                o.Unit = param.unit;

            // Column-derived time: when the parameter IS a time measurement
            // (e.g., Half-life, Tmax), override Time/TimeUnit with the measured value
            if (param.isTimeMeasure && o.PrimaryValue.HasValue)
            {
                o.Time = o.PrimaryValue;
                o.TimeUnit = normalizeTimeUnit(param.unit ?? "hours");
            }

            #endregion
        }

        // Pattern for detecting CI level in table text: "90% CI", "95% CI"
        private static readonly Regex _ciLevelPattern = new(
            @"(\d+)\s*%\s*CI\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Scans table footer rows, caption, and spanning data rows for CI level indicators
        /// (e.g., "90% CI", "95% CI"). Returns the BoundType string ("90CI", "95CI") or null.
        /// </summary>
        /// <remarks>
        /// Drug interaction PK tables often specify the CI level in a footer or spanning
        /// annotation row rather than in the caption. This method searches all non-header
        /// row text to find the CI specification.
        /// </remarks>
        /// <param name="table">The reconstructed table to scan.</param>
        /// <returns>"90CI", "95CI", or null if no CI level found.</returns>
        private static string? detectCILevelFromTableText(ReconstructedTable table)
        {
            #region implementation

            // Check caption first
            if (!string.IsNullOrWhiteSpace(table.Caption))
            {
                var captionMatch = _ciLevelPattern.Match(table.Caption);
                if (captionMatch.Success)
                    return $"{captionMatch.Groups[1].Value}CI";
            }

            // Check footer rows and data body rows for CI level text
            foreach (var row in table.Rows ?? Enumerable.Empty<ReconstructedRow>())
            {
                if (row.Classification != RowClassification.Footer &&
                    row.Classification != RowClassification.DataBody)
                    continue;

                foreach (var cell in row.Cells ?? Enumerable.Empty<ProcessedCell>())
                {
                    if (string.IsNullOrWhiteSpace(cell.CleanedText))
                        continue;

                    var match = _ciLevelPattern.Match(cell.CleanedText);
                    if (match.Success)
                        return $"{match.Groups[1].Value}CI";
                }
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts parameter name, unit, time-measure flag, and sample-size flag from header columns.
        /// Parses patterns like "Cmax (mcg/mL)" into structured definitions.
        /// </summary>
        /// <example>
        /// <code>
        /// "Cmax (mcg/mL)"      → ("Cmax", "mcg/mL", false, false)
        /// "Half-life (hours)"   → ("Half-life", "hours", true, false)
        /// "n"                   → ("n", null, false, true)
        /// </code>
        /// </example>
        private static List<(int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize)> extractParameterDefinitions(
            ReconstructedTable table, int paramStartColumn = 1)
        {
            #region implementation

            var defs = new List<(int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize)>();
            if (table.Header?.Columns == null)
                return defs;

            // Skip non-parameter columns (col 0 = label, optionally col 1 = dose)
            for (int i = paramStartColumn; i < table.Header.Columns.Count; i++)
            {
                var col = table.Header.Columns[i];
                var text = col.LeafHeaderText?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // Check if column is a sample size column
                var isSampleSize = _sampleSizeHeaders.Contains(text);

                var match = _paramUnitPattern.Match(text);
                if (match.Success)
                {
                    var name = match.Groups[1].Value.Trim();
                    var unit = match.Groups[2].Value.Trim();
                    var isTime = _timeUnitStrings.Contains(unit);
                    defs.Add((col.ColumnIndex ?? i, name, unit, isTime, isSampleSize));
                }
                else
                {
                    defs.Add((col.ColumnIndex ?? i, text, null, false, isSampleSize));
                }
            }

            return defs;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks whether column 0 header text indicates a population descriptor
        /// (e.g., "Age Group (y)", "Population") rather than a dose regimen.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True when column 0 contains population-related keywords.</returns>
        private static bool isColumn0Population(ReconstructedTable table)
        {
            #region implementation

            if (table.Header?.Columns == null || table.Header.Columns.Count == 0)
                return false;

            var col0Text = table.Header.Columns[0].LeafHeaderText?.Trim();
            if (string.IsNullOrWhiteSpace(col0Text))
                return false;

            // Check if the header contains any population keyword
            foreach (var keyword in _populationHeaderKeywords)
            {
                if (col0Text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;

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
        /// Normalizes time unit strings to plural lowercase canonical form.
        /// Handles abbreviations (hrs → hours, min → minutes, sec → seconds).
        /// </summary>
        /// <example>
        /// <code>
        /// normalizeTimeUnit("day")    → "days"
        /// normalizeTimeUnit("Week")   → "weeks"
        /// normalizeTimeUnit("months") → "months"
        /// normalizeTimeUnit("hrs")    → "hours"
        /// normalizeTimeUnit("hr")     → "hours"
        /// normalizeTimeUnit("h")      → "hours"
        /// normalizeTimeUnit("min")    → "minutes"
        /// normalizeTimeUnit("sec")    → "seconds"
        /// </code>
        /// </example>
        internal static string normalizeTimeUnit(string unit)
        {
            #region implementation

            var lower = unit.ToLowerInvariant().TrimEnd('s');

            // Map abbreviations to canonical forms
            return lower switch
            {
                "hr" or "h" => "hours",
                "hour" => "hours",
                "min" => "minutes",
                "minute" => "minutes",
                "sec" => "seconds",
                "second" => "seconds",
                "day" => "days",
                "week" => "weeks",
                "month" => "months",
                "year" => "years",
                _ => lower + "s"
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects whether the table has a dedicated dose column (e.g., "Dose/Route") at
        /// column 1, separate from the context/label column at column 0. When detected,
        /// PK parameters start at column 2 instead of column 1.
        /// </summary>
        /// <remarks>
        /// Guard: if column 1 header matches the parameter unit pattern (e.g., "Dose (mg)"),
        /// it is a numeric parameter column, NOT a categorical dose column.
        /// </remarks>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>
        /// Tuple of (doseColumnIndex, paramStartColumn):
        /// - (1, 2) when column 1 is a dose column → parameters start at col 2
        /// - (-1, 1) when no dose column → existing behavior, parameters start at col 1
        /// </returns>
        private static (int doseColumnIndex, int paramStartColumn) detectDoseColumn(ReconstructedTable table)
        {
            #region implementation

            if (table.Header?.Columns == null || table.Header.Columns.Count < 3)
                return (-1, 1);

            var col1Text = table.Header.Columns.Count > 1
                ? table.Header.Columns[1].LeafHeaderText?.Trim()
                : null;

            if (string.IsNullOrWhiteSpace(col1Text))
                return (-1, 1);

            // Guard: if col 1 header has a parenthesized unit, it's a parameter (e.g., "Dose (mg)")
            if (_paramUnitPattern.IsMatch(col1Text))
                return (-1, 1);

            // Check if col 1 header matches a dose column keyword
            if (_doseColumnHeaders.Contains(col1Text))
                return (1, 2);

            return (-1, 1);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects whether a data row is a sub-header row that re-states column headers
        /// (e.g., "Kidney Transplant Patients... | Dose/Route | Tmax(h) | Cmax(mcg/mL) | ...").
        /// These rows serve as category dividers within the table.
        /// </summary>
        /// <remarks>
        /// Detection requires TWO signals to avoid false positives:
        /// 1. The dose column cell echoes the dose column header (contains "dose" or "route")
        /// 2. At least 50% of parameter columns echo their header names
        /// </remarks>
        /// <param name="row">The data row to evaluate.</param>
        /// <param name="paramDefs">Parameter definitions from header.</param>
        /// <param name="doseColumnIndex">Index of the dose column (1 for two-column layout).</param>
        /// <returns>Col 0 text as the category name if this is a sub-header row, null otherwise.</returns>
        private static string? detectSubHeaderRow(
            ReconstructedRow row,
            List<(int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize)> paramDefs,
            int doseColumnIndex)
        {
            #region implementation

            if (paramDefs.Count == 0)
                return null;

            // Signal 1: dose column cell echoes "dose" or "route"
            var doseCell = getCellAtColumn(row, doseColumnIndex);
            var doseCellText = doseCell?.CleanedText?.Trim();
            if (string.IsNullOrWhiteSpace(doseCellText))
                return null;

            var doseEcho = doseCellText.Contains("dose", StringComparison.OrdinalIgnoreCase) ||
                           doseCellText.Contains("route", StringComparison.OrdinalIgnoreCase);
            if (!doseEcho)
                return null;

            // Signal 2: >= 50% of parameter columns echo their header name
            int echoCount = 0;
            foreach (var param in paramDefs)
            {
                var cell = getCellAtColumn(row, param.columnIndex);
                var cellText = cell?.CleanedText?.Trim();
                if (string.IsNullOrWhiteSpace(cellText))
                    continue;

                // Match if cell starts with the parameter name (case-insensitive)
                // Handles "Tmax(h)" matching param "Tmax", "Cmax (mcg/mL)" matching "Cmax"
                if (cellText.StartsWith(param.name, StringComparison.OrdinalIgnoreCase))
                    echoCount++;
            }

            if (echoCount < (paramDefs.Count + 1) / 2) // Ceiling division: >= 50%
                return null;

            // Both signals confirmed — return col 0 text as the category name
            var col0Cell = getCellAtColumn(row, 0);
            return col0Cell?.CleanedText?.Trim();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves the dispersion type for ± values when <see cref="ParsedObservation.SecondaryValueType"/>
        /// is null after ValueParser and caption hint processing. Checks header path, observation
        /// footnotes, and table-wide footnotes before falling back to SD with validation flag.
        /// </summary>
        /// <remarks>
        /// Resolution priority chain (first match wins):
        /// 1. Column header path text (multi-level headers may contain "Mean ± SD")
        /// 2. Observation's resolved footnote text
        /// 3. Table-wide footnotes (any footnote value)
        /// 4. Default: SD with <c>PLUSMINUS_TYPE_INFERRED:SD</c> flag
        /// </remarks>
        /// <param name="table">Table for footnote access.</param>
        /// <param name="obs">Observation for footnote text access.</param>
        /// <param name="paramColumnIndex">Column index of the parameter for header path lookup.</param>
        /// <returns>Tuple of (secondaryValueType, boundType, validationFlag or null).</returns>
        private static (string secondaryValueType, string boundType, string? flag) resolveDispersionType(
            ReconstructedTable table,
            ParsedObservation obs,
            int paramColumnIndex)
        {
            #region implementation

            // Source 1: Column header path (multi-level headers)
            if (table.Header?.Columns != null)
            {
                var headerCol = table.Header.Columns.FirstOrDefault(c =>
                    c.ColumnIndex == paramColumnIndex);
                if (headerCol?.CombinedHeaderText != null)
                {
                    var resolved = matchDispersionKeywords(headerCol.CombinedHeaderText);
                    if (resolved != null)
                        return (resolved.Value.svt, resolved.Value.bt, null);
                }
            }

            // Source 2: Observation's resolved footnote text
            if (!string.IsNullOrWhiteSpace(obs.FootnoteText))
            {
                var resolved = matchDispersionKeywords(obs.FootnoteText);
                if (resolved != null)
                    return (resolved.Value.svt, resolved.Value.bt, null);
            }

            // Source 3: Table-wide footnotes
            if (table.Footnotes != null)
            {
                foreach (var footnote in table.Footnotes.Values)
                {
                    if (string.IsNullOrWhiteSpace(footnote))
                        continue;
                    var resolved = matchDispersionKeywords(footnote);
                    if (resolved != null)
                        return (resolved.Value.svt, resolved.Value.bt, null);
                }
            }

            // Source 4: Default — SD is the most common ± type in PK tables
            return ("SD", "SD", "PLUSMINUS_TYPE_INFERRED:SD");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Scans text for dispersion type keywords (SD, SE, CI) and returns the matching type.
        /// </summary>
        /// <param name="text">Text to scan (header path, footnote, caption).</param>
        /// <returns>Tuple of (svt, bt) if matched, null otherwise.</returns>
        private static (string svt, string bt)? matchDispersionKeywords(string text)
        {
            #region implementation

            foreach (var (pattern, svt, bt) in _dispersionKeywords)
            {
                if (pattern.IsMatch(text))
                    return (svt, bt);
            }

            return null;

            #endregion
        }

        #endregion Private Helpers

        #region Compound Header Layout

        /**************************************************************/
        /// <summary>
        /// Parses a compound header layout PK table: spanning header provides ParameterCategory,
        /// embedded data rows serve as sub-headers for parameter definitions, SocDivider rows
        /// reset context and trigger sub-header re-parsing, and column 0 carries TreatmentArm labels.
        /// </summary>
        /// <remarks>
        /// This is the third layout path in the PK parser, alongside single-column and two-column.
        /// It activates only when <see cref="detectCompoundHeaderLayout"/> returns true.
        ///
        /// ## Structure
        /// ```
        /// [Spanning header] → ParameterCategory (all cols identical LeafHeaderText)
        /// [Sub-header row]  → Parameter definitions (Dose | Tmax (h) | Cmax (mcg/mL) | AUC(0-96h)(mcgh/mL))
        /// [Data rows]       → Col 0 = TreatmentArm, Dose col = DoseRegimen, Param cols = observations
        /// [SocDivider]      → New ParameterCategory
        /// [Sub-header row]  → Refreshed parameter definitions (may differ from first section)
        /// [Data rows]       → Continue with new context
        /// ```
        /// </remarks>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <param name="observations">The observation list to populate.</param>
        /// <param name="population">Detected population from caption/section.</param>
        /// <param name="captionHint">Caption-derived value type hint.</param>
        /// <returns>The populated observations list.</returns>
        private List<ParsedObservation> parseCompoundLayout(
            ReconstructedTable table,
            List<ParsedObservation> observations,
            string? population,
            CaptionValueHint captionHint)
        {
            #region implementation

            // 1. Extract initial ParameterCategory from spanning header
            var spanningText = table.Header?.Columns?.FirstOrDefault()?.LeafHeaderText;
            var currentCategory = extractCategoryFromSpanningHeader(spanningText);

            // 2. Get all data rows (includes SocDividers)
            var dataRows = getDataBodyRows(table);
            if (dataRows.Count == 0)
                return observations;

            // 3. Consume first DataBody row as sub-header
            int rowIndex = 0;
            var firstSubHeader = dataRows[rowIndex];
            var doseColIndex = detectDoseColumnInSubHeader(firstSubHeader);
            var (paramDefs, subtypeMap) = extractParameterDefinitionsFromDataRow(firstSubHeader, doseColIndex);
            rowIndex++;

            if (paramDefs.Count == 0)
                return observations;

            // 4. Iterate remaining rows
            string? currentDoseRegimen = null;

            for (; rowIndex < dataRows.Count; rowIndex++)
            {
                var row = dataRows[rowIndex];

                // Handle SocDivider: update category, consume next row as sub-header
                if (row.Classification == RowClassification.SocDivider)
                {
                    var dividerText = row.SocName ?? row.Cells?.FirstOrDefault()?.CleanedText;
                    currentCategory = extractCategoryFromSpanningHeader(dividerText);
                    currentDoseRegimen = null;

                    // Next row after SocDivider is likely a new sub-header
                    if (rowIndex + 1 < dataRows.Count
                        && dataRows[rowIndex + 1].Classification == RowClassification.DataBody
                        && looksLikeSubHeader(dataRows[rowIndex + 1]))
                    {
                        rowIndex++;
                        doseColIndex = detectDoseColumnInSubHeader(dataRows[rowIndex]);
                        (paramDefs, subtypeMap) = extractParameterDefinitionsFromDataRow(dataRows[rowIndex], doseColIndex);
                    }

                    continue;
                }

                // Regular data row
                var col0Cell = getCellAtColumn(row, 0);
                var col0Text = col0Cell?.CleanedText?.Trim();

                // Skip empty label rows
                if (string.IsNullOrWhiteSpace(col0Text))
                    continue;

                // Col 0 = treatment arm label
                var armLabel = col0Text;
                var armN = extractArmNFromLabel(armLabel);

                // Dose from dose column (carry forward if dose column present)
                if (doseColIndex >= 0)
                {
                    var doseCell = getCellAtColumn(row, doseColIndex);
                    var doseCellText = doseCell?.CleanedText?.Trim();
                    if (!string.IsNullOrWhiteSpace(doseCellText))
                        currentDoseRegimen = doseCellText;
                }

                var (time, timeUnit, timepoint) = extractDuration(currentDoseRegimen);

                parseRowSafe(table, row, observations, (r, obs) =>
                {
                    foreach (var param in paramDefs)
                    {
                        var cell = getCellAtColumn(r, param.columnIndex);
                        if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                            continue;

                        var o = createBaseObservation(table, r, cell, TableCategory.PK);
                        o.ParameterName = param.name;
                        o.ParameterCategory = currentCategory;
                        o.ParameterSubtype = subtypeMap.GetValueOrDefault(param.columnIndex);
                        o.TreatmentArm = armLabel;
                        o.ArmN = armN;
                        o.DoseRegimen = currentDoseRegimen;
                        o.Population = population;
                        o.Timepoint = timepoint;
                        o.Time = time;
                        o.TimeUnit = timeUnit;
                        o.Unit = param.unit;

                        parseAndApplyPkValue(table, o, cell, param, captionHint);

                        obs.Add(o);
                    }
                });
            }

            // Post-parse: refine generic "CI" bound type from table context
            if (observations.Any(o => o.BoundType == "CI"))
            {
                var ciLevel = detectCILevelFromTableText(table);
                if (ciLevel != null)
                {
                    foreach (var o in observations.Where(o => o.BoundType == "CI"))
                        o.BoundType = ciLevel;
                }
            }

            return observations;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a multi-parenthetical PK parameter header string into its constituent parts.
        /// Handles compound headers like "AUC(0-96h)(mcgh/mL)" where multiple parenthetical
        /// groups carry different semantic roles (qualifier vs unit).
        /// </summary>
        /// <remarks>
        /// The last parenthetical group is always treated as the unit. Any preceding groups
        /// become part of the ParameterSubtype. For single-parenthetical headers like
        /// "Cmax (mcg/mL)", subtype is null.
        /// </remarks>
        /// <example>
        /// <code>
        /// parseCompoundParameterHeader("AUC(0-96h)(mcgh/mL)") → ("AUC", "mcgh/mL", "AUC(0-96h)")
        /// parseCompoundParameterHeader("Cmax (mcg/mL)")        → ("Cmax", "mcg/mL", null)
        /// parseCompoundParameterHeader("Tmax (h)")             → ("Tmax", "h", null)
        /// parseCompoundParameterHeader("Dose")                 → ("Dose", null, null)
        /// </code>
        /// </example>
        /// <param name="text">The parameter header text to parse.</param>
        /// <returns>Tuple of (name, unit, subtype).</returns>
        internal static (string name, string? unit, string? subtype) parseCompoundParameterHeader(string text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return (text ?? "", null, null);

            var matches = _allParentheticalsPattern.Matches(text);
            if (matches.Count == 0)
                return (text.Trim(), null, null);

            // Name is everything before the first parenthetical
            var firstParenIndex = text.IndexOf('(');
            var name = text[..firstParenIndex].Trim();

            // Last parenthetical = unit
            var unit = matches[^1].Groups[1].Value.Trim();

            // If multiple parentheticals, build subtype from name + all but last
            string? subtype = null;
            if (matches.Count > 1)
            {
                var subtypeParts = new List<string> { name };
                for (int i = 0; i < matches.Count - 1; i++)
                    subtypeParts.Add($"({matches[i].Groups[1].Value})");
                subtype = string.Join("", subtypeParts);
            }

            return (name, unit, subtype);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the ParameterCategory from a spanning header or SocDivider text by
        /// stripping common PK prefixes like "Pharmacokinetic Parameters for".
        /// </summary>
        /// <example>
        /// <code>
        /// extractCategoryFromSpanningHeader("Pharmacokinetic Parameters for Renal Impairment")
        ///     → "Renal Impairment"
        /// extractCategoryFromSpanningHeader("Hepatic Impairment")
        ///     → "Hepatic Impairment"
        /// </code>
        /// </example>
        /// <param name="spanningText">The spanning header or SocDivider text.</param>
        /// <returns>The category portion of the text, or the full text if no prefix found.</returns>
        private static string? extractCategoryFromSpanningHeader(string? spanningText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(spanningText))
                return spanningText;

            var stripped = _pkCategoryPrefixPattern.Replace(spanningText.Trim(), "");
            return string.IsNullOrWhiteSpace(stripped) ? spanningText.Trim() : stripped.Trim();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the sample size (ArmN) from a treatment arm row label containing an
        /// "(n=X)" suffix. Returns null if no n= pattern is found.
        /// </summary>
        /// <example>
        /// <code>
        /// extractArmNFromLabel("Healthy Volunteers (n=6)")   → 6
        /// extractArmNFromLabel("Alcoholic Cirrhosis (n=18)") → 18
        /// extractArmNFromLabel("Severe Renal Impairment")    → null
        /// </code>
        /// </example>
        /// <param name="label">The treatment arm row label text.</param>
        /// <returns>The sample size as int, or null if not found.</returns>
        internal static int? extractArmNFromLabel(string? label)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(label))
                return null;

            var match = _armNFromLabelPattern.Match(label);
            if (!match.Success)
                return null;

            var rawN = match.Groups[1].Value.Replace(",", "");
            return int.TryParse(rawN, out var n) ? n : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Scans a data row (acting as a sub-header) for a cell whose text matches
        /// <see cref="_doseColumnHeaders"/>. Returns the resolved column index of
        /// the dose cell, or -1 if no dose column found.
        /// </summary>
        /// <param name="row">The data row to inspect as a sub-header.</param>
        /// <returns>The column index of the dose cell, or -1.</returns>
        private static int detectDoseColumnInSubHeader(ReconstructedRow row)
        {
            #region implementation

            if (row.Cells == null)
                return -1;

            foreach (var cell in row.Cells)
            {
                var text = cell.CleanedText?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && _doseColumnHeaders.Contains(text))
                    return cell.ResolvedColumnStart ?? -1;
            }

            return -1;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts PK parameter definitions from a data row that serves as an embedded
        /// sub-header (e.g., the first data row in a compound header layout). Returns the
        /// same tuple structure as <see cref="extractParameterDefinitions"/> for compatibility,
        /// plus a separate subtype dictionary for compound headers like "AUC(0-96h)(mcgh/mL)".
        /// </summary>
        /// <param name="row">The data row containing sub-header text in its cells.</param>
        /// <param name="doseColumnIndex">Column index of the dose column to skip (-1 if none).</param>
        /// <returns>
        /// Tuple of (paramDefs, subtypeMap):
        /// - paramDefs: same structure as extractParameterDefinitions
        /// - subtypeMap: columnIndex → subtype string for compound headers (e.g., "AUC(0-96h)")
        /// </returns>
        internal static (
            List<(int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize)> paramDefs,
            Dictionary<int, string?> subtypeMap
        ) extractParameterDefinitionsFromDataRow(ReconstructedRow row, int doseColumnIndex)
        {
            #region implementation

            var paramDefs = new List<(int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize)>();
            var subtypeMap = new Dictionary<int, string?>();

            if (row.Cells == null)
                return (paramDefs, subtypeMap);

            foreach (var cell in row.Cells)
            {
                var colIndex = cell.ResolvedColumnStart ?? -1;
                if (colIndex < 0)
                    continue;

                // Skip col 0 (row label axis) and the dose column
                if (colIndex == 0 || colIndex == doseColumnIndex)
                    continue;

                var text = cell.CleanedText?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // Check if column is a sample size column
                var isSampleSize = _sampleSizeHeaders.Contains(text);

                // Parse compound parameter header
                var (name, unit, subtype) = parseCompoundParameterHeader(text);

                var isTimeMeasure = unit != null && _timeUnitStrings.Contains(unit);

                paramDefs.Add((colIndex, name, unit, isTimeMeasure, isSampleSize));
                subtypeMap[colIndex] = subtype;
            }

            return (paramDefs, subtypeMap);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects whether a table has a compound header layout: a spanning header row
        /// (all columns share identical LeafHeaderText), embedded sub-header rows with
        /// parameter definitions, and SocDivider rows for section context switches.
        /// </summary>
        /// <remarks>
        /// Detection requires ALL of the following signals:
        /// 1. HasSocDividers=true AND HasInferredHeader=true
        /// 2. At least 3 header columns
        /// 3. All header columns have identical LeafHeaderText (spanning header repeated)
        /// 4. First DataBody row contains at least one dose keyword cell AND one param unit cell
        /// </remarks>
        /// <param name="table">The reconstructed table to evaluate.</param>
        /// <returns>True if compound header layout is detected.</returns>
        internal static bool detectCompoundHeaderLayout(ReconstructedTable table)
        {
            #region implementation

            // Guard: need both structural flags
            if (table.HasSocDividers != true || table.HasInferredHeader != true)
                return false;

            // Guard: need at least 3 columns (label + dose + 1 param minimum)
            if (table.Header?.Columns == null || table.Header.Columns.Count < 3)
                return false;

            // Signal 1: All header columns have identical LeafHeaderText
            var distinctTexts = table.Header.Columns
                .Select(c => c.LeafHeaderText?.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (distinctTexts != 1)
                return false;

            // Signal 2: First DataBody row looks like a sub-header
            var firstDataRow = table.Rows?
                .FirstOrDefault(r => r.Classification == RowClassification.DataBody);
            if (firstDataRow?.Cells == null)
                return false;

            bool hasDoseCell = false;
            bool hasParamCell = false;

            foreach (var cell in firstDataRow.Cells)
            {
                var text = cell.CleanedText?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (_doseColumnHeaders.Contains(text))
                    hasDoseCell = true;

                if (_paramUnitPattern.IsMatch(text))
                    hasParamCell = true;
            }

            return hasDoseCell && hasParamCell;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks whether a data row looks like an embedded sub-header row by testing
        /// if any cell matches a dose column keyword. Used to confirm rows following
        /// SocDividers should be consumed as new sub-headers rather than parsed as data.
        /// </summary>
        /// <param name="row">The data row to evaluate.</param>
        /// <returns>True if the row appears to be a sub-header.</returns>
        private static bool looksLikeSubHeader(ReconstructedRow row)
        {
            #region implementation

            if (row.Cells == null)
                return false;

            foreach (var cell in row.Cells)
            {
                var text = cell.CleanedText?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && _doseColumnHeaders.Contains(text))
                    return true;
            }

            return false;

            #endregion
        }

        #endregion Compound Header Layout
    }
}
