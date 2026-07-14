using MedRecPro.Data;
using MedRecPro.DataAccess;
using MedRecPro.Helpers;
using MedRecPro.Models;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Provides dynamic Label section type, repository, encrypted-ID, and projection operations.
    /// </summary>
    /// <remarks>
    /// The legacy dynamic-section feature is intentionally isolated here so controllers do not resolve
    /// services, read encryption configuration, or implement encrypted identifier handling.
    /// </remarks>
    /// <seealso cref="MedRecPro.Api.Controllers.LabelSectionController"/>
    public interface ILabelSectionCrudService
    {
        Type? GetEntityType(string menuSelection);
        object GetRepository(Type entityType);
        bool TryDecryptPk(string? encryptedId, Type propertyType, out object? value);
        Dictionary<string, object?> ToEncryptedEntity(object entity);
        List<string> GetMenu();
        ClassDocumentation? GetDocumentation(Type entityType);
        PropertyInfo? GetPrimaryKeyProperty(Type entityType);
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
        public Type? GetEntityType(string menuSelection)
        {
            #region implementation

            return string.IsNullOrWhiteSpace(menuSelection)
                ? null
                : typeof(Label).GetNestedType(menuSelection, BindingFlags.Public | BindingFlags.Instance);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public object GetRepository(Type entityType)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(entityType);
            var repository = _serviceProvider.GetService(typeof(Repository<>).MakeGenericType(entityType));
            return repository ?? throw new InvalidOperationException(
                $"Could not resolve repository for type {entityType.FullName}.");

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public PropertyInfo? GetPrimaryKeyProperty(Type entityType)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(entityType);
            return entityType.GetProperty(entityType.Name + "ID", BindingFlags.Public | BindingFlags.Instance)
                ?? entityType.GetProperty(entityType.Name + "Id", BindingFlags.Public | BindingFlags.Instance)
                ?? entityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public bool TryDecryptPk(string? encryptedId, Type propertyType, out object? value)
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
        /// <inheritdoc/>
        public Dictionary<string, object?> ToEncryptedEntity(object entity)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(entity);
            var dictionary = new Dictionary<string, object?>();
            var entityType = entity.GetType();
            var primaryKey = GetPrimaryKeyProperty(entityType);

            foreach (var property in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead)
                {
                    continue;
                }

                var getter = property.GetGetMethod();
                if (getter?.IsVirtual == true && !getter.IsFinal)
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

            if (primaryKey is null)
            {
                _logger.LogWarning("No primary-key property was found for {EntityType}.", entityType.FullName);
            }

            return dictionary;

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
        public ClassDocumentation? GetDocumentation(Type entityType)
        {
            #region implementation

            return DtoTransform.GetClassDocumentation(entityType, _logger);

            #endregion
        }

        /**************************************************************/
        /// <summary>Identifies conventional numeric primary and foreign-key property names.</summary>
        /// <param name="propertyName">Property name to evaluate.</param>
        /// <returns>True when the property should be represented by an encrypted identifier.</returns>
        private static bool isIdentifierProperty(string propertyName)
        {
            #region implementation

            return !propertyName.EndsWith("GUID", StringComparison.OrdinalIgnoreCase)
                && !propertyName.EndsWith("OID", StringComparison.Ordinal)
                && (propertyName.EndsWith("ID", StringComparison.OrdinalIgnoreCase)
                    || propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase));

            #endregion
        }
    }
}
