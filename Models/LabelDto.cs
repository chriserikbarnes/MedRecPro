namespace MedRecPro.Models
{
    /**************************************************************/
    /// <seealso cref="Label.AdditionalIdentifier"/>
    public class AdditionalIdentifierDto
    {
        public required Dictionary<string, object?> AdditionalIdentifier { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.ActiveMoiety"/>
    public class ActiveMoietyDto
    {
        public required Dictionary<string, object?> ActiveMoiety { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.Address"/>
    public class AddressDto
    {
        public required Dictionary<string, object?> Address { get; set; }
        public List<ContactPartyDto> ContactParties { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.Analyte"/>
    public class AnalyteDto
    {
        public required Dictionary<string, object?> Analyte { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.ApplicationType"/>
    public class ApplicationTypeDto
    {
        public required Dictionary<string, object?> ApplicationType { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.AttachedDocument"/>
    public class AttachedDocumentDto
    {
        public required Dictionary<string, object?> AttachedDocument { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.BillingUnitIndex"/>
    public class BillingUnitIndexDto
    {
        public required Dictionary<string, object?> BillingUnitIndex { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.BusinessOperation"/>
    public class BusinessOperationDto
    {
        public required Dictionary<string, object?> BusinessOperation { get; set; }
        public List<BusinessOperationQualifierDto> BusinessOperationQualifiers { get; set; } = new();
        public List<LicenseDto> Licenses { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.BusinessOperationProductLink"/>
    public class BusinessOperationProductLinkDto
    {
        public required Dictionary<string, object?> BusinessOperationProductLink { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.BusinessOperationQualifier"/>
    public class BusinessOperationQualifierDto
    {
        public required Dictionary<string, object?> BusinessOperationQualifier { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.CertificationProductLink"/>
    public class CertificationProductLinkDto
    {
        public required Dictionary<string, object?> CertificationProductLink { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.Characteristic"/>
    public class CharacteristicDto
    {
        public required Dictionary<string, object?> Characteristic { get; set; }
        public List<Dictionary<string, object?>> PackagingLevels { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.Commodity"/>
    public class CommodityDto
    {
        public required Dictionary<string, object?> Commodity { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.ContributingFactor"/>
    public class ContributingFactorDto
    {
        public required Dictionary<string, object?> ContributingFactor { get; set; }
        public List<InteractionConsequenceDto> InteractionConsequences { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.ComplianceAction"/>
    public class ComplianceActionDto
    {
        public required Dictionary<string, object?> ComplianceAction { get; set; }
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
    }

    /**************************************************************/
    /// <seealso cref="Label.ContactPartyTelecom"/>
    public class ContactPartyTelecomDto
    {
        public required Dictionary<string, object?> ContactPartyTelecom { get; set; }
        public ContactPartyDto? ContactParty { get; set; }
        public TelecomDto? Telecom { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.ContactPerson"/>
    public class ContactPersonDto
    {
        public required Dictionary<string, object?> ContactPerson { get; set; }
        public List<ContactPartyDto> ContactParties { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.DisciplinaryAction"/>
    public class DisciplinaryActionDto
    {
        public required Dictionary<string, object?> DisciplinaryAction { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.DocumentAuthor"/>
    public class DocumentAuthorDto
    {
        public required Dictionary<string, object?> DocumentAuthor { get; set; }
        public DocumentDto? Document { get; set; }
        public OrganizationDto? Organization { get; set; }
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
    }

    /**************************************************************/
    /// <seealso cref="Label.DocumentRelationship"/>
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
    /// <seealso cref="Label.DosingSpecification"/>
    public class DosingSpecificationDto
    {
        public required Dictionary<string, object?> DosingSpecification { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.EquivalentEntity"/>
    public class EquivalentEntityDto
    {
        public required Dictionary<string, object?> EquivalentEntity { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.FacilityProductLink"/>
    public class FacilityProductLinkDto
    {
        public required Dictionary<string, object?> FacilityProductLink { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.GenericMedicine"/>
    public class GenericMedicineDto
    {
        public required Dictionary<string, object?> GenericMedicine { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.Holder"/>
    public class HolderDto
    {
        public required Dictionary<string, object?> Holder { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.IdentifiedSubstance"/>
    public class IdentifiedSubstanceDto
    {
        public required Dictionary<string, object?> IdentifiedSubstance { get; set; }
        public List<SubstanceSpecificationDto> SubstanceSpecifications { get; set; } = new();
        public List<ContributingFactorDto> ContributingFactors { get; set; } = new();
        public List<PharmacologicClassDto> PharmacologicClasses { get; set; } = new();
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
    }

    /**************************************************************/
    /// <seealso cref="Label.IngredientInstance"/>
    public class IngredientInstanceDto
    {
        public required Dictionary<string, object?> IngredientInstance { get; set; }
        public LotIdentifierDto? LotIdentifier { get; set; }

        public List<IngredientSubstanceDto> IngredientSubstances { get; set; } = new();

    }

    /**************************************************************/
    /// <seealso cref="Label.IngredientSourceProduct"/>
    public class IngredientSourceProductDto
    {
        public required Dictionary<string, object?> IngredientSourceProduct { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.IngredientSubstance"/>
    public class IngredientSubstanceDto
    {
        public required Dictionary<string, object?> IngredientSubstance { get; set; }
        public List<IngredientInstanceDto> IngredientInstances { get; set; } = new();
        public List<ActiveMoietyDto> ActiveMoieties { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.InteractionConsequence"/>
    public class InteractionConsequenceDto
    {
        public required Dictionary<string, object?> InteractionConsequence { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.InteractionIssue"/>
    public class InteractionIssueDto
    {
        public required Dictionary<string, object?> InteractionIssue { get; set; }
        public List<InteractionConsequenceDto> InteractionConsequences { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.LegalAuthenticator"/>
    public class LegalAuthenticatorDto
    {
        public required Dictionary<string, object?> LegalAuthenticator { get; set; }
        public DocumentDto? Document { get; set; }
        public OrganizationDto? Organization { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.License"/>
    public class LicenseDto
    {
        public required Dictionary<string, object?> License { get; set; }
        public List<DisciplinaryActionDto> DisciplinaryActions { get; set; } = new();

        public List<TerritorialAuthorityDto> TerritorialAuthorities { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.LotHierarchy"/>
    public class LotHierarchyDto
    {
        public required Dictionary<string, object?> LotHierarchy { get; set; }
        public ProductInstanceDto? ParentInstance { get; set; }
        public ProductInstanceDto? ChildInstance { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.LotIdentifier"/>
    public class LotIdentifierDto
    {
        public required Dictionary<string, object?> LotIdentifier { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.MarketingCategory"/>
    public class MarketingCategoryDto
    {
        public required Dictionary<string, object?> MarketingCategory { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.MarketingStatus"/>
    public class MarketingStatusDto
    {
        public required Dictionary<string, object?> MarketingStatus { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.NamedEntity"/>
    public class NamedEntityDto
    {
        public required Dictionary<string, object?> NamedEntity { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.NCTLink"/>
    public class NCTLinkDto
    {
        public required Dictionary<string, object?> NCTLink { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.ObservationCriterion"/>
    public class ObservationCriterionDto
    {
        public required Dictionary<string, object?> ObservationCriterion { get; set; }
        public List<ApplicationTypeDto> ApplicationTypes { get; set; } = new();
        public List<CommodityDto> Commodities { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.ObservationMedia"/>
    public class ObservationMediaDto
    {
        public required Dictionary<string, object?> ObservationMedia { get; set; }
        public SectionDto? Section { get; set; }
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
    }

    /**************************************************************/
    /// <seealso cref="Label.OrganizationIdentifier"/>
    public class OrganizationIdentifierDto
    {
        public required Dictionary<string, object?> OrganizationIdentifier { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.OrganizationTelecom"/>
    public class OrganizationTelecomDto
    {
        public required Dictionary<string, object?> OrganizationTelecom { get; set; }
        public OrganizationDto? Organization { get; set; }
        public TelecomDto? Telecom { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.PackagingLevel"/>
    public class PackagingLevelDto
    {
        public required Dictionary<string, object?> PackagingLevel { get; set; }
        public List<PackagingHierarchyDto> PackagingHierarchy { get; set; } = new();
        public List<ProductEventDto> ProductEvents { get; set; } = new();
        public List<MarketingStatusDto> MarketingStatuses { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.PackageIdentifier"/>
    public class PackageIdentifierDto
    {
        public required Dictionary<string, object?> PackageIdentifier { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.PackagingHierarchy"/>
    public class PackagingHierarchyDto
    {
        public required Dictionary<string, object?> PackagingHierarchy { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.PartOfAssembly"/>
    public class PartOfAssemblyDto
    {
        public required Dictionary<string, object?> PartOfAssembly { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.PharmacologicClass"/>
    public class PharmacologicClassDto
    {
        public required Dictionary<string, object?> PharmacologicClass { get; set; }
        public List<PharmacologicClassNameDto> PharmacologicClassNames { get; set; } = new();
        public List<PharmacologicClassLinkDto> PharmacologicClassLinks { get; set; } = new();
        public List<PharmacologicClassHierarchyDto> PharmacologicClassHierarchies { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.PharmacologicClassName"/>
    public class PharmacologicClassNameDto
    {
        public required Dictionary<string, object?> PharmacologicClassName { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.PharmacologicClassLink"/>
    public class PharmacologicClassLinkDto
    {
        public required Dictionary<string, object?> PharmacologicClassLink { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.PharmacologicClassHierarchy"/>
    public class PharmacologicClassHierarchyDto
    {
        public required Dictionary<string, object?> PharmacologicClassHierarchy { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.Policy"/>
    public class PolicyDto
    {
        public required Dictionary<string, object?> Policy { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductConcept"/>
    public class ProductConceptDto
    {
        public required Dictionary<string, object?> ProductConcept { get; set; }
        public List<ProductConceptEquivalenceDto> ProductConceptEquivalences { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductConcept"/>
    public class ProductConceptEquivalenceDto
    {
        public required Dictionary<string, object?> ProductConcept { get; set; }
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
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductEvent"/>
    public class ProductEventDto
    {
        public required Dictionary<string, object?> ProductEvent { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductIdentifier"/>
    public class ProductIdentifierDto
    {
        public required Dictionary<string, object?> ProductIdentifier { get; set; }
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
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductPart"/>
    public class ProductPartDto
    {
        public required Dictionary<string, object?> ProductPart { get; set; }
        public ProductDto? KitProduct { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductRouteOfAdministration"/>
    public class ProductRouteOfAdministrationDto
    {
        public required Dictionary<string, object?> ProductRouteOfAdministration { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.ProductWebLink"/>
    public class ProductWebLinkDto
    {
        public required Dictionary<string, object?> ProductWebLink { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.Protocol"/>
    public class ProtocolDto
    {
        public required Dictionary<string, object?> Protocol { get; set; }
        public List<REMSApprovalDto> REMSApprovals { get; set; } = new();
        public List<RequirementDto> Requirements { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.ReferenceSubstance"/>
    public class ReferenceSubstanceDto
    {
        public required Dictionary<string, object?> ReferenceSubstance { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.RelatedDocument"/>
    public class RelatedDocumentDto
    {
        public required Dictionary<string, object?> RelatedDocument { get; set; }
        public DocumentDto? SourceDocument { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.REMSApproval"/>
    public class REMSApprovalDto
    {
        public required Dictionary<string, object?> REMSApproval { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.REMSElectronicResource"/>
    public class REMSElectronicResourceDto
    {
        public required Dictionary<string, object?> REMSElectronicResource { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.REMSMaterial"/>
    public class REMSMaterialDto
    {
        public required Dictionary<string, object?> REMSMaterial { get; set; }
        public List<AttachedDocumentDto> AttachedDocuments { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.RenderedMedia"/>
    public class RenderedMediaDto
    {
        public required Dictionary<string, object?> RenderedMedia { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.Requirement"/>
    public class RequirementDto
    {
        public required Dictionary<string, object?> Requirement { get; set; }
        public List<StakeholderDto> Stakeholders { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.ResponsiblePersonLink"/>
    public class ResponsiblePersonLinkDto
    {
        public required Dictionary<string, object?> ResponsiblePersonLink { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.Section"/>
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
        public List<NCTLinkDto> NCTLinks { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.SectionExcerptHighlight"/>
    public class SectionExcerptHighlightDto
    {
        public required Dictionary<string, object?> SectionExcerptHighlight { get; set; }
        public SectionDto? Section { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.SectionHierarchy"/>
    public class SectionHierarchyDto
    {
        public required Dictionary<string, object?> SectionHierarchy { get; set; }
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
    }

    /**************************************************************/
    /// <seealso cref="Label.SpecializedKind"/>
    public class SpecializedKindDto
    {
        public required Dictionary<string, object?> SpecializedKind { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.SpecifiedSubstance"/>
    public class SpecifiedSubstanceDto
    {
        public required Dictionary<string, object?> SpecifiedSubstance { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.Stakeholder"/>
    public class StakeholderDto
    {
        public required Dictionary<string, object?> Stakeholder { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.StructuredBody"/>
    public class StructuredBodyDto
    {
        public required Dictionary<string, object?> StructuredBody { get; set; }
        public DocumentDto? Document { get; set; }
        public List<SectionDto> Sections { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.SubstanceSpecification"/>
    public class SubstanceSpecificationDto
    {
        public required Dictionary<string, object?> SubstanceSpecification { get; set; }
        public List<AnalyteDto> Analytes { get; set; } = new();
        public List<ObservationCriterionDto> ObservationCriteria { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.Telecom"/>
    public class TelecomDto
    {
        public required Dictionary<string, object?> Telecom { get; set; }
        public List<Dictionary<string, object?>> ContactPartyLinks { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.TerritorialAuthority"/>
    public class TerritorialAuthorityDto
    {
        public required Dictionary<string, object?> TerritorialAuthority { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.TextList"/>
    public class TextListDto
    {
        public required Dictionary<string, object?> TextList { get; set; }
        public List<TextListItemDto> TextListItems { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.TextListItem"/>
    public class TextListItemDto
    {
        public required Dictionary<string, object?> TextListItem { get; set; }

    }

    /**************************************************************/
    /// <seealso cref="Label.TextTable"/>
    public class TextTableDto
    {
        public required Dictionary<string, object?> TextTable { get; set; }
        public List<TextTableRowDto> TextTableRows { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.TextTableRow"/>
    public class TextTableRowDto
    {
        public required Dictionary<string, object?> TextTableRow { get; set; }
        public List<TextTableCellDto> TextTableCells { get; set; } = new();
    }

    /**************************************************************/
    /// <seealso cref="Label.TextTableCell"/>
    public class TextTableCellDto
    {
        public required Dictionary<string, object?> TextTableCell { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.WarningLetterDate"/>
    public class WarningLetterDateDto
    {
        public required Dictionary<string, object?> WarningLetterDate { get; set; }
    }

    /**************************************************************/
    /// <seealso cref="Label.WarningLetterProductInfo"/>
    public class WarningLetterProductInfoDto
    {
        public required Dictionary<string, object?> WarningLetterProductInfo { get; set; }
    }
}