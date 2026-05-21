using System.Text.RegularExpressions;
using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.SampleSize;

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
    public abstract class BaseTableParser : ITableParser, ITableParserDiagnostics
    {
        #region Diagnostics

        /**************************************************************/
        /// <summary>
        /// Service that owns structural suppression diagnostics for the current parse.
        /// </summary>
        private readonly StructuralRowSuppressionService _suppressionDiagnostics = new();

        /**************************************************************/
        /// <inheritdoc/>
        public IReadOnlyList<TableSuppressionAuditRecord> SuppressedRows => _suppressionDiagnostics.SuppressedRows;

        /**************************************************************/
        /// <inheritdoc/>
        public void ClearDiagnostics()
        {
            #region implementation

            _suppressionDiagnostics.ClearDiagnostics();

            #endregion
        }

        #endregion Diagnostics

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
        /// - <see cref="ParsedValue.ConfidenceAdjustment.AmbiguousCaptionHint"/> = bare match (caption says "Mean" but no parenthetical for secondary type)
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
                new CaptionValueHint { PrimaryValueType = "GeometricMean", SecondaryValueType = null, ConfidenceAdjustment = ParsedValue.ConfidenceAdjustment.AmbiguousCaptionHint, Source = "caption:GeometricMean" }),

            // LS Mean / Least Squares Mean with SE/SD
            (new Regex(@"(?:LS\s*Mean|Least[\s-]*Squares?\s*Mean)\s*\(\s*(?:SE|Standard\s*Error)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "LSMean", SecondaryValueType = "SE", ConfidenceAdjustment = 1.0, Source = "caption:LSMean (SE)" }),

            (new Regex(@"(?:LS\s*Mean|Least[\s-]*Squares?\s*Mean)\s*\(\s*(?:SD|Standard\s*Deviation)\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "LSMean", SecondaryValueType = "SD", ConfidenceAdjustment = 1.0, Source = "caption:LSMean (SD)" }),

            // LS Mean bare
            (new Regex(@"(?:LS\s*Mean|Least[\s-]*Squares?\s*Mean)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "LSMean", SecondaryValueType = null, ConfidenceAdjustment = ParsedValue.ConfidenceAdjustment.AmbiguousCaptionHint, Source = "caption:LSMean" }),

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
                new CaptionValueHint { PrimaryValueType = "Ratio", BoundType = null, ConfidenceAdjustment = ParsedValue.ConfidenceAdjustment.AmbiguousCaptionHint, Source = "caption:Ratio" }),

            // Generic 90% CI (no value type specified)
            (new Regex(@"90\s*%\s*CI\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = null, BoundType = "90CI", ConfidenceAdjustment = 1.0, Source = "caption:90% CI" }),

            // Generic 95% CI (no value type specified)
            (new Regex(@"95\s*%\s*CI\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = null, BoundType = "95CI", ConfidenceAdjustment = 1.0, Source = "caption:95% CI" }),

            // Bare Mean (no parenthetical) — must come after all Mean+parenthetical patterns
            (new Regex(@"(?<!\w)Mean(?!\s*\()", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Mean", SecondaryValueType = null, ConfidenceAdjustment = ParsedValue.ConfidenceAdjustment.AmbiguousCaptionHint, Source = "caption:Mean" }),

            // Bare Median (no parenthetical) — must come after all Median+parenthetical patterns
            (new Regex(@"(?<!\w)Median(?!\s*\()", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                new CaptionValueHint { PrimaryValueType = "Median", SecondaryValueType = null, ConfidenceAdjustment = ParsedValue.ConfidenceAdjustment.AmbiguousCaptionHint, Source = "caption:Median" }),
        };

        #endregion Caption Hint Dictionary (Compiled Patterns)

        #region Compiled Regex Patterns

        /**************************************************************/
        /// <summary>
        /// Matches standalone less-than-one incidence cells that need AE/Efficacy
        /// percentage-context coercion rather than generic numeric parsing.
        /// </summary>
        private static readonly Regex _standaloneLtOnePattern = new(
            @"^\s*[<≤]\s*1\s*%?\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Detects p-value row labels so value parsing can preserve p-values as
        /// statistics instead of ordinary arm measurements.
        /// </summary>
        private static readonly Regex _pValueRowLabelPattern = new(
            @"\bp\s*[- ]?\s*value\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Detects comparison/statistic rows that describe cross-arm effects rather
        /// than per-arm clinical observations.
        /// </summary>
        private static readonly Regex _comparisonRowLabelPattern = new(
            @"^\s*(?:.*\bp\s*[- ]?\s*value\b.*|Difference\s+from\s+placebo.*|95\s*%\s*CI|Risk\s+Difference.*|Hazard\s+Ratio.*|Relative\s+Risk.*|Odds\s+Ratio.*)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Detects contextual hints that a bare numeric cell should be interpreted
        /// as a percentage.
        /// </summary>
        private static readonly Regex _percentContextPattern = new(
            @"(?:%|percent|percentage|responders?|response\s+rate|incidence|rate)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Detects contextual hints that a bare numeric cell should be interpreted
        /// as a count rather than a percentage.
        /// </summary>
        private static readonly Regex _countContextPattern = new(
            @"(?:^|\b)(?:n/N|Number|No\.?|Count|Events?|Patients?|Subjects?|Total)(?:\b|$)",
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
        /// Extracts arm definitions from the resolved header's leaf texts.
        /// </summary>
        /// <param name="table">Table with resolved header.</param>
        /// <returns>List of arm definitions with column positions.</returns>
        /// <seealso cref="ArmDefinitionExtractor"/>
        protected static List<ArmDefinition> extractArmDefinitions(ReconstructedTable table)
        {
            #region implementation

            return ArmDefinitionExtractor.ExtractArmDefinitions(table);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the best arm-name candidate text for a header column.
        /// </summary>
        /// <param name="col">Header column to recover an arm name from.</param>
        /// <returns>The recovered arm-name text, or <c>null</c> when no candidate qualifies.</returns>
        /// <seealso cref="ArmDefinitionExtractor"/>
        protected static string? recoverArmHeaderText(HeaderColumn col)
        {
            #region implementation

            return ArmDefinitionExtractor.RecoverArmHeaderText(col);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns <c>true</c> when header text is structural rather than a treatment arm.
        /// </summary>
        /// <param name="text">Header text to evaluate.</param>
        /// <returns><c>true</c> when the text should not be used as a treatment arm.</returns>
        /// <seealso cref="ArmDefinitionExtractor"/>
        protected static bool looksLikeGenericArmLabel(string? text)
        {
            #region implementation

            return ArmDefinitionExtractor.LooksLikeGenericArmLabel(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Router-facing wrapper for the shared generic arm-label detector.
        /// </summary>
        /// <param name="text">Header text to evaluate.</param>
        /// <returns><c>true</c> when the text is structural rather than a treatment arm.</returns>
        /// <seealso cref="ArmDefinitionExtractor"/>
        internal static bool LooksLikeGenericArmLabelForRouting(string? text)
        {
            #region implementation

            return ArmDefinitionExtractor.LooksLikeGenericArmLabel(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether an arm definition has a usable treatment-arm name.
        /// </summary>
        /// <param name="arm">Arm definition to evaluate.</param>
        /// <returns><c>true</c> when observations may be emitted for this arm.</returns>
        /// <seealso cref="ArmDefinitionExtractor"/>
        protected static bool hasUsableTreatmentArm(ArmDefinition? arm)
        {
            #region implementation

            return ArmDefinitionExtractor.HasUsableTreatmentArm(arm);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies a conservative single-product fallback when no column has a usable treatment arm.
        /// </summary>
        /// <param name="table">Reconstructed source table.</param>
        /// <param name="arms">Arm definitions to update in place.</param>
        /// <returns><c>true</c> when a product arm was applied.</returns>
        /// <seealso cref="ArmDefinitionExtractor"/>
        protected static bool applySingleProductArmFallback(ReconstructedTable table, List<ArmDefinition> arms)
        {
            #region implementation

            return ArmDefinitionExtractor.ApplySingleProductArmFallback(table, arms);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a parsed AE row is structural metadata rather than a reportable observation.
        /// </summary>
        /// <param name="obs">Observation candidate after value parsing.</param>
        /// <param name="parsed">Parsed value decomposition.</param>
        /// <returns><c>true</c> when the row should be suppressed.</returns>
        /// <seealso cref="StructuralRowSuppressionService"/>
        protected static bool shouldSuppressAeStructuralObservation(ParsedObservation obs, ParsedValue parsed)
        {
            #region implementation

            return StructuralRowSuppressionService.ShouldSuppressAeStructuralObservation(obs, parsed);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a parsed AE or Efficacy row is structural metadata rather than a reportable observation.
        /// </summary>
        /// <param name="obs">Observation candidate after value parsing.</param>
        /// <param name="parsed">Parsed value decomposition.</param>
        /// <returns><c>true</c> when the row should be suppressed.</returns>
        /// <seealso cref="StructuralRowSuppressionService"/>
        protected static bool shouldSuppressStructuralObservation(ParsedObservation obs, ParsedValue parsed)
        {
            #region implementation

            return StructuralRowSuppressionService.ShouldSuppressStructuralObservation(obs, parsed);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether text is a known structural row label.
        /// </summary>
        /// <param name="text">Candidate row or cell text.</param>
        /// <returns><c>true</c> when the text should be retained as context only.</returns>
        /// <seealso cref="StructuralRowSuppressionService"/>
        protected static bool isStructuralRowLabel(string? text)
        {
            #region implementation

            return StructuralRowSuppressionService.IsStructuralRowLabel(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a data-body row is structural context only.
        /// </summary>
        /// <param name="row">Candidate data row.</param>
        /// <param name="arms">Resolved arm definitions for the table.</param>
        /// <param name="parameterName">Cleaned row label from column 0.</param>
        /// <param name="category">Parser category evaluating the row.</param>
        /// <returns><c>true</c> when the row carries context and no reportable value cells.</returns>
        /// <seealso cref="StructuralRowSuppressionService"/>
        protected static bool isStructuralContextRow(
            ReconstructedRow row,
            IEnumerable<ArmDefinition> arms,
            string? parameterName,
            TableCategory category)
        {
            #region implementation

            return StructuralRowSuppressionService.IsStructuralContextRow(row, arms, parameterName, category);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects mid-body subpopulation header rows.
        /// </summary>
        /// <param name="row">Candidate body row.</param>
        /// <param name="arms">Resolved arm definitions for the table.</param>
        /// <param name="paramName">Cleaned row label from column 0.</param>
        /// <param name="subpopName">Subpopulation label for subsequent rows.</param>
        /// <param name="nOverrides">Per-arm <c>ColumnIndex</c> to sample-size overrides.</param>
        /// <returns><c>true</c> when the row is a subpopulation header and should be suppressed.</returns>
        /// <seealso cref="StructuralRowSuppressionService"/>
        protected static bool tryDetectSubpopulationHeader(
            ReconstructedRow row,
            IEnumerable<ArmDefinition> arms,
            string? paramName,
            out string? subpopName,
            out IDictionary<int, int> nOverrides)
        {
            #region implementation

            return StructuralRowSuppressionService.TryDetectSubpopulationHeader(
                row, arms, paramName, out subpopName, out nOverrides);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects and consumes AE denominator metadata rows before observation emission.
        /// </summary>
        /// <param name="table">Source reconstructed table.</param>
        /// <param name="row">Candidate body row.</param>
        /// <param name="arms">Resolved treatment-arm definitions.</param>
        /// <param name="paramName">Cleaned column-zero row label.</param>
        /// <param name="hasEmittedAeObservation">Whether a reportable AE row has already emitted.</param>
        /// <param name="followsResetBoundary">Whether the row follows a section/category reset.</param>
        /// <param name="tableLevelArmN">Persistent table-level denominator map.</param>
        /// <param name="sectionArmNOverrides">Resettable section-level denominator map.</param>
        /// <param name="denominatorScope">Detected denominator lifetime.</param>
        /// <param name="subpopulationName">Subpopulation name when the row opens a subpopulation scope.</param>
        /// <param name="subpopulationOverrides">Subpopulation denominator map when applicable.</param>
        /// <returns>True when the row was consumed as denominator metadata or rejected evidence.</returns>
        /// <seealso cref="AeDenominatorRowDetector"/>
        private protected bool tryConsumeAeDenominatorRow(
            ReconstructedTable table,
            ReconstructedRow row,
            IEnumerable<ArmDefinition> arms,
            string? paramName,
            bool hasEmittedAeObservation,
            bool followsResetBoundary,
            IDictionary<int, int> tableLevelArmN,
            IDictionary<int, int> sectionArmNOverrides,
            out AeDenominatorRowScope denominatorScope,
            out string? subpopulationName,
            out IDictionary<int, int>? subpopulationOverrides)
        {
            #region implementation

            denominatorScope = AeDenominatorRowScope.None;
            subpopulationName = null;
            subpopulationOverrides = null;

            var detection = AeDenominatorRowDetector.Detect(
                row,
                arms,
                paramName,
                new AeDenominatorRowContext(hasEmittedAeObservation, followsResetBoundary));

            if (detection.Scope == AeDenominatorRowScope.None)
                return false;

            denominatorScope = detection.Scope;
            var validationFlag = detection.DiagnosticFlag ?? ArmNResolver.FromMetadataRowFlag;
            var reason = detection.DiagnosticReason ?? "AE denominator metadata row captured before observation emission.";

            var conflict = detection.Scope switch
            {
                AeDenominatorRowScope.TableLevel => hasConflictingDenominator(tableLevelArmN, detection.PerColumnN),
                AeDenominatorRowScope.SectionLevel => hasConflictingDenominator(sectionArmNOverrides, detection.PerColumnN),
                _ => false
            };

            if (conflict)
            {
                validationFlag = ArmNResolver.RejectedConflictingNFlag;
                reason = "Conflicting denominator metadata row rejected; existing scoped ArmN preserved.";
            }
            else
            {
                switch (detection.Scope)
                {
                    case AeDenominatorRowScope.TableLevel:
                        copyDenominatorValues(detection.PerColumnN, tableLevelArmN);
                        break;

                    case AeDenominatorRowScope.SectionLevel:
                        copyDenominatorValues(detection.PerColumnN, sectionArmNOverrides);
                        break;

                    case AeDenominatorRowScope.Subpopulation:
                        subpopulationName = detection.SubpopulationName;
                        subpopulationOverrides = new Dictionary<int, int>(detection.PerColumnN);
                        break;
                }
            }

            recordSuppressedStructuralRow(
                table,
                row,
                null,
                TableCategory.ADVERSE_EVENT,
                paramName,
                null,
                paramName,
                detection.SubpopulationName ?? paramName,
                detection.Scope == AeDenominatorRowScope.Subpopulation ? "Subpopulation" : "ArmN",
                reason,
                validationFlag);
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Chooses the active scoped AE denominator for one arm column.
        /// </summary>
        /// <param name="columnIndex">Resolved arm column index.</param>
        /// <param name="subpopulationArmN">Most-specific subpopulation denominator map.</param>
        /// <param name="sectionArmN">Section-level denominator map.</param>
        /// <param name="tableLevelArmN">Persistent table-level denominator map.</param>
        /// <returns>The active scoped denominator, or null.</returns>
        /// <seealso cref="ArmNResolver.BuildValueContextArm"/>
        protected static int? getScopedAeArmN(
            int? columnIndex,
            IDictionary<int, int> subpopulationArmN,
            IDictionary<int, int> sectionArmN,
            IDictionary<int, int> tableLevelArmN)
        {
            #region implementation

            if (!columnIndex.HasValue)
                return null;

            if (subpopulationArmN.TryGetValue(columnIndex.Value, out var subpopulationN))
                return subpopulationN;

            if (sectionArmN.TryGetValue(columnIndex.Value, out var sectionN))
                return sectionN;

            if (tableLevelArmN.TryGetValue(columnIndex.Value, out var tableN))
                return tableN;

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Infers strict column-consensus ArmN values from repeated count-percent AE cells.
        /// </summary>
        /// <param name="rows">Candidate data rows.</param>
        /// <param name="arms">Resolved treatment-arm definitions.</param>
        /// <returns>Inferred sample sizes by arm column index.</returns>
        /// <seealso cref="SampleSizeParser.TryInferColumnConsensusSampleSize"/>
        protected static IDictionary<int, int> inferAeColumnConsensusArmN(
            IEnumerable<ReconstructedRow> rows,
            IEnumerable<ArmDefinition> arms)
        {
            #region implementation

            var inferred = new Dictionary<int, int>();
            foreach (var arm in arms.Where(hasUsableTreatmentArm))
            {
                if (!arm.ColumnIndex.HasValue || arm.SampleSize is > 0)
                    continue;

                var observations = new List<(int count, decimal percent)>();
                foreach (var row in rows)
                {
                    var (paramName, _) = getParameterName(row);
                    if (string.IsNullOrWhiteSpace(paramName) ||
                        AeDenominatorRowDetector.Detect(
                            row,
                            new[] { arm },
                            paramName,
                            new AeDenominatorRowContext(true, true)).Scope != AeDenominatorRowScope.None)
                    {
                        continue;
                    }

                    var cell = getCellAtColumn(row, arm.ColumnIndex.Value);
                    if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                        continue;

                    var parsed = ValueParser.Parse(cell.CleanedText);
                    if (!string.Equals(parsed.ParseRule, "n_pct", StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(parsed.PrimaryValueType, "Percentage", StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(parsed.SecondaryValueType, "Count", StringComparison.OrdinalIgnoreCase) ||
                        parsed.PrimaryValue is not > 0 ||
                        parsed.SecondaryValue is not > 0)
                    {
                        continue;
                    }

                    observations.Add(((int)Math.Round(parsed.SecondaryValue.Value), (decimal)parsed.PrimaryValue.Value));
                }

                if (SampleSizeParser.TryInferColumnConsensusSampleSize(observations, out var evidence) &&
                    evidence.IsExact &&
                    evidence.Value is > 0)
                {
                    inferred[arm.ColumnIndex.Value] = evidence.Value.Value;
                }
            }

            return inferred;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies column-consensus ArmN evidence to a parsed AE value when no stronger N exists.
        /// </summary>
        /// <param name="parsed">Parsed value to enrich.</param>
        /// <param name="arm">Resolved treatment-arm definition.</param>
        /// <param name="scopedArmN">Current scoped denominator, if any.</param>
        /// <param name="columnConsensusArmN">Consensus denominators by column index.</param>
        /// <seealso cref="ArmNResolver.FromCountPercentInferenceFlag"/>
        protected static void applyAeColumnConsensusArmN(
            ParsedValue parsed,
            ArmDefinition arm,
            int? scopedArmN,
            IDictionary<int, int> columnConsensusArmN)
        {
            #region implementation

            if (scopedArmN is > 0 ||
                arm.SampleSize is > 0 ||
                parsed.SampleSize is > 0 ||
                !arm.ColumnIndex.HasValue ||
                !string.Equals(parsed.ParseRule, "n_pct", StringComparison.OrdinalIgnoreCase) ||
                !columnConsensusArmN.TryGetValue(arm.ColumnIndex.Value, out var inferredN))
            {
                return;
            }

            parsed.SampleSize = inferredN;
            parsed.ParseRule = "count_percent_inference";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects whether incoming denominator values conflict with an active map.
        /// </summary>
        private static bool hasConflictingDenominator(
            IDictionary<int, int> active,
            IReadOnlyDictionary<int, int> incoming)
        {
            #region implementation

            return incoming.Any(kvp =>
                active.TryGetValue(kvp.Key, out var existing) &&
                existing != kvp.Value);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Copies exact denominator values into an active scope map.
        /// </summary>
        private static void copyDenominatorValues(
            IReadOnlyDictionary<int, int> source,
            IDictionary<int, int> target)
        {
            #region implementation

            foreach (var kvp in source)
                target[kvp.Key] = kvp.Value;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects combined/all-patient row labels that should suppress the row and reset subpopulation context.
        /// </summary>
        /// <param name="paramName">Cleaned row label from column 0.</param>
        /// <param name="row">Candidate body row.</param>
        /// <param name="arms">Resolved arm definitions for the table.</param>
        /// <returns><c>true</c> when the row is a combined/all-patients row.</returns>
        /// <seealso cref="StructuralRowSuppressionService"/>
        protected static bool isCombinedPopulationRowLabel(
            string? paramName,
            ReconstructedRow row,
            IEnumerable<ArmDefinition> arms)
        {
            #region implementation

            return StructuralRowSuppressionService.IsCombinedPopulationRowLabel(paramName, row, arms);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records an audit entry for a structural row or cell suppressed before observation emission.
        /// </summary>
        /// <param name="table">Source reconstructed table.</param>
        /// <param name="row">Source row.</param>
        /// <param name="cell">Source cell, or null for row-level suppression.</param>
        /// <param name="category">Parser category.</param>
        /// <param name="parameterName">Parameter label or row label.</param>
        /// <param name="treatmentArm">Treatment arm, when available.</param>
        /// <param name="rawValue">Raw value text suppressed.</param>
        /// <param name="structuralLabel">Structural label preserved as context.</param>
        /// <param name="contextTarget">Context field that received the label.</param>
        /// <param name="reason">Human-readable suppression reason.</param>
        /// <param name="validationFlag">Stable diagnostic validation flag.</param>
        /// <seealso cref="StructuralRowSuppressionService"/>
        protected void recordSuppressedStructuralRow(
            ReconstructedTable table,
            ReconstructedRow row,
            ProcessedCell? cell,
            TableCategory category,
            string? parameterName,
            string? treatmentArm,
            string? rawValue,
            string? structuralLabel,
            string? contextTarget,
            string reason,
            string validationFlag = "SUPPRESSED_STRUCTURAL_ROW")
        {
            #region implementation

            _suppressionDiagnostics.RecordSuppressedStructuralRow(
                table,
                row,
                cell,
                category,
                GetType().Name,
                parameterName,
                treatmentArm,
                rawValue,
                structuralLabel,
                contextTarget,
                reason,
                validationFlag);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records AE rows that still have no usable treatment arm after all parser rescue attempts.
        /// </summary>
        /// <param name="table">Source reconstructed table.</param>
        /// <param name="rows">Rows that could not be emitted safely.</param>
        /// <param name="reason">Stable suppression flag explaining the failure.</param>
        /// <seealso cref="StructuralRowSuppressionService"/>
        protected void recordUnrescuableAeRows(
            ReconstructedTable table,
            IEnumerable<ReconstructedRow> rows,
            string reason)
        {
            #region implementation

            _suppressionDiagnostics.RecordUnrescuableAeRows(table, rows, reason, GetType().Name);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Chooses the most specific AE suppression flag for an unresolved arm set.
        /// </summary>
        /// <param name="arms">Arm definitions after all rescue attempts.</param>
        /// <returns>Stable suppression flag for diagnostics.</returns>
        /// <seealso cref="StructuralRowSuppressionService"/>
        protected static string getUnrescuableAeReason(IEnumerable<ArmDefinition> arms)
        {
            #region implementation

            return StructuralRowSuppressionService.GetUnrescuableAeReason(arms);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a header column represents a stat/comparison column.
        /// </summary>
        /// <param name="headerText">The leaf header text to evaluate.</param>
        /// <returns>True if the column is a stat column.</returns>
        /// <seealso cref="ArmDefinitionExtractor"/>
        protected static bool isStatColumn(string? headerText)
        {
            #region implementation

            return ArmDefinitionExtractor.IsStatColumn(headerText);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a header column represents a timepoint.
        /// </summary>
        /// <param name="headerText">The leaf header text to evaluate.</param>
        /// <returns>True if the column is a timepoint column.</returns>
        /// <seealso cref="ArmDefinitionExtractor"/>
        protected static bool isTimepointColumn(string? headerText)
        {
            #region implementation

            return ArmDefinitionExtractor.IsTimepointColumn(headerText);

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

            // No row collection means the caller has nothing parser-safe to iterate;
            // return an empty list instead of forcing null checks in every parser.
            if (table.Rows == null)
                return new List<ReconstructedRow>();

            // Keep only body-like rows because parser loops should not accidentally
            // emit header/footer text as observations.
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

            // If either side is missing, no marker can be resolved and the caller
            // should keep FootnoteText null.
            if (markers == null || markers.Count == 0 || footnotes == null || footnotes.Count == 0)
                return null;

            // Preserve source marker order so joined footnote text mirrors the cell's
            // marker sequence.
            var resolved = new List<string>();
            foreach (var marker in markers)
            {
                // Trim marker text before lookup because reconstructed markers may
                // carry incidental whitespace from the SPL cell.
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

            // Build one observation shell with provenance and raw value intact; parser
            // methods will layer arm, parameter, and parsed-value fields onto it.
            return new ParsedObservation
            {
                // Provenance
                DocumentGUID = table.DocumentGUID,
                LabelerName = table.LabelerName,
                ProductTitle = table.Title != null ? TextUtil.RemoveTags(table.Title) : null,
                VersionNumber = table.VersionNumber,
                UNII = table.UNII,
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
            // Only AE/Efficacy tables receive this promotion because numeric cells in
            // PK or drug-interaction tables often carry true measurements.
            if (parsed.PrimaryValueType == "Numeric" &&
                (category == TableCategory.ADVERSE_EVENT || category == TableCategory.EFFICACY))
            {
                // Promote if arm format hint contains % or n(%)
                // The arm format is the strongest local evidence that this numeric
                // value belongs to an incidence/percentage display column.
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

        #region Protected Helpers - AE/Efficacy Value Context

        /**************************************************************/
        /// <summary>
        /// Parses an AE or Efficacy value with row, arm, and table context.
        /// </summary>
        /// <remarks>
        /// Raw value parsing intentionally remains context-neutral in
        /// <see cref="ValueParser"/>. This helper applies the domain-specific
        /// corrections that require row labels, arm sample sizes, and percent/stat
        /// hints, such as standalone less-than-one incidence cells and p-value rows.
        /// </remarks>
        /// <param name="rawText">Raw cell text.</param>
        /// <param name="category">Parser category.</param>
        /// <param name="rowLabel">Current row label or promoted parameter name.</param>
        /// <param name="parameterCategory">Current category/group context.</param>
        /// <param name="arm">Arm definition supplying sample size and format hints.</param>
        /// <param name="caption">Source table caption.</param>
        /// <param name="forcePValue">Whether the row is known to be a p-value row.</param>
        /// <returns>Context-adjusted parsed value.</returns>
        /// <seealso cref="ValueParser"/>
        /// <seealso cref="ParsedValue"/>
        protected static ParsedValue parseValueWithAeEfficacyContext(
            string? rawText,
            TableCategory category,
            string? rowLabel,
            string? parameterCategory,
            ArmDefinition? arm,
            string? caption,
            bool forcePValue = false)
        {
            #region implementation

            // Start with the context-neutral parse; every branch below only adjusts
            // semantics when AE/Efficacy evidence requires it.
            var parsed = ValueParser.Parse(rawText, arm?.SampleSize);

            // Other categories keep the raw parser result because these heuristics
            // are calibrated to incidence and efficacy tables.
            if (category != TableCategory.ADVERSE_EVENT && category != TableCategory.EFFICACY)
                return parsed;

            // P-value context can come from an explicit caller signal, the row label,
            // or a statistic-style arm header.
            var isPValueContext = forcePValue ||
                isPValueRowLabel(rowLabel) ||
                isPValueRowLabel(arm?.Name);

            // P-value rows should not be promoted to percentages or counts later in
            // this helper; exit immediately after coercion.
            if (isPValueContext)
                return coerceToPValue(parsed);

            // Standalone "<1" cells in percent/incidence contexts represent a
            // report-threshold value, not an ordinary numeric measurement.
            if (_standaloneLtOnePattern.IsMatch(rawText ?? string.Empty) &&
                (isPercentValueContext(category, rowLabel, parameterCategory, arm, caption) ||
                 category == TableCategory.ADVERSE_EVENT))
            {
                return coerceStandaloneLtOneToPercentage(parsed, arm?.SampleSize);
            }

            // Dash placeholders in AE/Efficacy percent contexts share the same
            // semantics as a "<1" cell — the value lies below the table's reporting
            // threshold. Coerce to a midpoint estimate so treatment/placebo pairing
            // is preserved and downstream comparison queries keep both arms.
            if (string.Equals(parsed.ParseRule, "dash_placeholder", StringComparison.Ordinal) &&
                (isPercentValueContext(category, rowLabel, parameterCategory, arm, caption) ||
                 category == TableCategory.ADVERSE_EVENT))
            {
                return coerceDashPlaceholderToPercentage(parsed, arm?.SampleSize);
            }

            // Count evidence wins over generic numeric parsing when row/header/caption
            // text explicitly names counts, totals, events, patients, or subjects.
            if (string.Equals(parsed.PrimaryValueType, "Numeric", StringComparison.OrdinalIgnoreCase) &&
                isCountValueContext(rowLabel, arm, caption))
            {
                parsed.PrimaryValueType = "Count";
                parsed.Unit = null;
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "COUNT_CONTEXT_PROMOTION");
            }

            // Percent evidence promotes remaining bare numeric values into percentages
            // so incidence tables preserve analyzable units.
            if (string.Equals(parsed.PrimaryValueType, "Numeric", StringComparison.OrdinalIgnoreCase) &&
                isPercentValueContext(category, rowLabel, parameterCategory, arm, caption))
            {
                parsed.PrimaryValueType = "Percentage";
                parsed.Unit = "%";
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "PCT_CONTEXT_PROMOTION");
            }

            // Percentages above 100 are invalid as percentages; demote them before
            // downstream filters assume percent semantics.
            if (string.Equals(parsed.PrimaryValueType, "Percentage", StringComparison.OrdinalIgnoreCase) &&
                parsed.PrimaryValue.HasValue &&
                parsed.PrimaryValue.Value > 100)
            {
                parsed = rejectPercentageOverOneHundred(parsed, rowLabel, arm, caption);
            }

            return parsed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when the row label describes a comparison/statistic row.
        /// </summary>
        /// <param name="rowLabel">Candidate row label.</param>
        /// <returns><c>true</c> for p-value, difference, CI, and ratio rows.</returns>
        protected static bool isComparisonRowLabel(string? rowLabel)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(rowLabel) &&
                   _comparisonRowLabelPattern.IsMatch(rowLabel.Trim());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when the supplied text is a p-value row label.
        /// </summary>
        /// <param name="text">Candidate label.</param>
        /// <returns><c>true</c> when the label is p-value.</returns>
        protected static bool isPValueRowLabel(string? text)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(text) &&
                   _pValueRowLabelPattern.IsMatch(text.Trim());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Removes duplicate ordinary-arm efficacy observations when the exact same source
        /// cell was also emitted as a comparison statistic row.
        /// </summary>
        /// <remarks>
        /// Intentionally no-op pending a separate duplicate-comparison design pass. The
        /// prior disabled implementation was removed so the parser does not carry dead
        /// suppression logic that could be mistaken for active behavior.
        /// </remarks>
        /// <param name="observations">Mutable observation list to deduplicate.</param>
        /// <seealso cref="ParsedObservation"/>
        protected static void suppressDuplicateComparisonEmissions(List<ParsedObservation> observations)
        {
            #region implementation

            return;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether row/header/table context supports percentage semantics.
        /// </summary>
        /// <param name="category">Parser category.</param>
        /// <param name="rowLabel">Current row label.</param>
        /// <param name="parameterCategory">Current category/group label.</param>
        /// <param name="arm">Arm definition.</param>
        /// <param name="caption">Source table caption.</param>
        /// <returns><c>true</c> when a bare number or less-than-one value is a percentage.</returns>
        private static bool isPercentValueContext(
            TableCategory category,
            string? rowLabel,
            string? parameterCategory,
            ArmDefinition? arm,
            string? caption)
        {
            #region implementation

            // P-value rows are statistic rows, not percentage contexts, even when
            // they contain symbols that could otherwise look numeric.
            if (isPValueRowLabel(rowLabel))
                return false;

            // Explicit count context wins over percentage hints so "Number (%)" or
            // similar labels can be handled by count-specific parsing first.
            if (isCountValueContext(rowLabel, arm, caption))
                return false;

            // Any percent hint in the local row, category, arm, or caption is enough
            // to promote a bare number to percentage semantics.
            if (containsPercentHint(rowLabel) ||
                containsPercentHint(parameterCategory) ||
                containsPercentHint(arm?.FormatHint) ||
                containsPercentHint(arm?.Name) ||
                containsPercentHint(caption))
            {
                return true;
            }

            // AE tables default to percentage context because incidence values are
            // commonly reported without repeating percent markers on every row.
            return category == TableCategory.ADVERSE_EVENT;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether row/header/table context indicates counts rather than percentages.
        /// </summary>
        /// <param name="rowLabel">Current row label.</param>
        /// <param name="arm">Arm definition.</param>
        /// <param name="caption">Source table caption.</param>
        /// <returns><c>true</c> when the context is explicitly count-oriented.</returns>
        private static bool isCountValueContext(string? rowLabel, ArmDefinition? arm, string? caption)
        {
            #region implementation

            // Treat row, header format, arm name, and caption as equivalent context
            // sources because SPL tables move count hints between all four positions.
            return isCountText(rowLabel) ||
                   isCountText(arm?.FormatHint) ||
                   isCountText(arm?.Name) ||
                   isCountText(caption);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks for percentage hints in free text.
        /// </summary>
        /// <param name="text">Candidate text.</param>
        /// <returns><c>true</c> when the text suggests percentage reporting.</returns>
        private static bool containsPercentHint(string? text)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(text) &&
                   _percentContextPattern.IsMatch(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks for count hints that are not themselves n-percent format hints.
        /// </summary>
        /// <param name="text">Candidate text.</param>
        /// <returns><c>true</c> when text indicates count semantics.</returns>
        private static bool isCountText(string? text)
        {
            #region implementation

            // Blank text carries no evidence for count semantics.
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Most percent contexts should not be reclassified as counts; n/N is the
            // exception because it explicitly means count over denominator.
            if (containsPercentHint(text) && !text.Contains("n/N", StringComparison.OrdinalIgnoreCase))
                return false;

            return _countContextPattern.IsMatch(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Reinterprets a numeric value as a p-value under p-value row context.
        /// </summary>
        /// <param name="parsed">Parsed value to adjust.</param>
        /// <returns>Adjusted p-value parse.</returns>
        private static ParsedValue coerceToPValue(ParsedValue parsed)
        {
            #region implementation

            // Only values with a parsed primary numeric component can become p-values.
            if (parsed.PrimaryValue.HasValue)
            {
                parsed.PrimaryValueType = "PValue";
                parsed.PValue = parsed.PrimaryValue;
                parsed.Unit = null;
                parsed.PValueQualifier ??= "=";
                // Preserve explicit p-value parse rules; otherwise append row context
                // so downstream review can see why the value was reinterpreted.
                if (!string.Equals(parsed.ParseRule, "pvalue", StringComparison.OrdinalIgnoreCase))
                    parsed.ParseRule = $"{parsed.ParseRule}+row_pvalue";
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "P_VALUE_ROW_CONTEXT");
            }

            return parsed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts a standalone <c>&lt;1</c> incidence cell to a percentage.
        /// </summary>
        /// <param name="parsed">Original parsed value.</param>
        /// <param name="armN">Optional arm denominator.</param>
        /// <returns>Percentage parse derived from arm size or fallback threshold.</returns>
        private static ParsedValue coerceStandaloneLtOneToPercentage(ParsedValue parsed, int? armN)
        {
            #region implementation

            // Positive ArmN lets the parser derive a table-specific threshold;
            // otherwise use the conservative fallback used by prior behavior.
            var hasArmN = armN.HasValue && armN.Value > 0;
            var resolvedArmN = armN.GetValueOrDefault();

            // Derive the midpoint-like percentage before changing type metadata so
            // PrimaryValue, Unit, and flags all describe the same interpretation.
            parsed.PrimaryValue = hasArmN
                ? Math.Round(1.0 / resolvedArmN * 100, 1)
                : 0.1;
            parsed.PrimaryValueType = "Percentage";
            parsed.PValue = null;
            parsed.PValueQualifier = null;
            parsed.Unit = "%";
            parsed.ParseRule = $"{parsed.ParseRule}+lt_one_percent_context";
            parsed.ValidationFlags = appendFlag(
                parsed.ValidationFlags,
                hasArmN
                    ? $"PCT_DERIVED_FROM_LT_ONE:ArmN={resolvedArmN}"
                    : "PCT_DERIVED_FROM_LT_ONE:fallback=0.1");
            return parsed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts a dash-placeholder cell (<c>-</c>, <c>--</c>, <c>—</c>, <c>–</c>)
        /// in an AE/Efficacy percent context to a sub-threshold midpoint estimate.
        /// </summary>
        /// <remarks>
        /// AE tables that follow the "report only at ≥1%" convention render
        /// sub-threshold incidences as a dash. Treating those cells as missing
        /// breaks treatment/placebo arm pairing. This helper mirrors
        /// <see cref="coerceStandaloneLtOneToPercentage"/>: it preserves the raw
        /// dash in <c>RawValue</c>, promotes the observation out of the excluded
        /// state, and emits a midpoint estimate (<c>1/ArmN * 100</c>, or <c>0.1</c>
        /// fallback) so downstream comparison queries can still pair the row.
        /// </remarks>
        /// <param name="parsed">Original parsed value tagged <c>dash_placeholder</c>.</param>
        /// <param name="armN">Optional arm denominator.</param>
        /// <returns>Percentage parse derived from arm size or fallback threshold.</returns>
        /// <seealso cref="coerceStandaloneLtOneToPercentage"/>
        private static ParsedValue coerceDashPlaceholderToPercentage(ParsedValue parsed, int? armN)
        {
            #region implementation

            // Mirror standalone "<1" handling: use ArmN when available, otherwise
            // preserve a deterministic sub-threshold fallback.
            var hasArmN = armN.HasValue && armN.Value > 0;
            var resolvedArmN = armN.GetValueOrDefault();

            // Convert the placeholder into an analyzable percentage so paired arms
            // stay together for downstream comparison.
            parsed.PrimaryValue = hasArmN
                ? Math.Round(1.0 / resolvedArmN * 100, 1)
                : 0.1;
            parsed.PrimaryValueType = "Percentage";
            parsed.IsExcluded = false;
            parsed.PValue = null;
            parsed.PValueQualifier = null;
            parsed.Unit = "%";
            parsed.ParseRule = $"{parsed.ParseRule}+lt_one_percent_context";
            parsed.ValidationFlags = appendFlag(
                parsed.ValidationFlags,
                hasArmN
                    ? $"PCT_DERIVED_FROM_DASH:ArmN={resolvedArmN}"
                    : "PCT_DERIVED_FROM_DASH:fallback=0.1");
            return parsed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Demotes percentages greater than 100 to count or numeric evidence.
        /// </summary>
        /// <param name="parsed">Parsed percentage candidate.</param>
        /// <param name="rowLabel">Current row label.</param>
        /// <param name="arm">Arm definition.</param>
        /// <param name="caption">Source table caption.</param>
        /// <returns>Demoted parsed value with an audit flag.</returns>
        private static ParsedValue rejectPercentageOverOneHundred(
            ParsedValue parsed,
            string? rowLabel,
            ArmDefinition? arm,
            string? caption)
        {
            #region implementation

            // Remember the invalid percentage so the demotion flag can preserve the
            // original rejected value for review.
            var rejectedPercentage = parsed.PrimaryValue;

            // For n(%) parses, the secondary value usually holds the count; swap it
            // back into PrimaryValue when the percentage component is impossible.
            if (string.Equals(parsed.ParseRule, "n_pct", StringComparison.OrdinalIgnoreCase) &&
                parsed.SecondaryValue.HasValue)
            {
                parsed.PrimaryValue = parsed.SecondaryValue;
                parsed.SecondaryValue = rejectedPercentage;
                parsed.SecondaryValueType = null;
            }

            parsed.PrimaryValueType = isCountValueContext(rowLabel, arm, caption) ? "Count" : "Numeric";
            parsed.Unit = null;
            parsed.ParseRule = $"{parsed.ParseRule}+pct_gt100_rejected";
            parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, $"PCT_GT100_REJECTED:{rejectedPercentage}");
            return parsed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects a row-label cell interspersed before the numeric arm cells.
        /// </summary>
        /// <param name="row">Source body row.</param>
        /// <param name="arms">Resolved arm definitions.</param>
        /// <param name="rowLabel">Current column-zero row label.</param>
        /// <param name="category">Parser category.</param>
        /// <param name="recoveredName">Recovered parameter name from the interspersed label cell.</param>
        /// <param name="categoryLabel">Optional category label preserved from column zero.</param>
        /// <param name="labelCellSequence">Source cell sequence to suppress.</param>
        /// <returns><c>true</c> when a text cell should become the parameter name.</returns>
        /// <seealso cref="ParsedObservation.ParameterName"/>
        /// <seealso cref="ParsedObservation.ParameterCategory"/>
        protected static bool tryRecoverInterspersedRowLabel(
            ReconstructedRow row,
            IReadOnlyList<ArmDefinition> arms,
            string? rowLabel,
            TableCategory category,
            out string? recoveredName,
            out string? categoryLabel,
            out int? labelCellSequence)
        {
            #region implementation

            // Initialize out parameters up front so every early return leaves callers
            // with a predictable "no recovery" state.
            recoveredName = null;
            categoryLabel = null;
            labelCellSequence = null;

            // This recovery only applies to AE/Efficacy layouts that sometimes place
            // a text row label inside the first arm column.
            if (category != TableCategory.ADVERSE_EVENT && category != TableCategory.EFFICACY)
                return false;

            // Work left-to-right by resolved column index so "first text arm cell"
            // means the visual first data column, not source list order.
            var orderedArms = arms
                .Where(a => a.ColumnIndex.HasValue)
                .OrderBy(a => a.ColumnIndex!.Value)
                .ToList();

            // Keep only arms that can actually emit observations; generic/stat columns
            // should not participate in the rescue decision.
            var usableArms = orderedArms
                .Where(hasUsableTreatmentArm)
                .ToList();

            // Need at least two columns and one usable arm to distinguish a misplaced
            // label cell from an ordinary single-arm text value.
            if (orderedArms.Count < 2 || usableArms.Count == 0)
                return false;

            // Find the first arm-position cell that is text-like, non-placeholder,
            // and different from the existing row label.
            var labelCandidate = orderedArms
                .FirstOrDefault(a =>
                {
                    // Pull the candidate cell from the arm column under review.
                    var candidateCell = getCellAtColumn(row, a.ColumnIndex!.Value);

                    // Blank candidate cells cannot supply a recoverable parameter name.
                    if (candidateCell == null || string.IsNullOrWhiteSpace(candidateCell.CleanedText))
                        return false;

                    // Parse the candidate to avoid treating numeric/stat placeholders
                    // as labels just because they contain punctuation or symbols.
                    var candidateText = candidateCell.CleanedText.Trim();
                    var candidateParsed = ValueParser.Parse(candidateText, a.SampleSize);
                    return string.Equals(candidateParsed.PrimaryValueType, "Text", StringComparison.OrdinalIgnoreCase) &&
                           Regex.IsMatch(candidateText, @"[A-Za-z]") &&
                           !StructuralRowSuppressionService.IsPlaceholderStatValue(candidateText) &&
                           !string.Equals(candidateText, rowLabel, StringComparison.OrdinalIgnoreCase);
                });

            // No qualifying text cell means the row follows normal parameter layout.
            if (labelCandidate == null)
                return false;

            // Re-read the winning cell so its sequence number and cleaned text can be
            // used by the caller for suppression diagnostics.
            var firstCell = getCellAtColumn(row, labelCandidate.ColumnIndex!.Value);

            // Defensive guard: the winning candidate should still exist, but null/blank
            // means the row cannot be safely rescued.
            if (firstCell == null || string.IsNullOrWhiteSpace(firstCell.CleanedText))
                return false;

            // Track the label text and column boundary so later arm columns can prove
            // the row still contains numeric observation data.
            var text = firstCell.CleanedText.Trim();
            var labelColumn = labelCandidate.ColumnIndex!.Value;

            // Require a later usable arm to carry numeric/p-value data; without this,
            // the "label" might just be a structural context row.
            var laterNumeric = usableArms
                .Where(arm => arm.ColumnIndex!.Value > labelColumn)
                .Any(arm =>
            {
                // Blank later cells do not prove the row is observable.
                var cell = getCellAtColumn(row, arm.ColumnIndex!.Value);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    return false;

                // Any later primary, secondary, or p-value evidence is enough to
                // confirm the recovered label belongs to an observable row.
                var later = ValueParser.Parse(cell.CleanedText, arm.SampleSize);
                return later.PrimaryValue.HasValue ||
                       later.SecondaryValue.HasValue ||
                       later.PValue.HasValue;
            });

            // Without later numeric/stat evidence, leave the row to structural
            // suppression rather than creating a partial observation.
            if (!laterNumeric)
                return false;

            // Clean footnote markers and punctuation from the recovered text before
            // assigning it to ParameterName.
            var (cleaned, _) = ValueParser.CleanParameterName(text);
            recoveredName = cleaned;
            labelCellSequence = firstCell.SequenceNumber;

            // Preserve a meaningful column-zero label as category context, but avoid
            // copying structural placeholders or AE header echoes into observations.
            if (!string.IsNullOrWhiteSpace(rowLabel) &&
                !StructuralRowSuppressionService.IsStructuralValueCell(rowLabel) &&
                !StructuralRowSuppressionService.IsAeHeaderEchoParameter(rowLabel))
            {
                categoryLabel = rowLabel.Trim();
            }

            return !string.IsNullOrWhiteSpace(recoveredName);

            #endregion
        }

        #endregion Protected Helpers - AE/Efficacy Value Context

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

            // Blank captions carry no statistical context; leave value types exactly
            // as ValueParser produced them.
            if (string.IsNullOrWhiteSpace(caption))
                return default;

            // First match wins because the pattern list is ordered from most specific
            // to most general.
            foreach (var (pattern, hint) in _captionHintPatterns)
            {
                // Return immediately when a descriptor is found so generic patterns
                // cannot override a more specific caption hint.
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

            // Empty hints are a no-op; this keeps callers free to apply caption hints
            // without branching.
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
                // Save both parsed values before swapping them so the reinterpretation
                // preserves the original numeric evidence under new semantic labels.
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
            // A bare numeric parse lacks stronger local evidence, so caption context
            // can safely provide the primary value type.
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
            // Fill only the missing secondary type; preserve existing secondary values
            // and stronger parser-derived classifications.
            if (hint.SecondaryValueType != null &&
                parsed.SecondaryValue != null &&
                string.IsNullOrEmpty(parsed.SecondaryValueType))
            {
                parsed.SecondaryValueType = hint.SecondaryValueType;
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags,
                    $"CAPTION_HINT:{hint.Source}");
            }

            // Case 4: BoundType refinement — caption specifies CI level (e.g., "90% CI" → "90CI")
            // Refine only generic CI bounds so explicit parser classifications are
            // not overwritten by caption text.
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
        protected static string appendFlag(string? existing, string flag)
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

        #region Protected Helpers — Caption Study Context

        /**************************************************************/
        /// <summary>
        /// Extracts a trial / study descriptor from an adverse-event table
        /// caption when the parser cannot recover <c>StudyContext</c> from
        /// header colspan paths. Conservative by design: returns <c>null</c>
        /// for any caption that does not match the canonical SPL AE caption
        /// grammar, so callers may invoke this indiscriminately without
        /// polluting non-AE output.
        /// </summary>
        /// <remarks>
        /// ## Canonical caption shape
        /// <code>
        /// Table N[:.]  &lt;measure phrase&gt;  &lt;connector&gt;  &lt;trial descriptor&gt;  [footnotes]
        /// </code>
        ///
        /// ## Extraction pipeline
        /// 1. Strip HTML tags (e.g., <c>&lt;sup&gt;*&lt;/sup&gt;</c>) and collapse whitespace.
        /// 2. Strip the leading <c>"Table N:"</c> / <c>"Table 9."</c> prefix.
        /// 3. Require an AE measure phrase (<c>"Adverse Reactions"</c>,
        ///    <c>"Treatment-Emergent Adverse Events"</c>, <c>"Incidence of …"</c>, etc.).
        ///    Bail to <c>null</c> if absent — this prevents non-AE captions
        ///    from producing StudyContext garbage.
        /// 4. Find the first connector (<c>in|during|from|reported in|…</c>)
        ///    that occurs *after* the measure phrase and keep everything
        ///    following it as the candidate descriptor.
        /// 5. Trim trailing footnote markers and punctuation.
        /// 6. Reject results that are too short to be meaningful (≤ 3 chars).
        /// </remarks>
        /// <example>
        /// <code>
        /// extractStudyContextFromCaption(
        ///     "Table 9: Incidence (%) of Treatment-Emergent Adverse Reactions " +
        ///     "in Placebo-Controlled, Add-On Epilepsy Trials in Pediatric " +
        ///     "Patients (Ages 2-16 Years)&lt;sup&gt;*&lt;/sup&gt;")
        /// // → "Placebo-Controlled, Add-On Epilepsy Trials in Pediatric Patients (Ages 2-16 Years)"
        ///
        /// extractStudyContextFromCaption("Table 2: Mean PK Parameters in Healthy Volunteers")
        /// // → null  (no AE measure phrase)
        /// </code>
        /// </example>
        /// <param name="caption">The raw table caption text, possibly containing HTML.</param>
        /// <returns>The extracted trial descriptor, or <c>null</c> when the
        /// caption does not match canonical AE caption grammar.</returns>
        /// <seealso cref="PopulationDetector"/>
        /// <seealso cref="detectPopulation"/>
        protected internal static string? extractStudyContextFromCaption(string? caption)
        {
            #region implementation

            return ArmDefinitionExtractor.ExtractStudyContextFromCaption(caption);

            #endregion
        }

        #endregion Protected Helpers — Caption Study Context

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

            // A null cell collection means no resolved grid spans are available for
            // lookup, so no cell can be proven to cover the requested column.
            if (row.Cells == null)
                return null;

            // Use resolved start/end spans instead of raw cell position so colspan
            // and reconstructed grid columns are honored.
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

            // Column zero is the canonical parameter-label location for parser loops
            // that call this helper.
            var cell = getCellAtColumn(row, 0);

            // Missing or blank label cells intentionally return nulls so callers can
            // decide whether context recovery or suppression should handle the row.
            if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                return (null, null);

            // Clean the label once here so downstream parsers share identical
            // footnote-marker stripping behavior.
            var (name, markers) = ValueParser.CleanParameterName(cell.CleanedText);
            return (name, markers);

            #endregion
        }

        #endregion Protected Helpers — Cell Lookup

        #region Protected Helpers - AE Arm Row Loop

        /**************************************************************/
        /// <summary>
        /// Parses the shared adverse-event arm-table row loop for single- and
        /// multi-level AE parsers.
        /// </summary>
        /// <remarks>
        /// This is the Phase C Template Method extraction: concrete parsers still own
        /// arm discovery and parser selection, while this method owns the stable
        /// body-row algorithm for subpopulation context, structural suppression,
        /// interspersed label recovery, per-arm observation creation, and AE value
        /// parsing.
        /// </remarks>
        /// <param name="table">Source reconstructed table.</param>
        /// <param name="arms">Resolved treatment-arm definitions.</param>
        /// <param name="captionStudyContext">Caption-derived study context fallback, or null.</param>
        /// <param name="socDividersSetCategory">Whether SOC divider rows should update <c>ParameterCategory</c>.</param>
        /// <param name="emptyDataRowsSetCategory">Whether blank data rows should become AE category context.</param>
        /// <param name="structuralRowReason">Suppression reason for structural row-context captures.</param>
        /// <param name="structuralCellReason">Suppression reason for structural value-cell captures.</param>
        /// <param name="captionHint">Optional caption-derived value hint to preserve simple-arm parsing behavior.</param>
        /// <param name="rowPValueResolver">Optional row-level P-value resolver for parser-specific statistic columns.</param>
        /// <param name="comparisonRowsEmitter">Optional parser-specific emitter for statistic/comparison columns.</param>
        /// <param name="emitGenericArmObservations">Whether generic arm candidates should flow to standardization cleanup.</param>
        /// <returns>Parsed adverse-event observations.</returns>
        /// <seealso cref="ParsedObservation"/>
        /// <seealso cref="ArmDefinition"/>
        protected List<ParsedObservation> parseAdverseEventArmRows(
            ReconstructedTable table,
            List<ArmDefinition> arms,
            string? captionStudyContext,
            bool socDividersSetCategory,
            bool emptyDataRowsSetCategory,
            string structuralRowReason,
            string structuralCellReason,
            CaptionValueHint captionHint = default,
            Func<ReconstructedRow, double?>? rowPValueResolver = null,
            Action<ReconstructedTable, ReconstructedRow, string, string?, string?, double?, List<ParsedObservation>>? comparisonRowsEmitter = null,
            bool emitGenericArmObservations = false)
        {
            #region implementation

            var observations = new List<ParsedObservation>();
            var (population, _) = detectPopulation(table);

            string? currentCategory = null;
            string? currentSubpopulation = null;
            IDictionary<int, int> subpopArmNOverrides = new Dictionary<int, int>();
            IDictionary<int, int> sectionArmNOverrides = new Dictionary<int, int>();
            IDictionary<int, int> tableLevelArmN = new Dictionary<int, int>();
            var hasEmittedAeObservation = false;
            var followsAeResetBoundary = false;
            var dataRows = getDataBodyRows(table);

            // Body rows may carry header-continuation metadata (dose, N=, format
            // hints). Enrich arms before the observable row loop starts.
            var skipRows = enrichArmsFromBodyRows(dataRows, arms);
            if (skipRows > 0)
            {
                dataRows = dataRows.Skip(skipRows).ToList();
            }

            applySingleProductArmFallback(table, arms);
            var columnConsensusArmN = inferAeColumnConsensusArmN(dataRows, arms);
            if (!arms.Any(hasUsableTreatmentArm))
            {
                recordUnrescuableAeRows(
                    table,
                    dataRows,
                    getUnrescuableAeReason(arms));
                return observations;
            }

            foreach (var row in dataRows)
            {
                // Some AE parsers consume explicit SOC divider rows as category
                // context; the simple-arm fallback only resets subpopulation state.
                if (row.Classification == RowClassification.SocDivider)
                {
                    if (socDividersSetCategory)
                        currentCategory = row.SocName;

                    currentSubpopulation = null;
                    subpopArmNOverrides = new Dictionary<int, int>();
                    sectionArmNOverrides = new Dictionary<int, int>();
                    followsAeResetBoundary = true;
                    continue;
                }

                var (paramName, _) = getParameterName(row);
                if (string.IsNullOrWhiteSpace(paramName))
                    continue;

                if (tryConsumeAeDenominatorRow(
                    table,
                    row,
                    arms,
                    paramName,
                    hasEmittedAeObservation,
                    followsAeResetBoundary,
                    tableLevelArmN,
                    sectionArmNOverrides,
                    out var denominatorScope,
                    out var subpopName,
                    out var nOverrides))
                {
                    if (denominatorScope == AeDenominatorRowScope.Subpopulation)
                    {
                        currentSubpopulation = subpopName;
                        subpopArmNOverrides = nOverrides ?? new Dictionary<int, int>();
                    }
                    else if (denominatorScope is AeDenominatorRowScope.TableLevel or AeDenominatorRowScope.SectionLevel)
                    {
                        currentSubpopulation = null;
                        subpopArmNOverrides = new Dictionary<int, int>();
                    }

                    followsAeResetBoundary = false;
                    continue;
                }

                if (isCombinedPopulationRowLabel(paramName, row, arms))
                {
                    currentSubpopulation = null;
                    subpopArmNOverrides = new Dictionary<int, int>();
                    sectionArmNOverrides = new Dictionary<int, int>();
                    followsAeResetBoundary = true;
                    recordSuppressedStructuralRow(
                        table, row, null, TableCategory.ADVERSE_EVENT,
                        paramName, null, paramName, paramName, "Subpopulation",
                        "Combined/all-patients row suppressed and subpopulation context reset");
                    continue;
                }

                if (isStructuralContextRow(row, arms, paramName, TableCategory.ADVERSE_EVENT))
                {
                    currentCategory = paramName;
                    currentSubpopulation = null;
                    subpopArmNOverrides = new Dictionary<int, int>();
                    sectionArmNOverrides = new Dictionary<int, int>();
                    followsAeResetBoundary = true;
                    recordSuppressedStructuralRow(
                        table, row, null, TableCategory.ADVERSE_EVENT,
                        paramName, null, paramName, paramName, "ParameterCategory",
                        structuralRowReason);
                    continue;
                }

                if (emptyDataRowsSetCategory && !rowHasArmData(row, arms))
                {
                    currentCategory = paramName;
                    currentSubpopulation = null;
                    subpopArmNOverrides = new Dictionary<int, int>();
                    sectionArmNOverrides = new Dictionary<int, int>();
                    followsAeResetBoundary = true;
                    continue;
                }

                var effectiveParamName = paramName;
                var interspersedLabelCellSequence = (int?)null;
                if (tryRecoverInterspersedRowLabel(
                    row, arms, paramName, TableCategory.ADVERSE_EVENT,
                    out var recoveredParamName,
                    out var recoveredCategory,
                    out var recoveredLabelCellSequence))
                {
                    effectiveParamName = recoveredParamName ?? paramName;
                    interspersedLabelCellSequence = recoveredLabelCellSequence;
                    if (!string.IsNullOrWhiteSpace(recoveredCategory))
                        currentCategory = recoveredCategory;
                }

                var capturedCategory = currentCategory;
                var capturedSubpopulation = currentSubpopulation;
                var capturedSubpopArmNOverrides = subpopArmNOverrides;
                var capturedSectionArmNOverrides = sectionArmNOverrides;
                var capturedTableLevelArmN = tableLevelArmN;
                var beforeRowObservationCount = observations.Count;

                parseRowSafe(table, row, observations, (r, obs) =>
                {
                    var rowPValue = rowPValueResolver?.Invoke(r);

                    foreach (var arm in arms)
                    {
                        var canEmitArm = hasUsableTreatmentArm(arm) ||
                            shouldAllowLegacyGenericArmEmission(arm, emitGenericArmObservations);
                        if (!canEmitArm)
                            continue;

                        var cell = getCellAtColumn(r, arm.ColumnIndex ?? 0);
                        if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                            continue;
                        if (interspersedLabelCellSequence.HasValue &&
                            cell.SequenceNumber == interspersedLabelCellSequence.Value)
                            continue;

                        var o = createBaseObservation(table, r, cell, TableCategory.ADVERSE_EVENT);
                        o.ParameterName = effectiveParamName;
                        o.ParameterCategory = capturedCategory;
                        o.ParameterSubtype = arm.ParameterSubtype;
                        o.TreatmentArm = arm.Name;
                        o.StudyContext = arm.StudyContext ?? captionStudyContext;
                        o.DoseRegimen = arm.DoseRegimen;
                        o.Dose = arm.Dose;
                        o.DoseUnit = arm.DoseUnit;
                        o.Population = population;
                        o.Subpopulation = capturedSubpopulation;
                        o.PValue = rowPValue;

                        var scopedArmN = getScopedAeArmN(
                            arm.ColumnIndex,
                            capturedSubpopArmNOverrides,
                            capturedSectionArmNOverrides,
                            capturedTableLevelArmN);
                        var parseArm = ArmNResolver.BuildValueContextArm(arm, scopedArmN);
                        var parsed = parseValueWithAeEfficacyContext(
                            cell.CleanedText,
                            TableCategory.ADVERSE_EVENT,
                            effectiveParamName,
                            capturedCategory,
                            parseArm,
                            table.Caption);

                        if (!captionHint.IsEmpty)
                        {
                            parsed = applyCaptionHint(parsed, captionHint);
                        }

                        applyAeColumnConsensusArmN(parsed, arm, scopedArmN, columnConsensusArmN);
                        applyParsedValue(o, parsed);
                        applyResolvedAeArmN(o, arm, parsed, scopedArmN);
                        if (shouldSuppressAeStructuralObservation(o, parsed))
                        {
                            if (emitGenericArmObservations &&
                                shouldLetGenericArmFlowToStandardization(arm, parsed))
                            {
                                obs.Add(o);
                                continue;
                            }

                            var suppressionParameterName = emitGenericArmObservations
                                ? paramName
                                : effectiveParamName;
                            recordSuppressedStructuralRow(
                                table, r, cell, TableCategory.ADVERSE_EVENT,
                                suppressionParameterName, arm.Name, cell.CleanedText, suppressionParameterName,
                                "ParameterCategory",
                                structuralCellReason);
                            continue;
                        }

                        obs.Add(o);
                    }

                    comparisonRowsEmitter?.Invoke(
                        table,
                        r,
                        effectiveParamName,
                        null,
                        population,
                        rowPValue,
                        obs);
                });

                if (observations.Count > beforeRowObservationCount)
                {
                    hasEmittedAeObservation = true;
                    followsAeResetBoundary = false;
                }
            }

            return observations;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether an AE body row contains data in any usable arm column.
        /// </summary>
        /// <param name="row">Body row to inspect.</param>
        /// <param name="arms">Resolved arm definitions.</param>
        /// <returns><c>true</c> when at least one usable arm cell has text.</returns>
        /// <seealso cref="parseAdverseEventArmRows"/>
        private static bool rowHasArmData(ReconstructedRow row, IEnumerable<ArmDefinition> arms)
        {
            #region implementation

            return arms.Where(hasUsableTreatmentArm).Any(arm =>
            {
                var cell = getCellAtColumn(row, arm.ColumnIndex ?? 0);
                return cell != null && !string.IsNullOrWhiteSpace(cell.CleanedText);
            });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a generic SimpleArm AE column should reach column standardization.
        /// </summary>
        /// <remarks>
        /// The legacy SimpleArm path emitted numeric value-axis columns and allowed
        /// column standardization to null/flag the generic arm. Caption and body-system
        /// leakage remains parser-suppressed.
        /// </remarks>
        /// <param name="arm">Candidate arm definition.</param>
        /// <param name="parsed">Parsed value evidence from the candidate cell.</param>
        /// <returns>True when the generic arm should be emitted for downstream cleanup.</returns>
        /// <seealso cref="parseAdverseEventArmRows"/>
        private static bool shouldLetGenericArmFlowToStandardization(ArmDefinition arm, ParsedValue parsed)
        {
            #region implementation

            if (!shouldAllowLegacyGenericArmEmission(arm, emitGenericArmObservations: true))
            {
                return false;
            }

            return parsed.PrimaryValue.HasValue ||
                   parsed.SecondaryValue.HasValue ||
                   parsed.PValue.HasValue;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether SimpleArm compatibility should emit a generic arm column.
        /// </summary>
        /// <remarks>
        /// The legacy SimpleArm AE path allowed value-axis placeholders such as
        /// <c>%</c>, <c>n</c>, and <c>Percentage</c> to reach column standardization,
        /// but it did not emit context-axis headers such as <c>Overall</c>.
        /// </remarks>
        /// <param name="arm">Candidate arm definition.</param>
        /// <param name="emitGenericArmObservations">Whether the caller requested legacy generic emission.</param>
        /// <returns>True when the arm should flow to the parser row loop.</returns>
        /// <seealso cref="parseAdverseEventArmRows"/>
        private static bool shouldAllowLegacyGenericArmEmission(
            ArmDefinition arm,
            bool emitGenericArmObservations)
        {
            #region implementation

            if (!emitGenericArmObservations || !arm.ColumnIndex.HasValue)
                return false;

            return string.IsNullOrWhiteSpace(arm.Name) ||
                   AeColumnContextResolver.IsValueAxisToken(arm.Name);

            #endregion
        }

        #endregion Protected Helpers - AE Arm Row Loop

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

            // Copy every parsed numeric/statistical component onto the observation
            // before applying sample-size and validation-flag side effects.
            obs.PrimaryValue = parsed.PrimaryValue;
            obs.PrimaryValueType = parsed.PrimaryValueType;
            obs.SecondaryValue = parsed.SecondaryValue;
            obs.SecondaryValueType = parsed.SecondaryValueType;
            obs.LowerBound = parsed.LowerBound;
            obs.UpperBound = parsed.UpperBound;
            obs.BoundType = parsed.BoundType;
            obs.PValue = parsed.PValue;

            // Cell-embedded sample size (e.g., "(n=129)") → ArmN when not already set from header
            // Header-derived ArmN wins; this branch only fills missing denominators
            // from the cell itself.
            if (parsed.SampleSize.HasValue && !obs.ArmN.HasValue)
                obs.ArmN = parsed.SampleSize;

            obs.Unit = parsed.Unit ?? obs.Unit;
            obs.ParseConfidence = parsed.ParseConfidence;
            obs.ParseRule = parsed.ParseRule;

            // Append validation flags from value parsing
            // Preserve existing parser flags and append value-parser flags so review
            // diagnostics retain both sources of evidence.
            if (!string.IsNullOrEmpty(parsed.ValidationFlags))
            {
                obs.ValidationFlags = string.IsNullOrEmpty(obs.ValidationFlags)
                    ? parsed.ValidationFlags
                    : $"{obs.ValidationFlags}; {parsed.ValidationFlags}";
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies centralized AE ArmN resolution and appends source diagnostics.
        /// </summary>
        /// <param name="obs">Observation receiving the resolved denominator.</param>
        /// <param name="arm">Resolved treatment-arm context.</param>
        /// <param name="parsed">Parsed value evidence from the source cell.</param>
        /// <param name="scopedMetadataN">Optional body/header-tier denominator override.</param>
        /// <seealso cref="ArmNResolver"/>
        /// <seealso cref="ParsedObservation.ArmN"/>
        protected static void applyResolvedAeArmN(
            ParsedObservation obs,
            ArmDefinition arm,
            ParsedValue parsed,
            int? scopedMetadataN)
        {
            #region implementation

            var resolution = ArmNResolver.ResolveForAeObservation(
                arm,
                parsed,
                scopedMetadataN,
                obs.ArmN);

            if (resolution.ArmN.HasValue)
                obs.ArmN = resolution.ArmN.Value;

            if (!string.IsNullOrWhiteSpace(resolution.ValidationFlag))
                obs.ValidationFlags = appendFlag(obs.ValidationFlags, resolution.ValidationFlag);

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

            // Capture the observation count before row parsing so a row-level fault
            // can remove only the partial emissions from that row.
            var preCount = observations.Count;

            try
            {
                // Let the concrete parser own the row-specific extraction logic while
                // this wrapper owns rollback and exception normalization.
                rowParser(row, observations);
            }
            catch (Exception ex)
            {
                // Roll back any observations added during this row
                // Expected outcome: a faulty row never leaves partial observations in
                // the table-level list.
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

        #region Protected Helpers — Body Row Enrichment

        /**************************************************************/
        /// <summary>
        /// Scans the first few body rows for header-continuation metadata and enriches arm definitions accordingly.
        /// </summary>
        /// <param name="dataRows">The filtered data body rows.</param>
        /// <param name="arms">Arm definitions to enrich (modified in place).</param>
        /// <returns>The number of leading enrichment rows to skip.</returns>
        /// <seealso cref="ArmMetadataEnrichmentService"/>
        protected static int enrichArmsFromBodyRows(List<ReconstructedRow> dataRows, List<ArmDefinition> arms)
        {
            #region implementation

            return ArmMetadataEnrichmentService.EnrichArmsFromBodyRows(dataRows, arms);

            #endregion
        }

        #endregion Protected Helpers — Body Row Enrichment
    }
}
