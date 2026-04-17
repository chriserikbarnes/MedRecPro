namespace MedRecProConsole.Services.Reporting
{
    /**************************************************************/
    /// <summary>
    /// GitHub-flavored markdown escaping helpers for table-cell content.
    /// </summary>
    /// <remarks>
    /// GFM tables are pipe-delimited, so literal <c>|</c> characters inside a cell must be
    /// escaped or they collapse the column. Newlines split rows, so they are converted to
    /// <c>&lt;br&gt;</c> HTML line breaks which GitHub and VS Code render inline.
    /// </remarks>
    public static class GfmEscape
    {
        #region public methods

        /**************************************************************/
        /// <summary>
        /// Escapes a string for safe inclusion inside a single GFM table cell.
        /// Replaces backslashes, pipes, and newlines; trims leading/trailing whitespace.
        /// </summary>
        /// <param name="text">Raw cell content. Null/empty returns a dash.</param>
        /// <returns>Escaped text suitable for a pipe-delimited GFM cell.</returns>
        public static string Inline(string? text)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(text))
                return "-";

            // Order matters: escape backslash first so subsequent escapes don't double-escape it.
            var escaped = text
                .Replace("\\", "\\\\")
                .Replace("|", "\\|");

            return MultilineToBr(escaped).Trim();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts embedded newlines (CR, LF, CRLF) to HTML <c>&lt;br&gt;</c> tags so multi-line
        /// cell text renders inside a single GFM table row.
        /// </summary>
        /// <param name="text">Text potentially containing newlines.</param>
        /// <returns>Single-line text with <c>&lt;br&gt;</c> separators.</returns>
        public static string MultilineToBr(string? text)
        {
            #region implementation

            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("\r\n", "<br>")
                .Replace("\r", "<br>")
                .Replace("\n", "<br>");

            #endregion
        }

        #endregion
    }
}
