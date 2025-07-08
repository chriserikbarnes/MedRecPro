using System.Runtime.Intrinsics;
using System.Xml.Linq;
using MedRecPro.Data;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using static System.Net.Mime.MediaTypeNames;
using static MedRecPro.Models.Label;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants; // Constant class for SPL elements and attributes


namespace MedRecPro.Service.ParsingServices
{
    /**************************************************************/
    /// <summary>
    /// Parses a section element and its children, like manufacturedProduct.
    /// NOTE: This is a simplified version that does not handle recursive sub-sections.
    /// </summary>
    /// <remarks>
    /// This parser handles section elements within SPL documents, extracting section
    /// metadata and coordinating the parsing of contained manufacturedProduct elements.
    /// It manages context switching to ensure that product parsers have access to the
    /// current section being processed. This implementation is simplified and does not
    /// support nested sub-sections, focusing on direct child manufacturedProduct elements.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="Section"/>
    /// <seealso cref="ManufacturedProductParser"/>
    /// <seealso cref="SplParseContext"/>
    public class SectionParser : ISplSectionParser
    {
        #region implementation
        /// <summary>
        /// Gets the section name for this parser, representing the section element.
        /// </summary>
        public string SectionName => "section";

        /// <summary>
        /// The XML namespace used for element parsing, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses a section element from an SPL document, creating the section entity
        /// and orchestrating the parsing of its associated manufacturedProduct elements.
        /// </summary>
        /// <param name="xEl">The XElement representing the section element to parse.</param>
        /// <param name="context">The current parsing context containing the structuredBody to link sections to.</param>
        /// <param name="reportProgress">Optional action to report progress during parsing.</param>
        /// <returns>A SplParseResult indicating the success status and any errors encountered during parsing.</returns>
        /// <example>
        /// <code>
        /// var parser = new SectionParser();
        /// var result = await parser.ParseAsync(sectionElement, parseContext);
        /// if (result.Success)
        /// {
        ///     Console.WriteLine($"Sections created: {result.SectionsCreated}");
        ///     Console.WriteLine($"Products created: {result.ProductsCreated}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the following operations:
        /// 1. Validates that a structuredBody context exists
        /// 2. Extracts section metadata (GUID, codes, title, effective time)
        /// 3. Creates and saves the Section entity
        /// 4. Sets up context for manufacturedProduct parsing
        /// 5. Delegates manufacturedProduct parsing to specialized parsers
        /// 6. Aggregates results from child parsers
        /// 7. Restores context to prevent side effects
        /// 
        /// The method maintains proper context isolation and supports the delegation
        /// pattern for hierarchical SPL document parsing.
        /// </remarks>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="ManufacturedProductParser"/>
        public async Task<SplParseResult> ParseAsync(XElement xEl,
            SplParseContext context,
            Action<string>? reportProgress = null)
        {
            #region implementation
            var result = new SplParseResult();
            int secID = 0;

            // Validate context
            if (context == null || context.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context is null or logger is null.");
                return result;
            }

            // Validate that we have a valid structuredBody context to link sections to
            if (context.StructuredBody?.StructuredBodyID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse section because no structuredBody context exists.");
                return result;
            }

            try
            {

                reportProgress?.Invoke($"Starting Section XML Elements {context.FileNameInZip}");

                // Create the Section entity with extracted metadata
                var section = new Section
                {
                    StructuredBodyID = context.StructuredBody.StructuredBodyID.Value,

                    // Extract section GUID from the id element's root attribute, defaulting to empty GUID
                    SectionGUID = Util.ParseNullableGuid(xEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root) ?? string.Empty) ?? Guid.Empty,

                    // Extract section code value from the code element's codeValue attribute
                    SectionCode = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue),

                    // Extract section code system from the code element's codeSystem attribute
                    SectionCodeSystem = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem),

                    // Extract section display name from the code element's displayName attribute, defaulting to empty string
                    SectionDisplayName = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName) ?? string.Empty,

                    // Extract and trim section title from the title element
                    Title = xEl.GetSplElementVal(sc.E.Title)?.Trim(),

                    // Extract and parse effective time from the effectiveTime element's value attribute, defaulting to DateTime.MinValue
                    EffectiveTime = Util.ParseNullableDateTime(xEl.GetSplElementAttrVal(sc.E.EffectiveTime, sc.A.Value) ?? string.Empty) ?? DateTime.MinValue
                };

                // Save the section entity to the database
                var sectionRepo = context.GetRepository<Section>();
                await sectionRepo.CreateAsync(section);
                result.SectionsCreated++;

                if (section != null && section.SectionID > 0)
                {
                    secID = section.SectionID.Value;

                    // --- PARSE SECTION HEIRARCHY ---
                    var hierarchies = await getOrCreateSectionHierarchiesAsync(xEl, secID, context);
                    result.SectionAttributesCreated += hierarchies.Count;

                    // --- PARSE SECTION TEXT CONTENT ---
                    var textEl = xEl.SplElement(sc.E.Text);
                    if (textEl != null)
                    {
                        var textContents = await getOrCreateSectionTextContentsAsync(textEl, secID, context, parseAndSaveSectionAsync);
                        result.SectionAttributesCreated += textContents.Count;
                    }

                    // --- PARSE SECTION HIGHLIGHT ---
                    var excerptHighlights = await getOrCreateSectionExcerptHighlightsAsync(xEl, secID, context);
                    result.SectionAttributesCreated += excerptHighlights.Count;
                }

                // Set current section in context for child parsers
                // Store the previous section context to restore later
                var oldSection = context.CurrentSection;
                context.CurrentSection = section;

                // Delegate parsing of <manufacturedProduct> if it exists
                // Navigate through the SPL hierarchy: section/subject/manufacturedProduct
                var productEl = xEl.SplElement(sc.E.Subject, sc.E.ManufacturedProduct);

                if (productEl != null)
                {
                    // Create and delegate to the manufacturedProduct parser
                    var productParser = new ManufacturedProductParser();
                    var productResult = await productParser.ParseAsync(productEl, context, reportProgress);
                    result.MergeFrom(productResult); // Aggregate results from product parsing
                }

                // Restore the previous section context to avoid side effects on other parsers
                context.CurrentSection = oldSection;

                reportProgress?.Invoke($"Completed Section XML Elements {context.FileNameInZip}");
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during section parsing
                result.Success = false;
                result.Errors.Add($"Error parsing section: {ex.Message}");
                context.Logger.LogError(ex, "Error processing <section> element.");
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recursively finds or creates SectionTextContent records for all content 
        /// blocks ([paragraph], [list], [table], [renderMultimedia], [excerpt], [highlight])
        /// within a section's [text] element, preserving the nested hierarchy for SPL round-tripping.
        /// Also processes nested child sections and establishes section hierarchies.
        /// </summary>
        /// <param name="parentEl">The XElement to start parsing (typically the [text] element).</param>
        /// <param name="sectionId">The SectionID owning this content.</param>
        /// <param name="context">Parsing context.</param>
        /// <param name="parseAndSaveSectionAsync">Function delegate for parsing and saving child sections.</param>
        /// <param name="parentSectionTextContentId">Parent SectionTextContentID for hierarchy, or null for top-level blocks.</param>
        /// <param name="sequence">The sequence number to start from (default 1).</param>
        /// <returns>A list of SectionTextContent objects created/found (entire tree).</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<List<SectionTextContent>> getOrCreateSectionTextContentsAsync(
            XElement parentEl,
            int sectionId,
            SplParseContext context,
            Func<XElement, SplParseContext, Task<Section>> parseAndSaveSectionAsync,
            int? parentSectionTextContentId = null,
            int sequence = 1)
        {
            #region implementation
            var created = new List<SectionTextContent>();

            // Validate all required input parameters and context dependencies
            if (parentEl == null
                || sectionId <= 0
                || context == null
                || context.ServiceProvider == null
                || context.Logger == null)
                return created;

            // Get database context and repository for section text content operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = context.GetRepository<SectionTextContent>();
            var dbSet = dbContext.Set<SectionTextContent>();

            // Initialize sequence counter for maintaining order within current hierarchy level
            int seq = sequence;

            // Process each allowed content block at the current hierarchy level
            foreach (var block in parentEl.SplBuildSectionContentTree())
            {
                // Capitalize first letter to standardize content type names (e.g., "excerpt" → "Excerpt")
                var contentType = char.ToUpper(block.Name.LocalName[0]) + block.Name.LocalName.Substring(1);

                // Extract text content only for paragraph elements; complex structures handled via recursion
                var contentText = contentType == "Paragraph" ? block.Value?.Trim() : null;

                // Extract optional style code attribute for formatting information
                var styleCode = block.Attribute("styleCode")?.Value?.Trim();

                // Deduplication: SectionID + ContentType + SequenceNumber + ParentSectionTextContentID + ContentText (for Paragraphs)
                // Search for existing content block with matching hierarchy and attributes
                var existing = await dbSet.FirstOrDefaultAsync(c =>
                    c.SectionID == sectionId &&
                    c.ContentType == contentType &&
                    c.SequenceNumber == seq &&
                    c.ParentSectionTextContentID == parentSectionTextContentId &&
                    (contentType != "Paragraph" || c.ContentText == contentText));

                SectionTextContent stc;

                // Use existing content block if found, otherwise create new one
                if (existing != null)
                {
                    stc = existing;
                }
                else
                {
                    // Create new section text content with hierarchical relationship
                    stc = new SectionTextContent
                    {
                        SectionID = sectionId,
                        ParentSectionTextContentID = parentSectionTextContentId,
                        ContentType = contentType,
                        StyleCode = styleCode,
                        SequenceNumber = seq,
                        ContentText = contentText
                    };
                    await repo.CreateAsync(stc);
                }

                // Add current content block to results
                created.Add(stc);

                // --- EXCERPT/HIGHLIGHT CAPTURE ---
                // Process special excerpt elements that contain highlighted text
                if (contentType == "Excerpt")
                {
                    // Extract and save excerpt highlights for specialized content processing
                    var excerptHighlights = await getOrCreateSectionExcerptHighlightsAsync(block, sectionId, context);
                }

                // Recurse for children (preserving structure)
                // Check for nested content blocks that require recursive processing
                var childBlocks = block.SplBuildSectionContentTree().ToList();
                if (childBlocks.Any())
                {
                    // Recursively process child content blocks with current block as parent
                    var childResults = await getOrCreateSectionTextContentsAsync(
                        block,
                        sectionId,
                        context,
                        parseAndSaveSectionAsync,
                        stc.SectionTextContentID,
                        1); // Always start child sequence at 1 for each parent block

                    // Add all child results to the overall collection
                    created.AddRange(childResults);
                }

                // Increment sequence for next sibling at current level
                seq++;
            }

            // --- CAPTURE CHILD SECTIONS: <component><section> ---
            // Process nested child sections within component elements
            var childSectionEls = parentEl.SplElements(sc.E.Component)
                .Select(comp => comp.SplElement(sc.E.Section))
                .Where(childSec => childSec != null)
                .ToList();

            // Process each child section and establish hierarchical relationships
            foreach (var childSectionEl in childSectionEls)
            {
                // Save the child section and get its SectionID
                // Parse and persist child section using provided delegate function
                var childSection = await parseAndSaveSectionAsync(childSectionEl, context);
                if (childSection != null && childSection.SectionID.HasValue)
                {
                    // Link parent to child in the hierarchy (if not already present)
                    // Establish section hierarchy relationship between parent and child
                    await getOrCreateSectionHierarchiesAsync(parentEl, sectionId, context);

                    // Recursively parse this child section's text content
                    // Process text content within the child section
                    var childTextEl = childSectionEl.Element(sc.E.Text);
                    if (childTextEl != null)
                    {
                        // Recursively process child section text content with fresh hierarchy context
                        var childTextContents = await getOrCreateSectionTextContentsAsync(
                            childTextEl,
                            childSection.SectionID.Value,
                            context,
                            parseAndSaveSectionAsync,
                            null, // new section, so null parentSectionTextContentId
                            1); // Start sequence at 1 for new section

                        // Add child section text content to overall results
                        created.AddRange(childTextContents);
                    }
                }
            }

            return created;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses and saves a Section entity from an XElement, extracting metadata and establishing
        /// relationships with the structured body context. Performs deduplication based on section GUID.
        /// </summary>
        /// <param name="sectionEl">The XElement representing the section to parse.</param>
        /// <param name="context">Parsing context containing structured body and repository access.</param>
        /// <returns>The Section entity (created or existing) with populated metadata.</returns>
        /// <exception cref="ArgumentNullException">Thrown when sectionEl or context is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when structured body context is invalid or section GUID is missing.</exception>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<Section> parseAndSaveSectionAsync(XElement sectionEl, SplParseContext context)
        {
            #region implementation
            // Validate required input parameters
            if (sectionEl == null)
                throw new ArgumentNullException(nameof(sectionEl));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.StructuredBody?.StructuredBodyID == null)
                throw new InvalidOperationException("No valid structured body context.");

            // Get section GUID
            // Extract unique identifier for section from XML element
            var sectionGuidStr = sectionEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root);
            if (!Guid.TryParse(sectionGuidStr, out var sectionGuid) || sectionGuid == Guid.Empty)
                throw new InvalidOperationException("Section <id root> is missing or not a valid GUID.");

            // Get repo/db
            // Get database context and repository for section operations
            var dbContext = context!.ServiceProvider!.GetRequiredService<ApplicationDbContext>();
            var sectionRepo = context.GetRepository<Section>();
            var sectionDbSet = dbContext.Set<Section>();

            // Deduplicate by GUID and StructuredBodyID (or just by GUID if global)
            // Search for existing section with matching GUID and structured body context
            var existing = await sectionDbSet
                .FirstOrDefaultAsync(s => s.SectionGUID == sectionGuid && s.StructuredBodyID == context.StructuredBody.StructuredBodyID.Value);

            // Return existing section if found to avoid duplicates
            if (existing != null)
                return existing;

            // Extract additional metadata
            // Parse section code information and metadata from XML attributes
            var sectionCode = sectionEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
            var sectionCodeSystem = sectionEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem);
            var sectionDisplayName = sectionEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName) ?? string.Empty;
            var sectionTitle = sectionEl.GetSplElementVal(sc.E.Title)?.Trim();

            // Parse effective time with fallback to minimum value if not present
            var sectionEffectiveTime = Util.ParseNullableDateTime(sectionEl.GetSplElementAttrVal(sc.E.EffectiveTime, sc.A.Value) ?? string.Empty) ?? DateTime.MinValue;

            // Create new Section entity
            // Build new section entity with all extracted metadata and context relationships
            var newSection = new Section
            {
                StructuredBodyID = context.StructuredBody.StructuredBodyID.Value,
                SectionGUID = sectionGuid,
                SectionCode = sectionCode,
                SectionCodeSystem = sectionCodeSystem,
                SectionDisplayName = sectionDisplayName,
                Title = sectionTitle,
                EffectiveTime = sectionEffectiveTime
            };

            // Persist new section to database and return
            await sectionRepo.CreateAsync(newSection);
            return newSection;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates SectionExcerptHighlight records for all highlight text nodes
        /// within excerpt elements of a section, capturing highlighted content for database storage.
        /// </summary>
        /// <param name="sectionEl">The XElement to search for excerpt/highlight/text patterns.</param>
        /// <param name="sectionId">The SectionID owning this highlight content.</param>
        /// <param name="context">Parsing context for repository and database access.</param>
        /// <returns>List of SectionExcerptHighlight objects (created or found).</returns>
        /// <seealso cref="SectionExcerptHighlight"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<List<SectionExcerptHighlight>> getOrCreateSectionExcerptHighlightsAsync(
            XElement sectionEl,
            int sectionId,
            SplParseContext context)
        {
            #region implementation
            var highlights = new List<SectionExcerptHighlight>();

            // Validate required input parameters
            if (sectionEl == null || sectionId <= 0)
                return highlights;

            // Validate required context dependencies
            if (context == null || context.Logger == null || context.ServiceProvider == null)
                return highlights;

            // Get database context and repository for section excerpt highlight operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = context.GetRepository<SectionExcerptHighlight>();
            var dbSet = dbContext.Set<SectionExcerptHighlight>();

            // Find all excerpt/highlight/text nodes for this section
            // Navigate the XML hierarchy to locate text nodes within highlight elements inside excerpts
            foreach (var highlightTextEl in sectionEl
                .Descendants()
                .Where(x => string.Equals(x.Name.LocalName, sc.E.Text, StringComparison.OrdinalIgnoreCase) &&
                            x.Parent != null &&
                            string.Equals(x.Parent.Name.LocalName, sc.E.Highlight, StringComparison.OrdinalIgnoreCase) &&
                            x.Parent.Parent != null &&
                            string.Equals(x.Parent.Parent.Name.LocalName, sc.E.Excerpt, StringComparison.OrdinalIgnoreCase)))
            {
                // Extract the highlighted text content from the XML element
                var txt = getHighlightXml(highlightTextEl);
                if (string.IsNullOrWhiteSpace(txt)) continue;

                // Dedupe: SectionID + HighlightText
                // Search for existing highlight with matching section and text content
                var existing = await dbSet.FirstOrDefaultAsync(eh =>
                    eh.SectionID == sectionId &&
                    eh.HighlightText == txt);

                // Return existing highlight if found to avoid duplicates
                if (existing != null)
                {
                    highlights.Add(existing);
                    continue;
                }

                // Create new section excerpt highlight with extracted text
                var newHighlight = new SectionExcerptHighlight
                {
                    SectionID = sectionId,
                    HighlightText = txt
                };

                // Persist new highlight to database
                await repo.CreateAsync(newHighlight);
                highlights.Add(newHighlight);
            }

            return highlights;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts and concatenates all descendant text content from a highlight text element
        /// for storage in the database, preserving the original highlighted content.
        /// </summary>
        /// <param name="highlightTextEl">The XElement containing highlight text content.</param>
        /// <returns>Concatenated and trimmed text content, or null if element is null.</returns>
        /// <seealso cref="SectionExcerptHighlight"/>
        /// <seealso cref="Label"/>
        private static string? getHighlightText(XElement highlightTextEl)
        {
            #region implementation
            // Simple: concatenate all descendant text, trimmed, for database storage
            // Extract all text nodes within the highlight element and combine them
            return highlightTextEl == null ? null : string.Concat(highlightTextEl.DescendantNodes().OfType<XText>().Select(x => x.Value)).Trim();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Same as getHighlightText but this preserves all the markup
        /// </summary>
        /// <param name="highlightTextEl"></param>
        /// <returns></returns>
        private static string? getHighlightXml(XElement highlightTextEl)
        {
            #region implementation
            // Find the <text> element directly under <highlight>
            var textEl = highlightTextEl?.Element(highlightTextEl.GetDefaultNamespace() + "text")
                         ?? highlightTextEl?.Element("text"); // fallback if no namespace

            if (textEl == null)
                return null;

            // Return concatenated inner XML (preserving all tags/markup)
            return string.Concat(textEl.Nodes().Select(n => n.ToString())).Trim();
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
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<List<SectionHierarchy>> getOrCreateSectionHierarchiesAsync(
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

                // Find the SectionID of the child (must already be saved to DB after parsing the child section)
                // Typically, you will have a way to map XML section GUID to saved Section entity.
                // Extract the unique identifier for the child section from XML
                var childSectionGuidStr = childSectionEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root);
                if (!Guid.TryParse(childSectionGuidStr, out var childSectionGuid) || childSectionGuid == Guid.Empty)
                    continue;

                // Find the Section record in DB by SectionGUID
                // Lookup the corresponding database entity for this child section
                var childSection = await dbContext.Set<Section>()
                    .FirstOrDefaultAsync(s => s.SectionGUID == childSectionGuid);
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

            return hierarchies;
            #endregion
        }
    }
}