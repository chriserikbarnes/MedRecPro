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

            return GetExclusionReason(row) is null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the durable Stage 5 exclusion reason for a source row, if any.
        /// </summary>
        /// <remarks>
        /// The service persists this reason into the coverage table so rows rejected
        /// before comparator grouping remain explainable after transient logs expire.
        /// </remarks>
        /// <param name="row">Flattened standardized AE source row.</param>
        /// <returns>Stable reason flag, or null when the row is denormalizable.</returns>
        /// <seealso cref="IsDenormalizableAeSourceRow"/>
        internal static string? GetExclusionReason(LabelView.FlattenedStandardizedTable row)
        {
            #region implementation

            if (row.DocumentGUID == null)
                return AeDenormalizationConstants.NoDocumentGuidFlag;

            if (AeColumnContextResolver.IsInvalidTreatmentArm(row.TreatmentArm))
                return AeDenormalizationConstants.InvalidTreatmentArmFlag;

            if (AeColumnContextResolver.IsCaptionLikeText(row.ParameterName) ||
                AeColumnContextResolver.IsBodySystemLabel(row.ParameterName) ||
                AeColumnContextResolver.IsValueAxisToken(row.ParameterName) ||
                AeColumnContextResolver.IsThresholdOnlyOrExcludedAeName(row.ParameterName))
            {
                return AeDenormalizationConstants.StructuralAeRowFlag;
            }

            if (!row.PrimaryValue.HasValue)
                return AeDenormalizationConstants.NoPrimaryValueFlag;

            return null;

            #endregion
        }
    }
}
