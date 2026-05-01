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
    public abstract class BaseTableParser : ITableParser, ITableParserDiagnostics
    {
        #region Diagnostics

        /**************************************************************/
        /// <summary>
        /// Suppressed structural rows captured during the current parse.
        /// </summary>
        private readonly List<TableSuppressionAuditRecord> _suppressedRows = new();

        /**************************************************************/
        /// <inheritdoc/>
        public IReadOnlyList<TableSuppressionAuditRecord> SuppressedRows => _suppressedRows;

        /**************************************************************/
        /// <inheritdoc/>
        public void ClearDiagnostics()
        {
            #region implementation

            _suppressedRows.Clear();

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

        // Pattern for detecting stat/comparison column headers
        private static readonly Regex _statColumnPattern = new(
            @"(?:P[\s-]*[Vv]alue|Difference|Risk\s+Reduction|\b(?:ARR|RR|HR|OR)\b|95\s*%?\s*CI|Relative\s+Risk)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern for detecting timepoint column headers
        private static readonly Regex _timepointPattern = new(
            @"(?:(?:Week|Month|Year|Day)\s*\d+|\d+\s*(?:Weeks?|Months?|Years?|Days?))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern for trailing format hint in arm headers without N= (e.g., "Paroxetine %", "Drug n(%)")
        protected static readonly Regex _trailingFormatHintPattern = new(
            @"^(.+?)\s+(n\s*\(\s*%\s*\)|%)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Phase 3 — Generic structural axis labels that are NOT real treatment-arm names.
        // When a leaf header equals one of these (anchored, exact match, case-insensitive),
        // arm derivation walks up the HeaderPath looking for a non-generic ancestor; if none
        // exists, the column is skipped rather than emitting an arm-less observation.
        // Conservative scope: only labels that are unambiguously generic / structural.
        // Drug names, study names, ambiguous tokens like "Placebo" / "Total" are NOT
        // included so they continue to be accepted as arm names.
        private static readonly Regex _genericArmLabelPattern = new(
            @"^\s*(?:" +
              @"Events?|" +
              @"Body\s+System(?:\s*[\(/\-].*)?|" +
              @"System\s+Organ\s+Class|SOC|" +
              @"MedDRA(?:\s+(?:Term|Preferred\s+Term))?|" +
              @"Adverse\s+(?:Reactions?|Events?|Experiences?)|" +
              @"Reactions?|" +
              @"Outcomes?|" +
              @"Variables?|" +
              @"Parameters?|" +
              @"Preferred\s+Term|Term|" +
              @"Percent|Percentage|%|" +
              @"n\s*\(\s*%\s*\)|" +
              @"[nN]|" +
              @"Number|Count|Frequency" +
            @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Phase 3b - AE/Efficacy column-axis labels that describe the value
        // dimension, not the treatment arm. These are preserved as
        // ArmDefinition.ParameterSubtype when possible.
        private static readonly Regex _armAxisLabelPattern = new(
            @"^\s*(?:" +
              @"Any(?:\s+(?:Grade|Grades?))?(?:\s+Adverse\s+(?:Reactions?|Events?))?|" +
              @"All\s+(?:CTC\s+)?Grades?|" +
              @"(?:CTC|CTCAE|NCI)\s+Grades?.*|" +
              @"(?:>=|>|\u2265)\s*Grade\s*\d+(?:\s+.*)?|" +
              @"(?:Toxicity|Severity)\s+Grades?\s*(?:(?:>=|>|\u2265)\s*)?(?:\d+|[IVX]+)?(?:\s+.*)?|" +
              @"Grades?\s*(?:(?:>=|>|\u2265)\s*)?(?:\d+|[IVX]+)(?:\s*(?:[-/\u2013,&]|and|or|to)\s*(?:\d+|[IVX]+)|\s+or\s+(?:Higher|Greater))*" +
                  @"(?:\s+Adverse\s+(?:Reactions?|Events?))?|" +
              @"(?:Number|No\.?|Percent(?:age)?)\s*(?:\(\s*%\s*\))?\s*(?:of\s+)?(?:Patients|Subjects|Participants|Reporting)?(?:\s+.*)?|" +
              @"%\s+of\s+(?:Patients|Subjects|Participants)(?:\s+.*)?|" +
              @"Incidence\s+of\s+adverse\s+(?:reactions?|events?)" +
            @")\s*(?:\(\s*%\s*\)|%)?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // AE column labels that describe a study/population/context axis rather
        // than a true treatment arm. These may be retained as StudyContext when
        // a product arm can be recovered from title evidence.
        private static readonly Regex _contextAxisLabelPattern = new(
            @"^\s*(?:" +
              @"\d+\s*[-\u2013]\s*day\s+Treatment|" +
              @"(?:Kidney|Heart|Liver)\s+Stud(?:y|ies)|" +
              @"(?:MDD|OCD|GAD)(?:\s*/\s*(?:MDD|OCD|GAD))*|" +
              @"Age\s+Group|" +
              @"Study\s+\d+[A-Z]?|" +
              @"Treatment\s+Regimen|" +
              @"Overall|" +
              @"All\s+Adverse\s+Reactions?|" +
              @"Grade\s+\d+(?:\s*/\s*\d+)?\s+Adverse\s+Reactions?|" +
              @"(?:HDD|NDD|PDD)\s*-\s*CKD|" +
              @"OXC\s+\d+(?:\s*/\s*\d+)*" +
            @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // AE non-observation cells that were previously emitted as null/text rows.
        private static readonly Regex _structuralAeValueCellPattern = new(
            @"^\s*(?:-{1,}|[–—]+|\(?\s*[Nn]\s*=\s*\d[\d,]*\s*\)?)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex _aeHeaderEchoParameterPattern = new(
            @"^\s*Adverse\s+(?:Drug\s+)?(?:Reaction|Reactions|Event|Events)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _aeNonObservationMetricPattern = new(
            @"^\s*Mean\s+Duration\s+of\s+Therapy(?:\s*\(.*\))?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _knownStructuralRowLabelPattern = new(
            @"^\s*(?:" +
              @"Patients\s+with\s+any\s+adverse\s+reaction|" +
              @"Respiratory,\s*thoracic,\s*and\s*mediastinal\s+disorders|" +
              @"CHD\s+events|" +
              @"System\s+Organ\s+Class|SOC|Body\s+System(?:\s*[\(/\-].*)?|" +
              @"(?:Cardiac|Congenital|Ear|Endocrine|Eye|Gastrointestinal|General|Hepatobiliary|Immune\s+system|Infections\s+and\s+infestations|Injury,\s*poisoning\s+and\s+procedural\s+complications|Investigations|Metabolism\s+and\s+nutrition|Musculoskeletal\s+and\s+connective\s+tissue|Neoplasms\s+benign,\s*malignant\s+and\s+unspecified|Nervous\s+system|Pregnancy,\s*puerperium\s+and\s+perinatal|Psychiatric|Renal\s+and\s+urinary|Reproductive\s+system\s+and\s+breast|Respiratory,\s*thoracic\s+and\s+mediastinal|Skin\s+and\s+subcutaneous\s+tissue|Social\s+circumstances|Surgical\s+and\s+medical\s+procedures|Vascular)\s+disorders" +
            @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _eventPseudoArmPattern = new(
            @"^\s*Events?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _placeholderStatValuePattern = new(
            @"^\s*(?:[-–—]+|↔|→|←|⟷|N/?A|No\.\s*Analyzed|No\.\s*Erad\.\s*\(%\))\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _standaloneLtOnePattern = new(
            @"^\s*[<≤]\s*1\s*%?\s*$",
            RegexOptions.Compiled);

        private static readonly Regex _pValueRowLabelPattern = new(
            @"\bp\s*[- ]?\s*value\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _comparisonRowLabelPattern = new(
            @"^\s*(?:.*\bp\s*[- ]?\s*value\b.*|Difference\s+from\s+placebo.*|95\s*%\s*CI|Risk\s+Difference.*|Hazard\s+Ratio.*|Relative\s+Risk.*|Odds\s+Ratio.*)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _percentContextPattern = new(
            @"(?:%|percent|percentage|responders?|response\s+rate|incidence|rate)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _countContextPattern = new(
            @"(?:^|\b)(?:n/N|Number|No\.?|Count|Events?|Patients?|Subjects?|Total)(?:\b|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern for dose regimen cells: "10 mg", "20 mg oral", "50 mcg once daily"
        private static readonly Regex _doseRegimenPattern = new(
            @"^\d+\s*(?:mg|mcg|µg|g|ml|mL)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern for n= declaration cells: "n = 102", "N=51", "N=5,310",
        // "(N=101)", "(N =101 )" — tolerates optional wrapping parentheses
        // and interior whitespace to match SPL tables that put the arm N
        // in the first body row as a parenthesized cell (e.g., Table 9 in
        // the Topiramate pediatric epilepsy label).
        private static readonly Regex _nEqualsCellPattern = new(
            @"^\(?\s*[Nn]\s*=\s*(\d[\d,]*)\s*\)?\s*$",
            RegexOptions.Compiled);

        // Pattern for format hint cells: "%" or "n(%)" or "n (%)"
        private static readonly Regex _formatHintCellPattern = new(
            @"^(?:n\s*\(\s*%\s*\)|%)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern matching the "Table N:" / "Table 9." / "Table 2 -" prefix
        // at the start of a table caption. Used by
        // <see cref="extractStudyContextFromCaption"/>.
        private static readonly Regex _tableNumberPrefixPattern = new(
            @"^\s*Table\s+[\w\d\-]+\s*[:.\-]\s*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern matching canonical AE caption "measure phrase" language
        // that signals an adverse-event table (as opposed to PK, efficacy,
        // lab, etc.). If a caption does not contain any of these phrases,
        // <see cref="extractStudyContextFromCaption"/> returns null rather
        // than guessing.
        private static readonly Regex _aeCaptionMeasurePhrasePattern = new(
            @"\b(?:(?:Treatment[-\s]*Emergent|Common|Serious|Most\s+Frequent|Drug[-\s]*Related)\s+)*" +
            @"Adverse\s+(?:Reactions?|Events?|Experiences?)" +
            @"|\bIncidence\s+(?:\(\s*%\s*\)\s+)?of\s+[\w\s\-,]*Adverse" +
            @"|\bFrequency\s+of\s+[\w\s\-,]*Adverse" +
            @"|\bPercent\s+of\s+Patients\s+(?:Reporting|With)\s+[\w\s\-,]*Adverse",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern matching the connector word that introduces the trial
        // descriptor AFTER the AE measure phrase. Used to split the
        // caption into "measure phrase" + "trial descriptor".
        private static readonly Regex _aeCaptionConnectorPattern = new(
            @"\b(?:reported\s+in|observed\s+in|occurring\s+in|seen\s+in|during|from|among|in)\s+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern for stripping embedded HTML tags from caption text
        // before trial-descriptor extraction. SPL captions frequently
        // carry trailing <sup>*</sup> / <sup>†</sup> footnote markers.
        private static readonly Regex _htmlTagPattern = new(
            @"<[^>]+>",
            RegexOptions.Compiled);

        // Pattern for stripping trailing footnote markers (bare *, †, ‡,
        // §, ¶ or parenthesized forms) from a trial descriptor.
        private static readonly Regex _trailingFootnoteMarkerPattern = new(
            @"(?:\s*\([\*†‡§¶]\)|\s*[\*†‡§¶]+)+\s*$",
            RegexOptions.Compiled);

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
        /// When a leaf header is blank or equals a generic structural label
        /// (<c>Event</c>, <c>Body System</c>, <c>Percent</c>, etc.), Phase 3 recovery
        /// walks up the <see cref="HeaderColumn.HeaderPath"/> looking for a real arm name;
        /// columns that cannot be recovered are skipped rather than emitting arm-less rows.
        /// </summary>
        /// <param name="table">Table with resolved header.</param>
        /// <returns>List of arm definitions with column positions. Empty if no header.</returns>
        /// <seealso cref="ArmDefinition"/>
        /// <seealso cref="ValueParser.ParseArmHeader"/>
        /// <seealso cref="recoverArmHeaderText"/>
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

                // Phase 3: recover the best arm-name candidate from this column's header
                // path, rejecting blank/generic leaves and walking up to a non-generic
                // ancestor. Skip the column entirely when no recovery is possible — the
                // alternative is emitting an observation that fails MissingRequired:TreatmentArm.
                var leafText = recoverArmHeaderText(col);
                leafText ??= recoverArmHeaderTextFromSiblingSpan(table.Header.Columns, i);
                if (leafText == null)
                {
                    arms.Add(createPlaceholderArmDefinition(col));
                    continue;
                }

                var arm = ValueParser.ParseArmHeader(leafText);
                if (arm != null)
                {
                    arm.ColumnIndex = col.ColumnIndex;

                    // Assign study context from parent header path if multi-level
                    if (col.HeaderPath != null && col.HeaderPath.Count > 1)
                    {
                        var contextCandidate = col.HeaderPath[0]?.Trim();
                        if (!string.Equals(contextCandidate, leafText, StringComparison.OrdinalIgnoreCase))
                            arm.StudyContext = contextCandidate;
                    }

                    applyAxisMetadata(col.LeafHeaderText, arm);
                    arms.Add(arm);
                }
                else
                {
                    // No N= found — check for trailing format hint (e.g., "Paroxetine %")
                    var trimmed = leafText.Trim();
                    var hintMatch = _trailingFormatHintPattern.Match(trimmed);
                    var armName = hintMatch.Success ? hintMatch.Groups[1].Value.Trim() : trimmed;
                    var formatHint = hintMatch.Success ? hintMatch.Groups[2].Value.Trim() : (string?)null;

                    var fallbackArm = new ArmDefinition
                    {
                        Name = armName,
                        FormatHint = formatHint,
                        ColumnIndex = col.ColumnIndex,
                        StudyContext = col.HeaderPath != null &&
                                       col.HeaderPath.Count > 1 &&
                                       !string.Equals(col.HeaderPath[0]?.Trim(), armName, StringComparison.OrdinalIgnoreCase)
                            ? col.HeaderPath[0]?.Trim()
                            : null
                    };
                    applyAxisMetadata(col.LeafHeaderText, fallbackArm);
                    arms.Add(fallbackArm);
                }
            }

            return arms;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3 helper: returns the best arm-name candidate text for a header column,
        /// or <c>null</c> when no real arm name can be recovered. Prefers the leaf header;
        /// when the leaf is blank or matches <see cref="_genericArmLabelPattern"/>
        /// (<c>Event</c>, <c>Body System (...)</c>, <c>Percent</c>, etc.), walks up the
        /// <see cref="HeaderColumn.HeaderPath"/> looking for a non-generic ancestor.
        /// </summary>
        /// <remarks>
        /// Treatment arm derivation surface: this method is called by both
        /// <see cref="extractArmDefinitions"/> (used by SimpleArm / AeWithSoc /
        /// EfficacyMultilevel parsers) and the multilevel AE parser's private extractor,
        /// so the rejection rule is consistent across all four AE/Efficacy parsers.
        ///
        /// ## Why ascend rather than emit
        /// Real-world failure shape (TextTableID 40880 family): the leaf header is a
        /// structural label like <c>Body System (Event)</c>, while the actual arm name
        /// is the spanning parent header (e.g., <c>Drug A (N=200)</c>). Walking up the
        /// HeaderPath recovers the arm; otherwise the parser emits observations with
        /// <c>TreatmentArm</c> set to <c>"Body System (Event)"</c> which is functionally
        /// equivalent to a missing arm and fires <c>MissingRequired:TreatmentArm</c> via
        /// downstream context inference.
        /// </remarks>
        /// <param name="col">Header column to recover an arm name from.</param>
        /// <returns>The recovered arm-name text, or <c>null</c> when no candidate qualifies.</returns>
        /// <seealso cref="looksLikeGenericArmLabel"/>
        /// <seealso cref="extractArmDefinitions"/>
        protected static string? recoverArmHeaderText(HeaderColumn col)
        {
            #region implementation

            var leaf = col.LeafHeaderText?.Trim();
            if (!looksLikeGenericArmLabel(leaf))
                return leaf;

            // Walk up the HeaderPath (excluding the leaf itself) looking for a candidate
            // that is not blank and not generic. The HeaderPath convention places the
            // leaf at the last index, so we iterate from Count-2 down to 0.
            if (col.HeaderPath != null)
            {
                for (int i = col.HeaderPath.Count - 2; i >= 0; i--)
                {
                    var candidate = col.HeaderPath[i]?.Trim();
                    if (!looksLikeGenericArmLabel(candidate))
                        return candidate;
                }
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recovers a treatment-arm header from an adjacent sibling column with the
        /// same parent header path.
        /// </summary>
        /// <remarks>
        /// SPL tables sometimes leave one leaf blank or use a structural leaf while a
        /// sibling in the same colspan carries the actual arm name. This keeps that
        /// column recoverable before body-row enrichment runs.
        /// </remarks>
        /// <param name="columns">Resolved header columns for the table.</param>
        /// <param name="columnOffset">Offset of the column within <paramref name="columns"/>.</param>
        /// <returns>Sibling arm text, or <c>null</c> when no sibling qualifies.</returns>
        /// <seealso cref="recoverArmHeaderText"/>
        private static string? recoverArmHeaderTextFromSiblingSpan(
            IReadOnlyList<HeaderColumn> columns, int columnOffset)
        {
            #region implementation

            if (columnOffset < 0 || columnOffset >= columns.Count)
                return null;

            var source = columns[columnOffset];
            for (int i = columnOffset - 1; i >= 1; i--)
            {
                if (!hasSameHeaderParent(source, columns[i]))
                    break;

                var candidate = recoverArmHeaderText(columns[i]);
                if (candidate != null)
                    return candidate;
            }

            for (int i = columnOffset + 1; i < columns.Count; i++)
            {
                if (!hasSameHeaderParent(source, columns[i]))
                    break;

                var candidate = recoverArmHeaderText(columns[i]);
                if (candidate != null)
                    return candidate;
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a column-position placeholder for a header column whose arm name
        /// must be recovered from body metadata.
        /// </summary>
        /// <param name="col">Header column that did not yield a real treatment arm.</param>
        /// <returns>An arm placeholder retaining column index and axis metadata.</returns>
        /// <seealso cref="enrichArmsFromBodyRows"/>
        private static ArmDefinition createPlaceholderArmDefinition(HeaderColumn col)
        {
            #region implementation

            var arm = new ArmDefinition
            {
                ColumnIndex = col.ColumnIndex,
                StudyContext = getHeaderStudyContext(col) ?? getContextAxisLabel(col.LeafHeaderText)
            };
            applyAxisMetadata(col.LeafHeaderText, arm);
            return arm;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when two columns share the same non-leaf header path.
        /// </summary>
        /// <param name="left">First header column.</param>
        /// <param name="right">Second header column.</param>
        /// <returns><c>true</c> when the columns are siblings under the same span.</returns>
        private static bool hasSameHeaderParent(HeaderColumn left, HeaderColumn right)
        {
            #region implementation

            var leftParent = getHeaderParentKey(left);
            var rightParent = getHeaderParentKey(right);
            return string.Equals(leftParent, rightParent, StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds a stable key from the non-leaf entries in a column header path.
        /// </summary>
        /// <param name="col">Header column to inspect.</param>
        /// <returns>Parent-path key, or an empty string for single-level headers.</returns>
        private static string getHeaderParentKey(HeaderColumn col)
        {
            #region implementation

            if (col.HeaderPath == null || col.HeaderPath.Count <= 1)
                return string.Empty;

            return string.Join("\u001F", col.HeaderPath.Take(col.HeaderPath.Count - 1)
                .Select(p => p?.Trim() ?? string.Empty));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets a non-generic parent header as study context for placeholder arms.
        /// </summary>
        /// <param name="col">Header column to inspect.</param>
        /// <returns>Study context candidate, or <c>null</c> when none is available.</returns>
        private static string? getHeaderStudyContext(HeaderColumn col)
        {
            #region implementation

            if (col.HeaderPath == null || col.HeaderPath.Count <= 1)
                return null;

            var candidate = col.HeaderPath[0]?.Trim();
            return looksLikeGenericArmLabel(candidate) ? null : candidate;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns a context-axis label that should be preserved outside
        /// <c>TreatmentArm</c>.
        /// </summary>
        /// <param name="text">Header text to inspect.</param>
        /// <returns>Trimmed context label, or <c>null</c> when not recognized.</returns>
        private static string? getContextAxisLabel(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return null;

            var trimmed = text.Trim();
            return _contextAxisLabelPattern.IsMatch(trimmed) ? trimmed : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Phase 3 helper: returns <c>true</c> when the given header text is null,
        /// whitespace, or matches the generic-axis-label pattern
        /// (<see cref="_genericArmLabelPattern"/>). Used by
        /// <see cref="recoverArmHeaderText"/> to decide whether to walk up the
        /// HeaderPath in search of a real arm name.
        /// </summary>
        /// <param name="text">Header text to evaluate.</param>
        /// <returns><c>true</c> when the text should NOT be used as a treatment arm.</returns>
        /// <seealso cref="_genericArmLabelPattern"/>
        protected static bool looksLikeGenericArmLabel(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return true;
            return _genericArmLabelPattern.IsMatch(text) ||
                   _armAxisLabelPattern.IsMatch(text) ||
                   _contextAxisLabelPattern.IsMatch(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Router-facing wrapper for the shared generic arm-label detector.
        /// </summary>
        /// <param name="text">Header text to evaluate.</param>
        /// <returns><c>true</c> when the text is structural rather than a treatment arm.</returns>
        internal static bool LooksLikeGenericArmLabelForRouting(string? text)
        {
            #region implementation

            return looksLikeGenericArmLabel(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether an arm definition has a usable treatment-arm name.
        /// </summary>
        /// <param name="arm">Arm definition to evaluate.</param>
        /// <returns><c>true</c> when observations may be emitted for this arm.</returns>
        /// <seealso cref="looksLikeGenericArmLabel"/>
        protected static bool hasUsableTreatmentArm(ArmDefinition? arm)
        {
            #region implementation

            return arm != null &&
                   !string.IsNullOrWhiteSpace(arm.Name) &&
                   !looksLikeGenericArmLabel(arm.Name);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies a conservative single-product fallback when no column has a
        /// usable treatment arm.
        /// </summary>
        /// <remarks>
        /// This fallback only trusts concise product-title evidence and preserves
        /// structural/context header labels in <see cref="ArmDefinition.StudyContext"/>.
        /// It is intentionally skipped as soon as any real arm has already been
        /// recovered from the header or body metadata rows.
        /// </remarks>
        /// <param name="table">Reconstructed source table.</param>
        /// <param name="arms">Arm definitions to update in place.</param>
        /// <returns><c>true</c> when a product arm was applied.</returns>
        /// <seealso cref="ArmDefinition"/>
        protected static bool applySingleProductArmFallback(ReconstructedTable table, List<ArmDefinition> arms)
        {
            #region implementation

            if (arms.Count == 0 || arms.Any(hasUsableTreatmentArm))
                return false;

            var productArm = inferSingleProductArmFromTitle(table);
            if (string.IsNullOrWhiteSpace(productArm))
                return false;

            foreach (var arm in arms)
            {
                if (string.IsNullOrWhiteSpace(arm.StudyContext))
                    arm.StudyContext = getContextAxisLabel(arm.Name);

                arm.Name = productArm;
            }

            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a parsed AE row is structural metadata rather than
        /// a reportable observation.
        /// </summary>
        /// <param name="obs">Observation candidate after value parsing.</param>
        /// <param name="parsed">Parsed value decomposition.</param>
        /// <returns><c>true</c> when the row should be suppressed.</returns>
        /// <seealso cref="ParsedObservation"/>
        protected static bool shouldSuppressAeStructuralObservation(ParsedObservation obs, ParsedValue parsed)
        {
            #region implementation

            return shouldSuppressStructuralObservation(obs, parsed);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a parsed AE or Efficacy row is structural metadata
        /// rather than a reportable observation.
        /// </summary>
        /// <param name="obs">Observation candidate after value parsing.</param>
        /// <param name="parsed">Parsed value decomposition.</param>
        /// <returns><c>true</c> when the row should be suppressed.</returns>
        /// <seealso cref="ParsedObservation"/>
        protected static bool shouldSuppressStructuralObservation(ParsedObservation obs, ParsedValue parsed)
        {
            #region implementation

            var isAe = string.Equals(obs.TableCategory, TableCategory.ADVERSE_EVENT.ToString(), StringComparison.OrdinalIgnoreCase);
            var isEfficacy = string.Equals(obs.TableCategory, TableCategory.EFFICACY.ToString(), StringComparison.OrdinalIgnoreCase);
            if (!isAe && !isEfficacy)
                return false;

            var rawValue = obs.RawValue?.Trim();
            var parameterName = obs.ParameterName?.Trim();

            // Dash/em-dash and "(N=…)" cells are structural ONLY when the row's
            // parameter is itself a structural divider (SOC label, header echo, or
            // non-observation metric). For real AE/Efficacy rows like
            // "Amnesia | 1 | -" the dash placebo cell is a legitimate sub-threshold
            // marker that downstream coercion converts to a <1% midpoint estimate.
            var parameterIsStructural =
                string.IsNullOrWhiteSpace(parameterName) ||
                isStructuralRowLabel(parameterName) ||
                _aeHeaderEchoParameterPattern.IsMatch(parameterName) ||
                _aeNonObservationMetricPattern.IsMatch(parameterName);

            if (!string.IsNullOrWhiteSpace(rawValue) &&
                _placeholderStatValuePattern.IsMatch(rawValue) &&
                parameterIsStructural)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(rawValue) &&
                _structuralAeValueCellPattern.IsMatch(rawValue) &&
                isAe &&
                parameterIsStructural)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(parameterName) &&
                _aeNonObservationMetricPattern.IsMatch(parameterName))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(parameterName) &&
                parsed.PrimaryValue == null &&
                parsed.SecondaryValue == null &&
                parsed.PValue == null &&
                _aeHeaderEchoParameterPattern.IsMatch(parameterName))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(rawValue) &&
                !string.IsNullOrWhiteSpace(parameterName) &&
                string.Equals(rawValue, parameterName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(rawValue) && isStructuralRowLabel(rawValue))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(parameterName) &&
                _eventPseudoArmPattern.IsMatch(obs.TreatmentArm ?? string.Empty) &&
                isStructuralRowLabel(parameterName))
            {
                return true;
            }

            return parsed.PrimaryValue == null &&
                   string.Equals(parsed.PrimaryValueType, "Text", StringComparison.OrdinalIgnoreCase) &&
                   (_aeHeaderEchoParameterPattern.IsMatch(rawValue ?? string.Empty) ||
                    isStructuralRowLabel(rawValue) ||
                    string.Equals(rawValue, parameterName, StringComparison.OrdinalIgnoreCase));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether text is a known structural row label such as an
        /// adverse-event SOC/body-system label or Efficacy event-group label.
        /// </summary>
        /// <param name="text">Candidate row or cell text.</param>
        /// <returns><c>true</c> when the text should be retained as context only.</returns>
        protected static bool isStructuralRowLabel(string? text)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(text) &&
                   _knownStructuralRowLabelPattern.IsMatch(text.Trim());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a data-body row is structural context only and
        /// should update the current category/group instead of emitting observations.
        /// </summary>
        /// <param name="row">Candidate data row.</param>
        /// <param name="arms">Resolved arm definitions for the table.</param>
        /// <param name="parameterName">Cleaned row label from column 0.</param>
        /// <param name="category">Parser category evaluating the row.</param>
        /// <returns><c>true</c> when the row carries context and no reportable value cells.</returns>
        /// <seealso cref="isStructuralRowLabel"/>
        /// <seealso cref="shouldSuppressStructuralObservation"/>
        protected static bool isStructuralContextRow(
            ReconstructedRow row,
            IEnumerable<ArmDefinition> arms,
            string? parameterName,
            TableCategory category)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(parameterName))
                return false;

            var rowLabelIsStructural =
                isStructuralRowLabel(parameterName) ||
                _aeHeaderEchoParameterPattern.IsMatch(parameterName) ||
                _aeNonObservationMetricPattern.IsMatch(parameterName);

            if (!rowLabelIsStructural)
                return false;

            var usableArms = arms.Where(hasUsableTreatmentArm).ToList();
            if (usableArms.Count == 0)
                return true;

            return !usableArms.Any(arm =>
            {
                var cell = getCellAtColumn(row, arm.ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    return false;

                var text = cell.CleanedText.Trim();
                if (_structuralAeValueCellPattern.IsMatch(text) ||
                    isStructuralRowLabel(text) ||
                    string.Equals(text, parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var parsed = ValueParser.Parse(text, arm.SampleSize);
                return parsed.PrimaryValue.HasValue ||
                       parsed.SecondaryValue.HasValue ||
                       parsed.PValue.HasValue;
            });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Records an audit entry for a structural row or cell suppressed before
        /// observation emission.
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
        /// <seealso cref="TableSuppressionAuditRecord"/>
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
            string reason)
        {
            #region implementation

            _suppressedRows.Add(new TableSuppressionAuditRecord
            {
                TextTableID = table.TextTableID,
                SourceRowSeq = row.SequenceNumberTextTableRow,
                SourceCellSeq = cell?.SequenceNumber,
                TableCategory = category.ToString(),
                ParserName = GetType().Name,
                ParameterName = parameterName,
                TreatmentArm = treatmentArm,
                RawValue = rawValue,
                StructuralLabel = structuralLabel,
                ContextTarget = contextTarget,
                Reason = reason
            });

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Infers a single product arm from concise product-title evidence.
        /// </summary>
        /// <param name="table">Reconstructed table carrying document title metadata.</param>
        /// <returns>Product arm candidate, or <c>null</c> when ambiguous or unavailable.</returns>
        private static string? inferSingleProductArmFromTitle(ReconstructedTable table)
        {
            #region implementation

            var title = TextUtil.RemoveTags(table.Title ?? string.Empty);
            title = Regex.Replace(title, @"\s+", " ").Trim();
            if (string.IsNullOrWhiteSpace(title))
                return null;

            var candidate = extractProductTitleCandidate(title);
            return candidate != null && isProductTitleCandidate(candidate) ? candidate : null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts a concise product-name candidate from a document title.
        /// </summary>
        /// <param name="title">Cleaned document title.</param>
        /// <returns>Candidate product name, or <c>null</c> if the title is not product-shaped.</returns>
        private static string? extractProductTitleCandidate(string title)
        {
            #region implementation

            var firstSentence = Regex.Split(title, @"[\r\n.]")
                .Select(p => p.Trim())
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
            if (string.IsNullOrWhiteSpace(firstSentence))
                return null;

            var productMatch = Regex.Match(firstSentence,
                @"^([A-Z][A-Za-z0-9-]{2,}(?:\s+[A-Z][A-Za-z0-9-]{2,}){0,3})(?=\s*(?:\(|-|,|:|$|\b(?:Tablets?|Capsules?|Injection|Nasal|Oral|Solution|Gel)\b))");
            if (productMatch.Success)
                return productMatch.Groups[1].Value.Trim();

            return firstSentence;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates that a title-derived candidate is specific enough to use as
        /// a treatment arm.
        /// </summary>
        /// <param name="candidate">Title-derived product candidate.</param>
        /// <returns><c>true</c> when the candidate is safe to use.</returns>
        private static bool isProductTitleCandidate(string candidate)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            var trimmed = candidate.Trim();
            if (trimmed.Length > 80)
                return false;

            if (Regex.IsMatch(trimmed,
                    @"^(?:Test\s+Drug|Drug|Treatment|Placebo|These\s+Highlights|Highlights\s+of\s+Prescribing\s+Information|Full\s+Prescribing\s+Information)\b",
                    RegexOptions.IgnoreCase))
            {
                return false;
            }

            return Regex.IsMatch(trimmed, @"^[A-Za-z][A-Za-z0-9'\-]*(?:\s+[A-Za-z0-9'\-]+){0,4}$");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies value-axis metadata from a header or body metadata cell to an arm.
        /// </summary>
        /// <param name="text">Header or metadata cell text.</param>
        /// <param name="arm">Arm definition to update.</param>
        /// <seealso cref="ArmDefinition.ParameterSubtype"/>
        private static void applyAxisMetadata(string? text, ArmDefinition arm)
        {
            #region implementation

            if (!tryExtractAxisMetadata(text, out var subtype, out var formatHint))
                return;

            if (!string.IsNullOrWhiteSpace(subtype))
                arm.ParameterSubtype = subtype;

            if (!string.IsNullOrWhiteSpace(formatHint))
                arm.FormatHint = formatHint;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts severity/metric subtype and percentage format hints from a
        /// non-arm column-axis label.
        /// </summary>
        /// <param name="text">Axis label text.</param>
        /// <param name="subtype">Extracted parameter subtype, if present.</param>
        /// <param name="formatHint">Extracted format hint, if present.</param>
        /// <returns><c>true</c> when the text matches a supported axis label.</returns>
        private static bool tryExtractAxisMetadata(
            string? text, out string? subtype, out string? formatHint)
        {
            #region implementation

            subtype = null;
            formatHint = null;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            if (!_armAxisLabelPattern.IsMatch(trimmed) && !_formatHintCellPattern.IsMatch(trimmed))
                return false;

            if (trimmed.Contains("n(", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("number", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("No.", StringComparison.OrdinalIgnoreCase))
            {
                formatHint = "n(%)";
            }
            else if (trimmed.Contains('%') ||
                     trimmed.Contains("percent", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("percentage", StringComparison.OrdinalIgnoreCase))
            {
                formatHint = "%";
            }

            var clean = Regex.Replace(trimmed, @"\s*(?:\(\s*%\s*\)|%)\s*$", "", RegexOptions.IgnoreCase).Trim();
            if (Regex.IsMatch(clean, @"^(?:Any|All\s+(?:CTC\s+)?Grades?|(?:CTC|CTCAE|NCI)\s+Grades?|Grades?|Grade|(?:>=|>|\u2265)\s*Grade|Toxicity\s+Grade|Severity\s+Grade)\b",
                    RegexOptions.IgnoreCase))
            {
                subtype = clean;
            }

            return true;

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

            var parsed = ValueParser.Parse(rawText, arm?.SampleSize);
            if (category != TableCategory.ADVERSE_EVENT && category != TableCategory.EFFICACY)
                return parsed;

            var isPValueContext = forcePValue ||
                isPValueRowLabel(rowLabel) ||
                isPValueRowLabel(arm?.Name);

            if (isPValueContext)
                return coerceToPValue(parsed);

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

            if (string.Equals(parsed.PrimaryValueType, "Numeric", StringComparison.OrdinalIgnoreCase) &&
                isCountValueContext(rowLabel, arm, caption))
            {
                parsed.PrimaryValueType = "Count";
                parsed.Unit = null;
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "COUNT_CONTEXT_PROMOTION");
            }

            if (string.Equals(parsed.PrimaryValueType, "Numeric", StringComparison.OrdinalIgnoreCase) &&
                isPercentValueContext(category, rowLabel, parameterCategory, arm, caption))
            {
                parsed.PrimaryValueType = "Percentage";
                parsed.Unit = "%";
                parsed.ValidationFlags = appendFlag(parsed.ValidationFlags, "PCT_CONTEXT_PROMOTION");
            }

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
        /// The comparison suppressor is deliberately narrow: it requires identical table,
        /// source row, source cell, parameter name, and raw value. This preserves valid
        /// arm-specific efficacy rows that merely share labels such as median, survival,
        /// response, or CI with comparison rows in neighboring cells.
        /// 
        /// NOTE: 05/01/2026 This is disabled because the GroupBy clause is insufficently 
        /// unique -- it removes tables where the rows are likely not duplicative.
        /// 
        /// Possible Fix:
        /// 
        /// var duplicateKeys = observations
        /// .Where(IsDefiniteComparisonStatEmission)
        /// .GroupBy(o => new
        /// {
        ///    o.TextTableID,
        ///    o.SourceRowSeq,
        ///    o.SourceCellSeq,
        ///    ParameterName = normalizeDuplicateKeyText(o.ParameterName),
        ///    RawValue = normalizeDuplicateKeyText(o.RawValue),
        ///    o.PrimaryValueType,
        ///    o.PrimaryValue,
        ///    o.PValue,
        ///    o.SecondaryValue
        /// })
        /// 
        /// </remarks>
        /// <param name="observations">Mutable observation list to deduplicate.</param>
        /// <seealso cref="ParsedObservation"/>
        protected static void suppressDuplicateComparisonEmissions(List<ParsedObservation> observations)
        {
            #region implementation

            //if (observations.Count < 2)
            //    return;

            //var duplicateKeys = observations
            //    .Where(o =>
            //        string.Equals(o.TableCategory, TableCategory.EFFICACY.ToString(), StringComparison.OrdinalIgnoreCase) &&
            //        o.SourceRowSeq.HasValue &&
            //        o.SourceCellSeq.HasValue)
            //    .GroupBy(o => new
            //    {
            //        o.TextTableID,
            //        o.SourceRowSeq,
            //        o.SourceCellSeq,
            //        ParameterName = normalizeDuplicateKeyText(o.ParameterName),
            //        RawValue = normalizeDuplicateKeyText(o.RawValue)
            //    })
            //    .Where(g =>
            //        g.Any(o => string.Equals(o.TreatmentArm, "Comparison", StringComparison.OrdinalIgnoreCase)) &&
            //        g.Any(o => !string.Equals(o.TreatmentArm, "Comparison", StringComparison.OrdinalIgnoreCase)))
            //    .Select(g => g.Key)
            //    .ToHashSet();

            //if (duplicateKeys.Count == 0)
            //    return;

            //observations.RemoveAll(o =>
            //    o.SourceRowSeq.HasValue &&
            //    o.SourceCellSeq.HasValue &&
            //    !string.Equals(o.TreatmentArm, "Comparison", StringComparison.OrdinalIgnoreCase) &&
            //    duplicateKeys.Contains(new
            //    {
            //        o.TextTableID,
            //        o.SourceRowSeq,
            //        o.SourceCellSeq,
            //        ParameterName = normalizeDuplicateKeyText(o.ParameterName),
            //        RawValue = normalizeDuplicateKeyText(o.RawValue)
            //    }));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes nullable text fields for duplicate comparison-row keying.
        /// </summary>
        /// <param name="text">Source text.</param>
        /// <returns>Trimmed text, or empty string for null values.</returns>
        private static string normalizeDuplicateKeyText(string? text)
        {
            #region implementation

            return text?.Trim() ?? string.Empty;

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

            if (isPValueRowLabel(rowLabel))
                return false;

            if (isCountValueContext(rowLabel, arm, caption))
                return false;

            if (containsPercentHint(rowLabel) ||
                containsPercentHint(parameterCategory) ||
                containsPercentHint(arm?.FormatHint) ||
                containsPercentHint(arm?.Name) ||
                containsPercentHint(caption))
            {
                return true;
            }

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

            if (string.IsNullOrWhiteSpace(text))
                return false;

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

            if (parsed.PrimaryValue.HasValue)
            {
                parsed.PrimaryValueType = "PValue";
                parsed.PValue = parsed.PrimaryValue;
                parsed.Unit = null;
                parsed.PValueQualifier ??= "=";
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

            var hasArmN = armN.HasValue && armN.Value > 0;
            var resolvedArmN = armN.GetValueOrDefault();
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

            var hasArmN = armN.HasValue && armN.Value > 0;
            var resolvedArmN = armN.GetValueOrDefault();
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

            var rejectedPercentage = parsed.PrimaryValue;
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

            recoveredName = null;
            categoryLabel = null;
            labelCellSequence = null;

            if (category != TableCategory.ADVERSE_EVENT && category != TableCategory.EFFICACY)
                return false;

            var orderedArms = arms
                .Where(a => a.ColumnIndex.HasValue)
                .OrderBy(a => a.ColumnIndex!.Value)
                .ToList();

            var usableArms = orderedArms
                .Where(hasUsableTreatmentArm)
                .ToList();

            if (orderedArms.Count < 2 || usableArms.Count == 0)
                return false;

            var labelCandidate = orderedArms
                .FirstOrDefault(a =>
                {
                    var candidateCell = getCellAtColumn(row, a.ColumnIndex!.Value);
                    if (candidateCell == null || string.IsNullOrWhiteSpace(candidateCell.CleanedText))
                        return false;

                    var candidateText = candidateCell.CleanedText.Trim();
                    var candidateParsed = ValueParser.Parse(candidateText, a.SampleSize);
                    return string.Equals(candidateParsed.PrimaryValueType, "Text", StringComparison.OrdinalIgnoreCase) &&
                           Regex.IsMatch(candidateText, @"[A-Za-z]") &&
                           !_placeholderStatValuePattern.IsMatch(candidateText) &&
                           !string.Equals(candidateText, rowLabel, StringComparison.OrdinalIgnoreCase);
                });

            if (labelCandidate == null)
                return false;

            var firstCell = getCellAtColumn(row, labelCandidate.ColumnIndex!.Value);
            if (firstCell == null || string.IsNullOrWhiteSpace(firstCell.CleanedText))
                return false;

            var text = firstCell.CleanedText.Trim();
            var labelColumn = labelCandidate.ColumnIndex!.Value;
            var laterNumeric = usableArms
                .Where(arm => arm.ColumnIndex!.Value > labelColumn)
                .Any(arm =>
            {
                var cell = getCellAtColumn(row, arm.ColumnIndex!.Value);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    return false;

                var later = ValueParser.Parse(cell.CleanedText, arm.SampleSize);
                return later.PrimaryValue.HasValue ||
                       later.SecondaryValue.HasValue ||
                       later.PValue.HasValue;
            });

            if (!laterNumeric)
                return false;

            var (cleaned, _) = ValueParser.CleanParameterName(text);
            recoveredName = cleaned;
            labelCellSequence = firstCell.SequenceNumber;

            if (!string.IsNullOrWhiteSpace(rowLabel) &&
                !_structuralAeValueCellPattern.IsMatch(rowLabel) &&
                !_aeHeaderEchoParameterPattern.IsMatch(rowLabel))
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

            if (string.IsNullOrWhiteSpace(caption))
                return null;

            // Stage 1: Normalize — HTML-decode, strip tags, collapse whitespace
            var normalized = System.Net.WebUtility.HtmlDecode(caption);
            normalized = _htmlTagPattern.Replace(normalized, " ");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            // Stage 2: Strip leading "Table N:" / "Table 9." prefix
            var stripped = _tableNumberPrefixPattern.Replace(normalized, "");

            // Stage 3: Require an AE measure phrase somewhere in the caption.
            // If the caption is not AE-shaped, refuse to guess a StudyContext.
            var measureMatch = _aeCaptionMeasurePhrasePattern.Match(stripped);
            if (!measureMatch.Success)
                return null;

            // Stage 4: Find the connector word that introduces the trial
            // descriptor AFTER the measure phrase. Scan from the end of the
            // measure phrase forward so we don't accidentally latch onto an
            // "in" that lives inside the measure phrase itself.
            var searchStart = measureMatch.Index + measureMatch.Length;
            if (searchStart >= stripped.Length)
                return null;

            var connectorMatch = _aeCaptionConnectorPattern.Match(stripped, searchStart);
            if (!connectorMatch.Success)
                return null;

            var descriptor = stripped.Substring(connectorMatch.Index + connectorMatch.Length).Trim();

            // Stage 5: Trim trailing footnote markers, HTML residue, punctuation
            descriptor = _trailingFootnoteMarkerPattern.Replace(descriptor, "").Trim();
            descriptor = descriptor.TrimEnd('.', ',', ';', ':', ' ');

            // Stage 6: Reject values that are too short to carry useful context
            if (descriptor.Length <= 3)
                return null;

            return descriptor;

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

            // Cell-embedded sample size (e.g., "(n=129)") → ArmN when not already set from header
            if (parsed.SampleSize.HasValue && !obs.ArmN.HasValue)
                obs.ArmN = parsed.SampleSize;

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

        #region Protected Helpers — Body Row Enrichment

        /**************************************************************/
        /// <summary>
        /// Scans the first few body rows for header-continuation metadata (dose regimen,
        /// sample size, format hints) and enriches arm definitions accordingly. Returns
        /// the number of enrichment rows consumed so callers can skip them.
        /// </summary>
        /// <remarks>
        /// Stops scanning at the first non-enrichment row. Each enrichment type
        /// (dose, n_equals, format_hint) is applied at most once.
        /// </remarks>
        /// <param name="dataRows">The filtered data body rows.</param>
        /// <param name="arms">Arm definitions to enrich (modified in place).</param>
        /// <returns>The number of leading enrichment rows to skip.</returns>
        /// <seealso cref="classifyEnrichmentRow"/>
        protected static int enrichArmsFromBodyRows(List<ReconstructedRow> dataRows, List<ArmDefinition> arms)
        {
            #region implementation

            int enrichmentCount = 0;
            var consumed = new HashSet<string>();
            var limit = Math.Min(dataRows.Count, 5);

            for (int r = 0; r < limit; r++)
            {
                var row = dataRows[r];
                if (row.Classification == RowClassification.SocDivider)
                    break;

                var rowType = classifyEnrichmentRow(row, arms);
                if (rowType == null || consumed.Contains(rowType))
                    break;

                consumed.Add(rowType);
                enrichmentCount++;
                applyEnrichmentRow(row, arms, rowType);
            }

            return enrichmentCount;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Classifies a body row as a specific enrichment type if most data cells match
        /// a single metadata pattern (dose, N=, or format hint).
        /// </summary>
        /// <param name="row">The body row to classify.</param>
        /// <param name="arms">Current arm definitions for column lookup.</param>
        /// <returns>"dose", "n_equals", "format_hint", or null for data rows.</returns>
        private static string? classifyEnrichmentRow(ReconstructedRow row, List<ArmDefinition> arms)
        {
            #region implementation

            if (arms.Count == 0) return null;

            var rowLabel = getCellAtColumn(row, 0)?.CleanedText;
            if (!looksLikeMetadataRowLabel(rowLabel))
                return null;

            var allowArmNameEnrichment = looksLikeArmNameMetadataRowLabel(rowLabel) ||
                arms.Any(a => !hasUsableTreatmentArm(a) || looksLikeStudyIdentifier(a.Name));

            int armNameCount = 0, doseCount = 0, nCount = 0, fmtCount = 0, cellCount = 0;
            var previousText = (string?)null;

            foreach (var arm in arms)
            {
                var cell = getCellAtColumn(row, arm.ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;

                cellCount++;
                var text = cell.CleanedText.Trim();

                if (isDittoCellText(text))
                {
                    if (!string.IsNullOrWhiteSpace(previousText))
                    {
                        if (looksLikeArmNameCell(previousText)) armNameCount++;
                        else if (_doseRegimenPattern.IsMatch(previousText)) doseCount++;
                        else if (_nEqualsCellPattern.IsMatch(previousText)) nCount++;
                        else if (looksLikeFormatAxisCell(previousText)) fmtCount++;
                    }
                    continue;
                }

                previousText = text;

                if (looksLikeArmNameCell(text)) armNameCount++;
                else if (_doseRegimenPattern.IsMatch(text)) doseCount++;
                else if (_nEqualsCellPattern.IsMatch(text)) nCount++;
                else if (looksLikeFormatAxisCell(text)) fmtCount++;
            }

            if (cellCount == 0) return null;

            // Require majority match (>= 50% of non-empty cells)
            if (allowArmNameEnrichment && armNameCount * 2 >= cellCount) return "arm_name";
            if (doseCount * 2 >= cellCount) return "dose";
            if (nCount * 2 >= cellCount) return "n_equals";
            if (fmtCount * 2 >= cellCount) return "format_hint";

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether column 0 identifies a leading metadata row rather than a
        /// clinical observation row.
        /// </summary>
        /// <param name="text">Column 0 text.</param>
        /// <returns><c>true</c> when the row can safely enrich arm metadata.</returns>
        private static bool looksLikeMetadataRowLabel(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return true;

            var trimmed = text.Trim();
            if (trimmed is "-" or "--")
                return true;

            return Regex.IsMatch(trimmed,
                @"^(?:Col\s*0|Adverse\s+(?:Reaction|Reactions|Event|Events)(?:\s*\(.*\))?|Body\s+System(?:\s*\(.*\))?|System\s+Organ\s+Class|Preferred\s+Term|Treatment\s+Arm|Arm|Group|Study\s+Drug)\s*$",
                RegexOptions.IgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether column 0 explicitly names a treatment-arm metadata row.
        /// </summary>
        /// <param name="text">Column 0 text.</param>
        /// <returns><c>true</c> when body cells should be interpreted as arm names.</returns>
        private static bool looksLikeArmNameMetadataRowLabel(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return false;

            return Regex.IsMatch(text.Trim(),
                @"^(?:Treatment\s+Arm|Arm|Group|Study\s+Drug)\s*$",
                RegexOptions.IgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a recovered header is more likely a study identifier than a treatment arm.
        /// </summary>
        /// <param name="text">Header-derived arm name.</param>
        /// <returns><c>true</c> for compact study labels such as TAX323 or TMC114-C230.</returns>
        private static bool looksLikeStudyIdentifier(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return false;

            return Regex.IsMatch(text.Trim(),
                @"^(?:[A-Z]{2,}[-\s]?)?\d{2,}[A-Z0-9/-]*$",
                RegexOptions.IgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a cell is an arrow/ditto marker that inherits the
        /// previous metadata value in the row.
        /// </summary>
        /// <param name="text">Cell text to inspect.</param>
        /// <returns><c>true</c> when the cell means "same as previous".</returns>
        private static bool isDittoCellText(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            return trimmed is "\u2194" or "\u2192" or "\u2190" or "\u27F7" or "\u27F6" or "<->" or "->" or "<-";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a leading body-row cell looks like a treatment arm name.
        /// </summary>
        /// <param name="text">Cell text to inspect.</param>
        /// <returns><c>true</c> when the cell can supply an arm name.</returns>
        private static bool looksLikeArmNameCell(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            if (isDittoCellText(trimmed) ||
                _nEqualsCellPattern.IsMatch(trimmed) ||
                looksLikeFormatAxisCell(trimmed) ||
                _doseRegimenPattern.IsMatch(trimmed) ||
                looksLikeGenericArmLabel(trimmed))
            {
                return false;
            }

            if (Regex.IsMatch(trimmed, @"^[<>=]?\s*\d+(?:\.\d+)?\s*(?:%|\([^)]*\))?$"))
                return false;

            return Regex.IsMatch(trimmed, @"[A-Za-z]");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a cell contains a format hint or value-axis label.
        /// </summary>
        /// <param name="text">Cell text to inspect.</param>
        /// <returns><c>true</c> for cells such as <c>n (%)</c>, <c>Any %</c>, or <c>Grade 3/4 %</c>.</returns>
        private static bool looksLikeFormatAxisCell(string? text)
        {
            #region implementation

            return _formatHintCellPattern.IsMatch(text ?? string.Empty) ||
                   tryExtractAxisMetadata(text, out _, out _);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies enrichment data from a classified body row to arm definitions.
        /// </summary>
        /// <param name="row">The enrichment row.</param>
        /// <param name="arms">Arm definitions to update (modified in place).</param>
        /// <param name="rowType">The enrichment type: "dose", "n_equals", or "format_hint".</param>
        private static void applyEnrichmentRow(
            ReconstructedRow row, List<ArmDefinition> arms, string rowType)
        {
            #region implementation

            string? previousArmName = null;
            string? previousDoseRegimen = null;
            int? previousN = null;
            string? previousFormatHint = null;
            string? previousSubtype = null;

            for (int i = 0; i < arms.Count; i++)
            {
                var cell = getCellAtColumn(row, arms[i].ColumnIndex ?? 0);
                if (cell == null || string.IsNullOrWhiteSpace(cell.CleanedText))
                    continue;

                var text = cell.CleanedText.Trim();

                switch (rowType)
                {
                    case "arm_name":
                        if (isDittoCellText(text))
                            text = previousArmName;
                        else
                            previousArmName = text;

                        applyArmNameMetadata(arms[i], text);
                        break;

                    case "dose":
                        if (isDittoCellText(text))
                            text = previousDoseRegimen;
                        else
                            previousDoseRegimen = text;

                        if (string.IsNullOrWhiteSpace(text))
                            break;

                        arms[i].DoseRegimen = text;
                        var (dose, doseUnit) = DoseExtractor.Extract(text);
                        arms[i].Dose = dose;
                        arms[i].DoseUnit = doseUnit;
                        break;

                    case "n_equals":
                        if (isDittoCellText(text))
                        {
                            if (previousN.HasValue)
                                arms[i].SampleSize = previousN;
                            break;
                        }

                        var nMatch = _nEqualsCellPattern.Match(text);
                        if (nMatch.Success && int.TryParse(nMatch.Groups[1].Value.Replace(",", ""), out var n))
                        {
                            arms[i].SampleSize = n;
                            previousN = n;
                        }
                        break;

                    case "format_hint":
                        if (isDittoCellText(text))
                        {
                            if (!string.IsNullOrWhiteSpace(previousFormatHint))
                                arms[i].FormatHint = previousFormatHint;
                            if (!string.IsNullOrWhiteSpace(previousSubtype))
                                arms[i].ParameterSubtype = previousSubtype;
                            break;
                        }

                        applyAxisMetadata(text, arms[i]);
                        if (_formatHintCellPattern.IsMatch(text))
                            arms[i].FormatHint = text;
                        previousFormatHint = arms[i].FormatHint;
                        previousSubtype = arms[i].ParameterSubtype;
                        break;
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Applies a body-row arm-name cell to an arm definition.
        /// </summary>
        /// <param name="arm">Arm definition to update.</param>
        /// <param name="text">Recovered arm-name cell text.</param>
        /// <seealso cref="ValueParser.ParseArmHeader"/>
        private static void applyArmNameMetadata(ArmDefinition arm, string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text) || !looksLikeArmNameCell(text))
                return;

            var parsedArm = ValueParser.ParseArmHeader(text);
            var recoveredName = parsedArm?.Name ?? text.Trim();

            if (!string.IsNullOrWhiteSpace(arm.Name) &&
                !looksLikeGenericArmLabel(arm.Name) &&
                !string.Equals(arm.Name, recoveredName, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(arm.StudyContext))
            {
                arm.StudyContext = arm.Name;
            }

            arm.Name = recoveredName;
            if (parsedArm?.SampleSize != null)
                arm.SampleSize = parsedArm.SampleSize;
            if (!string.IsNullOrWhiteSpace(parsedArm?.FormatHint))
                arm.FormatHint = parsedArm.FormatHint;

            #endregion
        }

        #endregion Protected Helpers — Body Row Enrichment
    }
}
