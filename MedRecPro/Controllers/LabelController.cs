using MedRecPro.Controllers;
using MedRecPro.Data;
using MedRecPro.DataAccess; // From LabelDataAccess.cs (Repository)
using MedRecPro.Filters;
using MedRecPro.Helpers;   // From DtoTransformer.cs (DtoTransformer, StringCipher)
using MedRecPro.Models; // From LabelClasses.cs
using MedRecPro.Models.Extensions;
using MedRecPro.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static MedRecPro.Models.UserRole;
using Newtonsoft.Json; // From SplImportService.cs (SplImportService)
using System.Reflection;
using System.Security.Claims;

namespace MedRecPro.Api.Controllers
{
    /**************************************************************/
    /// <summary>
    /// API controller for managing Label entities based on SPL metadata.
    /// Provides dynamic CRUD endpoints for various label sections (tables).
    /// Each section is identified by a 'menuSelection' parameter in the route,
    /// corresponding to a class name nested within MedRecPro.DataModels.Label.
    /// </summary>
    /// <remarks>
    /// This controller uses reflection to dynamically determine entity types and repositories
    /// based on the menuSelection parameter. All primary keys are encrypted for security.
    /// </remarks>
    /// <example>
    /// Available sections include: Document, Organization, ActiveMoiety, Address, etc.
    /// Each section corresponds to a nested class within the Label data model.
    /// </example>
    [ApiController]
    public class LabelController : ApiControllerBase
    {
        #region Private Properties

        private const int DefaultPageNumber = 1;
        private const int DefaultPageSize = 10;


        /// <summary>
        /// Service provider for dependency injection and repository resolution
        /// </summary>
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Configuration provider for accessing application settings
        /// </summary>
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Logger instance for this controller
        /// </summary>
        private readonly ILogger<LabelController> _logger;

        /// <summary>
        /// String cipher utility for encrypting and decrypting primary keys
        /// </summary>
        private readonly StringCipher _stringCipher;

        /// <summary>
        /// Secret key used for primary key encryption, retrieved from configuration
        /// </summary>
        private readonly string _pkEncryptionSecret;

        /// <summary>
        /// Service for importing SPL data from ZIP files containing XML files.
        /// </summary>
        private readonly SplImportService _splImportService;

        private readonly ISplExportService _splExportService;

        private readonly IBackgroundTaskQueueService _queue;

        private readonly IOperationStatusStore _statusStore;

        private readonly IServiceScopeFactory _scopeFactory;

        private readonly ApplicationDbContext _dbContext;

        private readonly Service.IClaudeApiService _claudeApiService;

        /**************************************************************/
        /// <summary>
        /// Claude search service for intelligent pharmacologic class search with AI-powered
        /// terminology matching. Falls back to standard database search if unavailable.
        /// </summary>
        /// <seealso cref="IClaudeSearchService"/>
        private readonly Service.IClaudeSearchService? _claudeSearchService;

        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the LabelController with required dependencies.
        /// </summary>
        /// <param name="serviceProvider">Service provider for dependency injection</param>
        /// <param name="configuration">Configuration provider for application settings</param>
        /// <param name="logger">Logger instance for this controller</param>
        /// <param name="stringCipher">String cipher utility for encryption operations</param>
        /// <param name="splImportService">Service for importing zipped SPL files</param>
        /// <param name="queue">Service for long running background tasks</param>
        /// <param name="statusStore">Service for storing operation status</param>
        /// <param name="scopeFactory"></param>
        /// <param name="applicationDbContext"></param>
        /// <param name="splExportService"></param>
        /// <param name="claudeApiService">Service for Claude AI API calls for markdown cleanup</param>
        /// <param name="claudeSearchService">Optional service for intelligent pharmacologic class search with AI</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when PKSecret configuration is missing</exception>
        public LabelController(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<LabelController> logger,
            StringCipher stringCipher,
            SplImportService splImportService,
            IBackgroundTaskQueueService queue,
            IOperationStatusStore statusStore,
            IServiceScopeFactory scopeFactory,
            ApplicationDbContext applicationDbContext,
            ISplExportService splExportService,
            Service.IClaudeApiService claudeApiService,
            Service.IClaudeSearchService? claudeSearchService = null)
        {
            #region Implementation

            // Validate all required dependencies are provided
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stringCipher = stringCipher ?? throw new ArgumentNullException(nameof(stringCipher));
            _splImportService = splImportService ?? throw new ArgumentNullException(nameof(splImportService));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _statusStore = statusStore ?? throw new ArgumentNullException(nameof(statusStore));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _dbContext = applicationDbContext ?? throw new ArgumentNullException(nameof(applicationDbContext));

            // Retrieve and validate the primary key encryption secret from configuration
            _pkEncryptionSecret = _configuration.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing or empty.");

            _splExportService = splExportService ?? throw new ArgumentNullException(nameof(splExportService));
            _claudeApiService = claudeApiService ?? throw new ArgumentNullException(nameof(claudeApiService));

            // Optional dependency - may be null if AI service is unavailable
            _claudeSearchService = claudeSearchService;

            #endregion
        }

        #region Private Methods

        /**************************************************************/
        /// <summary>
        /// Validates pagination parameters and returns a BadRequest result if invalid.
        /// </summary>
        /// <param name="pageNumber">The page number to validate.</param>
        /// <param name="pageSize">The page size to validate.</param>
        /// <returns>BadRequest result if validation fails, null if validation passes.</returns>
        /// <remarks>
        /// Validates that:
        /// - If pageNumber is provided, it must be greater than 0
        /// - If pageSize is provided, it must be greater than 0
        /// - If one paging parameter is provided, both must be provided
        /// </remarks>
        private BadRequestObjectResult? validatePagingParameters(int? pageNumber, int? pageSize)
        {
            #region implementation

            // Validate pageNumber if provided
            if (pageNumber.HasValue && pageNumber.Value <= 0)
            {
                return BadRequest($"Invalid page number: {pageNumber.Value}. Page number must be greater than 0 if provided.");
            }

            // Validate pageSize if provided
            if (pageSize.HasValue && pageSize.Value <= 0)
            {
                return BadRequest($"Invalid page size: {pageSize.Value}. Page size must be greater than 0 if provided.");
            }

            // Enforce both or neither for paging
            if (pageNumber.HasValue != pageSize.HasValue)
            {
                return BadRequest("If providing paging, both pageNumber and pageSize must be specified.");
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds pagination response headers to the HTTP response.
        /// </summary>
        /// <param name="pageNumber">The current page number.</param>
        /// <param name="pageSize">The page size.</param>
        /// <param name="totalCount">The total count of records in the current response.</param>
        /// <remarks>
        /// Adds the following headers when pagination is applied:
        /// - X-Page-Number: The current page number
        /// - X-Page-Size: The number of records per page
        /// - X-Total-Count: The total count of records returned
        /// </remarks>
        private void addPaginationHeaders(int? pageNumber, int? pageSize, int totalCount)
        {
            #region implementation

            if (pageNumber.HasValue && pageSize.HasValue)
            {
                Response.Headers.Append("X-Page-Number", pageNumber.Value.ToString());
                Response.Headers.Append("X-Page-Size", pageSize.Value.ToString());
                Response.Headers.Append("X-Total-Count", totalCount.ToString());
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines if the request is from a browser attempting to view the XML directly.
        /// </summary>
        /// <param name="request">The HTTP request to analyze.</param>
        /// <returns>True if request is from a browser for direct viewing, false otherwise.</returns>
        /// <remarks>
        /// Checks Accept header for text/html preference, indicating browser navigation.
        /// Download requests and API calls typically use application/xml or */*.
        /// </remarks>
        /// <seealso cref="GenerateXmlDocument"/>
        private bool isBrowserViewRequest(HttpRequest request)
        {
            #region implementation

            // Check if Accept header prefers HTML (browser navigation)
            var acceptHeader = request.Headers["Accept"].ToString();

            // Browser direct navigation typically includes text/html in Accept header
            // Swagger downloads and validation tools use application/xml or */*
            if (acceptHeader.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Additional check: query string parameter for explicit control
            if (request.Query.ContainsKey("view") &&
                request.Query["view"].ToString().Equals("browser", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Ensures the XML declaration specifies UTF-8 encoding as required by FDA specification.
        /// </summary>
        /// <param name="xmlContent">The XML content to process.</param>
        /// <returns>XML content with corrected encoding declaration.</returns>
        /// <remarks>
        /// FDA SPL specification 2.1.2.1 requires UTF-8 encoding.
        /// Replaces any other encoding declarations (e.g., UTF-16) with UTF-8.
        /// </remarks>
        /// <seealso cref="GenerateXmlDocument"/>
        private string ensureUtf8Encoding(string xmlContent)
        {
            #region implementation

            if (!string.IsNullOrWhiteSpace(xmlContent))
            {
                // Replace any encoding declaration with UTF-8
                if (xmlContent.Contains("encoding=\"UTF-16\"", StringComparison.OrdinalIgnoreCase))
                {
                    xmlContent = xmlContent.Replace(
                        "encoding=\"UTF-16\"",
                        "encoding=\"UTF-8\"",
                        StringComparison.OrdinalIgnoreCase);
                }
                else if (xmlContent.Contains("encoding=\"utf-16\"", StringComparison.OrdinalIgnoreCase))
                {
                    xmlContent = xmlContent.Replace(
                        "encoding=\"utf-16\"",
                        "encoding=\"UTF-8\"",
                        StringComparison.OrdinalIgnoreCase);
                }

                return xmlContent.Trim(); 
            }
            
            return xmlContent;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts FDA resource URLs to local server URLs for browser viewing.
        /// </summary>
        /// <param name="xmlContent">The XML content with FDA URLs.</param>
        /// <returns>XML content with local resource URLs.</returns>
        /// <remarks>
        /// Replaces FDA stylesheet URL with local path to avoid CORS issues.
        /// Schema location remains unchanged as browsers don't fetch it.
        /// Only modifies the xml-stylesheet processing instruction.
        /// </remarks>
        /// <seealso cref="GenerateXmlDocument"/>
        /// <seealso cref="isBrowserViewRequest"/>
        private string convertToLocalResources(string xmlContent)
        {
            #region implementation

            // Construct base URL from current request
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";

            // Replace FDA stylesheet URL with local path
#if DEBUG
            xmlContent = xmlContent.Replace(
                "href=\"https://www.accessdata.fda.gov/spl/stylesheet/spl.xsl\"",
                "href=\"/stylesheets/spl.xsl\"");

            // Note: Schema location is NOT replaced - browsers don't fetch XSD files
            // The xsi:schemaLocation remains as FDA URL for validation purposes
#else
            xmlContent = xmlContent.Replace(
               "href=\"https://www.accessdata.fda.gov/spl/stylesheet/spl.xsl\"",
               $"href=\"{baseUrl}/api/stylesheets/spl.xsl\"");

            // Note: Schema location is NOT replaced - browsers don't fetch XSD files
            // The xsi:schemaLocation remains as FDA URL for validation purposes
#endif
            return xmlContent;

#endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves the entity type based on the menu selection parameter using reflection.
        /// </summary>
        /// <param name="menuSelection">The name of the nested class within Label to resolve</param>
        /// <returns>The Type representing the nested class, or null if not found</returns>
        /// <remarks>
        /// Uses reflection to find public instance nested types within the Label class.
        /// The menuSelection should match exactly with a nested class name.
        /// </remarks>
        private Type? getEntityType(string menuSelection)
        {
            #region Implementation

            // Return null for empty or whitespace menu selections
            if (string.IsNullOrWhiteSpace(menuSelection)) return null;

            // Use reflection to find the nested type within Label class
            // Assumes menuSelection is a direct nested class name within Label
            return typeof(Label).GetNestedType(menuSelection, BindingFlags.Public | BindingFlags.Instance);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates and resolves a generic repository instance for the specified entity type.
        /// </summary>
        /// <param name="entityType">The entity type for which to create a repository</param>
        /// <returns>An instance of Repository&lt;T&gt; for the specified type</returns>
        /// <exception cref="InvalidOperationException">Thrown when repository cannot be resolved</exception>
        /// <remarks>
        /// Uses the service provider to resolve a Repository&lt;T&gt; instance.
        /// The repository and its dependencies must be properly registered in DI container.
        /// </remarks>
        private object getRepository(Type entityType)
        {
            #region Implementation

            // Create the generic repository type for the specific entity
            var repoType = typeof(Repository<>).MakeGenericType(entityType);

            // Attempt to resolve the repository from the service container
            var repo = _serviceProvider.GetService(repoType);

            // Validate that repository was successfully resolved
            if (repo == null)
            {
                var errorMsg = $"Could not resolve repository for type {entityType.FullName}. Ensure it and its dependencies (DbContext, ILogger<{entityType.Name}>, IConfiguration, StringCipher) are registered.";
                _logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            return repo;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Identifies the primary key property for an entity type using naming conventions.
        /// </summary>
        /// <param name="entityType">The entity type to analyze for primary key property</param>
        /// <returns>PropertyInfo for the primary key, or null if not found</returns>
        /// <remarks>
        /// Attempts to find primary key using conventions in this order:
        /// 1. {EntityName}ID (e.g., DocumentID)
        /// 2. {EntityName}Id (e.g., DocumentId) 
        /// 3. Id (case-insensitive)
        /// Does not use EF Core metadata unlike Repository constructor.
        /// </remarks>
        private PropertyInfo? getPrimaryKeyProperty(Type entityType)
        {
            #region Implementation

            // Try convention 1: {EntityName}ID
            string pkNameConvention1 = entityType.Name + "ID";
            var pkProperty = entityType.GetProperty(pkNameConvention1, BindingFlags.Public | BindingFlags.Instance);

            // Try convention 2: {EntityName}Id if first convention failed
            if (pkProperty == null)
            {
                string pkNameConvention2 = entityType.Name + "Id";
                pkProperty = entityType.GetProperty(pkNameConvention2, BindingFlags.Public | BindingFlags.Instance);
            }

            // Try convention 3: Generic "Id" property (case-insensitive) if previous failed
            if (pkProperty == null)
            {
                pkProperty = entityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            }

            // Log warning if no primary key property could be identified
            // Note: This helper does not use EF Core metadata to find PKs if conventions fail,
            // unlike the Repository's constructor. 
            if (pkProperty == null)
            {
                _logger.LogWarning($"Could not find PK property for type {entityType.Name} using conventions ('{pkNameConvention1}', '{entityType.Name + "Id"}', 'Id').");
            }

            return pkProperty;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to decrypt an encrypted primary key value and convert it to the appropriate type.
        /// </summary>
        /// <param name="encryptedPk">The encrypted primary key string to decrypt</param>
        /// <param name="pkPropertyType">The target type for the decrypted primary key</param>
        /// <param name="decryptedPkValue">Output parameter containing the decrypted and converted value</param>
        /// <returns>True if decryption and conversion succeeded, false otherwise</returns>
        /// <remarks>
        /// Supports int and long primary key types (including nullable versions).
        /// Uses the configured StringCipher and PKSecret for decryption.
        /// Logs warnings for unsupported types or parsing failures.
        /// </remarks>
        private bool tryDecryptPk(string? encryptedPk, Type pkPropertyType, out object? decryptedPkValue)
        {
            #region Implementation

            decryptedPkValue = null;

            // Skip processing for null or empty encrypted values
            if (string.IsNullOrWhiteSpace(encryptedPk))
            {
                _logger.LogTrace("Encrypted PK is null or whitespace, decryption skipped.");
                return false;
            }

            try
            {
                // Decrypt the primary key using the configured cipher and secret
                string decryptedString = _stringCipher.Decrypt(encryptedPk, _pkEncryptionSecret);

                // Handle nullable types by getting the underlying type
                Type underlyingPkType = Nullable.GetUnderlyingType(pkPropertyType) ?? pkPropertyType;

                // Convert decrypted string to appropriate primary key type
                if (underlyingPkType == typeof(int))
                {
                    if (int.TryParse(decryptedString, out int idVal))
                    {
                        decryptedPkValue = idVal;
                        return true;
                    }
                }
                else if (underlyingPkType == typeof(long))
                {
                    if (long.TryParse(decryptedString, out long idVal))
                    {
                        decryptedPkValue = idVal;
                        return true;
                    }
                }
                // Add other supported PK types if necessary (e.g., Guid, string)
                else
                {
                    _logger.LogWarning($"Unsupported PK type for decryption: {underlyingPkType.Name}. Decrypted string was: '{decryptedString}'.");
                    return false;
                }

                // Log failure to parse decrypted value
                _logger.LogWarning($"Failed to parse decrypted PK string '{decryptedString}' to type {underlyingPkType.Name}.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting PK. Encrypted value: {EncryptedValue}", encryptedPk);
                return false;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the current user ID from the HTTP context.
        /// This method should be called while the HTTP context is still available.
        /// </summary>
        /// <returns>The current user's ID if authenticated; otherwise, null.</returns>
        private long? getCurrentUserId()
        {
            try
            {
                if (HttpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (long.TryParse(userIdClaim, out long userId))
                    {
                        return userId;
                    }
                }
                return null; // No authenticated user or invalid user ID
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get current user ID from context");
                return null;
            }
        }

        /**************************************************************/
        /// <summary>
        /// Creates the initial status object for a new comparison operation.
        /// </summary>
        /// <param name="operationId">The unique operation identifier</param>
        /// <param name="documentGuid">The document GUID being analyzed</param>
        /// <param name="progressUrl">The URL for progress monitoring</param>
        /// <returns>A new ComparisonOperationStatus with initial values</returns>
        /// <seealso cref="ComparisonOperationStatus"/>
        /// <seealso cref="Label"/>
        private ComparisonOperationStatus createInitialComparisonStatus(string operationId, Guid documentGuid, string? progressUrl)
        {
            #region implementation
            return new ComparisonOperationStatus
            {
                OperationId = operationId,
                DocumentGuid = documentGuid,
                Status = ComparisonConstants.STATUS_QUEUED,
                PercentComplete = ComparisonConstants.PROGRESS_QUEUED,
                ProgressUrl = progressUrl,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds response headers for tracking comparison analysis operations.
        /// </summary>
        /// <param name="documentGuid">The document GUID being analyzed</param>
        /// <param name="operationId">The unique operation identifier</param>
        /// <param name="isAsynchronous">Whether this is an asynchronous operation</param>
        /// <seealso cref="Label"/>
        private void addComparisonResponseHeaders(Guid documentGuid, string operationId, bool isAsynchronous = true)
        {
            #region implementation
            Response.Headers.Append(ComparisonConstants.HEADER_DOCUMENT_GUID, documentGuid.ToString());
            Response.Headers.Append(ComparisonConstants.HEADER_OPERATION_ID, operationId);
            Response.Headers.Append(ComparisonConstants.HEADER_ANALYSIS_TYPE, ComparisonConstants.ANALYSIS_TYPE_DOCUMENT_COMPARISON);
            Response.Headers.Append(ComparisonConstants.HEADER_ANALYSIS_METHOD,
                isAsynchronous ? ComparisonConstants.ANALYSIS_METHOD_ASYNCHRONOUS : ComparisonConstants.ANALYSIS_METHOD_SYNCHRONOUS);
            Response.Headers.Append(ComparisonConstants.HEADER_ANALYSIS_TIMESTAMP, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Updates the comparison operation status with new information using extension methods.
        /// </summary>
        /// <param name="operationId">The unique operation identifier</param>
        /// <param name="status">The status message</param>
        /// <param name="percentComplete">The completion percentage</param>
        /// <param name="progressUrl">The progress monitoring URL</param>
        /// <param name="documentGuid">The document GUID being analyzed</param>
        /// <param name="result">The analysis result when complete</param>
        /// <param name="error">Any error message</param>
        /// <remarks>
        /// Uses the generic extension method to store ComparisonOperationStatus while maintaining
        /// backward compatibility with the strongly-typed IOperationStatusStore interface.
        /// </remarks>
        /// <seealso cref="ComparisonOperationStatus"/>
        /// <seealso cref="OperationStatusStoreExtensions"/>
        /// <seealso cref="Label"/>
        private void updateComparisonStatus(
            string operationId,
            string status,
            int percentComplete,
            string? progressUrl,
            Guid documentGuid,
            DocumentComparisonResult? result = null,
            string? error = null)
        {
            #region implementation
            var comparisonStatus = new ComparisonOperationStatus
            {
                OperationId = operationId,
                DocumentGuid = documentGuid,
                Status = status,
                PercentComplete = percentComplete,
                ProgressUrl = progressUrl,
                Result = result,
                Error = error,
                UpdatedAt = DateTime.UtcNow
            };

            // Preserve creation time if status exists using generic extension method
            if (_statusStore.TryGet<ComparisonOperationStatus>(operationId, out ComparisonOperationStatus? existingStatus))
            {
                comparisonStatus.CreatedAt = existingStatus.CreatedAt;
            }

            // Use generic extension method to store the status
            _statusStore.Set(operationId, comparisonStatus);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Executes the comparison analysis operation in the background using a scoped service provider.
        /// </summary>
        /// <param name="operationId">The unique operation identifier</param>
        /// <param name="documentGuid">The document GUID to analyze</param>
        /// <param name="progressUrl">The progress monitoring URL</param>
        /// <param name="cancellationToken">Cancellation token for the operation</param>
        /// <remarks>
        /// Creates a new service scope for background processing to ensure proper dependency injection
        /// container lifecycle management. This prevents "ObjectDisposedException" errors that occur
        /// when accessing services from a disposed scope in background tasks.
        /// </remarks>
        /// <seealso cref="IComparisonService"/>
        /// <seealso cref="DocumentComparisonResult"/>
        /// <seealso cref="IServiceScopeFactory"/>
        /// <seealso cref="Label"/>
        private async Task executeComparisonAnalysisAsync(
            string operationId,
            Guid documentGuid,
            string? progressUrl,
            CancellationToken cancellationToken)
        {
            #region implementation
            try
            {
                // Update status to indicate processing has started
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_PROCESSING,
                    ComparisonConstants.PROGRESS_PROCESSING_STARTED, progressUrl, documentGuid);

                _logger.LogInformation("Starting background comparison analysis for document {DocumentGuid}, operation {OperationId}",
                    documentGuid, operationId);

                // Create a new scope for background processing to avoid disposed context issues
                using var scope = _scopeFactory.CreateScope();
                var comparisonService = scope.ServiceProvider.GetRequiredService<IComparisonService>();

                // Update progress during analysis
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_ANALYZING,
                    ComparisonConstants.PROGRESS_ANALYZING, progressUrl, documentGuid);

                var analysisResult = await comparisonService.GenerateDocumentComparisonAsync(documentGuid);

                // Update progress as analysis completes
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_FINALIZING,
                    ComparisonConstants.PROGRESS_FINALIZING, progressUrl, documentGuid);

                // Mark operation as completed and store results
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_COMPLETED,
                    ComparisonConstants.PROGRESS_COMPLETED, progressUrl, documentGuid, analysisResult);

                _logger.LogInformation("Successfully completed background comparison analysis for document {DocumentGuid}, operation {OperationId}",
                    documentGuid, operationId);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_CANCELED,
                    ComparisonConstants.PROGRESS_QUEUED, progressUrl, documentGuid);
                _logger.LogInformation("Comparison analysis was canceled for document {DocumentGuid}, operation {OperationId}",
                    documentGuid, operationId);
            }
            catch (ArgumentException ex)
            {
                // Handle validation errors
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_FAILED,
                    ComparisonConstants.PROGRESS_QUEUED, progressUrl, documentGuid, error: ex.Message);
                _logger.LogWarning(ex, "Invalid argument during comparison analysis for document {DocumentGuid}, operation {OperationId}",
                    documentGuid, operationId);
            }
            catch (InvalidOperationException ex)
            {
                // Handle business logic errors
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_FAILED,
                    ComparisonConstants.PROGRESS_QUEUED, progressUrl, documentGuid, error: ex.Message);
                _logger.LogWarning(ex, "Invalid operation during comparison analysis for document {DocumentGuid}, operation {OperationId}",
                    documentGuid, operationId);
            }
            catch (Exception ex)
            {
                // Handle any unexpected processing errors
                updateComparisonStatus(operationId, ComparisonConstants.STATUS_FAILED,
                    ComparisonConstants.PROGRESS_QUEUED, progressUrl, documentGuid,
                    error: ComparisonConstants.ERROR_ANALYSIS_FAILED);
                _logger.LogError(ex, "Unexpected error during comparison analysis for document {DocumentGuid}, operation {OperationId}",
                    documentGuid, operationId);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the document GUID parameter and returns appropriate error response if invalid.
        /// </summary>
        /// <param name="documentGuid">The document GUID to validate</param>
        /// <returns>BadRequest result if invalid, null if valid</returns>
        /// <seealso cref="Label"/>
        private ActionResult? validateDocumentGuid(Guid documentGuid)
        {
            #region implementation
            if (documentGuid.IsNullOrEmpty())
            {
                _logger.LogWarning("Invalid empty GUID provided for document comparison analysis");
                return BadRequest(ComparisonConstants.ERROR_EMPTY_DOCUMENT_GUID);
            }
            return null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates the operation ID parameter and returns appropriate error response if invalid.
        /// </summary>
        /// <param name="operationId">The operation ID to validate</param>
        /// <returns>BadRequest result if invalid, null if valid</returns>
        /// <seealso cref="Label"/>
        private ActionResult? validateOperationId(string operationId)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(operationId))
            {
                return BadRequest(ComparisonConstants.ERROR_EMPTY_OPERATION_ID);
            }
            return null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the encrypted user ID for the current authenticated user.
        /// Used for building system context for AI-powered operations.
        /// </summary>
        /// <returns>Encrypted user ID string, or null if not authenticated.</returns>
        /// <seealso cref="StringCipher.Encrypt"/>
        private string? getEncryptedUserId()
        {
            #region implementation

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return null;
            }

            // Encrypt the user ID for external use
            return StringCipher.Encrypt(userIdClaim, _pkEncryptionSecret, StringCipher.EncryptionStrength.Fast);

            #endregion
        }

        #endregion Private Methods

        #region Application Number Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by regulatory application number (NDA, ANDA, BLA).
        /// Returns products sharing the same regulatory approval with navigation data.
        /// </summary>
        /// <param name="applicationNumber">
        /// The application number to search for (e.g., "NDA014526", "ANDA125669", "BLA103795").
        /// Supports flexible matching including exact match, prefix-only, and number-only searches.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// If provided, pageSize must also be provided for paging to apply.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// If provided, pageNumber must also be provided for paging to apply.
        /// </param>
        /// <returns>List of products matching the application number criteria with navigation data.</returns>
        /// <response code="200">Returns the list of products matching the application number.</response>
        /// <response code="400">If the applicationNumber is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/application-number/search?applicationNumber=NDA014526
        /// GET /api/Label/application-number/search?applicationNumber=ANDA&amp;pageNumber=1&amp;pageSize=25
        /// 
        /// The search supports multiple matching strategies:
        /// - Exact match after normalization (e.g., "ANDA125669" == "ANDA125669")
        /// - Prefix-only search (e.g., "ANDA" matches all ANDA applications)
        /// - Number-only search (e.g., "125669" matches "ANDA125669")
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "LIPITOR",
        ///     "ApplicationNumber": "NDA020702",
        ///     "MarketingCategoryCode": "NDA"
        ///   }
        /// ]
        /// ```
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search for all products under NDA014526
        /// GET /api/Label/application-number/search?applicationNumber=NDA014526
        /// 
        /// // Search for all ANDA products with pagination
        /// GET /api/Label/application-number/search?applicationNumber=ANDA&amp;pageNumber=1&amp;pageSize=50
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchByApplicationNumberAsync"/>
        /// <seealso cref="LabelView.ProductsByApplicationNumber"/>
        /// <seealso cref="Label.MarketingCategory"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("application-number/search")]
        [ProducesResponseType(typeof(IEnumerable<ProductsByApplicationNumberDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductsByApplicationNumberDto>>> SearchByApplicationNumber(
            [FromQuery] string applicationNumber,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate application number is provided
            if (string.IsNullOrWhiteSpace(applicationNumber))
            {
                return BadRequest("Application number is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching products by application number: {ApplicationNumber}, Page: {PageNumber}, Size: {PageSize}",
                    applicationNumber, pageNumber, pageSize);

                var results = await DtoLabelAccess.SearchByApplicationNumberAsync(
                    _dbContext,
                    applicationNumber,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products by application number {ApplicationNumber}", applicationNumber);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while searching products by application number.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets aggregated summaries of application numbers with product and document counts.
        /// Useful for understanding the scope of regulatory approvals across the database.
        /// </summary>
        /// <param name="marketingCategory">
        /// Optional filter by marketing category code (e.g., "NDA", "ANDA", "BLA").
        /// If not provided, returns summaries for all marketing categories.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of application number summaries with aggregated counts.</returns>
        /// <response code="200">Returns the list of application number summaries.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/application-number/summaries
        /// GET /api/Label/application-number/summaries?marketingCategoryCode=NDA
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "ApplicationNumber": "NDA020702",
        ///     "MarketingCategoryCode": "NDA",
        ///     "ProductCount": 15,
        ///     "DocumentCount": 45
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by ProductCount in descending order.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get all application number summaries
        /// GET /api/Label/application-number/summaries
        /// 
        /// // Get only NDA summaries
        /// GET /api/Label/application-number/summaries?marketingCategoryCode=NDA
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.GetApplicationNumberSummariesAsync"/>
        /// <seealso cref="LabelView.ApplicationNumberSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("application-number/summaries")]
        [ProducesResponseType(typeof(IEnumerable<ApplicationNumberSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ApplicationNumberSummaryDto>>> GetApplicationNumberSummaries(
            [FromQuery] string? marketingCategory,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting application number summaries. Category: {MarketingCategoryCode}, Page: {PageNumber}, Size: {PageSize}",
                    marketingCategory ?? "all", pageNumber, pageSize);

                var results = await DtoLabelAccess.GetApplicationNumberSummariesAsync(
                    _dbContext,
                    marketingCategory,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving application number summaries");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving application number summaries.");
            }

            #endregion
        }

        #endregion Application Number Navigation

        #region Pharmacologic Class Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by pharmacologic/therapeutic class using intelligent terminology matching.
        /// This endpoint solves the vocabulary mismatch problem where user queries (e.g., "beta blockers")
        /// differ from database class names (e.g., "Beta-Adrenergic Blockers [EPC]").
        /// </summary>
        /// <param name="query">
        /// Natural language query for AI-powered intelligent search.
        /// Examples: "beta blockers", "ACE inhibitors", "SSRIs", "statins"
        /// When provided, AI matches user terminology to actual database class names.
        /// </param>
        /// <param name="classNameSearch">
        /// Direct search term to match against pharmacologic class names.
        /// Supports partial matching for flexible searches. Used as fallback when AI is unavailable
        /// or when direct class name matching is preferred.
        /// </param>
        /// <param name="maxProductsPerClass">
        /// Maximum number of products to return per matched class when using AI search. Default is 500.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve (used with classNameSearch).
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page (used with classNameSearch).
        /// </param>
        /// <returns>
        /// When using `query`: Returns a <see cref="PharmacologicClassSearchResult"/> with matched classes,
        /// products organized by class, and label links.
        /// When using `classNameSearch`: Returns list of products matching the therapeutic class criteria.
        /// </returns>
        /// <response code="200">Returns the search results with products and label links.</response>
        /// <response code="400">If neither query nor classNameSearch is provided, or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// **AI-Powered Search (Recommended):**
        ///
        /// GET /api/Label/pharmacologic-class/search?query=beta blockers
        ///
        /// **Workflow:**
        /// 1. Retrieves all pharmacologic classes from the database
        /// 2. Uses AI to match user terminology to actual database class names
        /// 3. Searches for products in each matched class
        /// 4. Returns consolidated results with label links
        ///
        /// **Example Response (AI Search):**
        /// ```json
        /// {
        ///   "success": true,
        ///   "originalQuery": "beta blockers",
        ///   "matchedClasses": ["Beta-Adrenergic Blockers [EPC]"],
        ///   "productsByClass": {
        ///     "Beta-Adrenergic Blockers [EPC]": [
        ///       {
        ///         "productName": "METOPROLOL TARTRATE",
        ///         "documentGuid": "abc-123-def",
        ///         "activeIngredient": "Metoprolol Tartrate"
        ///       }
        ///     ]
        ///   },
        ///   "totalProductCount": 47,
        ///   "labelLinks": {
        ///     "View Full Label (METOPROLOL TARTRATE)": "/api/Label/generate/abc-123-def/true"
        ///   }
        /// }
        /// ```
        ///
        /// **Direct Database Search (Legacy):**
        ///
        /// GET /api/Label/pharmacologic-class/search?classNameSearch=Beta-Blocker
        ///
        /// Response returns raw product DTOs ordered by PharmClassName, then ProductName.
        /// </remarks>
        /// <example>
        /// <code>
        /// // AI-powered search (recommended)
        /// GET /api/Label/pharmacologic-class/search?query=beta%20blockers
        /// GET /api/Label/pharmacologic-class/search?query=ACE%20inhibitors&amp;maxProductsPerClass=10
        ///
        /// // Direct database search (legacy)
        /// GET /api/Label/pharmacologic-class/search?classNameSearch=Beta-Blocker
        /// GET /api/Label/pharmacologic-class/search?classNameSearch=ACE&amp;pageNumber=1&amp;pageSize=25
        /// </code>
        /// </example>
        /// <seealso cref="IClaudeSearchService"/>
        /// <seealso cref="PharmacologicClassSearchResult"/>
        /// <seealso cref="DtoLabelAccess.SearchByPharmacologicClassAsync"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("pharmacologic-class/search")]
        [ProducesResponseType(typeof(PharmacologicClassSearchResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(IEnumerable<ProductsByPharmacologicClassDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> SearchByPharmacologicClass(
            [FromQuery] string? query,
            [FromQuery] string? classNameSearch,
            [FromQuery] int maxProductsPerClass = 500,
            [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null)
        {
            #region Input Validation

            // Validate that at least one search parameter is provided
            if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(classNameSearch))
            {
                return BadRequest("Either 'query' (for AI search) or 'classNameSearch' (for direct search) is required.");
            }

            // Validate paging parameters when using classNameSearch
            if (!string.IsNullOrWhiteSpace(classNameSearch))
            {
                var pagingValidation = validatePagingParameters(pageNumber, pageSize);
                if (pagingValidation != null)
                {
                    return pagingValidation;
                }
            }

            #endregion

            #region AI-Powered Search

            // If query parameter is provided, attempt AI-powered search first
            if (!string.IsNullOrWhiteSpace(query))
            {
                // Try AI-powered search if service is available
                if (_claudeSearchService != null)
                {
                    try
                    {
                        _logger.LogInformation("[Label] AI pharmacologic class search for: {Query}", query);

                        // Build system context
                        var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
                        var userId = isAuthenticated ? getEncryptedUserId() : null;
                        var systemContext = await _claudeApiService.GetSystemContextAsync(isAuthenticated, userId);

                        // Execute the intelligent search
                        var result = await _claudeSearchService.SearchByUserQueryAsync(
                            query,
                            systemContext,
                            maxProductsPerClass);

                        return Ok(result);
                    }
                    catch (Exception ex)
                    {
                        // Log and fall through to legacy search
                        _logger.LogWarning(ex, "AI pharmacologic class search failed, falling back to database search: {Query}", query);
                    }
                }
                else
                {
                    _logger.LogDebug("AI search service unavailable, using database search for: {Query}", query);
                }

                // Fall back to using query as classNameSearch
                classNameSearch = query;
            }

            #endregion

            #region Database Search (Fallback/Legacy)

            try
            {
                _logger.LogInformation("Searching products by pharmacologic class: {ClassNameSearch}, Page: {PageNumber}, Size: {PageSize}",
                    classNameSearch, pageNumber, pageSize);

                var results = await DtoLabelAccess.SearchByPharmacologicClassAsync(
                    _dbContext,
                    classNameSearch!,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument for pharmacologic class search: {ClassNameSearch}", classNameSearch);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products by pharmacologic class {ClassNameSearch}", classNameSearch);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while searching products by pharmacologic class.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the pharmacologic class hierarchy showing parent-child relationships.
        /// Enables navigation through therapeutic classification levels.
        /// </summary>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of pharmacologic class hierarchy relationships.</returns>
        /// <response code="200">Returns the pharmacologic class hierarchy.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/pharmacologic-class/hierarchy
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "ParentClassID": "encrypted_string",
        ///     "ParentClassName": "Cardiovascular Agents",
        ///     "ChildClassID": "encrypted_string",
        ///     "ChildClassName": "Beta-Adrenergic Blockers"
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by ParentClassName, then ChildClassName.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetPharmacologicClassHierarchyAsync"/>
        /// <seealso cref="LabelView.PharmacologicClassHierarchy"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("pharmacologic-class/hierarchy")]
        [ProducesResponseType(typeof(IEnumerable<PharmacologicClassHierarchyViewDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<PharmacologicClassHierarchyViewDto>>> GetPharmacologicClassHierarchy(
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting pharmacologic class hierarchy. Page: {PageNumber}, Size: {PageSize}",
                    pageNumber, pageSize);

                var results = await DtoLabelAccess.GetPharmacologicClassHierarchyAsync(
                    _dbContext,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pharmacologic class hierarchy");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving pharmacologic class hierarchy.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Lists all available pharmacologic classes with product counts.
        /// This endpoint is useful for browsing available drug categories and
        /// understanding the classification structure in the database.
        /// </summary>
        /// <param name="useAiCache">
        /// Optional. When true, uses AI service's cached summaries which are optimized
        /// for intelligent search operations. Default is false for backwards compatibility.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>
        /// A list of <see cref="PharmacologicClassSummaryDto"/> containing class names
        /// and product counts for classes that have associated products.
        /// </returns>
        /// <response code="200">Returns the list of pharmacologic classes.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/pharmacologic-class/summaries
        ///
        /// Returns all pharmacologic classes that have at least one associated product.
        /// Results are cached to optimize performance on repeated queries.
        ///
        /// **Example Response:**
        /// ```json
        /// [
        ///   {
        ///     "pharmClassName": "Beta-Adrenergic Blockers [EPC]",
        ///     "productCount": 47
        ///   },
        ///   {
        ///     "pharmClassName": "Angiotensin Converting Enzyme Inhibitors [EPC]",
        ///     "productCount": 32
        ///   }
        /// ]
        /// ```
        ///
        /// **Use Cases:**
        /// - Browse available drug categories before searching
        /// - Provide suggestions when no match is found in class search
        /// - Display classification hierarchy to users
        /// </remarks>
        /// <seealso cref="IClaudeSearchService"/>
        /// <seealso cref="DtoLabelAccess.GetPharmacologicClassSummariesAsync"/>
        /// <seealso cref="LabelView.PharmacologicClassSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("pharmacologic-class/summaries")]
        [ProducesResponseType(typeof(List<PharmacologicClassSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<PharmacologicClassSummaryDto>>> GetPharmacologicClassSummaries(
            [FromQuery] bool useAiCache = false,
            [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region AI Service (Cached Summaries)

            // Try AI service's cached summaries first if requested and available
            if (useAiCache && _claudeSearchService != null)
            {
                try
                {
                    _logger.LogDebug("[Label] Retrieving pharmacologic class summaries from AI cache");

                    var summaries = await _claudeSearchService.GetAllClassSummariesAsync();

                    // Apply pagination if requested
                    if (pageNumber.HasValue && pageSize.HasValue)
                    {
                        var skip = (pageNumber.Value - 1) * pageSize.Value;
                        var paged = summaries.Skip(skip).Take(pageSize.Value).ToList();
                        addPaginationHeaders(pageNumber, pageSize, paged.Count);
                        return Ok(paged);
                    }

                    return Ok(summaries);
                }
                catch (Exception ex)
                {
                    // Log and fall through to database query
                    _logger.LogWarning(ex, "AI cache retrieval failed, falling back to database query");
                }
            }

            #endregion

            #region Database Query (Default/Fallback)

            try
            {
                _logger.LogInformation("Getting pharmacologic class summaries. Page: {PageNumber}, Size: {PageSize}",
                    pageNumber, pageSize);

                var results = await DtoLabelAccess.GetPharmacologicClassSummariesAsync(
                    _dbContext,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pharmacologic class summaries");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving pharmacologic class summaries.");
            }

            #endregion
        }

        #endregion Pharmacologic Class Navigation

        #region Ingredient Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by ingredient UNII code or substance name.
        /// Enables drug composition queries and ingredient-based product discovery.
        /// </summary>
        /// <param name="unii">
        /// Optional UNII (Unique Ingredient Identifier) code to search for (e.g., "R16CO5Y76E" for aspirin).
        /// </param>
        /// <param name="substanceNameSearch">
        /// Optional substance name search term. Supports partial matching.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of products matching the ingredient criteria.</returns>
        /// <response code="200">Returns the list of products matching the ingredient.</response>
        /// <response code="400">If neither unii nor substanceNameSearch is provided, or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/ingredient/search?unii=R16CO5Y76E
        /// GET /api/Label/ingredient/search?substanceNameSearch=aspirin
        /// 
        /// At least one of `unii` or `substanceNameSearch` must be provided.
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "BAYER ASPIRIN",
        ///     "UNII": "R16CO5Y76E",
        ///     "SubstanceName": "ASPIRIN"
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by SubstanceName, then ProductName.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search by UNII code
        /// GET /api/Label/ingredient/search?unii=R16CO5Y76E
        /// 
        /// // Search by substance name
        /// GET /api/Label/ingredient/search?substanceNameSearch=aspirin
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchByIngredientAsync"/>
        /// <seealso cref="LabelView.ProductsByIngredient"/>
        /// <seealso cref="Label.Ingredient"/>
        /// <seealso cref="Label.IngredientSubstance"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/search")]
        [ProducesResponseType(typeof(IEnumerable<ProductsByIngredientDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductsByIngredientDto>>> SearchByIngredient(
            [FromQuery] string? unii,
            [FromQuery] string? substanceNameSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate at least one search parameter is provided
            if (string.IsNullOrWhiteSpace(unii) && string.IsNullOrWhiteSpace(substanceNameSearch))
            {
                return BadRequest("At least one search parameter (unii or substanceNameSearch) is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching products by ingredient. UNII: {UNII}, SubstanceName: {SubstanceName}, Page: {PageNumber}, Size: {PageSize}",
                    unii ?? "null", substanceNameSearch ?? "null", pageNumber, pageSize);

                var results = await DtoLabelAccess.SearchByIngredientAsync(
                    _dbContext,
                    unii,
                    substanceNameSearch,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products by ingredient. UNII: {UNII}, SubstanceName: {SubstanceName}",
                    unii ?? "null", substanceNameSearch ?? "null");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while searching products by ingredient.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets ingredient summaries with product counts.
        /// Discover the most common ingredients across products in the database.
        /// </summary>
        /// <param name="ingredient">
        /// Optional. Filter by ingredient name (partial match on SubstanceName).
        /// Use this to vary results for AI skill discovery workflows.
        /// </param>
        /// <param name="minProductCount">
        /// Optional minimum product count filter. Only returns ingredients appearing in at least this many products.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of ingredient summaries with aggregated product counts.</returns>
        /// <response code="200">Returns the ingredient summaries.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/ingredient/summaries
        /// GET /api/Label/ingredient/summaries?minProductCount=10
        /// GET /api/Label/ingredient/summaries?ingredient=aspirin
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "UNII": "R16CO5Y76E",
        ///     "SubstanceName": "ASPIRIN",
        ///     "ProductCount": 250
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by ProductCount in descending order.
        /// The ingredient parameter enables varied results for paginated queries.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetIngredientSummariesAsync"/>
        /// <seealso cref="LabelView.IngredientSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/summaries")]
        [ProducesResponseType(typeof(IEnumerable<IngredientSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<IngredientSummaryDto>>> GetIngredientSummaries(
            [FromQuery] string? ingredient,
            [FromQuery] int? minProductCount,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate minProductCount if provided
            if (minProductCount.HasValue && minProductCount.Value < 0)
            {
                return BadRequest("Minimum product count cannot be negative.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting ingredient summaries. Ingredient: {Ingredient}, MinProductCount: {MinProductCount}, Page: {PageNumber}, Size: {PageSize}",
                    ingredient ?? "null", minProductCount, pageNumber, pageSize);

                var results = await DtoLabelAccess.GetIngredientSummariesAsync(
                    _dbContext,
                    minProductCount,
                    ingredient,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ingredient summaries");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving ingredient summaries.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets active ingredient summaries with product, document, and labeler counts.
        /// Discover the most common active ingredients across products in the database.
        /// </summary>
        /// <param name="ingredient">
        /// Optional. Filter by ingredient name (partial match on SubstanceName).
        /// Use this to vary results for AI skill discovery workflows.
        /// </param>
        /// <param name="minProductCount">
        /// Optional minimum product count filter. Only returns ingredients appearing in at least this many products.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of active ingredient summaries with aggregated counts.</returns>
        /// <response code="200">Returns the active ingredient summaries.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/ingredient/active/summaries
        /// GET /api/Label/ingredient/active/summaries?minProductCount=10
        /// GET /api/Label/ingredient/active/summaries?ingredient=aspirin
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedIngredientSubstanceID": "encrypted_string",
        ///     "UNII": "R16CO5Y76E",
        ///     "SubstanceName": "ASPIRIN",
        ///     "IngredientType": "activeIngredient",
        ///     "ProductCount": 250,
        ///     "DocumentCount": 180,
        ///     "LabelerCount": 45
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by ProductCount in descending order.
        /// The ingredient parameter enables varied results for paginated queries.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetIngredientActiveSummariesAsync"/>
        /// <seealso cref="LabelView.IngredientActiveSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/active/summaries")]
        [ProducesResponseType(typeof(IEnumerable<IngredientActiveSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<IngredientActiveSummaryDto>>> GetIngredientActiveSummaries(
            [FromQuery] string? ingredient,
            [FromQuery] int? minProductCount,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate minProductCount if provided
            if (minProductCount.HasValue && minProductCount.Value < 0)
            {
                return BadRequest("Minimum product count cannot be negative.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting active ingredient summaries. Ingredient: {Ingredient}, MinProductCount: {MinProductCount}, Page: {PageNumber}, Size: {PageSize}",
                    ingredient ?? "null", minProductCount, pageNumber, pageSize);

                var results = await DtoLabelAccess.GetIngredientActiveSummariesAsync(
                    _dbContext,
                    minProductCount,
                    ingredient,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active ingredient summaries");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving active ingredient summaries.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets inactive ingredient (excipient) summaries with product, document, and labeler counts.
        /// Discover the most common inactive ingredients across products in the database.
        /// </summary>
        /// <param name="ingredient">
        /// Optional. Filter by ingredient name (partial match on SubstanceName).
        /// Use this to vary results for AI skill discovery workflows.
        /// </param>
        /// <param name="minProductCount">
        /// Optional minimum product count filter. Only returns ingredients appearing in at least this many products.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of inactive ingredient summaries with aggregated counts.</returns>
        /// <response code="200">Returns the inactive ingredient summaries.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/ingredient/inactive/summaries
        /// GET /api/Label/ingredient/inactive/summaries?minProductCount=10
        /// GET /api/Label/ingredient/inactive/summaries?ingredient=starch
        ///
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedIngredientSubstanceID": "encrypted_string",
        ///     "UNII": "ETJ7Z6XBU4",
        ///     "SubstanceName": "SILICON DIOXIDE",
        ///     "IngredientType": "inactiveIngredient",
        ///     "ProductCount": 500,
        ///     "DocumentCount": 350,
        ///     "LabelerCount": 120
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by ProductCount in descending order.
        /// Inactive ingredients include excipients, fillers, binders, and other non-active substances.
        /// The ingredient parameter enables varied results for paginated queries.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetIngredientInactiveSummariesAsync"/>
        /// <seealso cref="LabelView.IngredientInactiveSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/inactive/summaries")]
        [ProducesResponseType(typeof(IEnumerable<IngredientInactiveSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<IngredientInactiveSummaryDto>>> GetIngredientInactiveSummaries(
            [FromQuery] string? ingredient,
            [FromQuery] int? minProductCount,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate minProductCount if provided
            if (minProductCount.HasValue && minProductCount.Value < 0)
            {
                return BadRequest("Minimum product count cannot be negative.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting inactive ingredient summaries. Ingredient: {Ingredient}, MinProductCount: {MinProductCount}, Page: {PageNumber}, Size: {PageSize}",
                    ingredient ?? "null", minProductCount, pageNumber, pageSize);

                var results = await DtoLabelAccess.GetIngredientInactiveSummariesAsync(
                    _dbContext,
                    minProductCount,
                    ingredient,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving inactive ingredient summaries");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving inactive ingredient summaries.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Advanced ingredient search with application number filtering, document linkage, and product name matching.
        /// Uses the new vw_Ingredients, vw_ActiveIngredients, and vw_InactiveIngredients views.
        /// </summary>
        /// <param name="unii">
        /// Optional. FDA UNII code for exact ingredient match.
        /// </param>
        /// <param name="substanceNameSearch">
        /// Optional. Substance name for partial/phonetic matching (tolerates misspellings).
        /// </param>
        /// <param name="applicationNumber">
        /// Optional. Application number (e.g., NDA020702, 020702) for filtering by regulatory approval.
        /// </param>
        /// <param name="applicationType">
        /// Optional. Application type filter (NDA, ANDA, BLA).
        /// </param>
        /// <param name="productNameSearch">
        /// Optional. Product name for partial/phonetic matching (tolerates misspellings).
        /// </param>
        /// <param name="activeOnly">
        /// Optional. Filter by ingredient type: true = active only, false = inactive only, null = all.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of ingredient view results with document linkage.</returns>
        /// <response code="200">Returns the list of ingredients matching the criteria.</response>
        /// <response code="400">If no search criteria provided or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Enhanced Ingredient Search
        ///
        /// This endpoint provides advanced search capabilities beyond the basic `/ingredient/search`:
        ///
        /// - **Application Number filtering**: Find ingredients by NDA, ANDA, or BLA number
        /// - **Document linkage**: Results include DocumentGUID for direct label retrieval
        /// - **Product name search**: Find ingredients by product name with phonetic matching
        /// - **Active/Inactive filtering**: Separate search for active vs inactive ingredients
        ///
        /// ### Examples
        ///
        /// ```
        /// GET /api/Label/ingredient/advanced?unii=R16CO5Y76E
        /// GET /api/Label/ingredient/advanced?substanceNameSearch=aspirin&amp;applicationNumber=020702
        /// GET /api/Label/ingredient/advanced?productNameSearch=TYLENOL&amp;activeOnly=true
        /// GET /api/Label/ingredient/advanced?applicationType=ANDA&amp;activeOnly=false
        /// ```
        ///
        /// ### Response
        ///
        /// ```json
        /// [
        ///   {
        ///     "IngredientView": {
        ///       "DocumentGUID": "12345678-1234-1234-1234-123456789012",
        ///       "ProductName": "TYLENOL",
        ///       "SubstanceName": "ACETAMINOPHEN",
        ///       "UNII": "362O9ITL9D",
        ///       "ApplicationType": "NDA",
        ///       "ApplicationNumber": "019872",
        ///       "ClassCode": "ACTIM"
        ///     }
        ///   }
        /// ]
        /// ```
        ///
        /// Use `DocumentGUID` with `/api/label/single/{documentGuid}` to retrieve the full label.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.SearchIngredientsAdvancedAsync"/>
        /// <seealso cref="LabelView.IngredientView"/>
        /// <seealso cref="LabelView.ActiveIngredientView"/>
        /// <seealso cref="LabelView.InactiveIngredientView"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/advanced")]
        [ProducesResponseType(typeof(IEnumerable<IngredientViewDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<IngredientViewDto>>> SearchIngredientsAdvanced(
            [FromQuery] string? unii,
            [FromQuery] string? substanceNameSearch,
            [FromQuery] string? applicationNumber,
            [FromQuery] string? applicationType,
            [FromQuery] string? productNameSearch,
            [FromQuery] bool? activeOnly,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate at least one search parameter is provided
            if (string.IsNullOrWhiteSpace(unii) &&
                string.IsNullOrWhiteSpace(substanceNameSearch) &&
                string.IsNullOrWhiteSpace(applicationNumber) &&
                string.IsNullOrWhiteSpace(applicationType) &&
                string.IsNullOrWhiteSpace(productNameSearch))
            {
                return BadRequest("At least one search parameter (unii, substanceNameSearch, applicationNumber, applicationType, or productNameSearch) is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Advanced ingredient search. UNII: {UNII}, SubstanceName: {SubstanceName}, AppNum: {AppNum}, AppType: {AppType}, ProductName: {ProductName}, ActiveOnly: {ActiveOnly}",
                    unii ?? "null", substanceNameSearch ?? "null", applicationNumber ?? "null", applicationType ?? "null", productNameSearch ?? "null", activeOnly?.ToString() ?? "null");

                var results = await DtoLabelAccess.SearchIngredientsAdvancedAsync(
                    _dbContext,
                    unii,
                    substanceNameSearch,
                    applicationNumber,
                    applicationType,
                    productNameSearch,
                    activeOnly,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in advanced ingredient search");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while searching ingredients.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Finds products that share the same active ingredient as a specified application number.
        /// Useful for finding generic equivalents or related brand products.
        /// </summary>
        /// <param name="applicationNumber">
        /// The application number to search (e.g., NDA020702, 020702).
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of products containing the same active ingredients.</returns>
        /// <response code="200">Returns the list of products with the same active ingredients.</response>
        /// <response code="400">If applicationNumber is not provided or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Find Products by Application Number with Same Ingredient
        ///
        /// This endpoint finds all products that contain the same active ingredient(s) as the specified application number.
        ///
        /// **Use Case:** Given an NDA or ANDA number, find all generic and brand products with the same active ingredient.
        ///
        /// ### Example
        ///
        /// ```
        /// GET /api/Label/ingredient/by-application?applicationNumber=020702
        /// ```
        ///
        /// This will:
        /// 1. Find the active ingredients for application number 020702
        /// 2. Return all products containing those same active ingredients
        ///
        /// ### Response
        ///
        /// ```json
        /// [
        ///   {
        ///     "IngredientView": {
        ///       "DocumentGUID": "12345678-1234-1234-1234-123456789012",
        ///       "ProductName": "LIPITOR",
        ///       "SubstanceName": "ATORVASTATIN CALCIUM",
        ///       "ApplicationNumber": "020702",
        ///       "ApplicationType": "NDA"
        ///     }
        ///   },
        ///   {
        ///     "IngredientView": {
        ///       "DocumentGUID": "87654321-4321-4321-4321-210987654321",
        ///       "ProductName": "ATORVASTATIN CALCIUM TABLETS",
        ///       "SubstanceName": "ATORVASTATIN CALCIUM",
        ///       "ApplicationNumber": "078456",
        ///       "ApplicationType": "ANDA"
        ///     }
        ///   }
        /// ]
        /// ```
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.FindProductsByApplicationNumberWithSameIngredientAsync"/>
        /// <seealso cref="LabelView.ActiveIngredientView"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/by-application")]
        [ProducesResponseType(typeof(IEnumerable<IngredientViewDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<IngredientViewDto>>> SearchIngredientByApplicationNumber(
            [FromQuery] string applicationNumber,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate application number is provided
            if (string.IsNullOrWhiteSpace(applicationNumber))
            {
                return BadRequest("Application number is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Finding products by application number with same ingredient. ApplicationNumber: {ApplicationNumber}",
                    applicationNumber);

                var results = await DtoLabelAccess.FindProductsByApplicationNumberWithSameIngredientAsync(
                    _dbContext,
                    applicationNumber,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding products by application number with same ingredient. ApplicationNumber: {ApplicationNumber}",
                    applicationNumber);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while searching products by application number.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets related ingredients for a specified ingredient.
        /// Given an ingredient (by UNII or name), finds all products containing it and their other ingredients.
        /// </summary>
        /// <param name="unii">
        /// Optional. FDA UNII code to search.
        /// </param>
        /// <param name="substanceNameSearch">
        /// Optional. Substance name for partial matching.
        /// </param>
        /// <param name="isActive">
        /// Optional. True if searching for an active ingredient, false for inactive. Default is true.
        /// </param>
        /// <returns>Related ingredient results including searched, related active, inactive, and products.</returns>
        /// <response code="200">Returns the related ingredient results.</response>
        /// <response code="400">If neither unii nor substanceNameSearch is provided.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Get Related Ingredients
        ///
        /// This endpoint finds all products containing a specified ingredient and returns:
        /// - **SearchedIngredients**: The ingredients that matched the search criteria
        /// - **RelatedActiveIngredients**: All active ingredients in those products
        /// - **RelatedInactiveIngredients**: All inactive ingredients (excipients) in those products
        /// - **RelatedProducts**: Summary of unique products found
        ///
        /// **Use Case:** Given an active ingredient, find all products containing it and their inactive ingredients.
        ///
        /// ### Examples
        ///
        /// ```
        /// GET /api/Label/ingredient/related?unii=R16CO5Y76E&amp;isActive=true
        /// GET /api/Label/ingredient/related?substanceNameSearch=aspirin&amp;isActive=true
        /// GET /api/Label/ingredient/related?substanceNameSearch=silicon dioxide&amp;isActive=false
        /// ```
        ///
        /// ### Response
        ///
        /// ```json
        /// {
        ///   "searchedIngredients": [...],
        ///   "relatedActiveIngredients": [...],
        ///   "relatedInactiveIngredients": [...],
        ///   "relatedProducts": [...],
        ///   "totalActiveCount": 5,
        ///   "totalInactiveCount": 25,
        ///   "totalProductCount": 10
        /// }
        /// ```
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.FindRelatedIngredientsAsync"/>
        /// <seealso cref="IngredientRelatedResultsDto"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ingredient/related")]
        [ProducesResponseType(typeof(IngredientRelatedResultsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IngredientRelatedResultsDto>> GetRelatedIngredients(
            [FromQuery] string? unii,
            [FromQuery] string? substanceNameSearch,
            [FromQuery] bool? isActive)
        {
            #region Input Validation

            // Validate at least one search parameter is provided
            if (string.IsNullOrWhiteSpace(unii) && string.IsNullOrWhiteSpace(substanceNameSearch))
            {
                return BadRequest("At least one search parameter (unii or substanceNameSearch) is required.");
            }

            #endregion

            #region Implementation

            try
            {
                // Default to searching for active ingredients
                bool searchingActive = isActive ?? true;

                _logger.LogInformation("Finding related ingredients. UNII: {UNII}, SubstanceName: {SubstanceName}, IsActive: {IsActive}",
                    unii ?? "null", substanceNameSearch ?? "null", searchingActive);

                var results = await DtoLabelAccess.FindRelatedIngredientsAsync(
                    _dbContext,
                    unii,
                    substanceNameSearch,
                    searchingActive,
                    _pkEncryptionSecret,
                    _logger);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding related ingredients. UNII: {UNII}, SubstanceName: {SubstanceName}",
                    unii ?? "null", substanceNameSearch ?? "null");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while finding related ingredients.");
            }

            #endregion
        }

        #endregion Ingredient Navigation

        #region Product Identifier Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by NDC (National Drug Code) or other product identifiers.
        /// Critical for pharmacy system integration and product lookup by code.
        /// </summary>
        /// <param name="productCode">
        /// The NDC or product code to search for (e.g., "12345-678-90").
        /// Supports partial matching for flexible searches.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of products matching the product code criteria.</returns>
        /// <response code="200">Returns the list of products matching the NDC code.</response>
        /// <response code="400">If productCode is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/ndc/search?productCode=12345-678
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "LIPITOR",
        ///     "ProductCode": "12345-678-90",
        ///     "LabelerName": "PFIZER INC"
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by ProductCode.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search by full NDC
        /// GET /api/Label/ndc/search?productCode=12345-678-90
        /// 
        /// // Search by partial NDC
        /// GET /api/Label/ndc/search?productCode=12345
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchByNDCAsync"/>
        /// <seealso cref="LabelView.ProductsByNDC"/>
        /// <seealso cref="Label.ProductIdentifier"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ndc/search")]
        [ProducesResponseType(typeof(IEnumerable<ProductsByNDCDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductsByNDCDto>>> SearchByNDC(
            [FromQuery] string productCode,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate product code is provided
            if (string.IsNullOrWhiteSpace(productCode))
            {
                return BadRequest("Product code (NDC) is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching products by NDC: {ProductCode}, Page: {PageNumber}, Size: {PageSize}",
                    productCode, pageNumber, pageSize);

                var results = await DtoLabelAccess.SearchByNDCAsync(
                    _dbContext,
                    productCode,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products by NDC {ProductCode}", productCode);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while searching products by NDC.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches package configurations by NDC package code.
        /// Shows packaging hierarchy and quantities for specific package codes.
        /// </summary>
        /// <param name="packageCode">
        /// The NDC package code to search for.
        /// Supports partial matching for flexible searches.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of package configurations matching the package code.</returns>
        /// <response code="200">Returns the list of packages matching the code.</response>
        /// <response code="400">If packageCode is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/ndc/package/search?packageCode=12345-678-90
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedPackagingLevelID": "encrypted_string",
        ///     "PackageCode": "12345-678-90",
        ///     "PackageDescription": "100 TABLETS in 1 BOTTLE",
        ///     "PackageQuantity": 100
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by PackageCode.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.SearchByPackageNDCAsync"/>
        /// <seealso cref="LabelView.PackageByNDC"/>
        /// <seealso cref="Label.PackageIdentifier"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("ndc/package/search")]
        [ProducesResponseType(typeof(IEnumerable<PackageByNDCDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<PackageByNDCDto>>> SearchByPackageNDC(
            [FromQuery] string packageCode,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate package code is provided
            if (string.IsNullOrWhiteSpace(packageCode))
            {
                return BadRequest("Package code is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching packages by NDC: {PackageCode}, Page: {PageNumber}, Size: {PageSize}",
                    packageCode, pageNumber, pageSize);

                var results = await DtoLabelAccess.SearchByPackageNDCAsync(
                    _dbContext,
                    packageCode,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching packages by NDC {PackageCode}", packageCode);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while searching packages by NDC.");
            }

            #endregion
        }

        #endregion Product Identifier Navigation

        #region Organization Navigation

        /**************************************************************/
        /// <summary>
        /// Searches products by labeler (marketing organization) name.
        /// Lists products associated with a specific pharmaceutical company or distributor.
        /// </summary>
        /// <param name="labelerNameSearch">
        /// Search term to match against labeler names (e.g., "Pfizer", "Johnson").
        /// Supports partial matching for flexible searches.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of products matching the labeler name criteria.</returns>
        /// <response code="200">Returns the list of products by labeler.</response>
        /// <response code="400">If labelerNameSearch is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/labeler/search?labelerNameSearch=Pfizer
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "LIPITOR",
        ///     "LabelerName": "PFIZER INC",
        ///     "LabelerDUNS": "123456789"
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by LabelerName, then ProductName.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search for Pfizer products
        /// GET /api/Label/labeler/search?labelerNameSearch=Pfizer
        /// 
        /// // Search with pagination
        /// GET /api/Label/labeler/search?labelerNameSearch=Johnson&amp;pageNumber=1&amp;pageSize=50
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchByLabelerAsync"/>
        /// <seealso cref="LabelView.ProductsByLabeler"/>
        /// <seealso cref="Label.Organization"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("labeler/search")]
        [ProducesResponseType(typeof(IEnumerable<ProductsByLabelerDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductsByLabelerDto>>> SearchByLabeler(
            [FromQuery] string labelerNameSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate labeler name search term is provided
            if (string.IsNullOrWhiteSpace(labelerNameSearch))
            {
                return BadRequest("Labeler name search term is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching products by labeler: {LabelerNameSearch}, Page: {PageNumber}, Size: {PageSize}",
                    labelerNameSearch, pageNumber, pageSize);

                var results = await DtoLabelAccess.SearchByLabelerAsync(
                    _dbContext,
                    labelerNameSearch,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products by labeler {LabelerNameSearch}", labelerNameSearch);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while searching products by labeler.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets labeler (marketing organization) summaries with product counts.
        /// Discover which pharmaceutical companies have the most products in the database.
        /// </summary>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of labeler summaries with aggregated product counts.</returns>
        /// <response code="200">Returns the labeler summaries.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/labeler/summaries
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "LabelerName": "PFIZER INC",
        ///     "LabelerDUNS": "123456789",
        ///     "ProductCount": 500
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by ProductCount in descending order.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetLabelerSummariesAsync"/>
        /// <seealso cref="LabelView.LabelerSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("labeler/summaries")]
        [ProducesResponseType(typeof(IEnumerable<LabelerSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<LabelerSummaryDto>>> GetLabelerSummaries(
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting labeler summaries. Page: {PageNumber}, Size: {PageSize}",
                    pageNumber, pageSize);

                var results = await DtoLabelAccess.GetLabelerSummariesAsync(
                    _dbContext,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving labeler summaries");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving labeler summaries.");
            }

            #endregion
        }

        #endregion Organization Navigation

        #region Document Navigation

        /**************************************************************/
        /// <summary>
        /// Gets document navigation data with version tracking capabilities.
        /// Supports discovery of latest document versions and version history navigation.
        /// </summary>
        /// <param name="latestOnly">
        /// If true, returns only the latest version of each document set.
        /// If false, returns all versions.
        /// </param>
        /// <param name="setGuid">
        /// Optional filter by SetGUID to get all versions of a specific document set.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of document navigation data with version information.</returns>
        /// <response code="200">Returns the document navigation data.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/document/navigation?latestOnly=true
        /// GET /api/Label/document/navigation?latestOnly=false&amp;setGuid=12345678-1234-1234-1234-123456789012
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedDocumentID": "encrypted_string",
        ///     "DocumentGUID": "12345678-1234-1234-1234-123456789012",
        ///     "SetGUID": "abcdefgh-1234-1234-1234-123456789012",
        ///     "VersionNumber": 3,
        ///     "IsLatestVersion": true,
        ///     "EffectiveDate": "2024-01-15"
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by EffectiveDate in descending order.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get only latest document versions
        /// GET /api/Label/document/navigation?latestOnly=true
        /// 
        /// // Get all versions of a specific document set
        /// GET /api/Label/document/navigation?latestOnly=false&amp;setGuid=12345678-1234-1234-1234-123456789012
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.GetDocumentNavigationAsync"/>
        /// <seealso cref="LabelView.DocumentNavigation"/>
        /// <seealso cref="Label.Document"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("document/navigation")]
        [ProducesResponseType(typeof(IEnumerable<DocumentNavigationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<DocumentNavigationDto>>> GetDocumentNavigation(
            [FromQuery] bool latestOnly = false,
            [FromQuery] Guid? setGuid = null,
            [FromQuery] int? pageNumber = null,
            [FromQuery] int? pageSize = null)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting document navigation. LatestOnly: {LatestOnly}, SetGUID: {SetGUID}, Page: {PageNumber}, Size: {PageSize}",
                    latestOnly, setGuid, pageNumber, pageSize);

                var results = await DtoLabelAccess.GetDocumentNavigationAsync(
                    _dbContext,
                    latestOnly,
                    setGuid,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document navigation");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving document navigation.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets document version history for a specific document set.
        /// Tracks all versions over time within a SetGUID or for a specific DocumentGUID.
        /// </summary>
        /// <param name="setGuidOrDocumentGuid">
        /// The SetGUID or DocumentGUID to retrieve version history for.
        /// When a DocumentGUID is provided, returns the history for its associated document set.
        /// </param>
        /// <returns>List of document version history records.</returns>
        /// <response code="200">Returns the document version history.</response>
        /// <response code="400">If setGuidOrDocumentGuid is empty.</response>
        /// <response code="404">If no version history is found for the specified GUID.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/document/version-history/12345678-1234-1234-1234-123456789012
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedDocumentID": "encrypted_string",
        ///     "DocumentGUID": "12345678-1234-1234-1234-123456789012",
        ///     "SetGUID": "abcdefgh-1234-1234-1234-123456789012",
        ///     "VersionNumber": 3,
        ///     "EffectiveDate": "2024-01-15",
        ///     "ChangeDescription": "Annual update"
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by VersionNumber in descending order (newest first).
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetDocumentVersionHistoryAsync"/>
        /// <seealso cref="LabelView.DocumentVersionHistory"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("document/version-history/{setGuidOrDocumentGuid}")]
        [ProducesResponseType(typeof(IEnumerable<DocumentVersionHistoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<DocumentVersionHistoryDto>>> GetDocumentVersionHistory(
            Guid setGuidOrDocumentGuid)
        {
            #region Input Validation

            // Validate GUID is not empty
            if (setGuidOrDocumentGuid == Guid.Empty)
            {
                return BadRequest("SetGUID or DocumentGUID cannot be empty.");
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting document version history for GUID: {SetGuidOrDocumentGuid}",
                    setGuidOrDocumentGuid);

                var results = await DtoLabelAccess.GetDocumentVersionHistoryAsync(
                    _dbContext,
                    setGuidOrDocumentGuid,
                    _pkEncryptionSecret,
                    _logger);

                // Check if any history was found
                if (results == null || !results.Any())
                {
                    _logger.LogWarning("No version history found for GUID: {SetGuidOrDocumentGuid}", setGuidOrDocumentGuid);
                    return NotFound($"No version history found for GUID {setGuidOrDocumentGuid}.");
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document version history for GUID {SetGuidOrDocumentGuid}",
                    setGuidOrDocumentGuid);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving document version history.");
            }

            #endregion
        }

        #endregion Document Navigation

        #region Section Navigation

        /**************************************************************/
        /// <summary>
        /// Searches sections by LOINC section code.
        /// Enables navigation to specific labeling sections across documents.
        /// </summary>
        /// <param name="sectionCode">
        /// The LOINC section code to search for (e.g., "34066-1" for Boxed Warning, "34067-9" for Indications).
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of sections matching the section code.</returns>
        /// <response code="200">Returns the list of sections matching the code.</response>
        /// <response code="400">If sectionCode is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/section/search?sectionCode=34066-1
        /// 
        /// Common LOINC section codes:
        /// - 34066-1: Boxed Warning
        /// - 34067-9: Indications and Usage
        /// - 34068-7: Dosage and Administration
        /// - 34069-5: Contraindications
        /// - 34070-3: Warnings
        /// - 34071-1: Warnings and Precautions
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedSectionID": "encrypted_string",
        ///     "SectionCode": "34066-1",
        ///     "SectionTitle": "BOXED WARNING",
        ///     "DocumentTitle": "LIPITOR Prescribing Information"
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by DocumentTitle.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search for all boxed warnings
        /// GET /api/Label/section/search?sectionCode=34066-1
        /// 
        /// // Search for indications with pagination
        /// GET /api/Label/section/search?sectionCode=34067-9&amp;pageNumber=1&amp;pageSize=50
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchBySectionCodeAsync"/>
        /// <seealso cref="LabelView.SectionNavigation"/>
        /// <seealso cref="Label.Section"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("section/search")]
        [ProducesResponseType(typeof(IEnumerable<SectionNavigationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<SectionNavigationDto>>> SearchBySectionCode(
            [FromQuery] string sectionCode,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate section code is provided
            if (string.IsNullOrWhiteSpace(sectionCode))
            {
                return BadRequest("Section code (LOINC) is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching sections by code: {SectionCode}, Page: {PageNumber}, Size: {PageSize}",
                    sectionCode, pageNumber, pageSize);

                var results = await DtoLabelAccess.SearchBySectionCodeAsync(
                    _dbContext,
                    sectionCode,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching sections by code {SectionCode}", sectionCode);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while searching sections by code.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets section type summaries with document counts.
        /// Discover which section types (LOINC codes) are most common across all documents.
        /// </summary>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of section type summaries with aggregated document counts.</returns>
        /// <response code="200">Returns the section type summaries.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/section/summaries
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "SectionCode": "34067-9",
        ///     "SectionTitle": "INDICATIONS AND USAGE",
        ///     "DocumentCount": 15000
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by DocumentCount in descending order.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetSectionTypeSummariesAsync"/>
        /// <seealso cref="LabelView.SectionTypeSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("section/summaries")]
        [ProducesResponseType(typeof(IEnumerable<SectionTypeSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<SectionTypeSummaryDto>>> GetSectionTypeSummaries(
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting section type summaries. Page: {PageNumber}, Size: {PageSize}",
                    pageNumber, pageSize);

                var results = await DtoLabelAccess.GetSectionTypeSummariesAsync(
                    _dbContext,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving section type summaries");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving section type summaries.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets section text content for AI summarization workflows.
        /// Provides efficient text retrieval from document sections.
        /// </summary>
        /// <param name="documentGuid">
        /// Required. The document GUID to retrieve section content for.
        /// </param>
        /// <param name="sectionGuid">
        /// Optional. Filter to a specific section by its GUID.
        /// </param>
        /// <param name="sectionCode">
        /// Optional. Filter by LOINC section code (e.g., "34084-4" for Adverse Reactions).
        /// Supports partial matching for flexible queries.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of section content with text for summarization.</returns>
        /// <response code="200">Returns the list of section content.</response>
        /// <response code="400">If documentGuid is empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ## Usage Examples
        /// 
        /// Get all section content for a document:
        /// ```
        /// GET /api/Label/section/content/{documentGuid}
        /// ```
        /// 
        /// Get specific section by GUID:
        /// ```
        /// GET /api/Label/section/content/{documentGuid}?sectionGuid={guid}
        /// ```
        /// 
        /// Get all Adverse Reactions sections:
        /// ```
        /// GET /api/Label/section/content/{documentGuid}?sectionCode=34084-4
        /// ```
        /// 
        /// ## Common LOINC Section Codes
        /// 
        /// | Code | Section Name |
        /// |------|--------------|
        /// | 34066-1 | Boxed Warning |
        /// | 34067-9 | Indications and Usage |
        /// | 34068-7 | Dosage and Administration |
        /// | 34069-5 | Contraindications |
        /// | 43685-7 | Warnings and Precautions |
        /// | 34084-4 | Adverse Reactions |
        /// | 34073-7 | Drug Interactions |
        /// | 34088-5 | Overdosage |
        /// | 34090-1 | Clinical Pharmacology |
        /// 
        /// ## Response Format (200)
        /// 
        /// ```json
        /// [
        ///   {
        ///     "sectionContent": {
        ///       "EncryptedDocumentID": "encrypted_string",
        ///       "EncryptedSectionID": "encrypted_string",
        ///       "DocumentGUID": "guid",
        ///       "SectionGUID": "guid",
        ///       "SectionCode": "34084-4",
        ///       "SectionDisplayName": "ADVERSE REACTIONS",
        ///       "SectionTitle": "Adverse Reactions",
        ///       "ContentText": "The following adverse reactions...",
        ///       "SequenceNumber": 1,
        ///       "ContentType": "paragraph"
        ///     }
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by SectionCode, then SequenceNumber for proper reading order.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Get warnings section for AI summarization
        /// GET /api/Label/section/content/12345678-1234-1234-1234-123456789012?sectionCode=43685-7
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.GetSectionContentAsync"/>
        /// <seealso cref="LabelView.SectionContent"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("section/content/{documentGuid}")]
        [ProducesResponseType(typeof(IEnumerable<SectionContentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<SectionContentDto>>> GetSectionContent(
            [FromRoute] Guid documentGuid,
            [FromQuery] Guid? sectionGuid,
            [FromQuery] string? sectionCode,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate documentGuid is provided
            if (documentGuid == Guid.Empty)
            {
                return BadRequest("A valid documentGuid is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation(
                    "Getting section content for DocumentGUID: {DocumentGuid}, SectionGUID: {SectionGuid}, SectionCode: {SectionCode}, Page: {PageNumber}, Size: {PageSize}",
                    documentGuid,
                    sectionGuid?.ToString() ?? "all",
                    sectionCode ?? "all",
                    pageNumber,
                    pageSize);

                var results = await DtoLabelAccess.GetSectionContentAsync(
                    _dbContext,
                    documentGuid,
                    sectionGuid,
                    sectionCode,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving section content for DocumentGUID {DocumentGuid}", documentGuid);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving section content.");
            }

            #endregion
        }

        #endregion Section Navigation

        #region Section Markdown Export

        /**************************************************************/
        /// <summary>
        /// Gets markdown-formatted section content for a document by DocumentGUID.
        /// Returns aggregated, LLM-ready section text from the vw_LabelSectionMarkdown view.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique identifier (GUID) for the document to retrieve sections for.
        /// </param>
        /// <param name="sectionCode">
        /// Optional LOINC section code to filter results (e.g., "34067-9" for Indications).
        /// When provided, only sections matching this code are returned, significantly reducing payload size.
        /// </param>
        /// <returns>List of section markdown DTOs with formatted content.</returns>
        /// <response code="200">Returns the list of markdown-formatted sections.</response>
        /// <response code="400">If documentGuid is not a valid GUID.</response>
        /// <response code="404">If no sections are found for the document.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Get Label Sections as Markdown
        ///
        /// This endpoint returns all sections of a drug label formatted as markdown,
        /// designed for AI/LLM consumption. Each section includes:
        /// - **SectionKey**: Unique identifier combining DocumentGUID, SectionCode, and SectionTitle
        /// - **FullSectionText**: Complete markdown text with ## header and content
        /// - **ContentBlockCount**: Number of content blocks aggregated
        ///
        /// ### Example
        ///
        /// ```
        /// GET /api/Label/markdown/sections/052493C7-89A3-452E-8140-04DD95F0D9E2
        ///
        /// // Filter to specific section (reduces payload significantly)
        /// GET /api/Label/markdown/sections/052493C7-89A3-452E-8140-04DD95F0D9E2?sectionCode=34067-9
        /// ```
        ///
        /// ### Response
        ///
        /// ```json
        /// [
        ///   {
        ///     "LabelSectionMarkdown": {
        ///       "DocumentGUID": "052493C7-89A3-452E-8140-04DD95F0D9E2",
        ///       "SectionCode": "34067-9",
        ///       "SectionTitle": "INDICATIONS AND USAGE",
        ///       "SectionKey": "052493C7-89A3-452E-8140-04DD95F0D9E2|34067-9|INDICATIONS AND USAGE",
        ///       "FullSectionText": "## INDICATIONS AND USAGE\n\nLIPITOR is indicated...",
        ///       "ContentBlockCount": 5
        ///     }
        ///   }
        /// ]
        /// ```
        ///
        /// ### Use Case
        ///
        /// This endpoint is designed for AI skill augmentation workflows where the Claude API
        /// needs authoritative label content rather than relying on training data to generate
        /// accurate summaries and descriptions.
        ///
        /// ### Token Optimization
        ///
        /// When comparing multiple drugs, use the `sectionCode` parameter to fetch only
        /// the relevant section(s). This reduces payload from ~88KB (all sections) to ~1-2KB
        /// per section, significantly reducing token usage for AI skill augmentation.
        ///
        /// **Common LOINC Section Codes:**
        /// - `34067-9` = Indications and Usage
        /// - `34084-4` = Adverse Reactions
        /// - `34070-3` = Contraindications
        /// - `43685-7` = Warnings and Precautions
        /// - `34068-7` = Dosage and Administration
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetLabelSectionMarkdownAsync"/>
        /// <seealso cref="LabelView.LabelSectionMarkdown"/>
        /// <seealso cref="GetLabelMarkdownExport"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("markdown/sections/{documentGuid:guid}")]
        [ProducesResponseType(typeof(IEnumerable<LabelSectionMarkdownDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<LabelSectionMarkdownDto>>> GetLabelSectionMarkdown(
            [FromRoute] Guid documentGuid,
            [FromQuery] string? sectionCode = null)
        {
            #region Implementation

            try
            {
                if (string.IsNullOrWhiteSpace(sectionCode))
                {
                    _logger.LogInformation("Getting all markdown sections for DocumentGUID: {DocumentGuid}", documentGuid);
                }
                else
                {
                    _logger.LogInformation("Getting markdown section {SectionCode} for DocumentGUID: {DocumentGuid}", sectionCode, documentGuid);
                }

                var results = await DtoLabelAccess.GetLabelSectionMarkdownAsync(
                    _dbContext,
                    documentGuid,
                    _pkEncryptionSecret,
                    _logger,
                    sectionCode);

                // Return 404 if no sections found
                if (results == null || results.Count == 0)
                {
                    var message = string.IsNullOrWhiteSpace(sectionCode)
                        ? $"No sections found for DocumentGUID {documentGuid}."
                        : $"No sections found for DocumentGUID {documentGuid} with SectionCode {sectionCode}.";
                    return NotFound(message);
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving markdown sections for DocumentGUID {DocumentGuid}", documentGuid);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving markdown sections.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates a complete markdown document for a drug label by DocumentGUID.
        /// Combines all sections with header information for AI skill augmentation.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique identifier (GUID) for the document to export.
        /// </param>
        /// <returns>A LabelMarkdownExportDto containing the complete markdown and metadata.</returns>
        /// <response code="200">Returns the complete markdown export.</response>
        /// <response code="400">If documentGuid is not a valid GUID.</response>
        /// <response code="404">If no document is found for the GUID.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Generate Complete Label Markdown Export
        ///
        /// This endpoint generates a complete markdown document suitable for AI/LLM consumption.
        /// The output includes:
        ///
        /// **Header Section:**
        /// - Document title and identifiers
        /// - Data dictionary explaining the structure
        /// - Section count and content block totals
        ///
        /// **Content Sections:**
        /// - All label sections in LOINC code order
        /// - Each section with ## markdown header
        /// - Content with markdown formatting (bold, italics, underline)
        ///
        /// ### Example
        ///
        /// ```
        /// GET /api/Label/markdown/export/052493C7-89A3-452E-8140-04DD95F0D9E2
        /// ```
        ///
        /// ### Response
        ///
        /// ```json
        /// {
        ///   "documentGUID": "052493C7-89A3-452E-8140-04DD95F0D9E2",
        ///   "setGUID": "A1B2C3D4-E5F6-7890-ABCD-EF1234567890",
        ///   "documentTitle": "LIPITOR- atorvastatin calcium tablet",
        ///   "sectionCount": 15,
        ///   "totalContentBlocks": 87,
        ///   "fullMarkdown": "# FDA Drug Label Reference\n\n..."
        /// }
        /// ```
        ///
        /// ### Use Case
        ///
        /// This endpoint is designed for building Claude API skills that need to summarize
        /// or analyze FDA drug label content accurately and completely, without relying
        /// on AI training data which may be outdated or incomplete.
        ///
        /// **Workflow:**
        /// 1. Query this endpoint with the DocumentGUID of the label
        /// 2. Pass the `fullMarkdown` content to your Claude API skill
        /// 3. Use the authoritative content for accurate summarization
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GenerateLabelMarkdownAsync"/>
        /// <seealso cref="LabelMarkdownExportDto"/>
        /// <seealso cref="GetLabelSectionMarkdown"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("markdown/export/{documentGuid:guid}")]
        [ProducesResponseType(typeof(LabelMarkdownExportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<LabelMarkdownExportDto>> GetLabelMarkdownExport(
            [FromRoute] Guid documentGuid)
        {
            #region Implementation

            try
            {
                _logger.LogInformation("Generating markdown export for DocumentGUID: {DocumentGuid}", documentGuid);

                var result = await DtoLabelAccess.GenerateLabelMarkdownAsync(
                    _dbContext,
                    documentGuid,
                    _pkEncryptionSecret,
                    _logger);

                // Return 404 if no content generated (empty document)
                if (result == null || result.SectionCount == 0)
                {
                    return NotFound($"No sections found for DocumentGUID {documentGuid}.");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating markdown export for DocumentGUID {DocumentGuid}", documentGuid);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while generating markdown export.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Downloads the complete markdown export as a .md file.
        /// Returns the markdown content with appropriate content-type for download.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique identifier (GUID) for the document to export.
        /// </param>
        /// <returns>A downloadable markdown file.</returns>
        /// <response code="200">Returns the markdown file for download.</response>
        /// <response code="400">If documentGuid is not a valid GUID.</response>
        /// <response code="404">If no document is found for the GUID.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Download Label as Markdown File
        ///
        /// This endpoint returns the same content as `/api/Label/markdown/export/{documentGuid}`
        /// but formatted as a downloadable .md file with appropriate headers.
        ///
        /// ### Example
        ///
        /// ```
        /// GET /api/Label/markdown/download/052493C7-89A3-452E-8140-04DD95F0D9E2
        /// ```
        ///
        /// ### Response Headers
        ///
        /// ```
        /// Content-Type: text/markdown
        /// Content-Disposition: attachment; filename="LIPITOR-label.md"
        /// ```
        ///
        /// ### Use Case
        ///
        /// This endpoint allows users to download the complete label markdown for:
        /// - Offline AI skill development and testing
        /// - Local storage of authoritative label content
        /// - Integration with documentation systems
        /// </remarks>
        /// <seealso cref="GetLabelMarkdownExport"/>
        /// <seealso cref="DtoLabelAccess.GenerateLabelMarkdownAsync"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("markdown/download/{documentGuid:guid}")]
        [Produces("text/markdown")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DownloadLabelMarkdown(
            [FromRoute] Guid documentGuid)
        {
            #region Implementation

            try
            {
                _logger.LogInformation("Downloading markdown for DocumentGUID: {DocumentGuid}", documentGuid);

                var result = await DtoLabelAccess.GenerateLabelMarkdownAsync(
                    _dbContext,
                    documentGuid,
                    _pkEncryptionSecret,
                    _logger);

                // Return 404 if no content generated (empty document)
                if (result == null || result.SectionCount == 0)
                {
                    return NotFound($"No sections found for DocumentGUID {documentGuid}.");
                }

                // Generate safe filename from document title
                var safeTitle = result.DocumentTitle?.Replace(" ", "-")
                    .Replace(",", "")
                    .Replace("/", "-")
                    .Replace("\\", "-")
                    ?? "drug-label";

                // Truncate if too long
                if (safeTitle.Length > 50)
                {
                    safeTitle = safeTitle.Substring(0, 50);
                }

                var fileName = $"{safeTitle}-label.md";

                // Return as file download
                var bytes = System.Text.Encoding.UTF8.GetBytes(result.FullMarkdown ?? "");
                return File(bytes, "text/markdown", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading markdown for DocumentGUID {DocumentGuid}", documentGuid);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while downloading markdown.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates clean, human-readable markdown for display by processing content through Claude AI.
        /// Returns formatted markdown suitable for static web app rendering.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique identifier (GUID) for the document to export.
        /// </param>
        /// <returns>Clean, formatted markdown text suitable for display.</returns>
        /// <response code="200">Returns the clean markdown content.</response>
        /// <response code="400">If documentGuid is not a valid GUID.</response>
        /// <response code="404">If no document is found for the GUID.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ### Generate Clean Display Markdown
        ///
        /// This endpoint generates clean, human-readable markdown by:
        /// 1. Retrieving all sections from vw_LabelSectionMarkdown
        /// 2. Passing the raw content to Claude AI for cleanup
        /// 3. Returning formatted markdown without XML/HTML artifacts
        ///
        /// The output is optimized for:
        /// - **Static web app display** (React/Angular markdown renderers)
        /// - **Documentation generation**
        /// - **Human readability** without technical artifacts
        ///
        /// ### Example
        ///
        /// ```
        /// GET /api/Label/markdown/display/22212bf6-1414-4b32-bc67-25d614c357ee
        /// ```
        ///
        /// ### Response (text/markdown)
        ///
        /// ```markdown
        /// # Lorazepam Tablets, USP CIV
        ///
        /// **Rx only**
        ///
        /// ## INDICATIONS AND USAGE
        ///
        /// Lorazepam tablets are indicated for the management of anxiety disorders...
        ///
        /// ## DOSAGE AND ADMINISTRATION
        ///
        /// Lorazepam tablets are administered orally...
        /// ```
        ///
        /// ### Performance Notes
        ///
        /// - Results are cached for 1 hour to minimize Claude API calls
        /// - First request may take 5-15 seconds for Claude processing
        /// - Subsequent requests return cached content instantly
        ///
        /// ### Use Case
        ///
        /// This endpoint is designed for displaying drug labels in static web applications
        /// where clean, readable markdown is preferred over XML rendering. It may be more
        /// performant than XML→XSLT transformation for simple display scenarios.
        /// </remarks>
        /// <seealso cref="GetLabelMarkdownExport"/>
        /// <seealso cref="DownloadLabelMarkdown"/>
        /// <seealso cref="DtoLabelAccess.GenerateCleanLabelMarkdownAsync"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("markdown/display/{documentGuid:guid}")]
        [Produces("text/markdown")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCleanLabelMarkdown(
            [FromRoute] Guid documentGuid)
        {
            #region Implementation

            try
            {
                _logger.LogInformation("Generating clean display markdown for DocumentGUID: {DocumentGuid}", documentGuid);

                var cleanMarkdown = await DtoLabelAccess.GenerateCleanLabelMarkdownAsync(
                    _dbContext,
                    documentGuid,
                    _claudeApiService,
                    _pkEncryptionSecret,
                    _logger);

                // Return 404 if no content generated (empty document)
                if (string.IsNullOrWhiteSpace(cleanMarkdown))
                {
                    return NotFound($"No sections found for DocumentGUID {documentGuid}.");
                }

                // Return as text/markdown content
                return Content(cleanMarkdown, "text/markdown", System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating clean markdown for DocumentGUID {DocumentGuid}", documentGuid);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while generating clean markdown.");
            }

            #endregion
        }

        #endregion Section Markdown Export

        #region Drug Safety Navigation

        /**************************************************************/
        /// <summary>
        /// Gets potential drug interactions based on shared active ingredients.
        /// Supports pharmacist review and clinical decision support systems.
        /// </summary>
        /// <param name="ingredientUNIIs">
        /// Comma-separated list of UNII codes to check for potential interactions.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of potential drug interactions based on shared ingredients.</returns>
        /// <response code="200">Returns the list of potential drug interactions.</response>
        /// <response code="400">If ingredientUNIIs is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/drug-safety/interactions?ingredientUNIIs=R16CO5Y76E,YOW8V9698H
        /// 
        /// This endpoint identifies products that share common active ingredients,
        /// which may indicate potential drug-drug interactions requiring clinical review.
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "BAYER ASPIRIN",
        ///     "IngredientUNII": "R16CO5Y76E",
        ///     "SubstanceName": "ASPIRIN",
        ///     "InteractionRisk": "Moderate"
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by ProductName.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Check interactions for multiple ingredients
        /// GET /api/Label/drug-safety/interactions?ingredientUNIIs=R16CO5Y76E,YOW8V9698H
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.GetDrugInteractionsAsync"/>
        /// <seealso cref="LabelView.DrugInteractionLookup"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("drug-safety/interactions")]
        [ProducesResponseType(typeof(IEnumerable<DrugInteractionLookupDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<DrugInteractionLookupDto>>> GetDrugInteractions(
            [FromQuery] string ingredientUNIIs,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate ingredient UNIIs are provided
            if (string.IsNullOrWhiteSpace(ingredientUNIIs))
            {
                return BadRequest("At least one ingredient UNII is required.");
            }

            // Parse comma-separated UNII list
            var uniiList = ingredientUNIIs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (uniiList.Length == 0)
            {
                return BadRequest("At least one valid ingredient UNII is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting drug interactions for UNIIs: {IngredientUNIIs}, Page: {PageNumber}, Size: {PageSize}",
                    ingredientUNIIs, pageNumber, pageSize);

                var results = await DtoLabelAccess.GetDrugInteractionsAsync(
                    _dbContext,
                    uniiList,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving drug interactions for UNIIs {IngredientUNIIs}", ingredientUNIIs);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving drug interactions.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets products with DEA controlled substance schedules.
        /// Important for pharmacy compliance and controlled substance management.
        /// </summary>
        /// <param name="scheduleCode">
        /// Optional filter by specific DEA schedule code (e.g., "CII", "CIII", "CIV", "CV").
        /// If not provided, returns all controlled substances.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of products with DEA schedule classifications.</returns>
        /// <response code="200">Returns the list of DEA scheduled products.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/drug-safety/dea-schedule
        /// GET /api/Label/drug-safety/dea-schedule?scheduleCode=CII
        /// 
        /// DEA Schedule Codes:
        /// - CI: Schedule I (no accepted medical use)
        /// - CII: Schedule II (high potential for abuse)
        /// - CIII: Schedule III (moderate to low potential for abuse)
        /// - CIV: Schedule IV (low potential for abuse)
        /// - CV: Schedule V (lower potential for abuse than Schedule IV)
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "OXYCONTIN",
        ///     "DEAScheduleCode": "CII",
        ///     "DEAScheduleName": "Schedule II Controlled Substance"
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by DEAScheduleCode, then ProductName.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetDEAScheduleProductsAsync"/>
        /// <seealso cref="LabelView.DEAScheduleLookup"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("drug-safety/dea-schedule")]
        [ProducesResponseType(typeof(IEnumerable<DEAScheduleLookupDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<DEAScheduleLookupDto>>> GetDEAScheduleProducts(
            [FromQuery] string? scheduleCode,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting DEA schedule products. ScheduleCode: {ScheduleCode}, Page: {PageNumber}, Size: {PageSize}",
                    scheduleCode ?? "all", pageNumber, pageSize);

                var results = await DtoLabelAccess.GetDEAScheduleProductsAsync(
                    _dbContext,
                    scheduleCode,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving DEA schedule products");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving DEA schedule products.");
            }

            #endregion
        }

        #endregion Drug Safety Navigation

        #region Product Summary and Cross-Reference

        /**************************************************************/
        /// <summary>
        /// Searches for products with comprehensive summary information.
        /// Provides a complete product overview with key attributes for quick reference.
        /// </summary>
        /// <param name="productNameSearch">
        /// Search term to match against product names (e.g., "Lipitor", "Aspirin").
        /// Supports partial matching for flexible searches.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of product summaries matching the product name.</returns>
        /// <response code="200">Returns the list of product summaries.</response>
        /// <response code="400">If productNameSearch is null/empty or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/product/search?productNameSearch=Lipitor
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "EncryptedProductID": "encrypted_string",
        ///     "ProductName": "LIPITOR",
        ///     "LabelerName": "PFIZER INC",
        ///     "ApplicationNumber": "NDA020702",
        ///     "ActiveIngredients": "ATORVASTATIN CALCIUM",
        ///     "DosageForm": "TABLET, FILM COATED"
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by ProductName.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Search for Lipitor
        /// GET /api/Label/product/search?productNameSearch=Lipitor
        /// 
        /// // Search with pagination
        /// GET /api/Label/product/search?productNameSearch=Aspirin&amp;pageNumber=1&amp;pageSize=50
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.SearchProductSummaryAsync"/>
        /// <seealso cref="LabelView.ProductSummary"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("product/search")]
        [ProducesResponseType(typeof(IEnumerable<ProductSummaryViewDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductSummaryViewDto>>> SearchProductSummary(
            [FromQuery] string productNameSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate product name search term is provided
            if (string.IsNullOrWhiteSpace(productNameSearch))
            {
                return BadRequest("Product name search term is required.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Searching product summaries: {ProductNameSearch}, Page: {PageNumber}, Size: {PageSize}",
                    productNameSearch, pageNumber, pageSize);

                var results = await DtoLabelAccess.SearchProductSummaryAsync(
                    _dbContext,
                    productNameSearch,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching product summaries for {ProductNameSearch}", productNameSearch);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while searching product summaries.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets related products by shared application number or active ingredient.
        /// Useful for finding alternatives, generics, or similar drugs.
        /// </summary>
        /// <param name="sourceProductId">
        /// Optional source product ID (decrypted) to find related products for.
        /// Either sourceProductId or sourceDocumentGuid must be provided.
        /// </param>
        /// <param name="sourceDocumentGuid">
        /// Optional source document GUID to find related products for.
        /// Either sourceProductId or sourceDocumentGuid must be provided.
        /// Use this parameter when you have the DocumentGUID from GetProductLatestLabels.
        /// </param>
        /// <param name="relationshipType">
        /// Optional filter by relationship type. Valid values:
        /// - "SameApplicationNumber": Products under the same NDA/ANDA/BLA
        /// - "SameActiveIngredient": Products with the same active ingredient(s)
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of products related to the source product.</returns>
        /// <response code="200">Returns the list of related products.</response>
        /// <response code="400">If neither sourceProductId nor sourceDocumentGuid is provided, or paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ## Usage Examples
        ///
        /// Find related products by Product ID:
        /// ```
        /// GET /api/Label/product/related?sourceProductId=12345
        /// ```
        ///
        /// Find related products by Document GUID (from GetProductLatestLabels):
        /// ```
        /// GET /api/Label/product/related?sourceDocumentGuid=12345678-1234-1234-1234-123456789012
        /// ```
        ///
        /// Filter by relationship type:
        /// ```
        /// GET /api/Label/product/related?sourceDocumentGuid=12345678-1234-1234-1234-123456789012&amp;relationshipType=SameActiveIngredient
        /// ```
        ///
        /// ## Response Format (200)
        ///
        /// ```json
        /// [
        ///   {
        ///     "RelatedProducts": {
        ///       "EncryptedRelatedProductID": "encrypted_string",
        ///       "RelatedProductName": "GENERIC LIPITOR",
        ///       "RelatedDocumentGUID": "87654321-4321-4321-4321-210987654321",
        ///       "RelationshipType": "SameActiveIngredient",
        ///       "SharedValue": "ATORVASTATIN CALCIUM"
        ///     }
        ///   }
        /// ]
        /// ```
        ///
        /// Results are ordered by RelatedProductName.
        ///
        /// ## Workflow Integration
        ///
        /// Use this endpoint after GetProductLatestLabels to find alternative products:
        /// 1. Call `/api/Label/product/latest?productNameSearch=Lipitor` to get DocumentGUID
        /// 2. Call `/api/Label/product/related?sourceDocumentGuid={DocumentGUID}` to find related products
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetRelatedProductsAsync"/>
        /// <seealso cref="LabelView.RelatedProducts"/>
        /// <seealso cref="GetProductLatestLabels"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("product/related")]
        [ProducesResponseType(typeof(IEnumerable<RelatedProductsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<RelatedProductsDto>>> GetRelatedProducts(
            [FromQuery] int? sourceProductId,
            [FromQuery] Guid? sourceDocumentGuid,
            [FromQuery] string? relationshipType,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate that at least one identifier is provided
            if ((!sourceProductId.HasValue || sourceProductId.Value <= 0) && !sourceDocumentGuid.HasValue)
            {
                return BadRequest("Either sourceProductId or sourceDocumentGuid must be provided.");
            }

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null)
            {
                return pagingValidation;
            }

            #endregion

            #region Implementation

            try
            {
                _logger.LogInformation("Getting related products for ProductID: {SourceProductId}, DocumentGUID: {SourceDocumentGuid}, RelationshipType: {RelationshipType}, Page: {PageNumber}, Size: {PageSize}",
                    sourceProductId, sourceDocumentGuid, relationshipType ?? "all", pageNumber, pageSize);

                var results = await DtoLabelAccess.GetRelatedProductsAsync(
                    _dbContext,
                    sourceProductId,
                    sourceDocumentGuid,
                    relationshipType,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was applied
                addPaginationHeaders(pageNumber, pageSize, results?.Count ?? 0);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving related products for ProductID {SourceProductId}, DocumentGUID {SourceDocumentGuid}", sourceProductId, sourceDocumentGuid);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving related products.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the API endpoint guide for AI-assisted endpoint discovery.
        /// Claude API and other AI integrations query this endpoint to understand
        /// available navigation views and usage patterns.
        /// </summary>
        /// <param name="category">
        /// Optional filter by endpoint category (e.g., "Navigation", "Search", "Summary").
        /// If not provided, returns all endpoint metadata.
        /// </param>
        /// <returns>List of API endpoint metadata for discovery purposes.</returns>
        /// <response code="200">Returns the API endpoint guide.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/guide
        /// GET /api/Label/guide?category=Navigation
        /// 
        /// This endpoint provides metadata about available view-based endpoints,
        /// including descriptions, parameters, and usage examples. Designed for
        /// programmatic discovery by AI assistants and integration tools.
        /// 
        /// Response (200):
        /// ```json
        /// [
        ///   {
        ///     "ViewName": "ProductsByApplicationNumber",
        ///     "Category": "Navigation",
        ///     "Description": "Search products by regulatory application number",
        ///     "EndpointPath": "/api/Label/application-number/search",
        ///     "Parameters": "applicationNumber (required), pageNumber, pageSize"
        ///   }
        /// ]
        /// ```
        /// 
        /// Results are ordered by Category, then ViewName.
        /// </remarks>
        /// <seealso cref="DtoLabelAccess.GetAPIEndpointGuideAsync"/>
        /// <seealso cref="LabelView.APIEndpointGuide"/>
        [HttpGet("guide")]
        [ProducesResponseType(typeof(IEnumerable<APIEndpointGuideDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<APIEndpointGuideDto>>> GetAPIEndpointGuide(
            [FromQuery] string? category)
        {
            #region Implementation

            try
            {
                _logger.LogInformation("Getting API endpoint guide. Category: {Category}", category ?? "all");

                var results = await DtoLabelAccess.GetAPIEndpointGuideAsync(
                    _dbContext,
                    category,
                    _pkEncryptionSecret,
                    _logger);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving API endpoint guide");
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving API endpoint guide.");
            }

            #endregion
        }

        #endregion Product Summary and Cross-Reference

        #region Latest Label Navigation

        /**************************************************************/
        /// <summary>
        /// Gets the latest label for each product/active ingredient combination.
        /// Returns the most recent document based on EffectiveTime for each UNII/ProductName pair.
        /// </summary>
        /// <param name="unii">
        /// Optional UNII (Unique Ingredient Identifier) code for exact match filtering.
        /// Example: "R16CO5Y76E" for aspirin.
        /// </param>
        /// <param name="productNameSearch">
        /// Optional product name search term. Supports partial and phonetic matching.
        /// </param>
        /// <param name="activeIngredientSearch">
        /// Optional active ingredient name search term. Supports partial and phonetic matching.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of latest labels matching the search criteria.</returns>
        /// <response code="200">Returns the list of latest labels.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ## Usage Examples
        ///
        /// Get latest label by UNII:
        /// ```
        /// GET /api/Label/product/latest?unii=R16CO5Y76E
        /// ```
        ///
        /// Search by product name:
        /// ```
        /// GET /api/Label/product/latest?productNameSearch=Lipitor
        /// ```
        ///
        /// Search by active ingredient name:
        /// ```
        /// GET /api/Label/product/latest?activeIngredientSearch=atorvastatin
        /// ```
        ///
        /// ## Response Format (200)
        ///
        /// ```json
        /// [
        ///   {
        ///     "ProductLatestLabel": {
        ///       "ProductName": "LIPITOR",
        ///       "ActiveIngredient": "ATORVASTATIN CALCIUM",
        ///       "UNII": "A0JWA85V8F",
        ///       "DocumentGUID": "12345678-1234-1234-1234-123456789012"
        ///     }
        ///   }
        /// ]
        /// ```
        ///
        /// Use the DocumentGUID with `/api/label/single/{documentGuid}` to retrieve the complete label.
        ///
        /// This view returns only one row per UNII/ProductName combination, selecting the document
        /// with the most recent EffectiveTime.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Find latest label for aspirin
        /// GET /api/Label/product/latest?unii=R16CO5Y76E
        ///
        /// // Find latest label by product name
        /// GET /api/Label/product/latest?productNameSearch=Lipitor&amp;pageNumber=1&amp;pageSize=25
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.GetProductLatestLabelsAsync"/>
        /// <seealso cref="LabelView.ProductLatestLabel"/>
        /// <seealso cref="GetSingleDocument"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("product/latest")]
        [ProducesResponseType(typeof(IEnumerable<ProductLatestLabelDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductLatestLabelDto>>> GetProductLatestLabels(
            [FromQuery] string? unii,
            [FromQuery] string? productNameSearch,
            [FromQuery] string? activeIngredientSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null) return pagingValidation;

            #endregion

            #region implementation

            try
            {
                // Get latest labels using the data access method
                var results = await DtoLabelAccess.GetProductLatestLabelsAsync(
                    _dbContext,
                    unii,
                    productNameSearch,
                    activeIngredientSearch,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was requested
                addPaginationHeaders(pageNumber, pageSize, results.Count);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest labels. UNII: {UNII}, Product: {Product}, Ingredient: {Ingredient}",
                    unii, productNameSearch, activeIngredientSearch);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving latest labels.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets product indication text combined with active ingredients.
        /// Returns indication section content for products filtered by UNII, product name, substance name, or indication text.
        /// </summary>
        /// <param name="unii">
        /// Optional UNII (Unique Ingredient Identifier) code for exact match filtering.
        /// Example: "R16CO5Y76E" for aspirin.
        /// </param>
        /// <param name="productNameSearch">
        /// Optional product name search term. Supports partial matching.
        /// </param>
        /// <param name="substanceNameSearch">
        /// Optional substance name search term. Supports partial matching.
        /// </param>
        /// <param name="indicationSearch">
        /// Optional clinical indication search term. Searches within indication text content.
        /// Examples: "hypertension", "diabetes", "pain"
        /// Uses partial matching - any search term can match within the indication text.
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// </param>
        /// <returns>List of product indications matching the search criteria.</returns>
        /// <response code="200">Returns the list of product indications.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// ## Usage Examples
        ///
        /// Get indications by UNII:
        /// ```
        /// GET /api/Label/product/indications?unii=R16CO5Y76E
        /// ```
        ///
        /// Search by product name:
        /// ```
        /// GET /api/Label/product/indications?productNameSearch=Lipitor
        /// ```
        ///
        /// Search by substance name:
        /// ```
        /// GET /api/Label/product/indications?substanceNameSearch=atorvastatin
        /// ```
        ///
        /// **Search by clinical indication:**
        /// ```
        /// GET /api/Label/product/indications?indicationSearch=hypertension
        /// GET /api/Label/product/indications?indicationSearch=type%202%20diabetes
        /// GET /api/Label/product/indications?indicationSearch=chronic%20pain
        /// ```
        ///
        /// ## Response Format (200)
        ///
        /// ```json
        /// [
        ///   {
        ///     "ProductIndications": {
        ///       "ProductName": "LIPITOR",
        ///       "SubstanceName": "ATORVASTATIN CALCIUM",
        ///       "UNII": "A0JWA85V8F",
        ///       "DocumentGUID": "12345678-1234-1234-1234-123456789012",
        ///       "ContentText": "LIPITOR is indicated as an adjunctive therapy to diet..."
        ///     }
        ///   }
        /// ]
        /// ```
        ///
        /// The view filters to INDICATION sections only and excludes inactive ingredients (IACT class).
        /// ContentText combines text from SectionTextContent and TextListItem.
        ///
        /// ## Indication Search Tips
        ///
        /// - Use medical terminology for best results (e.g., "hypertension" not "high blood pressure")
        /// - Multiple words are OR-matched (any term can match in indication text)
        /// - Combine with substanceNameSearch to narrow results to specific drug classes
        /// - For AI-assisted query interpretation, use POST /api/ai/interpret first
        /// </remarks>
        /// <example>
        /// <code>
        /// // Find indications for aspirin
        /// GET /api/Label/product/indications?unii=R16CO5Y76E
        ///
        /// // Find indications by product name with pagination
        /// GET /api/Label/product/indications?productNameSearch=Lipitor&amp;pageNumber=1&amp;pageSize=25
        ///
        /// // Find products indicated for hypertension
        /// GET /api/Label/product/indications?indicationSearch=hypertension
        ///
        /// // Find statins indicated for hypercholesterolemia
        /// GET /api/Label/product/indications?indicationSearch=hypercholesterolemia&amp;substanceNameSearch=statin
        /// </code>
        /// </example>
        /// <seealso cref="DtoLabelAccess.GetProductIndicationsAsync"/>
        /// <seealso cref="LabelView.ProductIndications"/>
        /// <seealso cref="SearchBySectionCode"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("product/indications")]
        [ProducesResponseType(typeof(IEnumerable<ProductIndicationsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ProductIndicationsDto>>> GetProductIndications(
            [FromQuery] string? unii,
            [FromQuery] string? productNameSearch,
            [FromQuery] string? substanceNameSearch,
            [FromQuery] string? indicationSearch,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation

            // Validate paging parameters
            var pagingValidation = validatePagingParameters(pageNumber, pageSize);
            if (pagingValidation != null) return pagingValidation;

            #endregion

            #region implementation

            try
            {
                // Get product indications using the data access method
                var results = await DtoLabelAccess.GetProductIndicationsAsync(
                    _dbContext,
                    unii,
                    productNameSearch,
                    substanceNameSearch,
                    indicationSearch,
                    _pkEncryptionSecret,
                    _logger,
                    pageNumber,
                    pageSize);

                // Add pagination headers if paging was requested
                addPaginationHeaders(pageNumber, pageSize, results.Count);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product indications. UNII: {UNII}, Product: {Product}, Substance: {Substance}, Indication: {Indication}",
                    unii, productNameSearch, substanceNameSearch, indicationSearch);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while retrieving product indications.");
            }

            #endregion
        }

        #endregion Latest Label Navigation

        #region Basic Navigation and CRUD
        /**************************************************************/
        /// <summary>
        /// Provides a list of available label sections (data tables) that can be interacted with.
        /// Each section name corresponds to a class within MedRecPro.DataModels.Label.
        /// </summary>
        /// <returns>A sorted list of section names.</returns>
        /// <response code="200">Returns the list of section names.</response>
        /// <response code="500">If an error occurs while generating the menu.</response>
        /// <remarks>
        /// GET /api/Label/SectionMenu
        ///   
        /// Response (200):
        /// ```json
        /// [
        ///   "ActiveMoiety",
        ///   "Address",
        ///   "Document",
        ///   "Organization",
        ///   ...
        /// ]
        /// ```
        /// 
        /// Uses DtoTransformer.ToEntityMenu to generate the list of available sections.
        /// Returns an empty list if an error occurs during menu generation.
        /// </remarks>
        [HttpGet("sectionMenu")]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<List<string>> GetLabelSectionMenu()
        {
            #region Implementation

            try
            {
                // Generate menu using DtoTransformer helper, fallback to empty list if null
                List<string> menu = DtoTransform.ToEntityMenu(new Label(), _logger)
                    ?? new List<string>();
                return Ok(menu);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in GetLabelSectionMenu");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while generating the menu.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves documentation for a specified label section (data model class).
        /// This includes the class summary and details for its public properties
        /// (name, type, nullability, and XML documentation summary).
        /// </summary>
        /// <param name="menuSelection">The name of the label section (e.g., "Document", "Organization")
        /// for which to retrieve documentation. This name must match a nested class within
        /// MedRecPro.DataModels.Label.</param>
        /// <returns>Detailed documentation for the specified class.</returns>
        /// <response code="200">Returns the class documentation.</response>
        /// <response code="400">If the menuSelection is invalid or not found.</response>
        /// <response code="500">If an internal server error occurs while retrieving documentation.</response>
        /// <remarks>
        /// GET /api/label/document/Documentation
        ///   
        /// Response (200):
        /// ```json
        /// {
        ///   "name": "Document",
        ///   "fullName": "MedRecPro.DataModels.Label.Document",
        ///   "summary": "Stores the main metadata for each SPL document version. Based on Section 2.1.3.",
        ///   "properties": [
        ///     {
        ///       "name": "DocumentID",
        ///       "typeName": "System.Nullable`1[[System.Int32, System.Private.CoreLib, Version=...]]",
        ///       "isNullable": true,
        ///       "summary": "Primary key for the Document table."
        ///     },
        ///     {
        ///       "name": "DocumentGUID",
        ///       "typeName": "System.Nullable`1[[System.Guid, System.Private.CoreLib, Version=...]]",
        ///       "isNullable": true,
        ///       "summary": "Globally Unique Identifier for this specific document version."
        ///     },
        ///     // ... other properties
        ///   ]
        /// }
        /// ```
        /// </remarks>       
        [HttpGet("{menuSelection}/documentation")]
        [ProducesResponseType(typeof(ClassDocumentation), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<ClassDocumentation> GetSectionDocumentation(string menuSelection)
        {
            #region Implementation
            var entityType = getEntityType(menuSelection);
            if (entityType == null)
            {
                _logger.LogWarning("GetSectionDocumentation called with invalid menuSelection: {MenuSelection}", menuSelection);

                return BadRequest($"Invalid menu selection: {menuSelection}. No matching class found within MedRecPro.DataModels.Label.");
            }

            try
            {
                var documentation = DtoTransform.GetClassDocumentation(entityType, _logger);

                if (documentation == null)
                {
                    // This case should ideally be handled by GetClassDocumentation logging or internal errors.
                    // It might mean the type was valid but something went wrong during doc generation.
                    _logger.LogError("Failed to generate documentation for type {EntityTypeFullName}", entityType.FullName);

                    return StatusCode(StatusCodes.Status500InternalServerError, $"Could not retrieve documentation for {menuSelection}.");
                }
                return Ok(documentation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting documentation for {MenuSelection}", menuSelection);
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while retrieving documentation for {menuSelection}.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves records for a specified label section, with optional paging.
        /// If paging parameters (pageNumber, pageSize) are not provided or are 
        /// null, all records for the section are returned. Each record is 
        /// transformed to include an encrypted primary key and omit the original numeric PK.
        /// </summary>
        /// <param name="menuSelection">
        /// The name of the label section (table) to query (e.g., "Document", "Organization").
        /// </param>
        /// <param name="pageNumber">
        /// Optional. The 1-based page number to retrieve.
        /// If provided, pageSize must also be provided for paging to apply.
        /// If omitted or null (and pageSize is also null/omitted), all records are returned.
        /// </param>
        /// <param name="pageSize">
        /// Optional. The number of records per page.
        /// If provided, pageNumber must also be provided for paging to apply.
        /// If omitted or null (and pageNumber is also null/omitted), all records are returned.
        /// </param>
        /// <returns>
        /// A list of records from the specified section, with encrypted IDs.
        /// If both pageNumber and pageSize are provided, returns a specific page of records. Otherwise, returns all records.
        /// </returns>
        /// <response code="200">Returns the list of records (all or paged).</response>
        /// <response code="400">
        /// If menuSelection is invalid, or if provided paging parameters are invalid (e.g., pageNumber &lt;= 0, pageSize &lt;= 0).
        /// </response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// To get all records:
        /// GET /api/Label/Section
        /// 
        /// To get paged records (e.g., page 2, 20 items per page):
        /// GET /api/label/section?pageNumber=2&amp;pageSize=20
        /// 
        /// Response (200 for paged request):
        ///   
        /// ```json
        /// [
        ///   {
        ///     "EncryptedDocumentID": "some_encrypted_string_page2_item1"
        ///     // other properties
        ///   }
        ///   // up to pageSize items
        /// ]
        /// ```
        ///   
        /// Uses reflection to invoke ReadAllAsync(int? pageNumber, int? pageSize) on the appropriate repository.
        /// The repository is expected to handle null for pageNumber or pageSize as a request for all records.
        /// If pageNumber is provided for paging, it's 1-based from the client and converted to 0-based for the repository.
        /// All numeric primary keys are replaced with encrypted equivalents for security.
        /// </remarks>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("section/{menuSelection}")]
        [ProducesResponseType(typeof(IEnumerable<Dictionary<string, object?>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Dictionary<string, object?>>>> GetSection(
            string menuSelection,
            [FromQuery] int? pageNumber,
            [FromQuery] int? pageSize)
        {
            #region Input Validation
            if (pageNumber.HasValue && pageNumber.Value <= 0)
            {
                return BadRequest($"Invalid page number: {pageNumber.Value}. Page number must be greater than 0 if provided.");
            }
            if (pageSize.HasValue && pageSize.Value <= 0)
            {
                return BadRequest($"Invalid page size: {pageSize.Value}. Page size must be greater than 0 if provided.");
            }

            // Enforce both or neither
            if (pageNumber.HasValue != pageSize.HasValue)
            {
                return BadRequest("If providing paging, both pageNumber and pageSize must be specified.");
            }
            #endregion

            #region Implementation

            var entityType = getEntityType(menuSelection);
            if (entityType == null)
            {
                _logger.LogWarning($"Invalid menu selection received: {menuSelection}");
                return BadRequest($"Invalid menu selection: {menuSelection}");
            }

            try
            {
                var repository = getRepository(entityType);

                // The repository's GetSection method accepts nullable ints.
                var readAllMethod = repository.GetType().GetMethod("ReadAllAsync", new Type[] { typeof(int?), typeof(int?) });

                if (readAllMethod == null)
                {
                    var errorMessage = $"GetSection(int?, int?) method not found on repository for {entityType.Name}. Ensure the repository implements this signature to support optional paging.";

                    _logger.LogError(errorMessage);

                    return StatusCode(StatusCodes.Status500InternalServerError, "Server configuration error: Required data access method not found.");
                }

                // pageSize is passed as is (it's either null or a positive value)
                var methodParams = new object?[] { pageNumber, pageSize };

                var task = (Task)readAllMethod.Invoke(repository, methodParams)!;
                await task;

                var resultProperty = task.GetType().GetProperty("Result");

                if (resultProperty == null)
                {
                    _logger.LogError($"Task for {readAllMethod.Name} on {entityType.Name} repository did not have a 'Result' property after completion.");
                    return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving data results.");
                }

                var entities = (IEnumerable<object>?)resultProperty.GetValue(task);

                if (entities == null)
                {
                    _logger.LogWarning($"GetSection for {entityType.Name} returned null (Page: {pageNumber}, Size: {pageSize}). Treating as empty list.");
                    entities = Enumerable.Empty<object>();
                }

                var dtoList = entities.Select(e => e.ToEntityWithEncryptedId(_pkEncryptionSecret, _logger)).ToList();

                // Add pagination headers if paging was applied and total count is available
                if (pageNumber.HasValue
                    && pageSize.HasValue)
                {
                    int totalCount = dtoList?.Count() ?? 0;
                    Response.Headers.Append("X-Page-Number", pageNumber.Value.ToString());
                    Response.Headers.Append("X-Page-Size", pageSize.Value.ToString());
                    Response.Headers.Append("X-Total-Count", totalCount.ToString());
                }

                return Ok(dtoList);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                // Log the actual exception thrown by the repository method
                _logger.LogError(ex.InnerException, $"Error executing repository's GetSection for section {menuSelection} (Client Page: {pageNumber}, Size: {pageSize}). Inner Exception: {ex.InnerException.Message}");

                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while processing your request for {menuSelection}. Details: {ex.InnerException.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving records for section {menuSelection} (Client Page: {pageNumber}, Size: {pageSize}).");

                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while processing your request for {menuSelection}.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a single "complete" label structure by its unique document identifier.
        /// Returns a hierarchical object starting with the Document and including all
        /// related child entities (authors, sections, structured bodies, relationships, etc.).
        /// </summary>
        /// <param name="documentGuid">The unique identifier (GUID) for the document to retrieve.</param>
        /// <returns>A complete, hierarchical label object for the specified document.</returns>
        /// <response code="200">Returns the complete label structure for the specified document.</response>
        /// <response code="400">If the document GUID parameter is invalid.</response>
        /// <response code="404">If no document is found with the specified GUID.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/label/single?documentGuid=12345678-1234-1234-1234-123456789012
        /// 
        /// This endpoint fetches a deep object graph for a single document. The response includes
        /// the complete hierarchy of structured bodies, authors, relationships, and authenticators.
        /// All primary keys within the structure are encrypted for security.
        /// Use this endpoint when you need to retrieve a specific document by its GUID rather than browsing paginated results.
        /// </remarks>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.Document.DocumentGUID"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("single/{documentGuid}")]
        [ProducesResponseType(typeof(Dictionary<string, object?>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Dictionary<string, object?>>> GetSingleCompleteLabel(Guid documentGuid)
        {
            #region Input Validation
            if (documentGuid == Guid.Empty)
            {
                return BadRequest("Document GUID cannot be empty.");
            }
            #endregion

            #region Implmentation
            try
            {
                // We need the specific repository for Label.Document
                var documentRepository = _serviceProvider.GetRequiredService<Repository<Label.Document>>();

                var completeLabels = await documentRepository.GetCompleteLabelsAsync(documentGuid);

                // Check if document was found
                if (completeLabels == null || !completeLabels.Any())
                {
                    _logger.LogWarning("Document with GUID {DocumentGuid} was not found.", documentGuid);
                    return NotFound($"Document with GUID {documentGuid} was not found.");
                }

                // Return the first (and should be only) document from the result
                var singleDocument = completeLabels.First();

                // Add response headers for tracking
                Response.Headers.Append("X-Document-Guid", documentGuid.ToString());
                Response.Headers.Append("X-Document-Found", "true");

                return Ok(singleDocument);
            }
            catch (NotSupportedException ex)
            {
                // This would indicate a developer error (calling the method on the wrong repository type).
                _logger.LogError(ex, "Developer error: GetCompleteLabelsAsync was called on an incorrect repository type for GUID {DocumentGuid}.", documentGuid);
                return StatusCode(StatusCodes.Status500InternalServerError, "A server configuration error occurred.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching complete label for document GUID {DocumentGuid}.", documentGuid);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a collection of "complete" label structures, with optional paging.
        /// Each item in the collection is a hierarchical object starting with a Document
        /// and including its related child entities (authors, sections, text, etc.).
        /// </summary>
        /// <param name="pageNumber">The 1-based page number to retrieve. Defaults to 1.</param>
        /// <param name="pageSize">The number of records per page. Defaults to 10.</param>
        /// <returns>A list of complete, hierarchical label objects.</returns>
        /// <response code="200">Returns the list of complete labels.</response>
        /// <response code="400">If paging parameters are invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/Label/complete?pageNumber=1&amp;pageSize=5
        /// 
        /// This endpoint fetches a deep object graph for each document. The response can be large.
        /// All primary keys within the structure are encrypted for security.
        /// </remarks>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("complete/{pageNumber?}/{pageSize?}")]
        [ProducesResponseType(typeof(IEnumerable<Dictionary<string, object?>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Dictionary<string, object?>>>> GetCompleteLabels(
            int pageNumber = 1,
            int pageSize = 10)
        {
            #region Input Validation
            if (pageNumber <= 0)
            {
                return BadRequest("Page number must be greater than 0.");
            }
            if (pageSize <= 0)
            {
                return BadRequest("Page size must be greater than 0.");
            }
            #endregion

            #region Implementation
            try
            {
                // We need the specific repository for Label.Document
                var documentRepository = _serviceProvider.GetRequiredService<Repository<Label.Document>>();

                var completeLabels = await documentRepository.GetCompleteLabelsAsync(pageNumber, pageSize);

                int totalCount = completeLabels?.Count() ?? 0;
                Response.Headers.Append("X-Page-Number", (pageNumber).ToString());
                Response.Headers.Append("X-Page-Size", (pageSize).ToString());
                Response.Headers.Append("X-Total-Count", totalCount.ToString());


                return Ok(completeLabels);
            }
            catch (NotSupportedException ex)
            {
                // This would indicate a developer error (calling the method on the wrong repository type).
                _logger.LogError(ex, "Developer error: ReadAllCompleteLabelsAsync was called on an incorrect repository type.");
                return StatusCode(StatusCodes.Status500InternalServerError, "A server configuration error occurred.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching complete labels.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// NOTE: This is a long running process (e.g. > 30 sec). Consider
        /// Using the POST method to queue the job in the background and 
        /// use polling to check on the progress. 
        /// 
        /// This generates an AI-powered comparison analysis between the original SPL XML data and the 
        /// structured DTO representation for a specific document. This endpoint leverages 
        /// Claude AI to identify data transformation differences, missing elements, and 
        /// completeness metrics between the source XML and the processed Label entity structure.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique GUID identifier of the document to analyze. This corresponds to the 
        /// DocumentGUID property in the Label.Document entity.
        /// </param>
        /// <returns>
        /// A comprehensive analysis report comparing XML source data with DTO representation,
        /// including completeness assessment, identified differences, and detailed metrics.
        /// </returns>
        /// <response code="200">Returns the comparison analysis results.</response>
        /// <response code="400">If the document GUID parameter is invalid.</response>
        /// <response code="404">If no document is found with the specified GUID.</response>
        /// <response code="500">If an internal server error occurs during analysis.</response>
        /// <remarks>
        /// **Limitation:** This endpoint only supports SPL Label document types. Other SPL sub-types
        /// (e.g., indexing files, establishment registrations, product listings) are not supported
        /// and will not produce accurate comparison results.
        /// 
        /// This endpoint delegates the comparison analysis to the ComparisonService, which handles:
        /// - Retrieving the complete label DTO structure from the database
        /// - Finding the corresponding SplData record containing original XML
        /// - Converting the DTO > JSON > Rendered SPL for comparison with original
        /// - Using Claude AI to perform intelligent difference analysis
        /// - Parsing AI response into structured comparison results
        /// 
        /// The analysis focuses on data preservation during XML-to-DTO transformation,
        /// identifying missing fields, structural differences, and completeness metrics
        /// critical for regulatory compliance and data integrity validation.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/label/comparison/analysis/12345678-1234-1234-1234-123456789012
        /// 
        /// Response:
        /// {
        ///   "documentGuid": "12345678-1234-1234-1234-123456789012",
        ///   "isComplete": true,
        ///   "completionPercentage": 95.5,
        ///   "summary": "Analysis shows high data preservation with minor formatting differences",
        ///   "differences": [
        ///     {
        ///       "type": "Missing",
        ///       "section": "ClinicalPharmacology", 
        ///       "description": "Pharmacokinetics subsection not fully preserved",
        ///       "severity": "Medium"
        ///     }
        ///   ],
        ///   "detailedAnalysis": "Full AI analysis text...",
        ///   "generatedAt": "2024-01-15T10:30:00Z"
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="Label.Document.DocumentGUID"/>
        /// <seealso cref="IComparisonService.GenerateDocumentComparisonAsync(Guid)"/>
        /// <seealso cref="GetSingleCompleteLabel(Guid)"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("comparison/analysis/{documentGuid}")]
        [ProducesResponseType(typeof(DocumentComparisonResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DocumentComparisonResult>> GetDocumentComparisonAnalysis(Guid documentGuid)
        {
            #region implementation

            #region input validation
            if (documentGuid == Guid.Empty)
            {
                _logger.LogWarning("Invalid empty GUID provided for document comparison analysis");
                return BadRequest("Document GUID cannot be empty.");
            }
            #endregion

            try
            {
                _logger.LogInformation("Starting document comparison analysis for GUID {DocumentGuid}", documentGuid);

                // Delegate to comparison service for business logic
                var comparisonService = _serviceProvider.GetRequiredService<IComparisonService>();
                var analysisResult = await comparisonService.GenerateDocumentComparisonAsync(documentGuid);

                _logger.LogInformation("Successfully completed document comparison analysis for GUID {DocumentGuid}", documentGuid);

                // Add response headers for tracking
                Response.Headers.Append("X-Document-Guid", documentGuid.ToString());
                Response.Headers.Append("X-Analysis-Type", "DocumentComparison");
                Response.Headers.Append("X-Analysis-Timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                return Ok(analysisResult);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument for document comparison analysis: {DocumentGuid}", documentGuid);
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                _logger.LogWarning(ex, "Document or related data not found for GUID {DocumentGuid}", documentGuid);
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation during document comparison for GUID {DocumentGuid}", documentGuid);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing document comparison analysis for GUID {DocumentGuid}", documentGuid);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    "An error occurred while performing document comparison analysis.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a specific record from a label section by its encrypted primary key ID.
        /// The record is transformed to include an encrypted primary key and omit the original numeric PK.
        /// </summary>
        /// <param name="menuSelection">The name of the label section (table) (e.g., "Document", "Organization").</param>
        /// <param name="encryptedId">The encrypted primary key ID of the record to retrieve.</param>
        /// <returns>The requested record with an encrypted ID, or NotFound.</returns>
        /// <response code="200">Returns the requested record.</response>
        /// <response code="400">If the menuSelection is invalid.</response>
        /// <response code="404">If the record with the specified ID is not found in the section.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// GET /api/label/{Document}/some_encrypted_string
        ///   
        /// Response (200):
        /// ```json
        /// {
        ///   "EncryptedDocumentID": "some_encrypted_string",
        ///   "DocumentGUID": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
        ///   "DocumentCode": "34391-9",
        ///   // ... other properties of Label.Document
        /// }
        /// ```
        ///   
        /// Response (404): Not Found Uses the repository's ReadByIdAsync 
        /// method which handles encrypted ID decryption internally.
        /// The returned entity has its numeric primary key replaced with 
        /// the encrypted equivalent.
        /// </remarks>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("{menuSelection}/{encryptedId}")]
        [ProducesResponseType(typeof(Dictionary<string, object?>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Dictionary<string, object?>>> GetByIdAsync(string menuSelection, string encryptedId)
        {
            #region Implementation

            // Resolve the entity type from the menu selection
            var entityType = getEntityType(menuSelection);
            if (entityType == null)
            {
                return BadRequest($"Invalid menu selection: {menuSelection}");
            }

            try
            {
                // Get the appropriate repository for this entity type
                var repository = getRepository(entityType);

                // Use reflection to invoke ReadByIdAsync method with encrypted ID parameter
                var readByIdMethod = repository.GetType().GetMethod("ReadByIdAsync", new[] { typeof(string) });
                if (readByIdMethod == null) throw new MissingMethodException($"ReadByIdAsync not found on repository for {entityType.Name}");

                // Execute the async method and await its completion
                var task = (Task)readByIdMethod.Invoke(repository, new object[] { encryptedId })!;
                await task;

                // Extract the result from the completed task
                var resultProperty = task.GetType().GetProperty("Result");
                object? entity = resultProperty?.GetValue(task);

                // Return NotFound if entity doesn't exist
                if (entity == null)
                {
                    return NotFound($"Record with ID {encryptedId} not found in section {menuSelection}.");
                }

                // Transform entity to include encrypted ID and remove numeric PK
                return Ok(entity.ToEntityWithEncryptedId(_pkEncryptionSecret, _logger));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving record {encryptedId} for section {menuSelection}.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while processing your request for {menuSelection}.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a new record in the specified label section.
        /// </summary>
        /// <param name="menuSelection">The name of the label section (table) where the record will be created (e.g., "Document", "Organization").</param>
        /// <param name="jsonData">A Json string representing the record to create. Primary key fields (e.g., "DocumentID") should be omitted or null as they are auto-generated.</param>
        /// <returns>The encrypted ID of the newly created record.</returns>
        /// <response code="201">Returns an object containing the encrypted ID of the new record and a Location header pointing to the new resource.</response>
        /// <response code="400">If menuSelection is invalid or input data is invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// POST /api/label/document
        ///   
        /// Request Body:
        /// ```json
        /// {
        ///   "DocumentID": null, (Omit or set to null)
        ///   "DocumentGUID": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
        ///   "DocumentCode": "34391-9",
        ///   // ... other properties of Label.Document
        /// }
        /// ```
        ///   
        /// Response (201):
        ///   
        /// ```json
        /// {
        ///   "encryptedId": "newly_generated_encrypted_string"
        /// }
        /// ```
        /// Header: Location: /api/Label/Document/newly_generated_encrypted_string
        ///   
        /// Primary key fields should be omitted from the request body as they are auto-generated.
        /// The response includes a Location header pointing to the newly created resource.
        /// Uses reflection to invoke CreateAsync on the appropriate repository.
        /// </remarks>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpPost("{menuSelection}")]
        [Authorize]
        [RequireUserRole(Admin)]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> CreateAsync(string menuSelection, [FromBody] object? jsonData)
        {
            #region Implementation

            string? json;

            // Resolve the entity type from the menu selection
            var entityType = getEntityType(menuSelection);

            if (entityType == null)
            {
                _logger.LogWarning("Invalid menu selection received for create: {MenuSelection}", menuSelection);
                return BadRequest($"Invalid menu selection: {menuSelection}");
            }

            // Validate that jsonData is not null
            if (jsonData == null)
            {
                _logger.LogWarning("JSON data is null for menu selection: {MenuSelection}", menuSelection);
                return BadRequest("Request body with JSON data is required.");
            }

            json = Convert.ToString(jsonData);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("JSON data is null or whitespace for menu selection: {MenuSelection}", menuSelection);
                return BadRequest("Request body with JSON data is required.");
            }

            // ModelState might not have much unless other model binders fail first.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                object? entityToCreate;

                // Deserialize the JSON string to an object of the resolved entityType
                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        // JSON has properties not in your C# class and you want to ignore them
                        MissingMemberHandling = MissingMemberHandling.Ignore,

                        // Ensures that when creating objects they are replaced if present in JSON,
                        // rather than merged with default/constructed values.
                        ObjectCreationHandling = ObjectCreationHandling.Replace
                    };

                    entityToCreate = JsonConvert.DeserializeObject(json, entityType, settings);

                    if (entityToCreate == null)
                    {
                        // This can happen if the JSON string is "null" or cannot be deserialized to the target type.
                        _logger.LogWarning("JSON deserialization resulted in a null object for {EntityType} with data: {JsonData}", entityType.Name, json);

                        return BadRequest($"Invalid JSON data. Deserialization resulted in a null object for {menuSelection}.");
                    }
                }
                catch (JsonException jsonEx) // Catches errors from Newtonsoft.Json
                {
                    _logger.LogWarning(jsonEx, "JSON deserialization failed for {EntityType} with data: {JsonData}", entityType.Name, json);

                    return BadRequest($"Invalid JSON format for {menuSelection}. Details: {jsonEx.Message}");
                }

                // Get the appropriate repository for this entity type
                var repository = getRepository(entityType);

                if (repository == null)
                {
                    _logger.LogError("Could not retrieve repository for entity type {EntityType}", entityType.FullName);

                    return StatusCode(StatusCodes.Status500InternalServerError, $"Internal configuration error for section {menuSelection}.");
                }

                // Use reflection to invoke CreateAsync method on the repository
                var createMethod = repository.GetType().GetMethod("CreateAsync", new[] { entityType });

                if (createMethod == null)
                {
                    _logger.LogError("CreateAsync method not found on repository for {EntityType}", entityType.Name);

                    // This is a server configuration issue, so throw to be caught by the generic error handlers below.
                    throw new MissingMethodException($"CreateAsync not found on repository for {entityType.Name}");
                }

                // Execute the async creation and get the encrypted ID of the new record
                var task = (Task<string?>)createMethod.Invoke(repository, new object[] { entityToCreate })!;

                // Await the task to complete the creation operation
                string? newEncryptedId = await task;

                // Validate that we received an encrypted ID for the new record
                if (string.IsNullOrWhiteSpace(newEncryptedId))
                {
                    _logger.LogError("CreateAsync for {MenuSelection} (EntityType: {EntityType}) did not return an encrypted ID. Input JSON: {JsonData}", menuSelection, entityType.Name, json);

                    return StatusCode(StatusCodes.Status500InternalServerError, "Record created, but failed to retrieve its identifier.");
                }

                _logger.LogInformation("Successfully created record in section {MenuSelection} (EntityType: {EntityType}). New Encrypted ID: {NewEncryptedId}", menuSelection, entityType.Name, newEncryptedId);

                // Return encrypted ID   
                return newEncryptedId;
            }
            catch (MissingMethodException mmEx)
            {
                _logger.LogError(mmEx, "A required repository method was not found for entity type of section {MenuSelection}.", menuSelection);

                return StatusCode(StatusCodes.Status500InternalServerError, $"Internal server configuration error for {menuSelection}.");
            }
            catch (TargetInvocationException tiEx) // Catch exceptions thrown by invoked
            {
                _logger.LogError(tiEx.InnerException ?? tiEx, "Error during repository operation for section {MenuSelection} with data: {JsonData}", menuSelection, json);

                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while creating the record in section {menuSelection}. Details: {tiEx.InnerException?.Message ?? tiEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating record for section {MenuSelection} with data: {JsonData}", menuSelection, json);

                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while processing your request for {menuSelection}.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Imports SPL data from one or more ZIP files.
        /// Each ZIP file should contain SPL XML files.
        /// </summary>
        /// <param name="files">List of ZIP files to import.</param>
        /// <param name="cancellationToken">Disconnect cancelation</param>
        /// <returns>A summary of the import operation.</returns>
        /// <response code="202">Import process queued. Check results for details.</response>
        /// <response code="400">If no files are provided or files are invalid.</response>
        /// <response code="500">If an unexpected error occurs during processing.</response>
        /// <remarks>
        /// This endpoint provides asynchronous processing of SPL ZIP file imports. The operation is queued
        /// immediately and returns an operation ID that can be used to track progress. The ZIP files are
        /// processed in background, extracting and parsing individual SPL XML files within each archive.
        /// Progress updates and status changes are tracked via the status store and can be monitored
        /// using the progress endpoint.
        /// </remarks>
        /// <example>
        /// <code>
        /// POST /api/label/import
        /// Content-Type: multipart/form-data
        /// 
        /// // Upload multiple ZIP files containing SPL XML documents
        /// // Returns: { "OperationId": "guid", "ProgressUrl": "/api/spl/import/progress/guid", ... }
        /// </code>
        /// </example>
        /// <seealso cref="SplImportService"/>
        /// <seealso cref="ImportOperationStatus"/>
        /// <seealso cref="SplZipImportResult"/>
        /// <seealso cref="Label"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpPost("import")]
        [Authorize]
        [ProducesResponseType(typeof(ImportOperationStatus), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status499ClientClosedRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadSplZips(List<IFormFile> files, CancellationToken cancellationToken)
        {
            #region Implementation
            List<BufferedFile>? bufferedFiles = null;

            var importEnabled = _configuration.GetValue<bool>("FeatureFlags:SplImportEnabled", true);

            if (!importEnabled)
            {
                return StatusCode(503, new
                {
                    error = "Import functionality is currently disabled"
                });
            }

            // Validate that files were provided in the request
            if (files == null || !files.Any())
            {
                return BadRequest("No files uploaded.");
            }

            _logger.LogInformation("Received {FileCount} files for SPL import.", files.Count);

            // Generate unique operation ID for tracking this import process
            var operationId = Guid.NewGuid().ToString();
            _logger.LogInformation("Queuing import for {FileCount} files, opId: {OpId}", files.Count, operationId);

            CancellationToken disconnectedToken = HttpContext.RequestAborted;

            CancellationTokenSource source = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken, disconnectedToken);

            long? currentUserId = getCurrentUserId();

            try
            {
                try
                {
                    bufferedFiles = await new BufferedFile().BufferFilesToTempAsync(files, cancellationToken);

                    if (bufferedFiles == null || !bufferedFiles.Any())
                    {
                        _logger.LogWarning("No valid files were buffered for import.");
                        return BadRequest("No valid files uploaded.");
                    }
                }
                catch (OperationCanceledException)
                {
                    // Clean up any partially buffered files
                    if (bufferedFiles != null)
                    {
                        foreach (var buffered in bufferedFiles)
                        {
                            try { System.IO.File.Delete(buffered.TempFilePath); } catch { }
                        }
                    }
                    return StatusCode(StatusCodes.Status499ClientClosedRequest); // 499 (non-standard) = Client Closed Request
                }

                var progressUrl = Url.Action("GetImportProgress", new { operationId });

                // Queue the background processing task
                _queue.Enqueue(operationId, async token =>
                {
                    // Update status to indicate processing has started
                    var status = new ImportOperationStatus
                    {
                        Status = "Queued",
                        PercentComplete = 0,
                        OperationId = operationId,
                        ProgressUrl = progressUrl,
                        TotalFiles = bufferedFiles.Count,
                        CurrentFile = 0
                    };

                    _statusStore.Set(operationId, status);

                    try
                    {
                        // Track current file being processed
                        int currentFileIndex = 0;

                        // Process ZIP files with progress and status callbacks
                        List<SplZipImportResult> results = await _splImportService.ProcessZipFilesAsync(
                            bufferedFiles,
                            currentUserId,
                            source.Token,
                            progress =>
                            {
                                // Update progress percentage during processing
                                status.PercentComplete = progress;
                                status.OperationId = operationId;
                                status.ProgressUrl = progressUrl;
                                _statusStore.Set(operationId, status);
                            },
                            message =>
                            {
                                // Detect file transitions - only increment on "Starting Document" which indicates a new XML file
                                // Other "Starting" messages (Author, Body, Product, etc.) are for different parsing phases of the same file
                                if (message.StartsWith("Starting Document XML Elements"))
                                {
                                    currentFileIndex++;
                                    status.CurrentFile = Math.Min(currentFileIndex, status.TotalFiles);
                                }

                                // Update status message during processing
                                status.Status = message;
                                status.OperationId = operationId;
                                status.ProgressUrl = progressUrl;
                                _statusStore.Set(operationId, status);
                            },
                            results =>
                            {
                                // Store results when processing is complete
                                status.Results = results;
                                status.OperationId = operationId;
                                status.ProgressUrl = progressUrl;
                                _statusStore.Set(operationId, status);
                            }
                        );

                        // Mark operation as completed and store results
                        status.Status = "Completed";
                        status.PercentComplete = 100;
                        status.Results = results; // fixed: assign the list
                    }
                    catch (OperationCanceledException)
                    {
                        // Handle cancellation gracefully
                        status.Status = "Canceled";
                    }
                    catch (Exception ex)
                    {
                        // Handle any processing errors
                        status.Status = "Failed";
                        status.Error = ex.Message;
                    }
                    finally
                    {
                        foreach (var buffered in bufferedFiles)
                        {
                            try { System.IO.File.Delete(buffered.TempFilePath); } catch { }
                        }
                    }

                    // Persist final status regardless of outcome
                    _statusStore.Set(operationId, status);
                });

                // Return accepted response with operation tracking information
                return Accepted(new
                {
                    OperationId = operationId,
                    ProgressUrl = Url.Action("GetImportProgress", new { operationId })
                });
            }
            catch (System.Exception ex)
            {
                // Handle any unexpected errors during queue setup
                _logger.LogError(ex, "Unhandled exception during SPL ZIP import.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred during import.");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the current progress and status of a previously queued SPL import operation.
        /// The results include the operation's current status, completion percentage, errors, and
        /// the file parsing outcomes during the import. The result summary is cached with a lifetime
        /// of one hour i.e. the URL provided in the import operation is transient.
        /// </summary>
        /// <param name="operationId">The unique identifier for the import operation to check.</param>
        /// <returns>The current status and progress information for the specified operation.</returns>
        /// <response code="200">Returns the current operation status and progress.</response>
        /// <response code="404">If the operation ID is not found or has expired.</response>
        /// <remarks>
        /// This endpoint allows clients to poll for updates on long-running import operations.
        /// The status includes completion percentage, current processing stage, any error messages,
        /// and final results once the operation completes. Operation statuses include: Queued, 
        /// Running, Completed, Canceled, and Failed.
        /// </remarks>
        /// <example>
        /// <code>
        /// GET /api/spl/import/progress/{operationId}
        /// 
        /// // Returns: ImportOperationStatus with current progress and status
        /// // { "Status": "Running", "PercentComplete": 45, "Results": null, "Error": null }
        /// </code>
        /// </example>
        /// <seealso cref="ImportOperationStatus"/>
        /// <seealso cref="SplZipImportResult"/>
        /// <seealso cref="Label"/>
        [ProducesResponseType(typeof(ImportOperationStatus), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [HttpGet("import/progress/{operationId}")]
        public IActionResult GetImportProgress(string operationId)
        {
            #region Implementation
            // Attempt to retrieve the operation status from the store
            if (_statusStore.TryGet(operationId, out var status))
                return Ok(status);

            // Return 404 if the operation ID is not found or has expired
            return NotFound();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves the progress status of a document comparison operation.
        /// </summary>
        /// <param name="operationId">The unique identifier for the comparison operation to check.</param>
        /// <returns>
        /// Returns an <see cref="IActionResult"/> containing the operation status if found,
        /// or NotFound if the operation doesn't exist or has expired.
        /// </returns>
        /// <remarks>
        /// This endpoint allows clients to poll for the status of long-running comparison operations.
        /// The operation ID is typically obtained from the initial comparison request.
        /// </remarks>
        /// <example>
        /// GET /comparison/progress/12345678-1234-1234-1234-123456789012
        /// </example>
        /// <seealso cref="Label"/>
        /// <seealso cref="ComparisonOperationStatus"/>
        /// <seealso cref="QueueDocumentComparisonAnalysis"/>
        [HttpGet("comparison/progress/{operationId}")]
        public IActionResult GetComparisonProgress(string operationId)
        {
            #region implementation
            // Use helper method for validation
            var validationResult = validateOperationId(operationId);
            if (validationResult != null) return validationResult;

            // Attempt to retrieve the operation status from the status store
            if (_statusStore.TryGet(operationId, out ComparisonOperationStatus? status) && status != null)
            {
                _logger.LogDebug("Retrieved comparison progress for operation {OperationId}: {Status}", operationId, status.Status);
                return Ok(status);
            }

            // Operation not found or has expired
            _logger.LogWarning("Comparison operation {OperationId} not found or has expired", operationId);
            return NotFound();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Queues an AI-powered comparison analysis operation for asynchronous processing to compare 
        /// original SPL XML data with structured DTO representation. This endpoint leverages Claude AI 
        /// to identify data transformation differences, missing elements, and completeness metrics 
        /// between the source XML and the processed Label entity structure for regulatory compliance validation.
        /// </summary>
        /// <param name="documentGuid">
        /// The unique GUID identifier of the document to analyze. This corresponds to the 
        /// DocumentGUID property in the Label.Document entity and must match an existing 
        /// document in the system.
        /// </param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests during the queuing process.</param>
        /// <returns>
        /// Returns an <see cref="ActionResult{T}"/> containing the initial <see cref="ComparisonOperationStatus"/>
        /// with a 202 Accepted response for successful queuing, or appropriate error responses for invalid requests.
        /// The status includes the operation ID for progress polling and the progress URL endpoint.
        /// </returns>
        /// <remarks>
        /// **Limitation:** This endpoint only supports SPL Label document types. Other SPL sub-types
        /// (e.g., indexing files, establishment registrations, product listings) are not supported
        /// and will not produce accurate comparison results.
        /// 
        /// This endpoint initiates a long-running AI-powered document comparison analysis that:
        /// - Retrieves the complete Label DTO structure from the database
        /// - Finds the corresponding SplData record containing original XML
        /// - Converts the DTO to JSON for standardized comparison
        /// - Uses Claude AI to perform intelligent difference analysis
        /// - Parses AI response into structured comparison results with completeness metrics
        /// 
        /// The analysis focuses on data preservation during XML-to-DTO transformation, identifying 
        /// missing fields, structural differences, and completeness percentages critical for 
        /// regulatory compliance and data integrity validation in pharmaceutical labeling.
        /// 
        /// Clients should use the returned operation ID to poll for progress and results via 
        /// the GetComparisonProgress endpoint. The background analysis may take several minutes 
        /// depending on document complexity and AI processing time.
        /// </remarks>
        /// <example>
        /// <code>
        /// POST /comparison/analysis/12345678-1234-1234-1234-123456789012
        /// Content-Type: application/json
        /// 
        /// Response: 202 Accepted
        /// {
        ///   "operationId": "op-67890123-4567-8901-2345-678901234567",
        ///   "status": "Queued",
        ///   "documentGuid": "12345678-1234-1234-1234-123456789012",
        ///   "progressUrl": "/comparison/progress/op-67890123-4567-8901-2345-678901234567",
        ///   "queuedAt": "2024-01-15T10:30:00Z"
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="Label"/>
        /// <seealso cref="Label.Document"/>
        /// <seealso cref="ComparisonOperationStatus"/>
        /// <seealso cref="GetComparisonProgress"/>
        /// <seealso cref="ComparisonConstants"/>
        /// <seealso cref="ComparisonService"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpPost("comparison/analysis/{documentGuid}")]
        [Authorize]
        [RequireUserRole(Admin)]
        [ProducesResponseType(typeof(ComparisonOperationStatus), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status499ClientClosedRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<ComparisonOperationStatus> QueueDocumentComparisonAnalysis(
            Guid documentGuid,
            CancellationToken cancellationToken)
        {
            #region implementation
            // Use helper method for validation
            var validationResult = validateDocumentGuid(documentGuid);
            if (validationResult != null) return validationResult;

            try
            {
                _logger.LogInformation("Queuing document comparison analysis for GUID {DocumentGuid}", documentGuid);

                // Generate unique operation identifier
                var operationId = Guid.NewGuid().ToString();
                var progressUrl = Url.Action("GetComparisonProgress", new { operationId });

                // Create linked cancellation token to handle client disconnection
                var disconnectedToken = HttpContext.RequestAborted;
                var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, disconnectedToken);

                // Use helper method to create initial status
                var status = createInitialComparisonStatus(operationId, documentGuid, progressUrl);

                // Store the initial status for client polling
                _statusStore.Set(operationId, status);

                // Queue the background processing task
                _queue.Enqueue(operationId, async token =>
                {
                    await executeComparisonAnalysisAsync(operationId, documentGuid, progressUrl, linkedTokenSource.Token);
                });

                _logger.LogInformation("Successfully queued document comparison analysis for GUID {DocumentGuid}", documentGuid);

                // Use helper method for response headers
                addComparisonResponseHeaders(documentGuid, operationId, isAsynchronous: true);

                return Accepted(status);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
                _logger.LogInformation("Document comparison analysis queuing was canceled for GUID {DocumentGuid}", documentGuid);
                return StatusCode(StatusCodes.Status499ClientClosedRequest);
            }
            catch (Exception ex)
            {
                // Handle unexpected errors
                _logger.LogError(ex, "Error queuing document comparison analysis for GUID {DocumentGuid}", documentGuid);
                return StatusCode(StatusCodes.Status500InternalServerError, ComparisonConstants.ERROR_QUEUING_FAILED);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates a populated XML document for the specified Label document GUID.
        /// Intelligently serves browser-friendly or FDA-compliant XML based on request context.
        /// </summary>
        /// <param name="documentGuid">The unique identifier for the document to process.</param>
        /// <param name="minify">(OPTIONAL default:false) Compacts XML output in post processing (might be slower)</param>
        /// <returns>HTTP response containing the populated XML document.</returns>
        /// <example>
        /// GET /api/xmldocument/generate/12345678-1234-1234-1234-123456789012
        /// GET /api/xmldocument/generate/12345678-1234-1234-1234-123456789012/true
        /// </example>
        /// <remarks>
        /// **Limitation:** This endpoint only supports SPL Label document types. Other SPL sub-types
        /// (e.g., indexing files, establishment registrations, product listings) are not supported
        /// and will not produce accurate XML output.
        /// 
        /// Returns XML with URLs for downloads/validation, or local URLs for browser viewing.
        /// Automatically detects request type based on Accept headers.
        /// Logs processing time and any errors encountered during generation.
        /// </remarks>
        /// <seealso cref="SplExportService"/>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpGet("generate/{documentGuid:guid}/{minify:bool}")]
        [ProducesResponseType(typeof(string), 200, "text/xml")]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        [ProducesResponseType(503)]
        public async Task<IActionResult> GenerateXmlDocument(Guid documentGuid, bool minify = false)
        {
            #region implementation
            try
            {
                var exportEnabled = _configuration.GetValue<bool>("FeatureFlags:SplExportEnabled", true);

                if (!exportEnabled)
                {
                    return StatusCode(503, new
                    {
                        error = "Export functionality is currently disabled"
                    });
                }

                _logger.LogInformation("Generating XML document for GUID: {DocumentGuid}", documentGuid);
                var startTime = DateTime.UtcNow;

                // Generate XML with FDA-compliant URLs
                var xmlContent = await _splExportService.ExportDocumentToSplAsync(documentGuid, minify);

                // Fix encoding to UTF-8 as required by FDA specification
                xmlContent = ensureUtf8Encoding(xmlContent);

                // Detect if request is from browser for direct viewing
                var isBrowserView = isBrowserViewRequest(Request);

                if (isBrowserView)
                {
                    // Modify URLs to local resources for browser rendering
                    xmlContent = convertToLocalResources(xmlContent);
                    _logger.LogInformation("Converted to browser-friendly format for GUID: {DocumentGuid}", documentGuid);
                }

                var processingTime = DateTime.UtcNow - startTime;
                _logger.LogInformation(
                    "Successfully generated XML document for GUID: {DocumentGuid} in {ProcessingTime}ms (Browser: {IsBrowserView})",
                    documentGuid, processingTime.TotalMilliseconds, isBrowserView);

                // Set proper content type with UTF-8 charset
                return Content(xmlContent, "application/xml; charset=utf-8");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No document found"))
            {
                _logger.LogWarning("Document not found for GUID: {DocumentGuid}", documentGuid);
                return NotFound($"Document not found for GUID: {documentGuid}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating XML document for GUID: {DocumentGuid}", documentGuid);
                return StatusCode(500, "An error occurred while generating the XML document");
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Updates an existing record in the specified label section.
        /// </summary>
        /// <param name="menuSelection">The name of the label section (table) (e.g., "Document", "Organization").</param>
        /// <param name="encryptedId">The encrypted primary key ID of the record to update.</param>
        /// <param name="jsonData">Json representing the updated record data. The primary key identified by `encryptedId` will be used; any PK in the body is ignored for identification but should match if present for data integrity.</param>
        /// <returns>No content if successful.</returns>
        /// <response code="204">If the update was successful.</response>
        /// <response code="400">If menuSelection is invalid, ID is invalid, or input data is invalid.</response>
        /// <response code="404">If the record with the specified ID is not found in the section.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// PUT /api/label/{Document}/some_encrypted_string
        ///   
        /// Request Body:
        /// ```json
        /// {
        ///   "DocumentID": (value matching decrypted 'some_encrypted_string', or can be omitted),
        ///   "DocumentGUID": "updated_guid_value",
        ///   "Title": "Updated Title",
        ///   // ... other properties to update
        /// }
        /// ```
        ///   
        /// Response (204): No Content
        ///   
        /// The primary key from the route parameter takes precedence over any PK in the request body.
        /// Validates that the record exists before attempting the update operation.
        /// Uses reflection to invoke repository methods for existence check and update.
        /// </remarks>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpPut("{menuSelection}/{encryptedId}")]
        [Authorize]
        [RequireUserRole(Admin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateAsync(string menuSelection, string encryptedId, [FromBody] object? jsonData)
        {
            #region Implementation

            string? json;

            // Resolve the entity type from the menu selection
            var entityType = getEntityType(menuSelection);
            if (entityType == null)
            {
                _logger.LogWarning("Invalid menu selection received: {MenuSelection}", menuSelection);
                return BadRequest($"Invalid menu selection: {menuSelection}");
            }

            // Validate input parameters
            if (string.IsNullOrWhiteSpace(encryptedId))
            {
                _logger.LogWarning("Encrypted ID is null or whitespace for menu selection: {MenuSelection}", menuSelection);
                return BadRequest("Encrypted ID is required.");
            }

            // Validate that jsonData is not null or empty
            if (jsonData == null)
            {
                _logger.LogWarning("JSON data is null for menu selection: {MenuSelection}", menuSelection);
                return BadRequest("Request body with JSON data is required.");
            }

            json = Convert.ToString(jsonData);

            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("JSON data is null or whitespace for menu selection: {MenuSelection}", menuSelection);
                return BadRequest("Request body with JSON data is required.");
            }

            if (!ModelState.IsValid) // Though with raw string, ModelState might not have much unless other model binders fail first.
            {
                return BadRequest(ModelState);
            }

            // Identify the primary key property for this entity type
            var pkProperty = getPrimaryKeyProperty(entityType);
            if (pkProperty == null)
            {
                _logger.LogError("Could not determine primary key for section {MenuSelection} (EntityType: {EntityType}). Update cannot proceed.", menuSelection, entityType.FullName);
                return BadRequest($"Could not determine primary key for section {menuSelection}. Update cannot proceed.");
            }

            // Decrypt the primary key from the route parameter
            if (!tryDecryptPk(encryptedId, pkProperty.PropertyType, out object? decryptedPkValue) || decryptedPkValue == null)
            {
                _logger.LogWarning("Invalid encrypted ID format or value for section {MenuSelection}. EncryptedID: {EncryptedId}", menuSelection, encryptedId);
                return BadRequest($"Invalid encrypted ID format or value for section {menuSelection}.");
            }

            try
            {
                // First, check if the entity exists using the generic repository's ReadByIdAsync
                var repository = getRepository(entityType);

                if (repository == null)
                {
                    _logger.LogError("Could not retrieve repository for entity type {EntityType}", entityType.FullName);

                    return StatusCode(StatusCodes.Status500InternalServerError, $"Internal configuration error for section {menuSelection}.");
                }

                var readByIdMethod = repository.GetType().GetMethod("ReadByIdAsync", new[] { typeof(string) });

                if (readByIdMethod == null)
                {
                    _logger.LogError("ReadByIdAsync method not found on repository for {EntityType}", entityType.Name);

                    throw new MissingMethodException($"ReadByIdAsync not found on repository for {entityType.Name}");
                }

                // Verify the record exists before attempting update
                var checkTask = (Task)readByIdMethod.Invoke(repository, new object[] { encryptedId })!;

                await checkTask;

                var checkResultProp = checkTask.GetType().GetProperty("Result");

                if (checkResultProp?.GetValue(checkTask) == null)
                {
                    _logger.LogInformation("Record with ID {EncryptedId} not found in section {MenuSelection} for update.", encryptedId, menuSelection);

                    return NotFound($"Record with ID {encryptedId} not found in section {menuSelection}.");
                }

                // Get the actual entity that was loaded and is being tracked
                object? entityToUpdate = checkResultProp?.GetValue(checkTask);

                if (entityToUpdate == null)
                {
                    _logger.LogInformation("Record with ID {EncryptedId} not found in section {MenuSelection} for update.", encryptedId, menuSelection);
                    return NotFound($"Record with ID {encryptedId} not found in section {menuSelection}.");
                }

                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        // JSON has properties not in your C# class and you want to ignore them
                        MissingMemberHandling = MissingMemberHandling.Ignore,

                        // Ensure objects are replaced, not merged 
                        ObjectCreationHandling = ObjectCreationHandling.Replace
                    };

                    JsonConvert.PopulateObject(json, entityToUpdate, settings);
                }
                catch (JsonException jsonEx) // Catches errors from Newtonsoft.Json
                {
                    _logger.LogWarning(jsonEx, "JSON deserialization failed for {EntityType} with data: {JsonData}", entityType.Name, jsonData);

                    return BadRequest($"Invalid JSON format for {menuSelection}. Details: {jsonEx.Message}");
                }

                // Set the PK property on the instance with the decrypted value from the route.
                // This ensures the PK from the URL is authoritative.
                var targetPkType = Nullable.GetUnderlyingType(pkProperty.PropertyType) ?? pkProperty.PropertyType;

                var convertedPkValue = Convert.ChangeType(decryptedPkValue, targetPkType);

                pkProperty.SetValue(entityToUpdate, convertedPkValue);

                // Execute the update operation using reflection
                var updateMethod = repository.GetType().GetMethod("UpdateAsync", new[] { entityType });

                if (updateMethod == null)
                {
                    _logger.LogError("UpdateAsync method not found on repository for {EntityType}", entityType.Name);
                    throw new MissingMethodException($"UpdateAsync not found on repository for {entityType.Name}");
                }

                var updateTask = (Task<int>)updateMethod.Invoke(repository, new object[] { entityToUpdate })!;

                var recordsAffected = await updateTask;

                _logger.LogInformation("Successfully updated record {EncryptedId} in section {MenuSelection}. Records affected: {RecordsAffected}", encryptedId, menuSelection, recordsAffected);

                return NoContent();
            }
            catch (KeyNotFoundException knfEx) // This might be thrown by your repository or related logic
            {
                _logger.LogWarning(knfEx, "Record with ID {EncryptedId} not found in section {MenuSelection} during update attempt (KeyNotFoundException).", encryptedId, menuSelection);

                return NotFound($"Record with ID {encryptedId} not found in section {menuSelection} during update attempt.");
            }
            catch (TargetInvocationException tiEx) when (tiEx.InnerException is KeyNotFoundException) // For KNF thrown inside invoked method
            {
                _logger.LogWarning(tiEx.InnerException, "Record with ID {EncryptedId} not found in section {MenuSelection} during update attempt (KeyNotFoundException via TargetInvocationException).", encryptedId, menuSelection);

                return NotFound($"Record with ID {encryptedId} not found in section {menuSelection} during update attempt.");
            }
            catch (MissingMethodException mmEx)
            {
                _logger.LogError(mmEx, "A required repository method was not found for entity type of section {MenuSelection}.", menuSelection);

                return StatusCode(StatusCodes.Status500InternalServerError, $"Internal server configuration error for {menuSelection}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating record {EncryptedId} for section {MenuSelection}.", encryptedId, menuSelection);

                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while processing your update request for {menuSelection}.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Deletes a record from the specified label section by its encrypted primary key ID.
        /// </summary>
        /// <param name="menuSelection">The name of the label section (table) (e.g., "Document", "Organization").</param>
        /// <param name="encryptedId">The encrypted primary key ID of the record to delete.</param>
        /// <returns>No content if successful.</returns>
        /// <response code="204">If the deletion was successful.</response>
        /// <response code="400">If the menuSelection is invalid.</response>
        /// <response code="404">If the record with the specified ID is not found in the section.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <remarks>
        /// DELETE /api/label/{Document}/some_encrypted_string
        ///   
        /// Response (204): No Content  
        /// Response (404): Not Found
        ///   
        /// Uses the repository's DeleteAsync method which handles encrypted ID decryption internally.
        /// The Repository throws KeyNotFoundException for non-existent records.
        /// Handles different exception types to provide appropriate HTTP status codes.
        /// </remarks>
        [DatabaseLimit(OperationCriticality.Normal, Wait = 100)]
        [DatabaseIntensive(OperationCriticality.Critical)]
        [HttpDelete("{menuSelection}/{encryptedId}")]
        [Authorize]
        [RequireUserRole(Admin)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteAsync(string menuSelection, string encryptedId)
        {
            #region Implementation

            // Resolve the entity type from the menu selection
            var entityType = getEntityType(menuSelection);
            if (entityType == null)
            {
                return BadRequest($"Invalid menu selection: {menuSelection}");
            }

            // Validate the encrypted ID parameter
            if (string.IsNullOrWhiteSpace(encryptedId))
            {
                return BadRequest("Encrypted ID cannot be empty.");
            }

            try
            {
                // Get the appropriate repository for this entity type
                var repository = getRepository(entityType);

                // Use reflection to invoke DeleteAsync method with encrypted ID parameter
                var deleteMethod = repository.GetType().GetMethod("DeleteAsync", new[] { typeof(string) });
                if (deleteMethod == null) throw new MissingMethodException($"DeleteAsync not found on repository for {entityType.Name}");

                // Execute the async deletion and get the number of affected rows
                var task = (Task<int>)deleteMethod.Invoke(repository, new object[] { encryptedId })!;
                var rowsAffected = await task;

                // Repository.DeleteAsync(string encryptedId) throws KeyNotFoundException if not found.
                // So if we reach here, it was successful or an unhandled error occurred.
                // If it returned 0 without exception (e.g., if FindAsync returned null and it didn't throw),
                // then it would be a NotFound scenario. However, the current Repository throws.
                if (rowsAffected == 0)
                {
                    return NotFound($"record with id {encryptedId} not found in section {menuSelection} for deletion, or no rows affected.");
                }

                return NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to decrypt ID")) // From Repository
            {
                _logger.LogWarning(ex, $"Decryption failed for ID {encryptedId} in section {menuSelection} during delete operation.");
                return BadRequest($"Invalid encrypted ID format for section {menuSelection}.");
            }
            catch (KeyNotFoundException ex) // From Repository
            {
                _logger.LogWarning(ex, $"Record with ID {encryptedId} not found in section {menuSelection} for deletion.");
                return NotFound($"Record with ID {encryptedId} not found in section {menuSelection}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting record {encryptedId} for section {menuSelection}.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while processing your request for {menuSelection}.");
            }

            #endregion
        }
    } 
    #endregion
}