using System.Text.RegularExpressions;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening
{
    /**************************************************************/
    /// <summary>
    /// Groups Stage 5 AE source rows into comparator-pairing cohorts.
    /// </summary>
    /// <remarks>
    /// Comparator pairing is scoped within a source table and normalized clinical
    /// context: parameter name, subtype, study context, population, and subpopulation.
    /// String keys are trimmed, whitespace-collapsed, and case-folded so harmless text
    /// variants do not split an otherwise valid comparator group.
    /// </remarks>
    /// <seealso cref="AdverseEventDenormalizationService"/>
    /// <seealso cref="ComparatorSelector"/>
    internal static class ComparatorGrouper
    {
        /**************************************************************/
        /// <summary>
        /// Groups rows from one TextTableID into comparator cohorts.
        /// </summary>
        /// <param name="tableRows">Rows from a single document/table group.</param>
        /// <returns>Comparator groups in deterministic source order.</returns>
        internal static IEnumerable<IReadOnlyList<LabelView.FlattenedStandardizedTable>> Group(
            IEnumerable<LabelView.FlattenedStandardizedTable> tableRows)
        {
            #region implementation

            return tableRows
                .GroupBy(r => new
                {
                    ParameterName = NormalizeKey(r.ParameterName),
                    ParameterSubtype = NormalizeKey(r.ParameterSubtype),
                    StudyContext = NormalizeKey(r.StudyContext),
                    Population = NormalizeKey(r.Population),
                    Subpopulation = NormalizeKey(r.Subpopulation),
                })
                .Select(g => (IReadOnlyList<LabelView.FlattenedStandardizedTable>)g.ToList());

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes nullable text for comparator grouping keys.
        /// </summary>
        /// <param name="value">Raw string value.</param>
        /// <returns>Upper-invariant, whitespace-collapsed key text.</returns>
        internal static string NormalizeKey(string? value)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var collapsed = Regex.Replace(value.Trim(), @"\s+", " ");
            return collapsed.ToUpperInvariant();

            #endregion
        }
    }
}
