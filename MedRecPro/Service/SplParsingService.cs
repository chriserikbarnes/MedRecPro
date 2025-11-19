
using System.Xml.Linq;
using MedRecPro.Models;
using MedRecPro.Service.ParsingServices; // Import the new parsers
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
using MedRecPro.Data;
using static MedRecPro.Models.Label;
using Microsoft.EntityFrameworkCore;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

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
        /// Resolves deferred facility-product links after all products have been created.
        /// </summary>
        /// <param name="context">The parsing context containing database access.</param>
        /// <returns>The number of links successfully resolved.</returns>
        private async Task<int> resolveDeferredFacilityLinksAsync(SplParseContext context)
        {
            if (context != null && context?.ServiceProvider == null) return 0;

            var dbContext = context?.ServiceProvider?.GetRequiredService<ApplicationDbContext>();
            int resolvedCount = 0;

            if (dbContext == null) return 0;

            // Get all unresolved facility links for this document
            var unresolvedLinks = await dbContext.Set<FacilityProductLink>()
                .Include(fl => fl.DocumentRelationship)
                .Where(fl => !fl.IsResolved
                    && fl.DocumentRelationship != null
                    && context != null
                    && context.Document != null
                    && fl.DocumentRelationship.DocumentID == context.Document.DocumentID &&
                       !string.IsNullOrEmpty(fl.ProductName))
                .ToListAsync();

            context?.Logger?.LogInformation("Found {count} unresolved facility-product links to process", unresolvedLinks.Count);

            foreach (var link in unresolvedLinks)
            {
                bool resolved = false;

                // Try to resolve by CLN first (most common case)
                // CLN codes follow pattern like "63020-230", "63020-390", etc.
                var productIdentifier = await dbContext.Set<ProductIdentifier>()
                    .OrderByDescending(pi => pi.ProductIdentifierID) // Prefer latest entry if multiple
                    .FirstOrDefaultAsync(pi => pi.IdentifierValue == link.ProductName);

                if (productIdentifier != null)
                {
                    // Resolve by CLN
                    link.ProductID = productIdentifier.ProductID;
                    link.ProductIdentifierID = productIdentifier.ProductIdentifierID;
                    link.ProductName = link.ProductName;
                    link.IsResolved = true;
                    resolved = true;
                    context?.Logger?.LogInformation("Resolved facility link via CLN: {cln} -> ProductID {productId}",
                        productIdentifier.IdentifierValue, productIdentifier.ProductID);
                }
                else
                {
                    // Try to resolve by product name
                    var product = await dbContext.Set<Product>()
                        .FirstOrDefaultAsync(p => p.ProductName == link.ProductName);

                    if (product != null)
                    {
                        link.ProductID = product.ProductID;
                        link.IsResolved = true;
                        resolved = true;
                        context?.Logger?.LogInformation("Resolved facility link via name: '{name}' -> ProductID {productId}",
                            link.ProductName, product.ProductID);
                        link.ProductName = null; // Clear since we now have ProductID
                    }
                }

                if (resolved)
                {
                    resolvedCount++;
                }
                else
                {
                    context?.Logger?.LogWarning("Could not resolve facility link for: '{reference}' - product may not exist in this document",
                        link.ProductName);
                }
            }

            // Save changes if any links were resolved
            if (resolvedCount > 0)
            {
                await dbContext.SaveChangesAsync();
                context?.Logger?.LogInformation("Successfully resolved {count} facility-product links", resolvedCount);
            }
            else if (unresolvedLinks.Any())
            {
                context?.Logger?.LogWarning("No facility-product links could be resolved. Products may be in different documents or have different identifiers.");
            }

            return resolvedCount;
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

            // Get configuration from ROOT provider before creating scope
            var rootConfig = _serviceProvider.GetRequiredService<IConfiguration>();
            var useBulkOps = rootConfig.GetValue<bool>("FeatureFlags:UseBulkOperations", false);
            var useStaging = rootConfig.GetValue<bool>("FeatureFlags:UseBulkStaging", false);

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

            // Manually set feature flag
            context.SetBulkOperationsFlag(useBulkOps);
            context.SetBulkStagingFlag(useStaging);

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

                // Add calls to other top-level element parsers here as needed.
                reportProgress?.Invoke("Resolving facility-product links...");
                var resolvedCount = await resolveDeferredFacilityLinksAsync(context);
                fileResult.Message += $" Resolved {resolvedCount} facility links.";

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
    }
}