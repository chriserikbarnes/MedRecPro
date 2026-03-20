using System.Text.RegularExpressions;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Abstract base class for all Stage 3 table parsers. Provides shared helper methods
    /// for arm extraction, data row filtering, footnote resolution, observation creation,
    /// type promotion, and population detection.
    /// </summary>
    /// <remarks>
    /// ## Shared Helpers
    /// - <see cref="extractArmDefinitions"/>: Parses header leaf texts into <see cref="ArmDefinition"/> objects
    /// - <see cref="getDataBodyRows"/>: Filters rows to DataBody + SocDivider only
    /// - <see cref="resolveFootnoteText"/>: Joins footnote markers to their definitions
    /// - <see cref="createBaseObservation"/>: Pre-populates provenance + classification fields
    /// - <see cref="applyTypePromotion"/>: Promotes bare Numeric → Percentage in AE context
    /// - <see cref="detectPopulation"/>: Calls <see cref="PopulationDetector"/> for auto-detection
    ///
    /// ## Usage Pattern
    /// Concrete parsers override <see cref="ITableParser.SupportedCategory"/>,
    /// <see cref="ITableParser.Priority"/>, <see cref="ITableParser.CanParse"/>,
    /// and <see cref="ITableParser.Parse"/>.
    /// </remarks>
    /// <seealso cref="ITableParser"/>
    /// <seealso cref="ValueParser"/>
    /// <seealso cref="PopulationDetector"/>
    /// <seealso cref="ReconstructedTable"/>
    public abstract class BaseTableParser : ITableParser
    {
        #region Compiled Regex Patterns

        // Pattern for detecting stat/comparison column headers
        private static readonly Regex _statColumnPattern = new(
            @"(?:P[\s-]*[Vv]alue|Difference|Risk\s+Reduction|ARR|RR|HR|OR|95%?\s*CI|Relative\s+Risk)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern for detecting timepoint column headers
        private static readonly Regex _timepointPattern = new(
            @"(?:(?:Week|Month|Year|Day)\s*\d+|\d+\s*(?:Weeks?|Months?|Years?|Days?))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Compiled Regex Patterns

        #region Abstract / Virtual Members

        /**************************************************************/
        /// <summary>
        /// The table category this parser handles.
        /// </summary>
        public abstract TableCategory SupportedCategory { get; }

        /**************************************************************/
        /// <summary>
        /// Selection priority within the same category. Lower = tried first.
        /// </summary>
        public abstract int Priority { get; }

        /**************************************************************/
        /// <summary>
        /// Structural check: can this parser handle the given table?
        /// </summary>
        /// <param name="table">The reconstructed table to evaluate.</param>
        /// <returns>True if this parser can handle the table.</returns>
        public abstract bool CanParse(ReconstructedTable table);

        /**************************************************************/
        /// <summary>
        /// Parses the table into flat observations.
        /// </summary>
        /// <param name="table">The reconstructed table from Stage 2.</param>
        /// <returns>List of parsed observations.</returns>
        public abstract List<ParsedObservation> Parse(ReconstructedTable table);

        #endregion Abstract / Virtual Members

        #region Protected Helpers — Arm Extraction

        /**************************************************************/
        /// <summary>
        /// Extracts arm definitions from the resolved header's leaf texts using
        /// <see cref="ValueParser.ParseArmHeader"/>. Skips column 0 (parameter name column).
        /// </summary>
        /// <param name="table">Table with resolved header.</param>
        /// <returns>List of arm definitions with column positions. Empty if no header.</returns>
        /// <seealso cref="ArmDefinition"/>
        /// <seealso cref="ValueParser.ParseArmHeader"/>
        protected static List<ArmDefinition> extractArmDefinitions(ReconstructedTable table)
        {
            #region implementation

            var arms = new List<ArmDefinition>();
            if (table.Header?.Columns == null || table.Header.Columns.Count <= 1)
                return arms;

            // Skip column 0 (parameter name column)
            for (int i = 1; i < table.Header.Columns.Count; i++)
            {
                var col = table.Header.Columns[i];
                var leafText = col.LeafHeaderText;

                if (string.IsNullOrWhiteSpace(leafText))
                    continue;

                var arm = ValueParser.ParseArmHeader(leafText);
                if (arm != null)
                {
                    arm.ColumnIndex = col.ColumnIndex;

                    // Assign study context from parent header path if multi-level
                    if (col.HeaderPath != null && col.HeaderPath.Count > 1)
                    {
                        arm.StudyContext = col.HeaderPath[0];
                    }

                    arms.Add(arm);
                }
                else
                {
                    // No N= found — create basic arm definition
                    arms.Add(new ArmDefinition
                    {
                        Name = leafText.Trim(),
                        ColumnIndex = col.ColumnIndex,
                        StudyContext = col.HeaderPath != null && col.HeaderPath.Count > 1
                            ? col.HeaderPath[0]
                            : null
                    });
                }
            }

            return arms;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a header column represents a stat/comparison column
        /// (P-value, Difference, Risk Reduction, etc.) rather than a treatment arm.
        /// </summary>
        /// <param name="headerText">The leaf header text to evaluate.</param>
        /// <returns>True if the column is a stat column.</returns>
        protected static bool isStatColumn(string? headerText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(headerText))
                return false;

            return _statColumnPattern.IsMatch(headerText);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a header column represents a timepoint
        /// (Week 12, 6 Months, Year 3, etc.).
        /// </summary>
        /// <param name="headerText">The leaf header text to evaluate.</param>
        /// <returns>True if the column is a timepoint column.</returns>
        protected static bool isTimepointColumn(string? headerText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(headerText))
                return false;

            return _timepointPattern.IsMatch(headerText);

            #endregion
        }

        #endregion Protected Helpers — Arm Extraction

        #region Protected Helpers — Row Filtering

        /**************************************************************/
        /// <summary>
        /// Returns only DataBody and SocDivider rows from the table, in order.
        /// Excludes header and footer rows.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>Filtered row list for parsing iteration.</returns>
        protected static List<ReconstructedRow> getDataBodyRows(ReconstructedTable table)
        {
            #region implementation

            if (table.Rows == null)
                return new List<ReconstructedRow>();

            return table.Rows
                .Where(r => r.Classification == RowClassification.DataBody ||
                            r.Classification == RowClassification.SocDivider)
                .ToList();

            #endregion
        }

        #endregion Protected Helpers — Row Filtering

        #region Protected Helpers — Footnote Resolution

        /**************************************************************/
        /// <summary>
        /// Resolves footnote markers to their full text definitions from the table's
        /// footnote dictionary, semicolon-delimited.
        /// </summary>
        /// <param name="markers">Footnote marker strings (e.g., "a", "b").</param>
        /// <param name="footnotes">Table-level footnote dictionary (marker → text).</param>
        /// <returns>Semicolon-delimited footnote text, or null if no matches.</returns>
        protected static string? resolveFootnoteText(List<string>? markers, Dictionary<string, string>? footnotes)
        {
            #region implementation

            if (markers == null || markers.Count == 0 || footnotes == null || footnotes.Count == 0)
                return null;

            var resolved = new List<string>();
            foreach (var marker in markers)
            {
                if (footnotes.TryGetValue(marker.Trim(), out var text))
                {
                    resolved.Add(text);
                }
            }

            return resolved.Count > 0 ? string.Join("; ", resolved) : null;

            #endregion
        }

        #endregion Protected Helpers — Footnote Resolution

        #region Protected Helpers — Observation Creation

        /**************************************************************/
        /// <summary>
        /// Creates a base <see cref="ParsedObservation"/> pre-populated with provenance
        /// and classification fields from the table and row context.
        /// </summary>
        /// <param name="table">Source reconstructed table.</param>
        /// <param name="row">Source row.</param>
        /// <param name="cell">Source cell.</param>
        /// <param name="category">Table category from router.</param>
        /// <returns>Pre-populated observation ready for value decomposition.</returns>
        /// <seealso cref="ParsedObservation"/>
        protected static ParsedObservation createBaseObservation(
            ReconstructedTable table, ReconstructedRow row, ProcessedCell cell, TableCategory category)
        {
            #region implementation

            return new ParsedObservation
            {
                // Provenance
                DocumentGUID = table.DocumentGUID,
                LabelerName = table.LabelerName,
                ProductTitle = table.Title != null ? TextUtil.RemoveTags(table.Title) : null,
                VersionNumber = table.VersionNumber,
                TextTableID = table.TextTableID,
                Caption = table.Caption,
                SourceRowSeq = row.SequenceNumberTextTableRow,
                SourceCellSeq = cell.SequenceNumber,

                // Classification
                TableCategory = category.ToString(),
                ParentSectionCode = table.ParentSectionCode,
                ParentSectionTitle = table.ParentSectionTitle,
                SectionTitle = table.SectionTitle,

                // Raw value preserved for audit
                RawValue = cell.CleanedText,

                // Footnote markers as comma-delimited string
                FootnoteMarkers = cell.FootnoteMarkers != null && cell.FootnoteMarkers.Count > 0
                    ? string.Join(", ", cell.FootnoteMarkers)
                    : null,

                // Resolve footnote text
                FootnoteText = resolveFootnoteText(cell.FootnoteMarkers, table.Footnotes)
            };

            #endregion
        }

        #endregion Protected Helpers — Observation Creation

        #region Protected Helpers — Type Promotion

        /**************************************************************/
        /// <summary>
        /// Applies context-specific type promotion to a parsed value. In ADVERSE_EVENT
        /// context, bare Numeric values are promoted to Percentage when the arm's format
        /// hint suggests percentage display.
        /// </summary>
        /// <param name="parsed">The parsed value from <see cref="ValueParser"/>.</param>
        /// <param name="category">Table category for context.</param>
        /// <param name="arm">Arm definition with format hint.</param>
        /// <returns>The same ParsedValue, potentially with promoted PrimaryValueType.</returns>
        /// <seealso cref="ParsedValue"/>
        protected static ParsedValue applyTypePromotion(ParsedValue parsed, TableCategory category, ArmDefinition? arm)
        {
            #region implementation

            // Type promotion: bare Numeric → Percentage in AE/Efficacy context
            if (parsed.PrimaryValueType == "Numeric" &&
                (category == TableCategory.ADVERSE_EVENT || category == TableCategory.EFFICACY))
            {
                // Promote if arm format hint contains % or n(%)
                if (arm?.FormatHint != null &&
                    (arm.FormatHint.Contains("%") || arm.FormatHint.Contains("n(")))
                {
                    parsed.PrimaryValueType = "Percentage";
                    parsed.Unit = "%";
                }
            }

            return parsed;

            #endregion
        }

        #endregion Protected Helpers — Type Promotion

        #region Protected Helpers — Population Detection

        /**************************************************************/
        /// <summary>
        /// Detects patient population from the table's Caption and SectionTitle using
        /// <see cref="PopulationDetector.DetectPopulation"/>.
        /// </summary>
        /// <param name="table">The reconstructed table.</param>
        /// <returns>Tuple of (population string, confidence score).</returns>
        /// <seealso cref="PopulationDetector"/>
        protected static (string? population, double confidence) detectPopulation(ReconstructedTable table)
        {
            #region implementation

            return PopulationDetector.DetectPopulation(
                table.Caption,
                table.SectionTitle,
                table.ParentSectionTitle);

            #endregion
        }

        #endregion Protected Helpers — Population Detection

        #region Protected Helpers — Cell Lookup

        /**************************************************************/
        /// <summary>
        /// Finds the cell in a row that covers the given resolved column index.
        /// Uses ResolvedColumnStart and ResolvedColumnEnd for grid-aware lookup.
        /// </summary>
        /// <param name="row">The row to search.</param>
        /// <param name="columnIndex">The 0-based resolved column index.</param>
        /// <returns>The cell covering the column, or null if not found.</returns>
        protected static ProcessedCell? getCellAtColumn(ReconstructedRow row, int columnIndex)
        {
            #region implementation

            if (row.Cells == null)
                return null;

            return row.Cells.FirstOrDefault(c =>
                c.ResolvedColumnStart != null &&
                c.ResolvedColumnEnd != null &&
                c.ResolvedColumnStart <= columnIndex &&
                c.ResolvedColumnEnd > columnIndex);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the parameter name from column 0 of a data row, with footnote markers
        /// cleaned via <see cref="ValueParser.CleanParameterName"/>.
        /// </summary>
        /// <param name="row">The data row.</param>
        /// <returns>Tuple of (cleaned parameter name, footnote markers string).</returns>
        protected static (string? name, string? markers) getParameterName(ReconstructedRow row)
        {
            #region implementation

            var cell = getCellAtColumn(row, 0);
            if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                return (null, null);

            var (name, markers) = ValueParser.CleanParameterName(cell.CleanedText);
            return (name, markers);

            #endregion
        }

        #endregion Protected Helpers — Cell Lookup

        #region Protected Helpers — Value Application

        /**************************************************************/
        /// <summary>
        /// Applies a <see cref="ParsedValue"/> to a <see cref="ParsedObservation"/>,
        /// mapping all decomposed value fields.
        /// </summary>
        /// <param name="obs">Target observation to populate.</param>
        /// <param name="parsed">Source parsed value.</param>
        /// <seealso cref="ParsedValue"/>
        /// <seealso cref="ParsedObservation"/>
        protected static void applyParsedValue(ParsedObservation obs, ParsedValue parsed)
        {
            #region implementation

            obs.PrimaryValue = parsed.PrimaryValue;
            obs.PrimaryValueType = parsed.PrimaryValueType;
            obs.SecondaryValue = parsed.SecondaryValue;
            obs.SecondaryValueType = parsed.SecondaryValueType;
            obs.LowerBound = parsed.LowerBound;
            obs.UpperBound = parsed.UpperBound;
            obs.BoundType = parsed.BoundType;
            obs.PValue = parsed.PValue;
            obs.Unit = parsed.Unit ?? obs.Unit;
            obs.ParseConfidence = parsed.ParseConfidence;
            obs.ParseRule = parsed.ParseRule;

            // Append validation flags from value parsing
            if (!string.IsNullOrEmpty(parsed.ValidationFlags))
            {
                obs.ValidationFlags = string.IsNullOrEmpty(obs.ValidationFlags)
                    ? parsed.ValidationFlags
                    : $"{obs.ValidationFlags}; {parsed.ValidationFlags}";
            }

            #endregion
        }

        #endregion Protected Helpers — Value Application
    }
}
