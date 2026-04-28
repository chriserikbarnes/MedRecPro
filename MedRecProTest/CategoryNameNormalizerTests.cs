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
            Assert.AreEqual("Efficacy", CategoryNameNormalizer.Normalize("EFFICACY"));

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
            Assert.AreEqual("EFFICACY", CategoryNameNormalizer.ToUnderscoreForm("Efficacy"));

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

        /**************************************************************/
        /// <summary>
        /// Lowercase underscore input normalizes to canonical uppercase form, honoring the
        /// "input may be either form" contract.
        /// </summary>
        [TestMethod]
        public void ToUnderscoreForm_LowercaseUnderscoreInput_NormalizesToUppercase()
        {
            #region implementation

            Assert.AreEqual("ADVERSE_EVENT", CategoryNameNormalizer.ToUnderscoreForm("adverse_event"));
            Assert.AreEqual("DRUG_INTERACTION", CategoryNameNormalizer.ToUnderscoreForm("drug_interaction"));
            Assert.AreEqual("EFFICACY", CategoryNameNormalizer.ToUnderscoreForm("efficacy"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Mixed-case underscore input normalizes to canonical uppercase form.
        /// </summary>
        [TestMethod]
        public void ToUnderscoreForm_MixedCaseUnderscoreInput_NormalizesToUppercase()
        {
            #region implementation

            Assert.AreEqual("ADVERSE_EVENT", CategoryNameNormalizer.ToUnderscoreForm("Adverse_Event"));
            Assert.AreEqual("DRUG_INTERACTION", CategoryNameNormalizer.ToUnderscoreForm("Drug_Interaction"));

            #endregion
        }

        #endregion ToUnderscoreForm Tests
    }
}
