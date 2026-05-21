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
        /// Arm header N annotations accept only known trailing display/context tokens.
        /// </summary>
        /// <param name="text">Header text to parse.</param>
        /// <param name="expectedArm">Expected cleaned arm candidate.</param>
        /// <param name="expectedN">Expected exact sample size.</param>
        /// <param name="expectedHint">Expected trailing context or format hint.</param>
        [DataTestMethod]
        [DataRow("Metoprolol succinate extended-release tablets n=1990 % of patients", "Metoprolol succinate extended-release tablets", 1990, "% of patients")]
        [DataRow("Budesonide Delayed-Release Capsules 9 mg n=520 Number (%)", "Budesonide Delayed-Release Capsules 9 mg", 520, "Number (%)")]
        [DataRow("Exemestane N=73 (% incidence)", "Exemestane", 73, "(% incidence)")]
        [DataRow("Placebo N=294 n (EAIR)", "Placebo", 294, "n (EAIR)")]
        [DataRow("Xermelo 250 mg Three Times Daily, N=45 (%)", "Xermelo 250 mg Three Times Daily", 45, "(%)")]
        [DataRow("Tenofovir Disoproxil Fumarate N=368 (Week 0\\u201324)", "Tenofovir Disoproxil Fumarate", 368, "(Week 0\\u201324)")]
        [DataRow("FINACEA Gel, 15% N=457 (100%)", "FINACEA Gel, 15%", 457, "(100%)")]
        public void SampleSizeParser_ArmHeaderN_AcceptsKnownTrailingContextTokens(
            string text,
            string expectedArm,
            int expectedN,
            string expectedHint)
        {
            #region implementation

            var normalizedText = text.Replace("\\u2013", "\u2013", StringComparison.Ordinal);
            var normalizedHint = expectedHint.Replace("\\u2013", "\u2013", StringComparison.Ordinal);
            var matched = SampleSizeParser.TryParseArmHeaderSampleSize(
                normalizedText,
                out var evidence,
                out var formatHint);

            Assert.IsTrue(matched);
            Assert.AreEqual(expectedN, evidence.Value);
            Assert.AreEqual(expectedArm, evidence.CleanedText);
            Assert.AreEqual(normalizedHint, formatHint);

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
        /// Whitespace adjacent to comma thousands separators is normalized before parsing.
        /// </summary>
        [TestMethod]
        public void SampleSizeParser_SpacedCommaThousands_NormalizesOnlyCommaAdjacentWhitespace()
        {
            #region implementation

            var matched = SampleSizeParser.TryParseStandaloneSampleSizeCell("n =1 , 142", out var evidence);

            Assert.IsTrue(matched);
            Assert.AreEqual(1142, evidence.Value);
            Assert.IsFalse(SampleSizeParser.TryParseStandaloneSampleSizeCell("n=1 142", out _));
            Assert.IsFalse(SampleSizeParser.TryParseArmHeaderSampleSize("Drug n=1 142", out _));
            Assert.IsFalse(SampleSizeParser.TryParseArmHeaderSampleSize("QuilliChew ER N= 4 2 n (%)", out _));
            Assert.IsFalse(SampleSizeParser.TryParseArmHeaderSampleSize("CBX N=414 6", out _));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Plain denominator cells with percent-format echoes are accepted only in explicit N-row context.
        /// </summary>
        [TestMethod]
        public void SampleSizeParser_NRowDenominatorCell_AcceptsFormatEchoWithoutWideningStandaloneN()
        {
            #region implementation

            var matched = SampleSizeParser.TryParseNRowDenominatorCell("425 (%)", out var evidence);

            Assert.IsTrue(matched);
            Assert.AreEqual(425, evidence.Value);
            Assert.AreEqual(SampleSizeSourceKind.BodyMetadataRow, evidence.SourceKind);
            Assert.IsFalse(SampleSizeParser.TryParseStandaloneSampleSizeCell("425 (%)", out _));

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

        /**************************************************************/
        /// <summary>
        /// Count-percent consensus requires at least three non-zero rows and a single rounded denominator.
        /// </summary>
        [TestMethod]
        public void SampleSizeParser_ColumnConsensusInference_RequiresThreeRowsAndUniqueDenominator()
        {
            #region implementation

            var exact = SampleSizeParser.TryInferColumnConsensusSampleSize(
                new List<(int count, decimal percent)>
                {
                    (10, 10.0m),
                    (20, 20.0m),
                    (30, 30.0m)
                },
                out var exactEvidence);

            Assert.IsTrue(exact);
            Assert.IsTrue(exactEvidence.IsExact);
            Assert.AreEqual(100, exactEvidence.Value);
            Assert.AreEqual(SampleSizeSourceKind.CountPercentInference, exactEvidence.SourceKind);

            Assert.IsFalse(SampleSizeParser.TryInferColumnConsensusSampleSize(
                new List<(int count, decimal percent)> { (10, 10.0m), (20, 20.0m) },
                out _));
            Assert.IsFalse(SampleSizeParser.TryInferColumnConsensusSampleSize(
                new List<(int count, decimal percent)> { (0, 10.0m), (0, 20.0m), (0, 30.0m) },
                out _));

            #endregion
        }
    }
}
