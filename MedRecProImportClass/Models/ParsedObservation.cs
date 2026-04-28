namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Intermediate 38-column DTO representing one atomic observation from a parsed SPL table
    /// in Stage 3 of the SPL Table Normalization pipeline. Parsers return
    /// <c>List&lt;ParsedObservation&gt;</c> which the orchestrator maps to the
    /// <c>FlattenedStandardizedTable</c> EF entity for bulk database insert.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Stage 2 (ReconstructedTable) → Parser → **ParsedObservation** → Orchestrator → DB
    ///
    /// ## Schema Groups
    /// - **Provenance (8)**: Traces every value back to the exact source cell
    /// - **Classification (4)**: Routes queries and groups comparable data
    /// - **Observation Context (11)**: Describes what was measured, in whom, under what conditions
    /// - **Decomposed Values (10)**: Typed, queryable components of the raw cell text
    /// - **Validation (5)**: Automated quality signals and confidence scores
    ///
    /// ## Unpivot Pattern
    /// Each source data cell becomes one ParsedObservation. A 5-column AE table with
    /// 20 data rows produces ~80 observations (20 rows × 4 arm columns).
    /// </remarks>
    /// <seealso cref="ParsedValue"/>
    /// <seealso cref="ArmDefinition"/>
    /// <seealso cref="ReconstructedTable"/>
    public class ParsedObservation
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
        /// Plus-delimited active ingredient UNIIs. Outermost anomaly model composite key segment.
        /// </summary>
        /// <seealso cref="LabelView.ActiveIngredientView"/>
        public string? UNII { get; set; }

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
        /// Table category. Values: PK, ADVERSE_EVENT, EFFICACY, DRUG_INTERACTION, SKIP.
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
        /// Populated from SOC divider rows.
        /// </summary>
        public string? ParameterCategory { get; set; }

        /**************************************************************/
        /// <summary>
        /// Sub-endpoint hierarchy: "Components of endpoint".
        /// </summary>
        public string? ParameterSubtype { get; set; }

        /**************************************************************/
        /// <summary>
        /// Treatment arm name: "EVISTA", "Placebo", "Fluconazole", or "Comparison"
        /// for between-arm statistics.
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
        /// Dose regimen for PK/dosing tables (e.g., "50 mg oral (once daily x 7 days)").
        /// </summary>
        public string? DoseRegimen { get; set; }

        /**************************************************************/
        /// <summary>
        /// Numeric dose value extracted from <see cref="DoseRegimen"/> or <see cref="TreatmentArm"/>
        /// via <see cref="DoseExtractor"/>. 0.0 for placebo arms. Null when no dose is recoverable.
        /// </summary>
        /// <seealso cref="DoseUnit"/>
        /// <seealso cref="DoseRegimen"/>
        public decimal? Dose { get; set; }

        /**************************************************************/
        /// <summary>
        /// Measurement unit for <see cref="Dose"/>, normalized (e.g., "mg", "mg/d", "mg/kg", "mcg/d").
        /// Inherited from comparator arms for placebo. Null when no dose is recoverable.
        /// </summary>
        /// <seealso cref="Dose"/>
        /// <seealso cref="DoseRegimen"/>
        public string? DoseUnit { get; set; }

        /**************************************************************/
        /// <summary>
        /// Auto-detected population: "Adult Healthy Volunteers", "Postmenopausal Women",
        /// "Premature Infants". Extracted from Caption/SectionTitle with fuzzy validation.
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
        /// Examples: 7 (from "x 7 days"), 12 (from "12 Months"), null (single dose).
        /// </summary>
        public double? Time { get; set; }

        /**************************************************************/
        /// <summary>
        /// Unit for <see cref="Time"/>. Values: "days", "weeks", "months", "hours".
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
        /// P-value when present. Applies to all arm rows from the same source row.
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
        /// Parse confidence score 0.0–1.0. High (≥0.9): deterministic parse.
        /// Medium (0.5–0.9): plain numbers or ranges. Low (&lt;0.5): unparsed text.
        /// </summary>
        public double? ParseConfidence { get; set; }

        /**************************************************************/
        /// <summary>
        /// Adjusted confidence after Stage 4 validation penalties. Starts as
        /// <see cref="ParseConfidence"/> and is reduced by multipliers for missing required
        /// fields, unexpected value types, time pairing issues, etc. Clamped to [0.0, 1.0].
        /// </summary>
        /// <seealso cref="ParseConfidence"/>
        public double? AdjustedConfidence { get; set; }

        /**************************************************************/
        /// <summary>
        /// Which regex pattern matched: n_pct, frac_pct, rr_ci, diff_ci,
        /// value_cv, plain_number, letter_code, empty_or_na, text_descriptive.
        /// </summary>
        public string? ParseRule { get; set; }

        /**************************************************************/
        /// <summary>
        /// Footnote markers extracted from &lt;sup&gt; tags: "b, c", "a".
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
