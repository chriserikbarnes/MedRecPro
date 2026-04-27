namespace MedRecProImportClass.Service.TransformationServices.Dictionaries
{
    /**************************************************************/
    /// <summary>
    /// Normalizes <see cref="MedRecProImportClass.Models.ParsedObservation.TableCategory"/> strings between the
    /// underscore-uppercase form (<c>ADVERSE_EVENT</c>) used by the parser pipeline and the
    /// documentation form (<c>AdverseEvent</c>) used by <see cref="ColumnContractRegistry"/>.
    /// </summary>
    /// <remarks>
    /// Lifted from the private <c>ColumnContractRegistry.normalize</c> helper so that
    /// <see cref="CategoryProfileRegistry"/> and any future consumer can resolve aliases
    /// from a single source of truth.
    /// </remarks>
    /// <seealso cref="ColumnContractRegistry"/>
    /// <seealso cref="CategoryProfileRegistry"/>
    public static class CategoryNameNormalizer
    {
        #region Alias Maps

        /**************************************************************/
        /// <summary>
        /// Underscore-uppercase → documentation form. Categories without an entry
        /// (PK, EFFICACY, DOSING, BMD) use the same string in both forms.
        /// </summary>
        private static readonly Dictionary<string, string> _toDocForm =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["ADVERSE_EVENT"] = "AdverseEvent",
            ["DRUG_INTERACTION"] = "DrugInteraction",
            ["TISSUE_DISTRIBUTION"] = "TissueDistribution",
            ["TEXT_DESCRIPTIVE"] = "TextDescriptive",
            ["EFFICACY"] = "Efficacy",
            ["DOSING"] = "Dosing",
        };

        /**************************************************************/
        /// <summary>
        /// Documentation form → underscore-uppercase. Inverse of <see cref="_toDocForm"/>
        /// for callers that need the parser-pipeline form.
        /// </summary>
        private static readonly Dictionary<string, string> _toUnderscoreForm =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["AdverseEvent"] = "ADVERSE_EVENT",
            ["DrugInteraction"] = "DRUG_INTERACTION",
            ["TissueDistribution"] = "TISSUE_DISTRIBUTION",
            ["TextDescriptive"] = "TEXT_DESCRIPTIVE",
            ["Efficacy"] = "EFFICACY",
            ["Dosing"] = "DOSING",
        };

        #endregion

        /**************************************************************/
        /// <summary>
        /// Normalizes a TableCategory string to the documentation form
        /// (<c>AdverseEvent</c>, <c>PK</c>, <c>BMD</c>, etc.). Trims whitespace; matches
        /// case-insensitively. Unknown categories are returned trimmed but otherwise unchanged.
        /// </summary>
        /// <param name="raw">Input category string. May be null, empty, or either form.</param>
        /// <returns>Documentation form, or empty string when <paramref name="raw"/> is null/whitespace.</returns>
        /// <example>
        /// <code>
        /// CategoryNameNormalizer.Normalize("ADVERSE_EVENT") // → "AdverseEvent"
        /// CategoryNameNormalizer.Normalize("AdverseEvent")  // → "AdverseEvent"
        /// CategoryNameNormalizer.Normalize("PK")            // → "PK"
        /// CategoryNameNormalizer.Normalize("Foo")           // → "Foo"
        /// </code>
        /// </example>
        public static string Normalize(string? raw)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var trimmed = raw.Trim();
            return _toDocForm.TryGetValue(trimmed, out var doc) ? doc : trimmed;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Inverse of <see cref="Normalize"/>: maps documentation form to the underscore-uppercase
        /// form used by <see cref="MedRecProImportClass.Models.ParsedObservation.TableCategory"/>. Trims whitespace.
        /// </summary>
        /// <param name="raw">Input category string. May be null, empty, or either form.</param>
        /// <returns>Underscore-uppercase form, or empty string when <paramref name="raw"/> is null/whitespace.</returns>
        /// <example>
        /// <code>
        /// CategoryNameNormalizer.ToUnderscoreForm("AdverseEvent") // → "ADVERSE_EVENT"
        /// CategoryNameNormalizer.ToUnderscoreForm("PK")           // → "PK"
        /// </code>
        /// </example>
        public static string ToUnderscoreForm(string? raw)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var trimmed = raw.Trim();
            return _toUnderscoreForm.TryGetValue(trimmed, out var under) ? under : trimmed;

            #endregion
        }
    }
}
