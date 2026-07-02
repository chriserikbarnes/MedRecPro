using MedRecPro.Helpers;
using Microsoft.AspNetCore.Html;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Encodings.Web;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Exercises public SPL template helper methods with deterministic inputs.
    /// </summary>
    /// <remarks>
    /// These tests cover XML attribute, date, GUID, boolean, numeric, and safe
    /// lookup formatting without invoking RazorLight template rendering.
    /// </remarks>
    /// <seealso cref="SplTemplateHelpers"/>
    [TestClass]
    public class SplTemplateHelperTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies XML attribute helpers trim, escape, and preserve pre-encoded values.
        /// </summary>
        /// <seealso cref="SplTemplateHelpers.Attribute"/>
        /// <seealso cref="SplTemplateHelpers.AttributePreEncoded"/>
        [TestMethod]
        public void Attribute_AttributePreEncoded_CommonValues_ReturnExpectedXml()
        {
            #region implementation
            var escaped = render(SplTemplateHelpers.Attribute("displayName", " A&B <C> "));
            var preEncoded = render(SplTemplateHelpers.AttributePreEncoded("displayName", "A&amp;B"));
            var empty = render(SplTemplateHelpers.Attribute("displayName", " "));

            Assert.AreEqual("displayName=\"A&amp;B &lt;C&gt;\"", escaped);
            Assert.AreEqual("displayName=\"A&amp;B\"", preEncoded);
            Assert.AreEqual(string.Empty, empty);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies safe attribute helpers resolve dictionary and nested object values.
        /// </summary>
        /// <seealso cref="SplTemplateHelpers.SafeAttribute(string, IDictionary{string, object?}, string)"/>
        /// <seealso cref="SplTemplateHelpers.SafeAttribute(string, object?, string)"/>
        [TestMethod]
        public void SafeAttribute_DictionaryAndNestedObject_ReturnsEscapedAttributes()
        {
            #region implementation
            var dictionary = new Dictionary<string, object?>
            {
                ["DisplayName"] = "Ada & Co"
            };
            var source = new SampleTemplateSource
            {
                Child = new SampleTemplateChild { Code = "C<1>" }
            };

            var dictionaryAttribute = render(SplTemplateHelpers.SafeAttribute("name", dictionary, "displayName"));
            var nestedAttribute = render(SplTemplateHelpers.SafeAttribute("code", source, "Child.Code"));
            var missingAttribute = render(SplTemplateHelpers.SafeAttribute("missing", source, "Child.Missing"));

            Assert.AreEqual("name=\"Ada &amp; Co\"", dictionaryAttribute);
            Assert.AreEqual("code=\"C&lt;1&gt;\"", nestedAttribute);
            Assert.AreEqual(string.Empty, missingAttribute);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies safe dictionary lookup and date attribute formatting.
        /// </summary>
        /// <seealso cref="SplTemplateHelpers.SafeGet"/>
        /// <seealso cref="SplTemplateHelpers.SafeAttributeDateTime"/>
        /// <seealso cref="SplTemplateHelpers.GetAvailableKeys"/>
        /// <seealso cref="SplTemplateHelpers.ToSplDate"/>
        [TestMethod]
        public void SafeGet_DateHelpers_ResolveKeysAndFormatDates()
        {
            #region implementation
            var dictionary = new Dictionary<string, object?>
            {
                ["effectiveTime"] = new DateTime(2026, 7, 2),
                ["Zulu"] = 1,
                ["Alpha"] = 2
            };
            var keyDictionary = new Dictionary<string, object>
            {
                ["effectiveTime"] = new DateTime(2026, 7, 2),
                ["Zulu"] = 1,
                ["Alpha"] = 2
            };

            var dateAttribute = render(SplTemplateHelpers.SafeAttributeDateTime("value", dictionary, "EffectiveTime"));

            Assert.AreEqual(new DateTime(2026, 7, 2), SplTemplateHelpers.SafeGet(dictionary, "EffectiveTime"));
            Assert.AreEqual("Alpha, effectiveTime, Zulu", SplTemplateHelpers.GetAvailableKeys(keyDictionary));
            Assert.AreEqual("null", SplTemplateHelpers.GetAvailableKeys(null));
            Assert.AreEqual("value=\"20260702\"", dateAttribute);
            Assert.AreEqual("20260702", SplTemplateHelpers.ToSplDate("2026-07-02"));
            Assert.AreEqual(string.Empty, SplTemplateHelpers.ToSplDate(null));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies GUID, optional attribute, boolean, numeric, XML, and NDC helpers.
        /// </summary>
        /// <seealso cref="SplTemplateHelpers.GuidUp(Guid?)"/>
        /// <seealso cref="SplTemplateHelpers.GuidDown"/>
        /// <seealso cref="SplTemplateHelpers.GuidUp(string?)"/>
        /// <seealso cref="SplTemplateHelpers.AttrOrNull"/>
        /// <seealso cref="SplTemplateHelpers.BoolToSplFormat(bool?)"/>
        /// <seealso cref="SplTemplateHelpers.BoolToSplFormat(string?)"/>
        /// <seealso cref="SplTemplateHelpers.FormatNumeric(decimal?)"/>
        /// <seealso cref="SplTemplateHelpers.FormatNumeric(double?)"/>
        /// <seealso cref="SplTemplateHelpers.FormatNumeric(int?)"/>
        /// <seealso cref="SplTemplateHelpers.EscapeXmlContent"/>
        /// <seealso cref="SplTemplateHelpers.FormatNdcCode"/>
        [TestMethod]
        public void FormattingHelpers_CommonValues_ReturnExpectedStrings()
        {
            #region implementation
            var guid = Guid.Parse("53566d4f-ff40-4815-b922-3416cde56fb1");

            Assert.AreEqual("53566D4F-FF40-4815-B922-3416CDE56FB1", SplTemplateHelpers.GuidUp(guid));
            Assert.AreEqual("53566d4f-ff40-4815-b922-3416cde56fb1", SplTemplateHelpers.GuidDown(guid));
            Assert.AreEqual("53566D4F-FF40-4815-B922-3416CDE56FB1", SplTemplateHelpers.GuidUp(guid.ToString()));
            Assert.AreEqual(string.Empty, SplTemplateHelpers.GuidUp("not-a-guid"));
            Assert.AreEqual("root=\"A&amp;B\"", SplTemplateHelpers.AttrOrNull("root", "A&B"));
            Assert.AreEqual(string.Empty, SplTemplateHelpers.AttrOrNull("root", " "));
            Assert.AreEqual("true", SplTemplateHelpers.BoolToSplFormat(true));
            Assert.AreEqual("false", SplTemplateHelpers.BoolToSplFormat("no"));
            Assert.AreEqual(string.Empty, SplTemplateHelpers.BoolToSplFormat("maybe"));
            Assert.AreEqual("12.5", SplTemplateHelpers.FormatNumeric(12.500m));
            Assert.AreEqual("12.5", SplTemplateHelpers.FormatNumeric(12.5d));
            Assert.AreEqual("12", SplTemplateHelpers.FormatNumeric(12));
            Assert.AreEqual("A&lt;B&amp;C", SplTemplateHelpers.EscapeXmlContent("A<B&C"));
            Assert.AreEqual("50090-5373", SplTemplateHelpers.FormatNdcCode("50090-5373"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Renders an <see cref="HtmlString"/> to text for assertions.
        /// </summary>
        /// <param name="htmlString">The HTML string returned by a template helper.</param>
        /// <returns>The rendered text.</returns>
        /// <seealso cref="HtmlString"/>
        private static string render(HtmlString htmlString)
        {
            #region implementation
            using var writer = new StringWriter();
            htmlString.WriteTo(writer, HtmlEncoder.Default);
            return writer.ToString();
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Simple source object for nested property lookup tests.
        /// </summary>
        private sealed class SampleTemplateSource
        {
            /**************************************************************/
            /// <summary>
            /// Gets or sets the child object.
            /// </summary>
            public SampleTemplateChild? Child { get; set; }
        }

        /**************************************************************/
        /// <summary>
        /// Simple child object for nested property lookup tests.
        /// </summary>
        private sealed class SampleTemplateChild
        {
            /**************************************************************/
            /// <summary>
            /// Gets or sets the sample code.
            /// </summary>
            public string? Code { get; set; }
        }

        #endregion
    }
}
