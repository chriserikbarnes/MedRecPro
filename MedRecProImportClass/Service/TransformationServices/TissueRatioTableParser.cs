using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for simple two-column tissue-to-plasma ratio tables.
    /// Column 0 = tissue name, column 1 = ratio value.
    /// </summary>
    /// <remarks>
    /// ## Table Structure
    /// - 2 columns only: tissue name and ratio
    /// - No arm structure — each row is one observation
    /// - PrimaryValueType defaults to "Ratio", Unit = "ratio"
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    public class TissueRatioTableParser : BaseTableParser
    {
        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports TISSUE_DISTRIBUTION category.
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.TISSUE_DISTRIBUTION;

        /**************************************************************/
        /// <summary>
        /// Priority 10 — only tissue parser.
        /// </summary>
        public override int Priority => 10;

        /**************************************************************/
        /// <summary>
        /// Returns true if the table has exactly 2 columns.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True if TotalColumnCount == 2.</returns>
        public override bool CanParse(ReconstructedTable table)
        {
            #region implementation

            return table.TotalColumnCount == 2;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a two-column tissue ratio table: tissue name → ratio value.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            var observations = new List<ParsedObservation>();
            var (population, popConfidence) = detectPopulation(table);

            var dataRows = getDataBodyRows(table);
            foreach (var row in dataRows)
            {
                if (row.Classification == RowClassification.SocDivider)
                    continue;

                // Column 0 = tissue name
                var tissueCell = getCellAtColumn(row, 0);
                var tissueName = tissueCell?.CleanedText?.Trim();
                if (string.IsNullOrWhiteSpace(tissueName))
                    continue;

                // Column 1 = ratio value
                var ratioCell = getCellAtColumn(row, 1);
                if (ratioCell == null || string.IsNullOrWhiteSpace(ratioCell.CleanedText))
                    continue;

                var obs = createBaseObservation(table, row, ratioCell, TableCategory.TISSUE_DISTRIBUTION);
                obs.ParameterName = tissueName;
                obs.Population = population;
                obs.Unit = "ratio";

                var parsed = ValueParser.Parse(ratioCell.CleanedText);

                // Tissue-specific: default type is Ratio
                if (parsed.PrimaryValueType == "Numeric")
                {
                    parsed.PrimaryValueType = "Ratio";
                }

                applyParsedValue(obs, parsed);
                observations.Add(obs);
            }

            return observations;

            #endregion
        }

        #endregion ITableParser Implementation
    }
}
