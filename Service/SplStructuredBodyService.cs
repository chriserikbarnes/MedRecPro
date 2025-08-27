using MedRecPro.Models;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Interface for structured body operations that complement section hierarchy services.
    /// Provides utility methods for checking section organization states and retrieving
    /// section hierarchy data from structured body objects.
    /// </summary>
    /// <seealso cref="OrganizedSectionStructure"/>
    /// <seealso cref="StructuredBodyDto"/>
    /// <seealso cref="SectionHierarchyDto"/>
    /// <seealso cref="ISectionHierarchyService"/>
    public interface IStructuredBodyService
    {
        /**************************************************************/
        /// <summary>
        /// Determines whether the organized sections contain any standalone sections.
        /// Standalone sections are those that exist independently without parent-child relationships.
        /// </summary>
        /// <param name="organizedSections">The organized section structure to check</param>
        /// <returns>True if standalone sections exist, false otherwise</returns>
        /// <seealso cref="OrganizedSectionStructure"/>
        /// <seealso cref="SectionDto"/>
        /// <example>
        /// <code>
        /// var hasStandalone = service.HasStandaloneSections(organizedStructure);
        /// if (hasStandalone)
        /// {
        ///     RenderStandaloneSections(organizedStructure.StandaloneSections);
        /// }
        /// </code>
        /// </example>
        bool HasStandaloneSections(OrganizedSectionStructure organizedSections);

        /**************************************************************/
        /// <summary>
        /// Determines whether the organized sections contain any hierarchical sections.
        /// Hierarchical sections are root sections that have child sections organized in parent-child relationships.
        /// </summary>
        /// <param name="organizedSections">The organized section structure to check</param>
        /// <returns>True if hierarchical sections exist, false otherwise</returns>
        /// <seealso cref="OrganizedSectionStructure"/>
        /// <seealso cref="SectionDto"/>
        /// <example>
        /// <code>
        /// var hasHierarchy = service.HasHierarchicalSections(organizedStructure);
        /// if (hasHierarchy)
        /// {
        ///     RenderHierarchicalSections(organizedStructure.RootSections);
        /// }
        /// </code>
        /// </example>
        bool HasHierarchicalSections(OrganizedSectionStructure organizedSections);

        /**************************************************************/
        /// <summary>
        /// Retrieves the section hierarchy relationships from a structured body.
        /// Returns the hierarchy data that defines parent-child relationships between sections.
        /// </summary>
        /// <param name="structuredBody">The structured body containing section hierarchies</param>
        /// <returns>List of section hierarchy relationships, or empty list if none exist</returns>
        /// <seealso cref="StructuredBodyDto"/>
        /// <seealso cref="SectionHierarchyDto"/>
        /// <example>
        /// <code>
        /// var hierarchies = service.GetSectionHierarchies(structuredBody);
        /// foreach(var hierarchy in hierarchies)
        /// {
        ///     ProcessHierarchyRelationship(hierarchy.ParentSectionID, hierarchy.ChildSectionID);
        /// }
        /// </code>
        /// </example>
        List<SectionHierarchyDto> GetSectionHierarchies(StructuredBodyDto structuredBody);
    }

    /**************************************************************/
    /// <summary>
    /// Service for structured body operations that don't duplicate hierarchy service functionality.
    /// Provides utility methods for checking organizational states and retrieving hierarchy data
    /// without overlapping with the core hierarchy building logic in ISectionHierarchyService.
    /// </summary>
    /// <seealso cref="IStructuredBodyService"/>
    /// <seealso cref="OrganizedSectionStructure"/>
    /// <seealso cref="StructuredBodyDto"/>
    /// <seealso cref="SectionHierarchyDto"/>
    /// <seealso cref="ISectionHierarchyService"/>
    /// <remarks>
    /// This service focuses on simple utility operations and data retrieval,
    /// while leaving complex hierarchy building to the dedicated SectionHierarchyService.
    /// It serves as a lightweight complement for common organizational queries.
    /// </remarks>
    public class StructuredBodyService : IStructuredBodyService
    {
        #region public methods

        /**************************************************************/
        /// <summary>
        /// Determines whether the organized sections contain any standalone sections.
        /// Standalone sections are those that exist independently without parent-child relationships.
        /// </summary>
        /// <param name="organizedSections">The organized section structure to check for standalone sections</param>
        /// <returns>True if standalone sections exist and the collection is not empty, false otherwise</returns>
        /// <seealso cref="OrganizedSectionStructure"/>
        /// <seealso cref="SectionDto"/>
        /// <example>
        /// <code>
        /// var service = new StructuredBodyService();
        /// var hasStandalone = service.HasStandaloneSections(organizedStructure);
        /// 
        /// if (hasStandalone)
        /// {
        ///     Console.WriteLine($"Found {organizedStructure.StandaloneSections.Count} standalone sections");
        ///     RenderStandaloneSections(organizedStructure.StandaloneSections);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs a null-safe check on the StandaloneSections collection.
        /// It returns false if the collection is null or empty, true if it contains any items.
        /// </remarks>
        public bool HasStandaloneSections(OrganizedSectionStructure organizedSections)
        {
            #region implementation

            // Null-safe check: ensure collection exists and contains at least one item
            return organizedSections.StandaloneSections?.Any() == true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether the organized sections contain any hierarchical sections.
        /// Hierarchical sections are root sections that have child sections organized in parent-child relationships.
        /// </summary>
        /// <param name="organizedSections">The organized section structure to check for hierarchical sections</param>
        /// <returns>True if root sections exist and the collection is not empty, false otherwise</returns>
        /// <seealso cref="OrganizedSectionStructure"/>
        /// <seealso cref="SectionDto"/>
        /// <example>
        /// <code>
        /// var service = new StructuredBodyService();
        /// var hasHierarchy = service.HasHierarchicalSections(organizedStructure);
        /// 
        /// if (hasHierarchy)
        /// {
        ///     Console.WriteLine($"Found {organizedStructure.RootSections.Count} root sections");
        ///     foreach(var rootSection in organizedStructure.RootSections)
        ///     {
        ///         RenderHierarchicalSection(rootSection);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs a null-safe check on the RootSections collection.
        /// Root sections represent the top level of hierarchical structures and may contain child sections.
        /// </remarks>
        public bool HasHierarchicalSections(OrganizedSectionStructure organizedSections)
        {
            #region implementation

            // Null-safe check: ensure collection exists and contains at least one root section
            return organizedSections.RootSections?.Any() == true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the section hierarchy relationships from a structured body.
        /// Returns the hierarchy data that defines parent-child relationships between sections,
        /// providing a safe way to access hierarchy information without null reference exceptions.
        /// </summary>
        /// <param name="structuredBody">The structured body containing section hierarchies</param>
        /// <returns>List of section hierarchy relationships, or empty list if the collection is null</returns>
        /// <seealso cref="StructuredBodyDto"/>
        /// <seealso cref="SectionHierarchyDto"/>
        /// <example>
        /// <code>
        /// var service = new StructuredBodyService();
        /// var hierarchies = service.GetSectionHierarchies(structuredBody);
        /// 
        /// Console.WriteLine($"Found {hierarchies.Count} hierarchy relationships");
        /// 
        /// foreach(var hierarchy in hierarchies)
        /// {
        ///     Console.WriteLine($"Parent: {hierarchy.ParentSectionID}, Child: {hierarchy.ChildSectionID}");
        ///     ProcessHierarchyRelationship(hierarchy);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method provides null-safe access to section hierarchies, returning an empty list
        /// instead of null to prevent null reference exceptions in consuming code.
        /// The returned list can be safely enumerated even when no hierarchies exist.
        /// </remarks>
        public List<SectionHierarchyDto> GetSectionHierarchies(StructuredBodyDto structuredBody)
        {
            #region implementation

            // Return the hierarchy collection or an empty list if null to prevent null reference exceptions
            return structuredBody.SectionHierarchies ?? new List<SectionHierarchyDto>();

            #endregion
        }

        #endregion
    }
}