
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Security.Principal;

//using c = MedRecPro.Constant;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using p = MedRecPro.Helpers.PerformanceHelper;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.

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
        /// <param name="httpContext">The current HTTP context</param>
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