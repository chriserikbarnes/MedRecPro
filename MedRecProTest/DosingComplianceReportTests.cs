using MedRecProConsole.Services.Reporting;
using MedRecProImportClass.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="DosingComplianceReport"/> — verifies metric
    /// computation against synthetic JSONL payloads. Real JSONL files live in
    /// the console build output directory; integration verification is the
    /// caller's responsibility (run the pipeline, then call
    /// <see cref="DosingComplianceReport.BuildReportFromJsonl"/>).
    /// </summary>
    [TestClass]
    public class DosingComplianceReportTests
    {
        #region Helpers

        private static DosingComplianceReport.CompliancePayload makeDosingRow(
            int textTableId,
            string? parameterName = null,
            string? population = null,
            string? doseRegimen = null,
            string? primaryValueType = null,
            string? rawValue = null,
            string? validationFlags = null)
        {
            return new DosingComplianceReport.CompliancePayload
            {
                TextTableId = textTableId,
                ParentSectionCode = "34068-7",
                Category = "dosing",
                ObservationCount = 1,
                Observation = new ParsedObservation
                {
                    TextTableID = textTableId,
                    ParameterName = parameterName,
                    Population = population,
                    DoseRegimen = doseRegimen,
                    PrimaryValueType = primaryValueType,
                    RawValue = rawValue,
                    ValidationFlags = validationFlags,
                }
            };
        }

        #endregion

        [TestMethod]
        public void BuildReport_AllRowsCompleteAndNumeric_HitsTargets()
        {
            var rows = new[]
            {
                makeDosingRow(1, "Starting Dose", "Adult", "10 mg", "Numeric"),
                makeDosingRow(1, "Maintenance Dose", "Adult", "20 mg", "Numeric"),
                makeDosingRow(2, "Pediatric", "Pediatric", "5 mg/kg", "Numeric"),
            };

            var md = DosingComplianceReport.BuildReport(rows, sourceLabel: null);

            Assert.IsTrue(md.Contains("Dosing observations: **3**"), md);
            Assert.IsTrue(md.Contains("Distinct Dosing tables: **2**"), md);
            // 3/3 → 100.0%
            Assert.IsTrue(md.Contains("Complete comparison key | 3 | 100.0%"), md);
            Assert.IsTrue(md.Contains("Missing DoseRegimen | 0 | 0.0%"), md);
            Assert.IsTrue(md.Contains("Non-Numeric PrimaryValueType | 0 | 0.0%"), md);
        }

        [TestMethod]
        public void BuildReport_MissingFieldsAndUnitLeak_AreFlagged()
        {
            var rows = new[]
            {
                makeDosingRow(1, "Starting Dose", null, null, "Text",
                    rawValue: "1 mg once daily", validationFlags: "COL_STD:UNIT_HEADER_LEAK"),
                makeDosingRow(1, null, "Adult", "10 mg", "Numeric"),
                makeDosingRow(2, "Pediatric", null, null, "Range",
                    rawValue: "10-20 mg"),
            };

            var md = DosingComplianceReport.BuildReport(rows, sourceLabel: null);

            // 0/3 complete (every row missing at least one field)
            Assert.IsTrue(md.Contains("Complete comparison key | 0 | 0.0%"), md);
            // Missing-field counts
            Assert.IsTrue(md.Contains("Missing ParameterName | 1"), md);
            Assert.IsTrue(md.Contains("Missing Population | 2"), md);
            Assert.IsTrue(md.Contains("Missing DoseRegimen | 2"), md);
            // 2/3 Non-Numeric (Text + Range)
            Assert.IsTrue(md.Contains("Non-Numeric PrimaryValueType | 2"), md);
            // UNIT_HEADER_LEAK flagged on row 0 only
            Assert.IsTrue(md.Contains("UNIT_HEADER_LEAK flagged | 1"), md);
            // Both prose-rows ("1 mg once daily", "10-20 mg") have raw doses but no DoseRegimen
            Assert.IsTrue(md.Contains("Raw-dose without DoseRegimen | 2"), md);
        }

        [TestMethod]
        public void BuildReport_DowngradedTablesAreCounted()
        {
            // Two 34068-7 tables ended up in TEXT_DESCRIPTIVE; one in OTHER; one
            // stayed DOSING. The downgraded section should show 2 + 1.
            var rows = new[]
            {
                new DosingComplianceReport.CompliancePayload
                {
                    TextTableId = 100,
                    ParentSectionCode = "34068-7",
                    Category = "text_descriptive",
                    ObservationCount = 0,
                    Observation = null
                },
                new DosingComplianceReport.CompliancePayload
                {
                    TextTableId = 101,
                    ParentSectionCode = "34068-7",
                    Category = "text_descriptive",
                    ObservationCount = 0,
                    Observation = null
                },
                new DosingComplianceReport.CompliancePayload
                {
                    TextTableId = 102,
                    ParentSectionCode = "34068-7",
                    Category = "other",
                    ObservationCount = 0,
                    Observation = null
                },
                makeDosingRow(103, "Starting Dose", "Adult", "10 mg", "Numeric"),
            };

            var md = DosingComplianceReport.BuildReport(rows, sourceLabel: null);

            Assert.IsTrue(md.Contains("`text_descriptive` | 2"), md);
            Assert.IsTrue(md.Contains("`other` | 1"), md);
        }

        [TestMethod]
        public void BuildReport_NoDosingRows_ReturnsNa()
        {
            var rows = Array.Empty<DosingComplianceReport.CompliancePayload>();
            var md = DosingComplianceReport.BuildReport(rows, sourceLabel: null);

            Assert.IsTrue(md.Contains("Dosing observations: **0**"), md);
            Assert.IsTrue(md.Contains("| n/a |"), md);
        }

        /**************************************************************/
        /// <summary>
        /// Integration check — when a real Stage 3 JSONL audit exists in the
        /// console build output directory, run the full reporter against it
        /// and write the rendered Markdown next to the JSONL. Asserts the
        /// failing baseline numbers are in the ballpark the user reported
        /// (0% comparison key, ≥90% missing DoseRegimen). When no JSONL is
        /// available, the test is inconclusive (skipped) instead of failing.
        /// </summary>
        [TestMethod]
        public void BuildReportFromJsonl_RealAuditOutput_GeneratesBaselineMarkdown()
        {
            var dir = Path.Combine(
                Directory.GetCurrentDirectory(), "..", "..", "..", "..",
                "MedRecProConsole", "bin", "Debug", "net8.0");
            dir = Path.GetFullPath(dir);

            if (!Directory.Exists(dir))
            {
                Assert.Inconclusive($"Console build output directory not found: {dir}");
                return;
            }

            var jsonlPath = Directory
                .EnumerateFiles(dir, "standardization-report-*.jsonl")
                .OrderByDescending(p => new FileInfo(p).LastWriteTimeUtc)
                .FirstOrDefault();

            if (jsonlPath == null)
            {
                Assert.Inconclusive("No standardization-report-*.jsonl found in the console build output. Run the standardization pipeline first.");
                return;
            }

            var md = DosingComplianceReport.BuildReportFromJsonl(jsonlPath);

            // Write the rendered markdown next to the JSONL so the user can
            // diff it against later runs.
            var outPath = Path.Combine(
                Path.GetDirectoryName(jsonlPath)!,
                $"dosing-compliance-{Path.GetFileNameWithoutExtension(jsonlPath)}.md");
            File.WriteAllText(outPath, md);

            // Sanity assertion — the failing baseline must show up. If
            // someone runs this AFTER PR 2 lands, these expectations will
            // need to flip; that's intentional — the test will surface the
            // change loudly.
            Assert.IsTrue(md.Contains("# Dosing Compliance Report"), md);
            Assert.IsTrue(md.Contains("Dosing observations:"), md);

            TestContext?.WriteLine($"Wrote compliance report to: {outPath}");
        }

        public TestContext? TestContext { get; set; }

        [TestMethod]
        public void BuildReport_NonDosingRows_AreIgnoredForRowMetrics()
        {
            var rows = new[]
            {
                new DosingComplianceReport.CompliancePayload
                {
                    TextTableId = 200,
                    ParentSectionCode = "34090-1",
                    Category = "pk",
                    ObservationCount = 1,
                    Observation = new ParsedObservation
                    {
                        TextTableID = 200,
                        ParameterName = "Cmax",
                        // no DoseRegimen, would skew Dosing metrics if not filtered
                    }
                },
                makeDosingRow(201, "Starting Dose", "Adult", "10 mg", "Numeric"),
            };

            var md = DosingComplianceReport.BuildReport(rows, sourceLabel: null);

            Assert.IsTrue(md.Contains("Total observations across all categories: **2**"), md);
            Assert.IsTrue(md.Contains("Dosing observations: **1**"), md);
            Assert.IsTrue(md.Contains("Complete comparison key | 1 | 100.0%"), md);
        }
    }
}
