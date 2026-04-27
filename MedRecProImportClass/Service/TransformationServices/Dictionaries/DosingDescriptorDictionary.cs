using System.Text.RegularExpressions;

namespace MedRecProImportClass.Service.TransformationServices.Dictionaries
{
    /**************************************************************/
    /// <summary>
    /// Single source of truth for dose-descriptor row labels and shape-keyword
    /// phrases that signal a real Dosing table — used by the table router to
    /// confirm DOSING categorization from table content, and by the dosing
    /// shape classifier to identify dose-reduction / titration / preparation
    /// layouts.
    /// </summary>
    /// <remarks>
    /// ## Responsibilities
    /// - Provide <see cref="ContainsDosingDescriptor"/> — substring match used by
    ///   <c>TableParserRouter.validateDosingOrDowngrade</c> to verify that a
    ///   34068-7-coded table actually carries dosing semantics (e.g. "Starting
    ///   Dose", "Renal Adjustment", "First dose reduction") rather than prose.
    /// - Provide <see cref="IsDoseReductionLabel"/> — anchored match used by the
    ///   dose-reduction shape profile to recognize ParameterName candidates.
    ///
    /// ## Design
    /// Keep entries narrow. Only phrases whose presence reliably indicates a
    /// Dosing table belong here — a generic word like "dose" alone is too noisy
    /// (it appears in adverse-event captions, study descriptions, etc.).
    ///
    /// ## Wave 1 contents
    /// Drawn from the column-contracts.md DOSING examples plus the named
    /// fixtures in the standardization-report (TextTableID 40876, 19220, 21539):
    /// - Titration / starting / maintenance / loading dose phrases
    /// - Dose-reduction levels (Recommended starting / First / Second / Third)
    /// - Renal / hepatic / weight-based / metabolizer adjustment phrases
    /// - Recommended Dosage / Dosage Modifications headers
    /// </remarks>
    /// <seealso cref="PkParameterDictionary"/>
    public static class DosingDescriptorDictionary
    {
        #region Descriptor Phrases

        /**************************************************************/
        /// <summary>
        /// Case-insensitive descriptor phrases. Matched as substrings against
        /// header / row-label text. Keep narrow — additions should be supported
        /// by a real fixture, not speculation.
        /// </summary>
        private static readonly string[] _descriptors = new[]
        {
            // Core dose descriptors (column-contracts.md DOSING examples)
            "Starting Dose",
            "Initial Dose",
            "Loading Dose",
            "Maintenance Dose",
            "Recommended Dose",
            "Recommended Dosage",
            "Target Dose",
            "Target Dosage",
            "Maximum Dose",
            "Minimum Dose",
            "Usual Dose",
            "Single Dose",

            // Titration
            "Titration Step",
            "Titration Schedule",
            "Dose Titration",

            // Dose modification / reduction (TextTableID 40876)
            "Dose Reduction",
            "Dose Reductions",
            "Dose Modification",
            "Dose Modifications",
            "Dosage Modifications",
            "Dose Level",
            "Dose Levels",
            "Recommended starting dose",
            "First dose reduction",
            "Second dose reduction",
            "Third dose reduction",

            // Adjustment by population / context
            "Renal Adjustment",
            "Renal Dose Adjustment",
            "Hepatic Adjustment",
            "Hepatic Dose Adjustment",
            "Weight-Based Dose",
            "Weight Based Dose",
            "Weight-Based Dosing",

            // Frequency / schedule descriptors that occur as row labels
            "Once Daily",
            "Twice Daily",
            "Three Times Daily",
            "Four Times Daily",
        };

        /**************************************************************/
        /// <summary>
        /// Compiled disjunction of all descriptor phrases for a single regex
        /// scan. Word boundaries on the leading edge prevent matches inside
        /// longer words (e.g. "doses" should not be treated as a dose-level
        /// descriptor).
        /// </summary>
        private static readonly Regex _containsAnyDescriptorPattern = buildContainsPattern();

        #endregion Descriptor Phrases

        #region Anchored Dose-Reduction Pattern

        /**************************************************************/
        /// <summary>
        /// Matches the column 0 row labels of a dose-reduction table:
        /// "Recommended starting dose", "First dose reduction", "Second dose
        /// reduction", "Third dose reduction" (case-insensitive).
        /// Anchored so generic prose containing the same words does not match.
        /// </summary>
        private static readonly Regex _doseReductionLabelPattern = new(
            @"^\s*(?:Recommended\s+starting\s+dose|(?:First|Second|Third|Fourth|1st|2nd|3rd|4th)\s+dose\s+reduction)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        #endregion Anchored Dose-Reduction Pattern

        #region Public Methods

        /**************************************************************/
        /// <summary>
        /// True when <paramref name="raw"/> contains any known dosing descriptor
        /// phrase. Used by the router to confirm DOSING categorization for a
        /// 34068-7-coded table whose content has not yet been validated.
        /// </summary>
        /// <param name="raw">Candidate text from a column header or row label.</param>
        /// <returns>True when the text carries a dosing descriptor signal.</returns>
        public static bool ContainsDosingDescriptor(string? raw)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            return _containsAnyDescriptorPattern.IsMatch(raw);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// True when <paramref name="raw"/> is a dose-reduction label such as
        /// "Recommended starting dose" or "First dose reduction". Anchored
        /// match — only fires on whole-cell labels, not on prose containing
        /// the same words.
        /// </summary>
        /// <param name="raw">Candidate column 0 row label.</param>
        /// <returns>True when the cell is a dose-reduction label.</returns>
        public static bool IsDoseReductionLabel(string? raw)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            return _doseReductionLabelPattern.IsMatch(raw);

            #endregion
        }

        #endregion Public Methods

        #region Private Helpers

        /**************************************************************/
        /// <summary>
        /// Builds the combined contains-any regex. Each descriptor is escaped
        /// for regex safety, whitespace inside multi-word phrases is loosened
        /// to <c>\s+</c> so cell text with extra spaces still matches, and a
        /// leading <c>\b</c> prevents partial-word matches.
        /// </summary>
        private static Regex buildContainsPattern()
        {
            #region implementation

            var alternations = _descriptors
                .Select(d => Regex.Escape(d).Replace(@"\ ", @"\s+"))
                .ToArray();

            var combined = @"\b(?:" + string.Join("|", alternations) + ")";

            return new Regex(
                combined,
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            #endregion
        }

        #endregion Private Helpers
    }
}
