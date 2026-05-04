using System.Text.RegularExpressions;

namespace MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening
{
    /**************************************************************/
    /// <summary>
    /// Pure-function statistical utility for computing Relative Risk (RR), Dose-Normalized
    /// Relative Risk (DNRR), 95% confidence-interval bounds, derived event counts, and
    /// per-table trial-design classification used by Stage 5 (Phase 2) AdverseEvent
    /// denormalization.
    /// </summary>
    /// <remarks>
    /// ## Method Family
    /// - <see cref="Compute"/>: Katz log-method RR + 95% CI with Haldane-Anscombe
    ///   continuity correction applied to BOTH the point estimate and the CI when
    ///   <c>a == 0</c> or <c>c == 0</c>. Raw event counts are returned alongside the
    ///   adjusted result so callers can persist the audit trail unchanged.
    /// - <see cref="DeriveEventCount"/>: PrimaryValue + PrimaryValueType + ArmN →
    ///   derived event count, with hard guards for invalid inputs.
    /// - <see cref="ComputeDnrr"/>: log-linear DNRR + CI with intra-study reference
    ///   dose; skips with diagnostic flag for placebo rows, reference-dose rows, missing
    ///   dose ranges, or dose-unit mismatches.
    /// - <see cref="IsPlaceboArm"/>: classifies a single arm as placebo-equivalent.
    /// - <see cref="ClassifyTrialDesign"/>: conservative per-table trial-design
    ///   classifier; ambiguous designs fall back to <c>AMBIGUOUS_TRIAL_DESIGN</c>. The
    ///   classification is now diagnostic only (surfaced via <c>CalculationFlags</c>);
    ///   the persisted <c>IsPlaceboControlled</c> column is comparator-driven, set per-row
    ///   in <see cref="AdverseEventDenormalizationService"/>.
    ///
    /// ## Stateless / Deterministic
    /// All methods are static and side-effect free. Inputs are doubles/decimals/strings;
    /// outputs are immutable records. Suitable for direct unit testing without DI.
    /// </remarks>
    /// <seealso cref="AdverseEventDenormalizationService"/>
    public static class RelativeRiskCalculator
    {
        #region Constants

        /**************************************************************/
        /// <summary>
        /// Z-value for 95% confidence interval (two-sided normal approximation).
        /// </summary>
        private const double Z95 = 1.959963984540054; // standard normal 0.975 quantile

        /**************************************************************/
        /// <summary>Regex identifying placebo / sham / vehicle arm names (case-insensitive).</summary>
        private static readonly Regex PlaceboPattern = new(
            @"placebo|sham|vehicle",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>Regex stripping numeric dose tokens (e.g., "50 mg", "5 mg/kg", "25 IU").</summary>
        private static readonly Regex DoseTokenPattern = new(
            @"\b\d+(\.\d+)?\s*(mg|mcg|µg|ug|g|kg|iu|ml|l|units?|%)(\s*/\s*(d|day|kg|m2|hr|h|wk))?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>Regex stripping common regimen tokens (qd, bid, daily, etc.).</summary>
        private static readonly Regex RegimenTokenPattern = new(
            @"\b(qd|bid|tid|qid|po|iv|sc|im|once|twice|daily|weekly|q\dh)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>Regex collapsing runs of whitespace.</summary>
        private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

        #endregion Constants

        #region Result Types

        /**************************************************************/
        /// <summary>
        /// Result of <see cref="Compute"/>. <see cref="Rr"/> / <see cref="RrLower"/> /
        /// <see cref="RrUpper"/> are NULL when prerequisites fail (see <see cref="Flags"/>).
        /// <see cref="EventsTreatmentRaw"/> and <see cref="EventsComparatorRaw"/> always
        /// reflect the un-adjusted inputs so callers can persist the audit trail even
        /// when continuity correction was applied.
        /// </summary>
        /// <param name="Rr">Relative Risk point estimate (NULL on guard failure).</param>
        /// <param name="RrLower">Lower bound of 95% CI for RR.</param>
        /// <param name="RrUpper">Upper bound of 95% CI for RR.</param>
        /// <param name="EventsTreatmentRaw">Raw a (events in treatment arm); preserved for audit.</param>
        /// <param name="EventsComparatorRaw">Raw c (events in comparator arm); preserved for audit.</param>
        /// <param name="Flags">Diagnostic flag (or NULL when computation succeeded clean).</param>
        public sealed record RrResult(
            double? Rr,
            double? RrLower,
            double? RrUpper,
            double? EventsTreatmentRaw,
            double? EventsComparatorRaw,
            string? Flags);

        /**************************************************************/
        /// <summary>
        /// Result of <see cref="ComputeDnrr"/>. <see cref="Dnrr"/>/bounds are NULL when DNRR
        /// is not applicable (see <see cref="Flags"/>).
        /// </summary>
        /// <param name="Dnrr">Dose-normalized RR point estimate.</param>
        /// <param name="DnrrLower">Lower bound of 95% CI for DNRR.</param>
        /// <param name="DnrrUpper">Upper bound of 95% CI for DNRR.</param>
        /// <param name="Flags">Diagnostic flag explaining a NULL result; null when DNRR was computed cleanly.</param>
        public sealed record DnrrResult(
            double? Dnrr,
            double? DnrrLower,
            double? DnrrUpper,
            string? Flags);

        /**************************************************************/
        /// <summary>
        /// Lightweight description of a treatment arm used by
        /// <see cref="ClassifyTrialDesign"/>. <see cref="Dose"/> may be null/zero (placebo).
        /// </summary>
        /// <param name="Name">Treatment arm name as printed in the source label.</param>
        /// <param name="Dose">Numeric dose extracted by Stage 3 (decimal — DECIMAL(18,6) in DDL).</param>
        /// <param name="DoseUnit">Normalized dose unit (e.g. "mg", "mg/kg").</param>
        public sealed record ArmInfo(string? Name, decimal? Dose, string? DoseUnit);

        /**************************************************************/
        /// <summary>
        /// Categorical kinds emitted by <see cref="ClassifyTrialDesign"/>. Diagnostic
        /// only — these no longer drive the persisted <c>IsPlaceboControlled</c> bit
        /// (which is row-level, set from the comparator selection in
        /// <see cref="AdverseEventDenormalizationService"/>). Only
        /// <see cref="PLACEBO_ONLY"/> and <see cref="STEPPED_DOSE_PLUS_PLACEBO"/> set
        /// the <see cref="TrialDesignClassification.IsPlaceboControlled"/> field on the
        /// returned record to <c>true</c>; that field is consumed only for internal
        /// diagnostic purposes.
        /// </summary>
        public enum TrialDesignKind
        {
            /// <summary>Document has only one distinct treatment arm.</summary>
            SINGLE_ARM,
            /// <summary>Drug + placebo, with exactly one drug arm.</summary>
            PLACEBO_ONLY,
            /// <summary>Multiple doses of one drug + placebo (same arm-name root).</summary>
            STEPPED_DOSE_PLUS_PLACEBO,
            /// <summary>Multiple doses of one drug, no placebo.</summary>
            STEPPED_DOSE_MONOTHERAPY,
            /// <summary>Two or more distinct drugs, no placebo.</summary>
            ACTIVE_ONLY,
            /// <summary>Drug + active comparator + placebo (distinct arm-name roots, with placebo).</summary>
            PLACEBO_PLUS_ACTIVE,
            /// <summary>Cannot classify confidently (e.g., arm names that won't reduce to comparable roots).</summary>
            AMBIGUOUS
        }

        /**************************************************************/
        /// <summary>
        /// Result of <see cref="ClassifyTrialDesign"/>. <see cref="Kind"/> and
        /// <see cref="Flag"/> drive the diagnostic <c>AMBIGUOUS_TRIAL_DESIGN</c>
        /// CalculationFlags entry. <see cref="IsPlaceboControlled"/> on this record is
        /// retained for backward compatibility and unit-test introspection but is
        /// **no longer written to the persisted DB column** — the
        /// <c>tmp_FlattenedAdverseEventTable.IsPlaceboControlled</c> bit is set per-row
        /// from the comparator-selection flag in
        /// <see cref="AdverseEventDenormalizationService"/>.
        /// </summary>
        public sealed record TrialDesignClassification(
            bool IsPlaceboControlled,
            TrialDesignKind Kind,
            string? Flag);

        #endregion Result Types

        #region Public API

        /**************************************************************/
        /// <summary>
        /// Computes the Katz log-method Relative Risk and 95% CI for a 2x2 contingency
        /// table (a/n1 vs c/n2). When either event count is zero, applies the
        /// Haldane-Anscombe continuity correction to BOTH the point estimate and the CI
        /// (a' = a + 0.5, c' = c + 0.5, n1' = n1 + 1, n2' = n2 + 1) and emits the
        /// <c>ZERO_CELL_CORRECTED</c> flag. Raw a / c are returned unchanged for audit.
        /// </summary>
        /// <param name="eventsTreatmentRaw">Events in treatment arm (a). Must be ≥ 0 and ≤ <paramref name="armN"/>.</param>
        /// <param name="armN">Treatment arm sample size (n1). Must be &gt; 0.</param>
        /// <param name="eventsComparatorRaw">Events in comparator arm (c). Must be ≥ 0 and ≤ <paramref name="comparatorN"/>.</param>
        /// <param name="comparatorN">Comparator arm sample size (n2). Must be &gt; 0.</param>
        /// <returns>
        /// <see cref="RrResult"/> with computed RR + CI when guards pass, or all-null
        /// statistics + a single guard flag when any precondition fails. Raw event
        /// counts always pass through.
        /// </returns>
        /// <example>
        /// <code>
        /// // 14% incidence in n=188 vs 8% in n=173:
        /// var r = RelativeRiskCalculator.Compute(
        ///     eventsTreatmentRaw: 188 * 14.0 / 100,
        ///     armN: 188,
        ///     eventsComparatorRaw: 173 * 8.0 / 100,
        ///     comparatorN: 173);
        /// // r.Rr ≈ 1.75, r.RrLower &lt; 1.75 &lt; r.RrUpper
        /// </code>
        /// </example>
        /// <seealso cref="DeriveEventCount"/>
        public static RrResult Compute(
            double? eventsTreatmentRaw,
            int? armN,
            double? eventsComparatorRaw,
            int? comparatorN)
        {
            #region implementation

            // Hard guards — return raw counts so the audit trail is always populated
            if (armN is null || armN <= 0)
                return new RrResult(null, null, null, eventsTreatmentRaw, eventsComparatorRaw, "NO_ARMN");

            if (comparatorN is null || comparatorN <= 0)
                return new RrResult(null, null, null, eventsTreatmentRaw, eventsComparatorRaw, "NO_COMPARATOR_N");

            if (eventsTreatmentRaw is null || eventsComparatorRaw is null)
                return new RrResult(null, null, null, eventsTreatmentRaw, eventsComparatorRaw, "INVALID_EVENT_COUNT");

            if (eventsTreatmentRaw < 0 || eventsComparatorRaw < 0)
                return new RrResult(null, null, null, eventsTreatmentRaw, eventsComparatorRaw, "INVALID_EVENT_COUNT");

            if (eventsTreatmentRaw > armN || eventsComparatorRaw > comparatorN)
                return new RrResult(null, null, null, eventsTreatmentRaw, eventsComparatorRaw, "EVENTS_EXCEED_ARMN");

            // Adjusted locals for Haldane-Anscombe — applied to point estimate AND CI
            double a = eventsTreatmentRaw.Value;
            double c = eventsComparatorRaw.Value;
            double n1 = armN.Value;
            double n2 = comparatorN.Value;

            string? flag = null;
            if (a == 0d || c == 0d)
            {
                a += 0.5;
                c += 0.5;
                n1 += 1d;
                n2 += 1d;
                flag = "ZERO_CELL_CORRECTED";
            }

            double rr = (a / n1) / (c / n2);
            double seLog = Math.Sqrt((1.0 / a) - (1.0 / n1) + (1.0 / c) - (1.0 / n2));
            double lnRr = Math.Log(rr);
            double rrLower = Math.Exp(lnRr - (Z95 * seLog));
            double rrUpper = Math.Exp(lnRr + (Z95 * seLog));

            return new RrResult(rr, rrLower, rrUpper, eventsTreatmentRaw, eventsComparatorRaw, flag);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts a source PrimaryValue + PrimaryValueType + ArmN into a derived event
        /// count usable in <see cref="Compute"/>. Only the canonical types <c>Percentage</c>
        /// and <c>Count</c> (per <c>ColumnStandardizationService</c>) yield a number; bare
        /// <c>Numeric</c> is treated as uncomparable because it could be a mean, ratio, or
        /// count and the upstream pipeline did not commit to count semantics.
        /// </summary>
        /// <param name="primaryValue">Source <c>PrimaryValue</c>.</param>
        /// <param name="primaryValueType">Source <c>PrimaryValueType</c> (case-insensitive).</param>
        /// <param name="armN">Source <c>ArmN</c> (required for Percentage path).</param>
        /// <returns>
        /// <c>(events, null)</c> when conversion succeeded; <c>(null, "FLAG")</c> when a
        /// guard tripped (<c>NO_ARMN</c>, <c>PERCENT_OUT_OF_RANGE</c>,
        /// <c>INVALID_EVENT_COUNT</c>, <c>UNCOMPARABLE_VALUE_TYPE</c>).
        /// </returns>
        /// <seealso cref="Compute"/>
        public static (double? events, string? flag) DeriveEventCount(
            double? primaryValue,
            string? primaryValueType,
            int? armN)
        {
            #region implementation

            if (primaryValue is null)
                return (null, "INVALID_EVENT_COUNT");

            if (primaryValue < 0)
                return (null, "INVALID_EVENT_COUNT");

            var pvt = primaryValueType?.Trim();

            // Percentage: events = ArmN * pv / 100
            if (string.Equals(pvt, "Percentage", StringComparison.OrdinalIgnoreCase))
            {
                if (primaryValue > 100d)
                    return (null, "PERCENT_OUT_OF_RANGE");

                if (armN is null || armN <= 0)
                    return (null, "NO_ARMN");

                return (armN.Value * primaryValue.Value / 100.0, null);
            }

            // Canonical Count (per ColumnStandardizationService) — direct passthrough
            if (string.Equals(pvt, "Count", StringComparison.OrdinalIgnoreCase))
            {
                return (primaryValue, null);
            }

            // All other types (Numeric, Mean, Median, Ratio, etc.) cannot be safely
            // interpreted as event counts.
            return (null, "UNCOMPARABLE_VALUE_TYPE");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes Dose-Normalized Relative Risk and 95% CI using the log-linear formula
        /// <c>logDNRR = ln(RR) / ln(rowDose / referenceDose)</c>. Skips with a diagnostic
        /// flag when the row is at the reference dose, dose units do not match, the
        /// reference dose is undefined, or the row is a placebo / no-dose row.
        /// </summary>
        /// <param name="rr">Source <see cref="RrResult"/> from <see cref="Compute"/>.</param>
        /// <param name="rowDose">Numeric dose for the row being computed.</param>
        /// <param name="rowDoseUnit">Dose unit for the row being computed.</param>
        /// <param name="referenceDose">Intra-study D_ref (MIN(Dose) WHERE Dose &gt; 0).</param>
        /// <param name="referenceDoseUnit">Dose unit at the reference dose.</param>
        /// <returns>
        /// <see cref="DnrrResult"/> with computed DNRR + CI, or all-null + diagnostic
        /// flag (<c>IS_REFERENCE_DOSE</c>, <c>NO_DOSE_RANGE</c>, <c>DOSE_UNIT_MISMATCH</c>).
        /// When <paramref name="rr"/>.<see cref="RrResult.Rr"/> is null, propagates with
        /// no flag (the cause is already in the upstream <see cref="RrResult"/>).
        /// </returns>
        /// <seealso cref="Compute"/>
        public static DnrrResult ComputeDnrr(
            RrResult rr,
            decimal? rowDose,
            string? rowDoseUnit,
            decimal? referenceDose,
            string? referenceDoseUnit)
        {
            #region implementation

            // Propagate NULL RR without adding a flag (upstream already explained it)
            if (rr.Rr is null || rr.Rr <= 0)
                return new DnrrResult(null, null, null, null);

            // Row at placebo / unknown dose → not eligible for DNRR (no flag — handled
            // by the placebo comparator path; placebo rows shouldn't reach here normally)
            if (rowDose is null || rowDose <= 0m)
                return new DnrrResult(null, null, null, null);

            // No dose range available in the group
            if (referenceDose is null || referenceDose <= 0m)
                return new DnrrResult(null, null, null, "NO_DOSE_RANGE");

            // Row is at the reference dose — denominator ln(1) = 0 is undefined
            if (rowDose == referenceDose)
                return new DnrrResult(null, null, null, "IS_REFERENCE_DOSE");

            // Dose units must match for log-linear extrapolation to be meaningful
            // (5 mg vs 0.1 mg/kg → meaningless ratio)
            var rowUnitNorm = (rowDoseUnit ?? string.Empty).Trim();
            var refUnitNorm = (referenceDoseUnit ?? string.Empty).Trim();
            if (!string.Equals(rowUnitNorm, refUnitNorm, StringComparison.OrdinalIgnoreCase))
                return new DnrrResult(null, null, null, "DOSE_UNIT_MISMATCH");

            double doseRatio = (double)(rowDose.Value / referenceDose.Value);
            if (doseRatio <= 0d)
                return new DnrrResult(null, null, null, "NO_DOSE_RANGE");

            double lnDoseRatio = Math.Log(doseRatio);
            // Defensive: ln(1) = 0 already excluded above by the dose==reference check,
            // but float equality is brittle for decimal→double round-trips
            if (lnDoseRatio == 0d)
                return new DnrrResult(null, null, null, "IS_REFERENCE_DOSE");

            double dnrr = Math.Exp(Math.Log(rr.Rr.Value) / lnDoseRatio);

            double? dnrrLower = null;
            double? dnrrUpper = null;
            if (rr.RrLower is double rl && rl > 0d)
                dnrrLower = Math.Exp(Math.Log(rl) / lnDoseRatio);
            if (rr.RrUpper is double ru && ru > 0d)
                dnrrUpper = Math.Exp(Math.Log(ru) / lnDoseRatio);

            // When lnDoseRatio < 0 (rowDose < referenceDose, possible if D_ref came from
            // outside the row set), the lower/upper bounds invert. Sort defensively.
            if (dnrrLower is double dl && dnrrUpper is double du && dl > du)
            {
                (dnrrLower, dnrrUpper) = (du, dl);
            }

            return new DnrrResult(dnrr, dnrrLower, dnrrUpper, null);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when the arm is placebo-equivalent: name matches
        /// <c>placebo|sham|vehicle</c> (case-insensitive), OR <paramref name="dose"/>
        /// is exactly zero. Whitespace-only or null names default to non-placebo unless
        /// dose==0.
        /// </summary>
        /// <param name="treatmentArm">Arm name as printed.</param>
        /// <param name="dose">Numeric dose extracted by Stage 3.</param>
        /// <returns>True when the arm should be treated as placebo for comparator selection.</returns>
        public static bool IsPlaceboArm(string? treatmentArm, decimal? dose)
        {
            #region implementation

            if (dose == 0m)
                return true;

            if (string.IsNullOrWhiteSpace(treatmentArm))
                return false;

            return PlaceboPattern.IsMatch(treatmentArm);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Conservative per-table trial-design classifier. Sets the returned record's
        /// <see cref="TrialDesignClassification.IsPlaceboControlled"/> to <c>true</c>
        /// only when the table has placebo arm(s) plus drug arm(s) of a single drug
        /// (one distinct arm-name root after dose-token stripping). Ambiguous designs
        /// default to <c>false</c> with the <c>AMBIGUOUS_TRIAL_DESIGN</c> flag — the
        /// classifier cannot reliably distinguish "drug + active comparator" from
        /// "stepped dose of one drug" without arm-level UNII, so the conservative read
        /// is preferred.
        /// </summary>
        /// <remarks>
        /// As of the row-level <c>IsPlaceboControlled</c> refactor, the result drives
        /// only the <c>AMBIGUOUS_TRIAL_DESIGN</c> diagnostic flag in
        /// <c>CalculationFlags</c>. The persisted DB bit is set per-row from the
        /// comparator selection in <see cref="AdverseEventDenormalizationService"/>.
        /// Caller groups arms per (DocumentGUID, TextTableID) so the diagnostic stays
        /// scoped to the row's source table rather than contaminating the whole document.
        /// </remarks>
        /// <param name="distinctDocumentArms">
        /// Distinct treatment arms across all AE rows in one (DocumentGUID, TextTableID)
        /// study table (caller deduplicates). Each arm contributes name + dose + dose-unit.
        /// Parameter name retained for source compatibility; semantically it is now per-table.
        /// </param>
        /// <returns>Classification + diagnostic flag.</returns>
        /// <seealso cref="IsPlaceboArm"/>
        public static TrialDesignClassification ClassifyTrialDesign(
            IReadOnlyCollection<ArmInfo> distinctDocumentArms)
        {
            #region implementation

            if (distinctDocumentArms is null || distinctDocumentArms.Count == 0)
                return new TrialDesignClassification(false, TrialDesignKind.SINGLE_ARM, null);

            if (distinctDocumentArms.Count == 1)
                return new TrialDesignClassification(false, TrialDesignKind.SINGLE_ARM, null);

            var placeboArms = distinctDocumentArms
                .Where(a => IsPlaceboArm(a.Name, a.Dose))
                .ToList();

            var drugArms = distinctDocumentArms
                .Where(a => !IsPlaceboArm(a.Name, a.Dose))
                .ToList();

            // Edge case: only placebo arms (degenerate) — treat as single-arm
            if (drugArms.Count == 0)
                return new TrialDesignClassification(false, TrialDesignKind.SINGLE_ARM, null);

            var drugRoots = drugArms
                .Select(a => extractArmRoot(a.Name))
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool ambiguousRoots = drugRoots.Count == 0;
            bool singleRoot = drugRoots.Count == 1;

            if (placeboArms.Count > 0)
            {
                // Placebo present
                if (ambiguousRoots)
                    return new TrialDesignClassification(false, TrialDesignKind.AMBIGUOUS, "AMBIGUOUS_TRIAL_DESIGN");

                if (drugArms.Count == 1)
                    return new TrialDesignClassification(true, TrialDesignKind.PLACEBO_ONLY, null);

                if (singleRoot)
                    return new TrialDesignClassification(true, TrialDesignKind.STEPPED_DOSE_PLUS_PLACEBO, null);

                // Multiple distinct roots + placebo → drug + active comparator + placebo
                return new TrialDesignClassification(false, TrialDesignKind.PLACEBO_PLUS_ACTIVE, null);
            }

            // No placebo
            if (ambiguousRoots)
                return new TrialDesignClassification(false, TrialDesignKind.AMBIGUOUS, "AMBIGUOUS_TRIAL_DESIGN");

            if (singleRoot)
                return new TrialDesignClassification(false, TrialDesignKind.STEPPED_DOSE_MONOTHERAPY, null);

            return new TrialDesignClassification(false, TrialDesignKind.ACTIVE_ONLY, null);

            #endregion
        }

        #endregion Public API

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Reduces an arm name to its drug-name root by stripping numeric dose tokens
        /// (e.g. "50 mg", "5 mg/kg", "25 IU") and common regimen tokens (qd, bid,
        /// daily, etc.), then collapsing whitespace and lowercasing. Returns null when
        /// the input is null/empty or strips down to nothing.
        /// </summary>
        /// <param name="armName">Arm name as printed in the source label.</param>
        /// <returns>Lowercased root, or null when no usable root can be extracted.</returns>
        /// <example>
        /// <code>
        /// extractArmRoot("Drug A 50 mg")      → "drug a"
        /// extractArmRoot("Drug A 100 mg/d")   → "drug a"
        /// extractArmRoot("Active Comparator") → "active comparator"
        /// extractArmRoot("50 mg")             → null  (only a dose token)
        /// </code>
        /// </example>
        private static string? extractArmRoot(string? armName)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(armName))
                return null;

            var s = armName.ToLowerInvariant().Trim();
            s = DoseTokenPattern.Replace(s, " ");
            s = RegimenTokenPattern.Replace(s, " ");
            s = WhitespacePattern.Replace(s, " ").Trim();

            return string.IsNullOrEmpty(s) ? null : s;

            #endregion
        }

        #endregion Private Helpers
    }
}
