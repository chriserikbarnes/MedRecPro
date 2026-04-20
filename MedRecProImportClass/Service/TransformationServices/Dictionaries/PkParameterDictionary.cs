using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MedRecProImportClass.Service.TransformationServices.Dictionaries
{
    /**************************************************************/
    /// <summary>
    /// Single source of truth for canonical pharmacokinetic parameter names, their
    /// aliases (including long-form English variants such as "Maximum Plasma
    /// Concentrations" for Cmax), and helpers that normalize Unicode variants found
    /// in SPL source tables.
    /// </summary>
    /// <remarks>
    /// ## Responsibilities
    /// - Provide <see cref="IsPkParameter"/> — a fast hit check used by the table
    ///   router to confirm PK categorization from table content and by
    ///   <c>PkTableParser.detectTransposedPkLayout</c> to identify PK metric rows.
    /// - Provide <see cref="TryCanonicalize"/> — collapse variant spellings to a
    ///   canonical ParameterName (e.g., "Elimination Half-life" → "t½",
    ///   "AUC0-∞(mcg⋅hr/mL)" → "AUC0-inf").
    /// - Provide <see cref="StartsWithPk"/> — anchored match used by
    ///   <c>ColumnStandardizationService</c> DoseRegimen triage to detect PK
    ///   sub-parameter names that leaked into the dose column.
    /// - Provide <see cref="NormalizeUnicode"/> — fold U+22C5 (DOT OPERATOR) to
    ///   U+00B7 (MIDDLE DOT) so the existing unit normalization map matches
    ///   strings like "mcg⋅hr/mL" that appear in real SPL tables.
    ///
    /// ## Design
    /// Canonical entries are declared inline with an alias list. At static
    /// construction time a case-insensitive alias → canonical dictionary is built
    /// for O(1) lookups. Each entry also compiles a prefix regex used by
    /// <see cref="StartsWithPk"/>.
    ///
    /// ## PD Markers Excluded
    /// Platelet and other pharmacodynamic markers (IPA, VASP-PRI) are intentionally
    /// absent from this dictionary — they belong in <see cref="PdMarkerDictionary"/>.
    /// </remarks>
    /// <seealso cref="PdMarkerDictionary"/>
    public static class PkParameterDictionary
    {
        #region Nested Types

        /**************************************************************/
        /// <summary>
        /// One canonical PK parameter plus the alias strings that should collapse to it.
        /// </summary>
        /// <param name="Canonical">The canonical ParameterName value written to observations.</param>
        /// <param name="Aliases">Variant spellings, including long-form English phrases and
        /// Unicode-laden forms. Matching is case-insensitive and ignores trailing parenthesized
        /// unit suffixes.</param>
        /// <param name="PrefixPattern">Anchored regex used by <see cref="StartsWithPk"/>.</param>
        public sealed record PkEntry(
            string Canonical,
            IReadOnlyList<string> Aliases,
            Regex PrefixPattern);

        #endregion Nested Types

        #region Unicode Normalization

        // U+22C5 DOT OPERATOR — appears in SPL tables as "mcg⋅hr/mL"
        private const char DOT_OPERATOR = '\u22C5';

        // U+00B7 MIDDLE DOT — canonical form matching _unitNormalizationMap
        private const char MIDDLE_DOT = '\u00B7';

        // U+221E INFINITY SIGN
        private const char INFINITY_SIGN = '\u221E';

        // Collapse sequences of whitespace to a single space
        private static readonly Regex _whitespaceCollapse = new(@"\s+", RegexOptions.Compiled);

        // Trailing parenthesized unit expression such as "(mcg/mL)", "(hr)", "(%)", "(mL/min/kg)"
        // Conservative: only strips a single trailing "(...)" group with no nested parens
        private static readonly Regex _trailingParenStrip = new(
            @"\s*\([^()]{1,40}\)\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Folds Unicode variants so downstream lookups see a canonical codepoint form.
        /// Currently maps U+22C5 (DOT OPERATOR, `⋅`) onto U+00B7 (MIDDLE DOT, `·`) and
        /// runs an NFKC normalization pass over the result.
        /// </summary>
        /// <param name="input">Raw text from the source table.</param>
        /// <returns>Canonicalized string, or the empty string when <paramref name="input"/> is null.</returns>
        /// <remarks>
        /// Real SPL tables have been observed to use `⋅` in unit expressions like
        /// `mcg⋅hr/mL`. The existing normalization map keys use `·`, so without this
        /// fold the normalization step silently fails and units remain embedded.
        /// </remarks>
        public static string NormalizeUnicode(string? input)
        {
            #region implementation

            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Fast path: replace known codepoint variants first
            var folded = input.Replace(DOT_OPERATOR, MIDDLE_DOT);

            // NFKC also collapses things like fullwidth digits; safe for unit text
            return folded.Normalize(NormalizationForm.FormKC);

            #endregion
        }

        #endregion Unicode Normalization

        #region Canonical Entries

        /**************************************************************/
        /// <summary>
        /// Canonical PK parameters and their alias lists. Order is not significant
        /// because the alias-to-canonical lookup is built exhaustively at static
        /// construction time; entries appear grouped by family for readability.
        /// </summary>
        public static IReadOnlyList<PkEntry> Entries { get; } = buildEntries();

        private static IReadOnlyList<PkEntry> buildEntries()
        {
            #region implementation

            // Local helper builds an anchored regex from a list of alternation
            // expressions. Each `alt` is ALREADY a regex fragment (so it can use
            // character classes and quantifiers) — the helper simply joins them
            // with `|` and anchors at the start with a trailing word boundary.
            static Regex prefix(params string[] alts)
            {
                var joined = string.Join("|", alts);
                return new Regex(@"^\s*(?:" + joined + @")\b",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }

            return new List<PkEntry>
            {
                // -- Concentration metrics --
                new("Cmax",
                    new[] { "Cmax", "C max", "Cmax,ss", "Cmax ss",
                            "Maximum Plasma Concentration", "Maximum Plasma Concentrations",
                            "Maximum Concentration", "Peak Plasma Concentration",
                            "Peak Concentration" },
                    prefix("Cmax", "C\\s*max", "Maximum\\s+Plasma\\s+Concentrations?",
                           "Peak\\s+Plasma\\s+Concentration", "Maximum\\s+Concentration")),

                new("Cmin",
                    new[] { "Cmin", "C min", "Cmin,ss",
                            "Minimum Plasma Concentration", "Minimum Concentration" },
                    prefix("Cmin", "C\\s*min", "Minimum\\s+Plasma\\s+Concentration",
                           "Minimum\\s+Concentration")),

                new("Ctrough",
                    new[] { "Ctrough", "Cthrough", "C trough",
                            "Trough Concentration", "Trough Plasma Concentration" },
                    prefix("Ctrough", "Cthrough", "C\\s*trough", "Trough\\s+(?:Plasma\\s+)?Concentration")),

                new("Cavg",
                    new[] { "Cavg", "C avg", "Css,avg", "Css avg",
                            "Average Concentration", "Average Plasma Concentration" },
                    prefix("Cavg", "C\\s*avg", "Css[,\\s]*avg", "Average\\s+(?:Plasma\\s+)?Concentration")),

                new("Css",
                    new[] { "Css", "C ss", "Steady State Concentration",
                            "Steady-State Concentration" },
                    prefix("Css", "C\\s*ss", "Steady[\\s-]State\\s+Concentration")),

                // -- Time metrics --
                new("Tmax",
                    new[] { "Tmax", "T max", "Tmax,ss",
                            "Time to Maximum Concentration", "Time to Peak",
                            "Time to Peak Concentration" },
                    prefix("Tmax", "T\\s*max", "Time\\s+to\\s+Maximum\\s+Concentration",
                           "Time\\s+to\\s+Peak")),

                new("t½",
                    new[] { "t½", "t1/2", "T 1/2", "T1/2", "Half-life", "Half Life",
                            "Half-Life", "Elimination Half-life", "Elimination Half Life",
                            "Elimination Half-Life", "Terminal Half-life",
                            "Terminal Half Life", "Plasma Half-life", "Apparent Half-life" },
                    prefix("t½", "t\\s*1/2", "T\\s*1/2",
                           "(?:Elimination|Terminal|Plasma|Apparent)\\s+Half[\\s-]?Life",
                           "Half[\\s-]?Life")),

                new("MRT",
                    new[] { "MRT", "Mean Residence Time" },
                    prefix("MRT", "Mean\\s+Residence\\s+Time")),

                new("MAT",
                    new[] { "MAT", "Mean Absorption Time" },
                    prefix("MAT", "Mean\\s+Absorption\\s+Time")),

                // -- AUC family (more specific variants appear with their own canonical forms) --
                new("AUC0-inf",
                    new[] { "AUC0-inf", "AUC(0-inf)", "AUC0-infinity", "AUC0-∞",
                            "AUC(0-∞)", "AUC∞", "AUCinf", "AUC 0-inf",
                            "AUC 0-∞", "AUC 0-infinity", "AUC(0,inf)" },
                    prefix("AUC\\s*[\\(]?\\s*0\\s*[\\-–,]\\s*(?:inf(?:inity)?|∞)",
                           "AUC\\s*∞", "AUCinf")),

                new("AUC0-24",
                    new[] { "AUC0-24", "AUC0-24h", "AUC(0-24)", "AUC(0-24h)",
                            "AUC 0-24", "AUC 0-24 hr", "AUC 0-24h", "AUC24" },
                    prefix("AUC\\s*[\\(]?\\s*0\\s*[\\-–,]\\s*24\\s*h?", "AUC24")),

                new("AUC0-12",
                    new[] { "AUC0-12", "AUC0-12h", "AUC(0-12)", "AUC 0-12", "AUC12" },
                    prefix("AUC\\s*[\\(]?\\s*0\\s*[\\-–,]\\s*12\\s*h?", "AUC12")),

                new("AUC0-t",
                    new[] { "AUC0-t", "AUC(0-t)", "AUC 0-t" },
                    prefix("AUC\\s*[\\(]?\\s*0\\s*[\\-–,]\\s*t(?![\\w])")),

                new("AUClast",
                    new[] { "AUClast", "AUC last", "AUC_last",
                            "AUC to last measurable", "AUC to last measurable concentration" },
                    prefix("AUClast", "AUC[\\s_]last", "AUC\\s+to\\s+last\\s+measurable")),

                new("AUCtau",
                    new[] { "AUCtau", "AUC tau", "AUC(0-tau)", "AUC 0-tau",
                            "AUC over dosing interval" },
                    prefix("AUCtau", "AUC\\s*[\\(]?\\s*0\\s*[\\-–,]\\s*tau",
                           "AUC\\s+over\\s+dosing\\s+interval")),

                new("AUC",
                    // Generic AUC falls through when no 0-X qualifier is present
                    new[] { "AUC", "Area Under the Curve", "Area Under Curve",
                            "Area Under the Concentration-Time Curve" },
                    prefix("AUC(?![0-9\\-])", "Area\\s+Under\\s+(?:the\\s+)?(?:Concentration-Time\\s+)?Curve")),

                // -- Clearance --
                new("CL/F",
                    new[] { "CL/F", "Cl/F", "CLss/F", "Apparent Clearance",
                            "Apparent Oral Clearance", "Oral Clearance" },
                    prefix("CL(?:ss)?/F", "Cl/F", "Apparent\\s+(?:Oral\\s+)?Clearance",
                           "Oral\\s+Clearance")),

                new("CL",
                    new[] { "CL", "Cl", "CLss", "Clss", "Clearance",
                            "Plasma Clearance", "Total Clearance",
                            "Total Body Clearance", "Systemic Clearance" },
                    prefix("CL(?:ss)?(?!/)", "Cl(?!/)",
                           "(?:Plasma|Total|Total\\s+Body|Systemic)\\s+Clearance",
                           "Clearance")),

                new("CLr",
                    new[] { "CLr", "CL r", "Renal Clearance" },
                    prefix("CLr", "CL\\s*r", "Renal\\s+Clearance")),

                // -- Volume of distribution --
                new("Vd/F",
                    new[] { "Vd/F", "V/F", "Vz/F", "Vd,z/F",
                            "Apparent Volume of Distribution",
                            "Apparent Oral Volume of Distribution" },
                    prefix("Vd/F", "V/F", "Vz/F", "Vd,z/F",
                           "Apparent\\s+(?:Oral\\s+)?Volume\\s+of\\s+Distribution")),

                new("Vss",
                    new[] { "Vss", "V ss", "V,ss", "Vd,ss",
                            "Steady-State Volume of Distribution",
                            "Steady State Volume of Distribution" },
                    prefix("V(?:d,)?ss", "V\\s*,?\\s*ss",
                           "Steady[\\s-]State\\s+Volume\\s+of\\s+Distribution")),

                new("Vd",
                    new[] { "Vd", "V d", "Volume of Distribution" },
                    prefix("Vd(?![/,])", "V\\s*d(?![/,])", "Volume\\s+of\\s+Distribution")),

                // -- Rate constants --
                new("ke",
                    new[] { "ke", "k el", "kel", "Elimination Rate Constant" },
                    prefix("k\\s*e(?!\\w)", "k\\s*el", "Elimination\\s+Rate\\s+Constant")),

                new("ka",
                    new[] { "ka", "k a", "Absorption Rate Constant" },
                    prefix("k\\s*a(?!\\w)", "Absorption\\s+Rate\\s+Constant")),

                new("λz",
                    new[] { "λz", "Lambda_z", "Lambda z", "Terminal Rate Constant" },
                    prefix("λz", "Lambda[_\\s]z", "Terminal\\s+Rate\\s+Constant")),

                // -- Bioavailability --
                new("F",
                    new[] { "F", "F(%)", "%F", "Bioavailability",
                            "Absolute Bioavailability", "Relative Bioavailability" },
                    prefix("F\\s*\\(\\s*%\\s*\\)", "%\\s*F",
                           "(?:Absolute|Relative)?\\s*Bioavailability")),
            };

            #endregion
        }

        #endregion Canonical Entries

        #region Lookup Index

        // Alias lowercase → canonical name
        private static readonly Dictionary<string, string> _aliasIndex = buildAliasIndex();

        private static Dictionary<string, string> buildAliasIndex()
        {
            #region implementation

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in Entries)
            {
                // Canonical itself is an alias
                addAlias(map, entry.Canonical, entry.Canonical);

                foreach (var alias in entry.Aliases)
                {
                    addAlias(map, alias, entry.Canonical);
                }
            }
            return map;

            #endregion
        }

        private static void addAlias(Dictionary<string, string> map, string alias, string canonical)
        {
            #region implementation

            // Aliases are stored with their parentheses intact — "AUC(0-inf)"
            // must not collapse to the same key as bare "AUC". Stripping is only
            // applied at lookup time.
            var key = storageKey(alias);
            if (string.IsNullOrEmpty(key))
                return;

            // First writer wins — declared canonical names take precedence over
            // cross-family overlap that might otherwise remap a specific alias.
            if (!map.ContainsKey(key))
                map[key] = canonical;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Builds the storage key used by the alias index: unicode-fold, trim,
        /// and collapse whitespace. Crucially does NOT strip trailing parens —
        /// aliases like "AUC(0-inf)" stay distinct from "AUC".
        /// </summary>
        private static string storageKey(string? raw)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var folded = NormalizeUnicode(raw).Trim();
            return _whitespaceCollapse.Replace(folded, " ");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Strips a trailing parenthesized unit suffix from an already-storage-keyed
        /// string. Used by <see cref="TryCanonicalize"/> as a second-chance lookup
        /// so real input like "Cmax (mcg/mL)" collapses to the stored "Cmax" alias.
        /// </summary>
        private static string stripTrailingParen(string key)
        {
            #region implementation

            if (string.IsNullOrEmpty(key))
                return string.Empty;

            var stripped = _trailingParenStrip.Replace(key, string.Empty).Trim();
            return _whitespaceCollapse.Replace(stripped, " ");

            #endregion
        }

        #endregion Lookup Index

        #region Public API

        /**************************************************************/
        /// <summary>
        /// True when <paramref name="raw"/> resolves to any known canonical PK parameter
        /// via alias lookup. Trailing parenthesized unit suffixes are ignored so inputs
        /// like <c>"AUC0-∞(mcg⋅hr/mL)"</c> match.
        /// </summary>
        /// <param name="raw">Candidate text from a header cell or row label.</param>
        /// <returns>True when a canonical match exists.</returns>
        public static bool IsPkParameter(string? raw)
        {
            #region implementation

            return TryCanonicalize(raw, out _);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Resolves <paramref name="raw"/> to its canonical PK parameter name when a
        /// match exists. Canonicalizes variant forms (long-form English, Unicode
        /// infinity / dot operator, alternate punctuation) to the declared canonical.
        /// </summary>
        /// <param name="raw">Candidate text.</param>
        /// <param name="canonical">The canonical ParameterName when matched, otherwise empty.</param>
        /// <returns>True when a canonical match exists.</returns>
        /// <example>
        /// <code>
        /// PkParameterDictionary.TryCanonicalize("AUC0-∞(mcg⋅hr/mL)", out var c); // c = "AUC0-inf"
        /// PkParameterDictionary.TryCanonicalize("Maximum Plasma Concentrations", out var c); // c = "Cmax"
        /// PkParameterDictionary.TryCanonicalize("Poor", out var c); // false, c = ""
        /// </code>
        /// </example>
        public static bool TryCanonicalize(string? raw, out string canonical)
        {
            #region implementation

            canonical = string.Empty;

            // First-chance key: parens intact, so "AUC(0-inf)" matches its alias
            var rawKey = storageKey(raw);
            if (string.IsNullOrEmpty(rawKey))
                return false;

            if (_aliasIndex.TryGetValue(rawKey, out var c1))
            {
                canonical = c1;
                return true;
            }

            // Second-chance: strip a trailing parenthesized unit/qualifier. This
            // handles real cell text like "Cmax (mcg/mL)" or
            // "Maximum Plasma Concentrations (mcg/mL)".
            var strippedKey = stripTrailingParen(rawKey);
            if (!string.IsNullOrEmpty(strippedKey)
                && !string.Equals(strippedKey, rawKey, StringComparison.Ordinal)
                && _aliasIndex.TryGetValue(strippedKey, out var c2))
            {
                canonical = c2;
                return true;
            }

            // Fallback: prefix regex scan for shape-matching variants not enumerated
            // (e.g., "AUC0-96", "AUC0-168h"). Uses the stripped key so trailing unit
            // text does not defeat the match.
            var scanKey = string.IsNullOrEmpty(strippedKey) ? rawKey : strippedKey;
            foreach (var entry in Entries)
            {
                var m = entry.PrefixPattern.Match(scanKey);
                if (m.Success && m.Index == 0)
                {
                    canonical = entry.Canonical;
                    return true;
                }
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Anchored prefix match — true when <paramref name="raw"/> begins with a
        /// canonical PK parameter (or any alias). Used by DoseRegimen triage to
        /// detect PK sub-parameter names that leaked into the dose column.
        /// </summary>
        /// <param name="raw">Candidate text (typically DoseRegimen content).</param>
        /// <returns>True when the text starts with a PK parameter token.</returns>
        public static bool StartsWithPk(string? raw)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var folded = NormalizeUnicode(raw).Trim();
            foreach (var entry in Entries)
            {
                if (entry.PrefixPattern.IsMatch(folded))
                    return true;
            }
            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Substring match — true when <paramref name="raw"/> contains a canonical
        /// PK parameter token anywhere in the string (word-boundary aware). Used
        /// by the table router to recognize modifier phrases like
        /// <c>"Change in AUC"</c> or <c>"Ratio of Cmax"</c> as PK content.
        /// </summary>
        /// <param name="raw">Candidate text (typically a header cell).</param>
        /// <returns>True when the text contains a PK parameter token.</returns>
        public static bool ContainsPkParameter(string? raw)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            return _containsAnyPkPattern.IsMatch(NormalizeUnicode(raw));

            #endregion
        }

        // Combined regex that recognizes any canonical PK short form anywhere in
        // the input. Deliberately conservative — only well-established short forms
        // so that stray tokens (e.g., "F" as a grade descriptor) do not trigger.
        private static readonly Regex _containsAnyPkPattern = new(
            @"\b(?:" +
            @"AUC(?:\w*|\s*\(\s*0\s*[\-–,]\s*(?:inf(?:inity)?|∞|\d+|t|tau)\s*\))?|" +
            @"Cmax|Cmin|Tmax|Cavg|Ctrough|Cthrough|Css|" +
            @"t½|t\s*1/2|T\s*1/2|Half[\s-]?Life|Elimination\s+Half[\s-]?Life|" +
            @"CL(?:/F|ss)?|Cl(?:/F|ss)?|" +
            @"Vd(?:/F)?|V/F|Vz/F|Vss|Volume\s+of\s+Distribution|" +
            @"MRT|Mean\s+Residence\s+Time|" +
            @"(?:Maximum|Peak)\s+Plasma\s+Concentrations?|" +
            @"(?:Plasma|Total|Systemic|Renal|Oral|Apparent)\s+Clearance|" +
            @"Bioavailability" +
            @")\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Public API
    }
}
