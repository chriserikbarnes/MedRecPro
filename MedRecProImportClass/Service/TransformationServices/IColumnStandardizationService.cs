using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Stage 3.25 column standardization service for the SPL Table Normalization pipeline.
    /// Detects and corrects systematic misclassification of values across all observation
    /// context columns for ALL table categories (except SKIP).
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Stage 3 (Parser) → **Stage 3.25 Column Standardization (this)** → Stage 3.5 (Claude Correction) → DB
    ///
    /// ## Why This Exists
    /// SPL tables use diverse layouts — doses as column headers, N-values as headers,
    /// study names in the wrong header row. The parsers assign values based on column
    /// position, but position does not always correspond to semantic meaning. This service
    /// applies deterministic rules + a drug name dictionary to relocate values to their
    /// correct columns.
    ///
    /// ## 4-Phase Processing Pipeline
    /// 1. **Phase 1: Arm/Context Corrections** — (AE + EFFICACY) 11 ordered rules to fix
    ///    misclassified TreatmentArm/StudyContext values
    /// 2. **Phase 2: Content Normalization** — (ALL) DoseRegimen triage, ParameterName cleanup,
    ///    TreatmentArm cleanup, Unit scrub, SOC mapping
    /// 3. **Phase 3: PrimaryValueType Migration** — (ALL) Map old type strings to tightened
    ///    enum using table category and caption context
    /// 4. **Phase 4: Column Contract Enforcement** — (ALL) NULL out N/A columns, flag missing
    ///    required columns, apply default BoundType
    ///
    /// All corrections are flagged in <see cref="ParsedObservation.ValidationFlags"/>
    /// with <c>COL_STD:</c> prefixed flags for audit trail.
    /// </remarks>
    /// <seealso cref="ITableParsingOrchestrator"/>
    /// <seealso cref="ParsedObservation"/>
    public interface IColumnStandardizationService
    {
        /**************************************************************/
        /// <summary>
        /// Loads the drug name dictionary from the database. Must be called once before
        /// <see cref="Standardize"/>. Safe to call multiple times (no-ops after first load).
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Task that completes when the dictionary is loaded.</returns>
        Task InitializeAsync(CancellationToken ct = default);

        /**************************************************************/
        /// <summary>
        /// Applies 4-phase column standardization to the given observations. Processes ALL
        /// table categories except SKIP. Modifies observations in-place and returns the same list.
        /// </summary>
        /// <param name="observations">Parsed observations from Stage 3.</param>
        /// <returns>The same list with corrected column assignments and validation flags appended.</returns>
        List<ParsedObservation> Standardize(List<ParsedObservation> observations);

        /**************************************************************/
        /// <summary>
        /// Stage 3.6 post-processing: re-applies targeted extraction rules after Claude correction.
        /// Catches units and N-values that Claude may have corrected into extractable form
        /// (e.g., Claude restores a ParameterSubtype like "Cmax(pg/mL)" or an N= value).
        /// Flags use the <c>COL_STD:POST_</c> prefix to distinguish from Phase 2 corrections.
        /// </summary>
        /// <param name="observations">Observations after all correction stages.</param>
        /// <returns>The same list with additional extractions applied.</returns>
        /// <seealso cref="Standardize"/>
        List<ParsedObservation> PostProcessExtraction(List<ParsedObservation> observations);
    }
}
