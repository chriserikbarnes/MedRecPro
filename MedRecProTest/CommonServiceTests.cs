using MedRecPro.Helpers;
using MedRecPro.Service.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests common dictionary and encryption services.
    /// </summary>
    /// <remarks>
    /// Uses in-memory configuration to avoid user secrets while preserving the
    /// production encryption service contract.
    /// </remarks>
    /// <seealso cref="DictionaryUtilityService"/>
    /// <seealso cref="EncryptionService"/>
    [TestClass]
    public class CommonServiceTests
    {
        #region implementation

        private const string TestSecret = "CommonServiceTests-Fixed-Secret";

        /**************************************************************/
        /// <summary>
        /// Verifies SafeGet supports exact, casing, and case-insensitive key matches.
        /// </summary>
        /// <seealso cref="DictionaryUtilityService.SafeGet(IDictionary{string, object?}, string)"/>
        /// <seealso cref="DictionaryUtilityService.SafeGet{T}(IDictionary{string, object?}, string)"/>
        [TestMethod]
        public void SafeGet_KeyVariants_ReturnsMatchingValues()
        {
            #region implementation
            var service = new DictionaryUtilityService();
            var dictionary = new Dictionary<string, object?>
            {
                ["PatientName"] = "Ada",
                ["patientAge"] = "42",
                ["MEDICAL_ID"] = 123
            };

            Assert.AreEqual("Ada", service.SafeGet(dictionary, "patientName"));
            Assert.AreEqual(42, service.SafeGet<int>(dictionary, "PatientAge"));
            Assert.AreEqual(123, service.SafeGet<int>(dictionary, "medical_id"));
            Assert.IsNull(service.SafeGet(dictionary, "Missing"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GetAvailableKeys sorts keys and handles null dictionaries.
        /// </summary>
        /// <seealso cref="DictionaryUtilityService.GetAvailableKeys"/>
        [TestMethod]
        public void GetAvailableKeys_NullAndPopulatedDictionary_ReturnsDiagnosticText()
        {
            #region implementation
            var service = new DictionaryUtilityService();
            var dictionary = new Dictionary<string, object>
            {
                ["Zulu"] = 1,
                ["Alpha"] = 2
            };

            Assert.AreEqual("null", service.GetAvailableKeys(null));
            Assert.AreEqual("Alpha, Zulu", service.GetAvailableKeys(dictionary));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies encrypted values decrypt to strings and integers.
        /// </summary>
        /// <seealso cref="EncryptionService.DecryptToInt"/>
        /// <seealso cref="EncryptionService.DecryptToString"/>
        [TestMethod]
        public void DecryptToInt_DecryptToString_ValidAndEmptyValues_ReturnExpectedValues()
        {
            #region implementation
            var service = new EncryptionService(createConfiguration());
            var encryptedNumber = StringCipher.Encrypt("42", TestSecret, StringCipher.EncryptionStrength.Fast);
            var encryptedText = StringCipher.Encrypt("label", TestSecret, StringCipher.EncryptionStrength.Fast);
            var encryptedNonNumber = StringCipher.Encrypt("not-number", TestSecret, StringCipher.EncryptionStrength.Fast);

            Assert.AreEqual(42, service.DecryptToInt(encryptedNumber));
            Assert.IsNull(service.DecryptToInt(encryptedNonNumber));
            Assert.IsNull(service.DecryptToInt(null));
            Assert.AreEqual("label", service.DecryptToString(encryptedText));
            Assert.AreEqual(string.Empty, service.DecryptToString(null));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates deterministic test configuration.
        /// </summary>
        /// <returns>Configuration with the test PK secret.</returns>
        /// <seealso cref="IConfiguration"/>
        private static IConfiguration createConfiguration()
        {
            #region implementation
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:DB:PKSecret"] = TestSecret
                })
                .Build();
            #endregion
        }

        #endregion
    }
}
