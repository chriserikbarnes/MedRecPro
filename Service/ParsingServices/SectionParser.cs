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
        #region private vars
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

        #region properties
        /**************************************************************/
        /// <summary>
        /// Helper class to encapsulate the results of processing a single content block.
        /// Contains the main content entity, nested content, and count of grandchild entities.
        /// </summary>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="Label"/>
        private class ProcessBlockResult
        {
            #region implementation
            /// <summary>
            /// The primary SectionTextContent entity created for this content block.
            /// </summary>
            /// <seealso cref="SectionTextContent"/>
            public SectionTextContent MainContent { get; set; }

            /// <summary>
            /// Collection of nested SectionTextContent entities within this block.
            /// </summary>
            /// <seealso cref="SectionTextContent"/>
            public List<SectionTextContent> NestedContent { get; set; } = new List<SectionTextContent>();

            /// <summary>
            /// Count of grandchild entities created (list items, table cells, etc.).
            /// </summary>
            public int GrandchildEntityCount { get; set; }
            #endregion
        }
        #endregion

        /**************************************************************/
        /// <summary>
        /// Parses a section element from an SPL document, creating the section entity
        /// and orchestrating the parsing of its associated content and child elements.
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
        /// This method orchestrates the following operations:
        /// 1. Validates the parsing context.
        /// 2. Creates and saves the primary Section entity.
        /// 3. Manages parsing context for child elements.
        /// 4. Delegates parsing of section content (text, highlights) to specialized helpers.
        /// 5. Delegates parsing of child sections recursively.
        /// 6. Delegates parsing of manufacturedProduct elements.
        /// 7. Aggregates results and handles exceptions.
        /// </remarks>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="ManufacturedProductParser"/>
        /// <seealso cref="Label"/>
        public async Task<SplParseResult> ParseAsync(XElement xEl,
            SplParseContext context,
            Action<string>? reportProgress = null)
        {
            #region implementation
            var result = new SplParseResult();

            // Validate parsing context to ensure all required dependencies are available
            if (!validateContext(context, result))
            {
                return result;
            }

            try
            {
                // Report parsing start for monitoring and debugging purposes
                reportProgress?.Invoke($"Starting Section parsing for {context.FileNameInZip}");

                // 1. Create the core Section entity from the XML element
                // Parse section metadata and persist the primary section entity
                var section = await createAndSaveSectionAsync(xEl, context);
                if (section?.SectionID == null)
                {
                    result.Success = false;
                    result.Errors.Add("Failed to create and save the Section entity.");
                    return result;
                }
                result.SectionsCreated++;

                // 2. Set context for child parsers and ensure it's restored
                // Manage parsing context state to provide section context to child parsers
                var oldSection = context.CurrentSection;
                context.CurrentSection = section;
                try
                {
                    // 3. Parse the content within this section (text, highlights, etc.)
                    // Process section text content, hierarchies, and excerpt highlights
                    var contentResult = await parseSectionContentAsync(xEl, section.SectionID.Value, context);
                    result.MergeFrom(contentResult);

                    // 4. Recursively parse all direct child sections
                    // Handle nested section structure and establish parent-child relationships
                    var childSectionsResult = await parseChildSectionsAsync(xEl, section.SectionID.Value, context, reportProgress);
                    result.MergeFrom(childSectionsResult);

                    // 5. Parse the associated manufactured product, if it exists
                    // Process product information contained within the section
                    var productResult = await parseManufacturedProductAsync(xEl, context, reportProgress);
                    result.MergeFrom(productResult);
                }
                finally
                {
                    // Restore the context to prevent side effects for sibling or parent parsers
                    context.CurrentSection = oldSection;
                }

                // Report parsing completion for monitoring purposes
                reportProgress?.Invoke($"Completed Section parsing for {context.FileNameInZip}");
            }
            catch (Exception ex)
            {
                // Handle unexpected errors and log them for debugging
                result.Success = false;
                result.Errors.Add($"An unexpected error occurred while parsing section: {ex.Message}");
                context?.Logger?.LogError(ex, "Error processing <section> element for {FileName}", context.FileNameInZip);
            }

            return result;
            #endregion
        }

        #region Private Helper Methods

        /**************************************************************/
        /// <summary>
        /// Validates the parsing context to ensure it's properly initialized.
        /// Checks for required dependencies and structured body context.
        /// </summary>
        /// <param name="context">The parsing context to validate.</param>
        /// <param name="result">The result object to add errors to if validation fails.</param>
        /// <returns>True if the context is valid; otherwise, false.</returns>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="Label"/>
        private bool validateContext(SplParseContext context, SplParseResult result)
        {
            #region implementation
            // Validate logger availability for error reporting and debugging
            if (context?.Logger == null)
            {
                result.Success = false;
                result.Errors.Add("Parsing context or its logger is null.");
                return false;
            }

            // Validate structured body context for section association
            if (context.StructuredBody?.StructuredBodyID == null)
            {
                result.Success = false;
                result.Errors.Add("Cannot parse section because no structuredBody context exists.");
                return false;
            }

            return true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new Section entity from the given XML element and saves it to the database.
        /// Extracts section metadata including GUID, codes, title, and effective time.
        /// </summary>
        /// <param name="xEl">The XElement representing the section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The created and saved Section entity, or null if creation failed.</returns>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private async Task<Section?> createAndSaveSectionAsync(XElement xEl, SplParseContext context)
        {
            #region implementation
            // Build section entity with extracted metadata from XML attributes and elements
            var section = new Section
            {
                StructuredBodyID = context.StructuredBody!.StructuredBodyID!.Value,
                SectionGUID = Util.ParseNullableGuid(xEl.GetSplElementAttrVal(sc.E.Id, sc.A.Root) ?? string.Empty) ?? Guid.Empty,
                SectionCode = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue),
                SectionCodeSystem = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeSystem),
                SectionDisplayName = xEl.GetSplElementAttrVal(sc.E.Code, sc.A.DisplayName) ?? string.Empty,
                Title = xEl.GetSplElementVal(sc.E.Title)?.Trim(),
                EffectiveTime = Util.ParseNullableDateTime(xEl.GetSplElementAttrVal(sc.E.EffectiveTime, sc.A.Value) ?? string.Empty) ?? DateTime.MinValue
            };

            // Persist section to database using repository pattern
            var sectionRepo = context.GetRepository<Section>();
            await sectionRepo.CreateAsync(section);

            // Return section if successfully created with valid ID
            return section.SectionID > 0 ? section : null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the inner content of a section, such as text, lists, and highlights.
        /// Processes hierarchies, text content, excerpts, and highlight elements.
        /// </summary>
        /// <param name="xEl">The XElement for the section whose content is to be parsed.</param>
        /// <param name="sectionId">The database ID of the parent section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A SplParseResult containing the outcome of parsing the content.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SectionHierarchy"/>
        /// <seealso cref="SectionExcerptHighlight"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseSectionContentAsync(XElement xEl, int sectionId, SplParseContext context)
        {
            #region implementation
            var result = new SplParseResult();

            // Parse hierarchies, text, and highlights, aggregating the number of attributes created.
            // Process section hierarchies to establish parent-child relationships
            var hierarchies = await getOrCreateSectionHierarchiesAsync(xEl, sectionId, context);
            result.SectionAttributesCreated += hierarchies.Count;

            // Process main text content including paragraphs, lists, tables, etc.
            var textEl = xEl.SplElement(sc.E.Text);
            if (textEl != null)
            {
                var (textContents, listEntityCount) = await getOrCreateSectionTextContentsAsync(textEl, sectionId, context, parseAndSaveSectionAsync);
                result.SectionAttributesCreated += textContents.Count;
                result.SectionAttributesCreated += listEntityCount;
            }

            // Process excerpt elements with nested content structure
            var excerptEl = xEl.SplElement(sc.E.Excerpt);
            if (excerptEl != null)
            {
                var (excerptTextContents, listEntityCount) = await getOrCreateSectionTextContentsAsync(excerptEl, sectionId, context, parseAndSaveSectionAsync);
                result.SectionAttributesCreated += excerptTextContents.Count;

                // Extract highlighted text within excerpts for specialized processing
                var eHighlights = await getOrCreateSectionExcerptHighlightsAsync(excerptEl, sectionId, context);
                result.SectionAttributesCreated += eHighlights.Count;
            }

            // Process direct highlights not contained within excerpts
            var directHighlights = await getOrCreateSectionExcerptHighlightsAsync(xEl, sectionId, context);
            result.SectionAttributesCreated += directHighlights.Count;

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
        /// <seealso cref="Section"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseChildSectionsAsync(XElement parentEl, int parentSectionId, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            var result = new SplParseResult();

            // Find all direct child sections within component elements
            var childSectionEls = parentEl.SplElements(sc.E.Component, sc.E.Section);

            // Process each child section recursively
            foreach (var childSectionEl in childSectionEls)
            {
                // Recursively call the main public parser for the child
                // Use recursive parsing to handle nested section structures
                var childResult = await this.ParseAsync(childSectionEl, context, reportProgress);
                result.MergeFrom(childResult);

                // If the child was created successfully, establish the parent-child relationship
                if (childResult.Success && childResult.SectionsCreated > 0)
                {
                    // Create hierarchy link between parent and child sections
                    await linkChildSectionAsync(parentSectionId, childSectionEl, context, result);
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
        /// <seealso cref="Label"/>
        private async Task linkChildSectionAsync(int parentSectionId, XElement childSectionEl, SplParseContext context, SplParseResult result)
        {
            #region implementation
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
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// A utility method to extract the inner XML of a table cell (td or th),
        /// preserving all markup for rich content display.
        /// </summary>
        /// <param name="cellElement">The [td] or [th] XElement.</param>
        /// <returns>The inner XML as a string, or null if the input is null.</returns>
        /// <seealso cref="TextTableCell"/>
        /// <seealso cref="Label"/>
        private static string? getCellXml(XElement cellElement)
        {
            #region implementation
            // Return null for invalid input to handle edge cases gracefully
            if (cellElement == null) return null;

            // Using a reader is more robust for getting inner XML than the Nodes().ToString()
            // approach, as it's less susceptible to modifications of the in-memory XDocument.
            var reader = cellElement.CreateReader();
            reader.MoveToContent();
            return reader.ReadInnerXml().Trim();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all cells ([td], [th]) within a given table row ([tr]), creating
        /// TextTableCell records. It skips empty cells and maintains correct sequencing.
        /// </summary>
        /// <param name="rowEl">The XElement for the table row ([tr]).</param>
        /// <param name="textTableRowId">The database ID of the parent TextTableRow.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A task that resolves to the number of TextTableCell entities created.</returns>
        /// <seealso cref="TextTableCell"/>
        /// <seealso cref="TextTableRow"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<int> parseAndCreateCellsAsync(
            XElement rowEl,
            int textTableRowId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate required context dependencies for database operations
            if (context == null || context.ServiceProvider == null)
                return 0;

            // Get database context and repository for table cell operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cellRepo = context.GetRepository<TextTableCell>();
            var cellDbSet = dbContext.Set<TextTableCell>();

            // Process both <th> and <td> elements in document order.
            // Extract both header and data cells while preserving their sequence
            var cellElements = rowEl.Elements()
                .Where(e => e.Name.LocalName == sc.E.Th || e.Name.LocalName == sc.E.Td)
                .ToList();

            // Initialize sequence number for maintaining cell order within row
            int seqNum = 1;

            // Process each cell element and create database entities
            foreach (var cellEl in cellElements)
            {
               
                // Check for existing cell to avoid duplicates based on row and sequence
                var existingCell = await cellDbSet.FirstOrDefaultAsync(c =>
                    c.TextTableRowID == textTableRowId &&
                    c.SequenceNumber == seqNum);

                // Create new cell if none exists at this position
                if (existingCell == null)
                {
                    // Safely parse integer attributes
                    // Extract rowspan and colspan attributes with safe parsing
                    _ = int.TryParse(cellEl.Attribute(sc.A.Rowspan)?.Value, out int rs);
                    _ = int.TryParse(cellEl.Attribute(sc.A.Colspan)?.Value, out int cs);

                    // Build new table cell entity with all extracted attributes
                    var newCell = new TextTableCell
                    {
                        TextTableRowID = textTableRowId,
                        CellType = cellEl.Name.LocalName, // "th" or "td"
                        SequenceNumber = seqNum,
                        CellText = getCellXml(cellEl),
                        RowSpan = rs > 0 ? rs : null, // Only store valid span values
                        ColSpan = cs > 0 ? cs : null, // Only store valid span values
                        StyleCode = cellEl.Attribute(sc.A.StyleCode)?.Value,
                        Align = cellEl.Attribute(sc.A.Align)?.Value,
                        VAlign = cellEl.Attribute(sc.A.VAlign)?.Value
                    };

                    // Persist new cell to database
                    await cellRepo.CreateAsync(newCell);
                    createdCount++;
                }

                // Increment sequence for next cell regardless of creation status
                seqNum++;
            }
            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses all rows ([tr]) within a given table group ([thead], [tbody], [tfoot]),
        /// creating TextTableRow records and delegating cell parsing.
        /// </summary>
        /// <param name="rowGroupEl">The XElement for the group (e.g., [tbody]).</param>
        /// <param name="textTableId">The database ID of the parent TextTable.</param>
        /// <param name="rowGroupType">The type of group: 'Header', 'Body', or 'Footer'.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A task that resolves to the total number of row and cell entities created.</returns>
        /// <seealso cref="TextTableRow"/>
        /// <seealso cref="TextTable"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<int> parseAndCreateRowsAsync(
            XElement rowGroupEl,
            int textTableId,
            string rowGroupType,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate required context dependencies for database operations
            if (context == null || context.ServiceProvider == null)
                return 0;

            // Get database context and repository for table row operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rowRepo = context.GetRepository<TextTableRow>();
            var rowDbSet = dbContext.Set<TextTableRow>();

            // Extract all table row elements from the current group
            var rowElements = rowGroupEl.SplElements(sc.E.Tr).ToList();

            // Initialize sequence number for maintaining row order within group
            int seqNum = 1;

            // Process each row element within the table group
            foreach (var rowEl in rowElements)
            {
                // Check for existing row to avoid duplicates based on table, group type, and sequence
                var existingRow = await rowDbSet.FirstOrDefaultAsync(r =>
                    r.TextTableID == textTableId &&
                    r.RowGroupType == rowGroupType &&
                    r.SequenceNumber == seqNum);

                TextTableRow textTableRow;

                // Use existing row if found, otherwise create new one
                if (existingRow != null)
                {
                    textTableRow = existingRow;
                }
                else
                {
                    // Create new table row entity with group classification and styling
                    textTableRow = new TextTableRow
                    {
                        TextTableID = textTableId,
                        RowGroupType = rowGroupType, // 'Header', 'Body', or 'Footer'
                        SequenceNumber = seqNum,
                        StyleCode = rowEl.Attribute(sc.A.StyleCode)?.Value
                    };

                    // Persist new row to database
                    await rowRepo.CreateAsync(textTableRow);
                    createdCount++;
                }

                // Process cells within this row if row was successfully created/retrieved
                if (textTableRow.TextTableRowID.HasValue)
                {
                    // Delegate cell parsing and accumulate created cell count
                    createdCount += await parseAndCreateCellsAsync(rowEl, textTableRow.TextTableRowID.Value, context);
                }

                // Increment sequence for next row in group
                seqNum++;
            }
            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a [table] element, creating the main TextTable record and then
        /// delegating the parsing of its header, body, and footer rows.
        /// </summary>
        /// <param name="tableEl">The XElement representing the [table] element.</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent record.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>A task that resolves to the total number of table, row, and cell entities created.</returns>
        /// <seealso cref="TextTable"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<int> getOrCreateTextTableAndChildrenAsync(
            XElement tableEl,
            int sectionTextContentId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate all required input parameters and context dependencies
            if (tableEl == null || sectionTextContentId <= 0 || context?.ServiceProvider == null)
            {
                return 0;
            }

            // Get database context and repository for table operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tableRepo = context.GetRepository<TextTable>();
            var tableDbSet = dbContext.Set<TextTable>();

            // 1. Find or Create the TextTable record
            // Check for existing table associated with the section text content
            var textTable = await tableDbSet.FirstOrDefaultAsync(t => t.SectionTextContentID == sectionTextContentId);
            if (textTable == null)
            {
                // Create new table entity with metadata about structure and styling
                textTable = new TextTable
                {
                    SectionTextContentID = sectionTextContentId,
                    Width = tableEl.Attribute(sc.A.Width)?.Value,
                    HasHeader = tableEl.SplElement(sc.E.Thead) != null, // Check for header section
                    HasFooter = tableEl.SplElement(sc.E.Tfoot) != null  // Check for footer section
                };

                // Persist new table to database
                await tableRepo.CreateAsync(textTable);
                createdCount++;
            }

            // Validate table creation was successful before proceeding
            if (textTable.TextTableID == null)
            {
                context.Logger?.LogError("Failed to create or retrieve TextTable for SectionTextContentID {id}", sectionTextContentId);
                return createdCount;
            }

            // 2. Parse rows for each section of the table (header, body, footer)
            // Process table header section if present
            var theadEl = tableEl.SplElement(sc.E.Thead);
            if (theadEl != null)
            {
                // Parse header rows and accumulate creation count
                createdCount += await parseAndCreateRowsAsync(theadEl, textTable.TextTableID.Value, "Header", context);
            }

            // Process table body section if present
            var tbodyEl = tableEl.SplElement(sc.E.Tbody);
            if (tbodyEl != null)
            {
                // Parse body rows and accumulate creation count
                createdCount += await parseAndCreateRowsAsync(tbodyEl, textTable.TextTableID.Value, "Body", context);
            }

            // Process table footer section if present
            var tfootEl = tableEl.SplElement(sc.E.Tfoot);
            if (tfootEl != null)
            {
                // Parse footer rows and accumulate creation count
                createdCount += await parseAndCreateRowsAsync(tfootEl, textTable.TextTableID.Value, "Footer", context);
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds and delegates parsing of a manufacturedProduct element within the section.
        /// Navigates SPL hierarchy to locate product elements for specialized processing.
        /// </summary>
        /// <param name="sectionEl">The XElement of the section.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="reportProgress">Optional progress reporting action.</param>
        /// <returns>The SplParseResult from the product parser, or an empty result if no product exists.</returns>
        /// <seealso cref="ManufacturedProductParser"/>
        /// <seealso cref="SplParseResult"/>
        /// <seealso cref="Label"/>
        private async Task<SplParseResult> parseManufacturedProductAsync(XElement sectionEl, SplParseContext context, Action<string>? reportProgress)
        {
            #region implementation
            // Navigate through the SPL hierarchy: section/subject/manufacturedProduct
            // Follow standard SPL document structure to locate product elements
            var productEl = sectionEl.SplElement(sc.E.Subject, sc.E.ManufacturedProduct);

            // Delegate to specialized product parser if product element exists
            if (productEl != null)
            {
                var productParser = new ManufacturedProductParser();
                return await productParser.ParseAsync(productEl, context, reportProgress);
            }

            // Return a default successful result if no product element is found
            return new SplParseResult { Success = true };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Orchestrates the recursive parsing of a section's [text] element. It iterates
        /// through top-level content blocks and delegates the processing of each block
        /// to specialized helpers, preserving the nested hierarchy for SPL round-tripping.
        /// </summary>
        /// <param name="parentEl">The XElement to start parsing (typically the [text] element).</param>
        /// <param name="sectionId">The SectionID owning this content.</param>
        /// <param name="context">Parsing context.</param>
        /// <param name="parseAndSaveSectionAsync">Function delegate for parsing and saving child sections.</param>
        /// <param name="parentSectionTextContentId">Parent SectionTextContentID for hierarchy, or null for top-level blocks.</param>
        /// <param name="sequence">The sequence number to start from (default 1).</param>
        /// <returns>A tuple containing the complete list of all created/found SectionTextContent objects and the total count of grandchild entities (e.g., list items, table cells).</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<Tuple<List<SectionTextContent>, int>> getOrCreateSectionTextContentsAsync(
            XElement parentEl,
            int sectionId,
            SplParseContext context,
            Func<XElement, SplParseContext, Task<Section>> parseAndSaveSectionAsync,
            int? parentSectionTextContentId = null,
            int sequence = 1)
        {
            #region implementation
            var allCreatedContent = new List<SectionTextContent>();
            int totalGrandChildEntities = 0;

            // Validate all required input parameters and context dependencies
            if (parentEl == null || sectionId <= 0 || context?.ServiceProvider == null || context.Logger == null)
            {
                return Tuple.Create(allCreatedContent, totalGrandChildEntities);
            }

            // Initialize sequence counter for maintaining content block order
            int seq = sequence;

            // Process each content block in the section using tree building helper
            foreach (var block in parentEl.SplBuildSectionContentTree())
            {
                // Delegate the processing of a single block to a dedicated handler
                // Use specialized handler for individual content block processing
                var blockResult = await processContentBlockAsync(
                    block,
                    sectionId,
                    context,
                    parseAndSaveSectionAsync,
                    parentSectionTextContentId,
                    seq);

                // Aggregate results from the processed block
                // Combine results from current block processing into overall collection
                if (blockResult != null)
                {
                    allCreatedContent.Add(blockResult.MainContent);
                    allCreatedContent.AddRange(blockResult.NestedContent);
                    totalGrandChildEntities += blockResult.GrandchildEntityCount;
                }

                // Increment sequence for next content block
                seq++;
            }

            return Tuple.Create(allCreatedContent, totalGrandChildEntities);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes a single content block ([paragraph], [list], etc.), creating its
        /// corresponding SectionTextContent record, handling specialized content, and
        /// initiating recursion for any nested blocks.
        /// </summary>
        /// <param name="block">The XElement representing the content block to process.</param>
        /// <param name="sectionId">The owning section ID.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="parseAndSaveSectionAsync">Function delegate for parsing child sections.</param>
        /// <param name="parentSectionTextContentId">Parent content ID for hierarchy.</param>
        /// <param name="sequence">Sequence number for ordering.</param>
        /// <returns>A ProcessBlockResult containing the created content and metadata, or null if the block is skipped.</returns>
        /// <seealso cref="ProcessBlockResult"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static async Task<ProcessBlockResult?> processContentBlockAsync(
            XElement block,
            int sectionId,
            SplParseContext context,
            Func<XElement, SplParseContext, Task<Section>> parseAndSaveSectionAsync,
            int? parentSectionTextContentId,
            int sequence)
        {
            #region implementation
            // Highlights are processed by getOrCreateSectionExcerptHighlightsAsync within an Excerpt.
            // Skipping them here prevents creating a duplicate SectionTextContent record.
            // Skip highlight elements as they are handled separately within excerpt processing
            if (block.Name.LocalName.Equals(sc.E.Highlight, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // 1. Find or create the primary SectionTextContent record for this block
            // Create or retrieve the main content entity for this block
            var stc = await findOrCreateSectionTextContentRecordAsync(block, sectionId, context, parentSectionTextContentId, sequence);
            if (stc == null) return null;

            // Initialize result container with main content entity
            var result = new ProcessBlockResult { MainContent = stc };

            // 2. Process specialized content (e.g., parse list items, table cells, or excerpt highlights)
            // Handle complex nested structures like lists, tables, and excerpts
            result.GrandchildEntityCount = await processSpecializedContentAsync(block, stc, context);

            // 3. Recurse for any nested child blocks, but ONLY if the block is not a type
            // that was fully handled by the specialized parser. This prevents the dual-parsing conflict.
            var contentType = stc.ContentType ?? string.Empty;
            if (!contentType.Equals(sc.E.List, StringComparison.OrdinalIgnoreCase) &&
                !contentType.Equals(sc.E.Table, StringComparison.OrdinalIgnoreCase))
            {
                // Process nested content blocks within the current block
                var childResult = await processChildContentBlocksAsync(block, stc, context, parseAndSaveSectionAsync);
                result.NestedContent.AddRange(childResult.Item1);
                result.GrandchildEntityCount += childResult.Item2;
            }

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds an existing SectionTextContent record in the database or creates a new one.
        /// This method encapsulates the database look-up and creation logic.
        /// </summary>
        /// <param name="block">The XElement representing the content block.</param>
        /// <param name="sectionId">The owning section ID.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="parentId">Parent content ID for hierarchy.</param>
        /// <param name="sequence">Sequence number for ordering.</param>
        /// <returns>The found or newly created SectionTextContent entity.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<SectionTextContent?> findOrCreateSectionTextContentRecordAsync(
            XElement block,
            int sectionId,
            SplParseContext context,
            int? parentId,
            int sequence)
        {
            #region implementation
            if (context == null || context.ServiceProvider == null)
                return null;

            // Get database context and repository for content operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = context.GetRepository<SectionTextContent>();

            // Standardize content type name with proper capitalization
            var contentType = char.ToUpper(block.Name.LocalName[0]) + block.Name.LocalName.Substring(1);

            // Extract optional style code for formatting information
            var styleCode = block.Attribute(sc.A.StyleCode)?.Value?.Trim();

            // Helper function to extract inner XML while preserving markup
            string? getInnerXml(XElement element) => string.Concat(element.Nodes().Select(n => n.ToString())).Trim();

            // ContentText is null for container types like List or Table.
            // Extract content text only for non-container elements
            string? contentText = null;
            if (!contentType.Equals(sc.E.List, StringComparison.OrdinalIgnoreCase) &&
                !contentType.Equals(sc.E.Table, StringComparison.OrdinalIgnoreCase))
            {
                contentText = getInnerXml(block);
            }

            // Deduplication: Find existing record based on a unique signature
            // Search for existing content with matching attributes and hierarchy
            var existing = await dbContext.Set<SectionTextContent>().FirstOrDefaultAsync(c =>
                c.SectionID == sectionId &&
                c.ContentType == contentType &&
                c.SequenceNumber == sequence &&
                c.ParentSectionTextContentID == parentId &&
                (contentType.ToLower() != sc.E.Paragraph || c.ContentText == contentText));

            // Return existing content if found to avoid duplicates
            if (existing != null)
            {
                return existing;
            }

            // Create a new record if not found
            // Build new content entity with extracted attributes and hierarchy
            var newStc = new SectionTextContent
            {
                SectionID = sectionId,
                ParentSectionTextContentID = parentId,
                ContentType = contentType,
                StyleCode = styleCode,
                SequenceNumber = sequence,
                ContentText = contentText
            };

            // Persist new content to database
            await repo.CreateAsync(newStc);
            return newStc;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Dispatches processing for special content types that have nested data structures,
        /// such as Lists, Tables, and Excerpts with Highlights.
        /// </summary>
        /// <param name="block">The XElement representing the content block.</param>
        /// <param name="stc">The SectionTextContent entity for this block.</param>
        /// <param name="context">The current parsing context.</param>
        /// <returns>The number of grandchild entities created (e.g., list items, table cells).</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static async Task<int> processSpecializedContentAsync(XElement block,
            SectionTextContent stc,
            SplParseContext context)
        {
            #region implementation
            // Validate content entity has valid ID before processing children
            if (!stc.SectionTextContentID.HasValue) return 0;

            int grandchildEntitiesCount = 0;
            var contentType = stc.ContentType ?? string.Empty;

            // Dispatch to appropriate specialized handler based on content type
            if (contentType.Equals(sc.E.List, StringComparison.OrdinalIgnoreCase))
            {
                // Process list structure and create list item entities
                grandchildEntitiesCount += await getOrCreateTextListAndItemsAsync(block, stc.SectionTextContentID.Value, context);
            }
            else if (contentType.Equals(sc.E.Table, StringComparison.OrdinalIgnoreCase))
            {
                // Process table structure and create row/cell entities
                grandchildEntitiesCount += await getOrCreateTextTableAndChildrenAsync(block, stc.SectionTextContentID.Value, context);
            }
            else if (contentType.Equals(sc.E.Excerpt, StringComparison.OrdinalIgnoreCase))
            {
                // This method doesn't return a count, but we call it for its side effect.
                // Process excerpt highlights for specialized content extraction
                if (stc.SectionID > 0)
                    await getOrCreateSectionExcerptHighlightsAsync(block, (int)stc.SectionID, context);
            }

            return grandchildEntitiesCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Handles the recursive processing of nested content blocks within a given parent block.
        /// Manages hierarchy relationships and sequence numbering for child content.
        /// </summary>
        /// <param name="parentBlock">The parent XElement containing child blocks.</param>
        /// <param name="parentStc">The parent SectionTextContent entity.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="parseAndSaveSectionAsync">Function delegate for parsing child sections.</param>
        /// <returns>A tuple containing the list of nested SectionTextContent objects and the count of their grandchild entities.</returns>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static async Task<Tuple<List<SectionTextContent>, int>> processChildContentBlocksAsync(
            XElement parentBlock,
            SectionTextContent parentStc,
            SplParseContext context,
            Func<XElement, SplParseContext, Task<Section>> parseAndSaveSectionAsync)
        {
            #region implementation
            // Find child content blocks within the parent element
            var childBlocks = parentBlock.SplBuildSectionContentTree().ToList();
            if (!childBlocks.Any() || parentStc == null || parentStc.SectionID == null)
            {
                // Return empty results if no child blocks found
                return Tuple.Create(new List<SectionTextContent>(), 0);
            }

            // Recurse by calling the main orchestrator for the children of the current block.
            // The current block's ID becomes the parent ID for the next level.
            // Recursively process child blocks with current block as parent
            return await getOrCreateSectionTextContentsAsync(
                parentBlock,
                (int)parentStc.SectionID,
                context,
                parseAndSaveSectionAsync,
                parentStc.SectionTextContentID,
                1); // Child sequence always restarts at 1 for each new parent.


            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a [list] element and its child [item] elements, creating and saving
        /// TextList and TextListItem records to the database. This method handles the
        /// specific structure of SPL lists, including attributes and nested content.
        /// It performs deduplication to avoid creating duplicate records for the same content.
        /// </summary>
        /// <param name="listEl">The XElement representing the [list] element.</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent record (where ContentType='List').</param>
        /// <param name="context">The current parsing context for database access.</param>
        /// <returns>A task that resolves to the total number of TextList and TextListItem entities created.</returns>
        /// <remarks>
        /// Assumes the SplConstants class (aliased as 'sc') contains constants for list elements and attributes:
        /// sc.A.ListType ("listType"), sc.A.StyleCode ("styleCode"), sc.E.Item ("item"), sc.E.Caption ("caption").
        /// </remarks>
        /// <seealso cref="TextList"/>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        private static async Task<int> getOrCreateTextListAndItemsAsync(
            XElement listEl,
            int sectionTextContentId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate inputs
            if (listEl == null
                || sectionTextContentId <= 0
                || context == null
                || context?.ServiceProvider == null)
            {
                return 0;
            }

            // Get DB context and repositories
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var textListRepo = context.GetRepository<TextList>();
            var textListItemRepo = context.GetRepository<TextListItem>();

            // 1. Find or Create the TextList record
            var textListDbSet = dbContext.Set<TextList>();
            var textList = await textListDbSet.FirstOrDefaultAsync(l => l.SectionTextContentID == sectionTextContentId);

            if (textList == null)
            {
                textList = new TextList
                {
                    SectionTextContentID = sectionTextContentId,
                    ListType = listEl.Attribute(sc.A.ListType)?.Value,
                    StyleCode = listEl.Attribute(sc.A.StyleCode)?.Value
                };
                await textListRepo.CreateAsync(textList);
                createdCount++;
            }

            if (textList.TextListID == null)
            {
                context.Logger?.LogError("Failed to create or retrieve TextList for SectionTextContentID {id}", sectionTextContentId);
                return createdCount; // Cannot proceed without a parent TextListID
            }

            // 2. Find or Create TextListItem records for each <item>
            var textListItemDbSet = dbContext.Set<TextListItem>();
            var itemElements = listEl.SplElements(sc.E.Item).ToList();
            int seqNum = 1;

            foreach (var itemEl in itemElements)
            {
                // Extract the content of the item
                var itemText = getItemXml(itemEl);

                // If the item's text content is empty, skip this iteration entirely
                // This prevents creating empty records and incorrectly advancing the sequence
                if (string.IsNullOrWhiteSpace(itemText))
                {
                    continue; // Move to the next <item> element
                }

                // Now that we know the item has content, proceed with deduplication and creation
                var existingItem = await textListItemDbSet.FirstOrDefaultAsync(i =>
                    i.TextListID == textList.TextListID &&
                    i.SequenceNumber == seqNum);

                if (existingItem == null)
                {
                    var newItem = new TextListItem
                    {
                        TextListID = textList.TextListID,
                        SequenceNumber = seqNum,
                        ItemCaption = itemEl.SplElement(sc.E.Caption)?.Value?.Trim(),
                        ItemText = itemText // Use the pre-fetched and validated text
                    };
                    await textListItemRepo.CreateAsync(newItem);
                    createdCount++;
                }

                seqNum++;
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the inner XML of a list [item] element, preserving all markup,
        /// but excluding the [caption] element itself.
        /// </summary>
        /// <param name="itemElement">The [item] XElement to process.</param>
        /// <returns>The inner XML as a string, or null if the input is null.</returns>
        private static string? getItemXml(XElement itemElement)
        {
            #region implementation
            if (itemElement == null) return null;

            // Create a temporary clone to manipulate without affecting the original XDocument tree.
            var clone = new XElement(itemElement);

            // Find and remove the <caption/> element from the clone, if it exists.
            clone.Element(itemElement.GetDefaultNamespace() + sc.E.Caption)?.Remove();

            // Concatenate the remaining nodes (including text and other elements/tags) into a single string.
            return string.Concat(clone.Nodes().Select(n => n.ToString())).Trim();
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
        /// <param name="excerptEl">The XElement to search for excerpt/highlight/text patterns.</param>
        /// <param name="sectionId">The SectionID owning this highlight content.</param>
        /// <param name="context">Parsing context for repository and database access.</param>
        /// <returns>List of SectionExcerptHighlight objects (created or found).</returns>
        /// <seealso cref="SectionExcerptHighlight"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<List<SectionExcerptHighlight>> getOrCreateSectionExcerptHighlightsAsync(
            XElement excerptEl,
            int sectionId,
            SplParseContext context)
        {
            #region implementation
            var highlights = new List<SectionExcerptHighlight>();

            // Validate required input parameters
            if (excerptEl == null || sectionId <= 0)
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
            foreach (var highlightTextEl in excerptEl
                 .Descendants(ns + sc.E.Text) // More direct search
                 .Where(x => x.Parent?.Name.LocalName == sc.E.Highlight))
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
        /// <param name="textElement"></param>
        /// <returns></returns>
        private static string? getHighlightXml(XElement textElement)
        {
            #region implementation
            // Find the <text> element directly under <highlight>
            //var textEl = textElement?.Element(textElement.GetDefaultNamespace() + "text")
            //             ?? textElement?.Element("text"); // fallback if no namespace

            if (textElement == null)
                return null;

            // Return concatenated inner XML (preserving all tags/markup)
            return string.Concat(textElement.Nodes().Select(n => n.ToString())).Trim();
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
    #endregion Private Helper Methods
}