using MedRecPro.Data;
using MedRecPro.DataAccess; // From LabelDataAccess.cs (Repository)
using MedRecPro.Helpers;   // From DtoTransformer.cs (DtoTransformer, StringCipher)
using MedRecPro.Models; // From LabelClasses.cs
using MedRecPro.Models.Extensions;
using MedRecPro.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    [Route("api/[controller]")]
    public class LabelController : ControllerBase
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

        private readonly IBackgroundTaskQueueService _queue;

        private readonly IOperationStatusStore _statusStore;

        private readonly IServiceScopeFactory _scopeFactory;

        private readonly ApplicationDbContext _dbContext;

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
            ApplicationDbContext applicationDbContext)
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

            #endregion
        }

        #region Private Methods
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
        // <summary>
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

        #endregion

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
        /// This endpoint delegates the comparison analysis to the ComparisonService, which handles:
        /// - Retrieving the complete label DTO structure from the database
        /// - Finding the corresponding SplData record containing original XML
        /// - Converting the DTO to JSON for standardized comparison
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
        [HttpPost("{menuSelection}")]
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
                        ProgressUrl = progressUrl
                    };

                    _statusStore.Set(operationId, status);

                    try
                    {
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
        [HttpPost("comparison/analysis/{documentGuid}")]
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
        /// Processes the XML template with flow control and returns the populated result.
        /// </summary>
        /// <param name="documentGuid">The unique identifier for the document to process.</param>
        /// <returns>HTTP response containing the populated XML document.</returns>
        /// <example>
        /// GET /api/xmldocument/generate/12345678-1234-1234-1234-123456789012
        /// </example>
        /// <remarks>
        /// Returns the populated XML as text/xml content type.
        /// Logs processing time and any errors encountered during generation.
        /// </remarks>
        [HttpGet("generate/{documentGuid:guid}")]
        [ProducesResponseType(typeof(string), 200, "text/xml")]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GenerateXmlDocument(Guid documentGuid)
        {
            #region implementation
            try
            {
                _logger.LogInformation("Generating XML document for GUID: {DocumentGuid}", documentGuid);

                var startTime = DateTime.UtcNow;
                var splService = new SplExportService(_dbContext, _pkEncryptionSecret, _logger);
                var xmlContent = await splService.ExportDocumentToSplAsync(documentGuid);
                var processingTime = DateTime.UtcNow - startTime;

                _logger.LogInformation("Successfully generated XML document for GUID: {DocumentGuid} in {ProcessingTime}ms",
                    documentGuid, processingTime.TotalMilliseconds);

                return Content(xmlContent, "text/xml");
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
        [HttpPut("{menuSelection}/{encryptedId}")]
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
        [HttpDelete("{menuSelection}/{encryptedId}")]
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
}