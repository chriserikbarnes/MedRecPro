using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using MedRecPro.Data;
using MedRecPro.Helpers;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;
using MedRecPro.Models;
using System.Security.Cryptography;
using AngleSharp.Svg.Dom;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses the author element, finds or creates the representedOrganization,
    /// and links it to the document. Normalizes organization data to prevent duplicates.
    /// </summary>
    /// <remarks>
    /// This parser specifically handles the author section of SPL documents, extracting
    /// organization information from the representedOrganization element and creating
    /// appropriate database entities and relationships. It implements deduplication
    /// logic to prevent duplicate organizations in the database.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="Organization"/>
    /// <seealso cref="DocumentAuthor"/>
    public class AuthorSectionParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser, using the constant for Author element.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.SplConstants"/>
        public string SectionName => sc.E.Author;

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses the author section of an SPL document, extracting organization information
        /// and creating necessary database entities and relationships.
        /// </summary>
        /// <param name="element">The XElement representing the author section to parse.</param>
        /// <param name="context">The current parsing context containing document and service information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <example>
        /// <code>
        /// var parser = new AuthorSectionParser();
        /// var result = await parser.ParseAsync(authorElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Organizations created: {result.OrganizationsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the following operations:
        /// 1. Validates the document context exists
        /// 2. Extracts the representedOrganization element from the author structure
        /// 3. Gets or creates the organization entity in the database
        /// 4. Creates a DocumentAuthor link between the document and organization
        /// </remarks>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElement"/>
        public async Task<SplParseResult> ParseAsync(XElement element,
            SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();
            int orgID = 0;
            int docID = 0;

            // Validate context
            if (context == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context is null or logger is null.");
                return result;
            }

            // Validate that we have a valid document context to work with
            if (context.Document?.DocumentID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse author because no document context exists.");
                return result;
            }

            docID = context.Document.DocumentID.Value;

            reportProgress?.Invoke($"Starting Author XML Elements {context.FileNameInZip}");

            // Navigate to the organization element using the SPL structure constants
            // Path: author/assignedEntity/representedOrganization
            var authorOrgElement = element.GetSplElement(sc.E.AssignedEntity)
                ?.GetSplElement(sc.E.RepresentedOrganization);

            // If no organization element found, log warning and return successful result
            if (authorOrgElement == null)
            {
                context.Logger.LogWarning("No <{OrganizationElement}> found within <{AuthorElement}> for file {FileName}",
                    sc.E.RepresentedOrganization, sc.E.Author, context.FileNameInZip);
                return result;
            }

            try
            {
                // --- PARSE ORGANIZATION ---
                var (organization, orgCreated) = await getOrCreateOrganizationAsync(authorOrgElement, context);

                // Validate that we successfully obtained an organization
                if (organization?.OrganizationID == null)
                {
                    result.Success = false;
                    result.Errors.Add("Failed to get or create an organization for the author.");
                    return result;
                }

                // Set org id
                orgID = organization.OrganizationID.Value;

                // Log the organization creation or retrieval result
                if (orgCreated)
                {
                    result.OrganizationsCreated++;
                    context?.Logger?.LogInformation("Created new Organization (Author) '{OrgName}' with ID {OrganizationID}",
                        organization.OrganizationName, organization.OrganizationID);
                }
                else
                {
                    context.Logger.LogInformation("Found existing Organization (Author) '{OrgName}' with ID {OrganizationID}",
                        organization.OrganizationName, organization.OrganizationID);
                }

                // --- PARSE AUTHOR ---
                var (docAuthor, docAuthorCreated) = await getOrCreateDocumentAuthorAsync(
                    context!.Document.DocumentID.Value,
                    orgID,
                    "Labeler",
                    context);

                // Log the document author link creation if it was newly created
                if (docAuthorCreated)
                {
                    context.Logger.LogInformation("Created DocumentAuthor link for DocumentID {DocumentID} and OrganizationID {OrganizationID}",
                       docID, orgID);

                    reportProgress?.Invoke($"Completed Author XML Elements {context.FileNameInZip}");
                }

                // --- PARSE DOCUMENT RELATIONSHIP ---
                var relationshipsCount = await parseAndSaveDocumentRelationshipsAsync(
                    element, context, docID, orgID);
                result.OrganizationsCreated += relationshipsCount;

                // --- PARSE CONTACT PARTIES ---
                var (partiesCreated, telecomsCreated) = await parseAndSaveContactPartiesAsync(element, context, orgID);
                result.OrganizationAttributesCreated += partiesCreated;
                result.OrganizationAttributesCreated += telecomsCreated;

                // --- PARSE TELECOMS ---
                var telecomCt = await parseAndSaveOrganizationTelecomsAsync(authorOrgElement, orgID, context);
                result.OrganizationAttributesCreated += telecomCt;

                // --- PARSE ORGANIZATION IDENTIFIERS ---
                var identifiers = await getOrCreateOrganizationIdentifierAsync(authorOrgElement, orgID, context);
                result.OrganizationAttributesCreated += identifiers?.Count ?? 0;

                // --- PARSE ORGANIZATION NAMED ENTITIES ---
                var namedEntities = await getOrCreateNamedEntitiesAsync(authorOrgElement, orgID, context);
                result.OrganizationAttributesCreated += namedEntities?.Count ?? 0;

            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during parsing
                result.Success = false;
                result.Errors.Add($"Error parsing author: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <{AuthorElement}> element.", sc.E.Author);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates NamedEntity records for all [asNamedEntity] elements under orgElement.
        /// Handles DBA (Doing Business As) names per Section 2.1.9, 18.1.3, and 18.1.4.
        /// </summary>
        /// <param name="orgElement">The XElement representing [assignedOrganization] or similar.</param>
        /// <param name="organizationId">The parent OrganizationID.</param>
        /// <param name="context">Parsing context.</param>
        /// <returns>List of NamedEntity (both created and found).</returns>
        /// <seealso cref="NamedEntity"/>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<List<NamedEntity>> getOrCreateNamedEntitiesAsync(
            XElement orgElement,
            int organizationId,
            SplParseContext context)
        {
            #region implementation
            var entities = new List<NamedEntity>();

            // Validate required input parameters
            if (orgElement == null || organizationId <= 0)
                return entities;

            // Validate required context dependencies
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return entities;

            // Get database context and repository for named entity operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = context.GetRepository<NamedEntity>();
            var dbSet = dbContext.Set<NamedEntity>();

            // Find all <asNamedEntity> elements (direct children of org)
            foreach (var asNamedEntityEl in orgElement.SplElements(sc.E.AsNamedEntity))
            {
                // <code> sub element (required for DBA)
                // Extract entity type code information from the code element
                var codeEl = asNamedEntityEl.SplElement(sc.E.Code);
                var entityTypeCode = codeEl?.Attribute(sc.A.CodeValue)?.Value?.Trim();
                var entityTypeCodeSystem = codeEl?.Attribute(sc.A.CodeSystem)?.Value?.Trim();
                var entityTypeDisplayName = codeEl?.Attribute(sc.A.DisplayName)?.Value?.Trim();

                // Validation: Must be code="C117113" and codeSystem="2.16.840.1.113883.3.26.1.1" for DBA
                // Only process legitimate DBA (Doing Business As) entries per specification
                if (entityTypeCode != "C117113" || entityTypeCodeSystem != "2.16.840.1.113883.3.26.1.1")
                    continue; // Only capture true DBA names

                // <name> is required for DBA
                // Extract the entity name which is mandatory for DBA entries
                var entityName = asNamedEntityEl.SplElement(sc.E.Name)?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(entityName))
                    continue;

                // Optional: <suffix> (used in WDD/3PL only)
                // Extract optional suffix used in specific workflow scenarios
                var entitySuffix = asNamedEntityEl.SplElement(sc.E.Suffix)?.Value?.Trim();

                // Deduplicate on OrganizationID + EntityName + EntityTypeCode + Suffix
                // Search for existing named entity with matching organization and attributes
                var existing = await dbSet.FirstOrDefaultAsync(e =>
                    e.OrganizationID == organizationId &&
                    e.EntityName == entityName &&
                    e.EntityTypeCode == entityTypeCode &&
                    e.EntitySuffix == entitySuffix);

                // Return existing entity if found to avoid duplicates
                if (existing != null)
                {
                    entities.Add(existing);
                    continue;
                }

                // Create new NamedEntity
                // Build new named entity with all extracted attributes
                var newEntity = new NamedEntity
                {
                    OrganizationID = organizationId,
                    EntityTypeCode = entityTypeCode,
                    EntityTypeCodeSystem = entityTypeCodeSystem,
                    EntityTypeDisplayName = entityTypeDisplayName,
                    EntityName = entityName,
                    EntitySuffix = entitySuffix
                };

                // Persist new named entity to database
                await repo.CreateAsync(newEntity);
                entities.Add(newEntity);
            }

            return entities;
            #endregion
        }


        /**************************************************************/
        /// <summary>
        /// Finds or creates OrganizationIdentifier(s) for all [id] elements under the orgElement.
        /// Handles DUNS, FEI, Labeler Code, etc. per Section 2.1.4/2.1.5.
        /// </summary>
        /// <param name="orgElement">The XElement representing [representedOrganization] or [assignedOrganization].</param>
        /// <param name="organizationId">The parent OrganizationID.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>List of OrganizationIdentifier (both created and found).</returns>
        /// <remarks>
        /// Processes all direct [id] child elements to extract organization identifiers including:
        /// - DUNS numbers (validated as 9-digit format)
        /// - FEI (FDA Establishment Identifier) numbers
        /// - NDC Labeler Codes
        /// - Other identifier types based on OID root values
        /// 
        /// Implements deduplication logic to prevent duplicate identifier records for the same
        /// organization. Validates DUNS numbers against the required 9-digit format per SPL standards.
        /// Maps identifier types based on standard healthcare OID roots.
        /// </remarks>
        /// <seealso cref="OrganizationIdentifier"/>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static async Task<List<OrganizationIdentifier>> getOrCreateOrganizationIdentifierAsync(
            XElement orgElement,
            int organizationId,
            SplParseContext context)
        {
            #region implementation
            var identifiers = new List<OrganizationIdentifier>();

            // Validate input parameters before proceeding
            if (orgElement == null || organizationId <= 0)
                return identifiers;

            // Validate required context before proceeding
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return identifiers;

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = context.GetRepository<OrganizationIdentifier>();
            var dbSet = dbContext.Set<OrganizationIdentifier>();

            // Find all <id> child elements (direct only)
            foreach (var idEl in orgElement.SplElements(sc.E.Id))
            {
                // Extract identifier value and system root from XML attributes
                var extension = idEl.Attribute(sc.A.Extension)?.Value?.Trim();
                var root = idEl.Attribute(sc.A.Root)?.Value?.Trim();

                // Spec: must have a DUNS root unless cosmetic/animal/other types
                if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(extension))
                    continue;

                // --- Infer type by OID ---
                // (Extend as needed: add other roots for FEI, NDC, etc.)
                string identifierType = root switch
                {
                    "1.3.6.1.4.1.519.1" => "DUNS",                   // Data Universal Numbering System
                    "2.16.840.1.113883.4.82" => "FEI",               // FDA Establishment Identifier
                    "2.16.840.1.113883.6.69" => "NDC Labeler Code",  // National Drug Code Labeler
                                                                     // Add more OIDs as needed here:
                    _ => "Other"
                };

                // --- DUNS number validation: must be 9 digits if type is DUNS ---
                if (identifierType == "DUNS" && !System.Text.RegularExpressions.Regex.IsMatch(extension, @"^\d{9}$"))
                {
                    context.Logger?.LogWarning("DUNS identifier '{Value}' is not 9 digits.", extension);
                    continue;
                }

                // --- Deduplication: check if identifier already exists ---
                var existing = await dbSet.FirstOrDefaultAsync(oi =>
                    oi.OrganizationID == organizationId &&
                    oi.IdentifierValue == extension &&
                    oi.IdentifierSystemOID == root);

                if (existing != null)
                {
                    // Add existing identifier to result list
                    identifiers.Add(existing);
                    continue;
                }

                // --- Create new identifier ---
                var newIdentifier = new OrganizationIdentifier
                {
                    OrganizationID = organizationId,
                    IdentifierValue = extension,
                    IdentifierSystemOID = root,
                    IdentifierType = identifierType
                };

                // Save the new identifier to database
                await repo.CreateAsync(newIdentifier);
                identifiers.Add(newIdentifier);

                context.Logger?.LogInformation("OrganizationIdentifier created: OrganizationID={OrganizationID}, Type={Type}, Value={Value}",
                    organizationId, identifierType, extension);
            }

            return identifiers;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all [telecom] elements under the given parent (usually [contactParty]), creates Telecom records,
        /// and links them via ContactPartyTelecom. Returns count of new telecoms created.
        /// </summary>
        /// <param name="parentEl">XElement containing [telecom] elements.</param>
        /// <param name="contactPartyId">The owning ContactPartyID.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>Count of new Telecoms created and linked.</returns>
        /// <seealso cref="Telecom"/>
        /// <seealso cref="ContactPartyTelecom"/>
        /// <seealso cref="ContactParty"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<int> parseAndSaveContactPartyTelecomsAsync(
            XElement parentEl,
            int contactPartyId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Find all direct <telecom> children elements
            var telecomEls = parentEl.SplElements(sc.E.Telecom).ToList();
            if (telecomEls == null || !telecomEls.Any())
                return 0;

            // Validate required context dependencies
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return 0;

            // Process each telecom element individually
            foreach (var telecomEl in telecomEls)
            {
                // Extract telecom value from the 'value' attribute
                var value = telecomEl.Attribute("value")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                // Determine telecom type: "tel", "mailto", "fax"
                string telecomType = null;
                if (value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) telecomType = "tel";
                else if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) telecomType = "mailto";
                else if (value.StartsWith("fax:", StringComparison.OrdinalIgnoreCase)) telecomType = "fax";
                else continue; // skip unsupported telecom types

                // --- Validation (basic, expand as needed for spec) ---
                // Validate phone and fax number formats
                if (telecomType == "tel" || telecomType == "fax")
                {
                    // US/international number validation - extract number after protocol prefix
                    var number = value.Substring(value.IndexOf(':') + 1);
                    if (!number.StartsWith("+") || number.Any(char.IsLetter) || number.Contains(" "))
                    {
                        context.Logger?.LogWarning("Invalid {TelecomType} format: {Value}", telecomType, value);
                    }
                    // US pattern check (if needed): +1-aaa-bbb-cccc
                }
                // Validate email address format
                else if (telecomType == "mailto")
                {
                    // Basic email validation - check format after 'mailto:' prefix
                    if (!System.Text.RegularExpressions.Regex.IsMatch(value.Substring(7), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    {
                        context.Logger?.LogWarning("Invalid email address: {Value}", value);
                    }
                }

                // --- Deduplication: By TelecomValue (case-insensitive) ---
                // Get database context and repositories for telecom operations
                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var telecomRepo = context.GetRepository<Telecom>();
                var cptRepo = context.GetRepository<ContactPartyTelecom>();
                var telecomDbSet = dbContext.Set<Telecom>();
                var cptDbSet = dbContext.Set<ContactPartyTelecom>();

                // Search for existing telecom with same value (case-insensitive)
                var existingTelecom = await telecomDbSet.FirstOrDefaultAsync(t =>
                    t != null && t.TelecomValue != null && t.TelecomValue.ToLower() == value.ToLower());

                // Create new telecom if none exists with this value
                if (existingTelecom == null)
                {
                    existingTelecom = new Telecom
                    {
                        TelecomType = telecomType,
                        TelecomValue = value
                    };
                    await telecomRepo.CreateAsync(existingTelecom);
                    createdCount++;
                }

                // Link to ContactParty via ContactPartyTelecom (deduplication)
                // Check if link already exists between contact party and telecom
                var existingLink = await cptDbSet.FirstOrDefaultAsync(
                    link => link.ContactPartyID == contactPartyId && link.TelecomID == existingTelecom.TelecomID);

                // Create new link if none exists
                if (existingLink == null)
                {
                    var link = new ContactPartyTelecom
                    {
                        ContactPartyID = contactPartyId,
                        TelecomID = existingTelecom.TelecomID
                    };
                    await cptRepo.CreateAsync(link);
                }
            }
            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all [telecom] elements under the given parent (usually [assignedOrganization]),
        /// creates Telecom records, and links them via OrganizationTelecom.
        /// Returns count of new telecoms created and linked.
        /// </summary>
        /// <param name="parentEl">XElement containing [telecom] elements.</param>
        /// <param name="organizationId">Owning OrganizationID.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>Count of new Telecoms created and linked.</returns>
        /// <seealso cref="Telecom"/>
        /// <seealso cref="OrganizationTelecom"/>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<int> parseAndSaveOrganizationTelecomsAsync(
            XElement parentEl,
            int organizationId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Find all direct <telecom> children elements
            var telecomEls = parentEl.SplElements(sc.E.Telecom).ToList();
            if (telecomEls == null || !telecomEls.Any())
                return 0;

            // Validate required context dependencies
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return 0;

            // Process each telecom element individually
            foreach (var telecomEl in telecomEls)
            {
                // Extract telecom value from the 'value' attribute
                var value = telecomEl.Attribute("value")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                // Determine telecom type based on protocol prefix
                string telecomType = null;
                if (value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) telecomType = "tel";
                else if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) telecomType = "mailto";
                else if (value.StartsWith("fax:", StringComparison.OrdinalIgnoreCase)) telecomType = "fax";
                else continue; // skip unsupported telecom types

                // Validation (same as above)
                // Validate phone and fax number formats
                if (telecomType == "tel" || telecomType == "fax")
                {
                    // Extract number portion after protocol prefix
                    var number = value.Substring(value.IndexOf(':') + 1);
                    // Check for valid international format requirements
                    if (!number.StartsWith("+") || number.Any(char.IsLetter) || number.Contains(" "))
                    {
                        context.Logger?.LogWarning("Invalid {TelecomType} format: {Value}", telecomType, value);
                    }
                }
                // Validate email address format
                else if (telecomType == "mailto")
                {
                    // Basic email validation - check format after 'mailto:' prefix
                    if (!System.Text.RegularExpressions.Regex.IsMatch(value.Substring(7), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    {
                        context.Logger?.LogWarning("Invalid email address: {Value}", value);
                    }
                }

                // Deduplication
                // Get database context and repositories for telecom operations
                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var telecomRepo = context.GetRepository<Telecom>();
                var orgTelecomRepo = context.GetRepository<OrganizationTelecom>();
                var telecomDbSet = dbContext.Set<Telecom>();
                var orgTelecomDbSet = dbContext.Set<OrganizationTelecom>();

                // Search for existing telecom with same value (case-insensitive)
                var existingTelecom = await telecomDbSet.FirstOrDefaultAsync(t =>
                     t != null && t.TelecomValue != null && t.TelecomValue.ToLower() == value.ToLower());

                // Create new telecom if none exists with this value
                if (existingTelecom == null)
                {
                    existingTelecom = new Telecom
                    {
                        TelecomType = telecomType,
                        TelecomValue = value
                    };
                    await telecomRepo.CreateAsync(existingTelecom);
                    createdCount++;
                }

                // Link to Organization via OrganizationTelecom (deduplication)
                // Check if link already exists between organization and telecom
                var existingLink = await orgTelecomDbSet.FirstOrDefaultAsync(
                    link => link.OrganizationID == organizationId && link.TelecomID == existingTelecom.TelecomID);

                // Create new link if none exists
                if (existingLink == null)
                {
                    var link = new OrganizationTelecom
                    {
                        OrganizationID = organizationId,
                        TelecomID = existingTelecom.TelecomID
                    };
                    await orgTelecomRepo.CreateAsync(link);
                }
            }
            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves all [contactParty] entities under the specified element,
        /// associating each with the given organization. Returns the count created.
        /// </summary>
        /// <param name="element">Parent XElement to scan for contactParty nodes.</param>
        /// <param name="context">Parsing context (repos, logger, etc).</param>
        /// <param name="organizationId">Owning OrganizationID (required).</param>
        /// <returns>Count of new ContactParty entities created.</returns>
        private static async Task<(int createdct, int telecomCt)> parseAndSaveContactPartiesAsync(
            XElement element,
            SplParseContext context,
            int organizationId)
        {
            int createdCt = 0;
            int telecomCt = 0;

            // Find all <contactParty> nodes (case-insensitive, supports namespace)
            var contactPartyEls = element.SplFindElements(sc.E.ContactParty);
            if (contactPartyEls != null)
            {
                foreach (var contactPartyEl in contactPartyEls)
                {
                    var (contactParty, partyCreated) = await getOrCreateContactPartyAsync(contactPartyEl, organizationId, context);

                    if (contactParty?.ContactPartyID == null)
                    {
                        context.Logger?.LogWarning("Failed to create ContactParty for OrganizationID {OrgId}.", organizationId);
                        context.Logger?.LogError($"Failed to create contact party for organization {organizationId}.");
                    }
                    else if (partyCreated)
                    {
                        context.Logger?.LogInformation("Created ContactParty for OrganizationID {OrgId} with AddressID {AddrId} and ContactPersonID {PersonId}",
                            organizationId, contactParty.AddressID, contactParty.ContactPersonID);
                        createdCt++;

                        // --- PARSE TELECOMS ---
                        var telecomsCreated = await parseAndSaveContactPartyTelecomsAsync(contactPartyEl, contactParty.ContactPartyID.Value, context);
                        telecomCt += telecomsCreated;

                    }
                    else
                    {
                        context.Logger?.LogInformation("Found existing ContactParty for OrganizationID {OrgId}", organizationId);
                    }
                }
            }
            return (createdCt, telecomCt);
        }

        /**************************************************************/
        /// <summary>
        /// Defines hierarchical relationships between organizations within a document header (e.g., Labeler → Registrant → Establishment).
        /// Parses and saves all DocumentRelationship entities at the Author level 
        /// by examining author/assignedEntity/representedOrganization hierarchies.
        /// </summary>
        /// <param name="authorEl">The author XML element containing organization hierarchy information.</param>
        /// <param name="context">The parsing context providing database access and logging services.</param>
        /// <param name="documentId">The document ID to associate relationships with.</param>
        /// <param name="lablelerId">The labeler organization ID as the root of the hierarchy.</param>
        /// <returns>The count of DocumentRelationship records created.</returns>
        /// <remarks>
        /// Describes the specific relationship types including:
        /// - LabelerToRegistrant (4.1.3 [cite: 788])
        /// - RegistrantToEstablishment (4.1.4 [cite: 791]) 
        /// - EstablishmentToUSagent (6.1.4 [cite: 914])
        /// - EstablishmentToImporter (6.1.5 [cite: 918])
        /// - LabelerToDetails (5.1.3 [cite: 863])
        /// - FacilityToParentCompany (35.1.6 [cite: 1695])
        /// - LabelerToParentCompany (36.1.2.5 [cite: 1719])
        /// - DocumentToBulkLotManufacturer (16.1.3)
        /// 
        /// Indicates the level in the hierarchy (e.g., 1 for Labeler, 2 for Registrant, 3 for Establishment).
        /// Follows the SPL pattern: author → assignedEntity (labeler) → representedOrganization (registrant) → assignedEntity (establishment).
        /// </remarks>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="Organization"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveDocumentRelationshipsAsync(
            XElement authorEl,
            SplParseContext context,
            int documentId,
            int lablelerId)
        {
            #region implementation
            int count = 0;
            var dbContext = context?.ServiceProvider?.GetRequiredService<ApplicationDbContext>();

            // Validate required context before proceeding
            if (context == null || dbContext == null || context.Logger == null || context.Document?.DocumentID == null)
                return count;

            // Track the parent and all children for relationship building
            Organization? labelerOrg = null;
            Organization? registrantOrg = null;
            List<Organization> establishmentOrgs = new List<Organization>();

            // Helper: gets or creates an org from an assignedEntity/assignedOrganization element
            async Task<Organization?> getOrgFromEntity(XElement entityEl)
            {
                #region implementation
                // Look for either assignedOrganization or representedOrganization
                var assignedOrgEl = entityEl.GetSplElement(sc.E.AssignedOrganization)
                                     ?? entityEl.GetSplElement(sc.E.RepresentedOrganization);
                if (assignedOrgEl == null)
                    return null;

                // Get or create the organization entity
                var (org, _) = await getOrCreateOrganizationAsync(assignedOrgEl, context);
                return org;
                #endregion
            }

            // Traverse author/assignedEntity/representedOrganization tree
            // Pattern: author → assignedEntity (labeler) → representedOrganization (registrant) → assignedEntity (establishment)
            var labelerEntityEl = authorEl.GetSplElement(sc.E.AssignedEntity);
            if (labelerEntityEl != null)
            {
                // Get the labeler organization from the assigned entity
                labelerOrg = await getOrgFromEntity(labelerEntityEl);

                // Registrant: Look for representedOrganization/assignedEntity under labeler
                var registrantRepOrgEl = labelerEntityEl.GetSplElement(sc.E.RepresentedOrganization);
                if (registrantRepOrgEl != null)
                {
                    var registrantEntityEl = registrantRepOrgEl.GetSplElement(sc.E.AssignedEntity);
                    if (registrantEntityEl != null)
                    {
                        // Get the registrant organization
                        registrantOrg = await getOrgFromEntity(registrantEntityEl);

                        if (registrantOrg?.OrganizationID != null)
                        {
                            // Save Labeler → Registrant relationship
                            await saveOrGetDocumentRelationshipAsync(
                                dbContext,
                                documentId,
                               lablelerId,
                                registrantOrg.OrganizationID,
                                "LabelerToRegistrant",
                                2
                            );
                            count++;
                            context.Logger.LogInformation(
                                $"DocumentRelationship: Labeler ({labelerOrg?.OrganizationID}) → Registrant ({registrantOrg.OrganizationID}) saved.");
                        }

                        // Establishment(s): Look for assignedOrganization(s) under registrant entity
                        foreach (var establishmentEntityEl in registrantEntityEl.SplElements(sc.E.AssignedEntity))
                        {
                            var establishmentOrg = await getOrgFromEntity(establishmentEntityEl);
                            if (establishmentOrg != null
                                && registrantOrg != null
                                && establishmentOrg?.OrganizationID != null
                                && registrantOrg.OrganizationID != null)
                            {
                                establishmentOrgs.Add(establishmentOrg);

                                // Save Registrant → Establishment relationship
                                await saveOrGetDocumentRelationshipAsync(
                                    dbContext,
                                    documentId,
                                    registrantOrg.OrganizationID,
                                    establishmentOrg.OrganizationID,
                                    "RegistrantToEstablishment",
                                    3
                                );
                                count++;
                                context.Logger.LogInformation(
                                    $"DocumentRelationship: Registrant ({registrantOrg.OrganizationID}) → Establishment ({establishmentOrg.OrganizationID}) saved.");
                            }
                        }
                    }
                    else
                    {
                        // Save relationship with null child when no registrant entity found
                        await saveOrGetDocumentRelationshipAsync(
                            dbContext,
                            documentId,
                            lablelerId,
                            null,
                            null,
                            1
                        );
                    }
                }
            }

            // If only a Labeler was found, optionally record a relationship to itself or log as a single org
            if (labelerOrg != null && count == 0)
            {
                context.Logger.LogInformation(
                    $"No Registrant/Establishment found; Labeler OrganizationID={labelerOrg.OrganizationID}");
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing Address by all normalized fields or creates a new one.
        /// Validates per Section 2.1.6.
        /// </summary>
        /// <param name="addrEl">XElement for [addr] (may be null).</param>
        /// <param name="context">Parsing context.</param>
        /// <returns>(Address entity, wasCreated)</returns>
        /// <seealso cref="Address"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<(Address? Address, bool Created)> getOrCreateAddressAsync(XElement? addrEl, SplParseContext context)
        {
            #region implementation
            // Return early if no address element provided
            if (addrEl == null) return (null, false);

            // Validate required context dependencies
            if (context == null || context.ServiceProvider == null || context.Logger == null)
                return (null, false);

            // Extract and normalize address field values from XML elements
            var streetLines = addrEl.SplElements(sc.E.StreetAddressLine).Select(x => x.Value?.Trim()).ToList();
            var city = addrEl.SplElement(sc.E.City)?.Value?.Trim();
            var state = addrEl.SplElement(sc.E.State)?.Value?.Trim();
            var postalCode = addrEl.SplElement(sc.E.PostalCode)?.Value?.Trim();

            // Extract country information from nested country element
            var countryEl = addrEl.SplElement(sc.E.Country);
            var countryCode = countryEl?.Attribute(sc.A.CodeValue)?.Value?.Trim();
            var countryName = countryEl?.Value?.Trim();
            var countryCodeSystem = countryEl?.Attribute(sc.A.CodeSystem)?.Value?.Trim();

            // --- Country code normalization/validation ---
            // If no country code but country name exists, check if name is 3-char code
            if (string.IsNullOrWhiteSpace(countryCode) && !string.IsNullOrWhiteSpace(countryName))
                countryCode = countryName.Length == 3 ? countryName.ToUpper() : null;

            // Enforce ISO 3166-1 alpha-3 for countryCode if codeSystem is present
            if (!string.IsNullOrWhiteSpace(countryCode) && countryCodeSystem == "1.0.3166.1.2.3" && countryCode.Length != 3)
            {
                context.Logger.LogWarning("Country code {CountryCode} is not ISO 3166-1 alpha-3.", countryCode);
            }

            // --- USA rules ---
            // Apply specific validation rules for USA addresses
            if (countryCode == "USA")
            {
                // Must have state and 5 or 5+4 digit zip
                if (string.IsNullOrWhiteSpace(state) || string.IsNullOrWhiteSpace(postalCode))
                {
                    context.Logger.LogWarning("USA address must have state and postalCode.");
                }
                // Validate ZIP code format (5 digits or ZIP+4)
                if (!System.Text.RegularExpressions.Regex.IsMatch(postalCode ?? "", @"^\d{5}(-\d{4})?$"))
                {
                    context.Logger.LogWarning("USA postalCode must be 5 digits or ZIP+4.");
                }
            }
            // For non-USA, just require postal code (per spec)
            else if (string.IsNullOrWhiteSpace(postalCode))
            {
                context.Logger.LogWarning("Non-USA address missing postalCode.");
            }

            // --- Deduplication: full match on all address fields ---
            // Get database context and repository for address lookups
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var addrRepo = context.GetRepository<Address>();
            var dbSet = dbContext.Set<Address>();

            // Search for existing address with matching normalized fields
            var existing = await dbSet.FirstOrDefaultAsync(a =>
                a.StreetAddressLine1 == (streetLines.Count > 0 ? streetLines[0] : null) &&
                a.StreetAddressLine2 == (streetLines.Count > 1 ? streetLines[1] : null) &&
                a.City == city &&
                a.StateProvince == state &&
                a.PostalCode == postalCode &&
                a.CountryCode == countryCode &&
                a.CountryName == countryName);

            // Return existing address if found
            if (existing != null)
                return (existing, false);

            // Create new address entity with normalized values
            var newAddr = new Address
            {
                StreetAddressLine1 = streetLines.Count > 0 ? streetLines[0] : null,
                StreetAddressLine2 = streetLines.Count > 1 ? streetLines[1] : null,
                City = city,
                StateProvince = state,
                PostalCode = postalCode,
                CountryCode = countryCode,
                CountryName = countryName
            };

            // Persist new address to database
            await addrRepo.CreateAsync(newAddr);
            return (newAddr, true);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts and persists a ContactParty and related entities (Address, ContactPerson) from XML.
        /// Follows Sections 2.1.6 and 2.1.8, validates address per specification, and enforces deduplication.
        /// </summary>
        /// <param name="contactPartyEl">XElement representing [contactParty].</param>
        /// <param name="organizationId">Owning OrganizationID (nullable, but must be provided).</param>
        /// <param name="context">The current parsing context (repo, logger, etc).</param>
        /// <returns>Tuple: (ContactParty entity, wasCreated), or (null, false) if not created.</returns>
        /// <seealso cref="ContactParty"/>
        /// <seealso cref="Address"/>
        /// <seealso cref="ContactPerson"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<(ContactParty? ContactParty, bool Created)> getOrCreateContactPartyAsync(
            XElement contactPartyEl,
            int? organizationId,
            SplParseContext context)
        {
            #region implementation
            // Validate required input parameters
            if (contactPartyEl == null || organizationId == null)
                return (null, false);

            // Validate context dependencies
            if (context == null || context.ServiceProvider == null)
                return (null, false);

            // --- ADDRESS ---
            // Extract and process address element if present
            var addrEl = contactPartyEl.SplElement(sc.E.Addr);
            var (address, addrCreated) = await getOrCreateAddressAsync(addrEl, context);

            // --- CONTACT PERSON ---
            // Extract and process contact person element if present
            var contactPersonEl = contactPartyEl.SplElement(sc.E.ContactPerson);
            var (contactPerson, personCreated) = await getOrCreateContactPersonAsync(contactPersonEl, context);

            // --- CONTACT PARTY DEDUPLICATION ---
            // Get database context and repository for contact party operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var partyRepo = context.GetRepository<ContactParty>();
            var dbSet = dbContext.Set<ContactParty>();

            // Check for existing contact party with same organization, address, and person
            if (address != null
                && contactPerson != null
                && address.AddressID > 0
                && contactPerson.ContactPersonID > 0)
            {
                var existingParty = await dbSet.FirstOrDefaultAsync(cp =>
                    cp.OrganizationID == organizationId &&
                    cp.AddressID == address.AddressID &&
                    cp.ContactPersonID == contactPerson.ContactPersonID);

                // Return existing party if found
                if (existingParty != null)
                    return (existingParty, false);
            }

            // Create new contact party entity linking organization, address, and person
            var newParty = new ContactParty
            {
                OrganizationID = organizationId,
                AddressID = address?.AddressID,
                ContactPersonID = contactPerson?.ContactPersonID
            };

            // Persist new contact party to database
            await partyRepo.CreateAsync(newParty);

            // Optionally: Handle telecom/email here if required (store on Organization, separate table, etc.)
            // TODO: Extract and link telecom if needed

            return (newParty, true);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates a ContactPerson by normalized name. For Section 2.1.8.
        /// </summary>
        /// <param name="contactPersonEl">XElement for [contactPerson] (may be null).</param>
        /// <param name="context">Parsing context.</param>
        /// <returns>(ContactPerson entity, wasCreated)</returns>
        /// <seealso cref="ContactPerson"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<(ContactPerson? ContactPerson, bool Created)> getOrCreateContactPersonAsync(XElement? contactPersonEl, SplParseContext context)
        {
            #region implementation
            // Return early if no contact person element provided
            if (contactPersonEl == null) return (null, false);

            // Validate required context dependencies
            if (context == null || context.ServiceProvider == null)
                return (null, false);

            // Extract and normalize contact person name
            var name = contactPersonEl.SplElement(sc.E.Name)?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return (null, false);

            // Get database context and repository for contact person operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var personRepo = context.GetRepository<ContactPerson>();
            var dbSet = dbContext.Set<ContactPerson>();

            // Search for existing contact person with matching name
            var existing = await dbSet.FirstOrDefaultAsync(p => p.ContactPersonName == name);
            if (existing != null)
                return (existing, false);

            // Create new contact person entity with normalized name
            var newPerson = new ContactPerson { ContactPersonName = name };
            await personRepo.CreateAsync(newPerson);

            return (newPerson, true);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing DocumentRelationship or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="docId">The document ID to associate with the relationship.</param>
        /// <param name="parentOrgId">The parent organization ID in the relationship hierarchy.</param>
        /// <param name="childOrgId">The child organization ID in the relationship hierarchy.</param>
        /// <param name="relationshipType">The type of relationship between the organizations.</param>
        /// <param name="relationshipLevel">The hierarchical level of the relationship.</param>
        /// <returns>The existing or newly created DocumentRelationship entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern with fallback search logic. First attempts to find
        /// an exact match on all parameters, then falls back to matching by document and parent
        /// organization if no exact match is found. This supports scenarios where relationship
        /// details may vary while maintaining organizational hierarchy integrity.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<DocumentRelationship> saveOrGetDocumentRelationshipAsync(
            ApplicationDbContext dbContext,
            int? docId,
            int? parentOrgId,
            int? childOrgId,
            string? relationshipType,
            int? relationshipLevel)
        {
            #region implementation
            // Validate inputs to ensure data integrity
            if (dbContext == null || docId == null || parentOrgId == null)
                throw new ArgumentNullException("A required argument for DocumentRelationship is null.");

            // Try to find an existing relationship with exact parameter match
            var existing = await dbContext.Set<DocumentRelationship>().FirstOrDefaultAsync(dr =>
                dr.DocumentID == docId &&
                dr.ParentOrganizationID == parentOrgId &&
                dr.ChildOrganizationID == childOrgId &&
                dr.RelationshipType == relationshipType);

            // Fallback: search by document and parent organization only
            if (existing == null)
            {
                existing = await dbContext.Set<DocumentRelationship>().FirstOrDefaultAsync(dr =>
                    dr.DocumentID == docId &&
                    dr.ParentOrganizationID == parentOrgId);
            }

            // Return existing relationship if found
            if (existing != null)
                return existing;

            // Create new relationship entity with provided parameters
            var newRel = new DocumentRelationship
            {
                DocumentID = docId,
                ParentOrganizationID = parentOrgId,
                ChildOrganizationID = childOrgId,
                RelationshipType = relationshipType,
                RelationshipLevel = relationshipLevel
            };

            // Save the new relationship to database
            dbContext.Set<DocumentRelationship>().Add(newRel);
            await dbContext.SaveChangesAsync();
            return newRel;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing organization by name or creates a new one if not found.
        /// This normalizes organization data, preventing duplicates.
        /// </summary>
        /// <param name="orgElement">The XElement representing the organization (e.g., representedOrganization).</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A tuple containing the Organization entity and a boolean indicating if it was newly created.</returns>
        /// <example>
        /// <code>
        /// var (org, wasCreated) = await getOrCreateOrganizationAsync(orgElement, context);
        /// if (wasCreated)
        /// {
        ///     Console.WriteLine($"Created new organization: {org.OrganizationName}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method implements deduplication logic by first checking if an organization
        /// with the same name already exists in the database. If found, it returns the
        /// existing entity. Otherwise, it creates a new organization with data extracted
        /// from the XML element.
        /// </remarks>
        /// <seealso cref="Organization"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElementExtensions.GetSplElementVal(XElement, string)"/>
        private static async Task<(Organization? Organization, bool Created)> getOrCreateOrganizationAsync(XElement orgElement, SplParseContext context)
        {
            #region implementation
            // Extract organization name using the helper extension method
            var orgName = orgElement.GetSplElementVal(sc.E.Name)?.Trim();

            if (context == null
                || context.Logger == null
                || context.ServiceProvider == null)
            {
                throw new ArgumentNullException(nameof(context), "Parsing context, logger, and provider cannot be null.");
            }

            // Validate that we have a valid organization name
            if (string.IsNullOrWhiteSpace(orgName))
            {
                context.Logger.LogWarning("Organization name is missing in file {FileName}. Cannot create organization.", context.FileNameInZip);
                return (null, false);
            }

            // Get database context and repository for organization operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orgRepo = context.GetRepository<Organization>();
            var orgDbSet = dbContext.Set<Organization>();

            // Check if the organization already exists in the database by name
            var existingOrg = await orgDbSet
                .FirstOrDefaultAsync(o => o.OrganizationName == orgName);

            // Return existing organization if found
            if (existingOrg != null)
            {
                return (existingOrg, false); // Return existing organization
            }

            // Create a new organization entity with extracted data
            var newOrganization = new Organization
            {
                OrganizationName = orgName,

                // Extract confidentiality information from XML attributes
                // Check if the confidentiality code value equals "B" (confidential)
                IsConfidential = orgElement
                .GetSplElementAttrVal(sc.E.ConfidentialityCode, sc.A.CodeValue) == "B"
            };

            // Save the new organization to the database (CreateAsync populates the ID)
            await orgRepo.CreateAsync(newOrganization);

            return (newOrganization, true); // Return newly created organization
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a link between a document and an authoring organization if it doesn't already exist.
        /// </summary>
        /// <param name="docId">The ID of the document.</param>
        /// <param name="orgId">The ID of the authoring organization.</param>
        /// <param name="authorType">The type of the author (e.g., "Labeler").</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A tuple containing the DocumentAuthor entity and a boolean indicating if it was newly created.</returns>
        /// <example>
        /// <code>
        /// var (docAuthor, wasCreated) = await getOrCreateDocumentAuthorAsync(123, 456, "Labeler", context);
        /// if (wasCreated)
        /// {
        ///     Console.WriteLine("Created new document author relationship");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method implements deduplication logic for document-author relationships.
        /// It first checks if a link between the specified document and organization already
        /// exists. If not found, it creates a new DocumentAuthor entity to establish the relationship.
        /// </remarks>
        /// <seealso cref="DocumentAuthor"/>
        /// <seealso cref="ApplicationDbContext"/>
        private static async Task<(DocumentAuthor? DocumentAuthor, bool Created)> getOrCreateDocumentAuthorAsync(int docId, int orgId, string authorType, SplParseContext context)
        {
            #region implementation

            if (context == null
               || context.Logger == null
               || context.ServiceProvider == null)
            {
                throw new ArgumentNullException(nameof(context), "Parsing context, logger, and provider cannot be null.");
            }

            // Get database context and repository for document author operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var docAuthorRepo = context.GetRepository<DocumentAuthor>();
            var docAuthorDbSet = dbContext.Set<DocumentAuthor>();

            // Check if the document-author link already exists in the database
            var existingLink = await docAuthorDbSet
                .FirstOrDefaultAsync(da =>
                da.DocumentID == docId && da.OrganizationID == orgId);

            // Return existing link if found
            if (existingLink != null)
            {
                return (existingLink, false);
            }

            // Create a new document author relationship entity
            var newDocAuthor = new DocumentAuthor
            {
                DocumentID = docId,
                OrganizationID = orgId,
                AuthorType = authorType
            };

            // Save the new document author link to the database
            await docAuthorRepo.CreateAsync(newDocAuthor);

            return (newDocAuthor, true);
            #endregion
        }
    }
}
