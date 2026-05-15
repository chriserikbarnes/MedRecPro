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
        /// Priority 20 - tried after MultilevelAeTableParser but before SimpleArmTableParser.
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

            ClearDiagnostics();
            var arms = extractArmDefinitions(table);
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
    }
}
