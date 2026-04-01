using Newtonsoft.Json;

namespace MedRecProImportClass.Models
{
    /**************************************************************/
    /// <summary>
    /// Represents a single field correction returned by the Claude API.
    /// Used by <see cref="MedRecProImportClass.Service.TransformationServices.ClaudeApiCorrectionService"/>
    /// to deserialize the JSON correction array from Stage 3.5.
    /// </summary>
    /// <remarks>
    /// ## Field Usage
    /// - SourceRowSeq + SourceCellSeq: Uniquely identify the target <see cref="ParsedObservation"/>
    /// - Field: Must be in the CorrectableFields set (ParameterName, PrimaryValueType, etc.)
    /// - OldValue/NewValue: Before/after for audit logging and ValidationFlags
    /// - Reason: Brief (≤6 words) explanation appended to ValidationFlags
    /// </remarks>
    /// <seealso cref="MedRecProImportClass.Service.TransformationServices.IClaudeApiCorrectionService"/>
    public class CorrectionEntry
    {
        /**************************************************************/
        /// <summary>Row sequence identifying the target observation.</summary>
        [JsonProperty("sourceRowSeq")]
        public int? SourceRowSeq { get; set; }

        /**************************************************************/
        /// <summary>Cell sequence identifying the target observation.</summary>
        [JsonProperty("sourceCellSeq")]
        public int? SourceCellSeq { get; set; }

        /**************************************************************/
        /// <summary>Field name to correct (must be in CorrectableFields set).</summary>
        [JsonProperty("field")]
        public string? Field { get; set; }

        /**************************************************************/
        /// <summary>Original value (for logging/audit).</summary>
        [JsonProperty("oldValue")]
        public string? OldValue { get; set; }

        /**************************************************************/
        /// <summary>Corrected value to apply.</summary>
        [JsonProperty("newValue")]
        public string? NewValue { get; set; }

        /**************************************************************/
        /// <summary>Brief reason for the correction.</summary>
        [JsonProperty("reason")]
        public string? Reason { get; set; }
    }
}
