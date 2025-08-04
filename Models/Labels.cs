

using MedRecPro.Helpers;
using MedRecPro.Models.Validation;
using MedRecPro.Service.ParsingServices;
using Microsoft.OpenApi.Attributes;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;


namespace MedRecPro.Models
{
    /*******************************************************************************/
    /// <summary>
    /// Adds nested types of the Label class to the Swagger documentation.
    /// </summary>
    public class IncludeLabelNestedTypesDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var labelBaseType = typeof(Label);
            var nestedTypes = labelBaseType.GetNestedTypes(BindingFlags.Public);

            foreach (var type in nestedTypes)
            {
                // Check if the schema already exists by trying to resolve its reference
                // If context.SchemaRepository.Schemas does not contain a key that would be generated for 'type',
                string schemaId = context.SchemaGenerator.GenerateSchema(type, context.SchemaRepository).Reference.Id;
            }

            // Ensure the Label container class itself is there if needed
            context.SchemaGenerator.GenerateSchema(labelBaseType, context.SchemaRepository);
        }
    }

    /*******************************************************************************/
    /// <summary>
    /// Container for all SPL Label metadata classes/sections. The nested classes are based on the
    /// Health Level Seven (HL7) SPL (Dec 2023) specification. https://www.fda.gov/media/84201/download. Most
    /// labels won't contain every section.
    /// </summary>
    public class Label
    {
        private static List<string> preserveTags = new List<string>
            { "paragraph", "list", "item", "caption", "linkHtml", "sup", "sub", "content" };

        #region Properties

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
            /// Globally Unique Identifier for this specific document version ([id root]).
            /// </summary>
            public Guid? DocumentGUID { get; set; } // Made nullable

            private string? _documentCode;
            /// <summary>
            /// LOINC code identifying the document type ([code] code).
            /// </summary>
            public string? DocumentCode
            {
                get => _documentCode;
                set => _documentCode = value?.RemoveHtmlXss();
            }

            private string? _documentCodeSystem;
            /// <summary>
            /// Code system for the document type code ([code] codeSystem), typically 2.16.840.1.113883.6.1.
            /// </summary>
            public string? DocumentCodeSystem
            {
                get => _documentCodeSystem;
                set => _documentCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _documentDisplayName;
            /// <summary>
            /// Display name matching the document type code ([code] displayName).
            /// </summary>
            public string? DocumentDisplayName
            {
                get => _documentDisplayName;
                set => _documentDisplayName = value?.RemoveHtmlXss();
            }

            private string? _title;
            /// <summary>
            /// Document title ([title]), if provided.
            /// </summary>
            public string? Title
            {
                get => _title;
                set => _title = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Date reference for the SPL version ([effectiveTime value]).
            /// </summary>
            public DateTime? EffectiveTime { get; set; } // Made nullable

            /// <summary>
            /// Globally Unique Identifier for the document set, constant across versions ([setId root]).
            /// </summary>
            public Guid? SetGUID { get; set; } // Made nullable

            /// <summary>
            /// Sequential integer for the document version ([versionNumber value]).
            /// </summary>
            public int? VersionNumber { get; set; } // Made nullable

            private string? _submissionFileName;
            /// <summary>
            /// Name of the submitted XML file (e.g., DocumentGUID.xml).
            /// </summary>
            public string? SubmissionFileName
            {
                get => _submissionFileName;
                set => _submissionFileName = value?.RemoveHtmlXss();
            }
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

            private string? _organizationName;
            /// <summary>
            /// Name of the organization ([name]).
            /// </summary>
            public string? OrganizationName
            {
                get => _organizationName;
                set => _organizationName = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Flag indicating if the organization information is confidential ([confidentialityCode code="B"]).
            /// </summary>
            public bool? IsConfidential { get; set; } = false;// Made nullable (Default is 0 (false) in SQL)

            /// <summary>
            /// Collection of identifiers associated with this organization.
            /// </summary>
            public virtual ICollection<OrganizationIdentifier> OrganizationIdentifiers { get; set; } = new List<OrganizationIdentifier>();
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

            private string? _streetAddressLine1;
            /// <summary>
            /// First line of the street address ([streetAddressLine]).
            /// </summary>
            public string? StreetAddressLine1
            {
                get => _streetAddressLine1;
                set => _streetAddressLine1 = value?.RemoveHtmlXss();
            }

            private string? _streetAddressLine2;
            /// <summary>
            /// Second line of the street address ([streetAddressLine]), optional.
            /// </summary>
            public string? StreetAddressLine2
            {
                get => _streetAddressLine2;
                set => _streetAddressLine2 = value?.RemoveHtmlXss();
            }

            private string? _city;
            /// <summary>
            /// City name ([city]).
            /// </summary>
            public string? City
            {
                get => _city;
                set => _city = value?.RemoveHtmlXss();
            }

            private string? _stateProvince;
            /// <summary>
            /// State or province ([state]), required if country is USA.
            /// </summary>
            public string? StateProvince
            {
                get => _stateProvince;
                set => _stateProvince = value?.RemoveHtmlXss();
            }

            private string? _postalCode;
            /// <summary>
            /// Postal or ZIP code ([postalCode]), required if country is USA.
            /// </summary>
            public string? PostalCode
            {
                get => _postalCode;
                set => _postalCode = value?.RemoveHtmlXss();
            }

            private string? _countryCode;
            /// <summary>
            /// ISO 3166-1 alpha-3 country code ([country code]).
            /// </summary>
            public string? CountryCode
            {
                get => _countryCode;
                set => _countryCode = value?.RemoveHtmlXss();
            }

            private string? _countryName;
            /// <summary>
            /// Full country name ([country] name).
            /// </summary>
            public string? CountryName
            {
                get => _countryName;
                set => _countryName = value?.RemoveHtmlXss();
            }
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

            private string? _telecomType;
            /// <summary>
            /// Type of telecommunication: "tel", "mailto", or "fax".
            /// </summary>
            public string? TelecomType
            {
                get => _telecomType;
                set => _telecomType = value?.RemoveHtmlXss();
            }

            private string? _telecomValue;
            /// <summary>
            /// The telecommunication value, prefixed with type (e.g., "tel:+1-...", "mailto:...", "fax:+1-...").
            /// </summary>
            public string? TelecomValue
            {
                get => _telecomValue;
                set => _telecomValue = value?.RemoveHtmlXss();
            }
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

            private string? _contactPersonName;
            /// <summary>
            /// Name of the contact person ([contactPerson][name]).
            /// </summary>
            public string? ContactPersonName
            {
                get => _contactPersonName;
                set => _contactPersonName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Represents the [contactParty] element, linking Organization, Address, Telecom, and ContactPerson. Based on Section 2.1.8.
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

            private string? _authorType;
            /// <summary>
            /// Identifies the type or role of the author, e.g., Labeler (4.1.2 [cite: 785]), FDA (8.1.2[cite: 945], 15.1.2[cite: 1030], 20.1.2[cite: 1295], 21.1.2[cite: 1330], 30.1.2[cite: 1553], 31.1.2[cite: 1580], 32.1.2[cite: 1607], 33.1.2 [cite: 1643]), NCPDP (12.1.2 [cite: 995]).
            /// </summary>
            public string? AuthorType
            {
                get => _authorType;
                set => _authorType = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Navigation property back to the parent Organization.
            /// </summary>
            public virtual Organization? Organization { get; set; }
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

            private string? _relationshipTypeCode;
            /// <summary>
            /// Code indicating the type of relationship (e.g., APND for core doc, RPLC for predecessor, DRIV for reference labeling, SUBJ for subject, XCRPT for excerpt).
            /// </summary>
            public string? RelationshipTypeCode
            {
                get => _relationshipTypeCode;
                set => _relationshipTypeCode = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Set GUID of the related/referenced document ([setId root]).
            /// </summary>
            public Guid? ReferencedSetGUID { get; set; } // Made nullable

            /// <summary>
            /// Document GUID of the related/referenced document ([id root]), used for RPLC relationship.
            /// </summary>
            public Guid? ReferencedDocumentGUID { get; set; } // Already nullable

            /// <summary>
            /// Version number of the related/referenced document ([versionNumber value]).
            /// </summary>
            public int? ReferencedVersionNumber { get; set; } // Already nullable

            private string? _referencedDocumentCode;
            /// <summary>
            /// Document type code, system, and display name of the related/referenced document ([code]), used for RPLC relationship.
            /// </summary>
            public string? ReferencedDocumentCode
            {
                get => _referencedDocumentCode;
                set => _referencedDocumentCode = value?.RemoveHtmlXss();
            }

            private string? _referencedDocumentCodeSystem;
            /// <summary>
            /// Corresponds to [code] codeSystem of the referenced document (used in RPLC).
            /// </summary>
            public string? ReferencedDocumentCodeSystem
            {
                get => _referencedDocumentCodeSystem;
                set => _referencedDocumentCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _referencedDocumentDisplayName;
            /// <summary>
            /// Corresponds to [code] displayName of the referenced document (used in RPLC).
            /// </summary>
            public string? ReferencedDocumentDisplayName
            {
                get => _referencedDocumentDisplayName;
                set => _referencedDocumentDisplayName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Defines hierarchical relationships between organizations within a document header (e.g., Labeler -] Registrant -] Establishment).
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

            private string? _relationshipType;
            /// <summary>
            /// Describes the specific relationship, e.g., LabelerToRegistrant (4.1.3 [cite: 788]), RegistrantToEstablishment (4.1.4 [cite: 791]), EstablishmentToUSagent (6.1.4 [cite: 914]), EstablishmentToImporter (6.1.5 [cite: 918]), LabelerToDetails (5.1.3 [cite: 863]), FacilityToParentCompany (35.1.6 [cite: 1695]), LabelerToParentCompany (36.1.2.5 [cite: 1719]), DocumentToBulkLotManufacturer (16.1.3).
            /// </summary>
            public string? RelationshipType
            {
                get => _relationshipType;
                set => _relationshipType = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Indicates the level in the hierarchy (e.g., 1 for Labeler, 2 for Registrant, 3 for Establishment).
            /// </summary>
            public int? RelationshipLevel { get; set; } // Made nullable
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Represents the main [structuredBody] container within a Document. Based on Section 2.2.
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
        /// Stores details for each [section] within the StructuredBody. Based on Section 2.2.1.
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

            [Microsoft.OpenApi.Attributes.Display("ID")]
            private string? _sectionLinkGUID;
            /// <summary>
            /// Attribute identifying the section link ([section][ID]), used for 
            /// cross-references within the document e.g. [section ID="ID_1dc7080f-1d52-4bf7-b353-3c13ec291810"]
            /// </summary>
            public string? SectionLinkGUID
            {
                get => _sectionLinkGUID;
                set => _sectionLinkGUID = value?.RemoveHtmlXss();
            }
           

            /// <summary>
            /// Unique identifier for the section ([id root]).
            /// </summary>
            public Guid? SectionGUID { get; set; } // Made nullable

            private string? _sectionCode;
            /// <summary>
            /// LOINC code for the section type ([code] code).
            /// </summary>
            public string? SectionCode
            {
                get => _sectionCode;
                set => _sectionCode = value?.RemoveHtmlXss();
            }

            private string? _sectionCodeSystem;
            /// <summary>
            /// Code system for the section code ([code] codeSystem), typically 2.16.840.1.113883.6.1.
            /// </summary>
            public string? SectionCodeSystem
            {
                get => _sectionCodeSystem;
                set => _sectionCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _sectionDisplayName;
            /// <summary>
            /// Display name matching the section code ([code] displayName).
            /// </summary>
            public string? SectionDisplayName
            {
                get => _sectionDisplayName;
                set => _sectionDisplayName = value?.RemoveHtmlXss();
            }

            private string? _title;
            /// <summary>
            /// Title of the section ([title]), may include numbering.
            /// </summary>
            public string? Title
            {
                get => _title;
                set => _title = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Effective time for the section ([effectiveTime value]). For Compounded Drug Labels (Sec 4.2.2), low/high represent the reporting period.
            /// </summary>
            public DateTime? EffectiveTime { get; set; } // Made nullable
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Manages the nested structure (parent-child relationships) of sections using [component][section]. Based on Section 2.2.1.
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
        /// Stores the main content blocks ([paragraph], [list], [table], block [renderMultimedia])
        /// within a section's [text] element. Based on Section 2.2.2.
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
            /// Parent SectionTextContent for hierarchy (e.g., a paragraph inside a highlight inside an excerpt)
            /// </summary>
            public int? ParentSectionTextContentID { get; set; } // nullable for top-level blocks

            private string? _contentType;
            /// <summary>
            /// Type of content block: Paragraph, List, Table, BlockImage (for [renderMultimedia] as direct child of [text]).
            /// </summary>
            public string? ContentType
            {
                get => _contentType;
                set => _contentType = value?.RemoveHtmlXss();
            }

            private string? _styleCode;
            /// <summary>
            /// The values for [styleCode] for font effect are bold, italics and underline. 
            /// To assist people who are visually impaired, the [styleCode=”emphasis”] 
            /// e.g. bold, italics and underline
            /// </summary>
            public string? StyleCode
            {
                get => _styleCode;
                set => _styleCode = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Order of this content block within the parent section's [text] element.
            /// </summary>
            public int? SequenceNumber { get; set; } // Made nullable

            private string? _contentText;
            /// <summary>
            /// Actual text for Paragraphs. For List/Table types, details are in related tables. Inline markup (bold, italic, links etc) handled separately.
            /// </summary>
            public string? ContentText
            {
                get => _contentText;
                set => _contentText = value?.RemoveUnwantedTags(preserveTags: preserveTags);
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores details specific to [list] elements. Based on Section 2.2.2.4.
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

            private string? _listType;
            /// <summary>
            /// Attribute identifying the list as ordered or unordered ([list listType=]).
            /// </summary>
            public string? ListType
            {
                get => _listType;
                set => _listType = value?.RemoveHtmlXss();
            }

            private string? _styleCode;
            /// <summary>
            /// Optional style code for numbering/bullet style ([list styleCode=]).
            /// </summary>
            public string? StyleCode
            {
                get => _styleCode;
                set => _styleCode = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores individual [item] elements within a [list]. Based on Section 2.2.2.4.
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

            private string? _itemCaption;
            /// <summary>
            /// Optional custom marker specified using [caption] within [item].
            /// </summary>
            public string? ItemCaption
            {
                get => _itemCaption;
                set => _itemCaption = value?.RemoveHtmlXss();
            }

            private string? _itemText;
            /// <summary>
            /// Text content of the list item [item].
            /// </summary>
            public string? ItemText
            {
                get => _itemText;
                set => _itemText = value?.RemoveUnwantedTags(preserveTags: preserveTags);
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores details specific to [table] elements. Based on Section 2.2.2.5.
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

            private string? _width;
            /// <summary>
            /// Optional width attribute specified on the [table] element.
            /// </summary>
            public string? Width
            {
                get => _width;
                set => _width = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Indicates if the table included a [thead] element.
            /// </summary>
            public bool? HasHeader { get; set; } // Made nullable (Default is 0 (false) in SQL)

            /// <summary>
            /// Indicates if the table included a [tfoot] element.
            /// </summary>
            public bool? HasFooter { get; set; } // Made nullable (Default is 0 (false) in SQL)
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores individual [tr] elements within a [table] (header, body, or footer). Based on Section 2.2.2.5.
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

            private string? _rowGroupType;
            /// <summary>
            /// 'Header', 'Body', 'Footer' (corresponding to thead, tbody, tfoot).
            /// </summary>
            public string? RowGroupType
            {
                get => _rowGroupType;
                set => _rowGroupType = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Order of the row within its group (thead, tbody, tfoot).
            /// </summary>
            public int? SequenceNumber { get; set; } // Made nullable

            private string? _styleCode;
            /// <summary>
            /// Optional styleCode attribute on [tr] (e.g., Botrule).
            /// </summary>
            public string? StyleCode
            {
                get => _styleCode;
                set => _styleCode = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores individual [td] or [th] elements within a [tr]. Based on Section 2.2.2.5.
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

            private string? _cellType;
            /// <summary>
            /// 'td' or 'th'.
            /// </summary>
            public string? CellType
            {
                get => _cellType;
                set => _cellType = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Order of the cell within the row (column number).
            /// </summary>
            public int? SequenceNumber { get; set; } // Made nullable

            private string? _cellText;
            /// <summary>
            /// Text content of the table cell ([td] or [th]).
            /// </summary>
            public string? CellText
            {
                get => _cellText;
                set => _cellText = value?.RemoveUnwantedTags(preserveTags: preserveTags);
            }

            /// <summary>
            /// Optional rowspan attribute on [td] or [th].
            /// </summary>
            public int? RowSpan { get; set; } // Already nullable

            /// <summary>
            /// Optional colspan attribute on [td] or [th].
            /// </summary>
            public int? ColSpan { get; set; } // Already nullable

            private string? _styleCode;
            /// <summary>
            /// Optional styleCode attribute for cell rules (Lrule, Rrule, Toprule, Botrule).
            /// </summary>
            public string? StyleCode
            {
                get => _styleCode;
                set => _styleCode = value?.RemoveHtmlXss();
            }

            private string? _align;
            /// <summary>
            /// Optional align attribute for horizontal alignment.
            /// </summary>
            public string? Align
            {
                get => _align;
                set => _align = value?.RemoveHtmlXss();
            }

            private string? _vAlign;
            /// <summary>
            /// Optional valign attribute for vertical alignment.
            /// </summary>
            public string? VAlign
            {
                get => _vAlign;
                set => _vAlign = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores metadata for images ([observationMedia]). Based on Section 2.2.3.
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

            private string? _mediaID;
            /// <summary>
            /// Identifier for the media object ([observationMedia ID=]), referenced by [renderMultimedia].
            /// </summary>
            public string? MediaID
            {
                get => _mediaID;
                set => _mediaID = value?.RemoveHtmlXss();
            }

            private string? _descriptionText;
            /// <summary>
            /// Text description of the image ([text] child of observationMedia), used by screen readers.
            /// </summary>
            public string? DescriptionText
            {
                get => _descriptionText;
                set => _descriptionText = value?.RemoveHtmlXss();
            }

            private string? _mediaType;
            /// <summary>
            /// Media type of the file ([value mediaType=]), e.g., image/jpeg.
            /// </summary>
            public string? MediaType
            {
                get => _mediaType;
                set => _mediaType = value?.RemoveHtmlXss();
            }

            private string? _xsiType;
            /// <summary>
            /// Xsi type of the file ([value xsi:type=]), e.g., "ED".
            /// </summary>
            public string? XsiType
            {
                get => _xsiType;
                set => _xsiType = value?.RemoveHtmlXss();
            }

            private string? _fileName;
            /// <summary>
            /// File name of the image ([reference value=]).
            /// </summary>
            public string? FileName
            {
                get => _fileName;
                set => _fileName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Represents the [renderMultimedia] tag, linking text content to an ObservationMedia 
        /// entry. Based on Section 2.2.3. The  renderMultimedia tag in the text of a [paragraph] 
        /// as appropriate.Inline images are expected to be uncommon and basically represent 
        /// symbols that cannot be represented by Unicode characters. In addition, [caption] 
        /// are not applicable for inline images since these are not offset from the surrounding text.
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
            /// Indicates if the image is inline (within a paragraph) or block level (direct child of [text]).
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

            private string? _highlightText;
            /// <summary>
            /// Text content from [excerpt][highlight][text].
            /// </summary>
            public string? HighlightText
            {
                get => _highlightText;
                set => _highlightText = value?.RemoveUnwantedTags(preserveTags: new List<string>
                { "paragraph", "list", "item", "caption", "linkHtml" });
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores core product information ([manufacturedProduct]). Based on Section 3.1.
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

            private string? _productName;
            /// <summary>
            /// Proprietary name or product name ([name]).
            /// </summary>
            public string? ProductName
            {
                get => _productName;
                set => _productName = value?.RemoveHtmlXss();
            }

            private string? _productSuffix;
            /// <summary>
            /// Suffix to the proprietary name ([suffix]), e.g., "XR".
            /// </summary>
            public string? ProductSuffix
            {
                get => _productSuffix;
                set => _productSuffix = value?.RemoveHtmlXss();
            }

            private string? _formCode;
            /// <summary>
            /// Dosage form code, system, and display name ([formCode]).
            /// </summary>
            public string? FormCode
            {
                get => _formCode;
                set => _formCode = value?.RemoveHtmlXss();
            }

            private string? _formCodeSystem;
            /// <summary>
            /// Corresponds to [formCode codeSystem].
            /// </summary>
            public string? FormCodeSystem
            {
                get => _formCodeSystem;
                set => _formCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _formDisplayName;
            /// <summary>
            /// Corresponds to [formCode displayName].
            /// </summary>
            public string? FormDisplayName
            {
                get => _formDisplayName;
                set => _formDisplayName = value?.RemoveHtmlXss();
            }

            private string? _descriptionText;
            /// <summary>
            /// Brief description of the product ([desc]), mainly used for devices.
            /// </summary>
            public string? DescriptionText
            {
                get => _descriptionText;
                set => _descriptionText = value?.RemoveHtmlXss();
            }
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

            private string? _identifierValue;
            /// <summary>
            /// The item code value ([code code=]).
            /// </summary>
            public string? IdentifierValue
            {
                get => _identifierValue;
                set => _identifierValue = value?.RemoveHtmlXss();
            }

            private string? _identifierSystemOID;
            /// <summary>
            /// OID for the identifier system ([code codeSystem=]).
            /// </summary>
            public string? IdentifierSystemOID
            {
                get => _identifierSystemOID;
                set => _identifierSystemOID = value?.RemoveHtmlXss();
            }

            private string? _identifierType;
            /// <summary>
            /// Type classification of the identifier based on the OID.
            /// </summary>
            public string? IdentifierType
            {
                get => _identifierType;
                set => _identifierType = value?.RemoveHtmlXss();
            }
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

            private string? _genericName;
            /// <summary>
            /// Non-proprietary name of the product ([genericMedicine][name]).
            /// </summary>
            public string? GenericName
            {
                get => _genericName;
                set => _genericName = value?.RemoveHtmlXss();
            }

            private string? _phoneticName;
            /// <summary>
            /// Phonetic spelling of the generic name ([name use="PHON"]), optional.
            /// </summary>
            public string? PhoneticName
            {
                get => _phoneticName;
                set => _phoneticName = value?.RemoveHtmlXss();
            }
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

            private string? _kindCode;
            /// <summary>
            /// Code for the specialized kind (e.g., device product classification, cosmetic category).
            /// </summary>
            public string? KindCode
            {
                get => _kindCode;
                set => _kindCode = value?.RemoveHtmlXss();
            }

            private string? _kindCodeSystem;
            /// <summary>
            /// Code system for the specialized kind code (typically 2.16.840.1.113883.6.303).
            /// </summary>
            public string? KindCodeSystem
            {
                get => _kindCodeSystem;
                set => _kindCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _kindDisplayName;
            /// <summary>
            /// Display name matching the specialized kind code.
            /// </summary>
            public string? KindDisplayName
            {
                get => _kindDisplayName;
                set => _kindDisplayName = value?.RemoveHtmlXss();
            }
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

            private string? _equivalenceCode;
            /// <summary>
            /// Code indicating the type of equivalence relationship, e.g., C64637 (Same), pending (Predecessor).
            /// </summary>
            public string? EquivalenceCode
            {
                get => _equivalenceCode;
                set => _equivalenceCode = value?.RemoveHtmlXss();
            }

            private string? _equivalenceCodeSystem;
            /// <summary>
            /// Code system for EquivalenceCode.
            /// </summary>
            public string? EquivalenceCodeSystem
            {
                get => _equivalenceCodeSystem;
                set => _equivalenceCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _definingMaterialKindCode;
            /// <summary>
            /// Item code of the equivalent product (e.g., source NDC product code).
            /// </summary>
            public string? DefiningMaterialKindCode
            {
                get => _definingMaterialKindCode;
                set => _definingMaterialKindCode = value?.RemoveHtmlXss();
            }

            private string? _definingMaterialKindSystem;
            /// <summary>
            /// Code system for the equivalent product's item code.
            /// </summary>
            public string? DefiningMaterialKindSystem
            {
                get => _definingMaterialKindSystem;
                set => _definingMaterialKindSystem = value?.RemoveHtmlXss();
            }
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

            private string? _identifierTypeCode;
            /// <summary>
            /// Code for the type of identifier (e.g., C99286 Model Number, C99285 Catalog Number, C99287 Reference Number).
            /// </summary>
            public string? IdentifierTypeCode
            {
                get => _identifierTypeCode;
                set => _identifierTypeCode = value?.RemoveHtmlXss();
            }

            private string? _identifierTypeCodeSystem;
            /// <summary>
            /// Code system for IdentifierTypeCode.
            /// </summary>
            public string? IdentifierTypeCodeSystem
            {
                get => _identifierTypeCodeSystem;
                set => _identifierTypeCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _identifierTypeDisplayName;
            /// <summary>
            /// Display name for IdentifierTypeCode.
            /// </summary>
            public string? IdentifierTypeDisplayName
            {
                get => _identifierTypeDisplayName;
                set => _identifierTypeDisplayName = value?.RemoveHtmlXss();
            }

            private string? _identifierValue;
            /// <summary>
            /// The actual identifier value ([id extension]).
            /// </summary>
            public string? IdentifierValue
            {
                get => _identifierValue;
                set => _identifierValue = value?.RemoveHtmlXss();
            }

            private string? _identifierRootOID;
            /// <summary>
            /// The root OID associated with the identifier ([id root]).
            /// </summary>
            public string? IdentifierRootOID
            {
                get => _identifierRootOID;
                set => _identifierRootOID = value?.RemoveHtmlXss();
            }
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

            private string? _unii;
            /// <summary>
            /// Unique Ingredient Identifier ([code code=] where codeSystem="2.16.840.1.113883.4.9"). Optional for cosmetics.
            /// </summary>
            public string? UNII
            {
                get => _unii;
                set => _unii = value?.RemoveHtmlXss();
            }

            private string? _substanceName;
            /// <summary>
            /// Name of the substance (name).
            /// </summary>
            public string? SubstanceName
            {
                get => _substanceName;
                set => _substanceName = value?.RemoveHtmlXss();
            }

            private string? _originatingElement;
            /// <summary>
            /// The name of the XML element this ingredient was parsed from (e.g., "inactiveIngredientSubstance").
            /// </summary>
            public string? OriginatingElement
            {
                get => _originatingElement;
                set => _originatingElement = value?.RemoveHtmlXss();
            }
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

            private string? _moietyUNII;
            /// <summary>
            /// UNII code of the active moiety ([activeMoiety][code] code).
            /// </summary>
            public string? MoietyUNII
            {
                get => _moietyUNII;
                set => _moietyUNII = value?.RemoveHtmlXss();
            }

            private string? _moietyName;
            /// <summary>
            /// Name of the active moiety ([activeMoiety][name]).
            /// </summary>
            public string? MoietyName
            {
                get => _moietyName;
                set => _moietyName = value?.RemoveHtmlXss();
            }
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

            private string? _refSubstanceUNII;
            /// <summary>
            /// UNII code of the reference substance ([definingSubstance][code] code).
            /// </summary>
            public string? RefSubstanceUNII
            {
                get => _refSubstanceUNII;
                set => _refSubstanceUNII = value?.RemoveHtmlXss();
            }

            private string? _refSubstanceName;
            /// <summary>
            /// Name of the reference substance ([definingSubstance][name]).
            /// </summary>
            public string? RefSubstanceName
            {
                get => _refSubstanceName;
                set => _refSubstanceName = value?.RemoveHtmlXss();
            }
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
            /// Foreign key for the SpecifiedSubstance table.
            /// </summary>
            public int? SpecifiedSubstanceID { get; set; } // Made nullable

            /// <summary>
            /// Link to ReferenceSubstance table if BasisOfStrength (ClassCode) is ACTIR.
            /// </summary>
            public int? ReferenceSubstanceID { get; set; } // Already nullable

            /// <summary>
            /// FK to ProductConcept, used when the ingredient belongs to a Product Concept instead of a concrete Product. Null if linked via ProductID.
            /// </summary>
            public int? ProductConceptID { get; set; } // Already nullable

            private string? _classCode;
            /// <summary>
            /// Class code indicating ingredient type (e.g., ACTIB, ACTIM, ACTIR, IACT, INGR, COLR, CNTM, ADJV).
            /// </summary>
            public string? ClassCode
            {
                get => _classCode;
                set => _classCode = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Strength expressed as numerator/denominator value and unit ([quantity]). Null for CNTM unless zero numerator.
            /// </summary>
            public decimal? QuantityNumerator { get; set; } // Already nullable

            private string? _quantityNumeratorUnit;
            /// <summary>
            /// Corresponds to [quantity][numerator unit].
            /// </summary>
            public string? QuantityNumeratorUnit
            {
                get => _quantityNumeratorUnit;
                set => _quantityNumeratorUnit = value?.RemoveHtmlXss();
            }

            private string? _numeratorTranslationCode;
            /// <summary>
            /// Translation attribute for the numerator (e.g., translation code="C28253")
            /// </summary>
            public string? NumeratorTranslationCode
            {
                get => _numeratorTranslationCode;
                set => _numeratorTranslationCode = value?.RemoveHtmlXss();
            }

            private string? _numeratorCodeSystem;
            /// <summary>
            /// Translation attribute for the numerator (e.g., translation codeSystem="2.16.840.1.113883.3.26.1.1")
            /// </summary>
            public string? NumeratorCodeSystem
            {
                get => _numeratorCodeSystem;
                set => _numeratorCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _numeratorDisplayName;
            /// <summary>
            /// Translation attribute for the numerator (e.g., translation displayName="MILLIGRAM")
            /// </summary>
            public string? NumeratorDisplayName
            {
                get => _numeratorDisplayName;
                set => _numeratorDisplayName = value?.RemoveHtmlXss();
            }

            private string? _numeratorValue;
            /// <summary>
            /// Translation attribute for the numerator (e.g., translation value="50")
            /// </summary>
            public string? NumeratorValue
            {
                get => _numeratorValue;
                set => _numeratorValue = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Corresponds to [quantity][denominator value].
            /// </summary>
            public decimal? QuantityDenominator { get; set; } // Already nullable

            private string? _denominatorTranslationCode;
            /// <summary>
            /// Translation attribute for the numerator (e.g., translation code="C28253")
            /// </summary>
            public string? DenominatorTranslationCode
            {
                get => _denominatorTranslationCode;
                set => _denominatorTranslationCode = value?.RemoveHtmlXss();
            }

            private string? _denominatorCodeSystem;
            /// <summary>
            /// Translation attribute for the numerator (e.g., translation codeSystem="2.16.840.1.113883.3.26.1.1")
            /// </summary>
            public string? DenominatorCodeSystem
            {
                get => _denominatorCodeSystem;
                set => _denominatorCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _denominatorDisplayName;
            /// <summary>
            /// Translation attribute for the numerator (e.g., translation displayName="MILLIGRAM")
            /// </summary>
            public string? DenominatorDisplayName
            {
                get => _denominatorDisplayName;
                set => _denominatorDisplayName = value?.RemoveHtmlXss();
            }

            private string? _denominatorValue;
            /// <summary>
            /// Translation attribute for the numerator (e.g., translation value="50")
            /// </summary>
            public string? DenominatorValue
            {
                get => _denominatorValue;
                set => _denominatorValue = value?.RemoveHtmlXss();
            }

            private string? _quantityDenominatorUnit;
            /// <summary>
            /// Corresponds to [quantity][denominator unit].
            /// </summary>
            public string? QuantityDenominatorUnit
            {
                get => _quantityDenominatorUnit;
                set => _quantityDenominatorUnit = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Flag indicating if the inactive ingredient information is confidential ([confidentialityCode code="B"]).
            /// </summary>
            public bool? IsConfidential { get; set; } = false; // Made nullable (Default is 0 (false) in SQL)

            /// <summary>
            /// Order of the ingredient as listed in the SPL file (important for cosmetics).
            /// </summary>
            public int? SequenceNumber { get; set; } // Made nullable

            private string? _displayName;
            /// <summary>
            /// Display name (displayName="MILLIGRAM").
            /// </summary>
            public string? DisplayName
            {
                get => _displayName;
                set => _displayName = value?.RemoveHtmlXss();
            }

            private string? _originatingElement;
            /// <summary>
            /// The name of the XML element this ingredient was parsed from (e.g., "ingredient", "activeIngredient").
            /// </summary>
            public string? OriginatingElement
            {
                get => _originatingElement;
                set => _originatingElement = value?.RemoveHtmlXss();
            }

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

            private string? _sourceProductNDC;
            /// <summary>
            /// NDC Product Code of the source product used for the ingredient.
            /// </summary>
            public string? SourceProductNDC
            {
                get => _sourceProductNDC;
                set => _sourceProductNDC = value?.RemoveHtmlXss();
            }

            private string? _sourceProductNDCSysten;
            /// <summary>
            /// Code system for Source NDC.
            /// </summary>
            public string? SourceProductNDCSysten
            {
                get => _sourceProductNDCSysten;
                set => _sourceProductNDCSysten = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Represents a level of packaging ([asContent]/[containerPackagedProduct]). Links to ProductID/PartProductID for definitions OR ProductInstanceID for lot distribution container data (3.15 packaging, 16.2.8).
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
            /// Quantity and unit of the item contained within this package level ([quantity]).
            /// </summary>
            public decimal? QuantityNumerator { get; set; } // Made nullable

            /// <summary>
            /// Corresponds to [quantity][denominator value].
            /// </summary>
            public decimal? QuantityDenominator { get; set; } // Made nullable

            private string? _quantityNumeratorUnit;
            /// <summary>
            /// Corresponds to [quantity][numerator unit].
            /// </summary>
            public string? QuantityNumeratorUnit
            {
                get => _quantityNumeratorUnit;
                set => _quantityNumeratorUnit = value?.RemoveHtmlXss();
            }

            private string? _packageCode;
            /// <summary>
            /// The package item code value ([containerPackagedProduct][code code="..." /]).
            /// </summary>
            public string? PackageCode
            {
                get => _packageCode;
                set => _packageCode = value?.RemoveHtmlXss();
            }

            private string? _packageCodeSystem;
            /// <summary>
            /// The code system OID for the package item code ([containerPackagedProduct][code codeSystem="..." /]).
            /// </summary>
            public string? PackageCodeSystem
            {
                get => _packageCodeSystem;
                set => _packageCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _packageFormCode;
            /// <summary>
            /// Package type code, system, and display name ([containerPackagedProduct][formCode]).
            /// </summary>
            public string? PackageFormCode
            {
                get => _packageFormCode;
                set => _packageFormCode = value?.RemoveHtmlXss();
            }

            private string? _packageFormCodeSystem;
            /// <summary>
            /// Corresponds to [containerPackagedProduct][formCode codeSystem].
            /// </summary>
            public string? PackageFormCodeSystem
            {
                get => _packageFormCodeSystem;
                set => _packageFormCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _packageFormDisplayName;
            /// <summary>
            /// Corresponds to [containerPackagedProduct][formCode displayName].
            /// </summary>
            public string? PackageFormDisplayName
            {
                get => _packageFormDisplayName;
                set => _packageFormDisplayName = value?.RemoveHtmlXss();
            }

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

            private string? _identifierValue;
            /// <summary>
            /// The package item code value ([containerPackagedProduct][code] code).
            /// </summary>
            public string? IdentifierValue
            {
                get => _identifierValue;
                set => _identifierValue = value?.RemoveHtmlXss();
            }

            private string? _identifierSystemOID;
            /// <summary>
            /// OID for the package identifier system ([containerPackagedProduct][code] codeSystem).
            /// </summary>
            public string? IdentifierSystemOID
            {
                get => _identifierSystemOID;
                set => _identifierSystemOID = value?.RemoveHtmlXss();
            }

            private string? _identifierType;
            /// <summary>
            /// e.g., 'NDCPackage', 'NHRICPackage', 'GS1Package', 'HIBCCPackage', 'ISBTPackage'.
            /// </summary>
            public string? IdentifierType
            {
                get => _identifierType;
                set => _identifierType = value?.RemoveHtmlXss();
            }
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

            private string? _categoryCode;
            /// <summary>
            /// Code identifying the marketing category (e.g., NDA, ANDA, OTC Monograph Drug).
            /// </summary>
            public string? CategoryCode
            {
                get => _categoryCode;
                set => _categoryCode = value?.RemoveHtmlXss();
            }

            private string? _categoryCodeSystem;
            /// <summary>
            /// Marketing Category code system ([approval][code] codeSystem).
            /// </summary>
            public string? CategoryCodeSystem
            {
                get => _categoryCodeSystem;
                set => _categoryCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _categoryDisplayName;
            /// <summary>
            /// Marketing Category display name ([approval][code] displayName).
            /// </summary>
            public string? CategoryDisplayName
            {
                get => _categoryDisplayName;
                set => _categoryDisplayName = value?.RemoveHtmlXss();
            }

            private string? _applicationOrMonographIDValue;
            /// <summary>
            /// Application number, monograph ID, or citation ([id extension]).
            /// </summary>
            public string? ApplicationOrMonographIDValue
            {
                get => _applicationOrMonographIDValue;
                set => _applicationOrMonographIDValue = value?.RemoveHtmlXss();
            }

            private string? _applicationOrMonographIDOID;
            /// <summary>
            /// Root OID for the application number or monograph ID system ([id root]).
            /// </summary>
            public string? ApplicationOrMonographIDOID
            {
                get => _applicationOrMonographIDOID;
                set => _applicationOrMonographIDOID = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Date of application approval, if applicable ([effectiveTime][low value]).
            /// </summary>
            public DateTime? ApprovalDate { get; set; } // Already nullable

            private string? _territoryCode;
            /// <summary>
            /// Territory code, typically USA ([territory][code]).
            /// </summary>
            public string? TerritoryCode
            {
                get => _territoryCode;
                set => _territoryCode = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// FK to ProductConcept, used when the marketing category applies to an Application Product Concept instead of a concrete Product. Null if linked via ProductID.
            /// </summary>
            public int? ProductConceptID { get; set; } // Already nullable
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores marketing status information for a product or package ([subjectOf][marketingAct]). Based on Section 3.1.8.
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

            private string? _marketingActCode;
            /// <summary>
            /// Code for the marketing activity (e.g., C53292 Marketing, C96974 Drug Sample).
            /// </summary>
            public string? MarketingActCode
            {
                get => _marketingActCode;
                set => _marketingActCode = value?.RemoveHtmlXss();
            }

            private string? _marketingActCodeSystem;
            /// <summary>
            /// Code system for MarketingActCode.
            /// </summary>
            public string? MarketingActCodeSystem
            {
                get => _marketingActCodeSystem;
                set => _marketingActCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _statusCode;
            /// <summary>
            /// Status code: active, completed, new, cancelled.
            /// </summary>
            public string? StatusCode
            {
                get => _statusCode;
                set => _statusCode = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Marketing start date ([effectiveTime][low value]).
            /// </summary>
            public DateTime? EffectiveStartDate { get; set; } // Already nullable

            /// <summary>
            /// Marketing end date ([effectiveTime][high value]).
            /// </summary>
            public DateTime? EffectiveEndDate { get; set; } // Already nullable
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores characteristics of a product or package ([subjectOf][characteristic]). Based on Section 3.1.9.
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

            private string? _characteristicCode;
            /// <summary>
            /// Code identifying the characteristic property (e.g., SPLCOLOR, SPLSHAPE, SPLSIZE, SPLIMPRINT, SPLFLAVOR, SPLSCORE, SPLIMAGE, SPLCMBPRDTP, SPLPRODUCTIONAMOUNT, SPLUSE, SPLSTERILEUSE, SPLPROFESSIONALUSE, SPLSMALLBUSINESS).
            /// </summary>
            public string? CharacteristicCode
            {
                get => _characteristicCode;
                set => _characteristicCode = value?.RemoveHtmlXss();
            }

            private string? _characteristicCodeSystem;
            /// <summary>
            /// Code system for CharacteristicCode.
            /// </summary>
            public string? CharacteristicCodeSystem
            {
                get => _characteristicCodeSystem;
                set => _characteristicCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _valueType;
            /// <summary>
            /// Indicates the XML Schema instance type of the [value] element (e.g., PQ, INT, CV, ST, BL, IVL_PQ, ED).
            /// </summary>
            public string? ValueType
            {
                get => _valueType;
                set => _valueType = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Value for PQ type.
            /// </summary>
            public decimal? ValuePQ_Value { get; set; } // Already nullable

            private string? _valuePQ_Unit;
            /// <summary>
            /// Unit for PQ type.
            /// </summary>
            public string? ValuePQ_Unit
            {
                get => _valuePQ_Unit;
                set => _valuePQ_Unit = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Value for INT type.
            /// </summary>
            public int? ValueINT { get; set; } // Already nullable

            private string? _valueCV_Code;
            /// <summary>
            /// Code for CV type.
            /// </summary>
            public string? ValueCV_Code
            {
                get => _valueCV_Code;
                set => _valueCV_Code = value?.RemoveHtmlXss();
            }

            private string? _valueCV_CodeSystem;
            /// <summary>
            /// Code system for CV type.
            /// </summary>
            public string? ValueCV_CodeSystem
            {
                get => _valueCV_CodeSystem;
                set => _valueCV_CodeSystem = value?.RemoveHtmlXss();
            }

            private string? _valueCV_DisplayName;
            /// <summary>
            /// Display name for CV type.
            /// </summary>
            public string? ValueCV_DisplayName
            {
                get => _valueCV_DisplayName;
                set => _valueCV_DisplayName = value?.RemoveHtmlXss();
            }

            private string? _valueST;
            /// <summary>
            /// Value for ST type.
            /// </summary>
            public string? ValueST
            {
                get => _valueST;
                set => _valueST = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Value for BL type.
            /// </summary>
            public bool? ValueBL { get; set; } // Already nullable

            /// <summary>
            /// Low value for IVL_PQ type.
            /// </summary>
            public decimal? ValueIVLPQ_LowValue { get; set; } // Already nullable

            private string? _valueIVLPQ_LowUnit;
            /// <summary>
            /// Low unit for IVL_PQ type.
            /// </summary>
            public string? ValueIVLPQ_LowUnit
            {
                get => _valueIVLPQ_LowUnit;
                set => _valueIVLPQ_LowUnit = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// High value for IVL_PQ type.
            /// </summary>
            public decimal? ValueIVLPQ_HighValue { get; set; } // Already nullable

            private string? _valueIVLPQ_HighUnit;
            /// <summary>
            /// High unit for IVL_PQ type.
            /// </summary>
            public string? ValueIVLPQ_HighUnit
            {
                get => _valueIVLPQ_HighUnit;
                set => _valueIVLPQ_HighUnit = value?.RemoveHtmlXss();
            }

            private string? _valueED_MediaType;
            /// <summary>
            /// Media type for ED type.
            /// </summary>
            public string? ValueED_MediaType
            {
                get => _valueED_MediaType;
                set => _valueED_MediaType = value?.RemoveHtmlXss();
            }

            private string? _valueED_FileName;
            /// <summary>
            /// File name for ED type.
            /// </summary>
            public string? ValueED_FileName
            {
                get => _valueED_FileName;
                set => _valueED_FileName = value?.RemoveHtmlXss();
            }

            private string? _valueNullFlavor;
            /// <summary>
            /// Used for INT type with nullFlavor="PINF" (e.g., SPLUSE, SPLPRODUCTIONAMOUNT).
            /// </summary>
            public string? ValueNullFlavor
            {
                get => _valueNullFlavor;
                set => _valueNullFlavor = value?.RemoveHtmlXss();
            }
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
            /// Quantity and unit of this part contained within the parent kit product ([part][quantity]).
            /// </summary>
            public decimal? PartQuantityNumerator { get; set; } // Made nullable

            private string? _partQuantityNumeratorUnit;
            /// <summary>
            /// Unit for the part quantity ([quantity][numerator unit]).
            /// </summary>
            public string? PartQuantityNumeratorUnit
            {
                get => _partQuantityNumeratorUnit;
                set => _partQuantityNumeratorUnit = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Links products sold separately but intended for use together ([asPartOfAssembly]). Based on Section 3.1.6, 3.3.8.
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
            /// Foreign key to Product (The other product in the assembly, referenced via [part][partProduct] inside [asPartOfAssembly]).
            /// </summary>
            public int? AccessoryProductID { get; set; } // Made nullable
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores policy information related to a product, like DEA Schedule ([subjectOf][policy]). Based on Section 3.2.11.
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

            private string? _policyClassCode;
            /// <summary>
            /// Class code for the policy, e.g., DEADrugSchedule.
            /// </summary>
            public string? PolicyClassCode
            {
                get => _policyClassCode;
                set => _policyClassCode = value?.RemoveHtmlXss();
            }

            private string? _policyCode;
            /// <summary>
            /// Code representing the specific policy value (e.g., DEA Schedule C-II).
            /// </summary>
            public string? PolicyCode
            {
                get => _policyCode;
                set => _policyCode = value?.RemoveHtmlXss();
            }

            private string? _policyCodeSystem;
            /// <summary>
            /// Code system for the policy code (e.g., 2.16.840.1.113883.3.26.1.1 for DEA schedule).
            /// </summary>
            public string? PolicyCodeSystem
            {
                get => _policyCodeSystem;
                set => _policyCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _policyDisplayName;
            /// <summary>
            /// Display name matching the policy code.
            /// </summary>
            public string? PolicyDisplayName
            {
                get => _policyDisplayName;
                set => _policyDisplayName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Links a product (or part) to its route(s) of administration ([consumedIn][substanceAdministration]). Based on Section 3.2.20.
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

            private string? _routeCode;
            /// <summary>
            /// Code identifying the route of administration.
            /// </summary>
            public string? RouteCode
            {
                get => _routeCode;
                set => _routeCode = value?.RemoveHtmlXss();
            }

            private string? _routeCodeSystem;
            /// <summary>
            /// Code system for the route code (typically 2.16.840.1.113883.3.26.1.1).
            /// </summary>
            public string? RouteCodeSystem
            {
                get => _routeCodeSystem;
                set => _routeCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _routeDisplayName;
            /// <summary>
            /// Display name matching the route code.
            /// </summary>
            public string? RouteDisplayName
            {
                get => _routeDisplayName;
                set => _routeDisplayName = value?.RemoveHtmlXss();
            }

            private string? _routeNullFlavor;
            /// <summary>
            /// Stores nullFlavor attribute value (e.g., NA) when route code is not applicable.
            /// </summary>
            public string? RouteNullFlavor
            {
                get => _routeNullFlavor;
                set => _routeNullFlavor = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores the web page link for a cosmetic product ([subjectOf][document][text][reference value=]). Based on Section 3.4.7.
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

            private string? _webURL;
            /// <summary>
            /// Absolute URL for the product web page, starting with http:// or https://.
            /// </summary>
            public string? WebURL
            {
                get => _webURL;
                set => _webURL = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores various identifiers associated with an Organization (DUNS, FEI, Labeler Code, License Number, etc.).
        /// Section 2.1.4 Author Information and 2.1.5 Identified Organizations
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

            private string? _identifierValue;
            /// <summary>
            /// The identifier value ([id extension]) pg 14 2.1.4 Author Information.
            /// </summary>
            public string? IdentifierValue
            {
                get => _identifierValue;
                set => _identifierValue = value?.RemoveHtmlXss();
            }

            private string? _identifierSystemOID;
            /// <summary>
            /// OID for the identifier system ([id root]).
            /// </summary>
            public string? IdentifierSystemOID
            {
                get => _identifierSystemOID;
                set => _identifierSystemOID = value?.RemoveHtmlXss();
            }

            private string? _identifierType;
            /// <summary>
            /// Type classification of the identifier based on the OID and context.
            /// </summary>
            public string? IdentifierType
            {
                get => _identifierType;
                set => _identifierType = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Navigation property back to the parent Organization.
            /// </summary>
            public virtual Organization? Organization { get; set; }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores business operation details for an establishment or labeler ([performance][actDefinition]). Based on Section 4.1.4, 5.1.5, 6.1.6.
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

            private string? _operationCode;
            /// <summary>
            /// Code identifying the business operation.
            /// </summary>
            public string? OperationCode
            {
                get => _operationCode;
                set => _operationCode = value?.RemoveHtmlXss();
            }

            private string? _operationCodeSystem;
            /// <summary>
            /// Code system for the operation code (typically 2.16.840.1.113883.3.26.1.1).
            /// </summary>
            public string? OperationCodeSystem
            {
                get => _operationCodeSystem;
                set => _operationCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _operationDisplayName;
            /// <summary>
            /// Display name matching the operation code.
            /// </summary>
            public string? OperationDisplayName
            {
                get => _operationDisplayName;
                set => _operationDisplayName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores qualifier details for a specific Business Operation ([actDefinition][subjectOf][approval][code]). Based on Section 5.1.5, 6.1.7.
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

            private string? _qualifierCode;
            /// <summary>
            /// Code qualifying the business operation.
            /// </summary>
            public string? QualifierCode
            {
                get => _qualifierCode;
                set => _qualifierCode = value?.RemoveHtmlXss();
            }

            private string? _qualifierCodeSystem;
            /// <summary>
            /// Code system for the qualifier code (typically 2.16.840.1.113883.3.26.1.1).
            /// </summary>
            public string? QualifierCodeSystem
            {
                get => _qualifierCodeSystem;
                set => _qualifierCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _qualifierDisplayName;
            /// <summary>
            /// Display name matching the qualifier code.
            /// </summary>
            public string? QualifierDisplayName
            {
                get => _qualifierDisplayName;
                set => _qualifierDisplayName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Links a Business Operation performed by an establishment to a specific product ([actDefinition][product]). Based on Section 4.1.5.
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
        /// Stores legal authenticator (signature) information for a document ([legalAuthenticator]). Based on Section 5.1.6, 35.1.3, 36.1.7.
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

            private string? _noteText;
            /// <summary>
            /// Optional signing statement provided in [noteText].
            /// </summary>
            public string? NoteText
            {
                get => _noteText;
                set => _noteText = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Timestamp of the signature ([time value]).
            /// </summary>
            public DateTime? TimeValue { get; set; } // Made nullable

            private string? _signatureText;
            /// <summary>
            /// The electronic signature text ([signatureText]).
            /// </summary>
            public string? SignatureText
            {
                get => _signatureText;
                set => _signatureText = value?.RemoveHtmlXss();
            }

            private string? _assignedPersonName;
            /// <summary>
            /// Name of the person signing ([assignedPerson][name]).
            /// </summary>
            public string? AssignedPersonName
            {
                get => _assignedPersonName;
                set => _assignedPersonName = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Link to the signing Organization, used for FDA signers in Labeler Code Inactivation (Sec 5.1.6).
            /// </summary>
            public int? SignerOrganizationID { get; set; } // Already nullable
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores substance details (e.g., active moiety, pharmacologic class identifier) used in Indexing contexts ([subject][identifiedSubstance]). Based on Section 8.2.2, 8.2.3.
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

            private string? _subjectType;
            /// <summary>
            /// Indicates whether the identified substance represents an Active Moiety (8.2.2) or a Pharmacologic Class being defined (8.2.3).
            /// </summary>
            public string? SubjectType
            {
                get => _subjectType;
                set => _subjectType = value?.RemoveHtmlXss();
            }

            private string? _substanceIdentifierValue;
            /// <summary>
            /// Identifier value - UNII for Active Moiety, MED-RT/MeSH code for Pharm Class.
            /// </summary>
            public string? SubstanceIdentifierValue
            {
                get => _substanceIdentifierValue;
                set => _substanceIdentifierValue = value?.RemoveHtmlXss();
            }

            private string? _substanceIdentifierSystemOID;
            /// <summary>
            /// Identifier system OID - UNII (2.16.840.1.113883.4.9), MED-RT (2.16.840.1.113883.6.345), or MeSH (2.16.840.1.113883.6.177).
            /// </summary>
            public string? SubstanceIdentifierSystemOID
            {
                get => _substanceIdentifierSystemOID;
                set => _substanceIdentifierSystemOID = value?.RemoveHtmlXss();
            }

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

            private string? _classCode;
            /// <summary>
            /// The MED-RT or MeSH code for the pharmacologic class.
            /// </summary>
            public string? ClassCode
            {
                get => _classCode;
                set => _classCode = value?.RemoveHtmlXss();
            }

            private string? _classCodeSystem;
            /// <summary>
            /// Code system ([code] codeSystem).
            /// </summary>
            public string? ClassCodeSystem
            {
                get => _classCodeSystem;
                set => _classCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _classDisplayName;
            /// <summary>
            /// The display name for the class code, including the type suffix like [EPC] or [CS].
            /// </summary>
            public string? ClassDisplayName
            {
                get => _classDisplayName;
                set => _classDisplayName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores preferred (L) and alternate (A) names for a Pharmacologic Class ([identifiedSubstance][name use=]). Based on Section 8.2.3.
        /// </summary>
        public class PharmacologicClassName
        {
            #region properties
            /// <summary>
            /// Primary key for the PharmacologicClassName table.
            /// </summary>
            [Column("PharmClassNameID")]
            public int? PharmacologicClassNameID { get; set; } // Made nullable

            /// <summary>
            /// Foreign key to PharmacologicClass.
            /// </summary>
            public int? PharmacologicClassID { get; set; } // Made nullable

            private string? _nameValue;
            /// <summary>
            /// The text of the preferred or alternate name.
            /// </summary>
            public string? NameValue
            {
                get => _nameValue;
                set => _nameValue = value?.RemoveHtmlXss();
            }

            private string? _nameUse;
            /// <summary>
            /// Indicates if the name is preferred (L) or alternate (A).
            /// </summary>
            public string? NameUse
            {
                get => _nameUse;
                set => _nameUse = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Links an active moiety (IdentifiedSubstance) to its associated Pharmacologic Class ([asSpecializedKind] under moiety). Based on Section 8.2.2.
        /// </summary>
        public class PharmacologicClassLink
        {
            #region properties
            /// <summary>
            /// Primary key for the PharmacologicClassLink table.
            /// </summary>
            [Column("PharmClassLinkID")]
            public int? PharmacologicClassLinkID { get; set; } // Made nullable

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
        /// Defines the hierarchy between Pharmacologic Classes ([asSpecializedKind] under class definition). Based on Section 8.2.3.
        /// </summary>
        public class PharmacologicClassHierarchy
        {
            #region properties
            /// <summary>
            /// Primary key for the PharmacologicClassHierarchy table.
            /// </summary>
            [Column("PharmClassHierarchyID")]
            public int? PharmacologicClassHierarchyID { get; set; } // Made nullable

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

            private string? _packageNDCValue;
            /// <summary>
            /// The NDC Package Code being linked ([containerPackagedProduct][code] code).
            /// </summary>
            public string? PackageNDCValue
            {
                get => _packageNDCValue;
                set => _packageNDCValue = value?.RemoveHtmlXss();
            }

            private string? _packageNDCSystemOID;
            /// <summary>
            /// System for NDC.
            /// </summary>
            public string? PackageNDCSystemOID
            {
                get => _packageNDCSystemOID;
                set => _packageNDCSystemOID = value?.RemoveHtmlXss();
            }

            private string? _billingUnitCode;
            /// <summary>
            /// The NCPDP Billing Unit Code associated with the NDC package (GM, ML, or EA).
            /// </summary>
            public string? BillingUnitCode
            {
                get => _billingUnitCode;
                set => _billingUnitCode = value?.RemoveHtmlXss();
            }

            private string? _billingUnitCodeSystemOID;
            /// <summary>
            /// Code system OID for the NCPDP Billing Unit Code (2.16.840.1.113883.2.13).
            /// </summary>
            public string? BillingUnitCodeSystemOID
            {
                get => _billingUnitCodeSystemOID;
                set => _billingUnitCodeSystemOID = value?.RemoveHtmlXss();
            }
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

            private string? _conceptCode;
            /// <summary>
            /// The computed MD5 hash code identifying the product concept ([code] code).
            /// </summary>
            public string? ConceptCode
            {
                get => _conceptCode;
                set => _conceptCode = value?.RemoveHtmlXss();
            }

            private string? _conceptCodeSystem;
            /// <summary>
            /// OID for Product Concept Codes.
            /// </summary>
            public string? ConceptCodeSystem
            {
                get => _conceptCodeSystem;
                set => _conceptCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _conceptType;
            /// <summary>
            /// Distinguishes Abstract Product/Kit concepts from Application-specific Product/Kit concepts.
            /// </summary>
            public string? ConceptType
            {
                get => _conceptType;
                set => _conceptType = value?.RemoveHtmlXss();
            }

            private string? _formCode;
            /// <summary>
            /// Dosage Form details, applicable only for Abstract Product concepts.
            /// </summary>
            public string? FormCode
            {
                get => _formCode;
                set => _formCode = value?.RemoveHtmlXss();
            }

            private string? _formCodeSystem;
            /// <summary>
            /// Code system for FormCode.
            /// </summary>
            public string? FormCodeSystem
            {
                get => _formCodeSystem;
                set => _formCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _formDisplayName;
            /// <summary>
            /// Display name for FormCode.
            /// </summary>
            public string? FormDisplayName
            {
                get => _formDisplayName;
                set => _formDisplayName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Links an Application Product Concept to its corresponding Abstract Product Concept ([asEquivalentEntity]). Based on Section 15.2.6.
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

            private string? _equivalenceCode;
            /// <summary>
            /// Code indicating the relationship type between Application and Abstract concepts (A, B, OTC, N).
            /// </summary>
            public string? EquivalenceCode
            {
                get => _equivalenceCode;
                set => _equivalenceCode = value?.RemoveHtmlXss();
            }

            private string? _equivalenceCodeSystem;
            /// <summary>
            /// OID for this code system.
            /// </summary>
            public string? EquivalenceCodeSystem
            {
                get => _equivalenceCodeSystem;
                set => _equivalenceCodeSystem = value?.RemoveHtmlXss();
            }
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

            private string? _lotNumber;
            /// <summary>
            /// The lot number string.
            /// </summary>
            public string? LotNumber
            {
                get => _lotNumber;
                set => _lotNumber = value?.RemoveHtmlXss();
            }

            private string? _lotRootOID;
            /// <summary>
            /// The computed globally unique root OID for the lot number.
            /// </summary>
            public string? LotRootOID
            {
                get => _lotRootOID;
                set => _lotRootOID = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Represents an instance of a product (Fill Lot, Label Lot, Package Lot, Salvaged Lot) in Lot Distribution or Salvage Reports. Based on Section 16.2.5, 16.2.7, 16.2.11,  .
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

            private string? _instanceType;
            /// <summary>
            /// Type of lot instance: FillLot, LabelLot, PackageLot (for kits), SalvagedLot.
            /// </summary>
            public string? InstanceType
            {
                get => _instanceType;
                set => _instanceType = value?.RemoveHtmlXss();
            }

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

        /**************************************************************/
        /// <summary>
        /// Stores dosing specification for Lot Distribution calculations ([consumedIn][substanceAdministration]) based on SPL Implementation Guide Section 16.2.4.
        /// Provides validation attributes to ensure compliance with SPL dosing specification requirements.
        /// </summary>
        /// <remarks>
        /// This model represents the dosing specification used to compute the number of doses in any lot or container,
        /// supporting compliance with FDA regulations for fill lot/label lot requirements.
        /// All validation follows SPL Implementation Guide Section 16.2.4 requirements.
        /// </remarks>
        /// <seealso cref="Label"/>
        /// <seealso cref="Product"/>
        /// <seealso cref="ProductRouteOfAdministration"/>
        /// <seealso cref="DosingSpecificationValidationService"/>
        public class DosingSpecification
        {
            #region properties
            /**************************************************************/
            /// <summary>
            /// Primary key for the DosingSpecification table.
            /// </summary>
            /// <seealso cref="Label"/>
            public int? DosingSpecificationID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Foreign key reference to the associated Product entity.
            /// Links this dosing specification to a specific pharmaceutical product.
            /// </summary>
            /// <seealso cref="Product"/>
            /// <seealso cref="Label"/>
            [Required(ErrorMessage = "ProductID is required for dosing specifications.")]
            public int? ProductID { get; set; }

            #region route code properties
            private string? _routeCode;

            /**************************************************************/
            /// <summary>
            /// Route of administration code associated with the dose, following SPL Implementation Guide Section 3.2.20.2f.
            /// Must be from FDA SPL code system (2.16.840.1.113883.3.26.1.1) or include nullFlavor.
            /// </summary>
            /// <seealso cref="ProductRouteOfAdministration"/>
            /// <seealso cref="RouteCodeValidationAttribute"/>
            /// <seealso cref="Label"/>
            [Required(ErrorMessage = "Route code is required for dosing specifications (SPL IG 16.2.4.2).")]
            [RouteCodeValidation]
            public string? RouteCode
            {
                #region implementation
                get => _routeCode;
                set => _routeCode = value?.RemoveHtmlXss();
                #endregion
            }

            private string? _routeCodeSystem;

            /**************************************************************/
            /// <summary>
            /// Code system identifier for the RouteCode, typically FDA SPL system (2.16.840.1.113883.3.26.1.1).
            /// Required when RouteCode is specified to ensure proper code system context.
            /// </summary>
            /// <seealso cref="ProductRouteOfAdministration"/>
            /// <seealso cref="Label"/>
            [Required(ErrorMessage = "Route code system is required when route code is specified.")]
            public string? RouteCodeSystem
            {
                #region implementation
                get => _routeCodeSystem;
                set => _routeCodeSystem = value?.RemoveHtmlXss();
                #endregion
            }

            private string? _routeDisplayName;

            /**************************************************************/
            /// <summary>
            /// Human-readable display name for the RouteCode.
            /// Provides clear identification of the route of administration for users.
            /// </summary>
            /// <seealso cref="ProductRouteOfAdministration"/>
            /// <seealso cref="Label"/>
            public string? RouteDisplayName
            {
                #region implementation
                get => _routeDisplayName;
                set => _routeDisplayName = value?.RemoveHtmlXss();
                #endregion
            }
            #endregion

            #region dose quantity properties
            /**************************************************************/
            /// <summary>
            /// Numeric value representing a single dose quantity according to SPL Implementation Guide Section 16.2.4.3-16.2.4.6.
            /// Must be a valid number (may be 0) and should not contain spaces.
            /// </summary>
            /// <seealso cref="UCUMUnitValidationAttribute"/>
            /// <seealso cref="NoSpacesValidationAttribute"/>
            /// <seealso cref="Label"/>
            [Range(0, double.MaxValue, ErrorMessage = "Dose quantity value cannot be negative (SPL IG 16.2.4.4).")]
            [NoSpacesValidation]
            public decimal? DoseQuantityValue { get; set; }

            private string? _doseQuantityUnit;

            /**************************************************************/
            /// <summary>
            /// Unit of measure for the dose quantity, must conform to UCUM (Unified Code for Units of Measure) standards per SPL Implementation Guide Section 16.2.4.5.
            /// Common units include mg, mL, g, L, and complex units like mg/mL.
            /// </summary>
            /// <seealso cref="UCUMUnitValidationAttribute"/>
            /// <seealso cref="DosingSpecificationValidationService"/>
            /// <seealso cref="Label"/>
            [UCUMUnitValidation]
            public string? DoseQuantityUnit
            {
                #region implementation
                get => _doseQuantityUnit;
                set => _doseQuantityUnit = value?.RemoveHtmlXss();
                #endregion
            }
            #endregion

            #region validation properties
            private string? _routeNullFlavor;

            /**************************************************************/
            /// <summary>
            /// NullFlavor attribute for route code when the specific route is unknown or not applicable.
            /// Allows for flexible handling of route specifications in SPL documents.
            /// </summary>
            /// <seealso cref="RouteCodeValidationAttribute"/>
            /// <seealso cref="Label"/>
            public string? RouteNullFlavor
            {
                get => _routeNullFlavor;
                set => _routeNullFlavor = value?.RemoveHtmlXss();
            }
            #endregion

            #region custom validation
            /**************************************************************/
            /// <summary>
            /// Performs custom validation logic for the DosingSpecification entity.
            /// Validates the relationship between dose quantity value and unit according to SPL requirements.
            /// </summary>
            /// <param name="validationContext">The validation context containing model state and services.</param>
            /// <returns>Enumerable of ValidationResult objects for any validation failures.</returns>
            /// <seealso cref="DosingSpecificationValidationService"/>
            /// <seealso cref="Label"/>
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                #region implementation
                var results = new List<ValidationResult>();

                // SPL IG 16.2.4.3 - If dose quantity is specified, both value and unit should be present
                if (DoseQuantityValue.HasValue && string.IsNullOrWhiteSpace(DoseQuantityUnit))
                {
                    results.Add(new ValidationResult(
                        "Dose quantity unit is required when dose quantity value is specified (SPL IG 16.2.4.3).",
                        new[] { nameof(DoseQuantityUnit) }));
                }

                if (!string.IsNullOrWhiteSpace(DoseQuantityUnit) && !DoseQuantityValue.HasValue)
                {
                    results.Add(new ValidationResult(
                        "Dose quantity value is required when dose quantity unit is specified (SPL IG 16.2.4.3).",
                        new[] { nameof(DoseQuantityValue) }));
                }

                // Additional validation for zero values with meaningful units
                if (DoseQuantityValue.HasValue && DoseQuantityValue.Value == 0 && !string.IsNullOrWhiteSpace(DoseQuantityUnit))
                {
                    // SPL IG 16.2.4.6 allows zero values, but log for review
                    // This is informational validation, not an error
                }

                return results;
                #endregion
            }
            #endregion

            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Represents Bulk Lot information in Lot Distribution Reports ([productInstance][ingredient]). Based on Section 16.2.6.
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
        /// Defines the relationship between Fill/Package Lots and Label Lots ([productInstance][member][memberProductInstance]). Based on Section 16.2.7, 16.2.11.
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
        /// Stores product events like distribution or return quantities ([subjectOf][productEvent]). Based on Section 16.2.9, 16.2.10.
        /// </summary>
        /// <seealso cref="ProductEventParser"/>
        /// <seealso cref="ProductEventValidationService"/>
        /// <seealso cref="Label"/>
        public class ProductEvent : IValidatableObject
        {
            #region properties
            /// <summary>
            /// Primary key for the ProductEvent table.
            /// </summary>
            /// <seealso cref="Label"/>
            public int? ProductEventID { get; set; }

            /// <summary>
            /// Foreign key to PackagingLevel (The container level the event applies to).
            /// </summary>
            /// <seealso cref="PackagingLevel"/>
            /// <seealso cref="Label"/>
            [Required(ErrorMessage = "PackagingLevelID is required for product events.")]
            public int? PackagingLevelID { get; set; }

            private string? _eventCode;
            /// <summary>
            /// Code identifying the type of event (e.g., C106325 Distributed, C106328 Returned).
            /// </summary>
            /// <seealso cref="ProductEventCodeValidationAttribute"/>
            /// <seealso cref="Label"/>
            [ProductEventCodeValidation]
            public string? EventCode
            {
                get => _eventCode;
                set => _eventCode = value?.RemoveHtmlXss();
            }

            private string? _eventCodeSystem;
            /// <summary>
            /// Code system for EventCode.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? EventCodeSystem
            {
                get => _eventCodeSystem;
                set => _eventCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _eventDisplayName;
            /// <summary>
            /// Display name for EventCode.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? EventDisplayName
            {
                get => _eventDisplayName;
                set => _eventDisplayName = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Integer quantity associated with the event (e.g., number of containers distributed/returned).
            /// </summary>
            /// <seealso cref="ProductEventQuantityValidationAttribute"/>
            /// <seealso cref="Label"/>
            [ProductEventQuantityValidation]
            public int? QuantityValue { get; set; }

            private string? _quantityUnit;
            /// <summary>
            /// Unit for quantity (usually '1' or null).
            /// </summary>
            /// <seealso cref="Label"/>
            public string? QuantityUnit
            {
                get => _quantityUnit;
                set => _quantityUnit = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Effective date (low value), used for Initial Distribution Date.
            /// </summary>
            /// <seealso cref="ProductEventEffectiveTimeValidationAttribute"/>
            /// <seealso cref="Label"/>
            [ProductEventEffectiveTimeValidation]
            public DateTime? EffectiveTimeLow { get; set; }
            #endregion properties

            /**************************************************************/
            /// <summary>
            /// Performs custom validation logic for the ProductEvent entity.
            /// Validates the relationship between event code, quantity, and effective time according to SPL requirements.
            /// </summary>
            /// <param name="validationContext">The validation context containing model state and services.</param>
            /// <returns>Enumerable of ValidationResult objects for any validation failures.</returns>
            /// <seealso cref="ProductEventValidationService"/>
            /// <seealso cref="Label"/>
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                #region implementation
                var results = new List<ValidationResult>();

                // Use the validation service for comprehensive validation
                var logger = validationContext.GetService<ILogger<ProductEvent>>();
                if (logger != null)
                {
                    var validationService = new ProductEventValidationService(logger);
                    var validationResult = validationService.ValidateProductEvent(this);

                    if (!validationResult.IsValid)
                    {
                        foreach (var error in validationResult.Errors)
                        {
                            results.Add(new ValidationResult(error));
                        }
                    }
                }

                return results;
                #endregion
            }
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores "Doing Business As" (DBA) names or other named entity types associated with an 
        /// Organization ([asNamedEntity]). Based on Section 2.1.9, 18.1.3. 18.1.4
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

            private string? _entityTypeCode;
            /// <summary>
            /// Code identifying the type of named entity, e.g., C117113 for "doing business as".
            /// </summary>
            public string? EntityTypeCode
            {
                get => _entityTypeCode;
                set => _entityTypeCode = value?.RemoveHtmlXss();
            }

            private string? _entityTypeCodeSystem;
            /// <summary>
            /// Code system for EntityTypeCode.
            /// </summary>
            public string? EntityTypeCodeSystem
            {
                get => _entityTypeCodeSystem;
                set => _entityTypeCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _entityTypeDisplayName;
            /// <summary>
            /// Display name for EntityTypeCode.
            /// </summary>
            public string? EntityTypeDisplayName
            {
                get => _entityTypeDisplayName;
                set => _entityTypeDisplayName = value?.RemoveHtmlXss();
            }

            private string? _entityName;
            /// <summary>
            /// The name of the entity, e.g., the DBA name.
            /// </summary>
            public string? EntityName
            {
                get => _entityName;
                set => _entityName = value?.RemoveHtmlXss();
            }

            private string? _entitySuffix;
            /// <summary>
            /// Optional suffix used with DBA names in WDD/3PL reports to indicate business type ([WDD] or [3PL]).
            /// </summary>
            public string? EntitySuffix
            {
                get => _entitySuffix;
                set => _entitySuffix = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Represents the issuing authority (State or Federal Agency like DEA) for licenses 
        /// ([author][territorialAuthority]). Based on Section 18.1.5.
        /// </summary>
        /// <seealso cref="Label"/>
        /// <seealso cref="License"/>
        [TerritorialAuthorityConsistencyValidation]
        public class TerritorialAuthority
        {
            #region properties
            /**************************************************************/
            /// <summary>
            /// Primary key for the TerritorialAuthority table.
            /// </summary>
            /// <seealso cref="Label"/>
            public int? TerritorialAuthorityID { get; set; } // Made nullable

            private string? _territoryCode;
            /**************************************************************/
            /// <summary>
            /// ISO 3166-2 State code (e.g., US-MD) or ISO 3166-1 Country code (e.g., USA).
            /// Used to identify the territorial scope of the licensing authority.
            /// </summary>
            /// <remarks>
            /// For state authorities, use ISO 3166-2 format like "US-MD" for Maryland.
            /// For federal authorities, use ISO 3166-1 country code "USA".
            /// </remarks>
            /// <seealso cref="Label"/>
            /// <seealso cref="TerritoryCodeValidationAttribute"/>
            [TerritoryCodeValidation]
            public string? TerritoryCode
            {
                #region implementation
                get => _territoryCode;
                set => _territoryCode = value?.RemoveHtmlXss();
                #endregion
            }

            private string? _territoryCodeSystem;
            /**************************************************************/
            /// <summary>
            /// Code system OID for the territory code (e.g., '1.0.3166.2' for state, '1.0.3166.1.2.3' for country).
            /// Must match the type of territory code being used.
            /// </summary>
            /// <remarks>
            /// Use '1.0.3166.2' for ISO 3166-2 state codes and '1.0.3166.1.2.3' for ISO 3166-1 country codes.
            /// </remarks>
            /// <seealso cref="Label"/>
            /// <seealso cref="TerritoryCodeSystemValidationAttribute"/>
            [TerritoryCodeSystemValidation]
            public string? TerritoryCodeSystem
            {
                #region implementation
                get => _territoryCodeSystem;
                set => _territoryCodeSystem = value?.RemoveHtmlXss();
                #endregion
            }

            private string? _governingAgencyIdExtension;
            /**************************************************************/
            /// <summary>
            /// DUNS number of the federal governing agency (e.g., "004234790" for DEA).
            /// Required when territory code is "USA", prohibited otherwise.
            /// </summary>
            /// <remarks>
            /// DUNS (Data Universal Numbering System) numbers are 9-digit identifiers.
            /// DEA uses DUNS number "004234790" as specified in SPL IG 18.1.5.26.
            /// </remarks>
            /// <seealso cref="Label"/>
            /// <seealso cref="GoverningAgencyDunsNumberValidationAttribute"/>
            [GoverningAgencyDunsNumberValidation]
            public string? GoverningAgencyIdExtension
            {
                #region implementation
                get => _governingAgencyIdExtension;
                set => _governingAgencyIdExtension = value?.RemoveHtmlXss();
                #endregion
            }

            private string? _governingAgencyIdRoot;
            /**************************************************************/
            /// <summary>
            /// Root OID for governing agency identification ("1.3.6.1.4.1.519.1").
            /// Required when territory code is "USA", prohibited otherwise.
            /// </summary>
            /// <remarks>
            /// This is the standard OID root for DUNS-based agency identification as specified
            /// in SPL Implementation Guide Section 18.1.5.24.
            /// </remarks>
            /// <seealso cref="Label"/>
            /// <seealso cref="GoverningAgencyIdRootValidationAttribute"/>
            [GoverningAgencyIdRootValidation]
            public string? GoverningAgencyIdRoot
            {
                #region implementation
                get => _governingAgencyIdRoot;
                set => _governingAgencyIdRoot = value?.RemoveHtmlXss();
                #endregion
            }

            private string? _governingAgencyName;
            /**************************************************************/
            /// <summary>
            /// Name of the federal governing agency (e.g., "DEA" for Drug Enforcement Agency).
            /// Required when territory code is "USA", prohibited otherwise.
            /// </summary>
            /// <remarks>
            /// Must be "DEA" when DUNS number is "004234790" as specified in SPL IG 18.1.5.26.
            /// Other federal agencies may have different names but must follow SPL requirements.
            /// </remarks>
            /// <seealso cref="Label"/>
            /// <seealso cref="GoverningAgencyNameValidationAttribute"/>
            [GoverningAgencyNameValidation]
            public string? GoverningAgencyName
            {
                #region implementation
                get => _governingAgencyName;
                set => _governingAgencyName = value?.RemoveHtmlXss();
                #endregion
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores license information for WDD/3PL facilities ([subjectOf][approval]). Based on Section 18.1.5.
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

            
            private string? _licenseNumber;
            /// <summary>
            /// The license number string.
            /// </summary>

            [LicenseNumberValidation]
            public string? LicenseNumber
            {
                get => _licenseNumber;
                set => _licenseNumber = value?.RemoveHtmlXss();
            }

            private string? _licenseRootOID;
            /// <summary>
            /// The root OID identifying the issuing authority and context.
            /// </summary>

            [LicenseRootOIDValidation]
            public string? LicenseRootOID
            {
                get => _licenseRootOID;
                set => _licenseRootOID = value?.RemoveHtmlXss();
            }

            private string? _licenseTypeCode;
            /// <summary>
            /// Code indicating the type of approval/license (e.g., C118777 licensing).
            /// </summary>
            public string? LicenseTypeCode
            {
                get => _licenseTypeCode;
                set => _licenseTypeCode = value?.RemoveHtmlXss();
            }

            private string? _licenseTypeCodeSystem;
            /// <summary>
            /// Code system for LicenseTypeCode.
            /// </summary>

            [LicenseTypeCodeValidation]
            public string? LicenseTypeCodeSystem
            {
                get => _licenseTypeCodeSystem;
                set => _licenseTypeCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _licenseTypeDisplayName;
            /// <summary>
            /// Display name for LicenseTypeCode.
            /// </summary>
            public string? LicenseTypeDisplayName
            {
                get => _licenseTypeDisplayName;
                set => _licenseTypeDisplayName = value?.RemoveHtmlXss();
            }

            private string? _statusCode;
            /// <summary>
            /// Status of the license: active, suspended, aborted (revoked), completed (expired).
            /// </summary>
            [LicenseStatusCodeValidation]
            public string? StatusCode
            {
                get => _statusCode;
                set => _statusCode = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Expiration date of the license.
            /// </summary>
            [LicenseExpirationDateValidation]
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
        /// Based on SPL Implementation Guide Sections 18.1.7 and 23.2.9.
        /// </summary>
        /// <seealso cref="Label"/>
        [AttachedDocumentValidation]
        [AttachedDocumentFileValidation]
        [AttachedDocumentREMSValidation]
        [AttachedDocumentParentEntityValidation]
        public class AttachedDocument
        {
            #region properties
            /**************************************************************/
            /// <summary>
            /// Primary key for the AttachedDocument table.
            /// </summary>
            public int? AttachedDocumentID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Foreign key to the Section where this document is referenced. Can be null.
            /// </summary>
            /// <seealso cref="Section"/>
            public int? SectionID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Foreign key to a ComplianceAction, if the document is part of a drug listing or establishment inactivation. Can be null.
            /// </summary>
            /// <seealso cref="ComplianceAction"/>
            public int? ComplianceActionID { get; set; }

            /**************************************************************/
            /// <summary>
            /// Foreign key to a Product, if the document is related to a specific product (e.g., REMS material). Can be null.
            /// </summary>
            /// <seealso cref="Product"/>
            public int? ProductID { get; set; }


            private string? _parentEntityType;
            /**************************************************************/
            /// <summary>
            /// Identifies the type of the parent element containing the document reference (e.g., "DisciplinaryAction").
            /// </summary>
            /// <seealso cref="DisciplinaryAction"/>
            public string? ParentEntityType
            {
                get => _parentEntityType;
                set => _parentEntityType = value?.RemoveHtmlXss();
            }

            /**************************************************************/
            /// <summary>
            /// Foreign key to the parent table (e.g., DisciplinaryActionID).
            /// </summary>
            public int? ParentEntityID { get; set; }

            // --- Common and REMS-specific fields ---
            private string? _mediaType;
            /**************************************************************/
            /// <summary>
            /// MIME type of the attached document (e.g., "application/pdf").
            /// </summary>
            public string? MediaType
            {
                get => _mediaType;
                set => _mediaType = value?.RemoveHtmlXss();
            }

            private string? _fileName;
            /**************************************************************/
            /// <summary>
            /// File name of the attached document.
            /// </summary>
            public string? FileName
            {
                get => _fileName;
                set => _fileName = value?.RemoveHtmlXss();
            }

            private string? _documentIdRoot;
            /**************************************************************/
            /// <summary>
            /// The root identifier of the document from the [id] element, required for REMS materials (SPL IG 23.2.9.1).
            /// </summary>
            public string? DocumentIdRoot
            {
                get => _documentIdRoot;
                set => _documentIdRoot = value?.RemoveHtmlXss();
            }

            private string? _title;
            /**************************************************************/
            /// <summary>
            /// The title of the document reference (SPL IG 23.2.9.2).
            /// </summary>
            public string? Title
            {
                get => _title;
                set => _title = value?.RemoveHtmlXss();
            }

            private string? _titleReference;
            /**************************************************************/
            /// <summary>
            /// The ID referenced within the document's title, linking it to content in the section text (SPL IG 23.2.9.3).
            /// </summary>
            public string? TitleReference
            {
                get => _titleReference;
                set => _titleReference = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores disciplinary action details related to a License ([approval][subjectOf][action]). Based on Section 18.1.7.
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

            private string? _actionCode;
            /// <summary>
            /// Code identifying the disciplinary action type (e.g., suspension, revocation, activation).
            /// </summary>
            [DisciplinaryActionCodeValidation]
            [DisciplinaryActionLicenseStatusConsistencyValidation]
            public string? ActionCode
            {
                get => _actionCode;
                set => _actionCode = value?.RemoveHtmlXss();
            }

            private string? _actionCodeSystem;
            /// <summary>
            /// Code system for ActionCode.
            /// </summary>
            public string? ActionCodeSystem
            {
                get => _actionCodeSystem;
                set => _actionCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _actionDisplayName;
            /// <summary>
            /// Display name for ActionCode.
            /// </summary>
            public string? ActionDisplayName
            {
                get => _actionDisplayName;
                set => _actionDisplayName = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Date the disciplinary action became effective.
            /// </summary>
            [DisciplinaryActionEffectiveTimeValidation]
            public DateTime? EffectiveTime { get; set; } // Made nullable

            private string? _actionText;
            /// <summary>
            /// Text description used when the action code is 'other'.
            /// </summary>
            [DisciplinaryActionTextValidation]
            public string? ActionText
            {
                get => _actionText;
                set => _actionText = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores substance specification details for tolerance documents ([subjectOf][substanceSpecification]). Based on Section 19.2.3.
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

            private string? _specCode;
            /// <summary>
            /// Specification code, format 40-CFR-...
            /// </summary>
            [SubstanceSpecificationCodeValidation]
            public string? SpecCode
            {
                get => _specCode;
                set => _specCode = value?.RemoveHtmlXss();
            }

            private string? _specCodeSystem;
            /// <summary>
            /// Code system (2.16.840.1.113883.3.149).
            /// </summary>
            public string? SpecCodeSystem
            {
                get => _specCodeSystem;
                set => _specCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _enforcementMethodCode;
            /// <summary>
            /// Code for the Enforcement Analytical Method used ([observation][code]).
            /// </summary>
            [EnforcementMethodCodeValidation]
            public string? EnforcementMethodCode
            {
                get => _enforcementMethodCode;
                set => _enforcementMethodCode = value?.RemoveHtmlXss();
            }

            private string? _enforcementMethodCodeSystem;
            /// <summary>
            /// Code system for Enforcement Analytical Method.
            /// </summary>
            public string? EnforcementMethodCodeSystem
            {
                get => _enforcementMethodCodeSystem;
                set => _enforcementMethodCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _enforcementMethodDisplayName;
            /// <summary>
            /// Display name for Enforcement Analytical Method.
            /// </summary>
            public string? EnforcementMethodDisplayName
            {
                get => _enforcementMethodDisplayName;
                set => _enforcementMethodDisplayName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Links a Substance Specification to the analyte(s) being measured ([analyte][identifiedSubstance]). Based on Section 19.2.3.
        /// </summary>
        [AnalyteValidation]
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
        /// Stores commodity details referenced in tolerance specifications ([subject][presentSubstance][presentSubstance]). Based on Section 19.2.4.
        /// </summary>
        public class Commodity
        {
            #region properties
            /// <summary>
            /// Primary key for the Commodity table.
            /// </summary>
            public int? CommodityID { get; set; } // Made nullable

            private string? _commodityCode;
            /// <summary>
            /// Code identifying the commodity.
            /// </summary>
            [CommodityCodeValidation]
            public string? CommodityCode
            {
                get => _commodityCode;
                set => _commodityCode = value?.RemoveHtmlXss();
            }

            private string? _commodityCodeSystem;
            /// <summary>
            /// Code system for CommodityCode (2.16.840.1.113883.6.275.1).
            /// </summary>
            public string? CommodityCodeSystem
            {
                get => _commodityCodeSystem;
                set => _commodityCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _commodityDisplayName;
            /// <summary>
            /// Display name for CommodityCode.
            /// </summary>
            public string? CommodityDisplayName
            {
                get => _commodityDisplayName;
                set => _commodityDisplayName = value?.RemoveHtmlXss();
            }

            private string? _commodityName;
            /// <summary>
            /// Optional name ([presentSubstance][name]).
            /// </summary>
            public string? CommodityName
            {
                get => _commodityName;
                set => _commodityName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores application type details referenced in tolerance specifications ([subjectOf][approval][code]). Based on Section 19.2.4.
        /// </summary>
        public class ApplicationType
        {
            #region properties
            /// <summary>
            /// Primary key for the ApplicationType table.
            /// </summary>
            public int? ApplicationTypeID { get; set; } // Made nullable

            private string? _appTypeCode;
            /// <summary>
            /// Code identifying the application type (e.g., General Tolerance).
            /// </summary>
            public string? AppTypeCode
            {
                get => _appTypeCode;
                set => _appTypeCode = value?.RemoveHtmlXss();
            }

            private string? _appTypeCodeSystem;
            /// <summary>
            /// Code system for AppTypeCode (2.16.840.1.113883.6.275.1).
            /// </summary>
            [ApplicationTypeCodeValidation]
            public string? AppTypeCodeSystem
            {
                get => _appTypeCodeSystem;
                set => _appTypeCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _appTypeDisplayName;
            /// <summary>
            /// Display name for AppTypeCode.
            /// </summary>
            public string? AppTypeDisplayName
            {
                get => _appTypeDisplayName;
                set => _appTypeDisplayName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores the tolerance range and related details ([referenceRange][observationCriterion]). Based on Section 19.2.4.
        /// </summary>
        [ObservationCriterionConsistencyValidation]
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

            private string? _toleranceHighUnit;
            /// <summary>
            /// Tolerance unit ([value][high unit]).
            /// </summary>
            [ToleranceHighValueValidation]
            public string? ToleranceHighUnit
            {
                get => _toleranceHighUnit;
                set => _toleranceHighUnit = value?.RemoveHtmlXss();
            }

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
            [ToleranceExpirationDateValidation]
            public DateTime? ExpirationDate { get; set; } // Already nullable

            private string? _textNote;
            /// <summary>
            /// Optional text annotation about the tolerance.
            /// </summary>
            [ToleranceTextNoteValidation]
            public string? TextNote
            {
                get => _textNote;
                set => _textNote = value?.RemoveHtmlXss();
            }
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

            private string? _substanceCode;
            /// <summary>
            /// The code assigned to the specified substance.(Atribute code="70097M6I30")
            /// </summary>
            public string? SubstanceCode
            {
                get => _substanceCode;
                set => _substanceCode = value?.RemoveHtmlXss();
            }

            private string? _substanceCodeSystem;
            /// <summary>
            /// Code system for the specified substance code (Atribute codeSystem="2.16.840.1.113883.4.9").
            /// </summary>
            public string? SubstanceCodeSystem
            {
                get => _substanceCodeSystem;
                set => _substanceCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _substanceCodeSystemName;
            /// <summary>
            /// Code name for the specified substance code (Atribute codeSystemName="FDA SRS").
            /// </summary>
            public string? SubstanceCodeSystemName
            {
                get => _substanceCodeSystemName;
                set => _substanceCodeSystemName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores key product identification details referenced in a Warning Letter Alert Indexing document. Based on Section 21.2.2.
        /// </summary>
        public class WarningLetterProductInfo
        {
            #region properties
            [WarningLetterProductInfoValidation]
            [WarningLetterProductInfoConsistencyValidation]
            public WarningLetterProductInfo ValidateAll() => this;

            /// <summary>
            /// Primary key for the WarningLetterProductInfo table.
            /// </summary>
            public int? WarningLetterProductInfoID { get; set; } // Made nullable

            /// <summary>
            /// Foreign key to the Indexing Section (48779-3).
            /// </summary>
            public int? SectionID { get; set; } // Made nullable

            private string? _productName;
            /// <summary>
            /// Proprietary name of the product referenced in the warning letter.
            /// </summary>
            public string? ProductName
            {
                get => _productName;
                set => _productName = value?.RemoveHtmlXss();
            }

            private string? _genericName;
            /// <summary>
            /// Generic name of the product referenced in the warning letter.
            /// </summary>
            public string? GenericName
            {
                get => _genericName;
                set => _genericName = value?.RemoveHtmlXss();
            }

            private string? _formCode;
            /// <summary>
            /// Dosage form code of the product referenced in the warning letter.
            /// </summary>
            [WarningLetterFormCodeValidation]
            public string? FormCode
            {
                get => _formCode;
                set => _formCode = value?.RemoveHtmlXss();
            }

            private string? _formCodeSystem;
            /// <summary>
            /// Dosage Form code system.
            /// </summary>
            public string? FormCodeSystem
            {
                get => _formCodeSystem;
                set => _formCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _formDisplayName;
            /// <summary>
            /// Dosage Form display name.
            /// </summary>
            public string? FormDisplayName
            {
                get => _formDisplayName;
                set => _formDisplayName = value?.RemoveHtmlXss();
            }

            private string? _strengthText;
            /// <summary>
            /// Text description of the ingredient strength(s).
            /// </summary>
            public string? StrengthText
            {
                get => _strengthText;
                set => _strengthText = value?.RemoveHtmlXss();
            }

            private string? _itemCodesText;
            /// <summary>
            /// Text description of the product item code(s) (e.g., NDC).
            /// </summary>
            [WarningLetterItemCodesValidation]
            public string? ItemCodesText
            {
                get => _itemCodesText;
                set => _itemCodesText = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores the issue date and optional resolution date for a warning letter alert. Based on Section 21.2.3.
        /// </summary>
        public class WarningLetterDate
        {
            #region properties
            [WarningLetterDateConsistencyValidation]
            public WarningLetterDate ValidateAll() => this;

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
            [WarningLetterDateValidation] 
            public DateTime? AlertIssueDate { get; set; } // Made nullable

            /// <summary>
            /// Date the issue described in the warning letter was resolved, if applicable.
            /// </summary>
            [WarningLetterDateValidation]
            public DateTime? ResolutionDate { get; set; } // Already nullable
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores the Application Holder organization linked to a Marketing Category for REMS products ([holder][role][playingOrganization]). Based on Section 23.2.3.
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
        /// Represents a REMS protocol defined within a section ([protocol] element). Based on Section 23.2.6.
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

            private string? _protocolCode;
            /// <summary>
            /// Protocol code with automatic REMS validation.
            /// The REMSProtocolCodeValidation attribute will validate:
            /// - Code is not null/empty
            /// - Code system is FDA SPL system (2.16.840.1.113883.3.26.1.1)
            /// - Code contains only alphanumeric characters
            /// </summary>
            [REMSProtocolCodeValidation(ErrorMessage = "Invalid REMS protocol code")]
            public string? ProtocolCode
            {
                get => _protocolCode;
                set => _protocolCode = value?.RemoveHtmlXss();
            }

            private string? _protocolCodeSystem;
            /// <summary>
            /// Code system for ProtocolCode.
            /// </summary>
            public string? ProtocolCodeSystem
            {
                get => _protocolCodeSystem;
                set => _protocolCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _protocolDisplayName;
            /// <summary>
            /// Display name for ProtocolCode.
            /// </summary>
            public string? ProtocolDisplayName
            {
                get => _protocolDisplayName;
                set => _protocolDisplayName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Lookup table for REMS stakeholder types ([stakeholder]). Based on Section 23.2.7.
        /// </summary>
        public class Stakeholder
        {
            #region properties
            /// <summary>
            /// Primary key for the Stakeholder table.
            /// </summary>
            public int? StakeholderID { get; set; } // Made nullable

            private string? _stakeholderCode;
            /// <summary>
            /// Code identifying the stakeholder role (e.g., prescriber, patient).
            /// </summary>
            // <summary>
            /// Stakeholder code with automatic REMS validation.
            /// Validates stakeholder role codes (prescriber, patient, etc.)
            /// </summary>
            [REMSStakeholderCodeValidation(ErrorMessage = "Invalid stakeholder code")]
            public string? StakeholderCode
            {
                get => _stakeholderCode;
                set => _stakeholderCode = value?.RemoveHtmlXss();
            }

            private string? _stakeholderCodeSystem;
            /// <summary>
            /// Code system for StakeholderCode.
            /// </summary>
            public string? StakeholderCodeSystem
            {
                get => _stakeholderCodeSystem;
                set => _stakeholderCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _stakeholderDisplayName;
            /// <summary>
            /// Display name for StakeholderCode.
            /// </summary>
            public string? StakeholderDisplayName
            {
                get => _stakeholderDisplayName;
                set => _stakeholderDisplayName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores references to REMS materials, linking to attached documents if applicable ([subjectOf][document]). Based on Section 23.2.9.
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

            private string? _title;
            /// <summary>
            /// Title of the material ([document][title]).
            /// </summary>
            public string? Title
            {
                get => _title;
                set => _title = value?.RemoveHtmlXss();
            }

            private string? _titleReference;
            /// <summary>
            /// Internal link ID (#...) embedded within the title, potentially linking to descriptive text.
            /// </summary>
            public string? TitleReference
            {
                get => _titleReference;
                set => _titleReference = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Link to the AttachedDocument table if the material is provided as an attachment (e.g., PDF).
            /// </summary>
            public int? AttachedDocumentID { get; set; } // Already nullable
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Represents a REMS requirement or monitoring observation within a protocol ([component][requirement] or [monitoringObservation]). Based on Section 23.2.7.
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
            [Range(1, 3, ErrorMessage = "Sequence number must be 1, 2, or 3")]
            public int? RequirementSequenceNumber { get; set; } // Made nullable

            /// <summary>
            /// Flag: True if [monitoringObservation], False if [requirement].
            /// </summary>
            public bool? IsMonitoringObservation { get; set; } // Made nullable (Default is 0 (false) in SQL)

            /// <summary>
            /// Optional delay (pause) relative to the start/end of the previous step.
            /// </summary>
            public decimal? PauseQuantityValue { get; set; } // Already nullable

            private string? _pauseQuantityUnit;
            /// <summary>
            /// Optional delay unit ([pauseQuantity unit]).
            /// </summary>
            public string? PauseQuantityUnit
            {
                get => _pauseQuantityUnit;
                set => _pauseQuantityUnit = value?.RemoveHtmlXss();
            }

            private string? _requirementCode;
            /// <summary>
            /// Code identifying the specific requirement or monitoring observation.
            /// </summary>
            [REMSRequirementValidation(ErrorMessage = "Invalid REMS requirement")]
            public string? RequirementCode
            {
                get => _requirementCode;
                set => _requirementCode = value?.RemoveHtmlXss();
            }

            private string? _requirementCodeSystem;
            /// <summary>
            /// Code system for RequirementCode.
            /// </summary>
            public string? RequirementCodeSystem
            {
                get => _requirementCodeSystem;
                set => _requirementCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _requirementDisplayName;
            /// <summary>
            /// Display name for RequirementCode.
            /// </summary>
            public string? RequirementDisplayName
            {
                get => _requirementDisplayName;
                set => _requirementDisplayName = value?.RemoveHtmlXss();
            }

            private string? _originalTextReference;
            /// <summary>
            /// Link ID (#...) pointing to the corresponding text description in the REMS Summary or REMS Participant Requirements section.
            /// </summary>
            public string? OriginalTextReference
            {
                get => _originalTextReference;
                set => _originalTextReference = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Optional repetition period for the requirement/observation.
            /// </summary>
            public decimal? PeriodValue { get; set; } // Already nullable

            private string? _periodUnit;
            /// <summary>
            /// Optional repetition period unit ([effectiveTime][period unit]).
            /// </summary>
            public string? PeriodUnit
            {
                get => _periodUnit;
                set => _periodUnit = value?.RemoveHtmlXss();
            }

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
        /// Stores the REMS approval details associated with the first protocol mention ([subjectOf][approval]). Based on Section 23.2.8.
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

            private string? _approvalCode;
            /// <summary>
            /// Code for REMS Approval (C128899).
            /// </summary>
            public string? ApprovalCode
            {
                get => _approvalCode;
                set => _approvalCode = value?.RemoveHtmlXss();
            }

            private string? _approvalCodeSystem;
            /// <summary>
            /// Code system for ApprovalCode.
            /// </summary>
            public string? ApprovalCodeSystem
            {
                get => _approvalCodeSystem;
                set => _approvalCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _approvalDisplayName;
            /// <summary>
            /// Display name for ApprovalCode.
            /// </summary>
            public string? ApprovalDisplayName
            {
                get => _approvalDisplayName;
                set => _approvalDisplayName = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Date of the initial REMS program approval.
            /// </summary>
            public DateTime? ApprovalDate { get; set; } // Made nullable

            private string? _territoryCode;
            /// <summary>
            /// Territory code ('USA').
            /// </summary>
            public string? TerritoryCode
            {
                get => _territoryCode;
                set => _territoryCode = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores references to REMS electronic resources (URLs or URNs) ([subjectOf][document]). Based on Section 23.2.10.
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

            private string? _title;
            /// <summary>
            /// Title of the resource ([document][title]).
            /// </summary>
            public string? Title
            {
                get => _title;
                set => _title = value?.RemoveHtmlXss();
            }

            private string? _titleReference;
            /// <summary>
            /// Internal link ID (#...) embedded within the title, potentially linking to descriptive text.
            /// </summary>
            public string? TitleReference
            {
                get => _titleReference;
                set => _titleReference = value?.RemoveHtmlXss();
            }

            private string? _resourceReferenceValue;
            /// <summary>
            /// The URI (URL or URN) of the electronic resource.
            /// </summary>
            public string? ResourceReferenceValue
            {
                get => _resourceReferenceValue;
                set => _resourceReferenceValue = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Links an establishment (within a Blanket No Changes Certification doc) to a product being certified ([performance][actDefinition][product]). Based on Section 28.1.3.
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
            /// Foreign key to ProductIdentifier.
            /// </summary>
            public int? ProductIdentifierID { get; set; } // Made nullable
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores FDA-initiated inactivation/reactivation status for Drug Listings 
        /// (linked via PackageIdentifierID) or Establishment Registrations 
        /// (linked via DocumentRelationshipID). Based on Section 30.2.3, 31.1.4.
        /// </summary>
        [ComplianceActionContextValidation]
        [ComplianceActionConsistencyValidation]
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

            private string? _actionCode;
            /// <summary>
            /// Code for the compliance action (e.g., C162847 Inactivated).
            /// </summary>
            
            [ComplianceActionCodeValidation]
            public string? ActionCode
            {
                get => _actionCode;
                set => _actionCode = value?.RemoveHtmlXss();
            }

            private string? _actionCodeSystem;
            /// <summary>
            /// Code system for ActionCode.
            /// </summary>
            public string? ActionCodeSystem
            {
                get => _actionCodeSystem;
                set => _actionCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _actionDisplayName;
            /// <summary>
            /// Display name for ActionCode.
            /// </summary>
            public string? ActionDisplayName
            {
                get => _actionDisplayName;
                set => _actionDisplayName = value?.RemoveHtmlXss();
            }

            /// <summary>
            /// Date the inactivation begins.
            /// </summary>
            [ComplianceActionEffectiveTimeLowValidation]
            public DateTime? EffectiveTimeLow { get; set; } // Made nullable

            /// <summary>
            /// Date the inactivation ends (reactivation date), if applicable.
            /// </summary>
            [ComplianceActionEffectiveTimeHighValidation]
            public DateTime? EffectiveTimeHigh { get; set; } // Already nullable
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Represents a drug interaction issue within a specific section ([subjectOf][issue]). Based on Section 32.2.3.
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

            private string? _interactionCode;
            /// <summary>
            /// Code identifying an interaction issue (C54708).
            /// </summary>
            public string? InteractionCode
            {
                get => _interactionCode;
                set => _interactionCode = value?.RemoveHtmlXss();
            }

            private string? _interactionCodeSystem;
            /// <summary>
            /// Code system.
            /// </summary>
            public string? InteractionCodeSystem
            {
                get => _interactionCodeSystem;
                set => _interactionCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _interactionDisplayName;
            /// <summary>
            /// Display name ('INTERACTION').
            /// </summary>
            public string? InteractionDisplayName
            {
                get => _interactionDisplayName;
                set => _interactionDisplayName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Links an InteractionIssue to the contributing substance/class ([issue][subject][substanceAdministrationCriterion]). Based on Section 32.2.4.
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
        /// Stores the consequence (pharmacokinetic effect or medical problem) of an InteractionIssue ([risk][consequenceObservation]). Based on Section 32.2.5.
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

            private string? _consequenceTypeCode;
            /// <summary>
            /// Code indicating the type of consequence: Pharmacokinetic effect (C54386) or Medical problem (44100-6).
            /// </summary>
            public string? ConsequenceTypeCode
            {
                get => _consequenceTypeCode;
                set => _consequenceTypeCode = value?.RemoveHtmlXss();
            }

            private string? _consequenceTypeCodeSystem;
            /// <summary>
            /// Code system.
            /// </summary>
            public string? ConsequenceTypeCodeSystem
            {
                get => _consequenceTypeCodeSystem;
                set => _consequenceTypeCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _consequenceTypeDisplayName;
            /// <summary>
            /// Display name.
            /// </summary>
            public string? ConsequenceTypeDisplayName
            {
                get => _consequenceTypeDisplayName;
                set => _consequenceTypeDisplayName = value?.RemoveHtmlXss();
            }

            private string? _consequenceValueCode;
            /// <summary>
            /// Code for the specific pharmacokinetic effect or medical problem.
            /// </summary>
            public string? ConsequenceValueCode
            {
                get => _consequenceValueCode;
                set => _consequenceValueCode = value?.RemoveHtmlXss();
            }

            private string? _consequenceValueCodeSystem;
            /// <summary>
            /// Code system for the value code (NCI Thesaurus 2.16.840.1.113883.3.26.1.1 or SNOMED CT 2.16.840.1.113883.6.96).
            /// </summary>
            public string? ConsequenceValueCodeSystem
            {
                get => _consequenceValueCodeSystem;
                set => _consequenceValueCodeSystem = value?.RemoveHtmlXss();
            }

            private string? _consequenceValueDisplayName;
            /// <summary>
            /// Display name for the value code.
            /// </summary>
            public string? ConsequenceValueDisplayName
            {
                get => _consequenceValueDisplayName;
                set => _consequenceValueDisplayName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Stores the link between an indexing section and a National Clinical Trials number ([protocol][id]). Based on Section 33.2.2.
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

            private string? _nctNumber;
            /// <summary>
            /// The National Clinical Trials number (id extension).
            /// </summary>
            public string? NCTNumber
            {
                get => _nctNumber;
                set => _nctNumber = value?.RemoveHtmlXss();
            }

            private string? _nctRootOID;
            /// <summary>
            /// The root OID for NCT numbers (id root).
            /// </summary>
            public string? NCTRootOID
            {
                get => _nctRootOID;
                set => _nctRootOID = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Links a Facility (in Registration or Listing docs) to a Cosmetic Product ([performance][actDefinition][product]). Link via ProductID, ProductIdentifierID (CLN), or ProductName. Based on Section 35.2.2, 36.1.6.
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

            private string? _productName;
            /// <summary>
            /// Link via Product Name (used if CLN not yet assigned).
            /// </summary>
            public string? ProductName
            {
                get => _productName;
                set => _productName = value?.RemoveHtmlXss();
            }
            #endregion properties
        }

        /*******************************************************************************/
        /// <summary>
        /// Links a Cosmetic Product (in Facility Reg doc) to its Responsible Person organization ([manufacturerOrganization]). Based on Section 35.2.3.
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
        #endregion
    }
}