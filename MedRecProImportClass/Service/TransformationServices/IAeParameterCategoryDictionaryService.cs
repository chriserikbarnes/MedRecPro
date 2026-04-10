using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Lookup service that resolves NULL <see cref="ParsedObservation.ParameterCategory"/>
    /// values for ADVERSE_EVENT observations using a static dictionary of unambiguous
    /// ParameterName → canonical SOC (System Organ Class) mappings built from production
    /// data analysis.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Called within Stage 3.25 Phase 2 (Content Normalization), after
    /// <see cref="IColumnStandardizationService"/> normalizes existing non-NULL categories.
    /// Only fills in NULL categories — never overwrites existing values.
    ///
    /// ## Dictionary Source
    /// Static dictionary of 747 unambiguous ParameterName → canonical SOC mappings
    /// derived from ~45K rows of production ADVERSE_EVENT data where the same
    /// ParameterName always maps to exactly one SOC after canonical normalization.
    ///
    /// ## Validation Flag
    /// Appends <c>DICT:SOC_RESOLVED</c> to <see cref="ParsedObservation.ValidationFlags"/>
    /// when a NULL ParameterCategory is successfully resolved from the dictionary.
    /// </remarks>
    /// <seealso cref="IColumnStandardizationService"/>
    /// <seealso cref="ParsedObservation"/>
    public interface IAeParameterCategoryDictionaryService
    {
        /**************************************************************/
        /// <summary>
        /// Pure lookup: returns the canonical SOC for the given AE parameter name,
        /// or null if the name is not in the dictionary.
        /// </summary>
        /// <param name="parameterName">The AE ParameterName to look up (case-insensitive).</param>
        /// <returns>Canonical SOC string, or null if not found.</returns>
        string? Resolve(string? parameterName);

        /**************************************************************/
        /// <summary>
        /// Attempts to resolve a NULL <see cref="ParsedObservation.ParameterCategory"/>
        /// on the given observation. Only acts when: TableCategory is ADVERSE_EVENT,
        /// ParameterCategory is null/whitespace, and ParameterName is found in the dictionary.
        /// Appends <c>DICT:SOC_RESOLVED</c> flag on success.
        /// </summary>
        /// <param name="obs">The observation to resolve. Modified in-place if resolved.</param>
        /// <returns>True if the category was resolved and set; false otherwise.</returns>
        bool TryResolveObservation(ParsedObservation obs);

        /**************************************************************/
        /// <summary>
        /// Returns the number of entries in the dictionary.
        /// </summary>
        int Count { get; }
    }
}
