
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MedRecPro.Helpers
{
    /// <summary>
    /// Provides encryption for the users logon credential
    /// </summary>
    /// 

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
        // with the original code's performance characteristics, but consider increasing for new uses.
        private const int DerivationIterations = 1000;

        // Hash algorithm for PBKDF2.
        // The original code implicitly used SHA1. SHA256 is more secure.
        // IMPORTANT: Changing this makes the derived key different.
        // If decrypting data from the old system, you MUST use HashAlgorithmName.SHA1.
        private static readonly HashAlgorithmName Pbkdf2HashAlgorithm = HashAlgorithmName.SHA256;
        #endregion

        /******************************************************/
        /// <summary>
        /// Returns an encrypted value for the passed string.
        /// </summary>
        /// <param name="plainText"></param>
        /// <param name="passPhrase"></param>
        /// <returns></returns>
        /// <remarks>According to online sources, FIPS compliance requires
        /// a 128 bit block size. 
        /// </remarks>
        public static string Encrypt(string plainText, string passPhrase)
        {
            #region implementation
            ArgumentNullException.ThrowIfNull(plainText);
            ArgumentNullException.ThrowIfNull(passPhrase);

            byte[] saltBytes = GenerateRandomBytes(SaltSizeBytes);
            byte[] ivBytes = GenerateRandomBytes(BlockSizeBytes);
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

            using (var password = new Rfc2898DeriveBytes(passPhrase, saltBytes, DerivationIterations, Pbkdf2HashAlgorithm))
            {
                byte[] keyBytes = password.GetBytes(KeySizeBytes);

                using (var aes = Aes.Create()) // Uses modern Aes.Create()
                {
                    aes.Key = keyBytes;
                    aes.IV = ivBytes;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    // aes.BlockSize is implicitly 128 for Aes.Create()

                    using (var encryptor = aes.CreateEncryptor())
                    using (var memoryStream = new MemoryStream())
                    {
                        // Write Salt and IV to the beginning of the stream
                        memoryStream.Write(saltBytes, 0, saltBytes.Length);
                        memoryStream.Write(ivBytes, 0, ivBytes.Length);

                        using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write, leaveOpen: true))
                        {
                            cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                            cryptoStream.FlushFinalBlock(); // Ensure all data is written
                        }
                        // No explicit cryptoStream.Close() needed due to using block

                        byte[] cipherTextBytesWithSaltAndIv = memoryStream.ToArray();
                        return TextUtil.ToUrlSafeBase64StringManual(cipherTextBytesWithSaltAndIv);
                    }
                }
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Returns a clear text result from the passed encrypted string.
        /// </summary>
        /// <param name="cipherTextWithSaltAndIvBase64"></param>
        /// <param name="passPhrase"></param>
        /// <returns></returns>
        protected internal string Decrypt(string cipherTextWithSaltAndIvBase64, string passPhrase)
        {
            #region implementation
            ArgumentNullException.ThrowIfNull(cipherTextWithSaltAndIvBase64);
            ArgumentNullException.ThrowIfNull(passPhrase);

            byte[] cipherTextBytesWithSaltAndIv = TextUtil.FromUrlSafeBase64StringManual(cipherTextWithSaltAndIvBase64);

            // Performance: Use ReadOnlySpan to avoid allocations for slicing until necessary.
            ReadOnlySpan<byte> fullCipherSpan = cipherTextBytesWithSaltAndIv;

            if (fullCipherSpan.Length < SaltSizeBytes + BlockSizeBytes)
            {
                throw new CryptographicException("Ciphertext is too short to contain salt, IV, and data.");
            }

            // Extract salt and IV
            // ToArray() is necessary here because Rfc2898DeriveBytes and aes.IV expect byte[]
            byte[] saltBytes = fullCipherSpan.Slice(0, SaltSizeBytes).ToArray();
            byte[] ivBytes = fullCipherSpan.Slice(SaltSizeBytes, BlockSizeBytes).ToArray();
            ReadOnlySpan<byte> actualCipherTextBytes = fullCipherSpan.Slice(SaltSizeBytes + BlockSizeBytes);

            using (var password = new Rfc2898DeriveBytes(passPhrase, saltBytes, DerivationIterations, Pbkdf2HashAlgorithm))
            {
                byte[] keyBytes = password.GetBytes(KeySizeBytes);

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

        /******************************************************/
        /// <summary>
        /// Adds randomness to the the encrypted value.
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        ///         * This method is deprecated and should not be used in new code.
        /// </remarks>
        private static byte[] Generate128BitsOfRandomEntropy_depricated()
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

        /******************************************************/
        private static byte[] GenerateRandomBytes(int numberOfBytes)
        {
            #region implementation
            byte[] randomBytes = new byte[numberOfBytes];
            RandomNumberGenerator.Fill(randomBytes); // secure random bytes
            return randomBytes; 
            #endregion
        }
    }
}