
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
    }
}