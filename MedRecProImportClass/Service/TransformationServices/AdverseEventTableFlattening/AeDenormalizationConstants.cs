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
        /// <summary>Comparator flag emitted when a one-arm cohort cannot support RR.</summary>
        internal const string SingleArmFlag = "SINGLE_ARM";

        /**************************************************************/
        /// <summary>Comparator flag emitted when an active comparator is selected by explicit header/name evidence.</summary>
        internal const string ExplicitControlComparatorFlag = "EXPLICIT_CONTROL_COMPARATOR";

        /**************************************************************/
        /// <summary>Comparator flag emitted when a two-arm active comparator is selected deterministically.</summary>
        internal const string ActiveComparatorInferredFlag = "ACTIVE_COMPARATOR_INFERRED";

        /**************************************************************/
        /// <summary>Comparator flag emitted when a multi-arm active group has no deterministic comparator.</summary>
        internal const string AmbiguousComparatorFlag = "AMBIGUOUS_COMPARATOR";

        /**************************************************************/
        /// <summary>Comparator flag emitted for non-placebo active comparator rows.</summary>
        internal const string ActiveComparatorFlag = "ACTIVE_COMPARATOR";

        /**************************************************************/
        /// <summary>Math guard emitted when the treatment denominator is missing or invalid.</summary>
        internal const string NoArmNFlag = "NO_ARMN";

        /**************************************************************/
        /// <summary>Math guard emitted when the comparator denominator is missing or invalid.</summary>
        internal const string NoComparatorNFlag = "NO_COMPARATOR_N";

        /**************************************************************/
        /// <summary>Coverage status for rows whose RR inputs completed and persisted.</summary>
        internal const string RrReadyStatus = "RR_READY";

        /**************************************************************/
        /// <summary>Coverage status for rows selected as the shared comparator in their cohort.</summary>
        internal const string SelectedComparatorStatus = "SELECTED_COMPARATOR";

        /**************************************************************/
        /// <summary>Coverage reason for rows without a document identifier.</summary>
        internal const string NoDocumentGuidFlag = "NO_DOCUMENT_GUID";

        /**************************************************************/
        /// <summary>Coverage reason for rows whose treatment-arm text is invalid or missing.</summary>
        internal const string InvalidTreatmentArmFlag = "INVALID_TREATMENT_ARM";

        /**************************************************************/
        /// <summary>Coverage reason for rows whose AE name is structural or otherwise not analyzable.</summary>
        internal const string StructuralAeRowFlag = "STRUCTURAL_AE_ROW";

        /**************************************************************/
        /// <summary>Coverage reason for rows without a source value.</summary>
        internal const string NoPrimaryValueFlag = "NO_PRIMARY_VALUE";

        /**************************************************************/
        /// <summary>Coverage reason for rows excluded by Stage 5 MedDRA/name standardization.</summary>
        internal const string StandardizerExcludedFlag = "STANDARDIZER_EXCLUDED";

        /**************************************************************/
        /// <summary>Risk-view provenance flag for rows without product/pharmacologic-class enrichment.</summary>
        internal const string NoProductClassContextFlag = "NO_PRODUCT_CLASS_CONTEXT";

        /**************************************************************/
        /// <summary>Fallback coverage reason for null-RR rows without a known guard flag.</summary>
        internal const string UnknownNullRrFlag = "UNKNOWN_NULL_RR";

        /**************************************************************/
        /// <summary>Flag emitted when Stage 5 fills ArmN from the same arm and comparator group.</summary>
        internal const string ArmNStage5GroupBackfillFlag = "AE_ARMN_STAGE5_GROUP_BACKFILL";

        /**************************************************************/
        /// <summary>Flag emitted when Stage 5 rejects same-arm backfill due to conflicting Ns.</summary>
        internal const string ArmNRejectedConflictingNFlag = AeArmNValidationFlags.RejectedConflictingN;
    }
}
