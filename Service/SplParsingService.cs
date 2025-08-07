
using System.Xml.Linq;
using MedRecPro.Models;
using MedRecPro.Service.ParsingServices; // Import the new parsers
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants; // Constant class for SPL elements and attributes


namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Parses SPL (Structured Product Labeling) XML files and saves the extracted data to the database.
    /// Handles HL7 v3 XML format documents containing pharmaceutical product information.
    /// </summary>
    /// <remarks>
    /// This parser is designed to handle various SPL XML schema variations and extract key entities
    /// including documents, organizations, sections, products, and ingredients. It uses a modular
    /// parser architecture with specialized parsers for different XML elements, enabling flexible
    /// and maintainable processing of complex SPL document structures.
    /// </remarks>
    /// <seealso cref="ISplSectionParser"/>
    /// <seealso cref="SplParseContext"/>
    /// <seealso cref="SplFileImportResult"/>
    /// <seealso cref="DocumentSectionParser"/>
    /// <seealso cref="AuthorSectionParser"/>
    /// <seealso cref="StructuredBodySectionParser"/>
    public class SplXmlParser
    {
        #region implementation
        /// <summary>
        /// Service provider for creating scoped repositories and accessing dependency injection services.
        /// </summary>
        /// <seealso cref="IServiceProvider"/>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Logger instance for tracking parsing operations and error reporting.
        /// </summary>
        /// <seealso cref="ILogger{SplXmlParser}"/>
        private readonly ILogger<SplXmlParser> _logger;

        /// <summary>
        /// The default HL7 v3 XML namespace used for element parsing.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;

        /// <summary>
        /// Registry of specialized parsers mapped by XML element names for modular parsing.
        /// </summary>
        /// <seealso cref="ISplSectionParser"/>
        private readonly Dictionary<string, ISplSectionParser> _sectionParsers = new();

        // Add field to store main section parser reference
        private ISplSectionParser? _mainSectionParser;

        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SplXmlParser with dependency injection services.
        /// </summary>
        /// <param name="serviceProvider">Service provider for creating scoped repositories</param>
        /// <param name="logger">Logger instance for tracking parsing operations</param>
        /// <example>
        /// <code>
        /// var parser = new SplXmlParser(serviceProvider, logger);
        /// var result = await parser.ParseAndSaveSplDataAsync(xmlContent, "drugLabel.xml");
        /// </code>
        /// </example>
        /// <seealso cref="IServiceProvider"/>
        /// <seealso cref="ILogger{SplXmlParser}"/>
        public SplXmlParser(IServiceProvider serviceProvider, ILogger<SplXmlParser> logger)
        {
            #region implementation
            _serviceProvider = serviceProvider;
            _logger = logger;
            // Initialize the parser registry with default parsers
            registerDefaultParsers();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers a parser for a specific XML element name.
        /// </summary>
        /// <param name="elementName">The local name of the XML element (e.g., "author").</param>
        /// <param name="parser">An instance of a class implementing ISplSectionParser.</param>
        /// <example>
        /// <code>
        /// parser.RegisterSectionParser("customElement", new CustomElementParser());
        /// </code>
        /// </example>
        /// <remarks>
        /// This method allows for dynamic registration of specialized parsers for different
        /// XML element types. Element names are normalized to lowercase for case-insensitive
        /// matching during parsing operations.
        /// </remarks>
        /// <seealso cref="ISplSectionParser"/>
        public void RegisterSectionParser(string elementName, ISplSectionParser parser)
        {
            #region implementation
            // Normalize element name to lowercase for consistent lookup
            _sectionParsers[elementName.ToLower()] = parser;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Registers the default set of parsers for known SPL top-level elements.
        /// </summary>
        /// <remarks>
        /// This method sets up the core parser registry with specialized parsers for:
        /// - Document: Handles root document metadata
        /// - Author: Processes authoring organization information
        /// - StructuredBody: Orchestrates section parsing
        /// 
        /// Child parsers like SectionParser and ManufacturedProductParser are called by their parents,
        /// so they don't need to be in the top-level registry unless they can appear at the root.
        /// </remarks>
        /// <seealso cref="DocumentSectionParser"/>
        /// <seealso cref="AuthorSectionParser"/>
        /// <seealso cref="StructuredBodySectionParser"/>
        private void registerDefaultParsers()
        {
            #region implementation
            // Register core SPL element parsers
            RegisterSectionParser(sc.E.Document, new DocumentSectionParser());
            RegisterSectionParser(sc.E.Author, new AuthorSectionParser());

            // Create the specialized parsers needed for SectionParser
            var contentParser = new SectionContentParser();
            var indexingParser = new SectionIndexingParser();
            var hierarchyParser = new SectionHierarchyParser();
            var mediaParser = new SectionMediaParser();
            var toleranceParser = new ToleranceSpecificationParser();

            // Create the main SectionParser with its dependencies
            var sectionParser = new SectionParser(contentParser, indexingParser, hierarchyParser, mediaParser, toleranceParser);

            // Register the section parser
            RegisterSectionParser(sc.E.Section, sectionParser);

            // Create the StructuredBodySectionParser with SectionParser dependency
            var structuredBodyParser = new StructuredBodySectionParser(sectionParser);
            RegisterSectionParser(sc.E.StructuredBody, structuredBodyParser);

            // Store reference to main section parser for context usage
            _mainSectionParser = sectionParser;

            // Child parsers like SectionParser and ManufacturedProductParser are called by their parents,
            // so they don't need to be in the top-level registry unless they can appear at the root.
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses SPL XML content by orchestrating specialized parsers and saves all extracted entities to the database.
        /// </summary>
        /// <param name="xmlContent">Raw XML content string to parse</param>
        /// <param name="fileNameInZip">Name of the file within the ZIP archive for logging</param>
        /// <param name="reportProgress">Delegate for progress reporting</param>
        /// <returns>Import result containing success status, counts, and any errors encountered</returns>
        /// <example>
        /// <code>
        /// var result = await parser.ParseAndSaveSplDataAsync(xmlContent, "drugLabel.xml");
        /// if (result.Success) {
        ///     Console.WriteLine($"Created {result.ProductsCreated} products");
        ///     Console.WriteLine($"Created {result.SectionsCreated} sections");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method performs the following orchestration workflow:
        /// 1. Parses and validates the XML document structure
        /// 2. Creates a scoped parsing context for transaction management
        /// 3. Processes the root document element to establish context
        /// 4. Parses author information and organization relationships
        /// 5. Processes the structured body and its contained sections
        /// 6. Aggregates results from all specialized parsers
        /// 
        /// The method uses database transactions through scoped service provider
        /// and maintains parsing context throughout the process for proper entity relationships.
        /// </remarks>
        /// <seealso cref="SplFileImportResult"/>
        /// <seealso cref="SplParseContext"/>
        /// <seealso cref="XDocument"/>
        public async Task<SplFileImportResult> ParseAndSaveSplDataAsync(string xmlContent, 
            string fileNameInZip, 
            Action<string>? reportProgress = null)
        {
            #region implementation
            var fileResult = new SplFileImportResult { FileName = fileNameInZip };
            XDocument xdoc;

            // Parse XML document with comprehensive error handling
            try
            {
                xdoc = XDocument.Parse(xmlContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse XML for file {FileName}", fileNameInZip);
                fileResult.Success = false;
                fileResult.Message = "XML parsing error.";
                fileResult.Errors.Add(ex.Message);
                return fileResult;
            }

            // Validate root document element structure
            var docEl = xdoc.Root;
            if (docEl == null || docEl.Name.LocalName != "document")
            {
                fileResult.Success = false;
                fileResult.Message = "XML root element is not <document>.";
                fileResult.Errors.Add("Invalid SPL XML structure.");
                return fileResult;
            }

            // Use a single scope for the entire file to process it as a single transaction.
            using var scope = _serviceProvider.CreateScope();
            var context = new SplParseContext
            {
                ServiceProvider = scope.ServiceProvider,
                Logger = _logger,
                FileResult = fileResult,
                FileNameInZip = fileNameInZip,
                MainSectionParser = _mainSectionParser,
                DocumentElement = docEl
            };

            try
            {
                // --- Orchestration Logic ---

                reportProgress?.Invoke($"Starting file {fileNameInZip}...");

                // Step 1: Parse the root <document> element to establish parsing context
                if (_sectionParsers.TryGetValue("document", out var documentParser))
                {
                    reportProgress?.Invoke("Parsing <document>...");

                    var docParseResult = await documentParser.ParseAsync(docEl, context, reportProgress);
                    context.UpdateFileResult(docParseResult);

                    // If the root document fails to parse, we cannot continue processing
                    if (!docParseResult.Success)
                    {
                        throw new InvalidOperationException("Failed to parse the main document element. Aborting file processing.");
                    }
                }
                else
                {
                    throw new NotSupportedException("No parser registered for the root 'document' element.");
                }

                // Step 2: Parse the <author> element to extract organization information
                var authorEl = docEl.Element(ns + sc.E.Author);
                if (authorEl != null
                    && _sectionParsers.TryGetValue(sc.E.Author, out var authorParser))
                {
                    reportProgress?.Invoke("Parsing <author>...");

                    var authorParseResult = await authorParser.ParseAsync(authorEl, context, reportProgress);
                    context.UpdateFileResult(authorParseResult);
                }

                // Step 3: Parse the <structuredBody> element and its contained sections
                var structuredBodyEl = docEl.Element(ns + sc.E.Component)?.Element(ns + sc.E.StructuredBody);
                if (structuredBodyEl != null
                    && _sectionParsers.TryGetValue(sc.E.StructuredBody.ToLower(), out var structuredBodyParser))
                {
                    reportProgress?.Invoke("Parsing <structuredBody>...");

                    var sbParseResult = await structuredBodyParser.ParseAsync(structuredBodyEl, context, reportProgress);
                    context.UpdateFileResult(sbParseResult);
                }

                // You can add calls to other top-level element parsers here as needed.

                // Determine final success status based on error accumulation
                fileResult.Success = fileResult.Errors.Count == 0;
                fileResult.Message = fileResult.Success ? "Imported successfully." : "Imported with errors.";
            }
            catch (Exception ex)
            {
                // Handle critical errors that prevent continued processing
                _logger.LogError(ex, "A critical error occurred during SPL data processing for file {FileName}", fileNameInZip);
                fileResult.Success = false;
                fileResult.Message = "A critical error occurred during processing.";
                fileResult.Errors.Add($"Critical error: {ex.Message}");
            }

            return fileResult;
            #endregion
        }

        #region old
        ///**************************************************************/
        ///// <summary>
        ///// Parses SPL XML content and saves all extracted entities to the database.
        ///// </summary>
        ///// <param name="xmlContent">Raw XML content string to parse</param>
        ///// <param name="fileNameInZip">Name of the file within the ZIP archive for logging</param>
        ///// <returns>Import result containing success status, counts, and any errors encountered</returns>
        ///// <example>
        ///// var result = await parser.ParseAndSaveSplDataAsync(xmlContent, "drugLabel.xml");
        ///// if (result.Success) {
        /////     Console.WriteLine($"Created {result.ProductsCreated} products");
        ///// }
        ///// </example>
        ///// <remarks>
        ///// Processes the document in order: Document metadata, Author organizations, 
        ///// Structured body sections, and embedded products with ingredients.
        ///// Uses database transactions through scoped service provider.
        ///// </remarks>
        //public async Task<SplFileImportResult> ParseAndSaveSplDataAsync_original(string xmlContent, string fileNameInZip)
        //{
        //    #region implementation
        //    var fileResult = new SplFileImportResult { FileName = fileNameInZip };
        //    XDocument xdoc;

        //    // Parse XML document with error handling
        //    try
        //    {
        //        xdoc = XDocument.Parse(xmlContent);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to parse XML for file {FileName}", fileNameInZip);
        //        fileResult.Success = false;
        //        fileResult.Message = "XML parsing error.";
        //        fileResult.Errors.Add(ex.Message);
        //        return fileResult;
        //    }

        //    // Validate root document element
        //    var docElement = xdoc.Root;
        //    if (docElement == null || docElement.Name.LocalName != "document")
        //    {
        //        fileResult.Success = false;
        //        fileResult.Message = "XML root element is not <document>.";
        //        fileResult.Errors.Add("Invalid SPL XML structure.");
        //        return fileResult;
        //    }

        //    using var scope = _serviceProvider.CreateScope();
        //    var sp = scope.ServiceProvider;

        //    try
        //    {
        //        // 1. Parse and Save Label.Document
        //        var labelDocument = parseDocumentElement(docElement);

        //        if (labelDocument == null)
        //        {
        //            fileResult.Errors.Add("Could not parse main document metadata.");
        //            // Decide if this is a fatal error for the file
        //        }
        //        else
        //        {
        //            var docRepo = getRepository<Label.Document>(sp);

        //            await docRepo.CreateAsync(labelDocument); // PK labelDocument.DocumentID is now populated
        //            fileResult.DocumentsCreated++;
        //            _logger.LogInformation("Created Document with ID {DocumentID} for file {FileName}", labelDocument.DocumentID, fileNameInZip);

        //            // 2. Parse and Save Author Organization and DocumentAuthor link
        //            var authorOrgElement = docElement.Element(ns + "author")?.Element(ns + "assignedEntity")?.Element(ns + "representedOrganization");
        //            if (authorOrgElement != null)
        //            {
        //                var organization = parseOrganizationElement(authorOrgElement, "Author");

        //                if (organization != null)
        //                {
        //                    var orgRepo = getRepository<Label.Organization>(sp);

        //                    await orgRepo.CreateAsync(organization); // PK organization.OrganizationID populated
        //                    fileResult.OrganizationsCreated++;

        //                    _logger.LogInformation("Created Organization (Author) with ID {OrganizationID}", organization.OrganizationID);

        //                    // Create document-author relationship
        //                    if (labelDocument.DocumentID.HasValue && organization.OrganizationID.HasValue)
        //                    {
        //                        var docAuthor = new Label.DocumentAuthor
        //                        {
        //                            DocumentID = labelDocument.DocumentID.Value,
        //                            OrganizationID = organization.OrganizationID.Value,
        //                            AuthorType = "Labeler" // Default or determine from XML if possible
        //                        };
        //                        var docAuthorRepo = getRepository<Label.DocumentAuthor>(sp);

        //                        await docAuthorRepo.CreateAsync(docAuthor);
        //                        _logger.LogInformation("Created DocumentAuthor link for DocumentID {DocumentID} and OrganizationID {OrganizationID}",
        //                            labelDocument.DocumentID.Value, organization.OrganizationID.Value);
        //                    }
        //                }
        //            }

        //            // 3. Parse Structured Body and Sections
        //            var structuredBodyEl = docElement.Element(ns + "component")?.Element(ns + "structuredBody");

        //            if (structuredBodyEl != null && labelDocument.DocumentID.HasValue)
        //            {
        //                var labelStructuredBody = new Label.StructuredBody { DocumentID = labelDocument.DocumentID.Value };

        //                var sbRepo = getRepository<Label.StructuredBody>(sp);

        //                await sbRepo.CreateAsync(labelStructuredBody); // PK labelStructuredBody.StructuredBodyID populated

        //                _logger.LogInformation("Created StructuredBody with ID {StructuredBodyID} for DocumentID {DocumentID}",
        //                    labelStructuredBody.StructuredBodyID, labelDocument.DocumentID.Value);

        //                // Process all section components
        //                foreach (var sectionCompEl in structuredBodyEl.Elements(ns + "component"))
        //                {
        //                    var sectionEl = sectionCompEl.Element(ns + "section");

        //                    if (sectionEl != null && labelStructuredBody.StructuredBodyID.HasValue)
        //                    {
        //                        await parseAndSaveSectionAsync(sectionEl, labelStructuredBody.StructuredBodyID.Value, null, sp, fileResult);
        //                    }
        //                }
        //            }

        //            // Example: Parse top-level subject's manufacturedProduct (common in drug listings)
        //            var subjectManufacturedProductEl = docElement.Element(ns + "component")?
        //                .Element(ns + "structuredBody")?
        //                .Elements(ns + "component")
        //                .Select(c => c.Element(ns + "section"))
        //                .FirstOrDefault(s => s != null)?
        //                .Element(ns + "subject")?
        //                .Element(ns + "manufacturedProduct");

        //            // Or sometimes it's directly under a section, or even a section subject
        //            // This part of SPL is very variable. For the given dd9bfbf7...xml, it's under a section.
        //            // For A0067DCB...xml, it's under section/subject/manufacturedProduct
        //            // For db6ed7ab...xml, it might be different as well.
        //            // We'll assume a common pattern of it being under a section's subject for now.

        //            // Placeholder for finding and parsing manufactured products - This is complex
        //            // You would iterate sections, check for <subject><manufacturedProduct>
        //            // Or find <manufacturedProduct> elements wherever they might appear based on SPL schema variations.
        //            // For brevity, a direct parse of first found product is shown, but a loop is needed.

        //            // Find first section containing a manufactured product
        //            var firstSectionWithProduct = structuredBodyEl?.Elements(ns + "component")
        //                .Select(c => c.Element(ns + "section"))
        //                .FirstOrDefault(s => s?.Element(ns + "subject")?.Element(ns + "manufacturedProduct") != null);

        //            if (firstSectionWithProduct != null)
        //            {
        //                var productEl = firstSectionWithProduct
        //                    ?.Element(ns + "subject")
        //                    ?.Element(ns + "manufacturedProduct")!;

        //                var sectionIdForProduct = getSectionIdFromElement(firstSectionWithProduct, sp, fileResult); // TODO: Refine Helper method

        //                if (productEl != null && sectionIdForProduct.HasValue)
        //                {
        //                    await parseAndSaveManufacturedProductAsync(productEl, sectionIdForProduct, sp, fileResult);
        //                }
        //            }
        //        }

        //        // Set final result status based on error count
        //        fileResult.Success = fileResult.Errors.Count == 0;
        //        fileResult.Message = fileResult.Success ? "Imported successfully." : "Imported with errors.";
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error saving SPL data for file {FileName}", fileNameInZip);
        //        fileResult.Success = false;
        //        fileResult.Message = "Database save error.";
        //        fileResult.Errors.Add($"Database save error: {ex.Message}");
        //    }
        //    return fileResult;
        //    #endregion
        //}

        ///**************************************************************/
        ///// <summary>
        ///// Recursively parses and saves a section element and all its child sections.
        ///// </summary>
        ///// <param name="sectionEl">The section XML element to parse</param>
        ///// <param name="structuredBodyId">ID of the parent structured body (for top-level sections)</param>
        ///// <param name="parentSectionId">ID of the parent section (for nested sections)</param>
        ///// <param name="sp">Service provider for repository access</param>
        ///// <param name="fileResult">Import result to track progress and errors</param>
        ///// <remarks>
        ///// Handles both top-level sections (linked to structured body) and nested child sections.
        ///// Also processes any manufactured products found as subjects within the section.
        ///// </remarks>
        //private async Task parseAndSaveSectionAsync(XElement sectionEl, int? structuredBodyId, int? parentSectionId, IServiceProvider sp, SplFileImportResult fileResult)
        //{            
        //    #region implementation
        //    var labelSection = parseSectionElement(sectionEl, structuredBodyId, parentSectionId);

        //    if (labelSection == null)
        //    {
        //        fileResult.Errors.Add($"Could not parse section element: {sectionEl.Element(ns + "title")?.Value ?? "Untitled Section"}");
        //        return;
        //    }

        //    var sectionRepo = getRepository<Label.Section>(sp);

        //    await sectionRepo.CreateAsync(labelSection); // PK labelSection.SectionID populated

        //    fileResult.SectionsCreated++;

        //    _logger.LogInformation("Created Section {SectionTitle} with ID {SectionID}", labelSection.Title, labelSection.SectionID);

        //    // Parse and save child sections recursively
        //    foreach (var childSectionCompEl in sectionEl.Elements(ns + "component"))
        //    {
        //        var childSectionEl = childSectionCompEl.Element(ns + "section");

        //        if (childSectionEl != null && labelSection.SectionID.HasValue)
        //        {
        //            // Create SectionHierarchy entry
        //            var sectionHierarchy = new Label.SectionHierarchy
        //            {
        //                ParentSectionID = labelSection.SectionID.Value,
        //                // ChildSectionID will be set after the child is created
        //            };

        //            // Recursively parse the child section
        //            // The ChildSectionID in sectionHierarchy needs to be updated after child save.
        //            // This requires careful handling, perhaps saving hierarchy after child.
        //            // For simplicity here, we're not fully implementing SectionHierarchy
        //            await parseAndSaveSectionAsync(childSectionEl, null, labelSection.SectionID.Value, sp, fileResult);
        //        }
        //    }

        //    // Parse manufacturedProduct if it's a subject of this section
        //    var subjectManufacturedProductEl = sectionEl?.Element(ns + "subject")?.Element(ns + "manufacturedProduct")!;

        //    if (subjectManufacturedProductEl != null
        //        && labelSection.SectionID.HasValue)
        //    {
        //        await parseAndSaveManufacturedProductAsync(subjectManufacturedProductEl, labelSection.SectionID, sp, fileResult);
        //    }

        //    // TODO: Parse SectionTextContent, TextList, TextTable, ObservationMedia, RenderedMedia etc.
        //    #endregion
        //}

        ///**************************************************************/
        ///// <summary>
        ///// Parses and saves a manufactured product element along with its ingredients.
        ///// </summary>
        ///// <param name="productEl">The manufacturedProduct XML element</param>
        ///// <param name="sectionId">ID of the section containing this product</param>
        ///// <param name="sp">Service provider for repository access</param>
        ///// <param name="fileResult">Import result to track progress and errors</param>
        ///// <remarks>
        ///// Handles SPL variations where the product may be nested within manufacturedMedicine 
        ///// or be the manufacturedProduct element itself. Processes all ingredient types.
        ///// </remarks>
        //private async Task parseAndSaveManufacturedProductAsync(XElement productEl, int? sectionId, IServiceProvider sp, SplFileImportResult fileResult)
        //{           
        //    #region implementation
        //    var manufacturedMedicineEl = productEl.Element(ns + "manufacturedMedicine") ?? productEl.Element(ns + "manufacturedProduct"); // SPL variations

        //    if (manufacturedMedicineEl == null)
        //    {
        //        manufacturedMedicineEl = productEl; // Sometimes productEl is already manufacturedMedicine

        //        if (manufacturedMedicineEl.Name.LocalName != "manufacturedMedicine" && manufacturedMedicineEl.Name.LocalName != "manufacturedProduct")
        //        {
        //            fileResult.Errors.Add("Could not find <manufacturedMedicine> or <manufacturedProduct> element for product parsing.");
        //            return;
        //        }
        //    }

        //    // Create product entity with basic information
        //    var labelProduct = new Label.Product
        //    {
        //        SectionID = sectionId,
        //        ProductName = manufacturedMedicineEl.Element(ns + "name")?.Value,
        //        FormCode = manufacturedMedicineEl.Element(ns + "formCode")?.Attribute("code")?.Value,
        //        FormCodeSystem = manufacturedMedicineEl.Element(ns + "formCode")?.Attribute("codeSystem")?.Value,
        //        FormDisplayName = manufacturedMedicineEl.Element(ns + "formCode")?.Attribute("displayName")?.Value
        //    };

        //    var productRepo = getRepository<Label.Product>(sp);

        //    await productRepo.CreateAsync(labelProduct);

        //    fileResult.ProductsCreated++;
        //    _logger.LogInformation("Created Product {ProductName} with ID {ProductID}", labelProduct.ProductName, labelProduct.ProductID);

        //    // Parse all ingredient types
        //    foreach (var ingredientEl in manufacturedMedicineEl.Elements(ns + "ingredient")) // Or productEl.Elements for other SPLs
        //    {
        //        await parseAndSaveIngredientAsync(ingredientEl, labelProduct.ProductID, sp, fileResult);
        //    }
        //    foreach (var ingredientEl in manufacturedMedicineEl.Elements(ns + "activeIngredient")) // Common in older SPLs
        //    {
        //        await parseAndSaveIngredientAsync(ingredientEl, labelProduct.ProductID, sp, fileResult, isExplicitlyActive: true);
        //    }
        //    foreach (var ingredientEl in manufacturedMedicineEl.Elements(ns + "inactiveIngredient"))
        //    {
        //        await parseAndSaveIngredientAsync(ingredientEl, labelProduct.ProductID, sp, fileResult, isExplicitlyInactive: true);
        //    }

        //    // TODO: Parse ProductIdentifier, GenericMedicine, SpecializedKind, EquivalentEntity, PackagingLevel, MarketingCategory, Characteristic etc.
        //    #endregion
        //}

        ///**************************************************************/
        ///// <summary>
        ///// Parses and saves an ingredient element including its substance and active moiety information.
        ///// </summary>
        ///// <param name="ingredientEl">The ingredient XML element to parse</param>
        ///// <param name="productId">ID of the product this ingredient belongs to</param>
        ///// <param name="sp">Service provider for repository access</param>
        ///// <param name="fileResult">Import result to track progress and errors</param>
        ///// <param name="isExplicitlyActive">Whether this ingredient is explicitly marked as active</param>
        ///// <param name="isExplicitlyInactive">Whether this ingredient is explicitly marked as inactive</param>
        ///// <remarks>
        ///// Handles quantity information with numerator/denominator units and creates
        ///// associated active moiety records when present.
        ///// </remarks>
        //private async Task parseAndSaveIngredientAsync(XElement ingredientEl, int? productId, IServiceProvider sp, SplFileImportResult fileResult, bool isExplicitlyActive = false, bool isExplicitlyInactive = false)
        //{
        //    #region implementation
        //    if (!productId.HasValue) return;

        //    // Find ingredient substance element (various naming patterns)
        //    var ingredientSubstanceEl = ingredientEl?.Element(ns + "ingredientSubstance")
        //        ?? ingredientEl?.Element(ns + "activeIngredientSubstance")
        //        ?? ingredientEl?.Element(ns + "inactiveIngredientSubstance");

        //    if (ingredientSubstanceEl == null)
        //    {
        //        fileResult.Errors.Add($"Could not parse ingredient substance for ProductID {productId}.");
        //        return;
        //    }

        //    // Create ingredient substance record
        //    var labelIngSubstance = new Label.IngredientSubstance
        //    {
        //        UNII = ingredientSubstanceEl.Element(ns + "code")
        //        ?.Attribute("code")
        //        ?.Value,

        //        SubstanceName = ingredientSubstanceEl.Element(ns + "name")?.Value
        //    };
        //    var ingSubRepo = getRepository<Label.IngredientSubstance>(sp);

        //    await ingSubRepo.CreateAsync(labelIngSubstance); // PK labelIngSubstance.IngredientSubstanceID populated

        //    _logger.LogInformation("Created IngredientSubstance {SubstanceName} with ID {IngredientSubstanceID}", labelIngSubstance.SubstanceName, labelIngSubstance.IngredientSubstanceID);

        //    // Create ingredient record linking product to substance
        //    var labelIngredient = new Label.Ingredient
        //    {
        //        ProductID = productId,
        //        IngredientSubstanceID = labelIngSubstance.IngredientSubstanceID,
        //        ClassCode = ingredientEl?.Attribute("classCode")?.Value
        //    };

        //    // Set default class codes for explicitly active/inactive ingredients
        //    if (isExplicitlyActive) labelIngredient.ClassCode = labelIngredient.ClassCode ?? "ACTIB"; // Active Ingredient Base

        //    if (isExplicitlyInactive) labelIngredient.ClassCode = labelIngredient.ClassCode ?? "IACT"; // Inactive

        //    // Parse quantity information (numerator/denominator pattern)
        //    var quantityEl = ingredientEl?.Element(ns + "quantity");

        //    if (quantityEl != null)
        //    {
        //        var numeratorEl = quantityEl.Element(ns + "numerator");
        //        var denominatorEl = quantityEl.Element(ns + "denominator");

        //        if (numeratorEl != null && numeratorEl?.Attribute("value") != null)
        //            labelIngredient.QuantityNumerator = parseNullableDecimal(numeratorEl?.Attribute("value")?.Value!);

        //        labelIngredient.QuantityNumeratorUnit = numeratorEl?.Attribute("unit")?.Value;

        //        if (denominatorEl != null && denominatorEl?.Attribute("value") != null)
        //            labelIngredient.QuantityDenominator = parseNullableDecimal(denominatorEl?.Attribute("value")?.Value!);

        //        labelIngredient.QuantityDenominatorUnit = denominatorEl?.Attribute("unit")?.Value;
        //    }

        //    var ingredientRepo = getRepository<Label.Ingredient>(sp);

        //    await ingredientRepo.CreateAsync(labelIngredient);

        //    _logger.LogInformation("Created Ingredient for SubstanceID {IngredientSubstanceID} linked to ProductID {ProductID}", labelIngredient.IngredientSubstanceID, productId);

        //    // Parse Active Moiety information
        //    var activeMoietyContainerEl = ingredientSubstanceEl.Element(ns + "activeMoiety"); // could be plural
        //    if (activeMoietyContainerEl != null)
        //    {
        //        foreach (var activeMoietyEl in activeMoietyContainerEl.Elements(ns + "activeMoiety")) // Handle nested activeMoiety
        //        {
        //            if (labelIngSubstance.IngredientSubstanceID.HasValue)
        //            {
        //                var labelActiveMoiety = new Label.ActiveMoiety
        //                {
        //                    IngredientSubstanceID = labelIngSubstance.IngredientSubstanceID.Value,
        //                    MoietyUNII = activeMoietyEl.Element(ns + "code")?.Attribute("code")?.Value,
        //                    MoietyName = activeMoietyEl.Element(ns + "name")?.Value
        //                };
        //                var amRepo = getRepository<Label.ActiveMoiety>(sp);
        //                await amRepo.CreateAsync(labelActiveMoiety);
        //                _logger.LogInformation("Created ActiveMoiety {MoietyName} for IngredientSubstanceID {IngredientSubstanceID}", labelActiveMoiety.MoietyName, labelIngSubstance.IngredientSubstanceID.Value);
        //            }
        //        }
        //    }
        //    #endregion
        //}

        ///**************************************************************/
        ///// <summary>
        ///// Parses the main document element extracting metadata and identifiers.
        ///// </summary>
        ///// <param name="docElement">The document root XML element</param>
        ///// <returns>Label.Document entity or null if parsing fails</returns>
        ///// <remarks>
        ///// Extracts document GUID, codes, title, effective time, set ID, and version information.
        ///// </remarks>
        //private Label.Document? parseDocumentElement(XElement docElement)
        //{

        //    #region implementation
        //    try
        //    {
        //        return new Label.Document
        //        {
        //            DocumentGUID = parseNullableGuid(docElement.Element(ns + "id")?.Attribute("root")?.Value!),

        //            DocumentCode = docElement.Element(ns + "code")?.Attribute("code")?.Value,

        //            DocumentCodeSystem = docElement.Element(ns + "code")?.Attribute("codeSystem")?.Value,

        //            DocumentDisplayName = docElement.Element(ns + "code")?.Attribute("displayName")?.Value,

        //            Title = docElement.Element(ns + "title")?.Value.Trim(),

        //            EffectiveTime = parseNullableDateTime(docElement.Element(ns + "effectiveTime")?.Attribute("value")?.Value!),

        //            SetGUID = parseNullableGuid(docElement.Element(ns + "setId")?.Attribute("root")?.Value!),

        //            VersionNumber = parseNullableInt(docElement.Element(ns + "versionNumber")?.Attribute("value")?.Value!),
        //            // SubmissionFileName might not be in the XML itself, but rather the name of the file in the ZIP.
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error parsing <document> element attributes.");
        //        return null;
        //    }
        //    #endregion
        //}

        ///**************************************************************/
        ///// <summary>
        ///// Parses an organization element extracting name and confidentiality information.
        ///// </summary>
        ///// <param name="orgElement">The organization XML element to parse</param>
        ///// <param name="typeHint">Type hint for logging context (e.g., "Author")</param>
        ///// <returns>Label.Organization entity or null if parsing fails</returns>
        //private Label.Organization? parseOrganizationElement(XElement orgElement, string typeHint)
        //{

        //    #region implementation
        //    try
        //    {
        //        return new Label.Organization
        //        {
        //            OrganizationName = orgElement.Element(ns + "name")?.Value.Trim(),

        //            IsConfidential = parseNullableBool(orgElement.Element(ns + "confidentialityCode")?.Attribute("code")?.Value == "B")
        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error parsing <organization> element ({TypeHint}).", typeHint);
        //        return null;
        //    }
        //    #endregion
        //}

        ///**************************************************************/
        ///// <summary>
        ///// Parses a section element extracting metadata and hierarchy information.
        ///// </summary>
        ///// <param name="sectionEl">The section XML element to parse</param>
        ///// <param name="structuredBodyId">ID of parent structured body (for top-level sections)</param>
        ///// <param name="parentSectionId">ID of parent section (for nested sections)</param>
        ///// <returns>Label.Section entity or null if parsing fails</returns>
        //private Label.Section? parseSectionElement(XElement sectionEl, int? structuredBodyId, int? parentSectionId)
        //{        
        //    #region implementation
        //    try
        //    {
        //        var section = new Label.Section
        //        {
        //            // Only for top-level sections. ParentSectionID would
        //            // be set if this is a child, handled by SectionHierarchy
        //            StructuredBodyID = structuredBodyId ?? null, 

        //            SectionGUID = parseNullableGuid(sectionEl.Element(ns + "id")?.Attribute("root")?.Value!) ?? Guid.Empty,

        //            SectionCode = sectionEl.Element(ns + "code")?.Attribute("code")?.Value ?? string.Empty,

        //            SectionCodeSystem = sectionEl.Element(ns + "code")?.Attribute("codeSystem")?.Value ?? string.Empty,

        //            SectionDisplayName = sectionEl.Element(ns + "code")?.Attribute("displayName")?.Value ?? string.Empty,

        //            Title = sectionEl.Element(ns + "title")?.Value.Trim() ?? string.Empty,

        //            EffectiveTime = parseNullableDateTime(sectionEl.Element(ns + "effectiveTime")?.Attribute("value")?.Value!) ?? DateTime.MinValue
        //        };
        //        return section;
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error parsing <section> element.");
        //        return null;
        //    }
        //    #endregion
        //}

        ///**************************************************************/
        ///// <summary>
        ///// Attempts to retrieve the database ID for a section element.
        ///// </summary>
        ///// <param name="sectionEl">The section XML element</param>
        ///// <param name="sp">Service provider for potential database queries</param>
        ///// <param name="fileResult">Import result to record errors</param>
        ///// <returns>Section ID if found, otherwise null</returns>
        ///// <remarks>
        ///// INCOMPLETE IMPLEMENTATION: This method currently always returns null and logs an error.
        ///// A complete implementation would need to query existing sections by GUID or maintain
        ///// a mapping of parsed sections to their database IDs.
        ///// </remarks>
        //private int? getSectionIdFromElement(XElement sectionEl, IServiceProvider sp, SplFileImportResult fileResult)
        //{

        //    #region implementation
        //    // This is tricky. If the section was just saved, its ID would be on the entity.
        //    // If it's a reference to an *existing* section by GUID, you'd need to query.
        //    // For simplicity, this assumes the section might have an ID attribute that we could use,
        //    // or relies on a prior save. The current ParseAndSaveSectionAsync populates the ID on the entity.
        //    // This helper needs to be used in a context where the section's DB ID is known.
        //    var sectionGuid = parseNullableGuid(sectionEl.Element(ns + "id")?.Attribute("root")?.Value);

        //    if (sectionGuid.HasValue)
        //    {
        //        // In a real scenario, you might need to query the DB if this section was saved in a different pass
        //        // For now, we'll assume this is insufficient to get a DB ID without prior context.
        //        // This highlights a complexity in mapping deeply nested or referenced structures.
        //        // The current structure implies ParseAndSaveSectionAsync is called, which *gets* the ID.
        //        // This helper is more for when a Product refers to a Section that *should* already exist or be parseable.
        //        _logger.LogWarning("GetSectionIdFromElement by GUID is not fully implemented for querying existing sections. Product may not be linked correctly if section not parsed in same flow.");
        //    }

        //    fileResult.Errors.Add("Cannot reliably get SectionID for product linking without a more robust section tracking mechanism or assuming it's the current section being parsed.");

        //    return null;
        //    #endregion
        //}

        ///**************************************************************/
        ///// <summary>
        ///// Resolves a repository instance for the specified entity type from the service provider.
        ///// </summary>
        ///// <typeparam name="TRepoType">The entity type for which to get a repository</typeparam>
        ///// <param name="sp">Service provider to resolve dependencies from</param>
        ///// <returns>Repository instance for the specified type</returns>
        ///// <exception cref="InvalidOperationException">Thrown when repository cannot be resolved</exception>
        //private Repository<TRepoType> getRepository<TRepoType>(IServiceProvider sp) where TRepoType : class
        //{            
        //    #region implementation

        //    var repo = sp.GetService<Repository<TRepoType>>();

        //    if (repo == null)
        //    {
        //        throw new InvalidOperationException($"Could not resolve repository for type {typeof(TRepoType).FullName}. Ensure it and its dependencies are registered.");
        //    }

        //    return repo;
        //    #endregion
        //}
        #endregion

    }
}