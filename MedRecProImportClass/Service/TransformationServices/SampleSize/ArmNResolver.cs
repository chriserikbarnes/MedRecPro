using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices.SampleSize
{
    /**************************************************************/
    /// <summary>
    /// Applies adverse-event sample-size policy to parser evidence.
    /// </summary>
    /// <remarks>
    /// The resolver is intentionally small and deterministic. Syntax evidence comes
    /// from <see cref="SampleSizeParser"/>; this helper only decides which exact
    /// value can become <see cref="ParsedObservation.ArmN"/>.
    /// </remarks>
    /// <seealso cref="SampleSizeEvidence"/>
    /// <seealso cref="ParsedObservation"/>
    internal static class ArmNResolver
    {
        /**************************************************************/
        /// <summary>Validation flag for header-derived denominators.</summary>
        internal const string FromHeaderFlag = "AE_ARMN_FROM_HEADER_N";

        /**************************************************************/
        /// <summary>Validation flag for fraction-derived denominators.</summary>
        internal const string FromFractionDenominatorFlag = "AE_ARMN_FROM_FRACTION_DENOMINATOR";

        /**************************************************************/
        /// <summary>Validation flag for body metadata denominators.</summary>
        internal const string FromMetadataRowFlag = "AE_ARMN_FROM_METADATA_ROW";

        /**************************************************************/
        /// <summary>Validation flag for inline suffix denominators.</summary>
        internal const string FromInlineSuffixFlag = "AE_ARMN_FROM_INLINE_SUFFIX";

        /**************************************************************/
        /// <summary>Validation flag for inferred count-percent denominators.</summary>
        internal const string FromCountPercentInferenceFlag = "AE_ARMN_FROM_COUNT_PERCENT_INFERENCE";

        /**************************************************************/
        /// <summary>Validation flag for conflicting denominator evidence.</summary>
        internal const string RejectedConflictingNFlag = "AE_ARMN_REJECTED_CONFLICTING_N";

        /**************************************************************/
        /// <summary>
        /// Resolves the best ArmN for an AE observation.
        /// </summary>
        /// <param name="arm">Resolved treatment-arm context.</param>
        /// <param name="parsed">Parsed value evidence from the observation cell.</param>
        /// <param name="scopedMetadataN">More specific body/header-tier N override.</param>
        /// <param name="existingArmN">Existing ArmN value, if a caller already set one.</param>
        /// <returns>Resolved ArmN plus the diagnostic flag to append, if any.</returns>
        /// <seealso cref="BuildValueContextArm"/>
        internal static ArmNResolution ResolveForAeObservation(
            ArmDefinition arm,
            ParsedValue parsed,
            int? scopedMetadataN = null,
            int? existingArmN = null)
        {
            #region implementation

            var chosen = chooseContextualArmN(arm, scopedMetadataN, out var sourceFlag);
            var cellN = parsed.SampleSize is > 0 ? parsed.SampleSize : null;
            if (chosen.HasValue && cellN.HasValue && chosen.Value != cellN.Value)
            {
                return new ArmNResolution(chosen, RejectedConflictingNFlag);
            }

            if (chosen.HasValue)
                return new ArmNResolution(chosen, sourceFlag);

            if (cellN.HasValue)
                return new ArmNResolution(cellN, getCellSampleSizeFlag(parsed));

            if (existingArmN is > 0)
                return new ArmNResolution(existingArmN, null);

            return new ArmNResolution(null, null);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates an arm context whose sample size reflects the active parser scope.
        /// </summary>
        /// <param name="arm">Original arm definition.</param>
        /// <param name="scopedMetadataN">Optional scoped sample size override.</param>
        /// <returns>Original arm or a shallow copy with the scoped sample size.</returns>
        /// <seealso cref="ArmDefinition"/>
        internal static ArmDefinition BuildValueContextArm(ArmDefinition arm, int? scopedMetadataN)
        {
            #region implementation

            if (scopedMetadataN is not > 0 || arm.SampleSize == scopedMetadataN)
                return arm;

            return new ArmDefinition
            {
                Name = arm.Name,
                SampleSize = scopedMetadataN,
                FormatHint = arm.FormatHint,
                ColumnIndex = arm.ColumnIndex,
                StudyContext = arm.StudyContext,
                ParameterSubtype = arm.ParameterSubtype,
                DoseRegimen = arm.DoseRegimen,
                Dose = arm.Dose,
                DoseUnit = arm.DoseUnit
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Chooses the strongest contextual denominator before cell-local evidence.
        /// </summary>
        private static int? chooseContextualArmN(
            ArmDefinition arm,
            int? scopedMetadataN,
            out string? sourceFlag)
        {
            #region implementation

            if (scopedMetadataN is > 0)
            {
                sourceFlag = FromMetadataRowFlag;
                return scopedMetadataN;
            }

            if (arm.SampleSize is > 0)
            {
                sourceFlag = FromHeaderFlag;
                return arm.SampleSize;
            }

            sourceFlag = null;
            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Maps parsed value evidence to the accepted denominator source flag.
        /// </summary>
        private static string getCellSampleSizeFlag(ParsedValue parsed)
        {
            #region implementation

            return parsed.ParseRule switch
            {
                "frac_pct" or "fraction_count" => FromFractionDenominatorFlag,
                "count_percent_inference" => FromCountPercentInferenceFlag,
                _ => FromInlineSuffixFlag
            };

            #endregion
        }
    }

    /**************************************************************/
    /// <summary>
    /// Result of AE ArmN resolution.
    /// </summary>
    /// <param name="ArmN">Resolved denominator value.</param>
    /// <param name="ValidationFlag">Diagnostic flag to append.</param>
    /// <seealso cref="ArmNResolver"/>
    internal sealed record ArmNResolution(int? ArmN, string? ValidationFlag);
}
