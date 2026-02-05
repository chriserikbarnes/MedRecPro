/**************************************************************/
/// <summary>
/// Interface for a persistent cache that survives process restarts.
/// </summary>
/// <remarks>
/// This replaces IMemoryCache for OAuth flow data (auth codes, PKCE data,
/// upstream state mappings) that must survive Kestrel process restarts
/// in OutOfProcess hosting mode on Azure App Service.
///
/// Implementations may use file-based storage, distributed cache (Redis),
/// or database storage depending on deployment requirements.
/// </remarks>
/**************************************************************/

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Service interface for persistent cache operations.
/// </summary>
/**************************************************************/
public interface IPersistedCacheService
{
    /**************************************************************/
    /// <summary>
    /// Sets a value in the cache with an absolute expiration time.
    /// </summary>
    /// <typeparam name="T">The type of value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="absoluteExpiration">The absolute expiration time.</param>
    /**************************************************************/
    Task SetAsync<T>(string key, T value, TimeSpan absoluteExpiration);

    /**************************************************************/
    /// <summary>
    /// Gets a value from the cache.
    /// </summary>
    /// <typeparam name="T">The expected type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or default(T) if not found or expired.</returns>
    /**************************************************************/
    Task<T?> GetAsync<T>(string key);

    /**************************************************************/
    /// <summary>
    /// Tries to get a value from the cache.
    /// </summary>
    /// <typeparam name="T">The expected type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>A tuple of (found, value). If found is false, value is default.</returns>
    /**************************************************************/
    Task<(bool Found, T? Value)> TryGetAsync<T>(string key);

    /**************************************************************/
    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /**************************************************************/
    Task RemoveAsync(string key);
}
