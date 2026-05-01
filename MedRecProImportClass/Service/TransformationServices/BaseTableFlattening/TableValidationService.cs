using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;
using Microsoft.Extensions.Logging;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Stage 4 table-level validation: runs cross-row consistency checks within each
    /// TextTableID group. Pure logic — no I/O or database access.
    /// </summary>
    /// <remarks>
    /// ## Validation Checks (in order)
    /// 1. Duplicate observations — same (ParameterName, TreatmentArm, SourceRowSeq) → Warning
    /// 2. Arm coverage gap — arms with ArmN defined but no data rows with PrimaryValue → Warning
    /// 3. Count reasonableness — AE/EFFICACY actual count vs (arms × params) → Warning if >20% deviation
    /// </remarks>
    /// <seealso cref="ITableValidationService"/>
    /// <seealso cref="TableValidationResult"/>
    /// <seealso cref="ParsedObservation"/>
    public class TableValidationService : ITableValidationService
    {
        #region Fields

        /**************************************************************/
        /// <summary>Logger for validation diagnostics.</summary>
        private readonly ILogger<TableValidationService> _logger;

        #endregion Fields

        #region Constructor

        /**************************************************************/
        /// <summary>
        /// Initializes the table validation service.
        /// </summary>
        /// <param name="logger">Logger for validation diagnostics.</param>
        public TableValidationService(ILogger<TableValidationService> logger)
        {
            #region implementation

            _logger = logger;

            #endregion
        }

        #endregion Constructor

        #region ITableValidationService Implementation

        /**************************************************************/
        /// <summary>
        /// Validates all tables by grouping observations by TextTableID.
        /// </summary>
        /// <param name="observations">All observations to validate.</param>
        /// <returns>One result per distinct TextTableID.</returns>
        public List<TableValidationResult> ValidateTables(List<ParsedObservation> observations)
        {
            #region implementation

            var results = new List<TableValidationResult>();

            var groups = observations
                .Where(o => o.TextTableID.HasValue)
                .GroupBy(o => o.TextTableID!.Value);

            foreach (var group in groups)
            {
                results.Add(ValidateTable(group.Key, group.ToList()));
            }

            var issueCount = results.Count(r => r.Status != ValidationStatus.Valid);
            if (issueCount > 0)
            {
                _logger.LogDebug(
                    "Table validation: {Total} tables — {Issues} with issues",
                    results.Count, issueCount);
            }

            return results;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Validates observations within a single table.
        /// </summary>
        /// <param name="textTableId">The TextTableID being validated.</param>
        /// <param name="tableObservations">All observations for this table.</param>
        /// <returns>Validation result with status and issues.</returns>
        public TableValidationResult ValidateTable(int textTableId, List<ParsedObservation> tableObservations)
        {
            #region implementation

            var category = tableObservations.FirstOrDefault()?.TableCategory;

            var result = new TableValidationResult
            {
                TextTableID = textTableId,
                TableCategory = category,
                ObservationCount = tableObservations.Count
            };

            // Check 1: Duplicate observations
            checkDuplicates(tableObservations, result);

            // Check 2: Arm coverage gap (AE/EFFICACY only) — sourced from CategoryProfileRegistry.
            // Get(...) handles null/whitespace by returning CategoryProfile.Empty (UsesArmCoverage=false),
            // so the null guard collapses.
            var profile = CategoryProfileRegistry.Get(category);
            if (profile.UsesArmCoverage)
            {
                checkArmCoverage(tableObservations, result);

                // Check 3: Count reasonableness
                checkCountReasonableness(tableObservations, result);
            }

            // Check 4: Time extraction consistency (PK/BMD only)
            if (profile.UsesTimeConsistency)
            {
                checkTimeConsistency(tableObservations, result);
            }

            // Determine overall status
            result.Status = result.Issues.Count == 0
                ? ValidationStatus.Valid
                : ValidationStatus.Warning;

            return result;

            #endregion
        }

        #endregion ITableValidationService Implementation

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Detects duplicate observations within a table: same (ParameterName, TreatmentArm, SourceRowSeq).
        /// </summary>
        private static void checkDuplicates(List<ParsedObservation> observations, TableValidationResult result)
        {
            #region implementation

            var duplicateGroups = observations
                .GroupBy(o => new
                {
                    o.ParameterName,
                    o.TreatmentArm,
                    o.SourceRowSeq
                })
                .Where(g => g.Count() > 1);

            foreach (var group in duplicateGroups)
            {
                var key = $"({group.Key.ParameterName}, {group.Key.TreatmentArm}, Row={group.Key.SourceRowSeq})";
                result.DuplicateKeys.Add(key);
                result.Issues.Add($"DUPLICATE_OBSERVATION:{key} × {group.Count()}");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Detects arms that have ArmN defined on some rows but no data rows with PrimaryValue.
        /// Only applies to AE and EFFICACY tables.
        /// </summary>
        private static void checkArmCoverage(List<ParsedObservation> observations, TableValidationResult result)
        {
            #region implementation

            // Exclude Comparison arm from coverage checks
            var nonComparisonObs = observations
                .Where(o => o.TreatmentArm != "Comparison")
                .ToList();

            // Arms that have ArmN defined (known arms)
            var knownArms = nonComparisonObs
                .Where(o => !string.IsNullOrWhiteSpace(o.TreatmentArm) && o.ArmN.HasValue)
                .Select(o => o.TreatmentArm!)
                .Distinct()
                .ToList();

            // Arms that have at least one data row with a PrimaryValue
            var armsWithData = nonComparisonObs
                .Where(o => !string.IsNullOrWhiteSpace(o.TreatmentArm) && o.PrimaryValue.HasValue)
                .Select(o => o.TreatmentArm!)
                .Distinct()
                .ToHashSet();

            foreach (var arm in knownArms)
            {
                if (!armsWithData.Contains(arm))
                {
                    result.MissingArms.Add(arm);
                    result.Issues.Add($"ARM_COVERAGE_GAP:{arm} has ArmN but no data rows");
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks whether the actual observation count is within 20% of the expected count
        /// (distinct non-Comparison arms × distinct parameter names).
        /// </summary>
        private static void checkCountReasonableness(List<ParsedObservation> observations, TableValidationResult result)
        {
            #region implementation

            // Exclude Comparison rows from the count check
            var dataObs = observations
                .Where(o => o.TreatmentArm != "Comparison")
                .ToList();

            if (dataObs.Count == 0)
                return;

            var distinctArms = dataObs
                .Where(o => !string.IsNullOrWhiteSpace(o.TreatmentArm))
                .Select(o => o.TreatmentArm!)
                .Distinct()
                .Count();

            var distinctParams = dataObs
                .Where(o => !string.IsNullOrWhiteSpace(o.ParameterName))
                .Select(o => o.ParameterName!)
                .Distinct()
                .Count();

            if (distinctArms == 0 || distinctParams == 0)
                return;

            var expected = distinctArms * distinctParams;
            var actual = dataObs.Count;
            var deviation = Math.Abs(actual - expected) / (double)expected;

            if (deviation > 0.20)
            {
                result.Issues.Add(
                    $"COUNT_DEVIATION:expected ~{expected} ({distinctArms} arms × {distinctParams} params), got {actual} ({deviation:P0} deviation)");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks whether Time extraction is consistent within a PK or BMD table.
        /// If some observations have Time populated and others do not (excluding single-dose
        /// timepoints), flags an inconsistency warning.
        /// </summary>
        private static void checkTimeConsistency(List<ParsedObservation> observations, TableValidationResult result)
        {
            #region implementation

            if (observations.Count == 0)
                return;

            // Exclude observations with "single dose" timepoint — these legitimately have no Time
            var nonSingleDose = observations
                .Where(o => !string.Equals(o.Timepoint, "single dose", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (nonSingleDose.Count == 0)
                return;

            var withTime = nonSingleDose.Count(o => o.Time.HasValue);
            var withoutTime = nonSingleDose.Count - withTime;

            // Flag if there's a mix (some have Time, some don't)
            if (withTime > 0 && withoutTime > 0)
            {
                result.Issues.Add(
                    $"TIME_EXTRACTION_INCONSISTENCY:{withoutTime} of {nonSingleDose.Count} observations missing Time");
            }

            #endregion
        }

        #endregion Private Helpers
    }
}
