using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Interface for routing reconstructed tables to the appropriate Stage 3 parser
    /// based on ParentSectionCode (LOINC) and structural characteristics.
    /// </summary>
    /// <remarks>
    /// ## Routing Strategy
    /// 1. Check skip conditions (patient info, single-column, NDC tables)
    /// 2. Map ParentSectionCode → <see cref="TableCategory"/>
    /// 3. Select parser by category + structural priority
    /// </remarks>
    /// <seealso cref="TableParserRouter"/>
    /// <seealso cref="ITableParser"/>
    /// <seealso cref="TableCategory"/>
    public interface ITableParserRouter
    {
        /**************************************************************/
        /// <summary>
        /// Routes a reconstructed table to a category and parser.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>
        /// Tuple of (category, parser). Parser is null when category is SKIP or
        /// no parser can handle the table structure.
        /// </returns>
        (TableCategory category, ITableParser? parser) Route(ReconstructedTable table);
    }
}
