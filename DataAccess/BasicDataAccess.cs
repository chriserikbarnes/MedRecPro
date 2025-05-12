using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient; // Recommended SQL client for .NET Core/.NET 8+
using MedRecPro.DataModels; // Assuming LabelClasses.cs is in this namespace

namespace MedRecPro.DataAccess
{
    /// <summary>
    /// A generic repository for performing basic CRUD operations on database tables
    /// corresponding to the data models defined in MedRecPro.DataModels.
    /// It uses Dapper for data access and reflection to keep the code DRY.
    /// Assumes table names match class names and primary keys follow the pattern [ClassName]ID.
    /// </summary>
    /// <typeparam name="T">The type of the data model class (e.g., Document, Organization).</typeparam>
    public class GenericRepository<T> where T : class
    {
        private readonly string? _connectionString;
        private readonly string? _tableName;
        private readonly string? _primaryKeyName;
        private readonly PropertyInfo? _primaryKeyProperty;
        private readonly IEnumerable<PropertyInfo> _properties;

        /// <summary>
        /// Initializes a new instance of the GenericRepository class.
        /// </summary>
        /// <param name="connectionString">The database connection string.</param>
        /// <exception cref="ArgumentNullException">Thrown if connectionString is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the primary key property cannot be found on type T.</exception>
        public GenericRepository(string connectionString)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentNullException(nameof(connectionString), "Connection string cannot be null or empty.");
            }
            _connectionString = connectionString;
            _tableName = typeof(T).Name; // Assumes table name matches class name
            _primaryKeyName = $"{_tableName}ID"; // Assumes primary key is ClassNameID
            _properties = typeof(T).GetProperties().Where(p => p.Name != _primaryKeyName); // Exclude PK for inserts/updates generally

            // Find the primary key property using reflection
            _primaryKeyProperty = typeof(T).GetProperty(_primaryKeyName);
            if (_primaryKeyProperty == null)
            {
                // Attempt common variation if ClassNameID not found (e.g. PharmClassNameID -> PharmClassNameId) - this is less ideal
                _primaryKeyProperty = typeof(T).GetProperty($"{_tableName}Id");

                if (_primaryKeyProperty == null)
                {
                    // Log or handle cases where PK convention isn't strictly followed if necessary
                    // For now, we'll throw if the assumed convention fails.
                    throw new InvalidOperationException($"Could not find primary key property '{_primaryKeyName}' or '{_tableName}Id' on type {typeof(T).Name}. Ensure the property exists and matches the naming convention.");
                }
                _primaryKeyName = $"{_tableName}Id"; // Update the key name if variation found
            }
            #endregion
        }

        /// <summary>
        /// Creates a new database connection instance.
        /// </summary>
        /// <returns>An open SqlConnection.</returns>
        private SqlConnection CreateConnection()
        {
            #region implementation
            var connection = new SqlConnection(_connectionString);
            // Consider opening the connection here if all methods are async and use it immediately.
            // For simplicity, Dapper typically handles opening/closing within its methods.
            // await connection.OpenAsync(); // Optional: Open explicitly if needed across multiple Dapper calls
            return connection;
            #endregion
        }

        /// <summary>
        /// Gets the list of column names for the type T, excluding the primary key.
        /// </summary>
        /// <returns>An enumerable of column names.</returns>
        private IEnumerable<string> GetColumns()
        {
            #region implementation
            return _properties.Select(p => p.Name);
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Creates a new record in the database corresponding to the provided entity.
        /// Assumes the primary key is an IDENTITY column and should not be included in the insert.
        /// </summary>
        /// <param name="entity">The entity object to insert.</param>
        /// <returns>The ID of the newly created record.</returns>
        /// <exception cref="ArgumentNullException">Thrown if entity is null.</exception>
        public virtual async Task<int> CreateAsync(T entity)
        {
            #region implementation
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var columns = GetColumns();
            var stringOfColumns = string.Join(", ", columns);
            var stringOfParameters = string.Join(", ", columns.Select(c => "@" + c));

            var query = $"INSERT INTO dbo.[{_tableName}] ({stringOfColumns}) VALUES ({stringOfParameters}); SELECT CAST(SCOPE_IDENTITY() as int)";

            using (var connection = CreateConnection())
            {
                var newId = await connection.ExecuteScalarAsync<int>(query, entity);
                // Set the ID on the original entity object if the PK property is settable
                if (_primaryKeyProperty.CanWrite)
                {
                    _primaryKeyProperty.SetValue(entity, newId);
                }
                return newId;
            }
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Reads a single record from the database based on its primary key ID.
        /// </summary>
        /// <param name="id">The primary key ID of the record to retrieve.</param>
        /// <returns>The entity object if found; otherwise, null.</returns>
        public virtual async Task<T?> ReadByIdAsync(int id)
        {
            #region implementation
            var query = $"SELECT * FROM dbo.[{_tableName}] WHERE [{_primaryKeyName}] = @Id";

            using (var connection = CreateConnection())
            {
                return await connection.QueryFirstOrDefaultAsync<T>(query, new { Id = id });
            }
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
            var query = $"SELECT * FROM dbo.[{_tableName}]";

            using (var connection = CreateConnection())
            {
                return await connection.QueryAsync<T>(query);
            }
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Updates an existing record in the database based on the provided entity's primary key.
        /// </summary>
        /// <param name="entity">The entity object with updated values. The primary key property must be set.</param>
        /// <returns>The number of rows affected (should be 1 if successful, 0 if not found).</returns>
        /// <exception cref="ArgumentNullException">Thrown if entity is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the primary key value cannot be retrieved from the entity.</exception>
        public virtual async Task<int> UpdateAsync(T entity)
        {
            #region implementation
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            var columns = GetColumns();
            var stringOfColumns = string.Join(", ", columns.Select(c => $"[{c}] = @{c}"));

            // Get the primary key value from the entity
            var primaryKeyValue = _primaryKeyProperty?.GetValue(entity);

            if (string.IsNullOrEmpty(_primaryKeyName))
            {
                throw new InvalidOperationException($"Primary key name for '{_primaryKeyName}' cannot be null for update operation on type {typeof(T).Name}.");

            }
            if (primaryKeyValue == null)
            {
                throw new InvalidOperationException($"Primary key value for '{_primaryKeyName}' cannot be null for update operation on type {typeof(T).Name}.");
            }


            var query = $"UPDATE dbo.[{_tableName}] SET {stringOfColumns} WHERE [{_primaryKeyName}] = @{_primaryKeyName}";

            // Dapper needs the primary key property added to the parameters for the WHERE clause
            var parameters = new DynamicParameters(entity);
            parameters.Add(_primaryKeyName, primaryKeyValue);


            using (var connection = CreateConnection())
            {
                return await connection.ExecuteAsync(query, parameters);
            }
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Deletes a record from the database based on its primary key ID.
        /// </summary>
        /// <param name="id">The primary key ID of the record to delete.</param>
        /// <returns>The number of rows affected (should be 1 if successful, 0 if not found).</returns>
        public virtual async Task<int> DeleteAsync(int id)
        {
            #region implementation
            var query = $"DELETE FROM dbo.[{_tableName}] WHERE [{_primaryKeyName}] = @Id";

            using (var connection = CreateConnection())
            {
                return await connection.ExecuteAsync(query, new { Id = id });
            }
            #endregion
        }

        /**************************************/
        /// <summary>
        /// Deletes a record from the database based on the primary key of the provided entity.
        /// </summary>
        /// <param name="entity">The entity object whose corresponding record should be deleted. The primary key property must be set.</param>
        /// <returns>The number of rows affected (should be 1 if successful, 0 if not found).</returns>
        /// <exception cref="ArgumentNullException">Thrown if entity is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the primary key value cannot be retrieved from the entity.</exception>
        public virtual async Task<int> DeleteAsync(T entity)
        {
            #region implementation
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // Get the primary key value from the entity
            var primaryKeyValue = _primaryKeyProperty.GetValue(entity);
            if (primaryKeyValue == null || (primaryKeyValue is int intValue && intValue <= 0)) // Check if PK is valid
            {
                throw new InvalidOperationException($"Primary key value for '{_primaryKeyName}' must be valid for delete operation on type {typeof(T).Name}.");
            }

            return await DeleteAsync((int)primaryKeyValue);
            #endregion
        }
    }
}