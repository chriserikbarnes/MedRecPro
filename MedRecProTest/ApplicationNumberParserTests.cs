using MedRecProImportClass.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="ApplicationNumberParser"/>. Covers the prefix-strip
    /// + normalize contract that feeds Orange Book joins from both
    /// <c>OrangeBookProductParsingService</c> (existing) and
    /// <c>BioequivalentLabelDedupService</c> (new).
    /// </summary>
    /// <seealso cref="ApplicationNumberParser"/>
    [TestClass]
    public class ApplicationNumberParserTests
    {
        #region TryParse — NDA/ANDA recognition

        [TestMethod]
        public void TryParse_AndaPrefix_ReturnsApplTypeA()
        {
            var ok = ApplicationNumberParser.TryParse("ANDA202230", out var type, out var no);

            Assert.IsTrue(ok);
            Assert.AreEqual("A", type);
            Assert.AreEqual("202230", no);
        }

        [TestMethod]
        public void TryParse_NdaPrefix_ReturnsApplTypeN()
        {
            var ok = ApplicationNumberParser.TryParse("NDA020610", out var type, out var no);

            Assert.IsTrue(ok);
            Assert.AreEqual("N", type);
            Assert.AreEqual("020610", no);
        }

        [TestMethod]
        public void TryParse_LowercasePrefix_NormalizesToUpper()
        {
            var ok = ApplicationNumberParser.TryParse("anda202230", out var type, out var no);

            Assert.IsTrue(ok);
            Assert.AreEqual("A", type);
            Assert.AreEqual("202230", no);
        }

        [TestMethod]
        public void TryParse_MixedCasePrefix_NormalizesToUpper()
        {
            var ok = ApplicationNumberParser.TryParse("aNdA202230", out var type, out var no);

            Assert.IsTrue(ok);
            Assert.AreEqual("A", type);
            Assert.AreEqual("202230", no);
        }

        #endregion

        #region TryParse — whitespace handling

        [TestMethod]
        public void TryParse_LeadingAndTrailingWhitespace_Trimmed()
        {
            var ok = ApplicationNumberParser.TryParse("   ANDA202230   ", out var type, out var no);

            Assert.IsTrue(ok);
            Assert.AreEqual("A", type);
            Assert.AreEqual("202230", no);
        }

        [TestMethod]
        public void TryParse_TrailingWhitespaceOnly_Trimmed()
        {
            // SPL data in the wild includes "ANDA202230 " entries — see user's data dump.
            var ok = ApplicationNumberParser.TryParse("ANDA202230 ", out var type, out var no);

            Assert.IsTrue(ok);
            Assert.AreEqual("A", type);
            Assert.AreEqual("202230", no);
        }

        #endregion

        #region TryParse — failure cases

        [TestMethod]
        public void TryParse_Null_ReturnsFalseAndEmptyOutParams()
        {
            var ok = ApplicationNumberParser.TryParse(null, out var type, out var no);

            Assert.IsFalse(ok);
            Assert.AreEqual(string.Empty, type);
            Assert.AreEqual(string.Empty, no);
        }

        [TestMethod]
        public void TryParse_EmptyString_ReturnsFalse()
        {
            var ok = ApplicationNumberParser.TryParse(string.Empty, out var type, out var no);

            Assert.IsFalse(ok);
            Assert.AreEqual(string.Empty, type);
            Assert.AreEqual(string.Empty, no);
        }

        [TestMethod]
        public void TryParse_WhitespaceOnly_ReturnsFalse()
        {
            var ok = ApplicationNumberParser.TryParse("   ", out var type, out var no);

            Assert.IsFalse(ok);
            Assert.AreEqual(string.Empty, type);
            Assert.AreEqual(string.Empty, no);
        }

        [TestMethod]
        public void TryParse_UnknownPrefix_ReturnsFalse()
        {
            // OTC monographs, BLAs, and other non-NDA/non-ANDA marketing categories.
            var ok = ApplicationNumberParser.TryParse("BLA125557", out var type, out var no);

            Assert.IsFalse(ok);
            Assert.AreEqual(string.Empty, type);
            Assert.AreEqual(string.Empty, no);
        }

        [TestMethod]
        public void TryParse_UnprefixedNumericOnly_ReturnsFalse()
        {
            // OrangeBookProductParsingService's Tier-2 fallback accepts bare numerics,
            // but TryParse is strict: it requires an NDA/ANDA prefix to classify the tier.
            // Callers that need Tier-2 behavior should use ExtractNumeric.
            var ok = ApplicationNumberParser.TryParse("020610", out var type, out var no);

            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void TryParse_PrefixOnlyNoNumeric_ReturnsFalse()
        {
            var ok = ApplicationNumberParser.TryParse("ANDA", out var type, out var no);

            Assert.IsFalse(ok);
            Assert.AreEqual(string.Empty, type);
            Assert.AreEqual(string.Empty, no);
        }

        [TestMethod]
        public void TryParse_PrefixWithOnlyWhitespaceAfter_ReturnsFalse()
        {
            var ok = ApplicationNumberParser.TryParse("ANDA   ", out var type, out var no);

            Assert.IsFalse(ok);
        }

        #endregion

        #region ExtractNumeric — preserves Tier-2 fallback behavior

        [TestMethod]
        public void ExtractNumeric_AndaPrefix_StripsToNumeric()
        {
            Assert.AreEqual("202230", ApplicationNumberParser.ExtractNumeric("ANDA202230"));
        }

        [TestMethod]
        public void ExtractNumeric_NdaPrefix_StripsToNumeric()
        {
            Assert.AreEqual("020610", ApplicationNumberParser.ExtractNumeric("NDA020610"));
        }

        [TestMethod]
        public void ExtractNumeric_UnprefixedNumeric_UpperCasedPassThrough()
        {
            // Matches OrangeBookProductParsingService's existing behavior: if the
            // source value is already numeric-only, return it unchanged (upper-cased).
            Assert.AreEqual("020610", ApplicationNumberParser.ExtractNumeric("020610"));
        }

        [TestMethod]
        public void ExtractNumeric_Null_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, ApplicationNumberParser.ExtractNumeric(null));
        }

        [TestMethod]
        public void ExtractNumeric_LowerCasePrefix_NormalizesToUpper()
        {
            Assert.AreEqual("202230", ApplicationNumberParser.ExtractNumeric("anda202230"));
        }

        [TestMethod]
        public void ExtractNumeric_Whitespace_Trimmed()
        {
            Assert.AreEqual("202230", ApplicationNumberParser.ExtractNumeric("  ANDA202230  "));
        }

        #endregion

        #region ApplType constants

        [TestMethod]
        public void ApplTypeConstants_MatchOrangeBookConvention()
        {
            // Orange Book products.txt uses single-character codes for Appl_Type.
            Assert.AreEqual("N", ApplicationNumberParser.ApplTypeNda);
            Assert.AreEqual("A", ApplicationNumberParser.ApplTypeAnda);
        }

        #endregion
    }
}
