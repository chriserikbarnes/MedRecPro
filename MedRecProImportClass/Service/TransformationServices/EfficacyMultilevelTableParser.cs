using System.Text.RegularExpressions;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for two-row header efficacy tables with stat columns (ARR, RR, P-value).
    /// Handles n= declaration rows that set ArmN mid-table, column sub-header rows
    /// that provide PrimaryValueType and Unit context, and emits Comparison rows
    /// for relative risk and hazard ratio columns.
    /// </summary>
    /// <remarks>
    /// ## Table Structure
    /// - Top-level header = study context or stat column labels
    /// - Leaf header = arm names
    /// - Body may contain n= declaration rows that set ArmN for subsequent rows
    /// - Body may contain column sub-header rows (e.g., "Absolute Risk per 10,000 Women-Years")
    /// - Stat columns (P-value, RR, ARR) produce Comparison observations
    ///
    /// ## N-Declaration Rows
    /// Rows where all data cells match "n=\d+" are treated as sample size declarations
    /// rather than data. The N values are stored and applied to subsequent rows.
    ///
    /// ## Column Sub-Header Rows
    /// Rows where arm cells contain descriptive text (e.g., "Absolute Risk per 10,000
    /// Women-Years") rather than numeric data. These provide PrimaryValueType and Unit
    /// context for subsequent data rows. Arrow symbols (↔, →) propagate the previous
    /// column's sub-header.
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="SimpleArmTableParser"/>
    public class EfficacyMultilevelTableParser : BaseTableParser
    {
        // Pattern for n= declaration cells: "n=102" or "N=5,310"
        private static readonly Regex _nEqualsPattern = new(
            @"^[Nn]\s*=\s*(\d[\d,]*)$",
            RegexOptions.Compiled);

        // Pattern for detecting column sub-header keywords in arm cells
        // Matches descriptive text: Risk, Rate, Incidence, Women-Years, Person-Years, Events
        // Also matches directional arrows used as "ditto" markers
        private static readonly Regex _subHeaderKeywordPattern = new(
            @"(?:Risk|Rate|Incidence|(?:Women|Person|Patient)[\s\-]*Years?|Events|↔|→|←|⟷|⟶)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern for extracting CI confidence level from stat column headers
        // Matches: "95% nCI", "95% CI", "90% CI"
        private static readonly Regex _ciLevelPattern = new(
            @"(\d+)\s*%\s*\w*\s*CI\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Patterns for inferring comparison PrimaryValueType from stat column header
        private static readonly Regex _hazardRatioPattern = new(
            @"(?:Hazard|\bHR\b)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _oddsRatioPattern = new(
            @"(?:Odds|\bOR\b)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Arrow characters used as "same as previous column" markers in sub-header rows
        private static readonly HashSet<char> _arrowChars = new() { '↔', '→', '←', '⟷' };

        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports EFFICACY category.
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.EFFICACY;

        /**************************************************************/
        /// <summary>
        /// Priority 10 — tried before SimpleArmTableParser for multi-level efficacy headers.
        /// </summary>
        public override int Priority => 10;

        /**************************************************************/
        /// <summary>
        /// Returns true if the table has 2+ header rows or contains stat column labels
        /// in the header (ARR, RR, P-value).
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True if multi-level or has stat columns.</returns>
        public override bool CanParse(ReconstructedTable table)
        {
            #region implementation

            if (table.Header?.HeaderRowCount >= 2)
                return true;

            // Check if header contains stat labels
            if (table.Header?.Columns != null)
            {
                if (table.Header.Columns.Any(c => isStatColumn(c.LeafHeaderText)))
                    return true;

                return hasSimpleEfficacyArmShape(table);
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recognizes single-header clinical-study arm tables whose row labels carry
        /// the endpoint hierarchy while arm headers carry treatment context.
        /// </summary>
        /// <param name="table">Candidate efficacy table.</param>
        /// <returns>True when the table has arm headers and parseable body outcomes.</returns>
        private static bool hasSimpleEfficacyArmShape(ReconstructedTable table)
        {
            #region implementation

            if (table.Header?.Columns == null || table.Header.Columns.Count <= 1)
                return false;

            var hasArm = table.Header.Columns
                .Skip(1)
                .Any(c =>
                {
                    var leaf = c.LeafHeaderText?.Trim();
                    return !string.IsNullOrWhiteSpace(leaf) &&
                           !isStatColumn(leaf) &&
                           !BaseTableParser.LooksLikeGenericArmLabelForRouting(leaf);
                });

            if (!hasArm)
                return false;

            foreach (var row in table.DataRows())
            {
                var cells = row.Cells?
                    .Where(c => (c.ResolvedColumnStart ?? 0) > 0 && !string.IsNullOrWhiteSpace(c.CleanedText))
                    .ToList();

                if (cells == null)
                    continue;

                foreach (var cell in cells)
                {
                    var parsed = ValueParser.Parse(cell.CleanedText);
                    if (parsed.PrimaryValue.HasValue || parsed.SecondaryValue.HasValue || parsed.PValue.HasValue)
                        return true;
                }
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a multi-level efficacy table with n= declaration rows and stat columns.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            ClearDiagnostics();
            var observations = new List<ParsedObservation>();
            var (population, popConfidence) = detectPopulation(table);

            // Separate arms from stat columns
            var allArms = extractArmDefinitions(table);
            var arms = new List<ArmDefinition>();
            var statColumns = new Dictionary<string, ArmDefinition>();

            foreach (var arm in allArms)
            {
                if (isStatColumn(arm.Name))
                {
                    classifyStatColumn(arm, statColumns);
                }
                else
                {
                    arms.Add(arm);
                }
            }

            // Track n= declarations per arm index and column sub-headers
            var pendingNs = new Dictionary<int, int>();
            var columnSubHeaders = new Dictionary<int, string>();
            string? currentGroup = null;
            var dataRows = getDataBodyRows(table);
            var skipRows = enrichArmsFromBodyRows(dataRows, arms);
            if (skipRows > 0)
            {
                dataRows = dataRows.Skip(skipRows).ToList();
            }

            foreach (var row in dataRows)
            {
                // SOC/group divider
                if (row.Classification == RowClassification.SocDivider)
                {
                    currentGroup = row.SocName;
                    pendingNs.Clear();
                    continue;
                }

                // Check if this is an n= declaration row
                if (isNDeclarationRow(row, arms))
                {
                    captureNDeclarations(row, arms, pendingNs);
                    continue;
                }

                // Check if this is a column sub-header row (e.g., "Absolute Risk per 10,000 Women-Years")
                // Only capture the first sub-header row encountered
                if (columnSubHeaders.Count == 0 && isColumnSubHeaderRow(row, arms))
                {
                    captureColumnSubHeaders(row, arms, columnSubHeaders);
                    continue;
                }

                var (paramName, fnMarkers) = getParameterName(row);
                if (string.IsNullOrWhiteSpace(paramName))
                    continue;

                var hasArmData = arms.Where(hasUsableTreatmentArm).Any(arm =>
                {
                    var cell = getCellAtColumn(row, arm.ColumnIndex ?? 0);
                    return cell != null && !string.IsNullOrWhiteSpace(cell.CleanedText);
                });

                if (!hasArmData)
                {
                    currentGroup = paramName;
                    pendingNs.Clear();
                    continue;
                }

                var effectiveParamName = paramName;
                var interspersedLabelCellSequence = (int?)null;
                if (tryRecoverInterspersedRowLabel(
                    row, arms, paramName, TableCategory.EFFICACY,
                    out var recoveredParamName,
                    out var recoveredCategory,
                    out var recoveredLabelCellSequence))
                {
                    effectiveParamName = recoveredParamName ?? paramName;
                    interspersedLabelCellSequence = recoveredLabelCellSequence;
                    if (!string.IsNullOrWhiteSpace(recoveredCategory))
                        currentGroup = recoveredCategory;
                }

                if (isStructuralContextRow(row, arms, paramName, TableCategory.EFFICACY))
                {
                    currentGroup = paramName;
                    pendingNs.Clear();
                    recordSuppressedStructuralRow(
                        table, row, null, TableCategory.EFFICACY,
                        paramName, null, paramName, paramName, "ParameterCategory",
                        "Structural Efficacy row captured as group context");
                    continue;
                }

                // Fault-tolerant row processing: if any cell throws, the entire table is skipped
                parseRowSafe(table, row, observations, (r, obs) =>
                {
                    if (isComparisonRowLabel(effectiveParamName))
                    {
                        emitRowLabelComparisonRows(
                            table, r, effectiveParamName, currentGroup,
                            population, arms, pendingNs, obs);
                        return;
                    }

                    // Extract row-level p-value from stat column
                    double? rowPValue = extractStatValue(r, statColumns, "pvalue");

                    // Arm data columns
                    for (int i = 0; i < arms.Count; i++)
                    {
                        var arm = arms[i];
                        if (!hasUsableTreatmentArm(arm))
                            continue;

                        var cell = getCellAtColumn(r, arm.ColumnIndex ?? 0);
                        if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                            continue;
                        if (interspersedLabelCellSequence.HasValue &&
                            cell.SequenceNumber == interspersedLabelCellSequence.Value)
                            continue;

                        var armN = pendingNs.TryGetValue(i, out var n) ? n : arm.SampleSize;
                        var parseArm = createArmForValueContext(arm, armN);

                        var o = createBaseObservation(table, r, cell, TableCategory.EFFICACY);
                        o.ParameterName = effectiveParamName;
                        o.ParameterCategory = currentGroup;
                        o.ParameterSubtype = arm.ParameterSubtype;
                        o.TreatmentArm = arm.Name;
                        o.ArmN = armN;
                        o.StudyContext = arm.StudyContext;
                        o.Population = population;
                        o.PValue = rowPValue;

                        var parsed = parseValueWithAeEfficacyContext(
                            cell.CleanedText,
                            TableCategory.EFFICACY,
                            effectiveParamName,
                            currentGroup,
                            parseArm,
                            table.Caption);
                        applyParsedValue(o, parsed);

                        // Apply column sub-header context (PrimaryValueType + Unit)
                        if (columnSubHeaders.TryGetValue(i, out var subHeader))
                        {
                            var subHeaderType = inferValueTypeFromSubHeader(subHeader);
                            if (subHeaderType != null &&
                                (o.PrimaryValueType == "Numeric" || o.PrimaryValueType == "Percentage"))
                            {
                                o.PrimaryValueType = subHeaderType;
                            }

                            var unit = extractUnitFromSubHeader(subHeader);
                            if (unit != null && string.IsNullOrEmpty(o.Unit))
                            {
                                o.Unit = unit;
                            }
                        }

                        if (shouldSuppressStructuralObservation(o, parsed))
                        {
                            recordSuppressedStructuralRow(
                                table, r, cell, TableCategory.EFFICACY,
                                effectiveParamName, arm.Name, cell.CleanedText, effectiveParamName,
                                "ParameterCategory",
                                "Structural Efficacy cell suppressed before observation emission");
                            continue;
                        }

                        obs.Add(o);
                    }

                    // RR/HR comparison column
                    emitRrComparison(table, r, effectiveParamName, currentGroup, population, rowPValue,
                        statColumns, obs, arms, pendingNs);
                });
            }
            // NOTE: This call is currently design only; the method does nothing. It is
            // a placeholder for future logic to suppress duplicate comparison observations.
            suppressDuplicateComparisonEmissions(observations);

            return observations;

            #endregion
        }

        #endregion ITableParser Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Creates an arm context with the resolved denominator for value parsing.
        /// </summary>
        /// <param name="arm">Original arm definition.</param>
        /// <param name="armN">Resolved denominator from header or pending n row.</param>
        /// <returns>Arm definition carrying the resolved sample size.</returns>
        /// <seealso cref="ArmDefinition"/>
        private static ArmDefinition createArmForValueContext(ArmDefinition arm, int? armN)
        {
            #region implementation

            if (arm.SampleSize == armN)
                return arm;

            return new ArmDefinition
            {
                Name = arm.Name,
                SampleSize = armN,
                FormatHint = arm.FormatHint,
                ColumnIndex = arm.ColumnIndex,
                StudyContext = arm.StudyContext,
                ParameterSubtype = arm.ParameterSubtype,
                DoseRegimen = arm.DoseRegimen,
                Dose = arm.Dose,
                DoseUnit = arm.DoseUnit
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Emits comparison observations for efficacy statistic rows that are encoded
        /// as body rows instead of dedicated statistic columns.
        /// </summary>
        /// <param name="table">Source reconstructed table.</param>
        /// <param name="row">Current data row.</param>
        /// <param name="paramName">Statistic row label.</param>
        /// <param name="group">Current endpoint group.</param>
        /// <param name="population">Detected population context.</param>
        /// <param name="arms">Treatment arms used to locate value cells.</param>
        /// <param name="pendingNs">Resolved denominators from preceding n rows.</param>
        /// <param name="observations">Observation sink.</param>
        /// <seealso cref="BaseTableParser"/>
        private void emitRowLabelComparisonRows(
            ReconstructedTable table, ReconstructedRow row,
            string paramName, string? group, string? population,
            List<ArmDefinition> arms, Dictionary<int, int> pendingNs,
            List<ParsedObservation> observations)
        {
            #region implementation

            var emittedCellSequences = new HashSet<int?>();
            for (int i = 0; i < arms.Count; i++)
            {
                var arm = arms[i];
                if (!hasUsableTreatmentArm(arm))
                    continue;

                var cell = getCellAtColumn(row, arm.ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;
                if (!emittedCellSequences.Add(cell.SequenceNumber))
                    continue;

                var armN = pendingNs.TryGetValue(i, out var n) ? n : arm.SampleSize;
                var parseArm = createArmForValueContext(arm, armN);

                var obs = createBaseObservation(table, row, cell, TableCategory.EFFICACY);
                obs.ParameterName = paramName;
                obs.ParameterCategory = group;
                obs.TreatmentArm = "Comparison";
                obs.Population = population;

                var parsed = parseValueWithAeEfficacyContext(
                    cell.CleanedText,
                    TableCategory.EFFICACY,
                    paramName,
                    group,
                    parseArm,
                    table.Caption,
                    forcePValue: isPValueRowLabel(paramName));
                parsed = applyRowLabelComparisonType(paramName, parsed);

                applyParsedValue(obs, parsed);
                if (shouldSuppressStructuralObservation(obs, parsed))
                {
                    recordSuppressedStructuralRow(
                        table, row, cell, TableCategory.EFFICACY,
                        paramName, "Comparison", cell.CleanedText, paramName,
                        "ParameterCategory",
                        "Structural Efficacy comparison-row cell suppressed before observation emission");
                    continue;
                }

                if (obs.PrimaryValue == null && obs.SecondaryValue == null && obs.PValue == null)
                    continue;

                observations.Add(obs);
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies statistic-row value types to parsed comparison values.
        /// </summary>
        /// <param name="rowLabel">Statistic row label.</param>
        /// <param name="parsed">Parsed value to adjust.</param>
        /// <returns>Adjusted parsed value.</returns>
        /// <seealso cref="ParsedValue"/>
        private static ParsedValue applyRowLabelComparisonType(string? rowLabel, ParsedValue parsed)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(rowLabel) || !parsed.PrimaryValue.HasValue)
                return parsed;

            if (isPValueRowLabel(rowLabel))
                return parsed;

            if (rowLabel.Contains("Difference", StringComparison.OrdinalIgnoreCase) ||
                rowLabel.Contains("Risk Difference", StringComparison.OrdinalIgnoreCase))
            {
                parsed.PrimaryValueType = "RiskDifference";
                if (rowLabel.Contains("%", StringComparison.OrdinalIgnoreCase) ||
                    rowLabel.Contains("percent", StringComparison.OrdinalIgnoreCase))
                {
                    parsed.Unit = "%";
                }
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "COMPARISON_ROW_CONTEXT");
            }
            else if (rowLabel.Contains("Hazard", StringComparison.OrdinalIgnoreCase))
            {
                parsed.PrimaryValueType = "HazardRatio";
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "COMPARISON_ROW_CONTEXT");
            }
            else if (rowLabel.Contains("Odds", StringComparison.OrdinalIgnoreCase))
            {
                parsed.PrimaryValueType = "OddsRatio";
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "COMPARISON_ROW_CONTEXT");
            }
            else if (rowLabel.Contains("Relative Risk", StringComparison.OrdinalIgnoreCase))
            {
                parsed.PrimaryValueType = "RelativeRisk";
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "COMPARISON_ROW_CONTEXT");
            }

            return parsed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies a stat column and adds it to the stat dictionary.
        /// </summary>
        private static void classifyStatColumn(ArmDefinition arm, Dictionary<string, ArmDefinition> statColumns)
        {
            #region implementation

            var name = arm.Name ?? "";
            if (name.Contains("P-value", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("P value", StringComparison.OrdinalIgnoreCase))
                statColumns["pvalue"] = arm;
            else if (name.Contains("ARR", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("Absolute Risk", StringComparison.OrdinalIgnoreCase))
                statColumns["arr"] = arm;
            else if (name.Contains("Relative Risk", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("Hazard", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("HR", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("RR", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("OR", StringComparison.OrdinalIgnoreCase))
                statColumns["rr_ci"] = arm;
            else if (name.Contains("Difference", StringComparison.OrdinalIgnoreCase))
                statColumns["diff_ci"] = arm;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if a row is an n= declaration row (all data cells match "n=\d+").
        /// </summary>
        private static bool isNDeclarationRow(ReconstructedRow row, List<ArmDefinition> arms)
        {
            #region implementation

            if (arms.Count == 0) return false;

            var matchCount = 0;
            var cellCount = 0;

            foreach (var arm in arms)
            {
                var cell = getCellAtColumn(row, arm.ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;

                cellCount++;
                if (_nEqualsPattern.IsMatch(cell.CleanedText.Trim()))
                    matchCount++;
            }

            return cellCount > 0 && matchCount == cellCount;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Captures n= values from a declaration row into the pending N dictionary.
        /// </summary>
        private static void captureNDeclarations(
            ReconstructedRow row, List<ArmDefinition> arms, Dictionary<int, int> pendingNs)
        {
            #region implementation

            for (int i = 0; i < arms.Count; i++)
            {
                var cell = getCellAtColumn(row, arms[i].ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;

                var match = _nEqualsPattern.Match(cell.CleanedText.Trim());
                if (match.Success && int.TryParse(match.Groups[1].Value.Replace(",", ""), out var n))
                {
                    pendingNs[i] = n;
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if a row is a column sub-header row containing descriptive text
        /// (e.g., "Absolute Risk per 10,000 Women-Years") rather than numeric data.
        /// Arrow symbols (↔, →, ←) are treated as sub-header markers ("same as previous").
        /// </summary>
        /// <param name="row">The data row to evaluate.</param>
        /// <param name="arms">Arm definitions for column lookup.</param>
        /// <returns>True if the row contains sub-header keywords in arm cells.</returns>
        internal static bool isColumnSubHeaderRow(ReconstructedRow row, List<ArmDefinition> arms)
        {
            #region implementation

            if (arms.Count == 0) return false;

            var keywordCount = 0;
            var cellCount = 0;
            var hasSubstantiveText = false;

            foreach (var arm in arms)
            {
                var cell = getCellAtColumn(row, arm.ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;

                var text = cell.CleanedText.Trim();
                cellCount++;

                // Single arrow character = ditto marker
                if (text.Length == 1 && _arrowChars.Contains(text[0]))
                {
                    keywordCount++;
                    continue;
                }

                // Check for descriptive sub-header keywords
                if (_subHeaderKeywordPattern.IsMatch(text))
                {
                    keywordCount++;
                    if (text.Length >= 3)
                        hasSubstantiveText = true;
                }
            }

            // All non-empty arm cells must be keywords/arrows, and at least one must be substantive
            return cellCount > 0 && keywordCount == cellCount && hasSubstantiveText;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts column sub-header text from arm cells. Arrow symbols (↔, →, ←)
        /// propagate the previous arm's sub-header text (meaning "same as above/previous").
        /// </summary>
        /// <param name="row">The sub-header row.</param>
        /// <param name="arms">Arm definitions for column lookup.</param>
        /// <param name="subHeaders">Dictionary to populate with arm index → sub-header text.</param>
        internal static void captureColumnSubHeaders(
            ReconstructedRow row, List<ArmDefinition> arms, Dictionary<int, string> subHeaders)
        {
            #region implementation

            string? lastSubHeader = null;

            for (int i = 0; i < arms.Count; i++)
            {
                var cell = getCellAtColumn(row, arms[i].ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;

                var text = cell.CleanedText.Trim();

                // Arrow = propagate previous column's sub-header
                if (text.Length == 1 && _arrowChars.Contains(text[0]))
                {
                    if (lastSubHeader != null)
                        subHeaders[i] = lastSubHeader;
                }
                else
                {
                    subHeaders[i] = text;
                    lastSubHeader = text;
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps column sub-header descriptive text to a PrimaryValueType.
        /// </summary>
        /// <param name="subHeaderText">Sub-header text (e.g., "Absolute Risk per 10,000 Women-Years").</param>
        /// <returns>PrimaryValueType string, or null if no mapping found.</returns>
        internal static string? inferValueTypeFromSubHeader(string subHeaderText)
        {
            #region implementation

            if (subHeaderText.Contains("Absolute Risk", StringComparison.OrdinalIgnoreCase))
                return "AbsoluteRisk";
            if (subHeaderText.Contains("Incidence", StringComparison.OrdinalIgnoreCase))
                return "Incidence";
            if (subHeaderText.Contains("Rate", StringComparison.OrdinalIgnoreCase))
                return "Rate";
            if (subHeaderText.Contains("Events", StringComparison.OrdinalIgnoreCase) ||
                subHeaderText.Contains("Number of", StringComparison.OrdinalIgnoreCase))
                return "Count";

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts a unit string from column sub-header text.
        /// Looks for "per " prefix to identify the unit portion.
        /// </summary>
        /// <param name="subHeaderText">Sub-header text (e.g., "Absolute Risk per 10,000 Women-Years").</param>
        /// <returns>Unit string (e.g., "per 10,000 Women-Years"), or null.</returns>
        internal static string? extractUnitFromSubHeader(string subHeaderText)
        {
            #region implementation

            var perIdx = subHeaderText.IndexOf("per ", StringComparison.OrdinalIgnoreCase);
            if (perIdx >= 0)
            {
                var unit = subHeaderText.Substring(perIdx).Trim();
                return string.IsNullOrWhiteSpace(unit) ? null : unit;
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts CI confidence level from a stat column header.
        /// Handles variants: "95% CI", "95% nCI", "90% CI".
        /// </summary>
        /// <param name="headerText">Stat column header text.</param>
        /// <returns>BoundType string (e.g., "95CI", "90CI"), or null.</returns>
        internal static string? extractCILevelFromHeader(string? headerText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(headerText))
                return null;

            var match = _ciLevelPattern.Match(headerText);
            return match.Success ? $"{match.Groups[1].Value}CI" : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Infers the PrimaryValueType for a Comparison observation from the stat column header.
        /// Maps header text to the correct statistical measure type.
        /// </summary>
        /// <param name="headerText">Stat column header text (e.g., "Relative Risk CE vs. Placebo (95% nCI)").</param>
        /// <returns>PrimaryValueType string (e.g., "RelativeRisk", "HazardRatio").</returns>
        internal static string inferComparisonTypeFromHeader(string? headerText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(headerText))
                return "RelativeRisk";

            // Check specificity order: Risk Reduction first (to not be swallowed by Relative Risk)
            if (headerText.Contains("Risk Reduction", StringComparison.OrdinalIgnoreCase))
                return "RelativeRiskReduction";
            if (_hazardRatioPattern.IsMatch(headerText))
                return "HazardRatio";
            if (_oddsRatioPattern.IsMatch(headerText))
                return "OddsRatio";
            if (headerText.Contains("Relative Risk", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(headerText, @"\bRR\b"))
                return "RelativeRisk";

            return "RelativeRisk";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts a numeric value from a stat column cell.
        /// </summary>
        private static double? extractStatValue(
            ReconstructedRow row, Dictionary<string, ArmDefinition> statColumns, string key)
        {
            #region implementation

            if (!statColumns.TryGetValue(key, out var statArm))
                return null;

            var cell = getCellAtColumn(row, statArm.ColumnIndex ?? 0);
            if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                return null;

            var parsed = ValueParser.Parse(cell.CleanedText);
            return parsed.PValue ?? parsed.PrimaryValue;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Emits a Comparison observation for the RR/HR stat column.
        /// Derives PrimaryValueType and CI level from the stat column header text.
        /// Computes ArmN as the sum of arm Ns for binary "X vs. Y" comparisons.
        /// </summary>
        private static void emitRrComparison(
            ReconstructedTable table, ReconstructedRow row,
            string paramName, string? group, string? population, double? pValue,
            Dictionary<string, ArmDefinition> statColumns, List<ParsedObservation> observations,
            List<ArmDefinition> arms, Dictionary<int, int> pendingNs)
        {
            #region implementation

            if (!statColumns.TryGetValue("rr_ci", out var rrArm))
                return;

            var cell = getCellAtColumn(row, rrArm.ColumnIndex ?? 0);
            if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                return;

            var parsed = ValueParser.Parse(cell.CleanedText);
            if (parsed.PrimaryValue == null && parsed.PrimaryValueType != "PValue")
                return;

            var obs = createBaseObservation(table, row, cell, TableCategory.EFFICACY);
            obs.ParameterName = paramName;
            obs.ParameterCategory = group;
            obs.TreatmentArm = "Comparison";
            obs.Population = population;
            obs.PValue = pValue;

            // Derive PrimaryValueType from header rather than hardcoding
            if (parsed.PrimaryValueType == "Numeric" || parsed.PrimaryValueType == null ||
                parsed.PrimaryValueType == "RelativeRiskReduction")
            {
                parsed.PrimaryValueType = inferComparisonTypeFromHeader(rrArm.Name);
            }

            applyParsedValue(obs, parsed);

            // Refine generic "CI" BoundType using stat column header text
            if (obs.BoundType == "CI")
            {
                var headerCILevel = extractCILevelFromHeader(rrArm.Name);
                if (headerCILevel != null)
                    obs.BoundType = headerCILevel;
            }

            // Compute ArmN for binary "X vs. Y" comparisons with both Ns known
            var isBinaryComparison = arms.Count == 2 &&
                (rrArm.Name?.Contains(" vs.", StringComparison.OrdinalIgnoreCase) == true ||
                 rrArm.Name?.Contains(" versus ", StringComparison.OrdinalIgnoreCase) == true);

            if (isBinaryComparison)
            {
                int? n0 = pendingNs.TryGetValue(0, out var pn0) ? pn0 : arms[0].SampleSize;
                int? n1 = pendingNs.TryGetValue(1, out var pn1) ? pn1 : arms[1].SampleSize;
                if (n0.HasValue && n1.HasValue)
                    obs.ArmN = n0.Value + n1.Value;
            }

            observations.Add(obs);

            #endregion
        }

        #endregion Private Helpers
    }
}
