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
        /// Priority 10 - tried before SimpleArmTableParser for multi-level headers.
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

            ClearDiagnostics();
            var arms = extractMultilevelArms(table);
            var captionStudyContext = extractStudyContextFromCaption(table.Caption);

            return parseAdverseEventArmRows(
                table,
                arms,
                captionStudyContext,
                socDividersSetCategory: true,
                emptyDataRowsSetCategory: false,
                structuralRowReason: "Structural AE/SOC row captured as category context",
                structuralCellReason: "Structural AE cell suppressed before observation emission");

            #endregion
        }

        #endregion ITableParser Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Extracts arm definitions from a multi-level header. Uses HeaderPath[0]
        /// for study context and leaf text for arm name/N.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>Resolved arm definitions.</returns>
        /// <seealso cref="ArmDefinition"/>
        private static List<ArmDefinition> extractMultilevelArms(ReconstructedTable table)
        {
            #region implementation

            return extractArmDefinitions(table);

            #endregion
        }

        #endregion Private Helpers
    }
}
