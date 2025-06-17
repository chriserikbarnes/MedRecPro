using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
                    LegalAuthenticators = authenticators
                });
            }
            return docDtos;
            #endregion
        }

        #region Document Children Builders

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

        #endregion

        #region StructuredBody & Section Builders

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

        /**************************************************************/
        /// <summary>
        /// Builds a list of Section DTOs for the specified structured body with all nested collections.
        /// Constructs comprehensive section data including products, highlights, media, substances, and various specialized content.
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
                var products = await buildProductsAsync(db, section.SectionID, pkSecret, logger);

                var parents = await buildParentSectionHierarchyDtoAsync(db, section.SectionID, pkSecret, logger) ?? new List<SectionHierarchyDto>();

                var children = await buildChildSectionHierarchyDtoAsync(db, section.SectionID, pkSecret, logger) ?? new List<SectionHierarchyDto>();

                var content = await buildSectionTextContentDtoAsync(db, section.SectionID, pkSecret, logger)
                    ?? new List<SectionTextContentDto>();

                var highlights = await buildSectionExcerptHighlightsAsync(db, section.SectionID, pkSecret, logger);

                var media = await buildObservationMediaAsync(db, section.SectionID, pkSecret, logger);

                var identifiedSubstances = await buildIdentifiedSubstancesAsync(db, section.SectionID, pkSecret, logger);

                var productConcepts = await buildProductConceptsAsync(db, section.SectionID, pkSecret, logger);

                var interactionIssues = await buildInteractionIssuesAsync(db, section.SectionID, pkSecret, logger);

                var billingUnitIndexes = await buildBillingUnitIndexesAsync(db, section.SectionID, pkSecret, logger);

                var warningLetterInfos = await buildWarningLetterProductInfosAsync(db, section.SectionID, pkSecret, logger);

                var warningLetterDates = await buildWarningLetterDatesAsync(db, section.SectionID, pkSecret, logger);

                var protocols = await buildProtocolsAsync(db, section.SectionID, pkSecret, logger);

                var remsMaterials = await buildREMSMaterialsAsync(db, section.SectionID, pkSecret, logger);

                var remsResources = await buildREMSElectronicResourcesAsync(db, section.SectionID, pkSecret, logger);

                // Assemble complete section DTO with all nested data
                sectionDtos.Add(new SectionDto
                {
                    Section = section.ToEntityWithEncryptedId(pkSecret, logger),
                    ParentSectionHierarchies = parents,
                    ChildSectionHierarchies = children,
                    TextContents = content,
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
            #endregion
        }

        #endregion

        #region Section Children Builders

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

            // Query product concepts for the specified section
            var items = await db.Set<Label.ProductConcept>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ProductConceptDto { ProductConcept = item.ToEntityWithEncryptedId(pkSecret, logger) })
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

            // Query interaction issues for the specified section
            var items = await db.Set<Label.InteractionIssue>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new InteractionIssueDto { InteractionIssue = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
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

            // Query protocols for the specified section
            var items = await db.Set<Label.Protocol>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ProtocolDto { Protocol = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
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

        #region Product Builders

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

            var productDtos = new List<ProductDto>();

            // For each product, build all its nested collections
            foreach (var product in products)
            {
                // Build all child collections for this product
                var genericMeds = await buildGenericMedicinesAsync(db, product.ProductID, pkSecret, logger);

                var productIds = await buildProductIdentifiersAsync(db, product.ProductID, pkSecret, logger);

                var productRoutes = await buildProductRouteOfAdministrationsAsync(db, product.ProductID, pkSecret, logger);

                var webLinks = await buildProductWebLinksAsync(db, product.ProductID, pkSecret, logger);

                var businessOpLinks = await buildBusinessOperationProductLinksAsync(db, product.ProductID, pkSecret, logger);

                var respPersonLinks = await buildResponsiblePersonLinksAsync(db, product.ProductID, pkSecret, logger);

                var productInstances = await buildProductInstancesAsync(db, product.ProductID, pkSecret, logger);

                var ingredients = await buildIngredientsAsync(db, product.ProductID, pkSecret, logger);

                var packageLevels = await buildPackagingLevelsAsync(db, product.ProductID, pkSecret, logger);

                var ingredientInstances = await buildProductIngredientInstancesAsync(db, product.ProductID, pkSecret, logger);

                var parentLots = await buildFillLotHierarchyDtoAsync(db, product.ProductID, pkSecret, logger) ?? new List<LotHierarchyDto>();

                var childLots = await buildLabelLotHierarchyDtoAsync(db, product.ProductID, pkSecret, logger)
                    ?? new List<LotHierarchyDto>();

                var characteristics = await buildCharacteristicsDtoAsync(db, product.ProductID, pkSecret, logger);

                var productParts = await buildProductPartsDtoAsync(db, product.ProductID, pkSecret, logger);

                var marketingCats = await buildMarketingCategoriesDtoAsync(
                    db,
                    product.ProductID,
                    pkSecret,
                    logger) ?? new List<MarketingCategoryDto>();


                // Assemble complete product DTO with all nested data
                productDtos.Add(new ProductDto
                {
                    Product = product.ToEntityWithEncryptedId(pkSecret, logger),
                    ProductParts = productParts ?? new List<ProductPartDto>(),
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
                    Characteristics = characteristics ?? new List<CharacteristicDto>(),
                    ProductInstances = productInstances,
                    Ingredients = ingredients,
                    IngredientInstances = ingredientInstances ?? new List<IngredientInstanceDto>()
                });
            }
            return productDtos;
            #endregion
        }

        #endregion

        #region Product Children Builders
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
        private static async Task<List<IngredientDto>> buildIngredientsAsync(
          ApplicationDbContext db,
          int? productId,
          string pkSecret,
          ILogger logger)
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
        private static async Task<IngredientSubstanceDto?> buildIngredientSubstanceAsync(
            ApplicationDbContext db,
            int? ingredientSubstanceId,
            string pkSecret,
            ILogger logger)
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
        private static async Task<List<IngredientInstanceDto>?> buildIngredientInstancesAsync(
            ApplicationDbContext db,
            int? ingredientSubstanceId,
            string pkSecret,
            ILogger logger)
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
        private static async Task<List<IngredientInstanceDto>?> buildProductIngredientInstancesAsync(
           ApplicationDbContext db,
           int? productID,
           string pkSecret,
           ILogger logger)
        {
            #region implementation
            // Return null if no ingredientSubstanceId provided
            if (productID == null)
                return null;

            // Query all IngredientInstance rows for this substance with no change tracking
            var entity = await db.Set<Label.IngredientInstance>()
                .AsNoTracking()
                .Where(ii => ii.FillLotInstanceID == productID)
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
                    LotIdentifier = await buildLotIdentifierDtoAsync(db, item.LotIdentifierID, pkSecret, logger)
                });
            }

            return ingredientInstances;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a list of GenericMedicine DTOs for the specified product.
        /// Retrieves non-proprietary medicine names associated with a product.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="productId">The product ID to find generic medicines for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of GenericMedicine DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.GenericMedicine"/>
        private static async Task<List<GenericMedicineDto>> buildGenericMedicinesAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productId == null) return new List<GenericMedicineDto>();

            // Query generic medicines for the specified product
            var items = await db.Set<Label.GenericMedicine>()
                .AsNoTracking()
                .Where(e => e.ProductID == productId)
                .ToListAsync();

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
        /// <param name="productId">The product ID to find product identifiers for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductIdentifier DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ProductIdentifier"/>
        private static async Task<List<ProductIdentifierDto>> buildProductIdentifiersAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productId == null) return new List<ProductIdentifierDto>();

            // Query product identifiers for the specified product
            var items = await db.Set<Label.ProductIdentifier>()
                .AsNoTracking()
                .Where(e => e.ProductID == productId)
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
        /// <param name="productId">The product ID to find routes of administration for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductRouteOfAdministration DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ProductRouteOfAdministration"/>
        private static async Task<List<ProductRouteOfAdministrationDto>> buildProductRouteOfAdministrationsAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productId == null) return new List<ProductRouteOfAdministrationDto>();

            // Query product routes of administration for the specified product
            var items = await db.Set<Label.ProductRouteOfAdministration>()
                .AsNoTracking()
                .Where(e => e.ProductID == productId)
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
        /// <param name="productId">The product ID to find web links for.</param>
        /// <param name="pkSecret">Secret used for ID encryption.</param>
        /// <param name="logger">Logger instance for diagnostics.</param>
        /// <returns>List of ProductWebLink DTOs with encrypted IDs.</returns>
        /// <seealso cref="Label.ProductWebLink"/>
        private static async Task<List<ProductWebLinkDto>> buildProductWebLinksAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            if (productId == null) return new List<ProductWebLinkDto>();

            // Query product web links for the specified product
            var items = await db.Set<Label.ProductWebLink>()
                .AsNoTracking()
                .Where(e => e.ProductID == productId)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new ProductWebLinkDto { ProductWebLink = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList();
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

        #endregion

        #region DocumentRelationship Children Builders

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

        #region Miscellaneous Builders

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

            List<Dictionary<string, object?>> packagingHierarchyDtos = new List<Dictionary<string, object?>>();

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
                            packagingHierarchyDtos.Add(p.PackagingHierarchy);
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
        private static async Task<List<LotHierarchyDto>?> buildFillLotHierarchyDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no product ID is provided
            if (productID == null) return new List<LotHierarchyDto>();

            // Query lot hierarchies for the specified product instance as parent
            var items = await db.Set<Label.LotHierarchy>()
                .AsNoTracking()
                .Where(e => e.ParentInstanceID == productID)
                .ToListAsync();

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
        private static async Task<List<LotHierarchyDto>?> buildLabelLotHierarchyDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return empty list if no product ID is provided
            if (productID == null) return new List<LotHierarchyDto>();

            // Query lot hierarchies for the specified product instance as child
            var items = await db.Set<Label.LotHierarchy>()
                .AsNoTracking()
                .Where(e => e.ChildInstanceID == productID)
                .ToListAsync();

            // Transform entities to DTOs with encrypted IDs
            return items
                .Select(item => new LotHierarchyDto { LotHierarchy = item.ToEntityWithEncryptedId(pkSecret, logger) })
                .ToList() ?? new List<LotHierarchyDto>();
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
        private static async Task<List<CharacteristicDto>> buildCharacteristicsDtoAsync(
            ApplicationDbContext db,
            int? productID,
            string pkSecret,
            ILogger logger)
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

        /**************************************************************/
        /// <summary>
        /// Builds a product DTO for a specified product ID.
        /// Helper method to retrieve product entity by primary key.
        /// </summary>
        /// <param name="db">The database context for querying product entities.</param>
        /// <param name="productId">The product identifier to retrieve.</param>
        /// <param name="pkSecret">The secret key used for encrypting entity IDs.</param>
        /// <param name="logger">The logger instance for tracking operations.</param>
        /// <returns>A ProductDto object with encrypted ID, or null if not found.</returns>
        /// <seealso cref="Label.Product"/>
        /// <seealso cref="ProductDto"/>
        /// <remarks>
        /// For full hierarchy, consider calling buildProductsAsync if you need full population.
        /// </remarks>
        private static async Task<ProductDto?> buildProductDtoAsync(ApplicationDbContext db, int? productId, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product ID is provided
            if (productId == null)
                return null;

            // Query product for the specified product ID
            var entity = await db.Set<Label.Product>()
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.ProductID == productId);

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
        private static async Task<List<ProductPartDto>?> buildProductPartsDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product ID is provided
            if (productID == null)
                return null;

            // Query product parts for the specified kit product
            var entity = await db.Set<Label.ProductPart>()
                .AsNoTracking()
                .Where(e => e.KitProductID == productID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new ProductPartDto
            {
                ProductPart = e.ToEntityWithEncryptedId(pkSecret, logger)
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

            // Query section text content for the specified section
            var entity = await db.Set<Label.SectionTextContent>()
                .AsNoTracking()
                .Where(e => e.SectionID == sectionID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(e => new SectionTextContentDto
            {
                SectionTextContent = e.ToEntityWithEncryptedId(pkSecret, logger)
            }).ToList();
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
        private static async Task<List<MarketingCategoryDto>?> buildMarketingCategoriesDtoAsync(ApplicationDbContext db, int? productID, string pkSecret, ILogger logger)
        {
            #region implementation
            // Return null if no product ID is provided
            if (productID == null)
                return null;

            // Query marketing categories for the specified product
            var entity = await db.Set<Label.MarketingCategory>()
                .AsNoTracking()
                .Where(e => e.ProductID == productID)
                .ToListAsync();

            // Return null if no entities found
            if (entity == null)
                return null;

            // Transform entities to DTOs with encrypted IDs
            return entity.Select(entity => new MarketingCategoryDto
            {
                MarketingCategory = entity.ToEntityWithEncryptedId(pkSecret, logger)
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
    }
    #endregion
}