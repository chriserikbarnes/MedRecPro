using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Exercises the Azure token surface without any network access:
    /// <see cref="AzureManagementTokenProvider"/> (MSAL client-credential
    /// provider), <see cref="AppOnlyTokenCredential"/> (TokenCredential
    /// adapter), and <see cref="AzureAppTokenProvider"/>
    /// (DefaultAzureCredential host provider).
    /// </summary>
    /// <remarks>
    /// Token acquisition is made deterministic through two seams: the
    /// production <c>virtual</c> methods on
    /// <see cref="AzureManagementTokenProvider"/> (overridden by
    /// <see cref="FakeManagementTokenProvider"/>) and a reflection swap of
    /// <see cref="AzureAppTokenProvider"/>'s private credential field with
    /// <see cref="FakeTokenCredential"/> — the same private-field pattern
    /// already used by ThrottleStateServiceTests. MSTest runs sequentially in
    /// this assembly, so environment-variable mutation is safe with a
    /// finally-restore.
    /// </remarks>
    /// <seealso cref="AzureManagementTokenProvider"/>
    /// <seealso cref="AppOnlyTokenCredential"/>
    /// <seealso cref="AzureAppTokenProvider"/>
    [TestClass]
    public class AzureTokenCredentialServiceTests
    {
        #region implementation

        /// <summary>
        /// Fixed expiration used by the fakes so assertions are exact.
        /// </summary>
        private static readonly DateTimeOffset FixedExpiration = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        #region AzureManagementTokenProvider

        /**************************************************************/
        /// <summary>
        /// Verifies the provider constructor rejects a null configuration.
        /// </summary>
        /// <seealso cref="AzureManagementTokenProvider"/>
        [TestMethod]
        public void AzureManagementTokenProvider_Constructor_NullConfiguration_Throws()
        {
            #region implementation
            // Act + Assert
            Assert.ThrowsException<ArgumentNullException>(() => new AzureManagementTokenProvider(null!));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies each required configuration key produces a targeted
        /// InvalidOperationException, including the environment-specific
        /// client-secret messages.
        /// </summary>
        /// <seealso cref="AzureManagementTokenProvider"/>
        [TestMethod]
        public void AzureManagementTokenProvider_Constructor_MissingConfiguration_ThrowsPerKey()
        {
            #region implementation
            // Arrange + Act + Assert - missing tenant.
            var noTenant = Assert.ThrowsException<InvalidOperationException>(() =>
                new AzureManagementTokenProvider(buildConfig(new Dictionary<string, string?>())));
            StringAssert.Contains(noTenant.Message, "TenantId");

            // Missing client ID.
            var noClient = Assert.ThrowsException<InvalidOperationException>(() =>
                new AzureManagementTokenProvider(buildConfig(new Dictionary<string, string?>
                {
                    ["Authentication:Microsoft:TenantId"] = "tenant-id"
                })));
            StringAssert.Contains(noClient.Message, "ClientId");

            // Missing Dev secret while the configured environment is Development.
            var noDevSecret = Assert.ThrowsException<InvalidOperationException>(() =>
                new AzureManagementTokenProvider(buildConfig(new Dictionary<string, string?>
                {
                    ["Authentication:Microsoft:TenantId"] = "tenant-id",
                    ["Authentication:Microsoft:ClientId"] = "client-id",
                    ["ASPNETCORE_ENVIRONMENT"] = "Development"
                })));
            StringAssert.Contains(noDevSecret.Message, "ClientSecret:Development");

            // Missing Prod secret in the default (Production) environment.
            var noProdSecret = Assert.ThrowsException<InvalidOperationException>(() =>
                new AzureManagementTokenProvider(buildConfig(new Dictionary<string, string?>
                {
                    ["Authentication:Microsoft:TenantId"] = "tenant-id",
                    ["Authentication:Microsoft:ClientId"] = "client-id"
                })));
            StringAssert.Contains(noProdSecret.Message, "ClientSecret:Production");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies both environment branches construct the MSAL client
        /// offline when their secret key is present.
        /// </summary>
        /// <seealso cref="AzureManagementTokenProvider"/>
        [TestMethod]
        public void AzureManagementTokenProvider_Constructor_ValidConfiguration_Constructs()
        {
            #region implementation
            // Act - Production branch (default environment).
            var production = new AzureManagementTokenProvider(buildConfig(new Dictionary<string, string?>
            {
                ["Authentication:Microsoft:TenantId"] = "tenant-id",
                ["Authentication:Microsoft:ClientId"] = "client-id",
                ["Authentication:Microsoft:ClientSecret:Prod"] = "prod-secret"
            }));

            // Act - Development branch.
            var development = new AzureManagementTokenProvider(buildConfig(new Dictionary<string, string?>
            {
                ["Authentication:Microsoft:TenantId"] = "tenant-id",
                ["Authentication:Microsoft:ClientId"] = "client-id",
                ["Authentication:Microsoft:ClientSecret:Dev"] = "dev-secret",
                ["ASPNETCORE_ENVIRONMENT"] = "Development"
            }));

            // Assert
            Assert.IsNotNull(production);
            Assert.IsNotNull(development);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetTokenExpiration reports roughly one hour ahead of the
        /// call time.
        /// </summary>
        /// <seealso cref="AzureManagementTokenProvider.GetTokenExpiration"/>
        [TestMethod]
        public void AzureManagementTokenProvider_GetTokenExpiration_ReturnsFutureExpiration()
        {
            #region implementation
            // Arrange
            var provider = createRealManagementProvider();
            var before = DateTimeOffset.UtcNow;

            // Act
            var expiration = provider.GetTokenExpiration();

            // Assert - within the [before+1h, after+1h] window.
            var after = DateTimeOffset.UtcNow;
            Assert.IsNotNull(expiration);
            Assert.IsTrue(expiration >= before.AddHours(1) && expiration <= after.AddHours(1),
                $"Expected ~1 hour ahead; got {expiration:O}.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the virtual token-acquisition seam: an overriding fake
        /// supplies a deterministic token through the public method signature.
        /// </summary>
        /// <remarks>
        /// The base implementation's only body is an MSAL network call, so the
        /// production seam (virtual methods) is the sanctioned no-network path
        /// for exercising this method's contract.
        /// </remarks>
        /// <seealso cref="AzureManagementTokenProvider.GetAccessTokenAsync"/>
        [TestMethod]
        public async Task AzureManagementTokenProvider_GetAccessTokenAsync_SeamOverride_ReturnsFixtureToken()
        {
            #region implementation
            // Arrange
            AzureManagementTokenProvider provider = new FakeManagementTokenProvider();

            // Act - dispatches through the virtual seam.
            var token = await provider.GetAccessTokenAsync();

            // Assert
            Assert.AreEqual("fixture-token", token);
            #endregion
        }

        #endregion

        #region AppOnlyTokenCredential

        /**************************************************************/
        /// <summary>
        /// Verifies the credential constructor rejects a null provider.
        /// </summary>
        /// <seealso cref="AppOnlyTokenCredential"/>
        [TestMethod]
        public void AppOnlyTokenCredential_Constructor_NullProvider_Throws()
        {
            #region implementation
            // Act + Assert
            Assert.ThrowsException<ArgumentNullException>(() => new AppOnlyTokenCredential(null!));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetTokenAsync returns the provider's token and expiration
        /// as an AccessToken.
        /// </summary>
        /// <seealso cref="AppOnlyTokenCredential.GetTokenAsync"/>
        [TestMethod]
        public async Task AppOnlyTokenCredential_GetTokenAsync_WithFakeProvider_ReturnsAccessToken()
        {
            #region implementation
            // Arrange
            var credential = new AppOnlyTokenCredential(new FakeManagementTokenProvider());

            // Act
            var token = await credential.GetTokenAsync(new TokenRequestContext(), CancellationToken.None);

            // Assert
            Assert.AreEqual("fixture-token", token.Token);
            Assert.AreEqual(FixedExpiration, token.ExpiresOn);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the null-expiration fallback substitutes roughly one hour
        /// from now.
        /// </summary>
        /// <seealso cref="AppOnlyTokenCredential.GetTokenAsync"/>
        [TestMethod]
        public async Task AppOnlyTokenCredential_GetTokenAsync_NullExpiration_FallsBackToOneHour()
        {
            #region implementation
            // Arrange - fake reports no expiration.
            var credential = new AppOnlyTokenCredential(new FakeManagementTokenProvider { Expiration = null });
            var before = DateTimeOffset.UtcNow;

            // Act
            var token = await credential.GetTokenAsync(new TokenRequestContext(), CancellationToken.None);

            // Assert
            var after = DateTimeOffset.UtcNow;
            Assert.IsTrue(token.ExpiresOn >= before.AddHours(1) && token.ExpiresOn <= after.AddHours(1));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies MSAL failures are wrapped in AuthenticationFailedException
        /// with the acquisition message and the original inner exception.
        /// </summary>
        /// <seealso cref="AppOnlyTokenCredential.GetTokenAsync"/>
        [TestMethod]
        public async Task AppOnlyTokenCredential_GetTokenAsync_MsalException_WrapsAsAuthenticationFailed()
        {
            #region implementation
            // Arrange
            var msalFailure = new MsalClientException("fixture_code", "msal failure");
            var credential = new AppOnlyTokenCredential(new FakeManagementTokenProvider { ToThrow = msalFailure });

            // Act + Assert
            var exception = await Assert.ThrowsExceptionAsync<AuthenticationFailedException>(
                async () => await credential.GetTokenAsync(new TokenRequestContext(), CancellationToken.None));

            StringAssert.Contains(exception.Message, "Failed to acquire");
            Assert.AreSame(msalFailure, exception.InnerException);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies non-MSAL failures are wrapped with the unexpected-error
        /// message and preserve the inner exception.
        /// </summary>
        /// <seealso cref="AppOnlyTokenCredential.GetTokenAsync"/>
        [TestMethod]
        public async Task AppOnlyTokenCredential_GetTokenAsync_UnexpectedException_WrapsAsAuthenticationFailed()
        {
            #region implementation
            // Arrange
            var failure = new InvalidOperationException("unexpected failure");
            var credential = new AppOnlyTokenCredential(new FakeManagementTokenProvider { ToThrow = failure });

            // Act + Assert
            var exception = await Assert.ThrowsExceptionAsync<AuthenticationFailedException>(
                async () => await credential.GetTokenAsync(new TokenRequestContext(), CancellationToken.None));

            StringAssert.Contains(exception.Message, "Unexpected error");
            Assert.AreSame(failure, exception.InnerException);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the synchronous GetToken overload delegates to the async
        /// path and returns identical values.
        /// </summary>
        /// <seealso cref="AppOnlyTokenCredential.GetToken"/>
        [TestMethod]
        public void AppOnlyTokenCredential_GetToken_WrapsAsyncToken()
        {
            #region implementation
            // Arrange
            var credential = new AppOnlyTokenCredential(new FakeManagementTokenProvider());

            // Act - safe from deadlock because the fake completes synchronously.
            var token = credential.GetToken(new TokenRequestContext(), CancellationToken.None);

            // Assert
            Assert.AreEqual("fixture-token", token.Token);
            Assert.AreEqual(FixedExpiration, token.ExpiresOn);
            #endregion
        }

        #endregion

        #region AzureAppTokenProvider

        /**************************************************************/
        /// <summary>
        /// Verifies environment detection reports Local Development when the
        /// WEBSITE_SITE_NAME variable is absent.
        /// </summary>
        /// <seealso cref="AzureAppTokenProvider.GetEnvironment"/>
        [TestMethod]
        public void AzureAppTokenProvider_GetEnvironment_ReturnsLocalDevelopmentWithoutWebsiteSiteName()
        {
            #region implementation
            // Arrange + Act
            var environment = withWebsiteSiteName(null,
                () => createAppTokenProvider().GetEnvironment());

            // Assert
            Assert.AreEqual("Local Development", environment);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies environment detection reports Azure App Service when the
        /// WEBSITE_SITE_NAME variable is present.
        /// </summary>
        /// <seealso cref="AzureAppTokenProvider.GetEnvironment"/>
        [TestMethod]
        public void AzureAppTokenProvider_GetEnvironment_ReturnsAzureAppServiceWithWebsiteSiteName()
        {
            #region implementation
            // Arrange + Act
            var environment = withWebsiteSiteName("fixture-site",
                () => createAppTokenProvider().GetEnvironment());

            // Assert
            Assert.AreEqual("Azure App Service", environment);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetCredential exposes the provider's TokenCredential
        /// instance (the swapped fake after the reflection seam is applied).
        /// </summary>
        /// <seealso cref="AzureAppTokenProvider.GetCredential"/>
        [TestMethod]
        public void AzureAppTokenProvider_GetCredential_ReturnsTokenCredentialInstance()
        {
            #region implementation
            // Arrange
            var provider = createAppTokenProvider();

            // Assert - the default construction yields a real TokenCredential.
            Assert.IsInstanceOfType(provider.GetCredential(), typeof(TokenCredential));

            // Arrange - after the swap the fake identity is exposed.
            var fake = new FakeTokenCredential();
            swapCredential(provider, fake);

            // Act + Assert
            Assert.AreSame(fake, provider.GetCredential());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetTokenExpiration reports roughly one hour ahead.
        /// </summary>
        /// <seealso cref="AzureAppTokenProvider.GetTokenExpiration"/>
        [TestMethod]
        public void AzureAppTokenProvider_GetTokenExpiration_ReturnsFutureExpiration()
        {
            #region implementation
            // Arrange
            var provider = createAppTokenProvider();
            var before = DateTimeOffset.UtcNow;

            // Act
            var expiration = provider.GetTokenExpiration();

            // Assert
            var after = DateTimeOffset.UtcNow;
            Assert.IsNotNull(expiration);
            Assert.IsTrue(expiration >= before.AddHours(1) && expiration <= after.AddHours(1));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetAccessTokenAsync returns the credential's token and
        /// requests the Azure management scope.
        /// </summary>
        /// <seealso cref="AzureAppTokenProvider.GetAccessTokenAsync"/>
        [TestMethod]
        public async Task AzureAppTokenProvider_GetAccessTokenAsync_WithFakeCredential_ReturnsTokenString()
        {
            #region implementation
            // Arrange
            var provider = createAppTokenProvider();
            var fake = new FakeTokenCredential();
            swapCredential(provider, fake);

            // Act
            var token = await provider.GetAccessTokenAsync();

            // Assert - token flows through and the management scope was used.
            Assert.AreEqual("fixture-token", token);
            Assert.IsNotNull(fake.LastContext);
            CollectionAssert.Contains(fake.LastContext!.Value.Scopes, "https://management.azure.com/.default");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies an unavailable credential is wrapped in a new
        /// AuthenticationFailedException naming the detected environment.
        /// </summary>
        /// <seealso cref="AzureAppTokenProvider.GetAccessTokenAsync"/>
        [TestMethod]
        public async Task AzureAppTokenProvider_GetAccessTokenAsync_CredentialUnavailable_WrapsWithEnvironment()
        {
            #region implementation
            // Arrange
            var provider = withWebsiteSiteName(null, () => createAppTokenProvider());
            var unavailable = new CredentialUnavailableException("no credential source");
            swapCredential(provider, new FakeTokenCredential { ToThrow = unavailable });

            // Act + Assert
            var exception = await Assert.ThrowsExceptionAsync<AuthenticationFailedException>(
                () => provider.GetAccessTokenAsync());

            StringAssert.Contains(exception.Message, "Local Development");
            Assert.AreSame(unavailable, exception.InnerException);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a plain AuthenticationFailedException is rethrown as the
        /// same instance (not re-wrapped).
        /// </summary>
        /// <seealso cref="AzureAppTokenProvider.GetAccessTokenAsync"/>
        [TestMethod]
        public async Task AzureAppTokenProvider_GetAccessTokenAsync_AuthenticationFailed_RethrowsSameInstance()
        {
            #region implementation
            // Arrange
            var provider = createAppTokenProvider();
            var failure = new AuthenticationFailedException("direct auth failure");
            swapCredential(provider, new FakeTokenCredential { ToThrow = failure });

            // Act + Assert
            var exception = await Assert.ThrowsExceptionAsync<AuthenticationFailedException>(
                () => provider.GetAccessTokenAsync());

            Assert.AreSame(failure, exception);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the metadata overload returns the full AccessToken and
        /// lets failures propagate unwrapped.
        /// </summary>
        /// <seealso cref="AzureAppTokenProvider.GetAccessTokenWithMetadataAsync"/>
        [TestMethod]
        public async Task AzureAppTokenProvider_GetAccessTokenWithMetadataAsync_WithFakeCredential_ReturnsFixedToken()
        {
            #region implementation
            // Arrange
            var provider = createAppTokenProvider();
            swapCredential(provider, new FakeTokenCredential());

            // Act
            var token = await provider.GetAccessTokenWithMetadataAsync();

            // Assert
            Assert.AreEqual("fixture-token", token.Token);
            Assert.AreEqual(FixedExpiration, token.ExpiresOn);

            // Arrange - failures propagate without wrapping.
            var failing = createAppTokenProvider();
            var failure = new InvalidOperationException("raw failure");
            swapCredential(failing, new FakeTokenCredential { ToThrow = failure });

            // Act + Assert
            var thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => failing.GetAccessTokenWithMetadataAsync());
            Assert.AreSame(failure, thrown);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies TestCredentialAsync reports success and failure tuples
        /// with the detected environment.
        /// </summary>
        /// <seealso cref="AzureAppTokenProvider.TestCredentialAsync"/>
        [TestMethod]
        public async Task AzureAppTokenProvider_TestCredentialAsync_ReportsSuccessAndFailure()
        {
            #region implementation
            // Arrange - success path.
            var healthy = withWebsiteSiteName(null, () => createAppTokenProvider());
            swapCredential(healthy, new FakeTokenCredential());

            // Act
            var success = await healthy.TestCredentialAsync();

            // Assert
            Assert.IsTrue(success.Success);
            Assert.AreEqual("Local Development", success.Environment);
            Assert.IsNull(success.ErrorMessage);

            // Arrange - failure path.
            var broken = withWebsiteSiteName(null, () => createAppTokenProvider());
            swapCredential(broken, new FakeTokenCredential { ToThrow = new InvalidOperationException("credential broke") });

            // Act
            var failure = await broken.TestCredentialAsync();

            // Assert
            Assert.IsFalse(failure.Success);
            Assert.AreEqual("Local Development", failure.Environment);
            Assert.AreEqual("credential broke", failure.ErrorMessage);
            #endregion
        }

        #endregion

        #region Harness helpers and fakes

        /**************************************************************/
        /// <summary>
        /// Builds an in-memory configuration from the supplied values.
        /// </summary>
        /// <param name="values">Configuration key/value pairs.</param>
        /// <returns>The built configuration.</returns>
        private static IConfiguration buildConfig(Dictionary<string, string?> values)
        {
            #region implementation
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a real (non-fake) management token provider with valid
        /// offline configuration.
        /// </summary>
        /// <returns>A constructed provider; no network occurs at construction.</returns>
        /// <seealso cref="AzureManagementTokenProvider"/>
        private static AzureManagementTokenProvider createRealManagementProvider()
        {
            #region implementation
            return new AzureManagementTokenProvider(buildConfig(new Dictionary<string, string?>
            {
                ["Authentication:Microsoft:TenantId"] = "tenant-id",
                ["Authentication:Microsoft:ClientId"] = "client-id",
                ["Authentication:Microsoft:ClientSecret:Prod"] = "prod-secret"
            }));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an <see cref="AzureAppTokenProvider"/> with minimal
        /// configuration; DefaultAzureCredential probes lazily so this is
        /// network-free.
        /// </summary>
        /// <returns>A constructed provider.</returns>
        private static AzureAppTokenProvider createAppTokenProvider()
        {
            #region implementation
            return new AzureAppTokenProvider(
                buildConfig(new Dictionary<string, string?>()),
                NullLogger<AzureAppTokenProvider>.Instance);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Swaps the provider's private credential field with a test double
        /// (reflection seam matching the ThrottleStateServiceTests precedent).
        /// </summary>
        /// <param name="provider">Provider under test.</param>
        /// <param name="credential">Replacement credential.</param>
        /// <seealso cref="AzureAppTokenProvider.GetCredential"/>
        private static void swapCredential(AzureAppTokenProvider provider, TokenCredential credential)
        {
            #region implementation
            var field = typeof(AzureAppTokenProvider).GetField(
                "_credential", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, "AzureAppTokenProvider private field '_credential' was not found; the seam has moved.");
            field!.SetValue(provider, credential);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Runs a factory with the WEBSITE_SITE_NAME environment variable set
        /// to the supplied value, restoring the original value afterwards.
        /// </summary>
        /// <typeparam name="TResult">Factory result type.</typeparam>
        /// <param name="value">Variable value (null clears it).</param>
        /// <param name="factory">Work to run inside the scope.</param>
        /// <returns>The factory result.</returns>
        private static TResult withWebsiteSiteName<TResult>(string? value, Func<TResult> factory)
        {
            #region implementation
            var original = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");

            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", value);
                return factory();
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", original);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Deterministic override of <see cref="AzureManagementTokenProvider"/>
        /// exercising the production virtual seam: returns a fixture token or
        /// throws a configured exception, with a controllable expiration.
        /// </summary>
        /// <seealso cref="AzureManagementTokenProvider.GetAccessTokenAsync"/>
        /// <seealso cref="AzureManagementTokenProvider.GetTokenExpiration"/>
        private sealed class FakeManagementTokenProvider : AzureManagementTokenProvider
        {
            #region implementation

            /**************************************************************/
            /// <summary>Exception to throw from token acquisition, if any.</summary>
            public Exception? ToThrow { get; set; }

            /**************************************************************/
            /// <summary>Expiration to report; null exercises the fallback.</summary>
            public DateTimeOffset? Expiration { get; set; } = FixedExpiration;

            /**************************************************************/
            /// <summary>
            /// Constructs the base provider with valid offline configuration.
            /// </summary>
            public FakeManagementTokenProvider() : base(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Microsoft:TenantId"] = "tenant-id",
                    ["Authentication:Microsoft:ClientId"] = "client-id",
                    ["Authentication:Microsoft:ClientSecret:Prod"] = "prod-secret"
                }).Build())
            {
            }

            /**************************************************************/
            /// <summary>
            /// Returns the fixture token or throws the configured exception.
            /// </summary>
            /// <param name="cancellationToken">Ignored.</param>
            /// <returns>The fixture token.</returns>
            public override Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            {
                #region implementation
                return ToThrow is null
                    ? Task.FromResult("fixture-token")
                    : Task.FromException<string>(ToThrow);
                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Returns the configured expiration.
            /// </summary>
            /// <returns>The configured expiration or null.</returns>
            public override DateTimeOffset? GetTokenExpiration()
            {
                #region implementation
                return Expiration;
                #endregion
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Deterministic <see cref="TokenCredential"/> returning a fixed
        /// AccessToken (or throwing) and capturing the last request context
        /// for scope assertions.
        /// </summary>
        /// <seealso cref="AzureAppTokenProvider"/>
        private sealed class FakeTokenCredential : TokenCredential
        {
            #region implementation

            /**************************************************************/
            /// <summary>Exception to throw from token acquisition, if any.</summary>
            public Exception? ToThrow { get; set; }

            /**************************************************************/
            /// <summary>Last request context observed, for scope assertions.</summary>
            public TokenRequestContext? LastContext { get; private set; }

            /**************************************************************/
            /// <summary>
            /// Synchronous acquisition; delegates to the async path.
            /// </summary>
            /// <param name="requestContext">Requested scopes.</param>
            /// <param name="cancellationToken">Ignored.</param>
            /// <returns>The fixture token.</returns>
            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                #region implementation
                return GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();
                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Records the context and returns the fixture token or throws.
            /// </summary>
            /// <param name="requestContext">Requested scopes.</param>
            /// <param name="cancellationToken">Ignored.</param>
            /// <returns>The fixture token.</returns>
            public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                #region implementation
                LastContext = requestContext;

                if (ToThrow != null)
                {
                    throw ToThrow;
                }

                return new ValueTask<AccessToken>(new AccessToken("fixture-token", FixedExpiration));
                #endregion
            }

            #endregion
        }

        #endregion

        #endregion
    }
}
