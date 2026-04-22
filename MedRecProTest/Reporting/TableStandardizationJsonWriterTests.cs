using System.Text.Json;
using MedRecProConsole.Services.Reporting;
using MedRecProImportClass.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Smoke-level tests for <see cref="TableStandardizationJsonWriter"/>. Confirms that
    /// the NDJSON companion dump is valid JSON, emits one line per observation, carries
    /// every ParsedObservation column the markdown formatter omits (ParameterSubtype,
    /// Timepoint, Population, DoseRegimen, Dose, DoseUnit, Unit, ValidationFlags), and
    /// maintains row-count parity with the markdown observations table.
    /// </summary>
    /// <seealso cref="TableStandardizationJsonWriter"/>
    /// <seealso cref="TableReportEntry"/>
    [TestClass]
    public class TableStandardizationJsonWriterTests
    {
        #region Helper Methods

        /**************************************************************/
        /// <summary>
        /// Builds a minimal PK table matching the fixture shape used by the markdown-writer tests
        /// so side-by-side comparisons stay apples-to-apples.
        /// </summary>
        private static ReconstructedTable buildMinimalTable(int textTableId = 24820,
            string caption = "Table 3: PK parameters")
        {
            #region implementation

            return new ReconstructedTable
            {
                TextTableID = textTableId,
                Caption = caption,
                ParentSectionCode = "34090-1",
                SectionTitle = "12.3 Pharmacokinetics",
                TotalColumnCount = 3,
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
                Rows = new List<ReconstructedRow>()
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a ParsedObservation populated with the fields that R6/R7 routing affects —
        /// the ones the markdown formatter does NOT emit. Each test asserts these survive
        /// JSON serialization.
        /// </summary>
        private static ParsedObservation buildFullObservation(int rowSeq = 1, int cellSeq = 1,
            string parameter = "Cmax",
            string? subtype = "single_dose",
            string? timepoint = "Day 1",
            string? population = "Healthy",
            string? doseRegimen = "500 mg oral",
            double? dose = 500,
            string? doseUnit = "mg",
            string? unit = "mcg/mL",
            string? flags = "PK_SECTION_QUALIFIER_APPLIED")
        {
            #region implementation

            return new ParsedObservation
            {
                TextTableID = 24820,
                SourceRowSeq = rowSeq,
                SourceCellSeq = cellSeq,
                ParameterName = parameter,
                ParameterSubtype = subtype,
                Timepoint = timepoint,
                Population = population,
                DoseRegimen = doseRegimen,
                Dose = dose.HasValue ? (decimal?)dose.Value : null,
                DoseUnit = doseUnit,
                Unit = unit,
                RawValue = "9044 (20)",
                PrimaryValue = 9044,
                PrimaryValueType = "Mean",
                ParseConfidence = 1.0,
                ParseRule = "caption_mean_cv_percent",
                ValidationFlags = flags
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses each NDJSON line of <paramref name="ndjson"/> and returns the resulting
        /// <see cref="JsonDocument"/>s. Line endings are normalized to Unix for portability.
        /// </summary>
        private static List<JsonDocument> parseLines(string ndjson)
        {
            #region implementation

            var docs = new List<JsonDocument>();
            foreach (var line in ndjson.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                docs.Add(JsonDocument.Parse(line));
            }
            return docs;

            #endregion
        }

        #endregion

        #region Format Tests

        /**************************************************************/
        /// <summary>
        /// A single-observation entry produces exactly one NDJSON line, valid JSON, with
        /// expected table-level context fields and an inlined observation object.
        /// </summary>
        [TestMethod]
        public void BuildSection_SingleObservation_ProducesOneValidJsonLine()
        {
            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { buildFullObservation() },
                ClaudeSkipped = true
            };

            var ndjson = TableStandardizationJsonWriter.BuildSection(entry);

            var lines = parseLines(ndjson);
            Assert.AreEqual(1, lines.Count, "Exactly one JSON line should be emitted");

            var root = lines[0].RootElement;
            Assert.AreEqual(24820, root.GetProperty("textTableId").GetInt32());
            Assert.AreEqual("Table 3: PK parameters", root.GetProperty("caption").GetString());
            Assert.AreEqual("34090-1", root.GetProperty("parentSectionCode").GetString());
            Assert.AreEqual("pk", root.GetProperty("category").GetString(),
                "Enum serializer uses camelCase policy for enum names");
            Assert.AreEqual("PkTableParser", root.GetProperty("parser").GetString());
            Assert.AreEqual(1, root.GetProperty("observationCount").GetInt32());
            Assert.IsTrue(root.GetProperty("claudeSkipped").GetBoolean());

            var obs = root.GetProperty("observation");
            Assert.AreEqual("Cmax", obs.GetProperty("parameterName").GetString());
        }

        /**************************************************************/
        /// <summary>
        /// The JSON payload surfaces the columns the markdown writer omits — R6/R7 verification
        /// depends on each of these being queryable directly: ParameterSubtype, Timepoint,
        /// Population, DoseRegimen, Dose, DoseUnit, Unit, ValidationFlags.
        /// </summary>
        [TestMethod]
        public void BuildSection_Observation_CarriesColumnsOmittedByMarkdownWriter()
        {
            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { buildFullObservation() },
                ClaudeSkipped = true
            };

            var ndjson = TableStandardizationJsonWriter.BuildSection(entry);
            var root = parseLines(ndjson)[0].RootElement;
            var obs = root.GetProperty("observation");

            Assert.AreEqual("single_dose", obs.GetProperty("parameterSubtype").GetString());
            Assert.AreEqual("Day 1", obs.GetProperty("timepoint").GetString());
            Assert.AreEqual("Healthy", obs.GetProperty("population").GetString());
            Assert.AreEqual("500 mg oral", obs.GetProperty("doseRegimen").GetString());
            Assert.AreEqual(500m, obs.GetProperty("dose").GetDecimal());
            Assert.AreEqual("mg", obs.GetProperty("doseUnit").GetString());
            Assert.AreEqual("mcg/mL", obs.GetProperty("unit").GetString());
            Assert.AreEqual("PK_SECTION_QUALIFIER_APPLIED", obs.GetProperty("validationFlags").GetString());
        }

        /**************************************************************/
        /// <summary>
        /// Multiple observations produce one line per observation, each with the same
        /// table-level context repeated (flat shape for easy jq filtering). Line count
        /// matches the markdown writer's observation-table row count.
        /// </summary>
        [TestMethod]
        public void BuildSection_MultipleObservations_OneLinePerObservation_RowCountParity()
        {
            var observations = new[]
            {
                buildFullObservation(rowSeq: 1, cellSeq: 1, parameter: "Cmax"),
                buildFullObservation(rowSeq: 1, cellSeq: 2, parameter: "AUC"),
                buildFullObservation(rowSeq: 2, cellSeq: 1, parameter: "Tmax",
                    subtype: "multiple_dose", timepoint: "Day 7")
            };

            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = observations,
                ClaudeSkipped = true
            };

            var ndjson = TableStandardizationJsonWriter.BuildSection(entry);
            var lines = parseLines(ndjson);

            Assert.AreEqual(3, lines.Count, "One JSON line per observation");

            var paramNames = lines
                .Select(d => d.RootElement.GetProperty("observation").GetProperty("parameterName").GetString())
                .ToArray();
            CollectionAssert.AreEqual(new[] { "Cmax", "AUC", "Tmax" }, paramNames);

            // Every line repeats the table-level textTableId (flat shape).
            Assert.IsTrue(lines.All(d => d.RootElement.GetProperty("textTableId").GetInt32() == 24820),
                "Each line carries the TextTableID at the top level");

            // observationCount on every line reports the total, not the position —
            // this makes per-table aggregates easy without a second pass.
            Assert.IsTrue(lines.All(d => d.RootElement.GetProperty("observationCount").GetInt32() == 3),
                "observationCount reports the total observations for the table");
        }

        /**************************************************************/
        /// <summary>
        /// A zero-observation entry emits exactly one meta line with <c>observation: null</c>
        /// and <c>observationCount: 0</c>, so routing decisions and skipped tables remain
        /// visible in the JSON log.
        /// </summary>
        [TestMethod]
        public void BuildSection_ZeroObservations_EmitsOneMetaLine()
        {
            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(),
                Category = TableCategory.SKIP,
                ParserName = null,
                Observations = Array.Empty<ParsedObservation>(),
                ClaudeSkipped = true
            };

            var ndjson = TableStandardizationJsonWriter.BuildSection(entry);
            var lines = parseLines(ndjson);

            Assert.AreEqual(1, lines.Count, "Exactly one meta line for a zero-observation table");

            var root = lines[0].RootElement;
            Assert.AreEqual(24820, root.GetProperty("textTableId").GetInt32());
            Assert.AreEqual("skip", root.GetProperty("category").GetString());
            Assert.AreEqual(JsonValueKind.Null, root.GetProperty("parser").ValueKind);
            Assert.AreEqual(0, root.GetProperty("observationCount").GetInt32());
            Assert.AreEqual(JsonValueKind.Null, root.GetProperty("observation").ValueKind);
        }

        /**************************************************************/
        /// <summary>
        /// The writer populates <c>beforeClaudeFlags</c> from the entry's snapshot dictionary,
        /// keyed per observation by <c>SourceRowSeq * 10000 + SourceCellSeq</c>. This enables
        /// jq-based diffing of Claude-applied corrections without loading both markdown and JSON.
        /// </summary>
        [TestMethod]
        public void BuildSection_BeforeClaudeFlags_ResolvedPerObservation()
        {
            var obs = buildFullObservation(rowSeq: 3, cellSeq: 7, flags: "PK_SECTION_QUALIFIER_APPLIED; AI_CORRECTED:ROUTE_ARM");
            var beforeKey = (obs.SourceRowSeq ?? 0) * 10000 + (obs.SourceCellSeq ?? 0);

            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[] { obs },
                BeforeClaudeFlags = new Dictionary<int, string?>
                {
                    [beforeKey] = "PK_SECTION_QUALIFIER_APPLIED"  // pre-Stage-3.5 snapshot (no AI_CORRECTED)
                },
                ClaudeSkipped = false
            };

            var ndjson = TableStandardizationJsonWriter.BuildSection(entry);
            var root = parseLines(ndjson)[0].RootElement;

            Assert.AreEqual("PK_SECTION_QUALIFIER_APPLIED",
                root.GetProperty("beforeClaudeFlags").GetString(),
                "Before-snapshot should surface the pre-Stage-3.5 state");
            Assert.IsFalse(root.GetProperty("claudeSkipped").GetBoolean());
        }

        /**************************************************************/
        /// <summary>
        /// Regression guard: every emitted line ends with a single <c>\n</c> so the file
        /// is valid NDJSON and <c>wc -l</c> returns the observation count.
        /// </summary>
        [TestMethod]
        public void BuildSection_EachLineTerminatedByNewline()
        {
            var entry = new TableReportEntry
            {
                Table = buildMinimalTable(),
                Category = TableCategory.PK,
                ParserName = "PkTableParser",
                Observations = new[]
                {
                    buildFullObservation(rowSeq: 1, cellSeq: 1),
                    buildFullObservation(rowSeq: 2, cellSeq: 1)
                },
                ClaudeSkipped = true
            };

            var ndjson = TableStandardizationJsonWriter.BuildSection(entry);
            var normalized = ndjson.Replace("\r\n", "\n");

            Assert.IsTrue(normalized.EndsWith("\n"), "Output must end with newline for NDJSON conformance");
            var lineCount = normalized.Count(c => c == '\n');
            Assert.AreEqual(2, lineCount, "One \\n terminator per observation");
        }

        #endregion
    }
}
