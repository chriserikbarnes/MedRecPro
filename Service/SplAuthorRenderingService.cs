using MedRecPro.Models;
using Microsoft.EntityFrameworkCore;
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
        /// Uses contextual identifier filtering to preserve original SPL document structure.
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
    /// Uses DocumentRelationshipIdentifier data to preserve contextual identifier information.
    /// </summary>
    /// <seealso cref="IAuthorRenderingService"/>
    /// <seealso cref="AuthorRendering"/>
    /// <seealso cref="Label.DocumentAuthor"/>
    /// <seealso cref="Label.Organization"/>
    /// <seealso cref="Label.BusinessOperation"/>
    /// <seealso cref="Label.DocumentRelationshipIdentifier"/>
    /// <remarks>
    /// This service processes the complex relationships defined in SPL specifications
    /// to create hierarchical author structures with child organizations and their
    /// associated business operations. It uses DocumentRelationshipIdentifier data
    /// to ensure that only the identifiers that appeared at each specific hierarchy
    /// level are included in the rendering, enabling accurate XML output that matches
    /// the original SPL document structure.
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
        /// associating business operations with organizations, and filtering identifiers
        /// using DocumentRelationshipIdentifier context to preserve original SPL structure.
        /// Applies contextual filtering to both author-level and child-level identifiers.
        /// </summary>
        /// <param name="author">The document author to prepare for rendering</param>
        /// <param name="allRelationships">All document relationships to find child organizations</param>
        /// <param name="allBusinessOperations">All business operations to associate with organizations</param>
        /// <param name="allFacilityProductLinks">All facility product links for business operations</param>
        /// <returns>Fully prepared AuthorRendering object with hierarchical structure</returns>
        /// <seealso cref="AuthorRendering"/>
        /// <seealso cref="DocumentAuthorDto"/>
        /// <seealso cref="buildChildOrganizations"/>
        /// <seealso cref="filterAuthorLevelIdentifiers"/>
        /// <example>
        /// <code>
        /// var authorRendering = service.PrepareForRendering(
        ///     author, relationships, businessOps, productLinks);
        /// // authorRendering now contains complete hierarchical structure
        /// // with contextually filtered identifiers at all levels
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

            // Filter author-level identifiers using contextual approach
            var filteredAuthorIdentifiers = filterAuthorLevelIdentifiers(
                author,
                allRelationships ?? new List<DocumentRelationshipDto>());

            // Create base AuthorRendering object with pre-computed properties
            var authorRendering = new AuthorRendering
            {
                Author = author,
                AuthorOrganizationName = author.Organization?.OrganizationName ?? string.Empty,
                AuthorIdentifiers = filteredAuthorIdentifiers, // Use filtered identifiers
                AuthorType = author.AuthorType ?? string.Empty
            };

            // Build child organizations with their business operations and contextual identifiers
            authorRendering.ChildOrganizations = buildChildOrganizations(
                author,
                allRelationships ?? new List<DocumentRelationshipDto>(),
                allBusinessOperations ?? new List<BusinessOperationDto>(),
                allFacilityProductLinks ?? new List<FacilityProductLinkDto>());

            _logger.LogDebug("Prepared author {AuthorId} with {ChildCount} child organizations and {AuthorIdentifierCount} author-level identifiers",
                author.DocumentAuthorID, authorRendering.ChildOrganizations.Count, filteredAuthorIdentifiers.Count);

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
        /// Uses DocumentRelationshipIdentifier data to filter identifiers contextually,
        /// ensuring only identifiers that appeared at each specific hierarchy level are included.
        /// </summary>
        /// <param name="author">The parent author to find children for</param>
        /// <param name="allRelationships">All relationships to search through</param>
        /// <param name="allBusinessOperations">All business operations to associate</param>
        /// <param name="allFacilityProductLinks">All product links to include</param>
        /// <returns>List of child organization renderings with contextual identifiers</returns>
        /// <remarks>
        /// This method implements the core logic for preserving SPL document structure
        /// by using DocumentRelationshipIdentifier to determine which identifiers appeared
        /// at each level. For example, if Henry Schein, Inc. has DUNS 012430880 at the parent
        /// level and DUNS 830995189 at the child level, this ensures each level shows only
        /// its specific identifier.
        /// </remarks>
        /// <seealso cref="ChildOrganizationRendering"/>
        /// <seealso cref="buildBusinessOperationsForOrganization"/>
        /// <seealso cref="filterContextualIdentifiers"/>
        /// <seealso cref="Label.DocumentRelationshipIdentifier"/>
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
                            r.ChildOrganization != null &&
                            (r.RelationshipLevel ?? 1) > 1)
                .ToList();

            _logger.LogDebug("Found {RelationshipCount} child relationships for author {AuthorId}",
                childRelationships.Count, author.DocumentAuthorID);

            // Build child organization renderings
            foreach (var relationship in childRelationships)
            {
                var childOrg = relationship.ChildOrganization;
                if (childOrg == null) continue;

                // Filter identifiers to only those that appeared at this hierarchy level
                var contextualIdentifiers = filterContextualIdentifiers(
                    relationship,
                    childOrg.Identifiers ?? new List<OrganizationIdentifierDto>());

                var childOrgRendering = new ChildOrganizationRendering
                {
                    Organization = childOrg,
                    OrganizationName = childOrg.OrganizationName ?? string.Empty,
                    OrganizationIdentifiers = contextualIdentifiers,
                    IsConfidential = childOrg.IsConfidential ?? false
                };

                _logger.LogDebug(
                    "Organization {OrgId} has {TotalIdentifiers} total identifiers, {ContextualIdentifiers} contextual for this relationship",
                    childOrg.OrganizationID,
                    childOrg.Identifiers?.Count ?? 0,
                    contextualIdentifiers.Count);

                // Build business operations for this child organization
                childOrgRendering.BusinessOperations = buildBusinessOperationsForOrganization(
                    relationship,
                    allBusinessOperations,
                    allFacilityProductLinks);

                childOrganizations.Add(childOrgRendering);
            }

            // CHANGE: Child orgs must have business operations
            //childOrganizations = childOrganizations
            //    ?.Where(x => x.HasBusinessOperations == true)
            //    ?.ToList();

            return childOrganizations ?? new List<ChildOrganizationRendering>();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Filters author-level identifiers to only those that should appear at the top
        /// (author/labeler) level in the SPL document hierarchy, using DocumentRelationshipIdentifier
        /// data to preserve original document structure.
        /// </summary>
        /// <param name="author">The document author containing the organization</param>
        /// <param name="allRelationships">All document relationships to find the author-level relationship</param>
        /// <returns>Filtered list of identifiers that appeared at the author/labeler level</returns>
        /// <remarks>
        /// This method finds the top-level relationship where the author organization appears
        /// as the child with no parent (representing the Document → Author relationship at level 0),
        /// and uses its RelationshipIdentifiers to determine which specific identifiers
        /// appeared at the author/labeler level in the original SPL document.
        /// 
        /// Example: If Henry Schein, Inc. appears as labeler with DUNS 012430880 at the top level
        /// (in a level 0 relationship where ParentOrganizationID is NULL), this method ensures
        /// only 012430880 is shown at the author level, matching the original document structure.
        /// 
        /// The method looks for relationships where:
        /// - The author's organization is the ChildOrganization
        /// - ParentOrganizationID is NULL (indicating top-level/document relationship)
        /// - OR RelationshipLevel is 0
        /// - The relationship has RelationshipIdentifiers (indicating parsed identifier context)
        /// </remarks>
        /// <seealso cref="DocumentRelationshipDto"/>
        /// <seealso cref="DocumentRelationshipIdentifierDto"/>
        /// <seealso cref="OrganizationIdentifierDto"/>
        /// <seealso cref="Label.DocumentRelationshipIdentifier"/>
        private List<OrganizationIdentifierDto> filterAuthorLevelIdentifiers(
            DocumentAuthorDto author,
            List<DocumentRelationshipDto> allRelationships)
        {
            #region implementation

            var authorOrgId = author.OrganizationID;
            if (!authorOrgId.HasValue)
            {
                _logger.LogWarning("Author {AuthorId} has no OrganizationID", author.DocumentAuthorID);
                return new List<OrganizationIdentifierDto>();
            }

            // Find the top-level relationship where this organization is the CHILD
            // and there is NO PARENT (ParentOrganizationID is NULL) or RelationshipLevel is 1
            // This represents the Document → Author Organization relationship
            var authorLevelRelationship = allRelationships
                .Where(r => r.ChildOrganizationID == authorOrgId.Value &&
                           (r.ParentOrganizationID == null || r.RelationshipLevel == Constant.LABEL_REGISTRANT) &&
                           r.RelationshipIdentifiers != null &&
                           r.RelationshipIdentifiers.Any())
                .OrderBy(r => r.RelationshipLevel ?? 0)
                .FirstOrDefault();

            // Alternative: if no relationship found with identifiers, try finding ANY level 0 relationship
            if (authorLevelRelationship == null)
            {
                authorLevelRelationship = allRelationships
                    .Where(r => r.ChildOrganizationID == authorOrgId.Value &&
                               (r.ParentOrganizationID == null || r.RelationshipLevel == 0))
                    .OrderBy(r => r.RelationshipLevel ?? 0)
                    .FirstOrDefault();
            }

            if (authorLevelRelationship == null)
            {
                _logger.LogWarning(
                    "No level 0 relationship found where author organization {OrgId} is child with no parent. " +
                    "Cannot determine contextual author-level identifiers. " +
                    "Returning empty list to preserve hierarchy integrity.",
                    authorOrgId.Value);

                return new List<OrganizationIdentifierDto>();
            }

            // If relationship exists but has no RelationshipIdentifiers, return empty list
            if (authorLevelRelationship.RelationshipIdentifiers == null ||
                !authorLevelRelationship.RelationshipIdentifiers.Any())
            {
                _logger.LogWarning(
                    "Found author-level relationship {RelationshipId} but it has no RelationshipIdentifiers. " +
                    "This may indicate missing DocumentRelationshipIdentifier data during parsing. " +
                    "Returning empty list to preserve hierarchy integrity.",
                    authorLevelRelationship.DocumentRelationshipID);

                return new List<OrganizationIdentifierDto>();
            }

            // Get all identifiers for the author organization
            var allOrgIdentifiers = author.Organization?.Identifiers ?? new List<OrganizationIdentifierDto>();

            // Extract the identifier IDs that appeared at the author/labeler level
            var authorLevelIdentifierIds = authorLevelRelationship.RelationshipIdentifiers
                .Select(ri => ri.OrganizationIdentifierID)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();

            if (!authorLevelIdentifierIds.Any())
            {
                _logger.LogWarning(
                    "RelationshipIdentifiers exist for author-level relationship {RelationshipId} " +
                    "but contain no valid OrganizationIdentifierIDs",
                    authorLevelRelationship.DocumentRelationshipID);

                return new List<OrganizationIdentifierDto>();
            }

            // Filter to only identifiers that appeared at the author level
            var filteredIdentifiers = allOrgIdentifiers
                .Where(oi => oi.OrganizationIdentifierID.HasValue &&
                            authorLevelIdentifierIds.Contains(oi.OrganizationIdentifierID.Value))
                .ToList();

            _logger.LogDebug(
                "Filtered {FilteredCount} author-level identifiers from {TotalCount} total identifiers " +
                "for author {AuthorId} using level 0 relationship {RelationshipId}",
                filteredIdentifiers.Count,
                allOrgIdentifiers.Count,
                author.DocumentAuthorID,
                authorLevelRelationship.DocumentRelationshipID);

            return filteredIdentifiers;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Filters organization identifiers to only those that appeared at a specific
        /// document relationship hierarchy level using DocumentRelationshipIdentifier data.
        /// This preserves the original SPL document structure where the same organization
        /// may have different identifiers at different hierarchy levels.
        /// </summary>
        /// <param name="relationship">The document relationship providing context</param>
        /// <param name="allOrgIdentifiers">All identifiers for the organization</param>
        /// <returns>Filtered list of identifiers that appeared at this relationship level</returns>
        /// <remarks>
        /// This method is critical for accurate SPL rendering. It uses the RelationshipIdentifiers
        /// collection from DocumentRelationshipDto, which contains DocumentRelationshipIdentifier
        /// records created during parsing. These records preserve which specific identifiers
        /// (e.g., DUNS numbers) were present at each hierarchy level in the original XML.
        /// 
        /// Example: Henry Schein, Inc. normalized to one Organization record with both
        /// DUNS 012430880 and 830995189. This method ensures the parent level shows only
        /// 012430880 and the child level shows only 830995189, matching the original structure.
        /// </remarks>
        /// <seealso cref="DocumentRelationshipDto"/>
        /// <seealso cref="DocumentRelationshipIdentifierDto"/>
        /// <seealso cref="OrganizationIdentifierDto"/>
        /// <seealso cref="Label.DocumentRelationshipIdentifier"/>
        private List<OrganizationIdentifierDto> filterContextualIdentifiers(
            DocumentRelationshipDto relationship,
            List<OrganizationIdentifierDto> allOrgIdentifiers)
        {
            #region implementation

            // If no relationship identifiers exist, return empty list to prevent
            // showing all identifiers (which would break the hierarchy)
            if (relationship.RelationshipIdentifiers == null ||
                !relationship.RelationshipIdentifiers.Any())
            {
                _logger.LogWarning(
                    "No RelationshipIdentifiers found for relationship {RelationshipId}. " +
                    "This may indicate missing DocumentRelationshipIdentifier data. " +
                    "Returning empty identifier list to preserve hierarchy integrity.",
                    relationship.DocumentRelationshipID);

                return new List<OrganizationIdentifierDto>();
            }

            // Extract the identifier IDs that appeared at this relationship level
            var contextualIdentifierIds = relationship.RelationshipIdentifiers
                .Select(ri => ri.OrganizationIdentifierID)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();

            if (!contextualIdentifierIds.Any())
            {
                _logger.LogWarning(
                    "RelationshipIdentifiers exist but contain no valid OrganizationIdentifierIDs " +
                    "for relationship {RelationshipId}",
                    relationship.DocumentRelationshipID);

                return new List<OrganizationIdentifierDto>();
            }

            // Filter the organization's identifiers to only those that appeared at this level
            var contextualIdentifiers = allOrgIdentifiers
                .Where(oi => oi.OrganizationIdentifierID.HasValue &&
                            contextualIdentifierIds.Contains(oi.OrganizationIdentifierID.Value))
                .ToList();

            _logger.LogDebug(
                "Filtered {FilteredCount} contextual identifiers from {TotalCount} total identifiers " +
                "for relationship {RelationshipId}",
                contextualIdentifiers.Count,
                allOrgIdentifiers.Count,
                relationship.DocumentRelationshipID);

            return contextualIdentifiers;

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