using System.Globalization;
using System.Text.RegularExpressions;

namespace MedRecProImportClass.Service.TransformationServices.SampleSize
{
    /**************************************************************/
    /// <summary>
    /// Central syntax parser for exact and inexact sample-size text forms.
    /// </summary>
    /// <remarks>
    /// This helper owns denominator pattern recognition only. It does not decide
    /// whether evidence should become <c>ArmN</c>; that policy belongs to
    /// <see cref="ArmNResolver"/>.
    /// </remarks>
    /// <seealso cref="SampleSizeEvidence"/>
    /// <seealso cref="ArmNResolver"/>
    internal static class SampleSizeParser
    {
        #region Diagnostic Constants

        /**************************************************************/
        /// <summary>Diagnostic emitted when only a sample-size range is present.</summary>
        internal const string RangeOnlyDiagnostic = "AE_ARMN_REJECTED_RANGE_ONLY";

        /**************************************************************/
        /// <summary>Diagnostic emitted when multiple exact N candidates conflict.</summary>
        internal const string ConflictingNDiagnostic = AeArmNValidationFlags.RejectedConflictingN;

        #endregion Diagnostic Constants

        #region Compiled Patterns

        private const string FootnoteMarkerPattern = @"[*\u2020\u2021\u00A7\u00B6#]?";
        private const string UnitContextPattern = @"(?:\s+(?:patients?|subjects?|eyes?))?";
        private const string FormatHintPattern =
            @"(?:" +
              @"%|" +
              @"%\s+of\s+(?:patients?|subjects?|participants)|" +
              @"%\s+incidence|" +
              @"\(\s*%\s*\)|" +
              @"\(\s*%\s+(?:of\s+)?(?:patients?|subjects?|participants)\s*\)|" +
              @"\(\s*%\s+incidence\s*\)|" +
              @"(?:Number|No\.?|Count)\s*\(\s*%\s*\)|" +
              @"n|" +
              @"n\s*\(\s*%\s*\)|" +
              @"n\s*\(\s*EAIR\s*\)|" +
              @"\(\s*100(?:\.0)?\s*%\s*\)|" +
              @"\(\s*(?:Weeks?|Months?|Days?)\s+\d+\s*(?:-|to|\u2013|\u2014)\s*\d+\s*\)" +
            @")?";

        private static readonly Regex _armHeaderParenPattern = new(
            @"^(.+?)\s*[\(\[]\s*[Nn]\s*\*?\s*=\s*(\d[\d,]*)\s*" +
            FootnoteMarkerPattern + UnitContextPattern + @"\s*[\)\]]\s*(" + FormatHintPattern + @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _armHeaderNoParenPattern = new(
            @"^(.+?)\s+[Nn]\s*\*?\s*=\s*(\d[\d,]*)\s*" +
            FootnoteMarkerPattern + UnitContextPattern + @"\s*(" + FormatHintPattern + @")\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _standaloneSampleSizePattern = new(
            @"^\(?\s*[Nn]\s*\*?\s*=\s*(\d[\d,]*)\s*" +
            FootnoteMarkerPattern + UnitContextPattern + @"\s*\)?\s*" +
            FormatHintPattern + @"\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _standaloneBracketSampleSizePattern = new(
            @"^\[\s*[Nn]\s*\*?\s*=\s*(\d[\d,]*)\s*" +
            FootnoteMarkerPattern + UnitContextPattern + @"\s*\]\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _nRowDenominatorCellPattern = new(
            @"^\s*(\d[\d,]*)\s*(?:\(\s*(?:%|[Nn])\s*\))?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _fractionDenominatorPattern = new(
            @"^\s*(\d[\d,]*)\s*/\s*(\d[\d,]*)(?:\s*\([^)]*\))?\s*$",
            RegexOptions.Compiled);

        private static readonly Regex _inlineBracketSampleSizePattern = new(
            @"[\(\[]\s*[Nn]\s*\*?\s*=\s*(\d[\d,]*)\s*" +
            FootnoteMarkerPattern + UnitContextPattern + @"\s*[\)\]]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _bareTrailingSampleSizePattern = new(
            @"\s+[Nn]\s*\*?\s*=\s*(\d[\d,]*)\s*" +
            FootnoteMarkerPattern + UnitContextPattern + @"\s*" + FormatHintPattern + @"\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex _rangeOnlySampleSizePattern = new(
            @"^\s*\d[\d,]*(?:\.\d+)?\s*(?:-|to|\u2013|\u2014)\s*\d[\d,]*(?:\.\d+)?\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Compiled Patterns

        /**************************************************************/
        /// <summary>
        /// Parses a treatment-arm header that embeds an exact sample size.
        /// </summary>
        /// <param name="text">Candidate header text.</param>
        /// <param name="evidence">Structured sample-size evidence.</param>
        /// <returns>True when the header contains an exact sample-size annotation.</returns>
        /// <seealso cref="TryParseHeaderTierSampleSize"/>
        internal static bool TryParseArmHeaderSampleSize(string? text, out SampleSizeEvidence evidence)
        {
            #region implementation

            return TryParseArmHeaderSampleSize(text, out evidence, out _);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a treatment-arm header and returns any trailing format hint separately.
        /// </summary>
        /// <param name="text">Candidate header text.</param>
        /// <param name="evidence">Structured sample-size evidence.</param>
        /// <param name="formatHint">Display hint after the sample-size annotation.</param>
        /// <returns>True when the header contains an exact sample-size annotation.</returns>
        /// <seealso cref="SampleSizeEvidence.FormatHint"/>
        internal static bool TryParseArmHeaderSampleSize(
            string? text,
            out SampleSizeEvidence evidence,
            out string? formatHint)
        {
            #region implementation

            return tryParseArmScopedSampleSize(
                text,
                SampleSizeSourceKind.ArmHeader,
                out evidence,
                out formatHint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a standalone cell such as <c>n=102</c> or <c>(N=442)</c>.
        /// </summary>
        /// <param name="text">Candidate sample-size cell text.</param>
        /// <param name="evidence">Structured sample-size evidence.</param>
        /// <returns>True when the cell is exact standalone sample-size evidence.</returns>
        /// <seealso cref="SampleSizeSourceKind.BodyMetadataRow"/>
        internal static bool TryParseStandaloneSampleSizeCell(string? text, out SampleSizeEvidence evidence)
        {
            #region implementation

            evidence = null!;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            var matchText = normalizeCommaAdjacentWhitespace(trimmed);
            var match = _standaloneSampleSizePattern.Match(matchText);
            if (!match.Success)
                match = _standaloneBracketSampleSizePattern.Match(matchText);

            if (!match.Success || !tryParsePositiveInt(match.Groups[1].Value, out var value))
                return false;

            evidence = SampleSizeEvidence.Exact(
                value,
                SampleSizeSourceKind.BodyMetadataRow,
                trimmed,
                cleanedText: null);
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses denominator cells that appear only inside an explicit N-row context.
        /// </summary>
        /// <remarks>
        /// This accepts plain integer cells such as <c>425</c> and format-token cells
        /// such as <c>425 (%)</c>. It deliberately rejects event-like cells such as
        /// <c>20 (5%)</c>; callers must only use this from a row-level denominator
        /// detector.
        /// </remarks>
        /// <param name="text">Candidate denominator cell text.</param>
        /// <param name="evidence">Structured sample-size evidence.</param>
        /// <returns>True when the cell is exact N-row denominator evidence.</returns>
        /// <seealso cref="SampleSizeSourceKind.BodyMetadataRow"/>
        internal static bool TryParseNRowDenominatorCell(string? text, out SampleSizeEvidence evidence)
        {
            #region implementation

            evidence = null!;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            var matchText = normalizeCommaAdjacentWhitespace(trimmed);
            var match = _nRowDenominatorCellPattern.Match(matchText);
            if (!match.Success || !tryParsePositiveInt(match.Groups[1].Value, out var value))
                return false;

            evidence = SampleSizeEvidence.Exact(
                value,
                SampleSizeSourceKind.BodyMetadataRow,
                trimmed,
                cleanedText: null);
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses header-tier text that scopes a sample size to a column or arm.
        /// </summary>
        /// <param name="text">Candidate header-tier text.</param>
        /// <param name="evidence">Structured sample-size evidence.</param>
        /// <returns>True when the header tier contains exact sample-size evidence.</returns>
        /// <seealso cref="SampleSizeParser"/>
        internal static bool TryParseHeaderTierSampleSize(string? text, out SampleSizeEvidence evidence)
        {
            #region implementation

            return tryParseArmScopedSampleSize(
                       text,
                       SampleSizeSourceKind.HeaderTier,
                       out evidence,
                       out _) ||
                   tryParseStandaloneWithSourceKind(
                       text,
                       SampleSizeSourceKind.HeaderTier,
                       out evidence);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses an explicit count-over-denominator value and returns the denominator.
        /// </summary>
        /// <param name="text">Candidate fraction text.</param>
        /// <param name="evidence">Structured denominator evidence.</param>
        /// <returns>True when the value carries an exact fraction denominator.</returns>
        /// <seealso cref="SampleSizeSourceKind.FractionDenominator"/>
        internal static bool TryParseFractionDenominator(string? text, out SampleSizeEvidence evidence)
        {
            #region implementation

            evidence = null!;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            var match = _fractionDenominatorPattern.Match(trimmed);
            if (!match.Success || !tryParsePositiveInt(match.Groups[2].Value, out var denominator))
                return false;

            evidence = SampleSizeEvidence.Exact(
                denominator,
                SampleSizeSourceKind.FractionDenominator,
                trimmed);
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses an inline trailing sample-size suffix and returns cleaned text.
        /// </summary>
        /// <param name="text">Candidate text with a trailing N annotation.</param>
        /// <param name="evidence">Structured sample-size evidence.</param>
        /// <returns>True when a trailing exact N annotation is found.</returns>
        /// <seealso cref="TryStripInlineSampleSize"/>
        internal static bool TryParseInlineSampleSizeSuffix(string? text, out SampleSizeEvidence evidence)
        {
            #region implementation

            evidence = null!;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            Match match;
            if (_inlineBracketSampleSizePattern.IsMatch(trimmed))
            {
                var matches = _inlineBracketSampleSizePattern.Matches(trimmed);
                match = matches[matches.Count - 1];
                if (match.Index + match.Length != trimmed.Length)
                    return false;
            }
            else
            {
                match = _bareTrailingSampleSizePattern.Match(trimmed);
                if (!match.Success)
                    return false;
            }

            if (!tryParsePositiveInt(match.Groups[1].Value, out var value))
                return false;

            var cleaned = normalizeCleanedText(trimmed.Remove(match.Index, match.Length));
            evidence = SampleSizeEvidence.Exact(
                value,
                SampleSizeSourceKind.InlineValueSuffix,
                trimmed,
                cleanedText: cleaned);
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Strips standalone, bracketed, or trailing inline N annotations from text.
        /// </summary>
        /// <param name="text">Candidate text.</param>
        /// <param name="evidence">Structured evidence with cleaned text.</param>
        /// <returns>True when an exact inline sample-size annotation is found.</returns>
        /// <seealso cref="TryParseInlineSampleSizeSuffix"/>
        internal static bool TryStripInlineSampleSize(string? text, out SampleSizeEvidence evidence)
        {
            #region implementation

            evidence = null!;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            if (TryParseStandaloneSampleSizeCell(trimmed, out var standalone))
            {
                evidence = standalone with
                {
                    SourceKind = SampleSizeSourceKind.InlineValueSuffix,
                    CleanedText = null
                };
                return true;
            }

            var inlineMatch = _inlineBracketSampleSizePattern.Match(trimmed);
            if (inlineMatch.Success && tryParsePositiveInt(inlineMatch.Groups[1].Value, out var inlineN))
            {
                var stripped = _inlineBracketSampleSizePattern.Replace(trimmed, " ");
                evidence = SampleSizeEvidence.Exact(
                    inlineN,
                    SampleSizeSourceKind.InlineValueSuffix,
                    trimmed,
                    cleanedText: normalizeCleanedText(stripped));
                return true;
            }

            var bareMatch = _bareTrailingSampleSizePattern.Match(trimmed);
            if (!bareMatch.Success || !tryParsePositiveInt(bareMatch.Groups[1].Value, out var bareN))
                return false;

            var bareStripped = _bareTrailingSampleSizePattern.Replace(trimmed, string.Empty);
            evidence = SampleSizeEvidence.Exact(
                bareN,
                SampleSizeSourceKind.InlineValueSuffix,
                trimmed,
                cleanedText: normalizeCleanedText(bareStripped),
                diagnosticCode: "BARE_TRAILING_N");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recognizes range-only sample-size text without creating exact evidence.
        /// </summary>
        /// <param name="text">Candidate range text.</param>
        /// <param name="evidence">Inexact range evidence.</param>
        /// <returns>True when the text is a denominator-like range.</returns>
        /// <seealso cref="RangeOnlyDiagnostic"/>
        internal static bool TryParseRangeOnlySampleSize(string? text, out SampleSizeEvidence evidence)
        {
            #region implementation

            evidence = null!;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            if (!_rangeOnlySampleSizePattern.IsMatch(trimmed))
                return false;

            evidence = SampleSizeEvidence.Rejected(
                SampleSizeSourceKind.RangeOnly,
                trimmed,
                RangeOnlyDiagnostic,
                "Range-only sample-size evidence is not exact enough for ArmN.");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Infers a sample size only when one sibling N exactly explains a count-percent pair.
        /// </summary>
        /// <param name="count">Observed event count.</param>
        /// <param name="percent">Reported percentage.</param>
        /// <param name="siblingCandidateNs">Candidate N values from sibling arms.</param>
        /// <param name="evidence">Inferred evidence or rejected conflict evidence.</param>
        /// <returns>True when inference produces exact or rejected audit evidence.</returns>
        /// <seealso cref="SampleSizeSourceKind.CountPercentInference"/>
        internal static bool TryInferCountPercentSampleSize(
            int count,
            decimal percent,
            IEnumerable<int> siblingCandidateNs,
            out SampleSizeEvidence evidence)
        {
            #region implementation

            evidence = null!;
            if (count <= 0 || percent <= 0m)
                return false;

            var matches = siblingCandidateNs
                .Where(n => n > 0)
                .Distinct()
                .Where(n =>
                {
                    var derived = Math.Round((decimal)count / n * 100m, 1);
                    return Math.Abs(derived - percent) <= 1.5m;
                })
                .ToList();

            if (matches.Count == 0)
                return false;

            if (matches.Count > 1)
            {
                evidence = SampleSizeEvidence.Rejected(
                    SampleSizeSourceKind.CountPercentInference,
                    $"{count} ({percent.ToString(CultureInfo.InvariantCulture)}%)",
                    ConflictingNDiagnostic,
                    "More than one sibling sample size can explain the count-percent pair.");
                return true;
            }

            evidence = SampleSizeEvidence.Exact(
                matches[0],
                SampleSizeSourceKind.CountPercentInference,
                $"{count} ({percent.ToString(CultureInfo.InvariantCulture)}%)");
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Infers a column-level sample size from repeated count-percent rows.
        /// </summary>
        /// <remarks>
        /// The inference is intentionally strict: at least three non-zero rows must
        /// round to the same unique denominator using the supplied one-decimal
        /// SPL rounding mode. Ambiguous denominator candidates return rejected
        /// evidence instead of an exact value.
        /// </remarks>
        /// <param name="columnObservations">Count and percent pairs from one arm column.</param>
        /// <param name="evidence">Inferred evidence or rejected conflict evidence.</param>
        /// <returns>True when inference produced exact or rejected audit evidence.</returns>
        /// <seealso cref="TryInferCountPercentSampleSize"/>
        internal static bool TryInferColumnConsensusSampleSize(
            IReadOnlyList<(int count, decimal percent)> columnObservations,
            out SampleSizeEvidence evidence)
        {
            #region implementation

            evidence = null!;
            var usable = columnObservations
                .Where(o => o.count > 0 && o.percent > 0m)
                .ToList();

            if (usable.Count < 3)
                return false;

            var candidateSets = usable
                .Select(getRoundedDenominatorCandidates)
                .ToList();

            if (candidateSets.Any(c => c.Count == 0))
                return false;

            var survivors = candidateSets
                .Skip(1)
                .Aggregate(
                    new HashSet<int>(candidateSets[0]),
                    (acc, next) =>
                    {
                        acc.IntersectWith(next);
                        return acc;
                    })
                .OrderBy(n => n)
                .ToList();

            var rawText = string.Join("; ", usable.Select(o =>
                $"{o.count} ({o.percent.ToString(CultureInfo.InvariantCulture)}%)"));

            if (survivors.Count == 0)
                return false;

            if (survivors.Count > 1)
            {
                evidence = SampleSizeEvidence.Rejected(
                    SampleSizeSourceKind.CountPercentInference,
                    rawText,
                    ConflictingNDiagnostic,
                    "More than one sample size can explain the column's count-percent observations.");
                return true;
            }

            evidence = SampleSizeEvidence.Exact(
                survivors[0],
                SampleSizeSourceKind.CountPercentInference,
                rawText);
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses arm-scoped N evidence from parenthesized or bare header forms.
        /// </summary>
        private static bool tryParseArmScopedSampleSize(
            string? text,
            SampleSizeSourceKind sourceKind,
            out SampleSizeEvidence evidence,
            out string? formatHint)
        {
            #region implementation

            evidence = null!;
            formatHint = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var trimmed = text.Trim();
            var matchText = normalizeCommaAdjacentWhitespace(trimmed);
            var match = _armHeaderParenPattern.Match(matchText);
            if (!match.Success)
                match = _armHeaderNoParenPattern.Match(matchText);

            if (!match.Success || !tryParsePositiveInt(match.Groups[2].Value, out var value))
                return false;

            var armText = normalizeCleanedText(match.Groups[1].Value);
            formatHint = normalizeCleanedText(match.Groups[3].Value);
            evidence = SampleSizeEvidence.Exact(
                value,
                sourceKind,
                trimmed,
                cleanedText: armText,
                armCandidate: armText,
                formatHint: formatHint);
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Collapses spaces adjacent to thousands separators before regex matching.
        /// </summary>
        private static string normalizeCommaAdjacentWhitespace(string text)
        {
            #region implementation

            return Regex.Replace(text, @"(?<=\d)\s*,\s*(?=\d)", ",");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets possible denominators that round one count to the reported percent.
        /// </summary>
        private static HashSet<int> getRoundedDenominatorCandidates((int count, decimal percent) observation)
        {
            #region implementation

            var candidates = new HashSet<int>();
            if (observation.count <= 0 || observation.percent <= 0m)
                return candidates;

            var lower = (int)Math.Floor(observation.count * 100m / (observation.percent + 0.05m));
            var upper = (int)Math.Ceiling(observation.count * 100m / Math.Max(0.0001m, observation.percent - 0.05m));
            lower = Math.Max(lower - 2, observation.count);
            upper = Math.Max(upper + 2, lower);

            for (var n = lower; n <= upper; n++)
            {
                var rounded = Math.Round((decimal)observation.count / n * 100m, 1, MidpointRounding.AwayFromZero);
                if (rounded == observation.percent)
                    candidates.Add(n);
            }

            return candidates;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses standalone evidence and rewrites its source kind for header callers.
        /// </summary>
        private static bool tryParseStandaloneWithSourceKind(
            string? text,
            SampleSizeSourceKind sourceKind,
            out SampleSizeEvidence evidence)
        {
            #region implementation

            if (!TryParseStandaloneSampleSizeCell(text, out evidence))
                return false;

            evidence = evidence with { SourceKind = sourceKind };
            return true;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parses a comma-formatted positive integer.
        /// </summary>
        private static bool tryParsePositiveInt(string raw, out int value)
        {
            #region implementation

            value = 0;
            var normalized = raw.Replace(",", string.Empty);
            return int.TryParse(
                       normalized,
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out value) &&
                   value > 0;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes cleaned text after a sample-size annotation has been removed.
        /// </summary>
        private static string? normalizeCleanedText(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return null;

            var collapsed = Regex.Replace(text.Trim(), @"\s{2,}", " ").Trim();
            collapsed = collapsed.Trim(' ', ',', ';', ':');
            return string.IsNullOrWhiteSpace(collapsed) ? null : collapsed;

            #endregion
        }
    }
}
