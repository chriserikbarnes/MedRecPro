namespace MedRecProImportClass.Service.TransformationServices.SampleSize
{
    /**************************************************************/
    /// <summary>
    /// Shared AE ArmN validation flag strings used by parser and Stage 5 code.
    /// </summary>
    /// <remarks>
    /// Keeping these strings in one source prevents parser diagnostics and
    /// denormalization diagnostics from drifting when denominator policy changes.
    /// </remarks>
    /// <seealso cref="ArmNResolver"/>
    /// <seealso cref="SampleSizeParser"/>
    internal static class AeArmNValidationFlags
    {
        /**************************************************************/
        /// <summary>Diagnostic emitted when multiple exact N candidates conflict.</summary>
        internal const string RejectedConflictingN = "AE_ARMN_REJECTED_CONFLICTING_N";
    }
}
