using MedRecProImportClass.Service.TransformationServices.Dictionaries;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="CategoryNameNormalizer"/>: normalizes TableCategory strings
    /// between the underscore-uppercase parser-pipeline form and the documentation form.
    /// </summary>
    /// <seealso cref="CategoryNameNormalizer"/>
    [TestClass]
    public class CategoryNameNormalizerTests
    {
        #region Normalize Tests

        /**************************************************************/
        /// <summary>
        /// Underscore-uppercase form is converted to documentation form.
        /// </summary>
        [TestMethod]
        public void Normalize_UnderscoreForm_ToDocForm()
        {
            #region implementation

            Assert.AreEqual("AdverseEvent", CategoryNameNormalizer.Normalize("ADVERSE_EVENT"));
            Assert.AreEqual("DrugInteraction", CategoryNameNormalizer.Normalize("DRUG_INTERACTION"));
            Assert.AreEqual("TissueDistribution", CategoryNameNormalizer.Normalize("TISSUE_DISTRIBUTION"));
            Assert.AreEqual("TextDescriptive", CategoryNameNormalizer.Normalize("TEXT_DESCRIPTIVE"));
            Assert.AreEqual("Efficacy", CategoryNameNormalizer.Normalize("EFFICACY"));
            Assert.AreEqual("Dosing", CategoryNameNormalizer.Normalize("DOSING"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Documentation form passes through unchanged.
        /// </summary>
        [TestMethod]
        public void Normalize_DocForm_Roundtrips()
        {
            #region implementation

            Assert.AreEqual("AdverseEvent", CategoryNameNormalizer.Normalize("AdverseEvent"));
            Assert.AreEqual("DrugInteraction", CategoryNameNormalizer.Normalize("DrugInteraction"));
            Assert.AreEqual("PK", CategoryNameNormalizer.Normalize("PK"));
            Assert.AreEqual("BMD", CategoryNameNormalizer.Normalize("BMD"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Lowercase input still resolves (case-insensitive matching).
        /// </summary>
        [TestMethod]
        public void Normalize_LowercaseInput_StillResolves()
        {
            #region implementation

            Assert.AreEqual("AdverseEvent", CategoryNameNormalizer.Normalize("adverse_event"));
            Assert.AreEqual("DrugInteraction", CategoryNameNormalizer.Normalize("drug_interaction"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Whitespace is trimmed before matching.
        /// </summary>
        [TestMethod]
        public void Normalize_WhitespaceInput_Trimmed()
        {
            #region implementation

            Assert.AreEqual("AdverseEvent", CategoryNameNormalizer.Normalize("  ADVERSE_EVENT  "));
            Assert.AreEqual("PK", CategoryNameNormalizer.Normalize("\tPK\n"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null and whitespace-only input returns empty string.
        /// </summary>
        [TestMethod]
        public void Normalize_NullOrEmpty_ReturnsEmpty()
        {
            #region implementation

            Assert.AreEqual(string.Empty, CategoryNameNormalizer.Normalize(null));
            Assert.AreEqual(string.Empty, CategoryNameNormalizer.Normalize(""));
            Assert.AreEqual(string.Empty, CategoryNameNormalizer.Normalize("   "));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Unknown categories are returned trimmed but unchanged (defensive — preserves the
        /// caller's value so unknown labels propagate rather than being silently rewritten).
        /// </summary>
        [TestMethod]
        public void Normalize_UnknownCategory_ReturnsTrimmedInput()
        {
            #region implementation

            Assert.AreEqual("FOO", CategoryNameNormalizer.Normalize("FOO"));
            Assert.AreEqual("Custom_Category", CategoryNameNormalizer.Normalize("  Custom_Category  "));

            #endregion
        }

        #endregion Normalize Tests

        #region ToUnderscoreForm Tests

        /**************************************************************/
        /// <summary>
        /// Documentation form is converted back to underscore-uppercase.
        /// </summary>
        [TestMethod]
        public void ToUnderscoreForm_DocForm_ToUnderscoreForm()
        {
            #region implementation

            Assert.AreEqual("ADVERSE_EVENT", CategoryNameNormalizer.ToUnderscoreForm("AdverseEvent"));
            Assert.AreEqual("DRUG_INTERACTION", CategoryNameNormalizer.ToUnderscoreForm("DrugInteraction"));
            Assert.AreEqual("TISSUE_DISTRIBUTION", CategoryNameNormalizer.ToUnderscoreForm("TissueDistribution"));
            Assert.AreEqual("TEXT_DESCRIPTIVE", CategoryNameNormalizer.ToUnderscoreForm("TextDescriptive"));
            Assert.AreEqual("EFFICACY", CategoryNameNormalizer.ToUnderscoreForm("Efficacy"));
            Assert.AreEqual("DOSING", CategoryNameNormalizer.ToUnderscoreForm("Dosing"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Already-underscore form passes through unchanged.
        /// </summary>
        [TestMethod]
        public void ToUnderscoreForm_AlreadyUnderscoreForm_Roundtrips()
        {
            #region implementation

            Assert.AreEqual("ADVERSE_EVENT", CategoryNameNormalizer.ToUnderscoreForm("ADVERSE_EVENT"));
            Assert.AreEqual("PK", CategoryNameNormalizer.ToUnderscoreForm("PK"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null and whitespace-only input returns empty string.
        /// </summary>
        [TestMethod]
        public void ToUnderscoreForm_NullOrEmpty_ReturnsEmpty()
        {
            #region implementation

            Assert.AreEqual(string.Empty, CategoryNameNormalizer.ToUnderscoreForm(null));
            Assert.AreEqual(string.Empty, CategoryNameNormalizer.ToUnderscoreForm("  "));

            #endregion
        }

        #endregion ToUnderscoreForm Tests
    }
}
