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

        #region Wave 2 R4 — Unicode Variant Folding + Matrix Prefix

        /// <summary>
        /// R4 — U+2044 FRACTION SLASH in <c>t1⁄2</c> variants folds to ASCII
        /// <c>/</c> before lookup so the canonical <c>t½</c> alias resolves.
        /// Observed in TID 126/127 (BENLYSTA) shape.
        /// </summary>
        [TestMethod]
        public void R4_NormalizeUnicode_FractionSlash_FoldsToForwardSlash()
        {
            // U+2044 FRACTION SLASH
            var input = "t1\u20442";
            var normalized = PkParameterDictionary.NormalizeUnicode(input);
            Assert.IsTrue(normalized.Contains('/'),
                $"FRACTION SLASH (U+2044) must fold to '/', got: '{normalized}'");
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"input '{input}' (with U+2044) should canonicalize to t½");
            Assert.AreEqual("t½", canon);
        }

        /// <summary>
        /// R4 — U+2215 DIVISION SLASH also folds to ASCII <c>/</c>.
        /// </summary>
        [TestMethod]
        public void R4_NormalizeUnicode_DivisionSlash_FoldsToForwardSlash()
        {
            // U+2215 DIVISION SLASH
            var input = "t1\u22152";
            var normalized = PkParameterDictionary.NormalizeUnicode(input);
            Assert.IsTrue(normalized.Contains('/'),
                $"DIVISION SLASH (U+2215) must fold to '/', got: '{normalized}'");
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon));
            Assert.AreEqual("t½", canon);
        }

        /// <summary>
        /// R4 — Biological-matrix prefixes (Serum, Plasma, Blood) are stripped
        /// at lookup time so matrix-prefixed variants resolve to the bare
        /// canonical. Observed in TID 569 (thyroid hormone) shape.
        /// </summary>
        [TestMethod]
        [DataRow("Serum T1/2", "t½")]
        [DataRow("Plasma T1/2", "t½")]
        [DataRow("Blood T1/2", "t½")]
        [DataRow("Whole Blood Cmax", "Cmax")]
        [DataRow("Urine AUC", "AUC")]
        public void R4_TryCanonicalize_MatrixPrefixStripped_ResolvesCanonical(
            string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"'{input}' should canonicalize after matrix-prefix strip");
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// R4 guard: a matrix-only input (no PK term after the prefix) must NOT
        /// yield a canonical match. Prevents "Serum" alone from accidentally
        /// canonicalizing to anything.
        /// </summary>
        [TestMethod]
        [DataRow("Serum")]
        [DataRow("Plasma")]
        [DataRow("Blood")]
        public void R4_TryCanonicalize_BareMatrix_DoesNotMatch(string input)
        {
            Assert.IsFalse(PkParameterDictionary.TryCanonicalize(input, out _),
                $"bare '{input}' must not canonicalize");
        }

        #endregion Wave 2 R4 — Unicode Variant Folding + Matrix Prefix

        #region Wave 2 R5 — New PK Aliases

        /// <summary>
        /// R5 — Renal clearance shorthand forms (CLREN, CLcr, CLCR) canonicalize
        /// to CLr. Observed ~80 rows in the 2026-04-21 audit with these forms
        /// in Subtype. Creatinine Clearance as a term also maps here.
        /// </summary>
        [TestMethod]
        [DataRow("CLREN", "CLr")]
        [DataRow("CLren", "CLr")]
        [DataRow("CLcr", "CLr")]
        [DataRow("CLCR", "CLr")]
        [DataRow("Creatinine Clearance", "CLr")]
        [DataRow("Renal Clearance", "CLr")]
        public void R5_TryCanonicalize_RenalClearanceVariants_MapToCLr(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"'{input}' should canonicalize");
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// R5 — Numeric-suffix period variants for Cmax/Tmax (period 1/2 dosing)
        /// and "Peak Conc." shorthand. Observed 59 rows in the audit with
        /// "Peak Conc." in Name.
        /// </summary>
        [TestMethod]
        [DataRow("Cmax1", "Cmax")]
        [DataRow("Cmax2", "Cmax")]
        [DataRow("Peak Conc.", "Cmax")]
        [DataRow("Peak Conc", "Cmax")]
        [DataRow("Tmax1", "Tmax")]
        [DataRow("Tmax2", "Tmax")]
        public void R5_TryCanonicalize_NumericSuffixAndPeakConc_MapToCanonical(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"'{input}' should canonicalize to {expected}");
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// R5 — <c>Cminip</c> (interpeak minimum concentration) maps to Cmin.
        /// </summary>
        [TestMethod]
        [DataRow("Cminip", "Cmin")]
        [DataRow("Cmin,ip", "Cmin")]
        [DataRow("Cmin ip", "Cmin")]
        public void R5_TryCanonicalize_Cminip_MapsToCmin(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon));
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// R5 — Timepoint-indexed concentrations (C12, C24, C48, C72, C96) all
        /// resolve to Ctrough. These are "concentration at hour N" measurements
        /// in steady-state PK tables; semantically trough-like.
        /// </summary>
        [TestMethod]
        [DataRow("C12", "Ctrough")]
        [DataRow("C24", "Ctrough")]
        [DataRow("C48", "Ctrough")]
        [DataRow("C72", "Ctrough")]
        [DataRow("C96", "Ctrough")]
        [DataRow("C12h", "Ctrough")]
        [DataRow("C24h", "Ctrough")]
        public void R5_TryCanonicalize_TimepointConcentrations_MapToCtrough(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"'{input}' should canonicalize to Ctrough");
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// R5 — <c>Vdss</c> canonicalizes to <c>Vss</c>. The "d" in Vdss stands
        /// for "distribution" which is redundant with "volume of distribution".
        /// Observed 36 rows in the audit with Vdss in Name.
        /// </summary>
        [TestMethod]
        [DataRow("Vdss", "Vss")]
        [DataRow("Vd,ss", "Vss")]
        [DataRow("Vd ss", "Vss")]
        public void R5_TryCanonicalize_Vdss_MapsToVss(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon));
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// R5 — New <c>tlag</c> canonical (absorption lag time). Distinct from
        /// Tmax (time-to-peak) — tlag is the delay before drug appears.
        /// </summary>
        [TestMethod]
        [DataRow("tlag", "tlag")]
        [DataRow("tLag", "tlag")]
        [DataRow("Tlag", "tlag")]
        [DataRow("t lag", "tlag")]
        [DataRow("t-lag", "tlag")]
        [DataRow("Lag Time", "tlag")]
        [DataRow("Absorption Lag Time", "tlag")]
        public void R5_TryCanonicalize_TlagVariants_MapToTlag(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"'{input}' should canonicalize to tlag");
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// R5 — <c>AUCtldc</c> (AUC to last detectable concentration) resolves
        /// to AUClast.
        /// </summary>
        [TestMethod]
        [DataRow("AUCtldc", "AUClast")]
        [DataRow("AUC_tldc", "AUClast")]
        [DataRow("AUC to last detectable", "AUClast")]
        public void R5_TryCanonicalize_AUCtldc_MapsToAUClast(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon));
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// R5 — <c>AUC0-Nd</c> day-interval variants (e.g., AUC0-180d) collapse
        /// to the generic AUC canonical. Observed in long-duration PK tables.
        /// </summary>
        [TestMethod]
        [DataRow("AUC0-180d", "AUC")]
        [DataRow("AUC0-90d", "AUC")]
        public void R5_TryCanonicalize_AUCDayInterval_CollapsesToAUC(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"'{input}' should canonicalize to generic AUC");
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// R5 — Distribution / terminal half-life variants. Phase-tagged forms
        /// fold to the same t½ canonical; the phase qualifier is captured
        /// separately by <see cref="PkParameterDictionary.TryExtractCanonicalFromPhrase"/>.
        /// </summary>
        [TestMethod]
        [DataRow("t1/2terminal", "t½")]
        [DataRow("t1/2 terminal", "t½")]
        [DataRow("t1/2λz", "t½")]
        [DataRow("Distribution Half-life", "t½")]
        public void R5_TryCanonicalize_PhaseTaggedHalfLife_MapsToTHalf(string input, string expected)
        {
            Assert.IsTrue(PkParameterDictionary.TryCanonicalize(input, out var canon),
                $"'{input}' should canonicalize to t½");
            Assert.AreEqual(expected, canon);
        }

        /// <summary>
        /// R5 — IsPkParameter recognizes all R5-added aliases. Confirms the
        /// alias index is wired for detection (not just canonicalization).
        /// </summary>
        [TestMethod]
        [DataRow("CLREN")]
        [DataRow("Peak Conc.")]
        [DataRow("Cminip")]
        [DataRow("C24")]
        [DataRow("Vdss")]
        [DataRow("tlag")]
        [DataRow("AUCtldc")]
        public void R5_IsPkParameter_RecognizesNewAliases(string input)
        {
            Assert.IsTrue(PkParameterDictionary.IsPkParameter(input),
                $"IsPkParameter should recognize '{input}'");
        }

        #endregion Wave 2 R5 — New PK Aliases
    }
}
