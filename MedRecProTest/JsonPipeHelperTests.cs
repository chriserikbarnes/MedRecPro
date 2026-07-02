using MedRecPro.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Tests JSON wrapper detection and pipe-delimited conversion.
    /// </summary>
    /// <remarks>
    /// The tests exercise Newtonsoft and System.Text.Json wrapper paths without
    /// depending on LLM calls or external serialization infrastructure.
    /// </remarks>
    /// <seealso cref="JsonPipeHelper"/>
    [TestClass]
    public class JsonPipeHelperTests
    {
        #region implementation

        /**************************************************************/
        /// <summary>
        /// Verifies a Newtonsoft JArray converts to a compact pipe table.
        /// </summary>
        /// <seealso cref="JsonPipeHelper.TryConvertToPipe"/>
        [TestMethod]
        public void TryConvertToPipe_JArray_ReturnsPipeTable()
        {
            #region implementation
            var array = JArray.Parse("""
                [
                  { "ProductName": "Alpha|Beta", "DocumentCount": 2, "IsActive": true },
                  { "ProductName": "Gamma", "DocumentCount": 3, "IsActive": false }
                ]
                """);

            var result = JsonPipeHelper.TryConvertToPipe(array);

            Assert.IsNotNull(result);
            StringAssert.Contains(result, "[KEY:");
            StringAssert.Contains(result, "PN|DC|IA");
            StringAssert.Contains(result, "Alpha\\|Beta|2|1");
            StringAssert.Contains(result, "Gamma|3|0");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies a Newtonsoft JObject converts as a single-row pipe table.
        /// </summary>
        /// <seealso cref="JsonPipeHelper.TryConvertToPipe"/>
        [TestMethod]
        public void TryConvertToPipe_JObject_ReturnsSingleRowPipeTable()
        {
            #region implementation
            var obj = JObject.Parse("""{ "Name": "Ada", "Score": 4.5 }""");

            var result = JsonPipeHelper.TryConvertToPipe(obj);

            Assert.IsNotNull(result);
            StringAssert.Contains(result, "NAM|SCO");
            StringAssert.Contains(result, "Ada|4.5");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies System.Text.Json elements convert arrays and objects.
        /// </summary>
        /// <seealso cref="JsonPipeHelper.TryConvertToPipe"/>
        [TestMethod]
        public void TryConvertToPipe_JsonElementArrayAndObject_ReturnsPipeTables()
        {
            #region implementation
            using var arrayDocument = JsonDocument.Parse("""[{ "Name": "Ada", "When": "2026-07-02T12:00:00" }]""");
            using var objectDocument = JsonDocument.Parse("""{ "Name": "Grace", "Id": "53566d4f-ff40-4815-b922-3416cde56fb1" }""");

            var arrayResult = JsonPipeHelper.TryConvertToPipe(arrayDocument.RootElement);
            var objectResult = JsonPipeHelper.TryConvertToPipe(objectDocument.RootElement);

            Assert.IsNotNull(arrayResult);
            Assert.IsNotNull(objectResult);
            StringAssert.Contains(arrayResult, "Ada|2026-07-02 12");
            StringAssert.Contains(objectResult, "Grace|53566d4fff404815b9223416cde56fb1");
            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies JSON type detection and unsupported conversion fallback.
        /// </summary>
        /// <seealso cref="JsonPipeHelper.IsJsonType"/>
        /// <seealso cref="JsonPipeHelper.TryConvertToPipe"/>
        [TestMethod]
        public void IsJsonType_UnsupportedValues_ReturnExpectedBooleansAndNull()
        {
            #region implementation
            using var document = JsonDocument.Parse("""{ "Name": "Ada" }""");

            Assert.IsTrue(JsonPipeHelper.IsJsonType(JArray.Parse("[]")));
            Assert.IsTrue(JsonPipeHelper.IsJsonType(JObject.Parse("{}")));
            Assert.IsTrue(JsonPipeHelper.IsJsonType(document.RootElement));
            Assert.IsFalse(JsonPipeHelper.IsJsonType("not-json"));
            Assert.IsNull(JsonPipeHelper.TryConvertToPipe("not-json"));
            Assert.IsNull(JsonPipeHelper.TryConvertToPipe(null));
            #endregion
        }

        #endregion
    }
}
