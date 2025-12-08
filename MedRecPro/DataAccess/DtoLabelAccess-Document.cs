
using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using static MedRecPro.Models.Label;
using Cached = MedRecPro.Helpers.PerformanceHelper;

namespace MedRecPro.DataAccess
{
    /// <summary>
    /// Provides helper methods for building Data Transfer Objects (DTOs) from SPL Label entities.
    /// Constructs complete hierarchical data structures representing medical product documents
    /// and their associated metadata, relationships, and compliance information.
    /// </summary>
    /// <seealso cref="Label"/>
    /// <seealso cref="DocumentDto"/>
    public static partial class DtoLabelAccess
    {
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

                var relationshipIdentifiers = await buildDocumentRelationshipIdentifiersAsync(db, rel.DocumentRelationshipID, pkSecret, logger);

                // Assemble complete relationship DTO with all nested data
                dtos.Add(new DocumentRelationshipDto
                {
                    DocumentRelationship = rel.ToEntityWithEncryptedId(pkSecret, logger),
                    BusinessOperations = businessOps,
                    CertificationProductLinks = certLinks,
                    ComplianceActions = complianceActions,
                    FacilityProductLinks = facilityLinks,
                    ChildOrganization = childOrg,
                    ParentOrganization = parentOrg,
                    RelationshipIdentifiers = relationshipIdentifiers
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

        /**************************************************************/
        /// <summary>
        /// Builds a list of DocumentRelationshipIdentifier DTOs for the specified document relationship.
        /// Retrieves the organization identifiers that were used at this specific hierarchy level
        /// in the original SPL document, enabling accurate XML rendering that matches the source structure.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="documentRelationshipId">The document relationship ID to find identifiers for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of DocumentRelationshipIdentifier DTOs with encrypted IDs and associated identifier data.</returns>
        /// <remarks>
        /// This method preserves the contextual information about which organization identifiers
        /// (e.g., DUNS numbers, FEI codes) appeared at which hierarchy level in the author section.
        /// This is critical for rendering SPL documents that match the original structure, where the
        /// same organization may have different identifiers at different hierarchy levels.
        /// Returns an empty list if documentRelationshipId is null or no identifiers are found.
        /// </remarks>
        /// <example>
        /// <code>
        /// var identifiers = await buildDocumentRelationshipIdentifiersAsync(dbContext, 456, "secret", logger);
        /// foreach (var identifier in identifiers)
        /// {
        ///     Console.WriteLine($"Identifier {identifier.OrganizationIdentifierID} at relationship {identifier.DocumentRelationshipID}");
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="Label.DocumentRelationshipIdentifier"/>
        /// <seealso cref="DocumentRelationshipIdentifierDto"/>
        /// <seealso cref="Label.OrganizationIdentifier"/>
        /// <seealso cref="Label.DocumentRelationship"/>
        private static async Task<List<DocumentRelationshipIdentifierDto>> buildDocumentRelationshipIdentifiersAsync(
            ApplicationDbContext db,
            int? documentRelationshipId,
            string pkSecret,
            ILogger logger)
        {
            #region implementation

            // Return empty list if no documentRelationshipId provided
            if (documentRelationshipId == null) return new List<DocumentRelationshipIdentifierDto>();

            // Fetch all DocumentRelationshipIdentifier entities for this relationship with no change tracking
            var items = await db.Set<Label.DocumentRelationshipIdentifier>()
                .AsNoTracking()
                .Where(e => e.DocumentRelationshipID == documentRelationshipId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new DocumentRelationshipIdentifierDto
                {
                    DocumentRelationshipIdentifier = item.ToEntityWithEncryptedId(pkSecret, logger)
                })
                .ToList();

            #endregion
        }
        #endregion
    }
}