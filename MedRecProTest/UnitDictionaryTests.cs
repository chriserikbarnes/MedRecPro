using MedRecProImportClass.Service.TransformationServices.Dictionaries;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="UnitDictionary"/> — the PK unit recognition
    /// and extraction helper introduced by the Wave 3 R10 remediation. Covers
    /// membership tests, canonical normalization, inline cell-text extraction
    /// (longest-match + post-digit anchor), and header-like cell recognition.
    /// </summary>
    /// <remarks>
    /// Complements the existing private <c>_knownUnits</c> /
    /// <c>_unitNormalizationMap</c> tests implicit in
    /// <c>ColumnStandardizationServiceTests</c> — this class exercises the
    /// parser-time extraction path that must fire before Phase-2d normalization.
    /// </remarks>
    /// <seealso cref="UnitDictionary"/>
    [TestClass]
    public class UnitDictionaryTests
    {
        #region IsRecognized

        [TestMethod]
        [DataRow("ng/mL")]
        [DataRow("mcg/mL")]
        [DataRow("pg/mL")]
        [DataRow("mcg·h/mL")]
        [DataRow("ng·h/mL")]
        [DataRow("mg/kg")]
        [DataRow("mL/min/kg")]
        [DataRow("hr")]
        [DataRow("h")]
        [DataRow("min")]
        [DataRow("%")]
        [DataRow("%CV")]
        [DataRow("ratio")]
        public void UnitDictionary_IsRecognized_KnownUnits_ReturnsTrue(string candidate)
        {
            Assert.IsTrue(UnitDictionary.IsRecognized(candidate),
                $"'{candidate}' must be recognized as a unit");
        }

        [TestMethod]
        [DataRow("mcg⋅hr/mL")]  // U+22C5 variant — must fold and match
        [DataRow("ng⋅h/mL")]
        [DataRow("ug/mL")]       // spelling variant
        [DataRow("hrs")]
        [DataRow("percent")]
        public void UnitDictionary_IsRecognized_Variants_ReturnsTrue(string candidate)
        {
            Assert.IsTrue(UnitDictionary.IsRecognized(candidate),
                $"Variant '{candidate}' must be recognized via normalization map");
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("Cmax")]
        [DataRow("Parameter")]
        [DataRow("13.9 ± 2.9")]
        [DataRow("Fluconazole")]
        public void UnitDictionary_IsRecognized_NonUnits_ReturnsFalse(string? candidate)
        {
            Assert.IsFalse(UnitDictionary.IsRecognized(candidate),
                $"'{candidate ?? "<null>"}' must NOT be recognized as a unit");
        }

        #endregion IsRecognized

        #region TryNormalize

        [TestMethod]
        [DataRow("ng/mL", "ng/mL")]
        [DataRow("mcg/mL", "mcg/mL")]
        [DataRow("hrs", "h")]
        [DataRow("hr", "h")]
        [DataRow("percent", "%")]
        [DataRow("ug/mL", "mcg/mL")]
        [DataRow("mcg⋅hr/mL", "mcg·h/mL")]  // U+22C5 fold + normalization
        [DataRow("ng⋅h/mL", "ng·h/mL")]
        public void UnitDictionary_TryNormalize_ReturnsCanonical(string input, string expected)
        {
            var actual = UnitDictionary.TryNormalize(input);
            Assert.AreEqual(expected, actual,
                $"'{input}' should normalize to '{expected}', got '{actual ?? "<null>"}'");
        }

        [TestMethod]
        public void UnitDictionary_TryNormalize_NullInput_ReturnsNull()
        {
            Assert.IsNull(UnitDictionary.TryNormalize(null));
            Assert.IsNull(UnitDictionary.TryNormalize(""));
            Assert.IsNull(UnitDictionary.TryNormalize("   "));
        }

        [TestMethod]
        public void UnitDictionary_TryNormalize_UnrecognizedInput_ReturnsNull()
        {
            Assert.IsNull(UnitDictionary.TryNormalize("not a unit"));
            Assert.IsNull(UnitDictionary.TryNormalize("Cmax"));
            Assert.IsNull(UnitDictionary.TryNormalize("Fluconazole"));
        }

        #endregion TryNormalize

        #region TryExtractFromCellText

        [TestMethod]
        [DataRow("5.5 mcg/mL", "mcg/mL")]
        [DataRow("1800 ng·h/mL", "ng·h/mL")]
        [DataRow("13.8 hr (6.4) (terminal)", "h")]   // hr → h via normalization
        [DataRow("391 ng/mL at 3.2 hr", "ng/mL")]    // first match wins
        [DataRow("125.5 ng/dL", "ng/dL")]
        [DataRow("1,800 mcg·h/mL", "mcg·h/mL")]
        [DataRow("0.5 L/kg", "L/kg")]
        [DataRow("10 mg/kg/day", "mg/kg/day")]
        [DataRow("32 %", "%")]
        public void UnitDictionary_TryExtractFromCellText_InlineUnit_Returned(string cellText, string expected)
        {
            var actual = UnitDictionary.TryExtractFromCellText(cellText);
            Assert.AreEqual(expected, actual,
                $"Cell '{cellText}' should yield unit '{expected}', got '{actual ?? "<null>"}'");
        }

        [TestMethod]
        [DataRow("1.5 mcg·h/mL")]  // longer form should win over any substring
        [DataRow("200 ng·h/mL")]
        public void UnitDictionary_TryExtractFromCellText_LongestMatchWins(string cellText)
        {
            // Asserts the extracted form contains "·h/mL" (composite) rather than
            // the shorter mcg / ng token alone.
            var actual = UnitDictionary.TryExtractFromCellText(cellText);
            Assert.IsNotNull(actual);
            StringAssert.Contains(actual, "·h/mL",
                $"Longest-match should win for '{cellText}', got '{actual}'");
        }

        [TestMethod]
        [DataRow("93.6±14.2")]           // No whitespace between number and ± — no unit
        [DataRow("269 ± 182 (284)")]     // ± is not a unit
        [DataRow("N = 101")]             // no numeric-then-unit form
        [DataRow("(6 years to less than 18 years)")]  // narrative, years not post-numeric-preceded
        [DataRow("")]
        [DataRow(null)]
        public void UnitDictionary_TryExtractFromCellText_NoInlineUnit_ReturnsNull(string? cellText)
        {
            Assert.IsNull(UnitDictionary.TryExtractFromCellText(cellText),
                $"Cell '{cellText ?? "<null>"}' should yield null (no post-digit unit)");
        }

        #endregion TryExtractFromCellText

        #region TryExtractFromHeaderLikeText

        [TestMethod]
        [DataRow("(ng/mL)", "ng/mL")]
        [DataRow("(mcg/mL)", "mcg/mL")]
        [DataRow("ng·h/mL", "ng·h/mL")]
        [DataRow("(hr)", "h")]
        [DataRow("hrs", "h")]
        [DataRow("  ng/mL  ", "ng/mL")]
        public void UnitDictionary_TryExtractFromHeaderLikeText_UnitCell_Returned(string cellText, string expected)
        {
            var actual = UnitDictionary.TryExtractFromHeaderLikeText(cellText);
            Assert.AreEqual(expected, actual,
                $"Header-like '{cellText}' should yield unit '{expected}', got '{actual ?? "<null>"}'");
        }

        [TestMethod]
        [DataRow("Cmax")]
        [DataRow("Parameter")]
        [DataRow("13.9 ± 2.9")]    // mixed content with number — not a pure unit cell
        [DataRow("(Cmax)")]
        [DataRow("")]
        [DataRow(null)]
        public void UnitDictionary_TryExtractFromHeaderLikeText_NonUnitCell_ReturnsNull(string? cellText)
        {
            Assert.IsNull(UnitDictionary.TryExtractFromHeaderLikeText(cellText),
                $"Header-like '{cellText ?? "<null>"}' should yield null");
        }

        #endregion TryExtractFromHeaderLikeText
    }
}
