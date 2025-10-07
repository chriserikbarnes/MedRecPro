
using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Diagnostics;
using static MedRecPro.Models.Label;

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

            // Build query for paginated documents
            var query = db.Set<Label.Document>().AsNoTracking();

            if (page.HasValue && size.HasValue)
            {
                query = query
                    .OrderBy(d => d.DocumentID)
                    .Skip((page.Value - 1) * size.Value)
                    .Take(size.Value);
            }

            var docs = await query.ToListAsync();
            return await buildDocumentDtosFromEntitiesAsync(db, docs, pkSecret, logger);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of Document DTOs for a specific document identified by its GUID.
        /// Constructs complete document hierarchies including structured bodies, authors, 
        /// relationships, and legal authenticators for the specified document.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="documentGuid">The unique identifier for the specific document to retrieve.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="DocumentDto"/> containing the document if found, empty list otherwise.</returns>
        /// <example>
        /// <code>
        /// var documentGuid = Guid.Parse("12345678-1234-1234-1234-123456789012");
        /// var documents = await DtoLabelHelper.BuildDocumentsAsync(db, documentGuid, secret, logger);
        /// </code>
        /// </example>
        /// <remarks>
        /// This method uses sequential awaits instead of Task.WhenAll to ensure DbContext thread-safety.
        /// Returns a list for consistency with the paginated overload, but will contain at most one document.
        /// </remarks>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.Document.DocumentGUID"/>
        /// <seealso cref="Label.StructuredBody"/>
        /// <seealso cref="Label.DocumentAuthor"/>
        /// <seealso cref="Label.RelatedDocument"/>
        /// <seealso cref="Label.DocumentRelationship"/>
        /// <seealso cref="Label.LegalAuthenticator"/>
        public static async Task<List<DocumentDto>> BuildDocumentsAsync(
           ApplicationDbContext db,
           Guid documentGuid,
           string pkSecret,
           ILogger logger)
        {
            #region implementation

            // Build query for specific document by GUID
            var docs = await db.Set<Label.Document>()
                .AsNoTracking()
                .Where(d => d.DocumentGUID == documentGuid)
                .ToListAsync();

            return await buildDocumentDtosFromEntitiesAsync(db, docs, pkSecret, logger);

            #endregion
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
        public static async Task<PackageIdentifierDto?> GetPackageIdentifierAsync(ApplicationDbContext db,
            int? packagingLevelID,
            string pkSecret,
            ILogger logger)
        {
            return await buildPackageIdentifierDtoAsync(db, packagingLevelID, pkSecret, logger, null);
        }

        #endregion

        #region Private Implementation Methods

        /**************************************************************/
        /// <summary>
        /// Builds complete Document DTO objects from a collection of Document entities.
        /// Constructs the full hierarchy of related data for each document including
        /// structured bodies, authors, relationships, and legal authenticators.
        /// </summary>
        /// <param name="db">The application database context.</param>
        /// <param name="docs">Collection of Document entities to process.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of <see cref="DocumentDto"/> with complete hierarchical data.</returns>
        /// <remarks>
        /// This is the shared implementation used by both public overloads to maintain DRY principles.
        /// Uses sequential processing to ensure DbContext thread-safety.
        /// </remarks>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="DocumentDto"/>
        private static async Task<List<DocumentDto>> buildDocumentDtosFromEntitiesAsync(
            ApplicationDbContext db,
            List<Label.Document> docs,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            Stopwatch? stopwatch = Stopwatch.StartNew();
            var docDtos = new List<DocumentDto>();

            // Process each Document to build its complete DTO graph
            foreach (var doc in docs)
            {
                // Convert base document entity with encrypted ID
                var docDict = doc.ToEntityWithEncryptedId(pkSecret, logger);

                // Sequentially build all direct children of the Document.
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
                var childOrg = await buildOrganizationAsync(db, rel.ChildOrganizationID, pkSecret, logger);

                var parentOrg = await buildOrganizationAsync(db, rel.ParentOrganizationID, pkSecret, logger);

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
                    FacilityProductLinks = facilityLinks,
                    ChildOrganization = childOrg,
                    ParentOrganization = parentOrg
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

            List<int?> parentIds = new List<int?>();

            var sbDtos = new List<StructuredBodyDto>();

            // For each structured body, build its sections
            foreach (var sb in sbs)
            {
                var sectionDtos = await buildSectionsAsync(db, sb.StructuredBodyID, pkSecret, logger);

                parentIds.AddRange(sectionDtos
                    ?.Where(x => x != null && x.SectionID != null && x.SectionID > 0)
                    ?.Select(x => x.SectionID).ToList() ?? new List<int?>());

                var uniqueParentIds = parentIds.Distinct().ToList();

                var hierarchies = await db.Set<Label.SectionHierarchy>()
                    .AsNoTracking()
                    .Where(sh => sh != null
                        && sh.ParentSectionID != null
                        && sh.ParentSectionID > 0
                        && uniqueParentIds.Contains((int)sh.ParentSectionID))
                    .ToListAsync();

                sbDtos.Add(new StructuredBodyDto(pkSecret)
                {
                    StructuredBody = sb.ToEntityWithEncryptedId(pkSecret, logger),
                    Sections = sectionDtos ?? new List<SectionDto>(),
                    SectionHierarchies = hierarchies
                        ?.Select(h => new SectionHierarchyDto(pkSecret) { SectionHierarchy = h.ToEntityWithEncryptedId(pkSecret, logger) })
                        ?.ToList() ?? new List<SectionHierarchyDto>()
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


#if DEBUG
                //logSectionTextContentDebugInfo(content);
                //logSectionObservationMediaDebugInfo(media);
#endif

                // Assemble complete section DTO with all nested data
                sectionDtos.Add(new SectionDto(pkSecret)
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
        /// Diagnostic logging for SectionTextContentDto and its RenderedMedias.
        /// </summary>
        /// <param name="content"></param>
        private static void logSectionTextContentDebugInfo(List<SectionTextContentDto> content)
        {
            #region implementation
            // DIAGNOSTIC: Log what was built
            if (content != null && content.Any())
            {
                Debug.WriteLine($"=== buildSectionTextContentDtoAsync Results ===");
                Debug.WriteLine($"Total TextContent records: {content.Count}");

                foreach (var tc in content)
                {
                    Debug.WriteLine($"\nTextContent ID={tc.SectionTextContentID}");
                    Debug.WriteLine($"  ContentType: {tc.ContentType}");
                    Debug.WriteLine($"  SequenceNumber: {tc.SequenceNumber}");
                    Debug.WriteLine($"  RenderedMedias count: {tc.RenderedMedias?.Count ?? 0}");

                    if (tc.RenderedMedias?.Any() == true)
                    {
                        foreach (var rm in tc.RenderedMedias)
                        {
                            Debug.WriteLine($"    RenderedMedia ID={rm.RenderedMediaID}");
                            Debug.WriteLine($"      ObservationMediaID={rm.ObservationMediaID}");
                            Debug.WriteLine($"      SequenceInContent={rm.SequenceInContent}");
                        }
                    }
                }
                Debug.WriteLine($"=== End buildSectionTextContentDtoAsync ===");
            }
            #endregion

        }

        /**************************************************************/
        /// <summary>
        /// Debug logging for ObservationMediaDto list.
        /// </summary>
        /// <param name="media"></param>
        private static void logSectionObservationMediaDebugInfo(List<ObservationMediaDto> media)
        {
            #region implementation
            // DIAGNOSTIC: Log what was built
            if (media != null && media.Any())
            {
                Debug.WriteLine($"=== buildObservationMediaAsync Results ===");
                Debug.WriteLine($"Total ObservationMedia records: {media.Count}");
                foreach (var om in media)
                {
                    Debug.WriteLine($"\nObservationMedia ID={om.ObservationMediaID}");
                    Debug.WriteLine($"  MediaID: {om.MediaID}");
                    Debug.WriteLine($"  FileName: {om.FileName}");
                }
                Debug.WriteLine($"=== End buildObservationMediaAsync ===");
            }
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
            return entity.Select(entity => new SectionHierarchyDto(pkSecret)
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
        /// Builds text table DTOs with their associated columns and rows for the specified section text content.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionTextContentID">The section text content ID to find text tables for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text table DTOs with nested column and row data for the section text content.</returns>
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

            // Build DTOs with nested column and row data for each text table
            foreach (var e in entity)
            {
                // Build all columns for this text table
                var columns = await buildTextTableColumnsAsync(db, e.TextTableID, pkSecret, logger);

                // Build all rows for this text table
                var rows = await buildTextTableRowsAsync(db, e.TextTableID, pkSecret, logger);

                dtos.Add(new TextTableDto
                {
                    TextTable = e.ToEntityWithEncryptedId(pkSecret, logger),
                    TextTableColumns = columns,
                    TextTableRows = rows
                });
            }

            // Return completed DTOs
            return dtos ?? new List<TextTableDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores details specific to individual col elements.
        /// Builds text table column DTOs for the specified text table.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="textTableID">The text table ID to find columns for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of text table column DTOs for the text table.</returns>
        /// <seealso cref="Label.TextTableColumn"/>
        /// <seealso cref="TextTableColumnDto"/>
        /// <seealso cref="TextTableDto"/>
        private static async Task<List<TextTableColumnDto>> buildTextTableColumnsAsync(ApplicationDbContext db, int? textTableID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no text table ID is provided
            if (textTableID == null)
                return new List<TextTableColumnDto>();

            // Query text table columns for the specified text table
            var entity = await db.Set<Label.TextTableColumn>()
                .AsNoTracking()
                .Where(e => e.TextTableID == textTableID)
                .ToListAsync();

            // Return empty list if no entities found
            if (entity == null)
                return new List<TextTableColumnDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new TextTableColumnDto
            {
                TextTableColumn = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<TextTableColumnDto>();
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
            var items = await db.Set<Label.Stakeholder>()
                .AsNoTracking()
                .Where(e => e.StakeholderID == stakeholderID)
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
        /// <seealso cref="Label.AttachedDocument"/>
        /// <seealso cref="REMSMaterialDto"/>
        /// <seealso cref="AttachedDocumentDto"/>
        /// <seealso cref="buildREMSAttachmentsAsync"/>
        private static async Task<List<REMSMaterialDto>> buildREMSMaterialsAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<REMSMaterialDto>();

            var dtos = new List<REMSMaterialDto>();

            // Query REMS materials for the specified section
            var items = await db.Set<Label.REMSMaterial>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<REMSMaterialDto>();

            foreach (var item in items)
            {
                if (item.REMSMaterialID == null) continue;

                // Build all attachments for this REMS material
                var attachments = await buildREMSAttachmentsAsync(db, item.REMSMaterialID, pkSecret, logger);

                dtos.Add(new REMSMaterialDto
                {
                    REMSMaterial = item.ToEntityWithEncryptedId(pkSecret, logger),
                    AttachedDocuments = attachments
                });
            }

            // Transform entities to DTOs with encrypted IDs
            return dtos ?? new List<REMSMaterialDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds REMS attachments for the specified parent entity. Retrieves 
        /// attached document records for a specified parent entity ID and 
        /// transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="parentEntityID">The unique identifier of the parent entity to find attached documents for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of AttachedDocumentDto objects representing the REMS attachments, or an empty list if none found</returns>
        /// <remarks>
        /// REMS (Risk Evaluation and Mitigation Strategies) attachments are regulatory documents associated with parent entities.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var remsAttachments = await buildREMSAttachmentsAsync(dbContext, 127, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.AttachedDocument"/>
        /// <seealso cref="AttachedDocumentDto"/>
        private static async Task<List<AttachedDocumentDto>> buildREMSAttachmentsAsync(ApplicationDbContext db, int? parentEntityID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no parent entity ID provided
            if (parentEntityID == null)
                return new List<AttachedDocumentDto>();

            // Query attached documents for the specified parent entity using read-only tracking
            var items = await db.Set<Label.AttachedDocument>()
                .AsNoTracking()
                .Where(e => e.ParentEntityID == parentEntityID)
                .ToListAsync();

            // Return empty list if no attached documents found
            if (items == null || !items.Any())
                return new List<AttachedDocumentDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new AttachedDocumentDto { AttachedDocument = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<AttachedDocumentDto>();
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
                var characteristics = await buildCharacteristicsDtoAsync(db, product.ProductID, pkSecret, logger, sectionId);
                var packageIdentifiers = await buildDirectPackageIdentifiersAsync(db, product.ProductID, sectionId, pkSecret, logger);
                var childLots = await buildLabelLotHierarchyDtoAsync(db, product.ProductID, pkSecret, logger);
                var dosingSpecs = await buildDosingSpecificationsAsync(db, product.ProductID, pkSecret, logger);
                var equivalents = await buildEquivalentEntitiesAsync(db, product.ProductID, pkSecret, logger);
                var genericMeds = await buildGenericMedicinesAsync(db, product.ProductID, pkSecret, logger);
                var ingredientInstances = await buildProductIngredientInstancesAsync(db, product.ProductID, pkSecret, logger);
                var ingredients = await buildIngredientsAsync(db, product.ProductID, pkSecret, logger);
                var marketingCats = await buildMarketingCategoriesDtoAsync(db, product.ProductID, pkSecret, logger);
                var marketingStatuses = await buildProductMarketingStatusesAsync(db, product.ProductID, pkSecret, logger);
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
                    MarketingStatuses = marketingStatuses,
                    ProductIdentifiers = productIds,
                    PackageIdentifiers = packageIdentifiers,
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
        /// Retrieves marketing status records for a specified product ID and 
        /// transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find marketing statuses for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of MarketingStatusDto objects representing the marketing statuses, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: ProductDto > MarketingStatusDto
        /// Marketing statuses track the regulatory and commercial status of products in various markets.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var marketingStatuses = await buildProductMarketingStatusesAsync(dbContext, 842, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.MarketingStatus"/>
        /// <seealso cref="MarketingStatusDto"/>
        private static async Task<List<MarketingStatusDto>> buildProductMarketingStatusesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null)
                return new List<MarketingStatusDto>();

            // Query marketing statuses for the specified product using read-only tracking
            var items = await db.Set<Label.MarketingStatus>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return empty list if no marketing statuses found
            if (items == null || !items.Any())
                return new List<MarketingStatusDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new MarketingStatusDto { MarketingStatus = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<MarketingStatusDto>();
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
        /// Builds a list of characteristic DTOs for a specified product ID or packaging level ID,
        /// including associated packaging levels. Stores characteristics of a product or package 
        /// (subjectOf characteristic).
        /// </summary>
        /// <param name="db">The database context for querying characteristic entities.</param>
        /// <param name="productID">The product identifier to filter characteristics. Optional if packagingLevelID is provided.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <param name="sectionId">Optional section ID used for compliance actions in indexing files.</param>
        /// <param name="packagingLevelID">Optional packaging level identifier to filter characteristics for package-level properties.</param>
        /// <returns>A list of CharacteristicDto objects with associated packaging levels.</returns>
        /// <seealso cref="Label.Characteristic"/>
        /// <seealso cref="CharacteristicDto"/>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="buildPackageIdentifierDtoAsync"/>
        /// <remarks>
        /// Based on Section 3.1.9 and enhanced for package-level characteristics per _Packaging.cshtml template.
        /// Relates to the Characteristic table (ProductID and PackagingLevelID foreign keys).
        /// Can filter by either ProductID or PackagingLevelID to support both product-level and package-level characteristics.
        /// When filtering by PackagingLevelID, retrieves characteristics specific to that packaging level.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get product-level characteristics
        /// var productChars = await buildCharacteristicsDtoAsync(db, 123, pkSecret, logger);
        /// 
        /// // Get package-level characteristics
        /// var packageChars = await buildCharacteristicsDtoAsync(db, null, pkSecret, logger, null, 456);
        /// </code>
        /// </example>
        private static async Task<List<CharacteristicDto>> buildCharacteristicsDtoAsync(
            ApplicationDbContext db,
            int? productID,
            string pkSecret,
            ILogger logger,
            int? sectionId = null,
            int? packagingLevelID = null)
        {
            #region implementation

            #region validation
            // Return empty list if neither product ID nor packaging level ID is provided
            if (productID == null && packagingLevelID == null)
                return new List<CharacteristicDto>();
            #endregion

            #region query construction
            // Build query based on provided parameters
            var query = db.Set<Label.Characteristic>()
                .AsNoTracking()
                .AsQueryable();

            // Apply appropriate filter based on which ID was provided
            if (productID != null)
            {
                query = query.Where(e => e.ProductID == productID);
            }
            else if (packagingLevelID != null)
            {
                query = query.Where(e => e.PackagingLevelID == packagingLevelID);
            }

            // Execute query to retrieve characteristic entities
            var entities = await query.ToListAsync();
            #endregion

            #region dto building
            var dtos = new List<CharacteristicDto>();
            List<PackagingLevelDto?> pkgLevelDtos = new List<PackagingLevelDto?>();

            // Process each characteristic and build associated packaging levels
            foreach (var item in entities)
            {
                // Build associated PackageIdentifiers if PackagingLevelID is present
                var packageIdentifiers = new List<PackageIdentifierDto?>();

                if (item.PackagingLevelID != null)
                {
                    // Build package identifier for this characteristic's packaging level
                    var pkgDto = await buildPackageIdentifierDtoAsync(
                        db,
                        item.PackagingLevelID,
                        pkSecret,
                        logger,
                        sectionId);

                    if (pkgDto?.PackageIdentifier != null)
                    {
                        packageIdentifiers.Add(pkgDto);
                    }

                    // Build packaging level details if needed for context
                    // Note: Avoid recursive loops - only fetch if characteristic is product-level
                    if (productID != null)
                    {
                        List<PackagingLevelDto>? itemPackagingLevels = (await buildPackagingLevelsDtoAsync(
                            db,
                            item.PackagingLevelID,
                            pkSecret,
                            logger))
                            ?.ToList();

                        if (itemPackagingLevels != null && itemPackagingLevels.Any())
                            pkgLevelDtos.AddRange(itemPackagingLevels);
                    }
                }

                // Create characteristic DTO with packaging levels and identifiers
                dtos.Add(new CharacteristicDto
                {
                    Characteristic = item.ToEntityWithEncryptedId(pkSecret, logger),
                    PackagingIdentifiers = packageIdentifiers,
                    PackagingLevels = pkgLevelDtos
                });
            }

            return dtos;
            #endregion

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
                    PackagingLevels = packagingLevels
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
        /// <seealso cref="Label.IngredientSourceProduct"/>
        /// <seealso cref="Label.IngredientInstance"/>
        /// <seealso cref="Label.SpecifiedSubstance"/>
        /// <seealso cref="IngredientDto"/>
        /// <seealso cref="IngredientSubstanceDto"/>
        /// <seealso cref="IngredientSourceProductDto"/>
        /// <seealso cref="IngredientInstanceDto"/>"/>
        /// <seealso cref="SpecifiedSubstanceDto"/>
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

                var specifiedSubstances = await buildSpecifiedSubstancesAsync(
                    db,
                    ingredient.SpecifiedSubstanceID,
                    pkSecret,
                    logger);

                // Assemble ingredient DTO with substance data and instances
                ingredientDtos.Add(new IngredientDto
                {
                    Ingredient = ingredient.ToEntityWithEncryptedId(pkSecret, logger),
                    IngredientInstances = ingredientInstances,
                    IngredientSubstance = ingredientSubstance,
                    IngredientSourceProducts = sourceProducts,
                    ReferenceSubstances = refSubstance,
                    SpecifiedSubstances = specifiedSubstances
                });
            }
            return ingredientDtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores the specified substance code and name linked to an ingredient in 
        /// Biologic/Drug Substance Indexing documents. Retrieves specified 
        /// substance records for a specified ingredient ID and transforms them 
        /// into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="specifiedSubstanceID">The unique identifier of the ingredient to find specified substances for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of SpecifiedSubstanceDto objects representing the specified substances, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IngredientDto > SpecifiedSubstanceDto
        /// Specified substances contain substance codes and names linked to ingredients in Biologic/Drug Substance Indexing documents.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var specifiedSubstances = await buildSpecifiedSubstancesAsync(dbContext, 321, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.SpecifiedSubstance"/>
        /// <seealso cref="SpecifiedSubstanceDto"/>
        private static async Task<List<SpecifiedSubstanceDto>> buildSpecifiedSubstancesAsync(ApplicationDbContext db, int? specifiedSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no ingredient ID provided
            if (specifiedSubstanceID == null)
                return new List<SpecifiedSubstanceDto>();

            // Query specified substances for the specified ingredient using read-only tracking
            var entity = await db.Set<Label.SpecifiedSubstance>()
                .AsNoTracking()
                .Where(e => e.SpecifiedSubstanceID == specifiedSubstanceID)
                .ToListAsync();

            // Return empty list if no specified substances found
            if (entity == null || !entity.Any())
                return new List<SpecifiedSubstanceDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new SpecifiedSubstanceDto { SpecifiedSubstance = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<SpecifiedSubstanceDto>();
            #endregion
        }

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
        /// <seealso cref="Label.ActiveMoiety"/>
        /// <seealso cref="Label.IngredientInstance"/>
        /// <seealso cref="IngredientInstanceDto"/>
        /// <seealso cref="IngredientSubstanceDto"/>
        /// <seealso cref="ActiveMoietyDto"/>
        /// <seealso cref="buildIngredientInstancesAsync"/>
        /// <seealso cref="buildActiveMoietiesDtoAsync"/>"/>
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

            var moieties = await buildActiveMoietiesDtoAsync(
                db,
                entity.IngredientSubstanceID,
                pkSecret,
                logger);

            // Transform entity to DTO with encrypted ID
            return new IngredientSubstanceDto
            {
                IngredientSubstance = entity.ToEntityWithEncryptedId(pkSecret, logger),
                IngredientInstances = substanceInstances,
                ActiveMoieties = moieties
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Uses the ingredient substance ID to find active moieties. Retrieves 
        /// active moiety records for a specified ingredient substance ID and 
        /// transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="ingredientSubstanceID">The unique identifier of the ingredient substance to find active moieties for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of ActiveMoietyDto objects representing the active moieties, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IngredientSubstanceDto > ActiveMoietyDto
        /// Active moieties represent the therapeutically active portions of ingredient substances.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var activeMoieties = await buildActiveMoietiesDtoAsync(dbContext, 579, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ActiveMoiety"/>
        /// <seealso cref="ActiveMoietyDto"/>
        private static async Task<List<ActiveMoietyDto>> buildActiveMoietiesDtoAsync(ApplicationDbContext db, int? ingredientSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no ingredient substance ID provided
            if (ingredientSubstanceID == null)
                return new List<ActiveMoietyDto>();

            // Query active moieties for the specified ingredient substance using read-only tracking
            var entity = await db.Set<Label.ActiveMoiety>()
                .AsNoTracking()
                .Where(e => e.IngredientSubstanceID == ingredientSubstanceID)
                .ToListAsync();

            // Return empty list if no active moieties found
            if (entity == null || !entity.Any())
                return new List<ActiveMoietyDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new ActiveMoietyDto { ActiveMoiety = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<ActiveMoietyDto>();
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
        private static async Task<List<IngredientInstanceDto>> buildIngredientInstancesAsync(ApplicationDbContext db, int? ingredientSubstanceId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredientSubstanceId provided
            if (ingredientSubstanceId == null)
                return new List<IngredientInstanceDto>();

            // Query all IngredientInstance rows for this substance with no change tracking
            var entity = await db.Set<Label.IngredientInstance>()
                .AsNoTracking()
                .Where(ii => ii.IngredientSubstanceID == ingredientSubstanceId)
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
                });
            }

            return ingredientInstances;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds product ingredient instances for a specified product ID. Retrieves 
        /// ingredient instance records associated with a product and transforms 
        /// them into DTOs with encrypted identifiers and related lot identifier data.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="productID">The unique identifier of the product to find ingredient instances for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of IngredientInstanceDto objects representing the ingredient instances with their lot identifiers, or an empty list if none found</returns>
        /// <remarks>
        /// This method queries ingredient instances where FillLotInstanceID matches the provided product ID.
        /// Each ingredient instance is enriched with its associated lot identifier information.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var ingredientInstances = await buildProductIngredientInstancesAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.IngredientInstance"/>
        /// <seealso cref="IngredientInstanceDto"/>
        /// <seealso cref="buildIngredientSubstanceAsync"/>
        /// <seealso cref="buildLotIdentifierDtoAsync"/>
        private static async Task<List<IngredientInstanceDto>> buildProductIngredientInstancesAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no product ID provided
            if (productID == null)
                return new List<IngredientInstanceDto>();

            // Query all ingredient instance rows for this product with no change tracking
            var entity = await db.Set<Label.IngredientInstance>()
                .AsNoTracking()
                .Where(ii => ii.FillLotInstanceID == productID)
                .ToListAsync();

            // Return empty list if no ingredient instances found
            if (entity == null || !entity.Any())
                return new List<IngredientInstanceDto>();

            // Build IngredientInstanceDto objects with lot identifier data
            List<IngredientInstanceDto> ingredientInstances = new List<IngredientInstanceDto>();

            // Process each ingredient instance and build associated data
            foreach (var item in entity)
            {
                // Build the lot identifier data for this instance
                var lotIdentifier = await buildLotIdentifierDtoAsync(db, item.LotIdentifierID, pkSecret, logger);

                // Create the IngredientInstanceDto with encrypted IDs and lot identifier
                ingredientInstances.Add(new IngredientInstanceDto
                {
                    IngredientInstance = item.ToEntityWithEncryptedId(pkSecret, logger),
                    LotIdentifier = lotIdentifier
                });
            }

            // Return processed ingredient instances with lot identifier data, ensuring non-null result
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
        private static async Task<List<IngredientInstanceDto>> buildIngredientInstancesDtoAsync(ApplicationDbContext db, int? ingredientSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredient substance ID is provided
            if (ingredientSubstanceID == null)
                return new List<IngredientInstanceDto>();

            // Query ingredient instances for the specified ingredient substance
            var entity = await db.Set<Label.IngredientInstance>()
                .AsNoTracking()
                .Where(e => e.IngredientSubstanceID == ingredientSubstanceID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<IngredientInstanceDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity
                .Select(e => new IngredientInstanceDto { IngredientInstance = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<IngredientInstanceDto>();
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
        private static async Task<List<IngredientSourceProductDto>> buildIngredientSourceProductsDtoAsync(ApplicationDbContext db, int? ingredientID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredient ID is provided
            if (ingredientID == null)
                return new List<IngredientSourceProductDto>();

            // Query ingredient source products for the specified ingredient
            var entity = await db.Set<Label.IngredientSourceProduct>()
                .AsNoTracking()
                .Where(e => e.IngredientID == ingredientID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<IngredientSourceProductDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new IngredientSourceProductDto { IngredientSourceProduct = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<IngredientSourceProductDto>();
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
        private static async Task<List<ReferenceSubstanceDto>> buildReferenceSubstancesDtoAsync(ApplicationDbContext db, int? ingredientSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no ingredient substance ID is provided
            if (ingredientSubstanceID == null)
                return new List<ReferenceSubstanceDto>();

            // Query reference substances for the specified ingredient substance
            var entity = await db.Set<Label.ReferenceSubstance>()
                .AsNoTracking()
                .Where(e => e.IngredientSubstanceID == ingredientSubstanceID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<ReferenceSubstanceDto>();

            // Transform entities to DTOs with encrypted IDs, filtering out null entities
            return entity.Where(e => e != null).Select(e => new ReferenceSubstanceDto
            {
                ReferenceSubstance = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<ReferenceSubstanceDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of IdentifiedSubstance DTOs for the specified section.
        /// Retrieves substance details such as active moieties and pharmacologic class identifiers used in indexing contexts.
        /// ENHANCED: Now includes chemical moiety data for substance structure information.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="sectionId">The section ID to find identified substances for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of IdentifiedSubstance DTOs with encrypted IDs including moiety data.</returns>
        /// <seealso cref="Label.IdentifiedSubstance"/>
        /// <seealso cref="Label.SubstanceSpecification"/>
        /// <seealso cref="Label.Moiety"/>
        /// <seealso cref="IdentifiedSubstanceDto"/>
        /// <seealso cref="SubstanceSpecificationDto"/>
        /// <seealso cref="MoietyDto"/>
        private static async Task<List<IdentifiedSubstanceDto>> buildIdentifiedSubstancesAsync(ApplicationDbContext db, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (sectionId == null) return new List<IdentifiedSubstanceDto>();

            // Query identified substances for the specified section
            var items = await db.Set<Label.IdentifiedSubstance>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<IdentifiedSubstanceDto>();

            // For each identified substance, build its related entities
            // Transform entities to DTOs with encrypted IDs
            var dtos = new List<IdentifiedSubstanceDto>();
            foreach (var item in items)
            {
                // For each IdentifiedSubstance, build SubstanceSpecifications
                var specs = await buildSubstanceSpecificationsAsync(db, item.IdentifiedSubstanceID, pkSecret, logger);

                var contributingFactors = await buildContributingFactorsAsync(db, item.IdentifiedSubstanceID, pkSecret, logger);

                var pharmacologicClasses = await buildPharmacologicClassesAsync(db, item.IdentifiedSubstanceID, pkSecret, logger);

                // ENHANCED: Build moieties for substance structure information
                var moieties = await buildMoietyAsync(db, item.IdentifiedSubstanceID, pkSecret, logger);

                dtos.Add(new IdentifiedSubstanceDto
                {
                    IdentifiedSubstance = item.ToEntityWithEncryptedId(pkSecret, logger),
                    SubstanceSpecifications = specs,
                    ContributingFactors = contributingFactors,
                    PharmacologicClasses = pharmacologicClasses,
                    Moiety = moieties
                });
            }

            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of moiety DTOs for the specified identified substance ID.
        /// Retrieves chemical moiety records that define the molecular structure and 
        /// quantity information for substance indexing contexts.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="identifiedSubstanceID">The unique identifier of the identified substance to find moieties for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of MoietyDto objects representing the chemical moieties, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IdentifiedSubstanceDto > MoietyDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// Moieties contain molecular structure and quantity information per FDA Substance Registration System.
        /// </remarks>
        /// <example>
        /// <code>
        /// var moieties = await buildMoietyAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.Moiety"/>
        /// <seealso cref="MoietyDto"/>
        private static async Task<List<MoietyDto>> buildMoietyAsync(ApplicationDbContext db, int? identifiedSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no identified substance ID provided
            if (identifiedSubstanceID == null)
                return new List<MoietyDto>();

            // Query moieties for the specified identified substance using read-only tracking
            var items = await db.Set<Label.Moiety>()
                .AsNoTracking()
                .Where(e => e.IdentifiedSubstanceID == identifiedSubstanceID)
                .OrderBy(e => e.SequenceNumber)
                .ToListAsync();

            // Return empty list if no moieties found
            if (items == null || !items.Any())
                return new List<MoietyDto>();

            // Transform entities to DTOs with encrypted IDs for security
            var dtos = new List<MoietyDto>();
            foreach (var item in items)
            {
                // Skip entities without valid IDs
                if (item.MoietyID == null)
                    continue;

                // ENHANCED: Build characteristics for this moiety
                var characteristics = await buildMoietyCharacteristicsAsync(
                    db,
                    item.MoietyID,
                    pkSecret,
                    logger);

                // Create moiety DTO with encrypted ID and characteristics
                dtos.Add(new MoietyDto
                {
                    Moiety = item.ToEntityWithEncryptedId(pkSecret, logger),
                    Characteristics = characteristics // Add characteristics to the DTO
                });
            }

            // Ensure non-null result
            return dtos ?? new List<MoietyDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of characteristic DTOs for a specified moiety ID.
        /// Retrieves chemical structure data including MOLFILE, InChI, and InChI-Key.
        /// </summary>
        private static async Task<List<CharacteristicDto>> buildMoietyCharacteristicsAsync(
            ApplicationDbContext db,
            int? moietyID,
            string pkSecret,
            ILogger logger)
        {
            #region implementation
            // Return empty list if no moiety ID is provided
            if (moietyID == null)
                return new List<CharacteristicDto>();

            // Query characteristics for the specified moiety
            var entities = await db.Set<Label.Characteristic>()
                .AsNoTracking()
                .Where(e => e.MoietyID == moietyID)
                .ToListAsync();

            var dtos = new List<CharacteristicDto>();

            // Process each characteristic
            foreach (var item in entities)
            {
                // Create characteristic DTO
                dtos.Add(new CharacteristicDto
                {
                    Characteristic = item.ToEntityWithEncryptedId(pkSecret, logger)
                    // Note: For moiety characteristics, PackagingLevels would be empty
                });
            }

            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of contributing factors for the specified factor substance ID 
        /// where FactorSubstanceID equals IdentifiedSubstanceID. Retrieves contributing 
        /// factor records associated with an identified substance and transforms 
        /// them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="identifiedSubstanceID">The unique identifier of the identified substance to find contributing factors for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of ContributingFactorDto objects representing the contributing factors, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IdentifiedSubstanceDto > ContributingFactorDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var contributingFactors = await buildContributingFactorsAsync(dbContext, 456, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ContributingFactor"/>
        /// <seealso cref="Label.InteractionConsequence"/>
        /// <seealso cref="ContributingFactorDto"/>
        /// <seealso cref="InteractionConsequenceDto"/>
        private static async Task<List<ContributingFactorDto>> buildContributingFactorsAsync(ApplicationDbContext db, int? identifiedSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no identified substance ID provided
            if (identifiedSubstanceID == null)
                return new List<ContributingFactorDto>();

            var dtos = new List<ContributingFactorDto>();

            // Query contributing factors for the specified identified substance using read-only tracking
            var entity = await db.Set<Label.ContributingFactor>()
                .AsNoTracking()
                .Where(e => e.FactorSubstanceID == identifiedSubstanceID)
                .ToListAsync();

            // Return empty list if no contributing factors found
            if (entity == null || !entity.Any())
                return new List<ContributingFactorDto>();

            foreach (var e in entity)
            {
                // Skip entities without valid IDs
                if (e.ContributingFactorID == null)
                    continue;

                var interactions = await buildContributingFactorInteractionConsequencesAsync(db, e.InteractionIssueID, pkSecret, logger);

                // Create contributing factor DTO with encrypted ID
                dtos.Add(new ContributingFactorDto
                {
                    ContributingFactor = e.ToEntityWithEncryptedId(pkSecret, logger),
                    InteractionConsequences = interactions
                });
            }

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return dtos ?? new List<ContributingFactorDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of pharmacologic classes for the IdentifiedSubstance.
        /// Handles both ActiveMoiety indexing (via PharmacologicClassLink) and 
        /// PharmacologicClass definitions (direct IdentifiedSubstanceID relationship).
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="identifiedSubstanceID">The unique identifier of the identified substance to find pharmacologic classes for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of PharmacologicClassDto objects representing the pharmacologic classes with their associated data, or an empty list if none found</returns>
        private static async Task<List<PharmacologicClassDto>> buildPharmacologicClassesAsync(ApplicationDbContext db,
            int? identifiedSubstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no identified substance ID provided
            if (identifiedSubstanceID == null)
                return new List<PharmacologicClassDto>();

            var dtos = new List<PharmacologicClassDto>();

            // CASE 1: PharmacologicClass Definitions (Section 8.2.3)
            // Direct relationship via IdentifiedSubstanceID
            var definitionClasses = await db.Set<Label.PharmacologicClass>()
                .AsNoTracking()
                .Where(e => e.IdentifiedSubstanceID == identifiedSubstanceID)
                .ToListAsync();

            // CASE 2: ActiveMoiety Indexing (Section 8.2.2) 
            // Relationship via PharmacologicClassLink table
            var linkedClassIds = await db.Set<Label.PharmacologicClassLink>()
                .AsNoTracking()
                .Where(link => link.ActiveMoietySubstanceID == identifiedSubstanceID)
                .Select(link => link.PharmacologicClassID)
                .ToListAsync();

            var linkedClasses = new List<Label.PharmacologicClass>();
            if (linkedClassIds.Any())
            {
                linkedClasses = await db.Set<Label.PharmacologicClass>()
                    .AsNoTracking()
                    .Where(pc => linkedClassIds.Contains(pc.PharmacologicClassID))
                    .ToListAsync();
            }

            // Combine both types of relationships
            var allClasses = definitionClasses.Concat(linkedClasses).Distinct().ToList();

            logger.LogInformation("Found {DefinitionCount} definition classes and {LinkedCount} linked classes for substance {SubstanceId}",
                definitionClasses.Count, linkedClasses.Count, identifiedSubstanceID);

            // Process each pharmacologic class and build associated data
            foreach (var pharmClass in allClasses)
            {
                // Skip entities without valid IDs
                if (pharmClass.PharmacologicClassID == null)
                    continue;

                // Build the pharmacologic class names, links, and hierarchies for this class
                var pharmacologicClassNames = await buildPharmacologicClassNamesAsync(db, pharmClass.PharmacologicClassID, pkSecret, logger);
                var pharmLinks = await buildPharmacologicClassLinksAsync(db, pharmClass.PharmacologicClassID, pkSecret, logger);
                var pharmHierarchies = await buildPharmacologicClassHierarchiesAsync(db, pharmClass.PharmacologicClassID, pkSecret, logger);

                // Create pharmacologic class DTO with encrypted ID and associated data
                dtos.Add(new PharmacologicClassDto
                {
                    PharmacologicClass = pharmClass.ToEntityWithEncryptedId(pkSecret, logger),
                    PharmacologicClassNames = pharmacologicClassNames,
                    PharmacologicClassLinks = pharmLinks,
                    PharmacologicClassHierarchies = pharmHierarchies
                });
            }

            logger.LogInformation("Built {Count} PharmacologicClassDto objects for substance {SubstanceId}", dtos.Count, identifiedSubstanceID);

            // Return processed pharmacologic classes with associated data, ensuring non-null result
            return dtos ?? new List<PharmacologicClassDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of pharmacologic class names for the pharmacologic classes.
        /// Retrieves pharmacologic class name records for a specified pharmacologic 
        /// class ID and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="pharmacologicClassID">The unique identifier of the pharmacologic class to find names for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of PharmacologicClassNameDto objects representing the pharmacologic class names, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IdentifiedSubstanceDto > PharmacologicClassDto > PharmacologicClassNameDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var classNames = await buildPharmacologicClassNamesAsync(dbContext, 789, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.PharmacologicClassName"/>
        /// <seealso cref="PharmacologicClassNameDto"/>
        private static async Task<List<PharmacologicClassNameDto>> buildPharmacologicClassNamesAsync(ApplicationDbContext db, int? pharmacologicClassID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no pharmacologic class ID provided
            if (pharmacologicClassID == null)
                return new List<PharmacologicClassNameDto>();

            // Query pharmacologic class names for the specified pharmacologic class using read-only tracking
            var entity = await db.Set<Label.PharmacologicClassName>()
                .AsNoTracking()
                .Where(e => e.PharmacologicClassID == pharmacologicClassID)
                .ToListAsync();

            // Return empty list if no pharmacologic class names found
            if (entity == null || !entity.Any())
                return new List<PharmacologicClassNameDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new PharmacologicClassNameDto { PharmacologicClassName = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<PharmacologicClassNameDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of pharmacologic class links for the pharmacologic classes. 
        /// Retrieves pharmacologic class link records for a specified pharmacologic 
        /// class ID and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="pharmacologicClassID">The unique identifier of the pharmacologic class to find links for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of PharmacologicClassLinkDto objects representing the pharmacologic class links, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IdentifiedSubstanceDto > PharmacologicClassDto > PharmacologicClassLinkDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var classLinks = await buildPharmacologicClassLinksAsync(dbContext, 789, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.PharmacologicClassLink"/>
        /// <seealso cref="PharmacologicClassLinkDto"/>
        private static async Task<List<PharmacologicClassLinkDto>> buildPharmacologicClassLinksAsync(ApplicationDbContext db, int? pharmacologicClassID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no pharmacologic class ID provided
            if (pharmacologicClassID == null)
                return new List<PharmacologicClassLinkDto>();

            // Query pharmacologic class links for the specified pharmacologic class using read-only tracking
            var entity = await db.Set<Label.PharmacologicClassLink>()
                .AsNoTracking()
                .Where(e => e.PharmacologicClassID == pharmacologicClassID)
                .ToListAsync();

            // Return empty list if no pharmacologic class links found
            if (entity == null || !entity.Any())
                return new List<PharmacologicClassLinkDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new PharmacologicClassLinkDto { PharmacologicClassLink = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<PharmacologicClassLinkDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of pharmacologic class hierarchies for the pharmacologic 
        /// classes where ChildPharmacologicClassID equals pharmacologicClassID.
        /// Retrieves pharmacologic class hierarchy records for a specified 
        /// pharmacologic class ID and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="pharmacologicClassID">The unique identifier of the child pharmacologic class to find hierarchies for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of PharmacologicClassHierarchyDto objects representing the pharmacologic class hierarchies, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IdentifiedSubstanceDto > PharmacologicClassDto > PharmacologicClassHierarchyDto
        /// The query filters by ChildPharmacologicClassID to find parent hierarchies for the specified class.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var classHierarchies = await buildPharmacologicClassHierarchiesAsync(dbContext, 789, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.PharmacologicClassHierarchy"/>
        /// <seealso cref="PharmacologicClassHierarchyDto"/>
        private static async Task<List<PharmacologicClassHierarchyDto>> buildPharmacologicClassHierarchiesAsync(ApplicationDbContext db, int? pharmacologicClassID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no pharmacologic class ID provided
            if (pharmacologicClassID == null)
                return new List<PharmacologicClassHierarchyDto>();

            // Query pharmacologic class hierarchies where this class is the child using read-only tracking
            var entity = await db.Set<Label.PharmacologicClassHierarchy>()
                .AsNoTracking()
                .Where(e => e.ChildPharmacologicClassID == pharmacologicClassID)
                .ToListAsync();

            // Return empty list if no pharmacologic class hierarchies found
            if (entity == null || !entity.Any())
                return new List<PharmacologicClassHierarchyDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new PharmacologicClassHierarchyDto { PharmacologicClassHierarchy = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<PharmacologicClassHierarchyDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of consequences for contributing factors. Retrieves interaction 
        /// consequence records for a specified interaction issue ID and transforms 
        /// them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="interactionIssueID">The unique identifier of the interaction issue to find consequences for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of InteractionConsequenceDto objects representing the interaction consequences, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: IdentifiedSubstanceDto > ContributingFactorDto > InteractionConsequenceDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var interactionConsequences = await buildContributingFactorInteractionConsequencesAsync(dbContext, 654, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.InteractionConsequence"/>
        /// <seealso cref="InteractionConsequenceDto"/>
        private static async Task<List<InteractionConsequenceDto>> buildContributingFactorInteractionConsequencesAsync(ApplicationDbContext db, int? interactionIssueID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no interaction issue ID provided
            if (interactionIssueID == null)
                return new List<InteractionConsequenceDto>();

            // Query interaction consequences for the specified interaction issue using read-only tracking
            var entity = await db.Set<Label.InteractionConsequence>()
                .AsNoTracking()
                .Where(e => e.InteractionIssueID == interactionIssueID)
                .ToListAsync();

            // Return empty list if no interaction consequences found
            if (entity == null || !entity.Any())
                return new List<InteractionConsequenceDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new InteractionConsequenceDto { InteractionConsequence = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<InteractionConsequenceDto>();
            #endregion
        }

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
        /// <seealso cref="Label.ObservationCriterion"/>
        /// <seealso cref="SubstanceSpecificationDto"/>
        /// <seealso cref="AnalyteDto"/>
        /// <seealso cref="ObservationCriterionDto"/>
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

            if (items == null || !items.Any())
                return new List<SubstanceSpecificationDto>();

            var dtos = new List<SubstanceSpecificationDto>();

            // For each substance specification, build its analytes
            foreach (var item in items)
            {
                // For each SubstanceSpecification, build associated Analytes
                var analytes = await buildAnalytesAsync(db, item.SubstanceSpecificationID, pkSecret, logger);

                var observationCriteria = await buildObservationCriterionAsync(db, item.SubstanceSpecificationID, pkSecret, logger);

                // Create SubstanceSpecificationDto with encrypted IDs and nested analytes
                dtos.Add(new SubstanceSpecificationDto
                {
                    SubstanceSpecification = item.ToEntityWithEncryptedId(pkSecret, logger),
                    Analytes = analytes,
                    ObservationCriteria = observationCriteria
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

            if (items == null || !items.Any())
                return new List<AnalyteDto>();

            // Transform entities to DTOs with encrypted IDs using LINQ Select for efficiency
            return items
                .Select(item => new AnalyteDto { Analyte = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of ObservationCriterion DTOs for the specified SubstanceSpecification.
        /// Retrieves observation criterion records for a specified substance specification ID 
        /// and enriches them with application types and commodities, then transforms them into 
        /// DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="substanceSpecificationID">The unique identifier of the substance specification to find observation criteria for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of ObservationCriterionDto objects representing the observation criteria with their associated data, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SubstanceSpecificationDto > ObservationCriterionDto
        /// Each observation criterion is enriched with its application types and commodities.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var observationCriteria = await buildObservationCriterionAsync(dbContext, 987, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ObservationCriterion"/>
        /// <seealso cref="ObservationCriterionDto"/>
        /// <seealso cref="buildApplicationTypesAsync"/>
        /// <seealso cref="buildCommoditiesAsync"/>
        private static async Task<List<ObservationCriterionDto>> buildObservationCriterionAsync(ApplicationDbContext db, int? substanceSpecificationID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no substance specification ID provided
            if (substanceSpecificationID == null)
                return new List<ObservationCriterionDto>();

            var dtos = new List<ObservationCriterionDto>();

            // Query observation criteria for the specified substance specification using read-only tracking
            var entity = await db.Set<Label.ObservationCriterion>()
                .AsNoTracking()
                .Where(e => e.SubstanceSpecificationID == substanceSpecificationID)
                .ToListAsync();

            // Return empty list if no observation criteria found
            if (entity == null || !entity.Any())
                return new List<ObservationCriterionDto>();

            // Process each observation criterion and build associated data
            foreach (var e in entity)
            {
                // Skip entities without valid substance specification IDs
                if (e.SubstanceSpecificationID == null)
                    continue;

                // Build the application types and commodities for this observation criterion
                var applicationTypes = await buildApplicationTypesAsync(db, e.ApplicationTypeID, pkSecret, logger);
                var commodities = await buildCommoditiesAsync(db, e.CommodityID, pkSecret, logger);

                // Create observation criterion DTO with encrypted ID and associated data
                dtos.Add(new ObservationCriterionDto
                {
                    ObservationCriterion = e.ToEntityWithEncryptedId(pkSecret, logger),
                    ApplicationTypes = applicationTypes,
                    Commodities = commodities
                });
            }

            // Return processed observation criteria with associated data, ensuring non-null result
            return dtos ?? new List<ObservationCriterionDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Get the application type IDs from the ObservationCriterion. Retrieves 
        /// application type records for a specified application type ID and transforms 
        /// them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="applicationTypeID">The unique identifier of the application type to retrieve. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of ApplicationTypeDto objects representing the application types, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SubstanceSpecificationDto > ObservationCriterionDto > ApplicationTypeDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var applicationTypes = await buildApplicationTypesAsync(dbContext, 147, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ApplicationType"/>
        /// <seealso cref="ApplicationTypeDto"/>
        private static async Task<List<ApplicationTypeDto>> buildApplicationTypesAsync(ApplicationDbContext db, int? applicationTypeID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no application type ID provided
            if (applicationTypeID == null)
                return new List<ApplicationTypeDto>();

            // Query application types for the specified application type ID using read-only tracking
            var entity = await db.Set<Label.ApplicationType>()
                .AsNoTracking()
                .Where(e => e.ApplicationTypeID == applicationTypeID)
                .ToListAsync();

            // Return empty list if no application types found
            if (entity == null || !entity.Any())
                return new List<ApplicationTypeDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new ApplicationTypeDto { ApplicationType = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<ApplicationTypeDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the commodities for the specified commodity ID. Retrieves 
        /// commodity records for a specified commodity ID and transforms them 
        /// into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="commodityId">The unique identifier of the commodity to retrieve. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of CommodityDto objects representing the commodities, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: SubstanceSpecificationDto > ObservationCriterionDto > CommodityDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var commodities = await buildCommoditiesAsync(dbContext, 258, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.Commodity"/>
        /// <seealso cref="CommodityDto"/>
        private static async Task<List<CommodityDto>> buildCommoditiesAsync(ApplicationDbContext db, int? commodityId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no commodity ID provided
            if (commodityId == null)
                return new List<CommodityDto>();

            // Query commodities for the specified commodity ID using read-only tracking
            var entity = await db.Set<Label.Commodity>()
                .AsNoTracking()
                .Where(e => e.CommodityID == commodityId)
                .ToListAsync();

            // Return empty list if no commodities found
            if (entity == null || !entity.Any())
                return new List<CommodityDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new CommodityDto { Commodity = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<CommodityDto>();
            #endregion
        }

        #endregion

        #region Packaging Hierarchy Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of packaging level DTOs for a specified product, including packaging 
        /// hierarchy data and package-level characteristics.
        /// </summary>
        /// <param name="db">The database context for querying packaging level entities.</param>
        /// <param name="productID">The product identifier to filter packaging levels.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of PackagingLevelDto objects with encrypted IDs, packaging hierarchy data, and characteristics.</returns>
        /// <seealso cref="Label.PackagingLevel"/>
        /// <seealso cref="Label.ProductEvent"/>
        /// <seealso cref="Label.PackagingHierarchy"/>
        /// <seealso cref="Label.MarketingStatus"/>
        /// <seealso cref="Label.Characteristic"/>
        /// <seealso cref="PackagingLevelDto"/>
        /// <seealso cref="ProductEventDto"/>
        /// <seealso cref="PackagingHierarchyDto"/>
        /// <seealso cref="MarketingStatusDto"/>
        /// <seealso cref="CharacteristicDto"/>
        /// <seealso cref="buildPackagingHierarchyDtoAsync"/>
        /// <seealso cref="buildProductEventsAsync"/>
        /// <seealso cref="buildPackageMarketingStatusesAsync"/>
        /// <seealso cref="buildCharacteristicsDtoAsync"/>
        /// <remarks>
        /// Enhanced to include package-level characteristics as specified in _Packaging.cshtml template.
        /// Package-level characteristics may include container type, labeling information, or other
        /// package-specific properties that differ from product-level characteristics.
        /// </remarks>
        /// <example>
        /// <code>
        /// var packagingLevels = await buildPackagingLevelsAsync(dbContext, 123, "secretKey", logger);
        /// </code>
        /// </example>
        private static async Task<List<PackagingLevelDto>> buildPackagingLevelsAsync(
            ApplicationDbContext db,
            int? productID,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            #region validation
            // Return empty list if no product ID is provided
            if (productID == null) return new List<PackagingLevelDto>();
            #endregion

            #region query execution
            var dtos = new List<PackagingLevelDto>();
            List<PackageIdentifierDto> packageIdentifierDtos = new List<PackageIdentifierDto>();

            // Query packaging levels for the specified product
            var items = await db.Set<Label.PackagingLevel>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            if (items == null || !items.Any())
                return new List<PackagingLevelDto>();
            #endregion

            #region dto building

            // Build packaging level DTOs with all associated data
            foreach (var item in items)
            {
                #region nested collections

                // Fetch package identifiers for this packaging level
                var packageIdentifier = await db.Set<Label.PackageIdentifier>()
                    .AsNoTracking()
                    .Where(e => e.PackagingLevelID == item.PackagingLevelID)
                    .ToListAsync();

                packageIdentifierDtos = new List<PackageIdentifierDto>();

                // Build packaging hierarchy, events, and marketing statuses
                var pack = await buildPackagingHierarchyDtoAsync(db, item.PackagingLevelID, pkSecret, logger);
                var events = await buildProductEventsAsync(db, item.PackagingLevelID, pkSecret, logger);
                var marketingStatuses = await buildPackageMarketingStatusesAsync(db, item.PackagingLevelID, pkSecret, logger);

                // Build package-level characteristics for this packaging level
                var characteristics = await buildCharacteristicsDtoAsync(db, null, pkSecret, logger, null, item.PackagingLevelID);

                #endregion

                #region package identifiers
                // Process package identifiers if present
                if (packageIdentifier != null && packageIdentifier.Any())
                {
                    foreach (var pk in packageIdentifier)
                    {
                        var itemPk = await buildPackageIdentifierDtoAsync(db, pk.PackageIdentifierID, pkSecret, logger);

                        if (itemPk == null)
                            continue;

                        packageIdentifierDtos.Add(itemPk);
                    }
                }

                #endregion

                #region dto assembly

                // Assemble complete packaging level DTO with all collections
                dtos.Add(new PackagingLevelDto
                {
                    PackagingLevel = item.ToEntityWithEncryptedId(pkSecret, logger),
                    PackagingHierarchy = pack,
                    ProductEvents = events,
                    MarketingStatuses = marketingStatuses,
                    PackageIdentifiers = packageIdentifierDtos,
                    Characteristics = characteristics
                });

                #endregion
            }
            #endregion

            // Return completed DTOs with encrypted IDs
            return dtos ?? new List<PackagingLevelDto>();

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
        private static async Task<List<PackagingLevelDto>> buildPackagingLevelsDtoAsync(ApplicationDbContext db, int? productInstanceID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product instance ID is provided
            if (productInstanceID == null)
                return new List<PackagingLevelDto>();

            // Query packaging levels for the specified product instance
            var entity = await db.Set<Label.PackagingLevel>()
                .AsNoTracking()
                .Where(e => e.ProductInstanceID == productInstanceID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<PackagingLevelDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new PackagingLevelDto
            {
                PackagingLevel = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<PackagingLevelDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves marketing status records for a specified packaging 
        /// level ID and transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="packagingLevelID">The unique identifier of the packaging level to find marketing statuses for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of MarketingStatusDto objects representing the marketing statuses, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: PackagingLevelDto > MarketingStatusDto
        /// Marketing statuses track the regulatory and commercial status of packaging levels in various markets.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var marketingStatuses = await buildPackageMarketingStatusesAsync(dbContext, 396, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.MarketingStatus"/>
        /// <seealso cref="MarketingStatusDto"/>
        private static async Task<List<MarketingStatusDto>> buildPackageMarketingStatusesAsync(ApplicationDbContext db, int? packagingLevelID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no packaging level ID provided
            if (packagingLevelID == null)
                return new List<MarketingStatusDto>();

            // Query marketing statuses for the specified packaging level using read-only tracking
            var items = await db.Set<Label.MarketingStatus>()
                .AsNoTracking()
                .Where(e => e.PackagingLevelID == packagingLevelID)
                .ToListAsync();

            // Return empty list if no marketing statuses found
            if (items == null || !items.Any())
                return new List<MarketingStatusDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return items
                .Select(item => new MarketingStatusDto { MarketingStatus = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<MarketingStatusDto>();
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
        private static async Task<List<PackagingHierarchyDto>> buildPackagingHierarchyDtoAsync(ApplicationDbContext db, int? outerPackagingLevelID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no outer packaging level ID is provided
            if (outerPackagingLevelID == null)
                return new List<PackagingHierarchyDto>();

            // Query packaging hierarchies for the specified outer packaging level
            var entity = await db.Set<Label.PackagingHierarchy>()
                .AsNoTracking()
                .Where(e => e.OuterPackagingLevelID == outerPackagingLevelID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return new List<PackagingHierarchyDto>();

            var dtos = new List<PackagingHierarchyDto>();

            // Build each hierarchy DTO with its child packaging level
            foreach (var hierarchy in entity)
            {
                var dto = new PackagingHierarchyDto
                {
                    PackagingHierarchy = hierarchy.ToEntityWithEncryptedId(pkSecret, logger)
                };

                // Build the child packaging level if InnerPackagingLevelID exists       
                if (hierarchy.InnerPackagingLevelID != null)
                {
                    var innerPackagingLevels = await buildInnerPackagingLevelsDtoAsync(db, hierarchy.InnerPackagingLevelID, pkSecret, logger);
                    dto.ChildPackagingLevel = innerPackagingLevels?.FirstOrDefault();
                }

                dtos.Add(dto);
            }

            return dtos;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of packaging level DTOs for a specified packaging level id.
        /// Helper method for retrieving packaging levels associated with package hierarchy.
        /// </summary>
        /// <param name="db">The database context for querying packaging level entities.</param>
        /// <param name="packingLevelId">The packing hierarchy identifier to filter packaging levels.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of PackagingLevelDto objects, or null if no product instance ID provided or no entities found.</returns>
        /// <seealso cref="Label.PackagingLevel"/>
        /// <seealso cref="PackagingLevelDto"/>
        private static async Task<List<PackagingLevelDto>> buildInnerPackagingLevelsDtoAsync(ApplicationDbContext db, int? packingLevelId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product instance ID is provided
            if (packingLevelId == null)
                return new List<PackagingLevelDto>();

            // Query packaging levels for the specified product instance
            var entity = await db.Set<Label.PackagingLevel>()
                .AsNoTracking()
                .Where(e => e.PackagingLevelID == packingLevelId)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<PackagingLevelDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new PackagingLevelDto
            {
                PackagingLevel = entity.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<PackagingLevelDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores product events like distribution or return quantities. Retrieves 
        /// product event records for a specified packaging level ID and transforms 
        /// them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="packagingLevelID">The unique identifier of the packaging level to find product events for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of ProductEventDto objects representing the product events, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: PackagingLevelDto > ProductEventDto
        /// Product events include activities like distribution or return quantities associated with packaging levels.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var productEvents = await buildProductEventsAsync(dbContext, 753, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ProductEvent"/>
        /// <seealso cref="ProductEventDto"/>
        private static async Task<List<ProductEventDto>> buildProductEventsAsync(ApplicationDbContext db, int? packagingLevelID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no packaging level ID provided
            if (packagingLevelID == null)
                return new List<ProductEventDto>();

            // Query product events for the specified packaging level using read-only tracking
            var entity = await db.Set<Label.ProductEvent>()
                .AsNoTracking()
                .Where(e => e.PackagingLevelID == packagingLevelID)
                .ToListAsync();

            // Return empty list if no product events found
            if (entity == null || !entity.Any())
                return new List<ProductEventDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entity
                .Select(e => new ProductEventDto { ProductEvent = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<ProductEventDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a package identifier DTO for a specified packaging level ID.
        /// </summary>
        /// <param name="db">The database context for querying package identifier entities.</param>
        /// <param name="packagingLevelID">The packaging level identifier to retrieve package identifier for.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <param name="sectionId">OPTIONAL for indexing files with compliance actions</param>
        /// <returns>A PackageIdentifierDto object with encrypted ID, or null if not found.</returns>
        /// <seealso cref="Label.PackageIdentifier"/>
        /// <seealso cref="PackageIdentifierDto"/>
        private static async Task<PackageIdentifierDto?> buildPackageIdentifierDtoAsync(ApplicationDbContext db,
            int? packagingLevelID,
            string pkSecret,
            ILogger logger,
            int? sectionId = null)
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

            //  Build ComplianceActions for this PackageIdentifier
            var complianceActions = await buildComplianceActionsForPackageAsync(
                db,
                entity.PackageIdentifierID,
                pkSecret,
                logger,
                sectionId);

            // Transform entity to DTO with encrypted ID
            return new PackageIdentifierDto
            {
                PackageIdentifier = entity.ToEntityWithEncryptedId(pkSecret, logger),
                ComplianceActions = complianceActions
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Asynchronously builds a list of compliance action DTOs for a specified package identifier.
        /// Retrieves compliance actions from the database and converts them to encrypted DTOs.
        /// </summary>
        /// <param name="db">The application database context for data access</param>
        /// <param name="packageIdentifierId">The unique identifier of the package to retrieve compliance actions for. If null, returns an empty list.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs in the response DTOs</param>
        /// <param name="logger">The logger instance for recording any errors or information during processing</param>
        /// <param name="sectionId">OPTIONAL used for spl indexing files for compliance actions</param>
        /// <returns>A list of ComplianceActionDto objects with encrypted IDs, or an empty list if no package identifier is provided</returns>
        /// <remarks>
        /// This method uses AsNoTracking() for better performance when the entities won't be modified.
        /// The method handles null package identifiers gracefully by returning an empty collection.
        /// </remarks>
        /// <example>
        /// <code>
        /// var complianceActions = await buildComplianceActionsForPackageAsync(dbContext, 123, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.ComplianceAction"/>
        /// <seealso cref="ComplianceActionDto"/>
        /// <seealso cref="ApplicationDbContext"/>
        private static async Task<List<ComplianceActionDto>> buildComplianceActionsForPackageAsync(
            ApplicationDbContext db,
            int? packageIdentifierId,
            string pkSecret,
            ILogger logger,
            int? sectionId = null)
        {
            #region implementation

            #region validation
            // Return empty list immediately if no package identifier provided
            if (packageIdentifierId == null)
                return new List<ComplianceActionDto>();
            #endregion

            #region data retrieval
            // Query compliance actions for the specified package identifier
            // Using AsNoTracking for performance since we're not modifying entities
            var query = db.Set<Label.ComplianceAction>()
                .AsNoTracking()
                .Where(e => e.PackageIdentifierID == packageIdentifierId);

            // Add section filter if provided
            if (sectionId != null)
            {
                query = query.Where(e => e.SectionID == sectionId);
            }

            var items = await query.ToListAsync();
            #endregion

            #region dto conversion
            // Transform database entities to DTOs with encrypted IDs
            return items
                .Select(item => new ComplianceActionDto
                {
                    // Convert entity to DTO with encrypted ID using the provided secret
                    ComplianceAction = item.ToEntityWithEncryptedId(pkSecret, logger)
                })
                .ToList();
            #endregion

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds PackageIdentifier DTOs directly for a product, bypassing Characteristics.
        /// Used for indexing documents and other cases where PackageIdentifiers exist 
        /// without going through the Characteristic → PackagingLevel path.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productID">The product ID to find package identifiers for.</param>
        /// <param name="sectionId">The section ID for context when building compliance actions.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of PackageIdentifier DTOs with ComplianceActions.</returns>
        private static async Task<List<PackageIdentifierDto>> buildDirectPackageIdentifiersAsync(
            ApplicationDbContext db, int? productID, int? sectionId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productID == null) return new List<PackageIdentifierDto>();

            // Find PackageIdentifiers through join with PackagingLevel → Product relationship
            var packageIdentifiers = await (from pi in db.Set<Label.PackageIdentifier>()
                                            join pl in db.Set<Label.PackagingLevel>() on pi.PackagingLevelID equals pl.PackagingLevelID
                                            where pl.ProductID == productID
                                            select pi)
                .AsNoTracking()
                .ToListAsync();

            var dtos = new List<PackageIdentifierDto>();

            foreach (var pkgId in packageIdentifiers)
            {
                // Build ComplianceActions for this PackageIdentifier with section context
                var complianceActions = await buildComplianceActionsForPackageAsync(
                    db, pkgId.PackageIdentifierID, pkSecret, logger, sectionId);

                dtos.Add(new PackageIdentifierDto
                {
                    PackageIdentifier = pkgId.ToEntityWithEncryptedId(pkSecret, logger),
                    ComplianceActions = complianceActions
                });
            }

            return dtos;
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
        private static async Task<OrganizationDto?> buildOrganizationAsync(ApplicationDbContext db,
            int? organizationId, string pkSecret, ILogger logger)
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
                Holders = holders

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
        private static async Task<ContactPartyDto?> buildContactPartyAsync(ApplicationDbContext db, int? contactPartyId, string pkSecret, ILogger logger)
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
        private static async Task<ContactPersonDto?> buildContactPersonAsync(ApplicationDbContext db, int? contactPersonId, string pkSecret, ILogger logger)
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
        private static async Task<AddressDto?> buildAddressAsync(ApplicationDbContext db, int? addressId, string pkSecret, ILogger logger)
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
        private static async Task<List<ContactPartyTelecomDto>> buildContactPartyTelecomsAsync(ApplicationDbContext db, int? contactPartyId, string pkSecret, ILogger logger)
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
        private static async Task<List<OrganizationTelecomDto>> buildOrganizationTelecomsAsync(ApplicationDbContext db, int? organizationId, string pkSecret, ILogger logger)
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
        private static async Task<List<OrganizationIdentifierDto>> buildOrganizationIdentifiersAsync(ApplicationDbContext db, int? organizationId, string pkSecret, ILogger logger)
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
        /// <param name="territorialAuthId">The organization identifier used as governing agency organization ID to filter territorial authorities.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A list of TerritorialAuthorityDto objects, or null if no organization ID provided or no entities found.</returns>
        /// <seealso cref="Label.TerritorialAuthority"/>
        /// <seealso cref="TerritorialAuthorityDto"/>
        private static async Task<List<TerritorialAuthorityDto>> buildTerritorialAuthoritiesDtoAsync(ApplicationDbContext db, int? territorialAuthId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no organization ID is provided
            if (territorialAuthId == null)
                return new List<TerritorialAuthorityDto>();

            // Query territorial authorities for the specified governing agency organization
            var entity = await db.Set<Label.TerritorialAuthority>()
                .AsNoTracking()
                .Where(e => e.TerritorialAuthorityID == territorialAuthId)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<TerritorialAuthorityDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity
                .Select(e => new TerritorialAuthorityDto { TerritorialAuthority = e.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<TerritorialAuthorityDto>();
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
        private static async Task<List<HolderDto>> buildHoldersDtoAsync(ApplicationDbContext db, int? organizationID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no organization ID is provided
            if (organizationID == null)
                return new List<HolderDto>();

            // Query holders for the specified holder organization
            var entity = await db.Set<Label.Holder>()
                .AsNoTracking()
                .Where(e => e.HolderOrganizationID == organizationID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null || !entity.Any())
                return new List<HolderDto>();

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new HolderDto
            {
                Holder = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList() ?? new List<HolderDto>();
            #endregion
        }
        #endregion

        #region Document Relationship Child Builders
        /**************************************************************/
        /// <summary>
        /// Builds a list of BusinessOperation DTOs for the specified document 
        /// relationship. Retrieves business operation details for establishments
        /// or labelers linked through document relationships and enriches them
        /// with licenses and qualifiers, then transforms them into DTOs with 
        /// encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="docRelId">The unique identifier of the document relationship to find business operations for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of BusinessOperationDto objects representing the business operations with their associated data, or an empty list if none found</returns>
        /// <remarks>
        /// Each business operation is enriched with its licenses and business operation qualifiers.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var businessOperations = await buildBusinessOperationsAsync(dbContext, 159, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.BusinessOperation"/>
        /// <seealso cref="Label.License"/>
        /// <seealso cref="Label.BusinessOperationQualifier"/>
        /// <seealso cref="LicenseDto"/>
        /// <seealso cref="BusinessOperationQualifier"/>
        /// <seealso cref="BusinessOperationDto"/>
        /// <seealso cref="buildLicensesAsync"/>
        /// <seealso cref="buildBusinessOperationQualifiersAsync"/>
        private static async Task<List<BusinessOperationDto>> buildBusinessOperationsAsync(ApplicationDbContext db, int? docRelId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no document relationship ID provided
            if (docRelId == null)
                return new List<BusinessOperationDto>();

            var dtos = new List<BusinessOperationDto>();

            // Query business operations for the specified document relationship using read-only tracking
            var entities = await db.Set<Label.BusinessOperation>()
                .AsNoTracking()
                .Where(e => e.DocumentRelationshipID == docRelId)
                .ToListAsync();

            // Return empty list if no business operations found
            if (entities == null || !entities.Any())
                return new List<BusinessOperationDto>();

            // Process each business operation and build associated data
            foreach (var e in entities)
            {
                // Skip entities without valid IDs
                if (e.BusinessOperationID == null)
                    continue;

                // Build licenses and business operation qualifiers for each business operation
                var licenses = await buildLicensesAsync(db, e.BusinessOperationID, pkSecret, logger);
                var qualifiers = await buildBusinessOperationQualifiersAsync(db, e.BusinessOperationID, pkSecret, logger);

                // Create business operation DTO with encrypted ID and associated data
                dtos.Add(new BusinessOperationDto
                {
                    BusinessOperation = e.ToEntityWithEncryptedId(pkSecret, logger),
                    Licenses = licenses,
                    BusinessOperationQualifiers = qualifiers
                });
            }

            // Return processed business operations with associated data, ensuring non-null result
            return dtos ?? new List<BusinessOperationDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds licenses to the business operation. Retrieves license 
        /// records for a specified business operation ID and enriches 
        /// them with disciplinary actions, then transforms them into DTOs 
        /// with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="businessOperationId">The unique identifier of the business operation to find licenses for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of LicenseDto objects representing the licenses with their associated data, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: BusinessOperationDto > LicenseDto
        /// Each license is enriched with its disciplinary actions.
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var licenses = await buildLicensesAsync(dbContext, 357, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.License"/>
        /// <seealso cref="Label.DisciplinaryAction"/>
        /// <seealso cref="LicenseDto"/>
        /// <seealso cref="DisciplinaryActionDto"/>"/>
        /// <seealso cref="buildDisciplinaryActionsAsync"/>
        private static async Task<List<LicenseDto>> buildLicensesAsync(ApplicationDbContext db, int? businessOperationId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no business operation ID provided
            if (businessOperationId == null)
                return new List<LicenseDto>();

            var dtos = new List<LicenseDto>();

            // Query licenses for the specified business operation using read-only tracking
            var entities = await db.Set<Label.License>()
                .AsNoTracking()
                .Where(e => e.BusinessOperationID == businessOperationId)
                .ToListAsync();

            // Return empty list if no licenses found
            if (entities == null || !entities.Any())
                return new List<LicenseDto>();

            // Process each license entity and build its DTO with associated data
            foreach (var e in entities)
            {
                // Skip entities without valid IDs
                if (e.LicenseID == null)
                    continue;

                // Build disciplinary actions for this license
                var disciplinaryActions = await buildDisciplinaryActionsAsync(db, e.LicenseID, pkSecret, logger);

                var govAuthorities = await buildTerritorialAuthoritiesDtoAsync(db, e.TerritorialAuthorityID, pkSecret, logger)
                    ?? new List<TerritorialAuthorityDto>();

                // Create license DTO with encrypted ID and associated data
                dtos.Add(new LicenseDto
                {
                    License = e.ToEntityWithEncryptedId(pkSecret, logger),
                    DisciplinaryActions = disciplinaryActions,
                    TerritorialAuthorities = govAuthorities
                });
            }

            // Return processed licenses with associated data, ensuring non-null result
            return dtos ?? new List<LicenseDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds DisciplinaryAction DTOs for the specified license. Retrieves 
        /// disciplinary action records for a specified license ID and 
        /// transforms them into DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="licenseId">The unique identifier of the license to find disciplinary actions for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of DisciplinaryActionDto objects representing the disciplinary actions, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: BusinessOperationDto > LicenseDto > DisciplinaryActionDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var disciplinaryActions = await buildDisciplinaryActionsAsync(dbContext, 468, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.DisciplinaryAction"/>
        /// <seealso cref="DisciplinaryActionDto"/>
        private static async Task<List<DisciplinaryActionDto>> buildDisciplinaryActionsAsync(ApplicationDbContext db, int? licenseId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no license ID provided
            if (licenseId == null)
                return new List<DisciplinaryActionDto>();

            // Query disciplinary actions for the specified license using read-only tracking
            var entities = await db.Set<Label.DisciplinaryAction>()
                .AsNoTracking()
                .Where(e => e.LicenseID == licenseId)
                .ToListAsync();

            // Return empty list if no disciplinary actions found
            if (entities == null || !entities.Any())
                return new List<DisciplinaryActionDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entities
                .Select(item => new DisciplinaryActionDto { DisciplinaryAction = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<DisciplinaryActionDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds BusinessOperationQualifier DTOs for the specified business 
        /// operation. Retrieves business operation qualifier records 
        /// for a specified business operation ID and transforms them into
        /// DTOs with encrypted identifiers.
        /// </summary>
        /// <param name="db">The application database context for data access operations</param>
        /// <param name="businessOperationId">The unique identifier of the business operation to find qualifiers for. Returns empty list if null</param>
        /// <param name="pkSecret">The private key secret used for encrypting entity identifiers in the returned DTOs</param>
        /// <param name="logger">The logger instance for recording operations and potential errors during processing</param>
        /// <returns>A list of BusinessOperationQualifierDto objects representing the business operation qualifiers, or an empty list if none found</returns>
        /// <remarks>
        /// This method follows the data flow: BusinessOperationDto > BusinessOperationQualifierDto
        /// The method uses AsNoTracking() for read-only operations to improve performance.
        /// All returned DTOs contain encrypted IDs for security purposes.
        /// </remarks>
        /// <example>
        /// <code>
        /// var qualifiers = await buildBusinessOperationQualifiersAsync(dbContext, 357, secretKey, logger);
        /// </code>
        /// </example>
        /// <seealso cref="Label.BusinessOperationQualifier"/>
        /// <seealso cref="BusinessOperationQualifierDto"/>
        private static async Task<List<BusinessOperationQualifierDto>> buildBusinessOperationQualifiersAsync(ApplicationDbContext db, int? businessOperationId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Early return if no business operation ID provided
            if (businessOperationId == null)
                return new List<BusinessOperationQualifierDto>();

            // Query business operation qualifiers for the specified business operation using read-only tracking
            var entities = await db.Set<Label.BusinessOperationQualifier>()
                .AsNoTracking()
                .Where(e => e.BusinessOperationID == businessOperationId)
                .ToListAsync();

            // Return empty list if no business operation qualifiers found
            if (entities == null || !entities.Any())
                return new List<BusinessOperationQualifierDto>();

            // Transform entities to DTOs with encrypted IDs for security, ensuring non-null result
            return entities
                .Select(item => new BusinessOperationQualifierDto { BusinessOperationQualifier = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<BusinessOperationQualifierDto>();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of CertificationProductLink DTOs for the specified 
        /// document relationship. Retrieves links between establishments 
        /// and products being certified in Blanket No Changes Certification documents.
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

            if (items == null || !items.Any())
                return new List<CertificationProductLinkDto>();

            // Transform entities to DTOs with encrypted IDs
            return items.Select(item => new CertificationProductLinkDto { CertificationProductLink = item.ToEntityWithEncryptedId(pkSecret, logger) }).ToList() ?? new List<CertificationProductLinkDto>();
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

            if (items == null || !items.Any())
                return new List<ComplianceActionDto>();

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

            List<FacilityProductLinkDto> retSet = new List<FacilityProductLinkDto>();

            // Query facility product links for the specified document relationship
            var items = await db.Set<Label.FacilityProductLink>()
                .AsNoTracking()
                .Where(e => e.DocumentRelationshipID == docRelId)
                .ToListAsync();

            if (!items.Any())
                return new List<FacilityProductLinkDto>();

            // Use HashSet for O(1) lookup performance instead of O(n) with List.Contains
            var productNamesSet = items.Select(x => x.ProductName).ToHashSet();

            // Get distinct ProductIDs to avoid duplicate calls
            var distinctProductIds = items
                .Where(i => i != null && i.ProductID != null)
                .Select(i => i.ProductID)
                .Distinct()
                .ToList();

            // prod identifiers batch fetch
            var allProductIdentifiers = await buildProductIdentifiersBatchAsync(db, distinctProductIds, pkSecret, logger);

            // Filter product identifiers efficiently using HashSet
            var filteredProductIdentifiers = allProductIdentifiers
                .Where(pi => pi?.IdentifierValue != null
                    && productNamesSet.Contains(pi.IdentifierValue))
                .ToList();

            // Map each FacilityProductLink to its corresponding ProductIdentifier
            foreach (var item in items)
            {
                // Get the matching ProductIdentifier for this FacilityProductLink
                var prodIdentifier = filteredProductIdentifiers.FirstOrDefault(pi => pi?.IdentifierValue == item.ProductName);

                // Create and add the DTO to the result set
                retSet.Add(new FacilityProductLinkDto
                {
                    FacilityProductLink = item.ToEntityWithEncryptedId(pkSecret, logger),
                    ProductIdentifier = prodIdentifier
                });

            }

            // return processed facility product links with associated product identifiers
            return retSet;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Helper method to batch fetch ProductIdentifier entities for multiple ProductIDs.
        /// </summary>
        /// <param name="db"></param>
        /// <param name="productIds"></param>
        /// <param name="pkSecret"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        private static async Task<List<ProductIdentifierDto>> buildProductIdentifiersBatchAsync(
            DbContext db,
            List<int?> productIds,
            string pkSecret,
            ILogger logger)
        {

            #region implementation
            // Single query to get all product identifiers for multiple ProductIDs
            return await db.Set<ProductIdentifier>()
                 .AsNoTracking()
                 .Where(pi => pi != null && pi.ProductID != null && productIds.Contains(pi.ProductID))
                 .Select(pi => new ProductIdentifierDto { ProductIdentifier = pi.ToEntityWithEncryptedId(pkSecret, logger) })
                 .ToListAsync();
            #endregion
        }
        #endregion
    }
}