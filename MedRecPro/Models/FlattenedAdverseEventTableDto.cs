namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// API consumption DTO for Stage 5 (Phase 2) AdverseEvent Denormalization output.
    /// Mirrors the columns of <see cref="LabelView.FlattenedAdverseEventTable"/> without
    /// EF Core attributes for clean serialization. The six log-scale columns are
    /// included even though they are server-computed because consumers visualize on
    /// log-scale axes.
    /// </summary>
    /// <remarks>
    /// ## Schema Groups
    /// - **Source linkage / projection (10)**: copied verbatim from the source AE row
    ///   in <see cref="FlattenedStandardizedTableDto"/>; never derived
    /// - **Comparator metadata (4)**: comparator arm + Document-level trial-design flag
    /// - **Derived event counts (2)**: a / c in the 2×2 (raw, audit-only)
    /// - **Risk statistics (6)**: RR, DNRR, ±CI bounds (linear scale)
    /// - **Log-scale companions (6)**: server-computed log of each statistic, NULL safe
    /// - **Calculation provenance (2)**: method + semicolon-delimited flag taxonomy
    ///
    /// ## Calculation Method
    /// - RR/CI: Katz log-method with Haldane-Anscombe continuity correction (a+0.5,
    ///   c+0.5, n1+1, n2+1 applied to point estimate AND CI when zero cells exist).
    ///   Raw event counts are still surfaced in <see cref="EventsTreatment"/> /
    ///   <see cref="EventsComparator"/> for audit.
    /// - DNRR: log-linear extrapolation with intra-study reference dose
    ///   (D_ref = MIN(Dose) WHERE Dose &gt; 0 within the study group).
    /// </remarks>
    /// <seealso cref="LabelView.FlattenedAdverseEventTable"/>
    /// <seealso cref="FlattenedStandardizedTableDto"/>
    public class FlattenedAdverseEventTableDto
    {
        #region Source Linkage Properties

        /**************************************************************/
        /// <summary>
        /// Provenance link to the source row in tmp_FlattenedStandardizedTable.
        /// </summary>
        public int FlattenedStandardizedTableId { get; set; }

        /**************************************************************/
        /// <summary>Source SPL document identifier.</summary>
        public Guid? DocumentGUID { get; set; }

        /**************************************************************/
        /// <summary>Plus-delimited active-ingredient UNIIs (document-level).</summary>
        public string? UNII { get; set; }

        /**************************************************************/
        /// <summary>AE term (e.g., "Nausea").</summary>
        public string? ParameterName { get; set; }

        /**************************************************************/
        /// <summary>SOC group (e.g., "Nervous System").</summary>
        public string? ParameterCategory { get; set; }

        /**************************************************************/
        /// <summary>Sample size for the treatment arm.</summary>
        public int? ArmN { get; set; }

        /**************************************************************/
        /// <summary>Numeric dose for the treatment arm.</summary>
        public decimal? Dose { get; set; }

        /**************************************************************/
        /// <summary>Normalized dose unit.</summary>
        public string? DoseUnit { get; set; }

        /**************************************************************/
        /// <summary>Source PrimaryValue — never derived.</summary>
        public double? PrimaryValue { get; set; }

        /**************************************************************/
        /// <summary>Source PrimaryValueType — never derived.</summary>
        public string? PrimaryValueType { get; set; }

        #endregion Source Linkage Properties

        #region Comparator Metadata Properties

        /**************************************************************/
        /// <summary>Treatment arm name.</summary>
        public string? TreatmentArm { get; set; }

        /**************************************************************/
        /// <summary>
        /// Comparator arm name selected by the comparator cascade
        /// (placebo &gt; lowest non-zero dose &gt; single-arm fallback). NULL for single-arm groups.
        /// </summary>
        public string? ComparatorArm { get; set; }

        /**************************************************************/
        /// <summary>Comparator arm sample size. NULL when no comparator was selected.</summary>
        public int? ComparatorN { get; set; }

        /**************************************************************/
        /// <summary>
        /// Document-level trial-design flag. <c>true</c> only when the document has
        /// placebo arm(s) plus drug arm(s) of a single drug. Same value on every row
        /// of a given DocumentGUID.
        /// </summary>
        public bool IsPlaceboControlled { get; set; }

        #endregion Comparator Metadata Properties

        #region Derived Event Counts (audit)

        /**************************************************************/
        /// <summary>Raw derived events for the treatment arm (a in 2×2, audit only).</summary>
        public double? EventsTreatment { get; set; }

        /**************************************************************/
        /// <summary>Raw derived events for the comparator arm (c in 2×2, audit only).</summary>
        public double? EventsComparator { get; set; }

        #endregion Derived Event Counts (audit)

        #region Risk Statistics Properties

        /**************************************************************/
        /// <summary>Relative Risk point estimate (Katz log-method).</summary>
        public double? RR { get; set; }

        /**************************************************************/
        /// <summary>Dose-Normalized Relative Risk (log-linear with intra-study D_ref).</summary>
        public double? DNRR { get; set; }

        /**************************************************************/
        /// <summary>Lower bound of 95% CI for RR.</summary>
        public double? RRLowerBound { get; set; }

        /**************************************************************/
        /// <summary>Upper bound of 95% CI for RR.</summary>
        public double? RRUpperBound { get; set; }

        /**************************************************************/
        /// <summary>Lower bound of 95% CI for DNRR.</summary>
        public double? DNRRLowerBound { get; set; }

        /**************************************************************/
        /// <summary>Upper bound of 95% CI for DNRR.</summary>
        public double? DNRRUpperBound { get; set; }

        #endregion Risk Statistics Properties

        #region Log-scale Companions

        /**************************************************************/
        /// <summary>Server-computed natural log of <see cref="RR"/>, NULL when RR ≤ 0.</summary>
        public double? LogRR { get; set; }

        /**************************************************************/
        /// <summary>Server-computed natural log of <see cref="RRLowerBound"/>.</summary>
        public double? LogRRLowerBound { get; set; }

        /**************************************************************/
        /// <summary>Server-computed natural log of <see cref="RRUpperBound"/>.</summary>
        public double? LogRRUpperBound { get; set; }

        /**************************************************************/
        /// <summary>Server-computed natural log of <see cref="DNRR"/>.</summary>
        public double? LogDNRR { get; set; }

        /**************************************************************/
        /// <summary>Server-computed natural log of <see cref="DNRRLowerBound"/>.</summary>
        public double? LogDNRRLowerBound { get; set; }

        /**************************************************************/
        /// <summary>Server-computed natural log of <see cref="DNRRUpperBound"/>.</summary>
        public double? LogDNRRUpperBound { get; set; }

        #endregion Log-scale Companions

        #region Calculation Provenance Properties

        /**************************************************************/
        /// <summary>Statistical method used. Currently always "KATZ_LOG" when stats are populated.</summary>
        public string? CalculationMethod { get; set; }

        /**************************************************************/
        /// <summary>Semicolon-delimited diagnostic flags. See README for the full taxonomy.</summary>
        public string? CalculationFlags { get; set; }

        #endregion Calculation Provenance Properties
    }
}
