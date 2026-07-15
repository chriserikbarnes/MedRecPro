using MedRecProImportClass.Models;
using MedRecProImportClass.Service.TransformationServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MedRecProTest.Unit.Parsing.Tables
{
    public partial class TableParserTests
    {
        #region PK R1.2 Transposed-Header Classification (Wave 1 R1.2)

        /**************************************************************/
        /// <summary>
        /// R1.2 — Transposed layout with food-state column headers. TID 22430
        /// (Tamsulosin) shape: row labels are PK metrics ("Cmin(ng/mL)",
        /// "Cmax(ng/mL)"), column headers are "Light Breakfast" / "Fasted" /
        /// "High-Fat Breakfast". Post-R1.2: Timepoint is populated, DoseRegimen
        /// stays null, flag <c>PK_TRANSPOSED_HEADER_TIMEPOINT_ROUTED</c> fires.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_Transposed_FoodStateHeaderRoutesToTimepoint()
        {
            var table = createTestTable(
                new[] { "Parameter", "Light Breakfast", "Fasted", "High-Fat Breakfast" },
                new List<string?[]>
                {
                    new[] { "Cmax (ng/mL)", "10.1", "12.3", "14.5" },
                    new[] { "Tmax (h)",     "2.0",  "1.0",  "4.0"  }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(6, results.Count);
            Assert.IsTrue(results.All(r => r.Timepoint != null),
                "every row should have Timepoint populated from the transposed food-state header");
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.DoseRegimen)),
                "DoseRegimen must stay null when header is a food-state timepoint");
            Assert.IsTrue(results.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_HEADER_TIMEPOINT_ROUTED")),
                "every row should carry the R1.2 timepoint attribution flag");
        }

        /**************************************************************/
        /// <summary>
        /// R1.2 — Transposed layout with population column headers. Post-R1.2:
        /// Population is populated from the column header, DoseRegimen stays null,
        /// flag <c>PK_TRANSPOSED_HEADER_POP_ROUTED</c> fires.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_Transposed_PopulationHeaderRoutesToPopulation()
        {
            var table = createTestTable(
                new[] { "Parameter", "Healthy Subjects", "Renal Impairment" },
                new List<string?[]>
                {
                    new[] { "Cmax (mcg/mL)", "5.5", "7.0" },
                    new[] { "AUC (mcg·h/mL)", "47.5", "62.1" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.Any(r => r.Population == "Healthy Volunteers"),
                "Healthy Subjects header should populate Population=Healthy Volunteers");
            Assert.IsTrue(results.Any(r => r.Population == "Renal Impairment"),
                "Renal Impairment header should populate Population");
            Assert.IsTrue(results.All(r => string.IsNullOrWhiteSpace(r.DoseRegimen)),
                "DoseRegimen must be null when header is a population stratifier");
            Assert.IsTrue(results.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_HEADER_POP_ROUTED")),
                "every row should carry the R1.2 population attribution flag");
        }

        /**************************************************************/
        /// <summary>
        /// R1.2 backward-compat guard: transposed layout with dose-level column
        /// headers (TID 13202 Ceftriaxone shape: "50 mg/kg IV" / "75 mg/kg IV")
        /// preserves pre-R1.2 behavior — DoseRegimen populated from the column
        /// header, Dose/DoseUnit extracted. No food-state / population flag fires.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_Transposed_DoseHeader_PreservesPreR1_2Behavior()
        {
            var table = createTestTable(
                new[] { null, "50 mg/kg IV", "75 mg/kg IV" },
                new List<string?[]>
                {
                    new[] { "Maximum Plasma Concentrations (mcg/mL)", "216", "275" },
                    new[] { "Elimination Half-life (hour)", "4.6", "4.3" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.All(r => !string.IsNullOrWhiteSpace(r.DoseRegimen)),
                "Dose-level column headers must still populate DoseRegimen");
            Assert.IsTrue(results.All(r =>
                !((r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_HEADER_TIMEPOINT_ROUTED"))),
                "Dose headers must NOT fire the R1.2 timepoint flag");
            Assert.IsTrue(results.All(r =>
                !((r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_HEADER_POP_ROUTED"))),
                "Dose headers must NOT fire the R1.2 population flag");
            // Existing transposed-layout flags still fire
            Assert.IsTrue(results.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_TRANSPOSED_LAYOUT_SWAP")));
        }

        #endregion PK R1.2 Transposed-Header Classification

        #region PK R2 Context-Column Suppression (Wave 1 R2)

        /**************************************************************/
        /// <summary>
        /// R2 — <see cref="PkTableParser.isContextColumnHeader"/> returns true
        /// for the non-PK context column labels the audit identified.
        /// </summary>
        [TestMethod]
        public void PkParser_R2_isContextColumnHeader_DetectsKnownPatterns()
        {
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Co-administered Drug"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Coadministered Drug"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Dose of Azithromycin"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Dose of Co-administered Drug"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Subject Group"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Patient Group"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Number of Subjects"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Condition"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Formulation"));
            Assert.IsTrue(PkTableParser.isContextColumnHeader("Route of Administration"));
        }

        /**************************************************************/
        /// <summary>
        /// R2 — <see cref="PkTableParser.isContextColumnHeader"/> returns false
        /// for legitimate PK column headers. Guards against over-matching.
        /// </summary>
        [TestMethod]
        public void PkParser_R2_isContextColumnHeader_DoesNotMatchPkHeaders()
        {
            Assert.IsFalse(PkTableParser.isContextColumnHeader("Cmax (mcg/mL)"));
            Assert.IsFalse(PkTableParser.isContextColumnHeader("AUC0-inf"));
            Assert.IsFalse(PkTableParser.isContextColumnHeader("Dose")); // Dose alone is a dose column, not context
            Assert.IsFalse(PkTableParser.isContextColumnHeader("n"));    // Sample size — handled elsewhere
            Assert.IsFalse(PkTableParser.isContextColumnHeader(null));
            Assert.IsFalse(PkTableParser.isContextColumnHeader(""));
        }

        /**************************************************************/
        /// <summary>
        /// R2 — PK table with a "Co-administered Drug" context column mixed in
        /// with legitimate PK columns: the context column should produce no
        /// observations, only the PK columns (Cmax, AUC) should emit rows.
        /// Mirrors TID 571 shape after R1 routes col 0 to TreatmentArm.
        /// </summary>
        [TestMethod]
        public void PkParser_R2_ContextColumnSuppressed_NoSpuriousObservations()
        {
            var table = createTestTable(
                new[] { "Regimen", "Co-administered Drug", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    new[] { "Atorvastatin 10 mg/day", "Atorvastatin", "0.83", "1.01" },
                    new[] { "Carbamazepine 200 mg",   "Carbamazepine", "0.97", "0.96" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // Expect 2 rows × 2 PK params = 4 observations (NOT 6 that would include context col)
            Assert.AreEqual(4, results.Count,
                "Only PK columns (Cmax, AUC) should emit observations; context column suppressed.");
            Assert.IsTrue(results.All(r =>
                r.ParameterName == "Cmax" || r.ParameterName == "AUC"),
                "No observation should carry ParameterName='Co-administered Drug'.");
            Assert.IsFalse(results.Any(r => r.ParameterName == "Co-administered Drug"),
                "Context column 'Co-administered Drug' must not emit any observation.");
        }

        /**************************************************************/
        /// <summary>
        /// R2 — Multiple context columns (Subject Group + Dose of X + Number of
        /// Subjects) are all suppressed; only PK parameter columns remain.
        /// </summary>
        [TestMethod]
        public void PkParser_R2_MultipleContextColumns_AllSuppressed()
        {
            var table = createTestTable(
                new[] { "Regimen", "Subject Group", "Dose of Co-administered Drug",
                        "Number of Subjects", "Cmax (mcg/mL)" },
                new List<string?[]>
                {
                    new[] { "Study Drug 50 mg", "Healthy", "placebo", "12", "5.5" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // 1 row × 1 PK column = 1 observation
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("Cmax", results[0].ParameterName);
        }

        #endregion PK R2 Context-Column Suppression

        #region PK R3 Section-Divider Suppression (Wave 1 R3)

        /**************************************************************/
        /// <summary>
        /// R3 — <see cref="PkTableParser.detectSectionDivider"/> recognizes
        /// asterisk-wrapped single-cell divider rows with PK qualifier
        /// phrases and extracts both qualifier state and embedded dose.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_detectSectionDivider_AsteriskWrappedSingleDose()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0,
                                        CleanedText = "**Single dose**" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1,
                                        CleanedText = null },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2,
                                        CleanedText = null }
                }
            };

            var result = PkTableParser.detectSectionDivider(row);

            Assert.IsTrue(result.IsDivider);
            Assert.AreEqual("single_dose", result.StickyQualifier);
        }

        /**************************************************************/
        /// <summary>
        /// R3 — Divider with embedded dose and qualifier:
        /// "**500 mg oral tablet single dose, effects of gender and age:**"
        /// Returns qualifier="single_dose", stickyDoseRegimen="500 mg oral tablet".
        /// Mirrors TID 2069 shape.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_detectSectionDivider_DoseAndQualifierExtracted()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0,
                                        CleanedText = "**500 mg oral tablet single dose, effects of gender and age:**" }
                }
            };

            var result = PkTableParser.detectSectionDivider(row);

            Assert.IsTrue(result.IsDivider);
            Assert.AreEqual("single_dose", result.StickyQualifier);
            Assert.IsTrue(result.StickyDoseRegimen != null && result.StickyDoseRegimen.StartsWith("500 mg"),
                $"expected dose fragment to start with '500 mg', got '{result.StickyDoseRegimen}'");
        }

        /**************************************************************/
        /// <summary>
        /// R3 — A normal PK data row (col 0 label + numeric PK cells) must
        /// NOT be detected as a divider. Guards against suppressing legitimate rows.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_detectSectionDivider_DataRowNotMisclassified()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0,
                                        CleanedText = "Healthy Subjects" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1,
                                        CleanedText = "5.5" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2,
                                        CleanedText = "47.5" }
                }
            };

            var result = PkTableParser.detectSectionDivider(row);

            Assert.IsFalse(result.IsDivider,
                "A data row with multiple non-empty cells must not be classified as a divider");
        }

        /**************************************************************/
        /// <summary>
        /// R3 — A single-cell row whose text does NOT contain a PK qualifier
        /// or embedded dose (e.g., "Summary:") must NOT be detected as a
        /// divider. Prevents over-suppression of legitimate title rows.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_detectSectionDivider_NoQualifier_NotDivider()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0,
                                        CleanedText = "Summary:" }
                }
            };

            var result = PkTableParser.detectSectionDivider(row);

            Assert.IsFalse(result.IsDivider,
                "A bare title (no qualifier, no dose) must not be misclassified as a divider");
        }

        /**************************************************************/
        /// <summary>
        /// R3 — End-to-end: a single-column PK table with a "**Single dose**"
        /// section divider between groups of data rows. Pre-R3 the divider
        /// emitted 7 spurious text observations (one per PK column); post-R3
        /// the divider is suppressed AND subsequent data rows inherit the
        /// sticky qualifier as ParameterSubtype with flag
        /// <c>PK_SECTION_QUALIFIER_APPLIED</c>.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_EndToEnd_DividerSuppressedAndQualifierApplied()
        {
            // Build a mini TID 2069 shape — col 0 label + 2 PK value columns
            // with a single-dose divider row embedded between data rows.
            var table = createTestTable(
                new[] { "Regimen", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    // Row 0: section divider (single cell, asterisk-wrapped)
                    new[] { "**Single dose**", null, null },
                    // Row 1: actual data
                    new[] { "250 mg oral", "5.5", "54.4" },
                    new[] { "500 mg oral", "7.0", "67.7" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // Only 2 data rows × 2 PK params = 4 observations (divider suppressed)
            Assert.AreEqual(4, results.Count,
                "Section divider row must NOT emit observations");
            Assert.IsFalse(results.Any(r =>
                (r.RawValue ?? "").Contains("Single dose")),
                "No observation should carry the divider text as RawValue");
            Assert.IsTrue(results.All(r => r.ParameterSubtype == "single_dose"),
                "All post-divider observations should carry the sticky qualifier");
            Assert.IsTrue(results.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_SECTION_QUALIFIER_APPLIED")),
                "Sticky-qualifier attribution flag must fire");
        }

        /**************************************************************/
        /// <summary>
        /// R3.1 — Bare (post-upstream-bold-strip) qualifier text such as
        /// "Single dose" or "Multiple dose" must be detected as a divider.
        /// The Stage 1/2 cell cleaner strips <c>**…**</c> markers, so the
        /// detector sees the bare phrase and — pre-R3.1 — the shell pattern
        /// rejected it. The anchored <c>_bareQualifierDividerPattern</c>
        /// fallback restores divider recognition.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_1_BareAsteriskStrippedDivider_DetectedAsDivider()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0,
                                        CleanedText = "Single dose" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1,
                                        CleanedText = null },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2,
                                        CleanedText = null }
                }
            };

            var result = PkTableParser.detectSectionDivider(row);

            Assert.IsTrue(result.IsDivider,
                "Bare 'Single dose' in a single-cell row must be recognized as a divider");
            Assert.AreEqual("single_dose", result.StickyQualifier);

            // Also verify 'Multiple dose' — the second commonly observed form
            row.Cells[0].CleanedText = "Multiple dose";
            var result2 = PkTableParser.detectSectionDivider(row);
            Assert.IsTrue(result2.IsDivider,
                "Bare 'Multiple dose' in a single-cell row must be recognized as a divider");
            Assert.AreEqual("multiple_dose", result2.StickyQualifier);
        }

        /**************************************************************/
        /// <summary>
        /// R3.1 — End-to-end: a single-column PK table where the section
        /// divider row arrives as plain text "Single dose" (asterisks
        /// stripped by upstream cleaning). Post-R3.1 the divider is
        /// suppressed and subsequent data rows inherit ParameterSubtype =
        /// "single_dose" with flag PK_SECTION_QUALIFIER_APPLIED.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_1_BareAsteriskStrippedDivider_EndToEnd_Suppressed()
        {
            var table = createTestTable(
                new[] { "Regimen", "Cmax (mcg/mL)", "AUC (mcg·h/mL)" },
                new List<string?[]>
                {
                    // Row 0: bare (post-bold-strip) divider
                    new[] { "Single dose", null, null },
                    new[] { "250 mg oral", "5.5", "54.4" },
                    new[] { "500 mg oral", "7.0", "67.7" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(4, results.Count,
                "Bare divider row must NOT emit observations");
            Assert.IsFalse(results.Any(r =>
                (r.RawValue ?? "").Contains("Single dose")),
                "No observation should carry the divider text as RawValue");
            Assert.IsTrue(results.All(r => r.ParameterSubtype == "single_dose"),
                "All post-divider observations should carry the sticky qualifier");
            Assert.IsTrue(results.All(r =>
                (r.ValidationFlags ?? "").Contains("PK_SECTION_QUALIFIER_APPLIED")),
                "Sticky-qualifier attribution flag must fire");
        }

        /**************************************************************/
        /// <summary>
        /// R3.1 — Over-suppression guard: a bare single-cell row whose text
        /// is NOT an exact canonical qualifier phrase (e.g., "Summary",
        /// "Results") must NOT be classified as a divider. The anchored
        /// bare pattern allows only the specific qualifier phrases through.
        /// </summary>
        [TestMethod]
        public void PkParser_R3_1_BareTextWithoutQualifier_NotDivider()
        {
            foreach (var plainText in new[] { "Summary", "Results", "Notes", "Discussion" })
            {
                var row = new ReconstructedRow
                {
                    Classification = RowClassification.DataBody,
                    Cells = new List<ProcessedCell>
                    {
                        new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0,
                                            CleanedText = plainText }
                    }
                };

                var result = PkTableParser.detectSectionDivider(row);

                Assert.IsFalse(result.IsDivider,
                    $"Bare text '{plainText}' (no asterisks, no colon, not a qualifier) must NOT be a divider");
            }
        }

        #endregion PK R3 Section-Divider Suppression

        #region PK R1.2.1 Food-State Sub-Header Suppression

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — A row shaped <c>Food | Fasted | Fed | Fasted | Fed | Fasted | Fed</c>
        /// (TID 3239/29134/33314 shape) must be classified as a food-state sub-header
        /// and suppressed. Pre-R1.2.1 the compound-layout path treated col 0 = "Food"
        /// as a drug-name TreatmentArm and produced one `Arm=Food, Raw=Fasted/Fed`
        /// text_descriptive observation per non-col-0 cell.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_FoodWithFastedFedCells_DetectedAsSubHeader()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "Food" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "Fasted" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "Fed" },
                    new ProcessedCell { SequenceNumber = 4, ResolvedColumnStart = 3, CleanedText = "Fasted" },
                    new ProcessedCell { SequenceNumber = 5, ResolvedColumnStart = 4, CleanedText = "Fed" }
                }
            };

            Assert.IsTrue(PkTableParser.detectFoodStateSubHeader(row),
                "A Food sub-header row with only Fasted/Fed qualifier cells must be recognized");
        }

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — Additional col 0 labels recognized as food descriptors
        /// (Food Effect, Food State, Food Condition, Food Intake, Prandial State,
        /// Fed State). All variants should match when the data cells are food-state
        /// qualifiers.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_AlternateFoodLabels_AllDetected()
        {
            foreach (var label in new[] { "Food Effect", "Food State", "Food Condition", "Food Intake", "Prandial State", "Fed State" })
            {
                var row = new ReconstructedRow
                {
                    Classification = RowClassification.DataBody,
                    Cells = new List<ProcessedCell>
                    {
                        new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = label },
                        new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "Fasted" },
                        new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "Fed" }
                    }
                };

                Assert.IsTrue(PkTableParser.detectFoodStateSubHeader(row),
                    $"Col 0 label '{label}' with food-state cells must be recognized as a sub-header");
            }
        }

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — Food-state cell-pattern coverage: Light Breakfast, High-Fat Meal,
        /// With Food, After Meal variants all match. Anchored pattern means partial
        /// matches (e.g., "Fasted conditions were") should NOT match.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_VariousFoodStateCells_AllRecognized()
        {
            foreach (var cell in new[] { "Light Breakfast", "High-Fat Meal", "High Fat Breakfast", "Low-Fat Meal", "With Food", "After Meal", "Fasting", "Fed state" })
            {
                var row = new ReconstructedRow
                {
                    Classification = RowClassification.DataBody,
                    Cells = new List<ProcessedCell>
                    {
                        new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "Food" },
                        new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = cell }
                    }
                };

                Assert.IsTrue(PkTableParser.detectFoodStateSubHeader(row),
                    $"Food-state cell '{cell}' must be recognized as a qualifier");
            }
        }

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — Guard against over-suppression: a row where col 0 is "Food" but
        /// cols 1..N carry numeric PK values (e.g., from an aberrantly-labeled data
        /// row, not a sub-header) must NOT be detected.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_FoodColZeroWithNumericValues_NotDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "Food" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "5.5 ± 1.1" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "7.0 ± 1.6" }
                }
            };

            Assert.IsFalse(PkTableParser.detectFoodStateSubHeader(row),
                "Numeric PK values in data cells must prevent detection even when col 0 is 'Food'");
        }

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — Guard against over-suppression: a row where col 0 is a drug name
        /// (not a food descriptor) but cells happen to contain food-state words
        /// must NOT be detected. Col 0 allowlist is the gate.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_DrugNameColZero_NotDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "Atorvastatin" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "Fasted" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "Fed" }
                }
            };

            Assert.IsFalse(PkTableParser.detectFoodStateSubHeader(row),
                "Drug-name col 0 must not be treated as a food-state sub-header");
        }

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — Guard: a lone "Food" col 0 with no food-state qualifier cells
        /// (all value cells empty) is NOT a sub-header — prevents suppressing
        /// legitimate-but-empty rows.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_FoodColZeroAllEmptyCells_NotDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "Food" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = null },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "" }
                }
            };

            Assert.IsFalse(PkTableParser.detectFoodStateSubHeader(row),
                "At least one non-empty food-state qualifier cell is required");
        }

        /**************************************************************/
        /// <summary>
        /// R1.2.1 — End-to-end: a compound-layout PK table shaped like TID 3239
        /// with a `Food | Fasted | Fed | Fasted | Fed` sub-header must suppress
        /// the sub-header row's observations entirely. Data rows (Cmax, Tmax)
        /// are emitted as normal.
        /// </summary>
        [TestMethod]
        public void PkParser_R1_2_1_EndToEnd_CompoundLayoutFoodSubHeader_Suppressed()
        {
            // Compound layout: row-1 is sub-header (Col 0 | cols 1..4 are "Younger"/"Older"),
            // subsequent rows have col 0 as a metadata label. The `Food | Fasted | Fed`
            // row should be suppressed.
            var table = createTestTable(
                new[] { "", "Younger", "Younger", "Older", "Older" },
                new List<string?[]>
                {
                    // Row 0: compound sub-header — column labels
                    new[] { "Parameter", "Cmax", "Tmax", "Cmax", "Tmax" },
                    // Row 1: food-state sub-header — MUST be suppressed
                    new[] { "Food", "Fasted", "Fed", "Fasted", "Fed" },
                    // Row 2: actual PK data
                    new[] { "Cmax (ng/mL)", "1816", "3510", "2719", "2915" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.IsFalse(results.Any(r =>
                (r.TreatmentArm ?? "") == "Food" ||
                (r.RawValue ?? "") == "Fasted" ||
                (r.RawValue ?? "") == "Fed"),
                "No observation should carry 'Food' as Arm or 'Fasted'/'Fed' as RawValue after R1.2.1 suppresses the sub-header");
        }

        #endregion PK R1.2.1 Food-State Sub-Header Suppression

        #region PK Wave 3 R10 — Unit Extraction Gap

        /**************************************************************/
        /// <summary>
        /// R10 — Sub-header unit row detection: a row whose col 0 is empty and
        /// whose data cells are all recognized unit strings qualifies as a
        /// sub-header unit row. The detector returns a column→canonical-unit
        /// map that callers use to augment <c>paramDefs</c>.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SubHeaderUnitRow_EmptyCol0_UnitCellsDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "(ng/mL)" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "(mcg·h/mL)" },
                    new ProcessedCell { SequenceNumber = 4, ResolvedColumnStart = 3, CleanedText = "hr" }
                }
            };

            var result = PkTableParser.detectSubHeaderUnitRow(row);
            Assert.IsNotNull(result, "Row with pure-unit data cells and empty col 0 must be detected");
            Assert.AreEqual(3, result!.Count, "All three unit columns must be captured");
            Assert.AreEqual("ng/mL", result[1]);
            Assert.AreEqual("mcg·h/mL", result[2]);
            Assert.AreEqual("h", result[3], "'hr' must normalize to 'h'");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — The conservative two-unit guard: a row with only one unit cell
        /// is too ambiguous (could be a footer, figure annotation, or spillover)
        /// and must NOT be detected as a sub-header unit row.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SubHeaderUnitRow_SingleUnitCell_NotDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "(ng/mL)" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "" },
                    new ProcessedCell { SequenceNumber = 4, ResolvedColumnStart = 3, CleanedText = "" }
                }
            };

            Assert.IsNull(PkTableParser.detectSubHeaderUnitRow(row),
                "A single unit cell in otherwise-empty row is too ambiguous to suppress");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — A row with mixed content (one unit cell + one data cell) must
        /// NOT be detected as a sub-header unit row. The detector requires every
        /// non-empty cell at col &gt; 0 to be a recognized unit.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SubHeaderUnitRow_MixedContent_NotDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "(ng/mL)" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "13.9 ± 2.9" }
                }
            };

            Assert.IsNull(PkTableParser.detectSubHeaderUnitRow(row),
                "Mixed unit + data cells must not be treated as a sub-header unit row");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — Col 0 carrying a recognized label ("Unit", "Units", etc.) is
        /// allowed and does not disqualify the row.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SubHeaderUnitRow_UnitLabelCol0_Detected()
        {
            foreach (var label in new[] { "Unit", "Units", "Parameter", "Dose", "Regimen" })
            {
                var row = new ReconstructedRow
                {
                    Classification = RowClassification.DataBody,
                    Cells = new List<ProcessedCell>
                    {
                        new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = label },
                        new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "ng/mL" },
                        new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "mcg/mL" }
                    }
                };

                Assert.IsNotNull(PkTableParser.detectSubHeaderUnitRow(row),
                    $"Col 0 label '{label}' with unit cells must be detected");
            }
        }

        /**************************************************************/
        /// <summary>
        /// R10 — A drug-name or narrative in col 0 disqualifies the row, even
        /// when cells happen to be unit-shaped.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SubHeaderUnitRow_DrugNameCol0_NotDetected()
        {
            var row = new ReconstructedRow
            {
                Classification = RowClassification.DataBody,
                Cells = new List<ProcessedCell>
                {
                    new ProcessedCell { SequenceNumber = 1, ResolvedColumnStart = 0, CleanedText = "Fluconazole" },
                    new ProcessedCell { SequenceNumber = 2, ResolvedColumnStart = 1, CleanedText = "ng/mL" },
                    new ProcessedCell { SequenceNumber = 3, ResolvedColumnStart = 2, CleanedText = "hr" }
                }
            };

            Assert.IsNull(PkTableParser.detectSubHeaderUnitRow(row),
                "A drug-name col 0 must prevent sub-header unit detection");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — <c>applySubHeaderUnitAugmentation</c> fills null-unit entries
        /// in paramDefs but preserves non-null units extracted from the primary
        /// header. Returns the count of entries augmented.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_ApplySubHeaderUnitAugmentation_PreservesHeaderUnits()
        {
            var paramDefs = new List<(int columnIndex, string name, string? unit, bool isTimeMeasure, bool isSampleSize)>
            {
                (1, "Cmax", null, false, false),       // No header unit → augmented
                (2, "AUC0-24", "mcg·h/mL", false, false), // Has header unit → preserved
                (3, "Tmax", null, false, false)         // No header unit → augmented
            };

            var unitsByColumn = new Dictionary<int, string>
            {
                [1] = "ng/mL",
                [2] = "ng·h/mL",   // Different unit — must be ignored (header wins)
                [3] = "h"
            };

            int augmented = PkTableParser.applySubHeaderUnitAugmentation(paramDefs, unitsByColumn);

            Assert.AreEqual(2, augmented, "Two null-unit entries must be augmented");
            Assert.AreEqual("ng/mL", paramDefs[0].unit, "Cmax null unit filled");
            Assert.AreEqual("mcg·h/mL", paramDefs[1].unit, "AUC0-24 header unit preserved (not overwritten)");
            Assert.AreEqual("h", paramDefs[2].unit, "Tmax null unit filled");
            Assert.IsTrue(paramDefs[2].isTimeMeasure, "Tmax with unit 'h' must be flagged as time-measure");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — Sibling-unit majority vote: when most observations sharing the
        /// same ParameterName have the same Unit, null-unit siblings are
        /// backfilled with that majority value and flagged
        /// <c>PK_UNIT_SIBLING_VOTED</c>.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SiblingUnitVote_MajorityBackfillsOrphans()
        {
            var observations = new List<ParsedObservation>
            {
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL" },
                new ParsedObservation { ParameterName = "Cmax", Unit = null },   // orphan
                new ParsedObservation { ParameterName = "AUC", Unit = "mcg·h/mL" },
                new ParsedObservation { ParameterName = "AUC", Unit = null }     // orphan
            };

            int backfilled = PkTableParser.applySiblingUnitVote(observations);

            Assert.AreEqual(2, backfilled, "Both orphan observations must be backfilled");
            Assert.AreEqual("ng/mL", observations[3].Unit);
            Assert.AreEqual("mcg·h/mL", observations[5].Unit);
            StringAssert.Contains(observations[3].ValidationFlags ?? "", "PK_UNIT_SIBLING_VOTED");
            StringAssert.Contains(observations[5].ValidationFlags ?? "", "PK_UNIT_SIBLING_VOTED");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — Sibling-vote guard: when siblings exhibit mixed units with no
        /// strict majority, orphan rows are left null (conservative — mixed
        /// groups are ambiguous).
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SiblingUnitVote_MixedUnitsNoMajority_LeavesOrphanNull()
        {
            var observations = new List<ParsedObservation>
            {
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "mcg/mL" },
                new ParsedObservation { ParameterName = "Cmax", Unit = null }
            };

            int backfilled = PkTableParser.applySiblingUnitVote(observations);

            Assert.AreEqual(0, backfilled, "No majority means no backfill");
            Assert.IsNull(observations[2].Unit);
        }

        /**************************************************************/
        /// <summary>
        /// R10 — Sibling-vote guard: a parameter group of size 1 does not
        /// qualify — cannot vote from a single sibling.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_SiblingUnitVote_SingleObservation_NoOp()
        {
            var observations = new List<ParsedObservation>
            {
                new ParsedObservation { ParameterName = "Cmax", Unit = null }
            };

            int backfilled = PkTableParser.applySiblingUnitVote(observations);

            Assert.AreEqual(0, backfilled);
            Assert.IsNull(observations[0].Unit);
        }

        /**************************************************************/
        /// <summary>
        /// PR #1 Area B — when paren-dispersion–rescued rows dominate a sibling group,
        /// the unit-vote threshold relaxes from strict majority to plurality. A single
        /// consistent "ng/mL" on two rescued rows — insufficient under the strict rule
        /// (2 / (2 + 2 other rescued) = 50% is not &gt; 50%) — now backfills successfully
        /// when the winning value is strictly unique.
        /// </summary>
        [TestMethod]
        public void PkParser_SiblingUnitVote_ParenDispersionDominant_UsesLoweredThreshold()
        {
            #region implementation

            // 4 rescue-origin rows share a ParameterName. 2 have Unit="ng/mL", 2 have
            // Unit=null. Strict majority would reject (2 of 2 non-null = 100% sounds
            // majority, but the empty rows don't count in denominator — still, the
            // strict path accepts this via existing logic, so make it a harder case):
            // 3 rescued rows with null Unit + 1 rescued row with "ng/mL" + 0 others.
            // Under strict majority: 1 of 1 non-null = 100% meets the > 50% test,
            // so rescue boost isn't required. To force the boost path we need multiple
            // distinct non-null units where the winner is below strict-majority but
            // above plurality AND rescue rows dominate.
            var observations = new List<ParsedObservation>
            {
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL", ParseRule = "value_paren_dispersion" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL", ParseRule = "value_paren_dispersion" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "mcg/mL", ParseRule = "value_paren_dispersion" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "pg/mL",  ParseRule = "value_paren_dispersion+caption" },
                new ParsedObservation { ParameterName = "Cmax", Unit = null,     ParseRule = "value_paren_dispersion+caption" },
                new ParsedObservation { ParameterName = "Cmax", Unit = null,     ParseRule = "value_paren_dispersion+caption" },
            };
            // Non-null count = 4 (ng/mL=2, mcg/mL=1, pg/mL=1). Strict majority needs winner*2 > 4,
            // so winner needs > 2 — 2 does not qualify. Rescue-dominant (6/6 rescued → ≥ 50%),
            // plurality threshold 4*? → winner*3 >= 4 means winner >= 1.33 (i.e. ≥ 2). Winner (2)
            // is strictly unique (next distinct has 1). → backfill under rescue boost.

            int backfilled = PkTableParser.applySiblingUnitVote(observations);

            Assert.AreEqual(2, backfilled, "Both orphan rows should be backfilled via rescue boost");
            Assert.AreEqual("ng/mL", observations[4].Unit);
            Assert.AreEqual("ng/mL", observations[5].Unit);
            StringAssert.Contains(observations[4].ValidationFlags ?? "",
                "PK_UNIT_SIBLING_VOTED:RESCUE_BOOST");
            StringAssert.Contains(observations[5].ValidationFlags ?? "",
                "PK_UNIT_SIBLING_VOTED:RESCUE_BOOST");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PR #1 Area B — when rescue rows are the minority, the strict-majority rule
        /// still applies. A 50/50 mix cannot dilute the protection that shields healthy
        /// groups from being over-imputed.
        /// </summary>
        [TestMethod]
        public void PkParser_SiblingUnitVote_ParenDispersionMinority_KeepsMajorityThreshold()
        {
            #region implementation

            // 5 siblings: 2 rescue rows + 3 non-rescue rows. Rescue is minority (2/5 = 40%).
            // Units: ng/mL=2, mcg/mL=1, pg/mL=1; one orphan. Winner (ng/mL, 2) does not meet
            // strict majority (2*2=4 is not > 4). Rescue boost should NOT activate because
            // rescue is not dominant. Orphan must stay null.
            var observations = new List<ParsedObservation>
            {
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL",  ParseRule = "plain_number" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL",  ParseRule = "plain_number" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "mcg/mL", ParseRule = "plain_number" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "pg/mL",  ParseRule = "value_paren_dispersion" },
                new ParsedObservation { ParameterName = "Cmax", Unit = null,     ParseRule = "value_paren_dispersion" },
            };

            int backfilled = PkTableParser.applySiblingUnitVote(observations);

            Assert.AreEqual(0, backfilled, "Non-rescue-dominant groups must keep the strict-majority rule");
            Assert.IsNull(observations[4].Unit);

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// PR #1 Area B — a healthy group where strict majority already passes should
        /// carry <c>PK_UNIT_SIBLING_VOTED</c> but NOT <c>:RESCUE_BOOST</c>. The secondary
        /// flag is reserved for cases where the relaxed threshold was the decider.
        /// </summary>
        [TestMethod]
        public void PkParser_SiblingUnitVote_RescueBoost_EmitsDiagnosticFlagOnlyWhenDecisive()
        {
            #region implementation

            // 3 rescue rows + 1 orphan: "ng/mL" on all three non-null rows → strict majority
            // applies (3/3 = 100% > 50%) AND rescue dominates (3/4 ≥ 50%). The rescue boost
            // exists but was not NEEDED to decide the vote, so the :RESCUE_BOOST flag must
            // not be emitted — only the base PK_UNIT_SIBLING_VOTED.
            var observations = new List<ParsedObservation>
            {
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL", ParseRule = "value_paren_dispersion" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL", ParseRule = "value_paren_dispersion" },
                new ParsedObservation { ParameterName = "Cmax", Unit = "ng/mL", ParseRule = "value_paren_dispersion" },
                new ParsedObservation { ParameterName = "Cmax", Unit = null,    ParseRule = "value_paren_dispersion" },
            };

            int backfilled = PkTableParser.applySiblingUnitVote(observations);

            Assert.AreEqual(1, backfilled);
            var flags = observations[3].ValidationFlags ?? string.Empty;
            StringAssert.Contains(flags, "PK_UNIT_SIBLING_VOTED");
            Assert.IsFalse(flags.Contains("PK_UNIT_SIBLING_VOTED:RESCUE_BOOST"),
                "RESCUE_BOOST must only fire when it was the deciding factor");

            #endregion
        }

        /**************************************************************/
        /// <summary>
        /// R10 — End-to-end: a PK table whose column headers carry param names
        /// without units AND whose second row is a sub-header unit row must
        /// produce observations with Unit populated from the sub-header.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_EndToEnd_SubHeaderUnitRow_AugmentsParamDefs()
        {
            // Primary header has no parenthesized units — just bare "Cmax", "Tmax", "AUC".
            // First data row is a sub-header unit row "(ng/mL) | hr | (ng·h/mL)".
            // Second data row carries actual PK values.
            var table = createTestTable(
                new[] { "Regimen", "Cmax", "Tmax", "AUC" },
                new List<string?[]>
                {
                    new[] { "", "(ng/mL)", "hr", "(ng·h/mL)" },      // sub-header unit row
                    new[] { "100 mg single oral", "5.5", "2.0", "45.2" } // data
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            // Expect 3 observations (one per param column of the data row);
            // the sub-header unit row must produce no observations.
            Assert.AreEqual(3, results.Count, "Only the single data row should emit observations");

            var cmax = results.FirstOrDefault(r => r.ParameterName == "Cmax");
            var tmax = results.FirstOrDefault(r => r.ParameterName == "Tmax");
            var auc = results.FirstOrDefault(r => r.ParameterName == "AUC");

            Assert.IsNotNull(cmax, "Cmax observation missing");
            Assert.IsNotNull(tmax, "Tmax observation missing");
            Assert.IsNotNull(auc, "AUC observation missing");

            Assert.AreEqual("ng/mL", cmax!.Unit, "Cmax unit should flow from sub-header");
            Assert.AreEqual("h", tmax!.Unit, "Tmax unit should flow from sub-header (hr → h)");
            Assert.AreEqual("ng·h/mL", auc!.Unit, "AUC unit should flow from sub-header");

            // Sub-header unit row's own cells must not appear as raw values
            Assert.IsFalse(results.Any(r => (r.RawValue ?? "") == "(ng/mL)"),
                "Unit cells must not leak as data observations");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — End-to-end: inline cell-text unit scan picks up units embedded
        /// in data cells when the header does not provide one. E.g., the cell
        /// <c>"13.8 hr (6.4)"</c> must yield <c>Unit = "h"</c> with flag
        /// <c>PK_UNIT_FROM_CELL</c>.
        /// </summary>
        [TestMethod]
        public void PkParser_R10_EndToEnd_CellInlineUnit_FlagsAppended()
        {
            // Header has bare "t½" with no unit, data cell has inline "hr".
            var table = createTestTable(
                new[] { "Regimen", "t½" },
                new List<string?[]>
                {
                    new[] { "100 mg single oral", "13.8 hr (6.4)" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            var obs = results[0];
            Assert.AreEqual("h", obs.Unit, "Cell-inline 'hr' should be extracted and normalized");
            StringAssert.Contains(obs.ValidationFlags ?? "", "PK_UNIT_FROM_CELL");
        }

        /**************************************************************/
        /// <summary>
        /// R10 — Precedence guard: header-carried units (via <c>Name (unit)</c>
        /// pattern) must continue to win over cell-inline and sub-header scans.
        /// A cell like <c>"13.8 hr"</c> under header <c>"t½ (h)"</c> should get
        /// Unit = "h" without the <c>PK_UNIT_FROM_CELL</c> flag (header wins).
        /// </summary>
        [TestMethod]
        public void PkParser_R10_HeaderUnitTakesPrecedence_OverCellInline()
        {
            var table = createTestTable(
                new[] { "Regimen", "t½ (h)" },
                new List<string?[]>
                {
                    new[] { "100 mg single oral", "13.8 hr" }
                },
                parentSectionCode: "34090-1");

            var parser = new PkTableParser();
            var results = parser.Parse(table);

            Assert.AreEqual(1, results.Count);
            var obs = results[0];
            Assert.AreEqual("h", obs.Unit, "Header unit should populate Unit");
            Assert.IsFalse((obs.ValidationFlags ?? "").Contains("PK_UNIT_FROM_CELL"),
                "Header unit precedence means cell-inline scan must not fire");
        }

        #endregion PK Wave 3 R10 — Unit Extraction Gap
    }
}

