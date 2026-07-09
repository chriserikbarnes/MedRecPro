using MedRecPro.Configuration;
using MedRecPro.Service.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Claims;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests the injectable current-user accessor registered by platform services.
    /// </summary>
    /// <seealso cref="IUserContextAccessor"/>
    [TestClass]
    public class UserContextAccessorTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies explicit and ambient HTTP contexts resolve numeric user claims.
        /// </summary>
        /// <seealso cref="IUserContextAccessor.GetCurrentUserId(HttpContext?)"/>
        /// <seealso cref="MedRecProApplicationServiceExtensions.AddMedRecProPlatformServices(IServiceCollection, IConfiguration)"/>
        [TestMethod]
        public void GetCurrentUserId_ExplicitAndAmbientContexts_ReturnsClaimUserIds()
        {
            #region implementation
            using var provider = createPlatformProvider();
            var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
            var userContextAccessor = provider.GetRequiredService<IUserContextAccessor>();

            var explicitContext = createHttpContext(42);
            var ambientContext = createHttpContext(84);
            httpContextAccessor.HttpContext = ambientContext;

            Assert.AreEqual(42L, userContextAccessor.GetCurrentUserId(explicitContext));
            Assert.AreEqual(84L, userContextAccessor.GetCurrentUserId());
            Assert.IsNull(userContextAccessor.GetCurrentUserId(new DefaultHttpContext()));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a platform service provider with isolated test configuration.
        /// </summary>
        /// <returns>A service provider containing platform service registrations.</returns>
        /// <seealso cref="MedRecProApplicationServiceExtensions.AddMedRecProPlatformServices(IServiceCollection, IConfiguration)"/>
        private static ServiceProvider createPlatformProvider()
        {
            #region implementation
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TarpitSettings:Enabled"] = "false"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMedRecProPlatformServices(configuration);

            return services.BuildServiceProvider();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an authenticated HTTP context with a numeric name-identifier claim.
        /// </summary>
        /// <param name="userId">Numeric user identifier to place in claims.</param>
        /// <returns>HTTP context containing the authenticated principal.</returns>
        /// <seealso cref="ClaimTypes.NameIdentifier"/>
        private static HttpContext createHttpContext(long userId)
        {
            #region implementation
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    authenticationType: "TestAuth"))
            };

            return context;
            #endregion
        }

        #endregion
    }
}
