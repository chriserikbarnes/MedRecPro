using MedRecPro.Helpers;
using MedRecPro.Service.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Drawing;

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
    public class UtilTests
    {
        #region implementation

        private const string TestSecret = "UtilTests-Fixed-Secret-For-Coverage";

        /**************************************************************/
        /// <summary>
        /// Initializes Util with deterministic services before each test.
        /// </summary>
        [TestInitialize]
        public void TestInitialize()
        {
            #region implementation
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
        /// Verifies wait helpers complete and time out on deterministic conditions.
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

            await Assert.ThrowsExceptionAsync<TimeoutException>(
                () => Util.WaitUntil(() => false, frequency: 1, timeout: 10));
            await Assert.ThrowsExceptionAsync<TimeoutException>(
                () => Task.Delay(50).ContinueWith(_ => 1).TimeoutAfter(TimeSpan.FromMilliseconds(1)));
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
