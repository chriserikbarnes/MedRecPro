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

        #region Validation Properties

        /**************************************************************/
        /// <summary>
        /// Parse confidence score from 0.0 to 1.0.
        /// High (≥0.9): deterministic regex match. Medium (0.5–0.9): plain numbers/ranges.
        /// Low (&lt;0.5): unparsed text.
        /// </summary>
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
