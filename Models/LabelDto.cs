namespace MedRecPro.Models
{
    /**************************************************************/
    public class AddressDto
    {
        public required Dictionary<string, object?> Address { get; set; }
        public List<ContactPartyDto> ContactParties { get; set; } = new();
    }

    /**************************************************************/
    public class AnalyteDto
    {
        public required Dictionary<string, object?> Analyte { get; set; }
    }

    /**************************************************************/
    public class BillingUnitIndexDto
    {
        public required Dictionary<string, object?> BillingUnitIndex { get; set; }
    }

    /**************************************************************/
    public class BusinessOperationDto
    {
        public required Dictionary<string, object?> BusinessOperation { get; set; }
    }

    /**************************************************************/
    public class BusinessOperationProductLinkDto
    {
        public required Dictionary<string, object?> BusinessOperationProductLink { get; set; }
    }

    /**************************************************************/
    public class CertificationProductLinkDto
    {
        public required Dictionary<string, object?> CertificationProductLink { get; set; }
    }

    /**************************************************************/
    public class CharacteristicDto
    {
        public required Dictionary<string, object?> Characteristic { get; set; }
        public List<Dictionary<string, object?>> PackagingLevels { get; set; } = new();
    }

    /**************************************************************/
    public class ComplianceActionDto
    {
        public required Dictionary<string, object?> ComplianceAction { get; set; }
    }

    /**************************************************************/
    public class ContactPartyDto
    {
        public required Dictionary<string, object?> ContactParty { get; set; }
        public OrganizationDto? Organization { get; set; }
        public AddressDto? Address { get; set; }
        public ContactPersonDto? ContactPerson { get; set; }
        public List<ContactPartyTelecomDto> Telecoms { get; set; } = new();
    }

    /**************************************************************/
    public class ContactPartyTelecomDto
    {
        public required Dictionary<string, object?> ContactPartyTelecom { get; set; }
        public ContactPartyDto? ContactParty { get; set; }
        public TelecomDto? Telecom { get; set; }
    }

    /**************************************************************/
    public class ContactPersonDto
    {
        public required Dictionary<string, object?> ContactPerson { get; set; }
        public List<ContactPartyDto> ContactParties { get; set; } = new();
    }

    /**************************************************************/
    public class DocumentAuthorDto
    {
        public required Dictionary<string, object?> DocumentAuthor { get; set; }
        public DocumentDto? Document { get; set; }
        public OrganizationDto? Organization { get; set; }
    }

    /**************************************************************/
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
    }

    /**************************************************************/
    public class DocumentRelationshipDto
    {
        public required Dictionary<string, object?> DocumentRelationship { get; set; }
        public DocumentDto? Document { get; set; }
        public OrganizationDto? ParentOrganization { get; set; }
        public OrganizationDto? ChildOrganization { get; set; }
        public List<BusinessOperationDto> BusinessOperations { get; set; } = new();
        public List<CertificationProductLinkDto> CertificationProductLinks { get; set; } = new();
        public List<ComplianceActionDto> ComplianceActions { get; set; } = new();
        public List<FacilityProductLinkDto> FacilityProductLinks { get; set; } = new();
    }

    /**************************************************************/
    public class FacilityProductLinkDto
    {
        public required Dictionary<string, object?> FacilityProductLink { get; set; }
    }

    /**************************************************************/
    public class GenericMedicineDto
    {
        public required Dictionary<string, object?> GenericMedicine { get; set; }
    }

    /**************************************************************/
    public class HolderDto
    {
        public required Dictionary<string, object?> Holder { get; set; }
    }

    /**************************************************************/
    public class IdentifiedSubstanceDto
    {
        public required Dictionary<string, object?> IdentifiedSubstance { get; set; }

        public List<SubstanceSpecificationDto> SubstanceSpecifications { get; set; } = new();
    }

    /**************************************************************/
    public class IngredientDto
    {
        public required Dictionary<string, object?> Ingredient { get; set; }

        public IngredientSubstanceDto? IngredientSubstance { get; set; }

        public List<IngredientInstanceDto> IngredientInstances { get; set; } = new();

        public List<ReferenceSubstanceDto> ReferenceSubstances { get; set; } = new();

        public List<IngredientSourceProductDto> IngredientSourceProducts { get; set; } = new();
    }

    /**************************************************************/
    public class IngredientInstanceDto
    {
        public required Dictionary<string, object?> IngredientInstance { get; set; }
        public LotIdentifierDto? LotIdentifier { get; set; }

        public List<IngredientSubstanceDto> IngredientSubstances { get; set; } = new();

    }

    /**************************************************************/
    public class IngredientSourceProductDto
    {
        public required Dictionary<string, object?> IngredientSourceProduct { get; set; }
    }

    /**************************************************************/
    public class IngredientSubstanceDto
    {
        public required Dictionary<string, object?> IngredientSubstance { get; set; }
        public List<IngredientInstanceDto> IngredientInstances { get; set; } = new();
    }

    /**************************************************************/
    public class InteractionIssueDto
    {
        public required Dictionary<string, object?> InteractionIssue { get; set; }
    }

    /**************************************************************/
    public class LegalAuthenticatorDto
    {
        public required Dictionary<string, object?> LegalAuthenticator { get; set; }
        public DocumentDto? Document { get; set; }
        public OrganizationDto? Organization { get; set; }
    }

    /**************************************************************/
    public class LotHierarchyDto
    {
        public required Dictionary<string, object?> LotHierarchy { get; set; }
        public ProductInstanceDto? ParentInstance { get; set; }
        public ProductInstanceDto? ChildInstance { get; set; }
    }

    /**************************************************************/
    public class LotIdentifierDto
    {
        public required Dictionary<string, object?> LotIdentifier { get; set; }
    }

    /**************************************************************/
    public class MarketingCategoryDto
    {
        public required Dictionary<string, object?> MarketingCategory { get; set; }
    }

    /**************************************************************/
    public class NamedEntityDto
    {
        public required Dictionary<string, object?> NamedEntity { get; set; }
    }

    /**************************************************************/
    public class ObservationMediaDto
    {
        public required Dictionary<string, object?> ObservationMedia { get; set; }
        public SectionDto? Section { get; set; }
    }

    /**************************************************************/
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
        public List<TerritorialAuthorityDto> GoverningAuthorities { get; set; } = new();
        public List<NamedEntityDto> NamedEntities { get; set; } = new();
        public List<HolderDto> Holders { get; set; } = new();
        public List<IngredientInstanceDto> ManufacturedIngredientInstances { get; set; } = new();
        public List<ResponsiblePersonLinkDto> ResponsibleForProducts { get; set; } = new();
    }

    /**************************************************************/
    public class OrganizationIdentifierDto
    {
        public required Dictionary<string, object?> OrganizationIdentifier { get; set; }
    }

    /**************************************************************/
    public class OrganizationTelecomDto
    {
        public required Dictionary<string, object?> OrganizationTelecom { get; set; }
        public OrganizationDto? Organization { get; set; }
        public TelecomDto? Telecom { get; set; }
    }

    /**************************************************************/
    public class PackagingLevelDto
    {
        public required Dictionary<string, object?> PackagingLevel { get; set; }
        public List<Dictionary<string, object?>> PackagingHierarchy { get; set; } = new();
    }

    /**************************************************************/
    public class PackageIdentifierDto
    {
        public required Dictionary<string, object?> PackageIdentifier { get; set; }
    }

    /**************************************************************/
    public class PackagingHierarchyDto
    {
        public required Dictionary<string, object?> PackagingHierarchy { get; set; }
    }

    /**************************************************************/
    public class ProductConceptDto
    {
        public required Dictionary<string, object?> ProductConcept { get; set; }
    }

    /**************************************************************/
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
        public List<PackagingLevelDto> PackagingLevels { get; set; } = new();
        public List<LotHierarchyDto> ParentLotHierarchies { get; set; } = new();
        public List<LotHierarchyDto> ChildLotHierarchies { get; set; } = new();
        public List<CharacteristicDto> Characteristics { get; set; } = new();
    }

   

    /**************************************************************/
    public class ProductIdentifierDto
    {
        public required Dictionary<string, object?> ProductIdentifier { get; set; }
    }

    /**************************************************************/
    public class ProductInstanceDto
    {
        public required Dictionary<string, object?> ProductInstance { get; set; }
        public LotIdentifierDto? LotIdentifier { get; set; }
        public List<LotHierarchyDto> ParentHierarchies { get; set; } = new();
        public List<LotHierarchyDto> ChildHierarchies { get; set; } = new();
        public List<PackagingLevelDto> PackagingLevels { get; set; } = new();
    }

    /**************************************************************/
    public class ProductPartDto
    {
        public required Dictionary<string, object?> ProductPart { get; set; }
        public ProductDto? KitProduct { get; set; }
    }

    /**************************************************************/
    public class ProductRouteOfAdministrationDto
    {
        public required Dictionary<string, object?> ProductRouteOfAdministration { get; set; }
    }

    /**************************************************************/
    public class ProductWebLinkDto
    {
        public required Dictionary<string, object?> ProductWebLink { get; set; }
    }

    /**************************************************************/
    public class ProtocolDto
    {
        public required Dictionary<string, object?> Protocol { get; set; }
    }

    /**************************************************************/
    public class ReferenceSubstanceDto
    {
        public required Dictionary<string, object?> ReferenceSubstance { get; set; }
    }

    /**************************************************************/
    public class RelatedDocumentDto
    {
        public required Dictionary<string, object?> RelatedDocument { get; set; }
        public DocumentDto? SourceDocument { get; set; }
    }

    /**************************************************************/
    public class REMSElectronicResourceDto
    {
        public required Dictionary<string, object?> REMSElectronicResource { get; set; }
    }

    /**************************************************************/
    public class REMSMaterialDto
    {
        public required Dictionary<string, object?> REMSMaterial { get; set; }
    }

    /**************************************************************/
    public class ResponsiblePersonLinkDto
    {
        public required Dictionary<string, object?> ResponsiblePersonLink { get; set; }
    }

    /**************************************************************/
    public class SectionDto
    {
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
    }

    /**************************************************************/
    public class SectionExcerptHighlightDto
    {
        public required Dictionary<string, object?> SectionExcerptHighlight { get; set; }
        public SectionDto? Section { get; set; }
    }

    /**************************************************************/
    public class SectionHierarchyDto
    {
        public required Dictionary<string, object?> SectionHierarchy { get; set; }
    }

    /**************************************************************/
    public class SectionTextContentDto
    {
        public required Dictionary<string, object?> SectionTextContent { get; set; }
        public SectionDto? Section { get; set; }
    }

    /**************************************************************/
    public class StructuredBodyDto
    {
        public required Dictionary<string, object?> StructuredBody { get; set; }
        public DocumentDto? Document { get; set; }
        public List<SectionDto> Sections { get; set; } = new();
    }

    /**************************************************************/
    public class SubstanceSpecificationDto
    {
        public required Dictionary<string, object?> SubstanceSpecification { get; set; }

        public List<AnalyteDto> Analytes { get; set; } = new();
    }

    /**************************************************************/
    public class TelecomDto
    {
        public required Dictionary<string, object?> Telecom { get; set; }
        public List<Dictionary<string, object?>> ContactPartyLinks { get; set; } = new();
    }

    /**************************************************************/
    public class TerritorialAuthorityDto
    {
        public required Dictionary<string, object?> TerritorialAuthority { get; set; }
    }

    /**************************************************************/
    public class WarningLetterDateDto
    {
        public required Dictionary<string, object?> WarningLetterDate { get; set; }
    }

    /**************************************************************/
    public class WarningLetterProductInfoDto
    {
        public required Dictionary<string, object?> WarningLetterProductInfo { get; set; }
    }
}