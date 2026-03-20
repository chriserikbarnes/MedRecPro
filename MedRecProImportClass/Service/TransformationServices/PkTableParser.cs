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

                // Each parameter column produces one observation
                foreach (var param in paramDefs)
                {
                    var cell = getCellAtColumn(row, param.columnIndex);
                    if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                        continue;

                    var obs = createBaseObservation(table, row, cell, TableCategory.PK);
                    obs.ParameterName = param.name;
                    obs.DoseRegimen = doseRegimen;
                    obs.Population = population;
                    obs.Unit = param.unit;

                    // Parse value — PK cells often use value(CV%) format
                    var parsed = ValueParser.Parse(cell.CleanedText);

                    // PK-specific: bare Numeric → Mean
                    if (parsed.PrimaryValueType == "Numeric")
                    {
                        parsed.PrimaryValueType = "Mean";
                    }

                    applyParsedValue(obs, parsed);

                    // Unit from header takes precedence over parsed unit
                    if (!string.IsNullOrEmpty(param.unit))
                        obs.Unit = param.unit;

                    observations.Add(obs);
                }
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

        #endregion Private Helpers
    }
}
