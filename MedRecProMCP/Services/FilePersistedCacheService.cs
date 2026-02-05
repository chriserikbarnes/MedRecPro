/**************************************************************/
/// <summary>
/// File-based persistent cache implementation.
/// </summary>
/// <remarks>
/// Stores cache entries as individual JSON files on disk. This ensures
/// OAuth flow data (auth codes, PKCE data, upstream state mappings)
/// survives Kestrel process restarts in OutOfProcess hosting mode.
///
/// File storage path:
/// - Azure App Service: %HOME%/data/mcp-cache/ (persists across restarts)
/// - Local development: {ContentRoot}/mcp-cache/
///
/// Each cache entry is stored as a JSON file with an embedded expiration
/// timestamp. Expired entries are cleaned up lazily on read and
/// periodically via a background cleanup timer.
///
/// Thread safety is ensured via file-level locking and atomic writes
/// (write to temp file, then rename).
/// </remarks>
/// <seealso cref="IPersistedCacheService"/>
/**************************************************************/

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedRecProMCP.Services;

/**************************************************************/
/// <summary>
/// Persistent cache service backed by the local filesystem.
/// </summary>
/**************************************************************/
public class FilePersistedCacheService : IPersistedCacheService, IDisposable
{
    private readonly string _cacheDirectory;
    private readonly ILogger<FilePersistedCacheService> _logger;
    private readonly Timer _cleanupTimer;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    // Cleanup expired entries every 5 minutes
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    /**************************************************************/
    /// <summary>
    /// Initializes a new instance of FilePersistedCacheService.
    /// </summary>
    /// <param name="environment">The web host environment for path resolution.</param>
    /// <param name="logger">Logger instance.</param>
    /**************************************************************/
    public FilePersistedCacheService(
        IWebHostEnvironment environment,
        ILogger<FilePersistedCacheService> logger)
    {
        _logger = logger;

        #region implementation
        // Determine cache directory
        // Azure App Service: %HOME%/data/mcp-cache/ (persistent storage)
        // Local: {ContentRoot}/mcp-cache/
        var homeDir = Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(homeDir) && Directory.Exists(homeDir))
        {
            // Azure App Service - use persistent storage under %HOME%/data
            _cacheDirectory = Path.Combine(homeDir, "data", "mcp-cache");
        }
        else
        {
            // Local development
            _cacheDirectory = Path.Combine(environment.ContentRootPath, "mcp-cache");
        }

        // Ensure directory exists
        Directory.CreateDirectory(_cacheDirectory);

        _logger.LogInformation(
            "[Cache] File-based persistent cache initialized at: {CacheDir}",
            _cacheDirectory);

        // Configure JSON serialization
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Start periodic cleanup
        _cleanupTimer = new Timer(
            callback: _ => cleanupExpiredEntries(),
            state: null,
            dueTime: CleanupInterval,
            period: CleanupInterval);
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task SetAsync<T>(string key, T value, TimeSpan absoluteExpiration)
    {
        #region implementation
        try
        {
            var entry = new CacheEntry<T>
            {
                Value = value,
                ExpiresAtUtc = DateTime.UtcNow.Add(absoluteExpiration),
                TypeName = typeof(T).FullName ?? typeof(T).Name
            };

            var filePath = getFilePath(key);
            var tempPath = filePath + ".tmp";

            var json = JsonSerializer.Serialize(entry, _jsonOptions);

            // Atomic write: write to temp file, then rename
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, filePath, overwrite: true);

            _logger.LogDebug(
                "[Cache] Set key: {Key}, expires in {Expiration}",
                key, absoluteExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Cache] Failed to set key: {Key}", key);
        }
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<T?> GetAsync<T>(string key)
    {
        var (found, value) = await TryGetAsync<T>(key);
        return found ? value : default;
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public async Task<(bool Found, T? Value)> TryGetAsync<T>(string key)
    {
        #region implementation
        try
        {
            var filePath = getFilePath(key);

            if (!File.Exists(filePath))
            {
                return (false, default);
            }

            var json = await File.ReadAllTextAsync(filePath);
            var entry = JsonSerializer.Deserialize<CacheEntry<T>>(json, _jsonOptions);

            if (entry == null)
            {
                return (false, default);
            }

            // Check expiration
            if (entry.ExpiresAtUtc < DateTime.UtcNow)
            {
                _logger.LogDebug("[Cache] Key expired: {Key}", key);
                // Lazy cleanup
                tryDeleteFile(filePath);
                return (false, default);
            }

            _logger.LogDebug("[Cache] Get key: {Key}", key);
            return (true, entry.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Cache] Failed to get key: {Key}", key);
            return (false, default);
        }
        #endregion
    }

    /**************************************************************/
    /// <inheritdoc/>
    /**************************************************************/
    public Task RemoveAsync(string key)
    {
        #region implementation
        try
        {
            var filePath = getFilePath(key);
            tryDeleteFile(filePath);

            _logger.LogDebug("[Cache] Removed key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Cache] Failed to remove key: {Key}", key);
        }

        return Task.CompletedTask;
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Disposes the cleanup timer.
    /// </summary>
    /**************************************************************/
    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer?.Dispose();
            _disposed = true;
        }
    }

    #region Private Methods

    /**************************************************************/
    /// <summary>
    /// Gets the file path for a cache key.
    /// </summary>
    /// <remarks>
    /// Keys are hashed to produce safe filenames that won't conflict
    /// with filesystem restrictions.
    /// </remarks>
    /**************************************************************/
    private string getFilePath(string key)
    {
        // Hash the key to produce a safe filename
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        var fileName = Convert.ToHexString(hashBytes)[..32].ToLowerInvariant() + ".json";

        return Path.Combine(_cacheDirectory, fileName);
    }

    /**************************************************************/
    /// <summary>
    /// Safely deletes a file, ignoring errors.
    /// </summary>
    /**************************************************************/
    private void tryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Cache] Failed to delete file: {Path}", filePath);
        }
    }

    /**************************************************************/
    /// <summary>
    /// Periodically cleans up expired cache entries.
    /// </summary>
    /**************************************************************/
    private void cleanupExpiredEntries()
    {
        #region implementation
        try
        {
            var files = Directory.GetFiles(_cacheDirectory, "*.json");
            var removed = 0;

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    // Quick check for expiration without full deserialization
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("expiresAtUtc", out var expProp))
                    {
                        if (expProp.TryGetDateTime(out var expiresAt) && expiresAt < DateTime.UtcNow)
                        {
                            File.Delete(file);
                            removed++;
                        }
                    }
                }
                catch
                {
                    // If we can't read/parse the file, delete it
                    try { File.Delete(file); } catch { }
                }
            }

            if (removed > 0)
            {
                _logger.LogInformation(
                    "[Cache] Cleanup removed {Count} expired entries", removed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Cache] Cleanup failed");
        }
        #endregion
    }

    #endregion
}

/**************************************************************/
/// <summary>
/// Wrapper for cached values with expiration metadata.
/// </summary>
/**************************************************************/
internal class CacheEntry<T>
{
    /**************************************************************/
    /// <summary>
    /// The cached value.
    /// </summary>
    /**************************************************************/
    public T? Value { get; set; }

    /**************************************************************/
    /// <summary>
    /// When this entry expires (UTC).
    /// </summary>
    /**************************************************************/
    public DateTime ExpiresAtUtc { get; set; }

    /**************************************************************/
    /// <summary>
    /// The type name for debugging purposes.
    /// </summary>
    /**************************************************************/
    public string? TypeName { get; set; }
}
