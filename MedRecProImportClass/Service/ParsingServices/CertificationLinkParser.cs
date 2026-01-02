
using System.Xml.Linq;
using MedRecProImportClass.DataAccess;
using MedRecProImportClass.Models;
using MedRecProImportClass.Data;
using MedRecProImportClass.Helpers;
using static MedRecProImportClass.Models.Label;
using Microsoft.EntityFrameworkCore;

namespace MedRecProImportClass.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses and creates a link between a certified establishment and a product.
    /// </summary>
    /// <remarks>
    /// This parser is specific to "Blanket No Changes Certification" documents (SPL IG Section 28.1.3).
    /// It creates a `CertificationProductLink` record, which is a junction table entry connecting
    /// a `DocumentRelationship` (the establishment) and a `ProductIdentifier` (the product).
    /// It relies on the calling parser to have set these entities in the `SplParseContext`.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="CertificationProductLink"/>
    /// <seealso cref="Label"/>
    public class CertificationProductLinkParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser.
        /// </summary>
        public string SectionName => "certificationProductLink";

        /**************************************************************/
        /// <summary>
        /// Creates a CertificationProductLink entity based on the current parsing context.
        /// </summary>
        /// <param name="element">The XElement being parsed, typically the [product] element. Its contents are not directly used as data is sourced from the context.</param>
        /// <param name="context">The current parsing context, which must contain the CurrentDocumentRelationship and CurrentProductIdentifier.</param>
        /// <param name="reportProgress">Optional action to report progress.</param>
        /// <param name="isParentCallingForAllSubElements"></param>
        /// <returns>A SplParseResult indicating success or failure.</returns>
        /// <example>
        /// <code>
        /// // Called from ManufacturedProductParser when inside a certification section
        /// var certLinkParser = new CertificationProductLinkParser();
        /// var certLinkResult = await certLinkParser.ParseAsync(productElement, context, reportProgress);
        /// result.MergeFrom(certLinkResult);
        /// </code>
        /// </example>
        /// <seealso cref="CertificationProductLink"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="DocumentRelationship"/>
        /// <seealso cref="ProductIdentifier"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress, bool? isParentCallingForAllSubElements = false)
        {
            #region implementation
            var result = new SplParseResult();

            // 1. Validate that the necessary context is available.
            if (context.CurrentDocumentRelationship?.DocumentRelationshipID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot create CertificationProductLink: No DocumentRelationship in context for the establishment.");
                return result;
            }
            if (context.CurrentProductIdentifier?.ProductIdentifierID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot create CertificationProductLink: No ProductIdentifier in context for the product.");
                return result;
            }

            reportProgress?.Invoke($"Creating certification link in {context.FileNameInZip}");

            // 2. Create the link object from context.
            var link = new CertificationProductLink
            {
                DocumentRelationshipID = context.CurrentDocumentRelationship.DocumentRelationshipID.Value,
                ProductIdentifierID = context.CurrentProductIdentifier.ProductIdentifierID.Value
            };

            // 3. Get or create the record in the database.
            await getOrCreateCertificationProductLinkAsync(link, context);
            result.ProductElementsCreated++; // Re-using counter

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks if a CertificationProductLink already exists; if not, it creates a new record.
        /// </summary>
        /// <param name="link">The CertificationProductLink entity to get or create.</param>
        /// <param name="context">The parsing context for database access.</param>
        /// <returns>The existing or newly created CertificationProductLink entity.</returns>
        /// <seealso cref="CertificationProductLink"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<CertificationProductLink?> getOrCreateCertificationProductLinkAsync(CertificationProductLink link, SplParseContext context)
        {
            #region implementation
            if (context == null || context.ServiceProvider == null)
            {
                return null;
            }

            // Explicitly get the DbContext from the service provider
            var dbContext = context.GetDbContext();
            var repo = context.GetRepository<CertificationProductLink>();
            var dbSet = dbContext.Set<CertificationProductLink>();

            // Check for existence to avoid creating duplicate links.
            // The unique key for this junction table record is the combination of the two foreign keys.
            var existingLink = await dbSet.FirstOrDefaultAsync(l =>
                l.DocumentRelationshipID == link.DocumentRelationshipID &&
                l.ProductIdentifierID == link.ProductIdentifierID);

            if (existingLink != null)
            {
                // If the link already exists, log the finding and return the existing record.
                context?.Logger?.LogInformation(
                    "Found existing CertificationProductLink with ID {CertLinkId} for DocRelID {DocRelId} and ProdID {ProdId}",
                    existingLink.CertificationProductLinkID,
                    link.DocumentRelationshipID,
                    link.ProductIdentifierID);
                return existingLink;
            }

            // If the link is new, create it using the repository.
            await repo.CreateAsync(link);
            context?.Logger?.LogInformation(
                "Created new CertificationProductLink for DocRelID {DocRelId} and ProdID {ProdId}",
                link.DocumentRelationshipID,
                link.ProductIdentifierID);

            // Return the new link, which now has its database-assigned ID.
            return link;
            #endregion
        }
        #endregion
    }
}
