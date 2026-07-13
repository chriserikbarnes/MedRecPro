using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecPro.Service.Test
{
    public partial class TableParserTests
    {
        #region PK R1 Row-Label Classification

        /**************************************************************/
        /// <summary>
        /// R1 — Two-column layout: col 0 = "Atorvastatin" (drug name) should
        /// route to TreatmentArm with the PK_COL0_ARM_ROUTED flag, not sit in
        /// ParameterSubtype (the pre-R1 fallback). ParameterName stays canonical.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_TwoColumn_BareDrugRoutesToTreatmentArm()
        {
            var table = createTestTable(
                new[] { "Co-administered Drug", "Dose", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Atorvastatin", "10 mg/day for 8 days", "0.83" },
                    new[] { "Carbamazepine", "200 mg twice a day", "0.97" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.ParameterName == "Cmax"));

            var atorv = results.First(r => r.TreatmentArm == "Atorvastatin");
            Assert.AreEqual("10 mg/day for 8 days", atorv.DoseRegimen);
            Assert.IsNull(atorv.ParameterSubtype);
            StringAssert.Contains(atorv.ValidationFlags ?? "", "PK_COL0_ARM_ROUTED");

            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.ParameterSubtype)));
        }

        /**************************************************************/
        /// <summary>
        /// R1 — Single-column layout: compound "Drug + Dose" row label splits
        /// into TreatmentArm (drug) + DoseRegimen (dose). Flag PK_COL0_ARM_DOSE_SPLIT.
        /// Simulates TID 571 shape after the Phase 2 context-column suppression
        /// (R2) has removed the explicit "Co-administered Drug" column.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_SingleColumn_DrugPlusDoseSplitsIntoArmAndRegimen()
        {
            var table = createTestTable(
                new[] { "Regimen", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Atorvastatin 10 mg/day for 8 days", "0.83", "1.01" },
                    new[] { "Carbamazepine 200 mg twice a day for 18 days", "0.97", "0.96" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);

            var atorvCmax = results.First(r => r.TreatmentArm == "Atorvastatin" && r.ParameterName == "Cmax");
            Assert.AreEqual("10 mg/day for 8 days", atorvCmax.DoseRegimen);
            StringAssert.Contains(atorvCmax.ValidationFlags ?? "", "PK_COL0_ARM_DOSE_SPLIT");

            var carbAuc = results.First(r => r.TreatmentArm == "Carbamazepine" && r.ParameterName == "AUC");
            Assert.AreEqual("200 mg twice a day for 18 days", carbAuc.DoseRegimen);
        }

        /**************************************************************/
        /// <summary>
        /// R1 — Single-column layout: row label is a population descriptor
        /// ("Healthy Subjects") even when the col 0 header doesn't match the
        /// population keyword set. Pre-R1: label went to DoseRegimen. Post-R1:
        /// label routes to Population with PK_COL0_POP_ROUTED flag.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_SingleColumn_PopulationLabelRoutesToPopulation()
        {
            var table = createTestTable(
                new[] { "Subject Group", "t½ (hours)", "CL (L/hour)" },
                new List<string?[]>
                {
                    new[] { "Healthy Subjects", "8.0", "1.0" },
                    new[] { "Renal Impairment", "14.7", "0.65" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.All(r => r.ParameterName == "t½" || r.ParameterName == "CL"));

            var healthy = results.Where(r => r.Population == "Healthy Volunteers").ToList();
            Assert.AreEqual(2, healthy.Count);
            Assert.IsTrue(healthy.All(r => string.IsNullOrWhiteSpace(r.DoseRegimen)));
            Assert.IsTrue(healthy.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_COL0_POP_ROUTED")));

            var renal = results.Where(r => r.Population == "Renal Impairment").ToList();
            Assert.AreEqual(2, renal.Count);
        }

        /**************************************************************/
        /// <summary>
        /// R1 — Single-column layout: row label matches the population regex
        /// second-pass (renal creatinine-clearance band) — should route to
        /// Population with the distinct PK_COL0_POP_ROUTED_REGEX flag so
        /// downstream reporting can distinguish dictionary vs regex matches.
        /// Mirrors TID 2069 "CLCR 50-80 mL/min" rows.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_SingleColumn_RenalBandRegexRoutesWithRegexFlag()
        {
            var table = createTestTable(
                new[] { "Subject Group", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Normal Creatinine Clearance greater than 80 mL/min", "5.5", "47.5" },
                    new[] { "Moderate Creatinine Clearance 30 to 50 mL/min",        "7.1", "182.1" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            // At least one row should have matched the regex second-pass
            Assert.IsTrue(results.Any(r =>
                (r.ValidationFlags ?? "").Contains("PK_COL0_POP_ROUTED_REGEX")),
                "expected at least one row with PK_COL0_POP_ROUTED_REGEX flag");
            Assert.IsTrue(results.All(r => !string.IsNullOrWhiteSpace(r.Population)));
        }

        /**************************************************************/
        /// <summary>
        /// R1 — Single-column layout: row label is a pure timepoint ("Day 14",
        /// "Single Dose") — should route to Timepoint / Time / TimeUnit and NOT
        /// pollute DoseRegimen. Flag PK_COL0_TIMEPOINT_ROUTED fires.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_SingleColumn_TimepointLabelRoutesToTimepoint()
        {
            var table = createTestTable(
                new[] { "Visit", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Day 14",      "0.44", "17.4" },
                    new[] { "Single Dose", "0.29", "1.2" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);

            var day14 = results.Where(r => r.Timepoint != null && r.Timepoint.Contains("14")).ToList();
            Assert.AreEqual(2, day14.Count);
            Assert.IsTrue(day14.All(r => string.IsNullOrWhiteSpace(r.DoseRegimen)));
            Assert.IsTrue(day14.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_COL0_TIMEPOINT_ROUTED")));

            var singleDose = results.Where(r => r.Timepoint != null && r.Timepoint.ToLower().Contains("single")).ToList();
            Assert.AreEqual(2, singleDose.Count);
        }

        /**************************************************************/
        /// <summary>
        /// R1 backward-compat guard: when col 0 is a pure dose regimen (no drug
        /// prefix, no population keyword, no timepoint pattern), the pre-R1
        /// behavior is preserved — DoseRegimen = col 0 text, no TreatmentArm,
        /// no special attribution flag. This is the dominant happy path for
        /// thousands of existing PK tables and must not regress.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_SingleColumn_PureDosePreservesBackwardCompat()
        {
            var table = createTestTable(
                new[] { "Dose", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "50 mg oral (once daily x 7 days)",  "0.29", "1.2" },
                    new[] { "100 mg oral (once daily x 14 days)", "0.58", "2.4" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            var first = results.First(r => r.DoseRegimen == "50 mg oral (once daily x 7 days)");
            Assert.AreEqual("Cmax", first.ParameterName);
            Assert.IsNull(first.TreatmentArm);
            // The happy-path dose row should not carry any of the R1 attribution flags
            Assert.IsFalse((first.ValidationFlags ?? "").Contains("PK_COL0_ARM_ROUTED"));
            Assert.IsFalse((first.ValidationFlags ?? "").Contains("PK_COL0_ARM_DOSE_SPLIT"));
            Assert.IsFalse((first.ValidationFlags ?? "").Contains("PK_COL0_TIMEPOINT_ROUTED"));
        }

        /**************************************************************/
        /// <summary>
        /// R1 backward-compat guard: col 0 text that doesn't match any classifier
        /// rule (a descriptive phrase like "Adults given 50 mg once daily N=12")
        /// falls through to Unknown. In single-column path this means the text
        /// lands in DoseRegimen just as it did pre-R1 — so existing Phase 2
        /// cleanups still fire on this content.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_SingleColumn_DescriptivePhraseFallsThroughToUnknown()
        {
            var table = createTestTable(
                new[] { "Regimen", "Cmax (ng/mL)" },
                new List<string?[]>
                {
                    new[] { "Adults given 50 mg once daily for 7 days N=12", "24.5" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            // Falls through to Unknown → col 0 lands in DoseRegimen (pre-R1 behavior)
            Assert.AreEqual("Adults given 50 mg once daily for 7 days N=12", results[0].DoseRegimen);
            Assert.IsNull(results[0].TreatmentArm);
            // No R1 attribution flag for Unknown
            Assert.IsFalse((results[0].ValidationFlags ?? "").Contains("PK_COL0_ARM_ROUTED"));
        }

        /**************************************************************/
        /// <summary>
        /// R1 backward-compat guard: when an AdverseEvent-shaped table is
        /// (incorrectly) routed to PkTableParser, the classifier must not
        /// misroute AE terms to Timepoint/TreatmentArm because of stray
        /// tokens. AdverseEvent tables are correctly handled by their own
        /// parsers; this test locks in that PkTableParser itself doesn't
        /// cross-contaminate AE rows if it ever sees them.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_AdverseEventShape_DoesNotCrossContaminate()
        {
            // AE-shaped row labels are typically MedDRA PTs like "Nausea" or
            // "Headache" — capitalized single tokens that could plausibly look
            // drug-shaped. Confirm they route to TreatmentArm ONLY when the PK
            // parser is explicitly invoked (router normally prevents this).
            var table = createTestTable(
                new[] { "Adverse Event", "Incidence (%)" },
                new List<string?[]>
                {
                    new[] { "Nausea",   "12.5" },
                    new[] { "Headache", "8.3" }
                },
                parentSectionCode: "34084-4"); // Adverse Reactions section code

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // The parser produces observations (no column-header unit), but critically
            // the R1 classifier should not generate spurious PK_COL0_TIMEPOINT_ROUTED
            // flags. Whatever routing happens, observations must remain internally
            // consistent (Timepoint empty when no timepoint pattern matched).
            Assert.IsTrue(results.All(r =>
                !((r.ValidationFlags ?? "").Contains("PK_COL0_TIMEPOINT_ROUTED"))),
                "AE terms must never be misrouted to Timepoint by the PK classifier");
        }

        #endregion PK R1 Row-Label Classification

        #region PK R1.1 PopulationDetector + Compound-Layout Routing

        /**************************************************************/
        /// <summary>
        /// R1.1 — Sex-stratum col 0 labels ("Male" / "Female") must route to
        /// Population via the expanded <see cref="PopulationDetector"/> dictionary,
        /// NOT fall through to the drug-name heuristic and land in TreatmentArm.
        /// This locks in the fix for the 196-row false-positive family observed
        /// in the 2026-04-21 corpus audit (TID 2069 Norfloxacin shape).
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_SexStratumRoutesToPopulation_NotTreatmentArm()
        {
            var table = createTestTable(
                new[] { "Regimen", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Male",   "5.5", "54.4" },
                    new[] { "Female", "7.0", "67.7" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.Any(r => r.Population == "Male"),
                "Male should route to Population");
            Assert.IsTrue(results.Any(r => r.Population == "Female"),
                "Female should route to Population");
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)),
                "Sex strata must NEVER route to TreatmentArm");
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — Bare age stratum labels ("Young", "Elderly") route to Population.
        /// Covers the 196-row false-positive family from the audit.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_BareAgeStratumRoutesToPopulation()
        {
            var table = createTestTable(
                new[] { "Regimen", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Young",   "5.5" },
                    new[] { "Elderly", "7.0" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Any(r => r.Population == "Young Adults"));
            Assert.IsTrue(results.Any(r => r.Population == "Elderly"));
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)));
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — Dialysis-status labels ("Hemodialysis", "CAPD") route to
        /// Population. Covers the 98-row "Hemodialysis" family from the audit.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_DialysisStatusRoutesToPopulation()
        {
            var table = createTestTable(
                new[] { "Regimen", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Hemodialysis", "5.7" },
                    new[] { "CAPD",         "6.9" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Any(r => r.Population == "Hemodialysis Patients"));
            Assert.IsTrue(results.Any(r => r.Population == "CAPD Patients"));
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)));
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — Multi-word population labels with descriptor trailers
        /// ("Elderly Subjects (mean age, 70.5 year)", "Healthy Subjects (N=18)")
        /// match the R1.1 age-qualified-Subjects regex and route to Population.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_ElderlySubjectsWithTrailerRoutesToPopulation()
        {
            var table = createTestTable(
                new[] { "Regimen", "t½ (hours)" },
                new List<string?[]>
                {
                    new[] { "Elderly Subjects (mean age, 70.5 year)", "8.9" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Elderly", results[0].Population);
            Assert.IsTrue(string.IsNullOrWhiteSpace(results[0].TreatmentArm));
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — ADME section-divider words ("Absorption", "Distribution",
        /// "Metabolism", "Elimination") must NOT route to TreatmentArm even
        /// though they pass the permissive drug-name heuristic. Negative list
        /// forces them to Unknown → DoseRegimen fallback. Covers the
        /// ~231-row false-positive family from the audit.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_AdmeDividerDoesNotRouteToTreatmentArm()
        {
            var table = createTestTable(
                new[] { "Section", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Absorption",   "5.5" },
                    new[] { "Distribution", "6.0" },
                    new[] { "Metabolism",   "4.2" },
                    new[] { "Elimination",  "3.8" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)),
                "ADME dividers must NEVER route to TreatmentArm");
            // They fall through to Unknown → col 0 → DoseRegimen (pre-R1 fallback)
            Assert.IsTrue(results.Any(r => r.DoseRegimen == "Absorption"));
            Assert.IsTrue(results.Any(r => r.DoseRegimen == "Metabolism"));
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — Generic header-echo words ("Parameter", "Subject", "Mean")
        /// must NOT route to TreatmentArm. Negative list catches them.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_GenericHeaderEchoDoesNotRouteToTreatmentArm()
        {
            var table = createTestTable(
                new[] { "Label", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Parameter", "5.5" },
                    new[] { "Subject",   "6.0" },
                    new[] { "Mean",      "4.2" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)),
                "Generic header echoes must NEVER route to TreatmentArm");
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — Food-state labels ("Fasted", "Light Breakfast", "High-Fat Meal")
        /// route to Timepoint via the extended <see cref="_timepointLabelPattern"/>
        /// instead of TreatmentArm. Covers the 72-row "Light Breakfast" family.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_FoodStateRoutesToTimepoint()
        {
            var table = createTestTable(
                new[] { "Condition", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Fasted",          "5.5" },
                    new[] { "Light Breakfast", "7.0" },
                    new[] { "High-Fat Meal",   "8.2" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.All(r => !string.IsNullOrWhiteSpace(r.Timepoint)),
                "Food state should populate Timepoint");
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.TreatmentArm)),
                "Food state must NEVER route to TreatmentArm");
        }

        /**************************************************************/
        /// <summary>
        /// R1.1 — Compound layout (InferredHeader + SocDividers + identical
        /// spanning header) now applies classifyRowLabel to col 0 labels.
        /// Population descriptors route to Population; bare drug names still
        /// route to TreatmentArm (the dominant happy path for compound tables).
        /// Validates the `parseCompoundLayout` R1.1 patch.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_1_CompoundLayout_PopulationRoutesCorrectly()
        {
            // Build a minimal compound layout fixture by reusing the existing
            // helper — rows have "Healthy Volunteers GFR..." which matches the
            // R1.1 age-qualified-Subjects regex via "Healthy Volunteers" prefix.
            var table = createCompoundPkTable();
            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // Healthy Volunteers row now routes to Population (was TreatmentArm
            // pre-R1.1), but ArmN extraction from "(n=6)" still works because
            // it's done on the col 0 text regardless of routing destination.
            var healthy = results.First(r =>
                r.Population != null && r.Population.Contains("Healthy Volunteers") &&
                r.ParameterCategory == "Renal Impairment");
            Assert.IsNull(healthy.TreatmentArm,
                "R1.1: Healthy Volunteers row should route Population, not TreatmentArm");
            Assert.AreEqual(6, healthy.ArmN,
                "R1.1: ArmN should still be extracted from col 0 '(n=6)' trailer");
            StringAssert.Contains(
                healthy.ValidationFlags ?? "",
                "PK_COMPOUND_POP_ROUTED",
                "R1.1: compound-layout population routing should emit attribution flag");
        }

        #endregion PK R1.1 PopulationDetector + Compound-Layout Routing

        #region Router PK Content Validation

        /**************************************************************/
        /// <summary>
        /// Narrative drug-interaction tables under LOINC 34090-1 (Clinical
        /// Pharmacology) should downgrade to <see cref="TableCategory.SKIP"/>
        /// when no PK parameter appears in headers or rows.
        /// Mirrors TextTableID=6139/6140/6141.
        /// </summary>
        [TestMethod]
        public void Router_ClinicalPharmacology_NarrativeContent_DowngradesToSkip()
        {
            var longProse = string.Join(" ", Enumerable.Repeat("word", 30));

            var table = createTestTable(
                new[] { "Drug or Drug Class", "Effect" },
                new List<string?[]>
                {
                    new[] { "Carbamazepine Hydantoins", longProse },
                    new[] { "Other drugs: Phenobarbital Rifampin", longProse }
                },
                parentSectionCode: "34090-1");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser()
            });

            var (category, parser) = router.Route(table);

            Assert.AreEqual(TableCategory.SKIP, category);
            Assert.IsNull(parser);
        }

        /**************************************************************/
        /// <summary>
        /// Legitimate PK table content (PK parameter names in headers) keeps the
        /// PK category even under LOINC 34090-1 — content validation must not
        /// regress the happy path.
        /// </summary>
        [TestMethod]
        public void Router_ClinicalPharmacology_WithPkHeaders_ConfirmsPk()
        {
            var table = createTestTable(
                new[] { "Dose", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "50 mg", "0.29", "1.2" }
                },
                parentSectionCode: "34090-1");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser()
            });

            var (category, parser) = router.Route(table);

            Assert.AreEqual(TableCategory.PK, category);
            Assert.IsInstanceOfType(parser, typeof(PkTableParser));
        }

        /**************************************************************/
        /// <summary>
        /// Transposed PK table (long-form English row labels, blank col-0 header)
        /// should still route to PK because row labels match the PK dictionary.
        /// </summary>
        [TestMethod]
        public void Router_ClinicalPharmacology_TransposedLongFormRowLabels_ConfirmsPk()
        {
            var table = createTestTable(
                new[] { null, "50 mg/kg IV", "75 mg/kg IV" },
                new List<string?[]>
                {
                    new[] { "Maximum Plasma Concentrations (mcg/mL)", "216", "275" },
                    new[] { "Elimination Half-life (hour)", "4.6", "4.3" }
                },
                parentSectionCode: "34090-1");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser()
            });

            var (category, _) = router.Route(table);
            Assert.AreEqual(TableCategory.PK, category);
        }

        #endregion Router PK Content Validation

        #region Router Wave 3 R8 — DDI Downgrade

        /**************************************************************/
        /// <summary>
        /// R8 — A PK-coded section (34090-1) whose caption carries the classic
        /// "Effect of X on the Pharmacokinetics of Co-administered Drugs" DDI
        /// signal must route to DRUG_INTERACTION, not PK. Matches TID 13081 shape.
        /// </summary>
        [TestMethod]
        public void Router_R8_PkCodedSection_CoadministeredCaption_RoutesDrugInteraction()
        {
            var table = createTestTable(
                new[] { "Coadministered Drug", "Cmax Ratio", "AUC Ratio" },
                new List<string?[]>
                {
                    new[] { "Ketoconazole", "1.42", "2.13" }
                },
                parentSectionCode: "34090-1",
                caption: "Table 7. Effect of Phentermine/Topiramate on the Pharmacokinetics of Co-administered Drugs");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser()
            });

            var (category, _) = router.Route(table);
            Assert.AreEqual(TableCategory.DRUG_INTERACTION, category,
                "DDI caption keyword 'Co-administered' must trump PK content.");
        }

        /**************************************************************/
        /// <summary>
        /// R8 — "Drug Interactions:" caption prefix routes to DRUG_INTERACTION.
        /// Matches TID 9921/9922 (Rilpivirine DDI table) shape.
        /// </summary>
        [TestMethod]
        public void Router_R8_DrugInteractionCaption_RoutesDrugInteraction()
        {
            var table = createTestTable(
                new[] { "Coadministered Drug", "Dose", "Cmax", "AUC" },
                new List<string?[]>
                {
                    new[] { "Rifabutin", "300 mg QD", "1820", "18400" }
                },
                parentSectionCode: "34090-1",
                caption: "Table 11: Drug Interactions: Pharmacokinetic Parameters for Rilpivirine in the Presence of Coadministered Drugs");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser()
            });

            var (category, _) = router.Route(table);
            Assert.AreEqual(TableCategory.DRUG_INTERACTION, category);
        }

        /**************************************************************/
        /// <summary>
        /// R8 — "in the Presence of" caption matches the DDI pattern even when
        /// "Co-administered" isn't spelled out separately.
        /// </summary>
        [TestMethod]
        public void Router_R8_InThePresenceOfCaption_RoutesDrugInteraction()
        {
            var table = createTestTable(
                new[] { "Drug", "Cmax Change", "AUC Change" },
                new List<string?[]>
                {
                    new[] { "Atorvastatin", "↓ 15%", "↓ 25%" }
                },
                parentSectionCode: "34090-1",
                caption: "Table 3: Changes in Pharmacokinetic Parameters for RPV in the Presence of Coadministered Drugs");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser()
            });

            var (category, _) = router.Route(table);
            Assert.AreEqual(TableCategory.DRUG_INTERACTION, category);
        }

        /**************************************************************/
        /// <summary>
        /// R8 — Defense-in-depth: a legitimate PK table whose caption mentions
        /// "Renal Impairment" (NOT a DDI signal) continues to route to PK.
        /// Guards against over-routing when the weaker "Effect of X on PK"
        /// phrase appears without any co-administration keyword.
        /// </summary>
        [TestMethod]
        public void Router_R8_RenalImpairmentCaption_StillRoutesPk()
        {
            var table = createTestTable(
                new[] { "Renal Function", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Normal", "5.5", "54.4" },
                    new[] { "Mild", "6.8", "67.7" }
                },
                parentSectionCode: "34090-1",
                caption: "Table 4. Effect of Renal Impairment on the Pharmacokinetics of Drug X");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser()
            });

            var (category, _) = router.Route(table);
            Assert.AreEqual(TableCategory.PK, category,
                "Population-stratification captions must NOT be mistaken for DDI — only explicit co-admin / interaction keywords qualify.");
        }

        /**************************************************************/
        /// <summary>
        /// R8 — SectionTitle-carried DDI signal also routes correctly even when
        /// the Caption is silent.
        /// </summary>
        [TestMethod]
        public void Router_R8_SectionTitleDrugInteraction_RoutesDrugInteraction()
        {
            var table = createTestTable(
                new[] { "Drug", "Cmax", "AUC" },
                new List<string?[]>
                {
                    new[] { "Rifampin", "450", "3200" }
                },
                parentSectionCode: "42229-5",
                caption: "Table X",
                sectionTitle: "Drug Interactions");

            var router = new TableParserRouter(new ITableParser[]
            {
                new PkTableParser()
            });

            var (category, _) = router.Route(table);
            Assert.AreEqual(TableCategory.DRUG_INTERACTION, category);
        }

        /**************************************************************/
        /// <summary>
        /// R8 — Direct unit test of the detector.
        /// </summary>
        [TestMethod]
        public void Router_R8_looksLikeDdi_ExactKeywordSet()
        {
            foreach (var caption in new[]
            {
                "Drug Interaction Parameters",
                "Drug-Interaction Table",
                "Effect of X on Co-administered Drugs",
                "Coadministered with Rifampin",
                "Coadministration with ketoconazole",
                "in the Presence of Rifampin",
                "in the presence of coadministered drugs",
                "DDI Analysis"
            })
            {
                var table = new ReconstructedTable { Caption = caption };
                Assert.IsTrue(TableParserRouter.looksLikeDdi(table),
                    $"Caption '{caption}' must be recognized as DDI.");
            }

            foreach (var caption in new[]
            {
                "Pharmacokinetics of Drug X",
                "Effect of Renal Impairment",
                "Cmax and AUC in Healthy Subjects",
                "Food Effect",
                "" // empty
            })
            {
                var table = new ReconstructedTable { Caption = caption };
                Assert.IsFalse(TableParserRouter.looksLikeDdi(table),
                    $"Caption '{caption}' must NOT be recognized as DDI.");
            }
        }

        #endregion Router Wave 3 R8 — DDI Downgrade
    }
}

