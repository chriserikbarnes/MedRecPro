using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Stage 3.25 column standardization service for the SPL Table Normalization pipeline.
    /// Detects and corrects systematic misclassification of values across TreatmentArm,
    /// ArmN, DoseRegimen, StudyContext, and ParameterSubtype columns for ADVERSE_EVENT
    /// and EFFICACY table categories.
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
    /// ## Correction Patterns
    /// 1. TreatmentArm contains N= value → move to ArmN
    /// 2. TreatmentArm contains format hint (%, #) → discard, recover arm from StudyContext
    /// 3. TreatmentArm contains severity grade → move to ParameterSubtype
    /// 4. TreatmentArm contains dose regimen → move to DoseRegimen
    /// 5. TreatmentArm contains bare number + StudyContext has dose descriptor → reconstruct
    /// 6. TreatmentArm contains drug+dose combined → split
    /// 7. StudyContext contains arm name with N= → split to TreatmentArm + ArmN
    /// 8. StudyContext contains drug name (swap needed) → swap with TreatmentArm
    /// 9. StudyContext contains descriptor hint → clear
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
        /// Applies column standardization rules to the given observations. Only processes
        /// observations with TableCategory == "ADVERSE_EVENT" or "EFFICACY"; others pass through
        /// unchanged. Modifies observations in-place and returns the same list.
        /// </summary>
        /// <param name="observations">Parsed observations from Stage 3.</param>
        /// <returns>The same list with corrected column assignments and validation flags appended.</returns>
        List<ParsedObservation> Standardize(List<ParsedObservation> observations);
    }
}
