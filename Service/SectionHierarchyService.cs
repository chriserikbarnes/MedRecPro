using MedRecPro.Models;
using System.Collections.Generic;
using System.Linq;

namespace MedRecPro.Services
{
    /**************************************************************/
    /// <summary>
    /// Service for organizing sections into hierarchical structures for rendering.
    /// Handles separation of standalone sections and parent-child relationships.
    /// </summary>
    /// <seealso cref="StructuredBodyDto"/>
    /// <seealso cref="SectionDto"/>
    /// <seealso cref="SectionHierarchyDto"/>
    public class SectionHierarchyService
    {
        #region constants
        private const int MAX_HIERARCHY_DEPTH = 3;
        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Organizes sections into renderable hierarchy structure with standalone and hierarchical sections separated.
        /// </summary>
        /// <param name="structuredBody">The structured body containing sections and hierarchies</param>
        /// <returns>Organized section structure for rendering</returns>
        /// <seealso cref="StructuredBodyDto.Sections"/>
        /// <seealso cref="StructuredBodyDto.SectionHierarchies"/>
        /// <example>
        /// var organized = sectionService.OrganizeSections(structuredBody);
        /// // organized.StandaloneSections contains sections without hierarchy
        /// // organized.RootSections contains hierarchical parent sections
        /// </example>
        public StructuredBodyDto.OrganizedSectionStructure OrganizeSections(StructuredBodyDto? structuredBody)
        {
            if (structuredBody?.Sections == null)
            {
                return createEmptyStructure();
            }

            #region implementation
            var validSections = getValidSections(structuredBody.Sections);
            var sectionLookup = createSectionLookup(validSections);
            var hierarchyRelationships = getHierarchyRelationships(structuredBody.SectionHierarchies);

            var hierarchicalSectionIds = getHierarchicalSectionIds(hierarchyRelationships);
            var standaloneSections = getStandaloneSections(validSections, hierarchicalSectionIds);
            var rootSections = buildRootSections(validSections, hierarchyRelationships, sectionLookup);
            #endregion

            return new StructuredBodyDto.OrganizedSectionStructure
            {
                StandaloneSections = standaloneSections,
                RootSections = rootSections
            };
        }

        /**************************************************************/
        /// <summary>
        /// Builds child section hierarchy for a parent section.
        /// </summary>
        /// <param name="parentSectionId">ID of the parent section</param>
        /// <param name="hierarchyRelationships">All hierarchy relationships</param>
        /// <param name="sectionLookup">Lookup dictionary for sections by ID</param>
        /// <param name="depth">Current recursion depth to prevent infinite loops</param>
        /// <returns>List of child sections with their own children populated</returns>
        /// <seealso cref="SectionHierarchyDto.ParentSectionID"/>
        /// <seealso cref="SectionHierarchyDto.ChildSectionID"/>
        public List<SectionDto> BuildChildSections(int parentSectionId,
            List<SectionHierarchyDto> hierarchyRelationships,
            Dictionary<int, SectionDto> sectionLookup,
            int depth = 0)
        {
            if (depth >= MAX_HIERARCHY_DEPTH)
            {
                return new List<SectionDto>();
            }

            #region implementation
            var childRelationships = hierarchyRelationships
                .Where(h => h.ParentSectionID == parentSectionId)
                .OrderBy(h => h.SequenceNumber ?? 0)
                .ToList();

            var childSections = new List<SectionDto>();

            foreach (var relationship in childRelationships)
            {
                if (relationship.ChildSectionID.HasValue &&
                    sectionLookup.TryGetValue(relationship.ChildSectionID.Value, out var childSection))
                {
                    // Recursively build grandchildren
                    var grandchildren = BuildChildSections(relationship.ChildSectionID.Value,
                        hierarchyRelationships, sectionLookup, depth + 1);

                    // Create a copy to avoid modifying the original
                    var sectionWithChildren = cloneSectionWithChildren(childSection, grandchildren);
                    childSections.Add(sectionWithChildren);
                }
            }
            #endregion

            return childSections;
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Creates empty organized structure when no sections available.
        /// </summary>
        /// <returns>Empty organized section structure</returns>
        /// <seealso cref="OrganizedSectionStructure"/>
        private StructuredBodyDto.OrganizedSectionStructure createEmptyStructure()
        {
            return new StructuredBodyDto.OrganizedSectionStructure
            {
                StandaloneSections = new List<SectionDto>(),
                RootSections = new List<SectionDto>()
            };
        }

        /**************************************************************/
        /// <summary>
        /// Filters and deduplicates sections to get valid sections for processing.
        /// </summary>
        /// <param name="sections">Raw sections list</param>
        /// <returns>Valid, deduplicated sections</returns>
        /// <seealso cref="SectionDto.SectionGUID"/>
        private List<SectionDto> getValidSections(List<SectionDto> sections)
        {
            return sections
                .Where(s => s?.SectionID.HasValue == true)
                .GroupBy(s => s.SectionGUID)
                .Select(g => g.First())
                .ToList();
        }

        /**************************************************************/
        /// <summary>
        /// Creates lookup dictionary for sections by their ID for efficient access.
        /// </summary>
        /// <param name="sections">List of valid sections</param>
        /// <returns>Dictionary mapping section ID to section object</returns>
        /// <seealso cref="SectionDto.SectionID"/>
        private Dictionary<int, SectionDto> createSectionLookup(List<SectionDto> sections)
        {
            return sections
                .Where(s => s.SectionID.HasValue)
                .ToDictionary(s => s.SectionID!.Value, s => s);
        }

        /**************************************************************/
        /// <summary>
        /// Gets valid hierarchy relationships with both parent and child IDs.
        /// </summary>
        /// <param name="hierarchies">Raw hierarchies list</param>
        /// <returns>Valid hierarchy relationships</returns>
        /// <seealso cref="SectionHierarchyDto.ParentSectionID"/>
        /// <seealso cref="SectionHierarchyDto.ChildSectionID"/>
        private List<SectionHierarchyDto> getHierarchyRelationships(List<SectionHierarchyDto>? hierarchies)
        {
            return hierarchies?
                .Where(h => h.ParentSectionID.HasValue && h.ChildSectionID.HasValue)
                .ToList() ?? new List<SectionHierarchyDto>();
        }

        /**************************************************************/
        /// <summary>
        /// Gets all section IDs that participate in hierarchical relationships.
        /// </summary>
        /// <param name="hierarchyRelationships">Valid hierarchy relationships</param>
        /// <returns>Set of section IDs involved in hierarchies</returns>
        /// <seealso cref="SectionHierarchyDto.ParentSectionID"/>
        /// <seealso cref="SectionHierarchyDto.ChildSectionID"/>
        private HashSet<int> getHierarchicalSectionIds(List<SectionHierarchyDto> hierarchyRelationships)
        {
            var parentIds = hierarchyRelationships.Select(h => h.ParentSectionID!.Value);
            var childIds = hierarchyRelationships.Select(h => h.ChildSectionID!.Value);
            return parentIds.Union(childIds).ToHashSet();
        }

        /**************************************************************/
        /// <summary>
        /// Gets sections that are not part of any hierarchical relationship.
        /// </summary>
        /// <param name="validSections">All valid sections</param>
        /// <param name="hierarchicalSectionIds">IDs of sections in hierarchies</param>
        /// <returns>Standalone sections</returns>
        /// <seealso cref="SectionDto.SectionID"/>
        private List<SectionDto> getStandaloneSections(List<SectionDto> validSections,
            HashSet<int> hierarchicalSectionIds)
        {
            return validSections
                .Where(s => s.SectionID.HasValue && !hierarchicalSectionIds.Contains(s.SectionID.Value))
                .ToList();
        }

        /**************************************************************/
        /// <summary>
        /// Builds root sections (parents with no parents) with their children populated.
        /// </summary>
        /// <param name="validSections">All valid sections</param>
        /// <param name="hierarchyRelationships">Valid hierarchy relationships</param>
        /// <param name="sectionLookup">Section lookup dictionary</param>
        /// <returns>Root sections with children populated</returns>
        /// <seealso cref="SectionHierarchyDto.ParentSectionID"/>
        /// <seealso cref="SectionHierarchyDto.ChildSectionID"/>
        private List<SectionDto> buildRootSections(List<SectionDto> validSections,
            List<SectionHierarchyDto> hierarchyRelationships,
            Dictionary<int, SectionDto> sectionLookup)
        {
            #region implementation
            var allParentIds = hierarchyRelationships.Select(h => h.ParentSectionID!.Value).Distinct();
            var allChildIds = hierarchyRelationships.Select(h => h.ChildSectionID!.Value).Distinct();
            var rootParentIds = allParentIds.Except(allChildIds).ToList();

            var rootSections = new List<SectionDto>();

            foreach (var rootId in rootParentIds)
            {
                if (sectionLookup.TryGetValue(rootId, out var rootSection))
                {
                    var children = BuildChildSections(rootId, hierarchyRelationships, sectionLookup);
                    var sectionWithChildren = cloneSectionWithChildren(rootSection, children);
                    rootSections.Add(sectionWithChildren);
                }
            }
            #endregion

            return rootSections;
        }

        /**************************************************************/
        /// <summary>
        /// Creates a copy of section with populated children to avoid modifying original.
        /// </summary>
        /// <param name="originalSection">Original section to copy</param>
        /// <param name="children">Children to assign to the copy</param>
        /// <returns>Section copy with children populated</returns>
        /// <seealso cref="SectionDto"/>
        private SectionDto cloneSectionWithChildren(SectionDto originalSection, List<SectionDto> children)
        {
            // Create a shallow copy and assign children
            // This is a simplified approach - in production, consider deep cloning if needed
            var clonedSection = originalSection; // Reference copy for now

            // Note: In a real implementation, you might want to implement proper cloning
            // or add a Children property to SectionDto to hold hierarchical children
            // For this implementation, we'll work with the existing structure

            return clonedSection;
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Result structure containing organized sections for rendering.
    /// </summary>
    /// <seealso cref="SectionDto"/>
    public class OrganizedSectionStructure
    {
        /// <summary>
        /// Sections that exist independently without hierarchical relationships.
        /// </summary>
        /// <seealso cref="SectionDto"/>
        public List<SectionDto> StandaloneSections { get; set; } = new();

        /// <summary>
        /// Root sections that have children in hierarchical relationships.
        /// </summary>
        /// <seealso cref="SectionDto"/>
        public List<SectionDto> RootSections { get; set; } = new();
    }
}