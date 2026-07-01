using MedRecPro.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Text;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Exercises public TextUtil helper methods with deterministic, pure inputs.
    /// </summary>
    /// <remarks>
    /// These tests cover the low-risk helper surface identified in the public method
    /// coverage plan without requiring database, network, or uploaded SPL fixtures.
    /// </remarks>
    /// <seealso cref="TextUtil"/>
    [TestClass]
    public class TextUtilTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies URL-safe Base64 conversion round trips arbitrary bytes.
        /// </summary>
        /// <seealso cref="TextUtil.ToUrlSafeBase64StringManual"/>
        /// <seealso cref="TextUtil.FromUrlSafeBase64StringManual"/>
        [TestMethod]
        public void ToUrlSafeBase64StringManual_FromUrlSafeBase64StringManual_RoundTripsBytes()
        {
            #region implementation
            var input = new byte[] { 0, 1, 2, 250, 251, 252, 253, 254, 255 };

            var encoded = TextUtil.ToUrlSafeBase64StringManual(input);
            var decoded = TextUtil.FromUrlSafeBase64StringManual(encoded);

            CollectionAssert.AreEqual(input, decoded);
            Assert.IsFalse(encoded.Contains('+'));
            Assert.IsFalse(encoded.Contains('/'));
            Assert.IsFalse(encoded.Contains('='));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies RemoveTags strips markup and decodes entities.
        /// </summary>
        /// <seealso cref="TextUtil.RemoveTags"/>
        [TestMethod]
        public void RemoveTags_HtmlWithEntities_ReturnsDecodedText()
        {
            #region implementation
            var html = "<p>Hello &amp; <strong>safe</strong></p>";

            var result = html.RemoveTags();

            Assert.AreEqual("Hello & safe", result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies RemoveUnwantedTags preserves allowed tags while removing wrappers.
        /// </summary>
        /// <seealso cref="TextUtil.RemoveUnwantedTags(string, List{string}, bool)"/>
        [TestMethod]
        public void RemoveUnwantedTags_PreserveParagraph_RemovesUnlistedWrapper()
        {
            #region implementation
            var html = "<div><p>Hello</p><span>world</span></div>";

            var result = html.RemoveUnwantedTags(new List<string> { "p" });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("<p>Hello</p>"));
            Assert.IsTrue(result.Contains("world"));
            Assert.IsFalse(result.Contains("<div>"));
            Assert.IsFalse(result.Contains("<span>"));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies the regex tag remover can strip all tags.
        /// </summary>
        /// <seealso cref="TextUtil.RemoveUnwantedTagsRegEx(string, List{string}, bool)"/>
        [TestMethod]
        public void RemoveUnwantedTagsRegEx_CleanAll_ReturnsOnlyText()
        {
            #region implementation
            var html = "<p>Hello <b>there</b></p>";

            var result = html.RemoveUnwantedTagsRegEx(new List<string>(), cleanAll: true);

            Assert.AreEqual("Hello there", result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies MinifyXml compacts valid XML and preserves invalid XML.
        /// </summary>
        /// <seealso cref="TextUtil.MinifyXml"/>
        [TestMethod]
        public void MinifyXml_ValidAndInvalidInput_CompactsOnlyValidXml()
        {
            #region implementation
            var validXml = "<root>\r\n  <item>Value</item>\r\n</root>";
            var invalidXml = "<root>";

            var minified = validXml.MinifyXml();
            var invalidResult = invalidXml.MinifyXml();

            Assert.AreEqual("<?xml version=\"1.0\" encoding=\"utf-16\"?><root><item>Value</item></root>", minified);
            Assert.AreEqual(invalidXml, invalidResult);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies NormalizeXmlWhitespace removes tag-adjacent whitespace.
        /// </summary>
        /// <seealso cref="TextUtil.NormalizeXmlWhitespace"/>
        [TestMethod]
        public void NormalizeXmlWhitespace_InlineSuperscript_RemovesUnsafeSpacing()
        {
            #region implementation
            var input = "area (mg/m\n<sup>2 </sup>\n). In";

            var result = input.NormalizeXmlWhitespace();

            Assert.AreEqual("area (mg/m<sup>2</sup>). In", result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies SanitizeXML removes invalid XML characters.
        /// </summary>
        /// <seealso cref="TextUtil.SanitizeXML"/>
        [TestMethod]
        public void SanitizeXML_InvalidControlCharacter_RemovesCharacter()
        {
            #region implementation
            var input = "A" + (char)0x0000 + "B";

            var result = TextUtil.SanitizeXML(input);

            Assert.AreEqual("AB", result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies delimited strings are unpacked, sorted, and de-duplicated.
        /// </summary>
        /// <seealso cref="TextUtil.UnpackDelimitedValues"/>
        [TestMethod]
        public void UnpackDelimitedValues_DelimitedRows_ReturnsDistinctSortedValues()
        {
            #region implementation
            var values = new List<string> { "z", "b;a", "a;c" };

            var result = values.UnpackDelimitedValues(';');

            CollectionAssert.AreEqual(new List<string> { "a", "b", "c", "z" }, result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies known colors are converted to RGBA strings.
        /// </summary>
        /// <seealso cref="TextUtil.ToRGBA"/>
        [TestMethod]
        public void ToRGBA_KnownColor_ReturnsCssRgba()
        {
            #region implementation
            var result = KnownColor.Red.ToRGBA(0.5);

            Assert.AreEqual("rgba(255,0,0,0.5)", result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies list and object comma serialization use readable property/value pairs.
        /// </summary>
        /// <seealso cref="TextUtil.ListToCommaString{T}"/>
        /// <seealso cref="TextUtil.ToCommaString{T}"/>
        [TestMethod]
        public void ListToCommaString_ToCommaString_FormatsProperties()
        {
            #region implementation
            var item = new SampleTextUtilDto { Name = "Alpha", Count = 2 };
            var list = new[] { item };

            var single = item.ToCommaString();
            var multiple = list.ListToCommaString();

            StringAssert.Contains(single, "Name:Alpha");
            StringAssert.Contains(single, "Count:2");
            StringAssert.Contains(multiple, "Name:Alpha");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies filename and JSON text cleanup remove unsafe characters.
        /// </summary>
        /// <seealso cref="TextUtil.CleanFileName"/>
        /// <seealso cref="TextUtil.RemoveJSONChars"/>
        [TestMethod]
        public void CleanFileName_RemoveJSONChars_RemovesUnsafeCharacters()
        {
            #region implementation
            var fileName = "bad:file/name?.txt".CleanFileName();
            var jsonText = "A\r\n\t\"B\"".RemoveJSONChars();

            Assert.IsFalse(fileName.Contains(':'));
            Assert.IsFalse(fileName.Contains('/'));
            Assert.IsFalse(jsonText.Contains('"'));
            Assert.IsTrue(jsonText.Contains('A'));
            Assert.IsTrue(jsonText.Contains('B'));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies comma-delimited text is trimmed and empty values are ignored.
        /// </summary>
        /// <seealso cref="TextUtil.CommaDelimitedToList"/>
        [TestMethod]
        public void CommaDelimitedToList_TextWithSpaces_ReturnsTrimmedValues()
        {
            #region implementation
            var result = "alpha, beta, ,gamma".CommaDelimitedToList();

            CollectionAssert.AreEqual(new List<string> { "alpha", "beta", string.Empty, "gamma" }, result);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies elapsed-time labels across the key display bands.
        /// </summary>
        /// <seealso cref="TextUtil.FormatElapsedTime"/>
        [TestMethod]
        public void FormatElapsedTime_CommonDurations_ReturnsExpectedLabels()
        {
            #region implementation
            Assert.AreEqual("Just Now", TextUtil.FormatElapsedTime(TimeSpan.FromSeconds(20)));
            Assert.AreEqual("Moments Ago", TextUtil.FormatElapsedTime(TimeSpan.FromMinutes(2)));
            Assert.AreEqual("Today", TextUtil.FormatElapsedTime(TimeSpan.FromHours(2)));
            Assert.AreEqual("Yesterday", TextUtil.FormatElapsedTime(TimeSpan.FromHours(25)));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies truncation helpers keep short text and shorten long text.
        /// </summary>
        /// <seealso cref="TextUtil.Truncate"/>
        /// <seealso cref="TextUtil.TruncateMiddle"/>
        [TestMethod]
        public void Truncate_TruncateMiddle_LongAndShortInputs_ReturnExpectedText()
        {
            #region implementation
            Assert.AreEqual("abc", "abc".Truncate(5));
            Assert.AreEqual("abc ...", "abcdef".Truncate(3));
            Assert.AreEqual("ab...ij", "abcdefghij".TruncateMiddle(4));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies assignment transfer entries include action and reason details.
        /// </summary>
        /// <seealso cref="TextUtil.GetAssignmentTransferLogEntry"/>
        [TestMethod]
        public void GetAssignmentTransferLogEntry_InternalTransferWithReason_IncludesExpectedTokens()
        {
            #region implementation
            var result = TextUtil.GetAssignmentTransferLogEntry(
                "from-user",
                "to-user",
                isExternal: false,
                category: "Review",
                newOwner: "Ada Lovelace",
                reason: "consultation");

            StringAssert.Contains(result, "<AssignmentAction>transferred</AssignmentAction>");
            StringAssert.Contains(result, "<AssignmentOwner>Ada Lovelace</AssignmentOwner>");
            StringAssert.Contains(result, "<TransferReason>consultation</TransferReason>");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies name parsing helpers return expected components.
        /// </summary>
        /// <seealso cref="TextUtil.PhoneNumber"/>
        /// <seealso cref="TextUtil.SplitName"/>
        /// <seealso cref="TextUtil.RemoveMiddleInitial"/>
        [TestMethod]
        public void PhoneNumber_SplitName_RemoveMiddleInitial_ParsesCommonText()
        {
            #region implementation
            var phone = TextUtil.PhoneNumber("2025550123");
            var split = TextUtil.SplitName("Doe, Jane");
            var name = "Jane Q. Doe".RemoveMiddleInitial();

            Assert.AreEqual("202-555-0123", phone);
            Assert.AreEqual("Jane", split.Item1);
            Assert.AreEqual("Doe", split.Item2);
            Assert.AreEqual("Jane  Doe", name);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies office and division parsing handle pipe-delimited LDAP locations.
        /// </summary>
        /// <seealso cref="TextUtil.ExtractOffice"/>
        /// <seealso cref="TextUtil.ExtractDivision"/>
        [TestMethod]
        public void ExtractOffice_ExtractDivision_PipeDelimitedLocation_ReturnsSegments()
        {
            #region implementation
            var location = "Root/Office of Testing/Division of Coverage";

            Assert.AreEqual("Office of Testing", TextUtil.ExtractOffice(location));
            Assert.AreEqual("Division of Coverage", TextUtil.ExtractDivision(location));
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies CSV, XML, and Base64 helpers produce stable serialized output.
        /// </summary>
        /// <seealso cref="TextUtil.ToCsv{T}"/>
        /// <seealso cref="TextUtil.Base64Encode(string)"/>
        /// <seealso cref="TextUtil.Base64Encode(int)"/>
        /// <seealso cref="TextUtil.Base64Decode"/>
        /// <seealso cref="TextUtil.ToXML{T}"/>
        [TestMethod]
        public void ToCsv_Base64_ToXML_SerializesStableOutput()
        {
            #region implementation
            var records = new[] { new SampleTextUtilDto { Name = "Alpha", Count = 2 } };
            var csv = TextUtil.ToCsv(records).ToList();
            var textEncoded = "Alpha".Base64Encode();
            var intEncoded = 42.Base64Encode();
            var xml = new[] { "a", "b" }.ToXML("item");

            Assert.AreEqual("Name,Count", csv[0].TrimEnd());
            Assert.AreEqual("Alpha,2", csv[1].TrimEnd());
            Assert.AreEqual("Alpha", textEncoded.Base64Decode());
            Assert.AreEqual("42", intEncoded.Base64Decode());
            Assert.AreEqual("<itemRoot><item>a</item><item>b</item></itemRoot>", xml);
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies HTML, URL, title, percent, and path helpers return safe strings.
        /// </summary>
        /// <seealso cref="TextUtil.RemoveHtmlXss"/>
        /// <seealso cref="TextUtil.GetFileNameFromUrl"/>
        /// <seealso cref="TextUtil.ToTitle"/>
        /// <seealso cref="TextUtil.TimeRemainingPercent"/>
        /// <seealso cref="TextUtil.TimeElapsedPercent"/>
        /// <seealso cref="TextUtil.GetDocumentTypeAbbreviation"/>
        /// <seealso cref="TextUtil.FixFilePath"/>
        /// <seealso cref="TextUtil.FixURLPath"/>
        [TestMethod]
        public void FormattingAndPathHelpers_CommonInputs_ReturnExpectedOutput()
        {
            #region implementation
            var sanitized = "<p>ok</p><script>alert(1)</script>".RemoveHtmlXss();
            var remaining = TextUtil.TimeRemainingPercent(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));
            var elapsed = TextUtil.TimeElapsedPercent(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

            Assert.IsFalse(sanitized?.Contains("<script>", StringComparison.OrdinalIgnoreCase) == true);
            Assert.AreEqual("file.txt", TextUtil.GetFileNameFromUrl("https://example.test/path/file.txt"));
            Assert.AreEqual("Hello World", "hello world".ToTitle());
            Assert.IsTrue(int.Parse(remaining) is >= 0 and <= 100);
            Assert.IsTrue(int.Parse(elapsed) is >= 0 and <= 100);
            Assert.AreEqual("SOP", TextUtil.GetDocumentTypeAbbreviation("standard operating procedure"));
            Assert.AreEqual(@"C:\temp\", @"C:\temp".FixFilePath());
            Assert.AreEqual("https://example.test/", "https://example.test".FixURLPath());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies pipe serialization emits a key header and compact values.
        /// </summary>
        /// <seealso cref="TextUtil.ToPipe{T}"/>
        [TestMethod]
        public void ToPipe_Object_ReturnsKeyHeaderAndValues()
        {
            #region implementation
            var record = new SampleTextUtilDto { Name = "Alpha|Beta", Count = 2 };

            var result = record.ToPipe();

            Assert.IsNotNull(result);
            StringAssert.Contains(result, "[KEY:");
            StringAssert.Contains(result, "Alpha\\|Beta");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies string classifiers return expected true and false values.
        /// </summary>
        /// <seealso cref="TextUtil.IsJson"/>
        /// <seealso cref="TextUtil.IsXml"/>
        /// <seealso cref="TextUtil.IsUrl"/>
        /// <seealso cref="TextUtil.IsEmail"/>
        /// <seealso cref="TextUtil.IsIpAddress"/>
        /// <seealso cref="TextUtil.IsMacAddress"/>
        /// <seealso cref="TextUtil.IsGuid"/>
        /// <seealso cref="TextUtil.IsIsbn10"/>
        /// <seealso cref="TextUtil.IsIsbn13"/>
        /// <seealso cref="TextUtil.IsCreditCard"/>
        /// <seealso cref="TextUtil.IsHexColor"/>
        /// <seealso cref="TextUtil.IsRgbColor"/>
        /// <seealso cref="TextUtil.IsHslColor"/>
        [TestMethod]
        public void StringClassifiers_RepresentativeInputs_ReturnExpectedBooleans()
        {
            #region implementation
            Assert.IsTrue("{\"a\":1}".IsJson());
            Assert.IsTrue("<root />".IsXml());
            Assert.IsTrue("https://example.test".IsUrl());
            Assert.IsTrue("person@example.test".IsEmail());
            Assert.IsTrue("127.0.0.1".IsIpAddress());
            Assert.IsTrue("00:11:22:33:44:55".IsMacAddress());
            Assert.IsTrue(Guid.NewGuid().ToString().IsGuid());
            Assert.IsTrue("0-306-40615-2".IsIsbn10());
            Assert.IsTrue("978-0-306-40615-7".IsIsbn13());
            Assert.IsTrue("4111111111111111".IsCreditCard());
            Assert.IsTrue("#00ff00".IsHexColor());
            Assert.IsTrue("rgb(0, 128, 255)".IsRgbColor());
            Assert.IsTrue("hsl(120, 100%, 50%)".IsHslColor());
            Assert.IsFalse("plain".IsJson());
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Simple DTO used for serialization helper tests.
        /// </summary>
        /// <seealso cref="ToCsv_Base64_ToXML_SerializesStableOutput"/>
        private sealed class SampleTextUtilDto
        {
            /**************************************************************/
            /// <summary>
            /// Gets or sets the sample name.
            /// </summary>
            public string Name { get; set; } = string.Empty;

            /**************************************************************/
            /// <summary>
            /// Gets or sets the sample count.
            /// </summary>
            public int Count { get; set; }
        }

        #endregion
    }
}
