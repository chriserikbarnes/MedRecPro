using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Stage 4 row-level validation service for the SPL Table Normalization pipeline.
    /// Runs automated consistency checks on individual <see cref="ParsedObservation"/> DTOs
    /// and assigns a <see cref="ValidationStatus"/> to each.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Stage 3 (ParsedObservation) → **Stage 4 Row Validation (this)** → BatchValidationReport
    ///
    /// ## Checks Performed
    /// - Required fields by TableCategory (e.g., PK needs DoseRegimen)
    /// - PrimaryValueType appropriateness per category
    /// - ArmN required when TreatmentArm is populated
    /// - Bound consistency (LowerBound &lt; UpperBound)
    /// - Low confidence flagging (ParseConfidence &lt; 0.5)
    /// - Orphan detection (null TextTableID)
    ///
    /// Pure logic — no I/O or database access.
    /// </remarks>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="RowValidationResult"/>
    /// <seealso cref="ITableValidationService"/>
    /// <seealso cref="IBatchValidationService"/>
    public interface IRowValidationService
    {
        /**************************************************************/
        /// <summary>
        /// Validates a batch of observations and returns results for those with issues.
        /// Also appends validation flags to each observation's <see cref="ParsedObservation.ValidationFlags"/>.
        /// </summary>
        /// <param name="observations">Observations to validate.</param>
        /// <returns>Validation results for all observations (including Valid ones).</returns>
        List<RowValidationResult> ValidateObservations(List<ParsedObservation> observations);

        /**************************************************************/
        /// <summary>
        /// Validates a single observation. Appends any new flags to
        /// <see cref="ParsedObservation.ValidationFlags"/> (semicolon-delimited).
        /// </summary>
        /// <param name="observation">The observation to validate.</param>
        /// <returns>Validation result with status and issues.</returns>
        RowValidationResult ValidateObservation(ParsedObservation observation);
    }
}
