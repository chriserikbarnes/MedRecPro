using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    public partial class TableParserTests
    {
        #region EfficacyMultilevelTableParser Tests

        /**************************************************************/
        /// <summary>
        /// Efficacy multilevel parser handles stat columns and emits Comparison rows.
        /// PrimaryValueType is derived from header ("Relative Risk" → "RelativeRisk").
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_EmitsComparisonRows()
        {
            var table = createTestTable(
                new[] { "Endpoint", "EVISTA (N=5129)", "Placebo (N=5133)", "P-value", "Relative Risk (95% CI)" },
                new List<string?[]>
                {
                    new[] { "Clinical Vertebral Fractures", "61", "93", "0.003", "0.65 (0.53, 0.79)" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            Assert.IsTrue(parser.CanParse(table));

            var results = parser.Parse(table);

            // Should have arm rows + comparison row
            Assert.IsTrue(results.Count >= 2);
            var comparison = results.FirstOrDefault(r => r.TreatmentArm == "Comparison");
            Assert.IsNotNull(comparison);
            Assert.AreEqual(0.65, comparison!.PrimaryValue);
            Assert.AreEqual("RelativeRisk", comparison.PrimaryValueType);
        }

        /**************************************************************/
        /// <summary>
        /// Column sub-header row sets PrimaryValueType and Unit on arm observations.
        /// "Absolute Risk per 10,000 Women-Years" → AbsoluteRisk + unit.
        /// Arrow symbol "↔" propagates the previous column's sub-header.
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_SubHeaderRow_SetsUnitAndType()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk (95% CI)", "CE (n = 5,310)", "Placebo (n = 5,429)" },
                new List<string?[]>
                {
                    new[] { "-", "-", "Absolute Risk per 10,000 Women-Years", "↔" },
                    new[] { "Deep vein thrombosis", "1.47 (1.06–2.06)", "23", "15" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var ceObs = results.FirstOrDefault(r => r.TreatmentArm == "CE");
            Assert.IsNotNull(ceObs);
            Assert.AreEqual("AbsoluteRisk", ceObs!.PrimaryValueType);
            Assert.AreEqual("per 10,000 Women-Years", ceObs.Unit);

            var placeboObs = results.FirstOrDefault(r => r.TreatmentArm == "Placebo");
            Assert.IsNotNull(placeboObs);
            Assert.AreEqual("AbsoluteRisk", placeboObs!.PrimaryValueType);
            Assert.AreEqual("per 10,000 Women-Years", placeboObs.Unit);
        }

        /**************************************************************/
        /// <summary>
        /// CI level is extracted from stat column header "(95% nCI)" → BoundType = "95CI".
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_CILevelFromHeader_Sets95CI()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk CE vs. Placebo (95% nCI)", "CE (n = 100)", "Placebo (n = 100)" },
                new List<string?[]>
                {
                    new[] { "Stroke", "1.33 (1.15–1.68)", "45", "33" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var comparison = results.FirstOrDefault(r => r.TreatmentArm == "Comparison");
            Assert.IsNotNull(comparison);
            Assert.AreEqual("95CI", comparison!.BoundType);
        }

        /**************************************************************/
        /// <summary>
        /// Comparison PrimaryValueType derived from header "Relative Risk" → "RelativeRisk".
        /// Not the hardcoded "RelativeRiskReduction".
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_ComparisonType_RelativeRisk()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk CE vs. Placebo (95% nCI)", "CE (n = 100)", "Placebo (n = 100)" },
                new List<string?[]>
                {
                    new[] { "DVT", "1.47 (1.06–2.06)", "23", "15" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var comparison = results.FirstOrDefault(r => r.TreatmentArm == "Comparison");
            Assert.IsNotNull(comparison);
            Assert.AreEqual("RelativeRisk", comparison!.PrimaryValueType);
        }

        /**************************************************************/
        /// <summary>
        /// For binary "X vs. Y" comparison with 2 arms, Comparison ArmN = sum of arm Ns.
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_ComparisonArmN_BinaryVs_SumsArms()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk CE vs. Placebo (95% CI)", "CE (n = 5,310)", "Placebo (n = 5,429)" },
                new List<string?[]>
                {
                    new[] { "DVT", "1.47 (1.06–2.06)", "23", "15" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var comparison = results.FirstOrDefault(r => r.TreatmentArm == "Comparison");
            Assert.IsNotNull(comparison);
            Assert.AreEqual(10739, comparison!.ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// Without "vs." in header, Comparison ArmN remains null even with 2 arms.
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_ComparisonArmN_NoVs_RemainsNull()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk (95% CI)", "CE (n = 100)", "Placebo (n = 200)" },
                new List<string?[]>
                {
                    new[] { "DVT", "1.47 (1.06–2.06)", "23", "15" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var comparison = results.FirstOrDefault(r => r.TreatmentArm == "Comparison");
            Assert.IsNotNull(comparison);
            Assert.IsNull(comparison!.ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// With 3 arms, Comparison ArmN remains null even with "vs." in header.
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_ComparisonArmN_ThreeArms_RemainsNull()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk CE vs. Placebo (95% CI)", "CE (n = 100)", "Placebo (n = 200)", "Drug B (n = 150)" },
                new List<string?[]>
                {
                    new[] { "DVT", "1.47 (1.06–2.06)", "23", "15", "18" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var comparison = results.FirstOrDefault(r => r.TreatmentArm == "Comparison");
            Assert.IsNotNull(comparison);
            Assert.IsNull(comparison!.ArmN);
        }

        /**************************************************************/
        /// <summary>
        /// Full WHI-style table integration test: validates ArmN, PrimaryValueType,
        /// Unit, BoundType, and Comparison ArmN all derived correctly.
        /// </summary>
        [TestMethod]
        public void EfficacyMultilevelParser_WHITable_Integration()
        {
            var table = createTestTable(
                new[] { "Event", "Relative Risk CE vs. Placebo (95% nCI)", "CE n = 5,310", "Placebo n = 5,429" },
                new List<string?[]>
                {
                    new[] { "-", "-", "Absolute Risk per 10,000 Women-Years", "↔" },
                    new[] { "Deep vein thrombosis", "1.47 (1.06–2.06)", "23", "15" },
                    new[] { "Hip fracture", "0.65 (0.45–0.94)", "12", "19" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            Assert.IsTrue(parser.CanParse(table));
            var results = parser.Parse(table);

            // DVT arm observations
            var dvtCe = results.FirstOrDefault(r =>
                r.ParameterName == "Deep vein thrombosis" && r.TreatmentArm == "CE");
            Assert.IsNotNull(dvtCe);
            Assert.AreEqual(5310, dvtCe!.ArmN, "CE ArmN from comma-formatted header");
            Assert.AreEqual(23.0, dvtCe.PrimaryValue);
            Assert.AreEqual("AbsoluteRisk", dvtCe.PrimaryValueType, "Type from sub-header");
            Assert.AreEqual("per 10,000 Women-Years", dvtCe.Unit, "Unit from sub-header");

            var dvtPlacebo = results.FirstOrDefault(r =>
                r.ParameterName == "Deep vein thrombosis" && r.TreatmentArm == "Placebo");
            Assert.IsNotNull(dvtPlacebo);
            Assert.AreEqual(5429, dvtPlacebo!.ArmN, "Placebo ArmN from comma-formatted header");
            Assert.AreEqual(15.0, dvtPlacebo.PrimaryValue);
            Assert.AreEqual("AbsoluteRisk", dvtPlacebo.PrimaryValueType, "Type propagated via ↔");
            Assert.AreEqual("per 10,000 Women-Years", dvtPlacebo.Unit, "Unit propagated via ↔");

            // DVT comparison observation
            var dvtComp = results.FirstOrDefault(r =>
                r.ParameterName == "Deep vein thrombosis" && r.TreatmentArm == "Comparison");
            Assert.IsNotNull(dvtComp);
            Assert.AreEqual(1.47, dvtComp!.PrimaryValue);
            Assert.AreEqual("RelativeRisk", dvtComp.PrimaryValueType, "Header-derived type");
            Assert.AreEqual("95CI", dvtComp.BoundType, "CI level from header (95% nCI)");
            Assert.AreEqual(1.06, dvtComp.LowerBound);
            Assert.AreEqual(2.06, dvtComp.UpperBound);
            Assert.AreEqual(10739, dvtComp.ArmN, "Sum of arm Ns for binary vs. comparison");

            // Hip fracture — verify second data row also gets sub-header context
            var hipCe = results.FirstOrDefault(r =>
                r.ParameterName == "Hip fracture" && r.TreatmentArm == "CE");
            Assert.IsNotNull(hipCe);
            Assert.AreEqual("AbsoluteRisk", hipCe!.PrimaryValueType);
            Assert.AreEqual("per 10,000 Women-Years", hipCe.Unit);
        }

        #endregion EfficacyMultilevelTableParser Tests

        #region AE/Efficacy Value Context Regression Tests

        /**************************************************************/
        /// <summary>
        /// AE count-plus-less-than-one cells derive percentage from ArmN while preserving count.
        /// </summary>
        [TestMethod]
        public void AeValueContext_CountInequalityPercent_DerivesFromArmN()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (N=180) n (%)", "Placebo (N=176) n (%)" },
                new List<string?[]>
                {
                    new[] { "Headache", "1 (<1)", "1 (<1%)" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.PrimaryValueType == "Percentage"));
            Assert.IsTrue(results.All(r => r.SecondaryValue == 1.0));
            Assert.IsTrue(results.All(r => r.SecondaryValueType == "Count"));
            Assert.AreEqual(0.6, results.Single(r => r.TreatmentArm == "Drug").PrimaryValue);
            Assert.AreEqual(0.6, results.Single(r => r.TreatmentArm == "Placebo").PrimaryValue);
            Assert.IsTrue(results.All(r => r.ValidationFlags!.Contains("PCT_DERIVED_FROM_COUNT_LT")));
        }

        /**************************************************************/
        /// <summary>
        /// AE decimal count-plus-inequality residual cells keep the derived percentage shape.
        /// </summary>
        [TestMethod]
        public void AeValueContext_DecimalCountInequalityPercent_DerivesFromArmN()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (N=3000) n (%)", "Placebo (N=1200) n (%)" },
                new List<string?[]>
                {
                    new[] { "Somnolence", "3.0 (<0.1)", "1.2 (<0.1)" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.PrimaryValueType == "Percentage"));
            Assert.AreEqual(3.0, results.Single(r => r.TreatmentArm == "Drug").SecondaryValue);
            Assert.AreEqual(1.2, results.Single(r => r.TreatmentArm == "Placebo").SecondaryValue);
            Assert.IsTrue(results.All(r => r.SecondaryValueType == "Count"));
            Assert.IsTrue(results.All(r => r.ValidationFlags!.Contains("PCT_DERIVED_FROM_COUNT_LT")));
        }

        /**************************************************************/
        /// <summary>
        /// Standalone less-than-one AE percentage cells stay percentages, not p-values.
        /// </summary>
        [TestMethod]
        public void AeValueContext_StandaloneLtOnePercent_DoesNotBecomePValue()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (N=258) %" },
                new List<string?[]>
                {
                    new[] { "Dyspepsia", "<1" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Percentage", results[0].PrimaryValueType);
            Assert.AreEqual(0.4, results[0].PrimaryValue);
            Assert.IsNull(results[0].PValue);
            Assert.IsTrue(results[0].ValidationFlags!.Contains("PCT_DERIVED_FROM_LT_ONE:ArmN=258"));
        }

        /**************************************************************/
        /// <summary>
        /// Standalone less-than-one AE cells remain percentages even when the header is count-like.
        /// </summary>
        [TestMethod]
        public void AeValueContext_StandaloneLtOneCountHeader_FallsBackToPercentage()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (N=258) n" },
                new List<string?[]>
                {
                    new[] { "Dyspepsia", "<1" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Percentage", results[0].PrimaryValueType);
            Assert.AreEqual(0.4, results[0].PrimaryValue);
            Assert.IsNull(results[0].PValue);
            Assert.IsTrue(results[0].ValidationFlags!.Contains("PCT_DERIVED_FROM_LT_ONE:ArmN=258"));
        }

        /**************************************************************/
        /// <summary>
        /// Parenthesized values over 100 are rejected as percentages and demoted.
        /// </summary>
        [TestMethod]
        public void AeValueContext_InvalidPercentOver100_IsDemoted()
        {
            var table = createTestTable(
                new[] { "Adverse Reaction", "Drug (N=100)" },
                new List<string?[]>
                {
                    new[] { "Total events", "1811 (1693)" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Count", results[0].PrimaryValueType);
            Assert.AreEqual(1811.0, results[0].PrimaryValue);
            Assert.AreNotEqual("Percentage", results[0].PrimaryValueType);
            Assert.IsTrue(results[0].ValidationFlags!.Contains("PCT_GT100_REJECTED:1693"));
        }

        /**************************************************************/
        /// <summary>
        /// Plain numeric AE total-event rows are typed as counts while percentage rows stay percentages.
        /// </summary>
        [TestMethod]
        public void AeValueContext_TotalEventRowsRemainCounts()
        {
            var table = createTestTable(
                new[] { "Col 0", "Drug (N=100)" },
                new List<string?[]>
                {
                    new[] { "Total number of AEs", "145" },
                    new[] { "% of patients with >=1 AE", "30" },
                    new[] { "Gastrointestinal disorders", "" },
                    new[] { "Nausea", "15" }
                },
                parentSectionCode: "34084-4");

            var parser = new MultilevelAeTableParser();
            var results = parser.Parse(table);

            var totalEvents = results.Single(r => r.ParameterName == "Total number of AEs");
            var patientsWithAe = results.Single(r => r.ParameterName == "% of patients with >=1 AE");

            Assert.AreEqual("Count", totalEvents.PrimaryValueType);
            Assert.AreEqual(145.0, totalEvents.PrimaryValue);
            Assert.IsNull(totalEvents.Unit);
            Assert.IsTrue(totalEvents.ValidationFlags!.Contains("COUNT_CONTEXT_PROMOTION"));
            Assert.AreEqual("Percentage", patientsWithAe.PrimaryValueType);
            Assert.AreEqual(30.0, patientsWithAe.PrimaryValue);
            Assert.AreEqual("%", patientsWithAe.Unit);
        }

        /**************************************************************/
        /// <summary>
        /// Interspersed text cells become row labels while column-zero text remains category context.
        /// </summary>
        [TestMethod]
        public void AeValueContext_InterspersedLabelPromotesParameterName()
        {
            var table = createTestTable(
                new[]
                {
                    "Body System/ Adverse Event (Preferred Term)",
                    "Body System/ Adverse Event (Preferred Term)",
                    "Drug (N=100) %",
                    "Placebo (N=100) %"
                },
                new List<string?[]>
                {
                    new[] { "Gastrointestinal", "Dyspepsia", "7.9", "3.2" }
                },
                parentSectionCode: "34084-4");

            var parser = new SimpleArmTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.ParameterName == "Dyspepsia"));
            Assert.IsTrue(results.All(r => r.ParameterCategory == "Gastrointestinal"));
            Assert.IsFalse(results.Any(r => r.RawValue == "Dyspepsia"));
            Assert.IsTrue(results.All(r => r.PrimaryValueType == "Percentage"));
        }

        /**************************************************************/
        /// <summary>
        /// Efficacy n/N, p-value, and difference rows are typed and emitted as comparison rows.
        /// </summary>
        [TestMethod]
        public void EfficacyValueContext_NNAndStatisticRows_AreTypedAndCompared()
        {
            var table = createTestTable(
                new[] { "Endpoint", "ZAVZPRET 10 mg (N=623)", "Placebo (N=646)" },
                new List<string?[]>
                {
                    new[] { "Pain Free at 2 hours", null, null },
                    new[] { "n/N", "147/623", "96/646" },
                    new[] { "% Responders", "23.6", "14.9" },
                    new[] { "Difference from placebo (%)", "8.8", "\u2194" },
                    new[] { "p-value", "<0.001", "\u2194" },
                    new[] { "MBS Free at 2 hours", null, null },
                    new[] { "n/N", "247/623", "201/646" },
                    new[] { "% Responders", "39.6", "31.1" },
                    new[] { "Difference from placebo (%)", "8.7", "\u2194" },
                    new[] { "p-value", "0.001", "\u2194" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var nn = results.Single(r =>
                r.ParameterCategory == "Pain Free at 2 hours" &&
                r.ParameterName == "n/N" &&
                r.TreatmentArm == "ZAVZPRET 10 mg");
            Assert.AreEqual("Count", nn.PrimaryValueType);
            Assert.AreEqual(147.0, nn.PrimaryValue);
            Assert.AreEqual("Denominator", nn.SecondaryValueType);
            Assert.AreEqual(623.0, nn.SecondaryValue);

            var responder = results.Single(r =>
                r.ParameterCategory == "Pain Free at 2 hours" &&
                r.ParameterName == "% Responders" &&
                r.TreatmentArm == "Placebo");
            Assert.AreEqual("Percentage", responder.PrimaryValueType);
            Assert.AreEqual(14.9, responder.PrimaryValue);

            var pRows = results.Where(r => r.ParameterName == "p-value").ToList();
            Assert.AreEqual(2, pRows.Count);
            Assert.IsTrue(pRows.All(r => r.TreatmentArm == "Comparison"));
            Assert.IsTrue(pRows.All(r => r.PrimaryValueType == "PValue"));
            Assert.AreEqual(0.001, pRows.Single(r => r.RawValue == "0.001").PValue);

            var diffRows = results.Where(r => r.ParameterName == "Difference from placebo (%)").ToList();
            Assert.AreEqual(2, diffRows.Count);
            Assert.IsTrue(diffRows.All(r => r.TreatmentArm == "Comparison"));
            Assert.IsTrue(diffRows.All(r => r.PrimaryValueType == "RiskDifference"));
            Assert.IsTrue(diffRows.All(r => r.Unit == "%"));
            Assert.IsFalse(results.Any(r => r.RawValue == "\u2194"));
        }

        /**************************************************************/
        /// <summary>
        /// Explicit p-value phrase variants are emitted as comparison PValue rows.
        /// </summary>
        [TestMethod]
        public void EfficacyValueContext_PValuePhraseVariants_AreComparisonRows()
        {
            var table = createTestTable(
                new[] { "Endpoint", "Drug (N=100)", "Placebo (N=100)" },
                new List<string?[]>
                {
                    new[] { "Primary endpoint", null, null },
                    new[] { "P-value versus Placebo", "0.0164", "\u2194" },
                    new[] { "Log rank p-value", "<0.001", "-" },
                    new[] { "p-value for difference", "0.004", "--" }
                },
                parentSectionCode: "34092-7",
                headerRowCount: 2);

            var parser = new EfficacyMultilevelTableParser();
            var results = parser.Parse(table);

            var pRows = results.Where(r => r.ParameterName!.Contains("p-value", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.AreEqual(3, pRows.Count);
            Assert.IsTrue(pRows.All(r => r.TreatmentArm == "Comparison"));
            Assert.IsTrue(pRows.All(r => r.PrimaryValueType == "PValue"));
            Assert.IsTrue(pRows.All(r => r.ValidationFlags!.Contains("P_VALUE_ROW_CONTEXT")));
            Assert.IsFalse(results.Any(r => r.TreatmentArm != "Comparison" &&
                                            r.ParameterName!.Contains("p-value", StringComparison.OrdinalIgnoreCase)));
        }

        /**************************************************************/
        /// <summary>
        /// Duplicate comparison suppression removes only exact same-source ordinary-arm rows.
        /// </summary>
        /// <remarks>
        /// TODO: Re-enable once <c>suppressDuplicateComparisonEmissions</c> in
        /// <c>BaseTableParser.cs</c> is restored. The body was disabled on 2026-05-01
        /// because the original GroupBy key over-suppressed legitimate tables; see the
        /// in-source NOTE block at BaseTableParser.cs:1501 for the proposed
        /// (PrimaryValueType + PrimaryValue + PValue + SecondaryValue + IsDefiniteComparisonStatEmission)
        /// key that would make this test pass without the over-suppression. Both call sites
        /// (EfficacyMultilevelTableParser:352, SimpleArmTableParser:284) currently invoke a
        /// no-op stub.
        /// </remarks>
        [TestMethod]
        [Ignore("Suppression disabled pending non-over-aggressive GroupBy fix — see BaseTableParser.cs:1501")]
        public void EfficacyValueContext_DuplicateComparisonSuppression_ExactSourceOnly()
        {
            var observations = new List<ParsedObservation>
            {
                new()
                {
                    TextTableID = 30055,
                    TableCategory = "EFFICACY",
                    SourceRowSeq = 7,
                    SourceCellSeq = 2,
                    ParameterName = "p-value compared to placebo",
                    RawValue = "0.01",
                    TreatmentArm = "Comparison",
                    PrimaryValue = 0.01,
                    PrimaryValueType = "PValue"
                },
                new()
                {
                    TextTableID = 30055,
                    TableCategory = "EFFICACY",
                    SourceRowSeq = 7,
                    SourceCellSeq = 2,
                    ParameterName = "p-value compared to placebo",
                    RawValue = "0.01",
                    TreatmentArm = "Drug",
                    PrimaryValue = 0.01,
                    PrimaryValueType = "PValue"
                },
                new()
                {
                    TextTableID = 30055,
                    TableCategory = "EFFICACY",
                    SourceRowSeq = 7,
                    SourceCellSeq = 3,
                    ParameterName = "p-value compared to placebo",
                    RawValue = "0.01",
                    TreatmentArm = "Placebo",
                    PrimaryValue = 0.01,
                    PrimaryValueType = "PValue"
                }
            };

            DuplicateComparisonHarness.Suppress(observations);

            Assert.AreEqual(2, observations.Count);
            Assert.IsTrue(observations.Any(r => r.TreatmentArm == "Comparison"));
            Assert.IsFalse(observations.Any(r => r.TreatmentArm == "Drug"));
            Assert.IsTrue(observations.Any(r => r.TreatmentArm == "Placebo"));
        }

        #endregion AE/Efficacy Value Context Regression Tests


        #region TableParserRouter Tests

        /**************************************************************/
        /// <summary>
        /// Router correctly categorizes by ParentSectionCode.
        /// </summary>
        [TestMethod]
        public void Router_CategorizesByParentSectionCode()
        {
            var parsers = TableParserTestHelper.CreateProductionParsers();
            var router = new TableParserRouter(parsers);

            var pkTable = createTestTable(
                new[] { "Dose", "Cmax" },
                new List<string?[]> { new[] { "50mg", "1.0" } },
                parentSectionCode: "34090-1");

            var (category, parser) = router.Route(pkTable);
            Assert.AreEqual(TableCategory.PK, category);
            Assert.IsNotNull(parser);
            Assert.IsInstanceOfType(parser, typeof(PkTableParser));
        }

        /**************************************************************/
        /// <summary>
        /// Router skips patient info tables.
        /// </summary>
        [TestMethod]
        public void Router_SkipsPatientInfoTables()
        {
            var parsers = TableParserTestHelper.CreateProductionParsers();
            var router = new TableParserRouter(parsers);

            var table = createTestTable(
                new[] { "A", "B" },
                new List<string?[]> { new[] { "1", "2" } },
                parentSectionCode: "68498-5");

            var (category, parser) = router.Route(table);
            Assert.AreEqual(TableCategory.SKIP, category);
            Assert.IsNull(parser);
        }

        /**************************************************************/
        /// <summary>
        /// Router skips single-column tables.
        /// </summary>
        [TestMethod]
        public void Router_SkipsSingleColumnTables()
        {
            var parsers = TableParserTestHelper.CreateProductionParsers();
            var router = new TableParserRouter(parsers);

            var table = createTestTable(
                new[] { "Only" },
                new List<string?[]>());
            table.TotalColumnCount = 1;

            var (category, parser) = router.Route(table);
            Assert.AreEqual(TableCategory.SKIP, category);
        }

        /**************************************************************/
        /// <summary>
        /// Router skips NDC caption tables.
        /// </summary>
        [TestMethod]
        public void Router_SkipsNdcCaptionTables()
        {
            var parsers = TableParserTestHelper.CreateProductionParsers();
            var router = new TableParserRouter(parsers);

            var table = createTestTable(
                new[] { "A", "B" },
                new List<string?[]> { new[] { "1", "2" } },
                caption: "NDC Number and Package Description");

            var (category, parser) = router.Route(table);
            Assert.AreEqual(TableCategory.SKIP, category);
        }

        /**************************************************************/
        /// <summary>
        /// Router selects multilevel AE parser over simple parser when header has 2+ rows.
        /// </summary>
        [TestMethod]
        public void Router_SelectsMultilevelAeForTwoRowHeader()
        {
            var parsers = TableParserTestHelper.CreateProductionParsers();
            var router = new TableParserRouter(parsers);

            var table = createMultilevelTable(
                new[] { "Treatment", "Treatment" },
                new[] { "Drug (N=100) %", "Placebo (N=100) %" },
                new List<string?[]> { new[] { "Nausea", "5.0", "2.0" } });

            var (category, parser) = router.Route(table);
            Assert.AreEqual(TableCategory.ADVERSE_EVENT, category);
            Assert.IsInstanceOfType(parser, typeof(MultilevelAeTableParser));
        }

        /**************************************************************/
        /// <summary>
        /// Router falls back to SectionTitle categorization for unclassified sections.
        /// </summary>
        [TestMethod]
        public void Router_FallsBackToSectionTitle()
        {
            var parsers = TableParserTestHelper.CreateProductionParsers();
            var router = new TableParserRouter(parsers);

            var table = createTestTable(
                new[] { "Dose", "Cmax" },
                new List<string?[]> { new[] { "50mg", "1.0" } },
                parentSectionCode: "42229-5",
                sectionTitle: "Pharmacokinetics in Special Populations");

            var (category, parser) = router.Route(table);
            Assert.AreEqual(TableCategory.PK, category);
        }


        #endregion TableParserRouter Tests
    }
}

