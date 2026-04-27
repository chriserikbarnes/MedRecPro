using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;

namespace MedRecProConsole.Services.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Computes Dosing-table contract compliance metrics from a Stage 3
    /// standardization JSONL audit file (the NDJSON output produced by
    /// <see cref="TableStandardizationJsonWriter"/>) and emits a Markdown
    /// report. Used to capture the failing baseline before parser changes
    /// land and to measure deltas after.
    /// </summary>
    /// <remarks>
    /// ## Metrics emitted
    /// All metrics are scoped to rows whose <c>category == "dosing"</c>.
    ///
    /// | Metric | Definition |
    /// |---|---|
    /// | Complete comparison key | <c>ParameterName</c>, <c>Population</c>, AND <c>DoseRegimen</c> all non-null |
    /// | Missing DoseRegimen | <c>DoseRegimen</c> is null/whitespace |
    /// | Missing Population | <c>Population</c> is null/whitespace |
    /// | Missing ParameterName | <c>ParameterName</c> is null/whitespace |
    /// | Non-Numeric PrimaryValueType | <c>PrimaryValueType</c> is anything other than "Numeric" |
    /// | UNIT_HEADER_LEAK rate | <c>ValidationFlags</c> contains <c>UNIT_HEADER_LEAK</c> |
    /// | Raw-dose-but-no-DoseRegimen | <see cref="DoseExtractor.Extract"/> on <c>RawValue</c> returns a dose AND <c>DoseRegimen</c> is null |
    /// | Tables with zero DoseRegimen rows | Distinct <c>TextTableID</c> values where every Dosing row has null <c>DoseRegimen</c> |
    /// | Tables routed away from DOSING | Tables whose section code is 34068-7 but whose category is now <c>TEXT_DESCRIPTIVE</c> or <c>OTHER</c> (validator downgrade visibility) |
    ///
    /// ## Usage
    /// <code>
    /// var report = DosingComplianceReport.BuildReportFromJsonl("standardization-report-20260427-143834.jsonl");
    /// File.WriteAllText("dosing-compliance-report.md", report);
    /// </code>
    /// </remarks>
    /// <seealso cref="TableStandardizationJsonWriter"/>
    /// <seealso cref="DoseExtractor"/>
    public static class DosingComplianceReport
    {
        #region Serializer

        /**************************************************************/
        /// <summary>
        /// Mirrors <see cref="TableStandardizationJsonWriter"/> options so the
        /// JSONL it produces round-trips into the local payload type.
        /// </summary>
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };

        #endregion

        #region Public Methods

        /**************************************************************/
        /// <summary>
        /// Reads a Stage 3 standardization JSONL file from disk and returns
        /// the rendered Markdown compliance report.
        /// </summary>
        /// <param name="jsonlPath">Path to the NDJSON file (one observation per line).</param>
        /// <returns>Markdown report ready to write to disk or stdout.</returns>
        /// <exception cref="FileNotFoundException">When <paramref name="jsonlPath"/> does not exist.</exception>
        public static string BuildReportFromJsonl(string jsonlPath)
        {
            #region implementation

            if (!File.Exists(jsonlPath))
                throw new FileNotFoundException("JSONL audit not found.", jsonlPath);

            var rows = readJsonl(jsonlPath);
            return BuildReport(rows, jsonlPath);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes metrics over a pre-loaded sequence of payload rows and
        /// renders the Markdown report. Exposed for unit tests that do not
        /// want to round-trip through a file on disk.
        /// </summary>
        /// <param name="rows">Deserialized payload rows.</param>
        /// <param name="sourceLabel">Display label for the report header (file
        /// path or freeform name). Pass null when rendering an unattributed
        /// report.</param>
        /// <returns>Markdown report.</returns>
        public static string BuildReport(IEnumerable<CompliancePayload> rows, string? sourceLabel)
        {
            #region implementation

            var metrics = computeMetrics(rows);
            return renderMarkdown(metrics, sourceLabel);

            #endregion
        }

        #endregion

        #region Metric Computation

        /**************************************************************/
        /// <summary>
        /// Aggregates all metrics in a single pass over the payload rows.
        /// </summary>
        private static ComplianceMetrics computeMetrics(IEnumerable<CompliancePayload> rows)
        {
            #region implementation

            var m = new ComplianceMetrics();
            var dosingTablesWithRegimen = new HashSet<int>();
            var dosingTablesAll = new HashSet<int>();
            var downgradedTablesByCategory = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in rows)
            {
                m.TotalRows++;

                var category = r.Category?.Trim() ?? string.Empty;

                // Track 34068-7 tables that ended up in a non-Dosing category — that
                // is the downgrade signal we want to surface in the report.
                if (string.Equals(r.ParentSectionCode, "34068-7", StringComparison.Ordinal)
                    && !string.Equals(category, "dosing", StringComparison.OrdinalIgnoreCase)
                    && r.TextTableId.HasValue)
                {
                    if (!downgradedTablesByCategory.TryGetValue(category, out var set))
                    {
                        set = new HashSet<int>();
                        downgradedTablesByCategory[category] = set;
                    }
                    set.Add(r.TextTableId.Value);
                }

                if (!string.Equals(category, "dosing", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (r.TextTableId.HasValue)
                    dosingTablesAll.Add(r.TextTableId.Value);

                // Meta lines (zero observations) carry no observation payload
                if (r.Observation == null)
                    continue;

                m.DosingRows++;

                var obs = r.Observation;
                var hasParameterName = !string.IsNullOrWhiteSpace(obs.ParameterName);
                var hasPopulation = !string.IsNullOrWhiteSpace(obs.Population);
                var hasDoseRegimen = !string.IsNullOrWhiteSpace(obs.DoseRegimen);

                if (!hasParameterName) m.MissingParameterName++;
                if (!hasPopulation) m.MissingPopulation++;
                if (!hasDoseRegimen) m.MissingDoseRegimen++;

                if (hasParameterName && hasPopulation && hasDoseRegimen)
                    m.CompleteComparisonKey++;

                if (!string.Equals(obs.PrimaryValueType, "Numeric", StringComparison.OrdinalIgnoreCase))
                    m.NonNumericPrimaryValueType++;

                if (!string.IsNullOrEmpty(obs.ValidationFlags)
                    && obs.ValidationFlags.Contains("UNIT_HEADER_LEAK", StringComparison.Ordinal))
                {
                    m.UnitHeaderLeak++;
                }

                if (!hasDoseRegimen && !string.IsNullOrWhiteSpace(obs.RawValue))
                {
                    var (dose, _) = DoseExtractor.Extract(obs.RawValue);
                    if (dose.HasValue)
                        m.RawDoseWithoutDoseRegimen++;
                }

                if (hasDoseRegimen && r.TextTableId.HasValue)
                    dosingTablesWithRegimen.Add(r.TextTableId.Value);
            }

            m.DosingTables = dosingTablesAll.Count;
            m.DosingTablesWithDoseRegimen = dosingTablesWithRegimen.Count;
            m.DosingTablesWithoutDoseRegimen = dosingTablesAll.Count - dosingTablesWithRegimen.Count;

            foreach (var kv in downgradedTablesByCategory.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                m.DowngradedTablesByCategory[kv.Key] = kv.Value.Count;
            }

            return m;

            #endregion
        }

        #endregion

        #region Markdown Rendering

        /**************************************************************/
        /// <summary>
        /// Renders the metrics struct into a scannable Markdown report.
        /// </summary>
        private static string renderMarkdown(ComplianceMetrics m, string? sourceLabel)
        {
            #region implementation

            var sb = new StringBuilder();

            sb.AppendLine("# Dosing Compliance Report");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(sourceLabel))
            {
                sb.AppendLine($"Source: `{sourceLabel}`");
                sb.AppendLine();
            }

            sb.AppendLine($"- Total observations across all categories: **{m.TotalRows:N0}**");
            sb.AppendLine($"- Dosing observations: **{m.DosingRows:N0}**");
            sb.AppendLine($"- Distinct Dosing tables: **{m.DosingTables:N0}**");
            sb.AppendLine();

            sb.AppendLine("## Comparison Key Compliance");
            sb.AppendLine();
            sb.AppendLine("Comparison key per [column-contracts.md](../MedRecProImportClass/TableStandards/column-contracts.md): `ParameterName + Population + DoseRegimen`.");
            sb.AppendLine();
            sb.AppendLine("| Metric | Count | Rate |");
            sb.AppendLine("|---|---:|---:|");
            sb.AppendLine($"| Complete comparison key | {m.CompleteComparisonKey:N0} | {pct(m.CompleteComparisonKey, m.DosingRows)} |");
            sb.AppendLine($"| Missing ParameterName | {m.MissingParameterName:N0} | {pct(m.MissingParameterName, m.DosingRows)} |");
            sb.AppendLine($"| Missing Population | {m.MissingPopulation:N0} | {pct(m.MissingPopulation, m.DosingRows)} |");
            sb.AppendLine($"| Missing DoseRegimen | {m.MissingDoseRegimen:N0} | {pct(m.MissingDoseRegimen, m.DosingRows)} |");
            sb.AppendLine();

            sb.AppendLine("## Value & Flag Compliance");
            sb.AppendLine();
            sb.AppendLine("| Metric | Count | Rate |");
            sb.AppendLine("|---|---:|---:|");
            sb.AppendLine($"| Non-Numeric PrimaryValueType | {m.NonNumericPrimaryValueType:N0} | {pct(m.NonNumericPrimaryValueType, m.DosingRows)} |");
            sb.AppendLine($"| UNIT_HEADER_LEAK flagged | {m.UnitHeaderLeak:N0} | {pct(m.UnitHeaderLeak, m.DosingRows)} |");
            sb.AppendLine($"| Raw-dose without DoseRegimen | {m.RawDoseWithoutDoseRegimen:N0} | {pct(m.RawDoseWithoutDoseRegimen, m.DosingRows)} |");
            sb.AppendLine();

            sb.AppendLine("## Table-Level Compliance");
            sb.AppendLine();
            sb.AppendLine("| Metric | Count | Rate |");
            sb.AppendLine("|---|---:|---:|");
            sb.AppendLine($"| Tables with at least one DoseRegimen row | {m.DosingTablesWithDoseRegimen:N0} | {pct(m.DosingTablesWithDoseRegimen, m.DosingTables)} |");
            sb.AppendLine($"| Tables with zero DoseRegimen rows | {m.DosingTablesWithoutDoseRegimen:N0} | {pct(m.DosingTablesWithoutDoseRegimen, m.DosingTables)} |");
            sb.AppendLine();

            sb.AppendLine("## Validator Downgrades (34068-7 → non-Dosing)");
            sb.AppendLine();
            if (m.DowngradedTablesByCategory.Count == 0)
            {
                sb.AppendLine("_No 34068-7 tables routed away from DOSING in this run._");
            }
            else
            {
                sb.AppendLine("Distinct tables with `parentSectionCode == 34068-7` whose final category is **not** `dosing`. These are the tables `validateDosingOrDowngrade` removed from the Dosing contract denominator.");
                sb.AppendLine();
                sb.AppendLine("| Final category | Tables |");
                sb.AppendLine("|---|---:|");
                foreach (var kv in m.DowngradedTablesByCategory)
                {
                    sb.AppendLine($"| `{kv.Key}` | {kv.Value:N0} |");
                }
            }
            sb.AppendLine();

            return sb.ToString();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats <paramref name="num"/> / <paramref name="den"/> as a
        /// percentage with one decimal place. Returns the literal string
        /// <c>n/a</c> when the denominator is zero so the reader sees "no
        /// data" rather than a misleading 0.0%.
        /// </summary>
        private static string pct(int num, int den)
        {
            #region implementation

            if (den <= 0)
                return "n/a";

            return ((double)num / den).ToString("P1");

            #endregion
        }

        #endregion

        #region JSONL Reader

        /**************************************************************/
        /// <summary>
        /// Streams the JSONL file line by line and yields one
        /// <see cref="CompliancePayload"/> per non-empty line. Bad lines are
        /// skipped silently — the report should not crash on a single
        /// malformed record.
        /// </summary>
        private static IEnumerable<CompliancePayload> readJsonl(string path)
        {
            #region implementation

            using var reader = new StreamReader(path);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                CompliancePayload? payload = null;
                try
                {
                    payload = JsonSerializer.Deserialize<CompliancePayload>(line, _serializerOptions);
                }
                catch (JsonException)
                {
                    // Tolerate occasional malformed lines.
                    continue;
                }

                if (payload != null)
                    yield return payload;
            }

            #endregion
        }

        #endregion

        #region Payload Types

        /**************************************************************/
        /// <summary>
        /// Minimal projection of <see cref="TableStandardizationJsonWriter"/>'s
        /// JSONL line shape — only the fields the compliance report consumes.
        /// </summary>
        public sealed class CompliancePayload
        {
            /// <summary>TextTableID — primary lookup key.</summary>
            public int? TextTableId { get; set; }

            /// <summary>LOINC-style parent section code.</summary>
            public string? ParentSectionCode { get; set; }

            /// <summary>Routing category as a lowercase string ("dosing", "pk", "text_descriptive", etc.).</summary>
            public string? Category { get; set; }

            /// <summary>Number of observations the parser produced.</summary>
            public int ObservationCount { get; set; }

            /// <summary>The full ParsedObservation payload, or null for meta lines (zero observations).</summary>
            public ParsedObservation? Observation { get; set; }
        }

        /**************************************************************/
        /// <summary>
        /// Aggregated counters produced by <see cref="computeMetrics"/>. Public
        /// so unit tests can assert on individual fields without parsing the
        /// rendered Markdown.
        /// </summary>
        public sealed class ComplianceMetrics
        {
            public int TotalRows { get; set; }
            public int DosingRows { get; set; }
            public int DosingTables { get; set; }
            public int DosingTablesWithDoseRegimen { get; set; }
            public int DosingTablesWithoutDoseRegimen { get; set; }

            public int CompleteComparisonKey { get; set; }
            public int MissingParameterName { get; set; }
            public int MissingPopulation { get; set; }
            public int MissingDoseRegimen { get; set; }

            public int NonNumericPrimaryValueType { get; set; }
            public int UnitHeaderLeak { get; set; }
            public int RawDoseWithoutDoseRegimen { get; set; }

            public Dictionary<string, int> DowngradedTablesByCategory { get; } =
                new(StringComparer.OrdinalIgnoreCase);
        }

        #endregion
    }
}
