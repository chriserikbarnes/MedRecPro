using System.Text.RegularExpressions;
using MedRecProImportClass.Models;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Static utility for extracting structured <c>Dose</c> (decimal) and <c>DoseUnit</c> (string)
    /// from free-text dose descriptions found in <see cref="ParsedObservation.DoseRegimen"/>,
    /// <see cref="ParsedObservation.TreatmentArm"/>, and other observation columns.
    /// </summary>
    /// <remarks>
    /// ## Design
    /// - Pure extraction: reads text, never modifies source columns.
    /// - Shared by <see cref="BaseTableParser"/>, <see cref="ColumnStandardizationService"/>,
    ///   and <see cref="MlNetCorrectionService"/>.
    /// - All regex patterns are compiled and static for performance.
    ///
    /// ## Extraction Priority
    /// 1. Range/titration patterns → max dose (e.g., "10-20 mg" → Dose=20)
    /// 2. Core dose+unit pattern (e.g., "600 mg/d" → Dose=600, DoseUnit="mg/d")
    /// 3. Frequency promotion (e.g., "2 mg Once Daily" → DoseUnit promoted to "mg/d")
    ///
    /// ## Normalization Rules
    /// - mg/day → mg/d, mcg/day → mcg/d, µg → mcg
    /// - Footnote markers stripped (†, *, ‡)
    /// </remarks>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="ArmDefinition"/>
    internal static class DoseExtractor
    {
        #region Compiled Regex Patterns

        /**************************************************************/
        /// <summary>
        /// Matches dose range/titration patterns: "10-20 mg", "150–600 mg/d".
        /// Groups: 1=low dose, 2=high dose, 3=unit.
        /// </summary>
        private static readonly Regex _rangePattern = new(
            @"(\d+\.?\d*)\s*[-–—]\s*(\d+\.?\d*)\s*(mg/m[²2]|(?:mg/d(?:ay)?|mg/kg|mcg/d(?:ay)?|mcg|µg|mg|mL|g|IU|[Uu]nits?)\b)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Core dose extraction: "600 mg/d", "50 mg", "1 mg/kg", "100 IU".
        /// Groups: 1=dose number, 2=unit.
        /// </summary>
        private static readonly Regex _coreDosePattern = new(
            @"(\d+\.?\d*)\s*(mg/m[²2]|(?:mg/d(?:ay)?|mg/kg|mcg/d(?:ay)?|mcg|µg|mg|mL|g|IU|[Uu]nits?)\b)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Detects daily frequency indicators in trailing text after the dose+unit.
        /// When matched, promotes base unit (mg → mg/d, mcg → mcg/d).
        /// </summary>
        private static readonly Regex _dailyFrequencyPattern = new(
            @"\b(?:once\s+daily|daily|per\s+day|QD|q\.?d\.?|every\s+day|each\s+day|/day)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Footnote markers to strip before extraction.
        /// Excludes superscript digits (¹²³) which appear in scientific units like mg/m².
        /// </summary>
        private static readonly Regex _footnotePattern = new(
            @"[†‡§¶*\u2020\u2021\u00A7\u00B6\u207A\u207B]+",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Detects "Any dose" or "All doses" labels — not extractable.
        /// </summary>
        private static readonly Regex _anyDosePattern = new(
            @"\b(?:any|all)\s+dose",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Percentage arm pattern: "% 10 mg", "% 40 mg". Groups: 1=dose, 2=unit.
        /// </summary>
        private static readonly Regex _percentArmPattern = new(
            @"^%\s+(\d+\.?\d*)\s*(mg|mL|mcg|µg|g|IU|[Uu]nits?)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Matches the LAST numeric dose token plus its unit in a string, used by
        /// <see cref="StripDoseFragment"/> to split compound "Drug + Dose" row labels
        /// like "Atorvastatin 10 mg/day for 8 days" into a drug-name prefix
        /// ("Atorvastatin") and a dose fragment ("10 mg/day for 8 days").
        /// </summary>
        /// <remarks>
        /// Intentionally greedy left-anchor: matches from the first `\d+ (mg|mcg|…)` token
        /// through end-of-string, so everything after the first numeric dose — including
        /// trailing schedule text ("for 8 days", "once daily") — is removed.
        /// </remarks>
        private static readonly Regex _doseFragmentPattern = new(
            @"\s*\b\d+(?:\.\d+)?\s*(?:mg|mcg|µg|μg|g|ng|kg|mL|units?|U|IU)\b.*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Compiled Regex Patterns

        #region Public Methods

        /**************************************************************/
        /// <summary>
        /// Extracts a structured (Dose, DoseUnit) tuple from free-text dose descriptions.
        /// </summary>
        /// <remarks>
        /// ## Extraction Order
        /// 1. Guard: null/whitespace, "Any dose", no-digit abbreviations → (null, null)
        /// 2. Strip footnote markers
        /// 3. Range/titration → max value (e.g., "10-20 mg" → 20, "mg")
        /// 4. Core pattern → first match (e.g., "600 mg/d" → 600, "mg/d")
        /// 5. Frequency promotion → mg/mcg + daily indicator → mg/d or mcg/d
        /// 6. Unit normalization via <see cref="NormalizeUnit"/>
        ///
        /// ## Edge Cases
        /// | Input | Dose | DoseUnit |
        /// |-------|------|----------|
        /// | "10-20 mg" | 20 | mg |
        /// | "2 mg Once Daily" | 2 | mg/d |
        /// | "1 mg/kg q12h" | 1 | mg/kg |
        /// | "PGB" | null | null |
        /// | "Any dose" | null | null |
        /// </remarks>
        /// <param name="text">Free-text dose description from DoseRegimen, TreatmentArm, etc.</param>
        /// <returns>Tuple of (dose, doseUnit). Both null when no dose is recoverable.</returns>
        /// <example>
        /// <code>
        /// var (dose, unit) = DoseExtractor.Extract("600 mg/d");
        /// // dose = 600m, unit = "mg/d"
        ///
        /// var (dose2, unit2) = DoseExtractor.Extract("PGB");
        /// // dose2 = null, unit2 = null
        /// </code>
        /// </example>
        public static (decimal? dose, string? doseUnit) Extract(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return (null, null);

            // Guard: "Any dose", "All doses" — not extractable
            if (_anyDosePattern.IsMatch(text))
                return (null, null);

            // Guard: no digits → abbreviation-only (e.g., "PGB", "Placebo")
            if (!text.Any(char.IsDigit))
                return (null, null);

            // Strip footnote markers before extraction
            var cleaned = _footnotePattern.Replace(text, "").Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
                return (null, null);

            // Try percentage arm pattern first: "% 10 mg"
            var pctMatch = _percentArmPattern.Match(cleaned);
            if (pctMatch.Success && decimal.TryParse(pctMatch.Groups[1].Value, out var pctDose))
            {
                var pctUnit = NormalizeUnit(pctMatch.Groups[2].Value);
                return (pctDose, pctUnit);
            }

            // Try range/titration pattern: "10-20 mg", "150-600 mg/d"
            var rangeMatch = _rangePattern.Match(cleaned);
            if (rangeMatch.Success)
            {
                // Take the max (target/high) dose
                if (decimal.TryParse(rangeMatch.Groups[2].Value, out var highDose))
                {
                    var rawUnit = rangeMatch.Groups[3].Value;
                    var unit = NormalizeUnit(rawUnit);
                    // Check for frequency promotion on text after the match
                    var trailing = cleaned[(rangeMatch.Index + rangeMatch.Length)..];
                    unit = promoteUnitIfDaily(unit, trailing);
                    return (highDose, unit);
                }
            }

            // Try core dose pattern: "600 mg/d", "50 mg oral", "1 mg/kg q12h"
            var coreMatch = _coreDosePattern.Match(cleaned);
            if (coreMatch.Success && decimal.TryParse(coreMatch.Groups[1].Value, out var dose))
            {
                var rawUnit = coreMatch.Groups[2].Value;
                var unit = NormalizeUnit(rawUnit);
                // Check for frequency promotion on text after the match
                var trailing = cleaned[(coreMatch.Index + coreMatch.Length)..];
                unit = promoteUnitIfDaily(unit, trailing);
                return (dose, unit);
            }

            return (null, null);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Strips the numeric dose fragment (and any trailing schedule text) from a
        /// compound "drug + dose" row label, returning just the drug-name prefix.
        /// Used by PK row-label classification to split labels like
        /// "Atorvastatin 10 mg/day for 8 days" → "Atorvastatin".
        /// </summary>
        /// <remarks>
        /// Returns an empty string when the input is null or whitespace.
        /// Returns the input unchanged (trimmed) when no dose fragment is detected.
        /// </remarks>
        /// <param name="input">Candidate row-label text.</param>
        /// <returns>Drug-name prefix with the trailing dose fragment removed.</returns>
        /// <example>
        /// <code>
        /// DoseExtractor.StripDoseFragment("Atorvastatin 10 mg/day for 8 days") // "Atorvastatin"
        /// DoseExtractor.StripDoseFragment("Placebo")                          // "Placebo"
        /// DoseExtractor.StripDoseFragment("100 mg")                           // ""
        /// </code>
        /// </example>
        public static string StripDoseFragment(string? input)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;
            return _doseFragmentPattern.Replace(input, "").TrimEnd();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes a dose unit string to a canonical short form.
        /// Idempotent — safe to call multiple times on the same value.
        /// </summary>
        /// <remarks>
        /// | Raw | Normalized |
        /// |-----|------------|
        /// | mg/day | mg/d |
        /// | mg/ day | mg/d |
        /// | mcg/day | mcg/d |
        /// | µg | mcg |
        /// | µg/day | mcg/d |
        /// | units | U |
        /// | Units | U |
        /// | mg/day† | mg/d |
        /// </remarks>
        /// <param name="unit">Raw unit string to normalize.</param>
        /// <returns>Normalized unit or null if input is null/whitespace.</returns>
        public static string? NormalizeUnit(string? unit)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(unit))
                return null;

            // Strip footnote markers from unit
            var normalized = _footnotePattern.Replace(unit, "").Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            // µg → mcg
            normalized = normalized.Replace("µg", "mcg");

            // Normalize /day variants → /d
            normalized = Regex.Replace(normalized, @"/\s*day\b", "/d", RegexOptions.IgnoreCase);

            // Normalize units/unit → U
            normalized = Regex.Replace(normalized, @"^units?$", "U", RegexOptions.IgnoreCase);

            return normalized;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Backfills placebo arms with Dose=0.0 and DoseUnit inherited from non-placebo
        /// treatment arms in the same table (matched by TextTableID).
        /// </summary>
        /// <remarks>
        /// Must be called after all per-observation dose extraction and normalization,
        /// so non-placebo arms already have their Dose/DoseUnit populated.
        ///
        /// ## Rules
        /// - Groups observations by TextTableID
        /// - Per table: finds the majority DoseUnit among non-placebo arms with non-null DoseUnit
        /// - Sets Dose=0.0, DoseUnit=majority on placebo arms where Dose is still null
        /// - Placebo detection: TreatmentArm contains "placebo" (case-insensitive)
        /// - If no treatment arms have parseable units, leaves placebo arms unchanged
        /// - If mixed units, uses the most common unit
        /// </remarks>
        /// <param name="observations">All observations in the batch (modified in place).</param>
        public static void BackfillPlaceboArms(List<ParsedObservation> observations)
        {
            #region implementation

            // Group by TextTableID to scope placebo backfill to each table
            var tableGroups = observations
                .Where(o => o.TextTableID.HasValue)
                .GroupBy(o => o.TextTableID!.Value);

            foreach (var group in tableGroups)
            {
                // Find the dominant DoseUnit among non-placebo arms with a parsed dose
                var nonPlaceboUnits = group
                    .Where(o => !isPlaceboArm(o.TreatmentArm) && !string.IsNullOrEmpty(o.DoseUnit))
                    .Select(o => o.DoseUnit!)
                    .ToList();

                if (nonPlaceboUnits.Count == 0)
                    continue;

                // Majority vote
                var majorityUnit = nonPlaceboUnits
                    .GroupBy(u => u, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Key;

                // Backfill placebo arms
                foreach (var obs in group.Where(o => isPlaceboArm(o.TreatmentArm) && !o.Dose.HasValue))
                {
                    obs.Dose = 0.0m;
                    obs.DoseUnit = majorityUnit;
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Scans all text columns of an observation for misplaced dose patterns.
        /// Populates <see cref="ParsedObservation.Dose"/> and <see cref="ParsedObservation.DoseUnit"/>
        /// from the first column where a dose is found, in priority order.
        /// </summary>
        /// <remarks>
        /// ## Scan Priority
        /// 1. DoseRegimen — primary source
        /// 2. TreatmentArm — embedded doses (e.g., "Vivelle 0.075 mg/day†")
        /// 3. ParameterName — bare dose levels or misplaced "10 mg"
        /// 4. ParameterSubtype — dose info routed here by PK sub-param detection
        /// 5. StudyContext — dose descriptors (e.g., "Target Dosage (mg/day)")
        ///
        /// Only extracts; never modifies source columns.
        /// Only populates Dose/DoseUnit if both are still null.
        /// </remarks>
        /// <param name="obs">Observation to scan (modified in place).</param>
        /// <returns>True if Dose/DoseUnit were populated; false if no dose found or already populated.</returns>
        public static bool ScanAllColumnsForDose(ParsedObservation obs)
        {
            #region implementation

            // Already has a dose — nothing to do
            if (obs.Dose.HasValue)
                return false;

            // Skip placebo arms — they're handled by BackfillPlaceboArms
            if (isPlaceboArm(obs.TreatmentArm))
                return false;

            // Scan columns in priority order
            var columnsToScan = new (string? value, string label)[]
            {
                (obs.DoseRegimen, "DOSE_REGIMEN"),
                (obs.TreatmentArm, "TREATMENT_ARM"),
                (obs.ParameterName, "PARAMETER_NAME"),
                (obs.ParameterSubtype, "PARAMETER_SUBTYPE"),
                (obs.StudyContext, "STUDY_CONTEXT"),
            };

            foreach (var (value, label) in columnsToScan)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                var (dose, doseUnit) = Extract(value);
                if (dose.HasValue)
                {
                    obs.Dose = dose;
                    obs.DoseUnit = doseUnit;
                    return true;
                }
            }

            return false;

            #endregion
        }

        #endregion Public Methods

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Promotes a base unit to daily form if the trailing text contains a daily frequency indicator.
        /// Only promotes simple units (mg → mg/d, mcg → mcg/d). Compound units (mg/kg, mg/m²) are unchanged.
        /// </summary>
        /// <param name="unit">Normalized unit to potentially promote.</param>
        /// <param name="trailingText">Text after the dose+unit match.</param>
        /// <returns>Promoted unit or original unit if no daily indicator found.</returns>
        private static string? promoteUnitIfDaily(string? unit, string trailingText)
        {
            #region implementation

            if (unit == null || string.IsNullOrWhiteSpace(trailingText))
                return unit;

            // Only promote simple units — compound units already encode their denominator
            if (unit.Contains('/'))
                return unit;

            if (_dailyFrequencyPattern.IsMatch(trailingText))
            {
                return unit.ToLowerInvariant() switch
                {
                    "mg" => "mg/d",
                    "mcg" => "mcg/d",
                    _ => unit
                };
            }

            return unit;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether a treatment arm name represents a placebo arm.
        /// </summary>
        /// <param name="treatmentArm">Treatment arm name to check.</param>
        /// <returns>True if the arm name contains "placebo" (case-insensitive).</returns>
        private static bool isPlaceboArm(string? treatmentArm)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(treatmentArm))
                return false;

            return treatmentArm.Contains("placebo", StringComparison.OrdinalIgnoreCase)
                || treatmentArm.Contains("% Placebo", StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        #endregion Private Helpers
    }
}
