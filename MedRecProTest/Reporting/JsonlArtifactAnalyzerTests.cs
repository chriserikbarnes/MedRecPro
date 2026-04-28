using MedRecProConsole.Services.Reporting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="JsonlArtifactAnalyzer"/>. Drive the analyzer with synthetic
    /// JSONL lines that mirror the shape produced by <c>TableStandardizationJsonWriter</c> and
    /// assert the aggregated forward-rate, reason combination, and shape buckets.
    /// </summary>
    /// <remarks>
    /// These tests pin down the gate semantics (strict <c>&lt;</c> against
    /// <see cref="JsonlArtifactAnalyzer.DefaultThreshold"/>), the combination dedupe / sort
    /// rule, and the raw-value shape bucketing. They use in-memory string sequences so they
    /// have no filesystem dependency.
    /// </remarks>
    /// <seealso cref="JsonlArtifactAnalyzer"/>
    [TestClass]
    public class JsonlArtifactAnalyzerTests
    {
        #region helpers

        /**************************************************************/
        /// <summary>
        /// Builds one JSONL line with the field names <see cref="JsonlArtifactAnalyzer"/>
        /// reads from. Matches the camelCase layout produced by the production writer.
        /// </summary>
        private static string makeLine(
            int textTableId,
            string innerCategory,
            string parser,
            string rawValue,
            double? score,
            string? reasons = null)
        {
            #region implementation

            var validationFlags = score.HasValue
                ? $"QC_PARSE_QUALITY:{score.Value:0.0000}"
                : "";

            if (!string.IsNullOrEmpty(reasons))
            {
                validationFlags += "; QC_PARSE_QUALITY:REVIEW_REASONS:" + reasons;
            }

            var topCategory = innerCategory
                .ToLowerInvariant()
                .Replace("_", "_");

            return "{"
                + $"\"textTableId\":{textTableId},"
                + $"\"category\":\"{topCategory}\","
                + $"\"parser\":\"{parser}\","
                + "\"observation\":{"
                + $"\"tableCategory\":\"{innerCategory}\","
                + $"\"rawValue\":\"{escape(rawValue)}\","
                + $"\"validationFlags\":\"{escape(validationFlags)}\""
                + "}}";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Escapes a string for safe inclusion in a JSON string literal.
        /// </summary>
        private static string escape(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        #endregion helpers

        #region tests

        /**************************************************************/
        /// <summary>
        /// Strict less-than gate: rows whose score is exactly the threshold are skipped, not
        /// forwarded. Matches <c>ClaudeApiCorrectionService</c>.
        /// </summary>
        [TestMethod]
        public void Analyze_GateIsStrictLessThanThreshold()
        {
            var lines = new[]
            {
                makeLine(1, "ADVERSE_EVENT", "SimpleArmTableParser", "5", 0.7400),
                makeLine(2, "ADVERSE_EVENT", "SimpleArmTableParser", "5", 0.7500),
                makeLine(3, "ADVERSE_EVENT", "SimpleArmTableParser", "5", 0.7501),
            };

            var result = JsonlArtifactAnalyzer.Analyze(lines, threshold: 0.75);

            Assert.AreEqual(3, result.TotalScored);
            Assert.AreEqual(0, result.LinesWithoutScore);
            Assert.AreEqual(1, result.ByCategory.Count);

            var ae = result.ByCategory[0];
            Assert.AreEqual("ADVERSE_EVENT", ae.Category);
            Assert.AreEqual(3, ae.TotalRows);
            Assert.AreEqual(1, ae.Forwarded, "only the 0.7400 row should fall under the threshold");
            Assert.AreEqual(2, ae.Skipped);
        }

        /**************************************************************/
        /// <summary>
        /// Per-(category, parser) roll-up keeps the parsers separate so we can target
        /// remediation by parser.
        /// </summary>
        [TestMethod]
        public void Analyze_GroupsByCategoryAndParser()
        {
            var lines = new[]
            {
                makeLine(1, "ADVERSE_EVENT", "SimpleArmTableParser", "5", 0.5),
                makeLine(2, "ADVERSE_EVENT", "MultilevelAeTableParser", "5", 0.5),
                makeLine(3, "ADVERSE_EVENT", "SimpleArmTableParser", "5", 0.9),
                makeLine(4, "PK", "PkTableParser", "5", 0.5),
            };

            var result = JsonlArtifactAnalyzer.Analyze(lines);

            Assert.AreEqual(3, result.ByParser.Count);

            var simpleArm = result.ByParser.Single(r => r.Parser == "SimpleArmTableParser");
            Assert.AreEqual("ADVERSE_EVENT", simpleArm.Category);
            Assert.AreEqual(2, simpleArm.TotalRows);
            Assert.AreEqual(1, simpleArm.Forwarded);

            var pk = result.ByParser.Single(r => r.Parser == "PkTableParser");
            Assert.AreEqual(1, pk.Forwarded);
            Assert.AreEqual(1.0, pk.ForwardRate);
        }

        /**************************************************************/
        /// <summary>
        /// Reason combinations are alphabetized so equivalent sets dedupe regardless of
        /// emission order.
        /// </summary>
        [TestMethod]
        public void Analyze_NormalizesReasonCombinationOrder()
        {
            var lines = new[]
            {
                makeLine(10, "ADVERSE_EVENT", "SimpleArmTableParser", "x", 0.5,
                    reasons: "PVT_MIGRATED|MissingRequired:TreatmentArm"),
                makeLine(11, "ADVERSE_EVENT", "SimpleArmTableParser", "x", 0.5,
                    reasons: "MissingRequired:TreatmentArm|PVT_MIGRATED"),
            };

            var result = JsonlArtifactAnalyzer.Analyze(lines);

            Assert.AreEqual(1, result.TopReasons.Count, "reorderings of the same set should collapse");
            var combo = result.TopReasons[0];
            Assert.AreEqual(2, combo.Rows);
            Assert.AreEqual(2, combo.DistinctTables);
            Assert.AreEqual("MissingRequired:TreatmentArm | PVT_MIGRATED", combo.ReasonCombination);
        }

        /**************************************************************/
        /// <summary>
        /// Reasons are aggregated only for forwarded rows — matches the comment in
        /// <c>TableStandardizationChecks.sql</c>.
        /// </summary>
        [TestMethod]
        public void Analyze_OnlyAggregatesReasonsForForwardedRows()
        {
            var lines = new[]
            {
                makeLine(1, "PK", "PkTableParser", "x", 0.5, reasons: "MissingRequired:Unit"),
                makeLine(2, "PK", "PkTableParser", "x", 0.9, reasons: "SoftRepair:PVT_MIGRATED"),
            };

            var result = JsonlArtifactAnalyzer.Analyze(lines);

            Assert.AreEqual(1, result.TopReasons.Count);
            Assert.AreEqual("MissingRequired:Unit", result.TopReasons[0].ReasonCombination);
        }

        /**************************************************************/
        /// <summary>
        /// Shape classifier recognizes the high-volume failure shapes from the 2026-04-28
        /// baseline.
        /// </summary>
        [TestMethod]
        public void ClassifyShape_RecognizesBaselineBuckets()
        {
            Assert.AreEqual(JsonlArtifactAnalyzer.ShapeLabels.LessThanEncoded,
                JsonlArtifactAnalyzer.classifyShape("&lt;1"));
            Assert.AreEqual(JsonlArtifactAnalyzer.ShapeLabels.LessThanEncoded,
                JsonlArtifactAnalyzer.classifyShape("1 (&lt;1%)"));
            Assert.AreEqual(JsonlArtifactAnalyzer.ShapeLabels.DashOrExclusion,
                JsonlArtifactAnalyzer.classifyShape("--"));
            Assert.AreEqual(JsonlArtifactAnalyzer.ShapeLabels.DashOrExclusion,
                JsonlArtifactAnalyzer.classifyShape("NR"));
            Assert.AreEqual(JsonlArtifactAnalyzer.ShapeLabels.DigitsOnly,
                JsonlArtifactAnalyzer.classifyShape("216"));
            Assert.AreEqual(JsonlArtifactAnalyzer.ShapeLabels.TextWithDigits,
                JsonlArtifactAnalyzer.classifyShape("0.75 mg once daily"));
            Assert.AreEqual(JsonlArtifactAnalyzer.ShapeLabels.TextNoDigit,
                JsonlArtifactAnalyzer.classifyShape("Patients with any adverse reaction"));
            Assert.AreEqual(JsonlArtifactAnalyzer.ShapeLabels.Blank,
                JsonlArtifactAnalyzer.classifyShape(""));
            Assert.AreEqual(JsonlArtifactAnalyzer.ShapeLabels.Blank,
                JsonlArtifactAnalyzer.classifyShape(null));
        }

        /**************************************************************/
        /// <summary>
        /// Worst-table sort surfaces the table with the most forwarded rows first, even when
        /// another table has a higher rate over fewer total rows.
        /// </summary>
        [TestMethod]
        public void Analyze_WorstTableSortPrefersAbsoluteForwardCount()
        {
            // Table 100: 5 rows, all forwarded → 5 forwarded, 100% rate.
            // Table 200: 50 rows, 10 forwarded → 10 forwarded, 20% rate.
            // The 10-forward table dominates cost; the analyzer surfaces it first.
            var lines = new List<string>();
            for (int i = 0; i < 5; i++)
                lines.Add(makeLine(100, "ADVERSE_EVENT", "SimpleArmTableParser", "x", 0.5));
            for (int i = 0; i < 10; i++)
                lines.Add(makeLine(200, "ADVERSE_EVENT", "SimpleArmTableParser", "x", 0.5));
            for (int i = 0; i < 40; i++)
                lines.Add(makeLine(200, "ADVERSE_EVENT", "SimpleArmTableParser", "x", 0.9));

            var result = JsonlArtifactAnalyzer.Analyze(lines);

            Assert.IsTrue(result.TopWorstTables.Count >= 2);
            Assert.AreEqual(200, result.TopWorstTables[0].TextTableId);
            Assert.AreEqual(100, result.TopWorstTables[1].TextTableId);
        }

        /**************************************************************/
        /// <summary>
        /// Lines without a parsable score (e.g. meta lines for skipped tables, malformed
        /// rows) are counted separately and do not pollute the forward-rate denominator.
        /// </summary>
        [TestMethod]
        public void Analyze_CountsLinesWithoutScoreSeparately()
        {
            var lines = new[]
            {
                makeLine(1, "ADVERSE_EVENT", "SimpleArmTableParser", "x", 0.5),
                // A meta line with no validationFlags / score — analyzer shouldn't count it.
                "{\"textTableId\":2,\"category\":\"adversE_EVENT\",\"parser\":null,\"observation\":null}",
                // A malformed line.
                "{not json"
            };

            var result = JsonlArtifactAnalyzer.Analyze(lines);

            Assert.AreEqual(1, result.TotalScored);
            Assert.AreEqual(2, result.LinesWithoutScore);
        }

        /**************************************************************/
        /// <summary>
        /// Markdown output includes all section headings so downstream readers can find each
        /// breakdown table by anchor.
        /// </summary>
        [TestMethod]
        public void ToMarkdown_RendersAllSections()
        {
            var lines = new[]
            {
                makeLine(1, "ADVERSE_EVENT", "SimpleArmTableParser", "&lt;1", 0.5,
                    reasons: "PrimaryValueNull|PrimaryValueTypeText"),
                makeLine(2, "PK", "PkTableParser", "216", 0.5,
                    reasons: "MissingRequired:Unit|MISSING_R_Unit"),
            };

            var result = JsonlArtifactAnalyzer.Analyze(lines);
            var md = JsonlArtifactAnalyzer.ToMarkdown(result);

            StringAssert.Contains(md, "Forward rate by category");
            StringAssert.Contains(md, "Forward rate by parser");
            StringAssert.Contains(md, "Top reason combinations");
            StringAssert.Contains(md, "Raw-value shapes");
            StringAssert.Contains(md, "Worst tables");
        }

        #endregion tests
    }
}
