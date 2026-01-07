using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace MedRecPro.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Provides helper methods for converting JSON data structures (JArray, JObject, JsonElement)
    /// to pipe-delimited format for token-efficient LLM serialization.
    /// </summary>
    /// <remarks>
    /// This helper class addresses a limitation in the generic ToPipe extension method which cannot
    /// handle JSON wrapper types because reflection on the generic type parameter fails for these types.
    ///
    /// JSON-to-pipe conversion provides significant token savings when serializing data for LLM prompts:
    /// - Typical JSON format: ~8,000-12,000 tokens for 50 items
    /// - Pipe-delimited format: ~1,500-2,500 tokens for same data
    /// - Savings: 75-80% reduction in token usage
    ///
    /// The output format matches the existing pipe format convention:
    /// <code>
    /// [KEY:PN=ProductName|DC=DocumentCount|LN=LabelerName]
    /// PN|DC|LN
    /// LIPITOR|5|Pfizer Inc
    /// VIAGRA|3|Pfizer Inc
    /// </code>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Convert a JArray to pipe format
    /// var jArray = JArray.Parse("[{\"Name\":\"Test\",\"Value\":42}]");
    /// string? pipeResult = JsonPipeHelper.TryConvertToPipe(jArray);
    ///
    /// // Convert a JsonElement to pipe format
    /// var jsonElement = JsonDocument.Parse("[{\"Name\":\"Test\"}]").RootElement;
    /// string? pipeResult = JsonPipeHelper.TryConvertToPipe(jsonElement);
    /// </code>
    /// </example>
    /// <seealso cref="TextUtil.ToPipe{T}(T, bool)"/>
    public static class JsonPipeHelper
    {
        /**************************************************************/
        /// <summary>
        /// Attempts to convert a JSON object (JArray, JObject, or JsonElement) to pipe-delimited format.
        /// Returns null if the object is not a recognized JSON type or conversion fails.
        /// </summary>
        /// <param name="obj">The object to convert (expected to be JArray, JObject, or JsonElement)</param>
        /// <returns>Pipe-delimited string if successful; null if the object is not a JSON type or has no properties</returns>
        /// <remarks>
        /// This method serves as the main entry point for JSON-to-pipe conversion.
        /// It detects the JSON type and dispatches to the appropriate conversion method.
        ///
        /// Supported types:
        /// - JArray (Newtonsoft.Json): Array of JSON objects
        /// - JObject (Newtonsoft.Json): Single JSON object (converted as single-row table)
        /// - JsonElement (System.Text.Json): Both arrays and objects
        /// </remarks>
        /// <example>
        /// <code>
        /// object result = await executeToolCall(tool);
        /// var pipeResult = JsonPipeHelper.TryConvertToPipe(result);
        /// if (!string.IsNullOrEmpty(pipeResult))
        /// {
        ///     // Use pipe format for token efficiency
        ///     return pipeResult;
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="IsJsonType(object?)"/>
        /// <seealso cref="convertJArrayToPipe(JArray)"/>
        /// <seealso cref="convertJObjectToPipe(JObject)"/>
        /// <seealso cref="convertJsonElementToPipe(JsonElement)"/>
        public static string? TryConvertToPipe(object? obj)
        {
            #region implementation

            if (obj == null)
            {
                return null;
            }

            try
            {
                // Handle Newtonsoft.Json JArray
                if (obj is JArray jArray)
                {
                    return convertJArrayToPipe(jArray);
                }

                // Handle Newtonsoft.Json JObject (single object as one-row table)
                if (obj is JObject jObject)
                {
                    return convertJObjectToPipe(jObject);
                }

                // Handle System.Text.Json JsonElement
                if (obj is JsonElement jsonElement)
                {
                    return convertJsonElementToPipe(jsonElement);
                }

                // Not a recognized JSON type
                return null;
            }
            catch (Exception e)
            {
                // Log error but don't throw - caller will fall back to JSON serialization
                ErrorHelper.AddErrorMsg("JsonPipeHelper.TryConvertToPipe: " + e);
                return null;
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Determines whether the given object is a JSON wrapper type that can be converted to pipe format.
        /// </summary>
        /// <param name="obj">The object to check</param>
        /// <returns>True if the object is a JArray, JObject, or JsonElement; false otherwise</returns>
        /// <remarks>
        /// Use this method for early detection of JSON types before attempting conversion.
        /// This avoids unnecessary exception handling in performance-critical code paths.
        /// </remarks>
        /// <example>
        /// <code>
        /// if (JsonPipeHelper.IsJsonType(result))
        /// {
        ///     var pipeResult = JsonPipeHelper.TryConvertToPipe(result);
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="TryConvertToPipe(object?)"/>
        public static bool IsJsonType(object? obj)
        {
            #region implementation

            return obj is JArray || obj is JObject || obj is JsonElement;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts a Newtonsoft.Json JArray to pipe-delimited format.
        /// </summary>
        /// <param name="jArray">The JArray to convert</param>
        /// <returns>Pipe-delimited string with key header and data rows; null if empty or no properties</returns>
        /// <remarks>
        /// Extracts property names from the first item in the array, generates abbreviations,
        /// and builds the pipe-delimited output. All items must have the same structure.
        ///
        /// Values are formatted using <see cref="formatJTokenValue(JToken?)"/> for compact representation.
        /// </remarks>
        /// <seealso cref="generateAbbreviations(string[])"/>
        /// <seealso cref="formatJTokenValue(JToken?)"/>
        private static string? convertJArrayToPipe(JArray jArray)
        {
            #region implementation

            if (jArray == null || jArray.Count == 0)
            {
                return null;
            }

            // Get property names from the first item
            var firstItem = jArray[0] as JObject;
            if (firstItem == null)
            {
                return null;
            }

            var propertyNames = firstItem.Properties().Select(p => p.Name).ToArray();
            if (propertyNames.Length == 0)
            {
                return null;
            }

            // Generate abbreviations for property names
            var abbreviations = generateAbbreviations(propertyNames);

            var result = new StringBuilder();

            // Build the key header [KEY:Ab=Abbreviation|...]
            result.Append("[KEY:");
            result.Append(string.Join("|", abbreviations.Select(kvp => $"{kvp.Value}={kvp.Key}")));
            result.Append("]");
            result.Append(Environment.NewLine);

            // Build the abbreviated header row
            result.Append(string.Join("|", propertyNames.Select(p => abbreviations[p])));
            result.Append(Environment.NewLine);

            // Build value rows for each item
            foreach (var item in jArray)
            {
                if (item is JObject obj)
                {
                    var values = propertyNames.Select(name =>
                    {
                        var token = obj[name];
                        return formatJTokenValue(token);
                    });

                    result.Append(string.Join("|", values));
                    result.Append(Environment.NewLine);
                }
            }

            // Remove trailing newline
            if (result.Length > 0)
            {
                result.Length -= Environment.NewLine.Length;
            }

            return result.ToString();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts a single Newtonsoft.Json JObject to pipe-delimited format as a single-row table.
        /// </summary>
        /// <param name="jObject">The JObject to convert</param>
        /// <returns>Pipe-delimited string with key header and one data row; null if empty or no properties</returns>
        /// <remarks>
        /// Useful when a tool returns a single JSON object rather than an array.
        /// The object is formatted as a single-row table for consistency with array output.
        /// </remarks>
        /// <seealso cref="generateAbbreviations(string[])"/>
        /// <seealso cref="formatJTokenValue(JToken?)"/>
        private static string? convertJObjectToPipe(JObject jObject)
        {
            #region implementation

            if (jObject == null)
            {
                return null;
            }

            var propertyNames = jObject.Properties().Select(p => p.Name).ToArray();
            if (propertyNames.Length == 0)
            {
                return null;
            }

            // Generate abbreviations for property names
            var abbreviations = generateAbbreviations(propertyNames);

            var result = new StringBuilder();

            // Build the key header [KEY:Ab=Abbreviation|...]
            result.Append("[KEY:");
            result.Append(string.Join("|", abbreviations.Select(kvp => $"{kvp.Value}={kvp.Key}")));
            result.Append("]");
            result.Append(Environment.NewLine);

            // Build the abbreviated header row
            result.Append(string.Join("|", propertyNames.Select(p => abbreviations[p])));
            result.Append(Environment.NewLine);

            // Build single value row
            var values = propertyNames.Select(name =>
            {
                var token = jObject[name];
                return formatJTokenValue(token);
            });

            result.Append(string.Join("|", values));

            return result.ToString();

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Converts a System.Text.Json JsonElement to pipe-delimited format.
        /// </summary>
        /// <param name="jsonElement">The JsonElement to convert</param>
        /// <returns>Pipe-delimited string with key header and data rows; null if not an array/object or empty</returns>
        /// <remarks>
        /// Handles both array and object JsonElements:
        /// - Arrays are converted to multi-row tables
        /// - Objects are converted to single-row tables
        ///
        /// Values are formatted using <see cref="formatJsonElementValue(JsonElement)"/> for compact representation.
        /// </remarks>
        /// <seealso cref="generateAbbreviations(string[])"/>
        /// <seealso cref="formatJsonElementValue(JsonElement)"/>
        private static string? convertJsonElementToPipe(JsonElement jsonElement)
        {
            #region implementation

            // Handle array of objects
            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                var items = jsonElement.EnumerateArray().ToList();
                if (items.Count == 0)
                {
                    return null;
                }

                // Get property names from the first item
                var firstItem = items[0];
                if (firstItem.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var propertyNames = firstItem.EnumerateObject().Select(p => p.Name).ToArray();
                if (propertyNames.Length == 0)
                {
                    return null;
                }

                // Generate abbreviations for property names
                var abbreviations = generateAbbreviations(propertyNames);

                var result = new StringBuilder();

                // Build the key header [KEY:Ab=Abbreviation|...]
                result.Append("[KEY:");
                result.Append(string.Join("|", abbreviations.Select(kvp => $"{kvp.Value}={kvp.Key}")));
                result.Append("]");
                result.Append(Environment.NewLine);

                // Build the abbreviated header row
                result.Append(string.Join("|", propertyNames.Select(p => abbreviations[p])));
                result.Append(Environment.NewLine);

                // Build value rows for each item
                foreach (var item in items)
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        var values = propertyNames.Select(name =>
                        {
                            if (item.TryGetProperty(name, out var prop))
                            {
                                return formatJsonElementValue(prop);
                            }
                            return string.Empty;
                        });

                        result.Append(string.Join("|", values));
                        result.Append(Environment.NewLine);
                    }
                }

                // Remove trailing newline
                if (result.Length > 0)
                {
                    result.Length -= Environment.NewLine.Length;
                }

                return result.ToString();
            }

            // Handle single object as single-row table
            if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                var propertyNames = jsonElement.EnumerateObject().Select(p => p.Name).ToArray();
                if (propertyNames.Length == 0)
                {
                    return null;
                }

                // Generate abbreviations for property names
                var abbreviations = generateAbbreviations(propertyNames);

                var result = new StringBuilder();

                // Build the key header [KEY:Ab=Abbreviation|...]
                result.Append("[KEY:");
                result.Append(string.Join("|", abbreviations.Select(kvp => $"{kvp.Value}={kvp.Key}")));
                result.Append("]");
                result.Append(Environment.NewLine);

                // Build the abbreviated header row
                result.Append(string.Join("|", propertyNames.Select(p => abbreviations[p])));
                result.Append(Environment.NewLine);

                // Build single value row
                var values = propertyNames.Select(name =>
                {
                    if (jsonElement.TryGetProperty(name, out var prop))
                    {
                        return formatJsonElementValue(prop);
                    }
                    return string.Empty;
                });

                result.Append(string.Join("|", values));

                return result.ToString();
            }

            // Not a supported JsonElement type (primitive, null, etc.)
            return null;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats a Newtonsoft.Json JToken value for pipe output with compact representation.
        /// </summary>
        /// <param name="token">The JToken value to format</param>
        /// <returns>Compact string representation of the value with pipe characters escaped</returns>
        /// <remarks>
        /// Formatting rules:
        /// - Null/undefined: empty string
        /// - Booleans: "1" for true, "0" for false
        /// - Dates: ISO format (yyyy-MM-dd HH:mm:ss) with trailing zeros trimmed
        /// - GUIDs: 32-character format without hyphens
        /// - Arrays/Objects: JSON representation (for nested structures)
        /// - Strings: Pipe characters escaped as \|
        /// </remarks>
        /// <seealso cref="convertJArrayToPipe(JArray)"/>
        /// <seealso cref="convertJObjectToPipe(JObject)"/>
        private static string formatJTokenValue(JToken? token)
        {
            #region implementation

            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return string.Empty;
            }

            switch (token.Type)
            {
                case JTokenType.Boolean:
                    // Use single char for booleans (saves tokens)
                    return token.Value<bool>() ? "1" : "0";

                case JTokenType.Date:
                    // Use compact ISO format, trim trailing zeros for compactness
                    var dateValue = token.Value<DateTime>();
                    return dateValue.ToString("yyyy-MM-dd HH:mm:ss").TrimEnd(' ', '0', ':').TrimEnd(':');

                case JTokenType.Guid:
                    // Use short guid format without hyphens (32 chars vs 36)
                    var guidValue = token.Value<Guid>();
                    return guidValue.ToString("N");

                case JTokenType.Integer:
                    return token.Value<long>().ToString();

                case JTokenType.Float:
                    // Remove trailing zeros for compactness
                    return token.Value<double>().ToString("G15");

                case JTokenType.Array:
                case JTokenType.Object:
                    // For nested structures, use compact JSON (no formatting)
                    return token.ToString(Newtonsoft.Json.Formatting.None).Replace("|", "\\|");

                case JTokenType.String:
                default:
                    // Escape pipe characters to preserve data integrity
                    string stringValue = token.ToString();
                    return stringValue.Replace("|", "\\|");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Formats a System.Text.Json JsonElement value for pipe output with compact representation.
        /// </summary>
        /// <param name="element">The JsonElement value to format</param>
        /// <returns>Compact string representation of the value with pipe characters escaped</returns>
        /// <remarks>
        /// Formatting rules match those of <see cref="formatJTokenValue(JToken?)"/>:
        /// - Null/undefined: empty string
        /// - Booleans: "1" for true, "0" for false
        /// - Dates: ISO format with trailing zeros trimmed (when string looks like a date)
        /// - GUIDs: 32-character format without hyphens (when string looks like a GUID)
        /// - Arrays/Objects: Raw JSON string
        /// - Strings: Pipe characters escaped as \|
        /// </remarks>
        /// <seealso cref="convertJsonElementToPipe(JsonElement)"/>
        private static string formatJsonElementValue(JsonElement element)
        {
            #region implementation

            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return string.Empty;

                case JsonValueKind.True:
                    return "1";

                case JsonValueKind.False:
                    return "0";

                case JsonValueKind.Number:
                    // Try to get as long first, then double for decimals
                    if (element.TryGetInt64(out var longValue))
                    {
                        return longValue.ToString();
                    }
                    if (element.TryGetDouble(out var doubleValue))
                    {
                        return doubleValue.ToString("G15");
                    }
                    return element.GetRawText();

                case JsonValueKind.String:
                    var stringValue = element.GetString() ?? string.Empty;

                    // Try to parse as DateTime for compact formatting
                    if (DateTime.TryParse(stringValue, out var dateValue))
                    {
                        return dateValue.ToString("yyyy-MM-dd HH:mm:ss").TrimEnd(' ', '0', ':').TrimEnd(':');
                    }

                    // Try to parse as GUID for compact formatting
                    if (Guid.TryParse(stringValue, out var guidValue))
                    {
                        return guidValue.ToString("N");
                    }

                    // Escape pipe characters
                    return stringValue.Replace("|", "\\|");

                case JsonValueKind.Array:
                case JsonValueKind.Object:
                    // For nested structures, use raw JSON text
                    return element.GetRawText().Replace("|", "\\|");

                default:
                    return element.GetRawText().Replace("|", "\\|");
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Generates compact abbreviations for an array of property names.
        /// Uses uppercase letters from property name, falls back to first characters
        /// if insufficient uppercase letters exist. Ensures uniqueness.
        /// </summary>
        /// <param name="propertyNames">Array of property names to abbreviate</param>
        /// <returns>Dictionary mapping full property names to their abbreviations</returns>
        /// <remarks>
        /// Abbreviation strategy:
        /// - Uses uppercase letters from property name (e.g., FirstName → FN)
        /// - Falls back to first 2-3 characters if no uppercase letters exist
        /// - Ensures uniqueness by appending numeric suffix if needed
        /// - Designed to minimize token usage for LLM API calls
        /// </remarks>
        /// <example>
        /// <code>
        /// var names = new[] { "FirstName", "LastName", "DateOfBirth" };
        /// var abbrevs = generateAbbreviations(names);
        /// // Result: { "FirstName": "FN", "LastName": "LN", "DateOfBirth": "DOB" }
        /// </code>
        /// </example>
        /// <seealso cref="createAbbreviation(string, HashSet{string})"/>
        private static Dictionary<string, string> generateAbbreviations(string[] propertyNames)
        {
            #region implementation

            var result = new Dictionary<string, string>();
            var usedAbbreviations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var name in propertyNames)
            {
                string abbreviation = createAbbreviation(name, usedAbbreviations);
                result[name] = abbreviation;
                usedAbbreviations.Add(abbreviation);
            }

            return result;

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Creates a unique abbreviation for a single property name.
        /// Strategy: Extract uppercase letters, use first N chars as fallback,
        /// append numeric suffix if collision occurs.
        /// </summary>
        /// <param name="propertyName">The property name to abbreviate</param>
        /// <param name="usedAbbreviations">Set of already used abbreviations to avoid collisions</param>
        /// <returns>A unique abbreviation string</returns>
        /// <seealso cref="generateAbbreviations(string[])"/>
        private static string createAbbreviation(string propertyName, HashSet<string> usedAbbreviations)
        {
            #region implementation

            if (string.IsNullOrEmpty(propertyName))
            {
                return "X";
            }

            // Strategy 1: Extract uppercase letters (including first char if lowercase)
            var uppercaseChars = new StringBuilder();

            // Always include first character (capitalize if needed)
            uppercaseChars.Append(char.ToUpper(propertyName[0]));

            // Add subsequent uppercase letters
            for (int i = 1; i < propertyName.Length; i++)
            {
                if (char.IsUpper(propertyName[i]))
                {
                    uppercaseChars.Append(propertyName[i]);
                }
            }

            string abbreviation = uppercaseChars.ToString();

            // Strategy 2: If only one char or too short, use first 2-3 chars
            if (abbreviation.Length < 2)
            {
                abbreviation = propertyName.Length >= 3
                    ? propertyName.Substring(0, 3).ToUpper()
                    : propertyName.ToUpper();
            }

            // Ensure uniqueness by appending numeric suffix if needed
            string baseAbbreviation = abbreviation;
            int suffix = 1;

            while (usedAbbreviations.Contains(abbreviation))
            {
                abbreviation = $"{baseAbbreviation}{suffix}";
                suffix++;
            }

            return abbreviation;

            #endregion
        }
    }
}
