using MedRecProImportClass.Service.TransformationServices.SampleSize;

namespace MedRecProImportClass.Service.TransformationServices.AdverseEventTableFlattening
{
    /**************************************************************/
    /// <summary>
    /// Shared constants for Stage 5 adverse-event denormalization collaborators.
    /// </summary>
    /// <remarks>
    /// Constants live here so helper extractions preserve the exact persisted strings
    /// used by the original monolithic implementation.
    /// </remarks>
    /// <seealso cref="AdverseEventDenormalizationService"/>
    internal static class AeDenormalizationConstants
    {
        /**************************************************************/
        /// <summary>Statistical method label persisted in CalculationMethod.</summary>
        internal const string KatzLogMethod = "KATZ_LOG";

        /**************************************************************/
        /// <summary>Comparator flag for placebo, sham, vehicle, or zero-dose comparator arms.</summary>
        internal const string PlaceboComparatorFlag = "PLACEBO_COMPARATOR";

        /**************************************************************/
        /// <summary>Comparator flag for the lowest non-zero dose comparator tier.</summary>
        internal const string LowDoseComparatorFlag = "LOW_DOSE_COMPARATOR";

        /**************************************************************/
        /// <summary>Comparator flag emitted when no comparator can be selected.</summary>
        internal const string NoComparatorFlag = "NO_COMPARATOR";

        /**************************************************************/
        /// <summary>Math guard emitted when the treatment denominator is missing or invalid.</summary>
        internal const string NoArmNFlag = "NO_ARMN";

        /**************************************************************/
        /// <summary>Math guard emitted when the comparator denominator is missing or invalid.</summary>
        internal const string NoComparatorNFlag = "NO_COMPARATOR_N";

        /**************************************************************/
        /// <summary>Flag emitted when Stage 5 fills ArmN from the same arm and comparator group.</summary>
        internal const string ArmNStage5GroupBackfillFlag = "AE_ARMN_STAGE5_GROUP_BACKFILL";

        /**************************************************************/
        /// <summary>Flag emitted when Stage 5 rejects same-arm backfill due to conflicting Ns.</summary>
        internal const string ArmNRejectedConflictingNFlag = AeArmNValidationFlags.RejectedConflictingN;
    }
}
