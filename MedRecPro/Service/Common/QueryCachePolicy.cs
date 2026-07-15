namespace MedRecPro.Service.Common
{
    /**************************************************************/
    /// <summary>
    /// Provides domain-neutral typed query-result cache operations for opaque keys.
    /// </summary>
    /// <remarks>
    /// Callers retain ownership of cache-key composition and pass the completed key
    /// to this policy. The policy preserves managed-key membership and expiration
    /// behavior through the injected <see cref="IAppCache"/> boundary.
    /// </remarks>
    /// <example>
    /// <code>
    /// var cached = policy.GetByKey&lt;IReadOnlyList&lt;string&gt;&gt;(cacheKey);
    /// policy.SetManagedByKey(cacheKey, values, 1.0);
    /// </code>
    /// </example>
    /// <seealso cref="IAppCache"/>
    internal sealed class QueryCachePolicy
    {
        #region implementation

        private readonly IAppCache _appCache;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="QueryCachePolicy"/> class.
        /// </summary>
        /// <param name="appCache">The injected application cache adapter.</param>
        /// <seealso cref="IAppCache"/>
        internal QueryCachePolicy(IAppCache appCache)
        {
            #region implementation

            _appCache = appCache ?? throw new ArgumentNullException(nameof(appCache));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves a typed cached query result by its opaque key.
        /// </summary>
        /// <typeparam name="T">The expected cache value type.</typeparam>
        /// <param name="cacheKey">The complete caller-owned cache key.</param>
        /// <returns>The cached value, or default when the entry is absent or expired.</returns>
        /// <seealso cref="IAppCache.Get{T}(string)"/>
        internal T? GetByKey<T>(string cacheKey)
        {
            #region implementation

            ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
            return _appCache.Get<T>(cacheKey);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Stores a query result under an opaque key and tracks it for managed reset.
        /// </summary>
        /// <param name="cacheKey">The complete caller-owned cache key.</param>
        /// <param name="value">The query result to cache.</param>
        /// <param name="durationHours">The absolute expiration duration in hours.</param>
        /// <seealso cref="IAppCache.SetManaged(string, object, double)"/>
        internal void SetManagedByKey(string cacheKey, object value, double durationHours = 1.0)
        {
            #region implementation

            ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
            ArgumentNullException.ThrowIfNull(value);
            _appCache.SetManaged(cacheKey, value, durationHours);

            #endregion
        }

        #endregion
    }
}
