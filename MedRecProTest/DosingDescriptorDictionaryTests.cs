using MedRecProImportClass.Service.TransformationServices.Dictionaries;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="DosingDescriptorDictionary"/> — the small
    /// lookup that confirms a 34068-7-coded table is a real Dosing table by
    /// detecting dose-descriptor row labels and shape-keyword phrases.
    /// </summary>
    [TestClass]
    public class DosingDescriptorDictionaryTests
    {
        #region ContainsDosingDescriptor

        [TestMethod]
        [DataRow("Starting Dose")]
        [DataRow("starting dose")]
        [DataRow("Recommended Starting Dose")]
        [DataRow("Initial Dose")]
        [DataRow("Loading Dose")]
        [DataRow("Maintenance Dose")]
        [DataRow("Recommended Dose")]
        [DataRow("Recommended Dosage")]
        [DataRow("Target Dose")]
        [DataRow("Target Dosage")]
        [DataRow("Maximum Dose")]
        [DataRow("Minimum Dose")]
        [DataRow("Usual Dose")]
        [DataRow("Single Dose")]
        [DataRow("Titration Step")]
        [DataRow("Titration Schedule")]
        [DataRow("Dose Titration")]
        [DataRow("Dose Reduction")]
        [DataRow("Dose Reductions")]
        [DataRow("Dose Modification")]
        [DataRow("Dose Modifications")]
        [DataRow("Dosage Modifications")]
        [DataRow("Dose Level")]
        [DataRow("Dose Levels")]
        [DataRow("First dose reduction")]
        [DataRow("Renal Adjustment")]
        [DataRow("Renal Dose Adjustment")]
        [DataRow("Hepatic Adjustment")]
        [DataRow("Weight-Based Dose")]
        [DataRow("Once Daily")]
        [DataRow("Twice Daily")]
        public void ContainsDosingDescriptor_KnownPhrase_ReturnsTrue(string raw)
        {
            Assert.IsTrue(
                DosingDescriptorDictionary.ContainsDosingDescriptor(raw),
                $"'{raw}' should be recognized as a dosing descriptor");
        }

        [TestMethod]
        [DataRow("Cmax")]
        [DataRow("AUC")]
        [DataRow("Adverse Reactions")]
        [DataRow("Pediatric Patients")]
        [DataRow("Drug Interaction")]
        [DataRow("How Supplied")]
        [DataRow("")]
        public void ContainsDosingDescriptor_NonDescriptor_ReturnsFalse(string raw)
        {
            Assert.IsFalse(
                DosingDescriptorDictionary.ContainsDosingDescriptor(raw),
                $"'{raw}' should not be recognized as a dosing descriptor");
        }

        [TestMethod]
        public void ContainsDosingDescriptor_Null_ReturnsFalse()
        {
            Assert.IsFalse(DosingDescriptorDictionary.ContainsDosingDescriptor(null));
        }

        [TestMethod]
        public void ContainsDosingDescriptor_PhraseEmbeddedInProse_StillMatches()
        {
            // The dictionary is contains-style so a header carrying extra
            // qualifiers ("Recommended Dosage for Adults") still hits.
            Assert.IsTrue(DosingDescriptorDictionary.ContainsDosingDescriptor(
                "Recommended Dosage for Adults"));
        }

        [TestMethod]
        public void ContainsDosingDescriptor_LooseWhitespace_StillMatches()
        {
            // Multi-space whitespace inside multi-word phrases is loosened to
            // \s+ so cells with extra spaces still match.
            Assert.IsTrue(DosingDescriptorDictionary.ContainsDosingDescriptor(
                "Recommended  Dosage"));
        }

        #endregion ContainsDosingDescriptor

        #region IsDoseReductionLabel

        [TestMethod]
        [DataRow("Recommended starting dose")]
        [DataRow("recommended starting dose")]
        [DataRow("First dose reduction")]
        [DataRow("Second dose reduction")]
        [DataRow("Third dose reduction")]
        [DataRow("Fourth dose reduction")]
        [DataRow("1st dose reduction")]
        [DataRow("2nd dose reduction")]
        [DataRow("3rd dose reduction")]
        public void IsDoseReductionLabel_DoseReductionLabel_ReturnsTrue(string raw)
        {
            Assert.IsTrue(
                DosingDescriptorDictionary.IsDoseReductionLabel(raw),
                $"'{raw}' should be a dose-reduction label");
        }

        [TestMethod]
        [DataRow("First")]
        [DataRow("Starting Dose")]
        [DataRow("Pediatric")]
        [DataRow("the first dose reduction was 50%")]
        [DataRow("")]
        public void IsDoseReductionLabel_NonReductionLabel_ReturnsFalse(string raw)
        {
            Assert.IsFalse(
                DosingDescriptorDictionary.IsDoseReductionLabel(raw),
                $"'{raw}' should not match the anchored dose-reduction pattern");
        }

        [TestMethod]
        public void IsDoseReductionLabel_Null_ReturnsFalse()
        {
            Assert.IsFalse(DosingDescriptorDictionary.IsDoseReductionLabel(null));
        }

        #endregion IsDoseReductionLabel
    }
}
