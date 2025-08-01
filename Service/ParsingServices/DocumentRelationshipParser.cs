
using System.Xml.Linq;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Data;
using MedRecPro.Helpers;
using static MedRecPro.Models.Label;
using sc = MedRecPro.Models.SplConstants;
using c = MedRecPro.Models.Constant;
using Microsoft.EntityFrameworkCore;

namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses the subject of a section to create a DocumentRelationship entity,
    /// linking a parent organization (Labeler) to a child organization (Establishment).
    /// </summary>
    /// <remarks>
    /// This parser is specifically used for sections where the subject is an establishment.
    /// It reuses helper methods from the AuthorSectionParser to find or create the
    /// Organization records before creating the linking DocumentRelationship record.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="DocumentRelationship"/>
    /// <seealso cref="AuthorSectionParser"/>
    /// <seealso cref="Label"/>
    public class DocumentRelationshipParser : ISplSectionParser
    {
        #region implementation
        public string SectionName => "documentRelationship";

        /**************************************************************/
        /// <summary>
        /// Parses the provided [subject] element to create a DocumentRelationship.
        /// </summary>
        /// <param name="subjectEl">The [subject] XElement of the section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional action to report progress.</param>
        /// <returns>A SplParseResult. The created DocumentRelationship is set on the context.</returns>
        public async Task<SplParseResult> ParseAsync(XElement subjectEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            // 1. Find the Parent Organization (the Labeler for this document)
            var parentOrg = await getLabelerOrganizationAsync(context);
            if (parentOrg?.OrganizationID == null)
            {
                result.Errors.Add("Could not determine the parent Labeler organization for the document.");
                return result;
            }

            // 2. Find the Child Organization (the Establishment from the section's subject)
            var establishmentOrgEl = subjectEl.SplElement(sc.E.RepresentedOrganization)
                ?? subjectEl.SplElement(sc.E.AssignedEntity, sc.E.RepresentedOrganization);

            if (establishmentOrgEl == null)
            {
                result.Errors.Add("Could not find the establishment's <representedOrganization> element in the section subject.");
                return result;
            }

            // **REVISED CALL**: Use the public static helper from AuthorSectionParser
            var (childOrg, _) = await AuthorSectionParser.GetOrCreateOrganizationByIdentifierAsync(establishmentOrgEl, context);
            if (childOrg?.OrganizationID == null)
            {
                result.Errors.Add("Failed to parse or create the child Establishment organization.");
                return result;
            }

            if (context == null || context.Document == null)
            {
                result.Errors.Add("Parsing context or document is not set.");
                return result;
            }

            // 3. Create the DocumentRelationship object
            var relationship = new DocumentRelationship
            {
                DocumentID = context.Document.DocumentID,
                ParentOrganizationID = parentOrg.OrganizationID,
                ChildOrganizationID = childOrg.OrganizationID,
                RelationshipType = c.CERTIFICATION_RELATIONSHIP_TYPE,
                RelationshipLevel = c.CERTIFICATION_RELATIONSHIP_LEVEL
            };

            // 4. Get or create the relationship record and update the context
            var createdRelationship = await getOrCreateDocumentRelationshipAsync(relationship, context);
            if (createdRelationship != null || createdRelationship?.DocumentRelationshipID != null)
            {
                context.CurrentDocumentRelationship = createdRelationship;
                result.ProductElementsCreated++;
            }
            else
            {
                result.Errors.Add("Failed to create the DocumentRelationship link between Labeler and Establishment.");
            }

            return result; 
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the Organization entity for the document's Labeler by querying the DocumentAuthor link.
        /// </summary>
        private async Task<Organization?> getLabelerOrganizationAsync(SplParseContext context)
        {
            #region implementation
            if (context == null
                   || context.ServiceProvider == null
                   || context.Document == null)
            {
                return null;
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Find the DocumentAuthor link created during header parsing
            var labelerAuthorLink = await dbContext.Set<DocumentAuthor>()
                .Include(da => da.Organization)
                .FirstOrDefaultAsync(da =>
                    da.DocumentID == context.Document.DocumentID &&
                    da.AuthorType == "Labeler");

            return labelerAuthorLink?.Organization; 
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets or creates the DocumentRelationship record to avoid duplicates.
        /// </summary>
        private async Task<DocumentRelationship?> getOrCreateDocumentRelationshipAsync(DocumentRelationship relationship, SplParseContext context)
        {
            #region implementation
            if (context == null ||
                    context.ServiceProvider == null)
            {
                return null;
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = context.GetRepository<DocumentRelationship>();
            var dbSet = dbContext.Set<DocumentRelationship>();

            var existing = await dbSet.FirstOrDefaultAsync(dr =>
                dr.DocumentID == relationship.DocumentID &&
                dr.ParentOrganizationID == relationship.ParentOrganizationID &&
                dr.ChildOrganizationID == relationship.ChildOrganizationID &&
                dr.RelationshipType == relationship.RelationshipType);

            if (existing != null) return existing;

            await repo.CreateAsync(relationship);
            return relationship; 
            #endregion
        }
        #endregion
    }
}

