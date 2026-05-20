using MedRecProImportClass.Service.TransformationServices.SampleSize;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for centralized sample-size syntax recognition.
    /// </summary>
    /// <remarks>
    /// These tests keep denominator shape parsing characterized while parser call
    /// sites migrate away from local regular-expression ownership.
    /// </remarks>
    /// <seealso cref="SampleSizeParser"/>
    /// <seealso cref="SampleSizeEvidence"/>
    [TestClass]
    public class SampleSizeParserTests
    {
        /**************************************************************/
        /// <summary>
        /// Arm header N annotations return exact evidence and cleaned arm text.
        /// </summary>
        /// <param name="text">Header text to parse.</param>
        /// <param name="expectedArm">Expected cleaned arm candidate.</param>
        /// <param name="expectedN">Expected exact sample size.</param>
        /// <param name="expectedHint">Expected format hint.</param>
        [DataTestMethod]
        [DataRow("EVISTA(N=2557)n(%)", "EVISTA", 2557, "n(%)")]
        [DataRow("Placebo n = 51 %", "Placebo", 51, "%")]
        [DataRow("Sham (N=94 Eyes)", "Sham", 94, null)]
        [DataRow("4 mg n=172", "4 mg", 172, null)]
        [DataRow("CE (n = 5,310)", "CE", 5310, null)]
        public void SampleSizeParser_ArmHeaderN_ReturnsEvidenceAndCleanedArmText(
            string text,
            string expectedArm,
            int expectedN,
            string? expectedHint)
        {
            #region implementation

            var matched = SampleSizeParser.TryParseArmHeaderSampleSize(
                text,
                out var evidence,
                out var formatHint);

            Assert.IsTrue(matched);
            Assert.IsTrue(evidence.IsExact);
            Assert.AreEqual(expectedN, evidence.Value);
            Assert.AreEqual(expectedArm, evidence.CleanedText);
            Assert.AreEqual(expectedHint, formatHint);
            Assert.AreEqual(SampleSizeSourceKind.ArmHeader, evidence.SourceKind);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Standalone N cells with display hints are exact body metadata evidence.
        /// </summary>
        [TestMethod]
        public void SampleSizeParser_StandaloneNCell_ReturnsBodyMetadataEvidence()
        {
            #region implementation

            var matched = SampleSizeParser.TryParseStandaloneSampleSizeCell("n = 51 %", out var evidence);

            Assert.IsTrue(matched);
            Assert.AreEqual(51, evidence.Value);
            Assert.AreEqual(SampleSizeSourceKind.BodyMetadataRow, evidence.SourceKind);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Header-tier arm text with an N value is column-scoped evidence.
        /// </summary>
        [TestMethod]
        public void SampleSizeParser_HeaderTierN_ReturnsColumnScopedEvidence()
        {
            #region implementation

            var matched = SampleSizeParser.TryParseHeaderTierSampleSize("10 mg n = 102 %", out var evidence);

            Assert.IsTrue(matched);
            Assert.AreEqual(102, evidence.Value);
            Assert.AreEqual("10 mg", evidence.CleanedText);
            Assert.AreEqual("%", evidence.FormatHint);
            Assert.AreEqual(SampleSizeSourceKind.HeaderTier, evidence.SourceKind);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Fraction cells expose the explicit denominator as exact evidence.
        /// </summary>
        /// <param name="text">Fraction text.</param>
        /// <param name="expectedN">Expected denominator.</param>
        [DataTestMethod]
        [DataRow("2/103 (4%)", 103)]
        [DataRow("0/95", 95)]
        [DataRow("63/113 (56%)", 113)]
        public void SampleSizeParser_FractionValue_UsesExplicitDenominator(string text, int expectedN)
        {
            #region implementation

            var matched = SampleSizeParser.TryParseFractionDenominator(text, out var evidence);

            Assert.IsTrue(matched);
            Assert.AreEqual(expectedN, evidence.Value);
            Assert.AreEqual(SampleSizeSourceKind.FractionDenominator, evidence.SourceKind);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Range-only denominator text is inexact and audit-only.
        /// </summary>
        [TestMethod]
        public void SampleSizeParser_RangeOnly_ReturnsInexactEvidence()
        {
            #region implementation

            var matched = SampleSizeParser.TryParseRangeOnlySampleSize("85-144", out var evidence);

            Assert.IsTrue(matched);
            Assert.IsFalse(evidence.IsExact);
            Assert.IsNull(evidence.Value);
            Assert.AreEqual(SampleSizeParser.RangeOnlyDiagnostic, evidence.DiagnosticCode);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Lone percentages are not denominator evidence.
        /// </summary>
        [TestMethod]
        public void SampleSizeParser_LonePercentage_ReturnsNoEvidence()
        {
            #region implementation

            Assert.IsFalse(SampleSizeParser.TryParseStandaloneSampleSizeCell("51%", out _));
            Assert.IsFalse(SampleSizeParser.TryParseFractionDenominator("51%", out _));
            Assert.IsFalse(SampleSizeParser.TryParseArmHeaderSampleSize("51%", out _));

            #endregion
        }
    }
}
