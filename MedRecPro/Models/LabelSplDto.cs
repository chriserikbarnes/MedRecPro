//using System.Xml;
//using System.Xml.Serialization;


//namespace MedRecPro.Models
//{

//    /**************************************************************/
//    /// <summary>
//    /// Root DTO for generating SPL (Structured Product Labeling) XML documents.
//    /// Represents the top-level document element with all required header information,
//    /// authors, and structured body content following FDA SPL specifications.
//    /// </summary>
//    /// <seealso cref="Label.Document"/>
//    /// <seealso cref="SplConstants"/>
//    [XmlRoot(SplConstants.E.Document, Namespace = "urn:hl7-org:v3")]
//    public class SplDocumentDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Document identifier with root and optional extension attributes.
//        /// Maps to the SPL document's primary identification element.
//        /// </summary>
//        /// <seealso cref="Label.Document.DocumentGUID"/>
//        [XmlElement(SplConstants.E.Id)]
//        public SplIdentifierDto DocumentId { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Document type code with system and display name information.
//        /// Identifies the specific type of SPL document being generated.
//        /// </summary>
//        /// <seealso cref="Label.Document.DocumentCode"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto DocumentCode { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Document title element with optional media type for rich content.
//        /// Provides human-readable identification of the document.
//        /// </summary>
//        /// <seealso cref="Label.Document.Title"/>
//        [XmlElement(SplConstants.E.Title)]
//        public SplTitleDto Title { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Effective time indicating when this document version is valid.
//        /// Critical for document versioning and regulatory compliance.
//        /// </summary>
//        /// <seealso cref="Label.Document.EffectiveTime"/>
//        [XmlElement(SplConstants.E.EffectiveTime)]
//        public SplEffectiveTimeDto EffectiveTime { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Set identifier linking related document versions together.
//        /// Remains constant across all versions of the same document.
//        /// </summary>
//        /// <seealso cref="Label.Document.SetGUID"/>
//        [XmlElement(SplConstants.E.SetId)]
//        public SplIdentifierDto SetId { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Version number for this specific document iteration.
//        /// Increments with each new version of the document set.
//        /// </summary>
//        /// <seealso cref="Label.Document.VersionNumber"/>
//        [XmlElement(SplConstants.E.VersionNumber)]
//        public SplVersionNumberDto VersionNumber { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of document authors, typically including labeler organizations.
//        /// Supports multiple authors with different roles and relationships.
//        /// </summary>
//        /// <seealso cref="Label.DocumentAuthor"/>
//        [XmlElement(SplConstants.E.Author)]
//        public List<SplAuthorDto> Authors { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of related documents including predecessors and core documents.
//        /// Establishes relationships between different SPL document versions.
//        /// </summary>
//        /// <seealso cref="Label.RelatedDocument"/>
//        [XmlElement(SplConstants.E.RelatedDocument)]
//        public List<SplRelatedDocumentDto> RelatedDocuments { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Legal authenticator for signed documents with signature information.
//        /// Required for certain document types requiring legal validation.
//        /// </summary>
//        /// <seealso cref="Label.LegalAuthenticator"/>
//        [XmlElement(SplConstants.E.LegalAuthenticator)]
//        public SplLegalAuthenticatorDto? LegalAuthenticator { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Main document body component containing all structured content.
//        /// Houses the primary information organized in sections and subsections.
//        /// </summary>
//        /// <seealso cref="Label.StructuredBody"/>
//        [XmlElement(SplConstants.E.Component)]
//        public SplComponentDto Component { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL active ingredient elements containing quantity and substance information.
//    /// Represents active pharmaceutical ingredients with their strength specifications.
//    /// </summary>
//    /// <seealso cref="Label.Ingredient"/>
//    public class SplActiveIngredientDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Quantity element describing the ingredient strength or amount.
//        /// Contains numerator and denominator for precise strength specification.
//        /// </summary>
//        /// <seealso cref="Label.Ingredient.QuantityNumerator"/>
//        [XmlElement("quantity")]
//        public SplQuantityDto? Quantity { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Active ingredient substance element containing identification and names.
//        /// Provides the chemical identity and naming information for the active component.
//        /// </summary>
//        /// <seealso cref="Label.IngredientSubstance"/>
//        [XmlElement("activeIngredientSubstance")]
//        public SplIngredientSubstanceDto ActiveIngredientSubstance { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL inactive ingredient elements containing substance information.
//    /// Represents inactive pharmaceutical ingredients and excipients.
//    /// </summary>
//    /// <seealso cref="Label.Ingredient"/>
//    public class SplInactiveIngredientDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Inactive ingredient substance element containing identification and names.
//        /// Provides the chemical identity and naming information for the inactive component.
//        /// </summary>
//        /// <seealso cref="Label.IngredientSubstance"/>
//        [XmlElement("inactiveIngredientSubstance")]
//        public SplIngredientSubstanceDto InactiveIngredientSubstance { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL identifier elements containing root OID and optional extension.
//    /// Used throughout SPL documents for various identification purposes.
//    /// </summary>
//    /// <seealso cref="Label.Document"/>
//    /// <seealso cref="Label.Organization"/>
//    public class SplIdentifierDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Root OID (Object Identifier) for the identification system.
//        /// Provides the authoritative namespace for the identifier.
//        /// </summary>
//        /// <seealso cref="Label.Document.DocumentGUID"/>
//        [XmlAttribute(SplConstants.A.Root)]
//        public string Root { get; set; } = string.Empty;

//        /**************************************************************/
//        /// <summary>
//        /// Extension value within the root OID namespace.
//        /// Contains the actual identifier value when applicable.
//        /// </summary>
//        /// <seealso cref="Label.Document.VersionNumber"/>
//        [XmlAttribute(SplConstants.A.Extension)]
//        public string? Extension { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL code elements with coded values and display information.
//    /// Standard pattern for representing coded concepts throughout SPL documents.
//    /// </summary>
//    /// <seealso cref="Label.Document"/>
//    /// <seealso cref="Label.Section"/>
//    public class SplCodeDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// The coded value from the specified code system.
//        /// Provides machine-readable identification of concepts.
//        /// </summary>
//        /// <seealso cref="Label.Document.DocumentCode"/>
//        [XmlAttribute(SplConstants.A.CodeValue)]
//        public string Code { get; set; } = string.Empty;

//        /**************************************************************/
//        /// <summary>
//        /// OID of the code system defining the code's meaning.
//        /// Establishes the authoritative source for code interpretation.
//        /// </summary>
//        /// <seealso cref="Label.Document.DocumentCodeSystem"/>
//        [XmlAttribute(SplConstants.A.CodeSystem)]
//        public string CodeSystem { get; set; } = string.Empty;

//        /**************************************************************/
//        /// <summary>
//        /// Human-readable name or description of the coded concept.
//        /// Provides context and meaning for the coded value.
//        /// </summary>
//        /// <seealso cref="Label.Document.DocumentDisplayName"/>
//        [XmlAttribute(SplConstants.A.DisplayName)]
//        public string? DisplayName { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Optional name of the code system for additional clarity.
//        /// Supplements the OID with a human-readable system name.
//        /// </summary>
//        [XmlAttribute(SplConstants.A.CodeSystemName)]
//        public string? CodeSystemName { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL title elements supporting both plain text and rich content.
//    /// Can contain simple text or complex markup depending on media type.
//    /// </summary>
//    /// <seealso cref="Label.Document.Title"/>
//    /// <seealso cref="Label.Section.Title"/>
//    public class SplTitleDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Media type indicating the format of the title content.
//        /// Typically "text/x-hl7-title+xml" for rich content or omitted for plain text.
//        /// </summary>
//        [XmlAttribute(SplConstants.A.MediaType)]
//        public string? MediaType { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// The title text content, which may be plain text or XML markup.
//        /// Contains the actual title information for display purposes.
//        /// </summary>
//        [XmlText]
//        public string Content { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL effective time elements supporting various time formats.
//    /// Handles both simple timestamps and complex time ranges.
//    /// </summary>
//    /// <seealso cref="Label.Document.EffectiveTime"/>
//    /// <seealso cref="Label.Section.EffectiveTime"/>
//    public class SplEffectiveTimeDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Simple timestamp value in HL7 format (YYYYMMDD or YYYYMMDDHHMMSS).
//        /// Used for single point-in-time effective dates.
//        /// </summary>
//        [XmlAttribute(SplConstants.A.Value)]
//        public string? Value { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Low boundary for time ranges indicating start of effective period.
//        /// Used in conjunction with High for date ranges.
//        /// </summary>
//        /// <seealso cref="Label.Section.EffectiveTimeLow"/>
//        [XmlElement(SplConstants.E.Low)]
//        public SplTimeValueDto? Low { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// High boundary for time ranges indicating end of effective period.
//        /// Optional element used to close time ranges.
//        /// </summary>
//        /// <seealso cref="Label.Section.EffectiveTimeHigh"/>
//        [XmlElement(SplConstants.E.High)]
//        public SplTimeValueDto? High { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for individual time value elements within effective time ranges.
//    /// Provides granular control over time boundary specifications.
//    /// </summary>
//    /// <seealso cref="SplEffectiveTimeDto"/>
//    public class SplTimeValueDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Timestamp value in HL7 format for this time boundary.
//        /// Specifies the exact moment for the time constraint.
//        /// </summary>
//        [XmlAttribute(SplConstants.A.Value)]
//        public string Value { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL version number elements with numeric values.
//    /// Represents document version information for change tracking.
//    /// </summary>
//    /// <seealso cref="Label.Document.VersionNumber"/>
//    public class SplVersionNumberDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Numeric version identifier as string for XML serialization.
//        /// Typically increments with each document revision.
//        /// </summary>
//        [XmlAttribute(SplConstants.A.Value)]
//        public string Value { get; set; } = "1";

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL author elements representing document authoring organizations.
//    /// Contains the complete organizational hierarchy and contact information.
//    /// </summary>
//    /// <seealso cref="Label.DocumentAuthor"/>
//    /// <seealso cref="Label.Organization"/>
//    public class SplAuthorDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Optional timestamp indicating when the authoring relationship was established.
//        /// Typically omitted for most SPL documents.
//        /// </summary>
//        [XmlElement(SplConstants.E.Time)]
//        public SplTimeValueDto? Time { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Assigned entity containing the organizational details and relationships.
//        /// Primary container for author identification and contact information.
//        /// </summary>
//        /// <seealso cref="Label.DocumentAuthor"/>
//        [XmlElement(SplConstants.E.AssignedEntity)]
//        public SplAssignedEntityDto AssignedEntity { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL assigned entity elements containing organizational hierarchies.
//    /// Supports complex organizational relationships and business operations.
//    /// </summary>
//    /// <seealso cref="Label.DocumentAuthor"/>
//    /// <seealso cref="Label.DocumentRelationship"/>
//    public class SplAssignedEntityDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Primary identifier for simple author types without complex hierarchies.
//        /// Used when the author is a single identified entity.
//        /// </summary>
//        [XmlElement(SplConstants.E.Id)]
//        public SplIdentifierDto? Id { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Represented organization for complex labeler author types.
//        /// Contains the complete organizational hierarchy and relationships.
//        /// </summary>
//        /// <seealso cref="Label.Organization"/>
//        [XmlElement(SplConstants.E.RepresentedOrganization)]
//        public SplOrganizationDto? RepresentedOrganization { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Collection of performance elements representing business operations.
//        /// Contains manufacturing, testing, and other regulated activities.
//        /// </summary>
//        /// <seealso cref="Label.BusinessOperation"/>
//        [XmlElement(SplConstants.E.Performance)]
//        public List<SplPerformanceDto> Performances { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Assigned organization element for nested organizational structures.
//        /// Used in complex organizational hierarchies with multiple levels.
//        /// </summary>
//        /// <seealso cref="Label.Organization"/>
//        [XmlElement(SplConstants.E.AssignedOrganization)]
//        public SplAssignedOrganizationDto? AssignedOrganization { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL assigned organization elements in nested structures.
//    /// Represents intermediate organizational levels in complex hierarchies.
//    /// </summary>
//    /// <seealso cref="Label.Organization"/>
//    /// <seealso cref="Label.DocumentRelationship"/>
//    public class SplAssignedOrganizationDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Organization identifier with root OID and extension.
//        /// Provides unique identification for the organization entity.
//        /// </summary>
//        /// <seealso cref="Label.OrganizationIdentifier"/>
//        [XmlElement(SplConstants.E.Id)]
//        public SplIdentifierDto Id { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Organization name for display and identification purposes.
//        /// Contains the official business name of the organization.
//        /// </summary>
//        /// <seealso cref="Label.Organization.OrganizationName"/>
//        [XmlElement(SplConstants.E.Name)]
//        public string Name { get; set; } = string.Empty;

//        /**************************************************************/
//        /// <summary>
//        /// Nested assigned entity for complex organizational hierarchies.
//        /// Allows for multiple levels of organizational relationships.
//        /// </summary>
//        /// <seealso cref="Label.DocumentRelationship"/>
//        [XmlElement(SplConstants.E.AssignedEntity)]
//        public SplAssignedEntityDto? AssignedEntity { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL organization elements with hierarchical relationships.
//    /// Represents organizations, establishments, and their business operations.
//    /// </summary>
//    /// <seealso cref="Label.Organization"/>
//    /// <seealso cref="Label.DocumentRelationship"/>
//    public class SplOrganizationDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Primary organizational identifier with root OID and extension.
//        /// Typically contains DUNS numbers, FEI numbers, or labeler codes.
//        /// </summary>
//        /// <seealso cref="Label.OrganizationIdentifier"/>
//        [XmlElement(SplConstants.E.Id)]
//        public List<SplIdentifierDto> Identifiers { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Organization name for display and identification purposes.
//        /// Contains the official business name of the organization.
//        /// </summary>
//        /// <seealso cref="Label.Organization.OrganizationName"/>
//        [XmlElement(SplConstants.E.Name)]
//        public string Name { get; set; } = string.Empty;

//        /**************************************************************/
//        /// <summary>
//        /// Confidentiality code indicating if organization information is protected.
//        /// Used when business details need to be marked as confidential.
//        /// </summary>
//        /// <seealso cref="Label.Organization.IsConfidential"/>
//        [XmlElement(SplConstants.E.ConfidentialityCode)]
//        public SplCodeDto? ConfidentialityCode { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Physical address information for the organization.
//        /// Contains street address, city, state, postal code, and country.
//        /// </summary>
//        /// <seealso cref="Label.Address"/>
//        [XmlElement(SplConstants.E.Addr)]
//        public SplAddressDto? Address { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Contact party information including address and telecommunications.
//        /// Provides detailed contact information for the organization.
//        /// </summary>
//        /// <seealso cref="Label.ContactParty"/>
//        [XmlElement(SplConstants.E.ContactParty)]
//        public SplContactPartyDto? ContactParty { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Collection of assigned entities representing child organizations.
//        /// Supports organizational hierarchies like registrant-establishment relationships.
//        /// </summary>
//        /// <seealso cref="Label.DocumentRelationship"/>
//        [XmlElement(SplConstants.E.AssignedEntity)]
//        public List<SplAssignedEntityDto> AssignedEntities { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of business operations performed by this organization.
//        /// Includes manufacturing, testing, packaging, and other regulated activities.
//        /// </summary>
//        /// <seealso cref="Label.BusinessOperation"/>
//        [XmlElement(SplConstants.E.Performance)]
//        public List<SplPerformanceDto> Performances { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL address elements containing geographic location information.
//    /// Supports both domestic and international address formats.
//    /// </summary>
//    /// <seealso cref="Label.Address"/>
//    public class SplAddressDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Collection of street address lines for detailed location information.
//        /// Typically includes street number, street name, and unit information.
//        /// </summary>
//        /// <seealso cref="Label.Address.StreetAddressLine1"/>
//        /// <seealso cref="Label.Address.StreetAddressLine2"/>
//        [XmlElement(SplConstants.E.StreetAddressLine)]
//        public List<string> StreetAddressLines { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// City or municipality name for the address location.
//        /// Required component of most address formats.
//        /// </summary>
//        /// <seealso cref="Label.Address.City"/>
//        [XmlElement(SplConstants.E.City)]
//        public string? City { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// State, province, or administrative region identifier.
//        /// Required for addresses within certain countries like the USA.
//        /// </summary>
//        /// <seealso cref="Label.Address.StateProvince"/>
//        [XmlElement(SplConstants.E.State)]
//        public string? State { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Postal code or ZIP code for mail delivery purposes.
//        /// Format varies by country and postal system.
//        /// </summary>
//        /// <seealso cref="Label.Address.PostalCode"/>
//        [XmlElement(SplConstants.E.PostalCode)]
//        public string? PostalCode { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Country information with code and optional display name.
//        /// Uses ISO country codes for international standardization.
//        /// </summary>
//        /// <seealso cref="Label.Address.CountryCode"/>
//        [XmlElement(SplConstants.E.Country)]
//        public SplCountryDto? Country { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL country elements with coded values and display names.
//    /// Represents country information using ISO standard codes.
//    /// </summary>
//    /// <seealso cref="Label.Address"/>
//    public class SplCountryDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// ISO country code for machine-readable country identification.
//        /// Typically uses ISO 3166-1 alpha-3 format (e.g., "USA").
//        /// </summary>
//        /// <seealso cref="Label.Address.CountryCode"/>
//        [XmlAttribute(SplConstants.A.CodeValue)]
//        public string Code { get; set; } = string.Empty;

//        /**************************************************************/
//        /// <summary>
//        /// Human-readable country name for display purposes.
//        /// Provides context for the country code.
//        /// </summary>
//        /// <seealso cref="Label.Address.CountryName"/>
//        [XmlText]
//        public string? Name { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL contact party elements containing detailed contact information.
//    /// Includes address, telecommunications, and contact person details.
//    /// </summary>
//    /// <seealso cref="Label.ContactParty"/>
//    public class SplContactPartyDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Physical address for the contact party.
//        /// May differ from the organization's primary address.
//        /// </summary>
//        /// <seealso cref="Label.ContactParty.AddressID"/>
//        [XmlElement(SplConstants.E.Addr)]
//        public SplAddressDto? Address { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Collection of telecommunications information including phone and email.
//        /// Provides multiple contact methods for the organization.
//        /// </summary>
//        /// <seealso cref="Label.ContactPartyTelecom"/>
//        [XmlElement(SplConstants.E.Telecom)]
//        public List<SplTelecomDto> Telecoms { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Specific contact person information within the organization.
//        /// Provides individual contact details when available.
//        /// </summary>
//        /// <seealso cref="Label.ContactPerson"/>
//        [XmlElement(SplConstants.E.ContactPerson)]
//        public SplContactPersonDto? ContactPerson { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL telecommunications elements supporting various communication methods.
//    /// Handles phone numbers, email addresses, and fax numbers.
//    /// </summary>
//    /// <seealso cref="Label.Telecom"/>
//    public class SplTelecomDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Telecommunication value with protocol prefix (tel:, mailto:, fax:).
//        /// Contains the complete contact information including protocol identifier.
//        /// </summary>
//        /// <seealso cref="Label.Telecom.TelecomValue"/>
//        [XmlAttribute(SplConstants.A.Value)]
//        public string Value { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL contact person elements with individual name information.
//    /// Represents specific individuals within contact organizations.
//    /// </summary>
//    /// <seealso cref="Label.ContactPerson"/>
//    public class SplContactPersonDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Full name of the contact person for identification purposes.
//        /// Contains the individual's name within the organization.
//        /// </summary>
//        /// <seealso cref="Label.ContactPerson.ContactPersonName"/>
//        [XmlElement(SplConstants.E.Name)]
//        public string Name { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL performance elements representing business operations.
//    /// Contains operation definitions and associated qualifiers.
//    /// </summary>
//    /// <seealso cref="Label.BusinessOperation"/>
//    public class SplPerformanceDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Activity definition containing the business operation details.
//        /// Describes the specific regulated activity being performed.
//        /// </summary>
//        /// <seealso cref="Label.BusinessOperation"/>
//        [XmlElement(SplConstants.E.ActDefinition)]
//        public SplActDefinitionDto ActDefinition { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL activity definition elements describing business operations.
//    /// Contains operation codes and associated product links.
//    /// </summary>
//    /// <seealso cref="Label.BusinessOperation"/>
//    public class SplActDefinitionDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Coded value identifying the specific business operation type.
//        /// Uses FDA-defined codes for regulated activities.
//        /// </summary>
//        /// <seealso cref="Label.BusinessOperation.OperationCode"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto Code { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of operation qualifiers providing additional context.
//        /// Describes specific aspects or limitations of the operation.
//        /// </summary>
//        /// <seealso cref="Label.BusinessOperationQualifier"/>
//        [XmlElement(SplConstants.E.SubjectOf)]
//        public List<SplSubjectOfDto> SubjectOf { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of products associated with this business operation.
//        /// Links specific products to the regulated activities performed.
//        /// </summary>
//        /// <seealso cref="Label.BusinessOperationProductLink"/>
//        [XmlElement(SplConstants.E.Product)]
//        public List<SplProductLinkDto> Products { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL subject-of elements containing approval and qualification information.
//    /// Used for business operation qualifiers and similar relationships.
//    /// </summary>
//    /// <seealso cref="Label.BusinessOperationQualifier"/>
//    public class SplSubjectOfDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Approval element containing qualifier codes and descriptions.
//        /// Provides additional context for business operations.
//        /// </summary>
//        /// <seealso cref="Label.BusinessOperationQualifier"/>
//        [XmlElement(SplConstants.E.Approval)]
//        public SplApprovalDto? Approval { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL approval elements with coded qualifications.
//    /// Represents approvals, qualifiers, and regulatory classifications.
//    /// </summary>
//    /// <seealso cref="Label.BusinessOperationQualifier"/>
//    /// <seealso cref="Label.MarketingCategory"/>
//    public class SplApprovalDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Coded value for the approval or qualifier type.
//        /// Identifies the specific regulatory classification or qualification.
//        /// </summary>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto Code { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL product link elements connecting operations to specific products.
//    /// Represents manufactured products within business operation contexts.
//    /// </summary>
//    /// <seealso cref="Label.BusinessOperationProductLink"/>
//    public class SplProductLinkDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Manufactured product element containing product identification.
//        /// Links business operations to specific pharmaceutical products.
//        /// </summary>
//        /// <seealso cref="Label.Product"/>
//        [XmlElement(SplConstants.E.ManufacturedProduct)]
//        public SplManufacturedProductLinkDto ManufacturedProduct { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for manufactured product links containing material kind identification.
//    /// Used within business operations to reference specific products.
//    /// </summary>
//    /// <seealso cref="Label.Product"/>
//    public class SplManufacturedProductLinkDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Material kind element containing product identification codes.
//        /// References products using NDC or other standard identifiers.
//        /// </summary>
//        /// <seealso cref="Label.ProductIdentifier"/>
//        [XmlElement(SplConstants.E.ManufacturedMaterialKind)]
//        public SplMaterialKindDto ManufacturedMaterialKind { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL material kind elements with product identification codes.
//    /// Contains coded references to specific pharmaceutical products.
//    /// </summary>
//    /// <seealso cref="Label.ProductIdentifier"/>
//    public class SplMaterialKindDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Product identification code such as NDC product code.
//        /// Provides machine-readable reference to specific products.
//        /// </summary>
//        /// <seealso cref="Label.ProductIdentifier.IdentifierValue"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto Code { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL related document elements establishing document relationships.
//    /// Links documents to predecessors, core documents, and reference materials.
//    /// </summary>
//    /// <seealso cref="Label.RelatedDocument"/>
//    public class SplRelatedDocumentDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Type code indicating the nature of the document relationship.
//        /// Common values include RPLC (replaces), APND (appends), DRIV (derived from).
//        /// </summary>
//        /// <seealso cref="Label.RelatedDocument.RelationshipTypeCode"/>
//        [XmlAttribute(SplConstants.A.TypeCode)]
//        public string TypeCode { get; set; } = string.Empty;

//        /**************************************************************/
//        /// <summary>
//        /// Related document element containing identification and metadata.
//        /// References the target document in the relationship.
//        /// </summary>
//        [XmlElement(SplConstants.E.RelatedDocument)]
//        public SplRelatedDocumentContentDto RelatedDocument { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for the content of related document references.
//    /// Contains identification and type information for referenced documents.
//    /// </summary>
//    /// <seealso cref="Label.RelatedDocument"/>
//    public class SplRelatedDocumentContentDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Document identifier for the referenced document.
//        /// Used for RPLC relationships to identify specific document versions.
//        /// </summary>
//        /// <seealso cref="Label.RelatedDocument.ReferencedDocumentGUID"/>
//        [XmlElement(SplConstants.E.Id)]
//        public SplIdentifierDto? Id { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Set identifier linking related document versions.
//        /// Primary identifier for establishing document relationships.
//        /// </summary>
//        /// <seealso cref="Label.RelatedDocument.ReferencedSetGUID"/>
//        [XmlElement(SplConstants.E.SetId)]
//        public SplIdentifierDto SetId { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Version number of the referenced document.
//        /// Specifies which version of the document set is being referenced.
//        /// </summary>
//        /// <seealso cref="Label.RelatedDocument.ReferencedVersionNumber"/>
//        [XmlElement(SplConstants.E.VersionNumber)]
//        public SplVersionNumberDto? VersionNumber { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Document type code for the referenced document.
//        /// Identifies the type of document being referenced.
//        /// </summary>
//        /// <seealso cref="Label.RelatedDocument.ReferencedDocumentCode"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto? Code { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL legal authenticator elements containing signature information.
//    /// Required for documents needing legal validation and electronic signatures.
//    /// </summary>
//    /// <seealso cref="Label.LegalAuthenticator"/>
//    public class SplLegalAuthenticatorDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Timestamp indicating when the document was signed.
//        /// Records the moment of legal authentication.
//        /// </summary>
//        /// <seealso cref="Label.LegalAuthenticator.TimeValue"/>
//        [XmlElement(SplConstants.E.Time)]
//        public SplTimeValueDto Time { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Electronic signature text validating the document content.
//        /// Contains the cryptographic or electronic signature information.
//        /// </summary>
//        /// <seealso cref="Label.LegalAuthenticator.SignatureText"/>
//        [XmlElement(SplConstants.E.SignatureText)]
//        public string SignatureText { get; set; } = string.Empty;

//        /**************************************************************/
//        /// <summary>
//        /// Optional note providing context for the signature.
//        /// May contain signing statements or other explanatory text.
//        /// </summary>
//        /// <seealso cref="Label.LegalAuthenticator.NoteText"/>
//        [XmlElement(SplConstants.E.NoteText)]
//        public string? NoteText { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Assigned entity containing signer identification and organization.
//        /// Identifies who performed the legal authentication.
//        /// </summary>
//        [XmlElement(SplConstants.E.AssignedEntity)]
//        public SplLegalAuthenticatorEntityDto AssignedEntity { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for legal authenticator assigned entity with signer information.
//    /// Contains details about the person and organization performing authentication.
//    /// </summary>
//    /// <seealso cref="Label.LegalAuthenticator"/>
//    public class SplLegalAuthenticatorEntityDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Assigned person element containing the signer's name.
//        /// Identifies the individual who legally authenticated the document.
//        /// </summary>
//        /// <seealso cref="Label.LegalAuthenticator.AssignedPersonName"/>
//        [XmlElement(SplConstants.E.AssignedPerson)]
//        public SplAssignedPersonDto AssignedPerson { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Represented organization for the signer when applicable.
//        /// Provides organizational context for the authentication.
//        /// </summary>
//        [XmlElement(SplConstants.E.RepresentedOrganization)]
//        public SplOrganizationDto? RepresentedOrganization { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL assigned person elements containing individual names.
//    /// Represents specific individuals in various SPL contexts.
//    /// </summary>
//    /// <seealso cref="Label.LegalAuthenticator"/>
//    public class SplAssignedPersonDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Person's name for identification purposes.
//        /// Contains the full name of the individual.
//        /// </summary>
//        /// <seealso cref="Label.LegalAuthenticator.AssignedPersonName"/>
//        [XmlElement(SplConstants.E.Name)]
//        public string Name { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL component elements containing structured body content.
//    /// Primary container for all document content organized in sections.
//    /// </summary>
//    /// <seealso cref="Label.StructuredBody"/>
//    public class SplComponentDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Structured body element containing all organized document content.
//        /// Houses sections, subsections, and various content types.
//        /// </summary>
//        /// <seealso cref="Label.StructuredBody"/>
//        [XmlElement(SplConstants.E.StructuredBody)]
//        public SplStructuredBodyDto StructuredBody { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL structured body elements containing section collections.
//    /// Organizes document content into logical sections and subsections.
//    /// </summary>
//    /// <seealso cref="Label.StructuredBody"/>
//    public class SplStructuredBodyDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Collection of component elements each containing a section.
//        /// Provides the primary organizational structure for document content.
//        /// </summary>
//        /// <seealso cref="Label.Section"/>
//        [XmlElement(SplConstants.E.Component)]
//        public List<SplSectionComponentDto> Components { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL section component elements wrapping individual sections.
//    /// Provides the component wrapper required by SPL structure.
//    /// </summary>
//    /// <seealso cref="Label.Section"/>
//    public class SplSectionComponentDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Section element containing content, metadata, and nested structures.
//        /// Primary organizational unit for SPL document content.
//        /// </summary>
//        /// <seealso cref="Label.Section"/>
//        [XmlElement(SplConstants.E.Section)]
//        public SplSectionDto Section { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL section elements with rich content and metadata support.
//    /// Contains text content, product data, and various specialized elements.
//    /// </summary>
//    /// <seealso cref="Label.Section"/>
//    public class SplSectionDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Optional section link identifier for cross-references within the document.
//        /// Used to create internal links and references between sections.
//        /// </summary>
//        /// <seealso cref="Label.Section.SectionLinkGUID"/>
//        [XmlAttribute(SplConstants.A.ID)]
//        public string? ID { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Unique section identifier for version control and referencing.
//        /// Provides stable identification across document versions.
//        /// </summary>
//        /// <seealso cref="Label.Section.SectionGUID"/>
//        [XmlElement(SplConstants.E.Id)]
//        public SplIdentifierDto Id { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Section type code identifying the kind of content contained.
//        /// Uses LOINC codes to categorize section purpose and content.
//        /// </summary>
//        /// <seealso cref="Label.Section.SectionCode"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto? Code { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Section title for display and navigation purposes.
//        /// Provides human-readable identification of section content.
//        /// </summary>
//        /// <seealso cref="Label.Section.Title"/>
//        [XmlElement(SplConstants.E.Title)]
//        public SplTitleDto? Title { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Text content element containing paragraphs, lists, tables, and media.
//        /// Houses the primary narrative and structured content of the section.
//        /// </summary>
//        /// <seealso cref="Label.SectionTextContent"/>
//        [XmlElement(SplConstants.E.Text)]
//        public SplTextDto? Text { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Effective time indicating when this section content is valid.
//        /// Supports both simple timestamps and complex date ranges.
//        /// </summary>
//        /// <seealso cref="Label.Section.EffectiveTime"/>
//        [XmlElement(SplConstants.E.EffectiveTime)]
//        public SplEffectiveTimeDto? EffectiveTime { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Collection of observation media elements for multimedia content.
//        /// Contains images, videos, and other media files referenced in the section.
//        /// </summary>
//        /// <seealso cref="Label.ObservationMedia"/>
//        [XmlElement(SplConstants.E.ObservationMedia)]
//        public List<SplObservationMediaDto> ObservationMedia { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of product subjects containing detailed product information.
//        /// Includes manufactured products, ingredients, and packaging details.
//        /// </summary>
//        /// <seealso cref="Label.Product"/>
//        [XmlElement(SplConstants.E.Subject)]
//        public List<SplSubjectDto> Subjects { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of nested section components for hierarchical organization.
//        /// Supports complex document structures with multiple content levels.
//        /// </summary>
//        /// <seealso cref="Label.SectionHierarchy"/>
//        [XmlElement(SplConstants.E.Component)]
//        public List<SplSectionComponentDto> Components { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL observation media elements containing multimedia references.
//    /// Handles images, videos, and other media content within document sections.
//    /// </summary>
//    /// <seealso cref="Label.ObservationMedia"/>
//    public class SplObservationMediaDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Media identifier for cross-references within the document.
//        /// Used to link media elements to references in text content.
//        /// </summary>
//        /// <seealso cref="Label.ObservationMedia.ObservationMediaID"/>
//        [XmlAttribute(SplConstants.A.ID)]
//        public string? ID { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Class name for the media element type classification.
//        /// Typically "ED" for encapsulated data elements.
//        /// </summary>
//        [XmlAttribute("classCode")]
//        public string ClassName { get; set; } = "ED";

//        /**************************************************************/
//        /// <summary>
//        /// Media type specification for the multimedia content.
//        /// MIME type indicating the format of the media file.
//        /// </summary>
//        /// <seealso cref="Label.ObservationMedia.MediaType"/>
//        [XmlAttribute(SplConstants.A.MediaType)]
//        public string MediaType { get; set; } = string.Empty;

//        /**************************************************************/
//        /// <summary>
//        /// Reference element containing the media file path or identifier.
//        /// Points to the actual multimedia file or resource.
//        /// </summary>
//        /// <seealso cref="Label.ObservationMedia.FileName"/>
//        [XmlElement(SplConstants.E.Reference)]
//        public SplReferenceDto Reference { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL reference elements pointing to external resources.
//    /// Used for multimedia files and other referenced content.
//    /// </summary>
//    /// <seealso cref="Label.ObservationMedia"/>
//    public class SplReferenceDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Reference value containing the file path or resource identifier.
//        /// Points to the location of the referenced multimedia or document.
//        /// </summary>
//        /// <seealso cref="Label.ObservationMedia.FileName"/>
//        [XmlAttribute(SplConstants.A.Value)]
//        public string Value { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL text elements containing rich narrative and structured content.
//    /// Supports paragraphs, lists, tables, and multimedia references.
//    /// </summary>
//    /// <seealso cref="Label.SectionTextContent"/>
//    public class SplTextDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Collection of paragraph elements containing narrative text content.
//        /// Primary mechanism for including textual information in sections.
//        /// </summary>
//        /// <seealso cref="Label.SectionTextContent"/>
//        [XmlElement(SplConstants.E.Paragraph)]
//        public List<SplParagraphDto> Paragraphs { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of list elements for organized information display.
//        /// Supports both ordered and unordered lists with various formatting.
//        /// </summary>
//        /// <seealso cref="Label.TextList"/>
//        [XmlElement(SplConstants.E.List)]
//        public List<SplListDto> Lists { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of table elements for tabular data presentation.
//        /// Provides structured display of complex information with headers and formatting.
//        /// </summary>
//        /// <seealso cref="Label.TextTable"/>
//        [XmlElement(SplConstants.E.Table)]
//        public List<SplTableDto> Tables { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of render multimedia elements for media references.
//        /// Provides inline references to images and other multimedia content.
//        /// </summary>
//        /// <seealso cref="Label.SectionTextContent"/>
//        [XmlElement(SplConstants.E.RenderMultimedia)]
//        public List<SplRenderMultiMediaDto> RenderMultiMedia { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of generic content elements for miscellaneous text.
//        /// Handles content types not covered by specific element types.
//        /// </summary>
//        /// <seealso cref="Label.SectionTextContent"/>
//        [XmlElement(SplConstants.E.AsContent)]
//        public List<SplContentDto> Content { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL render multimedia elements referencing media content.
//    /// Provides inline references to images and other multimedia within text.
//    /// </summary>
//    /// <seealso cref="Label.SectionTextContent"/>
//    public class SplRenderMultiMediaDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Referenced object identifier pointing to the multimedia element.
//        /// Links to observation media elements within the document.
//        /// </summary>
//        /// <seealso cref="Label.SectionTextContent.ReferencedObject"/>
//        [XmlAttribute("referencedObject")]
//        public string ReferencedObject { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL generic content elements with styling and text.
//    /// Handles miscellaneous content not covered by specific text element types.
//    /// </summary>
//    /// <seealso cref="Label.SectionTextContent"/>
//    public class SplContentDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Style code for content formatting and appearance.
//        /// Controls visual presentation aspects of the content element.
//        /// </summary>
//        /// <seealso cref="Label.SectionTextContent.StyleCode"/>
//        [XmlAttribute(SplConstants.A.StyleCode)]
//        public string? StyleCode { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Content text supporting rich formatting and references.
//        /// Contains the actual textual content for display.
//        /// </summary>
//        /// <seealso cref="Label.SectionTextContent.ContentText"/>
//        [XmlText]
//        public string Content { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL paragraph elements containing text and inline multimedia.
//    /// Supports rich text content with embedded images and formatting.
//    /// </summary>
//    /// <seealso cref="Label.SectionTextContent"/>
//    public class SplParagraphDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Style code for paragraph formatting and appearance.
//        /// Controls visual presentation aspects like emphasis and alignment.
//        /// </summary>
//        /// <seealso cref="Label.SectionTextContent.StyleCode"/>
//        [XmlAttribute(SplConstants.A.StyleCode)]
//        public string? StyleCode { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Paragraph text content as XML content for rich text support.
//        /// Allows mixed content including text and multimedia references.
//        /// </summary>
//        /// <seealso cref="Label.SectionTextContent.ContentText"/>
//        [XmlText]
//        public string Content { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL list elements with configurable formatting and items.
//    /// Supports both ordered and unordered lists with custom styling.
//    /// </summary>
//    /// <seealso cref="Label.TextList"/>
//    public class SplListDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// List type indicating ordered or unordered presentation.
//        /// Controls whether items are numbered or bulleted.
//        /// </summary>
//        /// <seealso cref="Label.TextList.ListType"/>
//        [XmlAttribute(SplConstants.A.ListType)]
//        public string? ListType { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Style code for custom list formatting and appearance.
//        /// Provides control over numbering schemes and bullet styles.
//        /// </summary>
//        /// <seealso cref="Label.TextList.StyleCode"/>
//        [XmlAttribute(SplConstants.A.StyleCode)]
//        public string? StyleCode { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Collection of list items containing the actual list content.
//        /// Each item represents a discrete piece of information in the list.
//        /// </summary>
//        /// <seealso cref="Label.TextListItem"/>
//        [XmlElement(SplConstants.E.Item)]
//        public List<SplListItemDto> Items { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL list item elements with optional captions and content.
//    /// Represents individual entries within ordered or unordered lists.
//    /// </summary>
//    /// <seealso cref="Label.TextListItem"/>
//    public class SplListItemDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Optional caption element for custom item markers or labels.
//        /// Allows override of default numbering or bullet formatting.
//        /// </summary>
//        /// <seealso cref="Label.TextListItem.ItemCaption"/>
//        [XmlElement(SplConstants.E.Caption)]
//        public string? Caption { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Item text content supporting rich formatting and references.
//        /// Contains the actual informational content of the list item.
//        /// </summary>
//        /// <seealso cref="Label.TextListItem.ItemText"/>
//        [XmlText]
//        public string Content { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL table elements with headers, body rows, and formatting.
//    /// Provides structured tabular presentation of complex data.
//    /// </summary>
//    /// <seealso cref="Label.TextTable"/>
//    public class SplTableDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Table width specification for layout control.
//        /// Defines the display width of the table element.
//        /// </summary>
//        /// <seealso cref="Label.TextTable.Width"/>
//        [XmlAttribute(SplConstants.A.Width)]
//        public string? Width { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Table summary text for accessibility and description.
//        /// Provides a brief description of the table content and purpose.
//        /// </summary>
//        /// <seealso cref="Label.TextTable.Summary"/>
//        [XmlAttribute("summary")]
//        public string? Summary { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Table border specification for visual presentation.
//        /// Controls the appearance of table borders and grid lines.
//        /// </summary>
//        /// <seealso cref="Label.TextTable.Border"/>
//        [XmlAttribute("border")]
//        public string? Border { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Optional table header section containing column headers.
//        /// Provides labeled columns for data interpretation.
//        /// </summary>
//        /// <seealso cref="Label.TextTableRow"/>
//        [XmlElement(SplConstants.E.Thead)]
//        public SplTableSectionDto? THead { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Table body section containing the primary data rows.
//        /// Houses the main content and information of the table.
//        /// </summary>
//        /// <seealso cref="Label.TextTableRow"/>
//        [XmlElement(SplConstants.E.Tbody)]
//        public SplTableSectionDto TBody { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Optional table footer section for summary or additional information.
//        /// Provides concluding data or totals for table content.
//        /// </summary>
//        /// <seealso cref="Label.TextTableRow"/>
//        [XmlElement(SplConstants.E.Tfoot)]
//        public SplTableSectionDto? TFoot { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL table section elements containing rows of cells.
//    /// Represents header, body, or footer sections within tables.
//    /// </summary>
//    /// <seealso cref="Label.TextTableRow"/>
//    public class SplTableSectionDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Collection of table rows containing cells with data or headers.
//        /// Organizes tabular content into logical rows for presentation.
//        /// </summary>
//        /// <seealso cref="Label.TextTableRow"/>
//        [XmlElement(SplConstants.E.Tr)]
//        public List<SplTableRowDto> Rows { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL table row elements containing cells and formatting.
//    /// Represents horizontal rows of data within table structures.
//    /// </summary>
//    /// <seealso cref="Label.TextTableRow"/>
//    public class SplTableRowDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Style code for row-level formatting and appearance.
//        /// Controls visual aspects like borders and highlighting.
//        /// </summary>
//        /// <seealso cref="Label.TextTableRow.StyleCode"/>
//        [XmlAttribute(SplConstants.A.StyleCode)]
//        public string? StyleCode { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Row identifier for cross-references and linking.
//        /// Used to reference specific table rows from other parts of the document.
//        /// </summary>
//        /// <seealso cref="Label.TextTableRow.RowID"/>
//        [XmlAttribute(SplConstants.A.ID)]
//        public string? ID { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Collection of table cells containing data or header information.
//        /// Provides the individual data points within the table row.
//        /// </summary>
//        /// <seealso cref="Label.TextTableCell"/>
//        [XmlElement(SplConstants.E.Td)]
//        public List<SplTableCellDto> Cells { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL table cell elements with content and spanning support.
//    /// Represents individual data points within table structures.
//    /// </summary>
//    /// <seealso cref="Label.TextTableCell"/>
//    public class SplTableCellDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Style code for cell-level formatting including borders.
//        /// Controls visual presentation of individual table cells.
//        /// </summary>
//        /// <seealso cref="Label.TextTableCell.StyleCode"/>
//        [XmlAttribute(SplConstants.A.StyleCode)]
//        public string? StyleCode { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Horizontal alignment specification for cell content.
//        /// Controls text positioning within the cell boundaries.
//        /// </summary>
//        /// <seealso cref="Label.TextTableCell.Align"/>
//        [XmlAttribute(SplConstants.A.Align)]
//        public string? Align { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Vertical alignment specification for cell content.
//        /// Controls vertical positioning of content within cells.
//        /// </summary>
//        /// <seealso cref="Label.TextTableCell.VAlign"/>
//        [XmlAttribute(SplConstants.A.VAlign)]
//        public string? VAlign { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Cell identifier for cross-references and linking.
//        /// Used to reference specific table cells from other parts of the document.
//        /// </summary>
//        /// <seealso cref="Label.TextTableCell.CellID"/>
//        [XmlAttribute(SplConstants.A.ID)]
//        public string? ID { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Row span value for cells spanning multiple table rows.
//        /// Enables complex table layouts with merged cells.
//        /// </summary>
//        /// <seealso cref="Label.TextTableCell.RowSpan"/>
//        [XmlIgnore]
//        public int? RowSpan { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Column span value for cells spanning multiple table columns.
//        /// Supports horizontal cell merging for layout flexibility.
//        /// </summary>
//        /// <seealso cref="Label.TextTableCell.ColSpan"/>
//        [XmlIgnore]
//        public int? ColSpan { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// XML serialization backing property for RowSpan.
//        /// Only serializes when RowSpan has a value greater than 1.
//        /// </summary>
//        [XmlAttribute(SplConstants.A.Rowspan)]
//        public string RowSpanXml
//        {
//            get => RowSpan.HasValue && RowSpan.Value > 1 ? RowSpan.Value.ToString() : null!;
//            set
//            {
//                if (string.IsNullOrEmpty(value))
//                {
//                    RowSpan = null;
//                }
//                else if (int.TryParse(value, out int result))
//                {
//                    RowSpan = result;
//                }
//                else
//                {
//                    RowSpan = null; // or throw an exception, depending on your preference
//                }
//            }
//        }

//        /**************************************************************/
//        /// <summary>
//        /// XML serialization backing property for ColSpan.
//        /// Only serializes when ColSpan has a value greater than 1.
//        /// </summary>
//        [XmlAttribute(SplConstants.A.Colspan)]
//        public string ColSpanXml
//        {
//            get => ColSpan.HasValue && ColSpan.Value > 1 ? ColSpan.Value.ToString() : null!;
//            set => ColSpan = string.IsNullOrEmpty(value) ? null : int.Parse(value);
//        }

//        /**************************************************************/
//        /// <summary>
//        /// Indicates whether RowSpanXml should be serialized.
//        /// Prevents empty attributes from appearing in the XML.
//        /// </summary>
//        public bool ShouldSerializeRowSpanXml() => RowSpan.HasValue && RowSpan.Value > 1;

//        /**************************************************************/
//        /// <summary>
//        /// Indicates whether ColSpanXml should be serialized.
//        /// Prevents empty attributes from appearing in the XML.
//        /// </summary>
//        public bool ShouldSerializeColSpanXml() => ColSpan.HasValue && ColSpan.Value > 1;

//        /**************************************************************/
//        /// <summary>
//        /// Cell content supporting rich text and multimedia references.
//        /// Contains the actual data or information displayed in the cell.
//        /// </summary>
//        /// <seealso cref="Label.TextTableCell.CellText"/>
//        [XmlText]
//        public string Content { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL subject elements containing product and substance information.
//    /// Primary container for manufactured products and identified substances.
//    /// </summary>
//    /// <seealso cref="Label.Product"/>
//    /// <seealso cref="Label.IdentifiedSubstance"/>
//    public class SplSubjectDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Manufactured product element containing detailed product information.
//        /// Houses product identification, ingredients, and packaging details.
//        /// </summary>
//        /// <seealso cref="Label.Product"/>
//        [XmlElement(SplConstants.E.ManufacturedProduct)]
//        public SplManufacturedProductDto? ManufacturedProduct { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Identified substance element for indexing and classification purposes.
//        /// Used in substance indexing documents for active moieties and classes.
//        /// </summary>
//        /// <seealso cref="Label.IdentifiedSubstance"/>
//        [XmlElement(SplConstants.E.IdentifiedSubstance)]
//        public SplIdentifiedSubstanceDto? IdentifiedSubstance { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL manufactured product elements with comprehensive product data.
//    /// Contains product identification, ingredients, packaging, and regulatory information.
//    /// </summary>
//    /// <seealso cref="Label.Product"/>
//    public class SplManufacturedProductDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Inner manufactured product element containing core product details.
//        /// Houses the essential product information and specifications.
//        /// </summary>
//        /// <seealso cref="Label.Product"/>
//        [XmlElement(SplConstants.E.ManufacturedMedicine)]
//        public SplProductDto ManufacturedProduct { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of subject-of elements containing regulatory and classification data.
//        /// Includes marketing categories, statuses, and regulatory approvals.
//        /// </summary>
//        /// <seealso cref="Label.MarketingCategory"/>
//        /// <seealso cref="Label.MarketingStatus"/>
//        [XmlElement(SplConstants.E.SubjectOf)]
//        public List<SplProductSubjectOfDto> SubjectOf { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of consumed-in elements for route of administration.
//        /// Specifies how the product is administered to patients.
//        /// </summary>
//        /// <seealso cref="Label.Route"/>
//        [XmlElement(SplConstants.E.ConsumedIn)]
//        public List<SplConsumedInDto> ConsumedIn { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL consumed-in elements describing substance administration.
//    /// Represents routes of administration for pharmaceutical products.
//    /// </summary>
//    /// <seealso cref="Label.Route"/>
//    public class SplConsumedInDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Substance administration element containing route information.
//        /// Specifies the method by which the product is administered.
//        /// </summary>
//        /// <seealso cref="Label.Route"/>
//        [XmlElement(SplConstants.E.SubstanceAdministration)]
//        public SplSubstanceAdministrationDto SubstanceAdministration { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL substance administration elements with route codes.
//    /// Contains administration method information for pharmaceutical products.
//    /// </summary>
//    /// <seealso cref="Label.Route"/>
//    public class SplSubstanceAdministrationDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Route code identifying the method of administration.
//        /// Uses FDA-defined codes for routes like oral, topical, injection.
//        /// </summary>
//        /// <seealso cref="Label.Route.RouteCode"/>
//        [XmlElement(SplConstants.E.RouteCode)]
//        public SplCodeDto RouteCode { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL product elements containing identification and composition data.
//    /// Core product information including codes, names, forms, and ingredients.
//    /// </summary>
//    /// <seealso cref="Label.Product"/>
//    public class SplProductDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Collection of product identification codes such as NDC product codes.
//        /// Provides machine-readable identification for the product.
//        /// </summary>
//        /// <seealso cref="Label.ProductIdentifier"/>
//        [XmlElement(SplConstants.E.Code)]
//        public List<SplCodeDto> Codes { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Product name for identification and display purposes.
//        /// Contains the proprietary or trade name of the product.
//        /// </summary>
//        /// <seealso cref="Label.Product.ProductName"/>
//        [XmlElement(SplConstants.E.Name)]
//        public string? Name { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Product name suffix providing additional identification context.
//        /// Contains modifiers like "XR" for extended-release formulations.
//        /// </summary>
//        /// <seealso cref="Label.Product.ProductSuffix"/>
//        [XmlElement(SplConstants.E.Suffix)]
//        public string? Suffix { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Product description providing additional context and information.
//        /// Contains supplementary details about the product, mainly for devices.
//        /// </summary>
//        /// <seealso cref="Label.Product.DescriptionText"/>
//        [XmlElement(SplConstants.E.Desc)]
//        public string? Description { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Dosage form code identifying the physical form of the product.
//        /// Specifies how the product is formulated for administration.
//        /// </summary>
//        /// <seealso cref="Label.Product.FormCode"/>
//        [XmlElement(SplConstants.E.FormCode)]
//        public SplCodeDto? FormCode { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Collection of generic medicine entities for non-proprietary names.
//        /// Provides established names and phonetic pronunciations.
//        /// </summary>
//        /// <seealso cref="Label.GenericMedicine"/>
//        [XmlElement(SplConstants.E.AsEntityWithGeneric)]
//        public List<SplGenericMedicineDto> AsEntityWithGeneric { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of specialized kind classifications for devices and cosmetics.
//        /// Provides product categorization for regulatory classification.
//        /// </summary>
//        /// <seealso cref="Label.SpecializedKind"/>
//        [XmlElement(SplConstants.E.AsSpecializedKind)]
//        public List<SplSpecializedKindDto> AsSpecializedKind { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of ingredient elements describing product composition.
//        /// Includes active ingredients, inactive ingredients, and other components.
//        /// </summary>
//        /// <seealso cref="Label.Ingredient"/>
//        [XmlElement(SplConstants.E.Ingredient)]
//        public List<SplIngredientDto> Ingredients { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of packaging level elements describing product containers.
//        /// Defines the hierarchical packaging structure and identifiers.
//        /// </summary>
//        /// <seealso cref="Label.PackagingLevel"/>
//        [XmlElement(SplConstants.E.AsContent)]
//        public List<SplAsContentDto> AsContent { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of active ingredient elements with structured composition.
//        /// Alternative representation for active pharmaceutical ingredients.
//        /// </summary>
//        /// <seealso cref="Label.Ingredient"/>
//        [XmlElement("activeIngredient")]
//        public List<SplActiveIngredientDto> ActiveIngredients { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of inactive ingredient elements with structured composition.
//        /// Alternative representation for inactive pharmaceutical ingredients.
//        /// </summary>
//        /// <seealso cref="Label.Ingredient"/>
//        [XmlElement("inactiveIngredient")]
//        public List<SplInactiveIngredientDto> InactiveIngredients { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL generic medicine entities providing non-proprietary names.
//    /// Contains established names and phonetic pronunciation information.
//    /// </summary>
//    /// <seealso cref="Label.GenericMedicine"/>
//    public class SplGenericMedicineDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Generic medicine element containing the non-proprietary name information.
//        /// Provides the established name without trademark considerations.
//        /// </summary>
//        /// <seealso cref="Label.GenericMedicine"/>
//        [XmlElement(SplConstants.E.GenericMedicine)]
//        public SplGenericMedicineContentDto GenericMedicine { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for generic medicine content with names and pronunciations.
//    /// Contains the actual generic name information and optional phonetics.
//    /// </summary>
//    /// <seealso cref="Label.GenericMedicine"/>
//    public class SplGenericMedicineContentDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Collection of name elements including standard and phonetic forms.
//        /// Provides multiple representations of the generic medicine name.
//        /// </summary>
//        /// <seealso cref="Label.GenericMedicine.GenericName"/>
//        /// <seealso cref="Label.GenericMedicine.PhoneticName"/>
//        [XmlElement(SplConstants.E.Name)]
//        public List<SplGenericNameDto> Names { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for generic medicine name elements with usage indicators.
//    /// Represents different forms of generic names including phonetic versions.
//    /// </summary>
//    /// <seealso cref="Label.GenericMedicine"/>
//    public class SplGenericNameDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Usage indicator for the name type (standard name vs. phonetic).
//        /// Distinguishes between regular names and pronunciation guides.
//        /// </summary>
//        /// <seealso cref="Label.GenericMedicine.PhoneticName"/>
//        [XmlAttribute(SplConstants.A.Use)]
//        public string? Use { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// The actual name content, either standard or phonetic spelling.
//        /// Contains the text representation of the generic medicine name.
//        /// </summary>
//        /// <seealso cref="Label.GenericMedicine.GenericName"/>
//        [XmlText]
//        public string Content { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL specialized kind elements providing product classifications.
//    /// Used for device product classes and cosmetic categories.
//    /// </summary>
//    /// <seealso cref="Label.SpecializedKind"/>
//    public class SplSpecializedKindDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Class code indicating the classification relationship type.
//        /// Typically "GEN" for generalization relationships.
//        /// </summary>
//        [XmlAttribute(SplConstants.A.ClassCode)]
//        public string? ClassCode { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Generalized material kind element containing the classification code.
//        /// Provides the specific category or class for the product.
//        /// </summary>
//        /// <seealso cref="Label.SpecializedKind.KindCode"/>
//        [XmlElement(SplConstants.E.GeneralizedMaterialKind)]
//        public SplCodeDto GeneralizedMaterialKind { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL ingredient elements describing product components.
//    /// Supports active ingredients, inactive ingredients, and other components.
//    /// </summary>
//    /// <seealso cref="Label.Ingredient"/>
//    public class SplIngredientDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Class code indicating the ingredient type and role.
//        /// Common values include ACTIM, ACTIB, ACTIR, IACT, INGR, COLR, CNTM.
//        /// </summary>
//        /// <seealso cref="Label.Ingredient.ClassCode"/>
//        [XmlAttribute(SplConstants.A.ClassCode)]
//        public string ClassCode { get; set; } = string.Empty;

//        /**************************************************************/
//        /// <summary>
//        /// Sequence number for ordering ingredients within the product.
//        /// Determines the display order of ingredients in the final document.
//        /// </summary>
//        /// <seealso cref="Label.Ingredient.SequenceNumber"/>
//        [XmlIgnore]
//        public int? SequenceNumber { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Indicates whether ingredient information is confidential.
//        /// Used when ingredient details need to be marked as protected.
//        /// </summary>
//        /// <seealso cref="Label.Ingredient.IsConfidential"/>
//        [XmlIgnore]
//        public bool IsConfidential { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Quantity element describing the ingredient strength or amount.
//        /// Contains numerator and denominator for precise strength specification.
//        /// </summary>
//        /// <seealso cref="Label.Ingredient.QuantityNumerator"/>
//        [XmlElement(SplConstants.E.Quantity)]
//        public SplQuantityDto? Quantity { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Ingredient substance element containing identification and names.
//        /// Provides the chemical identity and naming information.
//        /// </summary>
//        /// <seealso cref="Label.IngredientSubstance"/>
//        [XmlElement(SplConstants.E.IngredientSubstance)]
//        public SplIngredientSubstanceDto IngredientSubstance { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of specified substance elements for additional substance information.
//        /// Provides supplementary substance codes and identifiers.
//        /// </summary>
//        /// <seealso cref="Label.IngredientSubstance"/>
//        [XmlElement(SplConstants.E.SubstanceSpecification)]
//        public List<SplSpecifiedSubstanceDto> SpecifiedSubstances { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL specified substance elements with additional substance codes.
//    /// Provides supplementary substance identification information for ingredients.
//    /// </summary>
//    /// <seealso cref="Label.IngredientSubstance"/>
//    public class SplSpecifiedSubstanceDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Substance identification code providing additional substance reference.
//        /// Supplements the main ingredient substance with alternative identifiers.
//        /// </summary>
//        /// <seealso cref="Label.IngredientSubstance.UNII"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto Code { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL quantity elements with numerator and denominator values.
//    /// Provides precise specification of ingredient strengths and amounts.
//    /// </summary>
//    /// <seealso cref="Label.Ingredient"/>
//    public class SplQuantityDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Numerator element containing the primary quantity value and unit.
//        /// Specifies the amount of ingredient in the given formulation.
//        /// </summary>
//        /// <seealso cref="Label.Ingredient.QuantityNumerator"/>
//        [XmlElement(SplConstants.E.Numerator)]
//        public SplQuantityValueDto Numerator { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Optional denominator element for ratio-based strength expressions.
//        /// Provides the basis for calculating relative ingredient concentrations.
//        /// </summary>
//        /// <seealso cref="Label.Ingredient.QuantityDenominator"/>
//        [XmlElement(SplConstants.E.Denominator)]
//        public SplQuantityValueDto? Denominator { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for quantity value elements with numeric values and units.
//    /// Represents individual components of ingredient quantity specifications.
//    /// </summary>
//    /// <seealso cref="Label.Ingredient"/>
//    public class SplQuantityValueDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Numeric value for the quantity component.
//        /// Contains the actual measurement amount as a string for XML serialization.
//        /// </summary>
//        /// <seealso cref="Label.Ingredient.QuantityNumerator"/>
//        [XmlAttribute(SplConstants.A.Value)]
//        public string Value { get; set; } = string.Empty;

//        /**************************************************************/
//        /// <summary>
//        /// Unit of measure for the quantity value.
//        /// Specifies the measurement unit using standard abbreviations.
//        /// </summary>
//        /// <seealso cref="Label.Ingredient.QuantityNumeratorUnit"/>
//        [XmlAttribute(SplConstants.A.Unit)]
//        public string Unit { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL ingredient substance elements with identification codes.
//    /// Contains substance identity information including UNII codes and names.
//    /// </summary>
//    /// <seealso cref="Label.IngredientSubstance"/>
//    public class SplIngredientSubstanceDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Substance identification code, typically UNII for FDA registration.
//        /// Provides authoritative identification of the chemical substance.
//        /// </summary>
//        /// <seealso cref="Label.IngredientSubstance.UNII"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto? Code { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Substance name for identification and display purposes.
//        /// Contains the established or preferred name of the substance.
//        /// </summary>
//        /// <seealso cref="Label.IngredientSubstance.SubstanceName"/>
//        [XmlElement(SplConstants.E.Name)]
//        public string Name { get; set; } = string.Empty;

//        /**************************************************************/
//        /// <summary>
//        /// Collection of active moiety elements for substance classification.
//        /// Identifies the pharmacologically active portions of the substance.
//        /// </summary>
//        /// <seealso cref="Label.ActiveMoiety"/>
//        [XmlElement(SplConstants.E.ActiveMoiety)]
//        public List<SplActiveMoietyDto> ActiveMoieties { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL active moiety elements identifying pharmacologically active components.
//    /// Represents the therapeutically active portions of pharmaceutical substances.
//    /// </summary>
//    /// <seealso cref="Label.ActiveMoiety"/>
//    public class SplActiveMoietyDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Active moiety element containing identification and naming information.
//        /// Provides details about the pharmacologically active component.
//        /// </summary>
//        /// <seealso cref="Label.ActiveMoiety"/>
//        [XmlElement(SplConstants.E.ActiveMoiety)]
//        public SplActiveMoietyContentDto ActiveMoiety { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for active moiety content with identification codes and names.
//    /// Contains the specific identity information for active moieties.
//    /// </summary>
//    /// <seealso cref="Label.ActiveMoiety"/>
//    public class SplActiveMoietyContentDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Active moiety identification code, typically UNII.
//        /// Provides authoritative identification of the active component.
//        /// </summary>
//        /// <seealso cref="Label.ActiveMoiety.MoietyUNII"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto Code { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Active moiety name for identification and display purposes.
//        /// Contains the established name of the active moiety.
//        /// </summary>
//        /// <seealso cref="Label.ActiveMoiety.MoietyName"/>
//        [XmlElement(SplConstants.E.Name)]
//        public string Name { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL as-content elements describing packaging hierarchies.
//    /// Represents container packaging levels and their relationships.
//    /// </summary>
//    /// <seealso cref="Label.PackagingLevel"/>
//    public class SplAsContentDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Quantity element describing how many items are contained.
//        /// Specifies the number of products or containers at this level.
//        /// </summary>
//        /// <seealso cref="Label.PackagingLevel.QuantityNumerator"/>
//        [XmlElement(SplConstants.E.Quantity)]
//        public SplQuantityDto Quantity { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Container packaged product element containing packaging details.
//        /// Describes the specific container and its identification codes.
//        /// </summary>
//        /// <seealso cref="Label.PackagingLevel"/>
//        [XmlElement(SplConstants.E.ContainerPackagedProduct)]
//        public SplContainerPackagedProductDto ContainerPackagedProduct { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL container packaged product elements with packaging information.
//    /// Contains container identification, form, and regulatory data.
//    /// </summary>
//    /// <seealso cref="Label.PackagingLevel"/>
//    public class SplContainerPackagedProductDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Container identification code such as NDC package code.
//        /// Provides unique identification for the specific package configuration.
//        /// </summary>
//        /// <seealso cref="Label.PackageIdentifier"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto? Code { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Container form code describing the package type.
//        /// Identifies the physical nature of the packaging container.
//        /// </summary>
//        /// <seealso cref="Label.PackagingLevel.PackageFormCode"/>
//        [XmlElement(SplConstants.E.FormCode)]
//        public SplCodeDto FormCode { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of subject-of elements containing package-specific regulatory data.
//        /// Includes marketing statuses and package-level characteristics.
//        /// </summary>
//        /// <seealso cref="Label.MarketingStatus"/>
//        [XmlElement(SplConstants.E.SubjectOf)]
//        public List<SplPackageSubjectOfDto> SubjectOf { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for package-level subject-of elements containing marketing and regulatory data.
//    /// Represents information specifically associated with packaging levels.
//    /// </summary>
//    /// <seealso cref="Label.MarketingStatus"/>
//    public class SplPackageSubjectOfDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Marketing act element containing status and timing information.
//        /// Describes the marketing status specific to this package configuration.
//        /// </summary>
//        /// <seealso cref="Label.MarketingStatus"/>
//        [XmlElement(SplConstants.E.MarketingAct)]
//        public SplMarketingActDto? MarketingAct { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for product-level subject-of elements containing regulatory information.
//    /// Houses marketing categories, statuses, and other product classifications.
//    /// </summary>
//    /// <seealso cref="Label.MarketingCategory"/>
//    /// <seealso cref="Label.MarketingStatus"/>
//    public class SplProductSubjectOfDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Marketing act element containing marketing status information.
//        /// Describes the current marketing status of the product.
//        /// </summary>
//        /// <seealso cref="Label.MarketingStatus"/>
//        [XmlElement(SplConstants.E.MarketingAct)]
//        public SplMarketingActDto? MarketingAct { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Approval element containing marketing category information.
//        /// Describes the regulatory pathway and application details.
//        /// </summary>
//        /// <seealso cref="Label.MarketingCategory"/>
//        [XmlElement(SplConstants.E.Approval)]
//        public SplMarketingApprovalDto? Approval { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Characteristic element containing product physical attributes.
//        /// Describes characteristics like color, shape, size, imprint, etc.
//        /// </summary>
//        /// <seealso cref="Label.ProductCharacteristic"/>
//        [XmlElement(SplConstants.E.Characteristic)]
//        public SplCharacteristicDto? Characteristic { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL characteristic elements describing product physical attributes.
//    /// Represents product characteristics such as color, shape, size, and markings.
//    /// </summary>
//    /// <seealso cref="Label.ProductCharacteristic"/>
//    public class SplCharacteristicDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Characteristic type code identifying the attribute being described.
//        /// Uses standardized codes for characteristics like color, shape, size.
//        /// </summary>
//        /// <seealso cref="Label.ProductCharacteristic.CharacteristicType"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto Code { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Characteristic value element containing the actual attribute data.
//        /// Can represent coded values, numeric measurements, or text descriptions.
//        /// </summary>
//        /// <seealso cref="Label.ProductCharacteristic.CharacteristicValue"/>
//        [XmlElement(SplConstants.E.Value)]
//        public SplCharacteristicValueDto Value { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL characteristic value elements with typed content.
//    /// Supports various data types for characteristic values including codes and measurements.
//    /// </summary>
//    /// <seealso cref="Label.ProductCharacteristic"/>
//    public class SplCharacteristicValueDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Coded value for coded characteristics like color or shape.
//        /// Contains the standardized code representing the characteristic value.
//        /// </summary>
//        /// <seealso cref="Label.ProductCharacteristic.CharacteristicValueCode"/>
//        [XmlAttribute(SplConstants.A.CodeValue)]
//        public string? Code { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Code system for coded characteristic values.
//        /// Identifies the vocabulary used for the characteristic code.
//        /// </summary>
//        /// <seealso cref="Label.ProductCharacteristic.CharacteristicValueCodeSystem"/>
//        [XmlAttribute(SplConstants.A.CodeSystem)]
//        public string? CodeSystem { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Display name for coded characteristic values.
//        /// Provides human-readable description of the coded value.
//        /// </summary>
//        /// <seealso cref="Label.ProductCharacteristic.CharacteristicValue"/>
//        [XmlAttribute(SplConstants.A.DisplayName)]
//        public string? DisplayName { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Numeric or text value for characteristics with measurements or descriptions.
//        /// Contains the actual measurement or descriptive text for the characteristic.
//        /// </summary>
//        /// <seealso cref="Label.ProductCharacteristic.CharacteristicValue"/>
//        [XmlAttribute(SplConstants.A.Value)]
//        public string? Value { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Unit of measure for numeric characteristic values.
//        /// Specifies the measurement unit for quantitative characteristics.
//        /// </summary>
//        /// <seealso cref="Label.ProductCharacteristic.CharacteristicUnit"/>
//        [XmlAttribute(SplConstants.A.Unit)]
//        public string? Unit { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// XML Schema instance type for the characteristic value.
//        /// Indicates the data type: CE (coded), PQ (physical quantity), ST (string), INT (integer).
//        /// </summary>
//        [XmlAttribute("type", Namespace = "http://www.w3.org/2001/XMLSchema-instance")]
//        public string? XsiType { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL marketing act elements containing status and timing data.
//    /// Represents marketing activities and their current status.
//    /// </summary>
//    /// <seealso cref="Label.MarketingStatus"/>
//    public class SplMarketingActDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Marketing activity code identifying the type of marketing.
//        /// Distinguishes between regular marketing, samples, and other activities.
//        /// </summary>
//        /// <seealso cref="Label.MarketingStatus.MarketingActCode"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto Code { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Status code indicating the current state of marketing activity.
//        /// Common values include active, completed, new, cancelled.
//        /// </summary>
//        /// <seealso cref="Label.MarketingStatus.StatusCode"/>
//        [XmlElement(SplConstants.E.StatusCode)]
//        public SplStatusCodeDto StatusCode { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Effective time range for the marketing activity.
//        /// Indicates when marketing began and potentially when it ended.
//        /// </summary>
//        /// <seealso cref="Label.MarketingStatus.EffectiveStartDate"/>
//        [XmlElement(SplConstants.E.EffectiveTime)]
//        public SplEffectiveTimeDto EffectiveTime { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL status code elements with coded status values.
//    /// Represents various status conditions throughout SPL documents.
//    /// </summary>
//    /// <seealso cref="Label.MarketingStatus"/>
//    public class SplStatusCodeDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Status code value indicating the current state.
//        /// Provides machine-readable status information.
//        /// </summary>
//        /// <seealso cref="Label.MarketingStatus.StatusCode"/>
//        [XmlAttribute(SplConstants.A.CodeValue)]
//        public string Code { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL marketing approval elements containing application information.
//    /// Represents marketing categories and regulatory pathway details.
//    /// </summary>
//    /// <seealso cref="Label.MarketingCategory"/>
//    public class SplMarketingApprovalDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Application or monograph identifier for the marketing category.
//        /// Contains the regulatory submission number or reference.
//        /// </summary>
//        /// <seealso cref="Label.MarketingCategory.ApplicationOrMonographIDValue"/>
//        [XmlElement(SplConstants.E.Id)]
//        public SplIdentifierDto Id { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Marketing category code identifying the regulatory pathway.
//        /// Specifies whether the product follows NDA, ANDA, OTC, or other pathways.
//        /// </summary>
//        /// <seealso cref="Label.MarketingCategory.CategoryCode"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto Code { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Effective time for the approval, typically the approval date.
//        /// Indicates when the regulatory approval became effective.
//        /// </summary>
//        /// <seealso cref="Label.MarketingCategory.ApprovalDate"/>
//        [XmlElement(SplConstants.E.EffectiveTime)]
//        public SplEffectiveTimeDto? EffectiveTime { get; set; }

//        /**************************************************************/
//        /// <summary>
//        /// Author element containing territorial authority information.
//        /// Identifies the regulatory agency granting the approval.
//        /// </summary>
//        [XmlElement(SplConstants.E.Author)]
//        public SplMarketingAuthorDto Author { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for marketing approval author elements with territorial authority.
//    /// Identifies the regulatory jurisdiction for marketing approvals.
//    /// </summary>
//    /// <seealso cref="Label.MarketingCategory"/>
//    public class SplMarketingAuthorDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Territorial authority element identifying the regulatory jurisdiction.
//        /// Specifies which country or region granted the marketing approval.
//        /// </summary>
//        /// <seealso cref="Label.MarketingCategory.TerritoryCode"/>
//        [XmlElement(SplConstants.E.TerritorialAuthority)]
//        public SplTerritorialAuthorityDto TerritorialAuthority { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL territorial authority elements with territory identification.
//    /// Represents regulatory jurisdictions and governing agencies.
//    /// </summary>
//    /// <seealso cref="Label.TerritorialAuthority"/>
//    public class SplTerritorialAuthorityDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Territory element containing the jurisdiction code.
//        /// Identifies the country or region with regulatory authority.
//        /// </summary>
//        /// <seealso cref="Label.TerritorialAuthority.TerritoryCode"/>
//        [XmlElement(SplConstants.E.Territory)]
//        public SplTerritoryDto Territory { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Optional governing agency element for federal authorities.
//        /// Specifies the specific agency within the territorial jurisdiction.
//        /// </summary>
//        /// <seealso cref="Label.TerritorialAuthority.GoverningAgencyName"/>
//        [XmlElement(SplConstants.E.GoverningAgency)]
//        public SplGoverningAgencyDto? GoverningAgency { get; set; }

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL territory elements with jurisdiction codes.
//    /// Represents specific countries or regulatory regions.
//    /// </summary>
//    /// <seealso cref="Label.TerritorialAuthority"/>
//    public class SplTerritoryDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Territory code identifying the regulatory jurisdiction.
//        /// Uses ISO country codes or other standard jurisdiction identifiers.
//        /// </summary>
//        /// <seealso cref="Label.TerritorialAuthority.TerritoryCode"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto Code { get; set; } = new();

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL governing agency elements with agency identification.
//    /// Represents specific regulatory agencies within territorial authorities.
//    /// </summary>
//    /// <seealso cref="Label.TerritorialAuthority"/>
//    public class SplGoverningAgencyDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Agency identifier such as DUNS number for federal agencies.
//        /// Provides unique identification of the governing regulatory body.
//        /// </summary>
//        /// <seealso cref="Label.TerritorialAuthority.GoverningAgencyIdExtension"/>
//        [XmlElement(SplConstants.E.Id)]
//        public SplIdentifierDto Id { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Agency name for identification and display purposes.
//        /// Contains the official name of the regulatory agency.
//        /// </summary>
//        /// <seealso cref="Label.TerritorialAuthority.GoverningAgencyName"/>
//        [XmlElement(SplConstants.E.Name)]
//        public string Name { get; set; } = string.Empty;

//        #endregion
//    }

//    /**************************************************************/
//    /// <summary>
//    /// DTO for SPL identified substance elements for indexing purposes.
//    /// Used in substance indexing documents for active moieties and classifications.
//    /// </summary>
//    /// <seealso cref="Label.IdentifiedSubstance"/>
//    public class SplIdentifiedSubstanceDto
//    {
//        #region implementation

//        /**************************************************************/
//        /// <summary>
//        /// Substance identification code, typically UNII or classification code.
//        /// Provides authoritative identification of the substance or class.
//        /// </summary>
//        /// <seealso cref="Label.IdentifiedSubstance.SubstanceIdentifierValue"/>
//        [XmlElement(SplConstants.E.Code)]
//        public SplCodeDto Code { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of name elements for substance identification.
//        /// Includes standard names and potentially alternate names or uses.
//        /// </summary>
//        [XmlElement(SplConstants.E.Name)]
//        public List<SplGenericNameDto> Names { get; set; } = new();

//        /**************************************************************/
//        /// <summary>
//        /// Collection of specialized kind relationships for substance classification.
//        /// Links substances to their pharmacologic classes or categories.
//        /// </summary>
//        /// <seealso cref="Label.PharmacologicClassLink"/>
//        [XmlElement(SplConstants.E.AsSpecializedKind)]
//        public List<SplSpecializedKindDto> AsSpecializedKind { get; set; } = new();

//        #endregion
//    }

//}