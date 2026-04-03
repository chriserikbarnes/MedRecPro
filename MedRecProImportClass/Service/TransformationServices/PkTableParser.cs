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
    }
}
