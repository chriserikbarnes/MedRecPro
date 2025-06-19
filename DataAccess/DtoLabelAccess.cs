using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;
using Windows.Services.Store;
using static MedRecPro.Models.Label;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;

namespace MedRecPro.DataAccess
{
    /// <summary>
    /// Provides helper methods for building Data Transfer Objects (DTOs) from SPL Label entities.
    /// Constructs complete hierarchical data structures representing medical product documents
    /// and their associated metadata, relationships, and compliance information.
    /// </summary>
    /// <seealso cref="Label"/>
    /// <seealso cref="DocumentDto"/>
    public static class DtoLabelAccess
    {
        #region Primary Entry Point
        /**************************************************************/
        /// <summary>
        /// The primary entry point for building a list of Document DTOs with 
        /// their full hierarchy of related data. Constructs complete 
        /// document hierarchies including structured bodies, authors, 
        /// relationships, and legal authenticators.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <param name="page">Optional 1-based page number for pagination.</param>
        /// <param name="size">Optional page size for pagination.</param>
        /// <returns>List of <see cref="DocumentDto"/> representing the fetched documents and their related entities.</returns>
        /// <example>
        /// <code>
        /// var documents = await DtoLabelHelper.BuildDocumentsAsync(db, secret, logger, 1, 10);
        /// </code>
        /// </example>
        /// <remarks>
        /// This method uses sequential awaits instead of Task.WhenAll to ensure DbContext thread-safety.
        /// Each document is processed individually to build its complete DTO graph.
        /// </remarks>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.StructuredBody"/>
        /// <seealso cref="Label.DocumentAuthor"/>
        /// <seealso cref="Label.RelatedDocument"/>
        /// <seealso cref="Label.DocumentRelationship"/>
        /// <seealso cref="Label.LegalAuthenticator"/>
        public static async Task<List<DocumentDto>> BuildDocumentsAsync(
           ApplicationDbContext db,
           string pkSecret,
           ILogger logger,
           int? page = null,
           int? size = null)
        {
            #region implementation

            Stopwatch? stopwatch = Stopwatch.StartNew();

            // 1. Fetch top-level Documents with optional pagination
            var query = db.Set<Label.Document>().AsNoTracking();

            if (page.HasValue && size.HasValue)
            {
                // Apply pagination using LINQ skip/take
                query = query
                    .OrderBy(d => d.DocumentID)
                    .Skip((page.Value - 1) * size.Value)
                    .Take(size.Value);
            }

            var docs = await query.ToListAsync();
            var docDtos = new List<DocumentDto>();

            // 2. For each Document, build its complete DTO graph
            foreach (var doc in docs)
            {
                // Convert base document entity with encrypted ID
                var docDict = doc.ToEntityWithEncryptedId(pkSecret, logger);

                // 3. Sequentially build all direct children of the Document.
                // NOTE: We are intentionally using sequential awaits instead of Task.WhenAll to ensure
                // the DbContext is not used concurrently, addressing thread-safety concerns.
                var structuredBodies = await buildStructuredBodiesAsync(db, doc.DocumentID, pkSecret, logger);
                var authors = await buildDocumentAuthorsAsync(db, doc.DocumentID, pkSecret, logger);
                var relatedDocs = await buildRelatedDocumentsAsync(db, doc.DocumentID, pkSecret, logger);
                var relationships = await buildDocumentRelationshipsAsync(db, doc.DocumentID, pkSecret, logger);
                var authenticators = await buildLegalAuthenticatorsAsync(db, doc.DocumentID, pkSecret, logger);

                // Assemble complete document DTO with all child collections
                docDtos.Add(new DocumentDto
                {
                    Document = docDict,
                    StructuredBodies = structuredBodies,
                    DocumentAuthors = authors,
                    SourceRelatedDocuments = relatedDocs,
                    DocumentRelationships = relationships,
                    LegalAuthenticators = authenticators,
                    PerformanceMs = stopwatch.Elapsed.TotalMilliseconds
                });

                stopwatch.Restart();
            }
            stopwatch.Stop();
            stopwatch = null;
            return docDtos;
            #endregion
        }
        #endregion

        #region Document-Level Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of DocumentAuthor DTOs for the specified document.
        /// Retrieves and processes all authoring organizations associated with a document.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="documentId">The document ID to find authors for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of DocumentAuthor DTOs with encrypted IDs and associated organization data.</returns>
        /// <seealso cref="Label.DocumentAuthor"/>
        /// <seealso cref="DocumentAuthorDto"/>
        /// <seealso cref="OrganizationDto"/>
        /// <remarks>
        /// This method builds complete DocumentAuthor DTOs including nested Organization data.
        /// Returns an empty list if documentId is null or no authors are found.
        /// Each DocumentAuthor that has an associated OrganizationID will include the full OrganizationDto hierarchy.
        /// </remarks>
        /// <example>
        /// <code>
        /// var authors = await buildDocumentAuthorsAsync(dbContext, 123, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<List<DocumentAuthorDto>> buildDocumentAuthorsAsync(ApplicationDbContext db, int? documentId, string pkSecret, ILogger logger)
        {
            #region implementation

            // Return empty list if no documentId provided
            if (documentId == null) return new List<DocumentAuthorDto>();

            // Fetch all DocumentAuthor entities for this document with no change tracking
            var items = await db.Set<Label.DocumentAuthor>()
                .AsNoTracking()
                .Where(e => e.DocumentID == documentId)
                .ToListAsync();

            // Build DocumentAuthor DTOs with associated Organization data
            var dtos = new List<DocumentAuthorDto>();

            foreach (var item in items)
            {
                // Build associated OrganizationDto if OrganizationID is present
                OrganizationDto? orgDto = null;

                if (item.OrganizationID != null)
                {
                    orgDto = await buildOrganizationAsync(db, item.OrganizationID, pkSecret, logger);
                }

                // Create DocumentAuthorDto with encrypted IDs and associated organization
                dtos.Add(new DocumentAuthorDto
                {
                    DocumentAuthor = item.ToEntityWithEncryptedId(pkSecret, logger),
                    Organization = orgDto
                });
            }
            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of RelatedDocument DTOs for the specified document.
        /// Retrieves documents that are related to the source document through various relationship types.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="documentId">The source document ID to find related documents for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of RelatedDocument DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.RelatedDocument"/>
        private static async Task<List<RelatedDocumentDto>> buildRelatedDocumentsAsync(ApplicationDbContext db, int? documentId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (documentId == null) return new List<RelatedDocumentDto>();

            // Query related documents where this document is the source
            var items = await db.Set<Label.RelatedDocument>()
                .AsNoTracking()
                .Where(e => e.SourceDocumentID == documentId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new RelatedDocumentDto { RelatedDocument = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of DocumentRelationship DTOs for the specified document with nested collections.
        /// Constructs hierarchical relationships between organizations and their associated business operations,
        /// certification links, compliance actions, and facility product links.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="documentId">The document ID to find relationships for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of DocumentRelationship DTOs with complete nested collections.</returns>
        /// <seealso cref="Label.DocumentRelationship"/>
        /// <seealso cref="Label.BusinessOperation"/>
        /// <seealso cref="Label.CertificationProductLink"/>
        /// <seealso cref="Label.ComplianceAction"/>
        /// <seealso cref="Label.FacilityProductLink"/>
        private static async Task<List<DocumentRelationshipDto>> buildDocumentRelationshipsAsync(ApplicationDbContext db, int? documentId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (documentId == null) return new List<DocumentRelationshipDto>();

            // Get all document relationships for this document
            var relationships = await db.Set<Label.DocumentRelationship>()
                .AsNoTracking()
                .Where(e => e.DocumentID == documentId)
                .ToListAsync();

            var dtos = new List<DocumentRelationshipDto>();

            // For each relationship, build its nested collections
            foreach (var rel in relationships)
            {
                // Build all child collections for this relationship
                var businessOps = await buildBusinessOperationsAsync(db, rel.DocumentRelationshipID, pkSecret, logger);

                var certLinks = await buildCertificationProductLinksAsync(db, rel.DocumentRelationshipID, pkSecret, logger);

                var complianceActions = await buildComplianceActionsForRelationshipAsync(db, rel.DocumentRelationshipID, pkSecret, logger);

                var facilityLinks = await buildFacilityProductLinksAsync(db, rel.DocumentRelationshipID, pkSecret, logger);

                // Assemble complete relationship DTO with all nested data
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
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of LegalAuthenticator DTOs for the specified document.
        /// Retrieves legal signature and authentication information associated with a document.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="documentId">The document ID to find legal authenticators for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of LegalAuthenticator DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.LegalAuthenticator"/>
        private static async Task<List<LegalAuthenticatorDto>> buildLegalAuthenticatorsAsync(ApplicationDbContext db, int? documentId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (documentId == null) return new List<LegalAuthenticatorDto>();

            // Query legal authenticators for the specified document
            var items = await db.Set<Label.LegalAuthenticator>()
                .AsNoTracking()
                .Where(e => e.DocumentID == documentId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new LegalAuthenticatorDto { LegalAuthenticator = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of StructuredBody DTOs for the specified document with nested section hierarchies.
        /// Constructs the main content structure containing organized sections of document data.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="documentId">The document ID to find structured bodies for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of StructuredBody DTOs with complete section hierarchies.</returns>
        /// <seealso cref="Label.StructuredBody"/>
        private static async Task<List<StructuredBodyDto>> buildStructuredBodiesAsync(ApplicationDbContext db, int? documentId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (documentId == null) return new List<StructuredBodyDto>();

            // Get all structured bodies for this document
            var sbs = await db.Set<Label.StructuredBody>()
                .AsNoTracking()
                .Where(sb => sb.DocumentID == documentId)
                .ToListAsync();

            var sbDtos = new List<StructuredBodyDto>();

            // For each structured body, build its sections
            foreach (var sb in sbs)
            {
                var sectionDtos = await buildSectionsAsync(db, sb.StructuredBodyID, pkSecret, logger);

                sbDtos.Add(new StructuredBodyDto
                {
                    StructuredBody = sb.ToEntityWithEncryptedId(pkSecret, logger),
                    Sections = sectionDtos
                });
            }
            return sbDtos;
            #endregion
        }
        #endregion

        #region Content Hierarchy Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of Section DTOs for the specified structured body with all 
        /// nested collections. Constructs comprehensive section data including 
        /// products, highlights, media, substances, and various specialized content.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="structuredBodyId">The structured body ID to find sections for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of Section DTOs with complete nested collections.</returns>
        /// <seealso cref="Label.Section"/>
        private static async Task<List<SectionDto>> buildSectionsAsync(ApplicationDbContext db, int? structuredBodyId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (structuredBodyId == null) return new List<SectionDto>();

            // Get all sections for this structured body
            var sections = await db.Set<Label.Section>()
                .AsNoTracking()
                .Where(s => s.StructuredBodyID == structuredBodyId)
                .ToListAsync();

            var sectionDtos = new List<SectionDto>();

            // For each section, build all its nested collections
            foreach (var section in sections)
            {
                // Build all child collections for this section
                var billingUnitIndexes = await buildBillingUnitIndexesAsync(db, section.SectionID, pkSecret, logger);

                var children = await buildChildSectionHierarchyDtoAsync(db, section.SectionID, pkSecret, logger) ?? new List<SectionHierarchyDto>();

                var content = await buildSectionTextContentDtoAsync(db, section.SectionID, pkSecret, logger)
                    ?? new List<SectionTextContentDto>();

                var highlights = await buildSectionExcerptHighlightsAsync(db, section.SectionID, pkSecret, logger);

                var identifiedSubstances = await buildIdentifiedSubstancesAsync(db, section.SectionID, pkSecret, logger);

                var interactionIssues = await buildInteractionIssuesAsync(db, section.SectionID, pkSecret, logger);

                var media = await buildObservationMediaAsync(db, section.SectionID, pkSecret, logger);

                var NCTLinks = await buildNCTLinkDtoAsync(db, section.SectionID, pkSecret, logger);

                var parents = await buildParentSectionHierarchyDtoAsync(db, section.SectionID, pkSecret, logger) ?? new List<SectionHierarchyDto>();

                var productConcepts = await buildProductConceptsAsync(db, section.SectionID, pkSecret, logger);

                var products = await buildProductsAsync(db, section.SectionID, pkSecret, logger);

                var protocols = await buildProtocolsAsync(db, section.SectionID, pkSecret, logger);

                var remsMaterials = await buildREMSMaterialsAsync(db, section.SectionID, pkSecret, logger);

                var remsResources = await buildREMSElectronicResourcesAsync(db, section.SectionID, pkSecret, logger);

                var warningLetterDates = await buildWarningLetterDatesAsync(db, section.SectionID, pkSecret, logger);

                var warningLetterInfos = await buildWarningLetterProductInfosAsync(db, section.SectionID, pkSecret, logger);

                // Assemble complete section DTO with all nested data
                sectionDtos.Add(new SectionDto
                {
                    BillingUnitIndexes = billingUnitIndexes,
                    ChildSectionHierarchies = children,
                    ExcerptHighlights = highlights,
                    IdentifiedSubstances = identifiedSubstances,
                    InteractionIssues = interactionIssues,
                    NCTLinks = NCTLinks,
                    ObservationMedia = media,
                    ParentSectionHierarchies = parents,
                    ProductConcepts = productConcepts,
                    Products = products,
                    Protocols = protocols,
                    REMSElectronicResources = remsResources,
                    REMSMaterials = remsMaterials,
                    Section = section.ToEntityWithEncryptedId(pkSecret, logger),
                    TextContents = content,
                    WarningLetterDates = warningLetterDates,
                    WarningLetterProductInfos = warningLetterInfos
                });
            }
            return sectionDtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the NCTLink DTOs for the specified section.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionID">The section ID to find NCT links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of NCTLink DTOs for the section.</returns>
        /// <seealso cref="Label.NCTLink"/>
        /// <seealso cref="NCTLinkDto"/>
        /// <seealso cref="SectionDto"/>
        private static async Task<List<NCTLinkDto>> buildNCTLinkDtoAsync(ApplicationDbContext db, int? sectionID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no section ID is provided
            if (sectionID == null)
                return new List<NCTLinkDto>();

            // Query NCT links for the specified section
            var entity = await db.Set<Label.NCTLink>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<NCTLinkDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new NCTLinkDto
            {
                NCTLink = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<NCTLinkDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of section hierarchy DTOs where the specified section ID is the parent.
        /// SectionHierarchy contains ParentSectionID and ChildSectionID relationships.
        /// </summary>
        /// <param name="db">The database context for querying section hierarchy entities.</param>
        /// <param name="sectionID">The section identifier used as parent section ID to filter section hierarchies.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of SectionHierarchyDto objects representing parent relationships, or null if no section ID provided or no entities found.</returns>
        /// <seealso cref="Label.SectionHierarchy"/>
        /// <seealso cref="SectionHierarchyDto"/>
        private static async Task<List<SectionHierarchyDto>?> buildParentSectionHierarchyDtoAsync(ApplicationDbContext db, int? sectionID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no section ID is provided
            if (sectionID == null)
                return null;

            // Query section hierarchies where the specified section is the parent
            var entity = await db.Set<Label.SectionHierarchy>()
                .AsNoTracking()
                .Where(e => e.ParentSectionID == sectionID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new SectionHierarchyDto
            {
                SectionHierarchy = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of section hierarchy DTOs where the specified section ID is the child.
        /// SectionHierarchy contains ParentSectionID and ChildSectionID relationships.
        /// </summary>
        /// <param name="db">The database context for querying section hierarchy entities.</param>
        /// <param name="sectionID">The section identifier used as child section ID to filter section hierarchies.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of SectionHierarchyDto objects representing child relationships, or null if no section ID provided or no entities found.</returns>
        /// <seealso cref="Label.SectionHierarchy"/>
        /// <seealso cref="SectionHierarchyDto"/>
        private static async Task<List<SectionHierarchyDto>?> buildChildSectionHierarchyDtoAsync(ApplicationDbContext db, int? sectionID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no section ID is provided
            if (sectionID == null)
                return null;

            // Query section hierarchies where the specified section is the child
            var entity = await db.Set<Label.SectionHierarchy>()
                .AsNoTracking()
                .Where(e => e.ChildSectionID == sectionID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new SectionHierarchyDto
            {
                SectionHierarchy = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of section text content DTOs for a specified section ID.
        /// SectionID is a foreign key to the Section table.
        /// </summary>
        /// <param name="db">The database context for querying section text content entities.</param>
        /// <param name="sectionID">The section identifier to filter section text content.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of SectionTextContentDto objects, or null if no section ID provided or no entities found.</returns>
        /// <seealso cref="Label.SectionTextContent"/>
        /// <seealso cref="SectionTextContentDto"/>
        private static async Task<List<SectionTextContentDto>?> buildSectionTextContentDtoAsync(ApplicationDbContext db, int? sectionID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no section ID is provided
            if (sectionID == null)
                return null;

            List<SectionTextContentDto> sectionTextContentDtos = new List<SectionTextContentDto>();

            // Query section text content for the specified section
            var entity = await db.Set<Label.SectionTextContent>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            foreach (var e in entity)
            {
                // Build all child collections for this section text content
                var renderedMedias = await buildRenderedMediasAsync(db, e.SectionTextContentID, pkSecret, logger);

                var textTables = await buildTextTablesAsync(db, e.SectionTextContentID, pkSecret, logger);

                var textLists = await buildTextListsAsync(db, e.SectionTextContentID, pkSecret, logger);

                // Create SectionTextContentDto with encrypted ID and nested collections
                sectionTextContentDtos.Add(new SectionTextContentDto
                {
                    SectionTextContent = e.ToEntityWithEncryptedId(pkSecret, logger),
                    RenderedMedias = renderedMedias,
                    TextTables = textTables,
                    TextLists = textLists
                });
            }

            // Return DTOs with encrypted IDs
            return sectionTextContentDtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Represents the renderMultimedia tag, linking text content to an ObservationMedia entry.
        /// Builds rendered media DTOs for the specified section text content.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionTextContentID">The section text content ID to find rendered media for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of rendered media DTOs for the section text content.</returns>
        /// <seealso cref="Label.RenderedMedia"/>
        /// <seealso cref="RenderedMediaDto"/>
        /// <seealso cref="SectionTextContentDto"/>
        private static async Task<List<RenderedMediaDto>> buildRenderedMediasAsync(ApplicationDbContext db, int? sectionTextContentID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no section text content ID is provided
            if (sectionTextContentID == null)
                return new List<RenderedMediaDto>();

            // Query rendered media for the specified section text content
            var entity = await db.Set<Label.RenderedMedia>()
                .AsNoTracking()
                .Where(e => e.SectionTextContentID == sectionTextContentID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<RenderedMediaDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new RenderedMediaDto
            {
                RenderedMedia = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<RenderedMediaDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores details specific to table elements.
        /// Builds text table DTOs with their associated rows for the specified section text content.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionTextContentID">The section text content ID to find text tables for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text table DTOs with nested row data for the section text content.</returns>
        /// <seealso cref="Label.TextTable"/>
        /// <seealso cref="TextTableDto"/>
        /// <seealso cref="SectionTextContentDto"/>
        private static async Task<List<TextTableDto>> buildTextTablesAsync(ApplicationDbContext db, int? sectionTextContentID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no section text content ID is provided
            if (sectionTextContentID == null)
                return new List<TextTableDto>();

            var dtos = new List<TextTableDto>();

            // Query text tables for the specified section text content
            var entity = await db.Set<Label.TextTable>()
                .AsNoTracking()
                .Where(e => e.SectionTextContentID == sectionTextContentID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<TextTableDto>();

            // Build DTOs with nested row data for each text table
            foreach (var e in entity)
            {
                // Build all rows for this text table
                var rows = await buildTextTableRowsAsync(db, e.TextTableID, pkSecret, logger);

                dtos.Add(new TextTableDto
                {
                    TextTable = e.ToEntityWithEncryptedId(pkSecret, logger),
                    TextTableRows = rows
                });
            }

            // Return completed DTOs
            return dtos ?? new List<TextTableDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores details specific to individual tr elements.
        /// Builds text table row DTOs with their associated cells for the specified text table.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="textTableID">The text table ID to find rows for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text table row DTOs with nested cell data for the text table.</returns>
        /// <seealso cref="Label.TextTableRow"/>
        /// <seealso cref="TextTableRowDto"/>
        /// <seealso cref="TextTableDto"/>
        private static async Task<List<TextTableRowDto>> buildTextTableRowsAsync(ApplicationDbContext db, int? textTableID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no text table ID is provided
            if (textTableID == null)
                return new List<TextTableRowDto>();

            var dtos = new List<TextTableRowDto>();

            // Query text table rows for the specified text table
            var entity = await db.Set<Label.TextTableRow>()
                .AsNoTracking()
                .Where(e => e.TextTableID == textTableID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<TextTableRowDto>();

            // Build DTOs with nested cell data for each text table row
            foreach (var e in entity)
            {
                // Build all cells for this text table row
                var cells = await buildTextTableCellsAsync(db, e.TextTableRowID, pkSecret, logger);

                dtos.Add(new TextTableRowDto
                {
                    TextTableRow = e.ToEntityWithEncryptedId(pkSecret, logger),
                    TextTableCells = cells
                });
            }

            // Return completed DTOs
            return dtos ?? new List<TextTableRowDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores details specific to individual td elements.
        /// Builds text table cell DTOs for the specified text table row.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="textTableRowID">The text table row ID to find cells for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text table cell DTOs for the text table row.</returns>
        /// <seealso cref="Label.TextTableCell"/>
        /// <seealso cref="TextTableCellDto"/>
        /// <seealso cref="TextTableRowDto"/>
        private static async Task<List<TextTableCellDto>> buildTextTableCellsAsync(ApplicationDbContext db, int? textTableRowID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no text table row ID is provided
            if (textTableRowID == null)
                return new List<TextTableCellDto>();

            // Query text table cells for the specified text table row
            var entity = await db.Set<Label.TextTableCell>()
                .AsNoTracking()
                .Where(e => e.TextTableRowID == textTableRowID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<TextTableCellDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new TextTableCellDto
            {
                TextTableCell = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<TextTableCellDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores details specific to list elements.
        /// Builds text list DTOs with their associated items for the specified section text content.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionTextContentID">The section text content ID to find text lists for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text list DTOs with nested item data for the section text content.</returns>
        /// <seealso cref="Label.TextList"/>
        /// <seealso cref="TextListDto"/>
        /// <seealso cref="SectionTextContentDto"/>
        private static async Task<List<TextListDto>> buildTextListsAsync(ApplicationDbContext db, int? sectionTextContentID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no section text content ID is provided
            if (sectionTextContentID == null)
                return new List<TextListDto>();

            var dtos = new List<TextListDto>();

            // Query text lists for the specified section text content
            var entity = await db.Set<Label.TextList>()
                .AsNoTracking()
                .Where(e => e.SectionTextContentID == sectionTextContentID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<TextListDto>();

            // Build DTOs with nested item data for each text list
            foreach (var e in entity)
            {
                // Build all items for this text list
                var items = await buildTextListItemsAsync(db, e.TextListID, pkSecret, logger);

                dtos.Add(new TextListDto
                {
                    TextList = e.ToEntityWithEncryptedId(pkSecret, logger),
                    TextListItems = items
                });
            }

            // Return completed DTOs
            return dtos ?? new List<TextListDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores details specific to list item elements.
        /// Builds text list item DTOs for the specified text list.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="textListID">The text list ID to find items for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text list item DTOs for the text list.</returns>
        /// <seealso cref="Label.TextListItem"/>
        /// <seealso cref="TextListItemDto"/>
        /// <seealso cref="TextListDto"/>
        private static async Task<List<TextListItemDto>?> buildTextListItemsAsync(ApplicationDbContext db, int? textListID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no text list ID is provided
            if (textListID == null)
                return new List<TextListItemDto>();

            // Query text list items for the specified text list
            var entity = await db.Set<Label.TextListItem>()
                .AsNoTracking()
                .Where(e => e.TextListID == textListID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<TextListItemDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new TextListItemDto
            {
                TextListItem = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<TextListItemDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of SectionExcerptHighlight DTOs for the specified section.
        /// Retrieves highlighted text content within excerpt sections such as Boxed Warnings and Indications.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find excerpt highlights for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of SectionExcerptHighlight DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.SectionExcerptHighlight"/>
        private static async Task<List<SectionExcerptHighlightDto>> buildSectionExcerptHighlightsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<SectionExcerptHighlightDto>();

            // Query excerpt highlights for the specified section
            var items = await db.Set<Label.SectionExcerptHighlight>().AsNoTracking().Where(e => e.SectionID == sectionId).ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items.Select(item => new SectionExcerptHighlightDto { SectionExcerptHighlight = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ObservationMedia DTOs for the specified section.
        /// Retrieves image and media metadata associated with section content.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find observation media for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ObservationMedia DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ObservationMedia"/>
        private static async Task<List<ObservationMediaDto>> buildObservationMediaAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<ObservationMediaDto>();

            // Query observation media for the specified section
            var items = await db.Set<Label.ObservationMedia>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ObservationMediaDto { ObservationMedia = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductConcept DTOs for the specified section.
        /// Retrieves abstract or application-specific product/kit concept definitions.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find product concepts for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductConcept DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ProductConcept"/>
        private static async Task<List<ProductConceptDto>> buildProductConceptsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<ProductConceptDto>();

            var dtos = new List<ProductConceptDto>();

            // Query product concepts for the specified section
            var items = await db.Set<Label.ProductConcept>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<ProductConceptDto>();

            foreach (var item in items)
            {
                // Build all equivalences for this product concept
                var equivalences = await buildProductConceptEquivalencesAsync(db, item.ProductConceptID, pkSecret, logger);

                dtos.Add(new ProductConceptDto
                {
                    ProductConcept = item.ToEntityWithEncryptedId(pkSecret, logger),
                    ProductConceptEquivalences = equivalences
                });
            }

            // Transform entities to DTOs with encrypted IDs
            return dtos ?? new List<ProductConceptDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Links an Application Product Concept to its corresponding 
        /// Abstract Product Concept. Retrieves product concept equivalences 
        /// for a specified product concept ID and transforms them into DTOs 
        /// with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productConceptID">The unique identifier of the product concept to find equivalences for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of ProductConceptEquivalenceDto objects representing the equivalences, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SectionDto > ProductConceptDto > ProductConceptEquivalenceDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var equivalences = await buildProductConceptEquivalencesAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ProductConceptEquivalence"/>
        /// <seealso cref="ProductConceptEquivalenceDto"/>
        private static async Task<List<ProductConceptEquivalenceDto>> buildProductConceptEquivalencesAsync(ApplicationDbContext db, int? productConceptID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product concept ID provided
            if (productConceptID == null) return new List<ProductConceptEquivalenceDto>();

            // Query for equivalents of the specified product concept using read-only tracking
            var items = await db.Set<Label.ProductConceptEquivalence>()
                .AsNoTracking()
                .Where(e => e.ProductConceptEquivalenceID == productConceptID)
                .ToListAsync();

            // Return empty list if no equivalences found
            if (items == null || !items.Any())
                return new List<ProductConceptEquivalenceDto>();

            // Transform entities to DTOs with encrypted IDs for security
            return items
                .Select(item => new ProductConceptEquivalenceDto { ProductConcept = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of InteractionIssue DTOs for the specified section.
        /// Retrieves drug interaction issues and their associated details.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find interaction issues for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of InteractionIssue DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.InteractionIssue"/>
        private static async Task<List<InteractionIssueDto>> buildInteractionIssuesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<InteractionIssueDto>();

            var dtos = new List<InteractionIssueDto>();

            // Query interaction issues for the specified section
            var items = await db.Set<Label.InteractionIssue>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<InteractionIssueDto>();

            foreach (var item in items)
            {
                // Build all consequences for this interaction issue
                var consequences = await buildInteractionConsequencesAsync(db, item.InteractionIssueID, pkSecret, logger);

                dtos.Add(new InteractionIssueDto
                {
                    InteractionIssue = item.ToEntityWithEncryptedId(pkSecret, logger),
                    InteractionConsequences = consequences
                });
            }

            // return DTOs with encrypted IDs
            return dtos ?? new List<InteractionIssueDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the list of consequences from an interaction issue.
        /// Retrieves interaction consequences associated with a 
        /// specified interaction issue ID and transforms them into DTOs 
        /// with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="interactionIssueId">The unique identifier of the interaction issue to find consequences for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of InteractionConsequenceDto objects representing the consequences, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SectionDto > InteractionIssueDto > InteractionConsequenceDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var consequences = await buildInteractionConsequencesAsync(dbContext, 456, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.InteractionConsequence"/>
        /// <seealso cref="InteractionConsequenceDto"/>
        private static async Task<List<InteractionConsequenceDto>> buildInteractionConsequencesAsync(ApplicationDbContext db, int? interactionIssueId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no interaction issue ID provided
            if (interactionIssueId == null) return new List<InteractionConsequenceDto>();

            // Query interaction consequences for the specified interaction issue using read-only tracking
            var items = await db.Set<Label.InteractionConsequence>()
                .AsNoTracking()
                .Where(e => e.InteractionIssueID == interactionIssueId)
                .ToListAsync();

            // Return empty list if no consequences found
            if (items == null || !items.Any())
                return new List<InteractionConsequenceDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new InteractionConsequenceDto
                {
                    InteractionConsequence = item.ToEntityWithEncryptedId(pkSecret, logger)
                }).ToList() ?? new List<InteractionConsequenceDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of BillingUnitIndex DTOs for the specified section.
        /// Retrieves links between NDC Package Codes and their NCPDP Billing Units.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find billing unit indexes for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of BillingUnitIndex DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.BillingUnitIndex"/>
        private static async Task<List<BillingUnitIndexDto>> buildBillingUnitIndexesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<BillingUnitIndexDto>();

            // Query billing unit indexes for the specified section
            var items = await db.Set<Label.BillingUnitIndex>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new BillingUnitIndexDto { BillingUnitIndex = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of WarningLetterProductInfo DTOs for the specified section.
        /// Retrieves key product identification details referenced in Warning Letter Alert Indexing documents.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find warning letter product info for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of WarningLetterProductInfo DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.WarningLetterProductInfo"/>
        private static async Task<List<WarningLetterProductInfoDto>> buildWarningLetterProductInfosAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<WarningLetterProductInfoDto>();

            // Query warning letter product info for the specified section
            var items = await db.Set<Label.WarningLetterProductInfo>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new WarningLetterProductInfoDto { WarningLetterProductInfo = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of WarningLetterDate DTOs for the specified section.
        /// Retrieves issue dates and optional resolution dates for warning letter alerts.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find warning letter dates for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of WarningLetterDate DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.WarningLetterDate"/>
        private static async Task<List<WarningLetterDateDto>> buildWarningLetterDatesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<WarningLetterDateDto>();

            // Query warning letter dates for the specified section
            var items = await db.Set<Label.WarningLetterDate>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new WarningLetterDateDto { WarningLetterDate = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of Protocol DTOs for the specified section.
        /// Retrieves REMS protocols defined within sections.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find protocols for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of Protocol DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.Protocol"/>
        private static async Task<List<ProtocolDto>> buildProtocolsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<ProtocolDto>();

            var dtos = new List<ProtocolDto>();

            // Query protocols for the specified section
            var items = await db.Set<Label.Protocol>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<ProtocolDto>();

            foreach (var e in items)
            {
                // Build all REMS approvals for this protocol
                var remsApprovals = await buildREMSApprovalsAsync(db, e.ProtocolID, pkSecret, logger);

                // Build all requirements for this protocol
                var requirements = await buildRequirementsAsync(db, e.ProtocolID, pkSecret, logger);

                dtos.Add(new ProtocolDto
                {
                    Protocol = e.ToEntityWithEncryptedId(pkSecret, logger),
                    REMSApprovals = remsApprovals,
                    Requirements = requirements
                });
            }

            // Transform entities to DTOs with encrypted IDs
            return dtos ?? new List<ProtocolDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores the REMS approval details associated with the first protocol mention.
        /// Retrieves REMS approval records for a specified protocol ID and 
        /// transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="protocolID">The unique identifier of the protocol to find REMS approvals for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of REMSApprovalDto objects representing the REMS approvals, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SectionDto > ProtocolDto > REMSApprovalDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var remsApprovals = await buildREMSApprovalsAsync(dbContext, 789, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.REMSApproval"/>
        /// <seealso cref="REMSApprovalDto"/>
        private static async Task<List<REMSApprovalDto>> buildREMSApprovalsAsync(ApplicationDbContext db, int? protocolID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no protocol ID provided
            if (protocolID == null) return new List<REMSApprovalDto>();

            // Query REMS approvals for the specified protocol using read-only tracking
            var items = await db.Set<Label.REMSApproval>()
                .AsNoTracking()
                .Where(e => e.ProtocolID == protocolID)
                .ToListAsync();

            // Return empty list if no REMS approvals found
            if (items == null || !items.Any())
                return new List<REMSApprovalDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new REMSApprovalDto { REMSApproval = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<REMSApprovalDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Represents a REMS requirement or monitoring observation within a protocol.
        /// Retrieves requirement records for a specified protocol ID along with their 
        /// associated stakeholders and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="protocolID">The unique identifier of the protocol to find requirements for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of RequirementDto objects representing the requirements with their stakeholders, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SectionDto > ProtocolDto > RequirementDto
        /// Each requirement is enriched with its associated stakeholder information.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var requirements = await buildRequirementsAsync(dbContext, 789, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.Requirement"/>
        /// <seealso cref="RequirementDto"/>
        /// <seealso cref="buildStakeholdersAsync"/>
        private static async Task<List<RequirementDto>> buildRequirementsAsync(ApplicationDbContext db, int? protocolID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no protocol ID provided
            if (protocolID == null) return new List<RequirementDto>();

            var dtos = new List<RequirementDto>();

            // Query requirements for the specified protocol using read-only tracking
            var items = await db.Set<Label.Requirement>()
                .AsNoTracking()
                .Where(e => e.ProtocolID == protocolID)
                .ToListAsync();

            // Return empty list if no requirements found
            if (items == null || !items.Any())
                return new List<RequirementDto>();

            // Process each requirement and build associated stakeholder data
            foreach (var item in items)
            {
                // Build all stakeholders for this requirement
                var stakeholders = await buildStakeholdersAsync(db, item.StakeholderID, pkSecret, logger);

                // Create requirement DTO with encrypted ID and associated stakeholders
                dtos.Add(new RequirementDto
                {
                    Requirement = item.ToEntityWithEncryptedId(pkSecret, logger),
                    Stakeholders = stakeholders
                });
            }

            // Return processed requirements with stakeholder data, ensuring non-null result
            return dtos ?? new List<RequirementDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Lookup table for REMS stakeholder types. Retrieves stakeholder 
        /// records for a specified stakeholder ID and transforms them into 
        /// DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="stakeholderID">The unique identifier of the stakeholder to find records for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of StakeholderDto objects representing the stakeholders, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SectionDto > ProtocolDto > RequirementDto > StakeholderDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// Note: Currently queries REMSApproval table using stakeholderID as ProtocolID - this may need review.
        /// </remarks>
        /// <example>
        /// <code>
        /// var stakeholders = await buildStakeholdersAsync(dbContext, 101, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.REMSApproval"/>
        /// <seealso cref="StakeholderDto"/>
        private static async Task<List<StakeholderDto>> buildStakeholdersAsync(ApplicationDbContext db, int? stakeholderID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no stakeholder ID provided
            if (stakeholderID == null) return new List<StakeholderDto>();

            // Query stakeholder data using REMSApproval table with stakeholderID as ProtocolID filter
            var items = await db.Set<Label.REMSApproval>()
                .AsNoTracking()
                .Where(e => e.ProtocolID == stakeholderID)
                .ToListAsync();

            // Return empty list if no stakeholder records found
            if (items == null || !items.Any())
                return new List<StakeholderDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new StakeholderDto { Stakeholder = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<StakeholderDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of REMSMaterial DTOs for the specified section.
        /// Retrieves references to REMS materials with potential document attachments.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find REMS materials for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of REMSMaterial DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.REMSMaterial"/>
        private static async Task<List<REMSMaterialDto>> buildREMSMaterialsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<REMSMaterialDto>();

            // Query REMS materials for the specified section
            var items = await db.Set<Label.REMSMaterial>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<REMSMaterialDto>();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new REMSMaterialDto { REMSMaterial = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of REMSElectronicResource DTOs for the specified section.
        /// Retrieves references to REMS electronic resources including URLs and URNs.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find REMS electronic resources for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of REMSElectronicResource DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.REMSElectronicResource"/>
        private static async Task<List<REMSElectronicResourceDto>> buildREMSElectronicResourcesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<REMSElectronicResourceDto>();

            // Query REMS electronic resources for the specified section
            var items = await db.Set<Label.REMSElectronicResource>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new REMSElectronicResourceDto { REMSElectronicResource = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }
        #endregion

        #region Product Hierarchy Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of Product DTOs for the specified section with complete nested hierarchies.
        /// Constructs comprehensive product data including generic medicines, identifiers, routes, web links,
        /// business operation links, responsible person links, product instances, and ingredients.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find products for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of Product DTOs with complete nested collections.</returns>
        /// <seealso cref="Label.Product"/>
        private static async Task<List<ProductDto>> buildProductsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<ProductDto>();

            // Get all products for this section
            var products = await db.Set<Label.Product>()
                .AsNoTracking()
                .Where(p => p.SectionID == sectionId)
                .ToListAsync();

            if (products == null || !products.Any())
                return new List<ProductDto>();

            var productDtos = new List<ProductDto>();

            // For each product, build all its nested collections
            foreach (var product in products)
            {
                // Build all child collections for this product
                var additionalIds = await buildAdditionalIdentifiersAsync(db, product.ProductID, pkSecret, logger);
                var businessOpLinks = await buildBusinessOperationProductLinksAsync(db, product.ProductID, pkSecret, logger);
                var characteristics = await buildCharacteristicsDtoAsync(db, product.ProductID, pkSecret, logger);
                var childLots = await buildLabelLotHierarchyDtoAsync(db, product.ProductID, pkSecret, logger);
                var dosingSpecs = await buildDosingSpecificationsAsync(db, product.ProductID, pkSecret, logger);
                var equivalents = await buildEquivalentEntitiesAsync(db, product.ProductID, pkSecret, logger);
                var genericMeds = await buildGenericMedicinesAsync(db, product.ProductID, pkSecret, logger);
                var ingredientInstances = await buildProductIngredientInstancesAsync(db, product.ProductID, pkSecret, logger);
                var ingredients = await buildIngredientsAsync(db, product.ProductID, pkSecret, logger);
                var marketingCats = await buildMarketingCategoriesDtoAsync(db, product.ProductID, pkSecret, logger);
                var packageLevels = await buildPackagingLevelsAsync(db, product.ProductID, pkSecret, logger);
                var parentLots = await buildFillLotHierarchyDtoAsync(db, product.ProductID, pkSecret, logger);
                var partsOfAssembly = await buildPartOfAssembliesAsync(db, product.ProductID, pkSecret, logger);
                var policies = await buildPoliciesAsync(db, product.ProductID, pkSecret, logger);
                var productIds = await buildProductIdentifiersAsync(db, product.ProductID, pkSecret, logger);
                var productInstances = await buildProductInstancesAsync(db, product.ProductID, pkSecret, logger);
                var productParts = await buildProductPartsDtoAsync(db, product.ProductID, pkSecret, logger);
                var productRoutes = await buildProductRouteOfAdministrationsAsync(db, product.ProductID, pkSecret, logger);
                var respPersonLinks = await buildResponsiblePersonLinksAsync(db, product.ProductID, pkSecret, logger);
                var specializedKinds = await buildSpecializedKindsAsync(db, product.ProductID, pkSecret, logger);
                var webLinks = await buildProductWebLinksAsync(db, product.ProductID, pkSecret, logger);

                // Assemble complete product DTO with all nested data
                productDtos.Add(new ProductDto
                {
                    Product = product.ToEntityWithEncryptedId(pkSecret, logger),
                    ProductParts = productParts,
                    GenericMedicines = genericMeds,
                    ParentLotHierarchies = parentLots,
                    ChildLotHierarchies = childLots,
                    MarketingCategories = marketingCats,
                    ProductIdentifiers = productIds,
                    PackagingLevels = packageLevels,
                    ProductRouteOfAdministrations = productRoutes,
                    ProductWebLinks = webLinks,
                    BusinessOperationProductLinks = businessOpLinks,
                    ResponsiblePersonLinks = respPersonLinks,
                    Characteristics = characteristics,
                    ProductInstances = productInstances,
                    Ingredients = ingredients,
                    IngredientInstances = ingredientInstances,
                    AdditionalIdentifiers = additionalIds,
                    DosingSpecifications = dosingSpecs,
                    EquivalentEntities = equivalents,
                    PartOfAssemblies = partsOfAssembly,
                    Policies = policies,
                    SpecializedKinds = specializedKinds
                });
            }
            return productDtos ?? new List<ProductDto>();
            #endregion
        }
        /**************************************************************/
        /// <summary>
        /// Additional identifiers for the product. Retrieves additional 
        /// identifier records for a specified product ID and transforms 
        /// them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find additional identifiers for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of AdditionalIdentifierDto objects representing the additional identifiers, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > AdditionalIdentifiersDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var additionalIdentifiers = await buildAdditionalIdentifiersAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.AdditionalIdentifier"/>
        /// <seealso cref="AdditionalIdentifierDto"/>
        private static async Task<List<AdditionalIdentifierDto>> buildAdditionalIdentifiersAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null) return new List<AdditionalIdentifierDto>();

            // Query additional identifiers for the specified product using read-only tracking
            var items = await db.Set<Label.AdditionalIdentifier>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return empty list if no additional identifiers found
            if (items == null || !items.Any())
                return new List<AdditionalIdentifierDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new AdditionalIdentifierDto { AdditionalIdentifier = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<AdditionalIdentifierDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Dosing specs for the product. Retrieves dosing specification records 
        /// for a specified product ID and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find dosing specifications for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of DosingSpecificationDto objects representing the dosing specifications, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > DosingSpecificationDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var dosingSpecs = await buildDosingSpecificationsAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.DosingSpecification"/>
        /// <seealso cref="DosingSpecificationDto"/>
        private static async Task<List<DosingSpecificationDto>> buildDosingSpecificationsAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null) return new List<DosingSpecificationDto>();

            // Query dosing specifications for the specified product using read-only tracking
            var items = await db.Set<Label.DosingSpecification>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return empty list if no dosing specifications found
            if (items == null || !items.Any())
                return new List<DosingSpecificationDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new DosingSpecificationDto { DosingSpecification = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<DosingSpecificationDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Equivalent entities for the product. Retrieves equivalent 
        /// entity records for a specified product ID and transforms 
        /// them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find equivalent entities for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of EquivalentEntityDto objects representing the equivalent entities, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > EquivalentEntityDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var equivalentEntities = await buildEquivalentEntitiesAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.EquivalentEntity"/>
        /// <seealso cref="EquivalentEntityDto"/>
        private static async Task<List<EquivalentEntityDto>> buildEquivalentEntitiesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null) return new List<EquivalentEntityDto>();

            // Query equivalent entities for the specified product using read-only tracking
            var items = await db.Set<Label.EquivalentEntity>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return empty list if no equivalent entities found
            if (items == null || !items.Any())
                return new List<EquivalentEntityDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new EquivalentEntityDto { EquivalentEntity = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<EquivalentEntityDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Equivalent entities for the product where ProductID equals PrimaryProductID. Retrieves 
        /// part of assembly records for a specified product ID and transforms them into 
        /// DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the primary product to find assembly parts for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of PartOfAssemblyDto objects representing the assembly parts, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > PartOfAssemblyDto
        /// The query filters by PrimaryProductID to find parts that belong to the specified product assembly.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var assemblyParts = await buildPartOfAssembliesAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.PartOfAssembly"/>
        /// <seealso cref="PartOfAssemblyDto"/>
        private static async Task<List<PartOfAssemblyDto>> buildPartOfAssembliesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null) return new List<PartOfAssemblyDto>();

            // Query assembly parts where the product is the primary product using read-only tracking
            var items = await db.Set<Label.PartOfAssembly>()
                .AsNoTracking()
                .Where(e => e.PrimaryProductID == productID)
                .ToListAsync();

            // Return empty list if no assembly parts found
            if (items == null || !items.Any())
                return new List<PartOfAssemblyDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new PartOfAssemblyDto { PartOfAssembly = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<PartOfAssemblyDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Policies for the product where ProductID equals productID. Retrieves 
        /// policy records for a specified product ID and transforms them into 
        /// DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find policies for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of PolicyDto objects representing the policies, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > PolicyDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var policies = await buildPoliciesAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.Policy"/>
        /// <seealso cref="PolicyDto"/>
        private static async Task<List<PolicyDto>> buildPoliciesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null) return new List<PolicyDto>();

            // Query policies for the specified product using read-only tracking
            var items = await db.Set<Label.Policy>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return empty list if no policies found
            if (items == null || !items.Any())
                return new List<PolicyDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new PolicyDto { Policy = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<PolicyDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores specialized kind information, like device product classification 
        /// or cosmetic category. Retrieves specialized kind records for a specified 
        /// product ID and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find specialized kinds for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of SpecializedKindDto objects representing the specialized kinds, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > SpecializedKindsDto
        /// Specialized kinds include information such as device product classifications or cosmetic categories.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var specializedKinds = await buildSpecializedKindsAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.SpecializedKind"/>
        /// <seealso cref="SpecializedKindDto"/>
        private static async Task<List<SpecializedKindDto>> buildSpecializedKindsAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null) return new List<SpecializedKindDto>();

            // Query specialized kinds for the specified product using read-only tracking
            var items = await db.Set<Label.SpecializedKind>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return empty list if no specialized kinds found
            if (items == null || !items.Any())
                return new List<SpecializedKindDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new SpecializedKindDto { SpecializedKind = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<SpecializedKindDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a product DTO for a specified product ID.
        /// Helper method to retrieve product entity by primary key.
        /// </summary>
        /// <param name="db">The database context for querying product entities.</param>
        /// <param name="productID">The product identifier to retrieve.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A ProductDto object with encrypted ID, or null if not found.</returns>
        /// <seealso cref="Label.Product"/>
        /// <seealso cref="ProductDto"/>
        /// <remarks>
        /// For full hierarchy, consider calling buildProductsAsync if you need full population.
        /// </remarks>
        private static async Task<ProductDto?> buildProductDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product ID is provided
            if (productID == null)
                return null;

            // Query product for the specified product ID
            var entity = await db.Set<Label.Product>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ProductID == productID);

            // Return null if no entity found
            if (entity == null)
                return null;

            // Transform entity to DTO with encrypted ID
            return new ProductDto
            {
                Product = entity.ToEntityWithEncryptedId(pkSecret, logger)
                // For full hierarchy, consider calling buildProductsAsync if you need full population.
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of GenericMedicine DTOs for the specified product.
        /// Retrieves non-proprietary medicine names associated with a product.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productID">The product ID to find generic medicines for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of GenericMedicine DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.GenericMedicine"/>
        private static async Task<List<GenericMedicineDto>> buildGenericMedicinesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productID == null) return new List<GenericMedicineDto>();

            // Query generic medicines for the specified product
            var items = await db.Set<Label.GenericMedicine>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<GenericMedicineDto>();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new GenericMedicineDto { GenericMedicine = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductIdentifier DTOs for the specified product.
        /// Retrieves various types of identifiers associated with a product such as NDC, GTIN, etc.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productID">The product ID to find product identifiers for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductIdentifier DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ProductIdentifier"/>
        private static async Task<List<ProductIdentifierDto>> buildProductIdentifiersAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productID == null) return new List<ProductIdentifierDto>();

            // Query product identifiers for the specified product
            var items = await db.Set<Label.ProductIdentifier>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ProductIdentifierDto { ProductIdentifier = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductRouteOfAdministration DTOs for the specified product.
        /// Retrieves routes of administration associated with a product or part.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productID">The product ID to find routes of administration for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductRouteOfAdministration DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ProductRouteOfAdministration"/>
        private static async Task<List<ProductRouteOfAdministrationDto>> buildProductRouteOfAdministrationsAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productID == null) return new List<ProductRouteOfAdministrationDto>();

            // Query product routes of administration for the specified product
            var items = await db.Set<Label.ProductRouteOfAdministration>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ProductRouteOfAdministrationDto { ProductRouteOfAdministration = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductWebLink DTOs for the specified product.
        /// Retrieves web page links for cosmetic products.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productID">The product ID to find web links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductWebLink DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ProductWebLink"/>
        private static async Task<List<ProductWebLinkDto>> buildProductWebLinksAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productID == null) return new List<ProductWebLinkDto>();

            // Query product web links for the specified product
            var items = await db.Set<Label.ProductWebLink>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ProductWebLinkDto { ProductWebLink = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of marketing category DTOs for a specified product ID.
        /// ProductID is used to get MarketingCategory, then fetch by MarketingCategoryID.
        /// </summary>
        /// <param name="db">The database context for querying marketing category entities.</param>
        /// <param name="productID">The product identifier to filter marketing categories.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of MarketingCategoryDto objects, or null if no product ID provided or no entities found.</returns>
        /// <seealso cref="Label.MarketingCategory"/>
        /// <seealso cref="MarketingCategoryDto"/>
        private static async Task<List<MarketingCategoryDto>> buildMarketingCategoriesDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product ID is provided
            if (productID == null)
                return new List<MarketingCategoryDto>();

            // Query marketing categories for the specified product
            var entity = await db.Set<Label.MarketingCategory>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return new List<MarketingCategoryDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new MarketingCategoryDto
            {
                MarketingCategory = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of product part DTOs for a specified product ID.
        /// ProductPart has a one-to-many relationship with Product (KitProductID == productID).
        /// </summary>
        /// <param name="db">The database context for querying product part entities.</param>
        /// <param name="productID">The product identifier used as kit product ID to filter product parts.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of ProductPartDto objects, or null if no product ID provided or no entities found.</returns>
        /// <seealso cref="Label.ProductPart"/>
        /// <seealso cref="ProductPartDto"/>
        private static async Task<List<ProductPartDto>> buildProductPartsDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product ID is provided
            if (productID == null)
                return new List<ProductPartDto>();

            // Query product parts for the specified kit product
            var entity = await db.Set<Label.ProductPart>()
                .AsNoTracking()
                .Where(e => e.KitProductID == productID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<ProductPartDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity
                .Select(e => new ProductPartDto { ProductPart = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<ProductPartDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of characteristic DTOs for a specified product ID, including associated packaging levels.
        /// Stores characteristics of a product or package (subjectOf characteristic).
        /// </summary>
        /// <param name="db">The database context for querying characteristic entities.</param>
        /// <param name="productID">The product identifier to filter characteristics.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of CharacteristicDto objects with associated packaging levels.</returns>
        /// <seealso cref="Label.Characteristic"/>
        /// <seealso cref="CharacteristicDto"/>
        /// <seealso cref="buildPackageIdentifierDtoAsync"/>
        /// <remarks>
        /// Based on Section 3.1.9. Relates to the Characteristic table (ProductID and PackagingLevelID).
        /// </remarks>
        private static async Task<List<CharacteristicDto>> buildCharacteristicsDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no product ID is provided
            if (productID == null)
                return new List<CharacteristicDto>();

            // Query characteristics for the specified product
            var entities = await db.Set<Label.Characteristic>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            var dtos = new List<CharacteristicDto>();

            // Process each characteristic and build associated packaging levels
            foreach (var item in entities)
            {
                // For each characteristic, build its PackagingLevel(s) as dictionaries
                var packagingLevels = new List<Dictionary<string, object?>>();
                if (item.PackagingLevelID != null)
                {
                    // You might have one or many packaging levels per characteristic
                    var pkgDto = await buildPackageIdentifierDtoAsync(db, item.PackagingLevelID, pkSecret, logger);
                    if (pkgDto?.PackageIdentifier != null)
                    {
                        packagingLevels.Add(pkgDto.PackageIdentifier);
                    }
                }

                // Create characteristic DTO with packaging levels
                dtos.Add(new CharacteristicDto
                {
                    Characteristic = item.ToEntityWithEncryptedId(pkSecret, logger),
                    PackagingLevels = packagingLevels
                });
            }

            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of BusinessOperationProductLink DTOs for the specified product.
        /// Retrieves links between business operations performed by establishments and specific products.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productId">The product ID to find business operation links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of BusinessOperationProductLink DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.BusinessOperationProductLink"/>
        private static async Task<List<BusinessOperationProductLinkDto>> buildBusinessOperationProductLinksAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productId == null) return new List<BusinessOperationProductLinkDto>();

            // Query business operation product links for the specified product
            var items = await db.Set<Label.BusinessOperationProductLink>()
                .AsNoTracking()
                .Where(e => e.ProductID == productId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new BusinessOperationProductLinkDto { BusinessOperationProductLink = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ResponsiblePersonLink DTOs for the specified product.
        /// Retrieves links between cosmetic products and their responsible person organizations.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productId">The product ID to find responsible person links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ResponsiblePersonLink DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ResponsiblePersonLink"/>
        private static async Task<List<ResponsiblePersonLinkDto>> buildResponsiblePersonLinksAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productId == null) return new List<ResponsiblePersonLinkDto>();

            // Query responsible person links for the specified product
            var items = await db.Set<Label.ResponsiblePersonLink>()
                .AsNoTracking()
                .Where(e => e.ProductID == productId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ResponsiblePersonLinkDto { ResponsiblePersonLink = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }
        #endregion

        #region Product Instance & Lot Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of ProductInstance DTOs for the specified product with nested lot hierarchies.
        /// Constructs product instance data including parent and child hierarchy relationships for lot distribution.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productId">The product ID to find product instances for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductInstance DTOs with nested hierarchy data.</returns>
        /// <seealso cref="Label.ProductInstance"/>
        /// <seealso cref="Label.LotHierarchy"/>
        private static async Task<List<ProductInstanceDto>> buildProductInstancesAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productId == null) return new List<ProductInstanceDto>();

            // Get all product instances for this product
            var instances = await db.Set<Label.ProductInstance>()
                .AsNoTracking()
                .Where(e => e.ProductID == productId)
                .ToListAsync();

            var dtos = new List<ProductInstanceDto>();

            // For each instance, build its lot hierarchies
            foreach (var instance in instances)
            {
                var lot = await buildLotIdentifierDtoAsync(db, instance.LotIdentifierID, pkSecret, logger);

                // Build parent and child hierarchy relationships
                var parentHierarchies = await buildLotHierarchiesAsParentAsync(db, instance.ProductInstanceID, pkSecret, logger);

                var childHierarchies = await buildLotHierarchiesAsChildAsync(db, instance.ProductInstanceID, pkSecret, logger);

                var packagingLevels = await buildPackagingLevelsDtoAsync(db, instance.ProductInstanceID, pkSecret, logger);

                // Assemble product instance DTO with hierarchy data
                dtos.Add(new ProductInstanceDto
                {
                    ProductInstance = instance.ToEntityWithEncryptedId(pkSecret, logger),
                    LotIdentifier = lot,
                    ParentHierarchies = parentHierarchies,
                    ChildHierarchies = childHierarchies,
                    PackagingLevels = packagingLevels ?? new List<PackagingLevelDto>()
                });
            }
            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a lot identifier DTO for a specified lot identifier ID.
        /// The lot identifier can be related to either the Product or the Ingredient.
        /// </summary>
        /// <param name="db">The database context for querying lot identifier entities.</param>
        /// <param name="lotIdentifierId">The lot identifier ID to retrieve.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A LotIdentifierDto object with encrypted ID, or null if not found.</returns>
        /// <seealso cref="Label.LotIdentifier"/>
        /// <seealso cref="LotIdentifierDto"/>
        /// <remarks>
        /// LotIdentifierDto can be related to either the Product or the Ingredient.
        /// </remarks>
        private static async Task<LotIdentifierDto?> buildLotIdentifierDtoAsync(ApplicationDbContext db, int? lotIdentifierId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no lot identifier ID is provided
            if (lotIdentifierId == null) return null;

            // Query lot identifier for the specified lot identifier ID
            var entity = await db.Set<Label.LotIdentifier>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.LotIdentifierID == lotIdentifierId);

            // Return null if entity not found
            if (entity == null)
                return null;

            // Transform entity to DTO with encrypted ID
            return new LotIdentifierDto
            {
                LotIdentifier = entity.ToEntityWithEncryptedId(pkSecret, logger)
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of LotHierarchy DTOs where the specified instance acts as a parent.
        /// Retrieves relationships between Fill/Package Lots and their member Label Lots.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="parentInstanceId">The parent instance ID to find child hierarchies for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of LotHierarchy DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.LotHierarchy"/>
        private static async Task<List<LotHierarchyDto>> buildLotHierarchiesAsParentAsync(ApplicationDbContext db, int? parentInstanceId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (parentInstanceId == null) return new List<LotHierarchyDto>();

            // Query lot hierarchies where this instance is the parent
            var items = await db.Set<Label.LotHierarchy>()
                .AsNoTracking()
                .Where(e => e.ParentInstanceID == parentInstanceId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new LotHierarchyDto { LotHierarchy = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of LotHierarchy DTOs where the specified instance acts as a child.
        /// Retrieves relationships where this instance is a member of other Fill/Package Lots.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="childInstanceId">The child instance ID to find parent hierarchies for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of LotHierarchy DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.LotHierarchy"/>
        private static async Task<List<LotHierarchyDto>> buildLotHierarchiesAsChildAsync(ApplicationDbContext db, int? childInstanceId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (childInstanceId == null) return new List<LotHierarchyDto>();

            // Query lot hierarchies where this instance is the child
            var items = await db.Set<Label.LotHierarchy>()
                .AsNoTracking()
                .Where(e => e.ChildInstanceID == childInstanceId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new LotHierarchyDto { LotHierarchy = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of lot hierarchy DTOs for fill/package lots based on product instance ID.
        /// Establishes the relationship between Fill/Package Lots and Label Lots where the parent is the package/fill lot.
        /// </summary>
        /// <param name="db">The database context for querying lot hierarchy entities.</param>
        /// <param name="productID">The product instance identifier used as parent instance ID.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of LotHierarchyDto objects representing fill lot hierarchies.</returns>
        /// <seealso cref="Label.LotHierarchy"/>
        /// <seealso cref="LotHierarchyDto"/>
        /// <remarks>
        /// Based on Section 16.2.7, 16.2.11. The parent is the package/fill lot and the child is the label lot.
        /// LotHierarchy has a one-to-many relationship with ProductInstance.
        /// </remarks>
        private static async Task<List<LotHierarchyDto>> buildFillLotHierarchyDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no product ID is provided
            if (productID == null) return new List<LotHierarchyDto>();

            // Query lot hierarchies for the specified product instance as parent
            var items = await db.Set<Label.LotHierarchy>()
                .AsNoTracking()
                .Where(e => e.ParentInstanceID == productID)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<LotHierarchyDto>();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new LotHierarchyDto { LotHierarchy = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<LotHierarchyDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of lot hierarchy DTOs for label lots based on product instance ID.
        /// Establishes the relationship between Fill/Package Lots and Label Lots where the child is the label lot.
        /// </summary>
        /// <param name="db">The database context for querying lot hierarchy entities.</param>
        /// <param name="productID">The product instance identifier used as child instance ID.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of LotHierarchyDto objects representing label lot hierarchies.</returns>
        /// <seealso cref="Label.LotHierarchy"/>
        /// <seealso cref="LotHierarchyDto"/>
        /// <remarks>
        /// Based on Section 16.2.7, 16.2.11. The parent is the package/fill lot and the child is the label lot.
        /// LotHierarchy has a one-to-many relationship with ProductInstance.
        /// </remarks>
        private static async Task<List<LotHierarchyDto>> buildLabelLotHierarchyDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no product ID is provided
            if (productID == null) return new List<LotHierarchyDto>();

            // Query lot hierarchies for the specified product instance as child
            var items = await db.Set<Label.LotHierarchy>()
                .AsNoTracking()
                .Where(e => e.ChildInstanceID == productID)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<LotHierarchyDto>();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new LotHierarchyDto { LotHierarchy = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<LotHierarchyDto>();
            #endregion
        }
        #endregion

        #region Ingredient & Substance Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of Ingredient DTOs for the specified product with nested ingredient substance details.
        /// Constructs ingredient hierarchies including substance information and reference data.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productId">The product ID to find ingredients for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of Ingredient DTOs with nested IngredientSubstance data.</returns>
        /// <seealso cref="Label.Ingredient"/>
        /// <seealso cref="Label.IngredientSubstance"/>
        /// <seealso cref="Label.IngredientInstance"/>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="IngredientSubstanceDto"/>
        /// <seealso cref="IngredientInstanceDto"/>"/>
        /// <remarks>
        /// This method builds complete IngredientDto objects including nested substance and instance data.
        /// Returns an empty list if productId is null or no ingredients are found.
        /// Each Ingredient includes its associated IngredientSubstance and IngredientInstance collections.
        /// </remarks>
        /// <example>
        /// <code>
        /// var ingredients = await buildIngredientsAsync(dbContext, 123, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<List<IngredientDto>> buildIngredientsAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no productId provided
            if (productId == null)
                return new List<IngredientDto>();

            // Get all ingredients for this product with no change tracking for performance
            var ingredients = await db.Set<Label.Ingredient>()
                .AsNoTracking()
                .Where(i => i.ProductID == productId)
                .ToListAsync();

            var ingredientDtos = new List<IngredientDto>();

            // For each ingredient, build its substance details and instances
            foreach (var ingredient in ingredients)
            {
                // Skip null ingredients or ingredients without valid IDs
                if (ingredient == null || ingredient.IngredientID == null)
                    continue;

                // Build the associated IngredientSubstance (using correct FK: IngredientSubstanceID)
                var ingredientSubstance = await buildIngredientSubstanceAsync(
                    db,
                    ingredient.IngredientSubstanceID,
                    pkSecret,
                    logger);

                var refSubstance = await buildReferenceSubstancesDtoAsync(
                    db,
                    ingredient.IngredientSubstanceID,
                    pkSecret,
                    logger);

                // Build all IngredientInstances for this substance
                var ingredientInstances = await buildIngredientInstancesAsync(
                    db,
                    ingredient.IngredientSubstanceID,
                    pkSecret,
                    logger
                );

                var sourceProducts = await buildIngredientSourceProductsDtoAsync(
                    db,
                    ingredient.IngredientID,
                    pkSecret,
                    logger);

                // Assemble ingredient DTO with substance data and instances
                ingredientDtos.Add(new IngredientDto
                {
                    Ingredient = ingredient.ToEntityWithEncryptedId(pkSecret, logger),
                    IngredientInstances = ingredientInstances ?? new List<IngredientInstanceDto>(),
                    IngredientSubstance = ingredientSubstance,
                    IngredientSourceProducts = sourceProducts ?? new List<IngredientSourceProductDto>(),
                    ReferenceSubstances = refSubstance ?? new List<ReferenceSubstanceDto>()
                });
            }
            return ingredientDtos;
            #endregion
        }

        /**************************************************************/
        // Stores the specified substance code and name linked to an
        // ingredient in Biologic/Drug Substance Indexing documents.
        // IngredientDto > SpecifiedSubstanceDto
        private static async Task<List<SpecifiedSubstanceDto>> buildSpecifiedSubstancesAsync(ApplicationDbContext db, int? ingredientID, string pkSecret, ILogger logger)
        { return null; }

        /**************************************************************/
        /// <summary>
        /// Builds an IngredientSubstance DTO for the specified ingredient substance ID.
        /// Retrieves detailed substance information including UNII codes and substance names.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="ingredientSubstanceId">The ingredient substance ID to retrieve.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>IngredientSubstance DTO with encrypted ID, or null if not found.</returns>
        /// <seealso cref="Label.IngredientSubstance"/>
        /// <seealso cref="IngredientSubstanceDto"/>
        /// <remarks>
        /// This method retrieves and transforms a single IngredientSubstance entity into a DTO.
        /// Returns null if ingredientSubstanceId is null or the entity is not found in the database.
        /// The returned DTO includes encrypted primary key for security.
        /// </remarks>
        /// <example>
        /// <code>
        /// var substance = await buildIngredientSubstanceAsync(dbContext, 789, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<IngredientSubstanceDto?> buildIngredientSubstanceAsync(ApplicationDbContext db, int? ingredientSubstanceId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredientSubstanceId provided
            if (ingredientSubstanceId == null)
                return null;

            // Query for the specific ingredient substance with no change tracking
            var entity = await db.Set<Label.IngredientSubstance>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.IngredientSubstanceID == ingredientSubstanceId);

            // Return null if entity not found
            if (entity == null)
                return null;

            var substanceInstances = await buildIngredientInstancesDtoAsync(
                db,
                entity.IngredientSubstanceID,
                pkSecret,
                logger);

            // Transform entity to DTO with encrypted ID
            return new IngredientSubstanceDto
            {
                IngredientSubstance = entity.ToEntityWithEncryptedId(pkSecret, logger),
                IngredientInstances = substanceInstances ?? new List<IngredientInstanceDto>()
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of IngredientInstanceDto for a given IngredientSubstanceID.
        /// Each includes its IngredientSubstance (as IngredientSubstanceDto), if available.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="ingredientSubstanceId">The ingredient substance ID to find instances for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of IngredientInstanceDto with nested IngredientSubstanceDto where available, or null if not found.</returns>
        /// <seealso cref="Label.IngredientInstance"/>
        /// <seealso cref="IngredientInstanceDto"/>
        /// <seealso cref="IngredientSubstanceDto"/>
        /// <remarks>
        /// This method builds complete IngredientInstanceDto objects including nested IngredientSubstance data.
        /// Returns null if ingredientSubstanceId is null or no ingredient instances are found.
        /// Each IngredientInstance that has an associated IngredientSubstanceID will include the full IngredientSubstanceDto.
        /// The method processes all instances associated with the specified substance ID.
        /// </remarks>
        /// <example>
        /// <code>
        /// var instances = await buildIngredientInstancesAsync(dbContext, 456, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<List<IngredientInstanceDto>?> buildIngredientInstancesAsync(ApplicationDbContext db, int? ingredientSubstanceId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredientSubstanceId provided
            if (ingredientSubstanceId == null)
                return null;

            // Query all IngredientInstance rows for this substance with no change tracking
            var entity = await db.Set<Label.IngredientInstance>()
                .AsNoTracking()
                .Where(ii => ii.IngredientSubstanceID == ingredientSubstanceId)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Build IngredientInstanceDto objects with substance data
            List<IngredientInstanceDto> ingredientInstances = new List<IngredientInstanceDto>();

            foreach (var item in entity)
            {
                // Build the IngredientSubstanceDto for this instance (recursive call for substance details)
                var ingredientSubstance = await buildIngredientSubstanceAsync(
                    db,
                    item.IngredientSubstanceID,
                    pkSecret,
                    logger);

                // Create the IngredientInstanceDto with encrypted IDs
                ingredientInstances.Add(new IngredientInstanceDto
                {
                    IngredientInstance = item.ToEntityWithEncryptedId(pkSecret, logger),
                });
            }

            return ingredientInstances;
            #endregion
        }

        /**************************************************************/
        private static async Task<List<IngredientInstanceDto>> buildProductIngredientInstancesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredientSubstanceId provided
            if (productID == null)
                return new List<IngredientInstanceDto>();

            // Query all IngredientInstance rows for this substance with no change tracking
            var entity = await db.Set<Label.IngredientInstance>()
                .AsNoTracking()
                .Where(ii => ii.FillLotInstanceID == productID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<IngredientInstanceDto>();

            // Build IngredientInstanceDto objects with substance data
            List<IngredientInstanceDto> ingredientInstances = new List<IngredientInstanceDto>();

            foreach (var item in entity)
            {
                // Build the IngredientSubstanceDto for this instance (recursive call for substance details)
                var ingredientSubstance = await buildIngredientSubstanceAsync(
                    db,
                    item.IngredientSubstanceID,
                    pkSecret,
                    logger);

                // Create the IngredientInstanceDto with encrypted IDs
                ingredientInstances.Add(new IngredientInstanceDto
                {
                    IngredientInstance = item.ToEntityWithEncryptedId(pkSecret, logger),
                    LotIdentifier = await buildLotIdentifierDtoAsync(db, item.LotIdentifierID, pkSecret, logger)
                });
            }

            return ingredientInstances ?? new List<IngredientInstanceDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ingredient instance DTOs for a specified ingredient substance ID.
        /// </summary>
        /// <param name="db">The database context for querying ingredient instance entities.</param>
        /// <param name="ingredientSubstanceID">The ingredient substance identifier to filter ingredient instances.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of IngredientInstanceDto objects, or null if no ingredient substance ID provided or no entities found.</returns>
        /// <seealso cref="Label.IngredientInstance"/>
        /// <seealso cref="IngredientInstanceDto"/>
        private static async Task<List<IngredientInstanceDto>?> buildIngredientInstancesDtoAsync(ApplicationDbContext db, int? ingredientSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredient substance ID is provided
            if (ingredientSubstanceID == null)
                return null;

            // Query ingredient instances for the specified ingredient substance
            var entity = await db.Set<Label.IngredientInstance>()
                .AsNoTracking()
                .Where(e => e.IngredientSubstanceID == ingredientSubstanceID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new IngredientInstanceDto
            {
                IngredientInstance = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ingredient source product DTOs for a specified ingredient ID.
        /// </summary>
        /// <param name="db">The database context for querying ingredient source product entities.</param>
        /// <param name="ingredientID">The ingredient identifier to filter ingredient source products.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of IngredientSourceProductDto objects, or null if no ingredient ID provided or no entities found.</returns>
        /// <seealso cref="Label.IngredientSourceProduct"/>
        /// <seealso cref="IngredientSourceProductDto"/>
        private static async Task<List<IngredientSourceProductDto>?> buildIngredientSourceProductsDtoAsync(ApplicationDbContext db, int? ingredientID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredient ID is provided
            if (ingredientID == null)
                return null;

            // Query ingredient source products for the specified ingredient
            var entity = await db.Set<Label.IngredientSourceProduct>()
                .AsNoTracking()
                .Where(e => e.IngredientID == ingredientID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new IngredientSourceProductDto
            {
                IngredientSourceProduct = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of reference substance DTOs for a specified ingredient substance ID.
        /// The reference substance is related to Ingredient via IngredientSubstanceID.
        /// </summary>
        /// <param name="db">The database context for querying reference substance entities.</param>
        /// <param name="ingredientSubstanceID">The ingredient substance identifier to filter reference substances.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of ReferenceSubstanceDto objects, or null if no ingredient substance ID provided or no entities found.</returns>
        /// <seealso cref="Label.ReferenceSubstance"/>
        /// <seealso cref="ReferenceSubstanceDto"/>
        private static async Task<List<ReferenceSubstanceDto>?> buildReferenceSubstancesDtoAsync(ApplicationDbContext db, int? ingredientSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredient substance ID is provided
            if (ingredientSubstanceID == null)
                return null;

            // Query reference substances for the specified ingredient substance
            var entity = await db.Set<Label.ReferenceSubstance>()
                .AsNoTracking()
                .Where(e => e.IngredientSubstanceID == ingredientSubstanceID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs, filtering out null entities
            return entity.Where(e => e != null).Select(e => new ReferenceSubstanceDto
            {
                ReferenceSubstance = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of IdentifiedSubstance DTOs for the specified section.
        /// Retrieves substance details such as active moieties and pharmacologic class identifiers used in indexing contexts.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find identified substances for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of IdentifiedSubstance DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.IdentifiedSubstance"/>
        /// <seealso cref="Label.SubstanceSpecification"/>
        /// <seealso cref="IdentifiedSubstanceDto"/>
        /// <seealso cref="SubstanceSpecificationDto"/>
        private static async Task<List<IdentifiedSubstanceDto>> buildIdentifiedSubstancesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<IdentifiedSubstanceDto>();

            // Query identified substances for the specified section
            var items = await db.Set<Label.IdentifiedSubstance>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // For each identified substance, build its substance specifications
            // Transform entities to DTOs with encrypted IDs
            var dtos = new List<IdentifiedSubstanceDto>();
            foreach (var item in items)
            {
                // For each IdentifiedSubstance, build SubstanceSpecifications
                var specs = await buildSubstanceSpecificationsAsync(
                    db,
                    item.IdentifiedSubstanceID,
                    pkSecret,
                    logger);

                dtos.Add(new IdentifiedSubstanceDto
                {
                    IdentifiedSubstance = item.ToEntityWithEncryptedId(pkSecret, logger),
                    SubstanceSpecifications = specs
                });
            }

            return dtos;
            #endregion
        }

        /**************************************************************/
        // Builds a list of contributing factors for the specified factor
        // substance ID. FactorSubstanceID == IdentifiedSubstanceID
        // IdentifiedSubstanceDto > ContributingFactorDto
        private static async Task<List<ContributingFactorDto>> buildContributingFactorsAsync(ApplicationDbContext db, int? factorSubstanceID, string pkSecret, ILogger logger)
        { return null; }

        /**************************************************************/
        // Builds a list of pharmacologic classes for the IdentifiedSubstance
        // IdentifiedSubstanceDto > PharmacologicClassDto
        private static async Task<List<PharmacologicClassDto>> buildPharmacologicClassesAsync(ApplicationDbContext db, int? identifiedSubstanceID, string pkSecret, ILogger logger)
        { return null; }

        /**************************************************************/
        // Builds a list of pharmacologic classes names for the pharmacologic classes
        // IdentifiedSubstanceDto > PharmacologicClassDto > PharmacologicClassNameDto
        private static async Task<List<PharmacologicClassNameDto>> buildPharmacologicClassNamesAsync(ApplicationDbContext db, int? pharmacologicClassID, string pkSecret, ILogger logger)
        { return null; }

        /**************************************************************/
        // Builds a list of pharmacologic classes links for the pharmacologic classes
        // IdentifiedSubstanceDto > PharmacologicClassDto > PharmacologicClassLinkDto
        private static async Task<List<PharmacologicClassLinkDto>> buildPharmacologicClassLinksAsync(ApplicationDbContext db, int? pharmacologicClassID, string pkSecret, ILogger logger)
        { return null; }

        /**************************************************************/
        // Builds a list of pharmacologic class hierarchies for the pharmacologic classes
        // ChildPharmacologicClassID == pharmacologicClassID
        // IdentifiedSubstanceDto > PharmacologicClassDto > PharmacologicClassLinkDto
        private static async Task<List<PharmacologicClassHierarchyDto>> buildPharmacologicClassHierarchiesAsync(ApplicationDbContext db, int? pharmacologicClassID, string pkSecret, ILogger logger)
        { return null; }

        /**************************************************************/
        // Builds a list of consequences for contributing factors.
        // IdentifiedSubstanceDto > ContributingFactorDto > InteractionConsequenceDto
        private static async Task<List<ContributingFactorDto>> buildContributingFactornteractionConsequencesAsync(ApplicationDbContext db, int? interactionIssueID, string pkSecret, ILogger logger)
        { return null; }

        /**************************************************************/
        /// <summary>
        /// Builds a list of SubstanceSpecification DTOs for the specified IdentifiedSubstance.
        /// Retrieves detailed substance specifications including analyte information and chemical properties.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="identifiedSubstanceID">The IdentifiedSubstance ID to find specifications for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of SubstanceSpecification DTOs with nested Analyte data and encrypted IDs.</returns>
        /// <seealso cref="Label.SubstanceSpecification"/>
        /// <seealso cref="Label.Analyte"/>
        /// <seealso cref="SubstanceSpecificationDto"/>
        /// <seealso cref="AnalyteDto"/>
        /// <remarks>
        /// This method builds complete SubstanceSpecificationDto objects including nested Analyte data.
        /// Returns an empty list if identifiedSubstanceID is null or no specifications are found.
        /// Each SubstanceSpecification includes its associated Analytes collection with encrypted IDs.
        /// </remarks>
        /// <example>
        /// <code>
        /// var specs = await buildSubstanceSpecificationsAsync(dbContext, 456, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<List<SubstanceSpecificationDto>> buildSubstanceSpecificationsAsync(ApplicationDbContext db, int? identifiedSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no identifiedSubstanceID provided
            if (identifiedSubstanceID == null) return new List<SubstanceSpecificationDto>();

            // Query substance specifications for the specified IdentifiedSubstance with no change tracking
            var items = await db.Set<Label.SubstanceSpecification>()
                .AsNoTracking()
                .Where(e => e.IdentifiedSubstanceID == identifiedSubstanceID)
                .ToListAsync();

            var dtos = new List<SubstanceSpecificationDto>();

            // For each substance specification, build its analytes
            foreach (var item in items)
            {
                // For each SubstanceSpecification, build associated Analytes
                var analytes = await buildAnalytesAsync(
                    db,
                    item.SubstanceSpecificationID,
                    pkSecret,
                    logger);

                // Create SubstanceSpecificationDto with encrypted IDs and nested analytes
                dtos.Add(new SubstanceSpecificationDto
                {
                    SubstanceSpecification = item.ToEntityWithEncryptedId(pkSecret, logger),
                    Analytes = analytes
                });
            }

            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of Analyte DTOs for the specified SubstanceSpecification.
        /// This is a junction between SubstanceSpecification and its IdentifiedSubstances.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="substanceSpecificationID">The SubstanceSpecification ID to find analytes for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of Analyte DTOs with encrypted IDs, empty list if none found.</returns>
        /// <seealso cref="Label.Analyte"/>
        /// <seealso cref="AnalyteDto"/>
        /// <remarks>
        /// This method builds Analyte DTOs with encrypted primary keys for security.
        /// Returns an empty list if substanceSpecificationID is null or no analytes are found.
        /// Uses LINQ Select for efficient transformation of entities to DTOs.
        /// </remarks>
        /// <example>
        /// <code>
        /// var analytes = await buildAnalytesAsync(dbContext, 789, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<List<AnalyteDto>> buildAnalytesAsync(ApplicationDbContext db, int? substanceSpecificationID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no substanceSpecificationID provided
            if (substanceSpecificationID == null) return new List<AnalyteDto>();

            // Query analytes for the specified SubstanceSpecification with no change tracking
            var items = await db.Set<Label.Analyte>()
                .AsNoTracking()
                .Where(e => e.AnalyteSubstanceID == substanceSpecificationID)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs using LINQ Select for efficiency
            return items
                .Select(item => new AnalyteDto { Analyte = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        // Builds a list of ObservationCriterion DTOs for the specified SubstanceSpecification.
        // SubstanceSpecificationDto > ObservationCriterionDto
        private static async Task<List<ObservationCriterionDto>> buildObservationCriterionAsync(ApplicationDbContext db, int? substanceSpecificationID, string pkSecret, ILogger logger) { return null; }

        /**************************************************************/
        // Get the application type ids from the ObservationCriterion.
        // SubstanceSpecificationDto > ObservationCriterionDto > ApplicationTypeDto
        private static async Task<List<ApplicationTypeDto>> buildApplicationTypesAsync(ApplicationDbContext db, int? applicationTypeID, string pkSecret, ILogger logger) { return null; }

        /**************************************************************/
        // Gets the commodities for the specified commodity ID.
        // SubstanceSpecificationDto ObservationCriterionDto > CommodityDto
        private static async Task<List<CommodityDto>> buildCommoditiesAsync(ApplicationDbContext db, int? commodityId, string pkSecret, ILogger logger) { return null; }

        #endregion

        #region Packaging Hierarchy Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of packaging level DTOs for a specified product, including packaging hierarchy data.
        /// </summary>
        /// <param name="db">The database context for querying packaging level entities.</param>
        /// <param name="productID">The product identifier to filter packaging levels.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of PackagingLevelDto objects with encrypted IDs and packaging hierarchy data.</returns>
        /// <seealso cref="Label.PackagingLevel"/>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="buildPackagingHierarchyDtoAsync"/>
        /// <example>
        /// var packagingLevels = await buildPackagingLevelsAsync(dbContext, 123, "secretKey", logger);
        /// </example>
        private static async Task<List<PackagingLevelDto>> buildPackagingLevelsAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no product ID is provided
            if (productID == null) return new List<PackagingLevelDto>();

            List<PackagingHierarchyDto> packagingHierarchyDtos = new List<PackagingHierarchyDto>();

            // Query packaging levels for the specified packaging hierarchy
            var items = await db.Set<Label.PackagingLevel>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Build packaging hierarchy data for each packaging level
            foreach (var item in items)
            {
                var pack = await buildPackagingHierarchyDtoAsync(db, item.PackagingLevelID, pkSecret, logger);

                // Add packaging hierarchy data if available
                if (pack != null && pack.Any())
                    foreach (var p in pack)
                        if (p.PackagingHierarchy != null)
                            packagingHierarchyDtos.Add(p);
            }

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new PackagingLevelDto
                {
                    PackagingLevel = item.ToEntityWithEncryptedId(pkSecret, logger),
                    PackagingHierarchy = packagingHierarchyDtos
                })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of packaging level DTOs for a specified product instance ID.
        /// Helper method for retrieving packaging levels associated with product instances.
        /// </summary>
        /// <param name="db">The database context for querying packaging level entities.</param>
        /// <param name="productInstanceID">The product instance identifier to filter packaging levels.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of PackagingLevelDto objects, or null if no product instance ID provided or no entities found.</returns>
        /// <seealso cref="Label.PackagingLevel"/>
        /// <seealso cref="PackagingLevelDto"/>
        private static async Task<List<PackagingLevelDto>?> buildPackagingLevelsDtoAsync(ApplicationDbContext db, int? productInstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product instance ID is provided
            if (productInstanceID == null)
                return null;

            // Query packaging levels for the specified product instance
            var entity = await db.Set<Label.PackagingLevel>()
                .AsNoTracking()
                .Where(e => e.ProductInstanceID == productInstanceID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new PackagingLevelDto
            {
                PackagingLevel = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of packaging hierarchy DTOs for a specified outer packaging level ID.
        /// Contains both OuterPackagingLevelID and InnerPackagingLevelID relationships.
        /// </summary>
        /// <param name="db">The database context for querying packaging hierarchy entities.</param>
        /// <param name="outerPackagingLevelID">The outer packaging level identifier to filter packaging hierarchies.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of PackagingHierarchyDto objects, or null if no outer packaging level ID provided or no entities found.</returns>
        /// <seealso cref="Label.PackagingHierarchy"/>
        /// <seealso cref="PackagingHierarchyDto"/>
        private static async Task<List<PackagingHierarchyDto>?> buildPackagingHierarchyDtoAsync(ApplicationDbContext db, int? outerPackagingLevelID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no outer packaging level ID is provided
            if (outerPackagingLevelID == null)
                return null;

            // Query packaging hierarchies for the specified outer packaging level
            var entity = await db.Set<Label.PackagingHierarchy>()
                .AsNoTracking()
                .Where(e => e.OuterPackagingLevelID == outerPackagingLevelID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new PackagingHierarchyDto
            {
                PackagingHierarchy = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }

        /**************************************************************/
        // Stores product events like distribution or return quantities
        // PackagingLevelDto > ProductEventDto
        private static async Task<List<ProductEventDto>?> buildProductEventsAsync(ApplicationDbContext db, int? packagingLevelID, string pkSecret, ILogger logger)
        {
            return null;
        }

        /**************************************************************/
        /// <summary>
        /// Builds a package identifier DTO for a specified packaging level ID.
        /// </summary>
        /// <param name="db">The database context for querying package identifier entities.</param>
        /// <param name="packagingLevelID">The packaging level identifier to retrieve package identifier for.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A PackageIdentifierDto object with encrypted ID, or null if not found.</returns>
        /// <seealso cref="Label.PackageIdentifier"/>
        /// <seealso cref="PackageIdentifierDto"/>
        private static async Task<PackageIdentifierDto?> buildPackageIdentifierDtoAsync(ApplicationDbContext db, int? packagingLevelID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no packaging level ID is provided
            if (packagingLevelID == null)
                return null;

            // Query package identifier for the specified packaging level
            var entity = await db.Set<Label.PackageIdentifier>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.PackagingLevelID == packagingLevelID);

            // Return null if no entity found
            if (entity == null)
                return null;

            // Transform entity to DTO with encrypted ID
            return new PackageIdentifierDto
            {
                PackageIdentifier = entity.ToEntityWithEncryptedId(pkSecret, logger)
                // Add children as needed
            };
            #endregion
        }
        #endregion

        #region Organization & Contact Builders
        /**************************************************************/
        /// <summary>
        /// Builds an OrganizationDto for a given OrganizationID,
        /// including nested ContactParties, Telecoms, Identifiers, etc.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="organizationId">The OrganizationID to build the DTO for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>OrganizationDto with all child collections, or null if not found.</returns>
        /// <seealso cref="Label.Organization"/>
        /// <seealso cref="Label.ContactParty"/>
        /// <seealso cref="OrganizationDto"/>
        /// <seealso cref="ContactPartyDto"/>
        /// <remarks>
        /// This method builds a complete OrganizationDto hierarchy including all related entities.
        /// Returns null if the organization is not found in the database.
        /// </remarks>
        /// <example>
        /// <code>
        /// var orgDto = await buildOrganizationAsync(dbContext, 123, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<OrganizationDto?> buildOrganizationAsync(
            ApplicationDbContext db,
            int? organizationId,
            string pkSecret,
            ILogger logger)
        {
            #region implementation
            // Return null immediately if organizationId is not provided
            if (organizationId == null)
                return null;

            // Fetch the Organization entity with no change tracking for performance
            var org = await db.Set<Label.Organization>()
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrganizationID == organizationId);

            // Return null if organization not found
            if (org == null)
                return null;

            // Convert organization entity to dictionary with encrypted ID
            var orgDict = org.ToEntityWithEncryptedId(pkSecret, logger);

            // Build ContactParties for this organization
            var contactParties = await db.Set<Label.ContactParty>()
                .AsNoTracking()
                .Where(cp => cp.OrganizationID == organizationId)
                .ToListAsync();

            var names = await buildNamedEntityDtoAsync(db, organizationId, pkSecret, logger)
                ?? new List<NamedEntityDto>();

            // Convert each ContactParty to DTO
            var contactPartyDtos = new List<ContactPartyDto>();
            foreach (var cp in contactParties)
            {
                var cpDto = await buildContactPartyAsync(db, cp.ContactPartyID, pkSecret, logger);
                if (cpDto != null)
                    contactPartyDtos.Add(cpDto);
            }

            var govAuthorities = await buildTerritorialAuthoritiesDtoAsync(db, organizationId, pkSecret, logger)
                ?? new List<TerritorialAuthorityDto>();

            // Build Telecoms, Identifiers, etc.
            var telecoms = await buildOrganizationTelecomsAsync(db, organizationId, pkSecret, logger);

            var identifiers = await buildOrganizationIdentifiersAsync(db, organizationId, pkSecret, logger);

            var holders = await buildHoldersDtoAsync(db, organizationId, pkSecret, logger)
                ?? new List<HolderDto>();

            // Assemble OrganizationDto with all child collections
            return new OrganizationDto
            {
                Organization = orgDict,
                ContactParties = contactPartyDtos,
                Telecoms = telecoms,
                Identifiers = identifiers,
                NamedEntities = names,
                Holders = holders,
                GoverningAuthorities = govAuthorities

                // TODO: Continue with org dependencies
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a ContactPartyDto for a given ContactPartyID.
        /// Recursively constructs Organization, Address, ContactPerson, and Telecoms.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="contactPartyId">ContactPartyID to build the DTO for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>ContactPartyDto with child hierarchy, or null if not found.</returns>
        /// <remarks>
        /// Builds all nested objects in the ContactPartyDto graph.
        /// This method recursively builds related entities to create a complete object hierarchy.
        /// </remarks>
        /// <seealso cref="Label.ContactParty"/>
        /// <seealso cref="ContactPartyDto"/>
        /// <seealso cref="OrganizationDto"/>
        /// <seealso cref="AddressDto"/>
        /// <example>
        /// <code>
        /// var contactPartyDto = await buildContactPartyAsync(dbContext, 456, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<ContactPartyDto?> buildContactPartyAsync(
            ApplicationDbContext db,
            int? contactPartyId,
            string pkSecret,
            ILogger logger)
        {
            #region implementation
            // Return null if no ContactPartyID provided
            if (contactPartyId == null)
                return null;

            // 1. Fetch ContactParty entity with no change tracking
            var contactParty = await db.Set<Label.ContactParty>()
                .AsNoTracking()
                .FirstOrDefaultAsync(cp => cp.ContactPartyID == contactPartyId);

            // Return null if ContactParty not found
            if (contactParty == null)
                return null;

            // 2. Convert to dictionary with encrypted ID
            var cpDict = contactParty.ToEntityWithEncryptedId(pkSecret, logger);

            // 3. Build related OrganizationDto (if present)
            OrganizationDto? orgDto = null;
            if (contactParty.OrganizationID != null)
                orgDto = await buildOrganizationAsync(db, contactParty.OrganizationID, pkSecret, logger);

            // 4. Build related AddressDto (if present)
            AddressDto? addressDto = null;
            if (contactParty.AddressID != null)
                addressDto = await buildAddressAsync(db, contactParty.AddressID, pkSecret, logger);

            // 5. Build related ContactPersonDto (if present)
            ContactPersonDto? contactPersonDto = null;
            if (contactParty.ContactPersonID != null)
                contactPersonDto = await buildContactPersonAsync(db, contactParty.ContactPersonID, pkSecret, logger);

            // 6. Build Telecoms (list)
            var telecoms = await buildContactPartyTelecomsAsync(db, contactParty.ContactPartyID, pkSecret, logger);

            // 7. Assemble and return ContactPartyDto with all related entities
            return new ContactPartyDto
            {
                ContactParty = cpDict,
                Organization = orgDto,
                Address = addressDto,
                ContactPerson = contactPersonDto,
                Telecoms = telecoms
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a ContactPersonDto for a given ContactPersonID,
        /// including all ContactParties where this person is referenced.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="contactPersonId">The ContactPersonID to build the DTO for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>ContactPersonDto with associated ContactParties, or null if not found.</returns>
        /// <seealso cref="Label.ContactPerson"/>
        /// <seealso cref="Label.ContactParty"/>
        /// <seealso cref="ContactPersonDto"/>
        /// <seealso cref="ContactPartyDto"/>
        /// <remarks>
        /// This method builds a ContactPersonDto with all ContactParties that reference this person.
        /// </remarks>
        /// <example>
        /// <code>
        /// var personDto = await buildContactPersonAsync(dbContext, 789, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<ContactPersonDto?> buildContactPersonAsync(
            ApplicationDbContext db,
            int? contactPersonId,
            string pkSecret,
            ILogger logger)
        {
            #region implementation
            // Return null if no ContactPersonID provided
            if (contactPersonId == null)
                return null;

            // Fetch ContactPerson entity with no change tracking
            var entity = await db.Set<Label.ContactPerson>()
                .AsNoTracking()
                .FirstOrDefaultAsync(cp => cp.ContactPersonID == contactPersonId);

            // Return null if ContactPerson not found
            if (entity == null)
                return null;

            // Convert ContactPerson entity to dictionary with encrypted ID
            var contactPersonDict = entity.ToEntityWithEncryptedId(pkSecret, logger);

            // Build ContactParties that reference this ContactPerson
            var parties = await db.Set<Label.ContactParty>()
                .AsNoTracking()
                .Where(cp => cp.ContactPersonID == contactPersonId)
                .ToListAsync();

            // Convert each ContactParty to DTO
            var partyDtos = new List<ContactPartyDto>();
            foreach (var cp in parties)
            {
                var dto = await buildContactPartyAsync(db, cp.ContactPartyID, pkSecret, logger);
                if (dto != null)
                    partyDtos.Add(dto);
            }

            // Assemble and return ContactPersonDto with all associated ContactParties
            return new ContactPersonDto
            {
                ContactPerson = contactPersonDict,
                ContactParties = partyDtos
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds an AddressDto for the specified AddressID with full child graph.
        /// Recursively constructs AddressDto with nested ContactPartyDtos and their children.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="addressId">The AddressID to build the DTO for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>AddressDto with full ContactParty hierarchy, or null if not found.</returns>
        /// <remarks>
        /// This method ensures all IDs are encrypted. Returns null if Address not found.
        /// Builds complete ContactParty hierarchy including Organization, ContactPerson, and Telecoms.
        /// </remarks>
        /// <seealso cref="Label.Address"/>
        /// <seealso cref="AddressDto"/>
        /// <seealso cref="ContactPartyDto"/>
        /// <example>
        /// <code>
        /// var addressDto = await buildAddressAsync(dbContext, 101, "secret", logger);
        /// </code>
        /// </example>
        private static async Task<AddressDto?> buildAddressAsync(
            ApplicationDbContext db,
            int? addressId,
            string pkSecret,
            ILogger logger)
        {
            #region implementation
            // Return null if no AddressID provided
            if (addressId == null)
                return null;

            // 1. Fetch Address entity with no change tracking for performance
            var address = await db.Set<Label.Address>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AddressID == addressId);

            // Return null if Address not found
            if (address == null)
                return null;

            // 2. Convert Address entity to dictionary with encrypted PK
            var addressDict = address.ToEntityWithEncryptedId(pkSecret, logger);

            // 3. Fetch and build all ContactParties associated with this address
            var contactParties = await db.Set<Label.ContactParty>()
                .AsNoTracking()
                .Where(cp => cp.AddressID == addressId)
                .ToListAsync();

            // Convert each ContactParty to DTO with full hierarchy
            var contactPartyDtos = new List<ContactPartyDto>();
            foreach (var cp in contactParties)
            {
                // Build the ContactPartyDto recursively, including Organization, Address, ContactPerson, Telecoms
                var contactPartyDto = await buildContactPartyAsync(db, cp.ContactPartyID, pkSecret, logger);
                if (contactPartyDto != null)
                    contactPartyDtos.Add(contactPartyDto);
            }

            // 4. Assemble and return AddressDto with all ContactParty relationships
            return new AddressDto
            {
                Address = addressDict,
                ContactParties = contactPartyDtos
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a telecom DTO for a specified telecom ID, including associated contact party links.
        /// </summary>
        /// <param name="db">The database context for querying telecom entities.</param>
        /// <param name="telecomId">The telecom identifier to retrieve.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A TelecomDto object with encrypted ID and contact party links, or null if not found.</returns>
        /// <seealso cref="Label.Telecom"/>
        /// <seealso cref="Label.ContactPartyTelecom"/>
        /// <seealso cref="TelecomDto"/>
        private static async Task<TelecomDto?> buildTelecomDtoAsync(ApplicationDbContext db, int? telecomId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no telecom ID is provided
            if (telecomId == null)
                return null;

            // Query telecom entity by primary key
            var entity = await db.Set<Label.Telecom>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.TelecomID == telecomId);

            // Query associated contact party telecom relationships
            var party = await db.Set<Label.ContactPartyTelecom>()
                .AsNoTracking()
                .Where(e => e.TelecomID == telecomId)
                .ToListAsync();

            // Return null if no telecom entity found
            if (entity == null)
                return null;

            // Transform entity and relationships to DTO with encrypted IDs
            return new TelecomDto
            {
                Telecom = entity.ToEntityWithEncryptedId(pkSecret, logger),
                ContactPartyLinks = party.Select(p => p.ToEntityWithEncryptedId(pkSecret, logger)).ToList()
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ContactPartyTelecomDto for a given ContactPartyID.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="contactPartyId">The ContactPartyID to build telecoms for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ContactPartyTelecomDto objects, empty list if none found.</returns>
        /// <seealso cref="Label.ContactPartyTelecom"/>
        /// <seealso cref="ContactPartyTelecomDto"/>
        /// <remarks>
        /// Returns an empty list if contactPartyId is null or no telecoms are found.
        /// </remarks>
        private static async Task<List<ContactPartyTelecomDto>> buildContactPartyTelecomsAsync(
            ApplicationDbContext db,
            int? contactPartyId,
            string pkSecret,
            ILogger logger)
        {
            #region implementation
            // Return empty list if no ContactPartyID provided
            if (contactPartyId == null)
                return new List<ContactPartyTelecomDto>();

            // Fetch all ContactPartyTelecom entities for this ContactParty
            var items = await db.Set<Label.ContactPartyTelecom>()
                .AsNoTracking()
                .Where(t => t.ContactPartyID == contactPartyId)
                .ToListAsync();

            // Convert each entity to DTO with encrypted ID
            var dtos = new List<ContactPartyTelecomDto>();
            foreach (var item in items)
            {
                var telecom = await buildTelecomDtoAsync(db, item.TelecomID, pkSecret, logger);

                var dto = new ContactPartyTelecomDto
                {
                    ContactPartyTelecom = item.ToEntityWithEncryptedId(pkSecret, logger),
                    Telecom = telecom
                    // Optionally add child DTOs if desired
                };
                dtos.Add(dto);
            }
            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of OrganizationTelecomDto for a given OrganizationID.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="organizationId">The OrganizationID to build telecoms for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of OrganizationTelecomDto objects, empty list if none found.</returns>
        /// <seealso cref="Label.OrganizationTelecom"/>
        /// <seealso cref="OrganizationTelecomDto"/>
        /// <remarks>
        /// Returns an empty list if organizationId is null or no telecoms are found.
        /// </remarks>
        private static async Task<List<OrganizationTelecomDto>> buildOrganizationTelecomsAsync(
            ApplicationDbContext db,
            int? organizationId,
            string pkSecret,
            ILogger logger)
        {
            #region implementation
            // Return empty list if no OrganizationID provided
            if (organizationId == null)
                return new List<OrganizationTelecomDto>();

            // Fetch all OrganizationTelecom entities for this Organization
            var items = await db.Set<Label.OrganizationTelecom>()
                .AsNoTracking()
                .Where(t => t.OrganizationID == organizationId)
                .ToListAsync();

            // Convert each entity to DTO with encrypted ID
            var dtos = new List<OrganizationTelecomDto>();
            foreach (var item in items)
            {
                var dto = new OrganizationTelecomDto
                {
                    OrganizationTelecom = item.ToEntityWithEncryptedId(pkSecret, logger)
                    // Optionally add child DTOs if needed
                };
                dtos.Add(dto);
            }
            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of OrganizationIdentifierDto for a given OrganizationID.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="organizationId">The OrganizationID to build identifiers for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of OrganizationIdentifierDto objects, empty list if none found.</returns>
        /// <seealso cref="Label.OrganizationIdentifier"/>
        /// <seealso cref="OrganizationIdentifierDto"/>
        /// <remarks>
        /// Returns an empty list if organizationId is null or no identifiers are found.
        /// Uses LINQ Select for more concise conversion compared to foreach loop.
        /// </remarks>
        private static async Task<List<OrganizationIdentifierDto>> buildOrganizationIdentifiersAsync(
            ApplicationDbContext db,
            int? organizationId,
            string pkSecret,
            ILogger logger)
        {
            #region implementation
            // Return empty list if no OrganizationID provided
            if (organizationId == null)
                return new List<OrganizationIdentifierDto>();

            // Fetch all OrganizationIdentifier entities for this Organization
            var items = await db.Set<Label.OrganizationIdentifier>()
                .AsNoTracking()
                .Where(i => i.OrganizationID == organizationId)
                .ToListAsync();

            // Convert each entity to DTO with encrypted ID using LINQ Select
            return items
                .Select(item => new OrganizationIdentifierDto { OrganizationIdentifier = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of named entity DTOs for a specified organization ID.
        /// </summary>
        /// <param name="db">The database context for querying named entity entities.</param>
        /// <param name="organizationID">The organization identifier to filter named entities.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of NamedEntityDto objects, or null if organization ID is not provided or no entities found.</returns>
        /// <seealso cref="Label.NamedEntity"/>
        /// <seealso cref="NamedEntityDto"/>
        private static async Task<List<NamedEntityDto>?> buildNamedEntityDtoAsync(ApplicationDbContext db, int? organizationID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no organization ID is provided
            if (organizationID == null)
                return null;

            // Query named entities for the specified organization
            var entity = await db.Set<Label.NamedEntity>()
                .AsNoTracking()
                .Where(e => e.OrganizationID == organizationID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity
                .Select(e => new NamedEntityDto
                {
                    NamedEntity = entity.ToEntityWithEncryptedId(pkSecret, logger)
                })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of territorial authority DTOs for a specified organization ID.
        /// Related by OrganizationID through GoverningAgencyOrgID (GoverningAgencyOrgID == OrganizationID).
        /// </summary>
        /// <param name="db">The database context for querying territorial authority entities.</param>
        /// <param name="organizationID">The organization identifier used as governing agency organization ID to filter territorial authorities.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of TerritorialAuthorityDto objects, or null if no organization ID provided or no entities found.</returns>
        /// <seealso cref="Label.TerritorialAuthority"/>
        /// <seealso cref="TerritorialAuthorityDto"/>
        private static async Task<List<TerritorialAuthorityDto>?> buildTerritorialAuthoritiesDtoAsync(ApplicationDbContext db, int? organizationID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no organization ID is provided
            if (organizationID == null)
                return null;

            // Query territorial authorities for the specified governing agency organization
            var entity = await db.Set<Label.TerritorialAuthority>()
                .AsNoTracking()
                .Where(e => e.GoverningAgencyOrgID == organizationID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new TerritorialAuthorityDto
            {
                TerritorialAuthority = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of holder DTOs for a specified organization ID.
        /// </summary>
        /// <param name="db">The database context for querying holder entities.</param>
        /// <param name="organizationID">The organization identifier used as holder organization ID to filter holders.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of HolderDto objects, or null if no organization ID provided or no entities found.</returns>
        /// <seealso cref="Label.Holder"/>
        /// <seealso cref="HolderDto"/>
        private static async Task<List<HolderDto>?> buildHoldersDtoAsync(ApplicationDbContext db, int? organizationID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no organization ID is provided
            if (organizationID == null)
                return null;

            // Query holders for the specified holder organization
            var entity = await db.Set<Label.Holder>()
                .AsNoTracking()
                .Where(e => e.HolderOrganizationID == organizationID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new HolderDto
            {
                Holder = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
            #endregion
        }
        #endregion

        #region Document Relationship Child Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of BusinessOperation DTOs for the specified document relationship.
        /// Retrieves business operation details for establishments or labelers linked through document relationships.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="docRelId">The document relationship ID to find business operations for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of BusinessOperation DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.BusinessOperation"/>
        private static async Task<List<BusinessOperationDto>> buildBusinessOperationsAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (docRelId == null) return new List<BusinessOperationDto>();

            // Query business operations for the specified document relationship
            var items = await db.Set<Label.BusinessOperation>()
                .AsNoTracking()
                .Where(e => e.DocumentRelationshipID == docRelId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new BusinessOperationDto { BusinessOperation = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        // Adds licenses to the the business operation
        // BusinessOperationDto > LicenseDto
        private static async Task<List<LicenseDto>> buildLicensesAsync(ApplicationDbContext db, int? businessOperationId, string pkSecret, ILogger logger)
        { return null; }

        /**************************************************************/
        // Adds DisciplinaryAction DTOs for the specified license.
        //  BusinessOperationDto > LicenseDto > DisciplinaryActionDto
        private static async Task<List<DisciplinaryActionDto>> buildDisciplinaryActionsAsync(ApplicationDbContext db, int? licenseId, string pkSecret, ILogger logger)
        { return null; }

        /**************************************************************/
        // Adds BusinessOperationQualifier DTOs for the specified business operation.
        // BuisnessOperation > BusinessOperationQualifier
        private static async Task<List<BusinessOperationQualifierDto>> buildBusinessOperationQualifiersAsync(ApplicationDbContext db, int? businessOperationId, string pkSecret, ILogger logger) { return null; }

        /**************************************************************/
        /// <summary>
        /// Builds a list of CertificationProductLink DTOs for the specified document relationship.
        /// Retrieves links between establishments and products being certified in Blanket No Changes Certification documents.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="docRelId">The document relationship ID to find certification product links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of CertificationProductLink DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.CertificationProductLink"/>
        private static async Task<List<CertificationProductLinkDto>> buildCertificationProductLinksAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (docRelId == null) return new List<CertificationProductLinkDto>();

            // Query certification product links for the specified document relationship
            var items = await db.Set<Label.CertificationProductLink>()
                .AsNoTracking()
                .Where(e => e.DocumentRelationshipID == docRelId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items.Select(item => new CertificationProductLinkDto { CertificationProductLink = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ComplianceAction DTOs for the specified document relationship.
        /// Retrieves FDA-initiated inactivation/reactivation status for establishment registrations.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="docRelId">The document relationship ID to find compliance actions for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ComplianceAction DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ComplianceAction"/>
        private static async Task<List<ComplianceActionDto>> buildComplianceActionsForRelationshipAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (docRelId == null) return new List<ComplianceActionDto>();

            // Query compliance actions for the specified document relationship
            var items = await db.Set<Label.ComplianceAction>()
                .AsNoTracking()
                .Where(e => e.DocumentRelationshipID == docRelId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ComplianceActionDto { ComplianceAction = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of FacilityProductLink DTOs for the specified document relationship.
        /// Retrieves links between facilities and cosmetic products in registration or listing documents.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="docRelId">The document relationship ID to find facility product links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of FacilityProductLink DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.FacilityProductLink"/>
        private static async Task<List<FacilityProductLinkDto>> buildFacilityProductLinksAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (docRelId == null) return new List<FacilityProductLinkDto>();

            // Query facility product links for the specified document relationship
            var items = await db.Set<Label.FacilityProductLink>()
                .AsNoTracking()
                .Where(e => e.DocumentRelationshipID == docRelId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new FacilityProductLinkDto { FacilityProductLink = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }
        #endregion
    }
}