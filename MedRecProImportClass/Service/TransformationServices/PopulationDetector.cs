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
            #region implementation

            canonical = string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var key = _whitespaceCollapse.Replace(raw.Trim(), " ");

            if (_labelToCanonical.TryGetValue(key, out var hit))
            {
                canonical = hit;
                return true;
            }

            return false;

            #endregion
        }

        #endregion Public Methods

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
