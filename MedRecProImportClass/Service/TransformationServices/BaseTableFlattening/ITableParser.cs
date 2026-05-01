using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Interface for Stage 3 table parsers in the SPL Table Normalization pipeline.
    /// Each implementation handles a specific table structure (PK, AE, Efficacy, etc.)
    /// and decomposes cell values into <see cref="ParsedObservation"/> rows.
    /// </summary>
    /// <remarks>
    /// ## Parser Selection
    /// The <see cref="ITableParserRouter"/> selects the appropriate parser based on
    /// <see cref="TableCategory"/> and structural characteristics. When multiple parsers
    /// support the same category, <see cref="Priority"/> determines evaluation order
    /// (lower = tried first), and <see cref="CanParse"/> provides the final structural check.
    ///
    /// ## Contract
    /// - <see cref="SupportedCategory"/>: Declares the table category this parser handles
    /// - <see cref="Priority"/>: Lower values are tried first within the same category
    /// - <see cref="CanParse"/>: Structural validation (header depth, column count, etc.)
    /// - <see cref="Parse"/>: Produces one <see cref="ParsedObservation"/> per data cell
    /// </remarks>
    /// <seealso cref="BaseTableParser"/>
    /// <seealso cref="ITableParserRouter"/>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="ReconstructedTable"/>
    public interface ITableParser
    {
        /**************************************************************/
        /// <summary>
        /// The table category this parser is designed to handle.
        /// </summary>
        /// <seealso cref="TableCategory"/>
        TableCategory SupportedCategory { get; }

        /**************************************************************/
        /// <summary>
        /// Selection priority within the same category. Lower values are tried first
        /// (more specific parsers win). Example: MultilevelAeTableParser(10) before SimpleArmTableParser(30).
        /// </summary>
        int Priority { get; }

        /**************************************************************/
        /// <summary>
        /// Determines whether this parser can handle the given table based on its
        /// structural characteristics (header depth, SOC dividers, column count, etc.).
        /// </summary>
        /// <param name="table">The reconstructed table to evaluate.</param>
        /// <returns>True if this parser can parse the table; false to try the next parser in priority order.</returns>
        bool CanParse(ReconstructedTable table);

        /**************************************************************/
        /// <summary>
        /// Parses a reconstructed table into a flat list of observations.
        /// Each data cell becomes one <see cref="ParsedObservation"/> with fully
        /// decomposed values, provenance, and classification fields.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>
        /// List of parsed observations. Empty list (not null) if no data cells were found.
        /// </returns>
        /// <example>
        /// <code>
        /// var parser = new SimpleArmTableParser();
        /// if (parser.CanParse(table))
        /// {
        ///     var observations = parser.Parse(table);
        ///     // observations.Count == dataRows * armColumns
        /// }
        /// </code>
        /// </example>
        List<ParsedObservation> Parse(ReconstructedTable table);
    }
}
