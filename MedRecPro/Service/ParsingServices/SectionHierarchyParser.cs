using System.Xml.Linq;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using MedRecPro.Data;
using MedRecPro.Helpers;
using Microsoft.EntityFrameworkCore;
using static MedRecPro.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.


namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Specialized parser for handling section hierarchy and child section relationships.
    /// Manages the recursive parsing of nested sections and establishes proper 
    /// parent-child relationships through SectionHierarchy entities.
    /// </summary>
    /// <remarks>
    /// This parser handles the complex nested section structures found in SPL documents,
    /// ensuring that section hierarchies are properly maintained and that child sections
    /// are recursively processed while preserving document structure integrity.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="SectionHierarchy"/>
    /// <seealso cref="Section"/>
    /// <seealso cref="SplParseContext"/>
    public class SectionHierarchyParser : ISplSectionParser
    {
        #region Private Fields
        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SectionHierarchyParser.
        /// Uses parameterless constructor - gets dependencies from SplParseContext.
        /// </summary>
        public SectionHierarchyParser() { }

        /**************************************************************/
        /// <summary>
        /// Gets the section name for this parser, representing hierarchy processing.
        /// </summary>
        public string SectionName => "hierarchy";

        /**************************************************************/
        /// <summary>
        /// Parses section hierarchy elements, processing child sections and establishing
        /// proper parent-child relationships through SectionHierarchy entities.
        /// </summary>
        /// <param name="element">The XElement representing the section to parse for hierarchy.</param>
        /// <param name="context">The current parsing context containing section information.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and hierarchy elements created.</returns>
        /// <seealso cref="parseChildSectionsAsync"/>
        /// <seealso cref="getOrCreateSectionHierarchiesAsync"/>
        public async Task<SplParseResult> ParseAsync(XElement element, SplParseContext context, Action<string>? reportProgress = null)
        {
            #region implementation
            var result = new SplParseResult();

            try
            {
                // Validate context and current section
                if (context?.CurrentSection?.SectionID == null)
                {
                    result.Success = false;
                    result.Errors.Add("No current section available for hierarchy parsing.");
                    return result;
                }

                var currentSectionId = context.CurrentSection.SectionID.Value;
                reportProgress?.Invoke("Processing section hierarchy...");

                // Parse section hierarchies to establish parent-child relationships
                var hierarchies = await getOrCreateSectionHierarchiesAsync(element, currentSectionId, context);
                result.SectionAttributesCreated += hierarchies.Count;

                // Recursively parse all direct child sections
                var childSectionsResult = await parseChildSectionsAsync(element, currentSectionId, context, reportProgress);
                result.MergeFrom(childSectionsResult);

                reportProgress?.Invoke($"Processed {result.SectionAttributesCreated} hierarchy attributes and {result.SectionsCreated} child sections");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error parsing section hierarchy: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing section hierarchy for section {SectionId}", context.CurrentSection?.SectionID);
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds and recursively parses all direct child sections of the current section.
        /// Establishes parent-child relationships and processes nested section hierarchies.
        /// </summary>
        /// <param name="parentEl">The XElement of the parent section.</param>
        /// <param name="parentSectionId">The database ID of the parent section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>An aggregated SplParseResult from all child section parsing operations.</returns>
        /// <seealso cref="SectionHierarchy"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseChildSectionsAsync(XElement parentEl, int parentSectionId, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate context has service provider
            if (context == null || context.ServiceProvider == null)
            {
                result.Success = false;
                result.Errors.Add("Service provider not available in parsing context.");
                return result;
            }

            // Validate that MainSectionParser is available in context
            if (context.MainSectionParser == null)
            {
                result.Success = false;
                result.Errors.Add("Main section parser not available in parsing context.");
                return result;
            }

            // Find all direct child sections within component elements
            var childSectionEls = parentEl.SplElements(sc.E.Component, sc.E.Section);

            // Process each child section recursively
            foreach (var childSectionEl in childSectionEls)
            {
                try
                {
                    // Use service locator pattern to resolve the main SectionParser for recursive calls
                    // This avoids circular dependency issues while enabling recursive section processing
                    var sectionParser = context!.MainSectionParser;

                    // Recursively call the main public parser for the child
                    var childResult = await sectionParser.ParseAsync(childSectionEl, context, reportProgress);
                    result.MergeFrom(childResult);

                    // If the child was created successfully, establish the parent-child relationship
                    if (childResult.Success && childResult.SectionsCreated > 0)
                    {
                        // Create hierarchy link between parent and child sections
                        await linkChildSectionAsync(parentSectionId, childSectionEl, context, result);
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Errors.Add($"Error parsing child section: {ex.Message}");
                    context?.Logger?.LogError(ex, "Error processing child section for parent {ParentSectionId}", parentSectionId);
                }
            }
            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a SectionHierarchy record to link a parent and child section.
        /// Establishes hierarchical relationships with proper sequence numbering.
        /// </summary>
        /// <param name="parentSectionId">The ID of the parent section.</param>
        /// <param name="childSectionEl">The XElement of the child section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="result">The result object to update with counts.</param>
        /// <seealso cref="SectionHierarchy"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task linkChildSectionAsync(int parentSectionId, XElement childSectionEl, SplParseContext context, SplParseResult result)
        {
            #region implementation
            try
            {
                // Extract child section GUID for database lookup
                var childGuidStr = childSectionEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root);
                if (!Guid.TryParse(childGuidStr, out var childGuid))
                {
                    return;
                }

                if (context == null || context.ServiceProvider == null)
                    return;

                // We query by GUID (a non-PK), so direct DbContext access is more flexible here.
                // Find child section entity by GUID for hierarchy establishment
                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var childSection = await dbContext
                    .Set<Section>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SectionGUID == childGuid);

                // Validate child section exists before creating hierarchy
                if (childSection?.SectionID == null)
                {
                    return;
                }

                // Check for existing hierarchy relationship to avoid duplicates
                var hierarchyRepo = context.GetRepository<SectionHierarchy>();
                var existingHierarchy = await dbContext
                    .Set<SectionHierarchy>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.ParentSectionID == parentSectionId
                        && h.ChildSectionID == childSection.SectionID.Value);

                // Create new hierarchy relationship if none exists
                if (existingHierarchy == null)
                {
                    await hierarchyRepo.CreateAsync(new SectionHierarchy
                    {
                        ParentSectionID = parentSectionId,
                        ChildSectionID = childSection.SectionID.Value,
                        SequenceNumber = result.SectionAttributesCreated + 1 // Simple sequence
                    });
                    result.SectionAttributesCreated++;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Error linking child section: {ex.Message}");
                context?.Logger?.LogError(ex, "Error linking child section to parent {ParentSectionId}", parentSectionId);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates SectionHierarchy records for all child sections nested under a parent section.
        /// Each [component][section] child becomes a child of the given parentSection.
        /// </summary>
        /// <param name="parentSectionEl">The XElement for the parent [section].</param>
        /// <param name="parentSectionId">The SectionID of the parent section (already saved).</param>
        /// <param name="context">Parsing context for repo/db access.</param>
        /// <returns>List of SectionHierarchy objects (created or found).</returns>
        /// <seealso cref="SectionHierarchy"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private async Task<List<SectionHierarchy>> getOrCreateSectionHierarchiesAsync(
            XElement parentSectionEl,
            int parentSectionId,
            SplParseContext context)
        {
            #region implementation
            var hierarchies = new List<SectionHierarchy>();

            // Validate required input parameters
            if (parentSectionEl == null || parentSectionId <= 0)
                return hierarchies;

            // Validate required context dependencies
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return hierarchies;

            try
            {
                // Get the DocumentID from context for section lookups
                int documentId = context.Document?.DocumentID ?? 0;

                // Get database context and repository for section hierarchy operations
                var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var repo = context.GetRepository<SectionHierarchy>();
                var dbSet = dbContext.Set<SectionHierarchy>();

                // Find all direct <component><section> children of this parent section, preserving order
                // Extract nested child sections while maintaining their XML document order
                var childSectionEls = parentSectionEl.SplElements(sc.E.Component)
                    .Select(comp => comp.SplElement(sc.E.Section))
                    .Where(childSec => childSec != null)
                    .ToList();

                // Initialize sequence number for maintaining child section order
                int seqNum = 1;

                // Process each child section to establish parent-child relationships
                foreach (var childSectionEl in childSectionEls)
                {
                    if (childSectionEl == null) continue;

                    try
                    {
                        // Find the SectionID of the child (must already be saved to DB after parsing the child section)
                        // Typically, you will have a way to map XML section GUID to saved Section entity.
                        // Extract the unique identifier for the child section from XML
                        var childSectionGuidStr = childSectionEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root);
                        if (!Guid.TryParse(childSectionGuidStr, out var childSectionGuid) || childSectionGuid == Guid.Empty)
                            continue;

                        // Find the Section record in DB by SectionGUID
                        // Lookup the corresponding database entity for this child section
                        var childSection = await dbContext.Set<Section>()
                            .FirstOrDefaultAsync(s => s.SectionGUID == childSectionGuid && s.DocumentID == documentId);
                        if (childSection == null || childSection.SectionID == null)
                            continue;

                        // Deduplicate on (parent, child)
                        // Check for existing hierarchy relationship between parent and child
                        var existing = await dbSet.FirstOrDefaultAsync(h =>
                            h.ParentSectionID == parentSectionId &&
                            h.ChildSectionID == childSection.SectionID);

                        // Return existing hierarchy if found, increment sequence for next iteration
                        if (existing != null)
                        {
                            hierarchies.Add(existing);
                            seqNum++;
                            continue;
                        }

                        // Create new section hierarchy relationship with preserved order
                        var newHierarchy = new SectionHierarchy
                        {
                            ParentSectionID = parentSectionId,
                            ChildSectionID = childSection.SectionID,
                            SequenceNumber = seqNum
                        };

                        // Persist new hierarchy relationship to database
                        await repo.CreateAsync(newHierarchy);
                        hierarchies.Add(newHierarchy);
                        seqNum++;
                    }
                    catch (Exception ex)
                    {
                        context.Logger?.LogError(ex, "Error processing child section hierarchy for parent {ParentSectionId}", parentSectionId);
                    }
                }
            }
            catch (Exception ex)
            {
                context?.Logger?.LogError(ex, "Error creating section hierarchies for parent {ParentSectionId}", parentSectionId);
            }

            return hierarchies;
            #endregion
        }
    }
}