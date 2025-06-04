using Microsoft.AspNetCore.Mvc;
using System.Reflection;

using MedRecPro.DataModels; // From LabelClasses.cs
using MedRecPro.DataAccess; // From LabelDataAccess.cs (GenericRepository)
using MedRecPro.Helpers;   // From DtoTransformer.cs (DtoTransformer, StringCipher)

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
        #region implementation

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

        #endregion

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the LabelController with required dependencies.
        /// </summary>
        /// <param name="serviceProvider">Service provider for dependency injection</param>
        /// <param name="configuration">Configuration provider for application settings</param>
        /// <param name="logger">Logger instance for this controller</param>
        /// <param name="stringCipher">String cipher utility for encryption operations</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null</exception>
        /// <exception cref="InvalidOperationException">Thrown when PKSecret configuration is missing</exception>
        public LabelController(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<LabelController> logger,
            StringCipher stringCipher)
        {
            #region implementation

            // Validate all required dependencies are provided
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _stringCipher = stringCipher ?? throw new ArgumentNullException(nameof(stringCipher));

            // Retrieve and validate the primary key encryption secret from configuration
            _pkEncryptionSecret = _configuration.GetSection("Security:DB:PKSecret").Value
                ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing or empty.");

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
            #region implementation

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
        /// <returns>An instance of GenericRepository&lt;T&gt; for the specified type</returns>
        /// <exception cref="InvalidOperationException">Thrown when repository cannot be resolved</exception>
        /// <remarks>
        /// Uses the service provider to resolve a GenericRepository&lt;T&gt; instance.
        /// The repository and its dependencies must be properly registered in DI container.
        /// </remarks>
        private object getRepository(Type entityType)
        {
            #region implementation

            // Create the generic repository type for the specific entity
            var repoType = typeof(GenericRepository<>).MakeGenericType(entityType);

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
        /// Does not use EF Core metadata unlike GenericRepository constructor.
        /// </remarks>
        private PropertyInfo? getPrimaryKeyProperty(Type entityType)
        {
            #region implementation

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
            // unlike the GenericRepository's constructor. 
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
            #region implementation

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
        /// Deserializes a dictionary of property values into an entity instance using reflection.
        /// </summary>
        /// <param name="data">Dictionary containing property names and values to set</param>
        /// <param name="entityType">The target entity type to create and populate</param>
        /// <param name="pkPropertyToExclude">Optional primary key property to exclude from deserialization</param>
        /// <returns>The populated entity instance, or null if creation failed</returns>
        /// <remarks>
        /// Uses reflection to set properties on the entity instance.
        /// Handles type conversion for common types including enums, Guids, and nullable types.
        /// Logs warnings for type mismatches and continues processing other properties.
        /// Excludes specified primary key property to prevent overwriting route-based PKs.
        /// </remarks>
        private object? deserializeFromDictionary(Dictionary<string, object?> data, Type entityType, PropertyInfo? pkPropertyToExclude = null)
        {
            #region implementation

            if (data == null) return null;

            // Create a new instance of the target entity type
            var entity = Activator.CreateInstance(entityType);

            if (entity == null)
            {
                _logger.LogError($"Failed to create instance of type {entityType.FullName}");
                return null;
            }

            // Iterate through all public instance properties of the entity
            foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Skip the primary key property if specified (used during updates)
                if (pkPropertyToExclude != null && prop.Name == pkPropertyToExclude.Name) continue;

                // Only process writable properties that exist in the input data
                if (prop.CanWrite && data.TryGetValue(prop.Name, out var value))
                {
                    try
                    {
                        // Handle null values by setting property to null
                        if (value == null)
                        {
                            prop.SetValue(entity, null);
                        }
                        else
                        {
                            // Get the target type, handling nullable types
                            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                            object? convertedValue;

                            // Handle type conversion based on value and target types
                            if (value is IConvertible) // Check if value itself is IConvertible
                            {
                                if (targetType.IsEnum)
                                {
                                    convertedValue = Enum.Parse(targetType, value.ToString()!);
                                }
                                else if (targetType == typeof(Guid) && value is string sGuid)
                                {
                                    convertedValue = Guid.Parse(sGuid);
                                }
                                else
                                {
                                    convertedValue = Convert.ChangeType(value, targetType);
                                }
                            }
                            else if (targetType == typeof(Guid) && value is Guid guidValue) // Handle if it's already a Guid
                            {
                                convertedValue = guidValue;
                            }
                            else if (targetType == typeof(DateTime) && value is DateTime dtValue) // Handle if it's already a DateTime
                            {
                                convertedValue = dtValue;
                            }
                            else // If not IConvertible (e.g. complex type, or already correct type)
                            {
                                // Check if the value is already assignable to the property type
                                if (prop.PropertyType.IsInstanceOfType(value))
                                {
                                    convertedValue = value;
                                }
                                else
                                {
                                    _logger.LogWarning($"Type mismatch for property {prop.Name} on {entityType.Name}. Expected assignable from {prop.PropertyType}, got {value.GetType()}. Value: {value}. Skipping.");
                                    continue;
                                }
                            }

                            // Set the converted value on the entity property
                            prop.SetValue(entity, convertedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Error setting property {prop.Name} on type {entityType.Name} with value '{value}'. Target type: {prop.PropertyType}.");
                    }
                }
            }

            return entity;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Provides a list of available label sections (data tables) that can be interacted with.
        /// Each section name corresponds to a class within MedRecPro.DataModels.Label.
        /// </summary>
        /// <returns>A sorted list of section names.</returns>
        /// <response code="200">Returns the list of section names.</response>
        /// <response code="500">If an error occurs while generating the menu.</response>
        /// <example>
        /// GET /api/Label/SectionMenu
        /// Response (200):
        /// [
        ///   "ActiveMoiety",
        ///   "Address",
        ///   "Document",
        ///   "Organization",
        ///   ...
        /// ]
        /// </example>
        /// <remarks>
        /// Uses DtoTransformer.ToEntityMenu to generate the list of available sections.
        /// Returns an empty list if an error occurs during menu generation.
        /// </remarks>
        [HttpGet("SectionMenu")]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<List<string>> GetLabelSectionMenu()
        {
            #region implementation

            try
            {
                // Generate menu using DtoTransformer helper, fallback to empty list if null
                List<string> menu = DtoTransformer.ToEntityMenu(new Label(), _logger)
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
        /// <example>
        /// GET /api/Label/Document/Documentation
        /// Response (200):
        /// {
        ///   "name": "Document",
        ///   "fullName": "MedRecPro.DataModels.Label+Document",
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
        ///       "summary": "Globally Unique Identifier for this specific document version ($gt;id root$lt;)."
        ///     },
        ///     // ... other properties
        ///   ]
        /// }
        /// </example>
        [HttpGet("{menuSelection}/Documentation")]
        [ProducesResponseType(typeof(ClassDocumentation), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public ActionResult<ClassDocumentation> GetSectionDocumentation(string menuSelection)
        {
            var entityType = getEntityType(menuSelection);
            if (entityType == null)
            {
                _logger.LogWarning("GetSectionDocumentation called with invalid menuSelection: {MenuSelection}", menuSelection);

                return BadRequest($"Invalid menu selection: {menuSelection}. No matching class found within MedRecPro.DataModels.Label.");
            }

            try
            {
                var documentation = DtoTransformer.GetClassDocumentation(entityType, _logger);

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
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves all records for a specified label section.
        /// Each record is transformed to include an encrypted primary key and omit the original numeric PK.
        /// </summary>
        /// <param name="menuSelection">The name of the label section (table) to query (e.g., "Document", "Organization").</param>
        /// <returns>A list of records from the specified section, with encrypted IDs.</returns>
        /// <response code="200">Returns the list of records.</response>
        /// <response code="400">If the menuSelection is invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <example>
        /// GET /api/Label/Document
        /// Response (200):
        /// [
        ///   {
        ///     "EncryptedDocumentID": "some_encrypted_string_1",
        ///     "DocumentGUID": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
        ///     "DocumentCode": "34391-9",
        ///     // ... other properties of Label.Document
        ///   },
        ///   {
        ///     "EncryptedDocumentID": "some_encrypted_string_2",
        ///     // ...
        ///   }
        /// ]
        /// </example>
        /// <remarks>
        /// Uses reflection to invoke ReadAllAsync on the appropriate repository.
        /// All numeric primary keys are replaced with encrypted equivalents for security.
        /// </remarks>
        [HttpGet("{menuSelection}")]
        [ProducesResponseType(typeof(IEnumerable<Dictionary<string, object?>>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<Dictionary<string, object?>>>> GetAllAsync(string menuSelection)
        {
            #region implementation

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

                // Use reflection to invoke ReadAllAsync method on the repository
                var readAllMethod = repository.GetType().GetMethod("ReadAllAsync");
                if (readAllMethod == null) throw new MissingMethodException($"ReadAllAsync not found on repository for {entityType.Name}");

                // Execute the async method and await its completion
                var task = (Task)readAllMethod.Invoke(repository, null)!;
                await task;

                // Extract the result from the completed task
                var resultProperty = task.GetType().GetProperty("Result");
                var entities = (IEnumerable<object>)resultProperty?.GetValue(task)!;

                // Transform each entity to include encrypted ID and remove numeric PK
                var dtoList = entities.Select(e => e.ToEntityWithEncryptedId(_pkEncryptionSecret, _logger)).ToList();

                return Ok(dtoList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving all records for section {menuSelection}.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while processing your request for {menuSelection}.");
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
        /// <example>
        /// GET /api/Label/Document/some_encrypted_string
        /// Response (200):
        /// {
        ///   "EncryptedDocumentID": "some_encrypted_string",
        ///   "DocumentGUID": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
        ///   "DocumentCode": "34391-9",
        ///   // ... other properties of Label.Document
        /// }
        /// Response (404): Not Found
        /// </example>
        /// <remarks>
        /// Uses the repository's ReadByIdAsync method which handles encrypted ID decryption internally.
        /// The returned entity has its numeric primary key replaced with the encrypted equivalent.
        /// </remarks>
        [HttpGet("{menuSelection}/{encryptedId}")]
        [ProducesResponseType(typeof(Dictionary<string, object?>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Dictionary<string, object?>>> GetByIdAsync(string menuSelection, string encryptedId)
        {
            #region implementation

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
        /// <param name="data">A dictionary representing the record to create. Primary key fields (e.g., "DocumentID") should be omitted or null as they are auto-generated.</param>
        /// <returns>The encrypted ID of the newly created record.</returns>
        /// <response code="201">Returns an object containing the encrypted ID of the new record and a Location header pointing to the new resource.</response>
        /// <response code="400">If menuSelection is invalid or input data is invalid.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <example>
        /// POST /api/Label/Document
        /// Request Body:
        /// {
        ///   // "DocumentID": null, (Omit or set to null)
        ///   "DocumentGUID": "a1b2c3d4-e5f6-7890-1234-567890abcdef",
        ///   "DocumentCode": "34391-9",
        ///   // ... other properties of Label.Document
        /// }
        /// Response (201):
        /// {
        ///   "encryptedId": "newly_generated_encrypted_string"
        /// }
        /// Header: Location: /api/Label/Document/newly_generated_encrypted_string
        /// </example>
        /// <remarks>
        /// Primary key fields should be omitted from the request body as they are auto-generated.
        /// The response includes a Location header pointing to the newly created resource.
        /// Uses reflection to invoke CreateAsync on the appropriate repository.
        /// </remarks>
        [HttpPost("{menuSelection}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> CreateAsync(string menuSelection, [FromBody] Dictionary<string, object?> data)
        {
            #region implementation

            // Resolve the entity type from the menu selection
            var entityType = getEntityType(menuSelection);
            if (entityType == null)
            {
                return BadRequest($"Invalid menu selection: {menuSelection}");
            }

            // Validate input data and model state
            if (data == null || !ModelState.IsValid) // ModelState might not be very effective for Dictionary
            {
                return BadRequest(ModelState.IsValid ? "Input data is null." : ModelState);
            }

            try
            {
                // Deserialize the dictionary data into an entity instance
                object? entityInstance = deserializeFromDictionary(data, entityType);
                if (entityInstance == null)
                {
                    return BadRequest("Failed to deserialize input data.");
                }

                // Get the appropriate repository for this entity type
                var repository = getRepository(entityType);

                // Use reflection to invoke CreateAsync method on the repository
                var createMethod = repository.GetType().GetMethod("CreateAsync", new[] { entityType });
                if (createMethod == null) throw new MissingMethodException($"CreateAsync not found on repository for {entityType.Name}");

                // Execute the async creation and get the encrypted ID of the new record
                var task = (Task<string?>)createMethod.Invoke(repository, new object[] { entityInstance })!;
                string? newEncryptedId = await task;

                // Validate that we received an encrypted ID for the new record
                if (string.IsNullOrWhiteSpace(newEncryptedId))
                {
                    _logger.LogError($"CreateAsync for {menuSelection} did not return an encrypted ID.");
                    return StatusCode(StatusCodes.Status500InternalServerError, "Record created, but failed to retrieve its identifier.");
                }

                // Return Created response with Location header and encrypted ID
                return CreatedAtAction(nameof(GetByIdAsync),
                                       new { menuSelection = menuSelection, encryptedId = newEncryptedId },
                                       new { encryptedId = newEncryptedId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating record for section {menuSelection}.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while processing your request for {menuSelection}.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Updates an existing record in the specified label section.
        /// </summary>
        /// <param name="menuSelection">The name of the label section (table) (e.g., "Document", "Organization").</param>
        /// <param name="encryptedId">The encrypted primary key ID of the record to update.</param>
        /// <param name="data">A dictionary representing the updated record data. The primary key identified by `encryptedId` will be used; any PK in the body is ignored for identification but should match if present for data integrity.</param>
        /// <returns>No content if successful.</returns>
        /// <response code="204">If the update was successful.</response>
        /// <response code="400">If menuSelection is invalid, ID is invalid, or input data is invalid.</response>
        /// <response code="404">If the record with the specified ID is not found in the section.</response>
        /// <response code="500">If an internal server error occurs.</response>
        /// <example>
        /// PUT /api/Label/Document/some_encrypted_string
        /// Request Body:
        /// {
        ///   // "DocumentID": (value matching decrypted 'some_encrypted_string', or can be omitted),
        ///   "DocumentGUID": "updated_guid_value",
        ///   "Title": "Updated Title",
        ///   // ... other properties to update
        /// }
        /// Response (204): No Content
        /// </example>
        /// <remarks>
        /// The primary key from the route parameter takes precedence over any PK in the request body.
        /// Validates that the record exists before attempting the update operation.
        /// Uses reflection to invoke repository methods for existence check and update.
        /// </remarks>
        [HttpPut("{menuSelection}/{encryptedId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateAsync(string menuSelection, string encryptedId, [FromBody] Dictionary<string, object?> data)
        {
            #region implementation

            // Resolve the entity type from the menu selection
            var entityType = getEntityType(menuSelection);
            if (entityType == null)
            {
                return BadRequest($"Invalid menu selection: {menuSelection}");
            }

            // Validate input parameters and model state
            if (data == null || string.IsNullOrWhiteSpace(encryptedId) || !ModelState.IsValid)
            {
                return BadRequest(ModelState.IsValid ? "Input data or ID is invalid." : ModelState);
            }

            // Identify the primary key property for this entity type
            var pkProperty = getPrimaryKeyProperty(entityType);
            if (pkProperty == null)
            {
                return BadRequest($"Could not determine primary key for section {menuSelection}. Update cannot proceed.");
            }

            // Decrypt the primary key from the route parameter
            if (!tryDecryptPk(encryptedId, pkProperty.PropertyType, out object? decryptedPkValue) || decryptedPkValue == null)
            {
                return BadRequest($"Invalid encrypted ID format or value for section {menuSelection}.");
            }

            try
            {
                // First, check if the entity exists using the generic repository's ReadByIdAsync
                // This ensures we are trying to update an existing record.
                var repository = getRepository(entityType);
                var readByIdMethod = repository.GetType().GetMethod("ReadByIdAsync", new[] { typeof(string) });
                if (readByIdMethod == null) throw new MissingMethodException($"ReadByIdAsync not found on repository for {entityType.Name}");

                // Verify the record exists before attempting update
                var checkTask = (Task)readByIdMethod.Invoke(repository, new object[] { encryptedId })!;
                await checkTask;
                var checkResultProp = checkTask.GetType().GetProperty("Result");
                if (checkResultProp?.GetValue(checkTask) == null)
                {
                    return NotFound($"Record with ID {encryptedId} not found in section {menuSelection}.");
                }

                // Deserialize, excluding the PK property from dictionary values initially
                object? entityInstance = deserializeFromDictionary(data, entityType, pkProperty);
                if (entityInstance == null)
                {
                    return BadRequest("Failed to deserialize input data for update.");
                }

                // Set the PK property on the instance with the decrypted value from the route
                pkProperty.SetValue(entityInstance, Convert.ChangeType(decryptedPkValue, Nullable.GetUnderlyingType(pkProperty.PropertyType) ?? pkProperty.PropertyType));

                // Execute the update operation using reflection
                var updateMethod = repository.GetType().GetMethod("UpdateAsync", new[] { entityType });
                if (updateMethod == null) throw new MissingMethodException($"UpdateAsync not found on repository for {entityType.Name}");

                var updateTask = (Task<int>)updateMethod.Invoke(repository, new object[] { entityInstance })!;
                await updateTask;

                return NoContent();
            }
            catch (KeyNotFoundException) // Could be thrown by GenericRepo if FindAsync fails internally before update
            {
                return NotFound($"Record with ID {encryptedId} not found in section {menuSelection} during update attempt.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating record {encryptedId} for section {menuSelection}.");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while processing your request for {menuSelection}.");
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
        /// <example>
        /// DELETE /api/Label/Document/some_encrypted_string
        /// Response (204): No Content
        /// Response (404): Not Found
        /// </example>
        /// <remarks>
        /// Uses the repository's DeleteAsync method which handles encrypted ID decryption internally.
        /// The GenericRepository throws KeyNotFoundException for non-existent records.
        /// Handles different exception types to provide appropriate HTTP status codes.
        /// </remarks>
        [HttpDelete("{menuSelection}/{encryptedId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteAsync(string menuSelection, string encryptedId)
        {
            #region implementation

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

                // GenericRepository.DeleteAsync(string encryptedId) throws KeyNotFoundException if not found.
                // So if we reach here, it was successful or an unhandled error occurred.
                // If it returned 0 without exception (e.g., if FindAsync returned null and it didn't throw),
                // then it would be a NotFound scenario. However, the current GenericRepository throws.
                if (rowsAffected == 0)
                {
                    return NotFound($"record with id {encryptedId} not found in section {menuSelection} for deletion, or no rows affected.");
                }

                return NoContent();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to decrypt ID")) // From GenericRepository
            {
                _logger.LogWarning(ex, $"Decryption failed for ID {encryptedId} in section {menuSelection} during delete operation.");
                return BadRequest($"Invalid encrypted ID format for section {menuSelection}.");
            }
            catch (KeyNotFoundException ex) // From GenericRepository
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