using System.Text.RegularExpressions;

namespace MedRecProImportClass.Service.TransformationServices.Dictionaries
{
    /**************************************************************/
    /// <summary>
    /// Single source of truth for PK unit recognition, used by
    /// <c>PkTableParser</c> to fill in <c>observation.Unit</c> when the column
    /// header did not carry a parenthesized unit. Complements the
    /// <c>ColumnStandardizationService</c> Phase-2d unit scrub by providing
    /// parser-time extraction from cell text, sub-header unit rows, and
    /// sibling-row majority vote (Wave 3 R10).
    /// </summary>
    /// <remarks>
    /// ## Responsibilities
    /// - <see cref="IsRecognized"/> — fast membership test combining the known-units
    ///   hash set, the variant normalization map, and the structural PK-unit regex.
    /// - <see cref="TryNormalize"/> — collapse variant spellings (e.g., <c>hr</c> →
    ///   <c>h</c>, <c>ug/mL</c> → <c>mcg/mL</c>, <c>mcg⋅hr/mL</c> → <c>mcg·h/mL</c>).
    /// - <see cref="TryExtractFromCellText"/> — find the first unit token following a
    ///   numeric literal in a PK value cell (e.g., <c>"13.8 hr (6.4)"</c> → <c>h</c>,
    ///   <c>"391 ng/mL at 3.2 hr"</c> → <c>ng/mL</c>). Longest-match alternation
    ///   with Unicode fold prevents <c>mg</c> from winning over <c>mg/kg/day</c>.
    /// - <see cref="TryExtractFromHeaderLikeText"/> — strip parentheses / whitespace
    ///   around a cell that is entirely a unit string (for sub-header unit rows like
    ///   <c>"(ng/mL)"</c>).
    ///
    /// ## Design
    /// Case-insensitive. Internally folds U+22C5 DOT OPERATOR to U+00B7 MIDDLE DOT via
    /// <see cref="PkParameterDictionary.NormalizeUnicode"/> so source cells containing
    /// <c>mcg⋅h/mL</c> normalize consistently with the canonical <c>mcg·h/mL</c>.
    ///
    /// ## Consistency with Phase-2d scrub
    /// This class is the single source of truth for <see cref="KnownUnits"/>,
    /// <see cref="NormalizationMap"/>, and <see cref="PkUnitStructurePattern"/>
    /// — shared by both <c>PkTableParser</c> (parser-time extraction) and
    /// <c>ColumnStandardizationService</c> (Phase-2d scrub). Phase-2d layers
    /// additional header-leak rejection and drug-name filtering on top; this
    /// dictionary stays concerned only with what is or isn't a unit.
    /// </remarks>
    /// <seealso cref="PkParameterDictionary"/>
    public static class UnitDictionary
    {
        #region Known Units

        /**************************************************************/
        /// <summary>
        /// Canonical PK unit tokens. Shared source of truth for
        /// <c>PkTableParser</c> (parser-time extraction) and
        /// <c>ColumnStandardizationService</c> (Phase-2d scrub). Exposed as
        /// <see cref="HashSet{T}"/> for O(1) membership probing via
        /// <c>UnitDictionary.KnownUnits.Contains(x)</c>.
        /// </summary>
        public static readonly HashSet<string> KnownUnits = new(StringComparer.OrdinalIgnoreCase)
        {
            "%", "%CV", "h", "hr", "min", "days", "weeks", "months", "years",
            "mg", "mcg", "\u03BCg", "g", "kg",
            "mcg/mL", "ng/mL", "pg/mL", "\u03BCg/mL", "mg/L", "ng/dL", "mg/dL",
            "mcg·h/mL", "ng·h/mL", "\u03BCg·h/mL", "pg·h/mL", "mg·h/L",
            "mL/min", "mL/min/kg", "L/h", "L/h/kg", "L/kg/h", "L/h/m", "mL/h/kg",
            "L", "mL", "L/kg",
            "mcg/kg/min", "mg/h", "IU/mL",
            "mg/kg", "mcg/kg", "mg/m²", "mg/kg/day",
            "ratio", "g/cm²", "beats/min", "mmHg", "mEq/L", "mOsm/kg",
            "percentage points", "subjects", "events", "patients",
            "ng/g", "mcg/g",
            "mg/day", "mg/d", "mcg/day",
            // R11 — micromolar concentration & AUC for high-potency / molarly-reported drugs
            "\u03BCM", "\u03BCM·h",
            // R11 — long-acting drug AUC (very long t½ products, e.g., depot formulations)
            "ng·day/mL"
        };

        #endregion Known Units

        #region Normalization Map

        /**************************************************************/
        /// <summary>
        /// Variant → canonical normalization map. Shared source of truth for
        /// <c>PkTableParser</c> and <c>ColumnStandardizationService</c>. Used by
        /// <see cref="TryNormalize"/> before the <see cref="KnownUnits"/> check
        /// so overlapping keys (e.g., <c>hr</c> present in both) always collapse
        /// to the preferred canonical form (<c>h</c>).
        /// </summary>
        public static readonly Dictionary<string, string> NormalizationMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["mcg h/mL"] = "mcg·h/mL",
            ["mcgh/mL"] = "mcg·h/mL",
            ["ng h/mL"] = "ng·h/mL",
            ["ngh/mL"] = "ng·h/mL",
            ["nghr/mL"] = "ng·h/mL",
            ["ug/mL"] = "mcg/mL",
            ["L/kghr"] = "L/kg/h",
            ["hrs"] = "h",
            ["hr"] = "h",
            ["pp"] = "percentage points",
            ["percent"] = "%",
            ["pct"] = "%",
            ["pg·hr/mL"] = "pg·h/mL",
            ["mcg·hr/mL"] = "mcg·h/mL",
            ["ng·hr/mL"] = "ng·h/mL",
            ["ug·hr/mL"] = "mcg·h/mL",
            // U+22C5 DOT OPERATOR variants — folded to U+00B7 MIDDLE DOT via NormalizeUnicode
            // (Note: post-fold these match the entries above. Listed verbatim only for any
            // pre-NormalizeUnicode caller — current callers all run NormalizeUnicode first.)

            // R11 — Time-word variants. Case-insensitive HashSet handles "Hours"/"HOURS".
            ["hours"] = "h",
            ["hour"] = "h",
            ["day"] = "days",
            ["H"] = "h",

            // R11 — Long-form spelled-out concentrations
            ["nanogram per mL"] = "ng/mL",
            ["nanogram/mL"] = "ng/mL",

            // R11 — L/h family (post-NormalizeUnicode keys: ⋅•∙× all → ·)
            ["L/hr"] = "L/h",
            ["L/hour"] = "L/h",
            ["L/kg/hr"] = "L/kg/h",
            ["L/kg·hr"] = "L/kg/h",  // post-bullet-fold of "L/kg•hr"
            ["L/kg·h"] = "L/kg/h",
            ["L/hr/m"] = "L/h/m",    // hr→h, otherwise verbatim per user decision

            // R11 — Asterisk and period variants of AUC. Period (`.`) is enumerated
            // because we cannot fold `.` globally (would break decimal numbers in cell
            // text). Asterisk (`*`) for the same reason — also appears in footnotes.
            ["mcg*h/mL"] = "mcg·h/mL",
            ["mcg*hr/mL"] = "mcg·h/mL",
            ["mcg.h/mL"] = "mcg·h/mL",
            ["mcg.hr/mL"] = "mcg·h/mL",
            ["ng*h/mL"] = "ng·h/mL",
            ["ng*hr/mL"] = "ng·h/mL",
            ["ng.h/mL"] = "ng·h/mL",
            ["ng.hr/mL"] = "ng·h/mL",
            ["ngxhr/mL"] = "ng·h/mL",  // ASCII 'x' between ng and hr
            ["pg.h/mL"] = "pg·h/mL",
            ["pg.hr/mL"] = "pg·h/mL",
            ["pg*h/mL"] = "pg·h/mL",
            ["pg*hr/mL"] = "pg·h/mL",

            // R11 — mg·h/L family (new canonical) — post-NormalizeUnicode keys
            ["mg·hr/L"] = "mg·h/L",
            ["mg.h/L"] = "mg·h/L",
            ["mg.hr/L"] = "mg·h/L",
            ["mg*h/L"] = "mg·h/L",
            ["mg*hr/L"] = "mg·h/L",

            // R11 — Reversed-order AUC (rare, observed in corpus as "h·ng/mL")
            ["h·ng/mL"] = "ng·h/mL",
            ["h·mcg/mL"] = "mcg·h/mL",
            ["h·pg/mL"] = "pg·h/mL",

            // R11 — Micromolar (μM) AUC variants — post-NormalizeUnicode keys (μ = U+03BC)
            ["\u03BCM·hr"] = "\u03BCM·h",
            ["\u03BCM.hr"] = "\u03BCM·h",
            ["\u03BCM.h"] = "\u03BCM·h",
            ["\u03BCM*hr"] = "\u03BCM·h",
            ["\u03BCM·hr/L"] = "\u03BCM·h",
            ["\u03BCM.hr/L"] = "\u03BCM·h",

            // R11 — Greek-mu AUC variants. NormalizeUnicode folds U+00B5 → U+03BC,
            // so these keys must use U+03BC (Greek Mu).
            ["\u03BCg·hr/mL"] = "\u03BCg·h/mL",
            ["\u03BCg.h/mL"] = "\u03BCg·h/mL",
            ["\u03BCg.hr/mL"] = "\u03BCg·h/mL",
            ["\u03BCg*h/mL"] = "\u03BCg·h/mL",
            ["\u03BCg*hr/mL"] = "\u03BCg·h/mL",
            ["\u03BCghr/mL"] = "\u03BCg·h/mL",
            ["\u03BCg h/mL"] = "\u03BCg·h/mL",

            // R11 — CV percentage (CV% appears as a header but means "%CV")
            ["CV%"] = "%CV"
        };

        #endregion Normalization Map

        #region Structural Pattern

        /**************************************************************/
        /// <summary>
        /// Structural fallback regex matching PK unit tokens not enumerated above.
        /// Covers concentration (<c>mcg/mL</c>, <c>ng/mL</c>), AUC-style
        /// (<c>mcg·h/mL</c>, <c>ng·hr/mL</c>), and body-weight-normalized
        /// (<c>mg/kg</c>, <c>mg/m²</c>) shapes. Anchored — full-string match only.
        /// </summary>
        public static readonly Regex PkUnitStructurePattern = new(
            "^(?:(?:mc?g|ng|pg|\u03BCg|mg|IU)(?:\u00B7(?:h|hr))?/(?:mL|L|kg|m\u00B2))$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**************************************************************/
        /// <summary>
        /// R11 — Internal-whitespace stripper used by the whitespace-tolerant
        /// fallback in <see cref="TryNormalize"/> and <see cref="IsRecognized"/>
        /// to canonicalize PDF-extraction defects like <c>"mcg /mL"</c>,
        /// <c>"mcg . hr /mL"</c>, <c>"mL /min /kg"</c>, <c>"m c g/mL"</c>.
        /// </summary>
        private static readonly Regex _whitespaceStrip = new(@"\s+", RegexOptions.Compiled);

        #endregion Structural Pattern

        #region Inline Extraction Pattern

        /**************************************************************/
        /// <summary>
        /// Longest-first alternation used by <see cref="TryExtractFromCellText"/>
        /// to locate a unit token immediately following a numeric literal inside a
        /// cell. Order matters — <c>mcg·h/mL</c> must appear before <c>mcg</c> so
        /// the longer match wins. Built once at static construction time from
        /// <see cref="KnownUnits"/> (compound forms) plus the canonical short forms
        /// (<c>h</c>, <c>min</c>, etc.).
        /// </summary>
        /// <remarks>
        /// Bare single-letter units (<c>h</c>, <c>L</c>, <c>g</c>) are intentionally
        /// omitted from the inline scan — too easy to false-match against drug
        /// names or numeric tokens. <c>hr</c> is included and normalized to
        /// <c>h</c>, covering the overwhelmingly common observed form.
        /// </remarks>
        private static readonly Regex _inlineUnitPattern = buildInlineUnitPattern();

        /// <summary>
        /// Builds the inline-unit regex by ordering candidates longest-first so the
        /// regex engine prefers <c>mcg·h/mL</c> over <c>mcg</c> when both match at
        /// the same position.
        /// </summary>
        private static Regex buildInlineUnitPattern()
        {
            #region implementation

            // Units safe to detect inline: length ≥ 2 characters AND either composite
            // (contains "/" or "·") OR a multi-char PK-specific time token. Long-form
            // date words (days, weeks, months, years) are deliberately EXCLUDED — they
            // appear in narrative cells ("6 years to 18 years") and would produce
            // false-positive unit assignments on non-PK content.
            var inlineCandidates = new List<string>
            {
                // Composite concentration units (longest first). Mu entries use
                // U+03BC (GREEK MU) to match post-NormalizeUnicode input.
                "mcg·h/mL", "ng·h/mL", "pg·h/mL", "\u03BCg·h/mL",
                "mcg·hr/mL", "ng·hr/mL", "pg·hr/mL", "\u03BCg·hr/mL",
                "mL/min/kg", "mg/kg/day", "mcg/kg/min",
                "mL/h/kg", "L/h/kg", "L/kg/h",
                "mcg/mL", "ng/mL", "pg/mL", "\u03BCg/mL",
                "mg/dL", "ng/dL", "mg/L", "mg·h/L",
                "mg/kg", "mcg/kg",
                "mg/day", "mcg/day", "mg/m²",
                "mL/min", "L/kg", "L/h", "mg/h", "IU/mL",
                "mg/d",
                "ng/g", "mcg/g",
                "ng·day/mL",
                // Time tokens — PK-specific only (hours / minutes). Excludes
                // days/weeks/months/years to avoid narrative age-range matches.
                "hrs", "hr", "min",
                // Pressure / physiology
                "beats/min", "mmHg", "mEq/L", "mOsm/kg",
                // Percentage spellings
                "%CV", "%",
                "percentage points",
                // Area (bare)
                "g/cm²"
            };

            // Order by descending length — critical for longest-first matching.
            var ordered = inlineCandidates
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(u => u.Length)
                .ThenBy(u => u, StringComparer.Ordinal)
                .ToList();

            // Build alternation with escaped literals. Prefix with "(?<=\d[\d\.,]*\s*)"
            // to require a numeric literal before the unit token. Suffix "(?!\w)" is a
            // negative word-boundary that allows non-word chars ( /, %, ·, ² ) to
            // follow the match but prevents partial-token matches like "hrs" in "hrsomething".
            var alternation = string.Join("|", ordered.Select(Regex.Escape));
            var pattern = @"(?<=\d[\d\.,]*\s{0,3})(" + alternation + @")(?!\w)";

            return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

            #endregion
        }

        #endregion Inline Extraction Pattern

        #region Public Helpers

        /**************************************************************/
        /// <summary>
        /// True when <paramref name="candidate"/> is a recognized PK unit — either
        /// an exact match in <see cref="KnownUnits"/>, a registered variant in
        /// <see cref="NormalizationMap"/>, or a structural match via
        /// <see cref="PkUnitStructurePattern"/>. Applies Unicode fold first.
        /// </summary>
        /// <param name="candidate">Candidate unit string (trimmed by caller).</param>
        /// <returns>True if recognized.</returns>
        /// <example>
        /// <code>
        /// IsRecognized("ng/mL")       → true
        /// IsRecognized("mcg⋅hr/mL")   → true (variant → canonical)
        /// IsRecognized("hr")          → true
        /// IsRecognized("Regimen")     → false
        /// </code>
        /// </example>
        public static bool IsRecognized(string? candidate)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            var folded = PkParameterDictionary.NormalizeUnicode(candidate).Trim();

            if (KnownUnits.Contains(folded))
                return true;

            if (NormalizationMap.ContainsKey(folded))
                return true;

            if (PkUnitStructurePattern.IsMatch(folded))
                return true;

            // R11 — Whitespace-tolerant fallback. Real corpus has spacing defects
            // from PDF extraction: "mcg /mL", "mcg ·h/mL", "mcg . hr /mL",
            // "mL /min /kg", "m c g/mL". Strip ALL internal whitespace and retry.
            // Skipped for entries that legitimately contain spaces (`percentage
            // points`, `mean ± SD` if added) — those are caught by the spaced-form
            // checks above. Whitespace-stripped form is tried last so we never
            // collapse a legitimate spaced canonical to an unrelated concatenation.
            var compact = _whitespaceStrip.Replace(folded, "");
            if (compact.Length > 0 && compact.Length != folded.Length)
            {
                if (KnownUnits.Contains(compact))
                    return true;
                if (NormalizationMap.ContainsKey(compact))
                    return true;
                if (PkUnitStructurePattern.IsMatch(compact))
                    return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns the canonical form of <paramref name="candidate"/> — an exact
        /// <see cref="KnownUnits"/> entry when one exists, else the
        /// <see cref="NormalizationMap"/> target, else the folded structural match,
        /// else null when the candidate is not recognized.
        /// </summary>
        /// <param name="candidate">Candidate unit string.</param>
        /// <returns>Canonical unit, or null if unrecognized.</returns>
        public static string? TryNormalize(string? candidate)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(candidate))
                return null;

            var folded = PkParameterDictionary.NormalizeUnicode(candidate).Trim();

            // Check NormalizationMap FIRST. A handful of legacy known-units
            // entries (e.g., "hr") overlap with normalization-map keys that
            // redirect to a preferred canonical form (e.g., "hr" → "h"). Map
            // precedence ensures callers always receive the canonical short form,
            // matching the observed canonical shape in the post-Iter7 corpus.
            if (NormalizationMap.TryGetValue(folded, out var mapped))
                return mapped;

            // Exact hit in known units — return the HashSet's canonical entry
            if (KnownUnits.TryGetValue(folded, out var canonical))
                return canonical;

            if (PkUnitStructurePattern.IsMatch(folded))
                return folded;

            // R11 — Whitespace-tolerant fallback. See IsRecognized for full rationale.
            // Tried last so legitimate spaced canonicals ("percentage points") win on
            // their original-spacing form before the compact form is considered.
            var compact = _whitespaceStrip.Replace(folded, "");
            if (compact.Length > 0 && compact.Length != folded.Length)
            {
                if (NormalizationMap.TryGetValue(compact, out var compactMapped))
                    return compactMapped;
                if (KnownUnits.TryGetValue(compact, out var compactCanonical))
                    return compactCanonical;
                if (PkUnitStructurePattern.IsMatch(compact))
                    return compact;
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Scans <paramref name="cellText"/> for a unit token immediately following
        /// a numeric literal. Returns the canonical unit on the first match, or
        /// null when no inline unit is present. Longest-match alternation ensures
        /// <c>mcg·h/mL</c> wins over <c>mcg</c>.
        /// </summary>
        /// <param name="cellText">Raw (or cleaned) cell text from a PK value cell.</param>
        /// <returns>Canonical unit, or null.</returns>
        /// <example>
        /// <code>
        /// TryExtractFromCellText("13.8 hr (6.4) (terminal)")  → "h"
        /// TryExtractFromCellText("391 ng/mL at 3.2 hr")       → "ng/mL"  (first match wins)
        /// TryExtractFromCellText("5.5 mcg/mL")                → "mcg/mL"
        /// TryExtractFromCellText("1800 ng·h/mL")              → "ng·h/mL"
        /// TryExtractFromCellText("93.6±14.2")                 → null (no unit)
        /// TryExtractFromCellText("N = 101")                   → null (no post-digit unit)
        /// </code>
        /// </example>
        public static string? TryExtractFromCellText(string? cellText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(cellText))
                return null;

            var folded = PkParameterDictionary.NormalizeUnicode(cellText);
            var match = _inlineUnitPattern.Match(folded);
            if (!match.Success)
                return null;

            var raw = match.Groups[1].Value;
            return TryNormalize(raw) ?? raw;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recognizes an entire cell as a unit string — strips surrounding
        /// parentheses and whitespace, then checks <see cref="IsRecognized"/>.
        /// Intended for sub-header unit rows where a column's cell contains only
        /// the unit (e.g., <c>"(ng/mL)"</c>, <c>"mcg·h/mL"</c>).
        /// </summary>
        /// <param name="cellText">Cell text from a candidate sub-header row.</param>
        /// <returns>Canonical unit, or null when the cell is not a pure unit.</returns>
        /// <example>
        /// <code>
        /// TryExtractFromHeaderLikeText("(ng/mL)")  → "ng/mL"
        /// TryExtractFromHeaderLikeText("mcg·h/mL") → "mcg·h/mL"
        /// TryExtractFromHeaderLikeText("Cmax")     → null
        /// TryExtractFromHeaderLikeText("13.9 ± 2.9") → null
        /// </code>
        /// </example>
        public static string? TryExtractFromHeaderLikeText(string? cellText)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(cellText))
                return null;

            var trimmed = cellText.Trim();

            // Strip a single wrapping parentheses pair if present
            if (trimmed.Length >= 2 && trimmed[0] == '(' && trimmed[trimmed.Length - 1] == ')')
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            }

            if (string.IsNullOrWhiteSpace(trimmed))
                return null;

            return TryNormalize(trimmed);

            #endregion
        }

        #endregion Public Helpers
    }
}
