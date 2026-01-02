using MedRecProImportClass.Service;

namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Result structure containing organized sections for rendering.
    /// Provides a separation between standalone sections and hierarchical sections
    /// to enable different rendering strategies for each type of section organization.
    /// Moved to Models namespace to avoid duplication across services.
    /// </summary>
    /// <seealso cref="SectionDto"/>
    /// <seealso cref="ISectionHierarchyService"/>
    /// <seealso cref="IStructuredBodyService"/>
    /// <seealso cref="StructuredBodyViewModel"/>
    /// <example>
    /// <code>
    /// var organizedStructure = new OrganizedSectionStructure
    /// {
    ///     StandaloneSections = standaloneSections,
    ///     RootSections = rootSections
    /// };
    /// 
    /// // Process each type separately
    /// if (organizedStructure.StandaloneSections.Any())
    /// {
    ///     RenderStandaloneSections(organizedStructure.StandaloneSections);
    /// }
    /// 
    /// if (organizedStructure.RootSections.Any())
    /// {
    ///     RenderHierarchicalSections(organizedStructure.RootSections);
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// This structure enables efficient rendering by pre-organizing sections into their appropriate
    /// display categories. Standalone sections can be rendered as individual items, while root sections
    /// contain their complete hierarchical structures for tree-based rendering.
    /// </remarks>
    public class OrganizedSectionStructure
    {
        #region properties

        /**************************************************************/
        /// <summary>
        /// Sections that exist independently without hierarchical relationships.
        /// These sections are not part of any parent-child structure and should be
        /// rendered as individual, standalone items in the user interface.
        /// </summary>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="RootSections"/>
        /// <seealso cref="IStructuredBodyService.HasStandaloneSections"/>
        /// <example>
        /// <code>
        /// foreach(var standaloneSection in organizedStructure.StandaloneSections)
        /// {
        ///     RenderIndividualSection(standaloneSection);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Initialized as empty list to prevent null reference exceptions.
        /// Standalone sections are identified by not appearing in any hierarchy relationships.
        /// </remarks>
        public List<SectionDto> StandaloneSections { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Root sections that have children in hierarchical relationships.
        /// These sections represent the top level of hierarchical structures and contain
        /// their complete child hierarchies for tree-based rendering scenarios.
        /// </summary>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="StandaloneSections"/>
        /// <seealso cref="IStructuredBodyService.HasHierarchicalSections"/>
        /// <seealso cref="ISectionHierarchyService.BuildChildSections"/>
        /// <example>
        /// <code>
        /// foreach(var rootSection in organizedStructure.RootSections)
        /// {
        ///     RenderHierarchicalSection(rootSection); // Renders section with all children
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Initialized as empty list to prevent null reference exceptions.
        /// Each root section contains its complete child hierarchy populated by the section hierarchy service.
        /// Root sections are identified as parents that are not children of any other section.
        /// </remarks>
        public List<SectionDto> RootSections { get; set; } = new();

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// View model for structured body rendering with pre-computed contexts and organizational state.
    /// Combines the raw structured body data with organized sections and rendering contexts
    /// to provide a complete model for efficient user interface rendering scenarios.
    /// </summary>
    /// <seealso cref="StructuredBodyDto"/>
    /// <seealso cref="OrganizedSectionStructure"/>
    /// <seealso cref="SectionRendering"/>
    /// <seealso cref="SectionDto"/>
    /// <seealso cref="ISectionHierarchyService"/>
    /// <seealso cref="IStructuredBodyService"/>
    /// <example>
    /// <code>
    /// var viewModel = new StructuredBodyViewModel
    /// {
    ///     StructuredBody = structuredBody,
    ///     OrganizedSections = organizedStructure,
    ///     HasStandaloneSections = organizedStructure.StandaloneSections.Any(),
    ///     HasHierarchicalSections = organizedStructure.RootSections.Any()
    /// };
    /// 
    /// // Use in rendering logic
    /// if (viewModel.HasStandaloneSections)
    /// {
    ///     RenderStandaloneContexts(viewModel.StandaloneSectionContexts);
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// This view model is designed to minimize computation during rendering by pre-computing
    /// organizational states and rendering contexts. It serves as a complete data package
    /// for structured body display components.
    /// </remarks>
    public class StructuredBodyViewModel
    {
        #region core data properties

        /**************************************************************/
        /// <summary>
        /// The original structured body data containing sections and hierarchy relationships.
        /// Represents the raw data model before organization and processing for display.
        /// </summary>
        /// <seealso cref="StructuredBodyDto"/>
        /// <seealso cref="OrganizedSections"/>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="SectionHierarchyDto"/>
        /// <example>
        /// <code>
        /// var sections = viewModel.StructuredBody.Sections;
        /// var hierarchies = viewModel.StructuredBody.SectionHierarchies;
        /// ProcessRawData(sections, hierarchies);
        /// </code>
        /// </example>
        /// <remarks>
        /// This property maintains access to the original data structure for scenarios
        /// where raw data access is needed beyond the organized representation.
        /// </remarks>
        public StructuredBodyDto StructuredBody { get; set; } = null!;

        /**************************************************************/
        /// <summary>
        /// Pre-organized sections separated into standalone and hierarchical structures.
        /// Contains the processed and organized version of the sections from the StructuredBody
        /// for efficient rendering without requiring real-time organization.
        /// </summary>
        /// <seealso cref="OrganizedSectionStructure"/>
        /// <seealso cref="StructuredBody"/>
        /// <seealso cref="ISectionHierarchyService.OrganizeSections"/>
        /// <example>
        /// <code>
        /// var standalones = viewModel.OrganizedSections.StandaloneSections;
        /// var hierarchical = viewModel.OrganizedSections.RootSections;
        /// 
        /// RenderSections(standalones, hierarchical);
        /// </code>
        /// </example>
        /// <remarks>
        /// This organized structure is typically populated by the section hierarchy service
        /// and provides the foundation for the rendering contexts and organizational flags.
        /// </remarks>
        public OrganizedSectionStructure OrganizedSections { get; set; } = null!;

        #endregion

        #region rendering context properties

        /**************************************************************/
        /// <summary>
        /// Pre-computed rendering contexts for standalone sections.
        /// Contains processed rendering data for each standalone section to eliminate
        /// the need for real-time context computation during user interface rendering.
        /// </summary>
        /// <seealso cref="SectionRendering"/>
        /// <seealso cref="OrganizedSections"/>
        /// <seealso cref="HasStandaloneSections"/>
        /// <example>
        /// <code>
        /// foreach(var context in viewModel.StandaloneSectionContexts)
        /// {
        ///     RenderSectionWithContext(context);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Initialized as empty list to prevent null reference exceptions.
        /// These contexts should correspond to the sections in OrganizedSections.StandaloneSections
        /// with additional rendering-specific data pre-computed for performance.
        /// </remarks>
        public List<SectionRendering> StandaloneSectionContexts { get; set; } = new();

        /**************************************************************/
        /// <summary>
        /// Pre-computed rendering contexts for hierarchical sections.
        /// Contains processed rendering data for each root section and its complete hierarchy
        /// to enable efficient tree-based rendering without real-time context computation.
        /// </summary>
        /// <seealso cref="SectionRendering"/>
        /// <seealso cref="OrganizedSections"/>
        /// <seealso cref="HasHierarchicalSections"/>
        /// <example>
        /// <code>
        /// foreach(var context in viewModel.HierarchicalSectionContexts)
        /// {
        ///     RenderHierarchicalSectionWithContext(context);
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Initialized as empty list to prevent null reference exceptions.
        /// These contexts should correspond to the sections in OrganizedSections.RootSections
        /// with additional rendering-specific data for the complete hierarchical structure.
        /// </remarks>
        public List<SectionRendering> HierarchicalSectionContexts { get; set; } = new();


        /**************************************************************/
        /// <summary>
        /// Unified collection of all section rendering contexts preserving original document order.
        /// Contains both standalone and hierarchical sections in their original sequence,
        /// eliminating the need to iterate separate collections while maintaining rendering context.
        /// </summary>
        /// <seealso cref="Label.Section"/>
        /// <seealso cref="SectionRendering"/>
        /// <seealso cref="StandaloneSectionContexts"/>
        /// <seealso cref="HierarchicalSectionContexts"/>
        /// <remarks>
        /// This property addresses the ordering issue where iterating standalone and hierarchical
        /// sections separately breaks the original document structure. Each section maintains
        /// its IsStandalone flag and Children collection for proper rendering.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Render all sections in original order
        /// foreach(var context in viewModel.AllSectionContexts)
        /// {
        ///     if(context.IsStandalone)
        ///         RenderStandaloneSection(context.Section);
        ///     else
        ///         RenderHierarchicalSection(context.Section, context.Children);
        /// }
        /// </code>
        /// </example>
        public List<SectionRendering> AllSectionContexts { get; set; } = new();

        #endregion

        #region organizational state properties

        /**************************************************************/
        /// <summary>
        /// Indicates whether the organized sections contain any standalone sections.
        /// Pre-computed flag to avoid repeated collection checks during rendering logic,
        /// enabling efficient conditional rendering of standalone section components.
        /// </summary>
        /// <seealso cref="OrganizedSections"/>
        /// <seealso cref="StandaloneSectionContexts"/>
        /// <seealso cref="IStructuredBodyService.HasStandaloneSections"/>
        /// <example>
        /// <code>
        /// if (viewModel.HasStandaloneSections)
        /// {
        ///     ShowStandaloneSectionPanel();
        ///     RenderStandaloneSections(viewModel.StandaloneSectionContexts);
        /// }
        /// else
        /// {
        ///     HideStandaloneSectionPanel();
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This flag should be set based on OrganizedSections.StandaloneSections.Any()
        /// to provide consistent state information across the view model.
        /// </remarks>
        public bool HasStandaloneSections { get; set; }

        /**************************************************************/
        /// <summary>
        /// Indicates whether the organized sections contain any hierarchical sections.
        /// Pre-computed flag to avoid repeated collection checks during rendering logic,
        /// enabling efficient conditional rendering of hierarchical section components.
        /// </summary>
        /// <seealso cref="OrganizedSections"/>
        /// <seealso cref="HierarchicalSectionContexts"/>
        /// <seealso cref="IStructuredBodyService.HasHierarchicalSections"/>
        /// <example>
        /// <code>
        /// if (viewModel.HasHierarchicalSections)
        /// {
        ///     ShowHierarchicalSectionTree();
        ///     RenderHierarchicalSections(viewModel.HierarchicalSectionContexts);
        /// }
        /// else
        /// {
        ///     HideHierarchicalSectionTree();
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This flag should be set based on OrganizedSections.RootSections.Any()
        /// to provide consistent state information across the view model.
        /// </remarks>
        public bool HasHierarchicalSections { get; set; }

        #endregion
    }
}