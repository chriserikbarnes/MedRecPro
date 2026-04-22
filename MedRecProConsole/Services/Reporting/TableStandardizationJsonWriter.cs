using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MedRecProImportClass.Models;

namespace MedRecProConsole.Services.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Serializes a <see cref="TableReportEntry"/> into newline-delimited JSON (NDJSON) —
    /// the structured companion format to <see cref="TableStandardizationMarkdownWriter"/>.
    /// Designed for programmatic diffing and column-level audits (e.g. verifying R6/R7
    /// routing effects on <see cref="ParsedObservation.ParameterSubtype"/>,
    /// <see cref="ParsedObservation.Timepoint"/>, <see cref="ParsedObservation.Population"/>).
    /// </summary>
    /// <remarks>
    /// ## Line shape
    /// Each observation becomes a single JSON line carrying both table-level context
    /// (<c>textTableId</c>, <c>caption</c>, <c>parentSectionCode</c>, <c>category</c>,
    /// <c>parser</c>) and the full <see cref="ParsedObservation"/> payload under the
    /// <c>observation</c> property. Flat per-observation lines make <c>jq</c> queries
    /// straightforward (no array unwrapping).
    ///
    /// Tables with zero observations emit one meta line with <c>observation: null</c>
    /// and <c>observationCount: 0</c> so routing decisions are still captured.
    ///
    /// ## Query examples (jq)
    /// <code>
    /// # All rows for a specific table
    /// jq -c 'select(.textTableId == 571)' report.jsonl
    ///
    /// # Count ParameterSubtype values across the corpus
    /// jq -c 'select(.observation != null) | .observation.parameterSubtype' report.jsonl | sort | uniq -c
    ///
    /// # All non-empty Timepoint values for PK tables
    /// jq -c 'select(.category == "PK" and .observation.timepoint != null) | {tid: .textTableId, param: .observation.parameterName, timepoint: .observation.timepoint}' report.jsonl
    /// </code>
    /// </remarks>
    /// <seealso cref="TableReportEntry"/>
    /// <seealso cref="JsonReportSink"/>
    /// <seealso cref="TableStandardizationMarkdownWriter"/>
    public static class TableStandardizationJsonWriter
    {
        #region serializer options

        /**************************************************************/
        /// <summary>
        /// Shared serializer options for NDJSON output. CamelCase matches typical JS
        /// consumers, <c>WriteIndented = false</c> keeps each record on one line,
        /// and enum-to-string avoids opaque integer categories.
        /// </summary>
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Serializes a single entry to one-or-more NDJSON lines. The returned string
        /// ends with a trailing newline so callers can concatenate consecutive entries
        /// without tracking delimiters.
        /// </summary>
        /// <param name="entry">The per-table snapshot to render.</param>
        /// <returns>Zero-or-more JSON lines, each terminated with <c>\n</c>.</returns>
        public static string BuildSection(TableReportEntry entry)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(entry);

            var sb = new StringBuilder();

            if (entry.Observations == null || entry.Observations.Count == 0)
            {
                // Emit one meta line so skipped / empty tables are still visible in
                // the JSON log — mirrors the markdown writer which emits a "No
                // observations produced." note in that case.
                writeMetaLine(sb, entry);
                return sb.ToString();
            }

            foreach (var obs in entry.Observations)
            {
                writeObservationLine(sb, entry, obs);
            }

            return sb.ToString();

            #endregion
        }

        #endregion

        #region private methods

        /**************************************************************/
        /// <summary>
        /// Writes a single observation-level JSON line. Flattens table-level context
        /// at the top so each line is self-contained for standalone <c>jq</c> queries.
        /// </summary>
        private static void writeObservationLine(StringBuilder sb, TableReportEntry entry, ParsedObservation obs)
        {
            #region implementation

            var payload = new JsonLinePayload
            {
                TextTableId = entry.Table.TextTableID,
                Caption = entry.Table.Caption,
                ParentSectionCode = entry.Table.ParentSectionCode,
                SectionTitle = entry.Table.SectionTitle,
                Category = entry.Category,
                Parser = entry.ParserName,
                ObservationCount = entry.Observations?.Count ?? 0,
                Observation = obs,
                BeforeClaudeFlags = resolveBeforeFlags(entry, obs),
                ClaudeSkipped = entry.ClaudeSkipped
            };

            sb.AppendLine(JsonSerializer.Serialize(payload, _serializerOptions));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Writes one meta line for an entry that produced zero observations. Carries
        /// the same table-level context as observation lines so downstream consumers
        /// can filter uniformly on <c>textTableId</c>, <c>category</c>, etc.
        /// </summary>
        private static void writeMetaLine(StringBuilder sb, TableReportEntry entry)
        {
            #region implementation

            var payload = new JsonLinePayload
            {
                TextTableId = entry.Table.TextTableID,
                Caption = entry.Table.Caption,
                ParentSectionCode = entry.Table.ParentSectionCode,
                SectionTitle = entry.Table.SectionTitle,
                Category = entry.Category,
                Parser = entry.ParserName,
                ObservationCount = 0,
                Observation = null,
                BeforeClaudeFlags = null,
                ClaudeSkipped = entry.ClaudeSkipped
            };

            sb.AppendLine(JsonSerializer.Serialize(payload, _serializerOptions));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Looks up the pre-Stage-3.5 <see cref="ParsedObservation.ValidationFlags"/>
        /// snapshot for this observation, when available. Keyed by
        /// <c>(SourceRowSeq * 10000 + SourceCellSeq)</c> matching the convention
        /// used by the markdown writer.
        /// </summary>
        private static string? resolveBeforeFlags(TableReportEntry entry, ParsedObservation obs)
        {
            #region implementation

            if (entry.BeforeClaudeFlags == null)
                return null;

            var key = (obs.SourceRowSeq ?? 0) * 10000 + (obs.SourceCellSeq ?? 0);
            entry.BeforeClaudeFlags.TryGetValue(key, out var before);
            return before;

            #endregion
        }

        #endregion

        #region payload shape

        /**************************************************************/
        /// <summary>
        /// Shape of each NDJSON line. Flattens table-level context at the top so each
        /// record is self-contained for <c>jq</c>-style filtering.
        /// </summary>
        /// <remarks>
        /// Property ordering is intentional — most-filtered fields (<c>textTableId</c>,
        /// <c>category</c>) come first so line prefixes are scannable by eye in the
        /// raw file.
        /// </remarks>
        private sealed class JsonLinePayload
        {
            /// <summary>TextTableID — primary lookup key.</summary>
            public int? TextTableId { get; set; }

            /// <summary>Caption (truncated client-side if needed).</summary>
            public string? Caption { get; set; }

            /// <summary>LOINC-style parent section code.</summary>
            public string? ParentSectionCode { get; set; }

            /// <summary>Parent section display title.</summary>
            public string? SectionTitle { get; set; }

            /// <summary>Routing category (PK, PD, DDI, SKIP, …).</summary>
            public TableCategory Category { get; set; }

            /// <summary>Parser name, or null when the table was skipped.</summary>
            public string? Parser { get; set; }

            /// <summary>Number of observations the parser produced for this table.</summary>
            public int ObservationCount { get; set; }

            /// <summary>The full ParsedObservation payload, or null when no observations were produced.</summary>
            public ParsedObservation? Observation { get; set; }

            /// <summary>Pre-Stage-3.5 ValidationFlags snapshot, or null when Claude was skipped / this row was new.</summary>
            public string? BeforeClaudeFlags { get; set; }

            /// <summary>True when Stage 3.5 (Claude enhance) was skipped for this table.</summary>
            public bool ClaudeSkipped { get; set; }
        }

        #endregion
    }
}
