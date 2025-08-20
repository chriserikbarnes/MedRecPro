using MedRecPro.Models;
using Microsoft.AspNetCore.Html;
using System.Security;

namespace MedRecPro.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Provides static helper methods for use in RazorLight templates to generate SPL XML.
    /// </summary>
    /// <seealso cref="Label"/>
    public static class SplTemplateHelpers
    {
        #region implementation
        /**************************************************************/
        /// <summary>
        /// Conditionally renders an XML attribute if its value is not null or empty.
        /// Returns an HtmlString to ensure it's not double-encoded by Razor.
        /// </summary>
        /// <param name="name">The name of the attribute.</param>
        /// <param name="value">The value of the attribute.</param>
        /// <returns>An HtmlString containing the formatted XML attribute or an empty string.</returns>
        /// <seealso cref="Label"/>
        public static HtmlString Attribute(string name, object? value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return HtmlString.Empty;
            }

            // SecurityElement.Escape handles XML-invalid characters like '&', '<', '>'
            return new HtmlString($" {name}=\"{SecurityElement.Escape(value.ToString())}\"");
        }

        /**************************************************************/
        /// <summary>
        /// Safely gets a value from a dictionary with case-insensitive key lookup and returns it as a formatted XML attribute.
        /// First tries the exact key, then tries variations (camelCase, PascalCase).
        /// </summary>
        /// <param name="name">The name of the XML attribute.</param>
        /// <param name="dictionary">The dictionary to search</param>
        /// <param name="key">The key to find</param>
        /// <returns>An HtmlString containing the formatted XML attribute or an empty string if not found.</returns>
        public static HtmlString SafeAttribute(string name, IDictionary<string, object?> dictionary, string key)
        {
            if (dictionary == null || string.IsNullOrEmpty(key))
                return HtmlString.Empty;

            // Try exact match first
            if (dictionary.ContainsKey(key))
            {
                var value = dictionary[key];
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    // SecurityElement.Escape handles XML-invalid characters like '&', '<', '>'
                    return new HtmlString($" {name}=\"{SecurityElement.Escape(value.ToString())}\"");
                }
            }

            // Try PascalCase version
            var pascalKey = char.ToUpper(key[0]) + key.Substring(1);
            if (dictionary.ContainsKey(pascalKey))
            {
                var value = dictionary[pascalKey];
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    // SecurityElement.Escape handles XML-invalid characters like '&', '<', '>'
                    return new HtmlString($" {name}=\"{SecurityElement.Escape(value.ToString())}\"");
                }
            }

            // Try camelCase version  
            var camelKey = char.ToLower(key[0]) + key.Substring(1);
            if (dictionary.ContainsKey(camelKey))
            {
                var value = dictionary[camelKey];
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    // SecurityElement.Escape handles XML-invalid characters like '&', '<', '>'
                    return new HtmlString($" {name}=\"{SecurityElement.Escape(value.ToString())}\"");
                }
            }

            // Try case-insensitive search
            var foundKey = dictionary.Keys.FirstOrDefault(k =>
                string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

            if (foundKey != null)
            {
                var value = dictionary[foundKey];
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    // SecurityElement.Escape handles XML-invalid characters like '&', '<', '>'
                    return new HtmlString($" {name}=\"{SecurityElement.Escape(value.ToString())}\"");
                }
            }

            return HtmlString.Empty;
        }

        /**************************************************************/
        /// <summary>
        /// Safely gets a DateTime value from a dictionary and returns it as a formatted XML attribute with yyyyMMdd format.
        /// </summary>
        /// <param name="name">The name of the XML attribute.</param>
        /// <param name="dictionary">The dictionary to search</param>
        /// <param name="key">The key to find</param>
        /// <returns>An HtmlString containing the formatted XML attribute or an empty string if not found.</returns>
        public static HtmlString SafeAttributeDateTime(string name, IDictionary<string, object?> dictionary, string key)
        {
            var value = SafeGet(dictionary, key);

            // Handle regular DateTime
            if (value is DateTime dateTime)
            {
                return new HtmlString($" {name}=\"{SecurityElement.Escape(dateTime.ToString("yyyyMMdd"))}\"");
            }

            // Handle nullable DateTime using type checking
            if (value != null && value.GetType() == typeof(DateTime?))
            {
                var nullableDateTime = (DateTime?)value;
                if (nullableDateTime.HasValue)
                {
                    return new HtmlString($" {name}=\"{SecurityElement.Escape(nullableDateTime.Value.ToString("yyyyMMdd"))}\"");
                }
            }

            // Try to parse as DateTime if it's a string
            if (value is string stringValue && DateTime.TryParse(stringValue, out DateTime parsedDate))
            {
                return new HtmlString($" {name}=\"{SecurityElement.Escape(parsedDate.ToString("yyyyMMdd"))}\"");
            }

            return HtmlString.Empty;
        }

        /**************************************************************/
        /// <summary>
        /// Safely gets a value from a dictionary with case-insensitive key lookup.
        /// First tries the exact key, then tries variations (camelCase, PascalCase).
        /// </summary>
        /// <param name="dictionary">The dictionary to search</param>
        /// <param name="key">The key to find</param>
        /// <returns>The value if found, null otherwise</returns>
        public static object? SafeGet(IDictionary<string, object?> dictionary, string key)
        {
            if (dictionary == null || string.IsNullOrEmpty(key))
                return null;

            // Try exact match first
            if (dictionary.ContainsKey(key))
                return dictionary[key];

            // Try PascalCase version
            var pascalKey = char.ToUpper(key[0]) + key.Substring(1);
            if (dictionary.ContainsKey(pascalKey))
                return dictionary[pascalKey];

            // Try camelCase version  
            var camelKey = char.ToLower(key[0]) + key.Substring(1);
            if (dictionary.ContainsKey(camelKey))
                return dictionary[camelKey];

            // Try case-insensitive search
            var foundKey = dictionary.Keys.FirstOrDefault(k =>
                string.Equals(k, key, StringComparison.OrdinalIgnoreCase));

            if (foundKey != null)
                return dictionary[foundKey];

            return null;
        }

        /**************************************************************/
        /// <summary>
        /// Gets all available keys from a dictionary for debugging purposes
        /// </summary>
        /// <param name="dictionary">The dictionary to inspect</param>
        /// <returns>Comma-separated list of keys</returns>
        public static string GetAvailableKeys(IDictionary<string, object>? dictionary)
        {
            if (dictionary == null)
                return "null";

            return string.Join(", ", dictionary.Keys.OrderBy(k => k));
        }

        /**************************************************************/
        /// <summary>
        /// Formats DateTime? or string dates to SPL YYYYMMDD
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static string ToSplDate(object? dt)
        {
            if (dt == null) return "";
            try
            {
                if (dt is DateTime d) return d.ToString("yyyyMMdd");
                var s = dt.ToString();
                if (DateTime.TryParse(s, out var parsed)) return parsed.ToString("yyyyMMdd");
                // assume already in yyyymmdd or unknown
                return s ?? "";
            }
            catch { return ""; }
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes GUIDs to uppercase to match SPL examples
        /// </summary>
        /// <param name="g"></param>
        /// <returns></returns>
        public static string GuidUp(Guid? g) => g.HasValue ? g.Value.ToString("D").ToUpperInvariant() : "";

        /**************************************************************/
        /// <summary>
        /// Returns attr
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string AttrOrNull(string name, string? value) => value == null ? "" : $" {name}=\"{value}\"";
        #endregion
    }
}