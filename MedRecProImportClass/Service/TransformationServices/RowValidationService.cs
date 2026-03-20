using MedRecProImportClass.Models;
using Microsoft.Extensions.Logging;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Stage 4 row-level validation: runs per-observation consistency checks and assigns
    /// <see cref="ValidationStatus"/>. Pure logic — no I/O or database access.
    /// </summary>
    /// <remarks>
    /// ## Validation Checks (in order)
    /// 1. Orphan detection — null TextTableID → Error
    /// 2. Required fields by TableCategory → Warning
    /// 3. PrimaryValueType appropriateness → Warning
    /// 4. ArmN required with TreatmentArm → Warning
    /// 5. Bound consistency — LowerBound > UpperBound → Error
    /// 6. Low confidence — ParseConfidence &lt; 0.5 → Warning
    ///
    /// ## Flag Coexistence
    /// Appends new flags to <see cref="ParsedObservation.ValidationFlags"/> using semicolon
    /// delimiter, preserving existing Stage 3 flags (e.g., PCT_CHECK:PASS).
    /// </remarks>
    /// <seealso cref="IRowValidationService"/>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="RowValidationResult"/>
    public class RowValidationService : IRowValidationService
    {
        #region Fields

        /**************************************************************/
        /// <summary>Logger for validation diagnostics.</summary>
        private readonly ILogger<RowValidationService> _logger;

        /**************************************************************/
        /// <summary>
        /// Required fields per TableCategory. Key = category, Value = list of property names
        /// that must be non-null and non-empty.
        /// </summary>
        private static readonly Dictionary<string, List<string>> _requiredFieldsByCategory = new()
        {
            ["PK"] = new() { "ParameterName", "DoseRegimen" },
            ["ADVERSE_EVENT"] = new() { "ParameterName", "TreatmentArm" },
            ["EFFICACY"] = new() { "ParameterName", "TreatmentArm" },
            ["BMD"] = new() { "ParameterName", "Timepoint" },
            ["DOSING"] = new() { "ParameterName" },
            ["TISSUE_DISTRIBUTION"] = new() { "ParameterName" },
            ["DRUG_INTERACTION"] = new() { "ParameterName" },
            ["OTHER"] = new() { "ParameterName" }
        };

        /**************************************************************/
        /// <summary>
        /// Allowed PrimaryValueType values per TableCategory. Types outside these sets
        /// produce a Warning (not Error, since edge cases exist).
        /// </summary>
        private static readonly Dictionary<string, HashSet<string>> _allowedValueTypesByCategory = new()
        {
            ["PK"] = new() { "Mean", "Median", "Numeric", "Ratio", "Text", "CodedExclusion", "SampleSize" },
            ["ADVERSE_EVENT"] = new() { "Percentage", "Count", "Numeric", "CodedExclusion", "Text", "RiskDifference", "RelativeRiskReduction", "PValue", "SampleSize" },
            ["EFFICACY"] = new() { "Percentage", "Count", "Numeric", "Mean", "Median", "RiskDifference", "RelativeRiskReduction", "Ratio", "PValue", "Text", "CodedExclusion", "SampleSize", "MeanPercentChange" },
            ["BMD"] = new() { "MeanPercentChange", "Percentage", "Numeric", "Mean", "Text" },
            ["TISSUE_DISTRIBUTION"] = new() { "Ratio", "Numeric", "Text" },
            ["DOSING"] = new() { "Numeric", "Percentage", "Mean", "Text", "SampleSize" }
        };

        #endregion Fields

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the row validation service.
        /// </summary>
        /// <param name="logger">Logger for validation diagnostics.</param>
        public RowValidationService(ILogger<RowValidationService> logger)
        {
            #region implementation

            _logger = logger;

            #endregion
        }

        #endregion Constructor

        #region IRowValidationService Implementation

        /**************************************************************/
        /// <summary>
        /// Validates a batch of observations and returns results for all.
        /// </summary>
        /// <param name="observations">Observations to validate.</param>
        /// <returns>All validation results.</returns>
        public List<RowValidationResult> ValidateObservations(List<ParsedObservation> observations)
        {
            #region implementation

            var results = new List<RowValidationResult>(observations.Count);

            foreach (var obs in observations)
            {
                results.Add(ValidateObservation(obs));
            }

            var errorCount = results.Count(r => r.Status == ValidationStatus.Error);
            var warningCount = results.Count(r => r.Status == ValidationStatus.Warning);

            if (errorCount > 0 || warningCount > 0)
            {
                _logger.LogDebug(
                    "Row validation: {Total} observations — {Valid} valid, {Warnings} warnings, {Errors} errors",
                    results.Count,
                    results.Count - errorCount - warningCount,
                    warningCount,
                    errorCount);
            }

            return results;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates a single observation. Appends flags to ValidationFlags.
        /// </summary>
        /// <param name="observation">The observation to validate.</param>
        /// <returns>Validation result with status and issues.</returns>
        public RowValidationResult ValidateObservation(ParsedObservation observation)
        {
            #region implementation

            var result = new RowValidationResult
            {
                TextTableID = observation.TextTableID,
                SourceRowSeq = observation.SourceRowSeq,
                SourceCellSeq = observation.SourceCellSeq,
                TreatmentArm = observation.TreatmentArm,
                ParameterName = observation.ParameterName
            };

            var newFlags = new List<string>();

            // Check 1: Orphan detection — null TextTableID
            if (observation.TextTableID == null)
            {
                result.Issues.Add("ORPHAN_ROW:TextTableID is null");
                newFlags.Add("ORPHAN_ROW");
            }

            // Check 2: Required fields by TableCategory
            checkRequiredFields(observation, result, newFlags);

            // Check 3: PrimaryValueType appropriateness
            checkValueTypeAppropriateness(observation, result, newFlags);

            // Check 4: ArmN required with TreatmentArm
            if (!string.IsNullOrWhiteSpace(observation.TreatmentArm)
                && observation.TreatmentArm != "Comparison"
                && observation.ArmN == null)
            {
                result.Issues.Add("MISSING_ARM_N:TreatmentArm set without ArmN");
                newFlags.Add("MISSING_ARM_N");
            }

            // Check 5: Bound consistency
            if (observation.LowerBound.HasValue && observation.UpperBound.HasValue
                && observation.LowerBound.Value > observation.UpperBound.Value)
            {
                result.Issues.Add($"BOUND_INVERSION:LowerBound ({observation.LowerBound}) > UpperBound ({observation.UpperBound})");
                newFlags.Add("BOUND_INVERSION");
            }

            // Check 6: Low confidence
            if (observation.ParseConfidence.HasValue && observation.ParseConfidence.Value < 0.5)
            {
                result.Issues.Add($"LOW_CONFIDENCE:{observation.ParseConfidence:F2}");
                newFlags.Add("LOW_CONFIDENCE");
            }

            // Determine overall status
            result.Status = determineStatus(result.Issues);

            // Append new flags to observation's ValidationFlags
            if (newFlags.Count > 0)
            {
                appendFlags(observation, newFlags);
            }

            return result;

            #endregion
        }

        #endregion IRowValidationService Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Checks required fields based on the observation's TableCategory.
        /// </summary>
        private static void checkRequiredFields(ParsedObservation observation, RowValidationResult result, List<string> newFlags)
        {
            #region implementation

            var category = observation.TableCategory;
            if (string.IsNullOrWhiteSpace(category))
            {
                result.Issues.Add("MISSING_FIELD:TableCategory");
                newFlags.Add("MISSING_CATEGORY");
                return;
            }

            if (!_requiredFieldsByCategory.TryGetValue(category, out var requiredFields))
                return;

            foreach (var fieldName in requiredFields)
            {
                var value = getFieldValue(observation, fieldName);
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.Issues.Add($"MISSING_FIELD:{fieldName}");
                    newFlags.Add($"MISSING_{fieldName.ToUpperInvariant()}");
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks whether PrimaryValueType is appropriate for the TableCategory.
        /// </summary>
        private static void checkValueTypeAppropriateness(ParsedObservation observation, RowValidationResult result, List<string> newFlags)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(observation.TableCategory)
                || string.IsNullOrWhiteSpace(observation.PrimaryValueType))
                return;

            if (!_allowedValueTypesByCategory.TryGetValue(observation.TableCategory, out var allowedTypes))
                return;

            if (!allowedTypes.Contains(observation.PrimaryValueType))
            {
                result.Issues.Add($"UNEXPECTED_VALUE_TYPE:{observation.PrimaryValueType} in {observation.TableCategory}");
                newFlags.Add($"UNEXPECTED_VALUE_TYPE");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets a string field value from a <see cref="ParsedObservation"/> by property name.
        /// </summary>
        private static string? getFieldValue(ParsedObservation observation, string fieldName)
        {
            #region implementation

            return fieldName switch
            {
                "ParameterName" => observation.ParameterName,
                "TreatmentArm" => observation.TreatmentArm,
                "DoseRegimen" => observation.DoseRegimen,
                "Timepoint" => observation.Timepoint,
                "Population" => observation.Population,
                _ => null
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines overall <see cref="ValidationStatus"/> from issue list.
        /// Any issue containing "ORPHAN" or "INVERSION" → Error; other issues → Warning.
        /// </summary>
        private static ValidationStatus determineStatus(List<string> issues)
        {
            #region implementation

            if (issues.Count == 0)
                return ValidationStatus.Valid;

            if (issues.Any(i => i.StartsWith("ORPHAN_ROW") || i.StartsWith("BOUND_INVERSION")))
                return ValidationStatus.Error;

            return ValidationStatus.Warning;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Appends new validation flags to the observation's ValidationFlags string
        /// using semicolon delimiter, preserving existing Stage 3 flags.
        /// </summary>
        private static void appendFlags(ParsedObservation observation, List<string> newFlags)
        {
            #region implementation

            var flagString = string.Join(";", newFlags);

            if (string.IsNullOrWhiteSpace(observation.ValidationFlags))
            {
                observation.ValidationFlags = flagString;
            }
            else
            {
                observation.ValidationFlags = observation.ValidationFlags + ";" + flagString;
            }

            #endregion
        }

        #endregion Private Helpers
    }
}
