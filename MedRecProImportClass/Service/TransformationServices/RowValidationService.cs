using MedRecProImportClass.Helpers;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;
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
    /// 6. Low confidence — ParseConfidence &lt; <see cref="ParsedValue.ConfidenceThreshold.LowConfidence"/> → Warning
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
        /// Allowed TimeUnit values for vocabulary validation.
        /// </summary>
        private static readonly HashSet<string> _allowedTimeUnits = new(StringComparer.OrdinalIgnoreCase)
        {
            "days", "weeks", "months", "hours", "years"
        };

        /**************************************************************/
        /// <summary>
        /// Confidence penalty multipliers by issue type. Applied cumulatively to
        /// <see cref="ParsedObservation.ParseConfidence"/> to produce <see cref="ParsedObservation.AdjustedConfidence"/>.
        /// </summary>
        private static readonly Dictionary<string, double> _confidencePenalties = new()
        {
            ["MISSING_FIELD"] = 0.85,
            ["UNEXPECTED_VALUE_TYPE"] = 0.90,
            ["TIME_UNIT_MISMATCH"] = 0.90,
            ["UNREASONABLE_TIME"] = 0.85,
            ["INVALID_TIME_UNIT"] = 0.90,
            ["MISSING_ARM_N"] = 0.95
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

            // Check 6: Low confidence (raw ParseConfidence)
            if (observation.ParseConfidence.HasValue && observation.ParseConfidence.Value < ParsedValue.ConfidenceThreshold.LowConfidence)
            {
                result.Issues.Add($"LOW_CONFIDENCE:{observation.ParseConfidence:F2}");
                newFlags.Add("LOW_CONFIDENCE");
            }

            // Check 7: Time/TimeUnit pairing
            if ((observation.Time.HasValue && string.IsNullOrWhiteSpace(observation.TimeUnit))
                || (!observation.Time.HasValue && !string.IsNullOrWhiteSpace(observation.TimeUnit)))
            {
                result.Issues.Add("TIME_UNIT_MISMATCH:Time and TimeUnit must both be present or both absent");
                newFlags.Add("TIME_UNIT_MISMATCH");
            }

            // Check 8: Time range
            if (observation.Time.HasValue && observation.Time.Value <= 0)
            {
                result.Issues.Add($"UNREASONABLE_TIME:{observation.Time.Value}");
                newFlags.Add("UNREASONABLE_TIME");
            }

            // Check 9: TimeUnit vocabulary
            if (!string.IsNullOrWhiteSpace(observation.TimeUnit)
                && !_allowedTimeUnits.Contains(observation.TimeUnit))
            {
                result.Issues.Add($"INVALID_TIME_UNIT:{observation.TimeUnit}");
                newFlags.Add("INVALID_TIME_UNIT");
            }

            // Compute field completeness score
            result.FieldCompletenessScore = calculateFieldCompleteness(observation);

            // Compute adjusted confidence with penalty multipliers
            observation.AdjustedConfidence = calculateAdjustedConfidence(observation, newFlags);

            // Determine overall status
            result.Status = determineStatus(result.Issues);

            // Append new flags to observation's ValidationFlags
            foreach (var flag in newFlags)
            {
                observation.AppendValidationFlag(flag);
            }

            return result;

            #endregion
        }

        #endregion IRowValidationService Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Checks required fields based on the observation's TableCategory, sourcing the
        /// required-field list from <see cref="CategoryProfileRegistry"/>.
        /// </summary>
        /// <seealso cref="CategoryProfile.RowRequiredFields"/>
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

            var profile = CategoryProfileRegistry.Get(category);

            foreach (var fieldName in profile.RowRequiredFields)
            {
                if (!ParsedObservationFieldAccess.IsPopulated(observation, fieldName))
                {
                    result.Issues.Add($"MISSING_FIELD:{fieldName}");
                    newFlags.Add($"MISSING_{fieldName.ToUpperInvariant()}");
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks whether PrimaryValueType is appropriate for the TableCategory, sourcing the
        /// allowed-types set from <see cref="CategoryProfileRegistry"/>. An empty set means
        /// "no constraint" — the check is skipped (matches legacy behavior for categories that
        /// had no entry in the old <c>_allowedValueTypesByCategory</c> dictionary).
        /// </summary>
        /// <seealso cref="CategoryProfile.AllowedValueTypes"/>
        private static void checkValueTypeAppropriateness(ParsedObservation observation, RowValidationResult result, List<string> newFlags)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(observation.TableCategory)
                || string.IsNullOrWhiteSpace(observation.PrimaryValueType))
                return;

            var allowedTypes = CategoryProfileRegistry.Get(observation.TableCategory).AllowedValueTypes;
            if (allowedTypes.Count == 0)
                return; // No constraint configured — skip (matches legacy "category not in dict" path)

            if (!allowedTypes.Contains(observation.PrimaryValueType))
            {
                result.Issues.Add($"UNEXPECTED_VALUE_TYPE:{observation.PrimaryValueType} in {observation.TableCategory}");
                newFlags.Add($"UNEXPECTED_VALUE_TYPE");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Calculates field completeness score (0.0–1.0) based on how many expected fields
        /// (required + desirable) are populated for the observation's TableCategory, sourcing
        /// the completeness-fields list from <see cref="CategoryProfileRegistry"/>.
        /// </summary>
        /// <remarks>
        /// Categories with an empty <see cref="CategoryProfile.CompletenessFields"/> list
        /// (DRUG_INTERACTION, TEXT_DESCRIPTIVE, OTHER, unknown) return 1.0 to avoid penalizing
        /// rows the registry has no opinion on — preserves the legacy "unknown category — don't
        /// penalize" behavior of the old <c>_completenessFieldsByCategory</c> lookup.
        /// </remarks>
        /// <param name="observation">The observation to score.</param>
        /// <returns>Score from 0.0 (no fields populated) to 1.0 (all expected fields populated).</returns>
        /// <seealso cref="CategoryProfile.CompletenessFields"/>
        private static double calculateFieldCompleteness(ParsedObservation observation)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(observation.TableCategory))
                return 0.0;

            var fields = CategoryProfileRegistry.Get(observation.TableCategory).CompletenessFields;
            if (fields.Count == 0)
                return 1.0; // No constraint configured — don't penalize

            var populated = fields.Count(f => ParsedObservationFieldAccess.IsPopulated(observation, f));
            return (double)populated / fields.Count;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Calculates adjusted confidence by applying cumulative penalty multipliers
        /// based on validation issues found. Starts from <see cref="ParsedObservation.ParseConfidence"/>
        /// and reduces by each applicable penalty.
        /// </summary>
        /// <param name="observation">The observation being validated.</param>
        /// <param name="flags">Validation flags collected during this validation pass.</param>
        /// <returns>Adjusted confidence clamped to [0.0, 1.0], or null if ParseConfidence is null.</returns>
        private static double? calculateAdjustedConfidence(ParsedObservation observation, List<string> flags)
        {
            #region implementation

            if (!observation.ParseConfidence.HasValue)
                return null;

            var adjusted = observation.ParseConfidence.Value;

            foreach (var flag in flags)
            {
                // Extract the base flag name (before any colon detail)
                var baseName = flag.Contains(':') ? flag[..flag.IndexOf(':')] : flag;

                // MISSING_FIELD flags all start with "MISSING_" — map to MISSING_FIELD penalty
                if (baseName.StartsWith("MISSING_") && baseName != "MISSING_CATEGORY")
                {
                    baseName = "MISSING_FIELD";
                }

                if (_confidencePenalties.TryGetValue(baseName, out var multiplier))
                {
                    adjusted *= multiplier;
                }
            }

            return Math.Max(0.0, Math.Min(1.0, adjusted));

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

        #endregion Private Helpers
    }
}
