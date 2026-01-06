using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using static MedRecPro.Models.Label;

namespace MedRecPro.DataAccess
{
    /// <summary>
    /// Provides batch loading methods for building DTOs from SPL Label entities.
    /// These methods fetch multiple entities in single database queries to minimize
    /// connection overhead and eliminate N+1 query patterns.
    /// </summary>
    /// <remarks>
    /// All batch methods return Dictionary&lt;int, List&lt;TDto&gt;&gt; where the key is the parent ID
    /// and the value is the list of child DTOs. This enables O(1) lookup during DTO assembly.
    /// </remarks>
    /// <seealso cref="Label"/>
    /// <seealso cref="DocumentDto"/>
    public static partial class DtoLabelAccess
    {
        #region Document Level Batch Loaders

        /**************************************************************/
        /// <summary>
        /// Batch loads StructuredBody entities for multiple document IDs in a single query.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="documentIds">Collection of document IDs to load structured bodies for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>Dictionary mapping DocumentID to list of StructuredBodyDto.</returns>
        private static async Task<Dictionary<int, List<StructuredBodyDto>>> batchLoadStructuredBodiesAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> documentIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (documentIds == null || !documentIds.Any())
                return new Dictionary<int, List<StructuredBodyDto>>();

            // Single query to fetch all structured bodies for all document IDs
            var entities = await db.Set<Label.StructuredBody>()
                .AsNoTracking()
                .Where(e => e.DocumentID != null && documentIds.Contains((int)e.DocumentID))
                .ToListAsync();

            // Collect all structured body IDs for batch loading sections
            var structuredBodyIds = entities
                .Where(e => e.StructuredBodyID != null)
                .Select(e => (int)e.StructuredBodyID!)
                .ToList();

            // Batch load all sections for all structured bodies
            var allSections = await batchLoadSectionsAsync(db, structuredBodyIds, pkSecret, logger);

            // Batch load section hierarchies
            var sectionIds = allSections.Values
                .SelectMany(s => s)
                .Where(s => s.SectionID != null && s.SectionID > 0)
                .Select(s => (int)s.SectionID!)
                .Distinct()
                .ToList();

            var allSectionHierarchies = await batchLoadSectionHierarchiesAsync(db, sectionIds, pkSecret, logger);

            // Group by DocumentID and build DTOs
            var result = new Dictionary<int, List<StructuredBodyDto>>();

            foreach (var group in entities.GroupBy(e => e.DocumentID!.Value))
            {
                var sbDtos = new List<StructuredBodyDto>();

                foreach (var sb in group)
                {
                    var sectionDtos = allSections.GetValueOrDefault((int)sb.StructuredBodyID!) ?? new List<SectionDto>();
                    var sectionIdsForSb = sectionDtos
                        .Where(s => s.SectionID != null && s.SectionID > 0)
                        .Select(s => (int)s.SectionID!)
                        .ToList();

                    var hierarchies = allSectionHierarchies
                        .Where(kvp => sectionIdsForSb.Contains(kvp.Key))
                        .SelectMany(kvp => kvp.Value)
                        .ToList();

                    sbDtos.Add(new StructuredBodyDto(pkSecret)
                    {
                        StructuredBody = sb.ToEntityWithEncryptedId(pkSecret, logger),
                        Sections = sectionDtos,
                        SectionHierarchies = hierarchies
                    });
                }

                result[group.Key] = sbDtos;
            }

            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads DocumentAuthor entities for multiple document IDs in a single query.
        /// </summary>
        private static async Task<Dictionary<int, List<DocumentAuthorDto>>> batchLoadDocumentAuthorsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> documentIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (documentIds == null || !documentIds.Any())
                return new Dictionary<int, List<DocumentAuthorDto>>();

            var entities = await db.Set<Label.DocumentAuthor>()
                .AsNoTracking()
                .Where(e => e.DocumentID != null && documentIds.Contains((int)e.DocumentID))
                .ToListAsync();

            // Collect organization IDs for batch loading
            var organizationIds = entities
                .Where(e => e.OrganizationID != null)
                .Select(e => (int)e.OrganizationID!)
                .Distinct()
                .ToList();

            // Batch load all organizations
            var allOrganizations = await batchLoadOrganizationsAsync(db, organizationIds, pkSecret, logger);

            // Group by DocumentID and build DTOs
            return entities
                .GroupBy(e => e.DocumentID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(item => new DocumentAuthorDto
                    {
                        DocumentAuthor = item.ToEntityWithEncryptedId(pkSecret, logger),
                        Organization = item.OrganizationID != null
                            ? allOrganizations.GetValueOrDefault((int)item.OrganizationID)
                            : null
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads RelatedDocument entities for multiple document IDs in a single query.
        /// </summary>
        private static async Task<Dictionary<int, List<RelatedDocumentDto>>> batchLoadRelatedDocumentsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> documentIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (documentIds == null || !documentIds.Any())
                return new Dictionary<int, List<RelatedDocumentDto>>();

            var entities = await db.Set<Label.RelatedDocument>()
                .AsNoTracking()
                .Where(e => e.SourceDocumentID != null && documentIds.Contains((int)e.SourceDocumentID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.SourceDocumentID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(item => new RelatedDocumentDto
                    {
                        RelatedDocument = item.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads LegalAuthenticator entities for multiple document IDs in a single query.
        /// </summary>
        private static async Task<Dictionary<int, List<LegalAuthenticatorDto>>> batchLoadLegalAuthenticatorsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> documentIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (documentIds == null || !documentIds.Any())
                return new Dictionary<int, List<LegalAuthenticatorDto>>();

            var entities = await db.Set<Label.LegalAuthenticator>()
                .AsNoTracking()
                .Where(e => e.DocumentID != null && documentIds.Contains((int)e.DocumentID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.DocumentID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(item => new LegalAuthenticatorDto
                    {
                        LegalAuthenticator = item.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads DocumentRelationship entities for multiple document IDs with all children.
        /// </summary>
        private static async Task<Dictionary<int, List<DocumentRelationshipDto>>> batchLoadDocumentRelationshipsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> documentIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (documentIds == null || !documentIds.Any())
                return new Dictionary<int, List<DocumentRelationshipDto>>();

            var entities = await db.Set<Label.DocumentRelationship>()
                .AsNoTracking()
                .Where(e => e.DocumentID != null && documentIds.Contains((int)e.DocumentID))
                .ToListAsync();

            var relationshipIds = entities
                .Where(e => e.DocumentRelationshipID != null)
                .Select(e => (int)e.DocumentRelationshipID!)
                .ToList();

            // Collect organization IDs for batch loading
            var organizationIds = entities
                .SelectMany(e => new[] { e.ChildOrganizationID, e.ParentOrganizationID })
                .Where(id => id != null)
                .Select(id => (int)id!)
                .Distinct()
                .ToList();

            // Batch load all children
            var allOrganizations = await batchLoadOrganizationsAsync(db, organizationIds, pkSecret, logger);
            var allBusinessOps = await batchLoadBusinessOperationsAsync(db, relationshipIds, pkSecret, logger);
            var allCertLinks = await batchLoadCertificationProductLinksAsync(db, relationshipIds, pkSecret, logger);
            var allComplianceActions = await batchLoadComplianceActionsForRelationshipsAsync(db, relationshipIds, pkSecret, logger);
            var allFacilityLinks = await batchLoadFacilityProductLinksAsync(db, relationshipIds, pkSecret, logger);
            var allRelIdentifiers = await batchLoadDocumentRelationshipIdentifiersAsync(db, relationshipIds, pkSecret, logger);

            // Group by DocumentID and build DTOs
            return entities
                .GroupBy(e => e.DocumentID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(rel => new DocumentRelationshipDto
                    {
                        DocumentRelationship = rel.ToEntityWithEncryptedId(pkSecret, logger),
                        ChildOrganization = rel.ChildOrganizationID != null
                            ? allOrganizations.GetValueOrDefault((int)rel.ChildOrganizationID)
                            : null,
                        ParentOrganization = rel.ParentOrganizationID != null
                            ? allOrganizations.GetValueOrDefault((int)rel.ParentOrganizationID)
                            : null,
                        BusinessOperations = allBusinessOps.GetValueOrDefault((int)rel.DocumentRelationshipID!) ?? new(),
                        CertificationProductLinks = allCertLinks.GetValueOrDefault((int)rel.DocumentRelationshipID!) ?? new(),
                        ComplianceActions = allComplianceActions.GetValueOrDefault((int)rel.DocumentRelationshipID!) ?? new(),
                        FacilityProductLinks = allFacilityLinks.GetValueOrDefault((int)rel.DocumentRelationshipID!) ?? new(),
                        RelationshipIdentifiers = allRelIdentifiers.GetValueOrDefault((int)rel.DocumentRelationshipID!) ?? new()
                    }).ToList()
                );

            #endregion
        }

        #endregion

        #region Section Level Batch Loaders

        /**************************************************************/
        /// <summary>
        /// Batch loads Section entities for multiple structured body IDs with all children.
        /// This is the highest-impact batch loader as sections have 17 child collections.
        /// </summary>
        private static async Task<Dictionary<int, List<SectionDto>>> batchLoadSectionsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> structuredBodyIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (structuredBodyIds == null || !structuredBodyIds.Any())
                return new Dictionary<int, List<SectionDto>>();

            // Fetch all sections for all structured bodies in single query
            var sections = await db.Set<Label.Section>()
                .AsNoTracking()
                .Where(s => s.StructuredBodyID != null && structuredBodyIds.Contains((int)s.StructuredBodyID))
                .ToListAsync();

            if (!sections.Any())
                return new Dictionary<int, List<SectionDto>>();

            // Collect all section IDs
            var sectionIds = sections
                .Where(s => s.SectionID != null)
                .Select(s => (int)s.SectionID!)
                .ToList();

            // Batch load ALL section children in parallel-safe sequential calls
            var allBillingUnits = await batchLoadBillingUnitIndexesAsync(db, sectionIds, pkSecret, logger);
            var allChildHierarchies = await batchLoadChildSectionHierarchiesAsync(db, sectionIds, pkSecret, logger);
            var allParentHierarchies = await batchLoadParentSectionHierarchiesAsync(db, sectionIds, pkSecret, logger);
            var allTextContents = await batchLoadSectionTextContentsAsync(db, sectionIds, pkSecret, logger);
            var allHighlights = await batchLoadSectionExcerptHighlightsAsync(db, sectionIds, pkSecret, logger);
            var allSubstances = await batchLoadIdentifiedSubstancesAsync(db, sectionIds, pkSecret, logger);
            var allInteractions = await batchLoadInteractionIssuesAsync(db, sectionIds, pkSecret, logger);
            var allMedia = await batchLoadObservationMediaAsync(db, sectionIds, pkSecret, logger);
            var allNCTLinks = await batchLoadNCTLinksAsync(db, sectionIds, pkSecret, logger);
            var allProductConcepts = await batchLoadProductConceptsAsync(db, sectionIds, pkSecret, logger);
            var allProducts = await batchLoadProductsAsync(db, sectionIds, pkSecret, logger);
            var allProtocols = await batchLoadProtocolsAsync(db, sectionIds, pkSecret, logger);
            var allRemsMaterials = await batchLoadREMSMaterialsAsync(db, sectionIds, pkSecret, logger);
            var allRemsResources = await batchLoadREMSElectronicResourcesAsync(db, sectionIds, pkSecret, logger);
            var allWarningDates = await batchLoadWarningLetterDatesAsync(db, sectionIds, pkSecret, logger);
            var allWarningInfos = await batchLoadWarningLetterProductInfosAsync(db, sectionIds, pkSecret, logger);

            // Group by StructuredBodyID and build DTOs
            return sections
                .GroupBy(s => s.StructuredBodyID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(section => new SectionDto(pkSecret)
                    {
                        Section = section.ToEntityWithEncryptedId(pkSecret, logger),
                        BillingUnitIndexes = allBillingUnits.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        ChildSectionHierarchies = allChildHierarchies.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        ParentSectionHierarchies = allParentHierarchies.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        TextContents = allTextContents.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        ExcerptHighlights = allHighlights.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        IdentifiedSubstances = allSubstances.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        InteractionIssues = allInteractions.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        ObservationMedia = allMedia.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        NCTLinks = allNCTLinks.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        ProductConcepts = allProductConcepts.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        Products = allProducts.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        Protocols = allProtocols.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        REMSMaterials = allRemsMaterials.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        REMSElectronicResources = allRemsResources.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        WarningLetterDates = allWarningDates.GetValueOrDefault((int)section.SectionID!) ?? new(),
                        WarningLetterProductInfos = allWarningInfos.GetValueOrDefault((int)section.SectionID!) ?? new()
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads SectionHierarchy entities for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<SectionHierarchyDto>>> batchLoadSectionHierarchiesAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<SectionHierarchyDto>>();

            var entities = await db.Set<Label.SectionHierarchy>()
                .AsNoTracking()
                .Where(e => e.ParentSectionID != null && sectionIds.Contains((int)e.ParentSectionID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.ParentSectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new SectionHierarchyDto(pkSecret)
                    {
                        SectionHierarchy = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads BillingUnitIndex entities for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<BillingUnitIndexDto>>> batchLoadBillingUnitIndexesAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<BillingUnitIndexDto>>();

            var entities = await db.Set<Label.BillingUnitIndex>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new BillingUnitIndexDto
                    {
                        BillingUnitIndex = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads child SectionHierarchy entities for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<SectionHierarchyDto>>> batchLoadChildSectionHierarchiesAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<SectionHierarchyDto>>();

            var entities = await db.Set<Label.SectionHierarchy>()
                .AsNoTracking()
                .Where(e => e.ChildSectionID != null && sectionIds.Contains((int)e.ChildSectionID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.ChildSectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new SectionHierarchyDto
                    {
                        SectionHierarchy = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads parent SectionHierarchy entities for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<SectionHierarchyDto>>> batchLoadParentSectionHierarchiesAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<SectionHierarchyDto>>();

            var entities = await db.Set<Label.SectionHierarchy>()
                .AsNoTracking()
                .Where(e => e.ParentSectionID != null && sectionIds.Contains((int)e.ParentSectionID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.ParentSectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new SectionHierarchyDto(pkSecret)
                    {
                        SectionHierarchy = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads SectionTextContent entities with children for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<SectionTextContentDto>>> batchLoadSectionTextContentsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<SectionTextContentDto>>();

            var entities = await db.Set<Label.SectionTextContent>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            if (!entities.Any())
                return new Dictionary<int, List<SectionTextContentDto>>();

            // Collect text content IDs for batch loading children
            var textContentIds = entities
                .Where(e => e.SectionTextContentID != null)
                .Select(e => (int)e.SectionTextContentID!)
                .ToList();

            // Batch load all children
            var allRenderedMedias = await batchLoadRenderedMediasAsync(db, textContentIds, pkSecret, logger);
            var allTextTables = await batchLoadTextTablesAsync(db, textContentIds, pkSecret, logger);
            var allTextLists = await batchLoadTextListsAsync(db, textContentIds, pkSecret, logger);

            // Group by SectionID and build DTOs
            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new SectionTextContentDto
                    {
                        SectionTextContent = e.ToEntityWithEncryptedId(pkSecret, logger),
                        RenderedMedias = allRenderedMedias.GetValueOrDefault((int)e.SectionTextContentID!) ?? new(),
                        TextTables = allTextTables.GetValueOrDefault((int)e.SectionTextContentID!) ?? new(),
                        TextLists = allTextLists.GetValueOrDefault((int)e.SectionTextContentID!) ?? new()
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads RenderedMedia entities for multiple text content IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<RenderedMediaDto>>> batchLoadRenderedMediasAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> textContentIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (textContentIds == null || !textContentIds.Any())
                return new Dictionary<int, List<RenderedMediaDto>>();

            var entities = await db.Set<Label.RenderedMedia>()
                .AsNoTracking()
                .Where(e => e.SectionTextContentID != null && textContentIds.Contains((int)e.SectionTextContentID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.SectionTextContentID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new RenderedMediaDto
                    {
                        RenderedMedia = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads TextTable entities with children for multiple text content IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<TextTableDto>>> batchLoadTextTablesAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> textContentIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (textContentIds == null || !textContentIds.Any())
                return new Dictionary<int, List<TextTableDto>>();

            var entities = await db.Set<Label.TextTable>()
                .AsNoTracking()
                .Where(e => e.SectionTextContentID != null && textContentIds.Contains((int)e.SectionTextContentID))
                .ToListAsync();

            if (!entities.Any())
                return new Dictionary<int, List<TextTableDto>>();

            var tableIds = entities
                .Where(e => e.TextTableID != null)
                .Select(e => (int)e.TextTableID!)
                .ToList();

            // Batch load columns and rows
            var allColumns = await batchLoadTextTableColumnsAsync(db, tableIds, pkSecret, logger);
            var allRows = await batchLoadTextTableRowsAsync(db, tableIds, pkSecret, logger);

            return entities
                .GroupBy(e => e.SectionTextContentID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new TextTableDto
                    {
                        TextTable = e.ToEntityWithEncryptedId(pkSecret, logger),
                        TextTableColumns = allColumns.GetValueOrDefault((int)e.TextTableID!) ?? new(),
                        TextTableRows = allRows.GetValueOrDefault((int)e.TextTableID!) ?? new()
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads TextTableColumn entities for multiple table IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<TextTableColumnDto>>> batchLoadTextTableColumnsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> tableIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (tableIds == null || !tableIds.Any())
                return new Dictionary<int, List<TextTableColumnDto>>();

            var entities = await db.Set<Label.TextTableColumn>()
                .AsNoTracking()
                .Where(e => e.TextTableID != null && tableIds.Contains((int)e.TextTableID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.TextTableID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new TextTableColumnDto
                    {
                        TextTableColumn = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads TextTableRow entities with cells for multiple table IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<TextTableRowDto>>> batchLoadTextTableRowsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> tableIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (tableIds == null || !tableIds.Any())
                return new Dictionary<int, List<TextTableRowDto>>();

            var entities = await db.Set<Label.TextTableRow>()
                .AsNoTracking()
                .Where(e => e.TextTableID != null && tableIds.Contains((int)e.TextTableID))
                .ToListAsync();

            if (!entities.Any())
                return new Dictionary<int, List<TextTableRowDto>>();

            var rowIds = entities
                .Where(e => e.TextTableRowID != null)
                .Select(e => (int)e.TextTableRowID!)
                .ToList();

            // Batch load cells
            var allCells = await batchLoadTextTableCellsAsync(db, rowIds, pkSecret, logger);

            return entities
                .GroupBy(e => e.TextTableID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new TextTableRowDto
                    {
                        TextTableRow = e.ToEntityWithEncryptedId(pkSecret, logger),
                        TextTableCells = allCells.GetValueOrDefault((int)e.TextTableRowID!) ?? new()
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads TextTableCell entities for multiple row IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<TextTableCellDto>>> batchLoadTextTableCellsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> rowIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (rowIds == null || !rowIds.Any())
                return new Dictionary<int, List<TextTableCellDto>>();

            var entities = await db.Set<Label.TextTableCell>()
                .AsNoTracking()
                .Where(e => e.TextTableRowID != null && rowIds.Contains((int)e.TextTableRowID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.TextTableRowID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new TextTableCellDto
                    {
                        TextTableCell = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads TextList entities with items for multiple text content IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<TextListDto>>> batchLoadTextListsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> textContentIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (textContentIds == null || !textContentIds.Any())
                return new Dictionary<int, List<TextListDto>>();

            var entities = await db.Set<Label.TextList>()
                .AsNoTracking()
                .Where(e => e.SectionTextContentID != null && textContentIds.Contains((int)e.SectionTextContentID))
                .ToListAsync();

            if (!entities.Any())
                return new Dictionary<int, List<TextListDto>>();

            var listIds = entities
                .Where(e => e.TextListID != null)
                .Select(e => (int)e.TextListID!)
                .ToList();

            // Batch load items
            var allItems = await batchLoadTextListItemsAsync(db, listIds, pkSecret, logger);

            return entities
                .GroupBy(e => e.SectionTextContentID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new TextListDto
                    {
                        TextList = e.ToEntityWithEncryptedId(pkSecret, logger),
                        TextListItems = allItems.GetValueOrDefault((int)e.TextListID!) ?? new()
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads TextListItem entities for multiple list IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<TextListItemDto>>> batchLoadTextListItemsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> listIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (listIds == null || !listIds.Any())
                return new Dictionary<int, List<TextListItemDto>>();

            var entities = await db.Set<Label.TextListItem>()
                .AsNoTracking()
                .Where(e => e.TextListID != null && listIds.Contains((int)e.TextListID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.TextListID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new TextListItemDto
                    {
                        TextListItem = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads SectionExcerptHighlight entities for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<SectionExcerptHighlightDto>>> batchLoadSectionExcerptHighlightsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<SectionExcerptHighlightDto>>();

            var entities = await db.Set<Label.SectionExcerptHighlight>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new SectionExcerptHighlightDto
                    {
                        SectionExcerptHighlight = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads ObservationMedia entities for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<ObservationMediaDto>>> batchLoadObservationMediaAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<ObservationMediaDto>>();

            var entities = await db.Set<Label.ObservationMedia>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new ObservationMediaDto
                    {
                        ObservationMedia = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads NCTLink entities for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<NCTLinkDto>>> batchLoadNCTLinksAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<NCTLinkDto>>();

            var entities = await db.Set<Label.NCTLink>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new NCTLinkDto
                    {
                        NCTLink = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads ProductConcept entities with equivalences for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<ProductConceptDto>>> batchLoadProductConceptsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<ProductConceptDto>>();

            var entities = await db.Set<Label.ProductConcept>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            if (!entities.Any())
                return new Dictionary<int, List<ProductConceptDto>>();

            var conceptIds = entities
                .Where(e => e.ProductConceptID != null)
                .Select(e => (int)e.ProductConceptID!)
                .ToList();

            var allEquivalences = await batchLoadProductConceptEquivalencesAsync(db, conceptIds, pkSecret, logger);

            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new ProductConceptDto
                    {
                        ProductConcept = e.ToEntityWithEncryptedId(pkSecret, logger),
                        ProductConceptEquivalences = allEquivalences.GetValueOrDefault((int)e.ProductConceptID!) ?? new()
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads ProductConceptEquivalence entities for multiple concept IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<ProductConceptEquivalenceDto>>> batchLoadProductConceptEquivalencesAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> conceptIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (conceptIds == null || !conceptIds.Any())
                return new Dictionary<int, List<ProductConceptEquivalenceDto>>();

            var entities = await db.Set<Label.ProductConceptEquivalence>()
                .AsNoTracking()
                .Where(e => e.ProductConceptEquivalenceID != null && conceptIds.Contains((int)e.ProductConceptEquivalenceID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.ProductConceptEquivalenceID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new ProductConceptEquivalenceDto
                    {
                        ProductConcept = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads Protocol entities with children for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<ProtocolDto>>> batchLoadProtocolsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<ProtocolDto>>();

            var entities = await db.Set<Label.Protocol>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            if (!entities.Any())
                return new Dictionary<int, List<ProtocolDto>>();

            var protocolIds = entities
                .Where(e => e.ProtocolID != null)
                .Select(e => (int)e.ProtocolID!)
                .ToList();

            var allApprovals = await batchLoadREMSApprovalsAsync(db, protocolIds, pkSecret, logger);
            var allRequirements = await batchLoadRequirementsAsync(db, protocolIds, pkSecret, logger);

            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new ProtocolDto
                    {
                        Protocol = e.ToEntityWithEncryptedId(pkSecret, logger),
                        REMSApprovals = allApprovals.GetValueOrDefault((int)e.ProtocolID!) ?? new(),
                        Requirements = allRequirements.GetValueOrDefault((int)e.ProtocolID!) ?? new()
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads REMSApproval entities for multiple protocol IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<REMSApprovalDto>>> batchLoadREMSApprovalsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> protocolIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (protocolIds == null || !protocolIds.Any())
                return new Dictionary<int, List<REMSApprovalDto>>();

            var entities = await db.Set<Label.REMSApproval>()
                .AsNoTracking()
                .Where(e => e.ProtocolID != null && protocolIds.Contains((int)e.ProtocolID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.ProtocolID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new REMSApprovalDto
                    {
                        REMSApproval = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads Requirement entities with stakeholders for multiple protocol IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<RequirementDto>>> batchLoadRequirementsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> protocolIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (protocolIds == null || !protocolIds.Any())
                return new Dictionary<int, List<RequirementDto>>();

            var entities = await db.Set<Label.Requirement>()
                .AsNoTracking()
                .Where(e => e.ProtocolID != null && protocolIds.Contains((int)e.ProtocolID))
                .ToListAsync();

            if (!entities.Any())
                return new Dictionary<int, List<RequirementDto>>();

            var stakeholderIds = entities
                .Where(e => e.StakeholderID != null)
                .Select(e => (int)e.StakeholderID!)
                .Distinct()
                .ToList();

            var allStakeholders = await batchLoadStakeholdersAsync(db, stakeholderIds, pkSecret, logger);

            return entities
                .GroupBy(e => e.ProtocolID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new RequirementDto
                    {
                        Requirement = e.ToEntityWithEncryptedId(pkSecret, logger),
                        Stakeholders = e.StakeholderID != null
                            ? allStakeholders.GetValueOrDefault((int)e.StakeholderID) ?? new()
                            : new()
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads Stakeholder entities for multiple stakeholder IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<StakeholderDto>>> batchLoadStakeholdersAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> stakeholderIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (stakeholderIds == null || !stakeholderIds.Any())
                return new Dictionary<int, List<StakeholderDto>>();

            var entities = await db.Set<Label.Stakeholder>()
                .AsNoTracking()
                .Where(e => e.StakeholderID != null && stakeholderIds.Contains((int)e.StakeholderID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.StakeholderID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new StakeholderDto
                    {
                        Stakeholder = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads REMSMaterial entities with attachments for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<REMSMaterialDto>>> batchLoadREMSMaterialsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<REMSMaterialDto>>();

            var entities = await db.Set<Label.REMSMaterial>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            if (!entities.Any())
                return new Dictionary<int, List<REMSMaterialDto>>();

            var materialIds = entities
                .Where(e => e.REMSMaterialID != null)
                .Select(e => (int)e.REMSMaterialID!)
                .ToList();

            var allAttachments = await batchLoadAttachedDocumentsAsync(db, materialIds, pkSecret, logger);

            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new REMSMaterialDto
                    {
                        REMSMaterial = e.ToEntityWithEncryptedId(pkSecret, logger),
                        AttachedDocuments = e.REMSMaterialID != null
                            ? allAttachments.GetValueOrDefault((int)e.REMSMaterialID) ?? new()
                            : new()
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads AttachedDocument entities for multiple parent entity IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<AttachedDocumentDto>>> batchLoadAttachedDocumentsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> parentEntityIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (parentEntityIds == null || !parentEntityIds.Any())
                return new Dictionary<int, List<AttachedDocumentDto>>();

            var entities = await db.Set<Label.AttachedDocument>()
                .AsNoTracking()
                .Where(e => e.ParentEntityID != null && parentEntityIds.Contains((int)e.ParentEntityID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.ParentEntityID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new AttachedDocumentDto
                    {
                        AttachedDocument = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads REMSElectronicResource entities for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<REMSElectronicResourceDto>>> batchLoadREMSElectronicResourcesAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<REMSElectronicResourceDto>>();

            var entities = await db.Set<Label.REMSElectronicResource>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new REMSElectronicResourceDto
                    {
                        REMSElectronicResource = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads WarningLetterDate entities for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<WarningLetterDateDto>>> batchLoadWarningLetterDatesAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<WarningLetterDateDto>>();

            var entities = await db.Set<Label.WarningLetterDate>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new WarningLetterDateDto
                    {
                        WarningLetterDate = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads WarningLetterProductInfo entities for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<WarningLetterProductInfoDto>>> batchLoadWarningLetterProductInfosAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<WarningLetterProductInfoDto>>();

            var entities = await db.Set<Label.WarningLetterProductInfo>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new WarningLetterProductInfoDto
                    {
                        WarningLetterProductInfo = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        #endregion

        #region Product Level Batch Loaders

        /**************************************************************/
        /// <summary>
        /// Batch loads Product entities with all 23 child collections for multiple section IDs.
        /// Excludes products that are Kit part products since those should only be rendered
        /// within their Kit's &lt;part&gt; elements.
        /// This is the second highest-impact batch loader.
        /// </summary>
        /// <remarks>
        /// Kit part product identification uses a view-based approach:
        /// 1. Identify Kit products by FormCode = C47916
        /// 2. Find other products in the same section without marketing data
        /// 3. These are inferred as Kit parts and excluded from top-level rendering
        ///
        /// This approach is more reliable than relying solely on the ProductPart table
        /// which may have data integrity issues (e.g., self-references).
        ///
        /// Backward compatibility is maintained:
        /// - Non-Kit sections are unaffected
        /// - Products with their own marketing data are never excluded
        /// </remarks>
        /// <seealso cref="identifyKitPartProductsAsync"/>
        /// <seealso cref="batchLoadProductPartsAsync"/>
        private static async Task<Dictionary<int, List<ProductDto>>> batchLoadProductsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<ProductDto>>();

            // Load ALL products for the sections first (we'll filter after Kit analysis)
            var allProducts = await db.Set<Label.Product>()
                .AsNoTracking()
                .Where(p => p.SectionID != null && sectionIds.Contains((int)p.SectionID))
                .ToListAsync();

            if (!allProducts.Any())
                return new Dictionary<int, List<ProductDto>>();

            // Identify Kit products and build mapping of Kit ProductID -> SectionID
            var kitProducts = allProducts.Where(p => isKitProduct(p) && p.ProductID.HasValue).ToList();
            var kitProductIds = kitProducts.Select(p => p.ProductID!.Value).ToList();
            var kitSectionIds = kitProducts.ToDictionary(p => p.ProductID!.Value, p => p.SectionID!.Value);

            // Use view-based logic to identify Kit part products
            var kitPartsMapping = await identifyKitPartProductsAsync(db, kitProductIds, kitSectionIds, allProducts);

            // Get all part product IDs that should be excluded from top-level rendering
            var partProductIdsToExclude = getPartProductIdsToExclude(kitPartsMapping);

            // Filter products: exclude part products (they'll be rendered inside Kit's <part> elements)
            var products = allProducts
                .Where(p => !p.ProductID.HasValue || !partProductIdsToExclude.Contains(p.ProductID.Value))
                .ToList();

            if (!products.Any())
                return new Dictionary<int, List<ProductDto>>();

            var productIds = products
                .Where(p => p.ProductID != null)
                .Select(p => (int)p.ProductID!)
                .ToList();

            // Batch load all product children
            var allAdditionalIds = await batchLoadAdditionalIdentifiersAsync(db, productIds, pkSecret, logger);
            var allBusinessOpLinks = await batchLoadBusinessOperationProductLinksAsync(db, productIds, pkSecret, logger);
            var allCharacteristics = await batchLoadProductCharacteristicsAsync(db, productIds, pkSecret, logger);
            var allChildLots = await batchLoadLabelLotHierarchiesAsync(db, productIds, pkSecret, logger);
            var allDosingSpecs = await batchLoadDosingSpecificationsAsync(db, productIds, pkSecret, logger);
            var allEquivalents = await batchLoadEquivalentEntitiesAsync(db, productIds, pkSecret, logger);
            var allGenericMeds = await batchLoadGenericMedicinesAsync(db, productIds, pkSecret, logger);
            var allIngredientInstances = await batchLoadProductIngredientInstancesAsync(db, productIds, pkSecret, logger);
            var allIngredients = await batchLoadIngredientsAsync(db, productIds, pkSecret, logger);
            var allMarketingCats = await batchLoadMarketingCategoriesAsync(db, productIds, pkSecret, logger);
            var allMarketingStatuses = await batchLoadProductMarketingStatusesAsync(db, productIds, pkSecret, logger);
            var allPackageLevels = await batchLoadPackagingLevelsAsync(db, productIds, sectionIds.FirstOrDefault(), pkSecret, logger);
            var allParentLots = await batchLoadFillLotHierarchiesAsync(db, productIds, pkSecret, logger);
            var allPartsOfAssembly = await batchLoadPartOfAssembliesAsync(db, productIds, pkSecret, logger);
            var allPolicies = await batchLoadPoliciesAsync(db, productIds, pkSecret, logger);
            var allProductIds = await batchLoadProductIdentifiersAsync(db, productIds, pkSecret, logger);
            var allProductInstances = await batchLoadProductInstancesAsync(db, productIds, pkSecret, logger);
            var allProductParts = await batchLoadProductPartsAsync(db, productIds, pkSecret, logger);
            var allProductRoutes = await batchLoadProductRoutesAsync(db, productIds, pkSecret, logger);
            var allRespPersonLinks = await batchLoadResponsiblePersonLinksAsync(db, productIds, pkSecret, logger);
            var allSpecializedKinds = await batchLoadSpecializedKindsAsync(db, productIds, pkSecret, logger);
            var allWebLinks = await batchLoadProductWebLinksAsync(db, productIds, pkSecret, logger);

            // Batch load PackageIdentifiers via join with PackagingLevel -> Product
            var allPackageIdentifiers = await batchLoadDirectPackageIdentifiersAsync(db, productIds, pkSecret, logger);

            // Group by SectionID and build DTOs
            return products
                .GroupBy(p => p.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(product => new ProductDto
                    {
                        Product = product.ToEntityWithEncryptedId(pkSecret, logger),
                        AdditionalIdentifiers = allAdditionalIds.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        BusinessOperationProductLinks = allBusinessOpLinks.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        Characteristics = allCharacteristics.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        ChildLotHierarchies = allChildLots.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        DosingSpecifications = allDosingSpecs.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        EquivalentEntities = allEquivalents.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        GenericMedicines = allGenericMeds.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        IngredientInstances = allIngredientInstances.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        Ingredients = allIngredients.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        MarketingCategories = allMarketingCats.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        MarketingStatuses = allMarketingStatuses.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        PackagingLevels = allPackageLevels.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        PackageIdentifiers = allPackageIdentifiers.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        ParentLotHierarchies = allParentLots.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        PartOfAssemblies = allPartsOfAssembly.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        Policies = allPolicies.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        ProductIdentifiers = allProductIds.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        ProductInstances = allProductInstances.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        ProductParts = allProductParts.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        ProductRouteOfAdministrations = allProductRoutes.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        ResponsiblePersonLinks = allRespPersonLinks.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        SpecializedKinds = allSpecializedKinds.GetValueOrDefault((int)product.ProductID!) ?? new(),
                        ProductWebLinks = allWebLinks.GetValueOrDefault((int)product.ProductID!) ?? new()
                    }).ToList()
                );

            #endregion
        }

        // Product child batch loaders - simplified implementations

        private static async Task<Dictionary<int, List<AdditionalIdentifierDto>>> batchLoadAdditionalIdentifiersAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.AdditionalIdentifier>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new AdditionalIdentifierDto { AdditionalIdentifier = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<BusinessOperationProductLinkDto>>> batchLoadBusinessOperationProductLinksAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.BusinessOperationProductLink>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new BusinessOperationProductLinkDto { BusinessOperationProductLink = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<CharacteristicDto>>> batchLoadProductCharacteristicsAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.Characteristic>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new CharacteristicDto { Characteristic = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<LotHierarchyDto>>> batchLoadLabelLotHierarchiesAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.LotHierarchy>().AsNoTracking()
                .Where(e => e.ChildInstanceID != null && productIds.Contains((int)e.ChildInstanceID)).ToListAsync();
            return entities.GroupBy(e => e.ChildInstanceID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new LotHierarchyDto { LotHierarchy = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<DosingSpecificationDto>>> batchLoadDosingSpecificationsAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.DosingSpecification>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new DosingSpecificationDto { DosingSpecification = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<EquivalentEntityDto>>> batchLoadEquivalentEntitiesAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.EquivalentEntity>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new EquivalentEntityDto { EquivalentEntity = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<GenericMedicineDto>>> batchLoadGenericMedicinesAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.GenericMedicine>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new GenericMedicineDto { GenericMedicine = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<IngredientInstanceDto>>> batchLoadProductIngredientInstancesAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.IngredientInstance>().AsNoTracking()
                .Where(e => e.FillLotInstanceID != null && productIds.Contains((int)e.FillLotInstanceID)).ToListAsync();
            return entities.GroupBy(e => e.FillLotInstanceID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new IngredientInstanceDto { IngredientInstance = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<MarketingCategoryDto>>> batchLoadMarketingCategoriesAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.MarketingCategory>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new MarketingCategoryDto { MarketingCategory = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<MarketingStatusDto>>> batchLoadProductMarketingStatusesAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.MarketingStatus>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new MarketingStatusDto { MarketingStatus = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<LotHierarchyDto>>> batchLoadFillLotHierarchiesAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.LotHierarchy>().AsNoTracking()
                .Where(e => e.ParentInstanceID != null && productIds.Contains((int)e.ParentInstanceID)).ToListAsync();
            return entities.GroupBy(e => e.ParentInstanceID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new LotHierarchyDto { LotHierarchy = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<PartOfAssemblyDto>>> batchLoadPartOfAssembliesAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.PartOfAssembly>().AsNoTracking()
                .Where(e => e.PrimaryProductID != null && productIds.Contains((int)e.PrimaryProductID)).ToListAsync();
            return entities.GroupBy(e => e.PrimaryProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new PartOfAssemblyDto { PartOfAssembly = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<PolicyDto>>> batchLoadPoliciesAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.Policy>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new PolicyDto { Policy = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<ProductIdentifierDto>>> batchLoadProductIdentifiersAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.ProductIdentifier>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new ProductIdentifierDto { ProductIdentifier = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads ProductPart entities for Kit products, including full PartProduct DTOs.
        /// Each PartProduct includes its ingredients, characteristics, routes, marketing status, etc.
        /// </summary>
        /// <remarks>
        /// This method uses a view-based approach to identify Kit parts:
        /// 1. Identify Kit products by FormCode = C47916
        /// 2. Find other products in the same section without marketing data
        /// 3. Build synthetic ProductPart records from the inferred relationships
        ///
        /// The view-based approach is used because the ProductPart table may have data integrity
        /// issues (e.g., self-references where KitProductID == PartProductID).
        ///
        /// Backward compatibility: If the ProductPart table has valid data (non-self-referencing),
        /// the quantity information from the table is used. Otherwise, parts are inferred from
        /// section membership and marketing data absence.
        /// </remarks>
        /// <seealso cref="identifyKitPartProductsAsync"/>
        /// <seealso cref="batchLoadPartProductsAsync"/>
        private static async Task<Dictionary<int, List<ProductPartDto>>> batchLoadProductPartsAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            #region implementation

            if (productIds == null || !productIds.Any()) return new();

            // Load all products for the sections where our Kit products reside
            // We need this to identify part products by section membership
            // Note: Cannot use isKitProduct() in LINQ-to-SQL, must inline the FormCode check
            var kitProducts = await db.Set<Label.Product>().AsNoTracking()
                .Where(p => p.ProductID != null && productIds.Contains((int)p.ProductID)
                    && p.FormCode != null && p.FormCode == Constant.KIT_FORM_CODE)
                .ToListAsync();

            if (!kitProducts.Any())
                return new();

            // Get section IDs for all Kit products
            var kitSectionIds = kitProducts
                .Where(p => p.SectionID.HasValue)
                .Select(p => p.SectionID!.Value)
                .Distinct()
                .ToList();

            // Load all products in Kit sections to identify parts
            var allProductsInKitSections = await db.Set<Label.Product>().AsNoTracking()
                .Where(p => p.SectionID != null && kitSectionIds.Contains((int)p.SectionID))
                .ToListAsync();

            // Build Kit ProductID -> SectionID mapping
            var kitProductIdToSectionId = kitProducts
                .Where(p => p.ProductID.HasValue && p.SectionID.HasValue)
                .ToDictionary(p => p.ProductID!.Value, p => p.SectionID!.Value);

            // Use view-based logic to identify Kit part products
            var kitPartsMapping = await identifyKitPartProductsAsync(
                db,
                productIds.ToList(),
                kitProductIdToSectionId,
                allProductsInKitSections);

            if (!kitPartsMapping.Any())
                return new();

            // Collect all part product IDs for batch loading their full data
            var allPartProductIds = kitPartsMapping.Values
                .SelectMany(parts => parts)
                .Where(p => p.ProductID.HasValue)
                .Select(p => p.ProductID!.Value)
                .Distinct()
                .ToList();

            // Load full product data for each part product
            var partProductDtos = await batchLoadPartProductsAsync(db, allPartProductIds, pkSecret, logger);

            // Try to get quantity information from ProductPart table (if available and valid)
            // This handles cases where the table has correct data
            var existingProductParts = await db.Set<Label.ProductPart>().AsNoTracking()
                .Where(pp => pp.KitProductID != null && productIds.Contains((int)pp.KitProductID)
                    && pp.PartProductID != null && pp.KitProductID != pp.PartProductID) // Exclude self-references
                .ToListAsync();

            // Build lookup for quantity data from ProductPart table
            var quantityLookup = existingProductParts
                .Where(pp => pp.KitProductID.HasValue && pp.PartProductID.HasValue)
                .ToDictionary(
                    pp => (KitId: pp.KitProductID!.Value, PartId: pp.PartProductID!.Value),
                    pp => pp);

            // Build the result dictionary
            var result = new Dictionary<int, List<ProductPartDto>>();

            foreach (var kvp in kitPartsMapping)
            {
                var kitProductId = kvp.Key;
                var partProducts = kvp.Value;

                var productPartDtos = new List<ProductPartDto>();

                foreach (var partProduct in partProducts.OrderBy(p => p.ProductName)) // Order alphabetically by name
                {
                    if (!partProduct.ProductID.HasValue)
                        continue;

                    var partProductId = partProduct.ProductID.Value;

                    // Try to get existing ProductPart record for quantity info
                    Label.ProductPart? existingPart = null;
                    quantityLookup.TryGetValue((kitProductId, partProductId), out existingPart);

                    // Create synthetic ProductPart entity if no valid one exists
                    var productPartEntity = existingPart ?? new Label.ProductPart
                    {
                        KitProductID = kitProductId,
                        PartProductID = partProductId,
                        // Quantity will be null if not in table - templates should handle this
                    };

                    productPartDtos.Add(new ProductPartDto
                    {
                        ProductPart = productPartEntity.ToEntityWithEncryptedId(pkSecret, logger),
                        PartProduct = partProductDtos.GetValueOrDefault(partProductId)
                    });
                }

                if (productPartDtos.Any())
                {
                    result[kitProductId] = productPartDtos;
                }
            }

            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads full Product DTOs for part products (used by Kit products).
        /// Loads core product children needed for SPL partProduct rendering.
        /// </summary>
        private static async Task<Dictionary<int, ProductDto>> batchLoadPartProductsAsync(
            ApplicationDbContext db, IReadOnlyList<int> partProductIds, string pkSecret, ILogger logger)
        {
            if (partProductIds == null || !partProductIds.Any()) return new();

            // Load part product entities
            var products = await db.Set<Label.Product>().AsNoTracking()
                .Where(p => p.ProductID != null && partProductIds.Contains((int)p.ProductID))
                .ToListAsync();

            if (!products.Any())
                return new();

            // Batch load essential children for part products
            var allIngredients = await batchLoadIngredientsAsync(db, partProductIds, pkSecret, logger);
            var allCharacteristics = await batchLoadProductCharacteristicsAsync(db, partProductIds, pkSecret, logger);
            var allRoutes = await batchLoadProductRoutesAsync(db, partProductIds, pkSecret, logger);
            var allGenericMedicines = await batchLoadGenericMedicinesAsync(db, partProductIds, pkSecret, logger);
            var allMarketingCategories = await batchLoadMarketingCategoriesAsync(db, partProductIds, pkSecret, logger);
            var allMarketingStatuses = await batchLoadProductMarketingStatusesAsync(db, partProductIds, pkSecret, logger);
            var allProductIdentifiers = await batchLoadProductIdentifiersAsync(db, partProductIds, pkSecret, logger);

            return products.ToDictionary(
                p => p.ProductID!.Value,
                p => new ProductDto
                {
                    Product = p.ToEntityWithEncryptedId(pkSecret, logger),
                    Ingredients = allIngredients.GetValueOrDefault((int)p.ProductID!) ?? new(),
                    Characteristics = allCharacteristics.GetValueOrDefault((int)p.ProductID!) ?? new(),
                    ProductRouteOfAdministrations = allRoutes.GetValueOrDefault((int)p.ProductID!) ?? new(),
                    GenericMedicines = allGenericMedicines.GetValueOrDefault((int)p.ProductID!) ?? new(),
                    MarketingCategories = allMarketingCategories.GetValueOrDefault((int)p.ProductID!) ?? new(),
                    MarketingStatuses = allMarketingStatuses.GetValueOrDefault((int)p.ProductID!) ?? new(),
                    ProductIdentifiers = allProductIdentifiers.GetValueOrDefault((int)p.ProductID!) ?? new()
                });
        }

        private static async Task<Dictionary<int, List<ProductRouteOfAdministrationDto>>> batchLoadProductRoutesAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.ProductRouteOfAdministration>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new ProductRouteOfAdministrationDto { ProductRouteOfAdministration = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<ResponsiblePersonLinkDto>>> batchLoadResponsiblePersonLinksAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.ResponsiblePersonLink>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new ResponsiblePersonLinkDto { ResponsiblePersonLink = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<SpecializedKindDto>>> batchLoadSpecializedKindsAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.SpecializedKind>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new SpecializedKindDto { SpecializedKind = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<ProductWebLinkDto>>> batchLoadProductWebLinksAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.ProductWebLink>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new ProductWebLinkDto { ProductWebLink = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<ProductInstanceDto>>> batchLoadProductInstancesAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.ProductInstance>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();
            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new ProductInstanceDto { ProductInstance = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<PackagingLevelDto>>> batchLoadPackagingLevelsAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, int? sectionId, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();
            var entities = await db.Set<Label.PackagingLevel>().AsNoTracking()
                .Where(e => e.ProductID != null && productIds.Contains((int)e.ProductID)).ToListAsync();

            if (!entities.Any())
                return new();

            // Get all packaging level IDs for batch loading children
            var packagingLevelIds = entities.Where(e => e.PackagingLevelID != null)
                .Select(e => (int)e.PackagingLevelID!).ToList();

            // Batch load all child collections
            var allPackagingHierarchies = await batchLoadPackagingHierarchiesAsync(db, packagingLevelIds, pkSecret, logger);
            var allProductEvents = await batchLoadProductEventsAsync(db, packagingLevelIds, pkSecret, logger);
            var allMarketingStatuses = await batchLoadPackageMarketingStatusesAsync(db, packagingLevelIds, pkSecret, logger);
            var allPackageIdentifiers = await batchLoadPackageIdentifiersByLevelAsync(db, packagingLevelIds, pkSecret, logger);
            var allCharacteristics = await batchLoadPackageLevelCharacteristicsAsync(db, packagingLevelIds, pkSecret, logger);

            return entities.GroupBy(e => e.ProductID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new PackagingLevelDto
                {
                    PackagingLevel = e.ToEntityWithEncryptedId(pkSecret, logger),
                    PackagingHierarchy = e.PackagingLevelID != null ? allPackagingHierarchies.GetValueOrDefault((int)e.PackagingLevelID!) ?? new() : new(),
                    ProductEvents = e.PackagingLevelID != null ? allProductEvents.GetValueOrDefault((int)e.PackagingLevelID!) ?? new() : new(),
                    MarketingStatuses = e.PackagingLevelID != null ? allMarketingStatuses.GetValueOrDefault((int)e.PackagingLevelID!) ?? new() : new(),
                    PackageIdentifiers = e.PackagingLevelID != null ? allPackageIdentifiers.GetValueOrDefault((int)e.PackagingLevelID!) ?? new() : new(),
                    Characteristics = e.PackagingLevelID != null ? allCharacteristics.GetValueOrDefault((int)e.PackagingLevelID!) ?? new() : new()
                }).ToList());
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads PackagingHierarchy entities with full ChildPackagingLevel population.
        /// Recursively loads inner packaging levels with all their children to support nested asContent rendering.
        /// </summary>
        private static async Task<Dictionary<int, List<PackagingHierarchyDto>>> batchLoadPackagingHierarchiesAsync(
            ApplicationDbContext db, IReadOnlyList<int> packagingLevelIds, string pkSecret, ILogger logger)
        {
            if (packagingLevelIds == null || !packagingLevelIds.Any()) return new();

            // Load all hierarchy entities for the given outer packaging level IDs
            var hierarchyEntities = await db.Set<Label.PackagingHierarchy>().AsNoTracking()
                .Where(e => e.OuterPackagingLevelID != null && packagingLevelIds.Contains((int)e.OuterPackagingLevelID))
                .ToListAsync();

            if (!hierarchyEntities.Any())
                return new();

            // Collect all InnerPackagingLevelIDs to load the child packaging levels
            var innerPackagingLevelIds = hierarchyEntities
                .Where(h => h.InnerPackagingLevelID != null)
                .Select(h => (int)h.InnerPackagingLevelID!)
                .Distinct()
                .ToList();

            // Load all inner packaging levels with their children recursively
            var innerPackagingLevels = await batchLoadInnerPackagingLevelsRecursiveAsync(db, innerPackagingLevelIds, pkSecret, logger);

            // Build the hierarchy DTOs with populated ChildPackagingLevel
            return hierarchyEntities.GroupBy(e => e.OuterPackagingLevelID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new PackagingHierarchyDto
                {
                    PackagingHierarchy = e.ToEntityWithEncryptedId(pkSecret, logger),
                    ChildPackagingLevel = e.InnerPackagingLevelID != null
                        ? innerPackagingLevels.GetValueOrDefault((int)e.InnerPackagingLevelID!)
                        : null
                }).ToList());
        }

        /**************************************************************/
        /// <summary>
        /// Recursively loads inner PackagingLevel entities with all their children.
        /// Supports multi-level nested packaging structures (e.g., BLISTER PACK inside CARTON).
        /// </summary>
        private static async Task<Dictionary<int, PackagingLevelDto>> batchLoadInnerPackagingLevelsRecursiveAsync(
            ApplicationDbContext db, IReadOnlyList<int> packagingLevelIds, string pkSecret, ILogger logger)
        {
            if (packagingLevelIds == null || !packagingLevelIds.Any()) return new();

            // Load all packaging level entities
            var entities = await db.Set<Label.PackagingLevel>().AsNoTracking()
                .Where(e => e.PackagingLevelID != null && packagingLevelIds.Contains((int)e.PackagingLevelID))
                .ToListAsync();

            if (!entities.Any())
                return new();

            // Batch load all child collections for these packaging levels
            var allProductEvents = await batchLoadProductEventsAsync(db, packagingLevelIds, pkSecret, logger);
            var allMarketingStatuses = await batchLoadPackageMarketingStatusesAsync(db, packagingLevelIds, pkSecret, logger);
            var allPackageIdentifiers = await batchLoadPackageIdentifiersByLevelAsync(db, packagingLevelIds, pkSecret, logger);
            var allCharacteristics = await batchLoadPackageLevelCharacteristicsAsync(db, packagingLevelIds, pkSecret, logger);

            // Load hierarchies for these packaging levels (for further nesting)
            var hierarchyEntities = await db.Set<Label.PackagingHierarchy>().AsNoTracking()
                .Where(e => e.OuterPackagingLevelID != null && packagingLevelIds.Contains((int)e.OuterPackagingLevelID))
                .ToListAsync();

            // Collect inner packaging level IDs for recursive loading
            var nextLevelIds = hierarchyEntities
                .Where(h => h.InnerPackagingLevelID != null)
                .Select(h => (int)h.InnerPackagingLevelID!)
                .Distinct()
                .ToList();

            // Recursively load the next level of inner packaging
            var nestedInnerPackagingLevels = nextLevelIds.Any()
                ? await batchLoadInnerPackagingLevelsRecursiveAsync(db, nextLevelIds, pkSecret, logger)
                : new Dictionary<int, PackagingLevelDto>();

            // Build hierarchy DTOs with nested ChildPackagingLevel
            var allPackagingHierarchies = hierarchyEntities.GroupBy(e => e.OuterPackagingLevelID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new PackagingHierarchyDto
                {
                    PackagingHierarchy = e.ToEntityWithEncryptedId(pkSecret, logger),
                    ChildPackagingLevel = e.InnerPackagingLevelID != null
                        ? nestedInnerPackagingLevels.GetValueOrDefault((int)e.InnerPackagingLevelID!)
                        : null
                }).ToList());

            // Build and return the packaging level DTOs
            return entities.ToDictionary(
                e => e.PackagingLevelID!.Value,
                e => new PackagingLevelDto
                {
                    PackagingLevel = e.ToEntityWithEncryptedId(pkSecret, logger),
                    PackagingHierarchy = e.PackagingLevelID != null ? allPackagingHierarchies.GetValueOrDefault((int)e.PackagingLevelID!) ?? new() : new(),
                    ProductEvents = e.PackagingLevelID != null ? allProductEvents.GetValueOrDefault((int)e.PackagingLevelID!) ?? new() : new(),
                    MarketingStatuses = e.PackagingLevelID != null ? allMarketingStatuses.GetValueOrDefault((int)e.PackagingLevelID!) ?? new() : new(),
                    PackageIdentifiers = e.PackagingLevelID != null ? allPackageIdentifiers.GetValueOrDefault((int)e.PackagingLevelID!) ?? new() : new(),
                    Characteristics = e.PackagingLevelID != null ? allCharacteristics.GetValueOrDefault((int)e.PackagingLevelID!) ?? new() : new()
                });
        }

        private static async Task<Dictionary<int, List<ProductEventDto>>> batchLoadProductEventsAsync(
            ApplicationDbContext db, IReadOnlyList<int> packagingLevelIds, string pkSecret, ILogger logger)
        {
            if (packagingLevelIds == null || !packagingLevelIds.Any()) return new();
            var entities = await db.Set<Label.ProductEvent>().AsNoTracking()
                .Where(e => e.PackagingLevelID != null && packagingLevelIds.Contains((int)e.PackagingLevelID)).ToListAsync();
            return entities.GroupBy(e => e.PackagingLevelID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new ProductEventDto { ProductEvent = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<MarketingStatusDto>>> batchLoadPackageMarketingStatusesAsync(
            ApplicationDbContext db, IReadOnlyList<int> packagingLevelIds, string pkSecret, ILogger logger)
        {
            if (packagingLevelIds == null || !packagingLevelIds.Any()) return new();
            var entities = await db.Set<Label.MarketingStatus>().AsNoTracking()
                .Where(e => e.PackagingLevelID != null && packagingLevelIds.Contains((int)e.PackagingLevelID)).ToListAsync();
            return entities.GroupBy(e => e.PackagingLevelID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new MarketingStatusDto { MarketingStatus = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<PackageIdentifierDto>>> batchLoadPackageIdentifiersByLevelAsync(
            ApplicationDbContext db, IReadOnlyList<int> packagingLevelIds, string pkSecret, ILogger logger)
        {
            if (packagingLevelIds == null || !packagingLevelIds.Any()) return new();
            var entities = await db.Set<Label.PackageIdentifier>().AsNoTracking()
                .Where(e => e.PackagingLevelID != null && packagingLevelIds.Contains((int)e.PackagingLevelID)).ToListAsync();
            return entities.GroupBy(e => e.PackagingLevelID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new PackageIdentifierDto { PackageIdentifier = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<CharacteristicDto>>> batchLoadPackageLevelCharacteristicsAsync(
            ApplicationDbContext db, IReadOnlyList<int> packagingLevelIds, string pkSecret, ILogger logger)
        {
            if (packagingLevelIds == null || !packagingLevelIds.Any()) return new();
            var entities = await db.Set<Label.Characteristic>().AsNoTracking()
                .Where(e => e.PackagingLevelID != null && packagingLevelIds.Contains((int)e.PackagingLevelID)).ToListAsync();
            return entities.GroupBy(e => e.PackagingLevelID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new CharacteristicDto { Characteristic = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads PackageIdentifier entities for multiple products by joining through PackagingLevel.
        /// Includes ComplianceActions for each PackageIdentifier.
        /// </summary>
        private static async Task<Dictionary<int, List<PackageIdentifierDto>>> batchLoadDirectPackageIdentifiersAsync(
            ApplicationDbContext db, IReadOnlyList<int> productIds, string pkSecret, ILogger logger)
        {
            if (productIds == null || !productIds.Any()) return new();

            // Join PackageIdentifier -> PackagingLevel to get ProductID association
            var packageIdentifiersWithProducts = await (
                from pi in db.Set<Label.PackageIdentifier>().AsNoTracking()
                join pl in db.Set<Label.PackagingLevel>().AsNoTracking() on pi.PackagingLevelID equals pl.PackagingLevelID
                where pl.ProductID != null && productIds.Contains((int)pl.ProductID)
                select new { PackageIdentifier = pi, ProductID = pl.ProductID!.Value }
            ).ToListAsync();

            if (!packageIdentifiersWithProducts.Any())
                return new();

            // Get all package identifier IDs for batch loading compliance actions
            var packageIdentifierIds = packageIdentifiersWithProducts
                .Where(x => x.PackageIdentifier.PackageIdentifierID != null)
                .Select(x => (int)x.PackageIdentifier.PackageIdentifierID!)
                .Distinct()
                .ToList();

            // Batch load compliance actions for all package identifiers
            var allComplianceActions = await batchLoadComplianceActionsForPackagesAsync(db, packageIdentifierIds, pkSecret, logger);

            // Group by ProductID and build DTOs
            return packageIdentifiersWithProducts
                .GroupBy(x => x.ProductID)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => new PackageIdentifierDto
                    {
                        PackageIdentifier = x.PackageIdentifier.ToEntityWithEncryptedId(pkSecret, logger),
                        ComplianceActions = x.PackageIdentifier.PackageIdentifierID != null
                            ? allComplianceActions.GetValueOrDefault((int)x.PackageIdentifier.PackageIdentifierID!) ?? new()
                            : new()
                    }).ToList()
                );
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads ComplianceAction entities for multiple PackageIdentifier IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<ComplianceActionDto>>> batchLoadComplianceActionsForPackagesAsync(
            ApplicationDbContext db, IReadOnlyList<int> packageIdentifierIds, string pkSecret, ILogger logger)
        {
            if (packageIdentifierIds == null || !packageIdentifierIds.Any()) return new();

            var entities = await db.Set<Label.ComplianceAction>().AsNoTracking()
                .Where(e => e.PackageIdentifierID != null && packageIdentifierIds.Contains((int)e.PackageIdentifierID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.PackageIdentifierID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new ComplianceActionDto
                    {
                        ComplianceAction = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );
        }

        #endregion

        #region Ingredient Level Batch Loaders

        /**************************************************************/
        /// <summary>
        /// Batch loads Ingredient entities with all children for multiple product IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<IngredientDto>>> batchLoadIngredientsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> productIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (productIds == null || !productIds.Any())
                return new Dictionary<int, List<IngredientDto>>();

            var ingredients = await db.Set<Label.Ingredient>()
                .AsNoTracking()
                .Where(i => i.ProductID != null && productIds.Contains((int)i.ProductID))
                .ToListAsync();

            if (!ingredients.Any())
                return new Dictionary<int, List<IngredientDto>>();

            // Collect IDs for batch loading
            var ingredientIds = ingredients.Where(i => i.IngredientID != null).Select(i => (int)i.IngredientID!).ToList();
            var substanceIds = ingredients.Where(i => i.IngredientSubstanceID != null).Select(i => (int)i.IngredientSubstanceID!).Distinct().ToList();
            var specifiedSubstanceIds = ingredients.Where(i => i.SpecifiedSubstanceID != null).Select(i => (int)i.SpecifiedSubstanceID!).Distinct().ToList();

            // Batch load children
            var allSubstances = await batchLoadIngredientSubstancesAsync(db, substanceIds, pkSecret, logger);
            var allRefSubstances = await batchLoadReferenceSubstancesAsync(db, substanceIds, pkSecret, logger);
            var allInstances = await batchLoadIngredientInstancesBySubstanceAsync(db, substanceIds, pkSecret, logger);
            var allSourceProducts = await batchLoadIngredientSourceProductsAsync(db, ingredientIds, pkSecret, logger);
            var allSpecifiedSubstances = await batchLoadSpecifiedSubstancesAsync(db, specifiedSubstanceIds, pkSecret, logger);

            return ingredients
                .GroupBy(i => i.ProductID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(ingredient => new IngredientDto
                    {
                        Ingredient = ingredient.ToEntityWithEncryptedId(pkSecret, logger),
                        IngredientSubstance = ingredient.IngredientSubstanceID != null
                            ? allSubstances.GetValueOrDefault((int)ingredient.IngredientSubstanceID)
                            : null,
                        ReferenceSubstances = ingredient.IngredientSubstanceID != null
                            ? allRefSubstances.GetValueOrDefault((int)ingredient.IngredientSubstanceID) ?? new()
                            : new(),
                        IngredientInstances = ingredient.IngredientSubstanceID != null
                            ? allInstances.GetValueOrDefault((int)ingredient.IngredientSubstanceID) ?? new()
                            : new(),
                        IngredientSourceProducts = ingredient.IngredientID != null
                            ? allSourceProducts.GetValueOrDefault((int)ingredient.IngredientID) ?? new()
                            : new(),
                        SpecifiedSubstances = ingredient.SpecifiedSubstanceID != null
                            ? allSpecifiedSubstances.GetValueOrDefault((int)ingredient.SpecifiedSubstanceID) ?? new()
                            : new()
                    }).ToList()
                );

            #endregion
        }

        private static async Task<Dictionary<int, IngredientSubstanceDto>> batchLoadIngredientSubstancesAsync(
            ApplicationDbContext db, IReadOnlyList<int> substanceIds, string pkSecret, ILogger logger)
        {
            if (substanceIds == null || !substanceIds.Any()) return new();
            var entities = await db.Set<Label.IngredientSubstance>().AsNoTracking()
                .Where(e => e.IngredientSubstanceID != null && substanceIds.Contains((int)e.IngredientSubstanceID)).ToListAsync();

            if (!entities.Any())
                return new();

            // Batch load ActiveMoieties for all ingredient substances
            var allActiveMoieties = await batchLoadActiveMoietiesAsync(db, substanceIds, pkSecret, logger);

            return entities.ToDictionary(e => e.IngredientSubstanceID!.Value,
                e => new IngredientSubstanceDto
                {
                    IngredientSubstance = e.ToEntityWithEncryptedId(pkSecret, logger),
                    ActiveMoieties = allActiveMoieties.GetValueOrDefault((int)e.IngredientSubstanceID!) ?? new()
                });
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads ActiveMoiety entities for multiple IngredientSubstance IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<ActiveMoietyDto>>> batchLoadActiveMoietiesAsync(
            ApplicationDbContext db, IReadOnlyList<int> substanceIds, string pkSecret, ILogger logger)
        {
            if (substanceIds == null || !substanceIds.Any()) return new();
            var entities = await db.Set<Label.ActiveMoiety>().AsNoTracking()
                .Where(e => e.IngredientSubstanceID != null && substanceIds.Contains((int)e.IngredientSubstanceID))
                .ToListAsync();
            return entities.GroupBy(e => e.IngredientSubstanceID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new ActiveMoietyDto { ActiveMoiety = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<ReferenceSubstanceDto>>> batchLoadReferenceSubstancesAsync(
            ApplicationDbContext db, IReadOnlyList<int> substanceIds, string pkSecret, ILogger logger)
        {
            if (substanceIds == null || !substanceIds.Any()) return new();
            var entities = await db.Set<Label.ReferenceSubstance>().AsNoTracking()
                .Where(e => e.IngredientSubstanceID != null && substanceIds.Contains((int)e.IngredientSubstanceID)).ToListAsync();
            return entities.GroupBy(e => e.IngredientSubstanceID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new ReferenceSubstanceDto { ReferenceSubstance = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<IngredientInstanceDto>>> batchLoadIngredientInstancesBySubstanceAsync(
            ApplicationDbContext db, IReadOnlyList<int> substanceIds, string pkSecret, ILogger logger)
        {
            if (substanceIds == null || !substanceIds.Any()) return new();
            var entities = await db.Set<Label.IngredientInstance>().AsNoTracking()
                .Where(e => e.IngredientSubstanceID != null && substanceIds.Contains((int)e.IngredientSubstanceID)).ToListAsync();
            return entities.GroupBy(e => e.IngredientSubstanceID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new IngredientInstanceDto { IngredientInstance = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<IngredientSourceProductDto>>> batchLoadIngredientSourceProductsAsync(
            ApplicationDbContext db, IReadOnlyList<int> ingredientIds, string pkSecret, ILogger logger)
        {
            if (ingredientIds == null || !ingredientIds.Any()) return new();
            var entities = await db.Set<Label.IngredientSourceProduct>().AsNoTracking()
                .Where(e => e.IngredientID != null && ingredientIds.Contains((int)e.IngredientID)).ToListAsync();
            return entities.GroupBy(e => e.IngredientID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new IngredientSourceProductDto { IngredientSourceProduct = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<SpecifiedSubstanceDto>>> batchLoadSpecifiedSubstancesAsync(
            ApplicationDbContext db, IReadOnlyList<int> specifiedSubstanceIds, string pkSecret, ILogger logger)
        {
            if (specifiedSubstanceIds == null || !specifiedSubstanceIds.Any()) return new();
            var entities = await db.Set<Label.SpecifiedSubstance>().AsNoTracking()
                .Where(e => e.SpecifiedSubstanceID != null && specifiedSubstanceIds.Contains((int)e.SpecifiedSubstanceID)).ToListAsync();
            return entities.GroupBy(e => e.SpecifiedSubstanceID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new SpecifiedSubstanceDto { SpecifiedSubstance = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads IdentifiedSubstance entities with children for multiple section IDs.
        /// Includes SubstanceSpecifications, ContributingFactors, PharmacologicClasses, and Moiety.
        /// </summary>
        private static async Task<Dictionary<int, List<IdentifiedSubstanceDto>>> batchLoadIdentifiedSubstancesAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<IdentifiedSubstanceDto>>();

            var entities = await db.Set<Label.IdentifiedSubstance>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            if (!entities.Any())
                return new Dictionary<int, List<IdentifiedSubstanceDto>>();

            var substanceIds = entities.Where(e => e.IdentifiedSubstanceID != null)
                .Select(e => (int)e.IdentifiedSubstanceID!).ToList();

            // Batch load all children
            var allSpecs = await batchLoadSubstanceSpecificationsAsync(db, substanceIds, pkSecret, logger);
            var allContributingFactors = await batchLoadContributingFactorsAsync(db, substanceIds, pkSecret, logger);
            var allPharmacologicClasses = await batchLoadPharmacologicClassesAsync(db, substanceIds, pkSecret, logger);
            var allMoieties = await batchLoadMoietiesAsync(db, substanceIds, pkSecret, logger);

            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new IdentifiedSubstanceDto
                    {
                        IdentifiedSubstance = e.ToEntityWithEncryptedId(pkSecret, logger),
                        SubstanceSpecifications = allSpecs.GetValueOrDefault((int)e.IdentifiedSubstanceID!) ?? new(),
                        ContributingFactors = allContributingFactors.GetValueOrDefault((int)e.IdentifiedSubstanceID!) ?? new(),
                        PharmacologicClasses = allPharmacologicClasses.GetValueOrDefault((int)e.IdentifiedSubstanceID!) ?? new(),
                        Moiety = allMoieties.GetValueOrDefault((int)e.IdentifiedSubstanceID!) ?? new()
                    }).ToList()
                );

            #endregion
        }

        private static async Task<Dictionary<int, List<SubstanceSpecificationDto>>> batchLoadSubstanceSpecificationsAsync(
            ApplicationDbContext db, IReadOnlyList<int> substanceIds, string pkSecret, ILogger logger)
        {
            if (substanceIds == null || !substanceIds.Any()) return new();
            var entities = await db.Set<Label.SubstanceSpecification>().AsNoTracking()
                .Where(e => e.IdentifiedSubstanceID != null && substanceIds.Contains((int)e.IdentifiedSubstanceID)).ToListAsync();
            return entities.GroupBy(e => e.IdentifiedSubstanceID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new SubstanceSpecificationDto { SubstanceSpecification = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<ContributingFactorDto>>> batchLoadContributingFactorsAsync(
            ApplicationDbContext db, IReadOnlyList<int> substanceIds, string pkSecret, ILogger logger)
        {
            if (substanceIds == null || !substanceIds.Any()) return new();
            // ContributingFactor uses FactorSubstanceID to reference IdentifiedSubstance
            var entities = await db.Set<Label.ContributingFactor>().AsNoTracking()
                .Where(e => e.FactorSubstanceID != null && substanceIds.Contains((int)e.FactorSubstanceID)).ToListAsync();
            return entities.GroupBy(e => e.FactorSubstanceID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new ContributingFactorDto { ContributingFactor = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<PharmacologicClassDto>>> batchLoadPharmacologicClassesAsync(
            ApplicationDbContext db, IReadOnlyList<int> substanceIds, string pkSecret, ILogger logger)
        {
            if (substanceIds == null || !substanceIds.Any()) return new();
            var entities = await db.Set<Label.PharmacologicClass>().AsNoTracking()
                .Where(e => e.IdentifiedSubstanceID != null && substanceIds.Contains((int)e.IdentifiedSubstanceID)).ToListAsync();
            return entities.GroupBy(e => e.IdentifiedSubstanceID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new PharmacologicClassDto { PharmacologicClass = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<MoietyDto>>> batchLoadMoietiesAsync(
            ApplicationDbContext db, IReadOnlyList<int> substanceIds, string pkSecret, ILogger logger)
        {
            if (substanceIds == null || !substanceIds.Any()) return new();
            var entities = await db.Set<Label.Moiety>().AsNoTracking()
                .Where(e => e.IdentifiedSubstanceID != null && substanceIds.Contains((int)e.IdentifiedSubstanceID))
                .OrderBy(e => e.SequenceNumber)
                .ToListAsync();

            if (!entities.Any())
                return new();

            // Batch load characteristics for all moieties
            var moietyIds = entities.Where(e => e.MoietyID != null).Select(e => (int)e.MoietyID!).ToList();
            var allMoietyCharacteristics = await batchLoadMoietyCharacteristicsAsync(db, moietyIds, pkSecret, logger);

            return entities.GroupBy(e => e.IdentifiedSubstanceID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new MoietyDto
                {
                    Moiety = e.ToEntityWithEncryptedId(pkSecret, logger),
                    Characteristics = e.MoietyID != null
                        ? allMoietyCharacteristics.GetValueOrDefault((int)e.MoietyID!) ?? new()
                        : new()
                }).ToList());
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads Characteristic entities for multiple Moiety IDs.
        /// Used for substance structure data including MOLFILE, InChI, and InChI-Key.
        /// </summary>
        private static async Task<Dictionary<int, List<CharacteristicDto>>> batchLoadMoietyCharacteristicsAsync(
            ApplicationDbContext db, IReadOnlyList<int> moietyIds, string pkSecret, ILogger logger)
        {
            if (moietyIds == null || !moietyIds.Any()) return new();

            var entities = await db.Set<Label.Characteristic>().AsNoTracking()
                .Where(e => e.MoietyID != null && moietyIds.Contains((int)e.MoietyID))
                .ToListAsync();

            return entities.GroupBy(e => e.MoietyID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new CharacteristicDto
                {
                    Characteristic = e.ToEntityWithEncryptedId(pkSecret, logger)
                }).ToList());
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads InteractionIssue entities with consequences for multiple section IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<InteractionIssueDto>>> batchLoadInteractionIssuesAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> sectionIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (sectionIds == null || !sectionIds.Any())
                return new Dictionary<int, List<InteractionIssueDto>>();

            var entities = await db.Set<Label.InteractionIssue>()
                .AsNoTracking()
                .Where(e => e.SectionID != null && sectionIds.Contains((int)e.SectionID))
                .ToListAsync();

            if (!entities.Any())
                return new Dictionary<int, List<InteractionIssueDto>>();

            var issueIds = entities.Where(e => e.InteractionIssueID != null)
                .Select(e => (int)e.InteractionIssueID!).ToList();

            var allConsequences = await batchLoadInteractionConsequencesAsync(db, issueIds, pkSecret, logger);

            return entities
                .GroupBy(e => e.SectionID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new InteractionIssueDto
                    {
                        InteractionIssue = e.ToEntityWithEncryptedId(pkSecret, logger),
                        InteractionConsequences = allConsequences.GetValueOrDefault((int)e.InteractionIssueID!) ?? new()
                    }).ToList()
                );

            #endregion
        }

        private static async Task<Dictionary<int, List<InteractionConsequenceDto>>> batchLoadInteractionConsequencesAsync(
            ApplicationDbContext db, IReadOnlyList<int> issueIds, string pkSecret, ILogger logger)
        {
            if (issueIds == null || !issueIds.Any()) return new();
            var entities = await db.Set<Label.InteractionConsequence>().AsNoTracking()
                .Where(e => e.InteractionIssueID != null && issueIds.Contains((int)e.InteractionIssueID)).ToListAsync();
            return entities.GroupBy(e => e.InteractionIssueID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new InteractionConsequenceDto { InteractionConsequence = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        #endregion

        #region Organization Batch Loaders

        /**************************************************************/
        /// <summary>
        /// Batch loads Organization entities for multiple organization IDs.
        /// Includes OrganizationIdentifier, Telecom, NamedEntity, and Holder records.
        /// </summary>
        private static async Task<Dictionary<int, OrganizationDto>> batchLoadOrganizationsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> organizationIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (organizationIds == null || !organizationIds.Any())
                return new Dictionary<int, OrganizationDto>();

            var entities = await db.Set<Label.Organization>()
                .AsNoTracking()
                .Where(o => o.OrganizationID != null && organizationIds.Contains((int)o.OrganizationID))
                .ToListAsync();

            // Batch load all organization child collections
            var allIdentifiers = await batchLoadOrganizationIdentifiersAsync(db, organizationIds, pkSecret, logger);
            var allTelecoms = await batchLoadOrganizationTelecomsAsync(db, organizationIds, pkSecret, logger);
            var allNamedEntities = await batchLoadNamedEntitiesAsync(db, organizationIds, pkSecret, logger);
            var allHolders = await batchLoadHoldersAsync(db, organizationIds, pkSecret, logger);

            return entities.ToDictionary(
                e => e.OrganizationID!.Value,
                e => new OrganizationDto
                {
                    Organization = e.ToEntityWithEncryptedId(pkSecret, logger),
                    Identifiers = allIdentifiers.GetValueOrDefault((int)e.OrganizationID!) ?? new List<OrganizationIdentifierDto>(),
                    Telecoms = allTelecoms.GetValueOrDefault((int)e.OrganizationID!) ?? new List<OrganizationTelecomDto>(),
                    NamedEntities = allNamedEntities.GetValueOrDefault((int)e.OrganizationID!) ?? new List<NamedEntityDto>(),
                    Holders = allHolders.GetValueOrDefault((int)e.OrganizationID!) ?? new List<HolderDto>()
                }
            );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads OrganizationIdentifier entities for multiple organization IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<OrganizationIdentifierDto>>> batchLoadOrganizationIdentifiersAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> organizationIds,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            if (organizationIds == null || !organizationIds.Any())
                return new Dictionary<int, List<OrganizationIdentifierDto>>();

            var entities = await db.Set<Label.OrganizationIdentifier>()
                .AsNoTracking()
                .Where(e => e.OrganizationID != null && organizationIds.Contains((int)e.OrganizationID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.OrganizationID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new OrganizationIdentifierDto
                    {
                        OrganizationIdentifier = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads OrganizationTelecom entities for multiple organization IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<OrganizationTelecomDto>>> batchLoadOrganizationTelecomsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> organizationIds,
            string pkSecret,
            ILogger logger)
        {
            if (organizationIds == null || !organizationIds.Any())
                return new Dictionary<int, List<OrganizationTelecomDto>>();

            var entities = await db.Set<Label.OrganizationTelecom>()
                .AsNoTracking()
                .Where(e => e.OrganizationID != null && organizationIds.Contains((int)e.OrganizationID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.OrganizationID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new OrganizationTelecomDto
                    {
                        OrganizationTelecom = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads NamedEntity entities for multiple organization IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<NamedEntityDto>>> batchLoadNamedEntitiesAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> organizationIds,
            string pkSecret,
            ILogger logger)
        {
            if (organizationIds == null || !organizationIds.Any())
                return new Dictionary<int, List<NamedEntityDto>>();

            var entities = await db.Set<Label.NamedEntity>()
                .AsNoTracking()
                .Where(e => e.OrganizationID != null && organizationIds.Contains((int)e.OrganizationID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.OrganizationID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new NamedEntityDto
                    {
                        NamedEntity = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );
        }

        /**************************************************************/
        /// <summary>
        /// Batch loads Holder entities for multiple organization IDs.
        /// </summary>
        private static async Task<Dictionary<int, List<HolderDto>>> batchLoadHoldersAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> organizationIds,
            string pkSecret,
            ILogger logger)
        {
            if (organizationIds == null || !organizationIds.Any())
                return new Dictionary<int, List<HolderDto>>();

            var entities = await db.Set<Label.Holder>()
                .AsNoTracking()
                .Where(e => e.HolderOrganizationID != null && organizationIds.Contains((int)e.HolderOrganizationID))
                .ToListAsync();

            return entities
                .GroupBy(e => e.HolderOrganizationID!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new HolderDto
                    {
                        Holder = e.ToEntityWithEncryptedId(pkSecret, logger)
                    }).ToList()
                );
        }

        #endregion

        #region Document Relationship Child Batch Loaders

        private static async Task<Dictionary<int, List<BusinessOperationDto>>> batchLoadBusinessOperationsAsync(
            ApplicationDbContext db, IReadOnlyList<int> relationshipIds, string pkSecret, ILogger logger)
        {
            if (relationshipIds == null || !relationshipIds.Any()) return new();
            var entities = await db.Set<Label.BusinessOperation>().AsNoTracking()
                .Where(e => e.DocumentRelationshipID != null && relationshipIds.Contains((int)e.DocumentRelationshipID)).ToListAsync();
            return entities.GroupBy(e => e.DocumentRelationshipID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new BusinessOperationDto { BusinessOperation = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<CertificationProductLinkDto>>> batchLoadCertificationProductLinksAsync(
            ApplicationDbContext db, IReadOnlyList<int> relationshipIds, string pkSecret, ILogger logger)
        {
            if (relationshipIds == null || !relationshipIds.Any()) return new();
            var entities = await db.Set<Label.CertificationProductLink>().AsNoTracking()
                .Where(e => e.DocumentRelationshipID != null && relationshipIds.Contains((int)e.DocumentRelationshipID)).ToListAsync();
            return entities.GroupBy(e => e.DocumentRelationshipID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new CertificationProductLinkDto { CertificationProductLink = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<ComplianceActionDto>>> batchLoadComplianceActionsForRelationshipsAsync(
            ApplicationDbContext db, IReadOnlyList<int> relationshipIds, string pkSecret, ILogger logger)
        {
            if (relationshipIds == null || !relationshipIds.Any()) return new();
            var entities = await db.Set<Label.ComplianceAction>().AsNoTracking()
                .Where(e => e.DocumentRelationshipID != null && relationshipIds.Contains((int)e.DocumentRelationshipID)).ToListAsync();
            return entities.GroupBy(e => e.DocumentRelationshipID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new ComplianceActionDto { ComplianceAction = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<FacilityProductLinkDto>>> batchLoadFacilityProductLinksAsync(
            ApplicationDbContext db, IReadOnlyList<int> relationshipIds, string pkSecret, ILogger logger)
        {
            if (relationshipIds == null || !relationshipIds.Any()) return new();
            var entities = await db.Set<Label.FacilityProductLink>().AsNoTracking()
                .Where(e => e.DocumentRelationshipID != null && relationshipIds.Contains((int)e.DocumentRelationshipID)).ToListAsync();
            return entities.GroupBy(e => e.DocumentRelationshipID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new FacilityProductLinkDto { FacilityProductLink = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        private static async Task<Dictionary<int, List<DocumentRelationshipIdentifierDto>>> batchLoadDocumentRelationshipIdentifiersAsync(
            ApplicationDbContext db, IReadOnlyList<int> relationshipIds, string pkSecret, ILogger logger)
        {
            if (relationshipIds == null || !relationshipIds.Any()) return new();
            var entities = await db.Set<Label.DocumentRelationshipIdentifier>().AsNoTracking()
                .Where(e => e.DocumentRelationshipID != null && relationshipIds.Contains((int)e.DocumentRelationshipID)).ToListAsync();
            return entities.GroupBy(e => e.DocumentRelationshipID!.Value).ToDictionary(g => g.Key,
                g => g.Select(e => new DocumentRelationshipIdentifierDto { DocumentRelationshipIdentifier = e.ToEntityWithEncryptedId(pkSecret, logger) }).ToList());
        }

        #endregion

        #region Kit Product Helper Methods

        /**************************************************************/
        /// <summary>
        /// Determines if a product is a Kit based on its FormCode.
        /// Kit products use FormCode C47916 per FDA SPL specifications.
        /// </summary>
        /// <param name="product">The product entity to check.</param>
        /// <returns>True if the product is a Kit; otherwise, false.</returns>
        /// <seealso cref="Constant.KIT_FORM_CODE"/>
        /// <seealso cref="Label.Product"/>
        private static bool isKitProduct(Label.Product product)
        {
            #region implementation
            return product?.FormCode?.Equals(Constant.KIT_FORM_CODE, StringComparison.OrdinalIgnoreCase) == true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Identifies part products that belong to a Kit within the same section.
        /// Part products are identified by:
        /// - Being in the same section as a Kit product
        /// - Not being a Kit themselves (FormCode != C47916)
        /// - Having no marketing category data (marketing data belongs to Kit)
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="kitProductIds">Collection of Kit product IDs.</param>
        /// <param name="kitSectionIds">Dictionary mapping Kit ProductID to its SectionID.</param>
        /// <param name="allProductsInSections">All products loaded for the sections.</param>
        /// <returns>Dictionary mapping Kit ProductID to list of part Product entities.</returns>
        /// <seealso cref="Label.Product"/>
        /// <seealso cref="Label.MarketingCategory"/>
        /// <remarks>
        /// This method uses a view-based approach to identify Kit parts by inferring relationships
        /// from section membership and marketing data presence. This is more reliable than
        /// relying on the ProductPart table which may have data integrity issues.
        ///
        /// The logic maintains backward compatibility:
        /// - Non-Kit products are unaffected
        /// - Products with their own marketing data are not treated as parts
        /// - Only products in Kit sections without marketing data become parts
        /// </remarks>
        private static async Task<Dictionary<int, List<Label.Product>>> identifyKitPartProductsAsync(
            ApplicationDbContext db,
            IReadOnlyList<int> kitProductIds,
            Dictionary<int, int> kitSectionIds,
            IReadOnlyList<Label.Product> allProductsInSections)
        {
            #region implementation

            var result = new Dictionary<int, List<Label.Product>>();

            if (kitProductIds == null || !kitProductIds.Any())
                return result;

            // Get all product IDs that have marketing categories (these are NOT parts)
            // Parts don't have their own marketing data - they inherit from the Kit
            var productsWithMarketingData = await db.Set<Label.MarketingCategory>()
                .AsNoTracking()
                .Where(mc => mc.ProductID != null)
                .Select(mc => mc.ProductID!.Value)
                .Distinct()
                .ToListAsync();

            var productsWithMarketingSet = new HashSet<int>(productsWithMarketingData);

            // For each Kit, find its part products
            foreach (var kitProductId in kitProductIds)
            {
                if (!kitSectionIds.TryGetValue(kitProductId, out var kitSectionId))
                    continue;

                // Find products that:
                // 1. Are in the same section as this Kit
                // 2. Are NOT the Kit itself
                // 3. Are NOT Kits themselves (FormCode != C47916)
                // 4. Do NOT have their own marketing category data
                var partProducts = allProductsInSections
                    .Where(p =>
                        p.SectionID == kitSectionId &&
                        p.ProductID != kitProductId &&
                        !isKitProduct(p) &&
                        p.ProductID.HasValue &&
                        !productsWithMarketingSet.Contains(p.ProductID.Value))
                    .ToList();

                if (partProducts.Any())
                {
                    result[kitProductId] = partProducts;
                }
            }

            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets all part product IDs that should be excluded from top-level product rendering.
        /// These products will be rendered as &lt;part&gt; elements within their parent Kit.
        /// </summary>
        /// <param name="kitPartsMapping">Dictionary mapping Kit ProductID to list of part Products.</param>
        /// <returns>HashSet of ProductIDs that are Kit parts and should be excluded from top-level.</returns>
        /// <seealso cref="identifyKitPartProductsAsync"/>
        private static HashSet<int> getPartProductIdsToExclude(Dictionary<int, List<Label.Product>> kitPartsMapping)
        {
            #region implementation

            var excludeIds = new HashSet<int>();

            foreach (var kvp in kitPartsMapping)
            {
                foreach (var partProduct in kvp.Value)
                {
                    if (partProduct.ProductID.HasValue)
                    {
                        excludeIds.Add(partProduct.ProductID.Value);
                    }
                }
            }

            return excludeIds;

            #endregion
        }

        #endregion
    }
}
