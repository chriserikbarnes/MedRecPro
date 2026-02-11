/**************************************************************/
/// <summary>
/// Provides AES encryption and decryption for string values using PBKDF2 key derivation.
/// </summary>
/// <remarks>
/// This is a copy of the StringCipher class from the MedRecPro API project
/// (MedRecPro\Helpers\EncryptionHelper.cs) with the base64 URL-safe encoding
/// helpers from TextUtil inlined to avoid pulling in heavy dependencies
/// (HtmlAgilityPack, Humanizer, Ganss.Xss).
///
/// Only the Encrypt (static) and Decrypt (instance) methods are needed here
/// for encrypting/decrypting user IDs with EncryptionStrength.Fast.
/// </remarks>
/// <seealso cref="MedRecProMCP.Services.UserResolutionService"/>
/**************************************************************/

using System.Security.Cryptography;
using System.Text;

namespace MedRecProMCP.Helpers;

/**************************************************************/
/// <summary>
/// Provides encryption for internal key masking and external
/// user-supplied passphrase encryption.
/// </summary>
/**************************************************************/
public class StringCipher
{
    #region Constants
    // AES key size in bits
    private const int KeySizeBits = 128;
    // AES key size in bytes
    private const int KeySizeBytes = KeySizeBits / 8; // 16 bytes

    // AES block size is fixed at 128 bits; IV size matches block size
    private const int BlockSizeBytes = 128 / 8; // 16 bytes

    // Salt size for PBKDF2 (128 bits)
    private const int SaltSizeBytes = 16;

    // Iteration counts for PBKDF2
    private const int DerivationIterationsFast = 1;
    private const int DerivationIterationsStrong = 1000000;
    private const int DerivationIterationsOriginal = 1000;

    // Hash algorithm for PBKDF2 — must match the API's implementation
    private static readonly HashAlgorithmName Pbkdf2HashAlgorithm = HashAlgorithmName.SHA256;

    /// <summary>
    /// Encryption strength selector for choosing between fast and strong encryption.
    /// Use Fast for internal ID masking; use Strong for sensitive data.
    /// </summary>
    public enum EncryptionStrength
    {
        Fast,   // Low PBKDF2 iterations (internal IDs)
        Strong  // High PBKDF2 iterations (sensitive data)
    }
    #endregion

    /**************************************************************/
    /// <summary>
    /// Encrypts a plain text string using AES-CBC with PBKDF2 key derivation.
    /// </summary>
    /// <param name="plainText">The plain text string to encrypt.</param>
    /// <param name="passPhrase">The passphrase used for key derivation.</param>
    /// <param name="strength">The encryption strength level (Fast or Strong).</param>
    /// <returns>A URL-safe base64-encoded encrypted string with "F-" or "S-" prefix.</returns>
    /// <remarks>
    /// The encrypted result includes the salt and IV prepended to the ciphertext,
    /// then encoded as URL-safe base64 with a strength prefix.
    /// </remarks>
    /// <seealso cref="Decrypt"/>
    /**************************************************************/
    public static string Encrypt(string plainText,
        string passPhrase,
        EncryptionStrength strength = EncryptionStrength.Strong)
    {
        #region implementation
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

                using (var encryptor = aes.CreateEncryptor())
                using (var memoryStream = new MemoryStream())
                {
                    // Write salt and IV to the beginning of the stream
                    memoryStream.Write(saltBytes, 0, saltBytes.Length);
                    memoryStream.Write(ivBytes, 0, ivBytes.Length);

                    // Encrypt the plaintext
                    using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write, leaveOpen: true))
                    {
                        cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                        cryptoStream.FlushFinalBlock();
                    }

                    byte[] cipherTextBytesWithSaltAndIv = memoryStream.ToArray();

                    // Inlined from TextUtil.ToUrlSafeBase64StringManual
                    string encoding = Convert.ToBase64String(cipherTextBytesWithSaltAndIv)
                        .Replace('+', '-')
                        .Replace('/', '_')
                        .TrimEnd('=');

                    // Prefix with strength identifier for decryption
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
    /// <param name="cipherTextWithSaltAndIvBase64">The encrypted string with embedded salt and IV.</param>
    /// <param name="passPhrase">The passphrase used for key derivation.</param>
    /// <returns>The original plain text string.</returns>
    /// <remarks>
    /// Automatically detects the encryption strength from the prefix ("F-" or "S-")
    /// and maintains backward compatibility with legacy encrypted data.
    /// </remarks>
    /// <seealso cref="Encrypt"/>
    /**************************************************************/
    public string Decrypt(string cipherTextWithSaltAndIvBase64, string passPhrase)
    {
        #region implementation
        ArgumentNullException.ThrowIfNull(cipherTextWithSaltAndIvBase64);
        ArgumentNullException.ThrowIfNull(passPhrase);

        // Determine encryption strength from prefix
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
            // Backward compatibility: handle legacy data
            return decryptInternal(cipherTextWithSaltAndIvBase64, passPhrase, DerivationIterationsOriginal);
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Internal decryption implementation.
    /// </summary>
    /// <param name="cipherTextWithSaltAndIvBase64">The base64-encoded encrypted data (without prefix).</param>
    /// <param name="passPhrase">The passphrase used for key derivation.</param>
    /// <param name="iterations">The number of PBKDF2 iterations to use.</param>
    /// <returns>The decrypted plain text string.</returns>
    /**************************************************************/
    private static string decryptInternal(string cipherTextWithSaltAndIvBase64,
        string passPhrase,
        int iterations)
    {
        #region implementation
        ArgumentNullException.ThrowIfNull(cipherTextWithSaltAndIvBase64);
        ArgumentNullException.ThrowIfNull(passPhrase);

        byte[] cipherTextBytesWithSaltAndIv;

        try
        {
            // Inlined from TextUtil.FromUrlSafeBase64StringManual
            string base64 = cipherTextWithSaltAndIvBase64.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            cipherTextBytesWithSaltAndIv = Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Invalid ciphertext format. The data could not be decoded.", ex);
        }

        ReadOnlySpan<byte> fullCipherSpan = cipherTextBytesWithSaltAndIv;

        // Validate minimum required length for salt, IV, and encrypted data
        if (fullCipherSpan.Length < SaltSizeBytes + BlockSizeBytes)
        {
            throw new CryptographicException("Ciphertext is too short to contain salt, IV, and data.");
        }

        // Extract salt and IV from the beginning of the encrypted data
        byte[] saltBytes = fullCipherSpan.Slice(0, SaltSizeBytes).ToArray();
        byte[] ivBytes = fullCipherSpan.Slice(SaltSizeBytes, BlockSizeBytes).ToArray();
        ReadOnlySpan<byte> actualCipherTextBytes = fullCipherSpan.Slice(SaltSizeBytes + BlockSizeBytes);

        // Derive the decryption key using the extracted salt
        using (var password = new Rfc2898DeriveBytes(passPhrase, saltBytes, iterations, Pbkdf2HashAlgorithm))
        {
            byte[] keyBytes = password.GetBytes(KeySizeBytes);

            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
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
                        throw new CryptographicException(
                            "Decryption failed. This could be due to an incorrect passphrase or corrupted data.", ex);
                    }
                }
            }
        }
        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Generates cryptographically secure random bytes.
    /// </summary>
    /// <param name="numberOfBytes">The number of random bytes to generate.</param>
    /// <returns>A byte array filled with cryptographically secure random data.</returns>
    /**************************************************************/
    private static byte[] generateRandomBytes(int numberOfBytes)
    {
        #region implementation
        byte[] randomBytes = new byte[numberOfBytes];
        RandomNumberGenerator.Fill(randomBytes);
        return randomBytes;
        #endregion
    }
}
