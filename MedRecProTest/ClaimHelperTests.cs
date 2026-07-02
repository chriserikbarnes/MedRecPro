using MedRecPro.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Security.Claims;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests public claim helper methods for cookie and JWT user identifiers.
    /// </summary>
    /// <seealso cref="ClaimHelper"/>
    [TestClass]
    public class ClaimHelperTests
    {
        #region implementation

        private const string TestSecret = "ClaimHelperTests-Fixed-Secret";

        /**************************************************************/
        /// <summary>
        /// Verifies user ID extraction supports Identity and JWT subject claim types.
        /// </summary>
        /// <seealso cref="ClaimHelper.GetUserIdFromClaims"/>
        [TestMethod]
        public void GetUserIdFromClaims_NameIdentifierAndSubject_ReturnsNumericId()
        {
            #region implementation
            var identityClaims = new[] { new Claim(ClaimTypes.NameIdentifier, "42") };
            var jwtClaims = new[] { new Claim("sub", "84") };
            var invalidClaims = new[] { new Claim("sub", "not-a-number") };

            Assert.AreEqual(42L, ClaimHelper.GetUserIdFromClaims(identityClaims));
            Assert.AreEqual(84L, ClaimHelper.GetUserIdFromClaims(jwtClaims));
            Assert.IsNull(ClaimHelper.GetUserIdFromClaims(invalidClaims));
            Assert.IsNull(ClaimHelper.GetUserIdFromClaims(Array.Empty<Claim>()));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies encrypted user ID extraction encrypts valid claims and rejects missing IDs.
        /// </summary>
        /// <seealso cref="ClaimHelper.GetEncryptedUserIdOrThrow"/>
        [TestMethod]
        public void GetEncryptedUserIdOrThrow_ValidAndMissingClaims_ReturnsEncryptedIdOrThrows()
        {
            #region implementation
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "42") };

            var encrypted = ClaimHelper.GetEncryptedUserIdOrThrow(claims, TestSecret);

            Assert.AreEqual("42", encrypted.Decrypt(TestSecret));
            Assert.ThrowsException<UnauthorizedAccessException>(
                () => ClaimHelper.GetEncryptedUserIdOrThrow(Array.Empty<Claim>(), TestSecret));
            #endregion
        }

        #endregion
    }
}
