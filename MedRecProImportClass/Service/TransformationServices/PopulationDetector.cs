using System.Text.RegularExpressions;

namespace MedRecProImportClass.Service.TransformationServices
{
    /**************************************************************/
    /// <summary>
    /// Static utility for auto-detecting patient population from Caption and SectionTitle
    /// fields in Stage 3 of the SPL Table Normalization pipeline. Uses regex extraction
    /// with Levenshtein-based fuzzy validation to assess detection quality.
    /// </summary>
    /// <remarks>
    /// ## Detection Strategy
    /// 1. Extract population from Caption via known patterns (e.g., "Pediatric Patients",
    ///    "Postmenopausal Women", age-based like "≥65 years")
    /// 2. Extract population from SectionTitle (e.g., "Pharmacokinetics in {Population}")
    /// 3. Cross-validate: when both sources yield a population, compute similarity score.
    ///    If similarity is LOW (&lt;0.5), flag POP_MISMATCH in ValidationFlags.
    ///
    /// ## Confidence Thresholds
    /// - ≥0.8: HIGH — keyword match with cross-validation
    /// - 0.5–0.8: MEDIUM — single-source extraction or partial match
    /// - &lt;0.5: LOW — heuristic guess or mismatch
    /// </remarks>
    /// <seealso cref="ParsedObservation"/>
    public static class PopulationDetector
    {
        #region Known Population Patterns

        // Regex patterns for common population descriptors in captions
        private static readonly Regex _captionPopulationPattern = new(
            @"(?:in|for|of|among)\s+(?:the\s+)?(?<pop>" +
            @"(?:adult|pediatric|geriatric|neonatal|postmenopausal|premenopausal|pregnant)\s+\w+" +
            @"|(?:premature|preterm)\s+(?:infants?|neonates?)" +
            @"|(?:healthy\s+)?(?:adult|male|female)\s+(?:healthy\s+)?volunteers?" +
            @"|(?:patients?\s+with\s+\w[\w\s]{2,30})" +
            @"|(?:children|adolescents?|infants?|neonates?)" +
            @"|(?:renal|hepatic)\s+impairment" +
            @"|(?:[\d]+\s*(?:to|[-–])\s*[\d]+\s*years?)" +
            @"|(?:[≥≤><]\s*\d+\s*years?)" +
            @")",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Regex patterns for section title population extraction
        private static readonly Regex _sectionPopulationPattern = new(
            @"(?:Pharmacokinetics\s+in\s+|Clinical\s+Studies\s+in\s+|Use\s+in\s+)(?<pop>[^(]+?)(?:\s*\(|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Known population keywords for dictionary-based extraction
        private static readonly Dictionary<string, string> _populationKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            { "pediatric", "Pediatric" },
            { "geriatric", "Geriatric" },
            { "neonatal", "Neonatal" },
            { "postmenopausal", "Postmenopausal Women" },
            { "premenopausal", "Premenopausal Women" },
            { "pregnant", "Pregnant Women" },
            { "premature infants", "Premature Infants" },
            { "preterm infants", "Premature Infants" },
            { "renal impairment", "Renal Impairment" },
            { "hepatic impairment", "Hepatic Impairment" },
            { "healthy volunteers", "Healthy Volunteers" },
            { "adult healthy", "Adult Healthy Volunteers" },
        };

        #endregion Known Population Patterns

        #region Public Methods

        /**************************************************************/
        /// <summary>
        /// Detects patient population from Caption and SectionTitle fields.
        /// Cross-validates when both sources yield a result.
        /// </summary>
        /// <param name="caption">Table caption text (HTML-stripped).</param>
        /// <param name="sectionTitle">Section title from vw_SectionNavigation.</param>
        /// <param name="parentSectionTitle">Parent section title for additional context.</param>
        /// <returns>
        /// Tuple of (detected population string, confidence 0.0–1.0).
        /// Returns (null, 0.0) if no population could be detected.
        /// </returns>
        /// <example>
        /// <code>
        /// var (pop, conf) = PopulationDetector.DetectPopulation(
        ///     "Table 3: PK in Pediatric Patients", "Pharmacokinetics in Pediatric Patients", null);
        /// // pop = "Pediatric Patients", conf = 0.95
        /// </code>
        /// </example>
        public static (string? population, double confidence) DetectPopulation(
            string? caption, string? sectionTitle, string? parentSectionTitle)
        {
            #region implementation

            var fromCaption = extractFromCaption(caption);
            var fromSection = extractFromSectionTitle(sectionTitle);

            // If both are null, try parent section title as fallback
            if (fromCaption == null && fromSection == null)
            {
                fromSection = extractFromSectionTitle(parentSectionTitle);
            }

            // Both sources yielded a result — cross-validate
            if (fromCaption != null && fromSection != null)
            {
                var similarity = computeLevenshteinRatio(
                    fromCaption.ToLowerInvariant(),
                    fromSection.ToLowerInvariant());

                if (similarity >= 0.5)
                {
                    // Good agreement — use the longer (more descriptive) one
                    var best = fromCaption.Length >= fromSection.Length ? fromCaption : fromSection;
                    return (best, Math.Min(0.95, 0.7 + similarity * 0.3));
                }
                else
                {
                    // Mismatch — use section title (more authoritative), lower confidence
                    return (fromSection, 0.4);
                }
            }

            // Single source
            if (fromCaption != null)
                return (fromCaption, 0.7);

            if (fromSection != null)
                return (fromSection, 0.8);

            // No population detected
            return (null, 0.0);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes normalized similarity between two strings using Levenshtein distance.
        /// Returns 1.0 for identical strings, 0.0 for completely different strings.
        /// </summary>
        /// <param name="a">First string.</param>
        /// <param name="b">Second string.</param>
        /// <returns>Similarity ratio from 0.0 to 1.0.</returns>
        public static double ComputeSimilarity(string a, string b)
        {
            #region implementation

            if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
                return 1.0;

            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                return 0.0;

            return computeLevenshteinRatio(a, b);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Attempts to resolve a column 0 or ParameterName cell value to a canonical
        /// population string. Handles the standard populations already covered by
        /// <see cref="_populationKeywords"/> plus CYP metabolizer phenotypes
        /// (Poor / Intermediate / Normal / Ultrarapid / Extensive) that appear in
        /// pharmacogenomic PK tables.
        /// </summary>
        /// <param name="raw">Candidate row-label or ParameterName text.</param>
        /// <param name="canonical">Canonical population string when matched, otherwise empty.</param>
        /// <returns>True when a match is found.</returns>
        /// <remarks>
        /// Matching is deliberately strict — it only fires on exact or near-exact text
        /// matches against the label dictionary so that free-form prose in a
        /// ParameterName cell is not mis-routed. Callers should only invoke this when
        /// the surrounding TableCategory is PK (or similar) where a population-style
        /// label would be a schema violation.
        /// </remarks>
        public static bool TryMatchLabel(string? raw, out string canonical)
        {
            return TryMatchLabel(raw, out canonical, out _);
        }

        /**************************************************************/
        /// <summary>
        /// Overload of <see cref="TryMatchLabel(string?, out string)"/> that also
        /// reports whether the match came from the strict dictionary or the regex
        /// second-pass. Callers can use <paramref name="matchedViaRegex"/> to emit
        /// distinct validation flags (dictionary match vs. open-form regex match).
        /// </summary>
        /// <param name="raw">Candidate row-label or ParameterName text.</param>
        /// <param name="canonical">Canonical population string when matched.</param>
        /// <param name="matchedViaRegex">True when the regex second-pass (age range,
        /// renal function band, infant-birth-to-N, trimester) produced the match;
        /// false when the strict dictionary hit first.</param>
        /// <returns>True when a match is found.</returns>
        public static bool TryMatchLabel(string? raw, out string canonical, out bool matchedViaRegex)
        {
            #region implementation

            canonical = string.Empty;
            matchedViaRegex = false;

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var key = _whitespaceCollapse.Replace(raw.Trim(), " ");

            if (_labelToCanonical.TryGetValue(key, out var hit))
            {
                canonical = hit;
                return true;
            }

            // Regex second-pass: open-form population descriptors not enumerable
            // as dictionary entries. Patterns are anchored (^...$) where sensible
            // to avoid over-matching arbitrary prose.
            foreach (var (rx, canonicalize) in _populationRegexPatterns)
            {
                var m = rx.Match(key);
                if (m.Success)
                {
                    canonical = canonicalize(m);
                    matchedViaRegex = true;
                    return true;
                }
            }

            return false;

            #endregion
        }

        #endregion Public Methods

        #region Regex Population Patterns

        /**************************************************************/
        /// <summary>
        /// Regex-based population descriptors for open-form row labels that
        /// cannot be practically enumerated as dictionary entries — age ranges,
        /// renal function bands, infant-birth-to-N phrases, and pregnancy
        /// trimesters. Each pattern is paired with a canonicalizer function
        /// that produces the output Population value.
        /// </summary>
        /// <remarks>
        /// Match order does not matter — patterns are mutually exclusive by
        /// their anchors.
        /// </remarks>
        private static readonly (Regex rx, Func<Match, string> canonicalize)[]
            _populationRegexPatterns = new (Regex, Func<Match, string>)[]
        {
            // Age range: "6 to 11 years", "12-17 years", "18 – 64 Years"
            (new Regex(
                @"^\s*(?<lo>\d+)\s*(?:to|[-–])\s*(?<hi>\d+)\s*years?\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
             m => $"Ages {m.Groups["lo"].Value}-{m.Groups["hi"].Value} Years"),

            // Infants birth-to-N: "Infants from Birth to 12 Months", "Infant Birth to 2 Years"
            (new Regex(
                @"^\s*Infants?\s+(?:from\s+)?Birth\s+to\s+(?<hi>\d+)\s+(?<unit>Months?|Years?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
             m => $"Infants Birth to {m.Groups["hi"].Value} " +
                  titleCase(m.Groups["unit"].Value.TrimEnd('s') + "s")),

            // Renal function band: "Normal Creatinine Clearance 90-140 mL/min",
            // "Mild Renal Impairment Creatinine Clearance 60-89 mL/min",
            // "ESRD Creatinine Clearance <10 mL/min on Hemodialysis"
            (new Regex(
                @"^\s*(?<band>Normal|Mild|Moderate|Severe|ESRD|End[\s-]Stage)\b" +
                @".*?Creatinine\s+Clearance",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
             m => $"{titleCase(m.Groups["band"].Value)} Renal Function"),

            // Trimester: "2nd Trimester of Pregnancy", "Third Trimester", and
            // compressed "2Trimester" (digit-no-space — observed in OCR output
            // from SPL tables where the superscript "nd"/"rd" was dropped).
            (new Regex(
                @"^\s*(?<ord>1st|2nd|3rd|First|Second|Third|1|2|3)\s*Trimester",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
             m => $"{normalizeOrdinal(m.Groups["ord"].Value)} Trimester"),

            // R1.1: age-qualified Subjects with trailer — "Elderly Subjects (mean age, 70.5 year)",
            // "Healthy Subjects (N=18)", "Pediatric Patients aged 6 to 16".
            // Strips the parenthesized / descriptor trailer and returns the
            // bare age stratum as the canonical Population.
            (new Regex(
                @"^\s*(?<pop>Elderly|Young|Adult|Pediatric|Geriatric|Healthy|Hemodialysis)\s+(?:Subjects?|Patients?|Volunteers?|Adults?|Children)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
             m => titleCase(m.Groups["pop"].Value) switch {
                 "Healthy" => "Healthy Volunteers",
                 "Hemodialysis" => "Hemodialysis Patients",
                 "Pediatric" => "Pediatric",
                 var p => p,
             }),

            // R1.1: "Patients with {Condition}" — "Patients with Renal Impairment",
            // "Patients With Liver Disease", "Patients With Cardiac Failure".
            // Canonicalizes to the condition-only form.
            (new Regex(
                @"^\s*Patients?\s+[Ww]ith\s+(?<cond>Renal|Hepatic|Cardiac|Liver|Kidney)\s+(?<state>Impairment|Disease|Failure|Dysfunction)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
             m => $"{titleCase(m.Groups["cond"].Value)} {titleCase(m.Groups["state"].Value)}"),
        };

        // Known all-caps acronyms preserved as-is by titleCase. Uppercase lookup
        // so any case variant of the input still triggers preservation.
        private static readonly HashSet<string> _preservedAcronyms = new(StringComparer.OrdinalIgnoreCase)
        {
            "ESRD"
        };

        /**************************************************************/
        /// <summary>
        /// Title-cases a single word (first letter uppercase, rest lowercase),
        /// preserving known all-caps acronyms (<c>ESRD</c>). Handles hyphenated
        /// forms like <c>end-stage</c> → <c>End-Stage</c>.
        /// </summary>
        private static string titleCase(string word)
        {
            #region implementation

            if (string.IsNullOrEmpty(word))
                return word;

            if (_preservedAcronyms.Contains(word))
                return word.ToUpperInvariant();

            var parts = word.Split('-');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                    continue;
                if (_preservedAcronyms.Contains(parts[i]))
                {
                    parts[i] = parts[i].ToUpperInvariant();
                    continue;
                }
                parts[i] = char.ToUpperInvariant(parts[i][0]) +
                           parts[i].Substring(1).ToLowerInvariant();
            }
            return string.Join("-", parts);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes the ordinal form for trimester matches. Returns <c>First</c>,
        /// <c>Second</c>, or <c>Third</c> regardless of whether the input used
        /// numeric (<c>1st</c>) or word form.
        /// </summary>
        private static string normalizeOrdinal(string ord)
        {
            #region implementation

            return ord.ToLowerInvariant() switch
            {
                "1st" or "first" or "1" => "First",
                "2nd" or "second" or "2" => "Second",
                "3rd" or "third" or "3" => "Third",
                _ => titleCase(ord),
            };

            #endregion
        }

        #endregion Regex Population Patterns

        #region Label Dictionary

        // Case-insensitive mapping from row-label text to canonical Population value.
        // Extends _populationKeywords with CYP metabolizer phenotypes and their
        // "Intermediate Metabolizer"-style long forms.
        private static readonly Dictionary<string, string> _labelToCanonical = new(StringComparer.OrdinalIgnoreCase)
        {
            // Standard populations (match the existing _populationKeywords + common surface forms)
            ["Pediatric"] = "Pediatric",
            ["Pediatric Patients"] = "Pediatric",
            ["Geriatric"] = "Geriatric",
            ["Elderly"] = "Elderly",
            ["Neonatal"] = "Neonatal",
            ["Neonates"] = "Neonatal",
            ["Postmenopausal"] = "Postmenopausal Women",
            ["Postmenopausal Women"] = "Postmenopausal Women",
            ["Premenopausal"] = "Premenopausal Women",
            ["Pregnant"] = "Pregnant Women",
            ["Pregnant Women"] = "Pregnant Women",
            ["Premature Infants"] = "Premature Infants",
            ["Preterm Infants"] = "Premature Infants",
            ["Renal Impairment"] = "Renal Impairment",
            ["Hepatic Impairment"] = "Hepatic Impairment",
            ["Healthy Volunteers"] = "Healthy Volunteers",
            ["Healthy Subjects"] = "Healthy Volunteers",
            ["Adult Healthy Volunteers"] = "Adult Healthy Volunteers",
            ["Adult"] = "Adult",
            ["Adults"] = "Adult",

            // CYP metabolizer phenotypes — bare forms as they appear in row labels
            ["Poor"] = "Poor Metabolizer",
            ["Intermediate"] = "Intermediate Metabolizer",
            ["Normal"] = "Normal Metabolizer",
            ["Ultrarapid"] = "Ultrarapid Metabolizer",
            ["Ultra-rapid"] = "Ultrarapid Metabolizer",
            ["Extensive"] = "Extensive Metabolizer",
            ["Rapid"] = "Rapid Metabolizer",

            // CYP metabolizer phenotypes — explicit long forms
            ["Poor Metabolizer"] = "Poor Metabolizer",
            ["Poor Metabolizers"] = "Poor Metabolizer",
            ["Intermediate Metabolizer"] = "Intermediate Metabolizer",
            ["Intermediate Metabolizers"] = "Intermediate Metabolizer",
            ["Normal Metabolizer"] = "Normal Metabolizer",
            ["Normal Metabolizers"] = "Normal Metabolizer",
            ["Ultrarapid Metabolizer"] = "Ultrarapid Metabolizer",
            ["Ultrarapid Metabolizers"] = "Ultrarapid Metabolizer",
            ["Extensive Metabolizer"] = "Extensive Metabolizer",
            ["Extensive Metabolizers"] = "Extensive Metabolizer",
            ["Rapid Metabolizer"] = "Rapid Metabolizer",

            // R1.1 additions — sex, age, dialysis, and multi-word stratifiers
            // that appeared in the 2026-04-21 post-R1 validation audit as
            // TreatmentArm false positives (e.g., Norfloxacin / TID 2069).
            // These surface as bare single-word col 0 labels in compound
            // layouts and were falling through to the drug-name heuristic.
            //
            // Sex strata
            ["Male"] = "Male",
            ["Males"] = "Male",
            ["Female"] = "Female",
            ["Females"] = "Female",
            // Bare age strata (age-range and "Elderly" already covered above;
            // "Young" / "Young Adults" are the common bare forms)
            ["Young"] = "Young Adults",
            ["Young Adults"] = "Young Adults",
            ["Elderly Subjects"] = "Elderly",
            ["Elderly Patients"] = "Elderly",
            ["Children"] = "Pediatric",
            ["Infants"] = "Infants",
            ["Adolescents"] = "Adolescents",
            // Dialysis / end-stage renal forms — the word "Hemodialysis" alone
            // appears as a row label in renal PK tables. The detailed renal
            // bands ("CLCR X to Y mL/min") continue to route via the regex
            // second pass.
            ["Hemodialysis"] = "Hemodialysis Patients",
            ["Hemodialysis Patients"] = "Hemodialysis Patients",
            ["CAPD"] = "CAPD Patients",
            ["CAPD Patients"] = "CAPD Patients",
            ["Peritoneal Dialysis"] = "Peritoneal Dialysis Patients",
            // Subject-group compound forms with optional parenthesized trailer
            // (the parens are stripped before lookup by the regex second pass)
            ["Pediatric Subjects"] = "Pediatric",
            ["Adult Subjects"] = "Adult",
            ["Healthy"] = "Healthy Volunteers",
            // HIV-specific populations appearing in antiretroviral labels
            ["HIV-1-Infected Pediatric Subjects"] = "HIV-1-Infected Pediatric Subjects",
            ["HIV-Infected Pediatric Subjects"] = "HIV-1-Infected Pediatric Subjects",
            ["HIV-1-Infected Adults"] = "HIV-1-Infected Adults",
        };

        // Whitespace collapser used by TryMatchLabel to normalize lookup keys
        private static readonly Regex _whitespaceCollapse = new(@"\s+", RegexOptions.Compiled);

        #endregion Label Dictionary

        #region Internal Methods

        /**************************************************************/
        /// <summary>
        /// Extracts population from table caption text using regex patterns
        /// and keyword dictionary.
        /// </summary>
        internal static string? extractFromCaption(string? caption)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(caption))
                return null;

            // Try regex pattern match first
            var match = _captionPopulationPattern.Match(caption);
            if (match.Success)
            {
                var pop = match.Groups["pop"].Value.Trim();
                if (!string.IsNullOrEmpty(pop))
                    return normalizePopulation(pop);
            }

            // Fall back to keyword dictionary
            var lowerCaption = caption.ToLowerInvariant();
            foreach (var kvp in _populationKeywords)
            {
                if (lowerCaption.Contains(kvp.Key))
                    return kvp.Value;
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts population from section title using structural patterns
        /// like "Pharmacokinetics in {Population}".
        /// </summary>
        internal static string? extractFromSectionTitle(string? title)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(title))
                return null;

            // Try structural pattern match
            var match = _sectionPopulationPattern.Match(title);
            if (match.Success)
            {
                var pop = match.Groups["pop"].Value.Trim();
                if (!string.IsNullOrEmpty(pop))
                    return normalizePopulation(pop);
            }

            // Fall back to keyword dictionary
            var lowerTitle = title.ToLowerInvariant();
            foreach (var kvp in _populationKeywords)
            {
                if (lowerTitle.Contains(kvp.Key))
                    return kvp.Value;
            }

            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Computes normalized Levenshtein distance ratio.
        /// Returns 1.0 - (editDistance / max(len(a), len(b))).
        /// </summary>
        internal static double computeLevenshteinRatio(string a, string b)
        {
            #region implementation

            if (a == b)
                return 1.0;

            var maxLen = Math.Max(a.Length, b.Length);
            if (maxLen == 0)
                return 1.0;

            var distance = computeLevenshteinDistance(a, b);
            return 1.0 - ((double)distance / maxLen);

            #endregion
        }

        #endregion Internal Methods

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Computes Levenshtein edit distance between two strings using
        /// Wagner-Fischer dynamic programming algorithm.
        /// </summary>
        private static int computeLevenshteinDistance(string a, string b)
        {
            #region implementation

            var m = a.Length;
            var n = b.Length;

            // Use single-row optimization for memory efficiency
            var prev = new int[n + 1];
            var curr = new int[n + 1];

            for (int j = 0; j <= n; j++)
                prev[j] = j;

            for (int i = 1; i <= m; i++)
            {
                curr[0] = i;
                for (int j = 1; j <= n; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(
                        Math.Min(curr[j - 1] + 1, prev[j] + 1),
                        prev[j - 1] + cost);
                }

                // Swap rows
                (prev, curr) = (curr, prev);
            }

            return prev[n];

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes extracted population text: title-cases, trims,
        /// removes trailing punctuation.
        /// </summary>
        private static string normalizePopulation(string raw)
        {
            #region implementation

            var trimmed = raw.Trim().TrimEnd('.', ',', ';', ':');

            // Check keyword dictionary for canonical form
            var lower = trimmed.ToLowerInvariant();
            foreach (var kvp in _populationKeywords)
            {
                if (lower.Contains(kvp.Key))
                    return kvp.Value;
            }

            // Return trimmed form with first letter uppercase
            if (trimmed.Length > 0)
                return char.ToUpper(trimmed[0]) + trimmed[1..];

            return trimmed;

            #endregion
        }

        #endregion Private Helpers
    }
}
