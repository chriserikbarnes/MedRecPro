using MedRecProConsole.Services.Reporting;
using MedRecProImportClass.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="TableStandardizationMarkdownWriter"/>. Tests cover the GFM
    /// format of each section (metadata, pivot, footnotes, routing, observations, Claude
    /// corrections) plus regression coverage for Spectre markup leaking into output.
    /// </summary>
    /// <seealso cref="TableStandardizationMarkdownWriter"/>
    /// <seealso cref="TableReportEntry"/>
    [TestClass]
    public class TableStandardizationMarkdownWriterTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Builds a minimal PK-style <see cref="ReconstructedTable"/> with two columns and
        /// one data row. Overrides are optional — pass null to skip footnotes.
        /// </summary>
        private static ReconstructedTable buildMinimalTable(
            int textTableId = 24820,
            string caption = "Table 3: PK parameters",
            Dictionary<string, string>? footnotes = null,
            int columnCount = 3)
        {
            #region implementation

            return new ReconstructedTable
            {
                TextTableID = textTableId,
                Caption = caption,
                ParentSectionCode = "34090-1",
                SectionTitle = "12.3 Pharmacokinetics",
                TotalColumnCount = columnCount,
                TotalRowCount = 2,
                HasInferredHeader = true,
                Header = new ResolvedHeader
                {
                    Columns = new List<HeaderColumn>
                    {
                        new() { LeafHeaderText = "Parameter" },
                        new() { LeafHeaderText = "Cmax" },
                        new() { LeafHeaderText = "AUC" }
                    }
                },
                Rows = new List<ReconstructedRow>
                {
                    new()
                    {
                        Classification = RowClassification.DataBody,
                        Cells = new List<ProcessedCell>
                        {
                            new() { ResolvedColumnStart = 0, ResolvedColumnEnd = 1, CleanedText = "Drug A" },
                            new() { ResolvedColumnStart = 1, ResolvedColumnEnd = 2, CleanedText = "9044 (20)" },
                            new() { ResolvedColumnStart = 2, ResolvedColumnEnd = 3, CleanedText = "80289 (15)" }
                        }
                    }
                },
                Footnotes = footnotes
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a <see cref="ParsedObservation"/> with the common fields populated.
        /// </summary>
        private static ParsedObservation buildObservation(
            int textTableId = 24820,
            int rowSeq = 1,
            int cellSeq = 1,
            string parameter = "Cmax",
            string raw = "9044 (20)",
            double primary = 9044,
            string type = "Mean",
            double confidence = 1.0,
            string rule = "caption_mean_cv_percent",
            string? validationFlags = null)
        {
            #region implementation

            return new ParsedObservation
            {
                TextTableID = textTableId,
                SourceRowSeq = rowSeq,
                SourceCellSeq = cellSeq,
                ParameterName = parameter,
                TreatmentArm = null,
                RawValue = raw,
                PrimaryValue = primary,
                PrimaryValueType = type,
                ParseConfidence = confidence,
                ParseRule = rule,
                ValidationFlags = validationFlags
            };

            #endregion
        }

        #endregion

        #region Section Format Tests

        /**************************************************************/
        /// <summary>
        /// A minimal PK table renders the expected GFM sections in order: H2 header,
        /// Stage 2 block, Stage 3 block, Stage 3.5 block, trailing separator.
        /// </summary>
        [TestMethod]
        public void BuildSection_PkTableMinimal_ProducesAllSections()
        {
            // Arrange
            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { buildObservation() },
                BeforeClaudeFlags = null,
                ClaudeSkipped = true
            };

            // Act
            var md = TableStandardizationMarkdownWriter.BuildSection(entry);

            // Assert
            StringAssert.Contains(md, "## TextTableID=24820 — Table 3: PK parameters");
            StringAssert.Contains(md, "### Stage 2: Pivot Table");
            StringAssert.Contains(md, "**Table Metadata**");
            StringAssert.Contains(md, "| TextTableID | 24820 |");
            StringAssert.Contains(md, "**Pivoted Table Data**");
            StringAssert.Contains(md, "| Parameter | Cmax | AUC |");
            StringAssert.Contains(md, "### Stage 3: Standardize");
            StringAssert.Contains(md, "- **Category:** PK");
            StringAssert.Contains(md, "- **Parser:** `PkTableParser`");
            StringAssert.Contains(md, "**TextTableID=24820 — 1 observation**");
            StringAssert.Contains(md, "| Parameter | Arm | Raw Value | Primary | Type | Confidence | Rule |");
            StringAssert.Contains(md, "### Stage 3.5: Claude Enhance");
            StringAssert.Contains(md, "_Skipped (--no-claude)_");
            Assert.IsTrue(md.TrimEnd().EndsWith("---"), "Section must end with horizontal rule.");
        }

        /**************************************************************/
        /// <summary>
        /// Footnotes, when present, render as a bullet list under a bold header.
        /// </summary>
        [TestMethod]
        public void BuildSection_Footnotes_RendersBulletList()
        {
            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(footnotes: new Dictionary<string, string>
                {
                    ["a"] = "Measured at steady state.",
                    ["b"] = "N = safety population."
                }),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { buildObservation() },
                ClaudeSkipped = true
            };

            var md = TableStandardizationMarkdownWriter.BuildSection(entry);

            StringAssert.Contains(md, "**Footnotes**");
            StringAssert.Contains(md, "- `[a]` Measured at steady state.");
            StringAssert.Contains(md, "- `[b]` N = safety population.");
        }

        /**************************************************************/
        /// <summary>
        /// When no footnotes exist, the Footnotes section is omitted entirely.
        /// </summary>
        [TestMethod]
        public void BuildSection_NoFootnotes_OmitsSection()
        {
            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(footnotes: null),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { buildObservation() },
                ClaudeSkipped = true
            };

            var md = TableStandardizationMarkdownWriter.BuildSection(entry);

            Assert.IsFalse(md.Contains("**Footnotes**"), "Footnotes heading must not appear when no footnotes exist.");
        }

        /**************************************************************/
        /// <summary>
        /// SOC divider rows render with bold first column and empty remaining columns.
        /// </summary>
        [TestMethod]
        public void BuildSection_SocDivider_RendersBoldFirstCellEmptyRest()
        {
            var table = buildMinimalTable();
            table.HasSocDividers = true;
            table.Rows!.Insert(0, new ReconstructedRow
            {
                Classification = RowClassification.SocDivider,
                SocName = "Gastrointestinal Disorders",
                Cells = new List<ProcessedCell>()
            });

            var entry = new TableReportEntry
            {
                Table = table,
                Category = TableCategory.ADVERSE_EVENT,
                ParserName = "AdverseEventParser",
                Observations = new[] { buildObservation() },
                ClaudeSkipped = true
            };

            var md = TableStandardizationMarkdownWriter.BuildSection(entry);

            StringAssert.Contains(md, "| **Gastrointestinal Disorders** |  |  |");
        }

        /**************************************************************/
        /// <summary>
        /// Cell spans render as the leftmost value followed by the ↔ placeholder in
        /// subsequent columns, matching the console behaviour.
        /// </summary>
        [TestMethod]
        public void BuildSection_CellSpan_RendersArrowPlaceholder()
        {
            var table = buildMinimalTable(columnCount: 3);
            // Replace the only row with a single cell that spans all 3 columns
            table.Rows = new List<ReconstructedRow>
            {
                new()
                {
                    Classification = RowClassification.DataBody,
                    Cells = new List<ProcessedCell>
                    {
                        new() { ResolvedColumnStart = 0, ResolvedColumnEnd = 3, CleanedText = "Spanned Row" }
                    }
                }
            };

            var entry = new TableReportEntry
            {
                Table = table,
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { buildObservation() },
                ClaudeSkipped = true
            };

            var md = TableStandardizationMarkdownWriter.BuildSection(entry);

            StringAssert.Contains(md, "| Spanned Row | ↔ | ↔ |");
        }

        /**************************************************************/
        /// <summary>
        /// An empty observations list renders a "no observations produced" note rather than
        /// an empty GFM table.
        /// </summary>
        [TestMethod]
        public void BuildSection_ZeroObservations_WritesZeroObsNote()
        {
            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(),
                Category = TableCategory.SKIP,
                ParserName = null,
                Observations = Array.Empty<ParsedObservation>(),
                ClaudeSkipped = true
            };

            var md = TableStandardizationMarkdownWriter.BuildSection(entry);

            StringAssert.Contains(md, "**TextTableID=24820 — 0 observations**");
            StringAssert.Contains(md, "_No observations produced._");
            StringAssert.Contains(md, "- **Parser:** _(none — skipped)_");
            Assert.IsFalse(md.Contains("| Parameter | Arm | Raw Value |"),
                "No observation table should be rendered for zero-obs entries.");
        }

        /**************************************************************/
        /// <summary>
        /// Pipes inside cell text are escaped with a backslash so they do not prematurely
        /// close a GFM cell.
        /// </summary>
        [TestMethod]
        public void BuildSection_PipeInCellText_EscapesWithBackslash()
        {
            var table = buildMinimalTable();
            table.Rows![0].Cells![0].CleanedText = "Drug | mixed";

            var entry = new TableReportEntry
            {
                Table = table,
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { buildObservation(raw: "95 | 99") },
                ClaudeSkipped = true
            };

            var md = TableStandardizationMarkdownWriter.BuildSection(entry);

            StringAssert.Contains(md, @"Drug \| mixed");
            StringAssert.Contains(md, @"95 \| 99");
        }

        /**************************************************************/
        /// <summary>
        /// Newlines inside caption or cell text collapse to &lt;br&gt; so the GFM row stays
        /// on a single line.
        /// </summary>
        [TestMethod]
        public void BuildSection_NewlineInCaption_ReplacesWithBr()
        {
            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(caption: "Table 3:\nPK parameters"),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { buildObservation() },
                ClaudeSkipped = true
            };

            var md = TableStandardizationMarkdownWriter.BuildSection(entry);

            StringAssert.Contains(md, "## TextTableID=24820 — Table 3:<br>PK parameters");
        }

        #endregion

        #region Claude Section Tests

        /**************************************************************/
        /// <summary>
        /// When Claude was skipped, Stage 3.5 renders the italic skipped line.
        /// </summary>
        [TestMethod]
        public void BuildSection_ClaudeSkipped_WritesSkippedLine()
        {
            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { buildObservation() },
                ClaudeSkipped = true
            };

            var md = TableStandardizationMarkdownWriter.BuildSection(entry);

            StringAssert.Contains(md, "_Skipped (--no-claude)_");
            Assert.IsFalse(md.Contains("Correction Applied"), "No corrections table should appear.");
        }

        /**************************************************************/
        /// <summary>
        /// When Claude ran and at least one AI_CORRECTED flag was added, the corrections
        /// table renders a row per correction with Row, Cell, and the full flag text.
        /// </summary>
        [TestMethod]
        public void BuildSection_ClaudeAppliedCorrections_RendersCorrectionsTable()
        {
            var obs = buildObservation();
            obs.ValidationFlags = "AI_CORRECTED:Unit (mg → mcg)";

            var before = new Dictionary<int, string?>
            {
                [(obs.SourceRowSeq ?? 0) * 10000 + (obs.SourceCellSeq ?? 0)] = null
            };

            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { obs },
                BeforeClaudeFlags = before,
                ClaudeSkipped = false
            };

            var md = TableStandardizationMarkdownWriter.BuildSection(entry);

            StringAssert.Contains(md, "**1 Correction Applied**");
            StringAssert.Contains(md, "| Row | Cell | Correction |");
            StringAssert.Contains(md, "AI_CORRECTED:Unit (mg → mcg)");
        }

        /**************************************************************/
        /// <summary>
        /// When Claude ran but no flags changed, writes a "no corrections" note — not an
        /// empty table.
        /// </summary>
        [TestMethod]
        public void BuildSection_ClaudeRanNoChanges_WritesNoCorrectionsNote()
        {
            var obs = buildObservation(validationFlags: "COL_STD:ARM_WAS_N");
            var before = new Dictionary<int, string?>
            {
                [(obs.SourceRowSeq ?? 0) * 10000 + (obs.SourceCellSeq ?? 0)] = "COL_STD:ARM_WAS_N"
            };

            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { obs },
                BeforeClaudeFlags = before,
                ClaudeSkipped = false
            };

            var md = TableStandardizationMarkdownWriter.BuildSection(entry);

            StringAssert.Contains(md, "_No corrections applied by Claude._");
        }

        #endregion

        #region Regression Tests

        /**************************************************************/
        /// <summary>
        /// A low-confidence observation must not leak Spectre markup like "[red]0.30[/]"
        /// into the markdown output — rendering targets are separate.
        /// </summary>
        [TestMethod]
        public void BuildSection_LowConfidenceRow_NoColorMarkupInMarkdown()
        {
            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { buildObservation(confidence: 0.3) },
                ClaudeSkipped = true
            };

            var md = TableStandardizationMarkdownWriter.BuildSection(entry);

            Assert.IsFalse(md.Contains("[red]"), "Spectre color markup must not leak into markdown.");
            Assert.IsFalse(md.Contains("[/]"), "Spectre closing tag must not leak into markdown.");
            StringAssert.Contains(md, "0.30");
        }

        #endregion
    }
}
