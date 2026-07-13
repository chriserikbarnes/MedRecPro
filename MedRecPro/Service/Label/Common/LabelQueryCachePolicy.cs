using MedRecPro.Service.Common;

namespace MedRecPro.Service.LabelQuery.Common
{
    /**************************************************************/
    /// <summary>
    /// Provides injected cache operations for extracted label-query services.
    /// </summary>
    /// <remarks>
    /// The policy centralizes managed-cache ownership and delegates key construction
    /// to <see cref="LegacyDtoLabelCacheKeyBuilder"/> so moving a method cannot
    /// silently change cache identity, TTL handling, or managed-key membership.
    /// </remarks>
    /// <seealso cref="IAppCache"/>
    /// <seealso cref="LegacyDtoLabelCacheKeyBuilder"/>
    internal sealed class LabelQueryCachePolicy
    {
        #region implementation

        private readonly IAppCache _appCache;
        private readonly LegacyDtoLabelCacheKeyBuilder _keyBuilder;

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="LabelQueryCachePolicy"/> class.
        /// </summary>
        /// <param name="appCache">The injected application cache adapter.</param>
        /// <param name="keyBuilder">The stable legacy cache-key builder.</param>
        /// <seealso cref="IAppCache"/>
        internal LabelQueryCachePolicy(IAppCache appCache, LegacyDtoLabelCacheKeyBuilder keyBuilder)
        {
            #region implementation

            _appCache = appCache ?? throw new ArgumentNullException(nameof(appCache));
            _keyBuilder = keyBuilder ?? throw new ArgumentNullException(nameof(keyBuilder));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a legacy search key and retrieves its typed cached value.
        /// </summary>
        /// <typeparam name="T">The expected cache value type.</typeparam>
        /// <param name="memberName">The legacy member contributing to the key.</param>
        /// <param name="searchTerm">Optional search text.</param>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <returns>The cached value, or default when no entry exists.</returns>
        /// <seealso cref="LegacyDtoLabelCacheKeyBuilder.BuildQueryKey"/>
        internal T? Get<T>(string memberName, string? searchTerm, int? page, int? size)
        {
            #region implementation

            return _appCache.Get<T>(_keyBuilder.BuildQueryKey(memberName, searchTerm, page, size));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a legacy search key and stores a managed cache entry.
        /// </summary>
        /// <param name="memberName">The legacy member contributing to the key.</param>
        /// <param name="searchTerm">Optional search text.</param>
        /// <param name="page">Optional 1-based page number.</param>
        /// <param name="size">Optional page size.</param>
        /// <param name="value">The value to cache.</param>
        /// <param name="durationHours">The cache expiration duration in hours.</param>
        /// <seealso cref="IAppCache.SetManaged"/>
        internal void Set(
            string memberName,
            string? searchTerm,
            int? page,
            int? size,
            object value,
            double durationHours = 1.0)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(value);
            _appCache.SetManaged(
                _keyBuilder.BuildQueryKey(memberName, searchTerm, page, size),
                value,
                durationHours);

            #endregion
        }

        #endregion
    }
}
