namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents a single resolved column in a multi-level header structure.
    /// Each column carries its full header path from outermost to leaf level.
    /// </summary>
    /// <remarks>
    /// For a two-level header like:
    /// <code>
    /// Row 1: |           | Treatment [span=2]  | Prevention [span=2] |
    /// Row 2: | Adverse   | Drug A   | Placebo  | Drug A   | Placebo  |
    /// </code>
    /// The "Drug A" column under "Treatment" would have:
    /// - HeaderPath = ["Treatment", "Drug A"]
    /// - LeafHeaderText = "Drug A"
    /// - CombinedHeaderText = "Treatment > Drug A"
    /// </remarks>
    /// <seealso cref="ResolvedHeader"/>
    /// <seealso cref="ProcessedCell"/>
    public class HeaderColumn
    {
        /**************************************************************/
        /// <summary>
        /// 0-based resolved column index within the table.
        /// </summary>
        public int? ColumnIndex { get; set; }

        /**************************************************************/
        /// <summary>
        /// Most specific (deepest) header label for this column.
        /// </summary>
        /// <remarks>
        /// For single-level headers, this equals the only header text.
        /// For multi-level headers, this is the leaf entry from the deepest header row.
        /// </remarks>
        public string? LeafHeaderText { get; set; }

        /**************************************************************/
        /// <summary>
        /// Full header path from outermost to leaf level.
        /// </summary>
        /// <remarks>
        /// For a single-level header: ["Adverse Reaction"].
        /// For a two-level header: ["Treatment", "Drug A (N=188) %"].
        /// </remarks>
        /// <example>
        /// <code>
        /// // Two-level header: "Treatment" spanning "Drug A" and "Placebo"
        /// var path = column.HeaderPath; // ["Treatment", "Drug A"]
        /// </code>
        /// </example>
        public List<string>? HeaderPath { get; set; }

        /**************************************************************/
        /// <summary>
        /// Combined display text joining the header path with " > " separator.
        /// </summary>
        /// <remarks>
        /// Single level: "Adverse Reaction".
        /// Two levels: "Treatment > Drug A".
        /// Null when no header rows exist.
        /// </remarks>
        public string? CombinedHeaderText { get; set; }

        /**************************************************************/
        /// <summary>
        /// Footnote markers collected from header cells covering this column.
        /// </summary>
        public List<string>? HeaderFootnoteMarkers { get; set; }
    }

    /**************************************************************/
    /// <summary>
    /// Represents the resolved multi-level header structure for a reconstructed table.
    /// Built by walking classified header rows (ExplicitHeader, InferredHeader, ContinuationHeader)
    /// and mapping each leaf column to its full header path.
    /// </summary>
    /// <remarks>
    /// ## Resolution Algorithm
    /// 1. Collect all rows classified as header types, ordered by AbsoluteRowIndex
    /// 2. For each column index 0..ColumnCount-1:
    ///    - Walk header rows top-to-bottom
    ///    - Find the cell covering that column (via ResolvedColumnStart ≤ col &lt; ResolvedColumnEnd)
    ///    - Build HeaderPath from outermost to leaf
    /// 3. CombinedHeaderText = string.Join(" > ", path)
    /// </remarks>
    /// <seealso cref="HeaderColumn"/>
    /// <seealso cref="ReconstructedRow"/>
    /// <seealso cref="ReconstructedTable"/>
    public class ResolvedHeader
    {
        /**************************************************************/
        /// <summary>
        /// Number of rows consumed to build this header structure.
        /// </summary>
        /// <remarks>
        /// 0 when no header rows exist, 1 for single-level, 2+ for multi-level headers.
        /// </remarks>
        public int? HeaderRowCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Total column count (maximum width across all rows in the table).
        /// </summary>
        public int? ColumnCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Resolved columns in order from index 0 to ColumnCount-1.
        /// </summary>
        /// <seealso cref="HeaderColumn"/>
        public List<HeaderColumn>? Columns { get; set; }
    }
}
