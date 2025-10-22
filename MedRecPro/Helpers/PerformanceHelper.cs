
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace MedRecPro.Helpers
{
    /// <summary>
    /// This is used to hold values in a session to prevent
    /// repetitive db calls that are going to return the same
    /// set of values. 
    /// </summary>
    internal class PerformanceProperty
    {
        #region properties
        public string ExpirationKey { get; private set; } = ("QDOC_PerformanceHelperExpiration").GetSHA1HashString();

        #endregion

        public PerformanceProperty() {; }

    }


    /// <summary>
    /// Some SP's can get called repeatedly resulting in
    /// performance degradation from network latency.
    /// the performance helper instantiates a singleton
    /// to store frequently accessed information so a 
    /// database call can be avoided.
    /// 
    /// The cache methods store data in key/value pairs
    /// within the system default memory cache. These
    /// items are held in RAM and are subject to 
    /// garbage collection on their expiration date.
    /// The default lifetime is 1.0 hour for unmanaged
    /// keys. The default for managed keys is 4.0 hours.
    /// Expiration can be passed if longer/shorter 
    /// lifetimes are needed.
    /// 
    /// Managed keys are listed in a key chain and may
    /// be garbage collected early if ResetManagedCache()
    /// is called. Early garbage collection is needed
    /// when a database change occurs and the information
    /// on screen needs to be consistent with the database
    /// change e.g. change in assignment owner.
    /// </summary>
    public sealed class PerformanceHelper
    {

        #region properties
        private static readonly Lazy<PerformanceHelper> lazy = new Lazy<PerformanceHelper>(() => new PerformanceHelper());

        public PerformanceHelper Instance { get { return lazy.Value; } }

        public static Random Rnd { get; private set; } = new Random(Guid.NewGuid().GetHashCode());

        private static PerformanceProperty p;

        private PerformanceHelper() { }

        #endregion

        /******************************************************/
        private static void initStatic()
        {
            p = new PerformanceProperty();
        }


        #region memory cache methods

        // Static lock objects for synchronization.
        private static readonly object cacheLock = new object();
        private static readonly object keySetLock = new object();

        // Static ConcurrentDictionary to track expiration times.
        private static readonly ConcurrentDictionary<string, DateTimeOffset> expirationMap
            = new ConcurrentDictionary<string, DateTimeOffset>();

        // Thread-safe dictionary for managed keys.
        private static readonly ConcurrentDictionary<string, byte> keySet
            = new ConcurrentDictionary<string, byte>();


        /******************************************************/
        /// <summary>
        /// Empties the cached items that have a key listed
        /// in the managed key list (Key Chain) e.g.
        /// Assignments, AssignmentItems, AssignmentCount, AssignmentStatuses
        /// </summary>
        public static void ResetManagedCache()
        {
            removeKeyListItems();
        }

        /******************************************************/
        /// <summary>
        /// Caches an object for 1 hour. The key could be created
        /// from the concatenation of the calling method and its params.
        /// It is important for keys to be unique within their
        /// respective methods or unique for their database calls.
        /// 
        /// e.g. String.Concat("GetEmployeeDiscipline", 
        /// myGUID ?? string.Empty, 
        /// aGuid ?? string.Empty, 
        /// "Quality Management");
        /// 
        /// This is calls the method SetCache(key, obj, duration)
        /// and sets the duration to 1 hour.
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="obj"></param>
        public static void SetCache(string key, object obj)
        {
            #region implmenatation
            try
            {
                SetCache(key, obj, 1.0);
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("PerformanceHelper.SetCache: " + e);
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Caches an object for the passed duration in hours. The key could be created
        /// from the concatenation of the calling method and its params
        /// 
        /// e.g. String.Concat("GetEmployeeDiscipline", 
        /// myGUID ?? string.Empty, 
        /// aGuid ?? string.Empty, 
        /// "Quality Management");
        /// 
        /// Note that Microsoft discourages developers from calling
        /// MemoryCache.Default.Dispose() and they discourage enumerating
        /// due to performance issues. Stackoverflow writers report mixed
        /// results from MemoryCache.Default.Trim(100) as a means
        /// of clearing items from cache. 
        /// 
        /// All items are tracked with a dictionary of keys and expirations. 
        /// The dictionary expiration is checked prior to returning a 
        /// cached item. This is to insure that no stale item is 
        /// returned. Note: there is a lag between cache expiration 
        /// and system disposal, meaning a stale item could be returned 
        /// if we didn't use a secondary dictionary check.
        /// 
        /// This method maintains a cached timer for each user. 
        /// The timer is used to sample the current count for
        /// all cached items every three minutes on a per
        /// user basis. 
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="obj"></param>
        /// <param name="duration"></param>
        public static void SetCache(string key, object obj, double duration)
        {
            #region implementation
            /*
                * NOTE: in production items are not eliminated at the moment 
                * of removal or expiration, meaning a stale item may be returned.
                * the implementation of the expirationMap dictionary is to manually keep
                * track of expirations
                */

            // Ensure static initialization
            initStatic();

            try
            {

                //clock for logging the cache count
                DateTimeOffset? logTimer = null;

                // Generate the logging key (hashed per user).
                string logTimerKey = $"SetCache{Util.GetLoginName("PerformanceHelper.SetCache")}".GetSHA1HashString();

                //when the timer cache is null or stale
                //this gets switched to true
                bool isExpired = isTimerExpired(logTimerKey, out logTimer);

                // Get the MemoryCache instance.
                ObjectCache cache = System.Runtime.Caching.MemoryCache.Default;

                // Calculate expiration for the object.
                DateTimeOffset expiration = DateTimeOffset.Now.AddHours(duration);

                // Define cache policies.
                CacheItemPolicy objectPolicy = new CacheItemPolicy { AbsoluteExpiration = expiration };
                CacheItemPolicy dictionaryPolicy = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddHours(8.0) };

                if (!string.IsNullOrEmpty(key) && obj != null && expirationMap != null)
                {
                    lock (cacheLock)
                    {
                        // Update the static expiration map with the new object's expiration.
                        expirationMap[key] = expiration;

                        // Cache the object.
                        cache.Set(key, obj, objectPolicy);

                        // MemoryCache with the expiration dictionary and log timer.
                        // This step can be omitted if the static dictionary is the sole source of truth.
                        cache.Set(p.ExpirationKey, expirationMap.ToDictionary(kv => kv.Key, kv => kv.Value), dictionaryPolicy);

                        // Cache the log timer
                        if (logTimer != null) cache.Set(logTimerKey, logTimer, dictionaryPolicy);

                        // Log cache counts if the log timer was expired and there are enough items.
                        if (isExpired && expirationMap.Count > 2)
                        {
                            int expiredCount = expirationMap?.Count(kv => kv.Value < DateTimeOffset.Now) ?? 0;
                            ErrorHelper.AddErrorMsg($"PerformanceHelper.SetCache (info): Cached count ({expirationMap.Count})");
                            ErrorHelper.AddErrorMsg($"PerformanceHelper.SetCache (info): Expired cached count ({expiredCount})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorHelper.AddErrorMsg("PerformanceHelper.SetCache: " + ex);
            }
            #endregion
        }

        #region 03/21/2025 depricated
        /******************************************************/
        /// <summary>
        /// Caches an object for the passed duration in hours. The key could be created
        /// from the concatenation of the calling method and its params
        /// 
        /// e.g. String.Concat("GetEmployeeDiscipline", 
        /// myGUID ?? string.Empty, 
        /// aGuid ?? string.Empty, 
        /// "Quality Management");
        /// 
        /// Note that Microsoft discourages developers from calling
        /// MemoryCache.Default.Dispose() and they discourage enumerating
        /// due to performance issues. Stackoverflow writers report mixed
        /// results from MemoryCache.Default.Trim(100) as a means
        /// of clearing items from cache. 
        /// 
        /// All items are tracked with a dictionary of keys and expirations. 
        /// The dictionary expiration is checked prior to returning a 
        /// cached item. This is to insure that no stale item is 
        /// returned. Note: there is a lag between cache expiration 
        /// and system disposal, meaning a stale item could be returned 
        /// if we didn't use a secondary dictionary check.
        /// 
        /// This method maintains a cached timer for each user. 
        /// The timer is used to sample the current count for
        /// all cached items every three minutes on a per
        /// user basis. 
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="obj"></param>
        /// <param name="duration"></param>
        public static void depricated_SetCache(string key, object obj, double duration)
        {
            #region implementation
            /*
                * NOTE: in production items are not eliminated at the moment 
                * of removal or expiration, meaning a stale item may be returned.
                * the implementation of the dictionary is to manually keep
                * track of expirations
                */

            //init class
            initStatic();

            #region vars
            //the number of expired keys 
            //in the dictionary
            int expiredKeysCount = 0;

            //when the timer cache is null 
            //this gets switched to true
            bool isExpiredTimer = false;

            //logging key. Each user has a logging
            //key and the cache has its count
            //sampled every 3 minutes for each user
            string logTimerKey = string.Concat("SetCache", Util.GetLoginName("PerformanceHelper.SetCache"))
                .GetSHA1HashString();

            //clock for logging the cache count
            DateTimeOffset logTimer;

            //when the passed object is to expire
            DateTimeOffset expiration;

            //dictionary that keeps track of keys and their expirations
            Dictionary<string, DateTimeOffset> cacheExpirations;

            //concurrent dictionary for thread safety
            ConcurrentDictionary<string, DateTimeOffset> cacheExpirationsBag;
            #endregion

            try
            {
                #region initialize vars
                //get the cache instance
                ObjectCache cache = System.Runtime.Caching.MemoryCache.Default;

                //set the expiration for the object to be cached
                expiration = DateTimeOffset.Now.AddHours(duration);

                #region log timer
                //check for expired timer (null is expired)
                try
                {
                    if (GetCache(logTimerKey) == null)
                    {
                        //set log time expiration
                        logTimer = DateTimeOffset.Now.AddMinutes(3);

                        //log event
                        isExpiredTimer = true;
                    }
                    else
                    {
                        //there is an active timer; don't reset.
                        logTimer = (DateTimeOffset)GetCache(logTimerKey);
                    }
                }
                catch
                {
                    //set log time expiration
                    logTimer = DateTimeOffset.Now.AddMinutes(3);

                    //log event
                    isExpiredTimer = true;
                }
                #endregion

                //get the dictionary of cached objects with expirations
                var tempDict = (Dictionary<string, DateTimeOffset>)cache.Get(p.ExpirationKey);

                #endregion

                //create concurrent dictionary when
                //no dictionary is found in cache. the concurrent
                //bag is to ensure this method is thread safe.
                if (tempDict == null)
                {
                    //create new bag if none currently exist
                    cacheExpirationsBag = new ConcurrentDictionary<string, DateTimeOffset>();
                }
                else
                {
                    lock (tempDict)
                    {
                        try
                        {
                            //set the expiration bag to the
                            //dictionary that has already been cached
                            cacheExpirationsBag = new ConcurrentDictionary<string, DateTimeOffset>(tempDict);
                        }
                        catch
                        {
                            cacheExpirationsBag = null;
                        }
                    }
                }

                #region set expirations
                //expiration for passed object
                CacheItemPolicy cacheObjectPolicy = new CacheItemPolicy
                {
                    AbsoluteExpiration = expiration
                };

                //expiration for the dictionary of expirations
                CacheItemPolicy cacheDictionaryPolicy = new CacheItemPolicy
                {
                    AbsoluteExpiration = DateTimeOffset.Now.AddHours(8.0)
                };
                #endregion

                #region cache passed object, expiration dictionary, and log timer

                if (key != null
                    && key != string.Empty
                    && obj != null
                    && cacheExpirationsBag != null)
                {
                    //set the expiration date
                    cacheExpirationsBag[key] = expiration;
                    cacheExpirationsBag[logTimerKey] = logTimer;

                    //convert concurrent dictionary to dictionary
                    cacheExpirations = cacheExpirationsBag.ToDictionary(x => x.Key, x => x.Value);

                    if (cacheExpirations != null && cache != null)
                    {
                        lock (cacheExpirations)
                        {
                            lock (cache)
                            {
                                try
                                {
                                    //cache the dictionary of keys and expirations
                                    cache.Set(p.ExpirationKey, cacheExpirations, cacheDictionaryPolicy);

                                    //cache the logging timer. when this
                                    //expires, a sample of the cache count
                                    //will be posted to the error log as info.
                                    cache.Set(logTimerKey, logTimer, cacheDictionaryPolicy);

                                    //cache the passed object
                                    cache.Set(key, obj, cacheObjectPolicy);

                                    //this is to calm down sampling. Each
                                    //user reports the count every 3 minutes
                                    //after the expiration dictionary contains
                                    //over 2 items.
                                    if (isExpiredTimer && cacheExpirations.Count > 2)
                                    {
                                        expiredKeysCount = cacheExpirations
                                            ?.Where(x => x.Value < DateTimeOffset.Now)
                                            ?.Count() ?? 0;

                                        ErrorHelper.AddErrorMsg("PerformanceHelper.SetCache (info): Cached count ("
                                            + cacheExpirations.Count
                                            + ")");

                                        ErrorHelper.AddErrorMsg("PerformanceHelper.SetCache (info): Expired cached count ("
                                            + expiredKeysCount
                                            + ")");
                                    }
                                }
                                catch (Exception e)
                                {
                                    ErrorHelper.AddErrorMsg("PerformanceHelper.SetCache: " + e);
                                }
                            }
                        }
                    }
                }
                #endregion
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("PerformanceHelper.SetCache: " + e);
            }
            #endregion
        }
        #endregion

        /******************************************************/
        /// <summary>
        /// Adds item to memory cache and retains the key
        /// in the key list (Key Chain). The elements in the key list
        /// will be purged with resets or expiration,
        /// whichever comes first. This is used to track objects
        /// that must be cleared when there is a database change
        /// where all users must have consistent data e.g.
        /// an assignment transfer must result in all users
        /// seeing the new owner.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="obj"></param>
        /// <param name="duration"></param>
        public static void SetCacheManageKey(string key, object obj, double duration = 4.0)
        {
            #region implmenatation
            try
            {
                SetCache(key, obj, duration);
                appendKeyList(key);
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("PerformanceHelper.SetCacheManageKey: " + e);
            }
            #endregion
        }

        /// <summary>
        /// /**************************************************************/
        /// Checks if the timer for a given key is expired.
        /// This method verifies whether the cached timer associated with the provided key is expired.
        /// If the timer is expired or not found, it sets a new expiration timer (3 minutes from now) and returns true.
        /// Otherwise, it returns false with the current active timer.
        /// </summary>
        /// <param name="key">The key associated with the timer. If null, a default hashed key based on the current user's login is used.</param>
        /// <param name="logTimer">Outputs the new expiration time if the timer was expired; otherwise, outputs the current active timer.</param>
        /// <returns>
        /// True if the timer was expired and a new timer is set; false if an active timer exists and is still valid.
        /// </returns>
        /// <remarks>
        /// Example usage:
        /// <code>
        /// bool expired = isTimerExpired("MyKey", out DateTimeOffset? timer);
        /// // If expired is true, timer holds a new expiration time 3 minutes from now.
        /// </code>
        /// </remarks>
        private static bool isTimerExpired(string key, out DateTimeOffset? logTimer)
        {
            #region implementation
            // Initialize the return flag to false, indicating that by default the timer is not expired.
            bool ret = false;

            // Generate the logging key using the provided key.
            // If key is null, create a default key by combining "SetCache" with the current user's login name and hashing it.
            string logTimerKey = key ?? $"SetCache{Util.GetLoginName("PerformanceHelper.SetCache")}".GetSHA1HashString();

            // Check if the timer is expired.
            // A null cache entry indicates an expired timer.
            try
            {
                // Retrieve the timer from cache using the generated key.
                if (GetCache(logTimerKey) == null
                    || (DateTimeOffset)GetCache(logTimerKey) < DateTimeOffset.Now)
                {
                    // If there is no cached timer or it is expired, set a new expiration timer 3 minutes from now.
                    logTimer = DateTimeOffset.Now.AddMinutes(3);
                    // Mark that the timer was expired.
                    ret = true;
                }
                else
                {
                    // If an active timer exists, retrieve it from the cache without resetting.
                    logTimer = (DateTimeOffset)GetCache(logTimerKey);
                }
            }
            catch
            {
                // In case of an exception (e.g., cache retrieval failure), assume the timer is expired.
                // Set a new expiration timer 3 minutes from now.
                logTimer = DateTimeOffset.Now.AddMinutes(3);
                // Mark that the timer was expired.
                ret = true;
            }
            // Return the result indicating whether a new timer was set.
            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Adds submitted key to a cached key list. The key
        /// list can be enumerated and is used so that cached
        /// items can be cleared in bulk. Store keys here
        /// when you want the item removed from cache during
        /// a Performance Helper Reset.
        /// </summary>
        /// <param name="toKeyList"></param>
        private static void appendKeyList(string toKeyList)
        {
            #region implmenatation

            initStatic();
            try
            {
                if (!string.IsNullOrEmpty(toKeyList)
                    && keySet != null)
                {
                    keySet.TryAdd(toKeyList, 0);
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("PerformanceHelper.appendKeyList: " + e);
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Clears all the cached items that have a key
        /// in the cached key list.
        /// </summary>
        private static void removeKeyListItems()
        {
            #region implmenatation
            try
            {
                if (keySet != null
                    && keySet.Count > 0)
                {
                    lock (keySetLock)
                    {
                        foreach (var key in keySet.Keys)
                        {
                            RemoveCache(key);
                        }

                        keySet.Clear();
                    }
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("PerformanceHelper.removeKeyListItems: " + e);
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Returns a generic object from cache for the passed
        /// key. If there is no result, then null is returned
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static object? GetCache(string key)
        {
            #region implmenatation
            //init class
            initStatic();

            DateTimeOffset expiration;
            bool isKeyed = false;
            object? returnObject = null;

            try
            {
                if (!string.IsNullOrEmpty(key))
                {
                    ObjectCache cache = System.Runtime.Caching.MemoryCache.Default;

                    if (cache != null)
                    {
                        lock (cacheLock)
                        {
                            #region locked cache

                            try
                            {
                                //if the object isn't expired then return it
                                returnObject = cache.Get(key);

                            }
                            catch (Exception e)
                            {
                                ErrorHelper.AddErrorMsg("PerformanceHelper.GetCache (cache): " + e);
                            }

                            #endregion
                        }

                        #region checking for removal requirement
                        if (expirationMap != null)
                        {
                            try
                            {
                                isKeyed = (cache.Contains(key) && expirationMap.ContainsKey(key));

                                //make sure cache contains the key and we have expirations to check
                                if (isKeyed)
                                {
                                    //get the expiration for the requested cached object
                                    expiration = expirationMap[key];

                                    //is the item is older than expiration date
                                    if (expiration <= DateTimeOffset.Now)
                                    {
                                        //if the object is expired then remove it
                                        RemoveCache(key);

                                        //set return object to null
                                        returnObject = null;
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                ErrorHelper.AddErrorMsg("PerformanceHelper.GetCache (remove error): " + e);
                            }
                        }
                        #endregion
                    }
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("PerformanceHelper.GetCache: " + e);
            }

            return returnObject;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// This is an overload that provides unboxing for the
        /// cached item. It lets you make calls that return
        /// specific model types.
        /// 
        /// e.g. PerformanceHelper.GetCache&lt;AssignmentItem&gt;(key)
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <example>PerformanceHelper.GetCache&lt;AssignmentItem&gt;(key)</example>
        /// <seealso cref="GetCache(string)"/>
        public static T? GetCache<T>(string key) =>
            GetCache(key) == null
            ? default : (T?)GetCache(key);

        /******************************************************/
        /// <summary>
        /// Removes an item that has been cached. 
        /// </summary>
        /// <param name="key"></param>
        public static void RemoveCache(string key)
        {
            #region implmenatation
            /*
                 * NOTE: the cache expiration is being updated
                 * here b/c in Dev calling "remove" wasn't resulting
                 * in immediate expiration, and stale cached data was 
                 * still being returned. Setting an ultra short 
                 * expiration is an attempt to insure the stale cached
                 * object isn't returned. The expiration dictionary
                 * should suppress all stale items.
                 */

            try
            {
                ObjectCache cache = System.Runtime.Caching.MemoryCache.Default;

                if (cache != null)
                {
                    lock (cacheLock)
                    {
                        try
                        {
                            if (key != null && cache.Contains(key))
                            {
                                var obj = cache.Get(key);

                                if (obj != null)
                                {
                                    // Update the static expiration map with the new object's expiration.
                                    expirationMap[key] = DateTimeOffset.Now;

                                    dispose(obj);

                                    obj = null;
                                }

                                //calls cache removal. Actual disposal depends
                                //upon garbage collection
                                cache.Remove(key);
                            }
                        }
                        catch (Exception e)
                        {
                            ErrorHelper.AddErrorMsg("PerformanceHelper.RemoveCache: " + e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("PerformanceHelper.RemoveCache: " + e);
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Calls the dispose method using reflection for
        /// the cached object
        /// </summary>
        /// <param name="obj"></param>
        private static void dispose(object obj)
        {
            #region implmenatation
            try
            {
                if (obj != null && isDisposable(obj))
                {
                    MethodInfo? mi = obj?.GetType()?.GetMethod("Dispose");

                    if (mi != null)
                        mi.Invoke(obj, null);
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("PerformanceHelper.dispose: " + e);
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Determines whether an object implements the method "Dispose"
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        private static bool isDisposable(object obj)
        {
            #region implmenatation
            bool disposable = false;
            StringComparison ignoreCase = StringComparison.InvariantCultureIgnoreCase;

            try
            {
                if (obj != null)
                {
                    disposable = obj
                        ?.GetType()
                        ?.DeclaringType
                        ?.GetInterfaceMap(typeof(IDisposable))
                            .TargetMethods
                                .Any(x => x != null
                                    && !string.IsNullOrEmpty(x.Name)
                                    && x.Name.Equals("Dispose", ignoreCase))
                                ?? false;
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("PerformanceHelper.IsDisposable: " + e);
            }

            return disposable;
            #endregion
        }
        #endregion

    }
}