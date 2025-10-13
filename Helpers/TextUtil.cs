
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Globalization;

using System.Web;
using System.Collections.Concurrent;
using System.Net;
using System.Drawing;
using Ganss.Xss;
using HtmlAgilityPack;
using Humanizer;


using System.ComponentModel.DataAnnotations;
using static MedRecPro.Helpers.StringCipher;
using MedRecPro.Models;

namespace MedRecPro.Helpers
{
    public static partial class TextUtil
    {
        const int HOURS_TO_ROUND_DAYS = 14;
        const int TAB_SPACES = 8;
        private static readonly HtmlSanitizer _htmlSanitizer = new HtmlSanitizer();
        private static object _lock = new object();


        #region private methods

        /******************************************************/
        /// <summary>
        /// Formats a single path segment (synchronously) according to the specified rules.
        /// </summary>
        /// <param name="input">The raw input segment (can be null or empty).</param>
        /// <param name="defaultValue">The default value if input is null/empty or becomes empty after processing.</param>
        /// <param name="toLower">Whether to convert to lower-case.</param>
        /// <param name="pluralize">Whether to pluralize the segment.</param>
        /// <param name="replaceSpaces">Whether to replace spaces with hyphens.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The processed string segment.</returns>
        private static string? formatSegment(
            string? input,
            string? defaultValue,
            bool toLower,
            bool pluralize,
            bool replaceSpaces,
            CancellationToken cancellationToken)
        {
            #region implementation
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(input))
            {
                return defaultValue;
            }

            // Trim the input.
            string segment = input.Trim();

            // Replace spaces with hyphens if requested.
            if (replaceSpaces)
            {
                segment = segment.Replace(' ', '-');
            }

            // Lower-case conversion.
            if (toLower)
            {
                segment = segment.ToLower();
            }

            // Pluralization if needed.
            if (pluralize)
            {
                // Assumes a Pluralize extension method is available.
                segment = segment.Pluralize();
            }

            // If the result is empty, use the default.
            return string.IsNullOrEmpty(segment) ? defaultValue : segment;
            #endregion
        }

        /// <summary>
        /// Asynchronously wraps the FormatSegment method.
        /// </summary>
        private static Task<string?> formatSegmentAsync(
            string? input,
            string? defaultValue,
            bool toLower,
            bool pluralize,
            bool replaceSpaces,
            CancellationToken cancellationToken)
        {
            return Task.Run(
                () => formatSegment(input, defaultValue, toLower, pluralize, replaceSpaces, cancellationToken),
                cancellationToken);
        }
        #endregion

        /******************************************************/
        /// <summary>
        /// Removes characters that get url encoded
        /// </summary>
        public static string ToUrlSafeBase64StringManual(byte[] inputBytes)
        {
            string base64 = Convert.ToBase64String(inputBytes);
            // Replace URL-unsafe characters and remove padding
            return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        /******************************************************/
        /// <summary>
        /// Converts a URL-safe Base64 string back to a byte array.
        /// </summary>
        /// <param name="urlSafeBase64String"></param>
        /// <returns></returns>
        public static byte[] FromUrlSafeBase64StringManual(string urlSafeBase64String)
        {
            string base64 = urlSafeBase64String.Replace('-', '+').Replace('_', '/');
            // Add padding back if it was removed
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }

        /******************************************************/
        /// <summary>
        /// Decrypts a string of text with a symmetric key.
        /// </summary>  
        public static string? Decrypt(this string text, string key)
        {
            #region implementation
            try
            {
                return new StringCipher().Decrypt(text, key);
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg($"TextUtil.Decrypt: {e.Message}");
                return null;
            }
            #endregion
        }
    
        /******************************************************/
        public static bool IsValidEmail(string email)
        {
            #region implementation
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
            #endregion
        }

        /******************************************************/
        private static string removeInvalidXmlChars(string text)
        {
            #region implementation
            if (!string.IsNullOrEmpty(text))
            {
                var isValid = new Predicate<char>(value =>
                                      (value >= 0x0020 && value <= 0xD7FF) ||
                                      (value >= 0xE000 && value <= 0xFFFD) ||
                                      value == 0x0009 ||
                                      value == 0x000A ||
                                      value == 0x000D);

                return new string(Array.FindAll(text.Where(x => x != 0).ToArray(), isValid));
            }
            else { return text; }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Removes all html type tags
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static string RemoveTags(this string html)
        {
            #region implementation
            html = html.RemoveUnwantedTags(new List<string>(), true);
            html = HttpUtility.HtmlDecode(html);
            return html;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Removes all html type tags
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static string RemoveUnwantedTags(this string html)
        {
            #region implementation
            html = html.RemoveUnwantedTags(new List<string>() { "em", "p", "b" }, false);
            return html;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Takes an HTML string removes most of its tags.
        /// Those items listed in the preserveTags param
        /// will be saved from sanitation.
        /// 
        /// https://stackoverflow.com/questions/12787449/html-agility-pack-removing-unwanted-tags-without-removing-content
        /// </summary>
        /// <param name="html">This string (html)</param>
        /// <param name="preserveTags">list of allowed tags</param>
        /// <param name="cleanAll">default = false</param>
        /// <returns>Sanitized HTML string</returns>
        /// <example>
        /// <code>
        /// var html = "&lt;div&gt;&lt;p&gt;Hello&lt;/p&gt;&lt;script&gt;alert('test')&lt;/script&gt;&lt;/div&gt;";
        /// var preservedTags = new List&lt;string&gt; { "p", "div" };
        /// var result = html.RemoveUnwantedTags(preservedTags);
        /// // Result: "&lt;div&gt;&lt;p&gt;Hello&lt;/p&gt;&lt;/div&gt;"
        /// </code>
        /// </example>
        /// <remarks>
        /// This method uses HtmlAgilityPack to parse and sanitize HTML content.
        /// When cleanAll is true, all HTML tags are removed and only text content is preserved.
        /// When cleanAll is false, only tags not listed in preserveTags are removed.
        /// </remarks>
        /// <seealso cref="Label"/>
        public static string? RemoveUnwantedTags(this string html, List<string> preserveTags, bool cleanAll = false)
        {
            #region implementation

            // Early return for null or empty input
            if (String.IsNullOrEmpty(html))
            {
                return html;
            }

            try
            {
                var document = new HtmlDocument();
                document.LoadHtml(html);

                // Remove all tags and return plain text content
                if (cleanAll)
                {
                    #region strip all html tags

                    char[] array = new char[html.Length];
                    int arrayIndex = 0;
                    bool inside = false; // Tracks whether we're inside an HTML tag

                    for (int i = 0; i < html.Length; i++)
                    {
                        char let = html[i];

                        // Start of HTML tag
                        if (let == '<')
                        {
                            inside = true;
                            continue;
                        }

                        // End of HTML tag
                        if (let == '>')
                        {
                            inside = false;
                            continue;
                        }

                        // Add character to result if we're not inside a tag
                        if (!inside)
                        {
                            array[arrayIndex] = let;
                            arrayIndex++;
                        }
                    }

                    return new string(array, 0, arrayIndex);

                    #endregion
                }

                // Remove unwanted tags using recursive approach while preserving allowed tags
                processNode(document.DocumentNode, preserveTags);
                return document.DocumentNode.InnerHtml;
            }
            catch (Exception e)
            {
                // Log error and return original HTML as fallback
                ErrorHelper.AddErrorMsg("TextUtil.RemoveUnwantedTags: " + e);
            }

            return html;

            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Recursively processes HTML nodes to remove unwanted tags while preserving specified tags and their content.
        /// Uses depth-first traversal to process child nodes before parent nodes.
        /// </summary>
        /// <param name="node">The HTML node to process</param>
        /// <param name="preserveTags">List of tag names that should be preserved during sanitization</param>
        /// <remarks>
        /// This method moves child nodes of removed elements up to the parent level,
        /// ensuring content is preserved even when the containing tag is removed.
        /// </remarks>
        /// <seealso cref="Label"/>
        /// <seealso cref="RemoveUnwantedTags(string, List{string}, bool)"/>
        private static void processNode(HtmlNode node, List<string> preserveTags)
        {
            #region implementation

            // Process children first using depth-first traversal
            // Create copy of child nodes to avoid modification during iteration
            var childrenToProcess = node.ChildNodes.ToList();

            foreach (var child in childrenToProcess)
            {
                processNode(child, preserveTags);
            }

            // Process current node after children have been processed
            if (node.NodeType == HtmlNodeType.Element &&
                !preserveTags.Contains(node.Name, StringComparer.OrdinalIgnoreCase))
            {
                #region remove unwanted element

                var parent = node.ParentNode;
                if (parent != null)
                {
                    // Move all child nodes to the parent level before removing this node
                    var children = node.ChildNodes.ToList(); // Create copy to avoid collection modification issues

                    foreach (var child in children)
                    {
                        // Insert each child before the current node
                        parent.InsertBefore(child, node);
                    }

                    // Remove the unwanted tag node (content has already been preserved)
                    parent.RemoveChild(node);
                }

                #endregion
            }

            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Removes unwanted HTML/XML tags from text fragments while properly handling nested structures 
        /// and preserving the original case of both tag names and attribute names.
        /// This method tracks opening/closing tag pairs to handle complex nesting scenarios correctly.
        /// </summary>
        /// <param name="html">The text fragment containing HTML/XML tags to process</param>
        /// <param name="preserveTags">List of tag names to preserve (case-insensitive matching)</param>
        /// <param name="cleanAll">If true, removes all tags and returns only text content</param>
        /// <returns>Text with unwanted tags removed while preserving case and proper nesting</returns>
        /// <example>
        /// <code>
        /// var html = "&lt;caption&gt;&lt;unwanted&gt;Some text&lt;content styleCode=\"bold\"&gt;&lt;linkHtml href=\"ID_\"&gt;txt&lt;/linkHtml&gt;&lt;/content&gt;&lt;/unwanted&gt;&lt;/caption&gt;";
        /// var preserveTags = new List&lt;string&gt; { "caption", "content", "linkHtml" };
        /// var result = html.RemoveUnwantedTagsRegEx(preserveTags);
        /// // Result: "&lt;caption&gt;Some text&lt;content styleCode=\"bold\"&gt;&lt;linkHtml href=\"ID_\"&gt;txt&lt;/linkHtml&gt;&lt;/content&gt;&lt;/caption&gt;"
        /// </code>
        /// </example>
        /// <remarks>
        /// This method fails in the following scenarios:
        /// 1. Malformed HTML with unmatched tags: "&lt;div&gt;&lt;span&gt;content&lt;/div&gt;" - May produce unexpected results
        /// 2. Tags with complex attributes containing angle brackets: "&lt;script&gt;if (a &lt; b) {}&lt;/script&gt;" - Regex may not parse correctly
        /// 3. CDATA sections: "&lt;![CDATA[&lt;unwanted&gt;content&lt;/unwanted&gt;]]&gt;" - Content inside CDATA is not processed
        /// 4. Comments containing tag-like content: "&lt;!-- &lt;unwanted&gt;comment&lt;/unwanted&gt; --&gt;" - Comments are not handled specially
        /// 5. Script/style content with tag-like strings: "&lt;script&gt;document.write('&lt;div&gt;');&lt;/script&gt;" - May interfere with tag matching
        /// </remarks>
        /// <seealso cref="Label"/>
        public static string? RemoveUnwantedTagsRegEx(this string html, List<string> preserveTags, bool cleanAll = false)
        {
            if (String.IsNullOrEmpty(html))
            {
                return html;
            }

            try
            {
                #region implementation

                // Handle complete tag removal scenario
                if (cleanAll)
                {
                    // Pattern explanation: <[^>]*>; matches any content between angle brackets
                    // [^>]* means "any character except >" zero or more times
                    return Regex.Replace(html, @"<[^>]*>", "");
                }

                // Create case-insensitive lookup set for performance
                var preserveSet = new HashSet<string>(preserveTags, StringComparer.OrdinalIgnoreCase);
                var result = new StringBuilder();
                var tagStack = new Stack<tagInfo>(); // Track nested tag hierarchy

                // Pattern explanation:
                // (/?)                    - Group 1: Optional forward slash for closing tags
                // ([a-zA-Z][a-zA-Z0-9-]*) - Group 2: Tag name starting with letter, followed by letters/numbers/hyphens
                // [^>]*?                  - Non-greedy match of any attributes until closing bracket
                // (/?)                    - Group 3: Optional forward slash for self-closing tags
                var tagPattern = @"<(/?)([a-zA-Z][a-zA-Z0-9-]*)[^>]*?(/?)>";
                var lastIndex = 0;

                foreach (Match match in Regex.Matches(html, tagPattern))
                {
                    var isClosing = match.Groups[1].Value == "/";      // Detect closing tags like </div>
                    var tagName = match.Groups[2].Value;               // Extract tag name (preserves original case)
                    var isSelfClosing = match.Groups[3].Value == "/";  // Detect self-closing tags like <br/>
                    var shouldPreserve = preserveSet.Contains(tagName); // Case-insensitive tag preservation check

                    // Extract and process text content before this tag
                    var textBefore = html.Substring(lastIndex, match.Index - lastIndex);

                    // Only include text content if we're not inside an unwanted tag block
                    if (!isInsideUnwantedTag(tagStack))
                    {
                        result.Append(textBefore);
                    }

                    // Process different tag types
                    if (isSelfClosing)
                    {
                        // Self-closing tags don't affect nesting hierarchy
                        if (shouldPreserve && !isInsideUnwantedTag(tagStack))
                        {
                            result.Append(match.Value); // Preserve original case and attributes
                        }
                    }
                    else if (isClosing)
                    {
                        // Handle closing tags by matching with most recent opening tag
                        if (tagStack.Count > 0 &&
                            string.Equals(tagStack.Peek().Name, tagName, StringComparison.OrdinalIgnoreCase))
                        {
                            var poppedTag = tagStack.Pop();
                            // Only output closing tag if the opening tag was preserved
                            if (poppedTag.ShouldPreserve && !isInsideUnwantedTag(tagStack))
                            {
                                result.Append(match.Value); // Preserve original case
                            }
                        }
                        // Unmatched closing tags are silently ignored
                    }
                    else
                    {
                        // Handle opening tags by pushing to stack for nesting tracking
                        var tagInfo = new tagInfo
                        {
                            Name = tagName,                    // Store original case
                            ShouldPreserve = shouldPreserve,   // Cache preservation decision
                            OriginalTag = match.Value          // Store complete original tag with attributes
                        };

                        tagStack.Push(tagInfo);

                        // Output opening tag if it should be preserved and we're not inside unwanted content
                        if (shouldPreserve && !isInsideUnwantedTag(tagStack, excludeCurrent: true))
                        {
                            result.Append(match.Value); // Preserve original case and all attributes
                        }
                    }

                    lastIndex = match.Index + match.Length;
                }

                // Append any remaining text after the last tag
                var remainingText = html.Substring(lastIndex);
                if (!isInsideUnwantedTag(tagStack))
                {
                    result.Append(remainingText);
                }

                return result.ToString();

                #endregion
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.RemoveUnwantedTagsRegEx: " + e);
                return html; // Return original content on error
            }
        }

        #region regex tag handling helpers

        /******************************************************/
        /// <summary>
        /// Helper class to track tag information during processing.
        /// Maintains original tag case and preservation status for nested tag handling.
        /// </summary>
        /// <seealso cref="Label"/>
        private class tagInfo
        {
            /// <summary>
            /// Gets or sets the original tag name with preserved case.
            /// </summary>
            /// <seealso cref="Label"/>
            public string Name { get; set; } = "";

            /// <summary>
            /// Gets or sets whether this tag should be preserved in the output.
            /// Cached during processing for performance optimization.
            /// </summary>
            /// <seealso cref="Label"/>
            public bool ShouldPreserve { get; set; }

            /// <summary>
            /// Gets or sets the complete original tag string including all attributes.
            /// Preserves exact case and formatting of the source content.
            /// </summary>
            /// <seealso cref="Label"/>
            public string OriginalTag { get; set; } = "";
        }

        /******************************************************/
        /// <summary>
        /// Determines if the current processing position is inside an unwanted tag block.
        /// Traverses the tag stack to check if any parent tag is marked for removal.
        /// </summary>
        /// <param name="tagStack">Stack containing the current tag hierarchy</param>
        /// <param name="excludeCurrent">If true, excludes the topmost tag from the check</param>
        /// <returns>True if inside an unwanted tag block, false otherwise</returns>
        /// <remarks>
        /// This method is critical for proper nesting handling. It ensures that content
        /// inside unwanted tags is not included in the output, even if it contains wanted tags.
        /// </remarks>
        /// <seealso cref="Label"/>
        private static bool isInsideUnwantedTag(Stack<tagInfo> tagStack, bool excludeCurrent = false)
        {
            // Convert stack to array for efficient traversal
            var stackArray = tagStack.ToArray();
            var startIndex = excludeCurrent ? 1 : 0; // Skip current tag if requested

            // Check each parent tag in the hierarchy
            for (int i = startIndex; i < stackArray.Length; i++)
            {
                // If any parent tag is not preserved, we're inside unwanted content
                if (!stackArray[i].ShouldPreserve)
                {
                    return true;
                }
            }

            return false; // All parent tags are preserved
        }

        #endregion


        /**************************************************************/
        /// <summary>
        /// Minifies XML by removing unnecessary whitespace and formatting while preserving the XML structure and content.
        /// Returns the original XML string if it is null, empty, or if parsing fails.
        /// </summary>
        /// <param name="xml">The XML string to minify. Can be null or whitespace.</param>
        /// <returns>
        /// A minified XML string with whitespace removed, or the original input if null, empty, or invalid XML.
        /// </returns>
        /// <remarks>
        /// This method uses XDocument to parse and reformat the XML with SaveOptions.DisableFormatting.
        /// If an exception occurs during parsing or minification, the original XML is returned and an error is logged.
        /// </remarks>
        /// <example>
        /// <code>
        /// string xml = @"&lt;root&gt;
        ///     &lt;item&gt;Value&lt;/item&gt;
        /// &lt;/root&gt;";
        /// string minified = xml.MinifyXml();
        /// // Result: "&lt;root&gt;&lt;item&gt;Value&lt;/item&gt;&lt;/root&gt;"
        /// </code>
        /// </example>
        /// <seealso cref="System.Xml.Linq.XDocument"/>
        /// <seealso cref="System.Xml.Linq.SaveOptions"/>
        public static string? MinifyXml(this string? xml)
        {
            #region implementation
            // Return early if input is null or whitespace
            if (string.IsNullOrWhiteSpace(xml)) return xml;

            try
            {
                // Trim leading/trailing whitespace to avoid XML declaration errors
                var trimmedXml = xml.Trim();

                // Parse the XML string into an XDocument
                var doc = XDocument.Parse(trimmedXml);

                // Write the document to a string with formatting disabled
                using (var stringWriter = new StringWriter())
                {
                    doc.Save(stringWriter, SaveOptions.DisableFormatting);
                    string compactedXml = stringWriter.ToString();
                    return compactedXml;
                }
            }
            catch (Exception e)
            {
                // Log the error and return the original XML to prevent data loss
                ErrorHelper.AddErrorMsg("TextUtil.MinifyXml: " + e);
                return xml; // Return original XML on error
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Aggressively normalizes whitespace in XML/HTML content to create compact
        /// representation while ensuring proper spacing between text and tags for readability.
        /// Special handling for inline formatting tags (sup, sub, em, strong, etc.) to avoid
        /// unwanted spaces.
        /// </summary>
        /// <param name="text">The XML/HTML text content to normalize</param>
        /// <returns>Compact text with proper spacing preserved</returns>
        /// <example>
        /// <code>
        /// var input = "area (mg/m\n&lt;sup&gt;2 &lt;/sup&gt;\n). In";
        /// var result = input.NormalizeXmlWhitespace();
        /// // Result: "area (mg/m&lt;sup&gt;2&lt;/sup&gt;). In"
        /// 
        /// var input2 = "tablets&lt;content styleCode=\"italics\"&gt;[see&lt;linkHtml";
        /// var result2 = input2.NormalizeXmlWhitespace();
        /// // Result: "tablets &lt;content styleCode=\"italics\"&gt;[see &lt;linkHtml"
        /// </code>
        /// </example>
        /// <remarks>
        /// This method ensures:
        /// - NO space before/after/inside inline formatting tags (sup, sub, em, strong, i, b, u, span)
        /// - Space after text/punctuation when followed by block-level opening tags (for readability)
        /// - NO space before &lt;br /&gt; tags (they provide their own spacing)
        /// - NO space between consecutive tags
        /// - Minimal whitespace while maintaining readable output
        /// </remarks>
        /// <seealso cref="Label"/>
        public static string? NormalizeXmlWhitespace(this string? text)
        {
            #region implementation
            if (string.IsNullOrEmpty(text)) return text;

            // Step 1: Collapse all consecutive whitespace (spaces, tabs, newlines) to single space
            var normalized = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\s+",
                " ");

            // Step 2: Remove ALL whitespace between consecutive tags
            // Handles: "</tag> <tag>" → "</tag><tag>", "> <" → "><"
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @">\s+<",
                "><");

            // Step 3: Remove whitespace immediately after opening tags (before text/content)
            // Handles: "<tag> text" → "<tag>text"
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @">\s+([^<])",
                ">$1");

            // Step 4: Remove whitespace immediately before closing tags (after text/content)
            // Handles: "text </tag>" → "text</tag>"
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @"([^>])\s+</",
                "$1</");

            // Step 5: Remove whitespace specifically before inline formatting tags
            // These tags should be tight against surrounding text: m<sup>2</sup> not m <sup>2</sup>
            // Inline tags: sup, sub, em, strong, i, b, u, span, italics
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @"\s+(<(?:sup|sub|em|strong|i|b|u|span|italics)[\s>])",
                "$1",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Step 6: Remove whitespace after closing inline formatting tags when followed by punctuation
            // Handles: "</sup> )" → "</sup>)" and "</em> ." → "</em>."
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @"(<\/(?:sup|sub|em|strong|i|b|u|span|italics)>)\s+([.,;:!?\)\]\}])",
                "$1$2",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Step 7: Ensure single space between text/punctuation and following block-level opening tags
            // This prevents "word<tag>" from rendering as "wordcontent" on screen
            // Exclude: br tags and inline formatting tags
            // Pattern explanation:
            // ([\w\).,;:!?\]"'\-])                    - Match word char or common punctuation
            // (<(?!br[\s/>]|sup|sub|em|strong|i|b|u|span|italics))  - Opening tag that's NOT br or inline
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @"([\w\).,;:!?\]""'\-])(<(?!br[\s/>]|/?(?:sup|sub|em|strong|i|b|u|span|italics)[\s>]))",
                "$1 $2",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Step 8: Remove any space before <br> tags specifically
            // br tags provide their own spacing, so "text. <br />" should be "text.<br />"
            normalized = System.Text.RegularExpressions.Regex.Replace(
                normalized,
                @"\s+(<br[\s/>])",
                "$1",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return normalized.Trim();
            #endregion
        }

        /******************************************************/
        public static string SanitizeXML(string text)
        {
            #region implementation
            try
            {
                if (!string.IsNullOrEmpty(text))
                {
                    return removeInvalidXmlChars(text);
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("FacetData.SanitizeXML: " + e);
            }

            return null;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Takes a string list and evaluates the rows to 
        /// see if any contain the specified delimiter. If the
        /// there a matches the delimited text is broken apart and
        /// add as individual values to the string. The final
        /// result is ordered and made unique. This is useful
        /// when you are constructing a menu where you need to
        /// unpack multiselect values from the packed data.
        /// </summary>
        /// <example>{ a, a;b;c, a;b, d, a;e;f } => { a, b, c, d, e, f }</example>
        /// <param name="values"></param>
        /// <param name="delimiter"></param>
        /// <returns></returns>
        public static List<string> UnpackDelimitedValues(this List<string> values, char delimiter)
        {
            #region implementation
            try
            {
                //break apart any delimiter delimited strings and
                //use them as individual menu elements
                if (values != null
                    && values.Count > 0
                    && values.Any(x => x.Contains(delimiter)))
                {
                    //split delimited strings and add to list
                    values
                        ?.Where(x => !string.IsNullOrEmpty(x) && x.Contains(delimiter))
                        ?.Select(x => x)
                    ?.ToList()
                        ?.ForEach(y => values.AddRange(y.Split(delimiter).ToList()));

                    //remove delimited strings as they were
                    //parsed and divided above
                    values
                        ?.RemoveAll(x => x.Contains(delimiter));

                    //order the new values and
                    //create a distinct list
                    values = values
                        ?.OrderBy(x => x)
                        ?.Distinct()
                        ?.ToList();
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.UnpackDelimitedValues (failed string unpacking): " + e.Message);
            }

            return values;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Converts a known color into an RGBA string that 
        /// can be used in JSON
        /// </summary>
        /// <param name="knownColor"></param>
        /// <param name="alpha">(optional) values 0.0 - 1.0 with single decimal place</param>
        /// <returns></returns>
        /// <example>rgba(158,158,158,0.5)</example>
        public static string ToRGBA(this KnownColor knownColor, double alpha = 1.0)
        {
            #region implementation
            StringBuilder stringBuilder = new StringBuilder(32);

            Color color = Color.FromKnownColor(knownColor);

            if (color != null)
            {
                try
                {
                    stringBuilder.Append("rgba(");
                    stringBuilder.Append(Convert.ToString((int)color.R));
                    stringBuilder.Append(",");
                    stringBuilder.Append(Convert.ToString((int)color.G));
                    stringBuilder.Append(",");
                    stringBuilder.Append(Convert.ToString((int)color.B));
                    stringBuilder.Append(",");
                    if (alpha <= 1.0)
                        stringBuilder.Append(alpha.ToString("N1"));
                    else
                        stringBuilder.Append("1.0");
                    stringBuilder.Append(")");


                    return stringBuilder.ToString();
                }
                catch (Exception e)
                {
                    ErrorHelper.AddErrorMsg("Util.ToRGBA: " + e);
                }
            }
            return string.Empty;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Encrypts a string of text with a symmetric key.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="key"></param>
        /// <param name="strength">default = Strong</param>
        /// <seealso cref="StringCipher.Encrypt(string, string, StringCipher.EncryptionStrength)"/>
        /// <returns></returns>
        public static string Encrypt(this string text, string key, EncryptionStrength strength = EncryptionStrength.Strong)
        {
            return StringCipher.Encrypt(text, key);
        }

        /******************************************************/
        /// <summary>
        /// Provides rows of document guids that can be passed
        /// to GetDocumentSearchResult for getting a single 
        /// document
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static string GetDocumentGuidRowXML(this Guid? guid)
        {
            #region implementation

            string? xml = null;

            try
            {
                if (!guid.IsNullOrEmpty())
                {
                    xml += "<rows>";
                    xml += "<row DocumentGUID=" + '\u0022' + guid.ToString() + '\u0022' + " />";
                    xml += "</rows>";
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.GetGuidRowXML: " + e);
            }

            return xml;

            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Returns a comma delimited string for every property
        /// value in a generic IEnumerable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        /// <example>Value:Text,Value:Other Text, ...</example>
        /// <remarks>This method was performance checked using three
        /// different looping styles. The slowest were commented out and
        /// the remaining item was shown to be faster.</remarks>
        public static string ListToCommaString<T>(this IEnumerable<T> obj)
        {
            #region implementation
            string ret = null;
            //List<T> objList;
            ConcurrentBag<string> objVals = new ConcurrentBag<string>();
            //double t1, t2, t3;
            //Stopwatch stopwatch;
            try
            {
                //avoid enumeration error
                //objList = obj?.ToList();
                var objArr = obj?.ToArray();

                //add the value of every prop to a bag of strings
                if (objArr != null && objArr.Length > 0)
                {
                    for (int i = 0; i < objArr.Length; i++)
                    {
                        if (objArr[i] != null)
                        {
                            var props = objArr[i].GetType().GetProperties();

                            for (int k = 0; k < props.Count(); k++)
                            {
                                string name = props[k].Name;
                                objVals.Add(name + ":" + objArr[i].GetPropertyValueAsString(name));
                            }
                        }
                    }

                    //add every row to one big honking string
                    if (objVals != null && objVals.Count > 0)
                    {
                        ret = string.Join(",", objVals);
                    }

                    //for performance testing only
                    //t3 = stopwatch.Elapsed.TotalMilliseconds;
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.ListToCommaString: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Returns a comma delimited string for every property
        /// value in a generic type. If the object of type T is a List, then
        /// this will produce the same values as ListToCommaString().
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        /// <seealso cref="ListToCommaString{T}(IEnumerable{T})"/>
        /// <example>Value:Text,Value:Other Text, ...</example>
        public static string ToCommaString<T>(this T obj)
        {
            #region implementation
            string ret = null;
            List<string> objVals = new List<string>();

            try
            {
                //add the value of every prop to a bag of strings
                if (obj != null)
                {
                    //in case this is a list
                    ret = ListToCommaString(obj as IEnumerable<T>);

                    //process if the object wasn't a list
                    if (string.IsNullOrEmpty(ret))
                    {
                        var props = obj.GetType().GetProperties();

                        foreach (var prop in props)
                        {
                            string name = prop.Name;
                            objVals.Add(string.Concat(name, ":", obj.GetPropertyValueAsString(name)));
                        }
                    }
                }

                //add every row to one big honking string
                if (objVals != null && objVals.Count > 0)
                {
                    ret = string.Join(",", objVals);
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.ToCommaString: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Removes problematic characters from  a string
        /// in order to produce a clean file name.
        /// </summary>
        /// <param name="txt"></param>
        /// <returns></returns>
        public static string CleanFileName(this string txt)
        {
            #region implementation
            try
            {
                if (!string.IsNullOrEmpty(txt))
                {
                    //clean bad chars out of the file name
                    txt = Regex.Replace(txt, @"\s", "-");
                    txt = Regex.Replace(txt, @"\\|\/|,", "");
                    txt = Regex.Replace(txt, @"(?!\.)(?!-)([\W+\s+])", "");
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.CleanFileName: " + e);
            }

            return txt;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Takes a string seperated by commas and converts it
        /// to a string list.
        /// 
        /// Example Input: "dog, cat, mouse"
        /// Output: {"dog","cat","mouse"}
        /// </summary>
        /// <param name="txt"></param>
        /// <returns></returns>
        public static List<string> CommaDelimitedToList(this string txt)
        {
            #region implementation
            List<string> ret = null;

            try
            {
                //check for commas
                if (txt.Contains(','))
                {
                    //split to array
                    string[] ar = txt?.Split(',');

                    //if the array has elements then process
                    if (ar != null && ar.Length > 0)
                    {
                        //initialize list
                        ret = new List<string>(ar.Length);

                        //walk array
                        for (int i = 0; i < ar.Length; i++)
                        {
                            //check that member has a value
                            if (!string.IsNullOrEmpty(ar[i]))
                            {
                                //add trimmed string to list
                                ret?.Add(ar[i].Trim());
                            }
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(txt))
                {
                    ret = new List<string>();
                    ret?.Add(txt.Trim());
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.CommaDelimitedToList: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Removes special characters except whitespace, 
        /// commas, and dashes e.g.it cleans the quotes 
        /// and brackets but retains the spaces, dash and comma.
        /// 
        /// for the below input: 
        /// ["["Tag Tag-Taggety Tag"]","["Test"]"]
        /// 
        /// output:
        /// Tag Tag-Taggety Tag,Test
        /// </summary>
        /// <param name="txt"></param>
        /// <returns></returns>
        public static string RemoveJSONChars(this string txt)
        {
            #region implementation
            string ret = null;

            /*
             * Removes special characters except whitespace,
             * commas, and dashes e.g. it cleans the quotes
             * and brackets but retains the spaces, dash and comma
             * for the below input:
             * ["["Tag Tag-Taggety Tag"]","["Test"]"]
             */
            string pattern = @"((?!\,)(?!\s)(?!-)([\W]))";

            try
            {
                if (!string.IsNullOrEmpty(txt))
                {
                    ret = Regex.Replace(txt, pattern, string.Empty);
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.RemoveJSONChars: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Provides an elapsed time frame summary string e.g. "5 Minutes ago"
        /// This does need some improvment for those lists that require
        /// sorting. Once the time span is summarized and used in a table
        /// the sort becomes alphabetical which is not what you want.
        /// </summary>
        /// <param name="ts">The timespan you want summarized</param>
        ///  <param name="referenceHour">The hour in which the entry was made</param>
        /// <returns>A string e.g. "6 Days ago</returns>
        public static string FormatElapsedTime(TimeSpan ts, int? referenceHour = 0)
        {
            #region implementation
            int numDays = ts.Days;

            //if total hours + the entry hour is before midnight
            if (ts.TotalHours + Convert.ToInt32(referenceHour) <= 24)
            {
                if (ts.TotalHours < 1)
                {
                    //previous display commented out incase it needs to be changed back
                    return ts.TotalMinutes <= 1 ? /* string.Format(@"{0:%m} Minute ago", ts) */ "Just Now" : /* string.Format(@"{0:%m} Minutes ago", ts) */ "Moments Ago";
                }
                else
                {
                    //previous display commented out incase it needs to be changed back
                    //return ts.Hours >= 1 ? string.Format(@"{0:%h} Hour ago", ts)  : string.Format(@"{0:%h} Hours ago", ts);
                    return "Today";
                }
            }
            else
            {
                //if less than 1 day and after 5am then 'Yesterday' otherwise 'xxx Days ago'
                return ts.Days <= 1 ? /* string.Format(@"{0:%d} Day ago", ts */ "Yesterday" : /* string.Format(@"{0:%d} Days ago", ts); */ string.Format(@"{0} Days Ago", numDays.ToString("D3"));
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Shortens a string to specified length with a trailing elipses
        /// </summary>
        /// <param name="value">The string to be truncated</param>
        /// <param name="maxChars">The number of charachters you want</param>
        /// <returns>The truncated string</returns>
        public static string Truncate(this string value, int maxChars)
        {
            #region implementation
            if (value == null) { return ""; }
            return value.Length <= maxChars ? value : value.Substring(0, maxChars) + " ...";
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// truncates a string in the middle and replaces
        /// the center with ellipses.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="maxChars"></param>
        /// <returns></returns>
        public static string TruncateMiddle(this string value, int maxChars)
        {
            #region implementation
            int middle;
            if (value == null) { return ""; }

            middle = maxChars / 2;
            maxChars += 3;

            if (value.Length > maxChars)
            {
                return value.Substring(0, middle) + "..." + value.Substring((value.Length - middle), middle);
            }

            return value;

            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Produces a local date/time string representation in the long
        /// format.
        /// </summary>
        /// <param name="timezone">A string that can be resolved to a timezone e.g. "Eastern Standard Time"</param>
        /// <param name="utcTime">The time you want convert</param>
        /// <returns>string</returns>
        public static string GetLongDateTime(String timezone, DateTime utcTime)
        {
            #region implementation

            try
            {
                TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                DateTime myTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, zone);

                return myTime.ToString("f");
            }
            catch (TimeZoneNotFoundException)
            {
                Debug.Write("The registry does not define the " + timezone + " zone.");
                return utcTime.ToLocalTime().ToString("f");

            }
            catch (InvalidTimeZoneException)
            {
                Debug.Write("Registry data on the " + timezone + " zone has been corrupted.");
                return utcTime.ToLocalTime().ToString("f");
            }

            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Builds a string log entry for transferring an assignment
        /// </summary>
        /// <param name="myGUID">The calling user's GUID</param>
        /// <param name="destinationGUID">The new assignment owner's GUID</param>
        /// <param name="isExternal">Is the assignment in this office or another one?</param>
        /// <param name="category">Assignment category</param>
        /// <param name="newOwner">The new assignment owner's name (firstName lastName)</param>
        /// <param name="newOffice">The office for the assignment's destination</param>
        /// <param name="reason">The reason the assignment is being transferred e.g. consultaion, review, etc</param>
        /// <param name="returnToMeBy">A sentence that requests the return date e.g. Please return to me by MM/dd/yy</param>
        /// <returns>string</returns>
        public static string GetAssignmentTransferLogEntry(
           string myGUID,
           string destinationGUID,
           bool isExternal,
           string category,
           string newOwner = null,
           string newOffice = null,
           string reason = null,
           string returnToMeBy = null)
        {
            #region implementation

            string logEntry;

            //----------------------------------------------------
            //build log entry with a transfer reason
            if (reason != null && reason != "")
            {
                //gone to another office
                if (isExternal)
                {
                    //with return date
                    if (returnToMeBy != null)
                    {
                        logEntry = String.Format("<AssignmentAction>forwarded</AssignmentAction> the <em><AssignmentCategory>{0}</AssignmentCategory></em> assignment to <AssignmentOwner>{1}</AssignmentOwner> (<TransferOffice>{2}</TransferOffice>) for <TransferReason>{3}</TransferReason>. {4}", category, newOwner, newOffice, reason, returnToMeBy);
                    }
                    //without return date
                    else
                    {
                        logEntry = String.Format("<AssignmentAction>forwarded</AssignmentAction> the <em><AssignmentCategory>{0}</AssignmentCategory></em> assignment to <AssignmentOwner>{1}</AssignmentOwner> (<TransferOffice>{2}</TransferOffice>) for <TransferReason>{3}</TransferReason>.", category, newOwner, newOffice, reason);
                    }
                }
                //stayed in my office
                else
                {
                    //coming back to me
                    if (myGUID.Equals(destinationGUID))
                    {
                        logEntry = String.Format("<AssignmentAction>recalled</AssignmentAction> the <em><AssignmentCategory>{0}</AssignmentCategory></em> assignment for <TransferReason>{1}</TransferReason>.", category, reason);
                    }
                    //going to someone else
                    else
                    {
                        //with return date
                        if (returnToMeBy != null)
                        {
                            logEntry = String.Format("<AssignmentAction>transferred</AssignmentAction> the <em><AssignmentCategory>{0}</AssignmentCategory></em> assignment to <AssignmentOwner>{1}</AssignmentOwner> for <TransferReason>{2}</TransferReason>. {3}", category, newOwner, reason, returnToMeBy);
                        }
                        //without return date
                        else
                        {
                            logEntry = String.Format("<AssignmentAction>transferred</AssignmentAction> the <em><AssignmentCategory>{0}</AssignmentCategory></em> assignment to <AssignmentOwner>{1}</AssignmentOwner> for <TransferReason>{2}</TransferReason>.", category, newOwner, reason);
                        }
                    }
                }
            }
            //no transfer reason provided
            else
            {
                //gone to another office
                if (isExternal)
                {
                    //with return date
                    if (returnToMeBy != null)
                    {
                        logEntry = String.Format("<AssignmentAction>forwarded</AssignmentAction> the <em><AssignmentCategory>{0}</AssignmentCategory></em> assignment to <AssignmentOwner>{1}</AssignmentOwner> (<TransferOffice>{2}</TransferOffice>). {3}", category, newOwner, newOffice, returnToMeBy);
                    }
                    //without return date
                    else
                    {
                        logEntry = String.Format("<AssignmentAction>forwarded</AssignmentAction> the <em><AssignmentCategory>{0}</AssignmentCategory></em> assignment to <AssignmentOwner>{1}</AssignmentOwner> (<TransferOffice>{2}</TransferOffice>).", category, newOwner, newOffice);
                    }
                }
                //stayed in my office
                else
                {
                    //coming back to me
                    if (myGUID.Equals(destinationGUID))
                    {
                        logEntry = String.Format("<AssignmentAction>recalled</AssignmentAction> the <em><AssignmentCategory>{0}</AssignmentCategory></em> assignment.", category);
                    }
                    //going to someone else
                    else
                    {
                        //with return date
                        if (returnToMeBy != null)
                        {
                            logEntry = String.Format("<AssignmentAction>transferred</AssignmentAction> the <em><AssignmentCategory>{0}</AssignmentCategory></em> assignment to <AssignmentOwner>{1}</AssignmentOwner>. {2}", category, newOwner, returnToMeBy);
                        }
                        //without return date
                        else
                        {
                            logEntry = String.Format("<AssignmentAction>transferred</AssignmentAction> the <em><AssignmentCategory>{0}</AssignmentCategory></em> assignment to <AssignmentOwner>{1}</AssignmentOwner>.", category, newOwner);
                        }
                    }
                }
            }

            return logEntry;

            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Generate US phone numbers
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string PhoneNumber(string value)
        {
            #region implementation
            value = new System.Text.RegularExpressions.Regex(@"\D")
                    .Replace(value, string.Empty);
            value = value.TrimStart('1');
            if (value.Length == 7)
            {
                return Convert.ToInt64(value).ToString("###-####");
            }

            if (value.Length == 10)
            {
                return Convert.ToInt64(value).ToString("###-###-####");
            }

            if (value.Length > 10)
            {
                return Convert.ToInt64(value)
                    .ToString("###-###-#### " + new String('#', (value.Length - 10)));
            }

            return value;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Takes a string and returns it as a Tuple i.e.
        /// item1 = First Name and item2 = Last Name
        /// </summary>
        /// <param name="txt"></param>
        /// <returns></returns>
        public static Tuple<string, string> SplitName(string txt)
        {
            #region implementation

            Tuple<string, string> ret = new Tuple<string, string>(null, null);

            //helper to convert text to title case
            TextInfo tc = new CultureInfo("en-US", false).TextInfo;

            try
            {
                if (txt != null)
                {
                    txt = tc.ToTitleCase(txt);

                    //if supplied as "lastname, firstname"
                    if (txt.Contains(","))
                    {
                        string[] names = txt.Split(',').ToArray();

                        if (names != null && names.Length >= 2)
                        {
                            ret = new Tuple<string, string>(names[1].Trim(), names[0].Trim());
                        }
                    }
                    else
                    {
                        string[] split = Regex.Split(txt, @"\W|_");

                        if (split.Count() >= 2)
                        {
                            //if supplied as "firstname lastname"
                            ret = new Tuple<string, string>(
                            Convert.ToString(split[0]),
                            Convert.ToString(split[1]));
                        }
                        else if (split.Count() == 1)
                        {
                            //if supplied a single word
                            ret = new Tuple<string, string>(Convert.ToString(split[0]), string.Empty);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.SplitName: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Strips the middle initials from a string of names.
        /// The passed string can contain multiple names. In
        /// the below input example "X." will be removed.
        /// 
        /// Input Example: Zou, Ling; Pottel, Joshua; Khuri, Natalia; Ngo, X. Huy
        /// </summary>
        /// <param name="txt"></param>
        /// <returns></returns>
        public static string RemoveMiddleInitial(this string txt)
        {
            #region implementation
            string ret = txt;
            string pattern = @"([A-Z]\.)";
            try
            {
                if (txt != null && txt != string.Empty)
                {
                    ret = Regex.Replace(ret, pattern, string.Empty);
                    txt = ret;
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.RemoveMiddleInitial: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Extracts the office component from the LDAP location
        /// </summary>
        /// <param name="LDAPOfficeLocation"></param>
        /// <returns>string Office</returns>
        public static string ExtractOffice(string LDAPOfficeLocation)
        {
            #region implementation

            string[] office;
            int officeLen;

            //set office
            office = LDAPOfficeLocation.Split('/');
            officeLen = office.Length;
            if (officeLen > 2)
            {
                return office[(office.Length - 2)];
            }
            else if (officeLen >= 1)
            {
                return office.First();
            }
            else
            {
                return null;
            }

            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Extracts the division component from the LDAP location
        /// </summary>
        /// <param name="LDAPOfficeLocation"></param>
        /// <returns>string Office</returns>
        public static string ExtractDivision(string LDAPOfficeLocation)
        {
            #region implementation

            string[] office;
            int officeLen;

            //set office
            office = LDAPOfficeLocation.Split('/');
            officeLen = office.Length;
            if (officeLen >= 1)
            {
                return office.Last();
            }
            else
            {
                return null;
            }
            #endregion
        }


        /******************************************************/
        /// <summary>
        /// Takes a generic object and converts its data to a multi-line
        /// csv string. e.g. TextUtil.ToCsv(items, ",") where items is
        /// some sort of generic list.
        /// </summary>
        /// <typeparam name="T">Optional</typeparam>
        /// <param name="objectlist">The object to be converted to csv</param>
        /// <param name="separator">You can use a "," or some other delimiter. Default is ","</param>
        /// <param name="header">Do you want to have the feild names on line 1? Default is true</param>
        /// <returns>IEnumerable string object</returns>
        public static IEnumerable<string> ToCsv<T>(IEnumerable<T> objectlist, string separator = ",", bool header = true)
        {
            #region implementation
            FieldInfo[] fields = typeof(T).GetFields();
            PropertyInfo[] properties = typeof(T).GetProperties();
            string str1;
            string str2;

            if (header)
            {
                str1 = String.Join(separator, fields.Select(f => f.Name).Concat(properties.Select(p => p.Name)).ToArray());
                str1 = str1 + Environment.NewLine;
                yield return str1;
            }
            foreach (var o in objectlist)
            {

                //empty value throws mismatch param exception
                if (Convert.ToString(o) != "")
                {
                    //regex is to remove any misplaced returns or tabs that would
                    //really mess up a csv conversion.
                    str2 = string.Join(separator, fields.Select(f => (Regex.Replace(Convert.ToString(f.GetValue(o)), @"(?!\.)(?!-)(?!\/)(?!\\)([\t|\r|\n|\W+])", @" ") ?? "").Trim())
                       .Concat(properties.Select(p => (Regex.Replace(Convert.ToString(p.GetValue(o, null)), @"(?!\.)(?!-)(?!\/)(?!\\)([\t|\r|\n|\W+])", @" ") ?? "").Trim())).ToArray());
                }
                else
                {
                    str2 = separator;
                }

                str2 = str2 + Environment.NewLine;
                yield return str2;
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Converts XML text to a csv string
        /// </summary>
        /// <param name="xmlString">The XML to be converted</param>
        /// <param name="valuesFileName">(Disabled) uncomment in method to enable</param>
        /// <param name="isTabDelimited">Whether you want to have it be delimited by a Tab</param>
        /// <returns>String</returns>
        public static string ToCsvFromXml(string xmlString, string valuesFileName, bool isTabDelimited)
        {
            #region implementation
            XDocument xDoc = XDocument.Parse(xmlString);

            string xmlDataString;

            var tabsNeededList = new List<int>(); // only used for TabDelimited file

            string delimiter = isTabDelimited
                ? "\t"
                : ",";

            // Get title row 
            var titlesList = xDoc.Root
                .Elements()
                .First()
                .Elements()
                .Select(s => s.Name.LocalName)
                .ToList();

            // Get the values
            var masterValuesList = xDoc.Root
                .Elements()
                .Select(e => e
                    .Elements()
                    .Select(c => c.Value)
                    .ToList())
                .ToList();

            // Add titles as first row in master values list
            masterValuesList.Insert(0, titlesList);

            // For tab delimited, we need to figure out the number of tabs
            // needed to keep the file uniform, for each column
            if (isTabDelimited)
            {
                for (var i = 0; i < titlesList.Count; i++)
                {
                    int maxLength =
                        masterValuesList
                            .Select(vl => vl[i].Length)
                            .Max();

                    // assume tab is 4 characters
                    int rem;
                    int tabsNeeded = Math.DivRem(maxLength, TAB_SPACES, out rem);
                    tabsNeededList.Add(tabsNeeded);
                }
            }

            // Write the file
            //using(var fs = new FileStream(valuesFileName, FileMode.Create))
            //using(var sw = new StreamWriter(fs))
            using (StringWriter textWriter = new StringWriter())
            {
                foreach (var values in masterValuesList)
                {
                    string line = string.Empty;

                    foreach (var value in values)
                    {
                        line += value;
                        if (titlesList.IndexOf(value) < titlesList.Count - 1)
                        {
                            if (isTabDelimited)
                            {
                                int rem;
                                int tabsUsed = Math.DivRem(value.Length, TAB_SPACES, out rem);
                                int tabsLeft = tabsNeededList[values.IndexOf(value)] - tabsUsed + 1; // one tab is always needed!

                                for (var i = 0; i < tabsLeft; i++)
                                {
                                    line += delimiter;
                                }
                            }
                            else // comma delimited
                            {
                                line += delimiter;
                            }
                        }
                    }

                    textWriter.WriteLine(line);
                    //sw.WriteLine(line);
                }

                xmlDataString
                   = textWriter.ToString();
            }

            return xmlDataString;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Converts string to base64
        /// </summary>
        /// <param name="plainText"></param>
        /// <param name="isChecked">(Default: false) Attempts to determine if 
        /// the string is already encoded. False positives can values 
        /// e.g. input 'Category'</param>
        /// <returns></returns>
        public static string Base64Encode(this string plainText, bool isChecked)
        {
            #region implementation
            int num;
            try
            {

                if (!string.IsNullOrEmpty(plainText))
                {
                    if (isChecked)
                    {
                        if (!plainText.isBase64())
                        {
                            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                            plainText = System.Convert.ToBase64String(plainTextBytes);
                        }
                        else if (int.TryParse(plainText, out num))
                        {
                            //if the text is an integer then encode it
                            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                            plainText = System.Convert.ToBase64String(plainTextBytes);
                        }
                    }
                    else
                    {
                        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                        plainText = System.Convert.ToBase64String(plainTextBytes);
                    }

                    return plainText;
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.Base64Encode: " + e);
            }

            return string.Empty;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Converts an integer to a Base64 string. This used for
        /// masking integer id values e.g. FileID converted to to FileCode
        /// </summary>
        /// <param name="integer"></param>
        /// <returns></returns>
        public static string Base64Encode(this int integer)
        {
            #region implementation
            string ret = string.Empty;

            try
            {
                //convert integer to string
                ret = integer.ToString();

                //get the bytes
                byte[] plainTextBytes = System.Text.Encoding.UTF8.GetBytes(ret);

                //encode the bytes
                ret = System.Convert.ToBase64String(plainTextBytes);

                return ret;
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.Base64Encode: " + e);
            }

            return string.Empty;
            #endregion
        }

        public static string Base64Encode(this string plainText)
        {
            return Base64Encode(plainText, false);
        }

        /******************************************************/
        /// <summary>
        /// Using regex, this checks to see if a string has
        /// already been encoded. This is to avoid accidental
        /// double encoding.
        /// 
        /// https://stackoverflow.com/questions/6309379/how-to-check-for-a-valid-base64-encoded-string
        /// </summary>
        /// <param name="base64"></param>
        /// <returns></returns>
        private static bool isBase64(this string base64)
        {
            #region implementation
            bool ret = false;

            try
            {
                base64 = base64.Trim();
                ret = (base64.Length % 4 == 0) && Regex.IsMatch(base64, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.isBase64: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Decodes base64 string
        /// </summary>
        /// <param name="base64EncodedData"></param>
        /// <returns></returns>
        public static string Base64Decode(this string base64EncodedData)
        {
            #region implementation
            string? caller = string.Empty;

            try
            {
                //get caller from stack trace
                caller = new StackTrace()?.GetFrame(1)?.GetMethod()?.Name;
            }
            catch (Exception)
            {
                ErrorHelper.AddErrorMsg("TextUtil.Base64Decode (info): Unable to get method caller");
            }
            try
            {
                base64EncodedData = base64EncodedData.Trim();

                byte[] base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);

                base64EncodedData = System.Text.Encoding.UTF8.GetString(base64EncodedBytes);

                return base64EncodedData;
            }
            catch (System.FormatException)
            {
                ErrorHelper.AddErrorMsg("TextUtil.Base64Decode (info): Incorrect Format (" + caller + ")");
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.Base64Decode: (" + caller + ") " + e);

            }

            return string.Empty;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Takes an IEnumberable object and returns a tagged string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objectlist"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static string ToXML<T>(this IEnumerable<T> objectlist, string tag)
        {
            #region implementation
            StringBuilder sb = new StringBuilder();
            string ret = null;

            try
            {
                sb.Append("<" + tag + "Root>");
                foreach (var o in objectlist)
                {
                    sb.Append("<" + tag + ">");
                    sb.Append(Convert.ToString(o));
                    sb.Append("</" + tag + ">");
                }
                sb.Append("</" + tag + "Root>");

                ret = sb.ToString();
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.ToXML: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Takes an HTML string and it helps to remove many
        /// string sequences that could be used in an XSS
        /// type attack. 
        /// </summary>
        /// <param name="htmlIn">This string (html)</param>
        /// <param name="baseUrl">Optional url param</param>
        /// <returns>Sanitized HTML string</returns>
        public static string? RemoveHtmlXss(this string htmlIn, string? baseUrl = null)
        {
            #region implementation
            string? ret = null;
            if (htmlIn == null)
            {
                return ret;
            }

            lock (_lock)
            {
                var sanitizer = _htmlSanitizer;
                ret = sanitizer.Sanitize(htmlIn, baseUrl!);
            }
            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Returns the file name from a URL
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string GetFileNameFromUrl(string url)
        {
            #region implementation
            string name;
            Uri SomeBaseUri = new Uri("http://canbeanything");

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                uri = new Uri(SomeBaseUri, url);
            }

            name = Path.GetFileName(uri.LocalPath);

            if (name == null || name == string.Empty)
            {
                name = url;
            }

            return name;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Converts string to title case
        /// </summary>
        /// <typeparam name="?"></typeparam>
        /// <param name="txt"></param>
        /// <returns></returns>
        public static string ToTitle(this string txt)
        {
            #region implementation

            string ret = "";

            TextInfo ti = CultureInfo.CurrentCulture.TextInfo;

            if (txt != null)
            {
                txt = ti.ToTitleCase(txt);
            }

            ret = txt;

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Returns a string representation of the relative
        /// time remaining between a start date and end date
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static String TimeRemainingPercent(DateTime start, DateTime end)
        {
            #region implementation
            //trap divide by zero when assigned and due are equal
            try
            {
                return Convert.ToString(
                   (100 * (end - DateTime.UtcNow).Days / (end - start).Days) < 0
                   ||
                   (100 * (end - DateTime.UtcNow).Days / (end - start).Days) > 100
                   ? 0
                   : (100 * (end - DateTime.UtcNow).Days / (end - start).Days));
            }
            catch
            {
                return Convert.ToString(0);
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Returns a string representation of the relative
        /// time elapsed between a start date and end date
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static String TimeElapsedPercent(DateTime start, DateTime end)
        {
            #region implementation
            //trap divide by zero when assigned and due are equal
            try
            {
                return Convert.ToString(
                   100 - ((100 * (end - DateTime.UtcNow).Days / (end - start).Days) < 0
                   ||
                   (100 * (end - DateTime.UtcNow).Days / (end - start).Days) > 100
                   ? 0
                   : (100 * (end - DateTime.UtcNow).Days / (end - start).Days)));
            }
            catch
            {
                return Convert.ToString(0);
            }
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Provides an abbreviation for supplied value. This was
        /// created primarily to support naming files for the controlled 
        /// document library.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetDocumentTypeAbbreviation(string value)
        {
            #region implementation
            string ret = "";
            try
            {
                switch (value.ToLower())
                {
                    case "charter":
                        ret = "CTR";
                        break;

                    case "checklist":
                    case "form":
                    case "flowchart":
                    case "work aid":
                        ret = "WAI";
                        break;

                    case "other":
                        ret = "OTHER";
                        break;

                    case "standard operating procedure":
                        ret = "SOP";
                        break;

                    case "template":
                        ret = "TEM";
                        break;

                    default:
                        ret = "OTHER";
                        break;
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.GetDocumentTypeAbbreviation: " + e);
            }
            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Makes sure a file path ends with a "\" character
        /// </summary>
        /// <param name="txt"></param>
        /// <returns></returns>
        public static string FixFilePath(this string txt)
        {
            #region implementation
            string ret = txt;

            try
            {
                if (!string.IsNullOrEmpty(txt)
                    && !txt.EndsWith(@"\"))
                {
                    ret = txt + @"\";
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.FixFilePath: " + e);
            }

            return ret;
            #endregion
        }

        /******************************************************/
        /// <summary>
        /// Makes sure a url path ends with a "/" character
        /// </summary>
        /// <param name="txt"></param>
        /// <returns></returns>
        public static string FixURLPath(this string txt)
        {
            #region implementation
            string ret = txt;

            try
            {
                if (!txt.EndsWith(@"/"))
                {
                    ret = txt + @"/";
                }
            }
            catch (Exception e)
            {
                ErrorHelper.AddErrorMsg("TextUtil.FixURLPath: " + e);
            }

            return ret;
            #endregion
        }



        #region openai.com generated methods
        //https://beta.openai.com/playground/p/XFlvDAbPLd9Qs6io6vAsq519
        //the methods in this block that don't have documentation
        //haven't been validated. OpenAI generated many useful
        //tests that went beyond the initial prompt.

        /******************************************************/
        /// <summary>
        /// Use this when you expect that a string may be JSON.
        /// Not all strings that start/end with braces or brackets
        /// is going to be JSON. This will help avoid NewtonSoft
        /// deserialization errors by helping you avoid passing 
        /// invalid strings.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        /// <remarks>For more complete answers see the below references:
        /// https://www.newtonsoft.com/jsonschema/help/html/ValidatingJson.htm
        /// https://stackoverflow.com/questions/14977848/how-to-make-sure-that-string-is-valid-json-using-json-net
        /// </remarks>
        public static bool IsJson(this string input)
        {
            input = input.Trim();
            return input.StartsWith("{") && input.EndsWith("}")
                   || input.StartsWith("[") && input.EndsWith("]");
        }

        /******************************************************/
        public static bool IsXml(this string input)
        {
            try
            {
                XDocument.Parse(input);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /******************************************************/
        public static bool IsUrl(this string input)
        {
            Uri uriResult;
            return Uri.TryCreate(input, UriKind.Absolute, out uriResult)
                   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        /******************************************************/
        public static bool IsEmail(this string input)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(input);
                return addr.Address == input;
            }
            catch
            {
                return false;
            }
        }

        /******************************************************/
        public static bool IsIpAddress(this string input)
        {
            IPAddress ip;
            return IPAddress.TryParse(input, out ip);
        }

        /******************************************************/
        public static bool IsMacAddress(this string input)
        {
            return Regex.IsMatch(input, @"([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})");
        }

        /******************************************************/
        public static bool IsGuid(this string input)
        {
            Guid guid;
            return Guid.TryParse(input, out guid);
        }

        /******************************************************/
        public static bool IsIsbn10(this string input)
        {
            return Regex.IsMatch(input, @"^(?:ISBN(?:-10)?:? )?(?=[0-9X]{10}$|(?=(?:[0-9]+[- ]){3})[- 0-9X]{13}$|97[89][0-9]{10}$|(?=(?:[0-9]+[- ]){4})[- 0-9]{17}$)(?:97[89][- ]?)?[0-9]{1,5}[- ]?[0-9]+[- ]?[0-9]+[- ]?[0-9X]$");
        }

        /******************************************************/
        public static bool IsIsbn13(this string input)
        {
            return Regex.IsMatch(input, @"^(?:ISBN(?:-13)?:? )?(?=[0-9]{13}$|(?=(?:[0-9]+[- ]){4})[- 0-9]{17}$)97[89][- ]?[0-9]{1,5}[- ]?[0-9]+[- ]?[0-9]+[- ]?[0-9]$");
        }

        /******************************************************/
        public static bool IsCreditCard(this string input)
        {
            return Regex.IsMatch(input, @"^(?:(4[0-9]{12}(?:[0-9]{3})?)|(5[1-5][0-9]{14})|(6(?:011|5[0-9]{2})[0-9]{12})|(3[47][0-9]{13})|(3(?:0[0-5]|[68][0-9])[0-9]{11})|((?:2131|1800|35[0-9]{3})[0-9]{11}))$");
        }

        /******************************************************/
        public static bool IsHexColor(this string input)
        {
            return Regex.IsMatch(input, @"^#?([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");
        }

        /******************************************************/
        public static bool IsRgbColor(this string input)
        {
            return Regex.IsMatch(input, @"^rgb\((((((((1?[1-9]?\d)|10\d|(2[0-4]\d)|25[0-5]),\s?)){2}|((((1?[1-9]?\d)|10\d|(2[0-4]\d)|25[0-5])\s)){2})((1?[1-9]?\d)|10\d|(2[0-4]\d)|25[0-5]))|((((([1-9]?\d(\.\d+)?)|100|(\.\d+))%,\s?){2}|((([1-9]?\d(\.\d+)?)|100|(\.\d+))%\s){2})(([1-9]?\d(\.\d+)?)|100|(\.\d+))%))\)$");
        }

        /******************************************************/
        public static bool IsHslColor(this string input)
        {
            return Regex.IsMatch(input, @"^hsl\(((((([12]?[1-9]?\d)|[12]0\d|(3[0-5]\d))(\.\d+)?)|(\.\d+))(deg)?|(0|0?\.\d+)turn|(([0-6](\.\d+)?)|(\.\d+))rad)((,\s?(([1-9]?\d(\.\d+)?)|100|(\.\d+))%){2})|(,\s?(([1-9]?\d(\.\d+)?)|100|(\.\d+))%)|(\))$");
        }

        #endregion

    }
}