using MedRecProImportClass.Service.TransformationServices.Dictionaries;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="PkParameterDictionary"/> — the shared canonical
    /// source of truth for PK parameter names used by table routing, layout
    /// detection, and column standardization.
    /// </summary>
    /// <remarks>
    /// Tests cover:
    /// - Exact canonical and alias matches
    /// - Long-form English variants (Maximum Plasma Concentrations → Cmax, etc.)
    /// - Unicode variants (AUC0-∞, mcg⋅hr/mL)
    /// - Trailing parenthesized unit stripping
    /// - Negative cases (PD markers, population labels, prose)
    /// - StartsWithPk anchored match semantics
    /// - NormalizeUnicode codepoint folding
    /// </remarks>
    /// <seealso cref="PkParameterDictionary"/>
    [TestClass]
    public class PkParameterDictionaryTests
    {
        #region TryCanonicalize — Exact & Alias Matches

        [TestMethod]
        [DataRow("Cmax", "Cmax")]
        [DataRow("C max", "Cmax")]
        [DataRow("Maximum Plasma Concentration", "Cmax")]
        [DataRow("Maximum Plasma Concentrations", "Cmax")]
        [DataRow("Peak Plasma Concentration", "Cmax")]
        [DataRow("Cmin", "Cmin")]
        [DataRow("Minimum Plasma Concentration", "Cmin")]
        [DataRow("Tmax", "Tmax")]
        [DataRow("Time to Maximum Concentration", "Tmax")]
        [DataRow("t½", "t½")]
        [DataRow("t1/2", "t½")]
        [DataRow("Half-life", "t½")]
        [DataRow("Half-Life", "t½")]
        [DataRow("Half Life", "t½")]
        [DataRow("Elimination Half-life", "t½")]
        [DataRow("Elimination Half Life", "t½")]
        [DataRow("MRT", "MRT")]
        [DataRow("Mean Residence Time", "MRT")]
        [DataRow("AUC", "AUC")]
        [DataRow("AUC0-inf", "AUC0-inf")]
        [DataRow("AUC(0-inf)", "AUC0-inf")]
        [DataRow("AUCinf", "AUC0-inf")]
        [DataRow("AUC0-24", "AUC0-24")]
        [DataRow("AUC0-24h", "AUC0-24")]
        [DataRow("AUC(0-24)", "AUC0-24")]
        [DataRow("AUClast", "AUClast")]
        [DataRow("AUC0-t", "AUC0-t")]
        [DataRow("AUCtau", "AUCtau")]
        [DataRow("CL", "CL")]
        [DataRow("Clearance", "CL")]
        [DataRow("Plasma Clearance", "CL")]
        [DataRow("Total Clearance", "CL")]
        [DataRow("Total Body Clearance", "CL")]
        [DataRow("CL/F", "CL/F")]
        [DataRow("Apparent Clearance", "CL/F")]
        [DataRow("Oral Clearance", "CL/F")]
        [DataRow("Vd", "Vd")]
        [DataRow("Volume of Distribution", "Vd")]
        [DataRow("Vd/F", "Vd/F")]
        [DataRow("V/F", "Vd/F")]
        [DataRow("Vz/F", "Vd/F")]
        [DataRow("Apparent Volume of Distribution", "Vd/F")]
        [DataRow("Vss", "Vss")]
        [DataRow("Steady-State Volume of Distribution", "Vss")]
        [DataRow("Bioavailability", "F")]
        [DataRow("F(%)", "F")]
        [DataRow("ke", "ke")]
        [DataRow("Elimination Rate Constant", "ke")]
        public void TryCanonicalize_KnownAlias_ReturnsCanonical(string input, string expected)
        {
            var hit = PkParameterDictionary.TryCanonicalize(input, out var canonical);
            Assert.IsTrue(hit, $"expected '{input}' to match");
            Assert.AreEqual(expected, canonical);
        }

        #endregion TryCanonicalize — Exact & Alias Matches

        #region TryCanonicalize — Trailing Unit Parentheses

        [TestMethod]
        [DataRow("Cmax (mcg/mL)", "Cmax")]
        [DataRow("Maximum Plasma Concentrations (mcg/mL)", "Cmax")]
        [DataRow("Elimination Half-life (hr)", "t½")]
        [DataRow("Elimination Half-life (hour)", "t½")]
        [DataRow("Plasma Clearance (mL/hr/kg)", "CL")]
        [DataRow("Volume of Distribution (mL/kg)", "Vd")]
        [DataRow("AUC(mcg·h/mL)", "AUC")]
        public void TryCanonicalize_TrailingUnit_StripsAndResolves(string input, string expected)
        {
            var hit = PkParameterDictionary.TryCanonicalize(input, out var canonical);
            Assert.IsTrue(hit, $"expected '{input}' to match");
            Assert.AreEqual(expected, canonical);
        }

        #endregion TryCanonicalize — Trailing Unit Parentheses

        #region TryCanonicalize — Unicode Variants

        [TestMethod]
        public void TryCanonicalize_UnicodeInfinity_ResolvesToAuc0Inf()
        {
            // AUC0-∞ using U+221E INFINITY SIGN
            var hit = PkParameterDictionary.TryCanonicalize("AUC0-\u221E", out var canonical);
            Assert.IsTrue(hit);
            Assert.AreEqual("AUC0-inf", canonical);
        }

        [TestMethod]
        public void TryCanonicalize_UnicodeDotOperatorUnit_StripsAndResolves()
        {
            // "AUC0-∞(mcg⋅hr/mL)" — uses U+22C5 DOT OPERATOR in the unit
            var input = "AUC0-\u221E(mcg\u22C5hr/mL)";
            var hit = PkParameterDictionary.TryCanonicalize(input, out var canonical);
            Assert.IsTrue(hit, $"expected '{input}' to canonicalize");
            Assert.AreEqual("AUC0-inf", canonical);
        }

        [TestMethod]
        public void TryCanonicalize_AUC0_24h_WithUnicodeDotUnit_ResolvesToAuc0_24()
        {
            var input = "AUC0-24h(mcg\u22C5hr/mL)";
            var hit = PkParameterDictionary.TryCanonicalize(input, out var canonical);
            Assert.IsTrue(hit);
            Assert.AreEqual("AUC0-24", canonical);
        }

        #endregion TryCanonicalize — Unicode Variants

        #region TryCanonicalize — Negative Cases

        [TestMethod]
        [DataRow("Poor")]             // CYP metabolizer phenotype — not PK
        [DataRow("Intermediate")]
        [DataRow("Ultrarapid")]
        [DataRow("IPA")]              // PD marker
        [DataRow("VASP-PRI")]
        [DataRow("Healthy Subjects")] // Population descriptor
        [DataRow("Patients With Renal Impairment")]
        [DataRow("")]
        [DataRow(" ")]
        public void TryCanonicalize_NonPkContent_ReturnsFalse(string input)
        {
            var hit = PkParameterDictionary.TryCanonicalize(input, out var canonical);
            Assert.IsFalse(hit, $"'{input}' should not match as PK");
            Assert.AreEqual(string.Empty, canonical);
        }

        [TestMethod]
        public void TryCanonicalize_Null_ReturnsFalse()
        {
            var hit = PkParameterDictionary.TryCanonicalize(null, out var canonical);
            Assert.IsFalse(hit);
            Assert.AreEqual(string.Empty, canonical);
        }

        #endregion TryCanonicalize — Negative Cases

        #region IsPkParameter

        [TestMethod]
        public void IsPkParameter_KnownCanonical_ReturnsTrue()
        {
            Assert.IsTrue(PkParameterDictionary.IsPkParameter("Cmax"));
            Assert.IsTrue(PkParameterDictionary.IsPkParameter("AUC0-inf"));
            Assert.IsTrue(PkParameterDictionary.IsPkParameter("Maximum Plasma Concentrations"));
        }

        [TestMethod]
        public void IsPkParameter_NonPk_ReturnsFalse()
        {
            Assert.IsFalse(PkParameterDictionary.IsPkParameter("Poor"));
            Assert.IsFalse(PkParameterDictionary.IsPkParameter("IPA"));
        }

        #endregion IsPkParameter

        #region StartsWithPk

        [TestMethod]
        [DataRow("Cmax 300 mg", true)]
        [DataRow("AUC0-24 study", true)]
        [DataRow("Clearance at steady state", true)]
        [DataRow("Half-life (terminal)", true)]
        [DataRow("300 mg oral", false)]   // Actual dose — NOT PK prefix
        [DataRow("Healthy Volunteers", false)]
        public void StartsWithPk_AnchoredMatch(string input, bool expected)
        {
            Assert.AreEqual(expected, PkParameterDictionary.StartsWithPk(input));
        }

        #endregion StartsWithPk

        #region NormalizeUnicode

        [TestMethod]
        public void NormalizeUnicode_DotOperator_FoldedToMiddleDot()
        {
            var input = "mcg\u22C5hr/mL";   // U+22C5 DOT OPERATOR
            var output = PkParameterDictionary.NormalizeUnicode(input);
            Assert.IsFalse(output.Contains('\u22C5'), "dot operator should be folded");
            Assert.IsTrue(output.Contains('\u00B7'), "should contain middle dot");
        }

        [TestMethod]
        public void NormalizeUnicode_Null_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, PkParameterDictionary.NormalizeUnicode(null));
        }

        [TestMethod]
        public void NormalizeUnicode_NoVariants_Unchanged()
        {
            Assert.AreEqual("Cmax", PkParameterDictionary.NormalizeUnicode("Cmax"));
        }

        #endregion NormalizeUnicode
    }
}
