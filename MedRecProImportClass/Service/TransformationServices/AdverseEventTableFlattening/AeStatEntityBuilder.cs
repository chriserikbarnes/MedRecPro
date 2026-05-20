using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening
{
    /**************************************************************/
    /// <summary>
    /// Builds denormalized adverse-event rows and derived RR/DNRR statistics.
    /// </summary>
    /// <remarks>
    /// This helper owns the source projection, comparator metadata, validation flags,
    /// event-count derivation, Katz RR calculation, and dose-normalized RR calculation
    /// that were previously embedded inside the service batch loop.
    /// </remarks>
    /// <seealso cref="RelativeRiskCalculator"/>
    /// <seealso cref="ComparatorSelector"/>
    internal static class AeStatEntityBuilder
    {
        /**************************************************************/
        /// <summary>
        /// Builds one output entity for a non-comparator source row.
        /// </summary>
        /// <remarks>
        /// The persisted IsPlaceboControlled bit is set strictly per-row from
        /// comparatorFlag: true only when the selected comparator was placebo-equivalent.
        /// Trial-design classification is diagnostic-only.
        /// </remarks>
        /// <param name="row">The non-comparator source row to project.</param>
        /// <param name="comparator">Comparator row chosen for this group, or null.</param>
        /// <param name="comparatorFlag">Comparator-kind flag.</param>
        /// <param name="dRef">Group D_ref, the minimum positive dose.</param>
        /// <param name="dRefUnit">Dose unit at D_ref.</param>
        /// <param name="design">Per-table trial-design classification used only for diagnostics.</param>
        /// <param name="additionalCalculationFlags">Optional Stage 5 provenance flags.</param>
        /// <returns>Entity ready for AddRange and SaveChangesAsync.</returns>
        internal static LabelView.FlattenedAdverseEventTable Build(
            LabelView.FlattenedStandardizedTable row,
            LabelView.FlattenedStandardizedTable? comparator,
            string comparatorFlag,
            decimal? dRef,
            string? dRefUnit,
            RelativeRiskCalculator.TrialDesignClassification design,
            IEnumerable<string>? additionalCalculationFlags = null)
        {
            #region implementation

            var entity = new LabelView.FlattenedAdverseEventTable
            {
                FlattenedStandardizedTableId = row.Id,
                DocumentGUID = row.DocumentGUID,
                UNII = row.UNII,
                ParameterName = row.ParameterName,
                ParameterCategory = row.ParameterCategory,
                ArmN = row.ArmN,
                Dose = row.Dose,
                DoseUnit = row.DoseUnit,
                PrimaryValue = row.PrimaryValue,
                PrimaryValueType = row.PrimaryValueType,
                StudyContext = row.StudyContext,
                Population = row.Population,
                Subpopulation = row.Subpopulation,
                TreatmentArm = row.TreatmentArm,
                ComparatorArm = comparator?.TreatmentArm,
                ComparatorN = comparator?.ArmN,
                IsPlaceboControlled = string.Equals(
                    comparatorFlag,
                    AeDenormalizationConstants.PlaceboComparatorFlag,
                    StringComparison.Ordinal)
            };

            var flags = new List<string> { comparatorFlag };
            if (design.Flag is not null)
                flags.Add(design.Flag);
            if (additionalCalculationFlags is not null)
            {
                flags.AddRange(additionalCalculationFlags
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Distinct(StringComparer.Ordinal));
            }

            if (comparator is null)
            {
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            var rowPvt = row.PrimaryValueType?.Trim();
            var compPvt = comparator.PrimaryValueType?.Trim();

            if (!string.Equals(rowPvt, compPvt, StringComparison.OrdinalIgnoreCase))
            {
                flags.Add("MIXED_VALUE_TYPES");
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            bool isPercentage = string.Equals(rowPvt, "Percentage", StringComparison.OrdinalIgnoreCase);
            bool isCount = string.Equals(rowPvt, "Count", StringComparison.OrdinalIgnoreCase);
            if (!isPercentage && !isCount)
            {
                flags.Add("UNCOMPARABLE_VALUE_TYPE");
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            if (row.PrimaryValue is null || comparator.PrimaryValue is null)
            {
                flags.Add("INVALID_EVENT_COUNT");
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            if (row.PrimaryValue < 0d || comparator.PrimaryValue < 0d)
            {
                flags.Add("INVALID_EVENT_COUNT");
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            if (isPercentage && (row.PrimaryValue > 100d || comparator.PrimaryValue > 100d))
            {
                flags.Add("PERCENT_OUT_OF_RANGE");
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            if (isPercentage)
            {
                var missingDenominatorFlags = getMissingDenominatorFlags(row.ArmN, comparator.ArmN);
                if (missingDenominatorFlags.Count > 0)
                {
                    flags.AddRange(missingDenominatorFlags);
                    entity.CalculationFlags = string.Join(";", flags);
                    return entity;
                }
            }

            var (rowEvents, rowEventFlag) = RelativeRiskCalculator.DeriveEventCount(
                row.PrimaryValue, rowPvt, row.ArmN);
            var (compEvents, compEventFlag) = RelativeRiskCalculator.DeriveEventCount(
                comparator.PrimaryValue, compPvt, comparator.ArmN);

            entity.EventsTreatment = rowEvents;
            entity.EventsComparator = compEvents;

            if (rowEvents is null || compEvents is null)
            {
                var flag = rowEventFlag ?? compEventFlag ?? "INVALID_EVENT_COUNT";
                flags.Add(flag);
                entity.CalculationFlags = string.Join(";", flags);
                return entity;
            }

            var rrResult = RelativeRiskCalculator.Compute(
                rowEvents, row.ArmN, compEvents, comparator.ArmN);

            entity.RR = rrResult.Rr;
            entity.RRLowerBound = rrResult.RrLower;
            entity.RRUpperBound = rrResult.RrUpper;

            if (rrResult.Flags is not null)
                flags.Add(rrResult.Flags);

            if (rrResult.Rr is not null)
            {
                var dnrrResult = RelativeRiskCalculator.ComputeDnrr(
                    rrResult, row.Dose, row.DoseUnit, dRef, dRefUnit);
                entity.DNRR = dnrrResult.Dnrr;
                entity.DNRRLowerBound = dnrrResult.DnrrLower;
                entity.DNRRUpperBound = dnrrResult.DnrrUpper;

                if (dnrrResult.Flags is not null)
                    flags.Add(dnrrResult.Flags);
            }

            entity.CalculationMethod = rrResult.Rr is not null
                ? AeDenormalizationConstants.KatzLogMethod
                : null;
            entity.CalculationFlags = string.Join(";", flags);

            return entity;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Identifies missing or invalid denominators before percentage-derived event
        /// counts and RR statistics are calculated.
        /// </summary>
        /// <remarks>
        /// Percentage observations cannot produce auditable event counts without a
        /// positive denominator on both treatment and comparator arms. This guard keeps
        /// those rows in the flattened table while preventing RR point estimates from
        /// being inferred from percentages alone.
        /// </remarks>
        /// <param name="armN">Treatment denominator.</param>
        /// <param name="comparatorN">Comparator denominator.</param>
        /// <returns>Zero, one, or two denominator diagnostic flags.</returns>
        /// <seealso cref="RelativeRiskCalculator"/>
        private static IReadOnlyList<string> getMissingDenominatorFlags(int? armN, int? comparatorN)
        {
            #region implementation

            var flags = new List<string>();

            if (armN is null || armN <= 0)
                flags.Add(AeDenormalizationConstants.NoArmNFlag);

            if (comparatorN is null || comparatorN <= 0)
                flags.Add(AeDenormalizationConstants.NoComparatorNFlag);

            return flags;

            #endregion
        }
    }
}
