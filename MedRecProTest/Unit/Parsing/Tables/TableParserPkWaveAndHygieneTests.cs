using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Parsing.Tables
{
    public partial class TableParserTests
    {
        #region PK Wave 3 R11 — ArmN from DoseRegimen Fallback

        /**************************************************************/
        /// <summary>
        /// R11 — <c>applyDoseRegimenArmNFallback</c> extracts the sample size
        /// from a DoseRegimen string containing a trailing "N=X" token and
        /// populates the observation's ArmN accordingly. Flag is appended for
        /// audit.
        /// </summary>
        [TestMethod]
        public void PkParser_R11_DoseRegimenArmN_TrailingUppercaseN_Populates()
        {
            var obs = new List<ParsedObservation>
            {
                new ParsedObservation
                {
                    DoseRegimen = "Age 6-16 given 0.7 mg/kg once daily for 7 days N=25",
                    ArmN = null
                }
            };

            PkTableParser.applyDoseRegimenArmNFallback(obs);

            Assert.AreEqual(25, obs[0].ArmN);
            Assert.IsTrue((obs[0].ValidationFlags ?? "").Contains("PK_DOSE_REGIMEN_ARMN_FALLBACK:25"),
                $"Expected PK_DOSE_REGIMEN_ARMN_FALLBACK:25 flag, got '{obs[0].ValidationFlags}'");
        }

        /**************************************************************/
        /// <summary>
        /// R11 — Lowercase "n=" is equally acceptable. Regex is explicitly
        /// case-folded via the <c>[Nn]</c> class so it matches both forms
        /// without <c>RegexOptions.IgnoreCase</c>.
        /// </summary>
        [TestMethod]
        public void PkParser_R11_DoseRegimenArmN_LowercaseN_Populates()
        {
            var obs = new List<ParsedObservation>
            {
                new ParsedObservation
                {
                    DoseRegimen = "Adults given 50 mg once daily for 7 days n=12",
                    ArmN = null
                }
            };

            PkTableParser.applyDoseRegimenArmNFallback(obs);

            Assert.AreEqual(12, obs[0].ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// R11 — Whitespace around the equals sign is tolerated: "N = 188",
        /// "N =188", "N= 188" must all match.
        /// </summary>
        [TestMethod]
        public void PkParser_R11_DoseRegimenArmN_WhitespaceAroundEquals_Populates()
        {
            var obs = new List<ParsedObservation>
            {
                new ParsedObservation { DoseRegimen = "Placebo N = 188", ArmN = null },
                new ParsedObservation { DoseRegimen = "Active N= 42", ArmN = null },
                new ParsedObservation { DoseRegimen = "Comparator N =17", ArmN = null }
            };

            PkTableParser.applyDoseRegimenArmNFallback(obs);

            Assert.AreEqual(188, obs[0].ArmN);
            Assert.AreEqual(42, obs[1].ArmN);
            Assert.AreEqual(17, obs[2].ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// R11 — Comma-formatted integers and parenthesized N tokens are
        /// handled. <c>\b</c> word boundaries consume at the digit boundary;
        /// commas inside the number are stripped before parsing.
        /// </summary>
        [TestMethod]
        public void PkParser_R11_DoseRegimenArmN_CommaFormattedParenthesized_Populates()
        {
            var obs = new List<ParsedObservation>
            {
                new ParsedObservation { DoseRegimen = "Dose: 100 mg (n=1,234)", ArmN = null }
            };

            PkTableParser.applyDoseRegimenArmNFallback(obs);

            Assert.AreEqual(1234, obs[0].ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// R11 — Caption or row-label precedence: an ArmN already set MUST
        /// NOT be overridden by a DoseRegimen-derived value, even when the
        /// DoseRegimen contains a different N= token.
        /// </summary>
        [TestMethod]
        public void PkParser_R11_DoseRegimenArmN_ExistingArmN_NotOverridden()
        {
            var obs = new List<ParsedObservation>
            {
                new ParsedObservation
                {
                    DoseRegimen = "Subjects given 100 mg N=99",
                    ArmN = 36 // pre-set by caption or row label
                }
            };

            PkTableParser.applyDoseRegimenArmNFallback(obs);

            Assert.AreEqual(36, obs[0].ArmN, "Caption/row-label ArmN must be preserved");
            Assert.IsFalse((obs[0].ValidationFlags ?? "").Contains("PK_DOSE_REGIMEN_ARMN_FALLBACK"),
                "Fallback flag must not be appended when ArmN was already set");
        }

        /**************************************************************/
        /// <summary>
        /// R11 — Guard: a DoseRegimen with no N= token leaves ArmN untouched
        /// and emits no flag.
        /// </summary>
        [TestMethod]
        public void PkParser_R11_DoseRegimenArmN_NoNToken_NoMutation()
        {
            var obs = new List<ParsedObservation>
            {
                new ParsedObservation { DoseRegimen = "50 mg once daily for 7 days", ArmN = null }
            };

            PkTableParser.applyDoseRegimenArmNFallback(obs);

            Assert.IsNull(obs[0].ArmN);
            Assert.IsFalse((obs[0].ValidationFlags ?? "").Contains("PK_DOSE_REGIMEN_ARMN_FALLBACK"));
        }

        /**************************************************************/
        /// <summary>
        /// R11 — Guard: a malformed N= token ("N=abc", pure alpha after the
        /// equals) must not crash and must not mutate. Because <c>\b...\b</c>
        /// anchors the match on digit characters, this actually fails to
        /// match at all — the regex never fires on non-numeric targets.
        /// </summary>
        [TestMethod]
        public void PkParser_R11_DoseRegimenArmN_NonNumericValue_NoMutation()
        {
            var obs = new List<ParsedObservation>
            {
                new ParsedObservation { DoseRegimen = "iron N = abc", ArmN = null }
            };

            PkTableParser.applyDoseRegimenArmNFallback(obs);

            Assert.IsNull(obs[0].ArmN, "Non-numeric N= value must not be parsed as ArmN");
        }

        /**************************************************************/
        /// <summary>
        /// R11 — Guard: null or whitespace DoseRegimen is a no-op (no NREs).
        /// </summary>
        [TestMethod]
        public void PkParser_R11_DoseRegimenArmN_NullOrEmpty_NoMutation()
        {
            var obs = new List<ParsedObservation>
            {
                new ParsedObservation { DoseRegimen = null, ArmN = null },
                new ParsedObservation { DoseRegimen = "", ArmN = null },
                new ParsedObservation { DoseRegimen = "   ", ArmN = null }
            };

            PkTableParser.applyDoseRegimenArmNFallback(obs);

            Assert.IsTrue(obs.All(o => o.ArmN == null));
            Assert.IsTrue(obs.All(o => string.IsNullOrEmpty(o.ValidationFlags)));
        }

        #endregion PK Wave 3 R11 — ArmN from DoseRegimen Fallback

        #region PK R14 — Post-Iter9 Arm-Routing Hygiene (Exposure, Age Group)

        /**************************************************************/
        /// <summary>
        /// R14 — "Exposure" (and plural/compound variants) is a spanning-header
        /// / column-group label and must NEVER route to <see cref="ParsedObservation.TreatmentArm"/>.
        /// Observed post-Iter9 in TID 2574 (Rivaroxaban CLr-stratified table) where
        /// 51 rows leaked to Arm="Exposure". Negative-list entry forces the classifier
        /// to Unknown so the pre-R1 fallback (col 0 → DoseRegimen) applies.
        /// </summary>
        [TestMethod]
        public void PkParser_R14_ExposureDoesNotRouteToTreatmentArm()
        {
            var table = createTestTable(
                new[] { "Label", "CLr (mL/min)" },
                new List<string?[]>
                {
                    new[] { "Exposure",       "44" },
                    new[] { "Exposures",      "52" },
                    new[] { "Mean Exposure",  "64" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)),
                "Exposure / Exposures / Mean Exposure must never route to TreatmentArm");
        }

        /**************************************************************/
        /// <summary>
        /// R14 — Age-Group compound labels ("Pediatric Age Group", "Adult Age Group",
        /// etc.) route to Population, NOT TreatmentArm. Observed post-Iter9 in
        /// TID 25038 (Palonosetron pediatric table) where R12-rescued rows had
        /// Arm="Pediatric Age Group". PopulationDetector dictionary entries (R14)
        /// force correct routing via <c>classifyRowLabel</c>'s Population match
        /// step, which runs before the drug-name heuristic.
        /// </summary>
        [TestMethod]
        public void PkParser_R14_AgeGroupRoutesToPopulation()
        {
            // Use a neutral col-0 header ("Regimen") — matches the shape of the
            // existing PkParser_R1_1_BareAgeStratumRoutesToPopulation test. The
            // parser's classifyRowLabel inspects each row's col 0 text regardless
            // of the column header label.
            var table = createTestTable(
                new[] { "Regimen", "AUC0-inf (ng·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Pediatric Age Group", "103.5 (40.4)" },
                    new[] { "Adult Age Group",     "98.7 (47.7)" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)),
                "Age Group labels must NEVER route to TreatmentArm");
            Assert.IsTrue(results.Any(r => r.Population == "Pediatric"),
                "Pediatric Age Group must resolve to Population=Pediatric");
            Assert.IsTrue(results.Any(r => r.Population == "Adult"),
                "Adult Age Group must resolve to Population=Adult");
        }

        /**************************************************************/
        /// <summary>
        /// R14 — Regression guard: genuine drug-name labels still route to
        /// <see cref="ParsedObservation.TreatmentArm"/>. Ensures R14 additions
        /// didn't accidentally blacklist or mis-route drug content.
        /// </summary>
        [TestMethod]
        public void PkParser_R14_DrugNameStillRoutesToTreatmentArm()
        {
            var table = createTestTable(
                new[] { "Drug", "Cmax (ng/mL)" },
                new List<string?[]>
                {
                    new[] { "Rivaroxaban",  "250" },
                    new[] { "Palonosetron", "5.5" },
                    new[] { "Losartan",     "3.9 (1.9)" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "Rivaroxaban"));
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "Palonosetron"));
            Assert.IsTrue(results.Any(r => r.TreatmentArm == "Losartan"));
        }

        #endregion PK R14 — Post-Iter9 Arm-Routing Hygiene (Exposure, Age Group)
    }
}

