using System;

namespace MedRecProImportClass.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Parses SPL marketing-category application numbers (e.g., "ANDA202230",
    /// "NDA020610", or numeric-only "020610") into their <c>ApplType</c>
    /// (<c>"N"</c> / <c>"A"</c>) and <c>ApplNo</c> components, matching the
    /// composite natural key used by <see cref="MedRecProImportClass.Models.OrangeBook.Product"/>.
    /// </summary>
    /// <remarks>
    /// ## Purpose
    /// Orange Book keys products on the composite <c>(ApplType, ApplNo)</c>, but SPL
    /// <c>MarketingCategory.ApplicationOrMonographIDValue</c> stores the concatenation
    /// as a single string that may be prefixed (<c>"NDA020610"</c>), un-prefixed
    /// (<c>"020610"</c>), lower-cased, or padded with whitespace. This helper
    /// centralizes the prefix-strip + normalize logic so the Orange Book matcher
    /// (<see cref="MedRecProImportClass.Service.ParsingServices.OrangeBookProductParsingService"/>)
    /// and the bioequivalent-label dedup service share one implementation.
    ///
    /// ## Parse Rules
    /// 1. Trim surrounding whitespace and upper-case the input.
    /// 2. If the value starts with <c>"NDA"</c>, emit <c>ApplType = "N"</c> and the
    ///    remainder as <c>ApplNo</c>.
    /// 3. If the value starts with <c>"ANDA"</c>, emit <c>ApplType = "A"</c> and the
    ///    remainder as <c>ApplNo</c>.
    /// 4. Otherwise the input is not classifiable — <c>TryParse</c> returns false and
    ///    the out parameters are empty strings.
    ///
    /// Other SPL application types (for example BLAs, OTC monographs, unapproved-drug
    /// designations) intentionally fail the parse: the downstream dedup logic treats
    /// those as unclassifiable and falls back to per-options handling.
    /// </remarks>
    /// <seealso cref="MedRecProImportClass.Models.OrangeBook.Product"/>
    /// <seealso cref="MedRecProImportClass.Service.ParsingServices.OrangeBookProductParsingService"/>
    public static class ApplicationNumberParser
    {
        #region constants

        private const string NdaPrefix = "NDA";
        private const string AndaPrefix = "ANDA";

        /// <summary>Orange Book <c>ApplType</c> value for NDAs.</summary>
        public const string ApplTypeNda = "N";

        /// <summary>Orange Book <c>ApplType</c> value for ANDAs.</summary>
        public const string ApplTypeAnda = "A";

        #endregion

        /**************************************************************/
        /// <summary>
        /// Attempts to parse an SPL application-number string into Orange Book
        /// composite-key components (<paramref name="applType"/> + <paramref name="applNo"/>).
        /// </summary>
        /// <param name="applicationNumber">The raw SPL application number (nullable,
        /// may include <c>NDA</c>/<c>ANDA</c> prefix, may be lower-cased, may have
        /// leading/trailing whitespace).</param>
        /// <param name="applType">On success, <c>"N"</c> for NDA or <c>"A"</c> for ANDA.
        /// On failure, the empty string.</param>
        /// <param name="applNo">On success, the numeric portion (e.g., <c>"202230"</c>)
        /// upper-cased and trimmed. On failure, the empty string.</param>
        /// <returns>True when the input carried a recognizable <c>NDA</c> or <c>ANDA</c>
        /// prefix and at least one character of numeric remainder; false otherwise.</returns>
        /// <example>
        /// <code>
        /// ApplicationNumberParser.TryParse("ANDA202230", out var t, out var n);
        /// // t == "A", n == "202230"
        ///
        /// ApplicationNumberParser.TryParse("  nda020610 ", out var t, out var n);
        /// // t == "N", n == "020610"
        ///
        /// ApplicationNumberParser.TryParse("OTC-monograph", out _, out _);
        /// // returns false
        /// </code>
        /// </example>
        /// <seealso cref="ApplTypeNda"/>
        /// <seealso cref="ApplTypeAnda"/>
        public static bool TryParse(string? applicationNumber, out string applType, out string applNo)
        {
            #region implementation

            applType = string.Empty;
            applNo = string.Empty;

            if (string.IsNullOrWhiteSpace(applicationNumber))
            {
                return false;
            }

            // Trim + upper-case first so prefix detection is case/whitespace insensitive.
            var normalized = applicationNumber.Trim().ToUpperInvariant();

            // Order matters: ANDA must be tested before NDA because "ANDA..." also
            // starts with "NDA" after the leading "A" if we stripped it naively.
            // StartsWith on the full 4-char prefix avoids that pitfall.
            if (normalized.StartsWith(AndaPrefix, StringComparison.Ordinal))
            {
                var remainder = normalized.Substring(AndaPrefix.Length).Trim();
                if (remainder.Length == 0)
                {
                    return false;
                }
                applType = ApplTypeAnda;
                applNo = remainder;
                return true;
            }

            if (normalized.StartsWith(NdaPrefix, StringComparison.Ordinal))
            {
                var remainder = normalized.Substring(NdaPrefix.Length).Trim();
                if (remainder.Length == 0)
                {
                    return false;
                }
                applType = ApplTypeNda;
                applNo = remainder;
                return true;
            }

            // No recognized prefix — caller treats as unclassifiable.
            return false;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Strips an <c>NDA</c> or <c>ANDA</c> prefix from an application number and
        /// returns only the numeric portion (upper-cased, trimmed). When no prefix is
        /// present the input is returned upper-cased and trimmed.
        /// </summary>
        /// <remarks>
        /// Preserves the legacy behavior of
        /// <see cref="MedRecProImportClass.Service.ParsingServices.OrangeBookProductParsingService"/>
        /// fallback matcher (Tier 2), which accepts un-prefixed numeric app numbers as valid.
        /// Use <see cref="TryParse"/> when you need to distinguish NDA from ANDA.
        /// </remarks>
        /// <param name="applicationNumber">Raw application number.</param>
        /// <returns>Numeric portion (e.g., <c>"202230"</c>), or empty when input is null/blank.</returns>
        /// <seealso cref="TryParse"/>
        public static string ExtractNumeric(string? applicationNumber)
        {
            #region implementation

            if (string.IsNullOrWhiteSpace(applicationNumber))
            {
                return string.Empty;
            }

            var normalized = applicationNumber.Trim().ToUpperInvariant();

            // Test 4-char prefix first so ANDA doesn't fall into the NDA branch.
            if (normalized.StartsWith(AndaPrefix, StringComparison.Ordinal))
            {
                return normalized.Substring(AndaPrefix.Length).Trim();
            }
            if (normalized.StartsWith(NdaPrefix, StringComparison.Ordinal))
            {
                return normalized.Substring(NdaPrefix.Length).Trim();
            }
            return normalized;

            #endregion
        }
    }
}
