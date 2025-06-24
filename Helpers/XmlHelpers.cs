
using System.Xml.Linq;

namespace MedRecPro.Helpers
{
    /// <summary>
    /// Provides extension methods for System.Xml.Linq.XElement to simplify
    /// the process of extracting data during XML parsing.
    /// </summary>
    public static class XElementExtensions
    {
        /// <summary>
        /// Gets the value of a direct child element.
        /// </summary>
        /// <param name="parent">The parent XElement.</param>
        /// <param name="childName">The XName of the child element.</param>
        /// <returns>The child element's value, or null if the child does not exist.</returns>
        public static string? GetChildVal(this XElement parent, XName childName)
        {
            return parent.Element(childName)?.Value;
        }

        /// <summary>
        /// Gets the value of a specified attribute from an element.
        /// </summary>
        /// <param name="element">The XElement containing the attribute.</param>
        /// <param name="attributeName">The XName of the attribute.</param>
        /// <returns>The attribute's value, or null if the attribute does not exist.</returns>
        public static string? GetAttrVal(this XElement element, XName attributeName)
        {
            return element.Attribute(attributeName)?.Value;
        }

        /// <summary>
        /// Gets the value of a specified attribute from a direct child element.
        /// This is a convenience method combining GetChild and GetAttributeValue.
        /// </summary>
        /// <param name="parent">The parent XElement.</param>
        /// <param name="childName">The XName of the child element.</param>
        /// <param name="attributeName">The XName of the attribute on the child element.</param>
        /// <returns>The attribute's value, or null if the child or attribute does not exist.</returns>
        public static string? GetChildAttrVal(this XElement parent, XName childName, XName attributeName)
        {
            return parent.Element(childName)?.Attribute(attributeName)?.Value;
        }
    }
}
