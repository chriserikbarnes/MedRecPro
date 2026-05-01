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
            $@"(?:{UnitDictionary.DoseUnitAlternation})(?=$|[\s,;.)\]]|/\s*d(?:ay)?\b)";


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
            @"\b(?:once\s+daily|daily|per\s+day|QD|q\.?d\.?|every\s+day|each\s+day)\b|/\s*(?:d|day)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Detects multi-dose daily schedules that must not collapse into a
        /// daily unit such as <c>mg/d</c>.
        /// </summary>
        private static readonly Regex _multiDailyFrequencyPattern = new(
            @"\b(?:BID|b\.?i\.?d\.?|TID|t\.?i\.?d\.?|QID|q\.?i\.?d\.?|twice\s+daily|two\s+times\s+daily|three\s+times\s+daily|four\s+times\s+daily|[234]\s+times\s+(?:a\s+)?day)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Matches trailing frequency text that should be retained in a
        /// recovered <see cref="ParsedObservation.DoseRegimen"/>.
        /// </summary>
        private static readonly Regex _doseRegimenFrequencyPattern = new(
            @"^\s*(?:once\s+daily|daily|per\s+day|QD|q\.?d\.?|every\s+day|each\s+day|BID|b\.?i\.?d\.?|TID|t\.?i\.?d\.?|QID|q\.?i\.?d\.?|twice\s+daily|two\s+times\s+daily|three\s+times\s+daily|four\s+times\s+daily|[234]\s+times\s+(?:a\s+)?day)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Matches an ingredient phrase immediately following a dose unit, such
        /// as <c>of Testosterone</c> in a treatment-arm label.
        /// </summary>
        private static readonly Regex _doseRegimenIngredientPhrasePattern = new(
            @"^\s+of\s+(.+?)(?=(?:\)\s*)?\s+(?:once\s+daily|daily|per\s+day|QD|q\.?d\.?|every\s+day|each\s+day|BID|b\.?i\.?d\.?|TID|t\.?i\.?d\.?|QID|q\.?i\.?d\.?|twice\s+daily|two\s+times\s+daily|three\s+times\s+daily|four\s+times\s+daily|[234]\s+times\s+(?:a\s+)?day)\b|$|[,;.])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// Matches explicit per-day suffixes that may be separated from the unit
        /// by whitespace in SPL headers.
        /// </summary>
        private static readonly Regex _perDaySuffixPattern = new(
            @"^\s*/\s*(?:d|day)\b",
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

        /**************************************************************/
        /// <summary>
        /// Dose units that are safe to recover from AE treatment-arm labels.
        /// </summary>
        /// <remarks>
        /// AE arm scanning is intentionally narrower than PK/dosing extraction:
        /// percentage, time, count, and lab-threshold units are not valid arm-dose
        /// evidence. Explicit <see cref="ParsedObservation.DoseRegimen"/> values
        /// still use the broader extractor, except that percent units are rejected
        /// for AE rows.
        /// </remarks>
        private static readonly HashSet<string> _aeTreatmentArmDoseUnits = new(StringComparer.OrdinalIgnoreCase)
        {
            "mg", "mg/d", "mg/day", "mg/kg", "mg/kg/day", "mg/mL", "mg/m²", "mg/m^2",
            "mcg", "mcg/d", "mcg/day", "mcg/kg", "mcg/kg/min",
            "g", "ng", "mL", "mL/kg", "mL/h", "mL/hr", "mg/5 mL",
            "IU", "IU/mL", "U"
        };

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
        /// <remarks>
        /// AE incidence percentages must not become placebo dose metadata. When
        /// the majority unit in an adverse-event table is <c>%</c>, placebo rows
        /// are left without a synthetic dose.
        /// </remarks>
        /// <param name="observations">All observations in the batch, modified in place.</param>
        public static void BackfillPlaceboArms(List<ParsedObservation> observations)
        {
            #region implementation

            var tableGroups = observations
                .Where(o => o.TextTableID.HasValue)
                .GroupBy(o => o.TextTableID!.Value);

            foreach (var group in tableGroups)
            {
                var isAdverseEventGroup = group.Any(o => isAdverseEvent(o.TableCategory));
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

                if (isAdverseEventGroup && isPercentDoseUnit(majorityUnit))
                    continue;

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
        /// Scans text columns of an observation for misplaced dose patterns.
        /// </summary>
        /// <remarks>
        /// ADVERSE_EVENT rows use a narrower source policy: never scan
        /// StudyContext, ParameterName, ParameterCategory, or ParameterSubtype;
        /// reject percent dose units; and only recover treatment-arm doses when
        /// the unit is an explicit drug-dose unit.
        /// </remarks>
        /// <param name="obs">Observation to scan, modified in place when a dose is found.</param>
        /// <returns>True if Dose, DoseUnit, or DoseRegimen were populated; otherwise false.</returns>
        public static bool ScanAllColumnsForDose(ParsedObservation obs)
        {
            #region implementation

            if (obs.Dose.HasValue && !isAdverseEvent(obs.TableCategory))
                return false;

            if (isPlaceboArm(obs.TreatmentArm))
                return false;

            if (isAdverseEvent(obs.TableCategory))
            {
                if (tryApplyExtractedDose(
                    obs,
                    obs.DoseRegimen,
                    allowPercentDoseUnit: false,
                    requireAeTreatmentArmDoseUnit: false,
                    populateDoseRegimen: false,
                    allowDoseUnitCorrection: false))
                {
                    return true;
                }

                if (tryApplyExtractedDose(
                    obs,
                    obs.TreatmentArm,
                    allowPercentDoseUnit: false,
                    requireAeTreatmentArmDoseUnit: true,
                    populateDoseRegimen: true,
                    allowDoseUnitCorrection: true))
                {
                    return true;
                }

                return false;
            }

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

                if (tryApplyExtractedDose(
                    obs,
                    value,
                    allowPercentDoseUnit: true,
                    requireAeTreatmentArmDoseUnit: false,
                    populateDoseRegimen: false,
                    allowDoseUnitCorrection: false))
                {
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
        /// Extracts dose data from a candidate source and applies the row-specific
        /// guardrails before mutating the observation.
        /// </summary>
        /// <param name="obs">Observation to populate.</param>
        /// <param name="value">Candidate text source.</param>
        /// <param name="allowPercentDoseUnit">Whether <c>%</c> may populate DoseUnit.</param>
        /// <param name="requireAeTreatmentArmDoseUnit">Whether to require the AE treatment-arm unit allowlist.</param>
        /// <param name="populateDoseRegimen">Whether to recover a regimen fragment from the candidate text.</param>
        /// <param name="allowDoseUnitCorrection">Whether to correct an existing DoseUnit from treatment-arm evidence.</param>
        /// <returns>True when any dose field was assigned or corrected.</returns>
        private static bool tryApplyExtractedDose(
            ParsedObservation obs,
            string? value,
            bool allowPercentDoseUnit,
            bool requireAeTreatmentArmDoseUnit,
            bool populateDoseRegimen,
            bool allowDoseUnitCorrection)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var (dose, doseUnit) = Extract(value);
            if (!dose.HasValue)
                return false;

            if (!allowPercentDoseUnit && isPercentDoseUnit(doseUnit))
                return false;

            if (requireAeTreatmentArmDoseUnit && !isAeTreatmentArmDoseUnit(doseUnit))
                return false;

            var changed = false;
            if (!obs.Dose.HasValue)
            {
                obs.Dose = dose;
                changed = true;
            }

            if (shouldApplyDoseUnit(obs.DoseUnit, doseUnit, value, allowDoseUnitCorrection))
            {
                obs.DoseUnit = doseUnit;
                changed = true;
            }

            if (populateDoseRegimen &&
                string.IsNullOrWhiteSpace(obs.DoseRegimen) &&
                tryExtractDoseRegimenFragment(value, out var doseRegimen))
            {
                obs.DoseRegimen = doseRegimen;
                changed = true;
            }

            return changed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether an extracted dose unit should be applied to the
        /// observation.
        /// </summary>
        /// <param name="currentDoseUnit">Existing dose unit.</param>
        /// <param name="extractedDoseUnit">Dose unit extracted from source text.</param>
        /// <param name="sourceText">Source text used for extraction.</param>
        /// <param name="allowDoseUnitCorrection">Whether existing units may be corrected.</param>
        /// <returns><c>true</c> when the extracted unit should replace the current value.</returns>
        private static bool shouldApplyDoseUnit(
            string? currentDoseUnit,
            string? extractedDoseUnit,
            string sourceText,
            bool allowDoseUnitCorrection)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(extractedDoseUnit))
                return false;

            if (string.IsNullOrWhiteSpace(currentDoseUnit))
                return true;

            if (!allowDoseUnitCorrection ||
                string.Equals(currentDoseUnit, extractedDoseUnit, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return isDailyDoseUnit(currentDoseUnit) &&
                   !isDailyDoseUnit(extractedDoseUnit) &&
                   _multiDailyFrequencyPattern.IsMatch(sourceText);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when a normalized dose unit is a percent sign.
        /// </summary>
        private static bool isPercentDoseUnit(string? doseUnit)
        {
            #region implementation

            return string.Equals(doseUnit, "%", StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when a dose unit is safe evidence inside an AE treatment arm.
        /// </summary>
        private static bool isAeTreatmentArmDoseUnit(string? doseUnit)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(doseUnit) &&
                   _aeTreatmentArmDoseUnits.Contains(doseUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when the dose unit already encodes a per-day denominator.
        /// </summary>
        /// <param name="doseUnit">Candidate dose unit.</param>
        /// <returns><c>true</c> for <c>mg/d</c>, <c>mg/day</c>, and equivalents.</returns>
        private static bool isDailyDoseUnit(string? doseUnit)
        {
            #region implementation

            return !string.IsNullOrWhiteSpace(doseUnit) &&
                   Regex.IsMatch(doseUnit, @"/\s*d(?:ay)?\b", RegexOptions.IgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns true when the observation belongs to the adverse-event contract.
        /// </summary>
        private static bool isAdverseEvent(string? tableCategory)
        {
            #region implementation

            return string.Equals(tableCategory, "ADVERSE_EVENT", StringComparison.OrdinalIgnoreCase);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recovers the dose-regimen fragment embedded in an AE treatment-arm label.
        /// </summary>
        /// <param name="value">Treatment-arm source text.</param>
        /// <param name="doseRegimen">Recovered regimen fragment.</param>
        /// <returns><c>true</c> when a regimen fragment was recovered.</returns>
        private static bool tryExtractDoseRegimenFragment(string? value, out string? doseRegimen)
        {
            #region implementation

            doseRegimen = null;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var cleaned = _footnotePattern.Replace(value, "").Trim();
            var coreMatch = _coreDosePattern.Match(cleaned);
            if (!coreMatch.Success || !tryParseDose(coreMatch.Groups[1].Value, out _))
                return false;

            var numberText = coreMatch.Groups[1].Value.Replace(",", "");
            var unit = NormalizeUnit(coreMatch.Groups[2].Value) ?? coreMatch.Groups[2].Value.Trim();
            var cursor = coreMatch.Index + coreMatch.Length;
            var remainder = cleaned[cursor..];

            if (tryConsumePerDaySuffix(remainder, out var perDayConsumed))
            {
                unit = normalizeDailyUnitForRegimen(unit);
                cursor += perDayConsumed;
                remainder = cleaned[cursor..];
            }

            var parts = new List<string> { $"{numberText} {unit}" };
            if (tryConsumeIngredientPhrase(remainder, out var ingredientPhrase, out var ingredientConsumed))
            {
                parts.Add(ingredientPhrase!);
                cursor += ingredientConsumed;
                remainder = cleaned[cursor..];
            }

            if (tryConsumeFrequencyPhrase(remainder, out var frequencyPhrase))
                parts.Add(frequencyPhrase!);

            doseRegimen = Regex.Replace(string.Join(" ", parts), @"\s+", " ").Trim();
            return !string.IsNullOrWhiteSpace(doseRegimen);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Consumes a spaced or compact per-day suffix after a simple dose unit.
        /// </summary>
        /// <param name="text">Trailing source text.</param>
        /// <param name="consumed">Character count consumed.</param>
        /// <returns><c>true</c> when a per-day suffix was consumed.</returns>
        private static bool tryConsumePerDaySuffix(string text, out int consumed)
        {
            #region implementation

            consumed = 0;
            var match = _perDaySuffixPattern.Match(text);
            if (!match.Success)
                return false;

            consumed = match.Length;
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes a regimen-display unit to an explicit <c>/day</c> suffix.
        /// </summary>
        /// <param name="unit">Dose unit from the embedded regimen.</param>
        /// <returns>Display unit suitable for <see cref="ParsedObservation.DoseRegimen"/>.</returns>
        private static string normalizeDailyUnitForRegimen(string unit)
        {
            #region implementation

            var normalized = NormalizeUnit(unit) ?? unit.Trim();
            return Regex.IsMatch(normalized, @"/\s*d(?:ay)?\b", RegexOptions.IgnoreCase)
                ? Regex.Replace(normalized, @"/\s*d\b", "/day", RegexOptions.IgnoreCase)
                : $"{normalized}/day";

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Consumes an ingredient phrase that appears between a dose unit and a
        /// frequency phrase.
        /// </summary>
        /// <param name="text">Trailing source text.</param>
        /// <param name="ingredientPhrase">Recovered phrase.</param>
        /// <param name="consumed">Character count consumed.</param>
        /// <returns><c>true</c> when an ingredient phrase was consumed.</returns>
        private static bool tryConsumeIngredientPhrase(string text, out string? ingredientPhrase, out int consumed)
        {
            #region implementation

            ingredientPhrase = null;
            consumed = 0;
            var match = _doseRegimenIngredientPhrasePattern.Match(text);
            if (!match.Success)
                return false;

            ingredientPhrase = $"of {match.Groups[1].Value.Trim().TrimEnd(')')}";
            consumed = match.Length;

            var closingMatch = Regex.Match(text[consumed..], @"^\s*\)");
            if (closingMatch.Success)
                consumed += closingMatch.Length;

            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Consumes a frequency phrase after a dose unit or ingredient phrase.
        /// </summary>
        /// <param name="text">Trailing source text.</param>
        /// <param name="frequencyPhrase">Recovered frequency phrase.</param>
        /// <returns><c>true</c> when a frequency phrase was consumed.</returns>
        private static bool tryConsumeFrequencyPhrase(string text, out string? frequencyPhrase)
        {
            #region implementation

            frequencyPhrase = null;
            var match = _doseRegimenFrequencyPattern.Match(text);
            if (!match.Success)
                return false;

            frequencyPhrase = Regex.Replace(match.Value.Trim(), @"\s+", " ");
            return true;

            #endregion
        }

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

            if (_multiDailyFrequencyPattern.IsMatch(trailingText))
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
