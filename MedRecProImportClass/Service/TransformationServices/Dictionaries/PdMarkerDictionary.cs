namespace MedRecProImportClass.Service.TransformationServices.Dictionaries
{
    /**************************************************************/
    /// <summary>
    /// Known pharmacodynamic (PD) markers that sometimes appear in tables routed to
    /// the PK parser — most commonly platelet-function measures mixed into
    /// pharmacogenomic tables (e.g., a CYP2C19 metabolizer study reporting Cmax
    /// alongside IPA and VASP-PRI).
    /// </summary>
    /// <remarks>
    /// Rows whose ParameterName or ParameterSubtype matches this dictionary are
    /// flagged with <c>COL_STD:PK_NON_PK_MARKER_DETECTED</c> during
    /// <c>ColumnStandardizationService.applyPkCanonicalization</c>. The row is
    /// preserved — the flag exists so downstream review queries can decide whether
    /// to keep the row under PK, emit a separate PD category later, or exclude.
    /// </remarks>
    /// <seealso cref="PkParameterDictionary"/>
    public static class PdMarkerDictionary
    {
        /**************************************************************/
        /// <summary>
        /// Case-insensitive PD marker aliases. Keep this list narrow — only markers
        /// that have actually been observed mixing with PK tables in production.
        /// </summary>
        private static readonly HashSet<string> _markers = new(StringComparer.OrdinalIgnoreCase)
        {
            "IPA",                   // Inhibition of Platelet Aggregation
            "Platelet Aggregation",
            "Inhibition of Platelet Aggregation",
            "VASP-PRI",              // Vasodilator-Stimulated Phosphoprotein Platelet Reactivity Index
            "VASP PRI",
            "PRI",                   // Platelet Reactivity Index (stand-alone)
            "Platelet Reactivity Index",
            "Maximum Platelet Aggregation",
            "MPA",                   // Maximum Platelet Aggregation abbreviation
        };

        /**************************************************************/
        /// <summary>
        /// True when <paramref name="raw"/> matches a known PD marker. Trims
        /// whitespace and compares case-insensitively. Null/empty input returns false.
        /// </summary>
        /// <param name="raw">Candidate text from a ParameterName or ParameterSubtype cell.</param>
        /// <returns>True when the text is a known PD marker.</returns>
        public static bool IsPdMarker(string? raw)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            return _markers.Contains(raw.Trim());

            #endregion
        }
    }
}
