using MedRecPro.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Security
{
    /**************************************************************/
    /// <summary>
    /// Tests public token-cache middleware helper methods and registration.
    /// </summary>
    /// <seealso cref="TokenCacheMiddleware"/>
    /// <seealso cref="TokenCacheMiddlewareExtensions"/>
    [TestClass]
    [TestCategory("Unit")]
    public class TokenCacheMiddlewareHelperTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies token cache keys are stable and null-safe.
        /// </summary>
        /// <seealso cref="TokenCacheMiddleware.GetTokenCacheKey"/>
        [TestMethod]
        public void GetTokenCacheKey_UserAndType_ReturnsStableHash()
        {
            #region implementation
            var lower = TokenCacheMiddleware.GetTokenCacheKey("user@example.test", "Graph");
            var upper = TokenCacheMiddleware.GetTokenCacheKey("USER@EXAMPLE.TEST", "Graph");

            Assert.IsNotNull(lower);
            Assert.AreEqual(40, lower.Length);
            Assert.AreEqual(lower, upper);
            Assert.IsNull(TokenCacheMiddleware.GetTokenCacheKey(null, "Graph"));
            Assert.IsNull(TokenCacheMiddleware.GetTokenCacheKey("user@example.test", null));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies token cache lookup handles missing keys and returns stored token values.
        /// </summary>
        /// <seealso cref="TokenCacheMiddleware.GetTokenFromCache"/>
        /// <seealso cref="PerformanceHelper.SetCache"/>
        [TestMethod]
        public void GetTokenFromCache_MissingAndStoredTokens_ReturnsExpectedValues()
        {
            #region implementation
            var key = $"TokenCacheMiddlewareHelperTests:{Guid.NewGuid():N}";

            try
            {
                Assert.AreEqual(string.Empty, TokenCacheMiddleware.GetTokenFromCache(null));
                Assert.AreEqual(string.Empty, TokenCacheMiddleware.GetTokenFromCache(key));

                PerformanceHelper.SetCache(key, "cached-token", 1.0);

                Assert.AreEqual("cached-token", TokenCacheMiddleware.GetTokenFromCache(key));
            }
            finally
            {
                PerformanceHelper.RemoveCache(key);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the middleware instance passes through to the next delegate.
        /// </summary>
        /// <seealso cref="TokenCacheMiddleware.InvokeAsync"/>
        [TestMethod]
        public async Task InvokeAsync_RequestContext_CallsNextDelegate()
        {
            #region implementation
            var called = false;
            var accessor = new HttpContextAccessor();
            var context = new DefaultHttpContext();
            HttpContext? observedContext = null;
            var sut = new TokenCacheMiddleware(
                nextContext =>
                {
                    called = true;
                    observedContext = nextContext;
                    return Task.CompletedTask;
                },
                NullLogger<TokenCacheMiddleware>.Instance,
                accessor);

            await sut.InvokeAsync(context);

            Assert.IsTrue(called);
            Assert.AreSame(context, observedContext);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the middleware extension can be added to a minimal application builder.
        /// </summary>
        /// <seealso cref="TokenCacheMiddlewareExtensions.UseTokenCache"/>
        [TestMethod]
        public void UseTokenCache_MinimalBuilder_ReturnsBuilder()
        {
            #region implementation
            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
                .BuildServiceProvider();
            var builder = new ApplicationBuilder(services);

            var result = builder.UseTokenCache();

            Assert.AreSame(builder, result);
            #endregion
        }

        #endregion
    }
}
