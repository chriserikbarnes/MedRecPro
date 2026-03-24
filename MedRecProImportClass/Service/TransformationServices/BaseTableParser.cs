using System.Text.RegularExpressions;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Abstract base class for all Stage 3 table parsers. Provides shared helper methods
    /// for arm extraction, data row filtering, footnote resolution, observation creation,
    /// type promotion, caption-based value type inference, and population detection.
    /// </summary>
    /// <remarks>
    /// ## Shared Helpers
    /// - <see cref="extractArmDefinitions"/>: Parses header leaf texts into <see cref="ArmDefinition"/> objects
    /// - <see cref="getDataBodyRows"/>: Filters rows to DataBody + SocDivider only
    /// - <see cref="resolveFootnoteText"/>: Joins footnote markers to their definitions
    /// - <see cref="createBaseObservation"/>: Pre-populates provenance + classification fields
    /// - <see cref="applyTypePromotion"/>: Promotes bare Numeric → Percentage in AE context
    /// - <see cref="detectCaptionValueHint"/>: Extracts value type hints from table captions
    /// - <see cref="applyCaptionHint"/>: Applies caption-derived type overrides to parsed values
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
        #region Caption Value Hint Types

        /**************************************************************/
        /// <summary>
        /// Carries value type hints extracted from a table caption. When a caption contains
        /// statistical descriptors like "Mean (SD)" or "Median (Range)", this struct tells
        /// parsers how to interpret the Number (Number) cell pattern.
        /// </summary>
        /// <remarks>
        /// ## Confidence Adjustment
        /// - 1.0 = exact match (caption explicitly states the format)
        /// - 0.85 = bare match (caption says "Mean" but no parenthetical for secondary type)
        /// - Parsers apply this as a multiplier to <see cref="ParsedValue.ParseConfidence"/>
        /// </remarks>
        /// <seealso cref="detectCaptionValueHint"/>
        /// <seealso cref="applyCaptionHint"/>
        protected readonly struct CaptionValueHint
        {
            /**************************************************************/
            /// <summary>
            /// Primary value type inferred from caption (e.g., "Mean", "Median", "GeometricMean").
            /// Null when caption provides no value type hint.
            /// </summary>
            public string? PrimaryValueType { get; init; }

            /**************************************************************/
            /// <summary>
            /// Secondary value type inferred from caption parenthetical (e.g., "SD", "SE", "CV_Percent").
            /// Null when caption has no parenthetical or parenthetical is unrecognized.
            /// </summary>
            public string? SecondaryValueType { get; init; }

            /**************************************************************/
            /// <summary>
            /// Confidence multiplier: 1.0 = no change, 0.85 = reduce for ambiguous hints.
            /// Applied to <see cref="ParsedValue.ParseConfidence"/> when this hint overrides types.
            /// </summary>
            public double ConfidenceAdjustment { get; init; }

            /**************************************************************/
            /// <summary>
            /// Diagnostic source string (e.g., "caption:Mean (SD)") for validation flags.
            /// </summary>
            public string? Source { get; init; }

            /**************************************************************/
            /// <summary>
            /// Bound type inferred from caption (e.g., "90CI", "95CI").
            /// Used to refine the generic "CI" BoundType returned by value_ci_dash pattern.
            /// </summary>
            public string? BoundType { get; init; }

            /**************************************************************/
            /// <summary>
            /// True when no value type hint was detected from the caption.
            /// </summary>
            public bool IsEmpty => PrimaryValueType == null && SecondaryValueType == null && BoundType == null;
        }

        #endregion Caption Value Hint Types

        #region Caption Hint Dictionary (Compiled Patterns)

        /**************************************************************/
        /// <summary>
        /// Compiled regex patterns for extracting value type hints from table captions.
        /// Ordered by specificity (most specific first). First match wins.
        /// </summary>
        /// <remarks>
        /// Patterns match common statistical descriptors in SPL table captions:
        /// "Mean (SD)", "Geometric Mean (%CV)", "Median (Range)", "LS Mean (SE)", etc.
        /// </remarks>
        private static readonly (Regex pattern, CaptionValueHint hint)[] _captionHintPatterns =
        {
            // Mean ± SD / Mean ± Standard Deviation / Mean (±SD)
            (new Regex(@"Mean\s*(?:±|\+/?-)\s*(?:SD|Standard\s*Deviation)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Mean", SecondaryValueType = "SD", ConfidenceAdjustment = 1.0, Source = "caption:Mean ± SD" }),

            // Mean (SD) / Mean (Standard Deviation)
            (new Regex(@"Mean\s*\(\s*(?:SD|Standard\s*Deviation)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Mean", SecondaryValueType = "SD", ConfidenceAdjustment = 1.0, Source = "caption:Mean (SD)" }),

            // Mean (SE) / Mean (Standard Error)
            (new Regex(@"Mean\s*\(\s*(?:SE|Standard\s*Error)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Mean", SecondaryValueType = "SE", ConfidenceAdjustment = 1.0, Source = "caption:Mean (SE)" }),

            // Mean (CV%) / Mean (%CV) / Mean (CV)
            (new Regex(@"Mean\s*\(\s*(?:%?\s*CV\s*%?|CV\s*%)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Mean", SecondaryValueType = "CV_Percent", ConfidenceAdjustment = 1.0, Source = "caption:Mean (CV%)" }),

            // Geometric Mean with CV/SD parenthetical
            (new Regex(@"Geometric\s+Mean\s*\(\s*(?:%?\s*CV\s*%?|CV\s*%)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "GeometricMean", SecondaryValueType = "CV_Percent", ConfidenceAdjustment = 1.0, Source = "caption:GeometricMean (CV%)" }),

            (new Regex(@"Geometric\s+Mean\s*\(\s*(?:SD|Standard\s*Deviation)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "GeometricMean", SecondaryValueType = "SD", ConfidenceAdjustment = 1.0, Source = "caption:GeometricMean (SD)" }),

            // Geometric Mean bare (no parenthetical)
            (new Regex(@"Geometric\s+Mean", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "GeometricMean", SecondaryValueType = null, ConfidenceAdjustment = 0.85, Source = "caption:GeometricMean" }),

            // LS Mean / Least Squares Mean with SE/SD
            (new Regex(@"(?:LS\s*Mean|Least[\s-]*Squares?\s*Mean)\s*\(\s*(?:SE|Standard\s*Error)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "LSMean", SecondaryValueType = "SE", ConfidenceAdjustment = 1.0, Source = "caption:LSMean (SE)" }),

            (new Regex(@"(?:LS\s*Mean|Least[\s-]*Squares?\s*Mean)\s*\(\s*(?:SD|Standard\s*Deviation)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "LSMean", SecondaryValueType = "SD", ConfidenceAdjustment = 1.0, Source = "caption:LSMean (SD)" }),

            // LS Mean bare
            (new Regex(@"(?:LS\s*Mean|Least[\s-]*Squares?\s*Mean)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "LSMean", SecondaryValueType = null, ConfidenceAdjustment = 0.85, Source = "caption:LSMean" }),

            // Median (Range) / Median (Min, Max)
            (new Regex(@"Median\s*\(\s*(?:Range|Min\s*[,;]\s*Max)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Median", SecondaryValueType = null, ConfidenceAdjustment = 1.0, Source = "caption:Median (Range)" }),

            // Median (IQR) / Median (Interquartile Range)
            (new Regex(@"Median\s*\(\s*(?:IQR|Interquartile\s*Range?)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Median", SecondaryValueType = null, ConfidenceAdjustment = 1.0, Source = "caption:Median (IQR)" }),

            // n (%) / Number (Percentage) — confirms n(%) pattern, no override needed
            (new Regex(@"(?:^|\W)n\s*\(\s*%\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = null, SecondaryValueType = "Count", ConfidenceAdjustment = 1.0, Source = "caption:n(%)" }),

            // Mean ratio with 90% CI / Geometric Mean Ratio with 90% CI
            (new Regex(@"(?:Geometric\s+)?Mean\s+[Rr]atio.*?90\s*%\s*CI", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Ratio", BoundType = "90CI", ConfidenceAdjustment = 1.0, Source = "caption:Ratio (90% CI)" }),

            // Mean ratio with 95% CI
            (new Regex(@"(?:Geometric\s+)?Mean\s+[Rr]atio.*?95\s*%\s*CI", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Ratio", BoundType = "95CI", ConfidenceAdjustment = 1.0, Source = "caption:Ratio (95% CI)" }),

            // Bare "Mean ratio" — after specific CI patterns
            (new Regex(@"(?:Geometric\s+)?Mean\s+[Rr]atio", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Ratio", BoundType = null, ConfidenceAdjustment = 0.85, Source = "caption:Ratio" }),

            // Generic 90% CI (no value type specified)
            (new Regex(@"90\s*%\s*CI\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = null, BoundType = "90CI", ConfidenceAdjustment = 1.0, Source = "caption:90% CI" }),

            // Generic 95% CI (no value type specified)
            (new Regex(@"95\s*%\s*CI\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = null, BoundType = "95CI", ConfidenceAdjustment = 1.0, Source = "caption:95% CI" }),

            // Bare Mean (no parenthetical) — must come after all Mean+parenthetical patterns
            (new Regex(@"(?<!\w)Mean(?!\s*\()", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Mean", SecondaryValueType = null, ConfidenceAdjustment = 0.85, Source = "caption:Mean" }),

            // Bare Median (no parenthetical) — must come after all Median+parenthetical patterns
            (new Regex(@"(?<!\w)Median(?!\s*\()", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Median", SecondaryValueType = null, ConfidenceAdjustment = 0.85, Source = "caption:Median" }),
        };

        #endregion Caption Hint Dictionary (Compiled Patterns)

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

        #region Protected Helpers — Caption Value Hint

        /**************************************************************/
        /// <summary>
        /// Analyzes a table caption to extract value type hints. Searches for statistical
        /// descriptors like "Mean (SD)", "Geometric Mean (%CV)", "Median (Range)" that
        /// indicate how the Number (Number) cell pattern should be interpreted.
        /// </summary>
        /// <remarks>
        /// Patterns are evaluated in specificity order (most specific first). For example,
        /// "Mean (SD)" matches before bare "Mean". Returns <c>CaptionValueHint.IsEmpty == true</c>
        /// when no statistical descriptor is found.
        /// </remarks>
        /// <param name="caption">Table caption text (may contain HTML remnants).</param>
        /// <returns>
        /// A <see cref="CaptionValueHint"/> with inferred types and confidence adjustment.
        /// </returns>
        /// <seealso cref="CaptionValueHint"/>
        /// <seealso cref="applyCaptionHint"/>
        protected static CaptionValueHint detectCaptionValueHint(string? caption)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(caption))
                return default;

            foreach (var (pattern, hint) in _captionHintPatterns)
            {
                if (pattern.IsMatch(caption))
                    return hint;
            }

            return default;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies a caption-derived value type hint to a <see cref="ParsedValue"/>,
        /// reinterpreting value types when the caption provides stronger context than
        /// pattern matching alone. Handles the critical case where "3057 (980)" is
        /// misidentified as n(%) when the caption indicates Mean (SD).
        /// </summary>
        /// <remarks>
        /// ## Reinterpretation Cases
        /// - **n_pct → mean_sd**: When ValueParser matches "3057 (980)" as n=3057/pct=980
        ///   but caption says "Mean (SD)", swaps values: primary=3057 (Mean), secondary=980 (SD).
        /// - **Numeric promotion**: When ValueParser returns bare "Numeric" and caption provides
        ///   a specific type (Mean, Median, etc.), promotes the type.
        /// - **SecondaryValueType fill**: When caption specifies a secondary type (SD, SE) and
        ///   parsed has a secondary value with no type, applies the caption's type.
        ///
        /// ## Confidence Adjustment
        /// Applies <see cref="CaptionValueHint.ConfidenceAdjustment"/> as a multiplier when
        /// the hint overrides or augments the parsed type.
        ///
        /// ## Validation Flags
        /// Appends <c>CAPTION_REINTERPRET:{old}→{new}</c> when values are reinterpreted,
        /// or <c>CAPTION_HINT:{source}</c> when type is promoted.
        /// </remarks>
        /// <param name="parsed">The parsed value from <see cref="ValueParser"/>.</param>
        /// <param name="hint">The caption-derived hint from <see cref="detectCaptionValueHint"/>.</param>
        /// <returns>The same ParsedValue, potentially with reinterpreted types and values.</returns>
        /// <seealso cref="CaptionValueHint"/>
        /// <seealso cref="detectCaptionValueHint"/>
        protected static ParsedValue applyCaptionHint(ParsedValue parsed, CaptionValueHint hint)
        {
            #region implementation

            if (hint.IsEmpty)
                return parsed;

            // Case 1: n_pct was matched but caption says Mean/Median/etc.
            // ValueParser parsed "3057 (980)" as: PrimaryValue=980 (Percentage), SecondaryValue=3057 (Count)
            // Caption says Mean (SD): reinterpret as PrimaryValue=3057 (Mean), SecondaryValue=980 (SD)
            if (parsed.ParseRule == "n_pct" &&
                hint.PrimaryValueType != null &&
                hint.PrimaryValueType != "Percentage" &&
                hint.SecondaryValueType != null &&
                hint.SecondaryValueType != "Count")
            {
                var oldPrimary = parsed.PrimaryValue;
                var oldSecondary = parsed.SecondaryValue;

                parsed.PrimaryValue = oldSecondary;        // count → mean
                parsed.PrimaryValueType = hint.PrimaryValueType;
                parsed.SecondaryValue = oldPrimary;        // pct → SD
                parsed.SecondaryValueType = hint.SecondaryValueType;
                parsed.Unit = null;                        // remove "%" unit from n_pct
                parsed.ParseConfidence = parsed.ParseConfidence * hint.ConfidenceAdjustment;
                parsed.ParseRule = $"caption_{hint.PrimaryValueType.ToLowerInvariant()}_{hint.SecondaryValueType.ToLowerInvariant()}";
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags,
                    $"CAPTION_REINTERPRET:n_pct→{hint.PrimaryValueType}({hint.SecondaryValueType})");

                return parsed;
            }

            // Case 2: Bare Numeric → promote to caption's PrimaryValueType
            if (parsed.PrimaryValueType == "Numeric" && hint.PrimaryValueType != null)
            {
                parsed.PrimaryValueType = hint.PrimaryValueType;
                parsed.ParseConfidence = parsed.ParseConfidence * hint.ConfidenceAdjustment;
                parsed.ParseRule = $"{parsed.ParseRule}+caption";
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags,
                    $"CAPTION_HINT:{hint.Source}");

                return parsed;
            }

            // Case 3: Secondary type fill — parsed has secondary value but no type
            if (hint.SecondaryValueType != null &&
                parsed.SecondaryValue != null &&
                string.IsNullOrEmpty(parsed.SecondaryValueType))
            {
                parsed.SecondaryValueType = hint.SecondaryValueType;
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags,
                    $"CAPTION_HINT:{hint.Source}");
            }

            // Case 4: BoundType refinement — caption specifies CI level (e.g., "90% CI" → "90CI")
            if (hint.BoundType != null && parsed.BoundType == "CI")
            {
                parsed.BoundType = hint.BoundType;
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags,
                    $"CAPTION_HINT:{hint.Source}");
            }

            return parsed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Appends a flag to an existing semicolon-delimited validation flags string.
        /// </summary>
        /// <param name="existing">Current flags (may be null/empty).</param>
        /// <param name="flag">New flag to append.</param>
        /// <returns>Combined flags string.</returns>
        private static string appendFlag(string? existing, string flag)
        {
            #region implementation

            return string.IsNullOrEmpty(existing) ? flag : $"{existing}; {flag}";

            #endregion
        }

        #endregion Protected Helpers — Caption Value Hint

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

        #region Protected Helpers — Fault Tolerance

        /**************************************************************/
        /// <summary>
        /// Wraps row-level parsing in a try/catch to enforce table-level atomicity.
        /// If the row parser delegate throws, any observations added during this row
        /// are rolled back and a <see cref="TableParseException"/> is thrown, causing
        /// the entire table to be skipped.
        /// </summary>
        /// <remarks>
        /// Call this inside each parser's <c>foreach (var row in dataRows)</c> loop.
        /// SOC divider handling and subtype/group state updates should remain outside
        /// this wrapper since they are structural navigation, not data parsing.
        /// </remarks>
        /// <param name="table">Source reconstructed table (for context in exception).</param>
        /// <param name="row">The current data row being processed.</param>
        /// <param name="observations">The shared observations list that the delegate appends to.</param>
        /// <param name="rowParser">Delegate containing the row's data-extraction logic.</param>
        /// <exception cref="TableParseException">
        /// Thrown when the row parser delegate throws any exception. Contains structured
        /// context (TextTableID, RowSequence, ParserName) and the original exception as InnerException.
        /// </exception>
        /// <seealso cref="TableParseException"/>
        protected void parseRowSafe(
            ReconstructedTable table,
            ReconstructedRow row,
            List<ParsedObservation> observations,
            Action<ReconstructedRow, List<ParsedObservation>> rowParser)
        {
            #region implementation

            var preCount = observations.Count;

            try
            {
                rowParser(row, observations);
            }
            catch (Exception ex)
            {
                // Roll back any observations added during this row
                if (observations.Count > preCount)
                {
                    observations.RemoveRange(preCount, observations.Count - preCount);
                }

                throw new TableParseException(
                    $"Row {row.SequenceNumberTextTableRow} failed in {GetType().Name}: {ex.Message}",
                    textTableId: table.TextTableID,
                    rowSequence: row.SequenceNumberTextTableRow,
                    parserName: GetType().Name,
                    innerException: ex);
            }

            #endregion
        }

        #endregion Protected Helpers — Fault Tolerance
    }
}
