using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for dosing parameter grid tables. Header = dose levels/units,
    /// rows = parameters/populations.
    /// </summary>
    /// <remarks>
    /// ## Table Structure
    /// - Header columns = dose levels, units, or population labels
    /// - Column 0 = parameter name or population descriptor
    /// - Data cells = dosing values
    ///
    /// ## DoseRegimen
    /// Populated from the header column text for each data cell.
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    public class DosingTableParser : BaseTableParser
    {
        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports DOSING category.
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.DOSING;

        /**************************************************************/
        /// <summary>
        /// Priority 10 — only dosing parser.
        /// </summary>
        public override int Priority => 10;

        /**************************************************************/
        /// <summary>
        /// Always returns true for DOSING-categorized tables.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True.</returns>
        public override bool CanParse(ReconstructedTable table) => true;

        /**************************************************************/
        /// <summary>
        /// Parses a dosing table: header columns provide dose context, each data cell
        /// becomes one observation with Unit from header text.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            var observations = new List<ParsedObservation>();
            var (population, popConfidence) = detectPopulation(table);

            // Extract dose headers (column labels)
            var doseHeaders = extractDoseHeaders(table);

            var dataRows = getDataBodyRows(table);
            foreach (var row in dataRows)
            {
                if (row.Classification == RowClassification.SocDivider)
                    continue;

                // Column 0 = parameter/label
                var (paramName, fnMarkers) = getParameterName(row);
                if (string.IsNullOrWhiteSpace(paramName))
                    continue;

                // Each dose column
                foreach (var dose in doseHeaders)
                {
                    var cell = getCellAtColumn(row, dose.columnIndex);
                    if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                        continue;

                    var obs = createBaseObservation(table, row, cell, TableCategory.DOSING);
                    obs.ParameterName = paramName;
                    obs.Population = population;
                    obs.Unit = dose.headerText;

                    var parsed = ValueParser.Parse(cell.CleanedText);
                    applyParsedValue(obs, parsed);
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
        /// Extracts dose header labels from header columns (skip col 0).
        /// </summary>
        private static List<(int columnIndex, string headerText)> extractDoseHeaders(ReconstructedTable table)
        {
            #region implementation

            var headers = new List<(int columnIndex, string headerText)>();
            if (table.Header?.Columns == null)
                return headers;

            for (int i = 1; i < table.Header.Columns.Count; i++)
            {
                var col = table.Header.Columns[i];
                var text = col.LeafHeaderText?.Trim() ?? $"Col{i}";
                headers.Add((col.ColumnIndex ?? i, text));
            }

            return headers;

            #endregion
        }

        #endregion Private Helpers
    }
}
