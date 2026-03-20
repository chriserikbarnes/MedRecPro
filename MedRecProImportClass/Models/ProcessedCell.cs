namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents a single table cell after HTML processing in Stage 2 of the SPL Table
    /// Normalization pipeline. Contains cleaned text, extracted footnote markers, resolved
    /// column positions (after ColSpan/RowSpan resolution), and extracted styleCode attributes.
    /// </summary>
    /// <remarks>
    /// This is a projection DTO — not an EF Core entity. It has no [Table] attribute,
    /// no navigation properties, and no primary key. All properties are nullable to match
    /// the source entity conventions.
    ///
    /// ## Processing Steps
    /// 1. Footnote markers are extracted from &lt;sup&gt; tags before HTML stripping
    /// 2. styleCode attributes are extracted from HTML before stripping
    /// 3. All HTML is stripped and whitespace normalized to produce CleanedText
    /// 4. Column positions are resolved via occupancy grid after all cells are processed
    ///
    /// ## Property Groups
    /// - **Identity**: TextTableCellID, TextTableRowID
    /// - **Position**: SequenceNumber, ResolvedColumnStart, ResolvedColumnEnd
    /// - **Span**: RowSpan, ColSpan
    /// - **Type**: CellType
    /// - **Text**: CleanedText, RawCellText
    /// - **Footnotes**: FootnoteMarkers, IsFootnoteOnly
    /// - **Style**: StyleCode
    /// </remarks>
    /// <seealso cref="TableCellContext"/>
    /// <seealso cref="ReconstructedRow"/>
    /// <seealso cref="ReconstructedTable"/>
    public class ProcessedCell
    {
        #region Identity Properties

        /**************************************************************/
        /// <summary>
        /// Primary key from the source TextTableCell.
        /// </summary>
        /// <seealso cref="Label.TextTableCell"/>
        public int? TextTableCellID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Foreign key to the parent TextTableRow.
        /// </summary>
        /// <seealso cref="Label.TextTableRow"/>
        public int? TextTableRowID { get; set; }

        #endregion Identity Properties

        #region Position Properties

        /**************************************************************/
        /// <summary>
        /// Original column position within the row (1-based, from source SequenceNumber).
        /// </summary>
        /// <seealso cref="Label.TextTableCell"/>
        public int? SequenceNumber { get; set; }

        /**************************************************************/
        /// <summary>
        /// Absolute column index (0-based) after ColSpan/RowSpan resolution via occupancy grid.
        /// </summary>
        /// <remarks>
        /// Populated by the column position resolution algorithm. Accounts for
        /// RowSpan from cells in prior rows occupying column slots.
        /// </remarks>
        public int? ResolvedColumnStart { get; set; }

        /**************************************************************/
        /// <summary>
        /// Exclusive end column index: ResolvedColumnStart + (ColSpan ?? 1).
        /// </summary>
        public int? ResolvedColumnEnd { get; set; }

        #endregion Position Properties

        #region Span Properties

        /**************************************************************/
        /// <summary>
        /// Row span from source. Null means 1 (no span).
        /// </summary>
        /// <seealso cref="Label.TextTableCell"/>
        public int? RowSpan { get; set; }

        /**************************************************************/
        /// <summary>
        /// Column span from source. Null means 1 (no span).
        /// </summary>
        /// <seealso cref="Label.TextTableCell"/>
        public int? ColSpan { get; set; }

        #endregion Span Properties

        #region Type Properties

        /**************************************************************/
        /// <summary>
        /// Cell element type: 'td' or 'th'.
        /// </summary>
        /// <seealso cref="Label.TextTableCell"/>
        public string? CellType { get; set; }

        #endregion Type Properties

        #region Text Properties

        /**************************************************************/
        /// <summary>
        /// Cell text after HTML stripping and whitespace normalization.
        /// </summary>
        /// <remarks>
        /// Produced by stripping all HTML tags (after footnote and styleCode extraction)
        /// and collapsing multiple whitespace characters into single spaces.
        /// </remarks>
        public string? CleanedText { get; set; }

        /**************************************************************/
        /// <summary>
        /// Original CellText preserved for tracing and debugging.
        /// </summary>
        /// <remarks>
        /// Contains raw HTML markup including &lt;paragraph&gt;, &lt;content&gt;,
        /// &lt;sub&gt;, &lt;sup&gt;, &lt;br /&gt;, and styleCode attributes.
        /// </remarks>
        public string? RawCellText { get; set; }

        #endregion Text Properties

        #region Footnote Properties

        /**************************************************************/
        /// <summary>
        /// Footnote markers extracted from &lt;sup&gt; tags before HTML stripping.
        /// </summary>
        /// <remarks>
        /// Common markers include: "a", "b", "c", "*", "†", "‡", "1", "2", "3".
        /// Comma-separated markers within a single &lt;sup&gt; tag are split into
        /// individual entries.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Input: "Nausea&lt;sup&gt;a,b&lt;/sup&gt;"
        /// // Result: ["a", "b"]
        /// </code>
        /// </example>
        public List<string>? FootnoteMarkers { get; set; }

        /**************************************************************/
        /// <summary>
        /// True if the cell contained only a footnote marker with no other meaningful text.
        /// </summary>
        public bool? IsFootnoteOnly { get; set; }

        #endregion Footnote Properties

        #region Style Properties

        /**************************************************************/
        /// <summary>
        /// Extracted styleCode attribute value from the cell's HTML content.
        /// </summary>
        /// <remarks>
        /// Common values: "bold", "italics", "Botrule", "Lrule", "Rrule", "Toprule",
        /// "Lrule Rrule". Used by Stage 3 for header inference and formatting context.
        /// </remarks>
        /// <seealso cref="Label.TextTableCell"/>
        public string? StyleCode { get; set; }

        #endregion Style Properties
    }
}
