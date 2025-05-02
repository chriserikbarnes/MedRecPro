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

        // Initialize this once in your application startup:
        // Util.Initialize(httpContextAccessor);
        public static void Initialize(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

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
        public static string GetHashString<T>(this T obj, bool recursion = false)
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
                            ret = GetHashString(obj.ToCommaString(), true);
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
        /// Takes an IEnumerable and returns a string representation
        /// of the SHA1 Hash
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        /// <seealso cref="TextUtil.ListToCommaString{T}(IEnumerable{T})"/>
        public static string GetListHashString<T>(this IEnumerable<T> obj)
        {
            #region implementation
            string ret = null;
            string txt = null;
            HashAlgorithm alg = SHA1.Create();
            StringBuilder sb = new StringBuilder();
            ConcurrentBag<string> objVals = new ConcurrentBag<string>();

            try
            {
                //convert list to comma delimited string
                txt = obj.ListToCommaString();

                //get the hash code
                if (!string.IsNullOrEmpty(txt))
                {
                    byte[] hash = alg?.ComputeHash(Encoding.UTF8?.GetBytes(txt));

                    foreach (byte b in hash)
                    {
                        sb?.Append(b.ToString("X2"));
                    }

                    ret = sb?.ToString();
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("Util.GetListHashString: " + e);
            }

            return ret;
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
            string c = GetHashString(a);
            string d = GetHashString(b);

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