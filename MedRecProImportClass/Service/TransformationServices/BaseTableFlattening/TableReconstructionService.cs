using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Stage 2 implementation of the SPL Table Normalization pipeline.
    /// Reconstructs logical table structures from the flat TableCellContext projection,
    /// including row classification, ColSpan/RowSpan resolution, multi-level header resolution,
    /// and footnote extraction.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Stage 1 (ITableCellContextService) → **Stage 2 (this)** → Stage 3 (section-aware parsing)
    ///
    /// ## Processing Steps
    /// 1. Fetch cells via ITableCellContextService (DRY data access)
    /// 2. Group by TextTableRowID, sort by RowGroupType priority then SequenceNumber
    /// 3. Process each cell: extract footnotes from &lt;sup&gt; tags, extract styleCode, strip HTML
    /// 4. Classify rows: Header, InferredHeader, ContinuationHeader, SocDivider, DataBody, Footer
    /// 5. Resolve column positions via 2D occupancy grid (handles RowSpan/ColSpan)
    /// 6. Build multi-level header structure
    /// 7. Extract footnotes from Footer rows
    /// </remarks>
    /// <seealso cref="ITableReconstructionService"/>
    /// <seealso cref="TableCellContext"/>
    /// <seealso cref="ITableCellContextService"/>
    /// <seealso cref="ReconstructedTable"/>
    public class TableReconstructionService : ITableReconstructionService
    {
        #region Private Fields

        private readonly ITableCellContextService _tableCellContextService;
        private readonly ILogger<TableReconstructionService> _logger;

        /// <summary>
        /// Compiled regex for extracting footnote markers from &lt;sup&gt; tags.
        /// </summary>
        private static readonly Regex SupTagRegex = new Regex(
            @"<sup[^>]*>\s*(?<marker>[^<]+?)\s*</sup>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Compiled regex for extracting styleCode attribute values from HTML.
        /// </summary>
        private static readonly Regex StyleCodeRegex = new Regex(
            @"styleCode\s*=\s*""(?<code>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Compiled regex for parsing footnote entries in footer text (marker followed by text).
        /// </summary>
        private static readonly Regex FootnoteEntryRegex = new Regex(
            @"^\s*(?<marker>[a-zA-Z*†‡§¶#\d]+)\s+(?<text>.+)$",
            RegexOptions.Compiled);

        /// <summary>
        /// Empty tag list used when calling RemoveUnwantedTags with cleanAll = true.
        /// </summary>
        private static readonly List<string> EmptyTagList = new List<string>();

        #endregion

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes a new instance of the TableReconstructionService class.
        /// </summary>
        /// <param name="tableCellContextService">Stage 1 data access service for cell context data.</param>
        /// <param name="logger">Logger for tracking operations and errors.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <seealso cref="ITableCellContextService"/>
        public TableReconstructionService(
            ITableCellContextService tableCellContextService,
            ILogger<TableReconstructionService> logger)
        {
            #region implementation
            _tableCellContextService = tableCellContextService ?? throw new ArgumentNullException(nameof(tableCellContextService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Protected parameterless constructor for mocking purposes.
        /// </summary>
        protected TableReconstructionService()
        {
            // Initialize with null! to satisfy compiler - only used for mocking
            _tableCellContextService = null!;
            _logger = null!;
        }

        #endregion

        #region Public Methods

        /**************************************************************/
        /// <summary>
        /// Reconstructs a single table by its TextTableID.
        /// </summary>
        /// <param name="textTableId">The TextTableID to reconstruct.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A ReconstructedTable with classified rows and resolved headers,
        /// or null if no cells exist for the given TextTableID.
        /// </returns>
        /// <seealso cref="ReconstructedTable"/>
        /// <seealso cref="reconstructFromCells"/>
        public async Task<ReconstructedTable?> ReconstructTableAsync(
            int textTableId,
            CancellationToken cancellationToken = default)
        {
            #region implementation
            _logger.LogDebug("ReconstructTableAsync called for TextTableID={TextTableId}", textTableId);

            var filter = new TableCellContextFilter { TextTableID = textTableId };
            filter.Validate();

            var cells = await _tableCellContextService.GetTableCellContextsAsync(filter, cancellationToken);

            if (cells == null || cells.Count == 0)
            {
                _logger.LogDebug("ReconstructTableAsync: no cells found for TextTableID={TextTableId}", textTableId);
                return null;
            }

            var result = reconstructFromCells(cells);
            _logger.LogDebug("ReconstructTableAsync: reconstructed table {TextTableId} with {RowCount} rows, {ColCount} columns",
                textTableId, result.TotalRowCount, result.TotalColumnCount);
            return result;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Reconstructs all tables matching the optional filter.
        /// </summary>
        /// <param name="filter">Optional filter for document, table, range, or row cap.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of ReconstructedTable DTOs.</returns>
        /// <seealso cref="ReconstructedTable"/>
        /// <seealso cref="reconstructFromCells"/>
        public async Task<List<ReconstructedTable>> ReconstructTablesAsync(
            TableCellContextFilter? filter = null,
            CancellationToken cancellationToken = default)
        {
            #region implementation
            _logger.LogDebug("ReconstructTablesAsync called");

            var grouped = await _tableCellContextService.GetTableCellContextsGroupedByTableAsync(filter, cancellationToken);
            var results = new List<ReconstructedTable>();

            foreach (var kvp in grouped)
            {
                var table = reconstructFromCells(kvp.Value);
                results.Add(table);
            }

            _logger.LogDebug("ReconstructTablesAsync: reconstructed {Count} tables", results.Count);
            return results;
            #endregion
        }

        #endregion

        #region Internal Methods

        /**************************************************************/
        /// <summary>
        /// Orchestrates reconstruction of a single table from its flat cell list.
        /// </summary>
        /// <param name="cells">All TableCellContext rows for one TextTableID.</param>
        /// <seealso cref="TableCellContext"/>
        /// <returns>A fully reconstructed table with classified rows and resolved headers.</returns>
        internal ReconstructedTable reconstructFromCells(List<TableCellContext> cells)
        {
            #region implementation
            // Extract context from first cell
            var first = cells[0];

            // Group cells by TextTableRowID, sort by RowGroupType priority then SequenceNumber
            var rowGroups = cells
                .Where(c => c.TextTableRowID.HasValue)
                .GroupBy(c => c.TextTableRowID!.Value)
                .Select(g => new
                {
                    RowId = g.Key,
                    RowGroupType = g.First().RowGroupType,
                    SequenceNumberTextTableRow = g.First().SequenceNumberTextTableRow,
                    Cells = g.OrderBy(c => c.SequenceNumber).ToList()
                })
                .OrderBy(r => rowGroupTypePriority(r.RowGroupType))
                .ThenBy(r => r.SequenceNumberTextTableRow)
                .ToList();

            // Process each row
            var rows = rowGroups
                .Select(rg => processRow(rg.Cells))
                .ToList();

            // Determine total column count (max sum of ColSpan across rows)
            int totalColumnCount = 0;
            foreach (var row in rows)
            {
                int rowWidth = 0;
                foreach (var cell in row.Cells ?? Enumerable.Empty<ProcessedCell>())
                    rowWidth += cell.ColSpan ?? 1;
                totalColumnCount = Math.Max(totalColumnCount, rowWidth);
            }

            // Classify rows
            classifyRows(rows, totalColumnCount);

            // Resolve column positions via occupancy grid
            resolveColumnPositions(rows, totalColumnCount);

            // Build header structure
            var header = resolveHeaders(rows, totalColumnCount);

            // Extract footnotes from footer rows
            var footnotes = extractFootnotes(rows);

            // Assemble result
            return new ReconstructedTable
            {
                TextTableID = first.TextTableID,
                Caption = first.Caption,
                DocumentGUID = first.DocumentGUID,
                Title = first.Title,
                VersionNumber = first.VersionNumber,
                UNII = first.UNII,
                SectionGUID = first.SectionGUID,
                SectionCode = first.SectionCode,
                SectionType = first.SectionType,
                SectionTitle = first.SectionTitle,
                ParentSectionCode = first.ParentSectionCode,
                ParentSectionTitle = first.ParentSectionTitle,
                LabelerName = first.LabelerName,
                TotalColumnCount = totalColumnCount,
                TotalRowCount = rows.Count,
                HasExplicitHeader = rows.Any(r => r.Classification == RowClassification.ExplicitHeader),
                HasInferredHeader = rows.Any(r => r.Classification == RowClassification.InferredHeader),
                HasFooter = rows.Any(r => r.Classification == RowClassification.Footer),
                HasSocDividers = rows.Any(r => r.Classification == RowClassification.SocDivider),
                Header = header,
                Rows = rows,
                Footnotes = footnotes
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts footnote markers from &lt;sup&gt; tags in raw HTML cell text.
        /// </summary>
        /// <param name="html">Raw CellText containing HTML markup.</param>
        /// <returns>List of distinct footnote marker strings; empty list if none found.</returns>
        /// <example>
        /// <code>
        /// var markers = extractFootnoteMarkers("Nausea&lt;sup&gt;a,b&lt;/sup&gt;");
        /// // Returns: ["a", "b"]
        /// </code>
        /// </example>
        internal static List<string> extractFootnoteMarkers(string? html)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(html))
                return new List<string>();

            var markers = new List<string>();
            var matches = SupTagRegex.Matches(html);

            foreach (Match match in matches)
            {
                var markerText = match.Groups["marker"].Value;
                // Split comma-separated markers (e.g., "a,b" → ["a", "b"])
                var parts = markerText.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        markers.Add(trimmed);
                }
            }

            return markers.Distinct().ToList();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the first styleCode attribute value from raw HTML cell text.
        /// </summary>
        /// <param name="html">Raw CellText containing HTML markup.</param>
        /// <returns>The styleCode value (e.g., "bold", "Botrule"), or null if not found.</returns>
        internal static string? extractStyleCode(string? html)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(html))
                return null;

            var match = StyleCodeRegex.Match(html);
            return match.Success ? match.Groups["code"].Value : null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Strips all HTML tags from cell text and normalizes whitespace.
        /// Reuses TextUtil.RemoveUnwantedTags with cleanAll = true.
        /// </summary>
        /// <param name="html">Raw HTML cell text (with &lt;sup&gt; tags already processed).</param>
        /// <seealso cref="TextUtil.RemoveUnwantedTags(string, List{string}, bool)"/>
        /// <returns>Plain text with collapsed whitespace, or null if input is null/empty.</returns>
        internal static string? stripHtml(string? html)
        {
            #region implementation
            if (string.IsNullOrWhiteSpace(html))
                return html;

            // Replace <br /> and <br> with space before stripping
            var preprocessed = Regex.Replace(html, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);

            // Use existing TextUtil to strip all HTML tags
            var text = preprocessed.RemoveUnwantedTags(EmptyTagList, cleanAll: true);

            if (string.IsNullOrEmpty(text))
                return null;

            // Normalize whitespace: collapse multiple spaces/newlines, trim
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return string.IsNullOrEmpty(text) ? null : text;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies rows within a table based on RowGroupType and structural heuristics.
        /// Mutates ReconstructedRow.Classification and ReconstructedRow.AbsoluteRowIndex.
        /// </summary>
        /// <seealso cref="ReconstructedRow"/>
        /// <seealso cref="RowClassification"/>
        /// <param name="rows">All rows in the table, sorted by group priority then sequence.</param>
        /// <param name="totalColumnCount">Total column count for SOC divider detection.</param>
        internal static void classifyRows(List<ReconstructedRow> rows, int totalColumnCount)
        {
            #region implementation
            bool hasExplicitHeader = false;

            // First pass: classify by RowGroupType
            foreach (var row in rows)
            {
                if (string.Equals(row.RowGroupType, "Header", StringComparison.OrdinalIgnoreCase))
                {
                    row.Classification = RowClassification.ExplicitHeader;
                    hasExplicitHeader = true;
                }
                else if (string.Equals(row.RowGroupType, "Footer", StringComparison.OrdinalIgnoreCase))
                {
                    row.Classification = RowClassification.Footer;
                }
            }

            // Second pass: classify Body rows
            bool headerAssigned = hasExplicitHeader;
            bool lastWasHeader = hasExplicitHeader;

            foreach (var row in rows)
            {
                // Skip already-classified rows
                if (row.Classification.HasValue)
                {
                    lastWasHeader = row.Classification == RowClassification.ExplicitHeader;
                    continue;
                }

                // Body row processing
                if (!headerAssigned)
                {
                    // Always promote first body row to InferredHeader
                    row.Classification = RowClassification.InferredHeader;
                    headerAssigned = true;
                    lastWasHeader = true;
                    continue;
                }

                // Check for continuation header (consecutive body rows with all "th" cells)
                if (lastWasHeader && row.Cells != null && row.Cells.Count > 0 &&
                    row.Cells.All(c => string.Equals(c.CellType, "th", StringComparison.OrdinalIgnoreCase)))
                {
                    row.Classification = RowClassification.ContinuationHeader;
                    lastWasHeader = true;
                    continue;
                }

                lastWasHeader = false;

                // Check for SOC divider
                if (detectSocDivider(row, totalColumnCount))
                {
                    row.Classification = RowClassification.SocDivider;
                    row.SocName = row.Cells?.FirstOrDefault()?.CleanedText;
                    continue;
                }

                row.Classification = RowClassification.DataBody;
            }

            // Assign absolute row indices
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].AbsoluteRowIndex = i;
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects whether a row is a SOC (System Organ Class) divider.
        /// </summary>
        /// <param name="row">The row to test.</param>
        /// <param name="totalColumnCount">Total column count of the table.</param>
        /// <returns>True if the row is a SOC divider (single cell spanning full width with non-empty short text).</returns>
        internal static bool detectSocDivider(ReconstructedRow row, int totalColumnCount)
        {
            #region implementation
            if (row.Cells == null || row.Cells.Count != 1)
                return false;

            var cell = row.Cells[0];
            var colSpan = cell.ColSpan ?? 1;

            // Cell must span the full table width (or be the only column in a single-column table)
            if (colSpan < totalColumnCount && totalColumnCount > 1)
                return false;

            // Must have non-empty text
            if (string.IsNullOrWhiteSpace(cell.CleanedText))
                return false;

            // SOC names are typically short category labels, not data rows
            if (cell.CleanedText.Length > 200)
                return false;

            return true;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves absolute column positions for all cells using a 2D occupancy grid.
        /// Handles RowSpan bleeding into subsequent rows and ColSpan spanning multiple columns.
        /// Mutates ProcessedCell.ResolvedColumnStart and ProcessedCell.ResolvedColumnEnd.
        /// </summary>
        /// <seealso cref="ProcessedCell"/>
        /// <param name="rows">All rows in the table, in order.</param>
        /// <param name="totalColumnCount">Maximum column width of the table.</param>
        internal static void resolveColumnPositions(List<ReconstructedRow> rows, int totalColumnCount)
        {
            #region implementation
            if (totalColumnCount <= 0 || rows.Count == 0)
                return;

            // Build occupancy grid: tracks which cells are occupied by prior RowSpan
            var occupied = new bool[rows.Count, totalColumnCount];

            for (int r = 0; r < rows.Count; r++)
            {
                int colCursor = 0;
                foreach (var cell in rows[r].Cells ?? Enumerable.Empty<ProcessedCell>())
                {
                    // Skip occupied columns (from prior RowSpan)
                    while (colCursor < totalColumnCount && occupied[r, colCursor])
                        colCursor++;

                    if (colCursor >= totalColumnCount)
                        break;

                    int cs = cell.ColSpan ?? 1;
                    int rs = cell.RowSpan ?? 1;

                    cell.ResolvedColumnStart = colCursor;
                    cell.ResolvedColumnEnd = colCursor + cs;

                    // Mark occupied for RowSpan × ColSpan area
                    for (int dr = 0; dr < rs && (r + dr) < rows.Count; dr++)
                        for (int dc = 0; dc < cs && (colCursor + dc) < totalColumnCount; dc++)
                            occupied[r + dr, colCursor + dc] = true;

                    colCursor += cs;
                }
            }
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves the multi-level header structure by walking header rows per column.
        /// </summary>
        /// <param name="allRows">All classified rows in the table.</param>
        /// <param name="totalColumnCount">Total column count from the table.</param>
        /// <returns>A ResolvedHeader with column paths.</returns>
        /// <seealso cref="ResolvedHeader"/>
        internal static ResolvedHeader resolveHeaders(List<ReconstructedRow> allRows, int totalColumnCount)
        {
            #region implementation
            var headerRows = allRows
                .Where(r => r.Classification == RowClassification.ExplicitHeader
                         || r.Classification == RowClassification.InferredHeader
                         || r.Classification == RowClassification.ContinuationHeader)
                .OrderBy(r => r.AbsoluteRowIndex)
                .ToList();

            if (headerRows.Count == 0)
            {
                return new ResolvedHeader
                {
                    HeaderRowCount = 0,
                    ColumnCount = totalColumnCount,
                    Columns = new List<HeaderColumn>()
                };
            }

            var columns = new List<HeaderColumn>();
            for (int col = 0; col < totalColumnCount; col++)
            {
                var path = new List<string>();
                var footnotes = new List<string>();

                foreach (var hRow in headerRows)
                {
                    // Find the cell that covers this column
                    var cell = hRow.Cells?.FirstOrDefault(c =>
                        (c.ResolvedColumnStart ?? 0) <= col && col < (c.ResolvedColumnEnd ?? 0));

                    if (cell != null && !string.IsNullOrWhiteSpace(cell.CleanedText))
                    {
                        // Only add if different from previous path entry (avoid duplicates for spanning cells)
                        if (path.Count == 0 || path.Last() != cell.CleanedText)
                            path.Add(cell.CleanedText);

                        if (cell.FootnoteMarkers != null)
                            footnotes.AddRange(cell.FootnoteMarkers);
                    }
                }

                columns.Add(new HeaderColumn
                {
                    ColumnIndex = col,
                    HeaderPath = path,
                    LeafHeaderText = path.Count > 0 ? path.Last() : null,
                    CombinedHeaderText = path.Count > 0 ? string.Join(" > ", path) : null,
                    HeaderFootnoteMarkers = footnotes.Distinct().ToList()
                });
            }

            return new ResolvedHeader
            {
                HeaderRowCount = headerRows.Count,
                ColumnCount = totalColumnCount,
                Columns = columns
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts footnotes from Footer-classified rows as marker → text pairs.
        /// </summary>
        /// <param name="rows">All classified rows in the table.</param>
        /// <returns>Dictionary of marker → footnote text. Empty if no footer rows.</returns>
        internal static Dictionary<string, string> extractFootnotes(List<ReconstructedRow> rows)
        {
            #region implementation
            var footnotes = new Dictionary<string, string>();

            var footerRows = rows.Where(r => r.Classification == RowClassification.Footer).ToList();
            if (footerRows.Count == 0)
                return footnotes;

            foreach (var row in footerRows)
            {
                // Concatenate all cell texts in the footer row
                var cellTexts = row.Cells?
                    .Where(c => !string.IsNullOrWhiteSpace(c.CleanedText))
                    .Select(c => c.CleanedText!)
                    .ToList();

                if (cellTexts == null || cellTexts.Count == 0)
                    continue;

                var fullText = string.Join(" ", cellTexts);

                // Try to parse as "marker text" entries (split on common footnote patterns)
                var match = FootnoteEntryRegex.Match(fullText);
                if (match.Success)
                {
                    var marker = match.Groups["marker"].Value;
                    var text = match.Groups["text"].Value.Trim();
                    if (!footnotes.ContainsKey(marker))
                        footnotes[marker] = text;
                }
                else if (cellTexts.Count > 0)
                {
                    // If no marker pattern, store with row index as key
                    var key = $"footer_{row.AbsoluteRowIndex}";
                    footnotes[key] = fullText;
                }
            }

            return footnotes;
            #endregion
        }

        #endregion

        #region Private Methods

        /**************************************************************/
        /// <summary>
        /// Processes a group of cells from a single row into a ReconstructedRow.
        /// </summary>
        /// <param name="rowCells">All TableCellContext cells for one row, sorted by SequenceNumber.</param>
        /// <seealso cref="ReconstructedRow"/>
        /// <seealso cref="TableCellContext"/>
        /// <returns>A row with processed cells.</returns>
        private static ReconstructedRow processRow(List<TableCellContext> rowCells)
        {
            #region implementation
            var first = rowCells[0];
            var processedCells = rowCells.Select(c => processCell(c)).ToList();

            return new ReconstructedRow
            {
                TextTableRowID = first.TextTableRowID,
                RowGroupType = first.RowGroupType,
                SequenceNumberTextTableRow = first.SequenceNumberTextTableRow,
                Cells = processedCells
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Processes a single TableCellContext into a ProcessedCell.
        /// Extracts footnote markers and styleCode before stripping HTML.
        /// </summary>
        /// <param name="cell">The source cell context.</param>
        /// <returns>A processed cell with cleaned text, footnote markers, and styleCode.</returns>
        /// <seealso cref="TableCellContext"/>
        /// <seealso cref="ProcessedCell"/>
        private static ProcessedCell processCell(TableCellContext cell)
        {
            #region implementation
            var rawText = cell.CellText;

            // Step 1: Extract footnote markers from <sup> tags
            var footnoteMarkers = extractFootnoteMarkers(rawText);

            // Step 2: Extract styleCode attribute
            var styleCode = extractStyleCode(rawText);

            // Step 3: Remove <sup> tags before stripping to avoid contaminating CleanedText
            string? textForStripping = rawText;
            if (!string.IsNullOrWhiteSpace(textForStripping))
                textForStripping = SupTagRegex.Replace(textForStripping, "");

            // Step 4: Strip all remaining HTML
            var cleanedText = stripHtml(textForStripping);

            // Determine if cell is footnote-only (had markers but no other text)
            var isFootnoteOnly = footnoteMarkers.Count > 0 && string.IsNullOrWhiteSpace(cleanedText);

            return new ProcessedCell
            {
                TextTableCellID = cell.TextTableCellID,
                TextTableRowID = cell.TextTableRowID,
                SequenceNumber = cell.SequenceNumber,
                RowSpan = cell.RowSpan,
                ColSpan = cell.ColSpan,
                CellType = cell.CellType,
                CleanedText = cleanedText,
                RawCellText = rawText,
                FootnoteMarkers = footnoteMarkers,
                IsFootnoteOnly = isFootnoteOnly,
                StyleCode = styleCode
            };
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns a sort priority for RowGroupType to order Header → Body → Footer.
        /// </summary>
        /// <param name="rowGroupType">The row group type string.</param>
        /// <returns>0 for Header, 1 for Body, 2 for Footer, 3 for unknown.</returns>
        private static int rowGroupTypePriority(string? rowGroupType)
        {
            #region implementation
            return rowGroupType?.ToLowerInvariant() switch
            {
                "header" => 0,
                "body" => 1,
                "footer" => 2,
                _ => 3
            };
            #endregion
        }

        #endregion
    }
}
