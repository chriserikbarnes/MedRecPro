using MedRecPro.Configuration;
using MedRecPro.Helpers;
using Microsoft.Extensions.Options;

namespace MedRecPro.Service
{
    /**************************************************************/
    /// <summary>
    /// Encrypts and safely decrypts primary-key identifiers for application boundaries.
    /// </summary>
    /// <remarks>
    /// The cipher centralizes the raw database-security secret and preserves the established
    /// fast encrypted-ID wire format used by Label endpoints.
    /// </remarks>
    /// <seealso cref="DatabaseSecurityOptions"/>
    /// <seealso cref="StringCipher"/>
    public interface IPrimaryKeyCipher
    {
        /**************************************************************/
        /// <summary>
        /// Encrypts a numeric primary-key identifier.
        /// </summary>
        /// <param name="id">Numeric primary-key identifier to encrypt.</param>
        /// <returns>The established encrypted identifier format.</returns>
        string Encrypt(long id);

        /**************************************************************/
        /// <summary>
        /// Encrypts a primary-key value represented as text.
        /// </summary>
        /// <param name="value">Primary-key value to encrypt.</param>
        /// <returns>The established encrypted identifier format.</returns>
        string Encrypt(string value);

        /**************************************************************/
        /// <summary>
        /// Attempts to decrypt a numeric primary-key identifier.
        /// </summary>
        /// <param name="encryptedId">Encrypted identifier supplied at an application boundary.</param>
        /// <returns>The numeric identifier, or null when the input is malformed or foreign.</returns>
        long? TryDecrypt(string? encryptedId);
    }

    /**************************************************************/
    /// <summary>
    /// Implements <see cref="IPrimaryKeyCipher"/> with the established <see cref="StringCipher"/> format.
    /// </summary>
    /// <remarks>
    /// This stateless singleton reads its fixed secret from validated startup options and never exposes it
    /// to controllers or application services.
    /// </remarks>
    /// <seealso cref="IPrimaryKeyCipher"/>
    public sealed class PrimaryKeyCipher : IPrimaryKeyCipher
    {
        private readonly string _secret;
        private readonly StringCipher _stringCipher = new();

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the <see cref="PrimaryKeyCipher"/> class.
        /// </summary>
        /// <param name="options">Validated database-security options.</param>
        /// <exception cref="ArgumentNullException">Thrown when options are unavailable.</exception>
        public PrimaryKeyCipher(IOptions<DatabaseSecurityOptions> options)
        {
            #region implementation

            _secret = options?.Value?.PKSecret
                ?? throw new ArgumentNullException(nameof(options));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public string Encrypt(long id)
        {
            #region implementation

            return Encrypt(id.ToString(System.Globalization.CultureInfo.InvariantCulture));

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public string Encrypt(string value)
        {
            #region implementation

            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            return StringCipher.Encrypt(value, _secret, StringCipher.EncryptionStrength.Fast);

            #endregion
        }

        /**************************************************************/
        /// <inheritdoc/>
        public long? TryDecrypt(string? encryptedId)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(encryptedId))
            {
                return null;
            }

            try
            {
                var decrypted = _stringCipher.Decrypt(encryptedId, _secret);
                return long.TryParse(
                    decrypted,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var id)
                    ? id
                    : null;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }

            #endregion
        }
    }
}
