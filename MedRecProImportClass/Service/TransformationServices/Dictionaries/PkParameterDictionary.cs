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

        // U+00B7 MIDDLE DOT — canonical form matching UnitDictionary.NormalizationMap
        private const char MIDDLE_DOT = '\u00B7';

        // U+221E INFINITY SIGN
        private const char INFINITY_SIGN = '\u221E';

        // R4 — U+2044 FRACTION SLASH, U+2215 DIVISION SLASH. Both appear in real
        // SPL tables as the slash in "t1⁄2" variants (U+2044) and occasionally in
        // unit expressions. Fold to ASCII '/' so downstream regex/lookup matches.
        private const char FRACTION_SLASH = '\u2044';
        private const char DIVISION_SLASH = '\u2215';

        // Collapse sequences of whitespace to a single space
        private static readonly Regex _whitespaceCollapse = new(@"\s+", RegexOptions.Compiled);

        // Trailing parenthesized unit expression such as "(mcg/mL)", "(hr)", "(%)", "(mL/min/kg)"
        // Conservative: only strips a single trailing "(...)" group with no nested parens.
        // Optional trailing footnote markers (*, †, ‡, §, ¶, #) after the paren are also
        // consumed so cell text like "AUC48(ng·h/mL)*" canonicalizes correctly.
        private static readonly Regex _trailingParenStrip = new(
            @"\s*\([^()]{1,40}\)\s*[*†‡§¶#]*\s*$",
            RegexOptions.Compiled);

        // Trailing footnote markers ONLY (no parens). Used to strip superscript
        // asterisk/dagger/etc. before lookups so inputs like "AUCT*" or "Cmax†"
        // collapse to their clean form.
        private static readonly Regex _trailingFootnoteStrip = new(
            @"\s*[*†‡§¶#]+\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Folds Unicode variants so downstream lookups see a canonical codepoint form.
        /// Maps U+22C5 (DOT OPERATOR, `⋅`) onto U+00B7 (MIDDLE DOT, `·`),
        /// U+2044 (FRACTION SLASH, `⁄`) and U+2215 (DIVISION SLASH, `∕`) onto
        /// ASCII `/`, then runs an NFKC normalization pass over the result.
        /// </summary>
        /// <param name="input">Raw text from the source table.</param>
        /// <returns>Canonicalized string, or the empty string when <paramref name="input"/> is null.</returns>
        /// <remarks>
        /// Real SPL tables use:
        /// - `⋅` (U+22C5) in unit expressions like `mcg⋅hr/mL` — folded to `·`
        ///   (U+00B7) so the <see cref="UnitDictionary.NormalizationMap"/> keys match.
        /// - `⁄` (U+2044 FRACTION SLASH) in `t1⁄2` variants — folded to `/` so
        ///   `t1/2` resolves to the canonical `t½` entry.
        /// - `∕` (U+2215 DIVISION SLASH) in rare compound units — folded to `/`
        ///   for symmetry.
        /// </remarks>
        public static string NormalizeUnicode(string? input)
        {
            #region implementation

            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Fast path: replace known codepoint variants first
            var folded = input
                .Replace(DOT_OPERATOR, MIDDLE_DOT)
                .Replace(FRACTION_SLASH, '/')
                .Replace(DIVISION_SLASH, '/');

            // NFKC also collapses things like fullwidth digits; safe for unit text
            return folded.Normalize(NormalizationForm.FormKC);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R4 — Biological-matrix prefixes that sometimes prepend a canonical PK
        /// term in SPL tables (<c>"Serum T1/2"</c>, <c>"Plasma Cmax"</c>,
        /// <c>"Blood AUC"</c>). Stripping the matrix prefix at lookup time lets
        /// the downstream alias index resolve the bare canonical without
        /// declaring matrix-prefixed aliases on every entry.
        /// </summary>
        private static readonly Regex _matrixPrefixStrip = new(
            @"^\s*(?:Serum|Plasma|Blood|Whole\s+Blood|Urine|Cerebrospinal\s+Fluid|CSF)\s+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
            // character classes and quantifiers) — the helper joins them with `|`,
            // anchors at the start, and appends a "not-followed-by-word-char"
            // lookahead as the terminator.
            //
            // We use `(?!\w)` instead of `\b` because `\b` requires a transition
            // between word and non-word characters and FAILS at end-of-string when
            // the preceding char is non-word (e.g., U+221E INFINITY in "AUC0-∞").
            // `(?!\w)` succeeds both at end-of-string and before any non-word char,
            // giving the same semantics as `\b` for ASCII inputs while also
            // handling Unicode symbols correctly.
            static Regex prefix(params string[] alts)
            {
                var joined = string.Join("|", alts);
                return new Regex(@"^\s*(?:" + joined + @")(?!\w)",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }

            return new List<PkEntry>
            {
                // -- Concentration metrics --
                new("Cmax",
                    new[] { "Cmax", "C max", "Cmax,ss", "Cmax ss",
                            // R5 — numeric-suffix period variants and shorthand
                            "Cmax1", "Cmax2", "Cmax 1", "Cmax 2",
                            "Peak Conc.", "Peak Conc",
                            "Maximum Plasma Concentration", "Maximum Plasma Concentrations",
                            "Maximum Concentration", "Peak Plasma Concentration",
                            "Peak Concentration", "Peak Concentrations",
                            "Peak concentration at steady state",
                            "Peak Concentration at Steady State" },
                    prefix("Cmax(?:\\s*[12])?", "C\\s*max(?:\\s*[12])?",
                           "Peak\\s+Conc\\.?",
                           "Maximum\\s+Plasma\\s+Concentrations?",
                           "Peak\\s+Plasma\\s+Concentrations?",
                           "Peak\\s+Concentrations?(?:\\s+at\\s+steady\\s+state)?",
                           "Maximum\\s+Concentrations?")),

                new("Cmin",
                    new[] { "Cmin", "C min", "Cmin,ss",
                            // R5 — inter-peak minimum (observed as Cminip in TID 571 shape)
                            "Cminip", "Cmin,ip", "Cmin ip",
                            "Minimum Plasma Concentration", "Minimum Concentration" },
                    prefix("Cmin(?:ip|,ip|\\s*ip)?", "C\\s*min",
                           "Minimum\\s+Plasma\\s+Concentration",
                           "Minimum\\s+Concentration")),

                new("Ctrough",
                    new[] { "Ctrough", "Cthrough", "C trough",
                            "Trough Concentration", "Trough Plasma Concentration",
                            "C0h", "C 0h", "C0", "C 0",
                            // R5 — additional timepoint-indexed concentrations.
                            // C12, C24, C48, C72, C96 are "concentration at hour N"
                            // measurements reported in steady-state PK tables;
                            // semantically they are trough-like observations, so
                            // they collapse to the Ctrough canonical.
                            "C12", "C12h", "C 12", "C 12h",
                            "C24", "C24h", "C 24", "C 24h",
                            "C48", "C48h", "C 48", "C 48h",
                            "C72", "C72h", "C 72", "C 72h",
                            "C96", "C96h", "C 96", "C 96h",
                            "Concentration at 0 hours", "Predose Concentration" },
                    prefix("Ctrough", "Cthrough", "C\\s*trough",
                           "Trough\\s+(?:Plasma\\s+)?Concentration",
                           "C\\s*0\\s*h\\b", "Predose\\s+Concentration",
                           "Concentration\\s+at\\s+0\\s+hours?",
                           // R5 — generic C<N>[h] pattern for concentration-at-hour
                           // time points. Excludes C0 (covered above) and C<N>-<M>
                           // range forms (reserved for AUC-style intervals).
                           "C\\s*\\d{1,3}\\s*h?(?![\\w\\-/])")),

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
                            // R5 — numeric-suffix period variants
                            "Tmax1", "Tmax2", "Tmax 1", "Tmax 2",
                            "Time to Maximum Concentration", "Time to Peak",
                            "Time to Peak Concentration",
                            "TPEAK", "T PEAK", "T peak", "T-peak",
                            "Time of Peak" },
                    prefix("Tmax(?:\\s*[12])?", "T\\s*max(?:\\s*[12])?",
                           "TPEAK", "T[\\s-]?peak",
                           "Time\\s+to\\s+Maximum\\s+Concentration",
                           "Time\\s+to\\s+Peak",
                           "Time\\s+of\\s+Peak")),

                new("t½",
                    new[] { "t½", "t1/2", "T 1/2", "T1/2", "Half-life", "Half Life",
                            "Half-Life", "Elimination Half-life", "Elimination Half Life",
                            "Elimination Half-Life", "Terminal Half-life",
                            "Terminal Half Life", "Plasma Half-life", "Apparent Half-life",
                            // R5 — compact/variant forms observed in SPL corpus:
                            // "t1/2terminal" (no space), "t1/2λz", and distribution-phase
                            // variants. The U+2044 FRACTION SLASH case "t1⁄2" is
                            // normalized to "t1/2" by NormalizeUnicode before lookup.
                            "t1/2terminal", "t1/2 terminal", "t 1/2 terminal",
                            "t1/2λz", "t1/2 λz", "t1/2 lambda-z", "t1/2 lambda z",
                            "t1/2 β", "t1/2β", "t1/2 alpha", "t1/2α",
                            "Distribution Half-life", "Distribution Half Life",
                            "Distribution Half-Life" },
                    prefix("t½", "t\\s*1/2(?:\\s*(?:terminal|λz|β|α|alpha|beta|lambda[\\s-]?z))?",
                           "T\\s*1/2(?:\\s*(?:terminal|λz|β|α|alpha|beta|lambda[\\s-]?z))?",
                           "(?:Elimination|Terminal|Plasma|Apparent|Distribution)\\s+Half[\\s-]?Life",
                           "Half[\\s-]?Life")),

                new("MRT",
                    new[] { "MRT", "Mean Residence Time" },
                    prefix("MRT", "Mean\\s+Residence\\s+Time")),

                new("MAT",
                    new[] { "MAT", "Mean Absorption Time" },
                    prefix("MAT", "Mean\\s+Absorption\\s+Time")),

                // R5 — Absorption lag time (tlag). Observed in oral PK tables as a
                // separate time-domain metric; keep as its own canonical rather
                // than collapsing to Tmax because the semantics differ (tlag is
                // the delay before any drug appears, not the time-to-peak).
                new("tlag",
                    new[] { "tlag", "t lag", "t-lag", "tLag", "Tlag",
                            "Lag Time", "Absorption Lag Time" },
                    prefix("t[\\s-]?lag", "Absorption\\s+Lag\\s+Time", "Lag\\s+Time")),

                // -- AUC family (more specific variants appear with their own canonical forms) --
                new("AUC0-inf",
                    new[] { "AUC0-inf", "AUC(0-inf)", "AUC0-infinity", "AUC0-∞",
                            "AUC(0-∞)", "AUC∞", "AUCinf", "AUC 0-inf",
                            "AUC 0-∞", "AUC 0-infinity", "AUC(0,inf)",
                            "AUC0 to ∞", "AUC0 to inf", "AUC0 to infinity",
                            "AUC 0 to ∞", "AUC 0 to inf", "AUC 0 to infinity",
                            "AUC(0 to ∞)", "AUC(0 to inf)",
                            "Area under the curve (AUC0-∞)" },
                    prefix("AUC\\s*[\\(]?\\s*0\\s*(?:[\\-–,]|\\s+to\\s+)\\s*(?:inf(?:inity)?|∞)",
                           "AUC\\s*∞", "AUCinf")),

                new("AUC0-24",
                    new[] { "AUC0-24", "AUC0-24h", "AUC(0-24)", "AUC(0-24h)",
                            "AUC 0-24", "AUC 0-24 hr", "AUC 0-24h", "AUC24", "AUC24h",
                            "AUC0 to 24", "AUC 0 to 24", "AUC0 to 24h",
                            "AUC(0 to 24)", "AUC(0 to 24h)" },
                    prefix("AUC\\s*[\\(]?\\s*0\\s*(?:[\\-–,]|\\s+to\\s+)\\s*24\\s*h?",
                           "AUC24\\s*h?\\b")),

                new("AUC0-12",
                    new[] { "AUC0-12", "AUC0-12h", "AUC(0-12)", "AUC 0-12", "AUC12", "AUC12h",
                            "AUC0 to 12", "AUC 0 to 12", "AUC(0 to 12)" },
                    prefix("AUC\\s*[\\(]?\\s*0\\s*(?:[\\-–,]|\\s+to\\s+)\\s*12\\s*h?",
                           "AUC12\\s*h?\\b")),

                new("AUC0-t",
                    new[] { "AUC0-t", "AUC(0-t)", "AUC 0-t",
                            "AUC0 to t", "AUC 0 to t", "AUC(0 to t)" },
                    prefix("AUC\\s*[\\(]?\\s*0\\s*(?:[\\-–,]|\\s+to\\s+)\\s*t(?![\\w])")),

                new("AUClast",
                    new[] { "AUClast", "AUC last", "AUC_last",
                            // R5 — "AUC to last detectable concentration" variant
                            "AUCtldc", "AUC tldc", "AUC_tldc",
                            "AUC to last measurable", "AUC to last measurable concentration",
                            "AUC to last detectable", "AUC to last detectable concentration" },
                    prefix("AUClast", "AUCtldc", "AUC[\\s_](?:last|tldc)",
                           "AUC\\s+to\\s+last\\s+(?:measurable|detectable)")),

                new("AUCtau",
                    new[] { "AUCtau", "AUC tau", "AUC(0-tau)", "AUC 0-tau",
                            "AUC over dosing interval",
                            "AUCT", "AUCτ", "AUC(τ)", "AUC(T)",
                            "AUC,tau", "AUC,τ" },
                    prefix("AUCtau", "AUC\\s*[\\(]?\\s*0\\s*[\\-–,]\\s*tau",
                           "AUCT(?![a-z0-9])", "AUC\\s*τ",
                           "AUC\\s+over\\s+dosing\\s+interval")),

                new("AUC",
                    // Generic AUC falls through when no 0-X qualifier is present,
                    // OR when the interval is a non-standard integer count of hours
                    // (AUC48, AUC72, AUC8, AUC96, …) — those aren't dedicated
                    // canonicals so they collapse to the generic AUC.
                    new[] { "AUC", "Total AUC", "Overall AUC",
                            "Area Under the Curve", "Area Under Curve",
                            "Area Under the Concentration-Time Curve" },
                    prefix("AUC(?![0-9\\-])",
                           // AUC<N>[h|d] catch-all. The `(?!0)` lookahead reserves
                           // AUC0-XX forms (AUC0-inf, AUC0-24, AUC0-12, AUC0-t)
                           // for their dedicated entries — without this, a raw
                           // "AUC0 to ∞" would eagerly match AUC via the \d+.
                           // Non-standard intervals (AUC48, AUC72, AUC8, AUC96)
                           // still collapse to the generic AUC via this pattern.
                           // R5 — extend with 'd' suffix for AUC<N>d variants
                           // observed in thyroid/hormone PK tables.
                           "AUC(?!0)\\d+\\s*[hd]?\\b",
                           // R5 — AUC0-<N>d form (e.g., "AUC0-180d") not covered
                           // by the dedicated 24/12/t entries. Collapses to
                           // generic AUC rather than introducing a day-scoped
                           // canonical for every possible interval.
                           "AUC\\s*[\\(]?\\s*0\\s*(?:[\\-–,]|\\s+to\\s+)\\s*\\d+\\s*d",
                           "Total\\s+AUC(?![0-9\\-])",
                           "Overall\\s+AUC(?![0-9\\-])",
                           "Area\\s+Under\\s+(?:the\\s+)?(?:Concentration-Time\\s+)?Curve")),

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
                    new[] { "CLr", "CL r", "Renal Clearance",
                            // R5 — CLREN / CLcr / CLCR shorthand forms commonly
                            // used in SPL renal-impairment PK tables. The bare
                            // abbreviation forms canonicalize here; when bound
                            // to a numeric range ("CLCR 50-80 mL/min") the
                            // content is a POPULATION descriptor — handled by
                            // PopulationDetector regex / R6 subtype routing,
                            // not here.
                            "CLREN", "CLren", "CL ren", "CL-REN",
                            "CLcr", "CLCR", "CL cr", "CL-CR",
                            "Creatinine Clearance" },
                    prefix("CL[\\s-]?(?:r|REN|ren|cr|CR)(?!\\w)",
                           "Renal\\s+Clearance",
                           "Creatinine\\s+Clearance")),

                // -- Volume of distribution --
                new("Vd/F",
                    new[] { "Vd/F", "V/F", "Vz/F", "Vd,z/F",
                            "Apparent Volume of Distribution",
                            "Apparent Oral Volume of Distribution" },
                    prefix("Vd/F", "V/F", "Vz/F", "Vd,z/F",
                           "Apparent\\s+(?:Oral\\s+)?Volume\\s+of\\s+Distribution")),

                new("Vss",
                    new[] { "Vss", "V ss", "V,ss", "Vd,ss",
                            // R5 — Vdss is the same concept as Vss (the "d" stands
                            // for "distribution", which is redundant with "volume of
                            // distribution"). Collapse to Vss canonical.
                            "Vdss", "Vd ss", "Vd-ss", "Vd SS", "V dss",
                            "Steady-State Volume of Distribution",
                            "Steady State Volume of Distribution",
                            "Volume of Distribution at Steady State",
                            "Volume of Distribution at Steady-State",
                            "Volume of distribution at steady state" },
                    prefix("V(?:d[,\\s-]?)?ss", "V\\s*,?\\s*ss", "Vdss",
                           "Steady[\\s-]State\\s+Volume\\s+of\\s+Distribution",
                           "Volume\\s+of\\s+Distribution\\s+at\\s+Steady[\\s-]State")),

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
        /// strip trailing footnote markers, and collapse whitespace. Crucially
        /// does NOT strip trailing parens — aliases like "AUC(0-inf)" stay
        /// distinct from "AUC".
        /// </summary>
        private static string storageKey(string? raw)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var folded = NormalizeUnicode(raw).Trim();
            // Strip trailing footnote markers (*, †, ‡, §, ¶, #) so cell text
            // like "Cmax*" or "AUCT†" still maps to the clean alias.
            folded = _trailingFootnoteStrip.Replace(folded, string.Empty);
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

            // R4 — Third-chance lookup with biological-matrix prefix stripped.
            // Applies to "Serum T1/2", "Plasma Cmax", "Blood AUC", etc. Stripping
            // only happens when the prefix regex actually matches and leaves
            // non-empty content, so pure "Serum" never collapses to an empty lookup.
            var matrixStripped = _matrixPrefixStrip.Replace(scanKey, string.Empty).Trim();
            if (!string.IsNullOrEmpty(matrixStripped)
                && !string.Equals(matrixStripped, scanKey, StringComparison.Ordinal))
            {
                // Try alias index first, then prefix regex scan on the stripped form.
                if (_aliasIndex.TryGetValue(matrixStripped, out var c3))
                {
                    canonical = c3;
                    return true;
                }
                foreach (var entry in Entries)
                {
                    var m = entry.PrefixPattern.Match(matrixStripped);
                    if (m.Success && m.Index == 0)
                    {
                        canonical = entry.Canonical;
                        return true;
                    }
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

        /**************************************************************/
        /// <summary>
        /// Extracts a canonical PK parameter from a longer descriptive phrase,
        /// returning the canonical plus any detected qualifier (<c>steady_state</c>,
        /// <c>single_dose</c>, <c>terminal</c>, <c>distribution</c>).
        /// </summary>
        /// <remarks>
        /// ## Resolution order
        /// 1. <see cref="TryCanonicalize"/> on the full string — if it succeeds,
        ///    the qualifier side-lookup runs on the original input and returns
        ///    <c>("Cmax", "steady_state")</c> for <c>"Cmax,ss"</c>, etc.
        /// 2. Recurse on content INSIDE the trailing parenthesized suffix
        ///    (<c>"Systemic clearance (CL, mL/day)"</c> → inner content <c>"CL,
        ///    mL/day"</c> → first comma-split token is the PK term).
        /// 3. Scan each prefix regex against every cut of the input; take the
        ///    first multi-character match (single-character canonicals like
        ///    <c>F</c> are skipped to avoid <c>"Fasted"</c>-style false positives).
        /// 4. Return <c>false</c> when nothing matches.
        /// </remarks>
        /// <param name="raw">The candidate phrase, which may embed a PK term
        /// inside descriptive English (e.g.,
        /// <c>"Peak concentration at steady state (Cmax,ss, mcg/mL)"</c>).</param>
        /// <param name="canonical">Canonical PK parameter name when found.</param>
        /// <param name="qualifier">Detected qualifier (<c>steady_state</c>,
        /// <c>single_dose</c>, <c>terminal</c>, <c>distribution</c>) or null.</param>
        /// <returns>True when a canonical match is found inside the phrase.</returns>
        /// <seealso cref="TryCanonicalize"/>
        public static bool TryExtractCanonicalFromPhrase(
            string? raw, out string canonical, out string? qualifier)
        {
            #region implementation

            canonical = string.Empty;
            qualifier = null;

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            // Side-channel qualifier detection runs on the original text regardless
            // of which branch succeeds. Keyword matching is case-insensitive.
            qualifier = detectQualifier(raw);

            var folded = NormalizeUnicode(raw);

            // 1. Try content INSIDE trailing parens FIRST — the parenthesized
            //    suffix is typically the most specific form (e.g.,
            //    "Area under the curve (AUC0-∞, day•mcg/mL)" → specific
            //    canonical is "AUC0-inf", not the generic "AUC" that would
            //    match "Area Under the Curve" outside the parens).
            var parenMatch = _trailingParenCaptureInner.Match(folded);
            if (parenMatch.Success)
            {
                var inner = parenMatch.Groups[1].Value.Trim();
                // Inside parens, the PK term is usually the first comma-split token.
                var firstToken = inner.Split(',').FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(firstToken) &&
                    TryCanonicalize(firstToken, out var cInner))
                {
                    canonical = cInner;
                    return true;
                }
                // Fall back to the whole inner text as a single-shot lookup.
                if (TryCanonicalize(inner, out var cInnerAll))
                {
                    canonical = cInnerAll;
                    return true;
                }
            }

            // 2. Scan prefix patterns across every word position in the input.
            //    Prefers the more specific canonical (Entries are ordered so that
            //    AUC0-inf / AUC0-24 / AUC0-t / AUClast / AUCtau appear BEFORE the
            //    generic AUC — iterating in Entries order gives specificity
            //    precedence when both could match).
            //    Word-start positions: 0, and every position after whitespace,
            //    comma, or opening paren. The prefix regex anchors at `^` so we
            //    test each candidate start position by slicing the string.
            var wordStarts = computeWordStarts(folded);
            foreach (var entry in Entries)
            {
                if (entry.Canonical.Length <= 1)
                    continue;

                foreach (var pos in wordStarts)
                {
                    if (pos >= folded.Length)
                        continue;
                    var candidate = pos == 0 ? folded : folded.Substring(pos);
                    if (entry.PrefixPattern.IsMatch(candidate))
                    {
                        canonical = entry.Canonical;
                        return true;
                    }
                }
            }

            // 3. Whole-string TryCanonicalize as final fallback (generic
            //    matches like "Area Under the Curve" → AUC when no more
            //    specific term was found above).
            if (TryCanonicalize(raw, out var c0))
            {
                canonical = c0;
                return true;
            }

            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Returns all word-start positions in the input: position 0 plus every
        /// position immediately following whitespace, comma, or opening paren.
        /// Used by <see cref="TryExtractCanonicalFromPhrase"/> to test each
        /// candidate start against the anchored prefix regex via substring.
        /// </summary>
        private static List<int> computeWordStarts(string folded)
        {
            #region implementation

            var starts = new List<int> { 0 };
            for (int i = 0; i < folded.Length - 1; i++)
            {
                var c = folded[i];
                if (char.IsWhiteSpace(c) || c == ',' || c == '(' || c == '/')
                {
                    starts.Add(i + 1);
                }
            }
            return starts;

            #endregion
        }

        // Captures the content INSIDE a trailing parenthesized suffix. Non-nested.
        private static readonly Regex _trailingParenCaptureInner = new(
            @"\(([^()]{1,80})\)\s*$",
            RegexOptions.Compiled);

        /**************************************************************/
        /// <summary>
        /// Side-channel qualifier detection used by
        /// <see cref="TryExtractCanonicalFromPhrase"/>. Returns a canonical
        /// qualifier string for common PK sub-conditions observed in descriptive
        /// labels, or null when no qualifier signal is present.
        /// </summary>
        private static string? detectQualifier(string raw)
        {
            #region implementation

            var lower = raw.ToLowerInvariant();

            if (lower.Contains("steady state") || lower.Contains("steady-state") ||
                lower.Contains(",ss") || lower.Contains(", ss"))
                return "steady_state";
            if (lower.Contains("single dose") || lower.Contains("single-dose"))
                return "single_dose";
            if (lower.Contains("terminal"))
                return "terminal";
            if (lower.Contains("distribution half") || lower.Contains("distribution phase"))
                return "distribution";
            if (lower.Contains("fasted"))
                return "fasted";
            if (lower.Contains("fed"))
                return "fed";

            return null;

            #endregion
        }

        // Combined regex that recognizes any canonical PK short form anywhere in
        // the input. Deliberately conservative — only well-established short forms
        // so that stray tokens (e.g., "F" as a grade descriptor) do not trigger.
        private static readonly Regex _containsAnyPkPattern = new(
            @"\b(?:" +
            @"AUC(?:\w*|\s*\(?\s*0\s*(?:[\-–,]|\s+to\s+)\s*(?:inf(?:inity)?|∞|\d+|t|tau)\s*\)?)?|" +
            @"Total\s+AUC|Overall\s+AUC|" +
            @"Cmax(?:\s*[12])?|Cmin(?:ip)?|Tmax(?:\s*[12])?|TPEAK|T[\s-]?peak|Cavg|Ctrough|Cthrough|Css|" +
            @"C\s*0\s*h\b|C\s*\d{1,3}\s*h?\b|Predose\s+Concentration|Peak\s+Conc\.?|" +
            @"t½|t\s*1/2|T\s*1/2|t[\s-]?lag|Half[\s-]?Life|Elimination\s+Half[\s-]?Life|" +
            @"CL(?:/F|ss)?|Cl(?:/F|ss)?|CL[\s-]?(?:r|REN|ren|cr|CR)|Creatinine\s+Clearance|" +
            @"Vd(?:/F|ss)?|V/F|Vz/F|Vss|Vdss|Volume\s+of\s+Distribution|" +
            @"MRT|Mean\s+Residence\s+Time|Absorption\s+Lag\s+Time|" +
            @"(?:Maximum|Peak)\s+(?:Plasma\s+)?Concentrations?|" +
            @"(?:Plasma|Total|Systemic|Renal|Oral|Apparent)\s+Clearance|" +
            @"Bioavailability" +
            @")\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Public API
    }
}
