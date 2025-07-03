using System;
using System.Text;
using System.ComponentModel;
using System.Reflection;
using System.Security.Cryptography;
using System.Data;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using Color = System.Drawing.Color;
using System.Drawing;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.IdentityModel.Tokens;
using System.Security.Principal;
using Newtonsoft.Json;


namespace MedRecPro.Helpers
{

    public static class Util
    {
        private static readonly object lockObj = new object();

        // Replaces CallContext usage with AsyncLocal.
        private static readonly AsyncLocal<string> _userName = new AsyncLocal<string>();

        // IHttpContextAccessor should be injected via DI.
        // You can assign it via a static property for legacy static code usage, 
        // but it's generally better to refactor to instance methods or 
        // pass it as a parameter.
        private static IHttpContextAccessor? _httpContextAccessor;

        public static HttpContext? HttpContext => _httpContextAccessor?.HttpContext;

        // Initialize this once in your application startup:
        // Util.Initialize(httpContextAccessor);
        public static void Initialize(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /**************************************************************/
        /// <summary>
        /// Safely parses a string to a nullable integer.
        /// </summary>
        /// <param name="value">String value to parse</param>
        /// <returns>Parsed integer or null if parsing fails</returns>
        public static int? ParseNullableInt(string value) => int.TryParse(value, out int result) ? result : null;

        /**************************************************************/
        /// <summary>
        /// Safely parses a string to a nullable decimal.
        /// </summary>
        /// <param name="value">String value to parse</param>
        /// <returns>Parsed decimal or null if parsing fails</returns>
        public static decimal? ParseNullableDecimal(string value) => decimal.TryParse(value, out decimal result) ? result : null;

        /**************************************************************/
        /// <summary>
        /// Safely parses a string to a nullable GUID.
        /// </summary>
        /// <param name="value">String value to parse</param>
        /// <returns>Parsed GUID or null if parsing fails</returns>
        public static Guid? ParseNullableGuid(string value) => Guid.TryParse(value, out Guid result) ? result : null;

        /**************************************************************/
        /// <summary>
        /// Safely parses SPL date/time strings which can be in various formats.
        /// </summary>
        /// <param name="value">String value to parse</param>
        /// <returns>Parsed DateTime or null if parsing fails</returns>
        /// <remarks>
        /// Handles SPL date formats: YYYY, YYYYMM, YYYYMMDD, YYYYMMDDHHMMSS, etc.
        /// </remarks>
        public static DateTime? ParseNullableDateTime(string value)
        {
            /**************************************************************/
            #region implementation
            if (string.IsNullOrWhiteSpace(value)) return null;

            // Dates in SPL can be YYYY, YYYYMM, YYYYMMDD, YYYYMMDDHHMMSS etc.
            // DateTime.Parse/TryParse might need specific format strings.
            // For YYYYMMDD:
            if (value.Length == 8 && int.TryParse(value.Substring(0, 4), out int year) &&
                int.TryParse(value.Substring(4, 2), out int month) &&
                int.TryParse(value.Substring(6, 2), out int day))
            {
                try { return new DateTime(year, month, day); }
                catch { /* Fall through */ }
            }
            return DateTime.TryParse(value, out DateTime result) ? result : null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Safely parses a string to a nullable decimal.
        /// </summary>
        /// <param name="value">String value to parse</param>
        /// <returns>Parsed decimal or null if parsing fails</returns>
        public static decimal? parseNullableDecimal(string value) => decimal.TryParse(value, out decimal result) ? result : null;

        /**************************************************************/
        /// <summary>
        /// Safely parses a string to a nullable boolean.
        /// </summary>
        /// <param name="value">String value to parse</param>
        /// <returns>Parsed boolean or null if parsing fails</returns>
        public static bool? ParseNullableBoolWithStringValue(string value) => bool.TryParse(value, out bool result) ? result : (bool?)null;

        /**************************************************************/
        /// <summary>
        /// Converts a boolean value to nullable boolean (overload for direct bool input).
        /// </summary>
        /// <param name="value">Boolean value to convert</param>
        /// <returns>The boolean value as nullable boolean</returns>
        public static bool? ParseNullableBool(bool value) => value; // Overload for direct bool

        /******************************************************/
        /// <summary>
        /// Returns the token type based on the passed enum
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static string GetTokenType(Enum type)
        {
            #region implementation
            string ret = string.Empty;

            //try
            //{
            //    switch (type)
            //    {

            //        case c.TokenType.Graph:
            //            ret = c.GR_TOKEN;
            //            break;

            //        case c.TokenType.Exchange:
            //            ret = c.EX_TOKEN;
            //            break;

            //        case c.TokenType.SharePoint:
            //            ret = c.SP_TOKEN;
            //            break;
            //    }
            //}
            //catch (Exception e)
            //{
            //    ErrorHelper.AddErrorMsg("Util.GetTokenType: " + e);
            //}

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Extracts the bearer token from the X-Token, Authorization header or cache
        /// </summary>
        public static string GetBearerToken(Enum type, IHttpContextAccessor? httpContextAccessor = null, Guid? employeeGuid = null)
        {

            string token = string.Empty; ;
            string? tokenKey;

            try
            {

                string tokenType = GetTokenType(type);
                HttpContext? httpContext = httpContextAccessor?.HttpContext
                    ?? _httpContextAccessor?.HttpContext
                    ?? new HttpContextAccessor().HttpContext;

                ErrorHelper.AddErrorMsg($"Util.GetBearerToken Passed GUID (info): {employeeGuid}");
                ErrorHelper.AddErrorMsg($"Util.GetBearerToken Token User (info): {Util.GetUserName(employeeGuid)}");

                // Get the Authorization header from the current request
                var authHeader = httpContext?.Request.Headers["X-Token"].FirstOrDefault()
                ?? httpContext?.Request.Headers["Authorization"].FirstOrDefault();

                if (string.IsNullOrEmpty(authHeader))
                {
                    ErrorHelper.AddErrorMsg($"Util.GetBearerToken (info): No Authorization header present");
                }
                else
                {
                    ErrorHelper.AddErrorMsg($"Util.GetBearerToken Auth Header (info): {TextUtil.Truncate(authHeader, 30)}");
                }

                // Check if it's a Bearer token and extract it
                if (!string.IsNullOrEmpty(authHeader)
                    && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = authHeader.Substring("Bearer ".Length).Trim();

                    ErrorHelper.AddErrorMsg($"Util.GetBearerToken Token Header (info): {TextUtil.Truncate(token ?? "Empty", 30)}");

                    return token ?? string.Empty;
                }

                tokenKey = TokenCacheMiddleware.GetTokenCacheKey(Util.GetUserName(employeeGuid), tokenType);

                // Check if the token is in the cache
                if (!string.IsNullOrEmpty(tokenKey))
                {
                    return TokenCacheMiddleware.GetTokenFromCache(tokenKey) ?? string.Empty;
                }

                return string.Empty;
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg($"Util.GetBearerToken: {e.Message}");
                throw e;
            }
        }

        /******************************************************/
        /// <summary>
        /// Retrieves the login name of the current user in a thread-safe manner. 
        /// If the username is not available, attempts to get it from the Windows identity.
        /// The username is then cached using AsyncLocal for future use.
        /// </summary>
        /// <param name="callingMethod">
        /// Optional. The name of the method invoking this function, used for error logging.
        /// </param>
        /// <returns>
        /// A string representing the login name of the current user, or null if the login name cannot be determined.
        /// </returns>
        public static string? GetLoginName(string? callingMethod = null)
        {
            string? result = null;
            try
            {
                // Attempt to retrieve the username from the AsyncLocal cache
                result = _userName.Value;

                if (string.IsNullOrEmpty(result))
                {
                    // Retrieve from the current HTTP context
                    var context = _httpContextAccessor?.HttpContext;
                    string? user = context?.User?.Identity?.Name;

                    if (string.IsNullOrEmpty(user))
                    {
                        // If no user is found in the current context, attempt to get the user from WindowsIdentity.
                        // Note: Impersonation is different in .NET Core. If you still need impersonation logic,
                        // you must acquire a SafeAccessTokenHandle and call WindowsIdentity.RunImpersonated.
                        // For now, just get the current identity:
                        user = WindowsIdentity.GetCurrent()?.Name;

                    }

                    if (!string.IsNullOrEmpty(user))
                    {
                        // Extract the username after the last backslash
                        int lastBackslashIndex = user.LastIndexOf('\\');
                        result = lastBackslashIndex >= 0 ? user.Substring(lastBackslashIndex + 1) : user;

                        if (!string.IsNullOrEmpty(result))
                        {
                            lock (lockObj)
                            {
                                // Store in AsyncLocal for future access
                                _userName.Value = result;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Log error with context-specific message
                if (!string.IsNullOrEmpty(callingMethod))
                {
                    ErrorHelper.AddErrorMsg($"Util.GetLoginName ({callingMethod}): {e.Message}");
                }
                else
                {
                    ErrorHelper.AddErrorMsg($"Util.GetLoginName: {e.Message}");
                }

                return result;
            }

            return result;
        }

        /******************************************************/
        /// <summary>
        /// Determines whether the specified enumerable sequence is null or contains no elements.
        /// </summary>
        /// <typeparam name="T">The type of elements in the sequence.</typeparam>
        /// <param name="source">The enumerable sequence to check.</param>
        /// <returns>
        /// true if the sequence is null or empty; otherwise, false.
        /// </returns>
        /// <example> 
        /// var list = new List<int>();
        /// bool isEmpty = list.IsNullOrEmpty(); // Returns true
        /// 
        /// list.Add(1);
        /// isEmpty = list.IsNullOrEmpty(); // Returns false
        /// </example>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> source)
        {
            return source?.Any() != true;
        }

        /******************************************************/
        /// <summary>
        /// Returns the username for the current windows identity.
        /// Use this result as a parameter for obtaining the 
        /// QDOC user GUID. The user GUID is a needed parameter
        /// for all Stored Procedure calls.
        /// </summary>
        /// <returns></returns>
        public static string? GetUserName(Guid? employeeGuid = null)
        {
            #region implentation
            string? name = null;
            string key = string.Empty;

            try
            {
                //#region try cache/db
                //if (!employeeGuid.IsNullOrEmpty())
                //{
                //    key = ($"Util.GetUserName_{employeeGuid}").GetHashString();

                //    if (!string.IsNullOrEmpty(key))
                //    {
                //        name = (string) PerformanceHelper.GetCache(key);

                //        if (!string.IsNullOrEmpty(name))
                //        {
                //            return name;
                //        }
                //        else
                //        {
                //            name = new EmployeeData().GetEmployee((Guid)employeeGuid)?.UserName;

                //            if (!string.IsNullOrEmpty(name))
                //            {
                //                PerformanceHelper.SetCache(key, name, 8.0);

                //                ErrorHelper.AddErrorMsg($"Util.GetUserName DB user (info): {name}");

                //                return name;
                //            }
                //        }
                //    }
                //} 
                //#endregion

                //var httpContext = _httpContextAccessor?.HttpContext ?? new HttpContextAccessor()?.HttpContext;

                //if (httpContext != null)
                //{
                //    // Check if the client passed a username in a custom header.
                //    if (httpContext.Request.Headers.TryGetValue("X-Username", out var headerUsername))
                //    {
                //        name = headerUsername.FirstOrDefault();

                //        ErrorHelper.AddErrorMsg($"Util.GetUserName X-Username (info): {name}");
                //    }
                //    // Otherwise, fall back to the identity from the request.
                //    else if (httpContext.User?.Identity?.IsAuthenticated == true)
                //    {
                //        name = httpContext.User.Identity.Name;
                //    }
                //}

                //// As a final fallback, use the current Windows identity.
                //if (string.IsNullOrEmpty(name))
                //{
                //    name = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                //}

                //if (!string.IsNullOrEmpty(name))
                //{
                //    string[] parsedName = name.Split(new char[] { '\\' });
                //    name = parsedName[parsedName.Length - 1];
                //}
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.GetUserName: " + e);
                throw new Exception(name, e);
            }

            return name;
            #endregion
        }

        /******************************************************/
        public static Guid? TryGuidParse(this string value)
        {
            #region implementation
            Guid? ret = null;
            Guid val;
            try
            {
                Guid.TryParse(value, out val);

                if (val != Guid.Empty)
                {
                    ret = val;
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.TryGuidParse: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Blocks while condition is true or timeout occurs.
        /// https://stackoverflow.com/questions/29089417/c-sharp-wait-until-condition-is-true
        /// https://dotnetfiddle.net/Vy8GbV
        /// </summary>
        /// <param name="condition">The condition that will perpetuate the block.</param>
        /// <param name="frequency">The frequency at which the condition will be check, in milliseconds.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <exception cref="TimeoutException"></exception>
        /// <returns></returns>
        public static async Task WaitWhile(Func<bool> condition, int frequency = 25, int timeout = -1)
        {
            #region implementation
            var waitTask = Task.Run(async () =>
               {
                   while (condition()) await Task.Delay(frequency);
               });

            if (waitTask != await Task.WhenAny(waitTask, Task.Delay(timeout)))
                throw new TimeoutException();
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Blocks until condition is true or timeout occurs.
        /// https://stackoverflow.com/questions/29089417/c-sharp-wait-until-condition-is-true
        /// https://dotnetfiddle.net/Vy8GbV
        /// </summary>
        /// <param name="condition">The break condition.</param>
        /// <param name="frequency">The frequency at which the condition will be checked.</param>
        /// <param name="timeout">The timeout in milliseconds.</param>
        /// <returns></returns>
        public static async Task WaitUntil(Func<bool> condition, int frequency = 25, int timeout = -1)
        {
            #region implementation
            var waitTask = Task.Run(async () =>
              {
                  while (!condition()) await Task.Delay(frequency);
              });

            if (waitTask != await Task.WhenAny(waitTask,
                    Task.Delay(timeout)))
                throw new TimeoutException();
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Extension method to set a specified timeout for an
        /// async task.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="task"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        /// <remarks>https://stackoverflow.com/questions/4238345/asynchronously-wait-for-taskt-to-complete-with-timeout</remarks>
        /// <exception cref="TimeoutException"></exception>
        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            #region implementation

            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {

                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                if (completedTask == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;  // Very important in order to propagate exceptions
                }
                else
                {
                    throw new TimeoutException("The operation has timed out.");
                }
            }

            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Provides a random color that is defined by the
        /// windows system e.g. blue, black, aqua, etc. Since
        /// this selects a random color there is no way to know
        /// in advance whether the selected color meets the
        /// brightness specification. When it the random color
        /// is above the requested brightness, then this will recursively
        /// call itself until a color matches the request. The
        /// overFlowCheck sets the max number of times this
        /// will attempt to satisfy the brightness requirement. Use
        /// ToRBA to create a color string that can be used
        /// in JSON/JavaScript.
        /// </summary>
        /// <param name="maxBrightness">(optional) default is 0.5. Threshold for how dark a color you want. Values 0.0 (black) - 1.0 (white)</param>
        /// <param name="overFlowCheck">(optional) default is 30. The max number of times a recursive call is permitted</param>
        /// <returns></returns>
        /// <seealso cref="TextUtil.ToRGBA(KnownColor, double)"/>
        public static KnownColor GetRandomColor(double maxBrightness = 0.5, int overFlowCheck = 30)
        {
            #region implementation

            KnownColor ret = new KnownColor();

            Random rand;
            List<KnownColor> colorList;
            int maxColorIndex;
            float brightness;

            try
            {
                rand = new Random(Guid.NewGuid().GetHashCode());

                colorList = Enum.GetValues(typeof(KnownColor))
                .Cast<KnownColor>()
                .Where(clr => !(Color.FromKnownColor(clr).IsSystemColor))
                .ToList();

                maxColorIndex = colorList.Count();

                ret = colorList[rand.Next(0, maxColorIndex)];

                brightness = Color.FromKnownColor(ret).GetBrightness();

                //if the color is too bright get a new one.
                //the overflow check is the max number of
                //times we can retry to get a color.
                if (overFlowCheck >= 0
                    && maxBrightness < 1.0
                    && brightness > maxBrightness)
                {
                    return GetRandomColor(maxBrightness, (overFlowCheck - 1));
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.GetRandomColor: " + e);
            }

            return ret;

            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Converts a url that is prefixed with 'http' to 
        /// one that is prefixed with 'https'. Any references to port
        /// 80 and 8080 are removed too.
        /// </summary>
        /// <param name="uriToSecure"></param>
        /// <returns></returns>
        public static string ToSecureUri(this string uriToSecure)
        {
            #region implementation

            string ret = uriToSecure;
            string verbPattern = @"(http:)";
            string portPattern = @"(:8080)|(:80)";
            string replacementVerb = "https:";
            try
            {
                ret = Regex.Replace(uriToSecure, verbPattern, replacementVerb);
                ret = Regex.Replace(ret, portPattern, string.Empty);
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.ToSecureUri: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Takes an integer and a max integer and returns a double
        /// between 0.0 and 1.0
        /// </summary>
        /// <param name="number"></param>
        /// <param name="maxNumber"></param>
        /// <returns></returns>
        public static double Normalize(this int number, int maxNumber)
        {
            #region implementation
            double ret = 0.0;

            try
            {
                ret = (Convert.ToDouble(number) / Convert.ToDouble(maxNumber)) * 1.0;
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.Normalize: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Takes a value between 0 and 1 and returns a color
        /// the falls within the gradient between red and green
        /// </summary>
        /// <param name="interpolationFactor"></param>
        /// <returns></returns>
        /// <remarks>The interpolationFactor is assumed to be in the range of 0.0 ... 1.0.
        /// The colors are assumed to be in the range of 0 ... 255.</remarks>
        public static Color GetInterpolatedRedToGreen(this double interpolationFactor)
        {
            #region implementation
            Color ret = new Color();
            Color mColor1 = Color.FromArgb(255, 26, 179, 148); //green
            Color mColor2 = Color.FromArgb(255, 255, 255, 0); //yellow
            Color mColor3 = Color.FromArgb(255, 200, 0, 0); //red

            try
            {

                double interpolationFactor1 = Math.Max(interpolationFactor - 0.5, 0.0);
                double interpolationFactor2 = 0.5 - Math.Abs(0.5 - interpolationFactor);
                double interpolationFactor3 = Math.Max(0.5 - interpolationFactor, 0.0);

                ret = Color.FromArgb(255,
                            (byte)((mColor1.R * interpolationFactor1 +
                                    mColor2.R * interpolationFactor2 +
                                    mColor3.R * interpolationFactor3) * 2.0),

                            (byte)((mColor1.G * interpolationFactor1 +
                                    mColor2.G * interpolationFactor2 +
                                    mColor3.G * interpolationFactor3) * 2.0),

                            (byte)((mColor1.B * interpolationFactor1 +
                                    mColor2.B * interpolationFactor2 +
                                    mColor3.B * interpolationFactor3) * 2.0));
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.GetInterpolatedGreenToRed: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Walks through a list of strings and returns
        /// the first items that is a GUID. If no GUID is
        /// found then null is returned.
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public static Guid? TryGetGuid(this List<string> list)
        {
            #region implementation
            Guid? ret = null;
            Guid guid;
            bool isGuid = false;

            try
            {
                foreach (string item in list)
                {
                    string word = item.Trim();

                    isGuid = Guid.TryParse(word, out guid);

                    if (isGuid)
                    {
                        ret = guid;
                        break;
                    }

                    isGuid = false;
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.TryGetGuid: " + e);
            }

            return ret;
            #endregion
        }
        /******************************************************/
        /// <summary>
        /// Returns the fiscal year as integer for the passed
        /// date. The newYearMonth designates the month number
        /// for the start of the new fiscal year. If the newYearMonth
        /// is January i.e. 1, then it is assumed the calendar
        /// and fiscal year are the same.
        /// 
        /// e.g. DateTime.Now.ToFiscalYear() //return current fiscal year
        /// </summary>
        /// <param name="date"></param>
        /// <param name="newYearMonth">(optional) default is October i.e. 10</param>
        /// <returns></returns>
        public static int ToFiscalYear(this DateTime date, int newYearMonth = 10)
        {
            #region implementation
            int ret = 0;
            int month = date.Month;

            try
            {
                //if we pass the new year
                //month then add one to the
                //year. If the new year month
                //is January, then assume fiscal
                //and calendar year are equal.
                if (month >= newYearMonth
                    && newYearMonth != 1)
                {
                    ret = date.Year + 1;
                }
                else
                {
                    ret = date.Year;
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.ToFiscalYear: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Sets a value in an object, used to hide all the logic that goes into
        /// handling this sort of thing
        /// </summary>
        /// <param name="target"></param>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// /******************************************************/
        public static void SetValueFromString(this object target, string propertyName, string propertyValue)
        {
            #region implementation
            PropertyInfo oProp = target?.GetType()?.GetProperty(propertyName);
            Type tProp = oProp?.PropertyType;

            //Nullable properties have to be treated differently, since we 
            //  use their underlying property to set the value in the object
            if (tProp != null
                && tProp.IsGenericType
                && tProp.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
            {
                //if it's null, just set the value from the reserved word null, and return
                if (oProp != null
                    && target != null
                    && propertyValue == null)
                {
                    oProp.SetValue(target, null, null);
                    return;
                }

                //Get the underlying type property instead of the nullable generic
                tProp = new NullableConverter(oProp.PropertyType).UnderlyingType;
            }

            //use the converter to get the correct value
            if (oProp != null
                && target != null
                && tProp != null
                && !string.IsNullOrEmpty(propertyValue))
            {
                oProp?.SetValue(target, Convert.ChangeType(propertyValue, tProp), null);
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Takes an object and returns a string representation
        /// of the SHA1 Hash. If the object of type T is a List,
        /// then this will produce the same result as GetListHashString()
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="recursion">(optional) default is false. Halts recursive call when true.</param>
        /// <returns></returns>
        /// <seealso cref="GetListHashString{T}(IEnumerable{T})"/>
        /// <seealso cref="TextUtil.ToCommaString{T}(T)"/>
        public static string GetSHA1HashString<T>(this T obj, bool recursion = false)
        {
            #region implementation
            string ret = null;
            string txt = null;
            HashAlgorithm alg = SHA1.Create();
            StringBuilder sb = new StringBuilder();
            List<string> objVals = new List<string>(10);

            try
            {
                if (obj != null)
                {
                    //when the object is a string
                    if (obj.GetType().Name.Equals("string", StringComparison.InvariantCultureIgnoreCase))
                    {
                        txt = Convert.ToString(obj);

                        //get the hash code
                        byte[] hash = alg.ComputeHash(Encoding.UTF8.GetBytes(txt));

                        //build has from each byte
                        foreach (byte b in hash)
                        {
                            sb?.Append(b.ToString("X2"));
                        }

                        ret = sb?.ToString();
                    }
                    //this object is NOT a string
                    else
                    {
                        ret = GetListHashString(obj as IEnumerable<T>);

                        //the object wasn't a list
                        if (string.IsNullOrEmpty(ret) && !recursion)
                        {
                            ret = GetSHA1HashString(obj.ToCommaString(), true);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.GetHashString: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Computes a hash string for an object using SHA256.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="obj">The object to hash.</param>
        /// <param name="recursion">Internal flag to indicate recursive call, typically for handling non-string, non-list objects.</param>
        /// <returns>A SHA256 hash string, or null if the object is null or an error occurs.</returns>
        /// <remarks>
        /// This method handles various object types differently:
        /// - For strings: Directly computes the SHA256 hash
        /// - For collections: Converts to a string representation and then hashes
        /// - For other objects: Attempts to convert to a string representation before hashing
        /// 
        /// The recursion parameter prevents infinite loops when processing complex object graphs.
        /// </remarks>
        /// <example>
        /// string hash = "Hello, World!".GetHashString();
        /// // Returns a SHA256 hash of the string "Hello, World!"
        /// 
        /// List&lt;int&gt; numbers = new List&lt;int&gt; { 1, 2, 3 };
        /// string listHash = numbers.GetHashString();
        /// // Returns a hash of the string representation of the list
        /// </example>
        public static string? GetSHA256HashString<T>(this T obj, bool recursion = false)
        {
            #region implementation
            string? ret = null;
            string? txt = null;
            // Use SHA256 for a stronger hash
            using (SHA256 alg = SHA256.Create())
            {
                StringBuilder sb = new StringBuilder();

                try
                {
                    if (obj != null)
                    {
                        //when the object is a string
                        if (obj is string stringObj)
                        {
                            txt = stringObj;

                            //get the hash code
                            byte[] hash = alg.ComputeHash(Encoding.UTF8.GetBytes(txt));

                            //build hash from each byte
                            foreach (byte b in hash)
                            {
                                sb.Append(b.ToString("X2")); // Use Append directly
                            }
                            ret = sb.ToString();
                        }
                        //this object is NOT a string
                        else if (obj is System.Collections.IEnumerable enumerableObj && !(obj is string)) // Check if it's IEnumerable but not a string
                        {

                            if (!recursion) // Prevent infinite recursion if ToCommaString calls GetHashString
                            {
                                // Convert the enumerable to a canonical string representation for hashing
                                // This relies on a consistent ToCommaString or similar serialization for the enumerable.
                                // For complex scenarios, you might want a dedicated list hashing strategy.
                                var listAsString = convertEnumerableToString(enumerableObj);
                                ret = GetSHA256HashString(listAsString, true); // Recursive call with the string representation
                            }
                            else
                            {
                                // If already in recursion (e.g. called from the fallback), convert to basic string and hash.
                                txt = Convert.ToString(obj); // Fallback to simple ToString

                                if (!string.IsNullOrEmpty(txt))
                                {
                                    byte[] hash = alg.ComputeHash(Encoding.UTF8.GetBytes(txt));
                                    foreach (byte b in hash) { sb.Append(b.ToString("X2")); }
                                    ret = sb.ToString();
                                }
                            }
                        }
                        // For other object types (not string, not IEnumerable)
                        else
                        {
                            // The object wasn't a string or a recognized IEnumerable.
                            // Fallback to converting the object to a string and hashing that.                          
                            if (!recursion) // Prevent infinite recursion if ToCommaString calls GetHashString
                            {
                                string objectAsText = convertToTextRepresentation(obj);
                                ret = GetSHA256HashString(objectAsText, true);
                            }
                            else
                            {
                                // If already in recursion (e.g. ToCommaString itself resulted in an object that came back here),
                                // this indicates a complex object structure or a loop.
                                // Fallback to a simple string representation to avoid stack overflow.
                                txt = Convert.ToString(obj); // Basic ToString()

                                if (!string.IsNullOrEmpty(txt))
                                {
                                    byte[] hash = alg.ComputeHash(Encoding.UTF8.GetBytes(txt));
                                    foreach (byte b in hash) { sb.Append(b.ToString("X2")); }
                                    ret = sb.ToString();
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    // ErrorHelper.AddErrorMsg("Util.GetHashString: " + e.ToString()); // Log the full exception
                    Console.WriteLine("Util.GetHashString: " + e.ToString());
                }
            } // `alg` is disposed here

            return ret;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Helper method for converting an IEnumerable to a string representation for hashing.
        /// </summary>
        /// <param name="enumerable">The enumerable collection to convert.</param>
        /// <returns>A string representation of the collection in the format [item1,item2,...].</returns>
        /// <remarks>
        /// Creates a canonical string representation of the collection contents.
        /// For complex collections, consider implementing a more robust serialization strategy.
        /// </remarks>
        private static string convertEnumerableToString(System.Collections.IEnumerable enumerable)
        {
            #region implementation
            if (enumerable == null) return "";
            StringBuilder sb = new StringBuilder();
            sb.Append("["); // Open bracket for collection
            bool first = true;
            foreach (var item in enumerable)
            {
                if (!first) sb.Append(","); // Add comma separator between items                                         
                sb.Append(item?.ToString() ?? "null"); // Append item string or "null" if item is null
                first = false;
            }
            sb.Append("]"); // Close bracket for collection
            return sb.ToString();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Helper method for converting a generic object to a string representation for hashing.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="obj">The object to convert to a string.</param>
        /// <returns>A string representation of the object.</returns>
        /// <remarks>
        /// This method is meant to be a placeholder for a more robust serialization approach.
        /// In a production environment, consider using JSON serialization or a custom ToCommaString() extension method.
        /// </remarks>
        private static string convertToTextRepresentation<T>(T obj)
        {

            #region implementation
            if (obj == null) return "";
            return JsonConvert.SerializeObject(obj); // Fallback to ToString()
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes a hash string for a collection of elements using SHA256.
        /// </summary>
        /// <typeparam name="TElement">The type of elements in the collection.</typeparam>
        /// <param name="list">The collection to hash.</param>
        /// <returns>A SHA256 hash string of the collection, or null if the collection is null.</returns>
        /// <remarks>
        /// This method creates a combined hash by concatenating the hash strings of individual elements.
        /// Empty collections are handled by returning the hash of an empty string.
        /// </remarks>
        /// <example>
        /// List&lt;string&gt; names = new List&lt;string&gt; { "Alice", "Bob", "Charlie" };
        /// string hash = names.GetListHashString();
        /// // Returns a hash that represents the entire collection
        /// </example>
        public static string? GetListHashString<TElement>(this IEnumerable<TElement> list)
        {

            #region implementation
            if (list == null) return null; // Return null for null collections

            // Concatenate hashes of individual elements or their string representations
            StringBuilder combinedContent = new StringBuilder();
            foreach (var item in list)
            {
               
                combinedContent.Append(item.GetSHA256HashString(true) ?? "null"); // Append hash of item or "null"
            }

            if (combinedContent.Length == 0)
            {
                // Decide how to hash an empty list (e.g., hash of empty string or a specific constant)
                return "".GetSHA256HashString(true); // Hash of an empty string
            }

            // Hash the combined content
            return combinedContent.ToString().GetSHA256HashString(true);
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Produces a binary comparison of two objects by
        /// Hashing both and checking equality
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool IsEqual(this object a, object b)
        {
            #region implementation
            //this was made longer than needed for debugging
            string c = GetSHA1HashString(a);
            string d = GetSHA1HashString(b);

            bool ret = c.Equals(d);

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Takes a string and converts it to a GUID if it is
        /// valid input. If it is not valid it returns null and logs
        /// the error.
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public static Guid? ConvertToGUID(this object a)
        {
            #region implementation
            Guid? ret = null;
            try
            {
                ret = Guid.Parse(Convert.ToString(a));
                return ret;
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("ConvertStringToGUID: " + e);
                return null;
            }
            #endregion
        }


        /******************************************************/
        /// <summary>
        /// Gets the time in Ticks since 1/1/1970. This
        /// is used for Flot.js for ploting time based.
        /// charts. Note that the hour passed is assumed
        /// to be EST and is offset by +5 to convert to 
        /// GMT
        /// </summary>
        /// <param name="day"></param>
        /// <param name="month"></param>
        /// <param name="year"></param>
        /// <param name="hour">EST</param>
        /// <param name="UTCOffset"></param>
        /// <returns></returns>
        public static long GetJavaScriptTimestamp(int day, int month, int year, int hour)
        {
            #region implementation

            string timeStamp = month.ToString()
               + "/"
               + day.ToString()
               + "/"
               + year.ToString()
               + " "
               + hour.ToString()
               + ":00:00";

            System.TimeSpan span = new System.TimeSpan(System.DateTime.Parse("1/1/1970").Ticks);
            System.DateTime time = DateTime.Parse(timeStamp).Subtract(span);
            return (long)(time.Ticks / 10000);
            #endregion
        }


        /******************************************************/
        /// <summary>
        /// Determines if a nullable Guid (Guid?) is null or Guid.Empty
        /// https://stackoverflow.com/questions/9837602/why-isnt-there-a-guid-isnullorempty-method
        /// </summary>
        public static bool IsNullOrEmpty(this Guid? guid)
        {
            return (!guid.HasValue || guid.Value == Guid.Empty);
        }

        public static bool IsNullOrEmpty(this Guid guid)
        {
            return (guid == Guid.Empty);
        }

        /******************************************************/
        /// <summary>
        /// Determines if a nullable int (int?) is null or 0
        /// </summary>
        public static bool IsNullOrZero(this int? val)
        {
            #region implementation
            bool ret = false;
            if (val == null)
                ret = true;
            else if (val == 0)
                ret = true;
            return (ret);
            #endregion
        }

        public static bool IsZero(this int val)
        {
            return (val == 0);
        }

        /******************************************************/
        /// <summary>
        /// Given an object this will return the property value.
        /// Pass the name of the property as a string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="propName"></param>
        /// <returns></returns>
        public static T GetPropertyValue<T>(this object obj, string propName)
        {
            #region implementation
            try
            {
                var ret = (T)obj.GetType().GetProperty(propName)?.GetValue(obj, null);
                return ret;
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.GetPropertyValue: " + e);
                return default(T);
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Given an object this will return a property value
        /// converted to a string. If the value is null then
        /// this will return an empty string.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="propName"></param>
        /// <returns></returns>
        public static string GetPropertyValueAsString(this object obj, string propName)
        {
            #region implementation
            try
            {
                string ret = Convert.ToString(obj.GetType()
                    ?.GetProperty(propName)
                    ?.GetValue(obj, null)
                    ?? string.Empty);

                return ret;
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.GetPropertyValueAsString: " + e);
                return string.Empty;
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Overload that provides date formatting
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="propName"></param>
        /// <returns></returns>
        public static string GetPropertyValueAsString(this object obj, string propName, string propType)
        {
            #region implementation
            string ret;

            try
            {
                ret = Convert.ToString(obj.GetType()
                   ?.GetProperty(propName)
                   ?.GetValue(obj, null)
                   ?? string.Empty);

                if (ret != null
                    && ret != string.Empty
                    && propType != null
                    && propType != string.Empty
                    && propType.Equals("date", StringComparison.InvariantCultureIgnoreCase)
                    )
                {
                    ret = Convert.ToDateTime(ret)
                   .ToShortDateString();
                }

                return ret;
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.GetPropertyValueAsString: " + e);
                return string.Empty;
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Returns a copy of the list. This is usefule when
        /// you are going to operate on a list and you don't want
        /// to modify items in the original reference.
        /// 
        /// https://stackoverflow.com/questions/222598/how-do-i-clone-a-generic-list-in-c
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="listToClone"></param>
        /// <returns></returns>
        public static List<T> Clone<T>(this List<T> listToClone)
        {
            #region implementation

            try
            {
                List<T> newList = new List<T>(listToClone.Count);

                listToClone.ForEach(item =>
                {
                    newList.Add(item);
                });

                return newList;
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.Clone: " + e);
            }

            //exception occurred return empty list
            return new List<T>();

            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Makes a copy of an IList
        /// 
        /// https://stackoverflow.com/questions/222598/how-do-i-clone-a-generic-list-in-c
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="listToClone"></param>
        /// <returns></returns>
        public static IList<T> Clone<T>(this IList<T> listToClone) where T : ICloneable
        {
            return listToClone.Select(item => (T)item.Clone()).ToList();
        }
    }
}