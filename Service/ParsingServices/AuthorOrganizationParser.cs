using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using MedRecPro.Data;
using MedRecPro.Helpers;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;
using MedRecPro.Models;

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
                var (_, docAuthorCreated) = await getOrCreateDocumentAuthorAsync(
                    context!.Document.DocumentID.Value,
                    organization.OrganizationID.Value,
                    "Labeler",
                    context);

                // Log the document author link creation if it was newly created
                if (docAuthorCreated)
                {
                    context.Logger.LogInformation("Created DocumentAuthor link for DocumentID {DocumentID} and OrganizationID {OrganizationID}",
                       context.Document.DocumentID.Value, organization.OrganizationID.Value);

                    reportProgress?.Invoke($"Completed Author XML Elements {context.FileNameInZip}");
                }


                // --- PARSE DOCUMENT RELATIONSHIP ---
                var relationshipsCount = await parseAndSaveDocumentRelationshipsAsync(
                    element, context, context.Document.DocumentID.Value, organization.OrganizationID.Value);
                result.OrganizationsCreated += relationshipsCount;
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
