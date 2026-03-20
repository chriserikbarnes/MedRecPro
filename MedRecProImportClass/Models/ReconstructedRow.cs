namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Classification of a row's role within a reconstructed table.
    /// </summary>
    /// <remarks>
    /// Row classification determines how downstream Stage 3 parsers interpret each row.
    /// Only ~0.5% of SPL rows carry explicit Header RowGroupType; most tables encode
    /// headers as the first Body row, which is always promoted to InferredHeader.
    /// </remarks>
    /// <seealso cref="ReconstructedRow"/>
    public enum RowClassification
    {
        /**************************************************************/
        /// <summary>
        /// Row from SPL thead (RowGroupType = "Header"). Rare (~0.5% of rows).
        /// </summary>
        ExplicitHeader,

        /**************************************************************/
        /// <summary>
        /// First body row promoted to header. Common SPL pattern where column labels
        /// appear in the first Body row rather than in a Header row.
        /// </summary>
        InferredHeader,

        /**************************************************************/
        /// <summary>
        /// Additional consecutive header rows beyond the first (multi-level headers).
        /// Identified by all cells having CellType = "th" in body rows following
        /// an InferredHeader or ExplicitHeader.
        /// </summary>
        ContinuationHeader,

        /**************************************************************/
        /// <summary>
        /// SOC (System Organ Class) divider row — a single cell spanning the full
        /// table width containing a category name (e.g., "Body as a Whole", "Nervous System").
        /// </summary>
        SocDivider,

        /**************************************************************/
        /// <summary>
        /// Normal data row containing observation values.
        /// </summary>
        DataBody,

        /**************************************************************/
        /// <summary>
        /// Footer row containing footnote definitions (RowGroupType = "Footer").
        /// </summary>
        Footer
    }

    /**************************************************************/
    /// <summary>
    /// Represents a classified row within a reconstructed table in Stage 2 of the SPL Table
    /// Normalization pipeline. Contains the original row metadata, computed classification,
    /// and processed cells in column order.
    /// </summary>
    /// <remarks>
    /// ## Classification Logic
    /// 1. Rows with RowGroupType = "Header" → ExplicitHeader
    /// 2. Rows with RowGroupType = "Footer" → Footer
    /// 3. First body row (when no ExplicitHeader exists) → InferredHeader
    /// 4. Consecutive body rows with all CellType = "th" → ContinuationHeader
    /// 5. Single-cell full-span body rows → SocDivider
    /// 6. Everything else → DataBody
    /// </remarks>
    /// <seealso cref="ProcessedCell"/>
    /// <seealso cref="RowClassification"/>
    /// <seealso cref="ReconstructedTable"/>
    public class ReconstructedRow
    {
        #region Identity Properties

        /**************************************************************/
        /// <summary>
        /// Primary key from the source TextTableRow.
        /// </summary>
        /// <seealso cref="Label.TextTableRow"/>
        public int? TextTableRowID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Original SPL row group: "Header", "Body", or "Footer" (from thead, tbody, tfoot).
        /// </summary>
        /// <seealso cref="Label.TextTableRow"/>
        public string? RowGroupType { get; set; }

        /**************************************************************/
        /// <summary>
        /// Row position within its original group (1-based).
        /// </summary>
        /// <seealso cref="Label.TextTableRow"/>
        public int? SequenceNumberTextTableRow { get; set; }

        #endregion Identity Properties

        #region Computed Properties

        /**************************************************************/
        /// <summary>
        /// Computed classification of this row's role within the table.
        /// </summary>
        /// <seealso cref="RowClassification"/>
        public RowClassification? Classification { get; set; }

        /**************************************************************/
        /// <summary>
        /// 0-based row index across all groups (Header → Body → Footer), assigned
        /// after classification for positional reference.
        /// </summary>
        public int? AbsoluteRowIndex { get; set; }

        #endregion Computed Properties

        #region Content Properties

        /**************************************************************/
        /// <summary>
        /// Processed cells in column order within this row.
        /// </summary>
        /// <seealso cref="ProcessedCell"/>
        public List<ProcessedCell>? Cells { get; set; }

        /**************************************************************/
        /// <summary>
        /// For SocDivider rows only: the extracted System Organ Class name
        /// (e.g., "Body as a Whole", "Nervous System").
        /// </summary>
        /// <remarks>
        /// Null for all non-SocDivider rows. Populated from the single cell's CleanedText.
        /// Becomes ParameterCategory in the Stage 4 normalized output.
        /// </remarks>
        public string? SocName { get; set; }

        #endregion Content Properties
    }
}
