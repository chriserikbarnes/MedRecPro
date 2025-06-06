using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MedRecPro.Helpers;
using MedRecPro.Data;


namespace MedRecPro.DataAccess
{
    /// <summary>
    /// A generic repository for performing basic CRUD operations on database entities
    /// corresponding to the data models defined in MedRecPro.DataModels.
    /// It uses Entity Framework Core for data access.
    /// Assumes primary keys follow the pattern [ClassName]ID or [ClassName]Id for reflection-based PK retrieval,
    /// but primarily relies on EF Core's conventions and configurations for PK handling.
    /// </summary>
    /// <typeparam name="T">The type of the data model class (e.g., Document, Organization), which must be an EF Core entity.</typeparam>
    public class Repository<T> where T : class
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<T> _dbSet;
        private readonly string _tableName; // Retained for informational purposes, EF Core handles mapping
        private readonly string _primaryKeyName;
        private readonly PropertyInfo? _primaryKeyProperty;
        private readonly StringCipher _stringCipher;
        private readonly ILogger<T> _logger;
        private readonly IConfiguration _configuration;
        private string _encryptionKey;


        // Static lock for thread-safe
        private static readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the Repository class.
        /// </summary>
        /// <param name="context">The Entity Framework ApplicationDbContext instance.</param>
        /// <param name="configuration">Configuration settings for the application, used to retrieve encryption keys.</param>
        /// <param name="logger">Logger instance for logging operations and errors.</param>
        /// <param name="stringCipher">StringCipher instance for encrypting and decrypting sensitive data.</param>
        /// <exception cref="ArgumentNullException">Thrown if context is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the primary key property cannot be found on type T using expected conventions.</exception>
        public Repository(ApplicationDbContext context,
            StringCipher stringCipher,
            ILogger<T> logger,
            IConfiguration configuration)
        {
            #region implementation
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _stringCipher = stringCipher ?? throw new ArgumentNullException(nameof(stringCipher));
            _encryptionKey = getPkSecret() ?? throw new InvalidOperationException("Configuration key 'Security:DB:PKSecret' is missing."); ;

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), "DbContext cannot be null.");
            }
            _context = context;
            _dbSet = _context.Set<T>();
            _tableName = typeof(T).Name; // EF Core might use a different table name based on conventions/configuration

            // Try to find the primary key property using reflection for
            // methods that might need it (e.g., getting PK value from an entity instance)
            string tentativePkName = $"{_tableName}ID";

            _primaryKeyProperty = typeof(T).GetProperty(tentativePkName);

            if (_primaryKeyProperty == null)
            {
                tentativePkName = $"{_tableName}Id"; // Common variation

                _primaryKeyProperty = typeof(T).GetProperty(tentativePkName);
            }

            if (_primaryKeyProperty == null)
            {
                // Find PKs named just "Id" by convention
                tentativePkName = "Id";

                _primaryKeyProperty = typeof(T).GetProperty(tentativePkName);
            }

            if (_primaryKeyProperty == null)
            {
                // If we still can't find it, try to find the primary key column using EF Core metadata
                string? keyColumn = getPrimaryKeyColumn(_context, _tableName);

                if (!string.IsNullOrWhiteSpace(keyColumn))
                {
                    // If we found a primary key column name, try to get the property by that name
                    _primaryKeyProperty = typeof(T).GetProperty(keyColumn);
                }
            }

            if (_primaryKeyProperty == null)
            {
                // This reflection-based PK discovery is a fallback or for specific utility.
                // EF Core's primary operations (FindAsync, Add, Update, Remove) use its own metadata.
                // However, if we cannot find it by common conventions, some helper methods might fail.
                throw new InvalidOperationException($"Could not find a primary key property (e.g., '{typeof(T).Name}ID', '{typeof(T).Name}Id', or 'Id') on type {typeof(T).Name} using reflection. " +
                    "Ensure the entity has a primary key discoverable by EF Core and/or matching these " +
                    "conventions if used by repository helper methods.");
            }

            _primaryKeyName = _primaryKeyProperty.Name;
            #endregion
        }

        #region Private
        /**************************************/
        /// <summary>
        /// Retrieves the primary key column name for a given table in the database context.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private static string? getPrimaryKeyColumn(DbContext context, string tableName)
        {
            // Find the entity type mapped to the given table name (case-insensitive)
            var entityType = context.Model.GetEntityTypes()
                .FirstOrDefault(et =>
                    et.GetTableName()?.Equals(tableName, StringComparison.OrdinalIgnoreCase) ?? false);

            if (entityType == null)
                return null; // Table not mapped

            var primaryKey = entityType.FindPrimaryKey();

            if (primaryKey == null)
                return null; // No primary key defined

            // Return the property (column) names of the primary key
            return primaryKey.Properties
                .Select(p => p.GetColumnName(StoreObjectIdentifier.Table(tableName, entityType.GetSchema())))
                .FirstOrDefault();
        }

        /**************************************/
        /// <summary>
        /// Retrieves the Primary Key encryption secret from configuration.
        /// </summary>
        /// <returns>The encryption secret string.</returns>
        /// <exception cref="InvalidOperationException">Thrown if configuration is not set or the secret is missing.</exception>
        private string getPkSecret()
        {
            #region Implementation
            if (_encryptionKey == null)
            {
                lock (_lock)
                {
                    if (_encryptionKey == null)
                    {
                        string? secret = _configuration.GetSection("Security:DB:PKSecret").Value;

                        if (string.IsNullOrWhiteSpace(secret))
                        {
                            _logger.LogCritical("Required configuration key 'Security:DB:PKSecret' is missing or empty.");

                            throw new InvalidOperationException("Required configuration key 'Security:DB:PKSecret' is missing or empty.");
                        }
                        _encryptionKey = secret;
                    }
                }
            }
            return _encryptionKey;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Helper method to decrypt a user ID string.
        /// </summary>
        /// <param name="encryptedId">The encrypted ID string.</param>
        /// <param name="parameterName">Name of the parameter being decrypted (for logging).</param>
        /// <param name="decryptedId">The decrypted long ID, if successful.</param>
        /// <returns>True if decryption and parsing were successful and ID is positive; false otherwise.</returns>
        private bool tryDecryptId(string? encryptedId, string parameterName, out long decryptedId)
        {
            #region Implementation
            decryptedId = 0;
            if (string.IsNullOrWhiteSpace(encryptedId))
            {
                _logger.LogWarning("{ParameterName} is null or whitespace.", parameterName);
                return false;
            }

            try
            {
                string decryptedString = new StringCipher().Decrypt(encryptedId, getPkSecret());
                if (long.TryParse(decryptedString, out long id) && id > 0)
                {
                    decryptedId = id;
                    return true;
                }
                _logger.LogWarning("Invalid or non-positive ID after decrypting {ParameterName}. Encrypted value: {EncryptedValue}, Decrypted string: {DecryptedString}", parameterName, encryptedId, decryptedString);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting {ParameterName}. Encrypted value: {EncryptedValue}", parameterName, encryptedId);
                return false;
            }
            #endregion
        }

        #endregion

        /**************************************/
        /// <summary>
        /// Creates a new record in the database corresponding to the provided entity.
        /// The primary key is assumed to be database-generated and will be populated on the entity.
        /// </summary>
        /// <param name="entity">The entity object to insert.</param>
        /// <returns>The Encrypted ID of the newly created record.</returns>
        /// <exception cref="ArgumentNullException">Thrown if entity is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the primary key value cannot be retrieved after creation.</exception>
        public virtual async Task<string?> CreateAsync(T entity)
        {
            #region implementation

            string? ret = null;

            if (entity == null) throw new ArgumentNullException(nameof(entity));

            if (_primaryKeyProperty == null)
            {
                throw new InvalidOperationException($"Primary key property NULL on type {typeof(T).Name}. Cannot create entity without a primary key.");
            }

            await _dbSet.AddAsync(entity);

            await _context.SaveChangesAsync();

            // EF Core populates the PK on the entity object after SaveChangesAsync.
            // Retrieve it using the discovered primary key property.
            var pkValue = _primaryKeyProperty.GetValue(entity);

            if (pkValue == null)
            {
                _logger.LogError("Primary key '{PrimaryKeyName}' on type {EntityType} was not populated after creation.", _primaryKeyName, typeof(T).Name);

                throw new InvalidOperationException($"Primary key '{_primaryKeyName}' on type {typeof(T).Name} was not populated after creation.");
            }

            ret = pkValue?.ToString()?.Encrypt(_encryptionKey);

            if (string.IsNullOrWhiteSpace(ret))
            {
                _logger.LogError("Encryption of primary key '{PrimaryKeyName}' on type {EntityType} resulted in null or empty string.", _primaryKeyName, typeof(T).Name);

                throw new InvalidOperationException($"Encryption of primary key '{_primaryKeyName}' on type {typeof(T).Name} resulted in null or empty string.");
            }

            return ret;

            #endregion
        }

        /**************************************/
        /// <summary>
        /// Reads a single record from the database based on its encrypted primary key ID.
        /// </summary>
        /// <param name="encryptedId">The encrypted primary key ID of the record to retrieve.</param>
        /// <returns>The entity object if found; otherwise, null.</returns>
        public virtual async Task<T?> ReadByIdAsync(string encryptedId)
        {
            #region implementation

            long decryptedId;

            if (!tryDecryptId(encryptedId, nameof(encryptedId), out decryptedId))
            {
                _logger.LogWarning("Failed to decrypt ID for ReadByIdAsync. Encrypted ID: {EncryptedId}", encryptedId);

                return null;
            }

            return await _dbSet.FindAsync(Convert.ToInt32(decryptedId));
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Reads all records from the database table corresponding to type T.
        /// </summary>
        /// <returns>An enumerable collection of all entity objects in the table.</returns>
        public virtual async Task<IEnumerable<T>> ReadAllAsync()
        {
            #region implementation
            return await _dbSet.ToListAsync();
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Updates an existing record in the database based on the provided entity.
        /// The entity must be tracked or attachable by EF Core, and its primary key property must be set.
        /// </summary>
        /// <param name="entity">The entity object with updated values.</param>
        /// <returns>The number of rows affected.</returns>
        /// <exception cref="ArgumentNullException">Thrown if entity is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the primary key value cannot be retrieved from the entity.</exception>
        public virtual async Task<int> UpdateAsync(T entity)
        {
            #region implementation
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // Ensure the PK property is set on the entity for EF Core to identify it.
            var primaryKeyValue = _primaryKeyProperty?.GetValue(entity);

            if (primaryKeyValue == null)
            {
                throw new InvalidOperationException($"Primary key value for '{_primaryKeyName}' cannot be null for update operation on type {typeof(T).Name}.");
            }

            // _dbSet.Update(entity); // Marks all properties as modified.
            // More robust way if entity might be detached:
            _context.Entry(entity).State = EntityState.Modified;

            return await _context.SaveChangesAsync();
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Deletes a record from the database based on its encrypted primary key ID.
        /// </summary>
        /// <param name="encryptedId">The encrypted primary key ID of the record to delete.</param>
        /// <returns>The number of rows affected (should be 1 if successful, 0 if not found).</returns>
        public virtual async Task<int> DeleteAsync(string encryptedId)
        {
            #region implementation
            long id;

            if (!tryDecryptId(encryptedId, nameof(encryptedId), out id))
            {
                _logger.LogWarning("Failed to decrypt ID for DeleteAsync. Encrypted ID: {EncryptedId}", encryptedId);
                
                throw new InvalidOperationException($"Failed to decrypt ID for DeleteAsync. Encrypted ID: {encryptedId}");
            }

            var entityToDelete = await _dbSet.FindAsync(Convert.ToInt32(id));

            if (entityToDelete == null)
            {
                throw new KeyNotFoundException($"No entity found with primary key '{_primaryKeyName}' value '{encryptedId}' for deletion in type {typeof(T).Name}.");
            }

            _dbSet.Remove(entityToDelete);

            return await _context.SaveChangesAsync();
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Deletes a record from the database based on the primary key of the provided entity.
        /// </summary>
        /// <param name="entity">The entity object whose corresponding record should be deleted. The primary key property must be set.</param>
        /// <returns>The number of rows affected.</returns>
        /// <exception cref="ArgumentNullException">Thrown if entity is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the primary key value cannot be retrieved or is invalid.</exception>
        public virtual async Task<int> DeleteAsync(T entity)
        {
            #region implementation
            string encryptedId;

            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var primaryKeyValueObject = _primaryKeyProperty?.GetValue(entity);

            if (primaryKeyValueObject == null)
            {
                _logger.LogError("Primary key '{PrimaryKeyName}' on type {EntityType} is null for delete operation.", _primaryKeyName, typeof(T).Name);

                throw new InvalidOperationException($"Primary key value for '{_primaryKeyName}' cannot be null for delete operation on type {typeof(T).Name}.");
            }

            if (primaryKeyValueObject is long longValue)
            {
                // Additional check for typical auto-incrementing int PKs
                if (longValue <= 0 && typeof(T).GetProperty(_primaryKeyName)?.PropertyType == typeof(long)) 
                {
                    throw new InvalidOperationException($"Primary key value '{longValue}' for '{_primaryKeyName}' is not valid for delete operation on type {typeof(T).Name}.");
                }

                // Encrypt the primary key value to match the expected format
                encryptedId = longValue.ToString().Encrypt(_encryptionKey);

                // Call the ID-based delete for simplicity and consistency
                return await DeleteAsync(encryptedId);
            }

            if (primaryKeyValueObject is int intValue)
            {
                // Additional check for typical auto-incrementing int PKs
                if (intValue <= 0 && typeof(T).GetProperty(_primaryKeyName)?.PropertyType == typeof(int))
                {
                    throw new InvalidOperationException($"Primary key value '{intValue}' for '{_primaryKeyName}' is not valid for delete operation on type {typeof(T).Name}.");
                }

                // Encrypt the primary key value to match the expected format
                encryptedId = intValue.ToString().Encrypt(_encryptionKey);

                // Call the ID-based delete for simplicity and consistency
                return await DeleteAsync(encryptedId);
            }

            // If the primary key is not a long, we need to handle it differently.
            // Here it assumed that the key is encrypted.
            if (primaryKeyValueObject is string stringValue) {
                return await DeleteAsync(stringValue);
            }

            throw new InvalidOperationException($"Primary key for '{_primaryKeyName}' on type {typeof(T).Name} is not an integer, or could not be retrieved correctly for deletion. PK type found: {primaryKeyValueObject.GetType().Name}");
            #endregion
        }
    }
}
