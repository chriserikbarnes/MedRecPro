using MedRecProImportClass.Service.TransformationServices.Dictionaries;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="PdMarkerDictionary"/> — the small lookup that
    /// flags pharmacodynamic markers mixed into PK tables (e.g., IPA, VASP-PRI).
    /// </summary>
    [TestClass]
    public class PdMarkerDictionaryTests
    {
        [TestMethod]
        [DataRow("IPA", true)]
        [DataRow("ipa", true)]
        [DataRow("VASP-PRI", true)]
        [DataRow("VASP PRI", true)]
        [DataRow("PRI", true)]
        [DataRow("Platelet Aggregation", true)]
        [DataRow("Inhibition of Platelet Aggregation", true)]
        [DataRow("Maximum Platelet Aggregation", true)]
        [DataRow("MPA", true)]
        [DataRow("Platelet Reactivity Index", true)]
        public void IsPdMarker_KnownMarker_ReturnsTrue(string raw, bool expected)
        {
            Assert.AreEqual(expected, PdMarkerDictionary.IsPdMarker(raw));
        }

        [TestMethod]
        [DataRow("Cmax")]
        [DataRow("AUC")]
        [DataRow("Poor Metabolizer")]
        [DataRow("Healthy Subjects")]
        [DataRow("")]
        public void IsPdMarker_NonMarker_ReturnsFalse(string raw)
        {
            Assert.IsFalse(PdMarkerDictionary.IsPdMarker(raw));
        }

        [TestMethod]
        public void IsPdMarker_Null_ReturnsFalse()
        {
            Assert.IsFalse(PdMarkerDictionary.IsPdMarker(null));
        }

        [TestMethod]
        public void IsPdMarker_TrimmedWhitespace_StillMatches()
        {
            Assert.IsTrue(PdMarkerDictionary.IsPdMarker("  IPA  "));
        }
    }
}
