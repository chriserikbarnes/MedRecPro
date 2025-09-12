using MedRecPro.Models;

namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents a rendered author with child organizations and their business operations
    /// for SPL document author section generation. Contains pre-computed properties
    /// for efficient template rendering of hierarchical author structures.
    /// </summary>
    /// <seealso cref="Label.DocumentAuthor"/>
    /// <seealso cref="Label.Organization"/>
    /// <seealso cref="Label.BusinessOperation"/>
    /// <seealso cref="DocumentAuthorDto"/>
    /// <seealso cref="OrganizationDto"/>
    /// <seealso cref="BusinessOperationDto"/>
    public class AuthorRendering
    {
        #region core properties

        /**************************************************************/
        /// <summary>
        /// The source document author DTO containing base author information.
        /// </summary>
        /// <seealso cref="DocumentAuthorDto"/>
        public required DocumentAuthorDto Author { get; set; }

        /**************************************************************/
        /// <summary>
        /// Pre-computed list of child organizations with their business operations
        /// for hierarchical author rendering in SPL documents.
        /// </summary>
        /// <seealso cref="ChildOrganizationRendering"/>
        /// <seealso cref="Label.Organization"/>
        public List<ChildOrganizationRendering> ChildOrganizations { get; set; } = new();

        #endregion

        #region pre-computed rendering properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed author organization name for rendering.
        /// Fallback to empty string if organization or name is null.
        /// </summary>
        /// <seealso cref="Label.Organization"/>
        public string AuthorOrganizationName { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed author organization identifiers for rendering.
        /// </summary>
        /// <seealso cref="OrganizationIdentifierDto"/>
        /// <seealso cref="Label.OrganizationIdentifier"/>
        public List<OrganizationIdentifierDto> AuthorIdentifiers { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Pre-computed author type for rendering (e.g., Labeler, FDA, NCPDP).
        /// </summary>
        /// <seealso cref="DocumentAuthorDto"/>
        public string AuthorType { get; set; } = string.Empty;

        #endregion

        #region convenience properties

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether author has child organizations.
        /// </summary>
        public bool HasChildOrganizations => ChildOrganizations?.Any() == true;

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether author has identifiers.
        /// </summary>
        public bool HasAuthorIdentifiers => AuthorIdentifiers?.Any() == true;

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether author organization name is available.
        /// </summary>
        public bool HasAuthorOrganizationName => !string.IsNullOrWhiteSpace(AuthorOrganizationName);

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether author type is available.
        /// </summary>
        public bool HasAuthorType => !string.IsNullOrWhiteSpace(AuthorType);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Represents a child organization within an author hierarchy with business operations
    /// for SPL document rendering. Contains organization details and associated business operations.
    /// </summary>
    /// <seealso cref="Label.Organization"/>
    /// <seealso cref="Label.BusinessOperation"/>
    /// <seealso cref="OrganizationDto"/>
    /// <seealso cref="BusinessOperationDto"/>
    public class ChildOrganizationRendering
    {
        #region core properties

        /**************************************************************/
        /// <summary>
        /// The source organization DTO containing base organization information.
        /// </summary>
        /// <seealso cref="OrganizationDto"/>
        public required OrganizationDto Organization { get; set; }

        /**************************************************************/
        /// <summary>
        /// List of business operations performed by this organization.
        /// </summary>
        /// <seealso cref="BusinessOperationRendering"/>
        /// <seealso cref="Label.BusinessOperation"/>
        public List<BusinessOperationRendering> BusinessOperations { get; set; } = new();

        #endregion

        #region pre-computed rendering properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed organization name for rendering.
        /// </summary>
        /// <seealso cref="OrganizationDto"/>
        public string OrganizationName { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed organization identifiers for rendering.
        /// </summary>
        /// <seealso cref="OrganizationIdentifierDto"/>
        public List<OrganizationIdentifierDto> OrganizationIdentifiers { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Pre-computed confidentiality flag for rendering.
        /// </summary>
        /// <seealso cref="OrganizationDto"/>
        public bool IsConfidential { get; set; }

        #endregion

        #region convenience properties

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether organization has business operations.
        /// </summary>
        public bool HasBusinessOperations => BusinessOperations?.Any() == true;

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether organization has identifiers.
        /// </summary>
        public bool HasOrganizationIdentifiers => OrganizationIdentifiers?.Any() == true;

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether organization name is available.
        /// </summary>
        public bool HasOrganizationName => !string.IsNullOrWhiteSpace(OrganizationName);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Represents a business operation with associated products for SPL rendering.
    /// Contains operation details and linked product information for performance elements.
    /// </summary>
    /// <seealso cref="Label.BusinessOperation"/>
    /// <seealso cref="BusinessOperationDto"/>
    /// <seealso cref="FacilityProductLinkDto"/>
    public class BusinessOperationRendering
    {
        #region core properties

        /**************************************************************/
        /// <summary>
        /// The source business operation DTO containing operation information.
        /// </summary>
        /// <seealso cref="BusinessOperationDto"/>
        public required BusinessOperationDto BusinessOperation { get; set; }

        /**************************************************************/
        /// <summary>
        /// List of facility product links associated with this business operation.
        /// Used to generate product elements within performance sections.
        /// </summary>
        /// <seealso cref="FacilityProductLinkDto"/>
        /// <seealso cref="Label.FacilityProductLink"/>
        public List<FacilityProductLinkDto> ProductLinks { get; set; } = new();

        #endregion

        #region pre-computed rendering properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed operation code for rendering (e.g., C25391, C84731).
        /// </summary>
        /// <seealso cref="BusinessOperationDto"/>
        public string OperationCode { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed operation code system for rendering.
        /// Typically "2.16.840.1.113883.3.26.1.1".
        /// </summary>
        /// <seealso cref="BusinessOperationDto"/>
        public string OperationCodeSystem { get; set; } = string.Empty;

        /**************************************************************/
        /// <summary>
        /// Pre-computed operation display name for rendering (e.g., ANALYSIS, PACK, LABEL).
        /// </summary>
        /// <seealso cref="BusinessOperationDto"/>
        public string OperationDisplayName { get; set; } = string.Empty;

        #endregion

        #region convenience properties

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether operation has product links.
        /// </summary>
        public bool HasProductLinks => ProductLinks?.Any() == true;

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether operation code is available.
        /// </summary>
        public bool HasOperationCode => !string.IsNullOrWhiteSpace(OperationCode);

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether operation display name is available.
        /// </summary>
        public bool HasOperationDisplayName => !string.IsNullOrWhiteSpace(OperationDisplayName);

        /**************************************************************/
        /// <summary>
        /// Convenience flag indicating whether operation code system is available.
        /// </summary>
        public bool HasOperationCodeSystem => !string.IsNullOrWhiteSpace(OperationCodeSystem);

        #endregion
    }
}