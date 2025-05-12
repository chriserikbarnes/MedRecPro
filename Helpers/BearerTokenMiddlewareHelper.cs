
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Security.Principal;

//using c = MedRecPro.Constant;
using p = MedRecPro.Helpers.PerformanceHelper;

namespace MedRecPro.Helpers
{
    /// <summary>
    /// Middleware for handling token caching and authentication in the request pipeline
    /// </summary>
    /// <remarks>
    /// This middleware intercepts requests to manage bearer token caching and Windows authentication integration
    /// </remarks>
    public class TokenCacheMiddleware
    {
        // Private constant for Bearer token prefix
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenCacheMiddleware> _logger;
        private readonly IHttpContextAccessor _httpContext;
        private const string BEARER_PREFIX = "Bearer ";

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the TokenCacheMiddleware
        /// </summary>
        /// <param name="next">The next middleware delegate in the pipeline</param>
        /// <param name="logger">Message logging</param>
        /// <param name="httpContext">The current HTTP context</param></param>
        public TokenCacheMiddleware(RequestDelegate next,
            ILogger<TokenCacheMiddleware> logger,
            IHttpContextAccessor httpContext)
        {
            _logger = logger;
            _next = next;
            _httpContext = httpContext;
        }

        /**************************************************************/
        public static string GetTokenFromCache(string? tokenKey)
        {
            #region implementation
            string token = string.Empty;
            try
            {
                if (string.IsNullOrEmpty(tokenKey))
                {
                    throw new Exception("Key is not defined");
                }

                ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.GetTokenFromCache key (info): {tokenKey}");

                token = (string)PerformanceHelper.GetCache(tokenKey) ?? string.Empty;

                ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.GetTokenFromCache token (info): {TextUtil.Truncate(token ?? "Empty", 30)}");

                return token ?? string.Empty;
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.GetTokenFromCache: {e.Message}");
            }

            return string.Empty; 
            #endregion
        }

        /**************************************************************/
        public static string? GetTokenCacheKey(string? userName, string? tokenType)
        {
            if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(tokenType))
                return @$"GetOrSetCacheToken{tokenType}{userName?.ToLower()}".GetSHA1HashString();
            else return null;
        }

        /**************************************************************/
        /// <summary>
        /// Retrieves or sets a token in the cache based on the provided token form
        /// </summary>
        /// <param name="tokenForm">Token form containing type and bearer token information</param>
        /// <returns>The cached token string or null if unsuccessful</returns>
        /// <remarks>
        /// Example usage:
        /// <code>
        /// var token = await TokenCacheMiddleware.GetOrSetToken(tokenForm);
        /// </code>
        /// </remarks>
        //public static string? GetOrSetToken(API.Controllers.UtilityController.TokenForm? tokenForm = null)
        //{
        //    #region implementation
        //    // Initialize local variables for token management
        //    string? tokenKey;
        //    string? userName;
        //    string? token;
        //    string? tokenType = null;
        //    string? ret = null;
        //    //string? name = null;

        //    try
        //    {
        //        #region disabled
        //        /**************************************************
        //        * 02/26/2025 - Commented out the following code
        //        * b/c the load balancer is not passing the header
        //        **************************************************/
        //        //try
        //        //{
        //        //    HttpContext? context = httpContext?.HttpContext ?? new HttpContextAccessor().HttpContext;

        //        //    #region Implementation
        //        //    // Check if the client passed a username in a custom header.
        //        //    if (context != null
        //        //        && context.Request != null
        //        //        && context.Request.Headers != null
        //        //        && context.Request.Headers.TryGetValue("X-Username", out var headerUsername))
        //        //    {
        //        //        name = headerUsername.FirstOrDefault();

        //        //        ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.GetOrSetToken X-Username (info): {name}");
        //        //    }
        //        //}
        //        //catch (Exception e)
        //        //{
        //        //    ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.GetOrSetToken faild to get username from header: {e.Message}");
        //        //} 
        //        #endregion

        //        // get username from passed form or from current user
        //        userName = tokenForm?.UserName?.ToLower()
        //            ?? Util.GetUserName()?.ToLower();

        //        if (string.IsNullOrEmpty(userName))
        //        {
        //            throw new Exception("Username is not defined");
        //        }

        //        ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.GetOrSetToken userName (info): {userName}");

        //        // Get and validate token type
        //        tokenType = Util.GetTokenType(c.TokenType.Graph);
        //        if (tokenType.IsNullOrEmpty())
        //        {
        //            throw new Exception("TokenType is not defined");
        //        }

        //        // Generate and validate token key
        //        tokenKey = GetTokenCacheKey(userName, tokenType);
        //        if (string.IsNullOrEmpty(tokenKey))
        //        {
        //            throw new Exception("TokenKey is not defined");
        //        }

        //        ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.GetOrSetToken tokenKey (info): {tokenKey}");

        //        // Check cache for existing token
        //        ret = p.GetCache<string>(tokenKey);
        //        if (!string.IsNullOrEmpty(ret))
        //        {
        //            return ret;
        //        }

        //        // Validate token form and bearer token
        //        if (tokenForm == null
        //            || string.IsNullOrEmpty(tokenType)
        //            || string.IsNullOrEmpty(tokenForm.BearerToken ?? Util.GetBearerToken(c.TokenType.Graph)))
        //        {
        //            throw new Exception("TokenForm is not defined");
        //        }

        //        // Get and cache new token
        //        token = tokenForm?.BearerToken ?? Util.GetBearerToken(c.TokenType.Graph);
        //        p.SetCache(tokenKey, token, 0.9);
        //        ret = p.GetCache<string>(tokenKey);
             
        //    }
        //    catch (Exception ex)
        //    {
        //        ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.GetOrSetToken: {ex.Message}");
        //    }

        //    return ret;
        //    #endregion
        //}

        /**************************************************************/
        /// <summary>
        /// Removes a cached token for the specified user or the current user if no username is provided.
        /// </summary>
        /// <param name="userName">Optional username. If null, the current user's name will be used.</param>
        /// <returns>True if the token was successfully removed or was not found, false if an error occurred.</returns>
        /// <example>
        /// var success = RemoveCachedToken("john.doe");
        /// // Or use current user
        /// var success = RemoveCachedToken(null);
        /// </example>
        /// <remarks>
        /// This method handles token removal from the cache system. It will validate cache for the appropriate
        /// token key based on the username and token type, then attempt to remove it from the cache.
        /// All errors are logged via ErrorHelper.
        /// </remarks>
        //public static bool RemoveCachedToken(string? userName)
        //{
        //    #region implementation
        //    // Initialize local variables for token management
        //    string? tokenKey;
        //    string? token;
        //    string? tokenType = null;
        //    bool ret = false;
        //    //string? name = null;  // Commented out but retained as requested
        //    try
        //    {
        //        // Get username from passed value or from current user
        //        userName = userName?.ToLower()
        //            ?? Util.GetUserName()?.ToLower();
        //        if (string.IsNullOrEmpty(userName))
        //        {
        //            // Cannot proceed without a valid username
        //            throw new Exception("Username is not defined");
        //        }

        //        // Get and validate token type
        //        tokenType = Util.GetTokenType(c.TokenType.Graph);
        //        if (tokenType.IsNullOrEmpty())
        //        {
        //            // TokenType is required for generating the token key
        //            throw new Exception("TokenType is not defined");
        //        }

        //        // Generate and validate token key
        //        tokenKey = GetTokenCacheKey(userName, tokenType);
        //        if (string.IsNullOrEmpty(tokenKey))
        //        {
        //            // Cannot proceed without a valid token key
        //            throw new Exception("TokenKey is not defined");
        //        }

        //        // Log token removal attempt information
        //        ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.RemoveCachedToken userName (info): {userName}");
        //        ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.RemoveCachedToken tokenKey (info): {tokenKey}");

        //        // Check cache for existing token
        //        token = p.GetCache<string>(tokenKey);
        //        if (!string.IsNullOrEmpty(token))
        //        {
        //            // Remove token from cache if it exists
        //            p.RemoveCache(tokenKey);
        //            ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.RemoveCachedToken token succesfully removed (info): {tokenKey}");
        //        }
        //        else
        //        {
        //            // Log that no token was found to remove
        //            ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.RemoveCachedToken token not found removal not needed (info): {tokenKey}");
        //        }

        //        // Return true if token was removed or not found
        //        ret = true;
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log any exceptions that occur during token removal
        //        ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.RemoveCachedToken: {ex.Message}");
        //    }

        //    return ret;
        //    #endregion
        //}


        /**************************************************************/
        /// <summary>
        /// Processes the HTTP request through the middleware pipeline
        /// </summary>
        /// <param name="context">The HTTP context for the current request</param>
        /// <returns>A task representing the completion of middleware processing</returns>
        /// <remarks>
        /// Handles authentication by managing both Windows and Bearer token authentication
        /// Skips processing for Swagger endpoints
        /// </remarks>
        //public async Task InvokeAsync(HttpContext context)
        //{
        //    try
        //    {
        //        #region Implementation
        //        if (!context.Request.Path.StartsWithSegments("/swagger"))
        //        {
        //            // Store the original Windows Principal
        //            var originalPrincipal = context.User;
        //            var originalIdentity = originalPrincipal?.Identity;

        //            // Get original authorization header
        //            string? originalAuth = null;
        //            if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        //            {
        //                originalAuth = authHeader.ToString();
        //            }

        //            // Process bearer token if present
        //            if (!string.IsNullOrEmpty(originalAuth) &&
        //                originalAuth.StartsWith(BEARER_PREFIX, StringComparison.OrdinalIgnoreCase))
        //            {
        //                var tokenForm = new API.Controllers.UtilityController.TokenForm
        //                {
        //                    BearerToken =  Util.GetBearerToken(c.TokenType.Graph)
        //                };

        //                string? tokenCache = GetOrSetToken(tokenForm);

        //                _logger.LogInformation($"TokenCacheMiddleware.InvokeAsync cached token: {(!string.IsNullOrEmpty(tokenCache)).ToString()}");

        //                if (!string.IsNullOrEmpty(tokenCache))
        //                {
        //                    // Create a new ClaimsIdentity for the bearer token
        //                    var bearerIdentity = new ClaimsIdentity("Bearer");

        //                    // Create a composite principal that includes both Windows and Bearer identities
        //                    var identities = new List<ClaimsIdentity>();

        //                    // Add the original Windows identity if it exists
        //                    if (originalIdentity is WindowsIdentity windowsIdentity)
        //                    {
        //                        identities.Add(windowsIdentity);
        //                    }

        //                    // Add the bearer token identity
        //                    identities.Add(bearerIdentity);

        //                    // Create a new principal with both identities
        //                    context.User = new ClaimsPrincipal(identities);

        //                    // Update the Authorization header with the cached token
        //                    context.Request.Headers["Authorization"] = $"{BEARER_PREFIX}{tokenCache}";
        //                }
        //            }
        //        }
        //        #endregion
        //    }
        //    catch (Exception e)
        //    {
        //        ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.InvokeAsync: {e.Message}");
        //    }

        //    await _next(context);
        //}

        #region replaced by above
        //    public async Task InvokeAsync(HttpContext context)
        //    {
        //        try
        //        {
        //            if (!context.Request.Path.StartsWithSegments("/swagger"))
        //            {
        //                string? originalAuth = null;
        //                if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        //                {
        //                    originalAuth = authHeader.ToString();
        //                }

        //                // Only proceed if we have a Bearer token
        //                if (!string.IsNullOrEmpty(originalAuth) &&
        //                    originalAuth.StartsWith(BEARER_PREFIX, StringComparison.OrdinalIgnoreCase))
        //                {
        //                    var tokenForm = new API.Controllers.UtilityController.TokenForm
        //                    {
        //                        Type = c.TokenType.Graph,
        //                        BearerToken = await Util.GetBearerTokenAsync(c.TokenType.Graph)
        //                    };

        //                    string? tokenCache = await GetOrSetToken(tokenForm);

        //                    if (!string.IsNullOrEmpty(tokenCache))
        //                    {
        //                        // Replace only the token part while preserving the Bearer prefix
        //                        context.Request.Headers["Authorization"] = $"{BEARER_PREFIX}{tokenCache}";
        //                    }
        //                }
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            ErrorHelper.AddErrorMsg($"TokenCacheMiddleware.InvokeAsync: {e.Message}");
        //        }

        //        await _next(context);
        //    }
        //} 
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Extension methods for configuring TokenCacheMiddleware in the application pipeline
    /// </summary>
    public static class TokenCacheMiddlewareExtensions
    {
        /**************************************************************/
        /// <summary>
        /// Adds TokenCacheMiddleware to the application's request pipeline
        /// </summary>
        /// <param name="builder">The application builder</param>
        /// <returns>The application builder with token cache middleware configured</returns>
        /// <remarks>
        /// Example usage:
        /// <code>
        /// app.UseTokenCache();
        /// </code>
        /// </remarks>
        public static IApplicationBuilder UseTokenCache(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TokenCacheMiddleware>();
        }
    }
}