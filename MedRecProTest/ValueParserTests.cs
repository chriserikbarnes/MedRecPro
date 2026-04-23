using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="ValueParser"/> (Stage 3 of the SPL Table Normalization pipeline).
    /// </summary>
    /// <remarks>
    /// Tests cover all 13 regex patterns in priority order, arm header parsing,
    /// parameter name cleaning, and PCT_CHECK validation logic.
    ///
    /// No database or mocking needed — ValueParser is a static utility operating on strings.
    /// Internal helpers are tested directly via InternalsVisibleTo.
    /// </remarks>
    /// <seealso cref="ValueParser"/>
    /// <seealso cref="ParsedValue"/>
    /// <seealso cref="ArmDefinition"/>
    [TestClass]
    public class ValueParserTests
    {
        #region Empty / NA Tests

        /**************************************************************/
        /// <summary>
        /// Null input returns IsExcluded with empty_or_na rule.
        /// </summary>
        [TestMethod]
        public void Parse_NullInput_ReturnsExcluded()
        {
            var result = ValueParser.Parse(null);
            Assert.IsTrue(result.IsExcluded);
            Assert.AreEqual("empty_or_na", result.ParseRule);
            Assert.AreEqual(0.8, result.ParseConfidence);
        }

        /**************************************************************/
        /// <summary>
        /// Empty string returns IsExcluded.
        /// </summary>
        [TestMethod]
        public void Parse_EmptyString_ReturnsExcluded()
        {
            var result = ValueParser.Parse("");
            Assert.IsTrue(result.IsExcluded);
            Assert.AreEqual("empty_or_na", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// Whitespace-only returns IsExcluded.
        /// </summary>
        [TestMethod]
        public void Parse_WhitespaceOnly_ReturnsExcluded()
        {
            var result = ValueParser.Parse("   ");
            Assert.IsTrue(result.IsExcluded);
        }

        /**************************************************************/
        /// <summary>
        /// "NA" returns IsExcluded.
        /// </summary>
        [TestMethod]
        public void Parse_NA_ReturnsExcluded()
        {
            var result = ValueParser.Parse("NA");
            Assert.IsTrue(result.IsExcluded);
            Assert.AreEqual("empty_or_na", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// "N/A" returns IsExcluded.
        /// </summary>
        [TestMethod]
        public void Parse_NSlashA_ReturnsExcluded()
        {
            var result = ValueParser.Parse("N/A");
            Assert.IsTrue(result.IsExcluded);
        }

        /**************************************************************/
        /// <summary>
        /// Dash "--" returns IsExcluded.
        /// </summary>
        [TestMethod]
        public void Parse_DoubleDash_ReturnsExcluded()
        {
            var result = ValueParser.Parse("--");
            Assert.IsTrue(result.IsExcluded);
        }

        /**************************************************************/
        /// <summary>
        /// Em dash "—" returns IsExcluded.
        /// </summary>
        [TestMethod]
        public void Parse_EmDash_ReturnsExcluded()
        {
            var result = ValueParser.Parse("—");
            Assert.IsTrue(result.IsExcluded);
        }

        #endregion Empty / NA Tests

        #region Coded Exclusion Tests

        /**************************************************************/
        /// <summary>
        /// Single uppercase letter "A" returns CodedExclusion.
        /// </summary>
        [TestMethod]
        public void Parse_SingleUpperLetter_ReturnsCodedExclusion()
        {
            var result = ValueParser.Parse("A");
            Assert.AreEqual("CodedExclusion", result.PrimaryValueType);
            Assert.AreEqual("A", result.TextValue);
            Assert.IsTrue(result.IsExcluded);
            Assert.AreEqual(1.0, result.ParseConfidence);
            Assert.AreEqual("letter_code", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// Single letter "B" returns CodedExclusion.
        /// </summary>
        [TestMethod]
        public void Parse_SingleUpperLetterB_ReturnsCodedExclusion()
        {
            var result = ValueParser.Parse("B");
            Assert.AreEqual("CodedExclusion", result.PrimaryValueType);
            Assert.AreEqual("B", result.TextValue);
        }

        #endregion Coded Exclusion Tests

        #region Fraction Percent Tests

        /**************************************************************/
        /// <summary>
        /// "239/347 (69%)" returns Percentage with count and PCT_CHECK.
        /// </summary>
        [TestMethod]
        public void Parse_FractionPercent_ReturnsPercentageWithCount()
        {
            var result = ValueParser.Parse("239/347 (69%)");
            Assert.AreEqual(69.0, result.PrimaryValue);
            Assert.AreEqual("Percentage", result.PrimaryValueType);
            Assert.AreEqual(239.0, result.SecondaryValue);
            Assert.AreEqual("Count", result.SecondaryValueType);
            Assert.AreEqual("%", result.Unit);
            Assert.AreEqual(1.0, result.ParseConfidence);
            Assert.AreEqual("frac_pct", result.ParseRule);
            Assert.IsNotNull(result.ValidationFlags);
            Assert.IsTrue(result.ValidationFlags!.StartsWith("PCT_CHECK:"));
        }

        /**************************************************************/
        /// <summary>
        /// "15/188(8.0%)" without space returns correct parse.
        /// </summary>
        [TestMethod]
        public void Parse_FractionPercentNoSpace_ReturnsCorrect()
        {
            var result = ValueParser.Parse("15/188(8.0%)");
            Assert.AreEqual(8.0, result.PrimaryValue);
            Assert.AreEqual("Percentage", result.PrimaryValueType);
            Assert.AreEqual(15.0, result.SecondaryValue);
        }

        #endregion Fraction Percent Tests

        #region N Percent Tests

        /**************************************************************/
        /// <summary>
        /// "33 (17.6)" returns Percentage=17.6 with Count=33.
        /// </summary>
        [TestMethod]
        public void Parse_NPercent_ReturnsPercentageWithCount()
        {
            var result = ValueParser.Parse("33 (17.6)");
            Assert.AreEqual(17.6, result.PrimaryValue);
            Assert.AreEqual("Percentage", result.PrimaryValueType);
            Assert.AreEqual(33.0, result.SecondaryValue);
            Assert.AreEqual("Count", result.SecondaryValueType);
            Assert.AreEqual("%", result.Unit);
            Assert.AreEqual("n_pct", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// "15 (8.0%)" with % sign returns correct parse.
        /// </summary>
        [TestMethod]
        public void Parse_NPercentWithSign_ReturnsCorrect()
        {
            var result = ValueParser.Parse("15 (8.0%)");
            Assert.AreEqual(8.0, result.PrimaryValue);
            Assert.AreEqual(15.0, result.SecondaryValue);
        }

        /**************************************************************/
        /// <summary>
        /// n(%) with armN performs PCT_CHECK validation — PASS case.
        /// </summary>
        [TestMethod]
        public void Parse_NPercent_WithArmN_PassesPctCheck()
        {
            // 33/188*100 = 17.55%, reported 17.6% — within 1.5pp
            var result = ValueParser.Parse("33 (17.6)", armN: 188);
            Assert.IsNotNull(result.ValidationFlags);
            Assert.IsTrue(result.ValidationFlags!.Contains("PCT_CHECK:PASS"));
        }

        /**************************************************************/
        /// <summary>
        /// n(%) with mismatched armN performs PCT_CHECK validation — WARN case.
        /// </summary>
        [TestMethod]
        public void Parse_NPercent_WithWrongArmN_WarnsPctCheck()
        {
            // 33/50*100 = 66%, reported 17.6% — way off
            var result = ValueParser.Parse("33 (17.6)", armN: 50);
            Assert.IsNotNull(result.ValidationFlags);
            Assert.IsTrue(result.ValidationFlags!.Contains("PCT_CHECK:WARN"));
        }

        #endregion N Percent Tests

        #region RR with CI Tests

        /**************************************************************/
        /// <summary>
        /// "55%(29%, 71%)" returns RelativeRiskReduction with CI bounds.
        /// </summary>
        [TestMethod]
        public void Parse_RRWithCI_ReturnsRelativeRiskReduction()
        {
            var result = ValueParser.Parse("55%(29%, 71%)");
            Assert.AreEqual(55.0, result.PrimaryValue);
            Assert.AreEqual("RelativeRiskReduction", result.PrimaryValueType);
            Assert.AreEqual(29.0, result.LowerBound);
            Assert.AreEqual(71.0, result.UpperBound);
            Assert.AreEqual("95CI", result.BoundType);
            Assert.AreEqual("rr_ci", result.ParseRule);
        }

        #endregion RR with CI Tests

        #region Diff with CI Tests

        /**************************************************************/
        /// <summary>
        /// "-4.4(-12.6, 3.8)" returns RiskDifference with CI bounds.
        /// </summary>
        [TestMethod]
        public void Parse_DiffWithCI_ReturnsRiskDifference()
        {
            var result = ValueParser.Parse("-4.4(-12.6, 3.8)");
            Assert.AreEqual(-4.4, result.PrimaryValue);
            Assert.AreEqual("RiskDifference", result.PrimaryValueType);
            Assert.AreEqual(-12.6, result.LowerBound);
            Assert.AreEqual(3.8, result.UpperBound);
            Assert.AreEqual("95CI", result.BoundType);
            Assert.AreEqual("diff_ci", result.ParseRule);
        }

        #endregion Diff with CI Tests

        #region Value CI Tests

        /**************************************************************/
        /// <summary>
        /// "0.38 (0.31 - 0.46)" returns Numeric with CI bounds (dash-separated).
        /// </summary>
        [TestMethod]
        public void Parse_ValueCI_DashFormat_ParsesCorrectly()
        {
            var result = ValueParser.Parse("0.38 (0.31 - 0.46)");
            Assert.AreEqual(0.38, result.PrimaryValue);
            Assert.AreEqual("Numeric", result.PrimaryValueType);
            Assert.AreEqual(0.31, result.LowerBound);
            Assert.AreEqual(0.46, result.UpperBound);
            Assert.AreEqual("CI", result.BoundType);
            Assert.AreEqual("value_ci", result.ParseRule);
            Assert.AreEqual(0.95, result.ParseConfidence);
        }

        /**************************************************************/
        /// <summary>
        /// "1.23(0.95-1.55)" with no spaces still parses correctly.
        /// </summary>
        [TestMethod]
        public void Parse_ValueCI_NoSpaces_ParsesCorrectly()
        {
            var result = ValueParser.Parse("1.23(0.95-1.55)");
            Assert.AreEqual(1.23, result.PrimaryValue);
            Assert.AreEqual(0.95, result.LowerBound);
            Assert.AreEqual(1.55, result.UpperBound);
            Assert.AreEqual("CI", result.BoundType);
            Assert.AreEqual("value_ci", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// "0.94 (0.86–1.03)" with en-dash separator parses correctly.
        /// </summary>
        [TestMethod]
        public void Parse_ValueCI_EnDash_ParsesCorrectly()
        {
            var result = ValueParser.Parse("0.94 (0.86\u20131.03)"); // \u2013 = en-dash
            Assert.AreEqual(0.94, result.PrimaryValue);
            Assert.AreEqual(0.86, result.LowerBound);
            Assert.AreEqual(1.03, result.UpperBound);
            Assert.AreEqual("CI", result.BoundType);
        }

        /**************************************************************/
        /// <summary>
        /// "0.99 (0.91 to 1.08)" with "to" separator parses correctly.
        /// Common in drug interaction PK tables (Geometric Mean Ratio with 90% CI).
        /// </summary>
        [TestMethod]
        public void Parse_ValueCI_ToSeparator_ParsesCorrectly()
        {
            var result = ValueParser.Parse("0.99 (0.91 to 1.08)");
            Assert.AreEqual(0.99, result.PrimaryValue);
            Assert.AreEqual("Numeric", result.PrimaryValueType);
            Assert.AreEqual(0.91, result.LowerBound);
            Assert.AreEqual(1.08, result.UpperBound);
            Assert.AreEqual("CI", result.BoundType);
            Assert.AreEqual("value_ci", result.ParseRule);
            Assert.AreEqual(0.95, result.ParseConfidence);
        }

        /**************************************************************/
        /// <summary>
        /// "1.66(1.53 to 1.81)" with "to" separator and no spaces before parens.
        /// </summary>
        [TestMethod]
        public void Parse_ValueCI_ToSeparator_NoLeadingSpace_ParsesCorrectly()
        {
            var result = ValueParser.Parse("1.66(1.53 to 1.81)");
            Assert.AreEqual(1.66, result.PrimaryValue);
            Assert.AreEqual(1.53, result.LowerBound);
            Assert.AreEqual(1.81, result.UpperBound);
            Assert.AreEqual("value_ci", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// "-2.5 (-4.1 - -0.9)" with negative primary and bounds parses correctly.
        /// </summary>
        [TestMethod]
        public void Parse_ValueCI_NegativeValues_ParsesCorrectly()
        {
            var result = ValueParser.Parse("-2.5 (-4.1 - -0.9)");
            Assert.AreEqual(-2.5, result.PrimaryValue);
            Assert.AreEqual(-4.1, result.LowerBound);
            Assert.AreEqual(-0.9, result.UpperBound);
            Assert.AreEqual("CI", result.BoundType);
        }

        /**************************************************************/
        /// <summary>
        /// "0.38 (0.46 - 0.31)" with lower > upper is rejected (falls to text).
        /// </summary>
        [TestMethod]
        public void Parse_ValueCI_InvalidBoundsOrder_FallsToText()
        {
            var result = ValueParser.Parse("0.38 (0.46 - 0.31)");
            Assert.AreEqual("Text", result.PrimaryValueType);
            Assert.AreEqual("text_descriptive", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// CI pattern does not steal from existing comma-based patterns.
        /// "-4.4(-12.6, 3.8)" still matches diff_ci, not value_ci.
        /// </summary>
        [TestMethod]
        public void Parse_ValueCI_CommaFormat_StillMatchesDiffCI()
        {
            var result = ValueParser.Parse("-4.4(-12.6, 3.8)");
            Assert.AreEqual("diff_ci", result.ParseRule);
            Assert.AreEqual("95CI", result.BoundType);
        }

        /**************************************************************/
        /// <summary>
        /// "to" separator does not interfere with range pattern.
        /// "10.7 to 273" still matches range, not value_ci (no parens).
        /// </summary>
        [TestMethod]
        public void Parse_ValueCI_ToSeparator_DoesNotStealFromRange()
        {
            var result = ValueParser.Parse("10.7 to 273");
            Assert.AreEqual("Range", result.BoundType);
            Assert.AreEqual(10.7, result.LowerBound);
            Assert.AreEqual(273.0, result.UpperBound);
        }

        #endregion Value CI Tests

        #region Value PlusMinus Tests

        /**************************************************************/
        /// <summary>
        /// "1.1 ± 0.5" returns Numeric with SD bounds.
        /// </summary>
        [TestMethod]
        public void Parse_ValuePlusMinus_Standard_ParsesCorrectly()
        {
            var result = ValueParser.Parse("1.1 ± 0.5");
            Assert.AreEqual(1.1, result.PrimaryValue);
            Assert.AreEqual("Numeric", result.PrimaryValueType);
            Assert.AreEqual(0.5, result.SecondaryValue);
            Assert.AreEqual("SD", result.SecondaryValueType);
            Assert.AreEqual(0.6, result.LowerBound!.Value, 0.001);
            Assert.AreEqual(1.6, result.UpperBound!.Value, 0.001);
            Assert.AreEqual("SD", result.BoundType);
            Assert.AreEqual("value_plusminus", result.ParseRule);
            Assert.AreEqual(0.95, result.ParseConfidence);
        }

        /**************************************************************/
        /// <summary>
        /// "580±450" with no spaces still parses correctly.
        /// </summary>
        [TestMethod]
        public void Parse_ValuePlusMinus_NoSpaces_ParsesCorrectly()
        {
            var result = ValueParser.Parse("580±450");
            Assert.AreEqual(580.0, result.PrimaryValue);
            Assert.AreEqual(450.0, result.SecondaryValue);
            Assert.AreEqual(130.0, result.LowerBound!.Value, 0.001);
            Assert.AreEqual(1030.0, result.UpperBound!.Value, 0.001);
            Assert.AreEqual("value_plusminus", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// "71 +/- 40" with +/- notation parses correctly.
        /// </summary>
        [TestMethod]
        public void Parse_ValuePlusMinus_PlusSlashMinus_ParsesCorrectly()
        {
            var result = ValueParser.Parse("71 +/- 40");
            Assert.AreEqual(71.0, result.PrimaryValue);
            Assert.AreEqual(40.0, result.SecondaryValue);
            Assert.AreEqual(31.0, result.LowerBound!.Value, 0.001);
            Assert.AreEqual(111.0, result.UpperBound!.Value, 0.001);
        }

        /**************************************************************/
        /// <summary>
        /// "-2.5 ± 1.0" with negative primary parses correctly.
        /// </summary>
        [TestMethod]
        public void Parse_ValuePlusMinus_NegativePrimary_ParsesCorrectly()
        {
            var result = ValueParser.Parse("-2.5 ± 1.0");
            Assert.AreEqual(-2.5, result.PrimaryValue);
            Assert.AreEqual(1.0, result.SecondaryValue);
            Assert.AreEqual(-3.5, result.LowerBound!.Value, 0.001);
            Assert.AreEqual(-1.5, result.UpperBound!.Value, 0.001);
        }

        /**************************************************************/
        /// <summary>
        /// "55 +- 18" with +- notation (no slash) parses correctly.
        /// </summary>
        [TestMethod]
        public void Parse_ValuePlusMinus_PlusMinus_NoSlash_ParsesCorrectly()
        {
            var result = ValueParser.Parse("55 +- 18");
            Assert.AreEqual(55.0, result.PrimaryValue);
            Assert.AreEqual(18.0, result.SecondaryValue);
            Assert.AreEqual("value_plusminus", result.ParseRule);
        }

        #endregion Value PlusMinus Tests

        #region Value CV Tests

        /**************************************************************/
        /// <summary>
        /// "0.29 (35%)" returns Mean with CV_Percent.
        /// </summary>
        [TestMethod]
        public void Parse_ValueCV_ReturnsMeanWithCV()
        {
            var result = ValueParser.Parse("0.29 (35%)");
            Assert.AreEqual(0.29, result.PrimaryValue);
            Assert.AreEqual("Mean", result.PrimaryValueType);
            Assert.AreEqual(35.0, result.SecondaryValue);
            Assert.AreEqual("CV_Percent", result.SecondaryValueType);
            Assert.AreEqual("value_cv", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// "125(32%)" without space matches n_pct first (pattern priority).
        /// Value(CV%) requires a decimal in primary or no % on inner: "0.29 (35%)" is value_cv.
        /// "125(32%)" is ambiguous — n_pct wins by priority order.
        /// </summary>
        [TestMethod]
        public void Parse_ValueCVNoSpace_MatchesNPctByPriority()
        {
            var result = ValueParser.Parse("125(32%)");
            Assert.AreEqual(32.0, result.PrimaryValue);
            Assert.AreEqual(125.0, result.SecondaryValue);
            Assert.AreEqual("n_pct", result.ParseRule);
        }

        #endregion Value CV Tests

        #region Range Tests

        /**************************************************************/
        /// <summary>
        /// "10.7 to 273" returns Range bounds.
        /// </summary>
        [TestMethod]
        public void Parse_Range_ReturnsBounds()
        {
            var result = ValueParser.Parse("10.7 to 273");
            Assert.AreEqual(10.7, result.LowerBound);
            Assert.AreEqual(273.0, result.UpperBound);
            Assert.AreEqual("Range", result.BoundType);
            Assert.AreEqual(0.9, result.ParseConfidence);
            Assert.AreEqual("range_to", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// R15.1 — Range now synthesizes <see cref="ParsedValue.PrimaryValue"/> as
        /// the arithmetic midpoint of the two bounds. Historically this was null,
        /// causing R13's PK analyzability filter and downstream analyses to treat
        /// range rows as unusable. The midpoint is a reasonable central-tendency
        /// proxy; consumers can still inspect LowerBound/UpperBound for the
        /// actual interval.
        /// </summary>
        [TestMethod]
        public void Parse_R15_1_Range_SynthesizesMidpointAsPrimaryValue()
        {
            var result = ValueParser.Parse("10.7 to 273");
            Assert.AreEqual((10.7 + 273.0) / 2.0, result.PrimaryValue,
                "PrimaryValue must equal the arithmetic midpoint of the range");
            Assert.AreEqual("Range", result.PrimaryValueType,
                "PrimaryValueType must signal that this is a synthesized range midpoint");
            // Bounds still populated for downstream consumers that want the interval
            Assert.AreEqual(10.7, result.LowerBound);
            Assert.AreEqual(273.0, result.UpperBound);
        }

        /**************************************************************/
        /// <summary>
        /// R15.1 — Small tight range (5 to 6) — midpoint = 5.5.
        /// </summary>
        [TestMethod]
        public void Parse_R15_1_Range_TightRange_MidpointCorrect()
        {
            var result = ValueParser.Parse("5 to 6");
            Assert.AreEqual(5.5, result.PrimaryValue);
            Assert.AreEqual("Range", result.PrimaryValueType);
        }

        /**************************************************************/
        /// <summary>
        /// R15.1 — Decimal bounds (0.58 to 1.45) — midpoint = 1.015.
        /// </summary>
        [TestMethod]
        public void Parse_R15_1_Range_DecimalBounds_MidpointCorrect()
        {
            var result = ValueParser.Parse("0.58 to 1.45");
            Assert.AreEqual(1.015, result.PrimaryValue!.Value, 1e-9);
            Assert.AreEqual("Range", result.PrimaryValueType);
        }

        #endregion Range Tests

        #region Standalone Percent Tests

        /**************************************************************/
        /// <summary>
        /// "8.5%" returns Percentage.
        /// </summary>
        [TestMethod]
        public void Parse_StandalonePercent_ReturnsPercentage()
        {
            var result = ValueParser.Parse("8.5%");
            Assert.AreEqual(8.5, result.PrimaryValue);
            Assert.AreEqual("Percentage", result.PrimaryValueType);
            Assert.AreEqual("%", result.Unit);
            Assert.AreEqual("percentage", result.ParseRule);
        }

        #endregion Standalone Percent Tests

        #region N Equals Tests

        /**************************************************************/
        /// <summary>
        /// "n=1401" returns SampleSize.
        /// </summary>
        [TestMethod]
        public void Parse_NEquals_ReturnsSampleSize()
        {
            var result = ValueParser.Parse("n=1401");
            Assert.AreEqual(1401.0, result.PrimaryValue);
            Assert.AreEqual("SampleSize", result.PrimaryValueType);
            Assert.AreEqual("n_equals", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// "N=188" uppercase returns SampleSize.
        /// </summary>
        [TestMethod]
        public void Parse_NEqualsUppercase_ReturnsSampleSize()
        {
            var result = ValueParser.Parse("N=188");
            Assert.AreEqual(188.0, result.PrimaryValue);
            Assert.AreEqual("SampleSize", result.PrimaryValueType);
        }

        #endregion N Equals Tests

        #region P-Value Tests

        /**************************************************************/
        /// <summary>
        /// "p&lt;0.05" returns PValue with qualifier.
        /// </summary>
        [TestMethod]
        public void Parse_PValueLessThan_ReturnsPValue()
        {
            var result = ValueParser.Parse("p<0.05");
            Assert.AreEqual(0.05, result.PrimaryValue);
            Assert.AreEqual("PValue", result.PrimaryValueType);
            Assert.AreEqual(0.05, result.PValue);
            Assert.AreEqual("<", result.PValueQualifier);
            Assert.AreEqual("pvalue", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// "P=0.001" returns PValue with equals qualifier.
        /// </summary>
        [TestMethod]
        public void Parse_PValueEquals_ReturnsPValue()
        {
            var result = ValueParser.Parse("P=0.001");
            Assert.AreEqual(0.001, result.PrimaryValue);
            Assert.AreEqual("=", result.PValueQualifier);
        }

        #endregion P-Value Tests

        #region Plain Number Tests

        /**************************************************************/
        /// <summary>
        /// "12.5" returns Numeric.
        /// </summary>
        [TestMethod]
        public void Parse_PlainNumber_ReturnsNumeric()
        {
            var result = ValueParser.Parse("12.5");
            Assert.AreEqual(12.5, result.PrimaryValue);
            Assert.AreEqual("Numeric", result.PrimaryValueType);
            Assert.AreEqual(0.9, result.ParseConfidence);
            Assert.AreEqual("plain_number", result.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// Negative number "-3.2" returns Numeric.
        /// </summary>
        [TestMethod]
        public void Parse_NegativeNumber_ReturnsNumeric()
        {
            var result = ValueParser.Parse("-3.2");
            Assert.AreEqual(-3.2, result.PrimaryValue);
            Assert.AreEqual("Numeric", result.PrimaryValueType);
        }

        /**************************************************************/
        /// <summary>
        /// Number with commas "1,234" returns Numeric.
        /// </summary>
        [TestMethod]
        public void Parse_NumberWithCommas_ReturnsNumeric()
        {
            var result = ValueParser.Parse("1,234");
            Assert.AreEqual(1234.0, result.PrimaryValue);
            Assert.AreEqual("Numeric", result.PrimaryValueType);
        }

        #endregion Plain Number Tests

        #region Text Fallback Tests

        /**************************************************************/
        /// <summary>
        /// "Diarrhea" returns Text type at low confidence.
        /// </summary>
        [TestMethod]
        public void Parse_TextContent_ReturnsText()
        {
            var result = ValueParser.Parse("Diarrhea");
            Assert.AreEqual("Text", result.PrimaryValueType);
            Assert.AreEqual("Diarrhea", result.TextValue);
            Assert.AreEqual(0.5, result.ParseConfidence);
            Assert.AreEqual("text_descriptive", result.ParseRule);
        }

        #endregion Text Fallback Tests

        #region Arm Header Parsing Tests

        /**************************************************************/
        /// <summary>
        /// Standard arm header "EVISTA(N=2557)n(%)" parses correctly.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_StandardFormat_ReturnsArmDefinition()
        {
            var arm = ValueParser.ParseArmHeader("EVISTA(N=2557)n(%)");
            Assert.IsNotNull(arm);
            Assert.AreEqual("EVISTA", arm!.Name);
            Assert.AreEqual(2557, arm.SampleSize);
            Assert.AreEqual("n(%)", arm.FormatHint);
        }

        /**************************************************************/
        /// <summary>
        /// Arm header with spaces "Drug A (N=188) %" parses correctly.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_WithSpaces_ReturnsArmDefinition()
        {
            var arm = ValueParser.ParseArmHeader("Drug A (N=188) %");
            Assert.IsNotNull(arm);
            Assert.AreEqual("Drug A", arm!.Name);
            Assert.AreEqual(188, arm.SampleSize);
            Assert.AreEqual("%", arm.FormatHint);
        }

        /**************************************************************/
        /// <summary>
        /// Non-arm header "Adverse Reaction" returns null.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_NoPattern_ReturnsNull()
        {
            var arm = ValueParser.ParseArmHeader("Adverse Reaction");
            Assert.IsNull(arm);
        }

        /**************************************************************/
        /// <summary>
        /// Null input returns null.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_NullInput_ReturnsNull()
        {
            var arm = ValueParser.ParseArmHeader(null);
            Assert.IsNull(arm);
        }

        /**************************************************************/
        /// <summary>
        /// Comma-formatted N in parenthesized arm header is parsed correctly.
        /// "CE (n = 5,310)" → SampleSize = 5310.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_CommaFormattedN_Parenthesized()
        {
            var arm = ValueParser.ParseArmHeader("CE (n = 5,310)");
            Assert.IsNotNull(arm);
            Assert.AreEqual("CE", arm!.Name);
            Assert.AreEqual(5310, arm.SampleSize);
        }

        /**************************************************************/
        /// <summary>
        /// Comma-formatted N in no-parentheses arm header is parsed correctly.
        /// "Placebo n = 5,429" → SampleSize = 5429.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_CommaFormattedN_NoParens()
        {
            var arm = ValueParser.ParseArmHeader("Placebo n = 5,429");
            Assert.IsNotNull(arm);
            Assert.AreEqual("Placebo", arm!.Name);
            Assert.AreEqual(5429, arm.SampleSize);
        }

        /**************************************************************/
        /// <summary>
        /// Large comma-formatted N in parenthesized header is parsed correctly.
        /// "Drug (N=12,345)" → SampleSize = 12345.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_LargeCommaFormattedN()
        {
            var arm = ValueParser.ParseArmHeader("Drug (N=12,345)");
            Assert.IsNotNull(arm);
            Assert.AreEqual("Drug", arm!.Name);
            Assert.AreEqual(12345, arm.SampleSize);
        }

        #endregion Arm Header Parsing Tests

        #region Parameter Name Cleaning Tests

        /**************************************************************/
        /// <summary>
        /// Parameter with footnote marker "Nausea†" strips marker.
        /// </summary>
        [TestMethod]
        public void CleanParameterName_WithFootnoteMarker_StripsMarker()
        {
            var (name, markers) = ValueParser.CleanParameterName("Nausea†");
            Assert.AreEqual("Nausea", name);
            Assert.AreEqual("†", markers);
        }

        /**************************************************************/
        /// <summary>
        /// Parameter without marker "Headache" passes through.
        /// </summary>
        [TestMethod]
        public void CleanParameterName_NoMarker_PassesThrough()
        {
            var (name, markers) = ValueParser.CleanParameterName("Headache");
            Assert.AreEqual("Headache", name);
            Assert.IsNull(markers);
        }

        /**************************************************************/
        /// <summary>
        /// Null input returns null.
        /// </summary>
        [TestMethod]
        public void CleanParameterName_NullInput_ReturnsNull()
        {
            var (name, markers) = ValueParser.CleanParameterName(null);
            Assert.IsNull(name);
            Assert.IsNull(markers);
        }

        #endregion Parameter Name Cleaning Tests

        #region R12 — Value Paren Dispersion + Trailing Unit

        /**************************************************************/
        /// <summary>
        /// R12 Pattern 4b — decimal leading value with parenthesized SD, no
        /// footnote. PrimaryValueType=Numeric (PK promotes to Mean), SecondaryValueType=null
        /// (resolved downstream from context).
        /// </summary>
        [TestMethod]
        public void Parse_R12_ValueParenDispersion_DecimalBasic()
        {
            var r = ValueParser.Parse("3.9 (1.9)");
            Assert.AreEqual("value_paren_dispersion", r.ParseRule);
            Assert.AreEqual(3.9, r.PrimaryValue);
            Assert.AreEqual("Numeric", r.PrimaryValueType);
            Assert.AreEqual(1.9, r.SecondaryValue);
            Assert.IsNull(r.SecondaryValueType);
            Assert.AreEqual(ParsedValue.ConfidenceTier.ValidatedMatch, r.ParseConfidence);
        }

        /**************************************************************/
        /// <summary>
        /// R12 Pattern 4b — trailing footnote markers are stripped from the
        /// secondary value without affecting the parse.
        /// </summary>
        [TestMethod]
        public void Parse_R12_ValueParenDispersion_TrailingFootnote()
        {
            var r = ValueParser.Parse("17.4 (6.2)*");
            Assert.AreEqual("value_paren_dispersion", r.ParseRule);
            Assert.AreEqual(17.4, r.PrimaryValue);
            Assert.AreEqual(6.2, r.SecondaryValue);
        }

        /**************************************************************/
        /// <summary>
        /// R12 Pattern 4b — small decimal values like 0.44 (0.22) parse correctly.
        /// </summary>
        [TestMethod]
        public void Parse_R12_ValueParenDispersion_SmallDecimal()
        {
            var r = ValueParser.Parse("0.44 (0.22)");
            Assert.AreEqual("value_paren_dispersion", r.ParseRule);
            Assert.AreEqual(0.44, r.PrimaryValue);
            Assert.AreEqual(0.22, r.SecondaryValue);
        }

        /**************************************************************/
        /// <summary>
        /// R12 Pattern 4b — negative leading value is supported for diff / change
        /// columns.
        /// </summary>
        [TestMethod]
        public void Parse_R12_ValueParenDispersion_NegativeLeading()
        {
            var r = ValueParser.Parse("-2.5 (0.8)");
            Assert.AreEqual("value_paren_dispersion", r.ParseRule);
            Assert.AreEqual(-2.5, r.PrimaryValue);
            Assert.AreEqual(0.8, r.SecondaryValue);
        }

        /**************************************************************/
        /// <summary>
        /// R12 guard — integer leading + % inside parens must route to Pattern 4
        /// (n_pct), NOT value_paren_dispersion. Pattern 4 wins because it runs
        /// earlier in the chain and 4b requires a decimal leading value.
        /// </summary>
        [TestMethod]
        public void Parse_R12_Guard_IntegerLeadingPercent_RoutesToNPct()
        {
            var r = ValueParser.Parse("33 (17.6%)");
            Assert.AreEqual("n_pct", r.ParseRule,
                "Integer leading + % must still route to n_pct, not value_paren_dispersion");
        }

        /**************************************************************/
        /// <summary>
        /// R12 guard — explicit ± must still route to Pattern 6c (value_plusminus),
        /// NOT value_paren_dispersion. 6c runs earlier in the chain and has no
        /// paren wrapping.
        /// </summary>
        [TestMethod]
        public void Parse_R12_Guard_PlusMinus_RoutesToValuePlusMinus()
        {
            var r = ValueParser.Parse("1.1 ± 0.5");
            Assert.AreEqual("value_plusminus", r.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// R12 guard — CV% shape (%, inside parens) must still route to Pattern 7
        /// (value_cv), NOT value_paren_dispersion.
        /// </summary>
        [TestMethod]
        public void Parse_R12_Guard_ValueCvPercent_RoutesToValueCv()
        {
            var r = ValueParser.Parse("0.29 (35%)");
            Assert.AreEqual("value_cv", r.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// R12 Pattern 12b — decimal with trailing time unit. "hr" normalizes to
        /// canonical "h" via <see cref="UnitDictionary.TryNormalize"/>.
        /// </summary>
        [TestMethod]
        public void Parse_R12_ValueTrailingUnit_DecimalHour()
        {
            var r = ValueParser.Parse("71.8 hr");
            Assert.AreEqual("value_trailing_unit", r.ParseRule);
            Assert.AreEqual(71.8, r.PrimaryValue);
            Assert.AreEqual("Numeric", r.PrimaryValueType);
            Assert.AreEqual("h", r.Unit, "'hr' must normalize to canonical 'h'");
            Assert.AreEqual(ParsedValue.ConfidenceTier.ValidatedMatch, r.ParseConfidence);
        }

        /**************************************************************/
        /// <summary>
        /// R12 Pattern 12b — decimal with trailing concentration unit mcg/mL.
        /// </summary>
        [TestMethod]
        public void Parse_R12_ValueTrailingUnit_DecimalConcentration()
        {
            var r = ValueParser.Parse("5.5 mcg/mL");
            Assert.AreEqual("value_trailing_unit", r.ParseRule);
            Assert.AreEqual(5.5, r.PrimaryValue);
            Assert.AreEqual("mcg/mL", r.Unit);
        }

        /**************************************************************/
        /// <summary>
        /// R12 Pattern 12b — integer leading with compound AUC-style unit.
        /// </summary>
        [TestMethod]
        public void Parse_R12_ValueTrailingUnit_IntegerCompoundAuc()
        {
            var r = ValueParser.Parse("1800 ng·h/mL");
            Assert.AreEqual("value_trailing_unit", r.ParseRule);
            Assert.AreEqual(1800, r.PrimaryValue);
            Assert.AreEqual("ng·h/mL", r.Unit);
        }

        /**************************************************************/
        /// <summary>
        /// R12 guard — unknown trailing word ("hello") must NOT match. Falls
        /// through to text_descriptive fallback.
        /// </summary>
        [TestMethod]
        public void Parse_R12_Guard_UnknownTrailingWord_FallsThroughToText()
        {
            var r = ValueParser.Parse("71.8 hello");
            Assert.AreEqual("text_descriptive", r.ParseRule,
                "Unknown unit words must fall through to text, not be parsed as a unit");
        }

        /**************************************************************/
        /// <summary>
        /// R12 guard — decimal-only value with no trailing token must route to
        /// Pattern 12 (plain_number), NOT value_trailing_unit.
        /// </summary>
        [TestMethod]
        public void Parse_R12_Guard_PlainDecimal_RoutesToPlainNumber()
        {
            var r = ValueParser.Parse("71.8");
            Assert.AreEqual("plain_number", r.ParseRule);
            Assert.AreEqual(71.8, r.PrimaryValue);
        }

        /**************************************************************/
        /// <summary>
        /// R12 direct call — tryParseValueParenDispersion returns false on
        /// non-matching inputs so callers can fall through.
        /// </summary>
        [TestMethod]
        public void TryParseValueParenDispersion_NonMatching_ReturnsFalse()
        {
            Assert.IsFalse(ValueParser.tryParseValueParenDispersion("33 (17.6%)", out _));
            Assert.IsFalse(ValueParser.tryParseValueParenDispersion("1.1 ± 0.5", out _));
            Assert.IsFalse(ValueParser.tryParseValueParenDispersion("71.8", out _));
            Assert.IsFalse(ValueParser.tryParseValueParenDispersion("narrative text", out _));
        }

        /**************************************************************/
        /// <summary>
        /// R12 direct call — tryParseValueTrailingUnit returns false on
        /// non-matching inputs and unknown units.
        /// </summary>
        [TestMethod]
        public void TryParseValueTrailingUnit_NonMatching_ReturnsFalse()
        {
            Assert.IsFalse(ValueParser.tryParseValueTrailingUnit("71.8 hello", out _));
            Assert.IsFalse(ValueParser.tryParseValueTrailingUnit("71.8", out _));
            Assert.IsFalse(ValueParser.tryParseValueTrailingUnit("hr", out _));
            Assert.IsFalse(ValueParser.tryParseValueTrailingUnit("narrative", out _));
        }

        #endregion R12 — Value Paren Dispersion + Trailing Unit
    }
}
