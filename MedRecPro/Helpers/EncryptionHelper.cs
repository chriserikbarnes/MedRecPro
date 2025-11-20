using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MedRecPro.Models;

namespace MedRecPro.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Provides encryption both internal key masking and external
    /// user supplied passphrase encryption.
    /// </summary>
    /// <seealso cref="Label"/>
    /// <remarks>
    /// This class implements AES encryption with PBKDF2 key derivation for secure string encryption.
    /// It supports both fast and strong encryption modes with different iteration counts.
    /// </remarks>
    /// <example>
    /// <code>
    /// string encrypted = StringCipher.Encrypt("sensitive data", "password", EncryptionStrength.Strong);
    /// string decrypted = StringCipher.Decrypt(encrypted, "password");
    /// </code>
    /// </example>
    public class StringCipher
    {

        #region FIPS compliance info
        /* NOTE for .NET Core & .NET 5+:
         * 
         * Modern .NET (Core and .NET 5+) simplifies FIPS compliance.
         * Using `Aes.Create()` will generally provide a FIPS-compliant AES implementation 
         * if the underlying OS is configured for FIPS mode (e.g., AesCng on Windows).
         * The concerns with RijndaelManaged, AesManaged, and specific limitations of 
         * AesCryptoServiceProvider are largely historical when using `Aes.Create()`.
         * 
         * The original code used CBC mode, which is maintained for compatibility.
         * For new applications, consider AEAD modes like AES-GCM (System.Security.Cryptography.AesGcm)
         * for enhanced security (authenticated encryption).
         */
        #endregion

        #region Constants
        // AES key size in bits.
        private const int KeySizeBits = 128;
        // AES key size in bytes.
        private const int KeySizeBytes = KeySizeBits / 8; // 16 bytes

        // AES block size is fixed at 128 bits for System.Security.Cryptography.Aes implementations.
        // IV size should match the block size.
        private const int BlockSizeBytes = 128 / 8; // 16 bytes

        // Salt size for PBKDF2. 16 bytes (128 bits) is a common choice.
        private const int SaltSizeBytes = 16;

        // Iteration count for PBKDF2.
        // WARNING: 1000 is low by modern standards. OWASP recommends 600,000 for PBKDF2-HMAC-SHA256.
        // Increasing this significantly improves security against passphrase brute-forcing
        // but also significantly decreases performance. Maintained at 1000 for compatibility
        // with the original code's performance characteristics.
        private const int DerivationIterationsFast = 1;
        private const int DerivationIterationsStrong = 1000000;
        private const int DerivationIterationsOriginal = 1000;

        // Hash algorithm for PBKDF2.
        // The original code implicitly used SHA1. SHA256 is more secure.
        // IMPORTANT: Changing this makes the derived key different.
        // If decrypting data from the old system, you MUST use HashAlgorithmName.SHA1.
        private static readonly HashAlgorithmName Pbkdf2HashAlgorithm = HashAlgorithmName.SHA256;

        /// <summary>
        /// Encryption switch for selecting between fast and strong encryption.
        /// Use fast encryption for operation that are only internal and
        /// have no public facing component e.g. primary key ids. Use strong
        /// encryption for health information and other sensitive data.
        /// </summary>
        public enum EncryptionStrength
        {
            Fast,   // e.g., low PBKDF2 iterations
            Strong  // e.g., high PBKDF2 iterations
        }
        #endregion

        /**************************************************************/
        /// <summary>
        /// Returns an encrypted value for the passed string. Use Fast
        /// Encryption when masking internal IDs. Use Strong Encryption
        /// when encrypting sensitive data like health information or 
        /// passwords.
        /// </summary>
        /// <param name="plainText">The plain text string to encrypt</param>
        /// <param name="passPhrase">The passphrase used for key derivation</param>
        /// <param name="strength">The encryption strength level (Fast or Strong)</param>
        /// <returns>A base64-encoded encrypted string with salt and IV prepended</returns>
        /// <remarks>
        /// According to online sources, FIPS compliance requires a 128 bit block size.
        /// The encrypted result includes a prefix indicating the encryption strength used.
        /// </remarks>
        /// <example>
        /// <code>
        /// string encrypted = StringCipher.Encrypt("Hello World", "mypassword", EncryptionStrength.Strong);
        /// </code>
        /// </example>
        public static string Encrypt(string plainText,
            string passPhrase,
            EncryptionStrength strength = EncryptionStrength.Strong)
        {
            #region implementation
            // Validate input parameters to prevent null reference exceptions
            ArgumentNullException.ThrowIfNull(plainText);
            ArgumentNullException.ThrowIfNull(passPhrase);

            // Select iteration count based on encryption strength
            int iterations = (strength == EncryptionStrength.Fast)
                ? DerivationIterationsFast
                : DerivationIterationsStrong;

            // Generate cryptographically secure random salt and IV
            byte[] saltBytes = generateRandomBytes(SaltSizeBytes);
            byte[] ivBytes = generateRandomBytes(BlockSizeBytes);
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

            // Derive encryption key using PBKDF2
            using (var password = new Rfc2898DeriveBytes(passPhrase, saltBytes, iterations, Pbkdf2HashAlgorithm))
            {
                byte[] keyBytes = password.GetBytes(KeySizeBytes);

                // Create AES encryptor with CBC mode and PKCS7 padding
                using (var aes = Aes.Create())
                {
                    aes.Key = keyBytes;
                    aes.IV = ivBytes;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    // aes.BlockSize is implicitly 128 for Aes.Create()
                    using (var encryptor = aes.CreateEncryptor())
                    using (var memoryStream = new MemoryStream())
                    {
                        // Write Salt and IV to the beginning of the stream for later decryption
                        memoryStream.Write(saltBytes, 0, saltBytes.Length);
                        memoryStream.Write(ivBytes, 0, ivBytes.Length);

                        // Encrypt the plaintext and write to stream
                        using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write, leaveOpen: true))
                        {
                            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                            cryptoStream.FlushFinalBlock(); // Ensure all data is written
                        }

                        // No explicit cryptoStream.Close() needed due to using block
                        byte[] cipherTextBytesWithSaltAndIv = memoryStream.ToArray();
                        string encoding = TextUtil.ToUrlSafeBase64StringManual(cipherTextBytesWithSaltAndIv);

                        // Prefix with mode identifier for decryption
                        string tag = (strength == EncryptionStrength.Fast) ? "F-" : "S-";
                        return tag + encoding;

                    }
                }
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Decrypts an encrypted string that was created using the Encrypt method.
        /// </summary>
        /// <param name="cipherTextWithSaltAndIvBase64">The encrypted string with embedded salt and IV</param>
        /// <param name="passPhrase">The passphrase used for key derivation</param>
        /// <returns>The original plain text string</returns>
        /// <remarks>
        /// This method automatically detects the encryption strength used based on the prefix
        /// and maintains backward compatibility with legacy encrypted data.
        /// </remarks>
        /// <example>
        /// <code>
        /// string decrypted = StringCipher.Decrypt(encryptedString, "mypassword");
        /// </code>
        /// </example>
        protected internal string Decrypt(string cipherTextWithSaltAndIvBase64, string passPhrase)
        {
            #region implementation
            // Validate input parameters
            ArgumentNullException.ThrowIfNull(cipherTextWithSaltAndIvBase64);
            ArgumentNullException.ThrowIfNull(passPhrase);

            // Determine encryption strength from prefix and decrypt accordingly
            if (cipherTextWithSaltAndIvBase64.StartsWith("F-"))
            {
                return decryptInternal(cipherTextWithSaltAndIvBase64.Substring(2), passPhrase, DerivationIterationsFast);
            }
            else if (cipherTextWithSaltAndIvBase64.StartsWith("S-"))
            {
                return decryptInternal(cipherTextWithSaltAndIvBase64.Substring(2), passPhrase, DerivationIterationsStrong);
            }
            else
            {
                // Backward compatibility: handle legacy data (assume strong or old iteration count)
                return decryptInternal(cipherTextWithSaltAndIvBase64, passPhrase, DerivationIterationsOriginal);
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns a clear text result from the passed encrypted string.
        /// </summary>
        /// <param name="cipherTextWithSaltAndIvBase64">The base64-encoded encrypted data</param>
        /// <param name="passPhrase">The passphrase used for key derivation</param>
        /// <param name="iterations">The number of PBKDF2 iterations to use</param>
        /// <returns>The decrypted plain text string</returns>
        /// <remarks>
        /// This internal method performs the actual decryption work and is called by the public Decrypt method.
        /// </remarks>
        private static string decryptInternal(string cipherTextWithSaltAndIvBase64,
            string passPhrase,
            int iterations)
        {
            #region implementation
            // Validate all input parameters
            ArgumentNullException.ThrowIfNull(cipherTextWithSaltAndIvBase64);
            ArgumentNullException.ThrowIfNull(passPhrase);
            ArgumentNullException.ThrowIfNull(iterations);

            byte[] cipherTextBytesWithSaltAndIv;

            try
            {
                // Decode the base64 encrypted data
                cipherTextBytesWithSaltAndIv = TextUtil.FromUrlSafeBase64StringManual(cipherTextWithSaltAndIvBase64);
            }
            catch (FormatException ex)
            {
                throw new CryptographicException("Invalid ciphertext format. The data could not be decoded.", ex);
            }

            // Performance: Use ReadOnlySpan to avoid allocations for slicing until necessary.
            ReadOnlySpan<byte> fullCipherSpan = cipherTextBytesWithSaltAndIv;

            // Validate minimum required length for salt, IV, and encrypted data
            if (fullCipherSpan.Length < SaltSizeBytes + BlockSizeBytes)
            {
                throw new CryptographicException("Ciphertext is too short to contain salt, IV, and data.");
            }

            // Extract salt and IV from the beginning of the encrypted data
            // ToArray() is necessary here because Rfc2898DeriveBytes and aes.IV expect byte[]
            byte[] saltBytes = fullCipherSpan.Slice(0, SaltSizeBytes).ToArray();
            byte[] ivBytes = fullCipherSpan.Slice(SaltSizeBytes, BlockSizeBytes).ToArray();
            ReadOnlySpan<byte> actualCipherTextBytes = fullCipherSpan.Slice(SaltSizeBytes + BlockSizeBytes);

            // Derive the decryption key using the extracted salt
            using (var password = new Rfc2898DeriveBytes(passPhrase, saltBytes, iterations, Pbkdf2HashAlgorithm))
            {
                byte[] keyBytes = password.GetBytes(KeySizeBytes);

                // Create AES decryptor with the same settings used for encryption
                using (var aes = Aes.Create())
                {
                    aes.Key = keyBytes;
                    aes.IV = ivBytes;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var decryptor = aes.CreateDecryptor())
                    // Pass actualCipherTextBytes.ToArray() to MemoryStream
                    using (var memoryStream = new MemoryStream(actualCipherTextBytes.ToArray()))
                    using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    using (var streamReader = new StreamReader(cryptoStream, Encoding.UTF8))
                    {
                        try
                        {
                            return streamReader.ReadToEnd();
                        }
                        catch (CryptographicException ex)
                        {
                            // Log the error if needed, but prefer letting the specific exception propagate
                            // or wrap it in a custom, more informative exception.
                            // ErrorHelper.AddErrorMsg("StringCipher.Decrypt: " + ex.Message);
                            // The original code threw a generic new Exception(), which is generally not good.
                            // Throwing the original CryptographicException is better.
                            // It often indicates an incorrect passphrase or corrupted data.
                            throw new CryptographicException("Decryption failed. This could be due to an incorrect passphrase or corrupted data.", ex);
                        }
                    }
                }
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Adds randomness to the the encrypted value.
        /// </summary>
        /// <returns>A 16-byte array filled with cryptographically secure random data</returns>
        /// <remarks>
        /// This method is deprecated and should not be used in new code.
        /// Use GenerateRandomBytes method instead for better performance and modern cryptographic practices.
        /// </remarks>
        private static byte[] generate128BitsOfRandomEntropy_depricated()
        {
            #region implementation (original code)
            var randomBytes = new byte[16]; // 16 Bytes will give us 128 bits.
            using (var rngCsp = new RNGCryptoServiceProvider())
            {
                // Fill the array with cryptographically secure random bytes.
                rngCsp.GetBytes(randomBytes);
            }
            return randomBytes;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates cryptographically secure random bytes for use in encryption operations.
        /// </summary>
        /// <param name="numberOfBytes">The number of random bytes to generate</param>
        /// <returns>A byte array filled with cryptographically secure random data</returns>
        /// <remarks>
        /// This method uses the modern RandomNumberGenerator.Fill method which is more efficient
        /// than the legacy RNGCryptoServiceProvider approach.
        /// </remarks>
        private static byte[] generateRandomBytes(int numberOfBytes)
        {
            #region implementation
            byte[] randomBytes = new byte[numberOfBytes];
            RandomNumberGenerator.Fill(randomBytes); // secure random bytes
            return randomBytes;
            #endregion
        }
    }
}