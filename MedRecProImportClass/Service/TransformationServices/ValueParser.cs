using System.Text.RegularExpressions;
using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices.Dictionaries;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Static utility for decomposing raw cell text into structured <see cref="ParsedValue"/>
    /// components in Stage 3 of the SPL Table Normalization pipeline. Handles 13+ cell formats
    /// including n(%), n/d(%), RR with CI, diff with CI, value(CV%), ranges, p-values,
    /// coded exclusions, and plain numbers.
    /// </summary>
    /// <remarks>
    /// ## Pattern Priority
    /// Patterns are evaluated in specificity order (first match wins):
    /// 1. Empty/NA/dash → IsExcluded
    /// 2. Coded exclusion (single letter A-Z)
    /// 3. n/d(%) fraction with percentage
    /// 4. n(%) count with percentage
    /// 5. RR with CI
    /// 6. Diff with CI
    /// 7. Value(CV%) — PK context
    /// 8. Range ("X to Y")
    /// 9. Standalone percentage ("X%")
    /// 10. n= sample size
    /// 11. P-value
    /// 12. Plain number
    /// 13. Text fallback
    ///
    /// ## Type Promotion
    /// The value parser returns <c>Numeric</c> for bare numbers. Parsers apply
    /// context-specific type promotion (e.g., bare numbers in AE tables → Percentage).
    ///
    /// ## Reuse
    /// Uses <see cref="Helpers.TextUtil.RemoveUnwantedTags"/> for HTML stripping
    /// and <see cref="Helpers.TextUtil.NormalizeXmlWhitespace"/> for whitespace normalization.
    /// </remarks>
    /// <seealso cref="ParsedValue"/>
    /// <seealso cref="ArmDefinition"/>
    /// <seealso cref="ParsedObservation"/>
    public static class ValueParser
    {
        #region Compiled Regex Patterns

        // Pattern 3: n/d(%) — 239/347 (69%) or 15/188(8.0%)
        private static readonly Regex _fracPctPattern = new(
            @"^(\d+)\s*/\s*(\d+)\s*\((\d+\.?\d*)\s*%?\s*\)$",
            RegexOptions.Compiled);

        // Pattern 4: n(%) — 33 (17.6) or 33 (17.6%)
        private static readonly Regex _nPctPattern = new(
            @"^(\d+)\s*\((\d+\.?\d*)\s*%?\s*\)$",
            RegexOptions.Compiled);

        // Pattern 5: RR with CI — 55%(29%, 71%) or 1.23(0.95, 1.55)
        private static readonly Regex _rrCiPattern = new(
            @"^(\d+\.?\d*)\s*%?\s*\(\s*(\d+\.?\d*)\s*%?\s*[,]\s*(\d+\.?\d*)\s*%?\s*\)$",
            RegexOptions.Compiled);

        // Pattern 6: Diff with CI — -4.4(-12.6, 3.8)
        private static readonly Regex _diffCiPattern = new(
            @"^(-?[\d.]+)\s*\(\s*(-?[\d.]+)\s*[,]\s*(-?[\d.]+)\s*\)$",
            RegexOptions.Compiled);

        // Pattern 6b: Value with CI — 0.38 (0.31 - 0.46) or 0.99 (0.91 to 1.08) or 1.23 (0.95–1.55)
        // Supports dash/en-dash/em-dash and "to" word separators with optional spaces
        private static readonly Regex _valueCiPattern = new(
            @"^(-?[\d.]+)\s*\(\s*(-?[\d.]+)\s*(?:[-–—]|to)\s*(-?[\d.]+)\s*\)$",
            RegexOptions.Compiled);

        // Pattern 6c: Value ± tolerance — 1.1 ± 0.5 or 580±450 or 71 +/- 40
        // Mean ± SD format common in PK tables. ± (U+00B1), +/-, +- supported.
        private static readonly Regex _valuePlusMinusPattern = new(
            @"^(-?[\d.]+)\s*(?:±|\+/?-)\s*(-?[\d.]+)$",
            RegexOptions.Compiled);

        // Pattern 6d: Value (±X) (n=N) — 0.80 (±0.36) (n=129) or 24.5 (±9.5)
        // Parenthesized ± format with optional trailing sample size.
        // ± (U+00B1), +/-, +- all supported, matching Pattern 6c convention.
        // SecondaryValueType intentionally null — resolved downstream from caption/header/footnote context.
        private static readonly Regex _valuePlusMinusSamplePattern = new(
            @"^(-?[\d.]+)\s*\(\s*(?:±|\+/?-)\s*(-?[\d.]+)\s*\)\s*(?:\(\s*[Nn]\s*=\s*(\d+)\s*\))?$",
            RegexOptions.Compiled);

        // Pattern 7: Value(CV%) — 0.29 (35%) or 125(32%)
        private static readonly Regex _valueCvPattern = new(
            @"^([\d.]+)\s*\((\d+)\s*%\s*\)\s*(.*)$",
            RegexOptions.Compiled);

        // Pattern 4b (R12): Decimal with parenthesized SD + optional footnote —
        // 3.9 (1.9), 17.4 (6.2)*, 0.44 (0.22)
        //
        // Differs from Pattern 4 (n_pct) by requiring a DECIMAL leading value (dot
        // required) — n_pct requires integer leading (\d+ only).
        // Differs from Pattern 6d (value_plusminus_sample) by NOT requiring the ± marker.
        // Differs from Pattern 7 (value_cv) by NOT allowing % inside the parentheses.
        // SecondaryValueType is left null — resolved downstream from caption/header/footnote.
        private static readonly Regex _valueParenDispersionPattern = new(
            @"^(-?\d+\.\d+)\s*\(\s*(-?\d+\.?\d*)\s*\)\s*[*†‡§¶#]?\s*$",
            RegexOptions.Compiled);

        // Pattern 8: Range — 10.7 to 273
        private static readonly Regex _rangePattern = new(
            @"^([\d.]+)\s+to\s+([\d.]+)",
            RegexOptions.Compiled);

        // Pattern 9: Standalone % — 5% or 8.5%
        private static readonly Regex _standalonePercentPattern = new(
            @"^([\d.]+)\s*%$",
            RegexOptions.Compiled);

        // Pattern 9b (Phase 2): Inequality percentage — <1%, ≤0.5%, < 1 %.
        // Matches an explicit upper-limit percentage, common in adverse-event tables
        // where the exact incidence is suppressed (e.g., reported as <1% when fewer
        // than 1% of subjects experienced the event). Tags as Percentage with an
        // INEQUALITY_UPPER validation flag so downstream consumers know the value is
        // an upper bound, not the exact incidence.
        private static readonly Regex _inequalityPercentPattern = new(
            @"^([<≤])\s*(\d+\.?\d*)\s*%$",
            RegexOptions.Compiled);

        // Pattern 4c (Phase 2): Compound count + inequality percent — 27 (<1%) or
        // 5 (≤0.5%). The count is the observed n; the percentage is an upper bound.
        // Decomposes to PrimaryValue=percentage with INEQUALITY_UPPER flag and
        // SecondaryValue=count, mirroring Pattern 4 (n_pct) for clean compound shape.
        private static readonly Regex _countInequalityPercentPattern = new(
            @"^(\d+)\s*\(\s*([<≤])\s*(\d+\.?\d*)\s*%\s*\)$",
            RegexOptions.Compiled);

        // Pattern 10: n= — n=1401 or N=188 or N=5,310 (footnote markers tolerated:
        // N = 100*, N = 112‡). Footnote markers are stripped before extraction; the
        // documentation footnote text is recovered separately by the parser.
        private static readonly Regex _nEqualsPattern = new(
            @"^[Nn]\s*=\s*(\d[\d,]*)\s*[*†‡§¶#]?\s*$",
            RegexOptions.Compiled);

        // Pattern 11: P-value — p<0.05, P=0.001, <0.001, 0.0295
        private static readonly Regex _pValuePattern = new(
            @"^[Pp]?\s*([<>=≤≥])\s*(\d+\.?\d*)$",
            RegexOptions.Compiled);

        // Pattern 12: Plain number — 12.5, -3.2, 1,234
        private static readonly Regex _plainNumberPattern = new(
            @"^-?[\d,]+\.?\d*$",
            RegexOptions.Compiled);

        // Pattern 12b (R12): Decimal / integer leading value followed by whitespace
        // and a recognized unit word — 71.8 hr, 5.5 mcg/mL, 1800 ng·h/mL.
        //
        // Built from a curated longest-first alternation of compound/time units
        // (minimum length 2) to avoid false-positive matches on narrative cells.
        // Single-letter units (h, L, g) are excluded — too easily matched against
        // drug suffixes or isolated characters. Only fires AFTER all other
        // numeric-literal patterns have failed, so "71.8" alone still falls through
        // to Pattern 12 (plain_number), not here.
        private static readonly Regex _valueTrailingUnitPattern = buildValueTrailingUnitPattern();

        /// <summary>
        /// Builds the trailing-unit regex from the curated PK unit alternation. Shares
        /// the candidate list with <see cref="UnitDictionary"/>'s inline scan but uses
        /// a stricter anchor: whole-cell match with exactly one leading number and one
        /// trailing unit, optional whitespace in between.
        /// </summary>
        private static Regex buildValueTrailingUnitPattern()
        {
            #region implementation

            // Curated longest-first. Mirrors UnitDictionary._inlineUnitPattern candidates
            // minus bare single-letter units. Order matters: "mcg·h/mL" must precede
            // "mcg" so the longer match wins.
            var units = new[]
            {
                // Composite AUC-style concentration units
                "mcg·h/mL", "ng·h/mL", "pg·h/mL", "µg·h/mL",
                "mcg·hr/mL", "ng·hr/mL", "pg·hr/mL", "µg·hr/mL",
                // Clearance + body-weight-normalized
                "mL/min/kg", "mg/kg/day", "mcg/kg/min",
                "L/h/kg", "mL/h/kg",
                // Concentrations
                "mcg/mL", "ng/mL", "pg/mL", "µg/mL",
                "mg/dL", "ng/dL", "mg/L",
                // Body-weight-normalized dose / volume
                "mg/kg", "mcg/kg", "mg/day", "mcg/day", "mg/m²",
                "mL/min", "L/kg", "L/h", "mg/h", "IU/mL", "mg/d",
                "ng/g", "mcg/g",
                // Time tokens — PK-specific (hours / minutes)
                "hrs", "hr", "min",
                // Percentage spellings
                "%CV", "%",
                // Pressure / physiology
                "beats/min", "mmHg", "mEq/L", "mOsm/kg"
            };

            var ordered = units
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(u => u.Length)
                .ThenBy(u => u, StringComparer.Ordinal);
            var alt = string.Join("|", ordered.Select(Regex.Escape));

            // Anchored whole-cell match: leading optional minus, decimal/integer,
            // at least one whitespace, recognized unit, optional trailing whitespace.
            return new Regex(
                @"^(-?\d+\.?\d*)\s+(" + alt + @")\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            #endregion
        }

        // Arm header pattern (parenthesized): DrugName(N=188)n(%) or Drug (n = 5,310) %
        // Supports uppercase/lowercase N, optional spaces around =, and comma-formatted numbers
        private static readonly Regex _armHeaderPattern = new(
            @"^(.+?)\s*\([Nn]\s*=\s*(\d[\d,]*)\)\s*(.*)$",
            RegexOptions.Compiled);

        // Arm header pattern (no parentheses): Placebo n = 51 % or CE n = 5,429
        // Requires whitespace before N to avoid false matches on drug names ending in 'n'
        private static readonly Regex _armHeaderNoParenPattern = new(
            @"^(.+?)\s+[Nn]\s*=\s*(\d[\d,]*)\s*(.*)$",
            RegexOptions.Compiled);

        // Footnote marker pattern for parameter name cleaning
        // Matches: (1) special symbols (†‡§¶#*) at any position, or
        // (2) single letters [a-g] only when preceded by a non-letter to avoid
        // false matches on words ending in a-g (e.g., "Headache" should not match "e")
        private static readonly Regex _trailingFootnotePattern = new(
            @"([*†‡§¶#](?:\s*,\s*[a-g*†‡§¶#])*|(?<=[^a-zA-Z])[a-g](?:\s*,\s*[a-g*†‡§¶#])*)$",
            RegexOptions.Compiled);

        // Empty/NA values
        private static readonly HashSet<string> _emptyValues = new(StringComparer.OrdinalIgnoreCase)
        {
            "", "N/A", "NA", "ND", "NR", "NC", "--", "—", "–", "-", "NDd"
        };

        #endregion Compiled Regex Patterns

        #region Public Methods

        /**************************************************************/
        /// <summary>
        /// Parses a raw cell text value into structured components. Tries 13 patterns
        /// in priority order, returning the first match.
        /// </summary>
        /// <param name="rawText">Raw cell text (already HTML-stripped by Stage 2).</param>
        /// <param name="armN">Optional arm sample size for PCT_CHECK validation.</param>
        /// <returns>Structured <see cref="ParsedValue"/> with typed components.</returns>
        /// <example>
        /// <code>
        /// var result = ValueParser.Parse("33 (17.6)", armN: 188);
        /// // result.PrimaryValue = 17.6, result.PrimaryValueType = "Percentage"
        /// // result.SecondaryValue = 33, result.SecondaryValueType = "Count"
        /// </code>
        /// </example>
        /// <seealso cref="ParsedValue"/>
        public static ParsedValue Parse(string? rawText, int? armN = null)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new ParsedValue
                {
                    IsExcluded = true,
                    ParseConfidence = ParsedValue.ConfidenceTier.KnownExclusion,
                    ParseRule = "empty_or_na"
                };
            }

            var text = rawText.Trim();

            // Pattern 1: Empty/NA/dash
            if (tryParseEmpty(text, out var emptyResult))
                return emptyResult;

            // Pattern 2: Coded exclusion (single letter A-Z)
            if (tryParseCodedExclusion(text, out var codedResult))
                return codedResult;

            // Pattern 3: n/d(%) — 239/347 (69%)
            if (tryParseFractionPercent(text, out var fracResult))
                return fracResult;

            // Pattern 4: n(%) — 33 (17.6)
            if (tryParseNPercent(text, armN, out var nPctResult))
                return nPctResult;

            // Pattern 4c (Phase 2): Compound count + inequality percent — 27 (<1%).
            // Runs immediately after Pattern 4 because both share the n(?) outer shape;
            // Pattern 4 requires a numeric inner group so the inequality form falls
            // through cleanly.
            if (tryParseCountInequalityPercent(text, out var countIneqResult))
                return countIneqResult;

            // Pattern 5: RR with CI — 55%(29%, 71%)
            if (tryParseRRWithCI(text, out var rrResult))
                return rrResult;

            // Pattern 6: Diff with CI — -4.4(-12.6, 3.8)
            if (tryParseDiffWithCI(text, out var diffResult))
                return diffResult;

            // Pattern 6b: Value with CI — 0.38 (0.31 - 0.46) or 0.99 (0.91 to 1.08)
            if (tryParseValueCI(text, out var ciResult))
                return ciResult;

            // Pattern 6c: Value ± tolerance — 1.1 ± 0.5 or 71 +/- 40
            if (tryParseValuePlusMinus(text, out var pmResult))
                return pmResult;

            // Pattern 6d: Value (±X) (n=N) — 0.80 (±0.36) (n=129)
            if (tryParseValuePlusMinusSample(text, out var pmSampleResult))
                return pmSampleResult;

            // Pattern 7: Value(CV%) — 0.29 (35%)
            if (tryParseValueCV(text, out var cvResult))
                return cvResult;

            // Pattern 4b (R12): Decimal with parenthesized SD — 3.9 (1.9), 17.4 (6.2)*
            // Runs AFTER n_pct / value_plusminus / value_cv so their guards win on
            // integer-leading / ± / % shapes; only fires on decimal-paren-no-% cells.
            if (tryParseValueParenDispersion(text, out var parenDispResult))
                return parenDispResult;

            // Pattern 8: Range — 10.7 to 273
            if (tryParseRange(text, out var rangeResult))
                return rangeResult;

            // Pattern 9: Standalone % — 5%
            if (tryParseStandalonePercent(text, out var pctResult))
                return pctResult;

            // Pattern 9b (Phase 2): Inequality percent — <1%, ≤0.5%. Must run after
            // Pattern 9 (standalone %) because a clean standalone percentage like "5%"
            // is more specific. Runs before Pattern 11 (p-value) so the trailing %
            // wins over the bare-inequality interpretation.
            if (tryParseInequalityPercent(text, out var ineqPctResult))
                return ineqPctResult;

            // Pattern 10: n= — n=1401
            if (tryParseNEquals(text, out var nEqResult))
                return nEqResult;

            // Pattern 11: P-value — p<0.05
            if (tryParsePValue(text, out var pvResult))
                return pvResult;

            // Pattern 12b (R12): Decimal + trailing unit word — 71.8 hr, 5.5 mcg/mL
            // Must run BEFORE plain_number so that "71.8 hr" produces a unit-bearing
            // result. Plain_number is whole-cell-anchored so it wouldn't match cells
            // with trailing content anyway, but this ordering makes the intent
            // explicit and supports future plain-number relaxations.
            if (tryParseValueTrailingUnit(text, out var trailUnitResult))
                return trailUnitResult;

            // Pattern 12: Plain number — 12.5
            if (tryParsePlainNumber(text, out var numResult))
                return numResult;

            // Pattern 13: Text fallback
            return new ParsedValue
            {
                PrimaryValueType = "Text",
                TextValue = text,
                ParseConfidence = ParsedValue.ConfidenceTier.TextFallback,
                ParseRule = "text_descriptive"
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a header cell text to extract treatment arm definition.
        /// Tries two patterns in order:
        /// 1. Parenthesized: <c>ArmName([Nn] = SampleSize)FormatHint</c>
        /// 2. No-parentheses: <c>ArmName [Nn] = SampleSize FormatHint</c>
        /// </summary>
        /// <param name="headerText">Header cell text.</param>
        /// <returns>
        /// An <see cref="ArmDefinition"/> if either pattern matches, or null if the text
        /// does not contain an arm definition.
        /// </returns>
        /// <example>
        /// <code>
        /// var arm1 = ValueParser.ParseArmHeader("EVISTA(N=2557)n(%)");
        /// // arm1.Name = "EVISTA", arm1.SampleSize = 2557, arm1.FormatHint = "n(%)"
        ///
        /// var arm2 = ValueParser.ParseArmHeader("Paroxetine (n = 421) %");
        /// // arm2.Name = "Paroxetine", arm2.SampleSize = 421, arm2.FormatHint = "%"
        ///
        /// var arm3 = ValueParser.ParseArmHeader("Placebo n = 51 %");
        /// // arm3.Name = "Placebo", arm3.SampleSize = 51, arm3.FormatHint = "%"
        /// </code>
        /// </example>
        /// <seealso cref="ArmDefinition"/>
        public static ArmDefinition? ParseArmHeader(string? headerText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(headerText))
                return null;

            var text = headerText.Trim();

            // Try parenthesized pattern first: "Drug (N=100) %" or "Drug (n = 421) %"
            var match = _armHeaderPattern.Match(text);
            if (match.Success)
                return buildArmFromMatch(match);

            // Try no-parentheses pattern: "Placebo n = 51 %"
            match = _armHeaderNoParenPattern.Match(text);
            if (match.Success)
                return buildArmFromMatch(match);

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds an <see cref="ArmDefinition"/> from a successful regex match.
        /// Group 1 = arm name, Group 2 = sample size, Group 3 = format hint.
        /// </summary>
        /// <param name="match">Successful regex match with 3 capture groups.</param>
        /// <returns>Populated <see cref="ArmDefinition"/>.</returns>
        private static ArmDefinition buildArmFromMatch(Match match)
        {
            #region implementation

            return new ArmDefinition
            {
                Name = match.Groups[1].Value.Trim(),
                SampleSize = int.TryParse(match.Groups[2].Value.Replace(",", ""), out var n) ? n : null,
                FormatHint = match.Groups[3].Value.Trim()
            };

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Strips trailing footnote markers from a parameter name.
        /// Returns the cleaned name and any extracted markers.
        /// </summary>
        /// <param name="text">Raw parameter name text.</param>
        /// <returns>Tuple of (cleaned name, comma-separated markers or null).</returns>
        /// <example>
        /// <code>
        /// var (name, markers) = ValueParser.CleanParameterName("Headache†,‡");
        /// // name = "Headache", markers = "†,‡"
        /// </code>
        /// </example>
        public static (string? cleanedName, string? markers) CleanParameterName(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return (text, null);

            var trimmed = text.Trim();
            var match = _trailingFootnotePattern.Match(trimmed);

            if (match.Success && match.Length <= 5)
            {
                var markers = match.Value.Trim();
                var idx = trimmed.LastIndexOf(markers[0]);
                var cleaned = trimmed[..idx].TrimEnd().TrimEnd(',').Trim();
                return (cleaned, markers);
            }

            return (trimmed, null);

            #endregion
        }

        #endregion Public Methods

        #region Internal Pattern Methods

        /**************************************************************/
        /// <summary>
        /// Checks for empty, NA, dash, or other excluded values.
        /// </summary>
        internal static bool tryParseEmpty(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;

            if (_emptyValues.Contains(text))
            {
                result = new ParsedValue
                {
                    IsExcluded = true,
                    ParseConfidence = ParsedValue.ConfidenceTier.KnownExclusion,
                    ParseRule = "empty_or_na"
                };
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Checks for single-letter coded exclusion (A-Z).
        /// EVISTA convention: A = placebo ≥ EVISTA, B = &lt;2%.
        /// </summary>
        internal static bool tryParseCodedExclusion(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;

            if (text.Length == 1 && char.IsUpper(text[0]))
            {
                result = new ParsedValue
                {
                    PrimaryValueType = "CodedExclusion",
                    TextValue = text,
                    IsExcluded = true,
                    ParseConfidence = ParsedValue.ConfidenceTier.Unambiguous,
                    ParseRule = "letter_code"
                };
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses n/d(%) pattern: 239/347 (69%).
        /// </summary>
        internal static bool tryParseFractionPercent(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _fracPctPattern.Match(text);

            if (!match.Success)
                return false;

            var numerator = int.Parse(match.Groups[1].Value);
            var denominator = int.Parse(match.Groups[2].Value);
            var pct = double.Parse(match.Groups[3].Value);

            // PCT_CHECK: derive n/d*100 and compare to reported %
            var derived = denominator > 0 ? Math.Round((double)numerator / denominator * 100, 1) : 0;
            var flag = Math.Abs(derived - pct) < 1.5
                ? "PCT_CHECK:PASS"
                : $"PCT_CHECK:WARN:{derived}";

            result = new ParsedValue
            {
                PrimaryValue = pct,
                PrimaryValueType = "Percentage",
                SecondaryValue = numerator,
                SecondaryValueType = "Count",
                Unit = "%",
                ParseConfidence = ParsedValue.ConfidenceTier.Unambiguous,
                ParseRule = "frac_pct",
                ValidationFlags = flag
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses n(%) pattern: 33 (17.6) or 33 (17.6%).
        /// Validates against armN when available.
        /// </summary>
        internal static bool tryParseNPercent(string text, int? armN, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _nPctPattern.Match(text);

            if (!match.Success)
                return false;

            var count = int.Parse(match.Groups[1].Value);
            var pct = double.Parse(match.Groups[2].Value);

            string? flag = null;
            if (armN.HasValue && armN.Value > 0)
            {
                var derived = Math.Round((double)count / armN.Value * 100, 1);
                flag = Math.Abs(derived - pct) < 1.5
                    ? "PCT_CHECK:PASS"
                    : $"PCT_CHECK:WARN:{derived}";
            }

            result = new ParsedValue
            {
                PrimaryValue = pct,
                PrimaryValueType = "Percentage",
                SecondaryValue = count,
                SecondaryValueType = "Count",
                Unit = "%",
                ParseConfidence = ParsedValue.ConfidenceTier.Unambiguous,
                ParseRule = "n_pct",
                ValidationFlags = flag
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses relative risk reduction with CI: 55%(29%, 71%).
        /// </summary>
        internal static bool tryParseRRWithCI(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _rrCiPattern.Match(text);

            if (!match.Success)
                return false;

            result = new ParsedValue
            {
                PrimaryValue = double.Parse(match.Groups[1].Value),
                PrimaryValueType = "RelativeRiskReduction",
                LowerBound = double.Parse(match.Groups[2].Value),
                UpperBound = double.Parse(match.Groups[3].Value),
                BoundType = "95CI",
                Unit = "%",
                ParseConfidence = ParsedValue.ConfidenceTier.Unambiguous,
                ParseRule = "rr_ci"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses difference with CI: -4.4(-12.6, 3.8).
        /// </summary>
        internal static bool tryParseDiffWithCI(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _diffCiPattern.Match(text);

            if (!match.Success)
                return false;

            result = new ParsedValue
            {
                PrimaryValue = double.Parse(match.Groups[1].Value),
                PrimaryValueType = "RiskDifference",
                LowerBound = double.Parse(match.Groups[2].Value),
                UpperBound = double.Parse(match.Groups[3].Value),
                BoundType = "95CI",
                Unit = "pp",
                ParseConfidence = ParsedValue.ConfidenceTier.Unambiguous,
                ParseRule = "diff_ci"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses value with confidence interval: 0.38 (0.31 - 0.46) or 0.99 (0.91 to 1.08).
        /// Supports dash (hyphen, en-dash, em-dash) and "to" word separators with optional spaces.
        /// Returns BoundType="CI" (generic, unspecified level) — caption hints or
        /// table context detection refine this to "90CI"/"95CI" downstream.
        /// </summary>
        /// <remarks>
        /// Validates lower &lt; upper to distinguish from malformed data.
        /// Does not conflict with existing patterns:
        /// - n_pct requires no separator inside parens
        /// - rr_ci/diff_ci require comma separator
        /// - value_cv requires % inside parens
        /// - range requires no leading value or parens (anchored to ^)
        /// </remarks>
        /// <example>
        /// <code>
        /// tryParseValueCI("0.38 (0.31 - 0.46)", out var r)   → true  (dash)
        /// tryParseValueCI("0.99 (0.91 to 1.08)", out var r)  → true  ("to" separator)
        /// tryParseValueCI("1.0 (0.94 – 1.13)", out var r)    → true  (en-dash)
        /// tryParseValueCI("0.38 (0.46 - 0.31)", out var r)   → false (lower > upper)
        /// </code>
        /// </example>
        /// <seealso cref="tryParseRRWithCI"/>
        /// <seealso cref="tryParseDiffWithCI"/>
        internal static bool tryParseValueCI(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _valueCiPattern.Match(text);

            if (!match.Success)
                return false;

            var primary = double.Parse(match.Groups[1].Value);
            var lower = double.Parse(match.Groups[2].Value);
            var upper = double.Parse(match.Groups[3].Value);

            // Validate: lower must be less than upper
            if (lower >= upper)
                return false;

            result = new ParsedValue
            {
                PrimaryValue = primary,
                PrimaryValueType = "Numeric",
                LowerBound = lower,
                UpperBound = upper,
                BoundType = "CI",
                ParseConfidence = ParsedValue.ConfidenceTier.ValidatedMatch,
                ParseRule = "value_ci"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses value ± tolerance pattern: 1.1 ± 0.5, 580±450, 71 +/- 40.
        /// Common Mean ± SD format in PK tables. Computes LowerBound and UpperBound
        /// from primary ± secondary.
        /// </summary>
        /// <remarks>
        /// Returns PrimaryValueType = "Numeric" (parser promotes to Mean in PK context).
        /// SecondaryValueType = "SD". BoundType = "SD" (standard deviation bounds, not CI).
        /// </remarks>
        /// <example>
        /// <code>
        /// tryParseValuePlusMinus("1.1 ± 0.5", out var r)   → true, Primary=1.1, Secondary=0.5, Lower=0.6, Upper=1.6
        /// tryParseValuePlusMinus("71 +/- 40", out var r)    → true, Primary=71, Secondary=40, Lower=31, Upper=111
        /// tryParseValuePlusMinus("580±450", out var r)       → true, Primary=580, Secondary=450, Lower=130, Upper=1030
        /// </code>
        /// </example>
        /// <seealso cref="tryParseValueCI"/>
        internal static bool tryParseValuePlusMinus(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _valuePlusMinusPattern.Match(text);

            if (!match.Success)
                return false;

            var primary = double.Parse(match.Groups[1].Value);
            var tolerance = double.Parse(match.Groups[2].Value);

            result = new ParsedValue
            {
                PrimaryValue = primary,
                PrimaryValueType = "Numeric",
                SecondaryValue = tolerance,
                SecondaryValueType = "SD",
                LowerBound = primary - tolerance,
                UpperBound = primary + tolerance,
                BoundType = "SD",
                ParseConfidence = ParsedValue.ConfidenceTier.ValidatedMatch,
                ParseRule = "value_plusminus"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses parenthesized value ± tolerance with optional sample size:
        /// 0.80 (±0.36) (n=129), 24.5 (±9.5), 1.8 (+/-1.3) (n=11).
        /// Common in PK tables where ± is enclosed in parentheses and a trailing (n=X)
        /// indicates the sample size for that measurement.
        /// </summary>
        /// <remarks>
        /// Returns <c>SecondaryValueType = null</c> and <c>BoundType = null</c> intentionally.
        /// The ± symbol may represent SD, SE, CI half-width, or other dispersion types.
        /// Type resolution is deferred to the parser which has access to caption, header path,
        /// and footnote context.
        /// </remarks>
        /// <example>
        /// <code>
        /// tryParseValuePlusMinusSample("0.80 (±0.36) (n=129)", out var r)
        ///   → true, Primary=0.80, Secondary=0.36, Lower=0.44, Upper=1.16, SampleSize=129
        /// tryParseValuePlusMinusSample("24.5 (±9.5)", out var r)
        ///   → true, Primary=24.5, Secondary=9.5, Lower=15.0, Upper=34.0, SampleSize=null
        /// </code>
        /// </example>
        /// <seealso cref="tryParseValuePlusMinus"/>
        internal static bool tryParseValuePlusMinusSample(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _valuePlusMinusSamplePattern.Match(text);

            if (!match.Success)
                return false;

            var primary = double.Parse(match.Groups[1].Value);
            var tolerance = double.Parse(match.Groups[2].Value);
            int? sampleSize = match.Groups[3].Success
                ? int.Parse(match.Groups[3].Value)
                : null;

            result = new ParsedValue
            {
                PrimaryValue = primary,
                PrimaryValueType = "Numeric",
                SecondaryValue = tolerance,
                SecondaryValueType = null,   // Resolved downstream from caption/header/footnote
                LowerBound = primary - tolerance,
                UpperBound = primary + tolerance,
                BoundType = null,            // Resolved downstream alongside SecondaryValueType
                SampleSize = sampleSize,
                ParseConfidence = ParsedValue.ConfidenceTier.ValidatedMatch,
                ParseRule = "value_plusminus_sample"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R12 — Parses decimal leading value with parenthesized SD (and optional
        /// trailing footnote marker): <c>3.9 (1.9)</c>, <c>17.4 (6.2)*</c>,
        /// <c>0.44 (0.22)</c>, <c>-2.5 (0.8)</c>.
        /// </summary>
        /// <remarks>
        /// ## Priority placement
        /// Runs AFTER Pattern 4 (n_pct), Pattern 6c (value_plusminus), Pattern 6d
        /// (value_plusminus_sample), and Pattern 7 (value_cv) so their
        /// integer-leading / ± / % guards fire first. Runs BEFORE Pattern 8
        /// (range) and later patterns.
        ///
        /// ## Why PrimaryValueType="Numeric" (not "Mean")
        /// The PK parser promotes Numeric → Mean via its PK context fallback
        /// (<c>parseAndApplyPkValue</c>). Leaving it Numeric here preserves
        /// semantic correctness for non-PK contexts where the same shape could
        /// appear.
        ///
        /// ## Why SecondaryValueType=null
        /// The value inside the parentheses may represent SD, SE, CI half-width,
        /// range, or other dispersion. Resolution happens downstream in
        /// <c>resolveDispersionType</c> using caption / header / footnote context
        /// (mirrors the pattern in <see cref="tryParseValuePlusMinusSample"/>).
        /// </remarks>
        /// <example>
        /// <code>
        /// tryParseValueParenDispersion("3.9 (1.9)", out var r)     → true, Primary=3.9, Secondary=1.9
        /// tryParseValueParenDispersion("17.4 (6.2)*", out var r)   → true, Primary=17.4, Secondary=6.2
        /// tryParseValueParenDispersion("33 (17.6%)", out var r)    → false (integer leading + %)
        /// </code>
        /// </example>
        /// <seealso cref="tryParseNPercent"/>
        /// <seealso cref="tryParseValuePlusMinusSample"/>
        /// <seealso cref="tryParseValueCV"/>
        internal static bool tryParseValueParenDispersion(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _valueParenDispersionPattern.Match(text);

            if (!match.Success)
                return false;

            if (!double.TryParse(match.Groups[1].Value, out var primary))
                return false;
            if (!double.TryParse(match.Groups[2].Value, out var secondary))
                return false;

            result = new ParsedValue
            {
                PrimaryValue = primary,
                PrimaryValueType = "Numeric",         // PK fallback promotes to Mean
                SecondaryValue = secondary,
                SecondaryValueType = null,             // Resolved downstream
                LowerBound = primary - secondary,
                UpperBound = primary + secondary,
                BoundType = null,                      // Resolved downstream alongside SecondaryValueType
                ParseConfidence = ParsedValue.ConfidenceTier.ValidatedMatch,
                ParseRule = "value_paren_dispersion"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R12 — Parses a decimal or integer value followed by a trailing unit word:
        /// <c>71.8 hr</c>, <c>5.5 mcg/mL</c>, <c>1800 ng·h/mL</c>. Unit is normalized
        /// via <see cref="UnitDictionary.TryNormalize"/> so variant spellings
        /// (<c>hr</c> → <c>h</c>) collapse to canonical form.
        /// </summary>
        /// <remarks>
        /// ## Priority placement
        /// Runs AFTER all existing numeric patterns (including CV%, range, p-value,
        /// n=) and BEFORE Pattern 12 (plain_number). Plain number is
        /// whole-cell-anchored and would not match cells with trailing content
        /// anyway, so ordering is primarily about correctness, not conflict.
        ///
        /// ## Unit recognition
        /// Only fires when the trailing token is a recognized PK unit. The regex
        /// itself enforces the alternation; no post-match dictionary lookup is
        /// required for membership. <c>TryNormalize</c> is called to canonicalize
        /// variants (e.g., <c>hr</c> → <c>h</c>); falls back to the raw matched
        /// token if somehow the normalization map misses.
        /// </remarks>
        /// <example>
        /// <code>
        /// tryParseValueTrailingUnit("71.8 hr", out var r)     → true, Primary=71.8, Unit="h"
        /// tryParseValueTrailingUnit("5.5 mcg/mL", out var r)  → true, Primary=5.5, Unit="mcg/mL"
        /// tryParseValueTrailingUnit("71.8 hello", out var r)  → false (unknown word)
        /// tryParseValueTrailingUnit("71.8", out var r)        → false (no trailing token)
        /// </code>
        /// </example>
        /// <seealso cref="UnitDictionary.TryNormalize"/>
        /// <seealso cref="tryParsePlainNumber"/>
        internal static bool tryParseValueTrailingUnit(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _valueTrailingUnitPattern.Match(text);

            if (!match.Success)
                return false;

            if (!double.TryParse(match.Groups[1].Value, out var primary))
                return false;

            var rawUnit = match.Groups[2].Value;
            var normalized = UnitDictionary.TryNormalize(rawUnit) ?? rawUnit;

            result = new ParsedValue
            {
                PrimaryValue = primary,
                PrimaryValueType = "Numeric",          // PK fallback promotes to Mean
                Unit = normalized,
                ParseConfidence = ParsedValue.ConfidenceTier.ValidatedMatch,
                ParseRule = "value_trailing_unit"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses value(CV%) pattern: 0.29 (35%) — typical PK format.
        /// </summary>
        internal static bool tryParseValueCV(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _valueCvPattern.Match(text);

            if (!match.Success)
                return false;

            result = new ParsedValue
            {
                PrimaryValue = double.Parse(match.Groups[1].Value),
                PrimaryValueType = "Mean",
                SecondaryValue = double.Parse(match.Groups[2].Value),
                SecondaryValueType = "CV_Percent",
                ParseConfidence = ParsedValue.ConfidenceTier.Unambiguous,
                ParseRule = "value_cv"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses range pattern: 10.7 to 273.
        /// </summary>
        /// <remarks>
        /// ## R15.1 — PrimaryValue midpoint synthesis
        /// Ranges carry legitimate numeric bounds but historically left
        /// <see cref="ParsedValue.PrimaryValue"/> null. Downstream analyses (and
        /// R13's PK analyzability filter) rely on <c>PrimaryValue</c> as a point
        /// estimate. The arithmetic midpoint <c>(lower + upper) / 2</c> is used
        /// as a reasonable central-tendency proxy, paired with
        /// <c>PrimaryValueType="Range"</c> so downstream consumers can
        /// distinguish midpoint-synthesized values from true point estimates
        /// and consult <c>LowerBound</c>/<c>UpperBound</c> when they need the
        /// actual interval.
        /// </remarks>
        internal static bool tryParseRange(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _rangePattern.Match(text);

            if (!match.Success)
                return false;

            var lower = double.Parse(match.Groups[1].Value);
            var upper = double.Parse(match.Groups[2].Value);

            result = new ParsedValue
            {
                // R15.1 — synthesize a PrimaryValue as the arithmetic midpoint so
                // range rows are analyzable downstream (previously PrimaryValue was
                // always null for ParseRule="range_to", causing the R13 filter and
                // cross-product analyses to treat them as unusable).
                PrimaryValue = (lower + upper) / 2.0,
                PrimaryValueType = "Range",
                LowerBound = lower,
                UpperBound = upper,
                BoundType = "Range",
                ParseConfidence = ParsedValue.ConfidenceTier.AmbiguousMatch,
                ParseRule = "range_to"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses standalone percentage: 5% or 8.5%.
        /// </summary>
        internal static bool tryParseStandalonePercent(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _standalonePercentPattern.Match(text);

            if (!match.Success)
                return false;

            result = new ParsedValue
            {
                PrimaryValue = double.Parse(match.Groups[1].Value),
                PrimaryValueType = "Percentage",
                Unit = "%",
                ParseConfidence = ParsedValue.ConfidenceTier.Unambiguous,
                ParseRule = "percentage"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses inequality-percentage cells: <c>&lt;1%</c>, <c>≤0.5%</c>. Tags as
        /// <c>Percentage</c> with <see cref="ParsedValue.PrimaryValue"/> set to the upper
        /// bound and <see cref="ParsedValue.ValidationFlags"/> = <c>INEQUALITY_UPPER:&lt;</c>
        /// (or <c>INEQUALITY_UPPER:≤</c>) so downstream consumers know the value is an
        /// upper limit, not the exact rate.
        /// </summary>
        /// <remarks>
        /// Common in adverse-event tables where exact incidence is suppressed below a
        /// reporting threshold. Without this pattern the cell falls to text fallback,
        /// driving the row under the 0.75 quality gate via the <c>PrimaryValueTypeText</c>
        /// + <c>PrimaryValueNull</c> hard penalties.
        /// </remarks>
        internal static bool tryParseInequalityPercent(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _inequalityPercentPattern.Match(text);

            if (!match.Success)
                return false;

            if (!double.TryParse(match.Groups[2].Value, out var bound))
                return false;

            result = new ParsedValue
            {
                PrimaryValue = bound,
                PrimaryValueType = "Percentage",
                Unit = "%",
                ParseConfidence = ParsedValue.ConfidenceTier.Unambiguous,
                ParseRule = "inequality_percent",
                ValidationFlags = "INEQUALITY_UPPER:" + match.Groups[1].Value
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses compound count + inequality-percent cells: <c>27 (&lt;1%)</c>,
        /// <c>5 (≤0.5%)</c>. The integer is the observed n; the percentage is an upper
        /// bound. Mirrors Pattern 4 (n_pct) for clean count-with-percentage shape but
        /// preserves the inequality semantics in <see cref="ParsedValue.ValidationFlags"/>.
        /// </summary>
        internal static bool tryParseCountInequalityPercent(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _countInequalityPercentPattern.Match(text);

            if (!match.Success)
                return false;

            if (!int.TryParse(match.Groups[1].Value, out var count))
                return false;
            if (!double.TryParse(match.Groups[3].Value, out var bound))
                return false;

            result = new ParsedValue
            {
                PrimaryValue = bound,
                PrimaryValueType = "Percentage",
                SecondaryValue = count,
                SecondaryValueType = "Count",
                Unit = "%",
                ParseConfidence = ParsedValue.ConfidenceTier.Unambiguous,
                ParseRule = "count_inequality_percent",
                ValidationFlags = "INEQUALITY_UPPER:" + match.Groups[2].Value
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses n= pattern: n=1401 or N=188 (footnote markers tolerated: N = 100*).
        /// </summary>
        internal static bool tryParseNEquals(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _nEqualsPattern.Match(text);

            if (!match.Success)
                return false;

            result = new ParsedValue
            {
                PrimaryValue = int.Parse(match.Groups[1].Value.Replace(",", "")),
                PrimaryValueType = "SampleSize",
                ParseConfidence = ParsedValue.ConfidenceTier.Unambiguous,
                ParseRule = "n_equals"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses p-value patterns: p&lt;0.05, P=0.001, &lt;0.001.
        /// </summary>
        internal static bool tryParsePValue(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _pValuePattern.Match(text);

            if (!match.Success)
                return false;

            if (!double.TryParse(match.Groups[2].Value, out var pval))
                return false;

            result = new ParsedValue
            {
                PrimaryValue = pval,
                PrimaryValueType = "PValue",
                PValue = pval,
                PValueQualifier = match.Groups[1].Value,
                ParseConfidence = ParsedValue.ConfidenceTier.Unambiguous,
                ParseRule = "pvalue"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses plain numbers: 12.5, -3.2, 1,234.
        /// </summary>
        internal static bool tryParsePlainNumber(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _plainNumberPattern.Match(text);

            if (!match.Success)
                return false;

            if (!double.TryParse(text.Replace(",", ""), out var val))
                return false;

            result = new ParsedValue
            {
                PrimaryValue = val,
                PrimaryValueType = "Numeric",
                ParseConfidence = ParsedValue.ConfidenceTier.AmbiguousMatch,
                ParseRule = "plain_number"
            };
            return true;

            #endregion
        }

        #endregion Internal Pattern Methods
    }
}
