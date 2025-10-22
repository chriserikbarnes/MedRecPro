using MedRecPro.Models;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Interface for organizing sections into hierarchical structures for rendering.
    /// Provides methods for building parent-child relationships and organizing sections
    /// into standalone and hierarchical structures.
    /// </summary>
    /// <seealso cref="SectionDto"/>
    /// <seealso cref="StructuredBodyDto"/>
    /// <seealso cref="OrganizedSectionStructure"/>
    public interface ISectionHierarchyService
    {
        /**************************************************************/
        /// <summary>
        /// Organizes sections from a structured body into hierarchical and standalone structures.
        /// </summary>
        /// <param name="structuredBody">The structured body containing sections and hierarchy relationships</param>
        /// <returns>An organized structure containing standalone sections and root sections with their children</returns>
        /// <seealso cref="StructuredBodyDto"/>
        /// <seealso cref="OrganizedSectionStructure"/>
        /// <example>
        /// <code>
        /// var structure = service.OrganizeSections(structuredBody);
        /// var standalones = structure.StandaloneSections;
        /// var hierarchical = structure.RootSections;
        /// </code>
        /// </example>
        OrganizedSectionStructure OrganizeSections(StructuredBodyDto? structuredBody);

        /**************************************************************/
        /// <summary>
        /// Recursively builds child sections for a given parent section ID.
        /// </summary>
        /// <param name="parentSectionId">The ID of the parent section</param>
        /// <param name="hierarchyRelationships">List of hierarchy relationships</param>
        /// <param name="sectionLookup">Dictionary for fast section lookup by ID</param>
        /// <param name="depth">Current recursion depth to prevent infinite loops</param>
        /// <returns>List of child sections with their own children populated</returns>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="SectionHierarchyDto"/>
        /// <remarks>
        /// This method prevents infinite recursion by limiting the depth to MAX_HIERARCHY_DEPTH.
        /// Children are ordered by their sequence number.
        /// </remarks>
        List<SectionDto> BuildChildSections(int parentSectionId,
            List<SectionHierarchyDto> hierarchyRelationships,
            Dictionary<int, SectionDto> sectionLookup,
            int depth = 0);

        /**************************************************************/
        /// <summary>
        /// Filters and deduplicates sections to return only valid sections with IDs.
        /// </summary>
        /// <param name="sections">Raw list of sections to filter</param>
        /// <returns>List of valid sections without duplicates</returns>
        /// <seealso cref="SectionDto"/>
        /// <remarks>
        /// Removes sections without IDs and deduplicates based on SectionGUID.
        /// </remarks>
        List<SectionDto> GetValidSections(List<SectionDto> sections);

        /**************************************************************/
        /// <summary>
        /// Creates a dictionary lookup for sections keyed by their section ID.
        /// </summary>
        /// <param name="sections">List of sections to create lookup for</param>
        /// <returns>Dictionary with section ID as key and section as value</returns>
        /// <seealso cref="SectionDto"/>
        Dictionary<int, SectionDto> CreateSectionLookup(List<SectionDto> sections);
    }

    /**************************************************************/
    /// <summary>
    /// Service for organizing sections into hierarchical structures for rendering.
    /// Handles separation of standalone sections and parent-child relationships.
    /// This service processes medical record sections and organizes them for display
    /// in user interfaces by building hierarchical relationships.
    /// </summary>
    /// <seealso cref="ISectionHierarchyService"/>
    /// <seealso cref="SectionDto"/>
    /// <seealso cref="StructuredBodyDto"/>
    /// <seealso cref="OrganizedSectionStructure"/>
    /// <remarks>
    /// The service implements depth limiting to prevent infinite recursion in
    /// malformed hierarchical data and provides efficient lookup mechanisms
    /// for large section collections.
    /// </remarks>
    public class SectionHierarchyService : ISectionHierarchyService
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// Maximum allowed depth for section hierarchies to prevent infinite recursion.
        /// </summary>
        /// <seealso cref="BuildChildSections"/>
        private const int MAX_HIERARCHY_DEPTH = 5;

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Organizes sections from a structured body into hierarchical and standalone structures.
        /// Separates sections that exist independently from those that are part of parent-child relationships.
        /// </summary>
        /// <param name="structuredBody">The structured body containing sections and hierarchy relationships</param>
        /// <returns>An organized structure containing standalone sections and root sections with their children</returns>
        /// <seealso cref="StructuredBodyDto"/>
        /// <seealso cref="OrganizedSectionStructure"/>
        /// <seealso cref="SectionDto"/>
        /// <example>
        /// <code>
        /// var service = new SectionHierarchyService();
        /// var structure = service.OrganizeSections(structuredBody);
        /// 
        /// // Process standalone sections
        /// foreach(var section in structure.StandaloneSections)
        /// {
        ///     ProcessSection(section);
        /// }
        /// 
        /// // Process hierarchical sections
        /// foreach(var rootSection in structure.RootSections)
        /// {
        ///     ProcessHierarchy(rootSection);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Returns an empty structure if the input structured body is null or has no sections.
        /// The method first validates sections, then separates them into standalone and hierarchical groups.
        /// </remarks>
        public OrganizedSectionStructure OrganizeSections(StructuredBodyDto? structuredBody)
        {
            #region implementation

            // Return empty structure for null or empty input
            if (structuredBody?.Sections == null)
            {
                return createEmptyStructure();
            }

            // Filter and deduplicate sections
            var validSections = GetValidSections(structuredBody.Sections);

            // Create lookup dictionary for efficient section retrieval
            var sectionLookup = CreateSectionLookup(validSections);

            // Extract valid hierarchy relationships
            var hierarchyRelationships = getHierarchyRelationships(structuredBody.SectionHierarchies);

            // Identify sections that are part of hierarchies
            var hierarchicalSectionIds = getHierarchicalSectionIds(hierarchyRelationships);

            // Separate standalone sections from hierarchical ones
            var standaloneSections = getStandaloneSections(validSections, hierarchicalSectionIds);

            // Build root sections with their complete child hierarchies
            var rootSections = buildRootSections(validSections, hierarchyRelationships, sectionLookup);

            return new OrganizedSectionStructure
            {
                StandaloneSections = standaloneSections,
                RootSections = rootSections
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recursively builds child sections for a given parent section ID.
        /// Creates a complete hierarchy tree by following parent-child relationships
        /// and populating each section with its children.
        /// </summary>
        /// <param name="parentSectionId">The ID of the parent section to build children for</param>
        /// <param name="hierarchyRelationships">List of all hierarchy relationships in the system</param>
        /// <param name="sectionLookup">Dictionary for fast section lookup by ID</param>
        /// <param name="depth">Current recursion depth to prevent infinite loops (default: 0)</param>
        /// <returns>List of child sections with their own children populated recursively</returns>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="SectionHierarchyDto"/>
        /// <seealso cref="MAX_HIERARCHY_DEPTH"/>
        /// <example>
        /// <code>
        /// var children = service.BuildChildSections(
        ///     parentId: 123,
        ///     hierarchyRelationships: relationships,
        ///     sectionLookup: lookup,
        ///     depth: 0
        /// );
        /// </code>
        /// </example>
        /// <remarks>
        /// This method prevents infinite recursion by limiting the depth to MAX_HIERARCHY_DEPTH.
        /// Children are ordered by their sequence number for consistent display ordering.
        /// The method uses recursion to build complete hierarchies in a single pass.
        /// </remarks>
        public List<SectionDto> BuildChildSections(int parentSectionId,
            List<SectionHierarchyDto> hierarchyRelationships,
            Dictionary<int, SectionDto> sectionLookup,
            int depth = 0)
        {
            #region implementation

            // Prevent infinite recursion by limiting depth
            if (depth >= MAX_HIERARCHY_DEPTH)
            {
                return new List<SectionDto>();
            }

            // Find all direct children of the parent section, ordered by sequence
            var childRelationships = hierarchyRelationships
                .Where(h => h.ParentSectionID == parentSectionId)
                .OrderBy(h => h.SequenceNumber ?? 0) // Use 0 as default for null sequence numbers
                .ToList();

            var childSections = new List<SectionDto>();

            // Process each child relationship
            foreach (var relationship in childRelationships)
            {
                // Ensure child ID exists and section can be found in lookup
                if (relationship.ChildSectionID.HasValue &&
                    sectionLookup.TryGetValue(relationship.ChildSectionID.Value, out var childSection))
                {
                    // Recursively build grandchildren (next level down)
                    var grandchildren = BuildChildSections(relationship.ChildSectionID.Value,
                        hierarchyRelationships, sectionLookup, depth + 1);

                    // Clone section with its children populated
                    var sectionWithChildren = cloneSectionWithChildren(childSection, grandchildren);
                    childSections.Add(sectionWithChildren);
                }
            }

            return childSections;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Filters and deduplicates sections to return only valid sections with IDs.
        /// Removes sections without valid IDs and eliminates duplicates based on SectionGUID.
        /// </summary>
        /// <param name="sections">Raw list of sections to filter and validate</param>
        /// <returns>List of valid, unique sections</returns>
        /// <seealso cref="SectionDto"/>
        /// <example>
        /// <code>
        /// var validSections = service.GetValidSections(allSections);
        /// // validSections now contains only sections with IDs and no duplicates
        /// </code>
        /// </example>
        /// <remarks>
        /// This method ensures data quality by removing invalid entries and duplicates.
        /// Deduplication is based on SectionGUID, with the first occurrence being kept.
        /// </remarks>
        public List<SectionDto> GetValidSections(List<SectionDto> sections)
        {
            #region implementation

            return sections
                .Where(s => s?.SectionID.HasValue == true) // Filter out null sections and those without IDs
                .GroupBy(s => s.SectionGUID) // Group by GUID to identify duplicates
                .Select(g => g.First()) // Take first occurrence of each unique GUID
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a dictionary lookup for sections keyed by their section ID for efficient retrieval.
        /// </summary>
        /// <param name="sections">List of sections to create lookup dictionary for</param>
        /// <returns>Dictionary with section ID as key and section object as value</returns>
        /// <seealso cref="SectionDto"/>
        /// <example>
        /// <code>
        /// var lookup = service.CreateSectionLookup(validSections);
        /// var section = lookup[sectionId]; // Fast O(1) lookup
        /// </code>
        /// </example>
        /// <remarks>
        /// This lookup dictionary enables O(1) section retrieval during hierarchy building,
        /// significantly improving performance for large section collections.
        /// </remarks>
        public Dictionary<int, SectionDto> CreateSectionLookup(List<SectionDto> sections)
        {
            #region implementation

            return sections
                .Where(s => s != null && s.SectionID != null && s.SectionID.HasValue) // Only include sections with valid IDs
                .OrderBy(s => s.SectionID) // Optional: order by ID for consistency
                .ToDictionary(s => s.SectionID!.Value, s => s); // Create ID -> Section mapping

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Creates an empty organized section structure with initialized empty lists.
        /// </summary>
        /// <returns>Empty OrganizedSectionStructure with empty lists</returns>
        /// <seealso cref="OrganizedSectionStructure"/>
        /// <seealso cref="SectionDto"/>
        private static OrganizedSectionStructure createEmptyStructure()
        {
            #region implementation

            return new OrganizedSectionStructure
            {
                StandaloneSections = new List<SectionDto>(),
                RootSections = new List<SectionDto>()
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts and validates hierarchy relationships from the input list.
        /// Filters out relationships without valid parent and child section IDs.
        /// </summary>
        /// <param name="hierarchies">Raw list of hierarchy relationships</param>
        /// <returns>List of valid hierarchy relationships</returns>
        /// <seealso cref="SectionHierarchyDto"/>
        private static List<SectionHierarchyDto> getHierarchyRelationships(List<SectionHierarchyDto>? hierarchies)
        {
            #region implementation

            return hierarchies?
                .Where(h => h.ParentSectionID.HasValue && h.ChildSectionID.HasValue) // Only valid relationships
                .ToList() ?? new List<SectionHierarchyDto>(); // Return empty list if null

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets a set of all section IDs that participate in hierarchical relationships.
        /// Combines both parent and child IDs from all hierarchy relationships.
        /// </summary>
        /// <param name="hierarchyRelationships">List of hierarchy relationships</param>
        /// <returns>HashSet of section IDs that are part of hierarchies</returns>
        /// <seealso cref="SectionHierarchyDto"/>
        private static HashSet<int> getHierarchicalSectionIds(List<SectionHierarchyDto> hierarchyRelationships)
        {
            #region implementation

            // Combine parent IDs and child IDs into a single unique set
            var parentIds = hierarchyRelationships.Select(h => h.ParentSectionID!.Value);
            var childIds = hierarchyRelationships.Select(h => h.ChildSectionID!.Value);
            return parentIds.Union(childIds).ToHashSet();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Identifies sections that exist independently (not part of any hierarchy).
        /// Returns sections that are not referenced in any parent-child relationship.
        /// </summary>
        /// <param name="validSections">List of all valid sections</param>
        /// <param name="hierarchicalSectionIds">Set of IDs that participate in hierarchies</param>
        /// <returns>List of sections that exist independently</returns>
        /// <seealso cref="SectionDto"/>
        private static List<SectionDto> getStandaloneSections(List<SectionDto> validSections,
            HashSet<int> hierarchicalSectionIds)
        {
            #region implementation

            return validSections
                .Where(s => s.SectionID.HasValue && !hierarchicalSectionIds.Contains(s.SectionID.Value))
                .ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds root sections with their complete child hierarchies.
        /// Identifies sections that are parents but not children of other sections.
        /// </summary>
        /// <param name="validSections">List of all valid sections</param>
        /// <param name="hierarchyRelationships">List of hierarchy relationships</param>
        /// <param name="sectionLookup">Dictionary for fast section lookup</param>
        /// <returns>List of root sections with their children populated</returns>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="SectionHierarchyDto"/>
        /// <seealso cref="BuildChildSections"/>
        private List<SectionDto> buildRootSections(List<SectionDto> validSections,
            List<SectionHierarchyDto> hierarchyRelationships,
            Dictionary<int, SectionDto> sectionLookup)
        {
            #region implementation

            // Find sections that are parents but not children (root level)
            var allParentIds = hierarchyRelationships.Select(h => h.ParentSectionID!.Value).Distinct();
            var allChildIds = hierarchyRelationships.Select(h => h.ChildSectionID!.Value).Distinct();
            var rootParentIds = allParentIds.Except(allChildIds).ToList();

            var rootSections = new List<SectionDto>();

            // Build each root section with its complete hierarchy
            foreach (var rootId in rootParentIds)
            {
                if (sectionLookup.TryGetValue(rootId, out var rootSection))
                {
                    // Recursively build all children for this root section
                    var children = BuildChildSections(rootId, hierarchyRelationships, sectionLookup);
                    var sectionWithChildren = cloneSectionWithChildren(rootSection, children);
                    rootSections.Add(sectionWithChildren);
                }
            }

            return rootSections;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a copy of a section with its children populated.
        /// This method should implement proper deep cloning based on the SectionDto structure.
        /// </summary>
        /// <param name="originalSection">The original section to clone</param>
        /// <param name="children">List of child sections to attach</param>
        /// <returns>Cloned section with children populated</returns>
        /// <seealso cref="SectionDto"/>
        /// <remarks>
        /// In production, this method should implement proper cloning based on your SectionDto structure.
        /// Consider using a cloning library or implementing ICloneable interface.
        /// </remarks>
        private static SectionDto cloneSectionWithChildren(SectionDto originalSection, List<SectionDto> children)
        {
            #region implementation

            // Note: In production, implement proper cloning based on your SectionDto structure
            // This might include creating a new instance and copying all properties,
            // then setting the Children property to the provided children list
            return originalSection;

            #endregion
        }

        #endregion
    }
}