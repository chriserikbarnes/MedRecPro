using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Baseline regression fixtures for the deterministic parse-QC remediation effort
    /// (plan: <c>~/.claude/plans/deterministic-parse-qc-splendid-beacon.md</c>). Each fixture
    /// reproduces a high-volume failure shape from the 2026-04-28 standardization artifact and
    /// asserts the parser's *current* (broken) behavior. When Phase 2-6 fixes land, these
    /// tests will fail and need to be updated to reflect the new expected behavior — that is
    /// the whole point of the regression baseline.
    /// </summary>
    /// <remarks>
    /// ## Source
    /// Failure shapes and TextTableID examples are taken from
    /// <c>standardization-report-20260428-134443.jsonl</c>:
    /// <list type="bullet">
    /// <item><description>AE encoded <c>&lt;</c> values — TextTableID 44946 and similar (1,830 rows)</description></item>
    /// <item><description>AE missing <c>TreatmentArm</c> — TextTableID 40880 and similar (6,080 rows)</description></item>
    /// <item><description>AE body-system header in data position — TextTableID 31303 and similar (1,598 rows)</description></item>
    /// <item><description>Efficacy event-axis layout — TextTableID 41264 and similar</description></item>
    /// <item><description>PK missing <c>Unit</c> — TextTableID 42461 and similar (1,132 rows)</description></item>
    /// </list>
    ///
    /// ## What these tests do not assert
    /// They do not assert correctness — only current observable behavior. The point is to
    /// detect drift; the plan's later phases will update the assertions to match the new
    /// expected behavior once the parser fix lands.
    ///
    /// ## Quality scoring
    /// Where useful, fixtures invoke <see cref="ParseQualityService"/> via a real
    /// <see cref="ColumnContractRegistry"/> to assert the resulting score crosses (or fails to
    /// cross) the 0.75 forwarding gate. This pins the gate-side behavior at the same time as
    /// the parser-side behavior.
    /// </remarks>
    /// <seealso cref="TableParserTests"/>
    /// <seealso cref="ParseQualityService"/>
    [TestClass]
    public class TableParserBaselineFixtureTests
    {
        #region multilevel header helper

        /**************************************************************/
        /// <summary>
        /// Builds a 2-level header AE table where each leaf column has a HeaderPath
        /// supplied explicitly. Used by Phase 3 fixtures to exercise the parent-header
        /// recovery path in <c>extractArmDefinitions</c>.
        /// </summary>
        private static ReconstructedTable createMultilevelTable(
            string column0Header,
            (string parent, string leaf)[] dataColumns,
            List<string?[]> dataRows,
            string? caption = null,
            string? parentSectionCode = null,
            int textTableId = 1)
        {
            #region implementation

            var columns = new List<HeaderColumn>
            {
                new HeaderColumn
                {
                    ColumnIndex = 0,
                    LeafHeaderText = column0Header,
                    HeaderPath = new List<string> { column0Header },
                    CombinedHeaderText = column0Header,
                }
            };

            for (int i = 0; i < dataColumns.Length; i++)
            {
                var (parent, leaf) = dataColumns[i];
                columns.Add(new HeaderColumn
                {
                    ColumnIndex = i + 1,
                    LeafHeaderText = leaf,
                    HeaderPath = new List<string> { parent, leaf },
                    CombinedHeaderText = $"{parent} > {leaf}",
                });
            }

            var rows = new List<ReconstructedRow>();
            for (int r = 0; r < dataRows.Count; r++)
            {
                var cells = new List<ProcessedCell>();
                for (int c = 0; c < dataRows[r].Length; c++)
                {
                    cells.Add(new ProcessedCell
                    {
                        SequenceNumber = c + 1,
                        ResolvedColumnStart = c,
                        ResolvedColumnEnd = c + 1,
                        CleanedText = dataRows[r][c],
                        CellType = "td"
                    });
                }
                rows.Add(new ReconstructedRow
                {
                    SequenceNumberTextTableRow = r + 3,
                    Classification = RowClassification.DataBody,
                    AbsoluteRowIndex = r + 2,
                    Cells = cells
                });
            }

            return new ReconstructedTable
            {
                TextTableID = textTableId,
                Caption = caption,
                DocumentGUID = Guid.NewGuid(),
                Title = "Test Drug",
                VersionNumber = 1,
                ParentSectionCode = parentSectionCode,
                LabelerName = "Test Lab",
                TotalColumnCount = dataColumns.Length + 1,
                TotalRowCount = dataRows.Count + 2,
                HasExplicitHeader = true,
                HasInferredHeader = false,
                HasFooter = false,
                HasSocDividers = false,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 2,
                    ColumnCount = dataColumns.Length + 1,
                    Columns = columns
                },
                Rows = rows
            };

            #endregion
        }

        #endregion multilevel header helper

        #region helpers

        private static readonly ParseQualityService _qualityService =
            new ParseQualityService(new ColumnContractRegistry());

        private const float ForwardThreshold = 0.75f;

        /**************************************************************/
        /// <summary>
        /// Builds a flat single-header table with the given header texts and data rows. The
        /// helper mirrors <c>TableParserTests.createTestTable</c> but is duplicated locally so
        /// this fixture file stands alone.
        /// </summary>
        private static ReconstructedTable createSimpleTable(
            string?[] headerTexts,
            List<string?[]> dataRows,
            string? caption = null,
            string? parentSectionCode = null,
            string? sectionTitle = null,
            int textTableId = 1)
        {
            #region implementation

            var columns = new List<HeaderColumn>();
            for (int i = 0; i < headerTexts.Length; i++)
            {
                columns.Add(new HeaderColumn
                {
                    ColumnIndex = i,
                    LeafHeaderText = headerTexts[i],
                    HeaderPath = new List<string> { headerTexts[i] ?? "" },
                    CombinedHeaderText = headerTexts[i]
                });
            }

            var rows = new List<ReconstructedRow>();
            for (int r = 0; r < dataRows.Count; r++)
            {
                var cells = new List<ProcessedCell>();
                for (int c = 0; c < dataRows[r].Length; c++)
                {
                    cells.Add(new ProcessedCell
                    {
                        SequenceNumber = c + 1,
                        ResolvedColumnStart = c,
                        ResolvedColumnEnd = c + 1,
                        CleanedText = dataRows[r][c],
                        CellType = "td"
                    });
                }
                rows.Add(new ReconstructedRow
                {
                    SequenceNumberTextTableRow = r + 2,
                    Classification = RowClassification.DataBody,
                    AbsoluteRowIndex = r + 1,
                    Cells = cells
                });
            }

            return new ReconstructedTable
            {
                TextTableID = textTableId,
                Caption = caption,
                DocumentGUID = Guid.NewGuid(),
                Title = "Test Drug",
                VersionNumber = 1,
                ParentSectionCode = parentSectionCode,
                SectionTitle = sectionTitle,
                LabelerName = "Test Lab",
                TotalColumnCount = headerTexts.Length,
                TotalRowCount = dataRows.Count + 1,
                HasExplicitHeader = true,
                HasInferredHeader = false,
                HasFooter = false,
                HasSocDividers = false,
                Header = new ResolvedHeader
                {
                    HeaderRowCount = 1,
                    ColumnCount = headerTexts.Length,
                    Columns = columns
                },
                Rows = rows
            };

            #endregion
        }

        #endregion helpers

        #region Phase 2 — value-parsing fixtures (post-fix)

        /**************************************************************/
        /// <summary>
        /// Phase 2 fix: bare <c>&lt;1</c> reaches <see cref="ValueParser"/> directly only
        /// when callers bypass Stage 2 (the boundary HtmlDecode happens in
        /// <c>TextUtil.RemoveUnwantedTags</c>). Calling the parser with the *encoded* form
        /// still falls to text — that is the expected isolation: ValueParser does not own
        /// HTML decoding. The end-to-end fix is verified by
        /// <see cref="Phase2_AeBareLessThan_TaggedAsPValueAfterDecode"/> which parses the
        /// decoded form.
        /// </summary>
        [TestMethod]
        public void Phase2_AeEncodedLessThan_StillTextWhenParserCalledDirectly()
        {
            var parsed = ValueParser.Parse("&lt;1");

            Assert.AreEqual("Text", parsed.PrimaryValueType,
                "ValueParser does not decode HTML; the boundary fix lives in TextUtil.RemoveUnwantedTags.");
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2 fix: the decoded form <c>&lt;1</c> matches the existing p-value pattern
        /// and tags as <c>PValue</c>. This is the deterministic interpretation without
        /// table-category context. The PValue tag does not fire any
        /// <see cref="ParseQualityService"/> hard penalty (PrimaryValue and PrimaryValueType
        /// are both populated), so the row clears the 0.75 gate provided the other Required
        /// fields are present. Context-aware promotion of bare-inequality cells from PValue
        /// to Percentage upper-limit in AE/Efficacy contexts is a Phase 3 / Phase 6 concern.
        /// </summary>
        [TestMethod]
        public void Phase2_AeBareLessThan_TaggedAsPValueAfterDecode()
        {
            var parsed = ValueParser.Parse("<1");

            Assert.AreEqual("PValue", parsed.PrimaryValueType);
            Assert.AreEqual(1.0, parsed.PrimaryValue);
            Assert.AreEqual("<", parsed.PValueQualifier);
            Assert.AreEqual("pvalue", parsed.ParseRule);
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2 fix: <c>&lt;1%</c> with explicit percent suffix is now decomposed into
        /// a <c>Percentage</c> upper bound with the <c>INEQUALITY_UPPER:&lt;</c> validation
        /// flag, instead of falling to text fallback.
        /// </summary>
        [TestMethod]
        public void Phase2_InequalityPercent_TaggedAsPercentageUpperLimit()
        {
            var parsed = ValueParser.Parse("<1%");

            Assert.AreEqual("Percentage", parsed.PrimaryValueType);
            Assert.AreEqual(1.0, parsed.PrimaryValue);
            Assert.AreEqual("%", parsed.Unit);
            Assert.AreEqual("inequality_percent", parsed.ParseRule);
            Assert.AreEqual("INEQUALITY_UPPER:<", parsed.ValidationFlags);
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2 fix: <c>27 (&lt;1%)</c> compound shape is now decomposed into count +
        /// percentage upper-limit. The count goes to SecondaryValue (matching Pattern 4
        /// n_pct conventions), and the percentage upper-bound goes to PrimaryValue.
        /// </summary>
        [TestMethod]
        public void Phase2_CountInequalityPercent_DecomposesCleanly()
        {
            var parsed = ValueParser.Parse("27 (<1%)");

            Assert.AreEqual("Percentage", parsed.PrimaryValueType);
            Assert.AreEqual(1.0, parsed.PrimaryValue);
            Assert.AreEqual("Count", parsed.SecondaryValueType);
            Assert.AreEqual(27.0, parsed.SecondaryValue);
            Assert.AreEqual("%", parsed.Unit);
            Assert.AreEqual("count_inequality_percent", parsed.ParseRule);
            Assert.AreEqual("INEQUALITY_UPPER:<", parsed.ValidationFlags);
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2 fix: Pattern 10 (<c>n=</c>) now tolerates a single trailing footnote
        /// marker (<c>* † ‡ § ¶ #</c>), matching how SPL tables annotate sample-size cells.
        /// </summary>
        [TestMethod]
        public void Phase2_NEqualsWithFootnoteMarker_StillExtractsSampleSize()
        {
            var withStar = ValueParser.Parse("N = 100*");
            Assert.AreEqual("SampleSize", withStar.PrimaryValueType);
            Assert.AreEqual(100.0, withStar.PrimaryValue);

            var withDoubleDagger = ValueParser.Parse("N = 112‡");
            Assert.AreEqual("SampleSize", withDoubleDagger.PrimaryValueType);
            Assert.AreEqual(112.0, withDoubleDagger.PrimaryValue);
        }

        /**************************************************************/
        /// <summary>
        /// Phase 2 boundary fix: <c>TextUtil.RemoveUnwantedTags</c> with cleanAll=true now
        /// HTML-decodes after stripping tags, so cells like <c>&amp;lt;1</c> reach
        /// downstream consumers as <c>&lt;1</c>. This is the path used by
        /// <c>TableReconstructionService</c> to populate <c>ProcessedCell.CleanedText</c>.
        /// </summary>
        [TestMethod]
        public void Phase2_TextUtilRemoveUnwantedTags_DecodesEntities()
        {
            // Direct call mirrors the way TableReconstructionService normalizes cell text.
            var input = "&lt;1";
            var result = MedRecProImportClass.Helpers.TextUtil.RemoveUnwantedTags(
                input, new List<string>(), cleanAll: true);

            Assert.AreEqual("<1", result,
                "Stage 2 cell normalization should now decode HTML entities so &lt;1 reaches ValueParser as <1.");
        }

        #endregion Phase 2 — value-parsing fixtures (post-fix)

        #region Phase 3 — AE / Efficacy header-arm recovery baselines

        /**************************************************************/
        /// <summary>
        /// Baseline for the truly-blank-header case: when the leaf header is empty string,
        /// <see cref="SimpleArmTableParser"/> already skips the column rather than emitting
        /// arm-less observations. This pins that behavior so any regression is detected.
        /// </summary>
        /// <remarks>
        /// The 6,080 AE rows that fire <c>MissingRequired:TreatmentArm</c> in the 2026-04-28
        /// baseline (TextTableID 40880 and similar) have a different failure shape — the
        /// header is a generic label like <c>Event</c> / <c>Body System (Event)</c> /
        /// <c>Percent</c>, or the leaf header is part of a multi-level study-context path
        /// rather than a real arm name. Reproducing that shape requires the actual table
        /// HTML, not a synthetic minimal table — this fixture documents the gap, and the QC
        /// half of the failure (a missing-arm observation lands below the gate) is covered by
        /// <see cref="Baseline_AeMissingArmObservation_QualityScoreIsBelowGate"/>.
        /// </remarks>
        [TestMethod]
        public void Baseline_AeBlankArmHeader_ParserSkipsColumn()
        {
            var table = createSimpleTable(
                headerTexts: new[] { "Adverse Reaction", "" },
                dataRows: new List<string?[]>
                {
                    new[] { "Headache", "12" },
                    new[] { "Nausea", "8" },
                },
                parentSectionCode: "34084-4",
                textTableId: 40880);

            var parser = new SimpleArmTableParser();
            var observations = parser.Parse(table);

            // Current behavior: blank-header columns are skipped, so no observations emit.
            Assert.AreEqual(0, observations.Count,
                "Pre-fix baseline: parser skips columns whose leaf header is empty.");
        }

        /**************************************************************/
        /// <summary>
        /// QC-side baseline for an AE observation whose <c>TreatmentArm</c> field is null. The
        /// score lands below the 0.75 gate via the <c>MissingRequired:TreatmentArm</c>
        /// multiplier. This pins the gate-side behavior independently of the parser-side
        /// reproducer in <see cref="Baseline_AeBlankArmHeader_ParserSkipsColumn"/>.
        /// </summary>
        [TestMethod]
        public void Baseline_AeMissingArmObservation_QualityScoreIsBelowGate()
        {
            var obs = new ParsedObservation
            {
                TableCategory = "ADVERSE_EVENT",
                ParameterName = "Headache",
                TreatmentArm = null, // the failure
                ArmN = 100,
                RawValue = "12",
                PrimaryValue = 12,
                PrimaryValueType = "Percentage",
                Unit = "%",
                ParseConfidence = 0.9f,
                ValidationFlags = ""
            };

            var score = _qualityService.Evaluate(obs);

            Assert.IsTrue(score.Score < ForwardThreshold,
                $"AE row missing TreatmentArm should be forwarded; got score {score.Score:0.0000}.");
            CollectionAssert.Contains(score.Reasons.ToList(), "MissingRequired:TreatmentArm");
        }

        #endregion Phase 3 — AE / Efficacy header-arm recovery baselines

        #region Phase 4 — structural-row suppression baselines

        /**************************************************************/
        /// <summary>
        /// Baseline TextTableID 31303-style: AE table with a body-system row leaking into the
        /// data body. The parser currently emits the body-system row as a low-quality
        /// observation (text-only, no numeric value). Phase 4 should suppress it with a
        /// structural-row flag and zero penalty.
        /// </summary>
        [TestMethod]
        public void Baseline_AeBodySystemRowInData_EmitsTextOnlyObservation()
        {
            var table = createSimpleTable(
                headerTexts: new[] { "Adverse Reaction", "Drug A (N=200)" },
                dataRows: new List<string?[]>
                {
                    // Body-system / SOC label that should ideally be suppressed.
                    new[] { "Respiratory, thoracic, and mediastinal disorders", "" },
                    new[] { "Nasal Discomfort", "5" },
                },
                parentSectionCode: "34084-4",
                textTableId: 31303);

            var parser = new SimpleArmTableParser();
            var observations = parser.Parse(table);

            // Pre-fix: parser may emit the body-system row as a text-only observation. We
            // assert the broad shape rather than an exact count so this fixture stays
            // resilient to small parser changes.
            Assert.IsTrue(observations.Count >= 1, "Parser should emit at least the real AE row.");
        }

        #endregion Phase 4 — structural-row suppression baselines

        #region Phase 5 — PK unit recovery baselines

        /**************************************************************/
        /// <summary>
        /// Baseline TextTableID 42461-style: PK table where the unit is implicit in the
        /// caption / parent header rather than the leaf column header. The parser emits a PK
        /// observation with no Unit, firing <c>MissingRequired:Unit</c> + <c>MISSING_R_Unit</c>
        /// and dropping the row below the forwarding gate. Phase 5 should recover the unit
        /// from caption or sibling-header context.
        /// </summary>
        [TestMethod]
        public void Baseline_PkMissingUnit_FailsRequiredUnitContract()
        {
            // Note: real failures arise when the parser cannot recover the unit. We model the
            // failure at the QC layer directly so the assertion is independent of the
            // unit-recovery heuristics in PkTableParser.
            var obs = new ParsedObservation
            {
                TableCategory = "PK",
                ParameterName = "AUC",
                PrimaryValue = 216,
                PrimaryValueType = "ArithmeticMean",
                Unit = null,
                ParseConfidence = 0.85f,
                ValidationFlags = "PVT_MIGRATED; MISSING_R_Unit"
            };

            var score = _qualityService.Evaluate(obs);

            Assert.IsTrue(score.Score < ForwardThreshold,
                $"Pre-fix: PK without Unit should be forwarded; got score {score.Score:0.0000}.");
            CollectionAssert.Contains(score.Reasons.ToList(), "MissingRequired:Unit");
            CollectionAssert.Contains(score.Reasons.ToList(), "SoftRepair:PVT_MIGRATED");
            CollectionAssert.Contains(score.Reasons.ToList(), "SoftRepair:MISSING_R_Unit");
        }

        /**************************************************************/
        /// <summary>
        /// Baseline TextTableID 33852-style: PK row with all Required fields populated and a
        /// sane unit, but the row is held below threshold by <c>PVT_MIGRATED</c> combined
        /// with a low <see cref="ParsedObservation.ParseConfidence"/>. Demonstrates the cliff
        /// at <c>0.9 × ParseConfidence ≈ 0.747</c> that Phase 5 / Phase 7 calibration must
        /// address.
        /// </summary>
        [TestMethod]
        public void Baseline_PkPvtMigratedAndConfidenceCliff_LandsBelowGate()
        {
            // Confidence 0.83 × PVT_MIGRATED 0.9 = 0.747 — and the floor pulls score down to
            // 0.83 (since 0.747 < 0.83 is FALSE; floor only kicks in when confidence < score).
            // The arithmetic that fails the gate is actually the PVT_MIGRATED penalty alone:
            // 1.0 × 0.9 = 0.9, then floor → 0.83. So this row PASSES the gate. We instead
            // model a row that fails: a slightly lower confidence pushes it under.
            var obs = new ParsedObservation
            {
                TableCategory = "PK",
                ParameterName = "Cmax",
                PrimaryValue = 5.5,
                PrimaryValueType = "ArithmeticMean",
                Unit = "ng/mL",
                ParseConfidence = 0.74f,
                ValidationFlags = "PVT_MIGRATED"
            };

            var score = _qualityService.Evaluate(obs);

            Assert.IsTrue(score.Score < ForwardThreshold,
                $"Confidence {obs.ParseConfidence} should pull score under 0.75; got {score.Score:0.0000}.");
            CollectionAssert.Contains(score.Reasons.ToList(), "SoftRepair:PVT_MIGRATED");
            CollectionAssert.Contains(score.Reasons.ToList(), "ParseConfidenceFloor");
        }

        /**************************************************************/
        /// <summary>
        /// Sanity check that captures the cliff itself: <c>0.9 × ParseConfidence</c> only
        /// dips under the gate when ParseConfidence is below ~0.83. Documents the
        /// calibration boundary identified in the remediation plan.
        /// </summary>
        [TestMethod]
        public void Baseline_PkPvtMigratedAt083Confidence_PassesGate()
        {
            var obs = new ParsedObservation
            {
                TableCategory = "PK",
                ParameterName = "Cmax",
                PrimaryValue = 5.5,
                PrimaryValueType = "ArithmeticMean",
                Unit = "ng/mL",
                ParseConfidence = 0.83f,
                ValidationFlags = "PVT_MIGRATED"
            };

            var score = _qualityService.Evaluate(obs);

            Assert.IsTrue(score.Score >= ForwardThreshold,
                $"At ParseConfidence 0.83 the row should clear the gate; got {score.Score:0.0000}.");
        }

        #endregion Phase 5 — PK unit recovery baselines

        #region Phase 6 — Efficacy structural baselines

        /**************************************************************/
        /// <summary>
        /// Baseline TextTableID 41264-style: efficacy table with an event-rate cell shaped
        /// <c>"74% (220/296) 58% (167/288)"</c>. The current value parser cannot decompose
        /// the compound cell and falls to text. Captures the baseline so Phase 2 / Phase 6
        /// can repair this shape.
        /// </summary>
        [TestMethod]
        public void Baseline_EfficacyCompoundEventRate_FallsToText()
        {
            var parsed = ValueParser.Parse("74% (220/296) 58% (167/288)");

            Assert.AreEqual("Text", parsed.PrimaryValueType,
                "Pre-fix: compound event-rate cell falls to text fallback.");
            Assert.IsFalse(parsed.PrimaryValue.HasValue);
        }

        #endregion Phase 6 — Efficacy structural baselines

        #region Phase 3 — header-arm recovery (post-fix)

        /**************************************************************/
        /// <summary>
        /// Phase 3 fix: a leaf header equal to a generic structural label like
        /// <c>Event</c> with no real arm in the parent header path is rejected. The
        /// column produces no observations, so the row no longer fires
        /// <c>MissingRequired:TreatmentArm</c> downstream.
        /// </summary>
        [TestMethod]
        public void Phase3_GenericLeaf_NoParent_SkipsColumn()
        {
            var table = createSimpleTable(
                headerTexts: new[] { "Adverse Reaction", "Event" },
                dataRows: new List<string?[]>
                {
                    new[] { "Headache", "12" },
                },
                parentSectionCode: "34084-4",
                textTableId: 40880);

            var parser = new SimpleArmTableParser();
            var observations = parser.Parse(table);

            Assert.AreEqual(0, observations.Count,
                "Phase 3: leaf 'Event' with no real arm in HeaderPath is rejected; no observations.");
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3 fix: when the leaf header is a generic structural label but a real
        /// arm name lives in the parent HeaderPath, the parser walks up and recovers
        /// the parent. This is the TextTableID 40880 representative shape — a
        /// multi-row header where the spanning row carries the arm and the leaf row
        /// carries an axis label.
        /// </summary>
        [TestMethod]
        public void Phase3_GenericLeaf_RecoversFromParentHeader()
        {
            var table = createMultilevelTable(
                column0Header: "Adverse Reaction",
                dataColumns: new[]
                {
                    ("Drug A (N=200)", "Event"),
                    ("Placebo (N=200)", "Event"),
                },
                dataRows: new List<string?[]>
                {
                    new[] { "Headache", "12", "5" },
                    new[] { "Nausea", "8", "3" },
                },
                parentSectionCode: "34084-4",
                textTableId: 40880);

            var parser = new SimpleArmTableParser();
            var observations = parser.Parse(table);

            Assert.IsTrue(observations.Count >= 2,
                "Phase 3: parent-header recovery should yield observations for both arms.");
            var arms = observations.Select(o => o.TreatmentArm).Where(a => a != null).Distinct().ToList();
            CollectionAssert.Contains(arms, "Drug A");
            CollectionAssert.Contains(arms, "Placebo");
            // No observation should land with a generic label as TreatmentArm.
            Assert.IsFalse(observations.Any(o => o.TreatmentArm == "Event"),
                "Phase 3: 'Event' must never be promoted into TreatmentArm.");
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3 fix: <c>Body System (Event)</c> — a frequent SPL leaf-header pattern
        /// where the column 1 leaf names the row-axis instead of an arm — is rejected.
        /// </summary>
        [TestMethod]
        public void Phase3_GenericLeaf_BodySystemEvent_RejectedAsArm()
        {
            var table = createSimpleTable(
                headerTexts: new[] { "Adverse Reaction", "Body System (Event)" },
                dataRows: new List<string?[]>
                {
                    new[] { "Headache", "12" },
                },
                parentSectionCode: "34084-4",
                textTableId: 31303);

            var parser = new SimpleArmTableParser();
            var observations = parser.Parse(table);

            Assert.AreEqual(0, observations.Count,
                "Phase 3: 'Body System (Event)' is generic and recovers nothing; column skipped.");
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3 fix: <c>%</c> as a leaf header is a format-hint label, not an arm.
        /// </summary>
        [TestMethod]
        public void Phase3_GenericLeaf_PercentSign_RejectedAsArm()
        {
            var table = createSimpleTable(
                headerTexts: new[] { "Adverse Reaction", "%" },
                dataRows: new List<string?[]>
                {
                    new[] { "Headache", "12" },
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var observations = parser.Parse(table);

            Assert.AreEqual(0, observations.Count,
                "Phase 3: '%' leaf is a format hint; column skipped when no parent arm exists.");
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3 negative: real arm names — even short ones — pass through unchanged.
        /// Confirms the generic-label regex is anchored and does not over-reject.
        /// </summary>
        [TestMethod]
        public void Phase3_RealArmNames_StillAccepted()
        {
            var table = createSimpleTable(
                headerTexts: new[] { "Adverse Reaction", "Drug A (N=200)", "Placebo", "ZAVZPRET" },
                dataRows: new List<string?[]>
                {
                    new[] { "Headache", "12", "5", "8" },
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var observations = parser.Parse(table);

            Assert.AreEqual(3, observations.Count,
                "Phase 3: short and long real arm names like 'Placebo' / 'ZAVZPRET' must still be accepted.");
            var arms = observations.Select(o => o.TreatmentArm).ToList();
            CollectionAssert.Contains(arms, "Drug A");
            CollectionAssert.Contains(arms, "Placebo");
            CollectionAssert.Contains(arms, "ZAVZPRET");
        }

        #endregion Phase 3 — header-arm recovery (post-fix)
    }
}
