using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Models;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using RazorLight;
using System.Dynamic;

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
    /// Main SPL export service implementation with separated concerns and comprehensive coordination.
    /// Orchestrates the complete SPL (Structured Product Labeling) export process by combining
    /// document data retrieval and template rendering services to generate compliant XML output.
    /// </summary>
    /// <seealso cref="ISplExportService"/>
    /// <seealso cref="IDocumentDataService"/>
    /// <seealso cref="ITemplateRenderingService"/>
    /// <seealso cref="DocumentDto"/>
    /// <remarks>
    /// This service follows the separation of concerns principle by delegating specific
    /// responsibilities to specialized services while coordinating the overall export workflow.
    /// Comprehensive error handling and logging ensure reliable operation and diagnostics.
    /// </remarks>
    public class SplExportService : ISplExportService
    {
        #region private fields

        /**************************************************************/
        /// <summary>
        /// Service for retrieving document data from the database for export processing.
        /// Handles secure data access with encryption and provides document DTOs for template rendering.
        /// </summary>
        /// <seealso cref="IDocumentDataService"/>
        /// <seealso cref="DocumentDto"/>
        private readonly IDocumentDataService _documentDataService;

        /**************************************************************/
        /// <summary>
        /// Service for rendering Razor templates with document data models.
        /// Transforms document data into structured XML format using configured template files.
        /// </summary>
        /// <seealso cref="ITemplateRenderingService"/>
        /// <seealso cref="DocumentDto"/>
        private readonly ITemplateRenderingService _templateRenderingService;

        /**************************************************************/
        /// <summary>
        /// Logger instance for recording export operations, workflow steps, and error information.
        /// Used to track the complete export process and provide diagnostic information.
        /// </summary>
        /// <seealso cref="ILogger"/>
        private readonly ILogger _logger;

        /**************************************************************/
        /// <summary>
        /// Represents a factory used to create instances of structured body view models.
        /// </summary>
        /// <remarks>This field is intended to store a reference to an implementation of the <see
        /// cref="IStructuredBodyViewModelFactory"/> interface, which provides methods for creating structured body view
        /// models. It is typically used to generate view models dynamically based on specific requirements.</remarks>
        private readonly IStructuredBodyViewModelFactory _viewModelFactory;

        #endregion

        #region constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the SplExportService with required service dependencies.
        /// Sets up the export service with document data access, template rendering capabilities,
        /// and logging for comprehensive SPL export functionality.
        /// </summary>
        /// <param name="documentDataService">Service for retrieving document data from the database</param>
        /// <param name="templateRenderingService">Service for rendering Razor templates with data models</param>
        /// <param name="structuredBodyViewModelFactory">Service for generating views</param>
        /// <param name="logger">Logger instance for operation tracking and diagnostics</param>
        /// <seealso cref="IDocumentDataService"/>
        /// <seealso cref="ITemplateRenderingService"/>
        /// <seealso cref="ILogger"/>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null</exception>
        /// <example>
        /// <code>
        /// // Dependency injection registration
        /// services.AddScoped&lt;ISplExportService, SplExportService&gt;();
        /// 
        /// // Manual instantiation (not recommended)
        /// var exportService = new SplExportService(documentService, templateService, structuredBodyViewModelFactory, logger);
        /// </code>
        /// </example>
        /// <remarks>
        /// All dependencies are required and validated for null values to ensure reliable service operation.
        /// This constructor supports dependency injection patterns for clean service composition.
        /// </remarks>
        public SplExportService(
            IDocumentDataService documentDataService,
            ITemplateRenderingService templateRenderingService,
            IStructuredBodyViewModelFactory structuredBodyViewModelFactory,
            ILogger logger)
        {
            #region implementation

            _documentDataService = documentDataService ?? throw new ArgumentNullException(nameof(documentDataService));
            _templateRenderingService = templateRenderingService ?? throw new ArgumentNullException(nameof(templateRenderingService));
            _viewModelFactory = structuredBodyViewModelFactory ?? throw new ArgumentNullException(nameof(structuredBodyViewModelFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            #endregion
        }

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Asynchronously exports a document to SPL (Structured Product Labeling) XML format.
        /// Coordinates the complete export workflow including secure data retrieval and template rendering
        /// to generate compliant SPL XML output with comprehensive error handling and logging.
        /// </summary>
        /// <param name="documentGuid">The unique identifier of the document to export to SPL format</param>
        /// <returns>The complete SPL XML content as a formatted string ready for use or transmission</returns>
        /// <seealso cref="IDocumentDataService.GetDocumentAsync"/>
        /// <seealso cref="ITemplateRenderingService.RenderAsync"/>
        /// <seealso cref="DocumentDto"/>
        /// <exception cref="InvalidOperationException">Thrown when the document is not found in the database</exception>
        /// <exception cref="Exception">Thrown when template rendering or data access fails</exception>
        /// <example>
        /// <code>
        /// var exportService = serviceProvider.GetService&lt;ISplExportService&gt;();
        /// 
        /// try
        /// {
        ///     var xmlContent = await exportService.ExportDocumentToSplAsync(documentGuid);
        ///     
        ///     // Save the SPL XML to file
        ///     await File.WriteAllTextAsync($"spl-{documentGuid}.xml", xmlContent);
        ///     
        ///     // Or send to regulatory system
        ///     await SendToRegulatorySystem(xmlContent);
        ///     
        ///     Console.WriteLine($"Export completed: {xmlContent.Length} characters");
        /// }
        /// catch (InvalidOperationException ex)
        /// {
        ///     Console.WriteLine($"Document not found: {ex.Message}");
        /// }
        /// catch (Exception ex)
        /// {
        ///     Console.WriteLine($"Export failed: {ex.Message}");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method implements a two-step process: first retrieving the document data securely,
        /// then rendering it through the SPL template. All steps are logged for audit and diagnostic
        /// purposes. The method provides comprehensive error handling for both data access and
        /// template rendering failures.
        /// </remarks>
        public async Task<string> ExportDocumentToSplAsync(Guid documentGuid)
        {
            #region implementation

            // Log the start of the export process for audit and performance tracking
            _logger.LogInformation("Starting SPL export for document {DocumentGuid}", documentGuid);

            try
            {
                // Step 1: Retrieve document data securely from the database
                var documentDto = await _documentDataService.GetDocumentAsync(documentGuid);

                // Validate that the document was found and is accessible
                if (documentDto == null)
                {
                    throw new InvalidOperationException($"Document with GUID {documentGuid} not found");
                }

                // Step 2: Populate StructuredBodyView property if structured body exists
                if (documentDto.StructuredBodies != null
                    && documentDto.StructuredBodies.Any())
                {
                    foreach (var body in documentDto.StructuredBodies)
                    {
                        _logger.LogDebug("Creating structured body view model for document {DocumentGuid}", documentGuid);
                        body.StructuredBodyView = _viewModelFactory.Create(body);
                    }
                }

                // Step 3: Render the SPL template with the enhanced document data
                var xmlContent = await _templateRenderingService.RenderAsync("GenerateSpl", documentDto);

                // Log successful completion with basic metrics
                _logger.LogInformation("Successfully exported document {DocumentGuid} to SPL XML", documentGuid);

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
    }
}