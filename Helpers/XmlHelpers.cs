using MedRecPro.Models;
using System.Xml.Linq;
using static MedRecPro.Models.Label;
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using sc = MedRecPro.Models.SplConstants;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
#pragma warning disable CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.
using c = MedRecPro.Models.Constant;
#pragma warning restore CS8981 // The type name only contains lower-cased ascii characters. Such names may become reserved for the language.


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
        /// Creates a deep clone of an <see cref="XNode"/> without losing
        /// comments, CDATA, or processing instructions.
        /// </summary>
        /// <param name="node">The node to clone.</param>
        /// <returns>A deep-cloned copy of the node.</returns>
        /// <remarks>
        /// This uses [see cref="XNode.CreateReader"/] internally to produce
        /// a fresh instance without affecting the original.
        /// </remarks>
        /// <example>
        /// <code>
        /// var clone = myElement.CloneNode();
        /// </code>
        /// </example>
        public static XNode CloneNode(this XNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            return XNode.ReadFrom(node.CreateReader());
        }

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
        /// Gets the value of a specified attribute from a nested child element path with 3 levels.
        /// This method navigates through multiple levels of child elements before getting the attribute.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E1">The string name of the first child element (namespace will be automatically applied).</param>
        /// <param name="E2">The string name of the second child element (namespace will be automatically applied).</param>
        /// <param name="E3">The string name of the third child element (namespace will be automatically applied).</param>
        /// <param name="A">The string name of the attribute on the nested child element.</param>
        /// <returns>The attribute's value, or null if any element in the path or the attribute does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root xmlns='namespace'&gt;&lt;subjectOf&gt;&lt;approval&gt;&lt;code value='123'/&gt;&lt;/approval&gt;&lt;/subjectOf&gt;&lt;/root&gt;");
        /// string value = parent.GetSplElementAttrVal("subjectOf", "approval", "code", "value");
        /// // value will be "123"
        /// </code>
        /// </example>
        /// <seealso cref="GetSplElementAttrVal(XElement, XName, XName, XName, XName)"/>
        /// <seealso cref="XNamespace"/>
        /// <seealso cref="Label"/>
        public static string? GetSplElementAttrVal(this XElement element, string E1, string E2, string E3, string A)
        {
            #region implementation
            // Apply namespace to all element names and delegate to the XName overload
            return GetSplElementAttrVal(element, ns + E1, ns + E2, ns + E3, A);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of a specified attribute from a nested child element path with 3 levels using XName parameters.
        /// This method navigates through multiple levels of child elements before getting the attribute.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E1">The XName of the first child element.</param>
        /// <param name="E2">The XName of the second child element (nested within E1).</param>
        /// <param name="E3">The XName of the third child element (nested within E2).</param>
        /// <param name="A">The XName of the attribute on the nested child element.</param>
        /// <returns>The attribute's value, or null if any element in the path or the attribute does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root&gt;&lt;subjectOf&gt;&lt;approval&gt;&lt;code value='123'/&gt;&lt;/approval&gt;&lt;/subjectOf&gt;&lt;/root&gt;");
        /// string value = parent.GetSplElementAttrVal(XName.Get("subjectOf"), XName.Get("approval"), XName.Get("code"), XName.Get("value"));
        /// // value will be "123"
        /// </code>
        /// </example>
        /// <seealso cref="XElement(XName)"/>
        /// <seealso cref="XElement.Attribute(XName)"/>
        /// <seealso cref="XAttribute.Value"/>
        /// <seealso cref="Label"/>
        public static string? GetSplElementAttrVal(this XElement element, XName E1, XName E2, XName E3, XName A)
        {
            #region implementation
            // Navigate through the nested element path: element -> E1 -> E2 -> E3, then get the attribute value
            return element.Element(E1)?.Element(E2)?.Element(E3)?.Attribute(A)?.Value;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of a specified attribute from a nested child element path with 4 levels.
        /// This method navigates through multiple levels of child elements before getting the attribute.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E1">The string name of the first child element (namespace will be automatically applied).</param>
        /// <param name="E2">The string name of the second child element (namespace will be automatically applied).</param>
        /// <param name="E3">The string name of the third child element (namespace will be automatically applied).</param>
        /// <param name="E4">The string name of the fourth child element (namespace will be automatically applied).</param>
        /// <param name="A">The string name of the attribute on the nested child element.</param>
        /// <returns>The attribute's value, or null if any element in the path or the attribute does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root xmlns='namespace'&gt;&lt;subjectOf&gt;&lt;approval&gt;&lt;effectiveTime&gt;&lt;high value='20251231'/&gt;&lt;/effectiveTime&gt;&lt;/approval&gt;&lt;/subjectOf&gt;&lt;/root&gt;");
        /// string value = parent.GetSplElementAttrVal("subjectOf", "approval", "effectiveTime", "high", "value");
        /// // value will be "20251231"
        /// </code>
        /// </example>
        /// <seealso cref="GetSplElementAttrVal(XElement, XName, XName, XName, XName, XName)"/>
        /// <seealso cref="XNamespace"/>
        /// <seealso cref="Label"/>
        public static string? GetSplElementAttrVal(this XElement element, string E1, string E2, string E3, string E4, string A)
        {
            #region implementation
            // Apply namespace to all element names and delegate to the XName overload
            return GetSplElementAttrVal(element, ns + E1, ns + E2, ns + E3, ns + E4, A);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of a specified attribute from a nested child element path with 4 levels using XName parameters.
        /// This method navigates through multiple levels of child elements before getting the attribute.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E1">The XName of the first child element.</param>
        /// <param name="E2">The XName of the second child element (nested within E1).</param>
        /// <param name="E3">The XName of the third child element (nested within E2).</param>
        /// <param name="E4">The XName of the fourth child element (nested within E3).</param>
        /// <param name="A">The XName of the attribute on the nested child element.</param>
        /// <returns>The attribute's value, or null if any element in the path or the attribute does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root&gt;&lt;subjectOf&gt;&lt;approval&gt;&lt;effectiveTime&gt;&lt;high value='20251231'/&gt;&lt;/effectiveTime&gt;&lt;/approval&gt;&lt;/subjectOf&gt;&lt;/root&gt;");
        /// string value = parent.GetSplElementAttrVal(XName.Get("subjectOf"), XName.Get("approval"), XName.Get("effectiveTime"), XName.Get("high"), XName.Get("value"));
        /// // value will be "20251231"
        /// </code>
        /// </example>
        /// <seealso cref="XElement(XName)"/>
        /// <seealso cref="XElement.Attribute(XName)"/>
        /// <seealso cref="XAttribute.Value"/>
        /// <seealso cref="Label"/>
        public static string? GetSplElementAttrVal(this XElement element, XName E1, XName E2, XName E3, XName E4, XName A)
        {
            #region implementation
            // Navigate through the nested element path: element -> E1 -> E2 -> E3 -> E4, then get the attribute value
            return element.Element(E1)?.Element(E2)?.Element(E3)?.Element(E4)?.Attribute(A)?.Value;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of a nested child element using a 2-level path with the configured namespace.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E1">The string name of the first child element (namespace will be automatically applied).</param>
        /// <param name="E2">The string name of the second child element (namespace will be automatically applied).</param>
        /// <returns>The nested child element's value, or null if any element in the path does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root xmlns='namespace'&gt;&lt;approval&gt;&lt;text&gt;Some text&lt;/text&gt;&lt;/approval&gt;&lt;/root&gt;");
        /// string value = parent.GetSplElementVal("approval", "text");
        /// // value will be "Some text"
        /// </code>
        /// </example>
        /// <seealso cref="GetSplElementVal(XElement, XName, XName)"/>
        /// <seealso cref="XNamespace"/>
        /// <seealso cref="Label"/>
        public static string? GetSplElementVal(this XElement element, string E1, string E2)
        {
            #region implementation
            // Apply namespace to both element names and delegate to the XName overload
            return GetSplElementVal(element, ns + E1, ns + E2);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of a nested child element using a 2-level path with XName parameters.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E1">The XName of the first child element.</param>
        /// <param name="E2">The XName of the second child element (nested within E1).</param>
        /// <returns>The nested child element's value, or null if any element in the path does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root&gt;&lt;approval&gt;&lt;text&gt;Some text&lt;/text&gt;&lt;/approval&gt;&lt;/root&gt;");
        /// string value = parent.GetSplElementVal(XName.Get("approval"), XName.Get("text"));
        /// // value will be "Some text"
        /// </code>
        /// </example>
        /// <seealso cref="XElement(XName)"/>
        /// <seealso cref="XElement.Value"/>
        /// <seealso cref="Label"/>
        public static string? GetSplElementVal(this XElement element, XName E1, XName E2)
        {
            #region implementation
            // Navigate through the nested element path: element -> E1 -> E2, then get the value
            return element.Element(E1)?.Element(E2)?.Value;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of a nested child element using a 3-level path with the configured namespace.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E1">The string name of the first child element (namespace will be automatically applied).</param>
        /// <param name="E2">The string name of the second child element (namespace will be automatically applied).</param>
        /// <param name="E3">The string name of the third child element (namespace will be automatically applied).</param>
        /// <returns>The nested child element's value, or null if any element in the path does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root xmlns='namespace'&gt;&lt;subjectOf&gt;&lt;approval&gt;&lt;text&gt;Some text&lt;/text&gt;&lt;/approval&gt;&lt;/subjectOf&gt;&lt;/root&gt;");
        /// string value = parent.GetSplElementVal("subjectOf", "approval", "text");
        /// // value will be "Some text"
        /// </code>
        /// </example>
        /// <seealso cref="GetSplElementVal(XElement, XName, XName, XName)"/>
        /// <seealso cref="XNamespace"/>
        /// <seealso cref="Label"/>
        public static string? GetSplElementVal(this XElement element, string E1, string E2, string E3)
        {
            #region implementation
            // Apply namespace to all element names and delegate to the XName overload
            return GetSplElementVal(element, ns + E1, ns + E2, ns + E3);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of a nested child element using a 3-level path with XName parameters.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E1">The XName of the first child element.</param>
        /// <param name="E2">The XName of the second child element (nested within E1).</param>
        /// <param name="E3">The XName of the third child element (nested within E2).</param>
        /// <returns>The nested child element's value, or null if any element in the path does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root&gt;&lt;subjectOf&gt;&lt;approval&gt;&lt;text&gt;Some text&lt;/text&gt;&lt;/approval&gt;&lt;/subjectOf&gt;&lt;/root&gt;");
        /// string value = parent.GetSplElementVal(XName.Get("subjectOf"), XName.Get("approval"), XName.Get("text"));
        /// // value will be "Some text"
        /// </code>
        /// </example>
        /// <seealso cref="XElement(XName)"/>
        /// <seealso cref="XElement.Value"/>
        /// <seealso cref="Label"/>
        public static string? GetSplElementVal(this XElement element, XName E1, XName E2, XName E3)
        {
            #region implementation
            // Navigate through the nested element path: element -> E1 -> E2 -> E3, then get the value
            return element.Element(E1)?.Element(E2)?.Element(E3)?.Value;
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
        /// Gets the value of a namespace-prefixed type attribute (like p3:type, p4:type, etc.).
        /// This handles dynamic namespace prefixes that appear in XML documents.
        /// </summary>
        /// <param name="element">The XElement to check.</param>
        /// <param name="typeAttributeName">The local name of the type attribute (default: "type").</param>
        /// <returns>The value of the type attribute, or null if not found.</returns>
        /// <example>
        /// <code>
        /// // For &lt;numerator xmlns:p3="http://www.w3.org/2001/XMLSchema-instance" p3:type="URG_PQ"&gt;
        /// string typeValue = element.GetNamespacePrefixedType(); // Returns "URG_PQ"
        /// </code>
        /// </example>
        public static string? GetNamespacePrefixedType(this XElement element, string typeAttributeName = "type")
        {
            #region implementation
            if (element == null) return null;

            // Look for any attribute with the local name "type" regardless of namespace
            var typeAttribute = element.Attributes()
                .FirstOrDefault(attr => attr.Name.LocalName == typeAttributeName);

            return typeAttribute?.Value;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts CDATA content from an element, returning the raw text without CDATA wrapper.
        /// Useful for chemical structure data and other embedded content.
        /// </summary>
        /// <param name="element">The XElement containing CDATA content.</param>
        /// <returns>The CDATA content as string, or null if no CDATA found.</returns>
        /// <example>
        /// <code>
        /// XElement chemicalStructure = element.GetSplElement("value");
        /// string molFile = chemicalStructure.GetCDataContent();
        /// // Returns the molecular structure data without CDATA wrapper
        /// </code>
        /// </example>
        public static string? GetCDataContent(this XElement element)
        {
            #region implementation
            if (element == null) return null;

            // Look for CDATA nodes within the element
            var cdataNode = element.Nodes()
                .OfType<XCData>()
                .FirstOrDefault();

            return cdataNode?.Value?.Trim();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the media type attribute value from an element.
        /// Commonly used for identifying content types like chemical structures, images, etc.
        /// </summary>
        /// <param name="element">The XElement to check for media type.</param>
        /// <returns>The media type value, or null if not found.</returns>
        /// <example>
        /// <code>
        /// string mediaType = valueElement.GetMediaType();
        /// // Returns "application/x-mdl-molfile" or "application/x-inchi", etc.
        /// </code>
        /// </example>
        public static string? GetMediaType(this XElement element)
        {
            #region implementation
            return element?.Attribute("mediaType")?.Value;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Safely extracts chemical structure data based on media type.
        /// Handles both CDATA and regular text content appropriately.
        /// </summary>
        /// <param name="element">The value element containing chemical data.</param>
        /// <returns>A tuple containing the media type and content, or null if invalid.</returns>
        /// <example>
        /// <code>
        /// var (mediaType, content) = valueElement.GetChemicalStructureData();
        /// if (mediaType == "application/x-mdl-molfile") {
        ///     // Handle MOL file data
        /// }
        /// </code>
        /// </example>
        public static (string mediaType, string content)? GetChemicalStructureData(this XElement element)
        {
            #region implementation
            if (element == null) return null;

            var mediaType = element.GetMediaType();
            if (string.IsNullOrEmpty(mediaType)) return null;

            // Try CDATA first, then regular content
            var content = element.GetCDataContent() ?? element.Value?.Trim();

            if (string.IsNullOrEmpty(content)) return null;

            return (mediaType, content);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the inclusive attribute value as a boolean.
        /// Used in quantity ranges and other numeric bounds.
        /// </summary>
        /// <param name="element">The XElement to check.</param>
        /// <returns>The inclusive value as boolean, or null if not found or invalid.</returns>
        /// <example>
        /// <code>
        /// bool? isInclusive = lowElement.GetInclusiveAttribute();
        /// // Returns true, false, or null
        /// </code>
        /// </example>
        public static bool? GetInclusiveAttribute(this XElement element)
        {
            #region implementation
            var inclusiveValue = element?.Attribute("inclusive")?.Value;

            if (string.IsNullOrEmpty(inclusiveValue)) return null;

            return bool.TryParse(inclusiveValue, out bool result) ? result : null;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets all moiety elements from an identified substance.
        /// Convenience method for accessing chemical compound components.
        /// </summary>
        /// <param name="identifiedSubstanceElement">The identifiedSubstance element.</param>
        /// <returns>Collection of moiety elements.</returns>
        /// <example>
        /// <code>
        /// var moieties = identifiedSubstanceElement.GetMoieties();
        /// foreach (var moiety in moieties) {
        ///     // Process each moiety component
        /// }
        /// </code>
        /// </example>
        public static IEnumerable<XElement> GetMoieties(this XElement identifiedSubstanceElement)
        {
            #region implementation
            if (identifiedSubstanceElement == null)
                return Enumerable.Empty<XElement>();

            return identifiedSubstanceElement.Elements(ns + "moiety");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Safely gets a unit attribute value from quantity-related elements.
        /// </summary>
        /// <param name="element">The element containing a unit attribute.</param>
        /// <returns>The unit value, or null if not found.</returns>
        /// <example>
        /// <code>
        /// string unit = numeratorElement.GetUnitAttribute();
        /// // Returns "mg", "mL", "1", etc.
        /// </code>
        /// </example>
        public static string? GetUnitAttribute(this XElement element)
        {
            #region implementation
            return element?.Attribute("unit")?.Value;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets characteristics of a specific type from a moiety or other subject element.
        /// </summary>
        /// <param name="element">The element to search within.</param>
        /// <param name="characteristicCode">The characteristic code to filter by (e.g., "C103240" for Chemical Structure).</param>
        /// <returns>Collection of matching characteristic elements.</returns>
        /// <example>
        /// <code>
        /// var chemicalStructures = moietyElement.GetCharacteristicsByCode("C103240");
        /// foreach (var structure in chemicalStructures) {
        ///     var structureData = structure.GetSplElement("value")?.GetChemicalStructureData();
        /// }
        /// </code>
        /// </example>
        public static IEnumerable<XElement> GetCharacteristicsByCode(this XElement element, string characteristicCode)
        {
            #region implementation
            if (element == null || string.IsNullOrEmpty(characteristicCode))
                return Enumerable.Empty<XElement>();

            return element.SplElements("subjectOf", "characteristic")
                .Where(c => c.GetSplElementAttrVal("code", "code") == characteristicCode);
            #endregion
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
        /// Gets the value of a specified attribute from a nested child element path.
        /// This method navigates through multiple levels of child elements before getting the attribute.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E1">The XName of the first child element.</param>
        /// <param name="E2">The XName of the second child element (nested within E1).</param>
        /// <param name="A">The XName of the attribute on the nested child element.</param>
        /// <returns>The attribute's value, or null if any element in the path or the attribute does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root&gt;&lt;effectiveTime&gt;&lt;low value='20230101'/&gt;&lt;/effectiveTime&gt;&lt;/root&gt;");
        /// string value = parent.GetSplElementAttrVal(XName.Get("effectiveTime"), XName.Get("low"), XName.Get("value"));
        /// // value will be "20230101"
        /// </code>
        /// </example>
        /// <seealso cref="XElement(XName)"/>
        /// <seealso cref="XElement.Attribute(XName)"/>
        /// <seealso cref="XAttribute.Value"/>
        /// <seealso cref="Label"/>
        public static string? GetSplElementAttrVal(this XElement element, XName E1, XName E2, XName A)
        {
            #region implementation
            // Navigate through the nested element path: element -> E1 -> E2, then get the attribute value
            return element.Element(E1)?.Element(E2)?.Attribute(A)?.Value;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Gets the value of a specified attribute from a nested child element path using string names with the configured namespace.
        /// </summary>
        /// <param name="element">The parent XElement.</param>
        /// <param name="E1">The string name of the first child element (namespace will be automatically applied).</param>
        /// <param name="E2">The string name of the second child element (namespace will be automatically applied).</param>
        /// <param name="A">The string name of the attribute on the nested child element.</param>
        /// <returns>The attribute's value, or null if any element in the path or the attribute does not exist.</returns>
        /// <example>
        /// <code>
        /// XElement parent = XElement.Parse("&lt;root xmlns='namespace'&gt;&lt;effectiveTime&gt;&lt;low value='20230101'/&gt;&lt;/effectiveTime&gt;&lt;/root&gt;");
        /// string value = parent.GetSplElementAttrVal("effectiveTime", "low", "value");
        /// // value will be "20230101"
        /// </code>
        /// </example>
        /// <seealso cref="GetSplElementAttrVal(XElement, XName, XName, XName)"/>
        /// <seealso cref="XNamespace"/>
        /// <seealso cref="Label"/>
        public static string? GetSplElementAttrVal(this XElement element, string E1, string E2, string A)
        {
            #region implementation
            // Apply namespace to both element names and delegate to the XName overload
            return GetSplElementAttrVal(element, ns + E1, ns + E2, A);
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
        /// <param name="parentEl">The [text] XElement to search for nested content blocks.</param>
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
        public static List<XElement> SplBuildSectionContentTree(this XElement parentEl)
        {
            #region implementation

            // Define the set of supported content block types
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                sc.E.Paragraph, sc.E.List, sc.E.Table, sc.E.RenderMultimedia,
                sc.E.Excerpt, sc.E.Highlight
            };

            // The only thing this method needs to do is return the direct child elements
            // that are valid content blocks. The recursive parsing is handled by the calling method.
            if (parentEl == null)
            {
                return new List<XElement>();
            }

            return parentEl.Elements()
                .Where(el => allowed.Contains(el.Name.LocalName))
                .ToList();

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

        /**************************************************************/
        /// <summary>
        /// Extracts the inner XML of a table cell (td or th), preserving all markup for rich content display.
        /// </summary>
        /// <param name="cellElement">The [td] or [th] XElement.</param>
        /// <returns>The inner XML as a string, or null if the input is null.</returns>
        /// <example>
        /// <code>
        /// XElement cell = XElement.Parse("&lt;td&gt;&lt;strong&gt;Bold Text&lt;/strong&gt;&lt;/td&gt;");
        /// string content = XElementExtensions.GetCellXml(cell);
        /// // content will be "&lt;strong&gt;Bold Text&lt;/strong&gt;"
        /// </code>
        /// </example>
        /// <remarks>This will add the namespace to the elements</remarks>
        /// <seealso cref="TextTableCell"/>
        /// <seealso cref="Label"/>
        public static string? GetCellXml(XElement? cellElement)
        {
            #region implementation
            // Return null for invalid input to handle edge cases gracefully
            if (cellElement == null) return null;

            // Using a reader is more robust for getting inner XML than the Nodes().ToString()
            // approach, as it's less susceptible to modifications of the in-memory XDocument.
            var reader = cellElement.CreateReader();
            reader.MoveToContent();
            return reader.ReadInnerXml().Trim();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the inner XML of a list [item] element, preserving all markup,
        /// but excluding the [caption] element itself. 
        /// </summary>
        /// <param name="itemElement">The [item] XElement to process.</param>
        /// <returns>The inner XML as a string, or null if the input is null.</returns>
        /// <example>
        /// <code>
        /// XElement item = XElement.Parse("&lt;item&gt;&lt;caption&gt;Title&lt;/caption&gt;&lt;em&gt;Content&lt;/em&gt;&lt;/item&gt;");
        /// string content = XElementExtensions.GetItemXml(item);
        /// // content will be "&lt;em&gt;Content&lt;/em&gt;" (caption excluded)
        /// </code>
        /// </example>
        /// <remarks>This will add the namespace to the elements</remarks>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="Label"/>
        public static string? GetItemXml(XElement? itemElement)
        {
            #region implementation
            if (itemElement == null) return null;

            // Create a temporary clone to manipulate without affecting the original XDocument tree.
            var clone = new XElement(itemElement);

            // Find and remove the <caption/> element from the clone, if it exists.
            clone.Element(itemElement.GetDefaultNamespace() + sc.E.Caption)?.Remove();

            // Concatenate the remaining nodes (including text and other elements/tags) into a single string.
            return string.Concat(clone.Nodes().Select(n => n.ToString())).Trim();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the inner HTML of an element, preserving all markup 
        /// without introducing line breaks. Removes insignificant whitespace
        /// while maintaining spaces between words.
        /// </summary>
        /// <param name="itemElement">The [item] XElement to process.</param>
        /// <param name="stripNamespaces">If true, removes namespace declarations and converts 
        /// name-spaced elements to their local names.</param>
        /// <returns>The inner XML as a string, or null if the input is null.</returns>
        /// <example>
        /// <code>
        /// XElement item = XElement.Parse("&lt;item&gt;&lt;caption&gt;Title&lt;/caption&gt;&lt;br xmlns='urn:hl7-org:v3' /&gt;&lt;em&gt;Content&lt;/em&gt;&lt;/item&gt;");
        /// string content = XElementExtensions.GetSplHtml(item, stripNamespaces: true);
        /// // content will be "&lt;br /&gt;&lt;em&gt;Content&lt;/em&gt;" (caption excluded, namespaces stripped)
        /// </code>
        /// </example>
        /// <seealso cref="TextListItem"/>
        /// <seealso cref="Label"/>
        public static string? GetSplHtml(this XElement itemElement, bool stripNamespaces)
        {
            #region implementation

            if (itemElement == null) return null;

            // Create a temporary clone to manipulate without affecting the original XDocument tree.
            var clone = new XElement(itemElement);

            // Normalize all text nodes to remove insignificant whitespace
            normalizeWhitespace(clone);

            if (stripNamespaces)
            {
                // Process each node to strip namespaces
                var processedNodes = clone.Nodes()
                    .Select(n => stripNamespacesFromNode(n));

                // return string with no line breaks
                return string.Concat(processedNodes
                    .Select(n => n.ToString(SaveOptions.DisableFormatting)))
                    .Trim();
            }
            else
            {
                // Concatenate the remaining nodes (including text and other elements/tags) into a single string.
                // returns without line breaks
                return string.Concat(clone.Nodes()
                    .Select(n => n.ToString(SaveOptions.DisableFormatting)))
                    .Trim();
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recursively normalizes whitespace in an XElement tree, removing
        /// insignificant whitespace while preserving spaces between words.
        /// </summary>
        /// <param name="element">The element to normalize.</param>
        /// <remarks>
        /// This method modifies the element in place by:
        /// - Removing whitespace-only text nodes between elements
        /// - Trimming leading/trailing whitespace from text nodes
        /// - Collapsing multiple consecutive spaces into a single space
        /// </remarks>
        /// <seealso cref="GetSplHtml"/>
        private static void normalizeWhitespace(XElement element)
        {
            #region implementation

            // Process child nodes
            var nodes = element.Nodes().ToList();

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is XText textNode)
                {
                    string normalized = System.Text.RegularExpressions.Regex.Replace(
                        textNode.Value,
                        @"\s+",
                        " "); // Collapse all whitespace to single space

                    // Check if this text node is between elements (structural whitespace)
                    bool isBetweenElements =
                        (i == 0 || nodes[i - 1] is XElement) &&
                        (i == nodes.Count - 1 || nodes[i + 1] is XElement);

                    if (isBetweenElements && string.IsNullOrWhiteSpace(normalized))
                    {
                        // Remove whitespace-only nodes between elements
                        textNode.Remove();
                    }
                    else
                    {
                        // Trim edges but keep internal spaces
                        if (i == 0)
                            normalized = normalized.TrimStart();
                        if (i == nodes.Count - 1)
                            normalized = normalized.TrimEnd();

                        textNode.Value = normalized;
                    }
                }
                else if (nodes[i] is XElement childElement)
                {
                    // Recursively process child elements
                    normalizeWhitespace(childElement);
                }
            }

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Recursively strips namespaces from an XNode and its descendants.
        /// </summary>
        /// <param name="node">The node to process.</param>
        /// <returns>A new node with namespaces removed.</returns>
        private static XNode stripNamespacesFromNode(XNode node)
        {
            #region implementation
            if (node is XElement element)
            {
                // Create a new element with just the local name (no namespace)
                var newElement = new XElement(element.Name.LocalName);

                // Copy attributes, but exclude namespace declarations
                foreach (var attr in element.Attributes())
                {
                    if (!attr.IsNamespaceDeclaration)
                    {
                        newElement.Add(new XAttribute(attr.Name.LocalName, attr.Value));
                    }
                }

                // Recursively process child nodes
                foreach (var childNode in element.Nodes())
                {
                    newElement.Add(stripNamespacesFromNode(childNode));
                }

                return newElement;
            }
            else if (node is XText textNode)
            {
                // Text nodes don't have namespaces, so just return a copy
                return new XText(textNode.Value);
            }
            else
            {
                // For other node types (comments, processing instructions, etc.), return as-is
                return node;
            } 

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Extracts the inner XML of a highlight text element, preserving all markup
        /// for rich content display and round-trip fidelity.
        /// </summary>
        /// <param name="textElement">The text XElement containing highlighted content.</param>
        /// <returns>The inner XML as a string with preserved markup, or null if element is null.</returns>
        /// <example>
        /// <code>
        /// XElement highlight = XElement.Parse("&lt;text&gt;Important &lt;em&gt;text&lt;/em&gt; here&lt;/text&gt;");
        /// string xml = XElementExtensions.GetHighlightXml(highlight);
        /// // xml will be "Important &lt;em&gt;text&lt;/em&gt; here"
        /// </code>
        /// </example>
        /// <remarks>
        /// This method preserves all markup within the highlight element, making it suitable
        /// for scenarios where the original formatting needs to be maintained for display
        /// or round-trip processing back to XML.
        /// </remarks>
        /// <seealso cref="SectionExcerptHighlight"/>
        /// <seealso cref="Label"/>
        public static string? GetHighlightXml(XElement? textElement)
        {
            #region implementation
            if (textElement == null)
                return null;

            // Return concatenated inner XML (preserving all tags/markup)
            return string.Concat(textElement.Nodes().Select(n => n.ToString())).Trim();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Safely extracts an attribute value from an XElement, returning null if the
        /// element or attribute doesn't exist, rather than throwing an exception.
        /// </summary>
        /// <param name="element">The XElement to extract the attribute from.</param>
        /// <param name="attributeName">The name of the attribute to extract.</param>
        /// <returns>The attribute value as a string, or null if not found.</returns>
        /// <example>
        /// <code>
        /// XElement elem = XElement.Parse("&lt;item id='123'&gt;Content&lt;/item&gt;");
        /// string id = SectionParserUtilities.SafeGetAttribute(elem, "id"); // "123"
        /// string missing = SectionParserUtilities.SafeGetAttribute(elem, "missing"); // null
        /// </code>
        /// </example>
        /// <remarks>
        /// This method provides a null-safe way to extract attribute values without
        /// the need for null-checking the element or handling exceptions when
        /// attributes don't exist.
        /// </remarks>
        public static string? SafeGetAttribute(this XElement? element, string attributeName)
        {
            #region implementation
            if (element == null || string.IsNullOrEmpty(attributeName)) return null;

            return element.Attribute(attributeName)?.Value;
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Safely extracts an attribute value from an XElement and attempts to parse it
        /// as an integer, returning null if parsing fails.
        /// </summary>
        /// <param name="element">The XElement to extract the attribute from.</param>
        /// <param name="attributeName">The name of the attribute to extract and parse.</param>
        /// <returns>The parsed integer value, or null if the attribute doesn't exist or parsing fails.</returns>
        /// <example>
        /// <code>
        /// XElement elem = XElement.Parse("&lt;item rowspan='3'&gt;Content&lt;/item&gt;");
        /// int? span = SectionParserUtilities.SafeGetIntAttribute(elem, "rowspan"); // 3
        /// int? missing = SectionParserUtilities.SafeGetIntAttribute(elem, "missing"); // null
        /// </code>
        /// </example>
        /// <remarks>
        /// This method combines attribute extraction and integer parsing in a safe way,
        /// making it useful for extracting numeric attributes like rowspan, colspan,
        /// or sequence numbers without exception handling.
        /// </remarks>
        public static int? SafeGetIntAttribute(this XElement? element, string attributeName)
        {
            #region implementation
            var attributeValue = SafeGetAttribute(element, attributeName);

            if (string.IsNullOrWhiteSpace(attributeValue)) return null;

            return int.TryParse(attributeValue, out int result) ? result : null;
            #endregion
        }

    }
}