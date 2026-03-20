namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Read-only DTO for the SPL Table Normalization pipeline's source view assembly (Stage 1).
    /// Joins cell-level data (TextTableCell → TextTableRow → TextTable → SectionTextContent)
    /// with section context (vw_SectionNavigation) and document context (Document) into a flat
    /// 26-column projection for downstream table reconstruction and meta-analysis.
    /// </summary>
    /// <remarks>
    /// This is a projection DTO — not an EF Core entity. It has no [Table] attribute,
    /// no navigation properties, and no primary key. All properties are nullable to match
    /// the source entity conventions.
    ///
    /// ## Property Groups
    /// - **Cell**: TextTableCellID, CellType, CellText, SequenceNumber, RowSpan, ColSpan
    /// - **Row**: TextTableRowID, RowGroupType, SequenceNumberTextTableRow
    /// - **Table**: TextTableID, SectionTextContentID, Caption
    /// - **Content**: ContentType, SequenceNumberSectionTextContent, ContentText
    /// - **Document**: DocumentGUID, Title, VersionNumber
    /// - **Section Nav**: SectionGUID, SectionCode, SectionType, SectionTitle,
    ///   ParentSectionID, ParentSectionCode, ParentSectionTitle, LabelerName
    /// </remarks>
    /// <seealso cref="Label.TextTableCell"/>
    /// <seealso cref="Label.TextTableRow"/>
    /// <seealso cref="Label.TextTable"/>
    /// <seealso cref="Label.SectionTextContent"/>
    /// <seealso cref="Label.Document"/>
    /// <seealso cref="LabelView.SectionNavigation"/>
    public class TableCellContext
    {
        #region Cell Properties

        /**************************************************************/
        /// <summary>
        /// Primary key from TextTableCell.
        /// </summary>
        /// <seealso cref="Label.TextTableCell"/>
        public int? TextTableCellID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Cell element type: 'td' or 'th'.
        /// </summary>
        /// <seealso cref="Label.TextTableCell"/>
        public string? CellType { get; set; }

        /**************************************************************/
        /// <summary>
        /// Text content of the table cell.
        /// </summary>
        /// <seealso cref="Label.TextTableCell"/>
        public string? CellText { get; set; }

        /**************************************************************/
        /// <summary>
        /// Order of the cell within its row (column number).
        /// </summary>
        /// <seealso cref="Label.TextTableCell"/>
        public int? SequenceNumber { get; set; }

        /**************************************************************/
        /// <summary>
        /// Optional rowspan attribute on the cell element.
        /// </summary>
        /// <seealso cref="Label.TextTableCell"/>
        public int? RowSpan { get; set; }

        /**************************************************************/
        /// <summary>
        /// Optional colspan attribute on the cell element.
        /// </summary>
        /// <seealso cref="Label.TextTableCell"/>
        public int? ColSpan { get; set; }

        #endregion Cell Properties

        #region Row Properties

        /**************************************************************/
        /// <summary>
        /// Primary key from TextTableRow.
        /// </summary>
        /// <seealso cref="Label.TextTableRow"/>
        public int? TextTableRowID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Row group type: 'Header', 'Body', or 'Footer' (from thead, tbody, tfoot).
        /// </summary>
        /// <seealso cref="Label.TextTableRow"/>
        public string? RowGroupType { get; set; }

        /**************************************************************/
        /// <summary>
        /// Order of the row within its group. Disambiguated from cell SequenceNumber.
        /// </summary>
        /// <seealso cref="Label.TextTableRow"/>
        public int? SequenceNumberTextTableRow { get; set; }

        #endregion Row Properties

        #region Table Properties

        /**************************************************************/
        /// <summary>
        /// Primary key from TextTable. Used for batch range filtering and grouping.
        /// </summary>
        /// <seealso cref="Label.TextTable"/>
        public int? TextTableID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Foreign key from TextTable to SectionTextContent.
        /// </summary>
        /// <seealso cref="Label.TextTable"/>
        /// <seealso cref="Label.SectionTextContent"/>
        public int? SectionTextContentID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Optional caption text for the table.
        /// </summary>
        /// <seealso cref="Label.TextTable"/>
        public string? Caption { get; set; }

        #endregion Table Properties

        #region Content Properties

        /**************************************************************/
        /// <summary>
        /// Type of content block (Paragraph, List, Table, BlockImage).
        /// </summary>
        /// <seealso cref="Label.SectionTextContent"/>
        public string? ContentType { get; set; }

        /**************************************************************/
        /// <summary>
        /// Order of the content block within the parent section's text element.
        /// Disambiguated from cell SequenceNumber.
        /// </summary>
        /// <seealso cref="Label.SectionTextContent"/>
        public int? SequenceNumberSectionTextContent { get; set; }

        /**************************************************************/
        /// <summary>
        /// Actual text content for the section content block.
        /// </summary>
        /// <seealso cref="Label.SectionTextContent"/>
        public string? ContentText { get; set; }

        #endregion Content Properties

        #region Document Properties

        /**************************************************************/
        /// <summary>
        /// Globally Unique Identifier for the document version.
        /// </summary>
        /// <seealso cref="Label.Document"/>
        public Guid? DocumentGUID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Document title.
        /// </summary>
        /// <seealso cref="Label.Document"/>
        public string? Title { get; set; }

        /**************************************************************/
        /// <summary>
        /// Sequential integer for the document version.
        /// </summary>
        /// <seealso cref="Label.Document"/>
        public int? VersionNumber { get; set; }

        #endregion Document Properties

        #region Section Navigation Properties

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
        /// Parent section ID from vw_SectionNavigation.
        /// </summary>
        /// <seealso cref="LabelView.SectionNavigation"/>
        public int? ParentSectionID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Parent section LOINC code from vw_SectionNavigation.
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

        #endregion Section Navigation Properties
    }
}
