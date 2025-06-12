using MedRecPro.Data;
using MedRecPro.Models;
using MedRecPro.DataModels;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MedRecPro.Helpers
{
    public static class DtoLabelHelper
    {
        /**************************************************************/
        /// <summary>
        /// The primary entry point for building a list of Document DTOs with their full hierarchy of related data.
        /// <br/>
        /// <b>References:</b>
        /// <list type="bullet">
        ///   <item><description><see cref="Label.Document"/></description></item>
        ///   <item><description><see cref="Label.StructuredBody"/></description></item>
        ///   <item><description><see cref="Label.DocumentAuthor"/></description></item>
        ///   <item><description><see cref="Label.RelatedDocument"/></description></item>
        ///   <item><description><see cref="Label.DocumentRelationship"/></description></item>
        ///   <item><description><see cref="Label.LegalAuthenticator"/></description></item>
        /// </list>
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="DocumentDto"/> representing the fetched documents and their related entities.</returns>ary>
        public static async Task<List<DocumentDto>> BuildDocumentsAsync(
           ApplicationDbContext db,
           string pkSecret,
           ILogger logger,
           int? page = null,
           int? size = null)
        {
            // 1. Fetch top-level Documents with optional pagination
            var query = db.Set<Label.Document>().AsNoTracking();
            if (page.HasValue && size.HasValue)
            {
                query = query.OrderBy(d => d.DocumentID).Skip((page.Value - 1) * size.Value).Take(size.Value);
            }

            var docs = await query.ToListAsync();
            var docDtos = new List<DocumentDto>();

            // 2. For each Document, build its complete DTO graph
            foreach (var doc in docs)
            {
                var docDict = doc.ToEntityWithEncryptedId(pkSecret, logger);

                // 3. Sequentially build all direct children of the Document.
                // NOTE: We are intentionally using sequential awaits instead of Task.WhenAll to ensure
                // the DbContext is not used concurrently, addressing thread-safety concerns.
                var structuredBodies = await BuildStructuredBodiesAsync(db, doc.DocumentID, pkSecret, logger);
                var authors = await BuildDocumentAuthorsAsync(db, doc.DocumentID, pkSecret, logger);
                var relatedDocs = await BuildRelatedDocumentsAsync(db, doc.DocumentID, pkSecret, logger);
                var relationships = await BuildDocumentRelationshipsAsync(db, doc.DocumentID, pkSecret, logger);
                var authenticators = await BuildLegalAuthenticatorsAsync(db, doc.DocumentID, pkSecret, logger);

                docDtos.Add(new DocumentDto
                {
                    Document = docDict,
                    StructuredBodies = structuredBodies,
                    DocumentAuthors = authors,
                    SourceRelatedDocuments = relatedDocs,
                    DocumentRelationships = relationships,
                    LegalAuthenticators = authenticators
                });
            }
            return docDtos;
        }

        #region Document Children Builders

        /**************************************************************/
        /// <summary>
        /// Builds a list of <see cref="DocumentAuthorDto"/> for the specified document.
        /// See: <see cref="Label.DocumentAuthor"/> (navigation property)
        /// </summary>
        private static async Task<List<DocumentAuthorDto>> BuildDocumentAuthorsAsync(ApplicationDbContext db, int? documentId, string pkSecret, ILogger logger)
        {
            if (documentId == null) return new List<DocumentAuthorDto>();

            var items = await db.Set<Label.DocumentAuthor>()
                .AsNoTracking()
                .Where(e => e.DocumentID == documentId)
                .ToListAsync();

            return items
                .Select(item => new DocumentAuthorDto { DocumentAuthor = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of <see cref="RelatedDocumentDto"/> for the specified document.
        /// See: <see cref="Label.RelatedDocument"/> (navigation property)
        /// </summary>
        private static async Task<List<RelatedDocumentDto>> BuildRelatedDocumentsAsync(ApplicationDbContext db, int? documentId, string pkSecret, ILogger logger)
        {
            if (documentId == null) return new List<RelatedDocumentDto>();

            var items = await db.Set<Label.RelatedDocument>()
                .AsNoTracking()
                .Where(e => e.SourceDocumentID == documentId)
                .ToListAsync();

            return items
                .Select(item => new RelatedDocumentDto { RelatedDocument = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of <see cref="DocumentRelationshipDto"/> for the specified document.
        /// See: <see cref="Label.DocumentRelationship"/> (navigation property)
        /// <br/>
        /// Nested collections map to:
        /// <list type="bullet">
        ///   <item><description><see cref="Label.BusinessOperation"/></description></item>
        ///   <item><description><see cref="Label.CertificationProductLink"/></description></item>
        ///   <item><description><see cref="Label.ComplianceAction"/></description></item>
        ///   <item><description><see cref="Label.FacilityProductLink"/></description></item>
        /// </list>
        /// </summary>
        private static async Task<List<DocumentRelationshipDto>> BuildDocumentRelationshipsAsync(ApplicationDbContext db, int? documentId, string pkSecret, ILogger logger)
        {
            if (documentId == null) return new List<DocumentRelationshipDto>();

            var relationships = await db.Set<Label.DocumentRelationship>()
                .AsNoTracking()
                .Where(e => e.DocumentID == documentId)
                .ToListAsync();

            var dtos = new List<DocumentRelationshipDto>();

            foreach (var rel in relationships)
            {
                var businessOps = await BuildBusinessOperationsAsync(db, rel.DocumentRelationshipID, pkSecret, logger);

                var certLinks = await BuildCertificationProductLinksAsync(db, rel.DocumentRelationshipID, pkSecret, logger);

                var complianceActions = await BuildComplianceActionsForRelationshipAsync(db, rel.DocumentRelationshipID, pkSecret, logger);

                var facilityLinks = await BuildFacilityProductLinksAsync(db, rel.DocumentRelationshipID, pkSecret, logger);

                dtos.Add(new DocumentRelationshipDto
                {
                    DocumentRelationship = rel.ToEntityWithEncryptedId(pkSecret, logger),
                    BusinessOperations = businessOps,
                    CertificationProductLinks = certLinks,
                    ComplianceActions = complianceActions,
                    FacilityProductLinks = facilityLinks
                });
            }
            return dtos;
        }

        private static async Task<List<LegalAuthenticatorDto>> BuildLegalAuthenticatorsAsync(ApplicationDbContext db, int? documentId, string pkSecret, ILogger logger)
        {
            if (documentId == null) return new List<LegalAuthenticatorDto>();
            var items = await db.Set<Label.LegalAuthenticator>().AsNoTracking().Where(e => e.DocumentID == documentId).ToListAsync();
            return items.Select(item => new LegalAuthenticatorDto { LegalAuthenticator = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        #endregion

        #region StructuredBody & Section Builders

        private static async Task<List<StructuredBodyDto>> BuildStructuredBodiesAsync(ApplicationDbContext db, int? documentId, string pkSecret, ILogger logger)
        {
            if (documentId == null) return new List<StructuredBodyDto>();
            var sbs = await db.Set<Label.StructuredBody>().AsNoTracking().Where(sb => sb.DocumentID == documentId).ToListAsync();
            var sbDtos = new List<StructuredBodyDto>();
            foreach (var sb in sbs)
            {
                var sectionDtos = await BuildSectionsAsync(db, sb.StructuredBodyID, pkSecret, logger);
                sbDtos.Add(new StructuredBodyDto
                {
                    StructuredBody = sb.ToEntityWithEncryptedId(pkSecret, logger),
                    Sections = sectionDtos
                });
            }
            return sbDtos;
        }

        private static async Task<List<SectionDto>> BuildSectionsAsync(ApplicationDbContext db, int? structuredBodyId, string pkSecret, ILogger logger)
        {
            if (structuredBodyId == null) return new List<SectionDto>();
            var sections = await db.Set<Label.Section>().AsNoTracking().Where(s => s.StructuredBodyID == structuredBodyId).ToListAsync();
            var sectionDtos = new List<SectionDto>();
            foreach (var section in sections)
            {
                var products = await BuildProductsAsync(db, section.SectionID, pkSecret, logger);
                var highlights = await BuildSectionExcerptHighlightsAsync(db, section.SectionID, pkSecret, logger);
                var media = await BuildObservationMediaAsync(db, section.SectionID, pkSecret, logger);
                var identifiedSubstances = await BuildIdentifiedSubstancesAsync(db, section.SectionID, pkSecret, logger);
                var productConcepts = await BuildProductConceptsAsync(db, section.SectionID, pkSecret, logger);
                var interactionIssues = await BuildInteractionIssuesAsync(db, section.SectionID, pkSecret, logger);
                var billingUnitIndexes = await BuildBillingUnitIndexesAsync(db, section.SectionID, pkSecret, logger);
                var warningLetterInfos = await BuildWarningLetterProductInfosAsync(db, section.SectionID, pkSecret, logger);
                var warningLetterDates = await BuildWarningLetterDatesAsync(db, section.SectionID, pkSecret, logger);
                var protocols = await BuildProtocolsAsync(db, section.SectionID, pkSecret, logger);
                var remsMaterials = await BuildREMSMaterialsAsync(db, section.SectionID, pkSecret, logger);
                var remsResources = await BuildREMSElectronicResourcesAsync(db, section.SectionID, pkSecret, logger);

                sectionDtos.Add(new SectionDto
                {
                    Section = section.ToEntityWithEncryptedId(pkSecret, logger),
                    Products = products,
                    ExcerptHighlights = highlights,
                    ObservationMedia = media,
                    IdentifiedSubstances = identifiedSubstances,
                    ProductConcepts = productConcepts,
                    InteractionIssues = interactionIssues,
                    BillingUnitIndexes = billingUnitIndexes,
                    WarningLetterProductInfos = warningLetterInfos,
                    WarningLetterDates = warningLetterDates,
                    Protocols = protocols,
                    REMSMaterials = remsMaterials,
                    REMSElectronicResources = remsResources
                });
            }
            return sectionDtos;
        }

        #endregion

        #region Section Children Builders

        private static async Task<List<SectionExcerptHighlightDto>> BuildSectionExcerptHighlightsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            if (sectionId == null) return new List<SectionExcerptHighlightDto>();
            var items = await db.Set<Label.SectionExcerptHighlight>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();
            return items.Select(item => new SectionExcerptHighlightDto { SectionExcerptHighlight = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<ObservationMediaDto>> BuildObservationMediaAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            if (sectionId == null) return new List<ObservationMediaDto>();
            var items = await db.Set<Label.ObservationMedia>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();
            return items.Select(item => new ObservationMediaDto { ObservationMedia = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<IdentifiedSubstanceDto>> BuildIdentifiedSubstancesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            if (sectionId == null) return new List<IdentifiedSubstanceDto>();
            var items = await db.Set<Label.IdentifiedSubstance>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();
            return items.Select(item => new IdentifiedSubstanceDto { IdentifiedSubstance = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<ProductConceptDto>> BuildProductConceptsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            if (sectionId == null) return new List<ProductConceptDto>();
            var items = await db.Set<Label.ProductConcept>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();
            return items.Select(item => new ProductConceptDto { ProductConcept = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<InteractionIssueDto>> BuildInteractionIssuesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            if (sectionId == null) return new List<InteractionIssueDto>();
            var items = await db.Set<Label.InteractionIssue>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();
            return items.Select(item => new InteractionIssueDto { InteractionIssue = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<BillingUnitIndexDto>> BuildBillingUnitIndexesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            if (sectionId == null) return new List<BillingUnitIndexDto>();
            var items = await db.Set<Label.BillingUnitIndex>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();
            return items.Select(item => new BillingUnitIndexDto { BillingUnitIndex = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<WarningLetterProductInfoDto>> BuildWarningLetterProductInfosAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            if (sectionId == null) return new List<WarningLetterProductInfoDto>();
            var items = await db.Set<Label.WarningLetterProductInfo>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();
            return items.Select(item => new WarningLetterProductInfoDto { WarningLetterProductInfo = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<WarningLetterDateDto>> BuildWarningLetterDatesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            if (sectionId == null) return new List<WarningLetterDateDto>();
            var items = await db.Set<Label.WarningLetterDate>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();
            return items.Select(item => new WarningLetterDateDto { WarningLetterDate = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<ProtocolDto>> BuildProtocolsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            if (sectionId == null) return new List<ProtocolDto>();
            var items = await db.Set<Label.Protocol>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();
            return items.Select(item => new ProtocolDto { Protocol = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<REMSMaterialDto>> BuildREMSMaterialsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            if (sectionId == null) return new List<REMSMaterialDto>();
            var items = await db.Set<Label.REMSMaterial>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();
            return items.Select(item => new REMSMaterialDto { REMSMaterial = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<REMSElectronicResourceDto>> BuildREMSElectronicResourcesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            if (sectionId == null) return new List<REMSElectronicResourceDto>();
            var items = await db.Set<Label.REMSElectronicResource>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();
            return items.Select(item => new REMSElectronicResourceDto { REMSElectronicResource = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        #endregion

        #region Product Builders
        private static async Task<List<ProductDto>> BuildProductsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            if (sectionId == null) return new List<ProductDto>();
            var products = await db.Set<Label.Product>().AsNoTracking().Where(p => p.SectionID == sectionId).ToListAsync();
            var productDtos = new List<ProductDto>();
            foreach (var product in products)
            {
                var genericMeds = await BuildGenericMedicinesAsync(db, product.ProductID, pkSecret, logger);
                var productIds = await BuildProductIdentifiersAsync(db, product.ProductID, pkSecret, logger);
                var productRoutes = await BuildProductRouteOfAdministrationsAsync(db, product.ProductID, pkSecret, logger);
                var webLinks = await BuildProductWebLinksAsync(db, product.ProductID, pkSecret, logger);
                var businessOpLinks = await BuildBusinessOperationProductLinksAsync(db, product.ProductID, pkSecret, logger);
                var respPersonLinks = await BuildResponsiblePersonLinksAsync(db, product.ProductID, pkSecret, logger);
                var productInstances = await BuildProductInstancesAsync(db, product.ProductID, pkSecret, logger);
                var ingredients = await BuildIngredientsAsync(db, product.ProductID, pkSecret, logger);

                productDtos.Add(new ProductDto
                {
                    Product = product.ToEntityWithEncryptedId(pkSecret, logger),
                    GenericMedicines = genericMeds,
                    ProductIdentifiers = productIds,
                    ProductRouteOfAdministrations = productRoutes,
                    ProductWebLinks = webLinks,
                    BusinessOperationProductLinks = businessOpLinks,
                    ResponsiblePersonLinks = respPersonLinks,
                    ProductInstances = productInstances,
                    Ingredients = ingredients
                });
            }
            return productDtos;
        }

        #endregion

        #region Product Children Builders

        private static async Task<List<IngredientDto>> BuildIngredientsAsync(
      ApplicationDbContext db,
      int? productId,
      string pkSecret,
      ILogger logger)
        {
            var ingredients = await db.Set<Label.Ingredient>()
                .AsNoTracking()
                .Where(i => i.ProductID == productId)
                .ToListAsync();

            var ingredientDtos = new List<IngredientDto>();
            foreach (var ingredient in ingredients)
            {
                if (ingredient == null || ingredient.IngredientID == null)
                    continue;

                // The correct FK is IngredientSubstanceID, not IngredientID
                var ingredientSubstance = await BuildIngredientSubstanceAsync(
                    db,
                    ingredient.IngredientSubstanceID,
                    pkSecret,
                    logger
                );

                ingredientDtos.Add(new IngredientDto
                {
                    Ingredient = ingredient.ToEntityWithEncryptedId(pkSecret, logger),
                    IngredientSubstance = ingredientSubstance
                });
            }
            return ingredientDtos;
        }


        private static async Task<IngredientSubstanceDto?> BuildIngredientSubstanceAsync(
            ApplicationDbContext db,
            int? ingredientSubstanceId,
            string pkSecret,
            ILogger logger)
        {
            if (ingredientSubstanceId == null)
                return null;

            var entity = await db.Set<Label.IngredientSubstance>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IngredientSubstanceID == ingredientSubstanceId);

            if (entity == null)
                return null;

            return new IngredientSubstanceDto
            {
                IngredientSubstance = entity.ToEntityWithEncryptedId(pkSecret, logger)
            };
        }


        private static async Task<List<GenericMedicineDto>> BuildGenericMedicinesAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            if (productId == null) return new List<GenericMedicineDto>();
            var items = await db.Set<Label.GenericMedicine>().AsNoTracking().Where(e => e.ProductID == productId).ToListAsync();
            return items.Select(item => new GenericMedicineDto { GenericMedicine = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<ProductIdentifierDto>> BuildProductIdentifiersAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            if (productId == null) return new List<ProductIdentifierDto>();
            var items = await db.Set<Label.ProductIdentifier>().AsNoTracking().Where(e => e.ProductID == productId).ToListAsync();
            return items.Select(item => new ProductIdentifierDto { ProductIdentifier = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<ProductRouteOfAdministrationDto>> BuildProductRouteOfAdministrationsAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            if (productId == null) return new List<ProductRouteOfAdministrationDto>();
            var items = await db.Set<Label.ProductRouteOfAdministration>().AsNoTracking().Where(e => e.ProductID == productId).ToListAsync();
            return items.Select(item => new ProductRouteOfAdministrationDto { ProductRouteOfAdministration = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<ProductWebLinkDto>> BuildProductWebLinksAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            if (productId == null) return new List<ProductWebLinkDto>();
            var items = await db.Set<Label.ProductWebLink>().AsNoTracking().Where(e => e.ProductID == productId).ToListAsync();
            return items.Select(item => new ProductWebLinkDto { ProductWebLink = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<BusinessOperationProductLinkDto>> BuildBusinessOperationProductLinksAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            if (productId == null) return new List<BusinessOperationProductLinkDto>();
            var items = await db.Set<Label.BusinessOperationProductLink>().AsNoTracking().Where(e => e.ProductID == productId).ToListAsync();
            return items.Select(item => new BusinessOperationProductLinkDto { BusinessOperationProductLink = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<ResponsiblePersonLinkDto>> BuildResponsiblePersonLinksAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            if (productId == null) return new List<ResponsiblePersonLinkDto>();
            var items = await db.Set<Label.ResponsiblePersonLink>().AsNoTracking().Where(e => e.ProductID == productId).ToListAsync();
            return items.Select(item => new ResponsiblePersonLinkDto { ResponsiblePersonLink = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<ProductInstanceDto>> BuildProductInstancesAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            if (productId == null) return new List<ProductInstanceDto>();
            var instances = await db.Set<Label.ProductInstance>().AsNoTracking().Where(e => e.ProductID == productId).ToListAsync();
            var dtos = new List<ProductInstanceDto>();
            foreach (var instance in instances)
            {
                var parentHierarchies = await BuildLotHierarchiesAsParentAsync(db, instance.ProductInstanceID, pkSecret, logger);
                var childHierarchies = await BuildLotHierarchiesAsChildAsync(db, instance.ProductInstanceID, pkSecret, logger);
                dtos.Add(new ProductInstanceDto
                {
                    ProductInstance = instance.ToEntityWithEncryptedId(pkSecret, logger),
                    ParentHierarchies = parentHierarchies,
                    ChildHierarchies = childHierarchies
                });
            }
            return dtos;
        }

        #endregion

        #region DocumentRelationship Children Builders

        private static async Task<List<BusinessOperationDto>> BuildBusinessOperationsAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            if (docRelId == null) return new List<BusinessOperationDto>();
            var items = await db.Set<Label.BusinessOperation>().AsNoTracking().Where(e => e.DocumentRelationshipID == docRelId).ToListAsync();
            return items.Select(item => new BusinessOperationDto { BusinessOperation = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<CertificationProductLinkDto>> BuildCertificationProductLinksAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            if (docRelId == null) return new List<CertificationProductLinkDto>();
            var items = await db.Set<Label.CertificationProductLink>().AsNoTracking().Where(e => e.DocumentRelationshipID == docRelId).ToListAsync();
            return items.Select(item => new CertificationProductLinkDto { CertificationProductLink = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<ComplianceActionDto>> BuildComplianceActionsForRelationshipAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            if (docRelId == null) return new List<ComplianceActionDto>();
            var items = await db.Set<Label.ComplianceAction>().AsNoTracking().Where(e => e.DocumentRelationshipID == docRelId).ToListAsync();
            return items.Select(item => new ComplianceActionDto { ComplianceAction = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<FacilityProductLinkDto>> BuildFacilityProductLinksAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            if (docRelId == null) return new List<FacilityProductLinkDto>();
            var items = await db.Set<Label.FacilityProductLink>().AsNoTracking().Where(e => e.DocumentRelationshipID == docRelId).ToListAsync();
            return items.Select(item => new FacilityProductLinkDto { FacilityProductLink = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        #endregion

        #region Miscellaneous Builders

        private static async Task<List<LotHierarchyDto>> BuildLotHierarchiesAsParentAsync(ApplicationDbContext db, int? parentInstanceId, string pkSecret, ILogger logger)
        {
            if (parentInstanceId == null) return new List<LotHierarchyDto>();
            var items = await db.Set<Label.LotHierarchy>().AsNoTracking().Where(e => e.ParentInstanceID == parentInstanceId).ToListAsync();
            return items.Select(item => new LotHierarchyDto { LotHierarchy = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        private static async Task<List<LotHierarchyDto>> BuildLotHierarchiesAsChildAsync(ApplicationDbContext db, int? childInstanceId, string pkSecret, ILogger logger)
        {
            if (childInstanceId == null) return new List<LotHierarchyDto>();
            var items = await db.Set<Label.LotHierarchy>().AsNoTracking().Where(e => e.ChildInstanceID == childInstanceId).ToListAsync();
            return items.Select(item => new LotHierarchyDto { LotHierarchy = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
        }

        #endregion
    }
}