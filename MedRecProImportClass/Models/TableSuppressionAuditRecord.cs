namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Audit record for a source table row or cell that was identified as structural
    /// table metadata and intentionally suppressed before observation emission.
    /// </summary>
    /// <remarks>
    /// Suppression records are diagnostic metadata only. They are not written as
    /// <see cref="ParsedObservation"/> rows and therefore do not participate in
    /// Claude quality gating, ML scoring, or database persistence.
    /// </remarks>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="Service.TransformationServices.ITableParserDiagnostics"/>
    public sealed class TableSuppressionAuditRecord
    {
        /**************************************************************/
        /// <summary>Source TextTableID for the suppressed row or cell.</summary>
        public int? TextTableID { get; init; }

        /**************************************************************/
        /// <summary>Source row sequence number, when available.</summary>
        public int? SourceRowSeq { get; init; }

        /**************************************************************/
        /// <summary>Source cell sequence number, when suppression was cell-specific.</summary>
        public int? SourceCellSeq { get; init; }

        /**************************************************************/
        /// <summary>Parser category assigned to the source table.</summary>
        public string? TableCategory { get; init; }

        /**************************************************************/
        /// <summary>Concrete parser that made the suppression decision.</summary>
        public string? ParserName { get; init; }

        /**************************************************************/
        /// <summary>Parameter label that supplied row context.</summary>
        public string? ParameterName { get; init; }

        /**************************************************************/
        /// <summary>Treatment arm context, when suppression was tied to one arm cell.</summary>
        public string? TreatmentArm { get; init; }

        /**************************************************************/
        /// <summary>Raw source cell text that was suppressed.</summary>
        public string? RawValue { get; init; }

        /**************************************************************/
        /// <summary>Structural label preserved as category or group context.</summary>
        public string? StructuralLabel { get; init; }

        /**************************************************************/
        /// <summary>Target context field that received the structural label.</summary>
        public string? ContextTarget { get; init; }

        /**************************************************************/
        /// <summary>Human-readable reason for the suppression decision.</summary>
        public string? Reason { get; init; }

        /**************************************************************/
        /// <summary>Stable validation flag used by diagnostic reports.</summary>
        public string ValidationFlag { get; init; } = "SUPPRESSED_STRUCTURAL_ROW";
    }
}
