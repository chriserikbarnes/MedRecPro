using MedRecPro.Models;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Factory service interface for creating structured body view models from DTOs.
    /// Provides a contract for converting raw structured body data into complete
    /// view models with pre-computed rendering contexts and organizational states.
    /// </summary>
    /// <seealso cref="StructuredBodyViewModel"/>
    /// <seealso cref="StructuredBodyDto"/>
    /// <seealso cref="OrganizedSectionStructure"/>
    /// <seealso cref="ISectionHierarchyService"/>
    /// <seealso cref="IStructuredBodyService"/>
    public interface IStructuredBodyViewModelFactory
    {
        /**************************************************************/
        /// <summary>
        /// Creates a complete structured body view model from a DTO.
        /// Processes the raw structured body data to create organized sections,
        /// rendering contexts, and pre-computed organizational flags for efficient UI rendering.
        /// </summary>
        /// <param name="structuredBodyDto">The structured body DTO containing sections and hierarchy data</param>
        /// <returns>A complete view model ready for rendering with organized sections and contexts</returns>
        /// <seealso cref="StructuredBodyViewModel"/>
        /// <seealso cref="StructuredBodyDto"/>
        /// <seealso cref="OrganizedSectionStructure"/>
        /// <seealso cref="SectionRendering"/>
        /// <example>
        /// <code>
        /// var factory = serviceProvider.GetService&lt;IStructuredBodyViewModelFactory&gt;();
        /// var viewModel = await factory.CreateAsync(structuredBodyDto);
        /// 
        /// // Use the complete view model for rendering
        /// if (viewModel.HasStandaloneSections)
        /// {
        ///     RenderStandaloneSections(viewModel.StandaloneSectionContexts);
        /// }
        /// </code>
        /// </example>
        StructuredBodyViewModel Create(StructuredBodyDto structuredBodyDto);
    }

    /**************************************************************/
    /// <summary>
    /// Factory for creating structured body view models with pre-computed rendering contexts.
    /// Orchestrates multiple services to transform raw structured body data into complete
    /// view models optimized for user interface rendering with minimal real-time computation.
    /// </summary>
    /// <seealso cref="IStructuredBodyViewModelFactory"/>
    /// <seealso cref="StructuredBodyViewModel"/>
    /// <seealso cref="ISectionHierarchyService"/>
    /// <seealso cref="IStructuredBodyService"/>
    /// <seealso cref="OrganizedSectionStructure"/>
    /// <seealso cref="SectionRendering"/>
    /// <remarks>
    /// This factory combines the functionality of hierarchy and structured body services
    /// to create complete view models. It handles the coordination of section organization,
    /// context creation, and state computation to provide a single entry point for
    /// view model creation from raw DTOs.
    /// </remarks>
    public class StructuredBodyViewModelFactory : IStructuredBodyViewModelFactory
    {
        #region private fields

        /**************************************************************/
        /// <summary>
        /// Service for organizing sections into hierarchical structures.
        /// Used for core section organization logic and hierarchy building operations.
        /// </summary>
        /// <seealso cref="ISectionHierarchyService"/>
        private readonly ISectionHierarchyService _hierarchyService;

        /**************************************************************/
        /// <summary>
        /// Service for structured body utility operations.
        /// Used for checking organizational states and retrieving hierarchy relationships.
        /// </summary>
        /// <seealso cref="IStructuredBodyService"/>
        private readonly IStructuredBodyService _structuredBodyService;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the StructuredBodyViewModelFactory with required services.
        /// Sets up the factory with the necessary dependencies for creating complete view models.
        /// </summary>
        /// <param name="hierarchyService">Service for section hierarchy operations and organization</param>
        /// <param name="structuredBodyService">Service for structured body utility operations</param>
        /// <seealso cref="ISectionHierarchyService"/>
        /// <seealso cref="IStructuredBodyService"/>
        /// <example>
        /// <code>
        /// // Dependency injection registration
        /// services.AddScoped&lt;IStructuredBodyViewModelFactory, StructuredBodyViewModelFactory&gt;();
        /// 
        /// // Manual instantiation (not recommended)
        /// var factory = new StructuredBodyViewModelFactory(hierarchyService, structuredBodyService);
        /// </code>
        /// </example>
        public StructuredBodyViewModelFactory(
            ISectionHierarchyService hierarchyService,
            IStructuredBodyService structuredBodyService)
        {
            #region implementation

            _hierarchyService = hierarchyService ?? throw new ArgumentNullException(nameof(hierarchyService));
            _structuredBodyService = structuredBodyService ?? throw new ArgumentNullException(nameof(structuredBodyService));

            #endregion
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Synchronously creates a complete structured body view model from a DTO.
        /// Orchestrates section organization, context creation, and state computation
        /// to produce a view model optimized for efficient rendering operations.
        /// </summary>
        /// <param name="structuredBodyDto">The structured body DTO containing sections and hierarchy relationships</param>
        /// <returns>A complete view model with organized sections, rendering contexts, and pre-computed flags</returns>
        /// <seealso cref="StructuredBodyViewModel"/>
        /// <seealso cref="StructuredBodyDto"/>
        /// <seealso cref="OrganizedSectionStructure"/>
        /// <seealso cref="ISectionHierarchyService.OrganizeSections"/>
        /// <seealso cref="IStructuredBodyService.HasStandaloneSections"/>
        /// <seealso cref="IStructuredBodyService.HasHierarchicalSections"/>
        /// <seealso cref="Label.StructuredBody"/>
        /// <example>
        /// <code>
        /// var factory = serviceProvider.GetService&lt;IStructuredBodyViewModelFactory&gt;();
        /// var structuredBodyDto = GetStructuredBodyFromDatabase();
        /// var viewModel = await factory.CreateAsync(structuredBodyDto);
        /// 
        /// // View model is ready for immediate rendering
        /// Console.WriteLine($"Standalone sections: {viewModel.StandaloneSectionContexts.Count}");
        /// Console.WriteLine($"Hierarchical sections: {viewModel.HierarchicalSectionContexts.Count}");
        /// Console.WriteLine($"All sections in order: {viewModel.AllSectionContexts.Count}");
        /// 
        /// return View(viewModel);
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs all necessary preprocessing to create a complete view model.
        /// It coordinates multiple services to organize sections, create rendering contexts,
        /// and compute organizational flags, eliminating the need for real-time processing
        /// during UI rendering operations. The unified AllSectionContexts collection preserves
        /// the original document order while maintaining separate collections for backward compatibility.
        /// </remarks>
        public StructuredBodyViewModel Create(StructuredBodyDto structuredBodyDto)
        {
            #region implementation

            // Use hierarchy service for core organization logic - separates standalone from hierarchical sections
            var organizedSections = _hierarchyService.OrganizeSections(structuredBodyDto);

            // Get valid sections and create lookup dictionary for efficient access during context creation
            var allSections = _hierarchyService.GetValidSections(structuredBodyDto.Sections);
            var sectionLookup = _hierarchyService.CreateSectionLookup(allSections);

            // Create the base view model with core data and organizational flags
            var viewModel = new StructuredBodyViewModel
            {
                StructuredBody = structuredBodyDto,
                OrganizedSections = organizedSections,
                HasStandaloneSections = _structuredBodyService.HasStandaloneSections(organizedSections),
                HasHierarchicalSections = _structuredBodyService.HasHierarchicalSections(organizedSections)
            };

            // Create pre-computed rendering contexts for efficient UI rendering
            viewModel.StandaloneSectionContexts = createStandaloneSectionContexts(organizedSections);
            viewModel.HierarchicalSectionContexts = createHierarchicalSectionContexts(
                organizedSections, structuredBodyDto, sectionLookup);

            // Create unified section context list preserving original document order
            viewModel.AllSectionContexts = createAllSectionContexts(
                structuredBodyDto, organizedSections, sectionLookup);

            return viewModel;

            #endregion
        }

        #endregion

        #region private methods
   
        /**************************************************************/
        /// <summary>
        /// Creates a unified collection of section rendering contexts with complete N-level hierarchy.
        /// Builds complete SectionRendering objects that contain the full nested structure 
        /// rather than relying on separate Children collections.
        /// </summary>
        /// <param name="structuredBodyDto">The original structured body DTO containing section order information</param>
        /// <param name="organizedSections">The organized section structure with standalone and root classifications</param>
        /// <param name="sectionLookup">Dictionary for efficient section lookup during hierarchy building</param>
        /// <returns>Unified list of complete SectionRendering contexts with N-level nesting</returns>
        /// <seealso cref="SectionRendering"/>
        /// <seealso cref="StructuredBodyDto"/>
        /// <seealso cref="Label.Section"/>
        /// <seealso cref="Label.SectionHierarchy"/>
        /// <remarks>
        /// This method creates SectionRendering objects that contain the complete hierarchy
        /// structure. Each SectionRendering includes not just direct children, but the
        /// complete N-level nested structure required for proper document rendering.
        /// The recursive approach ensures all descendant levels are properly built.
        /// </remarks>
        private List<SectionRendering> createAllSectionContexts(
            StructuredBodyDto structuredBodyDto,
            OrganizedSectionStructure organizedSections,
            Dictionary<int, SectionDto> sectionLookup)
        {
            #region implementation

            var allContexts = new List<SectionRendering>();

            // Get hierarchy relationships needed for building child structures
            var hierarchies = _structuredBodyService.GetSectionHierarchies(structuredBodyDto);

            // Create lookup sets for quick section classification during iteration
            var standaloneSet = new HashSet<int>(
                organizedSections.StandaloneSections.Select(s => s.SectionID!.Value));

            var rootSet = new HashSet<int>(
                organizedSections.RootSections.Select(s => s.SectionID!.Value));

            // Identify child sections that should not be processed as top-level
            var childSectionIds = new HashSet<int>(hierarchies
                .Where(h => h != null
                    && h.ChildSectionID != null
                    && h.ChildSectionID.HasValue)
                .Select(h => h.ChildSectionID!.Value));

            // Create parent-to-children lookup for efficient hierarchy building
            // Use Distinct() to handle potential duplicate hierarchy entries (same parent-child pair)
            var parentToChildrenLookup = hierarchies
                .Where(h => h != null && h.ParentSectionID.HasValue && h.ChildSectionID.HasValue)
                .GroupBy(h => h.ParentSectionID!.Value)
                .ToDictionary(g => g.Key, g => g.Select(h => h.ChildSectionID!.Value).Distinct().ToList());

            // Process only top-level sections in their original order
            foreach (var section in structuredBodyDto.Sections
                .Where(s => s != null
                    && s.SectionID != null
                    && s.SectionID.HasValue
                    && !childSectionIds.Contains(s.SectionID.Value)))
            {
                var sectionId = section.SectionID!.Value;

                if (standaloneSet.Contains(sectionId))
                {
                    // Create standalone context - no children by definition
                    allContexts.Add(new SectionRendering
                    {
                        Section = section,
                        Children = new List<SectionDto>(), // Standalone sections have no children
                        HierarchicalChildren = new List<SectionRendering>(), // No hierarchical children either
                        IsStandalone = true
                    });
                }
                else if (rootSet.Contains(sectionId))
                {
                    // Create hierarchical context with complete N-level child structure
                    var hierarchicalChildren = buildHierarchicalChildrenRecursively(sectionId, parentToChildrenLookup, sectionLookup, hierarchies);
                    var flatChildren = flattenHierarchicalChildren(hierarchicalChildren);

                    allContexts.Add(new SectionRendering
                    {
                        Section = section,
                        Children = flatChildren, // Flat list for backward compatibility
                        HierarchicalChildren = hierarchicalChildren, // Complete N-level hierarchy
                        IsStandalone = false
                    });
                }
            }

            return allContexts;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recursively builds the complete N-level child section hierarchy as SectionRendering objects.
        /// </summary>
        /// <param name="parentSectionId">The ID of the parent section to build children for</param>
        /// <param name="parentToChildrenLookup">Dictionary mapping parent section IDs to their child section IDs</param>
        /// <param name="sectionLookup">Dictionary for efficient section lookup by ID</param>
        /// <param name="hierarchies">All hierarchy relationships for sorting</param>
        /// <returns>List of SectionRendering objects with their complete nested hierarchies</returns>
        private List<SectionRendering> buildHierarchicalChildrenRecursively(
            int parentSectionId,
            Dictionary<int, List<int>> parentToChildrenLookup,
            Dictionary<int, SectionDto> sectionLookup,
            List<SectionHierarchyDto> hierarchies)
        {
            var children = new List<SectionRendering>();

            // Check if this parent has any children
            if (!parentToChildrenLookup.TryGetValue(parentSectionId, out var childIds))
            {
                return children; // No children found
            }

            // Get display order information from hierarchies for sorting
            // Use GroupBy to handle potential duplicate hierarchy entries (same parent-child relationship)
            // by taking the first occurrence's sequence number
            var childOrderLookup = hierarchies
                .Where(h => h.ParentSectionID == parentSectionId && h.ChildSectionID.HasValue)
                .GroupBy(h => h.ChildSectionID!.Value)
                .ToDictionary(g => g.Key, g => g.First().SequenceNumber ?? int.MaxValue);

            foreach (var childId in childIds)
            {
                // Get the child section from lookup
                if (!sectionLookup.TryGetValue(childId, out var childSection))
                {
                    continue; // Skip if child section not found
                }

                // Recursively build children for this child section
                var grandchildren = buildHierarchicalChildrenRecursively(childId, parentToChildrenLookup, sectionLookup, hierarchies);

                // Create SectionRendering for this child with its complete hierarchy
                var childRendering = new SectionRendering
                {
                    Section = childSection,
                    HierarchicalChildren = grandchildren,
                    Children = flattenHierarchicalChildren(grandchildren), // Flat list of all descendants
                    IsStandalone = false
                };

                children.Add(childRendering);
            }

            // Sort children by display order from hierarchy information
            return children.OrderBy(c => childOrderLookup.TryGetValue(c.Section.SectionID ?? 0, out var order) ? order : int.MaxValue).ToList();
        }

        /**************************************************************/
        /// <summary>
        /// Flattens hierarchical children into a flat list for backward compatibility
        /// </summary>
        /// <param name="hierarchicalChildren">The hierarchical structure to flatten</param>
        /// <returns>Flat list of all SectionDto objects in the hierarchy</returns>
        private List<SectionDto> flattenHierarchicalChildren(List<SectionRendering> hierarchicalChildren)
        {
            var flatList = new List<SectionDto>();

            foreach (var child in hierarchicalChildren)
            {
                flatList.Add(child.Section);
                if (child.HierarchicalChildren.Any())
                {
                    flatList.AddRange(flattenHierarchicalChildren(child.HierarchicalChildren));
                }
            }

            return flatList;
        }

        /**************************************************************/
        /// <summary>
        /// Creates rendering contexts for standalone sections.
        /// Transforms standalone sections into rendering contexts with empty children
        /// and appropriate flags for individual section rendering scenarios.
        /// </summary>
        /// <param name="organizedSections">The organized section structure containing standalone sections</param>
        /// <returns>List of rendering contexts for standalone sections</returns>
        /// <seealso cref="OrganizedSectionStructure"/>
        /// <seealso cref="SectionRendering"/>
        /// <seealso cref="SectionDto"/>
        /// <example>
        /// <code>
        /// var contexts = createStandaloneSectionContexts(organizedSections);
        /// foreach(var context in contexts)
        /// {
        ///     Console.WriteLine($"Standalone section: {context.Section.Title}");
        ///     Debug.Assert(context.IsStandalone == true);
        ///     Debug.Assert(context.Children.Count == 0);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Standalone sections by definition have no children, so the Children collection
        /// is initialized as empty. The IsStandalone flag is set to true to indicate
        /// this context represents an independent section for rendering purposes.
        /// </remarks>
        private static List<SectionRendering> createStandaloneSectionContexts(
            OrganizedSectionStructure organizedSections)
        {
            #region implementation

            return organizedSections.StandaloneSections
                .Select(section => new SectionRendering
                {
                    Section = section,
                    Children = new List<SectionDto>(), // Standalone sections have no children
                    IsStandalone = true // Flag for rendering logic to handle as individual item
                }).ToList();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates rendering contexts for hierarchical sections with their complete child structures.
        /// Transforms root sections into rendering contexts by building their complete hierarchies
        /// using the hierarchy service for tree-based rendering scenarios.
        /// </summary>
        /// <param name="organizedSections">The organized section structure containing root sections</param>
        /// <param name="structuredBodyDto">The original structured body DTO containing hierarchy relationships</param>
        /// <param name="sectionLookup">Dictionary for efficient section lookup during hierarchy building</param>
        /// <returns>List of rendering contexts for hierarchical sections with populated children</returns>
        /// <seealso cref="OrganizedSectionStructure"/>
        /// <seealso cref="StructuredBodyDto"/>
        /// <seealso cref="SectionRendering"/>
        /// <seealso cref="ISectionHierarchyService.BuildChildSections"/>
        /// <seealso cref="IStructuredBodyService.GetSectionHierarchies"/>
        /// <example>
        /// <code>
        /// var contexts = createHierarchicalSectionContexts(organizedSections, structuredBodyDto, sectionLookup);
        /// foreach(var context in contexts)
        /// {
        ///     Console.WriteLine($"Root section: {context.Section.Title}");
        ///     Console.WriteLine($"Children count: {context.Children.Count}");
        ///     Debug.Assert(context.IsStandalone == false);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method leverages the hierarchy service to build complete child structures
        /// for each root section. The resulting contexts contain the full hierarchical
        /// data needed for tree-based rendering without requiring additional service calls.
        /// </remarks>
        private List<SectionRendering> createHierarchicalSectionContexts(
            OrganizedSectionStructure organizedSections,
            StructuredBodyDto structuredBodyDto,
            Dictionary<int, SectionDto> sectionLookup)
        {
            #region implementation

            // Get hierarchy relationships needed for building child structures
            var hierarchies = _structuredBodyService.GetSectionHierarchies(structuredBodyDto);

            return organizedSections.RootSections
                .Select(rootSection => new SectionRendering
                {
                    Section = rootSection,
                    // Use hierarchy service to build complete child structure recursively
                    Children = _hierarchyService.BuildChildSections(
                        rootSection.SectionID!.Value,
                        hierarchies,
                        sectionLookup
                    ),
                    IsStandalone = false // Flag for rendering logic to handle as hierarchical structure
                }).ToList();

            #endregion
        }

        #endregion
    }
}