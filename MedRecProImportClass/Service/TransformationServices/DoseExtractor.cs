using System.Globalization;
using System.Text.RegularExpressions;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Static utility for extracting structured <c>Dose</c> and <c>DoseUnit</c>
    /// from free-text dose descriptions found in observation context columns.
    /// </summary>
    /// <remarks>
    /// ## Extraction Priority
    /// 1. Range/titration patterns -> max dose (for example, <c>10-20 mg</c> -> 20)
    /// 2. Core dose+unit pattern (for example, <c>600 mg/d</c> -> 600 mg/d)
    /// 3. Frequency promotion for once-daily simple units
    ///
    /// ## Structural rule
    /// Every dose unit must have a left numerator. Bare-denominator tokens
    /// such as <c>/mcL</c> or <c>/μL</c> are clinical-lab COUNT units, not
    /// dose units, and are intentionally excluded from extraction here.
    /// They are recognized by
    /// <c>PopulationDetector.LooksLikeLabThresholdDoseModification</c>.
    ///
    /// Clinical lab CONCENTRATION units such as <c>g/dL</c> and <c>mg/dL</c>
    /// have a left numerator and are recognized structurally; the dosing
    /// parser intercepts them as lab thresholds at row level so the value is
    /// not committed as <see cref="ParsedObservation.Dose"/>.
    /// </remarks>
    /// <seealso cref="ParsedObservation"/>
    /// <seealso cref="ArmDefinition"/>
    /// <seealso cref="UnitDictionary.DoseUnitAlternation"/>
    public static class DoseExtractor
    {
        #region Compiled Regex Patterns

        private const string DoseNumberPattern = @"(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d+)?";

        /**************************************************************/
        /// <summary>
        /// Wraps <see cref="UnitDictionary.DoseUnitAlternation"/> with the
        /// trailing positive lookahead that asserts a unit is followed by
        /// end-of-string, whitespace, common punctuation, or the <c>/day</c>
        /// boundary. The boundary stops <c>"5 mg"</c> from matching inside
        /// <c>"5 mgxyz"</c>.
        /// </summary>
        private static readonly string DoseUnitPattern =
            $@"(?:{UnitDictionary.DoseUnitAlternation})(?=$|[\s,;.)\]]|/d(?:ay)?\b)";


        /**************************************************************/
        /// <summary>
        /// Matches dose range/titration patterns such as <c>10-20 mg</c>,
        /// <c>150-600 mg/d</c>, and <c>150 to 600 mg</c>.
        /// </summary>
        private static readonly Regex _rangePattern = new(
            $@"({DoseNumberPattern})\s*(?:to|[-\u2013\u2014])\s*({DoseNumberPattern})\s*({DoseUnitPattern})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Core dose extraction such as <c>600 mg/d</c>, <c>50 mg</c>,
        /// <c>1 mg/kg</c>, <c>250 mg/5 mL</c>, and <c>50,000/mcL</c>.
        /// </summary>
        private static readonly Regex _coreDosePattern = new(
            $@"({DoseNumberPattern})\s*({DoseUnitPattern})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Detects once-daily frequency indicators in trailing text after the
        /// dose+unit. Promotes simple mg/mcg units to daily form.
        /// </summary>
        private static readonly Regex _dailyFrequencyPattern = new(
            @"\b(?:once\s+daily|daily|per\s+day|QD|q\.?d\.?|every\s+day|each\s+day|/day)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Footnote markers to strip before extraction. Superscript digits are
        /// excluded because they appear in scientific units such as mg/m2.
        /// </summary>
        private static readonly Regex _footnotePattern = new(
            @"[\u2020\u2021\u00A7\u00B6*\u207A\u207B]+",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Detects "Any dose" or "All doses" labels, which are not extractable.
        /// </summary>
        private static readonly Regex _anyDosePattern = new(
            @"\b(?:any|all)\s+dose",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Percentage arm pattern: <c>% 10 mg</c>, <c>% 40 mg</c>.
        /// </summary>
        private static readonly Regex _percentArmPattern = new(
            $@"^%\s+({DoseNumberPattern})\s*({DoseUnitPattern})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Matches the numeric dose token plus trailing schedule text in a
        /// compound "Drug + Dose" row label.
        /// </summary>
        private static readonly Regex _doseFragmentPattern = new(
            @"\s*\b\d[\d,]*(?:\.\d+)?\s*(?:mg|mcg|\u00B5g|\u03BCg|g|ng|kg|mL|units?|U|IU)\b.*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Compiled Regex Patterns

        #region Public Methods

        /**************************************************************/
        /// <summary>
        /// Extracts a structured (Dose, DoseUnit) tuple from free-text dose
        /// descriptions.
        /// </summary>
        /// <remarks>
        /// Returns both values as null when no dose is recoverable. Range inputs
        /// return the high/target value; simple once-daily regimens normalize
        /// <c>mg</c> to <c>mg/d</c> and <c>mcg</c> to <c>mcg/d</c>.
        /// </remarks>
        /// <param name="text">Free-text dose description from DoseRegimen, TreatmentArm, etc.</param>
        /// <returns>Tuple of (dose, doseUnit). Both null when no dose is recoverable.</returns>
        /// <example>
        /// <code>
        /// var (dose, unit) = DoseExtractor.Extract("600 mg/d");
        /// // dose = 600m, unit = "mg/d"
        /// </code>
        /// </example>
        public static (decimal? dose, string? doseUnit) Extract(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return (null, null);

            if (_anyDosePattern.IsMatch(text))
                return (null, null);

            if (!text.Any(char.IsDigit))
                return (null, null);

            var cleaned = _footnotePattern.Replace(text, "").Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
                return (null, null);

            var pctMatch = _percentArmPattern.Match(cleaned);
            if (pctMatch.Success && tryParseDose(pctMatch.Groups[1].Value, out var pctDose))
            {
                var pctUnit = NormalizeUnit(pctMatch.Groups[2].Value);
                return (pctDose, pctUnit);
            }

            var rangeMatch = _rangePattern.Match(cleaned);
            if (rangeMatch.Success && tryParseDose(rangeMatch.Groups[2].Value, out var highDose))
            {
                var unit = NormalizeUnit(rangeMatch.Groups[3].Value);
                var trailing = cleaned[(rangeMatch.Index + rangeMatch.Length)..];
                unit = promoteUnitIfDaily(unit, trailing);
                return (highDose, unit);
            }

            var coreMatch = _coreDosePattern.Match(cleaned);
            if (coreMatch.Success && tryParseDose(coreMatch.Groups[1].Value, out var dose))
            {
                var unit = NormalizeUnit(coreMatch.Groups[2].Value);
                var trailing = cleaned[(coreMatch.Index + coreMatch.Length)..];
                unit = promoteUnitIfDaily(unit, trailing);
                return (dose, unit);
            }

            return (null, null);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// True when <paramref name="text"/> contains a dose range/titration
        /// expression recognized by <see cref="Extract"/>.
        /// </summary>
        /// <param name="text">Candidate dose text.</param>
        /// <returns>True when a recognized dose range is present.</returns>
        public static bool LooksLikeDoseRange(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return false;

            return _rangePattern.IsMatch(text);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Strips the numeric dose fragment and any trailing schedule text from
        /// a compound "drug + dose" row label, returning just the drug-name prefix.
        /// </summary>
        /// <param name="input">Candidate row-label text.</param>
        /// <returns>Drug-name prefix with the trailing dose fragment removed.</returns>
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
        /// </summary>
        /// <param name="unit">Raw unit string to normalize.</param>
        /// <returns>Normalized unit or null if input is null/whitespace.</returns>
        public static string? NormalizeUnit(string? unit)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(unit))
                return null;

            var normalized = _footnotePattern.Replace(unit, "").Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            normalized = normalized
                .Replace("\u00C2\u00B2", "\u00B2")
                .Replace("\u00B5g", "mcg")
                .Replace("\u03BCg", "mcg");
            normalized = Regex.Replace(normalized, @"/\s*day\b", "/d", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"/\s*(?:hour|hr)\b", "/h", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"^units?$", "U", RegexOptions.IgnoreCase);

            return normalized;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Backfills placebo arms with Dose=0.0 and DoseUnit inherited from
        /// non-placebo treatment arms in the same table.
        /// </summary>
        /// <param name="observations">All observations in the batch, modified in place.</param>
        public static void BackfillPlaceboArms(List<ParsedObservation> observations)
        {
            #region implementation

            var tableGroups = observations
                .Where(o => o.TextTableID.HasValue)
                .GroupBy(o => o.TextTableID!.Value);

            foreach (var group in tableGroups)
            {
                var nonPlaceboUnits = group
                    .Where(o => !isPlaceboArm(o.TreatmentArm) && !string.IsNullOrEmpty(o.DoseUnit))
                    .Select(o => o.DoseUnit!)
                    .ToList();

                if (nonPlaceboUnits.Count == 0)
                    continue;

                var majorityUnit = nonPlaceboUnits
                    .GroupBy(u => u, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count())
                    .First()
                    .Key;

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
        /// </summary>
        /// <param name="obs">Observation to scan, modified in place when a dose is found.</param>
        /// <returns>True if Dose/DoseUnit were populated; otherwise false.</returns>
        public static bool ScanAllColumnsForDose(ParsedObservation obs)
        {
            #region implementation

            if (obs.Dose.HasValue)
                return false;

            if (isPlaceboArm(obs.TreatmentArm))
                return false;

            var columnsToScan = new (string? value, string label)[]
            {
                (obs.DoseRegimen, "DOSE_REGIMEN"),
                (obs.TreatmentArm, "TREATMENT_ARM"),
                (obs.ParameterName, "PARAMETER_NAME"),
                (obs.ParameterSubtype, "PARAMETER_SUBTYPE"),
                (obs.StudyContext, "STUDY_CONTEXT"),
            };

            foreach (var (value, _) in columnsToScan)
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
        /// Parses decimal dose text while accepting comma-formatted thousands.
        /// </summary>
        private static bool tryParseDose(string raw, out decimal dose)
        {
            #region implementation

            return decimal.TryParse(
                raw.Replace(",", ""),
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out dose);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Promotes a simple unit to daily form when trailing text contains a
        /// once-daily frequency indicator.
        /// </summary>
        private static string? promoteUnitIfDaily(string? unit, string trailingText)
        {
            #region implementation

            if (unit == null || string.IsNullOrWhiteSpace(trailingText))
                return unit;

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
