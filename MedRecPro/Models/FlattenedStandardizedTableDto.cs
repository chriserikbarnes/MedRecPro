namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// API consumption DTO for Stage 3 SPL Table Normalization output.
    /// Mirrors the 36 columns of <see cref="LabelView.FlattenedStandardizedTable"/>
    /// without EF Core attributes for clean serialization.
    /// </summary>
    /// <remarks>
    /// ## Schema Groups (36 columns)
    /// - **Provenance (8)**: Traces every value back to the exact source cell
    /// - **Classification (4)**: Routes queries and groups comparable data
    /// - **Observation Context (9)**: Describes what was measured, in whom, under what conditions
    /// - **Decomposed Values (10)**: Typed, queryable components of the raw cell text
    /// - **Validation (5)**: Automated quality signals and confidence scores
    /// </remarks>
    /// <seealso cref="LabelView.FlattenedStandardizedTable"/>
    public class FlattenedStandardizedTableDto
    {
        #region Provenance Properties

        /**************************************************************/
        /// <summary>
        /// Source SPL document identifier.
        /// </summary>
        public Guid? DocumentGUID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Manufacturer name from vw_SectionNavigation.
        /// </summary>
        public string? LabelerName { get; set; }

        /**************************************************************/
        /// <summary>
        /// Drug product name (HTML stripped from Title).
        /// </summary>
        public string? ProductTitle { get; set; }

        /**************************************************************/
        /// <summary>
        /// Label version number — enables cross-version comparison.
        /// </summary>
        public int? VersionNumber { get; set; }

        /**************************************************************/
        /// <summary>
        /// FK to source TextTable.
        /// </summary>
        public int? TextTableID { get; set; }

        /**************************************************************/
        /// <summary>
        /// Table caption text (HTML stripped).
        /// </summary>
        public string? Caption { get; set; }

        /**************************************************************/
        /// <summary>
        /// SequenceNumberTextTableRow of the source row.
        /// </summary>
        public int? SourceRowSeq { get; set; }

        /**************************************************************/
        /// <summary>
        /// SequenceNumber of the source cell.
        /// </summary>
        public int? SourceCellSeq { get; set; }

        #endregion Provenance Properties

        #region Classification Properties

        /**************************************************************/
        /// <summary>
        /// Table category. Values: PK, ADVERSE_EVENT, EFFICACY, DOSING, BMD,
        /// TISSUE_DISTRIBUTION, DRUG_INTERACTION, OTHER.
        /// </summary>
        public string? TableCategory { get; set; }

        /**************************************************************/
        /// <summary>
        /// LOINC code of the parent section — primary routing key.
        /// </summary>
        public string? ParentSectionCode { get; set; }

        /**************************************************************/
        /// <summary>
        /// Human-readable parent section name (e.g., "ADVERSE REACTIONS").
        /// </summary>
        public string? ParentSectionTitle { get; set; }

        /**************************************************************/
        /// <summary>
        /// Section title (e.g., "Pharmacokinetics in Pediatric Patients").
        /// </summary>
        public string? SectionTitle { get; set; }

        #endregion Classification Properties

        #region Observation Context Properties

        /**************************************************************/
        /// <summary>
        /// What was measured: "Cmax", "Nausea", "Death or candidiasis", "Lumbar Spine".
        /// </summary>
        public string? ParameterName { get; set; }

        /**************************************************************/
        /// <summary>
        /// SOC class grouping: "Body as a Whole", "Nervous System".
        /// </summary>
        public string? ParameterCategory { get; set; }

        /**************************************************************/
        /// <summary>
        /// Sub-endpoint hierarchy: "Components of endpoint".
        /// </summary>
        public string? ParameterSubtype { get; set; }

        /**************************************************************/
        /// <summary>
        /// Treatment arm name: "EVISTA", "Placebo", "Fluconazole", or "Comparison".
        /// </summary>
        public string? TreatmentArm { get; set; }

        /**************************************************************/
        /// <summary>
        /// Sample size for this arm (parsed from header: N=2557).
        /// </summary>
        public int? ArmN { get; set; }

        /**************************************************************/
        /// <summary>
        /// From colspan grouping: "Treatment", "Prevention", "Study 1".
        /// </summary>
        public string? StudyContext { get; set; }

        /**************************************************************/
        /// <summary>
        /// Dose regimen for PK/dosing tables.
        /// </summary>
        public string? DoseRegimen { get; set; }

        /**************************************************************/
        /// <summary>
        /// Numeric dose value extracted from DoseRegimen or TreatmentArm. 0.0 for placebo.
        /// </summary>
        public decimal? Dose { get; set; }

        /**************************************************************/
        /// <summary>
        /// Normalized dose unit (e.g., "mg", "mg/d", "mg/kg"). Inherited for placebo arms.
        /// </summary>
        public string? DoseUnit { get; set; }

        /**************************************************************/
        /// <summary>
        /// Auto-detected population: "Adult Healthy Volunteers", "Postmenopausal Women".
        /// </summary>
        public string? Population { get; set; }

        /**************************************************************/
        /// <summary>
        /// Timepoint for BMD and longitudinal tables: "12 Months", "Day 49", "Week 12".
        /// For PK tables: extracted schedule duration label (e.g., "7 days", "single dose").
        /// </summary>
        public string? Timepoint { get; set; }

        /**************************************************************/
        /// <summary>
        /// Numeric time value extracted from DoseRegimen (PK) or Timepoint (BMD).
        /// </summary>
        public double? Time { get; set; }

        /**************************************************************/
        /// <summary>
        /// Unit for Time: "days", "weeks", "months", "hours".
        /// </summary>
        public string? TimeUnit { get; set; }

        #endregion Observation Context Properties

        #region Decomposed Value Properties

        /**************************************************************/
        /// <summary>
        /// Original cell text after HTML stripping — always preserved for audit.
        /// </summary>
        public string? RawValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// Main numeric value: the percentage, mean, hazard ratio, or risk difference.
        /// </summary>
        public double? PrimaryValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// What PrimaryValue represents. Values: Percentage, Mean, Median, Numeric,
        /// RelativeRiskReduction, RiskDifference, Ratio, MeanPercentChange, PValue,
        /// SampleSize, CodedExclusion, Text.
        /// </summary>
        public string? PrimaryValueType { get; set; }

        /**************************************************************/
        /// <summary>
        /// Companion value: n-count when primary is %, SD when primary is mean.
        /// </summary>
        public double? SecondaryValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// What SecondaryValue represents. Values: Count, SD, CV_Percent, SE.
        /// </summary>
        public string? SecondaryValueType { get; set; }

        /**************************************************************/
        /// <summary>
        /// Lower limit of CI or range.
        /// </summary>
        public double? LowerBound { get; set; }

        /**************************************************************/
        /// <summary>
        /// Upper limit of CI or range.
        /// </summary>
        public double? UpperBound { get; set; }

        /**************************************************************/
        /// <summary>
        /// Type of bounds. Values: 95CI, 90CI, Range, IQR.
        /// </summary>
        public string? BoundType { get; set; }

        /**************************************************************/
        /// <summary>
        /// P-value when present.
        /// </summary>
        public double? PValue { get; set; }

        /**************************************************************/
        /// <summary>
        /// Unit of measurement: "mcg/mL", "hours", "%", "ratio", "percentage points".
        /// </summary>
        public string? Unit { get; set; }

        #endregion Decomposed Value Properties

        #region Validation Properties

        /**************************************************************/
        /// <summary>
        /// Parse confidence score 0.0–1.0.
        /// </summary>
        public double? ParseConfidence { get; set; }

        /**************************************************************/
        /// <summary>
        /// Which regex pattern matched: n_pct, frac_pct, rr_ci, diff_ci, etc.
        /// </summary>
        public string? ParseRule { get; set; }

        /**************************************************************/
        /// <summary>
        /// Footnote markers extracted from sup tags: "b, c", "a".
        /// </summary>
        public string? FootnoteMarkers { get; set; }

        /**************************************************************/
        /// <summary>
        /// Full footnote text from Footer rows, semicolon-delimited.
        /// </summary>
        public string? FootnoteText { get; set; }

        /**************************************************************/
        /// <summary>
        /// Automated check results: PCT_CHECK:PASS, PCT_CHECK:WARN:16.2, POP_MISMATCH.
        /// </summary>
        public string? ValidationFlags { get; set; }

        #endregion Validation Properties
    }
}
