using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Xml.Linq;
using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Windows.UI;
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

        #region classes
        /**************************************************************/
        /// <summary>
        /// Helper class to encapsulate the results of processing a single content block.
        /// Contains the main content entity, nested content, and count of grandchild entities.
        /// </summary>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="Label"/>
        private class processBlockResult
        {
            #region implementation
            /// <summary>
            /// The primary SectionTextContent entity created for this content block.
            /// </summary>
            /// <seealso cref="SectionTextContent"/>
            public SectionTextContent? MainContent { get; set; }

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

        /**************************************************************/
        /// <summary>
        /// Represents the main subject information extracted from an identified substance.
        /// </summary>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="Label"/>
        private class mainSubjectInfo
        {
            #region implementation
            /// <summary>
            /// Gets or sets the identifier code value.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? Identifier { get; set; }

            /// <summary>
            /// Gets or sets the system OID.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? SystemOid { get; set; }

            /// <summary>
            /// Gets or sets the display name.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? DisplayName { get; set; }

            /// <summary>
            /// Gets or sets the subject type (ActiveMoiety or PharmacologicClass).
            /// </summary>
            /// <seealso cref="Label"/>
            public string? SubjectType { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether this is a definition.
            /// </summary>
            /// <seealso cref="Label"/>
            public bool IsDefinition { get; set; }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Represents pharmacologic class information extracted from a specialized kind element.
        /// </summary>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="Label"/>
        private class pharmacologicClassInfo
        {
            #region implementation
            /// <summary>
            /// Gets or sets the pharmacologic class code.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? Code { get; set; }

            /// <summary>
            /// Gets or sets the pharmacologic class system OID.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? System { get; set; }

            /// <summary>
            /// Gets or sets the pharmacologic class display name.
            /// </summary>
            /// <seealso cref="Label"/>
            public string? DisplayName { get; set; }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Represents the repositories needed for text list processing.
        /// </summary>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="TextList"/>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="Label"/>
        private class TextListRepositories
        {
            #region implementation
            /// <summary>
            /// Gets or sets the database context.
            /// </summary>
            /// <seealso cref="ApplicationDbContext"/>
            /// <seealso cref="Label"/>
            public ApplicationDbContext? DbContext { get; set; }

            /// <summary>
            /// Gets or sets the TextList repository.
            /// </summary>
            /// <seealso cref="TextList"/>
            /// <seealso cref="Label"/>
            public Repository<TextList>? TextListRepo { get; set; }

            /// <summary>
            /// Gets or sets the TextListItem repository.
            /// </summary>
            /// <seealso cref="TextListItem"/>
            /// <seealso cref="Label"/>
            public Repository<TextListItem>? TextListItemRepo { get; set; }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Represents the result of processing a text list item.
        /// </summary>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="Label"/>
        private class TextListItemProcessResult
        {
            #region implementation
            /// <summary>
            /// Gets or sets a value indicating whether the item was processed (had content).
            /// </summary>
            /// <seealso cref="Label"/>
            public bool WasProcessed { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether a new item was created.
            /// </summary>
            /// <seealso cref="Label"/>
            public bool WasCreated { get; set; }
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
        /// <seealso cref="XElementExtensions"/>
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

                    // 6. Parse Identified Substances for Pharmacologic Class Indexing
                    var identifiedSubstancesCreated = await parseAndSaveIdentifiedSubstancesAsync(xEl, section, context);
                    result.ProductElementsCreated += identifiedSubstancesCreated;

                    // 7. Parse Billing Unit Indexing if applicable
                    // Check if this is a Billing Unit Indexing section (Code 48779-3)
                    // inside a Billing Unit Indexing document (Code 71446-9).
                    if (context.Document?.DocumentCode == "71446-9" && section.SectionCode == "48779-3")
                    {
                        var billingUnitResult = await parseAndSaveBillingUnitIndexAsync(xEl, section, context);
                        result.SectionAttributesCreated += billingUnitResult;
                    }

                    // 8. Parse Product Concept Indexing if applicable
                    // Check if this is a Product Concept Indexing section (Code 48779-3)
                    // inside a Product Concept Indexing document (Code 71445-1).
                    if (context.Document?.DocumentCode == "71445-1" && section.SectionCode == "48779-3")
                    {
                        var conceptResult = await parseAndSaveProductConceptsAsync(xEl, section, context);
                        result.SectionAttributesCreated += conceptResult;
                    }

                    // 9. Parse Drug Interactions Indexing if applicable
                    // Check if this is a Drug Interactions Indexing document (Code 71444-4)
                    if (context.Document?.DocumentCode == "71444-4")
                    {
                        var interactionResult = await parseAndSaveDrugInteractionsAsync(xEl, section, context);
                        result.SectionAttributesCreated += interactionResult;
                    }

                    // 10. Parse National Clinical Trials Indexing if applicable
                    // Check if this is a National Clinical Trials Indexing document 
                    var nctLinkResult = await parseAndSaveNCTLinksAsync(xEl, section, context);
                    result.SectionAttributesCreated += nctLinkResult;

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
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private async Task<Section?> createAndSaveSectionAsync(XElement xEl, SplParseContext context)
        {
            #region implementation
            // Build section entity with extracted metadata from XML attributes and elements
            var section = new Section
            {
                StructuredBodyID = context.StructuredBody!.StructuredBodyID!.Value,
                SectionLinkGUID = xEl.GetAttrVal(sc.A.ID),
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

            // Process observationMedia elements for images
            var observationMedia = await getOrCreateObservationMediaAsync(xEl, sectionId, context);
            result.SectionAttributesCreated += observationMedia.Count;

            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates ObservationMedia records for all [observationMedia] elements
        /// within a section, capturing image metadata for database storage.
        /// </summary>
        /// <param name="sectionEl">The XElement for the parent [section].</param>
        /// <param name="sectionId">The SectionID of the parent section (already saved).</param>
        /// <param name="context">Parsing context for repo/db access.</param>
        /// <returns>List of ObservationMedia objects (created or found).</returns>
        /// <seealso cref="ObservationMedia"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<List<ObservationMedia>> getOrCreateObservationMediaAsync(
            XElement sectionEl,
            int sectionId,
            SplParseContext context)
        {
            #region implementation
            var mediaList = new List<ObservationMedia>();

            // Validate required input parameters to prevent null reference exceptions
            if (sectionEl == null || sectionId <= 0)
                return mediaList;

            // Validate required context dependencies to ensure proper service resolution
            if (context?.ServiceProvider == null)
                return mediaList;

            // Get database context and repository for ObservationMedia operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var repo = context.GetRepository<ObservationMedia>();
            var dbSet = dbContext.Set<ObservationMedia>();

            // Find all <component><observationMedia> children within the section
            var mediaElements = sectionEl.SplElements(sc.E.Component, sc.E.ObservationMedia);

            foreach (var mediaEl in mediaElements)
            {
                if (mediaEl == null) continue;

                // Extract data from the XML element
                var mediaId = mediaEl.GetAttrVal(sc.A.ID);
                if (string.IsNullOrWhiteSpace(mediaId))
                {
                    // ID is crucial for linking, skip if missing
                    continue;
                }

                // Deduplicate based on SectionID and the media's own ID attribute
                var existingMedia = await dbSet.FirstOrDefaultAsync(m =>
                    m.SectionID == sectionId &&
                    m.MediaID == mediaId);

                if (existingMedia != null)
                {
                    // Use existing media record instead of creating duplicate
                    mediaList.Add(existingMedia);
                    continue;
                }

                // Not found, so create a new one with extracted XML data
                var newMedia = new ObservationMedia
                {
                    SectionID = sectionId,
                    MediaID = mediaId,
                    DescriptionText = mediaEl.GetSplElementVal(sc.E.Text),
                    MediaType = mediaEl.GetSplElementAttrVal(sc.E.Value, sc.A.MediaType),
                    XsiType = mediaEl.SplElement(sc.E.Value)?.GetXsiType(),
                    // Path: observationMedia > value > reference
                    FileName = mediaEl.SplElement(sc.E.Value)?.SplElement(sc.E.Reference)?.Attribute(sc.A.Value)?.Value
                };

                // Save new media record to database
                await repo.CreateAsync(newMedia);
                mediaList.Add(newMedia);
            }

            return mediaList;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates RenderedMedia records for all [renderMultimedia] tags within a given content block.
        /// This method links a SectionTextContent entry to its corresponding ObservationMedia entry.
        /// </summary>
        /// <param name="contentBlockEl">The XElement for the content block (e.g., a paragraph or a block-level renderMultimedia).</param>
        /// <param name="sectionTextContentId">The ID of the parent SectionTextContent record.</param>
        /// <param name="context">The current parsing context.</param>
        /// <param name="isInline">Indicates if the media is rendered inline within text or as a standalone block.</param>
        /// <returns>The number of RenderedMedia entities created.</returns>
        /// <seealso cref="RenderedMedia"/>
        /// <seealso cref="ObservationMedia"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="Label"/>
        private static async Task<int> getOrCreateRenderedMediaAsync(
            XElement contentBlockEl,
            int sectionTextContentId,
            SplParseContext context,
            bool isInline)
        {
            #region implementation
            int createdCount = 0;

            // Validate context to ensure required services and current section are available
            if (context?.ServiceProvider == null || context?.CurrentSection == null) return 0;

            // Get DB context and repositories for RenderedMedia and ObservationMedia operations
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var renderedMediaRepo = context.GetRepository<RenderedMedia>();
            var renderedMediaDbSet = dbContext.Set<RenderedMedia>();
            var observationMediaDbSet = dbContext.Set<ObservationMedia>();

            // Find all <renderMultimedia> elements. If the block is the element itself, this will be the only one.
            // If it's a paragraph, it will find all descendants.
            var renderedElements = contentBlockEl.Name.LocalName == sc.E.RenderMultimedia
                ? new List<XElement> { contentBlockEl }
                : contentBlockEl.Descendants(ns + sc.E.RenderMultimedia).ToList();

            // Return early if no renderMultimedia elements found
            if (!renderedElements.Any()) return 0;

            int seqNum = 1;
            foreach (var el in renderedElements)
            {
                // Extract the referenced object ID from the renderMultimedia element
                var referencedObjectId = el.Attribute(sc.A.ReferencedObject)?.Value;
                if (string.IsNullOrWhiteSpace(referencedObjectId))
                {
                    // Log warning for missing referencedObject attribute
                    context.Logger?.LogWarning("Found <renderMultimedia> tag with no referencedObject attribute in file {FileName}.", context.FileNameInZip);
                    continue;
                }

                // Find the ObservationMedia this tag refers to.
                // Note: This assumes ObservationMedia for the entire section has already been parsed.
                var observationMedia = await observationMediaDbSet
                    .FirstOrDefaultAsync(om => om.MediaID == referencedObjectId && om.SectionID == context.CurrentSection.SectionID);

                if (observationMedia?.ObservationMediaID == null)
                {
                    // Log warning for dangling reference when no matching ObservationMedia found
                    context.Logger?.LogWarning("Dangling reference: <renderMultimedia referencedObject='{RefId}'> found, but no matching <observationMedia> was found in the same section in file {FileName}.", referencedObjectId, context.FileNameInZip);
                    continue;
                }

                // Deduplicate: Check if this link already exists to avoid duplicate records
                var existingLink = await renderedMediaDbSet.FirstOrDefaultAsync(rm =>
                    rm.SectionTextContentID == sectionTextContentId &&
                    rm.ObservationMediaID == observationMedia.ObservationMediaID &&
                    rm.SequenceInContent == seqNum);

                if (existingLink == null)
                {
                    // Create new RenderedMedia link between SectionTextContent and ObservationMedia
                    var newLink = new RenderedMedia
                    {
                        SectionTextContentID = sectionTextContentId,
                        ObservationMediaID = observationMedia.ObservationMediaID,
                        SequenceInContent = seqNum,
                        IsInline = isInline
                    };
                    await renderedMediaRepo.CreateAsync(newLink);
                    createdCount++;
                }
                // Increment sequence number for proper ordering of media within content
                seqNum++;
            }

            return createdCount;
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
        /// <seealso cref="XElementExtensions"/>
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
        /// <seealso cref="XElementExtensions"/>
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
        /// <seealso cref="XElementExtensions"/>
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
        /// <seealso cref="XElementExtensions"/>
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
        /// <seealso cref="XElementExtensions"/>
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
        /// <seealso cref="XElementExtensions"/>
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
        /// <seealso cref="processBlockResult"/>
        /// <seealso cref="SectionTextContent"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static async Task<processBlockResult?> processContentBlockAsync(
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
            var result = new processBlockResult { MainContent = stc };

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
        /// <seealso cref="XElementExtensions"/>
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
            else if (contentType.Equals(sc.E.RenderMultimedia, StringComparison.OrdinalIgnoreCase))
            {
                // Handle block-level images, where <renderMultimedia> is its own content block.
                grandchildEntitiesCount += await getOrCreateRenderedMediaAsync(block, stc.SectionTextContentID.Value, context, isInline: false);
            }

            // Check for INLINE images inside other content types, like Paragraph.
            // This runs in addition to the handlers above.
            if (block.Descendants(ns + sc.E.RenderMultimedia).Any())
            {
                // If the block itself isn't a RenderMultiMedia tag, any images inside it must be inline.
                bool isInline = !contentType.Equals(sc.E.RenderMultimedia, StringComparison.OrdinalIgnoreCase);
                if (isInline)
                {
                    grandchildEntitiesCount += await getOrCreateRenderedMediaAsync(block, stc.SectionTextContentID.Value, context, isInline: true);
                }
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
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static async Task<int> getOrCreateTextListAndItemsAsync(
            XElement listEl,
            int sectionTextContentId,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;

            // Validate inputs using existing validation pattern
            if (!validateTextListInputs(listEl, sectionTextContentId, context))
            {
                return 0;
            }

            // Get database context and repositories
            var repositories = getTextListRepositories(context);

            if (repositories != null
                && repositories.TextListRepo != null
                && repositories.TextListItemRepo != null)
            {
                var dbContext = repositories.DbContext;

                // Find or create the main TextList record
                if (dbContext != null)
                {
                    var textList = await getOrCreateTextListAsync(dbContext, repositories.TextListRepo, listEl, sectionTextContentId);
                    if (textList?.TextListID == null)
                    {
                        context.Logger?.LogError("Failed to create or retrieve TextList for SectionTextContentID {id}", sectionTextContentId);
                        return createdCount;
                    }

                    // Increment count if a new TextList was created
                    if (textList.TextListID > 0)
                    {
                        createdCount++;
                    }

                    // Process all list items
                    createdCount += await processTextListItems(dbContext, repositories.TextListItemRepo, listEl, textList, context);
                }
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the input parameters for text list processing.
        /// </summary>
        /// <param name="listEl">The XElement representing the list.</param>
        /// <param name="sectionTextContentId">The section text content ID.</param>
        /// <param name="context">The parsing context.</param>
        /// <returns>True if all inputs are valid, false otherwise.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static bool validateTextListInputs(XElement listEl, int sectionTextContentId, SplParseContext context)
        {
            #region implementation
            // Check for null or invalid parameters
            return listEl != null &&
                   sectionTextContentId > 0 &&
                   context?.ServiceProvider != null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the required repositories and database context for text list processing.
        /// </summary>
        /// <param name="context">The parsing context.</param>
        /// <returns>A tuple containing the database context and repositories.</returns>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="TextList"/>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="Label"/>
        private static TextListRepositories? getTextListRepositories(SplParseContext context)
        {
            #region implementation
            // Validate context and service provider
            if (context == null || context.ServiceProvider == null)
            {
                return null;
            }

            // Get database context and repositories from service provider
            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var textListRepo = context.GetRepository<TextList>();
            var textListItemRepo = context.GetRepository<TextListItem>();

            return new TextListRepositories
            {
                DbContext = dbContext,
                TextListRepo = textListRepo,
                TextListItemRepo = textListItemRepo
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds or creates a TextList record for the given list element and section content.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="textListRepo">The TextList repository.</param>
        /// <param name="listEl">The XElement representing the list.</param>
        /// <param name="sectionTextContentId">The section text content ID.</param>
        /// <returns>The existing or newly created TextList entity.</returns>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="TextList"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private static async Task<TextList> getOrCreateTextListAsync(
            ApplicationDbContext dbContext,
            Repository<TextList> textListRepo,
            XElement listEl,
            int sectionTextContentId)
        {
            #region implementation
            // Check if TextList already exists for this section content
            var textListDbSet = dbContext.Set<TextList>();
            var textList = await textListDbSet.FirstOrDefaultAsync(l => l.SectionTextContentID == sectionTextContentId);

            // Create new TextList if it doesn't exist
            if (textList == null)
            {
                textList = createTextListEntity(listEl, sectionTextContentId);
                await textListRepo.CreateAsync(textList);
            }

            return textList;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new TextList entity from the provided list element and section content ID.
        /// </summary>
        /// <param name="listEl">The XElement representing the list.</param>
        /// <param name="sectionTextContentId">The section text content ID.</param>
        /// <returns>A new TextList entity with populated attributes.</returns>
        /// <seealso cref="TextList"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private static TextList createTextListEntity(XElement listEl, int sectionTextContentId)
        {
            #region implementation
            // Extract list attributes and create new entity
            return new TextList
            {
                SectionTextContentID = sectionTextContentId,
                ListType = listEl.Attribute(sc.A.ListType)?.Value,
                StyleCode = listEl.Attribute(sc.A.StyleCode)?.Value
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes all list items within the list element, creating TextListItem records as needed.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="textListItemRepo">The TextListItem repository.</param>
        /// <param name="listEl">The XElement representing the list.</param>
        /// <param name="textList">The parent TextList entity.</param>
        /// <param name="context">The parsing context for logging.</param>
        /// <returns>The count of TextListItem records created.</returns>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="TextList"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Label"/>
        private static async Task<int> processTextListItems(
            ApplicationDbContext dbContext,
            Repository<TextListItem> textListItemRepo,
            XElement listEl,
            TextList textList,
            SplParseContext context)
        {
            #region implementation
            int createdCount = 0;
            var textListItemDbSet = dbContext.Set<TextListItem>();

            // Get all item elements from the list
            var itemElements = listEl.SplElements(sc.E.Item).ToList();
            int seqNum = 1;

            // Process each item element
            foreach (var itemEl in itemElements)
            {
                var itemProcessResult = await processTextListItem(
                    textListItemDbSet, textListItemRepo, itemEl, textList, seqNum);

                // Only increment sequence if item was processed (had content)
                if (itemProcessResult.WasProcessed)
                {
                    if (itemProcessResult.WasCreated)
                    {
                        createdCount++;
                    }
                    seqNum++;
                }
            }

            return createdCount;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes a single list item element, creating a TextListItem record if needed.
        /// </summary>
        /// <param name="textListItemDbSet">The TextListItem DbSet for querying.</param>
        /// <param name="textListItemRepo">The TextListItem repository for creation.</param>
        /// <param name="itemEl">The XElement representing the item.</param>
        /// <param name="textList">The parent TextList entity.</param>
        /// <param name="seqNum">The sequence number for this item.</param>
        /// <returns>A result indicating whether the item was processed and created.</returns>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="TextList"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private static async Task<TextListItemProcessResult> processTextListItem(
            DbSet<TextListItem> textListItemDbSet,
            Repository<TextListItem> textListItemRepo,
            XElement itemEl,
            TextList textList,
            int seqNum)
        {
            #region implementation
            // Extract the content of the item
            var itemText = getItemXml(itemEl);

            // Skip items with empty content to prevent creating empty records
            if (string.IsNullOrWhiteSpace(itemText))
            {
                return new TextListItemProcessResult { WasProcessed = false, WasCreated = false };
            }

            // Check for existing item with same sequence number
            var existingItem = await textListItemDbSet.FirstOrDefaultAsync(i =>
                i.TextListID == textList.TextListID &&
                i.SequenceNumber == seqNum);

            // Create new item if it doesn't exist
            if (existingItem == null)
            {
                var newItem = createTextListItemEntity(itemEl, textList, seqNum, itemText);
                await textListItemRepo.CreateAsync(newItem);
                return new TextListItemProcessResult { WasProcessed = true, WasCreated = true };
            }

            return new TextListItemProcessResult { WasProcessed = true, WasCreated = false };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new TextListItem entity from the provided item element and metadata.
        /// </summary>
        /// <param name="itemEl">The XElement representing the item.</param>
        /// <param name="textList">The parent TextList entity.</param>
        /// <param name="seqNum">The sequence number for this item.</param>
        /// <param name="itemText">The extracted item text content.</param>
        /// <returns>A new TextListItem entity with populated properties.</returns>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="TextList"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private static TextListItem createTextListItemEntity(
            XElement itemEl,
            TextList textList,
            int seqNum,
            string itemText)
        {
            #region implementation
            // Extract caption and create new entity
            return new TextListItem
            {
                TextListID = textList.TextListID,
                SequenceNumber = seqNum,
                ItemCaption = itemEl.SplElement(sc.E.Caption)?.Value?.Trim(),
                ItemText = itemText
            };
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
        /// <seealso cref="XElementExtensions"/>
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
            var sectionLinkGuid = sectionEl.GetAttrVal(sc.A.ID);
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
        /// <seealso cref="XElementExtensions"/>
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
        /// <seealso cref="XElementExtensions"/>
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

        /**************************************************************/
        /// <summary>
        /// Parses the [subject][identifiedSubstance] element within a section for indexing information.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement.</param>
        /// <param name="section">The Section entity that has just been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of IdentifiedSubstance records created.</returns>
        /// <remarks>
        /// Handles both Pharmacologic Class Indexing (8.2.2) and Definition (8.2.3) sections.
        /// It parses the primary subject (Active Moiety or Pharm Class) and any associated
        /// specialized kinds (defining super-classes or associated classes).
        /// </remarks>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="getOrSaveIdentifiedSubstanceAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> parseAndSaveIdentifiedSubstancesAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            #region implementation
            int count = 0;

            // Validate input parameters and context
            if (!validateParseContext(context, section))
            {
                return count;
            }

            var dbContext = context!.ServiceProvider!.GetRequiredService<ApplicationDbContext>();

            // Extract the main identified substance element
            var identifiedSubstanceEl = extractIdentifiedSubstanceElement(sectionEl);
            if (identifiedSubstanceEl == null) return count;

            // Parse main subject information
            var mainSubjectInfo = extractMainSubjectInfo(identifiedSubstanceEl);
            if (mainSubjectInfo == null) return count;

            // Create the main identified substance record
            var mainIdentifiedSubstance = await getOrSaveIdentifiedSubstanceAsync(
                dbContext, section.SectionID, mainSubjectInfo.SubjectType,
                mainSubjectInfo.Identifier, mainSubjectInfo.SystemOid, mainSubjectInfo.IsDefinition);
            count++;

            // Process based on subject type (Active Moiety or Pharmacologic Class)
            if (mainSubjectInfo.SubjectType == "ActiveMoiety")
            {
                count += await processActiveMoietyIndexing(dbContext, identifiedSubstanceEl, mainIdentifiedSubstance);
            }
            else
            {
                count += await processPharmacologicClassDefinition(dbContext, identifiedSubstanceEl, mainIdentifiedSubstance, mainSubjectInfo);
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the parsing context and section parameters.
        /// </summary>
        /// <param name="context">The parsing context to validate.</param>
        /// <param name="section">The section to validate.</param>
        /// <returns>True if validation passes, false otherwise.</returns>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="Label"/>
        private bool validateParseContext(SplParseContext context, Section section)
        {
            #region implementation
            // Check for required context components
            return context?.ServiceProvider != null &&
                   context.Logger != null &&
                   section.SectionID.HasValue;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the inner identified substance element from the section element.
        /// </summary>
        /// <param name="sectionEl">The section XElement to search within.</param>
        /// <returns>The inner identified substance XElement, or null if not found.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private XElement? extractIdentifiedSubstanceElement(XElement sectionEl)
        {
            #region implementation
            // Navigate through the SPL structure: section > subject > identifiedSubstance > identifiedSubstance
            var subjectEl = sectionEl.GetSplElement(sc.E.Subject);
            var identifiedSubstanceEl = subjectEl?.GetSplElement(sc.E.IdentifiedSubstance);

            // Return the inner identified substance element
            return identifiedSubstanceEl?.GetSplElement(sc.E.IdentifiedSubstance);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the main subject information from the identified substance element.
        /// </summary>
        /// <param name="identifiedSubstanceEl">The identified substance XElement.</param>
        /// <returns>Main subject information, or null if required data is missing.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private mainSubjectInfo? extractMainSubjectInfo(XElement identifiedSubstanceEl)
        {
            #region implementation
            // Extract code information from the identified substance
            var mainCodeEl = identifiedSubstanceEl.GetSplElement(sc.E.Code);
            var mainIdentifier = mainCodeEl?.GetAttrVal(sc.A.CodeValue);
            var mainSystemOid = mainCodeEl?.GetAttrVal(sc.A.CodeSystem);
            var mainDisplayName = mainCodeEl?.GetAttrVal(sc.A.DisplayName);

            // Validate required fields
            if (string.IsNullOrWhiteSpace(mainIdentifier) || string.IsNullOrWhiteSpace(mainSystemOid))
            {
                return null;
            }

            // Determine subject type based on system OID
            string subjectType = mainSystemOid == "2.16.840.1.113883.4.9" ? "ActiveMoiety" : "PharmacologicClass";
            bool isDefinition = subjectType == "PharmacologicClass";

            return new mainSubjectInfo
            {
                Identifier = mainIdentifier,
                SystemOid = mainSystemOid,
                DisplayName = mainDisplayName,
                SubjectType = subjectType,
                IsDefinition = isDefinition
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes Active Moiety indexing (Spec 8.2.2) by linking moieties to their pharmacologic classes.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="identifiedSubstanceEl">The identified substance XElement.</param>
        /// <param name="mainIdentifiedSubstance">The main identified substance entity.</param>
        /// <returns>The count of records created during processing.</returns>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="getOrCreatePharmacologicClassAsync"/>
        /// <seealso cref="getOrCreatePharmacologicClassLinkAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> processActiveMoietyIndexing(
            ApplicationDbContext dbContext,
            XElement identifiedSubstanceEl,
            IdentifiedSubstance mainIdentifiedSubstance)
        {
            #region implementation
            int count = 0;

            // Process each specialized kind (pharmacologic class association)
            foreach (var specializedKindEl in identifiedSubstanceEl.SplElements(sc.E.AsSpecializedKind))
            {
                // Extract pharmacologic class information
                var classInfo = extractPharmacologicClassInfo(specializedKindEl);
                if (classInfo == null) continue;

                // Create or get the referenced pharmacologic class (not a definition)
                var pharmClass = await getOrCreatePharmacologicClassAsync(
                    dbContext, null, classInfo.Code, classInfo.System, classInfo.DisplayName);
                count++;

                // Create the link between the moiety and the class
                await getOrCreatePharmacologicClassLinkAsync(
                    dbContext, mainIdentifiedSubstance.IdentifiedSubstanceID, pharmClass.PharmacologicClassID);
                count++;
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes Pharmacologic Class definition (Spec 8.2.3) including names and hierarchy.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="identifiedSubstanceEl">The identified substance XElement.</param>
        /// <param name="mainIdentifiedSubstance">The main identified substance entity.</param>
        /// <param name="mainSubjectInfo">The main subject information.</param>
        /// <returns>The count of records created during processing.</returns>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="mainSubjectInfo"/>
        /// <seealso cref="getOrCreatePharmacologicClassAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> processPharmacologicClassDefinition(
            ApplicationDbContext dbContext,
            XElement identifiedSubstanceEl,
            IdentifiedSubstance mainIdentifiedSubstance,
            mainSubjectInfo mainSubjectInfo)
        {
            #region implementation
            int count = 0;

            // Create the main pharmacologic class record for this definition
            var mainPharmClass = await getOrCreatePharmacologicClassAsync(
                dbContext, mainIdentifiedSubstance.IdentifiedSubstanceID,
                mainSubjectInfo.Identifier, mainSubjectInfo.SystemOid, mainSubjectInfo.DisplayName);
            count++;

            // Process all names (preferred and alternate) for this class definition
            count += await processPharmacologicClassNames(dbContext, identifiedSubstanceEl, mainPharmClass);

            // Process all defining super-classes (hierarchy)
            count += await processPharmacologicClassHierarchy(dbContext, identifiedSubstanceEl, mainPharmClass);

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes all names (preferred and alternate) for a pharmacologic class definition.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="identifiedSubstanceEl">The identified substance XElement.</param>
        /// <param name="mainPharmClass">The main pharmacologic class entity.</param>
        /// <returns>The count of name records created.</returns>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="getOrCreatePharmacologicClassNameAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> processPharmacologicClassNames(
            ApplicationDbContext dbContext,
            XElement identifiedSubstanceEl,
            PharmacologicClass mainPharmClass)
        {
            #region implementation
            int count = 0;

            // Parse all name elements within the identified substance
            foreach (var nameEl in identifiedSubstanceEl.SplElements(sc.E.Name))
            {
                var nameValue = nameEl.Value?.Trim();
                var nameUse = nameEl.Attribute(sc.A.Use)?.Value ?? "A"; // Default to Alternate if 'use' is missing

                // Skip empty names
                if (string.IsNullOrWhiteSpace(nameValue)) continue;

                // Create the pharmacologic class name record
                await getOrCreatePharmacologicClassNameAsync(
                    dbContext, mainPharmClass.PharmacologicClassID, nameValue, nameUse);
                count++;
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes all defining super-classes (hierarchy) for a pharmacologic class definition.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="identifiedSubstanceEl">The identified substance XElement.</param>
        /// <param name="mainPharmClass">The main pharmacologic class entity.</param>
        /// <returns>The count of hierarchy records created.</returns>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="XElement"/>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="getOrCreatePharmacologicClassAsync"/>
        /// <seealso cref="getOrCreatePharmacologicClassHierarchyAsync"/>
        /// <seealso cref="Label"/>
        private async Task<int> processPharmacologicClassHierarchy(
            ApplicationDbContext dbContext,
            XElement identifiedSubstanceEl,
            PharmacologicClass mainPharmClass)
        {
            #region implementation
            int count = 0;

            // Process each specialized kind (parent class relationship)
            foreach (var specializedKindEl in identifiedSubstanceEl.SplElements(sc.E.AsSpecializedKind))
            {
                // Extract parent class information
                var parentClassInfo = extractPharmacologicClassInfo(specializedKindEl);
                if (parentClassInfo == null) continue;

                // Create or get the referenced parent pharmacologic class
                var parentPharmClass = await getOrCreatePharmacologicClassAsync(
                    dbContext, null, parentClassInfo.Code, parentClassInfo.System, parentClassInfo.DisplayName);
                count++;

                // Create the hierarchy link
                await getOrCreatePharmacologicClassHierarchyAsync(
                    dbContext, mainPharmClass.PharmacologicClassID, parentPharmClass.PharmacologicClassID);
                count++;
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts pharmacologic class information from a specialized kind element.
        /// </summary>
        /// <param name="specializedKindEl">The specialized kind XElement.</param>
        /// <returns>Pharmacologic class information, or null if required data is missing.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="XElementExtensions"/>
        /// <seealso cref="Label"/>
        private pharmacologicClassInfo? extractPharmacologicClassInfo(XElement specializedKindEl)
        {
            #region implementation
            // Navigate to the code element within the generalized material kind
            var classCodeEl = specializedKindEl.SplElement(sc.E.GeneralizedMaterialKind, sc.E.Code);
            var classCode = classCodeEl?.GetAttrVal(sc.A.CodeValue);
            var classSystem = classCodeEl?.GetAttrVal(sc.A.CodeSystem);
            var classDisplayName = classCodeEl?.GetAttrVal(sc.A.DisplayName);

            // Validate required code value
            if (string.IsNullOrWhiteSpace(classCode))
            {
                return null;
            }

            return new pharmacologicClassInfo
            {
                Code = classCode,
                System = classSystem,
                DisplayName = classDisplayName
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing IdentifiedSubstance or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="sectionId">The ID of the parent Section.</param>
        /// <param name="subjectType">The type of subject (e.g., "ActiveMoiety", "PharmacologicClass").</param>
        /// <param name="identifierValue">The identifier value (UNII or class code).</param>
        /// <param name="identifierSystemOid">The OID for the identifier system.</param>
        /// <param name="isDefinition">Flag indicating if this is a definition record.</param>
        /// <returns>The existing or newly created IdentifiedSubstance entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate indexing records within a section.
        /// Uniqueness is determined by the combination of the SectionID, identifier value, and system OID.
        /// </remarks>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<IdentifiedSubstance> getOrSaveIdentifiedSubstanceAsync(
            ApplicationDbContext dbContext,
            int? sectionId,
            string? subjectType,
            string? identifierValue,
            string? identifierSystemOid,
            bool? isDefinition)
        {
            #region implementation
            // Search for an existing record with the same key identifiers
            // Deduplication based on SectionID, identifier value, and system OID
            var existing = await dbContext.Set<IdentifiedSubstance>().FirstOrDefaultAsync(i =>
                i.SectionID == sectionId &&
                i.SubstanceIdentifierValue == identifierValue &&
                i.SubstanceIdentifierSystemOID == identifierSystemOid);

            // If a record already exists, return it to avoid creating duplicates
            if (existing != null)
            {
                return existing;
            }

            // Create a new IdentifiedSubstance entity with the provided indexing data
            var newIdentifiedSubstance = new IdentifiedSubstance
            {
                SectionID = sectionId,
                SubjectType = subjectType,
                SubstanceIdentifierValue = identifierValue,
                SubstanceIdentifierSystemOID = identifierSystemOid,
                IsDefinition = isDefinition
            };

            // Save the new entity to the database and persist changes immediately
            dbContext.Set<IdentifiedSubstance>().Add(newIdentifiedSubstance);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted identified substance
            return newIdentifiedSubstance;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing PharmacologicClass by its code and system, or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="identifiedSubstanceId">The parent IdentifiedSubstance ID (for definitions).</param>
        /// <param name="classCode">The MED-RT or MeSH code for the class.</param>
        /// <param name="classCodeSystem">The OID for the code system.</param>
        /// <param name="classDisplayName">The display name for the class.</param>
        /// <returns>The existing or newly created PharmacologicClass entity.</returns>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<PharmacologicClass> getOrCreatePharmacologicClassAsync(
            ApplicationDbContext dbContext,
            int? identifiedSubstanceId,
            string? classCode,
            string? classCodeSystem,
            string? classDisplayName)
        {
            #region implementation
            // Deduplicate by the unique class code and system to prevent duplicate class records
            var existing = await dbContext.Set<PharmacologicClass>().FirstOrDefaultAsync(pc =>
                pc.ClassCode == classCode &&
                pc.ClassCodeSystem == classCodeSystem);

            if (existing != null)
            {
                // Return existing pharmacologic class if found
                return existing;
            }

            // Create new PharmacologicClass entity with the provided classification data
            var newClass = new PharmacologicClass
            {
                IdentifiedSubstanceID = identifiedSubstanceId, // Only linked for definitions
                ClassCode = classCode,
                ClassCodeSystem = classCodeSystem,
                ClassDisplayName = classDisplayName
            };

            // Save the new pharmacologic class to the database and persist changes immediately
            dbContext.Set<PharmacologicClass>().Add(newClass);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted pharmacologic class
            return newClass;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing PharmacologicClassName or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="pharmacologicClassId">The parent PharmacologicClass ID.</param>
        /// <param name="nameValue">The text of the name.</param>
        /// <param name="nameUse">The use code ('L' for preferred, 'A' for alternate).</param>
        /// <returns>The existing or newly created PharmacologicClassName entity.</returns>
        /// <seealso cref="PharmacologicClassName"/>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<PharmacologicClassName> getOrCreatePharmacologicClassNameAsync(
            ApplicationDbContext dbContext,
            int? pharmacologicClassId,
            string? nameValue,
            string? nameUse)
        {
            #region implementation
            // Search for existing name with matching class ID, name value, and use code
            // Deduplication based on PharmacologicClassID, NameValue, and NameUse
            var existing = await dbContext.Set<PharmacologicClassName>().FirstOrDefaultAsync(pcn =>
                pcn.PharmacologicClassID == pharmacologicClassId &&
                pcn.NameValue == nameValue &&
                pcn.NameUse == nameUse);

            if (existing != null)
            {
                // Return existing pharmacologic class name if found
                return existing;
            }

            // Create new PharmacologicClassName entity with the provided name data
            var newName = new PharmacologicClassName
            {
                PharmacologicClassID = pharmacologicClassId,
                NameValue = nameValue,
                NameUse = nameUse // 'L' for preferred, 'A' for alternate
            };

            // Save the new pharmacologic class name to the database and persist changes immediately
            dbContext.Set<PharmacologicClassName>().Add(newName);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted pharmacologic class name
            return newName;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing PharmacologicClassLink or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="activeMoietySubstanceId">The ID of the active moiety IdentifiedSubstance.</param>
        /// <param name="pharmacologicClassId">The ID of the associated PharmacologicClass.</param>
        /// <returns>The existing or newly created PharmacologicClassLink entity.</returns>
        /// <seealso cref="PharmacologicClassLink"/>
        /// <seealso cref="IdentifiedSubstance"/>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<PharmacologicClassLink> getOrCreatePharmacologicClassLinkAsync(
            ApplicationDbContext dbContext,
            int? activeMoietySubstanceId,
            int? pharmacologicClassId)
        {
            #region implementation
            // Search for existing link between active moiety and pharmacologic class
            // Deduplication based on ActiveMoietySubstanceID and PharmacologicClassID
            var existing = await dbContext.Set<PharmacologicClassLink>().FirstOrDefaultAsync(pcl =>
                pcl.ActiveMoietySubstanceID == activeMoietySubstanceId &&
                pcl.PharmacologicClassID == pharmacologicClassId);

            if (existing != null)
            {
                // Return existing pharmacologic class link if found
                return existing;
            }

            // Create new PharmacologicClassLink entity connecting active moiety to pharmacologic class
            var newLink = new PharmacologicClassLink
            {
                ActiveMoietySubstanceID = activeMoietySubstanceId,
                PharmacologicClassID = pharmacologicClassId
            };

            // Save the new pharmacologic class link to the database and persist changes immediately
            dbContext.Set<PharmacologicClassLink>().Add(newLink);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted pharmacologic class link
            return newLink;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing PharmacologicClassHierarchy or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="childClassId">The ID of the child (more specific) class.</param>
        /// <param name="parentClassId">The ID of the parent (super-class).</param>
        /// <returns>The existing or newly created PharmacologicClassHierarchy entity.</returns>
        /// <seealso cref="PharmacologicClassHierarchy"/>
        /// <seealso cref="PharmacologicClass"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<PharmacologicClassHierarchy> getOrCreatePharmacologicClassHierarchyAsync(
            ApplicationDbContext dbContext,
            int? childClassId,
            int? parentClassId)
        {
            #region implementation
            // Search for existing hierarchy relationship between child and parent classes
            // Deduplication based on ChildPharmacologicClassID and ParentPharmacologicClassID
            var existing = await dbContext.Set<PharmacologicClassHierarchy>().FirstOrDefaultAsync(pch =>
                pch.ChildPharmacologicClassID == childClassId &&
                pch.ParentPharmacologicClassID == parentClassId);

            if (existing != null)
            {
                // Return existing pharmacologic class hierarchy if found
                return existing;
            }

            // Create new PharmacologicClassHierarchy entity establishing parent-child relationship
            var newHierarchy = new PharmacologicClassHierarchy
            {
                ChildPharmacologicClassID = childClassId,
                ParentPharmacologicClassID = parentClassId
            };

            // Save the new pharmacologic class hierarchy to the database and persist changes immediately
            dbContext.Set<PharmacologicClassHierarchy>().Add(newHierarchy);
            await dbContext.SaveChangesAsync();

            // Return the newly created and persisted pharmacologic class hierarchy
            return newHierarchy;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing BillingUnitIndex or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="sectionId">The ID of the parent Indexing Section.</param>
        /// <param name="packageNdc">The NDC Package Code being linked.</param>
        /// <param name="packageNdcSystem">The OID for the NDC system.</param>
        /// <param name="billingUnitCode">The NCPDP Billing Unit Code (GM, ML, or EA).</param>
        /// <param name="billingUnitSystem">The OID for the NCPDP Billing Unit system.</param>
        /// <returns>The existing or newly created BillingUnitIndex entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate billing unit index records.
        /// Uniqueness is determined by the combination of the SectionID and the Package NDC value.
        /// </remarks>
        /// <seealso cref="BillingUnitIndex"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<BillingUnitIndex> getOrCreateBillingUnitIndexAsync(
            ApplicationDbContext dbContext,
            int? sectionId,
            string? packageNdc,
            string? packageNdcSystem,
            string? billingUnitCode,
            string? billingUnitSystem)
        {
            #region implementation
            // Deduplicate based on the unique combination of SectionID and the NDC Package Code.
            var existing = await dbContext.Set<BillingUnitIndex>().FirstOrDefaultAsync(bui =>
                bui.SectionID == sectionId &&
                bui.PackageNDCValue == packageNdc);

            // If a record already exists, return it.
            if (existing != null)
            {
                return existing;
            }

            // Create a new BillingUnitIndex entity.
            var newIndex = new BillingUnitIndex
            {
                SectionID = sectionId,
                PackageNDCValue = packageNdc,
                PackageNDCSystemOID = packageNdcSystem,
                BillingUnitCode = billingUnitCode,
                BillingUnitCodeSystemOID = billingUnitSystem
            };

            // Save the new entity to the database.
            dbContext.Set<BillingUnitIndex>().Add(newIndex);
            await dbContext.SaveChangesAsync();

            return newIndex;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the subject of a Billing Unit Indexing section to create BillingUnitIndex records.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement (must be the '48779-3' indexing section).</param>
        /// <param name="section">The Section entity that has just been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of BillingUnitIndex records created.</returns>
        /// <remarks>
        /// This method implements the logic from SPL IG Section 12. It finds each NDC package code
        /// and its corresponding NCPDP billing unit characteristic within the section's subject.
        /// </remarks>
        /// <seealso cref="BillingUnitIndex"/>
        /// <seealso cref="getOrCreateBillingUnitIndexAsync"/>
        private async Task<int> parseAndSaveBillingUnitIndexAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            if (context?.ServiceProvider == null || context.Logger == null || !section.SectionID.HasValue)
            {
                return count;
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // The structure is a series of <manufacturedProduct> elements inside the <subject>.
            foreach (var manufacturedProductEl in sectionEl.SplElements(sc.E.Subject, sc.E.ManufacturedProduct, sc.E.ManufacturedProduct))
            {
                // 1. Extract the NDC Package Code
                var ndcCodeEl = manufacturedProductEl.SplElement(sc.E.AsContent, sc.E.ContainerPackagedProduct, sc.E.Code);
                var packageNdc = ndcCodeEl?.GetAttrVal(sc.A.CodeValue);
                var packageNdcSystem = ndcCodeEl?.GetAttrVal(sc.A.CodeSystem);

                if (string.IsNullOrWhiteSpace(packageNdc))
                {
                    context.Logger.LogWarning("Found a billing unit index entry without an NDC Package Code in SectionID {SectionID}.", section.SectionID);
                    continue;
                }

                // 2. Extract the Billing Unit from the characteristic
                string? billingUnitCode = null;
                string? billingUnitSystem = null;

                var characteristicEl = manufacturedProductEl.SplElement(sc.E.SubjectOf, sc.E.Characteristic);
                if (characteristicEl != null)
                {
                    // Check if the characteristic code is for NCPDP Billing Unit
                    var charCode = characteristicEl.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);
                    if (charCode == "NCPDPBILLINGUNIT")
                    {
                        var valueEl = characteristicEl.GetSplElement(sc.E.Value);
                        if (valueEl?.GetXsiType() == "CV" || valueEl?.GetXsiType() == "CE")
                        {
                            billingUnitCode = valueEl.GetAttrVal(sc.A.CodeValue);
                            billingUnitSystem = valueEl.GetAttrVal(sc.A.CodeSystem);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(billingUnitCode))
                {
                    context.Logger.LogWarning("Could not find a valid NCPDP Billing Unit for NDC {NDC} in SectionID {SectionID}.", packageNdc, section.SectionID);
                    continue;
                }

                // 3. Get or create the BillingUnitIndex record
                await getOrCreateBillingUnitIndexAsync(
                    dbContext,
                    section.SectionID,
                    packageNdc,
                    packageNdcSystem,
                    billingUnitCode,
                    billingUnitSystem
                );
                count++;
                context.Logger.LogInformation("Created BillingUnitIndex for NDC {NDC} -> {BU}", packageNdc, billingUnitCode);
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing ProductConcept by its unique concept code, or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="sectionId">The ID of the parent Indexing Section.</param>
        /// <param name="conceptCode">The MD5 hash code for the product concept.</param>
        /// <param name="conceptCodeSystem">The OID for the product concept code system.</param>
        /// <param name="conceptType">The type of concept ("Abstract" or "Application").</param>
        /// <param name="formCodeEl">The XElement for the [formCode], used only for Abstract concepts.</param>
        /// <returns>The existing or newly created ProductConcept entity.</returns>
        private async Task<ProductConcept> getOrCreateProductConceptAsync(
            ApplicationDbContext dbContext,
            int? sectionId,
            string? conceptCode,
            string? conceptCodeSystem,
            string? conceptType,
            XElement? formCodeEl)
        {
            #region implementation
            // Deduplicate based on the globally unique concept code.
            var existing = await dbContext.Set<ProductConcept>()
                .FirstOrDefaultAsync(pc => pc.ConceptCode == conceptCode);

            if (existing != null)
            {
                return existing;
            }

            var newConcept = new ProductConcept
            {
                SectionID = sectionId,
                ConceptCode = conceptCode,
                ConceptCodeSystem = conceptCodeSystem,
                ConceptType = conceptType,
                // Form code details are only applicable for Abstract concepts
                FormCode = (conceptType == "Abstract") ? formCodeEl?.GetAttrVal(sc.A.CodeValue) : null,
                FormCodeSystem = (conceptType == "Abstract") ? formCodeEl?.GetAttrVal(sc.A.CodeSystem) : null,
                FormDisplayName = (conceptType == "Abstract") ? formCodeEl?.GetAttrVal(sc.A.DisplayName) : null
            };

            dbContext.Set<ProductConcept>().Add(newConcept);
            await dbContext.SaveChangesAsync();
            return newConcept;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing ProductConceptEquivalence link or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="applicationConceptId">The ID of the Application ProductConcept.</param>
        /// <param name="abstractConceptId">The ID of the Abstract ProductConcept.</param>
        /// <param name="equivalenceCode">The code for the equivalence relationship (A, B, OTC, N).</param>
        /// <param name="equivalenceCodeSystem">The OID for the equivalence code system.</param>
        /// <returns>The existing or newly created ProductConceptEquivalence entity.</returns>
        private async Task<ProductConceptEquivalence> getOrCreateProductConceptEquivalenceAsync(
            ApplicationDbContext dbContext,
            int? applicationConceptId,
            int? abstractConceptId,
            string? equivalenceCode,
            string? equivalenceCodeSystem)
        {
            #region implementation
            var existing = await dbContext.Set<ProductConceptEquivalence>().FirstOrDefaultAsync(pce =>
                pce.ApplicationProductConceptID == applicationConceptId &&
                pce.AbstractProductConceptID == abstractConceptId);

            if (existing != null)
            {
                return existing;
            }

            var newEquivalence = new ProductConceptEquivalence
            {
                ApplicationProductConceptID = applicationConceptId,
                AbstractProductConceptID = abstractConceptId,
                EquivalenceCode = equivalenceCode,
                EquivalenceCodeSystem = equivalenceCodeSystem
            };

            dbContext.Set<ProductConceptEquivalence>().Add(newEquivalence);
            await dbContext.SaveChangesAsync();
            return newEquivalence;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the subject of a Product Concept Indexing section to create ProductConcept records.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement (must be the '48779-3' indexing section).</param>
        /// <param name="section">The Section entity that has just been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of all product concept related records created.</returns>
        /// <remarks>
        /// This method implements the logic from SPL IG Section 15. It handles both Abstract (15.2.2)
        /// and Application (15.2.6) product concepts, including their equivalence links.
        /// </remarks>
        /// <seealso cref="ProductConcept"/>
        /// <seealso cref="ProductConceptEquivalence"/>
        private async Task<int> parseAndSaveProductConceptsAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            if (context?.ServiceProvider == null || context.Logger == null || !section.SectionID.HasValue)
            {
                return count;
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // The structure is a series of <manufacturedProduct> elements inside the <subject>.
            foreach (var manufacturedProductEl in sectionEl.SplElements(sc.E.Subject, sc.E.ManufacturedProduct, sc.E.ManufacturedProduct))
            {
                // 1. Extract the main concept code
                var conceptCodeEl = manufacturedProductEl.GetSplElement(sc.E.Code);
                var conceptCode = conceptCodeEl?.GetAttrVal(sc.A.CodeValue);
                var conceptCodeSystem = conceptCodeEl?.GetAttrVal(sc.A.CodeSystem);

                if (string.IsNullOrWhiteSpace(conceptCode)) continue;

                // 2. Determine if it's an Abstract or Application concept
                var equivalentEntityEl = manufacturedProductEl.GetSplElement(sc.E.AsEquivalentEntity);
                bool isApplicationConcept = equivalentEntityEl != null;
                string conceptType = isApplicationConcept ? "Application" : "Abstract";

                // 3. Get or create the ProductConcept
                var formCodeEl = manufacturedProductEl.GetSplElement(sc.E.FormCode); // Only used for Abstract
                var productConcept = await getOrCreateProductConceptAsync(
                    dbContext,
                    section.SectionID,
                    conceptCode,
                    conceptCodeSystem,
                    conceptType,
                    formCodeEl
                );
                count++;
                context.Logger.LogInformation("Created ProductConcept: Type={type}, Code={code}", conceptType, conceptCode);

                // 4. If it's an Application concept, create the equivalence link
                if (isApplicationConcept && productConcept.ProductConceptID.HasValue && equivalentEntityEl != null)
                {
                    var equivalenceCodeEl = equivalentEntityEl.GetSplElement(sc.E.Code);
                    var equivalenceCode = equivalenceCodeEl?.GetAttrVal(sc.A.CodeValue);
                    var equivalenceCodeSystem = equivalenceCodeEl?.GetAttrVal(sc.A.CodeSystem);

                    var definingKindEl = equivalentEntityEl.GetSplElement(sc.E.DefiningMaterialKind);
                    var abstractConceptCode = definingKindEl?.GetSplElementAttrVal(sc.E.Code, sc.A.CodeValue);

                    if (!string.IsNullOrWhiteSpace(abstractConceptCode))
                    {
                        // Find the abstract concept it links to (it should already be in the DB from a previous loop or file)
                        var abstractConcept = await dbContext.Set<ProductConcept>()
                            .FirstOrDefaultAsync(pc => pc.ConceptCode == abstractConceptCode);

                        if (abstractConcept != null)
                        {
                            await getOrCreateProductConceptEquivalenceAsync(
                                dbContext,
                                productConcept.ProductConceptID,
                                abstractConcept.ProductConceptID,
                                equivalenceCode,
                                equivalenceCodeSystem
                            );
                            count++;
                            context.Logger.LogInformation("Created ProductConceptEquivalence link: App({app}) -> Abstract({abs})", productConcept.ProductConceptID, abstractConcept.ProductConceptID);
                        }
                        else
                        {
                            context.Logger.LogWarning("Application concept {appCode} referenced an abstract concept {absCode} that was not found in the database.", conceptCode, abstractConceptCode);
                        }
                    }
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses drug interaction issues from a section's subject.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement.</param>
        /// <param name="section">The Section entity that has just been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of all interaction-related records created.</returns>
        /// <remarks>
        /// This method implements the logic from SPL IG Section 32. It finds each [issue]
        /// and parses its contributing factors and consequences.
        /// </remarks>
        /// <seealso cref="InteractionIssue"/>
        /// <seealso cref="ContributingFactor"/>
        /// <seealso cref="InteractionConsequence"/>
        private async Task<int> parseAndSaveDrugInteractionsAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            if (context?.ServiceProvider == null || context.Logger == null || !section.SectionID.HasValue)
            {
                return count;
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // The structure is <substanceAdministration><subjectOf><issue>
            foreach (var issueEl in sectionEl.SplElements(sc.E.SubstanceAdministration, sc.E.SubjectOf, sc.E.Issue))
            {
                // 1. Create the main InteractionIssue record
                var interactionIssue = await getOrCreateInteractionIssueAsync(dbContext, section.SectionID, issueEl);
                count++;

                if (!interactionIssue.InteractionIssueID.HasValue) continue;

                // 2. Parse Contributing Factors
                var factorEl = issueEl.SplElement(sc.E.Subject, sc.E.SubstanceAdministrationCriterion, sc.E.Consumable, sc.E.AdministrableMaterial, sc.E.AdministrableMaterialKind, sc.E.Code);
                if (factorEl != null)
                {
                    var factorIdentifier = factorEl.GetAttrVal(sc.A.CodeValue);
                    var factorSystem = factorEl.GetAttrVal(sc.A.CodeSystem);

                    // The contributing factor is itself an IdentifiedSubstance. We need to find it.
                    // This assumes the Pharmacologic Class Indexing documents have already been processed.
                    var factorSubstance = await dbContext.Set<IdentifiedSubstance>().FirstOrDefaultAsync(i =>
                        i.SubstanceIdentifierValue == factorIdentifier &&
                        i.SubstanceIdentifierSystemOID == factorSystem);

                    if (factorSubstance != null)
                    {
                        await getOrCreateContributingFactorAsync(dbContext, interactionIssue.InteractionIssueID, factorSubstance.IdentifiedSubstanceID);
                        count++;
                    }
                    else
                    {
                        context.Logger.LogWarning("Could not find IdentifiedSubstance for contributing factor {factorId} in Section {sectionId}", factorIdentifier, section.SectionID);
                    }
                }

                // 3. Parse Consequences
                foreach (var consequenceEl in issueEl.SplElements(sc.E.Risk, sc.E.ConsequenceObservation))
                {
                    await getOrCreateInteractionConsequenceAsync(dbContext, interactionIssue.InteractionIssueID, consequenceEl);
                    count++;
                }
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing InteractionIssue or creates a new one for a given section.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="sectionId">The ID of the parent Section.</param>
        /// <param name="issueEl">The XElement for the [issue].</param>
        /// <returns>The existing or newly created InteractionIssue entity.</returns>
        private async Task<InteractionIssue> getOrCreateInteractionIssueAsync(
            ApplicationDbContext dbContext,
            int? sectionId,
            XElement issueEl)
        {
            #region implementation
            var codeEl = issueEl.GetSplElement(sc.E.Code);
            var interactionCode = codeEl?.GetAttrVal(sc.A.CodeValue);
            var interactionCodeSystem = codeEl?.GetAttrVal(sc.A.CodeSystem);
            var interactionDisplayName = codeEl?.GetAttrVal(sc.A.DisplayName);

            // An issue is unique per section. We can deduplicate based on this.
            var existing = await dbContext.Set<InteractionIssue>().FirstOrDefaultAsync(i =>
                i.SectionID == sectionId &&
                i.InteractionCode == interactionCode);

            if (existing != null)
            {
                return existing;
            }

            var newIssue = new InteractionIssue
            {
                SectionID = sectionId,
                InteractionCode = interactionCode,
                InteractionCodeSystem = interactionCodeSystem,
                InteractionDisplayName = interactionDisplayName
            };

            dbContext.Set<InteractionIssue>().Add(newIssue);
            await dbContext.SaveChangesAsync();
            return newIssue;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing ContributingFactor link or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="interactionIssueId">The ID of the parent InteractionIssue.</param>
        /// <param name="factorSubstanceId">The ID of the IdentifiedSubstance that is the factor.</param>
        /// <returns>The existing or newly created ContributingFactor entity.</returns>
        private async Task<ContributingFactor> getOrCreateContributingFactorAsync(
            ApplicationDbContext dbContext,
            int? interactionIssueId,
            int? factorSubstanceId)
        {
            #region implementation
            var existing = await dbContext.Set<ContributingFactor>().FirstOrDefaultAsync(cf =>
                cf.InteractionIssueID == interactionIssueId &&
                cf.FactorSubstanceID == factorSubstanceId);

            if (existing != null)
            {
                return existing;
            }

            var newFactor = new ContributingFactor
            {
                InteractionIssueID = interactionIssueId,
                FactorSubstanceID = factorSubstanceId
            };

            dbContext.Set<ContributingFactor>().Add(newFactor);
            await dbContext.SaveChangesAsync();
            return newFactor;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing InteractionConsequence or creates a new one.
        /// </summary>
        /// <param name="dbContext">The database context.</param>
        /// <param name="interactionIssueId">The ID of the parent InteractionIssue.</param>
        /// <param name="consequenceEl">The XElement for the [consequenceObservation].</param>
        /// <returns>The existing or newly created InteractionConsequence entity.</returns>
        private async Task<InteractionConsequence> getOrCreateInteractionConsequenceAsync(
            ApplicationDbContext dbContext,
            int? interactionIssueId,
            XElement consequenceEl)
        {
            #region implementation
            var typeCodeEl = consequenceEl.GetSplElement(sc.E.Code);
            var valueEl = consequenceEl.GetSplElement(sc.E.Value);

            var consequenceTypeCode = typeCodeEl?.GetAttrVal(sc.A.CodeValue);
            var consequenceValueCode = valueEl?.GetAttrVal(sc.A.CodeValue);

            // Deduplicate based on the issue and the specific consequence value code.
            var existing = await dbContext.Set<InteractionConsequence>().FirstOrDefaultAsync(ic =>
                ic.InteractionIssueID == interactionIssueId &&
                ic.ConsequenceValueCode == consequenceValueCode);

            if (existing != null)
            {
                return existing;
            }

            var newConsequence = new InteractionConsequence
            {
                InteractionIssueID = interactionIssueId,
                ConsequenceTypeCode = consequenceTypeCode,
                ConsequenceTypeCodeSystem = typeCodeEl?.GetAttrVal(sc.A.CodeSystem),
                ConsequenceTypeDisplayName = typeCodeEl?.GetAttrVal(sc.A.DisplayName),
                ConsequenceValueCode = consequenceValueCode,
                ConsequenceValueCodeSystem = valueEl?.GetAttrVal(sc.A.CodeSystem),
                ConsequenceValueDisplayName = valueEl?.GetAttrVal(sc.A.DisplayName)
            };

            dbContext.Set<InteractionConsequence>().Add(newConsequence);
            await dbContext.SaveChangesAsync();
            return newConsequence;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses the subject of a National Clinical Trials Indexing section to create NCTLink records.
        /// </summary>
        /// <param name="sectionEl">The parent [section] XElement.</param>
        /// <param name="section">The Section entity that has just been created.</param>
        /// <param name="context">The parsing context for repository and service access.</param>
        /// <returns>The count of NCTLink records created.</returns>
        /// <remarks>
        /// This method implements the logic from SPL IG Section 33.2.2. It finds each
        /// NCT number within the section's subject and creates a link record.
        /// </remarks>
        /// <seealso cref="NCTLink"/>
        /// <seealso cref="getOrCreateNCTLinkAsync"/>
        private async Task<int> parseAndSaveNCTLinksAsync(
            XElement sectionEl,
            Section section,
            SplParseContext context)
        {
            #region implementation
            int count = 0;
            if (context?.ServiceProvider == null || context.Logger == null || !section.SectionID.HasValue)
            {
                return count;
            }

            var dbContext = context.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // The structure is <subject2><substanceAdministration><componentOf><protocol><id>
            // The XElementExtensions helper `SplElements` is perfect for this deep navigation.
            foreach (var idEl in sectionEl.SplElements(sc.E.Subject2, sc.E.SubstanceAdministration, sc.E.ComponentOf, sc.E.Protocol, sc.E.Id))
            {
                // 1. Extract the NCT number and its root OID
                var nctNumber = idEl.GetAttrVal(sc.A.Extension);
                var nctRootOid = idEl.GetAttrVal(sc.A.Root);

                // 2. Validate the data according to the specification
                if (string.IsNullOrWhiteSpace(nctNumber) || nctRootOid != "2.16.840.1.113883.3.1077")
                {
                    context.Logger.LogWarning("Found an invalid or non-NCT protocol ID in Section {SectionID}. Skipping.", section.SectionID);
                    continue;
                }

                // Validate NCT number format: "NCT" + 8 digits
                if (!System.Text.RegularExpressions.Regex.IsMatch(nctNumber, @"^NCT\d{8}$"))
                {
                    context.Logger.LogWarning("NCT number '{NCT}' has an invalid format in Section {SectionID}. Skipping.", nctNumber, section.SectionID);
                    continue;
                }

                // 3. Get or create the NCTLink record
                await getOrCreateNCTLinkAsync(
                    dbContext,
                    section.SectionID,
                    nctNumber,
                    nctRootOid
                );
                count++;
                context.Logger.LogInformation("Created NCTLink for Section {SectionID} to NCT# {NCT}", section.SectionID, nctNumber);
            }

            return count;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets an existing NCTLink or creates and saves it if not found.
        /// </summary>
        /// <param name="dbContext">The database context for entity operations.</param>
        /// <param name="sectionId">The ID of the parent Indexing Section.</param>
        /// <param name="nctNumber">The National Clinical Trials number.</param>
        /// <param name="nctRootOid">The root OID for the NCT number system.</param>
        /// <returns>The existing or newly created NCTLink entity.</returns>
        /// <remarks>
        /// Implements a get-or-create pattern to prevent duplicate NCT links within a section.
        /// Uniqueness is determined by the combination of the SectionID and the NCTNumber.
        /// </remarks>
        /// <seealso cref="NCTLink"/>
        /// <seealso cref="Section"/>
        /// <seealso cref="ApplicationDbContext"/>
        private async Task<NCTLink> getOrCreateNCTLinkAsync(
            ApplicationDbContext dbContext,
            int? sectionId,
            string? nctNumber,
            string? nctRootOid)
        {
            #region implementation
            // Deduplicate based on the unique combination of SectionID and NCTNumber.
            var existing = await dbContext.Set<NCTLink>().FirstOrDefaultAsync(nctl =>
                nctl.SectionID == sectionId &&
                nctl.NCTNumber == nctNumber);

            // If a record already exists, return it.
            if (existing != null)
            {
                return existing;
            }

            // Create a new NCTLink entity.
            var newLink = new NCTLink
            {
                SectionID = sectionId,
                NCTNumber = nctNumber,
                NCTRootOID = nctRootOid
            };

            // Save the new entity to the database.
            dbContext.Set<NCTLink>().Add(newLink);
            await dbContext.SaveChangesAsync();

            return newLink;
            #endregion
        }
    }
    #endregion Private Helper Methods
}