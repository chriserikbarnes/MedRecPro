using System.Text.RegularExpressions;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for two-row header efficacy tables with stat columns (ARR, RR, P-value).
    /// Handles n= declaration rows that set ArmN mid-table and emits Comparison rows
    /// for relative risk and hazard ratio columns.
    /// </summary>
    /// <remarks>
    /// ## Table Structure
    /// - Top-level header = study context or stat column labels
    /// - Leaf header = arm names
    /// - Body may contain n= declaration rows that set ArmN for subsequent rows
    /// - Stat columns (P-value, RR, ARR) produce Comparison observations
    ///
    /// ## N-Declaration Rows
    /// Rows where all data cells match "n=\d+" are treated as sample size declarations
    /// rather than data. The N values are stored and applied to subsequent rows.
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="SimpleArmTableParser"/>
    public class EfficacyMultilevelTableParser : BaseTableParser
    {
        // Pattern for n= declaration cells
        private static readonly Regex _nEqualsPattern = new(
            @"^[Nn]\s*=\s*(\d+)$",
            RegexOptions.Compiled);

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
                return table.Header.Columns.Any(c => isStatColumn(c.LeafHeaderText));
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

            // Track n= declarations per arm index
            var pendingNs = new Dictionary<int, int>();
            string? currentGroup = null;
            var dataRows = getDataBodyRows(table);

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

                var (paramName, fnMarkers) = getParameterName(row);
                if (string.IsNullOrWhiteSpace(paramName))
                    continue;

                // Fault-tolerant row processing: if any cell throws, the entire table is skipped
                parseRowSafe(table, row, observations, (r, obs) =>
                {
                    // Extract row-level p-value from stat column
                    double? rowPValue = extractStatValue(r, statColumns, "pvalue");

                    // Arm data columns
                    for (int i = 0; i < arms.Count; i++)
                    {
                        var arm = arms[i];
                        var cell = getCellAtColumn(r, arm.ColumnIndex ?? 0);
                        if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                            continue;

                        var armN = pendingNs.TryGetValue(i, out var n) ? n : arm.SampleSize;

                        var o = createBaseObservation(table, r, cell, TableCategory.EFFICACY);
                        o.ParameterName = paramName;
                        o.ParameterCategory = currentGroup;
                        o.TreatmentArm = arm.Name;
                        o.ArmN = armN;
                        o.StudyContext = arm.StudyContext;
                        o.Population = population;
                        o.PValue = rowPValue;

                        var parsed = ValueParser.Parse(cell.CleanedText, armN);
                        applyParsedValue(o, parsed);
                        obs.Add(o);
                    }

                    // RR/HR comparison column
                    emitRrComparison(table, r, paramName, currentGroup, population, rowPValue,
                        statColumns, obs);
                });
            }

            return observations;

            #endregion
        }

        #endregion ITableParser Implementation

        #region Private Helpers

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
                if (match.Success && int.TryParse(match.Groups[1].Value, out var n))
                {
                    pendingNs[i] = n;
                }
            }

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
        /// </summary>
        private static void emitRrComparison(
            ReconstructedTable table, ReconstructedRow row,
            string paramName, string? group, string? population, double? pValue,
            Dictionary<string, ArmDefinition> statColumns, List<ParsedObservation> observations)
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

            if (parsed.PrimaryValueType == "Numeric" || parsed.PrimaryValueType == null)
                parsed.PrimaryValueType = "RelativeRiskReduction";

            applyParsedValue(obs, parsed);
            observations.Add(obs);

            #endregion
        }

        #endregion Private Helpers
    }
}
