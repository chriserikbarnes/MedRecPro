using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Stage 4 table-level validation service for the SPL Table Normalization pipeline.
    /// Runs cross-row consistency checks within each TextTableID group.
    /// </summary>
    /// <remarks>
    /// ## Pipeline Position
    /// Stage 3 (ParsedObservation) → **Stage 4 Table Validation (this)** → BatchValidationReport
    ///
    /// ## Checks Performed
    /// - Duplicate observation detection (same ParameterName + TreatmentArm + SourceRowSeq)
    /// - Arm coverage gap detection (arms with ArmN but no data rows)
    /// - Observation count reasonableness (actual vs expected for AE/EFFICACY tables)
    ///
    /// Pure logic — no I/O or database access.
    /// </remarks>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="TableValidationResult"/>
    /// <seealso cref="IRowValidationService"/>
    /// <seealso cref="IBatchValidationService"/>
    public interface ITableValidationService
    {
        /**************************************************************/
        /// <summary>
        /// Validates all tables in a batch by grouping observations by TextTableID.
        /// </summary>
        /// <param name="observations">All observations to validate.</param>
        /// <returns>One <see cref="TableValidationResult"/> per distinct TextTableID.</returns>
        List<TableValidationResult> ValidateTables(List<ParsedObservation> observations);

        /**************************************************************/
        /// <summary>
        /// Validates observations within a single table.
        /// </summary>
        /// <param name="textTableId">The TextTableID being validated.</param>
        /// <param name="tableObservations">All observations for this table.</param>
        /// <returns>Validation result with status and issues.</returns>
        TableValidationResult ValidateTable(int textTableId, List<ParsedObservation> tableObservations);
    }
}
