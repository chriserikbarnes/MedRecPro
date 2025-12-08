namespace MedRecPro.Models
{
    /**************************************************************/
    /// <summary>
    /// Provides flexible search term parsing for FDA application numbers.
    /// Handles various input formats including prefix-only, number-only, 
    /// and combined prefix/number patterns.
    /// </summary>
    /// <remarks>
    /// Supports FDA application number formats:
    /// <list type="bullet">
    ///   <item><description>NDA - New Drug Application</description></item>
    ///   <item><description>ANDA - Abbreviated New Drug Application</description></item>
    ///   <item><description>BLA - Biologics License Application</description></item>
    /// </list>
    /// Input is normalized to uppercase with whitespace removed for consistent matching.
    /// </remarks>
    /// <example>
    /// <code>
    /// var terms = ApplicationNumberSearch.Parse("ANDA 125669");
    /// // terms.Normalized = "ANDA125669"
    /// // terms.NumericOnly = "125669"
    /// // terms.AlphaOnly = "ANDA"
    /// // terms.IsNumericOnly = false
    /// // terms.IsPrefixOnly = false
    /// </code>
    /// </example>
    /// <seealso cref="LabelView.ProductsByApplicationNumber"/>
    public static class ApplicationNumberSearch
    {
        #region nested types

        /**************************************************************/
        /// <summary>
        /// Contains parsed search components extracted from user input.
        /// </summary>
        /// <param name="Normalized">Input with whitespace removed and converted to uppercase.</param>
        /// <param name="NumericOnly">Only the numeric characters from input.</param>
        /// <param name="AlphaOnly">Only the alphabetic characters from input.</param>
        /// <param name="IsNumericOnly">True if input contains only numbers (e.g., "125669").</param>
        /// <param name="IsPrefixOnly">True if input contains only letters (e.g., "ANDA").</param>
        /// <example>
        /// <code>
        /// // Prefix only search
        /// var prefixTerms = ApplicationNumberSearch.Parse("ANDA");
        /// // prefixTerms.IsPrefixOnly = true
        /// 
        /// // Number only search
        /// var numTerms = ApplicationNumberSearch.Parse("125669");
        /// // numTerms.IsNumericOnly = true
        /// </code>
        /// </example>
        public record SearchTerms(
            string Normalized,
            string NumericOnly,
            string AlphaOnly,
            bool IsNumericOnly,
            bool IsPrefixOnly);

        #endregion

        #region public methods

        /**************************************************************/
        /// <summary>
        /// Parses user input into search components for flexible application number matching.
        /// </summary>
        /// <param name="input">
        /// The user-provided search string. Accepts formats such as:
        /// "ANDA125669", "ANDA 125669", "ANDA", "125669", or partial values.
        /// </param>
        /// <returns>
        /// A <see cref="SearchTerms"/> record containing normalized and decomposed search components.
        /// </returns>
        /// <remarks>
        /// The parsing logic:
        /// <list type="number">
        ///   <item><description>Trims whitespace and converts to uppercase</description></item>
        ///   <item><description>Creates a normalized version with internal spaces removed</description></item>
        ///   <item><description>Extracts numeric and alphabetic portions separately</description></item>
        ///   <item><description>Determines search mode (prefix-only, number-only, or combined)</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Combined prefix and number
        /// var terms = ApplicationNumberSearch.Parse("ANDA 125669");
        /// var query = db.Products.Where(p => 
        ///     p.ApplicationNumber.Contains(terms.Normalized));
        /// 
        /// // Prefix only - find all ANDAs
        /// var prefixTerms = ApplicationNumberSearch.Parse("ANDA");
        /// var andaQuery = db.Products.Where(p => 
        ///     p.ApplicationNumber.StartsWith(prefixTerms.AlphaOnly));
        /// </code>
        /// </example>
        /// <seealso cref="SearchTerms"/>
        public static SearchTerms Parse(string? input)
        {
            #region implementation

            // Normalize input: trim and convert to uppercase for case-insensitive matching
            var trimmed = input?.Trim().ToUpperInvariant() ?? "";

            // Remove internal whitespace for consistent comparison (e.g., "ANDA 125669" -> "ANDA125669")
            var normalized = trimmed.Replace(" ", "");

            // Extract only numeric characters for number-based searches
            var numericOnly = new string(trimmed.Where(char.IsDigit).ToArray());

            // Extract only alphabetic characters for prefix-based searches
            var alphaOnly = new string(trimmed.Where(char.IsLetter).ToArray());

            // Determine search mode based on input composition
            var isNumericOnly = alphaOnly.Length == 0 && numericOnly.Length > 0;
            var isPrefixOnly = numericOnly.Length == 0 && alphaOnly.Length > 0;

            return new SearchTerms(
                Normalized: normalized,
                NumericOnly: numericOnly,
                AlphaOnly: alphaOnly,
                IsNumericOnly: isNumericOnly,
                IsPrefixOnly: isPrefixOnly);

            #endregion
        }

        #endregion
    }
}
