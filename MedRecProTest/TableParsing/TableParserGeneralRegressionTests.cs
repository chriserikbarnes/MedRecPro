using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    public partial class TableParserTests
    {
        #region Arm Header Parsing Tests

        /**************************************************************/
        /// <summary>
        /// Verifies lowercase n with spaces in parenthesized format is parsed correctly.
        /// Covers Issue 1: "Paroxetine (n = 421) %" was not matched by original regex.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_LowercaseNWithSpaces_ExtractsCorrectly()
        {
            #region implementation

            var arm = ValueParser.ParseArmHeader("Paroxetine (n = 421) %");

            Assert.IsNotNull(arm);
            Assert.AreEqual("Paroxetine", arm.Name);
            Assert.AreEqual(421, arm.SampleSize);
            Assert.AreEqual("%", arm.FormatHint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies no-parentheses N format is parsed correctly.
        /// Covers Issue 4: "Placebo n = 51 %" has no parentheses around N clause.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_NoParentheses_ExtractsCorrectly()
        {
            #region implementation

            var arm = ValueParser.ParseArmHeader("Placebo n = 51 %");

            Assert.IsNotNull(arm);
            Assert.AreEqual("Placebo", arm.Name);
            Assert.AreEqual(51, arm.SampleSize);
            Assert.AreEqual("%", arm.FormatHint);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Regression guard: original uppercase N with no spaces still works.
        /// </summary>
        [TestMethod]
        public void ParseArmHeader_UppercaseNNoSpaces_StillWorks()
        {
            #region implementation

            var arm = ValueParser.ParseArmHeader("EVISTA(N=2557)n(%)");

            Assert.IsNotNull(arm);
            Assert.AreEqual("EVISTA", arm.Name);
            Assert.AreEqual(2557, arm.SampleSize);
            Assert.AreEqual("n(%)", arm.FormatHint);

            #endregion
        }

        #endregion Arm Header Parsing Tests

        #region SimpleArmTableParser — AE Category Propagation Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that empty-data rows in AE tables set ParameterCategory (SOC)
        /// rather than ParameterSubtype.
        /// Covers Issue 2: ParameterCategory was NULL for all rows.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_AeTable_EmptyDataRowsSetsCategory()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Body System/ Adverse Reaction", "Paroxetine (n = 421) %", "Placebo (n = 421) %" },
                new List<string?[]>
                {
                    new[] { "Body as a Whole", null, null },
                    new[] { "Headache", "18", "17" },
                    new[] { "Asthenia", "15", "6" },
                    new[] { "Cardiovascular", null, null },
                    new[] { "Palpitation", "3", "1" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            // 3 data rows x 2 arms = 6 observations
            Assert.AreEqual(6, results.Count);

            var headache = results.First(r => r.ParameterName == "Headache" && r.TreatmentArm == "Paroxetine");
            Assert.AreEqual("Body as a Whole", headache.ParameterCategory);
            Assert.AreEqual(18.0, headache.PrimaryValue);
            Assert.AreEqual(421, headache.ArmN);

            var palpitation = results.First(r => r.ParameterName == "Palpitation" && r.TreatmentArm == "Paroxetine");
            Assert.AreEqual("Cardiovascular", palpitation.ParameterCategory);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies bare numbers are promoted to Percentage when arm header contains "%".
        /// Covers Issue 3: PrimaryValueType was "Numeric" instead of "Percentage".
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_LowercaseNHeader_PromotesToPercentage()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (n = 100) %", "Placebo (n = 100) %" },
                new List<string?[]>
                {
                    new[] { "Nausea", "15", "5" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.PrimaryValueType == "Percentage"));
            Assert.IsTrue(results.All(r => r.ArmN == 100));
            Assert.IsTrue(results.All(r => r.Unit == "%"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies no-parentheses N format extracts ArmN and promotes to Percentage.
        /// Covers Issue 4: "N = 51 %" in Table 7 was not parsed.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_NoParenNFormat_ExtractsArmN()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug n = 51 %", "Placebo n = 48 %" },
                new List<string?[]>
                {
                    new[] { "Headache", "10", "8" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            var drug = results.First(r => r.TreatmentArm == "Drug");
            Assert.AreEqual(51, drug.ArmN);
            Assert.AreEqual("Percentage", drug.PrimaryValueType);

            var placebo = results.First(r => r.TreatmentArm == "Placebo");
            Assert.AreEqual(48, placebo.ArmN);
            Assert.AreEqual("Percentage", placebo.PrimaryValueType);

            #endregion
        }

        #endregion SimpleArmTableParser — AE Category Propagation Tests

        #region MultilevelAeTableParser — Lowercase N Header Tests

        /**************************************************************/
        /// <summary>
        /// Verifies MultilevelAeTableParser correctly parses multi-indication tables
        /// with lowercase n arm headers. StudyContext should capture the indication.
        /// Covers Issue 5: Multi-indication table was entirely skipped.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_LowercaseNHeaders_ParsesCorrectly()
        {
            #region implementation

            var table = createMultilevelTable(
                new[] { "OCD", "OCD", "Panic Disorder", "Panic Disorder" },
                new[] { "Paroxetine (n = 542) %", "Placebo (n = 265) %",
                        "Paroxetine (n = 469) %", "Placebo (n = 324) %" },
                new List<string?[]>
                {
                    new[] { "Nausea", "23", "10", "22", "17" }
                });

            var parser = new MultilevelAeTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);

            var ocdParoxetine = results.First(r =>
                r.StudyContext == "OCD" && r.TreatmentArm == "Paroxetine");
            Assert.AreEqual(542, ocdParoxetine.ArmN);
            Assert.AreEqual("Percentage", ocdParoxetine.PrimaryValueType);
            Assert.AreEqual(23.0, ocdParoxetine.PrimaryValue);

            var panicPlacebo = results.First(r =>
                r.StudyContext == "Panic Disorder" && r.TreatmentArm == "Placebo");
            Assert.AreEqual(324, panicPlacebo.ArmN);
            Assert.AreEqual(17.0, panicPlacebo.PrimaryValue);

            #endregion
        }

        #endregion MultilevelAeTableParser — Lowercase N Header Tests

        #region Trailing Format Hint Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that headers like "Paroxetine %" strip the trailing "%"
        /// into FormatHint, leaving only the drug name as the arm Name.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_TrailingPercentHeader_StripsFormatHint()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Paroxetine %", "Placebo %" },
                new List<string?[]>
                {
                    new[] { "Nausea", "15", "5" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            var parox = results.First(r => r.TreatmentArm == "Paroxetine");
            Assert.AreEqual("Paroxetine", parox.TreatmentArm);
            Assert.AreEqual("Percentage", parox.PrimaryValueType);
            Assert.AreEqual("%", parox.Unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies type promotion works when FormatHint comes from trailing "%" stripping
        /// rather than from the N= regex.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_TrailingPercentHeader_PromotesToPercentage()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug A %", "Drug B %" },
                new List<string?[]>
                {
                    new[] { "Headache", "12", "8" },
                    new[] { "Nausea", "26", "9" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.All(r => r.PrimaryValueType == "Percentage"));

            #endregion
        }

        #endregion Trailing Format Hint Tests

        #region Body Row Enrichment Tests

        /**************************************************************/
        /// <summary>
        /// Verifies that body rows with dose regimen cells (e.g., "10 mg", "20 mg")
        /// are consumed as enrichment and populate DoseRegimen on observations.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_DoseEnrichmentRows_ExtractsDoseRegimen()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Paroxetine", "Paroxetine" },
                new List<string?[]>
                {
                    new[] { "-", "10 mg", "20 mg" },           // dose enrichment row
                    new[] { "Headache", "5", "8" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            // Only the data row should produce observations (dose row consumed)
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual("10 mg", results[0].DoseRegimen);
            Assert.AreEqual(10m, results[0].Dose, "Dose extracted from enrichment row");
            Assert.AreEqual("mg", results[0].DoseUnit);
            Assert.AreEqual("20 mg", results[1].DoseRegimen);
            Assert.AreEqual(20m, results[1].Dose, "Dose extracted from enrichment row");
            Assert.AreEqual("mg", results[1].DoseUnit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that body rows with n= cells are consumed as enrichment
        /// and set ArmN on observations.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_NEqualsEnrichmentRow_SetsArmN()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Paroxetine", "Placebo" },
                new List<string?[]>
                {
                    new[] { "-", "n = 102", "n = 50" },        // N= enrichment row
                    new[] { "Nausea", "15", "5" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(102, results.First(r => r.TreatmentArm == "Paroxetine").ArmN);
            Assert.AreEqual(50, results.First(r => r.TreatmentArm == "Placebo").ArmN);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that body rows with "%" cells are consumed as enrichment
        /// and set FormatHint which drives type promotion.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_FormatHintEnrichmentRow_SetsFormatHint()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Paroxetine", "Placebo" },
                new List<string?[]>
                {
                    new[] { "-", "%", "%" },                    // format hint enrichment row
                    new[] { "Nausea", "15", "5" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.PrimaryValueType == "Percentage"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies that multiple consecutive enrichment rows (dose + N= + format)
        /// are all consumed and applied to arm definitions.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_MultiRowEnrichment_SkipsAllThree()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Paroxetine", "Paroxetine" },
                new List<string?[]>
                {
                    new[] { "-", "10 mg", "20 mg" },           // dose
                    new[] { "-", "n = 102", "n = 104" },       // N=
                    new[] { "-", "%", "%" },                    // format hint
                    new[] { "Nausea", "15", "25" }              // actual data
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            // Only the data row should produce observations
            Assert.AreEqual(2, results.Count);

            var arm10 = results.First(r => r.DoseRegimen == "10 mg");
            Assert.AreEqual(102, arm10.ArmN);
            Assert.AreEqual("Percentage", arm10.PrimaryValueType);
            Assert.AreEqual(15.0, arm10.PrimaryValue);

            var arm20 = results.First(r => r.DoseRegimen == "20 mg");
            Assert.AreEqual(104, arm20.ArmN);
            Assert.AreEqual(25.0, arm20.PrimaryValue);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Verifies MultilevelAeTableParser strips trailing "%" from headers
        /// that have no N= value and promotes to Percentage.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_TrailingPercentHeader_ParsesCorrectly()
        {
            #region implementation

            var table = createMultilevelTable(
                new[] { "MDD", "MDD", "OCD", "OCD" },
                new[] { "Paroxetine %", "Placebo %", "Paroxetine %", "Placebo %" },
                new List<string?[]>
                {
                    new[] { "Nausea", "23", "10", "15", "8" }
                });

            var parser = new MultilevelAeTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);
            Assert.AreEqual(4, results.Count);

            var mddParox = results.First(r => r.StudyContext == "MDD" && r.TreatmentArm == "Paroxetine");
            Assert.AreEqual("Paroxetine", mddParox.TreatmentArm);
            Assert.AreEqual("Percentage", mddParox.PrimaryValueType);
            Assert.AreEqual(23.0, mddParox.PrimaryValue);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Body row with parenthesized N= cells (e.g., "(N=101)" / "(N=98)")
        /// must be consumed as enrichment and populate ArmN on the arms.
        /// This is the minimal regression test for the Table 9 (TextTableID
        /// 203, Topiramate pediatric epilepsy AE table) fix.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_ParenthesizedNEnrichmentRow_SetsArmN()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Placebo", "Topiramate" },
                new List<string?[]>
                {
                    new[] { "-", "(N=101)", "(N=98)" },       // parenthesized N= enrichment row
                    new[] { "Fatigue", "5", "16" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            // Only the data row produces observations; enrichment row is consumed
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(101, results.First(r => r.TreatmentArm == "Placebo").ArmN);
            Assert.AreEqual(98, results.First(r => r.TreatmentArm == "Topiramate").ArmN);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Exact shape seen in Table 9 of TextTableID 203: "(N =101 )" and
        /// "(N =98 )" — leading/trailing whitespace inside the parentheses.
        /// </summary>
        [TestMethod]
        public void SimpleArmParser_ParenthesizedNEnrichmentRow_WithInnerSpaces_SetsArmN()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Placebo", "Topiramate" },
                new List<string?[]>
                {
                    new[] { "-", "(N =101 )", "(N =98 )" },   // messy inner whitespace
                    new[] { "Injury", "13", "14" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(101, results.First(r => r.TreatmentArm == "Placebo").ArmN);
            Assert.AreEqual(98, results.First(r => r.TreatmentArm == "Topiramate").ArmN);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Parenthesized N= enrichment row in an AE table with SOC dividers
        /// (the exact combination in TextTableID 203). Verifies that
        /// <see cref="AeWithSocTableParser"/> propagates ArmN to every
        /// observation after the enrichment row is consumed.
        /// </summary>
        [TestMethod]
        public void AeWithSocParser_ParenthesizedNEnrichmentRow_PropagatesArmN()
        {
            #region implementation

            var table = createTestTable(
                new[] { "Adverse Reaction", "Placebo", "Topiramate" },
                new List<string?[]>
                {
                    new[] { "-", "(N =101 )", "(N =98 )" },
                    new[] { "Fatigue", "5", "16" },
                    new[] { "Injury", "13", "14" }
                },
                parentSectionCode: "34084-4");

            table.HasSocDividers = true;
            insertSocDivider(table, 1, "Body as a Whole - General Disorders");

            var parser = new AeWithSocTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count); // 2 params × 2 arms
            Assert.IsTrue(results.Where(r => r.TreatmentArm == "Placebo").All(r => r.ArmN == 101));
            Assert.IsTrue(results.Where(r => r.TreatmentArm == "Topiramate").All(r => r.ArmN == 98));
            Assert.IsTrue(results.All(r => r.ParameterCategory == "Body as a Whole - General Disorders"));

            #endregion
        }

        #endregion Body Row Enrichment Tests

        #region Caption StudyContext Extraction Tests

        // Concrete parser subclass used to exercise the protected-internal
        // BaseTableParser.extractStudyContextFromCaption helper directly.
        private sealed class CaptionStudyContextProbe : BaseTableParser
        {
            public override TableCategory SupportedCategory => TableCategory.ADVERSE_EVENT;
            public override int Priority => 999;
            public override bool CanParse(ReconstructedTable table) => false;
            public override List<ParsedObservation> Parse(ReconstructedTable table) => new();
            public static string? Extract(string? caption) => extractStudyContextFromCaption(caption);
        }

        /**************************************************************/
        /// <summary>
        /// The Table 9 (TextTableID 203) caption should produce a non-null
        /// StudyContext that preserves the full trial descriptor including
        /// the "Placebo-Controlled," qualifier.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_Table203Caption()
        {
            #region implementation

            const string caption =
                "Table 9: Incidence (%) of Treatment-Emergent Adverse Reactions in " +
                "Placebo-Controlled, Add-On Epilepsy Trials in Pediatric Patients " +
                "(Ages 2 -16 Years)<sup>*</sup>,<sup>+</sup>" +
                "(Reactions That Occurred in at Least 1% of Topiramate Tablets-Treated " +
                "Patients and Occurred More Frequently in Topiramate Tablets -Treated " +
                "Than Placebo-Treated Patients)";

            var result = CaptionStudyContextProbe.Extract(caption);

            Assert.IsNotNull(result);
            StringAssert.Contains(result!, "Add-On Epilepsy Trials");
            StringAssert.Contains(result, "Pediatric Patients");
            StringAssert.Contains(result, "Ages 2 -16 Years");
            StringAssert.StartsWith(result, "Placebo-Controlled");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Trailing <sup>*</sup> / <sup>†</sup> HTML footnote markers must
        /// be stripped from the extracted descriptor.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_StripsSupFootnote()
        {
            #region implementation

            const string caption =
                "Table 3: Adverse Reactions Reported in Clinical Trials of " +
                "Adult Patients With Hypertension<sup>*</sup>";

            var result = CaptionStudyContextProbe.Extract(caption);

            Assert.IsNotNull(result);
            Assert.IsFalse(result!.Contains("<sup>"), "HTML residue should be stripped");
            Assert.IsFalse(result.Contains('*'), "Bare footnote marker should be stripped");
            StringAssert.Contains(result, "Clinical Trials of Adult Patients With Hypertension");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Captions without an AE measure phrase (e.g., PK summaries) must
        /// return null — no guessing allowed.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_MissingMeasurePhrase_ReturnsNull()
        {
            #region implementation

            Assert.IsNull(CaptionStudyContextProbe.Extract(
                "Table 5: Clinical Pharmacology Summary for Healthy Volunteers"));
            Assert.IsNull(CaptionStudyContextProbe.Extract(
                "Table 1: Demographics of Study Population"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Captions with a measure phrase but no trial-descriptor connector
        /// (no "in"/"during"/"from"/etc.) must return null.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_MissingConnector_ReturnsNull()
        {
            #region implementation

            // Trailing punctuation only, no connector introducing a trial descriptor
            Assert.IsNull(CaptionStudyContextProbe.Extract("Table 4: Adverse Reactions."));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Null / empty / whitespace captions short-circuit to null.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_NullOrEmpty_ReturnsNull()
        {
            #region implementation

            Assert.IsNull(CaptionStudyContextProbe.Extract(null));
            Assert.IsNull(CaptionStudyContextProbe.Extract(""));
            Assert.IsNull(CaptionStudyContextProbe.Extract("   "));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Real PK captions must return null — confirms the helper can be
        /// called indiscriminately without polluting non-AE parser output.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_PkCaption_ReturnsNull()
        {
            #region implementation

            Assert.IsNull(CaptionStudyContextProbe.Extract(
                "Table 2: Mean PK Parameters in Healthy Volunteers"));
            Assert.IsNull(CaptionStudyContextProbe.Extract(
                "Table 4: Pharmacokinetic Parameters Following Single-Dose " +
                "Administration in Subjects with Renal Impairment"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Locks in the accepted connector list: each connector word must
        /// successfully split the measure phrase from the trial descriptor.
        /// </summary>
        [TestMethod]
        public void BaseTableParser_ExtractStudyContextFromCaption_AlternateConnectors()
        {
            #region implementation

            Assert.AreEqual("the Double-Blind Phase of Study XYZ",
                CaptionStudyContextProbe.Extract(
                    "Table 5: Adverse Events During the Double-Blind Phase of Study XYZ"));

            Assert.AreEqual("Pooled Phase 3 Trials",
                CaptionStudyContextProbe.Extract(
                    "Table 6: Adverse Reactions Reported in Pooled Phase 3 Trials"));

            Assert.AreEqual("Patients with Renal Impairment",
                CaptionStudyContextProbe.Extract(
                    "Table 7: Adverse Events Observed in Patients with Renal Impairment"));

            Assert.AreEqual("Study ABC-123",
                CaptionStudyContextProbe.Extract(
                    "Table 8: Treatment-Emergent Adverse Reactions from Study ABC-123"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Full AeWithSocTableParser flow: when the header offers no
        /// StudyContext but the caption matches the AE grammar, every
        /// observation should inherit the caption-derived descriptor.
        /// </summary>
        [TestMethod]
        public void AeWithSocParser_CaptionStudyContextFallback_PopulatesObservations()
        {
            #region implementation

            const string caption =
                "Table 9: Incidence (%) of Treatment-Emergent Adverse Reactions in " +
                "Placebo-Controlled, Add-On Epilepsy Trials in Pediatric Patients " +
                "(Ages 2 -16 Years)";

            var table = createTestTable(
                new[] { "Adverse Reaction", "Placebo", "Topiramate" },
                new List<string?[]>
                {
                    new[] { "-", "(N =101 )", "(N =98 )" },
                    new[] { "Fatigue", "5", "16" }
                },
                caption: caption,
                parentSectionCode: "34084-4");

            table.HasSocDividers = true;
            insertSocDivider(table, 1, "Body as a Whole - General Disorders");

            var parser = new AeWithSocTableParser();
            var results = parser.Parse(table);

            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results.All(r => r.StudyContext != null));
            Assert.IsTrue(results.All(r => r.StudyContext!.Contains("Add-On Epilepsy Trials")));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// Multilevel AE parser: when both a header-derived StudyContext
        /// AND a caption-extractable context are available, the
        /// header-derived value must win.
        /// </summary>
        [TestMethod]
        public void MultilevelAeParser_HeaderStudyContextWinsOverCaption()
        {
            #region implementation

            const string caption =
                "Table 10: Adverse Reactions Reported in Pooled Phase 3 Trials";

            var table = createMultilevelTable(
                new[] { "Treatment", "Treatment", "Prevention", "Prevention" },
                new[] { "EVISTA (N=2557) %", "Placebo (N=2576) %", "EVISTA (N=581) %", "Placebo (N=584) %" },
                new List<string?[]>
                {
                    new[] { "Hot Flashes", "24.6", "18.3", "28.7", "21.2" }
                },
                caption: caption);

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            // Header path carries "Treatment" / "Prevention" — caption fallback must NOT overwrite
            Assert.IsTrue(results.All(r => r.StudyContext == "Treatment" || r.StudyContext == "Prevention"));
            Assert.IsFalse(results.Any(r => r.StudyContext == "Pooled Phase 3 Trials"));

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// When the caption does not match the AE grammar, AeWithSocTableParser
        /// must leave StudyContext as null rather than inject garbage.
        /// </summary>
        [TestMethod]
        public void AeWithSocParser_NonAeCaption_LeavesStudyContextNull()
        {
            #region implementation

            const string caption = "Table 12: Demographics of Study Population";

            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (N=100)", "Placebo (N=100)" },
                new List<string?[]>
                {
                    new[] { "Nausea", "10 (10.0)", "5 (5.0)" }
                },
                caption: caption,
                parentSectionCode: "34084-4");

            table.HasSocDividers = true;
            insertSocDivider(table, 0, "Gastrointestinal");

            var parser = new AeWithSocTableParser();
            var results = parser.Parse(table);

            Assert.IsTrue(results.Count > 0);
            Assert.IsTrue(results.All(r => r.StudyContext == null));

            #endregion
        }

        #endregion Caption StudyContext Extraction Tests

        #region PK Transposed Layout Relaxation — Long-Form English Row Labels

        /**************************************************************/
        /// <summary>
        /// Mirrors TextTableID=13202/22685/33852 (Ceftriaxone): col 0 header is
        /// blank/missing, dose headers occupy cols 1..n, row labels use long-form
        /// English PK names. Verifies the relaxed transposed-layout detection
        /// recovers observations and canonicalizes ParameterName via the
        /// <see cref="PkParameterDictionary"/>.
        /// </summary>
        [TestMethod]
        public void PkParser_TransposedLayout_BlankCol0Header_RecoversObservations()
        {
            var table = createTestTable(
                new[] { null, "50 mg/kg IV", "75 mg/kg IV" },
                new List<string?[]>
                {
                    new[] { "Maximum Plasma Concentrations (mcg/mL)", "216", "275" },
                    new[] { "Elimination Half-life (hour)", "4.6", "4.3" },
                    new[] { "Plasma Clearance (mL/hour/kg)", "49", "60" },
                    new[] { "Volume of Distribution (mL/kg)", "338", "373" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // 4 rows × 2 dose columns = 8 observations
            Assert.AreEqual(8, results.Count,
                "transposed layout should produce rows × dose-columns observations");

            // Every observation is PK
            Assert.IsTrue(results.All(r => r.TableCategory == "PK"));

            // Canonical names — collapsed via PkParameterDictionary
            var names = results.Select(r => r.ParameterName).Distinct().ToList();
            CollectionAssert.Contains(names, "Cmax");
            CollectionAssert.Contains(names, "t½");
            CollectionAssert.Contains(names, "CL");
            CollectionAssert.Contains(names, "Vd");

            // DoseRegimen carries the original column header, not the row label
            Assert.IsTrue(results.Any(r => r.DoseRegimen == "50 mg/kg IV"));
            Assert.IsTrue(results.Any(r => r.DoseRegimen == "75 mg/kg IV"));

            // Flagging
            Assert.IsTrue(
                results.Any(r => (r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_LAYOUT_SWAP")),
                "expected PK_TRANSPOSED_LAYOUT_SWAP on at least one observation");
            Assert.IsTrue(
                results.Any(r => (r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_CANONICALIZED")),
                "expected PK_TRANSPOSED_CANONICALIZED when long-form English is collapsed");
        }

        /**************************************************************/
        /// <summary>
        /// Regression test for the production 0-observation bug on TextTableID
        /// 13202/13203/22685/22686/33852 (Ceftriaxone). The trimmed 4-row test
        /// above passes because every row canonicalizes to PK; the production
        /// table has 7 rows where only 4 are PK metrics and the remaining 3
        /// (CSF Concentration, Range, Time after dose) are non-PK. Verifies the
        /// transposed-layout swap still fires at 4-of-7 majority and produces
        /// the expected 4 × 2 = 8 PK observations (non-PK rows may still be
        /// emitted but the swap must not be blocked by their presence).
        /// </summary>
        [TestMethod]
        public void PkParser_TransposedLayout_CeftriaxoneFullShape_RecoversObservations()
        {
            var table = createTestTable(
                new[] { null, "50 mg/kg IV", "75 mg/kg IV" },
                new List<string?[]>
                {
                    new[] { "Maximum Plasma Concentrations (mcg/mL)", "216", "275" },
                    new[] { "Elimination Half-life (hour)", "4.6", "4.3" },
                    new[] { "Plasma Clearance (mL/hour/kg)", "49", "60" },
                    new[] { "Volume of Distribution (mL/kg)", "338", "373" },
                    new[] { "CSF Concentration –inflamed meninges (mcg/mL)", "5.6", "6.4" },
                    new[] { "Range (mcg/mL)", "1.3 to 18.5", "1.3 to 44" },
                    new[] { "Time after dose (hour)", "3.7 (± 1.6)", "3.3 (± 1.4)" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // The 4 canonical PK rows × 2 dose columns MUST produce 8 observations.
            // Non-PK rows (CSF/Range/Time-after-dose) may add extras but the 4 canonical
            // metrics must be present after the swap.
            var pkCanonicalNames = new[] { "Cmax", "t½", "CL", "Vd" };
            foreach (var canonical in pkCanonicalNames)
            {
                Assert.IsTrue(
                    results.Any(r => r.ParameterName == canonical),
                    $"expected at least one observation with ParameterName == \"{canonical}\"");
            }

            // Each of the 4 canonical PK rows should have 2 dose observations.
            foreach (var canonical in pkCanonicalNames)
            {
                var dosesForParam = results
                    .Where(r => r.ParameterName == canonical)
                    .Select(r => r.DoseRegimen)
                    .ToList();
                Assert.IsTrue(dosesForParam.Contains("50 mg/kg IV"),
                    $"expected DoseRegimen=\"50 mg/kg IV\" for {canonical}");
                Assert.IsTrue(dosesForParam.Contains("75 mg/kg IV"),
                    $"expected DoseRegimen=\"75 mg/kg IV\" for {canonical}");
            }

            Assert.IsTrue(
                results.Any(r => (r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_LAYOUT_SWAP")),
                "expected PK_TRANSPOSED_LAYOUT_SWAP to fire at 4-of-7 majority");
        }

        #endregion PK Transposed Layout Relaxation

        #region PK Two-Column Population Routing

        /**************************************************************/
        /// <summary>
        /// Mirrors TextTableID=970 shape: col 0 carries CYP2C19 metabolizer
        /// phenotypes (Poor / Intermediate / Normal / Ultrarapid), col 1 is a
        /// dedicated dose column, and cols 2+ carry PK / PD parameters. Verifies
        /// phenotypes route to <c>Population</c> rather than being parked in
        /// <c>ParameterSubtype</c>.
        /// </summary>
        [TestMethod]
        public void PkParser_TwoColumnLayout_PhenotypeRowsRouteToPopulation()
        {
            var table = createTestTable(
                new[] { "Phenotype", "Dose", "Cmax (ng/mL)" },
                new List<string?[]>
                {
                    new[] { "Poor",          "300 mg (24 h)", "11 (4)" },
                    new[] { "Intermediate",  "300 mg (24 h)", "23 (11)" },
                    new[] { "Normal",        "300 mg (24 h)", "32 (21)" },
                    new[] { "Ultrarapid",    "300 mg (24 h)", "24 (10)" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.All(r => r.ParameterName == "Cmax"),
                "every row should carry canonical Cmax as ParameterName");

            // Population populated, ParameterSubtype empty
            Assert.IsTrue(results.Any(r => r.Population == "Poor Metabolizer"));
            Assert.IsTrue(results.Any(r => r.Population == "Intermediate Metabolizer"));
            Assert.IsTrue(results.Any(r => r.Population == "Normal Metabolizer"));
            Assert.IsTrue(results.Any(r => r.Population == "Ultrarapid Metabolizer"));
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.ParameterSubtype)));
            Assert.IsTrue(results.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_COL0_POP_ROUTED")));
        }

        #endregion PK Two-Column Population Routing
    }
}

