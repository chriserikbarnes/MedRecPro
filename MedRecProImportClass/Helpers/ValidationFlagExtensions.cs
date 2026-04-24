using MedRecProImportClass.Models;

namespace MedRecProImportClass.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Extension methods for appending semicolon-delimited validation flags onto a
    /// <see cref="ParsedObservation"/>. Centralizes the delimiter convention shared by
    /// <c>QCNetCorrectionService</c>, <c>ColumnStandardizationService</c>, and
    /// <c>ClaudeApiCorrectionService</c>, all of which previously carried byte-identical
    /// <c>appendFlag</c> helpers.
    /// </summary>
    /// <remarks>
    /// The delimiter is <c>"; "</c> (semicolon + space). When
    /// <see cref="ParsedObservation.ValidationFlags"/> is null or empty the new flag is assigned
    /// directly; otherwise it is concatenated with the delimiter. Flags are plain strings — no
    /// deduplication, no escaping, no ordering semantics. This matches the pre-existing behavior
    /// exactly so existing parsers that split <c>ValidationFlags</c> on <c>"; "</c> remain valid.
    /// <para>
    /// Out of scope: <c>BaseTableParser.appendFlag</c> (functional, returns a string) and
    /// <c>RowValidationService.appendFlags</c> (batch, plural) — those have different shapes and
    /// are intentionally not consolidated here.
    /// </para>
    /// </remarks>
    /// <seealso cref="ParsedObservation.ValidationFlags"/>
    public static class ValidationFlagExtensions
    {
        /**************************************************************/
        /// <summary>
        /// Appends a flag to <see cref="ParsedObservation.ValidationFlags"/> using the
        /// semicolon-space (<c>"; "</c>) delimiter convention.
        /// </summary>
        /// <param name="obs">Observation to mutate.</param>
        /// <param name="flag">Flag string to append.</param>
        /// <example>
        /// <code>
        /// obs.AppendValidationFlag("COL_STD:POPULATION_EXTRACTED");
        /// // If obs.ValidationFlags was null → "COL_STD:POPULATION_EXTRACTED"
        /// // If obs.ValidationFlags was "AI_CORRECTED:x" →
        /// //     "AI_CORRECTED:x; COL_STD:POPULATION_EXTRACTED"
        /// </code>
        /// </example>
        public static void AppendValidationFlag(this ParsedObservation obs, string flag)
        {
            #region implementation

            obs.ValidationFlags = string.IsNullOrEmpty(obs.ValidationFlags)
                ? flag
                : $"{obs.ValidationFlags}; {flag}";

            #endregion
        }
    }
}
