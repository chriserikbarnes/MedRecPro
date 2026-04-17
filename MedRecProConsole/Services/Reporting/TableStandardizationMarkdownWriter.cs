using System.Text;
using MedRecProImportClass.Models;

namespace MedRecProConsole.Services.Reporting
{
    /**************************************************************/
    /// <summary>
    /// Serializes a <see cref="TableReportEntry"/> into a markdown section that mirrors
    /// the console output produced by <c>TableStandardizationService.ExecuteParseSingleAsync</c>.
    /// </summary>
    /// <remarks>
    /// ## Section layout
    /// <code>
    /// ## TextTableID={id} — {caption first 80 chars}
    ///
    /// ### Stage 2: Pivot Table
    ///   **Table Metadata**          (2-col GFM table)
    ///   **Pivoted Table Data**      (N-col GFM table)
    ///   **Footnotes**               (bullet list, omitted when empty)
    ///
    /// ### Stage 3: Standardize
    ///   - **Category:** {category}
    ///   - **Parser:** `{parserName}` | (none, skipped)
    ///   **TextTableID={id} — K observations**
    ///   (7-col observations GFM table)
    ///
    /// ### Stage 3.5: Claude Enhance
    ///   _Skipped (--no-claude)_  | correction diff table
    ///
    /// ---
    /// </code>
    ///
    /// Pure text output — no Spectre.Console markup leaks into the returned string.
    /// </remarks>
    /// <seealso cref="TableReportEntry"/>
    /// <seealso cref="GfmEscape"/>
    /// <seealso cref="MarkdownReportSink"/>
    public static class TableStandardizationMarkdownWriter
    {
        #region public methods

        /**************************************************************/
        /// <summary>
        /// Builds the complete markdown section for a single table standardization entry.
        /// The returned string ends with a trailing newline and a horizontal rule separator.
        /// </summary>
        /// <param name="entry">The per-table snapshot to render.</param>
        /// <returns>Markdown section (GFM), ready to append to a report file.</returns>
        public static string BuildSection(TableReportEntry entry)
        {
            #region implementation

            var sb = new StringBuilder();

            writeHeader(sb, entry);
            writeStage2(sb, entry.Table);
            writeStage3(sb, entry);
            writeStage35(sb, entry);

            sb.AppendLine("---");
            sb.AppendLine();

            return sb.ToString();

            #endregion
        }

        #endregion

        #region private methods — section writers

        /**************************************************************/
        /// <summary>
        /// Writes the H2 header: <c># TextTableID={id} — {caption excerpt}</c>.
        /// Caption is truncated to 80 chars to keep the header scannable.
        /// </summary>
        private static void writeHeader(StringBuilder sb, TableReportEntry entry)
        {
            #region implementation

            var id = entry.Table.TextTableID?.ToString() ?? "(none)";
            var caption = (entry.Table.Caption ?? "(no caption)").Trim();
            if (caption.Length > 80)
                caption = caption.Substring(0, 77) + "…";

            sb.Append("## TextTableID=");
            sb.Append(id);
            sb.Append(" — ");
            sb.AppendLine(GfmEscape.MultilineToBr(caption));
            sb.AppendLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Writes the Stage 2 section: metadata table, pivoted data grid, footnotes list.
        /// </summary>
        private static void writeStage2(StringBuilder sb, ReconstructedTable table)
        {
            #region implementation

            sb.AppendLine("### Stage 2: Pivot Table");
            sb.AppendLine();

            writeMetadataTable(sb, table);
            writePivotedDataTable(sb, table);
            writeFootnotes(sb, table);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Writes the 2-column Property/Value metadata block, mirroring the console Table at
        /// <c>TableStandardizationService.displayReconstructedTable</c>.
        /// </summary>
        private static void writeMetadataTable(StringBuilder sb, ReconstructedTable table)
        {
            #region implementation

            sb.AppendLine("**Table Metadata**");
            sb.AppendLine();
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|---|---|");

            sb.Append("| TextTableID | ");
            sb.Append(GfmEscape.Inline(table.TextTableID?.ToString()));
            sb.AppendLine(" |");

            sb.Append("| Caption | ");
            sb.Append(GfmEscape.Inline(table.Caption ?? "(none)"));
            sb.AppendLine(" |");

            sb.Append("| ParentSectionCode | ");
            sb.Append(GfmEscape.Inline(table.ParentSectionCode));
            sb.AppendLine(" |");

            sb.Append("| SectionTitle | ");
            sb.Append(GfmEscape.Inline(table.SectionTitle));
            sb.AppendLine(" |");

            sb.Append("| Dimensions | ");
            sb.Append($"{table.TotalColumnCount ?? 0} columns x {table.TotalRowCount ?? 0} rows");
            sb.AppendLine(" |");

            // Mirror the Flags list assembled in displayReconstructedTable (console)
            var flags = new List<string>();
            if (table.HasExplicitHeader == true) flags.Add("ExplicitHeader");
            if (table.HasInferredHeader == true) flags.Add("InferredHeader");
            if (table.HasSocDividers == true) flags.Add("SocDividers");
            if (table.HasFooter == true) flags.Add("Footer");

            sb.Append("| Flags | ");
            sb.Append(flags.Count > 0 ? string.Join(", ", flags) : "(none)");
            sb.AppendLine(" |");

            sb.AppendLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Writes the pivoted data grid as a GFM table. Body rows (classified DataBody or
        /// SocDivider) are rendered; header/footer classifications are skipped. SOC divider
        /// rows collapse to a single bold parameter cell with the remaining columns empty,
        /// matching the console behaviour. Cell spans repeat the leftmost value and fill
        /// non-start positions with <c>↔</c>.
        /// </summary>
        private static void writePivotedDataTable(StringBuilder sb, ReconstructedTable table)
        {
            #region implementation

            var columnCount = Math.Max(1, table.TotalColumnCount ?? 1);

            sb.AppendLine("**Pivoted Table Data**");
            sb.AppendLine();

            // Header row
            sb.Append('|');
            for (int c = 0; c < columnCount; c++)
            {
                var headerText = table.Header?.Columns?.ElementAtOrDefault(c)?.LeafHeaderText ?? $"Col {c}";
                sb.Append(' ');
                sb.Append(GfmEscape.Inline(headerText));
                sb.Append(" |");
            }
            sb.AppendLine();

            // Separator row
            sb.Append('|');
            for (int c = 0; c < columnCount; c++)
                sb.Append("---|");
            sb.AppendLine();

            // Body
            var dataRows = table.Rows?.Where(r =>
                r.Classification is RowClassification.DataBody or RowClassification.SocDivider)
                ?? Enumerable.Empty<ReconstructedRow>();

            foreach (var row in dataRows)
            {
                var cellTexts = new string[columnCount];
                for (int c = 0; c < columnCount; c++)
                    cellTexts[c] = "-";

                if (row.Cells != null)
                {
                    foreach (var cell in row.Cells)
                    {
                        var start = cell.ResolvedColumnStart ?? 0;
                        var end = cell.ResolvedColumnEnd ?? (start + 1);
                        var text = GfmEscape.Inline(cell.CleanedText);

                        for (int c = start; c < end && c < columnCount; c++)
                        {
                            cellTexts[c] = c == start ? text : "↔";
                        }
                    }
                }

                if (row.Classification == RowClassification.SocDivider)
                {
                    // Bold the SOC name in the first column, blank out the rest
                    var socName = GfmEscape.Inline(row.SocName ?? cellTexts[0]);
                    cellTexts[0] = $"**{socName}**";
                    for (int c = 1; c < columnCount; c++)
                        cellTexts[c] = "";
                }

                sb.Append('|');
                foreach (var text in cellTexts)
                {
                    sb.Append(' ');
                    sb.Append(text);
                    sb.Append(" |");
                }
                sb.AppendLine();
            }

            sb.AppendLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Writes the footnote bullet list. Omitted entirely when no footnotes exist.
        /// </summary>
        private static void writeFootnotes(StringBuilder sb, ReconstructedTable table)
        {
            #region implementation

            if (table.Footnotes == null || table.Footnotes.Count == 0)
                return;

            sb.AppendLine("**Footnotes**");
            sb.AppendLine();
            foreach (var fn in table.Footnotes)
            {
                sb.Append("- `[");
                sb.Append(GfmEscape.Inline(fn.Key));
                sb.Append("]` ");
                sb.AppendLine(GfmEscape.Inline(fn.Value));
            }
            sb.AppendLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Writes the Stage 3 routing header (Category/Parser) followed by the observations
        /// table. Mirrors <c>displayRoutingResult</c> and <c>displayParseSingleResults</c>.
        /// </summary>
        private static void writeStage3(StringBuilder sb, TableReportEntry entry)
        {
            #region implementation

            sb.AppendLine("### Stage 3: Standardize");
            sb.AppendLine();
            sb.Append("- **Category:** ");
            sb.AppendLine(entry.Category.ToString());
            sb.Append("- **Parser:** ");
            sb.AppendLine(entry.ParserName != null
                ? $"`{GfmEscape.Inline(entry.ParserName)}`"
                : "_(none — skipped)_");
            sb.AppendLine();

            writeObservationsTable(sb, entry);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Writes the per-observation 7-column table. When there are no observations, writes
        /// a short note instead of an empty table.
        /// </summary>
        private static void writeObservationsTable(StringBuilder sb, TableReportEntry entry)
        {
            #region implementation

            var id = entry.Table.TextTableID?.ToString() ?? "(none)";
            var count = entry.Observations.Count;

            sb.Append("**TextTableID=");
            sb.Append(id);
            sb.Append(" — ");
            sb.Append(count);
            sb.AppendLine(count == 1 ? " observation**" : " observations**");
            sb.AppendLine();

            if (count == 0)
            {
                sb.AppendLine("_No observations produced._");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("| Parameter | Arm | Raw Value | Primary | Type | Confidence | Rule |");
            sb.AppendLine("|---|---|---|---|---|---|---|");

            foreach (var obs in entry.Observations)
            {
                sb.Append("| ");
                sb.Append(GfmEscape.Inline(obs.ParameterName));
                sb.Append(" | ");
                sb.Append(GfmEscape.Inline(obs.TreatmentArm));
                sb.Append(" | ");
                sb.Append(GfmEscape.Inline(obs.RawValue));
                sb.Append(" | ");
                sb.Append(GfmEscape.Inline(obs.PrimaryValue?.ToString("G")));
                sb.Append(" | ");
                sb.Append(GfmEscape.Inline(obs.PrimaryValueType));
                sb.Append(" | ");
                sb.Append(obs.ParseConfidence?.ToString("F2") ?? "-");
                sb.Append(" | ");
                sb.Append(GfmEscape.Inline(obs.ParseRule));
                sb.AppendLine(" |");
            }

            sb.AppendLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Writes the Stage 3.5 section. Renders either a "skipped" note, a "no corrections"
        /// note, or a corrections diff table. Diff logic matches the console
        /// <c>displayClaudeCorrections</c> implementation exactly: extracts
        /// <c>AI_CORRECTED:*</c> entries added after the before-snapshot.
        /// </summary>
        private static void writeStage35(StringBuilder sb, TableReportEntry entry)
        {
            #region implementation

            sb.AppendLine("### Stage 3.5: Claude Enhance");
            sb.AppendLine();

            if (entry.ClaudeSkipped)
            {
                sb.AppendLine("_Skipped (--no-claude)_");
                sb.AppendLine();
                return;
            }

            var corrections = diffCorrections(entry);

            if (corrections.Count == 0)
            {
                sb.AppendLine("_No corrections applied by Claude._");
                sb.AppendLine();
                return;
            }

            sb.Append("**");
            sb.Append(corrections.Count);
            sb.AppendLine(corrections.Count == 1
                ? " Correction Applied**"
                : " Corrections Applied**");
            sb.AppendLine();
            sb.AppendLine("| Row | Cell | Correction |");
            sb.AppendLine("|---|---|---|");

            foreach (var (row, cell, flag) in corrections)
            {
                sb.Append("| ");
                sb.Append(row);
                sb.Append(" | ");
                sb.Append(cell);
                sb.Append(" | ");
                sb.Append(GfmEscape.Inline(flag));
                sb.AppendLine(" |");
            }
            sb.AppendLine();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts new <c>AI_CORRECTED:*</c> entries appended between the before-snapshot
        /// and the current state of <see cref="ParsedObservation.ValidationFlags"/>.
        /// </summary>
        /// <returns>List of (SourceRowSeq, SourceCellSeq, new-flag) tuples.</returns>
        private static List<(int row, int cell, string flag)> diffCorrections(TableReportEntry entry)
        {
            #region implementation

            var corrections = new List<(int row, int cell, string flag)>();
            var before = entry.BeforeClaudeFlags;

            foreach (var obs in entry.Observations)
            {
                var key = (obs.SourceRowSeq ?? 0) * 10000 + (obs.SourceCellSeq ?? 0);
                var beforeFlags = before?.GetValueOrDefault(key);
                var after = obs.ValidationFlags;

                if (after == null || after == beforeFlags)
                    continue;

                var newFlags = after
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .Where(f => f.StartsWith("AI_CORRECTED:"))
                    .Where(f => beforeFlags == null || !beforeFlags.Contains(f));

                foreach (var flag in newFlags)
                {
                    corrections.Add((obs.SourceRowSeq ?? 0, obs.SourceCellSeq ?? 0, flag));
                }
            }

            return corrections;

            #endregion
        }

        #endregion
    }
}
