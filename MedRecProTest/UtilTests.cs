using MedRecPro.Helpers;
using MedRecPro.Service.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Drawing;
using System.Reflection;
using System.Security.Claims;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Exercises deterministic public Util helper methods.
    /// </summary>
    /// <remarks>
    /// Tests initialize Util with in-memory services so static helpers can be
    /// exercised without user secrets or external configuration.
    /// </remarks>
    /// <seealso cref="Util"/>
    [TestClass]
    [TestCategory("Unit")]
    public class UtilTests
    {
        #region implementation

        private const string TestSecret = "UtilTests-Fixed-Secret-For-Coverage";

        /**************************************************************/
        /// <summary>
        /// Initializes Util with deterministic services before each test and
        /// clears the process-wide AsyncLocal login-name cache so tests stay
        /// order-independent.
        /// </summary>
        /// <remarks>
        /// Util caches the resolved login name in a private static AsyncLocal;
        /// without the reflection reset a name resolved by one test could leak
        /// into subsequent tests running on the same execution context.
        /// </remarks>
        /// <seealso cref="Util.Initialize"/>
        /// <seealso cref="Util.GetLoginName"/>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation
            // Reset the shared login-name cache first so no previously
            // resolved name leaks into this test's async flow.
            clearLoginNameCache();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:DB:PKSecret"] = TestSecret
                })
                .Build();

            Util.Initialize(
                new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
                new EncryptionService(configuration),
                new DictionaryUtilityService());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies SafeGet delegates to the initialized dictionary service.
        /// </summary>
        /// <seealso cref="Util.SafeGet"/>
        [TestMethod]
        public void SafeGet_InitializedDictionaryService_ReturnsMatchingValue()
        {
            #region implementation
            var dictionary = new Dictionary<string, object?> { ["DisplayName"] = "Ada" };

            var result = Util.SafeGet(dictionary, "displayName");

            Assert.AreEqual("Ada", result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies encrypted values can be decrypted to typed results through Util.
        /// </summary>
        /// <seealso cref="Util.DecryptAndParseInt"/>
        /// <seealso cref="Util.DecryptAndParseString"/>
        [TestMethod]
        public void DecryptAndParseInt_DecryptAndParseString_ValidCipherText_ReturnsValues()
        {
            #region implementation
            var encryptedInt = StringCipher.Encrypt("42", TestSecret, StringCipher.EncryptionStrength.Fast);
            var encryptedText = StringCipher.Encrypt("alpha", TestSecret, StringCipher.EncryptionStrength.Fast);

            Assert.AreEqual(42, Util.DecryptAndParseInt(encryptedInt));
            Assert.AreEqual("alpha", Util.DecryptAndParseString(encryptedText));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies nullable primitive parsers handle valid and invalid text.
        /// </summary>
        /// <seealso cref="Util.ParseNullableInt"/>
        /// <seealso cref="Util.ParseNullableDecimal"/>
        /// <seealso cref="Util.ParseNullableGuid"/>
        /// <seealso cref="Util.ParseNullableDateTime"/>
        /// <seealso cref="Util.parseNullableDecimal"/>
        /// <seealso cref="Util.ParseNullableBoolWithStringValue"/>
        /// <seealso cref="Util.ParseNullableBool"/>
        [TestMethod]
        public void ParseNullableHelpers_ValidAndInvalidInputs_ReturnExpectedValues()
        {
            #region implementation
            var guid = Guid.NewGuid();

            Assert.AreEqual(12, Util.ParseNullableInt("12"));
            Assert.IsNull(Util.ParseNullableInt("x"));
            Assert.AreEqual(12.5m, Util.ParseNullableDecimal("12.5"));
            Assert.AreEqual(12.5m, Util.parseNullableDecimal("12.5"));
            Assert.AreEqual(guid, Util.ParseNullableGuid(guid.ToString()));
            Assert.AreEqual(new DateTime(2026, 7, 1), Util.ParseNullableDateTime("20260701"));
            Assert.AreEqual(true, Util.ParseNullableBoolWithStringValue("true"));
            Assert.AreEqual(false, Util.ParseNullableBool(false));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetBearerToken reads bearer headers before cache fallback.
        /// </summary>
        /// <seealso cref="Util.GetBearerToken"/>
        /// <seealso cref="Util.GetTokenType"/>
        [TestMethod]
        public void GetBearerToken_AuthorizationHeader_ReturnsBearerValue()
        {
            #region implementation
            var context = new DefaultHttpContext();
            context.Request.Headers.Authorization = "Bearer token-value";
            var accessor = new HttpContextAccessor { HttpContext = context };

            var tokenType = Util.GetTokenType(SampleTokenType.Graph);
            var token = Util.GetBearerToken(SampleTokenType.Graph, accessor);

            Assert.AreEqual(string.Empty, tokenType);
            Assert.AreEqual("token-value", token);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetUserName keeps the current no-lookup behavior null-safe.
        /// </summary>
        /// <seealso cref="Util.GetUserName"/>
        [TestMethod]
        public void GetUserName_NoLookupConfigured_ReturnsNull()
        {
            #region implementation
            Assert.IsNull(Util.GetUserName());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetLoginName strips the domain prefix from a
        /// domain-qualified HTTP context identity name.
        /// </summary>
        /// <remarks>
        /// The helper returns only the portion after the last backslash when
        /// the identity name follows the DOMAIN\user convention.
        /// </remarks>
        /// <seealso cref="Util.GetLoginName"/>
        [TestMethod]
        public void GetLoginName_HttpContextIdentityWithDomain_ReturnsUserNameOnly()
        {
            #region implementation
            // Arrange - authenticated identity carrying a DOMAIN\user name.
            initializeUtilWithIdentityName(@"CONTOSO\fixture.user");

            // Act
            var result = Util.GetLoginName();

            // Assert - only the text after the last backslash is returned.
            Assert.AreEqual("fixture.user", result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetLoginName returns the identity name unchanged when no
        /// domain separator is present.
        /// </summary>
        /// <seealso cref="Util.GetLoginName"/>
        [TestMethod]
        public void GetLoginName_HttpContextIdentityWithoutDomain_ReturnsIdentityName()
        {
            #region implementation
            // Arrange - authenticated identity with a plain user name.
            initializeUtilWithIdentityName("fixture.user");

            // Act
            var result = Util.GetLoginName();

            // Assert - no backslash means the whole name comes back verbatim.
            Assert.AreEqual("fixture.user", result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetLoginName caches the first resolved name in its
        /// AsyncLocal store and serves it on subsequent calls without
        /// re-reading the HTTP context.
        /// </summary>
        /// <remarks>
        /// The second call runs against a different identity; getting the
        /// original name back proves the cache short-circuits context lookup.
        /// </remarks>
        /// <seealso cref="Util.GetLoginName"/>
        [TestMethod]
        public void GetLoginName_CachesResolvedNameForSubsequentCall()
        {
            #region implementation
            // Arrange - resolve and cache a name from the first identity.
            initializeUtilWithIdentityName(@"CONTOSO\cached.user");
            var first = Util.GetLoginName();

            // Act - swap in a different identity WITHOUT clearing the
            // AsyncLocal cache, then resolve again.
            initializeUtilWithIdentityName(@"CONTOSO\other.user");
            var second = Util.GetLoginName();

            // Assert - the cached name wins over the replacement identity.
            Assert.AreEqual("cached.user", first);
            Assert.AreEqual("cached.user", second);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetLoginName falls back to the current Windows identity
        /// when the HTTP context has no authenticated user name.
        /// </summary>
        /// <remarks>
        /// The concrete account name is machine-dependent, so this test
        /// asserts shape (non-empty, domain stripped) plus case-insensitive
        /// equality with Environment.UserName. Inconclusive on non-Windows
        /// hosts where the fallback branch never executes and null returns.
        /// </remarks>
        /// <seealso cref="Util.GetLoginName"/>
        [TestMethod]
        public void GetLoginName_NoHttpContextUserOnWindows_ReturnsWindowsUserWithoutDomain()
        {
            #region implementation
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("Windows identity fallback only executes on Windows hosts.");
                return;
            }

            // Arrange - TestInitialize installed a DefaultHttpContext with an
            // unauthenticated user, so the Windows identity fallback runs.

            // Act - callingMethod exercises the optional log-context argument.
            var result = Util.GetLoginName(nameof(GetLoginName_NoHttpContextUserOnWindows_ReturnsWindowsUserWithoutDomain));

            // Assert - fallback name is non-empty with the machine prefix removed.
            Assert.IsFalse(string.IsNullOrEmpty(result));
            Assert.IsFalse(result!.Contains('\\'));
            Assert.IsTrue(string.Equals(Environment.UserName, result, StringComparison.OrdinalIgnoreCase),
                $"Expected '{Environment.UserName}' (ignoring case) but got '{result}'.");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies IsNullOrEmpty handles null, empty, and populated sequences.
        /// </summary>
        /// <seealso cref="Util.IsNullOrEmpty{T}"/>
        [TestMethod]
        public void IsNullOrEmpty_NullEmptyAndPopulatedSequences_ReturnExpectedValues()
        {
            #region implementation
            IEnumerable<int>? nullList = null;

            Assert.IsTrue(Util.IsNullOrEmpty(nullList!));
            Assert.IsTrue(new List<int>().IsNullOrEmpty());
            Assert.IsFalse(new List<int> { 1 }.IsNullOrEmpty());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GUID parsing helpers return null for invalid values.
        /// </summary>
        /// <seealso cref="Util.TryGuidParse"/>
        /// <seealso cref="Util.TryGetGuid"/>
        /// <seealso cref="Util.ConvertToGUID"/>
        [TestMethod]
        public void GuidHelpers_ValidAndInvalidInputs_ReturnExpectedValues()
        {
            #region implementation
            var guid = Guid.NewGuid();

            Assert.AreEqual(guid, guid.ToString().TryGuidParse());
            Assert.IsNull("not-a-guid".TryGuidParse());
            Assert.AreEqual(guid, new List<string> { "x", guid.ToString() }.TryGetGuid());
            Assert.AreEqual(guid, guid.ToString().ConvertToGUID());
            Assert.IsNull("bad".ConvertToGUID());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies immediate wait helpers and timeout behavior without wall-clock delays.
        /// </summary>
        /// <seealso cref="Util.WaitWhile"/>
        /// <seealso cref="Util.WaitUntil"/>
        /// <seealso cref="Util.TimeoutAfter{TResult}"/>
        [TestMethod]
        public async Task WaitHelpers_CompleteAndTimeout_ReturnExpectedBehavior()
        {
            #region implementation
            await Util.WaitWhile(() => false, frequency: 1, timeout: 50);
            await Util.WaitUntil(() => true, frequency: 1, timeout: 50);
            Assert.AreEqual(7, await Task.FromResult(7).TimeoutAfter(TimeSpan.FromMilliseconds(50)));

            var timeProvider = new FakeTimeProvider();
            var pendingOperation = new TaskCompletionSource<int>();
            var timeout = Util.timeoutAfter(
                pendingOperation.Task,
                TimeSpan.FromSeconds(30),
                timeProvider);

            timeProvider.Advance(TimeSpan.FromSeconds(30));

            await Assert.ThrowsExceptionAsync<TimeoutException>(
                () => timeout);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies color and URI helpers return constrained values.
        /// </summary>
        /// <seealso cref="Util.GetRandomColor"/>
        /// <seealso cref="Util.ToSecureUri"/>
        /// <seealso cref="Util.Normalize"/>
        /// <seealso cref="Util.GetInterpolatedRedToGreen"/>
        [TestMethod]
        public void ColorAndUriHelpers_CommonInputs_ReturnExpectedValues()
        {
            #region implementation
            var color = Util.GetRandomColor(maxBrightness: 1.0);
            var interpolated = 0.5d.GetInterpolatedRedToGreen();

            Assert.AreNotEqual(default(KnownColor), color);
            Assert.AreEqual("https://example.test/path", "http://example.test:8080/path".ToSecureUri());
            Assert.AreEqual(0.5d, 5.Normalize(10));
            Assert.AreEqual(Color.FromArgb(255, 255, 255, 0), interpolated);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies fiscal year and timestamp helpers use documented boundaries.
        /// </summary>
        /// <seealso cref="Util.ToFiscalYear"/>
        /// <seealso cref="Util.GetJavaScriptTimestamp"/>
        [TestMethod]
        public void DateHelpers_FiscalYearAndJavaScriptTimestamp_ReturnExpectedValues()
        {
            #region implementation
            var beforeFiscalYear = new DateTime(2026, 9, 30).ToFiscalYear();
            var afterFiscalYear = new DateTime(2026, 10, 1).ToFiscalYear();
            var timestamp = Util.GetJavaScriptTimestamp(1, 1, 1970, 0);

            Assert.AreEqual(2026, beforeFiscalYear);
            Assert.AreEqual(2027, afterFiscalYear);
            Assert.AreEqual(0, timestamp);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies reflection value helpers read and set simple object properties.
        /// </summary>
        /// <seealso cref="Util.SetValueFromString"/>
        /// <seealso cref="Util.GetPropertyValue{T}"/>
        /// <seealso cref="Util.GetPropertyValueAsString(object, string)"/>
        /// <seealso cref="Util.GetPropertyValueAsString(object, string, string)"/>
        [TestMethod]
        public void PropertyHelpers_ReadAndSetProperties_ReturnExpectedValues()
        {
            #region implementation
            var sample = new SampleUtilDto();

            sample.SetValueFromString(nameof(SampleUtilDto.Count), "12");
            sample.SetValueFromString(nameof(SampleUtilDto.When), "2026-07-01");

            Assert.AreEqual(12, sample.GetPropertyValue<int>(nameof(SampleUtilDto.Count)));
            Assert.AreEqual("12", sample.GetPropertyValueAsString(nameof(SampleUtilDto.Count)));
            Assert.AreEqual(new DateTime(2026, 7, 1).ToShortDateString(), sample.GetPropertyValueAsString(nameof(SampleUtilDto.When), "date"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies hash, equality, null, zero, and clone helpers return stable values.
        /// </summary>
        /// <seealso cref="Util.GetSHA1HashString{T}"/>
        /// <seealso cref="Util.GetSHA256HashString{T}"/>
        /// <seealso cref="Util.GetListHashString{TElement}"/>
        /// <seealso cref="Util.IsEqual"/>
        /// <seealso cref="Util.IsNullOrEmpty(Guid?)"/>
        /// <seealso cref="Util.IsNullOrEmpty(Guid)"/>
        /// <seealso cref="Util.IsNullOrZero"/>
        /// <seealso cref="Util.IsZero"/>
        /// <seealso cref="Util.Clone{T}(List{T})"/>
        /// <seealso cref="Util.Clone{T}(IList{T})"/>
        [TestMethod]
        public void HashNullZeroAndCloneHelpers_CommonInputs_ReturnExpectedValues()
        {
            #region implementation
            var list = new List<string> { "a", "b" };
            var clone = list.Clone();
            IList<CloneableSample> cloneableList = new List<CloneableSample> { new("a") };
            var cloneableCopy = cloneableList.Clone();

            Assert.AreEqual("A9993E364706816ABA3E25717850C26C9CD0D89D", "abc".GetSHA1HashString());
            Assert.AreEqual(64, "abc".GetSHA256HashString()?.Length);
            Assert.IsNotNull(list.GetListHashString());
            Assert.IsTrue("abc".IsEqual("abc"));
            Assert.IsTrue(((Guid?)null).IsNullOrEmpty());
            Assert.IsTrue(Guid.Empty.IsNullOrEmpty());
            Assert.IsTrue(((int?)0).IsNullOrZero());
            Assert.IsTrue(0.IsZero());
            CollectionAssert.AreEqual(list, clone);
            Assert.AreNotSame(cloneableList[0], cloneableCopy[0]);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Clears the process-wide AsyncLocal login-name cache through Util's
        /// explicit friend-assembly seam so GetLoginName tests stay order-independent.
        /// </summary>
        /// <remarks>
        /// The internal seam remains invisible to production callers while
        /// avoiding brittle knowledge of Util's private storage implementation.
        /// </remarks>
        /// <seealso cref="Util.GetLoginName"/>
        private static void clearLoginNameCache()
        {
            #region implementation
            Util.resetUserNameForTests();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Initializes Util with an HttpContext whose user carries the
        /// supplied identity name claim.
        /// </summary>
        /// <param name="identityName">Identity name to expose through HttpContext.User; null or empty leaves the context unauthenticated.</param>
        /// <remarks>
        /// ClaimsIdentity resolves Name from the ClaimTypes.Name claim; the
        /// explicit authentication type marks the identity as authenticated.
        /// </remarks>
        /// <seealso cref="Util.Initialize"/>
        /// <seealso cref="Util.GetLoginName"/>
        private static void initializeUtilWithIdentityName(string? identityName)
        {
            #region implementation
            var context = new DefaultHttpContext();

            if (!string.IsNullOrEmpty(identityName))
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.Name, identityName) }, "TestAuth"));
            }

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:DB:PKSecret"] = TestSecret
                })
                .Build();

            Util.Initialize(
                new HttpContextAccessor { HttpContext = context },
                new EncryptionService(configuration),
                new DictionaryUtilityService());
            #endregion
        }

        private enum SampleTokenType
        {
            Graph
        }

        /**************************************************************/
        /// <summary>
        /// Simple DTO for reflection helper tests.
        /// </summary>
        private sealed class SampleUtilDto
        {
            /**************************************************************/
            /// <summary>
            /// Gets or sets a count.
            /// </summary>
            public int Count { get; set; }

            /**************************************************************/
            /// <summary>
            /// Gets or sets a date.
            /// </summary>
            public DateTime? When { get; set; }
        }

        /**************************************************************/
        /// <summary>
        /// Simple cloneable test type.
        /// </summary>
        private sealed class CloneableSample : ICloneable
        {
            private readonly string _value;

            /**************************************************************/
            /// <summary>
            /// Initializes a new cloneable sample.
            /// </summary>
            /// <param name="value">Sample value.</param>
            public CloneableSample(string value)
            {
                #region implementation
                _value = value;
                #endregion
            }

            /**************************************************************/
            /// <summary>
            /// Clones this instance.
            /// </summary>
            /// <returns>A new instance with the same value.</returns>
            public object Clone()
            {
                #region implementation
                return new CloneableSample(_value);
                #endregion
            }
        }

        #endregion
    }
}
