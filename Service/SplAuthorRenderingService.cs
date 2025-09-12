using MedRecPro.Models;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Interface for preparing author data with child organizations and business operations
    /// for SPL document rendering. Provides hierarchical author rendering functionality
    /// with pre-computed properties for efficient template processing.
    /// </summary>
    /// <seealso cref="Label.DocumentAuthor"/>
    /// <seealso cref="Label.Organization"/>
    /// <seealso cref="Label.BusinessOperation"/>
    /// <seealso cref="AuthorRendering"/>
    /// <seealso cref="DocumentAuthorDto"/>
    public interface IAuthorRenderingService
    {
        #region core methods

        /**************************************************************/
        /// <summary>
        /// Prepares a DocumentAuthorDto for rendering with child organizations and business operations.
        /// Builds the complete hierarchical structure including child organizations,
        /// their business operations, and associated product links for SPL author sections.
        /// </summary>
        /// <param name="author">The document author to prepare for rendering</param>
        /// <param name="allRelationships">All document relationships to find child organizations</param>
        /// <param name="allBusinessOperations">All business operations to associate with organizations</param>
        /// <param name="allFacilityProductLinks">All facility product links for business operations</param>
        /// <returns>Fully prepared AuthorRendering object with hierarchical structure</returns>
        /// <seealso cref="AuthorRendering"/>
        /// <seealso cref="DocumentAuthorDto"/>
        /// <seealso cref="DocumentRelationshipDto"/>
        /// <seealso cref="BusinessOperationDto"/>
        /// <seealso cref="FacilityProductLinkDto"/>
        AuthorRendering PrepareForRendering(
            DocumentAuthorDto author,
            List<DocumentRelationshipDto> allRelationships,
            List<BusinessOperationDto> allBusinessOperations,
            List<FacilityProductLinkDto> allFacilityProductLinks);

        /**************************************************************/
        /// <summary>
        /// Prepares multiple authors for rendering with optimized processing.
        /// Processes all authors simultaneously to minimize data traversal
        /// and provide consistent hierarchical structures.
        /// </summary>
        /// <param name="authors">The document authors to prepare for rendering</param>
        /// <param name="allRelationships">All document relationships to find child organizations</param>
        /// <param name="allBusinessOperations">All business operations to associate with organizations</param>
        /// <param name="allFacilityProductLinks">All facility product links for business operations</param>
        /// <returns>List of fully prepared AuthorRendering objects</returns>
        /// <seealso cref="AuthorRendering"/>
        /// <seealso cref="DocumentAuthorDto"/>
        List<AuthorRendering> PrepareAuthorsForRendering(
            List<DocumentAuthorDto> authors,
            List<DocumentRelationshipDto> allRelationships,
            List<BusinessOperationDto> allBusinessOperations,
            List<FacilityProductLinkDto> allFacilityProductLinks);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Service for preparing author data with child organizations and business operations
    /// for SPL document rendering. Handles the complex hierarchical relationships between
    /// authors, organizations, business operations, and product links for optimal template processing.
    /// </summary>
    /// <seealso cref="IAuthorRenderingService"/>
    /// <seealso cref="AuthorRendering"/>
    /// <seealso cref="Label.DocumentAuthor"/>
    /// <seealso cref="Label.Organization"/>
    /// <seealso cref="Label.BusinessOperation"/>
    /// <remarks>
    /// This service processes the complex relationships defined in SPL specifications
    /// to create hierarchical author structures with child organizations and their
    /// associated business operations. It optimizes data processing for template rendering
    /// by pre-computing all necessary properties and relationships.
    /// </remarks>
    public class AuthorRenderingService : IAuthorRenderingService
    {
        #region private fields

        /**************************************************************/
        /// <summary>
        /// Logger instance for recording author rendering operations and diagnostic information.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger _logger;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the AuthorRenderingService with logging capability.
        /// Sets up the service with diagnostic logging for author processing operations.
        /// </summary>
        /// <param name="logger">Logger instance for operation tracking and diagnostics</param>
        /// <seealso cref="ILogger"/>
        /// <exception cref="ArgumentNullException">Thrown when logger parameter is null</exception>
        public AuthorRenderingService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Prepares a DocumentAuthorDto for rendering with child organizations and business operations.
        /// Builds the complete hierarchical structure by traversing document relationships,
        /// associating business operations with organizations, and linking product information.
        /// </summary>
        /// <param name="author">The document author to prepare for rendering</param>
        /// <param name="allRelationships">All document relationships to find child organizations</param>
        /// <param name="allBusinessOperations">All business operations to associate with organizations</param>
        /// <param name="allFacilityProductLinks">All facility product links for business operations</param>
        /// <returns>Fully prepared AuthorRendering object with hierarchical structure</returns>
        /// <seealso cref="AuthorRendering"/>
        /// <seealso cref="DocumentAuthorDto"/>
        /// <seealso cref="buildChildOrganizations"/>
        /// <example>
        /// <code>
        /// var authorRendering = service.PrepareForRendering(
        ///     author, relationships, businessOps, productLinks);
        /// // authorRendering now contains complete hierarchical structure
        /// </code>
        /// </example>
        public AuthorRendering PrepareForRendering(
            DocumentAuthorDto author,
            List<DocumentRelationshipDto> allRelationships,
            List<BusinessOperationDto> allBusinessOperations,
            List<FacilityProductLinkDto> allFacilityProductLinks)
        {
            #region implementation

            if (author == null)
                throw new ArgumentNullException(nameof(author));

            _logger.LogDebug("Preparing author {AuthorId} for rendering", author.DocumentAuthorID);

            // Create base AuthorRendering object with pre-computed properties
            var authorRendering = new AuthorRendering
            {
                Author = author,
                AuthorOrganizationName = author.Organization?.OrganizationName ?? string.Empty,
                AuthorIdentifiers = author.Organization?.Identifiers ?? new List<OrganizationIdentifierDto>(),
                AuthorType = author.AuthorType ?? string.Empty
            };

            // Build child organizations with their business operations
            authorRendering.ChildOrganizations = buildChildOrganizations(
                author,
                allRelationships ?? new List<DocumentRelationshipDto>(),
                allBusinessOperations ?? new List<BusinessOperationDto>(),
                allFacilityProductLinks ?? new List<FacilityProductLinkDto>());

            _logger.LogDebug("Prepared author {AuthorId} with {ChildCount} child organizations",
                author.DocumentAuthorID, authorRendering.ChildOrganizations.Count);

            return authorRendering;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Prepares multiple authors for rendering with optimized processing.
        /// Processes all authors simultaneously to minimize data traversal overhead
        /// while maintaining individual author hierarchical integrity.
        /// </summary>
        /// <param name="authors">The document authors to prepare for rendering</param>
        /// <param name="allRelationships">All document relationships to find child organizations</param>
        /// <param name="allBusinessOperations">All business operations to associate with organizations</param>
        /// <param name="allFacilityProductLinks">All facility product links for business operations</param>
        /// <returns>List of fully prepared AuthorRendering objects</returns>
        /// <seealso cref="AuthorRendering"/>
        /// <seealso cref="PrepareForRendering"/>
        public List<AuthorRendering> PrepareAuthorsForRendering(
            List<DocumentAuthorDto> authors,
            List<DocumentRelationshipDto> allRelationships,
            List<BusinessOperationDto> allBusinessOperations,
            List<FacilityProductLinkDto> allFacilityProductLinks)
        {
            #region implementation

            if (authors == null || !authors.Any())
                return new List<AuthorRendering>();

            _logger.LogDebug("Preparing {AuthorCount} authors for rendering", authors.Count);

            var authorRenderings = new List<AuthorRendering>();

            // Process each author with the complete dataset for optimal performance
            foreach (var author in authors)
            {
                var authorRendering = PrepareForRendering(
                    author,
                    allRelationships,
                    allBusinessOperations,
                    allFacilityProductLinks);

                authorRenderings.Add(authorRendering);
            }

            return authorRenderings;

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Builds child organizations for an author by traversing document relationships
        /// and associating business operations with each child organization.
        /// </summary>
        /// <param name="author">The parent author to find children for</param>
        /// <param name="allRelationships">All relationships to search through</param>
        /// <param name="allBusinessOperations">All business operations to associate</param>
        /// <param name="allFacilityProductLinks">All product links to include</param>
        /// <returns>List of child organization renderings</returns>
        /// <seealso cref="ChildOrganizationRendering"/>
        /// <seealso cref="buildBusinessOperationsForOrganization"/>
        private List<ChildOrganizationRendering> buildChildOrganizations(
            DocumentAuthorDto author,
            List<DocumentRelationshipDto> allRelationships,
            List<BusinessOperationDto> allBusinessOperations,
            List<FacilityProductLinkDto> allFacilityProductLinks)
        {
            #region implementation

            var childOrganizations = new List<ChildOrganizationRendering>();

            // Find relationships where this author's organization is the parent
            var authorOrgId = author.OrganizationID;
            if (!authorOrgId.HasValue)
                return childOrganizations;

            var childRelationships = allRelationships
                .Where(r => r.ParentOrganizationID == authorOrgId.Value &&
                           r.ChildOrganization != null)
                .ToList();

            _logger.LogDebug("Found {RelationshipCount} child relationships for author {AuthorId}",
                childRelationships.Count, author.DocumentAuthorID);

            // Build child organization renderings
            foreach (var relationship in childRelationships)
            {
                var childOrg = relationship.ChildOrganization;
                if (childOrg == null) continue;

                var childOrgRendering = new ChildOrganizationRendering
                {
                    Organization = childOrg,
                    OrganizationName = childOrg.OrganizationName ?? string.Empty,
                    OrganizationIdentifiers = childOrg.Identifiers ?? new List<OrganizationIdentifierDto>(),
                    IsConfidential = childOrg.IsConfidential ?? false
                };

                // Build business operations for this child organization
                childOrgRendering.BusinessOperations = buildBusinessOperationsForOrganization(
                    relationship,
                    allBusinessOperations,
                    allFacilityProductLinks);

                childOrganizations.Add(childOrgRendering);
            }

            return childOrganizations;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds business operations for a specific organization relationship
        /// including associated facility product links for complete operation context.
        /// </summary>
        /// <param name="relationship">The document relationship containing the organization</param>
        /// <param name="allBusinessOperations">All business operations to filter</param>
        /// <param name="allFacilityProductLinks">All product links to associate</param>
        /// <returns>List of business operation renderings for the organization</returns>
        /// <seealso cref="BusinessOperationRendering"/>
        /// <seealso cref="DocumentRelationshipDto"/>
        private List<BusinessOperationRendering> buildBusinessOperationsForOrganization(
            DocumentRelationshipDto relationship,
            List<BusinessOperationDto> allBusinessOperations,
            List<FacilityProductLinkDto> allFacilityProductLinks)
        {
            #region implementation

            var businessOperations = new List<BusinessOperationRendering>();

            // Find business operations for this relationship
            var relationshipId = relationship.DocumentRelationshipID;
            if (!relationshipId.HasValue)
                return businessOperations;

            var orgBusinessOperations = allBusinessOperations
                .Where(bo => bo.DocumentRelationshipID == relationshipId.Value)
                .ToList();

            _logger.LogDebug("Found {OperationCount} business operations for relationship {RelationshipId}",
                orgBusinessOperations.Count, relationshipId.Value);

            // Build business operation renderings
            foreach (var businessOp in orgBusinessOperations)
            {
                var businessOpRendering = new BusinessOperationRendering
                {
                    BusinessOperation = businessOp,
                    OperationCode = businessOp.OperationCode ?? string.Empty,
                    OperationCodeSystem = businessOp.OperationCodeSystem ?? string.Empty,
                    OperationDisplayName = businessOp.OperationDisplayName ?? string.Empty
                };

                // Find product links for this business operation
                businessOpRendering.ProductLinks = findProductLinksForOperation(
                    relationshipId.Value,
                    allFacilityProductLinks);

                businessOperations.Add(businessOpRendering);
            }

            return businessOperations;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds facility product links associated with a document relationship
        /// for inclusion in business operation performance elements.
        /// </summary>
        /// <param name="relationshipId">The document relationship ID to find links for</param>
        /// <param name="allFacilityProductLinks">All product links to search</param>
        /// <returns>List of facility product links for the relationship</returns>
        /// <seealso cref="FacilityProductLinkDto"/>
        private List<FacilityProductLinkDto> findProductLinksForOperation(
            int relationshipId,
            List<FacilityProductLinkDto> allFacilityProductLinks)
        {
            #region implementation

            var productLinks = allFacilityProductLinks
                .Where(fpl => fpl.DocumentRelationshipID == relationshipId)
                .ToList();

            _logger.LogDebug("Found {ProductLinkCount} product links for relationship {RelationshipId}",
                productLinks.Count, relationshipId);

            return productLinks;

            #endregion
        }

        #endregion
    }
}