using System.Xml.Linq;
using MedRecPro.Models;
using c = MedRecPro.Models.Constant;
using sc = MedRecPro.Models.SplConstants;

namespace MedRecPro.Helpers
{
    /**************************************************************/
    /// <summary>
    /// Provides extension methods for System.Xml.Linq.XElement to simplify
    /// the process of extracting data during XML parsing.
    /// </summary>
    /// <remarks>
    /// This static class contains extension methods that make XML parsing more convenient
    /// by providing shorthand methods for common operations like getting element values,
    /// attribute values, and navigating through nested elements with namespace support.
    /// </remarks>
    /// <seealso cref="XElement"/>
    /// <seealso cref="XName"/>
    /// <seealso cref="XNamespace"/>
    public static class XElementExtensions
    {
        #region implementation
        /// <summary>
        /// The XML namespace used for element lookups, derived from the constant configuration.
        /// </summary>
        /// <seealso cref="MedRecPro.Models.Constant"/>
        private static readonly XNamespace ns = c.XML_NAMESPACE;
        #endregion

        /**************************************************************/
        /// <summary>
        /// Gets the value of a direct child element.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E">The XName of the child element.</param>
        /// <returns>The child element's value, or null if the child does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root&gt;&lt;child&gt;value&lt;/child&gt;&lt;/root&gt;");
        /// string value = parent.GetSplElementVal(XName.Get("child"));
        /// // value will be "value"
        /// </code>
        /// </example>
        /// <seealso cref="XElement(XName)"/>
        /// <seealso cref="XElement.Value"/>
        public static string? GetSplElementVal(this XElement element, XName E)
        {
            #region implementation
            // Get the direct child element and return its value if it exists
            if (element != null && E != null)
                return element.Element(E)?.Value;

            return null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of a direct child element using a string name with the configured namespace.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E">The string name of the child element (namespace will be automatically applied).</param>
        /// <returns>The child element's value, or null if the child does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root xmlns='namespace'&gt;&lt;child&gt;value&lt;/child&gt;&lt;/root&gt;");
        /// string value = parent.GetSplElementVal("child");
        /// // value will be "value"
        /// </code>
        /// </example>
        /// <seealso cref="GetSplElementVal(XElement, XName)"/>
        /// <seealso cref="XNamespace"/>
        public static string? GetSplElementVal(this XElement element, string E)
        {
            #region implementation
            // Combine the configured namespace with the element name and delegate to the XName overload
            return GetSplElementVal(element, ns + E);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets a direct child element using a string name with the configured namespace.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E">The string name of the child element (namespace will be automatically applied).</param>
        /// <returns>The child XElement, or null if the child does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root xmlns='namespace'&gt;&lt;child&gt;value&lt;/child&gt;&lt;/root&gt;");
        /// XElement child = parent.GetSplElement("child");
        /// // child will be the XElement representing the child node
        /// </code>
        /// </example>
        /// <seealso cref="XElement(XName)"/>
        /// <seealso cref="XNamespace"/>
        public static XElement? GetSplElement(this XElement element, string E)
        {
            #region implementation
            // Combine the configured namespace with the element name and get the child element
            return element.Element(ns + E);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of a specified attribute from an element.
        /// </summary>
        /// <param name="element">The XElement containing the attribute.</param>
        /// <param name="attributeName">The XName of the attribute.</param>
        /// <returns>The attribute's value, or null if the attribute does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement element = XElement.Parse("&lt;root id='123'&gt;content&lt;/root&gt;");
        /// string id = element.GetAttrVal(XName.Get("id"));
        /// // id will be "123"
        /// </code>
        /// </example>
        /// <seealso cref="XElement.Attribute(XName)"/>
        /// <seealso cref="XAttribute.Value"/>
        public static string? GetAttrVal(this XElement element, XName attributeName)
        {
            #region implementation
            // Get the attribute and return its value if it exists
            return element.Attribute(attributeName)?.Value;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of the xsi:type attribute for an element, handling namespaces.
        /// </summary>
        /// <param name="element">The XElement to check.</param>
        /// <returns>The value of the xsi:type attribute, or null if not found.</returns>
        public static string? GetXsiType(this XElement element)
        {
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            return element.Attribute(xsi + "type")?.Value;
        }

        /**************************************************************/
        /// <summary>
        /// Gets the decimal value of a specified attribute from an element.
        /// </summary>
        public static decimal? GetAttrDecimal(this XElement element, XName attributeName)
        {
            #region implementation
            // Get the attribute and return its value if it exists
            return Convert.ToDecimal(element.Attribute(attributeName)?.Value);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of a specified attribute from a direct child element.
        /// This is a convenience method combining GetChild and GetAttributeValue.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E">The XName of the child element.</param>
        /// <param name="A">The XName of the attribute on the child element.</param>
        /// <returns>The attribute's value, or null if the child or attribute does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root&gt;&lt;child id='123'&gt;value&lt;/child&gt;&lt;/root&gt;");
        /// string id = parent.GetSplElementAttrVal(XName.Get("child"), XName.Get("id"));
        /// // id will be "123"
        /// </code>
        /// </example>
        /// <seealso cref="XElement(XName)"/>
        /// <seealso cref="XElement.Attribute(XName)"/>
        /// <seealso cref="XAttribute.Value"/>
        public static string? GetSplElementAttrVal(this XElement element, XName E, XName A)
        {
            #region implementation
            // Get the child element, then get the attribute value if both exist
            return element.Element(E)?.Attribute(A)?.Value;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of a specified attribute from a direct child element using string names with the configured namespace.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E">The string name of the child element (namespace will be automatically applied).</param>
        /// <param name="A">The string name of the attribute on the child element.</param>
        /// <returns>The attribute's value, or null if the child or attribute does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root xmlns='namespace'&gt;&lt;child id='123'&gt;value&lt;/child&gt;&lt;/root&gt;");
        /// string id = parent.GetSplElementAttrVal("child", "id");
        /// // id will be "123"
        /// </code>
        /// </example>
        /// <seealso cref="GetSplElementAttrVal(XElement, XName, XName)"/>
        /// <seealso cref="XNamespace"/>
        public static string? GetSplElementAttrVal(this XElement element, string E, string A)
        {
            #region implementation
            // Apply namespace to element name and delegate to the XName overload
            return GetSplElementAttrVal(element, ns + E, A);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Navigates through a hierarchy of nested elements using the configured namespace.
        /// </summary>
        /// <param name="element">The root XElement to start navigation from.</param>
        /// <param name="elementNames">Array of element names representing the path to navigate.</param>
        /// <returns>The target XElement at the end of the path, or null if any element in the path does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement root = XElement.Parse("&lt;root xmlns='ns'&gt;&lt;level1&gt;&lt;level2&gt;value&lt;/level2&gt;&lt;/level1&gt;&lt;/root&gt;");
        /// XElement target = root.SplElement("level1", "level2");
        /// // target will be the level2 XElement
        /// </code>
        /// </example>
        /// <remarks>
        /// This method is useful for navigating deep XML structures without having to chain multiple Element() calls.
        /// If any element in the path doesn't exist, the method returns null immediately.
        /// </remarks>
        /// <seealso cref="XElement(XName)"/>
        /// <seealso cref="XNamespace"/>
        public static XElement? SplElement(this XElement element, params string[] elementNames)
        {
            #region implementation
            // Return null if any required parameters are missing or invalid
            if (element == null || elementNames == null || elementNames.Length == 0)
                return null;

            XElement? current = element;

            // Navigate through each element in the path
            foreach (var name in elementNames)
            {
                // Apply namespace and get the next element in the path
                current = current?.Element(ns + name);
                // If any element in the path doesn't exist, break early
                if (current == null) break;
            }

            return current;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets all elements that match a hierarchical path using the configured namespace.
        /// </summary>
        /// <param name="element">The root XElement to start searching from.</param>
        /// <param name="elementNames">Array of element names representing the path hierarchy to match.</param>
        /// <returns>An enumerable collection of XElements that match the specified path, or an empty sequence if no matches are found.</returns>
        /// <example>
        /// <code>
        /// XElement root = XElement.Parse("&lt;root xmlns='ns'&gt;&lt;items&gt;&lt;item&gt;1&lt;/item&gt;&lt;item&gt;2&lt;/item&gt;&lt;/items&gt;&lt;/root&gt;");
        /// IEnumerable&lt;XElement&gt; items = root.SplElements("items", "item");
        /// // items will contain both item elements
        /// </code>
        /// </example>
        /// <remarks>
        /// This method is useful for finding all elements that match a specific path pattern in XML documents.
        /// Unlike SplElement, this method returns all matching elements rather than just the first one.
        /// </remarks>
        /// <seealso cref="XElement(XName)"/>
        /// <seealso cref="XNamespace"/>
        /// <seealso cref="Enumerable.SelectMany{TSource, TResult}(IEnumerable{TSource}, Func{TSource, IEnumerable{TResult}})"/>
        public static IEnumerable<XElement> SplElements(this XElement element, params string[] elementNames)
        {
            #region implementation
            // Return empty sequence if any required parameters are missing or invalid
            if (element == null || elementNames == null || elementNames.Length == 0)
                return XElement.EmptySequence;

            // Start with the root element as the initial collection
            IEnumerable<XElement> currentElements = new[] { element };

            // Process each element name in the path
            foreach (var name in elementNames)
            {
                // For each current element, get all child elements with the specified name
                currentElements = currentElements
                    .SelectMany(e => e.Elements(ns + name));

                // If no elements match at this level, return empty sequence early
                if (!currentElements.Any())
                    return XElement.EmptySequence; // No matches at this level
            }

            // Return the final collection of matching elements, or empty sequence if none found
            return currentElements.Any() ? currentElements : XElement.EmptySequence;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches for XElements within the current element's descendants that match the specified search term.
        /// Performs case-insensitive matching against both element names and element values.
        /// </summary>
        /// <param name="element">The root XElement to search within.</param>
        /// <param name="search">The search term to match against element names and values.</param>
        /// <returns>IEnumerable of XElements that contain the search term in their name or value.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        public static IEnumerable<XElement> SplFindElements(this XElement element, string search)
        {
            #region implementation
            // Search through all descendant elements for matches in name or value
            return element.Descendants()
                .Where(e =>
                    // Check if element name contains the search term (case-insensitive)
                    e.Name.LocalName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    // Check if element value contains the search term (case-insensitive)
                    e.Value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                );
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recursively builds a tree of supported SPL content blocks from within a section's text element.
        /// Supported blocks include [paragraph], [list], [table], [renderMultimedia], [excerpt], and [highlight].
        /// This preserves the document order and nested structure for round-trip fidelity.
        /// </summary>
        /// <param name="textEl">The [text] XElement to search for nested content blocks.</param>
        /// <returns>A list of XElement trees representing the top-level blocks and their nested children.</returns>
        /// <example>
        /// var tree = sectionTextEl.SplBuildSectionContentTree();
        /// </example>
        /// <remarks>
        /// This method is useful when preserving the original nesting of highlights and excerpts.
        /// Use when reconstructing the section content hierarchy for SPL serialization.
        /// </remarks>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        public static List<XElement> SplBuildSectionContentTree(this XElement textEl)
        {
            #region implementation

            // Define the set of supported content block types
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                sc.E.Paragraph, sc.E.List, sc.E.Table, sc.E.RenderMultimedia, sc.E.Excerpt, sc.E.Highlight
            };

            // Recursive helper to extract tree structure
            List<XElement> extractBlocks(XElement parent)
            {
                var blocks = new List<XElement>();

                foreach (var el in parent.Elements())
                {
                    // Only include allowed block types
                    if (allowed.Contains(el.Name.LocalName))
                    {
                        // Clone the element to avoid modifying original DOM
                        var cloned = new XElement(el.Name, el.Attributes());

                        // Recursively process nested children
                        var childBlocks = extractBlocks(el);

                        if (childBlocks.Any())
                            cloned.Add(childBlocks);

                        blocks.Add(cloned);
                    }
                    else
                    {
                        // Dive deeper to find allowed nested content within non-matching wrappers
                        blocks.AddRange(extractBlocks(el));
                    }
                }

                return blocks;
            }

            return extractBlocks(textEl);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Searches for ingredient-related XElements while excluding elements that contain specified exclusion terms.
        /// Performs case-insensitive matching for both ingredient detection and exclusion filtering.
        /// </summary>
        /// <param name="element">The root XElement to search within.</param>
        /// <param name="excludingFieldsContaining">Text to exclude from results (elements containing this text will be filtered out).</param>
        /// <returns>IEnumerable of XElements that contain ingredient information but do not contain the exclusion term.</returns>
        /// <seealso cref="XElement"/>
        /// <seealso cref="Label"/>
        public static IEnumerable<XElement> SplFindIngredients(this XElement element, string excludingFieldsContaining)
        {
            #region implementation
            // Case-insensitive comparison for both search and exclusion
            return element.Descendants()
                .Where(e =>
                    (
                        // Check if element name contains "ingredient" (case-insensitive)
                        e.Name.LocalName.IndexOf(sc.E.Ingredient, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        // Check if element value contains "ingredient" (case-insensitive)
                        e.Value.IndexOf(sc.E.Ingredient, StringComparison.OrdinalIgnoreCase) >= 0
                    )
                    &&
                    (
                        // Exclude elements where the name or value contains the exclusion string
                        // Ensure element name does not contain exclusion term
                        e.Name.LocalName.IndexOf(excludingFieldsContaining, StringComparison.OrdinalIgnoreCase) < 0 &&
                        // Ensure element value does not contain exclusion term
                        e.Value.IndexOf(excludingFieldsContaining, StringComparison.OrdinalIgnoreCase) < 0
                    )
                );
            #endregion
        }

    }
}