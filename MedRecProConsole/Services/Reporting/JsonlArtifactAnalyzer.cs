using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MedRecProConsole.Services.Reporting
{
    #region result records

    /**************************************************************/
    /// <summary>
    /// Per-category roll-up of forwarded vs skipped scored rows.
    /// </summary>
    /// <param name="Category">TableCategory string from the observation payload (canonical form, e.g. <c>ADVERSE_EVENT</c>).</param>
    /// <param name="TotalRows">Total scored observation rows in the category.</param>
    /// <param name="Forwarded">Rows whose <c>QC_PARSE_QUALITY</c> is strictly below the gate threshold.</param>
    /// <param name="Skipped">Rows at or above the gate threshold.</param>
    /// <param name="ForwardRate">Forwarded / TotalRows.</param>
    public sealed record ArtifactCategoryRow(
        string Category,
        int TotalRows,
        int Forwarded,
        int Skipped,
        double ForwardRate);

    /**************************************************************/
    /// <summary>
    /// Per-(category, parser) roll-up of forwarded scored rows.
    /// </summary>
    public sealed record ArtifactParserRow(
        string Category,
        string Parser,
        int TotalRows,
        int Forwarded,
        double ForwardRate);

    /**************************************************************/
    /// <summary>
    /// Top combinations of <c>QC_PARSE_QUALITY:REVIEW_REASONS</c> on forwarded rows.
    /// </summary>
    /// <param name="ReasonCombination">Sorted, pipe-joined reason set (e.g. <c>MissingRequired:TreatmentArm | PVT_MIGRATED</c>).</param>
    /// <param name="DistinctTables">Number of distinct TextTableIDs contributing to this combination.</param>
    /// <param name="ExampleTextTableId">An example TextTableID for spot-checking.</param>
    public sealed record ArtifactReasonRow(
        string Category,
        string ReasonCombination,
        int Rows,
        int DistinctTables,
        int? ExampleTextTableId);

    /**************************************************************/
    /// <summary>
    /// Raw-value shape buckets among forwarded rows. Captures the "what does the cell look like"
    /// signal used to decide which value-parser improvements will pay off.
    /// </summary>
    /// <param name="Shape">Shape name — one of <see cref="JsonlArtifactAnalyzer.ShapeLabels"/>.</param>
    /// <param name="ExampleRawValue">First raw value seen in this bucket; for spot-checking.</param>
    public sealed record ArtifactShapeRow(
        string Category,
        string Shape,
        int Rows,
        int DistinctTables,
        string? ExampleRawValue);

    /**************************************************************/
    /// <summary>
    /// Per-table forward roll-up. A single malformed table can dominate cost, so the
    /// analyzer surfaces the worst tables explicitly.
    /// </summary>
    public sealed record ArtifactTableRow(
        int TextTableId,
        string Category,
        string? Parser,
        int Total,
        int Forwarded,
        double ForwardRate);

    /**************************************************************/
    /// <summary>
    /// Full result of an artifact analysis pass over a JSONL standardization report.
    /// </summary>
    /// <param name="SourcePath">Path of the JSONL file the analysis was run against, when known.</param>
    /// <param name="Threshold">Quality threshold applied (matches <c>ClaudeApiCorrectionSettings.ClaudeReviewQualityThreshold</c>).</param>
    /// <param name="TotalScored">Number of observation lines that carried a parsable <c>QC_PARSE_QUALITY</c> token.</param>
    /// <param name="LinesWithoutScore">Lines that had no parsable score — meta lines for skipped tables, or rows where the QC stage was bypassed.</param>
    public sealed record ArtifactAnalysisResult(
        string? SourcePath,
        double Threshold,
        int TotalScored,
        int LinesWithoutScore,
        IReadOnlyList<ArtifactCategoryRow> ByCategory,
        IReadOnlyList<ArtifactParserRow> ByParser,
        IReadOnlyList<ArtifactReasonRow> TopReasons,
        IReadOnlyList<ArtifactShapeRow> TopShapes,
        IReadOnlyList<ArtifactTableRow> TopWorstTables);

    #endregion result records

    /**************************************************************/
    /// <summary>
    /// Reads a Stage-3 standardization JSONL artifact (one observation per line, as written by
    /// <see cref="JsonReportSink"/>) and produces an <see cref="ArtifactAnalysisResult"/> with
    /// forward-rate roll-ups by category, parser, reason combination, raw-value shape, and
    /// TextTableID. Designed to make Phase 0 + Phase 1 of the deterministic-parse-QC remediation
    /// plan repeatable: every standardization run can be re-analyzed without rebuilding ad-hoc
    /// SQL or jq pipelines.
    /// </summary>
    /// <remarks>
    /// ## Gate semantics
    /// Rows whose <c>QC_PARSE_QUALITY:{score}</c> token parses to a value strictly less than
    /// <see cref="DefaultThreshold"/> are counted as "forwarded". This matches the gate evaluated
    /// by <c>ClaudeApiCorrectionService</c> and the corrected SQL audit query in
    /// <c>MedRecPro/SQL/Transient/TableStandardizationChecks.sql</c>.
    ///
    /// ## Score parsing
    /// <code>QC_PARSE_QUALITY:0.9000;...</code>
    /// is extracted via the regex <see cref="_scorePattern"/> with the score token starting
    /// after <c>LEN("QC_PARSE_QUALITY:")</c> and running to the next <c>;</c> or end-of-string.
    ///
    /// ## Reasons
    /// When a row carries <c>QC_PARSE_QUALITY:REVIEW_REASONS:|reason1|reason2|</c>, reasons are
    /// split on <c>|</c>, trimmed, and re-joined alphabetically so combinations dedupe regardless
    /// of emission order.
    ///
    /// ## Shape buckets
    /// Raw values are bucketed into the labels enumerated in <see cref="ShapeLabels"/>. The
    /// buckets mirror the high-volume failure shapes seen in the 2026-04-28 baseline.
    /// </remarks>
    /// <seealso cref="JsonReportSink"/>
    /// <seealso cref="TableStandardizationJsonWriter"/>
    public static class JsonlArtifactAnalyzer
    {
        #region constants

        /**************************************************************/
        /// <summary>
        /// Default quality threshold. Keep in sync with
        /// <c>ClaudeApiCorrectionSettings.ClaudeReviewQualityThreshold</c>.
        /// </summary>
        public const double DefaultThreshold = 0.75;

        /**************************************************************/
        /// <summary>
        /// Canonical shape labels emitted by <see cref="classifyShape"/>. Documented so test
        /// assertions and downstream consumers can refer to the labels by name.
        /// </summary>
        public static class ShapeLabels
        {
            public const string LessThanEncoded = "less-than encoded";
            public const string DashOrExclusion = "dash/exclusion";
            public const string DigitsOnly = "digits only";
            public const string TextWithDigits = "text with digits";
            public const string TextNoDigit = "text no digit";
            public const string Blank = "blank";
            public const string Other = "other";
        }

        #endregion constants

        #region private static

        /**************************************************************/
        /// <summary>
        /// Captures the score token immediately following the <c>QC_PARSE_QUALITY:</c> prefix
        /// and stops at the next <c>;</c> or end-of-string. Lazy-quantified to avoid
        /// inadvertently consuming the trailing reason payload.
        /// </summary>
        private static readonly Regex _scorePattern = new(
            @"QC_PARSE_QUALITY:(?<score>[0-9]+(?:\.[0-9]+)?)",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Captures the pipe-delimited reason payload after <c>QC_PARSE_QUALITY:REVIEW_REASONS:</c>.
        /// </summary>
        private static readonly Regex _reasonsPattern = new(
            @"QC_PARSE_QUALITY:REVIEW_REASONS:(?<reasons>[^;]*)",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// JSON serializer options aligned with the writer. Property names are camelCase to
        /// match the artifact layout produced by <see cref="TableStandardizationJsonWriter"/>.
        /// </summary>
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        #endregion private static

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Reads a JSONL file from disk and produces an <see cref="ArtifactAnalysisResult"/>.
        /// </summary>
        /// <param name="path">Absolute or relative path to the JSONL artifact.</param>
        /// <param name="threshold">Quality threshold; defaults to <see cref="DefaultThreshold"/>.</param>
        /// <param name="topN">Maximum row count for the reason / shape / worst-table tables.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Aggregated analysis result.</returns>
        public static async Task<ArtifactAnalysisResult> AnalyzeFileAsync(
            string path,
            double threshold = DefaultThreshold,
            int topN = 25,
            CancellationToken ct = default)
        {
            #region implementation

            var lines = new List<string>();
            using (var reader = new StreamReader(path))
            {
                string? line;
                while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        lines.Add(line);
                }
            }

            return Analyze(lines, threshold, topN, sourcePath: path);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Analyzes an in-memory sequence of JSONL lines. Suitable for tests that want to
        /// pass synthetic input without touching disk.
        /// </summary>
        /// <param name="jsonlLines">One JSON object per element. Empty / whitespace lines are skipped.</param>
        /// <param name="threshold">Quality threshold; defaults to <see cref="DefaultThreshold"/>.</param>
        /// <param name="topN">Maximum row count for the reason / shape / worst-table tables.</param>
        /// <param name="sourcePath">Optional source-path tag carried into the result.</param>
        /// <returns>Aggregated analysis result.</returns>
        public static ArtifactAnalysisResult Analyze(
            IEnumerable<string> jsonlLines,
            double threshold = DefaultThreshold,
            int topN = 25,
            string? sourcePath = null)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(jsonlLines);

            var byCategory = new Dictionary<string, (int total, int forwarded)>(StringComparer.OrdinalIgnoreCase);
            var byParser = new Dictionary<(string Cat, string Parser), (int total, int forwarded)>();
            var reasonAgg = new Dictionary<(string Cat, string Combo), (int rows, HashSet<int> tables, int? example)>();
            var shapeAgg = new Dictionary<(string Cat, string Shape), (int rows, HashSet<int> tables, string? example)>();
            var tableAgg = new Dictionary<int, (string Cat, string? Parser, int total, int forwarded)>();

            int totalScored = 0;
            int withoutScore = 0;

            foreach (var line in jsonlLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                ParsedLine parsed;
                try
                {
                    parsed = parseLine(line);
                }
                catch (JsonException)
                {
                    // Tolerate corrupt lines rather than aborting — the analyzer is a
                    // diagnostic tool, not a strict schema validator.
                    withoutScore++;
                    continue;
                }

                if (!parsed.Score.HasValue)
                {
                    withoutScore++;
                    continue;
                }

                totalScored++;

                var cat = parsed.Category ?? "(null)";
                var parser = parsed.Parser ?? "(none)";
                bool forwarded = parsed.Score.Value < threshold;

                // Category
                var (catTotal, catForwarded) = byCategory.TryGetValue(cat, out var c) ? c : (0, 0);
                byCategory[cat] = (catTotal + 1, catForwarded + (forwarded ? 1 : 0));

                // Parser within category
                var pkey = (cat, parser);
                var (pTotal, pForwarded) = byParser.TryGetValue(pkey, out var p) ? p : (0, 0);
                byParser[pkey] = (pTotal + 1, pForwarded + (forwarded ? 1 : 0));

                // Per-table
                var (tCat, tParser, tTotal, tForwarded) = tableAgg.TryGetValue(parsed.TextTableId, out var t)
                    ? t
                    : (cat, parser, 0, 0);
                tableAgg[parsed.TextTableId] = (tCat, tParser, tTotal + 1, tForwarded + (forwarded ? 1 : 0));

                if (!forwarded)
                    continue;

                // Reasons (forwarded rows only — matches the comment in TableStandardizationChecks.sql)
                if (!string.IsNullOrEmpty(parsed.ReasonCombination))
                {
                    var rkey = (cat, parsed.ReasonCombination);
                    if (reasonAgg.TryGetValue(rkey, out var rExisting))
                    {
                        rExisting.tables.Add(parsed.TextTableId);
                        reasonAgg[rkey] = (rExisting.rows + 1, rExisting.tables, rExisting.example ?? parsed.TextTableId);
                    }
                    else
                    {
                        var set = new HashSet<int> { parsed.TextTableId };
                        reasonAgg[rkey] = (1, set, parsed.TextTableId);
                    }
                }

                // Shape (forwarded rows only)
                var shape = classifyShape(parsed.RawValue);
                var skey = (cat, shape);
                if (shapeAgg.TryGetValue(skey, out var sExisting))
                {
                    sExisting.tables.Add(parsed.TextTableId);
                    shapeAgg[skey] = (sExisting.rows + 1, sExisting.tables, sExisting.example);
                }
                else
                {
                    var set = new HashSet<int> { parsed.TextTableId };
                    shapeAgg[skey] = (1, set, parsed.RawValue);
                }
            }

            // Project + sort
            var byCategoryRows = byCategory
                .Select(kv => new ArtifactCategoryRow(
                    kv.Key,
                    kv.Value.total,
                    kv.Value.forwarded,
                    kv.Value.total - kv.Value.forwarded,
                    kv.Value.total == 0 ? 0.0 : (double)kv.Value.forwarded / kv.Value.total))
                .OrderByDescending(r => r.ForwardRate)
                .ToList();

            var byParserRows = byParser
                .Select(kv => new ArtifactParserRow(
                    kv.Key.Cat,
                    kv.Key.Parser,
                    kv.Value.total,
                    kv.Value.forwarded,
                    kv.Value.total == 0 ? 0.0 : (double)kv.Value.forwarded / kv.Value.total))
                .OrderByDescending(r => r.Forwarded)
                .ToList();

            var topReasonRows = reasonAgg
                .Select(kv => new ArtifactReasonRow(
                    kv.Key.Cat,
                    kv.Key.Combo,
                    kv.Value.rows,
                    kv.Value.tables.Count,
                    kv.Value.example))
                .OrderByDescending(r => r.Rows)
                .Take(topN)
                .ToList();

            var topShapeRows = shapeAgg
                .Select(kv => new ArtifactShapeRow(
                    kv.Key.Cat,
                    kv.Key.Shape,
                    kv.Value.rows,
                    kv.Value.tables.Count,
                    kv.Value.example))
                .OrderByDescending(r => r.Rows)
                .Take(topN)
                .ToList();

            var topWorstTables = tableAgg
                .Where(kv => kv.Value.forwarded > 0)
                .Select(kv => new ArtifactTableRow(
                    kv.Key,
                    kv.Value.Cat,
                    kv.Value.Parser,
                    kv.Value.total,
                    kv.Value.forwarded,
                    kv.Value.total == 0 ? 0.0 : (double)kv.Value.forwarded / kv.Value.total))
                .OrderByDescending(r => r.Forwarded)
                .ThenByDescending(r => r.ForwardRate)
                .Take(topN)
                .ToList();

            return new ArtifactAnalysisResult(
                sourcePath,
                threshold,
                totalScored,
                withoutScore,
                byCategoryRows,
                byParserRows,
                topReasonRows,
                topShapeRows,
                topWorstTables);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Renders an <see cref="ArtifactAnalysisResult"/> as GitHub-flavored markdown — the
        /// same shape as the tables in the remediation plan, so analyzer output is directly
        /// comparable to the planning baseline.
        /// </summary>
        public static string ToMarkdown(ArtifactAnalysisResult result)
        {
            #region implementation

            ArgumentNullException.ThrowIfNull(result);

            var sb = new StringBuilder();
            sb.AppendLine("# JSONL artifact analysis");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(result.SourcePath))
            {
                sb.Append("Source: `").Append(result.SourcePath).AppendLine("`");
            }
            sb.AppendFormat("Threshold: `< {0:0.00}` (rows below threshold are forwarded to Claude).", result.Threshold).AppendLine();
            sb.AppendFormat("Total scored rows: **{0:N0}**.", result.TotalScored).AppendLine();
            if (result.LinesWithoutScore > 0)
            {
                sb.AppendFormat("Lines without a parsable score: {0:N0}.", result.LinesWithoutScore).AppendLine();
            }
            sb.AppendLine();

            sb.AppendLine("## Forward rate by category");
            sb.AppendLine();
            sb.AppendLine("| Category | Total Rows | Forwarded | Skipped | Forward Rate |");
            sb.AppendLine("|---|---:|---:|---:|---:|");
            foreach (var row in result.ByCategory)
            {
                sb.AppendFormat(
                    "| {0} | {1:N0} | {2:N0} | {3:N0} | {4:P1} |",
                    row.Category, row.TotalRows, row.Forwarded, row.Skipped, row.ForwardRate)
                  .AppendLine();
            }
            sb.AppendLine();

            sb.AppendLine("## Forward rate by parser");
            sb.AppendLine();
            sb.AppendLine("| Category | Parser | Total | Forwarded | Forward Rate |");
            sb.AppendLine("|---|---|---:|---:|---:|");
            foreach (var row in result.ByParser)
            {
                sb.AppendFormat(
                    "| {0} | {1} | {2:N0} | {3:N0} | {4:P1} |",
                    row.Category, row.Parser, row.TotalRows, row.Forwarded, row.ForwardRate)
                  .AppendLine();
            }
            sb.AppendLine();

            sb.AppendLine("## Top reason combinations on forwarded rows");
            sb.AppendLine();
            sb.AppendLine("| Category | Reason combination | Rows | Tables | Example TextTableID |");
            sb.AppendLine("|---|---|---:|---:|---:|");
            foreach (var row in result.TopReasons)
            {
                sb.AppendFormat(
                    "| {0} | {1} | {2:N0} | {3:N0} | {4} |",
                    row.Category, row.ReasonCombination, row.Rows, row.DistinctTables,
                    row.ExampleTextTableId?.ToString() ?? "—")
                  .AppendLine();
            }
            sb.AppendLine();

            sb.AppendLine("## Raw-value shapes on forwarded rows");
            sb.AppendLine();
            sb.AppendLine("| Category | Shape | Rows | Tables | Example |");
            sb.AppendLine("|---|---|---:|---:|---|");
            foreach (var row in result.TopShapes)
            {
                var ex = string.IsNullOrEmpty(row.ExampleRawValue) ? "—" : "`" + row.ExampleRawValue.Replace("|", "\\|") + "`";
                sb.AppendFormat(
                    "| {0} | {1} | {2:N0} | {3:N0} | {4} |",
                    row.Category, row.Shape, row.Rows, row.DistinctTables, ex)
                  .AppendLine();
            }
            sb.AppendLine();

            sb.AppendLine("## Worst tables by forwarded count");
            sb.AppendLine();
            sb.AppendLine("| TextTableID | Category | Parser | Total | Forwarded | Forward Rate |");
            sb.AppendLine("|---:|---|---|---:|---:|---:|");
            foreach (var row in result.TopWorstTables)
            {
                sb.AppendFormat(
                    "| {0} | {1} | {2} | {3:N0} | {4:N0} | {5:P1} |",
                    row.TextTableId, row.Category, row.Parser ?? "—",
                    row.Total, row.Forwarded, row.ForwardRate)
                  .AppendLine();
            }

            return sb.ToString();

            #endregion
        }

        #endregion public methods

        #region private helpers

        /**************************************************************/
        /// <summary>
        /// Lightweight projection of one JSONL line — only fields needed for aggregation.
        /// </summary>
        private readonly record struct ParsedLine(
            int TextTableId,
            string? Category,
            string? Parser,
            string? RawValue,
            double? Score,
            string? ReasonCombination);

        /**************************************************************/
        /// <summary>
        /// Parses one JSON line and projects out the fields the analyzer needs. Tolerates the
        /// camelCase-with-mixed-casing-on-enums layout produced by
        /// <see cref="TableStandardizationJsonWriter"/> — the inner <c>observation.tableCategory</c>
        /// holds the canonical <c>ADVERSE_EVENT</c> form, while the top-level <c>category</c>
        /// uses the camelCase enum name. We prefer the canonical form when present.
        /// </summary>
        private static ParsedLine parseLine(string line)
        {
            #region implementation

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            int tid = root.TryGetProperty("textTableId", out var tidEl) && tidEl.ValueKind == JsonValueKind.Number
                ? tidEl.GetInt32()
                : 0;

            string? topCategory = root.TryGetProperty("category", out var catEl) && catEl.ValueKind == JsonValueKind.String
                ? catEl.GetString()
                : null;

            string? parser = root.TryGetProperty("parser", out var pEl) && pEl.ValueKind == JsonValueKind.String
                ? pEl.GetString()
                : null;

            // Prefer the canonical TableCategory form from the inner observation when available.
            string? canonicalCategory = null;
            string? rawValue = null;
            string? validationFlags = null;
            if (root.TryGetProperty("observation", out var obsEl) && obsEl.ValueKind == JsonValueKind.Object)
            {
                if (obsEl.TryGetProperty("tableCategory", out var tcEl) && tcEl.ValueKind == JsonValueKind.String)
                    canonicalCategory = tcEl.GetString();
                if (obsEl.TryGetProperty("rawValue", out var rvEl) && rvEl.ValueKind == JsonValueKind.String)
                    rawValue = rvEl.GetString();
                if (obsEl.TryGetProperty("validationFlags", out var vfEl) && vfEl.ValueKind == JsonValueKind.String)
                    validationFlags = vfEl.GetString();
            }

            double? score = null;
            string? combo = null;
            if (!string.IsNullOrEmpty(validationFlags))
            {
                var sm = _scorePattern.Match(validationFlags);
                if (sm.Success && double.TryParse(sm.Groups["score"].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s))
                    score = s;

                var rm = _reasonsPattern.Match(validationFlags);
                if (rm.Success)
                {
                    var raw = rm.Groups["reasons"].Value;
                    var reasons = raw
                        .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(r => r.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(r => r, StringComparer.Ordinal)
                        .ToArray();
                    if (reasons.Length > 0)
                        combo = string.Join(" | ", reasons);
                }
            }

            return new ParsedLine(
                tid,
                canonicalCategory ?? topCategory,
                parser,
                rawValue,
                score,
                combo);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Buckets a raw cell value into one of the labels in <see cref="ShapeLabels"/>. The
        /// classifier prefers the most specific bucket — encoded HTML inequality before
        /// dash/exclusion before generic text-with-digits.
        /// </summary>
        public static string classifyShape(string? rawValue)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(rawValue))
                return ShapeLabels.Blank;

            var trimmed = rawValue.Trim();

            // Encoded inequality leak (`&lt;`, `&gt;` — these survive Stage 2 today)
            if (trimmed.Contains("&lt;", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("&gt;", StringComparison.OrdinalIgnoreCase))
            {
                return ShapeLabels.LessThanEncoded;
            }

            // Dash / exclusion-style tokens
            if (trimmed is "--" or "—" or "–" or "-" or "N/A" or "NA" or "ND" or "NR" or "NC" or "NDd")
                return ShapeLabels.DashOrExclusion;

            bool hasLetter = trimmed.Any(char.IsLetter);
            bool hasDigit = trimmed.Any(char.IsDigit);

            if (!hasLetter && hasDigit)
                return ShapeLabels.DigitsOnly;
            if (hasLetter && hasDigit)
                return ShapeLabels.TextWithDigits;
            if (hasLetter)
                return ShapeLabels.TextNoDigit;

            return ShapeLabels.Other;

            #endregion
        }

        #endregion private helpers
    }
}
