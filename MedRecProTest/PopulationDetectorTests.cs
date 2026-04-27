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

        #region TryMatchLabel — Regex Second Pass

        /**************************************************************/
        /// <summary>
        /// Age-range row labels ("6 to 11 years", "12-17 Years") are matched by
        /// the regex second pass and canonicalized to "Ages {lo}-{hi} Years".
        /// </summary>
        [TestMethod]
        [DataRow("6 to 11 years", "Ages 6-11 Years")]
        [DataRow("12 to 16 years", "Ages 12-16 Years")]
        [DataRow("12-17 years", "Ages 12-17 Years")]
        [DataRow("18 to 64 Years", "Ages 18-64 Years")]
        public void TryMatchLabel_AgeRange_RegexMatch(string input, string expected)
        {
            var hit = PopulationDetector.TryMatchLabel(input, out var canonical, out var viaRegex);
            Assert.IsTrue(hit);
            Assert.AreEqual(expected, canonical);
            Assert.IsTrue(viaRegex, "age range should match via regex second pass");
        }

        /**************************************************************/
        /// <summary>
        /// Infants Birth-to-N phrases canonicalize with consistent "Infants Birth to {N} {Unit}" form.
        /// </summary>
        [TestMethod]
        [DataRow("Infants from Birth to 12 Months", "Infants Birth to 12 Months")]
        [DataRow("Infant Birth to 2 Years", "Infants Birth to 2 Years")]
        [DataRow("Infants Birth to 6 Months", "Infants Birth to 6 Months")]
        public void TryMatchLabel_InfantsBirthToN_RegexMatch(string input, string expected)
        {
            var hit = PopulationDetector.TryMatchLabel(input, out var canonical, out var viaRegex);
            Assert.IsTrue(hit);
            Assert.AreEqual(expected, canonical);
            Assert.IsTrue(viaRegex);
        }

        /**************************************************************/
        /// <summary>
        /// Renal function band phrases collapse to "{Band} Renal Function"
        /// regardless of whether "Renal Impairment" or extra qualifiers appear.
        /// </summary>
        [TestMethod]
        [DataRow("Normal Creatinine Clearance 90-140 mL/min", "Normal Renal Function")]
        [DataRow("Mild Creatinine Clearance 60-90 mL/min", "Mild Renal Function")]
        [DataRow("Moderate Creatinine Clearance 30-60 mL/min", "Moderate Renal Function")]
        [DataRow("Severe Creatinine Clearance 10-30 mL/min", "Severe Renal Function")]
        [DataRow("Severe Renal Impairment Creatinine Clearance 10-30 mL/min", "Severe Renal Function")]
        [DataRow("ESRD Creatinine Clearance <10 mL/min on Hemodialysis", "ESRD Renal Function")]
        public void TryMatchLabel_RenalFunctionBand_RegexMatch(string input, string expected)
        {
            var hit = PopulationDetector.TryMatchLabel(input, out var canonical, out var viaRegex);
            Assert.IsTrue(hit);
            Assert.AreEqual(expected, canonical);
            Assert.IsTrue(viaRegex);
        }

        /**************************************************************/
        /// <summary>
        /// Trimester phrases canonicalize to "First / Second / Third Trimester"
        /// from either numeric ("2nd") or word ("Second") forms.
        /// </summary>
        [TestMethod]
        [DataRow("1st Trimester of Pregnancy", "First Trimester")]
        [DataRow("2nd Trimester of Pregnancy", "Second Trimester")]
        [DataRow("3rd Trimester", "Third Trimester")]
        [DataRow("First Trimester of Pregnancy", "First Trimester")]
        [DataRow("Second Trimester", "Second Trimester")]
        [DataRow("Third Trimester", "Third Trimester")]
        // Compressed ordinal-no-space forms observed in TID 9918 (OCR dropped
        // the superscript "nd"/"rd"): "2Trimester of pregnancy" → Second Trimester.
        [DataRow("1Trimester of pregnancy", "First Trimester")]
        [DataRow("2Trimester of pregnancy", "Second Trimester")]
        [DataRow("3Trimester of pregnancy", "Third Trimester")]
        [DataRow("2Trimester", "Second Trimester")]
        public void TryMatchLabel_Trimester_RegexMatch(string input, string expected)
        {
            var hit = PopulationDetector.TryMatchLabel(input, out var canonical, out var viaRegex);
            Assert.IsTrue(hit);
            Assert.AreEqual(expected, canonical);
            Assert.IsTrue(viaRegex);
        }

        /**************************************************************/
        /// <summary>
        /// The regex second pass must not over-match: drug names, PK parameter
        /// names, and unrelated medical terms remain unmatched.
        /// </summary>
        [TestMethod]
        [DataRow("Tramadol")]
        [DataRow("Cmax")]
        [DataRow("Normal Saline")]
        [DataRow("Aspirin 500 mg once daily")]
        [DataRow("Arbitrary prose with no population signal")]
        public void TryMatchLabel_RegexSecondPass_DoesNotOverMatch(string input)
        {
            var hit = PopulationDetector.TryMatchLabel(input, out var canonical, out _);
            Assert.IsFalse(hit, $"'{input}' should not match as population");
            Assert.AreEqual(string.Empty, canonical);
        }

        /**************************************************************/
        /// <summary>
        /// Dictionary matches report matchedViaRegex = false, distinguishing them
        /// from regex-second-pass matches for downstream flag emission.
        /// </summary>
        [TestMethod]
        [DataRow("Pediatric")]
        [DataRow("Healthy Volunteers")]
        [DataRow("Poor")]
        public void TryMatchLabel_DictionaryMatch_MatchedViaRegexIsFalse(string input)
        {
            var hit = PopulationDetector.TryMatchLabel(input, out _, out var viaRegex);
            Assert.IsTrue(hit);
            Assert.IsFalse(viaRegex, "dictionary hit should set matchedViaRegex = false");
        }

        #endregion TryMatchLabel — Regex Second Pass

        #region R14 — Age Group Compound Forms

        /**************************************************************/
        /// <summary>
        /// R14 — "{AgeStratum} Age Group" compound forms (surfaced post-Iter9 in
        /// TID 25038 Palonosetron pediatric table) route to canonical Population
        /// via <see cref="PopulationDetector.TryMatchLabel"/>, preventing the
        /// drug-name heuristic from claiming them for TreatmentArm.
        /// </summary>
        [TestMethod]
        [DataRow("Pediatric Age Group", "Pediatric")]
        [DataRow("Adult Age Group", "Adult")]
        [DataRow("Adolescent Age Group", "Adolescents")]
        [DataRow("Geriatric Age Group", "Geriatric")]
        [DataRow("Elderly Age Group", "Elderly")]
        [DataRow("Neonatal Age Group", "Neonatal")]
        [DataRow("Infant Age Group", "Infants")]
        [DataRow("Young Age Group", "Young Adults")]
        public void TryMatchLabel_AgeGroupCompound_ReturnsCanonicalPopulation(string input, string expected)
        {
            var hit = PopulationDetector.TryMatchLabel(input, out var canonical);
            Assert.IsTrue(hit, $"'{input}' must resolve as a population (not fall through to drug-name heuristic)");
            Assert.AreEqual(expected, canonical);
        }

        /**************************************************************/
        /// <summary>
        /// R14 — Age-Group lookups are dictionary hits (exact string match in
        /// <c>_labelToCanonical</c>), not regex-second-pass matches.
        /// </summary>
        [TestMethod]
        public void TryMatchLabel_AgeGroup_DictionaryMatch_NotViaRegex()
        {
            var hit = PopulationDetector.TryMatchLabel("Pediatric Age Group", out _, out var viaRegex);
            Assert.IsTrue(hit);
            Assert.IsFalse(viaRegex, "'Pediatric Age Group' should match via dictionary, not regex");
        }

        #endregion R14 — Age Group Compound Forms

        #region Weight Band Detection

        /**************************************************************/
        /// <summary>
        /// LooksLikeWeightBand recognizes comparator-form bands and range-form
        /// bands used in body-weight dosing tables (TextTableID 19220 / 21539).
        /// </summary>
        [TestMethod]
        [DataRow("<50 kg")]
        [DataRow("< 50 kg")]
        [DataRow("> 90 kg")]
        [DataRow("≤75 kg")]
        [DataRow("≥90 kg")]
        [DataRow("<=75 kg")]
        [DataRow(">=90 kg")]
        [DataRow("50-59 kg")]
        [DataRow("50–59 kg")]
        [DataRow("50—59 kg")]
        [DataRow("50 to 59 kg")]
        [DataRow("60-69 kg")]
        [DataRow("2.5-5 kg")]
        public void LooksLikeWeightBand_KnownBand_ReturnsTrue(string raw)
        {
            Assert.IsTrue(PopulationDetector.LooksLikeWeightBand(raw),
                $"'{raw}' should be recognized as a weight band");
        }

        /**************************************************************/
        /// <summary>
        /// LooksLikeWeightBand rejects bare numbers, bare units, and prose
        /// that contains weight-band-shaped substrings — the patterns are
        /// anchored to the whole cell.
        /// </summary>
        [TestMethod]
        [DataRow("50")]
        [DataRow("kg")]
        [DataRow("5 kg")]
        [DataRow("Pediatric")]
        [DataRow("the dose was reduced in patients <50 kg")]
        [DataRow("")]
        public void LooksLikeWeightBand_NotABand_ReturnsFalse(string raw)
        {
            Assert.IsFalse(PopulationDetector.LooksLikeWeightBand(raw),
                $"'{raw}' should not be recognized as a weight band");
        }

        /**************************************************************/
        /// <summary>
        /// LooksLikeWeightBand handles null without throwing.
        /// </summary>
        [TestMethod]
        public void LooksLikeWeightBand_Null_ReturnsFalse()
        {
            Assert.IsFalse(PopulationDetector.LooksLikeWeightBand(null));
        }

        /**************************************************************/
        /// <summary>
        /// TryMatchLabel canonicalizes weight bands to a stable form so
        /// duplicates collapse: ASCII hyphen for ranges, no space between
        /// comparator and number, &lt;= and &gt;= folded to ≤ / ≥.
        /// </summary>
        [TestMethod]
        [DataRow("50-59 kg", "50-59 kg")]
        [DataRow("50–59 kg", "50-59 kg")]
        [DataRow("50 to 59 kg", "50-59 kg")]
        [DataRow("<50 kg", "<50 kg")]
        [DataRow("< 50 kg", "<50 kg")]
        [DataRow("≥90 kg", "≥90 kg")]
        [DataRow("<=75 kg", "≤75 kg")]
        [DataRow(">=90 kg", "≥90 kg")]
        public void TryMatchLabel_WeightBand_CanonicalizesToStableForm(string input, string expected)
        {
            var hit = PopulationDetector.TryMatchLabel(input, out var canonical);
            Assert.IsTrue(hit, $"'{input}' must resolve as a weight-band population");
            Assert.AreEqual(expected, canonical);
        }

        #endregion Weight Band Detection
    }
}
