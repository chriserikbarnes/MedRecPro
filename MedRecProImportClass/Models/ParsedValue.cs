namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Output DTO from <c>ValueParser.Parse()</c> representing the structured decomposition
    /// of a single cell value in the SPL Table Normalization pipeline (Stage 3).
    /// Composite cells like "27 (14)" are broken into typed, queryable components.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Stage 2 (ReconstructedTable) → **ValueParser** → ParsedValue → ParsedObservation → DB
    ///
    /// ## Value Type Hierarchy
    /// The <see cref="PrimaryValueType"/> indicates what <see cref="PrimaryValue"/> represents:
    /// - **Percentage**: Rate/incidence (0–100). SecondaryValue holds the n-count.
    /// - **Mean**: Central tendency. SecondaryValue holds SD or CV%.
    /// - **Numeric**: Bare number — may be promoted to Percentage by the parser in AE context.
    /// - **RelativeRiskReduction / RiskDifference**: Effect size with CI bounds.
    /// - **CodedExclusion**: Single-letter coded footnote (e.g., "A" = placebo ≥ drug).
    /// - **Text**: Non-numeric content at low confidence.
    ///
    /// ## Validation
    /// <see cref="ValidationFlags"/> carries automated check results:
    /// - <c>PCT_CHECK:PASS</c> — derived % matches reported % within 1.5pp
    /// - <c>PCT_CHECK:WARN:{derived}</c> — mismatch detected, may indicate ColSpan misalignment
    /// </remarks>
    /// <seealso cref="ParsedObservation"/>
    public class ParsedValue
    {
        #region Primary Value Properties

        /**************************************************************/
        /// <summary>
        /// Main numeric value: percentage, mean, hazard ratio, risk difference, or p-value.
        /// Null for excluded/text values.
        /// </summary>
        public double? PrimaryValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// What <see cref="PrimaryValue"/> represents.
        /// Values: Percentage, Mean, Median, Numeric, RelativeRiskReduction, RiskDifference,
        /// Ratio, MeanPercentChange, PValue, SampleSize, CodedExclusion, Text.
        /// </summary>
        public string? PrimaryValueType { get; set; }

        #endregion Primary Value Properties

        #region Secondary Value Properties

        /**************************************************************/
        /// <summary>
        /// Companion numeric value: n-count when primary is %, SD when primary is mean,
        /// CV% for PK values. Null when not applicable.
        /// </summary>
        public double? SecondaryValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// What <see cref="SecondaryValue"/> represents.
        /// Values: Count, SD, CV_Percent, SE.
        /// </summary>
        public string? SecondaryValueType { get; set; }

        #endregion Secondary Value Properties

        #region Bound Properties

        /**************************************************************/
        /// <summary>
        /// Lower limit of confidence interval or range.
        /// </summary>
        public double? LowerBound { get; set; }

        /**************************************************************/
        /// <summary>
        /// Upper limit of confidence interval or range.
        /// </summary>
        public double? UpperBound { get; set; }

        /**************************************************************/
        /// <summary>
        /// Type of bounds. Values: 95CI, 90CI, Range, IQR.
        /// </summary>
        public string? BoundType { get; set; }

        #endregion Bound Properties

        #region Statistical Properties

        /**************************************************************/
        /// <summary>
        /// P-value when present in the cell.
        /// </summary>
        public double? PValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// P-value qualifier. Values: "&lt;", "=", "&lt;=", "&gt;".
        /// </summary>
        public string? PValueQualifier { get; set; }

        /**************************************************************/
        /// <summary>
        /// Sample size extracted from cell text (e.g., n=129 from "0.80 (±0.36) (n=129)").
        /// Null when no sample size is embedded in the cell value.
        /// </summary>
        /// <remarks>
        /// Populated by <c>value_plusminus_sample</c> pattern when a trailing <c>(n=X)</c>
        /// is present. Mapped to <see cref="ParsedObservation.ArmN"/> by
        /// <c>BaseTableParser.applyParsedValue()</c> when ArmN is not already set.
        /// </remarks>
        /// <seealso cref="ParsedObservation"/>
        public int? SampleSize { get; set; }

        #endregion Statistical Properties

        #region Metadata Properties

        /**************************************************************/
        /// <summary>
        /// Unit of measurement. Values: "%", "mcg/mL", "hours", "ratio", "pp".
        /// </summary>
        public string? Unit { get; set; }

        /**************************************************************/
        /// <summary>
        /// True for empty, NA, dash, or coded exclusion values — indicates no
        /// numeric data was extracted.
        /// </summary>
        public bool IsExcluded { get; set; }

        /**************************************************************/
        /// <summary>
        /// Non-numeric text content for Text or CodedExclusion types.
        /// </summary>
        public string? TextValue { get; set; }

        #endregion Metadata Properties

        #region Confidence Constants

        /**************************************************************/
        /// <summary>
        /// Named confidence tiers assigned by <see cref="ValueParser"/> based on pattern specificity.
        /// These are ordinal ambiguity rankings, not calibrated probabilities.
        /// The 16 parse patterns map to exactly 5 tiers; downstream adjustments
        /// multiply against these baselines (never overwrite).
        /// </summary>
        /// <seealso cref="ConfidenceAdjustment"/>
        public static class ConfidenceTier
        {
            /**************************************************************/
            /// <summary>
            /// Structurally deterministic — only one interpretation possible.
            /// Assigned to: n/d(%), n(%), RR+CI, Diff+CI, Value(CV%), standalone %, n=, p-value.
            /// </summary>
            public const double Unambiguous = 1.0;

            /**************************************************************/
            /// <summary>
            /// Structural match requiring validation logic (e.g., lower bound &lt; upper bound).
            /// Assigned to: Value+CI, Value±SD, Value(±X)(n=N).
            /// </summary>
            public const double ValidatedMatch = 0.95;

            /**************************************************************/
            /// <summary>
            /// Regex matched but semantic type unknown without context.
            /// Assigned to: Range, plain number.
            /// </summary>
            public const double AmbiguousMatch = 0.9;

            /**************************************************************/
            /// <summary>
            /// Recognized non-data pattern (empty, NA, dash).
            /// </summary>
            public const double KnownExclusion = 0.8;

            /**************************************************************/
            /// <summary>
            /// No pattern matched — unstructured text fallback.
            /// </summary>
            public const double TextFallback = 0.5;
        }

        /**************************************************************/
        /// <summary>
        /// Named confidence adjustment multipliers applied when downstream parsers
        /// modify the initial <see cref="ConfidenceTier"/> value. These are always
        /// multiplied against the existing confidence, never assigned directly.
        /// </summary>
        /// <seealso cref="ConfidenceTier"/>
        public static class ConfidenceAdjustment
        {
            /**************************************************************/
            /// <summary>
            /// Caption hint provides ambiguous type context (e.g., bare "Mean" without parenthetical).
            /// </summary>
            public const double AmbiguousCaptionHint = 0.85;

            /**************************************************************/
            /// <summary>
            /// Bare Numeric promoted to Mean without caption confirmation (PK fallback).
            /// </summary>
            public const double UncaptionedTypePromotion = 0.8;

            /**************************************************************/
            /// <summary>
            /// PK sample-size column detected by position, not caption.
            /// </summary>
            public const double PositionalSampleSize = 0.9;
        }

        /**************************************************************/
        /// <summary>
        /// Named thresholds used by downstream consumers to make decisions based on
        /// <see cref="ParseConfidence"/> values.
        /// </summary>
        public static class ConfidenceThreshold
        {
            /**************************************************************/
            /// <summary>
            /// Below this value, observations are flagged LOW_CONFIDENCE (Warning severity).
            /// Used by RowValidationService.
            /// </summary>
            public const double LowConfidence = 0.5;

            /**************************************************************/
            /// <summary>Reporting band boundary: VeryHigh ≥ 0.95.</summary>
            public const double BandVeryHigh = 0.95;

            /**************************************************************/
            /// <summary>Reporting band boundary: High ≥ 0.80.</summary>
            public const double BandHigh = 0.80;

            /**************************************************************/
            /// <summary>Reporting band boundary: Medium ≥ 0.60.</summary>
            public const double BandMedium = 0.60;

            /**************************************************************/
            /// <summary>Reporting band boundary: Low ≥ 0.40.</summary>
            public const double BandLow = 0.40;
        }

        #endregion Confidence Constants

        #region Validation Properties

        /**************************************************************/
        /// <summary>
        /// Parse confidence score from 0.0 to 1.0 based on the <see cref="ConfidenceTier"/> system.
        /// Assigned by <see cref="ValueParser"/> at parse time, then adjusted multiplicatively
        /// by downstream parsers using <see cref="ConfidenceAdjustment"/> factors.
        /// </summary>
        /// <seealso cref="ConfidenceTier"/>
        /// <seealso cref="ConfidenceAdjustment"/>
        /// <seealso cref="ConfidenceThreshold"/>
        public double ParseConfidence { get; set; }

        /**************************************************************/
        /// <summary>
        /// Which regex pattern matched. Values: empty_or_na, letter_code, frac_pct,
        /// n_pct, rr_ci, diff_ci, value_cv, range_to, percentage, n_equals, pvalue,
        /// plain_number, text_descriptive.
        /// </summary>
        public string? ParseRule { get; set; }

        /**************************************************************/
        /// <summary>
        /// Automated validation check results. Example: "PCT_CHECK:PASS" or "PCT_CHECK:WARN:16.2".
        /// </summary>
        public string? ValidationFlags { get; set; }

        #endregion Validation Properties
    }
}
