using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using RazorLight;
using System.Dynamic;
using static MedRecPro.Models.Label;

namespace MedRecPro.Service
{
    // ========== INTERFACES ==========

    /**************************************************************/
    /// <summary>
    /// Service interface for retrieving document data for SPL (Structured Product Labeling) export operations.
    /// Provides data access functionality to fetch document information from the database
    /// for use in medical record export and template rendering processes.
    /// </summary>
    /// <seealso cref="DocumentDto"/>
    /// <seealso cref="ISplExportService"/>
    /// <seealso cref="ITemplateRenderingService"/>
    public interface IDocumentDataService
    {
        /**************************************************************/
        /// <summary>
        /// Asynchronously retrieves a document by its unique identifier for SPL export processing.
        /// Fetches the complete document data including all related medical record information
        /// required for structured product labeling generation.
        /// </summary>
        /// <param name="documentGuid">The unique identifier of the document to retrieve</param>
        /// <returns>The document DTO containing all export-ready data, or null if not found</returns>
        /// <seealso cref="DocumentDto"/>
        /// <seealso cref="ISplExportService.ExportDocumentToSplAsync"/>
        /// <example>
        /// <code>
        /// var documentService = serviceProvider.GetService&lt;IDocumentDataService&gt;();
        /// var document = await documentService.GetDocumentAsync(documentGuid);
        /// 
        /// if (document != null)
        /// {
        ///     ProcessDocumentForExport(document);
        /// }
        /// </code>
        /// </example>
        Task<DocumentDto?> GetDocumentAsync(Guid documentGuid);
    }

    /**************************************************************/
    /// <summary>
    /// Service interface for rendering Razor templates with data models for SPL export generation.
    /// Provides template compilation and rendering capabilities to transform medical record data
    /// into structured XML formats using Razor template syntax.
    /// </summary>
    /// <seealso cref="ISplExportService"/>
    /// <seealso cref="IDocumentDataService"/>
    /// <seealso cref="DocumentDto"/>
    public interface ITemplateRenderingService
    {
        /**************************************************************/
        /// <summary>
        /// Asynchronously renders a Razor template with the provided data model.
        /// Compiles and executes the specified template using the model data to generate
        /// structured output such as SPL XML content for medical record export.
        /// </summary>
        /// <typeparam name="TModel">The type of the data model to pass to the template</typeparam>
        /// <param name="templateName">The name of the template file to render (without extension)</param>
        /// <param name="model">The data model instance to use during template rendering</param>
        /// <returns>The rendered template content as a string</returns>
        /// <seealso cref="DocumentDto"/>
        /// <seealso cref="ISplExportService.ExportDocumentToSplAsync"/>
        /// <example>
        /// <code>
        /// var templateService = serviceProvider.GetService&lt;ITemplateRenderingService&gt;();
        /// var renderedXml = await templateService.RenderAsync("GenerateSpl", documentDto);
        /// 
        /// // renderedXml now contains the SPL XML content
        /// SaveToFile(renderedXml);
        /// </code>
        /// </example>
        /// <remarks>
        /// The template should exist in the configured template directory.
        /// The model will be available in the template through Razor syntax.
        /// </remarks>
        Task<string> RenderAsync<TModel>(string templateName, TModel model);
    }

    /**************************************************************/
    /// <summary>
    /// Main service interface for coordinating SPL (Structured Product Labeling) export operations.
    /// Orchestrates the complete export process by combining document data retrieval
    /// and template rendering to generate structured XML output for medical records.
    /// </summary>
    /// <seealso cref="IDocumentDataService"/>
    /// <seealso cref="ITemplateRenderingService"/>
    /// <seealso cref="DocumentDto"/>
    /// <remarks>
    /// This service serves as the primary entry point for SPL export functionality,
    /// coordinating multiple services to provide a complete export solution.
    /// </remarks>
    public interface ISplExportService
    {
        /**************************************************************/
        /// <summary>
        /// Asynchronously exports a document to SPL (Structured Product Labeling) XML format.
        /// Coordinates the complete export process including data retrieval and template rendering
        /// to generate compliant SPL XML output for regulatory and interchange purposes.
        /// </summary>
        /// <param name="documentGuid">The unique identifier of the document to export</param>
        /// <returns>The complete SPL XML content as a string</returns>
        /// <seealso cref="IDocumentDataService.GetDocumentAsync"/>
        /// <seealso cref="ITemplateRenderingService.RenderAsync"/>
        /// <seealso cref="DocumentDto"/>
        /// <example>
        /// <code>
        /// var splService = serviceProvider.GetService&lt;ISplExportService&gt;();
        /// var xmlContent = await splService.ExportDocumentToSplAsync(documentGuid);
        /// 
        /// // Save or transmit the SPL XML
        /// await File.WriteAllTextAsync("export.xml", xmlContent);
        /// SendToRegulatory(xmlContent);
        /// </code>
        /// </example>
        /// <remarks>
        /// This method handles the complete export workflow including error handling.
        /// Throws InvalidOperationException if the document is not found.
        /// </remarks>
        Task<string> ExportDocumentToSplAsync(Guid documentGuid);
    }

    // ========== IMPLEMENTATIONS ==========

    /**************************************************************/
    /// <summary>
    /// Implementation service for handling data access operations for document retrieval.
    /// Provides database access functionality to fetch document information required
    /// for SPL export operations using Entity Framework and custom data access layers.
    /// </summary>
    /// <seealso cref="IDocumentDataService"/>
    /// <seealso cref="DocumentDto"/>
    /// <seealso cref="ApplicationDbContext"/>
    /// <remarks>
    /// This service encapsulates database access logic and provides secure document retrieval
    /// using private key encryption for sensitive medical record data access.
    /// </remarks>
    public class DocumentDataService : IDocumentDataService
    {
        #region private fields

        /**************************************************************/
        /// <summary>
        /// Entity Framework database context for data access operations.
        /// Provides access to the application database for document and related entity queries.
        /// </summary>
        /// <seealso cref="ApplicationDbContext"/>
        private readonly ApplicationDbContext _db;

        /**************************************************************/
        /// <summary>
        /// Private key secret used for secure access to encrypted document data.
        /// Required for decrypting and accessing sensitive medical record information.
        /// </summary>
        private readonly string _pkSecret;

        /**************************************************************/
        /// <summary>
        /// Logger instance for recording data access operations and error information.
        /// Used to track document retrieval attempts and diagnostic information.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger _logger;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the DocumentDataService with required dependencies.
        /// Sets up the service with database context, encryption key, and logging capabilities
        /// for secure document data access operations.
        /// </summary>
        /// <param name="db">Entity Framework database context for data operations</param>
        /// <param name="configuration">Application configuration containing encryption settings and security keys</param>
        /// <param name="logger">Logger instance for operation tracking and diagnostics</param>
        /// <seealso cref="ApplicationDbContext"/>
        /// <seealso cref="IConfiguration"/>
        /// <seealso cref="ILogger"/>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when PK encryption secret is not configured</exception>
        /// <example>
        /// <code>
        /// // Dependency injection registration
        /// services.AddScoped&lt;IDocumentDataService, DocumentDataService&gt;();
        /// 
        /// // Configuration in appsettings.json:
        /// {
        ///   "Security": {
        ///     "DB": {
        ///       "PKSecret": "your-encryption-key-here"
        ///     }
        ///   }
        /// }
        /// </code>
        /// </example>
        public DocumentDataService(ApplicationDbContext db, IConfiguration configuration, ILogger logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pkSecret = configuration?.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException("PK encryption secret not configured");
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Asynchronously retrieves a document by its unique identifier for SPL export processing.
        /// Fetches the complete document data including all related medical record information
        /// using secure data access methods with private key decryption.
        /// </summary>
        /// <param name="documentGuid">The unique identifier of the document to retrieve</param>
        /// <returns>The document DTO containing all export-ready data, or null if not found</returns>
        /// <seealso cref="DocumentDto"/>
        /// <seealso cref="DtoLabelAccess.BuildDocumentsAsync"/>
        /// <seealso cref="ISplExportService.ExportDocumentToSplAsync"/>
        /// <example>
        /// <code>
        /// var service = new DocumentDataService(dbContext, privateKey, logger);
        /// var document = await service.GetDocumentAsync(documentGuid);
        /// 
        /// if (document != null)
        /// {
        ///     Console.WriteLine($"Retrieved document: {document.Title}");
        ///     ProcessForExport(document);
        /// }
        /// else
        /// {
        ///     Console.WriteLine("Document not found");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method uses the DtoLabelAccess layer for secure document retrieval with
        /// private key decryption. Returns null if the document is not found or access is denied.
        /// All retrieval attempts are logged for security and diagnostic purposes.
        /// </remarks>
        public async Task<DocumentDto?> GetDocumentAsync(Guid documentGuid)
        {
            #region implementation

            // Log the document retrieval attempt for diagnostics and security auditing
            _logger.LogInformation("Fetching document data for {DocumentGuid}", documentGuid);

            // Use secure data access layer to retrieve document with private key decryption
            var documents = await DtoLabelAccess.BuildDocumentsAsync(_db, documentGuid, _pkSecret, _logger);

            // Return the first document from the collection, or null if none found
            return documents?.FirstOrDefault();

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Implementation service for handling Razor template rendering operations.
    /// Provides template compilation and rendering capabilities using RazorLight engine
    /// to transform medical record data models into structured XML output formats.
    /// </summary>
    /// <seealso cref="ITemplateRenderingService"/>
    /// <seealso cref="IRazorLightEngine"/>
    /// <seealso cref="DocumentDto"/>
    /// <remarks>
    /// This service configures and manages a RazorLight engine instance for template processing.
    /// Templates are loaded from the file system and cached for performance optimization.
    /// The service implements IDisposable for proper resource cleanup.
    /// </remarks>
    public class TemplateRenderingService : ITemplateRenderingService, IDisposable
    {
        #region private fields

        /**************************************************************/
        /// <summary>
        /// RazorLight engine instance for compiling and rendering Razor templates.
        /// Configured with file system project and memory caching for optimal performance.
        /// </summary>
        /// <seealso cref="IRazorLightEngine"/>
        /// <seealso cref="RazorLightEngineBuilder"/>
        private readonly IRazorLightEngine _razorEngine;

        /**************************************************************/
        /// <summary>
        /// Logger instance for recording template rendering operations and error information.
        /// Used to track rendering attempts, performance, and diagnostic information.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger _logger;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the TemplateRenderingService with logging capability.
        /// Configures the RazorLight engine with file system template loading, memory caching,
        /// and debug mode for optimal template processing performance.
        /// </summary>
        /// <param name="logger">Logger instance for operation tracking and diagnostics</param>
        /// <seealso cref="ILogger"/>
        /// <seealso cref="RazorLightEngineBuilder"/>
        /// <seealso cref="IRazorLightEngine"/>
        /// <exception cref="ArgumentNullException">Thrown when logger parameter is null</exception>
        /// <example>
        /// <code>
        /// // Dependency injection registration
        /// services.AddScoped&lt;ITemplateRenderingService, TemplateRenderingService&gt;();
        /// 
        /// // Manual instantiation
        /// var service = new TemplateRenderingService(logger);
        /// </code>
        /// </example>
        /// <remarks>
        /// The service automatically configures the template folder path as "Views/SplTemplates"
        /// relative to the current directory. Memory caching is enabled for template compilation
        /// performance, and debug mode provides detailed error information during development.
        /// </remarks>
        public TemplateRenderingService(ILogger logger)
        {
            #region implementation

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure template folder path relative to application root
            var projectRoot = Directory.GetCurrentDirectory();
            var templateFolderPath = Path.Combine(projectRoot, "Views", "SplTemplates");

            // Build and configure RazorLight engine with optimal settings for template processing
            _razorEngine = new RazorLightEngineBuilder()
                .UseFileSystemProject(templateFolderPath) // Load templates from file system
                .UseMemoryCachingProvider() // Enable memory caching for performance
                .EnableDebugMode() // Provide detailed error information
                .Build();

            #endregion
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Asynchronously renders a Razor template with the provided data model.
        /// Compiles and executes the specified template using the model data to generate
        /// structured output such as SPL XML content with comprehensive error handling.
        /// </summary>
        /// <typeparam name="TModel">The type of the data model to pass to the template</typeparam>
        /// <param name="templateName">The name of the template file to render (without .cshtml extension)</param>
        /// <param name="model">The data model instance to use during template rendering</param>
        /// <returns>The rendered template content as a string</returns>
        /// <seealso cref="DocumentDto"/>
        /// <seealso cref="IRazorLightEngine"/>
        /// <seealso cref="ISplExportService.ExportDocumentToSplAsync"/>
        /// <exception cref="Exception">Thrown when template rendering fails</exception>
        /// <example>
        /// <code>
        /// var service = new TemplateRenderingService(logger);
        /// var documentDto = await GetDocumentAsync(documentGuid);
        /// 
        /// try
        /// {
        ///     var xmlContent = await service.RenderAsync("GenerateSpl", documentDto);
        ///     Console.WriteLine($"Generated XML: {xmlContent.Length} characters");
        /// }
        /// catch (Exception ex)
        /// {
        ///     Console.WriteLine($"Template rendering failed: {ex.Message}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// The template name should match a .cshtml file in the configured template directory.
        /// The model will be available in the template as the strongly-typed model object.
        /// All rendering attempts are logged for diagnostics, and errors are logged with full details.
        /// </remarks>
        public async Task<string> RenderAsync<TModel>(string templateName, TModel model)
        {
            #region implementation

            try
            {
                // Log template rendering attempt for performance monitoring and diagnostics
                _logger.LogDebug("Rendering template {TemplateName}", templateName);

                // Then render it with the model
                return await _razorEngine.CompileRenderAsync<TModel>(key: templateName, model: model, viewBag: null);
            }
            catch (Exception ex)
            {
                // Log detailed error information and re-throw for upstream handling
                _logger.LogError(ex, "Error rendering template {TemplateName}", templateName);
                throw;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Disposes of RazorLight engine resources and performs cleanup operations.
        /// Ensures proper resource management for template rendering components
        /// when the service instance is no longer needed.
        /// </summary>
        /// <seealso cref="IRazorLightEngine"/>
        /// <seealso cref="IDisposable"/>
        /// <example>
        /// <code>
        /// using var templateService = new TemplateRenderingService(logger);
        /// var result = await templateService.RenderAsync("Template", model);
        /// // Service will be automatically disposed here
        /// </code>
        /// </example>
        /// <remarks>
        /// This method is called automatically when using the service in a using statement
        /// or when the service is disposed by the dependency injection container.
        /// </remarks>
        public void Dispose()
        {
            #region implementation

            // Dispose of RazorLight resources if needed
            // Note: Current RazorLight implementation may not require explicit disposal,
            // but this provides a cleanup point for future resource management needs

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Enhanced SPL export service implementation with comprehensive product rendering integration.
    /// Provides complete document-to-SPL XML conversion with structured body processing,
    /// section rendering optimization, and product rendering preparation using enhanced product collections.
    /// </summary>
    /// <seealso cref="IProductRenderingService"/>
    /// <seealso cref="ISplExportService"/>
    /// <seealso cref="DocumentDto"/>
    /// <seealso cref="StructuredBodyDto"/>
    /// <seealso cref="SectionDto"/>
    /// <seealso cref="ProductDto"/>
    /// <seealso cref="IDocumentDataService"/>
    /// <seealso cref="IDocumentRenderingService"/>
    /// <seealso cref="ITemplateRenderingService"/>
    /// <seealso cref="IStructuredBodyViewModelFactory"/>
    /// <seealso cref="ISectionRenderingService"/>
    /// <seealso cref="SectionRendering"/>
    /// <seealso cref="ProductRendering"/>
    /// <remarks>
    /// This service orchestrates the complete SPL export pipeline, including:
    /// - Document data retrieval and validation
    /// - Structured body processing and view model creation
    /// - Section context enhancement with pre-computed properties
    /// - Product rendering preparation using enhanced product collections
    /// - Final SPL XML template rendering with optimized performance
    /// 
    /// The service now uses enhanced product collections (EnhancedProducts) within section rendering
    /// contexts to provide optimal template processing performance.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Inject the service through dependency injection
    /// var splService = serviceProvider.GetRequiredService&lt;ISplExportService&gt;();
    /// 
    /// // Export document to SPL XML
    /// var documentGuid = Guid.Parse("12345678-1234-1234-1234-123456789012");
    /// var splXml = await splService.ExportDocumentToSplAsync(documentGuid);
    /// </code>
    /// </example>
    public class SplExportService : ISplExportService
    {
        #region private fields

        /// <summary>
        /// Service for retrieving document data from the database with validation and security checks.
        /// </summary>
        /// <seealso cref="IDocumentDataService"/>
        /// <seealso cref="DocumentDto"/>
        private readonly IDocumentDataService _documentDataService;

        /// <summary>
        /// Service for rendering top-level SPL XML view with document context preparation.
        /// </summary>
        /// <seealso cref="IDocumentRenderingService"/>
        /// <seealso cref="DocumentDto"/>
        private readonly IDocumentRenderingService _documentRenderingService;

        /// <summary>
        /// Service for rendering Razor templates with prepared data models and context.
        /// </summary>
        /// <seealso cref="ITemplateRenderingService"/>
        private readonly ITemplateRenderingService _templateRenderingService;

        /// <summary>
        /// Factory service for generating structured body view models with section organization.
        /// </summary>
        /// <seealso cref="IStructuredBodyViewModelFactory"/>
        /// <seealso cref="StructuredBodyDto"/>
        /// <seealso cref="StructuredBodyViewModel"/>
        private readonly IStructuredBodyViewModelFactory _viewModelFactory;

        /// <summary>
        /// Service for section rendering preparation with pre-computed properties and context enhancement.
        /// </summary>
        /// <seealso cref="ISectionRenderingService"/>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="SectionRendering"/>
        private readonly ISectionRenderingService _sectionRenderingService;

        /// <summary>
        /// Service for product rendering preparation with optimization and enhanced product collection creation.
        /// </summary>
        /// <seealso cref="IProductRenderingService"/>
        /// <seealso cref="ProductDto"/>
        /// <seealso cref="ProductRendering"/>
        private readonly IProductRenderingService _productRenderingService;

        /// <summary>
        /// Service for ingredient rendering preparation
        /// </summary>
        private readonly IIngredientRenderingService _ingredientRenderingService;

        /// <summary>
        /// Service for packaging spl products
        /// </summary>
        private readonly IPackageRenderingService _packageRenderingService;

        /// <summary>
        /// Service for text content rendering preparation
        /// </summary>
        private readonly ITextContentRenderingService _textContentRenderingService;

        /// <summary>
        /// Characteristic rendering service
        /// </summary>
        private readonly ICharacteristicRenderingService _characteristicRenderingService;

        /// <summary>
        /// Logger instance for operation tracking, performance monitoring, and diagnostic information.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger _logger;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SplExportService with required service dependencies.
        /// Sets up the export service with document data access, template rendering capabilities,
        /// product rendering services, text content rendering services, and logging for comprehensive SPL export functionality.
        /// </summary>
        /// <param name="documentDataService">Service for retrieving document data from the database</param>
        /// <param name="documentRenderingService">Service for rendering top level spl xml view</param>
        /// <param name="templateRenderingService">Service for rendering Razor templates with data models</param>
        /// <param name="structuredBodyViewModelFactory">Service for generating views</param>
        /// <param name="sectionRenderingService">Service for section rendering preparation</param>
        /// <param name="productRenderingService">Service for product rendering preparation</param>
        /// <param name="ingredientRenderingService">Service for ingredient rendering preparation</param>
        /// <param name="packageRenderingService">Service for packaging</param>
        /// <param name="textContentRenderingService">Service for text content rendering preparation</param>
        /// <param name="characteristicRenderingService">Service for characteristic rendering</param>
        /// <param name="logger">Logger instance for operation tracking and diagnostics</param>
        /// <exception cref="ArgumentNullException">Thrown when any required service dependency is null</exception>
        /// <seealso cref="IDocumentDataService"/>
        /// <seealso cref="IDocumentRenderingService"/>
        /// <seealso cref="ITemplateRenderingService"/>
        /// <seealso cref="IStructuredBodyViewModelFactory"/>
        /// <seealso cref="ISectionRenderingService"/>
        /// <seealso cref="IProductRenderingService"/>
        /// <seealso cref="ITextContentRenderingService"/>
        /// <seealso cref="ILogger"/>
        /// <remarks>
        /// All service dependencies are validated for null values during construction.
        /// The constructor follows the dependency injection pattern for service resolution.
        /// The text content rendering service is now passed to section rendering preparation for enhanced integration.
        /// </remarks>
        public SplExportService(
        IDocumentDataService documentDataService,
        IDocumentRenderingService documentRenderingService,
        ITemplateRenderingService templateRenderingService,
        IStructuredBodyViewModelFactory structuredBodyViewModelFactory,
        ISectionRenderingService sectionRenderingService,
        IProductRenderingService productRenderingService,
        IIngredientRenderingService ingredientRenderingService,
        IPackageRenderingService packageRenderingService,
        ITextContentRenderingService textContentRenderingService,
        ICharacteristicRenderingService characteristicRenderingService,
        ILogger logger
       )
        {
            #region implementation
            _documentDataService = documentDataService ?? throw new ArgumentNullException(nameof(documentDataService));
            _documentRenderingService = documentRenderingService ?? throw new ArgumentNullException(nameof(documentRenderingService));
            _templateRenderingService = templateRenderingService ?? throw new ArgumentNullException(nameof(templateRenderingService));
            _viewModelFactory = structuredBodyViewModelFactory ?? throw new ArgumentNullException(nameof(structuredBodyViewModelFactory));
            _sectionRenderingService = sectionRenderingService ?? throw new ArgumentNullException(nameof(sectionRenderingService));
            _productRenderingService = productRenderingService ?? throw new ArgumentNullException(nameof(productRenderingService));
            _ingredientRenderingService = ingredientRenderingService ?? throw new ArgumentNullException(nameof(ingredientRenderingService));
            _packageRenderingService = packageRenderingService ?? throw new ArgumentNullException(nameof(packageRenderingService));
            _textContentRenderingService = textContentRenderingService ?? throw new ArgumentNullException(nameof(textContentRenderingService));
            _characteristicRenderingService = characteristicRenderingService ?? throw new ArgumentNullException(nameof(characteristicRenderingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            #endregion
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Enhanced ExportDocumentToSplAsync method with comprehensive product rendering preparation.
        /// Orchestrates the complete document export pipeline from data retrieval through SPL XML generation,
        /// including structured body processing, section enhancement, and optimized product rendering collections.
        /// </summary>
        /// <param name="documentGuid">Unique identifier for the document to export to SPL format</param>
        /// <returns>Complete SPL XML content as a string ready for output or further processing</returns>
        /// <exception cref="InvalidOperationException">Thrown when the document is not found or inaccessible</exception>
        /// <exception cref="ArgumentException">Thrown when documentGuid is empty or invalid</exception>
        /// <seealso cref="DocumentDto"/>
        /// <seealso cref="IProductRenderingService.PrepareForRendering"/>
        /// <seealso cref="IDocumentDataService.GetDocumentAsync"/>
        /// <seealso cref="IDocumentRenderingService.PrepareForRendering"/>
        /// <seealso cref="ITemplateRenderingService.RenderAsync"/>
        /// <seealso cref="processStructuredBodyForRenderingAsync"/>
        /// <remarks>
        /// The export process follows these key steps:
        /// 1. Document data retrieval with validation
        /// 2. Document rendering context preparation
        /// 3. Structured body processing with enhanced product collections
        /// 4. SPL template rendering with optimized context
        /// 
        /// All operations are logged for audit trails and performance monitoring.
        /// Enhanced product collections provide improved template processing performance.
        /// </remarks>
        /// <example>
        /// <code>
        /// var documentId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
        /// var splXml = await exportService.ExportDocumentToSplAsync(documentId);
        /// Console.WriteLine($"Generated SPL XML: {splXml.Length} characters");
        /// </code>
        /// </example>
        public async Task<string> ExportDocumentToSplAsync(Guid documentGuid)
        {
            #region implementation

            // Log the start of the export process for audit and performance tracking
            _logger.LogInformation("Starting SPL export for document {DocumentGuid}", documentGuid);

            try
            {
                // Step 1: Retrieve document data securely from the database with comprehensive validation
                var documentDto = await _documentDataService.GetDocumentAsync(documentGuid);

                // Validate that the document was found and is accessible to prevent downstream errors
                if (documentDto == null)
                {
                    throw new InvalidOperationException($"Document with GUID {documentGuid} not found");
                }

                // Step 2: Prepare document for optimized rendering with pre-computed properties and context
                _logger.LogDebug("Preparing document rendering context for {DocumentGuid}", documentGuid);
                var documentRendering = _documentRenderingService.PrepareForRendering(documentDto);

                // Step 3: Process structured bodies for rendering optimization with enhanced product collections
                if (documentDto.StructuredBodies != null && documentDto.StructuredBodies.Any())
                {
                    // Iterate through each structured body to prepare comprehensive rendering context
                    foreach (var body in documentDto.StructuredBodies)
                    {
                        _logger.LogDebug("Processing structured body for document {DocumentGuid}", documentGuid);
                        // Process each body with section and enhanced product preparation
                        await processStructuredBodyForRenderingAsync(body, documentGuid);
                    }
                }

                // Step 4: Render the SPL template with the enhanced document rendering context
                // The "GenerateSpl" template uses the fully prepared document rendering context with enhanced products
                var xmlContent = await _templateRenderingService.RenderAsync("GenerateSpl", documentRendering);

                // Log successful completion with basic metrics for performance monitoring
                _logger.LogInformation("Successfully exported document {DocumentGuid} to SPL XML", documentGuid);

                // Return the complete SPL XML content
                return xmlContent;
            }
            catch (Exception ex)
            {
                // Log detailed error information for diagnostics and re-throw for upstream handling
                _logger.LogError(ex, "Error exporting document {DocumentGuid} to SPL", documentGuid);
                throw;
            }

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Processes a structured body for optimized rendering with pre-computed section and product properties.
        /// Creates view models and prepares all section contexts with rendering-ready data and enhanced product collections.
        /// Handles both standalone and hierarchical section organization patterns with comprehensive optimization.
        /// </summary>
        /// <param name="structuredBody">The structured body to process with sections and products</param>
        /// <param name="documentGuid">Document GUID for logging context and traceability</param>
        /// <returns>Task representing the asynchronous processing operation</returns>
        /// <seealso cref="IStructuredBodyViewModelFactory.Create"/>
        /// <seealso cref="ISectionRenderingService.PrepareSectionForRendering"/>
        /// <seealso cref="IProductRenderingService.PrepareForRendering"/>
        /// <seealso cref="StructuredBodyViewModel"/>
        /// <seealso cref="SectionRendering"/>
        /// <seealso cref="ProductRendering"/>
        /// <seealso cref="StructuredBodyDto"/>
        /// <seealso cref="enhanceSectionContexts"/>
        /// <seealso cref="enhanceHierarchicalSectionContextsAsync"/>
        /// <seealso cref="enhanceAllSectionContextsAsync"/>
        /// <remarks>
        /// The processing workflow includes:
        /// - Base view model creation with section organization
        /// - Standalone section context enhancement with enhanced products
        /// - Hierarchical section context enhancement with recursive processing
        /// - Unified section collection processing with comprehensive optimization
        /// - Enhanced product collections for improved template performance
        /// </remarks>
        private async Task processStructuredBodyForRenderingAsync(StructuredBodyDto structuredBody, Guid documentGuid)
        {
            #region implementation

            _logger.LogDebug("Creating structured body view model for document {DocumentGuid}", documentGuid);

            // Create the base view model using the factory (this handles section organization and relationships)
            var viewModel = _viewModelFactory.Create(structuredBody);

            // Step 2a: Enhance standalone section contexts with pre-computed properties and enhanced products
            if (viewModel.HasStandaloneSections && viewModel.StandaloneSectionContexts?.Any() == true)
            {
                _logger.LogDebug("Enhancing {Count} standalone sections for document {DocumentGuid}",
                    viewModel.StandaloneSectionContexts.Count, documentGuid);

                // Process standalone sections with optimized rendering preparation and enhanced product collections
                var enhancedStandalone = enhanceSectionContexts(viewModel.StandaloneSectionContexts, true, documentGuid);
                viewModel.StandaloneSectionContexts = enhancedStandalone;
            }

            // Step 2b: Enhance hierarchical section contexts with pre-computed properties and recursive processing
            if (viewModel.HasHierarchicalSections && viewModel.HierarchicalSectionContexts?.Any() == true)
            {
                _logger.LogDebug("Enhancing {Count} hierarchical sections for document {DocumentGuid}",
                    viewModel.HierarchicalSectionContexts.Count, documentGuid);

                // Process hierarchical sections with recursive enhancement for nested structures and enhanced products
                var enhancedHierarchical = await enhanceHierarchicalSectionContextsAsync(viewModel.HierarchicalSectionContexts, documentGuid);
                viewModel.HierarchicalSectionContexts = enhancedHierarchical;
            }

            // Step 2c: Enhance the unified AllSectionContexts collection for comprehensive processing
            if (viewModel.AllSectionContexts?.Any() == true)
            {
                _logger.LogDebug("Enhancing unified collection of {Count} sections for document {DocumentGuid}",
                    viewModel.AllSectionContexts.Count, documentGuid);

                // Process the complete unified collection maintaining document order with enhanced products
                var enhancedAll = await enhanceAllSectionContextsAsync(viewModel.AllSectionContexts, documentGuid);
                viewModel.AllSectionContexts = enhancedAll;
            }

            // Assign the enhanced view model back to the structured body for template access
            structuredBody.StructuredBodyView = viewModel;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Enhances section contexts with pre-computed rendering properties and processes products using enhanced collections.
        /// Applies both section and product rendering services to prepare all display-ready data for optimal template performance.
        /// Handles both standalone and hierarchical section types with appropriate processing logic and enhanced product integration.
        /// </summary>
        /// <param name="sectionContexts">Original section contexts to enhance with rendering properties</param>
        /// <param name="isStandalone">Whether these are standalone sections (affects processing logic)</param>
        /// <param name="documentGuid">Document GUID for logging context and traceability</param>
        /// <returns>Enhanced section contexts with pre-computed properties and enhanced product collections</returns>
        /// <seealso cref="ISectionRenderingService.PrepareSectionForRendering"/>
        /// <seealso cref="IProductRenderingService.PrepareForRendering"/>
        /// <seealso cref="SectionRendering"/>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="processProductsInSection"/>
        /// <remarks>
        /// Enhancement process includes:
        /// - Section rendering context preparation with product rendering service integration
        /// - Child and hierarchical relationship processing
        /// - Enhanced product collection creation within each section
        /// - Performance-optimized property computation
        /// - Integration of product rendering service for comprehensive optimization
        /// </remarks>
        private List<SectionRendering> enhanceSectionContexts(
            List<SectionRendering> sectionContexts,
            bool isStandalone,
            Guid documentGuid)
        {
            #region implementation

            var enhancedContexts = new List<SectionRendering>();

            // Process each section context individually for maximum rendering optimization
            foreach (var context in sectionContexts)
            {
                // Use the rendering service to create a fully prepared section context with integrated services
                var enhancedContext = _sectionRenderingService.PrepareSectionForRendering(
                    section: context.Section,
                    children: context.Children,
                    hierarchicalChildren: context.HierarchicalChildren,
                    isStandalone: isStandalone,
                    productRenderingService: _productRenderingService,
                    textContentRenderingService: _textContentRenderingService
                );

                // Process products within this section for enhanced collections and optimized rendering
                processProductsInSection(enhancedContext, documentGuid);

                // Add the fully enhanced context with enhanced products and text content to the collection
                enhancedContexts.Add(enhancedContext);
            }

            return enhancedContexts;


            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Enhances hierarchical section contexts recursively with pre-computed properties and processes products and text content using enhanced collections.
        /// Processes the complete hierarchy tree to prepare all levels for rendering with optimized performance and enhanced integration.
        /// Maintains parent-child relationships while ensuring comprehensive processing at all levels using enhanced collections.
        /// </summary>
        /// <param name="hierarchicalContexts">Hierarchical contexts to enhance with nested processing</param>
        /// <param name="documentGuid">Document GUID for logging context and traceability</param>
        /// <returns>Enhanced hierarchical contexts with complete nested preparation, enhanced product collections, and rendered text content</returns>
        /// <seealso cref="ISectionRenderingService.PrepareSectionForRendering"/>
        /// <seealso cref="SectionRendering"/>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="processProductsInSection"/>
        /// <remarks>
        /// Recursive processing workflow:
        /// - Child contexts are processed first (depth-first approach)
        /// - Parent contexts are enhanced with prepared children and comprehensive service integration
        /// - Enhanced product and text content collection processing occurs at each level
        /// - Hierarchical relationships are preserved throughout
        /// - Service integration provides comprehensive optimization
        /// </remarks>
        private async Task<List<SectionRendering>> enhanceHierarchicalSectionContextsAsync(
            List<SectionRendering> hierarchicalContexts,
            Guid documentGuid)
        {
            #region implementation

            var enhancedContexts = new List<SectionRendering>();

            // Process each hierarchical context with recursive child enhancement and comprehensive service integration
            foreach (var context in hierarchicalContexts)
            {
                // Recursively enhance child contexts first (depth-first processing for optimal hierarchy handling)
                var enhancedChildContexts = context.HierarchicalChildren?.Any() == true
                    ? await enhanceHierarchicalSectionContextsAsync(context.HierarchicalChildren, documentGuid)
                    : new List<SectionRendering>();

                // Create enhanced parent context with prepared children and integrated services
                var enhancedContext = _sectionRenderingService.PrepareSectionForRendering(
                    section: context.Section,
                    children: context.Children,
                    hierarchicalChildren: enhancedChildContexts,
                    isStandalone: false,
                    productRenderingService: _productRenderingService,
                    textContentRenderingService: _textContentRenderingService
                );

                // Process products within this section for enhanced collections at the current level
                processProductsInSection(enhancedContext, documentGuid);

                // Add the fully enhanced hierarchical context with enhanced products and text content to the collection
                enhancedContexts.Add(enhancedContext);
            }

            return enhancedContexts;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Enhances the unified AllSectionContexts collection recursively and processes products and text content using enhanced collections.
        /// Processes both standalone and hierarchical sections in document order while maintaining
        /// section type distinctions and applying appropriate processing logic for each type with comprehensive service integration.
        /// </summary>
        /// <param name="allSectionContexts">All section contexts to enhance in unified processing</param>
        /// <param name="documentGuid">Document GUID for logging context and traceability</param>
        /// <returns>Enhanced unified section contexts with comprehensive processing, enhanced product collections, and rendered text content</returns>
        /// <seealso cref="ISectionRenderingService.PrepareSectionForRendering"/>
        /// <seealso cref="SectionRendering"/>
        /// <seealso cref="SectionDto"/>
        /// <seealso cref="processProductsInSection"/>
        /// <seealso cref="enhanceHierarchicalSectionContextsAsync"/>
        /// <remarks>
        /// Unified processing handles:
        /// - Standalone sections with direct enhancement, enhanced product collections, and rendered text content
        /// - Hierarchical sections with recursive child processing, enhanced products, and text content
        /// - Document order preservation
        /// - Enhanced collection processing at all levels
        /// - Section type-appropriate rendering logic with comprehensive service integration
        /// </remarks>
        private async Task<List<SectionRendering>> enhanceAllSectionContextsAsync(
            List<SectionRendering> allSectionContexts,
            Guid documentGuid)
        {
            #region implementation

            var enhancedContexts = new List<SectionRendering>();

            // Process each section context with type-appropriate enhancement logic and comprehensive service integration
            foreach (var context in allSectionContexts)
            {
                if (context.IsStandalone)
                {
                    // Handle standalone section with direct processing and integrated services
                    var enhancedContext = _sectionRenderingService.PrepareSectionForRendering(
                        section: context.Section,
                        children: context.Children,
                        hierarchicalChildren: context.HierarchicalChildren,
                        isStandalone: true,
                        productRenderingService: _productRenderingService,
                        textContentRenderingService: _textContentRenderingService
                    );

                    // Process products within this standalone section using enhanced collections
                    processProductsInSection(enhancedContext, documentGuid);

                    enhancedContexts.Add(enhancedContext);
                }
                else
                {
                    // Handle hierarchical section with recursive enhancement for nested structures and comprehensive services
                    var enhancedChildContexts = context.HierarchicalChildren?.Any() == true
                        ? await enhanceHierarchicalSectionContextsAsync(context.HierarchicalChildren, documentGuid)
                        : new List<SectionRendering>();

                    // Create enhanced hierarchical context with prepared children and integrated services
                    var enhancedContext = _sectionRenderingService.PrepareSectionForRendering(
                        section: context.Section,
                        children: context.Children,
                        hierarchicalChildren: enhancedChildContexts,
                        isStandalone: false,
                        productRenderingService: _productRenderingService,
                        textContentRenderingService: _textContentRenderingService
                    );

                    // Process products within this hierarchical section using enhanced collections
                    processProductsInSection(enhancedContext, documentGuid);

                    enhancedContexts.Add(enhancedContext);
                }
            }

            return enhancedContexts;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes products within a section rendering context for optimized rendering with pre-computed properties.
        /// Creates enhanced ProductRendering objects from OrderedProducts and stores them in the section's EnhancedProducts collection
        /// for optimal template processing performance and comprehensive property computation.
        /// </summary>
        /// <param name="sectionRendering">The section rendering context containing products to process</param>
        /// <param name="documentGuid">Document GUID for logging context and additional processing parameters</param>
        /// <returns>Task representing the asynchronous processing operation</returns>
        /// <seealso cref="SectionRendering.OrderedProducts"/>
        /// <seealso cref="SectionRendering.RenderedProducts"/>
        /// <seealso cref="SectionRendering.HasProducts"/>
        /// <seealso cref="SectionRendering.HasRenderedProducts"/>
        /// <seealso cref="IProductRenderingService.PrepareForRendering"/>
        /// <seealso cref="ProductRendering"/>
        /// <seealso cref="ProductDto"/>
        /// <remarks>
        /// Product processing workflow:
        /// - Validation of existing products in OrderedProducts collection
        /// - Enhanced ProductRendering creation for each product
        /// - Additional parameters inclusion (DocumentGuid) for context
        /// - Enhanced product collection population in section rendering context
        /// - Performance tracking and logging for monitoring
        /// 
        /// The enhanced products provide optimized template processing with pre-computed properties.
        /// If no products exist, the enhanced product collections are properly initialized as empty.
        /// </remarks>
        private void processProductsInSection(SectionRendering sectionRendering, Guid documentGuid)
        {
            #region implementation

            // Process products if they exist within this section's OrderedProducts collection
            if (sectionRendering.HasProducts && sectionRendering.OrderedProducts?.Any() == true)
            {
                _logger.LogDebug("Processing {Count} products in section for document {DocumentGuid}",
                    sectionRendering.OrderedProducts.Count(), documentGuid);

                // Initialize enhanced products collection for optimized template processing
                var enhancedProducts = new List<ProductRendering>();

                // Process each product in the ordered collection for enhanced rendering preparation
                foreach (var product in sectionRendering.OrderedProducts)
                {
                    // Create enhanced ProductRendering using the service with comprehensive property computation
                    var enhancedProductRendering = _productRenderingService.PrepareForRendering(
                        product: product,
                        additionalParams: new { DocumentGuid = documentGuid },
                        ingredientRenderingService: _ingredientRenderingService,
                        packageRenderingService: _packageRenderingService,
                        characteristicRenderingService: _characteristicRenderingService
                    );

                    // Add the enhanced product rendering to the collection
                    enhancedProducts.Add(enhancedProductRendering);
                }

                // Store the enhanced products in the section rendering context for template access
                sectionRendering.RenderedProducts = enhancedProducts;
                sectionRendering.HasRenderedProducts = enhancedProducts.Any();

                _logger.LogDebug("Successfully enhanced {Count} products for section in document {DocumentGuid}",
                    enhancedProducts.Count, documentGuid);
            }
            else
            {
                // No products to process - initialize enhanced product collections as empty
                sectionRendering.RenderedProducts = null;
                sectionRendering.HasRenderedProducts = false;
            }

            #endregion
        }

        #endregion
    }
}