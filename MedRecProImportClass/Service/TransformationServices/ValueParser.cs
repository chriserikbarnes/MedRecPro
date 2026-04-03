using System.Text.RegularExpressions;
using MedRecProImportClass.Models;

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

        // Pattern 8: Range — 10.7 to 273
        private static readonly Regex _rangePattern = new(
            @"^([\d.]+)\s+to\s+([\d.]+)",
            RegexOptions.Compiled);

        // Pattern 9: Standalone % — 5% or 8.5%
        private static readonly Regex _standalonePercentPattern = new(
            @"^([\d.]+)\s*%$",
            RegexOptions.Compiled);

        // Pattern 10: n= — n=1401 or N=188
        private static readonly Regex _nEqualsPattern = new(
            @"^[Nn]\s*=\s*(\d+)$",
            RegexOptions.Compiled);

        // Pattern 11: P-value — p<0.05, P=0.001, <0.001, 0.0295
        private static readonly Regex _pValuePattern = new(
            @"^[Pp]?\s*([<>=≤≥])\s*(\d+\.?\d*)$",
            RegexOptions.Compiled);

        // Pattern 12: Plain number — 12.5, -3.2, 1,234
        private static readonly Regex _plainNumberPattern = new(
            @"^-?[\d,]+\.?\d*$",
            RegexOptions.Compiled);

        // Arm header pattern (parenthesized): DrugName(N=188)n(%) or Drug (n = 421) %
        // Supports uppercase/lowercase N and optional spaces around =
        private static readonly Regex _armHeaderPattern = new(
            @"^(.+?)\s*\([Nn]\s*=\s*(\d+)\)\s*(.*)$",
            RegexOptions.Compiled);

        // Arm header pattern (no parentheses): Placebo n = 51 % or Drug N=188 n(%)
        // Requires whitespace before N to avoid false matches on drug names ending in 'n'
        private static readonly Regex _armHeaderNoParenPattern = new(
            @"^(.+?)\s+[Nn]\s*=\s*(\d+)\s*(.*)$",
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
                    ParseConfidence = 0.8,
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

            // Pattern 8: Range — 10.7 to 273
            if (tryParseRange(text, out var rangeResult))
                return rangeResult;

            // Pattern 9: Standalone % — 5%
            if (tryParseStandalonePercent(text, out var pctResult))
                return pctResult;

            // Pattern 10: n= — n=1401
            if (tryParseNEquals(text, out var nEqResult))
                return nEqResult;

            // Pattern 11: P-value — p<0.05
            if (tryParsePValue(text, out var pvResult))
                return pvResult;

            // Pattern 12: Plain number — 12.5
            if (tryParsePlainNumber(text, out var numResult))
                return numResult;

            // Pattern 13: Text fallback
            return new ParsedValue
            {
                PrimaryValueType = "Text",
                TextValue = text,
                ParseConfidence = 0.5,
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
                SampleSize = int.TryParse(match.Groups[2].Value, out var n) ? n : null,
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
                    ParseConfidence = 0.8,
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
                    ParseConfidence = 1.0,
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
                ParseConfidence = 1.0,
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
                ParseConfidence = 1.0,
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
                ParseConfidence = 1.0,
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
                ParseConfidence = 1.0,
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
                ParseConfidence = 0.95,
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
                ParseConfidence = 0.95,
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
                ParseConfidence = 0.95,
                ParseRule = "value_plusminus_sample"
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
                ParseConfidence = 1.0,
                ParseRule = "value_cv"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses range pattern: 10.7 to 273.
        /// </summary>
        internal static bool tryParseRange(string text, out ParsedValue result)
        {
            #region implementation

            result = null!;
            var match = _rangePattern.Match(text);

            if (!match.Success)
                return false;

            result = new ParsedValue
            {
                LowerBound = double.Parse(match.Groups[1].Value),
                UpperBound = double.Parse(match.Groups[2].Value),
                BoundType = "Range",
                ParseConfidence = 0.9,
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
                ParseConfidence = 1.0,
                ParseRule = "percentage"
            };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses n= pattern: n=1401 or N=188.
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
                PrimaryValue = int.Parse(match.Groups[1].Value),
                PrimaryValueType = "SampleSize",
                ParseConfidence = 1.0,
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
                ParseConfidence = 1.0,
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
                ParseConfidence = 0.9,
                ParseRule = "plain_number"
            };
            return true;

            #endregion
        }

        #endregion Internal Pattern Methods
    }
}
