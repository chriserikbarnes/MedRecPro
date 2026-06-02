using MedRecProImportClass.Models;
using System.Text.RegularExpressions;

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
        #region Fields

        /**************************************************************/
        /// <summary>Regex identifying explicit active-control/comparator arm labels.</summary>
        private static readonly Regex ExplicitControlPattern = new(
            @"\b(active\s+comparator|comparator|control|reference|standard\s+of\s+care|usual\s+care)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        #endregion Fields

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

            if (groupRows.Count <= 1)
                return (null, AeDenormalizationConstants.SingleArmFlag);

            var placeboCandidates = groupRows
                .Where(r => RelativeRiskCalculator.IsPlaceboArm(r.TreatmentArm, r.Dose))
                .OrderBy(r => r, SourceRowComparer.Instance)
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

            if (dosedCandidates.Count > 0)
                return (dosedCandidates[0], AeDenormalizationConstants.LowDoseComparatorFlag);

            var explicitControlCandidates = groupRows
                .Where(IsExplicitControlArm)
                .OrderBy(r => r, SourceRowComparer.Instance)
                .ToList();

            if (hasExactlyOneDistinctArm(explicitControlCandidates))
                return (explicitControlCandidates[0], AeDenormalizationConstants.ExplicitControlComparatorFlag);

            if (explicitControlCandidates.Count > 0)
                return (null, AeDenormalizationConstants.AmbiguousComparatorFlag);

            var activeArmGroups = groupRows
                .Where(r => !string.IsNullOrWhiteSpace(r.TreatmentArm))
                .GroupBy(r => ComparatorGrouper.NormalizeKey(r.TreatmentArm))
                .Select(g => g.OrderBy(r => r, SourceRowComparer.Instance).First())
                .OrderBy(r => r, SourceRowComparer.Instance)
                .ToList();

            if (activeArmGroups.Count == 2)
                return (activeArmGroups[0], AeDenormalizationConstants.ActiveComparatorInferredFlag);

            if (activeArmGroups.Count > 2)
                return (null, AeDenormalizationConstants.AmbiguousComparatorFlag);

            return (null, AeDenormalizationConstants.NoComparatorFlag);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a source row carries explicit active-control evidence.
        /// </summary>
        /// <param name="row">Candidate comparator row.</param>
        /// <returns>True when the arm text identifies a control/comparator/reference arm.</returns>
        private static bool IsExplicitControlArm(LabelView.FlattenedStandardizedTable row)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(row.TreatmentArm))
                return false;

            return ExplicitControlPattern.IsMatch(row.TreatmentArm);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a candidate set represents one distinct arm label.
        /// </summary>
        /// <param name="rows">Candidate rows.</param>
        /// <returns>True when all candidates belong to one normalized arm.</returns>
        private static bool hasExactlyOneDistinctArm(
            IReadOnlyList<LabelView.FlattenedStandardizedTable> rows)
        {
            #region implementation

            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r.TreatmentArm))
                .Select(r => ComparatorGrouper.NormalizeKey(r.TreatmentArm))
                .Distinct(StringComparer.Ordinal)
                .Count() == 1;

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

        /**************************************************************/
        /// <summary>
        /// Deterministic source-order comparer used by all comparator tie-breakers.
        /// </summary>
        /// <seealso cref="Select"/>
        private sealed class SourceRowComparer : IComparer<LabelView.FlattenedStandardizedTable>
        {
            #region Fields

            /**************************************************************/
            /// <summary>Singleton comparer instance.</summary>
            internal static readonly SourceRowComparer Instance = new();

            #endregion Fields

            /**************************************************************/
            /// <summary>
            /// Compares two source rows by source row, source cell, then table row ID.
            /// </summary>
            /// <param name="x">First row.</param>
            /// <param name="y">Second row.</param>
            /// <returns>Sort comparison result.</returns>
            public int Compare(
                LabelView.FlattenedStandardizedTable? x,
                LabelView.FlattenedStandardizedTable? y)
            {
                #region implementation

                if (ReferenceEquals(x, y))
                    return 0;
                if (x is null)
                    return -1;
                if (y is null)
                    return 1;

                var rowCompare = (x.SourceRowSeq ?? int.MaxValue)
                    .CompareTo(y.SourceRowSeq ?? int.MaxValue);
                if (rowCompare != 0)
                    return rowCompare;

                var cellCompare = (x.SourceCellSeq ?? int.MaxValue)
                    .CompareTo(y.SourceCellSeq ?? int.MaxValue);
                if (cellCompare != 0)
                    return cellCompare;

                return x.Id.CompareTo(y.Id);

                #endregion
            }
        }
    }
}
