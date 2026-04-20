using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for text-only narrative tables — drug-interaction prose, safety
    /// narratives, hormone-physiology descriptions, and similar tables whose cells
    /// contain whole sentences rather than numeric observations. Produces one
    /// observation per non-empty data cell, preserving the raw text so downstream
    /// consumers can still index, search, and cite the content.
    /// </summary>
    /// <remarks>
    /// ## How Tables Reach This Parser
    /// Tables are routed here by <c>TableParserRouter</c> when a section-code hint
    /// (e.g., LOINC 34090-1 Clinical Pharmacology) initially suggests PK but the
    /// table content fails PK validation: no PK parameter names appear in headers
    /// or row labels, and a majority of cells contain prose (length &gt; 120 chars
    /// or word-count &gt; 20).
    ///
    /// ## Observation Shape
    /// Per <see cref="PkTableParser"/> value-type conventions and
    /// <c>column-contracts.md</c>:
    /// - <c>PrimaryValueType = "Text"</c>
    /// - <c>PrimaryValue</c>, <c>SecondaryValue</c>, <c>LowerBound</c>, <c>UpperBound</c>,
    ///   <c>PValue</c>, <c>Time</c>, <c>Dose</c> all null
    /// - <c>RawValue</c> carries the full cell text
    /// - <c>ParameterName</c> carries the col-0 row label when present
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="ITableParser"/>
    public class TextDescriptiveTableParser : BaseTableParser
    {
        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports <see cref="TableCategory.TEXT_DESCRIPTIVE"/>.
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.TEXT_DESCRIPTIVE;

        /**************************************************************/
        /// <summary>
        /// Priority 10 — only text-descriptive parser.
        /// </summary>
        public override int Priority => 10;

        /**************************************************************/
        /// <summary>
        /// Always returns true for TEXT_DESCRIPTIVE-categorized tables.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True.</returns>
        public override bool CanParse(ReconstructedTable table) => true;

        /**************************************************************/
        /// <summary>
        /// Emits one observation per non-empty data cell. Col 0 text is preserved
        /// as <c>ParameterName</c>; every other non-empty cell becomes its own
        /// <c>RawValue</c> observation keyed to the same row.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of text observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            var observations = new List<ParsedObservation>();
            var (population, _) = detectPopulation(table);
            var columnCount = table.TotalColumnCount;

            foreach (var row in getDataBodyRows(table))
            {
                // Row label candidate (col 0); may be null for tables where every
                // column carries narrative content
                var col0Cell = getCellAtColumn(row, 0);
                var rowLabel = col0Cell?.CleanedText?.Trim();

                // Emit one observation per non-empty cell. Start at col 0 unless
                // col 0 is being used as the row label AND there is at least one
                // further value column — in that case the label is stored on the
                // downstream observations as ParameterName and we skip col 0.
                var startColumn = (!string.IsNullOrWhiteSpace(rowLabel) && columnCount > 1) ? 1 : 0;

                parseRowSafe(table, row, observations, (r, obs) =>
                {
                    for (int col = startColumn; col < columnCount; col++)
                    {
                        var cell = getCellAtColumn(r, col);
                        if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                            continue;

                        var o = createBaseObservation(table, r, cell, TableCategory.TEXT_DESCRIPTIVE);
                        o.ParameterName = string.IsNullOrWhiteSpace(rowLabel) ? null : rowLabel;
                        o.Population = population;
                        o.PrimaryValueType = "Text";
                        o.RawValue = cell.CleanedText;

                        obs.Add(o);
                    }
                });
            }

            return observations;

            #endregion
        }

        #endregion ITableParser Implementation
    }
}
