using System.Reflection;
using MedRecPro.Helpers;
using static MedRecPro.Models.Label;

namespace MedRecPro.Models.Extensions
{
    /**************************************************************/
    /// <summary>
    /// Provides extension methods for IOperationStatusStore to support multiple operation types
    /// while maintaining backward compatibility with the strongly-typed interface.
    /// </summary>
    /// <remarks>
    /// These extensions use reflection to enable storage of different operation status types
    /// (ImportOperationStatus, ComparisonOperationStatus, etc.) while preserving the existing
    /// strongly-typed interface for backward compatibility. Uses prefixed cache keys to avoid
    /// collisions between different operation types.
    /// </remarks>
    /// <seealso cref="IOperationStatusStore"/>
    /// <seealso cref="ImportOperationStatus"/>
    /// <seealso cref="ComparisonOperationStatus"/>
    /// <seealso cref="Label"/>
    public static class OperationStatusStoreExtensions
    {
        #region implementation

        #region constants

        /**************************************************************/
        /// <summary>
        /// Cache key prefix for import operations to maintain compatibility.
        /// </summary>
        /// <seealso cref="Label"/>
        private const string IMPORT_PREFIX = "OperationStatus";

        /**************************************************************/
        /// <summary>
        /// Cache key prefix for comparison operations.
        /// </summary>
        /// <seealso cref="Label"/>
        private const string COMPARISON_PREFIX = "ComparisonStatus";

        /**************************************************************/
        /// <summary>
        /// Default cache duration in hours for operation status storage.
        /// </summary>
        /// <seealso cref="Label"/>
        private const double DEFAULT_CACHE_DURATION_HOURS = 1.0;

        #endregion

        #region generic extension methods

        /**************************************************************/
        /// <summary>
        /// Stores operation status for any supported operation type using reflection.
        /// </summary>
        /// <typeparam name="T">The operation status type to store</typeparam>
        /// <param name="store">The operation status store instance</param>
        /// <param name="operationId">The unique operation identifier</param>
        /// <param name="status">The status object to store</param>
        /// <remarks>
        /// Uses reflection to determine the appropriate cache key prefix based on the type.
        /// Maintains backward compatibility by using the same cache key format for ImportOperationStatus.
        /// </remarks>
        /// <seealso cref="IOperationStatusStore"/>
        /// <seealso cref="Label"/>
        public static void Set<T>(this IOperationStatusStore store, string operationId, T status)
            where T : class
        {
            #region implementation
            if (status is ImportOperationStatus importStatus)
            {
                // Use the original strongly-typed method for backward compatibility
                store.Set(operationId, importStatus);
                return;
            }

            // For other types, use reflection-based caching
            var cacheKey = generateCacheKey<T>(operationId);
            PerformanceHelper.SetCacheManageKey(cacheKey, status, DEFAULT_CACHE_DURATION_HOURS);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to retrieve operation status for any supported operation type using reflection.
        /// </summary>
        /// <typeparam name="T">The operation status type to retrieve</typeparam>
        /// <param name="store">The operation status store instance</param>
        /// <param name="operationId">The unique operation identifier</param>
        /// <param name="status">The retrieved status object, or null if not found</param>
        /// <returns>True if the operation status was found, false otherwise</returns>
        /// <remarks>
        /// Uses reflection to determine the appropriate cache key prefix based on the type.
        /// Maintains backward compatibility by using the original method for ImportOperationStatus.
        /// </remarks>
        /// <seealso cref="IOperationStatusStore"/>
        /// <seealso cref="Label"/>
        public static bool TryGet<T>(this IOperationStatusStore store, string operationId, out T? status)
            where T : class
        {
            #region implementation
            if (typeof(T) == typeof(ImportOperationStatus))
            {
                // Use the original strongly-typed method for backward compatibility
                var success = store.TryGet(operationId, out ImportOperationStatus? importStatus);
                status = importStatus as T;
                return success;
            }

            // For other types, use reflection-based caching
            var cacheKey = generateCacheKey<T>(operationId);
            status = PerformanceHelper.GetCache<T>(cacheKey);
            return status != null;
            #endregion
        }

        #endregion

        #region specialized methods for known types

        /**************************************************************/
        /// <summary>
        /// Stores comparison operation status with type-safe method signature.
        /// </summary>
        /// <param name="store">The operation status store instance</param>
        /// <param name="operationId">The unique operation identifier</param>
        /// <param name="status">The comparison status to store</param>
        /// <seealso cref="ComparisonOperationStatus"/>
        /// <seealso cref="Label"/>
        public static void SetComparisonStatus(this IOperationStatusStore store, string operationId, ComparisonOperationStatus status)
        {
            #region implementation
            store.Set(operationId, status);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to retrieve comparison operation status with type-safe method signature.
        /// </summary>
        /// <param name="store">The operation status store instance</param>
        /// <param name="operationId">The unique operation identifier</param>
        /// <param name="status">The retrieved comparison status, or null if not found</param>
        /// <returns>True if the comparison status was found, false otherwise</returns>
        /// <seealso cref="ComparisonOperationStatus"/>
        /// <seealso cref="Label"/>
        public static bool TryGetComparisonStatus(this IOperationStatusStore store, string operationId, out ComparisonOperationStatus? status)
        {
            #region implementation
            return store.TryGet(operationId, out status);
            #endregion
        }

        #endregion

        #region private helper methods

        /**************************************************************/
        /// <summary>
        /// Generates an appropriate cache key for the specified operation type and ID.
        /// </summary>
        /// <typeparam name="T">The operation status type</typeparam>
        /// <param name="operationId">The unique operation identifier</param>
        /// <returns>A cache key string with appropriate prefix</returns>
        /// <remarks>
        /// Uses reflection to determine the type and generate the appropriate prefix.
        /// Maintains backward compatibility for ImportOperationStatus keys.
        /// </remarks>
        /// <seealso cref="Label"/>
        private static string generateCacheKey<T>(string operationId)
        {
            #region implementation
            var typeName = typeof(T).Name;

            // Map known types to their prefixes
            var prefix = typeName switch
            {
                nameof(ImportOperationStatus) => IMPORT_PREFIX,
                nameof(ComparisonOperationStatus) => COMPARISON_PREFIX,
                _ => $"{typeName}Status" // Generic fallback for future types
            };

            return $"{prefix}:{operationId}";
            #endregion
        }

        #endregion

        #region cache management utilities

        /**************************************************************/
        /// <summary>
        /// Removes all cached status entries for a specific operation type.
        /// </summary>
        /// <typeparam name="T">The operation status type to clear</typeparam>
        /// <remarks>
        /// Utility method for cache maintenance and cleanup operations.
        /// Uses reflection to determine the appropriate cache key prefix.
        /// </remarks>
        /// <seealso cref="Label"/>
        public static void ClearStatusesByType<T>(this IOperationStatusStore store)
            where T : class
        {
            #region implementation
            // This would require additional cache management functionality
            // Implementation depends on your PerformanceHelper cache provider capabilities
            // For now, this serves as a placeholder for future cache management needs
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets information about supported operation status types.
        /// </summary>
        /// <returns>Collection of supported type information</returns>
        /// <remarks>
        /// Utility method for debugging and documentation purposes.
        /// </remarks>
        /// <seealso cref="Label"/>
        public static IEnumerable<string> GetSupportedTypes(this IOperationStatusStore store)
        {
            #region implementation
            return new[]
            {
                nameof(ImportOperationStatus),
                nameof(ComparisonOperationStatus)
            };
            #endregion
        }

        #endregion

        #endregion
    }
}