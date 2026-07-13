using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    public partial class TableParserTests
    {
        #region SimpleArmTableParser Tests

        /**************************************************************/
        /// <summary>
        /// Simple arm parser extracts arms from header and maps data cells.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_BasicAeTable_ProducesCorrectObservations()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug A (N=188) n(%)", "Placebo (N=183) n(%)" },
                new List<string?[]>
                {
                    new[] { "Nausea", "33 (17.6)", "10 (5.5)" },
                    new[] { "Headache", "25 (13.3)", "20 (10.9)" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count); // 2 rows × 2 arms
            Assert.IsTrue(results.All(r => r.TableCategory == "ADVERSE_EVENT"));

            var nausea_drugA = results.First(r => r.ParameterName == "Nausea" && r.TreatmentArm == "Drug A");
            Assert.AreEqual(17.6, nausea_drugA.PrimaryValue);
            Assert.AreEqual(188, nausea_drugA.ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// Simple arm parser detects subtype rows (empty data cells with parameter name).
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_DetectsSubtypeRows()
        {
            var table = createTestTable(
                new[] { "Endpoint", "Drug A (N=100)", "Placebo (N=100)" },
                new List<string?[]>
                {
                    new[] { "Components of primary endpoint", null, null },
                    new[] { "Death", "5 (5.0)", "10 (10.0)" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count); // only Death row produces data
            // In AE context, empty-data rows set ParameterCategory (SOC) not ParameterSubtype
            Assert.AreEqual("Components of primary endpoint", results[0].ParameterCategory);
        }

        #endregion SimpleArmTableParser Tests

        #region MultilevelAeTableParser Tests

        /**************************************************************/
        /// <summary>
        /// Multilevel AE parser assigns study context from colspan header path.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_AssignsStudyContext()
        {
            var table = createMultilevelTable(
                new[] { "Treatment", "Treatment", "Prevention", "Prevention" },
                new[] { "EVISTA (N=2557) %", "Placebo (N=2576) %", "EVISTA (N=581) %", "Placebo (N=584) %" },
                new List<string?[]>
                {
                    new[] { "Hot Flashes", "24.6", "18.3", "28.7", "21.2" }
                });

            var parser = new MultilevelAeTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            var treatmentEvista = results.First(r => r.TreatmentArm == "EVISTA" && r.StudyContext == "Treatment");
            Assert.AreEqual(24.6, treatmentEvista.PrimaryValue);
            Assert.AreEqual("Percentage", treatmentEvista.PrimaryValueType);
        }

        /**************************************************************/
        /// <summary>
        /// Multilevel AE parser propagates SOC from divider rows.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_PropagatesSocCategory()
        {
            var table = createMultilevelTable(
                new[] { "Treatment", "Treatment" },
                new[] { "Drug (N=100) %", "Placebo (N=100) %" },
                new List<string?[]>
                {
                    new[] { "Nausea", "5.0", "2.0" }
                });

            insertSocDivider(table, 0, "Body as a Whole");

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            Assert.IsTrue(results.All(r => r.ParameterCategory == "Body as a Whole"));
        }

        /**************************************************************/
        /// <summary>
        /// Multilevel AE parser detects mid-body subpopulation header rows
        /// ("Female Patients Only" with per-arm (N=…) cells), suppresses them,
        /// and stamps Subpopulation + per-arm overridden ArmN on subsequent rows.
        /// Mirrors TextTableID 44661 (Clomipramine).
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_SubpopulationHeader_OverridesArmNAndStampsLabel()
        {
            var table = createMultilevelTable(
                new[] { "Adults", "Adults", "Children and Adolescents", "Children and Adolescents" },
                new[] { "Clomipramine (N=322) %", "Placebo (N=319) %", "Clomipramine (N=46) %", "Placebo (N=44) %" },
                new List<string?[]>
                {
                    // Whole-study row first (Adults clomipramine ArmN should be 322).
                    new[] { "Headache", "52", "41", "28", "34" },
                    // Mid-body subpopulation header — should be suppressed.
                    new[] { "Female Patients Only", "(N=182)", "(N=167)", "(N=10)", "(N=21)" },
                    // Within the female-only section.
                    new[] { "Dysmenorrhea", "12", "14", "10", "10" }
                });

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            // The subpopulation header itself must not produce observations.
            Assert.IsFalse(results.Any(r => r.ParameterName == "Female Patients Only"),
                "Subpopulation header must be suppressed.");

            // Headache (before the partition): no Subpopulation, ArmN from header (322 / 319 / 46 / 44).
            var headacheClomi = results.Single(r => r.ParameterName == "Headache" && r.StudyContext == "Adults" && r.TreatmentArm == "Clomipramine");
            Assert.IsNull(headacheClomi.Subpopulation, "Pre-partition rows have no Subpopulation.");
            Assert.AreEqual(322, headacheClomi.ArmN);

            // Dysmenorrhea (within female-only): Subpopulation set, ArmN from partition row.
            var dysmenAdults = results.Single(r => r.ParameterName == "Dysmenorrhea" && r.StudyContext == "Adults" && r.TreatmentArm == "Clomipramine");
            Assert.AreEqual("Female Patients Only", dysmenAdults.Subpopulation);
            Assert.AreEqual(182, dysmenAdults.ArmN, "Dysmenorrhea must inherit female-only ArmN (182), not whole-study (322).");

            var dysmenAdultsPlacebo = results.Single(r => r.ParameterName == "Dysmenorrhea" && r.StudyContext == "Adults" && r.TreatmentArm == "Placebo");
            Assert.AreEqual(167, dysmenAdultsPlacebo.ArmN);

            var dysmenChildren = results.Single(r => r.ParameterName == "Dysmenorrhea" && r.StudyContext == "Children and Adolescents" && r.TreatmentArm == "Clomipramine");
            Assert.AreEqual(10, dysmenChildren.ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// "Male and Female Patients Combined" must not be classified as a subpopulation
        /// header (its data cells are all dashes — there is no parsed N). It must be
        /// suppressed AND must reset any active subpopulation context.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_CombinedRow_SuppressesAndResetsSubpopulation()
        {
            var table = createMultilevelTable(
                new[] { "Adults", "Adults" },
                new[] { "Clomipramine (N=322) %", "Placebo (N=319) %" },
                new List<string?[]>
                {
                    new[] { "Female Patients Only", "(N=182)", "(N=167)" },
                    new[] { "Dysmenorrhea", "12", "14" },
                    // Combined / all-patients reset row — dash-only data cells.
                    new[] { "Male and Female Patients Combined", "-", "-" },
                    // After reset, this row should be back to whole-study Ns.
                    new[] { "Headache", "52", "41" }
                });

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            Assert.IsFalse(results.Any(r => r.ParameterName == "Male and Female Patients Combined"),
                "Combined row must be suppressed.");

            var headache = results.Single(r => r.ParameterName == "Headache" && r.TreatmentArm == "Clomipramine");
            Assert.IsNull(headache.Subpopulation, "Combined row must reset Subpopulation context.");
            Assert.AreEqual(322, headache.ArmN, "Post-reset ArmN must revert to whole-study (322).");
        }

        /**************************************************************/
        /// <summary>
        /// Partial-N subpopulation header (only 3 of 4 arms have new Ns): the override
        /// applies only to the columns with parsed Ns; arms missing an N fall back to
        /// the header-derived <c>arm.SampleSize</c>.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_PartialNSubpopulation_OverridesOnlyParsedColumns()
        {
            var table = createMultilevelTable(
                new[] { "Adults", "Adults", "Children", "Children" },
                new[] { "Drug (N=322) %", "Placebo (N=319) %", "Drug (N=46) %", "Placebo (N=44) %" },
                new List<string?[]>
                {
                    // Subpopulation header with N for arms 0, 1, 2 but a dash for arm 3.
                    new[] { "Female Patients Only", "(N=182)", "(N=167)", "(N=10)", "-" },
                    new[] { "Dysmenorrhea", "12", "14", "10", "10" }
                });

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            var dysmenChildrenPlacebo = results.Single(r => r.ParameterName == "Dysmenorrhea" && r.StudyContext == "Children" && r.TreatmentArm == "Placebo");
            // No N parsed for the Children Placebo arm — ArmN must fall back to header (44).
            Assert.AreEqual(44, dysmenChildrenPlacebo.ArmN,
                "Arms missing an N on the partition row must fall back to header SampleSize.");

            var dysmenChildrenDrug = results.Single(r => r.ParameterName == "Dysmenorrhea" && r.StudyContext == "Children" && r.TreatmentArm == "Drug");
            Assert.AreEqual(10, dysmenChildrenDrug.ArmN, "Arms with parsed N must use the override.");
        }

        /**************************************************************/
        /// <summary>
        /// SOC divider row (e.g. "Body as a Whole") must reset subpopulation context,
        /// not just SOC. After the SOC divider, ArmN reverts to whole-study, Subpop
        /// is null.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_SocDivider_ResetsSubpopulationContext()
        {
            var table = createMultilevelTable(
                new[] { "Adults", "Adults" },
                new[] { "Drug (N=322) %", "Placebo (N=319) %" },
                new List<string?[]>
                {
                    new[] { "Female Patients Only", "(N=182)", "(N=167)" },
                    new[] { "Dysmenorrhea", "12", "14" },
                    // Then a real data row after a SOC divider.
                    new[] { "Headache", "52", "41" }
                });
            insertSocDivider(table, 2, "Body as a Whole"); // before the Headache row

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            var headache = results.Single(r => r.ParameterName == "Headache" && r.TreatmentArm == "Drug");
            Assert.IsNull(headache.Subpopulation,
                "SOC divider must reset Subpopulation alongside the SOC change.");
            Assert.AreEqual(322, headache.ArmN, "SOC divider must reset arm N overrides.");
            Assert.AreEqual("Body as a Whole", headache.ParameterCategory);
        }

        #endregion MultilevelAeTableParser Tests

        #region AeWithSocTableParser Tests

        /**************************************************************/
        /// <summary>
        /// AE with SOC parser propagates SOC name to ParameterCategory.
        /// </summary>
        [TestMethod]
        public void AeWithSocParser_PropagatesSocName()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (N=100)", "Placebo (N=100)" },
                new List<string?[]>
                {
                    new[] { "Nausea", "10 (10.0)", "5 (5.0)" },
                    new[] { "Vomiting", "3 (3.0)", "1 (1.0)" }
                },
                parentSectionCode: "34084-4");

            table.HasSocDividers = true;
            insertSocDivider(table, 0, "Gastrointestinal");

            var parser = new AeWithSocTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);

            Assert.IsTrue(results.All(r => r.ParameterCategory == "Gastrointestinal"));
            Assert.AreEqual(4, results.Count);
        }

        /**************************************************************/
        /// <summary>
        /// AeWithSocTableParser must apply the same subpopulation detection +
        /// per-arm ArmN override + label stamping that MultilevelAeTableParser does.
        /// Single-header AE table (no colspan), with a SOC divider followed by a
        /// mid-body subpopulation header.
        /// </summary>
        [TestMethod]
        public void AeWithSocParser_SubpopulationHeader_OverridesArmNAndStampsLabel()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (N=200)", "Placebo (N=200)" },
                new List<string?[]>
                {
                    new[] { "Headache", "20", "10" },
                    // Mid-body subpopulation header (must be suppressed).
                    new[] { "Female Patients Only", "(N=120)", "(N=110)" },
                    new[] { "Dysmenorrhea", "12", "14" }
                },
                parentSectionCode: "34084-4");
            table.HasSocDividers = true;
            insertSocDivider(table, 0, "Body as a Whole");

            var parser = new AeWithSocTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);

            Assert.IsFalse(results.Any(r => r.ParameterName == "Female Patients Only"),
                "Subpopulation header must be suppressed by AeWithSoc parser.");

            var headacheDrug = results.Single(r => r.ParameterName == "Headache" && r.TreatmentArm == "Drug");
            Assert.IsNull(headacheDrug.Subpopulation);
            Assert.AreEqual(200, headacheDrug.ArmN);
            Assert.AreEqual("Body as a Whole", headacheDrug.ParameterCategory);

            var dysmenDrug = results.Single(r => r.ParameterName == "Dysmenorrhea" && r.TreatmentArm == "Drug");
            Assert.AreEqual("Female Patients Only", dysmenDrug.Subpopulation);
            Assert.AreEqual(120, dysmenDrug.ArmN, "Dysmenorrhea must inherit female-only ArmN (120).");

            var dysmenPlacebo = results.Single(r => r.ParameterName == "Dysmenorrhea" && r.TreatmentArm == "Placebo");
            Assert.AreEqual(110, dysmenPlacebo.ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// AeWithSoc parser resets subpopulation context at the next SOC divider,
        /// independently of the MultilevelAeTableParser path.
        /// </summary>
        [TestMethod]
        public void AeWithSocParser_SocDivider_ResetsSubpopulationContext()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (N=200)", "Placebo (N=200)" },
                new List<string?[]>
                {
                    new[] { "Female Patients Only", "(N=120)", "(N=110)" },
                    new[] { "Dysmenorrhea", "12", "14" },
                    // Post-SOC, post-reset.
                    new[] { "Headache", "20", "10" }
                },
                parentSectionCode: "34084-4");
            table.HasSocDividers = true;
            // SOC divider after the female-only data row, before Headache.
            insertSocDivider(table, 2, "Body as a Whole");

            var parser = new AeWithSocTableParser();
            var results = parser.Parse(table);

            var headache = results.Single(r => r.ParameterName == "Headache" && r.TreatmentArm == "Drug");
            Assert.IsNull(headache.Subpopulation, "SOC divider must reset Subpopulation in AeWithSoc parser.");
            Assert.AreEqual(200, headache.ArmN, "ArmN must revert to whole-study (200) after SOC reset.");
        }

        #endregion AeWithSocTableParser Tests

        #region SimpleArmTableParser — Subpopulation Tests (AE-only)

        /**************************************************************/
        /// <summary>
        /// SimpleArmTableParser in AE mode applies subpopulation detection identically
        /// to the other AE parsers. Validates the third wiring path.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_AeMode_SubpopulationHeader_OverridesArmNAndStampsLabel()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug A (N=188) n(%)", "Placebo (N=183) n(%)" },
                new List<string?[]>
                {
                    new[] { "Nausea", "33 (17.6)", "10 (5.5)" },
                    new[] { "Female Patients Only", "(N=110)", "(N=105)" },
                    new[] { "Dysmenorrhea", "12 (10.9)", "14 (13.3)" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.IsFalse(results.Any(r => r.ParameterName == "Female Patients Only"),
                "Subpopulation header must be suppressed by SimpleArm parser in AE mode.");

            var nauseaDrug = results.Single(r => r.ParameterName == "Nausea" && r.TreatmentArm == "Drug A");
            Assert.IsNull(nauseaDrug.Subpopulation);
            Assert.AreEqual(188, nauseaDrug.ArmN);

            var dysmenDrug = results.Single(r => r.ParameterName == "Dysmenorrhea" && r.TreatmentArm == "Drug A");
            Assert.AreEqual("Female Patients Only", dysmenDrug.Subpopulation);
            Assert.AreEqual(110, dysmenDrug.ArmN, "Dysmenorrhea must inherit female-only ArmN (110).");

            var dysmenPlacebo = results.Single(r => r.ParameterName == "Dysmenorrhea" && r.TreatmentArm == "Placebo");
            Assert.AreEqual(105, dysmenPlacebo.ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// SimpleArmTableParser in AE mode: empty-data context row (which acts as a
        /// SOC divider in this parser) clears subpopulation state.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_AeMode_EmptyDataContextRow_ResetsSubpopulationContext()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug A (N=188)", "Placebo (N=183)" },
                new List<string?[]>
                {
                    new[] { "Female Patients Only", "(N=110)", "(N=105)" },
                    new[] { "Dysmenorrhea", "12 (10.9)", "14 (13.3)" },
                    // Empty-data context row — should reset subpop.
                    new[] { "Body as a Whole", null, null },
                    new[] { "Headache", "25 (13.3)", "20 (10.9)" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            var headache = results.Single(r => r.ParameterName == "Headache" && r.TreatmentArm == "Drug A");
            Assert.IsNull(headache.Subpopulation,
                "Empty-data context row must reset Subpopulation in SimpleArm AE-mode.");
            Assert.AreEqual(188, headache.ArmN, "ArmN must revert to whole-study (188).");
            Assert.AreEqual("Body as a Whole", headache.ParameterCategory);
        }

        /**************************************************************/
        /// <summary>
        /// SimpleArmTableParser in Efficacy mode must NEVER stamp Subpopulation —
        /// the wiring is gated by `category == ADVERSE_EVENT`. Even a row that looks
        /// exactly like an AE subpopulation header passes through the Efficacy path
        /// without subpop side-effects.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_EfficacyMode_DoesNotStampSubpopulation()
        {
            var table = createTestTable(
                new[] { "Endpoint", "Drug A (N=188)", "Placebo (N=183)" },
                new List<string?[]>
                {
                    new[] { "Overall Survival", "0.85 (0.72-0.99)", "1.00 (0.85-1.18)" },
                    // A row whose label matches the AE-subpop pattern shape.
                    // Efficacy mode must not interpret it as a subpop header.
                    new[] { "Female Patients Only", "(N=120)", "(N=110)" }
                });

            var parser = new SimpleArmTableParser();
            var results = parser.parseInternal(table, TableCategory.EFFICACY);

            Assert.IsTrue(results.All(r => string.IsNullOrEmpty(r.Subpopulation)),
                "Efficacy mode must never set Subpopulation.");
        }

        #endregion SimpleArmTableParser — Subpopulation Tests (AE-only)
    }
}

