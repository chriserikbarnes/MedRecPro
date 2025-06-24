using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using MedRecPro.Service.ParsingServices; // For SplConstants
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Service.ParsingServices.SplConstants; // Constant class for SPL elements and attributes

namespace MedRecPro.Service.ParsingServices
{
    /// <summary>
    /// Parses the <author> element, finds or creates the <representedOrganization>,
    /// and links it to the document. Normalizes organization data to prevent duplicates.
    /// </summary>
    public class AuthorSectionParser : ISplSectionParser
    {
        // Use constants to avoid magic strings
        public string SectionName => sc.E.Author;
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        /*******************************************************************************/
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context)
        {
            var result = new SplParseResult();
            if (context.Document?.DocumentID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse author because no document context exists.");
                return result;
            }

            // Use constants for element names
            var authorOrgElement = element.Element(ns + sc.E.AssignedEntity)
                                          ?.Element(ns + sc.E.RepresentedOrganization);

            if (authorOrgElement == null)
            {
                context.Logger.LogWarning("No <{OrganizationElement}> found within <{AuthorElement}> for file {FileName}",
                    sc.E.RepresentedOrganization, sc.E.Author, context.FileNameInZip);
                return result;
            }

            try
            {
                // Step 1: Get or Create the Organization
                var (organization, orgCreated) = await getOrCreateOrganizationAsync(authorOrgElement, context);

                if (organization?.OrganizationID == null)
                {
                    result.Success = false;
                    result.Errors.Add("Failed to get or create an organization for the author.");
                    return result;
                }

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

                // Step 2: Get or Create the DocumentAuthor link
                var (_, docAuthorCreated) = await getOrCreateDocumentAuthorAsync(context.Document.DocumentID.Value, organization.OrganizationID.Value, "Labeler", context);

                if (docAuthorCreated)
                {
                    context.Logger.LogInformation("Created DocumentAuthor link for DocumentID {DocumentID} and OrganizationID {OrganizationID}",
                       context.Document.DocumentID.Value, organization.OrganizationID.Value);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing author: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <{AuthorElement}> element.", sc.E.Author);
            }
            return result;
        }

        /*******************************************************************************/
        /// <summary>
        /// Finds an existing organization by name or creates a new one if not found.
        /// This normalizes organization data, preventing duplicates.
        /// </summary>
        /// <param name="orgElement">The XElement representing the organization (e.g., <representedOrganization>).</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A tuple containing the Organization entity and a boolean indicating if it was newly created.</returns>
        private static async Task<(Organization? Organization, bool Created)> getOrCreateOrganizationAsync(XElement orgElement, SplParseContext context)
        {
            // Use XElementExtensions helper to safely get values
            var orgName = orgElement.GetChildVal(ns + sc.E.Name)?.Trim();

            if (string.IsNullOrWhiteSpace(orgName))
            {
                context.Logger.LogWarning("Organization name is missing in file {FileName}. Cannot create organization.", context.FileNameInZip);
                return (null, false);
            }

            // To query by a non-PK field, we need the DbContext directly.
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orgRepo = context.GetRepository<Organization>();
            var orgDbSet = dbContext.Set<Organization>();

            // Check if the organization already exists
            var existingOrg = await orgDbSet
                .FirstOrDefaultAsync(o => o.OrganizationName == orgName);

            if (existingOrg != null)
            {
                return (existingOrg, false); // Return existing organization
            }

            // If not found, create a new one
            var newOrganization = new Organization
            {
                OrganizationName = orgName,

                // Use XElementExtensions and constants for attributes
                IsConfidential = orgElement
                .GetChildAttrVal(ns + sc.E.ConfidentialityCode, sc.A.CodeValue) == "B"
            };

            await orgRepo.CreateAsync(newOrganization); // CreateAsync populates the ID

            return (newOrganization, true); // Return newly created organization
        }

        /*******************************************************************************/
        /// <summary>
        /// Creates a link between a document and an authoring organization if it doesn't already exist.
        /// </summary>
        /// <param name="docId">The ID of the document.</param>
        /// <param name="orgId">The ID of the authoring organization.</param>
        /// <param name="authorType">The type of the author (e.g., "Labeler").</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A tuple containing the DocumentAuthor entity and a boolean indicating if it was newly created.</returns>
        private static async Task<(DocumentAuthor? DocumentAuthor, bool Created)> getOrCreateDocumentAuthorAsync(int docId, int orgId, string authorType, SplParseContext context)
        {
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var docAuthorRepo = context.GetRepository<DocumentAuthor>();
            var docAuthorDbSet = dbContext.Set<DocumentAuthor>();

            // Check if the link already exists
            var existingLink = await docAuthorDbSet
                .FirstOrDefaultAsync(da =>
                da.DocumentID == docId && da.OrganizationID == orgId);

            if (existingLink != null)
            {
                return (existingLink, false);
            }

            // If not found, create a new link
            var newDocAuthor = new DocumentAuthor
            {
                DocumentID = docId,
                OrganizationID = orgId,
                AuthorType = authorType
            };

            await docAuthorRepo.CreateAsync(newDocAuthor);

            return (newDocAuthor, true);
        }
    }
}
