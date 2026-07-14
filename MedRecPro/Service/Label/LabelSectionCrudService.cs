using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections;
using System.Reflection;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Identifies the established outcome of a dynamic Label section operation.
    /// </summary>
    /// <remarks>
    /// Validation and missing-resource outcomes are represented as values so the MVC boundary can
    /// preserve its existing HTTP responses without using exceptions for normal control flow.
    /// </remarks>
    /// <seealso cref="SectionCrudOutcome"/>
    public enum SectionCrudStatus
    {
        Success,
        InvalidInput,
        NotFound
    }

    /**************************************************************/
    /// <summary>
    /// Carries a dynamic Label section operation result to the HTTP boundary.
    /// </summary>
    /// <param name="Status">The classified completion status.</param>
    /// <param name="Payload">The response payload for a successful operation.</param>
    /// <param name="Error">The established validation or not-found message.</param>
    /// <seealso cref="SectionCrudStatus"/>
    public sealed record SectionCrudOutcome(SectionCrudStatus Status, object? Payload = null, string? Error = null);

    /**************************************************************/
    /// <summary>
    /// Provides dynamic Label section CRUD and metadata operations below the MVC boundary.
    /// </summary>
    /// <remarks>
    /// This service is the sole owner of nested-type discovery, generic repository resolution,
    /// reflection invocation, JSON projection, and encrypted identifier handling for the legacy
    /// dynamic-section endpoints.
    /// </remarks>
    /// <seealso cref="MedRecPro.Api.Controllers.LabelSectionController"/>
    public interface ILabelSectionCrudService
    {
        /**************************************************************/
        /// <summary>Gets the supported dynamic Label section names.</summary>
        /// <returns>The available section menu.</returns>
        /// <seealso cref="Label"/>
        List<string> GetMenu();

        /**************************************************************/
        /// <summary>Gets documentation for a dynamic Label section.</summary>
        /// <param name="menuSelection">The requested nested Label type name.</param>
        /// <returns>A classified documentation outcome.</returns>
        /// <seealso cref="ClassDocumentation"/>
        SectionCrudOutcome GetDocumentation(string menuSelection);

        /**************************************************************/
        /// <summary>Reads a dynamic Label section with optional paging.</summary>
        /// <param name="menuSelection">The requested nested Label type name.</param>
        /// <param name="pageNumber">The optional one-based page number.</param>
        /// <param name="pageSize">The optional page size.</param>
        /// <param name="cancellationToken">Cancellation propagated from the request boundary.</param>
        /// <returns>A classified collection outcome.</returns>
        /// <seealso cref="Label"/>
        Task<SectionCrudOutcome> GetAsync(string menuSelection, int? pageNumber, int? pageSize, CancellationToken cancellationToken);

        /**************************************************************/
        /// <summary>Reads one dynamic Label section entity by encrypted identifier.</summary>
        /// <param name="menuSelection">The requested nested Label type name.</param>
        /// <param name="encryptedId">The encrypted entity identifier.</param>
        /// <param name="cancellationToken">Cancellation propagated from the request boundary.</param>
        /// <returns>A classified entity outcome.</returns>
        /// <seealso cref="IPrimaryKeyCipher"/>
        Task<SectionCrudOutcome> GetByIdAsync(string menuSelection, string encryptedId, CancellationToken cancellationToken);

        /**************************************************************/
        /// <summary>Creates a dynamic Label section entity from its JSON representation.</summary>
        /// <param name="menuSelection">The requested nested Label type name.</param>
        /// <param name="json">The JSON request body.</param>
        /// <param name="cancellationToken">Cancellation propagated from the request boundary.</param>
        /// <returns>A classified outcome whose payload is the encrypted identifier.</returns>
        /// <seealso cref="IPrimaryKeyCipher"/>
        Task<SectionCrudOutcome> CreateAsync(string menuSelection, string? json, CancellationToken cancellationToken);

        /**************************************************************/
        /// <summary>Updates a dynamic Label section entity from its JSON representation.</summary>
        /// <param name="menuSelection">The requested nested Label type name.</param>
        /// <param name="encryptedId">The encrypted entity identifier.</param>
        /// <param name="json">The JSON request body.</param>
        /// <param name="cancellationToken">Cancellation propagated from the request boundary.</param>
        /// <returns>A classified update outcome.</returns>
        /// <seealso cref="IPrimaryKeyCipher"/>
        Task<SectionCrudOutcome> UpdateAsync(string menuSelection, string encryptedId, string? json, CancellationToken cancellationToken);

        /**************************************************************/
        /// <summary>Deletes a dynamic Label section entity by encrypted identifier.</summary>
        /// <param name="menuSelection">The requested nested Label type name.</param>
        /// <param name="encryptedId">The encrypted entity identifier.</param>
        /// <param name="cancellationToken">Cancellation propagated from the request boundary.</param>
        /// <returns>A classified delete outcome.</returns>
        /// <seealso cref="IPrimaryKeyCipher"/>
        Task<SectionCrudOutcome> DeleteAsync(string menuSelection, string encryptedId, CancellationToken cancellationToken);
    }

    /**************************************************************/
    /// <summary>
    /// Implements the legacy dynamic Label section boundary.
    /// </summary>
    /// <seealso cref="ILabelSectionCrudService"/>
    public sealed class LabelSectionCrudService : ILabelSectionCrudService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LabelSectionCrudService> _logger;
        private readonly IPrimaryKeyCipher _primaryKeyCipher;

        /**************************************************************/
        /// <summary>Initializes a new instance of the dynamic section service.</summary>
        /// <param name="serviceProvider">Provider used only inside the dynamic generic repository boundary.</param>
        /// <param name="logger">Logger for dynamic section diagnostics.</param>
        /// <param name="primaryKeyCipher">Cipher for encrypted entity identifiers.</param>
        /// <seealso cref="IPrimaryKeyCipher"/>
        public LabelSectionCrudService(IServiceProvider serviceProvider, ILogger<LabelSectionCrudService> logger, IPrimaryKeyCipher primaryKeyCipher)
        {
            #region implementation

            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _primaryKeyCipher = primaryKeyCipher ?? throw new ArgumentNullException(nameof(primaryKeyCipher));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public List<string> GetMenu()
        {
            #region implementation

            return DtoTransform.ToEntityMenu(new Label(), _logger) ?? new List<string>();

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public SectionCrudOutcome GetDocumentation(string menuSelection)
        {
            #region implementation

            var entityType = getEntityType(menuSelection);
            if (entityType is null)
            {
                return invalid($"Invalid menu selection: {menuSelection}. No matching class found within MedRecPro.DataModels.Label.");
            }

            var documentation = DtoTransform.GetClassDocumentation(entityType, _logger);
            return documentation is null
                ? throw new InvalidOperationException($"Documentation generation unexpectedly returned no value for {entityType.FullName}.")
                : success(documentation);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<SectionCrudOutcome> GetAsync(string menuSelection, int? pageNumber, int? pageSize, CancellationToken cancellationToken)
        {
            #region implementation

            if (pageNumber.HasValue && pageNumber.Value <= 0)
            {
                return invalid($"Invalid page number: {pageNumber.Value}. Page number must be greater than 0 if provided.");
            }

            if (pageSize.HasValue && pageSize.Value <= 0)
            {
                return invalid($"Invalid page size: {pageSize.Value}. Page size must be greater than 0 if provided.");
            }

            if (pageNumber.HasValue != pageSize.HasValue)
            {
                return invalid("If providing paging, both pageNumber and pageSize must be specified.");
            }

            var entityType = getEntityType(menuSelection);
            if (entityType is null)
            {
                return invalid($"Invalid menu selection: {menuSelection}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var repository = getRepository(entityType);
            var readAllMethod = repository.GetType().GetMethod("ReadAllAsync", new[] { typeof(int?), typeof(int?) })
                ?? throw new MissingMethodException($"ReadAllAsync(int?, int?) was not found on the {entityType.Name} repository.");
            var entities = await invokeTaskAsync(repository, readAllMethod, new object?[] { pageNumber, pageSize }).ConfigureAwait(false) as IEnumerable;
            var payload = (entities?.Cast<object>() ?? Enumerable.Empty<object>())
                .Select(toEncryptedEntity)
                .ToList();

            return success(payload);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<SectionCrudOutcome> GetByIdAsync(string menuSelection, string encryptedId, CancellationToken cancellationToken)
        {
            #region implementation

            var entityType = getEntityType(menuSelection);
            if (entityType is null)
            {
                return invalid($"Invalid menu selection: {menuSelection}");
            }

            if (string.IsNullOrWhiteSpace(encryptedId))
            {
                return invalid("Encrypted ID is required.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var repository = getRepository(entityType);
            var readByIdMethod = repository.GetType().GetMethod("ReadByIdAsync", new[] { typeof(string) })
                ?? throw new MissingMethodException($"ReadByIdAsync not found on repository for {entityType.Name}");
            var entity = await invokeTaskAsync(repository, readByIdMethod, new object?[] { encryptedId }).ConfigureAwait(false);

            return entity is null
                ? notFound($"Record with ID {encryptedId} not found in section {menuSelection}.")
                : success(toEncryptedEntity(entity));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<SectionCrudOutcome> CreateAsync(string menuSelection, string? json, CancellationToken cancellationToken)
        {
            #region implementation

            var entityType = getEntityType(menuSelection);
            if (entityType is null)
            {
                return invalid($"Invalid menu selection: {menuSelection}");
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return invalid("Request body with JSON data is required.");
            }

            object? entity;
            try
            {
                entity = JsonConvert.DeserializeObject(json, entityType, createJsonSettings());
            }
            catch (JsonException)
            {
                return invalid($"Invalid JSON format for {menuSelection}.");
            }

            if (entity is null)
            {
                return invalid($"Invalid JSON data. Deserialization resulted in a null object for {menuSelection}.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var repository = getRepository(entityType);
            var createMethod = repository.GetType().GetMethod("CreateAsync", new[] { entityType })
                ?? throw new MissingMethodException($"CreateAsync not found on repository for {entityType.Name}");
            var encryptedId = await invokeTaskAsync(repository, createMethod, new[] { entity }).ConfigureAwait(false) as string;

            return string.IsNullOrWhiteSpace(encryptedId)
                ? throw new InvalidOperationException($"CreateAsync did not return an encrypted identifier for {entityType.Name}.")
                : success(encryptedId);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<SectionCrudOutcome> UpdateAsync(string menuSelection, string encryptedId, string? json, CancellationToken cancellationToken)
        {
            #region implementation

            var entityType = getEntityType(menuSelection);
            if (entityType is null)
            {
                return invalid($"Invalid menu selection: {menuSelection}");
            }

            if (string.IsNullOrWhiteSpace(encryptedId))
            {
                return invalid("Encrypted ID is required.");
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                return invalid("Request body with JSON data is required.");
            }

            var primaryKey = getPrimaryKeyProperty(entityType);
            if (primaryKey is null)
            {
                return invalid($"Could not determine primary key for section {menuSelection}. Update cannot proceed.");
            }

            if (!tryDecryptPk(encryptedId, primaryKey.PropertyType, out var decryptedPrimaryKey) || decryptedPrimaryKey is null)
            {
                return invalid($"Invalid encrypted ID format or value for section {menuSelection}.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var repository = getRepository(entityType);
            var readByIdMethod = repository.GetType().GetMethod("ReadByIdAsync", new[] { typeof(string) })
                ?? throw new MissingMethodException($"ReadByIdAsync not found on repository for {entityType.Name}");
            var entity = await invokeTaskAsync(repository, readByIdMethod, new object?[] { encryptedId }).ConfigureAwait(false);

            if (entity is null)
            {
                return notFound($"Record with ID {encryptedId} not found in section {menuSelection}.");
            }

            try
            {
                JsonConvert.PopulateObject(json, entity, createJsonSettings());
            }
            catch (JsonException)
            {
                return invalid($"Invalid JSON format for {menuSelection}.");
            }

            primaryKey.SetValue(entity, Convert.ChangeType(decryptedPrimaryKey, Nullable.GetUnderlyingType(primaryKey.PropertyType) ?? primaryKey.PropertyType));
            var updateMethod = repository.GetType().GetMethod("UpdateAsync", new[] { entityType })
                ?? throw new MissingMethodException($"UpdateAsync not found on repository for {entityType.Name}");

            try
            {
                await invokeTaskAsync(repository, updateMethod, new[] { entity }).ConfigureAwait(false);
                return success();
            }
            catch (KeyNotFoundException)
            {
                return notFound($"Record with ID {encryptedId} not found in section {menuSelection} during update attempt.");
            }

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public async Task<SectionCrudOutcome> DeleteAsync(string menuSelection, string encryptedId, CancellationToken cancellationToken)
        {
            #region implementation

            var entityType = getEntityType(menuSelection);
            if (entityType is null)
            {
                return invalid($"Invalid menu selection: {menuSelection}");
            }

            if (string.IsNullOrWhiteSpace(encryptedId))
            {
                return invalid("Encrypted ID cannot be empty.");
            }

            var primaryKey = getPrimaryKeyProperty(entityType);
            if (primaryKey is null)
            {
                return invalid($"Could not determine primary key for section {menuSelection}. Delete cannot proceed.");
            }

            if (!tryDecryptPk(encryptedId, primaryKey.PropertyType, out var decryptedPrimaryKey) || decryptedPrimaryKey is null)
            {
                return invalid($"Invalid encrypted ID format for section {menuSelection}.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var repository = getRepository(entityType);
            var deleteMethod = repository.GetType().GetMethod("DeleteAsync", new[] { typeof(string) })
                ?? throw new MissingMethodException($"DeleteAsync not found on repository for {entityType.Name}");

            try
            {
                var rowsAffected = await invokeTaskAsync(repository, deleteMethod, new object?[] { encryptedId }).ConfigureAwait(false) as int? ?? 0;
                return rowsAffected == 0
                    ? notFound($"record with id {encryptedId} not found in section {menuSelection} for deletion, or no rows affected.")
                    : success();
            }
            catch (KeyNotFoundException)
            {
                return notFound($"Record with ID {encryptedId} not found in section {menuSelection}.");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>Resolves a supported nested Label entity type.</summary>
        /// <param name="menuSelection">The nested type name supplied by the client.</param>
        /// <returns>The matching entity type, or null when the menu item is invalid.</returns>
        /// <seealso cref="Label"/>
        private static Type? getEntityType(string menuSelection)
        {
            #region implementation

            return string.IsNullOrWhiteSpace(menuSelection)
                ? null
                : typeof(Label).GetNestedType(menuSelection, BindingFlags.Public | BindingFlags.Instance);

            #endregion
        }

        /**************************************************************/
        /// <summary>Resolves the generic repository required for a dynamic entity type.</summary>
        /// <param name="entityType">The Label entity type.</param>
        /// <returns>The corresponding repository instance.</returns>
        /// <seealso cref="Repository{T}"/>
        private object getRepository(Type entityType)
        {
            #region implementation

            var repository = _serviceProvider.GetService(typeof(Repository<>).MakeGenericType(entityType));
            return repository ?? throw new InvalidOperationException($"Could not resolve repository for type {entityType.FullName}.");

            #endregion
        }

        /**************************************************************/
        /// <summary>Gets the conventional primary-key property for a dynamic entity type.</summary>
        /// <param name="entityType">The Label entity type.</param>
        /// <returns>The primary-key property when one can be inferred.</returns>
        /// <seealso cref="Label"/>
        private static PropertyInfo? getPrimaryKeyProperty(Type entityType)
        {
            #region implementation

            return entityType.GetProperty(entityType.Name + "ID", BindingFlags.Public | BindingFlags.Instance)
                ?? entityType.GetProperty(entityType.Name + "Id", BindingFlags.Public | BindingFlags.Instance)
                ?? entityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>Decrypts an encrypted primary key and converts it to the entity property type.</summary>
        /// <param name="encryptedId">The encrypted identifier.</param>
        /// <param name="propertyType">The target primary-key property type.</param>
        /// <param name="value">The decrypted primary-key value.</param>
        /// <returns>True when decryption and conversion succeed.</returns>
        /// <seealso cref="IPrimaryKeyCipher"/>
        private bool tryDecryptPk(string? encryptedId, Type propertyType, out object? value)
        {
            #region implementation

            value = null;
            var decrypted = _primaryKeyCipher.TryDecrypt(encryptedId);
            if (!decrypted.HasValue)
            {
                return false;
            }

            var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (targetType == typeof(int) && decrypted.Value is >= int.MinValue and <= int.MaxValue)
            {
                value = (int)decrypted.Value;
                return true;
            }

            if (targetType == typeof(long))
            {
                value = decrypted.Value;
                return true;
            }

            _logger.LogWarning("Unsupported primary-key type {PrimaryKeyType} for decryption.", targetType.Name);
            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>Projects a dynamic entity with encrypted identifier properties.</summary>
        /// <param name="entity">The entity to project.</param>
        /// <returns>An API-compatible dictionary representation.</returns>
        /// <seealso cref="IPrimaryKeyCipher"/>
        private Dictionary<string, object?> toEncryptedEntity(object entity)
        {
            #region implementation

            var dictionary = new Dictionary<string, object?>();
            foreach (var property in entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetGetMethod()?.IsVirtual == true && !property.GetGetMethod()!.IsFinal)
                {
                    continue;
                }

                var value = property.GetValue(entity);
                if (isIdentifierProperty(property.Name))
                {
                    dictionary["Encrypted" + property.Name] = value is null
                        ? null
                        : _primaryKeyCipher.Encrypt(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!);
                }
                else if (value is not null)
                {
                    dictionary[property.Name] = value;
                }
            }

            return dictionary;

            #endregion
        }

        /**************************************************************/
        /// <summary>Awaits a reflected task and returns its result when present.</summary>
        /// <param name="target">The target repository instance.</param>
        /// <param name="method">The reflected asynchronous method.</param>
        /// <param name="arguments">The arguments for the reflected method.</param>
        /// <returns>The task result, or null for non-generic tasks.</returns>
        /// <seealso cref="Task"/>
        private static async Task<object?> invokeTaskAsync(object target, MethodInfo method, object?[] arguments)
        {
            #region implementation

            var task = method.Invoke(target, arguments) as Task
                ?? throw new InvalidOperationException($"{method.Name} on {target.GetType().Name} did not return a Task.");
            await task.ConfigureAwait(false);
            return task.GetType().GetProperty("Result")?.GetValue(task);

            #endregion
        }

        /**************************************************************/
        /// <summary>Creates the established permissive JSON settings for dynamic entities.</summary>
        /// <returns>The serializer settings used by create and update operations.</returns>
        /// <seealso cref="JsonSerializerSettings"/>
        private static JsonSerializerSettings createJsonSettings()
        {
            #region implementation

            return new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>Identifies conventional numeric primary and foreign-key property names.</summary>
        /// <param name="propertyName">Property name to evaluate.</param>
        /// <returns>True when the property should be represented by an encrypted identifier.</returns>
        /// <seealso cref="IPrimaryKeyCipher"/>
        private static bool isIdentifierProperty(string propertyName)
        {
            #region implementation

            return !propertyName.EndsWith("GUID", StringComparison.OrdinalIgnoreCase)
                && !propertyName.EndsWith("OID", StringComparison.Ordinal)
                && (propertyName.EndsWith("ID", StringComparison.OrdinalIgnoreCase)
                    || propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

            #endregion
        }

        /**************************************************************/
        /// <summary>Creates a successful dynamic section outcome.</summary>
        /// <param name="payload">The optional successful payload.</param>
        /// <returns>A success outcome.</returns>
        private static SectionCrudOutcome success(object? payload = null) => new(SectionCrudStatus.Success, payload);

        /**************************************************************/
        /// <summary>Creates an invalid-input dynamic section outcome.</summary>
        /// <param name="error">The established validation message.</param>
        /// <returns>An invalid-input outcome.</returns>
        private static SectionCrudOutcome invalid(string error) => new(SectionCrudStatus.InvalidInput, Error: error);

        /**************************************************************/
        /// <summary>Creates a not-found dynamic section outcome.</summary>
        /// <param name="error">The established not-found message.</param>
        /// <returns>A not-found outcome.</returns>
        private static SectionCrudOutcome notFound(string error) => new(SectionCrudStatus.NotFound, Error: error);
    }
}
