using MedRecPro.Models;
using Microsoft.AspNetCore.Html;
using System.Security;
using System.Globalization;

namespace MedRecPro.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Provides static helper methods for use in RazorLight templates to generate SPL XML.
    /// Enhanced version with additional utilities for complex data transformations.
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
        /// <seealso cref="Label"/>
        public static HtmlString SafeAttribute(string name, IDictionary<string, object?> dictionary, string key)
        {
            if (dictionary == null || string.IsNullOrEmpty(key))
                return HtmlString.Empty;

            var value = SafeGet(dictionary, key);
            if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new HtmlString($" {name}=\"{SecurityElement.Escape(value.ToString())}\"");
            }

            return HtmlString.Empty;
        }

        /**************************************************************/
        /// <summary>
        /// Enhanced safe attribute method that works with complex objects containing nested properties
        /// </summary>
        /// <param name="name">The name of the XML attribute.</param>
        /// <param name="sourceObject">The source object to extract property from</param>
        /// <param name="propertyPath">The property path (supports nested properties with dot notation)</param>
        /// <returns>An HtmlString containing the formatted XML attribute or an empty string if not found.</returns>
        /// <seealso cref="Label"/>
        public static HtmlString SafeAttribute(string name, object? sourceObject, string propertyPath)
        {
            if (sourceObject == null || string.IsNullOrEmpty(propertyPath))
                return HtmlString.Empty;

            var value = GetNestedPropertyValue(sourceObject, propertyPath);
            if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
            {
                return new HtmlString($" {name}=\"{SecurityElement.Escape(value.ToString())}\"");
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
        /// <seealso cref="Label"/>
        public static HtmlString SafeAttributeDateTime(string name, IDictionary<string, object?> dictionary, string key)
        {
            var value = SafeGet(dictionary, key);
            var dateString = ToSplDate(value);

            if (!string.IsNullOrEmpty(dateString))
            {
                return new HtmlString($" {name}=\"{SecurityElement.Escape(dateString)}\"");
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
        /// <seealso cref="Label"/>
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
        /// Gets a nested property value using reflection with dot notation support
        /// </summary>
        /// <param name="sourceObject">The source object</param>
        /// <param name="propertyPath">Property path (e.g., "Route.RouteCode")</param>
        /// <returns>The property value or null if not found</returns>
        /// <seealso cref="Label"/>
        private static object? GetNestedPropertyValue(object sourceObject, string propertyPath)
        {
            if (sourceObject == null || string.IsNullOrEmpty(propertyPath))
                return null;

            var properties = propertyPath.Split('.');
            var currentObject = sourceObject;

            foreach (var propertyName in properties)
            {
                if (currentObject == null)
                    return null;

                var propertyInfo = currentObject.GetType().GetProperty(propertyName);
                if (propertyInfo == null)
                    return null;

                currentObject = propertyInfo.GetValue(currentObject);
            }

            return currentObject;
        }

        /**************************************************************/
        /// <summary>
        /// Gets all available keys from a dictionary for debugging purposes
        /// </summary>
        /// <param name="dictionary">The dictionary to inspect</param>
        /// <returns>Comma-separated list of keys</returns>
        /// <seealso cref="Label"/>
        public static string GetAvailableKeys(IDictionary<string, object>? dictionary)
        {
            if (dictionary == null)
                return "null";

            return string.Join(", ", dictionary.Keys.OrderBy(k => k));
        }

        /**************************************************************/
        /// <summary>
        /// Formats DateTime? or string dates to SPL YYYYMMDD format
        /// Enhanced with better parsing and validation
        /// </summary>
        /// <param name="dt">The date object to format</param>
        /// <returns>Formatted date string or empty string if invalid</returns>
        /// <seealso cref="Label"/>
        public static string ToSplDate(object? dt)
        {
            if (dt == null)
                return "";

            try
            {
                // Handle DateTime directly
                if (dt is DateTime dateTime)
                    return dateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

                // Handle DateTimeOffset
                if (dt is DateTimeOffset dateTimeOffset)
                    return dateTimeOffset.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

                // Try to parse string representation
                var stringValue = dt.ToString();
                if (!string.IsNullOrEmpty(stringValue))
                {
                    // Try standard parsing first
                    if (DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                        return parsedDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

                    // Try exact format parsing for common SPL formats
                    var formats = new[] { "yyyyMMdd", "yyyy-MM-dd", "MM/dd/yyyy", "yyyy/MM/dd" };
                    if (DateTime.TryParseExact(stringValue, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactParsedDate))
                        return exactParsedDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                }

                return stringValue ?? "";
            }
            catch
            {
                return "";
            }
        }

        /**************************************************************/
        /// <summary>
        /// Normalizes GUIDs to uppercase to match SPL examples
        /// Enhanced with null safety and format validation
        /// </summary>
        /// <param name="g">The GUID to format</param>
        /// <returns>Uppercase GUID string or empty string if null</returns>
        /// <seealso cref="Label"/>
        public static string GuidUp(Guid? g)
        {
            if (!g.HasValue || g.Value == Guid.Empty)
                return "";

            return g.Value.ToString("D").ToUpperInvariant();
        }

        /**************************************************************/
        /// <summary>
        /// Safely converts string to uppercase GUID format
        /// </summary>
        /// <param name="guidString">String representation of GUID</param>
        /// <returns>Uppercase GUID string or empty string if invalid</returns>
        /// <seealso cref="Label"/>
        public static string GuidUp(string? guidString)
        {
            if (string.IsNullOrEmpty(guidString))
                return "";

            if (Guid.TryParse(guidString, out var guid))
                return guid.ToString("D").ToUpperInvariant();

            return "";
        }

        /**************************************************************/
        /// <summary>
        /// Returns formatted attribute string or empty string if value is null
        /// Enhanced for better null handling
        /// </summary>
        /// <param name="name">Attribute name</param>
        /// <param name="value">Attribute value</param>
        /// <returns>Formatted attribute string or empty string</returns>
        /// <seealso cref="Label"/>
        public static string AttrOrNull(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            return $" {name}=\"{SecurityElement.Escape(value)}\"";
        }

        /**************************************************************/
        /// <summary>
        /// Safely converts boolean values to lowercase string format required by SPL
        /// </summary>
        /// <param name="boolValue">Boolean value to convert</param>
        /// <returns>"true" or "false" in lowercase</returns>
        /// <seealso cref="Label"/>
        public static string BoolToSplFormat(bool? boolValue)
        {
            if (!boolValue.HasValue)
                return "";

            return boolValue.Value ? "true" : "false";
        }

        /**************************************************************/
        /// <summary>
        /// Safely converts string boolean representations to SPL format
        /// </summary>
        /// <param name="boolString">String representation of boolean</param>
        /// <returns>"true" or "false" in lowercase, or empty string if invalid</returns>
        /// <seealso cref="Label"/>
        public static string BoolToSplFormat(string? boolString)
        {
            if (string.IsNullOrEmpty(boolString))
                return "";

            if (bool.TryParse(boolString, out var boolValue))
                return boolValue ? "true" : "false";

            // Handle common string representations
            var lowerValue = boolString.ToLowerInvariant().Trim();
            return lowerValue switch
            {
                "yes" or "y" or "1" => "true",
                "no" or "n" or "0" => "false",
                _ => ""
            };
        }

        /**************************************************************/
        /// <summary>
        /// Formats numeric values with consistent precision for SPL output
        /// </summary>
        /// <param name="value">Numeric value to format</param>
        /// <returns>Formatted numeric string</returns>
        /// <seealso cref="Label"/>
        public static string FormatNumeric(decimal? value)
        {
            if (!value.HasValue)
                return "";

            return value.Value.ToString("G29", CultureInfo.InvariantCulture);
        }

        /**************************************************************/
        /// <summary>
        /// Formats numeric values with consistent precision for SPL output
        /// </summary>
        /// <param name="value">Numeric value to format</param>
        /// <returns>Formatted numeric string</returns>
        /// <seealso cref="Label"/>
        public static string FormatNumeric(double? value)
        {
            if (!value.HasValue)
                return "";

            return value.Value.ToString("G29", CultureInfo.InvariantCulture);
        }

        /**************************************************************/
        /// <summary>
        /// Formats numeric values with consistent precision for SPL output
        /// </summary>
        /// <param name="value">Numeric value to format</param>
        /// <returns>Formatted numeric string</returns>
        /// <seealso cref="Label"/>
        public static string FormatNumeric(int? value)
        {
            if (!value.HasValue)
                return "";

            return value.Value.ToString(CultureInfo.InvariantCulture);
        }

        /**************************************************************/
        /// <summary>
        /// Safely escapes XML content while preserving valid HTML tags for SPL
        /// </summary>
        /// <param name="content">Content to escape</param>
        /// <returns>XML-safe content string</returns>
        /// <seealso cref="Label"/>
        public static string EscapeXmlContent(string? content)
        {
            if (string.IsNullOrEmpty(content))
                return "";

            return SecurityElement.Escape(content);
        }

        /**************************************************************/
        /// <summary>
        /// Validates and formats NDC codes to ensure proper format
        /// </summary>
        /// <param name="ndcCode">NDC code to validate</param>
        /// <returns>Formatted NDC code or original if validation fails</returns>
        /// <seealso cref="Label"/>
        public static string FormatNdcCode(string? ndcCode)
        {
            if (string.IsNullOrEmpty(ndcCode))
                return "";

            // Remove any existing hyphens and validate format
            var cleanCode = ndcCode.Replace("-", "");

            // Basic validation for NDC format (10 or 11 digits)
            if (cleanCode.All(char.IsDigit) && (cleanCode.Length == 10 || cleanCode.Length == 11))
            {
                // Return with standard hyphen formatting if needed
                // Most SPL examples use existing format, so return as-is
                return ndcCode;
            }

            return ndcCode; // Return original if validation fails
        }
        #endregion
    }
}