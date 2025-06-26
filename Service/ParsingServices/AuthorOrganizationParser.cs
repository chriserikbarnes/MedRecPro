using Microsoft.EntityFrameworkCore;
using System.Xml.Linq;
using MedRecPro.Data;
using MedRecPro.Helpers;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;

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
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate that we have a valid document context to work with
            if (context.Document?.DocumentID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse author because no document context exists.");
                return result;
            }

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
                // Step 1: Get or Create the Organization entity
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
                    context.Logger.LogInformation("Created new Organization (Author) '{OrgName}' with ID {OrganizationID}",
                        organization.OrganizationName, organization.OrganizationID);
                }
                else
                {
                    context.Logger.LogInformation("Found existing Organization (Author) '{OrgName}' with ID {OrganizationID}",
                        organization.OrganizationName, organization.OrganizationID);
                }

                // Step 2: Get or Create the DocumentAuthor link between document and organization
                var (_, docAuthorCreated) = await getOrCreateDocumentAuthorAsync(
                    context.Document.DocumentID.Value,
                    organization.OrganizationID.Value,
                    "Labeler",
                    context);

                // Log the document author link creation if it was newly created
                if (docAuthorCreated)
                {
                    context.Logger.LogInformation("Created DocumentAuthor link for DocumentID {DocumentID} and OrganizationID {OrganizationID}",
                       context.Document.DocumentID.Value, organization.OrganizationID.Value);
                }
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
