using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening
{
    /**************************************************************/
    /// <summary>
    /// Selects Stage 5 AE comparator rows and dose-reference metadata.
    /// </summary>
    /// <remarks>
    /// The comparator cascade preserves the original three-tier behavior: placebo
    /// first, lowest non-zero dose second, and no comparator for single-arm groups.
    /// Deterministic tie-breakers keep repeated runs byte-stable.
    /// </remarks>
    /// <seealso cref="RelativeRiskCalculator"/>
    /// <seealso cref="AeStatEntityBuilder"/>
    internal static class ComparatorSelector
    {
        /**************************************************************/
        /// <summary>
        /// Selects one comparator row for a comparator group.
        /// </summary>
        /// <param name="groupRows">Rows in one comparator group.</param>
        /// <returns>Selected comparator and its diagnostic flag.</returns>
        internal static (LabelView.FlattenedStandardizedTable? comparator, string flag)
            Select(IReadOnlyList<LabelView.FlattenedStandardizedTable> groupRows)
        {
            #region implementation

            var placeboCandidates = groupRows
                .Where(r => RelativeRiskCalculator.IsPlaceboArm(r.TreatmentArm, r.Dose))
                .OrderBy(r => r.Dose ?? decimal.MinValue)
                .ThenBy(r => r.SourceRowSeq ?? int.MaxValue)
                .ThenBy(r => r.SourceCellSeq ?? int.MaxValue)
                .ThenBy(r => r.Id)
                .ToList();

            if (placeboCandidates.Count > 0)
                return (placeboCandidates[0], AeDenormalizationConstants.PlaceboComparatorFlag);

            var dosedCandidates = groupRows
                .Where(r => r.Dose != null && r.Dose > 0m)
                .OrderBy(r => r.Dose)
                .ThenBy(r => r.SourceRowSeq ?? int.MaxValue)
                .ThenBy(r => r.SourceCellSeq ?? int.MaxValue)
                .ThenBy(r => r.Id)
                .ToList();

            if (dosedCandidates.Count > 0 && groupRows.Count > 1)
                return (dosedCandidates[0], AeDenormalizationConstants.LowDoseComparatorFlag);

            return (null, AeDenormalizationConstants.NoComparatorFlag);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Selects D_ref metadata for dose-normalized relative risk calculations.
        /// </summary>
        /// <param name="groupRows">Rows in one comparator group.</param>
        /// <returns>Minimum positive dose and its unit, or nulls when no positive dose exists.</returns>
        internal static (decimal? dRef, string? dRefUnit) SelectReferenceDose(
            IReadOnlyList<LabelView.FlattenedStandardizedTable> groupRows)
        {
            #region implementation

            var dosedRows = groupRows
                .Where(r => r.Dose != null && r.Dose > 0m)
                .ToList();

            if (dosedRows.Count == 0)
                return (null, null);

            var dRef = dosedRows.Min(r => r.Dose);
            var dRefUnit = dosedRows.First(r => r.Dose == dRef).DoseUnit;
            return (dRef, dRefUnit);

            #endregion
        }
    }
}
