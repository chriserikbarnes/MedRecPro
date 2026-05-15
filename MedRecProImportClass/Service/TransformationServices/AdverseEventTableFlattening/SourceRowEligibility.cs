using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening
{
    /**************************************************************/
    /// <summary>
    /// Evaluates whether a standardized AE row can safely enter Stage 5 grouping.
    /// </summary>
    /// <remarks>
    /// This keeps the denormalization safety gate separate from EF orchestration while
    /// preserving the parser-first repair stance: Stage 5 filters obviously structural
    /// rows but does not try to invent missing parser evidence.
    /// </remarks>
    /// <seealso cref="AdverseEventDenormalizationService"/>
    /// <seealso cref="AeColumnContextResolver"/>
    internal static class SourceRowEligibility
    {
        /**************************************************************/
        /// <summary>
        /// Determines whether an AE source row is safe to enter Stage 5 grouping.
        /// </summary>
        /// <remarks>
        /// Rows with valid numeric values but missing ArmN remain eligible so the
        /// existing downstream RR-only/no-CI handling is preserved.
        /// </remarks>
        /// <param name="row">Flattened standardized AE source row.</param>
        /// <returns>True when the row can participate in Stage 5.</returns>
        internal static bool IsDenormalizableAeSourceRow(LabelView.FlattenedStandardizedTable row)
        {
            #region implementation

            if (row.DocumentGUID == null)
                return false;

            if (AeColumnContextResolver.IsInvalidTreatmentArm(row.TreatmentArm))
                return false;

            if (AeColumnContextResolver.IsCaptionLikeText(row.ParameterName) ||
                AeColumnContextResolver.IsBodySystemLabel(row.ParameterName) ||
                AeColumnContextResolver.IsValueAxisToken(row.ParameterName))
            {
                return false;
            }

            return row.PrimaryValue.HasValue;

            #endregion
        }
    }
}
