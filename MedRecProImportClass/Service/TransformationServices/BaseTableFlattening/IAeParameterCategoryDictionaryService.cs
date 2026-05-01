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
    /// Static dictionary of 698 unambiguous ParameterName → canonical SOC mappings
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
        /// Pure lookup: returns the canonical ParameterName for the given variant,
        /// or null if the input is not a known variant. Used to collapse textual
        /// variants (NOS suffix, (nonserious) suffix, plural/singular drift, known
        /// synonyms) into a single canonical grammar before SOC resolution.
        /// </summary>
        /// <param name="parameterName">The AE ParameterName to normalize (case-insensitive).</param>
        /// <returns>Canonical ParameterName string, or null if the input is not a known variant.</returns>
        /// <seealso cref="TryNormalizeObservationName"/>
        string? NormalizeParameterName(string? parameterName);

        /**************************************************************/
        /// <summary>
        /// Attempts to standardize <see cref="ParsedObservation.ParameterName"/> on the
        /// given observation by replacing a known variant with its canonical form. Only acts
        /// when: TableCategory is ADVERSE_EVENT, ParameterName is non-empty, and the current
        /// name is a variant distinct from its canonical form. Appends
        /// <c>DICT:NAME_NORM:&lt;old&gt;-&gt;&lt;new&gt;</c> flag on success.
        /// </summary>
        /// <remarks>
        /// Callers should invoke this BEFORE <see cref="TryResolveObservation"/> so the
        /// canonical name is persisted and downstream SOC resolution operates on the
        /// standardized form.
        /// </remarks>
        /// <param name="obs">The observation to normalize. Modified in-place if a variant is found.</param>
        /// <returns>True if the ParameterName was rewritten; false otherwise.</returns>
        /// <seealso cref="NormalizeParameterName"/>
        /// <seealso cref="TryResolveObservation"/>
        bool TryNormalizeObservationName(ParsedObservation obs);

        /**************************************************************/
        /// <summary>
        /// Returns the number of entries in the SOC resolution dictionary.
        /// </summary>
        int Count { get; }

        /**************************************************************/
        /// <summary>
        /// Returns the number of entries in the ParameterName variant → canonical
        /// normalization dictionary.
        /// </summary>
        int NormalizationCount { get; }
    }
}
