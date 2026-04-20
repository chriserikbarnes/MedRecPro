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

        #region New Aliases — Total AUC, Peak Concentration, TPEAK, C0h, Space-Variant AUC

        [TestMethod]
        [DataRow("Total AUC", "AUC")]
        [DataRow("total auc", "AUC")]
        [DataRow("Overall AUC", "AUC")]
        public void TryCanonicalize_TotalAUC_MapsToAUC(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon));
            Assert.AreEqual(expected, canon);
        }

        [TestMethod]
        [DataRow("Peak concentration", "Cmax")]
        [DataRow("Peak Concentrations", "Cmax")]
        [DataRow("Peak concentration at steady state", "Cmax")]
        [DataRow("Peak Concentration at Steady State", "Cmax")]
        public void TryCanonicalize_PeakConcentrationVariants_MapToCmax(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon));
            Assert.AreEqual(expected, canon);
        }

        [TestMethod]
        [DataRow("TPEAK", "Tmax")]
        [DataRow("T PEAK", "Tmax")]
        [DataRow("T-peak", "Tmax")]
        [DataRow("Time of Peak", "Tmax")]
        public void TryCanonicalize_TPEAKVariants_MapToTmax(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon));
            Assert.AreEqual(expected, canon);
        }

        [TestMethod]
        [DataRow("C0h", "Ctrough")]
        [DataRow("C 0h", "Ctrough")]
        [DataRow("Predose Concentration", "Ctrough")]
        public void TryCanonicalize_C0hVariants_MapToCtrough(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon));
            Assert.AreEqual(expected, canon);
        }

        [TestMethod]
        [DataRow("AUC0 to ∞", "AUC0-inf")]
        [DataRow("AUC0 to inf", "AUC0-inf")]
        [DataRow("AUC0 to infinity", "AUC0-inf")]
        [DataRow("AUC 0 to ∞", "AUC0-inf")]
        [DataRow("AUC 0 to inf", "AUC0-inf")]
        public void TryCanonicalize_AUC0ToInfSpaceVariants_MapToAUC0Inf(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon));
            Assert.AreEqual(expected, canon);
        }

        [TestMethod]
        [DataRow("AUC0 to 24", "AUC0-24")]
        [DataRow("AUC 0 to 24", "AUC0-24")]
        [DataRow("AUC0 to 24h", "AUC0-24")]
        [DataRow("AUC(0 to 24)", "AUC0-24")]
        public void TryCanonicalize_AUC0To24SpaceVariants_MapToAUC024(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon));
            Assert.AreEqual(expected, canon);
        }

        [TestMethod]
        [DataRow("AUC0 to ∞(ng*hr/mL)", "AUC0-inf")]
        public void TryCanonicalize_AUC0ToInfWithEmbeddedUnit_MapsCorrectly(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon));
            Assert.AreEqual(expected, canon);
        }

        #endregion New Aliases

        #region ContainsPkParameter — Extended Coverage

        [TestMethod]
        [DataRow("Total AUC")]
        [DataRow("TPEAK(h)‡")]
        [DataRow("Peak concentration at steady state (Cmax,ss, mcg/mL)")]
        [DataRow("AUC0 to ∞(ng*hr/mL)")]
        [DataRow("Change in AUC")]
        public void ContainsPkParameter_ExtendedVariants_ReturnsTrue(string input)
        {
            Assert.IsTrue(PkParameterDictionary.ContainsPkParameter(input),
                $"ContainsPkParameter should match: {input}");
        }

        #endregion ContainsPkParameter — Extended Coverage

        #region TryExtractCanonicalFromPhrase

        [TestMethod]
        public void TryExtractCanonicalFromPhrase_CmaxSteadyStateWithUnit_ReturnsCmaxAndSteadyState()
        {
            var ok = PkParameterDictionary.TryExtractCanonicalFromPhrase(
                "Peak concentration at steady state (Cmax,ss, mcg/mL)",
                out var canon, out var qualifier);

            Assert.IsTrue(ok);
            Assert.AreEqual("Cmax", canon);
            Assert.AreEqual("steady_state", qualifier);
        }

        [TestMethod]
        public void TryExtractCanonicalFromPhrase_AreaUnderCurveAUCInfWithUnit_ReturnsAUCInf()
        {
            var ok = PkParameterDictionary.TryExtractCanonicalFromPhrase(
                "Area under the curve (AUC0-∞, day•mcg/mL)",
                out var canon, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual("AUC0-inf", canon);
        }

        [TestMethod]
        public void TryExtractCanonicalFromPhrase_DistributionHalfLife_ReturnsThalfAndDistribution()
        {
            var ok = PkParameterDictionary.TryExtractCanonicalFromPhrase(
                "Distribution half-life (t½, days)",
                out var canon, out var qualifier);

            Assert.IsTrue(ok);
            Assert.AreEqual("t½", canon);
            Assert.AreEqual("distribution", qualifier);
        }

        [TestMethod]
        public void TryExtractCanonicalFromPhrase_TerminalHalfLife_ReturnsThalfAndTerminal()
        {
            var ok = PkParameterDictionary.TryExtractCanonicalFromPhrase(
                "Terminal half-life (t½, days)",
                out var canon, out var qualifier);

            Assert.IsTrue(ok);
            Assert.AreEqual("t½", canon);
            Assert.AreEqual("terminal", qualifier);
        }

        [TestMethod]
        public void TryExtractCanonicalFromPhrase_SystemicClearanceWithUnit_ReturnsCL()
        {
            var ok = PkParameterDictionary.TryExtractCanonicalFromPhrase(
                "Systemic clearance (CL, mL/day)",
                out var canon, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual("CL", canon);
        }

        [TestMethod]
        public void TryExtractCanonicalFromPhrase_VolumeOfDistributionSteadyState_ReturnsVssAndSteadyState()
        {
            var ok = PkParameterDictionary.TryExtractCanonicalFromPhrase(
                "Volume of distribution at steady state (Vss, L)",
                out var canon, out var qualifier);

            Assert.IsTrue(ok);
            Assert.AreEqual("Vss", canon);
            Assert.AreEqual("steady_state", qualifier);
        }

        [TestMethod]
        [DataRow("steady state", false)]
        [DataRow("Dose", false)]
        [DataRow("Healthy Subjects", false)]
        [DataRow("", false)]
        [DataRow("N=50", false)]
        public void TryExtractCanonicalFromPhrase_NonPkInputs_ReturnsFalse(string input, bool expected)
        {
            var ok = PkParameterDictionary.TryExtractCanonicalFromPhrase(
                input, out _, out _);
            Assert.AreEqual(expected, ok);
        }

        [TestMethod]
        public void TryExtractCanonicalFromPhrase_Null_ReturnsFalse()
        {
            var ok = PkParameterDictionary.TryExtractCanonicalFromPhrase(
                null, out var canon, out var qualifier);
            Assert.IsFalse(ok);
            Assert.AreEqual(string.Empty, canon);
            Assert.IsNull(qualifier);
        }

        #endregion TryExtractCanonicalFromPhrase

        #region Footnote Marker Stripping + AUCT/AUC Numeric Variants (smoke-test follow-ups)

        /// <summary>
        /// Trailing footnote markers (*, †, ‡, §) must not defeat canonicalization.
        /// Observed in TID 37621 where "AUC48(ng·h/mL)*" failed to canonicalize.
        /// </summary>
        [TestMethod]
        [DataRow("Cmax*", "Cmax")]
        [DataRow("Cmax†", "Cmax")]
        [DataRow("Cmax‡", "Cmax")]
        [DataRow("AUCT*", "AUCtau")]
        public void TryCanonicalize_TrailingFootnoteMarkers_Stripped(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"expected match for '{input}'");
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// Trailing footnote marker AFTER trailing paren must also be stripped:
        /// "AUC48(ng·h/mL)*" → generic AUC (non-standard interval collapses).
        /// </summary>
        [TestMethod]
        [DataRow("AUC48(ng·h/mL)*", "AUC")]
        [DataRow("AUC48(ng·h/mL)", "AUC")]
        [DataRow("Cmax(pg/mL)*", "Cmax")]
        public void TryCanonicalize_FootnoteAfterParens_Stripped(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"expected match for '{input}'");
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// AUCT and AUCτ variants collapse to AUCtau (observed in TID 24822
        /// Bismuth table where Subtype="AUCT(ng · h/mL)" defeated rescue).
        /// </summary>
        [TestMethod]
        [DataRow("AUCT", "AUCtau")]
        [DataRow("AUCτ", "AUCtau")]
        [DataRow("AUCT(ng · h/mL)", "AUCtau")]
        [DataRow("AUC(τ)", "AUCtau")]
        public void TryCanonicalize_AUCTVariants_MapToAUCtau(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"expected match for '{input}'");
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// AUC24h (no "0-" prefix) maps to AUC0-24. Observed in TID 9918
        /// Rilpivirine table where Subtype="AUC24h, ng.h/mL".
        /// </summary>
        [TestMethod]
        [DataRow("AUC24h", "AUC0-24")]
        [DataRow("AUC12h", "AUC0-12")]
        public void TryCanonicalize_AUCNhVariants_MapToInterval(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"expected match for '{input}'");
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// Non-standard numeric intervals (AUC48, AUC72, AUC8, AUC96) collapse
        /// to the generic AUC canonical. Observed in TID 37621 AUC48 rows.
        /// Standard intervals (AUC24, AUC12) must still route to their dedicated
        /// canonicals (AUC0-24, AUC0-12).
        /// </summary>
        [TestMethod]
        [DataRow("AUC48", "AUC")]
        [DataRow("AUC48h", "AUC")]
        [DataRow("AUC72", "AUC")]
        [DataRow("AUC8", "AUC")]
        [DataRow("AUC96", "AUC")]
        public void TryCanonicalize_NonStandardAUCIntervals_MapToGenericAUC(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"expected match for '{input}'");
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// Regression guard: the AUC<digits> catch-all must NOT override the
        /// dedicated AUC0-24 / AUC0-12 entries — those must still canonicalize
        /// to their specific forms (AUC0-24, AUC0-12), not the generic AUC.
        /// Entries are ordered specific-first so the iteration in
        /// TryCanonicalize's prefix scan finds them first.
        /// </summary>
        [TestMethod]
        [DataRow("AUC24", "AUC0-24")]
        [DataRow("AUC12", "AUC0-12")]
        public void TryCanonicalize_StandardIntervals_StayDedicated(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon));
            Assert.AreEqual(expected, canon);
        }

        #endregion Footnote Marker Stripping + AUCT/AUC Numeric Variants
    }
}
