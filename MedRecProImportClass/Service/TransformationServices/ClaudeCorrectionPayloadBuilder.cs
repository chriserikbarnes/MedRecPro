using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using Newtonsoft.Json;

namespace MedRecProImportClass.Service.TransformationServices;

/**************************************************************/
/// <summary>
/// Builds Claude correction payloads and applies approved field mutations.
/// </summary>
/// <remarks>
/// This deterministic transformation owns the correction-field allowlist and
/// compact wire shape separately from API orchestration. It can therefore be
/// tested directly without reflecting into <see cref="ClaudeApiCorrectionService"/>.
/// </remarks>
/// <seealso cref="ClaudeApiCorrectionService"/>
/// <seealso cref="ParsedObservationFieldAccess"/>
internal static class ClaudeCorrectionPayloadBuilder
{
    #region implementation

    /**************************************************************/
    /// <summary>
    /// Gets the fields that Claude is allowed to correct.
    /// </summary>
    /// <remarks>
    /// The set is case-insensitive and derived from the parser data-dictionary
    /// contract. Callers must reject all other proposed corrections.
    /// </remarks>
    internal static IReadOnlySet<string> CorrectableFields { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ParameterName", "PrimaryValueType", "SecondaryValueType",
        "TreatmentArm", "DoseRegimen", "Dose", "DoseUnit", "Population", "Subpopulation", "Unit",
        "ParameterCategory", "ParameterSubtype", "Timepoint", "TimeUnit",
        "StudyContext", "BoundType"
    };

    /**************************************************************/
    /// <summary>
    /// Serializes correction-relevant observation fields into a compact JSON payload.
    /// </summary>
    /// <param name="observations">Observations selected for Claude review.</param>
    /// <returns>Compact JSON excluding large provenance-only fields.</returns>
    /// <seealso cref="ParsedObservation"/>
    internal static string Build(IReadOnlyCollection<ParsedObservation> observations)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(observations);

        var compact = observations.Select(o => new
        {
            o.SourceRowSeq,
            o.SourceCellSeq,
            o.ParameterName,
            o.ParameterCategory,
            o.ParameterSubtype,
            o.TreatmentArm,
            o.ArmN,
            o.StudyContext,
            o.DoseRegimen,
            o.Dose,
            o.DoseUnit,
            o.Population,
            o.Subpopulation,
            o.Timepoint,
            o.TimeUnit,
            o.RawValue,
            o.PrimaryValue,
            o.PrimaryValueType,
            o.SecondaryValue,
            o.SecondaryValueType,
            o.LowerBound,
            o.UpperBound,
            o.BoundType,
            o.Unit,
            o.TableCategory,
            o.ParseConfidence,
            o.ParseRule,
            o.Caption
        });

        return JsonConvert.SerializeObject(compact, Formatting.None);

        #endregion
    }

    /**************************************************************/
    /// <summary>
    /// Applies a correction when its field is in the approved allowlist.
    /// </summary>
    /// <param name="observation">Observation receiving the correction.</param>
    /// <param name="fieldName">Case-insensitive correction field name.</param>
    /// <param name="value">Proposed textual value, or null to clear the field.</param>
    /// <returns>True when the field was allowed and applied; otherwise false.</returns>
    /// <seealso cref="ParsedObservationFieldAccess.SetFromString"/>
    internal static bool TrySetField(ParsedObservation observation, string fieldName, string? value)
    {
        #region implementation

        ArgumentNullException.ThrowIfNull(observation);

        return CorrectableFields.Contains(fieldName)
            && ParsedObservationFieldAccess.SetFromString(observation, fieldName, value);

        #endregion
    }

    #endregion
}
