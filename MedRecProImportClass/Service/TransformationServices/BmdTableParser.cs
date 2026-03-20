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

                foreach (var tp in timepoints)
                {
                    var cell = getCellAtColumn(row, tp.columnIndex);
                    if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                        continue;

                    var obs = createBaseObservation(table, row, cell, TableCategory.BMD);
                    obs.ParameterName = siteName;
                    obs.Timepoint = tp.label;
                    obs.Population = population;
                    obs.Unit = "%";

                    var parsed = ValueParser.Parse(cell.CleanedText);

                    // BMD-specific: default type is MeanPercentChange
                    if (parsed.PrimaryValueType == "Numeric" || parsed.PrimaryValueType == "Percentage")
                    {
                        parsed.PrimaryValueType = "MeanPercentChange";
                    }

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

        #endregion Private Helpers
    }
}
