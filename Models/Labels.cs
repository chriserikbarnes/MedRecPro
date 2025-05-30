using System;

namespace MedRecPro.DataModels
{
    /*******************************************************************************/
    /// <summary>
    /// Stores the main metadata for each SPL document version. Based on Section 2.1.3.
    /// </summary>
    public class Document
    {
        #region properties
        /// <summary>
        /// Primary key for the Document table.
        /// </summary>
        public int? DocumentID { get; set; } // Made nullable

        /// <summary>
        /// Globally Unique Identifier for this specific document version (&lt;id root&gt;).
        /// </summary>
        public Guid? DocumentGUID { get; set; } // Made nullable

        /// <summary>
        /// LOINC code identifying the document type (&lt;code&gt; code).
        /// </summary>
        public string? DocumentCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for the document type code (&lt;code&gt; codeSystem), typically 2.16.840.1.113883.6.1.
        /// </summary>
        public string? DocumentCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name matching the document type code (&lt;code&gt; displayName).
        /// </summary>
        public string? DocumentDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Document title (&lt;title&gt;), if provided.
        /// </summary>
        public string? Title { get; set; } // Already nullable

        /// <summary>
        /// Date reference for the SPL version (&lt;effectiveTime value&gt;).
        /// </summary>
        public DateTime? EffectiveTime { get; set; } // Made nullable

        /// <summary>
        /// Globally Unique Identifier for the document set, constant across versions (&lt;setId root&gt;).
        /// </summary>
        public Guid? SetGUID { get; set; } // Made nullable

        /// <summary>
        /// Sequential integer for the document version (&lt;versionNumber value&gt;).
        /// </summary>
        public int? VersionNumber { get; set; } // Made nullable

        /// <summary>
        /// Name of the submitted XML file (e.g., DocumentGUID.xml).
        /// </summary>
        public string? SubmissionFileName { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores information about organizations (e.g., labelers, registrants, establishments). Identifiers (DUNS, FEI, Labeler Code etc) stored in OrganizationIdentifier table. Based on Section 2.1.4, 2.1.5.
    /// </summary>
    public class Organization
    {
        #region properties
        /// <summary>
        /// Primary key for the Organization table.
        /// </summary>
        public int? OrganizationID { get; set; } // Made nullable

        /// <summary>
        /// Name of the organization (&lt;name&gt;).
        /// </summary>
        public string? OrganizationName { get; set; } // Already nullable

        /// <summary>
        /// Flag indicating if the organization information is confidential (&lt;confidentialityCode code="B"&gt;).
        /// </summary>
        public bool? IsConfidential { get; set; } // Made nullable (Default is 0 (false) in SQL)
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores address information for organizations or contact parties. Based on Section 2.1.6.
    /// </summary>
    public class Address
    {
        #region properties
        /// <summary>
        /// Primary key for the Address table.
        /// </summary>
        public int? AddressID { get; set; } // Made nullable

        /// <summary>
        /// First line of the street address (&lt;streetAddressLine&gt;).
        /// </summary>
        public string? StreetAddressLine1 { get; set; } // Made nullable

        /// <summary>
        /// Second line of the street address (&lt;streetAddressLine&gt;), optional.
        /// </summary>
        public string? StreetAddressLine2 { get; set; } // Already nullable

        /// <summary>
        /// City name (&lt;city&gt;).
        /// </summary>
        public string? City { get; set; } // Made nullable

        /// <summary>
        /// State or province (&lt;state&gt;), required if country is USA.
        /// </summary>
        public string? StateProvince { get; set; } // Already nullable

        /// <summary>
        /// Postal or ZIP code (&lt;postalCode&gt;), required if country is USA.
        /// </summary>
        public string? PostalCode { get; set; } // Already nullable

        /// <summary>
        /// ISO 3166-1 alpha-3 country code (&lt;country code&gt;).
        /// </summary>
        public string? CountryCode { get; set; } // Made nullable

        /// <summary>
        /// Full country name (&lt;country&gt; name).
        /// </summary>
        public string? CountryName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores telecommunication details (phone, email, fax) for organizations or contact parties. Based on Section 2.1.7.
    /// </summary>
    public class Telecom
    {
        #region properties
        /// <summary>
        /// Primary key for the Telecom table.
        /// </summary>
        public int? TelecomID { get; set; } // Made nullable

        /// <summary>
        /// Type of telecommunication: "tel", "mailto", or "fax".
        /// </summary>
        public string? TelecomType { get; set; } // Made nullable

        /// <summary>
        /// The telecommunication value, prefixed with type (e.g., "tel:+1-...", "mailto:...", "fax:+1-...").
        /// </summary>
        public string? TelecomValue { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores contact person details. Based on Section 2.1.8.
    /// </summary>
    public class ContactPerson
    {
        #region properties
        /// <summary>
        /// Primary key for the ContactPerson table.
        /// </summary>
        public int? ContactPersonID { get; set; } // Made nullable

        /// <summary>
        /// Name of the contact person (&lt;contactPerson&gt;&lt;name&gt;).
        /// </summary>
        public string? ContactPersonName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Represents the &lt;contactParty&gt; element, linking Organization, Address, Telecom, and ContactPerson. Based on Section 2.1.8.
    /// </summary>
    public class ContactParty
    {
        #region properties
        /// <summary>
        /// Primary key for the ContactParty table.
        /// </summary>
        public int? ContactPartyID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key linking to the Organization this contact party belongs to.
        /// </summary>
        public int? OrganizationID { get; set; } // Already nullable

        /// <summary>
        /// Foreign key linking to the Address for this contact party.
        /// </summary>
        public int? AddressID { get; set; } // Already nullable

        /// <summary>
        /// Foreign key linking to the ContactPerson for this contact party.
        /// </summary>
        public int? ContactPersonID { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Junction table to link ContactParty with multiple Telecom entries (typically tel and mailto [cite: 104]).
    /// </summary>
    public class ContactPartyTelecom
    {
        #region properties
        /// <summary>
        /// Primary key for the ContactPartyTelecom table.
        /// </summary>
        public int? ContactPartyTelecomID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to ContactParty.
        /// </summary>
        public int? ContactPartyID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Telecom.
        /// </summary>
        public int? TelecomID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Junction table to link Organizations directly with Telecom entries (e.g., for US Agents or facility phones without a full ContactParty).
    /// </summary>
    public class OrganizationTelecom
    {
        #region properties
        /// <summary>
        /// Primary key for the OrganizationTelecom table.
        /// </summary>
        public int? OrganizationTelecomID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Organization.
        /// </summary>
        public int? OrganizationID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Telecom.
        /// </summary>
        public int? TelecomID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Links Documents to Authoring Organizations (typically the Labeler). Based on Section 2.1.4.
    /// </summary>
    public class DocumentAuthor
    {
        #region properties
        /// <summary>
        /// Primary key for the DocumentAuthor table.
        /// </summary>
        public int? DocumentAuthorID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Document.
        /// </summary>
        public int? DocumentID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Organization (the authoring org, e.g., Labeler [cite: 786]).
        /// </summary>
        public int? OrganizationID { get; set; } // Made nullable

        /// <summary>
        /// Identifies the type or role of the author, e.g., Labeler (4.1.2 [cite: 785]), FDA (8.1.2[cite: 945], 15.1.2[cite: 1030], 20.1.2[cite: 1295], 21.1.2[cite: 1330], 30.1.2[cite: 1553], 31.1.2[cite: 1580], 32.1.2[cite: 1607], 33.1.2 [cite: 1643]), NCPDP (12.1.2 [cite: 995]).
        /// </summary>
        public string? AuthorType { get; set; } // Made nullable (Default is 'Labeler' in SQL)
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores references to other documents (e.g., Core Document[cite: 110], Predecessor[cite: 118], Reference Labeling[cite: 1031, 1298, 1332, 1608, 1644], Subject [cite: 1363, 1556, 1599]). Based on Sections 2.1.10, 2.1.11, 15.1.3, 20.1.3, 21.1.3, 23.1.3, 30.1.3, 31.1.5, 32.1.3, 33.1.3.
    /// </summary>
    public class RelatedDocument
    {
        #region properties
        /// <summary>
        /// Primary key for the RelatedDocument table.
        /// </summary>
        public int? RelatedDocumentID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Document (The document containing the reference).
        /// </summary>
        public int? SourceDocumentID { get; set; } // Made nullable

        /// <summary>
        /// Code indicating the type of relationship (e.g., APND for core doc, RPLC for predecessor, DRIV for reference labeling, SUBJ for subject, XCRPT for excerpt).
        /// </summary>
        public string? RelationshipTypeCode { get; set; } // Made nullable

        /// <summary>
        /// Set GUID of the related/referenced document (&lt;setId root&gt;).
        /// </summary>
        public Guid? ReferencedSetGUID { get; set; } // Made nullable

        /// <summary>
        /// Document GUID of the related/referenced document (&lt;id root&gt;), used for RPLC relationship.
        /// </summary>
        public Guid? ReferencedDocumentGUID { get; set; } // Already nullable

        /// <summary>
        /// Version number of the related/referenced document (&lt;versionNumber value&gt;).
        /// </summary>
        public int? ReferencedVersionNumber { get; set; } // Already nullable

        /// <summary>
        /// Document type code, system, and display name of the related/referenced document (&lt;code&gt;), used for RPLC relationship.
        /// </summary>
        public string? ReferencedDocumentCode { get; set; } // Already nullable

        /// <summary>
        /// Corresponds to &lt;code&gt; codeSystem of the referenced document (used in RPLC).
        /// </summary>
        public string? ReferencedDocumentCodeSystem { get; set; } // Already nullable

        /// <summary>
        /// Corresponds to &lt;code&gt; displayName of the referenced document (used in RPLC).
        /// </summary>
        public string? ReferencedDocumentDisplayName { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Defines hierarchical relationships between organizations within a document header (e.g., Labeler -&gt; Registrant -&gt; Establishment).
    /// </summary>
    public class DocumentRelationship
    {
        #region properties
        /// <summary>
        /// Primary key for the DocumentRelationship table.
        /// </summary>
        public int? DocumentRelationshipID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Document.
        /// </summary>
        public int? DocumentID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Organization (e.g., Labeler).
        /// </summary>
        public int? ParentOrganizationID { get; set; } // Already nullable

        /// <summary>
        /// Foreign key to Organization (e.g., Registrant or Establishment).
        /// </summary>
        public int? ChildOrganizationID { get; set; } // Made nullable

        /// <summary>
        /// Describes the specific relationship, e.g., LabelerToRegistrant (4.1.3 [cite: 788]), RegistrantToEstablishment (4.1.4 [cite: 791]), EstablishmentToUSagent (6.1.4 [cite: 914]), EstablishmentToImporter (6.1.5 [cite: 918]), LabelerToDetails (5.1.3 [cite: 863]), FacilityToParentCompany (35.1.6 [cite: 1695]), LabelerToParentCompany (36.1.2.5 [cite: 1719]), DocumentToBulkLotManufacturer (16.1.3).
        /// </summary>
        public string? RelationshipType { get; set; } // Made nullable

        /// <summary>
        /// Indicates the level in the hierarchy (e.g., 1 for Labeler, 2 for Registrant, 3 for Establishment).
        /// </summary>
        public int? RelationshipLevel { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Represents the main &lt;structuredBody&gt; container within a Document. Based on Section 2.2.
    /// </summary>
    public class StructuredBody
    {
        #region properties
        /// <summary>
        /// Primary key for the StructuredBody table.
        /// </summary>
        public int? StructuredBodyID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Document.
        /// </summary>
        public int? DocumentID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores details for each &lt;section&gt; within the StructuredBody. Based on Section 2.2.1.
    /// </summary>
    public class Section
    {
        #region properties
        /// <summary>
        /// Primary key for the Section table.
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to StructuredBody (for top-level sections).
        /// </summary>
        public int? StructuredBodyID { get; set; } // Made nullable

        /// <summary>
        /// Unique identifier for the section (&lt;id root&gt;).
        /// </summary>
        public Guid? SectionGUID { get; set; } // Made nullable

        /// <summary>
        /// LOINC code for the section type (&lt;code&gt; code).
        /// </summary>
        public string? SectionCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for the section code (&lt;code&gt; codeSystem), typically 2.16.840.1.113883.6.1.
        /// </summary>
        public string? SectionCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name matching the section code (&lt;code&gt; displayName).
        /// </summary>
        public string? SectionDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Title of the section (&lt;title&gt;), may include numbering.
        /// </summary>
        public string? Title { get; set; } // Already nullable

        /// <summary>
        /// Effective time for the section (&lt;effectiveTime value&gt;). For Compounded Drug Labels (Sec 4.2.2), low/high represent the reporting period.
        /// </summary>
        public DateTime? EffectiveTime { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Manages the nested structure (parent-child relationships) of sections using &lt;component&gt;&lt;section&gt;. Based on Section 2.2.1.
    /// </summary>
    public class SectionHierarchy
    {
        #region properties
        /// <summary>
        /// Primary key for the SectionHierarchy table.
        /// </summary>
        public int? SectionHierarchyID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Section (The parent section).
        /// </summary>
        public int? ParentSectionID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Section (The child/nested section).
        /// </summary>
        public int? ChildSectionID { get; set; } // Made nullable

        /// <summary>
        /// Order of the child section within the parent.
        /// </summary>
        public int? SequenceNumber { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the main content blocks (&lt;paragraph&gt;, &lt;list&gt;, &lt;table&gt;, block &lt;renderMultimedia&gt;) within a section's &lt;text&gt; element. Based on Section 2.2.2.
    /// </summary>
    public class SectionTextContent
    {
        #region properties
        /// <summary>
        /// Primary key for the SectionTextContent table.
        /// </summary>
        public int? SectionTextContentID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Section.
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// Type of content block: Paragraph, List, Table, BlockImage (for &lt;renderMultimedia&gt; as direct child of &lt;text&gt;).
        /// </summary>
        public string? ContentType { get; set; } // Made nullable

        /// <summary>
        /// Order of this content block within the parent section's &lt;text&gt; element.
        /// </summary>
        public int? SequenceNumber { get; set; } // Made nullable

        /// <summary>
        /// Actual text for Paragraphs. For List/Table types, details are in related tables. Inline markup (bold, italic, links etc) handled separately.
        /// </summary>
        public string? ContentText { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores details specific to &lt;list&gt; elements. Based on Section 2.2.2.4.
    /// </summary>
    public class TextList
    {
        #region properties
        /// <summary>
        /// Primary key for the TextList table.
        /// </summary>
        public int? TextListID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to SectionTextContent (where ContentType='List').
        /// </summary>
        public int? SectionTextContentID { get; set; } // Made nullable

        /// <summary>
        /// Attribute identifying the list as ordered or unordered (&lt;list listType=&gt;).
        /// </summary>
        public string? ListType { get; set; } // Made nullable

        /// <summary>
        /// Optional style code for numbering/bullet style (&lt;list styleCode=&gt;).
        /// </summary>
        public string? StyleCode { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores individual &lt;item&gt; elements within a &lt;list&gt;. Based on Section 2.2.2.4.
    /// </summary>
    public class TextListItem
    {
        #region properties
        /// <summary>
        /// Primary key for the TextListItem table.
        /// </summary>
        public int? TextListItemID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to TextList.
        /// </summary>
        public int? TextListID { get; set; } // Made nullable

        /// <summary>
        /// Order of the item within the list.
        /// </summary>
        public int? SequenceNumber { get; set; } // Made nullable

        /// <summary>
        /// Optional custom marker specified using &lt;caption&gt; within &lt;item&gt;.
        /// </summary>
        public string? ItemCaption { get; set; } // Already nullable

        /// <summary>
        /// Text content of the list item &lt;item&gt;.
        /// </summary>
        public string? ItemText { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores details specific to &lt;table&gt; elements. Based on Section 2.2.2.5.
    /// </summary>
    public class TextTable
    {
        #region properties
        /// <summary>
        /// Primary key for the TextTable table.
        /// </summary>
        public int? TextTableID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to SectionTextContent (where ContentType='Table').
        /// </summary>
        public int? SectionTextContentID { get; set; } // Made nullable

        /// <summary>
        /// Optional width attribute specified on the &lt;table&gt; element.
        /// </summary>
        public string? Width { get; set; } // Already nullable

        /// <summary>
        /// Indicates if the table included a &lt;thead&gt; element.
        /// </summary>
        public bool? HasHeader { get; set; } // Made nullable (Default is 0 (false) in SQL)

        /// <summary>
        /// Indicates if the table included a &lt;tfoot&gt; element.
        /// </summary>
        public bool? HasFooter { get; set; } // Made nullable (Default is 0 (false) in SQL)
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores individual &lt;tr&gt; elements within a &lt;table&gt; (header, body, or footer). Based on Section 2.2.2.5.
    /// </summary>
    public class TextTableRow
    {
        #region properties
        /// <summary>
        /// Primary key for the TextTableRow table.
        /// </summary>
        public int? TextTableRowID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to TextTable.
        /// </summary>
        public int? TextTableID { get; set; } // Made nullable

        /// <summary>
        /// 'Header', 'Body', 'Footer' (corresponding to thead, tbody, tfoot).
        /// </summary>
        public string? RowGroupType { get; set; } // Made nullable

        /// <summary>
        /// Order of the row within its group (thead, tbody, tfoot).
        /// </summary>
        public int? SequenceNumber { get; set; } // Made nullable

        /// <summary>
        /// Optional styleCode attribute on &lt;tr&gt; (e.g., Botrule).
        /// </summary>
        public string? StyleCode { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores individual &lt;td&gt; or &lt;th&gt; elements within a &lt;tr&gt;. Based on Section 2.2.2.5.
    /// </summary>
    public class TextTableCell
    {
        #region properties
        /// <summary>
        /// Primary key for the TextTableCell table.
        /// </summary>
        public int? TextTableCellID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to TextTableRow.
        /// </summary>
        public int? TextTableRowID { get; set; } // Made nullable

        /// <summary>
        /// 'td' or 'th'.
        /// </summary>
        public string? CellType { get; set; } // Made nullable

        /// <summary>
        /// Order of the cell within the row (column number).
        /// </summary>
        public int? SequenceNumber { get; set; } // Made nullable

        /// <summary>
        /// Text content of the table cell (&lt;td&gt; or &lt;th&gt;).
        /// </summary>
        public string? CellText { get; set; } // Made nullable

        /// <summary>
        /// Optional rowspan attribute on &lt;td&gt; or &lt;th&gt;.
        /// </summary>
        public int? RowSpan { get; set; } // Already nullable

        /// <summary>
        /// Optional colspan attribute on &lt;td&gt; or &lt;th&gt;.
        /// </summary>
        public int? ColSpan { get; set; } // Already nullable

        /// <summary>
        /// Optional styleCode attribute for cell rules (Lrule, Rrule, Toprule, Botrule).
        /// </summary>
        public string? StyleCode { get; set; } // Already nullable

        /// <summary>
        /// Optional align attribute for horizontal alignment.
        /// </summary>
        public string? Align { get; set; } // Already nullable

        /// <summary>
        /// Optional valign attribute for vertical alignment.
        /// </summary>
        public string? VAlign { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores metadata for images (&lt;observationMedia&gt;). Based on Section 2.2.3.
    /// </summary>
    public class ObservationMedia
    {
        #region properties
        /// <summary>
        /// Primary key for the ObservationMedia table.
        /// </summary>
        public int? ObservationMediaID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Section (where the observationMedia is defined).
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// Identifier for the media object (&lt;observationMedia ID=&gt;), referenced by &lt;renderMultimedia&gt;.
        /// </summary>
        public string? MediaID { get; set; } // Made nullable

        /// <summary>
        /// Text description of the image (&lt;text&gt; child of observationMedia), used by screen readers.
        /// </summary>
        public string? DescriptionText { get; set; } // Already nullable

        /// <summary>
        /// Media type of the file (&lt;value mediaType=&gt;), e.g., image/jpeg.
        /// </summary>
        public string? MediaType { get; set; } // Made nullable

        /// <summary>
        /// File name of the image (&lt;reference value=&gt;).
        /// </summary>
        public string? FileName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Represents the &lt;renderMultimedia&gt; tag, linking text content to an ObservationMedia entry. Based on Section 2.2.3.
    /// </summary>
    public class RenderedMedia
    {
        #region properties
        /// <summary>
        /// Primary key for the RenderedMedia table.
        /// </summary>
        public int? RenderedMediaID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to SectionTextContent (Paragraph or BlockImage).
        /// </summary>
        public int? SectionTextContentID { get; set; } // Made nullable

        /// <summary>
        /// Link to the ObservationMedia containing the image details, via the referencedObject attribute.
        /// </summary>
        public int? ObservationMediaID { get; set; } // Made nullable

        /// <summary>
        /// Order if multiple images are in one content block.
        /// </summary>
        public int? SequenceInContent { get; set; } // Made nullable

        /// <summary>
        /// Indicates if the image is inline (within a paragraph) or block level (direct child of &lt;text&gt;).
        /// </summary>
        public bool? IsInline { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the highlight text within an excerpt for specific sections (e.g., Boxed Warning, Indications). Based on Section 2.2.4.
    /// </summary>
    public class SectionExcerptHighlight
    {
        #region properties
        /// <summary>
        /// Primary key for the SectionExcerptHighlight table.
        /// </summary>
        public int? SectionExcerptHighlightID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Section (The section containing the excerpt/highlight).
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// Text content from &lt;excerpt&gt;&lt;highlight&gt;&lt;text&gt;.
        /// </summary>
        public string? HighlightText { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores core product information (&lt;manufacturedProduct&gt;). Based on Section 3.1.
    /// </summary>
    public class Product
    {
        #region properties
        /// <summary>
        /// Primary key for the Product table.
        /// </summary>
        public int? ProductID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Section (if product defined in a section).
        /// </summary>
        public int? SectionID { get; set; } // Already nullable

        /// <summary>
        /// Proprietary name or product name (&lt;name&gt;).
        /// </summary>
        public string? ProductName { get; set; } // Already nullable

        /// <summary>
        /// Suffix to the proprietary name (&lt;suffix&gt;), e.g., "XR".
        /// </summary>
        public string? ProductSuffix { get; set; } // Already nullable

        /// <summary>
        /// Dosage form code, system, and display name (&lt;formCode&gt;).
        /// </summary>
        public string? FormCode { get; set; } // Already nullable

        /// <summary>
        /// Corresponds to &lt;formCode codeSystem&gt;.
        /// </summary>
        public string? FormCodeSystem { get; set; } // Already nullable

        /// <summary>
        /// Corresponds to &lt;formCode displayName&gt;.
        /// </summary>
        public string? FormDisplayName { get; set; } // Already nullable

        /// <summary>
        /// Brief description of the product (&lt;desc&gt;), mainly used for devices.
        /// </summary>
        public string? DescriptionText { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores various types of identifiers associated with a product (Item Codes like NDC, GTIN, etc.). Based on Section 3.1.1.
    /// </summary>
    public class ProductIdentifier
    {
        #region properties
        /// <summary>
        /// Primary key for the ProductIdentifier table.
        /// </summary>
        public int? ProductIdentifierID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        public int? ProductID { get; set; } // Made nullable

        /// <summary>
        /// The item code value (&lt;code code=&gt;).
        /// </summary>
        public string? IdentifierValue { get; set; } // Made nullable

        /// <summary>
        /// OID for the identifier system (&lt;code codeSystem=&gt;).
        /// </summary>
        public string? IdentifierSystemOID { get; set; } // Made nullable

        /// <summary>
        /// Type classification of the identifier based on the OID.
        /// </summary>
        public string? IdentifierType { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores non-proprietary (generic) medicine names associated with a Product. Based on Section 3.1.1, 3.2.1.
    /// </summary>
    public class GenericMedicine
    {
        #region properties
        /// <summary>
        /// Primary key for the GenericMedicine table.
        /// </summary>
        public int? GenericMedicineID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        public int? ProductID { get; set; } // Made nullable

        /// <summary>
        /// Non-proprietary name of the product (&lt;genericMedicine&gt;&lt;name&gt;).
        /// </summary>
        public string? GenericName { get; set; } // Made nullable

        /// <summary>
        /// Phonetic spelling of the generic name (&lt;name use="PHON"&gt;), optional.
        /// </summary>
        public string? PhoneticName { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores specialized kind information, like device product classification or cosmetic category. Based on Section 3.1.1, 3.3.1, 3.4.3.
    /// </summary>
    public class SpecializedKind
    {
        #region properties
        /// <summary>
        /// Primary key for the SpecializedKind table.
        /// </summary>
        public int? SpecializedKindID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        public int? ProductID { get; set; } // Made nullable

        /// <summary>
        /// Code for the specialized kind (e.g., device product classification, cosmetic category).
        /// </summary>
        public string? KindCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for the specialized kind code (typically 2.16.840.1.113883.6.303).
        /// </summary>
        public string? KindCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name matching the specialized kind code.
        /// </summary>
        public string? KindDisplayName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores relationships indicating equivalence to other products (e.g., product source, predecessor). Based on Section 3.1.2.
    /// </summary>
    public class EquivalentEntity
    {
        #region properties
        /// <summary>
        /// Primary key for the EquivalentEntity table.
        /// </summary>
        public int? EquivalentEntityID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (The product being described).
        /// </summary>
        public int? ProductID { get; set; } // Made nullable

        /// <summary>
        /// Code indicating the type of equivalence relationship, e.g., C64637 (Same), pending (Predecessor).
        /// </summary>
        public string? EquivalenceCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for EquivalenceCode.
        /// </summary>
        public string? EquivalenceCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Item code of the equivalent product (e.g., source NDC product code).
        /// </summary>
        public string? DefiningMaterialKindCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for the equivalent product's item code.
        /// </summary>
        public string? DefiningMaterialKindSystem { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores additional product identifiers like Model Number, Catalog Number, Reference Number. Based on Section 3.1.3, 3.3.2.
    /// </summary>
    public class AdditionalIdentifier
    {
        #region properties
        /// <summary>
        /// Primary key for the AdditionalIdentifier table.
        /// </summary>
        public int? AdditionalIdentifierID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        public int? ProductID { get; set; } // Made nullable

        /// <summary>
        /// Code for the type of identifier (e.g., C99286 Model Number, C99285 Catalog Number, C99287 Reference Number).
        /// </summary>
        public string? IdentifierTypeCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for IdentifierTypeCode.
        /// </summary>
        public string? IdentifierTypeCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for IdentifierTypeCode.
        /// </summary>
        public string? IdentifierTypeDisplayName { get; set; } // Made nullable

        /// <summary>
        /// The actual identifier value (&lt;id extension&gt;).
        /// </summary>
        public string? IdentifierValue { get; set; } // Made nullable

        /// <summary>
        /// The root OID associated with the identifier (&lt;id root&gt;).
        /// </summary>
        public string? IdentifierRootOID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores details about a unique substance (identified primarily by UNII). Based on Section 3.1.4.
    /// </summary>
    public class IngredientSubstance
    {
        #region properties
        /// <summary>
        /// Primary key for the IngredientSubstance table.
        /// </summary>
        public int? IngredientSubstanceID { get; set; } // Made nullable

        /// <summary>
        /// Unique Ingredient Identifier (&lt;code code=&gt; where codeSystem="2.16.840.1.113883.4.9"). Optional for cosmetics.
        /// </summary>
        public string? UNII { get; set; } // Already nullable

        /// <summary>
        /// Name of the substance (&lt;name&gt;).
        /// </summary>
        public string? SubstanceName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores active moiety details linked to an IngredientSubstance. Based on Section 3.1.4, 3.2.4.
    /// </summary>
    public class ActiveMoiety
    {
        #region properties
        /// <summary>
        /// Primary key for the ActiveMoiety table.
        /// </summary>
        public int? ActiveMoietyID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to IngredientSubstance (The parent substance).
        /// </summary>
        public int? IngredientSubstanceID { get; set; } // Made nullable

        /// <summary>
        /// UNII code of the active moiety (&lt;activeMoiety&gt;&lt;code&gt; code).
        /// </summary>
        public string? MoietyUNII { get; set; } // Made nullable

        /// <summary>
        /// Name of the active moiety (&lt;activeMoiety&gt;&lt;name&gt;).
        /// </summary>
        public string? MoietyName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores reference substance details linked to an IngredientSubstance (used when BasisOfStrength='ReferenceIngredient'). Based on Section 3.1.4, 3.2.5.
    /// </summary>
    public class ReferenceSubstance
    {
        #region properties
        /// <summary>
        /// Primary key for the ReferenceSubstance table.
        /// </summary>
        public int? ReferenceSubstanceID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to IngredientSubstance (The parent substance).
        /// </summary>
        public int? IngredientSubstanceID { get; set; } // Made nullable

        /// <summary>
        /// UNII code of the reference substance (&lt;definingSubstance&gt;&lt;code&gt; code).
        /// </summary>
        public string? RefSubstanceUNII { get; set; } // Made nullable

        /// <summary>
        /// Name of the reference substance (&lt;definingSubstance&gt;&lt;name&gt;).
        /// </summary>
        public string? RefSubstanceName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Represents an ingredient instance within a product, part, or product concept. Link via ProductID OR ProductConceptID. Based on Section 3.1.4, 15.2.3.
    /// </summary>
    public class Ingredient
    {
        #region properties
        /// <summary>
        /// Primary key for the Ingredient table.
        /// </summary>
        public int? IngredientID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product or Product representing a Part. Null if linked via ProductConceptID.
        /// </summary>
        public int? ProductID { get; set; } // Already nullable

        /// <summary>
        /// Foreign key to IngredientSubstance.
        /// </summary>
        public int? IngredientSubstanceID { get; set; } // Made nullable

        /// <summary>
        /// Class code indicating ingredient type (e.g., ACTIB, ACTIM, ACTIR, IACT, INGR, COLR, CNTM, ADJV).
        /// </summary>
        public string? ClassCode { get; set; } // Made nullable

        /// <summary>
        /// Strength expressed as numerator/denominator value and unit (&lt;quantity&gt;). Null for CNTM unless zero numerator.
        /// </summary>
        public decimal? QuantityNumerator { get; set; } // Already nullable

        /// <summary>
        /// Corresponds to &lt;quantity&gt;&lt;numerator unit&gt;.
        /// </summary>
        public string? QuantityNumeratorUnit { get; set; } // Already nullable

        /// <summary>
        /// Corresponds to &lt;quantity&gt;&lt;denominator value&gt;.
        /// </summary>
        public decimal? QuantityDenominator { get; set; } // Already nullable

        /// <summary>
        /// Corresponds to &lt;quantity&gt;&lt;denominator unit&gt;.
        /// </summary>
        public string? QuantityDenominatorUnit { get; set; } // Already nullable

        /// <summary>
        /// Link to ReferenceSubstance table if BasisOfStrength (ClassCode) is ACTIR.
        /// </summary>
        public int? ReferenceSubstanceID { get; set; } // Already nullable

        /// <summary>
        /// Flag indicating if the inactive ingredient information is confidential (&lt;confidentialityCode code="B"&gt;).
        /// </summary>
        public bool? IsConfidential { get; set; } // Made nullable (Default is 0 (false) in SQL)

        /// <summary>
        /// Order of the ingredient as listed in the SPL file (important for cosmetics).
        /// </summary>
        public int? SequenceNumber { get; set; } // Made nullable

        /// <summary>
        /// FK to ProductConcept, used when the ingredient belongs to a Product Concept instead of a concrete Product. Null if linked via ProductID.
        /// </summary>
        public int? ProductConceptID { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Links an Ingredient to its source product NDC (used in compounded drugs). Based on Section 3.1.4.
    /// </summary>
    public class IngredientSourceProduct
    {
        #region properties
        /// <summary>
        /// Primary key for the IngredientSourceProduct table.
        /// </summary>
        public int? IngredientSourceProductID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Ingredient.
        /// </summary>
        public int? IngredientID { get; set; } // Made nullable

        /// <summary>
        /// NDC Product Code of the source product used for the ingredient.
        /// </summary>
        public string? SourceProductNDC { get; set; } // Made nullable

        /// <summary>
        /// Code system for Source NDC.
        /// </summary>
        public string? SourceProductNDCSysten { get; set; } // Made nullable (Default is '2.16.840.1.113883.6.69' in SQL)
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Represents a level of packaging (&lt;asContent&gt;/&lt;containerPackagedProduct&gt;). Links to ProductID/PartProductID for definitions OR ProductInstanceID for lot distribution container data (16.2.8).
    /// </summary>
    public class PackagingLevel
    {
        #region properties
        /// <summary>
        /// Primary key for the PackagingLevel table.
        /// </summary>
        public int? PackagingLevelID { get; set; } // Made nullable

        /// <summary>
        /// Link to Product table if this packaging directly contains the base manufactured product.
        /// </summary>
        public int? ProductID { get; set; } // Already nullable

        /// <summary>
        /// Link to Product table (representing a part) if this packaging contains a part of a kit.
        /// </summary>
        public int? PartProductID { get; set; } // Already nullable

        /// <summary>
        /// Quantity and unit of the item contained within this package level (&lt;quantity&gt;).
        /// </summary>
        public decimal? QuantityNumerator { get; set; } // Made nullable

        /// <summary>
        /// Corresponds to &lt;quantity&gt;&lt;numerator unit&gt;.
        /// </summary>
        public string? QuantityNumeratorUnit { get; set; } // Made nullable

        /// <summary>
        /// Package type code, system, and display name (&lt;containerPackagedProduct&gt;&lt;formCode&gt;).
        /// </summary>
        public string? PackageFormCode { get; set; } // Made nullable

        /// <summary>
        /// Corresponds to &lt;containerPackagedProduct&gt;&lt;formCode codeSystem&gt;.
        /// </summary>
        public string? PackageFormCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Corresponds to &lt;containerPackagedProduct&gt;&lt;formCode displayName&gt;.
        /// </summary>
        public string? PackageFormDisplayName { get; set; } // Made nullable

        /// <summary>
        /// FK to ProductInstance, used when the packaging details describe a container linked to a specific Label Lot instance (Lot Distribution).
        /// </summary>
        public int? ProductInstanceID { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores identifiers (NDC Package Code, etc.) for a specific packaging level. Based on Section 3.1.5.
    /// </summary>
    public class PackageIdentifier
    {
        #region properties
        /// <summary>
        /// Primary key for the PackageIdentifier table.
        /// </summary>
        public int? PackageIdentifierID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to PackagingLevel.
        /// </summary>
        public int? PackagingLevelID { get; set; } // Made nullable

        /// <summary>
        /// The package item code value (&lt;containerPackagedProduct&gt;&lt;code&gt; code).
        /// </summary>
        public string? IdentifierValue { get; set; } // Made nullable

        /// <summary>
        /// OID for the package identifier system (&lt;containerPackagedProduct&gt;&lt;code&gt; codeSystem).
        /// </summary>
        public string? IdentifierSystemOID { get; set; } // Made nullable

        /// <summary>
        /// e.g., 'NDCPackage', 'NHRICPackage', 'GS1Package', 'HIBCCPackage', 'ISBTPackage'.
        /// </summary>
        public string? IdentifierType { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Defines the nested structure of packaging levels. Links an outer package to the inner package(s) it contains.
    /// </summary>
    public class PackagingHierarchy
    {
        #region properties
        /// <summary>
        /// Primary key for the PackagingHierarchy table.
        /// </summary>
        public int? PackagingHierarchyID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to PackagingLevel (The containing package).
        /// </summary>
        public int? OuterPackagingLevelID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to PackagingLevel (The contained package).
        /// </summary>
        public int? InnerPackagingLevelID { get; set; } // Made nullable

        /// <summary>
        /// Order of inner package within outer package (if multiple identical inner packages).
        /// </summary>
        public int? SequenceNumber { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores marketing category and application/monograph information for a product, part, or application product concept. Link via ProductID OR ProductConceptID. Based on Section 3.1.7, 15.2.7.
    /// </summary>
    public class MarketingCategory
    {
        #region properties
        /// <summary>
        /// Primary key for the MarketingCategory table.
        /// </summary>
        public int? MarketingCategoryID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (or Product representing a Part). Null if linked via ProductConceptID.
        /// </summary>
        public int? ProductID { get; set; } // Already nullable

        /// <summary>
        /// Code identifying the marketing category (e.g., NDA, ANDA, OTC Monograph Drug).
        /// </summary>
        public string? CategoryCode { get; set; } // Made nullable

        /// <summary>
        /// Marketing Category code system (&lt;approval&gt;&lt;code&gt; codeSystem).
        /// </summary>
        public string? CategoryCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Marketing Category display name (&lt;approval&gt;&lt;code&gt; displayName).
        /// </summary>
        public string? CategoryDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Application number, monograph ID, or citation (&lt;id extension&gt;).
        /// </summary>
        public string? ApplicationOrMonographIDValue { get; set; } // Already nullable

        /// <summary>
        /// Root OID for the application number or monograph ID system (&lt;id root&gt;).
        /// </summary>
        public string? ApplicationOrMonographIDOID { get; set; } // Already nullable

        /// <summary>
        /// Date of application approval, if applicable (&lt;effectiveTime&gt;&lt;low value&gt;).
        /// </summary>
        public DateTime? ApprovalDate { get; set; } // Already nullable

        /// <summary>
        /// Territory code, typically USA (&lt;territory&gt;&lt;code&gt;).
        /// </summary>
        public string? TerritoryCode { get; set; } // Already nullable

        /// <summary>
        /// FK to ProductConcept, used when the marketing category applies to an Application Product Concept instead of a concrete Product. Null if linked via ProductID.
        /// </summary>
        public int? ProductConceptID { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores marketing status information for a product or package (&lt;subjectOf&gt;&lt;marketingAct&gt;). Based on Section 3.1.8.
    /// </summary>
    public class MarketingStatus
    {
        #region properties
        /// <summary>
        /// Primary key for the MarketingStatus table.
        /// </summary>
        public int? MarketingStatusID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (if status applies to product).
        /// </summary>
        public int? ProductID { get; set; } // Already nullable

        /// <summary>
        /// Foreign key to PackagingLevel (if status applies to a package).
        /// </summary>
        public int? PackagingLevelID { get; set; } // Already nullable

        /// <summary>
        /// Code for the marketing activity (e.g., C53292 Marketing, C96974 Drug Sample).
        /// </summary>
        public string? MarketingActCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for MarketingActCode.
        /// </summary>
        public string? MarketingActCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Status code: active, completed, new, cancelled.
        /// </summary>
        public string? StatusCode { get; set; } // Made nullable

        /// <summary>
        /// Marketing start date (&lt;effectiveTime&gt;&lt;low value&gt;).
        /// </summary>
        public DateTime? EffectiveStartDate { get; set; } // Already nullable

        /// <summary>
        /// Marketing end date (&lt;effectiveTime&gt;&lt;high value&gt;).
        /// </summary>
        public DateTime? EffectiveEndDate { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores characteristics of a product or package (&lt;subjectOf&gt;&lt;characteristic&gt;). Based on Section 3.1.9.
    /// </summary>
    public class Characteristic
    {
        #region properties
        /// <summary>
        /// Primary key for the Characteristic table.
        /// </summary>
        public int? CharacteristicID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (if characteristic applies to product).
        /// </summary>
        public int? ProductID { get; set; } // Already nullable

        /// <summary>
        /// Foreign key to PackagingLevel (if characteristic applies to package).
        /// </summary>
        public int? PackagingLevelID { get; set; } // Already nullable

        /// <summary>
        /// Code identifying the characteristic property (e.g., SPLCOLOR, SPLSHAPE, SPLSIZE, SPLIMPRINT, SPLFLAVOR, SPLSCORE, SPLIMAGE, SPLCMBPRDTP, SPLPRODUCTIONAMOUNT, SPLUSE, SPLSTERILEUSE, SPLPROFESSIONALUSE, SPLSMALLBUSINESS).
        /// </summary>
        public string? CharacteristicCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for CharacteristicCode.
        /// </summary>
        public string? CharacteristicCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Indicates the XML Schema instance type of the &lt;value&gt; element (e.g., PQ, INT, CV, ST, BL, IVL_PQ, ED).
        /// </summary>
        public string? ValueType { get; set; } // Made nullable

        /// <summary>
        /// Value for PQ type.
        /// </summary>
        public decimal? ValuePQ_Value { get; set; } // Already nullable

        /// <summary>
        /// Unit for PQ type.
        /// </summary>
        public string? ValuePQ_Unit { get; set; } // Already nullable

        /// <summary>
        /// Value for INT type.
        /// </summary>
        public int? ValueINT { get; set; } // Already nullable

        /// <summary>
        /// Code for CV type.
        /// </summary>
        public string? ValueCV_Code { get; set; } // Already nullable

        /// <summary>
        /// Code system for CV type.
        /// </summary>
        public string? ValueCV_CodeSystem { get; set; } // Already nullable

        /// <summary>
        /// Display name for CV type.
        /// </summary>
        public string? ValueCV_DisplayName { get; set; } // Already nullable

        /// <summary>
        /// Value for ST type.
        /// </summary>
        public string? ValueST { get; set; } // Already nullable

        /// <summary>
        /// Value for BL type.
        /// </summary>
        public bool? ValueBL { get; set; } // Already nullable

        /// <summary>
        /// Low value for IVL_PQ type.
        /// </summary>
        public decimal? ValueIVLPQ_LowValue { get; set; } // Already nullable

        /// <summary>
        /// Low unit for IVL_PQ type.
        /// </summary>
        public string? ValueIVLPQ_LowUnit { get; set; } // Already nullable

        /// <summary>
        /// High value for IVL_PQ type.
        /// </summary>
        public decimal? ValueIVLPQ_HighValue { get; set; } // Already nullable

        /// <summary>
        /// High unit for IVL_PQ type.
        /// </summary>
        public string? ValueIVLPQ_HighUnit { get; set; } // Already nullable

        /// <summary>
        /// Media type for ED type.
        /// </summary>
        public string? ValueED_MediaType { get; set; } // Already nullable

        /// <summary>
        /// File name for ED type.
        /// </summary>
        public string? ValueED_FileName { get; set; } // Already nullable

        /// <summary>
        /// Used for INT type with nullFlavor="PINF" (e.g., SPLUSE, SPLPRODUCTIONAMOUNT).
        /// </summary>
        public string? ValueNullFlavor { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Defines the parts comprising a kit product. Links a Kit Product to its constituent Part Products. Based on Section 3.1.6.
    /// </summary>
    public class ProductPart
    {
        #region properties
        /// <summary>
        /// Primary key for the ProductPart table.
        /// </summary>
        public int? ProductPartID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (The parent Kit product).
        /// </summary>
        public int? KitProductID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (The product representing the part).
        /// </summary>
        public int? PartProductID { get; set; } // Made nullable

        /// <summary>
        /// Quantity and unit of this part contained within the parent kit product (&lt;part&gt;&lt;quantity&gt;).
        /// </summary>
        public decimal? PartQuantityNumerator { get; set; } // Made nullable

        /// <summary>
        /// Unit for the part quantity (&lt;quantity&gt;&lt;numerator unit&gt;).
        /// </summary>
        public string? PartQuantityNumeratorUnit { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Links products sold separately but intended for use together (&lt;asPartOfAssembly&gt;). Based on Section 3.1.6, 3.3.8.
    /// </summary>
    public class PartOfAssembly
    {
        #region properties
        /// <summary>
        /// Primary key for the PartOfAssembly table.
        /// </summary>
        public int? PartOfAssemblyID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (The product being described that is part of the assembly).
        /// </summary>
        public int? PrimaryProductID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (The other product in the assembly, referenced via &lt;part&gt;&lt;partProduct&gt; inside &lt;asPartOfAssembly&gt;).
        /// </summary>
        public int? AccessoryProductID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores policy information related to a product, like DEA Schedule (&lt;subjectOf&gt;&lt;policy&gt;). Based on Section 3.2.11.
    /// </summary>
    public class Policy
    {
        #region properties
        /// <summary>
        /// Primary key for the Policy table.
        /// </summary>
        public int? PolicyID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        public int? ProductID { get; set; } // Made nullable

        /// <summary>
        /// Class code for the policy, e.g., DEADrugSchedule.
        /// </summary>
        public string? PolicyClassCode { get; set; } // Made nullable

        /// <summary>
        /// Code representing the specific policy value (e.g., DEA Schedule C-II).
        /// </summary>
        public string? PolicyCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for the policy code (e.g., 2.16.840.1.113883.3.26.1.1 for DEA schedule).
        /// </summary>
        public string? PolicyCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name matching the policy code.
        /// </summary>
        public string? PolicyDisplayName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Links a product (or part) to its route(s) of administration (&lt;consumedIn&gt;&lt;substanceAdministration&gt;). Based on Section 3.2.20.
    /// </summary>
    public class ProductRouteOfAdministration
    {
        #region properties
        /// <summary>
        /// Primary key for the ProductRouteOfAdministration table.
        /// </summary>
        public int? ProductRouteOfAdministrationID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (or Product representing a Part).
        /// </summary>
        public int? ProductID { get; set; } // Made nullable

        /// <summary>
        /// Code identifying the route of administration.
        /// </summary>
        public string? RouteCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for the route code (typically 2.16.840.1.113883.3.26.1.1).
        /// </summary>
        public string? RouteCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name matching the route code.
        /// </summary>
        public string? RouteDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Stores nullFlavor attribute value (e.g., NA) when route code is not applicable.
        /// </summary>
        public string? RouteNullFlavor { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the web page link for a cosmetic product (&lt;subjectOf&gt;&lt;document&gt;&lt;text&gt;&lt;reference value=&gt;). Based on Section 3.4.7.
    /// </summary>
    public class ProductWebLink
    {
        #region properties
        /// <summary>
        /// Primary key for the ProductWebLink table.
        /// </summary>
        public int? ProductWebLinkID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        public int? ProductID { get; set; } // Made nullable

        /// <summary>
        /// Absolute URL for the product web page, starting with http:// or https://.
        /// </summary>
        public string? WebURL { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores various identifiers associated with an Organization (DUNS, FEI, Labeler Code, License Number, etc.).
    /// </summary>
    public class OrganizationIdentifier
    {
        #region properties
        /// <summary>
        /// Primary key for the OrganizationIdentifier table.
        /// </summary>
        public int? OrganizationIdentifierID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Organization.
        /// </summary>
        public int? OrganizationID { get; set; } // Made nullable

        /// <summary>
        /// The identifier value (&lt;id extension&gt;).
        /// </summary>
        public string? IdentifierValue { get; set; } // Made nullable

        /// <summary>
        /// OID for the identifier system (&lt;id root&gt;).
        /// </summary>
        public string? IdentifierSystemOID { get; set; } // Made nullable

        /// <summary>
        /// Type classification of the identifier based on the OID and context.
        /// </summary>
        public string? IdentifierType { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores business operation details for an establishment or labeler (&lt;performance&gt;&lt;actDefinition&gt;). Based on Section 4.1.4, 5.1.5, 6.1.6.
    /// </summary>
    public class BusinessOperation
    {
        #region properties
        /// <summary>
        /// Primary key for the BusinessOperation table.
        /// </summary>
        public int? BusinessOperationID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to DocumentRelationship (linking to the Org performing the operation).
        /// </summary>
        public int? DocumentRelationshipID { get; set; } // Made nullable

        /// <summary>
        /// Code identifying the business operation.
        /// </summary>
        public string? OperationCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for the operation code (typically 2.16.840.1.113883.3.26.1.1).
        /// </summary>
        public string? OperationCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name matching the operation code.
        /// </summary>
        public string? OperationDisplayName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores qualifier details for a specific Business Operation (&lt;actDefinition&gt;&lt;subjectOf&gt;&lt;approval&gt;&lt;code&gt;). Based on Section 5.1.5, 6.1.7.
    /// </summary>
    public class BusinessOperationQualifier
    {
        #region properties
        /// <summary>
        /// Primary key for the BusinessOperationQualifier table.
        /// </summary>
        public int? BusinessOperationQualifierID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to BusinessOperation.
        /// </summary>
        public int? BusinessOperationID { get; set; } // Made nullable

        /// <summary>
        /// Code qualifying the business operation.
        /// </summary>
        public string? QualifierCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for the qualifier code (typically 2.16.840.1.113883.3.26.1.1).
        /// </summary>
        public string? QualifierCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name matching the qualifier code.
        /// </summary>
        public string? QualifierDisplayName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Links a Business Operation performed by an establishment to a specific product (&lt;actDefinition&gt;&lt;product&gt;). Based on Section 4.1.5.
    /// </summary>
    public class BusinessOperationProductLink
    {
        #region properties
        /// <summary>
        /// Primary key for the BusinessOperationProductLink table.
        /// </summary>
        public int? BusinessOperationProductLinkID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to BusinessOperation.
        /// </summary>
        public int? BusinessOperationID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (The product linked to the operation).
        /// </summary>
        public int? ProductID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores legal authenticator (signature) information for a document (&lt;legalAuthenticator&gt;). Based on Section 5.1.6, 35.1.3, 36.1.7.
    /// </summary>
    public class LegalAuthenticator
    {
        #region properties
        /// <summary>
        /// Primary key for the LegalAuthenticator table.
        /// </summary>
        public int? LegalAuthenticatorID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Document.
        /// </summary>
        public int? DocumentID { get; set; } // Made nullable

        /// <summary>
        /// Optional signing statement provided in &lt;noteText&gt;.
        /// </summary>
        public string? NoteText { get; set; } // Already nullable

        /// <summary>
        /// Timestamp of the signature (&lt;time value&gt;).
        /// </summary>
        public DateTime? TimeValue { get; set; } // Made nullable

        /// <summary>
        /// The electronic signature text (&lt;signatureText&gt;).
        /// </summary>
        public string? SignatureText { get; set; } // Made nullable

        /// <summary>
        /// Name of the person signing (&lt;assignedPerson&gt;&lt;name&gt;).
        /// </summary>
        public string? AssignedPersonName { get; set; } // Made nullable

        /// <summary>
        /// Link to the signing Organization, used for FDA signers in Labeler Code Inactivation (Sec 5.1.6).
        /// </summary>
        public int? SignerOrganizationID { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores substance details (e.g., active moiety, pharmacologic class identifier) used in Indexing contexts (&lt;subject&gt;&lt;identifiedSubstance&gt;). Based on Section 8.2.2, 8.2.3.
    /// </summary>
    public class IdentifiedSubstance
    {
        #region properties
        /// <summary>
        /// Primary key for the IdentifiedSubstance table.
        /// </summary>
        public int? IdentifiedSubstanceID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Section (The indexing section containing this substance).
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// Indicates whether the identified substance represents an Active Moiety (8.2.2) or a Pharmacologic Class being defined (8.2.3).
        /// </summary>
        public string? SubjectType { get; set; } // Made nullable

        /// <summary>
        /// Identifier value - UNII for Active Moiety, MED-RT/MeSH code for Pharm Class.
        /// </summary>
        public string? SubstanceIdentifierValue { get; set; } // Made nullable

        /// <summary>
        /// Identifier system OID - UNII (2.16.840.1.113883.4.9), MED-RT (2.16.840.1.113883.6.345), or MeSH (2.16.840.1.113883.6.177).
        /// </summary>
        public string? SubstanceIdentifierSystemOID { get; set; } // Made nullable

        /// <summary>
        /// Indicates if this row defines the substance/class (8.2.3) or references it (8.2.2).
        /// </summary>
        public bool? IsDefinition { get; set; } // Made nullable (Default is 0 (false) in SQL)
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the definition of a pharmacologic class concept, identified by its code. Based on Section 8.2.3.
    /// </summary>
    public class PharmacologicClass
    {
        #region properties
        /// <summary>
        /// Primary key for the PharmacologicClass table.
        /// </summary>
        public int? PharmacologicClassID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to IdentifiedSubstance (where IsDefinition=1 and SubjectType='PharmacologicClass').
        /// </summary>
        public int? IdentifiedSubstanceID { get; set; } // Made nullable

        /// <summary>
        /// The MED-RT or MeSH code for the pharmacologic class.
        /// </summary>
        public string? ClassCode { get; set; } // Made nullable

        /// <summary>
        /// Code system (&lt;code&gt; codeSystem).
        /// </summary>
        public string? ClassCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// The display name for the class code, including the type suffix like [EPC] or [CS].
        /// </summary>
        public string? ClassDisplayName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores preferred (L) and alternate (A) names for a Pharmacologic Class (&lt;identifiedSubstance&gt;&lt;name use=&gt;). Based on Section 8.2.3.
    /// </summary>
    public class PharmacologicClassName
    {
        #region properties
        /// <summary>
        /// Primary key for the PharmacologicClassName table.
        /// </summary>
        public int? PharmClassNameID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to PharmacologicClass.
        /// </summary>
        public int? PharmacologicClassID { get; set; } // Made nullable

        /// <summary>
        /// The text of the preferred or alternate name.
        /// </summary>
        public string? NameValue { get; set; } // Made nullable

        /// <summary>
        /// Indicates if the name is preferred (L) or alternate (A).
        /// </summary>
        public string? NameUse { get; set; } // Made nullable (CHAR(1) in SQL)
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Links an active moiety (IdentifiedSubstance) to its associated Pharmacologic Class (&lt;asSpecializedKind&gt; under moiety). Based on Section 8.2.2.
    /// </summary>
    public class PharmacologicClassLink
    {
        #region properties
        /// <summary>
        /// Primary key for the PharmacologicClassLink table.
        /// </summary>
        public int? PharmClassLinkID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to IdentifiedSubstance (where SubjectType='ActiveMoiety').
        /// </summary>
        public int? ActiveMoietySubstanceID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to PharmacologicClass.
        /// </summary>
        public int? PharmacologicClassID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Defines the hierarchy between Pharmacologic Classes (&lt;asSpecializedKind&gt; under class definition). Based on Section 8.2.3.
    /// </summary>
    public class PharmacologicClassHierarchy
    {
        #region properties
        /// <summary>
        /// Primary key for the PharmacologicClassHierarchy table.
        /// </summary>
        public int? PharmClassHierarchyID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to PharmacologicClass (The class being defined).
        /// </summary>
        public int? ChildPharmacologicClassID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to PharmacologicClass (The super-class).
        /// </summary>
        public int? ParentPharmacologicClassID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the link between an NDC Package Code and its NCPDP Billing Unit, from Indexing - Billing Unit (71446-9) documents. Based on Section 12.
    /// </summary>
    public class BillingUnitIndex
    {
        #region properties
        /// <summary>
        /// Primary key for the BillingUnitIndex table.
        /// </summary>
        public int? BillingUnitIndexID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to the Indexing Section (48779-3) in the Billing Unit Index document.
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// The NDC Package Code being linked (&lt;containerPackagedProduct&gt;&lt;code&gt; code).
        /// </summary>
        public string? PackageNDCValue { get; set; } // Made nullable

        /// <summary>
        /// System for NDC.
        /// </summary>
        public string? PackageNDCSystemOID { get; set; } // Made nullable (Default is '2.16.840.1.113883.6.69' in SQL)

        /// <summary>
        /// The NCPDP Billing Unit Code associated with the NDC package (GM, ML, or EA).
        /// </summary>
        public string? BillingUnitCode { get; set; } // Made nullable

        /// <summary>
        /// Code system OID for the NCPDP Billing Unit Code (2.16.840.1.113883.2.13).
        /// </summary>
        public string? BillingUnitCodeSystemOID { get; set; } // Made nullable (Default is '2.16.840.1.113883.2.13' in SQL)
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the definition of an abstract or application-specific product/kit concept. Based on Section 15.2.2, 15.2.6.
    /// </summary>
    public class ProductConcept
    {
        #region properties
        /// <summary>
        /// Primary key for the ProductConcept table.
        /// </summary>
        public int? ProductConceptID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to the Indexing Section (48779-3).
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// The computed MD5 hash code identifying the product concept (&lt;code&gt; code).
        /// </summary>
        public string? ConceptCode { get; set; } // Made nullable (VARCHAR(36) in SQL)

        /// <summary>
        /// OID for Product Concept Codes.
        /// </summary>
        public string? ConceptCodeSystem { get; set; } // Made nullable (Default is '2.16.840.1.113883.3.3389' in SQL)

        /// <summary>
        /// Distinguishes Abstract Product/Kit concepts from Application-specific Product/Kit concepts.
        /// </summary>
        public string? ConceptType { get; set; } // Made nullable

        /// <summary>
        /// Dosage Form details, applicable only for Abstract Product concepts.
        /// </summary>
        public string? FormCode { get; set; } // Already nullable

        /// <summary>
        /// Code system for FormCode.
        /// </summary>
        public string? FormCodeSystem { get; set; } // Already nullable

        /// <summary>
        /// Display name for FormCode.
        /// </summary>
        public string? FormDisplayName { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Links an Application Product Concept to its corresponding Abstract Product Concept (&lt;asEquivalentEntity&gt;). Based on Section 15.2.6.
    /// </summary>
    public class ProductConceptEquivalence
    {
        #region properties
        /// <summary>
        /// Primary key for the ProductConceptEquivalence table.
        /// </summary>
        public int? ProductConceptEquivalenceID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to ProductConcept (The Application concept).
        /// </summary>
        public int? ApplicationProductConceptID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to ProductConcept (The Abstract concept it derives from).
        /// </summary>
        public int? AbstractProductConceptID { get; set; } // Made nullable

        /// <summary>
        /// Code indicating the relationship type between Application and Abstract concepts (A, B, OTC, N).
        /// </summary>
        public string? EquivalenceCode { get; set; } // Made nullable

        /// <summary>
        /// OID for this code system.
        /// </summary>
        public string? EquivalenceCodeSystem { get; set; } // Made nullable (Default is '2.16.840.1.113883.3.2964' in SQL)
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores Lot Number and its associated globally unique root OID. Based on Section 16.2.5, 16.2.6, 16.2.7.
    /// </summary>
    public class LotIdentifier
    {
        #region properties
        /// <summary>
        /// Primary key for the LotIdentifier table.
        /// </summary>
        public int? LotIdentifierID { get; set; } // Made nullable

        /// <summary>
        /// The lot number string.
        /// </summary>
        public string? LotNumber { get; set; } // Made nullable

        /// <summary>
        /// The computed globally unique root OID for the lot number.
        /// </summary>
        public string? LotRootOID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Represents an instance of a product (Fill Lot, Label Lot, Package Lot, Salvaged Lot) in Lot Distribution or Salvage Reports. Based on Section 16.2.5, 16.2.7, 16.2.11, 29.2.2.
    /// </summary>
    public class ProductInstance
    {
        #region properties
        /// <summary>
        /// Primary key for the ProductInstance table.
        /// </summary>
        public int? ProductInstanceID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (The product definition this is an instance of).
        /// </summary>
        public int? ProductID { get; set; } // Made nullable

        /// <summary>
        /// Type of lot instance: FillLot, LabelLot, PackageLot (for kits), SalvagedLot.
        /// </summary>
        public string? InstanceType { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to LotIdentifier.
        /// </summary>
        public int? LotIdentifierID { get; set; } // Made nullable

        /// <summary>
        /// Expiration date, typically for Label Lots.
        /// </summary>
        public DateTime? ExpirationDate { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores dosing specification for Lot Distribution calculations (&lt;consumedIn&gt;&lt;substanceAdministration1&gt;). Based on Section 16.2.4.
    /// </summary>
    public class DosingSpecification
    {
        #region properties
        /// <summary>
        /// Primary key for the DosingSpecification table.
        /// </summary>
        public int? DosingSpecificationID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        public int? ProductID { get; set; } // Made nullable

        /// <summary>
        /// Route of administration associated with the dose.
        /// </summary>
        public string? RouteCode { get; set; } // Already nullable

        /// <summary>
        /// Code system for RouteCode.
        /// </summary>
        public string? RouteCodeSystem { get; set; } // Already nullable

        /// <summary>
        /// Display name for RouteCode.
        /// </summary>
        public string? RouteDisplayName { get; set; } // Already nullable

        /// <summary>
        /// Quantity and unit representing a single dose.
        /// </summary>
        public decimal? DoseQuantityValue { get; set; } // Already nullable

        /// <summary>
        /// Dose quantity unit (&lt;doseQuantity unit&gt;).
        /// </summary>
        public string? DoseQuantityUnit { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Represents Bulk Lot information in Lot Distribution Reports (&lt;productInstance&gt;&lt;ingredient&gt;). Based on Section 16.2.6.
    /// </summary>
    public class IngredientInstance
    {
        #region properties
        /// <summary>
        /// Primary key for the IngredientInstance table.
        /// </summary>
        public int? IngredientInstanceID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to ProductInstance (The Fill Lot this bulk lot contributes to).
        /// </summary>
        public int? FillLotInstanceID { get; set; } // Made nullable

        /// <summary>
        /// Reference to the substance constituting the bulk lot.
        /// </summary>
        public int? IngredientSubstanceID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to LotIdentifier (The Bulk Lot number).
        /// </summary>
        public int? LotIdentifierID { get; set; } // Made nullable

        /// <summary>
        /// Reference to the Organization that manufactured the bulk lot.
        /// </summary>
        public int? ManufacturerOrganizationID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Defines the relationship between Fill/Package Lots and Label Lots (&lt;productInstance&gt;&lt;member&gt;&lt;memberProductInstance&gt;). Based on Section 16.2.7, 16.2.11.
    /// </summary>
    public class LotHierarchy
    {
        #region properties
        /// <summary>
        /// Primary key for the LotHierarchy table.
        /// </summary>
        public int? LotHierarchyID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to ProductInstance (The Fill Lot or Package Lot).
        /// </summary>
        public int? ParentInstanceID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to ProductInstance (The Label Lot which is a member).
        /// </summary>
        public int? ChildInstanceID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores product events like distribution or return quantities (&lt;subjectOf&gt;&lt;productEvent&gt;). Based on Section 16.2.9, 16.2.10.
    /// </summary>
    public class ProductEvent
    {
        #region properties
        /// <summary>
        /// Primary key for the ProductEvent table.
        /// </summary>
        public int? ProductEventID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to PackagingLevel (The container level the event applies to).
        /// </summary>
        public int? PackagingLevelID { get; set; } // Made nullable

        /// <summary>
        /// Code identifying the type of event (e.g., C106325 Distributed, C106328 Returned).
        /// </summary>
        public string? EventCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for EventCode.
        /// </summary>
        public string? EventCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for EventCode.
        /// </summary>
        public string? EventDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Integer quantity associated with the event (e.g., number of containers distributed/returned).
        /// </summary>
        public int? QuantityValue { get; set; } // Made nullable

        /// <summary>
        /// Unit for quantity (usually '1' or null).
        /// </summary>
        public string? QuantityUnit { get; set; } // Already nullable

        /// <summary>
        /// Effective date (low value), used for Initial Distribution Date.
        /// </summary>
        public DateTime? EffectiveTimeLow { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores "Doing Business As" (DBA) names or other named entity types associated with an Organization (&lt;asNamedEntity&gt;). Based on Section 2.1.9, 18.1.3.
    /// </summary>
    public class NamedEntity
    {
        #region properties
        /// <summary>
        /// Primary key for the NamedEntity table.
        /// </summary>
        public int? NamedEntityID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Organization (The facility).
        /// </summary>
        public int? OrganizationID { get; set; } // Made nullable

        /// <summary>
        /// Code identifying the type of named entity, e.g., C117113 for "doing business as".
        /// </summary>
        public string? EntityTypeCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for EntityTypeCode.
        /// </summary>
        public string? EntityTypeCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for EntityTypeCode.
        /// </summary>
        public string? EntityTypeDisplayName { get; set; } // Made nullable

        /// <summary>
        /// The name of the entity, e.g., the DBA name.
        /// </summary>
        public string? EntityName { get; set; } // Made nullable

        /// <summary>
        /// Optional suffix used with DBA names in WDD/3PL reports to indicate business type ([WDD] or [3PL]).
        /// </summary>
        public string? EntitySuffix { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Represents the issuing authority (State or Federal Agency like DEA) for licenses (&lt;author&gt;&lt;territorialAuthority&gt;). Based on Section 18.1.5.
    /// </summary>
    public class TerritorialAuthority
    {
        #region properties
        /// <summary>
        /// Primary key for the TerritorialAuthority table.
        /// </summary>
        public int? TerritorialAuthorityID { get; set; } // Made nullable

        /// <summary>
        /// ISO 3166-2 State code (e.g., US-MD) or ISO 3166-1 Country code (e.g., USA).
        /// </summary>
        public string? TerritoryCode { get; set; } // Made nullable

        /// <summary>
        /// Code system (e.g., '1.0.3166.2' for state, '1.0.3166.1.2.3' for country).
        /// </summary>
        public string? TerritoryCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Link to the Organization representing the federal governing agency, if applicable.
        /// </summary>
        public int? GoverningAgencyOrgID { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores license information for WDD/3PL facilities (&lt;subjectOf&gt;&lt;approval&gt;). Based on Section 18.1.5.
    /// </summary>
    public class License
    {
        #region properties
        /// <summary>
        /// Primary key for the License table.
        /// </summary>
        public int? LicenseID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to BusinessOperation (The WDD/3PL operation being licensed).
        /// </summary>
        public int? BusinessOperationID { get; set; } // Made nullable

        /// <summary>
        /// The license number string.
        /// </summary>
        public string? LicenseNumber { get; set; } // Made nullable

        /// <summary>
        /// The root OID identifying the issuing authority and context.
        /// </summary>
        public string? LicenseRootOID { get; set; } // Made nullable

        /// <summary>
        /// Code indicating the type of approval/license (e.g., C118777 licensing).
        /// </summary>
        public string? LicenseTypeCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for LicenseTypeCode.
        /// </summary>
        public string? LicenseTypeCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for LicenseTypeCode.
        /// </summary>
        public string? LicenseTypeDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Status of the license: active, suspended, aborted (revoked), completed (expired).
        /// </summary>
        public string? StatusCode { get; set; } // Made nullable

        /// <summary>
        /// Expiration date of the license.
        /// </summary>
        public DateTime? ExpirationDate { get; set; } // Already nullable

        /// <summary>
        /// Foreign key to TerritorialAuthority (Issuing state/agency).
        /// </summary>
        public int? TerritorialAuthorityID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores references to attached documents (e.g., PDFs for Disciplinary Actions, REMS Materials).
    /// </summary>
    public class AttachedDocument
    {
        #region properties
        /// <summary>
        /// Primary key for the AttachedDocument table.
        /// </summary>
        public int? AttachedDocumentID { get; set; } // Made nullable

        /// <summary>
        /// Identifies the type of the parent element containing the document reference.
        /// </summary>
        public string? ParentEntityType { get; set; } // Made nullable

        /// <summary>
        /// FK to the parent table (e.g., DisciplinaryActionID).
        /// </summary>
        public int? ParentEntityID { get; set; } // Made nullable

        /// <summary>
        /// MIME type of the attached document.
        /// </summary>
        public string? MediaType { get; set; } // Made nullable

        /// <summary>
        /// File name of the attached document.
        /// </summary>
        public string? FileName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores disciplinary action details related to a License (&lt;approval&gt;&lt;subjectOf&gt;&lt;action&gt;). Based on Section 18.1.7.
    /// </summary>
    public class DisciplinaryAction
    {
        #region properties
        /// <summary>
        /// Primary key for the DisciplinaryAction table.
        /// </summary>
        public int? DisciplinaryActionID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to License.
        /// </summary>
        public int? LicenseID { get; set; } // Made nullable

        /// <summary>
        /// Code identifying the disciplinary action type (e.g., suspension, revocation, activation).
        /// </summary>
        public string? ActionCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for ActionCode.
        /// </summary>
        public string? ActionCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for ActionCode.
        /// </summary>
        public string? ActionDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Date the disciplinary action became effective.
        /// </summary>
        public DateTime? EffectiveTime { get; set; } // Made nullable

        /// <summary>
        /// Text description used when the action code is 'other'.
        /// </summary>
        public string? ActionText { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores substance specification details for tolerance documents (&lt;subjectOf&gt;&lt;substanceSpecification&gt;). Based on Section 19.2.3.
    /// </summary>
    public class SubstanceSpecification
    {
        #region properties
        /// <summary>
        /// Primary key for the SubstanceSpecification table.
        /// </summary>
        public int? SubstanceSpecificationID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to IdentifiedSubstance (The substance subject to tolerance).
        /// </summary>
        public int? IdentifiedSubstanceID { get; set; } // Made nullable

        /// <summary>
        /// Specification code, format 40-CFR-...
        /// </summary>
        public string? SpecCode { get; set; } // Made nullable

        /// <summary>
        /// Code system (2.16.840.1.113883.3.149).
        /// </summary>
        public string? SpecCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Code for the Enforcement Analytical Method used (&lt;observation&gt;&lt;code&gt;).
        /// </summary>
        public string? EnforcementMethodCode { get; set; } // Already nullable

        /// <summary>
        /// Code system for Enforcement Analytical Method.
        /// </summary>
        public string? EnforcementMethodCodeSystem { get; set; } // Already nullable

        /// <summary>
        /// Display name for Enforcement Analytical Method.
        /// </summary>
        public string? EnforcementMethodDisplayName { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Links a Substance Specification to the analyte(s) being measured (&lt;analyte&gt;&lt;identifiedSubstance&gt;). Based on Section 19.2.3.
    /// </summary>
    public class Analyte
    {
        #region properties
        /// <summary>
        /// Primary key for the Analyte table.
        /// </summary>
        public int? AnalyteID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to SubstanceSpecification.
        /// </summary>
        public int? SubstanceSpecificationID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to IdentifiedSubstance (The substance being measured).
        /// </summary>
        public int? AnalyteSubstanceID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores commodity details referenced in tolerance specifications (&lt;subject&gt;&lt;presentSubstance&gt;&lt;presentSubstance&gt;). Based on Section 19.2.4.
    /// </summary>
    public class Commodity
    {
        #region properties
        /// <summary>
        /// Primary key for the Commodity table.
        /// </summary>
        public int? CommodityID { get; set; } // Made nullable

        /// <summary>
        /// Code identifying the commodity.
        /// </summary>
        public string? CommodityCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for CommodityCode (2.16.840.1.113883.6.275.1).
        /// </summary>
        public string? CommodityCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for CommodityCode.
        /// </summary>
        public string? CommodityDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Optional name (&lt;presentSubstance&gt;&lt;name&gt;).
        /// </summary>
        public string? CommodityName { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores application type details referenced in tolerance specifications (&lt;subjectOf&gt;&lt;approval&gt;&lt;code&gt;). Based on Section 19.2.4.
    /// </summary>
    public class ApplicationType
    {
        #region properties
        /// <summary>
        /// Primary key for the ApplicationType table.
        /// </summary>
        public int? ApplicationTypeID { get; set; } // Made nullable

        /// <summary>
        /// Code identifying the application type (e.g., General Tolerance).
        /// </summary>
        public string? AppTypeCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for AppTypeCode (2.16.840.1.113883.6.275.1).
        /// </summary>
        public string? AppTypeCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for AppTypeCode.
        /// </summary>
        public string? AppTypeDisplayName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the tolerance range and related details (&lt;referenceRange&gt;&lt;observationCriterion&gt;). Based on Section 19.2.4.
    /// </summary>
    public class ObservationCriterion
    {
        #region properties
        /// <summary>
        /// Primary key for the ObservationCriterion table.
        /// </summary>
        public int? ObservationCriterionID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to SubstanceSpecification.
        /// </summary>
        public int? SubstanceSpecificationID { get; set; } // Made nullable

        /// <summary>
        /// The upper limit of the tolerance range in ppm.
        /// </summary>
        public decimal? ToleranceHighValue { get; set; } // Made nullable

        /// <summary>
        /// Tolerance unit (&lt;value&gt;&lt;high unit&gt;).
        /// </summary>
        public string? ToleranceHighUnit { get; set; } // Made nullable (Default is '[ppm]' in SQL)

        /// <summary>
        /// Optional link to the specific commodity the tolerance applies to.
        /// </summary>
        public int? CommodityID { get; set; } // Already nullable

        /// <summary>
        /// Link to the type of application associated with this tolerance.
        /// </summary>
        public int? ApplicationTypeID { get; set; } // Made nullable

        /// <summary>
        /// Optional expiration or revocation date for the tolerance.
        /// </summary>
        public DateTime? ExpirationDate { get; set; } // Already nullable

        /// <summary>
        /// Optional text annotation about the tolerance.
        /// </summary>
        public string? TextNote { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the specified substance code and name linked to an ingredient in Biologic/Drug Substance Indexing documents. Based on Section 20.2.6.
    /// </summary>
    public class SpecifiedSubstance
    {
        #region properties
        /// <summary>
        /// Primary key for the SpecifiedSubstance table.
        /// </summary>
        public int? SpecifiedSubstanceID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Ingredient (The ingredient being specified).
        /// </summary>
        public int? IngredientID { get; set; } // Made nullable

        /// <summary>
        /// The code assigned to the specified substance.
        /// </summary>
        public string? SubstanceCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for the specified substance code (2.16.840.1.113883.3.6277).
        /// </summary>
        public string? SubstanceCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name matching the specified substance code.
        /// </summary>
        public string? SubstanceDisplayName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores key product identification details referenced in a Warning Letter Alert Indexing document. Based on Section 21.2.2.
    /// </summary>
    public class WarningLetterProductInfo
    {
        #region properties
        /// <summary>
        /// Primary key for the WarningLetterProductInfo table.
        /// </summary>
        public int? WarningLetterProductInfoID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to the Indexing Section (48779-3).
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// Proprietary name of the product referenced in the warning letter.
        /// </summary>
        public string? ProductName { get; set; } // Already nullable

        /// <summary>
        /// Generic name of the product referenced in the warning letter.
        /// </summary>
        public string? GenericName { get; set; } // Made nullable

        /// <summary>
        /// Dosage form code of the product referenced in the warning letter.
        /// </summary>
        public string? FormCode { get; set; } // Made nullable

        /// <summary>
        /// Dosage Form code system.
        /// </summary>
        public string? FormCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Dosage Form display name.
        /// </summary>
        public string? FormDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Text description of the ingredient strength(s).
        /// </summary>
        public string? StrengthText { get; set; } // Already nullable

        /// <summary>
        /// Text description of the product item code(s) (e.g., NDC).
        /// </summary>
        public string? ItemCodesText { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the issue date and optional resolution date for a warning letter alert. Based on Section 21.2.3.
    /// </summary>
    public class WarningLetterDate
    {
        #region properties
        /// <summary>
        /// Primary key for the WarningLetterDate table.
        /// </summary>
        public int? WarningLetterDateID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to the Indexing Section (48779-3).
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// Date the warning letter alert was issued.
        /// </summary>
        public DateTime? AlertIssueDate { get; set; } // Made nullable

        /// <summary>
        /// Date the issue described in the warning letter was resolved, if applicable.
        /// </summary>
        public DateTime? ResolutionDate { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the Application Holder organization linked to a Marketing Category for REMS products (&lt;holder&gt;&lt;role&gt;&lt;playingOrganization&gt;). Based on Section 23.2.3.
    /// </summary>
    public class Holder
    {
        #region properties
        /// <summary>
        /// Primary key for the Holder table.
        /// </summary>
        public int? HolderID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to MarketingCategory.
        /// </summary>
        public int? MarketingCategoryID { get; set; } // Made nullable

        /// <summary>
        /// Link to the Organization table for the Application Holder.
        /// </summary>
        public int? HolderOrganizationID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Represents a REMS protocol defined within a section (&lt;protocol&gt; element). Based on Section 23.2.6.
    /// </summary>
    public class Protocol
    {
        #region properties
        /// <summary>
        /// Primary key for the Protocol table.
        /// </summary>
        public int? ProtocolID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to the Section containing the protocol definition.
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// Code identifying the REMS protocol type.
        /// </summary>
        public string? ProtocolCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for ProtocolCode.
        /// </summary>
        public string? ProtocolCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for ProtocolCode.
        /// </summary>
        public string? ProtocolDisplayName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Lookup table for REMS stakeholder types (&lt;stakeholder&gt;). Based on Section 23.2.7.
    /// </summary>
    public class Stakeholder
    {
        #region properties
        /// <summary>
        /// Primary key for the Stakeholder table.
        /// </summary>
        public int? StakeholderID { get; set; } // Made nullable

        /// <summary>
        /// Code identifying the stakeholder role (e.g., prescriber, patient).
        /// </summary>
        public string? StakeholderCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for StakeholderCode.
        /// </summary>
        public string? StakeholderCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for StakeholderCode.
        /// </summary>
        public string? StakeholderDisplayName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores references to REMS materials, linking to attached documents if applicable (&lt;subjectOf&gt;&lt;document&gt;). Based on Section 23.2.9.
    /// </summary>
    public class REMSMaterial
    {
        #region properties
        /// <summary>
        /// Primary key for the REMSMaterial table.
        /// </summary>
        public int? REMSMaterialID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to the REMS Material Section (82346-8).
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// Unique identifier for this specific material document reference.
        /// </summary>
        public Guid? MaterialDocumentGUID { get; set; } // Made nullable

        /// <summary>
        /// Title of the material (&lt;document&gt;&lt;title&gt;).
        /// </summary>
        public string? Title { get; set; } // Made nullable

        /// <summary>
        /// Internal link ID (#...) embedded within the title, potentially linking to descriptive text.
        /// </summary>
        public string? TitleReference { get; set; } // Already nullable

        /// <summary>
        /// Link to the AttachedDocument table if the material is provided as an attachment (e.g., PDF).
        /// </summary>
        public int? AttachedDocumentID { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Represents a REMS requirement or monitoring observation within a protocol (&lt;component&gt;&lt;requirement&gt; or &lt;monitoringObservation&gt;). Based on Section 23.2.7.
    /// </summary>
    public class Requirement
    {
        #region properties
        /// <summary>
        /// Primary key for the Requirement table.
        /// </summary>
        public int? RequirementID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Protocol.
        /// </summary>
        public int? ProtocolID { get; set; } // Made nullable

        /// <summary>
        /// Sequence number relative to the substance administration step (fixed at 2). 1=Before, 2=During/Concurrent, 3=After.
        /// </summary>
        public int? RequirementSequenceNumber { get; set; } // Made nullable

        /// <summary>
        /// Flag: True if &lt;monitoringObservation&gt;, False if &lt;requirement&gt;.
        /// </summary>
        public bool? IsMonitoringObservation { get; set; } // Made nullable (Default is 0 (false) in SQL)

        /// <summary>
        /// Optional delay (pause) relative to the start/end of the previous step.
        /// </summary>
        public decimal? PauseQuantityValue { get; set; } // Already nullable

        /// <summary>
        /// Optional delay unit (&lt;pauseQuantity unit&gt;).
        /// </summary>
        public string? PauseQuantityUnit { get; set; } // Already nullable

        /// <summary>
        /// Code identifying the specific requirement or monitoring observation.
        /// </summary>
        public string? RequirementCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for RequirementCode.
        /// </summary>
        public string? RequirementCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for RequirementCode.
        /// </summary>
        public string? RequirementDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Link ID (#...) pointing to the corresponding text description in the REMS Summary or REMS Participant Requirements section.
        /// </summary>
        public string? OriginalTextReference { get; set; } // Made nullable

        /// <summary>
        /// Optional repetition period for the requirement/observation.
        /// </summary>
        public decimal? PeriodValue { get; set; } // Already nullable

        /// <summary>
        /// Optional repetition period unit (&lt;effectiveTime&gt;&lt;period unit&gt;).
        /// </summary>
        public string? PeriodUnit { get; set; } // Already nullable

        /// <summary>
        /// Link to the stakeholder responsible for fulfilling the requirement.
        /// </summary>
        public int? StakeholderID { get; set; } // Made nullable

        /// <summary>
        /// Optional link to a REMS Material document referenced by the requirement.
        /// </summary>
        public int? REMSMaterialID { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the REMS approval details associated with the first protocol mention (&lt;subjectOf&gt;&lt;approval&gt;). Based on Section 23.2.8.
    /// </summary>
    public class REMSApproval
    {
        #region properties
        /// <summary>
        /// Primary key for the REMSApproval table.
        /// </summary>
        public int? REMSApprovalID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to the first Protocol defined in the document.
        /// </summary>
        public int? ProtocolID { get; set; } // Made nullable

        /// <summary>
        /// Code for REMS Approval (C128899).
        /// </summary>
        public string? ApprovalCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for ApprovalCode.
        /// </summary>
        public string? ApprovalCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for ApprovalCode.
        /// </summary>
        public string? ApprovalDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Date of the initial REMS program approval.
        /// </summary>
        public DateTime? ApprovalDate { get; set; } // Made nullable

        /// <summary>
        /// Territory code ('USA').
        /// </summary>
        public string? TerritoryCode { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores references to REMS electronic resources (URLs or URNs) (&lt;subjectOf&gt;&lt;document&gt;). Based on Section 23.2.10.
    /// </summary>
    public class REMSElectronicResource
    {
        #region properties
        /// <summary>
        /// Primary key for the REMSElectronicResource table.
        /// </summary>
        public int? REMSElectronicResourceID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to the REMS Material Section (82346-8) where resource is listed.
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// Unique identifier for this specific electronic resource reference.
        /// </summary>
        public Guid? ResourceDocumentGUID { get; set; } // Made nullable

        /// <summary>
        /// Title of the resource (&lt;document&gt;&lt;title&gt;).
        /// </summary>
        public string? Title { get; set; } // Made nullable

        /// <summary>
        /// Internal link ID (#...) embedded within the title, potentially linking to descriptive text.
        /// </summary>
        public string? TitleReference { get; set; } // Already nullable

        /// <summary>
        /// The URI (URL or URN) of the electronic resource.
        /// </summary>
        public string? ResourceReferenceValue { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Links an establishment (within a Blanket No Changes Certification doc) to a product being certified (&lt;performance&gt;&lt;actDefinition&gt;&lt;product&gt;). Based on Section 28.1.3.
    /// </summary>
    public class CertificationProductLink
    {
        #region properties
        /// <summary>
        /// Primary key for the CertificationProductLink table.
        /// </summary>
        public int? CertificationProductLinkID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to DocumentRelationship (linking Doc to certified Establishment).
        /// </summary>
        public int? DocumentRelationshipID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to ProductIdentifier (NDC or ISBT code being certified).
        /// </summary>
        public int? ProductIdentifierID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores FDA-initiated inactivation/reactivation status for Drug Listings (linked via PackageIdentifierID) or Establishment Registrations (linked via DocumentRelationshipID). Based on Section 30.2.3, 31.1.4.
    /// </summary>
    public class ComplianceAction
    {
        #region properties
        /// <summary>
        /// Primary key for the ComplianceAction table.
        /// </summary>
        public int? ComplianceActionID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Section (for Drug Listing Inactivation - Section 30).
        /// </summary>
        public int? SectionID { get; set; } // Already nullable

        /// <summary>
        /// Link to the specific package NDC being inactivated/reactivated (Section 30).
        /// </summary>
        public int? PackageIdentifierID { get; set; } // Already nullable

        /// <summary>
        /// Link to the DocumentRelationship representing the establishment being inactivated/reactivated (Section 31).
        /// </summary>
        public int? DocumentRelationshipID { get; set; } // Already nullable

        /// <summary>
        /// Code for the compliance action (e.g., C162847 Inactivated).
        /// </summary>
        public string? ActionCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for ActionCode.
        /// </summary>
        public string? ActionCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for ActionCode.
        /// </summary>
        public string? ActionDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Date the inactivation begins.
        /// </summary>
        public DateTime? EffectiveTimeLow { get; set; } // Made nullable

        /// <summary>
        /// Date the inactivation ends (reactivation date), if applicable.
        /// </summary>
        public DateTime? EffectiveTimeHigh { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Represents a drug interaction issue within a specific section (&lt;subjectOf&gt;&lt;issue&gt;). Based on Section 32.2.3.
    /// </summary>
    public class InteractionIssue
    {
        #region properties
        /// <summary>
        /// Primary key for the InteractionIssue table.
        /// </summary>
        public int? InteractionIssueID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Section where the interaction is mentioned.
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// Code identifying an interaction issue (C54708).
        /// </summary>
        public string? InteractionCode { get; set; } // Made nullable

        /// <summary>
        /// Code system.
        /// </summary>
        public string? InteractionCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name ('INTERACTION').
        /// </summary>
        public string? InteractionDisplayName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Links an InteractionIssue to the contributing substance/class (&lt;issue&gt;&lt;subject&gt;&lt;substanceAdministrationCriterion&gt;). Based on Section 32.2.4.
    /// </summary>
    public class ContributingFactor
    {
        #region properties
        /// <summary>
        /// Primary key for the ContributingFactor table.
        /// </summary>
        public int? ContributingFactorID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to InteractionIssue.
        /// </summary>
        public int? InteractionIssueID { get; set; } // Made nullable

        /// <summary>
        /// Link to the IdentifiedSubstance representing the drug or pharmacologic class that is the contributing factor.
        /// </summary>
        public int? FactorSubstanceID { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the consequence (pharmacokinetic effect or medical problem) of an InteractionIssue (&lt;risk&gt;&lt;consequenceObservation&gt;). Based on Section 32.2.5.
    /// </summary>
    public class InteractionConsequence
    {
        #region properties
        /// <summary>
        /// Primary key for the InteractionConsequence table.
        /// </summary>
        public int? InteractionConsequenceID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to InteractionIssue.
        /// </summary>
        public int? InteractionIssueID { get; set; } // Made nullable

        /// <summary>
        /// Code indicating the type of consequence: Pharmacokinetic effect (C54386) or Medical problem (44100-6).
        /// </summary>
        public string? ConsequenceTypeCode { get; set; } // Made nullable

        /// <summary>
        /// Code system.
        /// </summary>
        public string? ConsequenceTypeCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name.
        /// </summary>
        public string? ConsequenceTypeDisplayName { get; set; } // Made nullable

        /// <summary>
        /// Code for the specific pharmacokinetic effect or medical problem.
        /// </summary>
        public string? ConsequenceValueCode { get; set; } // Made nullable

        /// <summary>
        /// Code system for the value code (NCI Thesaurus 2.16.840.1.113883.3.26.1.1 or SNOMED CT 2.16.840.1.113883.6.96).
        /// </summary>
        public string? ConsequenceValueCodeSystem { get; set; } // Made nullable

        /// <summary>
        /// Display name for the value code.
        /// </summary>
        public string? ConsequenceValueDisplayName { get; set; } // Made nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Stores the link between an indexing section and a National Clinical Trials number (&lt;protocol&gt;&lt;id&gt;). Based on Section 33.2.2.
    /// </summary>
    public class NCTLink
    {
        #region properties
        /// <summary>
        /// Primary key for the NCTLink table.
        /// </summary>
        public int? NCTLinkID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to the Indexing Section (48779-3).
        /// </summary>
        public int? SectionID { get; set; } // Made nullable

        /// <summary>
        /// The National Clinical Trials number (id extension).
        /// </summary>
        public string? NCTNumber { get; set; } // Made nullable

        /// <summary>
        /// The root OID for NCT numbers (id root).
        /// </summary>
        public string? NCTRootOID { get; set; } // Made nullable (Default is '2.16.840.1.113883.3.1077' in SQL)
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Links a Facility (in Registration or Listing docs) to a Cosmetic Product (&lt;performance&gt;&lt;actDefinition&gt;&lt;product&gt;). Link via ProductID, ProductIdentifierID (CLN), or ProductName. Based on Section 35.2.2, 36.1.6.
    /// </summary>
    public class FacilityProductLink
    {
        #region properties
        /// <summary>
        /// Primary key for the FacilityProductLink table.
        /// </summary>
        public int? FacilityProductLinkID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to DocumentRelationship (linking Doc/Reg to Facility).
        /// </summary>
        public int? DocumentRelationshipID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (if linked by internal ProductID).
        /// </summary>
        public int? ProductID { get; set; } // Already nullable

        /// <summary>
        /// Link via Cosmetic Listing Number.
        /// </summary>
        public int? ProductIdentifierID { get; set; } // Already nullable

        /// <summary>
        /// Link via Product Name (used if CLN not yet assigned).
        /// </summary>
        public string? ProductName { get; set; } // Already nullable
        #endregion properties
    }

    /*******************************************************************************/
    /// <summary>
    /// Links a Cosmetic Product (in Facility Reg doc) to its Responsible Person organization (&lt;manufacturerOrganization&gt;). Based on Section 35.2.3.
    /// </summary>
    public class ResponsiblePersonLink
    {
        #region properties
        /// <summary>
        /// Primary key for the ResponsiblePersonLink table.
        /// </summary>
        public int? ResponsiblePersonLinkID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Product (The cosmetic product listed in the Facility Reg doc).
        /// </summary>
        public int? ProductID { get; set; } // Made nullable

        /// <summary>
        /// Foreign key to Organization (The responsible person organization).
        /// </summary>
        public int? ResponsiblePersonOrgID { get; set; } // Made nullable
        #endregion properties
    }
}
