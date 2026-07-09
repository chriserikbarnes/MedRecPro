using MedRecPro.Helpers;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace MedRecPro.Service.Common
{
    /**************************************************************/
    /// <summary>
    /// Provides an injectable cache seam over the legacy application cache helper.
    /// </summary>
    /// <remarks>
    /// New service code should depend on this abstraction instead of calling
    /// <see cref="PerformanceHelper"/> directly. The static helper remains as a
    /// compatibility layer while callers migrate to dependency injection.
    /// </remarks>
    /// <seealso cref="PerformanceHelper"/>
    public interface IAppCache
    {
        /**************************************************************/
        /// <summary>
        /// Gets a cached object by key.
        /// </summary>
        /// <param name="key">The cache key to retrieve.</param>
        /// <returns>The cached object, or null when the key is absent or expired.</returns>
        /// <seealso cref="PerformanceHelper.GetCache(string)"/>
        object? Get(string key);

        /**************************************************************/
        /// <summary>
        /// Gets a typed cached object by key.
        /// </summary>
        /// <typeparam name="T">Expected cached value type.</typeparam>
        /// <param name="key">The cache key to retrieve.</param>
        /// <returns>The typed cached value, or the default value when absent or expired.</returns>
        /// <seealso cref="PerformanceHelper.GetCache{T}(string)"/>
        T? Get<T>(string key);

        /**************************************************************/
        /// <summary>
        /// Gets and deserializes a cached JSON payload by key.
        /// </summary>
        /// <typeparam name="T">Expected deserialized value type.</typeparam>
        /// <param name="key">The cache key that stores a JSON string.</param>
        /// <returns>The deserialized cached value, or the default value when unavailable.</returns>
        /// <seealso cref="PerformanceHelper.GetCachedJson{T}(string)"/>
        T? GetCachedJson<T>(string key);

        /**************************************************************/
        /// <summary>
        /// Stores a value in the application cache.
        /// </summary>
        /// <param name="key">The cache key to write.</param>
        /// <param name="value">The cache value to store.</param>
        /// <param name="durationHours">Absolute expiration duration in hours.</param>
        /// <seealso cref="PerformanceHelper.SetCache(string, object, double)"/>
        void Set(string key, object value, double durationHours = 1.0);

        /**************************************************************/
        /// <summary>
        /// Stores a value in the managed-key cache list.
        /// </summary>
        /// <param name="key">The cache key to write and track.</param>
        /// <param name="value">The cache value to store.</param>
        /// <param name="durationHours">Absolute expiration duration in hours.</param>
        /// <seealso cref="PerformanceHelper.SetCacheManageKey(string, object, double)"/>
        void SetManaged(string key, object value, double durationHours = 4.0);

        /**************************************************************/
        /// <summary>
        /// Removes a cached item by key.
        /// </summary>
        /// <param name="key">The cache key to remove.</param>
        /// <seealso cref="PerformanceHelper.RemoveCache(string)"/>
        void Remove(string key);

        /**************************************************************/
        /// <summary>
        /// Clears all managed-key cache entries.
        /// </summary>
        /// <seealso cref="PerformanceHelper.ResetManagedCache"/>
        void ResetManaged();
    }

    /**************************************************************/
    /// <summary>
    /// Default <see cref="IAppCache"/> implementation backed by <see cref="PerformanceHelper"/>.
    /// </summary>
    /// <remarks>
    /// This adapter intentionally preserves legacy cache keys, expiration tracking,
    /// and managed-key reset behavior while giving new code a replaceable dependency.
    /// </remarks>
    /// <seealso cref="IAppCache"/>
    internal sealed class PerformanceAppCache : IAppCache
    {
        #region implementation

        /**************************************************************/
        /// <inheritdoc/>
        public object? Get(string key) => PerformanceHelper.GetCache(key);

        /**************************************************************/
        /// <inheritdoc/>
        public T? Get<T>(string key) => PerformanceHelper.GetCache<T>(key);

        /**************************************************************/
        /// <inheritdoc/>
        public T? GetCachedJson<T>(string key) => PerformanceHelper.GetCachedJson<T>(key);

        /**************************************************************/
        /// <inheritdoc/>
        public void Set(string key, object value, double durationHours = 1.0)
        {
            #region implementation

            PerformanceHelper.SetCache(key, value, durationHours);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public void SetManaged(string key, object value, double durationHours = 4.0)
        {
            #region implementation

            PerformanceHelper.SetCacheManageKey(key, value, durationHours);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public void Remove(string key)
        {
            #region implementation

            PerformanceHelper.RemoveCache(key);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public void ResetManaged()
        {
            #region implementation

            PerformanceHelper.ResetManagedCache();

            #endregion
        }

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Reads the current request user through an injectable seam.
    /// </summary>
    /// <remarks>
    /// New request-scoped services should depend on this abstraction instead of
    /// reading static or ambient user state directly.
    /// </remarks>
    /// <seealso cref="ClaimHelper"/>
    /// <seealso cref="IHttpContextAccessor"/>
    public interface IUserContextAccessor
    {
        /**************************************************************/
        /// <summary>
        /// Gets the current numeric user identifier from the supplied or ambient HTTP context.
        /// </summary>
        /// <param name="httpContext">Optional explicit HTTP context. When null, the ambient accessor is used.</param>
        /// <returns>The numeric user ID from claims, or null for anonymous and invalid principals.</returns>
        /// <seealso cref="ClaimHelper.GetUserIdFromClaims(IEnumerable{Claim})"/>
        long? GetCurrentUserId(HttpContext? httpContext = null);
    }

    /**************************************************************/
    /// <summary>
    /// Default <see cref="IUserContextAccessor"/> implementation for HTTP requests.
    /// </summary>
    /// <remarks>
    /// The implementation delegates claim parsing to <see cref="ClaimHelper"/> so
    /// MVC, cookie-auth, and MCP JWT claim behavior remains centralized.
    /// </remarks>
    /// <seealso cref="IUserContextAccessor"/>
    internal sealed class HttpUserContextAccessor : IUserContextAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpUserContextAccessor"/> class.
        /// </summary>
        /// <param name="httpContextAccessor">Accessor for the ambient HTTP context.</param>
        public HttpUserContextAccessor(IHttpContextAccessor httpContextAccessor)
        {
            #region implementation

            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public long? GetCurrentUserId(HttpContext? httpContext = null)
        {
            #region implementation

            var context = httpContext ?? _httpContextAccessor.HttpContext;
            return context?.User != null
                ? ClaimHelper.GetUserIdFromClaims(context.User.Claims)
                : null;

            #endregion
        }
    }
}
