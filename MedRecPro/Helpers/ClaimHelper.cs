using System.Security.Claims;

namespace MedRecPro.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Provides centralized claim extraction for user identity resolution.
    /// </summary>
    /// <remarks>
    /// The API supports two authentication paths that use different claim types
    /// for the user identifier:
    /// <list type="bullet">
    ///   <item>
    ///     <term>Cookie/Identity auth</term>
    ///     <description>Uses <c>ClaimTypes.NameIdentifier</c> (long URI)</description>
    ///   </item>
    ///   <item>
    ///     <term>MCP JWT auth (McpBearer)</term>
    ///     <description>Uses <c>"sub"</c> (standard JWT registered claim name)</description>
    ///   </item>
    /// </list>
    /// This helper abstracts that difference so callers don't need to know
    /// which authentication scheme was used.
    /// </remarks>
    /// <seealso cref="StringCipher"/>
    /**************************************************************/
    public static class ClaimHelper
    {
        /**************************************************************/
        /// <summary>
        /// Extracts the numeric user ID from claims, checking both cookie auth
        /// (<c>ClaimTypes.NameIdentifier</c>) and MCP JWT auth (<c>"sub"</c>).
        /// </summary>
        /// <param name="claims">The claims collection from the authenticated user.</param>
        /// <returns>The numeric user ID if found and valid; <c>null</c> otherwise.</returns>
        /**************************************************************/
        public static long? GetUserIdFromClaims(IEnumerable<Claim> claims)
        {
            #region implementation
            var idClaim = claims.FirstOrDefault(c =>
                    c.Type.Contains("NameIdentifier", StringComparison.OrdinalIgnoreCase) ||
                    c.Type == "sub")
                ?.Value;

            if (!string.IsNullOrEmpty(idClaim) && long.TryParse(idClaim, out long id) && id > 0)
            {
                return id;
            }

            return null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts and encrypts the numeric user ID from claims.
        /// </summary>
        /// <param name="claims">The claims collection from the authenticated user.</param>
        /// <param name="pkSecret">The encryption key for StringCipher.</param>
        /// <returns>The encrypted user ID string.</returns>
        /// <exception cref="UnauthorizedAccessException">
        /// Thrown when no valid numeric user ID claim is found.
        /// </exception>
        /// <seealso cref="StringCipher.Encrypt(string, string, StringCipher.EncryptionStrength)"/>
        /**************************************************************/
        public static string GetEncryptedUserIdOrThrow(IEnumerable<Claim> claims, string pkSecret)
        {
            #region implementation
            var userId = GetUserIdFromClaims(claims);

            if (!userId.HasValue)
            {
                throw new UnauthorizedAccessException(
                    "Unable to determine user ID from authentication context.");
            }

            return StringCipher.Encrypt(
                userId.Value.ToString(),
                pkSecret,
                StringCipher.EncryptionStrength.Fast);
            #endregion
        }
    }
}
