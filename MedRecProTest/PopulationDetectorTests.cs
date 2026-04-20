using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    /**************************************************************/
    /// <summary>
    /// Unit tests for <see cref="PopulationDetector"/> (Stage 3 of the SPL Table Normalization pipeline).
    /// </summary>
    /// <remarks>
    /// Tests cover:
    /// - Caption-based population extraction via regex
    /// - SectionTitle-based population extraction
    /// - Cross-validation via Levenshtein similarity
    /// - Keyword dictionary matching
    /// - Edge cases: null inputs, no match, conflicting sources
    ///
    /// No database or mocking needed — PopulationDetector is a static utility.
    /// Internal helpers are tested directly via InternalsVisibleTo.
    /// </remarks>
    /// <seealso cref="PopulationDetector"/>
    [TestClass]
    public class PopulationDetectorTests
    {
        #region Caption Extraction Tests

        /**************************************************************/
        /// <summary>
        /// Caption containing "in Pediatric Patients" extracts "Pediatric" population.
        /// </summary>
        [TestMethod]
        public void ExtractFromCaption_PediatricPatients_ReturnsPediatric()
        {
            var result = PopulationDetector.extractFromCaption("Table 3: PK Parameters in Pediatric Patients");
            Assert.IsNotNull(result);
            Assert.IsTrue(result!.Contains("Pediatric", StringComparison.OrdinalIgnoreCase));
        }

        /**************************************************************/
        /// <summary>
        /// Caption containing "Postmenopausal Women" extracts correct population.
        /// </summary>
        [TestMethod]
        public void ExtractFromCaption_PostmenopausalWomen_ReturnsCorrect()
        {
            var result = PopulationDetector.extractFromCaption("Adverse Events in Postmenopausal Women");
            Assert.IsNotNull(result);
            Assert.IsTrue(result!.Contains("Postmenopausal", StringComparison.OrdinalIgnoreCase));
        }

        /**************************************************************/
        /// <summary>
        /// Caption containing "premature infants" extracts correct population.
        /// </summary>
        [TestMethod]
        public void ExtractFromCaption_PrematureInfants_ReturnsCorrect()
        {
            var result = PopulationDetector.extractFromCaption("Efficacy in premature infants");
            Assert.IsNotNull(result);
            Assert.AreEqual("Premature Infants", result);
        }

        /**************************************************************/
        /// <summary>
        /// Caption containing "renal impairment" extracts correct population.
        /// </summary>
        [TestMethod]
        public void ExtractFromCaption_RenalImpairment_ReturnsCorrect()
        {
            var result = PopulationDetector.extractFromCaption("Dosing for renal impairment patients");
            Assert.IsNotNull(result);
            Assert.AreEqual("Renal Impairment", result);
        }

        /**************************************************************/
        /// <summary>
        /// Null caption returns null.
        /// </summary>
        [TestMethod]
        public void ExtractFromCaption_NullInput_ReturnsNull()
        {
            var result = PopulationDetector.extractFromCaption(null);
            Assert.IsNull(result);
        }

        /**************************************************************/
        /// <summary>
        /// Caption with no population keywords returns null.
        /// </summary>
        [TestMethod]
        public void ExtractFromCaption_NoPopulation_ReturnsNull()
        {
            var result = PopulationDetector.extractFromCaption("Table 1: Mean Pharmacokinetic Parameters");
            Assert.IsNull(result);
        }

        #endregion Caption Extraction Tests

        #region Section Title Extraction Tests

        /**************************************************************/
        /// <summary>
        /// "Pharmacokinetics in Pediatric Patients" extracts population.
        /// </summary>
        [TestMethod]
        public void ExtractFromSectionTitle_PharmacokineticsPediatric_ReturnsPediatric()
        {
            var result = PopulationDetector.extractFromSectionTitle("Pharmacokinetics in Pediatric Patients");
            Assert.IsNotNull(result);
            Assert.IsTrue(result!.Contains("Pediatric", StringComparison.OrdinalIgnoreCase));
        }

        /**************************************************************/
        /// <summary>
        /// "Use in Specific Populations" uses keyword fallback.
        /// </summary>
        [TestMethod]
        public void ExtractFromSectionTitle_NullInput_ReturnsNull()
        {
            var result = PopulationDetector.extractFromSectionTitle(null);
            Assert.IsNull(result);
        }

        #endregion Section Title Extraction Tests

        #region DetectPopulation Integration Tests

        /**************************************************************/
        /// <summary>
        /// Both sources agree — high confidence.
        /// </summary>
        [TestMethod]
        public void DetectPopulation_BothSourcesAgree_HighConfidence()
        {
            var (pop, conf) = PopulationDetector.DetectPopulation(
                "Table 3: PK in Pediatric Patients",
                "Pharmacokinetics in Pediatric Patients",
                null);
            Assert.IsNotNull(pop);
            Assert.IsTrue(conf >= 0.7);
        }

        /**************************************************************/
        /// <summary>
        /// Only caption has population — medium confidence.
        /// </summary>
        [TestMethod]
        public void DetectPopulation_OnlyCaption_MediumConfidence()
        {
            var (pop, conf) = PopulationDetector.DetectPopulation(
                "Adverse Events in Postmenopausal Women",
                null,
                null);
            Assert.IsNotNull(pop);
            Assert.AreEqual(0.7, conf);
        }

        /**************************************************************/
        /// <summary>
        /// Only section title has population — medium-high confidence.
        /// </summary>
        [TestMethod]
        public void DetectPopulation_OnlySectionTitle_MediumHighConfidence()
        {
            var (pop, conf) = PopulationDetector.DetectPopulation(
                null,
                "Pharmacokinetics in Pediatric Patients",
                null);
            Assert.IsNotNull(pop);
            Assert.AreEqual(0.8, conf);
        }

        /**************************************************************/
        /// <summary>
        /// No population detected — returns null with 0.0 confidence.
        /// </summary>
        [TestMethod]
        public void DetectPopulation_NoSources_ReturnsNullZeroConfidence()
        {
            var (pop, conf) = PopulationDetector.DetectPopulation(null, null, null);
            Assert.IsNull(pop);
            Assert.AreEqual(0.0, conf);
        }

        /**************************************************************/
        /// <summary>
        /// Parent section title used as fallback when both primary sources are null.
        /// </summary>
        [TestMethod]
        public void DetectPopulation_FallsBackToParentSectionTitle()
        {
            var (pop, conf) = PopulationDetector.DetectPopulation(
                null,
                null,
                "Clinical Studies in Pediatric Patients");
            Assert.IsNotNull(pop);
            Assert.IsTrue(conf > 0);
        }

        #endregion DetectPopulation Integration Tests

        #region Levenshtein Similarity Tests

        /**************************************************************/
        /// <summary>
        /// Identical strings return 1.0 similarity.
        /// </summary>
        [TestMethod]
        public void ComputeSimilarity_IdenticalStrings_ReturnsOne()
        {
            var result = PopulationDetector.ComputeSimilarity("Pediatric", "Pediatric");
            Assert.AreEqual(1.0, result);
        }

        /**************************************************************/
        /// <summary>
        /// Similar strings return high similarity.
        /// </summary>
        [TestMethod]
        public void ComputeSimilarity_SimilarStrings_ReturnsHigh()
        {
            var result = PopulationDetector.ComputeSimilarity("Pediatric Patients", "Pediatric Patient");
            Assert.IsTrue(result > 0.8);
        }

        /**************************************************************/
        /// <summary>
        /// Completely different strings return low similarity.
        /// </summary>
        [TestMethod]
        public void ComputeSimilarity_DifferentStrings_ReturnsLow()
        {
            var result = PopulationDetector.ComputeSimilarity("Pediatric", "Geriatric");
            Assert.IsTrue(result < 0.8);
        }

        /**************************************************************/
        /// <summary>
        /// Empty strings return 1.0.
        /// </summary>
        [TestMethod]
        public void ComputeSimilarity_BothEmpty_ReturnsOne()
        {
            var result = PopulationDetector.ComputeSimilarity("", "");
            Assert.AreEqual(1.0, result);
        }

        /**************************************************************/
        /// <summary>
        /// One empty string returns 0.0.
        /// </summary>
        [TestMethod]
        public void ComputeSimilarity_OneEmpty_ReturnsZero()
        {
            var result = PopulationDetector.ComputeSimilarity("Pediatric", "");
            Assert.AreEqual(0.0, result);
        }

        #endregion Levenshtein Similarity Tests

        #region TryMatchLabel — Metabolizer Phenotypes & Row-Label Populations

        /**************************************************************/
        /// <summary>
        /// CYP2C19 metabolizer phenotypes resolve to canonical "Xxx Metabolizer"
        /// population strings when they appear as bare row labels.
        /// </summary>
        [TestMethod]
        [DataRow("Poor", "Poor Metabolizer")]
        [DataRow("Intermediate", "Intermediate Metabolizer")]
        [DataRow("Normal", "Normal Metabolizer")]
        [DataRow("Ultrarapid", "Ultrarapid Metabolizer")]
        [DataRow("Extensive", "Extensive Metabolizer")]
        [DataRow("Poor Metabolizer", "Poor Metabolizer")]
        [DataRow("Intermediate Metabolizers", "Intermediate Metabolizer")]
        public void TryMatchLabel_Phenotype_ReturnsCanonical(string input, string expected)
        {
            var hit = PopulationDetector.TryMatchLabel(input, out var canonical);
            Assert.IsTrue(hit, $"'{input}' should match");
            Assert.AreEqual(expected, canonical);
        }

        /**************************************************************/
        /// <summary>
        /// Standard population row labels (Healthy Subjects, Pediatric, Elderly)
        /// resolve via <see cref="PopulationDetector.TryMatchLabel"/>.
        /// </summary>
        [TestMethod]
        [DataRow("Healthy Subjects", "Healthy Volunteers")]
        [DataRow("Healthy Volunteers", "Healthy Volunteers")]
        [DataRow("Pediatric", "Pediatric")]
        [DataRow("Elderly", "Elderly")]
        [DataRow("Renal Impairment", "Renal Impairment")]
        public void TryMatchLabel_StandardPopulation_ReturnsCanonical(string input, string expected)
        {
            var hit = PopulationDetector.TryMatchLabel(input, out var canonical);
            Assert.IsTrue(hit);
            Assert.AreEqual(expected, canonical);
        }

        /**************************************************************/
        /// <summary>
        /// Non-population content (PK parameter names, PD markers, prose) does not
        /// match — prevents false reroutes from ParameterName to Population.
        /// </summary>
        [TestMethod]
        [DataRow("Cmax")]
        [DataRow("AUC0-inf")]
        [DataRow("IPA")]
        [DataRow("VASP-PRI")]
        [DataRow("Some arbitrary prose about a drug")]
        [DataRow("")]
        public void TryMatchLabel_NonPopulation_ReturnsFalse(string input)
        {
            var hit = PopulationDetector.TryMatchLabel(input, out var canonical);
            Assert.IsFalse(hit, $"'{input}' should not match as population");
            Assert.AreEqual(string.Empty, canonical);
        }

        [TestMethod]
        public void TryMatchLabel_Null_ReturnsFalse()
        {
            var hit = PopulationDetector.TryMatchLabel(null, out var canonical);
            Assert.IsFalse(hit);
            Assert.AreEqual(string.Empty, canonical);
        }

        #endregion TryMatchLabel
    }
}
