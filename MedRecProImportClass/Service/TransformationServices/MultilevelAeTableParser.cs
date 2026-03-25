using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for two-row header AE tables with colspan study contexts and arm sub-headers.
    /// HeaderPath[0] = StudyContext (from colspan row), HeaderPath[last] = arm definition.
    /// </summary>
    /// <remarks>
    /// ## Table Structure
    /// - Row 1: colspan headers providing study context ("Treatment", "Prevention")
    /// - Row 2: arm sub-headers with N= ("EVISTA (N=2557) %", "Placebo (N=2576) %")
    /// - Body: SOC divider rows + data rows
    ///
    /// ## Type Promotion
    /// Bare Numeric values in AE context are promoted to Percentage.
    ///
    /// ## Positional Mapping
    /// Uses ResolvedColumnStart from header columns to map data cells to arms.
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="ValueParser"/>
    public class MultilevelAeTableParser : BaseTableParser
    {
        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports ADVERSE_EVENT category.
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.ADVERSE_EVENT;

        /**************************************************************/
        /// <summary>
        /// Priority 10 — tried before SimpleArmTableParser for multi-level headers.
        /// </summary>
        public override int Priority => 10;

        /**************************************************************/
        /// <summary>
        /// Returns true if the table has 2+ header rows (multi-level header structure).
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True if HeaderRowCount >= 2.</returns>
        public override bool CanParse(ReconstructedTable table)
        {
            #region implementation

            return table.Header?.HeaderRowCount >= 2;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a multi-level AE table: study context from colspan row, arms from
        /// sub-header row. SOC divider rows set ParameterCategory.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            var observations = new List<ParsedObservation>();
            var (population, popConfidence) = detectPopulation(table);

            // Extract arms with study context from multi-level header
            var arms = extractMultilevelArms(table);
            if (arms.Count == 0)
                return observations;

            // Iterate data rows
            string? currentSoc = null;
            var dataRows = getDataBodyRows(table);

            // Enrich arms from body-row header metadata (dose, N=, format hints)
            var skipRows = enrichArmsFromBodyRows(dataRows, arms);
            if (skipRows > 0)
            {
                dataRows = dataRows.Skip(skipRows).ToList();
            }

            foreach (var row in dataRows)
            {
                // SOC divider — update current category
                if (row.Classification == RowClassification.SocDivider)
                {
                    currentSoc = row.SocName;
                    continue;
                }

                var (paramName, fnMarkers) = getParameterName(row);
                if (string.IsNullOrWhiteSpace(paramName))
                    continue;

                // Fault-tolerant row processing: if any cell throws, the entire table is skipped
                parseRowSafe(table, row, observations, (r, obs) =>
                {
                    // Map data cells to arms by column position
                    foreach (var arm in arms)
                    {
                        var cell = getCellAtColumn(r, arm.ColumnIndex ?? 0);
                        if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                            continue;

                        var o = createBaseObservation(table, r, cell, TableCategory.ADVERSE_EVENT);
                        o.ParameterName = paramName;
                        o.ParameterCategory = currentSoc;
                        o.TreatmentArm = arm.Name;
                        o.ArmN = arm.SampleSize;
                        o.StudyContext = arm.StudyContext;
                        o.DoseRegimen = arm.DoseRegimen;
                        o.Population = population;

                        var parsed = ValueParser.Parse(cell.CleanedText, arm.SampleSize);

                        // AE type promotion: Numeric → Percentage
                        if (parsed.PrimaryValueType == "Numeric")
                        {
                            parsed.PrimaryValueType = "Percentage";
                            parsed.Unit = "%";
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
        /// Extracts arm definitions from a multi-level header. Uses HeaderPath[0]
        /// for study context and leaf text for arm name/N.
        /// </summary>
        private static List<ArmDefinition> extractMultilevelArms(ReconstructedTable table)
        {
            #region implementation

            var arms = new List<ArmDefinition>();
            if (table.Header?.Columns == null)
                return arms;

            // Skip column 0 (parameter name column)
            for (int i = 1; i < table.Header.Columns.Count; i++)
            {
                var col = table.Header.Columns[i];
                var leafText = col.LeafHeaderText?.Trim();
                if (string.IsNullOrWhiteSpace(leafText))
                    continue;

                var arm = ValueParser.ParseArmHeader(leafText);
                if (arm == null)
                {
                    // Check for trailing format hint (e.g., "Paroxetine %")
                    var hintMatch = _trailingFormatHintPattern.Match(leafText);
                    arm = new ArmDefinition
                    {
                        Name = hintMatch.Success ? hintMatch.Groups[1].Value.Trim() : leafText,
                        FormatHint = hintMatch.Success ? hintMatch.Groups[2].Value.Trim() : null
                    };
                }

                arm.ColumnIndex = col.ColumnIndex;

                // Study context from parent header path
                if (col.HeaderPath != null && col.HeaderPath.Count > 1)
                {
                    arm.StudyContext = col.HeaderPath[0];
                }

                arms.Add(arm);
            }

            return arms;

            #endregion
        }

        #endregion Private Helpers
    }
}
