namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Top-level output DTO for Stage 2 of the SPL Table Normalization pipeline.
    /// Represents a fully reconstructed table with classified rows, resolved header
    /// structure, extracted footnotes, and document/section context.
    /// </summary>
    /// <remarks>
    /// ## Stage 2 Output Contract
    /// This DTO is the input for Stage 3 (section-aware parsing). It provides:
    /// - Classified rows (header, body, SOC divider, footer) with cleaned cell text
    /// - Resolved multi-level header structure with column paths
    /// - Footnote dictionary (marker → text) extracted from footer rows
    /// - Document and section context for routing to the correct Stage 3 parser
    ///
    /// ## Property Groups
    /// - **Table**: TextTableID, Caption
    /// - **Document**: DocumentGUID, Title, VersionNumber
    /// - **Section**: SectionGUID, SectionCode, SectionType, SectionTitle,
    ///   ParentSectionCode, ParentSectionTitle, LabelerName
    /// - **Structural**: TotalColumnCount, TotalRowCount, HasExplicitHeader,
    ///   HasInferredHeader, HasFooter, HasSocDividers
    /// - **Content**: Header, Rows, Footnotes
    /// </remarks>
    /// <seealso cref="ReconstructedRow"/>
    /// <seealso cref="ResolvedHeader"/>
    /// <seealso cref="ProcessedCell"/>
    /// <seealso cref="TableCellContext"/>
    public class ReconstructedTable
    {
        #region Table Properties

        /**************************************************************/
        /// <summary>
        /// Primary key of the source TextTable. Grouping key for the reconstruction.
        /// </summary>
        /// <seealso cref="Label.TextTable"/>
        public int? TextTableID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Table caption text (e.g., "Table 1: Mean Pharmacokinetic Parameters...").
        /// Present on ~40% of tables; null on dosing charts, formulas, NDC tables.
        /// </summary>
        /// <seealso cref="Label.TextTable"/>
        public string? Caption { get; set; }

        #endregion Table Properties

        #region Document Properties

        /**************************************************************/
        /// <summary>
        /// Globally Unique Identifier for the source document version.
        /// </summary>
        /// <seealso cref="Label.Document"/>
        public Guid? DocumentGUID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Document title (may contain HTML markup — consumers should strip).
        /// </summary>
        /// <seealso cref="Label.Document"/>
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Label version number — enables cross-version comparison.
        /// </summary>
        /// <seealso cref="Label.Document"/>
        public int? VersionNumber { get; set; }

        /**************************************************************/
        /// <summary>
        /// Plus-delimited active ingredient UNIIs for the source document.
        /// Outermost grouping key for anomaly model partitioning.
        /// </summary>
        /// <seealso cref="LabelView.ActiveIngredientView"/>
        public string? UNII { get; set; }

        #endregion Document Properties

        #region Section Properties

        /**************************************************************/
        /// <summary>
        /// Section GUID from vw_SectionNavigation.
        /// </summary>
        /// <seealso cref="LabelView.SectionNavigation"/>
        public Guid? SectionGUID { get; set; }

        /**************************************************************/
        /// <summary>
        /// LOINC section code from vw_SectionNavigation.
        /// </summary>
        /// <seealso cref="LabelView.SectionNavigation"/>
        public string? SectionCode { get; set; }

        /**************************************************************/
        /// <summary>
        /// Section type name from vw_SectionNavigation.
        /// </summary>
        /// <seealso cref="LabelView.SectionNavigation"/>
        public string? SectionType { get; set; }

        /**************************************************************/
        /// <summary>
        /// Section title from vw_SectionNavigation.
        /// </summary>
        /// <seealso cref="LabelView.SectionNavigation"/>
        public string? SectionTitle { get; set; }

        /**************************************************************/
        /// <summary>
        /// Parent section LOINC code — the primary routing key for Stage 3 parser selection.
        /// </summary>
        /// <seealso cref="LabelView.SectionNavigation"/>
        public string? ParentSectionCode { get; set; }

        /**************************************************************/
        /// <summary>
        /// Parent section title from vw_SectionNavigation.
        /// </summary>
        /// <seealso cref="LabelView.SectionNavigation"/>
        public string? ParentSectionTitle { get; set; }

        /**************************************************************/
        /// <summary>
        /// Labeler (manufacturer) name from vw_SectionNavigation.
        /// </summary>
        /// <seealso cref="LabelView.SectionNavigation"/>
        public string? LabelerName { get; set; }

        #endregion Section Properties

        #region Structural Metadata Properties

        /**************************************************************/
        /// <summary>
        /// Total column count (maximum width across all rows after ColSpan resolution).
        /// </summary>
        public int? TotalColumnCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// Total number of rows across all groups (Header + Body + Footer).
        /// </summary>
        public int? TotalRowCount { get; set; }

        /**************************************************************/
        /// <summary>
        /// True if the table has rows with RowGroupType = "Header" (from SPL thead).
        /// </summary>
        public bool? HasExplicitHeader { get; set; }

        /**************************************************************/
        /// <summary>
        /// True if the first body row was promoted to header (no explicit header rows existed).
        /// </summary>
        public bool? HasInferredHeader { get; set; }

        /**************************************************************/
        /// <summary>
        /// True if the table has Footer rows containing footnote definitions.
        /// </summary>
        public bool? HasFooter { get; set; }

        /**************************************************************/
        /// <summary>
        /// True if the table contains SOC (System Organ Class) divider rows.
        /// </summary>
        public bool? HasSocDividers { get; set; }

        #endregion Structural Metadata Properties

        #region Content Properties

        /**************************************************************/
        /// <summary>
        /// Resolved multi-level header structure with column paths.
        /// </summary>
        /// <seealso cref="ResolvedHeader"/>
        public ResolvedHeader? Header { get; set; }

        /**************************************************************/
        /// <summary>
        /// All classified rows in order (Header → Body → Footer), each containing processed cells.
        /// </summary>
        /// <seealso cref="ReconstructedRow"/>
        public List<ReconstructedRow>? Rows { get; set; }

        /**************************************************************/
        /// <summary>
        /// Footnotes extracted from Footer rows as marker → text pairs.
        /// </summary>
        /// <remarks>
        /// Keys are footnote markers (e.g., "a", "*", "†").
        /// Values are the corresponding footnote text definitions.
        /// </remarks>
        /// <example>
        /// <code>
        /// // {"a": "Includes patients who discontinued early", "*": "p &lt; 0.05"}
        /// </code>
        /// </example>
        public Dictionary<string, string>? Footnotes { get; set; }

        #endregion Content Properties
    }
}
