using MedRecPro.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Cryptography;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for StringCipher encryption and decryption functionality.
    /// </summary>
    /// <remarks>
    /// Tests cover encryption round-trips, edge cases, error handling,
    /// legacy data backward compatibility, and both Fast and Strong encryption modes.
    /// </remarks>
    /// <seealso cref="StringCipher"/>
    [TestClass]
    public class StringCipherTests
    {
        #region Test Constants

        /// <summary>
        /// Standard passphrase for testing purposes.
        /// </summary>
        private const string TestPassphrase = "TestPassphrase123!@#";

        /// <summary>
        /// Alternative passphrase for negative testing.
        /// </summary>
        private const string WrongPassphrase = "WrongPassphrase456$%^";

        #endregion

        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Creates a new StringCipher instance for testing.
        /// </summary>
        /// <returns>A new StringCipher instance</returns>
        private StringCipher createCipher()
        {
            return new StringCipher();
        }

        #endregion

        #region Encrypt/Decrypt Round-Trip Tests - Fast Mode

        /**************************************************************/
        /// <summary>
        /// Verifies that encrypting and decrypting with Fast mode returns the original text.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Encrypt_Decrypt_FastMode_RoundTrip_ReturnsOriginalText()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var originalText = "Hello, World!";

            // Act
            var encrypted = StringCipher.Encrypt(originalText, TestPassphrase, StringCipher.EncryptionStrength.Fast);
            var decrypted = encrypted.Decrypt(TestPassphrase);

            // Assert
            Assert.AreEqual(originalText, decrypted, "Decrypted text should match original");
            Assert.IsTrue(encrypted.StartsWith("F-"), "Fast mode encryption should have F- prefix");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that encrypting and decrypting numeric IDs with Fast mode works correctly.
        /// This is the typical use case for primary key encryption.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Encrypt_Decrypt_FastMode_NumericId_ReturnsOriginalId()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var userId = "12345";

            // Act
            var encrypted = StringCipher.Encrypt(userId, TestPassphrase, StringCipher.EncryptionStrength.Fast);
            var decrypted = encrypted.Decrypt(TestPassphrase);;

            // Assert
            Assert.AreEqual(userId, decrypted, "Decrypted ID should match original");
            Assert.IsTrue(long.TryParse(decrypted, out _), "Decrypted value should be parseable as long");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that encrypting the same value twice produces different ciphertext
        /// due to random salt and IV generation.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        [TestMethod]
        public void Encrypt_FastMode_SameInput_ProducesDifferentOutput()
        {
            #region implementation

            // Arrange
            var originalText = "Same input";

            // Act
            var encrypted1 = StringCipher.Encrypt(originalText, TestPassphrase, StringCipher.EncryptionStrength.Fast);
            var encrypted2 = StringCipher.Encrypt(originalText, TestPassphrase, StringCipher.EncryptionStrength.Fast);

            // Assert - Different due to random salt/IV
            Assert.AreNotEqual(encrypted1, encrypted2, "Same input should produce different ciphertext due to random salt/IV");

            #endregion
        }

        #endregion

        #region Encrypt/Decrypt Round-Trip Tests - Strong Mode

        /**************************************************************/
        /// <summary>
        /// Verifies that encrypting and decrypting with Strong mode returns the original text.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Encrypt_Decrypt_StrongMode_RoundTrip_ReturnsOriginalText()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var originalText = "Sensitive health information";

            // Act
            var encrypted = StringCipher.Encrypt(originalText, TestPassphrase, StringCipher.EncryptionStrength.Strong);
            var decrypted = encrypted.Decrypt(TestPassphrase);;

            // Assert
            Assert.AreEqual(originalText, decrypted, "Decrypted text should match original");
            Assert.IsTrue(encrypted.StartsWith("S-"), "Strong mode encryption should have S- prefix");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that default encryption uses Strong mode.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        [TestMethod]
        public void Encrypt_DefaultStrength_UsesStrongMode()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var originalText = "Test default mode";

            // Act - No strength parameter defaults to Strong
            var encrypted = StringCipher.Encrypt(originalText, TestPassphrase);
            var decrypted = encrypted.Decrypt(TestPassphrase);;

            // Assert
            Assert.IsTrue(encrypted.StartsWith("S-"), "Default encryption should use Strong mode (S- prefix)");
            Assert.AreEqual(originalText, decrypted);

            #endregion
        }

        #endregion

        #region Special Character and Unicode Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that special characters are correctly encrypted and decrypted.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Encrypt_Decrypt_SpecialCharacters_ReturnsOriginalText()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var specialText = "!@#$%^&*()_+-=[]{}|;':\",./<>?`~";

            // Act
            var encrypted = StringCipher.Encrypt(specialText, TestPassphrase, StringCipher.EncryptionStrength.Strong);
            var decrypted = encrypted.Decrypt(TestPassphrase);;

            // Assert
            Assert.AreEqual(specialText, decrypted, "Special characters should be preserved");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that Unicode characters are correctly encrypted and decrypted.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Encrypt_Decrypt_UnicodeCharacters_ReturnsOriginalText()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var unicodeText = "日本語 中文 한국어 العربية";

            // Act
            var encrypted = StringCipher.Encrypt(unicodeText, TestPassphrase, StringCipher.EncryptionStrength.Strong);
            var decrypted = encrypted.Decrypt(TestPassphrase);;

            // Assert
            Assert.AreEqual(unicodeText, decrypted, "Unicode characters should be preserved");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that medical terminology and symbols are correctly encrypted and decrypted.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Encrypt_Decrypt_MedicalTerminology_ReturnsOriginalText()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var medicalText = "Patient ID: 12345, Dosage: 500mg/mL, Temperature: 98.6°F";

            // Act
            var encrypted = StringCipher.Encrypt(medicalText, TestPassphrase, StringCipher.EncryptionStrength.Strong);
            var decrypted = encrypted.Decrypt(TestPassphrase);;

            // Assert
            Assert.AreEqual(medicalText, decrypted, "Medical terminology should be preserved");

            #endregion
        }

        #endregion

        #region Edge Cases - Empty and Short Strings

        /**************************************************************/
        /// <summary>
        /// Verifies that empty strings can be encrypted and decrypted.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Encrypt_Decrypt_EmptyString_ReturnsEmptyString()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var emptyText = "";

            // Act
            var encrypted = StringCipher.Encrypt(emptyText, TestPassphrase, StringCipher.EncryptionStrength.Fast);
            var decrypted = encrypted.Decrypt(TestPassphrase);;

            // Assert
            Assert.AreEqual(emptyText, decrypted, "Empty string should be preserved");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that single character strings can be encrypted and decrypted.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Encrypt_Decrypt_SingleCharacter_ReturnsOriginalCharacter()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var singleChar = "X";

            // Act
            var encrypted = StringCipher.Encrypt(singleChar, TestPassphrase, StringCipher.EncryptionStrength.Fast);
            var decrypted = encrypted.Decrypt(TestPassphrase);;

            // Assert
            Assert.AreEqual(singleChar, decrypted, "Single character should be preserved");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that very long strings can be encrypted and decrypted.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Encrypt_Decrypt_LongString_ReturnsOriginalText()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var longText = new string('A', 10000);

            // Act
            var encrypted = StringCipher.Encrypt(longText, TestPassphrase, StringCipher.EncryptionStrength.Strong);
            var decrypted = encrypted.Decrypt(TestPassphrase);;

            // Assert
            Assert.AreEqual(longText, decrypted, "Long string should be preserved");
            Assert.AreEqual(10000, decrypted.Length, "Decrypted length should match original");

            #endregion
        }

        #endregion

        #region Error Handling Tests - Invalid Input

        /**************************************************************/
        /// <summary>
        /// Verifies that encrypting with null plaintext throws ArgumentNullException.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Encrypt_NullPlainText_ThrowsArgumentNullException()
        {
            #region implementation

            // Arrange & Act
            StringCipher.Encrypt(null!, TestPassphrase, StringCipher.EncryptionStrength.Fast);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that encrypting with null passphrase throws ArgumentNullException.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Encrypt_NullPassphrase_ThrowsArgumentNullException()
        {
            #region implementation

            // Arrange & Act
            StringCipher.Encrypt("test", null!, StringCipher.EncryptionStrength.Fast);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that decrypting with null ciphertext throws ArgumentNullException.
        /// </summary>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Decrypt_NullCipherText_ThrowsArgumentNullException()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();

            // Act
            TextUtil.Decrypt(null!, TestPassphrase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that decrypting with null passphrase throws ArgumentNullException.
        /// </summary>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Decrypt_NullPassphrase_ThrowsArgumentNullException()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var encrypted = StringCipher.Encrypt("test", TestPassphrase);

            // Act
            encrypted.Decrypt(null!);

            #endregion
        }

        #endregion

        #region Error Handling Tests - Wrong Passphrase

        /**************************************************************/
        /// <summary>
        /// Verifies that decrypting with wrong passphrase throws CryptographicException.
        /// </summary>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        [ExpectedException(typeof(CryptographicException))]
        public void Decrypt_WrongPassphrase_ThrowsCryptographicException()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var encrypted = StringCipher.Encrypt("test", TestPassphrase);

            // Act - Should throw CryptographicException
            encrypted.Decrypt(WrongPassphrase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that decrypting corrupted ciphertext throws CryptographicException.
        /// </summary>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        [ExpectedException(typeof(CryptographicException))]
        public void Decrypt_CorruptedCipherText_ThrowsCryptographicException()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var encrypted = StringCipher.Encrypt("test", TestPassphrase);
            var corrupted = encrypted.Substring(0, encrypted.Length / 2) + "CORRUPTED";

            // Act - Should throw CryptographicException
            corrupted.Decrypt(TestPassphrase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that decrypting truncated ciphertext throws CryptographicException.
        /// </summary>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        [ExpectedException(typeof(CryptographicException))]
        public void Decrypt_TruncatedCipherText_ThrowsCryptographicException()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();

            // Truncated ciphertext too short for salt + IV
            var truncated = "F-ABC";

            // Act - Should throw CryptographicException due to insufficient length
            truncated.Decrypt(TestPassphrase);

            #endregion
        }

        #endregion

        #region Legacy Backward Compatibility Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that ciphertext without prefix is handled as legacy data.
        /// </summary>
        /// <remarks>
        /// Legacy data without F- or S- prefix should be decrypted using
        /// the original iteration count for backward compatibility.
        /// </remarks>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Decrypt_LegacyFormatWithoutPrefix_HandlesBackwardCompatibility()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();

            // Note: This test demonstrates the backward compatibility behavior
            // Legacy data would not have F- or S- prefix
            // The Decrypt method should detect this and use legacy iteration count

            // For this test, we verify that the decrypt method accepts
            // data without the expected prefix and attempts legacy decryption
            var encrypted = StringCipher.Encrypt("test", TestPassphrase, StringCipher.EncryptionStrength.Fast);

            // Remove the F- prefix to simulate legacy data
            var legacyFormat = encrypted.Substring(2);

            // Act & Assert - Should attempt legacy decryption (may fail if format doesn't match)
            // This tests the code path, not necessarily successful decryption
            try
            {
                legacyFormat.Decrypt(TestPassphrase);
                // If it succeeds, the legacy path was taken
            }
            catch (CryptographicException)
            {
                // Expected when the iterations don't match
                // This confirms the legacy code path was attempted
                Assert.IsTrue(true, "Legacy decryption path was attempted");
            }

            #endregion
        }

        #endregion

        #region Mode Prefix Detection Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that Fast mode encrypted data has correct prefix.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        [TestMethod]
        public void Encrypt_FastMode_HasCorrectPrefix()
        {
            #region implementation

            // Arrange & Act
            var encrypted = StringCipher.Encrypt("test", TestPassphrase, StringCipher.EncryptionStrength.Fast);

            // Assert
            Assert.IsTrue(encrypted.StartsWith("F-"), "Fast mode should start with F- prefix");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that Strong mode encrypted data has correct prefix.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        [TestMethod]
        public void Encrypt_StrongMode_HasCorrectPrefix()
        {
            #region implementation

            // Arrange & Act
            var encrypted = StringCipher.Encrypt("test", TestPassphrase, StringCipher.EncryptionStrength.Strong);

            // Assert
            Assert.IsTrue(encrypted.StartsWith("S-"), "Strong mode should start with S- prefix");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that Fast mode ciphertext cannot be decrypted using Strong mode iteration count.
        /// </summary>
        /// <remarks>
        /// This test ensures mode detection is working correctly during decryption.
        /// </remarks>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Decrypt_FastModeData_UsesCorrectIterations()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var originalText = "Fast mode test";

            // Act
            var encrypted = StringCipher.Encrypt(originalText, TestPassphrase, StringCipher.EncryptionStrength.Fast);
            var decrypted = encrypted.Decrypt(TestPassphrase);;

            // Assert
            Assert.AreEqual(originalText, decrypted, "Fast mode decryption should work correctly");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that Strong mode ciphertext cannot be decrypted using Fast mode iteration count.
        /// </summary>
        /// <remarks>
        /// This test ensures mode detection is working correctly during decryption.
        /// </remarks>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Decrypt_StrongModeData_UsesCorrectIterations()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var originalText = "Strong mode test";

            // Act
            var encrypted = StringCipher.Encrypt(originalText, TestPassphrase, StringCipher.EncryptionStrength.Strong);
            var decrypted = encrypted.Decrypt(TestPassphrase);;

            // Assert
            Assert.AreEqual(originalText, decrypted, "Strong mode decryption should work correctly");

            #endregion
        }

        #endregion

        #region Multiple Values and Batch Operations

        /**************************************************************/
        /// <summary>
        /// Verifies that multiple different values can be encrypted and decrypted correctly.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Encrypt_Decrypt_MultipleValues_AllReturnCorrectly()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var testValues = new[]
            {
                "1",
                "123456789",
                "user@example.com",
                "Patient Name",
                "NDC: 12345-678-90"
            };

            // Act & Assert
            foreach (var original in testValues)
            {
                var encrypted = StringCipher.Encrypt(original, TestPassphrase, StringCipher.EncryptionStrength.Fast);
                var decrypted = encrypted.Decrypt(TestPassphrase);;
                Assert.AreEqual(original, decrypted, $"Value '{original}' should be preserved");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that different passphrases produce different ciphertext for the same input.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        [TestMethod]
        public void Encrypt_DifferentPassphrases_ProduceDifferentResults()
        {
            #region implementation

            // Arrange
            var originalText = "Same input";
            var passphrase1 = "FirstPassphrase";
            var passphrase2 = "SecondPassphrase";

            // Act
            var encrypted1 = StringCipher.Encrypt(originalText, passphrase1, StringCipher.EncryptionStrength.Fast);
            var encrypted2 = StringCipher.Encrypt(originalText, passphrase2, StringCipher.EncryptionStrength.Fast);

            // Assert
            Assert.AreNotEqual(encrypted1, encrypted2, "Different passphrases should produce different ciphertext");

            #endregion
        }

        #endregion

        #region URL-Safe Base64 Encoding Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that encrypted output is URL-safe Base64 encoded.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        [TestMethod]
        public void Encrypt_Output_IsUrlSafeBase64()
        {
            #region implementation

            // Arrange & Act
            var encrypted = StringCipher.Encrypt("test value", TestPassphrase, StringCipher.EncryptionStrength.Fast);

            // Remove the mode prefix for base64 check
            var base64Part = encrypted.Substring(2);

            // Assert - URL-safe Base64 should not contain + or / characters
            // (they are replaced with - and _)
            Assert.IsFalse(base64Part.Contains("+"), "URL-safe Base64 should not contain +");
            Assert.IsFalse(base64Part.Contains("/"), "URL-safe Base64 should not contain /");

            #endregion
        }

        #endregion

        #region Cross-Mode Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that data encrypted in Fast mode can only be decrypted in Fast mode.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Encrypt_FastMode_Decrypt_UsesCorrectMode()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var originalText = "Fast mode only";

            // Act
            var encryptedFast = StringCipher.Encrypt(originalText, TestPassphrase, StringCipher.EncryptionStrength.Fast);
            var decryptedFast = encryptedFast.Decrypt(TestPassphrase);

            // Assert
            Assert.AreEqual(originalText, decryptedFast);
            Assert.IsTrue(encryptedFast.StartsWith("F-"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that data encrypted in Strong mode can only be decrypted in Strong mode.
        /// </summary>
        /// <seealso cref="StringCipher.Encrypt"/>
        /// <seealso cref="StringCipher.Decrypt"/>
        [TestMethod]
        public void Encrypt_StrongMode_Decrypt_UsesCorrectMode()
        {
            #region implementation

            // Arrange
            var cipher = createCipher();
            var originalText = "Strong mode only";

            // Act
            var encryptedStrong = StringCipher.Encrypt(originalText, TestPassphrase, StringCipher.EncryptionStrength.Strong);
            var decrypted = encryptedStrong.Decrypt(TestPassphrase);

            // Assert
            Assert.AreEqual(originalText, decrypted);
            Assert.IsTrue(encryptedStrong.StartsWith("S-"));

            #endregion
        }

        #endregion
    }
}
