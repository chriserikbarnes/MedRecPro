using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Parser for single-header AE tables with SOC (System Organ Class) divider rows
    /// in the body. Like <see cref="SimpleArmTableParser"/> but propagates SOC category
    /// from divider rows into ParameterCategory.
    /// </summary>
    /// <remarks>
    /// ## Table Structure
    /// - Single-row header with arm definitions
    /// - Body contains SOC divider rows (single cell spanning full width)
    /// - DataBody rows following a SOC divider inherit its category
    ///
    /// ## Type Promotion
    /// Bare Numeric values are promoted to Percentage in AE context.
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="SimpleArmTableParser"/>
    public class AeWithSocTableParser : BaseTableParser
    {
        #region ITableParser Implementation

        /**************************************************************/
        /// <summary>
        /// Supports ADVERSE_EVENT category.
        /// </summary>
        public override TableCategory SupportedCategory => TableCategory.ADVERSE_EVENT;

        /**************************************************************/
        /// <summary>
        /// Priority 20 — tried after MultilevelAeTableParser but before SimpleArmTableParser.
        /// </summary>
        public override int Priority => 20;

        /**************************************************************/
        /// <summary>
        /// Returns true if the table has SOC divider rows.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>True if HasSocDividers is true.</returns>
        public override bool CanParse(ReconstructedTable table)
        {
            #region implementation

            return table.HasSocDividers == true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses an AE table with SOC dividers. Propagates SOC name from divider
        /// rows into ParameterCategory for subsequent data rows.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public override List<ParsedObservation> Parse(ReconstructedTable table)
        {
            #region implementation

            var observations = new List<ParsedObservation>();
            var (population, popConfidence) = detectPopulation(table);

            // Extract arm definitions from header
            var arms = extractArmDefinitions(table);
            if (arms.Count == 0)
                return observations;

            // Iterate data rows with SOC propagation
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
                        o.Dose = arm.Dose;
                        o.DoseUnit = arm.DoseUnit;
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
    }
}
