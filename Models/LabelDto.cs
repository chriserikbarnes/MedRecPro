
using MedRecPro.Helpers;
using MedRecPro.Service;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <seealso cref="Label.AdditionalIdentifier"/>
    public class AdditionalIdentifierDto
    {
        public required Dictionary<string, object?> AdditionalIdentifier { get; set; }

        /// <summary>
        /// Primary key for the AdditionalIdentifier table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? AdditionalIdentifierID =>
            AdditionalIdentifier.TryGetValue("EncryptedAdditionalIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            AdditionalIdentifier.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code for the type of identifier (e.g., C99286 Model Number, C99285 Catalog Number, C99287 Reference Number).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierTypeCode =>
            AdditionalIdentifier.TryGetValue(nameof(IdentifierTypeCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for IdentifierTypeCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierTypeCodeSystem =>
            AdditionalIdentifier.TryGetValue(nameof(IdentifierTypeCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for IdentifierTypeCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierTypeDisplayName =>
            AdditionalIdentifier.TryGetValue(nameof(IdentifierTypeDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The actual identifier value ([id extension]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierValue =>
            AdditionalIdentifier.TryGetValue(nameof(IdentifierValue), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The root OID associated with the identifier ([id root]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierRootOID =>
            AdditionalIdentifier.TryGetValue(nameof(IdentifierRootOID), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ActiveMoiety"/>
    public class ActiveMoietyDto
    {
        public required Dictionary<string, object?> ActiveMoiety { get; set; }

        /// <summary>
        /// Primary key for the ActiveMoiety table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ActiveMoietyID =>
            ActiveMoiety.TryGetValue("EncryptedActiveMoietyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to IngredientSubstance (The parent substance).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientSubstanceID =>
            ActiveMoiety.TryGetValue("EncryptedIngredientSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// UNII code of the active moiety ([activeMoiety][code] code).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? MoietyUNII =>
            ActiveMoiety.TryGetValue(nameof(MoietyUNII), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Name of the active moiety ([activeMoiety][name]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? MoietyName =>
            ActiveMoiety.TryGetValue(nameof(MoietyName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Address"/>
    public class AddressDto
    {
        public required Dictionary<string, object?> Address { get; set; }
        public List<ContactPartyDto> ContactParties { get; set; } = new();

        /// <summary>
        /// Primary key for the Address table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? AddressID =>
            Address.TryGetValue("EncryptedAddressID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// First line of the street address ([streetAddressLine]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StreetAddressLine1 =>
            Address.TryGetValue(nameof(StreetAddressLine1), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Second line of the street address ([streetAddressLine]), optional.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StreetAddressLine2 =>
            Address.TryGetValue(nameof(StreetAddressLine2), out var value)
                ? value as string
                : null;

        /// <summary>
        /// City name ([city]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? City =>
            Address.TryGetValue(nameof(City), out var value)
                ? value as string
                : null;

        /// <summary>
        /// State or province ([state]), required if country is USA.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StateProvince =>
            Address.TryGetValue(nameof(StateProvince), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Postal or ZIP code ([postalCode]), required if country is USA.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PostalCode =>
            Address.TryGetValue(nameof(PostalCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// ISO 3166-1 alpha-3 country code ([country code]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CountryCode =>
            Address.TryGetValue(nameof(CountryCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Full country name ([country] name).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CountryName =>
            Address.TryGetValue(nameof(CountryName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Analyte"/>
    public class AnalyteDto
    {
        public required Dictionary<string, object?> Analyte { get; set; }

        /// <summary>
        /// Primary key for the Analyte table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? AnalyteID =>
            Analyte.TryGetValue("EncryptedAnalyteID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to SubstanceSpecification.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SubstanceSpecificationID =>
            Analyte.TryGetValue("EncryptedSubstanceSpecificationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to IdentifiedSubstance (The substance being measured).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? AnalyteSubstanceID =>
            Analyte.TryGetValue("EncryptedAnalyteSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ApplicationType"/>
    public class ApplicationTypeDto
    {
        public required Dictionary<string, object?> ApplicationType { get; set; }

        /// <summary>
        /// Primary key for the ApplicationType table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ApplicationTypeID =>
            ApplicationType.TryGetValue("EncryptedApplicationTypeID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code identifying the application type (e.g., General Tolerance).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? AppTypeCode =>
            ApplicationType.TryGetValue(nameof(AppTypeCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for AppTypeCode (2.16.840.1.113883.6.275.1).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? AppTypeCodeSystem =>
            ApplicationType.TryGetValue(nameof(AppTypeCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for AppTypeCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? AppTypeDisplayName =>
            ApplicationType.TryGetValue(nameof(AppTypeDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.AttachedDocument"/>
    public class AttachedDocumentDto
    {
        public required Dictionary<string, object?> AttachedDocument { get; set; }

        /// <summary>
        /// Primary key for the AttachedDocument table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? AttachedDocumentID =>
            AttachedDocument.TryGetValue("EncryptedAttachedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to the Section where this document is referenced. Can be null.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            AttachedDocument.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to a ComplianceAction, if the document is part of a drug listing or establishment inactivation. Can be null.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ComplianceActionID =>
            AttachedDocument.TryGetValue("EncryptedComplianceActionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to a Product, if the document is related to a specific product (e.g., REMS material). Can be null.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            AttachedDocument.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Identifies the type of the parent element containing the document reference (e.g., "DisciplinaryAction").
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ParentEntityType =>
            AttachedDocument.TryGetValue(nameof(ParentEntityType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Foreign key to the parent table (e.g., DisciplinaryActionID).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ParentEntityID =>
            AttachedDocument.TryGetValue("EncryptedParentEntityID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// MIME type of the attached document (e.g., "application/pdf").
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? MediaType =>
            AttachedDocument.TryGetValue(nameof(MediaType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// File name of the attached document.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? FileName =>
            AttachedDocument.TryGetValue(nameof(FileName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The root identifier of the document from the [id] element, required for REMS materials (SPL IG 23.2.9.1).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DocumentIdRoot =>
            AttachedDocument.TryGetValue(nameof(DocumentIdRoot), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The title of the document reference (SPL IG 23.2.9.2).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? Title =>
            AttachedDocument.TryGetValue(nameof(Title), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The ID referenced within the document's title, linking it to content in the section text (SPL IG 23.2.9.3).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? TitleReference =>
            AttachedDocument.TryGetValue(nameof(TitleReference), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.BillingUnitIndex"/>
    public class BillingUnitIndexDto
    {
        public required Dictionary<string, object?> BillingUnitIndex { get; set; }

        /// <summary>
        /// Primary key for the BillingUnitIndex table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? BillingUnitIndexID =>
            BillingUnitIndex.TryGetValue("EncryptedBillingUnitIndexID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to the Indexing Section (48779-3) in the Billing Unit Index document.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            BillingUnitIndex.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// The NDC Package Code being linked ([containerPackagedProduct][code] code).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PackageNDCValue =>
            BillingUnitIndex.TryGetValue(nameof(PackageNDCValue), out var value)
                ? value as string
                : null;

        /// <summary>
        /// System for NDC.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PackageNDCSystemOID =>
            BillingUnitIndex.TryGetValue(nameof(PackageNDCSystemOID), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The NCPDP Billing Unit Code associated with the NDC package (GM, ML, or EA).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? BillingUnitCode =>
            BillingUnitIndex.TryGetValue(nameof(BillingUnitCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system OID for the NCPDP Billing Unit Code (2.16.840.1.113883.2.13).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? BillingUnitCodeSystemOID =>
            BillingUnitIndex.TryGetValue(nameof(BillingUnitCodeSystemOID), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.BusinessOperation"/>
    public class BusinessOperationDto
    {
        public required Dictionary<string, object?> BusinessOperation { get; set; }
        public List<BusinessOperationQualifierDto> BusinessOperationQualifiers { get; set; } = new();
        public List<LicenseDto> Licenses { get; set; } = new();

        /// <summary>
        /// Primary key for the BusinessOperation table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? BusinessOperationID =>
            BusinessOperation.TryGetValue("EncryptedBusinessOperationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to DocumentRelationship (linking to the Org performing the operation).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentRelationshipID =>
            BusinessOperation.TryGetValue("EncryptedDocumentRelationshipID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Organization performing the operation ([performance][actDefinition][code code="code"]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PerformingOrganizationID =>
            BusinessOperation.TryGetValue("EncryptedPerformingOrganizationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code identifying the business operation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? OperationCode =>
            BusinessOperation.TryGetValue(nameof(OperationCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for the operation code (typically 2.16.840.1.113883.3.26.1.1).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? OperationCodeSystem =>
            BusinessOperation.TryGetValue(nameof(OperationCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name matching the operation code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? OperationDisplayName =>
            BusinessOperation.TryGetValue(nameof(OperationDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.BusinessOperationProductLink"/>
    public class BusinessOperationProductLinkDto
    {
        public required Dictionary<string, object?> BusinessOperationProductLink { get; set; }

        /// <summary>
        /// Primary key for the BusinessOperationProductLink table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? BusinessOperationProductLinkID =>
            BusinessOperationProductLink.TryGetValue("EncryptedBusinessOperationProductLinkID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to BusinessOperation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? BusinessOperationID =>
            BusinessOperationProductLink.TryGetValue("EncryptedBusinessOperationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (The product linked to the operation).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            BusinessOperationProductLink.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.BusinessOperationQualifier"/>
    public class BusinessOperationQualifierDto
    {
        public required Dictionary<string, object?> BusinessOperationQualifier { get; set; }

        /// <summary>
        /// Primary key for the BusinessOperationQualifier table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? BusinessOperationQualifierID =>
            BusinessOperationQualifier.TryGetValue("EncryptedBusinessOperationQualifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to BusinessOperation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? BusinessOperationID =>
            BusinessOperationQualifier.TryGetValue("EncryptedBusinessOperationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code qualifying the business operation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? QualifierCode =>
            BusinessOperationQualifier.TryGetValue(nameof(QualifierCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for the qualifier code (typically 2.16.840.1.113883.3.26.1.1).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? QualifierCodeSystem =>
            BusinessOperationQualifier.TryGetValue(nameof(QualifierCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name matching the qualifier code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? QualifierDisplayName =>
            BusinessOperationQualifier.TryGetValue(nameof(QualifierDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.CertificationProductLink"/>
    public class CertificationProductLinkDto
    {
        public required Dictionary<string, object?> CertificationProductLink { get; set; }

        /// <summary>
        /// Primary key for the CertificationProductLink table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? CertificationProductLinkID =>
            CertificationProductLink.TryGetValue("EncryptedCertificationProductLinkID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to DocumentRelationship (linking Doc to certified Establishment).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentRelationshipID =>
            CertificationProductLink.TryGetValue("EncryptedDocumentRelationshipID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to ProductIdentifier.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductIdentifierID =>
            CertificationProductLink.TryGetValue("EncryptedProductIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Characteristic"/>
    public class CharacteristicDto
    {
        public required Dictionary<string, object?> Characteristic { get; set; }
        public List<PackageIdentifierDto?> PackagingIdentifiers { get; set; } = new();
        public List<PackagingLevelDto?> PackagingLevels { get; set; } = new();

        /// <summary>
        /// Primary key for the Characteristic table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? CharacteristicID =>
            Characteristic.TryGetValue("EncryptedCharacteristicID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (if characteristic applies to product).
        /// Used for traditional product-level characteristics like color, shape, imprint.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            Characteristic.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to PackagingLevel (if characteristic applies to package).
        /// Used for package-level characteristics like container type or labeling.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PackagingLevelID =>
            Characteristic.TryGetValue("EncryptedPackagingLevelID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// ENHANCED: Foreign key to Moiety (if characteristic applies to a chemical moiety).
        /// Used for substance indexing to link chemical structure data to specific 
        /// molecular components within a substance definition per ISO/FDIS 11238.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? MoietyID =>
            Characteristic.TryGetValue("EncryptedMoietyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code identifying the characteristic property.
        /// Traditional: SPLCOLOR, SPLSHAPE, SPLSIZE, SPLIMPRINT, SPLFLAVOR, SPLSCORE, SPLIMAGE, 
        /// SPLCMBPRDTP, SPLPRODUCTIONAMOUNT, SPLUSE, SPLSTERILEUSE, SPLPROFESSIONALUSE, SPLSMALLBUSINESS.
        /// ENHANCED: For substance indexing, typically "C103240" for Chemical Structure characteristics.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CharacteristicCode =>
            Characteristic.TryGetValue(nameof(CharacteristicCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for CharacteristicCode.
        /// Typically "2.16.840.1.113883.3.26.1.1" (NCI Thesaurus) for substance indexing.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CharacteristicCodeSystem =>
            Characteristic.TryGetValue(nameof(CharacteristicCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Indicates the XML Schema instance type of the [value] element.
        /// Standard types: PQ, INT, CV, ST, BL, IVL_PQ, ED.
        /// ENHANCED: "ED" (Encapsulated Data) used for chemical structure characteristics.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ValueType =>
            Characteristic.TryGetValue(nameof(ValueType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Value for PQ type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? ValuePQ_Value =>
            Characteristic.TryGetValue(nameof(ValuePQ_Value), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Unit for PQ type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ValuePQ_Unit =>
            Characteristic.TryGetValue(nameof(ValuePQ_Unit), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Value for INT type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ValueINT =>
            Characteristic.TryGetValue(nameof(ValueINT), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Code for CV type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ValueCV_Code =>
            Characteristic.TryGetValue(nameof(ValueCV_Code), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for CV type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ValueCV_CodeSystem =>
            Characteristic.TryGetValue(nameof(ValueCV_CodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for CV type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ValueCV_DisplayName =>
            Characteristic.TryGetValue(nameof(ValueCV_DisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Value for ST type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ValueST =>
            Characteristic.TryGetValue(nameof(ValueST), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Value for BL type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool? ValueBL =>
            Characteristic.TryGetValue(nameof(ValueBL), out var value)
                ? value as bool?
                : null;

        /// <summary>
        /// Low value for IVL_PQ type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? ValueIVLPQ_LowValue =>
            Characteristic.TryGetValue(nameof(ValueIVLPQ_LowValue), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Low unit for IVL_PQ type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ValueIVLPQ_LowUnit =>
            Characteristic.TryGetValue(nameof(ValueIVLPQ_LowUnit), out var value)
                ? value as string
                : null;

        /// <summary>
        /// High value for IVL_PQ type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? ValueIVLPQ_HighValue =>
            Characteristic.TryGetValue(nameof(ValueIVLPQ_HighValue), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// High unit for IVL_PQ type.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ValueIVLPQ_HighUnit =>
            Characteristic.TryGetValue(nameof(ValueIVLPQ_HighUnit), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Media type for ED type.
        /// ENHANCED: For chemical structure data, specifies molecular representation format:
        /// "application/x-mdl-molfile", "application/x-inchi", "application/x-inchi-key".
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ValueED_MediaType =>
            Characteristic.TryGetValue(nameof(ValueED_MediaType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// File name for ED type.
        /// ENHANCED: May reference chemical structure data files or internal identifiers.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ValueED_FileName =>
            Characteristic.TryGetValue(nameof(ValueED_FileName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// ENHANCED: Raw CDATA content for ED type chemical structure characteristics.
        /// Contains molecular structure data in format specified by ValueED_MediaType.
        /// For MOLFILE: Complete MDL connection table with atom coordinates and bonds.
        /// For InChI: IUPAC International Chemical Identifier string.
        /// For InChI-Key: Hash-based compact molecular identifier.
        /// Preserves exact formatting for scientific integrity per ISO/FDIS 11238.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ValueED_CDATAContent =>
            Characteristic.TryGetValue(nameof(ValueED_CDATAContent), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Used for INT type with nullFlavor="PINF" (e.g., SPLUSE, SPLPRODUCTIONAMOUNT).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ValueNullFlavor =>
            Characteristic.TryGetValue(nameof(ValueNullFlavor), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Optional additional text describing the characteristic ([characteristic][value][originalText]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? OriginalText =>
           Characteristic.TryGetValue(nameof(OriginalText), out var value)
               ? value as string
               : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Commodity"/>
    public class CommodityDto
    {
        public required Dictionary<string, object?> Commodity { get; set; }

        /// <summary>
        /// Primary key for the Commodity table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? CommodityID =>
            Commodity.TryGetValue("EncryptedCommodityID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code identifying the commodity.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CommodityCode =>
            Commodity.TryGetValue(nameof(CommodityCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for CommodityCode (2.16.840.1.113883.6.275.1).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CommodityCodeSystem =>
            Commodity.TryGetValue(nameof(CommodityCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for CommodityCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CommodityDisplayName =>
            Commodity.TryGetValue(nameof(CommodityDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Optional name ([presentSubstance][name]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CommodityName =>
            Commodity.TryGetValue(nameof(CommodityName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ContributingFactor"/>
    public class ContributingFactorDto
    {
        public required Dictionary<string, object?> ContributingFactor { get; set; }
        public List<InteractionConsequenceDto> InteractionConsequences { get; set; } = new();

        /// <summary>
        /// Primary key for the ContributingFactor table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ContributingFactorID =>
            ContributingFactor.TryGetValue("EncryptedContributingFactorID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to InteractionIssue.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? InteractionIssueID =>
            ContributingFactor.TryGetValue("EncryptedInteractionIssueID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Link to the IdentifiedSubstance representing the drug or pharmacologic class that is the contributing factor.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? FactorSubstanceID =>
            ContributingFactor.TryGetValue("EncryptedFactorSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ComplianceAction"/>
    public class ComplianceActionDto
    {
        public required Dictionary<string, object?> ComplianceAction { get; set; }

        /// <summary>
        /// Primary key for the ComplianceAction table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ComplianceActionID =>
            ComplianceAction.TryGetValue("EncryptedComplianceActionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Section (for Drug Listing Inactivation - Section 30).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            ComplianceAction.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Link to the specific package NDC being inactivated/reactivated (Section 30).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PackageIdentifierID =>
            ComplianceAction.TryGetValue("EncryptedPackageIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Link to the DocumentRelationship representing the establishment being inactivated/reactivated (Section 31).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentRelationshipID =>
            ComplianceAction.TryGetValue("EncryptedDocumentRelationshipID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code for the compliance action (e.g., C162847 Inactivated).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ActionCode =>
            ComplianceAction.TryGetValue(nameof(ActionCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for ActionCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ActionCodeSystem =>
            ComplianceAction.TryGetValue(nameof(ActionCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for ActionCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ActionDisplayName =>
            ComplianceAction.TryGetValue(nameof(ActionDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Date the inactivation begins.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? EffectiveTimeLow =>
            ComplianceAction.TryGetValue(nameof(EffectiveTimeLow), out var value)
                ? value as DateTime?
                : null;

        /// <summary>
        /// Date the inactivation ends (reactivation date), if applicable.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? EffectiveTimeHigh =>
            ComplianceAction.TryGetValue(nameof(EffectiveTimeHigh), out var value)
                ? value as DateTime?
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ContactParty"/>
    public class ContactPartyDto
    {
        public required Dictionary<string, object?> ContactParty { get; set; }
        public OrganizationDto? Organization { get; set; }
        public AddressDto? Address { get; set; }
        public ContactPersonDto? ContactPerson { get; set; }
        public List<ContactPartyTelecomDto> Telecoms { get; set; } = new();

        /// <summary>
        /// Primary key for the ContactParty table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ContactPartyID =>
            ContactParty.TryGetValue("EncryptedContactPartyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key linking to the Organization this contact party belongs to.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? OrganizationID =>
            ContactParty.TryGetValue("EncryptedOrganizationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key linking to the Address for this contact party.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? AddressID =>
            ContactParty.TryGetValue("EncryptedAddressID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key linking to the ContactPerson for this contact party.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ContactPersonID =>
            ContactParty.TryGetValue("EncryptedContactPersonID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ContactPartyTelecom"/>
    public class ContactPartyTelecomDto
    {
        public required Dictionary<string, object?> ContactPartyTelecom { get; set; }
        public ContactPartyDto? ContactParty { get; set; }
        public TelecomDto? Telecom { get; set; }

        /// <summary>
        /// Primary key for the ContactPartyTelecom table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ContactPartyTelecomID =>
            ContactPartyTelecom.TryGetValue("EncryptedContactPartyTelecomID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to ContactParty.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ContactPartyID =>
            ContactPartyTelecom.TryGetValue("EncryptedContactPartyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Telecom.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? TelecomID =>
            ContactPartyTelecom.TryGetValue("EncryptedTelecomID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ContactPerson"/>
    public class ContactPersonDto
    {
        public required Dictionary<string, object?> ContactPerson { get; set; }
        public List<ContactPartyDto> ContactParties { get; set; } = new();

        /// <summary>
        /// Primary key for the ContactPerson table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ContactPersonID =>
            ContactPerson.TryGetValue("EncryptedContactPersonID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Name of the contact person ([contactPerson][name]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ContactPersonName =>
            ContactPerson.TryGetValue(nameof(ContactPersonName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.DisciplinaryAction"/>
    public class DisciplinaryActionDto
    {
        public required Dictionary<string, object?> DisciplinaryAction { get; set; }

        /// <summary>
        /// Primary key for the DisciplinaryAction table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DisciplinaryActionID =>
            DisciplinaryAction.TryGetValue("EncryptedDisciplinaryActionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to License.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LicenseID =>
            DisciplinaryAction.TryGetValue("EncryptedLicenseID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code identifying the disciplinary action type (e.g., suspension, revocation, activation).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ActionCode =>
            DisciplinaryAction.TryGetValue(nameof(ActionCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for ActionCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ActionCodeSystem =>
            DisciplinaryAction.TryGetValue(nameof(ActionCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for ActionCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ActionDisplayName =>
            DisciplinaryAction.TryGetValue(nameof(ActionDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Date the disciplinary action became effective.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? EffectiveTime =>
            DisciplinaryAction.TryGetValue(nameof(EffectiveTime), out var value)
                ? value as DateTime?
                : null;

        /// <summary>
        /// Text description used when the action code is 'other'.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ActionText =>
            DisciplinaryAction.TryGetValue(nameof(ActionText), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.DocumentAuthor"/>
    public class DocumentAuthorDto
    {
        public required Dictionary<string, object?> DocumentAuthor { get; set; }
        public DocumentDto? Document { get; set; }
        public OrganizationDto? Organization { get; set; }

        /// <summary>
        /// Primary key for the DocumentAuthor table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentAuthorID =>
            DocumentAuthor.TryGetValue("EncryptedDocumentAuthorID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Document.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            DocumentAuthor.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Organization (the authoring org, e.g., Labeler [cite: 786]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? OrganizationID =>
            DocumentAuthor.TryGetValue("EncryptedOrganizationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Identifies the type or role of the author, e.g., Labeler (4.1.2 [cite: 785]), FDA (8.1.2[cite: 945], 15.1.2[cite: 1030], 20.1.2[cite: 1295], 21.1.2[cite: 1330], 30.1.2[cite: 1553], 31.1.2[cite: 1580], 32.1.2[cite: 1607], 33.1.2 [cite: 1643]), NCPDP (12.1.2 [cite: 995]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? AuthorType =>
            DocumentAuthor.TryGetValue(nameof(AuthorType), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Document"/>
    public class DocumentDto
    {
        public required Dictionary<string, object?> Document { get; set; }
        public List<DocumentAuthorDto> DocumentAuthors { get; set; } = new();
        public List<RelatedDocumentDto> SourceRelatedDocuments { get; set; } = new();
        public List<DocumentRelationshipDto> DocumentRelationships { get; set; } = new();
        public List<StructuredBodyDto> StructuredBodies { get; set; } = new();
        public List<LegalAuthenticatorDto> LegalAuthenticators { get; set; } = new();

        /// <summary>
        /// Provides query execution performance in Milliseconds.
        /// </summary>
        public double PerformanceMs { get; set; }

        /// <summary>
        /// Primary key for the Document table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            Document.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Globally Unique Identifier for this specific document version ([id root]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? DocumentGUID =>
            Document.TryGetValue(nameof(DocumentGUID), out var value)
                ? value as Guid?
                : null;

        /// <summary>
        /// LOINC code identifying the document type ([code] code).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DocumentCode =>
            Document.TryGetValue(nameof(DocumentCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for the document type code ([code] codeSystem), typically 2.16.840.1.113883.6.1.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DocumentCodeSystem =>
            Document.TryGetValue(nameof(DocumentCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        ///code system for the document codeSystemName="LOINC"
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DocumentCodeSystemName =>
            Document.TryGetValue(nameof(DocumentCodeSystemName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name matching the document type code ([code] displayName).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DocumentDisplayName =>
            Document.TryGetValue(nameof(DocumentDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Document title ([title]), if provided.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? Title =>
            Document.TryGetValue(nameof(Title), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Date reference for the SPL version ([effectiveTime value]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? EffectiveTime =>
            Document.TryGetValue(nameof(EffectiveTime), out var value)
                ? value as DateTime?
                : null;

        /// <summary>
        /// Globally Unique Identifier for the document set, constant across versions ([setId root]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? SetGUID =>
            Document.TryGetValue(nameof(SetGUID), out var value)
                ? value as Guid?
                : null;

        /// <summary>
        /// Sequential integer for the document version ([versionNumber value]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? VersionNumber =>
            Document.TryGetValue(nameof(VersionNumber), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Name of the submitted XML file (e.g., DocumentGUID.xml).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubmissionFileName =>
            Document.TryGetValue(nameof(SubmissionFileName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Author renderings with pre-computed hierarchical structures and business operations
        /// for optimized SPL template processing. Populated during export processing.
        /// </summary>
        /// <seealso cref="AuthorRendering"/>
        /// <seealso cref="IAuthorRenderingService"/>
        [Newtonsoft.Json.JsonIgnore]
        public List<AuthorRendering>? RenderedAuthors { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.DocumentRelationship"/>
    public class DocumentRelationshipDto
    {
        public required Dictionary<string, object?> DocumentRelationship { get; set; }
        public OrganizationDto? ParentOrganization { get; set; }
        public OrganizationDto? ChildOrganization { get; set; }
        public List<BusinessOperationDto> BusinessOperations { get; set; } = new();
        public List<CertificationProductLinkDto> CertificationProductLinks { get; set; } = new();
        public List<ComplianceActionDto> ComplianceActions { get; set; } = new();
        public List<FacilityProductLinkDto> FacilityProductLinks { get; set; } = new();
        public List<DocumentRelationshipIdentifierDto> RelationshipIdentifiers { get; set; } = new();

        /// <summary>
        /// Primary key for the DocumentRelationship table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentRelationshipID =>
            DocumentRelationship.TryGetValue("EncryptedDocumentRelationshipID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Document.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            DocumentRelationship.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Organization (e.g., Labeler).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ParentOrganizationID =>
            DocumentRelationship.TryGetValue("EncryptedParentOrganizationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Organization (e.g., Registrant or Establishment).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ChildOrganizationID =>
            DocumentRelationship.TryGetValue("EncryptedChildOrganizationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Describes the specific relationship, e.g., LabelerToRegistrant (4.1.3 [cite: 788]), RegistrantToEstablishment (4.1.4 [cite: 791]), EstablishmentToUSagent (6.1.4 [cite: 914]), EstablishmentToImporter (6.1.5 [cite: 918]), LabelerToDetails (5.1.3 [cite: 863]), FacilityToParentCompany (35.1.6 [cite: 1695]), LabelerToParentCompany (36.1.2.5 [cite: 1719]), DocumentToBulkLotManufacturer (16.1.3).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RelationshipType =>
            DocumentRelationship.TryGetValue(nameof(RelationshipType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Indicates the level in the hierarchy (e.g., 1 for Labeler, 2 for Registrant, 3 for Establishment).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? RelationshipLevel =>
            DocumentRelationship.TryGetValue(nameof(RelationshipLevel), out var value)
                ? value as int?
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.DocumentRelationshipIdentifier"/>
    public class DocumentRelationshipIdentifierDto
    {
        public required Dictionary<string, object?> DocumentRelationshipIdentifier { get; set; }

        /// <summary>
        /// Primary key for the DocumentRelationshipIdentifier table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentRelationshipIdentifierID =>
            DocumentRelationshipIdentifier.TryGetValue("EncryptedDocumentRelationshipIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Document Relationship.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentRelationshipID =>
            DocumentRelationshipIdentifier.TryGetValue("EncryptedDocumentRelationshipID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Organization Identifier (e.g., Labeler).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? OrganizationIdentifierID =>
            DocumentRelationshipIdentifier.TryGetValue("EncryptedOrganizationIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
     
    }

    /**************************************************************/
    /// <seealso cref="Label.DosingSpecification"/>
    public class DosingSpecificationDto
    {
        public required Dictionary<string, object?> DosingSpecification { get; set; }

        /// <summary>
        /// Primary key for the DosingSpecification table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DosingSpecificationID =>
            DosingSpecification.TryGetValue("EncryptedDosingSpecificationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key reference to the associated Product entity.
        /// Links this dosing specification to a specific pharmaceutical product.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            DosingSpecification.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Route of administration code associated with the dose, following SPL Implementation Guide Section 3.2.20.2f.
        /// Must be from FDA SPL code system (2.16.840.1.113883.3.26.1.1) or include nullFlavor.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RouteCode =>
            DosingSpecification.TryGetValue(nameof(RouteCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system identifier for the RouteCode, typically FDA SPL system (2.16.840.1.113883.3.26.1.1).
        /// Required when RouteCode is specified to ensure proper code system context.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RouteCodeSystem =>
            DosingSpecification.TryGetValue(nameof(RouteCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Human-readable display name for the RouteCode.
        /// Provides clear identification of the route of administration for users.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RouteDisplayName =>
            DosingSpecification.TryGetValue(nameof(RouteDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Numeric value representing a single dose quantity according to SPL Implementation Guide Section 16.2.4.3-16.2.4.6.
        /// Must be a valid number (may be 0) and should not contain spaces.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? DoseQuantityValue =>
            DosingSpecification.TryGetValue(nameof(DoseQuantityValue), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Unit of measure for the dose quantity, must conform to UCUM (Unified Code for Units of Measure) standards per SPL Implementation Guide Section 16.2.4.5.
        /// Common units include mg, mL, g, L, and complex units like mg/mL.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DoseQuantityUnit =>
            DosingSpecification.TryGetValue(nameof(DoseQuantityUnit), out var value)
                ? value as string
                : null;

        /// <summary>
        /// NullFlavor attribute for route code when the specific route is unknown or not applicable.
        /// Allows for flexible handling of route specifications in SPL documents.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RouteNullFlavor =>
            DosingSpecification.TryGetValue(nameof(RouteNullFlavor), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.EquivalentEntity"/>
    public class EquivalentEntityDto
    {
        public required Dictionary<string, object?> EquivalentEntity { get; set; }

        /// <summary>
        /// Primary key for the EquivalentEntity table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? EquivalentEntityID =>
            EquivalentEntity.TryGetValue("EncryptedEquivalentEntityID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (The product being described).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            EquivalentEntity.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code indicating the type of equivalence relationship, e.g., C64637 (Same), pending (Predecessor).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EquivalenceCode =>
            EquivalentEntity.TryGetValue(nameof(EquivalenceCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for EquivalenceCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EquivalenceCodeSystem =>
            EquivalentEntity.TryGetValue(nameof(EquivalenceCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Item code of the equivalent product (e.g., source NDC product code).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DefiningMaterialKindCode =>
            EquivalentEntity.TryGetValue(nameof(DefiningMaterialKindCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for the equivalent product's item code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DefiningMaterialKindSystem =>
            EquivalentEntity.TryGetValue(nameof(DefiningMaterialKindSystem), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.FacilityProductLink"/>
    public class FacilityProductLinkDto
    {
        public required Dictionary<string, object?> FacilityProductLink { get; set; }

        public ProductIdentifierDto? ProductIdentifier { get; set; }

        /// <summary>
        /// Primary key for the FacilityProductLink table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? FacilityProductLinkID =>
            FacilityProductLink.TryGetValue("EncryptedFacilityProductLinkID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to DocumentRelationship (linking Doc/Reg to Facility).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentRelationshipID =>
            FacilityProductLink.TryGetValue("EncryptedDocumentRelationshipID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (if linked by internal ProductID).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            FacilityProductLink.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Link via Cosmetic Listing Number.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductIdentifierID =>
            FacilityProductLink.TryGetValue("EncryptedProductIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Link via Product Name (used if CLN not yet assigned).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            FacilityProductLink.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

       
    }

    /**************************************************************/
    /// <seealso cref="Label.GenericMedicine"/>
    public class GenericMedicineDto
    {
        public required Dictionary<string, object?> GenericMedicine { get; set; }

        /// <summary>
        /// Primary key for the GenericMedicine table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? GenericMedicineID =>
            GenericMedicine.TryGetValue("EncryptedGenericMedicineID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            GenericMedicine.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Non-proprietary name of the product ([genericMedicine][name]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? GenericName =>
            GenericMedicine.TryGetValue(nameof(GenericName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Phonetic spelling of the generic name ([name use="PHON"]), optional.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PhoneticName =>
            GenericMedicine.TryGetValue(nameof(PhoneticName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Holder"/>
    public class HolderDto
    {
        public required Dictionary<string, object?> Holder { get; set; }

        /// <summary>
        /// Primary key for the Holder table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? HolderID =>
            Holder.TryGetValue("EncryptedHolderID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to MarketingCategory.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? MarketingCategoryID =>
            Holder.TryGetValue("EncryptedMarketingCategoryID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Link to the Organization table for the Application Holder.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? HolderOrganizationID =>
            Holder.TryGetValue("EncryptedHolderOrganizationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Moiety"/>
    public class MoietyDto
    {
        public required Dictionary<string, object?> Moiety { get; set; }
        public List<CharacteristicDto> Characteristics { get; set; } = new List<CharacteristicDto>();

        /// <summary>
        /// Primary key for the Moiety table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? MoietyID =>
            Moiety.TryGetValue("EncryptedMoietyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to IdentifiedSubstance (The substance this moiety helps define).
        /// Links this molecular component to its parent substance record.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IdentifiedSubstanceID =>
            Moiety.TryGetValue("EncryptedIdentifiedSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Position of this moiety within the substance definition.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SequenceNumber =>
            Moiety.TryGetValue(nameof(SequenceNumber), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Code identifying the type or role of this moiety within the substance definition.
        /// Typically indicates whether this is a mixture component or other structural element.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? MoietyCode =>
            Moiety.TryGetValue(nameof(MoietyCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system OID for the moiety code, typically NCI Thesaurus.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? MoietyCodeSystem =>
            Moiety.TryGetValue(nameof(MoietyCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Human-readable name for the moiety code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? MoietyDisplayName =>
            Moiety.TryGetValue(nameof(MoietyDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Lower bound value for the quantity numerator in mixture ratios.
        /// Used to specify ranges or minimum quantities for this moiety component.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? QuantityNumeratorLowValue =>
            Moiety.TryGetValue(nameof(QuantityNumeratorLowValue), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Unit of measure for the quantity numerator.
        /// Typically "1" for dimensionless ratios in mixture specifications.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? QuantityNumeratorUnit =>
            Moiety.TryGetValue(nameof(QuantityNumeratorUnit), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Indicates whether the numerator low value boundary is inclusive in range specifications.
        /// False typically indicates "greater than" rather than "greater than or equal to".
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool? QuantityNumeratorInclusive =>
            Moiety.TryGetValue(nameof(QuantityNumeratorInclusive), out var value)
                ? value as bool?
                : null;

        /// <summary>
        /// Denominator value for quantity ratios in mixture specifications.
        /// Provides the base for calculating relative proportions of mixture components.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? QuantityDenominatorValue =>
            Moiety.TryGetValue(nameof(QuantityDenominatorValue), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Unit of measure for the quantity denominator.
        /// Typically "1" for dimensionless ratios in mixture specifications.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? QuantityDenominatorUnit =>
            Moiety.TryGetValue(nameof(QuantityDenominatorUnit), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.IdentifiedSubstance"/>
    public class IdentifiedSubstanceDto
    {
        public required Dictionary<string, object?> IdentifiedSubstance { get; set; }
        public List<SubstanceSpecificationDto> SubstanceSpecifications { get; set; } = new();
        public List<ContributingFactorDto> ContributingFactors { get; set; } = new();
        public List<PharmacologicClassDto> PharmacologicClasses { get; set; } = new();
        public List<MoietyDto> Moiety { get; set; } = new();

        /// <summary>
        /// Primary key for the IdentifiedSubstance table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IdentifiedSubstanceID =>
            IdentifiedSubstance.TryGetValue("EncryptedIdentifiedSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Section (The indexing section containing this substance).
        /// Links to the SPL indexing section where this substance is defined or referenced.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            IdentifiedSubstance.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Indicates whether the identified substance represents an Active Moiety (8.2.2) or 
        /// a Pharmacologic Class being defined (8.2.3). Used to distinguish between substance 
        /// definitions and substance references in indexing contexts.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubjectType =>
            IdentifiedSubstance.TryGetValue(nameof(SubjectType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The unique identifier value assigned by the FDA Substance Registration System.
        /// For Active Moieties: UNII (Unique Ingredient Identifier) code.
        /// For Pharmacologic Classes: MED-RT or MeSH classification code.
        /// This identifier is the primary means of substance identification across FDA systems.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubstanceIdentifierValue =>
            IdentifiedSubstance.TryGetValue(nameof(SubstanceIdentifierValue), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Object Identifier (OID) for the substance identification system.
        /// Specifies the authoritative system that assigned the substance identifier.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubstanceIdentifierSystemOID =>
            IdentifiedSubstance.TryGetValue(nameof(SubstanceIdentifierSystemOID), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Indicates if this record defines the substance/class (8.2.3) or references it (8.2.2).
        /// True for substance definitions that include complete identifying characteristics.
        /// False for substance references used in classification or indexing relationships.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool? IsDefinition =>
            IdentifiedSubstance.TryGetValue(nameof(IsDefinition), out var value)
                ? value as bool?
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Ingredient"/>
    public class IngredientDto
    {
        public required Dictionary<string, object?> Ingredient { get; set; }
        public IngredientSubstanceDto? IngredientSubstance { get; set; }
        public List<IngredientInstanceDto> IngredientInstances { get; set; } = new();
        public List<ReferenceSubstanceDto> ReferenceSubstances { get; set; } = new();
        public List<IngredientSourceProductDto> IngredientSourceProducts { get; set; } = new();
        public List<SpecifiedSubstanceDto> SpecifiedSubstances { get; set; } = new();

        /// <summary>
        /// Primary key for the Ingredient table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientID =>
            Ingredient.TryGetValue("EncryptedIngredientID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product or Product representing a Part. Null if linked via ProductConceptID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            Ingredient.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to IngredientSubstance.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientSubstanceID =>
            Ingredient.TryGetValue("EncryptedIngredientSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key for the SpecifiedSubstance table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SpecifiedSubstanceID =>
            Ingredient.TryGetValue("EncryptedSpecifiedSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Link to ReferenceSubstance table if BasisOfStrength (ClassCode) is ACTIR.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ReferenceSubstanceID =>
            Ingredient.TryGetValue("EncryptedReferenceSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// FK to ProductConcept, used when the ingredient belongs to a Product Concept instead of a concrete Product. Null if linked via ProductID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductConceptID =>
            Ingredient.TryGetValue("EncryptedProductConceptID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Class code indicating ingredient type (e.g., ACTIB, ACTIM, ACTIR, IACT, INGR, COLR, CNTM, ADJV).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ClassCode =>
            Ingredient.TryGetValue(nameof(ClassCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Strength expressed as numerator/denominator value and unit ([quantity]). Null for CNTM unless zero numerator.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? QuantityNumerator =>
            Ingredient.TryGetValue(nameof(QuantityNumerator), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Corresponds to [quantity][numerator unit].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? QuantityNumeratorUnit =>
            Ingredient.TryGetValue(nameof(QuantityNumeratorUnit), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Translation attribute for the numerator (e.g., translation code="C28253")
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? NumeratorTranslationCode =>
            Ingredient.TryGetValue(nameof(NumeratorTranslationCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Translation attribute for the numerator (e.g., translation codeSystem="2.16.840.1.113883.3.26.1.1")
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? NumeratorCodeSystem =>
            Ingredient.TryGetValue(nameof(NumeratorCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Translation attribute for the numerator (e.g., translation displayName="MILLIGRAM")
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? NumeratorDisplayName =>
            Ingredient.TryGetValue(nameof(NumeratorDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Translation attribute for the numerator (e.g., translation value="50")
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? NumeratorValue =>
            Ingredient.TryGetValue(nameof(NumeratorValue), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Corresponds to [quantity][denominator value].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? QuantityDenominator =>
            Ingredient.TryGetValue(nameof(QuantityDenominator), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Translation attribute for the numerator (e.g., translation code="C28253")
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DenominatorTranslationCode =>
            Ingredient.TryGetValue(nameof(DenominatorTranslationCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Translation attribute for the numerator (e.g., translation codeSystem="2.16.840.1.113883.3.26.1.1")
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DenominatorCodeSystem =>
            Ingredient.TryGetValue(nameof(DenominatorCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Translation attribute for the numerator (e.g., translation displayName="MILLIGRAM")
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DenominatorDisplayName =>
            Ingredient.TryGetValue(nameof(DenominatorDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Translation attribute for the numerator (e.g., translation value="50")
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DenominatorValue =>
            Ingredient.TryGetValue(nameof(DenominatorValue), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Corresponds to [quantity][denominator unit].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? QuantityDenominatorUnit =>
            Ingredient.TryGetValue(nameof(QuantityDenominatorUnit), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Flag indicating if the inactive ingredient information is confidential ([confidentialityCode code="B"]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool? IsConfidential =>
            Ingredient.TryGetValue(nameof(IsConfidential), out var value)
                ? value as bool?
                : null;

        /// <summary>
        /// Order of the ingredient as listed in the SPL file (important for cosmetics).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SequenceNumber =>
            Ingredient.TryGetValue(nameof(SequenceNumber), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Display name (displayName="MILLIGRAM").
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DisplayName =>
            Ingredient.TryGetValue(nameof(DisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The name of the XML element this ingredient was parsed from (e.g., "ingredient", "activeIngredient").
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? OriginatingElement =>
            Ingredient.TryGetValue(nameof(OriginatingElement), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.IngredientInstance"/>
    public class IngredientInstanceDto
    {
        public required Dictionary<string, object?> IngredientInstance { get; set; }
        public LotIdentifierDto? LotIdentifier { get; set; }

        public List<IngredientSubstanceDto> IngredientSubstances { get; set; } = new();

        /// <summary>
        /// Primary key for the IngredientInstance table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientInstanceID =>
            IngredientInstance.TryGetValue("EncryptedIngredientInstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to ProductInstance (The Fill Lot this bulk lot contributes to).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? FillLotInstanceID =>
            IngredientInstance.TryGetValue("EncryptedFillLotInstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Reference to the substance constituting the bulk lot.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientSubstanceID =>
            IngredientInstance.TryGetValue("EncryptedIngredientSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to LotIdentifier (The Bulk Lot number).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LotIdentifierID =>
            IngredientInstance.TryGetValue("EncryptedLotIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Reference to the Organization that manufactured the bulk lot.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ManufacturerOrganizationID =>
            IngredientInstance.TryGetValue("EncryptedManufacturerOrganizationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.IngredientSourceProduct"/>
    public class IngredientSourceProductDto
    {
        public required Dictionary<string, object?> IngredientSourceProduct { get; set; }

        /// <summary>
        /// Primary key for the IngredientSourceProduct table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientSourceProductID =>
            IngredientSourceProduct.TryGetValue("EncryptedIngredientSourceProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Ingredient.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientID =>
            IngredientSourceProduct.TryGetValue("EncryptedIngredientID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// NDC Product Code of the source product used for the ingredient.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SourceProductNDC =>
            IngredientSourceProduct.TryGetValue(nameof(SourceProductNDC), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for Source NDC.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SourceProductNDCSysten =>
            IngredientSourceProduct.TryGetValue(nameof(SourceProductNDCSysten), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.IngredientSubstance"/>
    public class IngredientSubstanceDto
    {
        public required Dictionary<string, object?> IngredientSubstance { get; set; }
        public List<IngredientInstanceDto> IngredientInstances { get; set; } = new();
        public List<ActiveMoietyDto> ActiveMoieties { get; set; } = new();

        /// <summary>
        /// Primary key for the IngredientSubstance table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientSubstanceID =>
            IngredientSubstance.TryGetValue("EncryptedIngredientSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Unique Ingredient Identifier ([code code=] where codeSystem="2.16.840.1.113883.4.9"). Optional for cosmetics.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? UNII =>
            IngredientSubstance.TryGetValue(nameof(UNII), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Name of the substance (name).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubstanceName =>
            IngredientSubstance.TryGetValue(nameof(SubstanceName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The name of the XML element this ingredient was parsed from (e.g., "inactiveIngredientSubstance").
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? OriginatingElement =>
            IngredientSubstance.TryGetValue(nameof(OriginatingElement), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.InteractionConsequence"/>
    public class InteractionConsequenceDto
    {
        public required Dictionary<string, object?> InteractionConsequence { get; set; }

        /// <summary>
        /// Primary key for the InteractionConsequence table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? InteractionConsequenceID =>
            InteractionConsequence.TryGetValue("EncryptedInteractionConsequenceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to InteractionIssue.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? InteractionIssueID =>
            InteractionConsequence.TryGetValue("EncryptedInteractionIssueID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code indicating the type of consequence: Pharmacokinetic effect (C54386) or Medical problem (44100-6).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ConsequenceTypeCode =>
            InteractionConsequence.TryGetValue(nameof(ConsequenceTypeCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ConsequenceTypeCodeSystem =>
            InteractionConsequence.TryGetValue(nameof(ConsequenceTypeCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ConsequenceTypeDisplayName =>
            InteractionConsequence.TryGetValue(nameof(ConsequenceTypeDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code for the specific pharmacokinetic effect or medical problem.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ConsequenceValueCode =>
            InteractionConsequence.TryGetValue(nameof(ConsequenceValueCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for the value code (NCI Thesaurus 2.16.840.1.113883.3.26.1.1 or SNOMED CT 2.16.840.1.113883.6.96).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ConsequenceValueCodeSystem =>
            InteractionConsequence.TryGetValue(nameof(ConsequenceValueCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for the value code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ConsequenceValueDisplayName =>
            InteractionConsequence.TryGetValue(nameof(ConsequenceValueDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.InteractionIssue"/>
    public class InteractionIssueDto
    {
        public required Dictionary<string, object?> InteractionIssue { get; set; }
        public List<InteractionConsequenceDto> InteractionConsequences { get; set; } = new();

        /// <summary>
        /// Primary key for the InteractionIssue table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? InteractionIssueID =>
            InteractionIssue.TryGetValue("EncryptedInteractionIssueID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Section where the interaction is mentioned.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            InteractionIssue.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code identifying an interaction issue (C54708).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? InteractionCode =>
            InteractionIssue.TryGetValue(nameof(InteractionCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? InteractionCodeSystem =>
            InteractionIssue.TryGetValue(nameof(InteractionCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name ('INTERACTION').
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? InteractionDisplayName =>
            InteractionIssue.TryGetValue(nameof(InteractionDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.LegalAuthenticator"/>
    public class LegalAuthenticatorDto
    {
        public required Dictionary<string, object?> LegalAuthenticator { get; set; }
        public DocumentDto? Document { get; set; }
        public OrganizationDto? Organization { get; set; }

        /// <summary>
        /// Primary key for the LegalAuthenticator table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LegalAuthenticatorID =>
            LegalAuthenticator.TryGetValue("EncryptedLegalAuthenticatorID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Document.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            LegalAuthenticator.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Optional signing statement provided in [noteText].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? NoteText =>
            LegalAuthenticator.TryGetValue(nameof(NoteText), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Timestamp of the signature ([time value]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? TimeValue =>
            LegalAuthenticator.TryGetValue(nameof(TimeValue), out var value)
                ? value as DateTime?
                : null;

        /// <summary>
        /// The electronic signature text ([signatureText]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SignatureText =>
            LegalAuthenticator.TryGetValue(nameof(SignatureText), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Name of the person signing ([assignedPerson][name]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? AssignedPersonName =>
            LegalAuthenticator.TryGetValue(nameof(AssignedPersonName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Link to the signing Organization, used for FDA signers in Labeler Code Inactivation (Sec 5.1.6).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SignerOrganizationID =>
            LegalAuthenticator.TryGetValue("EncryptedSignerOrganizationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.License"/>
    public class LicenseDto
    {
        public required Dictionary<string, object?> License { get; set; }
        public List<DisciplinaryActionDto> DisciplinaryActions { get; set; } = new();

        public List<TerritorialAuthorityDto> TerritorialAuthorities { get; set; } = new();

        /// <summary>
        /// Primary key for the License table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LicenseID =>
            License.TryGetValue("EncryptedLicenseID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to BusinessOperation (The WDD/3PL operation being licensed).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? BusinessOperationID =>
            License.TryGetValue("EncryptedBusinessOperationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// The license number string.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? LicenseNumber =>
            License.TryGetValue(nameof(LicenseNumber), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The root OID identifying the issuing authority and context.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? LicenseRootOID =>
            License.TryGetValue(nameof(LicenseRootOID), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code indicating the type of approval/license (e.g., C118777 licensing).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? LicenseTypeCode =>
            License.TryGetValue(nameof(LicenseTypeCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for LicenseTypeCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? LicenseTypeCodeSystem =>
            License.TryGetValue(nameof(LicenseTypeCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for LicenseTypeCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? LicenseTypeDisplayName =>
            License.TryGetValue(nameof(LicenseTypeDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Status of the license: active, suspended, aborted (revoked), completed (expired).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StatusCode =>
            License.TryGetValue(nameof(StatusCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Expiration date of the license.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? ExpirationDate =>
            License.TryGetValue(nameof(ExpirationDate), out var value)
                ? value as DateTime?
                : null;

        /// <summary>
        /// Foreign key to TerritorialAuthority (Issuing state/agency).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? TerritorialAuthorityID =>
            License.TryGetValue("EncryptedTerritorialAuthorityID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.LotHierarchy"/>
    public class LotHierarchyDto
    {
        public required Dictionary<string, object?> LotHierarchy { get; set; }
        public ProductInstanceDto? ParentInstance { get; set; }
        public ProductInstanceDto? ChildInstance { get; set; }

        /// <summary>
        /// Primary key for the LotHierarchy table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LotHierarchyID =>
            LotHierarchy.TryGetValue("EncryptedLotHierarchyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to ProductInstance (The Fill Lot or Package Lot).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ParentInstanceID =>
            LotHierarchy.TryGetValue("EncryptedParentInstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to ProductInstance (The Label Lot which is a member).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ChildInstanceID =>
            LotHierarchy.TryGetValue("EncryptedChildInstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.LotIdentifier"/>
    public class LotIdentifierDto
    {
        public required Dictionary<string, object?> LotIdentifier { get; set; }

        /// <summary>
        /// Primary key for the LotIdentifier table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LotIdentifierID =>
            LotIdentifier.TryGetValue("EncryptedLotIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// The lot number string.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? LotNumber =>
            LotIdentifier.TryGetValue(nameof(LotNumber), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The computed globally unique root OID for the lot number.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? LotRootOID =>
            LotIdentifier.TryGetValue(nameof(LotRootOID), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.MarketingCategory"/>
    public class MarketingCategoryDto
    {
        public required Dictionary<string, object?> MarketingCategory { get; set; }

        /// <summary>
        /// Primary key for the MarketingCategory table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? MarketingCategoryID =>
            MarketingCategory.TryGetValue("EncryptedMarketingCategoryID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (or Product representing a Part). Null if linked via ProductConceptID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            MarketingCategory.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code identifying the marketing category (e.g., NDA, ANDA, OTC Monograph Drug).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CategoryCode =>
            MarketingCategory.TryGetValue(nameof(CategoryCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Marketing Category code system ([approval][code] codeSystem).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CategoryCodeSystem =>
            MarketingCategory.TryGetValue(nameof(CategoryCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Marketing Category display name ([approval][code] displayName).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CategoryDisplayName =>
            MarketingCategory.TryGetValue(nameof(CategoryDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Application number, monograph ID, or citation ([id extension]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ApplicationOrMonographIDValue =>
            MarketingCategory.TryGetValue(nameof(ApplicationOrMonographIDValue), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Root OID for the application number or monograph ID system ([id root]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ApplicationOrMonographIDOID =>
            MarketingCategory.TryGetValue(nameof(ApplicationOrMonographIDOID), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Date of application approval, if applicable ([effectiveTime][low value]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? ApprovalDate =>
            MarketingCategory.TryGetValue(nameof(ApprovalDate), out var value)
                ? value as DateTime?
                : null;

        /// <summary>
        /// Territory code, typically USA ([territory][code]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? TerritoryCode =>
            MarketingCategory.TryGetValue(nameof(TerritoryCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// FK to ProductConcept, used when the marketing category applies to an Application Product Concept instead of a concrete Product. Null if linked via ProductID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductConceptID =>
            MarketingCategory.TryGetValue("EncryptedProductConceptID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.MarketingStatus"/>
    public class MarketingStatusDto
    {
        public required Dictionary<string, object?> MarketingStatus { get; set; }

        /// <summary>
        /// Primary key for the MarketingStatus table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? MarketingStatusID =>
            MarketingStatus.TryGetValue("EncryptedMarketingStatusID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (if status applies to product).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            MarketingStatus.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to PackagingLevel (if status applies to a package).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PackagingLevelID =>
            MarketingStatus.TryGetValue("EncryptedPackagingLevelID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code for the marketing activity (e.g., C53292 Marketing, C96974 Drug Sample).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? MarketingActCode =>
            MarketingStatus.TryGetValue(nameof(MarketingActCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for MarketingActCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? MarketingActCodeSystem =>
            MarketingStatus.TryGetValue(nameof(MarketingActCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Status code: active, completed, new, cancelled.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StatusCode =>
            MarketingStatus.TryGetValue(nameof(StatusCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Marketing start date ([effectiveTime][low value]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? EffectiveStartDate =>
            MarketingStatus.TryGetValue(nameof(EffectiveStartDate), out var value)
                ? value as DateTime?
                : null;

        /// <summary>
        /// Marketing end date ([effectiveTime][high value]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? EffectiveEndDate =>
            MarketingStatus.TryGetValue(nameof(EffectiveEndDate), out var value)
                ? value as DateTime?
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.NamedEntity"/>
    public class NamedEntityDto
    {
        public required Dictionary<string, object?> NamedEntity { get; set; }

        /// <summary>
        /// Primary key for the NamedEntity table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? NamedEntityID =>
            NamedEntity.TryGetValue("EncryptedNamedEntityID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Organization (The facility).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? OrganizationID =>
            NamedEntity.TryGetValue("EncryptedOrganizationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code identifying the type of named entity, e.g., C117113 for "doing business as".
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EntityTypeCode =>
            NamedEntity.TryGetValue(nameof(EntityTypeCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for EntityTypeCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EntityTypeCodeSystem =>
            NamedEntity.TryGetValue(nameof(EntityTypeCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for EntityTypeCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EntityTypeDisplayName =>
            NamedEntity.TryGetValue(nameof(EntityTypeDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The name of the entity, e.g., the DBA name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EntityName =>
            NamedEntity.TryGetValue(nameof(EntityName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Optional suffix used with DBA names in WDD/3PL reports to indicate business type ([WDD] or [3PL]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EntitySuffix =>
            NamedEntity.TryGetValue(nameof(EntitySuffix), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.NCTLink"/>
    public class NCTLinkDto
    {
        public required Dictionary<string, object?> NCTLink { get; set; }

        /// <summary>
        /// Primary key for the NCTLink table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? NCTLinkID =>
            NCTLink.TryGetValue("EncryptedNCTLinkID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to the Indexing Section (48779-3).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            NCTLink.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// The National Clinical Trials number (id extension).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? NCTNumber =>
            NCTLink.TryGetValue(nameof(NCTNumber), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The root OID for NCT numbers (id root).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? NCTRootOID =>
            NCTLink.TryGetValue(nameof(NCTRootOID), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ObservationCriterion"/>
    public class ObservationCriterionDto
    {
        public required Dictionary<string, object?> ObservationCriterion { get; set; }
        public List<ApplicationTypeDto> ApplicationTypes { get; set; } = new();
        public List<CommodityDto> Commodities { get; set; } = new();

        /// <summary>
        /// Primary key for the ObservationCriterion table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ObservationCriterionID =>
            ObservationCriterion.TryGetValue("EncryptedObservationCriterionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to SubstanceSpecification.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SubstanceSpecificationID =>
            ObservationCriterion.TryGetValue("EncryptedSubstanceSpecificationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// The upper limit of the tolerance range in ppm.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? ToleranceHighValue =>
            ObservationCriterion.TryGetValue(nameof(ToleranceHighValue), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Tolerance unit ([value][high unit]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ToleranceHighUnit =>
            ObservationCriterion.TryGetValue(nameof(ToleranceHighUnit), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Optional link to the specific commodity the tolerance applies to.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? CommodityID =>
            ObservationCriterion.TryGetValue("EncryptedCommodityID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Link to the type of application associated with this tolerance.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ApplicationTypeID =>
            ObservationCriterion.TryGetValue("EncryptedApplicationTypeID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Optional expiration or revocation date for the tolerance.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? ExpirationDate =>
            ObservationCriterion.TryGetValue(nameof(ExpirationDate), out var value)
                ? value as DateTime?
                : null;

        /// <summary>
        /// Optional text annotation about the tolerance.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? TextNote =>
            ObservationCriterion.TryGetValue(nameof(TextNote), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ObservationMedia"/>
    public class ObservationMediaDto
    {
        public required Dictionary<string, object?> ObservationMedia { get; set; }
        public SectionDto? Section { get; set; }

        /// <summary>
        /// Primary key for the ObservationMedia table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ObservationMediaID =>
            ObservationMedia.TryGetValue("EncryptedObservationMediaID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Section (where the observationMedia is defined).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            ObservationMedia.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Identifier for the media object ([observationMedia ID=]), referenced by [renderMultimedia].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? MediaID =>
            ObservationMedia.TryGetValue("EncryptedMediaID", out var value)
                ? Util.DecryptAndParseString(value)
                : null;

        /// <summary>
        /// Text description of the image ([text] child of observationMedia), used by screen readers.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DescriptionText =>
            ObservationMedia.TryGetValue(nameof(DescriptionText), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Media type of the file ([value mediaType=]), e.g., image/jpeg.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? MediaType =>
            ObservationMedia.TryGetValue(nameof(MediaType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Xsi type of the file ([value xsi:type=]), e.g., "ED".
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? XsiType =>
            ObservationMedia.TryGetValue(nameof(XsiType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// File name of the image ([reference value=]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? FileName =>
            ObservationMedia.TryGetValue(nameof(FileName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Organization"/>
    public class OrganizationDto
    {
        public required Dictionary<string, object?> Organization { get; set; }
        public List<ContactPartyDto> ContactParties { get; set; } = new();
        public List<OrganizationTelecomDto> Telecoms { get; set; } = new();
        public List<DocumentAuthorDto> AuthoredDocuments { get; set; } = new();
        public List<DocumentRelationshipDto> ParentRelationships { get; set; } = new();
        public List<DocumentRelationshipDto> ChildRelationships { get; set; } = new();
        public List<OrganizationIdentifierDto> Identifiers { get; set; } = new();
        public List<LegalAuthenticatorDto> SignedDocuments { get; set; } = new();
        public List<NamedEntityDto> NamedEntities { get; set; } = new();
        public List<HolderDto> Holders { get; set; } = new();
        public List<IngredientInstanceDto> ManufacturedIngredientInstances { get; set; } = new();
        public List<ResponsiblePersonLinkDto> ResponsibleForProducts { get; set; } = new();

        /// <summary>
        /// Primary key for the Organization table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? OrganizationID =>
            Organization.TryGetValue("EncryptedOrganizationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Name of the organization ([name]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? OrganizationName =>
            Organization.TryGetValue(nameof(OrganizationName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Flag indicating if the organization information is confidential ([confidentialityCode code="B"]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool? IsConfidential =>
            Organization.TryGetValue(nameof(IsConfidential), out var value)
                ? value as bool?
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.OrganizationIdentifier"/>
    public class OrganizationIdentifierDto
    {
        public required Dictionary<string, object?> OrganizationIdentifier { get; set; }

        /// <summary>
        /// Primary key for the OrganizationIdentifier table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? OrganizationIdentifierID =>
            OrganizationIdentifier.TryGetValue("EncryptedOrganizationIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Organization.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? OrganizationID =>
            OrganizationIdentifier.TryGetValue("EncryptedOrganizationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// The identifier value ([id extension]) pg 14 2.1.4 Author Information.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierValue =>
            OrganizationIdentifier.TryGetValue(nameof(IdentifierValue), out var value)
                ? value as string
                : null;

        /// <summary>
        /// OID for the identifier system ([id root]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierSystemOID =>
            OrganizationIdentifier.TryGetValue(nameof(IdentifierSystemOID), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Type classification of the identifier based on the OID and context.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierType =>
            OrganizationIdentifier.TryGetValue(nameof(IdentifierType), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Characteristic"/>
    public class ProductCharacteristicDto
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Product characteristic data dictionary containing attribute information.
        /// Includes characteristic type, value, unit, and code information.
        /// </summary>
        /// <seealso cref="Label.Characteristic"/>
        public Dictionary<string, object?> ProductCharacteristic { get; set; } = new();

        #endregion
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductRouteOfAdministration"/>
    public class RouteDto
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Route data dictionary containing administration method information.
        /// Includes route code, code system, and display name.
        /// </summary>
        /// <seealso cref="Label.ProductRouteOfAdministration"/>
        public Dictionary<string, object?> Route { get; set; } = new();


        /// <summary>
        /// Primary key 
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductRouteOfAdministrationID =>
            Route.TryGetValue("EncryptedProductRouteOfAdministrationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key 
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            Route.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code identifying the route of administration.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RouteCode =>
            Route.TryGetValue(nameof(RouteCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for the route code (typically 2.16.840.1.113883.3.26.1.1).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RouteCodeSystem =>
            Route.TryGetValue(nameof(RouteCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name matching the route code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RouteDisplayName =>
            Route.TryGetValue(nameof(RouteDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Stores nullFlavor attribute value (e.g., NA) when route code is not applicable.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RouteNullFlavor =>
            Route.TryGetValue(nameof(RouteNullFlavor), out var value)
                ? value as string
                : null;
        #endregion
    }

    /**************************************************************/
    /// <seealso cref="Label.OrganizationTelecom"/>
    public class OrganizationTelecomDto
    {
        public required Dictionary<string, object?> OrganizationTelecom { get; set; }
        public OrganizationDto? Organization { get; set; }
        public TelecomDto? Telecom { get; set; }

        /// <summary>
        /// Primary key for the OrganizationTelecom table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? OrganizationTelecomID =>
            OrganizationTelecom.TryGetValue("EncryptedOrganizationTelecomID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Organization.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? OrganizationID =>
            OrganizationTelecom.TryGetValue("EncryptedOrganizationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Telecom.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? TelecomID =>
            OrganizationTelecom.TryGetValue("EncryptedTelecomID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.PackagingLevel"/>
    public class PackagingLevelDto
    {
        public required Dictionary<string, object?> PackagingLevel { get; set; }
        public List<PackagingHierarchyDto> PackagingHierarchy { get; set; } = new();
        public List<ProductEventDto> ProductEvents { get; set; } = new();
        public List<MarketingStatusDto> MarketingStatuses { get; set; } = new();
        public List<PackageIdentifierDto> PackageIdentifiers { get; set; } = new();

        /// <summary>
        /// Package-level characteristics such as container type, labeling information,
        /// or other properties specific to this packaging level.
        /// </summary>
        /// <seealso cref="Label.Characteristic"/>
        /// <seealso cref="CharacteristicDto"/>
        public List<CharacteristicDto> Characteristics { get; set; } = new();

        /// <summary>
        /// Primary key for the PackagingLevel table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PackagingLevelID =>
            PackagingLevel.TryGetValue("EncryptedPackagingLevelID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Link to Product table if this packaging directly contains the base manufactured product.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            PackagingLevel.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Link to Product table (representing a part) if this packaging contains a part of a kit.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PartProductID =>
            PackagingLevel.TryGetValue("EncryptedPartProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Quantity and unit of the item contained within this package level ([quantity]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? QuantityNumerator =>
            PackagingLevel.TryGetValue(nameof(QuantityNumerator), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Corresponds to [translation][numerator code].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? NumeratorTranslationCode =>
            PackagingLevel.TryGetValue(nameof(NumeratorTranslationCode), out var value)
                ? value as string
                : null;
        /// <summary>
        /// Corresponds to [translation][numerator codeSystem].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? NumeratorTranslationCodeSystem =>
            PackagingLevel.TryGetValue(nameof(NumeratorTranslationCodeSystem), out var value)
                ? value as string
                : null;
        /// <summary>
        /// Corresponds to [translation][displayName unit].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? NumeratorTranslationDisplayName =>
            PackagingLevel.TryGetValue(nameof(NumeratorTranslationDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Corresponds to [quantity][denominator value].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? QuantityDenominator =>
            PackagingLevel.TryGetValue(nameof(QuantityDenominator), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Corresponds to [quantity][numerator unit].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? QuantityNumeratorUnit =>
            PackagingLevel.TryGetValue(nameof(QuantityNumeratorUnit), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The package item code value ([containerPackagedProduct][code code="..." /]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PackageCode =>
            PackagingLevel.TryGetValue(nameof(PackageCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The code system OID for the package item code ([containerPackagedProduct][code codeSystem="..." /]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PackageCodeSystem =>
            PackagingLevel.TryGetValue(nameof(PackageCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Package type code, system, and display name ([containerPackagedProduct][formCode]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PackageFormCode =>
            PackagingLevel.TryGetValue(nameof(PackageFormCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Corresponds to [containerPackagedProduct][formCode codeSystem].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PackageFormCodeSystem =>
            PackagingLevel.TryGetValue(nameof(PackageFormCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Corresponds to [containerPackagedProduct][formCode displayName].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PackageFormDisplayName =>
            PackagingLevel.TryGetValue(nameof(PackageFormDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// FK to ProductInstance, used when the packaging details describe a container linked to a specific Label Lot instance (Lot Distribution).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductInstanceID =>
            PackagingLevel.TryGetValue("EncryptedProductInstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.PackageIdentifier"/>
    public class PackageIdentifierDto
    {
        public required Dictionary<string, object?> PackageIdentifier { get; set; }
        public List<ComplianceActionDto> ComplianceActions { get; set; } = new();

        /// <summary>
        /// Primary key for the PackageIdentifier table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PackageIdentifierID =>
            PackageIdentifier.TryGetValue("EncryptedPackageIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to PackagingLevel.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PackagingLevelID =>
            PackageIdentifier.TryGetValue("EncryptedPackagingLevelID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// The package item code value ([containerPackagedProduct][code] code).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierValue =>
            PackageIdentifier.TryGetValue(nameof(IdentifierValue), out var value)
                ? value as string
                : null;

        /// <summary>
        /// OID for the package identifier system ([containerPackagedProduct][code] codeSystem).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierSystemOID =>
            PackageIdentifier.TryGetValue(nameof(IdentifierSystemOID), out var value)
                ? value as string
                : null;

        /// <summary>
        /// e.g., 'NDCPackage', 'NHRICPackage', 'GS1Package', 'HIBCCPackage', 'ISBTPackage'.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierType =>
            PackageIdentifier.TryGetValue(nameof(IdentifierType), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.PackagingHierarchy"/>
    public class PackagingHierarchyDto
    {
        public required Dictionary<string, object?> PackagingHierarchy { get; set; }

        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="Label.PackagingLevel"/>
        public PackagingLevelDto? ChildPackagingLevel { get; set; }

        /// <summary>
        /// Primary key for the PackagingHierarchy table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PackagingHierarchyID =>
            PackagingHierarchy.TryGetValue("EncryptedPackagingHierarchyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to PackagingLevel (The containing package).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? OuterPackagingLevelID =>
            PackagingHierarchy.TryGetValue("EncryptedOuterPackagingLevelID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to PackagingLevel (The contained package).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? InnerPackagingLevelID =>
            PackagingHierarchy.TryGetValue("EncryptedInnerPackagingLevelID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Order of inner package within outer package (if multiple identical inner packages).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SequenceNumber =>
            PackagingHierarchy.TryGetValue(nameof(SequenceNumber), out var value)
                ? value as int?
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.PartOfAssembly"/>
    public class PartOfAssemblyDto
    {
        public required Dictionary<string, object?> PartOfAssembly { get; set; }

        /// <summary>
        /// Primary key for the PartOfAssembly table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PartOfAssemblyID =>
            PartOfAssembly.TryGetValue("EncryptedPartOfAssemblyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (The product being described that is part of the assembly).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PrimaryProductID =>
            PartOfAssembly.TryGetValue("EncryptedPrimaryProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (The other product in the assembly, referenced via [part][partProduct] inside [asPartOfAssembly]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? AccessoryProductID =>
            PartOfAssembly.TryGetValue("EncryptedAccessoryProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.PharmacologicClass"/>
    public class PharmacologicClassDto
    {
        public required Dictionary<string, object?> PharmacologicClass { get; set; }
        public List<PharmacologicClassNameDto> PharmacologicClassNames { get; set; } = new();
        public List<PharmacologicClassLinkDto> PharmacologicClassLinks { get; set; } = new();
        public List<PharmacologicClassHierarchyDto> PharmacologicClassHierarchies { get; set; } = new();

        /// <summary>
        /// Primary key for the PharmacologicClass table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassID =>
            PharmacologicClass.TryGetValue("EncryptedPharmacologicClassID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to IdentifiedSubstance (where IsDefinition=1 and SubjectType='PharmacologicClass').
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IdentifiedSubstanceID =>
            PharmacologicClass.TryGetValue("EncryptedIdentifiedSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// The MED-RT or MeSH code for the pharmacologic class.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ClassCode =>
            PharmacologicClass.TryGetValue(nameof(ClassCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system ([code] codeSystem).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ClassCodeSystem =>
            PharmacologicClass.TryGetValue(nameof(ClassCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The display name for the class code, including the type suffix like [EPC] or [CS].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ClassDisplayName =>
            PharmacologicClass.TryGetValue(nameof(ClassDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.PharmacologicClassName"/>
    public class PharmacologicClassNameDto
    {
        public required Dictionary<string, object?> PharmacologicClassName { get; set; }

        /// <summary>
        /// Primary key for the PharmacologicClassName table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassNameID =>
            PharmacologicClassName.TryGetValue("EncryptedPharmacologicClassNameID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to PharmacologicClass.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassID =>
            PharmacologicClassName.TryGetValue("EncryptedPharmacologicClassID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// The text of the preferred or alternate name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? NameValue =>
            PharmacologicClassName.TryGetValue(nameof(NameValue), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Indicates if the name is preferred (L) or alternate (A).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? NameUse =>
            PharmacologicClassName.TryGetValue(nameof(NameUse), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.PharmacologicClassLink"/>
    public class PharmacologicClassLinkDto
    {
        public required Dictionary<string, object?> PharmacologicClassLink { get; set; }

        /// <summary>
        /// Primary key for the PharmacologicClassLink table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassLinkID =>
            PharmacologicClassLink.TryGetValue("EncryptedPharmacologicClassLinkID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to IdentifiedSubstance (where SubjectType='ActiveMoiety').
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ActiveMoietySubstanceID =>
            PharmacologicClassLink.TryGetValue("EncryptedActiveMoietySubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to PharmacologicClass.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassID =>
            PharmacologicClassLink.TryGetValue("EncryptedPharmacologicClassID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.PharmacologicClassHierarchy"/>
    public class PharmacologicClassHierarchyDto
    {
        public required Dictionary<string, object?> PharmacologicClassHierarchy { get; set; }

        /// <summary>
        /// Primary key for the PharmacologicClassHierarchy table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PharmacologicClassHierarchyID =>
            PharmacologicClassHierarchy.TryGetValue("EncryptedPharmacologicClassHierarchyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to PharmacologicClass (The class being defined).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ChildPharmacologicClassID =>
            PharmacologicClassHierarchy.TryGetValue("EncryptedChildPharmacologicClassID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to PharmacologicClass (The super-class).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ParentPharmacologicClassID =>
            PharmacologicClassHierarchy.TryGetValue("EncryptedParentPharmacologicClassID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Policy"/>
    public class PolicyDto
    {
        public required Dictionary<string, object?> Policy { get; set; }

        /// <summary>
        /// Primary key for the Policy table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PolicyID =>
            Policy.TryGetValue("EncryptedPolicyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            Policy.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Class code for the policy, e.g., DEADrugSchedule.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PolicyClassCode =>
            Policy.TryGetValue(nameof(PolicyClassCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code representing the specific policy value (e.g., DEA Schedule C-II).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PolicyCode =>
            Policy.TryGetValue(nameof(PolicyCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for the policy code (e.g., 2.16.840.1.113883.3.26.1.1 for DEA schedule).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PolicyCodeSystem =>
            Policy.TryGetValue(nameof(PolicyCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name matching the policy code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PolicyDisplayName =>
            Policy.TryGetValue(nameof(PolicyDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductConcept"/>
    public class ProductConceptDto
    {
        public required Dictionary<string, object?> ProductConcept { get; set; }
        public List<ProductConceptEquivalenceDto> ProductConceptEquivalences { get; set; } = new();

        /// <summary>
        /// Primary key for the ProductConcept table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductConceptID =>
            ProductConcept.TryGetValue("EncryptedProductConceptID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to the Indexing Section (48779-3).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            ProductConcept.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// The computed MD5 hash code identifying the product concept ([code] code).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ConceptCode =>
            ProductConcept.TryGetValue(nameof(ConceptCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// OID for Product Concept Codes.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ConceptCodeSystem =>
            ProductConcept.TryGetValue(nameof(ConceptCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Distinguishes Abstract Product/Kit concepts from Application-specific Product/Kit concepts.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ConceptType =>
            ProductConcept.TryGetValue(nameof(ConceptType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Dosage Form details, applicable only for Abstract Product concepts.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? FormCode =>
            ProductConcept.TryGetValue(nameof(FormCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for FormCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? FormCodeSystem =>
            ProductConcept.TryGetValue(nameof(FormCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for FormCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? FormDisplayName =>
            ProductConcept.TryGetValue(nameof(FormDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductConcept"/>
    public class ProductConceptEquivalenceDto
    {
        public required Dictionary<string, object?> ProductConcept { get; set; }

        /// <summary>
        /// Primary key for the ProductConceptEquivalence table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductConceptEquivalenceID =>
            ProductConcept.TryGetValue("EncryptedProductConceptEquivalenceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to ProductConcept (The Application concept).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ApplicationProductConceptID =>
            ProductConcept.TryGetValue("EncryptedApplicationProductConceptID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to ProductConcept (The Abstract concept it derives from).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? AbstractProductConceptID =>
            ProductConcept.TryGetValue("EncryptedAbstractProductConceptID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code indicating the relationship type between Application and Abstract concepts (A, B, OTC, N).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EquivalenceCode =>
            ProductConcept.TryGetValue(nameof(EquivalenceCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// OID for this code system.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EquivalenceCodeSystem =>
            ProductConcept.TryGetValue(nameof(EquivalenceCodeSystem), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Product"/>
    public class ProductDto
    {
        public required Dictionary<string, object?> Product { get; set; }
        public SectionDto? Section { get; set; }
        public List<ProductPartDto> ProductParts { get; set; } = new();
        public List<GenericMedicineDto> GenericMedicines { get; set; } = new();
        public List<ProductIdentifierDto> ProductIdentifiers { get; set; } = new();
        public List<PackageIdentifierDto> PackageIdentifiers { get; set; } = new();
        public List<ProductInstanceDto> ProductInstances { get; set; } = new();
        public List<ProductRouteOfAdministrationDto> ProductRouteOfAdministrations { get; set; } = new();
        public List<ProductWebLinkDto> ProductWebLinks { get; set; } = new();
        public List<FacilityProductLinkDto> FacilityProductLinks { get; set; } = new();
        public List<IngredientDto> Ingredients { get; set; } = new();
        public List<IngredientInstanceDto> IngredientInstances { get; set; } = new();
        public List<IngredientSourceProductDto> IngredientSourceProducts { get; set; } = new();
        public List<BusinessOperationProductLinkDto> BusinessOperationProductLinks { get; set; } = new();
        public List<CertificationProductLinkDto> CertificationProductLinks { get; set; } = new();
        public List<ResponsiblePersonLinkDto> ResponsiblePersonLinks { get; set; } = new();
        public List<ReferenceSubstanceDto> ReferenceSubstances { get; set; } = new();
        public List<AnalyteDto> Analytes { get; set; } = new();
        public List<LotIdentifierDto> LotIdentifiers { get; set; } = new();
        public List<PackagingHierarchyDto> PackagingHierarchies { get; set; } = new();
        public List<MarketingCategoryDto> MarketingCategories { get; set; } = new();
        public List<MarketingStatusDto> MarketingStatuses { get; set; } = new();
        public List<PackagingLevelDto> PackagingLevels { get; set; } = new();
        public List<LotHierarchyDto> ParentLotHierarchies { get; set; } = new();
        public List<LotHierarchyDto> ChildLotHierarchies { get; set; } = new();
        public List<CharacteristicDto> Characteristics { get; set; } = new();
        public List<AdditionalIdentifierDto> AdditionalIdentifiers { get; set; } = new();
        public List<DosingSpecificationDto> DosingSpecifications { get; set; } = new();
        public List<EquivalentEntityDto> EquivalentEntities { get; set; } = new();
        public List<PartOfAssemblyDto> PartOfAssemblies { get; set; } = new();
        public List<PolicyDto> Policies { get; set; } = new();
        public List<SpecializedKindDto> SpecializedKinds { get; set; } = new();
        public List<RouteDto> Routes { get; set; } = new();
        public List<ProductCharacteristicDto> ProductCharacteristics { get; set; } = new();

        /// <summary>
        /// Primary key for the Product table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            Product.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Section (if product defined in a section).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            Product.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Proprietary name or product name ([name]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            Product.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Suffix to the proprietary name ([suffix]), e.g., "XR".
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductSuffix =>
            Product.TryGetValue(nameof(ProductSuffix), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Dosage form code, system, and display name ([formCode]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? FormCode =>
            Product.TryGetValue(nameof(FormCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Corresponds to [formCode codeSystem].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? FormCodeSystem =>
            Product.TryGetValue(nameof(FormCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Corresponds to [formCode displayName].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? FormDisplayName =>
            Product.TryGetValue(nameof(FormDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Brief description of the product ([desc]), mainly used for devices.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? DescriptionText =>
            Product.TryGetValue(nameof(DescriptionText), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductEvent"/>
    public class ProductEventDto
    {
        public required Dictionary<string, object?> ProductEvent { get; set; }

        /// <summary>
        /// Primary key for the ProductEvent table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductEventID =>
            ProductEvent.TryGetValue("EncryptedProductEventID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to PackagingLevel (The container level the event applies to).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PackagingLevelID =>
            ProductEvent.TryGetValue("EncryptedPackagingLevelID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code identifying the type of event (e.g., C106325 Distributed, C106328 Returned).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EventCode =>
            ProductEvent.TryGetValue(nameof(EventCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for EventCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EventCodeSystem =>
            ProductEvent.TryGetValue(nameof(EventCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for EventCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EventDisplayName =>
            ProductEvent.TryGetValue(nameof(EventDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Integer quantity associated with the event (e.g., number of containers distributed/returned).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? QuantityValue =>
            ProductEvent.TryGetValue(nameof(QuantityValue), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Unit for quantity (usually '1' or null).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? QuantityUnit =>
            ProductEvent.TryGetValue(nameof(QuantityUnit), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Effective date (low value), used for Initial Distribution Date.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? EffectiveTimeLow =>
            ProductEvent.TryGetValue(nameof(EffectiveTimeLow), out var value)
                ? value as DateTime?
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductIdentifier"/>
    public class ProductIdentifierDto
    {
        public required Dictionary<string, object?> ProductIdentifier { get; set; }

        /// <summary>
        /// Primary key for the ProductIdentifier table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductIdentifierID =>
            ProductIdentifier.TryGetValue("EncryptedProductIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            ProductIdentifier.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// The item code value ([code code=]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierValue =>
            ProductIdentifier.TryGetValue(nameof(IdentifierValue), out var value)
                ? value as string
                : null;

        /// <summary>
        /// OID for the identifier system ([code codeSystem=]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierSystemOID =>
            ProductIdentifier.TryGetValue(nameof(IdentifierSystemOID), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Type classification of the identifier based on the OID.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? IdentifierType =>
            ProductIdentifier.TryGetValue(nameof(IdentifierType), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductInstance"/>
    public class ProductInstanceDto
    {
        public required Dictionary<string, object?> ProductInstance { get; set; }
        public LotIdentifierDto? LotIdentifier { get; set; }
        public List<LotHierarchyDto> ParentHierarchies { get; set; } = new();
        public List<LotHierarchyDto> ChildHierarchies { get; set; } = new();
        public List<PackagingLevelDto> PackagingLevels { get; set; } = new();

        /// <summary>
        /// Primary key for the ProductInstance table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductInstanceID =>
            ProductInstance.TryGetValue("EncryptedProductInstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (The product definition this is an instance of).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            ProductInstance.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Type of lot instance: FillLot, LabelLot, PackageLot (for kits), SalvagedLot.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? InstanceType =>
            ProductInstance.TryGetValue(nameof(InstanceType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Foreign key to LotIdentifier.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? LotIdentifierID =>
            ProductInstance.TryGetValue("EncryptedLotIdentifierID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Expiration date, typically for Label Lots.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? ExpirationDate =>
            ProductInstance.TryGetValue(nameof(ExpirationDate), out var value)
                ? value as DateTime?
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductPart"/>
    public class ProductPartDto
    {
        public required Dictionary<string, object?> ProductPart { get; set; }
        public ProductDto? KitProduct { get; set; }

        /// <summary>
        /// Primary key for the ProductPart table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductPartID =>
            ProductPart.TryGetValue("EncryptedProductPartID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (The parent Kit product).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? KitProductID =>
            ProductPart.TryGetValue("EncryptedKitProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (The product representing the part).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? PartProductID =>
            ProductPart.TryGetValue("EncryptedPartProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Quantity and unit of this part contained within the parent kit product ([part][quantity]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? PartQuantityNumerator =>
            ProductPart.TryGetValue(nameof(PartQuantityNumerator), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Unit for the part quantity ([quantity][numerator unit]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PartQuantityNumeratorUnit =>
            ProductPart.TryGetValue(nameof(PartQuantityNumeratorUnit), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductRouteOfAdministration"/>
    public class ProductRouteOfAdministrationDto
    {
        public required Dictionary<string, object?> ProductRouteOfAdministration { get; set; }

        /// <summary>
        /// Primary key for the ProductRouteOfAdministration table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductRouteOfAdministrationID =>
            ProductRouteOfAdministration.TryGetValue("EncryptedProductRouteOfAdministrationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (or Product representing a Part).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            ProductRouteOfAdministration.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code identifying the route of administration.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RouteCode =>
            ProductRouteOfAdministration.TryGetValue(nameof(RouteCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for the route code (typically 2.16.840.1.113883.3.26.1.1).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RouteCodeSystem =>
            ProductRouteOfAdministration.TryGetValue(nameof(RouteCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name matching the route code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RouteDisplayName =>
            ProductRouteOfAdministration.TryGetValue(nameof(RouteDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Stores nullFlavor attribute value (e.g., NA) when route code is not applicable.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RouteNullFlavor =>
            ProductRouteOfAdministration.TryGetValue(nameof(RouteNullFlavor), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductWebLink"/>
    public class ProductWebLinkDto
    {
        public required Dictionary<string, object?> ProductWebLink { get; set; }

        /// <summary>
        /// Primary key for the ProductWebLink table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductWebLinkID =>
            ProductWebLink.TryGetValue("EncryptedProductWebLinkID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            ProductWebLink.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Absolute URL for the product web page, starting with http:// or https://.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? WebURL =>
            ProductWebLink.TryGetValue(nameof(WebURL), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Protocol"/>
    public class ProtocolDto
    {
        public required Dictionary<string, object?> Protocol { get; set; }
        public List<REMSApprovalDto> REMSApprovals { get; set; } = new();
        public List<RequirementDto> Requirements { get; set; } = new();

        /// <summary>
        /// Primary key for the Protocol table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProtocolID =>
            Protocol.TryGetValue("EncryptedProtocolID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to the Section containing the protocol definition.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            Protocol.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Protocol code with automatic REMS validation.
        /// The REMSProtocolCodeValidation attribute will validate:
        /// - Code is not null/empty
        /// - Code system is FDA SPL system (2.16.840.1.113883.3.26.1.1)
        /// - Code contains only alphanumeric characters
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProtocolCode =>
            Protocol.TryGetValue(nameof(ProtocolCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for ProtocolCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProtocolCodeSystem =>
            Protocol.TryGetValue(nameof(ProtocolCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for ProtocolCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProtocolDisplayName =>
            Protocol.TryGetValue(nameof(ProtocolDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ReferenceSubstance"/>
    public class ReferenceSubstanceDto
    {
        public required Dictionary<string, object?> ReferenceSubstance { get; set; }

        /// <summary>
        /// Primary key for the ReferenceSubstance table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ReferenceSubstanceID =>
            ReferenceSubstance.TryGetValue("EncryptedReferenceSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to IngredientSubstance (The parent substance).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IngredientSubstanceID =>
            ReferenceSubstance.TryGetValue("EncryptedIngredientSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// UNII code of the reference substance ([definingSubstance][code] code).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RefSubstanceUNII =>
            ReferenceSubstance.TryGetValue(nameof(RefSubstanceUNII), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Name of the reference substance ([definingSubstance][name]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RefSubstanceName =>
            ReferenceSubstance.TryGetValue(nameof(RefSubstanceName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.RelatedDocument"/>
    public class RelatedDocumentDto
    {
        public required Dictionary<string, object?> RelatedDocument { get; set; }
        public DocumentDto? SourceDocument { get; set; }

        /// <summary>
        /// Primary key for the RelatedDocument table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? RelatedDocumentID =>
            RelatedDocument.TryGetValue("EncryptedRelatedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Document (The document containing the reference).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SourceDocumentID =>
            RelatedDocument.TryGetValue("EncryptedSourceDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code indicating the type of relationship (e.g., APND for core doc, RPLC for predecessor, DRIV for reference labeling, SUBJ for subject, XCRPT for excerpt).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RelationshipTypeCode =>
            RelatedDocument.TryGetValue(nameof(RelationshipTypeCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Set GUID of the related/referenced document ([setId root]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? ReferencedSetGUID =>
            RelatedDocument.TryGetValue(nameof(ReferencedSetGUID), out var value)
                ? value as Guid?
                : null;

        /// <summary>
        /// Document GUID of the related/referenced document ([id root]), used for RPLC relationship.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? ReferencedDocumentGUID =>
            RelatedDocument.TryGetValue(nameof(ReferencedDocumentGUID), out var value)
                ? value as Guid?
                : null;

        /// <summary>
        /// Version number of the related/referenced document ([versionNumber value]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ReferencedVersionNumber =>
            RelatedDocument.TryGetValue(nameof(ReferencedVersionNumber), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Document type code, system, and display name of the related/referenced document ([code]), used for RPLC relationship.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ReferencedDocumentCode =>
            RelatedDocument.TryGetValue(nameof(ReferencedDocumentCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Corresponds to [code] codeSystem of the referenced document (used in RPLC).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ReferencedDocumentCodeSystem =>
            RelatedDocument.TryGetValue(nameof(ReferencedDocumentCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Corresponds to [code] displayName of the referenced document (used in RPLC).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ReferencedDocumentDisplayName =>
            RelatedDocument.TryGetValue(nameof(ReferencedDocumentDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.REMSApproval"/>
    public class REMSApprovalDto
    {
        public required Dictionary<string, object?> REMSApproval { get; set; }

        /// <summary>
        /// Primary key for the REMSApproval table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? REMSApprovalID =>
            REMSApproval.TryGetValue("EncryptedREMSApprovalID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to the first Protocol defined in the document.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProtocolID =>
            REMSApproval.TryGetValue("EncryptedProtocolID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code for REMS Approval (C128899).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ApprovalCode =>
            REMSApproval.TryGetValue(nameof(ApprovalCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for ApprovalCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ApprovalCodeSystem =>
            REMSApproval.TryGetValue(nameof(ApprovalCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for ApprovalCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ApprovalDisplayName =>
            REMSApproval.TryGetValue(nameof(ApprovalDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Date of the initial REMS program approval.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? ApprovalDate =>
            REMSApproval.TryGetValue(nameof(ApprovalDate), out var value)
                ? value as DateTime?
                : null;

        /// <summary>
        /// Territory code ('USA').
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? TerritoryCode =>
            REMSApproval.TryGetValue(nameof(TerritoryCode), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.REMSElectronicResource"/>
    public class REMSElectronicResourceDto
    {
        public required Dictionary<string, object?> REMSElectronicResource { get; set; }

        /// <summary>
        /// Primary key for the REMSElectronicResource table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? REMSElectronicResourceID =>
            REMSElectronicResource.TryGetValue("EncryptedREMSElectronicResourceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to the REMS Material Section (82346-8) where resource is listed.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            REMSElectronicResource.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Unique identifier for this specific electronic resource reference.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? ResourceDocumentGUID =>
            REMSElectronicResource.TryGetValue(nameof(ResourceDocumentGUID), out var value)
                ? value as Guid?
                : null;

        /// <summary>
        /// Title of the resource ([document][title]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? Title =>
            REMSElectronicResource.TryGetValue(nameof(Title), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Internal link ID (#...) embedded within the title, potentially linking to descriptive text.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? TitleReference =>
            REMSElectronicResource.TryGetValue(nameof(TitleReference), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The URI (URL or URN) of the electronic resource.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ResourceReferenceValue =>
            REMSElectronicResource.TryGetValue(nameof(ResourceReferenceValue), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.REMSMaterial"/>
    public class REMSMaterialDto
    {
        public required Dictionary<string, object?> REMSMaterial { get; set; }
        public List<AttachedDocumentDto> AttachedDocuments { get; set; } = new();

        /// <summary>
        /// Primary key for the REMSMaterial table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? REMSMaterialID =>
            REMSMaterial.TryGetValue("EncryptedREMSMaterialID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to the REMS Material Section (82346-8).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            REMSMaterial.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Unique identifier for this specific material document reference.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? MaterialDocumentGUID =>
            REMSMaterial.TryGetValue(nameof(MaterialDocumentGUID), out var value)
                ? value as Guid?
                : null;

        /// <summary>
        /// Title of the material ([document][title]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? Title =>
            REMSMaterial.TryGetValue(nameof(Title), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Internal link ID (#...) embedded within the title, potentially linking to descriptive text.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? TitleReference =>
            REMSMaterial.TryGetValue(nameof(TitleReference), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Link to the AttachedDocument table if the material is provided as an attachment (e.g., PDF).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? AttachedDocumentID =>
            REMSMaterial.TryGetValue("EncryptedAttachedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.RenderedMedia"/>
    public class RenderedMediaDto
    {
        public required Dictionary<string, object?> RenderedMedia { get; set; }

        /// <summary>
        /// Primary key for the RenderedMedia table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? RenderedMediaID =>
            RenderedMedia.TryGetValue("EncryptedRenderedMediaID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to SectionTextContent (Paragraph or BlockImage).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionTextContentID =>
            RenderedMedia.TryGetValue("EncryptedSectionTextContentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Link to the ObservationMedia containing the image details, via the referencedObject attribute.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ObservationMediaID =>
            RenderedMedia.TryGetValue("EncryptedObservationMediaID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Order if multiple images are in one content block.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SequenceInContent =>
            RenderedMedia.TryGetValue(nameof(SequenceInContent), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Indicates if the image is inline (within a paragraph) or block level (direct child of [text]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool? IsInline =>
            RenderedMedia.TryGetValue(nameof(IsInline), out var value)
                ? value as bool?
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Requirement"/>
    public class RequirementDto
    {
        public required Dictionary<string, object?> Requirement { get; set; }
        public List<StakeholderDto> Stakeholders { get; set; } = new();

        /// <summary>
        /// Primary key for the Requirement table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? RequirementID =>
            Requirement.TryGetValue("EncryptedRequirementID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Protocol.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProtocolID =>
            Requirement.TryGetValue("EncryptedProtocolID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Sequence number relative to the substance administration step (fixed at 2). 1=Before, 2=During/Concurrent, 3=After.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? RequirementSequenceNumber =>
            Requirement.TryGetValue(nameof(RequirementSequenceNumber), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Flag: True if [monitoringObservation], False if [requirement].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public bool? IsMonitoringObservation =>
            Requirement.TryGetValue(nameof(IsMonitoringObservation), out var value)
                ? value as bool?
                : null;

        /// <summary>
        /// Optional delay (pause) relative to the start/end of the previous step.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? PauseQuantityValue =>
            Requirement.TryGetValue(nameof(PauseQuantityValue), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Optional delay unit ([pauseQuantity unit]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PauseQuantityUnit =>
            Requirement.TryGetValue(nameof(PauseQuantityUnit), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code identifying the specific requirement or monitoring observation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RequirementCode =>
            Requirement.TryGetValue(nameof(RequirementCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for RequirementCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RequirementCodeSystem =>
            Requirement.TryGetValue(nameof(RequirementCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for RequirementCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RequirementDisplayName =>
            Requirement.TryGetValue(nameof(RequirementDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Link ID (#...) pointing to the corresponding text description in the REMS Summary or REMS Participant Requirements section.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? OriginalTextReference =>
            Requirement.TryGetValue(nameof(OriginalTextReference), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Optional repetition period for the requirement/observation.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public decimal? PeriodValue =>
            Requirement.TryGetValue(nameof(PeriodValue), out var value)
                ? value as decimal?
                : null;

        /// <summary>
        /// Optional repetition period unit ([effectiveTime][period unit]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? PeriodUnit =>
            Requirement.TryGetValue(nameof(PeriodUnit), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Link to the stakeholder responsible for fulfilling the requirement.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? StakeholderID =>
            Requirement.TryGetValue("EncryptedStakeholderID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Optional link to a REMS Material document referenced by the requirement.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? REMSMaterialID =>
            Requirement.TryGetValue("EncryptedREMSMaterialID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.ResponsiblePersonLink"/>
    public class ResponsiblePersonLinkDto
    {
        public required Dictionary<string, object?> ResponsiblePersonLink { get; set; }

        /// <summary>
        /// Primary key for the ResponsiblePersonLink table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ResponsiblePersonLinkID =>
            ResponsiblePersonLink.TryGetValue("EncryptedResponsiblePersonLinkID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product (The cosmetic product listed in the Facility Reg doc).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            ResponsiblePersonLink.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Organization (The responsible person organization).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ResponsiblePersonOrgID =>
            ResponsiblePersonLink.TryGetValue("EncryptedResponsiblePersonOrgID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Section"/>
    public class SectionDto
    {

        private readonly string _pkSecret;

        public required Dictionary<string, object?> Section { get; set; }
        public StructuredBodyDto? StructuredBody { get; set; }
        public List<SectionHierarchyDto> ParentSectionHierarchies { get; set; } = new();
        public List<SectionHierarchyDto> ChildSectionHierarchies { get; set; } = new();
        public List<SectionTextContentDto> TextContents { get; set; } = new();
        public List<ObservationMediaDto> ObservationMedia { get; set; } = new();
        public List<SectionExcerptHighlightDto> ExcerptHighlights { get; set; } = new();
        public List<ProductDto> Products { get; set; } = new();
        public List<IdentifiedSubstanceDto> IdentifiedSubstances { get; set; } = new();
        public List<ProductConceptDto> ProductConcepts { get; set; } = new();
        public List<InteractionIssueDto> InteractionIssues { get; set; } = new();
        public List<BillingUnitIndexDto> BillingUnitIndexes { get; set; } = new();
        public List<WarningLetterProductInfoDto> WarningLetterProductInfos { get; set; } = new();
        public List<WarningLetterDateDto> WarningLetterDates { get; set; } = new();
        public List<ProtocolDto> Protocols { get; set; } = new();
        public List<REMSMaterialDto> REMSMaterials { get; set; } = new();
        public List<REMSElectronicResourceDto> REMSElectronicResources { get; set; } = new();
        public List<NCTLinkDto> NCTLinks { get; set; } = new();

        /// <summary>
        /// Primary key for the Section table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            Section.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to StructuredBody (for top-level sections).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? StructuredBodyID =>
            Section.TryGetValue("EncryptedStructuredBodyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Document (for top-level sections).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            Section.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Attribute identifying the section link ([section][ID]), used for 
        /// cross-references within the document e.g. [section ID="ID_1dc7080f-1d52-4bf7-b353-3c13ec291810"]
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionLinkGUID =>
            Section.TryGetValue(nameof(SectionLinkGUID), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Unique identifier for the section ([id root]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public Guid? SectionGUID =>
            Section.TryGetValue(nameof(SectionGUID), out var value)
                ? value as Guid?
                : null;

        /// <summary>
        /// LOINC code for the section type ([code] code).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionCode =>
            Section.TryGetValue(nameof(SectionCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for the section code ([code] codeSystem), typically 2.16.840.1.113883.6.1.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionCodeSystem =>
            Section.TryGetValue(nameof(SectionCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for the section code ([code] codeSystemName), typically LOINC
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionCodeSystemName =>
            Section.TryGetValue(nameof(SectionCodeSystemName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name matching the section code ([code] displayName).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionDisplayName =>
            Section.TryGetValue(nameof(SectionDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Title of the section ([title]), may include numbering.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? Title =>
            Section.TryGetValue(nameof(Title), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Effective time for the section ([effectiveTime value]). For Compounded Drug Labels (Sec 4.2.2), low/high represent the reporting period.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? EffectiveTime =>
            Section.TryGetValue(nameof(EffectiveTime), out var value)
                ? value as DateTime?
                : null;

        /// <summary>
        /// Low boundary of the effective time period for the section ([effectiveTime][low value]).
        /// Used for reporting periods and date ranges, particularly in Warning Letters and Compounded Drug Labels.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? EffectiveTimeLow =>
            Section.TryGetValue(nameof(EffectiveTimeLow), out var value)
                ? value as DateTime?
                : null;

        /// <summary>
        /// High boundary of the effective time period for the section ([effectiveTime][high value]).
        /// Used for reporting periods and date ranges, particularly in Warning Letters and Compounded Drug Labels.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? EffectiveTimeHigh =>
            Section.TryGetValue(nameof(EffectiveTimeHigh), out var value)
                ? value as DateTime?
                : null;

        public SectionDto()
        {
            // Default constructor initializes with no encryption secret
            _pkSecret = string.Empty;
        }
        public SectionDto(string pkSecret)
        {
            // Initialize with the provided encryption secret for SectionHierarchyID decryption
            _pkSecret = pkSecret ?? throw new ArgumentNullException(nameof(pkSecret), "PK encryption secret cannot be null");
        }
        public SectionDto(IConfiguration? configuration)
        {
            // Initialize with the encryption secret for SectionHierarchyID decryption
            _pkSecret = configuration?.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException("PK encryption secret not configured");

        }
    }

    /**************************************************************/
    /// <seealso cref="Label.SectionExcerptHighlight"/>
    public class SectionExcerptHighlightDto
    {
        public required Dictionary<string, object?> SectionExcerptHighlight { get; set; }
        public SectionDto? Section { get; set; }

        /// <summary>
        /// Primary key for the SectionExcerptHighlight table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionExcerptHighlightID =>
            SectionExcerptHighlight.TryGetValue("EncryptedSectionExcerptHighlightID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Section (The section containing the excerpt/highlight).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            SectionExcerptHighlight.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Text content from [excerpt][highlight][text].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? HighlightText =>
            SectionExcerptHighlight.TryGetValue(nameof(HighlightText), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.SectionHierarchy"/>
    public class SectionHierarchyDto
    {
        private readonly string _pkSecret;
        public required Dictionary<string, object?> SectionHierarchy { get; set; }

        /// <summary>
        /// Primary key for the SectionHierarchy table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionHierarchyID =>
            SectionHierarchy.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Section (The parent section).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ParentSectionID =>
            SectionHierarchy.TryGetValue("EncryptedParentSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Section (The child/nested section).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ChildSectionID =>
            SectionHierarchy.TryGetValue("EncryptedChildSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Order of the child section within the parent.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SequenceNumber =>
            SectionHierarchy.TryGetValue(nameof(SequenceNumber), out var value)
                ? value as int?
                : null;


        public SectionHierarchyDto()
        {
            // Default constructor initializes with no encryption secret
            _pkSecret = string.Empty;
        }
        public SectionHierarchyDto(string pkSecret)
        {
            // Initialize with the provided encryption secret for SectionHierarchyID decryption
            _pkSecret = pkSecret ?? throw new ArgumentNullException(nameof(pkSecret), "PK encryption secret cannot be null");
        }

        public SectionHierarchyDto(IConfiguration? configuration)
        {
            _pkSecret = configuration?.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException("PK encryption secret not configured");
        }
    }

    /**************************************************************/
    /// <seealso cref="Label.SectionTextContent"/>
    public class SectionTextContentDto
    {
        public required Dictionary<string, object?> SectionTextContent { get; set; }
        public SectionDto? Section { get; set; }
        public List<RenderedMediaDto> RenderedMedias { get; set; } = new();
        public List<TextListDto> TextLists { get; set; } = new();
        public List<TextTableDto> TextTables { get; set; } = new();

        /// <summary>
        /// Primary key for the SectionTextContent table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionTextContentID =>
            SectionTextContent.TryGetValue("EncryptedSectionTextContentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Section.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            SectionTextContent.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Parent SectionTextContent for hierarchy (e.g., a paragraph inside a highlight inside an excerpt)
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ParentSectionTextContentID =>
            SectionTextContent.TryGetValue("EncryptedParentSectionTextContentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Type of content block: Paragraph, List, Table, BlockImage (for [renderMultimedia] as direct child of [text]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ContentType =>
            SectionTextContent.TryGetValue(nameof(ContentType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The values for [styleCode] for font effect are bold, italics and underline. 
        /// To assist people who are visually impaired, the [styleCode=”emphasis”] 
        /// e.g. bold, italics and underline
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StyleCode =>
            SectionTextContent.TryGetValue(nameof(StyleCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Order of this content block within the parent section's [text] element.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SequenceNumber =>
            SectionTextContent.TryGetValue(nameof(SequenceNumber), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Actual text for Paragraphs. For List/Table types, details are in related tables. Inline markup (bold, italic, links etc) handled separately.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ContentText =>
            SectionTextContent.TryGetValue(nameof(ContentText), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.SpecializedKind"/>
    public class SpecializedKindDto
    {
        public required Dictionary<string, object?> SpecializedKind { get; set; }

        /// <summary>
        /// Primary key for the SpecializedKind table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SpecializedKindID =>
            SpecializedKind.TryGetValue("EncryptedSpecializedKindID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to Product.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ProductID =>
            SpecializedKind.TryGetValue("EncryptedProductID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Code for the specialized kind (e.g., device product classification, cosmetic category).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? KindCode =>
            SpecializedKind.TryGetValue(nameof(KindCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for the specialized kind code (typically 2.16.840.1.113883.6.303).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? KindCodeSystem =>
            SpecializedKind.TryGetValue(nameof(KindCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name matching the specialized kind code.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? KindDisplayName =>
            SpecializedKind.TryGetValue(nameof(KindDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.SpecifiedSubstance"/>
    public class SpecifiedSubstanceDto
    {
        public required Dictionary<string, object?> SpecifiedSubstance { get; set; }

        /// <summary>
        /// Primary key for the SpecifiedSubstance table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SpecifiedSubstanceID =>
            SpecifiedSubstance.TryGetValue("EncryptedSpecifiedSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// The code assigned to the specified substance.(Atribute code="70097M6I30")
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubstanceCode =>
            SpecifiedSubstance.TryGetValue(nameof(SubstanceCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for the specified substance code (Atribute codeSystem="2.16.840.1.113883.4.9").
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubstanceCodeSystem =>
            SpecifiedSubstance.TryGetValue(nameof(SubstanceCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code name for the specified substance code (Atribute codeSystemName="FDA SRS").
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SubstanceCodeSystemName =>
            SpecifiedSubstance.TryGetValue(nameof(SubstanceCodeSystemName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Stakeholder"/>
    public class StakeholderDto
    {
        public required Dictionary<string, object?> Stakeholder { get; set; }

        /// <summary>
        /// Primary key for the Stakeholder table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? StakeholderID =>
            Stakeholder.TryGetValue("EncryptedStakeholderID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Stakeholder code with automatic REMS validation.
        /// Validates stakeholder role codes (prescriber, patient, etc.)
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StakeholderCode =>
            Stakeholder.TryGetValue(nameof(StakeholderCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for StakeholderCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StakeholderCodeSystem =>
            Stakeholder.TryGetValue(nameof(StakeholderCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for StakeholderCode.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StakeholderDisplayName =>
            Stakeholder.TryGetValue(nameof(StakeholderDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.StructuredBody"/>
    public class StructuredBodyDto
    {

        private readonly string _pkSecret;

        public required Dictionary<string, object?> StructuredBody { get; set; }

        public StructuredBodyViewModel? StructuredBodyView { get; set; }

        public DocumentDto? Document { get; set; }

        public List<SectionDto> Sections { get; set; } = new();


        public List<SectionHierarchyDto> SectionHierarchies = new();


        /// <summary>
        /// Primary key for the StructuredBody table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? StructuredBodyID =>
            StructuredBody.TryGetValue("EncryptedStructuredBodyID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;


        /// <summary>
        /// Foreign key to Document.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? DocumentID =>
            StructuredBody.TryGetValue("EncryptedDocumentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        public StructuredBodyDto()
        {
            // Default constructor for deserialization
            _pkSecret = string.Empty; // Set to empty, will be overridden in other constructors
        }

        public StructuredBodyDto(string pkSecret)
        {
            _pkSecret = pkSecret;
        }

        public StructuredBodyDto(IConfiguration? configuration)
        {
            _pkSecret = configuration?.GetSection("Security:DB:PKSecret").Value
                 ?? throw new InvalidOperationException("PK encryption secret not configured");
        }
    }

    /**************************************************************/
    /// <seealso cref="Label.SubstanceSpecification"/>
    public class SubstanceSpecificationDto
    {
        public required Dictionary<string, object?> SubstanceSpecification { get; set; }
        public List<AnalyteDto> Analytes { get; set; } = new();
        public List<ObservationCriterionDto> ObservationCriteria { get; set; } = new();

        /// <summary>
        /// Primary key for the SubstanceSpecification table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SubstanceSpecificationID =>
            SubstanceSpecification.TryGetValue("EncryptedSubstanceSpecificationID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to IdentifiedSubstance (The substance subject to tolerance).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? IdentifiedSubstanceID =>
            SubstanceSpecification.TryGetValue("EncryptedIdentifiedSubstanceID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Specification code, format 40-CFR-...
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SpecCode =>
            SubstanceSpecification.TryGetValue(nameof(SpecCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system (2.16.840.1.113883.3.149).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? SpecCodeSystem =>
            SubstanceSpecification.TryGetValue(nameof(SpecCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code for the Enforcement Analytical Method used ([observation][code]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EnforcementMethodCode =>
            SubstanceSpecification.TryGetValue(nameof(EnforcementMethodCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system for Enforcement Analytical Method.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EnforcementMethodCodeSystem =>
            SubstanceSpecification.TryGetValue(nameof(EnforcementMethodCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Display name for Enforcement Analytical Method.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? EnforcementMethodDisplayName =>
            SubstanceSpecification.TryGetValue(nameof(EnforcementMethodDisplayName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.Telecom"/>
    public class TelecomDto
    {
        public required Dictionary<string, object?> Telecom { get; set; }
        public List<Dictionary<string, object?>> ContactPartyLinks { get; set; } = new();

        /// <summary>
        /// Primary key for the Telecom table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? TelecomID =>
            Telecom.TryGetValue("EncryptedTelecomID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Type of telecommunication: "tel", "mailto", or "fax".
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? TelecomType =>
            Telecom.TryGetValue(nameof(TelecomType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// The telecommunication value, prefixed with type (e.g., "tel:+1-...", "mailto:...", "fax:+1-...").
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? TelecomValue =>
            Telecom.TryGetValue(nameof(TelecomValue), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.TerritorialAuthority"/>
    public class TerritorialAuthorityDto
    {
        public required Dictionary<string, object?> TerritorialAuthority { get; set; }

        /// <summary>
        /// Primary key for the TerritorialAuthority table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? TerritorialAuthorityID =>
            TerritorialAuthority.TryGetValue("EncryptedTerritorialAuthorityID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// ISO 3166-2 State code (e.g., US-MD) or ISO 3166-1 Country code (e.g., USA).
        /// Used to identify the territorial scope of the licensing authority.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? TerritoryCode =>
            TerritorialAuthority.TryGetValue(nameof(TerritoryCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Code system OID for the territory code (e.g., '1.0.3166.2' for state, '1.0.3166.1.2.3' for country).
        /// Must match the type of territory code being used.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? TerritoryCodeSystem =>
            TerritorialAuthority.TryGetValue(nameof(TerritoryCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// DUNS number of the federal governing agency (e.g., "004234790" for DEA).
        /// Required when territory code is "USA", prohibited otherwise.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? GoverningAgencyIdExtension =>
            TerritorialAuthority.TryGetValue(nameof(GoverningAgencyIdExtension), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Root OID for governing agency identification ("1.3.6.1.4.1.519.1").
        /// Required when territory code is "USA", prohibited otherwise.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? GoverningAgencyIdRoot =>
            TerritorialAuthority.TryGetValue(nameof(GoverningAgencyIdRoot), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Name of the federal governing agency (e.g., "DEA" for Drug Enforcement Agency).
        /// Required when territory code is "USA", prohibited otherwise.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? GoverningAgencyName =>
            TerritorialAuthority.TryGetValue(nameof(GoverningAgencyName), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.TextList"/>
    public class TextListDto
    {
        public required Dictionary<string, object?> TextList { get; set; }
        public List<TextListItemDto> TextListItems { get; set; } = new();

        /// <summary>
        /// Primary key for the TextList table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? TextListID =>
            TextList.TryGetValue("EncryptedTextListID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to SectionTextContent (where ContentType='List').
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionTextContentID =>
            TextList.TryGetValue("EncryptedSectionTextContentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Attribute identifying the list as ordered or unordered ([list listType=]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ListType =>
            TextList.TryGetValue(nameof(ListType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Optional style code for numbering/bullet style ([list styleCode=]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StyleCode =>
            TextList.TryGetValue(nameof(StyleCode), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.TextListItem"/>
    public class TextListItemDto
    {
        public required Dictionary<string, object?> TextListItem { get; set; }

        /// <summary>
        /// Primary key for the TextListItem table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? TextListItemID =>
            TextListItem.TryGetValue("EncryptedTextListItemID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to TextList.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? TextListID =>
            TextListItem.TryGetValue("EncryptedTextListID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Order of the item within the list.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SequenceNumber =>
            TextListItem.TryGetValue(nameof(SequenceNumber), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Optional custom marker specified using [caption] within [item].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ItemCaption =>
            TextListItem.TryGetValue(nameof(ItemCaption), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Text content of the list item [item].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ItemText =>
            TextListItem.TryGetValue(nameof(ItemText), out var value)
                ? (value as string)?.Trim()
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.TextTable"/>
    public class TextTableDto
    {
        #region properties

        public required Dictionary<string, object?> TextTable { get; set; }
        public List<TextTableColumnDto> TextTableColumns { get; set; } = new();
        public List<TextTableRowDto> TextTableRows { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Primary key for the TextTable table.
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public int? TextTableID =>
        #region implementation
            TextTable.TryGetValue("EncryptedTextTableID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Foreign key to SectionTextContent (where ContentType='Table').
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionTextContentID =>
        #region implementation
            TextTable.TryGetValue("EncryptedSectionTextContentID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Optional ID attribute on the [table] element.
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? SectionTableLink =>
        #region implementation
            TextTable.TryGetValue(nameof(SectionTableLink), out var value)
                ? value as string
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Optional width attribute specified on the [table] element.
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? Width =>
        #region implementation
            TextTable.TryGetValue(nameof(Width), out var value)
                ? value as string
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Optional caption text for the table.
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? Caption =>
        #region implementation
            TextTable.TryGetValue(nameof(Caption), out var value)
                ? value as string
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Indicates if the table included a [thead] element.
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public bool? HasHeader =>
        #region implementation
            TextTable.TryGetValue(nameof(HasHeader), out var value)
                ? value as bool?
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Indicates if the table included a [tfoot] element.
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public bool? HasFooter =>
        #region implementation
            TextTable.TryGetValue(nameof(HasFooter), out var value)
                ? value as bool?
                : null;
        #endregion

        #endregion properties
    }

    /**************************************************************/
    /// <seealso cref="Label.TextTableColumn"/>
    public class TextTableColumnDto
    {
        #region properties

        public required Dictionary<string, object?> TextTableColumn { get; set; }

        /**************************************************************/
        /// <summary>
        /// Primary key for the TextTableColumn table.
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public int? TextTableColumnID =>
        #region implementation
            TextTableColumn.TryGetValue("EncryptedTextTableColumnID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Foreign key to TextTable.
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public int? TextTableID =>
        #region implementation
            TextTableColumn.TryGetValue("EncryptedTextTableID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Order of the column within the table.
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public int? SequenceNumber =>
        #region implementation
            TextTableColumn.TryGetValue(nameof(SequenceNumber), out var value)
                ? value as int?
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Optional width attribute on [col].
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? Width =>
        #region implementation
            TextTableColumn.TryGetValue(nameof(Width), out var value)
                ? value as string
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Optional align attribute on [col].
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? Align =>
        #region implementation
            TextTableColumn.TryGetValue(nameof(Align), out var value)
                ? value as string
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Optional valign attribute on [col].
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? VAlign =>
        #region implementation
            TextTableColumn.TryGetValue(nameof(VAlign), out var value)
                ? value as string
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Optional styleCode attribute on [col].
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? StyleCode =>
        #region implementation
            TextTableColumn.TryGetValue(nameof(StyleCode), out var value)
                ? value as string
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Optional width attribute on [colgroup].
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public int? ColGroupSequenceNumber =>
        #region implementation
            TextTableColumn.TryGetValue(nameof(ColGroupSequenceNumber), out var value)
                ? value as int?
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Optional align attribute on [colgroup].
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? ColGroupStyleCode =>
        #region implementation
            TextTableColumn.TryGetValue(nameof(ColGroupStyleCode), out var value)
                ? value as string
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Optional valign attribute on [colgroup].
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? ColGroupAlign =>
        #region implementation
            TextTableColumn.TryGetValue(nameof(ColGroupAlign), out var value)
                ? value as string
                : null;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Optional styleCode attribute on [colgroup].
        /// </summary>
        /// <seealso cref="Label"/>
        [Newtonsoft.Json.JsonIgnore]
        public string? ColGroupVAlign =>
        #region implementation
            TextTableColumn.TryGetValue(nameof(ColGroupVAlign), out var value)
                ? value as string
                : null;
        #endregion

        #endregion properties
    }

    /**************************************************************/
    /// <seealso cref="Label.TextTableRow"/>
    public class TextTableRowDto
    {
        public required Dictionary<string, object?> TextTableRow { get; set; }
        public List<TextTableCellDto> TextTableCells { get; set; } = new();

        /// <summary>
        /// Primary key for the TextTableRow table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? TextTableRowID =>
            TextTableRow.TryGetValue("EncryptedTextTableRowID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to TextTable.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? TextTableID =>
            TextTableRow.TryGetValue("EncryptedTextTableID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// 'Header', 'Body', 'Footer' (corresponding to thead, tbody, tfoot).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? RowGroupType =>
            TextTableRow.TryGetValue(nameof(RowGroupType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Order of the row within its group (thead, tbody, tfoot).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SequenceNumber =>
            TextTableRow.TryGetValue(nameof(SequenceNumber), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Optional styleCode attribute on [tr] (e.g., Botrule).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StyleCode =>
            TextTableRow.TryGetValue(nameof(StyleCode), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.TextTableCell"/>
    public class TextTableCellDto
    {
        public required Dictionary<string, object?> TextTableCell { get; set; }

        /// <summary>
        /// Primary key for the TextTableCell table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? TextTableCellID =>
            TextTableCell.TryGetValue("EncryptedTextTableCellID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to TextTableRow.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? TextTableRowID =>
            TextTableCell.TryGetValue("EncryptedTextTableRowID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// 'td' or 'th'.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CellType =>
            TextTableCell.TryGetValue(nameof(CellType), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Order of the cell within the row (column number).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SequenceNumber =>
            TextTableCell.TryGetValue(nameof(SequenceNumber), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Text content of the table cell ([td] or [th]).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? CellText =>
            TextTableCell.TryGetValue(nameof(CellText), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Optional rowspan attribute on [td] or [th].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? RowSpan =>
            TextTableCell.TryGetValue(nameof(RowSpan), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Optional colspan attribute on [td] or [th].
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? ColSpan =>
            TextTableCell.TryGetValue(nameof(ColSpan), out var value)
                ? value as int?
                : null;

        /// <summary>
        /// Optional styleCode attribute for cell rules (Lrule, Rrule, Toprule, Botrule).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StyleCode =>
            TextTableCell.TryGetValue(nameof(StyleCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Optional align attribute for horizontal alignment.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? Align =>
            TextTableCell.TryGetValue(nameof(Align), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Optional valign attribute for vertical alignment.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? VAlign =>
            TextTableCell.TryGetValue(nameof(VAlign), out var value)
                ? value as string
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.WarningLetterDate"/>
    public class WarningLetterDateDto
    {
        public required Dictionary<string, object?> WarningLetterDate { get; set; }

        /// <summary>
        /// Primary key for the WarningLetterDate table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? WarningLetterDateID =>
            WarningLetterDate.TryGetValue("EncryptedWarningLetterDateID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to the Indexing Section (48779-3).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            WarningLetterDate.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Date the warning letter alert was issued.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? AlertIssueDate =>
            WarningLetterDate.TryGetValue(nameof(AlertIssueDate), out var value)
                ? value as DateTime?
                : null;

        /// <summary>
        /// Date the issue described in the warning letter was resolved, if applicable.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public DateTime? ResolutionDate =>
            WarningLetterDate.TryGetValue(nameof(ResolutionDate), out var value)
                ? value as DateTime?
                : null;
    }

    /**************************************************************/
    /// <seealso cref="Label.WarningLetterProductInfo"/>
    public class WarningLetterProductInfoDto
    {
        public required Dictionary<string, object?> WarningLetterProductInfo { get; set; }

        /// <summary>
        /// Primary key for the WarningLetterProductInfo table.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? WarningLetterProductInfoID =>
            WarningLetterProductInfo.TryGetValue("EncryptedWarningLetterProductInfoID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Foreign key to the Indexing Section (48779-3).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public int? SectionID =>
            WarningLetterProductInfo.TryGetValue("EncryptedSectionID", out var value)
                ? Util.DecryptAndParseInt(value)
                : null;

        /// <summary>
        /// Proprietary name of the product referenced in the warning letter.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ProductName =>
            WarningLetterProductInfo.TryGetValue(nameof(ProductName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Generic name of the product referenced in the warning letter.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? GenericName =>
            WarningLetterProductInfo.TryGetValue(nameof(GenericName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Dosage form code of the product referenced in the warning letter.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? FormCode =>
            WarningLetterProductInfo.TryGetValue(nameof(FormCode), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Dosage Form code system.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? FormCodeSystem =>
            WarningLetterProductInfo.TryGetValue(nameof(FormCodeSystem), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Dosage Form display name.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? FormDisplayName =>
            WarningLetterProductInfo.TryGetValue(nameof(FormDisplayName), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Text description of the ingredient strength(s).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? StrengthText =>
            WarningLetterProductInfo.TryGetValue(nameof(StrengthText), out var value)
                ? value as string
                : null;

        /// <summary>
        /// Text description of the product item code(s) (e.g., NDC).
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? ItemCodesText =>
            WarningLetterProductInfo.TryGetValue(nameof(ItemCodesText), out var value)
                ? value as string
                : null;
    }
}
